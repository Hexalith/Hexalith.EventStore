using System.Runtime.CompilerServices;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

// ATDD red-phase scaffolds for story:
//   _bmad-output/implementation-artifacts/post-epic-deferred-dw1-projection-and-drain-hardening.md
// AC #5: Tracker enumeration corruption must be bounded and observable. Scope-index and
// identity-index corruption paths must each emit a stable disposition reason code and must
// not produce a tight retry loop within one polling tick.
//
// Stable tracker disposition vocabulary:
//   tracker_corrupt_scope_index, tracker_corrupt_identity_index,
//   tracker_recovered, tracker_terminal_failure.
public class Dw1PollerCorruptionAtddTests {
    private const string SkipReasonAc5 = "ATDD red phase — DW1 AC#5 (tracker corruption boundedness). Remove Skip when implementing.";

    private static readonly AggregateIdentity FastIdentity = new("tenant", "fast", "agg-1");

    [Fact(Skip = SkipReasonAc5)]
    public async Task PollOnceAsync_ScopeIndexCorruption_EmitsTrackerCorruptScopeIndexReasonCode() {
        // Tracker enumeration fails with an exception carrying the scope-index marker.
        // The poller must classify it as tracker_corrupt_scope_index — distinct from a
        // generic EnumerationFailed with no scope classification.
        var trackerException = new InvalidOperationException("scope index page corrupt");
        trackerException.Data["ExhaustionScope"] = "ScopeIndex";
        var tracker = new ThrowingTracker(trackerException);
        var entries = new List<LogEntry>();
        ProjectionPollerService service = CreateService(tracker, entries);

        await service.PollOnceAsync(DateTimeOffset.UtcNow);

        entries.ShouldContain(e => e.Message.Contains("tracker_corrupt_scope_index"));
    }

    [Fact(Skip = SkipReasonAc5)]
    public async Task PollOnceAsync_IdentityIndexCorruption_EmitsTrackerCorruptIdentityIndexReasonCode() {
        // Identity-page index corruption must classify distinctly from scope-index corruption
        // so operators can route remediation differently (per scope vs per identity).
        var trackerException = new InvalidOperationException("identity index page corrupt");
        trackerException.Data["ExhaustionScope"] = "IdentityIndex";
        var tracker = new ThrowingTracker(trackerException);
        var entries = new List<LogEntry>();
        ProjectionPollerService service = CreateService(tracker, entries);

        await service.PollOnceAsync(DateTimeOffset.UtcNow);

        entries.ShouldContain(e => e.Message.Contains("tracker_corrupt_identity_index"));
    }

    [Fact(Skip = SkipReasonAc5)]
    public async Task PollOnceAsync_TrackerCorruption_BoundedRetryWithinSameTick() {
        // Boundedness invariant: corrupt tracker state must NOT cause repeated re-enumeration
        // within a single tick. The fake tracker counts how many times its enumerator was
        // entered; a bounded poller calls it at most once per PollOnceAsync.
        var trackerException = new InvalidOperationException("scope page corrupt");
        trackerException.Data["ExhaustionScope"] = "ScopeIndex";
        var tracker = new ThrowingTracker(trackerException);
        ProjectionPollerService service = CreateService(tracker, []);

        await service.PollOnceAsync(DateTimeOffset.UtcNow);

        tracker.EnumerationCount.ShouldBe(1);
    }

    [Fact(Skip = SkipReasonAc5)]
    public async Task PollOnceAsync_TrackerCorruption_AdvancesNextDueToPreventTightRetryStorm() {
        // After a corrupt-tracker tick, the next-due schedule for the configured polling
        // domain must be advanced by at least the configured interval. Otherwise the next
        // tick re-fires immediately and the poller hammers the failing tracker.
        var trackerException = new InvalidOperationException("scope page corrupt");
        trackerException.Data["ExhaustionScope"] = "ScopeIndex";
        var tracker = new ThrowingTracker(trackerException);
        IProjectionPollerDeliveryGateway gateway = Substitute.For<IProjectionPollerDeliveryGateway>();
        ProjectionPollerService service = new(
            tracker,
            gateway,
            Options.Create(new ProjectionOptions {
                Domains = new Dictionary<string, DomainProjectionOptions> {
                    ["fast"] = new() { RefreshIntervalMs = 1000 },
                },
            }),
            new NoopTickSource(),
            new FakeTimeProvider(DateTimeOffset.UtcNow),
            new TestLogger<ProjectionPollerService>([]));

        DateTimeOffset start = DateTimeOffset.UtcNow;
        await service.PollOnceAsync(start);
        // Even if a downstream caller fires another tick at start+1ms, no enumeration retry should run.
        await service.PollOnceAsync(start.AddMilliseconds(1));

        // First tick triggered enumeration (which threw); second tick at start+1ms must be
        // suppressed by next-due advancement, NOT by re-entering enumeration.
        tracker.EnumerationCount.ShouldBe(1);
    }

    private static ProjectionPollerService CreateService(IProjectionCheckpointTracker tracker, List<LogEntry> entries) =>
        new(
            tracker,
            Substitute.For<IProjectionPollerDeliveryGateway>(),
            Options.Create(new ProjectionOptions { DefaultRefreshIntervalMs = 1000 }),
            new NoopTickSource(),
            new FakeTimeProvider(DateTimeOffset.UtcNow),
            new TestLogger<ProjectionPollerService>(entries));

    private sealed class ThrowingTracker(Exception toThrow) : IProjectionCheckpointTracker {
        public int EnumerationCount { get; private set; }

        public Task<long> ReadLastDeliveredSequenceAsync(AggregateIdentity identity, CancellationToken cancellationToken = default) =>
            Task.FromResult(0L);

        public Task<bool> SaveDeliveredSequenceAsync(AggregateIdentity identity, long deliveredSequence, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task TrackIdentityAsync(AggregateIdentity identity, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

#pragma warning disable CS1998, CS0162 // async lacks await + unreachable yield — required by IAsyncEnumerable signature for throw-only iterator
        public async IAsyncEnumerable<AggregateIdentity> EnumerateTrackedIdentitiesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            EnumerationCount++;
            throw toThrow;
            yield break;
        }
#pragma warning restore CS1998, CS0162
    }

    private sealed class NoopTickSource : IProjectionPollerTickSource {
        public Task<bool> WaitForNextTickAsync(TimeSpan interval, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }
}
