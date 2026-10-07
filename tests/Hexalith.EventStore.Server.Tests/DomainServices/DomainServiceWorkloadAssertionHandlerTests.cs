using System.Net;
using System.Security.Cryptography;

using Dapr.Client;

using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

/// <summary>
/// Story 5.5 (FR28, AD-18): every domain-service invocation leaving EventStore carries a fresh workload assertion for
/// exactly the receiving application and operation, and never a caller-supplied assertion or human bearer.
/// </summary>
public sealed class DomainServiceWorkloadAssertionHandlerTests
{
    /// <summary>The Dapr invocation URI resolves to the receiving application and the catalog operation.</summary>
    /// <param name="uri">The invocation URI.</param>
    /// <param name="expectedAppId">The expected receiving application, or <see langword="null"/>.</param>
    /// <param name="expectedOperation">The expected operation, or <see langword="null"/>.</param>
    [Theory]
    [InlineData("http://127.0.0.1:3501/v1.0/invoke/sample/method/process", "sample", EventStoreWorkloadOperations.DomainServiceProcess)]
    [InlineData("http://127.0.0.1:3501/v1.0/invoke/tenants/method/replay-state", "tenants", EventStoreWorkloadOperations.DomainServiceReplayState)]
    [InlineData("http://127.0.0.1:3501/v1.0/invoke/tenants/method/query", "tenants", EventStoreWorkloadOperations.DomainServiceQuery)]
    [InlineData("http://127.0.0.1:3501/v1.0/invoke/tenants/method/project", "tenants", EventStoreWorkloadOperations.DomainServiceProject)]
    [InlineData("http://127.0.0.1:3501/v1.0/invoke/tenants/method/project/v2/reconcile", "tenants", EventStoreWorkloadOperations.DomainServiceProject)]
    [InlineData("http://127.0.0.1:3501/v1.0/invoke/tenants/method/project/rebuild/shared/v1", "tenants", EventStoreWorkloadOperations.DomainServiceProject)]
    [InlineData("http://127.0.0.1:3501/v1.0/invoke/sample/method/admin/operational-index-metadata", "sample", EventStoreWorkloadOperations.DomainServiceMetadata)]
    [InlineData("http://127.0.0.1:3501/v1.0/invoke/eventstore/method/api/v1/commands", null, null)]
    [InlineData("http://127.0.0.1:3501/v1.0/state/statestore", null, null)]
    [InlineData("https://identity.example.test/realms/hexalith/protocol/openid-connect/token", null, null)]
    public void TryGetDomainServiceTarget_ResolvesOnlyCanonicalDomainServiceMethods(
        string uri,
        string? expectedAppId,
        string? expectedOperation)
    {
        bool resolved = DomainServiceWorkloadAssertionHandler.TryGetDomainServiceTarget(
            new Uri(uri),
            out string? appId,
            out EventStoreDomainServiceRoute? route);

        resolved.ShouldBe(expectedAppId is not null);
        appId.ShouldBe(expectedAppId);
        route?.Operation.ShouldBe(expectedOperation);
    }

    /// <summary>
    /// A forwarded human bearer and a caller-supplied assertion are removed and replaced by a fresh assertion.
    /// </summary>
    [Fact]
    public async Task SendAsync_ReplacesForwardedCredentialsWithFreshAssertion()
    {
        IWorkloadAssertionIssuer issuer = Substitute.For<IWorkloadAssertionIssuer>();
        _ = issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("fresh-assertion"));
        var capture = new CapturingHandler();
        using var handler = new DomainServiceWorkloadAssertionHandler(IssuerProvider(issuer))
        {
            InnerHandler = capture,
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:3501/v1.0/invoke/sample/method/process");
        string forwardedHumanCredential = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
        request.Headers.Add("Authorization", "Bearer " + forwardedHumanCredential);
        request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, "caller-supplied-assertion");

