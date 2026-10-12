using Hexalith.EventStore.Contracts.Snapshots;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Exact shared deployment limits for the qualified capture fixture.</summary>
internal static class SnapshotCaptureTestLimits
{
    internal static FoldedSnapshotCaptureLimits Value { get; } = For();

    internal static FoldedSnapshotCaptureLimits For(
        int maximumEventCount = 2,
        int maximumEventBytes = 1024,
        int maximumStateJsonBytes = 1024,
        long maximumDomainParsedStateGraphBytes = 8192,
        int maximumNodeCount = 100,
        int maximumDepth = 16)
        => new(
        maximumCaptureWorkspaceBytes: 40_000,
        maximumPreCommandStateGraphBytes: 1024,
        maximumWorkingStateGraphBytes: 1024,
        maximumDetachedEventGraphBytes: 4096,
        maximumEventCount: maximumEventCount,
        maximumEventBytes: maximumEventBytes,
        maximumStateJsonBytes: maximumStateJsonBytes,
        maximumDomainParsedStateGraphBytes: maximumDomainParsedStateGraphBytes,
        maximumEscapedWireStateBytes: checked(6 * maximumStateJsonBytes + 2),
        maximumActorParsedStateBytes: 1024,
        maximumActorParsedGraphBytes: 8192,
        maximumCanonicalStateMeasurementBytes: 1024,
        maximumCanonicalEnvelopeBytes: 8192,
        maximumNodeCount: maximumNodeCount,
        maximumDepth: maximumDepth,
        maximumSerializerScratchBytes: 2048,
        maximumSimultaneousLiveBytes: 40_000,
        maximumActorWorkspaceBytes: 40_000);
}
