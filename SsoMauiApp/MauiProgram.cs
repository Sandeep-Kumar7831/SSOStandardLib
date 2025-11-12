using MauiSso.Plugin.Configuration;
using MauiSso.Plugin.Extensions;
using MauiSso.Plugin.Services;
using SsoMauiApp.Pages;

namespace SsoMauiApp;

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
        // Add SSO plugin with configuration
        .AddMauiSsoPlugin(config =>
        {
            config.Authority = "https://dev.psshub.honeywell.com/realms/catalystfabric";
            config.ClientId = "cf_honeywell_launcher";
            config.RedirectUri = "cfauth://callback";
            config.PostLogoutRedirectUri = "cfauth://callback";
            config.Scope = "openid email profile offline_access";
            config.EnableDPoP = false;
        });

        // Register pages
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<DashboardPage>();
        builder.Services.AddSingleton<DashboardViewModel>();
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }
}