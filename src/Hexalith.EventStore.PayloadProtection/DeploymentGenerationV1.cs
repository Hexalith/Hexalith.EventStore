// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 9-10, 13-15; lifecycle amendment sections 1-8.
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
