using CoinquiLindo.Application.Notifications;
using FirebaseAdmin.Messaging;

namespace CoinquiLindo.Infrastructure.Push
{
    public static class FcmMessageFactory
    {
        // Must match the channel created by the Android app (MainApplication)
        public const string AndroidChannelId = "coinquilindo_default";

        public static MulticastMessage Create(PushMessage push) => new()
        {
            // Firebase Installation IDs (Fids): registration tokens are deprecated since FirebaseAdmin 3.6
            Fids = push.Tokens,
            Notification = new Notification
            {
                Title = push.Title,
                Body = push.Body
            },
            Data = push.Data,
            Android = new AndroidConfig
            {
                Priority = Priority.High,
                Notification = new AndroidNotification
                {
                    ChannelId = AndroidChannelId,
                    // Same tag = the device replaces the previous push of this notification (refreshed vote totals)
                    Tag = push.Tag,
                    // Must be set explicitly: unset, FirebaseAdmin sends year 1 and Android shows "2032y"
                    EventTimestamp = push.OccurredAt
                }
            }
        };
    }
}
