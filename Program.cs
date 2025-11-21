using spotify_controller.Models;
using spotify_controller.Services;

using DotNetEnv;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.Configure<SpotifyConfig>(cfg =>
{
    cfg.ClientId = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID") ?? "";
    cfg.ClientSecret = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_SECRET") ?? "";
    cfg.RedirectUri = Environment.GetEnvironmentVariable("SPOTIFY_REDIRECT_URI") ?? "";
});

builder.Services.AddHttpClient();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// app.UseHttpsRedirection();

app.MapGet("/login", (IOptions<SpotifyConfig> cfg) =>
{
    var c = cfg.Value;

    var scope = Uri.EscapeDataString("user-modify-playback-state user-read-playback-state playlist-modify-private playlist-modify-public");

    var url =
        $"https://accounts.spotify.com/authorize?" +
        $"response_type=code&client_id={c.ClientId}&scope={scope}&redirect_uri={Uri.EscapeDataString(c.RedirectUri)}";

    return Results.Redirect(url);
});

app.MapGet("/callback", async (string code, IHttpClientFactory httpFactory, IOptions<SpotifyConfig> cfg) =>
{
    var c = cfg.Value;
    var http = httpFactory.CreateClient();

    var request = new HttpRequestMessage(
        HttpMethod.Post,
        "https://accounts.spotify.com/api/token"
    );
    var body = new Dictionary<string, string>
    {
        ["grant_type"] = "authorization_code",
        ["code"] = code,
        ["redirect_uri"] = c.RedirectUri,
        ["client_id"] = c.ClientId,
        ["client_secret"] = c.ClientSecret
    };
    request.Content = new FormUrlEncodedContent(body);

    var response = await http.SendAsync(request);

    var json = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
        return Results.BadRequest(json);
    
    var token = System.Text.Json.JsonSerializer.Deserialize<SpotifyTokenResponse>(json);

    TokenStore.AccessToken = token!.AccessToken;
    TokenStore.RefreshToken = token.RefreshToken;
    
    return Results.Ok("you can now call /play.");
});

app.MapPut("/play", async (IHttpClientFactory httpFactory) =>
{   
    var http = httpFactory.CreateClient();
    
    if (string.IsNullOrEmpty(TokenStore.AccessToken))
        return Results.Unauthorized();
    
    var request = new HttpRequestMessage(
        HttpMethod.Put,
        "https://api.spotify.com/v1/me/player/play"
    );
    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TokenStore.AccessToken);
    request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

    var response = await http.SendAsync(request);

    var json = await response.Content.ReadAsStringAsync();

    return Results.Text(json, "application/json");
});

app.MapPut("/pause", async (IHttpClientFactory httpFactory) =>
{
    var http = httpFactory.CreateClient();

    if (string.IsNullOrEmpty(TokenStore.AccessToken))
        return Results.Unauthorized();

    var request = new HttpRequestMessage(
        HttpMethod.Put,
        "https://api.spotify.com/v1/me/player/pause"
    );
    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TokenStore.AccessToken);
    request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

    var response = await http.SendAsync(request);

    var json = await response.Content.ReadAsStringAsync();

    return Results.Text(json, "application/json");
});

app.MapGet("/devices", async (IHttpClientFactory httpFactory) =>
{
    var http = httpFactory.CreateClient();
    
    if (string.IsNullOrEmpty(TokenStore.AccessToken))
        return Results.Unauthorized();
    
    var request = new HttpRequestMessage(
        HttpMethod.Get,
        "https://api.spotify.com/v1/me/player/devices"
    );
    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TokenStore.AccessToken);

    var response = await http.SendAsync(request);
    
    return Results.Text(await response.Content.ReadAsStringAsync());
});

app.MapGet("/queue", async (IHttpClientFactory httpFactory) =>
{
    var http = httpFactory.CreateClient();
    
    if (string.IsNullOrEmpty(TokenStore.AccessToken))
        return Results.Unauthorized();
    
    var request = new HttpRequestMessage(
        HttpMethod.Get,
        "https://api.spotify.com/v1/me/player/queue"
    );
    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TokenStore.AccessToken);

    var response = await http.SendAsync(request);
    
    return Results.Text(await response.Content.ReadAsStringAsync());
});

app.MapGet("/playlists", async (IHttpClientFactory httpFactory) =>
{
    var http = httpFactory.CreateClient();
    
    if (string.IsNullOrEmpty(TokenStore.AccessToken))
        return Results.Unauthorized();

    var request = new HttpRequestMessage(
        HttpMethod.Get,
        "https://api.spotify.com/v1/me/playlists"
    );
    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TokenStore.AccessToken);
    
    var response = await http.SendAsync(request);
    
    return Results.Text(await response.Content.ReadAsStringAsync());
});

app.MapPost("/playlists", async (IHttpClientFactory httpFactory) =>
{
    var http = httpFactory.CreateClient();
    
    if (string.IsNullOrEmpty(TokenStore.AccessToken))
        return Results.Unauthorized();

    var request = new HttpRequestMessage(
        HttpMethod.Post,
        "https://api.spotify.com/v1/me/playlists"
    );
    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TokenStore.AccessToken);
    var body = new Dictionary<string, object>
    {
        ["name"] = "loop-playlist",
        ["description"] = "created for your current loop",
        ["public"] = false
    };
    
    var json = System.Text.Json.JsonSerializer.Serialize(body);

    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

    var response = await http.SendAsync(request);

    var result = await response.Content.ReadAsStringAsync();

    return Results.Text(result, "application/json");
});

