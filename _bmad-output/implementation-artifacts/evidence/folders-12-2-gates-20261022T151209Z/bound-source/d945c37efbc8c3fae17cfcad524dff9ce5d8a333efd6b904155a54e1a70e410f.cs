// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 8, 14, and 15.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Represents a bounded pdenc-v2 authentication failure without exposing cryptographic details.
/// </summary>
internal sealed class PayloadProtectionAuthenticationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayloadProtectionAuthenticationException"/> class.
    /// </summary>
    internal PayloadProtectionAuthenticationException()
        : base("The protected payload could not be authenticated.")
    {
    }
}
