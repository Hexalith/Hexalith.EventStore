using System.CommandLine;

namespace Hexalith.EventStore.Admin.Cli.Commands.Config;

/// <summary>
/// Generates shell completion scripts for bash, zsh, PowerShell, and fish.
/// </summary>
public static class ConfigCompletionCommand
{
    private static readonly string[] _supportedShells = ["bash", "zsh", "powershell", "fish"];

    /// <summary>
    /// Creates the config completion subcommand.
    /// </summary>
    public static Command Create()
    {
        Argument<string> shellArg = new("shell") { Description = "Shell type (bash, zsh, powershell, fish)" };
        shellArg.AcceptOnlyFromAmong(_supportedShells);

        Command command = new("completion", "Generate shell completion scripts");
        command.Arguments.Add(shellArg);

        command.SetAction((parseResult, _) =>
        {
            string shell = parseResult.GetValue(shellArg)!;
            return Task.FromResult(Execute(shell));
        });

        return command;
    }

    internal static int Execute(string shell)
    {
        string script = shell.ToLowerInvariant() switch
        {
            "bash" => CompletionScripts.GenerateBash(),
            "zsh" => CompletionScripts.GenerateZsh(),
            "powershell" => CompletionScripts.GeneratePowerShell(),
            "fish" => CompletionScripts.GenerateFish(),
            _ => string.Empty,
        };

        if (string.IsNullOrEmpty(script))
        {
            Console.Error.WriteLine($"Unsupported shell '{shell}'. Supported: bash, zsh, powershell, fish.");
            return ExitCodes.Error;
        }

        // Write to stdout (not stderr) so it can be piped
        Console.Write(script);
        return ExitCodes.Success;
    }
}
