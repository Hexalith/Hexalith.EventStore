using System.Security.Cryptography;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;

namespace Hexalith.EventStore.Server.Actors;

public partial class EventReplayProjectionActor
{
    /// <inheritdoc/>
    public async Task<bool> ReadReplayCompletionAsync(ProjectionReplayPublicationRequest request)
    {
        AggregateIdentity identity = ValidatePublication(request);
        await ClearPublicationCacheAsync().ConfigureAwait(false);
        if (!await OwnsPublicationAsync(identity, request).ConfigureAwait(false)) return false;
        ConditionalValue<ProjectionRebuildPromotionReceipt> receipt = await ReadPublicationStateAsync<ProjectionRebuildPromotionReceipt>(ProjectionRebuildPromotionReceiptKey).ConfigureAwait(false);
        if (!receipt.HasValue)
        {
            // Upgrade compatibility only: the server has already validated the
            // complete canonical source and an exact completed schema1 row. Do
            // not repair or mint a receipt, and never use this as guarded proof.
            ConditionalValue<ProjectionRebuildCandidate> candidate = await ReadPublicationStateAsync<ProjectionRebuildCandidate>(ProjectionRebuildCandidateKey).ConfigureAwait(false);
            if (candidate.HasValue) return false;
            ConditionalValue<ProjectionState> prior = await ReadPublicationStateAsync<ProjectionState>(ProjectionStateKey).ConfigureAwait(false);
            return prior.HasValue && MatchesPublicationState(prior.Value, request.Binding);
        }
        if (!receipt.Value.Completed || receipt.Value.SelectedReplayFence is not { } selected
            || selected.SourceActorId != identity.ActorId || selected.ProjectionName != request.Binding.ProjectionName
            || selected.HeadSequence != request.Binding.HeadSequence) return false;
        if (receipt.Value.PublicationBinding is { } binding
            && (binding.HeadMessageId != request.Binding.HeadMessageId || binding.SourceFingerprint != request.Binding.SourceFingerprint
                || binding.CandidateFingerprint != request.Binding.CandidateFingerprint)) return false;
        ConditionalValue<ProjectionState> live = await ReadPublicationStateAsync<ProjectionState>(ProjectionStateKey).ConfigureAwait(false);
        return live.HasValue && MatchesPublicationState(live.Value, request.Binding);
    }

    /// <inheritdoc/>
    public async Task<ProjectionRebuildInspection> StageReplayAsync(ProjectionReplayPublicationRequest request)
    {
        AggregateIdentity identity = ValidatePublication(request);
        if (request.State is not null) request = request with { State = request.State.WithBoundedNotification() };
        await ClearPublicationCacheAsync().ConfigureAwait(false);
        if (!await OwnsPublicationAsync(identity, request).ConfigureAwait(false)) return PublicationConflict(request);
        ProjectionReplayPublicationBinding binding = request.Binding;
        ConditionalValue<ProjectionRebuildPromotionReceipt> receipt = await ReadPublicationStateAsync<ProjectionRebuildPromotionReceipt>(ProjectionRebuildPromotionReceiptKey).ConfigureAwait(false);
        if (receipt.HasValue && receipt.Value.OperationId == binding.OperationId)
            return receipt.Value.PublicationBinding == binding
                ? await InspectProjectionRebuildAsync(new(binding.OperationId)).ConfigureAwait(false) : PublicationConflict(request);
        if (receipt.HasValue && !receipt.Value.Completed) return PublicationConflict(request);
        if (receipt.HasValue && receipt.Value.SelectedReplayFence is { } selectedFence
            && (selectedFence.SourceActorId != identity.ActorId || selectedFence.ProjectionName != binding.ProjectionName
                || binding.HeadSequence <= selectedFence.HeadSequence)) return PublicationConflict(request);
        ProjectionReplayPublicationBinding? floor = receipt.HasValue
            ? receipt.Value.PublicationFloor ?? receipt.Value.PublicationBinding : null;
        if (floor is not null && (floor.TenantId != binding.TenantId || floor.Domain != binding.Domain
            || floor.AggregateId != binding.AggregateId || binding.HeadSequence <= floor.HeadSequence)) return PublicationConflict(request);
        ConditionalValue<ProjectionRebuildCandidate> existing = await ReadPublicationStateAsync<ProjectionRebuildCandidate>(ProjectionRebuildCandidateKey).ConfigureAwait(false);
        if (existing.HasValue)
            return existing.Value.PublicationBinding == binding && MatchesPublicationState(existing.Value.State, binding)
                ? new(binding.OperationId, ProjectionRebuildInspectionStatus.Staged, existing.Value.State) : PublicationConflict(request);
        if (request.State is null || !MatchesPublicationState(request.State, binding)) return PublicationConflict(request);
        var candidate = new ProjectionRebuildCandidate(binding.OperationId, request.State)
        { PublicationBinding = binding, PublicationFloor = floor,
            SelectedReplayFence = new(identity.ActorId, binding.ProjectionName, binding.OperationId, binding.HeadSequence),
            PriorReplayFence = receipt.HasValue ? receipt.Value.SelectedReplayFence : null };
        if (receipt.HasValue) await RemovePublicationStateAsync(ProjectionRebuildPromotionReceiptKey).ConfigureAwait(false);
        await SetPublicationStateAsync(ProjectionRebuildCandidateKey, candidate).ConfigureAwait(false);
        try { await SavePublicationStateAsync().ConfigureAwait(false); }
        catch
        {
            ProjectionRebuildInspection persisted = await InspectProjectionRebuildAsync(new(binding.OperationId)).ConfigureAwait(false);
            if (persisted.Status != ProjectionRebuildInspectionStatus.Staged
                || persisted.State is null || !MatchesPublicationState(persisted.State, binding)) throw;
            return persisted;
        }
        return new(binding.OperationId, ProjectionRebuildInspectionStatus.Staged, request.State);
    }

