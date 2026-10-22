
using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Contracts.Tests.Commands;

public class CommandStatusExtensionsTests {
    /// <summary>
    /// Pins the per-value mapping of <see cref="CommandStatus"/> to <see cref="CommandStatusExtensions.IsTerminal"/>.
    /// Closes Epic 1 retro R1-A4 / Epic 2 retro R2-A2 / Epic 3 retro R3-A3.
    /// </summary>
    [Theory]
    [InlineData(CommandStatus.Received, false)]
    [InlineData(CommandStatus.Processing, false)]
    [InlineData(CommandStatus.EventsStored, false)]
    [InlineData(CommandStatus.EventsPublished, false)]
    [InlineData(CommandStatus.Completed, true)]
    [InlineData(CommandStatus.Rejected, true)]
    [InlineData(CommandStatus.PublishFailed, true)]
    [InlineData(CommandStatus.TimedOut, true)]
    public void IsTerminal_ReturnsExpected_ForEachStatus(CommandStatus status, bool expected)
        => status.IsTerminal().ShouldBe(expected);

    /// <summary>
    /// Pins the convention <c>terminal == status &gt;= CommandStatus.Completed</c> across every defined enum value.
    /// If a future story replaces the body with a switch shape, this test gates the body-vs-convention relationship.
    /// </summary>
    [Fact]
    public void IsTerminal_AgreesWithGreaterOrEqualCompletedConvention() {
        foreach (CommandStatus status in Enum.GetValues<CommandStatus>()) {
            status.IsTerminal().ShouldBe((int)status >= (int)CommandStatus.Completed);
        }
    }

    /// <summary>
    /// Pins the 4-terminal / 4-in-flight split alongside <c>CommandStatusTests.CommandStatus_HasExactly8Values</c>.
    /// A future enum addition forces a deliberate decision about which side of the boundary the new value falls on.
    /// </summary>
    [Fact]
    public void IsTerminal_TerminalCount_IsExactly4()
        => Enum.GetValues<CommandStatus>().Count(s => s.IsTerminal()).ShouldBe(4);
}
