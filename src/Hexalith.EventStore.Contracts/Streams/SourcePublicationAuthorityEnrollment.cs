using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>Exact independently qualified installation. Receipt identifiers are references; the installed evidence verifier must authenticate their current meaning.</summary>
/// <param name="Scope">The exact namespace.</param><param name="Epoch">The independent current authority epoch.</param>
/// <param name="StorageIdentity">The independent journal storage identity.</param><param name="ValidUntil">The exclusive enrollment expiry.</param>
/// <param name="InitialSources">The complete finite legacy inventory.</param><param name="CommandSources">Exact sources requiring complete command evidence, including prospective empty streams.</param>
/// <param name="InventoryEvidenceId">Independently authenticated complete inventory evidence.</param><param name="WriterEnforcementEvidenceId">Independently authenticated all-writer enforcement evidence.</param>
/// <param name="ProducerWorkload">The enrolled producer.</param><param name="ObserverWorkload">The independent observer.</param>
/// <param name="RegistrarWorkload">The enrollment registrar.</param><param name="ReaderWorkload">The authority reader.</param><param name="RecoveryWorkload">The independent recovery reader.</param>
/// <param name="DeliveryTarget">The exact current receiver target.</param><param name="DeliveryAuthorityRevision">Current receiver permission revision.</param>
public sealed record SourcePublicationAuthorityEnrollment(SourcePublicationScope Scope, string Epoch, string StorageIdentity,
    DateTimeOffset ValidUntil, IReadOnlyList<AggregateIdentity> InitialSources, IReadOnlyList<AggregateIdentity> CommandSources,
    string InventoryEvidenceId, string WriterEnforcementEvidenceId, string ProducerWorkload, string ObserverWorkload,
    string RegistrarWorkload, string ReaderWorkload, string RecoveryWorkload, string DeliveryTarget, string DeliveryAuthorityRevision);
