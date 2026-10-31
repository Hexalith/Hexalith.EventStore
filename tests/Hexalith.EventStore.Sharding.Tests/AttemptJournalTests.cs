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
            }
            else
            {
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
}
