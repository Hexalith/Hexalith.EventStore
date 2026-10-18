using System.Net;
using Microsoft.Extensions.Time.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authentication;

/// <summary>Production-mode tests use a disposable synthetic RSA authority; they establish no real deployment or enrollment.</summary>
public sealed class WorkloadBindingAuthorityTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "workload-authority-" + Guid.NewGuid().ToString("N") + ".pfx");
    private readonly string _secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly WorkloadAuthorityOptions _options;
    private readonly WorkloadBindingAuthority _authority;

    /// <summary>Creates a disposable, untrusted test certificate and an explicitly synthetic grant.</summary>
    public WorkloadBindingAuthorityTests()
    {
        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=synthetic-authority-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(1));
        File.WriteAllBytes(_path, certificate.Export(X509ContentType.Pkcs12));
        _options = new WorkloadAuthorityOptions
        {
            Enabled = true, Issuer = "https://authority.example.test/workload-authority", KeyId = "synthetic-test-key",
            SigningCertificatePath = _path,
            Grants = [new WorkloadAuthorityGrant
            {
                GrantId = "synthetic-grant", Revision = 1, ClientId = "synthetic-client", Workload = "timesheets", Audience = "eventstore",
                ClientSecretSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_secret))),
                NotBefore = DateTimeOffset.UtcNow.AddMinutes(-1), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                Operations = ["gateway:query"], Bindings = new(StringComparer.Ordinal)
                {
                    [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = ["synthetic-tenant", "other-tenant"],
                    [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = ["timesheets"],
                },
            }],
        };
        IOptionsMonitor<WorkloadAuthorityOptions> monitor = Substitute.For<IOptionsMonitor<WorkloadAuthorityOptions>>();
        monitor.CurrentValue.Returns(_options);
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        _authority = new WorkloadBindingAuthority(monitor, environment, TimeProvider.System);
    }

    /// <summary>The production issuer signs only the exact authorized workload, operation and tenant, with verifiable RSA and bounded expiry.</summary>
    [Fact]
    public async Task ProductionGrantProducesExactlyBoundAsymmetricAssertion()
    {
        string token = Issue().ShouldNotBeNull();
        var parsed = new JsonWebToken(token);
        parsed.Alg.ShouldBe("RS256");
        parsed.GetClaim("azp").Value.ShouldBe("timesheets");
        parsed.GetClaim(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType).Value.ShouldBe("synthetic-tenant");
        parsed.GetClaim("eventstore:grant-id").Value.ShouldBe("synthetic-grant");
        (parsed.ValidTo - parsed.IssuedAt).ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(120));
        string jwks = JsonSerializer.Serialize(_authority.GetPublicKeys());
        var keys = new JsonWebKeySet(jwks);
        TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer, ValidAudience = "eventstore", IssuerSigningKeys = keys.GetSigningKeys(),
            ValidAlgorithms = ["RS256"], RequireSignedTokens = true, ValidateIssuerSigningKey = true, ClockSkew = TimeSpan.Zero,
        });
        result.IsValid.ShouldBeTrue();
        jwks.ShouldNotContain("PRIVATE");
        using JsonDocument document = JsonDocument.Parse(jwks);
        document.RootElement.GetProperty("keys")[0].TryGetProperty("d", out _).ShouldBeFalse();
    }

    /// <summary>One current grant is required; unknown, expired, revoked or ambiguous grants never mint a token.</summary>
    /// <param name="failure">The synthetic trust failure.</param>
    [Theory]
    [InlineData("disabled")]
    [InlineData("no-grants")]
    [InlineData("revoked")]
    [InlineData("expired")]
    [InlineData("not-started")]
    [InlineData("duplicate")]
    [InlineData("bad-secret")]
    [InlineData("bad-tenant")]
    [InlineData("bad-domain")]
    [InlineData("bad-operation")]
    [InlineData("extra-actor")]
    [InlineData("missing-required-actor")]
    [InlineData("http-issuer")]
    [InlineData("missing-signer")]
    [InlineData("unbound")]
    public void UnavailableOrUnauthorizedEvidenceFailsClosed(string failure)
    {
        WorkloadAuthorizationDetails details = Details();
        string secret = _secret;
        switch (failure)
        {
            case "disabled": _options.Enabled = false; break;
            case "no-grants": _options.Grants = []; break;
            case "revoked": _options.Grants[0].Revoked = true; break;
            case "expired": _options.Grants[0].ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1); break;
            case "not-started": _options.Grants[0].NotBefore = DateTimeOffset.UtcNow.AddHours(1); break;
            case "duplicate": _options.Grants = [_options.Grants[0], _options.Grants[0]]; break;
            case "bad-secret": secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); break;
            case "bad-tenant": details.Bindings[EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = "unapproved"; break;
            case "bad-domain": details.Bindings[EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = "party"; break;
            case "bad-operation": details = details with { Operation = "gateway:command" }; break;
            case "extra-actor": details.Bindings[EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType] = "unapproved"; break;
            case "missing-required-actor": _options.Grants[0].Bindings.Add(EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType, ["required-actor"]); break;
            case "http-issuer": _options.Issuer = "http://authority.example.test/workload-authority"; break;
            case "missing-signer": _options.SigningCertificatePath += ".missing"; break;
            case "unbound": details.Bindings.Clear(); break;
        }
        _authority.Issue("synthetic-client", secret, Scope(details), details).ShouldBeNull();
    }

    /// <summary>Grant revocation applies immediately to subsequent issuance; issued assertions are never reused by this broker.</summary>
    [Fact]
    public void CurrentGrantIsRecheckedEveryTime()
    {
        string first = Issue().ShouldNotBeNull();
        string second = Issue().ShouldNotBeNull();
        first.ShouldNotBe(second);
        _options.Grants[0].Revoked = true;
        Issue().ShouldBeNull();
    }

    /// <summary>JSON ambiguity and incomplete binding scopes are rejected before authentication.</summary>
    /// <param name="json">The malformed profile.</param>
    [Theory]
    [InlineData("[]")]
    [InlineData("[{}]")]
    [InlineData("[{\"type\":\"unknown\",\"audience\":\"a\",\"operation\":\"o\",\"bindings\":{}}]")]
    [InlineData("[{\"type\":\"https://hexalith.com/security/workload-binding/v1\",\"audience\":\"a\",\"operation\":\"o\",\"bindings\":{\"eventstore:bound-tenant\":\"t\"}}]")]
    public void InvalidDetailsAreRefused(string json) => WorkloadAuthorizationDetails.Decode(json).ShouldBeNull();

    /// <summary>The complete broker and outbound client accept a signed exact bound request and reject altered bindings and signatures.</summary>
    /// <param name="shape">Synthetic response shape.</param>
    /// <param name="success">Expected acceptance.</param>
    [Theory]
    [InlineData("exact", true)]
    [InlineData("wrong-tenant", false)]
    [InlineData("unsigned", false)]
    [InlineData("unbound", true)]
    [InlineData("oversized-response", false)]
    public async Task BoundClientChecksSignatureAndWholeResourceScope(string shape, bool success)
    {
        if (shape == "unbound") _options.Grants[0].AllowUnbound = true;
        using var handler = new SyntheticBindingAuthorityHandler(_authority, shape);
        using var http = new HttpClient(handler);
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(JwtWorkloadAssertionIssuer.HttpClientName).Returns(http);
        IOptionsMonitor<JwtBearerAuthenticationOptions> contract = Substitute.For<IOptionsMonitor<JwtBearerAuthenticationOptions>>();
        contract.Get(EventStoreWorkloadAuthenticationDefaults.JwtContractOptionsName).Returns(new JwtBearerAuthenticationOptions
        {
            Authority = _options.Issuer, Issuer = _options.Issuer, Audience = "eventstore", AllowedAlgorithms = ["RS256"],
        });
        IOptionsMonitor<WorkloadAssertionIssuerOptions> issuerOptions = Substitute.For<IOptionsMonitor<WorkloadAssertionIssuerOptions>>();
        issuerOptions.CurrentValue.Returns(new WorkloadAssertionIssuerOptions
        {
            Workload = "timesheets", ClientId = "synthetic-client", ClientSecret = _secret,
            TokenEndpoint = _options.Issuer + "/token", EnableResourceBindingRequests = true,
        });
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        using var issuer = new JwtWorkloadAssertionIssuer(contract, issuerOptions, environment, factory,
            WorkloadSecurityClock.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<JwtWorkloadAssertionIssuer>.Instance);
        issuer.CanBindResources.ShouldBeTrue();
        var request = new WorkloadAssertionRequest("eventstore", "gateway:query", shape == "unbound" ? null : Details().Bindings);
        string? first = await issuer.IssueAsync(request, TestContext.Current.CancellationToken);
        string? second = await issuer.IssueAsync(request, TestContext.Current.CancellationToken);
        (first is not null).ShouldBe(success);
        (second is not null).ShouldBe(success);
        handler.TokenCalls.ShouldBe(2);
        if (shape == "oversized-response") handler.ResponseBytesRead.ShouldBeLessThanOrEqualTo(65537);
        if (success)
        {
            first.ShouldNotBe(second);
            handler.MetadataCalls.ShouldBe(1);
            handler.KeyCalls.ShouldBe(1);
        }
    }

    /// <summary>Remaining certificate/grant validity and the maximum token lifetime independently clip exact JWT expiry.</summary>
    /// <param name="grantSeconds">Remaining grant lifetime.</param>
    /// <param name="certificateSeconds">Remaining signing-certificate lifetime.</param>
    /// <param name="expectedSeconds">The exact permitted assertion lifetime.</param>
    [Theory]
    [InlineData(17, 300, 17)]
    [InlineData(300, 31, 31)]
    [InlineData(300, 300, 120)]
    public void AssertionExpiryNeverExceedsAnyRemainingBoundary(int grantSeconds, int certificateSeconds, int expectedSeconds)
    {
        DateTimeOffset now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=synthetic-clipping-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(now.AddMinutes(-1), now.AddSeconds(certificateSeconds));
        File.WriteAllBytes(_path, certificate.Export(X509ContentType.Pkcs12));
        _options.Grants[0].NotBefore = now.AddMinutes(-1);
        _options.Grants[0].ExpiresAt = now.AddSeconds(grantSeconds);
        IOptionsMonitor<WorkloadAuthorityOptions> monitor = Substitute.For<IOptionsMonitor<WorkloadAuthorityOptions>>(); monitor.CurrentValue.Returns(_options);
        IHostEnvironment environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns(Environments.Production);
        var authority = new WorkloadBindingAuthority(monitor, environment, new FakeTimeProvider(now));
        string token = authority.Issue("synthetic-client", _secret, Scope(Details()), Details()).ShouldNotBeNull();
        new JsonWebToken(token).ValidTo.ShouldBe(now.AddSeconds(expectedSeconds).UtcDateTime);
    }

    /// <summary>Changing authority and credential invalidates discovery; no new secret is ever sent to the prior discovered endpoint.</summary>
    [Fact]
    public async Task AuthorityRotationIsolatesMetadataAndCredentialDestinations()
    {
        using var handler = new SyntheticBindingAuthorityHandler(_authority, "exact");
        using var http = new HttpClient(handler);
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>(); factory.CreateClient(JwtWorkloadAssertionIssuer.HttpClientName).Returns(http);
        IOptionsMonitor<JwtBearerAuthenticationOptions> contracts = Substitute.For<IOptionsMonitor<JwtBearerAuthenticationOptions>>();
        contracts.Get(EventStoreWorkloadAuthenticationDefaults.JwtContractOptionsName).Returns(_ => new JwtBearerAuthenticationOptions
        { Authority = _options.Issuer, Issuer = _options.Issuer, Audience = "eventstore", AllowedAlgorithms = ["RS256"] });
        var configuredIssuer = new WorkloadAssertionIssuerOptions { Workload = "timesheets", ClientId = "synthetic-client", ClientSecret = _secret, EnableResourceBindingRequests = true };
        IOptionsMonitor<WorkloadAssertionIssuerOptions> issuers = Substitute.For<IOptionsMonitor<WorkloadAssertionIssuerOptions>>(); issuers.CurrentValue.Returns(configuredIssuer);
        IHostEnvironment environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns(Environments.Production);
        using var issuer = new JwtWorkloadAssertionIssuer(contracts, issuers, environment, factory, WorkloadSecurityClock.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<JwtWorkloadAssertionIssuer>.Instance);
        var request = new WorkloadAssertionRequest("eventstore", "gateway:query", Details().Bindings);
        (await issuer.IssueAsync(request, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        Uri oldEndpoint = new(_options.Issuer + "/token");
        _options.Issuer = "https://second-authority.example.test/workload-authority";
        string newSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        configuredIssuer.ClientSecret = newSecret;
        string newDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(newSecret)));
        _options.Grants[0].ClientSecretSha256 = newDigest;
        (await issuer.IssueAsync(request, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        (await issuer.IssueAsync(request, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        handler.PostedCredentials.Select(value => value.Endpoint).ShouldBe([oldEndpoint, new Uri(_options.Issuer + "/token"), new Uri(_options.Issuer + "/token")]);
        handler.PostedCredentials.Any(value => value.Endpoint == oldEndpoint && value.SecretDigest == newDigest).ShouldBeFalse();
        handler.MetadataCalls.ShouldBe(2); handler.KeyCalls.ShouldBe(2);
    }

    /// <summary>The actual outbound issuer authenticates through the mapped authority's form boundary using the transmitted secret.</summary>
    /// <param name="correctSecret">Whether the configured transmitted secret is correct.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RealIssuerAuthenticatesAgainstMappedAuthority(bool correctSecret)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.WebHost.UseTestServer(); builder.Services.AddSingleton(_authority);
        await using WebApplication app = builder.Build(); app.MapEventStoreWorkloadBindingAuthority();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient http = app.GetTestClient();
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>(); factory.CreateClient(JwtWorkloadAssertionIssuer.HttpClientName).Returns(http);
        IOptionsMonitor<JwtBearerAuthenticationOptions> contracts = Substitute.For<IOptionsMonitor<JwtBearerAuthenticationOptions>>();
        contracts.Get(EventStoreWorkloadAuthenticationDefaults.JwtContractOptionsName).Returns(new JwtBearerAuthenticationOptions
        { Authority = _options.Issuer, Issuer = _options.Issuer, Audience = "eventstore", AllowedAlgorithms = ["RS256"] });
        IOptionsMonitor<WorkloadAssertionIssuerOptions> issuers = Substitute.For<IOptionsMonitor<WorkloadAssertionIssuerOptions>>();
        issuers.CurrentValue.Returns(new WorkloadAssertionIssuerOptions
        { Workload = "timesheets", ClientId = "synthetic-client", ClientSecret = correctSecret ? _secret : Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), EnableResourceBindingRequests = true });
        IHostEnvironment environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns(Environments.Production);
        using var issuer = new JwtWorkloadAssertionIssuer(contracts, issuers, environment, factory, WorkloadSecurityClock.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<JwtWorkloadAssertionIssuer>.Instance);
        string? assertion = await issuer.IssueAsync(new("eventstore", "gateway:query", Details().Bindings), TestContext.Current.CancellationToken);
        (assertion is not null).ShouldBe(correctSecret);
    }

    /// <summary>The issuer path must match the mapped route and cannot advertise nonexistent endpoints after configuration reload.</summary>
    [Fact]
    public async Task AuthorityMapRejectsMismatchedIssuerPrefixAndReloadDrift()
    {
        _options.Issuer = "https://authority.example.test/custom-authority";
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.WebHost.UseTestServer(); builder.Services.AddSingleton(_authority);
        await using WebApplication app = builder.Build();
        Should.Throw<InvalidOperationException>(() => app.MapEventStoreWorkloadBindingAuthority());
        app.MapEventStoreWorkloadBindingAuthority("/custom-authority");
        await app.StartAsync(TestContext.Current.CancellationToken);
        RouteEndpoint[] routes = [.. ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()];
        routes.Single(route => route.RoutePattern.RawText == "/custom-authority/token").Metadata.GetMetadata<WorkloadAuthorityEndpointMetadata>()
            .ShouldBeSameAs(WorkloadAuthorityEndpointMetadata.Token);
        routes.Single(route => route.RoutePattern.RawText == "/custom-authority/jwks").Metadata.GetMetadata<WorkloadAuthorityEndpointMetadata>()
            .ShouldBeSameAs(WorkloadAuthorityEndpointMetadata.Keys);
        routes.Single(route => route.RoutePattern.RawText == "/custom-authority/.well-known/openid-configuration").Metadata.GetMetadata<WorkloadAuthorityEndpointMetadata>()
            .ShouldBeSameAs(WorkloadAuthorityEndpointMetadata.Discovery);
        using HttpClient client = app.GetTestClient(); client.BaseAddress = new Uri("https://authority.example.test");
        using HttpResponseMessage metadata = await client.GetAsync("/custom-authority/.well-known/openid-configuration", TestContext.Current.CancellationToken);
        metadata.StatusCode.ShouldBe(HttpStatusCode.OK);
        string document = await metadata.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        document.ShouldContain(_options.Issuer + "/token");
        using HttpResponseMessage old = await client.GetAsync("/workload-authority/.well-known/openid-configuration", TestContext.Current.CancellationToken);
        old.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        _options.Issuer = "https://authority.example.test/nonexistent-authority";
        using HttpResponseMessage drift = await client.GetAsync("/custom-authority/.well-known/openid-configuration", TestContext.Current.CancellationToken);
        drift.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>A dedicated workload issuer and receiver verify the broker while public human JWT options retain their existing issuer.</summary>
    [Fact]
    public async Task DedicatedWorkloadContractKeepsPublicUserAuthoritySeparate()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Authority"] = "https://users.example.test",
            ["Authentication:JwtBearer:Issuer"] = "https://users.example.test",
            ["Authentication:JwtBearer:Audience"] = "public-users",
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "RS256",
            ["Authentication:WorkloadJwtBearer:Authority"] = _options.Issuer,
            ["Authentication:WorkloadJwtBearer:Issuer"] = _options.Issuer,
            ["Authentication:WorkloadJwtBearer:Audience"] = "eventstore",
            ["Authentication:WorkloadJwtBearer:AllowedAlgorithms:0"] = "RS256",
            ["Authentication:WorkloadIssuer:ClientId"] = "synthetic-client",
            ["Authentication:WorkloadIssuer:ClientSecret"] = _secret,
            ["Authentication:WorkloadIssuer:Workload"] = "timesheets",
            ["Authentication:WorkloadIssuer:EnableResourceBindingRequests"] = "true",
            ["Authentication:WorkloadIssuer:TokenEndpoint"] = _options.Issuer + "/token",
        };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(configuration);
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        services.AddSingleton(environment);
        services.AddAuthentication().AddJwtBearer("public-users", options => JwtBearerAuthenticationContract.Configure(options,
            configuration.GetSection("Authentication:JwtBearer").Get<JwtBearerAuthenticationOptions>()!))
            .AddEventStoreWorkloadScheme("dedicated-workload", "SyntheticReceiver", (options, _) =>
            { options.Audience = "eventstore"; options.AllowedCallers = ["timesheets"]; });
        services.AddEventStoreWorkloadAssertionIssuer();
        services.AddHttpClient(JwtWorkloadAssertionIssuer.HttpClientName).ConfigurePrimaryHttpMessageHandler(() =>
            new SyntheticBindingAuthorityHandler(_authority, "exact"));
        await using ServiceProvider provider = services.BuildServiceProvider();
        IOptionsMonitor<JwtBearerOptions> bearer = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        bearer.Get("public-users").Authority.ShouldBe("https://users.example.test");
        bearer.Get("dedicated-workload").Authority.ShouldBe(_options.Issuer);
        var metadata = new OpenIdConnectConfiguration { Issuer = _options.Issuer };
        foreach (SecurityKey key in new JsonWebKeySet(JsonSerializer.Serialize(_authority.GetPublicKeys())).GetSigningKeys()) metadata.SigningKeys.Add(key);
        bearer.Get("dedicated-workload").ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
        string token = (await provider.GetRequiredService<IWorkloadAssertionIssuer>().IssueAsync(
            new("eventstore", "gateway:query", Details().Bindings), TestContext.Current.CancellationToken)).ShouldNotBeNull();
        WorkloadAssertionEvaluation evaluation = await provider.GetRequiredService<WorkloadAssertionValidator>().ValidateAsync(
            "dedicated-workload", token, "gateway:query", null, TestContext.Current.CancellationToken);
        evaluation.Succeeded.ShouldBeTrue();
        evaluation.Bindings[EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType].ShouldBe("synthetic-tenant");
        bearer.Get("public-users").Authority.ShouldBe("https://users.example.test");
    }

    /// <summary>A partial dedicated section cannot inherit an otherwise usable public key/issuer.</summary>
    [Fact]
    public void PartialDedicatedContractFailsClosedWithoutPublicFallback()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Authority"] = "https://users.example.test",
            ["Authentication:JwtBearer:Issuer"] = "https://users.example.test",
            ["Authentication:JwtBearer:Audience"] = "public-users",
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "RS256",
            ["Authentication:WorkloadJwtBearer:Audience"] = "eventstore",
        }).Build();
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(configuration);
        IHostEnvironment environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns(Environments.Production);
        services.AddSingleton(environment); services.AddEventStoreWorkloadAssertionIssuer();
        using ServiceProvider provider = services.BuildServiceProvider();
        JwtBearerAuthenticationOptions contract = provider.GetRequiredService<IOptionsMonitor<JwtBearerAuthenticationOptions>>()
            .Get(EventStoreWorkloadAuthenticationDefaults.JwtContractOptionsName);
        contract.Authority.ShouldBeNull(); contract.Issuer.ShouldBeEmpty();
        provider.GetRequiredService<IWorkloadAssertionIssuer>().CanBindResources.ShouldBeFalse();
    }

    /// <summary>The mapped production endpoint enforces HTTPS, bounded exact form fields, credentials, and no-store responses.</summary>
    /// <param name="shape">The synthetic HTTP request variation.</param>
    /// <param name="status">Expected HTTP status.</param>
    [Theory]
    [InlineData("exact", 200)]
    [InlineData("http", 400)]
    [InlineData("unknown-field", 400)]
    [InlineData("duplicate-client", 400)]
    [InlineData("bad-secret", 401)]
    [InlineData("oversized", 400)]
    [InlineData("bad-profile", 400)]
    public async Task ProductionHttpEndpointEnforcesRequestBoundary(string shape, int status)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(_authority);
        await using WebApplication app = builder.Build();
        app.MapEventStoreWorkloadBindingAuthority();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        client.BaseAddress = new Uri(shape == "http" ? "http://authority.example.test" : "https://authority.example.test");
        var fields = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "client_credentials"), new("client_id", "synthetic-client"),
            new("client_secret", shape == "bad-secret" ? new string('x', 64) : _secret), new("scope", Scope(Details())),
            new("authorization_details", shape == "bad-profile" ? "[]" : WorkloadAuthorizationDetails.Encode(new("eventstore", "gateway:query", Details().Bindings))),
        };
        if (shape == "unknown-field") fields.Add(new("extra", "unexpected"));
        if (shape == "duplicate-client") fields.Add(new("client_id", "synthetic-client"));
        if (shape == "oversized") fields.Add(new("extra", new string('x', 20000)));
        using var content = new FormUrlEncodedContent(fields);
        using HttpResponseMessage response = await client.PostAsync("/workload-authority/token", content, TestContext.Current.CancellationToken);
        ((int)response.StatusCode).ShouldBe(status);
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldNotContain(_secret);
        if (status == 200)
        {
            using JsonDocument document = JsonDocument.Parse(body);
            document.RootElement.GetProperty("token_type").GetString().ShouldBe("Bearer");
            new JsonWebToken(document.RootElement.GetProperty("access_token").GetString()!).Alg.ShouldBe("RS256");
        }
    }

    /// <inheritdoc/>
    public void Dispose() => File.Delete(_path);

    private string? Issue() => _authority.Issue("synthetic-client", _secret, Scope(Details()), Details());
    private static string Scope(WorkloadAuthorizationDetails details) => new WorkloadAssertionIssuerOptions().BuildScope(details.Audience, details.Operation);
    private static WorkloadAuthorizationDetails Details() => new()
    {
        Audience = "eventstore", Operation = "gateway:query", Bindings = new(StringComparer.Ordinal)
        {
            [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = "synthetic-tenant",
            [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = "timesheets",
        },
    };
}
