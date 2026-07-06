namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Resolves convention-named diagnostics by domain name.
/// </summary>
public sealed class EventStoreDomainDiagnosticsRegistry : IDisposable {
    private readonly IReadOnlyDictionary<string, EventStoreDomainDiagnostics> _diagnostics;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStoreDomainDiagnosticsRegistry"/> class.
    /// </summary>
    /// <param name="registrations">The registered domain telemetry declarations.</param>
    internal EventStoreDomainDiagnosticsRegistry(IEnumerable<EventStoreDomainTelemetryRegistration> registrations) {
        ArgumentNullException.ThrowIfNull(registrations);

        _diagnostics = registrations
            .GroupBy(static registration => registration.Domain, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => new EventStoreDomainDiagnostics(group.Key),
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the registered domain names.
    /// </summary>
    public IReadOnlyCollection<string> Domains => _diagnostics.Keys.ToArray();

    /// <summary>
    /// Attempts to resolve diagnostics for the specified domain.
    /// </summary>
    /// <param name="domain">The domain name.</param>
    /// <param name="diagnostics">The resolved diagnostics, when registered.</param>
    /// <returns><see langword="true"/> when diagnostics were found; otherwise <see langword="false"/>.</returns>
    public bool TryGetDiagnostics(string domain, out EventStoreDomainDiagnostics? diagnostics) {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return _diagnostics.TryGetValue(domain.Trim(), out diagnostics);
    }

    /// <summary>
    /// Resolves diagnostics for the specified domain.
    /// </summary>
    /// <param name="domain">The domain name.</param>
    /// <returns>The resolved diagnostics, or <see langword="null"/> when the domain is unknown.</returns>
    public EventStoreDomainDiagnostics? GetDiagnostics(string domain)
        => TryGetDiagnostics(domain, out EventStoreDomainDiagnostics? diagnostics) ? diagnostics : null;

    /// <inheritdoc/>
    public void Dispose() {
        foreach (EventStoreDomainDiagnostics diagnostics in _diagnostics.Values) {
            diagnostics.Dispose();
        }
    }
}
