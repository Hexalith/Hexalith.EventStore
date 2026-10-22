namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Defines the tenant-scoped correlation index key convention.
/// </summary>
public static class CommandCorrelationIndexConstants
{
    /// <summary>Builds the state key for a tenant-scoped correlation index record.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="correlationId">The correlation identifier.</param>
    /// <returns>A key in the form <c>{tenant}:{correlation}:command-index</c>.</returns>
    public static string BuildKey(string tenantId, string correlationId)
        => $"{tenantId}:{correlationId}:command-index";
}
