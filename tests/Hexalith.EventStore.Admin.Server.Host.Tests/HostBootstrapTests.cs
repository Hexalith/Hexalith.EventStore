using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
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
        authOptions.SigningKey.ShouldBe(AuthenticationTestEnvironment.SigningKey);
        options.TokenValidationParameters.ValidIssuer.ShouldBe("hexalith-dev");
        options.TokenValidationParameters.ValidAudiences.ShouldBe(["hexalith-eventstore"]);
        options.RequireHttpsMetadata.ShouldBeFalse();
        _ = options.TokenValidationParameters.IssuerSigningKey.ShouldNotBeNull();
    }

    [Fact]
    public async Task AddControllers_Discovers_AdminControllers() {
        using HttpClient client = _factory.CreateClient();

        // If controllers are not discovered, any endpoint returns 404.
        // A valid admin controller route should NOT return 404.
        // (It will return 401 because no auth token is sent — but not 404.)
        // Route renamed in 48d5126e: the stream list endpoint is now
        // /api/v1/admin/streams/GetRecentlyActiveStreams (was bare /api/v1/admin/streams).
        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/streams/GetRecentlyActiveStreams");
        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Authenticated_Admin_Request_ReturnsExpectedStreamPayload() {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(
            new Claim("sub", "admin-user"),
            new Claim("global_admin", "true")));

        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/streams/GetRecentlyActiveStreams?tenantId=test-tenant");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        string payload = await response.Content.ReadAsStringAsync();
        payload.ShouldContain("test-aggregate");
        payload.ShouldContain("test-tenant");
    }

    [Fact]
    public async Task AdminRequest_WithHs384Token_ReturnsUnauthorized() {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateTokenWithAlgorithm(
            SecurityAlgorithms.HmacSha384Signature,
            new Claim("sub", "admin-user"),
            new Claim("global_admin", "true")));

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/admin/streams/GetRecentlyActiveStreams?tenantId=test-tenant");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminRequest_WithInvalidValidationDimension_ReturnsUnauthorized() {
        Claim[] claims =
        [
            new Claim("sub", "admin-user"),
            new Claim("global_admin", "true"),
        ];
        string signingKey = Convert.ToBase64String(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        (string Scenario, string Token)[] invalidTokens =
        [
            ("unsigned", CreateTokenForValidation(claims, signed: false)),
            ("expired", CreateTokenForValidation(claims, expires: DateTime.UtcNow.AddMinutes(-10))),
            ("wrong issuer", CreateTokenForValidation(claims, issuer: "unexpected-issuer")),
            ("wrong audience", CreateTokenForValidation(claims, audience: "unexpected-audience")),
            ("wrong signing key", CreateTokenForValidation(
                claims,
                "hexalith-dev",
                "hexalith-eventstore",
                expires: null,
                signingKey)),
        ];

        using HttpClient client = _factory.CreateClient();
        foreach ((string scenario, string token) in invalidTokens) {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "/api/v1/admin/streams/GetRecentlyActiveStreams?tenantId=test-tenant");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage response = await client.SendAsync(request);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, scenario);
        }
    }

    [Fact]
    public async Task AdminHttpPipeline_InProductionAuthorityMode_EnforcesEveryTokenValidationDimension() {
        const string authorityIssuer = "https://identity.example.test/realms/hexalith";
        const string audience = "hexalith-eventstore";
        const string additionalAudience = "hexalith-eventstore-admin";
        using RSA rsa = RSA.Create(2048);
        using RSA wrongRsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa) { KeyId = Guid.NewGuid().ToString("N") };
        var wrongSigningKey = new RsaSecurityKey(wrongRsa) { KeyId = Guid.NewGuid().ToString("N") };
        Claim[] claims =
        [
            new Claim("sub", "authority-mode-admin"),
            new Claim("global_admin", "true"),
        ];

        await using var baseFactory = new AdminServerHostFactory();
        await using WebApplicationFactory<Program> factory = baseFactory.WithWebHostBuilder(builder => {
            _ = builder.UseEnvironment(Environments.Production);
            _ = builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                    ["Authentication:JwtBearer:Authority"] = authorityIssuer,
                    ["Authentication:JwtBearer:Issuer"] = authorityIssuer,
                    ["Authentication:JwtBearer:Audience"] = audience,
                    ["Authentication:JwtBearer:ValidAudiences:0"] = additionalAudience,
                    ["Authentication:JwtBearer:AllowedAlgorithms:0"] = SecurityAlgorithms.RsaSha256,
                    ["Authentication:JwtBearer:SigningKey"] = null,
                    ["Authentication:JwtBearer:RequireHttpsMetadata"] = "true",
                }));
        });
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        ConfigureStaticAuthority(factory.Services, authorityIssuer, signingKey);

        using (var validRequest = CreateAuthorityRequest(
            CreateAuthorityToken(signingKey, authorityIssuer, additionalAudience, claims)))
        using (HttpResponseMessage validResponse = await client.SendAsync(
            validRequest,
            TestContext.Current.CancellationToken)) {
            validResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        string symmetricKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        (string Scenario, string Token)[] invalidTokens =
        [
            ("unsigned", CreateAuthorityToken(signingKey, authorityIssuer, audience, claims, signed: false)),
            ("missing expiry", CreateAuthorityToken(signingKey, authorityIssuer, audience, claims, includeExpiry: false)),
            ("expired", CreateAuthorityToken(
                signingKey,
                authorityIssuer,
                audience,
                claims,
                expires: DateTime.UtcNow.AddMinutes(-10))),
            ("wrong issuer", CreateAuthorityToken(signingKey, "https://unexpected.example.test", audience, claims)),
            ("wrong audience", CreateAuthorityToken(signingKey, authorityIssuer, "unexpected-audience", claims)),
            ("wrong signing key", CreateAuthorityToken(wrongSigningKey, authorityIssuer, audience, claims)),
            ("wrong algorithm family", CreateAuthorityToken(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(symmetricKey)),
                authorityIssuer,
                audience,
                claims,
                SecurityAlgorithms.HmacSha256Signature)),
            ("non-allowlisted algorithm", CreateAuthorityToken(
                signingKey,
                authorityIssuer,
                audience,
                claims,
                SecurityAlgorithms.RsaSha384)),
            ("missing algorithm", CreateAuthorityTokenWithoutAlgorithm(authorityIssuer, audience)),
        ];

        foreach ((string scenario, string token) in invalidTokens) {
            using HttpRequestMessage request = CreateAuthorityRequest(token);
            using HttpResponseMessage response = await client.SendAsync(
                request,
                TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, scenario);
        }
    }

    [Fact]
    public void Host_WithProductionSymmetricOverride_FailsBeforeServingWithoutEchoingKey() {
        string signingKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        using WebApplicationFactory<Program> factory = new AdminServerHostFactory().WithWebHostBuilder(builder => {
            _ = builder.UseEnvironment("Production");
            _ = builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                    ["Authentication:JwtBearer:Authority"] = string.Empty,
                    ["Authentication:JwtBearer:Issuer"] = "test-issuer",
                    ["Authentication:JwtBearer:Audience"] = "test-audience",
                    ["Authentication:JwtBearer:SigningKey"] = signingKey,
                    ["Authentication:JwtBearer:AllowInsecureSymmetricKey"] = "true",
                }));
        });

        OptionsValidationException exception = Should.Throw<OptionsValidationException>(() => factory.CreateClient());

        exception.Message.ShouldContain("forbidden in Production");
        exception.ToString().ShouldNotContain(signingKey);
    }

    [Fact]
    public async Task AnonymousAdminRequest_ReturnsBoundedRedacted401WithoutServiceWork() {
        _factory.StreamService.ClearReceivedCalls();
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            "/api/v1/admin/streams/private-tenant/orders/private-aggregate/state");

        await AssertBoundedProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "private-tenant",
            "private-aggregate");
        _ = await _factory.StreamService.DidNotReceiveWithAnyArgs()
            .GetAggregateStateAtPositionAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task UnknownExactRole_ReturnsBoundedRedacted403WithoutServiceWork() {
        _factory.StreamService.ClearReceivedCalls();
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(
            new Claim("sub", "unknown-role-user"),
            new Claim(AdminClaimTypes.AdminRole, "admin")));

        HttpResponseMessage response = await client.GetAsync(
            "/api/v1/admin/streams/private-tenant/orders/private-aggregate/state");

        await AssertBoundedProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            "private-tenant",
            "private-aggregate");
        _ = await _factory.StreamService.DidNotReceiveWithAnyArgs()
            .GetAggregateStateAtPositionAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task Authenticated_Request_ForUnauthorizedTenant_Returns403() {
        _factory.StreamService.ClearReceivedCalls();
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(
            new Claim("sub", "readonly-user"),
            new Claim(AdminClaimTypes.Tenant, "allowed-tenant")));

        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/streams/GetRecentlyActiveStreams?tenantId=blocked-tenant");

        await AssertBoundedProblemAsync(response, HttpStatusCode.Forbidden, "blocked-tenant", "allowed-tenant");
        _ = await _factory.StreamService.DidNotReceiveWithAnyArgs()
            .GetRecentlyActiveStreamsAsync(default, default, default, default);
    }

    [Theory]
    [InlineData("/api/v1/admin/backups/admissions/blocked-tenant/private-admission", true)]
    [InlineData("/api/v1/admin/backups/crypto-shredding/workflows/blocked-tenant/private-workflow", false)]
    public async Task BackupOpaqueRead_ForUnauthorizedTenant_IsRedactedAndDoesNoWork(
        string route,
        bool admission) {
        _factory.BackupQueryService.ClearReceivedCalls();
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(
            new Claim("sub", "readonly-user"),
            new Claim(AdminClaimTypes.Tenant, "allowed-tenant")));

        HttpResponseMessage response = await client.GetAsync(route);

        await AssertBoundedProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            "blocked-tenant",
            admission ? "private-admission" : "private-workflow");
        if (admission) {
            _ = await _factory.BackupQueryService.DidNotReceiveWithAnyArgs()
                .GetRestoreAdmissionAsync(default!, default!, default);
        }
        else {
            _ = await _factory.BackupQueryService.DidNotReceiveWithAnyArgs()
                .GetCryptoShreddingWorkflowAsync(default!, default!, default);
        }
    }

    [Theory]
    [InlineData("/api/v1/admin/consistency/checks/private-check", "check-result")]
    [InlineData("/api/v1/admin/consistency/checks/private-check/cancel", "check-cancel")]
    [InlineData("/api/v1/admin/dapr/actors/AggregateActor/state?actorId=private-actor", "actor-state")]
    public async Task OperatorOpaqueLookup_ReturnsRedacted403BeforeServiceWork(string route, string operation) {
        ArgumentNullException.ThrowIfNull(operation);
        _factory.ConsistencyQueryService.ClearReceivedCalls();
        _factory.ConsistencyCommandService.ClearReceivedCalls();
        _factory.DaprInfrastructureQueryService.ClearReceivedCalls();
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(
            new Claim("sub", "operator-user"),
            new Claim(AdminClaimTypes.AdminRole, "Operator")));

        using var request = new HttpRequestMessage(
            operation == "check-result" || operation == "actor-state" ? HttpMethod.Get : HttpMethod.Post,
            route);
        HttpResponseMessage response = await client.SendAsync(request);

        await AssertBoundedProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            operation.StartsWith("check", StringComparison.Ordinal) ? "private-check" : "private-actor");
        switch (operation) {
            case "check-result":
                _ = await _factory.ConsistencyQueryService.DidNotReceiveWithAnyArgs()
                    .GetCheckResultAsync(default!, default);
                break;
            case "check-cancel":
                _ = await _factory.ConsistencyCommandService.DidNotReceiveWithAnyArgs()
                    .CancelCheckAsync(default!, default);
                break;
            default:
                _ = await _factory.DaprInfrastructureQueryService.DidNotReceiveWithAnyArgs()
                    .GetActorInstanceStateAsync(default!, default!, default);
                break;
        }
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

    [Fact]
    public async Task ProductionPipeline_LeavesProbesAnonymousAndProtectedAdminRouteChallenged()
    {
        await using var factory = new ProductionAdminServerHostFactory();
        using HttpClient client = factory.CreateClient();

        foreach (string path in new[] { "/health", "/alive", "/ready" })
        {
            using HttpResponseMessage response = await client.GetAsync(path);
            string body = await response.Content.ReadAsStringAsync();

            response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
            body.Length.ShouldBeLessThan(64);
            body.ShouldNotContain("results", Case.Insensitive);
        }

        using HttpResponseMessage protectedResponse = await client.GetAsync(
            "/api/v1/admin/streams/GetRecentlyActiveStreams");
        protectedResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static string CreateToken(params Claim[] claims) {
        return CreateTokenWithAlgorithm(SecurityAlgorithms.HmacSha256Signature, claims);
    }

    private static void ConfigureStaticAuthority(
        IServiceProvider services,
        string issuer,
        SecurityKey signingKey) {
        var configuration = new OpenIdConnectConfiguration { Issuer = issuer };
        configuration.SigningKeys.Add(signingKey);
        services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme)
            .ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
    }

    private static HttpRequestMessage CreateAuthorityRequest(string token) {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/admin/streams/GetRecentlyActiveStreams?tenantId=test-tenant");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string CreateAuthorityToken(
        SecurityKey signingKey,
        string issuer,
        string audience,
        IEnumerable<Claim> claims,
        string algorithm = SecurityAlgorithms.RsaSha256,
        DateTime? expires = null,
        bool signed = true,
        bool includeExpiry = true) {
        DateTime expiresAt = expires ?? DateTime.UtcNow.AddMinutes(30);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expiresAt < DateTime.UtcNow ? expiresAt.AddMinutes(-30) : DateTime.UtcNow.AddMinutes(-1),
            includeExpiry ? expiresAt : null,
            signed ? new SigningCredentials(signingKey, algorithm) : null);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateAuthorityTokenWithoutAlgorithm(string issuer, string audience) {
        string header = Base64UrlEncoder.Encode("{\"typ\":\"JWT\"}");
        string payload = Base64UrlEncoder.Encode(JsonSerializer.Serialize(new {
            iss = issuer,
            aud = audience,
            sub = "authority-mode-admin",
            global_admin = true,
            exp = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds(),
        }));
        return $"{header}.{payload}.";
    }

    private static string CreateTokenWithAlgorithm(string algorithm, params Claim[] claims) {
        return CreateTokenForValidation(claims, algorithm: algorithm);
    }

    private static string CreateTokenForValidation(
        IEnumerable<Claim> claims,
        string issuer = "hexalith-dev",
        string audience = "hexalith-eventstore",
        DateTime? expires = null,
        string? signingKey = null,
        bool signed = true,
        string algorithm = SecurityAlgorithms.HmacSha256Signature) {
        DateTime expiresAt = expires ?? DateTime.UtcNow.AddMinutes(30);
        var descriptor = new SecurityTokenDescriptor {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = expiresAt < DateTime.UtcNow ? expiresAt.AddMinutes(-30) : DateTime.UtcNow.AddMinutes(-1),
            Expires = expiresAt,
            SigningCredentials = signed
                ? new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                        signingKey ?? AuthenticationTestEnvironment.SigningKey)),
                    algorithm)
                : null,
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private static async Task AssertBoundedProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        params string[] protectedValues) {
        response.StatusCode.ShouldBe(expectedStatus);
        response.Content.Headers.ContentType.ShouldNotBeNull();
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        string body = await response.Content.ReadAsStringAsync();
        body.Length.ShouldBeLessThan(512);
        foreach (string protectedValue in protectedValues) {
            body.ShouldNotContain(protectedValue);
        }
    }

    /// <summary>
    /// Custom WebApplicationFactory that replaces DaprClient with a mock
    /// and uses a test authentication scheme so the host boots without DAPR sidecar.
    /// </summary>
    public class AdminServerHostFactory : WebApplicationFactory<Program> {
        public IStreamQueryService StreamService { get; } = Substitute.For<IStreamQueryService>();

        public IBackupQueryService BackupQueryService { get; } = Substitute.For<IBackupQueryService>();

        public IConsistencyQueryService ConsistencyQueryService { get; } = Substitute.For<IConsistencyQueryService>();

        public IConsistencyCommandService ConsistencyCommandService { get; } = Substitute.For<IConsistencyCommandService>();

        public IDaprInfrastructureQueryService DaprInfrastructureQueryService { get; } = Substitute.For<IDaprInfrastructureQueryService>();

        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            ArgumentNullException.ThrowIfNull(builder);
            _ = StreamService.GetRecentlyActiveStreamsAsync(
                    Arg.Any<string?>(),
                    Arg.Any<string?>(),
                    Arg.Any<int>(),
                    Arg.Any<CancellationToken>())
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
            _ = builder.ConfigureServices(services => {
                // Replace DaprClient with mock
                ServiceDescriptor? daprDescriptor = services
                    .FirstOrDefault(d => d.ServiceType == typeof(DaprClient));
                if (daprDescriptor is not null) {
                    _ = services.Remove(daprDescriptor);
                }

                _ = services.AddSingleton(Substitute.For<DaprClient>());

                // Override DAPR-backed services with mocks so controller routes are reachable
                _ = services.AddSingleton(StreamService);
                _ = services.AddSingleton(BackupQueryService);
                _ = services.AddSingleton(ConsistencyQueryService);
                _ = services.AddSingleton(ConsistencyCommandService);
                _ = services.AddSingleton(DaprInfrastructureQueryService);
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

    private sealed class ProductionAdminServerHostFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            _ = builder.UseEnvironment(Environments.Production);
            _ = builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Authentication:JwtBearer:Authority"] = "https://login.example.test/realms/hexalith",
                    ["Authentication:JwtBearer:Issuer"] = "https://login.example.test/realms/hexalith",
                    ["Authentication:JwtBearer:Audience"] = "hexalith-eventstore",
                    ["Authentication:JwtBearer:AllowedAlgorithms:0"] = SecurityAlgorithms.RsaSha256,
                    ["Authentication:JwtBearer:SigningKey"] = null,
                    ["Authentication:JwtBearer:RequireHttpsMetadata"] = "true",
                }));
        }
    }
}
