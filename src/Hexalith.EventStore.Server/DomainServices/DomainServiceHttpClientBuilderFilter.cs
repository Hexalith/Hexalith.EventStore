using Microsoft.Extensions.Http;
using Microsoft.Extensions.Http.Resilience;

namespace Hexalith.EventStore.Server.DomainServices;

internal sealed class DomainServiceHttpClientBuilderFilter : IHttpMessageHandlerBuilderFilter {
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) {
        ArgumentNullException.ThrowIfNull(next);

        return builder => {
            next(builder);
            if (!string.Equals(
                builder.Name,
                DaprDomainServiceInvoker.HttpClientName,
                StringComparison.Ordinal)) {
                return;
            }

            for (int index = builder.AdditionalHandlers.Count - 1; index >= 0; index--) {
                if (builder.AdditionalHandlers[index] is ResilienceHandler) {
                    builder.AdditionalHandlers.RemoveAt(index);
                }
            }
        };
    }
}
