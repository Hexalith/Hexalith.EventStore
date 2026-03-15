
using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Contracts.Tests.Commands;

public class CommandStatusTests {
    [Fact]
    public void CommandStatus_HasExactly8Values() {
        CommandStatus[] values = Enum.GetValues<CommandStatus>();
        Assert.Equal(8, values.Length);
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
    public void CommandStatus_HasCorrectExplicitIntegerValues(CommandStatus status, int expectedValue) => Assert.Equal(expectedValue, (int)status);

    [Fact]
    public void CommandStatus_ValuesAreInLifecycleOrder() {
        CommandStatus[] values = Enum.GetValues<CommandStatus>();

        Assert.Equal(CommandStatus.Received, values[0]);
        Assert.Equal(CommandStatus.Processing, values[1]);
        Assert.Equal(CommandStatus.EventsStored, values[2]);
        Assert.Equal(CommandStatus.EventsPublished, values[3]);
        Assert.Equal(CommandStatus.Completed, values[4]);
        Assert.Equal(CommandStatus.Rejected, values[5]);
        Assert.Equal(CommandStatus.PublishFailed, values[6]);
        Assert.Equal(CommandStatus.TimedOut, values[7]);
    }

    [Fact]
    public void CommandStatus_TerminalStatuses_AreIdentifiedCorrectly() {
        CommandStatus[] terminalStatuses = new[] {
            CommandStatus.Completed,
            CommandStatus.Rejected,
            CommandStatus.PublishFailed,
            CommandStatus.TimedOut,
        };

        Assert.Equal(4, terminalStatuses.Length);
        Assert.DoesNotContain(CommandStatus.Received, terminalStatuses);
        Assert.DoesNotContain(CommandStatus.Processing, terminalStatuses);
        Assert.DoesNotContain(CommandStatus.EventsStored, terminalStatuses);
        Assert.DoesNotContain(CommandStatus.EventsPublished, terminalStatuses);
        Assert.Equal(
            Enum.GetValues<CommandStatus>().Where(status => (int)status >= (int)CommandStatus.Completed),
            terminalStatuses);
    }
}
