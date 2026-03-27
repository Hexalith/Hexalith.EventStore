using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
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
/// bUnit tests for the BisectTool component.
/// </summary>
public class BisectToolTests : AdminUITestContext
{
    private readonly AdminStreamApiClient _mockApiClient;

    public BisectToolTests()
    {
        _mockApiClient = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);

        // Default: stream has enough events for bisect
        _ = _mockApiClient.GetStreamTimelineAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(),
            Arg.Any<CancellationToken>())
            .Returns(new PagedResult<TimelineEntry>([], 100, null));

        Services.AddScoped(_ => _mockApiClient);
        Services.AddScoped<DashboardRefreshService>();
        Services.AddScoped<TopologyCacheService>();
        TestSignalRClient testClient = new();
        Services.AddSingleton(testClient);
        Services.AddSingleton(testClient.Inner);
    }

    [Fact]
    public void BisectTool_RendersSetupPhase()
    {
        // Act
        IRenderedComponent<BisectTool> cut = RenderBisectTool();

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Bisect Tool");
        markup.ShouldContain("Known Good Sequence");
        markup.ShouldContain("Known Bad Sequence");
        markup.ShouldContain("Start Bisect");
        markup.ShouldContain("Close");
    }

    [Fact]
    public void BisectTool_PreFillsFromParameters()
    {
        // Act
        IRenderedComponent<BisectTool> cut = RenderBisectTool(goodSeq: 0, badSeq: 100);

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Bisect Tool");
        markup.ShouldContain("Start Bisect");
    }

    [Fact]
    public void BisectTool_ShowsResult_OnSuccessfulBisect()
    {
        // Arrange
        DateTimeOffset timestamp = new(2026, 3, 27, 10, 0, 0, TimeSpan.Zero);
        List<FieldChange> changes = [new("Count", "4", "5")];
        List<BisectStep> steps =
        [
            new(1, 50, "good", 0),
            new(2, 75, "bad", 1),
        ];
        var bisectResult = new BisectResult(
            "test-tenant", "counter", "agg-001",
            50, 75, timestamp,
            "CounterIncremented", "corr-1", "user-1",
            changes, ["Count"], steps, 2, false);

        _ = _mockApiClient.BisectAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<long>(), Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BisectResult?>(bisectResult));

        // Act
        IRenderedComponent<BisectTool> cut = RenderBisectTool(goodSeq: 0, badSeq: 100);

        // Trigger bisect
        cut.Find("fluent-button[appearance='accent']").Click();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Divergent Event"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("#75");
        markup.ShouldContain("CounterIncremented");
        markup.ShouldContain("Count");
        markup.ShouldContain("Navigate to Event");
        markup.ShouldContain("New Bisect");
    }

    [Fact]
    public void BisectTool_ShowsNoDivergence_WhenFieldChangesEmpty()
    {
        // Arrange
        var bisectResult = new BisectResult(
            "test-tenant", "counter", "agg-001",
            0, 100, DateTimeOffset.MinValue,
            string.Empty, string.Empty, string.Empty,
            [], [], [new(1, 50, "good", 0)], 1, false);

        _ = _mockApiClient.BisectAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<long>(), Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BisectResult?>(bisectResult));

        // Act
        IRenderedComponent<BisectTool> cut = RenderBisectTool(goodSeq: 0, badSeq: 100);
        cut.Find("fluent-button[appearance='accent']").Click();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No divergence detected"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Verify your good/bad sequence selection");
    }

    [Fact]
    public void BisectTool_ShowsError_OnApiFailure()
    {
        // Arrange
        _ = _mockApiClient.BisectAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<long>(), Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BisectResult?>(null));

        // Act
        IRenderedComponent<BisectTool> cut = RenderBisectTool(goodSeq: 0, badSeq: 100);
        cut.Find("fluent-button[appearance='accent']").Click();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to run bisect"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Unable to run bisect");
    }

    [Fact]
    public void BisectTool_ShowsTimeoutError_OnOperationCanceled()
    {
        // Arrange
        _ = _mockApiClient.BisectAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<long>(), Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        IRenderedComponent<BisectTool> cut = RenderBisectTool(goodSeq: 0, badSeq: 100);
        cut.Find("fluent-button[appearance='accent']").Click();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Bisect timed out"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Bisect timed out");
    }

    [Fact]
    public void BisectTool_ShowsTruncationWarning_WhenIsTruncated()
    {
        // Arrange
        var bisectResult = new BisectResult(
            "test-tenant", "counter", "agg-001",
            40, 60, DateTimeOffset.UtcNow,
            "Evt", "c", "u",
            [new("F", "1", "2")], ["F"], [new(1, 50, "bad", 1)], 1, true);

        _ = _mockApiClient.BisectAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<long>(), Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BisectResult?>(bisectResult));

        // Act
        IRenderedComponent<BisectTool> cut = RenderBisectTool(goodSeq: 0, badSeq: 100);
        cut.Find("fluent-button[appearance='accent']").Click();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("maximum step limit"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("maximum step limit");
    }

    [Fact]
    public void BisectTool_ShowsBlameButton_WhenOnNavigateToBlameProvided()
    {
        // Arrange
        DateTimeOffset timestamp = new(2026, 3, 27, 10, 0, 0, TimeSpan.Zero);
        var bisectResult = new BisectResult(
            "test-tenant", "counter", "agg-001",
            50, 75, timestamp,
            "Evt", "c", "u",
            [new("F", "1", "2")], ["F"], [new(1, 62, "bad", 1)], 1, false);

        _ = _mockApiClient.BisectAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<long>(), Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BisectResult?>(bisectResult));

        // Act
        IRenderedComponent<BisectTool> cut = Render<BisectTool>(p => p
            .Add(c => c.TenantId, "test-tenant")
            .Add(c => c.Domain, "counter")
            .Add(c => c.AggregateId, "agg-001")
            .Add(c => c.InitialGoodSequence, (long?)0)
            .Add(c => c.InitialBadSequence, (long?)100)
            .Add(c => c.OnNavigateToBlame, EventCallback.Factory.Create<long>(this, _ => { })));

        cut.Find("fluent-button[appearance='accent']").Click();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Run Blame at This Event"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Run Blame at This Event");
    }

    private IRenderedComponent<BisectTool> RenderBisectTool(long? goodSeq = null, long? badSeq = null)
    {
        return Render<BisectTool>(p => p
            .Add(c => c.TenantId, "test-tenant")
            .Add(c => c.Domain, "counter")
            .Add(c => c.AggregateId, "agg-001")
            .Add(c => c.InitialGoodSequence, goodSeq)
            .Add(c => c.InitialBadSequence, badSeq));
    }
}
