using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.DeadLetters;

public class DeadLetterEntryTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var entry = new DeadLetterEntry("msg-001", "acme", "orders", "order-123", "corr-001", "Timeout", DateTimeOffset.UtcNow, 3, "CreateOrder");

        entry.MessageId.ShouldBe("msg-001");
        entry.TenantId.ShouldBe("acme");
        entry.Domain.ShouldBe("orders");
        entry.AggregateId.ShouldBe("order-123");
        entry.CorrelationId.ShouldBe("corr-001");
        entry.FailureReason.ShouldBe("Timeout");
        entry.RetryCount.ShouldBe(3);
        entry.OriginalCommandType.ShouldBe("CreateOrder");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidMessageId_ThrowsArgumentException(string? messageId)
    {
        Should.Throw<ArgumentException>(() =>
            new DeadLetterEntry(messageId!, "acme", "orders", "order-123", "corr-001", "Fail", DateTimeOffset.UtcNow, 0, "Cmd"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTenantId_ThrowsArgumentException(string? tenantId)
    {
        Should.Throw<ArgumentException>(() =>
            new DeadLetterEntry("msg-001", tenantId!, "orders", "order-123", "corr-001", "Fail", DateTimeOffset.UtcNow, 0, "Cmd"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidFailureReason_ThrowsArgumentException(string? failureReason)
    {
        Should.Throw<ArgumentException>(() =>
            new DeadLetterEntry("msg-001", "acme", "orders", "order-123", "corr-001", failureReason!, DateTimeOffset.UtcNow, 0, "Cmd"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidDomain_ThrowsArgumentException(string? domain)
    {
        Should.Throw<ArgumentException>(() =>
            new DeadLetterEntry("msg-001", "acme", domain!, "order-123", "corr-001", "Fail", DateTimeOffset.UtcNow, 0, "Cmd"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidAggregateId_ThrowsArgumentException(string? aggregateId)
    {
        Should.Throw<ArgumentException>(() =>
            new DeadLetterEntry("msg-001", "acme", "orders", aggregateId!, "corr-001", "Fail", DateTimeOffset.UtcNow, 0, "Cmd"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidCorrelationId_ThrowsArgumentException(string? correlationId)
    {
        Should.Throw<ArgumentException>(() =>
            new DeadLetterEntry("msg-001", "acme", "orders", "order-123", correlationId!, "Fail", DateTimeOffset.UtcNow, 0, "Cmd"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidOriginalCommandType_ThrowsArgumentException(string? commandType)
    {
        Should.Throw<ArgumentException>(() =>
            new DeadLetterEntry("msg-001", "acme", "orders", "order-123", "corr-001", "Fail", DateTimeOffset.UtcNow, 0, commandType!));
    }

    [Fact]
    public void ToString_RedactsFailureReason()
    {
        var entry = new DeadLetterEntry("msg-001", "acme", "orders", "order-123", "corr-001", "Timeout with sensitive stack trace", DateTimeOffset.UtcNow, 3, "CreateOrder");

        string result = entry.ToString();

        result.ShouldContain("[REDACTED]");
        result.ShouldContain("msg-001");
        result.ShouldNotContain("Timeout with sensitive stack trace");
    }
}
