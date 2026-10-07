using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.DomainService.Tests.Fixtures;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>
/// Story 5.5 (FR28, NFR1): the canonical domain-service host, through its real HTTP pipeline, admits operational
/// routes only with the Dapr app-channel token plus a short-lived EventStore workload assertion for exactly this
/// audience and operation; forged headers and wire administrator flags grant nothing and cause zero domain work.
/// </summary>
public sealed class DomainServiceTrustBoundaryTests
{
    private const string AppId = "sample";
    private const string Caller = "eventstore";
    private const string ChannelToken = "story-5-5-domain-channel-token";
    private const string Issuer = "hexalith-dev";
    private const string AuthorityIssuer = "https://identity.example.test/realms/hexalith";
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>A valid assertion for the route's operation reaches the domain processor exactly once.</summary>
    [Fact]
    public async Task ValidWorkloadAssertion_ProcessesTheCommand()
    {
        var processor = new CapturingWidgetProcessor();
        await using WebApplication app = await StartAsync(processor: processor);
        using HttpRequestMessage request = ProcessRequest(Assertion([EventStoreWorkloadOperations.DomainServiceProcess]));

        using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        processor.Commands.ShouldHaveSingleItem().CommandType.ShouldBe(nameof(CreateWidget));
    }

    /// <summary>
    /// A domain-service host that replaces <see cref="TimeProvider"/> with a clock frozen far in the past still admits
    /// a current assertion through the real pipeline: workload-assertion security keeps its own wall clock.
    /// </summary>
    [Fact]
    public async Task HostTimeProviderFrozenInThePast_StillAdmitsACurrentAssertion()
    {
        var processor = new CapturingWidgetProcessor();
        await using WebApplication app = await StartAsync(
            processor: processor,
            configureServices: services => services.AddSingleton<TimeProvider>(new FrozenTimeProvider(new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero))));
        using HttpRequestMessage request = ProcessRequest(Assertion([EventStoreWorkloadOperations.DomainServiceProcess]));

