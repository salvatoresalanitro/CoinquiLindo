using Android.App;
using Android.Runtime;
using CoinquiLindo.Mobile.Services;

namespace CoinquiLindo.Mobile
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        public override void OnCreate()
        {
            base.OnCreate();
            CreateNotificationChannel();
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        // Android 8+: notifications need a channel; the ID matches the server (FcmMessageFactory) and the manifest default
        private void CreateNotificationChannel()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                return;
            }

            var channel = new NotificationChannel(PushBridge.AndroidChannelId, "Notifiche CoinquiLindo", NotificationImportance.High)
            {
                Description = "Turni, segnalazioni e voti della tua casa"
            };
            ((NotificationManager)GetSystemService(NotificationService)!).CreateNotificationChannel(channel);
        }
    }
}
