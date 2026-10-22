using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Independent exact private credentials, atomic writer-boundary/cohort receipts and antirollback authority; absent defaults deny.</summary>
public interface IDirectoryMigrationBoundaryAuthority : IAnchoredStateTransitionAuthority
{
    /// <summary>Authenticates exact tenant, method and content-free original request digest before lookup/effect/release.</summary>
    Task<bool> AuthorizeOperationAsync(string tenantId, string method, string requestDigest, CancellationToken cancellationToken = default);
    /// <summary>Authenticates qualified complete writer installation and exact legacy revocation, not caller-supplied receipt strings alone.</summary>
    Task<bool> VerifyInstallationAsync(DirectoryEpochInstallation installation, CancellationToken cancellationToken = default);
    /// <summary>Proves the atomic repair write fence and complete finite post-fence namespace/cohort, including pending/committed/authorized original operations.</summary>
    Task<bool> VerifyRepairAsync(DirectoryRepairBoundary boundary, CancellationToken cancellationToken = default);
    /// <summary>Authenticates exact original source result and terminal disposition; unknown/unpersisted outcomes do not drain.</summary>
    Task<bool> VerifyDrainAsync(DirectoryRepairDrainReceipt receipt, CancellationToken cancellationToken = default);
    /// <summary>Proves successor activation atomically revoked old epoch/fence/bridge and preserved all guard/ordinal/seal/batch/outcome evidence.</summary>
    Task<bool> VerifyActivationAsync(DirectoryEpochActivation activation, CancellationToken cancellationToken = default);
    /// <summary>Validates exact captured durable tenant/revision/state digest against the independently installed antirollback anchor.</summary>
    Task<bool> ValidateStateAsync(string tenantId, long revision, string stateDigest, CancellationToken cancellationToken = default);
    /// <summary>Deprecated compatibility-only legacy anchor hook; current recoverable actors do not invoke it.
    /// Qualified implementations must implement the mandatory inherited IAnchoredStateTransitionAuthority admitted-original admission/recovery
    /// and conditional exact transition journal, including independent staging ownership, current permission and final durable-state/anchor confirmation.
    /// Implementing this legacy hook alone never enables an actor; omitted inherited proof defaults deny.</summary>
    Task<bool> RecordRevisionAsync(string tenantId, long expectedRevision, long nextRevision, string stateDigest, CancellationToken cancellationToken = default);
}
