using System.CommandLine;

namespace Hexalith.EventStore.Admin.Cli.Commands.Snapshot;

/// <summary>
/// Shared positional arguments for snapshot sub-subcommands.
/// </summary>
public static class SnapshotArguments
{
    /// <summary>Creates the tenant positional argument.</summary>
    public static Argument<string> TenantId() => new("tenantId") { Description = "Tenant identifier" };

    /// <summary>Creates the domain positional argument.</summary>
    public static Argument<string> Domain() => new("domain") { Description = "Domain name" };

    /// <summary>Creates the aggregate ID positional argument.</summary>
    public static Argument<string> AggregateId() => new("aggregateId") { Description = "Aggregate identifier" };

    /// <summary>Creates the aggregate type positional argument.</summary>
    public static Argument<string> AggregateType() => new("aggregateType") { Description = "Aggregate type name" };
}
