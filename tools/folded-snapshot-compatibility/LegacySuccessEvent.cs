using Hexalith.EventStore.Contracts.Events;

namespace FoldedSnapshotCompatibility;

/// <summary>Success event compiled against the published old serialized-payload contract.</summary>
public sealed class LegacySuccessEvent : ISerializedEventPayload
{
    /// <inheritdoc/>
    public string EventTypeName => "legacy-success-event";

    /// <inheritdoc/>
    public byte[] PayloadBytes { get; } = " { \"amount\":7, \"kind\":\"success\" } "u8.ToArray();

    /// <inheritdoc/>
    public string SerializationFormat => "json";
}
