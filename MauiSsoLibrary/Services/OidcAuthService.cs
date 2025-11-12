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

        public OidcAuthService(
            ITokenStore tokenStore,
            SsoConfiguration config,
            IBrowser browser)
        {
            _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));

            if (!config.IsValid())
                throw new ArgumentException($"Invalid SSO configuration. Errors: {string.Join(", ", config.GetValidationErrors())}");

            var options = new OidcClientOptions
            {
                Authority = config.Authority,
                ClientId = config.ClientId,
                ClientSecret = config.ClientSecret,
                Scope = config.Scope,
                RedirectUri = config.RedirectUri,
                PostLogoutRedirectUri = config.PostLogoutRedirectUri,
                Browser = browser
            };

            _oidcClient = new OidcClient(options);
        }

        public async Task<AuthResult> LoginAsync()
        {
            try
            {
                var result = await _oidcClient.LoginAsync(new LoginRequest());

                if (result.IsError)
                {
                    return new AuthResult
                    {
                        IsSuccess = false,
                        Error = result.Error,
                        ErrorDescription = result.ErrorDescription
                    };
                }

                var tokenResponse = new TokenResponse
                {
                    AccessToken = result.AccessToken ?? string.Empty,
                    RefreshToken = result.RefreshToken ?? string.Empty,
                    IdToken = result.IdentityToken ?? string.Empty,
                    ExpiresAt = result.AccessTokenExpiration
                };

                await _tokenStore.SaveTokensAsync(tokenResponse);

                return new AuthResult
                {
                    IsSuccess = true,
                    AccessToken = result.AccessToken
                };
            }
            catch (Exception ex)
            {
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
                var refreshToken = _tokenStore.GetRefreshToken();
                if (string.IsNullOrEmpty(refreshToken))
                {
                    return new AuthResult
                    {
                        IsSuccess = false,
                        Error = "NoRefreshToken"
                    };
                }

                var result = await _oidcClient.RefreshTokenAsync(refreshToken);

                if (result.IsError)
                {
                    return new AuthResult
                    {
                        IsSuccess = false,
                        Error = result.Error,
                        ErrorDescription = result.ErrorDescription
                    };
                }

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
                var idToken = _tokenStore.GetIdToken();
                if (!string.IsNullOrEmpty(idToken))
                {
                    await _oidcClient.LogoutAsync(new LogoutRequest { IdTokenHint = idToken });
                }

                _tokenStore.ClearTokens();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Logout error: {ex.Message}");
            }
        }

        public bool IsAuthenticated() => _tokenStore.IsAuthenticated();

        public string? GetAccessToken() => _tokenStore.GetAccessToken();
    }
}