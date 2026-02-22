
using Hexalith.EventStore.Server.Actors;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;
/// <summary>
/// Story 4.4 Task 10: UnpublishedEventsRecord unit tests.
/// Verifies key format, reminder name, IncrementRetry, and construction (AC: #2).
/// </summary>
public class UnpublishedEventsRecordTests {
    [Fact]
    public void GetStateKey_ReturnsCorrectFormat() {
        // Act
        string key = UnpublishedEventsRecord.GetStateKey("cmd-123");

        // Assert
        key.ShouldBe("drain:cmd-123");
    }

    [Fact]
    public void GetReminderName_ReturnsCorrectFormat() {
        // Act
        string name = UnpublishedEventsRecord.GetReminderName("cmd-456");

        // Assert
        name.ShouldBe("drain-unpublished-cmd-456");
    }

    [Fact]
    public void IncrementRetry_IncrementsCount_UpdatesReason() {
        // Arrange
        var record = new UnpublishedEventsRecord(
            "corr-1", 1, 3, 3, "CreateOrder", false,
            DateTimeOffset.UtcNow, RetryCount: 2, LastFailureReason: "old reason");

        // Act
        UnpublishedEventsRecord updated = record.IncrementRetry("new reason");

        // Assert
        updated.RetryCount.ShouldBe(3);
        updated.LastFailureReason.ShouldBe("new reason");
        updated.CorrelationId.ShouldBe("corr-1");
        updated.StartSequence.ShouldBe(1);
        updated.EndSequence.ShouldBe(3);
    }

    [Fact]
    public void IncrementRetry_WithNullReason_UpdatesReasonToNull() {
        // Arrange
        var record = new UnpublishedEventsRecord(
            "corr-1", 1, 3, 3, "CreateOrder", false,
            DateTimeOffset.UtcNow, RetryCount: 0, LastFailureReason: "previous");

        // Act
        UnpublishedEventsRecord updated = record.IncrementRetry(null);

        // Assert
        updated.RetryCount.ShouldBe(1);
        updated.LastFailureReason.ShouldBeNull();
    }

    [Fact]
    public void Construction_AllFieldsPreserved() {
        // Arrange
        DateTimeOffset failedAt = DateTimeOffset.UtcNow;

        // Act
        var record = new UnpublishedEventsRecord(
            CorrelationId: "corr-test",
            StartSequence: 5,
            EndSequence: 10,
            EventCount: 6,
            CommandType: "UpdateOrder",
            IsRejection: true,
            FailedAt: failedAt,
            RetryCount: 3,
            LastFailureReason: "timeout");

        // Assert
        record.CorrelationId.ShouldBe("corr-test");
        record.StartSequence.ShouldBe(5);
        record.EndSequence.ShouldBe(10);
        record.EventCount.ShouldBe(6);
        record.CommandType.ShouldBe("UpdateOrder");
        record.IsRejection.ShouldBeTrue();
        record.FailedAt.ShouldBe(failedAt);
        record.RetryCount.ShouldBe(3);
        record.LastFailureReason.ShouldBe("timeout");
    }

    [Fact]
    public void StateKeyPrefix_IsCorrectValue() => UnpublishedEventsRecord.StateKeyPrefix.ShouldBe("drain:");
}