        using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _ = processor.Commands.ShouldHaveSingleItem();
    }

    /// <summary>
    /// Every absent, forged, wrong, stale, duplicate, conflicting, or channel-less credential is denied before the
    /// domain processor runs, with bounded reason and correlation telemetry and no token in any log.
    /// </summary>
    /// <param name="scenario">The denial scenario.</param>
    /// <param name="expectedStatus">The expected bounded status.</param>
    /// <param name="expectedReason">The expected bounded reason.</param>
    [Theory]
    [InlineData("no-credentials", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.ChannelTokenMissing)]
    [InlineData("forged-caller-header", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.AssertionMissing)]
    [InlineData("missing-channel-token", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.ChannelTokenMissing)]
    [InlineData("wrong-channel-token", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.ChannelTokenInvalid)]
    [InlineData("duplicate-channel-token", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.ChannelTokenDuplicate)]
    [InlineData("wrong-operation", HttpStatusCode.Forbidden, WorkloadAuthenticationReasons.OperationNotGranted)]
    [InlineData("wrong-audience", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.AudienceInvalid)]
    [InlineData("wrong-caller", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.CallerNotAllowed)]
    [InlineData("conflicting-caller", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.CallerConflict)]
    [InlineData("expired", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.AssertionExpired)]
    [InlineData("long-lived", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.AssertionStale)]
    [InlineData("wrong-key", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.SignatureInvalid)]
    [InlineData("duplicate-assertion", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.AssertionDuplicate)]
    [InlineData("human-bearer", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.CredentialConflict)]
    public async Task InvalidCredential_IsDeniedBeforeDomainWork(string scenario, HttpStatusCode expectedStatus, string expectedReason)
    {
        var processor = new CapturingWidgetProcessor();
        var logs = new CapturingLoggerProvider();
        await using WebApplication app = await StartAsync(processor: processor, logs: logs);
        string valid = Assertion([EventStoreWorkloadOperations.DomainServiceProcess]);
        string? assertion = scenario switch
        {
            "no-credentials" or "forged-caller-header" => null,
            "wrong-operation" => Assertion([EventStoreWorkloadOperations.DomainServiceQuery]),
            "wrong-audience" => Assertion([EventStoreWorkloadOperations.DomainServiceProcess], audience: "tenants"),
            "wrong-caller" => Assertion([EventStoreWorkloadOperations.DomainServiceProcess], caller: "sample-api"),
            "expired" => Assertion([EventStoreWorkloadOperations.DomainServiceProcess], issuedAt: DateTime.UtcNow.AddMinutes(-10)),
            "long-lived" => Assertion([EventStoreWorkloadOperations.DomainServiceProcess], lifetime: TimeSpan.FromHours(1)),
            "wrong-key" => Assertion([EventStoreWorkloadOperations.DomainServiceProcess], signingKey: Convert.ToBase64String(new byte[32])),
            _ => valid,
        };
        using HttpRequestMessage request = ProcessRequest(
            assertion,
            channelTokens: scenario switch
            {
                "no-credentials" or "missing-channel-token" => [],
                "wrong-channel-token" => ["wrong-channel-token"],
                "duplicate-channel-token" => [ChannelToken, ChannelToken],
                _ => [ChannelToken],
            },
            adminFlag: true);
        switch (scenario)
        {
            case "forged-caller-header":
                request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.DaprCallerHeaderName, Caller);
                break;
            case "conflicting-caller":
                request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.DaprCallerHeaderName, "sample-api");
                break;
            case "duplicate-assertion":
                request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, valid);
                break;
            case "human-bearer":
                request.Headers.Add("Authorization", "Bearer " + valid);
                break;
        }

        using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expectedStatus, scenario);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty(scenario);
        processor.Commands.ShouldBeEmpty(scenario);
        string denial = logs.Messages
            .Where(static message => message.Contains("InternalAuthenticationDenied", StringComparison.Ordinal))
            .ShouldHaveSingleItem(scenario);
        denial.ShouldContain($"Reason={expectedReason}", Case.Sensitive, scenario);
        denial.ShouldContain("CorrelationId=story-5-5-domain", Case.Sensitive, scenario);
        logs.Messages.ShouldAllBe(message => !message.Contains(valid, StringComparison.Ordinal));
        logs.Messages.ShouldAllBe(message => !message.Contains(ChannelToken, StringComparison.Ordinal));
    }

    /// <summary>
    /// A wire administrator flag is removed at the boundary unless the domain's verifier confirms current authority;
    /// a verifier failure fails closed before any domain work.
    /// </summary>
    /// <param name="verifier">The verifier behavior: none, allow, deny, or throw.</param>
    /// <param name="expectedFlag">Whether the processor must observe the flag.</param>
    [Theory]
    [InlineData("none", false)]
    [InlineData("allow", true)]
    [InlineData("deny", false)]
    public async Task WireAdministratorFlag_IsHonoredOnlyAfterCurrentVerification(string verifier, bool expectedFlag)
    {
        var processor = new CapturingWidgetProcessor();
        var verifierCalls = new ConcurrentQueue<DomainServiceAdministratorClaim>();
        await using WebApplication app = await StartAsync(
            processor: processor,
            configureServices: services =>
            {
                if (verifier != "none")
                {
                    _ = services.AddSingleton<IDomainServiceAdministratorVerifier>(new DelegateVerifier(claim =>
                    {
                        verifierCalls.Enqueue(claim);
                        return verifier == "allow";
                    }));
                }
            });
        using HttpRequestMessage request = ProcessRequest(
            Assertion([EventStoreWorkloadOperations.DomainServiceProcess]),
            adminFlag: true,
            adminFlagKey: "Actor:GlobalAdmin");

        using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CommandEnvelope command = processor.Commands.ShouldHaveSingleItem();
        bool hasFlag = command.Extensions?.TryGetValue(DomainServiceAdministratorAssertions.GlobalAdminExtensionKey, out string? value) == true
            && value == "true";
        hasFlag.ShouldBe(expectedFlag);
        command.Extensions!.Keys.ShouldAllBe(key => key == DomainServiceAdministratorAssertions.GlobalAdminExtensionKey || key == "keep");
        command.Extensions!["keep"].ShouldBe("me");
        if (verifier != "none")
        {
            DomainServiceAdministratorClaim claim = verifierCalls.ShouldHaveSingleItem();
            claim.UserId.ShouldBe("acting-user");
            claim.Domain.ShouldBe("widget");
        }
    }

    /// <summary>
    /// Story 5.5 (P-4): an unavailable administrator verifier fails <c>/process</c> and <c>/query</c> closed before any
    /// domain work with a bounded, retryable 503, the <c>administrator-verifier-unavailable</c> reason, and the
    /// correlation identifier, while the raw exception text never reaches the response or the logs.
    /// </summary>
    [Fact]
    public async Task WireAdministratorFlag_VerifierFailure_FailsClosedWithBoundedUnavailable()
    {
        string internalDetail = "verifier-internal-detail-" + Guid.NewGuid().ToString("N");
        var processor = new CapturingWidgetProcessor();
        var logs = new CapturingLoggerProvider();
        IDomainQueryHandler handler = Substitute.For<IDomainQueryHandler>();
        _ = handler.Domain.Returns("widget");
        _ = handler.QueryType.Returns("probe-administrator-hint");
        await using WebApplication app = await StartAsync(
            processor: processor,
            logs: logs,
            configureServices: services =>
            {
                _ = services.AddScoped(_ => handler);
                _ = services.AddSingleton<IDomainServiceAdministratorVerifier>(
                    new DelegateVerifier(_ => throw new InvalidOperationException(internalDetail)));
            });
        HttpClient client = app.GetTestClient();
        using HttpRequestMessage command = ProcessRequest(Assertion([EventStoreWorkloadOperations.DomainServiceProcess]), adminFlag: true);
        var query = new QueryEnvelope("tenant-a", "widget", "widget-1", "probe-administrator-hint", [], "corr-q", "acting-user", isGlobalAdmin: true);
        using var queryRequest = new HttpRequestMessage(HttpMethod.Post, "/query")
        {
            Content = JsonContent.Create(query, options: JsonSerializerOptions.Web),
        };
        AddInternalHeaders(queryRequest, Assertion([EventStoreWorkloadOperations.DomainServiceQuery]), [ChannelToken]);

        using HttpResponseMessage commandResponse = await client.SendAsync(command, TestContext.Current.CancellationToken);
        using HttpResponseMessage queryResponse = await client.SendAsync(queryRequest, TestContext.Current.CancellationToken);

        foreach (HttpResponseMessage response in new[] { commandResponse, queryResponse })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            body.ShouldNotContain(internalDetail);
            body.ShouldNotContain(nameof(InvalidOperationException));
        }

        processor.Commands.ShouldBeEmpty();
        _ = await handler.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default);
        string[] denials = [.. logs.Messages.Where(static message => message.Contains(
            "Reason=" + WorkloadAuthenticationReasons.AdministratorVerifierUnavailable,
            StringComparison.Ordinal))];
        denials.Length.ShouldBe(2);
        denials.ShouldAllBe(static message => message.Contains("CorrelationId=story-5-5-domain", StringComparison.Ordinal)
            && message.Contains("StatusCode=503", StringComparison.Ordinal));
        logs.Messages.ShouldAllBe(message => !message.Contains(internalDetail, StringComparison.Ordinal));
    }

    /// <summary>
    /// Story 5.5 (P-13): every registered verifier must confirm current authority; a single refusal removes the wire
    /// administrator flag whatever the registration order.
    /// </summary>
    /// <param name="verdicts">The ordered verifier verdicts.</param>
    /// <param name="expectedFlag">Whether the processor must observe the flag.</param>
    [Theory]
    [InlineData("allow,deny", false)]
    [InlineData("deny,allow", false)]
    [InlineData("allow,allow", true)]
    public async Task WireAdministratorFlag_RequiresEveryVerifierToConfirm(string verdicts, bool expectedFlag)
    {
        ArgumentNullException.ThrowIfNull(verdicts);
        var processor = new CapturingWidgetProcessor();
        await using WebApplication app = await StartAsync(
            processor: processor,
            configureServices: services =>
            {
                foreach (string verdict in verdicts.Split(','))
                {
                    _ = services.AddSingleton<IDomainServiceAdministratorVerifier>(new DelegateVerifier(_ => verdict == "allow"));
                }
            });
        using HttpRequestMessage request = ProcessRequest(Assertion([EventStoreWorkloadOperations.DomainServiceProcess]), adminFlag: true);

        using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CommandEnvelope command = processor.Commands.ShouldHaveSingleItem();
        (command.Extensions?.TryGetValue(DomainServiceAdministratorAssertions.GlobalAdminExtensionKey, out string? value) == true && value == "true")
            .ShouldBe(expectedFlag, verdicts);
    }

    /// <summary>
    /// Story 5.5 (P-9): an assertion that also carries <c>sub</c>, <c>global_admin</c>, roles, tenant, domain, and
    /// permission claims authenticates as the minimal workload principal only: none of those claims survive.
    /// </summary>
    [Fact]
    public async Task WorkloadPrincipal_IsRebuiltMinimal()
    {
        await using WebApplication app = await StartAsync(beforeSdk: application =>
            application.MapPost("/probe/principal", (HttpContext context) => Results.Json(
                    context.User.Claims.Select(static claim => new[] { claim.Type, claim.Value }).ToArray()))
                .RequireAuthorization(EventStoreDomainServicePolicies.AnyWorkload));
        using HttpRequestMessage request = JsonPost("/probe/principal", "{}");
        AddInternalHeaders(
            request,
            Assertion(
                [EventStoreWorkloadOperations.DomainServiceQuery],
                extraClaims: new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["sub"] = "human-administrator",
                    ["role"] = "Admin",
                    ["roles"] = new[] { "global-admin", "tenant-admin" },
                    [System.Security.Claims.ClaimTypes.Role] = "Admin",
                    ["eventstore:domain"] = "widget",
                    ["eventstore:permission"] = "command:submit",
                    ["is_global_admin"] = "true",
                    ["name"] = "Mallory",
                }),
            [ChannelToken]);

        using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string[][] claims = (await response.Content.ReadFromJsonAsync<string[][]>(TestContext.Current.CancellationToken))!;
        claims.Select(static claim => claim[0]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ShouldBe(
            new[]
            {
                System.Security.Claims.ClaimTypes.NameIdentifier,
                EventStoreWorkloadAuthenticationDefaults.LegacyCallerClaimType,
                EventStoreWorkloadAuthenticationDefaults.OperationClaimType,
                EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType,
            }.Order(StringComparer.Ordinal));
        claims.Single(static claim => claim[0] == System.Security.Claims.ClaimTypes.NameIdentifier)[1].ShouldBe("workload:" + Caller);
        claims.Single(static claim => claim[0] == EventStoreWorkloadAuthenticationDefaults.OperationClaimType)[1]
            .ShouldBe(EventStoreWorkloadOperations.DomainServiceQuery);
        claims.ShouldAllBe(static claim => claim[1] != "human-administrator" && claim[1] != "true" && claim[1] != "tenant-a");
    }

    /// <summary>The query administrator hint reaches the handler only after verification.</summary>
    /// <param name="registerVerifier">Whether an allowing verifier is registered.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueryAdministratorHint_IsClearedUnlessVerified(bool registerVerifier)
    {
        var observed = new ConcurrentQueue<QueryEnvelope>();
        IDomainQueryHandler handler = Substitute.For<IDomainQueryHandler>();
        _ = handler.Domain.Returns("widget");
        _ = handler.QueryType.Returns("probe-administrator-hint");
        _ = handler.ExecuteAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            observed.Enqueue(call.Arg<QueryEnvelope>());
            return QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { ok = true }));
        });
        await using WebApplication app = await StartAsync(configureServices: services =>
        {
            _ = services.AddScoped(_ => handler);
            if (registerVerifier)
            {
                _ = services.AddSingleton<IDomainServiceAdministratorVerifier>(new DelegateVerifier(_ => true));
            }
        });
        var query = new QueryEnvelope("tenant-a", "widget", "widget-1", "probe-administrator-hint", [], "corr-q", "acting-user", isGlobalAdmin: true);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/query")
        {
            Content = JsonContent.Create(query, options: JsonSerializerOptions.Web),
        };
        AddInternalHeaders(request, Assertion([EventStoreWorkloadOperations.DomainServiceQuery]), [ChannelToken]);

        using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        observed.ShouldHaveSingleItem().IsGlobalAdmin.ShouldBe(registerVerifier);
    }

    /// <summary>Every canonical operational route enforces its own operation through the real pipeline.</summary>
    /// <param name="route">The route.</param>
    [Theory]
    [InlineData("/process")]
    [InlineData("/replay-state")]
    [InlineData("/query")]
    [InlineData("/project")]
    [InlineData("/project/v2")]
    [InlineData("/project/v2/reconcile")]
    [InlineData("/project/rebuild/v1")]
    [InlineData("/project/rebuild/stage/v1")]
    [InlineData("/project/rebuild/commit/v1")]
    [InlineData("/project/rebuild/abort/v1")]
    [InlineData("/project/rebuild/verify/v1")]
    [InlineData("/project/rebuild/shared/v1")]
    [InlineData("/admin/operational-index-metadata")]
    public async Task EveryOperationalRoute_RequiresItsOwnOperation(string route)
    {
        await using WebApplication app = await StartAsync();
        EventStoreDomainServiceRoutes.TryGet(route, out EventStoreDomainServiceRoute? catalogRoute).ShouldBeTrue();
        string otherOperation = catalogRoute!.Operation == EventStoreWorkloadOperations.DomainServiceMetadata
            ? EventStoreWorkloadOperations.DomainServiceProcess
            : EventStoreWorkloadOperations.DomainServiceMetadata;
        HttpClient client = app.GetTestClient();

        using HttpRequestMessage anonymous = JsonPost(route, "{}");
        using HttpResponseMessage anonymousResponse = await client.SendAsync(anonymous, TestContext.Current.CancellationToken);
        using HttpRequestMessage forbidden = JsonPost(route, "{}");
        AddInternalHeaders(forbidden, Assertion([otherOperation]), [ChannelToken]);
        using HttpResponseMessage forbiddenResponse = await client.SendAsync(forbidden, TestContext.Current.CancellationToken);
        using HttpRequestMessage allowed = JsonPost(route, "{}");
        AddInternalHeaders(allowed, Assertion([catalogRoute.Operation]), [ChannelToken]);
        using HttpResponseMessage allowedResponse = await client.SendAsync(allowed, TestContext.Current.CancellationToken);

        anonymousResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, route);
        forbiddenResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden, route);
        allowedResponse.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized, route);
        allowedResponse.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden, route);
    }

    /// <summary>The three probes stay anonymous while protected routes challenge the same anonymous caller.</summary>
    [Fact]
    public async Task Probes_StayAnonymous_WhileTheRootIsGone()
    {
        await using WebApplication app = await StartAsync();
        HttpClient client = app.GetTestClient();

        foreach (string probe in EventStoreDomainServiceRoutes.AnonymousProbeRoutes)
        {
            using HttpResponseMessage probeResponse = await client.GetAsync(probe, TestContext.Current.CancellationToken);
            probeResponse.StatusCode.ShouldBe(HttpStatusCode.OK, probe);
        }

        // The Tier-3 readiness probe invokes /alive through EventStore's sidecar, whose ACL admits POST only.
        using HttpResponseMessage postedProbe = await client.PostAsync("/alive", content: null, TestContext.Current.CancellationToken);
        postedProbe.StatusCode.ShouldBe(HttpStatusCode.OK);

        using HttpResponseMessage root = await client.GetAsync("/", TestContext.Current.CancellationToken);
        root.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
    }

    /// <summary>The domain-event subscription route requires the app-channel token; a forged delivery is refused.</summary>
    [Fact]
    public async Task SubscriptionRoute_RequiresTheAppChannelToken()
    {
        await using WebApplication app = await StartAsync(configureServices: services =>
            services.AddEventStoreDomainEvents(
                typeof(DomainServiceTrustBoundaryTests).Assembly,
                options => options.SubscriptionRoute = "/widget/events"));
        HttpClient client = app.GetTestClient();

        using HttpRequestMessage forged = JsonPost("/widget/events", "{}");
        forged.Headers.Add(EventStoreWorkloadAuthenticationDefaults.DaprCallerHeaderName, Caller);
        using HttpResponseMessage forgedResponse = await client.SendAsync(forged, TestContext.Current.CancellationToken);
        using HttpRequestMessage discovery = new(HttpMethod.Get, "/dapr/subscribe");
        using HttpResponseMessage discoveryResponse = await client.SendAsync(discovery, TestContext.Current.CancellationToken);
        using HttpRequestMessage delivered = JsonPost("/widget/events", "{}");
        delivered.Headers.Add(DaprAppChannelToken.HeaderName, ChannelToken);
        using HttpResponseMessage deliveredResponse = await client.SendAsync(delivered, TestContext.Current.CancellationToken);

        forgedResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        discoveryResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        deliveredResponse.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Production authority mode: an RS256 assertion from the authority admits the call, and unreachable issuer
    /// metadata is a bounded, retryable 503 with no domain work.
    /// </summary>
    [Fact]
    public async Task ProductionAuthorityMode_AdmitsAuthorityAssertionAndFailsClosedWhenMetadataIsUnavailable()
    {
        using RSA rsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa) { KeyId = "story-5-5" };
        var processor = new CapturingWidgetProcessor();
        var logs = new CapturingLoggerProvider();
        await using WebApplication app = await StartAsync(
            environmentName: Environments.Production,
            settings: new Dictionary<string, string?>
            {
                ["Authentication:JwtBearer:Authority"] = AuthorityIssuer,
                ["Authentication:JwtBearer:Issuer"] = AuthorityIssuer,
                ["Authentication:JwtBearer:SigningKey"] = null,
                ["Authentication:JwtBearer:AllowedAlgorithms:0"] = SecurityAlgorithms.RsaSha256,
                ["Authentication:JwtBearer:RequireHttpsMetadata"] = "true",
            },
            processor: processor,
            logs: logs);
        JwtBearerOptions bearer = app.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme);
        var configuration = new OpenIdConnectConfiguration { Issuer = AuthorityIssuer };
        configuration.SigningKeys.Add(signingKey);
        bearer.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
        string authorityAssertion = RsaAssertion(signingKey, [EventStoreWorkloadOperations.DomainServiceProcess]);

        using HttpRequestMessage admitted = ProcessRequest(authorityAssertion);
        using HttpResponseMessage admittedResponse = await app.GetTestClient().SendAsync(admitted, TestContext.Current.CancellationToken);
        admittedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        _ = processor.Commands.ShouldHaveSingleItem();

        bearer.ConfigurationManager = new UnavailableConfigurationManager();
        using HttpRequestMessage unavailable = ProcessRequest(authorityAssertion);
        using HttpResponseMessage unavailableResponse = await app.GetTestClient().SendAsync(unavailable, TestContext.Current.CancellationToken);
        unavailableResponse.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        processor.Commands.Count.ShouldBe(1);
        logs.Messages.ShouldContain(message => message.Contains($"Reason={WorkloadAuthenticationReasons.VerifierUnavailable}", StringComparison.Ordinal));
    }

    /// <summary>
    /// Story 5.5 (P-15): a Keycloak-shaped authority token is accepted: a multi-value <c>aud</c> that includes this
    /// receiver, operations as a JSON array, and Keycloak's own claims, which never reach the rebuilt principal.
    /// </summary>
    [Fact]
    public async Task ProductionAuthorityMode_AcceptsAKeycloakShapedAssertion()
    {
        using RSA rsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa) { KeyId = "story-5-5-keycloak" };
        var processor = new CapturingWidgetProcessor();
        await using WebApplication app = await StartAsync(
            environmentName: Environments.Production,
            settings: new Dictionary<string, string?>
            {
                ["Authentication:JwtBearer:Authority"] = AuthorityIssuer,
                ["Authentication:JwtBearer:Issuer"] = AuthorityIssuer,
                ["Authentication:JwtBearer:SigningKey"] = null,
                ["Authentication:JwtBearer:AllowedAlgorithms:0"] = SecurityAlgorithms.RsaSha256,
                ["Authentication:JwtBearer:RequireHttpsMetadata"] = "true",
            },
            processor: processor);
        JwtBearerOptions bearer = app.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme);
        var configuration = new OpenIdConnectConfiguration { Issuer = AuthorityIssuer };
        configuration.SigningKeys.Add(signingKey);
        bearer.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
        DateTime issued = DateTime.UtcNow;
        string keycloakAssertion = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = AuthorityIssuer,
            IssuedAt = issued,
            NotBefore = issued,
            Expires = issued.AddSeconds(300),
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["aud"] = new[] { "account", AppId },
                [EventStoreWorkloadAuthenticationDefaults.CallerClaimType] = Caller,
                [EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = new[] { EventStoreWorkloadOperations.DomainServiceProcess },
                ["typ"] = "Bearer",
                ["scope"] = "eventstore-audience.sample eventstore-operation.domain-service.process",
                ["sub"] = Guid.NewGuid().ToString("D"),
                ["preferred_username"] = "service-account-eventstore",
                ["clientHost"] = "127.0.0.1",
            },
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256),
        });

        using HttpRequestMessage request = ProcessRequest(keycloakAssertion);
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _ = processor.Commands.ShouldHaveSingleItem();
    }

    /// <summary>Outside Development a host without the app-channel token never starts.</summary>
    [Fact]
    public async Task ProductionWithoutChannelToken_FailsStartup()
    {
        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() => StartAsync(
            environmentName: Environments.Production,
            settings: new Dictionary<string, string?>
            {
                [DaprAppChannelToken.ConfigurationKey] = null,
                ["Authentication:JwtBearer:Authority"] = AuthorityIssuer,
                ["Authentication:JwtBearer:Issuer"] = AuthorityIssuer,
                ["Authentication:JwtBearer:SigningKey"] = null,
                ["Authentication:JwtBearer:AllowedAlgorithms:0"] = SecurityAlgorithms.RsaSha256,
                ["Authentication:JwtBearer:RequireHttpsMetadata"] = "true",
            }));

        failure.Message.ShouldContain(DaprAppChannelToken.ConfigurationKey);
        failure.Message.ShouldNotContain(ChannelToken);
    }

    /// <summary>
    /// A host override of an SDK route must carry the same policy: a weaker override or an extra anonymous endpoint
    /// fails startup, while an override with the exact policy starts and is enforced.
    /// </summary>
    /// <param name="overrideKind">weak-project, anonymous-extra, or secured-project.</param>
    [Theory]
    [InlineData("weak-project")]
    [InlineData("anonymous-extra")]
    [InlineData("weak-subscribe")]
    [InlineData("secured-project")]
    public async Task HostOverrides_MustCarryTheSdkPolicy(string overrideKind)
    {
        int hits = 0;
        void MapOverrides(WebApplication app)
        {
            switch (overrideKind)
            {
                case "weak-project":
                    _ = app.MapPost("/project", () => Interlocked.Increment(ref hits));
                    break;
                case "anonymous-extra":
                    _ = app.MapGet("/faults/hit-count", () => hits).AllowAnonymous();
                    break;
                case "weak-subscribe":
                    // A pre-mapped discovery route without the sidecar-channel policy would fall through to the
                    // any-workload fallback and silently deny every subscription discovery.
                    _ = app.MapSubscribeHandler();
                    break;
                default:
                    _ = app.MapPost("/project", () => Interlocked.Increment(ref hits))
                        .RequireAuthorization(EventStoreDomainServicePolicies.Project);
                    break;
            }
        }

        if (overrideKind != "secured-project")
        {
            InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() => StartAsync(beforeSdk: MapOverrides));
            failure.Message.ShouldContain(overrideKind switch
            {
                "weak-project" => "/project",
                "weak-subscribe" => "/dapr/subscribe",
                _ => "/faults/hit-count",
            });
            hits.ShouldBe(0);
            return;
        }

        await using WebApplication app = await StartAsync(beforeSdk: MapOverrides);
        using HttpRequestMessage forged = JsonPost("/project", "{}");
        forged.Headers.Add(DaprAppChannelToken.HeaderName, ChannelToken);
        forged.Headers.Add(EventStoreWorkloadAuthenticationDefaults.DaprCallerHeaderName, Caller);
        using HttpResponseMessage forgedResponse = await app.GetTestClient().SendAsync(forged, TestContext.Current.CancellationToken);
        using HttpRequestMessage allowed = JsonPost("/project", "{}");
        AddInternalHeaders(allowed, Assertion([EventStoreWorkloadOperations.DomainServiceProject]), [ChannelToken]);
        using HttpResponseMessage allowedResponse = await app.GetTestClient().SendAsync(allowed, TestContext.Current.CancellationToken);

        forgedResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        allowedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        hits.ShouldBe(1);
    }

    private static async Task<WebApplication> StartAsync(
        string environmentName = "Development",
        Dictionary<string, string?>? settings = null,
        CapturingWidgetProcessor? processor = null,
        CapturingLoggerProvider? logs = null,
        Action<IServiceCollection>? configureServices = null,
        Action<WebApplication>? beforeSdk = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environmentName });
        _ = builder.WebHost.UseTestServer();
        var configuration = new Dictionary<string, string?>
        {
            [DaprAppChannelToken.ConfigurationKey] = ChannelToken,
            ["EventStore:DomainService:AppId"] = AppId,
            ["Authentication:JwtBearer:Issuer"] = Issuer,
            ["Authentication:JwtBearer:Audience"] = "hexalith-eventstore",
            ["Authentication:JwtBearer:SigningKey"] = SigningKey,
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = SecurityAlgorithms.HmacSha256,
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
        };
        foreach (KeyValuePair<string, string?> setting in settings ?? [])
        {
            configuration[setting.Key] = setting.Value;
        }

        if (configuration.TryGetValue("Authentication:JwtBearer:Authority", out string? authority) && authority is not null)
        {
            _ = configuration.Remove("Authentication:JwtBearer:AllowedAlgorithms:0", out _);
            configuration["Authentication:JwtBearer:AllowedAlgorithms:0"] = SecurityAlgorithms.RsaSha256;
        }

        _ = builder.Configuration.AddInMemoryCollection(configuration);
        builder.Logging.ClearProviders();
        if (logs is not null)
        {
            _ = builder.Logging.AddProvider(logs);
        }

        _ = builder.AddEventStoreDomainService(typeof(WidgetAggregate).Assembly);
        if (processor is not null)
        {
            _ = builder.Services.AddKeyedSingleton<IDomainProcessor>("widget", processor);
            _ = builder.Services.AddKeyedSingleton<IAsyncDomainProcessor>("widget", processor);
        }

        configureServices?.Invoke(builder.Services);
        WebApplication app = builder.Build();
        beforeSdk?.Invoke(app);
        _ = app.UseEventStoreDomainService();
        try
        {
            await app.StartAsync(TestContext.Current.CancellationToken);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }

        return app;
    }

    private static HttpRequestMessage ProcessRequest(
        string? assertion,
        IReadOnlyList<string>? channelTokens = null,
        bool adminFlag = false,
        string adminFlagKey = "actor:globalAdmin")
    {
        Dictionary<string, string> extensions = new(StringComparer.Ordinal) { ["keep"] = "me" };
        if (adminFlag)
        {
            extensions[adminFlagKey] = "true";
        }

        var body = new DomainServiceRequest(
            new CommandEnvelope(
                MessageId: UniqueIdHelper.GenerateSortableUniqueStringId(),
                TenantId: "tenant-a",
                Domain: "widget",
                AggregateId: "widget-1",
                CommandType: nameof(CreateWidget),
                Payload: JsonSerializer.SerializeToUtf8Bytes(new CreateWidget()),
                CorrelationId: "corr-1",
                CausationId: null,
                UserId: "acting-user",
                Extensions: extensions),
            CurrentState: null);
        var request = new HttpRequestMessage(HttpMethod.Post, "/process")
        {
            Content = JsonContent.Create(body, options: JsonSerializerOptions.Web),
        };
        AddInternalHeaders(request, assertion, channelTokens ?? [ChannelToken]);
        return request;
    }

    private static HttpRequestMessage JsonPost(string route, string json)
        => new(HttpMethod.Post, route) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static void AddInternalHeaders(HttpRequestMessage request, string? assertion, IReadOnlyList<string> channelTokens)
    {
        request.Headers.Add("X-Correlation-ID", "story-5-5-domain");
        if (channelTokens.Count > 0)
        {
            request.Headers.Add(DaprAppChannelToken.HeaderName, channelTokens);
        }

        if (assertion is not null)
        {
            request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
        }
    }

    private static string Assertion(
        IReadOnlyCollection<string> operations,
        string audience = AppId,
        string caller = Caller,
        DateTime? issuedAt = null,
        TimeSpan? lifetime = null,
        string? signingKey = null,
        IReadOnlyDictionary<string, object>? extraClaims = null)
    {
        DateTime issued = issuedAt ?? DateTime.UtcNow;
        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [EventStoreWorkloadAuthenticationDefaults.CallerClaimType] = caller,
            [EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = operations.ToArray(),
            ["global_admin"] = "true",
            ["eventstore:tenant"] = "tenant-a",
        };
        foreach (KeyValuePair<string, object> claim in extraClaims ?? new Dictionary<string, object>())
        {
            claims[claim.Key] = claim.Value;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            IssuedAt = issued,
            NotBefore = issued,
            Expires = issued.Add(lifetime ?? TimeSpan.FromMinutes(2)),
            Claims = claims,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }

    private static string RsaAssertion(SecurityKey signingKey, IReadOnlyCollection<string> operations)
    {
        DateTime issued = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = AuthorityIssuer,
            Audience = AppId,
            IssuedAt = issued,
            NotBefore = issued,
            Expires = issued.AddMinutes(5),
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [EventStoreWorkloadAuthenticationDefaults.CallerClaimType] = Caller,
                [EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = operations.ToArray(),
            },
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256),
        };
        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }

    private sealed class CapturingWidgetProcessor : IDomainProcessor, IAsyncDomainProcessor
    {
        private readonly ConcurrentQueue<CommandEnvelope> _commands = new();

        public IReadOnlyCollection<CommandEnvelope> Commands => [.. _commands];

        public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState)
            => ProcessAsync(command, currentState, CancellationToken.None);

        public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);
            _commands.Enqueue(command);
            return Task.FromResult(DomainResult.Success(new IEventPayload[] { new WidgetCreated() }));
        }
    }

    private sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class DelegateVerifier(Func<DomainServiceAdministratorClaim, bool> verify) : IDomainServiceAdministratorVerifier
    {
        public Task<bool> IsCurrentGlobalAdministratorAsync(DomainServiceAdministratorClaim claim, CancellationToken cancellationToken)
            => Task.FromResult(verify(claim));
    }

    private sealed class UnavailableConfigurationManager : IConfigurationManager<OpenIdConnectConfiguration>
    {
        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
            => throw new InvalidOperationException("IDX20803: Unable to obtain configuration.");

        public void RequestRefresh()
        {
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IReadOnlyCollection<string> Messages => [.. _messages];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => messages.Enqueue(formatter(state, exception) + (exception is null ? string.Empty : " | " + exception));
        }
    }
}
