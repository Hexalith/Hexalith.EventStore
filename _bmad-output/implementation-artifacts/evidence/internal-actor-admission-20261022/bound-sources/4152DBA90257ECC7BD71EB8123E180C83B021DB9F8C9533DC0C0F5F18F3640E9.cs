// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 12, 13, and Appendix B.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Reads one historical protected format that the shared engine never reinterprets (normative section 12.2).
/// </summary>
/// <remarks>
/// The compatibility router calls a reader only after the carrier, format, and bounded marker shape agree.
/// A reader authenticates before it returns plaintext, returns either complete plaintext JSON or one bounded
/// unreadable reason, never returns the caller's input buffer, and lets caller cancellation propagate. Any other
/// exception is mapped to <see cref="UnreadableProtectedDataReason.ProviderUnavailable"/> by the router.
/// </remarks>
internal interface ILegacyPayloadReader
{
    /// <summary>Gets the exact, case-sensitive reader identifier, for example <c>parties-pdenc-v1</c>.</summary>
    string ReaderId { get; }

    /// <summary>
    /// Reads one stored event routed to this reader.
    /// </summary>
    /// <param name="record">The stored event. Its bytes are caller-owned and must not be mutated.</param>
    /// <param name="metadata">The classified stored metadata: the exact reader metadata or the legacy record.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>Complete authenticated plaintext JSON in a new buffer, or one bounded unreadable reason.</returns>
    ValueTask<CoreUnprotectionResult> ReadEventAsync(
        CompatibilityEventRecord record,
        EventStorePayloadProtectionMetadata metadata,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads one stored snapshot routed to this reader.
    /// </summary>
    /// <param name="record">The stored snapshot. Its state is caller-owned and must not be mutated.</param>
    /// <param name="metadata">The classified exact reader metadata.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The complete authenticated snapshot state as plaintext JSON, or one bounded unreadable reason.</returns>
    ValueTask<CoreUnprotectionResult> ReadSnapshotAsync(
        CompatibilitySnapshotRecord record,
        EventStorePayloadProtectionMetadata metadata,
        CancellationToken cancellationToken);
}
