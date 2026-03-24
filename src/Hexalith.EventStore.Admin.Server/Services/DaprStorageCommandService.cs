using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// DAPR-backed implementation of <see cref="IStorageCommandService"/>.
/// All write methods delegate to CommandApi via DAPR service invocation — never writes directly.
/// </summary>
public sealed class DaprStorageCommandService : IStorageCommandService
{
    private const string ErrorNoOperation = "error-no-operation";

    private readonly IAdminAuthContext _authContext;
    private readonly DaprClient _daprClient;
    private readonly ILogger<DaprStorageCommandService> _logger;
    private readonly AdminServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprStorageCommandService"/> class.
    /// </summary>
    /// <param name="daprClient">The DAPR client.</param>
    /// <param name="options">The admin server options.</param>
    /// <param name="authContext">The admin auth context for JWT forwarding.</param>
    /// <param name="logger">The logger.</param>
    public DaprStorageCommandService(
        DaprClient daprClient,
        IOptions<AdminServerOptions> options,
        IAdminAuthContext authContext,
        ILogger<DaprStorageCommandService> logger)
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
    public async Task<AdminOperationResult> TriggerCompactionAsync(
        string tenantId,
        string? domain,
        CancellationToken ct = default)
        => await InvokeCommandApiPostAsync(
            "api/v1/admin/storage/compact",
            new { tenantId, domain },
            ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<AdminOperationResult> CreateSnapshotAsync(
        string tenantId,
        string domain,
        string aggregateId,
        CancellationToken ct = default)
        => await InvokeCommandApiPostAsync(
            "api/v1/admin/storage/snapshot",
            new { tenantId, domain, aggregateId },
            ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<AdminOperationResult> SetSnapshotPolicyAsync(
        string tenantId,
        string domain,
        string aggregateType,
        int intervalEvents,
        CancellationToken ct = default)
        => await InvokeCommandApiAsync(
            HttpMethod.Put,
            "api/v1/admin/storage/snapshot-policy",
            new { tenantId, domain, aggregateType, intervalEvents },
            ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<AdminOperationResult> DeleteSnapshotPolicyAsync(
        string tenantId,
        string domain,
        string aggregateType,
        CancellationToken ct = default)
        => await InvokeCommandApiAsync(
            HttpMethod.Delete,
            "api/v1/admin/storage/snapshot-policy",
            new { tenantId, domain, aggregateType },
            ct).ConfigureAwait(false);

    private async Task<AdminOperationResult> InvokeCommandApiPostAsync<TRequest>(
        string endpoint,
        TRequest request,
        CancellationToken ct)
        => await InvokeCommandApiAsync(HttpMethod.Post, endpoint, request, ct).ConfigureAwait(false);

    private async Task<AdminOperationResult> InvokeCommandApiAsync<TRequest>(
        HttpMethod method,
        string endpoint,
        TRequest request,
        CancellationToken ct)
    {
        try
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.ServiceInvocationTimeoutSeconds));

            using HttpRequestMessage httpRequest = _daprClient.CreateInvokeMethodRequest(
                _options.CommandApiAppId,
                endpoint,
                request)
                ?? CreateFallbackRequest(method, endpoint, request);
            httpRequest.Method = method;

            string? token = _authContext.GetToken();
            if (token is not null)
            {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            AdminOperationResult? result = await _daprClient
                .InvokeMethodAsync<AdminOperationResult>(httpRequest, cts.Token)
                .ConfigureAwait(false);

            return result ?? new AdminOperationResult(false, ErrorNoOperation, "Null response from CommandApi", "NULL_RESPONSE");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("CommandApi endpoint '{Endpoint}' timed out.", endpoint);
            return new AdminOperationResult(false, ErrorNoOperation, "Service invocation timed out", "TIMEOUT");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invoke CommandApi endpoint '{Endpoint}'.", endpoint);
            return new AdminOperationResult(false, ErrorNoOperation, ex.Message, GetErrorCode(ex));
        }
    }

    private static string GetErrorCode(Exception exception)
    {
        Exception current = exception;
        while (true)
        {
            if (current is HttpRequestException httpRequestException && httpRequestException.StatusCode is HttpStatusCode statusCode)
            {
                return ((int)statusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            object? reflectedStatus = current.GetType().GetProperty("StatusCode")?.GetValue(current);
            if (reflectedStatus is not null)
            {
                return reflectedStatus.ToString() ?? current.GetType().Name;
            }

            if (current.InnerException is null)
            {
                return current.GetType().Name;
            }

            current = current.InnerException;
        }
    }

    private static HttpRequestMessage CreateFallbackRequest<TRequest>(HttpMethod method, string endpoint, TRequest request)
        => new(method, endpoint)
        {
            Content = JsonContent.Create(request),
        };
}
