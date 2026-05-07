using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// Distinguishes how a tenant entered the option set so the UI can preserve provenance
/// even when it doesn't show a label. Registered tenants come from the Tenants service;
/// observed-only tenants are inferred from event/stream data the current user is authorized
/// to see (e.g. the seeded sample writes events under tenant-a but tenant-a may not be
/// registered in the Tenants service).
/// </summary>
public enum TenantProvenance {
    Registered = 0,
    ObservedOnly = 1,
}

/// <summary>
/// A single tenant entry rendered in filter dropdowns.
/// </summary>
public sealed record TenantOption(
    string TenantId,
    string DisplayName,
    TenantProvenance Provenance);

/// <summary>
/// Outcome class for a tenant option lookup. The seven-state UI vocabulary (loading, empty,
/// zero, unavailable, unauthorized, stale, error) maps onto a subset of these statuses;
/// loading is owned by the calling component, zero is not applicable to discovery, and
/// stale is reserved for future cache integration.
/// </summary>
public enum TenantOptionsLoadStatus {
    Loaded = 0,
    Empty = 1,
    Unauthorized = 2,
    Forbidden = 3,
    Unavailable = 4,
    Partial = 5,
}

public sealed record TenantOptionsResult(
    IReadOnlyList<TenantOption> Options,
    TenantOptionsLoadStatus Status,
    string? Diagnostic) {
    public bool HasOptions => Options.Count > 0;
}

/// <summary>
/// Shared tenant option provider for filter dropdowns on /commands, /events, /streams, /projections.
/// Unions authorized registered tenants (Tenants service) with authorized observed-only tenants
/// (extracted from stream summaries the current user is authorized to read). Authorization is
/// enforced at the API boundary; this provider does not reduce a broader list client-side.
/// </summary>
public class AdminTenantOptionsProvider {
    public const string EmptyMessage =
        "No tenants found yet. Send a command, register a tenant, or check your tenant permissions.";

    private const string UnauthorizedDiagnostic = "Authentication required. Please sign in again.";
    private const string ForbiddenDiagnostic = "Access denied. Insufficient permissions to view tenants.";
    private const string UnavailableDiagnostic = "Tenant directory is temporarily unavailable.";
    private const string PartialDiagnostic = "Tenant directory is partially loaded; some sources are temporarily unavailable.";

    private readonly AdminTenantApiClient _tenants;
    private readonly AdminStreamApiClient _streams;
    private readonly ILogger<AdminTenantOptionsProvider> _logger;

    public AdminTenantOptionsProvider(
        AdminTenantApiClient tenants,
        AdminStreamApiClient streams,
        ILogger<AdminTenantOptionsProvider> logger) {
        ArgumentNullException.ThrowIfNull(tenants);
        ArgumentNullException.ThrowIfNull(streams);
        ArgumentNullException.ThrowIfNull(logger);
        _tenants = tenants;
        _streams = streams;
        _logger = logger;
    }

