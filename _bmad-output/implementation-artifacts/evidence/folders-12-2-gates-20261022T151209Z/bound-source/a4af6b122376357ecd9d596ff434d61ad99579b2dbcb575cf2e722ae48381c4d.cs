using System.CommandLine;

namespace Hexalith.EventStore.Admin.Cli.Commands.Stream;

/// <summary>
/// Shared positional arguments for stream sub-subcommands.
/// </summary>
public static class StreamArguments {
    /// <summary>Creates the tenant positional argument.</summary>
    public static Argument<string> Tenant() => new("tenant") { Description = "Tenant identifier" };

    /// <summary>Creates the domain positional argument.</summary>
    public static Argument<string> Domain() => new("domain") { Description = "Domain name" };

    /// <summary>Creates the aggregate ID positional argument.</summary>
    public static Argument<string> AggregateId() => new("aggregateId") { Description = "Aggregate identifier" };
}
