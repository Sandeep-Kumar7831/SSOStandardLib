using Duende.IdentityModel.OidcClient.Browser;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MauiSsoLibrary.Services
{
    /// <summary>
    /// Factory for creating platform-specific browser implementations
    /// </summary>
    public interface IBrowserFactory
    {
        IBrowser CreateBrowser();
    }

    /// <summary>
    /// Callback handler for OAuth redirects
    /// </summary>
    public interface ICallbackHandler
    {
        void RegisterCallback(string expectedCallback, Action<string> onCallback, Action onCancel = null);
        void UnregisterCallback(string expectedCallback);
        void HandleCallback(string callbackUrl);
    }

    /// <summary>
    /// Global callback manager for handling OAuth redirects
    /// </summary>
    public static class CallbackManager
    {
        private static readonly ConcurrentDictionary<string, CallbackRegistration> _callbacks = new();
        private static readonly object _lockObject = new();

        public static void RegisterCallback(string expectedCallback, Action<string> onCallback, Action onCancel = null)
        {
            try
            {
                var uri = new Uri(expectedCallback);
                var baseUrl = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";

                Debug.WriteLine($"[CallbackManager] Registering callback for: {baseUrl}");
                Debug.WriteLine($"[CallbackManager] Expected callback: {expectedCallback}");

                lock (_lockObject)
                {
                    _callbacks[baseUrl] = new CallbackRegistration
                    {
                        ExpectedCallback = expectedCallback,
                        OnCallback = onCallback,
                        OnCancel = onCancel
                    };
                }

                Debug.WriteLine($"[CallbackManager] ✓ Callback registered. Total callbacks: {_callbacks.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CallbackManager] ✗ Error registering callback: {ex.Message}");
            }
        }

        public static void UnregisterCallback(string expectedCallback)
        {
            try
            {
                var uri = new Uri(expectedCallback);
                var baseUrl = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";

                lock (_lockObject)
                {
                    if (_callbacks.TryRemove(baseUrl, out _))
                    {
                        Debug.WriteLine($"[CallbackManager] ✓ Unregistered: {baseUrl}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CallbackManager] ✗ Error unregistering: {ex.Message}");
            }
        }

        public static bool HandleCallback(string callbackUrl)
        {
            try
            {
                Debug.WriteLine($"\n[CallbackManager] ========== Processing Callback ==========");
                Debug.WriteLine($"[CallbackManager] Callback URL: {callbackUrl}");

                var uri = new Uri(callbackUrl);
                var callbackScheme = uri.Scheme;
                var callbackHost = uri.Host;
                var callbackPath = uri.AbsolutePath;

                Debug.WriteLine($"[CallbackManager] Parsed - Scheme: {callbackScheme}, Host: {callbackHost}, Path: {callbackPath}");

                lock (_lockObject)
                {
                    // Try exact match first: scheme://host/path
                    var exactUrl = $"{callbackScheme}://{callbackHost}{callbackPath}";
                    Debug.WriteLine($"[CallbackManager] Trying exact match: {exactUrl}");
                    Debug.WriteLine($"[CallbackManager] Available callbacks: {string.Join(", ", _callbacks.Keys)}");

                    if (_callbacks.TryGetValue(exactUrl, out var registration))
                    {
                        Debug.WriteLine($"[CallbackManager] ✓ Found exact callback handler");
                        InvokeCallback(registration, uri);
                        return true;
                    }

                    // Try scheme://host (without path) as fallback
                    var baseUrl = $"{callbackScheme}://{callbackHost}";
                    Debug.WriteLine($"[CallbackManager] Trying base match: {baseUrl}");

                    if (_callbacks.TryGetValue(baseUrl, out var registration2))
                    {
                        Debug.WriteLine($"[CallbackManager] ✓ Found base callback handler");
                        InvokeCallback(registration2, uri);
                        return true;
                    }

                    Debug.WriteLine($"[CallbackManager] ✗ No handler found for {callbackUrl}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CallbackManager] ✗ Error: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        private static void InvokeCallback(CallbackRegistration registration, Uri uri)
        {
            if (uri.Query.Contains("error=access_denied") || uri.Query.Contains("error=user_cancelled"))
            {
                Debug.WriteLine("[CallbackManager] User cancelled");
                registration.OnCancel?.Invoke();
            }
            else
            {
                Debug.WriteLine("[CallbackManager] Invoking success callback");
                registration.OnCallback?.Invoke(uri.OriginalString);
            }
        }

        private class CallbackRegistration
        {
            public string ExpectedCallback { get; set; }
            public Action<string> OnCallback { get; set; }
            public Action OnCancel { get; set; }
        }
    }

    /// <summary>
    /// Platform detection and browser factory implementation
    /// </summary>
    public class PlatformBrowserFactory : IBrowserFactory
    {
        private readonly IPlatformDetector _platformDetector;

        public PlatformBrowserFactory(IPlatformDetector platformDetector)
        {
            _platformDetector = platformDetector ?? throw new ArgumentNullException(nameof(platformDetector));
        }

        public IBrowser CreateBrowser()
        {
            var platform = _platformDetector.GetCurrentPlatform();

            return platform switch
            {
                PlatformType.Android => new AndroidBrowser(_platformDetector),
                PlatformType.iOS => new IosBrowser(_platformDetector),
                PlatformType.Windows => new WindowsBrowser(),
                PlatformType.macOS => new MacOSBrowser(),
                PlatformType.Linux => new LinuxBrowser(),
                _ => new SystemBrowser()
            };
        }
    }

    /// <summary>
    /// Platform detection interface - implement per platform
    /// </summary>
    public interface IPlatformDetector
    {
        PlatformType GetCurrentPlatform();
        void OpenBrowser(string url);
    }

    public enum PlatformType
    {
        Unknown,
        Android,
        iOS,
        Windows,
        macOS,
        Linux
    }
    /// <summary>
    /// Android browser implementation with proper callback handling
    /// </summary>
    public class AndroidBrowser : IBrowser
    {
        private readonly IPlatformDetector _platformDetector;
        private TaskCompletionSource<BrowserResult>? _tcs;
        private string _expectedCallback = string.Empty;

        public AndroidBrowser(IPlatformDetector platformDetector)
        {
            _platformDetector = platformDetector;
        }

        public async Task<BrowserResult> InvokeAsync(
            BrowserOptions options,
            System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                _tcs = new TaskCompletionSource<BrowserResult>();
                _expectedCallback = options.EndUrl;

                System.Diagnostics.Debug.WriteLine($"\n[AndroidBrowser] ========== Starting Login ==========");
                System.Diagnostics.Debug.WriteLine($"[AndroidBrowser] StartUrl: {options.StartUrl}");
                System.Diagnostics.Debug.WriteLine($"[AndroidBrowser] EndUrl (Callback): {options.EndUrl}");

                // CRITICAL: Register callback BEFORE opening browser
                SetupCallbackHandling(options.EndUrl);

                // Set up cancellation token handling
                using (cancellationToken.Register(() =>
                {
                    System.Diagnostics.Debug.WriteLine("[AndroidBrowser] Cancellation token triggered");
                    _tcs?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.UserCancel
                    });
                }))
                {
                    // Add a timeout of 5 minutes
                    var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5));
                    var resultTask = _tcs.Task;

                    // Open browser on main thread
                    _platformDetector.OpenBrowser(options.StartUrl);
                    System.Diagnostics.Debug.WriteLine("[AndroidBrowser] Browser opened, waiting for callback...");

                    var completedTask = await Task.WhenAny(resultTask, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        System.Diagnostics.Debug.WriteLine("[AndroidBrowser] Timeout waiting for callback");
                        return new BrowserResult
                        {
                            ResultType = BrowserResultType.Timeout
                        };
                    }

                    var result = await resultTask;
                    System.Diagnostics.Debug.WriteLine($"[AndroidBrowser] Result received: {result.ResultType}");
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidBrowser] ✗ Error: {ex.Message}\n{ex.StackTrace}");
                return new BrowserResult
                {
                    ResultType = BrowserResultType.UnknownError,
                    Error = ex.Message
                };
            }
            finally
            {
                // Clean up
                System.Diagnostics.Debug.WriteLine("[AndroidBrowser] Cleaning up callback");
                CallbackManager.UnregisterCallback(_expectedCallback);
            }
        }

        private void SetupCallbackHandling(string expectedCallback)
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidBrowser] Setting up callback handling for: {expectedCallback}");

            CallbackManager.RegisterCallback(
                expectedCallback,
                onCallback: (callbackUrl) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[AndroidBrowser] ✓ Success callback received");
                    System.Diagnostics.Debug.WriteLine($"[AndroidBrowser] Callback URL: {callbackUrl}");
                    _tcs?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.Success,
                        Response = callbackUrl
                    });
                },
                onCancel: () =>
                {
                    System.Diagnostics.Debug.WriteLine("[AndroidBrowser] ✗ User cancelled");
                    _tcs?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.UserCancel
                    });
                }
            );
        }
    }

    /// <summary>
    /// iOS browser implementation
    /// </summary>
    public class IosBrowser : IBrowser
    {
        private readonly IPlatformDetector _platformDetector;
        private TaskCompletionSource<BrowserResult>? _tcs;
        private string _expectedCallback = string.Empty;

        public IosBrowser(IPlatformDetector platformDetector)
        {
            _platformDetector = platformDetector;
        }

        public async Task<BrowserResult> InvokeAsync(
            BrowserOptions options,
            System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                _tcs = new TaskCompletionSource<BrowserResult>();
                _expectedCallback = options.EndUrl;

                // Set up callback handling before opening browser
                SetupCallbackHandling(options.EndUrl);

                // Set up cancellation token handling
                cancellationToken.Register(() =>
                {
                    _tcs?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.UserCancel
                    });
                });

                _platformDetector.OpenBrowser(options.StartUrl);

                // Wait for callback or cancellation
                var result = await _tcs.Task.ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                return new BrowserResult
                {
                    ResultType = BrowserResultType.UnknownError,
                    Error = ex.Message
                };
            }
            finally
            {
                // Clean up
                CallbackManager.UnregisterCallback(_expectedCallback);
            }
        }

        private void SetupCallbackHandling(string expectedCallback)
        {
            CallbackManager.RegisterCallback(
                expectedCallback,
                onCallback: (callbackUrl) =>
                {
                    _tcs?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.Success,
                        Response = callbackUrl
                    });
                },
                onCancel: () =>
                {
                    _tcs?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.UserCancel
                    });
                }
            );
        }
    }

    /// <summary>
    /// Windows browser implementation
    /// </summary>
    public class WindowsBrowser : IBrowser
    {
        private TaskCompletionSource<BrowserResult>? _tcs;
        private string _expectedCallback = string.Empty;

        public async Task<BrowserResult> InvokeAsync(
            BrowserOptions options,
            System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                _tcs = new TaskCompletionSource<BrowserResult>();
                _expectedCallback = options.EndUrl;

                // Set up callback handling before opening browser
                SetupCallbackHandling(options.EndUrl);

                // Set up cancellation token handling
                cancellationToken.Register(() =>
                {
                    _tcs?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.UserCancel
                    });
                });

