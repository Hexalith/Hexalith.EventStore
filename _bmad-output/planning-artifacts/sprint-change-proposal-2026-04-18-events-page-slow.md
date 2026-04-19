# Sprint Change Proposal: Events Page Slow & Empty (AdminApi 5-Second Timeout)

**Date:** 2026-04-18
**Triggered by:** Live observation — `https://localhost:60034/events` takes several seconds to render and shows no events, even after commands have been submitted and the stream-activity index is populated.
**Scope Classification:** Minor — Direct implementation by dev team
**Related prior proposals:** `sprint-change-proposal-2026-04-10-events-page-empty.md` (Story 15.13 — stream-activity writer), `sprint-change-proposal-2026-04-01-events-page.md` (Story 15.12 — Events UI)

---

## Section 1: Issue Summary

**Symptom:** The Admin UI `/events` page takes 3–10 s to render and the grid plus stat cards remain empty (`Recent Events = 0`, `Unique Event Types = 0`, `Active Streams = 0`), even though the upstream `admin:stream-activity:all` index is populated correctly by `DaprStreamActivityTracker`.

**Root cause:** `src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs:81` configures the `AdminApi` named HttpClient with `client.Timeout = TimeSpan.FromSeconds(5)`. `Pages/Events.razor.LoadEventsAsync()` then fan-outs up to 50 timeline fetches with `MaxDegreeOfParallelism = 10` (five batches × ten calls). Each fetch traverses:

1. Admin.UI → Admin.Server (`/api/v1/admin/streams/{tenant}/{domain}/{aggregate}/timeline`)
2. Admin.Server → DAPR service-invoke → EventStore
3. EventStore → DAPR actor runtime → actor state read (first-access actor activation adds 1–2 s of tail latency)

Under the 5 s per-call cap, tail-latency streams reliably time out. Polly's Standard-Retry pipeline (visible in live logs as `log_id 1156: Resilience event OnRetry Result:503` on the timeline URL) retries once, then hands back a failure. `Events.razor:294-302` swallows every per-stream exception in `catch (Exception ex) { Logger.LogWarning(...) }`, leaving the grid empty with no user-visible diagnostic.

**Why prior stories didn't catch this:**

- **Story 15.12** (Events page cross-stream browser) used the same 5 s `AdminApi` timeout and only tested against local empty-state actors with sub-second activation.
- **Story 15.13** (stream-activity writer) fixed the *streams index* — the data that feeds the single fast call — and validated the happy path where timeline fetches returned quickly. The tail-latency regime was never exercised.

**Evidence gathered during investigation:**

1. **Aspire live status:** `keycloak`, `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `tenants`, and four DAPR sidecars all `Running` / `Healthy`. Admin UI reachable at `https://localhost:60034`. Infrastructure ruled out.
2. **Structured logs (`eventstore-admin-ui`):** repeated `Start processing HTTP request GET https://eventstore-admin/api/v1/admin/streams/tenant-a/counter/counter-1/timeline?*` with no matching "End processing" inside the 5 s window, followed by `Resilience event OnRetry Standard-Retry Result:503` on the same URL (`log_id 1156`). Control-plane calls on the same trace (`/health`, `/tenants`, `/types/aggregates`, `/streams`) all return 200 in <15 ms — so the UI-to-Admin.Server hop is healthy.
3. **Source:** `AdminUIServiceExtensions.cs:81` confirmed `client.Timeout = TimeSpan.FromSeconds(5)`.
4. **Source:** `Events.razor:242-350` confirmed the 50-way fan-out with silent catch.

---

## Section 2: Impact Analysis

### Epic Impact

| Epic | Impact |
|------|--------|
| **Epic 15 (Admin Web UI)** | One story needs a patch (Story 15.12 follow-up) — no new epic, no scope change. |
| All other epics | None. |

### Story Impact

| Story | Action |
|-------|--------|
| Story 15.12 (Events Page Cross-Stream Browser) | Append dated note pointing at this proposal. No AC change. |
| Story 15.13 (Stream Activity Tracker Writer) | No change — writer remains correct; timeline fetch tail latency is a separate layer. |

### Artifact Conflicts

