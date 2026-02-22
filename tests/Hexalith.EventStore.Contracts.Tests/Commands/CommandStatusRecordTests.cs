
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

        Assert.Equal(CommandStatus.Completed, record.Status);
        Assert.Equal(timestamp, record.Timestamp);
        Assert.Equal("order-123", record.AggregateId);
        Assert.Equal(3, record.EventCount);
        Assert.Null(record.RejectionEventType);
        Assert.Null(record.FailureReason);
        Assert.Null(record.TimeoutDuration);
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

        Assert.Equal(CommandStatus.Rejected, record.Status);
        Assert.Equal("OrderRejected", record.RejectionEventType);
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

        Assert.Equal(CommandStatus.PublishFailed, record.Status);
        Assert.Equal("Pub/sub broker unavailable", record.FailureReason);
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

        Assert.Equal(CommandStatus.TimedOut, record.Status);
        Assert.Equal(timeout, record.TimeoutDuration);
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

        Assert.Equal(CommandStatus.Processing, record.Status);
        Assert.Null(record.AggregateId);
        Assert.Null(record.EventCount);
        Assert.Null(record.RejectionEventType);
        Assert.Null(record.FailureReason);
        Assert.Null(record.TimeoutDuration);
    }
}
