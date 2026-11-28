using System.Security.Cryptography;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Retains private canonical bytes after complete deterministic catalog validation.</summary>
/// <param name="canonicalBytes">The codec-owned canonical encoding, never exposed to callers.</param>
/// <param name="profile">The immutable profile string decoded from <paramref name="canonicalBytes"/>.</param>
internal sealed class ValidatedRouteCatalogEncoding(byte[] canonicalBytes, string profile)
{
    /// <summary>Gets the root bound to these exact validated bytes.</summary>
    internal string RootDigest { get; } = Convert.ToHexStringLower(SHA256.HashData(canonicalBytes));

    /// <summary>Gets the immutable profile string bound to these exact validated bytes.</summary>
    internal string Profile { get; } = profile;

    /// <summary>Requires equality of the entire freshly captured document, without trusting a supplied digest.</summary>
    internal bool Matches(ReadOnlySpan<byte> bytes) => bytes.SequenceEqual(canonicalBytes);
}
