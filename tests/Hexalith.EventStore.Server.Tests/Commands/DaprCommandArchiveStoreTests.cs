
using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

public class DaprCommandArchiveStoreTests {
    private readonly DaprClient _daprClient = Substitute.For<DaprClient>();
    private readonly IOptions<CommandStatusOptions> _options = Options.Create(new CommandStatusOptions());

    private DaprCommandArchiveStore CreateStore() => new(_daprClient, _options, NullLogger<DaprCommandArchiveStore>.Instance);

    private static ArchivedCommand CreateTestCommand() => new(
        Tenant: "tenant-a",
        Domain: "orders",
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        Extensions: null,
        OriginalTimestamp: DateTimeOffset.UtcNow);

    [Fact]
    public async Task WriteCommandAsync_ValidCommand_CallsSaveStateWithCorrectKey() {
        // Arrange
        DaprCommandArchiveStore store = CreateStore();
        ArchivedCommand command = CreateTestCommand();

        // Act
        await store.WriteCommandAsync("tenant-a", "corr-123", command, CancellationToken.None);

        // Assert
        await _daprClient.Received(1).SaveStateAsync(
            "statestore",
            "tenant-a:corr-123:command",
            command,
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Is<IReadOnlyDictionary<string, string>>(m => m.ContainsKey("ttlInSeconds")),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteCommandAsync_IncludesTtlMetadata_Default86400Seconds() {
        // Arrange
        DaprCommandArchiveStore store = CreateStore();
        ArchivedCommand command = CreateTestCommand();

        // Act
        await store.WriteCommandAsync("tenant-a", "corr-123", command, CancellationToken.None);

        // Assert
        await _daprClient.Received(1).SaveStateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ArchivedCommand>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Is<IReadOnlyDictionary<string, string>>(m => m["ttlInSeconds"] == "86400"),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteCommandAsync_DaprClientThrows_PropagatesException() {
        // Arrange
        DaprCommandArchiveStore store = CreateStore();
        ArchivedCommand command = CreateTestCommand();

        _ = _daprClient.SaveStateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ArchivedCommand>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Dapr sidecar unavailable"));

        // Act & Assert - exception propagates to caller (advisory handling is caller's responsibility)
        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => store.WriteCommandAsync("tenant-a", "corr-123", command, CancellationToken.None));
    }

    [Fact]
    public async Task ReadCommandAsync_ExistingKey_ReturnsArchivedCommand() {
        // Arrange
        DaprCommandArchiveStore store = CreateStore();
        ArchivedCommand expected = CreateTestCommand();

        _ = _daprClient.GetStateAsync<ArchivedCommand>(
            "statestore",
            "tenant-a:corr-123:command",
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        ArchivedCommand? result = await store.ReadCommandAsync("tenant-a", "corr-123", CancellationToken.None);

        // Assert
        _ = result.ShouldNotBeNull();
        result.ShouldBe(expected);
    }

    [Fact]
    public async Task ReadCommandAsync_NonExistentKey_ReturnsNull() {
        // Arrange
        DaprCommandArchiveStore store = CreateStore();

        _ = _daprClient.GetStateAsync<ArchivedCommand>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns((ArchivedCommand)null!);

        // Act
        ArchivedCommand? result = await store.ReadCommandAsync("tenant-a", "corr-missing", CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ReadCommandAsync_DaprClientThrows_LogsWarningAndReturnsNull() {
        // Arrange
        DaprCommandArchiveStore store = CreateStore();

        _ = _daprClient.GetStateAsync<ArchivedCommand>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            consistencyMode: Arg.Any<ConsistencyMode?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Dapr sidecar unavailable"));

        // Act
        ArchivedCommand? result = await store.ReadCommandAsync("tenant-a", "corr-123", CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }
}
