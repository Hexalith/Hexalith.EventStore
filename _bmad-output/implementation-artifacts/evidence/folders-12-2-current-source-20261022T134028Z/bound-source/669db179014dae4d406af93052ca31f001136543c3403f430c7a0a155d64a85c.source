
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;

public class DeadLetterMessageTests {
    private static CommandEnvelope CreateTestEnvelope(
        string tenantId = "test-tenant",
        string? correlationId = null,
        string? causationId = null) => new(
        MessageId: Guid.NewGuid().ToString(),
        TenantId: tenantId,
        Domain: "test-domain",
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: correlationId ?? Guid.NewGuid().ToString(),
        CausationId: causationId,
        UserId: "system",
        Extensions: null);

    [Fact]
    public void Construction_AllFieldsPreserved() {
        // Arrange
        CommandEnvelope command = CreateTestEnvelope();
        DateTimeOffset failedAt = DateTimeOffset.UtcNow;

        // Act
        var message = new DeadLetterMessage(
            Command: command,
            FailureStage: "Processing",
            ExceptionType: "HttpRequestException",
            ErrorMessage: "Connection timeout",
            CorrelationId: command.CorrelationId,
            CausationId: command.CausationId,
            TenantId: command.TenantId,
            Domain: command.Domain,
            AggregateId: command.AggregateId,
            CommandType: command.CommandType,
            FailedAt: failedAt,
            EventCountAtFailure: 5);

        // Assert
        message.Command.ShouldBe(command);
        message.FailureStage.ShouldBe("Processing");
        message.ExceptionType.ShouldBe("HttpRequestException");
        message.ErrorMessage.ShouldBe("Connection timeout");
        message.CorrelationId.ShouldBe(command.CorrelationId);
        message.CausationId.ShouldBe(command.CausationId);
        message.TenantId.ShouldBe("test-tenant");
        message.Domain.ShouldBe("test-domain");
        message.AggregateId.ShouldBe("agg-001");
        message.CommandType.ShouldBe("CreateOrder");
        message.FailedAt.ShouldBe(failedAt);
        message.EventCountAtFailure.ShouldBe(5);
    }

    [Fact]
    public void FromException_ExtractsExceptionType() {
        // Arrange
        CommandEnvelope command = CreateTestEnvelope();
        var exception = new InvalidOperationException("State store unavailable");

        // Act
        var message = DeadLetterMessage.FromException(
            command,
            CommandStatus.Processing,
            exception);

        // Assert
        message.ExceptionType.ShouldBe("InvalidOperationException");
    }

    [Fact]
    public void FromException_StoresSafeDiagnosticMessage() {
        // Arrange
        CommandEnvelope command = CreateTestEnvelope();
        var exception = new InvalidOperationException("State store unavailable");

        // Act
        var message = DeadLetterMessage.FromException(
            command,
            CommandStatus.Processing,
            exception);

        // Assert
        message.ErrorMessage.ShouldContain("Protected data diagnostic details were redacted.");
        message.ErrorMessage.ShouldContain("ReasonCode=protected-data-diagnostic-redacted");
        message.ErrorMessage.ShouldContain("Stage=Processing");
        message.ErrorMessage.ShouldNotContain("State store unavailable");
        message.ErrorMessage.ShouldNotContain("at ");
        message.ErrorMessage.ShouldNotContain("StackTrace");
    }

    [Fact]
    public void FromException_PreservesFullCommandEnvelope() {
        // Arrange
        CommandEnvelope command = CreateTestEnvelope();
        var exception = new InvalidOperationException("State store unavailable");

        // Act
        var message = DeadLetterMessage.FromException(
            command,
            CommandStatus.Processing,
            exception);

        // Assert
        ReferenceEquals(message.Command, command).ShouldBeTrue();
        message.Command.TenantId.ShouldBe(command.TenantId);
        message.Command.Domain.ShouldBe(command.Domain);
        message.Command.AggregateId.ShouldBe(command.AggregateId);
        message.Command.CommandType.ShouldBe(command.CommandType);
        message.Command.CorrelationId.ShouldBe(command.CorrelationId);
        message.Command.Payload.ShouldBe(command.Payload);
    }

    [Fact]
    public void FromException_SetsCorrectFailureStage() {
        // Arrange
        CommandEnvelope command = CreateTestEnvelope();
        var exception = new InvalidOperationException("State store unavailable");

        // Act
        var message = DeadLetterMessage.FromException(
            command,
            CommandStatus.EventsStored,
            exception);

        // Assert
        message.FailureStage.ShouldBe("EventsStored");
    }

