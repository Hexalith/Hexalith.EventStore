using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

using static Hexalith.EventStore.Admin.UI.Components.Shared.StatusBadge;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// Tests for StatusDisplayConfig.FromTimelineEntryType mapping.
/// </summary>
public class TimelineEntryTypeBadgeTests {
    [Fact]
    public void FromTimelineEntryType_Command_ReturnsBlueBadge() {
        var config = StatusDisplayConfig.FromTimelineEntryType(TimelineEntryType.Command);
        config.Label.ShouldBe("Command");
        config.CssColor.ShouldContain("inflight");
    }

    [Fact]
    public void FromTimelineEntryType_Event_ReturnsGreenBadge() {
        var config = StatusDisplayConfig.FromTimelineEntryType(TimelineEntryType.Event);
        config.Label.ShouldBe("Event");
        config.CssColor.ShouldContain("success");
    }

    [Fact]
    public void FromTimelineEntryType_Query_ReturnsGrayBadge() {
        var config = StatusDisplayConfig.FromTimelineEntryType(TimelineEntryType.Query);
        config.Label.ShouldBe("Query");
        config.CssColor.ShouldContain("neutral");
    }
}
