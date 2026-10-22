namespace Hexalith.EventStore.Admin.Server.Helpers;

/// <summary>
/// State store key derivation for admin reads. These patterns are duplicated from
/// Server internals (CommandStatusConstants, CommandArchiveConstants, EventReplayProjectionActor)
/// to avoid coupling Admin.Server to the Server project.
/// If Server key patterns change, these MUST be updated to match.
/// </summary>
public static class AdminStateStoreKeys {
    /// <summary>
    /// Builds the command status key. Source: Server/Commands/CommandStatusConstants.BuildKey().
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="messageId">The command message identifier.</param>
    /// <returns>The state store key for command status.</returns>
    public static string CommandStatusKey(string tenantId, string messageId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        return $"{tenantId}:{messageId}:status";
    }

    /// <summary>
    /// Builds the command archive key. Source: Server/Commands/CommandArchiveConstants.BuildKey().
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="messageId">The command message identifier.</param>
    /// <returns>The state store key for command archive.</returns>
    public static string CommandArchiveKey(string tenantId, string messageId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        return $"{tenantId}:{messageId}:command";
    }
}
