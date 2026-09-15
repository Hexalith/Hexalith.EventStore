// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 7, 8, 14, and 15.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Contains the sorted paths, encoded manifest, and SHA-256 commitment defined by normative section 7.2.
/// </summary>
/// <param name="Paths">The canonical paths in unsigned UTF-8 lexical order.</param>
/// <param name="Encoded">The complete HXPM version 1 encoding.</param>
/// <param name="Commitment">The SHA-256 digest of <paramref name="Encoded"/>.</param>
internal sealed record ProtectedPathManifest(
    IReadOnlyList<string> Paths,
    byte[] Encoded,
    byte[] Commitment)
{
    /// <inheritdoc/>
    public override string ToString() => nameof(ProtectedPathManifest);
}
