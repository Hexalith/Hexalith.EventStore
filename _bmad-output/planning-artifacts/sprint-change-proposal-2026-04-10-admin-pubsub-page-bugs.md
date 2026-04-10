# Sprint Change Proposal — Admin UI DAPR Pub/Sub Page Bugs

**Date:** 2026-04-10
**Author:** Scrum Master (bmad-correct-course workflow)
**Requested by:** Jerome
**Trigger:** User observation while testing the Admin UI after story 19-6 shipped
**Scope classification:** Minor (direct adjustment, single bug-fix story)
**Mode:** Incremental refinement (4 edits + 1 hot-fix approved individually)
**Status:** ✅ Implemented and verified by Jerome

---

## Section 1 — Issue Summary

While testing the Admin UI on a freshly restarted Aspire run (post story 19-6),
Jerome reported the following observations on the DAPR-related pages.
Investigation classified each as either **expected behaviour**, **architectural
bug**, or **content bug**.

### Observed issues

1. **`/dapr/actors`** — Active Instances column shows `N/A` for all three
   actor types (`AggregateActor`, `ETagActor`, `ProjectionActor`).
   → **Expected behaviour.** DAPR's `/v1.0/metadata` endpoint does not include
   per-type instance counts for remotely-hosted actors; the code stores `-1`
   and the UI renders it as `N/A`. Out of scope for this proposal.

2. **`/dapr/actors`** — Instance Lookup affordance unclear.
   → **Documentation gap, no code change.** Documented in conversation;
   not part of this proposal.

3. **`/dapr/pubsub`** — `Pub/Sub Components: 0` despite the EventStore
   sidecar having a `pubsub` reference.
   → **Architectural bug.** Admin.Server's `eventstore-admin` sidecar is
   wired with state-store references only
   (`HexalithEventStoreExtensions.cs:97-102` — *"intentionally does not
   reference the pub/sub component"*). The current
   `GetPubSubOverviewAsync` queries the LOCAL sidecar metadata, which is
   guaranteed to return zero pub/sub components. **Fix in Edit #1.**

4. **`/dapr/pubsub`** — Subscriptions section shows the banner
   *"EventStore sidecar unreachable. Attempted to query
   http://localhost:3501/v1.0/subscribe but the call failed."*
   The actor types page works (3 types displayed) using the same remote
   endpoint, so the asymmetry is unexplained. Manual Refresh does not
   recover the banner.
   → **Two distinct sub-issues:**
     - **Content bug** — the displayed URL is wrong: the code queries
       `/v1.0/metadata`, not `/v1.0/subscribe`. **Fix in Edit #2 part B.**
     - **Root cause of intermittent failure** — needs operator-level
       investigation via the enriched `LogWarning` from story 19-6.
       Treated as a **post-implementation diagnostic step**, not a code
       change. The content fix at least sends operators to the right URL.

5. **`/dapr/pubsub`** — Observability cards show
   *"Configure observability URL to enable"*.
   → **UX enhancement added mid-implementation.** Discussion with Jerome
   confirmed that the existing behaviour is correct for production
   (operator must set their own Grafana/Datadog/Application Insights
   URLs), but creates a poor zero-config dev experience. Resolved by
   auto-wiring the Aspire dashboard's built-in observability views
   (`/traces`, `/metrics`, `/structuredlogs`) from the Aspire extension.
   **Fix in Edit #4.**

6. **`/dapr/pubsub`** — The "Manage Dead Letters" button navigates to
   `/deadletters` which returns 404. The actual page is at
   `/health/dead-letters` (`DeadLetters.razor:1`).
   → **Content bug.** **Fix in Edit #3.**

### Evidence

- User screenshots and direct observation transcribed in conversation.
- Code inspection of:
  - `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs:313-440`
  - `src/Hexalith.EventStore.Admin.UI/Pages/DaprPubSub.razor:79-301`
  - `src/Hexalith.EventStore.Admin.UI/Pages/DeadLetters.razor:1`
  - `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs:78-102`
- Confirmation that `Hexalith.EventStore.Admin.Server.Host` does **not**
  call `AddActors()`/`MapActorsHandlers()` — therefore the actor types
  visible on `/dapr/actors` come from the remote fallback path, not the
  local sidecar metadata. This proves `/v1.0/metadata` is reachable from
  Admin.Server at least intermittently.
- Grep verification: only one occurrence of `"/deadletters"` in the
  codebase — no other orphan navigations.

### Categorisation

**Issue type:** Bug fix (architectural mis-wiring + two content bugs)
plus one dev-UX enhancement. No new requirement, no scope change, no
PRD impact. Four small edits in three files plus one hot-fix uncovered
during validation form one cohesive fix story.

---

## Section 2 — Impact Analysis

### Epic Impact

- **Current work:** follow-up to story **19-3** (DAPR Pub/Sub Delivery
  Metrics) and indirectly to **19-6** (DAPR metadata diagnostics). The
  pub/sub components are wired correctly at the AppHost level but the
  query path shipped in 19-3 doesn't actually reach them, and 19-6 didn't
  catch this because it focused on the diagnostic enum and message wording
  for the *unreachable* / *not configured* states.
- **No new epic required.** Fits inside the existing Admin UI epic as a
  bug-fix story.
- **No future epics invalidated.** No re-sequencing required.

### Story Impact

- **New story:** *"Fix Admin UI Pub/Sub page bugs (components from remote
  sidecar, message URL, dead-letter route)"* — recommended ID under the
  Admin UI epic, follow-up to 19-3 and 19-6.
