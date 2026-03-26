using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// DAPR-backed implementation of <see cref="IDaprInfrastructureQueryService"/>.
/// Uses DaprClient.GetMetadataAsync() as the sole data source.
/// </summary>
public sealed class DaprInfrastructureQueryService : IDaprInfrastructureQueryService
{
    private readonly DaprClient _daprClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DaprInfrastructureQueryService> _logger;
    private readonly AdminServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprInfrastructureQueryService"/> class.
    /// </summary>
    /// <param name="daprClient">The DAPR client.</param>
    /// <param name="httpClientFactory">The HTTP client factory for remote sidecar calls.</param>
    /// <param name="options">The admin server options.</param>
    /// <param name="logger">The logger.</param>
    public DaprInfrastructureQueryService(
        DaprClient daprClient,
        IHttpClientFactory httpClientFactory,
        IOptions<AdminServerOptions> options,
        ILogger<DaprInfrastructureQueryService> logger)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _daprClient = daprClient;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DaprComponentDetail>> GetComponentsAsync(CancellationToken ct = default)
    {
        DaprMetadata metadata;
        try
        {
            metadata = await _daprClient.GetMetadataAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DAPR sidecar metadata unavailable — cannot list components.");
            return [];
        }

        if (metadata?.Components is null || metadata.Components.Count == 0)
        {
            return [];
        }

        DaprComponentDetail[] components;
        try
        {
            components = metadata.Components
                .Where(c => !string.IsNullOrEmpty(c.Name) && !string.IsNullOrEmpty(c.Type))
                .Select(c => new DaprComponentDetail(
                    c.Name,
                    c.Type,
                    DaprComponentCategoryHelper.FromComponentType(c.Type),
                    c.Version ?? string.Empty,
                    HealthStatus.Healthy,
                    DateTimeOffset.UtcNow,
                    c.Capabilities ?? []))
                .ToArray();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to map DAPR component metadata.");
            return [];
        }

        // Run health probes for state store components in parallel
        using CancellationTokenSource probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        probeCts.CancelAfter(TimeSpan.FromSeconds(3));

        List<Task> probes = [];
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i].Category == DaprComponentCategory.StateStore)
            {
                probes.Add(ProbeStateStoreAsync(components, i, probeCts.Token));
            }
        }

        if (probes.Count > 0)
        {
            try
            {
                await Task.WhenAll(probes).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Probe timeout — mark remaining probed components as Degraded
                _logger.LogWarning("State store health probes timed out after 3 seconds.");
            }
        }

        return components;
    }

    /// <inheritdoc/>
    public async Task<DaprSidecarInfo?> GetSidecarInfoAsync(CancellationToken ct = default)
    {
        try
        {
            DaprMetadata metadata = await _daprClient.GetMetadataAsync(ct).ConfigureAwait(false);
            if (metadata is null)
            {
                return null;
            }

            // DAPR SDK 1.16.1 exposes Id, Components, Actors, Extended.
            // RuntimeVersion, Subscriptions, HttpEndpoints are not available in this SDK version.
            string runtimeVersion = metadata.Extended?.TryGetValue("daprRuntimeVersion", out string? version) == true
                ? version ?? "unknown"
                : "unknown";

            return new DaprSidecarInfo(
                string.IsNullOrWhiteSpace(metadata.Id) ? "unknown" : metadata.Id,
                runtimeVersion,
                metadata.Components?.Count ?? 0,
                0,  // Subscriptions not exposed in SDK 1.16.1
                0); // HttpEndpoints not exposed in SDK 1.16.1
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DAPR sidecar metadata unavailable — cannot get sidecar info.");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<DaprActorRuntimeInfo> GetActorRuntimeInfoAsync(CancellationToken ct = default)
    {
        // DAPR default actor runtime configuration (no custom values are set in this project)
        DaprActorRuntimeConfig config = new(
            IdleTimeout: TimeSpan.FromMinutes(60),
            ScanInterval: TimeSpan.FromSeconds(30),
            DrainOngoingCallTimeout: TimeSpan.FromSeconds(60),
            DrainRebalancedActors: true,
            ReentrancyEnabled: false,
            ReentrancyMaxStackDepth: 32);

        // Try local sidecar first
        List<DaprActorTypeInfo> actorTypes = [];
        bool isRemoteMetadataAvailable = false;

        try
        {
            DaprMetadata metadata = await _daprClient.GetMetadataAsync(ct).ConfigureAwait(false);
            if (metadata?.Actors is not null && metadata.Actors.Count > 0)
            {
                foreach (DaprActorMetadata actor in metadata.Actors)
                {
                    KnownActorTypeDescriptor descriptor = KnownActorTypes.GetDescriptor(actor.Type);
                    actorTypes.Add(new DaprActorTypeInfo(
                        actor.Type,
                        actor.Count,
                        descriptor.Description,
                        descriptor.ActorIdFormat));
                }

                isRemoteMetadataAvailable = true;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local DAPR sidecar metadata unavailable for actor types.");
        }

        // If local sidecar has no actors, try remote EventStore server sidecar
        if (actorTypes.Count == 0 && !string.IsNullOrEmpty(_options.EventStoreDaprHttpEndpoint))
        {
            try
            {
                HttpClient httpClient = _httpClientFactory.CreateClient("DaprSidecar");

                string baseUrl = _options.EventStoreDaprHttpEndpoint.TrimEnd('/');
                using HttpResponseMessage response = await httpClient
                    .GetAsync($"{baseUrl}/v1.0/metadata", ct)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                using JsonDocument doc = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                    cancellationToken: ct).ConfigureAwait(false);

                if (doc.RootElement.TryGetProperty("actors", out JsonElement actorsElement))
                {
                    foreach (JsonElement actorElement in actorsElement.EnumerateArray())
                    {
                        string type = actorElement.GetProperty("type").GetString() ?? string.Empty;
                        int count = actorElement.TryGetProperty("count", out JsonElement countEl)
                            ? countEl.GetInt32()
                            : -1;

                        if (!string.IsNullOrEmpty(type))
                        {
                            KnownActorTypeDescriptor descriptor = KnownActorTypes.GetDescriptor(type);
                            actorTypes.Add(new DaprActorTypeInfo(
                                type,
                                count,
                                descriptor.Description,
                                descriptor.ActorIdFormat));

                            if (!KnownActorTypes.Types.ContainsKey(type))
                            {
                                _logger.LogWarning("Unknown actor type '{ActorType}' detected — update KnownActorTypes map", type);
                            }
                        }
                    }

                    isRemoteMetadataAvailable = true;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Remote DAPR sidecar metadata unavailable at {Endpoint}.", _options.EventStoreDaprHttpEndpoint);
            }
        }

        int totalActive = actorTypes
            .Where(a => a.ActiveCount >= 0)
            .Sum(a => a.ActiveCount);

        return new DaprActorRuntimeInfo(
            actorTypes,
            totalActive,
            config,
            isRemoteMetadataAvailable);
    }

    /// <inheritdoc/>
    public async Task<DaprActorInstanceState?> GetActorInstanceStateAsync(
        string actorType, string actorId, CancellationToken ct = default)
    {
        if (!KnownActorTypes.Types.TryGetValue(actorType, out KnownActorTypeDescriptor? descriptor))
        {
            return null;
        }

        // Create a linked CTS with 5-second timeout BEFORE launching tasks
        // so all tasks observe the timeout token.
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

        List<Task<DaprActorStateEntry>> tasks = [];
        foreach (string stateKey in descriptor.StateKeys)
        {
            string? resolvedKey = KnownActorTypes.ResolveStateKey(stateKey, actorId);
            if (resolvedKey is null)
            {
                // Dynamic key family — report as not-found with the pattern as the display key
                tasks.Add(Task.FromResult(new DaprActorStateEntry(stateKey, null, 0, false)));
                continue;
            }

            tasks.Add(ReadActorStateKeyAsync(actorType, actorId, stateKey, resolvedKey, timeoutCts.Token));
        }

        DaprActorStateEntry[] entries;
        try
        {
            entries = await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Actor state reads timed out after 5 seconds for {ActorType}/{ActorId}.", actorType, actorId);
            entries = tasks
                .Select(t => t.IsCompletedSuccessfully ? t.Result : new DaprActorStateEntry("timeout", null, 0, false))
                .ToArray();
        }

        long totalSize = entries.Sum(e => e.EstimatedSizeBytes);

        return new DaprActorInstanceState(
            actorType,
            actorId,
            entries,
            totalSize,
            DateTimeOffset.UtcNow);
    }

    /// <inheritdoc/>
    public async Task<DaprPubSubOverview> GetPubSubOverviewAsync(CancellationToken ct = default)
    {
        // 1. Get pub/sub components from local sidecar metadata (no health probes — presence = healthy)
        List<DaprComponentDetail> pubSubComponents = [];
        try
        {
            DaprMetadata metadata = await _daprClient.GetMetadataAsync(ct).ConfigureAwait(false);
            if (metadata?.Components is not null)
            {
                foreach (DaprComponentsMetadata c in metadata.Components)
                {
                    if (!string.IsNullOrEmpty(c.Name) && !string.IsNullOrEmpty(c.Type)
                        && DaprComponentCategoryHelper.FromComponentType(c.Type) == DaprComponentCategory.PubSub)
                    {
                        pubSubComponents.Add(new DaprComponentDetail(
                            c.Name,
                            c.Type,
                            DaprComponentCategory.PubSub,
                            c.Version ?? string.Empty,
                            HealthStatus.Healthy,
                            DateTimeOffset.UtcNow,
                            c.Capabilities ?? []));
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DAPR sidecar metadata unavailable — cannot list pub/sub components.");
        }

        // 2. Get subscriptions from remote EventStore server sidecar via HTTP
        List<DaprSubscriptionInfo> subscriptions = [];
        bool isRemoteMetadataAvailable = false;

        if (!string.IsNullOrEmpty(_options.EventStoreDaprHttpEndpoint))
        {
            try
            {
                HttpClient httpClient = _httpClientFactory.CreateClient("DaprSidecar");

                string baseUrl = _options.EventStoreDaprHttpEndpoint.TrimEnd('/');
                using HttpResponseMessage response = await httpClient
                    .GetAsync($"{baseUrl}/v1.0/metadata", ct)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                using JsonDocument doc = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                    cancellationToken: ct).ConfigureAwait(false);

                if (doc.RootElement.TryGetProperty("subscriptions", out JsonElement subscriptionsElement))
                {
                    foreach (JsonElement sub in subscriptionsElement.EnumerateArray())
                    {
                        string? pubsubName = sub.TryGetProperty("pubsubName", out JsonElement pn) ? pn.GetString() : null;
                        string? topic = sub.TryGetProperty("topic", out JsonElement t) ? t.GetString() : null;
                        string? type = sub.TryGetProperty("type", out JsonElement ty) ? ty.GetString() : null;
                        string? deadLetterTopic = sub.TryGetProperty("deadLetterTopic", out JsonElement dlt) ? dlt.GetString() : null;

                        // Extract route from rules.rules[].path
                        string route = "/";
                        if (sub.TryGetProperty("rules", out JsonElement rulesElement)
                            && rulesElement.TryGetProperty("rules", out JsonElement rulesArray))
                        {
                            foreach (JsonElement rule in rulesArray.EnumerateArray())
                            {
                                if (rule.TryGetProperty("path", out JsonElement pathElement))
                                {
                                    string? path = pathElement.GetString();
                                    if (!string.IsNullOrEmpty(path))
                                    {
                                        route = path;
                                        break;
                                    }
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(pubsubName) && !string.IsNullOrEmpty(topic))
                        {
                            subscriptions.Add(new DaprSubscriptionInfo(
                                pubsubName,
                                topic,
                                route,
                                type ?? "UNKNOWN",
                                string.IsNullOrWhiteSpace(deadLetterTopic) ? null : deadLetterTopic));
                        }
                    }
                }

                isRemoteMetadataAvailable = true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Remote DAPR sidecar metadata unavailable at {Endpoint} — subscription data will be empty.", _options.EventStoreDaprHttpEndpoint);
            }
        }

        return new DaprPubSubOverview(pubSubComponents, subscriptions, isRemoteMetadataAvailable);
    }

    // DAPR internal actor state key convention — verify after SDK upgrades
    private static string ComposeActorStateKey(string appId, string actorType, string actorId, string stateKey)
        => $"{appId}||{actorType}||{actorId}||{stateKey}";

    private async Task<DaprActorStateEntry> ReadActorStateKeyAsync(
        string actorType, string actorId, string displayKey, string resolvedKey, CancellationToken ct)
    {
        string composedKey = ComposeActorStateKey(_options.CommandApiAppId, actorType, actorId, resolvedKey);
        try
        {
            string? value = await _daprClient
                .GetStateAsync<string>(_options.StateStoreName, composedKey, cancellationToken: ct)
                .ConfigureAwait(false);

            if (value is null)
            {
                return new DaprActorStateEntry(displayKey, null, 0, false);
            }

            long size = Encoding.UTF8.GetByteCount(value);
            return new DaprActorStateEntry(displayKey, value, size, true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read actor state key '{StateKey}' for {ActorType}/{ActorId}.", displayKey, actorType, actorId);
            return new DaprActorStateEntry(displayKey, null, 0, false);
        }
    }

    private async Task ProbeStateStoreAsync(
        DaprComponentDetail[] components,
        int index,
        CancellationToken ct)
    {
        DaprComponentDetail component = components[index];
        try
        {
            _ = await _daprClient
                .GetStateAsync<string>(component.ComponentName, "admin:dapr-probe", cancellationToken: ct)
                .ConfigureAwait(false);

            // Success (null return = key missing, but store responded) → Healthy
        }
        catch (OperationCanceledException)
        {
            // Probe timed out or was cancelled — mark as Degraded (inconclusive)
            components[index] = component with { Status = HealthStatus.Degraded, LastCheckUtc = DateTimeOffset.UtcNow };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "State store probe failed for {ComponentName}.", component.ComponentName);
            components[index] = component with { Status = HealthStatus.Unhealthy, LastCheckUtc = DateTimeOffset.UtcNow };
        }
    }
}
