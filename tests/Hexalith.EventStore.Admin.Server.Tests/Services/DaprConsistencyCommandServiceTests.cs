#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Consistency;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprConsistencyCommandServiceTests {
    private const string StateStoreName = "statestore";

    private static DaprConsistencyCommandService CreateService(
        DaprClient? daprClient = null,
        IStreamQueryService? streamQueryService = null) {
        daprClient ??= Substitute.For<DaprClient>();
        streamQueryService ??= Substitute.For<IStreamQueryService>();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            StateStoreName = StateStoreName,
        });

        return new DaprConsistencyCommandService(
            daprClient,
            streamQueryService,
            options,
            NullLogger<DaprConsistencyCommandService>.Instance);
    }

    [Fact]
    public async Task TriggerCheck_StoresCheckRecord_WithPendingStatus() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();

        // Empty index — no active checks
        _ = daprClient.GetStateAsync<List<string>>(
                StateStoreName, "admin:consistency:index", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<string>?)null);

        // ETag-based index save
        _ = daprClient.GetStateAndETagAsync<List<string>>(
                StateStoreName, "admin:consistency:index", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (new List<string>(), string.Empty));

        _ = daprClient.TrySaveStateAsync(
                StateStoreName, "admin:consistency:index", Arg.Any<List<string>>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);

        AdminOperationResult result = await service.TriggerCheckAsync(
            "tenant-a", null, [ConsistencyCheckType.SequenceContinuity]);

        result.Success.ShouldBeTrue();
        result.OperationId.ShouldNotBeNullOrWhiteSpace();

        // Verify state was saved with the check ID key
        await daprClient.Received().SaveStateAsync(
            StateStoreName,
            Arg.Is<string>(k => k.StartsWith("admin:consistency:")),
            Arg.Any<ConsistencyCheckResult>(),
            metadata: Arg.Any<Dictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerCheck_ReturnsConflict_WhenActiveCheckExists() {
        DaprClient daprClient = Substitute.For<DaprClient>();

        // Index with one active check
        List<string> index = ["active-check"];
        _ = daprClient.GetStateAsync<List<string>>(
                StateStoreName, "admin:consistency:index", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => index);

        ConsistencyCheckResult activeCheck = new(
            "active-check",
            ConsistencyCheckStatus.Running,
            "tenant-a",
            null,
            [ConsistencyCheckType.SequenceContinuity],
            DateTimeOffset.UtcNow.AddMinutes(-5),
            null,
            DateTimeOffset.UtcNow.AddMinutes(25), // Not timed out
            10,
            0,
            [],
            false,
            null);

        _ = daprClient.GetStateAsync<ConsistencyCheckResult>(
                StateStoreName, "admin:consistency:active-check", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => activeCheck);

        DaprConsistencyCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.TriggerCheckAsync(
            "tenant-a", null, [ConsistencyCheckType.SequenceContinuity]);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("Conflict");
    }

    [Fact]
    public async Task TriggerCheck_ReturnsOperationResult_WithCheckId() {
        DaprClient daprClient = Substitute.For<DaprClient>();

        _ = daprClient.GetStateAsync<List<string>>(
                StateStoreName, "admin:consistency:index", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<string>?)null);

        _ = daprClient.GetStateAndETagAsync<List<string>>(
                StateStoreName, "admin:consistency:index", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (new List<string>(), string.Empty));

        _ = daprClient.TrySaveStateAsync(
                StateStoreName, "admin:consistency:index", Arg.Any<List<string>>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);

        DaprConsistencyCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.TriggerCheckAsync(
            "tenant-a", "orders", [ConsistencyCheckType.MetadataConsistency]);

        result.Success.ShouldBeTrue();
        result.OperationId.Length.ShouldBe(26); // ULID is 26 chars
    }

    [Fact]
    public async Task TriggerCheck_AppendsToIndex_WithETag() {
        DaprClient daprClient = Substitute.For<DaprClient>();

        _ = daprClient.GetStateAsync<List<string>>(
                StateStoreName, "admin:consistency:index", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<string>?)null);

        List<string> existingIndex = ["old-check"];
        _ = daprClient.GetStateAndETagAsync<List<string>>(
                StateStoreName, "admin:consistency:index", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (existingIndex, "etag-123"));

        List<string>? savedIndex = null;
        _ = daprClient.TrySaveStateAsync(
                StateStoreName, "admin:consistency:index", Arg.Do<List<string>>(i => savedIndex = i), "etag-123", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);

        DaprConsistencyCommandService service = CreateService(daprClient);

        _ = await service.TriggerCheckAsync(
            "tenant-a", null, [ConsistencyCheckType.SequenceContinuity]);

        _ = savedIndex.ShouldNotBeNull();
        savedIndex.Count.ShouldBe(2); // new check + old-check
        savedIndex[1].ShouldBe("old-check"); // old check moved to position 1
    }

    [Fact]
    public async Task CancelCheck_UpdatesStatus_WhenRunning() {
        DaprClient daprClient = Substitute.For<DaprClient>();

        ConsistencyCheckResult runningCheck = new(
            "check-1",
            ConsistencyCheckStatus.Running,
            "tenant-a",
            null,
            [ConsistencyCheckType.SequenceContinuity],
            DateTimeOffset.UtcNow.AddMinutes(-5),
            null,
            DateTimeOffset.UtcNow.AddMinutes(25),
            10,
            0,
            [],
            false,
            null);

        _ = daprClient.GetStateAsync<ConsistencyCheckResult>(
                StateStoreName, "admin:consistency:check-1", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => runningCheck);

        DaprConsistencyCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.CancelCheckAsync("check-1");

        result.Success.ShouldBeTrue();

        await daprClient.Received().SaveStateAsync(
            StateStoreName,
            "admin:consistency:check-1",
            Arg.Is<ConsistencyCheckResult>(r => r.Status == ConsistencyCheckStatus.Cancelled),
            metadata: Arg.Any<Dictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelCheck_ReturnsError_WhenCompleted() {
        DaprClient daprClient = Substitute.For<DaprClient>();

        ConsistencyCheckResult completedCheck = new(
            "check-1",
            ConsistencyCheckStatus.Completed,
            "tenant-a",
            null,
            [ConsistencyCheckType.SequenceContinuity],
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(30),
            50,
            2,
            [],
            false,
            null);

        _ = daprClient.GetStateAsync<ConsistencyCheckResult>(
                StateStoreName, "admin:consistency:check-1", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => completedCheck);

        DaprConsistencyCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.CancelCheckAsync("check-1");

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("InvalidOperation");
    }

    [Fact]
    public async Task CancelCheck_ReturnsNotFound_WhenMissing() {
        DaprClient daprClient = Substitute.For<DaprClient>();

        _ = daprClient.GetStateAsync<ConsistencyCheckResult>(
                StateStoreName, "admin:consistency:nonexistent", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (ConsistencyCheckResult?)null);

        DaprConsistencyCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.CancelCheckAsync("nonexistent");

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("NotFound");
    }
}
