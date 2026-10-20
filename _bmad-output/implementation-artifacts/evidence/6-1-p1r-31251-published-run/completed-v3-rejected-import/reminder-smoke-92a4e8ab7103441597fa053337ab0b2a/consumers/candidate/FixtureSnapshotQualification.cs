using Hexalith.EventStore.Contracts.Snapshots;

/// <summary>Shared finite snapshot enrollment for the invocation-owned Counter fixture.</summary>
internal static class FixtureSnapshotQualification
{
    /// <summary>The exact profile shared by the actor and typed domain enrollment.</summary>
    internal const string ProfileId = "p1r-counter-folded-v1";

    /// <summary>The runtime-measured fixture parity identifier; it grants no owner acceptance.</summary>
    internal const string ParityEvidenceId = "p1r-counter-roundtrip-event-parity-v1";

    /// <summary>The simultaneous finite reservations shared by both serving hosts.</summary>
    internal static FoldedSnapshotCaptureLimits Limits { get; } = new(
        maximumCaptureWorkspaceBytes: 512_000,
        maximumPreCommandStateGraphBytes: 1024,
        maximumWorkingStateGraphBytes: 1024,
        maximumDetachedEventGraphBytes: 8192,
        maximumEventCount: 16,
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
        maximumSerializerScratchBytes: 262_144,
        maximumSimultaneousLiveBytes: 512_000,
        maximumActorWorkspaceBytes: 512_000);
}
