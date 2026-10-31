using System.Text;

namespace Hexalith.EventStore.Server.Tests.GlobalPositions;

/// <summary>
/// Exact UTF-8 shard identity for the isolated development candidate.
/// </summary>
internal readonly record struct QualificationShard
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Initializes a shard without normalizing either identity member.</summary>
    /// <param name="tenantId">The exact tenant identifier.</param>
    /// <param name="domain">The exact domain identifier.</param>
    internal QualificationShard(string tenantId, string domain)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ArgumentException.ThrowIfNullOrEmpty(domain);
        _ = StrictUtf8.GetByteCount(tenantId);
        _ = StrictUtf8.GetByteCount(domain);
        TenantId = tenantId;
        Domain = domain;
    }

    /// <summary>Gets the unmodified tenant identifier.</summary>
    internal string TenantId { get; }

    /// <summary>Gets the unmodified domain identifier.</summary>
    internal string Domain { get; }

    /// <summary>Gets whether this is a constructed identity rather than a default struct.</summary>
    internal bool IsValid => !string.IsNullOrEmpty(TenantId) && !string.IsNullOrEmpty(Domain);

    /// <summary>Creates an injective fixture key; provider size limits are not qualified.</summary>
    /// <returns>The versioned key for these exact identity bytes.</returns>
    internal string StateKey()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException("A valid shard identity is required.");
        }

        return "gpos-v2-candidate:1:"
            + Convert.ToBase64String(StrictUtf8.GetBytes(TenantId))
            + ":"
            + Convert.ToBase64String(StrictUtf8.GetBytes(Domain));
    }
}
