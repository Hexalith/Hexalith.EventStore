using Hexalith.EventStore.Contracts.Snapshots;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Exact shared deployment limits for the qualified capture fixture.</summary>
internal static class SnapshotCaptureTestLimits
{
    internal static FoldedSnapshotCaptureLimits Value { get; } = new(
        maximumCaptureWorkspaceBytes: 40_000,
        maximumPreCommandStateGraphBytes: 1024,
        maximumWorkingStateGraphBytes: 1024,
        maximumDetachedEventGraphBytes: 4096,
        maximumEventCount: 2,
        maximumEventBytes: 1024,
        maximumStateJsonBytes: 1024,
        maximumDomainParsedStateGraphBytes: 8192,
        maximumEscapedWireStateBytes: 6146,
        maximumActorParsedStateBytes: 1024,
        maximumActorParsedGraphBytes: 8192,
        maximumCanonicalStateMeasurementBytes: 1024,
        maximumCanonicalEnvelopeBytes: 8192,
        maximumNodeCount: 100,
        maximumDepth: 16,
        maximumSerializerScratchBytes: 2048,
        maximumSimultaneousLiveBytes: 40_000,
        maximumActorWorkspaceBytes: 40_000);
}
