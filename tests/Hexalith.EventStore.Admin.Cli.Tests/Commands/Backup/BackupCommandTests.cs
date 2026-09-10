using System.CommandLine;

using Hexalith.EventStore.Admin.Cli.Commands.Backup;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Backup;

[Collection("ConsoleTests")]
public class BackupCommandTests {
    [Fact]
    public void Create_ReturnsCommandWithCorrectName() {
        var binding = GlobalOptionsBinding.Create();

        Command command = BackupCommand.Create(binding);

        command.Name.ShouldBe("backup");
    }

    [Fact]
    public void Create_HasExpectedSubcommands() {
        var binding = GlobalOptionsBinding.Create();

        Command command = BackupCommand.Create(binding);

        command.Subcommands.Count.ShouldBe(3);
        command.Subcommands.Select(c => c.Name).ShouldContain("create");
        command.Subcommands.Select(c => c.Name).ShouldContain("restore");
        command.Subcommands.Select(c => c.Name).ShouldContain("list");
        command.Subcommands.ShouldAllBe(c => c.Description!.Contains("unavailable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Create_HasDescription() {
        var binding = GlobalOptionsBinding.Create();

        Command command = BackupCommand.Create(binding);

        command.Description.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("create")]
    [InlineData("restore")]
    [InlineData("list")]
    public async Task RegisteredSubcommand_IsExplicitlyUnavailableAndReturnsError(string subcommand) {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var binding = GlobalOptionsBinding.Create();
        Command command = BackupCommand.Create(binding);
        RootCommand root = new("test");
        root.Subcommands.Add(command);
        StringWriter stderr = new();
        Console.SetError(stderr);

        try {
            int exitCode = await root.Parse(["backup", subcommand]).InvokeAsync(null, cancellationToken);

            exitCode.ShouldBe(ExitCodes.Error);
            stderr.ToString().ShouldContain("unavailable", Case.Insensitive);
            stderr.ToString().ShouldNotContain("success", Case.Insensitive);
            stderr.ToString().ShouldNotContain("completed", Case.Insensitive);
        }
        finally {
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
    }
}
