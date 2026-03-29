using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.UI.Components;
using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.SignalR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

public class EventDetailPanelTests : AdminUITestContext
{
    private readonly AdminStreamApiClient _mockApiClient;

    public EventDetailPanelTests()
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
    public void EventDetailPanel_RendersEventMetadata()
    {
        var detail = CreateEventDetail(5, "CounterIncremented");
        SetupDetailMock(detail);

        IRenderedComponent<EventDetailPanel> cut = RenderPanel(5);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("CounterIncremented"), TimeSpan.FromSeconds(5));

        string markup = cut.Markup;
        markup.ShouldContain("CounterIncremented");
        markup.ShouldContain("corr-1");
    }

    [Fact]
    public void EventDetailPanel_RendersPayloadJson()
    {
        var detail = CreateEventDetail(5, "CounterIncremented", """{"count":5}""");
        SetupDetailMock(detail);

        IRenderedComponent<EventDetailPanel> cut = RenderPanel(5);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("count"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void EventDetailPanel_ShowsStateSnapshot_WhenLoaded()
    {
        var detail = CreateEventDetail(5, "CounterIncremented");
        SetupDetailMock(detail);
        _ = _mockApiClient.GetAggregateStateAtPositionAsync(
            "tenant-a", "Counter", "agg-1", 5, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateStateSnapshot?>(
                new AggregateStateSnapshot("tenant-a", "Counter", "agg-1", 5,
                    DateTimeOffset.UtcNow, """{"count":5}""")));

        IRenderedComponent<EventDetailPanel> cut = RenderPanel(5);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("CounterIncremented"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void EventDetailPanel_ShowsError_WhenApiReturnsNull()
    {
        _ = _mockApiClient.GetEventDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventDetail?>(null));

        IRenderedComponent<EventDetailPanel> cut = RenderPanel(5);
        cut.WaitForAssertion(() =>
        {
            string markup = cut.Markup;
            (markup.Contains("not found") || markup.Contains("not available") || markup.Contains("error")).ShouldBeTrue();
        }, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void EventDetailPanel_ShowsCausationChain_WhenAutoTraceEnabled()
    {
        var detail = CreateEventDetail(5, "CounterIncremented");
        SetupDetailMock(detail);
        var chain = new CausationChain(
            "IncrementCounter", "cmd-1", "corr-1", "user-1",
            [new CausationEvent(5, "CounterIncremented", DateTimeOffset.UtcNow)],
            ["CounterSummary"]);
        _ = _mockApiClient.TraceCausationChainAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CausationChain?>(chain));

        IRenderedComponent<EventDetailPanel> cut = Render<EventDetailPanel>(p => p
            .Add(c => c.TenantId, "tenant-a")
            .Add(c => c.Domain, "Counter")
            .Add(c => c.AggregateId, "agg-1")
            .Add(c => c.SequenceNumber, 5)
            .Add(c => c.AutoTraceCausation, true));

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("IncrementCounter"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void EventDetailPanel_DisplaysUserId_WhenPresent()
    {
        var detail = new EventDetail("tenant-a", "Counter", "agg-1", 5,
            "CounterIncremented", DateTimeOffset.UtcNow, "corr-1", "cause-1", "admin@acme.com",
            """{"count":5}""");
        _ = _mockApiClient.GetEventDetailAsync(
            "tenant-a", "Counter", "agg-1", 5, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventDetail?>(detail));

        IRenderedComponent<EventDetailPanel> cut = RenderPanel(5);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("admin@acme.com"), TimeSpan.FromSeconds(5));
    }

    private IRenderedComponent<EventDetailPanel> RenderPanel(long sequenceNumber)
    {
        return Render<EventDetailPanel>(p => p
            .Add(c => c.TenantId, "tenant-a")
            .Add(c => c.Domain, "Counter")
            .Add(c => c.AggregateId, "agg-1")
            .Add(c => c.SequenceNumber, sequenceNumber));
    }

    private void SetupDetailMock(EventDetail detail)
    {
        _ = _mockApiClient.GetEventDetailAsync(
            "tenant-a", "Counter", "agg-1", detail.SequenceNumber, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<EventDetail?>(detail));
    }

    private static EventDetail CreateEventDetail(long seq, string eventType, string payload = "{}")
    {
        return new EventDetail("tenant-a", "Counter", "agg-1", seq,
            eventType, DateTimeOffset.UtcNow, "corr-1", null, null, payload);
    }
}
