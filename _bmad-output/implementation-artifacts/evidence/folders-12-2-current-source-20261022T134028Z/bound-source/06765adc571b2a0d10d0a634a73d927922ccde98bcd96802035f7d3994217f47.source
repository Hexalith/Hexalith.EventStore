using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.Testing.Builders;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Events;

/// <summary>Proves a qualified folded snapshot is durable and reloads through real Dapr actor state.</summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public sealed class FoldedSnapshotReloadLiveSidecarTests(DaprTestContainerFixture fixture) : IDisposable
{
    [Fact]
    public async Task QualifiedSnapshotReloadsAfterSidecarRestartWithOnlyTailEvents()
    {
        const string domain = "foldedcounter";
        const string profileId = "live-counter-v1";
        string aggregateId = $"folded-reload-{Guid.NewGuid():N}";
        var identity = new AggregateIdentity("tenant-a", domain, aggregateId);
        fixture.DomainServiceInvoker.SetupAggregateSnapshotHandler(identity,
            (command, currentState, includeState, requestedProfile) =>
            {
                if (includeState)
                {
                    requestedProfile.ShouldBe(profileId);
                }

                var state = currentState as DomainServiceCurrentState;
                int nextCount = checked((int)(state?.CurrentSequence ?? 0) + 1);
                DomainResult result = DomainResult.Success([new CounterIncremented()]);
                if (includeState)
                {
                    result = result with
                    {
                        PostCommandStateJson = JsonSerializer.Serialize(new
                        {
                            count = nextCount,
                            isTerminated = false,
                        }),
                    };
                }

                return Task.FromResult(result);
            });

        IAggregateActor proxy = CreateProxy(identity);
        for (int sequence = 1; sequence <= 12; sequence++)
        {
            CommandProcessingResult result = await proxy.ProcessCommandAsync(CreateCommand(aggregateId));
            result.Accepted.ShouldBeTrue($"Increment {sequence} must commit");
        }

        string durableJson = await fixture.GetAggregateActorStateJsonAsync(identity.SnapshotKey);
        SnapshotRecord snapshot = JsonSerializer.Deserialize<SnapshotRecord>(durableJson, JsonSerializerOptions.Web)
            .ShouldNotBeNull();
        snapshot.SnapshotEnvelopeVersion.ShouldBe(1);
        snapshot.SequenceNumber.ShouldBe(11);
        JsonElement stateJson = JsonSerializer.SerializeToElement(snapshot.State);
        stateJson.GetProperty("count").GetInt32().ShouldBe(11);

        await fixture.RestartHostAndSidecarAsync();
        proxy = CreateProxy(identity);
        CommandProcessingResult afterRestart = await proxy.ProcessCommandAsync(CreateCommand(aggregateId));
        afterRestart.Accepted.ShouldBeTrue();
        afterRestart.EventCount.ShouldBe(1);

        (CommandEnvelope _, object? lastState) = fixture.DomainServiceInvoker.InvocationsWithState
            .Where(entry => entry.Command.AggregateId == aggregateId)
            .Last();
        DomainServiceCurrentState reloaded = lastState.ShouldBeOfType<DomainServiceCurrentState>();
        reloaded.LastSnapshotSequence.ShouldBe(11);
        reloaded.CurrentSequence.ShouldBe(12);
        reloaded.Events.Count.ShouldBe(1);
        reloaded.Events[0].Metadata.SequenceNumber.ShouldBe(12);
        JsonElement folded = reloaded.SnapshotState.ShouldBeOfType<JsonElement>();
        folded.GetProperty("count").GetInt32().ShouldBe(11);
        (await fixture.GetAggregateActorStateJsonAsync(identity.SnapshotKey)).ShouldBe(durableJson);
    }

    public void Dispose() => fixture.DomainServiceInvoker.ClearAll();

    private IAggregateActor CreateProxy(AggregateIdentity identity)
        => new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = fixture.DaprHttpEndpoint })
            .CreateActorProxy<IAggregateActor>(new ActorId(identity.ActorId), fixture.AggregateActorTypeName);

    private static CommandEnvelope CreateCommand(string aggregateId)
        => new CommandEnvelopeBuilder()
            .WithTenantId("tenant-a")
            .WithDomain("foldedcounter")
            .WithAggregateId(aggregateId)
            .WithCommandType("IncrementCounter")
            .Build();
}
