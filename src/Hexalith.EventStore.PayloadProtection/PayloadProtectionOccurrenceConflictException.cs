namespace Hexalith.EventStore.PayloadProtection;

/// <summary>A current durable occurrence already belongs to a different authenticated content intent. No plaintext, key or provider data is exposed.</summary>
public sealed class PayloadProtectionOccurrenceConflictException : InvalidOperationException
{
    /// <summary>Creates the fixed typed write refusal.</summary>
    public PayloadProtectionOccurrenceConflictException() : base("ProtectionOccurrenceConflict") { }
}
