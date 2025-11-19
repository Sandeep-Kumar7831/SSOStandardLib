using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiSsoLibrary.Services;
using System.Diagnostics;
using System.IO;

#if ANDROID
using Android.App;
#endif

namespace ExternStandardApp.Pages;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    string statusMessage = "Ready to read tokens";

    [ObservableProperty]
    string accessToken = "Not available";

    [ObservableProperty]
    string refreshToken = "Not available";

    [ObservableProperty]
    string idToken = "Not available";

    [ObservableProperty]
    bool isLoading;

    [ObservableProperty]
    bool tokenFound;

    [ObservableProperty]
    string debugInfo = "";

    public MainViewModel()
    {
    }

    [RelayCommand]
    public async Task ReadTokens()
    {
        IsLoading = true;
        StatusMessage = "Reading shared tokens...";
        DebugInfo = "";
        TokenFound = false;

        try
        {
            Debug.WriteLine("\n\n===== EXTERNAPP: READING TOKENS =====");

            // Get the actual path being used
            var actualPath = GetActualTokenPath();

            DebugInfo = $"Looking for tokens at:\n{actualPath}\n\n";
            Debug.WriteLine($"[ExternApp] Actual token path: {actualPath}");

            // Create token store
            var tokenStore = new CrossAppTokenStore();

            // Check if file exists
            var fileExists = File.Exists(actualPath);
            DebugInfo += $"File exists: {fileExists}\n";
            Debug.WriteLine($"[ExternApp] File exists: {fileExists}");

            if (!fileExists)
            {
                DebugInfo += "\n❌ TOKEN FILE NOT FOUND\n\n";
                DebugInfo += "Did you:\n";
                DebugInfo += "1. ✓ Open SsoMauiApp?\n";
                DebugInfo += "2. ✓ Click Login?\n";
                DebugInfo += "3. ✓ Complete OAuth?\n";
                DebugInfo += "4. ✓ Reach Dashboard?\n\n";
                DebugInfo += "Check SsoMauiApp Debug Output:\n";
                DebugInfo += "Should show:\n";
                DebugInfo += "[TokenShare] ✓ FILE SAVED\n";

                StatusMessage = "✗ Token file not found";
                return;
            }

            DebugInfo += "✓ File found!\n\n";

            // Show file contents for debugging
            try
            {
                var content = File.ReadAllText(actualPath);
                DebugInfo += $"File size: {content.Length} bytes\n";
                Debug.WriteLine($"[ExternApp] File content length: {content.Length}");
            }
            catch { }

            // Check if authenticated
            var isAuth = tokenStore.IsAuthenticated();
            Debug.WriteLine($"[ExternApp] IsAuthenticated: {isAuth}");
            DebugInfo += $"IsAuthenticated: {isAuth}\n\n";

            if (!isAuth)
            {
                DebugInfo += "❌ Not authenticated\n";
                DebugInfo += "Tokens may be expired\n";
                StatusMessage = "✗ Not authenticated";
                return;
            }

            // Get tokens
            DebugInfo += "🔑 Reading tokens...\n";

            var accessToken = tokenStore.GetAccessToken();
            var refreshTokenValue = tokenStore.GetRefreshToken();
            var idTokenValue = tokenStore.GetIdToken();

            Debug.WriteLine($"[ExternApp] AccessToken found: {(accessToken != null ? "YES" : "NO")}");
            Debug.WriteLine($"[ExternApp] RefreshToken found: {(refreshTokenValue != null ? "YES" : "NO")}");

            if (string.IsNullOrEmpty(accessToken))
            {
                DebugInfo += "❌ Access token is empty\n";
                StatusMessage = "✗ No access token";
                return;
            }

            // Display tokens
            AccessToken = accessToken.Length > 70 ? accessToken.Substring(0, 70) + "..." : accessToken;
            RefreshToken = string.IsNullOrEmpty(refreshTokenValue) ? "Not available" :
                (refreshTokenValue.Length > 70 ? refreshTokenValue.Substring(0, 70) + "..." : refreshTokenValue);
            IdToken = string.IsNullOrEmpty(idTokenValue) ? "Not available" :
                (idTokenValue.Length > 70 ? idTokenValue.Substring(0, 70) + "..." : idTokenValue);

            TokenFound = true;
            StatusMessage = "✅ SUCCESS! Tokens read from SsoMauiApp";
            DebugInfo += "\n✅ TOKENS SHARED SUCCESSFULLY!\n";
            DebugInfo += $"Access Token: {(string.IsNullOrEmpty(accessToken) ? "NO" : "YES")}\n";
            DebugInfo += $"Refresh Token: {(string.IsNullOrEmpty(refreshTokenValue) ? "NO" : "YES")}\n";
            DebugInfo += $"ID Token: {(string.IsNullOrEmpty(idTokenValue) ? "NO" : "YES")}\n";

            Debug.WriteLine("[ExternApp] ✓ SUCCESS - Tokens read!");
            Debug.WriteLine("===== EXTERNAPP: DONE =====\n");
        }
        catch (Exception ex)
        {
            DebugInfo = $"❌ ERROR\n\n{ex.GetType().Name}\n{ex.Message}";
            StatusMessage = $"✗ Error";
            Debug.WriteLine($"[ExternApp] ✗ Error: {ex.Message}");
            Debug.WriteLine($"[ExternApp] Stack: {ex.StackTrace}");
            TokenFound = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task CopyAccessToken()
    {
        try
        {
            if (!string.IsNullOrEmpty(AccessToken) && AccessToken != "Not available")
            {
                var tokenStore = new CrossAppTokenStore();
                var token = tokenStore.GetAccessToken();

                if (!string.IsNullOrEmpty(token))
                {
                    await Clipboard.SetTextAsync(token);
                    StatusMessage = "✓ Token copied";
                    DebugInfo = "Token copied to clipboard!";
                }
            }
        }
        catch (Exception ex)
        {
            DebugInfo = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ClearDebugInfo()
    {
        DebugInfo = "";
    }

    private string GetActualTokenPath()
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            var externalCacheDir = context.GetExternalCacheDirs();

            string basePath;
            if (externalCacheDir != null && externalCacheDir[0].Exists())
            {
                basePath = externalCacheDir[0].AbsolutePath;
            }
            else
            {
                basePath = context.CacheDir.AbsolutePath;
            }

            return Path.Combine(basePath, "honeywell_sso_tokens", "sso-tokens.json");
        }
        catch
        {
            return "/sdcard/Android/data/[package]/cache/honeywell_sso_tokens/sso-tokens.json";
        }
#else
        var appDataPath = FileSystem.AppDataDirectory;
        return Path.Combine(appDataPath, "honeywell_sso_tokens", "sso-tokens.json");
#endif
    }
}