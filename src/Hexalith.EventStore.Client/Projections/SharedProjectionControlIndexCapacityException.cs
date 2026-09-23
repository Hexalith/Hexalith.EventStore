namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// A discovery index reached its bounded CAS-document quota. The delivery remains unacknowledged
/// until an audited tenant offboarding/prune or a replacement index migration frees capacity.
/// </summary>
public sealed class SharedProjectionControlIndexCapacityException : InvalidOperationException
{
    /// <summary>Creates the fail-closed capacity error for an index.</summary>
    public SharedProjectionControlIndexCapacityException(string indexName)
        : base($"Control index '{indexName}' reached its bounded quota "
            + $"({SharedProjectionControlIndexWriter.MaxTenants} tenants, "
            + $"{SharedProjectionControlIndexWriter.MaxReceipts} receipts, "
            + $"{SharedProjectionControlIndexWriter.MaxStateBytes} serialized bytes); "
            + "offboard a tenant with an audit id or migrate the index before retrying.")
    {
        IndexName = indexName;
    }

    /// <summary>Gets the store-local control index name.</summary>
    public string IndexName { get; }
}
