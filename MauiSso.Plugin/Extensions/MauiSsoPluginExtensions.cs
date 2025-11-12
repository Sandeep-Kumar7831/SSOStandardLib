using MauiSsoLibrary.Extensions;
using MauiSsoLibrary.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using MauiSso.Plugin.Services;
using System.Diagnostics;
using SsoConfiguration = MauiSso.Plugin.Configuration.SsoConfiguration;

namespace MauiSso.Plugin.Extensions
{
    public static class MauiSsoPluginExtensions
    {
        /// <summary>
        /// Register SSO plugin for MAUI app
        /// </summary>
        public static MauiAppBuilder AddMauiSsoPlugin(
            this MauiAppBuilder builder,
            Action<SsoConfiguration> configureOptions,
            ITokenStore? customTokenStore = null)
        {
            var services = builder.Services;

            // Create plugin configuration
            var pluginConfig = new SsoConfiguration();
            configureOptions(pluginConfig);

            // Create MAUI-specific platform detector
            var platformDetector = new MauiPlatformDetector();

            // Use custom token store or default to secure storage for MAUI
            var tokenStore = customTokenStore ?? new MauiSecureFileTokenStore();

            // Register SSO services from core library with MAUI implementations
            // Convert plugin config to library config
            services.AddMauiSso(
                libConfig =>
                {
                    libConfig.Authority = pluginConfig.Authority;
                    libConfig.ClientId = pluginConfig.ClientId;
                    libConfig.ClientSecret = pluginConfig.ClientSecret;
                    libConfig.Scope = pluginConfig.Scope;
                    libConfig.RedirectUri = pluginConfig.RedirectUri;
                    libConfig.PostLogoutRedirectUri = pluginConfig.PostLogoutRedirectUri;
                    libConfig.EnableDPoP = pluginConfig.EnableDPoP;
                },
                tokenStore,
                browser: null, // Let factory create platform-specific browser
                platformDetector);

            // Register plugin
            services.AddSingleton<IMauiSsoPlugin, MauiSsoPlugin>();

            return builder;
        }

        /// <summary>
        /// MAUI-specific platform detector implementation
        /// </summary>
        public class MauiPlatformDetector : IPlatformDetector
        {
            public PlatformType GetCurrentPlatform()
            {


                var platform = DeviceInfo.Platform.ToString();

                return platform switch
                {
                    nameof(DevicePlatform.Android) => PlatformType.Android,
                    nameof(DevicePlatform.iOS) => PlatformType.iOS,
                    nameof(DevicePlatform.WinUI) => PlatformType.Windows,
                    nameof(DevicePlatform.MacCatalyst) => PlatformType.macOS,
                    _ => PlatformType.Unknown
                };
            }

            public void OpenBrowser(string url)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        await Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Browser open error: {ex.Message}");
                    }
                });
            }
        }

        /// <summary>
        /// Secure file-based token store for MAUI - uses MAUI FileSystem
        /// </summary>
        public class MauiSecureFileTokenStore : ITokenStore
        {
            private readonly string _appDataPath;
            private readonly object _lockObject = new object();
            private const string TOKEN_FILE = "sso-tokens.json";

            public MauiSecureFileTokenStore()
            {
                _appDataPath = FileSystem.AppDataDirectory;
            }

            public string? GetAccessToken() => GetToken("sso_access_token");
            public string? GetRefreshToken() => GetToken("sso_refresh_token");
            public string? GetIdToken() => GetToken("sso_id_token");
            public string? GetDPoPJwk() => GetToken("sso_dpop_jwk");

            public void SaveDPoPJwk(string jwkJson) => SaveToken("sso_dpop_jwk", jwkJson);

            public bool IsAuthenticated()
            {
                var accessToken = GetAccessToken();
                if (string.IsNullOrEmpty(accessToken))
                    return false;

                var expiresAtStr = GetToken("sso_expires_at");
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
                    SaveToken("sso_access_token", tokens.AccessToken);
                    SaveToken("sso_refresh_token", tokens.RefreshToken);
                    SaveToken("sso_id_token", tokens.IdToken);
                    SaveToken("sso_expires_at", tokens.ExpiresAt.ToString("O"));
                }

                await Task.CompletedTask;
            }

            public void ClearTokens()
            {
                lock (_lockObject)
                {
                    try
                    {
                        var tokenFile = Path.Combine(_appDataPath, TOKEN_FILE);
                        if (File.Exists(tokenFile))
                            File.Delete(tokenFile);

                        Debug.WriteLine("MauiSecureFileTokenStore: Tokens cleared");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error clearing tokens: {ex.Message}");
                    }
                }
            }

            private string? GetToken(string key)
            {
                lock (_lockObject)
                {
                    try
                    {
                        var tokenFile = Path.Combine(_appDataPath, TOKEN_FILE);
                        if (!File.Exists(tokenFile))
                            return null;

                        var json = File.ReadAllText(tokenFile);
                        var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(json);

                        if (data?.TryGetValue(key, out var value) == true)
                            return value;

                        return null;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error reading token: {ex.Message}");
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
                        var tokenFile = Path.Combine(_appDataPath, TOKEN_FILE);
                        var data = new Dictionary<string, string?>();

                        if (File.Exists(tokenFile))
                        {
                            var json = File.ReadAllText(tokenFile);
                            var existing = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(json);
                            if (existing != null)
                                data = existing;
                        }

                        data[key] = value;
                        var updatedJson = System.Text.Json.JsonSerializer.Serialize(data);
                        File.WriteAllText(tokenFile, updatedJson);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error saving token: {ex.Message}");
                    }
                }
            }
        }
    }
}