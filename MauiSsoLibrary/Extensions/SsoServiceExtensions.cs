using Duende.IdentityModel.OidcClient.Browser;
using Microsoft.Extensions.DependencyInjection;
using MauiSsoLibrary.Services;
using System;

namespace MauiSsoLibrary.Extensions
{
    public static class SsoServiceExtensions
    {
        /// <summary>
        /// Add SSO services to DI container
        /// </summary>
        public static IServiceCollection AddMauiSso(
            this IServiceCollection services,
            Action<SsoConfiguration> configureOptions,
            ITokenStore? tokenStore = null,
            IBrowser? browser = null,
            IPlatformDetector? platformDetector = null)
        {
            var config = new SsoConfiguration();
            configureOptions(config);

            var errors = config.GetValidationErrors();
            if (errors.Count > 0)
                throw new ArgumentException($"Invalid SSO configuration: {string.Join(", ", errors)}");

            services.AddSingleton(config);

            // Register token store
            if (tokenStore != null)
                services.AddSingleton(tokenStore);
            else
                services.AddSingleton<ITokenStore, TokenStore>();

            // Register platform detector if provided
            if (platformDetector != null)
                services.AddSingleton(platformDetector);

            // Register browser
            if (browser != null)
            {
                services.AddSingleton(browser);
            }
            else
            {
                services.AddSingleton<IBrowserFactory, PlatformBrowserFactory>();
                services.AddSingleton(sp =>
                {
                    var factory = sp.GetRequiredService<IBrowserFactory>();
                    return factory.CreateBrowser();
                });
            }

            // Register auth service
            services.AddSingleton<IOidcAuthService>(sp =>
            {
                var store = sp.GetRequiredService<ITokenStore>();
                var b = sp.GetRequiredService<IBrowser>();
                return new OidcAuthService(store, config, b);
            });

            return services;
        }
    }
}