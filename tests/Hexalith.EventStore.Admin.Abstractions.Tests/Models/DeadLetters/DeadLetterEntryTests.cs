using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.DeadLetters;

public class DeadLetterEntryTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
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
    public void Constructor_WithInvalidMessageId_ThrowsArgumentException(string? messageId) => Should.Throw<ArgumentException>(() =>
                                                                                                        new DeadLetterEntry(messageId!, "acme", "orders", "order-123", "corr-001", "Fail", DateTimeOffset.UtcNow, 0, "Cmd"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTenantId_ThrowsArgumentException(string? tenantId) => Should.Throw<ArgumentException>(() =>
                                                                                                      new DeadLetterEntry("msg-001", tenantId!, "orders", "order-123", "corr-001", "Fail", DateTimeOffset.UtcNow, 0, "Cmd"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidFailureReason_ThrowsArgumentException(string? failureReason) => Should.Throw<ArgumentException>(() =>
                                                                                                                new DeadLetterEntry("msg-001", "acme", "orders", "order-123", "corr-001", failureReason!, DateTimeOffset.UtcNow, 0, "Cmd"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidDomain_ThrowsArgumentException(string? domain) => Should.Throw<ArgumentException>(() =>
                                                                                                  new DeadLetterEntry("msg-001", "acme", domain!, "order-123", "corr-001", "Fail", DateTimeOffset.UtcNow, 0, "Cmd"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidAggregateId_ThrowsArgumentException(string? aggregateId) => Should.Throw<ArgumentException>(() =>
                                                                                                            new DeadLetterEntry("msg-001", "acme", "orders", aggregateId!, "corr-001", "Fail", DateTimeOffset.UtcNow, 0, "Cmd"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidCorrelationId_ThrowsArgumentException(string? correlationId) => Should.Throw<ArgumentException>(() =>
                                                                                                                new DeadLetterEntry("msg-001", "acme", "orders", "order-123", correlationId!, "Fail", DateTimeOffset.UtcNow, 0, "Cmd"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidOriginalCommandType_ThrowsArgumentException(string? commandType) => Should.Throw<ArgumentException>(() =>
                                                                                                                    new DeadLetterEntry("msg-001", "acme", "orders", "order-123", "corr-001", "Fail", DateTimeOffset.UtcNow, 0, commandType!));

    [Fact]
    public void PoisonEvidence_RoundTripsAndLegacyPayloadLeavesEvidenceUnavailable() {
        DateTimeOffset original = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);
        var entry = new DeadLetterEntry("msg-001", "acme", "orders", "order-123", "corr-001", "ProcessingFailure", original.AddMinutes(5), 0, "OrderCreated") {
            CausationId = "cause-001",
            SequenceNumber = 7,
            OriginalTimestampUtc = original,
            OriginalTopic = "acme.orders.events",
            DeliveryAttempts = 10,
            FirstFailureAtUtc = original.AddMinutes(1),
            LastFailureAtUtc = original.AddMinutes(4),
            FailureCategory = "ProcessingFailure",
            Retryable = true,
        };

        DeadLetterEntry restored = JsonSerializer.Deserialize<DeadLetterEntry>(
            JsonSerializer.Serialize(entry, JsonSerializerOptions.Web), JsonSerializerOptions.Web).ShouldNotBeNull();

        restored.ShouldBe(entry);
        const string legacy = """{"messageId":"msg-001","tenantId":"acme","domain":"orders","aggregateId":"order-123","correlationId":"corr-001","failureReason":"retained","failedAtUtc":"2026-11-26T10:00:00+00:00","retryCount":0,"originalCommandType":"OrderCreated"}""";
        DeadLetterEntry legacyEntry = JsonSerializer.Deserialize<DeadLetterEntry>(legacy, JsonSerializerOptions.Web).ShouldNotBeNull();
        legacyEntry.MessageId.ShouldBe("msg-001");
        legacyEntry.CausationId.ShouldBeNull();
        legacyEntry.SequenceNumber.ShouldBeNull();
        legacyEntry.DeliveryAttempts.ShouldBeNull();
        legacyEntry.FailureCategory.ShouldBeNull();
        legacyEntry.Retryable.ShouldBeNull();
    }

    [Fact]
    public void Summary_RoundTripsCountAndOptionalOldestAge() {
        var summary = new DeadLetterSummary(3, TimeSpan.FromMinutes(42));

        JsonSerializer.Deserialize<DeadLetterSummary>(JsonSerializer.Serialize(summary, JsonSerializerOptions.Web), JsonSerializerOptions.Web)
            .ShouldBe(summary);
        JsonSerializer.Deserialize<DeadLetterSummary>("""{"count":0}""", JsonSerializerOptions.Web)
            .ShouldBe(new DeadLetterSummary(0, null));
    }

    [Fact]
    public void ToString_RedactsFailureReason() {
        var entry = new DeadLetterEntry("msg-001", "acme", "orders", "order-123", "corr-001", "Timeout with sensitive stack trace", DateTimeOffset.UtcNow, 3, "CreateOrder");

        string result = entry.ToString();

        result.ShouldContain("[REDACTED]");
        result.ShouldContain("msg-001");
        result.ShouldNotContain("Timeout with sensitive stack trace");
    }
}
