namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Constants for command archive state store operations.
/// </summary>
public static class CommandArchiveConstants
{
    /// <summary>Key suffix for command archive entries.</summary>
    public const string KeySuffix = "command";

    /// <summary>
    /// Builds the state store key for a command archive entry.
    /// Key format: {tenantId}:{correlationId}:command (per D2).
    /// </summary>
    public static string BuildKey(string tenantId, string correlationId)
        => $"{tenantId}:{correlationId}:{KeySuffix}";
}
