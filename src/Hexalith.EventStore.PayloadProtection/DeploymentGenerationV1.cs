// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 13-15; lifecycle amendment sections 1-7.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed DeploymentGenerationV1 durable schema; encodings are checked by the state codec.</summary>
/// <param name="TenantId">The canonical tenantId field.</param>
/// <param name="Domain">The canonical domain field.</param>
/// <param name="TenantDomainDigest">The canonical tenantDomainDigest field.</param>
/// <param name="Generation">The canonical generation field.</param>
/// <param name="ApprovedSpecDigest">The canonical approvedSpecDigest field.</param>
/// <param name="ApprovedSourceSha">The canonical approvedSourceSha field.</param>
/// <param name="PackageSetSha256">The canonical packageSetSha256 field.</param>
/// <param name="Status">The canonical status field.</param>
/// <param name="ApprovedAtUtc">The canonical approvedAtUtc field.</param>
internal sealed record DeploymentGenerationV1(
    string TenantId,
    string Domain,
    string TenantDomainDigest,
    ulong Generation,
    string ApprovedSpecDigest,
    string ApprovedSourceSha,
    string PackageSetSha256,
    string Status,
    string ApprovedAtUtc)
{
    /// <summary>Gets the closed durable schema version.</summary>
    [JsonPropertyOrder(-1)]
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Returns only a bounded schema name.</summary>
    public override string ToString() => nameof(DeploymentGenerationV1);
}
