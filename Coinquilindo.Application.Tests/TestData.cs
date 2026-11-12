using CoinquiLindo.Domain.Entities;

namespace Coinquilindo.Application.Tests
{
    internal static class TestData
    {
        public static User NewUser(string name = "Salvo") =>
            new($"google-{Guid.NewGuid()}", $"{name.ToLowerInvariant()}@gmail.com", name);

        public static (House House, User Member) HouseWithMember(string houseName = "Casa studenti", string? timeZoneId = null)
        {
            var house = new House(houseName, timeZoneId);
            var member = NewUser();
            house.AddMember(member);

            return (house, member);
        }
    }
}
