namespace Probe.Host;

/// <summary>Records one exact managed fixture identity, content-addressed path and expected digest.</summary>
/// <param name="Name">The exact simple assembly name admitted by this fixture.</param>
/// <param name="Path">The content-addressed file consumed by the candidate.</param>
/// <param name="Sha256">The uppercase or lowercase hexadecimal SHA-256 expected before loading.</param>
internal sealed record PinnedAssembly(string Name, string Path, string Sha256);
