using System.Text.Json;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Reusable entry and encoded-byte budgets for a complete projection snapshot.</summary>
/// <param name="MaxEntries">The maximum combined number of snapshot entries.</param>
/// <param name="MaxSerializedBytes">The maximum UTF-8 JSON payload size.</param>
public sealed record ProjectionSnapshotLimits(int MaxEntries = 4096, int MaxSerializedBytes = 262144)
{
    /// <summary>Gets the supported default complete-snapshot budget.</summary>
    public static ProjectionSnapshotLimits Default { get; } = new();

    /// <summary>Validates the entry count and serializes JSON through a bounded buffer before emission.</summary>
    /// <typeparam name="T">The snapshot payload type.</typeparam>
    /// <param name="snapshot">The complete event payload.</param>
    /// <param name="entryCount">The combined number of entries in its collections.</param>
    /// <param name="options">The producer's serialization options; absent means the default event writer.</param>
    /// <exception cref="ProjectionSnapshotLimitExceededException">An entry or byte budget was exceeded.</exception>
    public void Validate<T>(T snapshot, long entryCount, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxEntries, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxSerializedBytes, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(entryCount);
        if (entryCount > MaxEntries)
        {
            throw new ProjectionSnapshotLimitExceededException("entries", entryCount, MaxEntries);
        }

        // A fixed upper bound also caps a serializer's request for one very large escaped
        // string token, before it can allocate an unbounded intermediate JSON buffer.
        ProjectionSnapshotBufferWriter sink = new(MaxSerializedBytes);
        using Utf8JsonWriter writer = new(sink);
        JsonSerializer.Serialize(writer, snapshot, options);
        writer.Flush();
    }
}
