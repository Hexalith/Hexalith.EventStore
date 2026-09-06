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
            feature.MaxRequestBodySize = limit.Value;
        }

        Stream? originalBody = null;
        MemoryStream? bufferedBody = null;
        if (!context.Request.ContentLength.HasValue) {
            bufferedBody = await BufferUnknownLengthBodyAsync(context, limit.Value).ConfigureAwait(false);
            if (bufferedBody is null) {
                return;
            }

            originalBody = context.Request.Body;
            context.Request.Body = bufferedBody;
        }

        try {
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
        int capacity = checked((int)Math.Min(limit, 64L * 1024L));
        var bufferedBody = new MemoryStream(capacity);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
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
                    await WriteTooLargeAsync(context).ConfigureAwait(false);
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
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task WriteTooLargeAsync(HttpContext context) {
        if (context.Response.HasStarted) {
            throw new BadHttpRequestException("The request body is too large.", StatusCodes.Status413PayloadTooLarge);
        }

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        context.Response.ContentType = "application/problem+json";
        string correlationId = context.Items["CorrelationId"]?.ToString() ?? "unknown";
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
