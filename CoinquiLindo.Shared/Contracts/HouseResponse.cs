namespace CoinquiLindo.Shared.Contracts
{
    public record TenantResponse(Guid Id, string Name);
    public record HouseResponse(Guid Id, string Name, List<TenantResponse> Tenants);
}