    /// <inheritdoc/>
    public async Task<ProjectionRebuildInspection> PromoteReplayAsync(ProjectionReplayPublicationRequest request)
    {
        AggregateIdentity identity = ValidatePublication(request);
        await ClearPublicationCacheAsync().ConfigureAwait(false);
        if (!await OwnsPublicationAsync(identity, request).ConfigureAwait(false)) return PublicationConflict(request);
        ProjectionReplayPublicationBinding binding = request.Binding;
        ConditionalValue<ProjectionRebuildPromotionReceipt> existing = await ReadPublicationStateAsync<ProjectionRebuildPromotionReceipt>(ProjectionRebuildPromotionReceiptKey).ConfigureAwait(false);
        if (existing.HasValue)
            return existing.Value.PublicationBinding == binding
                ? await InspectProjectionRebuildAsync(new(binding.OperationId)).ConfigureAwait(false) : PublicationConflict(request);
        ConditionalValue<ProjectionRebuildCandidate> candidate = await ReadPublicationStateAsync<ProjectionRebuildCandidate>(ProjectionRebuildCandidateKey).ConfigureAwait(false);
        if (!candidate.HasValue || candidate.Value.PublicationBinding != binding
            || !MatchesPublicationState(candidate.Value.State, binding)) return PublicationConflict(request);
        ConditionalValue<ProjectionState> previous = await ReadPublicationStateAsync<ProjectionState>(ProjectionStateKey)
            .ConfigureAwait(false);
        ProjectionState selected = candidate.Value.State;
        await SetPublicationStateAsync(ProjectionStateKey, selected).ConfigureAwait(false);
        await SetPublicationStateAsync(ProjectionRebuildPromotionReceiptKey,
            new ProjectionRebuildPromotionReceipt(binding.OperationId, previous.HasValue ? previous.Value : null, selected)
            { PublicationBinding = binding, PublicationFloor = binding, SelectedReplayFence = candidate.Value.SelectedReplayFence,
                PriorReplayFence = candidate.Value.PriorReplayFence, SelectedStateFingerprint = binding.CandidateFingerprint }).ConfigureAwait(false);
        await RemovePublicationStateAsync(ProjectionRebuildCandidateKey).ConfigureAwait(false);
        try { await SavePublicationStateAsync().ConfigureAwait(false); }
        catch
        {
            ProjectionRebuildInspection persisted = await InspectProjectionRebuildAsync(new(binding.OperationId)).ConfigureAwait(false);
            if (persisted.Status != ProjectionRebuildInspectionStatus.Promoted
                || persisted.State is null || !MatchesPublicationState(persisted.State, binding)) throw;
            return persisted;
        }
        try
        {
            await NotifyProjectionHintsAsync(selected).ConfigureAwait(false);
        }
        catch
        {
            ProjectionDeliveryDiagnostics.RecordGuarded("unknown", "full_replay", "retryable", "notification_unavailable",
                binding.HeadSequence, 0);
        }
        return new(binding.OperationId, ProjectionRebuildInspectionStatus.Promoted, selected);
    }

