using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiSso.Plugin.Services;
using MauiSsoLibrary.Services;
using System.Diagnostics;

namespace ExternStandardApp.Pages;

public partial class MainViewModel : ObservableObject
{
    private readonly IMauiSsoPlugin _ssoPlugin;

    [ObservableProperty]
    string statusMessage = "Ready to read tokens from SsoMauiApp";

    [ObservableProperty]
    string accessToken = "Not available";

    [ObservableProperty]
    string refreshToken = "Not available";

    [ObservableProperty]
    string idToken = "Not available";

    [ObservableProperty]
    string expiresAt = "Not available";

    [ObservableProperty]
    bool isLoading;

    [ObservableProperty]
    bool tokenFound;

    [ObservableProperty]
    bool isTokenValid;

    public MainViewModel(IMauiSsoPlugin ssoPlugin)
    {
        _ssoPlugin = ssoPlugin;
    }

    [RelayCommand]
    public async Task ReadTokens()
    {
        IsLoading = true;
        StatusMessage = "Reading tokens via MauiSso.Plugin...";
        TokenFound = false;

        try
        {
            // Check if authenticated (reads from SharedSecureTokenStore)
            var isAuthenticated = _ssoPlugin.IsAuthenticated();

            if (!isAuthenticated)
            {
                StatusMessage = "✗ No tokens found. Have you logged in with SsoMauiApp?";
                Debug.WriteLine("[MainViewModel] Not authenticated - no tokens in storage");
                TokenFound = false;
                return;
            }

            // Get access token via plugin
            var token = _ssoPlugin.GetAccessToken();

            if (string.IsNullOrEmpty(token))
            {
                StatusMessage = "✗ Access token is empty";
                TokenFound = false;
                return;
            }

            // Display tokens
            AccessToken = token.Length > 50
                ? token.Substring(0, 50) + "..."
                : token;

            // Try to get other tokens if available via token store
            var tokenStore = new SharedSecureTokenStore("honeywell_launcher");

            var refreshTokenValue = tokenStore.GetRefreshToken();
            RefreshToken = string.IsNullOrEmpty(refreshTokenValue)
                ? "Not available"
                : refreshTokenValue.Substring(0, Math.Min(50, refreshTokenValue.Length)) + "...";

            var idTokenValue = tokenStore.GetIdToken();
            IdToken = string.IsNullOrEmpty(idTokenValue)
                ? "Not available"
                : idTokenValue.Substring(0, Math.Min(50, idTokenValue.Length)) + "...";

            IsTokenValid = _ssoPlugin.IsAuthenticated();
            TokenFound = true;

            StatusMessage = IsTokenValid
                ? "✓ Tokens read successfully via MauiSso.Plugin!"
                : "⚠ Tokens found but may be expired!";

            Debug.WriteLine("[MainViewModel] ✓ Tokens read successfully");
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ Error: {ex.Message}";
            Debug.WriteLine($"[MainViewModel] Error reading tokens: {ex.Message}\n{ex.StackTrace}");
            TokenFound = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task RefreshTokenDisplay()
    {
        await ReadTokens();
    }

    [RelayCommand]
    public async Task CopyAccessToken()
    {
        try
        {
            var token = _ssoPlugin.GetAccessToken();
            if (!string.IsNullOrEmpty(token))
            {
                await Clipboard.SetTextAsync(token);
                StatusMessage = "✓ Access token copied to clipboard";
                Debug.WriteLine("[MainViewModel] Token copied to clipboard");
            }
            else
            {
                StatusMessage = "No token to copy";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error copying: {ex.Message}";
            Debug.WriteLine($"[MainViewModel] Error copying token: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task InitializePlugin()
    {
        IsLoading = true;
        StatusMessage = "Initializing MauiSso.Plugin...";

        try
        {
            var initialized = await _ssoPlugin.InitializeAsync();

            if (initialized)
            {
                StatusMessage = "✓ Plugin initialized successfully";
                await ReadTokens();
            }
            else
            {
                StatusMessage = "✗ Plugin initialization failed";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error initializing: {ex.Message}";
            Debug.WriteLine($"[MainViewModel] Error initializing plugin: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
