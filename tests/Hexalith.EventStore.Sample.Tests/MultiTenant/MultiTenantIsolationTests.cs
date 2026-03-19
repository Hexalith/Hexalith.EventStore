using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.Events;

namespace Hexalith.EventStore.Sample.Tests.MultiTenant;

/// <summary>
/// Tests verifying FR22/FR25 multi-tenant isolation at the contract and convention layers.
/// Domain services are pure functions — tenant routing is transparent to domain logic.
/// </summary>
public sealed class MultiTenantIsolationTests {
    // ── Task 3.1: AggregateIdentity tenant differentiation ──

    [Fact]
    public void AggregateIdentity_DifferentTenants_SameDomainAndAggregateId_ProduceDistinctIdentities() {
        var identityA = new AggregateIdentity("tenant-a", "counter", "counter-1");
        var identityB = new AggregateIdentity("tenant-b", "counter", "counter-1");

        Assert.NotEqual(identityA, identityB);
        Assert.NotEqual(identityA.ActorId, identityB.ActorId);
        Assert.NotEqual(identityA.EventStreamKeyPrefix, identityB.EventStreamKeyPrefix);
        Assert.NotEqual(identityA.MetadataKey, identityB.MetadataKey);
        Assert.NotEqual(identityA.SnapshotKey, identityB.SnapshotKey);
        Assert.NotEqual(identityA.PubSubTopic, identityB.PubSubTopic);
    }

    // ── Task 3.2: NamingConventionEngine tenant-scoped topics ──

    [Fact]
    public void GetPubSubTopic_DifferentTenants_ProducesTenantScopedTopics() {
        string topicA = NamingConventionEngine.GetPubSubTopic("tenant-a", "counter");
        string topicB = NamingConventionEngine.GetPubSubTopic("tenant-b", "counter");
        string greetingTopicA = NamingConventionEngine.GetPubSubTopic("tenant-a", "greeting");
        string greetingTopicB = NamingConventionEngine.GetPubSubTopic("tenant-b", "greeting");

        Assert.Equal("tenant-a.counter.events", topicA);
        Assert.Equal("tenant-b.counter.events", topicB);
        Assert.Equal("tenant-a.greeting.events", greetingTopicA);
        Assert.Equal("tenant-b.greeting.events", greetingTopicB);
        Assert.NotEqual(topicA, topicB);
        Assert.NotEqual(greetingTopicA, greetingTopicB);
    }

    // ── Task 3.3: Pure function contract — tenant does not affect domain logic ──

    [Fact]
    public async Task CounterAggregate_SameCommandAndRehydratedState_DifferentTenants_ProducesIdenticalResults() {
        var aggregateA = new CounterAggregate();
        var aggregateB = new CounterAggregate();

        CommandEnvelope commandA = CreateCommand("tenant-a", new DecrementCounter());
        CommandEnvelope commandB = CreateCommand("tenant-b", new DecrementCounter());
        JsonElement rehydratedState = CreateRehydratedCounterState(count: 1);

        DomainResult resultA = await aggregateA.ProcessAsync(commandA, rehydratedState);
        DomainResult resultB = await aggregateB.ProcessAsync(commandB, rehydratedState);

        Assert.True(resultA.IsSuccess);
        Assert.True(resultB.IsSuccess);

        CounterDecremented eventA = Assert.IsType<CounterDecremented>(Assert.Single(resultA.Events));
        CounterDecremented eventB = Assert.IsType<CounterDecremented>(Assert.Single(resultB.Events));

        Assert.Equal(DomainServiceWireResult.FromDomainResult(resultA).IsRejection, DomainServiceWireResult.FromDomainResult(resultB).IsRejection);
        Assert.Equal(eventA, eventB);
    }

    private static CommandEnvelope CreateCommand<T>(string tenantId, T command)
        where T : notnull
        => new(
            MessageId: Guid.NewGuid().ToString(),
            TenantId: tenantId,
            Domain: "counter",
            AggregateId: "counter-1",
            CommandType: typeof(T).Name,
            Payload: JsonSerializer.SerializeToUtf8Bytes(command),
            CorrelationId: "corr-1",
            CausationId: null,
            UserId: "test-user",
            Extensions: null);

    private static JsonElement CreateRehydratedCounterState(int count) {
        string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{}"));
        string eventsJson = string.Join(",", Enumerable.Repeat($"{{\"eventTypeName\":\"CounterIncremented\",\"payload\":\"{payload}\"}}", count));
        return JsonSerializer.Deserialize<JsonElement>($"[{eventsJson}]");
    }
}
