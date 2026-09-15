// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 6-8, 14, and 15.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Represents complete authenticated plaintext or one bounded unreadable reason.
/// </summary>
/// <param name="PayloadBytes">The complete plaintext JSON payload, or <see langword="null"/>.</param>
/// <param name="UnreadableReason">The safe unreadable reason, or <see langword="null"/>.</param>
internal sealed record CoreUnprotectionResult(
    byte[]? PayloadBytes,
    UnreadableProtectedDataReason? UnreadableReason)
{
    /// <summary>Gets a value indicating whether complete authenticated plaintext is available.</summary>
    internal bool IsReadable => PayloadBytes is not null && UnreadableReason is null;

    /// <summary>Creates a readable result.</summary>
    internal static CoreUnprotectionResult Readable(byte[] payloadBytes) => new(payloadBytes, null);

    /// <summary>Creates an unreadable result with no plaintext.</summary>
    internal static CoreUnprotectionResult Unreadable(UnreadableProtectedDataReason reason) => new(null, reason);
}
