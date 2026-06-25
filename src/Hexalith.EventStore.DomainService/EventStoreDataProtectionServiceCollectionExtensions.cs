using Dapr.Client;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Registration extensions for a shared, persisted Data Protection key ring for domain services.
/// </summary>
/// <remarks>
/// The key ring backs the opaque query pagination cursor codec (<c>IQueryCursorCodec</c>). Persisting it
/// to a shared DAPR state store means a cursor sealed by one replica can be unprotected by another, and the
/// ring survives pod restarts and rolling deploys — closing the multi-replica/rollout cursor-invalidation
/// gap. The selected DAPR state-store component must support ETag / first-write concurrency because key
/// updates are merged through compare-and-swap writes. The backing infrastructure is selected by the DAPR
/// state-store component YAML, so the domain takes no dependency on a concrete infrastructure SDK (see
/// <c>deploy/dapr</c>).
/// </remarks>
public static class EventStoreDataProtectionServiceCollectionExtensions {
    /// <summary>The configuration section bound to <see cref="EventStoreDataProtectionOptions"/>.</summary>
    public const string ConfigurationSectionName = "EventStore:DataProtection";

    /// <summary>
    /// Adds Data Protection for a domain service and, when configured, persists its key ring to a shared
    /// DAPR state store so cursors issued by one replica can be unprotected by another and survive restarts.
    /// </summary>
    /// <remarks>
    /// Behaviour is configuration-driven via the <c>EventStore:DataProtection</c> section
    /// (<see cref="EventStoreDataProtectionOptions"/>):
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <c>PersistToStateStore = false</c> (default, local/dev): uses an explicit ephemeral/per-host key
    /// ring. No DAPR dependency is taken at runtime, so the host still starts without a state store
    /// available. This degrades safely — single-replica dev cursors keep working.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <c>PersistToStateStore = true</c> (production): persists the ring to the DAPR state store named by
    /// <c>StateStoreName</c> (default <c>"statestore"</c>) via a <see cref="DaprClient"/>-backed
    /// <c>IXmlRepository</c>. Requires a registered <see cref="DaprClient"/> (e.g. via <c>AddDaprClient</c>).
    /// </description>
    /// </item>
    /// </list>
    /// The <paramref name="applicationName"/> anchors the key ring to a stable application identity so the
    /// purpose chain is not influenced by <c>IHostEnvironment.ApplicationName</c> drift across host variants.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration providing the <c>EventStore:DataProtection</c> section.</param>
    /// <param name="applicationName">The stable application name that anchors the key ring purpose chain.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventStoreDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        string applicationName) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        services.Configure<EventStoreDataProtectionOptions>(
            configuration.GetSection(ConfigurationSectionName));

        EventStoreDataProtectionOptions options = configuration
            .GetSection(ConfigurationSectionName)
            .Get<EventStoreDataProtectionOptions>() ?? new EventStoreDataProtectionOptions();

        IDataProtectionBuilder dataProtection = services.AddDataProtection().SetApplicationName(applicationName);

        if (!options.PersistToStateStore) {
            _ = dataProtection.UseEphemeralDataProtectionProvider();
            return services;
        }

        // Persist the key ring through DaprClient only. The actual backing store (Redis, a cloud
        // key/value service, …) lives entirely in the DAPR state-store component YAML, so no
        // infrastructure SDK leaks into the domain assembly.
        ArgumentException.ThrowIfNullOrWhiteSpace(options.StateStoreName);
        string stateStoreName = options.StateStoreName.Trim();
        string stateKey = ResolveStateKey(options.StateKey, applicationName);
        TimeSpan operationTimeout = ResolveOperationTimeout(options.OperationTimeout);

        services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
            new ConfigureOptions<KeyManagementOptions>(keyManagement =>
                keyManagement.XmlRepository = new DaprXmlRepository(
                    sp.GetRequiredService<DaprClient>(),
                    stateStoreName,
                    stateKey,
                    operationTimeout)));

        return services;
    }

    private static TimeSpan ResolveOperationTimeout(TimeSpan operationTimeout) {
        if (operationTimeout <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(operationTimeout),
                operationTimeout,
                "The Data Protection state-store operation timeout must be positive.");
        }

        return operationTimeout;
    }

    private static string ResolveStateKey(string configuredStateKey, string applicationName) =>
        string.IsNullOrWhiteSpace(configuredStateKey)
            ? $"dataprotection-keys-{applicationName}"
            : configuredStateKey.Trim();
}
