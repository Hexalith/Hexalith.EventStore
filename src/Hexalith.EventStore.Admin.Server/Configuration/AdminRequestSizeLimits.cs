namespace Hexalith.EventStore.Admin.Server.Configuration;

/// <summary>
/// Encoded HTTP body limits for public Admin JSON endpoints.
/// </summary>
public static class AdminRequestSizeLimits {
    /// <summary>Maximum encoded size for an ordinary JSON request body.</summary>
    public const long OrdinaryJsonBody = 1024L * 1024L;

    /// <summary>Maximum encoded size for a backup import JSON request body.</summary>
    public const long BackupImportJsonBody = 10L * 1024L * 1024L;
}
