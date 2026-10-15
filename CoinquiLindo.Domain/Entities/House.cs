namespace CoinquiLindo.Domain.Entities
{
    public class House
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }

        // add backfield to protect the tenants list from being modified directly
        private readonly List<Tenant> _tenants = new();

        // IReadOnlyCollection avoid the possibility of modifying the list from outside the class
        public IReadOnlyCollection<Tenant> Tenants => _tenants.AsReadOnly();

        public House(string name)
        {
            if(string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("House name cannot be null or empty.", nameof(name));
            }

            Id = Guid.NewGuid();
            Name = name;
        }

        public void AddTenant(Tenant tenant)
        {
            ArgumentNullException.ThrowIfNull(tenant);

            _tenants.Add(tenant);
        }
    }
}
