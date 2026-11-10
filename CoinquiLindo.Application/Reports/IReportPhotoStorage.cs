namespace CoinquiLindo.Application.Reports
{
    public interface IReportPhotoStorage
    {
        // Must not leave a partial file behind when it throws
        Task SaveAsync(string photoKey, Stream content, CancellationToken cancellationToken);
        Task<Stream?> OpenReadAsync(string photoKey, CancellationToken cancellationToken);
        Task DeleteAsync(string photoKey, CancellationToken cancellationToken);
    }
}
