using CoinquiLindo.Client;

namespace CoinquiLindo.Mobile.Services
{
    // Shared unread counter: the bell and the notifications page stay in sync without reloading each other
    public sealed class UnreadNotificationsState(CoinquiLindoApiClient apiClient)
    {
        public int UnreadCount { get; private set; }

        public event Action? Changed;

        // Silent on failure: the badge is a hint, errors surface on the notifications page
        public async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            var result = await apiClient.GetUnreadNotificationsCountAsync(cancellationToken);
            if (result.IsSuccess)
            {
                SetUnreadCount(result.Value);
            }
        }

        public void SetUnreadCount(int count)
        {
            count = Math.Max(0, count);
            if (count == UnreadCount)
            {
                return;
            }

            UnreadCount = count;
            Changed?.Invoke();
        }
    }
}
