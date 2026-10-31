using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Retains the real request for catalog scope selection after normal model binding and authorization.</summary>
internal sealed class LiveAdmissionCatalogStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, following) =>
        {
            // Actor partitions and stamped calls select their immutable scope without reading
            // the body. Preserve native actor-remoting limits and large replay envelopes.
            if (!context.Request.Path.StartsWithSegments("/actors", StringComparison.OrdinalIgnoreCase)
                && !context.Request.Headers.ContainsKey(RouteCatalogInvocationHandler.RootHeader))
            {
                // Spill retained JSON to disk while preserving the native server request-size limit.
                context.Request.EnableBuffering(bufferThreshold: 30 * 1024);
            }
            await following(context).ConfigureAwait(false);
        });
        next(app);
    };
}
