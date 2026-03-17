using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

public class InMemoryBackpressureTrackerTests {
    private static InMemoryBackpressureTracker CreateTracker(int threshold = 100) {
        IOptions<BackpressureOptions> options = Options.Create(new BackpressureOptions { MaxPendingCommandsPerAggregate = threshold });
        return new InMemoryBackpressureTracker(options);
    }

    [Fact]
    public void TryAcquire_UnderThreshold_ReturnsTrue() {
        // Arrange
        InMemoryBackpressureTracker tracker = CreateTracker(threshold: 5);

        // Act
        bool result = tracker.TryAcquire("tenant-a:domain:agg-001");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void TryAcquire_AtThreshold_ReturnsFalse() {
        // Arrange
        InMemoryBackpressureTracker tracker = CreateTracker(threshold: 3);
        const string actorId = "tenant-a:domain:agg-001";

        // Fill to threshold
        for (int i = 0; i < 3; i++) {
            tracker.TryAcquire(actorId).ShouldBeTrue();
        }

        // Act — at threshold
        bool result = tracker.TryAcquire(actorId);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void TryAcquire_OverThreshold_ReturnsFalse() {
        // Arrange
        InMemoryBackpressureTracker tracker = CreateTracker(threshold: 2);
        const string actorId = "tenant-a:domain:agg-001";

        tracker.TryAcquire(actorId).ShouldBeTrue();
        tracker.TryAcquire(actorId).ShouldBeTrue();

        // Act — over threshold (repeated calls)
        bool result1 = tracker.TryAcquire(actorId);
        bool result2 = tracker.TryAcquire(actorId);

        // Assert
        result1.ShouldBeFalse();
        result2.ShouldBeFalse();
    }

    [Fact]
    public void Release_DecrementsCounter_AllowsNewAcquire() {
        // Arrange
        InMemoryBackpressureTracker tracker = CreateTracker(threshold: 2);
        const string actorId = "tenant-a:domain:agg-001";

        tracker.TryAcquire(actorId).ShouldBeTrue();
        tracker.TryAcquire(actorId).ShouldBeTrue();
        tracker.TryAcquire(actorId).ShouldBeFalse(); // At threshold

        // Act — release one slot
        tracker.Release(actorId);

        // Assert — can acquire again
        tracker.TryAcquire(actorId).ShouldBeTrue();
    }

    [Fact]
    public void TryAcquire_DifferentAggregates_Independent() {
        // Arrange
        InMemoryBackpressureTracker tracker = CreateTracker(threshold: 1);
        const string actorIdA = "tenant-a:domain:agg-A";
        const string actorIdB = "tenant-a:domain:agg-B";

        // Fill aggregate A to threshold
        tracker.TryAcquire(actorIdA).ShouldBeTrue();
        tracker.TryAcquire(actorIdA).ShouldBeFalse();

        // Act — aggregate B should be independent
        bool result = tracker.TryAcquire(actorIdB);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Release_BelowZero_FloorsAtZero() {
        // Arrange
        InMemoryBackpressureTracker tracker = CreateTracker(threshold: 5);
        const string actorId = "tenant-a:domain:agg-001";

        // Act — release without any acquire (should not throw or go negative)
        tracker.Release(actorId);
        tracker.Release(actorId);

        // Assert — can still acquire normally
        tracker.TryAcquire(actorId).ShouldBeTrue();
    }

    [Fact]
    public void TryAcquire_DefaultThreshold100_Allows100() {
        // Arrange — default threshold is 100
        InMemoryBackpressureTracker tracker = CreateTracker(threshold: 100);
        const string actorId = "tenant-a:domain:agg-001";

        // Act — acquire 100 times
        int successCount = 0;
        for (int i = 0; i < 100; i++) {
            if (tracker.TryAcquire(actorId)) {
                successCount++;
            }
        }

        // Assert
        successCount.ShouldBe(100);
        tracker.TryAcquire(actorId).ShouldBeFalse(); // 101st should fail
    }

    [Fact]
    public void TryAcquire_CustomThreshold_Respected() {
        // Arrange
        InMemoryBackpressureTracker tracker = CreateTracker(threshold: 50);
        const string actorId = "tenant-a:domain:agg-001";

        // Act — acquire up to custom threshold
        int successCount = 0;
        for (int i = 0; i < 51; i++) {
            if (tracker.TryAcquire(actorId)) {
                successCount++;
            }
        }

        // Assert
        successCount.ShouldBe(50);
    }

    [Fact]
    public async Task TryAcquire_ConcurrentAccess_ExactlyThresholdSucceed() {
        // Arrange
        InMemoryBackpressureTracker tracker = CreateTracker(threshold: 50);
        const string actorId = "tenant-a:domain:agg-001";
        int totalAttempts = 100;

        // Act — fire 100 parallel TryAcquire calls
        Task<bool>[] tasks = Enumerable.Range(0, totalAttempts)
            .Select(_ => Task.Run(() => tracker.TryAcquire(actorId)))
            .ToArray();

        bool[] results = await Task.WhenAll(tasks);

        // Assert — exactly 50 should return true
        int successCount = results.Count(r => r);
        successCount.ShouldBe(50);
    }

    [Fact]
    public void Release_ToZero_RemovesDictionaryEntry() {
        // Arrange
        InMemoryBackpressureTracker tracker = CreateTracker(threshold: 5);
        const string actorId = "tenant-a:domain:agg-001";

        tracker.TryAcquire(actorId).ShouldBeTrue();

        // Act — release back to 0
        tracker.Release(actorId);

        // Assert — dictionary entry should be removed (prevents unbounded growth)
        tracker.GetEntryCount().ShouldBe(0);
    }

    [Fact]
    public void TryAcquire_ThresholdZero_AlwaysReturnsTrue() {
        // Arrange — threshold 0 = disabled
        InMemoryBackpressureTracker tracker = CreateTracker(threshold: 0);
        const string actorId = "tenant-a:domain:agg-001";

        // Act — call many times
        bool result1 = tracker.TryAcquire(actorId);
        bool result2 = tracker.TryAcquire(actorId);
        bool result3 = tracker.TryAcquire(actorId);

        // Also verify Release doesn't throw when disabled
        tracker.Release(actorId);

        // Assert — all should return true (disabled)
        result1.ShouldBeTrue();
        result2.ShouldBeTrue();
        result3.ShouldBeTrue();
        tracker.GetEntryCount().ShouldBe(0); // Dictionary should be empty (threshold 0 never touches it)
    }
}
