using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// DAPR-backed implementation of <see cref="IStorageQueryService"/>.
/// Reads storage metrics and snapshot policies from admin index keys in the state store.
/// </summary>
public sealed class DaprStorageQueryService : IStorageQueryService
{
    private readonly DaprClient _daprClient;
    private readonly ILogger<DaprStorageQueryService> _logger;
    private readonly AdminServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprStorageQueryService"/> class.
    /// </summary>
    /// <param name="daprClient">The DAPR client.</param>
    /// <param name="options">The admin server options.</param>
    /// <param name="logger">The logger.</param>
    public DaprStorageQueryService(
        DaprClient daprClient,
        IOptions<AdminServerOptions> options,
        ILogger<DaprStorageQueryService> logger)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _daprClient = daprClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<StorageOverview> GetStorageOverviewAsync(
        string? tenantId,
        CancellationToken ct = default)
    {
        string indexKey = $"admin:storage-overview:{tenantId ?? "all"}";
        try
        {
            StorageOverview? result = await _daprClient
                .GetStateAsync<StorageOverview>(_options.StateStoreName, indexKey, cancellationToken: ct)
                .ConfigureAwait(false);

            if (result is null)
            {
                _logger.LogWarning("Admin index '{IndexKey}' not found. Index population requires admin projection setup.", indexKey);
                return new StorageOverview(0, null, []);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read storage overview '{IndexKey}'.", indexKey);
            return new StorageOverview(0, null, []);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StreamStorageInfo>> GetHotStreamsAsync(
        string? tenantId,
        int count,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        string indexKey = $"admin:storage-hot-streams:{tenantId ?? "all"}";
        try
        {
            List<StreamStorageInfo>? result = await _daprClient
                .GetStateAsync<List<StreamStorageInfo>>(_options.StateStoreName, indexKey, cancellationToken: ct)
                .ConfigureAwait(false);

            if (result is null)
            {
                _logger.LogWarning("Admin index '{IndexKey}' not found. Index population requires admin projection setup.", indexKey);
                return [];
            }

            return result.OrderByDescending(s => s.EventCount).Take(count).ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read hot streams index '{IndexKey}'.", indexKey);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SnapshotPolicy>> GetSnapshotPoliciesAsync(
        string? tenantId,
        CancellationToken ct = default)
    {
        string indexKey = $"admin:storage-snapshot-policies:{tenantId ?? "all"}";
        try
        {
            List<SnapshotPolicy>? result = await _daprClient
                .GetStateAsync<List<SnapshotPolicy>>(_options.StateStoreName, indexKey, cancellationToken: ct)
                .ConfigureAwait(false);

            if (result is null)
            {
                _logger.LogWarning("Admin index '{IndexKey}' not found. Index population requires admin projection setup.", indexKey);
                return [];
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read snapshot policies index '{IndexKey}'.", indexKey);
            return [];
        }
    }
}
