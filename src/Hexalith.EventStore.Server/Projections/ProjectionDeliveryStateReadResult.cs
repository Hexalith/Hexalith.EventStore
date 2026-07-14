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
    bool WriterProtocolV2Active);
