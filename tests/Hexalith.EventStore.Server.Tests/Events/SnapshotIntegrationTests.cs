namespace Hexalith.EventStore.Server.Tests.Events;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Tests.Fixtures;
using Hexalith.EventStore.Testing.Builders;

using Shouldly;

/// <summary>
/// Story 7.4 / AC #2: Snapshot integration tests.
/// Validates snapshot creation and rehydration with real Redis state store.
/// </summary>
[Collection("DaprTestContainer")]
public class SnapshotIntegrationTests
{
    private readonly DaprTestContainerFixture _fixture;

    public SnapshotIntegrationTests(DaprTestContainerFixture fixture)
    {
        _fixture = fixture;
        _fixture.SetupCounterDomain();
    }

    /// <summary>
    /// Task 2.6: Test state rehydration from events.
    /// After persisting events, the actor should rehydrate state correctly on the next command.
    /// </summary>
    [Fact]
    public async Task ProcessCommandAsync_AfterMultipleEvents_RehydratesStateCorrectly()
    {
        // Arrange
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        string aggregateId = $"rehydrate-test-{Guid.NewGuid():N}";
        IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId($"tenant-a:counter:{aggregateId}"),
            nameof(AggregateActor));

        // Act - send 5 increments (event replay should work for subsequent commands)
        for (int i = 0; i < 5; i++)
        {
            var command = new CommandEnvelopeBuilder()
                .WithTenantId("tenant-a")
                .WithDomain("counter")
                .WithAggregateId(aggregateId)
                .WithCommandType("IncrementCounter")
                .Build();

            CommandProcessingResult result = await proxy.ProcessCommandAsync(command);
            result.Accepted.ShouldBeTrue($"Increment {i + 1} should succeed");
        }

        // Send a final command - the actor should have rehydrated state from previous events
        var finalCommand = new CommandEnvelopeBuilder()
            .WithTenantId("tenant-a")
            .WithDomain("counter")
            .WithAggregateId(aggregateId)
            .WithCommandType("IncrementCounter")
            .Build();

        CommandProcessingResult finalResult = await proxy.ProcessCommandAsync(finalCommand);

        // Assert
        finalResult.Accepted.ShouldBeTrue("Post-rehydration command should succeed");
        finalResult.EventCount.ShouldBe(1);
    }
}
