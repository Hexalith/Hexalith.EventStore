
using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Contracts.Tests.Commands;

public class CommandStatusRecordTests {
    [Fact]
    public void Constructor_WithCompletedStatus_SetsEventCount() {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var record = new CommandStatusRecord(
            Status: CommandStatus.Completed,
            Timestamp: timestamp,
            AggregateId: "order-123",
            EventCount: 3,
            RejectionEventType: null,
            FailureReason: null,
            TimeoutDuration: null);

        record.Status.ShouldBe(CommandStatus.Completed);
        record.Timestamp.ShouldBe(timestamp);
        record.AggregateId.ShouldBe("order-123");
        record.EventCount.ShouldBe(3);
        record.RejectionEventType.ShouldBeNull();
        record.FailureReason.ShouldBeNull();
        record.TimeoutDuration.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithRejectedStatus_SetsRejectionEventType() {
        var record = new CommandStatusRecord(
            Status: CommandStatus.Rejected,
            Timestamp: DateTimeOffset.UtcNow,
            AggregateId: "order-123",
            EventCount: null,
            RejectionEventType: "OrderRejected",
            FailureReason: null,
            TimeoutDuration: null);

        record.Status.ShouldBe(CommandStatus.Rejected);
        record.RejectionEventType.ShouldBe("OrderRejected");
    }

    [Fact]
    public void Constructor_WithPublishFailedStatus_SetsFailureReason() {
        var record = new CommandStatusRecord(
            Status: CommandStatus.PublishFailed,
            Timestamp: DateTimeOffset.UtcNow,
            AggregateId: "order-123",
            EventCount: null,
            RejectionEventType: null,
            FailureReason: "Pub/sub broker unavailable",
            TimeoutDuration: null);

        record.Status.ShouldBe(CommandStatus.PublishFailed);
        record.FailureReason.ShouldBe("Pub/sub broker unavailable");
    }

    [Fact]
    public void Constructor_WithTimedOutStatus_SetsTimeoutDuration() {
        var timeout = TimeSpan.FromSeconds(30);
        var record = new CommandStatusRecord(
            Status: CommandStatus.TimedOut,
            Timestamp: DateTimeOffset.UtcNow,
            AggregateId: "order-123",
            EventCount: null,
            RejectionEventType: null,
            FailureReason: null,
            TimeoutDuration: timeout);

        record.Status.ShouldBe(CommandStatus.TimedOut);
        record.TimeoutDuration.ShouldBe(timeout);
    }

    [Fact]
    public void Constructor_WithNonTerminalStatus_AllOptionalFieldsNull() {
        var record = new CommandStatusRecord(
            Status: CommandStatus.Processing,
            Timestamp: DateTimeOffset.UtcNow,
            AggregateId: null,
            EventCount: null,
            RejectionEventType: null,
            FailureReason: null,
            TimeoutDuration: null);

        record.Status.ShouldBe(CommandStatus.Processing);
        record.AggregateId.ShouldBeNull();
        record.EventCount.ShouldBeNull();
        record.RejectionEventType.ShouldBeNull();
        record.FailureReason.ShouldBeNull();
        record.TimeoutDuration.ShouldBeNull();
    }
}
