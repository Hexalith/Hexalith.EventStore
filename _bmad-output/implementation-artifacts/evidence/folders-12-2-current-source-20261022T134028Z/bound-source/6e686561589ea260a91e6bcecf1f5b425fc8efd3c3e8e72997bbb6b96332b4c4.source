using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Injected guard evidence. The authority returns it only when the receipt and provenance match the request.</summary>
public interface IGovernanceGuardInstallation
{
    /// <summary>Reads signing recovery for one no-issue receipt.</summary>
    /// <param name="noIssue">Exact no-issue receipt.</param>
    /// <param name="state">Observed guard state.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The installed recovery, or null.</returns>
    Task<GovernanceSigningRecovery?> ReadSigningRecoveryAsync(GovernanceProtocolReceipt noIssue, TenantGovernanceGuardState state, CancellationToken cancellationToken);

    /// <summary>Reads lookup evidence for one transition.</summary>
    /// <param name="transition">Closed transition.</param>
    /// <param name="intentDigest">Intent digest.</param>
    /// <param name="targetMutationDigest">Target mutation digest.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The installed evidence, or null.</returns>
    Task<GovernanceGuardEvidence?> ReadLookupAsync(GovernanceGuardTransition transition, string intentDigest, string targetMutationDigest, CancellationToken cancellationToken);

    /// <summary>Reads effect evidence for one transition.</summary>
    /// <param name="transition">Closed transition.</param>
    /// <param name="intentDigest">Intent digest.</param>
    /// <param name="targetMutationDigest">Target mutation digest.</param>
    /// <param name="state">Observed guard state.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The installed evidence, or null.</returns>
    Task<GovernanceGuardEvidence?> ReadAsync(GovernanceGuardTransition transition, string intentDigest, string targetMutationDigest, TenantGovernanceGuardState state, CancellationToken cancellationToken);
}
