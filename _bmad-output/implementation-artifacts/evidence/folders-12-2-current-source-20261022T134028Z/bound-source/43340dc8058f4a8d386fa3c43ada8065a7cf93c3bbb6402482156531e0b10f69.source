using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>
/// Cross-project proof that the supported async domain-projection seam can persist an exact
/// projection watermark with its read model and converge through duplicate delivery and full rebuild.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Tier", "2")]
public sealed class ProjectionWatermarkRebuildIntegrationTests {
    private const string AggregateId = "widget-1";
    private const string ProjectionType = "widget-watermark";
    private const string StoreName = "statestore";

    private static readonly DomainProjectionIdentityOptions s_identity = new() {
        AppId = "widget-service",
        ServiceVersion = "v1",
    };

    [Fact]
    public async Task GappedPositions_DuplicateDeliveryAndFullRebuild_ConvergeStateAndWatermark() {
        var store = new InMemoryReadModelStore();
        ProjectionDispatchRequest delivery = Request("delivery-1", Event(1, 101), Event(2, 104), Event(3, 109));
        ProjectionDispatchResponse first;
        ProjectionDispatchResponse duplicate;
        WatermarkedWidgetState afterFirst;
        WatermarkedWidgetState afterDuplicate;
        await using (ServiceProvider firstProvider = BuildProvider(store)) {
            first = await DomainProjectionDispatcher.DispatchAsync(
                firstProvider,
                delivery,
                new ProjectionDispatchOptions(),
                s_identity,
                CancellationToken.None);
            afterFirst = await ReadStateAsync(store);

            duplicate = await DomainProjectionDispatcher.DispatchAsync(
                firstProvider,
                delivery,
                new ProjectionDispatchOptions(),
                s_identity,
                CancellationToken.None);
            afterDuplicate = await ReadStateAsync(store);
        }

        // Recreate the domain handler and DI scope around the same durable store before invoking the
        // supported full-replay rebuild seam. This proves component-state independence without
        // pretending that an in-process reconstruction is an operating-system process restart.
        await using ServiceProvider rebuiltProvider = BuildProvider(store);
        ProjectionDispatchResponse rebuilt = await DomainProjectionDispatcher.RebuildAsync(
            rebuiltProvider,
            delivery with { DispatchId = "rebuild-1" },
            new ProjectionDispatchOptions(),
            s_identity,
            CancellationToken.None);
        WatermarkedWidgetState afterRebuild = await ReadStateAsync(store);

        first.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        duplicate.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        rebuilt.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        afterFirst.ShouldBe(new WatermarkedWidgetState(AppliedEventCount: 3, Watermark: 109));
        afterDuplicate.ShouldBe(afterFirst);
        afterRebuild.ShouldBe(afterFirst);
    }

    [Fact]
    public async Task MixedUnknownAndPositivePositions_PersistsHighestPositiveWhileAllUnknownRemainsZero() {
        var store = new InMemoryReadModelStore();
        await using ServiceProvider provider = BuildProvider(store);

        ProjectionDispatchResponse mixed = await DomainProjectionDispatcher.DispatchAsync(
            provider,
            Request("mixed-1", Event(1, 0), Event(2, 104), Event(3, 0)),
            new ProjectionDispatchOptions(),
            s_identity,
            CancellationToken.None);
        WatermarkedWidgetState mixedState = await ReadStateAsync(store);

        ProjectionDispatchResponse unknown = await DomainProjectionDispatcher.DispatchAsync(
            provider,
            Request("unknown-1", Event(1, 0), Event(2, 0), aggregateId: "widget-unknown"),
            new ProjectionDispatchOptions(),
            s_identity,
            CancellationToken.None);
        WatermarkedWidgetState unknownState = await ReadStateAsync(store, "widget-unknown");

        mixed.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        mixedState.ShouldBe(new WatermarkedWidgetState(AppliedEventCount: 3, Watermark: 104));
        unknown.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        unknownState.ShouldBe(new WatermarkedWidgetState(AppliedEventCount: 2, Watermark: 0));
    }

    [Fact]
    public async Task CursorIssuedFromPersistedReadModelWatermark_RejectsReplayAfterProjectionAdvances() {
        var store = new InMemoryReadModelStore();
        await using ServiceProvider provider = BuildProvider(store);
        IQueryCursorCodec codec = new QueryCursorCodec(
            new EphemeralDataProtectionProvider(),
            "Hexalith.EventStore.Tests.ProjectionWatermark.v1");

        ProjectionDispatchResponse first = await DomainProjectionDispatcher.DispatchAsync(
            provider,
            Request("cursor-1", Event(1, 39), Event(2, 41)),
            new ProjectionDispatchOptions(),
            s_identity,
            CancellationToken.None).ConfigureAwait(true);
        first.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);

        WatermarkedWidgetState persistedAtIssue = await ReadStateAsync(store).ConfigureAwait(true);
        persistedAtIssue.Watermark.ShouldBe(41);
        string issuedScope = QueryCursorScope.Create()
            .Add("tenant", "tenant-a")
            .Add("filter", "active")
            .AddProjectionWatermark(persistedAtIssue.Watermark)
            .Build();
        string cursor = codec.Encode("list-widgets", issuedScope, "widget-1");

