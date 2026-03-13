
using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Server.Queries;

/// <summary>
/// Pure-function helper for deriving query actor IDs using the 3-tier routing model.
/// </summary>
/// <remarks>
/// <para>Tier 1 (EntityId-scoped): <c>{QueryType}:{TenantId}:{EntityId}</c></para>
/// <para>Tier 2 (Payload-checksum): <c>{QueryType}:{TenantId}:{Checksum}</c></para>
/// <para>Tier 3 (Tenant-wide): <c>{QueryType}:{TenantId}</c></para>
/// <para>
/// Colon separator is used because this helper enforces colon-free routing segments,
/// guaranteeing structural disjointness for derived actor IDs.
/// </para>
/// </remarks>
public static class QueryActorIdHelper {
    /// <summary>
    /// Derives the query actor ID based on the 3-tier routing model.
    /// </summary>
    /// <param name="queryType">The query type name (must not contain colons).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="entityId">Optional entity identifier for Tier 1 routing.</param>
    /// <param name="payload">Serialized query payload. Use empty array for Tier 3.</param>
    /// <returns>The colon-separated actor ID appropriate for the query's tier.</returns>
    public static string DeriveActorId(string queryType, string tenantId, string? entityId, byte[] payload) {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryType);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(payload);

        ValidateSegmentDoesNotContainColon(queryType, nameof(queryType));
        ValidateSegmentDoesNotContainColon(tenantId, nameof(tenantId));

        if (!string.IsNullOrEmpty(entityId)) {
            ValidateSegmentDoesNotContainColon(entityId, nameof(entityId));
            return $"{queryType}:{tenantId}:{entityId}";
        }

        if (payload.Length > 0) {
            return $"{queryType}:{tenantId}:{ComputeChecksum(payload)}";
        }

        return $"{queryType}:{tenantId}";
    }

    /// <summary>
    /// Type-safe overload using query contract metadata for compile-time QueryType safety.
    /// Delegates to <see cref="DeriveActorId(string, string, string?, byte[])"/> using
    /// the query type from the contract's static abstract member.
    /// </summary>
    /// <typeparam name="TQuery">The query type implementing <see cref="IQueryContract"/>.</typeparam>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="entityId">Optional entity identifier for Tier 1 routing.</param>
    /// <param name="payload">Serialized query payload. Use empty array for Tier 3.</param>
    /// <returns>The colon-separated actor ID appropriate for the query's tier.</returns>
    /// <remarks>
    /// This method reads TQuery.QueryType directly without format validation.
    /// Call <c>QueryContractResolver.Resolve&lt;TQuery&gt;()</c> at least once
    /// (e.g., during application startup) to validate contract metadata before
    /// using this method in hot paths.
    /// </remarks>
    public static string DeriveActorId<TQuery>(string tenantId, string? entityId, byte[] payload)
        where TQuery : IQueryContract {
        return DeriveActorId(TQuery.QueryType, tenantId, entityId, payload);
    }

    /// <summary>
    /// Computes an 11-character base64url-encoded truncated SHA256 checksum of the payload.
    /// </summary>
    /// <param name="payload">The payload bytes to hash.</param>
    /// <returns>An 11-character URL-safe checksum string.</returns>
    public static string ComputeChecksum(byte[] payload) {
        byte[] hash = SHA256.HashData(payload);
        return Convert.ToBase64String(hash)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=')[..11];
    }

    private static void ValidateSegmentDoesNotContainColon(string value, string paramName) {
        if (value.Contains(':')) {
            throw new ArgumentException("Routing segments cannot contain colons.", paramName);
        }
    }
}
