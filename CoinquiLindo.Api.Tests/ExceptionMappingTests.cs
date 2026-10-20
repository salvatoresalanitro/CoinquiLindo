using System.Net;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CoinquiLindo.Api.Tests
{
    public class ExceptionMappingTests(CoinquiLindoApiFactory factory) : IClassFixture<CoinquiLindoApiFactory>
    {
        public static TheoryData<Exception, HttpStatusCode, string> MappedExceptions => new()
        {
            { new DomainValidationException("Test.Invalid", "Invalid value."), HttpStatusCode.BadRequest, "Test.Invalid" },
            { new NotFoundException("House", Guid.NewGuid()), HttpStatusCode.NotFound, "House.NotFound" },
            { new RoomNameAlreadyExistsException("Cucina"), HttpStatusCode.Conflict, "House.RoomNameAlreadyExists" },
            { new SelfReportNotAllowedException(), HttpStatusCode.UnprocessableEntity, "Shift.SelfReportNotAllowed" },
        };

        [Theory]
        [MemberData(nameof(MappedExceptions))]
        public async Task DomainExceptions_Should_BeMappedToProblemDetails(Exception exception, HttpStatusCode expectedStatus, string expectedCode)
        {
            var client = CreateClientThrowing(exception);

            var response = await client.GetAsync($"/api/houses/{Guid.NewGuid()}");

            var problem = await response.ReadProblemAsync(expectedStatus);
            problem.Status.Should().Be((int)expectedStatus);
            problem.ErrorCode.Should().Be(expectedCode);
            problem.Detail.Should().Be(exception.Message);
            problem.TraceId.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task UnexpectedException_Should_Return500_WithoutLeakingDetails()
        {
            var client = CreateClientThrowing(new InvalidOperationException("Sensitive: Data Source=secret.db"));

            var response = await client.GetAsync($"/api/houses/{Guid.NewGuid()}");

            var problem = await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
            problem.ErrorCode.Should().Be("General.Unexpected");
            problem.TraceId.Should().NotBeNullOrWhiteSpace();
            problem.RawBody.Should().NotContain("Sensitive").And.NotContain("secret.db").And.NotContain("InvalidOperationException");
        }

        private HttpClient CreateClientThrowing(Exception exception) =>
            factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(new ExceptionToThrow(exception));
                services.AddScoped<IHouseRepository, ThrowingHouseRepository>();
            })).CreateClient();

        public sealed record ExceptionToThrow(Exception Exception);

        // Concrete type (no lambda factory) so Wolverine can build it without service location
        public sealed class ThrowingHouseRepository(ExceptionToThrow toThrow) : IHouseRepository
        {
            public Task AddAsync(House house, CancellationToken cancellationToken = default) => throw toThrow.Exception;
            public Task<House?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw toThrow.Exception;
            public Task UpdateAsync(House house, CancellationToken cancellationToken = default) => throw toThrow.Exception;
        }
    }
}
