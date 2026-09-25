namespace Hexalith.EventStore.Server.Commands;

/// <summary>Signs and verifies the gateway's attested caller decision for an actor request.</summary>
public interface ITrustedEffectGatewayProof
{
    /// <summary>Signs an admitted effect after the gateway attests the calling workload.</summary>
    Task<string> SignAsync(TrustedEffectAdmission admission, CancellationToken cancellationToken = default);

    /// <summary>Rejects a missing, forged, or changed gateway decision before actor state is read.</summary>
    Task ValidateAsync(TrustedEffectAdmission admission, string proof, CancellationToken cancellationToken = default);
}
