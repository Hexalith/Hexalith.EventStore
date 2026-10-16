using Dapr.Actors.Runtime;
using System.Security.Cryptography;

namespace Hexalith.EventStore.Server.Actors;

public partial class EventReplayProjectionActor
{
    /// <inheritdoc/>
    public async Task<ProjectionRebuildInspection> InspectProjectionRebuildAsync(ProjectionRebuildCandidateOperation request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationId);
        // A prior save may have failed before commit or lost its committed response.
        // Pending actor-cache entries are never publication evidence. This barrier is
        // read-only at the provider and also discards an uncommitted earlier attempt.
        await ClearPublicationCacheAsync().ConfigureAwait(false);
        ConditionalValue<ProjectionRebuildPromotionReceipt> receipt = await ReadPublicationStateAsync<ProjectionRebuildPromotionReceipt>(ProjectionRebuildPromotionReceiptKey).ConfigureAwait(false);
        if (receipt.HasValue)
        {
            if (receipt.Value.OperationId != request.OperationId)
                return new(request.OperationId, ProjectionRebuildInspectionStatus.Conflict, null);
            ConditionalValue<ProjectionState> live = await ReadPublicationStateAsync<ProjectionState>(ProjectionStateKey)
                .ConfigureAwait(false);
            ProjectionState? selected = receipt.Value.Completed ? live.HasValue ? live.Value : null : receipt.Value.PromotedState;
            if (selected is null || selected.StateBytes is null || !live.HasValue || live.Value is null
                || live.Value.StateBytes is null || live.Value.ProjectionType != selected.ProjectionType
                || live.Value.TenantId != selected.TenantId || !live.Value.StateBytes.AsSpan().SequenceEqual(selected.StateBytes))
                return new(request.OperationId, ProjectionRebuildInspectionStatus.Conflict, null);
            if (receipt.Value.PublicationBinding is { } binding && !MatchesPublicationState(selected, binding))
                return new(request.OperationId, ProjectionRebuildInspectionStatus.Conflict, null);
            if (receipt.Value.SelectedStateFingerprint is { } digest
                && Convert.ToHexStringLower(SHA256.HashData(selected.StateBytes)) != digest)
                return new(request.OperationId, ProjectionRebuildInspectionStatus.Conflict, null);
            return new(request.OperationId, ProjectionRebuildInspectionStatus.Promoted, selected);
        }
        ConditionalValue<ProjectionRebuildCandidate> candidate = await ReadPublicationStateAsync<ProjectionRebuildCandidate>(ProjectionRebuildCandidateKey).ConfigureAwait(false);
        return !candidate.HasValue ? new(request.OperationId, ProjectionRebuildInspectionStatus.Missing, null)
            : candidate.Value is null || candidate.Value.State is null || candidate.Value.State.StateBytes is null
                || candidate.Value.OperationId != request.OperationId ? new(request.OperationId, ProjectionRebuildInspectionStatus.Conflict, null)
            : new(request.OperationId, ProjectionRebuildInspectionStatus.Staged, candidate.Value.State);
    }
}
