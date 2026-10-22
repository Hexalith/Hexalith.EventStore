#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprBackupQueryServiceTests {
    private const string StateStoreName = "statestore";

    private static DaprBackupQueryService CreateService(DaprClient? daprClient = null) {
        daprClient ??= Substitute.For<DaprClient>();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            StateStoreName = StateStoreName,
        });

        return new DaprBackupQueryService(
            daprClient,
            options,
            NullLogger<DaprBackupQueryService>.Instance);
    }

    [Fact]
    public async Task GetBackupJobsAsync_ReturnsTenantJobs_WhenTenantIdProvided() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var jobs = new List<BackupJob>
        {
            new("backup-1", "tenant-a", null, "nightly", BackupJobType.Backup, BackupJobStatus.Completed,
                true, DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow, 1000, 50000, true, null),
        };

        _ = daprClient.GetStateAsync<IReadOnlyList<BackupJob>>(
            StateStoreName,
            "admin:backup-jobs:tenant-a",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<BackupJob>?)jobs);

        DaprBackupQueryService service = CreateService(daprClient);

        IReadOnlyList<BackupJob> result = await service.GetBackupJobsAsync("tenant-a");

        result.Count.ShouldBe(1);
        result[0].BackupId.ShouldBe("backup-1");
        result[0].TenantId.ShouldBe("tenant-a");
    }

    [Fact]
    public async Task GetBackupJobsAsync_ReturnsAllJobs_WhenTenantIdIsNull() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var jobs = new List<BackupJob>
        {
            new("backup-1", "tenant-a", null, null, BackupJobType.Backup, BackupJobStatus.Completed,
                true, DateTimeOffset.UtcNow, null, null, null, false, null),
            new("backup-2", "tenant-b", null, null, BackupJobType.Backup, BackupJobStatus.Running,
                false, DateTimeOffset.UtcNow, null, null, null, false, null),
        };

        _ = daprClient.GetStateAsync<IReadOnlyList<BackupJob>>(
            StateStoreName,
            "admin:backup-jobs:all",
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<BackupJob>?)jobs);

        DaprBackupQueryService service = CreateService(daprClient);

        IReadOnlyList<BackupJob> result = await service.GetBackupJobsAsync(null);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetBackupJobsAsync_ReturnsEmpty_WhenIndexNotFound() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<IReadOnlyList<BackupJob>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<BackupJob>?)null);

        DaprBackupQueryService service = CreateService(daprClient);

        IReadOnlyList<BackupJob> result = await service.GetBackupJobsAsync("tenant-a");

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetBackupJobsAsync_Throws_WhenExceptionThrown() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<IReadOnlyList<BackupJob>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("State store down"));

        DaprBackupQueryService service = CreateService(daprClient);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => service.GetBackupJobsAsync("tenant-a"));
    }

    [Fact]
    public async Task GetBackupJobsAsync_PropagatesCancellation() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<IReadOnlyList<BackupJob>>(
            StateStoreName,
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<BackupJob>?>(_ => throw new OperationCanceledException());

        DaprBackupQueryService service = CreateService(daprClient);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => service.GetBackupJobsAsync("tenant-a", cts.Token));
    }
}
