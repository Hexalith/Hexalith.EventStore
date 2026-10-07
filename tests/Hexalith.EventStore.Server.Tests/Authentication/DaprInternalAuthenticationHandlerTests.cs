extern alias eventstore;

using System.Net;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.HealthChecks;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Tests.Integration;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using EventStoreProgram = eventstore::Program;

namespace Hexalith.EventStore.Server.Tests.Authentication;

/// <summary>
/// Story 5.5 (FR28): the <c>DaprInternal</c> boundary of the real EventStore host never mints identity from a
/// plaintext <c>dapr-caller-app-id</c> header or a wire administrator flag. It admits an internal caller only with
/// the app-channel token plus a short-lived workload assertion from the trusted issuer, and the resulting principal
/// carries no administrator, tenant, or permission authority. Every denial performs zero downstream work.
/// </summary>
public sealed class DaprInternalAuthenticationHandlerTests : IClassFixture<DaprInternalAuthenticationHandlerTests.InternalBoundaryFactory>
{
    private const string ChannelToken = "story-5-5-channel-token";
    private const string Audience = DaprInternalAuthenticationOptions.DefaultAudience;
    private const string AllowedCaller = "reactor";
    private const string TrustedEffectsRoute = "/api/v1/trusted-effects";
    private const string CommandsRoute = "/api/v1/commands";

    private readonly InternalBoundaryFactory _factory;

    public DaprInternalAuthenticationHandlerTests(InternalBoundaryFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _factory.Reset();
    }

    /// <summary>A plaintext caller header plus a valid channel token authenticates nothing and reaches nothing.</summary>
    [Fact]
    public async Task ForgedCallerHeaderWithChannelToken_IsDeniedWithoutDownstreamWork()
    {
        using HttpClient client = _factory.CreateClient();
        using HttpRequestMessage request = TrustedEffectRequest(assertion: null);
        request.Headers.Add(DaprInternalAuthenticationOptions.CallerHeaderName, AllowedCaller);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await AssertNoTrustedEffectWorkAsync();
    }

