
using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Contracts.Tests.Commands;

public class CommandStatusTests {
    [Fact]
    public void CommandStatus_HasExactly8Values() {
        CommandStatus[] values = Enum.GetValues<CommandStatus>();
        values.Length.ShouldBe(8);
    }

    [Theory]
    [InlineData(CommandStatus.Received, 0)]
    [InlineData(CommandStatus.Processing, 1)]
    [InlineData(CommandStatus.EventsStored, 2)]
    [InlineData(CommandStatus.EventsPublished, 3)]
    [InlineData(CommandStatus.Completed, 4)]
    [InlineData(CommandStatus.Rejected, 5)]
    [InlineData(CommandStatus.PublishFailed, 6)]
    [InlineData(CommandStatus.TimedOut, 7)]
    public void CommandStatus_HasCorrectExplicitIntegerValues(CommandStatus status, int expectedValue) => ((int)status).ShouldBe(expectedValue);

    [Fact]
    public void CommandStatus_ValuesAreInLifecycleOrder() {
        CommandStatus[] values = Enum.GetValues<CommandStatus>();

        values[0].ShouldBe(CommandStatus.Received);
        values[1].ShouldBe(CommandStatus.Processing);
        values[2].ShouldBe(CommandStatus.EventsStored);
        values[3].ShouldBe(CommandStatus.EventsPublished);
        values[4].ShouldBe(CommandStatus.Completed);
        values[5].ShouldBe(CommandStatus.Rejected);
        values[6].ShouldBe(CommandStatus.PublishFailed);
        values[7].ShouldBe(CommandStatus.TimedOut);
    }

    [Fact]
    public void CommandStatus_TerminalStatuses_AreIdentifiedCorrectly() {
        CommandStatus[] terminalStatuses = new[] {
            CommandStatus.Completed,
            CommandStatus.Rejected,
            CommandStatus.PublishFailed,
            CommandStatus.TimedOut,
        };

        terminalStatuses.Length.ShouldBe(4);
        terminalStatuses.ShouldNotContain(CommandStatus.Received);
        terminalStatuses.ShouldNotContain(CommandStatus.Processing);
        terminalStatuses.ShouldNotContain(CommandStatus.EventsStored);
        terminalStatuses.ShouldNotContain(CommandStatus.EventsPublished);
        terminalStatuses.ShouldBe(
            Enum.GetValues<CommandStatus>().Where(status => (int)status >= (int)CommandStatus.Completed));
    }
}
