using System.Globalization;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.Sharding.TestHost;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Proves a proxied ordinary one-key binding never stages the companion witness.</summary>
public sealed class OneKeyAttemptJournalTests
{
    /// <summary>The actual actor keeps its existing one-key SaveStateAsync under the new bound profile.</summary>
    [Fact]
    public async Task ProxyOneKeyBindingRetainsOrdinaryActorSaveAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "sharding-one-key-journal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var binding = new RunBinding("one-key", Canonical(now), Canonical(now.AddMinutes(1)), "", "", "", new(), new(), new(),
                new() { ["provider-commit-proxy"] = "bound", ["provider-one-key"] = "ordinary", ["authorization"] = ShardingFixture.AuthorizationFor(providerCommitProxy: true, providerOneKey: true), ["attempt-journal-directory"] = directory });
            binding = JsonSerializer.Deserialize<RunBinding>(JsonSerializer.Serialize(binding))!;
            binding.Configuration["authorization"].ShouldContain("spec-4-6-one-key-provider-boundaries.md");
            var journal = new AttemptJournal(binding);
            journal.CommitWitnessEnabled.ShouldBeFalse();
            using var fence = new RunFence(binding);
            var manager = new InMemoryStateManager();
            var actor = new ShardActor(ActorHost.CreateForTest<ShardActor>(new ActorTestOptions
            { ActorId = new ActorId(ShardIdentity.Encode("tenant", "domain")) }), fence, journal);
            ActorStateManagerTestHelper.SetStateManager(actor, manager);
            (await actor.ApplyAsync(new("Admit", "tenant", "domain", long.MaxValue, OperationId: "admit"))).Code.ShouldBe("saved");
            (await actor.ApplyAsync(new("Reserve", "tenant", "domain", long.MaxValue, 3, OperationId: "reserve"))).State
                .ShouldBe(new ShardState("tenant", "domain", long.MaxValue, 3));
            manager.CommittedState.Keys.ShouldBe(new[] { ShardActor.StateKey });
            File.ReadAllLines(Directory.GetFiles(directory).Single()).Length.ShouldBe(8);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>The one-key authority names its owning phase independently from the helper's expected full text.</summary>
    /// <param name="proxy">Whether the profile uses the provider proxy.</param>
    /// <param name="oneKey">Whether the profile selects ordinary one-key saves.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AuthorizationNamesTheOwningOneKeyPhase(bool proxy, bool oneKey)
    {
        string authorization = ShardingFixture.AuthorizationFor(proxy, oneKey);
        authorization.Contains("spec-4-6-one-key-provider-boundaries.md", StringComparison.Ordinal).ShouldBe(oneKey);
    }

    private static string Canonical(DateTimeOffset value) => value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
