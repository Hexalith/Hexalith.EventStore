using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// DAPR-backed implementation of <see cref="IDeadLetterCommandService"/>.
/// All write methods delegate to EventStore via DAPR service invocation — never writes directly.
/// </summary>
public sealed class DaprDeadLetterCommandService : IDeadLetterCommandService {
    private const string ErrorNoOperation = "error-no-operation";

    private readonly IAdminAuthContext _authContext;
    private readonly DaprClient _daprClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DaprDeadLetterCommandService> _logger;
    private readonly AdminServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprDeadLetterCommandService"/> class.
    /// </summary>
    /// <param name="daprClient">The DAPR client.</param>
    /// <param name="httpClientFactory">The HTTP client factory for DAPR service invocation.</param>
    /// <param name="options">The admin server options.</param>
    /// <param name="authContext">The admin auth context for JWT forwarding.</param>
    /// <param name="logger">The logger.</param>
    public DaprDeadLetterCommandService(
        DaprClient daprClient,
        IHttpClientFactory httpClientFactory,
        IOptions<AdminServerOptions> options,
        IAdminAuthContext authContext,
        ILogger<DaprDeadLetterCommandService> logger) {
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
    public async Task<AdminOperationResult> RetryDeadLettersAsync(
        string tenantId,
        IReadOnlyList<string> messageIds,
        CancellationToken ct = default)
        => await InvokeEventStorePostAsync(
            "api/v1/admin/dead-letters/retry",
            new { tenantId, messageIds },
            ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<AdminOperationResult> SkipDeadLettersAsync(
        string tenantId,
        IReadOnlyList<string> messageIds,
        CancellationToken ct = default)
        => await InvokeEventStorePostAsync(
            "api/v1/admin/dead-letters/skip",
            new { tenantId, messageIds },
            ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<AdminOperationResult> ArchiveDeadLettersAsync(
        string tenantId,
        IReadOnlyList<string> messageIds,
        CancellationToken ct = default)
        => await InvokeEventStorePostAsync(
            "api/v1/admin/dead-letters/archive",
            new { tenantId, messageIds },
            ct).ConfigureAwait(false);

    private async Task<AdminOperationResult> InvokeEventStorePostAsync<TRequest>(
        string endpoint,
        TRequest request,
        CancellationToken ct) {
        try {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.ServiceInvocationTimeoutSeconds));

            using HttpRequestMessage httpRequest = _daprClient.CreateInvokeMethodRequest(
                _options.EventStoreAppId, endpoint, request)
                ?? CreateFallbackRequest(endpoint, request);

            string? token = _authContext.GetToken();
            if (token is not null) {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            HttpClient httpClient = _httpClientFactory.CreateClient();
            using HttpResponseMessage httpResponse = await httpClient.SendAsync(httpRequest, cts.Token).ConfigureAwait(false);
            _ = httpResponse.EnsureSuccessStatusCode();
            AdminOperationResult? result = await httpResponse.Content.ReadFromJsonAsync<AdminOperationResult>(cts.Token).ConfigureAwait(false);

            return result ?? new AdminOperationResult(false, ErrorNoOperation, "Null response from EventStore", "NULL_RESPONSE");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            _logger.LogWarning("EventStore endpoint '{Endpoint}' timed out.", endpoint);
            return new AdminOperationResult(false, ErrorNoOperation, "Service invocation timed out", "TIMEOUT");
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to invoke EventStore endpoint '{Endpoint}'.", endpoint);
            return new AdminOperationResult(false, ErrorNoOperation, ex.Message, GetErrorCode(ex));
        }
    }

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

    private static HttpRequestMessage CreateFallbackRequest<TRequest>(string endpoint, TRequest request)
        => new(HttpMethod.Post, endpoint) {
            Content = JsonContent.Create(request),
        };
}
