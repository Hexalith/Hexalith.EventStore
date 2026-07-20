using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class IdempotencyAdmissionDirectoryActorTests
{
    [Fact]
    public async Task ResolveAsync_NewLogicalKey_AtomicallyBindsEveryAliasToActiveWriter()
    {
        (IdempotencyAdmissionDirectoryActor actor, IActorStateManager stateManager) = CreateActor();
        IdempotencyAdmissionDirectoryRequest request = Request();
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionDirectoryEntry>(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionDirectoryEntry>(false, default!));

        IdempotencyAdmissionDirectoryResult result = await actor.ResolveAsync(request);

        result.CanonicalActorId.ShouldBe("tenant-a:v2:active-digest");
        result.PromotionPhase.ShouldBe(IdempotencyAdmissionPromotionPhase.Stable);
        await stateManager.Received(2).SetStateAsync(
            Arg.Any<string>(),
            Arg.Is<IdempotencyAdmissionDirectoryEntry>(entry =>
                entry.CanonicalActorId == "tenant-a:v2:active-digest"
                && entry.Aliases.Count == 2),
            Arg.Any<CancellationToken>());
        await stateManager.Received(1).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_RetainedAuthorityExists_BeginsPersistedPromotionBeforeActiveAuthority()
    {
        (IdempotencyAdmissionDirectoryActor actor, IActorStateManager stateManager) = CreateActor();
        IdempotencyAdmissionDirectoryRequest request = Request(existingActorId: "tenant-a:v1:reader-digest");
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionDirectoryEntry>(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyAdmissionDirectoryEntry>(false, default!));

        IdempotencyAdmissionDirectoryResult result = await actor.ResolveAsync(request);

        result.CanonicalActorId.ShouldBe("tenant-a:v1:reader-digest");
        result.PromotionPhase.ShouldBe(IdempotencyAdmissionPromotionPhase.PrepareTarget);
        result.PromotionSourceActorId.ShouldBe("tenant-a:v1:reader-digest");
        result.PromotionTargetActorId.ShouldBe("tenant-a:v2:active-digest");
        await stateManager.Received(2).SetStateAsync(
            Arg.Any<string>(),
            Arg.Is<IdempotencyAdmissionDirectoryEntry>(entry =>
                entry.CanonicalActorId == "tenant-a:v1:reader-digest"
                && entry.PromotionPhase == IdempotencyAdmissionPromotionPhase.PrepareTarget),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdvanceAsync_PromotionOrder_KeepsSourceCanonicalUntilDirectoryFlip()
    {
        (IdempotencyAdmissionDirectoryActor actor, IActorStateManager stateManager) = CreateActor();
        IdempotencyAdmissionDirectoryRequest request = Request(existingActorId: "tenant-a:v1:reader-digest");
        IdempotencyAdmissionDirectoryEntry? stored = null;
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionDirectoryEntry>(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => stored is null
                ? new ConditionalValue<IdempotencyAdmissionDirectoryEntry>(false, default!)
                : new ConditionalValue<IdempotencyAdmissionDirectoryEntry>(true, stored));
        _ = stateManager.SetStateAsync(
                Arg.Any<string>(),
                Arg.Do<IdempotencyAdmissionDirectoryEntry>(entry => stored = entry),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        IdempotencyAdmissionDirectoryResult prepared = await actor.ResolveAsync(request);

        IdempotencyAdmissionDirectoryResult redirected = await actor.AdvanceAsync(
            new IdempotencyAdmissionDirectoryAdvanceRequest(
                request.Aliases,
                IdempotencyAdmissionPromotionPhase.PrepareTarget));
        IdempotencyAdmissionDirectoryResult readyToFlip = await actor.AdvanceAsync(
            new IdempotencyAdmissionDirectoryAdvanceRequest(
                request.Aliases,
                IdempotencyAdmissionPromotionPhase.RedirectSource));
        IdempotencyAdmissionDirectoryResult flipped = await actor.AdvanceAsync(
            new IdempotencyAdmissionDirectoryAdvanceRequest(
                request.Aliases,
                IdempotencyAdmissionPromotionPhase.FlipDirectory));
        IdempotencyAdmissionDirectoryResult stable = await actor.AdvanceAsync(
            new IdempotencyAdmissionDirectoryAdvanceRequest(
                request.Aliases,
                IdempotencyAdmissionPromotionPhase.ActivateTarget));

        prepared.CanonicalActorId.ShouldBe("tenant-a:v1:reader-digest");
        redirected.CanonicalActorId.ShouldBe("tenant-a:v1:reader-digest");
        readyToFlip.CanonicalActorId.ShouldBe("tenant-a:v1:reader-digest");
        flipped.CanonicalActorId.ShouldBe("tenant-a:v2:active-digest");
        flipped.PromotionPhase.ShouldBe(IdempotencyAdmissionPromotionPhase.ActivateTarget);
        stable.CanonicalActorId.ShouldBe("tenant-a:v2:active-digest");
        stable.PromotionPhase.ShouldBe(IdempotencyAdmissionPromotionPhase.Stable);
    }

    private static (IdempotencyAdmissionDirectoryActor Actor, IActorStateManager StateManager) CreateActor()
    {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ActorHost host = ActorHost.CreateForTest<IdempotencyAdmissionDirectoryActor>(
            new ActorTestOptions { ActorId = new ActorId("tenant-a") });
        var actor = new IdempotencyAdmissionDirectoryActor(
            host,
            NullLogger<IdempotencyAdmissionDirectoryActor>.Instance);
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);
        return (actor, stateManager);
    }

    private static IdempotencyAdmissionDirectoryRequest Request(string? existingActorId = null)
        => new(
            IdempotencyAdmissionDirectoryEntry.CurrentSchemaVersion,
            "tenant-a:v2:active-digest",
            [
                new IdempotencyAdmissionDirectoryAlias("v2", "tenant-a:v2:active-digest", "active-digest"),
                new IdempotencyAdmissionDirectoryAlias("v1", "tenant-a:v1:reader-digest", "reader-digest"),
            ],
            existingActorId);
}
