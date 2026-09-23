namespace Hexalith.EventStore.Client.Projections;

/// <summary>Append-only global tenant membership and scope receipts for one control index.</summary>
internal sealed record SharedProjectionControlIndexState(
    int Version,
    string IndexName,
    string OwnerDomain,
    string OwnerFamily,
    string[] TenantIds,
    Dictionary<string, SharedProjectionControlIndexReceipt> ScopeReceipts);
