using Hexalith.EventStore.Contracts.Snapshots;

namespace Hexalith.EventStore.FoldedSnapshotFixture;

/// <summary>Finite SDK-fixture reservations; never a declaration for a consuming module.</summary>
public static class FoldedOrderLimits
{
    /// <summary>Creates independently selectable output, node and serializer refusal controls.</summary>
    public static FoldedSnapshotCaptureLimits Create(int stateBytes = 16_384, int nodes = 2_048, long scratchBytes = 2_097_152)
        => new(
            maximumCaptureWorkspaceBytes: 4_194_304,
            maximumPreCommandStateGraphBytes: 65_536,
            maximumWorkingStateGraphBytes: 65_536,
            maximumDetachedEventGraphBytes: 65_536,
            maximumEventCount: 8,
            maximumEventBytes: 16_384,
            maximumStateJsonBytes: stateBytes,
            maximumDomainParsedStateGraphBytes: 524_288,
            maximumEscapedWireStateBytes: checked(6 * stateBytes + 2),
            maximumActorParsedStateBytes: stateBytes,
            maximumActorParsedGraphBytes: 524_288,
            maximumCanonicalStateMeasurementBytes: stateBytes,
            maximumCanonicalEnvelopeBytes: stateBytes + 4096,
            maximumNodeCount: nodes,
            maximumDepth: 16,
            maximumSerializerScratchBytes: scratchBytes,
            maximumSimultaneousLiveBytes: 4_194_304,
            maximumActorWorkspaceBytes: 4_194_304);
}
