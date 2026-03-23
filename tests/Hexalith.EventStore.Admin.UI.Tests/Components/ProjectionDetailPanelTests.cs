using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.UI.Components;
using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.SignalR;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// bUnit tests for the ProjectionDetailPanel component.
/// </summary>
public class ProjectionDetailPanelTests : AdminUITestContext
{
    private readonly AdminProjectionApiClient _mockApiClient;

    public ProjectionDetailPanelTests()
    {
        _mockApiClient = Substitute.For<AdminProjectionApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminProjectionApiClient>.Instance);
        Services.AddScoped(_ => _mockApiClient);
        Services.AddScoped<DashboardRefreshService>();
        TestSignalRClient testClient = new();
        Services.AddSingleton(testClient);
        Services.AddSingleton(testClient.Inner);
    }

    [Fact]
    public void DetailPanel_RendersProjectionMetrics()
    {
        // Arrange
        ProjectionDetail detail = CreateDetail();
        _ = _mockApiClient.GetProjectionDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(detail));

        // Act
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(
            parameters => parameters
                .Add(p => p.TenantId, "tenant-1")
                .Add(p => p.ProjectionName, "counter-projection"));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("counter-projection"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Projection: counter-projection (tenant-1)");
        markup.ShouldContain("Lag");
        markup.ShouldContain("Throughput");
        markup.ShouldContain("Errors");
    }

    [Fact]
    public void DetailPanel_RendersSubscribedEventTypes()
    {
        // Arrange
        ProjectionDetail detail = CreateDetail();
        _ = _mockApiClient.GetProjectionDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(detail));

        // Act
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(
            parameters => parameters
                .Add(p => p.TenantId, "tenant-1")
                .Add(p => p.ProjectionName, "counter-projection"));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("CounterIncremented"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Subscribed Event Types (2)");
        markup.ShouldContain("CounterIncremented");
        markup.ShouldContain("CounterReset");
    }

    [Fact]
    public void DetailPanel_RendersErrorList()
    {
        // Arrange
        ProjectionDetail detail = CreateDetailWithErrors();
        _ = _mockApiClient.GetProjectionDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(detail));

        // Act
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(
            parameters => parameters
                .Add(p => p.TenantId, "tenant-1")
                .Add(p => p.ProjectionName, "error-projection"));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Deserialization failed"), TimeSpan.FromSeconds(5));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Position");
        markup.ShouldContain("Timestamp");
        markup.ShouldContain("Message");
        markup.ShouldContain("Event Type");
        markup.ShouldContain("Deserialization failed");
    }

    [Fact]
    public void DetailPanel_ShowsNoErrorsRecorded_WhenEmpty()
    {
        // Arrange
        ProjectionDetail detail = CreateDetail();
        _ = _mockApiClient.GetProjectionDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(detail));

        // Act
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(
            parameters => parameters
                .Add(p => p.TenantId, "tenant-1")
                .Add(p => p.ProjectionName, "counter-projection"));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No errors recorded"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No errors recorded");
    }

    [Fact]
    public void DetailPanel_PauseButton_VisibleForRunningProjection()
    {
        // Arrange
        ProjectionDetail detail = CreateDetail();
        _ = _mockApiClient.GetProjectionDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(detail));

        // Act
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(
            parameters => parameters
                .Add(p => p.TenantId, "tenant-1")
                .Add(p => p.ProjectionName, "counter-projection"));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Pause"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Pause");
        cut.Markup.ShouldNotContain("Resume");
    }

    [Fact]
    public void DetailPanel_ResumeButton_VisibleForPausedProjection()
    {
        // Arrange
        ProjectionDetail detail = CreatePausedDetail();
        _ = _mockApiClient.GetProjectionDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(detail));

        // Act
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(
            parameters => parameters
                .Add(p => p.TenantId, "tenant-1")
                .Add(p => p.ProjectionName, "paused-projection"));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Resume"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Resume");
    }

    [Fact]
    public void DetailPanel_RendersConfigurationJson()
    {
        // Arrange
        ProjectionDetail detail = CreateDetail();
        _ = _mockApiClient.GetProjectionDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(detail));

        // Act
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(
            parameters => parameters
                .Add(p => p.TenantId, "tenant-1")
                .Add(p => p.ProjectionName, "counter-projection"));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Configuration"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Configuration");
    }

    [Fact]
    public void DetailPanel_ShowsNotFound_WhenNull()
    {
        // Arrange
        _ = _mockApiClient.GetProjectionDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(null));

        // Act
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(
            parameters => parameters
                .Add(p => p.TenantId, "tenant-1")
                .Add(p => p.ProjectionName, "nonexistent"));
        cut.WaitForAssertion(() =>
            cut.Markup.ShouldContain("Projection not found"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Projection not found");
    }

    [Fact]
    public void DetailPanel_ErrorTable_HasAriaLabel()
    {
        // Arrange
        ProjectionDetail detail = CreateDetailWithErrors();
        _ = _mockApiClient.GetProjectionDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(detail));

        // Act
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(
            parameters => parameters
                .Add(p => p.TenantId, "tenant-1")
                .Add(p => p.ProjectionName, "error-projection"));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Projection errors for"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Projection errors for error-projection");
    }

    [Fact]
    public void DetailPanel_ShowAllErrors_WhenMoreThan20()
    {
        // Arrange
        List<ProjectionError> errors = [];
        for (int i = 0; i < 25; i++)
        {
            errors.Add(new ProjectionError(i, DateTimeOffset.UtcNow.AddMinutes(-i), $"Error {i}", "SomeEvent"));
        }

        ProjectionDetail detail = new(
            "many-errors", "tenant-1", ProjectionStatusType.Error, 500, 0.0, 25,
            500, DateTimeOffset.UtcNow.AddMinutes(-1),
            errors, "{}", ["SomeEvent"]);
        _ = _mockApiClient.GetProjectionDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(detail));

        // Act
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(
            parameters => parameters
                .Add(p => p.TenantId, "tenant-1")
                .Add(p => p.ProjectionName, "many-errors"));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Show all 25 errors"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Show all 25 errors");
    }

    [Fact]
    public void DetailPanel_BackToList_Button_Exists()
    {
        // Arrange
        ProjectionDetail detail = CreateDetail();
        _ = _mockApiClient.GetProjectionDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(detail));

        // Act
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(
            parameters => parameters
                .Add(p => p.TenantId, "tenant-1")
                .Add(p => p.ProjectionName, "counter-projection"));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Back to List"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Back to List");
    }

    private static ProjectionDetail CreateDetail() => new(
        "counter-projection",
        "tenant-1",
        ProjectionStatusType.Running,
        10,
        5.2,
        0,
        1000,
        DateTimeOffset.UtcNow.AddMinutes(-1),
        [],
        """{"batchSize": 100}""",
        ["CounterIncremented", "CounterReset"]);

    private static ProjectionDetail CreatePausedDetail() => new(
        "paused-projection",
        "tenant-1",
        ProjectionStatusType.Paused,
        200,
        0.0,
        0,
        800,
        DateTimeOffset.UtcNow.AddMinutes(-10),
        [],
        """{"batchSize": 50}""",
        ["OrderPlaced"]);

    private static ProjectionDetail CreateDetailWithErrors() => new(
        "error-projection",
        "tenant-1",
        ProjectionStatusType.Error,
        500,
        0.0,
        2,
        500,
        DateTimeOffset.UtcNow.AddMinutes(-30),
        [
            new(450, DateTimeOffset.UtcNow.AddMinutes(-30), "Deserialization failed", "CounterIncremented"),
            new(445, DateTimeOffset.UtcNow.AddMinutes(-35), "Timeout processing event", null),
        ],
        """{"batchSize": 100}""",
        ["CounterIncremented"]);
}

