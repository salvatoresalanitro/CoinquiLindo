namespace CoinquiLindo.Application.Rotation.Commands
{
    public record DeleteRotationPlanCommand(Guid HouseId, Guid RequesterUserId);
}
