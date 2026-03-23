using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.UI.Components;
using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.SignalR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// bUnit tests for the StateDiffViewer component.
/// </summary>
public class StateDiffViewerTests : AdminUITestContext
{
    private readonly AdminStreamApiClient _mockApiClient;

    public StateDiffViewerTests()
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
    public void StateDiffViewer_RendersFieldChangeTable()
    {
        // Arrange
        List<FieldChange> changes =
        [
            new("order.status", "\"pending\"", "\"shipped\""),
            new("order.updatedAt", "\"2026-01-01\"", "\"2026-03-23\""),
        ];
        AggregateStateDiff diff = new(5, 10, changes);
        SetupDiffMocks(diff, """{"status":"pending"}""", """{"status":"shipped"}""");

        // Act
        IRenderedComponent<StateDiffViewer> cut = RenderDiffViewer(5, 10);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("order.status"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("order.status");
        markup.ShouldContain("order.updatedAt");
        markup.ShouldContain("Field Path");
        markup.ShouldContain("Old Value");
        markup.ShouldContain("New Value");
    }

    [Fact]
    public void StateDiffViewer_ShowsDiffColors()
    {
        // Arrange
        List<FieldChange> changes = [new("count", "1", "2")];
        AggregateStateDiff diff = new(5, 10, changes);
        SetupDiffMocks(diff, """{"count":1}""", """{"count":2}""");

        // Act
        IRenderedComponent<StateDiffViewer> cut = RenderDiffViewer(5, 10);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("count"), TimeSpan.FromSeconds(5));

        // Assert — old/new columns have diff CSS classes
        string markup = cut.Markup;
        markup.ShouldContain("diff-old-value");
        markup.ShouldContain("diff-new-value");
    }

    [Fact]
    public void StateDiffViewer_ShowsDiffNotAvailable_OnNullDiff()
    {
        // Arrange
        _ = _mockApiClient.GetAggregateStateDiffAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateStateDiff?>(null));
        _ = _mockApiClient.GetAggregateStateAtPositionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateStateSnapshot?>(null));

        // Act
        IRenderedComponent<StateDiffViewer> cut = RenderDiffViewer(5, 10);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("State diff not available"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("one or both positions have no state");
    }

    [Fact]
    public void StateDiffViewer_ShowsCollapsibleFullStatePanels()
    {
        // Arrange
        List<FieldChange> changes = [new("x", "1", "2")];
        AggregateStateDiff diff = new(5, 10, changes);
        SetupDiffMocks(diff, """{"x":1}""", """{"x":2}""");

        // Act
        IRenderedComponent<StateDiffViewer> cut = RenderDiffViewer(5, 10);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Full State at #5"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Full State at #5");
        markup.ShouldContain("Full State at #10");
    }

    [Fact]
    public void StateDiffViewer_LargeDiff_ShowsShowAllButton()
    {
        // Arrange — create 110 changes
        List<FieldChange> changes = [];
        for (int i = 0; i < 110; i++)
        {
            changes.Add(new FieldChange($"field{i}", $"\"{i}\"", $"\"{i + 1}\""));
        }

        AggregateStateDiff diff = new(1, 2, changes);
        SetupDiffMocks(diff, "{}", "{}");

        // Act
        IRenderedComponent<StateDiffViewer> cut = RenderDiffViewer(1, 2);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Show all 110 changes"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Show all 110 changes");
    }

    [Fact]
    public void StateDiffViewer_EmptyDiff_ShowsNoDifferences()
    {
        // Arrange
        AggregateStateDiff diff = new(5, 10, []);
        SetupDiffMocks(diff, """{"x":1}""", """{"x":1}""");

        // Act
        IRenderedComponent<StateDiffViewer> cut = RenderDiffViewer(5, 10);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No differences found"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("States are identical");
    }

    [Fact]
    public void StateDiffViewer_InitialStateDiff_HandlesFromSequenceZero()
    {
        // Arrange — diff from seq 0 (initial state creation)
        List<FieldChange> changes = [new("count", "", "0")];
        AggregateStateDiff diff = new(0, 1, changes);

        _ = _mockApiClient.GetAggregateStateDiffAsync(
            "test-tenant", "counter", "agg-001", 0, 1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateStateDiff?>(diff));
        // FromSequence == 0: no state fetch for position 0
        _ = _mockApiClient.GetAggregateStateAtPositionAsync(
            "test-tenant", "counter", "agg-001", 1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateStateSnapshot?>(
                new AggregateStateSnapshot("test-tenant", "counter", "agg-001", 1, DateTimeOffset.UtcNow, """{"count":0}""")));

        // Act
        IRenderedComponent<StateDiffViewer> cut = RenderDiffViewer(0, 1);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("count"), TimeSpan.FromSeconds(5));

        // Assert — shows the changes and indicates initial state
        string markup = cut.Markup;
        markup.ShouldContain("count");
        markup.ShouldContain("initial state");
    }

    [Fact]
    public void StateDiffViewer_RendersCompareHeader()
    {
        // Arrange
        List<FieldChange> changes = [new("a", "1", "2")];
        AggregateStateDiff diff = new(5, 15, changes);
        SetupDiffMocks(diff, "{}", "{}");

        // Act
        IRenderedComponent<StateDiffViewer> cut = RenderDiffViewer(5, 15);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Comparing state"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("#5");
        markup.ShouldContain("#15");
        markup.ShouldContain("Back to Timeline");
    }

    private IRenderedComponent<StateDiffViewer> RenderDiffViewer(long from, long to)
    {
        return Render<StateDiffViewer>(p => p
            .Add(c => c.TenantId, "test-tenant")
            .Add(c => c.Domain, "counter")
            .Add(c => c.AggregateId, "agg-001")
            .Add(c => c.FromSequence, from)
            .Add(c => c.ToSequence, to));
    }

    private void SetupDiffMocks(AggregateStateDiff diff, string fromStateJson, string toStateJson)
    {
        _ = _mockApiClient.GetAggregateStateDiffAsync(
            "test-tenant", "counter", "agg-001", diff.FromSequence, diff.ToSequence, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateStateDiff?>(diff));
        _ = _mockApiClient.GetAggregateStateAtPositionAsync(
            "test-tenant", "counter", "agg-001", diff.FromSequence, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateStateSnapshot?>(
                new AggregateStateSnapshot("test-tenant", "counter", "agg-001", diff.FromSequence,
                    DateTimeOffset.UtcNow, fromStateJson)));
        _ = _mockApiClient.GetAggregateStateAtPositionAsync(
            "test-tenant", "counter", "agg-001", diff.ToSequence, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateStateSnapshot?>(
                new AggregateStateSnapshot("test-tenant", "counter", "agg-001", diff.ToSequence,
                    DateTimeOffset.UtcNow, toStateJson)));
    }
}
