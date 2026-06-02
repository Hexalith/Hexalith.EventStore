using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Events;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Subscriptions;

public class EventStoreDomainEventProcessorTests {
    public sealed record TestEvent(string TenantId, int Value) : IEventPayload;

    private sealed class CapturingHandler : IEventStoreDomainEventHandler<TestEvent> {
        public List<(TestEvent Event, EventStoreDomainEventContext Context)> Handled { get; } = [];

        public Task HandleAsync(TestEvent @event, EventStoreDomainEventContext context, CancellationToken cancellationToken = default) {
            Handled.Add((@event, context));
            return Task.CompletedTask;
        }
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

    private static (EventStoreDomainEventProcessor Processor, CapturingHandler Handler) Build(string? payloadIdProperty = null) {
        var handler = new CapturingHandler();
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IEventStoreDomainEventHandler<TestEvent>>(handler)
            .BuildServiceProvider();

        var registry = new Dictionary<string, Type>(StringComparer.Ordinal) {
            [typeof(TestEvent).FullName!] = typeof(TestEvent),
        };

        var processor = new EventStoreDomainEventProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            registry,
            NullLogger<EventStoreDomainEventProcessor>.Instance,
            payloadIdProperty);

        return (processor, handler);
    }

    private static byte[] PayloadFor(string tenantId, int value)
        => JsonSerializer.SerializeToUtf8Bytes(new TestEvent(tenantId, value));

    [Fact]
    public async Task ProcessAsync_KnownEventWithHandler_DispatchesAndReturnsProcessed() {
        (EventStoreDomainEventProcessor processor, CapturingHandler handler) = Build();

        EventStoreDomainEventProcessingResult result = await processor
            .ProcessAsync(Envelope("m1", "t1", PayloadFor("t1", 42)));

        result.ShouldBe(EventStoreDomainEventProcessingResult.Processed);
        handler.Handled.Count.ShouldBe(1);
        handler.Handled[0].Event.Value.ShouldBe(42);
        handler.Handled[0].Context.AggregateId.ShouldBe("t1");
        handler.Handled[0].Context.CorrelationId.ShouldBe("corr-1");
    }

    [Fact]
    public async Task ProcessAsync_SameMessageIdTwice_SecondIsDuplicate() {
        (EventStoreDomainEventProcessor processor, CapturingHandler handler) = Build();

        _ = await processor.ProcessAsync(Envelope("dup", "t1", PayloadFor("t1", 1)));
        EventStoreDomainEventProcessingResult second = await processor.ProcessAsync(Envelope("dup", "t1", PayloadFor("t1", 2)));

        second.ShouldBe(EventStoreDomainEventProcessingResult.Duplicate);
        handler.Handled.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ProcessAsync_UnknownEventType_ReturnsSkippedUnknownEventType() {
        (EventStoreDomainEventProcessor processor, _) = Build();
        EventStoreDomainEventEnvelope envelope = Envelope("m1", "t1", PayloadFor("t1", 1)) with { EventTypeName = "Not.A.Known.Type" };

        EventStoreDomainEventProcessingResult result = await processor.ProcessAsync(envelope);

        result.ShouldBe(EventStoreDomainEventProcessingResult.SkippedUnknownEventType);
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
            NullLogger<EventStoreDomainEventProcessor>.Instance);

        EventStoreDomainEventProcessingResult result = await processor.ProcessAsync(Envelope("m1", "t1", PayloadFor("t1", 1)));

        result.ShouldBe(EventStoreDomainEventProcessingResult.SkippedNoHandlers);
    }

    [Fact]
    public async Task ProcessAsync_MalformedPayload_ReturnsFailedInvalidPayloadAndAllowsRetry() {
        (EventStoreDomainEventProcessor processor, _) = Build();
        byte[] garbage = Encoding.UTF8.GetBytes("{ not json");

        EventStoreDomainEventProcessingResult first = await processor.ProcessAsync(Envelope("m1", "t1", garbage));
        first.ShouldBe(EventStoreDomainEventProcessingResult.FailedInvalidPayload);

        // The message ID was released so a corrected redelivery can still be processed.
        EventStoreDomainEventProcessingResult retry = await processor.ProcessAsync(Envelope("m1", "t1", PayloadFor("t1", 7)));
        retry.ShouldBe(EventStoreDomainEventProcessingResult.Processed);
    }

    [Fact]
    public async Task ProcessAsync_PayloadIdCheckEnabled_MismatchRejected() {
        (EventStoreDomainEventProcessor processor, CapturingHandler handler) = Build(payloadIdProperty: "TenantId");

        // Envelope aggregate id "t1" but payload TenantId "other" — integrity check rejects.
        EventStoreDomainEventProcessingResult result = await processor
            .ProcessAsync(Envelope("m1", "t1", PayloadFor("other", 1)));

        result.ShouldBe(EventStoreDomainEventProcessingResult.FailedInvalidPayload);
        handler.Handled.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_PayloadIdCheckEnabled_MatchAccepted() {
        (EventStoreDomainEventProcessor processor, CapturingHandler handler) = Build(payloadIdProperty: "TenantId");

        EventStoreDomainEventProcessingResult result = await processor
            .ProcessAsync(Envelope("m1", "t1", PayloadFor("t1", 1)));

        result.ShouldBe(EventStoreDomainEventProcessingResult.Processed);
        handler.Handled.Count.ShouldBe(1);
    }
}
