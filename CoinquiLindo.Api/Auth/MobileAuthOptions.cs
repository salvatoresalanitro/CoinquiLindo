namespace CoinquiLindo.Api.Auth
{
    public sealed class MobileAuthOptions
    {
        public const string SectionName = "Auth";

        // Custom scheme registered by the mobile app (WebAuthenticator callback)
        public string MobileCallbackUri { get; set; } = "coinquilindo://auth";
    }
}
