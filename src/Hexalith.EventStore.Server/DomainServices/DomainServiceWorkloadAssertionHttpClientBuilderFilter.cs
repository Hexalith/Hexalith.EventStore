using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.Http;

namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>
/// Adds <see cref="DomainServiceWorkloadAssertionHandler"/> as the innermost handler of every factory HTTP client,
/// so no domain-service invocation path can omit or reimplement the workload proof.
/// </summary>
internal sealed class DomainServiceWorkloadAssertionHttpClientBuilderFilter : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return builder =>
        {
            next(builder);
            if (string.Equals(builder.Name, JwtWorkloadAssertionIssuer.HttpClientName, StringComparison.Ordinal))
            {
                return;
            }

            builder.AdditionalHandlers.Add(new DomainServiceWorkloadAssertionHandler(builder.Services));
        };
    }
}
