namespace CoinquiLindo.Domain.Exceptions
{
    public abstract class DomainException(string code, string message) : Exception(message)
    {
        // Stable, machine-readable identifier exposed to clients (e.g. "House.NotFound")
        public string Code { get; } = code;
    }
}
