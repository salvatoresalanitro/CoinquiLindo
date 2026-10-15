using CoinquiLindo.Domain.Entities;
using FluentAssertions;

namespace CoinquiLindo.Domain.Tests
{
    public class HouseTests
    {
        [Fact]
        public void AddTenant_Should_AddUserToTheHouse_WhenTenantIsValid()
        {
            //Arrange
            var house = new House("Casa studenti");
            var tenant = new Tenant("Salvo");

            //Act
            house.AddTenant(tenant);

            //Assert
            house.Tenants.Should().ContainSingle();
            house.Tenants.First().Name.Should().Be("Salvo");
        }

        [Fact]
        public void AddRoom_Should_AddRoomToTheHouse_WhenRoomIsValid()
        {
            //Arrange
            var house = new House("Casa studenti");
            var room = new Room("Cucina");

            //Act
            house.AddRoom(room);

            //Assert
            house.Rooms.Should().ContainSingle();
            house.Rooms.First().Name.Should().Be("Cucina");
        }
    }
}