        using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        capture.Authorization.ShouldBeNull();
        capture.Assertions.ShouldBe(["fresh-assertion"]);
        _ = await issuer.Received(1).IssueAsync(
            Arg.Is<WorkloadAssertionRequest>(assertion => assertion.Audience == "sample"
                && assertion.Operation == EventStoreWorkloadOperations.DomainServiceProcess
                && assertion.Bindings == null),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Requests that do not invoke a canonical domain-service method are left untouched.</summary>
    [Fact]
    public async Task SendAsync_LeavesOtherRequestsUntouched()
    {
        IWorkloadAssertionIssuer issuer = Substitute.For<IWorkloadAssertionIssuer>();
        var capture = new CapturingHandler();
        using var handler = new DomainServiceWorkloadAssertionHandler(IssuerProvider(issuer))
        {
            InnerHandler = capture,
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:3501/v1.0/invoke/eventstore/method/api/v1/commands");
        string adminUserCredential = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
        request.Headers.Add("Authorization", "Bearer " + adminUserCredential);

        using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        capture.Authorization.ShouldBe("Bearer " + adminUserCredential);
        capture.Assertions.ShouldBeEmpty();
        _ = await issuer.DidNotReceiveWithAnyArgs().IssueAsync(default!, default);
    }

    /// <summary>
    /// End to end: every factory client built by <c>AddEventStoreServer</c> carries the platform handler, and the
    /// assertion it attaches validates on a domain-service receiver for exactly that application and operation.
    /// </summary>
    [Fact]
    public async Task EventStoreServerClients_AttachAssertionsThatTheDomainServiceReceiverAccepts()
    {
        Dictionary<string, string?> settings = SymmetricContract();
        var capture = new CapturingHandler();
        IServiceCollection services = CreateHostServices(settings);
        _ = services.AddEventStoreServer(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        _ = services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => capture));
        await using ServiceProvider eventStore = services.BuildServiceProvider();
        HttpClient client = eventStore.GetRequiredService<IHttpClientFactory>().CreateClient();

        using HttpResponseMessage response = await client.PostAsync(
            "http://127.0.0.1:3501/v1.0/invoke/sample/method/process",
            new StringContent("{}"),
            TestContext.Current.CancellationToken);

        string assertion = capture.Assertions.ShouldHaveSingleItem();
        await using ServiceProvider receiver = CreateReceiver(settings, audience: "sample");
        WorkloadAssertionValidator validator = receiver.GetRequiredService<WorkloadAssertionValidator>();
        WorkloadAssertionEvaluation accepted = await validator.ValidateAsync(
            EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
            assertion,
            EventStoreWorkloadOperations.DomainServiceProcess,
            allowedCallersOverride: null,
            TestContext.Current.CancellationToken);
        accepted.Succeeded.ShouldBeTrue(accepted.ReasonCode);
        accepted.Caller.ShouldBe(EventStoreServerServiceCollectionExtensions.EventStoreWorkloadIdentity);
        accepted.Principal!.FindAll("global_admin").ShouldBeEmpty();
        accepted.Principal.FindAll("eventstore:tenant").ShouldBeEmpty();

        WorkloadAssertionEvaluation wrongOperation = await validator.ValidateAsync(
            EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
            assertion,
            EventStoreWorkloadOperations.DomainServiceQuery,
            allowedCallersOverride: null,
            TestContext.Current.CancellationToken);
        wrongOperation.ReasonCode.ShouldBe(WorkloadAuthenticationReasons.OperationNotGranted);

        await using ServiceProvider otherReceiver = CreateReceiver(settings, audience: "tenants");
        WorkloadAssertionEvaluation wrongAudience = await otherReceiver.GetRequiredService<WorkloadAssertionValidator>().ValidateAsync(
            EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
            assertion,
            EventStoreWorkloadOperations.DomainServiceProcess,
            allowedCallersOverride: null,
            TestContext.Current.CancellationToken);
        wrongAudience.ReasonCode.ShouldBe(WorkloadAuthenticationReasons.AudienceInvalid);
    }

    /// <summary>
    /// Story 5.5 (BS-C): in authority mode, when EventStore invokes domain service X for operation O, the attached
    /// assertion is the authority token requested for exactly X and O; a broader token from the authority is never sent.
    /// </summary>
    /// <param name="shape">The authority's token shape.</param>
    [Theory]
    [InlineData("exact")]
    [InlineData("extra-audience")]
    [InlineData("all-operations")]
    public async Task EventStoreServerClients_InAuthorityMode_AttachOnlyTheExactAudienceAndOperationToken(string shape)
    {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Authentication:JwtBearer:Authority"] = ScopedWorkloadTokenEndpoint.Authority,
            ["Authentication:JwtBearer:Issuer"] = ScopedWorkloadTokenEndpoint.Authority,
            ["Authentication:JwtBearer:Audience"] = "hexalith-eventstore",
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "RS256",
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "true",
            ["Authentication:WorkloadIssuer:ClientId"] = "eventstore",
            ["Authentication:WorkloadIssuer:ClientSecret"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            ["Authentication:WorkloadIssuer:TokenEndpoint"] = ScopedWorkloadTokenEndpoint.Authority + "/protocol/openid-connect/token",
        };
        var capture = new CapturingHandler();
        var authority = new ScopedWorkloadTokenEndpoint(shape);
        IServiceCollection services = CreateHostServices(settings, Environments.Production);
        _ = services.AddEventStoreServer(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        _ = services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => capture));
        _ = services.AddHttpClient(JwtWorkloadAssertionIssuer.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => authority);
        await using ServiceProvider eventStore = services.BuildServiceProvider();
        HttpClient client = eventStore.GetRequiredService<IHttpClientFactory>().CreateClient();

        using HttpResponseMessage process = await client.PostAsync(
            "http://127.0.0.1:3501/v1.0/invoke/sample/method/process",
            new StringContent("{}"),
            TestContext.Current.CancellationToken);
        IReadOnlyList<string> processAssertions = capture.Assertions;
        using HttpResponseMessage query = await client.PostAsync(
            "http://127.0.0.1:3501/v1.0/invoke/tenants/method/query",
            new StringContent("{}"),
            TestContext.Current.CancellationToken);
        IReadOnlyList<string> queryAssertions = capture.Assertions;

        authority.Scopes.ShouldBe(
        [
            "eventstore-audience.sample eventstore-operation.domain-service.process",
            "eventstore-audience.tenants eventstore-operation.domain-service.query",
        ]);
        if (shape == "exact")
        {
            ScopedWorkloadTokenEndpoint.AssertExactlyScoped(processAssertions.ShouldHaveSingleItem(), "sample", EventStoreWorkloadOperations.DomainServiceProcess);
            ScopedWorkloadTokenEndpoint.AssertExactlyScoped(queryAssertions.ShouldHaveSingleItem(), "tenants", EventStoreWorkloadOperations.DomainServiceQuery);
        }
        else
        {
            // The receiver denies the unasserted call; a broader token never leaves EventStore.
            processAssertions.ShouldBeEmpty();
            queryAssertions.ShouldBeEmpty();
        }
    }

