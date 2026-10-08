namespace Hexalith.EventStore.Client.Events;

/// <summary>Contains unverified logical route fields; only current model-scoped signature/source checks authorize use.</summary>
/// <param name="ApplicationLogicalDigest">The carried application logical digest.</param>
/// <param name="TenantId">The carried tenant id.</param>
/// <param name="Domain">The carried domain.</param>
/// <param name="AggregateId">The carried aggregate id.</param>
/// <param name="AggregateType">The carried aggregate type.</param>
/// <param name="SequenceNumber">The carried sequence number.</param>
/// <param name="MessageId">The carried message id.</param>
/// <param name="StoredEventType">The carried stored event type.</param>
/// <param name="StoredMetadataVersion">The carried stored metadata version.</param>
/// <param name="StoredCanonicalType">The carried stored canonical type.</param>
/// <param name="StoredPayloadVersion">The carried stored payload version.</param>
/// <param name="StoredFormat">The carried stored format.</param>
/// <param name="TargetCanonicalType">The carried target canonical type.</param>
/// <param name="TargetPayloadVersion">The carried target payload version.</param>
/// <param name="RegistryFingerprint">The carried registry fingerprint.</param>
/// <param name="EffectivePayloadHash">The carried effective payload hash.</param>
/// <param name="EffectiveFormat">The carried effective format.</param>
/// <param name="ConsumedMetadataHash">The carried consumed metadata hash.</param>
/// <param name="SourceBindingHash">The carried source binding hash.</param>
/// <param name="LogicalEvidenceModelId">The carried logical evidence model id.</param>
internal sealed record DaprLogicalRouteClaim(
    ReadOnlyMemory<byte> ApplicationLogicalDigest, string TenantId, string Domain, string AggregateId,
    string AggregateType, long SequenceNumber, string MessageId, string StoredEventType,
    int StoredMetadataVersion, string? StoredCanonicalType, int? StoredPayloadVersion, string StoredFormat,
    string TargetCanonicalType, int TargetPayloadVersion, ReadOnlyMemory<byte> RegistryFingerprint,
    ReadOnlyMemory<byte> EffectivePayloadHash, string EffectiveFormat, ReadOnlyMemory<byte> ConsumedMetadataHash,
    ReadOnlyMemory<byte> SourceBindingHash, string LogicalEvidenceModelId = DaprLogicalSourceBinding.ModelId);
