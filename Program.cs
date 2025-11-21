using spotify_controller.Models;
using spotify_controller.Services;

using DotNetEnv;
using Microsoft.Extensions.Options;

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

    var scope = Uri.EscapeDataString("user-modify-playback-state user-read-playback-state");

    var url =
        $"https://accounts.spotify.com/authorize?" +
        $"response_type=code&client_id={c.ClientId}&scope={scope}&redirect_uri={Uri.EscapeDataString(c.RedirectUri)}";

    return Results.Redirect(url);
});

app.MapGet("/callback", async (string code, IHttpClientFactory httpFactory, IOptions<SpotifyConfig> cfg) =>
{
    var c = cfg.Value;
    var http = httpFactory.CreateClient();

    var body = new Dictionary<string, string>
    {
        ["grant_type"] = "authorization_code",
        ["code"] = code,
        ["redirect_uri"] = c.RedirectUri,
        ["client_id"] = c.ClientId,
        ["client_secret"] = c.ClientSecret
    };

    var response = await http.PostAsync(
        "https://accounts.spotify.com/api/token",
        new FormUrlEncodedContent(body)
    );

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

    request.Headers.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TokenStore.AccessToken);

    request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

    var response = await http.SendAsync(request);

    var json = await response.Content.ReadAsStringAsync();

    return Results.Text(json, "application/json");
});

app.MapGet("/devices", async (IHttpClientFactory httpFactory) =>
{
    var http = httpFactory.CreateClient();
    http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TokenStore.AccessToken);

    var response = await http.GetAsync("https://api.spotify.com/v1/me/player/devices");
    return Results.Text(await response.Content.ReadAsStringAsync());
});

app.MapGet("/queue", async (IHttpClientFactory httpFactory) =>
{
    var http = httpFactory.CreateClient();
    http.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TokenStore.AccessToken);

    var response = await http.GetAsync("https://api.spotify.com/v1/me/player/queue");
    return Results.Text(await response.Content.ReadAsStringAsync());
});

app.Run();
