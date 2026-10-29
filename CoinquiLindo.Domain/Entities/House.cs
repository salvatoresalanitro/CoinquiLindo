using CoinquiLindo.Domain.Exceptions;

namespace CoinquiLindo.Domain.Entities
{
    public class House
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }

        // add backfield to protect the tenants list from being modified directly
        private readonly List<Tenant> _tenants = new();
        private readonly List<Room> _rooms = new();

        // IReadOnlyCollection avoid the possibility of modifying the list from outside the class
        public IReadOnlyCollection<Tenant> Tenants => _tenants.AsReadOnly();
        public IReadOnlyCollection<Room> Rooms => _rooms.AsReadOnly();

        public House(string name)
        {
            if(string.IsNullOrWhiteSpace(name))
            {
                throw new DomainValidationException("House.NameRequired", "House name cannot be empty.");
            }

            Id = Guid.NewGuid();
            Name = name;
        }

        public void AddTenant(Tenant tenant)
        {
            ArgumentNullException.ThrowIfNull(tenant, nameof(tenant));

            _tenants.Add(tenant);
        }

        public Tenant AddMember(User user)
        {
            ArgumentNullException.ThrowIfNull(user, nameof(user));

            if (IsMember(user.Id))
            {
                throw new UserAlreadyMemberException();
            }

            var tenant = new Tenant(user);
            _tenants.Add(tenant);

            return tenant;
        }

        public bool IsMember(Guid userId) => _tenants.Any(t => t.UserId == userId);

        public void EnsureIsMember(Guid userId)
        {
            if (!IsMember(userId))
            {
                throw new NotHouseMemberException();
            }
        }

        public void AddRoom(Room room)
        {
            ArgumentNullException.ThrowIfNull(room, nameof(room));

            var name = room.Name.Trim();
            if (_rooms.Any(r => string.Equals(r.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new RoomNameAlreadyExistsException(name);
            }

            _rooms.Add(room);
        }
    }
}
