using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Writes unchanged projection JSON while flushing base64 in bounded segments directly to its destination.</summary>
internal static class ProjectionWireJsonSerializer
{
    private const int SegmentBytes = 3_072;

    /// <summary>Creates streaming request content without a complete serialized event body or encoded payload token.</summary>
    internal static HttpContent CreateContent(object body, JsonSerializerOptions options) => new StreamingContent(body, options);

    /// <summary>Streams one event, complete event array or existing request carrier with its original JSON shape.</summary>
    internal static async Task SerializeAsync(Stream output, object value, JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        if (!SupportsBoundedSerialization(value, options))
        {
            // Custom converters/metadata retain their configured behavior. This
            // path is measured, but cannot claim bounded-token qualification.
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.WireStreamingFallbacks);
            await JsonSerializer.SerializeAsync(output, value, value.GetType(), options, cancellationToken).ConfigureAwait(false);
            return;
        }
        using var writer = new Utf8JsonWriter(output, new JsonWriterOptions
        { Encoder = options.Encoder, Indented = options.WriteIndented, MaxDepth = options.MaxDepth });
        try
        {
            await WriteValueAsync(writer, value, options, cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Disposing a failed writer must not retry its rejected/canceled
            // buffer against the destination as a second write attempt.
            writer.Reset(Stream.Null);
            throw;
        }
    }

    private static bool SupportsBoundedSerialization(object value, JsonSerializerOptions options)
    {
        if (options.Converters.Count != 0 || options.ReferenceHandler is not null
            || options.TypeInfoResolver is not null and not DefaultJsonTypeInfoResolver
            || options.TypeInfoResolver is DefaultJsonTypeInfoResolver { Modifiers.Count: > 0 }) return false;
        return value switch
        {
            ProjectionEventDto item => item.GetType() == typeof(ProjectionEventDto),
            ProjectionEventDto[] events => events.All(static item => item.GetType() == typeof(ProjectionEventDto)),
            ProjectionRequest request => request.GetType() == typeof(ProjectionRequest)
                && request.Events.All(static item => item.GetType() == typeof(ProjectionEventDto)),
            ProjectionDispatchRequest dispatch => SupportsBoundedSerialization(dispatch.Request, options),
            ProjectionRebuildTransportPartRequest part => SupportsBoundedSerialization(part.Header.Request, options),
            _ => true,
        };
    }

    private static Task WriteValueAsync(Utf8JsonWriter writer, object value, JsonSerializerOptions options, CancellationToken cancellationToken)
        => value switch
        {
            ProjectionEventDto item => WriteEventAsync(writer, item, options, cancellationToken),
            ProjectionEventDto[] events => WriteEventsAsync(writer, events, options, cancellationToken),
            ProjectionRequest request => WriteRequestAsync(writer, request, options, cancellationToken),
            ProjectionDispatchRequest dispatch => WriteDispatchAsync(writer, dispatch, options, cancellationToken),
            ProjectionRebuildTransportPartRequest part => WritePartAsync(writer, part, options, cancellationToken),
            _ => WriteSmallValueAsync(writer, value, options, cancellationToken),
        };

    private static async Task WriteSmallValueAsync(Utf8JsonWriter writer, object value, JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteEventAsync(Utf8JsonWriter writer, ProjectionEventDto item, JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        // Let the configured serializer retain ordering, escaping, naming and
        // optional-field behavior. Payload placeholders are always small.
        using JsonDocument skeleton = JsonSerializer.SerializeToDocument(item with
        { Payload = Placeholder(item.Payload)!, EffectivePayload = Placeholder(item.EffectivePayload), StoredDigest = Placeholder(item.StoredDigest) }, options);
        writer.WriteStartObject();
        foreach (JsonProperty property in skeleton.RootElement.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);
            if (property.NameEquals(Name(nameof(item.Payload), options)))
                await WriteBytesAsync(writer, item.Payload, cancellationToken).ConfigureAwait(false);
            else if (property.NameEquals(Name(nameof(item.EffectivePayload), options)))
                await WriteBytesAsync(writer, item.EffectivePayload, cancellationToken).ConfigureAwait(false);
            else if (property.NameEquals(Name(nameof(item.StoredDigest), options)))
                await WriteBytesAsync(writer, item.StoredDigest, cancellationToken).ConfigureAwait(false);
            else WriteUnchangedValue(writer, property.Value);
        }
        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteEventsAsync(Utf8JsonWriter writer, ProjectionEventDto[] events, JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        writer.WriteStartArray();
        foreach (ProjectionEventDto item in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WriteEventAsync(writer, item, options, cancellationToken).ConfigureAwait(false);
        }
        writer.WriteEndArray();
    }

    private static async Task WriteRequestAsync(Utf8JsonWriter writer, ProjectionRequest request, JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        using JsonDocument skeleton = JsonSerializer.SerializeToDocument(request with { Events = [], EventEvolutionProof = Placeholder(request.EventEvolutionProof) }, options);
        writer.WriteStartObject();
        foreach (JsonProperty property in skeleton.RootElement.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);
            if (property.NameEquals(Name(nameof(request.Events), options)))
                await WriteEventsAsync(writer, request.Events, options, cancellationToken).ConfigureAwait(false);
            else if (property.NameEquals(Name(nameof(request.EventEvolutionProof), options)))
                await WriteBytesAsync(writer, request.EventEvolutionProof, cancellationToken).ConfigureAwait(false);
            else WriteUnchangedValue(writer, property.Value);
        }
        writer.WriteEndObject();
    }

