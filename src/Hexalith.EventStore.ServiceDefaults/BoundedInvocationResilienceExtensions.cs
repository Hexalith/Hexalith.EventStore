using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace Hexalith.EventStore.ServiceDefaults;

/// <summary>Configures the bounded transport used by domain queries and Admin API invocations.</summary>
public static class BoundedInvocationResilienceExtensions
{
    /// <summary>
    /// Replaces inherited resilience on an owned named client with a thirty-second attempt and total budget.
    /// Early transient failures retain standard retries; a full attempt consumes the total budget.
    /// </summary>
    /// <param name="builder">The nonempty named client that owns this invocation policy.</param>
    /// <returns>The client builder for subsequent credential and Dapr handlers.</returns>
    public static IHttpClientBuilder AddEventStoreBoundedInvocationResilience(this IHttpClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(builder.Name);

        // The default builder shares its options with every inherited client. Remove only this
        // named client's inherited handler before adding its independently named standard pipeline.
        builder.ConfigureAdditionalHttpMessageHandlers(static (handlers, _) =>
        {
            for (int index = handlers.Count - 1; index >= 0; index--)
            {
                if (handlers[index] is ResilienceHandler) handlers.RemoveAt(index);
            }
        });
        builder.AddStandardResilienceHandler(static options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
        }).SelectPipelineBy(static _ => Extensions.GetResiliencePipelineKey);
        builder.ConfigureAdditionalHttpMessageHandlers(static (handlers, _) =>
        {
            ResilienceHandler resilience = handlers.OfType<ResilienceHandler>().Single();
            handlers.Remove(resilience);
            handlers.Insert(0, resilience);
        });
        return builder;
    }
}
