// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 9-10, 13-15; lifecycle amendment sections 1-8.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed CapabilityLeaseV1 durable schema; encodings are checked by the state codec.</summary>
/// <param name="TenantDomainDigest">The canonical tenantDomainDigest field.</param>
/// <param name="Generation">The canonical generation field.</param>
/// <param name="InstanceId">The canonical instanceId field.</param>
/// <param name="SourceSha">The canonical sourceSha field.</param>
/// <param name="PackageSetSha256">The canonical packageSetSha256 field.</param>
/// <param name="SpecDigest">The canonical specDigest field.</param>
/// <param name="BackendId">The canonical backendId field.</param>
/// <param name="ReadFormats">The canonical readFormats field.</param>
/// <param name="WriteModes">The canonical writeModes field.</param>
/// <param name="IssuedAtUtc">The canonical issuedAtUtc field.</param>
/// <param name="ExpiresAtUtc">The canonical expiresAtUtc field.</param>
internal sealed record CapabilityLeaseV1(
    string TenantDomainDigest,
    ulong Generation,
    string InstanceId,
    string SourceSha,
    string PackageSetSha256,
    string SpecDigest,
    string BackendId,
    string[] ReadFormats,
    string[] WriteModes,
    string IssuedAtUtc,
    string ExpiresAtUtc)
{
    /// <summary>Gets the closed durable schema version.</summary>
    [JsonPropertyOrder(-1)]
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Returns only a bounded schema name.</summary>
    public override string ToString() => nameof(CapabilityLeaseV1);
}
