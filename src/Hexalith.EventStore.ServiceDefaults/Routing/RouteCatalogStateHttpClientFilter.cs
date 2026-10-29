using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Http.Resilience;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Authority mutations have one CAS attempt; generic HTTP retry cannot reinterpret an uncertain write as absence.</summary>
internal sealed class RouteCatalogStateHttpClientFilter : IHttpMessageHandlerBuilderFilter
{
    /// <inheritdoc/>
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return builder =>
        {
            next(builder);
            // Validate at the transport boundary. The SDK header owner remains the innermost
            // delegating handler and this transport sees only its final owned target/credential.
            builder.PrimaryHandler = new RouteCatalogInvocationHandler(builder.Services.GetRequiredService<IRouteCatalogAuthority>(), builder.PrimaryHandler,
                builder.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RouteCatalogHostOptions>>(), builder.Name == "route-catalog-state");
            if (builder.Name != "route-catalog-state") return;
            for (int index = builder.AdditionalHandlers.Count - 1; index >= 0; index--)
                if (builder.AdditionalHandlers[index] is ResilienceHandler) builder.AdditionalHandlers.RemoveAt(index);
        };
    }
}
