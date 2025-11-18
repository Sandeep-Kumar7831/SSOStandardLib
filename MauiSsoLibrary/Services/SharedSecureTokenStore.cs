// MauiSsoLibrary/Services/SharedSecureTokenStore.cs
// Uses Xamarin.Essentials.SecureStorage (.NET Standard compatible)

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Xamarin.Essentials;

namespace MauiSsoLibrary.Services
{
    /// <summary>
    /// Shared secure token store using Xamarin.Essentials SecureStorage
    /// Works in .NET Standard libraries
    /// 
    /// Automatically uses platform-specific secure storage:
    /// - Android: Keystore
    /// - iOS: Keychain
    /// - Windows: DPAPI
    /// - macOS: Keychain
    /// </summary>
    public class SharedSecureTokenStore : ITokenStore
    {
        private const string ACCESS_TOKEN_KEY = "sso_access_token";
        private const string REFRESH_TOKEN_KEY = "sso_refresh_token";
        private const string ID_TOKEN_KEY = "sso_id_token";
        private const string EXPIRES_AT_KEY = "sso_expires_at";
        private const string DPOP_JWK_KEY = "sso_dpop_jwk";

        private readonly string _storagePrefix;
        private readonly object _lockObject = new object();

        /// <summary>
        /// Initialize shared secure token store
        /// </summary>
        /// <param name="storageGroupId">
        /// Prefix for storage keys (e.g., "honeywell_launcher")
        /// All keys stored as: {prefix}_{key}
        /// </param>
        public SharedSecureTokenStore(string storageGroupId = "honeywell_launcher")
        {
            _storagePrefix = storageGroupId;

            System.Diagnostics.Debug.WriteLine($"[SharedSecureTokenStore] Initialized with prefix: {storageGroupId}");
            System.Diagnostics.Debug.WriteLine($"[SharedSecureTokenStore] Using Xamarin.Essentials.SecureStorage");
        }

        public string? GetAccessToken() => GetTokenAsync(ACCESS_TOKEN_KEY).Result;
        public string? GetRefreshToken() => GetTokenAsync(REFRESH_TOKEN_KEY).Result;
        public string? GetIdToken() => GetTokenAsync(ID_TOKEN_KEY).Result;
        public string? GetDPoPJwk() => GetTokenAsync(DPOP_JWK_KEY).Result;

        public void SaveDPoPJwk(string jwkJson) => SaveTokenAsync(DPOP_JWK_KEY, jwkJson).Wait();

        public bool IsAuthenticated()
        {
            var accessToken = GetAccessToken();
            if (string.IsNullOrEmpty(accessToken))
                return false;

            var expiresAtStr = GetTokenAsync(EXPIRES_AT_KEY).Result;
            if (string.IsNullOrEmpty(expiresAtStr))
                return true;

            if (DateTimeOffset.TryParse(expiresAtStr, out var expiresAt))
                return DateTimeOffset.UtcNow < expiresAt.AddMinutes(-5);

            return true;
        }

        public async Task SaveTokensAsync(TokenResponse tokens)
        {
            lock (_lockObject)
            {
                try
                {
                    SaveTokenAsync(ACCESS_TOKEN_KEY, tokens.AccessToken).Wait();
                    SaveTokenAsync(REFRESH_TOKEN_KEY, tokens.RefreshToken).Wait();
                    SaveTokenAsync(ID_TOKEN_KEY, tokens.IdToken).Wait();
                    SaveTokenAsync(EXPIRES_AT_KEY, tokens.ExpiresAt.ToString("O")).Wait();

                    System.Diagnostics.Debug.WriteLine("[SharedSecureTokenStore] ✓ Tokens saved securely");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SharedSecureTokenStore] ✗ Error saving tokens: {ex.Message}");
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
                    RemoveTokenAsync(ACCESS_TOKEN_KEY).Wait();
                    RemoveTokenAsync(REFRESH_TOKEN_KEY).Wait();
                    RemoveTokenAsync(ID_TOKEN_KEY).Wait();
                    RemoveTokenAsync(EXPIRES_AT_KEY).Wait();
                    RemoveTokenAsync(DPOP_JWK_KEY).Wait();

                    System.Diagnostics.Debug.WriteLine("[SharedSecureTokenStore] ✓ Tokens cleared");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SharedSecureTokenStore] ✗ Error clearing tokens: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Get token from secure storage
        /// </summary>
        private async Task<string?> GetTokenAsync(string key)
        {
            lock (_lockObject)
            {
                try
                {
                    var fullKey = $"{_storagePrefix}_{key}";
                    var value = SecureStorage.GetAsync(fullKey).Result;

                    if (!string.IsNullOrEmpty(value))
                    {
                        System.Diagnostics.Debug.WriteLine($"[SharedSecureTokenStore] Retrieved {key} from secure storage");
                        return value;
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SharedSecureTokenStore] Error reading {key}: {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Save token to secure storage
        /// </summary>
        private async Task SaveTokenAsync(string key, string value)
        {
            lock (_lockObject)
            {
                try
                {
                    var fullKey = $"{_storagePrefix}_{key}";
                    SecureStorage.SetAsync(fullKey, value).Wait();

                    System.Diagnostics.Debug.WriteLine($"[SharedSecureTokenStore] Saved {key} to secure storage");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SharedSecureTokenStore] Error saving {key}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Remove token from secure storage
        /// </summary>
        private async Task RemoveTokenAsync(string key)
        {
            lock (_lockObject)
            {
                try
                {
                    var fullKey = $"{_storagePrefix}_{key}";
                    SecureStorage.Remove(fullKey);

                    System.Diagnostics.Debug.WriteLine($"[SharedSecureTokenStore] Removed {key}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SharedSecureTokenStore] Error removing {key}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Export all tokens for debugging or API calls
        /// </summary>
        public TokenExport ExportTokens()
        {
            lock (_lockObject)
            {
                return new TokenExport
                {
                    AccessToken = GetAccessToken(),
                    RefreshToken = GetRefreshToken(),
                    IdToken = GetIdToken(),
                    ExpiresAt = GetTokenAsync(EXPIRES_AT_KEY).Result ?? DateTime.UtcNow.ToString("O")
                };
            }
        }

        public class TokenExport
        {
            public string? AccessToken { get; set; }
            public string? RefreshToken { get; set; }
            public string? IdToken { get; set; }
            public string ExpiresAt { get; set; } = string.Empty;

            public string ToJson() => JsonSerializer.Serialize(this);
        }
    }
}