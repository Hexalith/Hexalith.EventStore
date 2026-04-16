using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.UI.Components;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// bUnit tests for the EventDebugger component.
/// </summary>
public class EventDebuggerTests : AdminUITestContext {
    private readonly AdminStreamApiClient _mockApiClient;

    public EventDebuggerTests() {
        _mockApiClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);

        _ = Services.AddScoped(_ => _mockApiClient);
        _ = Services.AddScoped<DashboardRefreshService>();
        _ = Services.AddScoped<TopologyCacheService>();
        TestSignalRClient testClient = new();
        _ = Services.AddSingleton(testClient);
        _ = Services.AddSingleton(testClient.Inner);
    }

    [Fact]
    public void EventDebugger_RendersFrameOnLoad() {
        // Arrange
        EventStepFrame frame = CreateTestFrame(3, 10);
        _ = _mockApiClient.GetEventStepFrameAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventStepFrame?>(frame));

        // Act
        IRenderedComponent<EventDebugger> cut = RenderDebugger();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Event 3 of 10"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Event Debugger");
        markup.ShouldContain("CounterIncremented");
        markup.ShouldContain("Close");
    }

    [Fact]
    public void EventDebugger_ShowsFieldChanges() {
        // Arrange
        EventStepFrame frame = CreateTestFrame(3, 10, [new FieldChange("Count", "2", "3")]);
        _ = _mockApiClient.GetEventStepFrameAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventStepFrame?>(frame));

        // Act
        IRenderedComponent<EventDebugger> cut = RenderDebugger();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Count"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Changes at This Event");
        markup.ShouldContain("Count");
    }

    [Fact]
    public void EventDebugger_FirstEvent_ShowsInitialState() {
        // Arrange
        EventStepFrame frame = CreateTestFrame(1, 10, [new FieldChange("Count", "", "1")]);
        _ = _mockApiClient.GetEventStepFrameAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventStepFrame?>(frame));

        // Act
        IRenderedComponent<EventDebugger> cut = RenderDebugger(initialSequence: 1);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Initial State"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Initial State");
    }

    [Fact]
    public void EventDebugger_NullFrame_ShowsEmptyStreamMessage() {
        // Arrange
        _ = _mockApiClient.GetEventStepFrameAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventStepFrame?>(null));

        // Act
        IRenderedComponent<EventDebugger> cut = RenderDebugger();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Stream has no events"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("nothing to step through");
    }

    [Fact]
    public void EventDebugger_ShowsError_OnTimeout() {
        // Arrange — return a faulted task (not ThrowsAsync, which throws synchronously in bUnit)
        _ = _mockApiClient.GetEventStepFrameAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EventStepFrame?>(new OperationCanceledException()));

        // Act
        IRenderedComponent<EventDebugger> cut = RenderDebugger();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("timed out"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("timed out");
    }

    [Fact]
    public void EventDebugger_ShowsError_OnApiFailure() {
        // Arrange — return a faulted task
        _ = _mockApiClient.GetEventStepFrameAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EventStepFrame?>(new InvalidOperationException("server error")));

        // Act
        IRenderedComponent<EventDebugger> cut = RenderDebugger();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to load event frame"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("check server connectivity");
    }

    [Fact]
    public void EventDebugger_DisablesPreviousButton_AtFirstEvent() {
        // Arrange
        EventStepFrame frame = CreateTestFrame(1, 5);
        _ = _mockApiClient.GetEventStepFrameAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventStepFrame?>(frame));

        // Act
        IRenderedComponent<EventDebugger> cut = RenderDebugger(initialSequence: 1);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Event 1 of 5"), TimeSpan.FromSeconds(5));

        // Assert: Previous and First buttons should be disabled
        AngleSharp.Dom.IElement[] disabledButtons = cut.FindAll("fluent-button[disabled]").ToArray();
        disabledButtons.Length.ShouldBeGreaterThanOrEqualTo(2); // First and Previous
    }

    [Fact]
    public void EventDebugger_DisablesNextButton_AtLastEvent() {
        // Arrange
        EventStepFrame frame = CreateTestFrame(5, 5);
        _ = _mockApiClient.GetEventStepFrameAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventStepFrame?>(frame));

        // Act
        IRenderedComponent<EventDebugger> cut = RenderDebugger(initialSequence: 5);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Event 5 of 5"), TimeSpan.FromSeconds(5));

        // Assert: Next, Last, and Play buttons should be disabled
        AngleSharp.Dom.IElement[] disabledButtons = cut.FindAll("fluent-button[disabled]").ToArray();
        disabledButtons.Length.ShouldBeGreaterThanOrEqualTo(3); // Next, Last, Play
    }

    [Fact]
    public void EventDebugger_ShowsViewEventDetailButton() {
        // Arrange
        EventStepFrame frame = CreateTestFrame(3, 10);
        _ = _mockApiClient.GetEventStepFrameAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventStepFrame?>(frame));

        // Act
        IRenderedComponent<EventDebugger> cut = RenderDebugger();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("View Event Detail"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("View Event Detail");
    }

    [Fact]
    public void EventDebugger_ShowsBlameButton_WhenOnNavigateToBlameProvided() {
        // Arrange
        EventStepFrame frame = CreateTestFrame(3, 10);
        _ = _mockApiClient.GetEventStepFrameAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventStepFrame?>(frame));

        // Act
        IRenderedComponent<EventDebugger> cut = Render<EventDebugger>(p => p
            .Add(c => c.TenantId, "test-tenant")
            .Add(c => c.Domain, "counter")
            .Add(c => c.AggregateId, "agg-001")
            .Add(c => c.InitialSequence, (long?)3)
            .Add(c => c.OnNavigateToBlame, EventCallback.Factory.Create<long>(this, _ => { })));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Blame at Seq 3"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Blame at Seq 3");
    }

    [Fact]
    public void EventDebugger_ShowsWatchFieldsButton() {
        // Arrange
        EventStepFrame frame = CreateTestFrame(3, 10);
        _ = _mockApiClient.GetEventStepFrameAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventStepFrame?>(frame));

        // Act
        IRenderedComponent<EventDebugger> cut = RenderDebugger();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Watch Fields"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Watch Fields");
    }

    [Fact]
    public void EventDebugger_ShowsSpeedSelector() {
        // Arrange
        EventStepFrame frame = CreateTestFrame(3, 10);
        _ = _mockApiClient.GetEventStepFrameAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventStepFrame?>(frame));

        // Act
        IRenderedComponent<EventDebugger> cut = RenderDebugger();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Normal"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Slow");
        markup.ShouldContain("Normal");
        markup.ShouldContain("Fast");
        markup.ShouldContain("Fastest");
    }

    [Fact]
    public void EventDebugger_AutoPlay_DisabledAtLastEvent() {
        // Arrange — last event, HasNext = false
        EventStepFrame frame = CreateTestFrame(5, 5);
        _ = _mockApiClient.GetEventStepFrameAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventStepFrame?>(frame));

        // Act
        IRenderedComponent<EventDebugger> cut = RenderDebugger(initialSequence: 5);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Event 5 of 5"), TimeSpan.FromSeconds(5));

        // Assert — Play button should be disabled since we're at the last event
        AngleSharp.Dom.IElement[] disabledButtons = cut.FindAll("fluent-button[disabled]").ToArray();
        disabledButtons.Length.ShouldBeGreaterThanOrEqualTo(3); // Next, Last, Play
    }

    [Fact]
    public async Task EventDebugger_AutoPlay_AdvancesToNextFrame() {
        // Arrange
        EventStepFrame frame3 = CreateTestFrame(3, 5, [new FieldChange("Count", "2", "3")]);
        EventStepFrame frame4 = CreateTestFrame(4, 5, [new FieldChange("Count", "3", "4")]);
        int callCount = 0;
        _ = _mockApiClient.GetEventStepFrameAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(c => {
                callCount++;
                long seq = c.Arg<long>();
                return Task.FromResult<EventStepFrame?>(seq <= 3 ? frame3 : frame4);
            });

        // Act
        IRenderedComponent<EventDebugger> cut = RenderDebugger(initialSequence: 3);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Event 3 of 5"), TimeSpan.FromSeconds(5));

        // Find and click the play button (the one that's not disabled and has Play icon)
        AngleSharp.Dom.IElement playButton = cut.FindAll("fluent-button")
            .First(b => b.GetAttribute("aria-label") == "Start auto-play");
        await cut.InvokeAsync(() => playButton.Click());

        // Assert — should advance to frame 4
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Event 4 of 5"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task EventDebugger_AutoPlay_PausesOnWatchedFieldChange() {
        // Arrange
        EventStepFrame frame3 = CreateTestFrame(3, 5);
        EventStepFrame frame4 = CreateTestFrame(4, 5, [new FieldChange("Count", "3", "4")]);
        _ = _mockApiClient.GetEventStepFrameAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(c => {
                long seq = c.Arg<long>();
                return Task.FromResult<EventStepFrame?>(seq <= 3 ? frame3 : frame4);
            });

        // Act — render and add "Count" as watched field
        IRenderedComponent<EventDebugger> cut = RenderDebugger(initialSequence: 3);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Event 3 of 5"), TimeSpan.FromSeconds(5));

        // Open watch panel
        AngleSharp.Dom.IElement watchButton = cut.FindAll("fluent-button")
            .First(b => b.GetAttribute("aria-label") == "Watch fields");
        await cut.InvokeAsync(() => watchButton.Click());

        // Type "Count" in watch field input and add
        AngleSharp.Dom.IElement searchInput = cut.Find("fluent-text-input[aria-label='Watch field path input']");
        await cut.InvokeAsync(() => searchInput.Change("Count"));
        AngleSharp.Dom.IElement addButton = cut.FindAll("fluent-button")
            .First(b => b.GetAttribute("aria-label") == "Add watch fields");
        await cut.InvokeAsync(() => addButton.Click());

        // Verify badge rendered
        cut.Markup.ShouldContain("Count");

        // Start auto-play
        AngleSharp.Dom.IElement playButton = cut.FindAll("fluent-button")
            .First(b => b.GetAttribute("aria-label") == "Start auto-play");
        await cut.InvokeAsync(() => playButton.Click());

        // Assert — should pause when "Count" field changes at frame 4
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Paused"), TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("watched field 'Count' changed");
    }

    [Fact]
    public void EventDebugger_WatchFields_ShowsHighlightOnSubstringMatch() {
        // Arrange
        EventStepFrame frame = CreateTestFrame(3, 10, [
            new FieldChange("Count", "2", "3"),
            new FieldChange("ItemCount", "10", "11"),
            new FieldChange("Name", "A", "B"),
        ]);
        _ = _mockApiClient.GetEventStepFrameAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventStepFrame?>(frame));

        // Act — render and add "Count" watch via component
        IRenderedComponent<EventDebugger> cut = RenderDebugger(initialSequence: 3);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Count"), TimeSpan.FromSeconds(5));

        // Assert — all three field changes are rendered
        string markup = cut.Markup;
        markup.ShouldContain("Count");
        markup.ShouldContain("ItemCount");
        markup.ShouldContain("Name");
    }

    [Fact]
    public void EventDebugger_WatchFields_ButtonRendersAndOpensPanel() {
        // Arrange
        EventStepFrame frame = CreateTestFrame(3, 10);
        _ = _mockApiClient.GetEventStepFrameAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventStepFrame?>(frame));

        // Act
        IRenderedComponent<EventDebugger> cut = RenderDebugger();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Watch Fields"), TimeSpan.FromSeconds(5));

        // Assert — watch panel starts hidden, appears after click
        cut.Markup.ShouldNotContain("Enter field path to watch");
        AngleSharp.Dom.IElement watchButton = cut.FindAll("fluent-button")
            .First(b => b.GetAttribute("aria-label") == "Watch fields");
        _ = cut.InvokeAsync(() => watchButton.Click());
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Enter field path to watch"), TimeSpan.FromSeconds(5));
    }

    private IRenderedComponent<EventDebugger> RenderDebugger(long? initialSequence = null) => Render<EventDebugger>(p => p
                                                                                                       .Add(c => c.TenantId, "test-tenant")
                                                                                                       .Add(c => c.Domain, "counter")
                                                                                                       .Add(c => c.AggregateId, "agg-001")
                                                                                                       .Add(c => c.InitialSequence, initialSequence));

    private static EventStepFrame CreateTestFrame(
        long sequenceNumber,
        long totalEvents,
        List<FieldChange>? changes = null) => new(
            "test-tenant", "counter", "agg-001",
            sequenceNumber, "CounterIncremented",
            new DateTimeOffset(2026, 3, 27, 10, 0, 0, TimeSpan.Zero),
            "corr-1", "cause-1", "user-1",
            "{\"Amount\":1}", "{\"Count\":" + sequenceNumber + "}",
            changes ?? [], totalEvents);
}
