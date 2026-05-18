using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Contracts.Security;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// DAPR-backed implementation of <see cref="IBackupQueryService"/>.
/// Reads backup job metadata from admin index keys in the state store.
/// </summary>
public sealed class DaprBackupQueryService : IBackupQueryService {
    private const string BackupJobsIndexPrefix = "admin:backup-jobs:";

    private readonly DaprClient _daprClient;
    private readonly ILogger<DaprBackupQueryService> _logger;
    private readonly AdminServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprBackupQueryService"/> class.
    /// </summary>
    /// <param name="daprClient">The DAPR client.</param>
    /// <param name="options">The admin server options.</param>
    /// <param name="logger">The logger.</param>
    public DaprBackupQueryService(
        DaprClient daprClient,
        IOptions<AdminServerOptions> options,
        ILogger<DaprBackupQueryService> logger) {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _daprClient = daprClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BackupJob>> GetBackupJobsAsync(
        string? tenantId,
        CancellationToken ct = default) {
        string scope = tenantId ?? "all";
        string indexKey = $"{BackupJobsIndexPrefix}{scope}";
        IReadOnlyList<BackupJob>? result = await _daprClient
            .GetStateAsync<IReadOnlyList<BackupJob>>(_options.StateStoreName, indexKey, cancellationToken: ct)
            .ConfigureAwait(false);

        if (result is null) {
            _logger.LogWarning("Admin index '{IndexKey}' not found. No backup jobs exist yet.", indexKey);
            return [];
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<RestoredBackupAdmissionResult?> GetRestoreAdmissionAsync(
        string tenantId,
        string admissionId,
        CancellationToken ct = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(admissionId);
        return await _daprClient
            .GetStateAsync<RestoredBackupAdmissionResult>(
                _options.StateStoreName,
                DaprBackupCommandService.AdmissionKey(tenantId, admissionId),
                cancellationToken: ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<CryptoShreddingWorkflowDecision?> GetCryptoShreddingWorkflowAsync(
        string tenantId,
        string workflowId,
        CancellationToken ct = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        return await _daprClient
            .GetStateAsync<CryptoShreddingWorkflowDecision>(
                _options.StateStoreName,
                DaprBackupCommandService.WorkflowIdKey(tenantId, workflowId),
                cancellationToken: ct)
            .ConfigureAwait(false);
    }
}
