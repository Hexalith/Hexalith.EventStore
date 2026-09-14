namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Represents a bounded pdenc-v2 authentication failure without exposing cryptographic details.
/// </summary>
internal sealed class PayloadProtectionAuthenticationException : Exception {
    /// <summary>
    /// Initializes a new instance of the <see cref="PayloadProtectionAuthenticationException"/> class.
    /// </summary>
    internal PayloadProtectionAuthenticationException()
        : base("The protected payload could not be authenticated.") {
    }
}
