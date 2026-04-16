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
        StringWriter stdout = new();
        Console.SetOut(stdout);
        try {
            int exitCode = ConfigCompletionCommand.Execute(shell);

            exitCode.ShouldBe(ExitCodes.Success);
            stdout.ToString().ShouldNotBeNullOrWhiteSpace();
        }
        finally {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
    }

    [Fact]
    public void Execute_BashScript_ContainsExpectedContent() {
        StringWriter stdout = new();
        Console.SetOut(stdout);
        try {
            _ = ConfigCompletionCommand.Execute("bash");
            string output = stdout.ToString();

            output.ShouldContain("eventstore-admin");
            output.ShouldContain("complete -F");
            output.ShouldContain("health");
            output.ShouldContain("stream");
            output.ShouldContain("config");
            output.ShouldContain("--profile");
            output.ShouldContain("profiles.json");
        }
        finally {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
    }

    [Fact]
    public void Execute_ZshScript_ContainsExpectedContent() {
        StringWriter stdout = new();
        Console.SetOut(stdout);
        try {
            _ = ConfigCompletionCommand.Execute("zsh");
            string output = stdout.ToString();

            output.ShouldContain("compdef");
            output.ShouldContain("_eventstore_admin");
            output.ShouldContain("_arguments");
        }
        finally {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
    }

    [Fact]
    public void Execute_PowerShellScript_ContainsExpectedContent() {
        StringWriter stdout = new();
        Console.SetOut(stdout);
        try {
            _ = ConfigCompletionCommand.Execute("powershell");
            string output = stdout.ToString();

            output.ShouldContain("Register-ArgumentCompleter");
            output.ShouldContain("eventstore-admin");
            output.ShouldContain("profiles.json");
        }
        finally {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
    }

    [Fact]
    public void Execute_FishScript_ContainsExpectedContent() {
        StringWriter stdout = new();
        Console.SetOut(stdout);
        try {
            _ = ConfigCompletionCommand.Execute("fish");
            string output = stdout.ToString();

            output.ShouldContain("complete -c eventstore-admin");
            output.ShouldContain("__fish_use_subcommand");
            output.ShouldContain("__eventstore_profiles");
            // Fish 'use' completions require both config and use context
            output.ShouldContain("__fish_seen_subcommand_from config; and __fish_seen_subcommand_from use");
        }
        finally {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
    }

    [Fact]
    public void Execute_AllShells_ContainProfileReadingLogic() {
        foreach (string shell in new[] { "bash", "zsh", "fish" }) {
            StringWriter stdout = new();
            Console.SetOut(stdout);
            try {
                _ = ConfigCompletionCommand.Execute(shell);
                string output = stdout.ToString();

                // All Unix shells should have grep/sed based profile extraction
                output.ShouldContain("profiles.json");
            }
            finally {
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            }
        }
    }

    [Fact]
    public void Execute_AllShells_ContainSubcommandNames() {
        string[] expectedSubcommands = ["health", "stream", "projection", "tenant", "snapshot", "backup", "config"];

        foreach (string shell in new[] { "bash", "zsh", "powershell", "fish" }) {
            StringWriter stdout = new();
            Console.SetOut(stdout);
            try {
                _ = ConfigCompletionCommand.Execute(shell);
                string output = stdout.ToString();

                foreach (string subcmd in expectedSubcommands) {
                    output.ShouldContain(subcmd);
                }
            }
            finally {
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            }
        }
    }
}
