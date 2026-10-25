using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.DomainService;

/// <summary>Explicit local complete-command intake composition.</summary>
public static class CompleteCommandSourceServiceCollectionExtensions
{
    /// <summary>Requires exact authenticated native prefixes for the bounded domain set. This does not install source or workload authority.</summary>
    public static IServiceCollection AddEventStoreCompleteCommandSourceVerification(this IServiceCollection services,
        string platformWorkloadId, IReadOnlyList<string> requiredDomains)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentException.ThrowIfNullOrWhiteSpace(platformWorkloadId);
        ArgumentNullException.ThrowIfNull(requiredDomains);
        if (platformWorkloadId.Length > 200 || requiredDomains.Count is < 1 or > 1000
            || requiredDomains.Any(domain => string.IsNullOrWhiteSpace(domain) || domain.Length > 128)
            || requiredDomains.Distinct(StringComparer.Ordinal).Count() != requiredDomains.Count
            || services.Any(item => item.ServiceType == typeof(CompleteCommandSourceVerificationOptions)))
            throw new ArgumentException("Explicit unique bounded complete-source domains are required.", nameof(requiredDomains));
        services.AddSingleton(new CompleteCommandSourceVerificationOptions(platformWorkloadId, Array.AsReadOnly(requiredDomains.ToArray())));
        services.TryAddSingleton(TimeProvider.System); services.TryAddSingleton<CompleteCommandSourceVerifier>();
        return services;
    }
}
