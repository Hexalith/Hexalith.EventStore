using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using CorrelationTraceMapComponent = Hexalith.EventStore.Admin.UI.Components.CorrelationTraceMap;
using CorrelationTraceMapModel = Hexalith.EventStore.Admin.Abstractions.Models.Streams.CorrelationTraceMap;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

public class CorrelationTraceMapTests : AdminUITestContext {
    private readonly AdminStreamApiClient _mockApiClient;

    public CorrelationTraceMapTests() {
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
    public void CorrelationTraceMap_RendersPipelineVisualization_WhenCompleted() {
        CorrelationTraceMapModel traceMap = CreateCompletedTrace();
        SetupTraceMock(traceMap);

        IRenderedComponent<CorrelationTraceMapComponent> cut = RenderTraceMap();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Completed"), TimeSpan.FromSeconds(5));

        string markup = cut.Markup;
        markup.ShouldContain("corr-123");
        markup.ShouldContain("IncrementCounter");
    }

    [Fact]
    public void CorrelationTraceMap_ShowsRejectedStatus() {
        var traceMap = new CorrelationTraceMapModel(
            "corr-456", "tenant-a", "Counter", "agg-1",
            "IncrementCounter", "Rejected", "user-1",
            DateTimeOffset.UtcNow.AddSeconds(-2), DateTimeOffset.UtcNow, 2000,
            [], [],
            "CounterLimitReached", "Counter limit exceeded", null,
            100, false, null);
        SetupTraceMock(traceMap);

        IRenderedComponent<CorrelationTraceMapComponent> cut = RenderTraceMap("corr-456");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Rejected"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CorrelationTraceMap_ShowsProducedEvents() {
        CorrelationTraceMapModel traceMap = CreateCompletedTrace();
        SetupTraceMock(traceMap);

        IRenderedComponent<CorrelationTraceMapComponent> cut = RenderTraceMap();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("CounterIncremented"), TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("CounterIncremented");
    }

    [Fact]
    public void CorrelationTraceMap_ShowsAffectedProjections() {
        CorrelationTraceMapModel traceMap = CreateCompletedTrace();
        SetupTraceMock(traceMap);

        IRenderedComponent<CorrelationTraceMapComponent> cut = RenderTraceMap();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("CounterSummary"), TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("CounterSummary");
    }

    [Fact]
    public void CorrelationTraceMap_ShowsScanCappedWarning() {
        var traceMap = new CorrelationTraceMapModel(
            "corr-123", "tenant-a", "Counter", "agg-1",
            "IncrementCounter", "Completed", "user-1",
            DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow, 1000,
            [new TraceMapEvent(5, "CounterIncremented", DateTimeOffset.UtcNow, null, false)],
            [],
            null, null, null,
            50000, true, "Scan stopped at 10,000 events");
        SetupTraceMock(traceMap);

        IRenderedComponent<CorrelationTraceMapComponent> cut = RenderTraceMap();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Scan stopped"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CorrelationTraceMap_ShowsError_WhenApiReturnsNull() {
        _ = _mockApiClient.GetCorrelationTraceMapAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CorrelationTraceMapModel?>(null));

        IRenderedComponent<CorrelationTraceMapComponent> cut = RenderTraceMap();
        cut.WaitForAssertion(() => {
            string markup = cut.Markup;
            (markup.Contains("not found") || markup.Contains("No trace") || markup.Contains("error")).ShouldBeTrue();
        }, TimeSpan.FromSeconds(5));
    }

    private IRenderedComponent<CorrelationTraceMapComponent> RenderTraceMap(string correlationId = "corr-123") => Render<CorrelationTraceMapComponent>(p => p
                                                                                                                           .Add(c => c.TenantId, "tenant-a")
                                                                                                                           .Add(c => c.CorrelationId, correlationId));

    private void SetupTraceMock(CorrelationTraceMapModel traceMap) => _ = _mockApiClient.GetCorrelationTraceMapAsync(
            traceMap.TenantId, traceMap.CorrelationId,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CorrelationTraceMapModel?>(traceMap));

    private static CorrelationTraceMapModel CreateCompletedTrace() => new(
            "corr-123", "tenant-a", "Counter", "agg-1",
            "IncrementCounter", "Completed", "user-1",
            DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow, 1000,
            [new TraceMapEvent(5, "CounterIncremented", DateTimeOffset.UtcNow, null, false)],
            [new TraceMapProjection("CounterSummary", "UpToDate", 5)],
            null, null, null,
            100, false, null);
}
