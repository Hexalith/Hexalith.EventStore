namespace Hexalith.EventStore.Admin.Cli;

/// <summary>
/// Standardized exit codes per UX-DR52.
/// </summary>
public static class ExitCodes {
    /// <summary>Success / healthy.</summary>
    public const int Success = 0;

    /// <summary>Degraded / warning (partial success, non-critical issues).</summary>
    public const int Degraded = 1;

    /// <summary>Critical / error (command failed, connectivity issues, auth failures).</summary>
    public const int Error = 2;
}
