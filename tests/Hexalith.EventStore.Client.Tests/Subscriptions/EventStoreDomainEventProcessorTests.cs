using System.Text;
using System.Text.Json;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Events;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Subscriptions;

public class EventStoreDomainEventProcessorTests {
    private static readonly string s_messageId = UniqueIdHelper.GenerateSortableUniqueStringId();
    private static readonly string s_secondMessageId = UniqueIdHelper.GenerateSortableUniqueStringId();
    private static readonly string s_duplicateMessageId = UniqueIdHelper.GenerateSortableUniqueStringId();

    public sealed record TestEvent(string TenantId, int Value) : IEventPayload;

    public sealed record MultiIdentityEvent(string TenantId, string AccountId, int Value) : IEventPayload;

    private sealed class CapturingHandler : IEventStoreDomainEventHandler<TestEvent> {
        public List<(TestEvent Event, EventStoreDomainEventContext Context)> Handled { get; } = [];

        public Task HandleAsync(TestEvent @event, EventStoreDomainEventContext context, CancellationToken cancellationToken = default) {
            Handled.Add((@event, context));
            return Task.CompletedTask;
        }
    }

    private sealed class MultiIdentityCapturingHandler : IEventStoreDomainEventHandler<MultiIdentityEvent> {
        public List<(MultiIdentityEvent Event, EventStoreDomainEventContext Context)> Handled { get; } = [];

        public Task HandleAsync(MultiIdentityEvent @event, EventStoreDomainEventContext context, CancellationToken cancellationToken = default) {
            Handled.Add((@event, context));
            return Task.CompletedTask;
        }
    }

    private sealed class FailingOnceHandler : IEventStoreDomainEventHandler<TestEvent> {
        private bool _shouldFail = true;

        public int InvocationCount { get; private set; }

