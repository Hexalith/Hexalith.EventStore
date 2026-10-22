using System.CommandLine;

namespace Hexalith.EventStore.Admin.Cli.Commands.Tenant;

/// <summary>
/// Shared positional arguments for tenant sub-subcommands.
/// </summary>
public static class TenantArguments {
    /// <summary>Creates the tenant ID positional argument.</summary>
    public static Argument<string> TenantId() => new("tenantId") { Description = "Tenant identifier" };
}
