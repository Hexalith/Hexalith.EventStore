namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>The durable independent enrollment and append-only original journal. Primary namespace/index storage is never its authority.</summary>
/// <param name="Enrollment">The exact independently qualified installation.</param><param name="Records">Retained original transitions, source observations and acknowledgement proofs.</param>
public sealed record SourcePublicationAuthorityState(SourcePublicationAuthorityEnrollment Enrollment, IReadOnlyList<SourcePublicationAuthorityRecord> Records);
