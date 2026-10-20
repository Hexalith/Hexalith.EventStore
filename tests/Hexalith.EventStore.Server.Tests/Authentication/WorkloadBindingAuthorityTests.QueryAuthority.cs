using System.Security.Claims;
using System.Net;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authentication;

public sealed partial class WorkloadBindingAuthorityTests
{
    [Fact]
    public async Task CapabilityAllowsChangingDigestsAndPreservesOtherExactBoundOperations()
    {
        ConfigureQueryGrant();
        _options.Grants[0].Operations = [EventStoreWorkloadOperations.DomainServiceQuery, EventStoreWorkloadOperations.GatewayStreamRead];
        string unchangedGrant = JsonSerializer.Serialize(_options.Grants[0]);
        QueryEnvelope first = QueryAuthorityEnvelope();
        QueryEnvelope second = first with { Payload = "[]"u8.ToArray(), CorrelationId = "second-query" };
        var firstDetails = new WorkloadAuthorizationDetails { Audience = "timesheets", Operation = EventStoreWorkloadOperations.DomainServiceQuery,
            Bindings = new(QueryCallerAuthorityBinding.Bind(first)) };
        var secondDetails = firstDetails with { Bindings = new(QueryCallerAuthorityBinding.Bind(second)) };
        firstDetails.Bindings[QueryCallerAuthorityBinding.DigestClaim].ShouldNotBe(secondDetails.Bindings[QueryCallerAuthorityBinding.DigestClaim]);
        string firstToken = _authority.Issue("synthetic-client", _secret, Scope(firstDetails), firstDetails).ShouldNotBeNull();
        string secondToken = _authority.Issue("synthetic-client", _secret, Scope(secondDetails), secondDetails).ShouldNotBeNull();
        IDomainQueryHandler handler = Substitute.For<IDomainQueryHandler>();
        handler.Domain.Returns(first.Domain); handler.QueryType.Returns(first.QueryType);
        handler.ExecuteAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>()).Returns(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { })));
        (await DispatchQueryAssertionAsync(first, firstToken, handler)).Success.ShouldBeTrue();
        (await DispatchQueryAssertionAsync(second, secondToken, handler)).Success.ShouldBeTrue();
        (await DispatchQueryAssertionAsync(second, firstToken, handler)).ErrorMessage.ShouldBe("safe-denial");

        var ordinary = firstDetails with { Operation = EventStoreWorkloadOperations.GatewayStreamRead,
            Bindings = firstDetails.Bindings.Where(pair => pair.Key != QueryCallerAuthorityBinding.DigestClaim)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal) };
        string ordinaryToken = _authority.Issue("synthetic-client", _secret, Scope(ordinary), ordinary).ShouldNotBeNull();
        (await DispatchQueryAssertionAsync(first, ordinaryToken, handler)).ErrorMessage.ShouldBe("safe-denial");
        var borrowedDigest = firstDetails with { Operation = EventStoreWorkloadOperations.GatewayStreamRead };
        _authority.Issue("synthetic-client", _secret, Scope(borrowedDigest), borrowedDigest).ShouldBeNull();
        ordinary.Bindings[EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = "other";
        _authority.Issue("synthetic-client", _secret, Scope(ordinary), ordinary).ShouldBeNull();
        await handler.Received(2).ExecuteAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
        JsonSerializer.Serialize(_options.Grants[0]).ShouldBe(unchangedGrant);
    }

    [Theory]
    [InlineData("eventstore:bound-tenant")]
    [InlineData("eventstore:bound-domain")]
    [InlineData("eventstore:bound-query-authority-v1")]
    public async Task DuplicateRawQueryBindingKeysAreRefusedBeforeIssuance(string binding)
    {
        ConfigureQueryGrant();
        IReadOnlyDictionary<string, string> bindings = QueryCallerAuthorityBinding.Bind(QueryAuthorityEnvelope());
        string duplicate = JsonSerializer.Serialize(binding) + ":" + JsonSerializer.Serialize(bindings[binding]) + ",";
        string json = WorkloadAuthorizationDetails.Encode(new("timesheets", EventStoreWorkloadOperations.DomainServiceQuery, bindings))
            .Replace("\"bindings\":{", "\"bindings\":{" + duplicate, StringComparison.Ordinal);
        WorkloadAuthorizationDetails.Decode(json).ShouldBeNull();
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.WebHost.UseTestServer(); builder.Logging.ClearProviders(); builder.Services.AddSingleton(_authority);
        await using WebApplication app = builder.Build();
        app.MapEventStoreWorkloadBindingAuthority();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient(); client.BaseAddress = new Uri("https://authority.example.test");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials", ["client_id"] = "synthetic-client", ["client_secret"] = _secret,
            ["scope"] = new WorkloadAssertionIssuerOptions().BuildScope("timesheets", EventStoreWorkloadOperations.DomainServiceQuery),
            ["authorization_details"] = json,
        });
        using HttpResponseMessage response = await client.PostAsync("/workload-authority/token", form, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldNotContain("access_token");
    }

    [Theory]
    [InlineData("mismatched-actor")]
    [InlineData("unsupported-origin")]
    public async Task CorrectlyAttestedInvalidOriginContextIsRefusedBeforeProtectedHandler(string fault)
    {
        ConfigureQueryGrant();
        QueryEnvelope query = QueryAuthorityEnvelope() with
        {
            IsDelegated = true,
            CallerAuthority = new(fault == "unsupported-origin" ? "authenticated-caller" : "delegated-owner-query", [], ["read_metadata"])
            {
                OriginatingContext = new(fault == "mismatched-actor" ? "other" : "actor", "original-client", true, "delegation", null, null),
            },
        };
        var details = new WorkloadAuthorizationDetails { Audience = "timesheets", Operation = EventStoreWorkloadOperations.DomainServiceQuery,
            Bindings = new(QueryCallerAuthorityBinding.Bind(query)) };
        string token = _authority.Issue("synthetic-client", _secret, Scope(details), details).ShouldNotBeNull();
        IDomainQueryHandler handler = Substitute.For<IDomainQueryHandler>();
        handler.Domain.Returns(query.Domain); handler.QueryType.Returns(query.QueryType);
        (await DispatchQueryAssertionAsync(query, token, handler)).ErrorMessage.ShouldBe("safe-denial");
        await handler.DidNotReceive().ExecuteAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProductionQueryAuthorityRunsThroughActualIssuerAndReceivingVerifierAndStopsNewIssuanceOnRevocation()
    {
        var originating = new OriginatingQueryContext("actor", "original-client", true, "delegation-1", ["read", "resolve"], ["public-gateway"]);
        QueryEnvelope query = QueryAuthorityEnvelope() with
        {
            AuthenticatedWorkloadId = "projects", Audience = ["eventstore"], IsDelegated = true,
            CallerAuthority = new("delegated-owner-query", [], ["read_metadata"]) { OriginatingContext = originating },
        };
        ConfigureQueryGrant();
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:WorkloadJwtBearer:Authority"] = _options.Issuer,
            ["Authentication:WorkloadJwtBearer:Issuer"] = _options.Issuer,
            ["Authentication:WorkloadJwtBearer:Audience"] = "timesheets",
            ["Authentication:WorkloadJwtBearer:AllowedAlgorithms:0"] = "RS256",
            ["Authentication:WorkloadIssuer:ClientId"] = "synthetic-client",
            ["Authentication:WorkloadIssuer:ClientSecret"] = _secret,
            ["Authentication:WorkloadIssuer:Workload"] = "eventstore",
            ["Authentication:WorkloadIssuer:EnableResourceBindingRequests"] = "true",
            ["Authentication:WorkloadIssuer:TokenEndpoint"] = _options.Issuer + "/token",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging(); services.AddSingleton(configuration);
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production); services.AddSingleton(environment);
        services.AddAuthentication().AddEventStoreWorkloadScheme("query-receiver", "SyntheticReceiver", (options, _) =>
        { options.Audience = "timesheets"; options.AllowedCallers = ["eventstore"]; });
        services.AddEventStoreWorkloadAssertionIssuer();
        services.AddHttpClient(JwtWorkloadAssertionIssuer.HttpClientName).ConfigurePrimaryHttpMessageHandler(() =>
            new SyntheticBindingAuthorityHandler(_authority, "exact"));
        await using ServiceProvider provider = services.BuildServiceProvider();
        JwtBearerOptions bearer = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("query-receiver");
        var metadata = new OpenIdConnectConfiguration { Issuer = _options.Issuer };
        foreach (SecurityKey key in new JsonWebKeySet(JsonSerializer.Serialize(_authority.GetPublicKeys())).GetSigningKeys()) metadata.SigningKeys.Add(key);
        bearer.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
        IWorkloadAssertionIssuer issuer = provider.GetRequiredService<IWorkloadAssertionIssuer>();
        var request = new WorkloadAssertionRequest("timesheets", EventStoreWorkloadOperations.DomainServiceQuery, QueryCallerAuthorityBinding.Bind(query));
        string token = (await issuer.IssueAsync(request, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        new JsonWebToken(token).Alg.ShouldBe("RS256");
        WorkloadAssertionValidator receiver = provider.GetRequiredService<WorkloadAssertionValidator>();
        WorkloadAssertionEvaluation accepted = await receiver.ValidateAsync("query-receiver", token, EventStoreWorkloadOperations.DomainServiceQuery, null, TestContext.Current.CancellationToken);
        accepted.Succeeded.ShouldBeTrue();
        QueryCallerAuthorityBinding.Verify(query, accepted.Principal!, "eventstore").ShouldBeTrue();
        var context = new DefaultHttpContext { User = accepted.Principal! };
        var accessor = new HttpContextAccessor { HttpContext = context };
        int protectedReads = 0;
        bool currentlyAllowed = true;
        IDomainQueryHandler handler = Substitute.For<IDomainQueryHandler>();
        handler.Domain.Returns(query.Domain); handler.QueryType.Returns(query.QueryType);
        handler.ExecuteAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            VerifiedQueryCaller verified = VerifiedQueryCaller.Get(context).ShouldNotBeNull();
            verified.OriginatingContext.ShouldBe(originating);
            verified.Query.AuthenticatedWorkloadId.ShouldBe("projects"); verified.Query.Audience.ShouldBe(["eventstore"]);
            verified.Query.Scopes.ShouldBeNull(); verified.Query.DelegationId.ShouldBeNull();
            context.User.HasClaim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "eventstore").ShouldBeTrue();
            context.User.HasClaim("aud", "public-gateway").ShouldBeFalse();
            if (!currentlyAllowed) return QueryResult.Failure("safe-denial");
            protectedReads++;
            return QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { allowed = true }));
        });
        await using ServiceProvider owner = new ServiceCollection().AddSingleton<IHttpContextAccessor>(accessor).AddSingleton(handler).BuildServiceProvider();
        // Exercise the serialized contract, rather than passing only the producer's in-memory object.
        QueryEnvelope received = JsonSerializer.Deserialize<QueryEnvelope>(JsonSerializer.Serialize(query, JsonSerializerOptions.Web), JsonSerializerOptions.Web)!;
        (await DomainQueryDispatcher.ExecuteAsync(owner, received, TestContext.Current.CancellationToken)).Success.ShouldBeTrue();
        VerifiedQueryCaller.Get(context).ShouldBeNull();
        currentlyAllowed = false;
        (await DomainQueryDispatcher.ExecuteAsync(owner, received, TestContext.Current.CancellationToken)).ErrorMessage.ShouldBe("safe-denial");
        protectedReads.ShouldBe(1);
        QueryEnvelope forged = received with { CallerAuthority = received.CallerAuthority! with { OriginatingContext = originating with { DelegationId = "forged" } } };
        (await DomainQueryDispatcher.ExecuteAsync(owner, forged, TestContext.Current.CancellationToken)).ErrorMessage.ShouldBe("safe-denial");
        await handler.Received(2).ExecuteAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
        VerifiedQueryCaller.Get(context).ShouldBeNull();
        _options.Grants[0].Workload = "other";
        (await issuer.IssueAsync(request, TestContext.Current.CancellationToken)).ShouldBeNull();
        _options.Grants[0].Workload = "eventstore";
        (await receiver.ValidateForAudienceAsync("query-receiver", token, EventStoreWorkloadOperations.DomainServiceQuery,
            ["eventstore"], "other", TestContext.Current.CancellationToken)).Succeeded.ShouldBeFalse();
        (await receiver.ValidateAsync("query-receiver", token, EventStoreWorkloadOperations.DomainServiceProcess,
            null, TestContext.Current.CancellationToken)).Succeeded.ShouldBeFalse();
        string[] parts = token.Split('.');
        parts[2] = (parts[2][0] == 'A' ? "B" : "A") + parts[2][1..];
        (await receiver.ValidateAsync("query-receiver", string.Join('.', parts), EventStoreWorkloadOperations.DomainServiceQuery,
            null, TestContext.Current.CancellationToken)).Succeeded.ShouldBeFalse();
        DateTimeOffset earlier = DateTimeOffset.UtcNow.AddMinutes(-10);
        _options.Grants[0].NotBefore = earlier.AddMinutes(-1);
        IOptionsMonitor<WorkloadAuthorityOptions> expiredMonitor = Substitute.For<IOptionsMonitor<WorkloadAuthorityOptions>>();
        expiredMonitor.CurrentValue.Returns(_options);
        var earlierAuthority = new WorkloadBindingAuthority(expiredMonitor, environment, new FakeTimeProvider(earlier));
        var earlierDetails = new WorkloadAuthorizationDetails { Audience = request.Audience, Operation = request.Operation, Bindings = new(request.Bindings!) };
        string expiredToken = earlierAuthority.Issue("synthetic-client", _secret, Scope(earlierDetails), earlierDetails).ShouldNotBeNull();
        (await receiver.ValidateAsync("query-receiver", expiredToken, EventStoreWorkloadOperations.DomainServiceQuery,
            null, TestContext.Current.CancellationToken)).Succeeded.ShouldBeFalse();
        _options.Grants[0].Revoked = true;
        (await issuer.IssueAsync(request, TestContext.Current.CancellationToken)).ShouldBeNull();
        // Revoking a loaded grant stops issuance; it does not retroactively invalidate an existing short-lived JWT.
        (await receiver.ValidateAsync("query-receiver", token, EventStoreWorkloadOperations.DomainServiceQuery,
            null, TestContext.Current.CancellationToken)).Succeeded.ShouldBeTrue();
        (new JsonWebToken(token).ValidTo - new JsonWebToken(token).IssuedAt).ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(120));
    }

    [Theory]
    [InlineData("default-off")]
    [InlineData("wrong-profile")]
    [InlineData("unbound")]
    [InlineData("unknown-key")]
    [InlineData("missing-tenant")]
    [InlineData("missing-domain")]
    [InlineData("missing-digest")]
    [InlineData("lowercase")]
    [InlineData("non-hex")]
    [InlineData("short")]
    [InlineData("long")]
    [InlineData("wildcard-request")]
    [InlineData("wildcard-grant")]
    [InlineData("extra-grant-key")]
    [InlineData("wrong-tenant")]
    [InlineData("wrong-domain")]
    [InlineData("wrong-audience")]
    [InlineData("wrong-operation")]
    [InlineData("wrong-client")]
    [InlineData("wrong-secret")]
    [InlineData("revoked")]
    [InlineData("expired")]
    public void QueryDigestCapabilityCannotWidenAnExactGrant(string fault)
    {
        ConfigureQueryGrant();
        var details = new WorkloadAuthorizationDetails { Audience = "timesheets", Operation = EventStoreWorkloadOperations.DomainServiceQuery,
            Bindings = new(QueryCallerAuthorityBinding.Bind(QueryAuthorityEnvelope())) };
        string client = "synthetic-client", secret = _secret;
        switch (fault)
        {
            case "default-off": _options.Grants[0].AllowQueryAuthorityDigest = false; break;
            case "wrong-profile": details = details with { Type = "unknown" }; break;
            case "unbound": details.Bindings.Clear(); _options.Grants[0].AllowUnbound = true; break;
            case "unknown-key": details.Bindings["eventstore:bound-unknown"] = "x"; break;
            case "missing-tenant": details.Bindings.Remove(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType); break;
            case "missing-domain": details.Bindings.Remove(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType); break;
            case "missing-digest": details.Bindings.Remove(QueryCallerAuthorityBinding.DigestClaim); break;
            case "lowercase": details.Bindings[QueryCallerAuthorityBinding.DigestClaim] = new string('a', 64); break;
            case "non-hex": details.Bindings[QueryCallerAuthorityBinding.DigestClaim] = new string('G', 64); break;
            case "short": details.Bindings[QueryCallerAuthorityBinding.DigestClaim] = new string('A', 63); break;
            case "long": details.Bindings[QueryCallerAuthorityBinding.DigestClaim] = new string('A', 65); break;
            case "wildcard-request": details.Bindings[EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = "*"; break;
            case "wildcard-grant": _options.Grants[0].Bindings[EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = ["synthetic-tenant", "*"]; break;
            case "extra-grant-key": _options.Grants[0].Bindings[EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType] = ["actor"]; break;
            case "wrong-tenant": details.Bindings[EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = "other"; break;
            case "wrong-domain": details.Bindings[EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = "other"; break;
            case "wrong-audience": details = details with { Audience = "other" }; break;
            case "wrong-operation": details = details with { Operation = "gateway:query" }; break;
            case "wrong-client": client = "other"; break;
            case "wrong-secret": secret = new string('x', 64); break;
            case "revoked": _options.Grants[0].Revoked = true; break;
            case "expired": _options.Grants[0].ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1); break;
        }
        _authority.Issue(client, secret, Scope(details), details).ShouldBeNull();
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("workload")]
    [InlineData("delegation")]
    [InlineData("delegated")]
    [InlineData("scopes")]
    [InlineData("audience")]
    [InlineData("tenant")]
    [InlineData("domain")]
    [InlineData("aggregate")]
    [InlineData("payload")]
    [InlineData("origin-context")]
    [InlineData("extra-binding")]
    [InlineData("missing-binding")]
    [InlineData("wrong-binding")]
    [InlineData("wrong-workload")]
    [InlineData("wrong-operation")]
    public void ExactQueryBindingRejectsEveryChangedIdentityResourceOrContext(string fault)
    {
        QueryEnvelope original = QueryAuthorityEnvelope();
        QueryEnvelope changed = fault switch
        {
            "actor" => original with { OriginalActorId = "other", UserId = "other" },
            "workload" => original with { AuthenticatedWorkloadId = "other" },
            "delegation" => original with { DelegationId = "other" },
            "delegated" => original with { IsDelegated = true },
            "scopes" => original with { Scopes = ["other"] },
            "audience" => original with { Audience = ["other"] },
            "tenant" => original with { TenantId = "other" },
            "domain" => original with { Domain = "other" },
            "aggregate" => original with { AggregateId = "other" },
            "payload" => original with { Payload = "[]"u8.ToArray() },
            "origin-context" => original with { CallerAuthority = original.CallerAuthority! with { OriginatingContext = new("actor", "other", true, "delegation", null, null) } },
            _ => original,
        };
        List<Claim> claims = [new(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, fault == "wrong-workload" ? "other" : "eventstore"),
            new(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, fault == "wrong-operation" ? "gateway:query" : EventStoreWorkloadOperations.DomainServiceQuery),
            .. QueryCallerAuthorityBinding.Bind(original).Select(pair => new Claim(pair.Key, pair.Value))];
        if (fault == "extra-binding") claims.Add(new("eventstore:bound-unknown", "x"));
        if (fault == "missing-binding") claims.RemoveAll(claim => claim.Type == EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType);
        if (fault == "wrong-binding")
        {
            claims.RemoveAll(claim => claim.Type == EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType);
            claims.Add(new(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType, "other"));
        }
        QueryCallerAuthorityBinding.Verify(changed, new ClaimsPrincipal(new ClaimsIdentity(claims, "verified-workload")), "eventstore").ShouldBeFalse();
    }

    private void ConfigureQueryGrant()
    {
        WorkloadAuthorityGrant grant = _options.Grants[0];
        grant.Workload = "eventstore"; grant.Audience = "timesheets";
        grant.Operations = [EventStoreWorkloadOperations.DomainServiceQuery]; grant.AllowQueryAuthorityDigest = true;
        grant.Bindings[EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = ["synthetic-tenant"];
    }

    private async Task<QueryResult> DispatchQueryAssertionAsync(QueryEnvelope query, string token, IDomainQueryHandler handler)
    {
        TokenValidationResult signed = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer, ValidAudience = "timesheets", ValidAlgorithms = ["RS256"],
            IssuerSigningKeys = new JsonWebKeySet(JsonSerializer.Serialize(_authority.GetPublicKeys())).GetSigningKeys(),
            RequireSignedTokens = true, ValidateIssuerSigningKey = true, ClockSkew = TimeSpan.Zero,
        });
        signed.IsValid.ShouldBeTrue();
        WorkloadAssertionEvaluation transport = WorkloadAssertionEvaluator.Evaluate(new ClaimsPrincipal(signed.ClaimsIdentity), "verified-query",
            new WorkloadAuthenticationOptions { Audience = "timesheets", AllowedCallers = ["eventstore"] }, null,
            StringValues.Empty, DateTimeOffset.UtcNow, TimeSpan.Zero);
        transport.Succeeded.ShouldBeTrue();
        var http = new DefaultHttpContext { User = transport.Principal! };
        await using ServiceProvider services = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = http }).AddSingleton(handler).BuildServiceProvider();
        QueryResult result = await DomainQueryDispatcher.ExecuteAsync(services, query, TestContext.Current.CancellationToken);
        VerifiedQueryCaller.Get(http).ShouldBeNull();
        return result;
    }

    private static QueryEnvelope QueryAuthorityEnvelope() => new("synthetic-tenant", "timesheets", "aggregate", "Read", "{}"u8.ToArray(), "corr", "actor")
        { OriginalActorId = "actor", AuthenticatedWorkloadId = "user-client", CallerAuthority = new("authenticated-caller", [], ["read_metadata"]) };
}
