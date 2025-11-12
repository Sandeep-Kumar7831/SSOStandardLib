namespace MauiSsoLibrary.Services
{
    /// <summary>
    /// Token storage interface - implement per platform as needed
    /// </summary>
    public interface ITokenStore
    {
        string? GetAccessToken();
        string? GetRefreshToken();
        string? GetIdToken();
        string? GetDPoPJwk();
        void SaveDPoPJwk(string jwkJson);
        bool IsAuthenticated();
        Task SaveTokensAsync(TokenResponse tokens);
        void ClearTokens();
    }

    public class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string IdToken { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public string TokenType { get; set; } = "Bearer";
    }
}