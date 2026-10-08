namespace Hexalith.EventStore.Client.Events;

/// <summary>Contains unverified event-only logical prefix fields; checkpoint and snapshot anchors are unavailable.</summary>
/// <param name="TenantId">The carried tenant id.</param>
/// <param name="Domain">The carried domain.</param>
/// <param name="AggregateId">The carried aggregate id.</param>
/// <param name="AggregateType">The carried aggregate type.</param>
/// <param name="StartSequence">The carried start sequence.</param>
/// <param name="EndSequence">The carried end sequence.</param>
/// <param name="ActorHead">The carried actor head.</param>
/// <param name="TargetSequence">The carried target sequence.</param>
/// <param name="Count">The carried count.</param>
/// <param name="OrderedLogicalDigestListHash">The carried ordered logical digest list hash.</param>
/// <param name="Accumulator">The carried accumulator.</param>
/// <param name="RegistryFingerprint">The carried registry fingerprint.</param>
/// <param name="SourceBindingHash">The carried source binding hash.</param>
/// <param name="LogicalEvidenceModelId">The carried logical evidence model id.</param>
internal sealed record DaprLogicalPrefixClaim(
    string TenantId, string Domain, string AggregateId, string AggregateType,
    long StartSequence, long EndSequence, long ActorHead, long TargetSequence, int Count,
    ReadOnlyMemory<byte> OrderedLogicalDigestListHash, ReadOnlyMemory<byte> Accumulator,
    ReadOnlyMemory<byte> RegistryFingerprint, ReadOnlyMemory<byte> SourceBindingHash,
    string LogicalEvidenceModelId = DaprLogicalSourceBinding.ModelId);
