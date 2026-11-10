using Android.App;
using Android.Content;
using AndroidX.Core.App;
using CoinquiLindo.Mobile.Services;
using Firebase.Messaging;

namespace CoinquiLindo.Mobile
{
    [Service(Exported = false)]
    [IntentFilter(["com.google.firebase.MESSAGING_EVENT"])]
    public class PushMessagingService : FirebaseMessagingService
    {
        // Called on registration, on FID rotation (reinstall, data cleared, restore) and on FCM periodic sync
        public override void OnRegistered(string installationId)
        {
            base.OnRegistered(installationId);
            PushBridge.OnInstallationId(installationId);
        }

        // Background notification messages are shown by FCM itself; this runs only while the app is in foreground
        public override void OnMessageReceived(RemoteMessage message)
        {
            base.OnMessageReceived(message);

            int? unreadCount = message.Data.TryGetValue("unreadCount", out var raw) && int.TryParse(raw, out var count)
                ? count
                : null;
            PushBridge.OnMessageReceived(unreadCount);

            if (message.GetNotification() is { } notification)
            {
                ShowLocalNotification(notification.Title, notification.Body, message.Data);
            }
        }

        private void ShowLocalNotification(string? title, string? body, IDictionary<string, string> data)
        {
            var intent = new Intent(this, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            foreach (var (key, value) in data)
            {
                intent.PutExtra(key, value);
            }

            var requestCode = data.TryGetValue("notificationId", out var id) ? id.GetHashCode() : 0;
            var pendingIntent = PendingIntent.GetActivity(this, requestCode, intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            var builder = new NotificationCompat.Builder(this, PushBridge.AndroidChannelId);
            builder.SetSmallIcon(Resource.Mipmap.appicon);
            builder.SetContentTitle(title);
            builder.SetContentText(body);
            builder.SetStyle(new NotificationCompat.BigTextStyle().BigText(body));
            builder.SetAutoCancel(true);
            builder.SetPriority(NotificationCompat.PriorityHigh);
            builder.SetContentIntent(pendingIntent);

            // Same tag as the server push: a refreshed vote summary replaces the previous one
            var tag = data.TryGetValue("notificationId", out var notificationId) ? notificationId : null;
            NotificationManagerCompat.From(this)!.Notify(tag, 0, builder.Build()!);
        }
    }
}
