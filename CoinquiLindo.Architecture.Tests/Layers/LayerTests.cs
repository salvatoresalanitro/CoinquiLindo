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

        [Fact]
        public void ApplicationLayer_ShouldNot_DependOn_MessagingFrameworks()
        {
            var result = Types.InAssembly(typeof(Application.Houses.Commands.CreateHouseCommand).Assembly)
                .ShouldNot()
                .HaveDependencyOnAny("MediatR", "Wolverine")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                "handlers must be plain classes discovered by Wolverine conventions. Offending types: {0}",
                string.Join(", ", result.FailingTypeNames ?? []));
        }

        [Fact]
        public void Handlers_Should_FollowWolverineConvention()
        {
            var handlers = Types.InAssembly(typeof(Application.Houses.Commands.CreateHouseCommand).Assembly)
                .That()
                .HaveNameEndingWith("Handler")
                .GetTypes()
                .ToList();

            handlers.Should().NotBeEmpty();
            handlers.Should().AllSatisfy(h =>
            {
                h.IsPublic.Should().BeTrue($"{h.Name} must be public");
                h.GetMethod("Handle").Should().NotBeNull($"{h.Name} must expose a public Handle method");
            });
        }

        [Fact]
        public void ApiLayer_ShouldNot_DependOn_DbContextOrRepositories()
        {
            var result = Types.InAssembly(typeof(Api.ErrorHandling.GlobalExceptionHandler).Assembly)
                .ShouldNot()
                .HaveDependencyOnAny("CoinquiLindo.Infrastructure.AppDbContext", "CoinquiLindo.Infrastructure.Repositories")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                "endpoints must only translate HTTP to Wolverine messages. Offending types: {0}",
                string.Join(", ", result.FailingTypeNames ?? []));
        }

        [Fact]
        public void ApplicationLayer_ShouldNot_DependOn_TokenOrWebFrameworks()
        {
            var result = Types.InAssembly(typeof(Application.Houses.Commands.CreateHouseCommand).Assembly)
                .ShouldNot()
                .HaveDependencyOnAny("Microsoft.IdentityModel", "System.IdentityModel", "Microsoft.AspNetCore")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                "token issuing must stay behind IAuthTokenService. Offending types: {0}",
                string.Join(", ", result.FailingTypeNames ?? []));
        }

        [Fact]
        public void DomainExceptions_Should_InheritFromDomainException()
        {
            var result = Types.InAssembly(typeof(Domain.Entities.House).Assembly)
                .That()
                .ResideInNamespace("CoinquiLindo.Domain.Exceptions")
                .And()
                .DoNotHaveName(nameof(Domain.Exceptions.DomainException))
                .Should()
                .Inherit(typeof(Domain.Exceptions.DomainException))
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                "every domain error must be mapped by the global exception handler. Offending types: {0}",
                string.Join(", ", result.FailingTypeNames ?? []));
        }
    }
}
