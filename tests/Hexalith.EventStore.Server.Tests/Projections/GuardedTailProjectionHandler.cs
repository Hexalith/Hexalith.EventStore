using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>Single-event production SDK fixture with an explicit stable per-event two-key batch.</summary>
internal sealed class GuardedTailProjectionHandler(string projection, DaprReadModelStore store, bool initializeOwner = false) :
    IAsyncDomainProjectionSingleEventHandler, IAsyncDomainProjectionIncrementalReplayHandler
{
    public string Domain => "counter";
    public string ProjectionType => projection;
    public int Calls { get; private set; }
    public int RebuildCalls { get; private set; }
    public string? ReplayGenerationOverride { get; set; }
    public string? ReplayOwnerIdentityOverride { get; set; }
    public string? ReplayMessageIdOverride { get; set; }
    public bool UnconditionalReplay { get; set; }
    public bool RequireRebuildBeforeEffects { get; set; }
    public int RebuildPaddingBytes { get; set; }
    public List<string> Dispatches { get; } = [];
    public DomainProjectionRebuildSemantics RebuildSemantics => DomainProjectionRebuildSemantics.FullReplay;
    public string RebuildTransportStoreName => "models";

    public async Task<DomainProjectionRebuildPlan> PrepareRebuildAsync(ProjectionRequest request, string operationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RebuildCalls++;
        var identity = new AggregateIdentity(request.TenantId, request.Domain, request.AggregateId);
        ProjectionEventDto last = request.Events[^1];
        string key = identity.ActorId + ":" + projection;
        ReadModelEntry<GuardedTailModel> prior = await store.GetAsync<GuardedTailModel>("models", key, cancellationToken).ConfigureAwait(false);
        ReadModelEntry<GuardedTailModel> priorIndex = await store.GetAsync<GuardedTailModel>("models", key + "-index", cancellationToken).ConfigureAwait(false);
        var selected = new ProjectionOwnerSelection(1, identity.TenantId, identity.Domain, identity.AggregateId,
            projection, ReplayOwnerIdentityOverride ?? "owner-1", ReplayGenerationOverride ?? request.RebuildOwnerGenerations?.GetValueOrDefault(projection) ?? "generation-1",
            last.SequenceNumber, ReplayMessageIdOverride ?? last.MessageId!, operationId, "replay-version-" + last.SequenceNumber)
        { ReceiptProjectionName = "rebuild" };
        var model = new GuardedTailModel(request.Events.Length, selected.FreshnessVersion, selected)
            { Padding = RebuildPaddingBytes == 0 ? null : new string('p', RebuildPaddingBytes) };
        return new DomainProjectionRebuildPlan("models",
        [ReadModelBatchOperation.Write(key, model, UnconditionalReplay ? ReadModelBatchConcurrency.LastWrite
                : prior.Value is null ? ReadModelBatchConcurrency.CreateOnly : ReadModelBatchConcurrency.Match(prior.ETag!)),
            ReadModelBatchOperation.Write(key + "-index", model, UnconditionalReplay ? ReadModelBatchConcurrency.LastWrite
                : priorIndex.Value is null ? ReadModelBatchConcurrency.CreateOnly : ReadModelBatchConcurrency.Match(priorIndex.ETag!))]);
    }

    public async Task<DomainProjectionRebuildPlan> PrepareStreamedRebuildAsync(ProjectionRequest sourceHeader,
        IAsyncEnumerable<ProjectionEventDto> events, string operationId, CancellationToken cancellationToken)
    {
        var all = new List<ProjectionEventDto>();
        await foreach (ProjectionEventDto item in events.WithCancellation(cancellationToken).ConfigureAwait(false)) all.Add(item);
        return await PrepareRebuildAsync(sourceHeader with { Events = [.. all] }, operationId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DomainProjectionHandlerResult> ProjectAsync(ProjectionRequest request, string dispatchId, CancellationToken cancellationToken)
    {
        if (request.Events.Length != 1) throw new InvalidOperationException("Single-event fixture received a page.");
        Calls++;
        Dispatches.Add(dispatchId);
        if (RequireRebuildBeforeEffects)
            return DomainProjectionHandlerResult.Retryable(ProjectionDispatchReasonCodes.DeliveryRebuildRequired);
        ProjectionEventDto item = request.Events[0];
        _ = System.Text.Json.JsonSerializer.Deserialize<Hexalith.EventStore.Sample.Counter.Events.CounterIncremented>(item.Payload)
            ?? throw new InvalidOperationException("Fixture handler could not decode its admitted domain event.");
        var identity = new AggregateIdentity(request.TenantId, request.Domain, request.AggregateId);
        string key = identity.ActorId + ":" + projection;
        ReadModelEntry<GuardedTailModel> current = await store.GetAsync<GuardedTailModel>("models", key, cancellationToken).ConfigureAwait(false);
        GuardedTailModel? prior = current.Value;
        if (prior is null && (!initializeOwner || item.SequenceNumber != 1)
            || prior is not null && prior.ProjectionOwner.AppliedSequence + 1 != item.SequenceNumber)
            return DomainProjectionHandlerResult.Retryable(ProjectionDispatchReasonCodes.DeliveryRebuildRequired);
        ProjectionOwnerSelection initial = prior?.ProjectionOwner ?? new(1, identity.TenantId, identity.Domain, identity.AggregateId,
            projection, identity.AggregateId, item.MessageId!, 1, item.MessageId!, item.MessageId!, "version-1");
        var selection = initial with
        {
            AppliedSequence = item.SequenceNumber, MessageId = item.MessageId!, BatchId = item.MessageId!,
            FreshnessVersion = "version-" + item.SequenceNumber, ReceiptProjectionName = null,
        };
        var model = new GuardedTailModel((prior?.Count ?? 0) + 1, selection.FreshnessVersion, selection);
        var batch = new ReadModelBatch(new("models", identity.TenantId, identity.Domain, identity.AggregateId, projection, item.MessageId!),
        [
            ReadModelBatchOperation.Write(key, model, prior is null ? ReadModelBatchConcurrency.CreateOnly : ReadModelBatchConcurrency.Match(current.ETag!)),
            ReadModelBatchOperation.Write(key + "-index", model, prior is null ? ReadModelBatchConcurrency.CreateOnly : ReadModelBatchConcurrency.LastWrite),
        ]) { OwnerSelection = selection };
        return ReadModelBatchProjectionResultMapper.Map(await store.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false));
    }
}
