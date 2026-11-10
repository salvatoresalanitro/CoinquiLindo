using CoinquiLindo.Client;
using CoinquiLindo.Shared.Contracts;
using Microsoft.Extensions.Logging;

namespace CoinquiLindo.Mobile.Services
{
    // Links this app install to the signed-in account for FCM pushes (ADR-017). Android only for now
    public sealed class PushRegistrationService(
        CoinquiLindoApiClient apiClient,
        UnreadNotificationsState unreadState,
        ILogger<PushRegistrationService> logger) : IDisposable
    {
        private bool isSubscribed;

        // Runs at every sign-in: PUT is idempotent and re-links the device after an account switch
        public async Task StartAsync()
        {
            if (!IsPushAvailable())
            {
                return;
            }

            if (!isSubscribed)
            {
                isSubscribed = true;
                PushBridge.InstallationIdChanged += OnInstallationIdChanged;
                PushBridge.MessageReceived += OnMessageReceived;
            }

            // Denial only hides system notifications: registration still happens so a later grant just works
            await RequestPermissionAsync();

            if (PushBridge.InstallationId is { } knownId)
            {
                await RegisterAsync(knownId);
            }

            TriggerPlatformRegistration();
        }

        // Called before logout, while the access token is still valid
        public async Task UnregisterAsync()
        {
            if (PushBridge.InstallationId is { } installationId)
            {
                await apiClient.UnregisterPushDeviceAsync(installationId);
            }
        }

        private void OnInstallationIdChanged(string installationId) => _ = RegisterAsync(installationId);

        private void OnMessageReceived(int? unreadCount)
        {
            if (unreadCount is { } count)
            {
                unreadState.SetUnreadCount(count);
            }
            else
            {
                _ = unreadState.RefreshAsync();
            }
        }

        private async Task RegisterAsync(string installationId)
        {
            var result = await apiClient.RegisterPushDeviceAsync(installationId, PushPlatformValue.Android);
            if (!result.IsSuccess)
            {
                logger.LogWarning("Push device registration failed: {ErrorCode}", result.Error.ErrorCode);
            }
        }

        private async Task RequestPermissionAsync()
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<Permissions.PostNotifications>);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Notification permission request failed");
            }
        }

        private bool IsPushAvailable()
        {
#if ANDROID
            // Without google-services.json Firebase is not initialized: the app keeps working with in-app notifications only
            return Firebase.FirebaseApp.GetApps(Platform.AppContext).Count > 0;
#else
            return false;
#endif
        }

        private void TriggerPlatformRegistration()
        {
#if ANDROID
            try
            {
                // Result arrives asynchronously in PushMessagingService.OnRegistered
                Firebase.Messaging.FirebaseMessaging.Instance.Register();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "FCM registration failed");
            }
#endif
        }

        public void Dispose()
        {
            PushBridge.InstallationIdChanged -= OnInstallationIdChanged;
            PushBridge.MessageReceived -= OnMessageReceived;
        }
    }
}
