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
    //
    // We cache the *result* (a value-type RemoteMetadataPayload), not the in-flight Task. The
    // task captures the first caller's CancellationToken; if that caller cancels, every later
    // in-scope consumer awaiting the cached task would re-throw the cancellation even when its
    // own token is fine. By caching only on successful completion (any non-throwing return —
    // Available / NotConfigured / Unreachable / InvalidPayload), a cancelled first caller does
    // not poison subsequent consumers; they retry with their own token.
    private RemoteMetadataPayload? _cachedRemoteMetadata;

    // Per-request memoization of the local Admin sidecar's DaprClient.GetMetadataAsync result.
    // Same rationale as _cachedRemoteMetadata: keeps GetCanonicalDaprInventoryAsync,
    // GetSidecarInfoAsync, and GetActorRuntimeInfoAsync on a single snapshot of the local
    // sidecar's component/actor lists within one request without capturing caller CTs.
    // null sentinel = "not yet successfully read". A successful read with a null DaprMetadata
    // (sidecar reachable but returned no metadata) is cached as LocalMetadataResult.Empty.
    private LocalMetadataResult? _cachedLocalMetadata;

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
                : SanitizeEndpoint(_options.EventStoreDaprHttpEndpoint));
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

        // Stage 1 — local Admin sidecar metadata (degraded fallback; supplies component names that
        // probes need to attach to when the remote EventStore sidecar metadata is also unavailable).
        LocalMetadataResult local = await TryReadLocalMetadataAsync(ct).ConfigureAwait(false);
        DaprMetadata? localMetadata = local.Metadata;

        // Round 10 P3: `LocalSidecarMetadataAvailable` answers ONE narrow question: did the local
        // sidecar's `/v1.0/metadata` endpoint respond successfully? It is propagated into
        // `SystemHealthReport.LocalSidecarMetadataStatus` and consumed by `ComputeOverallStatus`
        // as a proxy for "Admin operations can be served". A freshly-started sidecar that
        // responds with empty `Components` and `Actors` arrays during init is reachable — the
        // app simply has not registered yet — and forcing `Unreachable` here triggers a global
        // Unhealthy on cold start (false-positive outage). The empty-inventory signal is
        // separately surfaced through the canonical inventory's emptiness, not through this
        // flag. Round 5 P5's "evidence required" check is dropped because it conflated transport
        // success with app-registration completeness.
        bool localSidecarMetadataAvailable = local.Available && localMetadata is not null;

        if (localMetadata?.Components is not null) {
            // Round 5 P1 (D4 option a): the truth contract forbids "Healthy by metadata default"
            // for any category — local sidecar metadata names a component but does not prove the
            // backing dependency is usable. Only the configured Admin state-store row receives
            // probe evidence later in Stage 4; every other local-fallback row stays unverified
            // for the lifetime of the canonical inventory. Stamp non-probed rows as Unhealthy
            // uniformly so Source=LocalAdminMetadataFallback always pairs with a non-Healthy
            // status the operator cannot misread as probe-attested. The probed state-store row
            // defaults Healthy because Stage 4 always overwrites with the actual probe Status.
            string configuredName = _options.StateStoreName;
            bool stateStoreProbeWillRun = !string.IsNullOrWhiteSpace(configuredName);
            foreach (DaprComponentsMetadata c in localMetadata.Components) {
                if (string.IsNullOrEmpty(c.Name) || string.IsNullOrEmpty(c.Type)) {
                    continue;
                }

                DaprComponentCategory category = DaprComponentCategoryHelper.FromComponentType(c.Type);
                bool willBeProbed =
                    category == DaprComponentCategory.StateStore
                    && stateStoreProbeWillRun
                    && string.Equals(c.Name, configuredName, StringComparison.OrdinalIgnoreCase);

                // Round 10 P18: distinguish state-store category from other categories for the
                // non-probed default Status. The configured Admin state-store row gets Healthy
                // (Stage 4 will overwrite with the actual probe status). For any other state-store
                // row that won't be probed, mark Unhealthy — operators must not see a green
                // state-store row that lacks probe attestation. For non-state-store categories
                // (pubsub / bindings / secret stores / etc.), Stage 4 never probes them, so
                // "loaded inventory only, no probe attestation" maps to Degraded per the spec
                // status vocabulary ("operational but a non-Admin dependency lacks evidence").
                HealthStatus defaultStatus = (willBeProbed, category) switch {
                    (true, _) => HealthStatus.Healthy,
                    (false, DaprComponentCategory.StateStore) => HealthStatus.Unhealthy,
                    _ => HealthStatus.Degraded,
                };
                DaprComponentDetail entry = new(
                    c.Name,
                    c.Type,
                    category,
                    c.Version ?? string.Empty,
                    defaultStatus,
                    now,
                    c.Capabilities ?? [],
                    DaprComponentSource.LocalAdminMetadataFallback,
                    DaprComponentSource.LocalAdminMetadataFallback);
                merged[(entry.ComponentName, entry.ComponentType)] = entry;
            }
        }

        // Stage 2 — remote EventStore sidecar metadata. Canonical for loaded EventStore-sidecar
        // components and active pub/sub subscriptions. Remote evidence supersedes the local
        // fallback for any (name, type) it reports, but never the local probe — that runs after.
        //
        // Round 5 P3: ParseRemoteMetadata stamps every component with HealthStatus.Healthy, but
        // a remote state-store named differently from the configured Admin state-store name is
        // never probed by Stage 4 (the probe filter scopes to `_options.StateStoreName` to avoid
        // false-negative "component not found" results when the Admin sidecar does not host the
        // remote-only state store). Without intervention, a remote-reported `eventstore-state`
        // would render green on `/dapr` and `/health` with zero probe evidence — exactly the
        // truth-contract violation P3 forbids. Downgrade such rows to Degraded ("loaded
        // inventory only, no probe attestation"); the configured state-store row is still
        // written Healthy/Unhealthy by the Stage 4 probe.
        RemoteMetadataPayload remote = await TryReadRemoteMetadataAsync(ct).ConfigureAwait(false);

        if (remote.Components is not null) {
            string configuredStateStoreForRemote = _options.StateStoreName;
            bool stateStoreProbeWillRun = !string.IsNullOrWhiteSpace(configuredStateStoreForRemote);
            foreach (DaprComponentDetail c in remote.Components) {
                bool willBeProbed =
                    c.Category == DaprComponentCategory.StateStore
                    && stateStoreProbeWillRun
                    && string.Equals(c.ComponentName, configuredStateStoreForRemote, StringComparison.OrdinalIgnoreCase);

                bool needsStateStoreDowngrade =
                    c.Category == DaprComponentCategory.StateStore
                    && !willBeProbed
                    && c.Status == HealthStatus.Healthy;

                DaprComponentDetail entry = needsStateStoreDowngrade
                    ? c with {
                        Status = HealthStatus.Degraded,
                        Source = DaprComponentSource.RemoteEventStoreMetadata,
                        HealthEvidenceSource = DaprComponentSource.RemoteEventStoreMetadata,
                    }
                    : c with {
                        Source = DaprComponentSource.RemoteEventStoreMetadata,
                        HealthEvidenceSource = DaprComponentSource.RemoteEventStoreMetadata,
                    };
                merged[(entry.ComponentName, entry.ComponentType)] = entry;
            }
        }

        // Stage 3 — ensure the configured Admin state store has an entry to probe even when
        // neither metadata source listed it (both unavailable). Without this synth, a Redis
        // outage with no local/remote metadata would silently omit the state-store row from
        // /health and /dapr instead of surfacing it as Unhealthy.
        //
        // Skip when the configured state-store name is empty/whitespace: `DaprComponentDetail`
        // validates ComponentName non-empty and would throw, replacing the "graceful degradation"
        // contract with an unhandled 500.
        string configuredStateStore = _options.StateStoreName;
        if (!string.IsNullOrWhiteSpace(configuredStateStore)
            && !merged.Values.Any(c =>
                c.Category == DaprComponentCategory.StateStore
                && string.Equals(c.ComponentName, configuredStateStore, StringComparison.OrdinalIgnoreCase))) {
            const string SyntheticStateStoreType = "state.unknown";
            DaprComponentDetail synth = new(
                configuredStateStore,
                SyntheticStateStoreType,
                // Derive Category from the helper rather than hardcoding StateStore: keeps
                // synth.Category aligned with helper logic if the literal or mapping changes.
                DaprComponentCategoryHelper.FromComponentType(SyntheticStateStoreType),
                Version: string.Empty,
                Status: HealthStatus.Unhealthy,
                LastCheckUtc: now,
                Capabilities: [],
                // Round 5 P2: ship synth with Source = Unavailable. Source = LocalAdminProbe is
                // a truth-contract claim that probe evidence exists; setting it before the probe
                // runs would mean a future refactor that short-circuits Stage 4 silently leaves
                // the synth row reporting "probed and Unhealthy" without an actual probe call.
                // Stage 4 (the only writer of LocalAdminProbe) updates Source on probe completion,
                // so the synth's Source attribution is correct only *after* Stage 4 runs.
                Source: DaprComponentSource.Unavailable,
                HealthEvidenceSource: DaprComponentSource.Unavailable);
            merged[(configuredStateStore, SyntheticStateStoreType)] = synth;
        }

        // Stage 4 — probe the configured Admin state store last so probe Status wins over
        // remote Status (Operator Truth Contract conflict rule: "Local probe fails, remote says
        // loaded -> one row, loaded inventory + unhealthy probe evidence"). The probe also
        // rewrites Source to LocalAdminProbe to record the canonical health-check evidence.
        //
        // Filter to entries whose ComponentName matches the configured Admin state-store name:
        // probing every state-store category entry would call _daprClient.GetStateAsync against
        // the *Admin* sidecar for components that only exist on the EventStore sidecar (e.g.
        // `eventstore-state`), and DAPR returns "component not found" — which would falsely
        // mark a remote-only component as Unhealthy with Source = LocalAdminProbe.
        if (!string.IsNullOrWhiteSpace(configuredStateStore)) {
            using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            probeCts.CancelAfter(TimeSpan.FromSeconds(3));

            List<(DaprComponentDetail Entry, Task<(DaprComponentDetail Updated, bool Replace)> Task)> probesByEntry = [];
            foreach (DaprComponentDetail entry in merged.Values.ToArray()) {
                if (entry.Category != DaprComponentCategory.StateStore) {
                    continue;
                }

                if (!string.Equals(entry.ComponentName, configuredStateStore, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                probesByEntry.Add((entry, ProbeStateStoreEntryAsync(entry, ct, probeCts.Token)));
            }

            // Round 5 P6: stage probe results into a local dictionary, then merge once after
            // all probes settle. The previous shape iterated `merged.Values.ToArray()` then
            // awaited probes sequentially while overwriting `merged` mid-loop. Today benign
            // with one probe (configured-name filter), but the loop is documented as "shaped
            // for n probes" — at n>1 a probe returning a key already overwritten by a sibling
            // would race with last-write-wins. Staging keeps the merge step deterministic and
            // independent of probe completion order.
            Dictionary<(string Name, string Type), DaprComponentDetail> probeResults
                = new(InventoryKeyComparer.Instance);
            foreach ((DaprComponentDetail Entry, Task<(DaprComponentDetail Updated, bool Replace)> Task) pair in probesByEntry) {
                try {
                    (DaprComponentDetail updated, bool replace) = await pair.Task.ConfigureAwait(false);
                    if (replace) {
                        probeResults[(updated.ComponentName, updated.ComponentType)] = updated;
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
                    // Linked CTS fired its 3-second timeout without the caller cancelling. The
                    // inner ProbeStateStoreEntryAsync attempts to translate this into an
                    // Unhealthy row; if we get here the translation re-threw (e.g. probeCts
                    // cancellation raced an inner await). Stamp the row Unhealthy with
                    // LocalAdminProbe attribution so /dapr and /health do not diverge: leaving
                    // the prior LocalAdminMetadataFallback row would let /dapr render Healthy
                    // while IsStateStoreProbeFailed (which scopes to LocalAdminProbe rows)
                    // independently treats absence as failure on /health.
                    //
                    // Round 10 P9: use captured `now` from method entry, not DateTimeOffset.UtcNow.
                    // Test seams that inject a fixed clock (FakeTimeProvider) must observe
                    // deterministic timestamps that match the canonical inventory's `CapturedAtUtc`.
                    _logger.LogWarning("State store health probe timed out after 3 seconds for {ComponentName}.", pair.Entry.ComponentName);
                    probeResults[(pair.Entry.ComponentName, pair.Entry.ComponentType)] = pair.Entry with {
                        Status = HealthStatus.Unhealthy,
                        LastCheckUtc = now,
                        HealthEvidenceSource = DaprComponentSource.LocalAdminProbe,
                        // Round 10 P6: when the entry came from Stage-3 synth (Source = Unavailable),
                        // probe attestation IS the inventory evidence — the row exists only because
                        // we attempted a probe. Upgrade Source to LocalAdminProbe so the UI does not
                        // render "Source: unavailable" beside a probed status badge.
                        Source = pair.Entry.Source == DaprComponentSource.Unavailable
                            ? DaprComponentSource.LocalAdminProbe
                            : pair.Entry.Source,
                    };
                }
            }

            foreach (KeyValuePair<(string Name, string Type), DaprComponentDetail> result in probeResults) {
                merged[result.Key] = result.Value;
            }
        }

        IReadOnlyList<DaprSubscriptionInfo> subs = remote.Subscriptions ?? [];

        // Round 5 P16: tertiary tiebreaker on ComponentType so two rows with identical
        // (Category, ComponentName) but different Type (e.g., synth `state.unknown` + remote
        // `state.redis`) order deterministically across .NET runtime / Dictionary internals.
        // Without it, snapshot tests and history-persistence ordering can flip between runs.
        IReadOnlyList<DaprComponentDetail> ordered = merged.Values
            .OrderBy(c => (int)c.Category)
            .ThenBy(c => c.ComponentName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.ComponentType, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new DaprCanonicalInventory(
            ordered,
            subs,
            remote.Status,
            remote.Endpoint,
            localSidecarMetadataAvailable,
            now);
    }

    /// <inheritdoc/>
    public async Task<DaprSidecarInfo?> GetSidecarInfoAsync(CancellationToken ct = default) {
        // Use the per-request cached local sidecar metadata so /dapr and /dapr/pubsub agree on
        // local component count (round 3 D2). Direct GetMetadataAsync would race the cache.
        LocalMetadataResult local = await TryReadLocalMetadataAsync(ct).ConfigureAwait(false);
        DaprMetadata? metadata = local.Metadata;

        // Subscriptions and HttpEndpoints live on the remote EventStore sidecar; the local
        // 'eventstore-admin' sidecar has no pub/sub subscriptions of its own. We delegate to
        // the shared remote-metadata parser so every Admin surface sees the same status
        // (Available / NotConfigured / Unreachable / InvalidPayload).
        string runtimeVersion = metadata?.Extended?.TryGetValue("daprRuntimeVersion", out string? version) == true
            ? version ?? "unknown"
            : "unknown";

        RemoteMetadataPayload remote = await TryReadRemoteMetadataAsync(ct).ConfigureAwait(false);

        if (metadata is null && remote.Status == RemoteMetadataStatus.NotConfigured) {
            return null;
        }

        // Counts are meaningful only when remote.Status == Available; consumers reading
        // SubscriptionCount/HttpEndpointCount unconditionally would surface "0" for an
        // unreachable remote, exactly the unknown-zero pattern we forbid elsewhere.
        bool remoteAvailable = remote.Status == RemoteMetadataStatus.Available;
        return new DaprSidecarInfo(
            string.IsNullOrWhiteSpace(metadata?.Id) ? "unknown" : metadata.Id,
            runtimeVersion,
            metadata?.Components?.Count ?? 0,
            remoteAvailable ? remote.SubscriptionCount : 0,
            remoteAvailable ? remote.HttpEndpointCount : 0,
            remote.Status,
            remote.Endpoint);
    }

    /// <inheritdoc/>
    public async Task<DaprInfrastructureOverview> GetInfrastructureOverviewAsync(CancellationToken ct = default) {
        DaprCanonicalInventory inventory = await GetCanonicalDaprInventoryAsync(ct).ConfigureAwait(false);
        DaprSidecarInfo? sidecar = await GetSidecarInfoAsync(ct).ConfigureAwait(false);
        return new DaprInfrastructureOverview(sidecar, inventory.Components);
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

        DateTimeOffset observedAt = DateTimeOffset.UtcNow;
        Dictionary<string, DaprActorTypeInfo> actorTypes = new(StringComparer.Ordinal);
        LocalMetadataResult local = await TryReadLocalMetadataAsync(ct).ConfigureAwait(false);
        DaprMetadata? metadata = local.Metadata;

        RemoteMetadataPayload remote = await TryReadRemoteMetadataAsync(ct).ConfigureAwait(false);
        string inventorySource;
        RemoteMetadataStatus remoteStatus = remote.Status;
        string? remoteEndpoint = remote.Endpoint;

        if (remote.Status == RemoteMetadataStatus.Available) {
            inventorySource = "RemoteEventStoreSidecarMetadata";
            if (remote.Actors is not null) {
                foreach (DaprActorTypeInfo actor in remote.Actors) {
                    DaprActorCountStatus status = actor.ActiveCount >= 0
                        ? DaprActorCountStatus.Exact
                        : DaprActorCountStatus.Unavailable;
                    actorTypes[actor.TypeName] = new DaprActorTypeInfo(
                        actor.TypeName,
                        actor.ActiveCount,
                        actor.Description,
                        actor.ActorIdFormat,
                        status,
                        inventorySource,
                        status == DaprActorCountStatus.Exact
                            ? null
                            : "The EventStore sidecar metadata did not include an active count for this actor type.");

                    if (!KnownActorTypes.Types.ContainsKey(actor.TypeName)) {
                        _logger.LogWarning("Unknown actor type '{ActorType}' detected — update KnownActorTypes map", actor.TypeName);
                    }
                }
            }
        }
        else if (metadata?.Actors is not null && metadata.Actors.Count > 0) {
            inventorySource = "LocalAdminMetadataFallback";
            foreach (DaprActorMetadata actor in metadata.Actors) {
                // Guard against null/empty actor.Type — DAPR sidecar can return it during
                // init in some versions; KnownActorTypes.GetDescriptor(null) would NRE on
                // Dictionary.TryGetValue(null, out _). The remote actor branch already
                // applies the same guard.
                if (string.IsNullOrEmpty(actor.Type)) {
                    continue;
                }

                KnownActorTypeDescriptor descriptor = KnownActorTypes.GetDescriptor(actor.Type);
                actorTypes[actor.Type] = new DaprActorTypeInfo(
                    actor.Type,
                    actor.Count,
                    descriptor.Description,
                    descriptor.ActorIdFormat,
                    actor.Count >= 0 ? DaprActorCountStatus.SourceLimited : DaprActorCountStatus.Unavailable,
                    inventorySource,
                    "Count came from the local Admin sidecar fallback, not the EventStore actor owner sidecar.");
            }
        }
        else {
            inventorySource = remote.Status == RemoteMetadataStatus.NotConfigured
                ? "NotConfigured"
                : "Unavailable";
        }

        foreach ((string knownType, KnownActorTypeDescriptor descriptor) in KnownActorTypes.Types) {
            if (actorTypes.ContainsKey(knownType)) {
                continue;
            }

            actorTypes[knownType] = new DaprActorTypeInfo(
                knownType,
                -1,
                descriptor.Description,
                descriptor.ActorIdFormat,
                DaprActorCountStatus.Unavailable,
                inventorySource,
                remote.Status == RemoteMetadataStatus.Available
                    ? "The EventStore sidecar metadata omitted this known actor type; active count is unavailable."
                    : "Actor count source is unavailable.");
        }

        DaprActorTypeInfo[] orderedActorTypes = actorTypes.Values
            .OrderBy(a => KnownActorTypes.Types.ContainsKey(a.TypeName) ? 0 : 1)
            .ThenBy(a => a.TypeName, StringComparer.Ordinal)
            .ToArray();

        int totalActive = orderedActorTypes
            .Where(a => a.CountStatus != DaprActorCountStatus.Unavailable && a.ActiveCount >= 0)
            .Sum(a => a.ActiveCount);
        bool hasExactKnownCounts = orderedActorTypes.Any(a =>
            KnownActorTypes.Types.ContainsKey(a.TypeName)
            && a.CountStatus != DaprActorCountStatus.Unavailable
            && a.ActiveCount >= 0);
        bool isInventoryComplete = remote.Status == RemoteMetadataStatus.Available
            && KnownActorTypes.Types.Keys.All(type =>
                actorTypes.TryGetValue(type, out DaprActorTypeInfo? actor)
                && actor.CountStatus == DaprActorCountStatus.Exact
                && actor.ActiveCount >= 0);
        string inventoryMessage = isInventoryComplete
            ? "All known EventStore actor types returned active counts from the EventStore sidecar metadata."
            : hasExactKnownCounts
                ? "Partial actor inventory: some known EventStore actor type counts are unavailable, so the active count is not total inventory."
                : "Active actor data unavailable: no authoritative source returned active counts for the known EventStore actor types.";

        return new DaprActorRuntimeInfo(
            orderedActorTypes,
            totalActive,
            config,
            remoteStatus,
            remoteEndpoint,
            isInventoryComplete,
            inventorySource,
            inventoryMessage,
            observedAt,
            KnownActorTypes.Types.Count);
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

        List<Task<ActorStateReadResult>> tasks = [];
        foreach (string stateKey in descriptor.StateKeys) {
            string? resolvedKey = KnownActorTypes.ResolveStateKey(stateKey, actorId);
            if (resolvedKey is null) {
                // Dynamic key family — report as not-found with the pattern as the display key
                tasks.Add(Task.FromResult(new ActorStateReadResult(
                    new DaprActorStateEntry(stateKey, null, 0, false),
                    false,
                    null)));
                continue;
            }

            tasks.Add(ReadActorStateKeyAsync(actorType, actorId, stateKey, resolvedKey, timeoutCts.Token));
        }

        ActorStateReadResult[] readResults;
        bool lookupUnavailable = false;
        string? unavailableMessage = null;
        try {
            readResults = await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            lookupUnavailable = true;
            unavailableMessage = "Actor lookup unavailable: state store reads timed out before the owner app id/key convention could be verified.";
            _logger.LogWarning("Actor state reads timed out after 5 seconds for {ActorType}/{ActorId}.", actorType, actorId);
            readResults = tasks
                .Select(t => t.IsCompletedSuccessfully
                    ? t.Result
                    : new ActorStateReadResult(new DaprActorStateEntry("timeout", null, 0, false), true, unavailableMessage))
                .ToArray();
        }

        if (!lookupUnavailable) {
            ActorStateReadResult unavailable = readResults.FirstOrDefault(r => r.LookupUnavailable);
            if (unavailable.LookupUnavailable) {
                lookupUnavailable = true;
                unavailableMessage = unavailable.Message;
            }
        }

        DaprActorStateEntry[] entries = readResults.Select(r => r.Entry).ToArray();
        long totalSize = entries.Sum(e => e.EstimatedSizeBytes);
        DaprActorLookupStatus lookupStatus = lookupUnavailable
            ? DaprActorLookupStatus.LookupUnavailable
            : entries.Any(e => e.Found)
                ? DaprActorLookupStatus.Available
                : DaprActorLookupStatus.NotFound;
        string message = lookupStatus switch {
            DaprActorLookupStatus.Available => "Actor state lookup completed using the configured EventStore owner app id.",
            DaprActorLookupStatus.NotFound => "Actor instance not found. The lookup path completed, but no known state key exists for this actor id.",
            _ => unavailableMessage ?? "Actor lookup unavailable: state store read failed or returned inconclusive evidence.",
        };

        return new DaprActorInstanceState(
            actorType,
            actorId,
            entries,
            totalSize,
            DateTimeOffset.UtcNow,
            lookupStatus,
            _options.EventStoreAppId,
            _options.StateStoreName,
            string.IsNullOrWhiteSpace(_options.EventStoreDaprHttpEndpoint)
                ? "DaprStateStoreActorKeys"
                : "RemoteOwnerSidecarActorStateApi",
            message);
    }

    /// <inheritdoc/>
    public async Task<DaprPubSubOverview> GetPubSubOverviewAsync(CancellationToken ct = default) {
        RemoteMetadataPayload remote = await TryReadRemoteMetadataAsync(ct).ConfigureAwait(false);

        IReadOnlyList<DaprComponentDetail> pubSubComponents = remote.Components is null
            ? []
            : remote.Components
                .Where(c => c.Category == DaprComponentCategory.PubSub)
                .Select(c => c with {
                    Source = DaprComponentSource.RemoteEventStoreMetadata,
                    HealthEvidenceSource = DaprComponentSource.RemoteEventStoreMetadata,
                })
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

    private async Task<ActorStateReadResult> ReadActorStateKeyAsync(
        string actorType, string actorId, string displayKey, string resolvedKey, CancellationToken ct) {
        if (!string.IsNullOrWhiteSpace(_options.EventStoreDaprHttpEndpoint)) {
            return await ReadActorStateKeyFromOwnerSidecarAsync(actorType, actorId, displayKey, resolvedKey, ct)
                .ConfigureAwait(false);
        }

        string composedKey = ComposeActorStateKey(_options.EventStoreAppId, actorType, actorId, resolvedKey);
        try {
            JsonElement? value = await _daprClient
                .GetStateAsync<JsonElement?>(_options.StateStoreName, composedKey, cancellationToken: ct)
                .ConfigureAwait(false);

            if (value is null) {
                return new ActorStateReadResult(new DaprActorStateEntry(displayKey, null, 0, false), false, null);
            }

            string jsonValue = FormatStateValue(value.Value);
            long size = Encoding.UTF8.GetByteCount(jsonValue);
            return new ActorStateReadResult(new DaprActorStateEntry(displayKey, jsonValue, size, true), false, null);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to read actor state key '{StateKey}' for {ActorType}/{ActorId}.", displayKey, actorType, actorId);
            return new ActorStateReadResult(
                new DaprActorStateEntry(displayKey, null, 0, false),
                true,
                $"Actor lookup unavailable: failed to read state key '{displayKey}' from state store '{_options.StateStoreName}'.");
        }
    }

    private async Task<ActorStateReadResult> ReadActorStateKeyFromOwnerSidecarAsync(
        string actorType, string actorId, string displayKey, string resolvedKey, CancellationToken ct) {
        string endpoint = _options.EventStoreDaprHttpEndpoint!.TrimEnd('/');
        string uri =
            $"{endpoint}/v1.0/actors/{Uri.EscapeDataString(actorType)}/{Uri.EscapeDataString(actorId)}/state/{Uri.EscapeDataString(resolvedKey)}";

        try {
            HttpClient client = _httpClientFactory.CreateClient();
            using HttpResponseMessage response = await client
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.NoContent) {
                return new ActorStateReadResult(new DaprActorStateEntry(displayKey, null, 0, false), false, null);
            }

            if (!response.IsSuccessStatusCode) {
                string message = $"Actor lookup unavailable: owner sidecar state API returned {(int)response.StatusCode} for state key '{displayKey}'.";
                _logger.LogWarning(
                    "Owner sidecar actor state read failed with {StatusCode} for {ActorType}/{ActorId}/{StateKey}.",
                    (int)response.StatusCode,
                    actorType,
                    actorId,
                    displayKey);
                return new ActorStateReadResult(new DaprActorStateEntry(displayKey, null, 0, false), true, message);
            }

            string value = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "null", StringComparison.OrdinalIgnoreCase)) {
                return new ActorStateReadResult(new DaprActorStateEntry(displayKey, null, 0, false), false, null);
            }

            string jsonValue = FormatStateValue(value);
            long size = Encoding.UTF8.GetByteCount(jsonValue);
            return new ActorStateReadResult(new DaprActorStateEntry(displayKey, jsonValue, size, true), false, null);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to read actor state key '{StateKey}' from owner sidecar for {ActorType}/{ActorId}.", displayKey, actorType, actorId);
            return new ActorStateReadResult(
                new DaprActorStateEntry(displayKey, null, 0, false),
                true,
                $"Actor lookup unavailable: failed to read state key '{displayKey}' from owner sidecar '{_options.EventStoreAppId}'.");
        }
    }

    private readonly record struct ActorStateReadResult(
        DaprActorStateEntry Entry,
        bool LookupUnavailable,
        string? Message);

    private static string FormatStateValue(JsonElement value)
        => value.ValueKind == JsonValueKind.String
            ? TruncateStateValueForDisplay(value.GetString() ?? string.Empty)
            : TruncateStateValueForDisplay(value.GetRawText());

    // Round 10 P16: cap input to 64 KiB before parsing arbitrary state-store payloads through
    // JsonDocument.Parse. A hostile or oversized actor state blob can otherwise stall the
    // request thread and consume large amounts of LOH while the parser walks the structure.
    // Above the cap, return the raw string truncated with a sentinel so operators see the
    // payload was unsafe to render rather than getting a silent slow-path render.
    private const int MaxFormatStateValueBytes = 64 * 1024;

    private static string FormatStateValue(string value) {
        if (value is null) {
            return string.Empty;
        }

        string capped = TruncateStateValueForDisplay(value);
        if (capped.Length != value.Length) {
            return capped;
        }

        try {
            JsonDocumentOptions options = new() { MaxDepth = 64 };
            using JsonDocument document = JsonDocument.Parse(value, options);
            return FormatStateValue(document.RootElement);
        }
        catch (JsonException) {
            return value;
        }
    }

    private static string TruncateStateValueForDisplay(string value)
        => value.Length > MaxFormatStateValueBytes
            ? string.Concat(value.AsSpan(0, MaxFormatStateValueBytes), "… [truncated, exceeds 64 KiB display cap]")
            : value;

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
        CancellationToken outerCt,
        CancellationToken probeCt) {
        try {
            _ = await _daprClient
                .GetStateAsync<string>(component.ComponentName, "admin:dapr-probe", cancellationToken: probeCt)
                .ConfigureAwait(false);

            // Success — store responded. Preserve remote inventory provenance when the row came
            // from the EventStore sidecar; the local probe owns Status/LastCheckUtc, but Source
            // still answers where the component inventory fact came from.
            //
            // Round 10 P6: when the original Source is Unavailable (Stage-3 synth row that had
            // no inventory evidence before the probe ran), upgrade Source to LocalAdminProbe.
            // The probe attestation IS the inventory evidence for synth rows — the row exists
            // only because we attempted a probe. Without this upgrade, the UI renders the row's
            // Source column as "unavailable" beside a Healthy probe badge — exactly the
            // truth-contract violation Round 5 P2 was trying to avoid in the opposite direction.
            return (component with {
                Status = HealthStatus.Healthy,
                LastCheckUtc = DateTimeOffset.UtcNow,
                HealthEvidenceSource = DaprComponentSource.LocalAdminProbe,
                Source = component.Source == DaprComponentSource.Unavailable
                    ? DaprComponentSource.LocalAdminProbe
                    : component.Source,
            }, true);
        }
        catch (OperationCanceledException) when (!outerCt.IsCancellationRequested) {
            // Probe timed out without the *outer caller* cancelling — treat as Unhealthy
            // evidence. A bounded probe that does not respond within the budget is not
            // "degraded but usable"; for AC1 purposes it is the same signal as an exception.
            //
            // The filter explicitly inspects `outerCt` rather than `probeCt`: at the point this
            // catch runs, `probeCt` is the linked CTS token and IS already cancelled (that is
            // exactly why GetStateAsync threw). Filtering on `probeCt.IsCancellationRequested`
            // would always be false, leaving the timeout branch unreachable and leaking a
            // "Healthy by metadata default" state-store row through the AC1 contract.
            _logger.LogWarning("State store probe timed out for {ComponentName} after 3 seconds.", component.ComponentName);
            return (component with {
                Status = HealthStatus.Unhealthy,
                LastCheckUtc = DateTimeOffset.UtcNow,
                HealthEvidenceSource = DaprComponentSource.LocalAdminProbe,
                Source = component.Source == DaprComponentSource.Unavailable
                    ? DaprComponentSource.LocalAdminProbe
                    : component.Source,
            }, true);
        }
        catch (OperationCanceledException) {
            // Outer caller cancelled — propagate cancellation to the request pipeline.
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "State store probe failed for {ComponentName}.", component.ComponentName);
            return (component with {
                Status = HealthStatus.Unhealthy,
                LastCheckUtc = DateTimeOffset.UtcNow,
                HealthEvidenceSource = DaprComponentSource.LocalAdminProbe,
                Source = component.Source == DaprComponentSource.Unavailable
                    ? DaprComponentSource.LocalAdminProbe
                    : component.Source,
            }, true);
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
    /// Only the *result* is cached; in-flight tasks would close over the first caller's
    /// CancellationToken and re-throw cancellation/transport faults to subsequent in-scope
    /// consumers. Caching the result instead means a cancelled first caller leaves the cache
    /// empty and the next consumer gets a fresh attempt with its own token.
    /// </remarks>
    private async Task<RemoteMetadataPayload> TryReadRemoteMetadataAsync(CancellationToken ct) {
        if (_cachedRemoteMetadata is { } cached) {
            return cached;
        }

        RemoteMetadataPayload result = await ReadRemoteMetadataAsync(ct).ConfigureAwait(false);
        // Store the result *after* the await so a cancellation throw above leaves the cache
        // null; subsequent callers retry with their own token. Concurrent callers may both
        // perform the read; the deterministic-per-request payload makes either outcome safe.
        _cachedRemoteMetadata = result;
        return result;
    }

    /// <summary>Returns the configured endpoint with any embedded userinfo (user:password)
    /// stripped. Falls back to a best-effort string scrub if the value is not a valid URI.</summary>
    private static string SanitizeEndpoint(string raw) {
        if (Uri.TryCreate(raw, UriKind.Absolute, out Uri? uri)) {
            if (string.IsNullOrEmpty(uri.UserInfo)) {
                return raw;
            }

            UriBuilder builder = new(uri) { UserName = string.Empty, Password = string.Empty };
            return builder.Uri.ToString();
        }

        int schemeEnd = raw.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0) {
            return raw;
        }

        int authorityStart = schemeEnd + 3;
        int authorityEnd = raw.IndexOfAny(['/', '?', '#'], authorityStart);
        int at = raw.IndexOf('@', authorityStart);
        if (at < 0 || (authorityEnd >= 0 && at > authorityEnd)) {
            return raw;
        }

        return string.Concat(raw.AsSpan(0, authorityStart), raw.AsSpan(at + 1));
    }

    private async Task<RemoteMetadataPayload> ReadRemoteMetadataAsync(CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(_options.EventStoreDaprHttpEndpoint)) {
            return new RemoteMetadataPayload {
                Status = RemoteMetadataStatus.NotConfigured,
                Endpoint = null,
            };
        }

        // Round 5 P14: strip credentials from the endpoint before assigning to the
        // RemoteMetadataPayload — `Endpoint` flows directly into UI tooltips and the round-4
        // empty-state Description copy. A misconfigured endpoint such as
        // `http://user:pass@host:3500` would echo credentials verbatim into rendered HTML.
        // The AC1 leak test pins response bodies but not this UI render path.
        string rawEndpoint = _options.EventStoreDaprHttpEndpoint;
        string endpoint = SanitizeEndpoint(rawEndpoint);
        string baseUrl = rawEndpoint.TrimEnd('/');

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

            // A successful 200 OK with parseable JSON is reported as Available regardless of
            // whether the component/actor/subscription arrays are empty; consumers that need
            // to distinguish "available but empty" from "no information" inspect the explicit
            // collections. (RemoteMetadataStatus.Initializing was removed in round 3 — it had
            // no reachable producer; if a future signal source becomes available file a story.)
            return new RemoteMetadataPayload {
                Status = RemoteMetadataStatus.Available,
                Endpoint = endpoint,
                Components = components.AsReadOnly(),
                Subscriptions = subscriptions.AsReadOnly(),
                Actors = actors.AsReadOnly(),
                SubscriptionCount = subCount,
                HttpEndpointCount = httpEndpointCount,
            };
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested) {
            _logger.LogWarning(
                ex,
                "Remote DAPR sidecar metadata unavailable at {Endpoint}: request timed out.",
                endpoint);
            return new RemoteMetadataPayload {
                Status = RemoteMetadataStatus.Unreachable,
                Endpoint = endpoint,
            };
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) {
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

    /// <summary>
    /// Reads the local Admin sidecar's <c>DaprClient.GetMetadataAsync</c> result once per request
    /// and caches the outcome. Same memoization rationale as the remote variant: returning a
    /// cached result (not an in-flight Task) keeps the four consumers on a single snapshot
    /// without poisoning later consumers when the first caller's CT is cancelled.
    /// </summary>
    private async Task<LocalMetadataResult> TryReadLocalMetadataAsync(CancellationToken ct) {
        if (_cachedLocalMetadata is { } cached) {
            return cached;
        }

        DaprMetadata? metadata;
        try {
            using var localMetadataCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            localMetadataCts.CancelAfter(TimeSpan.FromSeconds(3));
            metadata = await _daprClient.GetMetadataAsync(localMetadataCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested) {
            _logger.LogWarning(ex, "Local DAPR sidecar metadata unavailable: request timed out.");
            return new LocalMetadataResult {
                Available = false,
                Metadata = null,
            };
        }
        catch (OperationCanceledException) {
            // Caller-driven cancellation — propagate; do not poison the cache.
            throw;
        }
        catch (Exception ex) {
            // Cache-on-success only — mirror the remote pattern. Caching a failure result here
            // would poison the request scope: a transient socket exception on the first caller
            // would force every subsequent consumer (GetSidecarInfoAsync, GetActorRuntimeInfoAsync)
            // to read the same failure rather than retry on its own. Returning the failure
            // without caching means the next consumer gets a fresh attempt with its own token.
            _logger.LogWarning(ex, "Local DAPR sidecar metadata unavailable.");
            return new LocalMetadataResult {
                Available = false,
                Metadata = null,
            };
        }

        LocalMetadataResult result = new() {
            Available = metadata is not null,
            Metadata = metadata,
        };
        _cachedLocalMetadata = result;
        return result;
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

        if (root.ValueKind != JsonValueKind.Object) {
            throw new JsonException("Remote DAPR sidecar metadata root must be a JSON object.");
        }

        JsonElement componentsElement = RequiredArray(root, "components");
        JsonElement subscriptionsElement = RequiredArray(root, "subscriptions");

        if (componentsElement.ValueKind == JsonValueKind.Array) {
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
                    DaprComponentSource.RemoteEventStoreMetadata,
                    DaprComponentSource.RemoteEventStoreMetadata));
            }
        }

        if (subscriptionsElement.ValueKind == JsonValueKind.Array) {
            // subCount is finalised from the filtered subscriptions list below so
            // /dapr's "Active Subscriptions" stat card and /dapr/pubsub's filtered grid
            // agree on the same canonical payload. The unfiltered array length is
            // discarded — partial subscription entries (missing pubsubName or topic)
            // are not "active" by AC6 and must not inflate the count.
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

            subCount = subscriptions.Count;
        }

        if (root.TryGetProperty("actors", out JsonElement actorsElement)) {
            if (actorsElement.ValueKind != JsonValueKind.Array) {
                throw new JsonException("Remote DAPR sidecar metadata property 'actors' must be an array.");
            }

            foreach (JsonElement actorElement in actorsElement.EnumerateArray()) {
                string type = actorElement.TryGetProperty("type", out JsonElement typeEl)
                    ? typeEl.GetString() ?? string.Empty
                    : string.Empty;
                // Guard against non-integer JSON tokens for "count" (string "3", null, fractional).
                // GetInt32() throws InvalidOperationException for those, which the catch-all in
                // ReadRemoteMetadataAsync collapses into RemoteMetadataStatus.Unreachable —
                // misclassifying a malformed payload as a transport failure. TryGetInt32 keeps
                // parsing intact and falls back to -1 (the same sentinel already used for the
                // missing-property branch).
                int count = actorElement.TryGetProperty("count", out JsonElement countEl)
                    && countEl.ValueKind == JsonValueKind.Number
                    && countEl.TryGetInt32(out int parsedCount)
                    ? parsedCount
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

        if (root.TryGetProperty("httpEndpoints", out JsonElement httpEndpointsElement)) {
            if (httpEndpointsElement.ValueKind != JsonValueKind.Array) {
                throw new JsonException("Remote DAPR sidecar metadata property 'httpEndpoints' must be an array.");
            }

            httpEndpointCount = httpEndpointsElement.GetArrayLength();
        }

        return (components, subscriptions, actors, subCount, httpEndpointCount);
    }

    private static JsonElement RequiredArray(JsonElement root, string propertyName) {
        if (!root.TryGetProperty(propertyName, out JsonElement element)) {
            throw new JsonException($"Remote DAPR sidecar metadata property '{propertyName}' is required.");
        }

        if (element.ValueKind != JsonValueKind.Array) {
            throw new JsonException($"Remote DAPR sidecar metadata property '{propertyName}' must be an array.");
        }

        return element;
    }

    private readonly struct RemoteMetadataPayload {
        public RemoteMetadataStatus Status { get; init; }

        public string? Endpoint { get; init; }

        // Round 5 P15: expose as IReadOnlyList so a single shared payload cannot be mutated
        // by one consumer in a way that corrupts other in-scope consumers' views. The parser
        // wraps its mutable working lists in `.AsReadOnly()` before assignment below.
        public IReadOnlyList<DaprComponentDetail>? Components { get; init; }

        public IReadOnlyList<DaprSubscriptionInfo>? Subscriptions { get; init; }

        public IReadOnlyList<DaprActorTypeInfo>? Actors { get; init; }

        public int SubscriptionCount { get; init; }

        public int HttpEndpointCount { get; init; }
    }

    /// <summary>
    /// Memoized outcome of a local <c>DaprClient.GetMetadataAsync</c> read.
    /// <c>Available</c> is <c>true</c> when the call returned a non-null DaprMetadata; the
    /// caller still needs to inspect <c>Metadata</c> to decide whether the payload carries
    /// usable evidence (a sidecar may respond before its app-metadata has loaded, in which
    /// case <c>Components</c>/<c>Actors</c>/<c>Extended</c> can all be empty/null).
    /// </summary>
    private readonly struct LocalMetadataResult {
        public bool Available { get; init; }

        public DaprMetadata? Metadata { get; init; }
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
