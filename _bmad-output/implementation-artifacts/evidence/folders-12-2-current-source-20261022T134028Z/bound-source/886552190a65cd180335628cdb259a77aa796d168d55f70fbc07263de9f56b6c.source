namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>Independently authenticated retained consecutive acknowledgement prefix. Exact installation, complete cut and current delivery binding must be authenticated again before reuse; strings alone grant no resume authority.</summary>
/// <param name="Scope">Exact installed source namespace.</param><param name="Version">Conditional original progress generation.</param>
/// <param name="AcknowledgedPrefix">Last consecutive independently proved original acknowledgement.</param><param name="SourceAuthorityRevision">Original complete-cut authority.</param>
/// <param name="SourcesDigest">Exact canonical complete source vector digest.</param><param name="LastOffset">Complete reconciled original inventory cut.</param>
/// <param name="PrefixDigest">Exact immutable reference prefix digest.</param><param name="DeliveryTarget">Independently installed receiver binding.</param>
/// <param name="DeliveryAuthorityRevision">Current private delivery authority.</param><param name="ReceiptId">Independent immutable original acknowledgement-prefix proof.</param>
public sealed record SourcePublicationDispatchProgress(SourcePublicationScope Scope, long Version, long AcknowledgedPrefix, string SourceAuthorityRevision,
    string SourcesDigest, long LastOffset, string PrefixDigest, string DeliveryTarget, string DeliveryAuthorityRevision, string ReceiptId);
