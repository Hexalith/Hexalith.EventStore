using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Runs one projection write in a separate operating-system process against the live Dapr store.</summary>
public sealed class ProjectionWatermarkProcessWorkerTests {
    [Fact]
    public async Task ApplyPersistedHistoryAsync() {
        string? phase = Environment.GetEnvironmentVariable("P2_WATERMARK_PHASE");
        if (phase is null) {
            Assert.Skip("Worker is launched by ProjectionWatermarkProcessRestartTests only.");
            return;
        }

        phase.ShouldBeOneOf("initial", "rebuild");
        string endpoint = RequiredEnvironment("P2_WATERMARK_DAPR_ENDPOINT");
        string historyKey = RequiredEnvironment("P2_WATERMARK_HISTORY_KEY");
        string stateKey = RequiredEnvironment("P2_WATERMARK_STATE_KEY");
        string tenantId = RequiredEnvironment("P2_WATERMARK_TENANT");
        string aggregateId = RequiredEnvironment("P2_WATERMARK_AGGREGATE");

        using DaprClient client = new DaprClientBuilder().UseGrpcEndpoint(endpoint).Build();
        EventEnvelope[] history = (await client.GetStateAsync<EventEnvelope[]>(
            "statestore", historyKey, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true))
            .ShouldNotBeNull();
        history.Length.ShouldBe(3);
        history.Select(static value => value.GlobalPosition).ShouldBe([101L, 104L, 109L]);

        var store = new DaprReadModelStore(client);
        if (phase == "rebuild") {
            ReadModelEntry<ProjectionWatermarkProcessState> before = await store
                .GetAsync<ProjectionWatermarkProcessState>("statestore", stateKey, TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            before.Value.ShouldBe(new ProjectionWatermarkProcessState(3, 109));
        }

        ProjectionEventReadabilityResult wire = await ProjectionEventWireBuilder.BuildAsync(
            new NoOpEventPayloadProtectionService(),
            new AggregateIdentity(tenantId, "widget", aggregateId),
            history,
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        ProjectionEventDto[] events = wire.Events.ShouldNotBeNull();
        long watermark = events
            .Where(static value => value.GlobalPosition > 0)
            .Select(static value => value.GlobalPosition)
            .Max();
        var state = new ProjectionWatermarkProcessState(events.Length, watermark);
        var batch = new ReadModelBatch(
            new ReadModelBatchScope(
                "statestore", tenantId, "widget", aggregateId, "widget-watermark", $"p2-{phase}-{aggregateId}"),
            [ReadModelBatchOperation.Write(stateKey, state, ReadModelBatchConcurrency.LastWrite)]);
        ReadModelBatchResult result = await store.ExecuteAsync(batch, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        result.Status.ShouldBe(ReadModelBatchStatus.Completed);

        ReadModelEntry<ProjectionWatermarkProcessState> persisted = await store
            .GetAsync<ProjectionWatermarkProcessState>("statestore", stateKey, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        persisted.Value.ShouldBe(state);
    }

    private static string RequiredEnvironment(string name)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Missing process proof input {name}.");
}
