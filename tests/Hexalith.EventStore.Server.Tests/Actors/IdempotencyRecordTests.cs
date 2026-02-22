
using System.Text.Json;

using Hexalith.EventStore.Server.Actors;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class IdempotencyRecordTests {
    [Fact]
    public void FromResult_MapsAllFields() {
        // Arrange
        var result = new CommandProcessingResult(Accepted: true, ErrorMessage: null, CorrelationId: "corr-123");

        // Act
        var record = IdempotencyRecord.FromResult("cause-456", result);

        // Assert
        record.CausationId.ShouldBe("cause-456");
        record.CorrelationId.ShouldBe("corr-123");
        record.Accepted.ShouldBeTrue();
        record.ErrorMessage.ShouldBeNull();
        record.ProcessedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public void ToResult_ReconstructsCommandProcessingResult() {
        // Arrange
        var record = new IdempotencyRecord("cause-1", "corr-1", true, null, DateTimeOffset.UtcNow);

        // Act
        CommandProcessingResult result = record.ToResult();

        // Assert
        result.Accepted.ShouldBeTrue();
        result.CorrelationId.ShouldBe("corr-1");
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void ToResult_WithError_ReconstructsCorrectly() {
        // Arrange
        var record = new IdempotencyRecord("cause-1", "corr-1", false, "Something failed", DateTimeOffset.UtcNow);

        // Act
        CommandProcessingResult result = record.ToResult();

        // Assert
        result.Accepted.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Something failed");
        result.CorrelationId.ShouldBe("corr-1");
    }

    [Fact]
    public void JsonRoundtrip_PreservesAllFields() {
        // Arrange
        var original = new IdempotencyRecord(
            "cause-abc",
            "corr-xyz",
            true,
            null,
            new DateTimeOffset(2026, 2, 14, 12, 0, 0, TimeSpan.Zero));

        // Act
        string json = JsonSerializer.Serialize(original);
        IdempotencyRecord? deserialized = JsonSerializer.Deserialize<IdempotencyRecord>(json);

        // Assert
        _ = deserialized.ShouldNotBeNull();
        deserialized.CausationId.ShouldBe(original.CausationId);
        deserialized.CorrelationId.ShouldBe(original.CorrelationId);
        deserialized.Accepted.ShouldBe(original.Accepted);
        deserialized.ErrorMessage.ShouldBe(original.ErrorMessage);
        deserialized.ProcessedAt.ShouldBe(original.ProcessedAt);
    }
}
