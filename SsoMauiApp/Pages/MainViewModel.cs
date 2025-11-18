// SsoMauiApp/Pages/MainViewModel.cs - UPDATED

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiSso.Plugin.Services;
using System.Diagnostics;

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
            Debug.WriteLine("\n[MainViewModel] ========== Initialize ==========");
            var initialized = await _ssoPlugin.InitializeAsync();

            if (initialized)
            {
                IsAuthenticated = _ssoPlugin.IsAuthenticated();

                if (IsAuthenticated)
                {
                    StatusMessage = "✓ Already authenticated! Navigating to dashboard...";
                    Debug.WriteLine("[MainViewModel] ✓ Already authenticated, navigating to dashboard...");

                    // Add a small delay to ensure UI updates
                    await Task.Delay(500);

                    // Use absolute routing with ///
                    await Shell.Current.GoToAsync("///dashboard");
                }
                else
                {
                    StatusMessage = "Ready to login";
                    Debug.WriteLine("[MainViewModel] Not authenticated, ready for login");
                }
            }
            else
            {
                StatusMessage = "Initialization failed";
                Debug.WriteLine("[MainViewModel] ✗ Initialization failed");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Debug.WriteLine($"[MainViewModel] ✗ Initialize error: {ex.Message}\n{ex.StackTrace}");
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
        StatusMessage = "Opening login browser...";

        try
        {
            Debug.WriteLine("\n[MainViewModel] ========== Login ==========");
            var success = await _ssoPlugin.LoginAsync();

            if (success)
            {
                IsAuthenticated = true;
                StatusMessage = "✓ Login successful! Navigating to dashboard...";
                Debug.WriteLine("[MainViewModel] ✓ Login successful, navigating to dashboard...");

                // Add a small delay to ensure tokens are saved and UI updates
                await Task.Delay(500);

                // Use absolute routing with ///
                await Shell.Current.GoToAsync("///dashboard");
            }
            else
            {
                IsAuthenticated = false;
                StatusMessage = "✗ Login failed";
                Debug.WriteLine("[MainViewModel] ✗ Login failed");
            }
        }
        catch (Exception ex)
        {
            IsAuthenticated = false;
            StatusMessage = $"Login error: {ex.Message}";
            Debug.WriteLine($"[MainViewModel] ✗ Login exception: {ex.Message}\n{ex.StackTrace}");
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
            Debug.WriteLine($"[MainViewModel] Authentication changed: {IsAuthenticated}");

            if (IsAuthenticated)
            {
                StatusMessage = "✓ Authentication changed to authenticated";
            }
            else
            {
                StatusMessage = "Authentication changed to not authenticated";
            }
        });
    }
}