using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.UI.Components.Shared;

using static Hexalith.EventStore.Admin.UI.Components.Shared.StatusBadge;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// bUnit tests for StatusBadge with StreamStatus display configurations.
/// </summary>
public class StatusBadgeStreamTests : AdminUITestContext
{
    [Theory]
    [InlineData(StreamStatus.Active, "Active")]
    [InlineData(StreamStatus.Idle, "Idle")]
    [InlineData(StreamStatus.Tombstoned, "Tombstoned")]
    public void StatusBadge_FromStreamStatus_RendersCorrectLabel(StreamStatus status, string expectedLabel)
    {
        // Arrange
        StatusDisplayConfig config = StatusDisplayConfig.FromStreamStatus(status);

        // Act
        IRenderedComponent<StatusBadge> cut = Render<StatusBadge>(
            p => p.Add(c => c.DisplayConfig, config)
                  .Add(c => c.AriaLabelPrefix, "Stream status"));

        // Assert
        cut.Markup.ShouldContain(expectedLabel);
        cut.Markup.ShouldContain($"Stream status: {expectedLabel}");
    }

    [Fact]
    public void StatusBadge_Active_HasSuccessColor()
    {
        // Arrange
        StatusDisplayConfig config = StatusDisplayConfig.FromStreamStatus(StreamStatus.Active);

        // Act
        IRenderedComponent<StatusBadge> cut = Render<StatusBadge>(
            p => p.Add(c => c.DisplayConfig, config));

        // Assert — green color token
        cut.Markup.ShouldContain("var(--hexalith-status-success)");
    }

    [Fact]
    public void StatusBadge_Idle_HasNeutralColor()
    {
        StatusDisplayConfig config = StatusDisplayConfig.FromStreamStatus(StreamStatus.Idle);
        IRenderedComponent<StatusBadge> cut = Render<StatusBadge>(
            p => p.Add(c => c.DisplayConfig, config));
        cut.Markup.ShouldContain("var(--hexalith-status-neutral)");
    }

    [Fact]
    public void StatusBadge_Tombstoned_HasErrorColor()
    {
        StatusDisplayConfig config = StatusDisplayConfig.FromStreamStatus(StreamStatus.Tombstoned);
        IRenderedComponent<StatusBadge> cut = Render<StatusBadge>(
            p => p.Add(c => c.DisplayConfig, config));
        cut.Markup.ShouldContain("var(--hexalith-status-error)");
    }
}
