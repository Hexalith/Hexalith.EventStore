using Hexalith.EventStore.Contracts.Snapshots;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>Explicit finite representation reservations for actor-side snapshot fixtures.</summary>
internal static class SnapshotTestLimits
{
    internal static FoldedSnapshotCaptureLimits ForState(int stateBytes = 1024 * 1024, int nodeCount = 100_000)
        => new(
            maximumCaptureWorkspaceBytes: 32L * 1024 * 1024,
            maximumPreCommandStateGraphBytes: 1024 * 1024,
            maximumWorkingStateGraphBytes: 1024 * 1024,
            maximumDetachedEventGraphBytes: 2 * 1024 * 1024,
            maximumEventCount: 1000,
            maximumEventBytes: 1024 * 1024,
            maximumStateJsonBytes: stateBytes,
            maximumDomainParsedStateGraphBytes: 8L * 1024 * 1024,
            maximumEscapedWireStateBytes: checked(6 * stateBytes + 2),
            maximumActorParsedStateBytes: stateBytes,
            maximumActorParsedGraphBytes: 8L * 1024 * 1024,
            maximumCanonicalStateMeasurementBytes: stateBytes,
            maximumCanonicalEnvelopeBytes: 4 * 1024 * 1024,
            maximumNodeCount: nodeCount,
            maximumDepth: 64,
            maximumSerializerScratchBytes: 4L * 1024 * 1024,
            maximumSimultaneousLiveBytes: 32L * 1024 * 1024,
            maximumActorWorkspaceBytes: 32L * 1024 * 1024);
}
