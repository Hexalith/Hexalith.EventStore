namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Records only a private owned sensitive file's observed identity, never its contents.</summary>
/// <param name="Owner">The actual runtime owner.</param>
/// <param name="Path">The exact owner-private file path.</param>
/// <param name="Purpose">The finite copy, restore or destruction boundary.</param>
/// <param name="Exists">Whether the exact file was present.</param>
/// <param name="Bytes">The independently read file length, zero after destruction.</param>
/// <param name="Sha256">The actually read byte digest, null after destruction.</param>
/// <param name="Started">The real read start.</param>
/// <param name="Ended">The real read completion.</param>
internal sealed record QualificationFileFingerprint(string Owner, string Path, string Purpose, bool Exists,
    long Bytes, string? Sha256, DateTimeOffset Started, DateTimeOffset Ended);
