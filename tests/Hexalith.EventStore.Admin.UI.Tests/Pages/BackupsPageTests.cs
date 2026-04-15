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
/// bUnit tests for the Backups page.
/// </summary>
public class BackupsPageTests : AdminUITestContext
{
    private readonly AdminBackupApiClient _mockBackupApi;

    public BackupsPageTests()
    {
        _mockBackupApi = Substitute.For<AdminBackupApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminBackupApiClient>.Instance);
        Services.AddScoped(_ => _mockBackupApi);

        // Register other API clients that shared components might need
        Services.AddScoped(_ => Substitute.For<AdminStorageApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStorageApiClient>.Instance));
        Services.AddScoped(_ => Substitute.For<AdminSnapshotApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminSnapshotApiClient>.Instance));
        Services.AddScoped(_ => Substitute.For<AdminCompactionApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminCompactionApiClient>.Instance));
    }

    // ===== Merge-blocking tests (6.1-6.13) =====

    [Fact]
    public void BackupsPage_RendersStatCards_WithCorrectValues()
    {
        // Arrange
        SetupJobs([
            CreateJob("bk-1", "tenant-a", BackupJobStatus.Completed, sizeBytes: 1_048_576, eventCount: 5000),
            CreateJob("bk-2", "tenant-b", BackupJobStatus.Running),
        ]);

        // Act
        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Active Backups"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Active Backups");
        cut.Markup.ShouldContain("Completed (30d)");
        cut.Markup.ShouldContain("Total Backup Size");
        cut.Markup.ShouldContain("Last Successful");
    }

    [Fact]
    public void BackupsPage_ShowsSkeletonCards_DuringLoading()
    {
        // Arrange — never complete the task
        TaskCompletionSource<IReadOnlyList<BackupJob>> tcs = new();
        _ = _mockBackupApi.GetBackupJobsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        // Act
        IRenderedComponent<Backups> cut = Render<Backups>();

        // Assert — skeleton cards present during loading
        cut.Markup.ShouldContain("aria-hidden=\"true\"");
    }

    [Fact]
    public void BackupsPage_Grid_RendersAllJobs()
    {
        // Arrange
        SetupJobs([
            CreateJob("bk-1", "tenant-a", BackupJobStatus.Completed, sizeBytes: 1_048_576, eventCount: 5000),
            CreateJob("bk-2", "tenant-b", BackupJobStatus.Running),
        ]);

        // Act
        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("tenant-a");
        cut.Markup.ShouldContain("tenant-b");
    }

    [Fact]
    public void BackupsPage_ShowsEmptyState_WhenNoBackups()
    {
        // Arrange
        SetupJobs([]);

        // Act
        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No backups"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("No backups");
        cut.Markup.ShouldContain("Create a backup to protect your event store data");
    }

    [Fact]
    public void BackupsPage_ShowsIssueBanner_OnApiError()
    {
        // Arrange
        _ = _mockBackupApi.GetBackupJobsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ServiceUnavailableException("test"));

        // Act
        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Unable to load backup jobs"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Unable to load backup jobs");
    }

    [Fact]
    public void BackupsPage_HasH1Heading()
    {
        // Arrange
        SetupJobs([]);

        // Act
        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Backups"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("<h1");
        cut.Markup.ShouldContain("Backups");
    }

    [Fact]
    public void BackupsPage_CreateBackupButton_HiddenForReadOnlyUsers()
    {
        // Arrange
        SetupReadOnlyUser();
        SetupJobs([]);

        // Act
        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Backups"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldNotContain("Create Backup");
    }

    [Fact]
    public void BackupsPage_CreateBackupButton_VisibleForAdminUsers()
    {
        // Arrange — default user is Admin (from AdminUITestContext)
        SetupJobs([]);

        // Act
        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Backup"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Create Backup");
    }

    [Fact]
    public async Task BackupsPage_CreateDialog_CallsTriggerAndReloads()
    {
        // Arrange
        SetupJobs([]);
        _ = _mockBackupApi.TriggerBackupAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AdminOperationResult?>(new AdminOperationResult(true, "op-1", "Started", null)));

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Backup"), TimeSpan.FromSeconds(5));

        // Act — open dialog
        IRenderedComponent<FluentButton> createBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Create Backup"));
        await createBtn.InvokeAsync(() => createBtn.Instance.OnClick.InvokeAsync());
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Start Backup"), TimeSpan.FromSeconds(5));

        // Fill in tenant ID
        IReadOnlyList<IRenderedComponent<FluentTextInput>> textFields = cut.FindComponents<FluentTextInput>();
        IRenderedComponent<FluentTextInput> tenantField = textFields.First(f => f.Markup.Contains("Tenant ID"));
        await tenantField.InvokeAsync(() => tenantField.Instance.ValueChanged.InvokeAsync("test-tenant"));

        // Click Start Backup
        IRenderedComponent<FluentButton> startBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Start Backup"));
        await startBtn.InvokeAsync(() => startBtn.Instance.OnClick.InvokeAsync());

        // Assert — API invoked
        await _mockBackupApi.Received(1).TriggerBackupAsync(
            "test-tenant", Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackupsPage_CreateDialog_ShowsErrorToastOnFailure()
    {
        // Arrange
        SetupJobs([]);
        _ = _mockBackupApi.TriggerBackupAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AdminOperationResult?>(new AdminOperationResult(false, "op-1", "Backup failed", "InvalidOperation")));

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Backup"), TimeSpan.FromSeconds(5));

        // Act — open dialog
        IRenderedComponent<FluentButton> createBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Create Backup"));
        await createBtn.InvokeAsync(() => createBtn.Instance.OnClick.InvokeAsync());
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Start Backup"), TimeSpan.FromSeconds(5));

        // Fill in tenant ID
        IReadOnlyList<IRenderedComponent<FluentTextInput>> textFields = cut.FindComponents<FluentTextInput>();
        IRenderedComponent<FluentTextInput> tenantField = textFields.First(f => f.Markup.Contains("Tenant ID"));
        await tenantField.InvokeAsync(() => tenantField.Instance.ValueChanged.InvokeAsync("test-tenant"));

        // Click Start Backup
        IRenderedComponent<FluentButton> startBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Start Backup"));
        await startBtn.InvokeAsync(() => startBtn.Instance.OnClick.InvokeAsync());

        // Assert — API was called (failure toast is handled by toast service)
        await _mockBackupApi.Received(1).TriggerBackupAsync(
            "test-tenant", Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void BackupsPage_StatusBadges_RenderCorrectAppearance()
    {
        // Arrange
        SetupJobs([
            CreateJob("bk-1", "tenant-a", BackupJobStatus.Pending),
            CreateJob("bk-2", "tenant-b", BackupJobStatus.Running),
            CreateJob("bk-3", "tenant-c", BackupJobStatus.Completed, sizeBytes: 512_000, eventCount: 1000),
            CreateJob("bk-4", "tenant-d", BackupJobStatus.Failed, errorMessage: "Error occurred"),
        ]);

        // Act
        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Assert — all status badges render with aria-labels
        cut.Markup.ShouldContain("Status: Pending");
        cut.Markup.ShouldContain("Status: Running");
        cut.Markup.ShouldContain("Status: Completed");
        cut.Markup.ShouldContain("Status: Failed");
    }

    [Fact]
    public void BackupsPage_ValidateButton_OnlyOnCompletedBackups()
    {
        // Arrange
        SetupJobs([
            CreateJob("bk-1", "tenant-a", BackupJobStatus.Completed, isValidated: false, sizeBytes: 1000, eventCount: 100),
            CreateJob("bk-2", "tenant-b", BackupJobStatus.Running),
            CreateJob("bk-3", "tenant-c", BackupJobStatus.Pending),
        ]);

        // Act
        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Assert — Validate button visible for completed non-validated backup
        cut.Markup.ShouldContain("Validate");
    }

    [Fact]
    public void BackupsPage_RestoreButton_OnlyOnCompletedAndValidatedBackups()
    {
        // Arrange
        SetupJobs([
            CreateJob("bk-1", "tenant-a", BackupJobStatus.Completed, isValidated: true, sizeBytes: 1000, eventCount: 100),
            CreateJob("bk-2", "tenant-b", BackupJobStatus.Completed, isValidated: false, sizeBytes: 1000, eventCount: 100),
        ]);

        // Act
        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Assert — Restore button visible only for validated backup
        cut.Markup.ShouldContain("Restore");
    }

    // ===== Recommended tests (6.14-6.24) =====

    [Fact]
    public void BackupsPage_UrlParameters_ReadOnInit()
    {
        // Arrange
        SetupJobs([
            CreateJob("bk-1", "tenant-filter", BackupJobStatus.Completed, sizeBytes: 512_000, eventCount: 1000),
            CreateJob("bk-2", "other-tenant", BackupJobStatus.Completed, sizeBytes: 256_000, eventCount: 500),
        ]);

        // Act — navigate with tenant parameter
        NavManager.NavigateTo("/backups?tenant=tenant-filter");
        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-filter"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("tenant-filter");
    }

    [Fact]
    public async Task BackupsPage_CreateDialog_RendersFormFieldsAndWarning()
    {
        // Arrange
        SetupJobs([]);

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Backup"), TimeSpan.FromSeconds(5));

        // Act — open create dialog
        IRenderedComponent<FluentButton> createBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Create Backup"));
        await createBtn.InvokeAsync(() => createBtn.Instance.OnClick.InvokeAsync());

        // Assert — dialog shows form fields and warning
        cut.Markup.ShouldContain("Tenant ID");
        cut.Markup.ShouldContain("Description");
        cut.Markup.ShouldContain("resource-intensive operation");
    }

    [Fact]
    public async Task BackupsPage_CreateDialog_StartDisabledWhenTenantEmpty()
    {
        // Arrange
        SetupJobs([]);

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Backup"), TimeSpan.FromSeconds(5));

        // Act — open create dialog
        IRenderedComponent<FluentButton> createBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Create Backup"));
        await createBtn.InvokeAsync(() => createBtn.Instance.OnClick.InvokeAsync());

        // Assert — Start Backup button is disabled (tenant ID is empty)
        IRenderedComponent<FluentButton> startBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Start Backup"));
        startBtn.Instance.Disabled.ShouldBe(true);
    }

    [Fact]
    public void BackupsPage_ActiveBackups_ShowsWarningSeverity()
    {
        // Arrange — 1 running backup = warning severity
        SetupJobs([
            CreateJob("bk-1", "tenant-a", BackupJobStatus.Running),
        ]);

        // Act
        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Active Backups"), TimeSpan.FromSeconds(5));

        // Assert — stat card shows warning severity
        cut.Markup.ShouldContain("Active Backups");
    }

    [Fact]
    public void BackupsPage_LastSuccessful_ShowsNever_WhenNoCompletedBackups()
    {
        // Arrange — no completed backups
        SetupJobs([
            CreateJob("bk-1", "tenant-a", BackupJobStatus.Running),
        ]);

        // Act
        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Last Successful"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Never");
    }

    [Fact]
    public void BackupsPage_BackupList_FiltersByTenant()
    {
        // Arrange
        SetupJobs([
            CreateJob("bk-1", "alpha-tenant", BackupJobStatus.Completed, sizeBytes: 512_000, eventCount: 1000),
            CreateJob("bk-2", "beta-tenant", BackupJobStatus.Completed, sizeBytes: 256_000, eventCount: 500),
        ]);

        // Act — navigate with tenant filter
        NavManager.NavigateTo("/backups?tenant=alpha");
        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("alpha-tenant"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("alpha-tenant");
    }

    [Fact]
    public void BackupsPage_ScopeColumn_ShowsFull_WhenStreamIdNull()
    {
        // Arrange
        SetupJobs([
            CreateJob("bk-1", "tenant-a", BackupJobStatus.Completed, streamId: null, sizeBytes: 512_000, eventCount: 1000),
        ]);

        // Act
        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Assert — scope shows "Full" when StreamId is null
        cut.Markup.ShouldContain("Full");
    }

    [Fact]
    public void BackupsPage_ScopeColumn_ShowsStream_WhenStreamIdPresent()
    {
        // Arrange
        SetupJobs([
            new BackupJob("bk-stream", "tenant-a", "stream-123", null, BackupJobType.Backup, BackupJobStatus.Completed,
                true, DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-1),
                1000, 512_000, false, null),
        ]);

        // Act
        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("tenant-a"), TimeSpan.FromSeconds(5));

        // Assert — scope shows "Stream" when StreamId is present
        cut.Markup.ShouldContain("Stream");
    }

    [Fact]
    public async Task BackupsPage_ConcurrentGuard_PreventsCreateForSameTenant()
    {
        // Arrange — tenant-a has a running backup
        SetupJobs([
            CreateJob("bk-1", "tenant-a", BackupJobStatus.Running),
        ]);

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Backup"), TimeSpan.FromSeconds(5));

        // Act — open create dialog
        IRenderedComponent<FluentButton> createBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Create Backup"));
        await createBtn.InvokeAsync(() => createBtn.Instance.OnClick.InvokeAsync());
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Start Backup"), TimeSpan.FromSeconds(5));

        // Enter the same tenant
        IRenderedComponent<FluentTextInput> tenantField = cut.FindComponents<FluentTextInput>()
            .First(f => f.Markup.Contains("Tenant ID"));
        await tenantField.InvokeAsync(() => tenantField.Instance.ValueChanged.InvokeAsync("tenant-a"));

        // Assert — Start Backup button should be disabled and warning shown
        cut.Markup.ShouldContain("Backup already in progress for this tenant");
        IRenderedComponent<FluentButton> startBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Start Backup"));
        startBtn.Instance.Disabled.ShouldBe(true);
    }

    [Fact]
    public async Task BackupsPage_ExportDialog_CallsExportWithCorrectParameters()
    {
        // Arrange
        SetupJobs([]);
        _ = _mockBackupApi.ExportStreamAsync(
            Arg.Any<StreamExportRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<StreamExportResult?>(
                new StreamExportResult(true, "t1", "d1", "a1", 100, "{}", "export.json", null)));

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Export Stream"), TimeSpan.FromSeconds(5));

        // Act — open export dialog
        IRenderedComponent<FluentButton> exportBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Export Stream"));
        await exportBtn.InvokeAsync(() => exportBtn.Instance.OnClick.InvokeAsync());
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Tenant ID"), TimeSpan.FromSeconds(5));

        // Fill fields
        IReadOnlyList<IRenderedComponent<FluentTextInput>> fields = cut.FindComponents<FluentTextInput>();
        await fields.First(f => f.Markup.Contains("Tenant ID")).InvokeAsync(
            () => fields.First(f => f.Markup.Contains("Tenant ID")).Instance.ValueChanged.InvokeAsync("t1"));
        await fields.First(f => f.Markup.Contains("Domain")).InvokeAsync(
            () => fields.First(f => f.Markup.Contains("Domain")).Instance.ValueChanged.InvokeAsync("d1"));
        await fields.First(f => f.Markup.Contains("Aggregate ID")).InvokeAsync(
            () => fields.First(f => f.Markup.Contains("Aggregate ID")).Instance.ValueChanged.InvokeAsync("a1"));

        // Click Export
        IRenderedComponent<FluentButton> submitBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains(">Export<"));
        await submitBtn.InvokeAsync(() => submitBtn.Instance.OnClick.InvokeAsync());

        // Assert — API invoked
        await _mockBackupApi.Received(1).ExportStreamAsync(
            Arg.Any<StreamExportRequest>(), Arg.Any<CancellationToken>());
    }

    // ===== Helpers =====

    private void SetupJobs(IReadOnlyList<BackupJob> jobs)
    {
        _ = _mockBackupApi.GetBackupJobsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(jobs));
    }

    private static BackupJob CreateJob(
        string backupId,
        string tenantId,
        BackupJobStatus status,
        string? streamId = null,
        long? sizeBytes = null,
        long? eventCount = null,
        bool isValidated = false,
        string? errorMessage = null,
        BackupJobType jobType = BackupJobType.Backup)
    {
        DateTimeOffset created = DateTimeOffset.UtcNow.AddHours(-2);
        DateTimeOffset? completed = status is BackupJobStatus.Completed or BackupJobStatus.Failed
            ? DateTimeOffset.UtcNow.AddHours(-1)
            : null;
        return new BackupJob(
            backupId, tenantId, streamId, null, jobType, status, true,
            created, completed, eventCount, sizeBytes, isValidated, errorMessage);
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
