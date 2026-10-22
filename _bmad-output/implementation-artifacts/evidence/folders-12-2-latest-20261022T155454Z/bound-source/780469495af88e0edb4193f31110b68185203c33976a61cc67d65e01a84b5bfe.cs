using Hexalith.EventStore.Contracts.Snapshots;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>Explicit finite representation reservations for actor-side snapshot fixtures.</summary>
internal static class SnapshotTestLimits
{
    /// <summary>Creates finite fixture bounds with independently selectable actor byte, graph and node ceilings.</summary>
    internal static FoldedSnapshotCaptureLimits ForState(
        int stateBytes = 1024 * 1024, int nodeCount = 100_000, long actorGraphBytes = 8L * 1024 * 1024,
        long serializerScratchBytes = 32L * 1024 * 1024, int envelopeBytes = 1024 * 1024 + 4096)
        => new(
            maximumCaptureWorkspaceBytes: 64L * 1024 * 1024,
            maximumPreCommandStateGraphBytes: 1024 * 1024,
            maximumWorkingStateGraphBytes: 1024 * 1024,
            maximumDetachedEventGraphBytes: 2 * 1024 * 1024,
            maximumEventCount: 1000,
            maximumEventBytes: 1024 * 1024,
            maximumStateJsonBytes: stateBytes,
            maximumDomainParsedStateGraphBytes: 8L * 1024 * 1024,
            maximumEscapedWireStateBytes: checked(6 * stateBytes + 2),
            maximumActorParsedStateBytes: stateBytes,
            maximumActorParsedGraphBytes: actorGraphBytes,
            maximumCanonicalStateMeasurementBytes: stateBytes,
            maximumCanonicalEnvelopeBytes: envelopeBytes,
            maximumNodeCount: nodeCount,
            maximumDepth: 64,
            maximumSerializerScratchBytes: serializerScratchBytes,
            maximumSimultaneousLiveBytes: 64L * 1024 * 1024,
            maximumActorWorkspaceBytes: 64L * 1024 * 1024);
}
