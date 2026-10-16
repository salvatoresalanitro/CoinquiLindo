using NetArchTest.Rules;
using FluentAssertions;

namespace CoinquiLindo.Architecture.Tests.Layers
{
    public class LayerTests
    {
        private const string ApplicationNamespace = "CoinquiLindo.Application";
        private const string InfrastructureNamespace = "CoinquiLindo.Infrastructure";
        private const string ApiNamespace = "CoinquiLindo.Api";

        [Fact]
        public void DomainLayer_ShouldNot_HaveDependenciesOnOtherLayers()
        {
            var result = Types.InAssembly(typeof(Domain.Entities.House).Assembly)
                .ShouldNot()
                .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
                .GetResult();

            result.IsSuccessful.Should().BeTrue();
        }

        [Fact]
        public void ApplicationLayer_ShouldNot_HaveDependenciesOn_InfrastructureOrApi()
        {
            var result = Types.InAssembly(typeof(Application.Houses.Commands.CreateHouseCommand).Assembly)
                .ShouldNot()
                .HaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
                .GetResult();

            result.IsSuccessful.Should().BeTrue();
        }
    }
}
