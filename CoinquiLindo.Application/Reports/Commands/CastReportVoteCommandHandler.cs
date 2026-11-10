using CoinquiLindo.Application.Notifications;
using CoinquiLindo.Domain.Enums;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Reports.Commands
{
    public class CastReportVoteCommandHandler(
        IHouseRepository houseRepository,
        IShiftRepository shiftRepository,
        NotificationPublisher notificationPublisher)
    {
        public async Task<ReportResponse> Handle(CastReportVoteCommand request, CancellationToken cancellationToken)
        {
            var (_, shift, voter) = await ReportMapping.GetMemberShiftAsync(houseRepository, shiftRepository,
                request.HouseId, request.ShiftId, request.RequesterUserId, cancellationToken);
            // Only a tenant's first vote notifies the reporter: changing it is not news
            var isFirstVote = shift.Reports.FirstOrDefault(r => r.Id == request.ReportId)?.VoteOf(voter.Id) is null;

            // Undefined numeric values pass through unchanged and are rejected by the domain
            shift.VoteReport(request.ReportId, voter, (VoteType)(int)request.Vote);
            await shiftRepository.UpdateAsync(shift, cancellationToken);

            var report = shift.GetReport(request.ReportId);
            if (isFirstVote)
            {
                await notificationPublisher.ReportVotedAsync(shift, report, cancellationToken);
            }

            return report.ToResponse(shift, voter.Id);
        }
    }
}
