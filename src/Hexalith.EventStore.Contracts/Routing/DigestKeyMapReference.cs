namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Value-free mapping of a digest-key generation to a logical secret map key.</summary>
/// <param name="Generation">Explicit reader generation.</param>
/// <param name="MapKey">Logical key name; never the key material.</param>
public sealed record DigestKeyMapReference(string Generation, string MapKey);
