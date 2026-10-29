using Hexalith.EventStore.Contracts.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Hexalith.EventStore.Sample.Tests;

/// <summary>Isolates human HTTP authentication from the independently tested generated-action catalog gate.</summary>
internal sealed class SampleApiCatalogTestStartupFilter : IStartupFilter
{
    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, continuation) =>
        {
            context.Items[RouteCatalogGeneratedAction.ValidatorKey] = (Func<Type, string, string, bool>)
                ((_, _, tenant) => tenant == "tenant-a");
            await continuation(context).ConfigureAwait(false);
        });
        next(app);
    };
}
