using System.Net.Http.Headers;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// DAPR-backed implementation of <see cref="IProjectionQueryService"/>.
/// Projection registry reads use state store; detail reads delegate to EventStore.
/// </summary>
public sealed class DaprProjectionQueryService : IProjectionQueryService
{
    private readonly IAdminAuthContext _authContext;
    private readonly DaprClient _daprClient;
    private readonly ILogger<DaprProjectionQueryService> _logger;
    private readonly AdminServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprProjectionQueryService"/> class.
    /// </summary>
    /// <param name="daprClient">The DAPR client.</param>
    /// <param name="options">The admin server options.</param>
    /// <param name="authContext">The admin auth context for JWT forwarding.</param>
    /// <param name="logger">The logger.</param>
    public DaprProjectionQueryService(
        DaprClient daprClient,
        IOptions<AdminServerOptions> options,
        IAdminAuthContext authContext,
        ILogger<DaprProjectionQueryService> logger)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(authContext);
        ArgumentNullException.ThrowIfNull(logger);
        _daprClient = daprClient;
        _options = options.Value;
        _authContext = authContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProjectionStatus>> ListProjectionsAsync(
        string? tenantId,
        CancellationToken ct = default)
    {
        string indexKey = $"admin:projections:{tenantId ?? "all"}";
        try
        {
            List<ProjectionStatus>? result = await _daprClient
                .GetStateAsync<List<ProjectionStatus>>(_options.StateStoreName, indexKey, cancellationToken: ct)
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
            _logger.LogWarning(ex, "Failed to read projection index '{IndexKey}'.", indexKey);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<ProjectionDetail> GetProjectionDetailAsync(
        string tenantId,
        string projectionName,
        CancellationToken ct = default)
    {
        string endpoint = $"api/v1/admin/projections/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(projectionName)}";
        try
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.ServiceInvocationTimeoutSeconds));

            using HttpRequestMessage request = _daprClient.CreateInvokeMethodRequest(
                HttpMethod.Get, _options.EventStoreAppId, endpoint)
                ?? new HttpRequestMessage(HttpMethod.Get, endpoint);

            string? token = _authContext.GetToken();
            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            ProjectionDetail? result = await _daprClient
                .InvokeMethodAsync<ProjectionDetail>(request, cts.Token)
                .ConfigureAwait(false);

            return result ?? CreateEmptyProjectionDetail(tenantId, projectionName, "not-found");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get projection detail for {TenantId}/{ProjectionName}.", tenantId, projectionName);
            return CreateEmptyProjectionDetail(tenantId, projectionName, "unavailable");
        }
    }

    private static ProjectionDetail CreateEmptyProjectionDetail(string tenantId, string projectionName, string status)
        => new(
            projectionName,
            tenantId,
            ProjectionStatusType.Error,
            0,
            0,
            1,
            0,
            DateTimeOffset.UnixEpoch,
            [new ProjectionError(0, DateTimeOffset.UnixEpoch, $"Projection detail {status}.", null)],
            $"{{\"status\":\"{status}\"}}",
            []);
}
