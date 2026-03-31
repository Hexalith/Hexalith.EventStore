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
    private const string StorageOverviewIndexPrefix = "admin:storage-overview:";
    // Optional projection-maintained index used to provide exact stream totals when
    // the StorageOverview payload does not include TotalStreamCount.
    private const string StorageStreamCountIndexPrefix = "admin:storage-stream-count:";

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
        string scope = tenantId ?? "all";
        string indexKey = $"{StorageOverviewIndexPrefix}{scope}";
        StorageOverview? result = await _daprClient
            .GetStateAsync<StorageOverview>(_options.StateStoreName, indexKey, cancellationToken: ct)
            .ConfigureAwait(false);

        if (result is null)
        {
            _logger.LogWarning("Admin index '{IndexKey}' not found. Index population requires admin projection setup.", indexKey);
            return new StorageOverview(0, null, [], 0);
        }

        if (result.TotalStreamCount is null)
        {
            long? streamCount = await TryGetStorageStreamCountAsync(scope, ct).ConfigureAwait(false);
            if (streamCount is not null)
            {
                return new StorageOverview(result.TotalEventCount, result.TotalSizeBytes, result.TenantBreakdown, streamCount.Value);
            }
        }

        return result;
    }

    private async Task<long?> TryGetStorageStreamCountAsync(string scope, CancellationToken ct)
    {
        string streamCountKey = $"{StorageStreamCountIndexPrefix}{scope}";

        try
        {
            long? streamCount = await _daprClient
                .GetStateAsync<long?>(_options.StateStoreName, streamCountKey, cancellationToken: ct)
                .ConfigureAwait(false);

            return streamCount is null || streamCount < 0
                ? null
                : streamCount;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Unable to read optional stream count index '{IndexKey}'.", streamCountKey);
            return null;
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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CompactionJob>> GetCompactionJobsAsync(
        string? tenantId,
        CancellationToken ct = default)
    {
        string indexKey = $"admin:storage-compaction-jobs:{tenantId ?? "all"}";
        List<CompactionJob>? result = await _daprClient
            .GetStateAsync<List<CompactionJob>>(_options.StateStoreName, indexKey, cancellationToken: ct)
            .ConfigureAwait(false);

        if (result is null)
        {
            _logger.LogWarning("Admin index '{IndexKey}' not found. Index population requires admin projection setup.", indexKey);
            return [];
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SnapshotPolicy>> GetSnapshotPoliciesAsync(
        string? tenantId,
        CancellationToken ct = default)
    {
        string indexKey = $"admin:storage-snapshot-policies:{tenantId ?? "all"}";
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
}
