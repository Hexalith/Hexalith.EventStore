using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// Routes authorized dead-letter queries to the dedicated EventStore operations workload.
/// </summary>
public sealed class DaprDeadLetterQueryService : IDeadLetterQueryService {
    private const string InternalRoute = "internal/dead-letters";

    private readonly IAdminAuthContext _authContext;
    private readonly DaprClient _daprClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DaprDeadLetterQueryService> _logger;
    private readonly AdminServerOptions _options;

    /// <summary>
    /// Initializes a new dead-letter query facade.
    /// </summary>
    /// <param name="daprClient">The Dapr client.</param>
    /// <param name="httpClientFactory">The HTTP client factory used for Dapr invocation.</param>
    /// <param name="options">The admin server options.</param>
    /// <param name="authContext">The caller authorization context.</param>
    /// <param name="logger">The logger.</param>
    public DaprDeadLetterQueryService(
        DaprClient daprClient,
        IHttpClientFactory httpClientFactory,
        IOptions<AdminServerOptions> options,
        IAdminAuthContext authContext,
        ILogger<DaprDeadLetterQueryService> logger) {
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
    public async Task<int> GetDeadLetterCountAsync(CancellationToken ct = default)
        => await InvokeGetAsync<int>(InternalRoute + "/count", [], ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<PagedResult<DeadLetterEntry>> ListDeadLettersAsync(
        string? tenantId,
        int count,
        string? continuationToken,
        CancellationToken ct = default) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        if (tenantId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        }

        List<KeyValuePair<string, string>> query = [new("count", count.ToString(System.Globalization.CultureInfo.InvariantCulture))];
        if (!string.IsNullOrWhiteSpace(tenantId)) {
            query.Add(new("tenantId", tenantId));
        }

        if (!string.IsNullOrWhiteSpace(continuationToken)) {
            query.Add(new("continuationToken", continuationToken));
        }

        return await InvokeGetAsync<PagedResult<DeadLetterEntry>>(InternalRoute, query, ct).ConfigureAwait(false);
    }

    private async Task<T> InvokeGetAsync<T>(
        string endpoint,
        IReadOnlyCollection<KeyValuePair<string, string>> query,
        CancellationToken ct) {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.ServiceInvocationTimeoutSeconds));
        try {
            using HttpRequestMessage request = query.Count == 0
                ? _daprClient.CreateInvokeMethodRequest(HttpMethod.Get, _options.OperationsAppId, endpoint)
                : _daprClient.CreateInvokeMethodRequest(HttpMethod.Get, _options.OperationsAppId, endpoint, query);
            string? token = _authContext.GetToken();
            if (token is not null) {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            HttpClient client = _httpClientFactory.CreateClient();
            using HttpResponseMessage response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);

            // The operations workload answers an unauthorized caller with 403. Canonicalizing it here (DW11 AC4)
            // keeps the read surfaces aligned with the command facade: without this, EnsureSuccessStatusCode
            // raises HttpRequestException, which the controller classifies as a backend outage and reports to the
            // operator as a transient 503 to retry -- hiding an authorization failure that retrying cannot fix.
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) {
                _logger.LogWarning(
                    "Operations endpoint '{Endpoint}' denied the forwarded operator context.",
                    endpoint);
                throw new UnauthorizedAccessException("The operations workload denied the request.");
            }

            _ = response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(cts.Token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The operations workload returned an empty response.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            _logger.LogWarning("Operations endpoint '{Endpoint}' timed out.", endpoint);
            throw new TimeoutException("The operations workload timed out.");
        }
    }
}
