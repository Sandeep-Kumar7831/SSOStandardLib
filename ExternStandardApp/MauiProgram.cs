using MauiSso.Plugin.Configuration;
using MauiSso.Plugin.Extensions;
using MauiSsoLibrary.Services;
using ExternStandardApp.Pages;
using ExternStandardApp;

namespace ExternStandardMauiApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            // Add SSO plugin - same configuration as SsoMauiApp
            .AddMauiSsoPlugin(config =>
            {
                //config.Authority = "https://dev.psshub.honeywell.com/realms/catalystfabric";
                //config.ClientId = "cf_honeywell_launcher";
                //config.RedirectUri = "cfauth://com.honeywell.tools.tokenreader/callback";
                //config.PostLogoutRedirectUri = "cfauth://com.honeywell.tools.tokenreader/callback";
                //config.Scope = "openid email profile";
                //config.EnableDPoP = false;

                config.Authority = "https://dev.psshub.honeywell.com/realms/catalystfabric";
                config.ClientId = "cf_honeywell_launcher";
                config.RedirectUri = "cfauth://com.honeywell.tools.honeywelllauncher/callback";
                config.PostLogoutRedirectUri = "cfauth://com.honeywell.tools.honeywelllauncher/callback";
                config.Scope = "openid email profile";
                config.EnableDPoP = false; // Enable DPoP
            },
            // Use same SharedSecureTokenStore to read tokens from SsoMauiApp
            customTokenStore: new SharedSecureTokenStore(
                storageGroupId: "honeywell_launcher"
            ));

        // Register pages
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }
}
