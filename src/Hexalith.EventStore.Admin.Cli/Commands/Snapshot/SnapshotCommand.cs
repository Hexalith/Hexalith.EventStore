using System.CommandLine;

namespace Hexalith.EventStore.Admin.Cli.Commands.Snapshot;

/// <summary>
/// Parent command for all snapshot sub-subcommands.
/// </summary>
public static class SnapshotCommand {
    /// <summary>
    /// Creates the snapshot parent command with all four sub-subcommands.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding) {
        Command command = new("snapshot", "Manage aggregate snapshots and snapshot policies");
        command.Subcommands.Add(SnapshotPoliciesCommand.Create(binding));
        command.Subcommands.Add(SnapshotCreateCommand.Create(binding));
        command.Subcommands.Add(SnapshotSetPolicyCommand.Create(binding));
        command.Subcommands.Add(SnapshotDeletePolicyCommand.Create(binding));
        return command;
    }
}
