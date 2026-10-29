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

        [Fact]
        public void AddMember_Should_AddTenantLinkedToUser()
        {
            //Arrange
            var house = new House("Casa studenti");
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");

            //Act
            var tenant = house.AddMember(user);

            //Assert
            house.Tenants.Should().ContainSingle().Which.Should().Be(tenant);
            tenant.UserId.Should().Be(user.Id);
            tenant.Name.Should().Be("Salvo");
            house.IsMember(user.Id).Should().BeTrue();
        }

        [Fact]
        public void AddMember_Should_Throw_When_UserIsAlreadyMember()
        {
            //Arrange
            var house = new House("Casa studenti");
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");
            house.AddMember(user);

            //Act
            var action = () => house.AddMember(user);

            //Assert
            action.Should().Throw<UserAlreadyMemberException>()
                .Which.Code.Should().Be("House.UserAlreadyMember");
            house.Tenants.Should().ContainSingle();
        }

        [Fact]
        public void IsMember_Should_BeFalse_ForUnknownUser_And_TenantsWithoutAccount()
        {
            //Arrange
            var house = new House("Casa studenti");
            house.AddTenant(new Tenant("Ospite senza account"));

            //Act & Assert
            house.IsMember(Guid.NewGuid()).Should().BeFalse();
            house.Tenants.Single().UserId.Should().BeNull();
        }

        [Fact]
        public void EnsureIsMember_Should_Throw_When_UserIsNotMember()
        {
            //Arrange
            var house = new House("Casa studenti");

            //Act
            var action = () => house.EnsureIsMember(Guid.NewGuid());

            //Assert
            action.Should().Throw<NotHouseMemberException>()
                .Which.Code.Should().Be("House.AccessDenied");
        }

        [Fact]
        public void EnsureIsMember_Should_NotThrow_When_UserIsMember()
        {
            //Arrange
            var house = new House("Casa studenti");
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");
            house.AddMember(user);

            //Act
            var action = () => house.EnsureIsMember(user.Id);

            //Assert
            action.Should().NotThrow();
        }
    }
}
