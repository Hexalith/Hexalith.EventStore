using System.Text.Json;

using Hexalith.EventStore.Server.Events;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Published shape and persisted-state compatibility of aggregate metadata.</summary>
public sealed class AggregateMetadataTests
{
    /// <summary>The published three-parameter constructor and deconstruction remain available.</summary>
    [Fact]
    public void ThreeParameterShapeDefaultsRetainedFloorToOne()
    {
        var modified = new DateTimeOffset(2026, 9, 29, 8, 0, 0, TimeSpan.Zero);
        var metadata = new AggregateMetadata(5, modified, "etag-1");

        (long sequence, DateTimeOffset lastModified, string? etag) = metadata;

        metadata.RetainedFloor.ShouldBe(1);
        sequence.ShouldBe(5);
        lastModified.ShouldBe(modified);
        etag.ShouldBe("etag-1");
    }

    /// <summary>Actor-state JSON preserves a retained floor and defaults a legacy record to one.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonRoundTripPreservesRetainedFloor(bool webDefaults)
    {
        JsonSerializerOptions options = webDefaults ? new(JsonSerializerDefaults.Web) : new();
        var metadata = new AggregateMetadata(9, DateTimeOffset.UnixEpoch, null, 8);

        AggregateMetadata copy = JsonSerializer.Deserialize<AggregateMetadata>(
            JsonSerializer.Serialize(metadata, options), options)!;
        AggregateMetadata legacy = JsonSerializer.Deserialize<AggregateMetadata>(
            webDefaults
                ? "{\"currentSequence\":3,\"lastModified\":\"1970-01-01T00:00:00+00:00\",\"eTag\":null}"
                : "{\"CurrentSequence\":3,\"LastModified\":\"1970-01-01T00:00:00+00:00\",\"ETag\":null}",
            options)!;

        copy.ShouldBe(metadata);
        legacy.RetainedFloor.ShouldBe(1);
        legacy.CurrentSequence.ShouldBe(3);
    }
}