/// <summary>
/// Tests that projection controls are hidden when user lacks Operator role.
/// Merge-blocking test (spec task 6.8, AC: 7, 8, 9).
/// </summary>
public class ProjectionDetailPanelReadOnlyTests : AdminUITestContext
{
    private readonly AdminProjectionApiClient _mockApiClient;

    public ProjectionDetailPanelReadOnlyTests()
    {
        // Override auth state with ReadOnly role
        AuthenticationStateProvider readOnlyAuth = Substitute.For<AuthenticationStateProvider>();
        System.Security.Claims.ClaimsPrincipal readOnlyUser = new(new System.Security.Claims.ClaimsIdentity(
        [
            new System.Security.Claims.Claim(AdminClaimTypes.Role, "ReadOnly"),
        ], "TestAuth"));
        _ = readOnlyAuth.GetAuthenticationStateAsync()
            .Returns(Task.FromResult(new AuthenticationState(readOnlyUser)));
        Services.AddSingleton(readOnlyAuth);
        Services.AddScoped<AdminUserContext>();
        Services.AddCascadingValue(sp =>
        {
            AuthenticationStateProvider asp = sp.GetRequiredService<AuthenticationStateProvider>();
            return asp.GetAuthenticationStateAsync();
        });

        _mockApiClient = Substitute.For<AdminProjectionApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminProjectionApiClient>.Instance);
        Services.AddScoped(_ => _mockApiClient);
        Services.AddScoped<DashboardRefreshService>();
        TestSignalRClient testClient = new();
        Services.AddSingleton(testClient);
        Services.AddSingleton(testClient.Inner);
    }

    [Fact]
    public void DetailPanel_ControlsHidden_WhenUserLacksOperatorRole()
    {
        // Arrange
        ProjectionDetail detail = new(
            "counter-projection", "tenant-1", ProjectionStatusType.Running,
            10, 5.2, 0, 1000, DateTimeOffset.UtcNow.AddMinutes(-1),
            [], """{"batchSize": 100}""", ["CounterIncremented"]);
        _ = _mockApiClient.GetProjectionDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(detail));

        // Act
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(
            parameters => parameters
                .Add(p => p.TenantId, "tenant-1")
                .Add(p => p.ProjectionName, "counter-projection"));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("counter-projection"), TimeSpan.FromSeconds(5));

        // Assert — controls should NOT be visible for ReadOnly user
        string markup = cut.Markup;
        markup.ShouldNotContain("Pause");
        markup.ShouldNotContain("Reset");
        markup.ShouldNotContain("Replay");
    }
}
