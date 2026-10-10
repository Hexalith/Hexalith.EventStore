using System.Text.Json;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Client.Tests.Events;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Subscriptions;

public sealed class VersionedSubscriptionTests
{
    private sealed class CapturingLogger : ILogger<EventStoreDomainEventProcessor>
    {
        public List<IReadOnlyDictionary<string, object?>> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                Entries.Add(values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
            }
        }
    }

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

    [Fact]
    public async Task ForeignFullNameWithLocalShortNameSkipsWithoutDispatch()
    {
        var handler = new CapturingHandler();
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IEventStoreDomainEventHandler<VersionedTestEvent>>(handler)
            .BuildServiceProvider();
        string currentName = typeof(VersionedTestEvent).FullName!;
        var registry = new EventPayloadEvolutionRegistry([typeof(VersionedTestEvent)],
            [new TestPayloadUpcaster(currentName, 1, null, static json => json),
             new TestPayloadUpcaster(currentName, 2, null, static json => json)]);
        var processor = new EventStoreDomainEventProcessor(provider.GetRequiredService<IServiceScopeFactory>(),
            new Dictionary<string, Type> { [currentName] = typeof(VersionedTestEvent) },
            new InMemoryEventStoreDomainEventMarkerStore(),
            NullLogger<EventStoreDomainEventProcessor>.Instance, evolution: registry);
        var envelope = new EventStoreDomainEventEnvelope(
            UniqueIdHelper.GenerateSortableUniqueStringId(), "account-1", "tenant-1",
            "Totally.Foreign.VersionedTestEvent", 1, DateTimeOffset.UnixEpoch,
            "correlation-1", "json", "{\"Value\":7}"u8.ToArray()) { PayloadVersion = 3 };

        (await processor.ProcessAsync(envelope)).ShouldBe(EventStoreDomainEventProcessingResult.SkippedUnknownEventType);
        handler.Values.ShouldBeEmpty();
    }

    [Fact]
    public async Task CurrentVersionStampedEnvelopeReachesHandler()
    {
        var handler = new CapturingHandler();
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IEventStoreDomainEventHandler<VersionedTestEvent>>(handler)
            .BuildServiceProvider();
        string currentName = typeof(VersionedTestEvent).FullName!;
        var registry = new EventPayloadEvolutionRegistry([typeof(VersionedTestEvent)],
            [new TestPayloadUpcaster(currentName, 1, null, static json => json),
             new TestPayloadUpcaster(currentName, 2, null, static json => json)]);
        var processor = new EventStoreDomainEventProcessor(provider.GetRequiredService<IServiceScopeFactory>(),
            new Dictionary<string, Type> { [currentName] = typeof(VersionedTestEvent) },
            new InMemoryEventStoreDomainEventMarkerStore(),
            NullLogger<EventStoreDomainEventProcessor>.Instance, evolution: registry);
        var envelope = new EventStoreDomainEventEnvelope(
            UniqueIdHelper.GenerateSortableUniqueStringId(), "account-1", "tenant-1", currentName,
            1, DateTimeOffset.UnixEpoch, "correlation-1", "json", "{\"Value\":9}"u8.ToArray())
        { PayloadVersion = 3 };

        (await processor.ProcessAsync(envelope)).ShouldBe(EventStoreDomainEventProcessingResult.Processed);
        handler.Values.ShouldBe([9]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task FallbackKnownTypeWithDifferentStoredVersionRemainsRetryable(int storedVersion)
    {
        var handler = new CapturingHandler();
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IEventStoreDomainEventHandler<VersionedTestEvent>>(handler)
            .BuildServiceProvider();
        var processor = new EventStoreDomainEventProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new Dictionary<string, Type> { [typeof(VersionedTestEvent).FullName!] = typeof(VersionedTestEvent) },
            new InMemoryEventStoreDomainEventMarkerStore(),
            NullLogger<EventStoreDomainEventProcessor>.Instance,
            evolution: new EventPayloadEvolutionRegistry([], []));
        var envelope = new EventStoreDomainEventEnvelope(
            UniqueIdHelper.GenerateSortableUniqueStringId(), "account-1", "tenant-1",
            typeof(VersionedTestEvent).FullName!, 1, DateTimeOffset.UnixEpoch,
            "correlation-1", "json", "{\"Value\":7}"u8.ToArray())
        {
            PayloadVersion = storedVersion,
        };

        EventStoreDomainEventProcessingResult result = await processor.ProcessAsync(envelope);

        result.ShouldBe(EventStoreDomainEventProcessingResult.RetryableCapabilityMismatch);
        handler.Values.ShouldBeEmpty();
        (await processor.ProcessAsync(envelope)).ShouldBe(EventStoreDomainEventProcessingResult.RetryableCapabilityMismatch);
    }

    [Fact]
    public async Task ThrowingUpcasterLogsSafeReasonAndExceptionType()
    {
        var logger = new CapturingLogger();
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        string name = typeof(VersionTwoTestEvent).FullName!;
        var registry = new EventPayloadEvolutionRegistry([typeof(VersionTwoTestEvent)],
            [new TestPayloadUpcaster(name, 1, null,
                static _ => throw new InvalidOperationException("secret payload detail"))]);
        var processor = new EventStoreDomainEventProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(), new Dictionary<string, Type>(),
            new InMemoryEventStoreDomainEventMarkerStore(), logger, evolution: registry);
        var envelope = new EventStoreDomainEventEnvelope(
            UniqueIdHelper.GenerateSortableUniqueStringId(), "account-1", "tenant-1", name, 1,
            DateTimeOffset.UnixEpoch, "correlation-1", "json", "{}"u8.ToArray())
        {
            PayloadVersion = 1,
        };

        (await processor.ProcessAsync(envelope)).ShouldBe(EventStoreDomainEventProcessingResult.RetryableCapabilityMismatch);

        IReadOnlyDictionary<string, object?> fields = logger.Entries.Single(entry => entry.ContainsKey("Reason"));
        fields["Reason"].ShouldBe("upcaster failed");
        fields["InnerExceptionType"].ShouldBe(nameof(InvalidOperationException));
        fields["UpcasterType"].ShouldBe(typeof(TestPayloadUpcaster).FullName);
        fields.Values.Any(value => value is string text
            && text.Contains("secret payload detail", StringComparison.Ordinal)).ShouldBeFalse();
    }
}
