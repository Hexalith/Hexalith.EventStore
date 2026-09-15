// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 8, 14, and 15.
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
