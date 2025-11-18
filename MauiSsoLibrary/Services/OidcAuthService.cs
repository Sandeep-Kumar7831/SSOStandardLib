
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.Browser;
using System;
using System.Threading.Tasks;

namespace MauiSsoLibrary.Services
{
    public class OidcAuthService : IOidcAuthService
    {
        private readonly OidcClient _oidcClient;
        private readonly ITokenStore _tokenStore;
        private readonly SsoConfiguration _config;

        public OidcAuthService(
            ITokenStore tokenStore,
            SsoConfiguration config,
            IBrowser browser)
        {
            _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));

            if (!config.IsValid())
                throw new ArgumentException($"Invalid SSO configuration. Errors: {string.Join(", ", config.GetValidationErrors())}");

            _config = config;

            System.Diagnostics.Debug.WriteLine("\n========== OIDC Configuration ==========");
            System.Diagnostics.Debug.WriteLine($"Authority: {config.Authority}");
            System.Diagnostics.Debug.WriteLine($"ClientId: {config.ClientId}");
            System.Diagnostics.Debug.WriteLine($"RedirectUri: {config.RedirectUri}");
            System.Diagnostics.Debug.WriteLine($"Scope: {config.Scope}");
            System.Diagnostics.Debug.WriteLine($"EnableDPoP: {config.EnableDPoP}");
            System.Diagnostics.Debug.WriteLine("=======================================\n");

            var options = new OidcClientOptions
            {
                Authority = config.Authority,
                ClientId = config.ClientId,
                ClientSecret = config.ClientSecret,
                Scope = config.Scope,
                RedirectUri = config.RedirectUri,
                PostLogoutRedirectUri = config.PostLogoutRedirectUri,
                Browser = browser,
                LoadProfile = true,
            };

            _oidcClient = new OidcClient(options);
        }

        public async Task<AuthResult> LoginAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("\n========== Starting Login ==========");

                var result = await _oidcClient.LoginAsync(new LoginRequest());

                System.Diagnostics.Debug.WriteLine($"Login Result - IsError: {result.IsError}");
                System.Diagnostics.Debug.WriteLine($"Error: {result.Error}");
                System.Diagnostics.Debug.WriteLine($"ErrorDescription: {result.ErrorDescription}");

                if (result.IsError)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Login Error: {result.Error} - {result.ErrorDescription}");

                    return new AuthResult
                    {
                        IsSuccess = false,
                        Error = result.Error,
                        ErrorDescription = result.ErrorDescription
                    };
                }

                System.Diagnostics.Debug.WriteLine($"✓ Login successful");
                System.Diagnostics.Debug.WriteLine($"AccessToken: {(result.AccessToken != null ? result.AccessToken.Substring(0, 20) + "..." : "null")}");

                var tokenResponse = new TokenResponse
                {
                    AccessToken = result.AccessToken ?? string.Empty,
                    RefreshToken = result.RefreshToken ?? string.Empty,
                    IdToken = result.IdentityToken ?? string.Empty,
                    ExpiresAt = result.AccessTokenExpiration
                };

                await _tokenStore.SaveTokensAsync(tokenResponse);

                System.Diagnostics.Debug.WriteLine($"✓ Tokens saved to store");

                return new AuthResult
                {
                    IsSuccess = true,
                    AccessToken = result.AccessToken
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Login Exception: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");

                return new AuthResult
                {
                    IsSuccess = false,
                    Error = "Exception",
                    ErrorDescription = ex.Message
                };
            }
        }

        public async Task<AuthResult> RefreshTokenAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("\n========== Starting Token Refresh ==========");

                var refreshToken = _tokenStore.GetRefreshToken();
                if (string.IsNullOrEmpty(refreshToken))
                {
                    System.Diagnostics.Debug.WriteLine("✗ No refresh token available");
                    return new AuthResult
                    {
                        IsSuccess = false,
                        Error = "NoRefreshToken"
                    };
                }

                var result = await _oidcClient.RefreshTokenAsync(refreshToken);

                if (result.IsError)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Refresh Error: {result.Error} - {result.ErrorDescription}");
                    return new AuthResult
                    {
                        IsSuccess = false,
                        Error = result.Error,
                        ErrorDescription = result.ErrorDescription
                    };
                }

                System.Diagnostics.Debug.WriteLine($"✓ Token refresh successful");

                var tokenResponse = new TokenResponse
                {
                    AccessToken = result.AccessToken ?? string.Empty,
                    RefreshToken = result.RefreshToken ?? string.Empty,
                    IdToken = result.IdentityToken ?? string.Empty,
                    ExpiresAt = result.AccessTokenExpiration
                };

                await _tokenStore.SaveTokensAsync(tokenResponse);

                return new AuthResult { IsSuccess = true };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Refresh Exception: {ex.Message}\n{ex.StackTrace}");
                return new AuthResult
                {
                    IsSuccess = false,
                    Error = "Exception",
                    ErrorDescription = ex.Message
                };
            }
        }

        public async Task LogoutAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("\n========== Starting Logout ==========");

                var idToken = _tokenStore.GetIdToken();
                if (!string.IsNullOrEmpty(idToken))
                {
                    await _oidcClient.LogoutAsync(new LogoutRequest { IdTokenHint = idToken });
                }

                _tokenStore.ClearTokens();
                System.Diagnostics.Debug.WriteLine("✓ Logout successful");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Logout error: {ex.Message}");
            }
        }

        public bool IsAuthenticated() => _tokenStore.IsAuthenticated();

        public string? GetAccessToken() => _tokenStore.GetAccessToken();
    }
}