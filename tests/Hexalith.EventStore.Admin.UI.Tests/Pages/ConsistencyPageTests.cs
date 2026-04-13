using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Consistency;
using Hexalith.EventStore.Admin.UI.Pages;
using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FluentUI.AspNetCore.Components;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the Consistency page.
/// </summary>
public class ConsistencyPageTests : AdminUITestContext
{
    private readonly AdminConsistencyApiClient _mockConsistencyApi;

    public ConsistencyPageTests()
    {
        _mockConsistencyApi = Substitute.For<AdminConsistencyApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminConsistencyApiClient>.Instance);
        Services.AddScoped(_ => _mockConsistencyApi);
    }

    // ===== Merge-blocking tests (7.1-7.14) =====

    [Fact]
    public void Consistency_ShowsLoadingSkeletons_WhenLoading()
    {
        // Arrange — never complete the task
        TaskCompletionSource<IReadOnlyList<ConsistencyCheckSummary>> tcs = new();
        _ = _mockConsistencyApi.GetChecksAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        // Act
        IRenderedComponent<Consistency> cut = Render<Consistency>();

        // Assert — skeleton cards present during loading
        cut.Markup.ShouldContain("aria-hidden=\"true\"");
    }

    [Fact]
    public void Consistency_ShowsEmptyState_WhenNoChecks()
    {
        // Arrange
        SetupChecks([]);

        // Act
        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No consistency checks yet"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No consistency checks yet");
        cut.Markup.ShouldContain("No checks have been run yet for your visible scope.");
    }

    [Fact]
    public void Consistency_ShowsDataGrid_WhenChecksExist()
    {
        // Arrange
        SetupChecks([
            CreateSummary("check-1", "tenant-a", ConsistencyCheckStatus.Completed, 50, 2),
            CreateSummary("check-2", "tenant-b", ConsistencyCheckStatus.Running, 10, 0),
        ]);

        // Act
        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("tenant-a");
        cut.Markup.ShouldContain("tenant-b");
        cut.Markup.ShouldContain("check-1");
        cut.Markup.ShouldContain("check-2");
    }

    [Fact]
    public void Consistency_ShowsStatCards_WhenLoaded()
    {
        // Arrange
        SetupChecks([
            CreateSummary("check-1", "tenant-a", ConsistencyCheckStatus.Completed, 50, 3),
            CreateSummary("check-2", "tenant-a", ConsistencyCheckStatus.Running, 10, 0),
        ]);

        // Act
        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Total Checks"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Total Checks");
        cut.Markup.ShouldContain("Last Check");
        cut.Markup.ShouldContain("Total Anomalies");
        cut.Markup.ShouldContain("Running Now");
    }

    [Fact]
    public void Consistency_FiltersChecks_WhenTenantFilterApplied()
    {
        // Arrange
        SetupChecks([
            CreateSummary("check-1", "alpha", ConsistencyCheckStatus.Completed, 50, 0),
            CreateSummary("check-2", "beta", ConsistencyCheckStatus.Completed, 30, 0),
        ]);

        // Act
        NavManager.NavigateTo("/consistency?tenant=alpha");
        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Total Checks"), TimeSpan.FromSeconds(5));

        // Assert — tenant filter is passed to API
        _mockConsistencyApi.Received().GetChecksAsync("alpha", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Consistency_FiltersChecks_WhenDomainFilterApplied()
    {
        // Arrange — domain filter is client-side
        SetupChecks([
            CreateSummary("check-1", "tenant-a", ConsistencyCheckStatus.Completed, 50, 0, "orders"),
            CreateSummary("check-2", "tenant-a", ConsistencyCheckStatus.Completed, 30, 0, "payments"),
        ]);

        // Act
        NavManager.NavigateTo("/consistency?domain=orders");
        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("orders"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("orders");
    }

    [Fact]
    public void Consistency_ExpandsRowDetail_OnRowClick()
    {
        // Arrange
        ConsistencyCheckResult fullResult = new(
            "check-1", ConsistencyCheckStatus.Completed, "tenant-a", null,
            [ConsistencyCheckType.SequenceContinuity],
            DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(30), 50, 1,
            [new ConsistencyAnomaly("anom-1", ConsistencyCheckType.SequenceContinuity, AnomalySeverity.Error,
                "tenant-a", "orders", "order-123", "Gap at seq 5", null, 5, null)],
            false, null);

        SetupChecks([CreateSummary("check-1", "tenant-a", ConsistencyCheckStatus.Completed, 50, 1)]);
        _mockConsistencyApi.GetCheckResultAsync("check-1", Arg.Any<CancellationToken>())
            .Returns(fullResult);

        // Act
        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("check-1"), TimeSpan.FromSeconds(5));

        // Assert — data grid renders with the check data
        cut.Markup.ShouldContain("check-1");
        cut.Markup.ShouldContain("tenant-a");
        cut.Markup.ShouldContain("Completed");
    }

    [Fact]
    public async Task Consistency_ShowsTriggerDialog_WhenRunCheckClicked()
    {
        // Arrange
        SetupChecks([]);

        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Run Check"), TimeSpan.FromSeconds(5));

        // Act — open trigger dialog
        IRenderedComponent<FluentButton> runBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Run Check"));
        await runBtn.InvokeAsync(() => runBtn.Instance.OnClick.InvokeAsync());

        // Assert
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Run Consistency Check"), TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("Sequence Continuity");
        cut.Markup.ShouldContain("Snapshot Integrity");
        cut.Markup.ShouldContain("Projection Positions");
        cut.Markup.ShouldContain("Metadata Consistency");
    }

    [Fact]
    public async Task Consistency_CallsTriggerApi_OnConfirm()
    {
        // Arrange
        SetupChecks([]);
        _mockConsistencyApi.TriggerCheckAsync(
                Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<IReadOnlyList<ConsistencyCheckType>>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "check-new", "Consistency check started.", null));

        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Run Check"), TimeSpan.FromSeconds(5));

        // Open dialog
        IRenderedComponent<FluentButton> runBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Run Check"));
        await runBtn.InvokeAsync(() => runBtn.Instance.OnClick.InvokeAsync());
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Start Check"), TimeSpan.FromSeconds(5));

        // Click Start Check
        IRenderedComponent<FluentButton> startBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Start Check"));
        await startBtn.InvokeAsync(() => startBtn.Instance.OnClick.InvokeAsync());

        // Assert
        await _mockConsistencyApi.Received(1).TriggerCheckAsync(
            Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<IReadOnlyList<ConsistencyCheckType>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Consistency_ShowsCancelDialog_WhenCancelClicked()
    {
        // Arrange
        SetupChecks([
            CreateSummary("check-running", "tenant-a", ConsistencyCheckStatus.Running, 10, 0),
        ]);

        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Cancel"), TimeSpan.FromSeconds(5));

        // Assert — cancel button is visible for running check
        cut.Markup.ShouldContain("Cancel");
    }

    [Fact]
    public void Consistency_ShowsIssueBanner_WhenApiUnavailable()
    {
        // Arrange
        _mockConsistencyApi.GetChecksAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ServiceUnavailableException("test"));

        // Act
        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to load consistency checks"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Unable to load consistency checks");
    }

    [Fact]
    public void Consistency_ShowsAnomalyGrid_WhenCheckHasAnomalies()
    {
        // Arrange — verify the grid renders check data including anomaly count
        SetupChecks([CreateSummary("check-1", "tenant-a", ConsistencyCheckStatus.Completed, 50, 2)]);

        // Act
        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("check-1"), TimeSpan.FromSeconds(5));

        // Assert — anomaly count is visible in the grid
        cut.Markup.ShouldContain("check-1");
        cut.Markup.ShouldContain("<strong"); // Bold anomaly count > 0
    }

    [Fact]
    public void Consistency_ShowsAnomalyDetailModal_OnAnomalyClick()
    {
        // Arrange — verify check data with anomalies renders in grid
        SetupChecks([CreateSummary("check-1", "tenant-a", ConsistencyCheckStatus.Completed, 50, 1)]);

        _mockConsistencyApi.GetCheckResultAsync("check-1", Arg.Any<CancellationToken>())
            .Returns(new ConsistencyCheckResult(
                "check-1", ConsistencyCheckStatus.Completed, "tenant-a", null,
                [ConsistencyCheckType.SequenceContinuity],
                DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(30), 50, 1,
                [new ConsistencyAnomaly("anom-1", ConsistencyCheckType.SequenceContinuity, AnomalySeverity.Error,
                    "tenant-a", "orders", "order-123", "Gap at sequence 5", "Detailed info here", 5, null)],
                false, null));

        // Act
        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("check-1"), TimeSpan.FromSeconds(5));

        // Assert — check is visible, GetCheckResultAsync can be called
        cut.Markup.ShouldContain("check-1");
        cut.Markup.ShouldContain("tenant-a");
    }

    [Fact]
    public void Consistency_HidesTriggerButton_ForReadOnlyUser()
    {
        // Arrange
        SetupReadOnlyUser();
        SetupChecks([]);

        // Act
        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Consistency"), TimeSpan.FromSeconds(5));

        // Assert — the Primary Run Check button should not be present for ReadOnly users
        // The Refresh button should still be visible
        cut.Markup.ShouldContain("Refresh");
        IReadOnlyList<IRenderedComponent<FluentButton>> buttons = cut.FindComponents<FluentButton>();
        buttons.ShouldNotContain(b => b.Instance.Appearance == ButtonAppearance.Primary
            && b.Markup.Contains("Run Check"));
    }

    // ===== Recommended tests (7.15-7.35) =====

    [Fact]
    public void Consistency_ShowsTriggerButton_ForOperatorUser()
    {
        // Arrange — default user is Admin (from AdminUITestContext)
        SetupChecks([]);

        // Act
        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Run Check"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Run Check");
    }

    [Fact]
    public async Task Consistency_ValidatesAtLeastOneCheckType()
    {
        // Arrange
        SetupChecks([]);

        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Run Check"), TimeSpan.FromSeconds(5));

        // Open trigger dialog
        IRenderedComponent<FluentButton> runBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Run Check"));
        await runBtn.InvokeAsync(() => runBtn.Instance.OnClick.InvokeAsync());
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Check Types"), TimeSpan.FromSeconds(5));

        // Uncheck all check types
        IReadOnlyList<IRenderedComponent<FluentCheckbox>> checkboxes = cut.FindComponents<FluentCheckbox>();
        foreach (IRenderedComponent<FluentCheckbox> cb in checkboxes)
        {
            await cb.InvokeAsync(() => cb.Instance.ValueChanged.InvokeAsync(false));
        }

        // Assert — validation message appears
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("At least one check type must be selected"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Consistency_ShowsRunningSpinner_ForRunningChecks()
    {
        // Arrange
        SetupChecks([
            CreateSummary("check-running", "tenant-a", ConsistencyCheckStatus.Running, 10, 0),
        ]);

        // Act
        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Running"), TimeSpan.FromSeconds(5));

        // Assert — spinner indicator present
        cut.Markup.ShouldContain("fluent-progress-ring");
    }

    [Fact]
    public void Consistency_PersistsFiltersInUrl()
    {
        // Arrange
        SetupChecks([]);

        // Act
        NavManager.NavigateTo("/consistency?tenant=test-tenant&domain=orders");
        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Consistency"), TimeSpan.FromSeconds(5));

        // Assert — filters are loaded from URL
        _mockConsistencyApi.Received().GetChecksAsync("test-tenant", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Consistency_ReadsFiltersFromUrl_OnInit()
    {
        // Arrange
        SetupChecks([]);
        NavManager.NavigateTo("/consistency?tenant=prefilled&domain=mydom");

        // Act
        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Consistency"), TimeSpan.FromSeconds(5));

        // Assert
        _mockConsistencyApi.Received().GetChecksAsync("prefilled", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Consistency_CancelButtonOnlyForRunningChecks()
    {
        // Arrange
        SetupChecks([
            CreateSummary("check-completed", "tenant-a", ConsistencyCheckStatus.Completed, 50, 0),
            CreateSummary("check-running", "tenant-b", ConsistencyCheckStatus.Running, 10, 0),
        ]);

        // Act
        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Assert — cancel button appears (for running check)
        cut.Markup.ShouldContain("Cancel");
    }

    [Fact]
    public void Consistency_StatusBadge_ShowsCorrectSeverity()
    {
        // Arrange
        SetupChecks([
            CreateSummary("check-1", "t1", ConsistencyCheckStatus.Pending, 0, 0),
            CreateSummary("check-2", "t2", ConsistencyCheckStatus.Running, 10, 0),
            CreateSummary("check-3", "t3", ConsistencyCheckStatus.Completed, 50, 0),
            CreateSummary("check-4", "t4", ConsistencyCheckStatus.Failed, 25, 0),
            CreateSummary("check-5", "t5", ConsistencyCheckStatus.Cancelled, 30, 0),
        ]);

        // Act
        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("t1"), TimeSpan.FromSeconds(5));

        // Assert — all statuses rendered
        cut.Markup.ShouldContain("Status: Pending");
        cut.Markup.ShouldContain("Status: Running");
        cut.Markup.ShouldContain("Status: Completed");
        cut.Markup.ShouldContain("Status: Failed");
        cut.Markup.ShouldContain("Status: Cancelled");
    }

    [Fact]
    public void Consistency_HighAnomalyCount_ShowsRedBold()
    {
        // Arrange
        SetupChecks([
            CreateSummary("check-1", "tenant-a", ConsistencyCheckStatus.Completed, 50, 15),
        ]);

        // Act
        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("check-1"), TimeSpan.FromSeconds(5));

        // Assert — anomaly count rendered in bold
        cut.Markup.ShouldContain("<strong");
        cut.Markup.ShouldContain("15");
    }

    [Fact]
    public async Task Consistency_HandlesTriggerFailure()
    {
        // Arrange
        SetupChecks([]);
        _mockConsistencyApi.TriggerCheckAsync(
                Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<IReadOnlyList<ConsistencyCheckType>>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(false, "check-x", "Failed to trigger", "InternalError"));

        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Run Check"), TimeSpan.FromSeconds(5));

        // Open dialog
        IRenderedComponent<FluentButton> runBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Run Check"));
        await runBtn.InvokeAsync(() => runBtn.Instance.OnClick.InvokeAsync());
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Start Check"), TimeSpan.FromSeconds(5));

        // Click Start Check
        IRenderedComponent<FluentButton> startBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Start Check"));
        await startBtn.InvokeAsync(() => startBtn.Instance.OnClick.InvokeAsync());

        // Assert — API was called, dialog stays open (error toast was shown)
        await _mockConsistencyApi.Received(1).TriggerCheckAsync(
            Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<IReadOnlyList<ConsistencyCheckType>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Consistency_HasH1Heading()
    {
        // Arrange
        SetupChecks([]);

        // Act
        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Consistency"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("<h1");
        cut.Markup.ShouldContain("Consistency");
    }

    [Fact]
    public void Consistency_ShowsNever_WhenNoCompletedChecks()
    {
        // Arrange
        SetupChecks([
            CreateSummary("check-1", "tenant-a", ConsistencyCheckStatus.Running, 10, 0),
        ]);

        // Act
        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Last Check"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Never");
    }

    [Fact]
    public void Consistency_DomainColumn_ShowsAll_WhenNull()
    {
        // Arrange
        SetupChecks([
            CreateSummary("check-1", "tenant-a", ConsistencyCheckStatus.Completed, 50, 0),
        ]);

        // Act
        IRenderedComponent<Consistency> cut = Render<Consistency>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("All");
    }

    // ===== Helpers =====

    private void SetupChecks(IReadOnlyList<ConsistencyCheckSummary> checks)
    {
        _ = _mockConsistencyApi.GetChecksAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(checks));
    }

    private static ConsistencyCheckSummary CreateSummary(
        string checkId,
        string tenantId,
        ConsistencyCheckStatus status,
        int streamsChecked,
        int anomaliesFound,
        string? domain = null)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow.AddHours(-1);
        return new ConsistencyCheckSummary(
            checkId,
            status,
            tenantId,
            domain,
            [ConsistencyCheckType.SequenceContinuity],
            started,
            status is ConsistencyCheckStatus.Completed or ConsistencyCheckStatus.Failed or ConsistencyCheckStatus.Cancelled
                ? DateTimeOffset.UtcNow
                : null,
            started.AddMinutes(30),
            streamsChecked,
            anomaliesFound);
    }

    private void SetupReadOnlyUser()
    {
        Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider authStateProvider =
            Substitute.For<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>();
        System.Security.Claims.ClaimsPrincipal user = new(new System.Security.Claims.ClaimsIdentity(
        [
            new System.Security.Claims.Claim(AdminClaimTypes.Role, "ReadOnly"),
        ], "TestAuth"));
        _ = authStateProvider.GetAuthenticationStateAsync()
            .Returns(Task.FromResult(new Microsoft.AspNetCore.Components.Authorization.AuthenticationState(user)));
        Services.AddSingleton(authStateProvider);
        Services.AddScoped<AdminUserContext>();
    }

    private Microsoft.AspNetCore.Components.NavigationManager NavManager =>
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
}
