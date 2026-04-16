using System.CommandLine;

using Hexalith.EventStore.Admin.Cli.Commands.Backup;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Backup;

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
    }

    [Fact]
    public void Create_HasDescription() {
        var binding = GlobalOptionsBinding.Create();

        Command command = BackupCommand.Create(binding);

        command.Description.ShouldNotBeNullOrWhiteSpace();
    }
}