        public Task HandleAsync(TestEvent @event, EventStoreDomainEventContext context, CancellationToken cancellationToken = default) {
            InvocationCount++;
            if (_shouldFail) {
                _shouldFail = false;
                throw new InvalidOperationException("synthetic handler failure");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class CompletionFailingMarkerStore : IEventStoreDomainEventMarkerStore {
        public int ReleaseCount { get; private set; }

        public Task<EventStoreDomainEventMarkerAcquisitionResult> TryAcquireAsync(
            string messageId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(EventStoreDomainEventMarkerAcquisitionResult.Acquired);

        public Task MarkCompletedAsync(string messageId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("synthetic marker completion failure");

        public Task ReleaseAsync(string messageId, CancellationToken cancellationToken = default) {
            ReleaseCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedAcquisitionMarkerStore : IEventStoreDomainEventMarkerStore {
        private readonly EventStoreDomainEventMarkerAcquisitionResult _result;

        public FixedAcquisitionMarkerStore(EventStoreDomainEventMarkerAcquisitionResult result) => _result = result;

        public int AcquireCount { get; private set; }

        public int MarkCompletedCount { get; private set; }

        public Task<EventStoreDomainEventMarkerAcquisitionResult> TryAcquireAsync(
            string messageId,
            CancellationToken cancellationToken = default) {
            AcquireCount++;
            return Task.FromResult(_result);
        }

        public Task MarkCompletedAsync(string messageId, CancellationToken cancellationToken = default) {
            MarkCompletedCount++;
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(string messageId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private static EventStoreDomainEventEnvelope Envelope(string messageId, string aggregateId, byte[] payload)
        => new(
            messageId,
            aggregateId,
            TenantId: "system",
            EventTypeName: typeof(TestEvent).FullName!,
            SequenceNumber: 1,
            Timestamp: DateTimeOffset.UnixEpoch,
            CorrelationId: "corr-1",
            SerializationFormat: "json",
            Payload: payload);

    private static EventStoreDomainEventEnvelope MultiIdentityEnvelope(string messageId, string aggregateId, byte[] payload)
        => new(
            messageId,
            aggregateId,
            TenantId: "system",
            EventTypeName: typeof(MultiIdentityEvent).FullName!,
            SequenceNumber: 1,
            Timestamp: DateTimeOffset.UnixEpoch,
            CorrelationId: "corr-1",
            SerializationFormat: "json",
            Payload: payload);

    private static (EventStoreDomainEventProcessor Processor, CapturingHandler Handler, InMemoryEventStoreDomainEventMarkerStore MarkerStore) Build(string? payloadIdProperty = null) {
        var handler = new CapturingHandler();
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IEventStoreDomainEventHandler<TestEvent>>(handler)
            .BuildServiceProvider();

        var registry = new Dictionary<string, Type>(StringComparer.Ordinal) {
            [typeof(TestEvent).FullName!] = typeof(TestEvent),
        };
        var markerStore = new InMemoryEventStoreDomainEventMarkerStore();

        var processor = new EventStoreDomainEventProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            registry,
            markerStore,
            NullLogger<EventStoreDomainEventProcessor>.Instance,
            payloadIdProperty);

        return (processor, handler, markerStore);
    }

    private static byte[] PayloadFor(string tenantId, int value)
        => JsonSerializer.SerializeToUtf8Bytes(new TestEvent(tenantId, value));

    private static byte[] MultiIdentityPayloadFor(string tenantId, string accountId, int value)
        => JsonSerializer.SerializeToUtf8Bytes(new MultiIdentityEvent(tenantId, accountId, value));

    [Fact]
    public async Task ProcessAsync_KnownEventWithHandler_DispatchesAndReturnsProcessed() {
        (EventStoreDomainEventProcessor processor, CapturingHandler handler, _) = Build();

        EventStoreDomainEventProcessingResult result = await processor
            .ProcessAsync(Envelope(s_messageId, "t1", PayloadFor("t1", 42)));

        result.ShouldBe(EventStoreDomainEventProcessingResult.Processed);
        handler.Handled.Count.ShouldBe(1);
        handler.Handled[0].Event.Value.ShouldBe(42);
        handler.Handled[0].Context.AggregateId.ShouldBe("t1");
        handler.Handled[0].Context.CorrelationId.ShouldBe("corr-1");
    }

    [Fact]
    public async Task ProcessAsync_SameMessageIdTwice_SecondIsDuplicate() {
        (EventStoreDomainEventProcessor processor, CapturingHandler handler, _) = Build();

        _ = await processor.ProcessAsync(Envelope(s_duplicateMessageId, "t1", PayloadFor("t1", 1)));
        EventStoreDomainEventProcessingResult second = await processor.ProcessAsync(
            Envelope(s_duplicateMessageId, "t1", Encoding.UTF8.GetBytes("{ not json")));

        second.ShouldBe(EventStoreDomainEventProcessingResult.Duplicate);
        handler.Handled.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ProcessAsync_UnknownEventType_ReturnsSkippedUnknownEventType() {
        (EventStoreDomainEventProcessor processor, _, _) = Build();
        EventStoreDomainEventEnvelope envelope = Envelope(s_messageId, "t1", PayloadFor("t1", 1)) with { EventTypeName = "Not.A.Known.Type" };

        EventStoreDomainEventProcessingResult result = await processor.ProcessAsync(envelope);

        result.ShouldBe(EventStoreDomainEventProcessingResult.SkippedUnknownEventType);

        EventStoreDomainEventProcessingResult duplicate = await processor.ProcessAsync(envelope);
        duplicate.ShouldBe(EventStoreDomainEventProcessingResult.Duplicate);
    }

    [Fact]
    public async Task ProcessAsync_NoHandlerRegistered_ReturnsSkippedNoHandlers() {
        ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        var registry = new Dictionary<string, Type>(StringComparer.Ordinal) {
            [typeof(TestEvent).FullName!] = typeof(TestEvent),
        };
        var processor = new EventStoreDomainEventProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            registry,
            new InMemoryEventStoreDomainEventMarkerStore(),
            NullLogger<EventStoreDomainEventProcessor>.Instance);

        EventStoreDomainEventProcessingResult result = await processor.ProcessAsync(Envelope(s_messageId, "t1", PayloadFor("t1", 1)));

        result.ShouldBe(EventStoreDomainEventProcessingResult.SkippedNoHandlers);
    }

    [Fact]
    public async Task ProcessAsync_MalformedPayload_ReturnsFailedInvalidPayloadAndMarksCompleted() {
        (EventStoreDomainEventProcessor processor, CapturingHandler handler, _) = Build();
        byte[] garbage = Encoding.UTF8.GetBytes("{ not json");
        EventStoreDomainEventEnvelope envelope = Envelope(s_messageId, "t1", garbage);

        EventStoreDomainEventProcessingResult first = await processor.ProcessAsync(envelope);
        first.ShouldBe(EventStoreDomainEventProcessingResult.FailedInvalidPayload);

        EventStoreDomainEventProcessingResult retry = await processor.ProcessAsync(envelope with { Payload = PayloadFor("t1", 7) });
        retry.ShouldBe(EventStoreDomainEventProcessingResult.Duplicate);
        handler.Handled.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_PayloadIdCheckEnabled_MismatchSkipped() {
        (EventStoreDomainEventProcessor processor, CapturingHandler handler, _) = Build(payloadIdProperty: "TenantId");

        // Envelope aggregate id "t1" but payload TenantId "other" — the event belongs to a different
        // aggregate (e.g. another aggregate type sharing the topic). It is skipped and acknowledged, not
        // dispatched and not retried.
        EventStoreDomainEventProcessingResult result = await processor
            .ProcessAsync(Envelope(s_messageId, "t1", PayloadFor("other", 1)));

        result.ShouldBe(EventStoreDomainEventProcessingResult.SkippedAggregateMismatch);
        handler.Handled.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_PayloadIdCheckEnabled_MatchAccepted() {
        (EventStoreDomainEventProcessor processor, CapturingHandler handler, _) = Build(payloadIdProperty: "TenantId");

        EventStoreDomainEventProcessingResult result = await processor
            .ProcessAsync(Envelope(s_messageId, "t1", PayloadFor("t1", 1)));

        result.ShouldBe(EventStoreDomainEventProcessingResult.Processed);
        handler.Handled.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ProcessAsync_HandlerException_ReleasesMarkerSoRedeliveryCanRun() {
        var handler = new FailingOnceHandler();
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IEventStoreDomainEventHandler<TestEvent>>(handler)
            .BuildServiceProvider();
        var registry = new Dictionary<string, Type>(StringComparer.Ordinal) {
            [typeof(TestEvent).FullName!] = typeof(TestEvent),
        };
        var processor = new EventStoreDomainEventProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            registry,
            new InMemoryEventStoreDomainEventMarkerStore(),
            NullLogger<EventStoreDomainEventProcessor>.Instance);
        EventStoreDomainEventEnvelope envelope = Envelope(s_messageId, "t1", PayloadFor("t1", 7));

        _ = await Should.ThrowAsync<InvalidOperationException>(() => processor.ProcessAsync(envelope));
        EventStoreDomainEventProcessingResult retry = await processor.ProcessAsync(envelope);

        retry.ShouldBe(EventStoreDomainEventProcessingResult.Processed);
        handler.InvocationCount.ShouldBe(2);
    }

    [Fact]
    public async Task ProcessAsync_MarkCompletedFailureAfterHandlerSuccess_DoesNotReleaseMarker() {
        var handler = new CapturingHandler();
        var markerStore = new CompletionFailingMarkerStore();
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IEventStoreDomainEventHandler<TestEvent>>(handler)
            .BuildServiceProvider();
        var registry = new Dictionary<string, Type>(StringComparer.Ordinal) {
            [typeof(TestEvent).FullName!] = typeof(TestEvent),
        };
        var processor = new EventStoreDomainEventProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            registry,
            markerStore,
            NullLogger<EventStoreDomainEventProcessor>.Instance);

        EventStoreDomainEventProcessingResult result = await processor
            .ProcessAsync(Envelope(s_messageId, "t1", PayloadFor("t1", 7)));

        result.ShouldBe(EventStoreDomainEventProcessingResult.Processed);
        handler.Handled.Count.ShouldBe(1);
        markerStore.ReleaseCount.ShouldBe(0);
    }

    [Fact]
    public async Task ProcessAsync_PayloadIdentityCache_IsScopedByEventTypeAndPropertyName() {
        var firstHandler = new MultiIdentityCapturingHandler();
        var secondHandler = new MultiIdentityCapturingHandler();
        var registry = new Dictionary<string, Type>(StringComparer.Ordinal) {
            [typeof(MultiIdentityEvent).FullName!] = typeof(MultiIdentityEvent),
        };
        ServiceProvider firstProvider = new ServiceCollection()
            .AddSingleton<IEventStoreDomainEventHandler<MultiIdentityEvent>>(firstHandler)
            .BuildServiceProvider();
        ServiceProvider secondProvider = new ServiceCollection()
            .AddSingleton<IEventStoreDomainEventHandler<MultiIdentityEvent>>(secondHandler)
            .BuildServiceProvider();
        var firstProcessor = new EventStoreDomainEventProcessor(
            firstProvider.GetRequiredService<IServiceScopeFactory>(),
            registry,
            new InMemoryEventStoreDomainEventMarkerStore(),
            NullLogger<EventStoreDomainEventProcessor>.Instance,
            "TenantId");
        var secondProcessor = new EventStoreDomainEventProcessor(
            secondProvider.GetRequiredService<IServiceScopeFactory>(),
            registry,
            new InMemoryEventStoreDomainEventMarkerStore(),
            NullLogger<EventStoreDomainEventProcessor>.Instance,
            "AccountId");

        EventStoreDomainEventProcessingResult first = await firstProcessor.ProcessAsync(
            MultiIdentityEnvelope(s_messageId, "tenant-1", MultiIdentityPayloadFor("tenant-1", "account-1", 1)));
        EventStoreDomainEventProcessingResult second = await secondProcessor.ProcessAsync(
            MultiIdentityEnvelope(s_secondMessageId, "account-1", MultiIdentityPayloadFor("tenant-1", "account-1", 2)));

        first.ShouldBe(EventStoreDomainEventProcessingResult.Processed);
        second.ShouldBe(EventStoreDomainEventProcessingResult.Processed);
        firstHandler.Handled.Count.ShouldBe(1);
        secondHandler.Handled.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("MessageId")]
    [InlineData("AggregateId")]
    [InlineData("TenantId")]
    [InlineData("EventTypeName")]
    [InlineData("CorrelationId")]
    [InlineData("SerializationFormat")]
    public async Task ProcessAsync_BlankEnvelopeIdentity_ReturnsFailedInvalidPayload(string propertyName) {
        (EventStoreDomainEventProcessor processor, CapturingHandler handler, _) = Build();
        EventStoreDomainEventEnvelope envelope = propertyName switch {
            "MessageId" => Envelope(" ", "t1", PayloadFor("t1", 1)),
            "AggregateId" => Envelope(s_messageId, " ", PayloadFor("t1", 1)),
            "TenantId" => Envelope(s_messageId, "t1", PayloadFor("t1", 1)) with { TenantId = " " },
            "EventTypeName" => Envelope(s_messageId, "t1", PayloadFor("t1", 1)) with { EventTypeName = " " },
            "CorrelationId" => Envelope(s_messageId, "t1", PayloadFor("t1", 1)) with { CorrelationId = " " },
            _ => Envelope(s_messageId, "t1", PayloadFor("t1", 1)) with { SerializationFormat = " " },
        };

        EventStoreDomainEventProcessingResult result = await processor.ProcessAsync(envelope);

        result.ShouldBe(EventStoreDomainEventProcessingResult.FailedInvalidPayload);
        handler.Handled.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_UnsupportedSerializationFormat_ReturnsFailedInvalidPayloadAndCompletesMarker() {
        (EventStoreDomainEventProcessor processor, CapturingHandler handler, _) = Build();
        EventStoreDomainEventEnvelope envelope = Envelope(s_messageId, "t1", PayloadFor("t1", 1)) with { SerializationFormat = "xml" };

        EventStoreDomainEventProcessingResult result = await processor.ProcessAsync(envelope);
        EventStoreDomainEventProcessingResult retry = await processor.ProcessAsync(envelope with { SerializationFormat = "json" });

        result.ShouldBe(EventStoreDomainEventProcessingResult.FailedInvalidPayload);
        retry.ShouldBe(EventStoreDomainEventProcessingResult.Duplicate);
        handler.Handled.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_InvalidMessageId_ReturnsFailedInvalidPayloadBeforeMarkerAcquire() {
        var handler = new CapturingHandler();
        var markerStore = new FixedAcquisitionMarkerStore(EventStoreDomainEventMarkerAcquisitionResult.Acquired);
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IEventStoreDomainEventHandler<TestEvent>>(handler)
            .BuildServiceProvider();
        var registry = new Dictionary<string, Type>(StringComparer.Ordinal) {
            [typeof(TestEvent).FullName!] = typeof(TestEvent),
        };
        var processor = new EventStoreDomainEventProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            registry,
            markerStore,
            NullLogger<EventStoreDomainEventProcessor>.Instance);

        EventStoreDomainEventProcessingResult result = await processor.ProcessAsync(
            Envelope("not-a-ulid", "t1", PayloadFor("t1", 1)));

        result.ShouldBe(EventStoreDomainEventProcessingResult.FailedInvalidPayload);
        markerStore.AcquireCount.ShouldBe(0);
        handler.Handled.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_UnsupportedMarkerAcquisitionResult_KeepsDeliveryRetryable() {
        var handler = new CapturingHandler();
        var markerStore = new FixedAcquisitionMarkerStore((EventStoreDomainEventMarkerAcquisitionResult)999);
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IEventStoreDomainEventHandler<TestEvent>>(handler)
            .BuildServiceProvider();
        var registry = new Dictionary<string, Type>(StringComparer.Ordinal) {
            [typeof(TestEvent).FullName!] = typeof(TestEvent),
        };
        var processor = new EventStoreDomainEventProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            registry,
            markerStore,
            NullLogger<EventStoreDomainEventProcessor>.Instance);

        EventStoreDomainEventProcessingResult result = await processor.ProcessAsync(
            Envelope(s_messageId, "t1", PayloadFor("t1", 1)));

        result.ShouldBe(EventStoreDomainEventProcessingResult.RetryableInProgress);
        markerStore.MarkCompletedCount.ShouldBe(0);
        handler.Handled.ShouldBeEmpty();
    }
}
