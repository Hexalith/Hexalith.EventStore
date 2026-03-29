namespace Hexalith.EventStore.Testing;

/// <summary>
/// Shared constants for test data used across all test projects.
/// </summary>
public static class TestDataConstants
{
    /// <summary>Default test tenant identifier.</summary>
    public const string TenantId = "test-tenant";

    /// <summary>Alternate tenant identifier for multi-tenant isolation tests.</summary>
    public const string TenantIdA = "tenant-a";

    /// <summary>Second alternate tenant identifier for cross-tenant tests.</summary>
    public const string TenantIdB = "tenant-b";

    /// <summary>Default test domain name.</summary>
    public const string Domain = "counter";

    /// <summary>Default test aggregate identifier.</summary>
    public const string AggregateId = "agg-001";

    /// <summary>Default test tenant name.</summary>
    public const string TenantName = "acme";

    /// <summary>Increment counter command name from the sample domain.</summary>
    public const string IncrementCommand = "IncrementCounter";

    /// <summary>Decrement counter command name from the sample domain.</summary>
    public const string DecrementCommand = "DecrementCounter";
}
