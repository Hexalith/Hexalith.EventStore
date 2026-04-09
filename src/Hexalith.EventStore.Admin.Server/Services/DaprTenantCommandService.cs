using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Identity;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// DAPR-backed implementation of <see cref="ITenantCommandService"/>.
/// All write methods route through EventStore's command pipeline (POST /api/v1/commands).
/// </summary>
public sealed class DaprTenantCommandService : ITenantCommandService {
    private const string CommandEndpoint = "api/v1/commands";
    private const string ErrorNoOperation = "error-no-operation";
    private static readonly string[] _tenantRoleNames = Enum.GetNames<TenantRole>();

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
        ILogger<DaprTenantCommandService> logger) {
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
    public async Task<AdminOperationResult> AddUserToTenantAsync(
        string tenantId,
        string userId,
        string role,
        CancellationToken ct = default) {
        if (!TryParseTenantRole(role, out TenantRole tenantRole)) {
            return InvalidRoleResult(role);
        }

        return await SubmitCommandAsync(
            tenantId,
            nameof(AddUserToTenant),
            new AddUserToTenant(tenantId, userId, tenantRole),
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<AdminOperationResult> ChangeUserRoleAsync(
        string tenantId,
        string userId,
        string newRole,
        CancellationToken ct = default) {
        if (!TryParseTenantRole(newRole, out TenantRole tenantRole)) {
            return InvalidRoleResult(newRole);
        }

        return await SubmitCommandAsync(
            tenantId,
            nameof(ChangeUserRole),
            new ChangeUserRole(tenantId, userId, tenantRole),
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<AdminOperationResult> CreateTenantAsync(
        CreateTenantRequest request,
        CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(request);
        return await SubmitCommandAsync(
            request.TenantId,
            nameof(CreateTenant),
            new CreateTenant(request.TenantId, request.Name, request.Description),
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<AdminOperationResult> DisableTenantAsync(
        string tenantId,
        CancellationToken ct = default)
        => await SubmitCommandAsync(
            tenantId,
            nameof(DisableTenant),
            new DisableTenant(tenantId),
            ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<AdminOperationResult> EnableTenantAsync(
        string tenantId,
        CancellationToken ct = default)
        => await SubmitCommandAsync(
            tenantId,
            nameof(EnableTenant),
            new EnableTenant(tenantId),
            ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<AdminOperationResult> RemoveUserFromTenantAsync(
        string tenantId,
        string userId,
        CancellationToken ct = default)
        => await SubmitCommandAsync(
            tenantId,
            nameof(RemoveUserFromTenant),
            new RemoveUserFromTenant(tenantId, userId),
            ct).ConfigureAwait(false);

    private static string GetErrorCode(Exception exception) {
        Exception current = exception;
        while (true) {
            if (current is HttpRequestException httpRequestException && httpRequestException.StatusCode is HttpStatusCode statusCode) {
                return ((int)statusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            object? reflectedStatus = current.GetType().GetProperty("StatusCode")?.GetValue(current);
            if (reflectedStatus is not null) {
                return reflectedStatus.ToString() ?? current.GetType().Name;
            }

            if (current.InnerException is null) {
                return current.GetType().Name;
            }

            current = current.InnerException;
        }
    }

    private static AdminOperationResult InvalidRoleResult(string? role)
        => new(
            false,
            ErrorNoOperation,
            $"Invalid role '{role}'. Valid values: {string.Join(", ", _tenantRoleNames)}",
            "INVALID_ROLE");

    private static bool TryParseTenantRole(string? role, out TenantRole tenantRole) {
        string? normalizedRole = role?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedRole)) {
            tenantRole = default;
            return false;
        }

        foreach (TenantRole candidate in Enum.GetValues<TenantRole>()) {
            if (string.Equals(candidate.ToString(), normalizedRole, StringComparison.OrdinalIgnoreCase)) {
                tenantRole = candidate;
                return true;
            }
        }

        tenantRole = default;
        return false;
    }

    private async Task<AdminOperationResult> SubmitCommandAsync<TPayload>(
        string aggregateId,
        string commandType,
        TPayload payload,
        CancellationToken ct) {
        try {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.ServiceInvocationTimeoutSeconds));

            string correlationId = Guid.NewGuid().ToString();
            JsonElement payloadElement = JsonSerializer.SerializeToElement(payload);

            object commandBody = new {
                messageId = Guid.NewGuid().ToString(),
                tenant = TenantIdentity.DefaultTenantId,
                domain = TenantIdentity.Domain,
                aggregateId,
                commandType,
                payload = payloadElement,
                correlationId,
                extensions = (Dictionary<string, string>?)null,
            };

            using HttpRequestMessage httpRequest = _daprClient.CreateInvokeMethodRequest(
                HttpMethod.Post,
                _options.EventStoreAppId,
                CommandEndpoint)
                ?? new HttpRequestMessage(HttpMethod.Post, CommandEndpoint);
            httpRequest.Content = JsonContent.Create(commandBody);

            string? token = _authContext.GetToken();
            if (token is not null) {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            HttpClient httpClient = _httpClientFactory.CreateClient();
            using HttpResponseMessage httpResponse = await httpClient.SendAsync(httpRequest, cts.Token).ConfigureAwait(false);

            if (httpResponse.StatusCode == HttpStatusCode.Accepted) {
                return new AdminOperationResult(true, correlationId, null, null);
            }

            string errorBody = await httpResponse.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);

            if (httpResponse.IsSuccessStatusCode) {
                _logger.LogWarning(
                    "Command {CommandType} for aggregate {AggregateId} returned unexpected success status {StatusCode}: {Error}",
                    commandType,
                    aggregateId,
                    (int)httpResponse.StatusCode,
                    errorBody);
                return new AdminOperationResult(
                    false,
                    correlationId,
                    $"Unexpected response status {(int)httpResponse.StatusCode} from EventStore command endpoint.",
                    "UNEXPECTED_STATUS");
            }

            _logger.LogWarning(
                "Command {CommandType} for aggregate {AggregateId} returned {StatusCode}: {Error}",
                commandType,
                aggregateId,
                (int)httpResponse.StatusCode,
                errorBody);
            return new AdminOperationResult(
                false,
                correlationId,
                $"Command rejected ({(int)httpResponse.StatusCode}). See server logs with correlation ID {correlationId}.",
                ((int)httpResponse.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            _logger.LogWarning("Command {CommandType} for aggregate {AggregateId} timed out.", commandType, aggregateId);
            return new AdminOperationResult(false, ErrorNoOperation, "Service invocation timed out", "TIMEOUT");
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to submit command {CommandType} for aggregate {AggregateId}.", commandType, aggregateId);
            return new AdminOperationResult(false, ErrorNoOperation, "EventStore service is unavailable", GetErrorCode(ex));
        }
    }
}
