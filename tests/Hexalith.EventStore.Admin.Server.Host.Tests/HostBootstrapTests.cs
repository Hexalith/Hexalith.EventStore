using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Host.Authentication;
using Hexalith.EventStore.Admin.Server.Host.Middleware;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Host.Tests;

public class HostBootstrapTests : IClassFixture<HostBootstrapTests.AdminServerHostFactory> {
    private readonly AdminServerHostFactory _factory;

    public HostBootstrapTests(AdminServerHostFactory factory) => _factory = factory;

    [Fact]
    public void WebApplicationFactory_Builds_Successfully() {
        // The factory fixture builds the host — if this test runs, the host started.
        using HttpClient client = _factory.CreateClient();
        _ = client.ShouldNotBeNull();
    }

    [Fact]
    public async Task AddAdminApi_Registers_AuthorizationPolicies() {
        using IServiceScope scope = _factory.Services.CreateScope();
        IAuthorizationPolicyProvider policyProvider = scope.ServiceProvider
            .GetRequiredService<IAuthorizationPolicyProvider>();

        _ = (await policyProvider.GetPolicyAsync(AdminAuthorizationPolicies.ReadOnly)).ShouldNotBeNull();
        _ = (await policyProvider.GetPolicyAsync(AdminAuthorizationPolicies.Operator)).ShouldNotBeNull();
        _ = (await policyProvider.GetPolicyAsync(AdminAuthorizationPolicies.Admin)).ShouldNotBeNull();
    }

    [Fact]
    public void AddAdminApi_Registers_ClaimsTransformation() {
        using IServiceScope scope = _factory.Services.CreateScope();
        IClaimsTransformation transformation = scope.ServiceProvider
            .GetRequiredService<IClaimsTransformation>();

        _ = transformation.ShouldBeOfType<AdminClaimsTransformation>();
    }

    [Fact]
    public void AddAdminApi_Registers_TenantAuthorizationFilter() {
        using IServiceScope scope = _factory.Services.CreateScope();
        AdminTenantAuthorizationFilter filter = scope.ServiceProvider
            .GetRequiredService<AdminTenantAuthorizationFilter>();

        _ = filter.ShouldNotBeNull();
    }

    [Fact]
    public void AddAdminApi_Registers_HttpContextAdminAuthContext() {
        using IServiceScope scope = _factory.Services.CreateScope();
        IAdminAuthContext authContext = scope.ServiceProvider
            .GetRequiredService<IAdminAuthContext>();

        _ = authContext.ShouldBeOfType<HttpContextAdminAuthContext>();
    }

    [Fact]
    public void AddAdminApi_Registers_AllServiceImplementations() {
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;

        _ = sp.GetService<IStreamQueryService>().ShouldNotBeNull();
        _ = sp.GetService<IProjectionQueryService>().ShouldNotBeNull();
        _ = sp.GetService<IProjectionCommandService>().ShouldNotBeNull();
        _ = sp.GetService<ITypeCatalogService>().ShouldNotBeNull();
        _ = sp.GetService<IHealthQueryService>().ShouldNotBeNull();
        _ = sp.GetService<IStorageQueryService>().ShouldNotBeNull();
        _ = sp.GetService<IStorageCommandService>().ShouldNotBeNull();
        _ = sp.GetService<IDeadLetterQueryService>().ShouldNotBeNull();
        _ = sp.GetService<IDeadLetterCommandService>().ShouldNotBeNull();
        _ = sp.GetService<ITenantQueryService>().ShouldNotBeNull();
    }

    [Fact]
    public void JwtBearerOptions_Are_Bound_From_DevelopmentConfiguration() {
        using IServiceScope scope = _factory.Services.CreateScope();
        JwtBearerOptions options = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        AdminServerAuthenticationOptions authOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<AdminServerAuthenticationOptions>>()
            .Value;

        authOptions.Issuer.ShouldBe("hexalith-dev");
        authOptions.Audience.ShouldBe("hexalith-eventstore");
        authOptions.SigningKey.ShouldBe("DevOnlySigningKey-AtLeast32Chars!");
        options.TokenValidationParameters.ValidIssuer.ShouldBe("hexalith-dev");
        options.TokenValidationParameters.ValidAudience.ShouldBe("hexalith-eventstore");
        options.RequireHttpsMetadata.ShouldBeFalse();
        _ = options.TokenValidationParameters.IssuerSigningKey.ShouldNotBeNull();
    }

    [Fact]
    public async Task AddControllers_Discovers_AdminControllers() {
        using HttpClient client = _factory.CreateClient();

        // If controllers are not discovered, any endpoint returns 404.
        // A valid admin controller route should NOT return 404.
        // (It will return 401 because no auth token is sent — but not 404.)
        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/streams");
        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Authenticated_Admin_Request_ReturnsExpectedStreamPayload() {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(
            new Claim("sub", "admin-user"),
            new Claim("global_admin", "true")));

        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/streams?tenantId=test-tenant");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        string payload = await response.Content.ReadAsStringAsync();
        payload.ShouldContain("test-aggregate");
        payload.ShouldContain("test-tenant");
    }

    [Fact]
    public async Task Authenticated_Request_ForUnauthorizedTenant_Returns403() {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(
            new Claim("sub", "readonly-user"),
            new Claim(AdminClaimTypes.Tenant, "allowed-tenant")));

        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/streams?tenantId=blocked-tenant");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HealthEndpoint_Returns200() {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains(CorrelationIdMiddleware.HeaderName).ShouldBeTrue();
    }

    [Fact]
    public async Task AliveEndpoint_Returns200() {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/alive");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadyEndpoint_Returns200() {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
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

    /// <summary>
    /// Custom WebApplicationFactory that replaces DaprClient with a mock
    /// and uses a test authentication scheme so the host boots without DAPR sidecar.
    /// </summary>
    public class AdminServerHostFactory : WebApplicationFactory<Program> {
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

                // Override DAPR-backed services with mocks so controller routes are reachable
                _ = services.AddScoped(_ => {
                    IStreamQueryService service = Substitute.For<IStreamQueryService>();
                    _ = (IServiceProvider)service.GetRecentlyActiveStreamsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                        .Returns(callInfo => new PagedResult<StreamSummary>(
                        [
                            new StreamSummary(
                                callInfo.ArgAt<string?>(0) ?? "test-tenant",
                                callInfo.ArgAt<string?>(1) ?? "test-domain",
                                "test-aggregate",
                                42,
                                DateTimeOffset.Parse("2026-03-21T12:00:00+00:00"),
                                42,
                                true,
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
}
