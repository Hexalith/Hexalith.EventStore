using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Http;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Isolates old state-protocol HTTP witnesses from catalog activation, without changing runtime handlers or application auth.</summary>
internal sealed class LegacyProtocolCatalogHttpIsolation : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
    {
        next(builder);
        if (builder.PrimaryHandler is RouteCatalogInvocationHandler guard) builder.PrimaryHandler = guard.Transport;
    };
}
