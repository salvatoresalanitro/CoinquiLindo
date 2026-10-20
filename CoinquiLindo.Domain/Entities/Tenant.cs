using CoinquiLindo.Domain.Exceptions;

namespace CoinquiLindo.Domain.Entities
{
    public class Tenant
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }

        public Tenant(string name)
        {
            if(string.IsNullOrWhiteSpace(name))
            {
                throw new DomainValidationException("Tenant.NameRequired", "Tenant name cannot be empty.");
            }

            Id = Guid.NewGuid();
            Name = name;
        }
    }
}