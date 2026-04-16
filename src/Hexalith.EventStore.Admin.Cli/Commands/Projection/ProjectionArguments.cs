using System.CommandLine;

namespace Hexalith.EventStore.Admin.Cli.Commands.Projection;

/// <summary>
/// Shared positional arguments for projection sub-subcommands.
/// </summary>
public static class ProjectionArguments {
    /// <summary>Creates the tenant positional argument.</summary>
    public static Argument<string> Tenant() => new("tenant") { Description = "Tenant identifier" };

    /// <summary>Creates the projection name positional argument.</summary>
    public static Argument<string> Name() => new("name") { Description = "Projection name" };
}
