using Hexalith.EventStore.Contracts.Effects;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Provides the inclusive retained envelope floor for a source stream.</summary>
public interface ITrustedEffectSourceFloorProvider
{
    /// <summary>Returns null when a trustworthy retained floor is unavailable.</summary>
    Task<long?> GetRetainedFloorAsync(EffectIdentity identity, CancellationToken cancellationToken = default);
}
