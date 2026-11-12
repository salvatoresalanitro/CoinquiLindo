using System.Security.Cryptography;
using CoinquiLindo.Domain.Exceptions;

namespace CoinquiLindo.Domain.Entities
{
    public class House
    {
        // No 0/O, 1/I/L: codes are read aloud and typed by hand
        private const string InviteCodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        public const int InviteCodeLength = 8;
        public const string DefaultTimeZoneId = "Europe/Rome";
        public const int MaxTimeZoneIdLength = 64;

        public Guid Id { get; private set; }
        public string Name { get; private set; }

        // Shared with flatmates to join the house; regenerating it revokes the previous one
        public string InviteCode { get; private set; }

        // add backfield to protect the tenants list from being modified directly
        private readonly List<Tenant> _tenants = new();
        private readonly List<Room> _rooms = new();

        // IReadOnlyCollection avoid the possibility of modifying the list from outside the class
        public IReadOnlyCollection<Tenant> Tenants => _tenants.AsReadOnly();
        public IReadOnlyCollection<Room> Rooms => _rooms.AsReadOnly();

        // IANA id: shift days and deadlines are interpreted in the time zone where the house is
        public string TimeZoneId { get; private set; }

        public House(string name, string? timeZoneId = null)
        {
            if(string.IsNullOrWhiteSpace(name))
            {
                throw new DomainValidationException("House.NameRequired", "House name cannot be empty.");
            }

            Id = Guid.NewGuid();
            Name = name;
            InviteCode = GenerateInviteCode();
            TimeZoneId = NormalizeTimeZoneId(timeZoneId);
        }

        public TimeZoneInfo GetTimeZone() => TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

        // Blank -> default. Windows ids are stored as IANA so the value resolves on every server OS
        private static string NormalizeTimeZoneId(string? timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId))
            {
                return DefaultTimeZoneId;
            }

            var id = timeZoneId.Trim();
            if (id.Length > MaxTimeZoneIdLength || !TimeZoneInfo.TryFindSystemTimeZoneById(id, out var timeZone))
            {
                throw new DomainValidationException("House.TimeZoneInvalid", "The time zone of the house is not valid.");
            }

            if (timeZone.HasIanaId)
            {
                return id;
            }

            return TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var ianaId)
                ? ianaId
                : throw new DomainValidationException("House.TimeZoneInvalid", "The time zone of the house is not valid.");
        }

        public string RegenerateInviteCode()
        {
            string newCode;
            do
            {
                newCode = GenerateInviteCode();
            }
            while (newCode == InviteCode);

            InviteCode = newCode;
            return newCode;
        }

        // Accepts what users type ("abcd-efgh", " ABCD EFGH "); null when it cannot be a valid code
        public static string? NormalizeInviteCode(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            var normalized = new string(input.Where(c => c != '-' && !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

            return normalized.Length == InviteCodeLength && normalized.All(InviteCodeAlphabet.Contains)
                ? normalized
                : null;
        }

        private static string GenerateInviteCode() =>
            RandomNumberGenerator.GetString(InviteCodeAlphabet, InviteCodeLength);

        // Legacy: tenants without an account (no longer exposed by the API, can be claimed via ClaimTenant)
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

        public Tenant ClaimTenant(Guid tenantId, User user)
        {
            ArgumentNullException.ThrowIfNull(user, nameof(user));

            if (IsMember(user.Id))
            {
                throw new UserAlreadyMemberException();
            }

            var tenant = _tenants.FirstOrDefault(t => t.Id == tenantId)
                ?? throw new NotFoundException(nameof(Tenant), tenantId);

            if (tenant.UserId is not null)
            {
                throw new TenantAlreadyClaimedException();
            }

            tenant.LinkToUser(user.Id);

            return tenant;
        }

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

#pragma warning disable CS8618 // It's needed for EF Core to work properly, but we don't want to allow it to be used outside of EF Core.
        private House() { }
#pragma warning restore CS8618
    }
}
