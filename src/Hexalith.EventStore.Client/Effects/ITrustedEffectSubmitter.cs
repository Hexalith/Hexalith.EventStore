using Hexalith.EventStore.Contracts.Effects;

namespace Hexalith.EventStore.Client.Effects;

/// <summary>Submits an authenticated effect to the target receipt authority.</summary>
public interface ITrustedEffectSubmitter
{
    /// <summary>Submits or replays one effect under an authenticated delegation.</summary>
    Task<TrustedEffectResult> SubmitAsync(
        TrustedEffectSubmission submission,
        TrustedEffectContext context,
        CancellationToken cancellationToken = default);
}
