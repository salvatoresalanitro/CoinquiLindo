namespace CoinquiLindo.Mobile.Services
{
    public static class AuthConstants
    {
        // Must match the Android intent filter, the iOS/MacCatalyst CFBundleURLSchemes and the API "Auth:MobileCallbackUri"
        public const string CallbackScheme = "coinquilindo";
        public const string CallbackUri = CallbackScheme + "://auth";
    }
}
