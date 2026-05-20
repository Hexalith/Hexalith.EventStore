using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.UI.Components;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

public class CommandDetailPanelTests : AdminUITestContext {
    [Fact]
    public void CommandDetailPanel_RendersCommandMetadata() {
        var entry = new TimelineEntry(1, DateTimeOffset.UtcNow, TimelineEntryType.Command,
            "IncrementCounter", "corr-1", "user-1");

        IRenderedComponent<CommandDetailPanel> cut = Render<CommandDetailPanel>(p => p
            .Add(c => c.Entry, entry));

        string markup = cut.Markup;
        markup.ShouldContain("IncrementCounter");
        markup.ShouldContain("corr-1");
    }

    [Fact]
    public void CommandDetailPanel_RendersSequenceNumber() {
        var entry = new TimelineEntry(42, DateTimeOffset.UtcNow, TimelineEntryType.Command,
            "IncrementCounter", "corr-1", null);

        IRenderedComponent<CommandDetailPanel> cut = Render<CommandDetailPanel>(p => p
            .Add(c => c.Entry, entry));

        cut.Markup.ShouldContain("42");
    }

    [Fact]
    public void CommandDetailPanel_RendersUserId_WhenPresent() {
        var entry = new TimelineEntry(1, DateTimeOffset.UtcNow, TimelineEntryType.Command,
            "IncrementCounter", "corr-1", "admin@acme.com");

        IRenderedComponent<CommandDetailPanel> cut = Render<CommandDetailPanel>(p => p
            .Add(c => c.Entry, entry));

        cut.Markup.ShouldContain("admin@acme.com");
    }

    [Fact]
    public void CommandDetailPanel_InvokesOnCopyCorrelation_ForCopyButton() {
        var entry = new TimelineEntry(1, DateTimeOffset.UtcNow, TimelineEntryType.Command,
            "IncrementCounter", "corr-copy-test", null);
        string? copiedCorrelation = null;
        string? tracedCorrelation = null;

        IRenderedComponent<CommandDetailPanel> cut = Render<CommandDetailPanel>(p => p
            .Add(c => c.Entry, entry)
            .Add(c => c.OnCopyCorrelation, id => copiedCorrelation = id)
            .Add(c => c.OnOpenTraceMap, id => tracedCorrelation = id));

        AngleSharp.Dom.IElement? copyButton = cut.FindAll("fluent-button")
            .FirstOrDefault(b => b.GetAttribute("aria-label") == "Copy correlation ID");
        _ = copyButton.ShouldNotBeNull();
        copyButton!.Click();

        copiedCorrelation.ShouldBe("corr-copy-test");
        tracedCorrelation.ShouldBeNull();
    }

    [Fact]
    public void CommandDetailPanel_InvokesOnOpenTraceMap_ForTraceButton() {
        var entry = new TimelineEntry(1, DateTimeOffset.UtcNow, TimelineEntryType.Command,
            "IncrementCounter", "corr-trace-test", null);
        string? copiedCorrelation = null;
        string? tracedCorrelation = null;

        IRenderedComponent<CommandDetailPanel> cut = Render<CommandDetailPanel>(p => p
            .Add(c => c.Entry, entry)
            .Add(c => c.OnCopyCorrelation, id => copiedCorrelation = id)
            .Add(c => c.OnOpenTraceMap, id => tracedCorrelation = id));

        AngleSharp.Dom.IElement? traceButton = cut.FindAll("fluent-button")
            .FirstOrDefault(b => b.GetAttribute("aria-label") == "Open trace map");
        _ = traceButton.ShouldNotBeNull();
        traceButton!.Click();

        tracedCorrelation.ShouldBe("corr-trace-test");
        copiedCorrelation.ShouldBeNull();
    }
}
