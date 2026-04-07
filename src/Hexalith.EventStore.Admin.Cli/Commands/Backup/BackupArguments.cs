using System.CommandLine;

namespace Hexalith.EventStore.Admin.Cli.Commands.Backup;

/// <summary>
/// Shared positional arguments for backup sub-subcommands.
/// </summary>
public static class BackupArguments
{
    /// <summary>Creates the tenant positional argument.</summary>
    public static Argument<string> TenantId() => new("tenantId") { Description = "Tenant identifier" };

    /// <summary>Creates the backup ID positional argument.</summary>
    public static Argument<string> BackupId() => new("backupId") { Description = "Backup identifier" };

    /// <summary>Creates the domain positional argument.</summary>
    public static Argument<string> Domain() => new("domain") { Description = "Domain name" };

    /// <summary>Creates the aggregate ID positional argument.</summary>
    public static Argument<string> AggregateId() => new("aggregateId") { Description = "Aggregate identifier" };
}
