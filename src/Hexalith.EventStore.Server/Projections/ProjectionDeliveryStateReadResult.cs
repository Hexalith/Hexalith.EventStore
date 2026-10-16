namespace Hexalith.EventStore.Server.Projections;

/// <summary>Returns one persisted delivery row with its ETag and version classification.</summary>
/// <param name="State">The deserialized row, when present.</param>
/// <param name="Etag">The authoritative state-store ETag.</param>
/// <param name="Classification">The row classification.</param>
/// <param name="WriterProtocolV2Active">Whether the store-global v2 cutover marker is current.</param>
internal sealed record ProjectionDeliveryStateReadResult(
    ProjectionDeliveryState? State,
    string Etag,
    ProjectionDeliveryStateClassification Classification,
    bool WriterProtocolV2Active)
{
    /// <summary>The separately read immutable scope admission, absent when this scope is unqualified.</summary>
    public ProjectionSourceGuard? SourceGuard { get; init; }

    /// <summary>The first-write/CAS token from the separate scope record read.</summary>
    public string SourceGuardEtag { get; init; } = string.Empty;

    /// <summary>The actual serialized proof-row bytes observed at the owning Dapr seam.</summary>
    public long StateBytes { get; init; }

    /// <summary>The actual bytes of both separately read writer and scope markers.</summary>
    public long MarkerBytes { get; init; }
}