| Artifact | Conflict | Action |
|----------|----------|--------|
| PRD | None | No FR text change. |
| Architecture | None | No new components, patterns, or contracts. |
| UX Design | None | Timeline-centric layout unchanged; adds a single warning banner. |
| epics.md | Minor | Optional dated note under Story 15.12. |
| Tests | None needed | Tier 1 behaviour unchanged; Tier 3 Playwright suite (if/when added) should assert the warning banner and a populated grid. |
| CI/CD / IaC / deployment | None | |

### Technical Impact

- **2 modified source files, 5 hunks total**
  - `src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs` — 1 hunk: bump `AdminApi` HttpClient timeout from `5` → `30` seconds.
  - `src/Hexalith.EventStore.Admin.UI/Pages/Events.razor` — 4 hunks: add `_failedStreamCount` / `_totalStreamCount` fields, reset/increment them in `LoadEventsAsync`, render a yellow warning banner after the stat-card row when `_failedStreamCount > 0`.
- **0 new files** / **0 API contract changes** / **0 schema changes** / **0 infrastructure changes**.
- **Build verified:** `dotnet build src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj --configuration Release` → 0 warnings, 0 errors (repo-wide `TreatWarningsAsErrors=true`).

---

## Section 3: Recommended Approach

**Selected path:** Direct Adjustment — Option 1 (targeted fix over architectural refactor).

**How it works:**

1. Raise the `AdminApi` HttpClient timeout from 5 s to 30 s. 30 s is the .NET `HttpClient` default and aligns with downstream `DaprStreamQueryService` timeouts already present in the codebase (blame = 30 s, bisect = 60 s). The control-plane calls finish in <15 ms and are unaffected; only slow tail-latency timeline calls benefit.
2. Make silent failures visible: add fields `_failedStreamCount` / `_totalStreamCount` to `Events.razor`, atomically increment the fail counter inside the per-stream catch, and render a warning banner stating `⚠ N of M stream timelines could not be loaded` when any remain. This converts "mystery empty page" into an actionable signal that tells the user to check Admin.Server logs.

**Why Option 1 over alternatives:**

- **Option 2 — Index-backed recent-events feed (e.g., `admin:event-activity:all`)**: Would collapse 51 HTTP hops into 1 state-store read. Architecturally cleaner long-term but explicitly deferred — the user directed us to "fix failing fetch" first and leave the architectural index for a future story. Effort: Medium; Risk: Low; Timeline impact: ~2 hrs + testing.
- **Option 3 — Rollback to Story 15.12 pre-fan-out design**: Would lose the cross-stream event browsing feature entirely. Effort: High; Risk: High (regresses shipped UX).
- **Option 1 (selected)**: Smallest blast radius. Fixes both symptoms with two-line + banner change. Effort: Low (~30 min); Risk: Low (no API/schema/infra changes).

**Effort estimate:** Low — applied and built in this session.
**Risk level:** Low — timeout extension is additive, banner is additive, no existing behaviour regressed.
**Timeline impact:** None.

---

## Section 4: Detailed Change Proposals

All edits were reviewed incrementally and approved individually during the course-correct workflow.

### 4.1 Modified — `AdminApi` HttpClient timeout

**File:** `src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs` (line 81)

```diff
- client.Timeout = TimeSpan.FromSeconds(5);
+ client.Timeout = TimeSpan.FromSeconds(30);
```

**Rationale:** Removes the primary cause of silent failures for the `/events` page. The 5 s ceiling was too aggressive for 50-way parallel actor-state reads; 30 s matches the .NET default and downstream blame/bisect budgets already used in `DaprStreamQueryService`. All control-plane calls (health, tenants, aggregates, streams-index) complete in single-digit milliseconds and are unaffected. User-initiated cancellation (navigation away, Events page dispose) still aborts in-flight calls via the existing CTS.

### 4.2 Modified — `Events.razor` field declarations

**File:** `src/Hexalith.EventStore.Admin.UI/Pages/Events.razor` (within `@code` block, around line 133)

```diff
  private CancellationTokenSource? _cts;
+ private int _failedStreamCount;
+ private int _totalStreamCount;
```

### 4.3 Modified — `Events.razor` `LoadEventsAsync` counter initialisation

**File:** `src/Hexalith.EventStore.Admin.UI/Pages/Events.razor` (`LoadEventsAsync`, around line 255)

```diff
  IReadOnlyList<StreamSummary> streams = streamsResult.Items;
  var perStreamResults = new ConcurrentBag<List<EventRow>>();
+ _totalStreamCount = streams.Count;
+ _failedStreamCount = 0;

  await Parallel.ForEachAsync(
```

