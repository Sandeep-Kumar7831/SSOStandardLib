using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiSso.Plugin.Services;

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
            var token = _ssoPlugin.GetAccessToken();
            if (!string.IsNullOrEmpty(token))
            {
                AccessToken = token.Substring(0, Math.Min(50, token.Length)) + "...";
                StatusMessage = "User data loaded successfully";

                // Use this token to call your API
                // Example: var response = await HttpClient.GetAsync("api/user", token);
            }
            else
            {
                StatusMessage = "No access token available";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading data: {ex.Message}";
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
            var success = await _ssoPlugin.RefreshTokenAsync();

            if (success)
            {
                var token = _ssoPlugin.GetAccessToken();
                AccessToken = token?.Substring(0, Math.Min(50, token.Length)) + "..." ?? "Not available";
                StatusMessage = "Token refreshed successfully";
            }
            else
            {
                StatusMessage = "Token refresh failed";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error refreshing token: {ex.Message}";
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
            var success = await _ssoPlugin.LogoutAsync();

            if (success)
            {
                StatusMessage = "Logged out successfully";
                await Shell.Current.GoToAsync("main");
            }
            else
            {
                StatusMessage = "Logout failed";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Logout error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}