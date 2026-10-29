using CoinquiLindo.Domain.Exceptions;

namespace CoinquiLindo.Domain.Entities
{
    public class Tenant
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }

        // Null for tenants added by name without an account (legacy flow, until invitations are available)
        public Guid? UserId { get; private set; }

        public Tenant(string name)
        {
            if(string.IsNullOrWhiteSpace(name))
            {
                throw new DomainValidationException("Tenant.NameRequired", "Tenant name cannot be empty.");
            }

            Id = Guid.NewGuid();
            Name = name;
        }

        internal Tenant(User user) : this(user.DisplayName)
        {
            UserId = user.Id;
        }

#pragma warning disable CS8618 // It's needed for EF Core to work properly, but we don't want to allow it to be used outside of EF Core.
        private Tenant() { }
#pragma warning restore CS8618
    }
}
