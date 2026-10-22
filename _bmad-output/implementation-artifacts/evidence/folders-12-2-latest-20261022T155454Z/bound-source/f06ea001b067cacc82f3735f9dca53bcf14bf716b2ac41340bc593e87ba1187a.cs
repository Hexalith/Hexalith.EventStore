using System.CommandLine;

namespace Hexalith.EventStore.Admin.Cli.Commands;

/// <summary>
/// Factory for placeholder subcommands that report their unavailable state as an error.
/// </summary>
public static class StubCommands {
    /// <summary>
    /// Creates a stub subcommand with the given name and description.
    /// </summary>
    public static Command Create(string name, string description) {
        Command command = new(name, description);
        command.SetAction((_, _) => {
            Console.Error.WriteLine("Command unavailable. This operation is not implemented in this release.");
            return Task.FromResult(ExitCodes.Error);
        });
        return command;
    }
}
