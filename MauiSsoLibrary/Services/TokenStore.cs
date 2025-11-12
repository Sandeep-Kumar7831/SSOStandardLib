using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MauiSsoLibrary.Services
{
    /// <summary>
    /// In-memory token store - inherit and override for persistent storage
    /// </summary>
    public class TokenStore : ITokenStore
    {
        private const string ACCESS_TOKEN_KEY = "sso_access_token";
        private const string REFRESH_TOKEN_KEY = "sso_refresh_token";
        private const string ID_TOKEN_KEY = "sso_id_token";
        private const string EXPIRES_AT_KEY = "sso_expires_at";
        private const string DPOP_JWK_KEY = "sso_dpop_jwk";

        protected Dictionary<string, string?> _tokenCache = new();
        protected readonly object _lockObject = new object();

        public virtual string? GetAccessToken()
        {
            lock (_lockObject)
            {
                _tokenCache.TryGetValue(ACCESS_TOKEN_KEY, out var token);
                return token;
            }
        }

        public virtual string? GetRefreshToken()
        {
            lock (_lockObject)
            {
                _tokenCache.TryGetValue(REFRESH_TOKEN_KEY, out var token);
                return token;
            }
        }

        public virtual string? GetIdToken()
        {
            lock (_lockObject)
            {
                _tokenCache.TryGetValue(ID_TOKEN_KEY, out var token);
                return token;
            }
        }

        public virtual string? GetDPoPJwk()
        {
            lock (_lockObject)
            {
                _tokenCache.TryGetValue(DPOP_JWK_KEY, out var jwk);
                return jwk;
            }
        }

        public virtual void SaveDPoPJwk(string jwkJson)
        {
            lock (_lockObject)
            {
                _tokenCache[DPOP_JWK_KEY] = jwkJson;
            }
        }

        public virtual bool IsAuthenticated()
        {
            var accessToken = GetAccessToken();
            if (string.IsNullOrEmpty(accessToken))
                return false;

            lock (_lockObject)
            {
                if (_tokenCache.TryGetValue(EXPIRES_AT_KEY, out var expiresAtStr))
                {
                    if (DateTimeOffset.TryParse(expiresAtStr, out var expiresAt))
                        return DateTimeOffset.UtcNow < expiresAt.AddMinutes(-5);
                }
            }

            return true;
        }

        public virtual async Task SaveTokensAsync(TokenResponse tokens)
        {
            lock (_lockObject)
            {
                _tokenCache[ACCESS_TOKEN_KEY] = tokens.AccessToken;
                _tokenCache[REFRESH_TOKEN_KEY] = tokens.RefreshToken;
                _tokenCache[ID_TOKEN_KEY] = tokens.IdToken;
                _tokenCache[EXPIRES_AT_KEY] = tokens.ExpiresAt.ToString("O");
            }

            await Task.CompletedTask;
        }

        public virtual void ClearTokens()
        {
            lock (_lockObject)
            {
                _tokenCache.Clear();
            }
        }
    }
}