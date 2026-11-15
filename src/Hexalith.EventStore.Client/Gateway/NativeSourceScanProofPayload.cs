namespace Hexalith.EventStore.Client.Gateway;

/// <summary>The signed operation, exact page digest and finite proof lifetime.</summary>
/// <param name="Version">The signed proof format.</param>
/// <param name="Purpose">The exact native scan operation.</param>
/// <param name="Caller">The actual enrolled workload.</param>
/// <param name="RequestDigest">The complete exact page request digest.</param>
/// <param name="IssuedAt">The UTC issue second.</param>
/// <param name="ExpiresAt">The exclusive expiry second.</param>
/// <param name="Nonce">The fresh proof identity.</param>
internal sealed record NativeSourceScanProofPayload(int Version, string Purpose, string Caller, string RequestDigest,
    long IssuedAt, long ExpiresAt, string Nonce);
