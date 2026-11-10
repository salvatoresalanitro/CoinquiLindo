using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using CoinquiLindo.Mobile.Services;

namespace CoinquiLindo.Mobile
{
    // SingleTop: tapping a push while the app is open reuses this activity (OnNewIntent) instead of stacking a new one
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            HandlePushTap(Intent);
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            HandlePushTap(intent);
        }

        // FCM (background) and local (foreground) notifications both carry the data payload as extras
        private static void HandlePushTap(Intent? intent)
        {
            if (intent?.Extras?.GetString("notificationId") is not null)
            {
                PushBridge.RequestNavigation(PushBridge.NotificationsRoute);
            }
        }
    }
}
