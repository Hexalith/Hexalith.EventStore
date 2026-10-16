using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Projections;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class ProjectionWireJsonSerializerTests
{
    [Fact]
    public async Task ConfiguredConvertersAndTypeMetadata_PreserveTheirBytesAndDeclareUnqualifiedStreamingFallback()
    {
        var converterOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        converterOptions.Converters.Add(new CustomBytesConverter());
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(static info =>
        {
            if (info.Type != typeof(ProjectionEventDto)) return;
            System.Text.Json.Serialization.Metadata.JsonPropertyInfo payload = info.Properties.Single(static property => property.Name == "payload");
            payload.Name = "custom_payload";
            payload.ShouldSerialize = static (_, value) => value is byte[] { Length: > 0 };
        });
        var metadataOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = resolver };
        foreach (JsonSerializerOptions options in new[] { converterOptions, metadataOptions,
            new(JsonSerializerDefaults.Web) { ReferenceHandler = ReferenceHandler.Preserve } })
        {
            var request = new ProjectionRequest("tenant-a", "sample", "aggregate-a", [Event([1, 2, 3, 4])]);
            byte[] expected = JsonSerializer.SerializeToUtf8Bytes(request, options);
            using var output = new MemoryStream();
            using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
            await ProjectionWireJsonSerializer.SerializeAsync(output, request, options, CancellationToken.None).ConfigureAwait(true);
            output.ToArray().AsSpan().SequenceEqual(expected).ShouldBeTrue();
            cost[ProjectionDeliveryCost.WireStreamingFallbacks].ShouldBe(1);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(3071)]
    [InlineData(3072)]
    [InlineData(3073)]
    [InlineData(65536)]
    public async Task SegmentedPayloads_PreserveEveryExistingCarrierByteAndOptionalField(int length)
    {
        byte[] bytes = [.. Enumerable.Range(0, length).Select(static value => (byte)(value % 256))];
        ProjectionEventDto item = Event(bytes) with
        {
            EffectivePayload = bytes, StoredDigest = bytes, MetadataVersion = 1,
            StoredEventContractType = "sample<&>\"", StoredPayloadVersion = 1, StoredSerializationFormat = "json",
            EffectiveEventContractType = "sample", EffectivePayloadVersion = 1, EffectiveSerializationFormat = "json",
            StoredEventTypeName = "Changed", RegistryFingerprint = "registry", IsAdapted = false,
        };
        foreach (JsonSerializerOptions options in new[] { JsonSerializerOptions.Web, new(JsonSerializerDefaults.Web)
            { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower } })
        {
            var request = new ProjectionRequest("tenant-a", "sample", "aggregate-a", [item])
            { EventEvolutionProof = bytes, RebuildSourceHeadSequence = 1, RebuildSourceHeadMessageId = "message-1" };
            var dispatch = new ProjectionDispatchRequest(request, ["route"], "operation", "catalog");
            object[] carriers = [item, new[] { item }, request, dispatch,
                new ProjectionRebuildTransportPartRequest(dispatch with { Request = request with { Events = [], EventEvolutionProof = null } }, 0, bytes),
                Event([]), Event([]) with { Payload = null! }];
            foreach (object carrier in carriers)
            {
                byte[] expected = JsonSerializer.SerializeToUtf8Bytes(carrier, carrier.GetType(), options);
                using var actual = new MemoryStream();
                await ProjectionWireJsonSerializer.SerializeAsync(actual, carrier, options, CancellationToken.None).ConfigureAwait(true);
                actual.ToArray().AsSpan().SequenceEqual(expected).ShouldBeTrue($"unchanged {carrier.GetType().Name}, payload length {length}, indented {options.WriteIndented}");
            }
        }
    }

    [Fact]
    public async Task LargePayload_WritesBoundedSegmentsToTheActualDestinationWithoutEncodedTokenBuffering()
    {
        var item = Event(new byte[48 * 1024 * 1024]);
        byte[] empty = JsonSerializer.SerializeToUtf8Bytes(item with { Payload = [] }, JsonSerializerOptions.Web);
        using var output = new ObservingStream();
        await ProjectionWireJsonSerializer.SerializeAsync(output, item, JsonSerializerOptions.Web, CancellationToken.None).ConfigureAwait(true);
        output.Length.ShouldBe(empty.LongLength + 67_108_864);
        output.MaximumWrite.ShouldBeLessThanOrEqualTo(8_192);
        output.Writes.ShouldBeGreaterThan(16_000);
    }

    [Fact]
    public async Task StreamingCarrier_CallerCancellationStopsPayloadAndChargesActualPartialTransportBytes()
    {
        using var cancel = new CancellationTokenSource();
        using var output = new ObservingStream(() => cancel.Cancel());
        using HttpContent content = ProjectionWireJsonSerializer.CreateContent(
            new ProjectionRequest("tenant-a", "sample", "aggregate-a", [Event(new byte[1024 * 1024])]), JsonSerializerOptions.Web);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        await Should.ThrowAsync<OperationCanceledException>(() => content.CopyToAsync(output, cancel.Token)).ConfigureAwait(true);
        cost[ProjectionDeliveryCost.WireTransportSerializationBytes].ShouldBe(output.Length);
        output.Length.ShouldBeGreaterThan(0);
        output.Length.ShouldBeLessThan(16_384);
        output.MaximumWrite.ShouldBeLessThanOrEqualTo(8_192);
    }

    private static ProjectionEventDto Event(byte[] payload)
        => new("Changed", payload, "json", 1, new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.FromHours(2)),
            "correlation", "message-1", "user", 41);

    private sealed class CustomBytesConverter : JsonConverter<byte[]>
    {
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => Convert.FromHexString(reader.GetString()![7..]);
        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
            => writer.WriteStringValue("custom:" + Convert.ToHexString(value));
    }

    private sealed class ObservingStream(Action? afterWrite = null) : Stream
    {
        private long _length;
        internal int MaximumWrite { get; private set; }
        internal int Writes { get; private set; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _length;
        public override long Position { get => _length; set => throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) => Observe(count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); Observe(buffer.Length); return ValueTask.CompletedTask; }
        private void Observe(int count) { _length += count; MaximumWrite = Math.Max(MaximumWrite, count); Writes++; afterWrite?.Invoke(); }
        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.CompletedTask; }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
