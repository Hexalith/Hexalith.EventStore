
using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

public class DaprCommandStatusStoreTests {
    private readonly DaprClient _daprClient = Substitute.For<DaprClient>();
    private readonly IOptions<CommandStatusOptions> _options = Options.Create(new CommandStatusOptions());
    private readonly ILogger<DaprCommandStatusStore> _logger = NullLogger<DaprCommandStatusStore>.Instance;

    private DaprCommandStatusStore CreateStore() => new(_daprClient, _options, _logger);

    [Fact]
    public async Task WriteStatusAsync_ValidStatus_CallsSaveStateWithCorrectKey() {
        // Arrange
        DaprCommandStatusStore store = CreateStore();
        var record = new CommandStatusRecord(CommandStatus.Received, DateTimeOffset.UtcNow, "agg-1", null, null, null, null);

        // Act
        await store.WriteStatusAsync("tenant-a", "corr-123", record, CancellationToken.None);

        // Assert
        await _daprClient.Received(1).SaveStateAsync(
            "statestore",
            "tenant-a:corr-123:status",
            record,
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Is<IReadOnlyDictionary<string, string>>(m => m.ContainsKey("ttlInSeconds")),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteStatusAsync_IncludesTtlMetadata_Default86400Seconds() {
        // Arrange
        DaprCommandStatusStore store = CreateStore();
        var record = new CommandStatusRecord(CommandStatus.Received, DateTimeOffset.UtcNow, "agg-1", null, null, null, null);

        // Act
        await store.WriteStatusAsync("tenant-a", "corr-123", record, CancellationToken.None);

        // Assert
        await _daprClient.Received(1).SaveStateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CommandStatusRecord>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Is<IReadOnlyDictionary<string, string>>(m => m["ttlInSeconds"] == "86400"),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteStatusAsync_DaprClientThrows_PropagatesException() {
        // Arrange
        DaprCommandStatusStore store = CreateStore();
        var record = new CommandStatusRecord(CommandStatus.Received, DateTimeOffset.UtcNow, "agg-1", null, null, null, null);

        _ = _daprClient.SaveStateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CommandStatusRecord>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Dapr sidecar unavailable"));

        // Act & Assert - exception propagates to caller (advisory handling is caller's responsibility)
        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => store.WriteStatusAsync("tenant-a", "corr-123", record, CancellationToken.None));
    }

    [Fact]
    public async Task ReadStatusAsync_ExistingKey_ReturnsRecord() {
        // Arrange
        DaprCommandStatusStore store = CreateStore();
        var expected = new CommandStatusRecord(CommandStatus.Received, DateTimeOffset.UtcNow, "agg-1", null, null, null, null);

        _ = _daprClient.GetStateAsync<CommandStatusRecord>(
            "statestore",
            "tenant-a:corr-123:status",
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        CommandStatusRecord? result = await store.ReadStatusAsync("tenant-a", "corr-123", CancellationToken.None);

        // Assert
        _ = result.ShouldNotBeNull();
        result.ShouldBe(expected);
    }

    [Fact]
    public async Task ReadStatusAsync_NonExistentKey_ReturnsNull() {
        // Arrange
        DaprCommandStatusStore store = CreateStore();

        _ = _daprClient.GetStateAsync<CommandStatusRecord>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns((CommandStatusRecord)null!);

        // Act
        CommandStatusRecord? result = await store.ReadStatusAsync("tenant-a", "corr-missing", CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ReadStatusAsync_DaprClientThrows_LogsWarningAndReturnsNull() {
        // Arrange
        DaprCommandStatusStore store = CreateStore();

        _ = _daprClient.GetStateAsync<CommandStatusRecord>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Dapr sidecar unavailable"));

        // Act
        CommandStatusRecord? result = await store.ReadStatusAsync("tenant-a", "corr-123", CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }
}
