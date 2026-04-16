using System.CommandLine;

namespace Hexalith.EventStore.Admin.Cli.Commands;

/// <summary>
/// Factory for placeholder subcommands that print "not yet implemented" and return exit code 0.
/// </summary>
public static class StubCommands {
    /// <summary>
    /// Creates a stub subcommand with the given name and description.
    /// </summary>
    public static Command Create(string name, string description) {
        Command command = new(name, description);
        command.SetAction((_, _) => {
            Console.Error.WriteLine("Not yet implemented. Coming in a future release.");
            return Task.FromResult(ExitCodes.Success);
        });
        return command;
    }
}