        ProjectionDispatchResponse advanced = await DomainProjectionDispatcher.DispatchAsync(
            provider,
            Request("cursor-2", Event(1, 39), Event(2, 41), Event(3, 47)),
            new ProjectionDispatchOptions(),
            s_identity,
            CancellationToken.None).ConfigureAwait(true);
        advanced.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);

        WatermarkedWidgetState persistedAfterAdvance = await ReadStateAsync(store).ConfigureAwait(true);
        persistedAfterAdvance.Watermark.ShouldBe(47);
        string currentScope = QueryCursorScope.Create()
            .Add("tenant", "tenant-a")
            .Add("filter", "active")
            .AddProjectionWatermark(persistedAfterAdvance.Watermark)
            .Build();

        codec.TryDecode(cursor, "list-widgets", issuedScope, out string? originalPosition, out string? originalFailure)
            .ShouldBeTrue();
        originalPosition.ShouldBe("widget-1");
        originalFailure.ShouldBeNull();
        codec.TryDecode(cursor, "list-widgets", currentScope, out string? stalePosition, out string? staleFailure)
            .ShouldBeFalse();
        stalePosition.ShouldBeNull();
        staleFailure.ShouldBe("wrong-scope");

        string otherTenantScope = QueryCursorScope.Create()
            .Add("tenant", "tenant-b")
            .Add("filter", "active")
            .AddProjectionWatermark(persistedAtIssue.Watermark)
            .Build();
        codec.TryDecode(cursor, "list-widgets", otherTenantScope, out string? otherTenantPosition, out string? otherTenantFailure)
            .ShouldBeFalse();
        otherTenantPosition.ShouldBeNull();
        otherTenantFailure.ShouldBe("wrong-scope");
    }

    private static ServiceProvider BuildProvider(InMemoryReadModelStore store) {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IReadModelBatchStore>(store);
        _ = services.AddScoped<IAsyncDomainProjectionHandler>(provider =>
            new WatermarkedWidgetProjection(provider.GetRequiredService<IReadModelBatchStore>()));
        return services.BuildServiceProvider();
    }

    private static ProjectionDispatchRequest Request(
        string dispatchId,
        ProjectionEventDto first,
        ProjectionEventDto second,
        ProjectionEventDto? third = null,
        string aggregateId = AggregateId) {
        ProjectionEventDto[] events = third is null ? [first, second] : [first, second, third];
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
            s_identity.AppId,
            s_identity.ServiceVersion,
            [new ProjectionDispatchRoute("widget", ProjectionType)]);
        return new ProjectionDispatchRequest(
            new ProjectionRequest("tenant-a", "widget", aggregateId, events),
            [ProjectionType],
            dispatchId,
            fingerprint);
    }

    private static ProjectionEventDto Event(long sequence, long globalPosition) => new(
        "widget-updated",
        [],
        "json",
        sequence,
        DateTimeOffset.UnixEpoch.AddSeconds(sequence),
        "correlation-1",
        $"message-{sequence}",
        "user-1",
        globalPosition);

    private static async Task<WatermarkedWidgetState> ReadStateAsync(
        InMemoryReadModelStore store,
        string aggregateId = AggregateId) {
        ReadModelEntry<WatermarkedWidgetState> entry = await store.GetAsync<WatermarkedWidgetState>(
            StoreName,
            StateKey(aggregateId),
            CancellationToken.None);
        return entry.Value.ShouldNotBeNull();
    }

    private static string StateKey(string aggregateId) => $"{aggregateId}:{ProjectionType}";

    private sealed record WatermarkedWidgetState(int AppliedEventCount, long Watermark);

    private sealed class WatermarkedWidgetProjection(IReadModelBatchStore store)
        : IAsyncDomainProjectionRebuildHandler {
        public string Domain => "widget";

        public string ProjectionType => ProjectionWatermarkRebuildIntegrationTests.ProjectionType;

        public DomainProjectionRebuildSemantics RebuildSemantics => DomainProjectionRebuildSemantics.FullReplay;

        public async Task<DomainProjectionHandlerResult> ProjectAsync(
            ProjectionRequest request,
            string dispatchId,
            CancellationToken cancellationToken) {
            var batch = new ReadModelBatch(
                new ReadModelBatchScope(
                    StoreName,
                    request.TenantId,
                    request.Domain,
                    request.AggregateId,
                    ProjectionType,
                    dispatchId),
                [BuildWrite(request)]);
            ReadModelBatchResult result = await store.ExecuteAsync(batch, cancellationToken);
            return ReadModelBatchProjectionResultMapper.Map(result);
        }

        public Task<DomainProjectionRebuildPlan> PrepareRebuildAsync(
            ProjectionRequest request,
            string operationId,
            CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new DomainProjectionRebuildPlan(StoreName, [BuildWrite(request)]));
        }

        private static ReadModelBatchOperation BuildWrite(ProjectionRequest request) {
            long watermark = request.Events
                .Where(static value => value.GlobalPosition > 0)
                .Select(static value => value.GlobalPosition)
                .DefaultIfEmpty(0)
                .Max();
            var state = new WatermarkedWidgetState(request.Events.Length, watermark);
            return ReadModelBatchOperation.Write(
                StateKey(request.AggregateId),
                state,
                ReadModelBatchConcurrency.LastWrite);
        }
    }
}
