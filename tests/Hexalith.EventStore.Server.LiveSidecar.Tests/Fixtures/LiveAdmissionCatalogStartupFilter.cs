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
            context.Request.EnableBuffering(bufferThreshold: 30 * 1024, bufferLimit: 1024 * 1024);
            await following(context).ConfigureAwait(false);
        });
        next(app);
    };
}
