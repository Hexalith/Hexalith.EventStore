using System.Text.Json;

using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Measures actual compact envelope JSON without retaining an extra serialized payload copy.</summary>
internal static class SourceEnvelopeResponseSize
{
    /// <summary>Counts all serialized bytes, including an oversized envelope already read and decoded by the actor.</summary>
    internal static long Count(EventEnvelope envelope, JsonSerializerOptions options)
    {
        if (options.WriteIndented) throw new InvalidOperationException("source_proof_unsupported");
        using var count = new SizeLimitedWriteStream(long.MaxValue);
        try { JsonSerializer.Serialize(count, envelope, options); }
        finally { ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceSizingBytes, count.Length); }

        return count.Length;
    }
}
