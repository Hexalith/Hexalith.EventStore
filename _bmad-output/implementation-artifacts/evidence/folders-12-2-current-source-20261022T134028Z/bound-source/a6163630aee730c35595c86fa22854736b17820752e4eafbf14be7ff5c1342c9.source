using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Independently protects complete-result private-copy scratch admission before serialization.</summary>
public sealed class BoundedV1DomainResultProducerScratchTests
{
    /// <summary>Checks scratch refusal precedes callbacks even when the maximum-size wire image fits.</summary>
    [Theory]
    [InlineData(42, true)]
    [InlineData(43, false)]
    public async Task SerializedSourceScratchBoundaryPrecedesFirstSerializer(int serializedCount, bool admitted)
    {
        const int maximumPayloadBytes = 1024 * 1024;
        byte[][] sources = Enumerable.Range(0, serializedCount).Select(index =>
        {
            byte[] bytes = new byte[maximumPayloadBytes];
            bytes.AsSpan().Fill((byte)(index + 1));
            return bytes;
        }).ToArray();
        byte[][] originalDigests = sources.Select(bytes => SHA256.HashData(bytes)).ToArray();
        int serializerCalls = 0;
        var producer = new BoundedV1DomainResultProducer([
            new(typeof(BoundedProducerTestEvent), "regular", "json", 16, (_, sink, _) =>
            {
                serializerCalls++;
                sink.Write([0x33]);
                return Task.CompletedTask;
            }),
            new(typeof(BoundedProducerSerializedTestEvent), "legacy-exact-alias", "json", maximumPayloadBytes,
                (_, _, _) => throw new InvalidOperationException("Admitted serialized input must not be serialized again.")),
        ]);

        IEventPayload[] payloads = [new BoundedProducerTestEvent(),
            .. sources.Select(bytes => (IEventPayload)new BoundedProducerSerializedTestEvent(bytes))];
        DomainResult input = DomainResult.Success(payloads);
        DomainServiceWireEvent[] maximumWireEvents = [new("regular", new byte[16], "json"),
            .. sources.Select(bytes => new DomainServiceWireEvent("legacy-exact-alias", bytes, "json"))];

        DomainServiceWireResult? output = null;
        try
        {
            // The real wire admission accepts each complete maximum-size image: encoded capacity cannot explain refusal.
            BoundedV1WireResultAdmission.Admit(new DomainServiceWireResult(false, maximumWireEvents), CancellationToken.None)
                .Events.Count.ShouldBe(serializedCount + 1);
            if (admitted)
            {
                output = await producer.ProduceAsync(input, CancellationToken.None);
                serializerCalls.ShouldBe(1);
                output.Events.Count.ShouldBe(serializedCount + 1);
                output.Events[0].EventTypeName.ShouldBe("regular");
                output.Events[0].Payload.ShouldBe([0x33]);
                for (int index = 0; index < sources.Length; index++)
                {
                    DomainServiceWireEvent item = output.Events[index + 1];
                    item.EventTypeName.ShouldBe("legacy-exact-alias");
                    item.SerializationFormat.ShouldBe("json");
                    item.Payload.Length.ShouldBe(maximumPayloadBytes);
                    item.Payload.ShouldNotBeSameAs(sources[index]);
                    SHA256.HashData(item.Payload).ShouldBe(originalDigests[index]);
                }
            }
            else
            {
                InvalidOperationException refusal = await Should.ThrowAsync<InvalidOperationException>(
                    async () => { output = await producer.ProduceAsync(input, CancellationToken.None); });
                refusal.Message.ShouldContain("ResultLimit");
                serializerCalls.ShouldBe(0);
            }

            for (int index = 0; index < sources.Length; index++)
            {
                SHA256.HashData(sources[index]).ShouldBe(originalDigests[index]);
            }
        }
        finally
        {
            if (output is not null)
            {
                foreach (DomainServiceWireEvent item in output.Events)
                {
                    CryptographicOperations.ZeroMemory(item.Payload);
                }
            }

            foreach (byte[] source in sources)
            {
                CryptographicOperations.ZeroMemory(source);
            }
        }
    }
}
