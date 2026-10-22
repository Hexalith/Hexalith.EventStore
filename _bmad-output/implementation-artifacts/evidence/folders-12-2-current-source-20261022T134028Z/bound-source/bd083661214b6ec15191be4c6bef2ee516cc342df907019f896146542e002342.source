using Hexalith.EventStore.Contracts.Effects;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Verifies signed purpose, attested origin, command authority, target, and canonical semantics.</summary>
public interface ITrustedEffectAdmissionPolicy
{
    /// <summary>Verifies provenance and canonical semantics without changing retention state.</summary>
    Task<TrustedEffectAdmission> PrepareAsync(
        TrustedEffectSubmission submission,
        TrustedEffectContext context,
        CancellationToken cancellationToken = default);

    /// <summary>Registers retention evidence after the gateway proof has been validated.</summary>
    Task CompleteAsync(
        TrustedEffectAdmission admission,
        CancellationToken cancellationToken = default);

    /// <summary>Validates all bindings before target state can be inspected.</summary>
    Task<TrustedEffectAdmission> AdmitAsync(
        TrustedEffectSubmission submission,
        TrustedEffectContext context,
        CancellationToken cancellationToken = default);
}
