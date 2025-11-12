using System.Threading.Tasks;

namespace MauiSsoLibrary.Services
{
    public interface IOidcAuthService
    {
        Task<AuthResult> LoginAsync();
        Task<AuthResult> RefreshTokenAsync();
        Task LogoutAsync();
        bool IsAuthenticated();
        string? GetAccessToken();
    }

    public class AuthResult
    {
        public bool IsSuccess { get; set; }
        public string? Error { get; set; }
        public string? ErrorDescription { get; set; }
        public string? AccessToken { get; set; }
    }
}