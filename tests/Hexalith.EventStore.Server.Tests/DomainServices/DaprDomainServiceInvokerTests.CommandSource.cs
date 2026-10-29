using System.Security.Cryptography;
using System.Text;
using Dapr.Client;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

public partial class DaprDomainServiceInvokerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompleteSourceConcreteInvokerBindsSerializedHttpRequestThroughHandlerAndSdkReceiver(bool nearLimit)
    {
        var command = CreateTestEnvelope(); var identity = command.AggregateIdentity;
        byte[] payload = nearLimit ? Encoding.UTF8.GetBytes("{\"data\":\"" + new string('a', CompleteCommandSource.MaximumPayloadBytes - 1035) + "\"}") : "{}"u8.ToArray();
        var item = new StreamReadEvent(1, "Created", payload, "json", 1, "event-1", "correlation", "cause", DateTimeOffset.UtcNow, "worker") { NativeEventDigest = new string('a', 64) };
        var native = NativeSourceProofCodec.ExtendDigest(NativeSourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV"), identity, 1, item.MessageId, item.NativeEventDigest);
        var source = new CompleteCommandSource(identity, "epoch-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(2), native, [item]);
        var state = new DomainServiceCurrentState(null, [new(new(item.MessageId, identity.AggregateId, "Aggregate", identity.TenantId, identity.Domain, 1, 0,
            item.Timestamp, "correlation", "cause", "worker", "v1", item.EventTypeName, 1, "json"), payload, null)], 0, 1);
        const string key = "complete-source-component-key-at-least-thirty-two-bytes";
        string appChannelToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var processor = Substitute.For<IDomainProcessor>(); bool invoked = false;
        processor.ProcessAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(async _ =>
        {
            await Task.Yield(); invoked = true;
            var admitted = CompleteCommandSourceContext.Current.ShouldNotBeNull(); admitted.Identity.ShouldBe(identity); admitted.Proof.ShouldBe(native);
            CryptographicOperations.FixedTimeEquals(admitted.Events.Single().Payload, payload).ShouldBeTrue();
            return DomainResult.NoOp();
        });
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Authority"] = "", ["Authentication:JwtBearer:Issuer"] = WorkloadAssertionTestTokens.Issuer,
            ["Authentication:JwtBearer:Audience"] = "test-app", ["Authentication:JwtBearer:SigningKey"] = key,
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false", ["Authentication:Workload:Audience"] = "test-app",
            ["Authentication:Workload:AllowedCallers:0"] = "eventstore", ["APP_API_TOKEN"] = appChannelToken,
        });
        builder.AddEventStoreDomainService(typeof(CommandEnvelope).Assembly);
        // This original source/wire fixture isolates catalog authority while retaining every credential,
        // serialized-binding and source assertion; activated HTTP tests exercise catalog enforcement.
        builder.Services.AddSingleton<IDomainRouteCatalogGuard, CommandSourceCatalogIsolatedGuard>();
        builder.Services.AddEventStoreCompleteCommandSourceVerification("eventstore", [command.Domain]); builder.Services.AddKeyedSingleton(command.Domain, processor);
        await using var app = builder.Build();
        app.UseEventStoreDomainService(); await app.StartAsync(TestContext.Current.CancellationToken);
        var issuer = Substitute.For<IWorkloadAssertionIssuer>(); issuer.CanBindResources.Returns(true);
        issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<WorkloadAssertionRequest>();
            return ValueTask.FromResult<string?>(WorkloadAssertionTestTokens.Create(request.Audience, "eventstore", [request.Operation], bindings: request.Bindings, signingKey: key));
        });
        using var services = new ServiceCollection().AddSingleton(issuer).BuildServiceProvider();
        using var forward = new CompleteCommandSourceForwardingHandler(app.GetTestServer().CreateHandler(), appChannelToken);
        using var handler = new DomainServiceWorkloadAssertionHandler(services) { InnerHandler = forward };
        using var client = new HttpClient(handler); using var dapr = new DaprClientBuilder().Build();
        var clients = Substitute.For<IHttpClientFactory>(); clients.CreateClient(DaprDomainServiceInvoker.HttpClientName).Returns(client);
        _resolver.ResolveAsync(command.TenantId, command.Domain, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(
            new DomainServiceRegistration("test-app", "process", command.TenantId, command.Domain, null));
        var invoker = new DaprDomainServiceInvoker(dapr, clients, _resolver, _options, TimeProvider.System, _logger);
        (await invoker.InvokeCompleteSourceAsync(command, state, source, null, null, TestContext.Current.CancellationToken)).IsNoOp.ShouldBeTrue();
        invoked.ShouldBeTrue(); forward.Assertion.ShouldNotBeNull();
        await issuer.Received(1).IssueAsync(Arg.Is<WorkloadAssertionRequest>(request => request.Bindings != null && request.Bindings.ContainsKey(CommandSourceAuthorityBinding.DigestClaim)), Arg.Any<CancellationToken>());
        if (nearLimit) forward.RequestBytes.ShouldBeGreaterThan(40L * 1024 * 1024);
        Console.WriteLine($"Complete-source HTTP request bytes={forward.RequestBytes}; application event bytes={payload.Length}; nearLimit={nearLimit}.");
    }
}
