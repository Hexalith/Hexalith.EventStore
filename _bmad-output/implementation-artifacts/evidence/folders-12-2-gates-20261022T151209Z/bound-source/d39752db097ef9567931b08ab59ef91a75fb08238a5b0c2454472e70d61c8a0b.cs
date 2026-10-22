using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.DependencyInjection;

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

        phase.ShouldBeOneOf("initial", "duplicate", "rebuild");
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
        if (phase != "initial") {
            ReadModelEntry<ProjectionWatermarkProcessState> before = await store
                .GetAsync<ProjectionWatermarkProcessState>("statestore", stateKey, TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            before.Value.ShouldBe(phase == "rebuild"
                ? new ProjectionWatermarkProcessState(AppliedEventCount: 1, Watermark: 101)
                : new ProjectionWatermarkProcessState(AppliedEventCount: 3, Watermark: 109));
        }

        ProjectionEventReadabilityResult wire = await ProjectionEventWireBuilder.BuildAsync(
            new NoOpEventPayloadProtectionService(),
            new AggregateIdentity(tenantId, "widget", aggregateId),
            history,
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        ProjectionEventDto[] events = wire.Events.ShouldNotBeNull();
        events.Select(static value => value.GlobalPosition).ShouldBe([101L, 104L, 109L]);
        var services = new ServiceCollection();
        _ = services.AddSingleton<IReadModelBatchStore>(store);
        _ = services.AddScoped<IAsyncDomainProjectionHandler>(provider =>
            new ProjectionWatermarkProcessHandler(provider.GetRequiredService<IReadModelBatchStore>(), stateKey));
        await using ServiceProvider provider = services.BuildServiceProvider();
        var identity = new DomainProjectionIdentityOptions { AppId = "p2-watermark-worker", ServiceVersion = "v1" };
        ProjectionDispatchRoute[] routes = [new("widget", "widget-watermark")];
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(identity.AppId, identity.ServiceVersion, routes);
        var catalog = new DomainProjectionCatalogRegistry();
        catalog.Register(fingerprint, routes);
        var request = new ProjectionDispatchRequest(
            new ProjectionRequest(tenantId, "widget", aggregateId, events),
            ["widget-watermark"],
            $"p2-{(phase == "rebuild" ? "rebuild" : "initial")}-{aggregateId}",
            fingerprint);
        ProjectionDispatchResponse result = phase == "rebuild"
            ? await DomainProjectionDispatcher.RebuildAsync(
                provider, request, new ProjectionDispatchOptions(), identity, TestContext.Current.CancellationToken)
                .ConfigureAwait(true)
            : await DomainProjectionDispatcher.DispatchAsync(
                provider, request, new ProjectionDispatchOptions(), catalog, TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
        result.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(phase == "duplicate"
            ? ProjectionDispatchStatus.AlreadyCompleted
            : ProjectionDispatchStatus.Completed);

        ReadModelEntry<ProjectionWatermarkProcessState> persisted = await store
            .GetAsync<ProjectionWatermarkProcessState>("statestore", stateKey, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        persisted.Value.ShouldBe(new ProjectionWatermarkProcessState(3, 109));
    }

    private static string RequiredEnvironment(string name)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Missing process proof input {name}.");
}
