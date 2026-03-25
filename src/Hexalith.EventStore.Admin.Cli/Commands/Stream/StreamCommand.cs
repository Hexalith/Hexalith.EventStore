using System.CommandLine;

namespace Hexalith.EventStore.Admin.Cli.Commands.Stream;

/// <summary>
/// Parent command for all stream sub-subcommands.
/// </summary>
public static class StreamCommand
{
    /// <summary>
    /// Creates the stream parent command with all six sub-subcommands.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        Command command = new("stream", "Query, list, and inspect event streams");
        command.Subcommands.Add(StreamListCommand.Create(binding));
        command.Subcommands.Add(StreamEventsCommand.Create(binding));
        command.Subcommands.Add(StreamEventCommand.Create(binding));
        command.Subcommands.Add(StreamStateCommand.Create(binding));
        command.Subcommands.Add(StreamDiffCommand.Create(binding));
        command.Subcommands.Add(StreamCausationCommand.Create(binding));
        return command;
    }
}
