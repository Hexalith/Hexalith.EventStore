using System.Text;

namespace Hexalith.EventStore.Sharding.TestHost;

/// <summary>Encodes exact, strictly valid UTF-8 pairs without normalization or hashing.</summary>
public static class ShardIdentity
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Returns an injective actor identity for an exact pair.</summary>
    /// <param name="tenantId">The exact tenant.</param>
    /// <param name="domain">The exact domain.</param>
    /// <returns>The URL-safe physical identity.</returns>
    public static string Encode(string tenantId, string domain)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ArgumentException.ThrowIfNullOrEmpty(domain);
        return "shard-v1." + EncodeMember(tenantId) + "." + EncodeMember(domain);
    }

    private static string EncodeMember(string value)
        => Convert.ToBase64String(StrictUtf8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
