using Hexalith.EventStore.Contracts.Effects;

namespace Hexalith.EventStore.Client.Processes;

/// <summary>Obtains a fresh bound delegation immediately before each trusted effect submission.</summary>
public interface IProcessDelegationProvider
{
    /// <summary>Creates authenticated context for exactly this submission and causation.</summary>
    Task<TrustedEffectContext> CreateAsync(TrustedEffectSubmission submission, string causationId, CancellationToken cancellationToken = default);
}