    /// <summary>
    /// A host may replace <see cref="TimeProvider"/> for business time. Workload-assertion security uses its own wall
    /// clock, so a host clock frozen far in the past changes neither the issued assertion's times and lifetime nor the
    /// receiver's acceptance of it.
    /// </summary>
    [Fact]
    public async Task HostTimeProviderFrozenInThePast_DoesNotChangeIssuedLifetimesOrReceiverAcceptance()
    {
        var frozen = new FakeTimeProvider(new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Dictionary<string, string?> settings = SymmetricContract();
        var capture = new CapturingHandler();
        IServiceCollection services = CreateHostServices(settings);
        _ = services.AddSingleton<TimeProvider>(frozen);
        _ = services.AddEventStoreServer(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        _ = services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => capture));
        await using ServiceProvider eventStore = services.BuildServiceProvider();
        DateTimeOffset before = DateTimeOffset.UtcNow;

        using HttpResponseMessage response = await eventStore.GetRequiredService<IHttpClientFactory>().CreateClient().PostAsync(
            "http://127.0.0.1:3501/v1.0/invoke/sample/method/process",
            new StringContent("{}"),
            TestContext.Current.CancellationToken);

        var token = new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(capture.Assertions.ShouldHaveSingleItem());
        eventStore.GetRequiredService<TimeProvider>().ShouldBeSameAs(frozen);
        token.IssuedAt.ShouldBeGreaterThanOrEqualTo(before.UtcDateTime.AddSeconds(-1));
        token.IssuedAt.ShouldBeLessThanOrEqualTo(DateTime.UtcNow.AddSeconds(1));
        (token.ValidTo - token.IssuedAt).ShouldBe(TimeSpan.FromSeconds(WorkloadAssertionIssuerOptions.DefaultLifetimeSeconds));

        var receiverSettings = new Dictionary<string, string?>(settings, StringComparer.Ordinal)
        {
            ["Authentication:Workload:Audience"] = "sample",
            ["Authentication:Workload:AllowedCallers:0"] = "eventstore",
        };
        IServiceCollection receiverServices = CreateHostServices(receiverSettings);
        _ = receiverServices.AddSingleton<TimeProvider>(frozen);
        _ = receiverServices.AddAuthentication().AddEventStoreWorkloadScheme(
            EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
            "Authentication:Workload");
        await using ServiceProvider receiver = receiverServices.BuildServiceProvider();
        WorkloadAssertionEvaluation accepted = await receiver.GetRequiredService<WorkloadAssertionValidator>().ValidateAsync(
            EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
            token.EncodedToken,
            EventStoreWorkloadOperations.DomainServiceProcess,
            allowedCallersOverride: null,
            TestContext.Current.CancellationToken);
        accepted.Succeeded.ShouldBeTrue(accepted.ReasonCode);
    }

