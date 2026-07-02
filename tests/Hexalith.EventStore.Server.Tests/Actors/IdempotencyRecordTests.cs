
using System.Text.Json;

using Hexalith.EventStore.Server.Actors;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class IdempotencyRecordTests {
    [Fact]
    public void FromResult_MapsAllFields() {
        // Arrange
        var result = new CommandProcessingResult(
            Accepted: true,
            ErrorMessage: null,
            CorrelationId: "corr-123",
            EventCount: 4,
            ResultPayload: """{"ok":true}""");
        DateTimeOffset before = DateTimeOffset.UtcNow;

        // Act
        var record = IdempotencyRecord.FromResult("cause-456", result);
        DateTimeOffset after = DateTimeOffset.UtcNow;

        // Assert
        record.CausationId.ShouldBe("cause-456");
        record.CorrelationId.ShouldBe("corr-123");
        record.Accepted.ShouldBeTrue();
        record.ErrorMessage.ShouldBeNull();
        record.EventCount.ShouldBe(4);
        record.ResultPayload.ShouldBe("""{"ok":true}""");
        record.ProcessedAt.ShouldBeGreaterThanOrEqualTo(before);
        record.ProcessedAt.ShouldBeLessThanOrEqualTo(after);
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
    public void ToResult_Preserves_EventCountAndResultPayload() {
        // Arrange — original result has non-default EventCount and ResultPayload
        var original = new CommandProcessingResult(
            Accepted: true,
            ErrorMessage: null,
            CorrelationId: "corr-1",
            EventCount: 5,
            ResultPayload: "{\"key\":\"value\"}");
        var record = IdempotencyRecord.FromResult("cause-1", original);

        // Act
        CommandProcessingResult roundtripped = record.ToResult();

        // Assert
        roundtripped.Accepted.ShouldBeTrue();
        roundtripped.CorrelationId.ShouldBe("corr-1");
        roundtripped.ErrorMessage.ShouldBeNull();
        roundtripped.EventCount.ShouldBe(5);
        roundtripped.ResultPayload.ShouldBe("{\"key\":\"value\"}");
    }

    [Fact]
    public void JsonRoundtrip_PreservesAllFields() {
        // Arrange
        var original = new IdempotencyRecord(
            "cause-abc",
            "corr-xyz",
            true,
            null,
            new DateTimeOffset(2026, 2, 14, 12, 0, 0, TimeSpan.Zero),
            EventCount: 7,
            ResultPayload: """{"result":"cached"}""",
            BackpressureExceeded: true,
            BackpressurePendingCount: 10,
            BackpressureThreshold: 9);

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
        deserialized.EventCount.ShouldBe(original.EventCount);
        deserialized.ResultPayload.ShouldBe(original.ResultPayload);
        deserialized.BackpressureExceeded.ShouldBe(original.BackpressureExceeded);
        deserialized.BackpressurePendingCount.ShouldBe(original.BackpressurePendingCount);
        deserialized.BackpressureThreshold.ShouldBe(original.BackpressureThreshold);
    }
}
