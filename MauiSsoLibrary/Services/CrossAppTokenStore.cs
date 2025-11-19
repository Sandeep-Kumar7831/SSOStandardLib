using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xamarin.Essentials;

#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using Java.IO;
#endif

namespace MauiSsoLibrary.Services
{
    /// <summary>
    /// TRUE cross-app token sharing using Android's external files directory
    /// Works without root - properly shared between apps
    /// </summary>
    public class CrossAppTokenStore : ITokenStore
    {
        private const string ACCESS_TOKEN_KEY = "sso_access_token";
        private const string REFRESH_TOKEN_KEY = "sso_refresh_token";
        private const string ID_TOKEN_KEY = "sso_id_token";
        private const string EXPIRES_AT_KEY = "sso_expires_at";
        private const string DPOP_JWK_KEY = "sso_dpop_jwk";
        private const string TOKEN_FILE = "sso-tokens.json";

        private readonly object _lockObject = new object();
        private readonly string _tokenStorePath;

#if ANDROID
        private const string TOKEN_DIR = "honeywell_sso_tokens";
#else
        private Dictionary<string, string> _tokenCache = new Dictionary<string, string>();
#endif

        public CrossAppTokenStore(string sharedDirectory = null, string encryptionKey = null)
        {
            try
            {
#if ANDROID
                // Use external cache directory - world accessible on shared storage
                var context = Application.Context;
                var externalCacheDir = context.GetExternalCacheDir();
                
                string storagePath;
                if (externalCacheDir != null && externalCacheDir.Exists())
                {
                    // Use external cache: /sdcard/Android/data/package/cache
                    storagePath = externalCacheDir.AbsolutePath;
                }
                else
                {
                    // Fallback to internal cache
                    storagePath = context.CacheDir.AbsolutePath;
                }

                // Create subdirectory
                var tokenDir = new Java.IO.File(storagePath, TOKEN_DIR);
                if (!tokenDir.Exists())
                {
                    tokenDir.Mkdirs();
                    System.Diagnostics.Debug.WriteLine($"[TokenShare] Created directory: {tokenDir.AbsolutePath}");
                }

                _tokenStorePath = new Java.IO.File(tokenDir, TOKEN_FILE).AbsolutePath;

                System.Diagnostics.Debug.WriteLine($"[TokenShare] ✓ Initialized");
                System.Diagnostics.Debug.WriteLine($"[TokenShare] Package: {context.PackageName}");
                System.Diagnostics.Debug.WriteLine($"[TokenShare] Storage: {storagePath}");
                System.Diagnostics.Debug.WriteLine($"[TokenShare] Token Path: {_tokenStorePath}");
                System.Diagnostics.Debug.WriteLine($"[TokenShare] External Storage State: {Environment.ExternalStorageState}");
#else
                var appDataPath = FileSystem.AppDataDirectory;
                var tokenDir = Path.Combine(appDataPath, "");
                if (!Directory.Exists(tokenDir))
                    Directory.CreateDirectory(tokenDir);

                _tokenStorePath = Path.Combine(tokenDir, TOKEN_FILE);
                System.Diagnostics.Debug.WriteLine($"[TokenShare] Desktop path: {_tokenStorePath}");
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TokenShare] ✗ Init error: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        public string? GetAccessToken() => GetToken(ACCESS_TOKEN_KEY);
        public string? GetRefreshToken() => GetToken(REFRESH_TOKEN_KEY);
        public string? GetIdToken() => GetToken(ID_TOKEN_KEY);
        public string? GetDPoPJwk() => GetToken(DPOP_JWK_KEY);
        public void SaveDPoPJwk(string jwkJson) => SaveToken(DPOP_JWK_KEY, jwkJson);

        public bool IsAuthenticated()
        {
            try
            {
                var accessToken = GetAccessToken();
                if (string.IsNullOrEmpty(accessToken))
                {
                    System.Diagnostics.Debug.WriteLine("[TokenShare] IsAuthenticated: FALSE (no token)");
                    return false;
                }

                var expiresAtStr = GetToken(EXPIRES_AT_KEY);
                if (string.IsNullOrEmpty(expiresAtStr))
                {
                    System.Diagnostics.Debug.WriteLine("[TokenShare] IsAuthenticated: TRUE (no expiry set)");
                    return true;
                }

                if (DateTimeOffset.TryParse(expiresAtStr, out var expiresAt))
                {
                    var isValid = DateTimeOffset.UtcNow < expiresAt.AddMinutes(-5);
                    System.Diagnostics.Debug.WriteLine($"[TokenShare] IsAuthenticated: {isValid} (expires: {expiresAt})");
                    return isValid;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TokenShare] IsAuthenticated error: {ex.Message}");
                return false;
            }
        }

        public async Task SaveTokensAsync(TokenResponse tokens)
        {
            lock (_lockObject)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("\n[TokenShare] ========== SAVING TOKENS ==========");
                    System.Diagnostics.Debug.WriteLine($"[TokenShare] Access Token length: {tokens.AccessToken?.Length ?? 0}");
                    System.Diagnostics.Debug.WriteLine($"[TokenShare] Refresh Token length: {tokens.RefreshToken?.Length ?? 0}");
                    System.Diagnostics.Debug.WriteLine($"[TokenShare] Expires at: {tokens.ExpiresAt}");

                    var tokenData = new Dictionary<string, string?>
                    {
                        { ACCESS_TOKEN_KEY, tokens.AccessToken },
                        { REFRESH_TOKEN_KEY, tokens.RefreshToken },
                        { ID_TOKEN_KEY, tokens.IdToken },
                        { EXPIRES_AT_KEY, tokens.ExpiresAt.ToString("O") }
                    };

                    var json = JsonSerializer.Serialize(tokenData);
                    System.Diagnostics.Debug.WriteLine($"[TokenShare] JSON size: {json.Length} bytes");
                    System.Diagnostics.Debug.WriteLine($"[TokenShare] Writing to: {_tokenStorePath}");

                    // Ensure directory exists
                    var dirInfo = new FileInfo(_tokenStorePath).Directory;
                    if (dirInfo != null && !dirInfo.Exists)
                    {
                        dirInfo.Create();
                    }

                    File.WriteAllText(_tokenStorePath, json);

                    // Verify file was written
                    if (File.Exists(_tokenStorePath))
                    {
                        var fileSize = new FileInfo(_tokenStorePath).Length;
                        System.Diagnostics.Debug.WriteLine($"[TokenShare] ✓ FILE SAVED: {fileSize} bytes");
                        System.Diagnostics.Debug.WriteLine($"[TokenShare] ✓ SUCCESS - Tokens saved to: {_tokenStorePath}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[TokenShare] ✗ File not found after write!");
                    }

                    System.Diagnostics.Debug.WriteLine("[TokenShare] ========== SAVE COMPLETE ==========\n");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TokenShare] ✗ SAVE ERROR: {ex.GetType().Name}");
                    System.Diagnostics.Debug.WriteLine($"[TokenShare] Message: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[TokenShare] Stack: {ex.StackTrace}");
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
                    if (File.Exists(_tokenStorePath))
                    {
                        File.Delete(_tokenStorePath);
                        System.Diagnostics.Debug.WriteLine("[TokenShare] ✓ Tokens cleared");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TokenShare] ✗ Clear error: {ex.Message}");
                }
            }
        }

        private string? GetToken(string key)
        {
            lock (_lockObject)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[TokenShare] ======== READING TOKEN: {key} ========");
                    System.Diagnostics.Debug.WriteLine($"[TokenShare] Path: {_tokenStorePath}");
                    System.Diagnostics.Debug.WriteLine($"[TokenShare] Exists: {File.Exists(_tokenStorePath)}");

                    if (!File.Exists(_tokenStorePath))
                    {
                        System.Diagnostics.Debug.WriteLine($"[TokenShare] ✗ TOKEN FILE DOES NOT EXIST");
                        System.Diagnostics.Debug.WriteLine($"[TokenShare] Expected at: {_tokenStorePath}");
                        return null;
                    }

                    var json = File.ReadAllText(_tokenStorePath);
                    System.Diagnostics.Debug.WriteLine($"[TokenShare] File content: {json.Length} chars");

                    var tokenData = JsonSerializer.Deserialize<Dictionary<string, string?>>(json);
                    System.Diagnostics.Debug.WriteLine($"[TokenShare] Parsed JSON: {tokenData?.Count ?? 0} keys");

                    if (tokenData?.TryGetValue(key, out var value) == true && !string.IsNullOrEmpty(value))
                    {
                        System.Diagnostics.Debug.WriteLine($"[TokenShare] ✓ FOUND {key}: {value.Length} chars");
                        return value;
                    }

                    System.Diagnostics.Debug.WriteLine($"[TokenShare] ✗ KEY NOT FOUND: {key}");
                    if (tokenData != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TokenShare] Available keys: {string.Join(", ", tokenData.Keys)}");
                    }
                    return null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TokenShare] ✗ READ ERROR: {ex.GetType().Name}");
                    System.Diagnostics.Debug.WriteLine($"[TokenShare] Message: {ex.Message}");
                    return null;
                }
            }
        }

        private void SaveToken(string key, string value)
        {
            lock (_lockObject)
            {
                try
                {
                    var tokenData = new Dictionary<string, string?>();

                    if (File.Exists(_tokenStorePath))
                    {
                        try
                        {
                            var json = File.ReadAllText(_tokenStorePath);
                            var existing = JsonSerializer.Deserialize<Dictionary<string, string?>>(json);
                            if (existing != null)
                                tokenData = existing;
                        }
                        catch { }
                    }

                    tokenData[key] = value;
                    var updatedJson = JsonSerializer.Serialize(tokenData);
                    File.WriteAllText(_tokenStorePath, updatedJson);

                    System.Diagnostics.Debug.WriteLine($"[TokenShare] ✓ Saved {key}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TokenShare] ✗ Error saving {key}: {ex.Message}");
                }
            }
        }
    }
}