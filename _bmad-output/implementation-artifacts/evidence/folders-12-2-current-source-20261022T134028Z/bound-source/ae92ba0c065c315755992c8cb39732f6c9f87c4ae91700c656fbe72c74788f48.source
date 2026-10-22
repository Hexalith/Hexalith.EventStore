using System.Text.Json;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Client.Tests.Streams;

internal static class NativePublicationFixture
{
    private static readonly Func<EventEnvelope, string> RawDigest = typeof(EventEnvelope).Assembly
        .GetType("Hexalith.EventStore.Server.Events.SourceProofCodec")!.GetMethod("ComputeEventHash",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
        .CreateDelegate<Func<EventEnvelope, string>>();

    internal static AuthoritativeEventStream Stream(AggregateIdentity identity, DateTimeOffset observedAt, int head = 2,
        string incarnation = "01ARZ3NDEKTSV4RRFFQ69G5FAV")
    {
        var proof = NativeSourceProofCodec.CreateSeed(identity, incarnation);
        var events = new List<StreamReadEvent>();
        for (int sequence = 1; sequence <= head; sequence++)
        {
            var envelope = new EventEnvelope("event-" + sequence, identity.AggregateId, "Aggregate", identity.TenantId,
                identity.Domain, sequence, sequence, observedAt, "corr", "cause", "actor", "v1", "Publication", 1, "json",
                JsonSerializer.SerializeToUtf8Bytes(new[] { $"safe-publication-{sequence}" }), null);
            string digest = RawDigest(envelope);
            proof = NativeSourceProofCodec.ExtendDigest(proof, identity, sequence, envelope.MessageId, digest);
            events.Add(new(sequence, envelope.EventTypeName, envelope.Payload, "json", 1, envelope.MessageId,
                null, null, observedAt, "actor") { NativeEventDigest = digest });
        }
        return new(identity, head, observedAt, events.AsReadOnly(), "native-source") { NativeProof = proof };
    }
}
