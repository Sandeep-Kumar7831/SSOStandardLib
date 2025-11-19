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

        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }

    /// <summary>
    /// Get shared token directory - MUST MATCH SsoMauiApp
    /// </summary>
    private static string GetSharedTokenDirectory()
    {
#if ANDROID
        var externalFilesDir = Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath;

        if (externalFilesDir == null)
        {
            externalFilesDir = Android.App.Application.Context.CacheDir.AbsolutePath;
        }

        var sharedTokenDir = Path.Combine(externalFilesDir, "..", "..", "shared_sso_tokens");
        var fullPath = Path.GetFullPath(sharedTokenDir);

        System.Diagnostics.Debug.WriteLine($"[ExternStandardApp MauiProgram] Token directory: {fullPath}");
        return fullPath;
#else
        var sharedDir = Path.Combine(FileSystem.AppDataDirectory, "..", "shared_sso_tokens");
        return Path.GetFullPath(sharedDir);
#endif
    }
}