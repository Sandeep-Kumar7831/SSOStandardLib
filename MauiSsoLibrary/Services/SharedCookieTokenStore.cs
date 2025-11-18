using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MauiSsoLibrary.Services
{
    /// <summary>
    /// Token store that persists tokens in browser cookies
    /// Allows other apps/webapps to read tokens from shared cookies
    /// </summary>
    public class SharedCookieTokenStore : ITokenStore
    {
        private readonly string _cookieDomain;      // Changed to field (not const)
        private const string COOKIE_PATH = "/";
        private const string ACCESS_TOKEN_COOKIE = "sso_access_token";
        private const string REFRESH_TOKEN_COOKIE = "sso_refresh_token";
        private const string ID_TOKEN_COOKIE = "sso_id_token";
        private const string EXPIRES_AT_COOKIE = "sso_expires_at";
        private const string DPOP_JWK_COOKIE = "sso_dpop_jwk";

        private readonly CookieContainer _cookieContainer;
        private readonly ITokenStore _fallbackStore;  // Fallback for in-memory storage
        private readonly object _lockObject = new object();

        /// <summary>
        /// Initialize shared cookie token store
        /// </summary>
        /// <param name="cookieDomain">Domain for cookies (e.g., ".honeywell.com")</param>
        /// <param name="fallbackStore">Fallback store if cookies unavailable</param>
        public SharedCookieTokenStore(string cookieDomain = ".honeywell.com", ITokenStore? fallbackStore = null)
        {
            _cookieDomain = cookieDomain;  // ← Now assigns to field (not const)
            _cookieContainer = new CookieContainer();
            _fallbackStore = fallbackStore ?? new TokenStore();

            System.Diagnostics.Debug.WriteLine($"[SharedCookieTokenStore] Initialized with domain: {cookieDomain}");
        }

        public string? GetAccessToken()
        {
            lock (_lockObject)
            {
                var cookieValue = GetCookie(ACCESS_TOKEN_COOKIE);
                if (!string.IsNullOrEmpty(cookieValue))
                    return cookieValue;

                // Fallback to in-memory store
                return _fallbackStore.GetAccessToken();
            }
        }

        public string? GetRefreshToken()
        {
            lock (_lockObject)
            {
                var cookieValue = GetCookie(REFRESH_TOKEN_COOKIE);
                if (!string.IsNullOrEmpty(cookieValue))
                    return cookieValue;

                return _fallbackStore.GetRefreshToken();
            }
        }

        public string? GetIdToken()
        {
            lock (_lockObject)
            {
                var cookieValue = GetCookie(ID_TOKEN_COOKIE);
                if (!string.IsNullOrEmpty(cookieValue))
                    return cookieValue;

                return _fallbackStore.GetIdToken();
            }
        }

        public string? GetDPoPJwk()
        {
            lock (_lockObject)
            {
                var cookieValue = GetCookie(DPOP_JWK_COOKIE);
                if (!string.IsNullOrEmpty(cookieValue))
                    return cookieValue;

                return _fallbackStore.GetDPoPJwk();
            }
        }

        public void SaveDPoPJwk(string jwkJson)
        {
            lock (_lockObject)
            {
                SaveCookie(DPOP_JWK_COOKIE, jwkJson);
                _fallbackStore.SaveDPoPJwk(jwkJson);
            }
        }

        public bool IsAuthenticated()
        {
            var accessToken = GetAccessToken();
            if (string.IsNullOrEmpty(accessToken))
                return false;

            lock (_lockObject)
            {
                var expiresAtStr = GetCookie(EXPIRES_AT_COOKIE) ?? _fallbackStore.GetAccessToken();
                if (string.IsNullOrEmpty(expiresAtStr))
                    return true;

                if (DateTimeOffset.TryParse(expiresAtStr, out var expiresAt))
                    return DateTimeOffset.UtcNow < expiresAt.AddMinutes(-5);
            }

            return true;
        }

        public async Task SaveTokensAsync(TokenResponse tokens)
        {
            lock (_lockObject)
            {
                try
                {
                    // Save to cookies for cross-app sharing
                    SaveCookie(ACCESS_TOKEN_COOKIE, tokens.AccessToken);
                    SaveCookie(REFRESH_TOKEN_COOKIE, tokens.RefreshToken);
                    SaveCookie(ID_TOKEN_COOKIE, tokens.IdToken);
                    SaveCookie(EXPIRES_AT_COOKIE, tokens.ExpiresAt.ToString("O"));

                    System.Diagnostics.Debug.WriteLine("[SharedCookieTokenStore] ✓ Tokens saved to cookies");

                    // Also save to fallback store
                    _fallbackStore.SaveTokensAsync(tokens).Wait();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SharedCookieTokenStore] ✗ Error saving tokens: {ex.Message}");
                }
            }

            await Task.CompletedTask;
        }

        public void ClearTokens()
        {
            lock (_lockObject)
            {
                try
                {
                    // Clear all cookies
                    DeleteCookie(ACCESS_TOKEN_COOKIE);
                    DeleteCookie(REFRESH_TOKEN_COOKIE);
                    DeleteCookie(ID_TOKEN_COOKIE);
                    DeleteCookie(EXPIRES_AT_COOKIE);
                    DeleteCookie(DPOP_JWK_COOKIE);

                    System.Diagnostics.Debug.WriteLine("[SharedCookieTokenStore] ✓ Tokens cleared from cookies");

                    // Also clear fallback store
                    _fallbackStore.ClearTokens();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SharedCookieTokenStore] ✗ Error clearing tokens: {ex.Message}");
                }
            }
        }

        private string? GetCookie(string name)
        {
            try
            {
                var uri = new Uri($"https://{_cookieDomain}/");
                var cookies = _cookieContainer.GetCookies(uri);
                var cookie = cookies[name];
                return cookie?.Value;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SharedCookieTokenStore] Error reading cookie {name}: {ex.Message}");
                return null;
            }
        }

        private void SaveCookie(string name, string value)
        {
            try
            {
                var uri = new Uri($"https://{_cookieDomain}/");
                var cookie = new System.Net.Cookie
                {
                    Name = name,
                    Value = value,
                    Domain = _cookieDomain,
                    Path = COOKIE_PATH,
                    Secure = true,
                    HttpOnly = true,
                    Expires = DateTime.UtcNow.AddYears(1)  // Long expiry for cross-app access
                };

                _cookieContainer.Add(uri, cookie);
                System.Diagnostics.Debug.WriteLine($"[SharedCookieTokenStore] Saved cookie: {name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SharedCookieTokenStore] Error saving cookie {name}: {ex.Message}");
            }
        }

        private void DeleteCookie(string name)
        {
            try
            {
                var uri = new Uri($"https://{_cookieDomain}/");
                var cookie = new System.Net.Cookie
                {
                    Name = name,
                    Value = string.Empty,
                    Domain = _cookieDomain,
                    Path = COOKIE_PATH,
                    Expires = DateTime.UtcNow.AddDays(-1)  // Set past date to delete
                };

                _cookieContainer.Add(uri, cookie);
                System.Diagnostics.Debug.WriteLine($"[SharedCookieTokenStore] Deleted cookie: {name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SharedCookieTokenStore] Error deleting cookie {name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Export cookies as Set-Cookie headers for other apps
        /// </summary>
        public Dictionary<string, string> ExportCookiesAsHeaders()
        {
            var headers = new Dictionary<string, string>();

            lock (_lockObject)
            {
                var uri = new Uri($"https://{_cookieDomain}/");
                var cookies = _cookieContainer.GetCookies(uri);

                foreach (System.Net.Cookie cookie in cookies)
                {
                    var setCookieHeader = $"{cookie.Name}={cookie.Value}; Domain={_cookieDomain}; Path={COOKIE_PATH}; Secure; HttpOnly";
                    headers[cookie.Name] = setCookieHeader;
                }
            }

            return headers;
        }
    }
}