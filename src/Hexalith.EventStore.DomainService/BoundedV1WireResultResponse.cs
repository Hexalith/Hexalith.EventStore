using Hexalith.EventStore.Contracts.Results;

using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.DomainService;

/// <summary>Streams one privately produced V1 result with charged framing, escaping and Base64.</summary>
/// <remarks>Upstream request graphs and transport buffering still require separate readiness admission.</remarks>
internal sealed class BoundedV1WireResultResponse(DomainServiceWireResult result) : IResult
{
    /// <inheritdoc/>
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        await WriteAsync(httpContext.Response.Body, result, httpContext.RequestAborted).ConfigureAwait(false);
    }

    /// <summary>Writes a complete result through a 64 KiB private window without whole-value JSON/Base64 arrays.</summary>
    internal static async Task WriteAsync(Stream target, DomainServiceWireResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(result);
        if (result.Events.Count > 1000 || result.WriterMode is not null || result.RegistryFingerprint is not null)
        {
            throw new InvalidOperationException("CapabilityMismatch: this renderer supports only the selected implicit V1 result.");
        }

        using var output = new BoundedV1WireWindowWriter(target, cancellationToken);
        await output.RawAsync("{\"isRejection\":"u8.ToArray()).ConfigureAwait(false);
        await output.RawAsync(result.IsRejection ? "true"u8.ToArray() : "false"u8.ToArray()).ConfigureAwait(false);
        await output.RawAsync(",\"events\":["u8.ToArray()).ConfigureAwait(false);
        for (int i = 0; i < result.Events.Count; i++)
        {
            DomainServiceWireEvent item = result.Events[i];
            if (item.MetadataVersion is not null || item.EventContractType is not null || item.PayloadVersion is not null)
            {
                throw new InvalidOperationException("CapabilityMismatch: unsolicited V2 metadata cannot enter the V1 renderer.");
            }

            if (i > 0) { await output.RawAsync(","u8.ToArray()).ConfigureAwait(false); }
            await output.RawAsync("{\"eventTypeName\":"u8.ToArray()).ConfigureAwait(false);
            await output.StringAsync(item.EventTypeName).ConfigureAwait(false);
            await output.RawAsync(",\"payload\":\""u8.ToArray()).ConfigureAwait(false);
            await output.Base64Async(item.Payload).ConfigureAwait(false);
            await output.RawAsync("\",\"serializationFormat\":"u8.ToArray()).ConfigureAwait(false);
            await output.StringAsync(item.SerializationFormat).ConfigureAwait(false);
            await output.RawAsync("}"u8.ToArray()).ConfigureAwait(false);
        }

        await output.RawAsync("],\"resultPayload\":"u8.ToArray()).ConfigureAwait(false);
        if (result.ResultPayload is null) { await output.RawAsync("null"u8.ToArray()).ConfigureAwait(false); }
        else { await output.StringAsync(result.ResultPayload).ConfigureAwait(false); }
        await output.RawAsync("}"u8.ToArray()).ConfigureAwait(false);
        await output.FinishAsync().ConfigureAwait(false);
    }

}
