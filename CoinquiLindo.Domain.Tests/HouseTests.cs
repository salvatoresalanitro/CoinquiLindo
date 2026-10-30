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

        [Fact]
        public void Constructor_Should_GenerateInviteCode_FromUnambiguousAlphabet()
        {
            var codes = Enumerable.Range(0, 200).Select(_ => new House("Casa").InviteCode).ToList();

            codes.Should().OnlyHaveUniqueItems();
            codes.Should().AllSatisfy(c => c.Should().MatchRegex("^[ABCDEFGHJKMNPQRSTUVWXYZ23456789]{8}$"));
        }

        [Fact]
        public void RegenerateInviteCode_Should_ReplaceTheCurrentCode()
        {
            var house = new House("Casa studenti");
            var oldCode = house.InviteCode;

            var newCode = house.RegenerateInviteCode();

            newCode.Should().Be(house.InviteCode);
            newCode.Should().NotBe(oldCode);
        }

        [Theory]
        [InlineData("ABCDEFGH", "ABCDEFGH")]
        [InlineData("abcd-efgh", "ABCDEFGH")]
        [InlineData("  ABCD EFGH ", "ABCDEFGH")]
        [InlineData("23456789", "23456789")]
        [InlineData("ABCDEFG", null)]
        [InlineData("ABCDEFGHJ", null)]
        [InlineData("ABCDEFG0", null)]
        [InlineData("ABCDEFGI", null)]
        [InlineData("ABCD_EFG", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void NormalizeInviteCode_Should_ReturnCanonicalCode_OrNullWhenInvalid(string? input, string? expected)
        {
            House.NormalizeInviteCode(input).Should().Be(expected);
        }

        [Fact]
        public void ClaimTenant_Should_LinkUser_KeepingTenantIdAndName()
        {
            //Arrange
            var house = new House("Casa studenti");
            var legacy = new Tenant("Luca");
            house.AddTenant(legacy);
            var user = new User("google-sub-2", "luca.rossi@gmail.com", "Luca Rossi");

            //Act
            var claimed = house.ClaimTenant(legacy.Id, user);

            //Assert
            claimed.Should().BeSameAs(legacy);
            claimed.Id.Should().Be(legacy.Id);
            claimed.Name.Should().Be("Luca");
            claimed.UserId.Should().Be(user.Id);
            house.Tenants.Should().ContainSingle();
            house.IsMember(user.Id).Should().BeTrue();
        }

        [Fact]
        public void ClaimTenant_Should_Throw_When_TenantIsAlreadyClaimed()
        {
            //Arrange
            var house = new House("Casa studenti");
            var owner = new User("google-sub-1", "salvo@gmail.com", "Salvo");
            var claimedTenant = house.AddMember(owner);
            var intruder = new User("google-sub-2", "other@gmail.com", "Other");

            //Act
            var action = () => house.ClaimTenant(claimedTenant.Id, intruder);

            //Assert
            action.Should().Throw<TenantAlreadyClaimedException>()
                .Which.Code.Should().Be("Tenant.AlreadyClaimed");
            claimedTenant.UserId.Should().Be(owner.Id);
        }

        [Fact]
        public void ClaimTenant_Should_ThrowNotFound_When_TenantIsNotInHouse()
        {
            var house = new House("Casa studenti");
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");

            var action = () => house.ClaimTenant(Guid.NewGuid(), user);

            action.Should().Throw<NotFoundException>()
                .Which.Code.Should().Be("Tenant.NotFound");
        }

        [Fact]
        public void ClaimTenant_Should_Throw_When_UserIsAlreadyMember()
        {
            //Arrange
            var house = new House("Casa studenti");
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");
            house.AddMember(user);
            var legacy = new Tenant("Luca");
            house.AddTenant(legacy);

            //Act
            var action = () => house.ClaimTenant(legacy.Id, user);

            //Assert
            action.Should().Throw<UserAlreadyMemberException>();
            legacy.UserId.Should().BeNull();
        }
    }
}
