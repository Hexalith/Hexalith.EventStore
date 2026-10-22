namespace Hexalith.EventStore.Contracts.Snapshots;

/// <summary>
/// Deployment-owned upper bounds for every library-held folded-snapshot representation.
/// All maxima are simultaneous reservations, including serializer scratch; they do not
/// claim to confine allocations made internally by a trusted application callback.
/// </summary>
public sealed record FoldedSnapshotCaptureLimits
{
    /// <summary>Creates a reviewed, finite capture declaration.</summary>
    public FoldedSnapshotCaptureLimits(
        long maximumCaptureWorkspaceBytes,
        long maximumPreCommandStateGraphBytes,
        long maximumWorkingStateGraphBytes,
        long maximumDetachedEventGraphBytes,
        int maximumEventCount,
        int maximumEventBytes,
        int maximumStateJsonBytes,
        long maximumDomainParsedStateGraphBytes,
        int maximumEscapedWireStateBytes,
        int maximumActorParsedStateBytes,
        long maximumActorParsedGraphBytes,
        int maximumCanonicalStateMeasurementBytes,
        int maximumCanonicalEnvelopeBytes,
        int maximumNodeCount,
        int maximumDepth,
        long maximumSerializerScratchBytes,
        long maximumSimultaneousLiveBytes,
        long maximumActorWorkspaceBytes)
    {
        if (maximumCaptureWorkspaceBytes is < 1 or > 128L * 1024 * 1024
            || maximumActorWorkspaceBytes is < 1 or > 128L * 1024 * 1024
            || maximumSimultaneousLiveBytes is < 1 or > 128L * 1024 * 1024
            || maximumPreCommandStateGraphBytes is < 1 or > 64L * 1024 * 1024
            || maximumWorkingStateGraphBytes is < 1 or > 64L * 1024 * 1024
            || maximumDetachedEventGraphBytes is < 1 or > 64L * 1024 * 1024
            || maximumEventCount is < 1 or > 1000
            || maximumEventBytes is < 1 or > 64 * 1024 * 1024
            || maximumStateJsonBytes is < 1 or > 1024 * 1024
            || maximumDomainParsedStateGraphBytes is < 1 or > 32L * 1024 * 1024
            || maximumEscapedWireStateBytes is < 1 or > 6 * 1024 * 1024 + 2
            || maximumActorParsedStateBytes is < 1 or > 1024 * 1024
            || maximumActorParsedGraphBytes is < 1 or > 32L * 1024 * 1024
            || maximumCanonicalStateMeasurementBytes is < 1 or > 1024 * 1024
            || maximumCanonicalEnvelopeBytes is < 1 or > 4 * 1024 * 1024
            || maximumNodeCount is < 1 or > 1_000_000
            || maximumDepth is < 1 or > 64
            || maximumSerializerScratchBytes is < 16 * 1024 or > 32L * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCaptureWorkspaceBytes), "SnapshotCaptureLimitExceeded");
        }

        long domainReservation;
        long actorReservation;
        try
        {
            domainReservation = checked(maximumPreCommandStateGraphBytes + maximumWorkingStateGraphBytes
                + maximumDetachedEventGraphBytes + 2L * maximumEventBytes
                + 2L * maximumStateJsonBytes + maximumDomainParsedStateGraphBytes
                + maximumEscapedWireStateBytes
                + maximumSerializerScratchBytes + 256L * maximumEventCount);
            actorReservation = checked(2L * maximumActorParsedStateBytes + maximumActorParsedGraphBytes
                + 4L * maximumCanonicalStateMeasurementBytes + 2L * maximumCanonicalEnvelopeBytes
                + maximumSerializerScratchBytes);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCaptureWorkspaceBytes), "SnapshotCaptureLimitExceeded");
        }

        if (maximumDetachedEventGraphBytes < maximumEventBytes + 256L * maximumEventCount
            || maximumEscapedWireStateBytes < 6L * maximumStateJsonBytes + 2
            || maximumActorParsedStateBytes < maximumStateJsonBytes
            || maximumCanonicalStateMeasurementBytes < maximumActorParsedStateBytes
            || maximumCanonicalEnvelopeBytes < (long)maximumCanonicalStateMeasurementBytes + 4096
            || domainReservation > maximumCaptureWorkspaceBytes
            || actorReservation > maximumActorWorkspaceBytes
            || domainReservation > maximumSimultaneousLiveBytes
            || actorReservation > maximumSimultaneousLiveBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCaptureWorkspaceBytes), "SnapshotCaptureLimitExceeded");
        }

        MaximumCaptureWorkspaceBytes = maximumCaptureWorkspaceBytes;
        MaximumPreCommandStateGraphBytes = maximumPreCommandStateGraphBytes;
        MaximumWorkingStateGraphBytes = maximumWorkingStateGraphBytes;
        MaximumDetachedEventGraphBytes = maximumDetachedEventGraphBytes;
        MaximumEventCount = maximumEventCount;
        MaximumEventBytes = maximumEventBytes;
        MaximumStateJsonBytes = maximumStateJsonBytes;
        MaximumDomainParsedStateGraphBytes = maximumDomainParsedStateGraphBytes;
        MaximumEscapedWireStateBytes = maximumEscapedWireStateBytes;
        MaximumActorParsedStateBytes = maximumActorParsedStateBytes;
        MaximumActorParsedGraphBytes = maximumActorParsedGraphBytes;
        MaximumCanonicalStateMeasurementBytes = maximumCanonicalStateMeasurementBytes;
        MaximumCanonicalEnvelopeBytes = maximumCanonicalEnvelopeBytes;
        MaximumNodeCount = maximumNodeCount;
        MaximumDepth = maximumDepth;
        MaximumSerializerScratchBytes = maximumSerializerScratchBytes;
        MaximumSimultaneousLiveBytes = maximumSimultaneousLiveBytes;
        MaximumActorWorkspaceBytes = maximumActorWorkspaceBytes;
        DomainReservationBytes = domainReservation;
        ActorReservationBytes = actorReservation;
    }

    /// <summary>Maximum live workspace held by the domain-side capture.</summary>
    public long MaximumCaptureWorkspaceBytes { get; }
    /// <summary>Maximum isolated pre-command state graph.</summary>
    public long MaximumPreCommandStateGraphBytes { get; }
    /// <summary>Maximum working state graph including Apply expansion.</summary>
    public long MaximumWorkingStateGraphBytes { get; }
    /// <summary>Maximum detached decoded event graph.</summary>
    public long MaximumDetachedEventGraphBytes { get; }
    /// <summary>Maximum admitted event count.</summary>
    public int MaximumEventCount { get; }
    /// <summary>Maximum combined canonical event payload bytes.</summary>
    public int MaximumEventBytes { get; }
    /// <summary>Maximum folded-state JSON bytes.</summary>
    public int MaximumStateJsonBytes { get; }
    /// <summary>Maximum parsed domain-side folded JSON graph held beside the working state.</summary>
    public long MaximumDomainParsedStateGraphBytes { get; }
    /// <summary>Maximum escaped optional wire-state scalar bytes.</summary>
    public int MaximumEscapedWireStateBytes { get; }
    /// <summary>Maximum actor-side evidence JSON bytes.</summary>
    public int MaximumActorParsedStateBytes { get; }
    /// <summary>Maximum actor-side parsed state graph.</summary>
    public long MaximumActorParsedGraphBytes { get; }
    /// <summary>Maximum canonical state measurement buffer.</summary>
    public int MaximumCanonicalStateMeasurementBytes { get; }
    /// <summary>Maximum complete canonical/protected envelope buffer.</summary>
    public int MaximumCanonicalEnvelopeBytes { get; }
    /// <summary>Maximum state JSON token count.</summary>
    public int MaximumNodeCount { get; }
    /// <summary>Maximum state JSON depth.</summary>
    public int MaximumDepth { get; }
    /// <summary>Reserved serializer growth and scratch allowance.</summary>
    public long MaximumSerializerScratchBytes { get; }
    /// <summary>Maximum simultaneous live library representations.</summary>
    public long MaximumSimultaneousLiveBytes { get; }
    /// <summary>Maximum actor-side workspace.</summary>
    public long MaximumActorWorkspaceBytes { get; }
    /// <summary>Declared conservative upper bound validated before domain capture starts.</summary>
    public long DomainReservationBytes { get; }
    /// <summary>Declared conservative upper bound validated before actor parsing starts.</summary>
    public long ActorReservationBytes { get; }
}
