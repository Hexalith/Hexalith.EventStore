using Hexalith.EventStore.Contracts.Effects;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Routes admitted effects directly to their target actor partition.</summary>
public interface ITrustedEffectRouter
{
    /// <summary>Submits an effect for authoritative receipt inspection and processing.</summary>
    Task<TrustedEffectResult> RouteAsync(
        TrustedEffectSubmission submission,
        TrustedEffectContext context,
        string gatewayProof,
        CancellationToken cancellationToken = default);
}
