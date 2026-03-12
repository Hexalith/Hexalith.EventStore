
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;

public class SnapshotManagerTests {
    private static readonly AggregateIdentity TestIdentity = new("test-tenant", "test-domain", "agg-001");

    private static (SnapshotManager Manager, IActorStateManager StateManager) CreateManager(
        SnapshotOptions? options = null) {
        options ??= new SnapshotOptions();
        IOptions<SnapshotOptions> optionsWrapper = Options.Create(options);
        ILogger<SnapshotManager> logger = Substitute.For<ILogger<SnapshotManager>>();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        return (new SnapshotManager(optionsWrapper, logger, new NoOpEventPayloadProtectionService()), stateManager);
    }

    // === 8.2: ShouldCreateSnapshot at default interval ===

    [Fact]
    public async Task ShouldCreateSnapshot_AtDefaultInterval_ReturnsTrue() {
        (SnapshotManager manager, _) = CreateManager();

        bool result = await manager.ShouldCreateSnapshotAsync("test-domain", 100, 0);

        result.ShouldBeTrue();
    }

    // === 8.3: ShouldCreateSnapshot below interval ===

    [Fact]
    public async Task ShouldCreateSnapshot_BelowInterval_ReturnsFalse() {
        (SnapshotManager manager, _) = CreateManager();

        bool result = await manager.ShouldCreateSnapshotAsync("test-domain", 50, 0);

        result.ShouldBeFalse();
    }

    // === 8.4: ShouldCreateSnapshot at multiples of interval ===

    [Theory]
    [InlineData(200, 100)]
    [InlineData(300, 200)]
    [InlineData(500, 400)]
    public async Task ShouldCreateSnapshot_AtMultipleOfInterval_ReturnsTrue(long currentSequence, long lastSnapshotSequence) {
        (SnapshotManager manager, _) = CreateManager();

        bool result = await manager.ShouldCreateSnapshotAsync("test-domain", currentSequence, lastSnapshotSequence);

        result.ShouldBeTrue();
    }

    // === 8.5: ShouldCreateSnapshot with custom domain interval ===

    [Fact]
    public async Task ShouldCreateSnapshot_WithCustomDomainInterval_UsesOverride() {
        var options = new SnapshotOptions {
            DefaultInterval = 100,
            DomainIntervals = new Dictionary<string, int> { ["fast-domain"] = 50 }
        };
        (SnapshotManager manager, _) = CreateManager(options);

        // 50 events since last snapshot -- should trigger with domain interval of 50
        bool result = await manager.ShouldCreateSnapshotAsync("fast-domain", 50, 0);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldCreateSnapshot_UnknownDomain_UsesDefaultInterval() {
        var options = new SnapshotOptions {
            DefaultInterval = 100,
            DomainIntervals = new Dictionary<string, int> { ["fast-domain"] = 50 }
        };
        (SnapshotManager manager, _) = CreateManager(options);

        // 50 events -- below default interval of 100
        bool result = await manager.ShouldCreateSnapshotAsync("other-domain", 50, 0);

        result.ShouldBeFalse();
    }

    // === 8.6: CreateSnapshot stores via actor state manager ===

    [Fact]
    public async Task CreateSnapshot_StoresViaActorStateManager() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        var state = new { Name = "test-state" };

        await manager.CreateSnapshotAsync(TestIdentity, 100, state, stateManager);

        await stateManager.Received(1).SetStateAsync(
            TestIdentity.SnapshotKey,
            Arg.Is<SnapshotRecord>(s => s.SequenceNumber == 100),
            Arg.Any<CancellationToken>());
    }

    // === 8.7: CreateSnapshot includes sequence number ===

    [Fact]
    public async Task CreateSnapshot_IncludesSequenceNumber() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        var state = new { Value = 42 };

        await manager.CreateSnapshotAsync(TestIdentity, 250, state, stateManager);