    [Fact]
    public void FromException_SetsFailedAtTimestamp() {
        // Arrange
        CommandEnvelope command = CreateTestEnvelope();
        var exception = new InvalidOperationException("State store unavailable");
        DateTimeOffset beforeCall = DateTimeOffset.UtcNow;

        // Act
        var message = DeadLetterMessage.FromException(
            command,
            CommandStatus.Processing,
            exception);

        // Assert
        DateTimeOffset afterCall = DateTimeOffset.UtcNow;
        message.FailedAt.ShouldBeGreaterThanOrEqualTo(beforeCall);
        message.FailedAt.ShouldBeLessThanOrEqualTo(afterCall);
    }

    [Fact]
    public void FromException_NestedExceptionUsesOuterType() {
        // Arrange
        CommandEnvelope command = CreateTestEnvelope();
        var innerException = new TimeoutException("Inner timeout");
        var outerException = new HttpRequestException("Connection failed", innerException);

        // Act
        var message = DeadLetterMessage.FromException(
            command,
            CommandStatus.Processing,
            outerException);

        // Assert
        message.ExceptionType.ShouldBe("HttpRequestException");
        message.ErrorMessage.ShouldContain("Protected data diagnostic details were redacted.");
        message.ErrorMessage.ShouldNotContain("Connection failed");
    }

    // --- Story 4.4: the drain-exhaustion sink envelope ---

    private static readonly AggregateIdentity _identity = new("test-tenant", "test-domain", "agg-001");

    private static UnpublishedEventsRecord CreateExhaustedRecord(string? messageId)
        => new(
            CorrelationId: "corr-exhausted",
            StartSequence: 4,
            EndSequence: 6,
            EventCount: 3,
            CommandType: "CreateOrder",
            IsRejection: false,
            FailedAt: DateTimeOffset.UtcNow,
            RetryCount: 8,
            LastFailureReason: "Pub/sub unavailable",
            MessageId: messageId);

    [Fact]
    public void FromException_KeepsTheOriginalEnvelopeReplayEligible() {
        var message = DeadLetterMessage.FromException(
            CreateTestEnvelope(), CommandStatus.Processing, new InvalidOperationException("boom"));

        message.ReplayEligible.ShouldBeTrue();
        message.ReasonCode.ShouldBeNull();
    }

    [Fact]
    public void FromDrainExhaustion_ReducedEnvelopeIsMarkedNotReplayEligible() {
        DeadLetterMessage message = DeadLetterMessage.FromDrainExhaustion(
            _identity, CreateExhaustedRecord("msg-exhausted"), "msg-exhausted", 8);

        message.ReplayEligible.ShouldBeFalse();
        message.ReasonCode.ShouldBe(DrainReasonCodes.AttemptsExhausted);
        message.FailureStage.ShouldBe(nameof(CommandStatus.PublishFailed));
        message.Command.Payload.ShouldBeEmpty("the reduced envelope carries no original command bytes");
    }

    [Fact]
    public void FromDrainExhaustion_CarriesTheCommittedRangeAndAttemptCount() {
        DeadLetterMessage message = DeadLetterMessage.FromDrainExhaustion(
            _identity, CreateExhaustedRecord("msg-exhausted"), "msg-exhausted", 8);

        message.StartSequence.ShouldBe(4);
        message.EndSequence.ShouldBe(6);
        message.EventCountAtFailure.ShouldBe(3);
        message.DrainAttempts.ShouldBe(8);
        message.CorrelationId.ShouldBe("corr-exhausted");
        message.TenantId.ShouldBe("test-tenant");
        message.Domain.ShouldBe("test-domain");
        message.AggregateId.ShouldBe("agg-001");
        message.CommandType.ShouldBe("CreateOrder");
    }

    [Fact]
    public void FromDrainExhaustion_RecordWithoutMessageId_FallsBackToTheTrackingId() {
        // Legacy correlation-keyed drain records have no MessageId; the envelope must still carry a
        // usable identity rather than throwing on the CommandEnvelope validation.
        DeadLetterMessage message = DeadLetterMessage.FromDrainExhaustion(
            _identity, CreateExhaustedRecord(messageId: null), "corr-exhausted", 8);

        message.Command.MessageId.ShouldBe("corr-exhausted");
    }

    [Fact]
    public void FromDrainExhaustion_RecordWithMessageId_PrefersTheMessageIdOverTheTrackingId() {
        DeadLetterMessage message = DeadLetterMessage.FromDrainExhaustion(
            _identity, CreateExhaustedRecord("msg-exhausted"), "legacy-tracking-id", 8);

        message.Command.MessageId.ShouldBe("msg-exhausted");
    }
}
