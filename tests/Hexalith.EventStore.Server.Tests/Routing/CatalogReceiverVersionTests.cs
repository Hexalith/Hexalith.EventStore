using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogReceiverVersionTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("  ", true)]
    [InlineData("v2", true)]
    [InlineData("V2", true)]
    [InlineData("v1", false)]
    [InlineData("invalid", false)]
    public async Task SdkReceiverUsesCommittedVersionAndRejectsExplicitConflictsBeforeProcessing(string? version, bool accepted)
    {
        var fixture = new RouteCatalogTestFixture();
        var declaration = fixture.Envelope.Declarations.Single(d => d.RouteId == "counter-increment-counter") with { ContractVersion = "v2" };
        var envelope = RouteCatalogCodec.Seal(fixture.Envelope with
        {
            Declarations = fixture.Envelope.Declarations.Select(d => d.RouteId == declaration.RouteId ? declaration : d).ToArray(),
            Routes = fixture.Envelope.Routes.Select(r => r.RouteId == declaration.RouteId
                ? r with { ContractVersion = "v2", DeclarationDigest = RouteCatalogCodec.ComputeDeclarationDigest(declaration) } : r).ToArray(),
        });
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1);
        int outstanding = 0, effects = 0;
        var authority = Substitute.For<IRouteCatalogAuthority>(); authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        { outstanding++; return new RouteCatalogDispatchLease(snapshot, () => { outstanding--; return Task.CompletedTask; }); });
        var guard = new DomainRouteCatalogGuard(authority, Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts.Single(h => h.AppId == "sample") }));
        var command = new CommandEnvelope("01ARZ3NDEKTSV4RRFFQ69G5FAV", "tenant-a", "counter", "aggregate", "increment-counter", "{}"u8.ToArray(), "correlation", null, "user",
            version is null ? null : new() { [DomainServiceVersion.ExtensionKey] = version });
        var http = new DefaultHttpContext(); http.Request.Path = "/process";
        http.Request.Headers[RouteCatalogInvocationHandler.RootHeader] = snapshot.RootDigest; http.Request.Headers[RouteCatalogInvocationHandler.EpochHeader] = "1";
        var invocation = new DefaultEndpointFilterInvocationContext(http, new DomainServiceRequest(command, null));
        object? response = await new DomainRouteCatalogFilter(guard).InvokeAsync(invocation, _ => { effects++; return ValueTask.FromResult<object?>(null); });
        effects.ShouldBe(accepted ? 1 : 0); outstanding.ShouldBe(0);
        if (!accepted) ((IStatusCodeHttpResult)response!).StatusCode.ShouldBe(503);
    }
}
