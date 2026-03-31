using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Commands;
using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Commands;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Commands;

public class DaprCommandActivityTrackerTests {
    private const string ActivityIndexKey = "admin:command-activity:all";
    private readonly DaprClient _daprClient = Substitute.For<DaprClient>();

    private DaprCommandActivityTracker CreateTracker()
        => new(_daprClient, Options.Create(new CommandStatusOptions()), NullLogger<DaprCommandActivityTracker>.Instance);

    private static CommandSummary MakeSummary(
        string correlationId,
        CommandStatus status,
        string tenantId = "tenant-a",
        string domain = "Counter",
        string aggregateId = "agg-1",
        string commandType = "CreateCounter",
        DateTimeOffset? timestamp = null)
        => new(tenantId, domain, aggregateId, correlationId, commandType,
            status, timestamp ?? DateTimeOffset.UtcNow, 1, null);

    [Fact]
    public async Task TrackAsync_ValidCommand_SavesStateToGlobalIndex() {
        DaprCommandActivityTracker tracker = CreateTracker();
        SetupGetStateAndEtag(ActivityIndexKey, null, "etag-1");
        SetupTrySave(ActivityIndexKey, true);

        await tracker.TrackAsync(
            "tenant-a", "Counter", "agg-1", "corr-1", "CreateCounter",
            CommandStatus.Received, DateTimeOffset.UtcNow, null, null);

        await _daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            ActivityIndexKey,
            Arg.Is<List<CommandSummary>>(items => items.Count == 1 && items[0].TenantId == "tenant-a"),
            "etag-1",
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackAsync_EtagMismatch_RetriesUntilSaveSucceeds() {
        DaprCommandActivityTracker tracker = CreateTracker();
        _ = _daprClient.GetStateAndETagAsync<List<CommandSummary>>(
            "statestore",
            ActivityIndexKey,
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(
                ((List<CommandSummary>, string))([], "etag-1"),
                ((List<CommandSummary>, string))([], "etag-2"));

        _ = _daprClient.TrySaveStateAsync(
            "statestore",
            ActivityIndexKey,
            Arg.Any<List<CommandSummary>>(),
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(false, true);

        await tracker.TrackAsync(
            "tenant-a", "Counter", "agg-1", "corr-1", "CreateCounter",
            CommandStatus.Received, DateTimeOffset.UtcNow, null, null);

        await _daprClient.Received(2).TrySaveStateAsync(
            "statestore",
            ActivityIndexKey,
            Arg.Any<List<CommandSummary>>(),
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackAsync_SameCorrelationDifferentTenant_PreservesBothEntries() {
        DaprCommandActivityTracker tracker = CreateTracker();
        List<CommandSummary> existing = [MakeSummary("corr-1", CommandStatus.Completed, tenantId: "tenant-b")];
        SetupGetStateAndEtag(ActivityIndexKey, existing, "etag-1");
        SetupTrySave(ActivityIndexKey, true);

        await tracker.TrackAsync(
            "tenant-a", "Counter", "agg-1", "corr-1", "CreateCounter",
            CommandStatus.Received, DateTimeOffset.UtcNow, null, null);

        await _daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            ActivityIndexKey,
            Arg.Is<List<CommandSummary>>(items => items.Count == 2 && items.Any(x => x.TenantId == "tenant-a") && items.Any(x => x.TenantId == "tenant-b")),
            "etag-1",
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackAsync_DaprClientThrows_DoesNotPropagate() {
        DaprCommandActivityTracker tracker = CreateTracker();
        _ = _daprClient.GetStateAndETagAsync<List<CommandSummary>>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Dapr sidecar unavailable"));

        await tracker.TrackAsync(
            "tenant-a", "Counter", "agg-1", "corr-1", "CreateCounter",
            CommandStatus.Received, DateTimeOffset.UtcNow, null, null);
    }

    [Fact]
    public async Task GetRecentCommandsAsync_CompletedFilter_ReturnsOnlyCompleted() {
        DaprCommandActivityTracker tracker = CreateTracker();
        List<CommandSummary> data =
        [
            MakeSummary("c1", CommandStatus.Completed),
            MakeSummary("c2", CommandStatus.Received),
            MakeSummary("c3", CommandStatus.Completed),
        ];
        SetupGetState(ActivityIndexKey, data);

        PagedResult<CommandSummary> result = await tracker.GetRecentCommandsAsync(null, "completed", null);

        result.Items.Count.ShouldBe(2);
        result.Items.ShouldAllBe(c => c.Status == CommandStatus.Completed);
    }

    [Fact]
    public async Task GetRecentCommandsAsync_ProcessingFilter_ReturnsMultiStatusGroup() {
        DaprCommandActivityTracker tracker = CreateTracker();
        List<CommandSummary> data =
        [
            MakeSummary("c1", CommandStatus.Received),
            MakeSummary("c2", CommandStatus.Processing),
            MakeSummary("c3", CommandStatus.EventsStored),
            MakeSummary("c4", CommandStatus.EventsPublished),
            MakeSummary("c5", CommandStatus.Completed),
            MakeSummary("c6", CommandStatus.Rejected),
        ];
        SetupGetState(ActivityIndexKey, data);

        PagedResult<CommandSummary> result = await tracker.GetRecentCommandsAsync(null, "processing", null);

        result.Items.Count.ShouldBe(4);
        CommandStatus[] expected = [CommandStatus.Received, CommandStatus.Processing, CommandStatus.EventsStored, CommandStatus.EventsPublished];
        result.Items.ShouldAllBe(c => expected.Contains(c.Status));
    }

    [Fact]
    public async Task GetRecentCommandsAsync_RejectedFilter_ReturnsOnlyRejected() {
        DaprCommandActivityTracker tracker = CreateTracker();
        List<CommandSummary> data =
        [
            MakeSummary("c1", CommandStatus.Rejected),
            MakeSummary("c2", CommandStatus.Completed),
        ];
        SetupGetState(ActivityIndexKey, data);

        PagedResult<CommandSummary> result = await tracker.GetRecentCommandsAsync(null, "rejected", null);

        result.Items.Count.ShouldBe(1);
        result.Items[0].Status.ShouldBe(CommandStatus.Rejected);
    }

    [Fact]
    public async Task GetRecentCommandsAsync_FailedFilter_ReturnsMultiStatusGroup() {
        DaprCommandActivityTracker tracker = CreateTracker();
        List<CommandSummary> data =
        [
            MakeSummary("c1", CommandStatus.PublishFailed),
            MakeSummary("c2", CommandStatus.TimedOut),
            MakeSummary("c3", CommandStatus.Completed),
        ];
        SetupGetState(ActivityIndexKey, data);

        PagedResult<CommandSummary> result = await tracker.GetRecentCommandsAsync(null, "failed", null);

        result.Items.Count.ShouldBe(2);
        CommandStatus[] expected = [CommandStatus.PublishFailed, CommandStatus.TimedOut];
        result.Items.ShouldAllBe(c => expected.Contains(c.Status));
    }

    [Fact]
    public async Task GetRecentCommandsAsync_RawEnumFilter_FallbackParsesStatus() {
        DaprCommandActivityTracker tracker = CreateTracker();
        List<CommandSummary> data =
        [
            MakeSummary("c1", CommandStatus.EventsStored),
            MakeSummary("c2", CommandStatus.Completed),
        ];
        SetupGetState(ActivityIndexKey, data);

        PagedResult<CommandSummary> result = await tracker.GetRecentCommandsAsync(null, "EventsStored", null);

        result.Items.Count.ShouldBe(1);
        result.Items[0].Status.ShouldBe(CommandStatus.EventsStored);
    }

    [Fact]
    public async Task GetRecentCommandsAsync_TenantFilter_ReadsGlobalIndex() {
        DaprCommandActivityTracker tracker = CreateTracker();
        List<CommandSummary> data =
        [
            MakeSummary("c1", CommandStatus.Completed, "tenant-a"),
            MakeSummary("c2", CommandStatus.Completed, "tenant-b"),
        ];
        SetupGetState(ActivityIndexKey, data);

        PagedResult<CommandSummary> result = await tracker.GetRecentCommandsAsync("tenant-a", null, null);

        result.Items.ShouldAllBe(c => c.TenantId == "tenant-a");
    }

    [Fact]
    public async Task GetRecentCommandsAsync_BlankTenantFilter_BehavesLikeNoFilter() {
        DaprCommandActivityTracker tracker = CreateTracker();
        List<CommandSummary> data =
        [
            MakeSummary("c1", CommandStatus.Completed, "tenant-a"),
            MakeSummary("c2", CommandStatus.Completed, "tenant-b"),
        ];
        SetupGetState(ActivityIndexKey, data);

        PagedResult<CommandSummary> result = await tracker.GetRecentCommandsAsync("   ", null, null);

        result.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetRecentCommandsAsync_CommandTypeFilter_FiltersCorrectly() {
        DaprCommandActivityTracker tracker = CreateTracker();
        List<CommandSummary> data =
        [
            MakeSummary("c1", CommandStatus.Completed, commandType: "CreateCounter"),
            MakeSummary("c2", CommandStatus.Completed, commandType: "DeleteCounter"),
        ];
        SetupGetState(ActivityIndexKey, data);

        PagedResult<CommandSummary> result = await tracker.GetRecentCommandsAsync(null, null, " Create ");

        result.Items.Count.ShouldBe(1);
        result.Items[0].CommandType.ShouldBe("CreateCounter");
    }

    [Fact]
    public async Task GetRecentCommandsAsync_EmptyState_ReturnsEmptyResult() {
        DaprCommandActivityTracker tracker = CreateTracker();
        SetupGetState(ActivityIndexKey, (List<CommandSummary>?)null);

        PagedResult<CommandSummary> result = await tracker.GetRecentCommandsAsync(null, null, null);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetRecentCommandsAsync_DaprClientThrows_ReturnsEmptyResult() {
        DaprCommandActivityTracker tracker = CreateTracker();
        _ = _daprClient.GetStateAsync<List<CommandSummary>>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Dapr unavailable"));

        PagedResult<CommandSummary> result = await tracker.GetRecentCommandsAsync(null, null, null);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public void CommandSummary_SerializationRoundTrip_PreservesAllProperties() {
        var original = new CommandSummary(
            "tenant-a",
            "Counter",
            "agg-123",
            "corr-456",
            "Hexalith.Samples.Counter.Commands.IncrementCounter",
            CommandStatus.Completed,
            new DateTimeOffset(2026, 3, 30, 12, 0, 0, TimeSpan.Zero),
            3,
            null);

        string json = JsonSerializer.Serialize(original);
        CommandSummary? deserialized = JsonSerializer.Deserialize<CommandSummary>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized.TenantId.ShouldBe(original.TenantId);
        deserialized.Domain.ShouldBe(original.Domain);
        deserialized.AggregateId.ShouldBe(original.AggregateId);
        deserialized.CorrelationId.ShouldBe(original.CorrelationId);
        deserialized.CommandType.ShouldBe(original.CommandType);
        deserialized.Status.ShouldBe(original.Status);
        deserialized.Timestamp.ShouldBe(original.Timestamp);
        deserialized.EventCount.ShouldBe(original.EventCount);
        deserialized.FailureReason.ShouldBe(original.FailureReason);
    }

    [Fact]
    public void CommandSummary_SerializationRoundTrip_WithFailureReason() {
        var original = new CommandSummary(
            "tenant-b",
            "Order",
            "order-789",
            "corr-fail",
            "PlaceOrder",
            CommandStatus.PublishFailed,
            new DateTimeOffset(2026, 3, 30, 14, 30, 0, TimeSpan.Zero),
            null,
            "Pub/sub broker unavailable");

        string json = JsonSerializer.Serialize(original);
        CommandSummary? deserialized = JsonSerializer.Deserialize<CommandSummary>(json);

        _ = deserialized.ShouldNotBeNull();
        deserialized.Status.ShouldBe(CommandStatus.PublishFailed);
        deserialized.FailureReason.ShouldBe("Pub/sub broker unavailable");
        deserialized.EventCount.ShouldBeNull();
    }

    private void SetupGetState<T>(string key, T value) {
        _ = _daprClient.GetStateAsync<T>(
            "statestore",
            key,
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(value);
    }

    private void SetupGetStateAndEtag(string key, List<CommandSummary>? value, string etag) {
        _ = _daprClient.GetStateAndETagAsync<List<CommandSummary>>(
            "statestore",
            key,
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(((List<CommandSummary>, string))(value!, etag));
    }

    private void SetupTrySave(string key, bool result) {
        _ = _daprClient.TrySaveStateAsync(
            "statestore",
            key,
            Arg.Any<List<CommandSummary>>(),
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(result);
    }
}
