using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Content;
using System.Diagnostics;
using Debug = System.Diagnostics.Debug;

namespace SsoMauiApp
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTop,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "cfauth")]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Debug.WriteLine("=== MainActivity OnCreate ===");
            HandleCallbackIntent(Intent);
        }

        protected override void OnNewIntent(Intent intent)
        {
            base.OnNewIntent(intent);
            Debug.WriteLine("=== MainActivity OnNewIntent ===");
            Intent = intent;
            HandleCallbackIntent(intent);
        }

        private void HandleCallbackIntent(Intent intent)
        {
            if (intent == null)
                return;

            var uri = intent.Data;
            if (uri == null)
                return;

            var callbackUrl = uri.ToString();
            Debug.WriteLine($"✓ MainActivity received callback: {callbackUrl}");

            if (uri.Scheme == "cfauth")
            {
                try
                {
                    bool handled = MauiSsoLibrary.Services.CallbackManager.HandleCallback(callbackUrl);
                    Debug.WriteLine($"✓ Callback handled: {handled}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"✗ Error handling callback: {ex.Message}");
                }
            }
        }
    }
}