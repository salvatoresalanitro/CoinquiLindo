using CoinquiLindo.Domain.Enums;

namespace CoinquiLindo.Domain.Entities
{
    // Child of User: an app install that receives the user's pushes. Token holds the FCM Firebase Installation ID (FID)
    public class PushDevice
    {
        public const int MaxTokenLength = 512;

        public Guid Id { get; private set; }
        public string Token { get; private set; }
        public PushPlatform Platform { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime LastSeenAt { get; private set; }

        internal PushDevice(string token, PushPlatform platform, DateTime now)
        {
            Id = Guid.NewGuid();
            Token = token;
            Platform = platform;
            CreatedAt = now;
            LastSeenAt = now;
        }

        internal void Touch(PushPlatform platform, DateTime now)
        {
            Platform = platform;
            LastSeenAt = now;
        }

#pragma warning disable CS8618 // It's needed for EF Core to work properly, but we don't want to allow it to be used outside of EF Core.
        private PushDevice() { }
#pragma warning restore CS8618
    }
}
