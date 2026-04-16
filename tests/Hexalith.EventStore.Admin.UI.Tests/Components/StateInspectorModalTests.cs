using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.UI.Components;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// bUnit tests for the StateInspectorModal component.
/// </summary>
public class StateInspectorModalTests : AdminUITestContext {
    private readonly AdminStreamApiClient _mockApiClient;

    public StateInspectorModalTests() {
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
    public void StateInspectorModal_RendersWithPreFilledSequence() {
        // Arrange & Act
        IRenderedComponent<StateInspectorModal> cut = RenderInspector(42L);

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("State Inspector");
        markup.ShouldContain("Sequence Number");
    }

    [Fact]
    public void StateInspectorModal_FetchesStateOnSubmit() {
        // Arrange
        AggregateStateSnapshot snapshot = new(
            "test-tenant", "counter", "agg-001", 42,
            DateTimeOffset.UtcNow, """{"count": 5}""");
        _ = _mockApiClient.GetAggregateStateAtPositionAsync(
            "test-tenant", "counter", "agg-001", 42, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateStateSnapshot?>(snapshot));

        IRenderedComponent<StateInspectorModal> cut = RenderInspector(42L);

        // Act — click Inspect button via markup
        AngleSharp.Dom.IElement inspectBtn = cut.Find("fluent-button[appearance='primary']");
        inspectBtn.Click();

        // Assert
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("State at Sequence #42"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void StateInspectorModal_ShowsNoStateAvailable_OnNullResponse() {
        // Arrange
        _ = _mockApiClient.GetAggregateStateAtPositionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateStateSnapshot?>(null));

        IRenderedComponent<StateInspectorModal> cut = RenderInspector(10L);

        // Act — click Inspect button
        AngleSharp.Dom.IElement inspectBtn = cut.Find("fluent-button[appearance='primary']");
        inspectBtn.Click();

        // Assert
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No state available at this position"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void StateInspectorModal_RendersToggleForTimestampMode() {
        // Arrange & Act
        IRenderedComponent<StateInspectorModal> cut = RenderInspector(1L);

        // Assert — initially shows sequence mode with toggle available
        string markup = cut.Markup;
        markup.ShouldContain("Sequence Number");
        markup.ShouldContain("By Timestamp");
        markup.ShouldContain("fluent-switch");
    }

    [Fact]
    public void StateInspectorModal_StaysOpenAfterSubmit() {
        // Arrange
        AggregateStateSnapshot snapshot = new(
            "test-tenant", "counter", "agg-001", 5,
            DateTimeOffset.UtcNow, """{"count": 3}""");
        _ = _mockApiClient.GetAggregateStateAtPositionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AggregateStateSnapshot?>(snapshot));

        IRenderedComponent<StateInspectorModal> cut = RenderInspector(5L);

        // Act — click Inspect
        AngleSharp.Dom.IElement inspectBtn = cut.Find("fluent-button[appearance='primary']");
        inspectBtn.Click();

        // Assert — modal stays open with result and title still visible
        cut.WaitForAssertion(() => {
            cut.Markup.ShouldContain("State at Sequence");
            cut.Markup.ShouldContain("State Inspector");
        }, TimeSpan.FromSeconds(5));
    }

    private IRenderedComponent<StateInspectorModal> RenderInspector(long? seq) => Render<StateInspectorModal>(p => p
                                                                                           .Add(c => c.TenantId, "test-tenant")
                                                                                           .Add(c => c.Domain, "counter")
                                                                                           .Add(c => c.AggregateId, "agg-001")
                                                                                           .Add(c => c.InitialSequenceNumber, seq));
}
