using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// Reads tenant projection data directly from the DAPR state store.
/// This is a tactical implementation that bypasses the EventStore query pipeline (not yet implemented).
/// When the v2 query pipeline (FR50-FR64) is available, this service should be updated to use it.
/// </summary>
public sealed class DaprTenantQueryService : ITenantQueryService {
    private const string TenantIndexProjectionKey = "projection:tenant-index:singleton";
    private const string TenantProjectionKeyPrefix = "projection:tenants:";

    private static readonly JsonSerializerOptions _options = new() {
        PropertyNameCaseInsensitive = true,
    };

    private readonly DaprClient _daprClient;
    private readonly ILogger<DaprTenantQueryService> _logger;
    private readonly AdminServerOptions _serverOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprTenantQueryService"/> class.
    /// </summary>
    public DaprTenantQueryService(
        DaprClient daprClient,
        IHttpClientFactory httpClientFactory,
        IOptions<AdminServerOptions> options,
        IAdminAuthContext authContext,
        ILogger<DaprTenantQueryService> logger) {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _daprClient = daprClient;
        _serverOptions = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TenantDetail?> GetTenantDetailAsync(string tenantId, CancellationToken ct = default) {
        try {
            JsonElement? state = await _daprClient
                .GetStateAsync<JsonElement?>(_serverOptions.StateStoreName, TenantProjectionKeyPrefix + tenantId, cancellationToken: ct)
                .ConfigureAwait(false);

            if (state is not { ValueKind: JsonValueKind.Object } root) {
                return null;
            }

            string? tid = root.TryGetProperty("TenantId", out JsonElement tidProp) ? tidProp.GetString()
                : root.TryGetProperty("tenantId", out tidProp) ? tidProp.GetString() : null;

            if (string.IsNullOrEmpty(tid)) {
                return null;
            }

            string name = GetStringProp(root, "Name", "name") ?? string.Empty;
            string? description = GetStringProp(root, "Description", "description");
            TenantStatusType status = GetStatusProp(root);
            DateTimeOffset createdAt = root.TryGetProperty("CreatedAt", out JsonElement caProp) || root.TryGetProperty("createdAt", out caProp)
                ? caProp.GetDateTimeOffset()
                : DateTimeOffset.MinValue;

            return new TenantDetail(tid, name, description, status, createdAt);
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to read tenant detail for {TenantId} from state store.", tenantId);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TenantUser>> GetTenantUsersAsync(string tenantId, CancellationToken ct = default) {
        try {
            JsonElement? state = await _daprClient
                .GetStateAsync<JsonElement?>(_serverOptions.StateStoreName, TenantProjectionKeyPrefix + tenantId, cancellationToken: ct)
                .ConfigureAwait(false);

            if (state is not { ValueKind: JsonValueKind.Object } root) {
                return [];
            }

            if (!root.TryGetProperty("Members", out JsonElement members) && !root.TryGetProperty("members", out members)) {
                return [];
            }

            List<TenantUser> users = [];
            foreach (JsonProperty prop in members.EnumerateObject()) {
                users.Add(new TenantUser(prop.Name, prop.Value.GetString() ?? "Unknown"));
            }

            return users;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to read tenant users for {TenantId} from state store.", tenantId);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TenantSummary>> ListTenantsAsync(CancellationToken ct = default) {
        try {
            _logger.LogInformation("Reading tenant index from state store '{StateStore}' with key '{Key}'.", _serverOptions.StateStoreName, TenantIndexProjectionKey);

            JsonElement state = await _daprClient
                .GetStateAsync<JsonElement>(_serverOptions.StateStoreName, TenantIndexProjectionKey, cancellationToken: ct)
                .ConfigureAwait(false);

            _logger.LogInformation("Tenant index state ValueKind={ValueKind}.", state.ValueKind);

            if (state.ValueKind != JsonValueKind.Object) {
                return [];
            }

            JsonElement root = state;

            if (!root.TryGetProperty("Tenants", out JsonElement tenantsElement) && !root.TryGetProperty("tenants", out tenantsElement)) {
                return [];
            }

            List<TenantSummary> tenants = [];
            foreach (JsonProperty prop in tenantsElement.EnumerateObject()) {
                string tenantId = prop.Name;
                string name = GetStringProp(prop.Value, "Name", "name") ?? tenantId;
                TenantStatusType status = GetStatusFromEntry(prop.Value);
                tenants.Add(new TenantSummary(tenantId, name, status));
            }

            return tenants;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to read tenant index from state store.");
            return [];
        }
    }

    private static string? GetStringProp(JsonElement element, string pascalName, string camelName)
        => element.TryGetProperty(pascalName, out JsonElement prop) || element.TryGetProperty(camelName, out prop)
            ? prop.GetString()
            : null;

    private static TenantStatusType GetStatusProp(JsonElement root) {
        if (!root.TryGetProperty("Status", out JsonElement statusProp) && !root.TryGetProperty("status", out statusProp)) {
            return TenantStatusType.Active;
        }

        // Status can be a string ("Active", "Disabled") or an integer (0, 1)
        if (statusProp.ValueKind == JsonValueKind.String) {
            string? val = statusProp.GetString();
            return string.Equals(val, "Disabled", StringComparison.OrdinalIgnoreCase)
                ? TenantStatusType.Disabled
                : TenantStatusType.Active;
        }

        if (statusProp.ValueKind == JsonValueKind.Number) {
            return statusProp.GetInt32() == 1 ? TenantStatusType.Disabled : TenantStatusType.Active;
        }

        return TenantStatusType.Active;
    }

    private static TenantStatusType GetStatusFromEntry(JsonElement entry) => GetStatusProp(entry);
}
