using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Consistency;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// DAPR-backed implementation of <see cref="IConsistencyQueryService"/>.
/// Reads consistency check records from admin index keys in the state store.
/// </summary>
public sealed class DaprConsistencyQueryService : IConsistencyQueryService {
    private const string CheckKeyPrefix = "admin:consistency:";
    private const string IndexKey = "admin:consistency:index";

    private readonly DaprClient _daprClient;
    private readonly AdminServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprConsistencyQueryService"/> class.
    /// </summary>
    /// <param name="daprClient">The DAPR client.</param>
    /// <param name="options">The admin server options.</param>
    public DaprConsistencyQueryService(
        DaprClient daprClient,
        IOptions<AdminServerOptions> options) {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentNullException.ThrowIfNull(options);
        _daprClient = daprClient;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task<ConsistencyCheckResult?> GetCheckResultAsync(
        string checkId,
        CancellationToken ct = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkId);
        string key = $"{CheckKeyPrefix}{checkId}";
        ConsistencyCheckResult? result = await _daprClient
            .GetStateAsync<ConsistencyCheckResult>(_options.StateStoreName, key, cancellationToken: ct)
            .ConfigureAwait(false);

        return result is null ? null : ProjectTimeoutStatus(result);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ConsistencyCheckSummary>> GetChecksAsync(
        string? tenantId,
        CancellationToken ct = default) {
        List<string>? index = await _daprClient
            .GetStateAsync<List<string>>(_options.StateStoreName, IndexKey, cancellationToken: ct)
            .ConfigureAwait(false);

        if (index is null or { Count: 0 }) {
            return [];
        }

        List<ConsistencyCheckSummary> summaries = [];
        foreach (string checkId in index) {
            string key = $"{CheckKeyPrefix}{checkId}";
            ConsistencyCheckResult? result = await _daprClient
                .GetStateAsync<ConsistencyCheckResult>(_options.StateStoreName, key, cancellationToken: ct)
                .ConfigureAwait(false);

            if (result is null) {
                continue;
            }

            result = ProjectTimeoutStatus(result);

            if (tenantId is not null && result.TenantId != tenantId) {
                continue;
            }

            summaries.Add(new ConsistencyCheckSummary(
                result.CheckId,
                result.Status,
                result.TenantId,
                result.Domain,
                result.CheckTypes,
                result.StartedAtUtc,
                result.CompletedAtUtc,
                result.TimeoutUtc,
                result.StreamsChecked,
                result.AnomaliesFound));
        }

        return summaries
            .OrderByDescending(s => s.StartedAtUtc)
            .ToList();
    }

    private static ConsistencyCheckResult ProjectTimeoutStatus(ConsistencyCheckResult result) {
        if (result.Status == ConsistencyCheckStatus.Running && DateTimeOffset.UtcNow > result.TimeoutUtc) {
            return result with {
                Status = ConsistencyCheckStatus.Failed,
                ErrorMessage = "Timed out",
                CompletedAtUtc = result.TimeoutUtc,
            };
        }

        return result;
    }
}
