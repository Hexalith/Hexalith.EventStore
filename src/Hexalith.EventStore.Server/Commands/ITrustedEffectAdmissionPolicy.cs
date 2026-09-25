using Hexalith.EventStore.Contracts.Effects;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Verifies signed purpose, attested origin, command authority, target, and canonical semantics.</summary>
public interface ITrustedEffectAdmissionPolicy
{
    /// <summary>Validates all bindings before target state can be inspected.</summary>
    Task<TrustedEffectAdmission> AdmitAsync(
        TrustedEffectSubmission submission,
        TrustedEffectContext context,
        CancellationToken cancellationToken = default);
}
