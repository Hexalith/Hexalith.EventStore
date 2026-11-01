using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>
/// Computes the stable fingerprint of an event payload's exact persisted bytes.
/// </summary>
/// <remarks>
/// Delivered envelopes and authoritative stream reads use the same function, so a consumer can prove that a
/// delivery at one aggregate sequence carries exactly the bytes that EventStore persisted at that sequence.
/// </remarks>
public static class EventStorePayloadFingerprint {
    /// <summary>
    /// Computes the lowercase hexadecimal SHA-256 digest of <paramref name="payload"/>.
    /// </summary>
    /// <param name="payload">The exact payload bytes.</param>
    /// <returns>A 64-character lowercase hexadecimal digest.</returns>
    public static string Compute(ReadOnlySpan<byte> payload)
        => Convert.ToHexStringLower(SHA256.HashData(payload));
}
