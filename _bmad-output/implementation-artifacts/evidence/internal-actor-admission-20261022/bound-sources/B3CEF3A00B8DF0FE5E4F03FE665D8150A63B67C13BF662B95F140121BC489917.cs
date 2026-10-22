namespace Hexalith.EventStore.Server.Projections;

/// <summary>Immutable payload-free tuple that application must match before each event's handler effects.</summary>
internal sealed record ProjectionSourceAdmittedEvent(long Sequence, string MessageId, string SourceEventDigest, string RouteEventFingerprint);
