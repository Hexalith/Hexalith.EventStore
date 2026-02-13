namespace Hexalith.EventStore.Server.Tests.Commands;

using Hexalith.EventStore.Server.Commands;

using Shouldly;

public class ConcurrencyConflictExceptionTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsProperties()
    {
        // Arrange & Act
        var inner = new InvalidOperationException("ETag mismatch");
        var ex = new ConcurrencyConflictException(
            correlationId: "corr-123",
            aggregateId: "order-456",
            tenantId: "acme",
            detail: "Custom conflict detail",
            conflictSource: "StateStore",
            innerException: inner);

        // Assert
        ex.CorrelationId.ShouldBe("corr-123");
        ex.AggregateId.ShouldBe("order-456");
        ex.TenantId.ShouldBe("acme");
        ex.Message.ShouldBe("Custom conflict detail");
        ex.ConflictSource.ShouldBe("StateStore");
        ex.InnerException.ShouldBe(inner);
    }

    [Fact]
    public void Constructor_WithNullDetail_UsesDefaultMessage()
    {
        // Arrange & Act
        var ex = new ConcurrencyConflictException(
            correlationId: "corr-123",
            aggregateId: "order-456");

        // Assert
        ex.Message.ShouldContain("order-456");
        ex.Message.ShouldContain("optimistic concurrency conflict");
        ex.Message.ShouldContain("Retry the command");
    }

    [Fact]
    public void Constructor_WithCustomDetail_UsesCustomMessage()
    {
        // Arrange & Act
        var ex = new ConcurrencyConflictException(
            correlationId: "corr-123",
            aggregateId: "order-456",
            detail: "My custom detail message");

        // Assert
        ex.Message.ShouldBe("My custom detail message");
    }

    [Fact]
    public void Constructor_WithInnerException_PreservesInnerException()
    {
        // Arrange
        var inner = new InvalidOperationException("ETag mismatch");

        // Act
        var ex = new ConcurrencyConflictException(
            correlationId: "corr-123",
            aggregateId: "order-456",
            innerException: inner);

        // Assert
        ex.InnerException.ShouldBe(inner);
        ex.InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_Parameterless_SetsDefaults()
    {
        // Arrange & Act
        var ex = new ConcurrencyConflictException();

        // Assert
        ex.CorrelationId.ShouldBe(string.Empty);
        ex.AggregateId.ShouldBe(string.Empty);
        ex.TenantId.ShouldBeNull();
        ex.ConflictSource.ShouldBeNull();
        ex.Message.ShouldContain("optimistic concurrency conflict");
    }

    [Fact]
    public void Constructor_MessageOnly_SetsMessage()
    {
        // Arrange & Act
        var ex = new ConcurrencyConflictException("Custom message");

        // Assert
        ex.Message.ShouldBe("Custom message");
        ex.CorrelationId.ShouldBe(string.Empty);
        ex.AggregateId.ShouldBe(string.Empty);
    }

    [Fact]
    public void Constructor_MessageAndInner_PreservesInnerException()
    {
        // Arrange
        var inner = new InvalidOperationException("root cause");

        // Act
        var ex = new ConcurrencyConflictException("Custom message", inner);

        // Assert
        ex.Message.ShouldBe("Custom message");
        ex.InnerException.ShouldBe(inner);
        ex.CorrelationId.ShouldBe(string.Empty);
        ex.AggregateId.ShouldBe(string.Empty);
    }

    [Fact]
    public void Constructor_WithConflictSource_SetsProperty()
    {
        // Arrange & Act
        var ex = new ConcurrencyConflictException(
            correlationId: "corr-123",
            aggregateId: "order-456",
            conflictSource: "ActorReentrancy");

        // Assert
        ex.ConflictSource.ShouldBe("ActorReentrancy");
    }
}
