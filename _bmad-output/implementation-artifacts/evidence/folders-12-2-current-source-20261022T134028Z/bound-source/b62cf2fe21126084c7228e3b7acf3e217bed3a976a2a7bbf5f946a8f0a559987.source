using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Projections;

internal sealed partial class GuardedTailProductionHarness
{
    internal Action<string, object?[]>? EventWorkingObserver { get; set; }
    internal long ProviderNativeBytes { get; private set; }
    private SourceEventRange? _eventWorkingRange;

    private void ObserveEventRange(SourceEventRange range)
    {
        _eventWorkingRange = range;
        EventWorkingObserver?.Invoke("source_page", [range]);
    }

    /// <summary>Uses an actual fixture AES/Brotli provider to separate stored-envelope and readable-wire budgets.</summary>
    internal void SetProtectedTailPayloadSizes(int completed, params int[] sizes)
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);
        var metadata = new EventStorePayloadProtectionMetadata(PayloadProtectionState.Protected, 1,
            "qualification-aes-brotli", null, "application/json", null);
        for (int index = 0; index < sizes.Length; index++)
        {
            byte[] plaintext = new byte[sizes[index]];
            "{}"u8.CopyTo(plaintext);
            plaintext.AsSpan(2).Fill((byte)' ');
            using var compressed = new MemoryStream();
            using (var brotli = new BrotliStream(compressed, CompressionLevel.Fastest, leaveOpen: true)) brotli.Write(plaintext);
            byte[] content = compressed.ToArray();
            byte[] carrier = new byte[28 + content.Length];
            RandomNumberGenerator.Fill(carrier.AsSpan(0, 12));
            using (var aes = new AesGcm(key, 16)) aes.Encrypt(carrier.AsSpan(0, 12), content, carrier.AsSpan(28), carrier.AsSpan(12, 16));
            EventEnvelope original = Events[completed + index];
            Events[completed + index] = original with { Payload = carrier, SerializationFormat = "fixture+protected+json",
                Extensions = EventStorePayloadProtectionMetadataCarrier.Write(original.Extensions, metadata),
                ApplicationPayloadDigest = EventLogicalDigest.Compute(original, "json", EventLogicalDigest.HashPayload(plaintext)) };
        }
        Protection.TryUnprotectEventPayloadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(),
            "fixture+protected+json", Arg.Any<EventStorePayloadProtectionMetadata?>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            call.ArgAt<CancellationToken>(5).ThrowIfCancellationRequested();
            byte[] carrier = call.ArgAt<byte[]>(2);
            byte[] compressed = new byte[carrier.Length - 28];
            using (var aes = new AesGcm(key, 16)) aes.Decrypt(carrier.AsSpan(0, 12), carrier.AsSpan(28), carrier.AsSpan(12, 16), compressed);
            using var input = new MemoryStream(compressed);
            using var brotli = new BrotliStream(input, CompressionMode.Decompress);
            using ObservedBrotliDecoderAllocation? native = EventWorkingObserver is null ? null : new(brotli);
            using var plaintext = new MemoryStream();
            try { brotli.CopyTo(plaintext); }
            catch { brotli.Dispose(); throw; }
            ProtectedReads++;
            byte[] returned = plaintext.ToArray();
            ProviderNativeBytes = native?.PeakBytes ?? 0;
            try { EventWorkingObserver?.Invoke("provider_return", [_eventWorkingRange, carrier, compressed, input, brotli, plaintext, returned]); }
            finally { brotli.Dispose(); }
            return PayloadUnprotectionOutcome.Readable(returned, "json", metadata);
        });
        RehashProtectedSource();
    }

    internal void SetExactProtectedTailWireBytes(long expectedBytes)
    {
        EventEnvelope[] tail = Events.Skip(1).ToArray();
        ProjectionEventDto[] empty = [.. tail.Select(item => new ProjectionEventDto(item.EventTypeName, [], "json", item.SequenceNumber,
            item.Timestamp, item.CorrelationId, item.MessageId, item.UserId, item.GlobalPosition) { MetadataVersion = item.MetadataVersion })];
        using var size = new SizeLimitedWriteStream(long.MaxValue);
        JsonSerializer.Serialize(size, empty, JsonSerializerOptions.Web);
        long remaining = expectedBytes - size.Length;
        int[] sizes = new int[tail.Length];
        for (int index = 0; index < sizes.Length - 1; index++)
        {
            sizes[index] = 1_048_576;
            remaining -= 4L * ((sizes[index] + 2) / 3);
        }
        sizes[^1] = checked((int)(remaining / 4 * 3));
        int headerPadding = checked((int)(remaining % 4));
        Events[^1] = Events[^1] with { UserId = Events[^1].UserId + new string('x', headerPadding) };
        SetProtectedTailPayloadSizes(1, sizes);
    }

    private void RehashProtectedSource()
    {
        Head = SourceProofCodec.CreateSeed(Identity, Head.SourceIncarnation);
        foreach (EventEnvelope item in Events) Head = SourceProofCodec.Extend(Head, item);
    }
}
