namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Private revocation subscriber capability, separate from batch, signing, human and Workflow authority.</summary>
/// <remarks>Inject only into the authenticated custody revocation subscriber; no public or general dispatcher registration.</remarks>
public interface IDeletionCapabilityCompromiseRegistrar
{
    /// <summary>Installs the tenant/key-version block at the same protection owner that reserves consumption.</summary>
    Task<DeletionCapabilityRevocationReceipt?> RegisterAsync(DeletionCapabilityRevocationEnvelope envelope,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves the exact original durable block after acknowledgement loss; changed evidence conflicts.</summary>
    Task<DeletionCapabilityRevocationReceipt?> LookupAsync(DeletionCapabilityRevocationEnvelope envelope,
        CancellationToken cancellationToken = default);
}
