using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Http;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

/// <summary>Isolates the original outbound credential protocol tests from separately qualified catalog activation.</summary>
internal sealed class WorkloadProtocolCatalogIsolation : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
    {
        next(builder);
        if (builder.PrimaryHandler is RouteCatalogInvocationHandler guard) builder.PrimaryHandler = guard.Transport;
    };
}
