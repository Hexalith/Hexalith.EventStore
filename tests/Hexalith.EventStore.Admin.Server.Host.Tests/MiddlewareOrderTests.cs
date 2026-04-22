using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Host.Middleware;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Host.Tests;

public class MiddlewareOrderTests : IClassFixture<MiddlewareOrderTests.AuthTestFactory>, IDisposable {
    private readonly AuthTestFactory _factory;
    private readonly HttpClient _client;

    public MiddlewareOrderTests(AuthTestFactory factory) {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Unauthenticated_Request_Returns401() {
        // No auth header → 401
        HttpResponseMessage response = await _client.GetAsync("/api/v1/admin/streams");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Contains(CorrelationIdMiddleware.HeaderName).ShouldBeTrue();
        Guid.TryParse(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single(), out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Authenticated_AdminRole_Returns200() {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(
            new Claim("sub", "admin-user"),
            new Claim("global_admin", "true")));

        HttpResponseMessage response = await _client.GetAsync("/api/v1/admin/streams");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public void Dispose() {
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Factory that replaces the JWT auth with a test scheme for middleware verification.
    /// </summary>
    public class AuthTestFactory : WebApplicationFactory<Program> {
        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            ArgumentNullException.ThrowIfNull(builder);
            _ = builder.ConfigureServices(services => {
                // Replace DaprClient with mock
                ServiceDescriptor? daprDescriptor = services
                    .FirstOrDefault(d => d.ServiceType == typeof(DaprClient));
                if (daprDescriptor is not null) {
                    _ = services.Remove(daprDescriptor);
                }

                _ = services.AddSingleton(Substitute.For<DaprClient>());

                // Override DAPR-backed services with mocks
                _ = services.AddScoped(sp => {
                    IStreamQueryService service = Substitute.For<IStreamQueryService>();
                    _ = service.GetRecentlyActiveStreamsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                        .Returns(new PagedResult<StreamSummary>(
                        [
                            new StreamSummary(
                                "test-tenant",
                                "test-domain",
                                "middleware-test-aggregate",
                                7,
                                DateTimeOffset.Parse("2026-03-21T12:00:00+00:00"),
                                7,
                                false,
                                StreamStatus.Active),
                        ],
                        1,
                        null));

                    return service;
                });
                _ = services.AddScoped(_ => Substitute.For<IProjectionQueryService>());
                _ = services.AddScoped(_ => Substitute.For<IProjectionCommandService>());
                _ = services.AddScoped(_ => Substitute.For<ITypeCatalogService>());
                _ = services.AddScoped(_ => Substitute.For<IHealthQueryService>());
                _ = services.AddScoped(_ => Substitute.For<IStorageQueryService>());
                _ = services.AddScoped(_ => Substitute.For<IStorageCommandService>());
                _ = services.AddScoped(_ => Substitute.For<IDeadLetterQueryService>());
                _ = services.AddScoped(_ => Substitute.For<IDeadLetterCommandService>());
                _ = services.AddScoped(_ => Substitute.For<ITenantQueryService>());
            });
        }
    }

    private static string CreateToken(params Claim[] claims) {
        var descriptor = new SecurityTokenDescriptor {
            Issuer = "hexalith-dev",
            Audience = "hexalith-eventstore",
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(30),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("DevOnlySigningKey-AtLeast32Chars!")),
                SecurityAlgorithms.HmacSha256Signature),
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
