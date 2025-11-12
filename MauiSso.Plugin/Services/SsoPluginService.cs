using MauiSsoLibrary.Services;
using Microsoft.Maui.Controls;
using System.Diagnostics;
using System.Threading.Tasks;
using System;

namespace MauiSso.Plugin.Services
{
    /// <summary>
    /// Plugin wrapper for SSO - provides high-level interface for MAUI apps
    /// </summary>
    public interface IMauiSsoPlugin
    {
        Task<bool> InitializeAsync();
        Task<bool> LoginAsync();
        Task<bool> LogoutAsync();
        Task<bool> RefreshTokenAsync();
        bool IsAuthenticated();
        string? GetAccessToken();
        event EventHandler? AuthenticationChanged;
    }

    public class MauiSsoPlugin : IMauiSsoPlugin
    {
        private readonly IOidcAuthService _oidcAuthService;
        private bool _isAuthenticated;

        public event EventHandler? AuthenticationChanged;

        public MauiSsoPlugin(IOidcAuthService oidcAuthService)
        {
            _oidcAuthService = oidcAuthService ?? throw new ArgumentNullException(nameof(oidcAuthService));
            _isAuthenticated = _oidcAuthService.IsAuthenticated();
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                // Check if already authenticated
                _isAuthenticated = _oidcAuthService.IsAuthenticated();

                if (_isAuthenticated)
                {
                    // Try to refresh if close to expiration
                    var refreshResult = await _oidcAuthService.RefreshTokenAsync();
                    if (!refreshResult.IsSuccess)
                    {
                        _isAuthenticated = false;
                        OnAuthenticationChanged();
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MauiSsoPlugin initialization error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> LoginAsync()
        {
            try
            {
                Debug.WriteLine("MauiSsoPlugin: Starting login");
                var result = await _oidcAuthService.LoginAsync();

                Debug.WriteLine($"MauiSsoPlugin: Login result - Success: {result.IsSuccess}, Error: {result.Error}");

                if (result.IsSuccess)
                {
                    _isAuthenticated = true;
                    OnAuthenticationChanged();
                    return true;
                }
                else
                {
                    Debug.WriteLine($"Login failed: {result.Error} - {result.ErrorDescription}");
                    _isAuthenticated = false;
                    OnAuthenticationChanged();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Login exception: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public async Task<bool> LogoutAsync()
        {
            try
            {
                await _oidcAuthService.LogoutAsync();
                _isAuthenticated = false;
                OnAuthenticationChanged();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Logout exception: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RefreshTokenAsync()
        {
            try
            {
                var result = await _oidcAuthService.RefreshTokenAsync();

                if (!result.IsSuccess)
                {
                    _isAuthenticated = false;
                    OnAuthenticationChanged();
                }

                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Token refresh exception: {ex.Message}");
                return false;
            }
        }

        public bool IsAuthenticated() => _isAuthenticated;

        public string? GetAccessToken() => _oidcAuthService.GetAccessToken();

        private void OnAuthenticationChanged()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                AuthenticationChanged?.Invoke(this, EventArgs.Empty);
            });
        }
    }
}