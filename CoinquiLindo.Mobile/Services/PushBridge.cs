namespace CoinquiLindo.Mobile.Services
{
    // Static bridge between OS-created platform components (FCM service, activity intents) and the Blazor app
    public static class PushBridge
    {
        public const string AndroidChannelId = "coinquilindo_default";
        public const string NotificationsRoute = "/notifications";

        private const string InstallationIdKey = "push.installationId";

        private static string? pendingRoute;

        public static event Action<string>? InstallationIdChanged;
        public static event Action<int?>? MessageReceived;
        public static event Action? PendingRouteChanged;

        // Persisted: FCM only calls back when the ID changes or on its periodic sync, not at every start
        public static string? InstallationId => Preferences.Default.Get<string?>(InstallationIdKey, null);

        public static void OnInstallationId(string installationId)
        {
            Preferences.Default.Set(InstallationIdKey, installationId);
            InstallationIdChanged?.Invoke(installationId);
        }

        public static void OnMessageReceived(int? unreadCount) => MessageReceived?.Invoke(unreadCount);

        public static void RequestNavigation(string route)
        {
            pendingRoute = route;
            PendingRouteChanged?.Invoke();
        }

        public static string? ConsumePendingRoute()
        {
            var route = pendingRoute;
            pendingRoute = null;
            return route;
        }
    }
}