    /// <inheritdoc/>
    public async Task<bool> FinalizeReplayAsync(ProjectionReplayPublicationRequest request)
    {
        AggregateIdentity identity = ValidatePublication(request);
        await ClearPublicationCacheAsync().ConfigureAwait(false);
        if (!await OwnsPublicationAsync(identity, request).ConfigureAwait(false)) return false;
        ConditionalValue<ProjectionRebuildPromotionReceipt> receipt = await ReadPublicationStateAsync<ProjectionRebuildPromotionReceipt>(ProjectionRebuildPromotionReceiptKey).ConfigureAwait(false);
        if (!receipt.HasValue || receipt.Value.PublicationBinding != request.Binding) return false;
        ProjectionRebuildInspection selected = await InspectProjectionRebuildAsync(new(request.Binding.OperationId)).ConfigureAwait(false);
        if (selected.Status != ProjectionRebuildInspectionStatus.Promoted) return false;
        if (receipt.Value.Completed) return true;
        await SetPublicationStateAsync(ProjectionRebuildPromotionReceiptKey,
            receipt.Value with { PreviousState = null, PromotedState = null, Completed = true }).ConfigureAwait(false);
        try { await SavePublicationStateAsync().ConfigureAwait(false); }
        catch
        {
            await ClearPublicationCacheAsync().ConfigureAwait(false);
            ConditionalValue<ProjectionRebuildPromotionReceipt> persisted = await ReadPublicationStateAsync<ProjectionRebuildPromotionReceipt>(ProjectionRebuildPromotionReceiptKey).ConfigureAwait(false);
            if (!persisted.HasValue || !persisted.Value.Completed || persisted.Value.PublicationBinding != request.Binding) throw;
        }
        return true;
    }

    private AggregateIdentity ValidatePublication(ProjectionReplayPublicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Binding);
        ArgumentNullException.ThrowIfNull(request.Lifecycle);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.LifecycleOperationId);
        ProjectionReplayPublicationBinding binding = request.Binding;
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.OperationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.HeadMessageId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(binding.HeadSequence);
        if (!IsPublicationDigest(binding.CandidateFingerprint) || !IsPublicationDigest(binding.SourceFingerprint))
            throw new ArgumentException("Publication references contain invalid digests.", nameof(request));
        var identity = new AggregateIdentity(binding.TenantId, binding.Domain, binding.AggregateId);
        if (Id.GetId() != QueryActorIdHelper.DeriveActorId(binding.ProjectionName, binding.TenantId, binding.AggregateId, []))
            throw new ArgumentException("Publication scope does not own this projection actor.", nameof(request));
        return identity;
    }

    private async Task<bool> OwnsPublicationAsync(AggregateIdentity identity, ProjectionReplayPublicationRequest request)
        => _publicationLifecycle is not null && request.Lifecycle.Phase == ProjectionLifecyclePhase.Delivering
            && await _publicationLifecycle.OwnsDeliveryWriteAsync(identity, request.Binding.ProjectionName,
                request.LifecycleOperationId, request.Lifecycle).ConfigureAwait(false);

    private static bool IsPublicationDigest(string? value) => value is { Length: 64 }
        && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool MatchesPublicationState(ProjectionState state, ProjectionReplayPublicationBinding binding)
        => state.StateBytes is not null && state.ProjectionType == binding.ProjectionName && state.TenantId == binding.TenantId
            && Convert.ToHexStringLower(SHA256.HashData(state.StateBytes)) == binding.CandidateFingerprint;

    private static ProjectionRebuildInspection PublicationConflict(ProjectionReplayPublicationRequest request)
        => new(request.Binding.OperationId, ProjectionRebuildInspectionStatus.Conflict, null);
}