#if WINDOWS
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = options.StartUrl,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
#endif

                // Wait for callback or cancellation
                var result = await _tcs.Task.ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                return new BrowserResult
                {
                    ResultType = BrowserResultType.UnknownError,
                    Error = ex.Message
                };
            }
            finally
            {
                // Clean up
                CallbackManager.UnregisterCallback(_expectedCallback);
            }
        }

        private void SetupCallbackHandling(string expectedCallback)
        {
            CallbackManager.RegisterCallback(
                expectedCallback,
                onCallback: (callbackUrl) =>
                {
                    _tcs?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.Success,
                        Response = callbackUrl
                    });
                },
                onCancel: () =>
                {
                    _tcs?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.UserCancel
                    });
                }
            );
        }
    }

    /// <summary>
    /// macOS browser implementation
    /// </summary>
    public class MacOSBrowser : IBrowser
    {
        private TaskCompletionSource<BrowserResult>? _tcs;
        private string _expectedCallback = string.Empty;

        public async Task<BrowserResult> InvokeAsync(
            BrowserOptions options,
            System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                _tcs = new TaskCompletionSource<BrowserResult>();
                _expectedCallback = options.EndUrl;

                // Set up callback handling before opening browser
                SetupCallbackHandling(options.EndUrl);

                // Set up cancellation token handling
                cancellationToken.Register(() =>
                {
                    _tcs?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.UserCancel
                    });
                });

