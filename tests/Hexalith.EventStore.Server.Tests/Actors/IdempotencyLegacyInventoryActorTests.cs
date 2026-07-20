using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class IdempotencyLegacyInventoryActorTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InspectAsync_RequiredInventoryWithoutEntry_FailsClosedAsUninventoried()
    {
        (IdempotencyLegacyInventoryActor actor, _) = CreateActor(requireInventory: true);

        IdempotencyLegacyInventoryInspection result = await actor.InspectAsync([Alias()]);

        result.Decision.ShouldBe(IdempotencyLegacyInventoryDecision.Uninventoried);
        result.Entry.ShouldBeNull();
    }

    [Fact]
    public async Task MigrationPhases_PreserveSourceEvidenceAndExactLogicalResult()
    {
        (IdempotencyLegacyInventoryActor actor, IActorStateManager stateManager) = CreateActor(requireInventory: true);
        IdempotencyLegacyInventoryEntry entry = Entry();

        await actor.InventoryAsync(entry);
        IdempotencyLegacyInventoryInspection inventoried = await actor.InspectAsync([Alias()]);
        IdempotencyLegacyInventoryEntry prepared = await actor.AdvanceAsync(
            "v1",
            "key-digest",
            IdempotencyLegacyMigrationPhase.Inventoried,
            "tenant-a:v1:key-digest");
        IdempotencyLegacyInventoryEntry migrated = await actor.AdvanceAsync(
            "v1",
            "key-digest",
            IdempotencyLegacyMigrationPhase.TargetPrepared,
            "tenant-a:v1:key-digest");
        IdempotencyLegacyInventoryInspection final = await actor.InspectAsync([Alias()]);

        inventoried.Decision.ShouldBe(IdempotencyLegacyInventoryDecision.Migrate);
        prepared.Phase.ShouldBe(IdempotencyLegacyMigrationPhase.TargetPrepared);
        migrated.Phase.ShouldBe(IdempotencyLegacyMigrationPhase.Migrated);
        migrated.SourceAggregateActorId.ShouldBe(entry.SourceAggregateActorId);
        migrated.SourceEvidenceDigest.ShouldBe(entry.SourceEvidenceDigest);
        migrated.ReplayResult.ShouldBe(entry.ReplayResult);
        final.Decision.ShouldBe(IdempotencyLegacyInventoryDecision.Migrated);
        await stateManager.Received(3).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    private static (IdempotencyLegacyInventoryActor Actor, IActorStateManager StateManager) CreateActor(
        bool requireInventory)
    {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        var entries = new Dictionary<string, IdempotencyLegacyInventoryEntry>(StringComparer.Ordinal);
        _ = stateManager.TryGetStateAsync<IdempotencyLegacyInventoryEntry>(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => entries.TryGetValue(
                callInfo.ArgAt<string>(0),
                out IdempotencyLegacyInventoryEntry? entry)
                    ? new ConditionalValue<IdempotencyLegacyInventoryEntry>(true, entry)
                    : new ConditionalValue<IdempotencyLegacyInventoryEntry>(false, default!));
        _ = stateManager.SetStateAsync(
                Arg.Any<string>(),
                Arg.Do<IdempotencyLegacyInventoryEntry>(entry =>
                    entries[$"legacy:{entry.DigestKeyVersion}:{entry.KeyDigest}"] = entry),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        ActorHost host = ActorHost.CreateForTest<IdempotencyLegacyInventoryActor>(
            new ActorTestOptions { ActorId = new ActorId("tenant-a") });
        var actor = new IdempotencyLegacyInventoryActor(
            host,
            Options.Create(new IdempotencyAdmissionOptions
            {
                RequireLegacyInventory = requireInventory,
            }));
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);
        return (actor, stateManager);
    }

    private static IdempotencyAdmissionDirectoryAlias Alias()
        => new("v1", "tenant-a:v1:key-digest", "key-digest");

    private static IdempotencyLegacyInventoryEntry Entry()
        => new(
            IdempotencyLegacyInventoryEntry.CurrentSchemaVersion,
            "tenant-a",
            "tenant-a:folders:legacy-folder",
            "source-evidence-digest",
            LegacySchemaVersion: 1,
            "v1",
            "key-digest",
            "verification-tag",
            "intent-digest",
            IdempotencyReplayRetentionTier.Mutation,
            _now.AddDays(-1),
            _now,
            _now.AddDays(1),
            new CommandProcessingResult(
                true,
                CorrelationId: "trace-original",
                EventCount: 1,
                ResultPayload: "{\"same\":true}"),
            "01J00000000000000000000000",
            "trace-original",
            IdempotencyLegacyMigrationPhase.Inventoried);
}
