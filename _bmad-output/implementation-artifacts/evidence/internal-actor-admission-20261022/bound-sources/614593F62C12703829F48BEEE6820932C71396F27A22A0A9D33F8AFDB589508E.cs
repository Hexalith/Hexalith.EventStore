// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 6-8, 14, and 15.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Represents a bounded local pdenc-v2 validation failure without retaining attacker-controlled text.
/// </summary>
internal sealed class PayloadProtectionFormatException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayloadProtectionFormatException"/> class.
    /// </summary>
    internal PayloadProtectionFormatException()
        : base("The protected payload is malformed or exceeds a supported limit.")
    {
    }
}
