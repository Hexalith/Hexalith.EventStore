using Dapr.Client;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.Server.Projections;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public sealed class ProjectionDeliveryCutoverLiveSidecarTests(DaprTestContainerFixture fixture) {
    private const string StoreName = "statestore";
    private const string BaselineCommit = "2794ecba4c435de5e53603aa6080b8d32d669858";

    [Fact]
    public async Task CutoverMarker_ReadinessDowngradeAndScopeErase_RehearseAgainstRedis() {
        using DaprClient client = new DaprClientBuilder()
            .UseHttpEndpoint(fixture.DaprHttpEndpoint)
            .UseGrpcEndpoint(fixture.DaprGrpcEndpoint)
            .Build();
        var store = new DaprProjectionDeliveryStateStore(
            client,
            Options.Create(new ProjectionOptions { CheckpointStateStoreName = StoreName }));
        var cutover = new ProjectionDeliveryCutover(store, TimeProvider.System);
        var readiness = new ProjectionDeliveryWriterProtocolHealthCheck(store);

        ProjectionDeliveryCutoverStatus activated = await cutover.ActivateAsync(
            new ProjectionDeliveryCutoverRequest(
                BaselineCommit,
                "disposable-live-fixture-backup",
                WritersQuiesced: true,
                RetryWorkersQuiesced: true,
                DowngradeProhibitedAcknowledged: true));

        activated.ShouldBe(ProjectionDeliveryCutoverStatus.Activated);
        (await readiness.CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Healthy);
        ProjectionDeliveryWriterProtocol marker = (await client.GetStateAsync<ProjectionDeliveryWriterProtocol>(
            StoreName,
            ProjectionDeliveryStateKeys.WriterProtocol)).ShouldNotBeNull();
        marker.WriterProtocolVersion.ShouldBe(2);
        marker.CutoverCommit.ShouldBe(BaselineCommit);

        var identity = new AggregateIdentity("cutover-tenant", "cutover-domain", $"aggregate-{Guid.NewGuid():N}");
        const string projection = "cutover-detail";
        ProjectionDeliveryStateReadResult absent = await store.ReadAsync(identity, projection);
        ProjectionDeliveryState current = ProjectionDeliveryState.CreateEmpty(
            identity,
            projection,
            ProjectionDeliveryFingerprint.ComputeInitial(identity, projection),
            DateTimeOffset.UtcNow);
        (await store.TrySaveAsync(identity, projection, current, absent.Etag)).ShouldBeTrue();
        ProjectionDeliveryStateReadResult written = await store.ReadAsync(identity, projection);

        bool downgraded = await client.TrySaveStateAsync(
            StoreName,
            ProjectionDeliveryStateKeys.GetStateKey(identity, projection),
            new ProjectionCheckpoint(
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                0,
                DateTimeOffset.UtcNow),
            written.Etag,
            new StateOptions { Concurrency = ConcurrencyMode.FirstWrite });
        downgraded.ShouldBeTrue();
        (await store.ReadAsync(identity, projection)).Classification
            .ShouldBe(ProjectionDeliveryStateClassification.SchemaRegression);

        await client.DeleteStateAsync(StoreName, ProjectionDeliveryStateKeys.GetStateKey(identity, projection));
        (await client.GetStateAsync<ProjectionDeliveryWriterProtocol>(StoreName, ProjectionDeliveryStateKeys.WriterProtocol))
            .ShouldNotBeNull("per-scope erasure must never remove the store-global writer marker");
    }
}
