using System.Globalization;
using System.Reflection;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.Sharding.TestHost;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Proves observation identities do not hide duplicate actor executions.</summary>
public sealed class AttemptJournalTests
{
    /// <summary>Journal I/O failures retain the actual save stage, original errors and complete durable outcome.</summary>
    /// <param name="stage">The journal append to fail with an actual filesystem collision.</param>
    /// <param name="saveFails">Whether the provider also reports a failed save.</param>
    /// <param name="committed">Whether this fake durably commits the attempted reservation.</param>
    [Theory]
    [InlineData("actor-entry", false, false)]
    [InlineData("save-start", false, false)]
    [InlineData("save-complete", false, true)]
    [InlineData("save-failed", true, false)]
    [InlineData("save-failed", true, true)]
    [InlineData("actor-exit", false, true)]
    [InlineData("actor-exit", true, false)]
    [InlineData("actor-exit", true, true)]
    public async Task JournalFailureCannotMisclassifyPersistenceOrMaskOriginalSaveAsync(string stage, bool saveFails, bool committed)
    {
        string directory = Path.Combine(Path.GetTempPath(), "sharding-attempt-io-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string journalPath = Path.Combine(directory, "host-" + Environment.ProcessId + ".jsonl");
        string retained = Path.Combine(directory, "retained.jsonl");
        bool injected = false;
        try
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var binding = new RunBinding("attempt-io", Canonical(now), Canonical(now.AddMinutes(1)), "", "", "",
                new(), new(), new(), new()
                {
                    ["attempt-journal-directory"] = directory,
                    ["provider-commit-proxy"] = "two-key experiment",
                });
            using var fence = new RunFence(binding);
            var journal = new AttemptJournal(binding, reached =>
            {
                if (injected || reached != stage) return;
                injected = true;
                if (File.Exists(journalPath)) File.Move(journalPath, retained);
                Directory.CreateDirectory(journalPath); // Actual append now fails, independent of filesystem permissions/root.
            });
            IActorStateManager manager = DispatchProxy.Create<IActorStateManager, SaveFailureStateManager>();
            var fake = (SaveFailureStateManager)manager;
            fake.FailNextSave = saveFails;
            fake.CommitBeforeFailure = committed;
            var previous = new ShardState("tenant", "domain", 7, 9);
            var previousWitness = new ShardCommitWitness("previous", previous);
            await fake.Inner.SetStateAsync(ShardActor.StateKey, previous);
            await fake.Inner.SetStateAsync(ShardActor.CommitWitnessStateKey, previousWitness);
            await fake.Inner.SetStateAsync("sentinel", "unchanged");
            await fake.Inner.SaveStateAsync();
            string emptyPending = PendingSnapshot(fake.Inner);
            var actor = new ShardActor(ActorHost.CreateForTest<ShardActor>(new ActorTestOptions
            {
                ActorId = new ActorId(ShardIdentity.Encode("tenant", "domain")),
            }), fence, journal);
            ActorStateManagerTestHelper.SetStateManager(actor, manager);
            var request = new ShardRequest("Reserve", "tenant", "domain", 7, 3, OperationId: "journal-failure");
            Exception failure = (await Record.ExceptionAsync(() => actor.ApplyAsync(request)))!;
            failure.ShouldNotBeNull(); // Incomplete observation must never turn into experiment success.
            injected.ShouldBeTrue();
            if (saveFails)
            {
                failure.ShouldBeSameAs(fake.SaveFailure);
                if (stage == "save-failed")
                    ((Exception)failure.Data["attempt-journal:save-failed"]!).Data["attempt-journal-stage"].ShouldBe("save-failed");
                ((Exception)failure.Data["attempt-journal:actor-exit"]!).Data["attempt-journal-stage"].ShouldBe("actor-exit");
            }
            else
            {
                failure.Data["attempt-journal-stage"].ShouldBe(stage);
                if (stage != "actor-exit")
                    ((Exception)failure.Data["attempt-journal:actor-exit"]!).Data["attempt-journal-stage"].ShouldBe("actor-exit");
            }

            fake.SaveAttempts.ShouldBe(stage is "actor-entry" or "save-start" ? 0 : 1);
            string[] expectedStages = stage switch
            {
                "actor-entry" => [],
                "save-start" => ["actor-entry"],
                "save-complete" or "save-failed" => ["actor-entry", "save-start"],
                _ => ["actor-entry", "save-start", saveFails ? "save-failed" : "save-complete"],
            };
            AttemptObservation[] entries = File.Exists(retained) ? File.ReadAllLines(retained)
                .Select(line => JsonSerializer.Deserialize<AttemptObservation>(line)!).ToArray() : [];
            entries.Select(entry => entry.Stage).ShouldBe(expectedStages);
            string actorId = ShardIdentity.Encode("tenant", "domain");
            var registered = saveFails
                ? new RegisteredShardOperation(request, actorId, "uncertain", Sends: 1, FaultMarker: "journal-fault",
                    FaultBoundary: committed ? PostgresCommitBoundary.CommitAcknowledgement : PostgresCommitBoundary.BeforeCommit,
                    FaultVerified: true)
                : new RegisteredShardOperation(request, actorId, "acknowledged-success", "saved", 1);
            AttemptObservation Observation(string observedStage, string? detail = null)
                => new(request.OperationId!, observedStage, Environment.ProcessId, now, actorId, request, detail);
            AttemptObservation[] complete =
            [
                Observation("actor-entry"), Observation("save-start"),
                Observation(saveFails ? "save-failed" : "save-complete", saveFails ? nameof(IOException) : null),
                Observation("actor-exit", saveFails ? "failed" : "saved"),
            ];
            PostgresCommitObservation[] ledger = [new(1, 1, saveFails ? "journal-fault" : null,
                Forwarded: !saveFails || committed, Completed: !saveFails || committed,
                AcknowledgementWithheld: saveFails && committed)];
            ShardAttemptValidator.Validate([registered], complete, [Environment.ProcessId], ledger);
            InvalidDataException stageFailure = Should.Throw<InvalidDataException>(
                () => ShardAttemptValidator.Validate([registered], entries, [Environment.ProcessId], ledger));
            stageFailure.Message.ShouldBe("Illegal, missing or duplicate execution stages: " + request.OperationId);

            ShardState durable = committed ? previous with { Ceiling = 12 } : previous;
            ShardCommitWitness witness = committed ? new(request.OperationId!, durable) : previousWitness;
            string expectedState = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                [ShardActor.StateKey] = durable,
                [ShardActor.CommitWitnessStateKey] = witness,
                ["sentinel"] = "unchanged",
            });
            JsonSerializer.Serialize(fake.Inner.CommittedState).ShouldBe(expectedState);
            PendingSnapshot(fake.Inner).ShouldBe(emptyPending);
            await manager.SaveStateAsync(); // Runtime completion cannot publish a pre-save failed observation's pending keys.
            JsonSerializer.Serialize(fake.Inner.CommittedState).ShouldBe(expectedState);
            Directory.Delete(journalPath);
            if (File.Exists(retained)) File.Move(retained, journalPath);
            ShardReply next = await actor.ApplyAsync(request with { Count = 2, OperationId = "distinct-next" });
            next.First.ShouldBe(durable.Ceiling + 1);
            next.State.ShouldBe(durable with { Ceiling = durable.Ceiling + 2 });
            fake.Inner.CommittedState[ShardActor.StateKey].ShouldBe(next.State);
            fake.Inner.CommittedState[ShardActor.CommitWitnessStateKey].ShouldBe(new ShardCommitWitness("distinct-next", next.State!));
            fake.Inner.CommittedState["sentinel"].ShouldBe("unchanged");
            fake.Inner.CommittedState.Count.ShouldBe(3);
            PendingSnapshot(fake.Inner).ShouldBe(emptyPending);
        }
        finally { Directory.Delete(directory, recursive: true); }
        Directory.Exists(directory).ShouldBeFalse();
    }

    /// <summary>Actual repeated executions append full entry/save observations and remain visible.</summary>
    /// <param name="twoKeys">Whether the bound provider experiment requires its companion state key.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JournalsRetainEveryActorExecutionWithoutDeduplicationAsync(bool twoKeys)
    {
        string directory = Path.Combine(Path.GetTempPath(), "sharding-attempt-unit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var binding = new RunBinding("attempt-unit", Canonical(now), Canonical(now.AddMinutes(1)), "", "", "",
                new(), new(), new(), new() { ["attempt-journal-directory"] = directory });
            if (twoKeys) binding.Configuration["provider-commit-proxy"] = "two-key experiment";
            using var fence = new RunFence(binding);
            var journal = new AttemptJournal(binding);
            var manager = new InMemoryStateManager();
            var actor = new ShardActor(ActorHost.CreateForTest<ShardActor>(new ActorTestOptions
            {
                ActorId = new ActorId(ShardIdentity.Encode("tenant", "domain")),
            }), fence, journal);
            ActorStateManagerTestHelper.SetStateManager(actor, manager);
            (await actor.ApplyAsync(new("Admit", "tenant", "domain", 7, OperationId: "admit"))).Accepted.ShouldBeTrue();
            var repeated = new ShardRequest("Reserve", "tenant", "domain", 7, 2, OperationId: "repeated");
            (await actor.ApplyAsync(repeated)).First.ShouldBe(1);
            (await actor.ApplyAsync(repeated)).First.ShouldBe(3);
            manager.CommittedState[ShardActor.StateKey].ShouldBe(new ShardState("tenant", "domain", 7, 4));
            if (twoKeys)
                manager.CommittedState[ShardActor.CommitWitnessStateKey].ShouldBe(new ShardCommitWitness("repeated", new("tenant", "domain", 7, 4)));
            else manager.CommittedState.ContainsKey(ShardActor.CommitWitnessStateKey).ShouldBeFalse();
            AttemptObservation[] records = File.ReadAllLines(Directory.GetFiles(directory).Single())
                .Select(line => JsonSerializer.Deserialize<AttemptObservation>(line)!).ToArray();
            records.Length.ShouldBe(12);
            foreach (string stage in new[] { "actor-entry", "save-start", "save-complete", "actor-exit" })
                records.Count(entry => entry.OperationId == "repeated" && entry.Stage == stage).ShouldBe(2);
            records.All(entry => entry.ProcessId == Environment.ProcessId).ShouldBeTrue();
            // A replacement journal appends to retained evidence instead of truncating it.
            new AttemptJournal(binding).Record(repeated, "exact-actor", "actor-entry");
            File.ReadAllLines(Directory.GetFiles(directory).Single()).Length.ShouldBe(13);
        }
        finally { Directory.Delete(directory, recursive: true); }
        Directory.Exists(directory).ShouldBeFalse();
    }

    /// <summary>Failed saves exit after cache eviction, and duplicate no-save rejections fail the actual validator.</summary>
    /// <param name="failedSave">Whether to fail a save rather than reject missing authority.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedAndRejectedExecutionsHaveObservableExitAndCannotHideDuplicatesAsync(bool failedSave)
    {
        string directory = Path.Combine(Path.GetTempPath(), "sharding-attempt-negative-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var binding = new RunBinding("attempt-negative", Canonical(now), Canonical(now.AddMinutes(1)), "", "", "",
                new(), new(), new(), new() { ["attempt-journal-directory"] = directory });
            using var fence = new RunFence(binding);
            var actor = new ShardActor(ActorHost.CreateForTest<ShardActor>(new ActorTestOptions
            {
                ActorId = new ActorId(ShardIdentity.Encode("tenant", "domain")),
            }), fence, new AttemptJournal(binding));
            IActorStateManager manager = DispatchProxy.Create<IActorStateManager, SaveFailureStateManager>();
            var fake = (SaveFailureStateManager)manager;
            fake.FailNextSave = failedSave;
            if (failedSave)
            {
                await fake.Inner.SetStateAsync(ShardActor.StateKey, new ShardState("tenant", "domain", 7, 0));
                await fake.Inner.SaveStateAsync();
            }
            ActorStateManagerTestHelper.SetStateManager(actor, manager);
            var request = new ShardRequest("Reserve", "tenant", "domain", 7, 2, OperationId: "negative");
            if (failedSave) await Should.ThrowAsync<IOException>(() => actor.ApplyAsync(request));
            else
            {
                (await actor.ApplyAsync(request)).Code.ShouldBe("not-admitted");
                (await actor.ApplyAsync(request)).Code.ShouldBe("not-admitted");
            }
            AttemptObservation[] records = File.ReadAllLines(Directory.GetFiles(directory).Single())
                .Select(line => JsonSerializer.Deserialize<AttemptObservation>(line)!).ToArray();
            if (failedSave)
            {
                records.Select(entry => entry.Stage).ShouldBe(new[] { "actor-entry", "save-start", "save-failed", "actor-exit" });
                records[^1].Detail.ShouldBe("failed");
                records[2].Detail.ShouldBe(nameof(IOException));
                fake.SaveAttempts.ShouldBe(1);
                fake.Inner.CommittedState.Count.ShouldBe(1);
                fake.Inner.CommittedState[ShardActor.StateKey].ShouldBe(new ShardState("tenant", "domain", 7, 0));
                await manager.SaveStateAsync();
                fake.Inner.CommittedState[ShardActor.StateKey].ShouldBe(new ShardState("tenant", "domain", 7, 0));
            }
            else
            {
                fake.Inner.CommittedState.ShouldBeEmpty();
                await manager.SaveStateAsync();
                fake.Inner.CommittedState.ShouldBeEmpty();
                var registered = new RegisteredShardOperation(request, ShardIdentity.Encode("tenant", "domain"), "known-rejection", "not-admitted", 1);
                ShardAttemptValidator.Validate([registered], records[..2], [Environment.ProcessId], []);
                Should.Throw<InvalidDataException>(() => ShardAttemptValidator.Validate([registered], records, [Environment.ProcessId], []));
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>The fixture rejects reused mutating IDs before transport while preserving constructor compatibility.</summary>
    [Fact]
    public async Task FixtureRejectsMutatingOperationIdentityReuseAsync()
    {
        await using var fixture = new ShardingFixture();
        var request = new ShardRequest("Reserve", "tenant", "domain", 7, 2, OperationId: "once");
        fixture.BindOperation(request).ShouldBe(request);
        Should.Throw<InvalidOperationException>(() => fixture.BindOperation(request));
        fixture.BindOperation(request with { Operation = "Inspect" }).ShouldBe(request with { Operation = "Inspect" });
        new ShardRequest("Admit", "tenant", "domain", 7).OperationId.ShouldBeNull();
        fixture.CommandCount.ShouldBe(0);
    }

    private static string Canonical(DateTimeOffset value) => value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string PendingSnapshot(InMemoryStateManager state)
        => JsonSerializer.Serialize(new
        {
            state = typeof(InMemoryStateManager).GetField("_pendingState", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(state),
            removals = typeof(InMemoryStateManager).GetField("_pendingRemovals", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(state),
        });
}
