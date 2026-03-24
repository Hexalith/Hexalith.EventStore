using System.Net.Http.Headers;
using System.Net.Http.Json;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// Delegates to Hexalith.Tenants peer service. EventStore does NOT own tenant state.
/// All methods use DAPR service invocation to the Tenants service app ID.
/// </summary>
public sealed class DaprTenantQueryService : ITenantQueryService
{
    private readonly IAdminAuthContext _authContext;
    private readonly DaprClient _daprClient;
    private readonly ILogger<DaprTenantQueryService> _logger;
    private readonly AdminServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprTenantQueryService"/> class.
    /// </summary>
    /// <param name="daprClient">The DAPR client.</param>
    /// <param name="options">The admin server options.</param>
    /// <param name="authContext">The admin auth context for JWT forwarding.</param>
    /// <param name="logger">The logger.</param>
    public DaprTenantQueryService(
        DaprClient daprClient,
        IOptions<AdminServerOptions> options,
        IAdminAuthContext authContext,
        ILogger<DaprTenantQueryService> logger)
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
    public async Task<IReadOnlyList<TenantSummary>> ListTenantsAsync(CancellationToken ct = default)
    {
        try
        {
            IReadOnlyList<TenantSummary>? result = await InvokeTenantServiceAsync<IReadOnlyList<TenantSummary>>(
                HttpMethod.Get, "api/v1/tenants", ct).ConfigureAwait(false);
            return result ?? [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list tenants from Tenants service.");
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<TenantQuotas> GetTenantQuotasAsync(string tenantId, CancellationToken ct = default)
    {
        try
        {
            TenantQuotas? result = await InvokeTenantServiceAsync<TenantQuotas>(
                HttpMethod.Get, $"api/v1/tenants/{Uri.EscapeDataString(tenantId)}/quotas", ct).ConfigureAwait(false);
            return result ?? new TenantQuotas(tenantId, 0, 0, 0);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get tenant quotas for {TenantId}.", tenantId);
            return new TenantQuotas(tenantId, 0, 0, 0);
        }
    }

    /// <inheritdoc/>
    public async Task<TenantDetail?> GetTenantDetailAsync(string tenantId, CancellationToken ct = default)
    {
        try
        {
            return await InvokeTenantServiceAsync<TenantDetail>(
                HttpMethod.Get, $"api/v1/tenants/{Uri.EscapeDataString(tenantId)}", ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get tenant detail for {TenantId}.", tenantId);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TenantUser>> GetTenantUsersAsync(string tenantId, CancellationToken ct = default)
    {
        try
        {
            IReadOnlyList<TenantUser>? result = await InvokeTenantServiceAsync<IReadOnlyList<TenantUser>>(
                HttpMethod.Get, $"api/v1/tenants/{Uri.EscapeDataString(tenantId)}/users", ct).ConfigureAwait(false);
            return result ?? [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get tenant users for {TenantId}.", tenantId);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<TenantComparison> CompareTenantUsageAsync(
        IReadOnlyList<string> tenantIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tenantIds);
        if (tenantIds.Count == 0)
        {
            return new TenantComparison([], DateTimeOffset.UtcNow);
        }

        try
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.ServiceInvocationTimeoutSeconds));

            using HttpRequestMessage request = _daprClient.CreateInvokeMethodRequest(
                _options.TenantServiceAppId, "api/v1/tenants/compare", tenantIds)
                ?? new HttpRequestMessage(HttpMethod.Post, "api/v1/tenants/compare")
                {
                    Content = JsonContent.Create(tenantIds),
                };

            string? token = _authContext.GetToken();
            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            TenantComparison? result = await _daprClient
                .InvokeMethodAsync<TenantComparison>(request, cts.Token)
                .ConfigureAwait(false);

            return result ?? new TenantComparison([], DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Tenants service compare endpoint timed out.");
            return new TenantComparison([], DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to compare tenant usage.");
            return new TenantComparison([], DateTimeOffset.UtcNow);
        }
    }

    private async Task<TResponse?> InvokeTenantServiceAsync<TResponse>(
        HttpMethod method,
        string endpoint,
        CancellationToken ct)
    {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.ServiceInvocationTimeoutSeconds));

        using HttpRequestMessage request = _daprClient.CreateInvokeMethodRequest(
            method, _options.TenantServiceAppId, endpoint)
            ?? new HttpRequestMessage(method, endpoint);

        string? token = _authContext.GetToken();
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await _daprClient.InvokeMethodAsync<TResponse>(request, cts.Token).ConfigureAwait(false);
    }
}
