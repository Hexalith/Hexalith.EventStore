using Hexalith.EventStore.Contracts.Snapshots;

namespace Hexalith.EventStore.Contracts.Tests.Snapshots;

public sealed class FoldedSnapshotCaptureLimitsTests
{
    [Fact]
    public void ExactDeclaredReservationsAreAdmittedAndOneByteLessIsRefused()
    {
        FoldedSnapshotCaptureLimits baseline = Create();
        long simultaneous = Math.Max(baseline.DomainReservationBytes, baseline.ActorReservationBytes);

        FoldedSnapshotCaptureLimits exact = Create(
            captureWorkspace: baseline.DomainReservationBytes,
            actorWorkspace: baseline.ActorReservationBytes,
            simultaneousLive: simultaneous);

        exact.DomainReservationBytes.ShouldBe(baseline.DomainReservationBytes);
        exact.ActorReservationBytes.ShouldBe(baseline.ActorReservationBytes);
        Refused(() => Create(captureWorkspace: baseline.DomainReservationBytes - 1));
        Refused(() => Create(actorWorkspace: baseline.ActorReservationBytes - 1));
        Refused(() => Create(simultaneousLive: simultaneous - 1));
    }

    [Fact]
    public void RangeAndRepresentationConsistencyRejectInvalidDeclarations()
    {
        Refused(() => Create(eventCount: 0));
        Refused(() => Create(eventCount: 1001));
        Refused(() => Create(maximumDepth: 65));
        Refused(() => Create(captureWorkspace: long.MaxValue));
        Refused(() => Create(detachedGraph: 1024 + 256L * 2 - 1));
        Refused(() => Create(escapedState: 6 * 1024 + 1));
        Refused(() => Create(actorState: 1023));
        Refused(() => Create(measurement: 1023));
        Refused(() => Create(envelope: 1024 + 4095));
    }

    private static void Refused(Func<FoldedSnapshotCaptureLimits> create)
        => Should.Throw<ArgumentOutOfRangeException>(create).Message.ShouldContain("SnapshotCaptureLimitExceeded");

    private static FoldedSnapshotCaptureLimits Create(
        long? captureWorkspace = null,
        long? actorWorkspace = null,
        long? simultaneousLive = null,
        long? detachedGraph = null,
        int? eventCount = null,
        int? escapedState = null,
        int? actorState = null,
        int? measurement = null,
        int? envelope = null,
        int? maximumDepth = null)
        => new(
            maximumCaptureWorkspaceBytes: captureWorkspace ?? 40_000,
            maximumPreCommandStateGraphBytes: 1024,
            maximumWorkingStateGraphBytes: 1024,
            maximumDetachedEventGraphBytes: detachedGraph ?? 4096,
            maximumEventCount: eventCount ?? 2,
            maximumEventBytes: 1024,
            maximumStateJsonBytes: 1024,
            maximumDomainParsedStateGraphBytes: 8192,
            maximumEscapedWireStateBytes: escapedState ?? 6146,
            maximumActorParsedStateBytes: actorState ?? 1024,
            maximumActorParsedGraphBytes: 8192,
            maximumCanonicalStateMeasurementBytes: measurement ?? 1024,
            maximumCanonicalEnvelopeBytes: envelope ?? 8192,
            maximumNodeCount: 100,
            maximumDepth: maximumDepth ?? 16,
            maximumSerializerScratchBytes: 2048,
            maximumSimultaneousLiveBytes: simultaneousLive ?? 40_000,
            maximumActorWorkspaceBytes: actorWorkspace ?? 40_000);
}
