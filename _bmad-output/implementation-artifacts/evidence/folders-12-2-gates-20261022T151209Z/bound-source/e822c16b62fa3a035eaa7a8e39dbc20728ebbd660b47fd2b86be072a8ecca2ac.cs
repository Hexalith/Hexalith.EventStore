using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Counts state serialization so unknown envelope versions must bypass it.</summary>
[JsonConverter(typeof(SnapshotVersionProbeStateConverter))]
public sealed class SnapshotVersionProbeState
{
    /// <summary>Gets or sets the number of attempted state conversions.</summary>
    public int SerializationCount { get; set; }
}
