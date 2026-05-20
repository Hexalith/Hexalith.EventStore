using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Contracts.Queries;

using Hexalith.Tenants.Contracts;
using Hexalith.Tenants.Contracts.Enums;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using ContractsTenantDetail = Hexalith.Tenants.Contracts.Queries.TenantDetail;
using ContractsTenantMember = Hexalith.Tenants.Contracts.Queries.TenantMember;
using ContractsTenantSummary = Hexalith.Tenants.Contracts.Queries.TenantSummary;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// Queries tenant data via the EventStore query pipeline using DAPR service invocation.
/// </summary>
public sealed class DaprTenantQueryService : ITenantQueryService {
    private const int _defaultPageSize = 100;
    private const string _queryEndpoint = "api/v1/queries";

    private static readonly JsonSerializerOptions _options = new() {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IAdminAuthContext _authContext;
    private readonly DaprClient _daprClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DaprTenantQueryService> _logger;
    private readonly AdminServerOptions _serverOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprTenantQueryService"/> class.
    /// </summary>
    public DaprTenantQueryService(
        DaprClient daprClient,
        IHttpClientFactory httpClientFactory,
        IOptions<AdminServerOptions> options,
        IAdminAuthContext authContext,
        ILogger<DaprTenantQueryService> logger) {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(authContext);
        ArgumentNullException.ThrowIfNull(logger);
        _daprClient = daprClient;
        _httpClientFactory = httpClientFactory;
        _serverOptions = options.Value;
        _authContext = authContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TenantDetail?> GetTenantDetailAsync(string tenantId, CancellationToken ct = default) {
        try {
            SubmitQueryResponse response = await SendQueryAsync(
                "tenants", tenantId, "get-tenant",
                null,
                ct).ConfigureAwait(false);

            // Envelope guard MUST run before any payload deserialization (story AC #5):
            // for failed envelopes, payload contents are undefined and constructing models
            // from them produces invalid state and misleading 503 responses.
            ClassifyFailedEnvelope(response, nameof(GetTenantDetailAsync));

            ContractsTenantDetail? detail = response.Payload.Deserialize<ContractsTenantDetail>(_options);
            if (detail is null) {
                return null;
            }

            return new TenantDetail(
                detail.TenantId,
                detail.Name,
                detail.Description,
                MapStatus(detail.Status),
                detail.CreatedAt);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TenantUser>> GetTenantUsersAsync(string tenantId, CancellationToken ct = default) {
        List<TenantUser> allUsers = [];
        string? cursor = null;

        do {
            SubmitQueryResponse response = await SendQueryAsync(
                "tenants", tenantId, "get-tenant-users",
                new { cursor, pageSize = _defaultPageSize },
                ct).ConfigureAwait(false);

            ClassifyFailedEnvelope(response, nameof(GetTenantUsersAsync));

            Hexalith.Tenants.Contracts.Queries.PaginatedResult<ContractsTenantMember>? page =
                response.Payload.Deserialize<Hexalith.Tenants.Contracts.Queries.PaginatedResult<ContractsTenantMember>>(_options);

            if (page?.Items is not null) {
                foreach (ContractsTenantMember m in page.Items) {
                    allUsers.Add(new TenantUser(m.UserId, m.Role.ToString()));
                }
            }

            cursor = page?.HasMore == true ? page.Cursor : null;
        }
        while (cursor is not null);

        return allUsers;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TenantSummary>> ListTenantsAsync(CancellationToken ct = default) {
        List<TenantSummary> allTenants = [];
        string? cursor = null;

        do {
            SubmitQueryResponse response = await SendQueryAsync(
                "tenants", "index", "list-tenants",
                new { cursor, pageSize = _defaultPageSize },
                ct).ConfigureAwait(false);

            // Tenant index queries treat semantic failures (Forbidden / Tenant not found / unknown)
            // as upstream contract errors — never silently flatten to an empty list (story AC #5).
            ClassifyFailedEnvelope(response, nameof(ListTenantsAsync), listSemantics: true);

            Hexalith.Tenants.Contracts.Queries.PaginatedResult<ContractsTenantSummary>? page =
                response.Payload.Deserialize<Hexalith.Tenants.Contracts.Queries.PaginatedResult<ContractsTenantSummary>>(_options);

            if (page?.Items is not null) {
                foreach (ContractsTenantSummary t in page.Items) {
                    allTenants.Add(new TenantSummary(t.TenantId, t.Name, MapStatus(t.Status)));
                }
            }

            cursor = page?.HasMore == true ? page.Cursor : null;
        }
        while (cursor is not null);

        return allTenants;
    }

    private void ClassifyFailedEnvelope(SubmitQueryResponse response, string method, bool listSemantics = false) {
        if (response.Success) {
            return;
        }

        string error = response.ErrorMessage ?? string.Empty;

        if (string.Equals(error, "Forbidden", StringComparison.Ordinal)) {
            // 403 – matches existing AdminTenantsController.QueryFailure HttpRequestException mapping.
            throw new HttpRequestException("Upstream tenant query forbidden.", null, HttpStatusCode.Forbidden);
        }

        if (string.Equals(error, "Tenant not found", StringComparison.Ordinal) && !listSemantics) {
            // Detail: caller's catch translates HttpRequestException(NotFound) to null, controller returns 404.
            // Users: AdminTenantsController.QueryFailure maps HttpRequestException(NotFound) to 404 ProblemDetails.
            throw new HttpRequestException("Tenant not found.", null, HttpStatusCode.NotFound);
        }

        _logger.LogWarning(
            "Tenant query failed envelope from upstream: Method={Method}, ErrorMessage={ErrorMessage}",
            method,
            error);

        // Semantic failure path -> 502 via dedicated typed exception. Must NOT be HttpRequestException
        // with HttpStatusCode.BadGateway because AdminTenantsController.IsServiceUnavailable currently
        // classifies BadGateway as transport 503; story explicitly forbids broadening that helper.
        throw new TenantQueryFailedException(error);
    }

    private static TenantStatusType MapStatus(TenantStatus status) => status switch {
        TenantStatus.Disabled => TenantStatusType.Disabled,
        _ => TenantStatusType.Active,
    };

    private async Task<SubmitQueryResponse> SendQueryAsync(
        string domain,
        string aggregateId,
        string queryType,
        object? payload,
        CancellationToken ct) {
        try {
            HttpRequestMessage request = _daprClient.CreateInvokeMethodRequest(
                HttpMethod.Post, _serverOptions.EventStoreAppId, _queryEndpoint);

            string? token = _authContext.GetToken();
            if (token is not null) {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            request.Content = JsonContent.Create(new SubmitQueryRequest(
                Tenant: "system",
                Domain: domain,
                AggregateId: aggregateId,
                QueryType: queryType,
                Payload: payload is not null ? JsonSerializer.SerializeToElement(payload) : null,
                ProjectionActorType: TenantProjectionRouting.ActorTypeName));

            HttpClient httpClient = _httpClientFactory.CreateClient(_serverOptions.EventStoreAppId);
            HttpResponseMessage httpResponse = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
            await EnsureQueryHttpSuccessAsync(httpResponse, $"{domain}/{queryType}", ct).ConfigureAwait(false);

            SubmitQueryResponse? queryResponse = await httpResponse.Content
                .ReadFromJsonAsync<SubmitQueryResponse>(_options, ct).ConfigureAwait(false);

            return queryResponse ?? throw new InvalidOperationException(
                $"Query response body was null for {domain}/{queryType}.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            throw new TimeoutException(
                $"Query to {_serverOptions.EventStoreAppId} timed out after {_serverOptions.ServiceInvocationTimeoutSeconds}s.");
        }
    }

    private static async Task EnsureQueryHttpSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken ct) {
        if (response.IsSuccessStatusCode) {
            return;
        }

        if (response.StatusCode is HttpStatusCode.InternalServerError or HttpStatusCode.NotImplemented) {
            string upstreamMessage = await ReadUpstreamProblemMessageAsync(response, operation, ct).ConfigureAwait(false);
            throw new TenantQueryFailedException(upstreamMessage);
        }

        _ = response.EnsureSuccessStatusCode();
    }

    private static async Task<string> ReadUpstreamProblemMessageAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken ct) {
        string fallback = $"{operation} returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}.";
        if (response.Content is null) {
            return fallback;
        }

        string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body)) {
            return fallback;
        }

        try {
            using var document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            string? detail = root.TryGetProperty("detail", out JsonElement detailElement)
                && detailElement.ValueKind == JsonValueKind.String
                    ? detailElement.GetString()
                    : null;
            string? title = root.TryGetProperty("title", out JsonElement titleElement)
                && titleElement.ValueKind == JsonValueKind.String
                    ? titleElement.GetString()
                    : null;

            return string.IsNullOrWhiteSpace(detail)
                ? (string.IsNullOrWhiteSpace(title) ? fallback : title)
                : detail;
        }
        catch (JsonException) {
            return body.Length > 512 ? body[..512] : body;
        }
    }
}
