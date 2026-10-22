namespace Hexalith.EventStore.Server.Commands;

/// <summary>Contains the active protected identity and all retained lookup aliases.</summary>
/// <param name="Active">The sole active-writer identity.</param>
/// <param name="Aliases">The active identity followed by retained-reader identities.</param>
public sealed record IdempotencyProtectedIdentitySet(
    IdempotencyProtectedIdentity Active,
    IReadOnlyList<IdempotencyProtectedIdentity> Aliases);
