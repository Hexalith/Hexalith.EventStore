using System.Net.Http.Json;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.Testing.Builders;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Events;

/// <summary>Exercises Story 6.6 V1 application-byte evidence through Dapr actor APIs.</summary>
/// <remarks>Redis is a Development test profile; these observations confer no production or provider-proof authority.</remarks>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public sealed class DaprEventEvolutionLogicalReadbackLiveSidecarTests(DaprTestContainerFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task TwoSidecarsAndRestartPreserveCommittedApplicationBytesAndMessageIdentity()
    {
        fixture.SetupCounterDomain();
        var identity = new AggregateIdentity("tenant-a", "counter", $"evolution-{Guid.NewGuid():N}");
        CommandEnvelope command = new CommandEnvelopeBuilder()
            .WithTenantId(identity.TenantId)
            .WithDomain(identity.Domain)
            .WithAggregateId(identity.AggregateId)
            .WithCommandType("IncrementCounter")
            .Build();
        IAggregateActor primary = CreateProxy(fixture.DaprHttpEndpoint, identity);

        CommandProcessingResult first = await primary.ProcessCommandAsync(command).ConfigureAwait(true);
        first.Accepted.ShouldBeTrue();
        EventEnvelope original = await ReadStateAsync<EventEnvelope>(
            fixture.DaprHttpEndpoint, identity, $"{identity.EventStreamKeyPrefix}1").ConfigureAwait(true);
        original.ApplicationPayloadDigest.ShouldNotBeNullOrWhiteSpace();
        original.MetadataVersion.ShouldBe(1);
        original.EventContractType.ShouldBeNull();
        original.PayloadVersion.ShouldBeNull();
        EventLogicalDigest.RequireMatching(original, original.SerializationFormat, original.Payload);
        byte[] originalBytes = original.Payload.ToArray();
        string originalMessageId = original.MessageId;
        string originalDigest = original.ApplicationPayloadDigest!;

        try
        {
            await fixture.EnsureReplicaAsync().ConfigureAwait(true);
            IAggregateActor replica = CreateProxy(fixture.ReplicaDaprHttpEndpoint, identity);
            CommandProcessingResult duplicate = await replica.ProcessCommandAsync(command).ConfigureAwait(true);
            duplicate.Accepted.ShouldBeTrue();
            EventEnvelope secondHostRead = await ReadStateAsync<EventEnvelope>(
                fixture.ReplicaDaprHttpEndpoint, identity, $"{identity.EventStreamKeyPrefix}1").ConfigureAwait(true);
            RequireSameLogicalEvent(secondHostRead, originalBytes, originalMessageId, originalDigest);
            AggregateMetadata metadata = await ReadStateAsync<AggregateMetadata>(
                fixture.ReplicaDaprHttpEndpoint, identity, identity.MetadataKey).ConfigureAwait(true);
            metadata.CurrentSequence.ShouldBe(1);

            await fixture.RestartHostAndSidecarAsync().ConfigureAwait(true);
            primary = CreateProxy(fixture.DaprHttpEndpoint, identity);
            CommandProcessingResult afterRestart = await primary.ProcessCommandAsync(command).ConfigureAwait(true);
            afterRestart.Accepted.ShouldBeTrue();
            EventEnvelope restartedRead = await ReadStateAsync<EventEnvelope>(
                fixture.DaprHttpEndpoint, identity, $"{identity.EventStreamKeyPrefix}1").ConfigureAwait(true);
            RequireSameLogicalEvent(restartedRead, originalBytes, originalMessageId, originalDigest);
            (await ReadStateAsync<AggregateMetadata>(fixture.DaprHttpEndpoint, identity,
                identity.MetadataKey).ConfigureAwait(true)).CurrentSequence.ShouldBe(1);

            EventEnvelope[] inspected = await primary.ReadEventsRangeAsync(1, 1, 1).ConfigureAwait(true);
            inspected.Length.ShouldBe(1);
            RequireSameLogicalEvent(inspected[0], originalBytes, originalMessageId, originalDigest);
        }
        finally
        {
            await fixture.StopReplicaHostAndSidecarAsync().ConfigureAwait(true);
        }
    }

    private IAggregateActor CreateProxy(string endpoint, AggregateIdentity identity)
        => new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = endpoint })
            .CreateActorProxy<IAggregateActor>(new ActorId(identity.ActorId), fixture.AggregateActorTypeName);

    private async Task<T> ReadStateAsync<T>(string endpoint, AggregateIdentity identity, string key)
    {
        // The public Dapr actor-state GET supplies independent logical readback.
        // No provider key, database connection or physical representation is inspected.
        string path = $"{endpoint}/v1.0/actors/{Uri.EscapeDataString(fixture.AggregateActorTypeName)}"
            + $"/{Uri.EscapeDataString(identity.ActorId)}/state/{Uri.EscapeDataString(key)}";
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        return await client.GetFromJsonAsync<T>(path, JsonOptions, TestContext.Current.CancellationToken)
            .ConfigureAwait(true) ?? throw new InvalidOperationException("Dapr logical actor readback returned no value.");
    }

    private static void RequireSameLogicalEvent(EventEnvelope observed, byte[] payload, string messageId, string digest)
    {
        observed.SequenceNumber.ShouldBe(1);
        observed.Payload.ShouldBe(payload);
        observed.MessageId.ShouldBe(messageId);
        observed.ApplicationPayloadDigest.ShouldBe(digest);
        EventLogicalDigest.RequireMatching(observed, observed.SerializationFormat, observed.Payload);
    }
}
