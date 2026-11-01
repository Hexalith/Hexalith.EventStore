using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class ProjectionActivationWorkerTests
{
    [Theory]
    [InlineData(false, "single-event-v1")]
    [InlineData(true, "single-event-v1")]
    [InlineData(false, "full-replay-v1")]
    [InlineData(true, "full-replay-v1")]
    public async Task GuardedNamedAndLegacyCommittedMethodsBothPersistBeforeCleanupAndForeignLegacyOutputRetainsWork(bool refuseLegacy, string semantics)
    {
        using GuardedTailProductionHarness named = await GuardedTailProductionHarness.CreateAsync(2, 1, handlerSemantics: semantics);
        await named.UseProductionOwnersAsync();
        using var legacy = new LegacyReplayProductionHarness(backend: named.Backend);
        await legacy.SeedSourceAsync(named.Events);
        legacy.Head.ShouldBe(named.Head);
        legacy.NamedCoordinator = Substitute.For<INamedProjectionDispatchCoordinator>();
        if (refuseLegacy) legacy.Http.ResponseProjectionTypeOverride = "undeclared-output";
        string route = named.Catalog.OrdinaryProjectionTypes.Single();
        CommittedRouteCatalogSnapshot snapshot = MixedSnapshot(route);
        var authority = Substitute.For<IRouteCatalogAuthority>();
        int acquisitions = 0, releases = 0;
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            acquisitions++;
            return new RouteCatalogDispatchLease(snapshot, () => { releases++; return Task.CompletedTask; });
        });
        // Hold the public legacy HTTP carrier after the actual named model/row has completed.
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        legacy.Http.BeforePreparation = async () => { entered.TrySetResult(); await resume.Task; };
        using var worker = new ProjectionActivationWorker(legacy.Outbox, legacy.CreateOrchestrator(authority, named.Probe),
            Options.Create(new ProjectionDispatchOptions()), named.Clock, named.Probe, authority);
        Task attempt = worker.RunOnceAsync(CancellationToken.None);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            acquisitions.ShouldBe(1);
            releases.ShouldBe(0);
            attempt.IsCompleted.ShouldBeFalse();
            var namedRow = new DaprProjectionDeliveryStateStore(named.Backend, Options.Create(new ProjectionOptions()));
            (await namedRow.ReadAsync(named.Identity, route)).State.ShouldNotBeNull().LastDeliveredSequence.ShouldBe(2);
            (await named.Models.GetAsync<GuardedTailModel>("models", named.Identity.ActorId + ":" + route)).Value.ShouldNotBeNull().Count.ShouldBe(2);
            (await legacy.Outbox.GetAsync(legacy.Identity)).ShouldNotBeNull().LegacyDelivery.ShouldNotBeNull();
            (await legacy.Model.ReadProjectionStateAsync()).ShouldBeNull();
            resume.SetResult();
            await attempt.WaitAsync(TimeSpan.FromSeconds(5));
            acquisitions.ShouldBe(1);
            legacy.Http.Preparations.ShouldHaveSingleItem().ShouldBe([1L, 2L]);
            if (refuseLegacy)
            {
                (await legacy.Outbox.GetAsync(legacy.Identity)).ShouldNotBeNull().LegacyDelivery.ShouldNotBeNull();
                (await legacy.Model.ReadProjectionStateAsync()).ShouldBeNull();
                (await legacy.Rows.ReadAsync(legacy.Identity, "counter")).State.ShouldBeNull();
            }
            else
            {
                releases.ShouldBe(1);
                (await legacy.Outbox.GetAsync(legacy.Identity)).ShouldBeNull();
                (await legacy.Model.ReadProjectionStateAsync()).ShouldNotBeNull().GetState().GetProperty("count").GetInt32().ShouldBe(2);
                ProjectionDeliveryState row = (await legacy.Rows.ReadAsync(legacy.Identity, "counter")).State.ShouldNotBeNull();
                row.LastDeliveredSequence.ShouldBe(2);
                row.ActiveReservation.ShouldBeNull();
                (await namedRow.ReadAsync(named.Identity, route)).State.ShouldNotBeNull().ActiveReservation.ShouldBeNull();
            }
        }
        finally { resume.TrySetResult(); await attempt.WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    private static CommittedRouteCatalogSnapshot MixedSnapshot(string namedOutput)
    {
        var fixture = new RouteCatalogTestFixture();
        RouteContractDeclaration legacy = fixture.Envelope.Declarations.Single(declaration => declaration.RouteId == "counter-projection");
        RouteContractDeclaration named = legacy with
        {
            RouteId = "counter-named-projection", ProjectionType = namedOutput,
            Execution = legacy.Execution! with { HandlerType = typeof(GuardedTailProjectionHandler).FullName!, Aliases = [namedOutput], ProjectionOutputs = [namedOutput] },
        };
        RouteCatalogEntry target = fixture.Envelope.Routes.Single(entry => entry.RouteId == legacy.RouteId);
        RouteCatalogEnvelope envelope = RouteCatalogCodec.Seal(fixture.Envelope with
        {
            Declarations = [.. fixture.Envelope.Declarations, named],
            Routes = [.. fixture.Envelope.Routes, target with { RouteId = named.RouteId,
                DeclarationDigest = RouteCatalogCodec.ComputeDeclarationDigest(named), Method = "project/v2" }],
        });
        return new(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1);
    }
}
