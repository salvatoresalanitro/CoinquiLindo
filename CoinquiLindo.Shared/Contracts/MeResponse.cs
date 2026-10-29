namespace CoinquiLindo.Shared.Contracts
{
    public record MyHouseResponse(Guid Id, string Name);

    public record MeResponse(Guid Id, string DisplayName, string Email, List<MyHouseResponse> Houses);
}
