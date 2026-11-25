using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Handlers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Client.Registration;

/// <summary>Checks ownership of Dapr headers on the typed gateway HTTP pipeline.</summary>
internal sealed class GatewayDaprRoutingFilter : IHttpMessageHandlerBuilderFilter
{
    /// <inheritdoc/>
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return builder =>
        {
            next(builder);
            if (builder.Name == nameof(IEventStoreGatewayClient)
                && builder.AdditionalHandlers.LastOrDefault() is not DaprServiceInvocationHandler)
            {
                builder.AdditionalHandlers.Add(new GatewayDaprRoutingGuard(
                    builder.Services.GetService<IConfiguration>(),
                    builder.Services.GetRequiredService<ILogger<GatewayDaprRoutingGuard>>()));
            }
        };
    }
}