    private static ServiceProvider IssuerProvider(IWorkloadAssertionIssuer issuer)
        => new ServiceCollection().AddSingleton(issuer).BuildServiceProvider();

    private static Dictionary<string, string?> SymmetricContract()
        => new(StringComparer.Ordinal)
        {
            ["Authentication:JwtBearer:Issuer"] = "hexalith-dev",
            ["Authentication:JwtBearer:Audience"] = "hexalith-eventstore",
            ["Authentication:JwtBearer:SigningKey"] = AuthenticationTestEnvironment.SigningKey,
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256",
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
        };

    private static IServiceCollection CreateHostServices(Dictionary<string, string?> settings, string environmentName = "Development")
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        environment.ApplicationName.Returns("Hexalith.EventStore");
        _ = services.AddSingleton(environment);
        _ = services.AddSingleton(Substitute.For<DaprClient>());
        return services;
    }

    private static ServiceProvider CreateReceiver(Dictionary<string, string?> settings, string audience)
    {
        var receiverSettings = new Dictionary<string, string?>(settings, StringComparer.Ordinal)
        {
            ["Authentication:Workload:Audience"] = audience,
            ["Authentication:Workload:AllowedCallers:0"] = "eventstore",
        };
        IServiceCollection services = CreateHostServices(receiverSettings);
        _ = services.AddAuthentication().AddEventStoreWorkloadScheme(
            EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
            "Authentication:Workload");
        return services.BuildServiceProvider();
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }

        public IReadOnlyList<string> Assertions { get; private set; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.TryGetValues("Authorization", out IEnumerable<string>? authorization)
                ? authorization.Single()
                : null;
            Assertions = request.Headers.TryGetValues(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, out IEnumerable<string>? values)
                ? [.. values]
                : [];
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
