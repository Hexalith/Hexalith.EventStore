using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authentication;

/// <summary>
/// Story 5.5: workload assertions come only from the trusted JWT issuer named by the shared contract — the
/// Development/break-glass signing key or the configured authority — and never exceed the short-lived bound.
/// </summary>
public sealed class JwtWorkloadAssertionIssuerTests
{
    private const string Authority = "https://identity.example.test/realms/hexalith";
    private const string TokenEndpoint = "https://identity.example.test/realms/hexalith/protocol/openid-connect/token";

    /// <summary>In symmetric Development mode the assertion is signed per call with exact audience, operation, and bindings.</summary>
    [Fact]
    public async Task SymmetricContract_SignsShortLivedBoundAssertion()
    {
        await using ServiceProvider services = Build(Environments.Development, Symmetric(), workload: "eventstore");
        IWorkloadAssertionIssuer issuer = services.GetRequiredService<IWorkloadAssertionIssuer>();

        string? assertion = await issuer.IssueAsync(
            new WorkloadAssertionRequest(
                "eventstore",
                EventStoreWorkloadOperations.ProjectionNotify,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = "acme",
                }),
            TestContext.Current.CancellationToken);

        assertion.ShouldNotBeNull();
        var token = new JsonWebToken(assertion);
        token.Alg.ShouldBe("HS256");
        token.Issuer.ShouldBe("hexalith-dev");
        token.Audiences.ShouldBe(["eventstore"]);
        token.GetClaim("azp").Value.ShouldBe("eventstore");
        token.GetClaim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType).Value.ShouldBe(EventStoreWorkloadOperations.ProjectionNotify);
        token.GetClaim(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType).Value.ShouldBe("acme");
        (token.ValidTo - token.IssuedAt).ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(JwtWorkloadAssertionIssuer.MaximumSignedLifetimeSeconds));
        token.TryGetClaim("global_admin", out _).ShouldBeFalse();
    }

    /// <summary>Production never signs with a symmetric key, even with the break-glass flag.</summary>
    [Fact]
    public async Task SymmetricContractInProduction_IssuesNothing()
    {
        Dictionary<string, string?> settings = Symmetric();
        settings["Authentication:JwtBearer:AllowInsecureSymmetricKey"] = "true";
        await using ServiceProvider services = Build(Environments.Production, settings, workload: "eventstore");

        string? assertion = await services.GetRequiredService<IWorkloadAssertionIssuer>().IssueAsync(
            new WorkloadAssertionRequest("sample", EventStoreWorkloadOperations.DomainServiceProcess),
            TestContext.Current.CancellationToken);

        assertion.ShouldBeNull();
    }

    /// <summary>
    /// Story 5.5 (BS-C): authority mode requests one client-credentials token per (audience, operation) through the
    /// audience and operation scopes, attaches it only when it names exactly that pair, and caches each pair separately.
    /// </summary>
    [Fact]
    public async Task AuthorityContract_RequestsAndCachesOneTokenPerAudienceAndOperation()
    {
        var tokenEndpoint = new ScopedWorkloadTokenEndpoint();
        await using ServiceProvider services = Build(Environments.Production, AuthorityWithClient(), workload: "eventstore", tokenEndpoint);
        IWorkloadAssertionIssuer issuer = services.GetRequiredService<IWorkloadAssertionIssuer>();

        string? sampleProcess = await issuer.IssueAsync(new WorkloadAssertionRequest("sample", EventStoreWorkloadOperations.DomainServiceProcess), TestContext.Current.CancellationToken);
        string? sampleProcessAgain = await issuer.IssueAsync(new WorkloadAssertionRequest("sample", EventStoreWorkloadOperations.DomainServiceProcess), TestContext.Current.CancellationToken);
        string? sampleQuery = await issuer.IssueAsync(new WorkloadAssertionRequest("sample", EventStoreWorkloadOperations.DomainServiceQuery), TestContext.Current.CancellationToken);
        string? tenantsQuery = await issuer.IssueAsync(new WorkloadAssertionRequest("tenants", EventStoreWorkloadOperations.DomainServiceQuery), TestContext.Current.CancellationToken);

        AssertExactlyScoped(sampleProcess, "sample", EventStoreWorkloadOperations.DomainServiceProcess);
        sampleProcessAgain.ShouldBe(sampleProcess);
        AssertExactlyScoped(sampleQuery, "sample", EventStoreWorkloadOperations.DomainServiceQuery);
        AssertExactlyScoped(tenantsQuery, "tenants", EventStoreWorkloadOperations.DomainServiceQuery);
        tokenEndpoint.Scopes.ShouldBe(
        [
            "eventstore-audience.sample eventstore-operation.domain-service.process",
            "eventstore-audience.sample eventstore-operation.domain-service.query",
            "eventstore-audience.tenants eventstore-operation.domain-service.query",
        ]);
        tokenEndpoint.Bodies.ShouldAllBe(static body => body.Contains("grant_type=client_credentials", StringComparison.Ordinal)
            && body.Contains("client_id=eventstore", StringComparison.Ordinal));
        tokenEndpoint.Uris.ShouldAllBe(static uri => uri == new Uri(TokenEndpoint));
    }

    /// <summary>
    /// A cached token is reused until its refresh point, and a new token is requested once the clock passes it.
    /// </summary>
    [Fact]
    public async Task AuthorityContract_RequestsANewTokenAfterTheCachedTokenRefreshPoint()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var tokenEndpoint = new ScopedWorkloadTokenEndpoint { Clock = clock.GetUtcNow, TokenLifetime = TimeSpan.FromSeconds(300), ExpiresIn = 300 };
        await using ServiceProvider services = Build(Environments.Production, AuthorityWithClient(), workload: "eventstore", tokenEndpoint, clock);
        IWorkloadAssertionIssuer issuer = services.GetRequiredService<IWorkloadAssertionIssuer>();
        var request = new WorkloadAssertionRequest("sample", EventStoreWorkloadOperations.DomainServiceProcess);

        string? first = await issuer.IssueAsync(request, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(269));
        string? beforeRefresh = await issuer.IssueAsync(request, TestContext.Current.CancellationToken);
        int callsBeforeRefresh = tokenEndpoint.Scopes.Count;
        clock.Advance(TimeSpan.FromSeconds(2));
        string? afterRefresh = await issuer.IssueAsync(request, TestContext.Current.CancellationToken);

        beforeRefresh.ShouldBe(first);
        callsBeforeRefresh.ShouldBe(1);
        tokenEndpoint.Scopes.Count.ShouldBe(2);
        afterRefresh.ShouldNotBeNull();
        afterRefresh.ShouldNotBe(first);
        ScopedWorkloadTokenEndpoint.AssertExactlyScoped(afterRefresh, "sample", EventStoreWorkloadOperations.DomainServiceProcess);
    }

    /// <summary>
    /// When the token's own <c>exp</c> is earlier than the advertised <c>expires_in</c>, the refresh point follows
    /// <c>exp</c>, so an expired token is never reused.
    /// </summary>
    [Fact]
    public async Task AuthorityContract_RefreshFollowsTheTokenExpiryWhenItIsEarlierThanExpiresIn()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var tokenEndpoint = new ScopedWorkloadTokenEndpoint { Clock = clock.GetUtcNow, TokenLifetime = TimeSpan.FromSeconds(60), ExpiresIn = 300 };
        await using ServiceProvider services = Build(Environments.Production, AuthorityWithClient(), workload: "eventstore", tokenEndpoint, clock);
        IWorkloadAssertionIssuer issuer = services.GetRequiredService<IWorkloadAssertionIssuer>();
        var request = new WorkloadAssertionRequest("sample", EventStoreWorkloadOperations.DomainServiceProcess);

        string? first = await issuer.IssueAsync(request, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(29));
        string? cached = await issuer.IssueAsync(request, TestContext.Current.CancellationToken);
        int callsWhileCached = tokenEndpoint.Scopes.Count;

        // Past half of the 60-second exp lifetime, long before the advertised 300-second expires_in would refresh.
        clock.Advance(TimeSpan.FromSeconds(2));
        string? refreshed = await issuer.IssueAsync(request, TestContext.Current.CancellationToken);

        cached.ShouldBe(first);
        callsWhileCached.ShouldBe(1);
        tokenEndpoint.Scopes.Count.ShouldBe(2);
        refreshed.ShouldNotBe(first);
    }

    /// <summary>
    /// <c>expires_in</c> sent as a numeric string, as some identity providers do, is honored; anything else falls back to
    /// the default lifetime instead of discarding a valid token.
    /// </summary>
    /// <param name="expiresIn">The raw <c>expires_in</c> JSON value.</param>
    /// <param name="expected">The expected seconds, or <see langword="null"/> for the default.</param>
    [Theory]
    [InlineData("300", 300)]
    [InlineData("\"300\"", 300)]
    [InlineData("\"-5\"", null)]
    [InlineData("\"soon\"", null)]
    [InlineData("0", null)]
    [InlineData("true", null)]
    [InlineData("12.5", null)]
    public void ReadExpiresIn_AcceptsNumbersAndNumericStrings(string expiresIn, int? expected)
    {
        using JsonDocument document = JsonDocument.Parse("{\"expires_in\":" + expiresIn + "}");

        JwtWorkloadAssertionIssuer.ReadExpiresIn(document.RootElement).ShouldBe(expected);
    }

    /// <summary>A token response whose <c>expires_in</c> is a numeric string is attached and cached, not discarded.</summary>
    [Fact]
    public async Task AuthorityContract_AcceptsAStringExpiresIn()
    {
        var tokenEndpoint = new ScopedWorkloadTokenEndpoint { ExpiresIn = "300" };
        await using ServiceProvider services = Build(Environments.Production, AuthorityWithClient(), workload: "eventstore", tokenEndpoint);
        IWorkloadAssertionIssuer issuer = services.GetRequiredService<IWorkloadAssertionIssuer>();
        var request = new WorkloadAssertionRequest("sample", EventStoreWorkloadOperations.DomainServiceProcess);

        string? first = await issuer.IssueAsync(request, TestContext.Current.CancellationToken);
        string? second = await issuer.IssueAsync(request, TestContext.Current.CancellationToken);

        ScopedWorkloadTokenEndpoint.AssertExactlyScoped(first, "sample", EventStoreWorkloadOperations.DomainServiceProcess);
        second.ShouldBe(first);
        tokenEndpoint.Scopes.Count.ShouldBe(1);
    }

    /// <summary>
    /// A token broader than, or different from, the requested audience and operation is never attached nor cached;
    /// the next request asks the authority again.
    /// </summary>
    /// <param name="shape">How the authority's token deviates from the request.</param>
    [Theory]
    [InlineData("extra-audience")]
    [InlineData("extra-operation")]
    [InlineData("all-operations")]
    [InlineData("wrong-audience")]
    [InlineData("wrong-operation")]
    [InlineData("no-audience")]
    [InlineData("no-operation")]
    [InlineData("opaque")]
    public async Task AuthorityContract_DiscardsTokensThatAreNotExactlyScoped(string shape)
    {
        var tokenEndpoint = new ScopedWorkloadTokenEndpoint(shape);
        await using ServiceProvider services = Build(Environments.Production, AuthorityWithClient(), workload: "eventstore", tokenEndpoint);
        IWorkloadAssertionIssuer issuer = services.GetRequiredService<IWorkloadAssertionIssuer>();
        var request = new WorkloadAssertionRequest("sample", EventStoreWorkloadOperations.DomainServiceProcess);

        string? first = await issuer.IssueAsync(request, TestContext.Current.CancellationToken);
        string? second = await issuer.IssueAsync(request, TestContext.Current.CancellationToken);

        first.ShouldBeNull(shape);
        second.ShouldBeNull(shape);
        tokenEndpoint.Scopes.Count.ShouldBe(2, shape);
    }

    /// <summary>The token scope check accepts exactly one audience and one operation, in string or array form.</summary>
    [Fact]
    public void GetTokenScopeFailure_AcceptsOnlyTheExactPair()
    {
        JwtWorkloadAssertionIssuer.GetTokenScopeFailure(ScopedWorkloadTokenEndpoint.UnsignedToken(["sample"], new[] { "domain-service:process" }), "sample", "domain-service:process").ShouldBeNull();
        JwtWorkloadAssertionIssuer.GetTokenScopeFailure(ScopedWorkloadTokenEndpoint.UnsignedToken(["sample"], "domain-service:process"), "sample", "domain-service:process").ShouldBeNull();
        JwtWorkloadAssertionIssuer.GetTokenScopeFailure(ScopedWorkloadTokenEndpoint.UnsignedToken(["sample", "tenants"], "domain-service:process"), "sample", "domain-service:process")
            .ShouldBe("token-audience-mismatch");
        JwtWorkloadAssertionIssuer.GetTokenScopeFailure(ScopedWorkloadTokenEndpoint.UnsignedToken(["sample"], "domain-service:process domain-service:query"), "sample", "domain-service:process")
            .ShouldBe("token-operation-mismatch");
        JwtWorkloadAssertionIssuer.GetTokenScopeFailure("not-a-jwt", "sample", "domain-service:process").ShouldBe("token-malformed");
    }

    /// <summary>
    /// Story 5.5 (P-6): a token endpoint discovered from the authority metadata is cached only after it passed
    /// validation; an invalid discovery is retried on the next request instead of poisoning the issuer until restart.
    /// </summary>
    [Fact]
    public async Task AuthorityContract_CachesADiscoveredTokenEndpointOnlyAfterValidation()
    {
        Dictionary<string, string?> settings = AuthorityWithClient();
        settings["Authentication:WorkloadIssuer:TokenEndpoint"] = null;
        var authority = new DiscoveringAuthority("http://identity.example.test/realms/hexalith/protocol/openid-connect/token");
        await using ServiceProvider services = Build(Environments.Production, settings, workload: "eventstore", authority);
        IWorkloadAssertionIssuer issuer = services.GetRequiredService<IWorkloadAssertionIssuer>();
        var request = new WorkloadAssertionRequest("sample", EventStoreWorkloadOperations.DomainServiceProcess);

        (await issuer.IssueAsync(request, TestContext.Current.CancellationToken)).ShouldBeNull();
        authority.DiscoveredTokenEndpoint = TokenEndpoint;
        string? assertion = await issuer.IssueAsync(request, TestContext.Current.CancellationToken);
        string? other = await issuer.IssueAsync(new WorkloadAssertionRequest("tenants", EventStoreWorkloadOperations.DomainServiceQuery), TestContext.Current.CancellationToken);

        AssertExactlyScoped(assertion, "sample", EventStoreWorkloadOperations.DomainServiceProcess);
        AssertExactlyScoped(other, "tenants", EventStoreWorkloadOperations.DomainServiceQuery);
        authority.MetadataRequests.ShouldBe(2);
        authority.TokenRequests.ShouldBe(2);
    }

    /// <summary>Story 5.5 (P-5): authority mode without the workload client fails options validation, naming settings only.</summary>
    /// <param name="mode">The contract mode and client state.</param>
    /// <param name="expectedValid">Whether validation succeeds.</param>
    [Theory]
    [InlineData("authority-with-client", true)]
    [InlineData("authority-without-secret", false)]
    [InlineData("authority-without-client-id", false)]
    [InlineData("authority-insecure-endpoint", false)]
    [InlineData("symmetric-without-client", true)]
    public void ClientCredentialsRequirement_FailsAuthorityModeWithoutAClient(string mode, bool expectedValid)
    {
        ArgumentNullException.ThrowIfNull(mode);
        Dictionary<string, string?> settings = mode.StartsWith("authority", StringComparison.Ordinal) ? AuthorityWithClient() : Symmetric();
        string? configuredValue = settings.GetValueOrDefault("Authentication:WorkloadIssuer:ClientSecret");
        switch (mode)
        {
            case "authority-without-secret":
                settings["Authentication:WorkloadIssuer:ClientSecret"] = null;
                break;
            case "authority-without-client-id":
                settings["Authentication:WorkloadIssuer:ClientId"] = null;
                break;
            case "authority-insecure-endpoint":
                settings["Authentication:WorkloadIssuer:TokenEndpoint"] = "http://identity.example.test/token";
                break;
        }

        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        _ = services.AddSingleton(environment);
        _ = services.AddEventStoreWorkloadAssertionIssuer().RequireEventStoreWorkloadIssuerClientCredentials();
        using ServiceProvider provider = services.BuildServiceProvider();

        if (expectedValid)
        {
            _ = provider.GetRequiredService<IOptions<WorkloadAssertionIssuerOptions>>().Value;
            return;
        }

        OptionsValidationException failure = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<WorkloadAssertionIssuerOptions>>().Value);
        failure.Message.ShouldContain("Authentication:WorkloadIssuer:");
        if (configuredValue is not null)
        {
            failure.Message.ShouldNotContain(configuredValue);
        }
    }

    /// <summary>
    /// Authority mode cannot embed per-request bindings: a bound request is refused before any token request, so an
    /// unbound, replayable assertion never leaves the issuer.
    /// </summary>
    [Fact]
    public async Task AuthorityContract_RefusesBoundRequests()
    {
        var tokenEndpoint = new TokenEndpointHandler(HttpStatusCode.OK, "{\"access_token\":\"authority-token\",\"expires_in\":300}");
        await using ServiceProvider services = Build(Environments.Production, AuthorityWithClient(), workload: "eventstore", tokenEndpoint);
        IWorkloadAssertionIssuer issuer = services.GetRequiredService<IWorkloadAssertionIssuer>();

        string? assertion = await issuer.IssueAsync(
            new WorkloadAssertionRequest(
                "eventstore",
                EventStoreWorkloadOperations.ProjectionNotify,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = "acme",
                }),
            TestContext.Current.CancellationToken);

        assertion.ShouldBeNull();
        tokenEndpoint.Calls.ShouldBe(0);
        issuer.CanBindResources.ShouldBeFalse();
    }

    /// <summary>Only the symmetric issuer the shared contract admits (Development) can bind resources.</summary>
    /// <param name="environmentName">The host environment.</param>
    /// <param name="authority">Whether the contract names an OIDC authority instead of a signing key.</param>
    /// <param name="expected">The expected binding capability.</param>
    [Theory]
    [InlineData("Development", false, true)]
    [InlineData("Production", false, false)]
    [InlineData("Production", true, false)]
    [InlineData("Development", true, false)]
    public async Task CanBindResources_OnlyForAnAdmittedSigningKey(string environmentName, bool authority, bool expected)
    {
        Dictionary<string, string?> settings = authority ? AuthorityWithClient() : Symmetric();
        if (!authority)
        {
            settings["Authentication:JwtBearer:AllowInsecureSymmetricKey"] = "true";
        }

        await using ServiceProvider services = Build(environmentName, settings, workload: "eventstore");

        services.GetRequiredService<IWorkloadAssertionIssuer>().CanBindResources.ShouldBe(expected);
    }

    /// <summary>Missing client credentials, a failing token endpoint, or an insecure endpoint issue nothing.</summary>
    /// <param name="scenario">The failure scenario.</param>
    [Theory]
    [InlineData("missing-secret")]
    [InlineData("endpoint-failure")]
    [InlineData("insecure-endpoint")]
    [InlineData("invalid-response")]
    public async Task AuthorityContract_FailsClosed(string scenario)
    {
        Dictionary<string, string?> settings = AuthorityWithClient();
        var tokenEndpoint = new TokenEndpointHandler(HttpStatusCode.OK, "{\"access_token\":\"authority-token\",\"expires_in\":300}");
        switch (scenario)
        {
            case "missing-secret":
                settings["Authentication:WorkloadIssuer:ClientSecret"] = null;
                break;
            case "endpoint-failure":
                tokenEndpoint = new TokenEndpointHandler(HttpStatusCode.Unauthorized, "{}");
                break;
            case "insecure-endpoint":
                settings["Authentication:WorkloadIssuer:TokenEndpoint"] = "http://identity.example.test/token";
                break;
            case "invalid-response":
                tokenEndpoint = new TokenEndpointHandler(HttpStatusCode.OK, "{\"token_type\":\"Bearer\"}");
                break;
        }

        await using ServiceProvider services = Build(Environments.Production, settings, workload: "eventstore", tokenEndpoint);

        string? assertion = await services.GetRequiredService<IWorkloadAssertionIssuer>().IssueAsync(
            new WorkloadAssertionRequest("sample", EventStoreWorkloadOperations.DomainServiceProcess),
            TestContext.Current.CancellationToken);

        assertion.ShouldBeNull(scenario);
    }

    private static Dictionary<string, string?> Symmetric()
        => new(StringComparer.Ordinal)
        {
            ["Authentication:JwtBearer:Issuer"] = "hexalith-dev",
            ["Authentication:JwtBearer:Audience"] = "hexalith-eventstore",
            ["Authentication:JwtBearer:SigningKey"] = AuthenticationTestEnvironment.SigningKey,
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256",
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
        };

    private static Dictionary<string, string?> AuthorityWithClient()
        => new(StringComparer.Ordinal)
        {
            ["Authentication:JwtBearer:Authority"] = Authority,
            ["Authentication:JwtBearer:Issuer"] = Authority,
            ["Authentication:JwtBearer:Audience"] = "hexalith-eventstore",
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "RS256",
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "true",
            ["Authentication:WorkloadIssuer:ClientId"] = "eventstore",
            ["Authentication:WorkloadIssuer:ClientSecret"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            ["Authentication:WorkloadIssuer:TokenEndpoint"] = TokenEndpoint,
        };

    private static ServiceProvider Build(
        string environmentName,
        Dictionary<string, string?> settings,
        string workload,
        HttpMessageHandler? tokenEndpoint = null,
        TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        if (timeProvider is not null)
        {
            // The dedicated security clock, registered before the issuer, whose TryAdd then keeps it.
            _ = services.AddSingleton(new WorkloadSecurityClock(timeProvider));
        }

        _ = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        _ = services.AddSingleton(environment);
        _ = services.AddEventStoreWorkloadAssertionIssuer(options => options.Workload ??= workload);
        if (tokenEndpoint is not null)
        {
            _ = services.AddHttpClient(JwtWorkloadAssertionIssuer.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => tokenEndpoint);
        }

        return services.BuildServiceProvider();
    }

    private static void AssertExactlyScoped(string? assertion, string audience, string operation)
    {
        ScopedWorkloadTokenEndpoint.AssertExactlyScoped(assertion, audience, operation);
    }

    /// <summary>An authority that publishes metadata with a configurable token endpoint and mints exact tokens.</summary>
    private sealed class DiscoveringAuthority(string discoveredTokenEndpoint) : HttpMessageHandler
    {
        private readonly ScopedWorkloadTokenEndpoint _tokens = new();

        public string DiscoveredTokenEndpoint { get; set; } = discoveredTokenEndpoint;

        public int MetadataRequests { get; private set; }

        public int TokenRequests { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/.well-known/openid-configuration", StringComparison.Ordinal))
            {
                MetadataRequests++;
                string metadata = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["issuer"] = Authority,
                    ["token_endpoint"] = DiscoveredTokenEndpoint,
                });
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(metadata, Encoding.UTF8, "application/json") };
            }

            TokenRequests++;
            using var invoker = new HttpMessageInvoker(_tokens, disposeHandler: false);
            return await invoker.SendAsync(request, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tokens.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class TokenEndpointHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public string LastBody { get; private set; } = string.Empty;

        public Uri? LastUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastUri = request.RequestUri;
            LastBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
