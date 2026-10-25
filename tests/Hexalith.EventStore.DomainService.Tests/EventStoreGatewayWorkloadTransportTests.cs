using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Verifies supported gateway routing and exact workload grants through the real SDK issuer.</summary>
public sealed class EventStoreGatewayWorkloadTransportTests
{
    /// <summary>Ordinary client calls opt into scoped workload transport without forwarding human authority.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingClientCallsUseWorkloadRoutesWithExactIndependentGrants(bool command)
    {
        var capture = new CapturingHandler();
        await using ServiceProvider services = Build(capture);
        IEventStoreGatewayClient client = services.GetRequiredService<IEventStoreGatewayClient>();
        if (command)
        {
            _ = await client.SubmitCommandAsync(new SubmitCommandRequest("message", "tenant-a", "work", "item-1", "ResumeWorkItem",
                JsonSerializer.SerializeToElement(new { })), TestContext.Current.CancellationToken);
        }
        else
        {
            _ = await client.ReadStreamAsync(new StreamReadRequest("tenant-a", "work", "item-1"), TestContext.Current.CancellationToken);
        }

        capture.Path.ShouldBe(command ? "/api/v1/commands/workload" : "/api/v1/streams/read/workload");
        capture.Authorization.ShouldBeNull();
        var token = new JsonWebToken(capture.Assertion.ShouldNotBeNull());
        token.Audiences.ShouldBe(["eventstore"]);
        token.GetClaim(EventStoreWorkloadAuthenticationDefaults.CallerClaimType).Value.ShouldBe("works-reader");
        token.GetClaim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType).Value.ShouldBe(command
            ? EventStoreGatewayWorkloadOperations.CommandSubmit : EventStoreGatewayWorkloadOperations.StreamRead);
        token.GetClaim(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType).Value.ShouldBe("tenant-a");
        token.GetClaim(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType).Value.ShouldBe("work");
        token.TryGetClaim("global_admin", out _).ShouldBeFalse();
    }

    /// <summary>Unavailable authority never falls back to an unbound or human request.</summary>
    [Fact]
    public async Task MissingIssuerAndUnrelatedOperationsCannotFallBackToHumanTransport()
    {
        var capture = new CapturingHandler();
        await using ServiceProvider services = Build(capture, configured: false);
        IEventStoreGatewayClient client = services.GetRequiredService<IEventStoreGatewayClient>();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => client.ReadStreamAsync(
            new StreamReadRequest("tenant-a", "work", "item-1"), TestContext.Current.CancellationToken));
        capture.Path.ShouldBeNull();
        (await services.GetRequiredService<IEventStoreGatewayWorkloadAssertionSource>().IssueAsync(
            EventStoreWorkloadOperations.TrustedEffect, "tenant-a", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>Multi-domain hosts issue one exact grant per request and reject undeclared domains before HTTP.</summary>
    [Fact]
    public async Task MultipleDomainsAreBoundToEachRequestWithoutGrantingTheWholeAllowlist()
    {
        var capture = new CapturingHandler();
        await using ServiceProvider services = Build(capture, domains: ["memories-tenants", "memories-cases", "memories-units"]);
        IEventStoreGatewayClient client = services.GetRequiredService<IEventStoreGatewayClient>();
        foreach (string domain in new[] { "memories-tenants", "memories-cases", "memories-units" })
        {
            _ = await client.SubmitCommandAsync(new SubmitCommandRequest("message", "tenant-a", domain, "item", "Command",
                JsonSerializer.SerializeToElement(new { })), TestContext.Current.CancellationToken);
            var token = new JsonWebToken(capture.Assertion!);
            token.GetClaim(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType).Value.ShouldBe(domain);
            token.GetClaim(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType).Value.ShouldBe("tenant-a");
        }
        string? prior = capture.Assertion;
        _ = await Should.ThrowAsync<InvalidOperationException>(() => client.SubmitCommandAsync(
            new SubmitCommandRequest("message", "tenant-a", "other", "item", "Command", JsonSerializer.SerializeToElement(new { })),
            TestContext.Current.CancellationToken));
        capture.Assertion.ShouldBe(prior);
        (await services.GetRequiredService<IEventStoreGatewayWorkloadAssertionSource>().IssueAsync(
            EventStoreGatewayWorkloadOperations.CommandSubmit, "tenant-a", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task ExistingStringRegistrationPreservesWhitespaceNormalizationAndNullGuard()
    {
        var capture = new CapturingHandler();
        await using ServiceProvider services = Build(capture, singleDomain: "  work  ");
        _ = await services.GetRequiredService<IEventStoreGatewayClient>().SubmitCommandAsync(
            new SubmitCommandRequest("message", "tenant-a", "work", "item", "Command", JsonSerializer.SerializeToElement(new { })),
            TestContext.Current.CancellationToken);
        new JsonWebToken(capture.Assertion!).GetClaim(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType).Value.ShouldBe("work");
        // Literal null keeps its original unambiguous string overload and guard.
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddEventStoreGatewayClient(_ => { })
            .AddEventStoreGatewayWorkloadTransport(null!, "eventstore"));
    }

    [Fact]
    public async Task BoundCommandRetainsExactPrincipalPayloadMetadataAndIndependentDomainGrant()
    {
        using RSA rsa = RSA.Create(2048);
        string key = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());
        var capture = new CapturingHandler();
        await using ServiceProvider services = Build(capture, proofKey: key);
        var command = new SubmitCommandRequest("message", "tenant-a", "work", "unit", "Command", JsonSerializer.SerializeToElement(new { value = 1 }),
            Extensions: new Dictionary<string, string> { ["actorId"] = "actor", ["source"] = "owned-source" });
        var request = new BoundWorkloadCommandRequest(command, "actor");
        IEventStoreGatewayBoundCommandProofSource source = services.GetRequiredService<IEventStoreGatewayBoundCommandProofSource>();
        string proof = (await source.IssueAsync(request, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        _ = await services.GetRequiredService<IEventStoreGatewayClient>().SubmitBoundWorkloadCommandAsync(request, proof, TestContext.Current.CancellationToken);
        capture.Path.ShouldBe("/api/v1/commands/bound/workload");
        capture.Authorization.ShouldBeNull();
        BoundWorkloadCommandRequest sent = JsonSerializer.Deserialize<BoundWorkloadCommandRequest>(capture.Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        sent.PrincipalId.ShouldBe("actor");
        sent.Command.Extensions.ShouldNotBeNull()["source"].ShouldBe("owned-source");
        string publicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        BoundWorkloadCommandProof.Verify(sent, capture.CommandProof, "works-reader", publicKey, DateTimeOffset.UtcNow).ShouldBeTrue();
        BoundWorkloadCommandProof.Verify(sent with { PrincipalId = "other" }, proof, "works-reader", publicKey, DateTimeOffset.UtcNow).ShouldBeFalse();
        BoundWorkloadCommandProof.Verify(sent with { Command = command with { Domain = "other" } }, proof, "works-reader", publicKey, DateTimeOffset.UtcNow).ShouldBeFalse();
        BoundWorkloadCommandProof.Verify(sent with { Command = command with { Tenant = "other" } }, proof, "works-reader", publicKey, DateTimeOffset.UtcNow).ShouldBeFalse();
        BoundWorkloadCommandProof.Verify(sent, proof + "x", "works-reader", publicKey, DateTimeOffset.UtcNow).ShouldBeFalse();
        BoundWorkloadCommandProof.Verify(sent, proof, "other-caller", publicKey, DateTimeOffset.UtcNow).ShouldBeFalse();
        (await source.IssueAsync(request with { Command = command with { Domain = "other" } }, TestContext.Current.CancellationToken)).ShouldBeNull();
        var assertion = new JsonWebToken(capture.Assertion!);
        assertion.GetClaim(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType).Value.ShouldBe("tenant-a");
        assertion.GetClaim(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType).Value.ShouldBe("work");
    }

    [Fact]
    public async Task BoundCommandCannotSendWithoutItsIndependentWorkloadCredential()
    {
        var capture = new CapturingHandler();
        await using ServiceProvider services = Build(capture, configured: false);
        var request = new BoundWorkloadCommandRequest(new SubmitCommandRequest("message", "tenant-a", "work", "unit", "Command", JsonSerializer.SerializeToElement(new { })), "actor");
        await Should.ThrowAsync<InvalidOperationException>(() => services.GetRequiredService<IEventStoreGatewayClient>()
            .SubmitBoundWorkloadCommandAsync(request, "a-proof-cannot-substitute-for-credentials", TestContext.Current.CancellationToken));
        capture.Path.ShouldBeNull();
    }

    [Fact]
    public async Task BoundCommandRejectsAReceiptForAnotherMessage()
    {
        using RSA rsa = RSA.Create(2048);
        var capture = new CapturingHandler();
        await using ServiceProvider services = Build(capture, proofKey: Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()));
        var request = new BoundWorkloadCommandRequest(new SubmitCommandRequest("expected-message", "tenant-a", "work", "unit", "Command", JsonSerializer.SerializeToElement(new { })), "actor");
        string proof = (await services.GetRequiredService<IEventStoreGatewayBoundCommandProofSource>().IssueAsync(request, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        await Should.ThrowAsync<EventStoreGatewayException>(() => services.GetRequiredService<IEventStoreGatewayClient>()
            .SubmitBoundWorkloadCommandAsync(request, proof, TestContext.Current.CancellationToken));
    }

    /// <summary>Status assertions carry only the requested declared domain and never an allowlist grant.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StatusGrantsUseTheRealIssuerAndOneDeclaredDomain(bool multipleDomains)
    {
        var capture = new CapturingHandler();
        await using ServiceProvider services = Build(capture, domains: multipleDomains ? ["work", "other-work"] : null);
        IEventStoreGatewayWorkloadAssertionSource source = services.GetRequiredService<IEventStoreGatewayWorkloadAssertionSource>();
        string assertion = (await source.IssueAsync(EventStoreGatewayWorkloadOperations.CommandStatus,
            "tenant-a", "work", TestContext.Current.CancellationToken)).ShouldNotBeNull();
        var token = new JsonWebToken(assertion);
        token.Audiences.ShouldBe(["eventstore"]);
        token.GetClaim(EventStoreWorkloadAuthenticationDefaults.CallerClaimType).Value.ShouldBe("works-reader");
        token.GetClaim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType).Value.ShouldBe(EventStoreGatewayWorkloadOperations.CommandStatus);
        token.GetClaim(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType).Value.ShouldBe("tenant-a");
        token.GetClaim(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType).Value.ShouldBe("work");
        token.TryGetClaim("global_admin", out _).ShouldBeFalse();
        (await source.IssueAsync(EventStoreGatewayWorkloadOperations.CommandStatus, "tenant-a", "undeclared",
            TestContext.Current.CancellationToken)).ShouldBeNull();
        (await source.IssueAsync(EventStoreWorkloadOperations.TrustedEffect, "tenant-a", "work",
            TestContext.Current.CancellationToken)).ShouldBeNull();
        if (multipleDomains)
        {
            (await source.IssueAsync(EventStoreGatewayWorkloadOperations.CommandStatus, "tenant-a",
                TestContext.Current.CancellationToken)).ShouldBeNull();
        }
    }

    /// <summary>A status operation has no credentials when the issuer is unconfigured.</summary>
    [Fact]
    public async Task StatusGrantsRequireIndependentIssuerCredentials()
    {
        await using ServiceProvider services = Build(new CapturingHandler(), configured: false);
        (await services.GetRequiredService<IEventStoreGatewayWorkloadAssertionSource>().IssueAsync(
            EventStoreGatewayWorkloadOperations.CommandStatus, "tenant-a", "work",
            TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    private static ServiceProvider Build(CapturingHandler capture, bool configured = true, IReadOnlyCollection<string>? domains = null, string singleDomain = "work", string? proofKey = null)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Issuer"] = "hexalith-dev",
            ["Authentication:JwtBearer:Audience"] = "hexalith-eventstore",
            ["Authentication:JwtBearer:SigningKey"] = configured ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) : null,
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256",
            ["Authentication:WorkloadIssuer:Workload"] = "works-reader",
        }).Build();
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton(configuration);
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);
        _ = services.AddSingleton(environment);
        IHttpClientBuilder gateway = services.AddEventStoreGatewayClient(options => options.BaseAddress = new Uri("http://localhost/"));
        _ = domains is null
            ? gateway.AddEventStoreGatewayWorkloadTransport(singleDomain)
            : gateway.AddEventStoreGatewayMultiDomainWorkloadTransport(domains);
        _ = gateway.ConfigurePrimaryHttpMessageHandler(() => capture);
        if (proofKey is not null) { _ = gateway.AddEventStoreGatewayBoundCommandProof("works-reader", proofKey, domains ?? ["work"]); }
        return services.BuildServiceProvider();
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Path { get; private set; }
        public string? Assertion { get; private set; }
        public string? Authorization { get; private set; }

        public string? CommandProof { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            CommandProof = request.Headers.TryGetValues(BoundWorkloadCommandProof.HeaderName, out IEnumerable<string>? values) ? values.Single() : null;
            Path = request.RequestUri!.AbsolutePath;
            Authorization = request.Headers.Authorization?.ToString();
            Assertion = request.Headers.GetValues(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName).Single();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Path.Contains("commands", StringComparison.Ordinal)
                    ? JsonContent.Create(new SubmitCommandResponse("correlation", MessageId: "message"))
                    : JsonContent.Create(new StreamReadPage("tenant-a", "work", "item-1", [], new(0, null, null, 0, 0, false, null))),
            };
        }
    }
}
