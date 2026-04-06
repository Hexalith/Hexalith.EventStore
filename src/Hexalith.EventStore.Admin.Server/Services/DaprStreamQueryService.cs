using System.Net.Http.Headers;
using System.Net.Http.Json;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Commands;
using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// DAPR-backed implementation of <see cref="IStreamQueryService"/>.
/// Event data reads delegate to EventStore via InvokeMethodAsync because
/// actor state uses a different key namespace not accessible via plain GetStateAsync.
/// </summary>
public sealed class DaprStreamQueryService : IStreamQueryService {
    private readonly IAdminAuthContext _authContext;
    private readonly DaprClient _daprClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DaprStreamQueryService> _logger;
    private readonly AdminServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprStreamQueryService"/> class.
    /// </summary>
    /// <param name="daprClient">The DAPR client.</param>
    /// <param name="httpClientFactory">The HTTP client factory for DAPR service invocation.</param>
    /// <param name="options">The admin server options.</param>
    /// <param name="authContext">The admin auth context for JWT forwarding.</param>
    /// <param name="logger">The logger.</param>
    public DaprStreamQueryService(
        DaprClient daprClient,
        IHttpClientFactory httpClientFactory,
        IOptions<AdminServerOptions> options,
        IAdminAuthContext authContext,
        ILogger<DaprStreamQueryService> logger) {
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
    public async Task<PagedResult<CommandSummary>> GetRecentCommandsAsync(
        string? tenantId,
        string? status,
        string? commandType,
        int count = 1000,
        CancellationToken ct = default) {
        string endpoint = "api/v1/admin/streams/commands";
        var queryParams = new List<string> { $"count={count}" };
        if (!string.IsNullOrWhiteSpace(tenantId)) {
            queryParams.Add($"tenantId={Uri.EscapeDataString(tenantId)}");
        }

        if (!string.IsNullOrWhiteSpace(status)) {
            queryParams.Add($"status={Uri.EscapeDataString(status)}");
        }

        if (!string.IsNullOrWhiteSpace(commandType)) {
            queryParams.Add($"commandType={Uri.EscapeDataString(commandType)}");
        }

        endpoint += "?" + string.Join("&", queryParams);

        try {
            PagedResult<CommandSummary>? result = await InvokeEventStoreAsync<PagedResult<CommandSummary>>(
                HttpMethod.Get, endpoint, ct).ConfigureAwait(false);
            return result ?? new PagedResult<CommandSummary>([], 0, null);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to get recent commands via DAPR service invocation to '{AppId}'.", _options.EventStoreAppId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<PagedResult<StreamSummary>> GetRecentlyActiveStreamsAsync(
        string? tenantId,
        string? domain,
        int count = 1000,
        CancellationToken ct = default) {
        string indexKey = $"admin:stream-activity:{tenantId ?? "all"}";
        try {
            List<StreamSummary>? result = await _daprClient
                .GetStateAsync<List<StreamSummary>>(_options.StateStoreName, indexKey, cancellationToken: ct)
                .ConfigureAwait(false);

            if (result is null) {
                _logger.LogWarning("Admin index '{IndexKey}' not found. Index population requires admin projection setup.", indexKey);
                return new PagedResult<StreamSummary>([], 0, null);
            }

            IReadOnlyList<StreamSummary> filtered = domain is null
                ? result
                : result.Where(s => s.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase)).ToList();

            IReadOnlyList<StreamSummary> page = filtered
                .OrderByDescending(s => s.LastActivityUtc)
                .Take(count)
                .ToList();
            return new PagedResult<StreamSummary>(page, filtered.Count, null);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to read stream activity index '{IndexKey}' from state store '{StateStore}'.", indexKey, _options.StateStoreName);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<PagedResult<TimelineEntry>> GetStreamTimelineAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long? fromSequence,
        long? toSequence,
        int count = 100,
        CancellationToken ct = default) {
        int maxEvents = _options.MaxTimelineEvents;
        count = Math.Clamp(count, 1, maxEvents);
        if (fromSequence.HasValue && toSequence.HasValue
            && toSequence.Value > fromSequence.Value + maxEvents) {
            toSequence = fromSequence.Value + maxEvents;
        }

        string endpoint = $"api/v1/admin/streams/{E(tenantId)}/{E(domain)}/{E(aggregateId)}/timeline";
        string query = BuildQueryString(fromSequence, toSequence, count);
        if (query.Length > 0) {
            endpoint += "?" + query;
        }

        try {
            PagedResult<TimelineEntry>? result = await InvokeEventStoreAsync<PagedResult<TimelineEntry>>(
                HttpMethod.Get, endpoint, ct).ConfigureAwait(false);
            return result ?? new PagedResult<TimelineEntry>([], 0, null);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to get stream timeline for {TenantId}/{Domain}/{AggregateId} via DAPR service invocation to '{AppId}'.", tenantId, domain, aggregateId, _options.EventStoreAppId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<AggregateStateSnapshot> GetAggregateStateAtPositionAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long sequenceNumber,
        CancellationToken ct = default) {
        string endpoint = $"api/v1/admin/streams/{E(tenantId)}/{E(domain)}/{E(aggregateId)}/state?at={sequenceNumber}";
        try {
            AggregateStateSnapshot? result = await InvokeEventStoreAsync<AggregateStateSnapshot>(
                HttpMethod.Get, endpoint, ct).ConfigureAwait(false);
            return result ?? CreateEmptyAggregateStateSnapshot(tenantId, domain, aggregateId, sequenceNumber, "not-found");
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to get aggregate state at position {Sequence} for {TenantId}/{Domain}/{AggregateId} via DAPR service invocation to '{AppId}'.", sequenceNumber, tenantId, domain, aggregateId, _options.EventStoreAppId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<AggregateStateDiff> DiffAggregateStateAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long fromSequence,
        long toSequence,
        CancellationToken ct = default) {
        string endpoint = $"api/v1/admin/streams/{E(tenantId)}/{E(domain)}/{E(aggregateId)}/diff?from={fromSequence}&to={toSequence}";
        try {
            AggregateStateDiff? result = await InvokeEventStoreAsync<AggregateStateDiff>(
                HttpMethod.Get, endpoint, ct).ConfigureAwait(false);
            return result ?? new AggregateStateDiff(fromSequence, toSequence, []);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to diff aggregate state for {TenantId}/{Domain}/{AggregateId} via DAPR service invocation to '{AppId}'.", tenantId, domain, aggregateId, _options.EventStoreAppId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<AggregateBlameView> GetAggregateBlameAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long? atSequence,
        CancellationToken ct = default) {
        string endpoint = $"api/v1/admin/streams/{E(tenantId)}/{E(domain)}/{E(aggregateId)}/blame";
        var queryParams = new List<string>();
        if (atSequence.HasValue) {
            queryParams.Add($"at={atSequence.Value}");
        }

        queryParams.Add($"maxEvents={_options.MaxBlameEvents}");
        queryParams.Add($"maxFields={_options.MaxBlameFields}");
        endpoint += "?" + string.Join("&", queryParams);

        try {
            // Use 30-second timeout for blame (longer than default because blame replays the entire event stream)
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            AggregateBlameView? result = await InvokeEventStoreAsync<AggregateBlameView>(
                HttpMethod.Get, endpoint, cts.Token).ConfigureAwait(false);
            return result ?? new AggregateBlameView(tenantId, domain, aggregateId, atSequence ?? 0, DateTimeOffset.MinValue, [], false, false);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to compute blame for {TenantId}/{Domain}/{AggregateId}.", tenantId, domain, aggregateId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<EventStepFrame> GetEventStepFrameAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long sequenceNumber,
        CancellationToken ct = default) {
        if (sequenceNumber < 1) {
            throw new ArgumentException("sequenceNumber must be >= 1.");
        }

        string endpoint = $"api/v1/admin/streams/{E(tenantId)}/{E(domain)}/{E(aggregateId)}/step?at={sequenceNumber}";

        try {
            // Use 30-second timeout (same as blame — single state reconstruction + diff is comparable workload)
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            EventStepFrame? result = await InvokeEventStoreAsync<EventStepFrame>(
                HttpMethod.Get, endpoint, cts.Token).ConfigureAwait(false);
            return result ?? new EventStepFrame(tenantId, domain, aggregateId, sequenceNumber, string.Empty, DateTimeOffset.MinValue, string.Empty, string.Empty, string.Empty, "{}", "{}", [], 0);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to get event step frame for {TenantId}/{Domain}/{AggregateId} at {SequenceNumber}.", tenantId, domain, aggregateId, sequenceNumber);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<BisectResult> BisectAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long goodSequence,
        long badSequence,
        IReadOnlyList<string>? fieldPaths,
        CancellationToken ct = default) {
        if (goodSequence < 0) {
            throw new ArgumentException("goodSequence must be >= 0.");
        }

        if (badSequence < 0) {
            throw new ArgumentException("badSequence must be >= 0.");
        }

        if (goodSequence >= badSequence) {
            throw new ArgumentException("goodSequence must be less than badSequence.");
        }

        string endpoint = $"api/v1/admin/streams/{E(tenantId)}/{E(domain)}/{E(aggregateId)}/bisect";
        var queryParams = new List<string>
        {
            $"good={goodSequence}",
            $"bad={badSequence}",
            $"maxSteps={_options.MaxBisectSteps}",
            $"maxFields={_options.MaxBisectFields}",
        };

        if (fieldPaths is { Count: > 0 }) {
            queryParams.Add($"fields={Uri.EscapeDataString(string.Join(",", fieldPaths))}");
        }

        endpoint += "?" + string.Join("&", queryParams);

        try {
            // Use 60-second timeout for bisect (longer than blame's 30s because bisect performs O(log N) state reconstructions)
            BisectResult? result = await InvokeEventStoreAsync<BisectResult>(
                HttpMethod.Get, endpoint, ct, timeoutSeconds: 60).ConfigureAwait(false);
            return result ?? new BisectResult(tenantId, domain, aggregateId, goodSequence, badSequence, DateTimeOffset.MinValue, string.Empty, string.Empty, string.Empty, [], [], [], 0, false);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to bisect aggregate state for {TenantId}/{Domain}/{AggregateId}.", tenantId, domain, aggregateId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<EventDetail> GetEventDetailAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long sequenceNumber,
        CancellationToken ct = default) {
        string endpoint = $"api/v1/admin/streams/{E(tenantId)}/{E(domain)}/{E(aggregateId)}/events/{sequenceNumber}";
        try {
            EventDetail? result = await InvokeEventStoreAsync<EventDetail>(
                HttpMethod.Get, endpoint, ct).ConfigureAwait(false);
            return result ?? CreateEmptyEventDetail(tenantId, domain, aggregateId, sequenceNumber, "not-found");
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to get event detail at {Sequence} for {TenantId}/{Domain}/{AggregateId} via DAPR service invocation to '{AppId}'.", sequenceNumber, tenantId, domain, aggregateId, _options.EventStoreAppId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<SandboxResult?> SandboxCommandAsync(
        string tenantId,
        string domain,
        string aggregateId,
        SandboxCommandRequest request,
        CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.CommandType)) {
            throw new ArgumentException("CommandType is required.");
        }

        if (request.AtSequence.HasValue && request.AtSequence.Value < 0) {
            throw new ArgumentException("AtSequence must be >= 0 when provided.");
        }

        string endpoint = $"api/v1/admin/streams/{E(tenantId)}/{E(domain)}/{E(aggregateId)}/sandbox";

        try {
            SandboxResult? result = await InvokeEventStoreAsync<SandboxCommandRequest, SandboxResult>(
                endpoint, request, ct).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to execute sandbox command for {TenantId}/{Domain}/{AggregateId}.", tenantId, domain, aggregateId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<CausationChain> TraceCausationChainAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long sequenceNumber,
        CancellationToken ct = default) {
        string endpoint = $"api/v1/admin/streams/{E(tenantId)}/{E(domain)}/{E(aggregateId)}/causation?at={sequenceNumber}";
        try {
            CausationChain? result = await InvokeEventStoreAsync<CausationChain>(
                HttpMethod.Get, endpoint, ct).ConfigureAwait(false);
            return result ?? CreateEmptyCausationChain("not-found");
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to trace causation chain at {Sequence} for {TenantId}/{Domain}/{AggregateId} via DAPR service invocation to '{AppId}'.", sequenceNumber, tenantId, domain, aggregateId, _options.EventStoreAppId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<CorrelationTraceMap> GetCorrelationTraceMapAsync(
        string tenantId,
        string correlationId,
        string? domain,
        string? aggregateId,
        CancellationToken ct = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        string endpoint = $"api/v1/admin/traces/{E(tenantId)}/{E(correlationId)}";
        var queryParams = new List<string>();
        if (!string.IsNullOrEmpty(domain)) {
            queryParams.Add($"domain={E(domain)}");
        }

        if (!string.IsNullOrEmpty(aggregateId)) {
            queryParams.Add($"aggregateId={E(aggregateId)}");
        }

        if (queryParams.Count > 0) {
            endpoint += "?" + string.Join("&", queryParams);
        }

        try {
            // Use 30-second timeout (trace map scans potentially large event streams)
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            CorrelationTraceMap? result = await InvokeEventStoreAsync<CorrelationTraceMap>(
                HttpMethod.Get, endpoint, cts.Token).ConfigureAwait(false);
            return result ?? new CorrelationTraceMap(correlationId, tenantId, string.Empty, string.Empty, string.Empty, "Unknown", null, null, null, null, [], [], null, "Unable to retrieve trace map from EventStore.", null, 0, false, null);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to get correlation trace map for {TenantId}/{CorrelationId} via DAPR service invocation to '{AppId}'.", tenantId, correlationId, _options.EventStoreAppId);
            throw;
        }
    }

    private static string E(string value) => Uri.EscapeDataString(value);

    private static IEnumerable<CommandSummary> ApplyCommandStatusFilter(
        IEnumerable<CommandSummary> commands,
        string? status) {
        if (string.IsNullOrWhiteSpace(status)) {
            return commands;
        }

        string normalizedStatus = status.Trim().ToLowerInvariant();
        return normalizedStatus switch {
            "completed" => commands.Where(c => c.Status == CommandStatus.Completed),
            "processing" => commands.Where(c => c.Status is CommandStatus.Received
                or CommandStatus.Processing
                or CommandStatus.EventsStored
                or CommandStatus.EventsPublished),
            "rejected" => commands.Where(c => c.Status == CommandStatus.Rejected),
            "failed" => commands.Where(c => c.Status is CommandStatus.PublishFailed
                or CommandStatus.TimedOut),
            _ when Enum.TryParse(status.Trim(), ignoreCase: true, out CommandStatus parsedStatus)
                => commands.Where(c => c.Status == parsedStatus),
            _ => commands,
        };
    }

    private static AggregateStateSnapshot CreateEmptyAggregateStateSnapshot(
        string tenantId,
        string domain,
        string aggregateId,
        long sequenceNumber,
        string status)
        => new(tenantId, domain, aggregateId, sequenceNumber, DateTimeOffset.UnixEpoch, $"{{\"status\":\"{status}\"}}");

    private static EventDetail CreateEmptyEventDetail(
        string tenantId,
        string domain,
        string aggregateId,
        long sequenceNumber,
        string status)
        => new(tenantId, domain, aggregateId, sequenceNumber, $"admin.event.{status}", DateTimeOffset.UnixEpoch, $"admin-{status}", null, null, $"{{\"status\":\"{status}\"}}");

    private static CausationChain CreateEmptyCausationChain(string status)
        => new($"admin.command.{status}", $"admin-{status}", $"admin-{status}", null, [], []);

    private async Task<TResponse?> InvokeEventStoreAsync<TResponse>(
        HttpMethod method,
        string endpoint,
        CancellationToken ct,
        int? timeoutSeconds = null) {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds ?? _options.ServiceInvocationTimeoutSeconds));

        using HttpRequestMessage request = _daprClient.CreateInvokeMethodRequest(
            method, _options.EventStoreAppId, endpoint)
            ?? new HttpRequestMessage(method, endpoint);

        string? token = _authContext.GetToken();
        if (token is not null) {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        HttpClient httpClient = _httpClientFactory.CreateClient();
        using HttpResponseMessage httpResponse = await httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
        httpResponse.EnsureSuccessStatusCode();
        return await httpResponse.Content.ReadFromJsonAsync<TResponse>(cts.Token).ConfigureAwait(false);
    }

    private async Task<TResponse?> InvokeEventStoreAsync<TRequest, TResponse>(
        string endpoint,
        TRequest body,
        CancellationToken ct,
        int? timeoutSeconds = null) {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds ?? _options.ServiceInvocationTimeoutSeconds));

        using HttpRequestMessage request = _daprClient.CreateInvokeMethodRequest(
            HttpMethod.Post, _options.EventStoreAppId, endpoint)
            ?? new HttpRequestMessage(HttpMethod.Post, endpoint);

        string? token = _authContext.GetToken();
        if (token is not null) {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        request.Content = JsonContent.Create(body);

        HttpClient httpClient2 = _httpClientFactory.CreateClient();
        using HttpResponseMessage httpResponse2 = await httpClient2.SendAsync(request, cts.Token).ConfigureAwait(false);
        httpResponse2.EnsureSuccessStatusCode();
        return await httpResponse2.Content.ReadFromJsonAsync<TResponse>(cts.Token).ConfigureAwait(false);
    }

    private static string BuildQueryString(long? from, long? to, int count) {
        var parts = new List<string>();
        if (from.HasValue) {
            parts.Add($"from={from.Value}");
        }

        if (to.HasValue) {
            parts.Add($"to={to.Value}");
        }

        parts.Add($"count={count}");
        return string.Join("&", parts);
    }
}
