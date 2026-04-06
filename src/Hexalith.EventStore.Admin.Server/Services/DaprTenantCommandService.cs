using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// DAPR-backed implementation of <see cref="ITenantCommandService"/>.
/// All write methods delegate to Hexalith.Tenants peer service via DAPR service invocation.
/// EventStore does NOT own tenant state (FR77).
/// </summary>
public sealed class DaprTenantCommandService : ITenantCommandService
{
    private const string ErrorNoOperation = "error-no-operation";

    private readonly IAdminAuthContext _authContext;
    private readonly DaprClient _daprClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DaprTenantCommandService> _logger;
    private readonly AdminServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprTenantCommandService"/> class.
    /// </summary>
    /// <param name="daprClient">The DAPR client.</param>
    /// <param name="httpClientFactory">The HTTP client factory for DAPR service invocation.</param>
    /// <param name="options">The admin server options.</param>
    /// <param name="authContext">The admin auth context for JWT forwarding.</param>
    /// <param name="logger">The logger.</param>
    public DaprTenantCommandService(
        DaprClient daprClient,
        IHttpClientFactory httpClientFactory,
        IOptions<AdminServerOptions> options,
        IAdminAuthContext authContext,
        ILogger<DaprTenantCommandService> logger)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(authContext);
        ArgumentNullException.ThrowIfNull(logger);
        _daprClient = daprClient;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _authContext = authContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AdminOperationResult> CreateTenantAsync(
        CreateTenantRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await InvokeTenantServicePostAsync(
            "api/v1/tenants",
            request,
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<AdminOperationResult> DisableTenantAsync(
        string tenantId,
        CancellationToken ct = default)
        => await InvokeTenantServicePostAsync<object?>(
            $"api/v1/tenants/{Uri.EscapeDataString(tenantId)}/disable",
            null,
            ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<AdminOperationResult> EnableTenantAsync(
        string tenantId,
        CancellationToken ct = default)
        => await InvokeTenantServicePostAsync<object?>(
            $"api/v1/tenants/{Uri.EscapeDataString(tenantId)}/enable",
            null,
            ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<AdminOperationResult> AddUserToTenantAsync(
        string tenantId,
        string email,
        string role,
        CancellationToken ct = default)
        => await InvokeTenantServicePostAsync(
            $"api/v1/tenants/{Uri.EscapeDataString(tenantId)}/users",
            new AddTenantUserRequest(email, role),
            ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<AdminOperationResult> RemoveUserFromTenantAsync(
        string tenantId,
        string email,
        CancellationToken ct = default)
        => await InvokeTenantServicePostAsync(
            $"api/v1/tenants/{Uri.EscapeDataString(tenantId)}/remove-user",
            new RemoveTenantUserRequest(email),
            ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<AdminOperationResult> ChangeUserRoleAsync(
        string tenantId,
        string email,
        string newRole,
        CancellationToken ct = default)
        => await InvokeTenantServicePostAsync(
            $"api/v1/tenants/{Uri.EscapeDataString(tenantId)}/change-role",
            new ChangeTenantUserRoleRequest(email, newRole),
            ct).ConfigureAwait(false);

    private async Task<AdminOperationResult> InvokeTenantServicePostAsync<TRequest>(
        string endpoint,
        TRequest request,
        CancellationToken ct)
    {
        try
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.ServiceInvocationTimeoutSeconds));

            using HttpRequestMessage httpRequest = _daprClient.CreateInvokeMethodRequest(
                _options.TenantServiceAppId,
                endpoint,
                request)
                ?? CreateFallbackRequest(endpoint, request);
            httpRequest.Method = HttpMethod.Post;

            string? token = _authContext.GetToken();
            if (token is not null)
            {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            HttpClient httpClient = _httpClientFactory.CreateClient();
            using HttpResponseMessage httpResponse = await httpClient.SendAsync(httpRequest, cts.Token).ConfigureAwait(false);
            httpResponse.EnsureSuccessStatusCode();
            AdminOperationResult? result = await httpResponse.Content.ReadFromJsonAsync<AdminOperationResult>(cts.Token).ConfigureAwait(false);

            return result ?? new AdminOperationResult(false, ErrorNoOperation, "Null response from Tenants service", "NULL_RESPONSE");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Tenants service endpoint '{Endpoint}' timed out.", endpoint);
            return new AdminOperationResult(false, ErrorNoOperation, "Service invocation timed out", "TIMEOUT");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invoke Tenants service endpoint '{Endpoint}'.", endpoint);
            return new AdminOperationResult(false, ErrorNoOperation, "Tenants service is unavailable", GetErrorCode(ex));
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

    private static HttpRequestMessage CreateFallbackRequest<TRequest>(string endpoint, TRequest request)
        => new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(request),
        };
}
