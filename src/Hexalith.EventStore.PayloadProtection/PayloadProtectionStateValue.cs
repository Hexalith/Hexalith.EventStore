// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base section 10.2.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Holds a validated strong read and its out-of-band ETag plus exact bytes for unchanged-value CAS.</summary>
/// <param name="Value">The closed decoded durable value.</param>
/// <param name="ETag">The component response ETag, never serialized inside the value.</param>
/// <param name="Bytes">The exact strongly observed value bytes.</param>
internal sealed record PayloadProtectionStateValue<T>(T Value, string ETag, ReadOnlyMemory<byte> Bytes) where T : class
{
    /// <summary>Returns a bounded type name without state data.</summary>
    public override string ToString() => nameof(PayloadProtectionStateValue<T>);
}
