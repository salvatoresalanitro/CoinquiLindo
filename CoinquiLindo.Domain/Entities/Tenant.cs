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
                throw new ArgumentException("Tenant name cannot be empty.", nameof(name));
            }

            Id = Guid.NewGuid();
            Name = name;
        }
    }
}