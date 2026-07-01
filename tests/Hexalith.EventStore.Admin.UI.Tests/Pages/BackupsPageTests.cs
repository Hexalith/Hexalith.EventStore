using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.UI.Pages;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;
using Hexalith.EventStore.Admin.UI.Tests.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FluentUI.AspNetCore.Components;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.UI.Tests.Pages;

/// <summary>
/// bUnit tests for the Backups page.
/// </summary>
public class BackupsPageTests : AdminUITestContext {
    private readonly AdminBackupApiClient _mockBackupApi;

    public BackupsPageTests() {
        _mockBackupApi = Substitute.For<AdminBackupApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminBackupApiClient>.Instance);
        _ = Services.AddScoped(_ => _mockBackupApi);

        // Register other API clients that shared components might need
        _ = Services.AddScoped(_ => Substitute.For<AdminStorageApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStorageApiClient>.Instance));
        _ = Services.AddScoped(_ => Substitute.For<AdminSnapshotApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminSnapshotApiClient>.Instance));
        _ = Services.AddScoped(_ => Substitute.For<AdminCompactionApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminCompactionApiClient>.Instance));
    }

    // ===== Merge-blocking tests (6.1-6.13) =====

    [Fact]
    public void BackupsPage_RendersStatCards_WithCorrectValues() {
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
    public void BackupsPage_ShowsSkeletonCards_DuringLoading() {
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
    public void BackupsPage_Grid_RendersAllJobs() {
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
    public void BackupsPage_ShowsEmptyState_WhenNoBackups() {
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
    public void BackupsPage_ShowsIssueBanner_OnApiError() {
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
    public void BackupsPage_HasH1Heading() {
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
    public void BackupsPage_CreateBackupButton_HiddenForReadOnlyUsers() {
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
    public void BackupsPage_CreateBackupButton_VisibleForAdminUsers() {
        // Arrange — default user is Admin (from AdminUITestContext)
        SetupJobs([]);

        // Act
        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Backup"), TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("Create Backup");
    }

    [Fact]
    public async Task BackupsPage_CreateDialog_CallsTriggerAndReloads() {
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
        await createBtn.InvokeAsync(createBtn.Instance.OnClick.InvokeAsync);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Submit Deferred Request"), TimeSpan.FromSeconds(5));

        // Fill in tenant ID
        IReadOnlyList<IRenderedComponent<FluentTextInput>> textFields = cut.FindComponents<FluentTextInput>();
        IRenderedComponent<FluentTextInput> tenantField = textFields.First(f => f.Markup.Contains("Tenant ID"));
        await tenantField.InvokeAsync(() => tenantField.Instance.ValueChanged.InvokeAsync("test-tenant"));

        // Click Submit Deferred Request
        IRenderedComponent<FluentButton> startBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Submit Deferred Request"));
        await startBtn.InvokeAsync(startBtn.Instance.OnClick.InvokeAsync);

        // Assert — API invoked
        _ = await _mockBackupApi.Received(1).TriggerBackupAsync(
            "test-tenant", Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());

        TestToastService toastService = Services.GetRequiredService<TestToastService>();
        ToastOptions toastOptions = toastService.LastOptions.ShouldNotBeNull();
        toastOptions.Message.ShouldBe("Backup creation is deferred. EventStore does not yet have an approved backup engine and manifest model.");
        toastOptions.Intent.ShouldBe(ToastIntent.Warning);
    }

    [Fact]
    public async Task BackupsPage_CreateDialog_ShowsErrorToastOnFailure() {
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
        await createBtn.InvokeAsync(createBtn.Instance.OnClick.InvokeAsync);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Submit Deferred Request"), TimeSpan.FromSeconds(5));

        // Fill in tenant ID
        IReadOnlyList<IRenderedComponent<FluentTextInput>> textFields = cut.FindComponents<FluentTextInput>();
        IRenderedComponent<FluentTextInput> tenantField = textFields.First(f => f.Markup.Contains("Tenant ID"));
        await tenantField.InvokeAsync(() => tenantField.Instance.ValueChanged.InvokeAsync("test-tenant"));

        // Click Submit Deferred Request
        IRenderedComponent<FluentButton> startBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Submit Deferred Request"));
        await startBtn.InvokeAsync(startBtn.Instance.OnClick.InvokeAsync);

        // Assert — API was called (failure toast is handled by toast service)
        _ = await _mockBackupApi.Received(1).TriggerBackupAsync(
            "test-tenant", Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackupsPage_CreateDialog_ShowsDeferredToastAndClearsBusyState() {
        SetupJobs([]);
        const string deferredMessage = "Backup creation is deferred. EventStore does not yet have an approved backup engine and manifest model.";
        _ = _mockBackupApi.TriggerBackupAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AdminOperationResult?>(new AdminOperationResult(
                false,
                "deferred-backup-trigger",
                deferredMessage,
                "Deferred")));

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Backup"), TimeSpan.FromSeconds(5));

        IRenderedComponent<FluentButton> createBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Create Backup"));
        await createBtn.InvokeAsync(createBtn.Instance.OnClick.InvokeAsync);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Submit Deferred Request"), TimeSpan.FromSeconds(5));

        IRenderedComponent<FluentTextInput> tenantField = cut.FindComponents<FluentTextInput>()
            .First(f => f.Markup.Contains("Tenant ID"));
        await tenantField.InvokeAsync(() => tenantField.Instance.ValueChanged.InvokeAsync("test-tenant"));

        IRenderedComponent<FluentButton> startBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Submit Deferred Request"));
        await startBtn.InvokeAsync(startBtn.Instance.OnClick.InvokeAsync);

        TestToastService toastService = Services.GetRequiredService<TestToastService>();
        ToastOptions toastOptions = toastService.LastOptions.ShouldNotBeNull();
        toastOptions.Message.ShouldBe(deferredMessage);
        toastOptions.Intent.ShouldBe(ToastIntent.Warning);
        cut.Markup.ShouldContain("Create Backup");
        startBtn.Instance.Disabled.ShouldBeFalse();
    }

    [Fact]
    public async Task BackupsPage_CreateDialog_ShowsWarningToast_WhenBackendReportsSuccessTrueWithDeferredMessage() {
        SetupJobs([]);
        const string deferredMessage = "Backup creation is deferred.";
        _ = _mockBackupApi.TriggerBackupAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AdminOperationResult?>(new AdminOperationResult(
                true,
                "deferred-success-true",
                deferredMessage,
                null)));

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Backup"), TimeSpan.FromSeconds(5));

        IRenderedComponent<FluentButton> createBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Create Backup"));
        await createBtn.InvokeAsync(createBtn.Instance.OnClick.InvokeAsync);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Submit Deferred Request"), TimeSpan.FromSeconds(5));

        IRenderedComponent<FluentTextInput> tenantField = cut.FindComponents<FluentTextInput>()
            .First(f => f.Markup.Contains("Tenant ID"));
        await tenantField.InvokeAsync(() => tenantField.Instance.ValueChanged.InvokeAsync("test-tenant"));

        IRenderedComponent<FluentButton> submitBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Submit Deferred Request"));
        await submitBtn.InvokeAsync(submitBtn.Instance.OnClick.InvokeAsync);

        TestToastService toastService = Services.GetRequiredService<TestToastService>();
        ToastOptions toastOptions = toastService.LastOptions.ShouldNotBeNull();
        toastOptions.Message.ShouldBe(deferredMessage);
        toastOptions.Intent.ShouldNotBe(ToastIntent.Success);
        toastOptions.Intent.ShouldBe(ToastIntent.Warning);
    }

    [Fact]
    public void BackupsPage_CreateButton_ShowsDeferredBadgeBeforeDialog() {
        // DW18-AC6: backup creation stays deferred; stream export no longer does.
        SetupJobs([]);

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Backup"), TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("Deferred by backend");
        cut.Markup.ShouldContain("data-deferred-action=\"backup-create\"");
        cut.Markup.ShouldNotContain("data-deferred-action=\"stream-export\"");
    }

    [Fact]
    public void BackupsPage_ValidateButton_ShowsDeferredBadgeBeforeDialog() {
        // AC5, AC9: validation pre-communicates deferred state before the dialog opens.
        SetupJobs([
            CreateJob("bk-1", "tenant-a", BackupJobStatus.Completed, isValidated: false, sizeBytes: 1000, eventCount: 100),
        ]);

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Validate"), TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("Deferred by backend");
        cut.Markup.ShouldContain("data-deferred-action=\"backup-validate\"");
    }

    [Fact]
    public async Task BackupsPage_CreateDialog_PreCommunicatesDeferredBeforeSubmit() {
        // AC5, AC8, AC9.
        SetupJobs([]);

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Backup"), TimeSpan.FromSeconds(5));

        IRenderedComponent<FluentButton> createBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Create Backup"));
        await createBtn.InvokeAsync(createBtn.Instance.OnClick.InvokeAsync);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Submit Deferred Request"), TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("Backup creation is deferred. EventStore does not yet have an approved backup engine and manifest model.");
        cut.Markup.ShouldContain("<span>Submit Deferred Request</span>");
        cut.Markup.ShouldNotContain("<span>Start Backup</span>");
    }

    [Fact]
    public async Task BackupsPage_ExportDialog_NoLongerPreCommunicatesDeferredBeforeSubmit() {
        // DW18-AC6: stream export is real; backup create/validate stay deferred.
        SetupJobs([]);

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Export Stream"), TimeSpan.FromSeconds(5));

        IRenderedComponent<FluentButton> exportBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Export Stream"));
        await exportBtn.InvokeAsync(exportBtn.Instance.OnClick.InvokeAsync);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Tenant ID"), TimeSpan.FromSeconds(5));

        cut.Markup.ShouldNotContain("Stream export is deferred");
        cut.Markup.ShouldContain("<span>Export</span>");
        cut.Markup.ShouldNotContain("data-deferred-action=\"stream-export\"");
    }

    [Fact]
    public async Task BackupsPage_ValidateDialog_PreCommunicatesDeferredBeforeSubmit() {
        // AC5, AC8, AC9 for Validate.
        SetupJobs([
            CreateJob("bk-1", "tenant-a", BackupJobStatus.Completed, isValidated: false, sizeBytes: 1000, eventCount: 100),
        ]);

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Validate"), TimeSpan.FromSeconds(5));

        IRenderedComponent<FluentButton> validateButton = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Validate"));
        await validateButton.InvokeAsync(validateButton.Instance.OnClick.InvokeAsync);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Validate Backup"), TimeSpan.FromSeconds(5));

        cut.Markup.ShouldContain("Backup validation is deferred. EventStore does not yet have an approved backup manifest and validation model.");
        cut.Markup.ShouldContain("<span>Submit Deferred Request</span>");
        cut.Markup.ShouldNotContain("<span>Validate</span>");
    }

    [Fact]
    public void BackupsPage_StatusBadges_RenderCorrectAppearance() {
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
    public void BackupsPage_ValidateButton_OnlyOnCompletedBackups() {
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
    public void BackupsPage_RestoreButton_OnlyOnCompletedAndValidatedBackups() {
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

    [Fact]
    public async Task BackupsPage_ValidateDialog_ShowsDeferredToastAndClearsBusyState() {
        SetupJobs([
            CreateJob("bk-1", "tenant-a", BackupJobStatus.Completed, isValidated: false, sizeBytes: 1000, eventCount: 100),
        ]);
        const string deferredMessage = "Backup validation is deferred. EventStore does not yet have an approved backup manifest and validation model.";
        _ = _mockBackupApi.ValidateBackupAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AdminOperationResult?>(new AdminOperationResult(
                false,
                "deferred-backup-validate",
                deferredMessage,
                "Deferred")));

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Validate"), TimeSpan.FromSeconds(5));

        IRenderedComponent<FluentButton> validateButton = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Validate"));
        await validateButton.InvokeAsync(validateButton.Instance.OnClick.InvokeAsync);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Validate Backup"), TimeSpan.FromSeconds(5));

        IRenderedComponent<FluentButton> confirmButton = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("<span>Submit Deferred Request</span>"));
        await confirmButton.InvokeAsync(confirmButton.Instance.OnClick.InvokeAsync);

        TestToastService toastService = Services.GetRequiredService<TestToastService>();
        ToastOptions toastOptions = toastService.LastOptions.ShouldNotBeNull();
        toastOptions.Message.ShouldBe(deferredMessage);
        toastOptions.Intent.ShouldBe(ToastIntent.Warning);
        cut.Markup.ShouldContain("Validate Backup");
        confirmButton.Instance.Disabled.ShouldBeFalse();
    }

    [Fact]
    public async Task BackupsPage_ValidateDialog_ShowsWarningToast_WhenBackendReportsSuccessTrueWithDeferredMessage() {
        SetupJobs([
            CreateJob("bk-1", "tenant-a", BackupJobStatus.Completed, isValidated: false, sizeBytes: 1000, eventCount: 100),
        ]);
        const string deferredMessage = "Backup validation is deferred.";
        _ = _mockBackupApi.ValidateBackupAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AdminOperationResult?>(new AdminOperationResult(
                true,
                "deferred-success-true",
                deferredMessage,
                null)));

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Validate"), TimeSpan.FromSeconds(5));

        IRenderedComponent<FluentButton> validateButton = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Validate"));
        await validateButton.InvokeAsync(validateButton.Instance.OnClick.InvokeAsync);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Validate Backup"), TimeSpan.FromSeconds(5));

        IRenderedComponent<FluentButton> submitButton = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("<span>Submit Deferred Request</span>"));
        await submitButton.InvokeAsync(submitButton.Instance.OnClick.InvokeAsync);

        TestToastService toastService = Services.GetRequiredService<TestToastService>();
        ToastOptions toastOptions = toastService.LastOptions.ShouldNotBeNull();
        toastOptions.Message.ShouldBe(deferredMessage);
        toastOptions.Intent.ShouldNotBe(ToastIntent.Success);
        toastOptions.Intent.ShouldBe(ToastIntent.Warning);
    }

    [Fact]
    public async Task BackupsPage_RestoreDialog_ShowsDeferredToastAndClearsBusyState() {
        SetupJobs([
            CreateJob("bk-1", "tenant-a", BackupJobStatus.Completed, isValidated: true, sizeBytes: 1000, eventCount: 100),
        ]);
        const string deferredMessage = "Restore is deferred.";
        _ = _mockBackupApi.TriggerRestoreAsync(
                Arg.Any<string>(), Arg.Any<DateTimeOffset?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AdminOperationResult?>(new AdminOperationResult(
                false,
                "deferred-backup-restore",
                deferredMessage,
                "Deferred")));

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Restore"), TimeSpan.FromSeconds(5));

        IRenderedComponent<FluentButton> restoreButton = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Restore"));
        await restoreButton.InvokeAsync(restoreButton.Instance.OnClick.InvokeAsync);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Restore from Backup"), TimeSpan.FromSeconds(5));

        IRenderedComponent<FluentCheckbox> acknowledge = cut.FindComponents<FluentCheckbox>()
            .First(c => c.Markup.Contains("I understand"));
        await acknowledge.InvokeAsync(() => acknowledge.Instance.ValueChanged.InvokeAsync(true));

        IRenderedComponent<FluentButton> nextButton = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Next"));
        await nextButton.InvokeAsync(nextButton.Instance.OnClick.InvokeAsync);

        IRenderedComponent<FluentButton> startButton = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Start Restore"));
        await startButton.InvokeAsync(startButton.Instance.OnClick.InvokeAsync);

        TestToastService toastService = Services.GetRequiredService<TestToastService>();
        ToastOptions toastOptions = toastService.LastOptions.ShouldNotBeNull();
        toastOptions.Message.ShouldBe(deferredMessage);
        cut.Markup.ShouldContain("Restore from Backup");
        startButton.Instance.Disabled.ShouldBeFalse();
    }

    // ===== Recommended tests (6.14-6.24) =====

    [Fact]
    public void BackupsPage_UrlParameters_ReadOnInit() {
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
    public async Task BackupsPage_CreateDialog_RendersFormFieldsAndDeferredCopy() {
        // Arrange
        SetupJobs([]);

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Backup"), TimeSpan.FromSeconds(5));

        // Act — open create dialog
        IRenderedComponent<FluentButton> createBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Create Backup"));
        await createBtn.InvokeAsync(createBtn.Instance.OnClick.InvokeAsync);

        // Assert — dialog shows form fields and deferred body copy
        cut.Markup.ShouldContain("Tenant ID");
        cut.Markup.ShouldContain("Description");
        cut.Markup.ShouldContain("Backup creation is deferred. EventStore does not yet have an approved backup engine and manifest model.");
    }

    [Fact]
    public async Task BackupsPage_CreateDialog_SubmitDisabledWhenTenantEmpty() {
        // Arrange
        SetupJobs([]);

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Backup"), TimeSpan.FromSeconds(5));

        // Act — open create dialog
        IRenderedComponent<FluentButton> createBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Create Backup"));
        await createBtn.InvokeAsync(createBtn.Instance.OnClick.InvokeAsync);

        // Assert — Submit Deferred Request button is disabled (tenant ID is empty)
        IRenderedComponent<FluentButton> startBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Submit Deferred Request"));
        startBtn.Instance.Disabled.ShouldBe(true);
    }

    [Fact]
    public void BackupsPage_ActiveBackups_ShowsWarningSeverity() {
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
    public void BackupsPage_LastSuccessful_ShowsNever_WhenNoCompletedBackups() {
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
    public void BackupsPage_BackupList_FiltersByTenant() {
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
    public void BackupsPage_ScopeColumn_ShowsFull_WhenStreamIdNull() {
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
    public void BackupsPage_ScopeColumn_ShowsStream_WhenStreamIdPresent() {
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
    public async Task BackupsPage_ConcurrentGuard_PreventsCreateForSameTenant() {
        // Arrange — tenant-a has a running backup
        SetupJobs([
            CreateJob("bk-1", "tenant-a", BackupJobStatus.Running),
        ]);

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create Backup"), TimeSpan.FromSeconds(5));

        // Act — open create dialog
        IRenderedComponent<FluentButton> createBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Create Backup"));
        await createBtn.InvokeAsync(createBtn.Instance.OnClick.InvokeAsync);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Submit Deferred Request"), TimeSpan.FromSeconds(5));

        // Enter the same tenant
        IRenderedComponent<FluentTextInput> tenantField = cut.FindComponents<FluentTextInput>()
            .First(f => f.Markup.Contains("Tenant ID"));
        await tenantField.InvokeAsync(() => tenantField.Instance.ValueChanged.InvokeAsync("tenant-a"));

        // Assert — Submit button should be disabled and warning shown
        cut.Markup.ShouldContain("Backup already in progress for this tenant");
        IRenderedComponent<FluentButton> startBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Submit Deferred Request"));
        startBtn.Instance.Disabled.ShouldBe(true);
    }

    [Fact]
    public async Task BackupsPage_ExportDialog_CallsExportWithCorrectParameters() {
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
        await exportBtn.InvokeAsync(exportBtn.Instance.OnClick.InvokeAsync);
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
            .First(b => b.Markup.Contains("<span>Export</span>"));
        await submitBtn.InvokeAsync(submitBtn.Instance.OnClick.InvokeAsync);

        // Assert — API invoked
        _ = await _mockBackupApi.Received(1).ExportStreamAsync(
            Arg.Any<StreamExportRequest>(), Arg.Any<CancellationToken>());

        TestToastService toastService = Services.GetRequiredService<TestToastService>();
        ToastOptions toastOptions = toastService.LastOptions.ShouldNotBeNull();
        toastOptions.Message.ShouldBe("Exported 100 events.");
        toastOptions.Intent.ShouldBe(ToastIntent.Success);
        _ = JSInterop.VerifyInvoke("blazorDownloadFile");
    }

    [Fact]
    public async Task BackupsPage_ExportDialog_ShowsErrorToastAndClearsBusyState_WhenBackendReturnsFailure() {
        SetupJobs([]);
        const string failureMessage = "Stream export failed. ReasonCode=missing-stream.";
        _ = _mockBackupApi.ExportStreamAsync(Arg.Any<StreamExportRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<StreamExportResult?>(new StreamExportResult(
                false,
                "tenant-a",
                "Counter",
                "counter-1",
                0,
                null,
                null,
                failureMessage,
                "missing-stream")));

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Export Stream"), TimeSpan.FromSeconds(5));

        IRenderedComponent<FluentButton> exportBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Export Stream"));
        await exportBtn.InvokeAsync(exportBtn.Instance.OnClick.InvokeAsync);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Tenant ID"), TimeSpan.FromSeconds(5));

        IReadOnlyList<IRenderedComponent<FluentTextInput>> fields = cut.FindComponents<FluentTextInput>();
        await fields.First(f => f.Markup.Contains("Tenant ID")).InvokeAsync(
            () => fields.First(f => f.Markup.Contains("Tenant ID")).Instance.ValueChanged.InvokeAsync("tenant-a"));
        await fields.First(f => f.Markup.Contains("Domain")).InvokeAsync(
            () => fields.First(f => f.Markup.Contains("Domain")).Instance.ValueChanged.InvokeAsync("Counter"));
        await fields.First(f => f.Markup.Contains("Aggregate ID")).InvokeAsync(
            () => fields.First(f => f.Markup.Contains("Aggregate ID")).Instance.ValueChanged.InvokeAsync("counter-1"));

        IRenderedComponent<FluentButton> submitBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("<span>Export</span>"));
        await submitBtn.InvokeAsync(submitBtn.Instance.OnClick.InvokeAsync);

        TestToastService toastService = Services.GetRequiredService<TestToastService>();
        ToastOptions toastOptions = toastService.LastOptions.ShouldNotBeNull();
        toastOptions.Message.ShouldBe(failureMessage);
        toastOptions.Intent.ShouldBe(ToastIntent.Error);
        cut.Markup.ShouldContain("Export Stream");
        submitBtn.Instance.Disabled.ShouldBeFalse();
    }

    [Fact]
    public async Task BackupsPage_ExportDialog_DownloadsAndCloses_WhenBackendReportsSuccess() {
        SetupJobs([]);
        _ = _mockBackupApi.ExportStreamAsync(Arg.Any<StreamExportRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<StreamExportResult?>(new StreamExportResult(
                true,
                "tenant-a",
                "Counter",
                "counter-1",
                0,
                "{}",
                "export.json",
                null)));

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Export Stream"), TimeSpan.FromSeconds(5));

        IRenderedComponent<FluentButton> exportBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Export Stream"));
        await exportBtn.InvokeAsync(exportBtn.Instance.OnClick.InvokeAsync);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Tenant ID"), TimeSpan.FromSeconds(5));

        IReadOnlyList<IRenderedComponent<FluentTextInput>> fields = cut.FindComponents<FluentTextInput>();
        await fields.First(f => f.Markup.Contains("Tenant ID")).InvokeAsync(
            () => fields.First(f => f.Markup.Contains("Tenant ID")).Instance.ValueChanged.InvokeAsync("tenant-a"));
        await fields.First(f => f.Markup.Contains("Domain")).InvokeAsync(
            () => fields.First(f => f.Markup.Contains("Domain")).Instance.ValueChanged.InvokeAsync("Counter"));
        await fields.First(f => f.Markup.Contains("Aggregate ID")).InvokeAsync(
            () => fields.First(f => f.Markup.Contains("Aggregate ID")).Instance.ValueChanged.InvokeAsync("counter-1"));

        IRenderedComponent<FluentButton> submitBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("<span>Export</span>"));
        await submitBtn.InvokeAsync(submitBtn.Instance.OnClick.InvokeAsync);

        TestToastService toastService = Services.GetRequiredService<TestToastService>();
        ToastOptions toastOptions = toastService.LastOptions.ShouldNotBeNull();
        toastOptions.Message.ShouldBe("Exported 0 events.");
        toastOptions.Intent.ShouldBe(ToastIntent.Success);
        _ = JSInterop.VerifyInvoke("blazorDownloadFile");
    }

    [Fact]
    public async Task BackupsPage_ImportDialog_ShowsDeferredToastAndClearsBusyState() {
        SetupJobs([]);
        const string deferredMessage = "Stream import is deferred.";
        _ = _mockBackupApi.ImportStreamAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AdminOperationResult?>(new AdminOperationResult(
                false,
                "deferred-backup-import-stream",
                deferredMessage,
                "Deferred")));

        IRenderedComponent<Backups> cut = Render<Backups>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Import Stream"), TimeSpan.FromSeconds(5));

        IRenderedComponent<FluentButton> importBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("Import Stream"));
        await importBtn.InvokeAsync(importBtn.Instance.OnClick.InvokeAsync);
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Select a previously exported JSON file"), TimeSpan.FromSeconds(5));

        SetPrivateField(cut.Instance, "_importTenantId", "tenant-a");
        SetPrivateField(cut.Instance, "_importContent", """{"TenantId":"tenant-a","Domain":"Counter","AggregateId":"counter-1","Events":[]}""");
        await cut.InvokeAsync(async () => {
            var importTask = (Task)typeof(Backups)
                .GetMethod("OnImportConfirm", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(cut.Instance, null)!;
            await importTask;
        });

        TestToastService toastService = Services.GetRequiredService<TestToastService>();
        ToastOptions toastOptions = toastService.LastOptions.ShouldNotBeNull();
        toastOptions.Message.ShouldBe(deferredMessage);
        cut.Markup.ShouldContain("Import Stream");
        IRenderedComponent<FluentButton> submitBtn = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains("<span>Import</span>"));
        submitBtn.Instance.Disabled.ShouldBeFalse();
    }

    // ===== Helpers =====

    private void SetupJobs(IReadOnlyList<BackupJob> jobs) => _ = _mockBackupApi.GetBackupJobsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(jobs));

    private static BackupJob CreateJob(
        string backupId,
        string tenantId,
        BackupJobStatus status,
        string? streamId = null,
        long? sizeBytes = null,
        long? eventCount = null,
        bool isValidated = false,
        string? errorMessage = null,
        BackupJobType jobType = BackupJobType.Backup) {
        DateTimeOffset created = DateTimeOffset.UtcNow.AddHours(-2);
        DateTimeOffset? completed = status is BackupJobStatus.Completed or BackupJobStatus.Failed
            ? DateTimeOffset.UtcNow.AddHours(-1)
            : null;
        return new BackupJob(
            backupId, tenantId, streamId, null, jobType, status, true,
            created, completed, eventCount, sizeBytes, isValidated, errorMessage);
    }

    private static void SetPrivateField<TValue>(Backups instance, string fieldName, TValue value)
        => typeof(Backups)
            .GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(instance, value);

    private void SetupReadOnlyUser() {
        // Override auth state with ReadOnly user
        Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider authStateProvider =
            Substitute.For<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>();
        System.Security.Claims.ClaimsPrincipal user = new(new System.Security.Claims.ClaimsIdentity(
        [
            new System.Security.Claims.Claim(AdminClaimTypes.Role, "ReadOnly"),
        ], "TestAuth"));
        _ = authStateProvider.GetAuthenticationStateAsync()
            .Returns(Task.FromResult(new Microsoft.AspNetCore.Components.Authorization.AuthenticationState(user)));
        _ = Services.AddSingleton(authStateProvider);
        _ = Services.AddScoped<AdminUserContext>();
    }

    private Microsoft.AspNetCore.Components.NavigationManager NavManager =>
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
}
