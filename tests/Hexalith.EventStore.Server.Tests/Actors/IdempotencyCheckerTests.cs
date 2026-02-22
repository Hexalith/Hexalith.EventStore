
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Actors;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class IdempotencyCheckerTests {
    private readonly IActorStateManager _stateManager = Substitute.For<IActorStateManager>();
    private readonly ILogger<IdempotencyChecker> _logger = Substitute.For<ILogger<IdempotencyChecker>>();

    private IdempotencyChecker CreateChecker() => new(_stateManager, _logger);

    [Fact]
    public async Task CheckAsync_NoExistingRecord_ReturnsNull() {
        // Arrange
        _ = _stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
        IdempotencyChecker checker = CreateChecker();

        // Act
        CommandProcessingResult? result = await checker.CheckAsync("cause-123");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task CheckAsync_ExistingRecord_ReturnsCachedResult() {
        // Arrange
        var record = new IdempotencyRecord("cause-123", "corr-456", true, null, DateTimeOffset.UtcNow);
        _ = _stateManager.TryGetStateAsync<IdempotencyRecord>("idempotency:cause-123", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, record));
        IdempotencyChecker checker = CreateChecker();

        // Act
        CommandProcessingResult? result = await checker.CheckAsync("cause-123");

        // Assert
        _ = result.ShouldNotBeNull();
        result.Accepted.ShouldBeTrue();
        result.CorrelationId.ShouldBe("corr-456");
    }

    [Fact]
    public async Task RecordAsync_StoresIdempotencyRecord() {
        // Arrange
        IdempotencyChecker checker = CreateChecker();
        var processingResult = new CommandProcessingResult(Accepted: true, CorrelationId: "corr-789");

        // Act
        await checker.RecordAsync("cause-abc", processingResult);

        // Assert
        await _stateManager.Received(1).SetStateAsync(
            "idempotency:cause-abc",
            Arg.Is<IdempotencyRecord>(r =>
                r.CausationId == "cause-abc" &&
                r.CorrelationId == "corr-789" &&
                r.Accepted),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_DoesNotCallSaveState() {
        // Arrange
        IdempotencyChecker checker = CreateChecker();
        var processingResult = new CommandProcessingResult(Accepted: true, CorrelationId: "corr-1");

        // Act
        await checker.RecordAsync("cause-1", processingResult);

        // Assert
        await _stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAsync_NullCausationId_ThrowsArgumentException() {
        // Arrange
        IdempotencyChecker checker = CreateChecker();

        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentException>(() => checker.CheckAsync(null!));
    }

    [Fact]
    public async Task CheckAsync_WhitespaceCausationId_ThrowsArgumentException() {
        // Arrange
        IdempotencyChecker checker = CreateChecker();

        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentException>(() => checker.CheckAsync("   "));
    }

    [Fact]
    public async Task RecordAsync_NullCausationId_ThrowsArgumentException() {
        // Arrange
        IdempotencyChecker checker = CreateChecker();
        var result = new CommandProcessingResult(Accepted: true);

        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentException>(() => checker.RecordAsync(null!, result));
    }

    [Fact]
    public async Task RecordAsync_NullResult_ThrowsArgumentNullException() {
        // Arrange
        IdempotencyChecker checker = CreateChecker();

        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentNullException>(() => checker.RecordAsync("cause-1", null!));
    }

    [Fact]
    public async Task CheckAsync_CorrectKeyFormat_UsesIdempotencyPrefix() {
        // Arrange
        _ = _stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
        IdempotencyChecker checker = CreateChecker();

        // Act
        _ = await checker.CheckAsync("my-causation-id");

        // Assert
        _ = await _stateManager.Received(1).TryGetStateAsync<IdempotencyRecord>(
            "idempotency:my-causation-id",
            Arg.Any<CancellationToken>());
    }
}
