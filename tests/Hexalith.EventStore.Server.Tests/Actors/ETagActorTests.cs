
using System.Reflection;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>
/// Tests for <see cref="ETagActor"/> that verify actor constants, cold start behavior,
/// and the migration detection/format logic.
/// Note: Tests for RegenerateAsync and OnActivateAsync migration require DAPR state
/// provider infrastructure and are deferred to Tier 2 integration tests.
/// </summary>
public class ETagActorTests {
    private static (ETagActor Actor, IActorStateManager StateManager) CreateActorWithMockState(
        string projectionType = "counter",
        string tenantId = "tenant1") {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        var host = ActorHost.CreateForTest<ETagActor>(
            new ActorTestOptions { ActorId = new ActorId($"{projectionType}:{tenantId}") });
        var actor = new ETagActor(host, NullLogger<ETagActor>.Instance);

        // Dapr runtime sets StateManager internally; tests inject a substitute.
        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        return (actor, stateManager);
    }

    private static async Task InvokeOnActivateAsync(ETagActor actor) {
        MethodInfo method = typeof(ETagActor).GetMethod("OnActivateAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await ((Task)method.Invoke(actor, null)!).ConfigureAwait(false);
    }

    private static bool IsProjectionTypeETag(string value, string expectedProjectionType) {
        return SelfRoutingETag.TryDecode(value, out string? projectionType, out _)
            && string.Equals(projectionType, expectedProjectionType, StringComparison.Ordinal);
    }

    // ===== GetCurrentETagAsync tests =====

    [Fact]
    public async Task GetCurrentETagAsync_BeforeActivation_ReturnsNull() {
        // Arrange — actor just created, no activation or state loaded
        (ETagActor actor, _) = CreateActorWithMockState();

        // Act
        string? result = await actor.GetCurrentETagAsync();

        // Assert — cold start, no ETag cached
        result.ShouldBeNull();
    }

    // ===== ETagActorTypeName constant test =====

    [Fact]
    public void ETagActorTypeName_IsCorrectValue() {
        // Assert — must match the type name used in DaprETagService actor proxy creation
        ETagActor.ETagActorTypeName.ShouldBe("ETagActor");
    }

    // ===== Old-format detection logic tests =====
    // These test the migration detection logic (no-dot = old format) that ETagActor.OnActivateAsync uses.

    [Fact]
    public async Task RegenerateAsync_PersistsAndCachesNewSelfRoutingETag() {
        // Arrange
        (ETagActor actor, IActorStateManager stateManager) = CreateActorWithMockState("counter", "tenant1");

        // Act
        string newETag = await actor.RegenerateAsync();
        string? cached = await actor.GetCurrentETagAsync();

        // Assert — persist before cache and value is decodable self-routing format
        await stateManager.Received(1).SetStateAsync("etag", newETag, Arg.Any<CancellationToken>());
        await stateManager.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());
        cached.ShouldBe(newETag);
        SelfRoutingETag.TryDecode(newETag, out string? projectionType, out _).ShouldBeTrue();
        projectionType.ShouldBe("counter");
    }

    [Fact]
    public async Task OnActivateAsync_WithSelfRoutingETag_LoadsStateWithoutMigration() {
        // Arrange
        (ETagActor actor, IActorStateManager stateManager) = CreateActorWithMockState("counter", "tenant1");
        string current = SelfRoutingETag.GenerateNew("counter");
        _ = stateManager.TryGetStateAsync<string>("etag", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<string>(true, current));

        // Act
        await InvokeOnActivateAsync(actor);

        // Assert
        (await actor.GetCurrentETagAsync()).ShouldBe(current);
        await stateManager.DidNotReceive().SetStateAsync("etag", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActivateAsync_WithOldFormatETag_MigratesAndPersistsSelfRoutingFormat() {
        // Arrange
        (ETagActor actor, IActorStateManager stateManager) = CreateActorWithMockState("counter", "tenant1");
        // Old-format ETags are base64url GUIDs without a dot separator
        string oldFormatETag = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        _ = stateManager.TryGetStateAsync<string>("etag", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<string>(true, oldFormatETag));

        // Act
        await InvokeOnActivateAsync(actor);

        // Assert — migrated value is persisted and cached as self-routing format
        await stateManager.Received(1).SetStateAsync(
            "etag",
            Arg.Is<string>(value => IsProjectionTypeETag(value, "counter")),
            Arg.Any<CancellationToken>());
        await stateManager.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());
        string? cached = await actor.GetCurrentETagAsync();
        cached.ShouldNotBeNull();
        cached!.Contains('.').ShouldBeTrue();
    }

    [Fact]
    public async Task OnActivateAsync_MigrationFailure_FallsBackToColdStart() {
        // Arrange
        (ETagActor actor, IActorStateManager stateManager) = CreateActorWithMockState("counter", "tenant1");
        string oldFormatETag = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        _ = stateManager.TryGetStateAsync<string>("etag", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<string>(true, oldFormatETag));
        _ = stateManager.SetStateAsync("etag", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("write failed"));

        // Act
        await InvokeOnActivateAsync(actor);

        // Assert — actor remains in cold-start state when migration fails
        (await actor.GetCurrentETagAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task OnActivateAsync_StateReadFailure_FallsBackToColdStart() {
        // Arrange
        (ETagActor actor, IActorStateManager stateManager) = CreateActorWithMockState("counter", "tenant1");
        _ = stateManager.TryGetStateAsync<string>("etag", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("read failed"));

        // Act
        await InvokeOnActivateAsync(actor);

        // Assert
        (await actor.GetCurrentETagAsync()).ShouldBeNull();
    }

    [Fact]
    public void OldFormatDetection_GuidOnlyNoDot_IsOldFormat() {
        // Old-format ETags are base64url GUIDs without a dot separator
        string oldFormatETag = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        // Old format: no dots → OnActivateAsync triggers migration
        oldFormatETag.Contains('.').ShouldBeFalse();

        // TryDecode correctly rejects old format
        SelfRoutingETag.TryDecode(oldFormatETag, out _, out _).ShouldBeFalse();
    }

    [Fact]
    public void OldFormatDetection_NewSelfRoutingFormat_HasDot() {
        // New self-routing format always contains a dot → OnActivateAsync loads as-is
        string newFormatETag = SelfRoutingETag.GenerateNew("counter");

        newFormatETag.Contains('.').ShouldBeTrue();
        SelfRoutingETag.TryDecode(newFormatETag, out string? projectionType, out _).ShouldBeTrue();
        projectionType.ShouldBe("counter");
    }

    [Fact]
    public void MigrationPath_GenerateNew_ProducesValidSelfRoutingETag() {
        // Migration calls SelfRoutingETag.GenerateNew(projectionType) — verify output is valid
        string projectionType = "order-list";
        string migrated = SelfRoutingETag.GenerateNew(projectionType);

        SelfRoutingETag.TryDecode(migrated, out string? decoded, out string? guidPart).ShouldBeTrue();
        decoded.ShouldBe(projectionType);
        guidPart!.Length.ShouldBe(22); // base64url GUID = 16 bytes → 22 chars
    }
}
