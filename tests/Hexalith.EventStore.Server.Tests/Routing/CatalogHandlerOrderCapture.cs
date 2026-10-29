using Microsoft.Extensions.Http;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Captures the executable additional-handler chain after all factory configuration.</summary>
internal sealed class CatalogHandlerOrderCapture : IHttpMessageHandlerBuilderFilter
{
    public IReadOnlyList<Type> Handlers { get; private set; } = [];
    public Type? Transport { get; private set; }
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
    {
        next(builder);
        Handlers = builder.AdditionalHandlers.Select(handler => handler.GetType()).ToArray();
        Transport = builder.PrimaryHandler.GetType();
    };
}