#if MACCATALYST
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = options.StartUrl,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
#endif

                // Wait for callback or cancellation
                var result = await _tcs.Task.ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                return new BrowserResult
                {
                    ResultType = BrowserResultType.UnknownError,
                    Error = ex.Message
                };
            }
            finally
            {
                // Clean up
                CallbackManager.UnregisterCallback(_expectedCallback);
            }
        }

        private void SetupCallbackHandling(string expectedCallback)
        {
            CallbackManager.RegisterCallback(
                expectedCallback,
                onCallback: (callbackUrl) =>
                {
                    _tcs?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.Success,
                        Response = callbackUrl
                    });
                },
                onCancel: () =>
                {
                    _tcs?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.UserCancel
                    });
                }
            );
        }
    }

    /// <summary>
    /// Linux browser implementation
    /// </summary>
    public class LinuxBrowser : IBrowser
    {
        private TaskCompletionSource<BrowserResult>? _tcs;
        private string _expectedCallback = string.Empty;

        public async Task<BrowserResult> InvokeAsync(
            BrowserOptions options,
            System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                _tcs = new TaskCompletionSource<BrowserResult>();
                _expectedCallback = options.EndUrl;

                // Set up callback handling before opening browser
                SetupCallbackHandling(options.EndUrl);

                // Set up cancellation token handling
                cancellationToken.Register(() =>
                {
                    _tcs?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.UserCancel
                    });
                });

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = options.StartUrl,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);

                // Wait for callback or cancellation
                var result = await _tcs.Task.ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                return new BrowserResult
                {
                    ResultType = BrowserResultType.UnknownError,
                    Error = ex.Message
                };
            }
            finally
            {
                // Clean up
                CallbackManager.UnregisterCallback(_expectedCallback);
            }
        }

        private void SetupCallbackHandling(string expectedCallback)
        {
            CallbackManager.RegisterCallback(
                expectedCallback,
                onCallback: (callbackUrl) =>
                {
                    _tcs?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.Success,
                        Response = callbackUrl
                    });
                },
                onCancel: () =>
                {
                    _tcs?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.UserCancel
                    });
                }
            );
        }
    }

    /// <summary>
    /// Generic system browser fallback
    /// </summary>
    public class SystemBrowser : IBrowser
    {
        private TaskCompletionSource<BrowserResult>? _tcs;
        private string _expectedCallback = string.Empty;

        public async Task<BrowserResult> InvokeAsync(
            BrowserOptions options,
            System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                _tcs = new TaskCompletionSource<BrowserResult>();
                _expectedCallback = options.EndUrl;

                // Set up callback handling before opening browser
                SetupCallbackHandling(options.EndUrl);

                // Set up cancellation token handling
                cancellationToken.Register(() =>
                {
                    _tcs?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.UserCancel
                    });
                });

#if WINDOWS
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = options.StartUrl,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
#elif MACCATALYST
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = options.StartUrl,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
#else
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = options.StartUrl,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
#endif

                // Wait for callback or cancellation
                var result = await _tcs.Task.ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                return new BrowserResult
                {
                    ResultType = BrowserResultType.UnknownError,
                    Error = ex.Message
                };
            }
            finally
            {
                // Clean up
                CallbackManager.UnregisterCallback(_expectedCallback);
            }
        }

        private void SetupCallbackHandling(string expectedCallback)
        {
            CallbackManager.RegisterCallback(
                expectedCallback,
                onCallback: (callbackUrl) =>
                {
                    _tcs?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.Success,
                        Response = callbackUrl
                    });
                },
                onCancel: () =>
                {
                    _tcs?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.UserCancel
                    });
                }
            );
        }
    }
}
