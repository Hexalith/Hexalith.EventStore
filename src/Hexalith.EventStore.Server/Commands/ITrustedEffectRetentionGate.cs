using Hexalith.EventStore.Contracts.Effects;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Checks source-floor, legal-hold, offboarding, and restore admission before receipt disclosure.</summary>
public interface ITrustedEffectRetentionGate
{
    /// <summary>Rejects when source and target evidence cannot be retained under one decision.</summary>
    Task ValidateAsync(EffectIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>Releases an admitted effect after its receipt or collision evidence is durable.</summary>
    Task CompleteAsync(EffectIdentity identity, CancellationToken cancellationToken = default);
}
