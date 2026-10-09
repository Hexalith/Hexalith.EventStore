using System.Text.Json;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Client.Tests.Events;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Subscriptions;

public sealed class VersionedSubscriptionTests
{
    private sealed class CapturingHandler : IEventStoreDomainEventHandler<VersionedTestEvent>
    {
        public List<int> Values { get; } = [];

        public Task HandleAsync(VersionedTestEvent @event, EventStoreDomainEventContext context,
            CancellationToken cancellationToken = default)
        {
            Values.Add(@event.Value);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task HistoricalNameRunsRenameAndReachesCurrentSubscriber()
    {
        var handler = new CapturingHandler();
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IEventStoreDomainEventHandler<VersionedTestEvent>>(handler)
            .BuildServiceProvider();
        var marker = new InMemoryEventStoreDomainEventMarkerStore();
        var registry = new EventPayloadEvolutionRegistry(
            [typeof(VersionedTestEvent)],
            [
                new TestPayloadUpcaster("Old.Billing.LegacyTestEvent", 1,
                    typeof(VersionedTestEvent).FullName, json => { json["Value"] = json["Amount"]!.GetValue<int>(); json.Remove("Amount"); return json; }),
                new TestPayloadUpcaster(typeof(VersionedTestEvent).FullName!, 2, null, json => json),
            ]);
        var processor = new EventStoreDomainEventProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new Dictionary<string, Type> { [typeof(VersionedTestEvent).FullName!] = typeof(VersionedTestEvent) },
            marker,
            NullLogger<EventStoreDomainEventProcessor>.Instance,
            evolution: registry);
        var envelope = new EventStoreDomainEventEnvelope(
            UniqueIdHelper.GenerateSortableUniqueStringId(), "account-1", "tenant-1",
            "Old.Billing.LegacyTestEvent", 1, DateTimeOffset.UnixEpoch,
            "correlation-1", "json", JsonSerializer.SerializeToUtf8Bytes(new { Amount = 7 }))
        {
            PayloadVersion = 1,
        };

        EventStoreDomainEventProcessingResult result = await processor.ProcessAsync(envelope);

        result.ShouldBe(EventStoreDomainEventProcessingResult.Processed);
        handler.Values.ShouldBe([7]);
        EventStoreDomainEventProcessingResult duplicate = await processor.ProcessAsync(envelope);
        duplicate.ShouldBe(EventStoreDomainEventProcessingResult.Duplicate);
    }
}
