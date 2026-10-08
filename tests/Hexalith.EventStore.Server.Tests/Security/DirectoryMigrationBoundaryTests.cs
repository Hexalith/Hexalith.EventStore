using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Testing.Fakes;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Actual durable conditional epoch/cohort/outcome protocol; independently qualified writer/fence facts are synthetic, no target append/physical proof.</summary>
public sealed class DirectoryMigrationBoundaryTests
{
    /// <summary>Every original finite obligation must drain before successor activation; serialized restart preserves predecessor cohorts/receipts/outcomes.</summary>
    [Fact]
    public async Task CompleteFiniteOriginalCohortPrecedesSuccessorAndSurvivesRestart()
    {
        var f = new DirectoryMigrationBoundaryFixture(); (await f.Actor.InstallAsync(DirectoryMigrationBoundaryFixture.Installation())).State.ShouldBe(DirectoryBoundaryOutcomeState.Installed);
        var repair = DirectoryMigrationBoundaryFixture.Repair(); (await f.Actor.RepairAsync(repair)).State.ShouldBe(DirectoryBoundaryOutcomeState.RepairFenced);
        (await f.Actor.ActivateAsync(DirectoryMigrationBoundaryFixture.Activation(2))).State.ShouldBe(DirectoryBoundaryOutcomeState.Unavailable); f.Persisted.CurrentEpochId.ShouldBe("epoch-1");
        (await f.Actor.RecordDrainAsync(DirectoryMigrationBoundaryFixture.Drain(repair.Cohort[0], 2))).State.ShouldBe(DirectoryBoundaryOutcomeState.DrainRecorded);
        (await f.Actor.RecordDrainAsync(DirectoryMigrationBoundaryFixture.Drain(repair.Cohort[1], 3))).State.ShouldBe(DirectoryBoundaryOutcomeState.DrainRecorded);
        var activated = await f.Actor.ActivateAsync(DirectoryMigrationBoundaryFixture.Activation(4)); activated.State.ShouldBe(DirectoryBoundaryOutcomeState.Activated);
        f.Persisted.CurrentEpochId.ShouldBe("epoch-2"); f.Persisted.ActiveRepairId.ShouldBeNull(); f.Persisted.Repairs.Single().Cohort.ShouldBe(repair.Cohort);
        f.Persisted.Drains.Count.ShouldBe(2); f.Persisted.Installations.Select(i => i.EpochId).ShouldBe(["epoch-1", "epoch-2"]);
        var stored = f.Backend.CommittedState.Single(); var backend = new InMemoryStateManager();
        await backend.SetStateAsync(stored.Key, JsonSerializer.Deserialize<DirectoryEpochLedger>(JsonSerializer.Serialize(stored.Value))!, TestContext.Current.CancellationToken); await backend.SaveStateAsync(TestContext.Current.CancellationToken);
        var restart = DirectoryMigrationBoundaryFixture.Create(backend, f.Authority);
        (await restart.LookupAsync("tenant-a", activated.OperationId, activated.RequestDigest)).ShouldBe(activated);
        (await restart.InstallAsync(DirectoryMigrationBoundaryFixture.Installation())).CommittedRevision.ShouldBe(1);
        (await restart.ReadAsync("tenant-a"))!.Outcomes.Count.ShouldBe(5);
    }
    /// <summary>Unmanifested/changed operations and unknown terminal proofs never drain; no new work gets bridge authority.</summary>
    [Theory]
    [InlineData("unmanifested")][InlineData("changed-source")][InlineData("unknown")]
    public async Task OnlyExactAuthenticatedOriginalResultDrains(string vector)
    {
        var f = new DirectoryMigrationBoundaryFixture(); await f.Actor.InstallAsync(DirectoryMigrationBoundaryFixture.Installation()); await f.Actor.RepairAsync(DirectoryMigrationBoundaryFixture.Repair());
        var item = DirectoryMigrationBoundaryFixture.Item(vector == "unmanifested" ? "not-in-cohort" : "original-1");
        if (vector == "changed-source") { item = item with { SourceConversationId = "changed-conversation" }; }
        if (vector == "unknown") { f.Authority.VerifyDrainAsync(Arg.Any<DirectoryRepairDrainReceipt>(), Arg.Any<CancellationToken>()).Returns(false); }
        (await f.Actor.RecordDrainAsync(DirectoryMigrationBoundaryFixture.Drain(item, 2))).State.ShouldBe(DirectoryBoundaryOutcomeState.Unavailable);
        f.Persisted.Drains.ShouldBeEmpty(); f.Persisted.Revision.ShouldBe(2); f.Persisted.ActiveRepairId.ShouldBe("repair-1");
    }
    /// <summary>Conditional stale is an immutable original outcome, not a later retry that silently succeeds at a new revision.</summary>
    [Fact]
    public async Task StaleConditionalOutcomeCannotBecomeInstalledOnRetry()
    {
        var f = new DirectoryMigrationBoundaryFixture(); await f.Actor.InstallAsync(DirectoryMigrationBoundaryFixture.Installation());
        var stale = DirectoryMigrationBoundaryFixture.Repair(0); var result = await f.Actor.RepairAsync(stale);
        result.State.ShouldBe(DirectoryBoundaryOutcomeState.Stale); result.CommittedRevision.ShouldBe(2); f.Persisted.Repairs.ShouldBeEmpty();
        (await f.Actor.RepairAsync(stale)).ShouldBe(result);
        (await f.Actor.RepairAsync(stale with { ExpectedRevision = 2 })).State.ShouldBe(DirectoryBoundaryOutcomeState.Conflict); f.Persisted.ActiveRepairId.ShouldBeNull();
    }
    /// <summary>Missing caller/installation authority and equal-revision divergent restore release no original metadata.</summary>
    [Fact]
    public async Task MissingPrivateAuthorityAndDivergentRestoreDenyLookup()
    {
        var f = new DirectoryMigrationBoundaryFixture(); (await DirectoryMigrationBoundaryFixture.Create(f.Backend).InstallAsync(DirectoryMigrationBoundaryFixture.Installation())).State.ShouldBe(DirectoryBoundaryOutcomeState.Unavailable);
        f.Backend.CommittedState.ShouldBeEmpty(); var result = await f.Actor.InstallAsync(DirectoryMigrationBoundaryFixture.Installation());
        var stored = f.Backend.CommittedState.Single(); var changed = f.Persisted with { Installations = [DirectoryMigrationBoundaryFixture.Installation() with { WriterEnforcementReceipt = "divergent-receipt" }] };
        await f.Backend.SetStateAsync(stored.Key, changed, TestContext.Current.CancellationToken); await f.Backend.SaveStateAsync(TestContext.Current.CancellationToken);
        (await f.Actor.ReadAsync("tenant-a")).ShouldBeNull(); (await f.Actor.LookupAsync("tenant-a", result.OperationId, result.RequestDigest)).ShouldBeNull();
        f.Persisted.Revision.ShouldBe(1);
    }
    /// <summary>Precommit failure closes availability; committed lost acknowledgement recovers the exact durable original without a repeated install.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task SaveFailureDoesNotCertifyStagedCache(bool committed)
    {
        var f = new DirectoryMigrationBoundaryFixture(); var manager = Substitute.For<IActorStateManager>();
        manager.ClearCacheAsync(Arg.Any<CancellationToken>()).Returns(call => f.Backend.ClearCacheAsync(call.Arg<CancellationToken>()));
        manager.TryGetStateAsync<DirectoryEpochLedger>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => f.Backend.TryGetStateAsync<DirectoryEpochLedger>(call.Arg<string>(), call.Arg<CancellationToken>()));
        manager.SetStateAsync(Arg.Any<string>(), Arg.Any<DirectoryEpochLedger>(), Arg.Any<CancellationToken>()).Returns(call => f.Backend.SetStateAsync(call.Arg<string>(), call.Arg<DirectoryEpochLedger>(), call.Arg<CancellationToken>()));
        manager.SaveStateAsync(Arg.Any<CancellationToken>()).Returns(async call => { if (committed) { await f.Backend.SaveStateAsync(call.Arg<CancellationToken>()); } throw new HttpRequestException("Controlled boundary save failure."); });
        await Should.ThrowAsync<HttpRequestException>(() => DirectoryMigrationBoundaryFixture.Create(manager, f.Authority).InstallAsync(DirectoryMigrationBoundaryFixture.Installation()));
        (await f.Actor.InstallAsync(DirectoryMigrationBoundaryFixture.Installation())).State.ShouldBe(committed ? DirectoryBoundaryOutcomeState.Installed : DirectoryBoundaryOutcomeState.Unavailable);
        f.Anchor.ShouldBe(1); if (committed) { f.Persisted.Installations.Count.ShouldBe(1); } else { f.Backend.CommittedState.ShouldBeEmpty(); }
    }
    /// <summary>Full original outcome bound refuses new mutation before anchor/save but retains authenticated terminal lookup.</summary>
    [Fact]
    public async Task CapacityRefusalPreservesOriginalImmutableLookup()
    {
        var f = new DirectoryMigrationBoundaryFixture(); var original = await f.Actor.InstallAsync(DirectoryMigrationBoundaryFixture.Installation());
        var state = f.Persisted with { Revision = 10000, Outcomes = new[] { original }.Concat(Enumerable.Range(2, 9999).Select(n =>
            new DirectoryBoundaryOutcome("stale-" + n, new string('A', 64), DirectoryBoundaryOutcomeState.Stale, n))).ToArray() };
        f.Anchor = 10000; f.AnchorDigest = DirectoryMigrationBoundaryFixture.Digest(state); var key = f.Backend.CommittedState.Single().Key;
        await f.Backend.SetStateAsync(key, state, TestContext.Current.CancellationToken); await f.Backend.SaveStateAsync(TestContext.Current.CancellationToken);
        (await f.Actor.RepairAsync(DirectoryMigrationBoundaryFixture.Repair(10000))).State.ShouldBe(DirectoryBoundaryOutcomeState.Unavailable); f.Anchor.ShouldBe(10000);
        (await f.Actor.LookupAsync("tenant-a", original.OperationId, original.RequestDigest)).ShouldBe(original); f.Persisted.Outcomes.Count.ShouldBe(10000);
    }
}