app.MapGet("/queue-loop", async (IHttpClientFactory httpFactory) =>
{
    var http = httpFactory.CreateClient();
    
    if (string.IsNullOrEmpty(TokenStore.AccessToken))
        return Results.Unauthorized();
    
    var queueRequest = new HttpRequestMessage(
        HttpMethod.Get,
        "https://api.spotify.com/v1/me/player/queue"
    );
    queueRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TokenStore.AccessToken);
    
    var queueResponse = await http.SendAsync(queueRequest);
    var queueJson = await queueResponse.Content.ReadAsStringAsync();
    
    if (!queueResponse.IsSuccessStatusCode)
        return Results.BadRequest(queueJson);
    
    var queueData = JsonSerializer.Deserialize<JsonElement>(queueJson);
    var uniqueTrackUris = new HashSet<string>();
    
    if (queueData.TryGetProperty("queue", out var queue))
    {
        foreach (var track in queue.EnumerateArray())
        {
            if (track.TryGetProperty("uri", out var uri))
            {
                uniqueTrackUris.Add(uri.GetString()!);
            }
        }
    }
    
    var trackUris = uniqueTrackUris.ToList();
    
    if (trackUris.Count == 0)
        return Results.BadRequest("No tracks in queue");
    
    var userRequest = new HttpRequestMessage(
        HttpMethod.Get,
        "https://api.spotify.com/v1/me"
    );
    userRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TokenStore.AccessToken);
    
    var userResponse = await http.SendAsync(userRequest);
    var userData = await userResponse.Content.ReadAsStringAsync();
    
    if (!userResponse.IsSuccessStatusCode)
        return Results.BadRequest(userData);
    
    var userJson = JsonSerializer.Deserialize<JsonElement>(userData);
    var userId = userJson.GetProperty("id").GetString();
    
    var createPlaylistRequest = new HttpRequestMessage(
        HttpMethod.Post,
        $"https://api.spotify.com/v1/users/{userId}/playlists"
    );
    createPlaylistRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TokenStore.AccessToken);
    
    var createPlaylistBody = new Dictionary<string, object>
    {
        ["name"] = "temporary-playlist",
        ["description"] = "created for your current loop",
        ["public"] = false
    };
    
    var createPlaylistJson = JsonSerializer.Serialize(createPlaylistBody);
    createPlaylistRequest.Content = new StringContent(createPlaylistJson, Encoding.UTF8, "application/json");
    
    var createPlaylistResponse = await http.SendAsync(createPlaylistRequest);
    var playlistData = await createPlaylistResponse.Content.ReadAsStringAsync();
    
    if (!createPlaylistResponse.IsSuccessStatusCode)
        return Results.BadRequest(playlistData);
    
    var playlistJson = JsonSerializer.Deserialize<JsonElement>(playlistData);
    var playlistId = playlistJson.GetProperty("id").GetString();
    var playlistUri = playlistJson.GetProperty("uri").GetString();
    
    var addTracksRequest = new HttpRequestMessage(
        HttpMethod.Post,
        $"https://api.spotify.com/v1/playlists/{playlistId}/tracks"
    );
    addTracksRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TokenStore.AccessToken);
    
    var addTracksBody = new Dictionary<string, object>
    {
        ["uris"] = trackUris
    };
    
    var addTracksJson = JsonSerializer.Serialize(addTracksBody);
    addTracksRequest.Content = new StringContent(addTracksJson, Encoding.UTF8, "application/json");
    
    var addTracksResponse = await http.SendAsync(addTracksRequest);
    
    if (!addTracksResponse.IsSuccessStatusCode)
    {
        var error = await addTracksResponse.Content.ReadAsStringAsync();
        return Results.BadRequest(error);
    }
    
    var playRequest = new HttpRequestMessage(
        HttpMethod.Put,
        "https://api.spotify.com/v1/me/player/play"
    );
    playRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TokenStore.AccessToken);
    
    var playBody = new Dictionary<string, object>
    {
        ["context_uri"] = playlistUri!
    };
    
    var playJson = JsonSerializer.Serialize(playBody);
    playRequest.Content = new StringContent(playJson, Encoding.UTF8, "application/json");
    
    var playResponse = await http.SendAsync(playRequest);
    
    if (!playResponse.IsSuccessStatusCode)
    {
        var error = await playResponse.Content.ReadAsStringAsync();
        return Results.BadRequest(error);
    }
    
    var repeatRequest = new HttpRequestMessage(
        HttpMethod.Put,
        "https://api.spotify.com/v1/me/player/repeat?state=context"
    );
    repeatRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TokenStore.AccessToken);
    repeatRequest.Content = new StringContent("{}", Encoding.UTF8, "application/json");
    
    var repeatResponse = await http.SendAsync(repeatRequest);
    
    if (!repeatResponse.IsSuccessStatusCode)
    {
        var error = await repeatResponse.Content.ReadAsStringAsync();
        return Results.BadRequest($"playlist created and playing, but repeat mode failed: {error}");
    }
    
    var result = new Dictionary<string, object>
    {
        ["message"] = "queue converted to looping playlist",
        ["playlistId"] = playlistId!,
        ["playlistUri"] = playlistUri!,
        ["trackCount"] = trackUris.Count
    };
    
    return Results.Ok(result);
});

app.Run();
