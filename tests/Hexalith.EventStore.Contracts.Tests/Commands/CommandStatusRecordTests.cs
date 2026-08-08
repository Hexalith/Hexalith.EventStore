
using System.Text.Json;

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

    // --- Story 4.4: the recovery fields are additive trailing optionals ---

    [Fact]
    public void Deserialize_LegacyPayloadWithoutRecoveryFields_StillRoundTrips() {
        // A record persisted before Story 4.4 has no retryable/recovery members at all. It must
        // still deserialize, and the missing tri-state must read as null -- "written before the
        // field existed" -- never as false.
        const string legacyJson = """
        {
          "Status": 6,
          "Timestamp": "2026-07-01T10:00:00+00:00",
          "AggregateId": "order-123",
          "EventCount": 2,
          "RejectionEventType": null,
          "FailureReason": "Pub/sub broker unavailable",
          "TimeoutDuration": null,
          "MessageId": "01MESSAGELEGACY0000000000001",
          "CorrelationId": "01CORRELATIONLEGACY000000001"
        }
        """;

        CommandStatusRecord? record = JsonSerializer.Deserialize<CommandStatusRecord>(legacyJson);

        _ = record.ShouldNotBeNull();
        record.Status.ShouldBe(CommandStatus.PublishFailed);
        record.EventCount.ShouldBe(2);
        record.MessageId.ShouldBe("01MESSAGELEGACY0000000000001");
        record.CorrelationId.ShouldBe("01CORRELATIONLEGACY000000001");
        record.Retryable.ShouldBeNull();
        record.RecoveryReasonCode.ShouldBeNull();
        record.DrainAttemptCount.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithoutRecoveryArguments_LeavesTheTriStateUnset() {
        var record = new CommandStatusRecord(
            Status: CommandStatus.PublishFailed,
            Timestamp: DateTimeOffset.UtcNow,
            AggregateId: "order-123",
            EventCount: 1,
            RejectionEventType: null,
            FailureReason: "unavailable",
            TimeoutDuration: null);

        record.Retryable.ShouldBeNull();
        record.RecoveryReasonCode.ShouldBeNull();
        record.DrainAttemptCount.ShouldBeNull();
    }

    [Fact]
    public void RoundTrip_WithRecoveryFields_PreservesTheTriStateAndReasonCode() {
        var original = new CommandStatusRecord(
            CommandStatus.PublishFailed,
            DateTimeOffset.UtcNow,
            "order-123",
            3,
            null,
            "drain_attempts_exhausted",
            null,
            "01MESSAGE00000000000000000001",
            "01CORRELATION0000000000000001",
            Retryable: false,
            RecoveryReasonCode: "drain_attempts_exhausted",
            DrainAttemptCount: 8);

        CommandStatusRecord? roundTripped = JsonSerializer.Deserialize<CommandStatusRecord>(
            JsonSerializer.Serialize(original));

        _ = roundTripped.ShouldNotBeNull();
        roundTripped.Retryable.ShouldBe(false);
        roundTripped.RecoveryReasonCode.ShouldBe("drain_attempts_exhausted");
        roundTripped.DrainAttemptCount.ShouldBe(8);
    }
}
