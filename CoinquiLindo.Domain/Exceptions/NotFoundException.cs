namespace CoinquiLindo.Domain.Exceptions
{
    public class NotFoundException(string entityName, object key)
        : DomainException($"{entityName}.NotFound", $"{entityName} with ID '{key}' was not found.")
    {
        public string EntityName { get; } = entityName;
        public object Key { get; } = key;
    }
}
