namespace Hexalith.EventStore.Server.Projections;

/// <summary>Complete admitted tail identity plus actual source and readable byte accounting.</summary>
internal sealed record ProjectionSourceAdmittedHistory(ProjectionDeliveryFingerprintHistory History,
    IReadOnlyList<ProjectionSourceAdmittedEvent> Events, long SourceBytes, long WireBytes, int Pages);
