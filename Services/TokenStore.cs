namespace spotify_controller.Services;

public static class TokenStore
{
    public static string AccessToken { get; set; } = string.Empty;
    
    public static string RefreshToken { get; set; } = string.Empty;
}
