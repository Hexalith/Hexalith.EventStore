namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Represents a bounded platform or provider cryptographic failure without retaining implementation details.
/// </summary>
internal sealed class PayloadProtectionCryptographicException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayloadProtectionCryptographicException"/> class.
    /// </summary>
    internal PayloadProtectionCryptographicException()
        : base("The payload-protection cryptographic operation failed.")
    {
    }
}
