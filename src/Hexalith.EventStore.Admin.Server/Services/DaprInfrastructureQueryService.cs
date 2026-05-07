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

    // Per-request memoization of the remote /v1.0/metadata payload. The service is registered
    // scoped so each HTTP request gets a fresh instance; sharing the parsed payload across the
    // four consumers (canonical inventory, sidecar info, actor info, pub/sub overview) keeps
    // /dapr and /dapr/pubsub on the same snapshot rather than racing separate roundtrips.
    private Task<RemoteMetadataPayload>? _remoteMetadataTask;

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
        DaprCanonicalInventory inventory = await GetCanonicalDaprInventoryAsync(ct).ConfigureAwait(false);
        return inventory.Components;
    }

    /// <inheritdoc/>
    public async Task<DaprCanonicalInventory> GetCanonicalDaprInventoryAsync(CancellationToken ct = default) {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Dictionary<(string Name, string Type), DaprComponentDetail> merged
            = new(InventoryKeyComparer.Instance);
        bool localProbeAvailable = false;

        // Stage 1 — local Admin sidecar metadata (degraded fallback; supplies component names that
        // probes need to attach to when the remote EventStore sidecar metadata is also unavailable).
        DaprMetadata? localMetadata = null;
        try {
            localMetadata = await _daprClient.GetMetadataAsync(ct).ConfigureAwait(false);
            localProbeAvailable = localMetadata is not null;
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Local DAPR sidecar metadata unavailable for canonical inventory.");
        }

        if (localMetadata?.Components is not null) {
            foreach (DaprComponentsMetadata c in localMetadata.Components) {
                if (string.IsNullOrEmpty(c.Name) || string.IsNullOrEmpty(c.Type)) {
                    continue;
                }

                DaprComponentDetail entry = new(
                    c.Name,
                    c.Type,
                    DaprComponentCategoryHelper.FromComponentType(c.Type),
                    c.Version ?? string.Empty,
                    HealthStatus.Healthy,
                    now,
                    c.Capabilities ?? [],
                    DaprComponentSource.LocalAdminMetadataFallback);
                merged[(entry.ComponentName, entry.ComponentType)] = entry;
            }
        }

        // Stage 2 — remote EventStore sidecar metadata. Canonical for loaded EventStore-sidecar
        // components and active pub/sub subscriptions. Remote evidence supersedes the local
        // fallback for any (name, type) it reports, but never the local probe — that runs after.
        RemoteMetadataPayload remote = await TryReadRemoteMetadataAsync(ct).ConfigureAwait(false);

        if (remote.Components is not null) {
            foreach (DaprComponentDetail c in remote.Components) {
                merged[(c.ComponentName, c.ComponentType)] = c with { Source = DaprComponentSource.RemoteEventStoreMetadata };
            }
        }

        // Stage 3 — ensure the configured Admin state store has an entry to probe even when
        // neither metadata source listed it (both unavailable). Without this synth, a Redis
        // outage with no local/remote metadata would silently omit the state-store row from
        // /health and /dapr instead of surfacing it as Unhealthy.
        string configuredStateStore = _options.StateStoreName;
        if (!merged.Keys.Any(k => string.Equals(k.Name, configuredStateStore, StringComparison.OrdinalIgnoreCase))) {
            const string SyntheticStateStoreType = "state.unknown";
            DaprComponentDetail synth = new(
                configuredStateStore,
                SyntheticStateStoreType,
                DaprComponentCategory.StateStore,
                Version: string.Empty,
                Status: HealthStatus.Unhealthy,
                LastCheckUtc: now,
                Capabilities: [],
                Source: DaprComponentSource.Unavailable);
            merged[(configuredStateStore, SyntheticStateStoreType)] = synth;
        }

        // Stage 4 — probe state-store entries last so probe Status wins over remote Status
        // (Operator Truth Contract conflict rule: "Local probe fails, remote says loaded ->
        // one row, loaded inventory + unhealthy probe evidence"). The probe also rewrites
        // Source to LocalAdminProbe to record the canonical health-check evidence.
        using (var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct)) {
            probeCts.CancelAfter(TimeSpan.FromSeconds(3));

            List<Task<(DaprComponentDetail Updated, bool Replace)>> probes = [];
            foreach (DaprComponentDetail entry in merged.Values.ToArray()) {
                if (entry.Category != DaprComponentCategory.StateStore) {
                    continue;
                }

                probes.Add(ProbeStateStoreEntryAsync(entry, probeCts.Token));
            }

            if (probes.Count > 0) {
                try {
                    (DaprComponentDetail Updated, bool Replace)[] probed
                        = await Task.WhenAll(probes).ConfigureAwait(false);
                    foreach ((DaprComponentDetail updated, bool replace) in probed) {
                        if (replace) {
                            merged[(updated.ComponentName, updated.ComponentType)] = updated;
                        }
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
                    _logger.LogWarning("State store health probes timed out after 3 seconds.");
                }
            }
        }

        IReadOnlyList<DaprSubscriptionInfo> subs = remote.Subscriptions ?? [];

        IReadOnlyList<DaprComponentDetail> ordered = merged.Values
            .OrderBy(c => (int)c.Category)
            .ThenBy(c => c.ComponentName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new DaprCanonicalInventory(
            ordered,
            subs,
            remote.Status,
            remote.Endpoint,
            localProbeAvailable,
            now);
    }

    /// <inheritdoc/>
    public async Task<DaprSidecarInfo?> GetSidecarInfoAsync(CancellationToken ct = default) {
        DaprMetadata metadata = await _daprClient.GetMetadataAsync(ct).ConfigureAwait(false);
        if (metadata is null) {
            return null;
        }

        // Subscriptions and HttpEndpoints live on the remote EventStore sidecar; the local
        // 'eventstore-admin' sidecar has no pub/sub subscriptions of its own. We delegate to
        // the shared remote-metadata parser so every Admin surface sees the same status
        // (Available / NotConfigured / Unreachable / InvalidPayload).
        string runtimeVersion = metadata.Extended?.TryGetValue("daprRuntimeVersion", out string? version) == true
            ? version ?? "unknown"
            : "unknown";

        RemoteMetadataPayload remote = await TryReadRemoteMetadataAsync(ct).ConfigureAwait(false);

        return new DaprSidecarInfo(
            string.IsNullOrWhiteSpace(metadata.Id) ? "unknown" : metadata.Id,
            runtimeVersion,
            metadata.Components?.Count ?? 0,
            remote.SubscriptionCount,
            remote.HttpEndpointCount,
            remote.Status,
            remote.Endpoint);
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
        RemoteMetadataStatus remoteStatus;
        string? remoteEndpoint;
        if (actorTypes.Count == 0) {
            RemoteMetadataPayload remote = await TryReadRemoteMetadataAsync(ct).ConfigureAwait(false);
            if (remote.Actors is not null) {
                foreach (DaprActorTypeInfo actor in remote.Actors) {
                    actorTypes.Add(actor);
                    if (!KnownActorTypes.Types.ContainsKey(actor.TypeName)) {
                        _logger.LogWarning("Unknown actor type '{ActorType}' detected — update KnownActorTypes map", actor.TypeName);
                    }
                }
            }

            remoteStatus = remote.Status;
            remoteEndpoint = remote.Endpoint;
        }
        else {
            // Local actors found — preserve original behavior: status reflects endpoint config
            // but we did not exercise remote connectivity, so we cannot claim Available.
            remoteEndpoint = string.IsNullOrWhiteSpace(_options.EventStoreDaprHttpEndpoint)
                ? null
                : _options.EventStoreDaprHttpEndpoint;
            remoteStatus = remoteEndpoint is null
                ? RemoteMetadataStatus.NotConfigured
                : RemoteMetadataStatus.Unreachable;
        }

        int totalActive = actorTypes
            .Where(a => a.ActiveCount >= 0)
            .Sum(a => a.ActiveCount);

        return new DaprActorRuntimeInfo(
            actorTypes,
            totalActive,
            config,
            remoteStatus,
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
        RemoteMetadataPayload remote = await TryReadRemoteMetadataAsync(ct).ConfigureAwait(false);

        IReadOnlyList<DaprComponentDetail> pubSubComponents = remote.Components is null
            ? []
            : remote.Components
                .Where(c => c.Category == DaprComponentCategory.PubSub)
                .Select(c => c with { Source = DaprComponentSource.RemoteEventStoreMetadata })
                .ToArray();

        return new DaprPubSubOverview(
            pubSubComponents,
            remote.Subscriptions ?? [],
            remote.Status,
            remote.Endpoint);
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

    private async Task<(DaprComponentDetail Updated, bool Replace)> ProbeStateStoreEntryAsync(
        DaprComponentDetail component,
        CancellationToken ct) {
        try {
            _ = await _daprClient
                .GetStateAsync<string>(component.ComponentName, "admin:dapr-probe", cancellationToken: ct)
                .ConfigureAwait(false);

            // Success — store responded. Promote source to LocalAdminProbe to record canonical evidence.
            return (component with { Status = HealthStatus.Healthy, LastCheckUtc = DateTimeOffset.UtcNow, Source = DaprComponentSource.LocalAdminProbe }, true);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            // Probe timed out without caller cancellation — treat as Unhealthy evidence.
            // A bounded probe that does not respond within the budget is not "degraded but
            // usable"; for AC1 purposes it is the same signal as an exception.
            _logger.LogWarning("State store probe timed out for {ComponentName} after 3 seconds.", component.ComponentName);
            return (component with { Status = HealthStatus.Unhealthy, LastCheckUtc = DateTimeOffset.UtcNow, Source = DaprComponentSource.LocalAdminProbe }, true);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "State store probe failed for {ComponentName}.", component.ComponentName);
            return (component with { Status = HealthStatus.Unhealthy, LastCheckUtc = DateTimeOffset.UtcNow, Source = DaprComponentSource.LocalAdminProbe }, true);
        }
    }

    /// <summary>
    /// Reads the remote EventStore sidecar's <c>/v1.0/metadata</c> endpoint once per request and
    /// exposes the shared-payload facts every Admin surface needs (components, subscriptions,
    /// actors, counts, and the consolidated <see cref="RemoteMetadataStatus"/>). Returns explicit
    /// status values for not-configured / unreachable / invalid-payload conditions so callers do
    /// not need to translate transport exceptions into UI text.
    /// </summary>
    /// <remarks>
    /// Memoized per scoped instance so the canonical inventory, sidecar info, actor info and
    /// pub/sub overview consumers all observe the same snapshot when serving one HTTP request.
    /// Cancellation is honored on the first caller; later callers receive the cached result
    /// regardless of their token state since the I/O has already completed.
    /// </remarks>
    private Task<RemoteMetadataPayload> TryReadRemoteMetadataAsync(CancellationToken ct) {
        // Race-free: scoped lifetime guarantees single-threaded access within a request.
        return _remoteMetadataTask ??= ReadRemoteMetadataAsync(ct);
    }

    private async Task<RemoteMetadataPayload> ReadRemoteMetadataAsync(CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(_options.EventStoreDaprHttpEndpoint)) {
            return new RemoteMetadataPayload {
                Status = RemoteMetadataStatus.NotConfigured,
                Endpoint = null,
            };
        }

        string endpoint = _options.EventStoreDaprHttpEndpoint;
        string baseUrl = endpoint.TrimEnd('/');

        try {
            HttpClient httpClient = _httpClientFactory.CreateClient("DaprSidecar");
            using HttpResponseMessage response = await httpClient
                .GetAsync($"{baseUrl}/v1.0/metadata", ct)
                .ConfigureAwait(false);
            _ = response.EnsureSuccessStatusCode();

            using JsonDocument doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                cancellationToken: ct).ConfigureAwait(false);

            (List<DaprComponentDetail> components, List<DaprSubscriptionInfo> subscriptions,
             List<DaprActorTypeInfo> actors, int subCount, int httpEndpointCount)
                = ParseRemoteMetadata(doc.RootElement);

            // RemoteMetadataStatus.Initializing is reserved for an explicit signal we don't yet
            // receive from DAPR's /v1.0/metadata endpoint; a successful 200 OK with parseable
            // JSON is reported as Available even when the component/actor/subscription arrays
            // are empty (which is itself observable by consumers via empty collections).
            return new RemoteMetadataPayload {
                Status = RemoteMetadataStatus.Available,
                Endpoint = endpoint,
                Components = components,
                Subscriptions = subscriptions,
                Actors = actors,
                SubscriptionCount = subCount,
                HttpEndpointCount = httpEndpointCount,
            };
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (JsonException ex) {
            _logger.LogWarning(
                ex,
                "Remote DAPR sidecar metadata at {Endpoint} returned an invalid JSON payload.",
                endpoint);
            return new RemoteMetadataPayload {
                Status = RemoteMetadataStatus.InvalidPayload,
                Endpoint = endpoint,
            };
        }
        catch (Exception ex) {
            _logger.LogWarning(
                ex,
                "Remote DAPR sidecar metadata unavailable at {Endpoint}. ExceptionType={ExceptionType}.",
                endpoint,
                ex.GetType().Name);
            return new RemoteMetadataPayload {
                Status = RemoteMetadataStatus.Unreachable,
                Endpoint = endpoint,
            };
        }
    }

    private static (List<DaprComponentDetail> Components,
                    List<DaprSubscriptionInfo> Subscriptions,
                    List<DaprActorTypeInfo> Actors,
                    int SubscriptionCount,
                    int HttpEndpointCount) ParseRemoteMetadata(JsonElement root) {
        List<DaprComponentDetail> components = [];
        List<DaprSubscriptionInfo> subscriptions = [];
        List<DaprActorTypeInfo> actors = [];
        int subCount = 0;
        int httpEndpointCount = 0;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (root.TryGetProperty("components", out JsonElement componentsElement)
            && componentsElement.ValueKind == JsonValueKind.Array) {
            foreach (JsonElement comp in componentsElement.EnumerateArray()) {
                string? name = comp.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
                string? compType = comp.TryGetProperty("type", out JsonElement tp) ? tp.GetString() : null;
                string? version = comp.TryGetProperty("version", out JsonElement v) ? v.GetString() : null;

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(compType)) {
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

                components.Add(new DaprComponentDetail(
                    name,
                    compType,
                    DaprComponentCategoryHelper.FromComponentType(compType),
                    version ?? string.Empty,
                    HealthStatus.Healthy,
                    now,
                    capabilities,
                    DaprComponentSource.RemoteEventStoreMetadata));
            }
        }

        if (root.TryGetProperty("subscriptions", out JsonElement subscriptionsElement)
            && subscriptionsElement.ValueKind == JsonValueKind.Array) {
            subCount = subscriptionsElement.GetArrayLength();
            foreach (JsonElement sub in subscriptionsElement.EnumerateArray()) {
                string? pubsubName = sub.TryGetProperty("pubsubName", out JsonElement pn) ? pn.GetString() : null;
                string? topic = sub.TryGetProperty("topic", out JsonElement t) ? t.GetString() : null;
                string? type = sub.TryGetProperty("type", out JsonElement ty) ? ty.GetString() : null;
                string? deadLetterTopic = sub.TryGetProperty("deadLetterTopic", out JsonElement dlt) ? dlt.GetString() : null;

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

        if (root.TryGetProperty("actors", out JsonElement actorsElement)
            && actorsElement.ValueKind == JsonValueKind.Array) {
            foreach (JsonElement actorElement in actorsElement.EnumerateArray()) {
                string type = actorElement.TryGetProperty("type", out JsonElement typeEl)
                    ? typeEl.GetString() ?? string.Empty
                    : string.Empty;
                int count = actorElement.TryGetProperty("count", out JsonElement countEl)
                    ? countEl.GetInt32()
                    : -1;

                if (!string.IsNullOrEmpty(type)) {
                    KnownActorTypeDescriptor descriptor = KnownActorTypes.GetDescriptor(type);
                    actors.Add(new DaprActorTypeInfo(
                        type,
                        count,
                        descriptor.Description,
                        descriptor.ActorIdFormat));
                }
            }
        }

        if (root.TryGetProperty("httpEndpoints", out JsonElement httpEndpointsElement)
            && httpEndpointsElement.ValueKind == JsonValueKind.Array) {
            httpEndpointCount = httpEndpointsElement.GetArrayLength();
        }

        return (components, subscriptions, actors, subCount, httpEndpointCount);
    }

    private readonly struct RemoteMetadataPayload {
        public RemoteMetadataStatus Status { get; init; }

        public string? Endpoint { get; init; }

        public List<DaprComponentDetail>? Components { get; init; }

        public List<DaprSubscriptionInfo>? Subscriptions { get; init; }

        public List<DaprActorTypeInfo>? Actors { get; init; }

        public int SubscriptionCount { get; init; }

        public int HttpEndpointCount { get; init; }
    }

    private sealed class InventoryKeyComparer : IEqualityComparer<(string Name, string Type)> {
        public static InventoryKeyComparer Instance { get; } = new();

        public bool Equals((string Name, string Type) x, (string Name, string Type) y)
            => StringComparer.OrdinalIgnoreCase.Equals(x.Name, y.Name)
            && StringComparer.OrdinalIgnoreCase.Equals(x.Type, y.Type);

        public int GetHashCode((string Name, string Type) obj)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Type));
    }
}
