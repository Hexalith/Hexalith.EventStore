// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base section 10.2; amendment sections 2-4.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>One qualified conditional state mutation; a null ETag means conditional creation, never overwrite.</summary>
/// <param name="Key">The exact validated state key.</param>
/// <param name="Bytes">The closed value bytes, or null for a conditional delete.</param>
/// <param name="ExpectedETag">The exact comparison ETag, or null for create-only.</param>
/// <param name="TtlSeconds">The qualified lease TTL, or null for persistent records.</param>
internal sealed record PayloadProtectionStateMutation(string Key, byte[]? Bytes, string? ExpectedETag, int? TtlSeconds = null)
{
    /// <summary>Creates a bounded validated upsert; caller must use the state record's exact key.</summary>
    internal static PayloadProtectionStateMutation Put<T>(T value, string? expectedETag = null) where T : class
    {
        int? ttl = value switch
        {
            CapabilityLeaseV1 => 60,
            CompletionLeaseV1 => 60,
            ReadLeaseV1 lease => checked((int)Math.Ceiling((PayloadProtectionStateCodec.ParseTimestamp(lease.ExpiresAtUtc)
                - PayloadProtectionStateCodec.ParseTimestamp(lease.IssuedAtUtc)).TotalSeconds)),
            _ => null,
        };
        return new(PayloadProtectionStateCodec.KeyFor(value), PayloadProtectionStateCodec.Encode(value), expectedETag, ttl);
    }

    /// <summary>Creates an unchanged-value CAS using exact strong-read bytes.</summary>
    internal static PayloadProtectionStateMutation Compare<T>(PayloadProtectionStateValue<T> current) where T : class
    {
        // Leases must be renewed with fresh issued/expires timestamps through Put, never made persistent by CAS.
        if (current.Value is CapabilityLeaseV1 or ReadLeaseV1 or CompletionLeaseV1) { throw new PayloadProtectionFormatException(); }
        return new(PayloadProtectionStateCodec.KeyFor(current.Value), current.Bytes.ToArray(), current.ETag);
    }

    /// <summary>Returns a bounded type name without state keys, bytes or ETags.</summary>
    public override string ToString() => nameof(PayloadProtectionStateMutation);
}
