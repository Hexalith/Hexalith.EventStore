using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using NSubstitute;

namespace Hexalith.EventStore.Client.Tests.Streams;

/// <summary>Actual shared feed/dispatcher with serialized conditional index end-state and synthetic source/namespace/exact-ack authorities.</summary>
internal sealed class SourcePublicationDispatcherFixture
{
    internal SourcePublicationScope Scope { get; } = new("tenant-a", "conversation", "approved-deletion-v1", "installation-1");
    internal AggregateIdentity Identity { get; } = new("tenant-a", "conversation", "source-a");
    internal ISourcePublicationNamespaceSource Namespace { get; } = Substitute.For<ISourcePublicationNamespaceSource>();
    internal IAuthoritativeEventStreamReader Streams { get; } = Substitute.For<IAuthoritativeEventStreamReader>();
    internal ISourcePublicationIndexStore Store { get; } = Substitute.For<ISourcePublicationIndexStore>();
    internal ISourcePublicationDelivery Delivery { get; } = Substitute.For<ISourcePublicationDelivery>();
    internal SourcePublicationFeed Feed { get; }
    internal SourcePublicationDispatcher Dispatcher { get; }
    internal byte[]? Persisted { get; private set; }
    internal HashSet<string> Acknowledged { get; } = [];
    internal List<long> Visited { get; } = [];
    internal long? StopAt { get; set; }
    internal bool Poison { get; set; }
    internal SourcePublicationDispatcherFixture(TimeProvider? clock = null, int count = 3)
    {
        clock ??= TimeProvider.System; DateTimeOffset now = clock.GetUtcNow();
        Namespace.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(new SourcePublicationCut(Scope, "current-namespace-1", now, now.AddHours(1), [new(Identity, count)], true));
        var events = Enumerable.Range(1, count).Select(n => new StreamReadEvent(n, "Publication", JsonSerializer.SerializeToUtf8Bytes(new[] { "safe-" + n }), "json", 1,
            "message-" + n, null, null, now, null)).ToArray();
        Streams.ReadAsync(Identity, Arg.Any<CancellationToken>()).Returns(new AuthoritativeStreamReadResult(new(Identity, count, now, events, "authenticated-observation"), null));
        var projector = Substitute.For<ISourcePublicationProjector>();
        projector.Project(Arg.Any<AuthoritativeEventStream>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<AuthoritativeEventStream>().Events.Select(e =>
            new SourcePublicationDescriptor("publication-" + e.SequenceNumber, Identity, e.SequenceNumber, e.MessageId, Convert.ToHexString(SHA256.HashData(e.Payload)))).ToArray());
        Store.ReadAsync(Scope, Arg.Any<CancellationToken>()).Returns(_ => Read());
        Store.TryWriteAsync(Scope, Arg.Any<long>(), Arg.Any<SourcePublicationIndexState>(), Arg.Any<CancellationToken>()).Returns(call => {
            if ((Read()?.Revision ?? 0) != call.Arg<long>()) { return false; }
            Persisted = JsonSerializer.SerializeToUtf8Bytes(call.Arg<SourcePublicationIndexState>()); return true;
        });
        Delivery.LookupAcknowledgementAsync(Arg.Any<SourcePublicationIndexEntry>(), Arg.Any<CancellationToken>()).Returns(call =>
            Acknowledged.Contains(call.Arg<SourcePublicationIndexEntry>().Publication.PublicationId) ? SourcePublicationDeliveryStatus.Acknowledged : SourcePublicationDeliveryStatus.Pending);
        Delivery.DeliverAsync(Arg.Any<SourcePublicationIndexEntry>(), Arg.Any<CancellationToken>()).Returns(call => {
            call.Arg<CancellationToken>().ThrowIfCancellationRequested(); var entry = call.Arg<SourcePublicationIndexEntry>(); Visited.Add(entry.Offset);
            if (entry.Offset == StopAt) { return Poison ? SourcePublicationDeliveryStatus.Quarantined : SourcePublicationDeliveryStatus.Unavailable; }
            Acknowledged.Add(entry.Publication.PublicationId); return SourcePublicationDeliveryStatus.Acknowledged;
        });
        Feed = new(Namespace, Streams, projector, Store, clock); Dispatcher = new(Feed, clock, Delivery);
    }
    internal SourcePublicationIndexState? Read() => Persisted is null ? null : JsonSerializer.Deserialize<SourcePublicationIndexState>(Persisted);
}
