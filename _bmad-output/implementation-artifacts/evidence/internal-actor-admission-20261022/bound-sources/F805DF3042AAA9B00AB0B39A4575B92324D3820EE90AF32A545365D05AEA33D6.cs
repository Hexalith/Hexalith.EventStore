namespace Hexalith.EventStore.Server.Identity;

/// <summary>Private alias capability independent of the stable actor's global status.</summary>
/// <param name="ActorId">The immutable stable actor associated with the alias.</param>
/// <param name="Active">Whether this login alias remains eligible.</param>
/// <param name="Revision">The immutable alias administration revision.</param>
public sealed record ActorRegistryAlias(string ActorId, bool Active, long Revision);
