
namespace Hexalith.EventStore.Server.Configuration;

/// <summary>
/// Configuration options for the server-managed projection builder.
/// Bound to the "EventStore:Projections" configuration section.
/// </summary>
public record ProjectionOptions {
    /// <summary>
    /// Gets the DAPR state store component name for projection delivery checkpoints.
    /// </summary>
    public string CheckpointStateStoreName { get; init; } = "statestore";

    /// <summary>
    /// Gets the DAPR state store component name for canonical, aggregate-owned projection read-model values
    /// minted by <c>ProjectionReadModelAddressFactory</c>. Resolved by the factory instead of being supplied
    /// by a caller, so a REST caller can never target an arbitrary store.
    /// </summary>
    public string ReadModelStateStoreName { get; init; } = "statestore";

    /// <summary>
    /// Gets the default refresh interval in milliseconds.
    /// 0 = immediate (fire-and-forget after persistence).
    /// Values &gt; 0 enable background polling at that interval (Story 11-4).
    /// </summary>
    public int DefaultRefreshIntervalMs { get; init; }

    /// <summary>
    /// Gets the per-domain refresh interval overrides. Key = domain name (kebab-case).
    /// </summary>
    public Dictionary<string, DomainProjectionOptions> Domains { get; init; } = [];

    /// <summary>
    /// P-DEC1-8P (pass-8): cadence in seconds for the active-rebuild-index cleanup service.
    /// Default 60s. Set to 0 to disable the cleanup service. The cleanup service polls known
    /// (tenant, domain) pairs and clears active-index entries whose operator-scope checkpoint is
    /// terminal or missing — recovering from partial best-effort active-index writes left behind
    /// by the P13-6P terminal-write recovery path.
    /// </summary>
    public int RebuildIndexCleanupCadenceSeconds { get; init; } = 60;

    /// <summary>
    /// Gets the effective refresh interval for a given domain.
    /// </summary>
    /// <param name="domain">The domain name.</param>
    /// <returns>The refresh interval in milliseconds for the specified domain.</returns>
    public int GetRefreshIntervalMs(string domain) {
        if (Domains.TryGetValue(domain, out DomainProjectionOptions? domainOptions)) {
            return domainOptions.RefreshIntervalMs;
        }

        foreach ((string key, DomainProjectionOptions value) in Domains) {
            if (string.Equals(key, domain, StringComparison.OrdinalIgnoreCase)) {
                return value.RefreshIntervalMs;
            }
        }

        return DefaultRefreshIntervalMs;
    }

    /// <summary>
    /// Validates projection options and throws if configuration is invalid.
    /// </summary>
    public void Validate() {
        if (string.IsNullOrWhiteSpace(CheckpointStateStoreName)) {
            throw new InvalidOperationException("Projection checkpoint state store name must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(ReadModelStateStoreName)) {
            throw new InvalidOperationException("Projection read-model state store name must not be empty.");
        }

        if (DefaultRefreshIntervalMs < 0) {
            throw new InvalidOperationException("Projection default refresh interval must be >= 0.");
        }

        if (RebuildIndexCleanupCadenceSeconds < 0) {
            throw new InvalidOperationException("Projection rebuild index cleanup cadence must be >= 0 seconds (0 disables the cleanup service).");
        }

        HashSet<string> seenKeys = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, DomainProjectionOptions value) in Domains) {
            if (string.IsNullOrWhiteSpace(key)) {
                throw new InvalidOperationException("Projection domain key must not be empty.");
            }

            if (!seenKeys.Add(key)) {
                throw new InvalidOperationException($"Projection domain keys must be unique ignoring case; duplicate detected for '{key}'.");
            }

            if (value.RefreshIntervalMs < 0) {
                throw new InvalidOperationException($"Projection refresh interval for domain '{key}' must be >= 0.");
            }
        }
    }
}

/// <summary>
/// Per-domain projection configuration options.
/// </summary>
public record DomainProjectionOptions {
    /// <summary>
    /// Gets the refresh interval in milliseconds for this domain.
    /// </summary>
    public int RefreshIntervalMs { get; init; }
}
