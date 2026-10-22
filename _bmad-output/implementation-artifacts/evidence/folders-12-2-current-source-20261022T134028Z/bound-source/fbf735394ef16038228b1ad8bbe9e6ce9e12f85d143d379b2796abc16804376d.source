using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Snapshots;

namespace Hexalith.EventStore.Client.Handlers;

/// <summary>Additive processing seam for a request-scoped qualified folded snapshot capture.</summary>
public interface IAsyncFoldedSnapshotProcessor
{
    /// <summary>Checks the exact state, profile and evidence declaration before optional work.</summary>
    bool IsQualified(Type stateType, string profileId, string parityEvidenceId,
        FoldedSnapshotCaptureLimits limits);

    /// <summary>Processes once and returns an optional callback for post-admission state capture.</summary>
    Task<FoldedSnapshotProcessingResult> ProcessForSnapshotAsync(
        DomainServiceRequest request,
        string qualifiedProfileId,
        FoldedSnapshotCaptureLimits limits,
        CancellationToken cancellationToken);
}
