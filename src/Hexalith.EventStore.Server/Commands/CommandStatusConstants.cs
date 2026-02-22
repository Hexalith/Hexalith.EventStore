namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Constants for command status state store operations.
/// </summary>
public static class CommandStatusConstants {
    /// <summary>Default DAPR state store component name.</summary>
    public const string DefaultStateStoreName = "statestore";

    /// <summary>Default TTL in seconds (24 hours).</summary>
    public const int DefaultTtlSeconds = 86400;

    /// <summary>
    /// Builds the state store key for a command status entry.
    /// Key format: {tenantId}:{correlationId}:status (per D2).
    /// </summary>
    public static string BuildKey(string tenantId, string correlationId)
        => $"{tenantId}:{correlationId}:status";
}
