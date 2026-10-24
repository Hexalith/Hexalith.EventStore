// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base section 10.2.
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
