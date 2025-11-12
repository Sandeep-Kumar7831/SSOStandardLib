using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiSso.Plugin.Services;

namespace SsoMauiApp.Pages;

public partial class MainViewModel : ObservableObject
{
    private readonly IMauiSsoPlugin _ssoPlugin;
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    bool isAuthenticated;

    [ObservableProperty]
    bool isLoading;

    [ObservableProperty]
    string statusMessage = "Initializing...";

    public MainViewModel(IMauiSsoPlugin ssoPlugin, IServiceProvider serviceProvider)
    {
        _ssoPlugin = ssoPlugin;
        _serviceProvider = serviceProvider;

        // Listen to authentication changes
        _ssoPlugin.AuthenticationChanged += OnAuthenticationChanged;
    }

    [RelayCommand]
    public async Task Initialize()
    {
        IsLoading = true;
        StatusMessage = "Initializing SSO...";

        try
        {
            var initialized = await _ssoPlugin.InitializeAsync();

            if (initialized)
            {
                IsAuthenticated = _ssoPlugin.IsAuthenticated();

                if (IsAuthenticated)
                {
                    StatusMessage = "Already authenticated";
                    await Shell.Current.GoToAsync("dashboard");
                }
                else
                {
                    StatusMessage = "Ready to login";
                }
            }
            else
            {
                StatusMessage = "Initialization failed";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task Login()
    {
        IsLoading = true;
        StatusMessage = "Logging in...";

        try
        {
            var success = await _ssoPlugin.LoginAsync();

            if (success)
            {
                IsAuthenticated = true;
                StatusMessage = "Login successful";
                await Shell.Current.GoToAsync("dashboard");
            }
            else
            {
                StatusMessage = "Login failed";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Login error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnAuthenticationChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsAuthenticated = _ssoPlugin.IsAuthenticated();
        });
    }
}