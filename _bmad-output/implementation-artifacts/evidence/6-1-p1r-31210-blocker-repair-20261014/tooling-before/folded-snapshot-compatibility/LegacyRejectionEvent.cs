using Hexalith.EventStore.Contracts.Events;

namespace FoldedSnapshotCompatibility;

/// <summary>Rejection event compiled against the published old serialized-payload contract.</summary>
public sealed class LegacyRejectionEvent : ISerializedEventPayload, IRejectionEvent
{
    /// <inheritdoc/>
    public string EventTypeName => "legacy-rejection-event";

    /// <inheritdoc/>
    public byte[] PayloadBytes { get; } = " { \"amount\":3, \"kind\":\"rejection\" } "u8.ToArray();

    /// <inheritdoc/>
    public string SerializationFormat => "json";
}
