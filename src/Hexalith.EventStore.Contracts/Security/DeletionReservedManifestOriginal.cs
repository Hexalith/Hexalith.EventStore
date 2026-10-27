using System.Runtime.Serialization;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>The retained reservation request and its current receipt-bearing owner outcome; reading it grants no physical authority.</summary>
/// <param name="Original">Complete unchanged request retained when the reservation was made, including any earlier legitimate re-attestation.</param>
/// <param name="Outcome">Actual retained reserved or consumed outcome, without reconstructing an earlier revision.</param>
[DataContract]
[KnownType(typeof(System.Collections.ObjectModel.ReadOnlyCollection<ProtectionTarget>))]
[KnownType(typeof(System.Collections.ObjectModel.ReadOnlyCollection<DeletionTargetReceipt>))]
public sealed record DeletionReservedManifestOriginal([property: DataMember] DeletionBatchConsumptionRequest Original, [property: DataMember] DeletionConsumptionOutcome Outcome);