### 4.4 Modified — `Events.razor` per-stream catch increments

**File:** `src/Hexalith.EventStore.Admin.UI/Pages/Events.razor` (around line 294)

```diff
  catch (Exception ex)
  {
+     Interlocked.Increment(ref _failedStreamCount);
      Logger.LogWarning(
          ex,
          "Failed to fetch timeline for stream {TenantId}/{Domain}/{AggregateId}, skipping",
          stream.TenantId,
          stream.Domain,
          stream.AggregateId);
  }
```

### 4.5 Modified — `Events.razor` warning banner

**File:** `src/Hexalith.EventStore.Admin.UI/Pages/Events.razor` (after stat-card row, around line 48)

```diff
      <StatCard Label="Active Streams" Value="@ComputeActiveStreams().ToString()" />
  </div>
+
+ @if (_failedStreamCount > 0)
+ {
+     <div role="alert" style="padding: 8px 12px; margin-bottom: 12px;
+          background: var(--colorPaletteYellowBackground2);
+          color: var(--colorPaletteYellowForeground2); border-radius: 4px; font-size: 13px;">
+         ⚠ @_failedStreamCount of @_totalStreamCount stream timelines could not be loaded.
+         Events from those streams are missing — check Admin.Server logs.
+     </div>
+ }
```

**Rationale:** Converts the previously silent failure path into a visible, accessible (`role="alert"`, yellow-on-yellow palette tokens) diagnostic. Uses Fluent UI design tokens so it honours light/dark theme automatically. Banner is absent on the happy path, so there is zero visual noise when everything works.

---

## Section 5: Implementation Handoff

**Scope:** Minor — direct implementation by dev team. All edits applied and built in this session.

**Verification checklist:**

| # | Check | Status |
|---|-------|--------|
| 1 | `dotnet build src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj --configuration Release` — 0 warnings, 0 errors | Done (this session) |
| 2 | `dotnet build Hexalith.EventStore.slnx --configuration Release` — full solution clean | Pending developer |
| 3 | `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/` (if present) — Tier 1 green | Pending developer |
| 4 | Rebuild `eventstore-admin-ui` resource in Aspire (live host) | Pending developer |
| 5 | Reload `https://localhost:60034/events` — page renders in 2–3 s; grid populated; stat cards show non-zero counts | Pending developer |
| 6 | Observe warning banner absent on happy path | Pending developer |
| 7 | Force a failure (stop eventstore momentarily, or tamper token) — verify banner appears with accurate `N of M` count and Admin.Server logs show the per-stream warnings | Pending developer |
| 8 | Verify `/streams` and `/commands` pages unaffected (they share the same `AdminApi` HttpClient) | Pending developer |

**New Story ID (optional):** `15-12a-admin-ui-events-page-timeout-and-diagnostics` — or append as a note under Story 15.12 in `epics.md`.

**Dependencies:** None.

---

## Appendix: Investigation Evidence Highlights

- **Aspire `list_resources`:** all resources `Healthy`, Admin UI at `https://localhost:60034`, Keycloak management probe `Healthy`.
- **Structured log (`eventstore-admin-ui`, log_id 1156):** `Resilience event OnRetry Standard-Retry Result:503` on `https://eventstore-admin/api/v1/admin/streams/tenant-a/counter/counter-1/timeline?*` — Polly retry emitted after client-side timeout transforms the in-flight request into a 503 signal.
- **Source reference:** `AdminUIServiceExtensions.cs:81` — `client.Timeout = TimeSpan.FromSeconds(5)`.
- **Source reference:** `Events.razor:258-303` — `Parallel.ForEachAsync(streams, new ParallelOptions { MaxDegreeOfParallelism = 10, ... })` with silent `catch (Exception ex) Logger.LogWarning` at line 294.
- **Source reference:** `DaprStreamQueryService.cs:236-237` — existing precedent for 30 s timeouts (`cts.CancelAfter(TimeSpan.FromSeconds(30))` for blame) and 60 s for bisect.
- **Unrelated side-finding:** repeated 401s on `POST http://localhost:49974/v1.0/invoke/eventstore/method/api/v1/queries` from `AdminTenantsController.ListTenants`. Distinct from this proposal — queries endpoint uses a different auth path — and should be filed as a separate issue.