    /// <summary>
    /// A forged internal caller with a wire administrator flag cannot submit a command: no global-administrator
    /// principal is minted and the command router is never reached.
    /// </summary>
    [Fact]
    public async Task ForgedCallerHeaderAndAdminFlag_GrantNothingOnCommands()
    {
        using HttpClient client = _factory.CreateClient();
        using HttpRequestMessage request = CommandRequest(assertion: null);
        request.Headers.Add(DaprInternalAuthenticationOptions.CallerHeaderName, "tenants");
        request.Headers.Add("global_admin", "true");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        _factory.CommandRouter.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>A valid assertion admits only the asserted workload, which reaches the operation it was granted.</summary>
    [Fact]
    public async Task ValidWorkloadAssertion_AdmitsTheSignedWorkloadOnly()
    {
        using HttpClient client = _factory.CreateClient();
        string assertion = WorkloadAssertionTestTokens.Create(Audience, AllowedCaller, [EventStoreWorkloadOperations.TrustedEffect]);
        using HttpRequestMessage request = TrustedEffectRequest(assertion);
        request.Headers.Add(DaprInternalAuthenticationOptions.CallerHeaderName, AllowedCaller);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _ = await _factory.Admission.Received(1).AdmitAsync(
            Arg.Any<TrustedEffectSubmission>(),
            Arg.Is<TrustedEffectContext>(context => context.Workload == AllowedCaller),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An authenticated internal workload carries no human subject and no administrator or tenant authority: a
    /// command it submits with a wire administrator flag is refused before authorization or the command router runs.
    /// </summary>
    [Fact]
    public async Task ValidWorkloadAssertion_CarriesNoAdministratorAuthority()
    {
        using HttpClient client = _factory.CreateClient();
        string assertion = WorkloadAssertionTestTokens.Create(Audience, AllowedCaller, [EventStoreWorkloadOperations.TrustedEffect]);
        using HttpRequestMessage request = CommandRequest(assertion);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        _factory.CommandRouter.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Every wrong, stale, duplicate, conflicting, or missing credential is denied before any work.</summary>
    /// <param name="scenario">The denial scenario.</param>
    /// <param name="expectedStatus">The expected bounded status.</param>
    /// <param name="expectedReason">The expected bounded reason code.</param>
    [Theory]
    [InlineData("missing-channel-token", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.ChannelTokenMissing)]
    [InlineData("wrong-channel-token", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.ChannelTokenInvalid)]
    [InlineData("duplicate-channel-token", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.ChannelTokenDuplicate)]
    [InlineData("wrong-audience", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.AudienceInvalid)]
    [InlineData("wrong-caller", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.CallerNotAllowed)]
    [InlineData("missing-caller", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.CallerMissing)]
    [InlineData("conflicting-caller-header", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.CallerConflict)]
    [InlineData("wrong-operation", HttpStatusCode.Forbidden, WorkloadAuthenticationReasons.OperationNotGranted)]
    [InlineData("expired", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.AssertionExpired)]
    [InlineData("long-lived", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.AssertionStale)]
    [InlineData("wrong-key", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.SignatureInvalid)]
    [InlineData("wrong-issuer", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.IssuerInvalid)]
    [InlineData("duplicate-assertion", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.AssertionDuplicate)]
    [InlineData("human-bearer-conflict", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.CredentialConflict)]
    [InlineData("malformed", HttpStatusCode.Unauthorized, WorkloadAuthenticationReasons.AssertionMalformed)]
    public async Task InvalidInternalCredential_IsDeniedWithBoundedTelemetryAndNoWork(
        string scenario,
        HttpStatusCode expectedStatus,
        string expectedReason)
    {
        using HttpClient client = _factory.CreateClient();
        string valid = WorkloadAssertionTestTokens.Create(Audience, AllowedCaller, [EventStoreWorkloadOperations.TrustedEffect]);
        string assertion = scenario switch
        {
            "wrong-audience" => WorkloadAssertionTestTokens.Create("sample", AllowedCaller, [EventStoreWorkloadOperations.TrustedEffect]),
            "wrong-caller" => WorkloadAssertionTestTokens.Create(Audience, "intruder", [EventStoreWorkloadOperations.TrustedEffect]),
            "missing-caller" => WorkloadAssertionTestTokens.Create(Audience, caller: null, [EventStoreWorkloadOperations.TrustedEffect]),
            "wrong-operation" => WorkloadAssertionTestTokens.Create(Audience, AllowedCaller, [EventStoreWorkloadOperations.ProjectionNotify]),
            "expired" => WorkloadAssertionTestTokens.Create(Audience, AllowedCaller, [EventStoreWorkloadOperations.TrustedEffect], issuedAt: DateTime.UtcNow.AddMinutes(-10)),
            "long-lived" => WorkloadAssertionTestTokens.Create(Audience, AllowedCaller, [EventStoreWorkloadOperations.TrustedEffect], lifetime: TimeSpan.FromHours(1)),
            "wrong-key" => WorkloadAssertionTestTokens.Create(Audience, AllowedCaller, [EventStoreWorkloadOperations.TrustedEffect], signingKey: Convert.ToBase64String(new byte[48])),
            "wrong-issuer" => WorkloadAssertionTestTokens.Create(Audience, AllowedCaller, [EventStoreWorkloadOperations.TrustedEffect], issuer: "https://unexpected.example.test"),
            "malformed" => "not-a-jwt",
            _ => valid,
        };
        using HttpRequestMessage request = TrustedEffectRequest(assertion, includeChannelToken: scenario != "missing-channel-token");
        switch (scenario)
        {
            case "wrong-channel-token":
                _ = request.Headers.Remove(DaprAppChannelToken.HeaderName);
                request.Headers.Add(DaprAppChannelToken.HeaderName, "wrong-channel-token");
                break;
            case "duplicate-channel-token":
                request.Headers.Add(DaprAppChannelToken.HeaderName, ChannelToken);
                break;
            case "conflicting-caller-header":
                request.Headers.Add(DaprInternalAuthenticationOptions.CallerHeaderName, "eventstore-admin");
                break;
            case "duplicate-assertion":
                request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, valid);
                break;
            case "human-bearer-conflict":
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestJwtHelper.GenerateToken());
                break;
        }

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expectedStatus, scenario);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty(scenario);
        await AssertNoTrustedEffectWorkAsync();
        string denial = _factory.Logs.Messages
            .Where(static message => message.Contains("InternalAuthenticationDenied", StringComparison.Ordinal))
            .ShouldHaveSingleItem(scenario);
        denial.ShouldContain($"Reason={expectedReason}", Case.Sensitive, scenario);
        denial.ShouldContain("CorrelationId=story-5-5-correlation", Case.Sensitive, scenario);
        _factory.Logs.Messages.ShouldAllBe(message => !message.Contains(assertion, StringComparison.Ordinal) || assertion.Length < 16);
        _factory.Logs.Messages.ShouldAllBe(message => !message.Contains(ChannelToken, StringComparison.Ordinal));
    }

    /// <summary>
    /// Story 5.5 (P-1): a plain <c>[Authorize]</c> endpoint authenticates the JwtBearer scheme only, so even a valid,
    /// allow-listed workload assertion never satisfies the gateway default policy.
    /// </summary>
    [Fact]
    public async Task DefaultPolicy_AuthenticatesOnlyHumanBearers()
    {
        using HttpClient client = _factory.CreateClient();
        Microsoft.AspNetCore.Authorization.AuthorizationPolicy defaultPolicy = await _factory.Services
            .GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider>()
            .GetDefaultPolicyAsync();
        string assertion = WorkloadAssertionTestTokens.Create(Audience, AllowedCaller, [EventStoreWorkloadOperations.TrustedEffect]);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/commands/status/01JSTATUS000000000000000000");
        AddInternalHeaders(request, assertion, includeChannelToken: true);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        defaultPolicy.AuthenticationSchemes.ShouldBe([Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme]);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        _factory.CommandRouter.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>
    /// Story 5.5 (BS-A): a domain service's <see cref="Client.Effects.HttpTrustedEffectSubmitter"/>, configured with its
    /// own workload assertion, is admitted as exactly that workload for <c>eventstore:trusted-effect</c>. Without the
    /// assertion, or as a workload the gateway does not allow-list, the submission gets 401 and nothing is admitted.
    /// </summary>
    /// <param name="submitter">The submitting domain service's workload identity.</param>
    /// <param name="attachAssertion">Whether the submitter client attaches its workload assertion.</param>
    /// <param name="expectedStatus">The expected status.</param>
    [Theory]
    [InlineData(AllowedCaller, true, HttpStatusCode.OK)]
    [InlineData(AllowedCaller, false, HttpStatusCode.Unauthorized)]
    [InlineData("sample", true, HttpStatusCode.Unauthorized)]
    public async Task DomainServiceTrustedEffectSubmitter_IsAdmittedOnlyWithItsOwnAllowListedAssertion(
        string submitter,
        bool attachAssertion,
        HttpStatusCode expectedStatus)
    {
        _ = _factory.Server;
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Issuer"] = WorkloadAssertionTestTokens.Issuer,
            ["Authentication:JwtBearer:Audience"] = "hexalith-eventstore",
            ["Authentication:JwtBearer:SigningKey"] = AuthenticationTestEnvironment.SigningKey,
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256",
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
            ["Authentication:WorkloadIssuer:Workload"] = submitter,
        }).Build());
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);
        _ = services.AddSingleton(environment);
        IHttpClientBuilder submitterClient = services.AddHttpClient<Client.Effects.ITrustedEffectSubmitter, Client.Effects.HttpTrustedEffectSubmitter>(
            client => client.BaseAddress = _factory.Server.BaseAddress);
        if (attachAssertion)
        {
            _ = submitterClient.AddEventStoreTrustedEffectWorkloadAssertion();
        }

        // The gateway's own sidecar presents its app-channel token on every delivery.
        _ = submitterClient
            .AddHttpMessageHandler(() => new GatewaySidecarHandler(ChannelToken))
            .ConfigurePrimaryHttpMessageHandler(() => _factory.Server.CreateHandler());
        await using ServiceProvider domainService = services.BuildServiceProvider();
        Client.Effects.ITrustedEffectSubmitter submitterService = domainService.GetRequiredService<Client.Effects.ITrustedEffectSubmitter>();
        using HttpRequestMessage template = TrustedEffectRequest(assertion: null);
        TrustedEffectSubmitRequest body = JsonSerializer.Deserialize<TrustedEffectSubmitRequest>(
            await template.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken),
            JsonSerializerOptions.Web)!;

        if (expectedStatus == HttpStatusCode.OK)
        {
            TrustedEffectResult result = await submitterService.SubmitAsync(
                body.Submission,
                new TrustedEffectContext(submitter, body.Purpose, body.CausationId, body.DelegationToken),
                TestContext.Current.CancellationToken);
            result.Disposition.ShouldBe(TrustedEffectDisposition.Success);
            _ = await _factory.Admission.Received(1).AdmitAsync(
                Arg.Any<TrustedEffectSubmission>(),
                Arg.Is<TrustedEffectContext>(context => context.Workload == submitter),
                Arg.Any<CancellationToken>());
            return;
        }

        HttpRequestException failure = await Should.ThrowAsync<HttpRequestException>(() => submitterService.SubmitAsync(
            body.Submission,
            new TrustedEffectContext(submitter, body.Purpose, body.CausationId, body.DelegationToken),
            TestContext.Current.CancellationToken));
        failure.StatusCode.ShouldBe(expectedStatus);
        await AssertNoTrustedEffectWorkAsync();
    }

    /// <summary>
    /// Story 5.5 (BS-B): the Tenants global-administrator bootstrap succeeds with the configured administrator's own
    /// delegated credential (the locally signed, short-lived symmetric-mode token), not with any app-id grant. The
    /// command reaches routing as that human administrator.
    /// </summary>
    [Fact]
    public async Task DelegatedAdministratorCredential_BootstrapsTheGlobalAdministrator()
    {
        _ = _factory.CommandRouter.RouteCommandAsync(Arg.Any<Hexalith.EventStore.Server.Pipeline.Commands.SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Hexalith.EventStore.Server.Actors.CommandProcessingResult(Accepted: true, CorrelationId: "story-5-5-correlation"));
        using HttpClient client = _factory.CreateClient();
        DateTime now = DateTime.UtcNow;
        string delegated = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler().CreateToken(new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Issuer = WorkloadAssertionTestTokens.Issuer,
            Audience = "hexalith-eventstore",
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddSeconds(120),
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["sub"] = "bootstrap-administrator",
                ["global_admin"] = "true",
                ["eventstore:tenant"] = "system",
            },
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(AuthenticationTestEnvironment.SigningKey)),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256),
        });
        var body = new
        {
            MessageId = "01JBOOTSTRAP00000000000000",
            Tenant = "system",
            Domain = "global-administrators",
            AggregateId = "global-administrators",
            CommandType = "BootstrapGlobalAdmin",
            Payload = new { userId = "bootstrap-administrator" },
            CorrelationId = "01JBOOTSTRAPCORRELATION000",
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, CommandsRoute)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonSerializerOptions.Web), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", delegated);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await _factory.CommandRouter.Received(1).RouteCommandAsync(
            Arg.Is<Hexalith.EventStore.Server.Pipeline.Commands.SubmitCommand>(command => command.CommandType == "BootstrapGlobalAdmin"
                && command.Tenant == "system"
                && command.UserId == "bootstrap-administrator"
                && command.IsGlobalAdmin),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Story 5.5 (P-11): outside Development, a host without <c>APP_API_TOKEN</c> verifies no channel at all
    /// (<see cref="DaprAppChannelTokenStatus.Unconfigured"/>); only Development without a token is
    /// <see cref="DaprAppChannelTokenStatus.NotRequired"/>.
    /// </summary>
    /// <param name="configured">The configured token, or <see langword="null"/>.</param>
    /// <param name="isDevelopment">Whether the host runs in Development.</param>
    /// <param name="expected">The expected status.</param>
    [Theory]
    [InlineData(null, false, DaprAppChannelTokenStatus.Unconfigured)]
    [InlineData("   ", false, DaprAppChannelTokenStatus.Unconfigured)]
    [InlineData(null, true, DaprAppChannelTokenStatus.NotRequired)]
    [InlineData("configured", false, DaprAppChannelTokenStatus.Missing)]
    public void AppChannelToken_IsUnconfiguredOutsideDevelopment(string? configured, bool isDevelopment, DaprAppChannelTokenStatus expected)
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();

        DaprAppChannelTokenStatus status = DaprAppChannelToken.Verify(context.Request, configured, isDevelopment);

        status.ShouldBe(expected);
        DaprAppChannelToken.IsAdmitted(status).ShouldBe(expected == DaprAppChannelTokenStatus.NotRequired);
    }

    /// <summary>
    /// Story 5.5 (P-11): through the real pipeline of a non-Development gateway without <c>APP_API_TOKEN</c>, even a
    /// valid assertion from an allow-listed caller is denied with the bounded <c>channel-unconfigured</c> reason and no work.
    /// </summary>
    [Fact]
    public async Task UnconfiguredChannelOutsideDevelopment_DeniesValidAssertionWithoutWork()
    {
        var logs = new CapturingLoggerProvider();
        ITrustedEffectAdmissionPolicy admission = Substitute.For<ITrustedEffectAdmissionPolicy>();
        await using WebApplicationFactory<EventStoreProgram> staging = new WebApplicationFactory<EventStoreProgram>().WithWebHostBuilder(builder =>
        {
            _ = builder.UseEnvironment(Environments.Staging);
            _ = builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DaprAppChannelToken.ConfigurationKey] = null,
                ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256",
                ["Authentication:JwtBearer:AllowInsecureSymmetricKey"] = "true",
                ["Authentication:DaprInternal:AllowedCallers:0"] = AllowedCaller,
            }));
            _ = builder.ConfigureLogging(logging => logging.AddProvider(logs));
            _ = builder.ConfigureTestServices(services =>
            {
                WebApplicationFactoryServiceOverrides.RemoveAdminOperationalIndexHostedService(services);
                services.RemoveAll<ITrustedEffectAdmissionPolicy>();
                _ = services.AddSingleton(admission);
            });
        });
        using HttpClient client = staging.CreateClient();
        using HttpRequestMessage request = TrustedEffectRequest(
            WorkloadAssertionTestTokens.Create(Audience, AllowedCaller, [EventStoreWorkloadOperations.TrustedEffect]));

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        _ = await admission.DidNotReceiveWithAnyArgs().AdmitAsync(default!, default!, default);
        logs.Messages.ShouldContain(message => message.Contains("InternalAuthenticationDenied", StringComparison.Ordinal)
            && message.Contains($"Reason={WorkloadAuthenticationReasons.ChannelUnconfigured}", StringComparison.Ordinal));
    }

    /// <summary>Readiness fails without a configured channel secret in Production.</summary>
    /// <param name="configuredToken">The configured token.</param>
    /// <param name="expectedStatus">The expected readiness status.</param>
    [Theory]
    [InlineData(null, HealthStatus.Unhealthy)]
    [InlineData("configured-token", HealthStatus.Healthy)]
    public async Task AppChannelReadiness_ProductionRequiresConfiguredSecret(
        string? configuredToken,
        HealthStatus expectedStatus)
    {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DaprAppChannelTokenValidator.ConfigurationKey] = configuredToken })
            .Build();
        var check = new DaprAppChannelTokenHealthCheck(
            new DaprAppChannelTokenValidator(environment, configuration));

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(expectedStatus);
    }

    /// <summary>Readiness requires the channel secret only while internal callers are allow-listed.</summary>
    /// <param name="hasAllowedCallers">Whether a caller is allow-listed.</param>
    /// <param name="configuredToken">The configured token.</param>
    /// <param name="expectedStatus">The expected readiness status.</param>
    [Theory]
    [InlineData(false, null, HealthStatus.Healthy)]
    [InlineData(true, null, HealthStatus.Unhealthy)]
    [InlineData(true, "configured-token", HealthStatus.Healthy)]
    public async Task AppChannelReadiness_RequiresSecretOnlyWhenInternalCallersAreAllowListed(
        bool hasAllowedCallers,
        string? configuredToken,
        HealthStatus expectedStatus)
    {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DaprAppChannelTokenValidator.ConfigurationKey] = configuredToken })
            .Build();
        var options = new TestOptionsMonitor<DaprInternalAuthenticationOptions>(new DaprInternalAuthenticationOptions
        {
            AllowedCallers = hasAllowedCallers ? ["tenants"] : [],
        });
        var check = new DaprAppChannelTokenHealthCheck(
            new DaprAppChannelTokenValidator(environment, configuration, options));

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(expectedStatus);
    }

    private async Task AssertNoTrustedEffectWorkAsync()
    {
        _ = await _factory.Admission.DidNotReceiveWithAnyArgs().AdmitAsync(default!, default!, default);
        _ = await _factory.Proof.DidNotReceiveWithAnyArgs().SignAsync(default!, default);
        _ = await _factory.Router.DidNotReceiveWithAnyArgs().RouteAsync(default!, default!, default!, default);
    }

    private static HttpRequestMessage TrustedEffectRequest(string? assertion, bool includeChannelToken = true)
    {
        var identity = new EffectIdentity(
            "tenant-a", "works", "source-1", 1,
            EffectKindCatalog.DateResume, "works", "target-1", 0);
        string messageId = EffectIdentityCodec.ComputeMessageId(identity);
        var body = new TrustedEffectSubmitRequest(
            new TrustedEffectSubmission(identity, "ResumeWorkItem", [123, 125], messageId, messageId),
            "date-resume",
            "source-event",
            "signed-token");
        var request = new HttpRequestMessage(HttpMethod.Post, TrustedEffectsRoute)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonSerializerOptions.Web), Encoding.UTF8, "application/json"),
        };
        AddInternalHeaders(request, assertion, includeChannelToken);
        return request;
    }

    private static HttpRequestMessage CommandRequest(string? assertion)
    {
        var body = new
        {
            MessageId = "01JCOMMAND0000000000000000",
            Tenant = "system",
            Domain = "global-administrators",
            AggregateId = "singleton",
            CommandType = "BootstrapGlobalAdmin",
            Payload = new { userId = "attacker" },
            Extensions = new Dictionary<string, string>(StringComparer.Ordinal) { ["actor:globalAdmin"] = "true" },
        };
        var request = new HttpRequestMessage(HttpMethod.Post, CommandsRoute)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonSerializerOptions.Web), Encoding.UTF8, "application/json"),
        };
        AddInternalHeaders(request, assertion, includeChannelToken: true);
        return request;
    }

    private static void AddInternalHeaders(HttpRequestMessage request, string? assertion, bool includeChannelToken)
    {
        request.Headers.Add("X-Correlation-ID", "story-5-5-correlation");
        if (includeChannelToken)
        {
            request.Headers.Add(DaprAppChannelToken.HeaderName, ChannelToken);
        }

        if (assertion is not null)
        {
            request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
        }
    }

    /// <summary>The real EventStore host with an allow-listed internal caller and substitute downstream services.</summary>
    public sealed class InternalBoundaryFactory : WebApplicationFactory<EventStoreProgram>
    {
        public ITrustedEffectAdmissionPolicy Admission { get; } = Substitute.For<ITrustedEffectAdmissionPolicy>();

        public ITrustedEffectGatewayProof Proof { get; } = Substitute.For<ITrustedEffectGatewayProof>();

        public ITrustedEffectRouter Router { get; } = Substitute.For<ITrustedEffectRouter>();

        public ICommandRouter CommandRouter { get; } = Substitute.For<ICommandRouter>();

        public CapturingLoggerProvider Logs { get; } = new();

        public void Reset()
        {
            Admission.ClearReceivedCalls();
            Proof.ClearReceivedCalls();
            Router.ClearReceivedCalls();
            CommandRouter.ClearReceivedCalls();
            Logs.Clear();
            _ = Admission.AdmitAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<CancellationToken>())
                .Returns(call => new TrustedEffectAdmission(call.ArgAt<TrustedEffectSubmission>(0), call.ArgAt<TrustedEffectContext>(1), "DIGEST"));
            _ = Proof.SignAsync(Arg.Any<TrustedEffectAdmission>(), Arg.Any<CancellationToken>()).Returns("gateway-proof");
            _ = Router.RouteAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new TrustedEffectResult("effect", TrustedEffectDisposition.Success, Replayed: false, null));
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            _ = builder.UseEnvironment(Environments.Development);
            _ = builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DaprAppChannelToken.ConfigurationKey] = ChannelToken,
                ["Authentication:DaprInternal:AllowedCallers:0"] = AllowedCaller,
            }));
            _ = builder.ConfigureLogging(logging => logging.AddProvider(Logs));
            _ = builder.ConfigureTestServices(services =>
            {
                WebApplicationFactoryServiceOverrides.RemoveAdminOperationalIndexHostedService(services);
                services.RemoveAll<ITrustedEffectAdmissionPolicy>();
                services.RemoveAll<ITrustedEffectGatewayProof>();
                services.RemoveAll<ITrustedEffectRouter>();
                services.RemoveAll<ICommandRouter>();
                services.RemoveAll<Hexalith.EventStore.Server.Projections.IProjectionActivationOutbox>();
                services.RemoveAll<ICommandCorrelationIndex>();
                _ = services.AddSingleton(Substitute.For<Hexalith.EventStore.Server.Projections.IProjectionActivationOutbox>());
                _ = services.AddSingleton(Substitute.For<ICommandCorrelationIndex>());
                _ = services.AddSingleton(Admission);
                _ = services.AddSingleton(Proof);
                _ = services.AddSingleton(Router);
                _ = services.AddSingleton(CommandRouter);
            });
        }
    }

    /// <summary>Simulates the receiving gateway sidecar: it presents the app-channel token and a correlation ID.</summary>
    private sealed class GatewaySidecarHandler(string channelToken) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.Headers.Add(DaprAppChannelToken.HeaderName, channelToken);
            request.Headers.Add("X-Correlation-ID", "story-5-5-correlation");
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
