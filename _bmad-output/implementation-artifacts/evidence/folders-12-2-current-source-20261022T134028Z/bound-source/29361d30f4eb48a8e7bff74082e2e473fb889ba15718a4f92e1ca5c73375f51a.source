using System.CommandLine;

namespace Hexalith.EventStore.Admin.Cli.Commands.Projection;

/// <summary>
/// Parent command for all projection sub-subcommands.
/// </summary>
public static class ProjectionCommand {
    /// <summary>
    /// Creates the projection parent command with all five sub-subcommands.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding) {
        Command command = new("projection", "List, pause, resume, and reset projections");
        command.Subcommands.Add(ProjectionListCommand.Create(binding));
        command.Subcommands.Add(ProjectionStatusCommand.Create(binding));
        command.Subcommands.Add(ProjectionPauseCommand.Create(binding));
        command.Subcommands.Add(ProjectionResumeCommand.Create(binding));
        command.Subcommands.Add(ProjectionResetCommand.Create(binding));
        return command;
    }
}
