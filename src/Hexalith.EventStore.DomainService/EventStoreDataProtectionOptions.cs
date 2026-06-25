namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Options that select how a domain service persists its Data Protection key ring.
/// </summary>
/// <remarks>
/// Bound from the <c>EventStore:DataProtection</c> configuration section by
/// <see cref="EventStoreDataProtectionServiceCollectionExtensions.AddEventStoreDataProtection"/>.
/// The backing infrastructure is chosen by the DAPR state-store component named here, never by a
/// concrete infrastructure SDK reference in the domain — keeping infrastructure choice in DAPR YAML.
/// </remarks>
public sealed class EventStoreDataProtectionOptions {
    /// <summary>
    /// Gets or sets a value indicating whether the key ring is persisted to the DAPR state store.
    /// </summary>
    /// <remarks>
    /// When <see langword="false"/> (the default for local/dev), the host uses an explicit
    /// ephemeral/per-host key ring. Production deployments set this to <see langword="true"/> so a cursor
    /// sealed by one replica can be unprotected by another and the ring survives restarts.
    /// </remarks>
    public bool PersistToStateStore { get; set; }

    /// <summary>
    /// Gets or sets the DAPR state-store component name that persists the key ring.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>"statestore"</c> — the component every Hexalith domain already binds. The backing
    /// store (Redis, a cloud key/value service, …) is decided entirely by this component's DAPR YAML.
    /// </remarks>
    public string StateStoreName { get; set; } = "statestore";

    /// <summary>
    /// Gets or sets the state key under which the key-ring elements are stored.
    /// </summary>
    /// <remarks>
    /// Leave blank to use an application-specific default derived from the application name passed to
    /// <c>AddEventStoreDataProtection</c>. Production deployments should prefer an explicit
    /// application-specific value when the DAPR state-store component is shared with other app IDs.
    /// </remarks>
    public string StateKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum duration allowed for each synchronous key-ring state-store operation.
    /// </summary>
    /// <remarks>
    /// The ASP.NET Core <c>IXmlRepository</c> contract is synchronous, so DAPR calls are bridged through a
    /// bounded cancellation token to avoid unbounded host startup or cursor-processing stalls.
    /// </remarks>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
