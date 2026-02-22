namespace Hexalith.EventStore.Server.Tests.Events;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Events;

using Shouldly;

public class DeadLetterMessageTests {
    private static CommandEnvelope CreateTestEnvelope(
        string tenantId = "test-tenant",
        string? correlationId = null,
        string? causationId = null) => new(
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
        var command = CreateTestEnvelope();
        var failedAt = DateTimeOffset.UtcNow;

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
        var command = CreateTestEnvelope();
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
    public void FromException_ExtractsErrorMessage() {
        // Arrange
        var command = CreateTestEnvelope();
        var exception = new InvalidOperationException("State store unavailable");

        // Act
        var message = DeadLetterMessage.FromException(
            command,
            CommandStatus.Processing,
            exception);

        // Assert
        message.ErrorMessage.ShouldBe("State store unavailable");
        message.ErrorMessage.ShouldNotContain("at ");
        message.ErrorMessage.ShouldNotContain("StackTrace");
    }

    [Fact]
    public void FromException_PreservesFullCommandEnvelope() {
        // Arrange
        var command = CreateTestEnvelope();
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
        var command = CreateTestEnvelope();
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
        var command = CreateTestEnvelope();
        var exception = new InvalidOperationException("State store unavailable");
        var beforeCall = DateTimeOffset.UtcNow;

        // Act
        var message = DeadLetterMessage.FromException(
            command,
            CommandStatus.Processing,
            exception);

        // Assert
        var afterCall = DateTimeOffset.UtcNow;
        message.FailedAt.ShouldBeGreaterThanOrEqualTo(beforeCall);
        message.FailedAt.ShouldBeLessThanOrEqualTo(afterCall);
    }

    [Fact]
    public void FromException_NestedExceptionUsesOuterType() {
        // Arrange
        var command = CreateTestEnvelope();
        var innerException = new TimeoutException("Inner timeout");
        var outerException = new HttpRequestException("Connection failed", innerException);

        // Act
        var message = DeadLetterMessage.FromException(
            command,
            CommandStatus.Processing,
            outerException);

        // Assert
        message.ExceptionType.ShouldBe("HttpRequestException");
        message.ErrorMessage.ShouldBe("Connection failed");
    }
}
