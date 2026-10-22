using Dapr.Actors;
using Dapr.Actors.Client;
using System.Diagnostics.Metrics;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class NamedProjectionDispatchCoordinatorTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task GuardOffCatalogPreservesOlderActorCompatibilityWhileSchema2StillFailsClosed(bool guardedRow, bool advertised)
    {
        INamedProjectionRouteCatalog catalogs = Substitute.For<INamedProjectionRouteCatalog>();
        var entry = new NamedProjectionRouteCatalogEntry("widget-service", "v1", Identity.Domain, ProjectionDispatchProtocol.Version,
            ProjectionDispatchProtocol.Capability, "historic-catalog", ["widget-detail"])
        { SourceGuardCapabilities = advertised ? new Dictionary<string, ProjectionSourceGuardCapability>
            { ["widget-detail"] = new(1, 1, 1, "single-event-v1", "projection-owner-proof/v1", "event-message-id/v1", "read-model-batch/v1") } : [] };
        catalogs.Current.Returns(new NamedProjectionRouteCatalogSnapshot([entry]));
        IProjectionDeliveryStateStore states = Substitute.For<IProjectionDeliveryStateStore>();
        ProjectionDeliveryState row = ProjectionDeliveryState.CreateEmpty(Identity, "widget-detail",
            ProjectionDeliveryFingerprint.ComputeInitial(Identity, "widget-detail"), DateTimeOffset.UtcNow);
        if (guardedRow) row = row with { SchemaVersion = 2, GuardEvidenceVersion = 1 };
        states.ReadAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(
            new ProjectionDeliveryStateReadResult(row, "original", ProjectionDeliveryStateClassification.Current, true));
        IAggregateActor oldSource = Substitute.For<IAggregateActor>();
        oldSource.InspectSourceProofAsync().Returns(Task.FromException<SourceProofInspection>(
            new NotSupportedException("Older actor has no additive source method.")));
        IActorProxyFactory actors = Substitute.For<IActorProxyFactory>();
        actors.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(oldSource);
        IProjectionLifecycleGateway lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        IProjectionSelectedOwnerProofGateway owners = Substitute.For<IProjectionSelectedOwnerProofGateway>();
        var guard = new ProjectionSourceGuardDelivery(catalogs, states, lifecycle, owners, actors,
            Options.Create(new EventStoreActorOptions()));

        ProjectionSourceGuardDeliveryResult result = await guard.TryDeliverAsync(Identity, CancellationToken.None).ConfigureAwait(true);

        result.Owned.ShouldBe(guardedRow);
        result.AlreadyCurrent.ShouldBeFalse();
        result.ReasonCode.ShouldBe(guardedRow ? "guard_activation_required" : null);
        _ = oldSource.Received(advertised ? 1 : 0).InspectSourceProofAsync();
        _ = lifecycle.DidNotReceiveWithAnyArgs().ReadSnapshotAsync(default!, default!, default);
        _ = owners.DidNotReceiveWithAnyArgs().ReadAsync(default!, default!, default!, default!, default);
        _ = states.Received(1).ReadAsync(Identity, "widget-detail", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QualifiedCurrentPreservesSourceRowBothMarkersLifecycleOwnerSourceReadOrder()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        var trace = new List<string>();
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        IProjectionDeliveryStateStore states = Substitute.For<IProjectionDeliveryStateStore>();
        states.ReadAsync(fixture.Identity, route, Arg.Any<CancellationToken>()).Returns(async call =>
        {
            trace.Add("row");
            return await store.ReadAsync(fixture.Identity, route, call.ArgAt<CancellationToken>(2)).ConfigureAwait(false);
        });
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        { if (instrument.Name == ProjectionDeliveryCostDiagnostics.CounterName) current.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            if (tags.ToArray().Any(static tag => tag.Key == ProjectionDeliveryCostDiagnostics.CategoryTag
                && tag.Value as string == nameof(ProjectionDeliveryCost.MarkerReads))) trace.Add("marker");
        });
        listener.Start();
        IAggregateActor source = Substitute.For<IAggregateActor>();
        source.InspectSourceProofAsync().Returns(_ =>
        {
            trace.Add("source");
            return new SourceProofInspection(SourceProofInspectionStatus.Available, true, 1, false, fixture.Head);
        });
        IActorProxyFactory actors = Substitute.For<IActorProxyFactory>();
        actors.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(source);
        IProjectionLifecycleGateway lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        lifecycle.ReadSnapshotAsync(fixture.Identity, route, Arg.Any<CancellationToken>()).Returns(_ =>
        { trace.Add("lifecycle"); return new ProjectionLifecycleSnapshot(ProjectionLifecyclePhase.Idle, 1); });
        IProjectionSelectedOwnerProofGateway owners = Substitute.For<IProjectionSelectedOwnerProofGateway>();
        owners.ReadAsync(fixture.Identity, fixture.Catalog, route, Arg.Any<ProjectionLifecycleSnapshot>(), Arg.Any<CancellationToken>()).Returns(_ =>
        { trace.Add("owner"); return new ProjectionOwnerProofResult(fixture.Rows[route].GuardEvidence!.OwnerProof, null, 0, 0); });
        INamedProjectionRouteCatalog catalogs = Substitute.For<INamedProjectionRouteCatalog>();
        catalogs.Current.Returns(new NamedProjectionRouteCatalogSnapshot([fixture.Catalog]));
        var guard = new ProjectionSourceGuardDelivery(catalogs, states, lifecycle, owners, actors, Options.Create(new EventStoreActorOptions()));

        (await guard.TryDeliverAsync(fixture.Identity, CancellationToken.None).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();

        trace.ShouldBe(["source", "row", "marker", "marker", "lifecycle", "owner", "source"]);
    }
}
