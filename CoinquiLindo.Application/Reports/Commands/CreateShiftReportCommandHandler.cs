using CoinquiLindo.Application.Notifications;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Domain.ValueObjects;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Reports.Commands
{
    public class CreateShiftReportCommandHandler(
        IHouseRepository houseRepository,
        IShiftRepository shiftRepository,
        IReportPhotoStorage photoStorage,
        NotificationPublisher notificationPublisher)
    {
        public async Task<ReportResponse> Handle(CreateShiftReportCommand request, CancellationToken cancellationToken)
        {
            var (house, shift, reporter) = await ReportMapping.GetMemberShiftAsync(houseRepository, shiftRepository,
                request.HouseId, request.ShiftId, request.RequesterUserId, cancellationToken);

            var photo = await ReadBoundedAsync(request.Photo, cancellationToken);
            var format = ReportPhotoFormat.Detect(photo);

            // Every domain rule runs before touching the disk, so rejected requests never leave files behind
            var report = new Report(reporter, request.Description!, format.CreatePhotoKey());
            shift.AddReport(report);

            await photoStorage.SaveAsync(report.PhotoKey,
                new MemoryStream(photo.Array!, photo.Offset, photo.Count, writable: false), cancellationToken);

            try
            {
                await shiftRepository.UpdateAsync(shift, cancellationToken);
            }
            catch
            {
                await photoStorage.DeleteAsync(report.PhotoKey, CancellationToken.None);
                throw;
            }

            await notificationPublisher.ReportCreatedAsync(house, shift, report, cancellationToken);

            return report.ToResponse(shift, reporter.Id);
        }

        // Reads at most MaxBytes + 1 so oversized uploads are rejected without buffering them entirely
        private static async Task<ArraySegment<byte>> ReadBoundedAsync(Stream? photo, CancellationToken cancellationToken)
        {
            if (photo is null)
            {
                return ArraySegment<byte>.Empty;
            }

            var buffer = new byte[ReportPhotoFormat.MaxBytes + 1];
            var total = 0;
            int read;
            while (total < buffer.Length
                && (read = await photo.ReadAsync(buffer.AsMemory(total), cancellationToken)) > 0)
            {
                total += read;
            }

            return new ArraySegment<byte>(buffer, 0, total);
        }
    }
}
