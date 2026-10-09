namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Independently qualified existing backend installation for joint guard/source/outcome first-write comparisons in one tenant partition.</summary>
/// <param name="TenantId">Exact tenant.</param><param name="ComponentName">Qualified existing transactional state component.</param>
/// <param name="PartitionKey">Independently installed tenant partition.</param><param name="InstallationId">Immutable source routing/migration installation.</param>
/// <param name="GuardCellId">Preinstalled single tenant guard cell; every transaction compares its required nonempty backend ETag.</param>
/// <param name="WriterRevocationReceipt">Exact complete legacy/direct writer revocation receipt.</param><param name="AuthorityRevision">Current independent qualification revision.</param>
/// <param name="ObservedAt">Current authority observation.</param><param name="ValidUntil">Exclusive authority limit.</param>
public sealed record GuardedStateTransactionTarget(string TenantId, string ComponentName, string PartitionKey, string InstallationId,
    string GuardCellId, string WriterRevocationReceipt, string AuthorityRevision, DateTimeOffset ObservedAt, DateTimeOffset ValidUntil);
