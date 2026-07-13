using System.Security.Cryptography;
using System.Text;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Durable payload-free retry metadata for one aggregate stream head.</summary>
/// <param name="WorkId">The deterministic work identity.</param>
/// <param name="TenantId">The aggregate tenant.</param>
/// <param name="Domain">The aggregate domain.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="AppId">The exact DAPR domain-service app id.</param>
/// <param name="ServiceVersion">The exact domain-service version.</param>
/// <param name="HeadSequence">The recorded stream head sequence.</param>
/// <param name="HeadMessageId">The recorded stream head message id.</param>
/// <param name="PendingRoutes">Routes that remain eligible for retry.</param>
/// <param name="TerminalRoutes">Known-terminal routes retained for operator evidence.</param>
/// <param name="DispatchId">The stable dispatch id reused on every retry.</param>
/// <param name="CatalogFingerprint">The exact admitted route-catalog fingerprint.</param>
/// <param name="Attempt">The bounded backoff attempt counter.</param>
/// <param name="NextDueUtc">The next eligible retry time.</param>
/// <param name="LastReasonCode">An optional support-safe terminal/retry reason.</param>
public sealed record ProjectionDeliveryRetryWorkItem(
    string WorkId,
    string TenantId,
    string Domain,
    string AggregateId,
    string AppId,
    string ServiceVersion,
    long HeadSequence,
    string HeadMessageId,
    IReadOnlyList<string> PendingRoutes,
    IReadOnlyList<string> TerminalRoutes,
    string DispatchId,
    string CatalogFingerprint,
    int Attempt,
    DateTimeOffset NextDueUtc,
    string? LastReasonCode) {
    /// <summary>Creates a deterministic opaque work id without persisting event payloads.</summary>
    /// <param name="tenantId">The aggregate tenant.</param>
    /// <param name="domain">The aggregate domain.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="headSequence">The recorded stream head.</param>
    /// <returns>A lowercase SHA-256 work id.</returns>
    public static string CreateWorkId(string tenantId, string domain, string aggregateId, long headSequence) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(headSequence);
        byte[] value = Encoding.UTF8.GetBytes($"{tenantId}\u001f{domain}\u001f{aggregateId}\u001f{headSequence}");
        return Convert.ToHexStringLower(SHA256.HashData(value));
    }
}