- **In-flight stories:** none affected.
- **Completed stories:** none rolled back.

### Artifact Conflicts

| Artifact | Impact |
|---|---|
| PRD (`prd.md`) | None — no scope change |
| Epics (`epics.md`) | Add one bug-fix story under Admin UI epic |
| Architecture (`architecture.md`) | None — wiring topology unchanged |
| UX specifications | None — labels/empty-state copy only |
| `sprint-status.yaml` | Add new story entry with status `backlog` |

### Technical Impact

- **4 files touched:**
  1. `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs`
     — `GetPubSubOverviewAsync` rewrite (Edit #1) + rules-array hot-fix
  2. `src/Hexalith.EventStore.Admin.UI/Pages/DaprPubSub.razor`
     — empty state (Edit #2A), message text (Edit #2B), button route (Edit #3)
  3. `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs`
     — observability URL injection (Edit #4)
  4. `src/Hexalith.EventStore.Admin.Server/Configuration/AdminServerOptions.cs`
     — XML doc update on Trace/Metrics/LogsUrl (Edit #4)
- **Test impact:**
  - `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprPubSubQueryServiceTests.cs`
    — 4 test cases rewritten to source pub/sub components from the remote
    JSON payload (instead of the local sidecar mock).
  - `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DaprPubSubPageTests.cs`
    — no changes required (the existing tests mock the API client and the
    new 3-way empty state's `Available` branch reuses the existing copy).
- **No infrastructure changes.** No DAPR component YAML changes. No NuGet
  package version bumps.
- **Breaking change surface:** **none in published artifacts.** The Aspire
  extension adds 3 new optional environment variables on the Admin.Server
  resource — these are pre-filled by the extension under Aspire only, and
  ignored entirely in production where the operator sets their own
  observability URLs via standard `appsettings.Production.json` or
  Helm/Kubernetes env vars. The
  `Hexalith.EventStore.Aspire` package's public API is unchanged.

---

## Section 3 — Recommended Approach

### Selected path: **Option 1 — Direct Adjustment**

**Rationale:**

- **Effort:** Low. Two files, one rewritten method, two one-line UI fixes,
  one new 3-way switch in the empty state.
- **Risk:** Low. The rewritten method consolidates two HTTP-derived data
  sources into one (was: components from local + subscriptions from
  remote; becomes: both from remote). Net effect: one *fewer* HTTP call
  per page load.
- **Timeline impact:** negligible — fits inside one sprint as a single
  bug-fix story (~½ day of dev effort).
- **Alternatives rejected:**
  - *Option 2 (Rollback)*: stories 19-3 and 19-6 shipped useful
    functionality; rollback would lose the entire pub/sub page for a
    fixable mis-wiring.
  - *Option 3 (MVP review)*: no scope or requirement impact; MVP
    unchanged.
  - *Add `pubSub` reference to the `eventstore-admin` sidecar*
    (alternative architecture for Edit #1): rejected because it adds
    a dependency Admin.Server doesn't actually use at runtime, and
    duplicates a component already present on the EventStore sidecar.
    Querying the canonical owner (EventStore sidecar) is more correct.

### Effort estimate (actual, post-implementation)

| Phase | Effort |
|---|---|
| Edit #1 — `GetPubSubOverviewAsync` rewrite | ~1 hour |
| Edit #2 — UI empty state + text fix | ~15 minutes |
| Edit #3 — route fix | ~5 minutes |
| Hot-fix — `rules` array parsing (uncovered during validation) | ~15 minutes |
| Edit #4 — Aspire observability wiring + XML docs | ~15 minutes |
| Test updates (Server + UI) | ~30 minutes |
| Manual verification under Aspire (2 restart cycles) | ~20 minutes |
| **Total actual** | **~2.5 hours** |

### Risk assessment (post-implementation review)

| Risk | Likelihood | Outcome |
|---|---|---|
| Remote `/v1.0/metadata` returns differently-shaped JSON for components | Low | ✅ Defensive parsing held up; components extracted correctly |
| Test mocks need IHttpClientFactory rework | Medium | ✅ Done — 4 cases rewritten, all green |
| Race condition root cause (item #4 sub-issue) is not fixed | High | ⚠️ Resolved in practice — once components were sourced from remote, the "unreachable" issue disappeared. Item revealed by validation was actually a separate JSON-parsing bug (rules array shape), now fixed. |
| `/health/dead-letters` route is itself wrong | Very low | ✅ Verified manually; navigation works |
| Hot-fix surfaced during validation | **Realised** | The `rules` parser expected `{"rules": {"rules": [...]}}` (legacy test fixture shape) but the real DAPR payload returns `"rules": [...]` directly. A `JsonElement.TryGetProperty` call on an array threw `InvalidOperationException`. **Fix:** parser now accepts both shapes via `ValueKind` switching. |
| Aspire dashboard URL hardcoded to `https://localhost:17017` | Low (dev only) | Acceptable: Aspire's default port is 17017 since v9; production deployments do not consume the Aspire extension at all. Documented in XML doc. |

---

## Section 4 — Detailed Change Proposals

The four edits below (plus one hot-fix uncovered during validation) were
refined incrementally and approved individually by Jerome.

### Edit #1 — Read pub/sub components from the remote EventStore sidecar

**File:** `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs`
**Method:** `GetPubSubOverviewAsync` (lines 313-440)

**Problem:**
The current implementation reads pub/sub components from the LOCAL
Admin.Server sidecar (`eventstore-admin`) via `_daprClient.GetMetadataAsync()`.
That sidecar is wired with state-store references only
(`HexalithEventStoreExtensions.cs:97-102`), so this query is guaranteed to
return zero components — producing the misleading "0 pub/sub components found"
message even when pub/sub is correctly configured on the EventStore sidecar.

**Fix:**
Replace the local-sidecar query with a remote query against
`{EventStoreDaprHttpEndpoint}/v1.0/metadata`. The same response payload is
already parsed for subscriptions in the same method, so the new logic merges
both extractions into a single HTTP round-trip.

**New shape of the method body** (replaces lines 316-413; the section after
`remoteFetchSucceeded = true` and the status/return computation are
unchanged):

```csharp
// 1. Query the EventStore sidecar's metadata once to get BOTH pub/sub
//    components AND subscriptions. We deliberately do NOT read the local
//    Admin.Server sidecar's components here, because the 'eventstore-admin'
//    sidecar (HexalithEventStoreExtensions.cs:97-102) is wired with
//    state-store references only — it never sees the pub/sub component.
//    The pub/sub component lives on the 'eventstore' sidecar, queryable
//    via {EventStoreDaprHttpEndpoint}/v1.0/metadata.
List<DaprComponentDetail> pubSubComponents = [];
List<DaprSubscriptionInfo> subscriptions = [];
bool remoteFetchSucceeded = false;

if (string.IsNullOrWhiteSpace(_options.EventStoreDaprHttpEndpoint))
{
    _logger.LogDebug("Skipping remote EventStore sidecar metadata query: endpoint not configured.");
}
else
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

        // 1a. Extract pub/sub components from the remote sidecar's components array.
        if (doc.RootElement.TryGetProperty("components", out JsonElement componentsElement)
            && componentsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement comp in componentsElement.EnumerateArray())
            {
                string? name = comp.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
                string? type = comp.TryGetProperty("type", out JsonElement t) ? t.GetString() : null;
                string? version = comp.TryGetProperty("version", out JsonElement v) ? v.GetString() : null;

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(type)
                    || DaprComponentCategoryHelper.FromComponentType(type) != DaprComponentCategory.PubSub)
                {
                    continue;
                }

                List<string> capabilities = [];
                if (comp.TryGetProperty("capabilities", out JsonElement capsEl)
                    && capsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement cap in capsEl.EnumerateArray())
                    {
                        string? capValue = cap.GetString();
                        if (!string.IsNullOrEmpty(capValue))
                        {
                            capabilities.Add(capValue);
                        }
                    }
                }

                pubSubComponents.Add(new DaprComponentDetail(
                    name,
                    type,
                    DaprComponentCategory.PubSub,
                    version ?? string.Empty,
                    HealthStatus.Healthy,
                    DateTimeOffset.UtcNow,
                    capabilities));
            }
        }

        // 1b. Extract subscriptions from the same payload (existing logic preserved verbatim).
        if (doc.RootElement.TryGetProperty("subscriptions", out JsonElement subscriptionsElement)
            && subscriptionsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement sub in subscriptionsElement.EnumerateArray())
            {
                string? pubsubName = sub.TryGetProperty("pubsubName", out JsonElement pn) ? pn.GetString() : null;
                string? topic = sub.TryGetProperty("topic", out JsonElement t) ? t.GetString() : null;
                string? type = sub.TryGetProperty("type", out JsonElement ty) ? ty.GetString() : null;
                string? deadLetterTopic = sub.TryGetProperty("deadLetterTopic", out JsonElement dlt) ? dlt.GetString() : null;

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

        remoteFetchSucceeded = true;
    }
    catch (OperationCanceledException)
    {
        throw;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(
            ex,
            "Remote DAPR sidecar metadata unavailable at {Endpoint}. ExceptionType={ExceptionType}. Check whether DAPR sidecar for 'eventstore' is running on that port (port conflicts on 3501 cause silent fallback).",
            _options.EventStoreDaprHttpEndpoint,
            ex.GetType().Name);
    }
}

// Status computation and return statement: unchanged from the current
// implementation (lines 429-440).
```

**Benefits:**

- ✅ Eliminates "0 pub/sub components found" when the sidecar is reachable
- ✅ One fewer HTTP call per page load (components and subscriptions
  share a single round-trip)
- ✅ Cleaner failure semantics: if remote is unreachable, **both** the
  components list and subscriptions list are empty, and the same
  `RemoteMetadataStatus` value drives both UI sections — no more
  visual contradictions

---

### Hot-fix #1.5 — Tolerant `rules` array parsing (uncovered during validation)

**File:** `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs`
**Discovery:** After Edit #1 was deployed, the first AppHost run produced
a runtime exception logged with trace ID `76487cef8812d0c514081d57f811d45e`:

```
System.InvalidOperationException: The requested operation requires an element
of type 'Object', but the target element has type 'Array'.
   at System.Text.Json.JsonElement.TryGetProperty(String propertyName, JsonElement& value)
   at DaprInfrastructureQueryService.GetPubSubOverviewAsync(...) line 399
```

**Root cause:** The pre-existing subscription parser (untouched by Edit #1)
expected `rules` as a wrapped object `{"rules": {"rules": [...]}}` — which
matched the legacy test fixtures from story 19-3 — but the real DAPR
`/v1.0/metadata` payload returns `"rules": [...]` as a **direct array**.
Calling `JsonElement.TryGetProperty("rules", ...)` on an array element
throws because `TryGetProperty` only works on JSON objects.

This bug was latent: it never surfaced before because the remote query
itself never succeeded (the broader Edit #1 architectural issue masked it).
Once components were correctly sourced from the remote payload, the
subscription parser was reached and crashed.

**Fix:** make the parser tolerant of both shapes via `ValueKind` checks:

```csharp
// Extract route from rules[].path. DAPR /v1.0/metadata returns
// 'rules' as a direct array of {match, path} objects. We also tolerate
// a legacy wrapped form '{"rules": {"rules": [...]}}' for backward
// compatibility with prior test fixtures.
string route = "/";
if (sub.TryGetProperty("rules", out JsonElement rulesElement))
{
    JsonElement rulesArray = rulesElement.ValueKind == JsonValueKind.Object
        && rulesElement.TryGetProperty("rules", out JsonElement nestedRules)
        && nestedRules.ValueKind == JsonValueKind.Array
            ? nestedRules
            : rulesElement;

    if (rulesArray.ValueKind == JsonValueKind.Array)
    {
        foreach (JsonElement rule in rulesArray.EnumerateArray())
        {
            if (rule.ValueKind == JsonValueKind.Object
                && rule.TryGetProperty("path", out JsonElement pathElement))
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
}
```

**Verification:** all 10 `DaprPubSubQueryServiceTests` cases (including
the legacy wrapped-rules fixtures) remain green; the AppHost no longer
throws when refreshing `/dapr/pubsub`.

**Lessons learned:**

- **Test fixtures should mirror real wire formats**, not the parser's
  internal expectations. The original story 19-3 test JSON used a
  hand-crafted shape that didn't match what DAPR actually emits.
- **Defensive `ValueKind` checking** belongs everywhere we navigate
  JSON returned from external sources, even when we control the
  surrounding code.
- This kind of latent bug is exactly why **end-to-end manual smoke
  tests under Aspire** are non-negotiable in this restart procedure.

---

### Edit #2 — UI: 3-way empty state for components + correct message text

**File:** `src/Hexalith.EventStore.Admin.UI/Pages/DaprPubSub.razor`

**Part A — 3-way empty state for "Pub/Sub Components" section**

Current code (lines 79-84):

```razor
@* Empty state: No pub/sub components (AC9) *@
@if (!_isLoading && _overview is not null && _overview.PubSubComponents.Count == 0 && _error is null)
{
    <EmptyState Title="No pub/sub components found"
                Description="Check DAPR component configuration. Ensure pub/sub components (e.g., pubsub.redis, pubsub.kafka) are registered in the DAPR component manifests." />
}
```

New code (replaces lines 79-84):

```razor
@* Empty states: No pub/sub components — distinguishes the three remote-metadata states *@
@if (!_isLoading && _overview is not null && _overview.PubSubComponents.Count == 0 && _error is null)
{
    @switch (_overview.RemoteMetadataStatus)
    {
        case RemoteMetadataStatus.NotConfigured:
            <EmptyState Title="Pub/sub components unavailable"
                        Description="Remote EventStore sidecar metadata is disabled. Set 'AdminServer:EventStoreDaprHttpEndpoint' in appsettings (under Aspire, this is wired automatically)." />
            break;
        case RemoteMetadataStatus.Unreachable:
            <EmptyState Title="EventStore sidecar unreachable"
                        Description="@($"Cannot list pub/sub components — the query to {_overview.RemoteEndpoint}/v1.0/metadata failed. Verify the EventStore DAPR sidecar is running on that port. Check Admin server logs for the exception details.")" />
            break;
        case RemoteMetadataStatus.Available:
            <EmptyState Title="No pub/sub components found"
                        Description="The EventStore sidecar is reachable but reports no pub/sub components. Check that 'pubsub' is registered as a DAPR component and referenced by the EventStore sidecar in HexalithEventStoreExtensions.cs." />
            break;
    }
}
```

**Part B — Correct the misleading URL in the subscriptions banner**

Current code (lines 136-139):

```razor
case RemoteMetadataStatus.Unreachable:
    <IssueBanner Visible="true"
                 Title="EventStore sidecar unreachable"
                 Description="@($"Attempted to query {_overview.RemoteEndpoint}/v1.0/subscribe but the call failed. Verify the EventStore DAPR sidecar is running. Check Admin server logs for details.")" />
    break;
```

New code:

```razor
case RemoteMetadataStatus.Unreachable:
    <IssueBanner Visible="true"
                 Title="EventStore sidecar unreachable"
                 Description="@($"Attempted to query {_overview.RemoteEndpoint}/v1.0/metadata but the call failed. Verify the EventStore DAPR sidecar is running. Check Admin server logs for details.")" />
    break;
```

Diff: a single word — `subscribe` → `metadata`. The backend code at
`DaprInfrastructureQueryService.cs:364` queries `/v1.0/metadata`, never
`/v1.0/subscribe`.

---

### Edit #3 — Fix the "Manage Dead Letters" button route

**File:** `src/Hexalith.EventStore.Admin.UI/Pages/DaprPubSub.razor`

Current code (lines 294-298):

```razor
<FluentButton Appearance="Appearance.Accent"
              OnClick="@(() => NavigationManager.NavigateTo("/deadletters"))"
              aria-label="Navigate to Dead Letter Manager">
    Manage Dead Letters
</FluentButton>
```

New code:

```razor
<FluentButton Appearance="Appearance.Accent"
              OnClick="@(() => NavigationManager.NavigateTo("/health/dead-letters"))"
              aria-label="Navigate to Dead Letter Manager">
    Manage Dead Letters
</FluentButton>
```

Diff: `/deadletters` → `/health/dead-letters`. This matches the actual
page route declared at `DeadLetters.razor:1` (`@page "/health/dead-letters"`).

**Orphan check:** grep verified — only one occurrence of `"/deadletters"`
in the entire codebase, and it is the line being fixed. No other
navigation sites need updating.

---

### Edit #4 — Auto-wire ObservabilityLinks to the Aspire dashboard (dev only)

**Files:**
- `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs`
- `src/Hexalith.EventStore.Admin.Server/Configuration/AdminServerOptions.cs`
  (XML doc only)

**Background discussion (added mid-implementation):**
Jerome questioned the "Configure observability URL to enable" placeholder
on the Pub/Sub page's three observability cards (Traces, Metrics, Logs)
and asked whether this was normal. Investigation showed:

- `AdminServerOptions.TraceUrl / MetricsUrl / LogsUrl` are nullable strings
  intended to point at an external observability dashboard
  (Grafana / Datadog / Application Insights / etc.)
- They are correctly read by `DaprHealthQueryService.cs:146` and surfaced
  via `SystemHealthReport.ObservabilityLinks`
- They are simply **never set** in dev — leaving the cards empty

**Production design (unchanged):**
The operator sets these URLs in `appsettings.Production.json`, Helm
values, or environment variables, pointing at their actual observability
stack. The Admin UI then renders working "Open Trace Dashboard" buttons.

**Dev gap (now closed):**
In Aspire dev, there is **no** external observability stack — but Aspire
itself ships a built-in dashboard at `https://localhost:17017` with
dedicated views for traces, metrics, and structured logs (collected from
all orchestrated resources via OTLP). Wiring these as the dev defaults
gives a zero-config functional experience without affecting production.

**Aspire extension change:**

```csharp
// Pre-fill the Admin UI's observability deep-links with the Aspire dashboard URLs
// for a zero-config dev experience. These env vars are ONLY injected under Aspire
// orchestration (this extension never runs in production deployments). In prod the
// operator sets AdminServer:TraceUrl / MetricsUrl / LogsUrl in their own appsettings
// (or env vars / Helm values) pointing to their actual observability stack
// (Grafana, Datadog, Application Insights, etc.).
// The Aspire dashboard default port is 17017; override these env vars if you run
// the dashboard on a different port.
const string AspireDashboardBaseUrl = "https://localhost:17017";
_ = adminServer
    .WithReference(eventStore)
    .WithEnvironment("AdminServer__EventStoreDaprHttpEndpoint", eventStoreEndpointUrl)
    .WithEnvironment("AdminServer__TraceUrl",   AspireDashboardBaseUrl + "/traces")
    .WithEnvironment("AdminServer__MetricsUrl", AspireDashboardBaseUrl + "/metrics")
    .WithEnvironment("AdminServer__LogsUrl",    AspireDashboardBaseUrl + "/structuredlogs")
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions {
            AppId = "eventstore-admin",
            Config = adminServerDaprConfigPath,
        })
        .WithReference(stateStore));
```

**XML doc update on `AdminServerOptions`** (3 properties):

```csharp
/// <summary>
/// Gets or sets the observability trace dashboard URL. Null disables the link.
/// </summary>
/// <remarks>
/// In Aspire dev orchestration, this is auto-wired to the Aspire dashboard's traces view
/// (https://localhost:17017/traces) by HexalithEventStoreExtensions.
/// In production, the operator must set this to their own observability stack URL
/// (e.g., Grafana, Datadog, Application Insights) via appsettings or environment variables.
/// </remarks>
public string? TraceUrl { get; set; }
// (same pattern for MetricsUrl → /metrics, LogsUrl → /structuredlogs)
```

**Production safety analysis:**

| Concern | Reality |
|---|---|
| Will `localhost:17017` end up in prod? | **No.** `HexalithEventStoreExtensions.cs` is part of the `Hexalith.EventStore.Aspire` package, consumed exclusively by the `Hexalith.EventStore.AppHost` project (a dev orchestrator). Production deployments deploy `Hexalith.EventStore.Admin.Server.Host` directly via Kubernetes / Helm / Container Apps, **never** going through the Aspire AppHost. The `WithEnvironment(...)` calls never execute in prod. |
| Can the operator still override? | **Yes.** Standard ASP.NET Core configuration precedence applies: `appsettings.Production.json` > environment variables. The operator sets `AdminServer__TraceUrl=https://grafana.mycompany.com/...` and the Aspire defaults are never seen in prod anyway. |
| Will the Aspire dashboard port (17017) drift? | **Stable.** Aspire's default dashboard port has been 17017 since v9. If a project overrides it, the operator can override the env var. Documented in the extension comment. |
| Anti-pattern risk | If someone deploys the AppHost itself to production (instead of the individual services), `localhost:17017` would appear in prod. This is an unsupported topology and should be caught in image / deployment review. The XML doc on `AdminServerOptions` flags the dev-only nature of the defaults. |

**Benefits:**

- ✅ Zero-config dev experience: clicking "Open Trace Dashboard" on
  `/dapr/pubsub` (or `/health`) opens the Aspire dashboard's relevant
  view directly
- ✅ No production impact whatsoever (Aspire extension is dev-only)
- ✅ Operator override path is preserved (standard ASP.NET Core config)
- ✅ XML doc clarifies the contract for future maintainers and prod
  operators

**Test impact:** none. No new test cases are required — the change is
purely a configuration injection in the dev orchestrator, and the
existing UI tests already cover the "links present" branch via
`PubSubPage_RendersObservabilityLinks_WhenUrlsConfigured`.

---

## Section 5 — Implementation Handoff

### Change scope classification

**Minor** — direct implementation, no backlog reorganisation, no
architectural review, no PRD/UX edits.

### Implementation log

| Step | Status |
|---|---|
| Edit #1 — `GetPubSubOverviewAsync` rewrite | ✅ Implemented |
| Edit #2A — UI 3-way empty state for components | ✅ Implemented |
| Edit #2B — UI text fix (`/v1.0/subscribe` → `/v1.0/metadata`) | ✅ Implemented |
| Edit #3 — Dead letters route fix | ✅ Implemented |
| Hot-fix #1.5 — `rules` array parsing | ✅ Implemented (uncovered during validation) |
| Edit #4 — Aspire observability auto-wiring + XML docs | ✅ Implemented |
| Server tests (10/10) | ✅ Green |
| UI tests (11/11) | ✅ Green |
| Build Release (0 warning, 0 error) | ✅ |
| Manual validation under Aspire (Jerome) | ✅ Confirmed: 1 pub/sub component visible, no banner, subscriptions section clean |

### Success criteria (post-implementation)

1. ✅ **Pub/Sub Components = 1** (the `pubsub` / `pubsub.redis` component)
   instead of "No pub/sub components found"
2. ✅ **Empty-state messages distinguish the 3 states** for the components
   section: not-configured / unreachable / reachable-but-empty
3. ✅ **Subscription banner** (when present) shows the correct URL
   (`/v1.0/metadata`)
4. ✅ **"Manage Dead Letters" button** navigates to `/health/dead-letters`
   (no 404)
5. ✅ **Tests green** — 10/10 Server, 11/11 UI
6. ✅ **Manual smoke test** under the standard restart procedure
   (Redis flush → build → `aspire run`) shows corrected behaviour
7. ✅ **Observability cards** are wired to the Aspire dashboard in dev
   (`https://localhost:17017/traces`, `/metrics`, `/structuredlogs`)
   and remain operator-configurable in production via standard
   ASP.NET Core configuration

### Items resolved during validation

- **The intermittent "EventStore sidecar unreachable" banner** turned
  out to be the latent `rules`-array parsing bug (Hot-fix #1.5), not a
  timing/connectivity issue. After Edit #1 succeeded in reaching the
  remote sidecar, the parser crashed inside the success path, which
  the calling code converted into a `RemoteMetadataStatus.Unreachable`
  via the catch block. Once the parser was made tolerant of the actual
  DAPR shape, the banner disappeared permanently.
- **The post-implementation diagnostic step** described in the original
  draft (collecting the enriched `LogWarning` from story 19-6 to identify
  `SocketException` / `HttpRequestException` / `TaskCanceledException`)
  is therefore **not needed** — Jerome confirmed visually that
  `/dapr/pubsub` shows correct data after the hot-fix.

### Items remaining out of scope (future enhancement candidates)

- **Active Instances "N/A"** on `/dapr/actors` — DAPR's `/v1.0/metadata`
  does not return per-type instance counts for remotely hosted actors.
  UI improvement opportunity: render `"—"` with a tooltip explaining
  DAPR's distributed actor model.
- **Dynamic Aspire dashboard port discovery** — the Aspire extension
  currently hardcodes `https://localhost:17017`. This is the documented
  default since Aspire v9. If a project overrides the port, the operator
  must override the env vars too. A future enhancement could read the
  dashboard port from `IDistributedApplicationBuilder` if/when the
  CommunityToolkit exposes it.

---

## Checklist Completion Summary

| Section | Status |
|---|---|
| 1. Understand trigger and context | ✅ Done |
| 2. Epic impact assessment | ✅ Done (no epic changes required) |
| 3. Artifact conflict analysis | ✅ Done |
| 4. Path forward evaluation | ✅ Done — Option 1 selected |
| 5. Sprint Change Proposal components | ✅ Done |
| 6. Final review and handoff | ✅ Approved by Jerome and implemented |

---

**Approval status:** ✅ Approved by Jerome on 2026-04-10. All edits
implemented, tested, and visually validated.
