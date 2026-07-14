using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.Testing.Builders;
using Hexalith.EventStore.Testing.Integration.Benchmarking;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Benchmarking;

/// <summary>
/// Proves the benchmark dataset helper writes state that the production aggregate actor can read and extend.
/// </summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public class BenchmarkDatasetBuilderLiveSidecarTests {
    private static readonly DateTimeOffset _timestamp = new(2026, 7, 14, 10, 0, 0, TimeSpan.Zero);
    private readonly DaprTestContainerFixture _fixture;

    /// <summary>
    /// Initializes a live-sidecar benchmark dataset test.
    /// </summary>
    /// <param name="fixture">The shared Dapr test fixture.</param>
    public BenchmarkDatasetBuilderLiveSidecarTests(DaprTestContainerFixture fixture) {
        _fixture = fixture;
        _fixture.ResetTestState();
        _fixture.SetupCounterDomain();
    }

    /// <summary>
    /// Activates, seeds, and deactivates a snapshot-plus-tail stream, then proves production reactivation and extension.
    /// </summary>
    [Fact]
    public async Task SeedAsync_ProductionActorReadsSnapshotTailAndAppendsNextEvent() {
        _fixture.ThrowIfHostStopped();
        string aggregateId = $"benchmark-reader-{Guid.NewGuid():N}";
        AggregateIdentity identity = new("tenant-a", "counter", aggregateId);
        BenchmarkDatasetDefinition definition = CreateDefinition(identity);
        var options = new BenchmarkDatasetBuilderOptions {
            MaxOperationsPerTransaction = 2,
            MaxTransactionBytes = 16 * 1024,
            MaxConcurrentActors = 1,
        };
        using var actorHostHttpClient = new HttpClient();
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });
        var actorLifecycle = new DaprBenchmarkActorLifecycle(
            actorProxyFactory,
            actorHostHttpClient,
            new Uri(_fixture.AppHttpEndpoint),
            _fixture.AggregateActorTypeName);
        using var builder = new BenchmarkDatasetBuilder(
            new Uri(_fixture.DaprHttpEndpoint),
            _fixture.Services.GetRequiredService<IGlobalPositionAllocator>(),
            _fixture.AggregateActorTypeName,
            actorLifecycle,
            options);
        BenchmarkDatasetReceipt receipt = await builder.SeedAsync(
            definition,
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId),
            _fixture.AggregateActorTypeName);

        AggregateStreamMetadata metadata = await actor.GetStreamMetadataAsync().ConfigureAwait(true);
        EventEnvelope[] seededEvents = await actor.ReadEventsRangeAsync(0, 3, 3).ConfigureAwait(true);

        metadata.Exists.ShouldBeTrue();
        metadata.CurrentSequence.ShouldBe(3);
        seededEvents.Select(static item => item.SequenceNumber).ShouldBe([1L, 2L, 3L]);
        seededEvents.Select(static item => item.GlobalPosition).ShouldBe([
            receipt.FirstGlobalPosition,
            receipt.FirstGlobalPosition + 1,
            receipt.FirstGlobalPosition + 2,
        ]);

        CommandEnvelope command = new CommandEnvelopeBuilder()
            .WithTenantId(identity.TenantId)
            .WithDomain(identity.Domain)
            .WithAggregateId(identity.AggregateId)
            .WithCommandType("IncrementCounter")
            .Build();
        CommandProcessingResult result = await actor.ProcessCommandAsync(command).ConfigureAwait(true);

        result.Accepted.ShouldBeTrue();
        result.EventCount.ShouldBe(1);
        (_, object? capturedState) = _fixture.DomainServiceInvoker.InvocationsWithState
            .Last(item => string.Equals(item.Command.AggregateId, aggregateId, StringComparison.Ordinal));
        DomainServiceCurrentState state = capturedState.ShouldBeOfType<DomainServiceCurrentState>();
        state.LastSnapshotSequence.ShouldBe(1);
        state.CurrentSequence.ShouldBe(3);
        state.Events.Select(static item => item.Metadata.SequenceNumber).ShouldBe([2L, 3L]);
        JsonElement snapshotState = state.SnapshotState.ShouldBeOfType<JsonElement>();
        snapshotState.GetProperty("Count").GetInt32().ShouldBe(1);

        (await actor.GetCurrentSequenceAsync().ConfigureAwait(true)).ShouldBe(4);
        EventEnvelope appended = (await actor.ReadEventsRangeAsync(3, 4, 1).ConfigureAwait(true)).ShouldHaveSingleItem();
        appended.SequenceNumber.ShouldBe(4);
        appended.GlobalPosition.ShouldBe(receipt.LastGlobalPosition + 1);
    }

    private static BenchmarkDatasetDefinition CreateDefinition(AggregateIdentity identity) {
        BenchmarkEventDefinition[] events = [
            .. Enumerable.Range(1, 3).Select(sequence => new BenchmarkEventDefinition(
                $"benchmark-message-{sequence}",
                _timestamp.AddSeconds(sequence),
                $"benchmark-correlation-{sequence}",
                $"benchmark-causation-{sequence}",
                "benchmark-user",
                "1.0.0",
                typeof(CounterIncremented).FullName!,
                1,
                "json",
                JsonSerializer.SerializeToUtf8Bytes(new CounterIncremented()),
                EventStorePayloadProtectionMetadata.Unprotected())),
        ];
        var snapshot = new BenchmarkSnapshotDefinition(
            1,
            JsonSerializer.SerializeToElement(new { Count = 1 }),
            _timestamp,
            EventStorePayloadProtectionMetadata.Unprotected());
        var aggregate = new BenchmarkAggregateDefinition(identity, "Counter", events, snapshot);
        return new BenchmarkDatasetDefinition("live-production-reader-proof", [aggregate]);
    }
}
