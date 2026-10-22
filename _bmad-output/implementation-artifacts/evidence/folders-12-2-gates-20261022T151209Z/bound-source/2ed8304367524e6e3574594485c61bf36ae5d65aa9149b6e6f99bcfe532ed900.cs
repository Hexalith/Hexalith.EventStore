namespace Hexalith.EventStore.Admin.Mcp;

/// <summary>
/// In-memory session state for MCP investigation context.
/// Persists across tool calls within a single MCP server process lifetime.
/// </summary>
internal sealed class InvestigationSession {
    private readonly object _lock = new();
    private string? _tenantId;
    private string? _domain;
    private DateTimeOffset? _startedAtUtc;

    internal readonly record struct Snapshot(string? TenantId, string? Domain, DateTimeOffset? StartedAtUtc, bool HasContext);

    /// <summary>Gets the active tenant ID scope, or null if unset.</summary>
    public string? TenantId {
        get {
            lock (_lock) {
                return _tenantId;
            }
        }
    }

    /// <summary>Gets the active domain scope, or null if unset.</summary>
    public string? Domain {
        get {
            lock (_lock) {
                return _domain;
            }
        }
    }

    /// <summary>Gets when the investigation session started, or null if no context set.</summary>
    public DateTimeOffset? StartedAtUtc {
        get {
            lock (_lock) {
                return _startedAtUtc;
            }
        }
    }

    /// <summary>Gets whether any context is currently set.</summary>
    public bool HasContext {
        get {
            lock (_lock) {
                return _tenantId is not null || _domain is not null;
            }
        }
    }

    /// <summary>
    /// Gets a lock-protected, atomic context snapshot.
    /// </summary>
    public Snapshot GetSnapshot() {
        lock (_lock) {
            bool hasContext = _tenantId is not null || _domain is not null;
            return new Snapshot(_tenantId, _domain, _startedAtUtc, hasContext);
        }
    }

    /// <summary>
    /// Sets the investigation context. Non-null values replace current values.
    /// Null values leave existing values unchanged (allowing partial updates).
    /// </summary>
    public void SetContext(string? tenantId, string? domain) {
        lock (_lock) {
            string? normalizedTenantId = NormalizeScope(tenantId);
            string? normalizedDomain = NormalizeScope(domain);

            if (normalizedTenantId is not null) {
                _tenantId = normalizedTenantId;
            }

            if (normalizedDomain is not null) {
                _domain = normalizedDomain;
            }

            _startedAtUtc ??= DateTimeOffset.UtcNow;
        }
    }

    /// <summary>Clears the tenant ID from session context.</summary>
    public void ClearTenantId() {
        lock (_lock) {
            _tenantId = null;
            if (_domain is null) {
                _startedAtUtc = null;
            }
        }
    }

    /// <summary>Clears the domain from session context.</summary>
    public void ClearDomain() {
        lock (_lock) {
            _domain = null;
            if (_tenantId is null) {
                _startedAtUtc = null;
            }
        }
    }

    /// <summary>Clears all session state.</summary>
    public void Clear() {
        lock (_lock) {
            _tenantId = null;
            _domain = null;
            _startedAtUtc = null;
        }
    }

    private static string? NormalizeScope(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
