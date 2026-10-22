using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Server.Tests.Projections;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Queries;

public sealed class QueryRouterSourceGuardTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CurrentInspection_ReadCancellationPropagatesOnlyTheCallerToken(bool callerCanceled, bool removedRoute)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        string route = fixture.Catalog.OrdinaryProjectionTypes[0];
        var removed = new NamedProjectionRouteCatalogEntry(fixture.Catalog.AppId, fixture.Catalog.ServiceVersion,
            fixture.Identity.Domain, 2, fixture.Catalog.DispatchCapability, fixture.Catalog.CatalogFingerprint, []);
        ProjectionSourceGuardDelivery probe = fixture.CreateReadOnlyProbe(removedRoute ? removed : fixture.Catalog);
        using var cancellation = new CancellationTokenSource();
        if (callerCanceled) cancellation.Cancel();
        fixture.StateReadFailure = new OperationCanceledException("sensitive-local-timeout", cancellation.Token);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();

        if (callerCanceled)
        {
            await Should.ThrowAsync<OperationCanceledException>(() => probe.InspectCurrentAsync(fixture.Identity, route,
                cancellation.Token)).ConfigureAwait(true);
        }
        else
        {
            ProjectionSourceGuardDeliveryResult result = await probe.InspectCurrentAsync(fixture.Identity, route,
                cancellation.Token).ConfigureAwait(true);
            result.Owned.ShouldBeTrue();
            result.AlreadyCurrent.ShouldBeFalse();
            result.ReasonCode.ShouldBe("delivery_state_unavailable");
            JsonSerializer.Serialize(result).ShouldNotContain("sensitive-");
            IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
            invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>()).Returns(
                QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { count = 1 }), route,
                    new QueryResponseMetadata(ProjectionVersion: "version-1", IsStale: false)));
            IProjectionLifecycleGateway lifecycle = Substitute.For<IProjectionLifecycleGateway>();
            lifecycle.ReadSnapshotAsync(fixture.Identity, route, Arg.Any<CancellationToken>())
                .Returns(new ProjectionLifecycleSnapshot(ProjectionLifecyclePhase.Idle, 1));
            var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance, lifecycle, probe);
            QueryRouterResult query = await router.RouteQueryAsync(new SubmitQuery(fixture.Identity.TenantId, fixture.Identity.Domain,
                fixture.Identity.AggregateId, "query-counter", [], "correlation-1", "user-1", ProjectionType: route)).ConfigureAwait(true);
            query.Success.ShouldBeTrue();
            query.Metadata.ShouldNotBeNull().Lifecycle.ShouldBe(ProjectionLifecycleState.Unavailable);
        }
        cost[ProjectionDeliveryCost.SourceInspections].ShouldBe(removedRoute || callerCanceled ? 0 : 2);
        cost[ProjectionDeliveryCost.SourceRanges].ShouldBe(0);
        cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(0);
        cost[ProjectionDeliveryCost.ProofWriteAttempts].ShouldBe(0);
        cost[ProjectionDeliveryCost.MutationAttempts].ShouldBe(0);
        fixture.RowWrites.ShouldBe(0);
    }

    [Theory]
    [InlineData("descriptor_removed", true)]
    [InlineData("route_removed", true)]
    [InlineData("rebuild_only", true)]
    [InlineData("route_removed", false)]
    public async Task QueryScope_RemovedCatalogAdmissionCannotHideItsPersistedGuardedEvidence(string change, bool activated)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1,
            unguardedLastRoute: !activated).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        string route = fixture.Catalog.OrdinaryProjectionTypes[0];
        var changed = new NamedProjectionRouteCatalogEntry(fixture.Catalog.AppId, fixture.Catalog.ServiceVersion,
            fixture.Identity.Domain, 2, fixture.Catalog.DispatchCapability, fixture.Catalog.CatalogFingerprint,
            change == "route_removed" ? [] : [route], change == "rebuild_only" ? [route] : [], [], [], [], [],
            change == "descriptor_removed" ? [route] : []);
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>()).Returns(
            QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { count = 1 }), route,
                new QueryResponseMetadata(ProjectionVersion: "version-1", IsStale: false) { Lifecycle = ProjectionLifecycleState.Current }));
        IProjectionLifecycleGateway lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        lifecycle.ReadSnapshotAsync(fixture.Identity, route, Arg.Any<CancellationToken>())
            .Returns(new ProjectionLifecycleSnapshot(ProjectionLifecyclePhase.Idle, 1));
        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance, lifecycle, fixture.CreateReadOnlyProbe(changed));
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();

        QueryRouterResult result = await router.RouteQueryAsync(new SubmitQuery(fixture.Identity.TenantId, fixture.Identity.Domain,
            fixture.Identity.AggregateId, "query-counter", [], "correlation-1", "user-1", ProjectionType: route)).ConfigureAwait(true);

        result.Metadata.ShouldNotBeNull().Lifecycle.ShouldBe(activated ? ProjectionLifecycleState.Unknown : ProjectionLifecycleState.Current);
        cost[ProjectionDeliveryCost.ProofReads].ShouldBe(1);
        cost[ProjectionDeliveryCost.MarkerReads].ShouldBe(2);
        cost[ProjectionDeliveryCost.SourceRanges].ShouldBe(0);
        cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(0);
        cost[ProjectionDeliveryCost.ProofWriteAttempts].ShouldBe(0);
        cost[ProjectionDeliveryCost.MutationAttempts].ShouldBe(0);
    }

    [Theory]
    [InlineData(1, 1, "version-1", false, false, ProjectionLifecycleState.Current, false)]
    [InlineData(2, 1, "version-1", false, false, ProjectionLifecycleState.Stale, false)]
    [InlineData(1, 1, "unproven-version", false, false, ProjectionLifecycleState.Unknown, false)]
    [InlineData(1, 1, "version-1", true, false, ProjectionLifecycleState.Unknown, false)]
    [InlineData(1, 1, "version-1", false, true, ProjectionLifecycleState.LocalOnly, false)]
    [InlineData(0, 0, "version-0", false, false, ProjectionLifecycleState.Unknown, false)]
    [InlineData(1, 1, "version-1", false, false, ProjectionLifecycleState.Unknown, true)]
    public async Task ActivatedProjection_QueryFreshnessRequiresSamePersistedSourceSelectionIdleAndVersionWithoutEffects(
        int head, int completed, string version, bool shared, bool localOnly, ProjectionLifecycleState expected, bool unguardedSibling)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(head, completed,
            routeCount: unguardedSibling ? 2 : 1, sharedLastRoute: shared, unguardedLastRoute: unguardedSibling).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        string route = fixture.Catalog.OrdinaryProjectionTypes[0];
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        var metadata = new QueryResponseMetadata(ProjectionVersion: version, IsStale: false)
        {
            Lifecycle = localOnly ? ProjectionLifecycleState.LocalOnly : ProjectionLifecycleState.Current,
        };
        invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { count = completed }), route, metadata));
        IProjectionLifecycleGateway lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        lifecycle.ReadSnapshotAsync(fixture.Identity, route, Arg.Any<CancellationToken>())
            .Returns(new ProjectionLifecycleSnapshot(ProjectionLifecyclePhase.Idle, 1));
        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance, lifecycle, fixture.Probe);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        QueryRouterResult result = await router.RouteQueryAsync(new SubmitQuery(fixture.Identity.TenantId, fixture.Identity.Domain,
            fixture.Identity.AggregateId, "query-counter", [], "correlation-1", "user-1", ProjectionType: route)).ConfigureAwait(true);
        result.Success.ShouldBeTrue();
        result.Metadata.ShouldNotBeNull().Provenance.ShouldBe(head == 0 ? QueryResponseProvenance.Unknown : QueryResponseProvenance.ProjectionBacked);
        result.Metadata.Lifecycle.ShouldBe(expected);
        result.Metadata.IsStale.ShouldBe(expected == ProjectionLifecycleState.Current ? false
            : expected == ProjectionLifecycleState.Stale ? true : null);
        cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(0);
        cost[ProjectionDeliveryCost.BatchExecutions].ShouldBe(0);
        cost[ProjectionDeliveryCost.SourceRanges].ShouldBe(0);
        cost[ProjectionDeliveryCost.MutationAttempts].ShouldBe(0);
        cost[ProjectionDeliveryCost.ProofWriteAttempts].ShouldBe(0);
        fixture.RowWrites.ShouldBe(0);
        fixture.Work.ShouldBeNull();
        if (head == 0)
        {
            ProjectionDeliveryState empty = fixture.Rows[route];
            empty.GuardEvidence.ShouldBeNull();
            empty.CompletedReceipts.ShouldBeEmpty();
            empty.CompletedMessageIdDigests.ShouldBeEmpty();
            (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route).ConfigureAwait(true))
                .Value.ShouldBeNull();
        }
    }
}
