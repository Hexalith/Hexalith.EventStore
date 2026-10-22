namespace Hexalith.EventStore.Server.Identity;

/// <summary>Immutable durable retry result for one registry intent.</summary>
/// <param name="IntentDigest">The exact original mutation digest.</param>
/// <param name="Result">The original registry result.</param>
public sealed record ActorRegistryOperation(string IntentDigest, ActorRegistryEntry Result);
