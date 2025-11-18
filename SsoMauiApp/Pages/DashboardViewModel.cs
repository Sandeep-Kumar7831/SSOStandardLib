// SsoMauiApp/Pages/DashboardViewModel.cs - FIXED

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiSso.Plugin.Services;
using System.Diagnostics;

namespace SsoMauiApp.Pages;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IMauiSsoPlugin _ssoPlugin;

    [ObservableProperty]
    string accessToken = "Not available";

    [ObservableProperty]
    bool isLoading;

    [ObservableProperty]
    string statusMessage = "";

    public DashboardViewModel(IMauiSsoPlugin ssoPlugin)
    {
        _ssoPlugin = ssoPlugin;
    }

    [RelayCommand]
    public async Task LoadUserData()
    {
        IsLoading = true;
        StatusMessage = "Loading user data...";

        try
        {
            Debug.WriteLine("[DashboardViewModel] Loading user data...");

            var token = _ssoPlugin.GetAccessToken();
            if (!string.IsNullOrEmpty(token))
            {
                AccessToken = token.Substring(0, Math.Min(50, token.Length)) + "...";
                StatusMessage = "User data loaded successfully";
                Debug.WriteLine("[DashboardViewModel] ✓ Token loaded successfully");

                // Use this token to call your API
                // Example: var response = await HttpClient.GetAsync("api/user", token);
            }
            else
            {
                StatusMessage = "No access token available";
                Debug.WriteLine("[DashboardViewModel] ✗ No access token available");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading data: {ex.Message}";
            Debug.WriteLine($"[DashboardViewModel] Error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task RefreshToken()
    {
        IsLoading = true;
        StatusMessage = "Refreshing token...";

        try
        {
            Debug.WriteLine("[DashboardViewModel] Refreshing token...");

            var success = await _ssoPlugin.RefreshTokenAsync();

            if (success)
            {
                var token = _ssoPlugin.GetAccessToken();
                AccessToken = token?.Substring(0, Math.Min(50, token.Length)) + "..." ?? "Not available";
                StatusMessage = "Token refreshed successfully";
                Debug.WriteLine("[DashboardViewModel] ✓ Token refreshed successfully");
            }
            else
            {
                StatusMessage = "Token refresh failed";
                Debug.WriteLine("[DashboardViewModel] ✗ Token refresh failed");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error refreshing token: {ex.Message}";
            Debug.WriteLine($"[DashboardViewModel] Error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task Logout()
    {
        IsLoading = true;
        StatusMessage = "Logging out...";

        try
        {
            Debug.WriteLine("[DashboardViewModel] Logging out...");

            var success = await _ssoPlugin.LogoutAsync();

            if (success)
            {
                StatusMessage = "Logged out successfully";
                Debug.WriteLine("[DashboardViewModel] ✓ Logout successful");

                // Use absolute routing with ///
                await Shell.Current.GoToAsync("///main");
            }
            else
            {
                StatusMessage = "Logout failed";
                Debug.WriteLine("[DashboardViewModel] ✗ Logout failed");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Logout error: {ex.Message}";
            Debug.WriteLine($"[DashboardViewModel] ✗ Logout error: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}