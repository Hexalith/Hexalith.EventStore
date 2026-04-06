using System.CommandLine;

namespace Hexalith.EventStore.Admin.Cli.Commands.Tenant;

/// <summary>
/// Parent command for all tenant sub-subcommands.
/// </summary>
public static class TenantCommand
{
    /// <summary>
    /// Creates the tenant parent command with all sub-subcommands.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        Command command = new("tenant", "List tenants, view details, and verify status");
        command.Subcommands.Add(TenantListCommand.Create(binding));
        command.Subcommands.Add(TenantDetailCommand.Create(binding));
        command.Subcommands.Add(TenantUsersCommand.Create(binding));
        command.Subcommands.Add(TenantVerifyCommand.Create(binding));
        return command;
    }
}
