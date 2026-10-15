namespace CoinquiLindo.Domain.Tests
{
    public class HouseTests
    {
        [Fact]
        public void AddTenant_Should_AddUserToTheHouse_WhenTenantIsValid()
        {
            //Arrange
            var house = new House("Via Sondrio 13/20");
            var tenant = new Tenant("Salvo");

            //Act
            house.AddTenant(tenant);

            //Assert
            house.Tenants.Should().ContainSingle();
            house.Tenants.First.Name.Should().Be("Salvo");
        }
    }
}
