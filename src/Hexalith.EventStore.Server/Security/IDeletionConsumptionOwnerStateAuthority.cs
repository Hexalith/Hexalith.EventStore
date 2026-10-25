using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Independently enrolled current protection-owner state and exact private operation authority.</summary>
/// <remarks>The installation binds one canonical tenant and the exact deletion-consumption actor/ledger scope.
/// Local absence, restored state and governance guard revisions cannot enroll or advance this authority.
/// Each inherited method authenticates the actor's actual admitted original and current phase permission;
/// recovery resolves that same original without repeating a physical effect.</remarks>
public interface IDeletionConsumptionOwnerStateAuthority : IAnchoredStateTransitionAuthority
{
    /// <summary>Gets the canonical tenant selected by the independently installed authority.</summary>
    string TenantId { get; }

    /// <summary>Gets the exact independently enrolled actor and private ledger purpose.</summary>
    string ScopeId { get; }

    /// <summary>Authenticates the exact current owner revision/digest, including independently enrolled genesis at revision zero.</summary>
    /// <param name="ownerRevision">Protection owner revision, independent of guard and custody-head revisions.</param>
    /// <param name="exactStateDigest">Digest of the exact owner ledger bytes.</param>
    /// <param name="cancellationToken">Explicit caller cancellation.</param>
    /// <returns>True only when independently surviving current authority selects this state.</returns>
    Task<bool> ValidateStateAsync(long ownerRevision, string exactStateDigest, CancellationToken cancellationToken = default);

    /// <summary>Authenticates current private permission for this exact actor method and logical batch or revocation identity.</summary>
    /// <param name="identity">Original batch or revocation identity.</param>
    /// <param name="operation">Exact actor operation name.</param>
    /// <param name="cancellationToken">Explicit caller cancellation.</param>
    /// <returns>True only for current independently authorized permission; no anchor advances.</returns>
    Task<bool> AuthorizeOperationAsync(string identity, string operation, CancellationToken cancellationToken = default);
}
