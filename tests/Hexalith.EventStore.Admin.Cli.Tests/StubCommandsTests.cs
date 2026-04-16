using System.CommandLine;

using Hexalith.EventStore.Admin.Cli.Commands;

namespace Hexalith.EventStore.Admin.Cli.Tests;

[Collection("ConsoleTests")]
public class StubCommandsTests {

    [Theory]
    [InlineData("projection", "List, pause, resume, and reset projections")]
    [InlineData("tenant", "List tenants, view quotas, and verify isolation")]
    [InlineData("snapshot", "Manage aggregate snapshots")]
    [InlineData("backup", "Trigger and manage backups")]
    public async Task StubCommands_PrintNotImplemented_AndReturnZero(string name, string description) {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Arrange
        Command command = StubCommands.Create(name, description);
        RootCommand root = new("test");
        root.Subcommands.Add(command);

        // Capture stderr
        StringWriter stderr = new();
        Console.SetError(stderr);

        try {
            // Act
            int exitCode = await root.Parse([name]).InvokeAsync(null, ct);

            // Assert
            exitCode.ShouldBe(ExitCodes.Success);
            stderr.ToString().ShouldContain("Not yet implemented");
        }
        finally {
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
    }
}
