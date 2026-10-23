// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 13-15; lifecycle amendment sections 1-7.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed FenceV1 durable schema; encodings are checked by the state codec.</summary>
/// <param name="TenantId">The canonical tenantId field.</param>
/// <param name="Domain">The canonical domain field.</param>
/// <param name="TenantDomainDigest">The canonical tenantDomainDigest field.</param>
/// <param name="ApprovedWriteMode">The canonical approvedWriteMode field.</param>
/// <param name="ApprovalEpoch">The canonical approvalEpoch field.</param>
/// <param name="ApprovalId">The canonical approvalId field.</param>
/// <param name="ApprovedSpecDigest">The canonical approvedSpecDigest field.</param>
/// <param name="DeploymentGeneration">The canonical deploymentGeneration field.</param>
/// <param name="HighestWrittenFormat">The canonical highestWrittenFormat field.</param>
/// <param name="HighestWrittenEpoch">The canonical highestWrittenEpoch field.</param>
/// <param name="UpdatedAtUtc">The canonical updatedAtUtc field.</param>
internal sealed record FenceV1(
    string TenantId,
    string Domain,
    string TenantDomainDigest,
    string ApprovedWriteMode,
    ulong ApprovalEpoch,
    string ApprovalId,
    string ApprovedSpecDigest,
    ulong DeploymentGeneration,
    string HighestWrittenFormat,
    ulong HighestWrittenEpoch,
    string UpdatedAtUtc)
{
    /// <summary>Gets the closed durable schema version.</summary>
    [JsonPropertyOrder(-1)]
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Returns only a bounded schema name.</summary>
    public override string ToString() => nameof(FenceV1);
}
