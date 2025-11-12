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

        public static void RegisterCallback(string expectedCallback, Action<string> onCallback, Action onCancel = null)
        {
            var uri = new Uri(expectedCallback);
            var baseUrl = $"{uri.Scheme}://{uri.Host}";

            _callbacks[baseUrl] = new CallbackRegistration
            {
                ExpectedCallback = expectedCallback,
                OnCallback = onCallback,
                OnCancel = onCancel
            };
        }

        public static void UnregisterCallback(string expectedCallback)
        {
            var uri = new Uri(expectedCallback);
            var baseUrl = $"{uri.Scheme}://{uri.Host}";
            _callbacks.TryRemove(baseUrl, out _);
        }

        public static bool HandleCallback(string callbackUrl)
        {
            try
            {
                Debug.WriteLine($"[CallbackManager] Processing: {callbackUrl}");

                var uri = new Uri(callbackUrl);

                // Build the base URL in the format: scheme://host/path
                var baseUrl = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";

                Debug.WriteLine($"[CallbackManager] Looking for: {baseUrl}");
                Debug.WriteLine($"[CallbackManager] Registered callbacks: {string.Join(", ", _callbacks.Keys)}");

                // Try exact match first
                if (_callbacks.TryGetValue(baseUrl, out var registration))
                {
                    Debug.WriteLine($"[CallbackManager] ✓ Found callback handler for {baseUrl}");

                    if (uri.Query.Contains("error=access_denied") || uri.Query.Contains("error=user_cancelled"))
                    {
                        Debug.WriteLine("[CallbackManager] User cancelled");
                        registration.OnCancel?.Invoke();
                    }
                    else
                    {
                        Debug.WriteLine("[CallbackManager] Invoking success callback");
                        registration.OnCallback?.Invoke(callbackUrl);
                    }
                    return true;
                }

                // Try base scheme://host as fallback
                var baseUrl2 = $"{uri.Scheme}://{uri.Host}";
                if (_callbacks.TryGetValue(baseUrl2, out var registration2))
                {
                    Debug.WriteLine($"[CallbackManager] ✓ Found callback handler (fallback) for {baseUrl2}");

                    if (uri.Query.Contains("error=access_denied") || uri.Query.Contains("error=user_cancelled"))
                    {
                        registration2.OnCancel?.Invoke();
                    }
                    else
                    {
                        registration2.OnCallback?.Invoke(callbackUrl);
                    }
                    return true;
                }

                Debug.WriteLine($"[CallbackManager] ✗ No handler found for {baseUrl}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CallbackManager] Error: {ex.Message}\n{ex.StackTrace}");
                return false;
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

                // Set up callback handling BEFORE opening browser
                SetupCallbackHandling(options.EndUrl);

                // Set up cancellation token handling with timeout
                using (cancellationToken.Register(() =>
                {
                    _tcs?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.UserCancel
                    });
                }))
                {
                    // Add a timeout of 5 minutes
                    var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5));
                    var resultTask = _tcs.Task;

                    _platformDetector.OpenBrowser(options.StartUrl);

                    var completedTask = await Task.WhenAny(resultTask, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        return new BrowserResult
                        {
                            ResultType = BrowserResultType.Timeout
                        };
                    }

                    return await resultTask;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AndroidBrowser error: {ex.Message}");
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
                    System.Diagnostics.Debug.WriteLine($"AndroidBrowser callback received: {callbackUrl}");
                    _tcs?.TrySetResult(new BrowserResult
                    {
                        ResultType = BrowserResultType.Success,
                        Response = callbackUrl
                    });
                },
                onCancel: () =>
                {
                    System.Diagnostics.Debug.WriteLine("AndroidBrowser user cancelled");
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