    /// <summary>
    /// Loads the union of authorized registered and authorized observed tenants. Sources are
    /// queried in parallel; partial-success returns the surviving source with a Partial status
    /// and a diagnostic that components can render as a non-blocking note.
    /// </summary>
    public virtual async Task<TenantOptionsResult> GetTenantOptionsAsync(CancellationToken ct = default) {
        Task<RegisteredOutcome> registeredTask = LoadRegisteredAsync(ct);
        Task<ObservedOutcome> observedTask = LoadObservedAsync(ct);

        try {
            await Task.WhenAll(registeredTask, observedTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch {
            // Per-source exceptions are captured into outcome enums below; only OCE propagates.
        }

        RegisteredOutcome registered = await registeredTask.ConfigureAwait(false);
        ObservedOutcome observed = await observedTask.ConfigureAwait(false);

        // Hard authentication failure on either source short-circuits to Unauthorized so the
        // dropdown does not silently look "empty" when the user actually needs to sign in.
        if (registered.Status == SourceStatus.Unauthorized || observed.Status == SourceStatus.Unauthorized) {
            return new TenantOptionsResult([], TenantOptionsLoadStatus.Unauthorized, UnauthorizedDiagnostic);
        }

        if (registered.Status == SourceStatus.Forbidden && observed.Status == SourceStatus.Forbidden) {
            return new TenantOptionsResult([], TenantOptionsLoadStatus.Forbidden, ForbiddenDiagnostic);
        }

        if (registered.Status == SourceStatus.Unavailable && observed.Status == SourceStatus.Unavailable) {
            return new TenantOptionsResult([], TenantOptionsLoadStatus.Unavailable, UnavailableDiagnostic);
        }

        // Build option set; registered first so its DisplayName wins on a TenantId collision.
        var byId = new Dictionary<string, TenantOption>(StringComparer.OrdinalIgnoreCase);

        if (registered.Status == SourceStatus.Loaded) {
            foreach (TenantSummary t in registered.Tenants) {
                if (string.IsNullOrWhiteSpace(t.TenantId)) {
                    continue;
                }

                string normalized = NormalizeTenantId(t.TenantId);
                string displayName = string.IsNullOrWhiteSpace(t.Name) ? normalized : t.Name;
                byId[normalized] = new TenantOption(normalized, displayName, TenantProvenance.Registered);
            }
        }

        if (observed.Status == SourceStatus.Loaded) {
            foreach (string tid in observed.TenantIds) {
                if (string.IsNullOrWhiteSpace(tid)) {
                    continue;
                }

                string normalized = NormalizeTenantId(tid);
                if (!byId.ContainsKey(normalized)) {
                    byId[normalized] = new TenantOption(normalized, normalized, TenantProvenance.ObservedOnly);
                }
            }
        }

        if (byId.Count == 0) {
            return new TenantOptionsResult([], TenantOptionsLoadStatus.Empty, EmptyMessage);
        }

        IReadOnlyList<TenantOption> sorted = [.. byId.Values
            .OrderBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(o => o.TenantId, StringComparer.OrdinalIgnoreCase)];

        bool partial = registered.Status != SourceStatus.Loaded || observed.Status != SourceStatus.Loaded;
        return partial
            ? new TenantOptionsResult(sorted, TenantOptionsLoadStatus.Partial, PartialDiagnostic)
            : new TenantOptionsResult(sorted, TenantOptionsLoadStatus.Loaded, null);
    }

    private static string NormalizeTenantId(string tenantId) =>
        tenantId.Trim().ToLowerInvariant();

    private async Task<RegisteredOutcome> LoadRegisteredAsync(CancellationToken ct) {
        try {
            IReadOnlyList<TenantSummary> tenants = await _tenants.ListTenantsAsync(ct).ConfigureAwait(false);
            return new RegisteredOutcome(SourceStatus.Loaded, tenants);
        }
        catch (UnauthorizedAccessException) {
            return new RegisteredOutcome(SourceStatus.Unauthorized, []);
        }
        catch (ForbiddenAccessException) {
            return new RegisteredOutcome(SourceStatus.Forbidden, []);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to load registered tenants for tenant option provider");
            return new RegisteredOutcome(SourceStatus.Unavailable, []);
        }
    }

    private async Task<ObservedOutcome> LoadObservedAsync(CancellationToken ct) {
        try {
            PagedResult<StreamSummary> result = await _streams
                .GetRecentlyActiveStreamsAsync(tenantId: null, domain: null, count: 1000, ct)
                .ConfigureAwait(false);
            IReadOnlyList<string> ids = [.. result.Items
                .Select(s => s.TenantId)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)];
            return new ObservedOutcome(SourceStatus.Loaded, ids);
        }
        catch (UnauthorizedAccessException) {
            return new ObservedOutcome(SourceStatus.Unauthorized, []);
        }
        catch (ForbiddenAccessException) {
            return new ObservedOutcome(SourceStatus.Forbidden, []);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to load observed tenants for tenant option provider");
            return new ObservedOutcome(SourceStatus.Unavailable, []);
        }
    }

    private enum SourceStatus { Loaded, Unauthorized, Forbidden, Unavailable }

    private sealed record RegisteredOutcome(SourceStatus Status, IReadOnlyList<TenantSummary> Tenants);

    private sealed record ObservedOutcome(SourceStatus Status, IReadOnlyList<string> TenantIds);
}
