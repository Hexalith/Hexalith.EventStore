using System.CommandLine;

using Hexalith.EventStore.Admin.Cli.Commands.Config;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Config;

[Collection("ConsoleTests")]
public class ConfigCompletionCommandTests {
    [Theory]
    [InlineData("bash")]
    [InlineData("zsh")]
    [InlineData("powershell")]
    [InlineData("fish")]
    public void Execute_SupportedShell_ReturnsSuccess(string shell) {
        (int exitCode, string output) = Execute(shell);

        exitCode.ShouldBe(ExitCodes.Success);
        output.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Execute_BashScript_ContainsExpectedContent() {
        (_, string output) = Execute("bash");

        output.ShouldContain("eventstore-admin");
        output.ShouldContain("complete -F");
        output.ShouldContain("health");
        output.ShouldContain("stream");
        output.ShouldContain("config");
        output.ShouldContain("--profile");
        output.ShouldContain("profiles.json");
    }

    [Fact]
    public void Execute_ZshScript_ContainsExpectedContent() {
        (_, string output) = Execute("zsh");

        output.ShouldContain("compdef");
        output.ShouldContain("_eventstore_admin");
        output.ShouldContain("_arguments");
    }

    [Fact]
    public void Execute_PowerShellScript_ContainsExpectedContent() {
        (_, string output) = Execute("powershell");

        output.ShouldContain("Register-ArgumentCompleter");
        output.ShouldContain("eventstore-admin");
        output.ShouldContain("profiles.json");
    }

    [Fact]
    public void Execute_FishScript_ContainsExpectedContent() {
        (_, string output) = Execute("fish");

        output.ShouldContain("complete -c eventstore-admin");
        output.ShouldContain("__fish_use_subcommand");
        output.ShouldContain("__eventstore_profiles");
        // Fish 'use' completions require both config and use context
        output.ShouldContain("__fish_seen_subcommand_from config; and __fish_seen_subcommand_from use");
    }

    [Fact]
    public void Execute_AllShells_ContainProfileReadingLogic() {
        foreach (string shell in new[] { "bash", "zsh", "fish" }) {
            (_, string output) = Execute(shell);

            // All Unix shells should have grep/sed based profile extraction
            output.ShouldContain("profiles.json");
        }
    }

    [Fact]
    public void Execute_AllShells_ContainSubcommandNames() {
        string[] expectedSubcommands = ["health", "stream", "projection", "tenant", "snapshot", "backup", "config"];

        foreach (string shell in new[] { "bash", "zsh", "powershell", "fish" }) {
            (_, string output) = Execute(shell);

            foreach (string subcmd in expectedSubcommands) {
                output.ShouldContain(subcmd);
            }
        }
    }

    [Fact]
    public async Task Create_BashArgument_DispatchesActionAndWritesProcessOutput() {
        TextWriter originalOutput = Console.Out;
        TextWriter originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            int exitCode = await ConfigCompletionCommand.Create()
                .Parse(["bash"])
                .InvokeAsync()
                .ConfigureAwait(true);

            exitCode.ShouldBe(ExitCodes.Success);
            stdout.ToString().ShouldContain("complete -F");
            stderr.ToString().ShouldBeEmpty();
        }
        finally {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
        }
    }

    private static (int ExitCode, string Output) Execute(string shell) {
        using StringWriter stdout = new();
        using StringWriter stderr = new();

        int exitCode = ConfigCompletionCommand.Execute(shell, stdout, stderr);

        stderr.ToString().ShouldBeEmpty();
        return (exitCode, stdout.ToString());
    }
}
