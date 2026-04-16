using System.Text;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using YamlDotNet.RepresentationModel;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// DAPR-backed implementation of <see cref="IDaprInfrastructureQueryService"/>.
/// Uses DaprClient.GetMetadataAsync() as the sole data source.
/// </summary>
public sealed class DaprInfrastructureQueryService : IDaprInfrastructureQueryService {
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
        ILogger<DaprInfrastructureQueryService> logger) {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _daprClient = daprClient;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
        _logger.LogInformation(
            "EventStoreDaprHttpEndpoint={Endpoint}",
            string.IsNullOrWhiteSpace(_options.EventStoreDaprHttpEndpoint)
                ? "<not configured — remote sidecar metadata disabled>"
                : _options.EventStoreDaprHttpEndpoint);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DaprComponentDetail>> GetComponentsAsync(CancellationToken ct = default) {
        DaprMetadata metadata = await _daprClient.GetMetadataAsync(ct).ConfigureAwait(false);

        if (metadata?.Components is null || metadata.Components.Count == 0) {
            return [];
        }

        DaprComponentDetail[] components = metadata.Components
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

        // Run health probes for state store components in parallel
        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        probeCts.CancelAfter(TimeSpan.FromSeconds(3));

        List<Task> probes = [];
        for (int i = 0; i < components.Length; i++) {
            if (components[i].Category == DaprComponentCategory.StateStore) {
                probes.Add(ProbeStateStoreAsync(components, i, probeCts.Token));
            }
        }

        if (probes.Count > 0) {
            try {
                await Task.WhenAll(probes).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
                // Probe timeout — mark remaining probed components as Degraded
                _logger.LogWarning("State store health probes timed out after 3 seconds.");
            }
        }

        return components;
    }

    /// <inheritdoc/>
    public async Task<DaprSidecarInfo?> GetSidecarInfoAsync(CancellationToken ct = default) {
        DaprMetadata metadata = await _daprClient.GetMetadataAsync(ct).ConfigureAwait(false);
        if (metadata is null) {
            return null;
        }

        // The DAPR SDK metadata model exposes Id, Components, Actors, Extended for the local
        // sidecar only. Subscriptions and HttpEndpoints live on the remote EventStore sidecar
        // (the eventstore-admin sidecar references the state store only — it has no pub/sub
        // subscriptions of its own), so we fetch them via HTTP from /v1.0/metadata, the same
        // pattern used by GetPubSubOverviewAsync and GetActorRuntimeInfoAsync.
        string runtimeVersion = metadata.Extended?.TryGetValue("daprRuntimeVersion", out string? version) == true
            ? version ?? "unknown"
            : "unknown";

        int subscriptionCount = 0;
        int httpEndpointCount = 0;
        bool remoteFetchSucceeded = false;

        if (string.IsNullOrWhiteSpace(_options.EventStoreDaprHttpEndpoint)) {
            _logger.LogDebug("Skipping remote EventStore sidecar metadata query for sidecar info: endpoint not configured.");
        }
        else {
            try {
                HttpClient httpClient = _httpClientFactory.CreateClient("DaprSidecar");

                string baseUrl = _options.EventStoreDaprHttpEndpoint.TrimEnd('/');
                using HttpResponseMessage response = await httpClient
                    .GetAsync($"{baseUrl}/v1.0/metadata", ct)
                    .ConfigureAwait(false);
                _ = response.EnsureSuccessStatusCode();

                using JsonDocument doc = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                    cancellationToken: ct).ConfigureAwait(false);

                if (doc.RootElement.TryGetProperty("subscriptions", out JsonElement subscriptionsElement)
                    && subscriptionsElement.ValueKind == JsonValueKind.Array) {
                    subscriptionCount = subscriptionsElement.GetArrayLength();
                }

                if (doc.RootElement.TryGetProperty("httpEndpoints", out JsonElement httpEndpointsElement)
                    && httpEndpointsElement.ValueKind == JsonValueKind.Array) {
                    httpEndpointCount = httpEndpointsElement.GetArrayLength();
                }

                remoteFetchSucceeded = true;
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) {
                _logger.LogWarning(
                    ex,
                    "Remote DAPR sidecar metadata unavailable at {Endpoint} for sidecar info. ExceptionType={ExceptionType}.",
                    _options.EventStoreDaprHttpEndpoint,
                    ex.GetType().Name);
            }
        }

        string? remoteEndpoint = string.IsNullOrWhiteSpace(_options.EventStoreDaprHttpEndpoint)
            ? null
            : _options.EventStoreDaprHttpEndpoint;

        RemoteMetadataStatus status = remoteEndpoint is null
            ? RemoteMetadataStatus.NotConfigured
            : remoteFetchSucceeded
                ? RemoteMetadataStatus.Available
                : RemoteMetadataStatus.Unreachable;

        return new DaprSidecarInfo(
            string.IsNullOrWhiteSpace(metadata.Id) ? "unknown" : metadata.Id,
            runtimeVersion,
            metadata.Components?.Count ?? 0,
            subscriptionCount,
            httpEndpointCount,
            status,
            remoteEndpoint);
    }

    /// <inheritdoc/>
    public async Task<DaprActorRuntimeInfo> GetActorRuntimeInfoAsync(CancellationToken ct = default) {
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

        try {
            DaprMetadata metadata = await _daprClient.GetMetadataAsync(ct).ConfigureAwait(false);
            if (metadata?.Actors is not null && metadata.Actors.Count > 0) {
                foreach (DaprActorMetadata actor in metadata.Actors) {
                    KnownActorTypeDescriptor descriptor = KnownActorTypes.GetDescriptor(actor.Type);
                    actorTypes.Add(new DaprActorTypeInfo(
                        actor.Type,
                        actor.Count,
                        descriptor.Description,
                        descriptor.ActorIdFormat));
                }
            }
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Local DAPR sidecar metadata unavailable for actor types.");
        }

        // If local sidecar has no actors, try remote EventStore server sidecar
        bool remoteFetchSucceeded = false;
        if (actorTypes.Count == 0) {
            if (string.IsNullOrWhiteSpace(_options.EventStoreDaprHttpEndpoint)) {
                _logger.LogDebug("Skipping remote EventStore sidecar metadata query: endpoint not configured.");
            }
            else {
                try {
                    HttpClient httpClient = _httpClientFactory.CreateClient("DaprSidecar");

                    string baseUrl = _options.EventStoreDaprHttpEndpoint.TrimEnd('/');
                    using HttpResponseMessage response = await httpClient
                        .GetAsync($"{baseUrl}/v1.0/metadata", ct)
                        .ConfigureAwait(false);
                    _ = response.EnsureSuccessStatusCode();

                    using JsonDocument doc = await JsonDocument.ParseAsync(
                        await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                        cancellationToken: ct).ConfigureAwait(false);

                    if (doc.RootElement.TryGetProperty("actors", out JsonElement actorsElement)) {
                        foreach (JsonElement actorElement in actorsElement.EnumerateArray()) {
                            string type = actorElement.GetProperty("type").GetString() ?? string.Empty;
                            int count = actorElement.TryGetProperty("count", out JsonElement countEl)
                                ? countEl.GetInt32()
                                : -1;

                            if (!string.IsNullOrEmpty(type)) {
                                KnownActorTypeDescriptor descriptor = KnownActorTypes.GetDescriptor(type);
                                actorTypes.Add(new DaprActorTypeInfo(
                                    type,
                                    count,
                                    descriptor.Description,
                                    descriptor.ActorIdFormat));

                                if (!KnownActorTypes.Types.ContainsKey(type)) {
                                    _logger.LogWarning("Unknown actor type '{ActorType}' detected — update KnownActorTypes map", type);
                                }
                            }
                        }

                        remoteFetchSucceeded = true;
                    }
                }
                catch (OperationCanceledException) {
                    throw;
                }
                catch (Exception ex) {
                    _logger.LogWarning(
                        ex,
                        "Remote DAPR sidecar metadata unavailable at {Endpoint}. ExceptionType={ExceptionType}. Check whether DAPR sidecar for 'eventstore' is running on that port (port conflicts on 3501 cause silent fallback).",
                        _options.EventStoreDaprHttpEndpoint,
                        ex.GetType().Name);
                }
            }
        }

        string? remoteEndpoint = string.IsNullOrWhiteSpace(_options.EventStoreDaprHttpEndpoint)
            ? null
            : _options.EventStoreDaprHttpEndpoint;

        RemoteMetadataStatus status = remoteEndpoint is null
            ? RemoteMetadataStatus.NotConfigured
            : remoteFetchSucceeded
                ? RemoteMetadataStatus.Available
                : RemoteMetadataStatus.Unreachable;

        int totalActive = actorTypes
            .Where(a => a.ActiveCount >= 0)
            .Sum(a => a.ActiveCount);

        return new DaprActorRuntimeInfo(
            actorTypes,
            totalActive,
            config,
            status,
            remoteEndpoint);
    }

    /// <inheritdoc/>
    public async Task<DaprActorInstanceState?> GetActorInstanceStateAsync(
        string actorType, string actorId, CancellationToken ct = default) {
        if (!KnownActorTypes.Types.TryGetValue(actorType, out KnownActorTypeDescriptor? descriptor)) {
            return null;
        }

        // Create a linked CTS with 5-second timeout BEFORE launching tasks
        // so all tasks observe the timeout token.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

        List<Task<DaprActorStateEntry>> tasks = [];
        foreach (string stateKey in descriptor.StateKeys) {
            string? resolvedKey = KnownActorTypes.ResolveStateKey(stateKey, actorId);
            if (resolvedKey is null) {
                // Dynamic key family — report as not-found with the pattern as the display key
                tasks.Add(Task.FromResult(new DaprActorStateEntry(stateKey, null, 0, false)));
                continue;
            }

            tasks.Add(ReadActorStateKeyAsync(actorType, actorId, stateKey, resolvedKey, timeoutCts.Token));
        }

        DaprActorStateEntry[] entries;
        try {
            entries = await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
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
    public async Task<DaprPubSubOverview> GetPubSubOverviewAsync(CancellationToken ct = default) {
        // Query the EventStore sidecar's metadata once to get BOTH pub/sub components AND
        // subscriptions. We deliberately do NOT read the local Admin.Server sidecar's components
        // here, because the 'eventstore-admin' sidecar (HexalithEventStoreExtensions.cs:97-102)
        // is wired with state-store references only — it never sees the pub/sub component.
        // The pub/sub component lives on the 'eventstore' sidecar, queryable via
        // {EventStoreDaprHttpEndpoint}/v1.0/metadata.
        List<DaprComponentDetail> pubSubComponents = [];
        List<DaprSubscriptionInfo> subscriptions = [];
        bool remoteFetchSucceeded = false;

        if (string.IsNullOrWhiteSpace(_options.EventStoreDaprHttpEndpoint)) {
            _logger.LogDebug("Skipping remote EventStore sidecar metadata query: endpoint not configured.");
        }
        else {
            try {
                HttpClient httpClient = _httpClientFactory.CreateClient("DaprSidecar");

                string baseUrl = _options.EventStoreDaprHttpEndpoint.TrimEnd('/');
                using HttpResponseMessage response = await httpClient
                    .GetAsync($"{baseUrl}/v1.0/metadata", ct)
                    .ConfigureAwait(false);
                _ = response.EnsureSuccessStatusCode();

                using JsonDocument doc = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                    cancellationToken: ct).ConfigureAwait(false);

                // Extract pub/sub components from the remote sidecar's components array.
                if (doc.RootElement.TryGetProperty("components", out JsonElement componentsElement)
                    && componentsElement.ValueKind == JsonValueKind.Array) {
                    foreach (JsonElement comp in componentsElement.EnumerateArray()) {
                        string? name = comp.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
                        string? compType = comp.TryGetProperty("type", out JsonElement tp) ? tp.GetString() : null;
                        string? version = comp.TryGetProperty("version", out JsonElement v) ? v.GetString() : null;

                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(compType)
                            || DaprComponentCategoryHelper.FromComponentType(compType) != DaprComponentCategory.PubSub) {
                            continue;
                        }

                        List<string> capabilities = [];
                        if (comp.TryGetProperty("capabilities", out JsonElement capsEl)
                            && capsEl.ValueKind == JsonValueKind.Array) {
                            foreach (JsonElement cap in capsEl.EnumerateArray()) {
                                string? capValue = cap.GetString();
                                if (!string.IsNullOrEmpty(capValue)) {
                                    capabilities.Add(capValue);
                                }
                            }
                        }

                        pubSubComponents.Add(new DaprComponentDetail(
                            name,
                            compType,
                            DaprComponentCategory.PubSub,
                            version ?? string.Empty,
                            HealthStatus.Healthy,
                            DateTimeOffset.UtcNow,
                            capabilities));
                    }
                }

                // Extract subscriptions from the same payload.
                if (doc.RootElement.TryGetProperty("subscriptions", out JsonElement subscriptionsElement)
                    && subscriptionsElement.ValueKind == JsonValueKind.Array) {
                    foreach (JsonElement sub in subscriptionsElement.EnumerateArray()) {
                        string? pubsubName = sub.TryGetProperty("pubsubName", out JsonElement pn) ? pn.GetString() : null;
                        string? topic = sub.TryGetProperty("topic", out JsonElement t) ? t.GetString() : null;
                        string? type = sub.TryGetProperty("type", out JsonElement ty) ? ty.GetString() : null;
                        string? deadLetterTopic = sub.TryGetProperty("deadLetterTopic", out JsonElement dlt) ? dlt.GetString() : null;

                        // Extract route from rules[].path. DAPR /v1.0/metadata returns
                        // 'rules' as a direct array of {match, path} objects. We also tolerate
                        // a legacy wrapped form '{"rules": {"rules": [...]}}' for backward
                        // compatibility with prior test fixtures.
                        string route = "/";
                        if (sub.TryGetProperty("rules", out JsonElement rulesElement)) {
                            JsonElement rulesArray = rulesElement.ValueKind == JsonValueKind.Object
                                && rulesElement.TryGetProperty("rules", out JsonElement nestedRules)
                                && nestedRules.ValueKind == JsonValueKind.Array
                                    ? nestedRules
                                    : rulesElement;

                            if (rulesArray.ValueKind == JsonValueKind.Array) {
                                foreach (JsonElement rule in rulesArray.EnumerateArray()) {
                                    if (rule.ValueKind == JsonValueKind.Object
                                        && rule.TryGetProperty("path", out JsonElement pathElement)) {
                                        string? path = pathElement.GetString();
                                        if (!string.IsNullOrEmpty(path)) {
                                            route = path;
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(pubsubName) && !string.IsNullOrEmpty(topic)) {
                            subscriptions.Add(new DaprSubscriptionInfo(
                                pubsubName,
                                topic,
                                route,
                                type ?? "UNKNOWN",
                                string.IsNullOrWhiteSpace(deadLetterTopic) ? null : deadLetterTopic));
                        }
                    }
                }

                remoteFetchSucceeded = true;
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) {
                _logger.LogWarning(
                    ex,
                    "Remote DAPR sidecar metadata unavailable at {Endpoint}. ExceptionType={ExceptionType}. Check whether DAPR sidecar for 'eventstore' is running on that port (port conflicts on 3501 cause silent fallback).",
                    _options.EventStoreDaprHttpEndpoint,
                    ex.GetType().Name);
            }
        }

        string? remoteEndpoint = string.IsNullOrWhiteSpace(_options.EventStoreDaprHttpEndpoint)
            ? null
            : _options.EventStoreDaprHttpEndpoint;

        RemoteMetadataStatus status = remoteEndpoint is null
            ? RemoteMetadataStatus.NotConfigured
            : remoteFetchSucceeded
                ? RemoteMetadataStatus.Available
                : RemoteMetadataStatus.Unreachable;

        return new DaprPubSubOverview(pubSubComponents, subscriptions, status, remoteEndpoint);
    }

    // DAPR internal actor state key convention — verify after SDK upgrades
    private static string ComposeActorStateKey(string appId, string actorType, string actorId, string stateKey)
        => $"{appId}||{actorType}||{actorId}||{stateKey}";

    private async Task<DaprActorStateEntry> ReadActorStateKeyAsync(
        string actorType, string actorId, string displayKey, string resolvedKey, CancellationToken ct) {
        string composedKey = ComposeActorStateKey(_options.EventStoreAppId, actorType, actorId, resolvedKey);
        try {
            string? value = await _daprClient
                .GetStateAsync<string>(_options.StateStoreName, composedKey, cancellationToken: ct)
                .ConfigureAwait(false);

            if (value is null) {
                return new DaprActorStateEntry(displayKey, null, 0, false);
            }

            long size = Encoding.UTF8.GetByteCount(value);
            return new DaprActorStateEntry(displayKey, value, size, true);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to read actor state key '{StateKey}' for {ActorType}/{ActorId}.", displayKey, actorType, actorId);
            return new DaprActorStateEntry(displayKey, null, 0, false);
        }
    }

    /// <inheritdoc/>
    public async Task<DaprResiliencySpec> GetResiliencySpecAsync(CancellationToken ct = default) {
        string? configPath = _options.ResiliencyConfigPath;
        if (string.IsNullOrWhiteSpace(configPath)) {
            return DaprResiliencySpec.Unavailable;
        }

        string resolvedPath = Path.IsPathRooted(configPath)
            ? configPath
            : Path.GetFullPath(configPath, AppContext.BaseDirectory);

        string yamlContent;
        try {
            yamlContent = await File.ReadAllTextAsync(resolvedPath, ct).ConfigureAwait(false);
        }
        catch (FileNotFoundException) {
            _logger.LogWarning("Resiliency config file not found: {Path}", resolvedPath);
            return DaprResiliencySpec.NotFound(resolvedPath);
        }
        catch (UnauthorizedAccessException ex) {
            _logger.LogError(ex, "Cannot read resiliency config (access denied or path is a directory): {Path}", resolvedPath);
            return DaprResiliencySpec.ReadError(resolvedPath, ex.Message);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to read resiliency config: {Path}", resolvedPath);
            return DaprResiliencySpec.ReadError(resolvedPath, ex.Message);
        }

        const int MaxFileSizeBytes = 1_048_576; // 1 MB
        if (yamlContent.Length > MaxFileSizeBytes) {
            _logger.LogWarning("Resiliency config file too large ({Size} bytes): {Path}", yamlContent.Length, resolvedPath);
            return DaprResiliencySpec.ReadError(resolvedPath, $"File exceeds maximum size of {MaxFileSizeBytes / 1024}KB ({yamlContent.Length} bytes)");
        }

        try {
            return ParseResiliencyYaml(yamlContent, _logger);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to parse resiliency YAML: {Path}", resolvedPath);
            return DaprResiliencySpec.ParseError(resolvedPath, yamlContent, ex.Message);
        }
    }

    internal static DaprResiliencySpec ParseResiliencyYaml(string yamlContent, ILogger? logger = null) {
        YamlStream yaml = [];
        yaml.Load(new StringReader(yamlContent));

        if (yaml.Documents.Count == 0) {
            return new DaprResiliencySpec([], [], [], [], IsConfigurationAvailable: true, RawYamlContent: yamlContent, ErrorMessage: null);
        }

        var root = (YamlMappingNode)yaml.Documents[0].RootNode;

        if (!root.Children.TryGetValue(new YamlScalarNode("spec"), out YamlNode? specNode)
            || specNode is not YamlMappingNode spec) {
            return new DaprResiliencySpec([], [], [], [], IsConfigurationAvailable: true, RawYamlContent: yamlContent, ErrorMessage: null);
        }

        // Parse policies
        List<DaprRetryPolicy> retries = [];
        List<DaprTimeoutPolicy> timeouts = [];
        List<DaprCircuitBreakerPolicy> circuitBreakers = [];

        HashSet<string> knownPolicySections = ["retries", "timeouts", "circuitBreakers"];
        if (spec.Children.TryGetValue(new YamlScalarNode("policies"), out YamlNode? policiesNode)
            && policiesNode is YamlMappingNode policies) {
            retries = ParseRetryPolicies(policies);
            timeouts = ParseTimeoutPolicies(policies);
            circuitBreakers = ParseCircuitBreakerPolicies(policies);

            foreach (KeyValuePair<YamlNode, YamlNode> entry in policies.Children) {
                if (entry.Key is YamlScalarNode keyNode
                    && keyNode.Value is not null
                    && !knownPolicySections.Contains(keyNode.Value)) {
                    logger?.LogWarning("Unrecognized resiliency policy section: {Section}", keyNode.Value);
                }
            }
        }

        // Parse targets
        List<DaprResiliencyTargetBinding> targetBindings = [];
        if (spec.Children.TryGetValue(new YamlScalarNode("targets"), out YamlNode? targetsNode)
            && targetsNode is YamlMappingNode targets) {
            targetBindings = ParseTargetBindings(targets);
        }

        return new DaprResiliencySpec(
            retries,
            timeouts,
            circuitBreakers,
            targetBindings,
            IsConfigurationAvailable: true,
            RawYamlContent: yamlContent,
            ErrorMessage: null);
    }

    private static List<DaprRetryPolicy> ParseRetryPolicies(YamlMappingNode policies) {
        List<DaprRetryPolicy> result = [];
        if (!policies.Children.TryGetValue(new YamlScalarNode("retries"), out YamlNode? retriesNode)
            || retriesNode is not YamlMappingNode retriesMap) {
            return result;
        }

        foreach (KeyValuePair<YamlNode, YamlNode> entry in retriesMap.Children) {
            string name = ((YamlScalarNode)entry.Key).Value!;
            if (entry.Value is not YamlMappingNode props) {
                continue;
            }

            result.Add(new DaprRetryPolicy(
                Name: name,
                Strategy: GetScalar(props, "policy") ?? "unknown",
                MaxRetries: int.TryParse(GetScalar(props, "maxRetries"), out int mr) ? mr : 0,
                Duration: GetScalar(props, "duration"),
                MaxInterval: GetScalar(props, "maxInterval")));
        }

        return result;
    }

    private static List<DaprTimeoutPolicy> ParseTimeoutPolicies(YamlMappingNode policies) {
        List<DaprTimeoutPolicy> result = [];
        if (!policies.Children.TryGetValue(new YamlScalarNode("timeouts"), out YamlNode? timeoutsNode)
            || timeoutsNode is not YamlMappingNode timeoutsMap) {
            return result;
        }

        foreach (KeyValuePair<YamlNode, YamlNode> entry in timeoutsMap.Children) {
            string name = ((YamlScalarNode)entry.Key).Value!;
            if (entry.Value is YamlScalarNode scalar) {
                result.Add(new DaprTimeoutPolicy(name, scalar.Value!));
            }
            else if (entry.Value is YamlMappingNode nested) {
                string? general = GetScalar(nested, "general");
                if (general is not null) {
                    result.Add(new DaprTimeoutPolicy(name, general));
                }
            }
        }

        return result;
    }

    private static List<DaprCircuitBreakerPolicy> ParseCircuitBreakerPolicies(YamlMappingNode policies) {
        List<DaprCircuitBreakerPolicy> result = [];
        if (!policies.Children.TryGetValue(new YamlScalarNode("circuitBreakers"), out YamlNode? cbNode)
            || cbNode is not YamlMappingNode cbMap) {
            return result;
        }

        foreach (KeyValuePair<YamlNode, YamlNode> entry in cbMap.Children) {
            string name = ((YamlScalarNode)entry.Key).Value!;
            if (entry.Value is not YamlMappingNode props) {
                continue;
            }

            result.Add(new DaprCircuitBreakerPolicy(
                Name: name,
                MaxRequests: int.TryParse(GetScalar(props, "maxRequests"), out int mq) ? mq : 0,
                Interval: GetScalar(props, "interval") ?? "0s",
                Timeout: GetScalar(props, "timeout") ?? "0s",
                Trip: GetScalar(props, "trip") ?? "unknown"));
        }

        return result;
    }

    private static List<DaprResiliencyTargetBinding> ParseTargetBindings(YamlMappingNode targets) {
        List<DaprResiliencyTargetBinding> result = [];

        // Parse app targets
        if (targets.Children.TryGetValue(new YamlScalarNode("apps"), out YamlNode? appsNode)
            && appsNode is YamlMappingNode appsMap) {
            foreach (KeyValuePair<YamlNode, YamlNode> entry in appsMap.Children) {
                string targetName = ((YamlScalarNode)entry.Key).Value!;
                if (entry.Value is YamlMappingNode props) {
                    result.Add(new DaprResiliencyTargetBinding(
                        targetName,
                        "App",
                        Direction: null,
                        RetryPolicy: GetScalar(props, "retry"),
                        TimeoutPolicy: GetScalar(props, "timeout"),
                        CircuitBreakerPolicy: GetScalar(props, "circuitBreaker")));
                }
            }
        }

        // Parse component targets
        if (targets.Children.TryGetValue(new YamlScalarNode("components"), out YamlNode? componentsNode)
            && componentsNode is YamlMappingNode componentsMap) {
            foreach (KeyValuePair<YamlNode, YamlNode> entry in componentsMap.Children) {
                string targetName = ((YamlScalarNode)entry.Key).Value!;
                if (entry.Value is not YamlMappingNode props) {
                    continue;
                }

                // Check if this component has directional sub-nodes (outbound/inbound)
                bool hasDirection = false;
                if (props.Children.TryGetValue(new YamlScalarNode("outbound"), out YamlNode? outNode)
                    && outNode is YamlMappingNode outbound) {
                    hasDirection = true;
                    result.Add(new DaprResiliencyTargetBinding(
                        targetName,
                        "Component",
                        Direction: "Outbound",
                        RetryPolicy: GetScalar(outbound, "retry"),
                        TimeoutPolicy: GetScalar(outbound, "timeout"),
                        CircuitBreakerPolicy: GetScalar(outbound, "circuitBreaker")));
                }

                if (props.Children.TryGetValue(new YamlScalarNode("inbound"), out YamlNode? inNode)
                    && inNode is YamlMappingNode inbound) {
                    hasDirection = true;
                    result.Add(new DaprResiliencyTargetBinding(
                        targetName,
                        "Component",
                        Direction: "Inbound",
                        RetryPolicy: GetScalar(inbound, "retry"),
                        TimeoutPolicy: GetScalar(inbound, "timeout"),
                        CircuitBreakerPolicy: GetScalar(inbound, "circuitBreaker")));
                }

                // Non-directional component
                if (!hasDirection) {
                    result.Add(new DaprResiliencyTargetBinding(
                        targetName,
                        "Component",
                        Direction: null,
                        RetryPolicy: GetScalar(props, "retry"),
                        TimeoutPolicy: GetScalar(props, "timeout"),
                        CircuitBreakerPolicy: GetScalar(props, "circuitBreaker")));
                }
            }
        }

        // Sort by target type then target name
        result.Sort((a, b) => {
            int typeCompare = string.Compare(a.TargetType, b.TargetType, StringComparison.Ordinal);
            return typeCompare != 0 ? typeCompare : string.Compare(a.TargetName, b.TargetName, StringComparison.Ordinal);
        });

        return result;
    }

    private static string? GetScalar(YamlMappingNode node, string key) => node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value)
            && value is YamlScalarNode scalar ? scalar.Value : null;

    private async Task ProbeStateStoreAsync(
        DaprComponentDetail[] components,
        int index,
        CancellationToken ct) {
        DaprComponentDetail component = components[index];
        try {
            _ = await _daprClient
                .GetStateAsync<string>(component.ComponentName, "admin:dapr-probe", cancellationToken: ct)
                .ConfigureAwait(false);

            // Success (null return = key missing, but store responded) → Healthy
        }
        catch (OperationCanceledException) {
            // Probe timed out or was cancelled — mark as Degraded (inconclusive)
            components[index] = component with { Status = HealthStatus.Degraded, LastCheckUtc = DateTimeOffset.UtcNow };
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "State store probe failed for {ComponentName}.", component.ComponentName);
            components[index] = component with { Status = HealthStatus.Unhealthy, LastCheckUtc = DateTimeOffset.UtcNow };
        }
    }
}
