using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
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

        [Theory]
        [InlineData("Cucina")]
        [InlineData("cucina")]
        [InlineData("  CUCINA  ")]
        public void AddRoom_Should_Throw_When_RoomNameAlreadyExists(string duplicateName)
        {
            //Arrange
            var house = new House("Casa studenti");
            house.AddRoom(new Room("Cucina"));

            //Act
            var action = () => house.AddRoom(new Room(duplicateName));

            //Assert
            action.Should().Throw<RoomNameAlreadyExistsException>()
                .Which.Code.Should().Be("House.RoomNameAlreadyExists");
            house.Rooms.Should().ContainSingle();
        }
    }
}
