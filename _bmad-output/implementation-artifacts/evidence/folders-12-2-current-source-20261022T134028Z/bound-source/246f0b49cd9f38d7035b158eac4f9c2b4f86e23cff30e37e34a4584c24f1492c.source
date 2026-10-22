using System.Buffers;
using System.Reflection;
using System.Text;
using System.Text.Json;

using Dapr.Actors;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>Measures concrete pinned SDK buffers and conservative source-decoder phase overlap.</summary>
internal static class ProjectionSourceDecodeWorkingMemory
{
    internal static (SourceEventRange Decoded, byte[] Body, long Bytes) ActorResponse(SourceEventRange range, object? prefix)
    {
        Assembly sdk = typeof(ActorId).Assembly;
        Type providerType = sdk.GetType("Dapr.Actors.Communication.ActorMessageBodyDataContractSerializationProvider", throwOnError: true)!;
        object provider = Activator.CreateInstance(providerType, nonPublic: true)!;
        object factory = providerType.GetMethod("CreateMessageBodyFactory")!.Invoke(provider, null)!;
        object wrapped = factory.GetType().GetMethod("CreateResponseMessageBody")!
            .Invoke(factory, [nameof(IAggregateActor), nameof(IAggregateActor.ReadMeasuredEventsBoundedRangeAsync), range])!;
        object serializer = providerType.GetMethod("CreateResponseMessageBodySerializer")!
            .Invoke(provider, [typeof(IAggregateActor), new[] { typeof(SourceEventRange) }, new[] { typeof(SourceEventRange) }])!;
        Type contract = sdk.GetType("Dapr.Actors.Communication.IActorResponseMessageBodySerializer", throwOnError: true)!;
        byte[] body = (byte[])contract.GetMethod("Serialize")!.Invoke(serializer, [wrapped])!;
        byte[]? copy = null;
        using var httpBody = new ObservedActorResponseMemoryStream(body, actual =>
        {
            copy = actual.GetBuffer();
            // The default HttpContent MemoryStream writes once into the empty
            // SDK stream, whose concrete capacity is exactly this body length.
            copy.Length.ShouldBe(Math.Max(256, body.Length));
        });
        object valueTask = contract.GetMethod("DeserializeAsync")!.Invoke(serializer, [httpBody])!;
        valueTask.GetType().GetProperty("IsCompleted")!.GetValue(valueTask).ShouldBe(true);
        Task completion = (Task)valueTask.GetType().GetMethod("AsTask")!.Invoke(valueTask, null)!;
        completion.GetAwaiter().GetResult();
        object decodedBody = completion.GetType().GetProperty("Result")!.GetValue(completion)!;
        SourceEventRange decoded = (SourceEventRange)decodedBody.GetType().GetProperty("Value")!.GetValue(decodedBody)!;
        decoded.Events.Length.ShouldBe(range.Events.Length);
        decoded.EnvelopeArrayBytes.ShouldBe(range.EnvelopeArrayBytes);
        // Keep the complete constructed range conservatively beside the two
        // actual binary arrays, although their synchronous decoder frame ends
        // before provider processing. The owning actor page is a separate copy.
        long bytes = ProjectionRetainedGraphSize.Measure(prefix, range, decoded, body, copy);
        return (decoded, body, bytes);
    }

    internal static (int JsonCharacters, int PoolBytes, long Bytes) StateJson(SourceEventRange page, object? prefix)
    {
        EventEnvelope largest = page.Events.MaxBy(static item => item.Payload.Length)!;
        string json = JsonSerializer.Serialize(largest, JsonSerializerOptions.Web);
        // Pinned JsonSerializer.Deserialize(string) requests json.Length*3.
        // Reproduce that concrete pool capacity and decode the same transcoded
        // bytes, retaining its entire constructed result as a safe phase bound.
        byte[] transcode = ArrayPool<byte>.Shared.Rent(checked(json.Length * 3));
        try
        {
            int written = Encoding.UTF8.GetBytes(json, transcode);
            EventEnvelope decoded = JsonSerializer.Deserialize<EventEnvelope>(transcode.AsSpan(0, written), JsonSerializerOptions.Web)!;
            decoded.Payload.ShouldBe(largest.Payload);
            return (json.Length, transcode.Length, ProjectionRetainedGraphSize.Measure(prefix, page, json, transcode, decoded));
        }
        finally { ArrayPool<byte>.Shared.Return(transcode); }
    }
}
