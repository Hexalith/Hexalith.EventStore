using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Uses production SDK batches to select one contiguous event's detail/index and its original receipt.</summary>
public sealed class LiveGuardedCounterProjectionHandler(IReadModelStore store, IReadModelBatchStore batches,
    LiveGuardedCounterFaultControl faults) : IAsyncDomainProjectionSingleEventHandler, IAsyncDomainProjectionRebuildHandler
{
    /// <summary>The fixture's independently catalogued source domain.</summary>
    public const string SourceDomain = "guard-counter";

    /// <summary>The exact registered route and selected-row suffix.</summary>
    public const string Route = "guard-counter-detail";

    /// <summary>The owning fixture state component.</summary>
    public const string Store = "statestore";

    /// <summary>Gets the explicitly enrolled source and recovery descriptor.</summary>
    public static ProjectionSourceGuardCapability Capability { get; } = new(1, 1, 1, "single-event-v1",
        "projection-owner-proof/v1", "event-message-id/v1", "read-model-batch/v1")
    { InitialOwnerProtocol = "aggregate-first-event/v1", CanonicalReplayProtocol = "staged-full-replay/v1" };

    /// <inheritdoc/>
    public string Domain => SourceDomain;

    /// <inheritdoc/>
    public string ProjectionType => Route;

    /// <inheritdoc/>
    public DomainProjectionRebuildSemantics RebuildSemantics => DomainProjectionRebuildSemantics.FullReplay;

    /// <inheritdoc/>
    public async Task<DomainProjectionRebuildPlan> PrepareRebuildAsync(ProjectionRequest request, string operationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        faults.RecordPreparation();
        var identity = new AggregateIdentity(request.TenantId, request.Domain, request.AggregateId);
        string key = identity.ActorId + ":" + Route;
        ReadModelEntry<LiveGuardedCounterModel> prior = await store.GetAsync<LiveGuardedCounterModel>(Store, key, cancellationToken).ConfigureAwait(false);
        ReadModelEntry<LiveGuardedCounterModel> index = await store.GetAsync<LiveGuardedCounterModel>(Store, key + "-index", cancellationToken).ConfigureAwait(false);
        ProjectionEventDto head = request.Events[^1];
        var selection = new ProjectionOwnerSelection(1, identity.TenantId, identity.Domain, identity.AggregateId, Route,
            request.RebuildOwnerIdentities?.GetValueOrDefault(Route) ?? identity.AggregateId,
            request.RebuildOwnerGenerations?.GetValueOrDefault(Route) ?? request.Events[0].MessageId!,
            head.SequenceNumber, head.MessageId!, operationId, "replay-version-" + head.SequenceNumber)
        { ReceiptProjectionName = "rebuild" };
        var model = new LiveGuardedCounterModel(request.Events.Length, selection.FreshnessVersion, selection);
        return new(Store,
        [ReadModelBatchOperation.Write(key, model, prior.Value is null ? ReadModelBatchConcurrency.CreateOnly : ReadModelBatchConcurrency.Match(prior.ETag!)),
            ReadModelBatchOperation.Write(key + "-index", model, index.Value is null ? ReadModelBatchConcurrency.CreateOnly : ReadModelBatchConcurrency.Match(index.ETag!))]);
    }

    /// <inheritdoc/>
    public async Task<DomainProjectionHandlerResult> ProjectAsync(ProjectionRequest request, string dispatchId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Events.Length != 1) throw new InvalidOperationException("The fixture requires one contiguous source event.");
        faults.RecordApplication();
        ProjectionEventDto item = request.Events[0];
        _ = System.Text.Json.JsonSerializer.Deserialize<Hexalith.EventStore.Sample.Counter.Events.CounterIncremented>(item.Payload)
            ?? throw new InvalidOperationException("The source event is unreadable.");
        var identity = new AggregateIdentity(request.TenantId, request.Domain, request.AggregateId);
        string key = identity.ActorId + ":" + Route;
        ReadModelEntry<LiveGuardedCounterModel> current = await store.GetAsync<LiveGuardedCounterModel>(Store, key, cancellationToken).ConfigureAwait(false);
        LiveGuardedCounterModel? prior = current.Value;
        if (prior is null && item.SequenceNumber != 1 || prior is not null && prior.ProjectionOwner.AppliedSequence + 1 != item.SequenceNumber)
            return DomainProjectionHandlerResult.Retryable(ProjectionDispatchReasonCodes.DeliveryRebuildRequired);
        ProjectionOwnerSelection selection = prior?.ProjectionOwner ?? new(1, identity.TenantId, identity.Domain, identity.AggregateId,
            Route, identity.AggregateId, item.MessageId!, 1, item.MessageId!, item.MessageId!, "version-1");
        selection = selection with { AppliedSequence = item.SequenceNumber, MessageId = item.MessageId!, BatchId = item.MessageId!,
            FreshnessVersion = "version-" + item.SequenceNumber, ReceiptProjectionName = null };
        var model = new LiveGuardedCounterModel((prior?.Count ?? 0) + 1, selection.FreshnessVersion, selection);
        var batch = new ReadModelBatch(new(Store, identity.TenantId, identity.Domain, identity.AggregateId, Route, item.MessageId!),
        [ReadModelBatchOperation.Write(key, model, prior is null ? ReadModelBatchConcurrency.CreateOnly : ReadModelBatchConcurrency.Match(current.ETag!)),
            ReadModelBatchOperation.Write(key + "-index", model, prior is null ? ReadModelBatchConcurrency.CreateOnly : ReadModelBatchConcurrency.LastWrite)])
        { OwnerSelection = selection };
        ReadModelBatchResult result = await batches.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
        if (result.Status is ReadModelBatchStatus.Completed or ReadModelBatchStatus.AlreadyCompleted) faults.AfterCommit(item.SequenceNumber);
        return ReadModelBatchProjectionResultMapper.Map(result);
    }
}
