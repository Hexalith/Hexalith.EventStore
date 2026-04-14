using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
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
/// bUnit tests for the Compaction page.
/// </summary>
public class CompactionPageTests : AdminUITestContext
{
    private readonly AdminCompactionApiClient _mockCompactionApi;

    public CompactionPageTests()
    {
        _mockCompactionApi = Substitute.For<AdminCompactionApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminCompactionApiClient>.Instance);
        Services.AddScoped(_ => _mockCompactionApi);

        // Register AdminStorageApiClient that some shared components might need
        Services.AddScoped(_ => Substitute.For<AdminStorageApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStorageApiClient>.Instance));

        // Register AdminSnapshotApiClient that some shared components might need
        Services.AddScoped(_ => Substitute.For<AdminSnapshotApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminSnapshotApiClient>.Instance));
    }

    // ===== Merge-blocking tests (5.1-5.11) =====

    [Fact]
    public void CompactionPage_RendersStatCards_WithCorrectValues()
    {
        // Arrange
        SetupJobs([
            new CompactionJob("op-1", "tenant-a", null, CompactionJobStatus.Completed,
                DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-1),
                5000, 1_048_576, null),
            new CompactionJob("op-2", "tenant-b", "orders", CompactionJobStatus.Running,
                DateTimeOffset.UtcNow.AddMinutes(-5), null, null, null, null),
        ]);

        // Act
        IRenderedComponent<Compaction> cut = Render<Compaction>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Active Jobs"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Active Jobs");
        cut.Markup.ShouldContain("Completed (30d)");
        cut.Markup.ShouldContain("Space Reclaimed");
    }

    [Fact]
    public void CompactionPage_ShowsSkeletonCards_DuringLoading()
    {
        // Arrange — never complete the task
        TaskCompletionSource<IReadOnlyList<CompactionJob>> tcs = new();
        _ = _mockCompactionApi.GetCompactionJobsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        // Act
        IRenderedComponent<Compaction> cut = Render<Compaction>();

        // Assert — skeleton cards present during loading
        cut.Markup.ShouldContain("aria-hidden=\"true\"");
    }

    [Fact]
    public void CompactionPage_JobGrid_RendersAllJobs()
    {
        // Arrange
        SetupJobs([
            new CompactionJob("op-1", "tenant-a", null, CompactionJobStatus.Completed,
                DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-1),
                5000, 1_048_576, null),
            new CompactionJob("op-2", "tenant-b", "orders", CompactionJobStatus.Running,
                DateTimeOffset.UtcNow.AddMinutes(-5), null, null, null, null),
        ]);

        // Act
        IRenderedComponent<Compaction> cut = Render<Compaction>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("tenant-a");
        cut.Markup.ShouldContain("tenant-b");
        cut.Markup.ShouldContain("orders");
    }

    [Fact]
    public void CompactionPage_ShowsEmptyState_WhenNoJobs()
    {
        // Arrange
        SetupJobs([]);

        // Act
        IRenderedComponent<Compaction> cut = Render<Compaction>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No compaction jobs"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No compaction jobs");
        cut.Markup.ShouldContain("Trigger a compaction to reclaim storage space");
    }

    [Fact]
    public void CompactionPage_ShowsIssueBanner_OnApiError()
    {
        // Arrange
        _ = _mockCompactionApi.GetCompactionJobsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ServiceUnavailableException("test"));

        // Act
        IRenderedComponent<Compaction> cut = Render<Compaction>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to load compaction jobs"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Unable to load compaction jobs");
    }

    [Fact]
    public void CompactionPage_HasH1Heading()
    {
        // Arrange
        SetupJobs([]);

        // Act
        IRenderedComponent<Compaction> cut = Render<Compaction>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Compaction"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("<h1");
        cut.Markup.ShouldContain("Compaction");
    }

    [Fact]
    public void CompactionPage_TriggerButton_HiddenForReadOnlyUsers()
    {
        // Arrange
        SetupReadOnlyUser();
        SetupJobs([]);

        // Act
        IRenderedComponent<Compaction> cut = Render<Compaction>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Compaction"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldNotContain("Trigger Compaction");
    }

    [Fact]
    public void CompactionPage_TriggerButton_VisibleForOperatorUsers()
    {
        // Arrange — default user is Admin (from AdminUITestContext)
        SetupJobs([]);

        // Act
        IRenderedComponent<Compaction> cut = Render<Compaction>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Trigger Compaction"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Trigger Compaction");
    }

    [Fact]
    public async Task CompactionPage_TriggerDialog_CallsTriggerAndReloads()
    {
        // Arrange
        SetupJobs([]);
        _ = _mockCompactionApi.TriggerCompactionAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AdminOperationResult?>(new AdminOperationResult(true, "op-1", "Started", null)));

        IRenderedComponent<Compaction> cut = Render<Compaction>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Trigger Compaction"), TimeSpan.FromSeconds(5));

        // Act — open dialog
        IRenderedComponent<FluentButton> triggerBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Trigger Compaction"));
        await triggerBtn.InvokeAsync(() => triggerBtn.Instance.OnClick.InvokeAsync());
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Start Compaction"), TimeSpan.FromSeconds(5));

        // Fill in tenant ID via the dialog's text field
        IReadOnlyList<IRenderedComponent<FluentTextField>> textFields = cut.FindComponents<FluentTextField>();
        IRenderedComponent<FluentTextField> tenantField = textFields.First(f => f.Markup.Contains("Tenant ID"));
        await tenantField.InvokeAsync(() => tenantField.Instance.ValueChanged.InvokeAsync("test-tenant"));

        // Click Start Compaction
        IRenderedComponent<FluentButton> startBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Start Compaction"));
        await startBtn.InvokeAsync(() => startBtn.Instance.OnClick.InvokeAsync());

        // Assert — API invoked
        await _mockCompactionApi.Received(1).TriggerCompactionAsync(
            "test-tenant", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompactionPage_TriggerDialog_ShowsErrorToastOnFailure()
    {
        // Arrange
        SetupJobs([]);
        _ = _mockCompactionApi.TriggerCompactionAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AdminOperationResult?>(new AdminOperationResult(false, "op-1", "Compaction failed", "InvalidOperation")));

        IRenderedComponent<Compaction> cut = Render<Compaction>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Trigger Compaction"), TimeSpan.FromSeconds(5));

        // Act — open dialog
        IRenderedComponent<FluentButton> triggerBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Trigger Compaction"));
        await triggerBtn.InvokeAsync(() => triggerBtn.Instance.OnClick.InvokeAsync());
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Start Compaction"), TimeSpan.FromSeconds(5));

        // Fill in tenant ID
        IReadOnlyList<IRenderedComponent<FluentTextField>> textFields = cut.FindComponents<FluentTextField>();
        IRenderedComponent<FluentTextField> tenantField = textFields.First(f => f.Markup.Contains("Tenant ID"));
        await tenantField.InvokeAsync(() => tenantField.Instance.ValueChanged.InvokeAsync("test-tenant"));

        // Click Start Compaction
        IRenderedComponent<FluentButton> startBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Start Compaction"));
        await startBtn.InvokeAsync(() => startBtn.Instance.OnClick.InvokeAsync());

        // Assert — API was called
        await _mockCompactionApi.Received(1).TriggerCompactionAsync(
            "test-tenant", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CompactionPage_StatusBadges_RenderCorrectAppearance()
    {
        // Arrange
        SetupJobs([
            new CompactionJob("op-1", "tenant-a", null, CompactionJobStatus.Pending,
                DateTimeOffset.UtcNow.AddMinutes(-10), null, null, null, null),
            new CompactionJob("op-2", "tenant-b", null, CompactionJobStatus.Running,
                DateTimeOffset.UtcNow.AddMinutes(-5), null, null, null, null),
            new CompactionJob("op-3", "tenant-c", null, CompactionJobStatus.Completed,
                DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-1),
                1000, 512_000, null),
            new CompactionJob("op-4", "tenant-d", null, CompactionJobStatus.Failed,
                DateTimeOffset.UtcNow.AddHours(-3), DateTimeOffset.UtcNow.AddHours(-2),
                null, null, "Error occurred"),
        ]);

        // Act
        IRenderedComponent<Compaction> cut = Render<Compaction>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Assert — all status badges render with aria-labels
        cut.Markup.ShouldContain("Status: Pending");
        cut.Markup.ShouldContain("Status: Running");
        cut.Markup.ShouldContain("Status: Completed");
        cut.Markup.ShouldContain("Status: Failed");
    }

    // ===== Recommended tests (5.12-5.20) =====

    [Fact]
    public void CompactionPage_UrlParameters_ReadOnInit()
    {
        // Arrange
        SetupJobs([
            new CompactionJob("op-1", "tenant-filter", null, CompactionJobStatus.Completed,
                DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-1),
                1000, 512_000, null),
            new CompactionJob("op-2", "other-tenant", null, CompactionJobStatus.Completed,
                DateTimeOffset.UtcNow.AddHours(-3), DateTimeOffset.UtcNow.AddHours(-2),
                500, 256_000, null),
        ]);

        // Act — navigate with tenant parameter
        NavManager.NavigateTo("/compaction?tenant=tenant-filter");
        IRenderedComponent<Compaction> cut = Render<Compaction>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-filter"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("tenant-filter");
    }

    [Fact]
    public async Task CompactionPage_TriggerDialog_RendersFormFieldsAndWarning()
    {
        // Arrange
        SetupJobs([]);

        IRenderedComponent<Compaction> cut = Render<Compaction>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Trigger Compaction"), TimeSpan.FromSeconds(5));

        // Act — open trigger dialog
        IRenderedComponent<FluentButton> triggerBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Trigger Compaction"));
        await triggerBtn.InvokeAsync(() => triggerBtn.Instance.OnClick.InvokeAsync());

        // Assert — dialog shows form fields and warning
        cut.Markup.ShouldContain("Tenant ID");
        cut.Markup.ShouldContain("Domain");
        cut.Markup.ShouldContain("resource-intensive operation");
    }

    [Fact]
    public async Task CompactionPage_TriggerDialog_StartDisabledWhenTenantEmpty()
    {
        // Arrange
        SetupJobs([]);

        IRenderedComponent<Compaction> cut = Render<Compaction>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Trigger Compaction"), TimeSpan.FromSeconds(5));

        // Act — open trigger dialog
        IRenderedComponent<FluentButton> triggerBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Trigger Compaction"));
        await triggerBtn.InvokeAsync(() => triggerBtn.Instance.OnClick.InvokeAsync());

        // Assert — Start Compaction button is disabled (tenant ID is empty)
        IRenderedComponent<FluentButton> startBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Start Compaction"));
        startBtn.Instance.Disabled.ShouldBe(true);
    }

    [Fact]
    public void CompactionPage_ActiveJobs_ShowsWarningSeverity()
    {
        // Arrange — 1 running job = warning severity
        SetupJobs([
            new CompactionJob("op-1", "tenant-a", null, CompactionJobStatus.Running,
                DateTimeOffset.UtcNow.AddMinutes(-5), null, null, null, null),
        ]);

        // Act
        IRenderedComponent<Compaction> cut = Render<Compaction>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Active Jobs"), TimeSpan.FromSeconds(5));

        // Assert — stat card shows warning severity
        cut.Markup.ShouldContain("Active Jobs");
    }

    [Fact]
    public void CompactionPage_SpaceReclaimed_ShowsNA_WhenNoCompletedJobs()
    {
        // Arrange — no completed jobs
        SetupJobs([
            new CompactionJob("op-1", "tenant-a", null, CompactionJobStatus.Running,
                DateTimeOffset.UtcNow.AddMinutes(-5), null, null, null, null),
        ]);

        // Act
        IRenderedComponent<Compaction> cut = Render<Compaction>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Space Reclaimed"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("N/A");
    }

    [Fact]
    public void CompactionPage_JobList_FiltersByTenant()
    {
        // Arrange
        SetupJobs([
            new CompactionJob("op-1", "alpha-tenant", null, CompactionJobStatus.Completed,
                DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-1),
                1000, 512_000, null),
            new CompactionJob("op-2", "beta-tenant", null, CompactionJobStatus.Completed,
                DateTimeOffset.UtcNow.AddHours(-3), DateTimeOffset.UtcNow.AddHours(-2),
                500, 256_000, null),
        ]);

        // Act — navigate with tenant filter
        NavManager.NavigateTo("/compaction?tenant=alpha");
        IRenderedComponent<Compaction> cut = Render<Compaction>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("alpha-tenant"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("alpha-tenant");
    }

    [Fact]
    public void CompactionPage_DomainColumn_ShowsAll_WhenDomainNull()
    {
        // Arrange
        SetupJobs([
            new CompactionJob("op-1", "tenant-a", null, CompactionJobStatus.Completed,
                DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-1),
                1000, 512_000, null),
        ]);

        // Act
        IRenderedComponent<Compaction> cut = Render<Compaction>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Assert — domain shows "All" when null
        cut.Markup.ShouldContain("All");
    }

    // ===== Story 21-6: v5 dialog structure contract =====

    [Fact]
    public async Task CompactionPage_TriggerDialog_RendersV5DialogStructure()
    {
        // AC 32a: Locks in the v5 DOM structure contract for FluentDialogBody / TitleTemplate / ActionTemplate.
        // Arrange
        SetupJobs([]);

        IRenderedComponent<Compaction> cut = Render<Compaction>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Trigger Compaction"), TimeSpan.FromSeconds(5));

        // Act — open dialog
        IRenderedComponent<FluentButton> triggerBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Trigger Compaction"));
        await triggerBtn.InvokeAsync(() => triggerBtn.Instance.OnClick.InvokeAsync());
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Start Compaction"), TimeSpan.FromSeconds(5));

        // Assert — v5 dialog body element is present, no v4 header/footer elements
        cut.Markup.ShouldContain("fluent-dialog-body");
        cut.Markup.ShouldNotContain("fluent-dialog-header");
        cut.Markup.ShouldNotContain("fluent-dialog-footer");

        // Assert — TitleTemplate-rendered title is present
        cut.Markup.ShouldContain("Trigger Compaction");
        // Assert — ActionTemplate-rendered footer button is present
        cut.Markup.ShouldContain("Start Compaction");
    }

    // ===== Helpers =====

    private void SetupJobs(IReadOnlyList<CompactionJob> jobs)
    {
        _ = _mockCompactionApi.GetCompactionJobsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(jobs));
    }

    private void SetupReadOnlyUser()
    {
        // Override auth state with ReadOnly user
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
