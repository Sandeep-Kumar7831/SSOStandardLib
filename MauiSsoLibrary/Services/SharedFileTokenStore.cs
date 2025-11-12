using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace MauiSsoLibrary.Services
{
    /// <summary>
    /// Shared file-based token store for multiple applications on same device
    /// </summary>
    public class SharedFileTokenStore : ITokenStore
    {
        private const string ACCESS_TOKEN_KEY = "sso_access_token";
        private const string REFRESH_TOKEN_KEY = "sso_refresh_token";
        private const string ID_TOKEN_KEY = "sso_id_token";
        private const string EXPIRES_AT_KEY = "sso_expires_at";
        private const string DPOP_JWK_KEY = "sso_dpop_jwk";

        private readonly string _tokenFile;
        private readonly object _lockObject = new object();

        /// <summary>
        /// Initialize shared token store
        /// </summary>
        /// <param name="sharedStoragePath">Common path for all apps (e.g., "C:\Shared\Tokens" or "/var/shared/tokens")</param>
        public SharedFileTokenStore(string sharedStoragePath)
        {
            if (string.IsNullOrWhiteSpace(sharedStoragePath))
                throw new ArgumentNullException(nameof(sharedStoragePath));

            // Create shared directory if it doesn't exist
            Directory.CreateDirectory(sharedStoragePath);

            // Store tokens in a common file accessible by all apps
            _tokenFile = Path.Combine(sharedStoragePath, "sso-tokens.json");

            System.Diagnostics.Debug.WriteLine($"SharedFileTokenStore initialized at: {_tokenFile}");
        }

        public string? GetAccessToken()
        {
            return GetTokenFromFile(ACCESS_TOKEN_KEY);
        }

        public string? GetRefreshToken()
        {
            return GetTokenFromFile(REFRESH_TOKEN_KEY);
        }

        public string? GetIdToken()
        {
            return GetTokenFromFile(ID_TOKEN_KEY);
        }

        public string? GetDPoPJwk()
        {
            return GetTokenFromFile(DPOP_JWK_KEY);
        }

        public void SaveDPoPJwk(string jwkJson)
        {
            SaveTokenToFile(DPOP_JWK_KEY, jwkJson);
        }

        public bool IsAuthenticated()
        {
            var accessToken = GetAccessToken();
            if (string.IsNullOrEmpty(accessToken))
                return false;

            var expiresAtStr = GetTokenFromFile(EXPIRES_AT_KEY);
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
                var tokenData = new Dictionary<string, string?>
                {
                    { ACCESS_TOKEN_KEY, tokens.AccessToken },
                    { REFRESH_TOKEN_KEY, tokens.RefreshToken },
                    { ID_TOKEN_KEY, tokens.IdToken },
                    { EXPIRES_AT_KEY, tokens.ExpiresAt.ToString("O") }
                };

                try
                {
                    var json = JsonSerializer.Serialize(tokenData);
                    File.WriteAllText(_tokenFile, json);
                    System.Diagnostics.Debug.WriteLine("SharedFileTokenStore: Tokens saved successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SharedFileTokenStore: Save error - {ex.Message}");
                    throw;
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
                    if (File.Exists(_tokenFile))
                        File.Delete(_tokenFile);

                    System.Diagnostics.Debug.WriteLine("SharedFileTokenStore: Tokens cleared");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SharedFileTokenStore: Clear error - {ex.Message}");
                }
            }
        }

        private string? GetTokenFromFile(string key)
        {
            lock (_lockObject)
            {
                try
                {
                    if (!File.Exists(_tokenFile))
                        return null;

                    var json = File.ReadAllText(_tokenFile);
                    var tokenData = JsonSerializer.Deserialize<Dictionary<string, string?>>(json);

                    if (tokenData != null && tokenData.TryGetValue(key, out var value))
                        return value;

                    return null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SharedFileTokenStore: Read error - {ex.Message}");
                    return null;
                }
            }
        }

        private void SaveTokenToFile(string key, string value)
        {
            lock (_lockObject)
            {
                try
                {
                    var tokenData = new Dictionary<string, string?>();

                    // Read existing data
                    if (File.Exists(_tokenFile))
                    {
                        var json = File.ReadAllText(_tokenFile);
                        var existing = JsonSerializer.Deserialize<Dictionary<string, string?>>(json);
                        if (existing != null)
                            tokenData = existing;
                    }

                    // Update the key
                    tokenData[key] = value;

                    // Write back
                    var updatedJson = JsonSerializer.Serialize(tokenData);
                    File.WriteAllText(_tokenFile, updatedJson);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SharedFileTokenStore: Save single token error - {ex.Message}");
                    throw;
                }
            }
        }
    }
}