    private static async Task WriteDispatchAsync(Utf8JsonWriter writer, ProjectionDispatchRequest request, JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        using JsonDocument skeleton = JsonSerializer.SerializeToDocument(request with
        { Request = request.Request with { Events = [], EventEvolutionProof = Placeholder(request.Request.EventEvolutionProof) } }, options);
        writer.WriteStartObject();
        foreach (JsonProperty property in skeleton.RootElement.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);
            if (property.NameEquals(Name(nameof(request.Request), options)))
                await WriteRequestAsync(writer, request.Request, options, cancellationToken).ConfigureAwait(false);
            else WriteUnchangedValue(writer, property.Value);
        }
        writer.WriteEndObject();
    }

    private static async Task WritePartAsync(Utf8JsonWriter writer, ProjectionRebuildTransportPartRequest part, JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        using JsonDocument skeleton = JsonSerializer.SerializeToDocument(part with { Data = [] }, options);
        writer.WriteStartObject();
        foreach (JsonProperty property in skeleton.RootElement.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);
            if (property.NameEquals(Name(nameof(part.Data), options)))
                await WriteBytesAsync(writer, part.Data, cancellationToken).ConfigureAwait(false);
            else WriteUnchangedValue(writer, property.Value);
        }
        writer.WriteEndObject();
    }

    private static async Task WriteBytesAsync(Utf8JsonWriter writer, byte[]? bytes, CancellationToken cancellationToken)
    {
        if (bytes is null) { writer.WriteNullValue(); return; }
        int offset = 0;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = Math.Min(SegmentBytes, bytes.Length - offset);
            writer.WriteBase64StringSegment(bytes.AsSpan(offset, count), offset + count == bytes.Length);
            offset += count;
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        } while (offset < bytes.Length);
    }

    private static void WriteUnchangedValue(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) value.WriteTo(writer);
        else writer.WriteRawValue(value.GetRawText(), skipInputValidation: true);
    }

    private static byte[]? Placeholder(byte[]? value) => value is null ? null : [];
    private static string Name(string property, JsonSerializerOptions options) => options.PropertyNamingPolicy?.ConvertName(property) ?? property;

    private sealed class StreamingContent : HttpContent
    {
        private readonly object _body;
        private readonly JsonSerializerOptions _options;

        internal StreamingContent(object body, JsonSerializerOptions options)
        {
            _body = body;
            _options = options;
            Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        }

        protected override bool TryComputeLength(out long length) { length = 0; return false; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            using var measured = new MeasuredStream(stream);
            try { await SerializeAsync(measured, _body, _options, cancellationToken).ConfigureAwait(false); }
            finally { ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.WireTransportSerializationBytes, measured.Bytes); }
        }

    }

    private sealed class MeasuredStream(Stream destination) : Stream
    {
        internal long Bytes { get; private set; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => Bytes;
        public override long Position { get => Bytes; set => throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { Bytes += count; destination.Write(buffer, offset, count); }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        { Bytes += buffer.Length; await destination.WriteAsync(buffer, cancellationToken).ConfigureAwait(false); }
        public override void Flush() => destination.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => destination.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
