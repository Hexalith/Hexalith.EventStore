using System.Collections.Concurrent;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Hexalith.EventStore.Admin.Server.Host.Tests;

/// <summary>
/// Records the request-body framing that reached the Admin host, ahead of every other middleware.
/// </summary>
public sealed class RequestBodyFramingRecorder : IStartupFilter {
    /// <summary>
    /// Gets the <c>Content-Length</c> observed for each request, or <see langword="null"/> when none was sent.
    /// </summary>
    public ConcurrentQueue<long?> ContentLengths { get; } = new();

    /// <inheritdoc/>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app => {
            _ = app.Use(async (context, nextMiddleware) => {
                ContentLengths.Enqueue(context.Request.ContentLength);
                await nextMiddleware(context);
            });
            next(app);
        };
}
