using System.Security.Cryptography;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Retains private canonical bytes after complete deterministic catalog validation.</summary>
/// <param name="canonicalBytes">The codec-owned canonical encoding, never exposed to callers.</param>
internal sealed class ValidatedRouteCatalogEncoding(byte[] canonicalBytes)
{
    /// <summary>Gets the root bound to these exact validated bytes.</summary>
    internal string RootDigest { get; } = Convert.ToHexStringLower(SHA256.HashData(canonicalBytes));

    /// <summary>Requires equality of the entire freshly captured document, without trusting a supplied digest.</summary>
    internal bool Matches(ReadOnlySpan<byte> bytes) => bytes.SequenceEqual(canonicalBytes);
}
