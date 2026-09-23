using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.UI.Components;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;
using Hexalith.EventStore.Admin.UI.Tests.Services;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// bUnit tests for the ProjectionDetailPanel component.
/// </summary>
public class ProjectionDetailPanelTests : AdminUITestContext {
    private readonly AdminProjectionApiClient _mockApiClient;

    public ProjectionDetailPanelTests() {
        _mockApiClient = Substitute.For<AdminProjectionApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminProjectionApiClient>.Instance);
        _ = Services.AddScoped(_ => _mockApiClient);
        _ = Services.AddScoped<DashboardRefreshService>();
        TestSignalRClient testClient = new();
        _ = Services.AddSingleton(testClient);
        _ = Services.AddSingleton(testClient.Inner);
    }

    [Fact]
    public void DetailPanel_RendersProjectionMetrics() {
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
    public void DetailPanel_RendersSubscribedEventTypes() {
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
    public void DetailPanel_RendersErrorList() {
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
    public void DetailPanel_ShowsNoErrorsRecorded_WhenEmpty() {
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
    public void DetailPanel_PauseButton_VisibleForRunningProjection() {
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
    public void DetailPanel_ResumeButton_VisibleForPausedProjection() {
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
    public void DetailPanel_RendersConfigurationJson() {
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
    public void DetailPanel_ShowsNotFound_WhenNull() {
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
    public void DetailPanel_ErrorTable_HasAriaLabel() {
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
    public void DetailPanel_ShowAllErrors_WhenMoreThan20() {
        // Arrange
        List<ProjectionError> errors = [];
        for (int i = 0; i < 25; i++) {
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
    public void DetailPanel_BackToList_Button_Exists() {
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

    [Theory]
    [InlineData("#projection-reset-button", "Clear projection state", "projection-reset-button")]
    [InlineData("#projection-replay-button", "Reprocess events", "projection-replay-button")]
    public async Task DestructiveDialog_CancelRendersSafetyFactsDoesNoWorkAndRestoresExactInitiator(
        string buttonSelector,
        string impactFragment,
        string expectedFocusId)
    {
        _ = _mockApiClient.GetProjectionDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(CreateDetail()));
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(parameters => parameters
            .Add(item => item.TenantId, "tenant-1")
            .Add(item => item.ProjectionName, "counter-projection"));
        cut.WaitForAssertion(() => cut.Find(buttonSelector), TimeSpan.FromSeconds(5));

        await cut.Find(buttonSelector).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Confirmation safety facts"), TimeSpan.FromSeconds(5));

        cut.Find("[data-confirmation-fact='target']").TextContent
            .ShouldBe("Projection 'counter-projection' in tenant 'tenant-1'");
        cut.Find("[data-confirmation-fact='impact']").TextContent.ShouldContain(impactFragment);
        cut.Find("[data-confirmation-fact='permission']").TextContent.ShouldBe("Operator");

        await cut.FindAll("fluent-button")
            .Single(button => button.TextContent.Trim() == "Cancel")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _ = _mockApiClient.DidNotReceive().ResetProjectionAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<long?>(),
            Arg.Any<CancellationToken>());
        _ = _mockApiClient.DidNotReceive().ReplayProjectionAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
        JSInterop.Invocations.Last(invocation => invocation.Identifier == "hexalithAdmin.focusElementById")
            .Arguments[0].ShouldBe(expectedFocusId);
    }

    [Fact]
    public async Task ReplayDialog_LocalValidationPerformsNoWorkUsesBoundedStateAndRestoresFocus()
    {
        _ = _mockApiClient.GetProjectionDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(CreateDetail()));
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(parameters => parameters
            .Add(item => item.TenantId, "tenant-1")
            .Add(item => item.ProjectionName, "counter-projection"));
        cut.WaitForAssertion(() => cut.Find("#projection-replay-button"), TimeSpan.FromSeconds(5));

        await cut.Find("#projection-replay-button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        SetPrivateField(cut.Instance, "_replayFromPosition", 20L);
        SetPrivateField(cut.Instance, "_replayToPosition", 20L);
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "ConfirmReplayAsync"));

        _ = _mockApiClient.DidNotReceive().ReplayProjectionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
        GetPrivateField<string>(cut.Instance, "_replayValidationError")
            .ShouldBe("From Position must be strictly less than To Position.");
        Services.GetRequiredService<TestToastService>().LastOptions!.Message.ShouldBe(
            "From Position must be strictly less than To Position.");
        cut.FindAll("fluent-dialog[aria-label='Replay projection']").ShouldBeEmpty();
        JSInterop.Invocations.Last(invocation => invocation.Identifier == "hexalithAdmin.focusElementById")
            .Arguments[0].ShouldBe("projection-replay-button");
    }

    [Theory]
    [InlineData("reset", "#projection-reset-button", "_resetFromPosition", "From Position must be non-negative.", "projection-reset-button")]
    [InlineData("replay", "#projection-replay-button", "_replayFromPosition", "Replay positions must be non-negative.", "projection-replay-button")]
    public async Task ProjectionDialog_NegativePositionPerformsNoWorkAndRestoresFocus(
        string action,
        string buttonSelector,
        string fieldName,
        string expectedMessage,
        string expectedFocusId)
    {
        _ = _mockApiClient.GetProjectionDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(CreateDetail()));
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(parameters => parameters
            .Add(item => item.TenantId, "tenant-1")
            .Add(item => item.ProjectionName, "counter-projection"));
        cut.WaitForAssertion(() => cut.Find(buttonSelector), TimeSpan.FromSeconds(5));
        await cut.Find(buttonSelector).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        SetPrivateField(cut.Instance, fieldName, -1L);

        await cut.InvokeAsync(() => InvokePrivateAsync(
            cut.Instance,
            action == "reset" ? "ConfirmResetAsync" : "ConfirmReplayAsync"));

        _ = _mockApiClient.DidNotReceive().ResetProjectionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>());
        _ = _mockApiClient.DidNotReceive().ReplayProjectionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
        Services.GetRequiredService<TestToastService>().LastOptions!.Message.ShouldBe(expectedMessage);
        JSInterop.Invocations.Last(invocation => invocation.Identifier == "hexalithAdmin.focusElementById")
            .Arguments[0].ShouldBe(expectedFocusId);
    }

    [Theory]
    [InlineData("reset", "#projection-reset-button", "projection-reset-button", true)]
    [InlineData("reset", "#projection-reset-button", "projection-reset-button", false)]
    [InlineData("replay", "#projection-replay-button", "projection-replay-button", true)]
    [InlineData("replay", "#projection-replay-button", "projection-replay-button", false)]
    public async Task ProjectionDialog_DenialUsesSupportSafeCopyAndRestoresFocusWithoutClaimingCompletion(
        string action,
        string buttonSelector,
        string expectedFocusId,
        bool forbidden)
    {
        Exception denial = forbidden
            ? new ForbiddenAccessException("hidden projection exists; bearer secret-value")
            : new UnauthorizedAccessException("hidden projection exists; bearer secret-value");
        _ = _mockApiClient.GetProjectionDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(CreateDetail()));
        _ = _mockApiClient.ResetProjectionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminOperationResult?>(denial));
        _ = _mockApiClient.ReplayProjectionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminOperationResult?>(denial));
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(parameters => parameters
            .Add(item => item.TenantId, "tenant-1")
            .Add(item => item.ProjectionName, "counter-projection"));
        cut.WaitForAssertion(() => cut.Find(buttonSelector), TimeSpan.FromSeconds(5));

        await cut.Find(buttonSelector).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await cut.InvokeAsync(() => InvokePrivateAsync(
            cut.Instance,
            action == "reset" ? "ConfirmResetAsync" : "ConfirmReplayAsync"));

        TestToastService toast = Services.GetRequiredService<TestToastService>();
        string message = toast.LastOptions?.Message?.ToString() ?? string.Empty;
        message.ShouldBe(forbidden ? "Forbidden — insufficient permissions" : "Unauthorized");
        message.ShouldNotContain("hidden projection");
        message.ShouldNotContain("secret-value");
        message.ShouldNotContain("completed", Case.Insensitive);
        if (action == "reset")
        {
            _ = await _mockApiClient.Received(1).ResetProjectionAsync(
                "tenant-1", "counter-projection", null, Arg.Any<CancellationToken>());
        }
        else
        {
            _ = await _mockApiClient.Received(1).ReplayProjectionAsync(
                "tenant-1", "counter-projection", Arg.Is<long>(value => value == 0), Arg.Any<long>(), Arg.Any<CancellationToken>());
        }

        JSInterop.Invocations.Last(invocation => invocation.Identifier == "hexalithAdmin.focusElementById")
            .Arguments[0].ShouldBe(expectedFocusId);
        GetPrivateField<bool>(cut.Instance, "_isOperating").ShouldBeFalse();
        GetPrivateField<string?>(cut.Instance, "_operatingAction").ShouldBeNull();
        cut.Find(buttonSelector).HasAttribute("disabled").ShouldBeFalse();
        cut.FindAll(action == "reset"
            ? "fluent-dialog[aria-label='Reset projection']"
            : "fluent-dialog[aria-label='Replay projection']").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("reset", "#projection-reset-button", "projection-reset-button")]
    [InlineData("replay", "#projection-replay-button", "projection-replay-button")]
    public async Task ProjectionDialog_UnexpectedFailureUsesFixedCopyClosesAndRestoresFocus(
        string action,
        string buttonSelector,
        string expectedFocusId)
    {
        string secret = Guid.NewGuid().ToString("N");
        string unsafeMessage = "bearer " + secret + " at redis://private";
        _ = _mockApiClient.GetProjectionDetailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(CreateDetail()));
        _ = _mockApiClient.ResetProjectionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminOperationResult?>(new Exception(unsafeMessage)));
        _ = _mockApiClient.ReplayProjectionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminOperationResult?>(new Exception(unsafeMessage)));
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(parameters => parameters
            .Add(item => item.TenantId, "tenant-1")
            .Add(item => item.ProjectionName, "counter-projection"));
        cut.WaitForAssertion(() => cut.Find(buttonSelector), TimeSpan.FromSeconds(5));
        await cut.Find(buttonSelector).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await cut.InvokeAsync(() => InvokePrivateAsync(
            cut.Instance,
            action == "reset" ? "ConfirmResetAsync" : "ConfirmReplayAsync"));

        string message = Services.GetRequiredService<TestToastService>().LastOptions!.Message!.ToString()!;
        message.ShouldBe("Projection operation failed. Refresh status before deciding whether to retry.");
        message.ShouldNotContain(secret);
        message.ShouldNotContain("redis://private");
        cut.FindAll(action == "reset"
            ? "fluent-dialog[aria-label='Reset projection']"
            : "fluent-dialog[aria-label='Replay projection']").ShouldBeEmpty();
        JSInterop.Invocations.Last(invocation => invocation.Identifier == "hexalithAdmin.focusElementById")
            .Arguments[0].ShouldBe(expectedFocusId);
    }

    [Fact]
    public async Task ExecuteOperationAsync_UnexpectedFailureUsesFixedCopy()
    {
        string secret = Guid.NewGuid().ToString("N");
        _ = _mockApiClient.GetProjectionDetailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(CreateDetail()));
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(parameters => parameters
            .Add(item => item.TenantId, "tenant-1")
            .Add(item => item.ProjectionName, "counter-projection"));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Pause"), TimeSpan.FromSeconds(5));

        await cut.InvokeAsync(() => InvokeExecuteOperationAsync(
            cut.Instance,
            () => throw new Exception("bearer " + secret + " at redis://private")));

        string message = Services.GetRequiredService<TestToastService>().LastOptions!.Message!.ToString()!;
        message.ShouldBe("Projection operation failed. Refresh status before deciding whether to retry.");
        message.ShouldNotContain(secret);
        message.ShouldNotContain("redis://private");
        message.ShouldNotContain("completed", Case.Insensitive);
    }

    [Theory]
    [InlineData("reset", "#projection-reset-button", "Reset request accepted")]
    [InlineData("replay", "#projection-replay-button", "Replay request accepted")]
    public async Task AsyncProjectionSuccess_UsesAcceptedNotCompletedWording(
        string action,
        string buttonSelector,
        string acceptedFragment)
    {
        _ = _mockApiClient.GetProjectionDetailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<ProjectionDetail?>(CreateDetail()),
                Task.FromResult<ProjectionDetail?>(CreatePausedDetail()));
        _ = _mockApiClient.ResetProjectionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "reset-op", "Accepted", null));
        _ = _mockApiClient.ReplayProjectionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "replay-op", "Accepted", null));
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(parameters => parameters
            .Add(item => item.TenantId, "tenant-1")
            .Add(item => item.ProjectionName, "counter-projection"));
        cut.WaitForAssertion(() => cut.Find(buttonSelector), TimeSpan.FromSeconds(5));
        await cut.Find(buttonSelector).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await cut.InvokeAsync(() => InvokePrivateAsync(
            cut.Instance,
            action == "reset" ? "ConfirmResetAsync" : "ConfirmReplayAsync"));

        TestToastService toast = Services.GetRequiredService<TestToastService>();
        string message = toast.CapturedOptions
            .Select(option => option.Message?.ToString() ?? string.Empty)
            .First(value => value.Contains(acceptedFragment, StringComparison.Ordinal));
        message.ShouldContain(acceptedFragment);
        message.ShouldNotContain("completed", Case.Insensitive);
        message.ShouldNotContain("successfully", Case.Insensitive);
    }

    [Theory]
    [InlineData("reset", "#projection-reset-button", "Reset request accepted", "Reset request was accepted, but status refresh failed")]
    [InlineData("replay", "#projection-replay-button", "Replay request accepted", "Replay request was accepted, but status refresh failed")]
    public async Task AcceptedProjectionRequest_PostAcceptanceRefreshFailureNeverClaimsMutationFailure(
        string action,
        string buttonSelector,
        string acceptedFragment,
        string refreshFailureFragment)
    {
        _ = _mockApiClient.GetProjectionDetailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<ProjectionDetail?>(CreateDetail()),
                Task.FromException<ProjectionDetail?>(new InvalidOperationException("refresh failed")));
        _ = _mockApiClient.ResetProjectionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "reset-op", "Accepted", null));
        _ = _mockApiClient.ReplayProjectionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "replay-op", "Accepted", null));
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(parameters => parameters
            .Add(item => item.TenantId, "tenant-1")
            .Add(item => item.ProjectionName, "counter-projection"));
        cut.WaitForAssertion(() => cut.Find(buttonSelector), TimeSpan.FromSeconds(5));
        await cut.Find(buttonSelector).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await cut.InvokeAsync(() => InvokePrivateAsync(
            cut.Instance,
            action == "reset" ? "ConfirmResetAsync" : "ConfirmReplayAsync"));

        string[] messages = Services.GetRequiredService<TestToastService>().CapturedOptions
            .Select(option => option.Message?.ToString() ?? string.Empty)
            .ToArray();
        messages.ShouldContain(message => message.Contains(acceptedFragment, StringComparison.Ordinal));
        messages.ShouldContain(message => message.Contains(refreshFailureFragment, StringComparison.Ordinal));
        messages.ShouldNotContain(message => message.Contains("Projection operation failed", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("reset", "#projection-reset-button")]
    [InlineData("replay", "#projection-replay-button")]
    public async Task UnsafeConfirmationTarget_IsNeverSubmitted(string action, string buttonSelector)
    {
        ProjectionDetail unsafeDetail = CreateDetail("Bearer " + "secret-token");
        _ = _mockApiClient.GetProjectionDetailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(unsafeDetail));
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(parameters => parameters
            .Add(item => item.TenantId, "tenant-1")
            .Add(item => item.ProjectionName, unsafeDetail.Name));
        cut.WaitForAssertion(() => cut.Find(buttonSelector), TimeSpan.FromSeconds(5));
        await cut.Find(buttonSelector).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await cut.InvokeAsync(() => InvokePrivateAsync(
            cut.Instance,
            action == "reset" ? "ConfirmResetAsync" : "ConfirmReplayAsync"));

        _ = _mockApiClient.DidNotReceive().ResetProjectionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>());
        _ = _mockApiClient.DidNotReceive().ReplayProjectionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
        Services.GetRequiredService<TestToastService>().LastOptions!.Message!.ToString()
            .ShouldBe("The selected target cannot be confirmed because its identifier is not support-safe.");
    }

    [Theory]
    [InlineData("reset", "#projection-reset-button", "projection-reset-button", "Failed to reset projection", false)]
    [InlineData("reset", "#projection-reset-button", "projection-reset-button", "Failed to reset projection", true)]
    [InlineData("replay", "#projection-replay-button", "projection-replay-button", "Failed to replay projection", false)]
    [InlineData("replay", "#projection-replay-button", "projection-replay-button", "Failed to replay projection", true)]
    [InlineData("reset", "#projection-reset-button", "projection-reset-button", "Projection operation failed. Refresh status before deciding whether to retry.", null)]
    [InlineData("replay", "#projection-replay-button", "projection-replay-button", "Projection operation failed. Refresh status before deciding whether to retry.", null)]
    public async Task ProjectionDialog_RejectedOrFailedRequestRestoresFocusToEnabledInitiator(
        string action,
        string buttonSelector,
        string expectedFocusId,
        string expectedMessage,
        bool? nullResult)
    {
        Task<AdminOperationResult?> outcome = nullResult switch
        {
            true => Task.FromResult<AdminOperationResult?>(null),
            false => Task.FromResult<AdminOperationResult?>(new AdminOperationResult(false, "op-rejected", "Rejected", null)),
            null => Task.FromException<AdminOperationResult?>(new Exception("unexpected")),
        };
        _ = _mockApiClient.GetProjectionDetailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(CreateDetail()));
        _ = _mockApiClient.ResetProjectionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(outcome);
        _ = _mockApiClient.ReplayProjectionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(outcome);
        IRenderedComponent<ProjectionDetailPanel>? cut = null;
        bool? initiatorDisabledWhenFocused = null;
        _ = JSInterop.SetupVoid(
                "hexalithAdmin.focusElementById",
                invocation =>
                {
                    initiatorDisabledWhenFocused = cut!.Find(buttonSelector).HasAttribute("disabled");
                    return true;
                })
            .SetVoidResult();
        cut = Render<ProjectionDetailPanel>(parameters => parameters
            .Add(item => item.TenantId, "tenant-1")
            .Add(item => item.ProjectionName, "counter-projection"));
        cut.WaitForAssertion(() => cut.Find(buttonSelector), TimeSpan.FromSeconds(5));
        await cut.Find(buttonSelector).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await cut.InvokeAsync(() => InvokePrivateAsync(
            cut.Instance,
            action == "reset" ? "ConfirmResetAsync" : "ConfirmReplayAsync"));

        string[] messages = Services.GetRequiredService<TestToastService>().CapturedOptions
            .Select(option => option.Message?.ToString() ?? string.Empty)
            .ToArray();
        messages.ShouldContain(expectedMessage);
        messages.ShouldNotContain(message => message.Contains("accepted", StringComparison.OrdinalIgnoreCase));
        cut.FindAll(action == "reset"
            ? "fluent-dialog[aria-label='Reset projection']"
            : "fluent-dialog[aria-label='Replay projection']").ShouldBeEmpty();
        JSInterop.Invocations.Last(invocation => invocation.Identifier == "hexalithAdmin.focusElementById")
            .Arguments[0].ShouldBe(expectedFocusId);
        initiatorDisabledWhenFocused.ShouldBe(false);
        GetPrivateField<bool>(cut.Instance, "_isOperating").ShouldBeFalse();
    }

    [Theory]
    [InlineData("reset", "#projection-reset-button")]
    [InlineData("replay", "#projection-replay-button")]
    public async Task ProjectionDialog_ServiceUnavailableReEnablesControlsWithoutClaimingAcceptance(
        string action,
        string buttonSelector)
    {
        _ = _mockApiClient.GetProjectionDetailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(CreateDetail()));
        _ = _mockApiClient.ResetProjectionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminOperationResult?>(new ServiceUnavailableException("down")));
        _ = _mockApiClient.ReplayProjectionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminOperationResult?>(new ServiceUnavailableException("down")));
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(parameters => parameters
            .Add(item => item.TenantId, "tenant-1")
            .Add(item => item.ProjectionName, "counter-projection"));
        cut.WaitForAssertion(() => cut.Find(buttonSelector), TimeSpan.FromSeconds(5));
        await cut.Find(buttonSelector).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await cut.InvokeAsync(() => InvokePrivateAsync(
            cut.Instance,
            action == "reset" ? "ConfirmResetAsync" : "ConfirmReplayAsync"));
        cut.Render();

        string[] messages = Services.GetRequiredService<TestToastService>().CapturedOptions
            .Select(option => option.Message?.ToString() ?? string.Empty)
            .ToArray();
        messages.ShouldContain("Service unavailable — try again later");
        messages.ShouldNotContain(message => message.Contains("accepted", StringComparison.OrdinalIgnoreCase));
        GetPrivateField<bool>(cut.Instance, "_isOperating").ShouldBeFalse();
        GetPrivateField<string?>(cut.Instance, "_operatingAction").ShouldBeNull();
        cut.Find(buttonSelector).HasAttribute("disabled").ShouldBeFalse();
    }

    [Theory]
    [InlineData("reset", "#projection-reset-button")]
    [InlineData("replay", "#projection-replay-button")]
    public async Task ProjectionDialog_FactsNameTheSubmittedProjection(string action, string buttonSelector)
    {
        _ = _mockApiClient.GetProjectionDetailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ProjectionDetail?>(CreateDetail("server-renamed-projection")));
        _ = _mockApiClient.ResetProjectionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "reset-op", "Accepted", null));
        _ = _mockApiClient.ReplayProjectionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "replay-op", "Accepted", null));
        IRenderedComponent<ProjectionDetailPanel> cut = Render<ProjectionDetailPanel>(parameters => parameters
            .Add(item => item.TenantId, "tenant-1")
            .Add(item => item.ProjectionName, "counter-projection"));
        cut.WaitForAssertion(() => cut.Find(buttonSelector), TimeSpan.FromSeconds(5));
        await cut.Find(buttonSelector).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        string target = cut.Find("[data-confirmation-fact='target']").TextContent;
        target.ShouldContain("counter-projection");
        target.ShouldNotContain("server-renamed-projection");

        await cut.InvokeAsync(() => InvokePrivateAsync(
            cut.Instance,
            action == "reset" ? "ConfirmResetAsync" : "ConfirmReplayAsync"));

        if (action == "reset")
        {
            _ = await _mockApiClient.Received(1).ResetProjectionAsync(
                "tenant-1", "counter-projection", Arg.Any<long?>(), Arg.Any<CancellationToken>());
        }
        else
        {
            _ = await _mockApiClient.Received(1).ReplayProjectionAsync(
                "tenant-1", "counter-projection", Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
        }
    }

    private static async Task InvokePrivateAsync(object instance, string methodName)
    {
        System.Reflection.MethodInfo method = instance.GetType().GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' not found.");
        await ((Task)method.Invoke(instance, null)!).ConfigureAwait(false);
    }

    private static async Task InvokeExecuteOperationAsync(object instance, Func<Task> operation)
    {
        System.Reflection.MethodInfo method = instance.GetType().GetMethod(
            "ExecuteOperationAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ExecuteOperationAsync was not found.");
        await ((Task)method.Invoke(instance, ["pause", operation])!).ConfigureAwait(false);
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
        => instance.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(instance, value);

    private static T GetPrivateField<T>(object instance, string fieldName)
        => (T)instance.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(instance)!;

    private static ProjectionDetail CreateDetail(string name = "counter-projection") => new(
        name,
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
public class ProjectionDetailPanelReadOnlyTests : AdminUITestContext {
    private readonly AdminProjectionApiClient _mockApiClient;

    public ProjectionDetailPanelReadOnlyTests() {
        // Override auth state with ReadOnly role
        AuthenticationStateProvider readOnlyAuth = Substitute.For<AuthenticationStateProvider>();
        System.Security.Claims.ClaimsPrincipal readOnlyUser = new(new System.Security.Claims.ClaimsIdentity(
        [
            new System.Security.Claims.Claim(AdminClaimTypes.Role, "ReadOnly"),
        ], "TestAuth"));
        _ = readOnlyAuth.GetAuthenticationStateAsync()
            .Returns(Task.FromResult(new AuthenticationState(readOnlyUser)));
        _ = Services.AddSingleton(readOnlyAuth);
        _ = Services.AddScoped<AdminUserContext>();
        _ = Services.AddCascadingValue(sp => {
            AuthenticationStateProvider asp = sp.GetRequiredService<AuthenticationStateProvider>();
            return asp.GetAuthenticationStateAsync();
        });

        _mockApiClient = Substitute.For<AdminProjectionApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminProjectionApiClient>.Instance);
        _ = Services.AddScoped(_ => _mockApiClient);
        _ = Services.AddScoped<DashboardRefreshService>();
        TestSignalRClient testClient = new();
        _ = Services.AddSingleton(testClient);
        _ = Services.AddSingleton(testClient.Inner);
    }

    [Fact]
    public void DetailPanel_ControlsHidden_WhenUserLacksOperatorRole() {
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
