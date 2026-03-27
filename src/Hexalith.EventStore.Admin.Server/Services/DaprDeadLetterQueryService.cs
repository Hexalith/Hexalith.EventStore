using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// DAPR-backed implementation of <see cref="IDeadLetterQueryService"/>.
/// Reads dead-letter index from the state store. The index is maintained by the DeadLetterPublisher in Server.
/// </summary>
public sealed class DaprDeadLetterQueryService : IDeadLetterQueryService
{
    private readonly DaprClient _daprClient;
    private readonly ILogger<DaprDeadLetterQueryService> _logger;
    private readonly AdminServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprDeadLetterQueryService"/> class.
    /// </summary>
    /// <param name="daprClient">The DAPR client.</param>
    /// <param name="options">The admin server options.</param>
    /// <param name="logger">The logger.</param>
    public DaprDeadLetterQueryService(
        DaprClient daprClient,
        IOptions<AdminServerOptions> options,
        ILogger<DaprDeadLetterQueryService> logger)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _daprClient = daprClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> GetDeadLetterCountAsync(CancellationToken ct = default)
    {
        List<DeadLetterEntry>? result = await _daprClient
            .GetStateAsync<List<DeadLetterEntry>>(_options.StateStoreName, "admin:dead-letters:all", cancellationToken: ct)
            .ConfigureAwait(false);
        return result?.Count ?? 0;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<DeadLetterEntry>> ListDeadLettersAsync(
        string? tenantId,
        int count,
        string? continuationToken,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        string indexKey = $"admin:dead-letters:{tenantId ?? "all"}";
        try
        {
            List<DeadLetterEntry>? result = await _daprClient
                .GetStateAsync<List<DeadLetterEntry>>(_options.StateStoreName, indexKey, cancellationToken: ct)
                .ConfigureAwait(false);

            if (result is null)
            {
                _logger.LogWarning("Admin index '{IndexKey}' not found. Index population requires admin projection setup.", indexKey);
                return new PagedResult<DeadLetterEntry>([], 0, null);
            }

            int skip = 0;
            if (continuationToken is not null && int.TryParse(continuationToken, out int offset) && offset >= 0)
            {
                skip = offset;
            }

            List<DeadLetterEntry> page = result.Skip(skip).Take(count).ToList();
            string? nextToken = skip + count < result.Count ? (skip + count).ToString() : null;

            return new PagedResult<DeadLetterEntry>(page, result.Count, nextToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read dead letter index '{IndexKey}'.", indexKey);
            return new PagedResult<DeadLetterEntry>([], 0, null);
        }
    }
}
