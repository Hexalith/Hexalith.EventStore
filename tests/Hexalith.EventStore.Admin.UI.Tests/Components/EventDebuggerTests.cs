using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.UI.Components;
using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.SignalR;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// bUnit tests for the EventDebugger component.
/// </summary>
public class EventDebuggerTests : AdminUITestContext
{
    private readonly AdminStreamApiClient _mockApiClient;

    public EventDebuggerTests()
    {
        _mockApiClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);

        Services.AddScoped(_ => _mockApiClient);
        Services.AddScoped<DashboardRefreshService>();
        Services.AddScoped<TopologyCacheService>();
        TestSignalRClient testClient = new();
        Services.AddSingleton(testClient);
        Services.AddSingleton(testClient.Inner);
    }

    [Fact]
    public void EventDebugger_RendersFrameOnLoad()
    {
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
    public void EventDebugger_ShowsFieldChanges()
    {
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
    public void EventDebugger_FirstEvent_ShowsInitialState()
    {
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
    public void EventDebugger_NullFrame_ShowsEmptyStreamMessage()
    {
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
    public void EventDebugger_ShowsError_OnTimeout()
    {
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
    public void EventDebugger_ShowsError_OnApiFailure()
    {
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
    public void EventDebugger_DisablesPreviousButton_AtFirstEvent()
    {
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
    public void EventDebugger_DisablesNextButton_AtLastEvent()
    {
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
    public void EventDebugger_ShowsViewEventDetailButton()
    {
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
    public void EventDebugger_ShowsBlameButton_WhenOnNavigateToBlameProvided()
    {
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
    public void EventDebugger_ShowsWatchFieldsButton()
    {
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
    public void EventDebugger_ShowsSpeedSelector()
    {
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

    private IRenderedComponent<EventDebugger> RenderDebugger(long? initialSequence = null)
    {
        return Render<EventDebugger>(p => p
            .Add(c => c.TenantId, "test-tenant")
            .Add(c => c.Domain, "counter")
            .Add(c => c.AggregateId, "agg-001")
            .Add(c => c.InitialSequence, initialSequence));
    }

    private static EventStepFrame CreateTestFrame(
        long sequenceNumber,
        long totalEvents,
        List<FieldChange>? changes = null)
    {
        return new EventStepFrame(
            "test-tenant", "counter", "agg-001",
            sequenceNumber, "CounterIncremented",
            new DateTimeOffset(2026, 3, 27, 10, 0, 0, TimeSpan.Zero),
            "corr-1", "cause-1", "user-1",
            "{\"Amount\":1}", "{\"Count\":" + sequenceNumber + "}",
            changes ?? [], totalEvents);
    }
}
