using MauiSso.Plugin.Configuration;
using MauiSso.Plugin.Extensions;
using MauiSso.Plugin.Services;
using MauiSsoLibrary.Services;
using SsoMauiApp.Pages;
using Microsoft.Maui.Storage;

namespace SsoMauiApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        _ = builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .AddMauiSsoPlugin(config =>
            {
                config.Authority = "https://dev.psshub.honeywell.com/realms/catalystfabric";
                config.ClientId = "cf_honeywell_launcher";
                config.RedirectUri = "cfauth://com.honeywell.tools.honeywelllauncher/callback";
                config.PostLogoutRedirectUri = "cfauth://com.honeywell.tools.honeywelllauncher/callback";
                config.Scope = "openid email profile";
                config.EnableDPoP = false;
            },
            customTokenStore: new CrossAppTokenStore());

        // Register pages
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<DashboardPage>();
        builder.Services.AddSingleton<DashboardViewModel>();
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }
}