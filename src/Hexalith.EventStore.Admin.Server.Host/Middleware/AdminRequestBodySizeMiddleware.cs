using System.Buffers;
using System.Text.Json;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.Admin.Server.Host.Middleware;

/// <summary>
/// Enforces endpoint-declared Admin request limits and emits a bounded overflow response.
/// </summary>
public sealed class AdminRequestBodySizeMiddleware(RequestDelegate next) {
    private const int ReadBufferSize = 64 * 1024;

    // Matches the server's own drain timeout, which does not cover a drain inside the application.
    private static readonly TimeSpan OverflowDrainTimeout = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Applies the endpoint request-body limit before controller work begins.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context) {
        ArgumentNullException.ThrowIfNull(context);

        long? limit = context.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize;
        if (!limit.HasValue) {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (context.Request.ContentLength is long contentLength && contentLength > limit.Value) {
            await WriteTooLargeAsync(context).ConfigureAwait(false);
            return;
        }

        IHttpMaxRequestBodySizeFeature? feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false }) {
            // Kestrel counts chunk framing against this cap. Unknown-length bodies are
            // bounded below by their decoded entity bytes instead.
            feature.MaxRequestBodySize = context.Request.ContentLength.HasValue ? limit.Value : null;
        }

        Stream? originalBody = null;
        MemoryStream? bufferedBody = null;
        try {
            if (!context.Request.ContentLength.HasValue) {
                bufferedBody = await BufferUnknownLengthBodyAsync(context, limit.Value).ConfigureAwait(false);
                if (bufferedBody is null) {
                    return;
                }

                originalBody = context.Request.Body;
                context.Request.Body = bufferedBody;
            }

            await next(context).ConfigureAwait(false);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge) {
            await WriteTooLargeAsync(context).ConfigureAwait(false);
        }
        finally {
            if (bufferedBody is not null) {
                context.Request.Body = originalBody!;
                await bufferedBody.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task<MemoryStream?> BufferUnknownLengthBodyAsync(HttpContext context, long limit) {
        int capacity = checked((int)Math.Min(limit, ReadBufferSize));
        var bufferedBody = new MemoryStream(capacity);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(ReadBufferSize);
        try {
            long total = 0;
            while (true) {
                int read = await context.Request.Body
                    .ReadAsync(buffer.AsMemory(0, buffer.Length), context.RequestAborted)
                    .ConfigureAwait(false);
                if (read == 0) {
                    break;
                }

                total += read;
                if (total > limit) {
                    bufferedBody.Dispose();
                    await RejectUnknownLengthOverflowAsync(context, buffer, limit).ConfigureAwait(false);
                    return null;
                }

                await bufferedBody.WriteAsync(buffer.AsMemory(0, read), context.RequestAborted).ConfigureAwait(false);
            }

            bufferedBody.Position = 0;
            return bufferedBody;
        }
        catch {
            bufferedBody.Dispose();
            throw;
        }
        finally {
            // The buffer held request-body bytes; never hand them to the next renter.
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static async Task RejectUnknownLengthOverflowAsync(HttpContext context, byte[] buffer, long drainAllowance) {
        await WriteTooLargeAsync(context, closeConnection: true).ConfigureAwait(false);
        await context.Response.CompleteAsync().ConfigureAwait(false);

        // The server drains an unread body after the response, and its own cap stays off for
        // unknown lengths. Discard at most one more limit of entity bytes, for at most the drain
        // timeout, so a client that overshot can read the 413; then abort rather than drain an
        // unbounded remainder.
        using var drainTimeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        drainTimeout.CancelAfter(OverflowDrainTimeout);
        long discarded = 0;
        try {
            while (discarded <= drainAllowance) {
                int read = await context.Request.Body
                    .ReadAsync(buffer.AsMemory(0, buffer.Length), drainTimeout.Token)
                    .ConfigureAwait(false);
                if (read == 0) {
                    return;
                }

                discarded += read;
            }
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException or BadHttpRequestException) {
            // The 413 is already complete; a failed drain only ends the connection.
        }

        context.Abort();
    }

    private static async Task WriteTooLargeAsync(HttpContext context, bool closeConnection = false) {
        if (context.Response.HasStarted) {
            throw new BadHttpRequestException("The request body is too large.", StatusCodes.Status413PayloadTooLarge);
        }

        string correlationId = context.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? "unknown";
        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
        if (closeConnection
            && (HttpProtocol.IsHttp10(context.Request.Protocol) || HttpProtocol.IsHttp11(context.Request.Protocol))) {
            context.Response.Headers.Connection = "close";
        }

        var problem = new ProblemDetails {
            Status = StatusCodes.Status413PayloadTooLarge,
            Title = "Payload Too Large",
            Detail = "The request body exceeds the allowed size.",
            Extensions = { ["correlationId"] = correlationId },
        };

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            problem,
            JsonOptions,
            context.RequestAborted).ConfigureAwait(false);
    }
}
