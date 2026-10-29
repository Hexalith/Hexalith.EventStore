using System.Net;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogConcreteCommandInvokerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("V2")]
    [InlineData("v2")]
    [InlineData("v1")]
    [InlineData("invalid")]
    public async Task ConcreteInvokerUsesCommittedVersionUnlessAnExplicitOverrideConflicts(string? version)
    {
        var fixture = new RouteCatalogTestFixture();
        var changed = fixture.Envelope.Declarations.Single(d => d.RouteId == "counter-increment-counter") with { ContractVersion = "v2" };
        var envelope = RouteCatalogCodec.Seal(fixture.Envelope with
        {
            Declarations = fixture.Envelope.Declarations.Select(d => d.RouteId == changed.RouteId ? changed : d).ToArray(),
            Routes = fixture.Envelope.Routes.Select(r => r.RouteId == changed.RouteId ? r with
            { ContractVersion = "v2", DeclarationDigest = RouteCatalogCodec.ComputeDeclarationDigest(changed) } : r).ToArray(),
        });
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 7);
        IRouteCatalogAuthority authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => new RouteCatalogDispatchLease(snapshot, () => Task.CompletedTask));
        int effects = 0;
        string? observedRoot = null;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var sink = builder.Build();
        sink.MapPost("/v1.0/invoke/sample/method/process", async (HttpContext context) =>
        {
            Interlocked.Increment(ref effects);
            observedRoot = context.Request.Headers[RouteCatalogInvocationHandler.RootHeader];
            context.Request.Headers[RouteCatalogInvocationHandler.EpochHeader].ToString().ShouldBe("7");
            var request = await JsonSerializer.DeserializeAsync<DomainServiceRequest>(context.Request.Body, JsonSerializerOptions.Web);
            request!.Command.CommandType.ShouldBe("increment-counter");
            return Results.Json(new DomainServiceWireResult(false, []));
        });
        await sink.StartAsync();
        using var dapr = new DaprClientBuilder().UseHttpEndpoint(sink.Urls.Single()).Build();
        using var client = new HttpClient(new RouteCatalogInvocationHandler(authority, new SocketsHttpHandler()));
        IHttpClientFactory clients = Substitute.For<IHttpClientFactory>(); clients.CreateClient(Arg.Any<string>()).Returns(client);
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        var invoker = new DaprDomainServiceInvoker(authority, dapr, clients, resolver, Options.Create(new DomainServiceOptions()), TimeProvider.System, NullLogger<DaprDomainServiceInvoker>.Instance);
        var command = new CommandEnvelope("01ARZ3NDEKTSV4RRFFQ69G5FAV", "tenant-a", "counter", "aggregate", "increment-counter", "{}"u8.ToArray(), "correlation", null, "user",
            version is null ? null : new() { [DaprDomainServiceInvoker.DomainServiceVersionExtensionKey] = version });
        if (version == "v1") await Should.ThrowAsync<RouteCatalogUnavailableException>(() => invoker.InvokeAsync(command, null));
        else if (version == "invalid") await Should.ThrowAsync<ArgumentException>(() => invoker.InvokeAsync(command, null));
        else
        {
            (await invoker.InvokeAsync(command, null)).IsNoOp.ShouldBeTrue();
            observedRoot.ShouldBe(snapshot.RootDigest);
        }
        effects.ShouldBe(version is "v1" or "invalid" ? 0 : 1);
        resolver.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("V1", true)]
    [InlineData("v1", true)]
    [InlineData("invalid", false)]
    public async Task CatalogResolverPreservesHistoricalVersionNormalizationAndValidation(string version, bool accepted)
    {
        var fixture = new RouteCatalogTestFixture(); await fixture.ActivateAsync(fixture.Candidate());
        var resolver = new DomainServiceResolver(fixture.Authorities[0], Substitute.For<DaprClient>(), Options.Create(new DomainServiceOptions()), NullLogger<DomainServiceResolver>.Instance);
        if (accepted)
        {
            var registration = await resolver.ResolveAsync("tenant-a", "counter", version);
            registration!.Version.ShouldBe("v1"); registration.AppId.ShouldBe("sample");
        }
        else await Should.ThrowAsync<ArgumentException>(() => resolver.ResolveAsync("tenant-a", "counter", version));
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }
}
