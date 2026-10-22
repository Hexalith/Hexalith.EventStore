using Microsoft.Extensions.Http.Resilience;

namespace Hexalith.EventStore.IntegrationTests.Fixtures;

/// <summary>
/// Configures the resilient HTTP defaults used by Aspire contract-test clients.
/// </summary>
internal static class AspireContractHttpResilience {
    /// <summary>
    /// Applies the contract-test time budgets and suppresses retries for unsafe HTTP methods.
    /// </summary>
    /// <param name="options">The standard resilience options.</param>
    internal static void Configure(HttpStandardResilienceOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(180);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
        options.Retry.DisableForUnsafeHttpMethods();
    }
}