        await stateManager.Received(1).SetStateAsync(
            Arg.Any<string>(),
            Arg.Is<SnapshotRecord>(s =>
                s.SequenceNumber == 250 &&
                s.Domain == "test-domain" &&
                s.AggregateId == "agg-001" &&
                s.TenantId == "test-tenant" &&
                s.CreatedAt > DateTimeOffset.MinValue),
            Arg.Any<CancellationToken>());
    }

    // === 8.8: CreateSnapshot overwrites previous ===

    [Fact]
    public async Task CreateSnapshot_OverwritesPrevious() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        var state1 = new { Version = 1 };
        var state2 = new { Version = 2 };

        await manager.CreateSnapshotAsync(TestIdentity, 100, state1, stateManager);
        await manager.CreateSnapshotAsync(TestIdentity, 200, state2, stateManager);

        // SetStateAsync called twice with the SAME key -- second call overwrites first
        await stateManager.Received(2).SetStateAsync(
            TestIdentity.SnapshotKey,
            Arg.Any<SnapshotRecord>(),
            Arg.Any<CancellationToken>());
    }

    // === 8.9: LoadSnapshot returns null when no snapshot ===

    [Fact]
    public async Task LoadSnapshot_ReturnsNullWhenNoSnapshot() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        _ = stateManager.TryGetStateAsync<SnapshotRecord>(TestIdentity.SnapshotKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<SnapshotRecord>(false, default!));

        SnapshotRecord? result = await manager.LoadSnapshotAsync(TestIdentity, stateManager);

        result.ShouldBeNull();
    }

    // === 8.10: LoadSnapshot returns stored snapshot ===

    [Fact]
    public async Task LoadSnapshot_ReturnsStoredSnapshot() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        var snapshot = new SnapshotRecord(100, new { Value = "stored" }, DateTimeOffset.UtcNow, "test-domain", "agg-001", "test-tenant");
        _ = stateManager.TryGetStateAsync<SnapshotRecord>(TestIdentity.SnapshotKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<SnapshotRecord>(true, snapshot));

        SnapshotRecord? result = await manager.LoadSnapshotAsync(TestIdentity, stateManager);

        _ = result.ShouldNotBeNull();
        result.SequenceNumber.ShouldBe(100);
    }

    // === 8.11: SnapshotOptions default interval is 100 ===

    [Fact]
    public void SnapshotOptions_DefaultIntervalIs100() {
        var options = new SnapshotOptions();

        options.DefaultInterval.ShouldBe(100);
    }

    // === 8.12: SnapshotOptions interval must be at least 10 ===

    [Fact]
    public void SnapshotOptions_IntervalBelowMinimum_ValidationFails() {
        var options = new SnapshotOptions { DefaultInterval = 5 };

        Should.Throw<InvalidOperationException>(options.Validate)
            .Message.ShouldContain("10");
    }

    [Fact]
    public void SnapshotOptions_DomainIntervalBelowMinimum_ValidationFails() {
        var options = new SnapshotOptions {
            DefaultInterval = 100,
            DomainIntervals = new Dictionary<string, int> { ["bad-domain"] = 3 }
        };

        Should.Throw<InvalidOperationException>(options.Validate)
            .Message.ShouldContain("bad-domain");
    }

    [Fact]
    public void SnapshotOptions_ValidIntervals_ValidationPasses() {
        var options = new SnapshotOptions {
            DefaultInterval = 50,
            DomainIntervals = new Dictionary<string, int> { ["fast"] = 10, ["slow"] = 500 }
        };

        Should.NotThrow(options.Validate);
    }

    // === 8.13: CreateSnapshot serialization failure logs warning and skips ===

    [Fact]
    public async Task CreateSnapshot_SerializationFailure_LogsWarningAndSkips() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        var state = new { Value = "test" };

        _ = stateManager.SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<SnapshotRecord>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Serialization failed"));

        // Should NOT throw -- advisory per rule #12
        await Should.NotThrowAsync(() =>
            manager.CreateSnapshotAsync(TestIdentity, 100, state, stateManager));
    }

    // === 8.14: LoadSnapshot deserialization failure returns null and deletes corrupt ===

    [Fact]
    public async Task LoadSnapshot_DeserializationFailure_ReturnsNullAndDeletesCorrupt() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();

        _ = stateManager.TryGetStateAsync<SnapshotRecord>(TestIdentity.SnapshotKey, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Deserialization failed"));

        SnapshotRecord? result = await manager.LoadSnapshotAsync(TestIdentity, stateManager);

        result.ShouldBeNull();
        await stateManager.Received(1).RemoveStateAsync(
            TestIdentity.SnapshotKey,
            Arg.Any<CancellationToken>());
    }

    // === Additional edge cases ===

    [Fact]
    public async Task ShouldCreateSnapshot_ExactlyAtInterval_ReturnsTrue() {
        (SnapshotManager manager, _) = CreateManager();

        // Exactly 100 events since last snapshot at 0
        bool result = await manager.ShouldCreateSnapshotAsync("test-domain", 100, 0);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldCreateSnapshot_OneBeforeInterval_ReturnsFalse() {
        (SnapshotManager manager, _) = CreateManager();

        bool result = await manager.ShouldCreateSnapshotAsync("test-domain", 99, 0);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ShouldCreateSnapshot_OneAfterInterval_ReturnsTrue() {
        (SnapshotManager manager, _) = CreateManager();

        bool result = await manager.ShouldCreateSnapshotAsync("test-domain", 101, 0);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateSnapshot_NullIdentity_ThrowsArgumentNullException() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        _ = await Should.ThrowAsync<ArgumentNullException>(() =>
            manager.CreateSnapshotAsync(null!, 100, new object(), stateManager));
    }

    [Fact]
    public async Task CreateSnapshot_NullState_ThrowsArgumentNullException() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        _ = await Should.ThrowAsync<ArgumentNullException>(() =>
            manager.CreateSnapshotAsync(TestIdentity, 100, null!, stateManager));
    }

    [Fact]
    public async Task CreateSnapshot_NullStateManager_ThrowsArgumentNullException() {
        (SnapshotManager manager, _) = CreateManager();
        _ = await Should.ThrowAsync<ArgumentNullException>(() =>
            manager.CreateSnapshotAsync(TestIdentity, 100, new object(), null!));
    }

    [Fact]
    public async Task LoadSnapshot_NullIdentity_ThrowsArgumentNullException() {
        (SnapshotManager manager, IActorStateManager stateManager) = CreateManager();
        _ = await Should.ThrowAsync<ArgumentNullException>(() =>
            manager.LoadSnapshotAsync(null!, stateManager));
    }

    [Fact]
    public async Task ShouldCreateSnapshot_NullDomain_ThrowsArgumentException() {
        (SnapshotManager manager, _) = CreateManager();
        _ = await Should.ThrowAsync<ArgumentException>(() =>
            manager.ShouldCreateSnapshotAsync(null!, 100, 0));
    }
}
