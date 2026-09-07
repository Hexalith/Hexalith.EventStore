extern alias SampleApi;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

namespace Hexalith.EventStore.Sample.Tests.SampleApi;

public sealed class SampleApiHostMiddlewareTests
{
    private const string Audience = "hexalith-eventstore";
    private const string Issuer = "hexalith-dev";
    private static readonly string s_signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    [Fact]
    public async Task CounterRoute_WhenBearerTokenIsMissing_ReturnsUnauthorized()
    {
        await using WebApplicationFactory<SampleApi::Program> factory = new WebApplicationFactory<SampleApi::Program>()
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(ConfigureSampleAuth));
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using HttpResponseMessage response = await client
            .GetAsync("/api/tenant-a/counter/counter-1", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CounterRoute_WhenBearerTokenIsValid_RoutesThroughGeneratedControllerAndGateway()
    {
        var gateway = new FakeEventStoreGatewayClient
        {
            QueryHandler = static (_, _, _) =>
            {
                JsonElement payload = JsonSerializer.SerializeToElement(new
                {
                    counterId = "counter-1",
                    value = 7,
                });

                return Task.FromResult(new EventStoreQueryResult(
                    "01KTESTQUERYHTTP00000000",
                    payload,
                    IsNotModified: false,
                    ETag: "counter-version")
                {
                    Metadata = new QueryResponseMetadata(
                        IsStale: false,
                        ProjectionVersion: "42",
                        ServedAt: new DateTimeOffset(2026, 7, 7, 10, 30, 0, TimeSpan.Zero))
                    {
                        Provenance = QueryResponseProvenance.ProjectionBacked,
                        Lifecycle = ProjectionLifecycleState.Current,
                    },
                });
            },
        };

        await using WebApplicationFactory<SampleApi::Program> factory = new WebApplicationFactory<SampleApi::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration(ConfigureSampleAuth);
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IEventStoreGatewayClient>();
                    services.AddSingleton<IEventStoreGatewayClient>(gateway);
                });
            });
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());

        using HttpResponseMessage response = await client
            .GetAsync("/api/tenant-a/counter/counter-1", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe("\"counter-version\"");
        response.Headers.GetValues("X-Hexalith-Query-Provenance").Single().ShouldBe("ProjectionBacked");
        response.Headers.GetValues(ProjectionLifecyclePolicy.HeaderName).Single().ShouldBe("Current");
        response.Headers.GetValues("X-Hexalith-Projection-Version").Single().ShouldBe("42");

        await using Stream body = await response.Content
            .ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(
            body,
            cancellationToken: TestContext.Current.CancellationToken);
        document.RootElement.GetProperty("counterId").GetString().ShouldBe("counter-1");
        document.RootElement.GetProperty("value").GetInt32().ShouldBe(7);

        gateway.QueryCallCount.ShouldBe(1);
        SubmitQueryRequest request = gateway.LastQueryRequest.ShouldNotBeNull();
        request.Tenant.ShouldBe("tenant-a");
        request.Domain.ShouldBe("counter");
        request.AggregateId.ShouldBe("counter-1");
        request.EntityId.ShouldBe("counter-1");
        request.QueryType.ShouldBe("get-counter-status");
    }

    [Fact]
    public async Task CounterRoute_WhenBearerTokenUsesHs384_ReturnsUnauthorized()
    {
        await using WebApplicationFactory<SampleApi::Program> factory = new WebApplicationFactory<SampleApi::Program>()
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(ConfigureSampleAuth));
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(SecurityAlgorithms.HmacSha384Signature));

        using HttpResponseMessage response = await client
            .GetAsync("/api/tenant-a/counter/counter-1", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CounterRoute_WhenTokenValidationDimensionIsInvalid_ReturnsUnauthorized()
    {
        string signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        (string Scenario, string Token)[] invalidTokens =
        [
            ("unsigned", CreateToken(signed: false)),
            ("expired", CreateToken(expires: DateTime.UtcNow.AddMinutes(-10))),
            ("wrong issuer", CreateToken(issuer: "unexpected-issuer")),
            ("wrong audience", CreateToken(audience: "unexpected-audience")),
            ("wrong signing key", CreateToken(
                SecurityAlgorithms.HmacSha256Signature,
                Issuer,
                Audience,
                expires: null,
                signingKey)),
        ];
        await using WebApplicationFactory<SampleApi::Program> factory = new WebApplicationFactory<SampleApi::Program>()
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(ConfigureSampleAuth));
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        foreach ((string scenario, string token) in invalidTokens)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/tenant-a/counter/counter-1");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage response = await client.SendAsync(
                request,
                TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, scenario);
        }
    }

    [Fact]
    public async Task CounterRoute_InAuthorityMode_EnforcesEveryTokenValidationDimension()
    {
        const string authorityIssuer = "https://identity.example.test/realms/hexalith";
        const string additionalAudience = "hexalith-sample-api";
        using RSA rsa = RSA.Create(2048);
        using RSA wrongRsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa) { KeyId = Guid.NewGuid().ToString("N") };
        var wrongSigningKey = new RsaSecurityKey(wrongRsa) { KeyId = Guid.NewGuid().ToString("N") };
        var gateway = new FakeEventStoreGatewayClient
        {
            QueryHandler = static (_, _, _) => Task.FromResult(new EventStoreQueryResult(
                "authority-mode-query",
                JsonSerializer.SerializeToElement(new { counterId = "counter-1", value = 7 }),
                IsNotModified: false,
                ETag: null)),
        };

        await using WebApplicationFactory<SampleApi::Program> factory = new WebApplicationFactory<SampleApi::Program>()
            .WithWebHostBuilder(builder =>
            {
                _ = builder.UseEnvironment("Production");
                _ = builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Authentication:JwtBearer:Authority"] = authorityIssuer,
                        ["Authentication:JwtBearer:Issuer"] = authorityIssuer,
                        ["Authentication:JwtBearer:Audience"] = Audience,
                        ["Authentication:JwtBearer:ValidAudiences:0"] = additionalAudience,
                        ["Authentication:JwtBearer:AllowedAlgorithms:0"] = SecurityAlgorithms.RsaSha256,
                        ["Authentication:JwtBearer:SigningKey"] = null,
                        ["Authentication:JwtBearer:RequireHttpsMetadata"] = "true",
                        ["DAPR_HTTP_ENDPOINT"] = "http://localhost:3500",
                    }));
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IEventStoreGatewayClient>();
                    services.AddSingleton<IEventStoreGatewayClient>(gateway);
                });
            });
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        ConfigureStaticAuthority(factory.Services, authorityIssuer, signingKey);

        string validToken = CreateRsaToken(signingKey, authorityIssuer, additionalAudience);
        using (var validRequest = new HttpRequestMessage(HttpMethod.Get, "/api/tenant-a/counter/counter-1"))
        {
            validRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", validToken);
            using HttpResponseMessage validResponse = await client.SendAsync(
                validRequest,
                TestContext.Current.CancellationToken);
            validResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        string symmetricKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        (string Scenario, string Token)[] invalidTokens =
        [
            ("unsigned", CreateRsaToken(signingKey, authorityIssuer, Audience, signed: false)),
            ("missing expiry", CreateRsaToken(signingKey, authorityIssuer, Audience, includeExpiry: false)),
            ("expired", CreateRsaToken(signingKey, authorityIssuer, Audience, expires: DateTime.UtcNow.AddMinutes(-10))),
            ("wrong issuer", CreateRsaToken(signingKey, "https://unexpected.example.test", Audience)),
            ("wrong audience", CreateRsaToken(signingKey, authorityIssuer, "unexpected-audience")),
            ("wrong signing key", CreateRsaToken(wrongSigningKey, authorityIssuer, Audience)),
            ("wrong algorithm family", CreateSymmetricToken(symmetricKey, authorityIssuer, Audience)),
            ("non-allowlisted algorithm", CreateRsaToken(signingKey, authorityIssuer, Audience, SecurityAlgorithms.RsaSha384)),
            ("missing algorithm", CreateTokenWithoutAlgorithm(authorityIssuer, Audience)),
        ];

        foreach ((string scenario, string token) in invalidTokens)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/tenant-a/counter/counter-1");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using HttpResponseMessage response = await client.SendAsync(
                request,
                TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, scenario);
        }
    }

    [Fact]
    public void Host_WhenProductionSymmetricOverrideIsConfigured_FailsBeforeServingWithoutEchoingKey()
    {
        string signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        using WebApplicationFactory<SampleApi::Program> factory = new WebApplicationFactory<SampleApi::Program>()
            .WithWebHostBuilder(builder =>
            {
                _ = builder.UseEnvironment("Production");
                _ = builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Authentication:JwtBearer:Authority"] = string.Empty,
                        ["Authentication:JwtBearer:Issuer"] = Issuer,
                        ["Authentication:JwtBearer:Audience"] = Audience,
                        ["Authentication:JwtBearer:SigningKey"] = signingKey,
                        ["Authentication:JwtBearer:AllowInsecureSymmetricKey"] = "true",
                        ["DAPR_HTTP_ENDPOINT"] = "http://localhost:3500",
                    }));
            });

        OptionsValidationException exception = Should.Throw<OptionsValidationException>(() => factory.CreateClient());

        exception.Message.ShouldContain("forbidden in Production");
        exception.ToString().ShouldNotContain(signingKey);
    }

    [Fact]
    public void Host_WhenProductionAuthorityAlgorithmsAreMissing_FailsBeforeServing()
    {
        using WebApplicationFactory<SampleApi::Program> factory = new WebApplicationFactory<SampleApi::Program>()
            .WithWebHostBuilder(builder =>
            {
                _ = builder.UseEnvironment("Production");
                _ = builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Authentication:JwtBearer:Authority"] = "https://identity.example.test/realms/hexalith",
                        ["Authentication:JwtBearer:Issuer"] = "https://identity.example.test/realms/hexalith",
                        ["Authentication:JwtBearer:Audience"] = Audience,
                        ["Authentication:JwtBearer:SigningKey"] = null,
                        ["DAPR_HTTP_ENDPOINT"] = "http://localhost:3500",
                    }));
            });

        OptionsValidationException exception = Should.Throw<OptionsValidationException>(() => factory.CreateClient());

        exception.Message.ShouldContain("AllowedAlgorithms");
    }

    private static void ConfigureSampleAuth(WebHostBuilderContext _, IConfigurationBuilder configuration)
        => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DAPR_HTTP_ENDPOINT"] = "http://localhost:3500",
            ["Authentication:JwtBearer:Issuer"] = Issuer,
            ["Authentication:JwtBearer:Audience"] = Audience,
            ["Authentication:JwtBearer:SigningKey"] = s_signingKey,
        });

    private static string CreateToken(
        string algorithm = SecurityAlgorithms.HmacSha256Signature,
        string issuer = Issuer,
        string audience = Audience,
        DateTime? expires = null,
        string? signingKey = null,
        bool signed = true)
    {
        DateTime expiresAt = expires ?? DateTime.UtcNow.AddMinutes(30);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity([new Claim("sub", "sample-user")]),
            NotBefore = expiresAt < DateTime.UtcNow ? expiresAt.AddMinutes(-30) : DateTime.UtcNow.AddMinutes(-1),
            Expires = expiresAt,
            SigningCredentials = signed
                ? new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? s_signingKey)),
                    algorithm)
                : null,
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private static void ConfigureStaticAuthority(
        IServiceProvider services,
        string issuer,
        SecurityKey signingKey)
    {
        var configuration = new OpenIdConnectConfiguration { Issuer = issuer };
        configuration.SigningKeys.Add(signingKey);
        services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme)
            .ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
    }

    private static string CreateRsaToken(
        SecurityKey signingKey,
        string issuer,
        string audience,
        string algorithm = SecurityAlgorithms.RsaSha256,
        DateTime? expires = null,
        bool signed = true,
        bool includeExpiry = true)
    {
        DateTime expiresAt = expires ?? DateTime.UtcNow.AddMinutes(30);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            [new Claim("sub", "authority-mode-user")],
            expiresAt < DateTime.UtcNow ? expiresAt.AddMinutes(-30) : DateTime.UtcNow.AddMinutes(-1),
            includeExpiry ? expiresAt : null,
            signed ? new SigningCredentials(signingKey, algorithm) : null);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateSymmetricToken(string key, string issuer, string audience)
        => CreateRsaToken(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            issuer,
            audience,
            SecurityAlgorithms.HmacSha256Signature);

    private static string CreateTokenWithoutAlgorithm(string issuer, string audience)
    {
        string header = Base64UrlEncoder.Encode("{\"typ\":\"JWT\"}");
        string payload = Base64UrlEncoder.Encode(JsonSerializer.Serialize(new
        {
            iss = issuer,
            aud = audience,
            sub = "authority-mode-user",
            exp = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds(),
        }));
        return $"{header}.{payload}.";
    }
}
