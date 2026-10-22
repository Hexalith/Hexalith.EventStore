namespace Hexalith.EventStore.Client.Configuration;

/// <summary>
/// Global options for the Event Store client SDK.
/// Configured via <c>AddEventStore(options =&gt; ...)</c> and available through <c>IOptions&lt;EventStoreOptions&gt;</c>.
/// </summary>
/// <remarks>
/// This class uses the standard .NET Options pattern (POCO with parameterless constructor and settable properties)
/// for compatibility with <c>IOptions&lt;T&gt;</c>, <c>IOptionsSnapshot&lt;T&gt;</c>, and <c>IOptionsMonitor&lt;T&gt;</c>.
/// </remarks>
public class EventStoreOptions {

    /// <summary>
    /// Gets or sets the global default suffix for DAPR state store names.
    /// When <c>null</c>, the convention default <c>"eventstore"</c> is used, producing <c>{domain}-eventstore</c>.
    /// Setting this to e.g. <c>"store"</c> produces <c>{domain}-store</c> for all domains (Layer 2 override).
    /// </summary>
    public string? DefaultStateStoreSuffix { get; set; }

    /// <summary>
    /// Gets or sets the global default suffix for pub/sub topic patterns.
    /// When <c>null</c>, the convention default <c>"events"</c> is used, producing <c>{domain}.events</c>.
    /// Setting this overrides the topic suffix for all domains (Layer 2 override).
    /// </summary>
    public string? DefaultTopicSuffix { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether registration diagnostics are enabled.
    /// This minimal flag exists to validate global options configuration flow and can be expanded in later stories.
    /// </summary>
    public bool EnableRegistrationDiagnostics { get; set; }

    /// <summary>
    /// Gets the per-domain configuration callbacks registered via <see cref="ConfigureDomain"/>.
    /// This is internal storage used by the cascade resolver during <c>UseEventStore()</c>.
    /// </summary>
    internal Dictionary<string, Action<EventStoreDomainOptions>> DomainConfigurations { get; } = new Dictionary<string, Action<EventStoreDomainOptions>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers an explicit per-domain configuration callback (Layer 5 — highest priority in the cascade).
    /// </summary>
    /// <param name="domainName">The domain name to configure (must match the discovered domain name).</param>
    /// <param name="configure">A delegate that configures <see cref="EventStoreDomainOptions"/> for the domain.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="domainName"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    public void ConfigureDomain(string domainName, Action<EventStoreDomainOptions> configure) {
        ArgumentException.ThrowIfNullOrWhiteSpace(domainName);
        ArgumentNullException.ThrowIfNull(configure);
        DomainConfigurations[domainName] = configure;
    }
}
