# Story: admin-ui-health-dapr-truthfulness-fix

Status: review

Context created: 2026-05-07
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-admin-ui-manual-test-suite-issues.md`
Triggering evidence: `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues-2026-05-07.md`
Scope: Issues #6, #7, and #8 only.

## Story

As an EventStore operator using the Admin UI,
I want the health and DAPR infrastructure pages to keep working when Redis is down and to report metric/component availability honestly,
so that I can diagnose state-store and pub/sub failures without seeing blank pages, fake zeros, or missing DAPR components.

## Operator Decision Outcome

After this fix, an operator can determine from the Admin UI whether the system is operational, degraded by state-store failure, missing DAPR metadata, reporting valid zero activity, or showing stale/unknown evidence. The pages should answer "what can I trust right now?" before they answer "what numbers are available?"

## Issue Traceability

| Issue | Failing symptom | Covered by | Required evidence |
| --- | --- | --- | --- |
| #6 | `/health` becomes blank, stuck, or globally failed when Redis/state-store is unavailable. | AC1, AC2, AC7, AC8 | Server partial-report tests; Health page no-blank tests; Redis stop/start Aspire evidence. |
| #7 | Home and Health disagree on metric truthfulness; unavailable metrics render as fake zero on `/health`. | AC3, AC7, AC8 | Shared metric rendering tests; valid-zero vs unavailable fixtures; Home/Health screenshots or payload notes. |
| #8 | DAPR pages disagree about components/pubsub/subscriptions/history because metadata sources are mixed or hidden. | AC4, AC5, AC6, AC7, AC8 | Canonical inventory tests; remote sidecar metadata fixtures; cross-page component/subscription comparison evidence. |

## Operator Truth Contract

The implementation must make these states distinguishable in API payloads, UI labels, and tests. Do not infer health from numeric defaults or from a component being absent from a locally scoped sidecar.

Truth model:

- Health means whether a dependency is usable now for Admin/EventStore operations.
- Metrics mean measured activity values plus explicit value availability status.
- Inventory means what a named DAPR sidecar reported as loaded/configured/active.
- History means sampled evidence plus persistence/read status.
- Unknown or unavailable evidence must never render as healthy, zero, or empty.

Allowed status vocabulary:

| Term | Meaning | UI rule |
| --- | --- | --- |
| `Healthy` | A dependency probe or component health check succeeded. | May render as healthy/green only when backed by current evidence. |
| `Unhealthy` | A dependency probe failed or a required operational dependency is not usable. | Must render as non-green and explain the affected dependency. |
| `Available` | A metric or metadata value was successfully computed from current evidence. | Values, including zero, may be displayed as real data. |
| `Unavailable` | A metric, metadata source, or history source could not be read. | Must not be displayed or aggregated as zero or healthy. |
| `Stale` | Last-good data is being reused after a newer fetch failed. | Must show stale label plus timestamp/source where available. |
| `NotConfigured` | A metadata source endpoint is intentionally absent from configuration. | Must explain that remote evidence is not configured. |
| `Unreachable` | A configured metadata source could not be reached within the bounded probe. | Must explain that remote evidence is unavailable, not empty. |
| `InvalidPayload` | A metadata source responded but could not be parsed or lacked required shape. | Must explain invalid metadata and avoid fake component/subscription counts. |
| `Initializing` | A sidecar or app is reachable but not ready or has not loaded metadata yet. | Must render as temporary non-green/unknown evidence, not healthy. |

Avoid unmapped synonyms such as "missing", "empty", "offline", or "issue" in API fields or assertions. Visible UI copy may be friendlier, but tests must map it back to this vocabulary.

| Scenario | API contract | UI contract | Inventory/history contract |
| --- | --- | --- | --- |
| Redis or configured DAPR state store is unreachable | `/api/v1/admin/health` returns HTTP 200 with a partial `SystemHealthReport`, bounded by the configured probe timeout. Overall status is `Unhealthy`; the state-store component row is `Unhealthy` or `Unavailable`; dependent metrics use `SystemHealthMetricStatus.Unavailable`; exception details are not exposed. | `/health` renders the partial report, shows the state-store outage, exits loading, and labels unavailable metrics as `unavailable`. | `/dapr` and `/dapr/health-history` keep named state-store evidence visible. History may show the current sample as unavailable, but must not silently overwrite a previous healthy sample with an empty component set. |
| Redis down while remote EventStore metadata is reachable | Health reports state-store `Unhealthy` while remote metadata status remains `Available`. | `/health` is non-green for state-store health; DAPR pages still show remote pub/sub facts as current. | Proves pub/sub visibility is not tied to Redis health. History persistence may be `Unavailable` if it depends on the failed state store. |
| Admin.Server local DAPR sidecar metadata is unavailable | Health returns a partial report with local sidecar metadata marked unavailable, unless the request itself is cancelled. | Health and DAPR pages show an issue state instead of an empty healthy inventory. | Local-admin-sidecar-only fields are labelled unavailable; remote EventStore metadata may still be shown if reachable. |
| Remote EventStore sidecar metadata is not configured or unreachable | The DAPR infrastructure API exposes `RemoteMetadataStatus.NotConfigured` or `RemoteMetadataStatus.Unreachable`; it does not convert this into zero pub/sub components or zero active subscriptions. | `/dapr`, `/dapr/pubsub`, and `/dapr/health-history` show remote metadata unavailable with the last reliable local facts, if any. | Pub/sub inventory and active subscriptions are `unavailable`, not `0`, unless the remote payload was successfully read and genuinely contained none. |
| Remote EventStore sidecar responds with malformed or incomplete metadata | The DAPR infrastructure API exposes `RemoteMetadataStatus.InvalidPayload`; parsing errors do not escape as raw 500 details. | DAPR pages show invalid remote metadata and avoid component/subscription totals derived from partial parsing. | Prior samples may be shown as `Stale`; new history samples mark the remote source invalid/unavailable. |
| Remote EventStore sidecar is reachable but app metadata is still initializing | The DAPR infrastructure API exposes `RemoteMetadataStatus.Initializing` when this condition is detectable; otherwise use `Unavailable` with a non-empty diagnostic category. | DAPR pages show temporary initializing/degraded state, not healthy empty inventory. | History records initializing/unknown evidence without deleting prior rows. |
| Remote EventStore sidecar is reachable and reports `statestore` plus `pubsub` | Shared DAPR inventory includes both components with named source metadata. | `/health`, `/dapr`, `/dapr/pubsub`, and `/dapr/health-history` agree on component names and active subscription counts, or label configured vs active differences. | Health history records the same canonical component set used by `/dapr`. |
| Remote EventStore sidecar is reachable and genuinely reports no pub/sub/subscriptions | Remote metadata status is `Available`; component/subscription counts may be `0` only because the successful payload contained no matching entries. | DAPR pages label this as real zero active/configured pub/sub data, not unavailable. | Tests must distinguish valid empty payload from failed metadata. |
| Metric value is genuinely zero | Metric status is `Available` and the value is `0`. | Home and Health render real zero using page-specific formatting. | Tests must distinguish this from unavailable values. |
| Metric source is unavailable | Metric status is `Unavailable`; numeric fallback values are ignored. | Home and Health render `unavailable`, never `0`, `0.0/s`, or `0.0%`. | No charts or summaries may treat unavailable metrics as zero activity. |
| Health-history persistence fails | Live health and inventory APIs may still return the current in-memory sample if available. History write/read status is `Unavailable` or `Stale`, not success with an empty timeline. | `/dapr/health-history` shows history storage unavailable or stale with timestamp/source. | Prior persisted samples are retained if readable; unreadable history is labelled unavailable. |

Health evidence and inventory evidence are related but not identical. Health answers "is this dependency usable for Admin/EventStore operations now?" Inventory answers "what did a named sidecar report as loaded/configured/active?" A component can be absent from the Admin sidecar but present in the EventStore sidecar; that is an inventory source distinction, not proof that pub/sub is healthy or missing.

Conflict rules:

| Evidence conflict | Required result |
| --- | --- |
| Remote EventStore metadata says `pubsub` exists, but local Admin metadata omits it. | Show remote `pubsub` with source `RemoteEventStoreMetadata`; do not treat the local omission as missing pub/sub. |
| Local state-store probe fails, but remote metadata says the component is loaded. | Show one component row with loaded inventory evidence plus unhealthy local probe evidence. |
| Remote metadata is unreachable, invalid, initializing, or not configured. | Show source unavailable/invalid/initializing/not-configured; do not convert to zero components or zero subscriptions. |
| Remote metadata succeeds and contains zero pub/sub components or subscriptions. | Show real zero with source status `Available`. |

## Acceptance Criteria

1. **`/health` survives Redis/state-store outage.**
   - Given Aspire is running and `/health` loads normally
   - When the Redis-backed DAPR state store is stopped or state-store calls fail/time out
   - Then `GET /api/v1/admin/health` still returns HTTP 200 with a partial `SystemHealthReport` within the configured dependency probe timeout.
   - And the report marks `state.redis` or the configured state store component `Unhealthy`.
   - And the overall status is `Unhealthy`, not a thrown global query failure.
   - And unavailable dependent data, including `TotalEventCount`, is marked with `SystemHealthMetricStatus.Unavailable` instead of fake `0`.
   - And response bodies do not leak raw exception messages, connection strings, Redis host details, or stack traces.
   - And real request cancellation still propagates as cancellation; only dependency failures are downgraded into health evidence.

2. **`Health.razor` renders degraded/partial reports instead of blank screens.**
   - Given the health endpoint returns a partial report, null, 401/403, 503, timeout, or malformed error response
   - When the user opens or refreshes `/health`
   - Then the page shows the first valid state in this priority order: current partial report, stale last-good report clearly labelled with timestamp/source, then `IssueBanner`/`EmptyState`.
   - And the page never spins forever, never crashes to a blank screen, and never hides an outage behind nominal data.
   - And the Refresh button returns to an idle state after success or failure.
   - And stale data is never reused across auth user or tenant-context changes.

3. **Home and Health use one metric truth table.**
   - Given a `SystemHealthReport` contains `TotalEventCountStatus`, `EventsPerSecondStatus`, and `ErrorPercentageStatus`
   - When `/` and `/health` render the same report
   - Then both pages apply the same rule:
     - `Available` plus value `0` renders as real zero (`0`, `0.0/s`, `0.0%`, according to page format).
     - `Unavailable` renders `unavailable`.
     - `Stale` is visually labelled as stale and never presented as fresh.
   - And `/health` must stop rendering raw `EventsPerSecond` and `ErrorPercentage` values when their status is `Unavailable`.
   - And status fields, not numeric defaults, are the source of truth.

4. **DAPR component inventory is canonical across `/health`, `/dapr`, `/dapr/pubsub`, and `/dapr/health-history`.**
   - Given the EventStore sidecar sees both `statestore` and `pubsub`
   - When the Admin UI renders DAPR component inventory
   - Then `/health`, `/dapr`, `/dapr/pubsub`, and `/dapr/health-history` use one shared Admin.Server DAPR inventory contract.
   - And the canonical component inventory includes EventStore-sidecar components from the remote EventStore metadata path plus Admin.Server local dependency probes such as the state store.
   - And `/dapr/pubsub` remains the source of active pub/sub component/subscription detail from the same remote EventStore sidecar metadata payload or an equivalent shared parser over that payload.
   - And `/dapr/health-history` captures the same canonical component set used by `/dapr` so the history heatmap does not silently omit pub/sub.
   - And component counts, subscription counts, and history rows either agree or carry explicit `configured` vs `active` vs `unavailable` labels.
   - And a local-admin-sidecar-only fallback is allowed only as a degraded mode when remote metadata is unavailable; it must be visibly labelled and must not be treated as the canonical inventory.

5. **Pub/sub scoping is handled deliberately, not accidentally.**
   - The implementation must keep `eventstore-admin` out of `pubsub.yaml` component `scopes` unless a written security rationale is added to this story during development.
   - Default implementation path: build Admin inventory from the existing read-only remote EventStore sidecar metadata path configured by `AdminServerOptions.EventStoreDaprHttpEndpoint`.
   - If the implementation instead adds `eventstore-admin` to `pubsub.yaml` component `scopes`, it must first add an ADR-style reviewer-approved note to this story, explicitly deny publish and subscribe topic access for `eventstore-admin` with empty `publishingScopes` and `subscriptionScopes` entries, add tests, and document why remote metadata could not be used.
   - Do not add `eventstore-admin` to component scopes by itself. DAPR component scopes grant the app access to the component; topic scoping must be explicit if this route is chosen.
   - This story does not make Admin.Server a pub/sub participant.

6. **Subscription counts are not contradictory.**
   - Given `/dapr` reports subscription counts and `/dapr/pubsub` reports active subscriptions
   - When both pages read from live metadata
   - Then the counts match for the same metadata source.
   - Or, if one count is static/configured and one is live/active, both labels state that distinction with exact visible labels: `configured subscriptions`, `active subscriptions`, or `subscription data unavailable`.
   - If `Subscriptions = N` and no pub/sub component metadata is available, show an issue banner explaining the mismatch rather than implying a healthy inventory.

7. **Automated tests pin the outage, metric, and inventory contracts.**
   - Server tests cover state-store failure, metadata failure, stream-index failure, remote sidecar available/unreachable/not-configured, and cancellation propagation.
   - UI tests cover `/health` unavailable metric rendering, partial report rendering, API failure/stale rendering, DAPR pub/sub category visibility, and health-history component rows.
   - Tests must prove that `Unavailable` metrics do not render as `0` on `/health`.
   - Tests must cover the Operator Truth Contract scenarios above, including valid zero vs unavailable, local metadata failure vs remote metadata failure, and health-history state-store failure.

8. **Manual Aspire evidence is captured before review.**
   - With Aspire running in the project-standard dev mode, the developer or operator records:
     - baseline `/`, `/health`, `/dapr`, `/dapr/pubsub`, `/dapr/health-history` screenshots or endpoint payloads after canonical seed;
     - Redis stopped, `/health` refreshed, and `state.redis` visible as unhealthy while the page stays usable;
     - Redis restarted and the pages returning to nominal or stale-labelled state;
     - component/subscription count comparison across the four DAPR pages.
   - Evidence should be appended to this story or saved under `_bmad-output/test-artifacts/`.

## Tasks / Subtasks

- [x] **ST1 - Server health partial-report hardening.** (AC: 1)
  - [x] Audit `DaprHealthQueryService.GetSystemHealthAsync` for every DAPR/state-store/stream-query call that can throw or hang when Redis is down.
  - [x] Add bounded timeouts to metadata and state-store probes where missing. Preserve caller cancellation.
  - [x] When the state-store probe fails, mutate or synthesize the matching `DaprComponentHealth` entry as `Unhealthy` instead of leaving metadata-derived `Healthy`.
  - [x] Keep `TotalEventCount` backed by the bounded `IStreamQueryService.GetRecentlyActiveStreamsAsync` source, but return `TotalEventCountStatus.Unavailable` when that source fails.
  - [x] Ensure `AdminHealthController.GetSystemHealth` receives a report for dependency failures instead of converting them into 503, except for genuine Admin.Server unavailability.

- [x] **ST2 - Health page no-blank/error-state rendering.** (AC: 2)
  - [x] Update `Health.razor` to treat partial reports as renderable data.
  - [x] Add explicit stale/issue rendering for null or failed refreshes using the existing Home/IssueBanner pattern.
  - [x] Ensure the initial loading state always exits after success, partial success, or failure.
  - [x] Verify inaccessible/forbidden errors remain honest and do not reuse stale data across auth contexts.

- [x] **ST3 - Shared metric rendering semantics on `/` and `/health`.** (AC: 3)
  - [x] Extract or duplicate a small local helper that formats metric value plus `SystemHealthMetricStatus` consistently.
  - [x] Update `Health.razor` stat cards for Total Events, Events/sec, and Error Rate to consume the status fields.
  - [x] Preserve `Index.razor` behavior from `admin-ui-manual-test-bug-bundle`: it already renders `unavailable` for unavailable Events/sec and Error Rate and gates ActivityChart on stream data, not hardcoded total events.
  - [x] Add or update bUnit tests so `/health` cannot regress to raw `0.0` / `0.0%` for unavailable metrics.

- [x] **ST4 - DAPR inventory source unification.** (AC: 4, 5, 6)
  - [x] Introduce or refactor to a shared Admin.Server DAPR inventory method, suggested name `GetCanonicalDaprInventoryAsync`, used by `/health`, `/dapr`, `/dapr/pubsub`, and the health-history collector.
  - [x] Make the canonical inventory source remote EventStore sidecar metadata merged with local Admin.Server dependency probes; local-only inventory is a degraded fallback, not the normal contract.
  - [x] Merge components deterministically by `{ name, type }`; preserve source attribution per component rather than merging by display label/category text.
  - [x] Reuse `AdminServerOptions.EventStoreDaprHttpEndpoint` and the existing `/v1.0/metadata` parsing pattern already used by `GetPubSubOverviewAsync`.
  - [x] Keep local admin sidecar state-store probe behavior so `state.redis` health still reflects Admin.Server's dependency.
  - [x] Include remote EventStore pub/sub components without requiring Admin.Server to publish or subscribe.
  - [x] If remote metadata is unavailable, invalid, not configured, or initializing, expose `RemoteMetadataStatus` and visible explanatory text; do not silently drop pub/sub.

- [x] **ST5 - Align `/dapr`, `/dapr/pubsub`, and health-history counts.** (AC: 4, 6)
  - [x] Update `DaprComponents.razor` to label component and subscription counts by source when they differ.
  - [x] Ensure `DaprHealthHistoryCollector` captures the unified component set and marks unavailable sources explicitly.
  - [x] If state-store persistence for history fails, keep the current in-memory sample visible in API/UI responses where possible, include prior persisted samples when readable, and show history persistence as unavailable; do not replace prior history with an empty successful timeline.
  - [x] Add regression tests proving a fixture with remote `pubsub` metadata yields a Pub/Sub row on `/dapr`, `/health`, and `/dapr/health-history`.
  - [x] Add regression tests proving `/dapr` and `/dapr/pubsub` show matching active subscription counts when both use the same remote metadata payload.

- [x] **ST6 - Security and DAPR scoping guardrails.** (AC: 5)
  - [x] Do not edit `pubsub.yaml` unless ST4 explicitly chooses the component-scope path.
  - [x] If `pubsub.yaml` is edited, update comments and tests to show `eventstore-admin` has component metadata visibility but empty publish and subscribe grants. (NOT INVOKED — default remote-metadata path used; pubsub.yaml unchanged.)
  - [x] Verify existing pub/sub topic isolation tests still pass or are updated intentionally.

- [x] **ST7 - Automated validation.** (AC: 7)
  - [x] Run `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests`.
  - [x] Run `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests`.
  - [x] Run impacted unit suites individually if shared abstractions change: `tests/Hexalith.EventStore.Admin.Abstractions.Tests`, `tests/Hexalith.EventStore.Contracts.Tests`, and `tests/Hexalith.EventStore.Client.Tests`.
  - [x] Do not rely on solution-level `dotnet test`; this repo has documented pre-existing Server.Tests/analyzer gotchas and test projects should be run individually.

- [ ] **ST8 - Manual Aspire smoke.** (AC: 8) — operator-owned per story scope
  - [ ] Start from a clean Redis state, build, and run Aspire using the project instructions.
  - [ ] Seed `tenant-a/counter/counter-1` with the canonical Pattern 2 sequence.
  - [ ] Capture baseline pages and endpoint payloads.
  - [ ] Stop Redis, refresh `/health`, and prove the page stays usable with `state.redis` unhealthy.
  - [ ] Restart Redis and prove the pages recover or mark stale data honestly.

> **AC8 deferral note (recorded 2026-05-08 from bmad-code-review D3 decision).** The Quality
> Gate language requires AC8 manual evidence (commands, timestamps, endpoint JSON before/during/
> after Redis outage). This story is moving forward at `review` per the project's established
> operator-deferred-smoke convention (see sprint-status comments on `OA1` and prior stories
> `admin-ui-aggregate-state-replay-correctness` / `admin-ui-state-inspection-cluster-fix`).
> **Exit criterion:** the operator pastes ST8 evidence (or notes "no longer reproducible") into
> the Completion Notes; only then does the story move `review -> done`. The automated Tier 1+2
> evidence already covers AC1-AC7 via `Hexalith.EventStore.Admin.Server.Tests` (576 passed) and
> `Hexalith.EventStore.Admin.UI.Tests` (709 passed) post-review-patches.

### Review Findings (2026-05-08 — bmad-code-review of PR #241 / commit 388ceb78)

Three reviewer layers ran in parallel: Blind Hunter (diff-only), Edge Case Hunter (diff + project), Acceptance Auditor (diff + spec). 24 unique findings after merge: 3 decision-needed, 16 patch, 3 defer, 2 dismissed.

**Decision-needed (resolve before patches)**

- [x] [Review][Decision][resolved] Overall-status policy for remote metadata gaps — `ComputeOverallStatus` (`DaprHealthQueryService.cs:225-228`) flips overall to `Degraded` for `RemoteMetadataStatus.Unreachable / InvalidPayload / Initializing`. The Operator Truth Contract distinguishes "dependency usable" (health) from "metadata source available" (inventory). A remote-sidecar metadata gap does not mean Admin/EventStore operations are degraded. Decide: keep blanket Degraded, only Degrade for Unreachable, or keep Healthy and surface the metadata gap exclusively via `InventorySourceStatus`.
- [x] [Review][Decision][resolved] Subscription-count snapshot stability — Each public method (`GetCanonicalDaprInventoryAsync`, `GetPubSubOverviewAsync`, `GetSidecarInfoAsync`, `GetActorRuntimeInfoAsync`) calls `TryReadRemoteMetadataAsync` independently, so concurrent UI navigation between `/dapr` and `/dapr/pubsub` can hit different `/v1.0/metadata` snapshots. Story line 461 claims "counts agree by construction" but no caching enforces this. Decide: add request-scoped memoization, accept best-effort agreement, or document the snapshot caveat.
- [x] [Review][Decision][resolved] AC8 manual-smoke evidence vs `review` status — Story Status is `review` but ST8 / AC8 boxes are unchecked and the Quality Gate requires manual Aspire evidence (commands, timestamps, endpoint JSON before/during/after Redis outage). Decide: keep `review` with operator-deferred AC8, or move back to `in-progress` until ST8 evidence is recorded.

**Patches (apply before status advances)**

- [x] [Review][Patch][applied][CRITICAL] State-store probe `Unhealthy` result silently overwritten by remote merge — `DaprInfrastructureQueryService.cs:134-138` runs probes first, then unconditionally rewrites `merged[(name,type)] = c with { Source = RemoteEventStoreMetadata }` using a remote `c` whose Status is hardcoded `Healthy` in `ParseRemoteMetadata`. `/dapr` and `DaprHealthHistoryCollector` therefore display fake-healthy state-store rows when Redis is down but the EventStore sidecar is reachable. Violates AC1, AC4, and the Operator Truth Contract.
- [x] [Review][Patch][applied][HIGH] `DaprActors.razor` switch missing `InvalidPayload` / `Initializing` cases — `src/Hexalith.EventStore.Admin.UI/Pages/DaprActors.razor:69-83` only handles `NotConfigured`/`Unreachable`/`Available`. When `RemoteMetadataStatus` is `InvalidPayload` or `Initializing`, the page renders no empty state. `DaprPubSub.razor` was patched to handle these statuses; `DaprActors.razor` was not.
- [x] [Review][Patch][applied][HIGH] State-store probe runs twice per `/health` request with divergent timeout semantics — `GetCanonicalDaprInventoryAsync` invokes `ProbeStateStoreEntryAsync` (writes `admin:dapr-probe`) and the timeout maps to `Degraded`; `DaprHealthQueryService.ProbeStateStoreAsync` then invokes a second probe (writes `admin:health-check`) and maps the same condition to `Unhealthy`. Two probes against the same Redis with disagreeing semantics — consolidate into one bounded probe with one classification rule.
- [x] [Review][Patch][applied][HIGH] `DaprHealthHistoryCollector` writes empty timeline when remote returns `Available` with zero components — Skip-guard `DaprHealthHistoryCollector.cs:97-103`: `if (components.Count == 0 && status is not Available) return;` allows the empty-Available case to fall through and persist `new DaprComponentHealthTimeline([], HasData: true)`, overwriting last-good. AC4 explicitly forbids this.
- [x] [Review][Patch][applied][HIGH] `Initializing` heuristic broken in both directions — `DaprInfrastructureQueryService.cs` parsing requires `id` property to exist AND be empty. (a) Fresh sidecar that omits `id` → returns `Available` with empty components instead of `Initializing`; (b) Healthy minimal sidecar with empty `id` and zero actors/subs → false-positive `Initializing` → blanket Degraded overall (depends on D1). Replace with a documented heuristic that doesn't depend on `id` shape.
- [x] [Review][Patch][applied][MEDIUM] `Health.razor` `MetricSeverity` collapses Available and Unavailable to `"neutral"` and never appends a stale label — `Health.razor:1949-1969`: severity switch returns `"neutral"` for both `Available` and the default branch (which includes `Unavailable`); the stat-card visual treatment for an unavailable metric is identical to a healthy one. Stale comment promises "render the stale value with a stale label" but no label is appended. Spec AC3 requires unavailable metrics to be visually distinguishable.
- [x] [Review][Patch][applied][MEDIUM] Synthesized state-store row in `MapToHealthComponents` hardcodes `ComponentType = "state"` — `DaprHealthQueryService.cs:200-205` falls back to literal `"state"` instead of the canonical `state.<provider>` (e.g., `state.redis`). Other state-store rows in the system use the fully-qualified type; this row is inconsistent with `DaprComponentCategoryHelper.FromComponentType` mapping and any UI grouping/filtering by type.
- [x] [Review][Patch][applied][MEDIUM] State-store probe match in `/health` requires both name AND `Category == StateStore` — `DaprHealthQueryService.cs:187-188`: when canonical inventory carries a state-store with a `type` not mapped to `Category.StateStore` (custom builder, future provider), the probe fallback synthesizes a duplicate row with the same name but different category, surfacing two rows for the same component on `/health`. Match on name (and configured-state-store equality) instead of relying on category resolution.
- [x] [Review][Patch][applied][MEDIUM] `HttpClient(new FakeHandler(...))` not disposed in canonical-inventory tests — `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprInfrastructureQueryServiceTests.cs` constructs `new HttpClient(new FakeHandler(...))` in every new test without `using` or per-class disposal. Wrap each in `using` or migrate to a `IAsyncLifetime` cleanup.
- [x] [Review][Patch][applied][MEDIUM] Connection-detail-leak test asserts on fields that cannot leak — `DaprHealthQueryServiceTests.cs:_DoesNotLeakConnectionDetails_OnStateStoreFailure` only checks `OverallStatus.ToString()` (an enum), `ComponentName` and `ComponentType` (fixed-shape strings). No assertion on logger output, exception message, `ObservabilityLinks`, or any field that could realistically carry the secret. Defensive theater — extend to inspect every reachable string field and the captured logger state.
- [x] [Review][Patch][applied][MEDIUM] Tautological remote-vs-local merge test — `DaprInfrastructureQueryServiceTests.cs:_MergesRemoteAndLocal_WithSourceAttribution`: the only shared key in the fixture is `statestore`, where `ProbeStateStoreEntryAsync` ultimately rewrites `Source = LocalAdminProbe`. The test asserts `Source.ShouldBe(RemoteEventStoreMetadata)` — passes only because of the merge order and probe behavior, not because remote-wins-on-shared-key was actually proven. Add a test where the same `(name,type)` exists in both sources with non-state-store category.
- [x] [Review][Patch][applied][MEDIUM] `LocalProbeAvailable` docstring vs implementation mismatch — `DaprInfrastructureQueryService.cs`: `localProbeAvailable = localMetadata is not null` ("did `/v1.0/metadata` respond?") but the field is documented as "Admin.Server dependency probe (state-store reachability) returned usable evidence." `ComputeOverallStatus` uses `!localProbeAvailable` to flag "Admin operations cannot be served." Either rename the field (e.g., `LocalSidecarMetadataAvailable`) or actually make it reflect probe success, then update the docstring.
- [x] [Review][Patch][applied][MEDIUM] Test fixtures construct `DaprComponentDetail` without `Source` — defaults to `Unavailable` while claiming healthy state — `DaprHealthHistoryCollectorTests.cs` and similar fixtures use the positional ctor that defaults `Source = DaprComponentSource.Unavailable`, then assert behavior against components carrying contradictory source attribution. Production code must never produce `Source = Unavailable` for a successfully reported component (truth contract). Update fixtures to pass an explicit Source.
- [x] [Review][Patch][applied][LOW] `DaprComponents.razor` empty-state copy does not differentiate `InvalidPayload` / `Initializing` — `src/Hexalith.EventStore.Admin.UI/Pages/DaprComponents.razor:105-110` shows the generic "DAPR sidecar is not available or has no registered components" for any zero-components case. The dedicated copy added to `DaprPubSub.razor` (lines 1882-1889 in the diff) was not mirrored here despite both pages sharing the data source.
- [x] [Review][Patch][applied][MEDIUM] AC7 test coverage gaps — Per the spec Test Plan, missing fixtures: (a) `RemoteMetadataStatus.Initializing` emission test (status was added; production code returns it; no assertion exists); (b) `RemoteMetadata_ZeroSubscriptions` valid-empty-payload distinguishing successful zero from failed metadata; (c) `Stale` metric rendering on `/health`; (d) ST5 cross-page regression for remote `pubsub` metadata producing a Pub/Sub row on `/dapr/health-history` (`DaprHealthHistoryPageTests.cs` is unchanged); (e) named UI failure fixtures `HealthApi_Forbidden_NoStaleReuse`, `HealthApi_Timeout_StaleLastGood`, `HealthApi_Malformed_IssueBanner`, `HealthApi_Null_IssueBanner`.
- [x] [Review][Patch][applied][LOW] `FormatSource` default case treats `Unavailable` and unknown future enum values identically — `DaprComponents.razor:1829-1835`: `_ => "unavailable"` default-case renders any future `DaprComponentSource` value as "unavailable" with no logger or telemetry. Add a logger.LogWarning on default to make missing UI translations observable.

**Deferred (real but out of current story scope)**

- [x] [Review][Defer] `DaprCanonicalInventory` record null-check pattern bypassed by `with` expressions and JSON deserialization [src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprCanonicalInventory.cs] — deferred, design-pattern concern with no current bug. The `?? throw new ArgumentNullException(...)` guards in record-property re-declarations cover positional construction only; `inventory with { Components = null! }` and `System.Text.Json` deserialization both bypass them. Track in deferred-work for a system-wide pattern review.
- [x] [Review][Defer] `StatCard` "subscription data unavailable" (27 chars) overflows the value slot on small viewports [src/Hexalith.EventStore.Admin.UI/Pages/DaprComponents.razor + DaprPubSub.razor] — deferred, UX/CSS concern with no manual-test evidence in this iteration. Track for next responsive-layout pass.
- [x] [Review][Defer] `Health.razor` `_cachedHealthReport` not cleared on auth-user change [src/Hexalith.EventStore.Admin.UI/Pages/Health.razor] — deferred, out of scope per story Dev Note ("Health is system-wide so cross-tenant stale-data reuse is not in scope"). No test pins the rule, so a future regression would not surface visibly.

**Dismissed (noise / false positive)**

- AC2 self-report "page unchanged" is not a finding — Health.razor was already structured for partial reports per ST2; both Blind Hunter and Acceptance Auditor confirm no code change is required for AC2.
- AC6 "Active Subscriptions" Title Case vs spec backtick lowercase is not a contract violation — Razor StatCard label convention; the truthful copy `subscription data unavailable` matches the spec literal.

### Review Findings (round 2 — 2026-05-07 — bmad-code-review of uncommitted post-patch working tree vs HEAD)

Three reviewer layers ran in parallel against the uncommitted working tree (the 19 patches recorded in `sprint-status.yaml` line 31 plus follow-on fixture/test updates): Blind Hunter (diff-only), Edge Case Hunter (diff + project), Acceptance Auditor (diff + spec). 56 raw findings → 38 unique after merge: 5 decision-needed, 25 patch, 5 defer, 3 dismissed. No CRITICAL severity remains except the memoization race; HIGH count concentrated in the new Stage 3/4 synth + probe ordering and in the canonical-vs-local count divergence on `/dapr`.

**Decision-needed (resolve before patches)**

- [ ] [Review][Decision] Synth state-store merge key — `state.unknown` collides with real `state.<provider>` — `DaprInfrastructureQueryService.cs:122-135` inserts the synth using key `(configuredStateStore, "state.unknown")`. When local or remote metadata later reports the same name with type `state.redis`, both rows survive (different keys); inventory has two state-store rows for the same logical component, and `IsStateStoreProbeFailed.FirstOrDefault` returns whichever row inserted first. Decide: (a) match state-store identity by name only (separate from generic `(name,type)` merge), (b) skip synth insertion when any row with the configured name is already present in `merged`, or (c) use a sentinel synth type that the merge stage explicitly replaces by name. Sources: Blind#5, Edge#4.
- [ ] [Review][Decision] Add `Source` to `DaprHealthHistoryEntry` — history persistence loses source attribution — `DaprHealthHistoryCollector.cs:121-126` writes `new DaprHealthHistoryEntry(ComponentName, ComponentType, Status, CapturedAtUtc)` with no `Source`. After this round operators cannot distinguish synth-Unhealthy from probe-confirmed-Unhealthy in the `/dapr/health-history` timeline. Decide: (a) add `Source` field with a migration plan for already-persisted entries, (b) accept the loss as a snapshot-of-Status-only timeline, or (c) append a per-entry diagnostic blob carrying source. Source: Edge#19.
- [ ] [Review][Decision] Composite source for "remote loaded + probe failed" — conflict-rule line 76 of the spec demands "loaded inventory + unhealthy probe **evidence**" both visible — `DaprInfrastructureQueryService.cs:636-650` rewrites `Source = LocalAdminProbe` on probe completion, erasing prior `RemoteEventStoreMetadata` attribution. Test `_PreservesProbeUnhealthy_WhenRemoteReportsStateStoreLoaded` even pins this single-source result. Decide: (a) extend `DaprComponentDetail` with a secondary `RemoteAlsoReports` flag/source list, (b) keep single-source and amend the spec conflict-rule, or (c) carry remote-loaded fact via `Capabilities`/metadata while keeping `Source = LocalAdminProbe`. Source: Auditor#4.
- [ ] [Review][Decision] `HealthStatus.Degraded` is emitted by code but absent from the spec's allowed status vocabulary — `DaprHealthQueryService.cs:194,198` returns `HealthStatus.Degraded` for `Unreachable` and for `eventStoreFailed`; the UI renders the literal string `"Degraded"`. The spec vocabulary table lists only `Healthy`/`Unhealthy`/`Available`/`Unavailable`/`Stale`/`NotConfigured`/`Unreachable`/`InvalidPayload`/`Initializing`. Decide: (a) add a `Degraded` row to the spec vocabulary, (b) remap wire status to `Unhealthy` and reserve `Degraded` for UI-only display, or (c) split into `PartiallyDegraded` vs `Unhealthy`. Source: Auditor#8.
- [ ] [Review][Decision] Distinguish 4xx (auth/config error) from transport failure in `RemoteMetadataStatus` — `ReadRemoteMetadataAsync` `EnsureSuccessStatusCode` collapses 401/403/4xx and TCP refusals into `Unreachable`. An operator who misconfigures a token sees the same UI as a sidecar-down failure. Decide: (a) add `RemoteMetadataStatus.Forbidden` (and possibly `BadRequest`) with corresponding UI copy, (b) keep `Unreachable` and surface the HTTP status only in a diagnostic field, or (c) treat 4xx as `InvalidPayload` to keep the enum stable. Source: Edge#25.

**Patches (apply before status advances)**

- [ ] [Review][Patch][CRITICAL] Per-request remote-metadata memoization caches faulted/cancelled task and forbids retry within the scope [`src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs:396-399`] — `_remoteMetadataTask ??= ReadRemoteMetadataAsync(ct)` captures the first caller's `ct`. If that caller cancels (or the network blips on the first read), every later consumer in the same scope (`GetSidecarInfoAsync`, `GetPubSubOverviewAsync`, `GetActorRuntimeInfoAsync`, the second `GetCanonicalDaprInventoryAsync` invocation) awaits the cached faulted task and rethrows the same cancellation/error even though their own token is fine. The "scoped lifetime guarantees single-threaded access" claim also breaks when concurrent overlap occurs (Blazor SSR pre-render + interactive boot, parallel controller actions sharing the scope, internal `Task.WhenAll` probes). Cache only successful payloads; clear `_remoteMetadataTask` on failure. Sources: Blind#1, Blind#2, Edge#1, Edge#2.
- [ ] [Review][Patch][HIGH] Synthetic state-store row carries `Source = Unavailable` while `Status = Unhealthy`, surviving probe-cancel paths [`src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs:124-135`] — Stage 3 constructs the synth with `Source: DaprComponentSource.Unavailable`. The probe rewrites Source only on completion; if the linked probeCts cancels by its 3-second timer (without outer ct cancellation), the synth survives. `MapToHealthComponents` then projects `Source = Unavailable` on a row carrying `Status = Unhealthy` — the exact "successfully reported component with Source=Unavailable" pattern the spec/tests forbid. Set `Source = LocalAdminProbe` at construction time (probe-pending IS local-probe evidence); the row exists only because of the probe. Sources: Blind#6, Edge#3, Edge#9.
- [ ] [Review][Patch][HIGH] `IsStateStoreProbeFailed` predicate is unsafe across multiple paths [`src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs:147-160`] — three independent issues: (a) returns `true` (overall Unhealthy) when no row matches `(name, Category=StateStore)` even though Stage 3 synth would always create a StateStore row, leaving overall Unhealthy with no Unhealthy component to point to if a remote sidecar reports the configured name with a different category; (b) `FirstOrDefault` on the unordered components yields non-deterministic results when two state-store rows with the same name coexist (synth + real, see merge-key decision); (c) condition is `row.Status != HealthStatus.Healthy` so a `Degraded` row from any future or test path is misclassified as probe failure. Tighten to: scope the lookup to `Source == LocalAdminProbe` (or "configured state-store name" predicate matching Stage 3), use `All`/`Any` instead of `FirstOrDefault`, and check exclusively for `Status == Unhealthy`. Sources: Blind#4, Edge#5, Edge#7.
- [ ] [Review][Patch][HIGH] Stage-4 probe iterates every state-store entry and falsely marks remote-only ones Unhealthy [`src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs:144-151,627-633`] — the loop calls `_daprClient.GetStateAsync<string>(component.ComponentName, "admin:dapr-probe", ...)` against the **Admin** sidecar for every component categorised as state-store. If the EventStore sidecar reports a state store the Admin sidecar does not host (`eventstore-state` etc.), the Admin probe attempts a key read and DAPR returns "component not found" → row rewritten as Unhealthy with `Source = LocalAdminProbe`. The Admin probe just falsely flagged a remote-only component. Filter the probe loop to entries where `ComponentName == _options.StateStoreName`. Source: Edge#6.
- [ ] [Review][Patch][HIGH] `GetSidecarInfoAsync` and `GetActorRuntimeInfoAsync` make additional `_daprClient.GetMetadataAsync` calls, defeating the snapshot-stability claim [`src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs:187,227`] — only the **remote** metadata task was memoized; local sidecar metadata is still hit three times per request. The D2 decision in round 1 promised "snapshot stability"; the local sidecar can hot-reload between calls, so `/dapr` and `/dapr/pubsub` (and the actor runtime card) can still disagree on local component count. Memoize local metadata under the same `Lazy<>`/`Interlocked.CompareExchange` pattern. Source: Edge#8.
- [ ] [Review][Patch][HIGH] AC1 connection-detail-leak test uses `NullLogger` — secret leakage to log records is unaudited [`tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprHealthQueryServiceTests.cs:212-277`] — `_DoesNotLeakConnectionDetails` walks every reachable string field on `SystemHealthReport` (good), but injects `NullLogger<DaprInfrastructureQueryService>.Instance`, which discards all log output. `_logger.LogWarning(ex, "State store probe failed for {ComponentName}.", component.ComponentName)` at `DaprInfrastructureQueryService.cs:649` echoes the secret-bearing exception message into structured logs, and the test cannot see it. The round-1 patch checklist marked "captured logger state" as `[applied]` — it was not. Replace `NullLogger` with `FakeLogger<T>` (`Microsoft.Extensions.Logging.Testing`) and assert no record's `Message` or `Exception?.Message` contains the secret. Sources: Blind#7, Edge#10, Auditor#3.
- [ ] [Review][Patch][HIGH] `/dapr` Components StatCard reads local-sidecar count while the grid below it renders the canonical merged inventory [`src/Hexalith.EventStore.Admin.UI/Pages/DaprComponents.razor:91-92`; `DaprInfrastructureQueryService.cs:205`] — the stat card binds to `_sidecar.ComponentCount`, populated from local `metadata.Components?.Count`. The grid binds to `_components` derived from `GetCanonicalDaprInventoryAsync` (canonical merge, includes remote pub/sub + state-store synth). When remote sees `pubsub` and local does not — exactly the canonical example for AC4 — the stat card under-counts the grid. Source the stat card from `_components.Count` (or label it explicitly "configured (local sidecar)" with a separate "active (canonical)" card). The hardcoded `Severity="success"` on this card also needs to be driven by canonical inventory health (any Unhealthy row → warning/error). Sources: Auditor#1, Auditor#11.
- [ ] [Review][Patch][HIGH] `RemoteMetadataStatus.Initializing` is now unreachable production code [`src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs:413-419`; `RemoteMetadataStatus.cs:20`; UI in `DaprActors.razor`, `DaprComponents.razor`, `DaprPubSub.razor`] — round-1 patch removed the `id` heuristic (correctly: it had a false-positive direction), but the replacement always returns `Available` on parse success. Three pages now have UI cases for an enum value production code cannot produce; the spec scenario "Remote EventStore sidecar is reachable but app metadata is still initializing" has no implementation. Default action: remove the enum value and its UI/test branches; if a producer is genuinely planned, file a story for `/v1.0/healthz/outbound` polling and keep the enum with a doc comment that no producer exists yet. Sources: Blind#12, Edge#15, Edge#20, Auditor#2.
- [ ] [Review][Patch][HIGH] Synth row's hardcoded `Category = StateStore` and helper-derived `FromComponentType("state.unknown")` are not pinned [`src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs:124-128`] — the synth literal `"state.unknown"` happens to map to `DaprComponentCategory.StateStore` via the helper's prefix logic today, but the diff hardcodes the Category field instead of deriving it. Any future change to either the literal (e.g., `"unknown"` with no prefix) or the helper's mapping logic will silently desynchronise: `synth.Category == StateStore` while `Helper.FromComponentType(synth.ComponentType) == Unknown`. Two consumers re-deriving category from type get a different answer than the field. Derive synth Category from the helper at construction (or add a unit test that pins the equivalence). Source: Edge#11.
- [ ] [Review][Patch][MEDIUM] Health-history collector skip rule erases valid `Available + zero` samples and floods timeline with synth Unhealthy entries during outage [`src/Hexalith.EventStore.Admin.Server/Services/DaprHealthHistoryCollector.cs:101-106`] — current guard skips persistence only when `components.Count == 0`. Stage 3 synth makes `components.Count >= 1` even during a total metadata outage, so 24h of outage produces hundreds of synth Unhealthy rows that collapse on subsequent reads into "history of one perpetually unhealthy state-store" with no diagnostic context (made worse by the missing `Source` on history entries — see decision-needed item 2). Conversely, if both metadata sources succeed and report zero, the synth still adds 1 row but a future code path that omits the synth would skip a legitimate `Available + zero` sample. Replace the guard with: skip when `RemoteMetadataStatus != Available && every component.Source == Unavailable`, otherwise persist (including a real-zero `Available` sample marked `HasData: true`). Sources: Auditor#7, Edge#18.
- [ ] [Review][Patch][MEDIUM] AC7 named UI failure fixtures are still missing [`tests/Hexalith.EventStore.Admin.UI.Tests/Pages/HealthPageTests.cs`] — Test Plan line 361 names five fixtures: `HealthReport_Partial_StateStoreUnavailable`, `HealthApi_Forbidden_NoStaleReuse`, `HealthApi_Timeout_StaleLastGood`, `HealthApi_Malformed_IssueBanner`, `HealthApi_Null_IssueBanner`. Round 2 added `HealthPage_RendersStaleSuffix_ForStaleMetric_AC3` (the `Stale` rendering one) but the four error-response fixtures are still absent — round-1 marked them `[applied]` in the patch log; they were not. Add four bUnit tests: `IAdminApiClient` substitute returns 401/403, throws timeout, returns malformed payload, returns null. Source: Auditor#5.
- [ ] [Review][Patch][MEDIUM] ST5 cross-page regression for `/dapr/health-history` rendering a Pub/Sub row is absent [`tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DaprHealthHistoryPageTests.cs`] — collector test `ExecuteAsync_PersistsRemotePubSubRow_WhenInventoryIncludesPubSub` covers the persistence write but the page-level regression is not added. ST5 explicitly requires "regression tests proving a fixture with remote `pubsub` metadata yields a Pub/Sub row on `/dapr`, `/health`, and `/dapr/health-history`" — only the first two have evidence. Add `DaprHealthHistoryPage_ShowsPubSubRow_WhenRemoteMetadataAvailable` with an admin API mock returning a timeline that includes a `pubsub` row and assert the rendered grid contains it. Source: Auditor#6.
- [ ] [Review][Patch][MEDIUM] `DaprHealthQueryService.GetDaprComponentStatusAsync` still bypasses the canonical inventory contract [`src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs:237-252`] — backs `/api/v1/admin/health/dapr` (consumed by Admin MCP and external operators). Reads local Admin sidecar metadata only and stamps every component as `Healthy` regardless of probe outcome. The story's UI consumes the canonical path, but the API contract surface still returns the pre-refactor untruthful answer. The embedded ADR consequences (line 297) explicitly forbid this. Reroute through `_infrastructure.GetCanonicalDaprInventoryAsync(ct)` and project to `DaprComponentHealth`. Source: Auditor#10.
- [ ] [Review][Patch][MEDIUM] Default-arm and null-guard inconsistency across `DaprActors.razor` / `DaprComponents.razor` / `DaprPubSub.razor` [respective `Pages/*.razor`] — three distinct truth-contract states (`null` (API call failed), `Unreachable`, future enum value) collapse to the same generic copy in `DaprComponents.razor:108-129`; `DaprPubSub.razor` has no `default:` arm at all (future enum → empty container); `DaprActors.razor` has a `default:` arm but emits no telemetry, so future statuses are silently squashed. Add an explicit `if (_sidecar is null) {...}` arm before each switch, mirror a `default:` with `Logger.LogWarning("Unrecognised RemoteMetadataStatus {Value}", status)`, and emit consistent copy. Sources: Edge#12, Edge#13, Edge#14.
- [ ] [Review][Patch][MEDIUM] Tautological merge test partially fixed; state-store + probe-success + version-conflict rule still unpinned [`tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprInfrastructureQueryServiceTests.cs:976-1004`] — the new "remote wins on shared key" test uses pubsub (which is **not probed**), so the merge-with-probe path is still untested. The actual rule operators rely on — "when probe succeeds, the resulting row's `Version` field comes from remote (or local fallback) per the documented merge order" — has no test. Add a state-store test where local fallback says `state.redis v1`, remote says `state.redis v2`, probe succeeds, and assert the resulting `Version` matches the documented winner. Source: Edge#16.
- [ ] [Review][Patch][MEDIUM] StatCard severity for unavailable subscriptions is `"neutral"` — visually identical to healthy zero [`src/Hexalith.EventStore.Admin.UI/Pages/DaprComponents.razor:97-99`; `DaprPubSub.razor:60-67`] — `Health.razor`'s `MetricSeverity` was patched in round 1 to return `"warning"` for `Unavailable`; the same patch was not mirrored to the StatCards on `/dapr` and `/dapr/pubsub`. Spec AC3 requires unavailable metrics to be visually distinguishable. Drive the severity from `_overview.RemoteMetadataStatus`: non-Available → `"warning"`. Source: Edge#21.
- [ ] [Review][Patch][MEDIUM] `_DoesNotLeakConnectionDetails` does not exercise the remote-HTTP path [`tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprHealthQueryServiceTests.cs`] — the test constructs a real `DaprInfrastructureQueryService` to "exercise the full real path", but `Options.Create(new AdminServerOptions())` leaves `EventStoreDaprHttpEndpoint` null/empty, so `ReadRemoteMetadataAsync` short-circuits to `NotConfigured` and the remote-HTTP code path is dark. Configure the endpoint AND wire a `FakeHandler` that returns a payload containing the secret to cover the JSON-leak surface. Source: Edge#23.
- [ ] [Review][Patch][MEDIUM] HealthPage stale-suffix test depends on runtime culture [`tests/Hexalith.EventStore.Admin.UI.Tests/Pages/HealthPageTests.cs:1145-1147`] — assertion uses `12345L.ToString("N0")` round-trip; under fr-FR (Jerome's likely environment) the group separator is NBSP (U+00A0). bUnit may not share the test thread's culture, so production rendering and assertion can use different separators. Pin `CultureInfo.InvariantCulture` in both production format calls and the test, or use a regex match. Source: Edge#24.
- [ ] [Review][Patch][LOW] `MetricSeverity` default returns `"warning"` silently for unknown enum values [`src/Hexalith.EventStore.Admin.UI/Pages/Health.razor:1965-1969`] — future `SystemHealthMetricStatus` members get bucketed as warning with no telemetry. `FormatSource` got the explicit-default-with-debug treatment in round 1; this didn't. Add `Logger.LogWarning("Unrecognised SystemHealthMetricStatus {Value}", status)` in the default arm. Source: Blind#8.
- [ ] [Review][Patch][LOW] `HttpClient` not disposed in the new leak test [`tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprHealthQueryServiceTests.cs:223-225`] — `infraHttpFactory.CreateClient(Arg.Any<string>()).Returns(new HttpClient())`. Round 1 added `using` to canonical-inventory tests but missed this newly-introduced occurrence. Wrap in `using HttpClient ...`. Source: Blind#13.
- [ ] [Review][Patch][LOW] `LocalProbeAvailable` field renamed in docstring only [`src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprCanonicalInventory.cs:99-106`; `DaprInfrastructureQueryService.cs:71`] — round-1 patch updated the XML doc to "metadata API responded successfully" but the field name still says `LocalProbeAvailable`, and `ComputeOverallStatus` still uses `!localProbeAvailable` as a proxy for "Admin operations cannot be served". Rename to `LocalSidecarMetadataAvailable` (matches the docstring meaning) and update consumers; or actually wire it to probe success and update the docstring back. Source: Blind#10.
- [ ] [Review][Patch][LOW] Collector test seeds `null!` cast that masks short-circuit-on-null behavior [`tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprHealthHistoryCollectorTests.cs:165-167`] — `daprClient.GetStateAsync<DaprComponentHealthTimeline>(...).Returns((DaprComponentHealthTimeline)null!)`. If the collector defensively skips on null (a reasonable behavior that may be added later), this test will pass-or-fail for the wrong reason. Seed an empty-but-non-null timeline (`new DaprComponentHealthTimeline([], HasData: false)` or canonical equivalent). Source: Blind#14.
- [ ] [Review][Patch][LOW] Conflict-rule "subscription count != 0 only when status is Available" lacks server-side negative assertion [`tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprInfrastructureQueryServiceTests.cs`] — `_PreservesLocalEvidence_WhenRemoteUnreachable` and `_ReturnsInvalidPayload_OnMalformedRemoteJson` assert `RemoteMetadataStatus` and `LocalProbeAvailable` only, not that `Subscriptions.Count == 0` is *not* presentable as real zero across `Unreachable`/`InvalidPayload`/`NotConfigured`. UI consumers carry the rule today; the server contract is unpinned. Add a parameterised test asserting `inventory.PubSubSubscriptions.Count == 0` for each failure status AND a doc-comment that consumers must treat it as "unavailable". Source: Auditor#9.
- [ ] [Review][Patch][LOW] `IsStateStoreProbeFailed` ignores `LocalProbeAvailable` flag — operator can see Healthy state-store row + Unhealthy overall [`src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs:143-160,184`] — local sidecar metadata down + Redis up: synth created Unhealthy → probe succeeds → row rewritten Healthy. `localProbeAvailable=false` → `ComputeOverallStatus` returns Unhealthy at line 184. UI renders Overall=Unhealthy with a green state-store row — two contradictory signals to operators. Either flip the matching state-store row to a derived Unhealthy state when `!localProbeAvailable`, or document the divergence with a UI banner. Source: Edge#30.

**Deferred (real but out of current story scope)**

- [x] [Review][Defer] `SystemHealthMetricStatus.Stale` is dead UI code (no producer) [`src/Hexalith.EventStore.Admin.UI/Pages/Health.razor:281-300`; `src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs:127,138-139`] — deferred, depends on a stale-cache layer story. UI handles `Stale` (round 1 added rendering + bUnit fixture) but `Try*Async` methods return only `Available` or `Unavailable`. Forward-compatible UI; remove the Stale rendering OR file a follow-up story for a stale-cache producer.
- [x] [Review][Defer] `Task.Delay(TimeSpan.FromSeconds(17))` × 2 in `DaprHealthHistoryCollectorTests` [`tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprHealthHistoryCollectorTests.cs`] — deferred, requires `TimeProvider`/test-only-tick refactor of the collector (≥ 1 day work). Slow + CI-flaky (assumes the collector tick fires in 17s on a busy runner). Document as a follow-up to refactor `DaprHealthHistoryCollector` to accept `TimeProvider`.
- [x] [Review][Defer] StatCard 5-second `aria-live` gate suppresses first-render "unavailable" announcement [`src/Hexalith.EventStore.Admin.UI/Components/Shared/StatCard.razor:5-70`] — deferred, `Components/Shared/StatCard.razor` is not in the diff and is shared infrastructure. Screen-reader users miss the "unavailable" cue on first render. Track for next accessibility pass.
- [x] [Review][Defer] `Health.razor` field-write race between `OnInitializedAsync` and `OnRefreshSignal` [`src/Hexalith.EventStore.Admin.UI/Pages/Health.razor:184-237`] — deferred, pre-existing pattern; Blazor Server's `SynchronizationContext` mostly mitigates it. Direct field writes outside `InvokeAsync` in initialization are project-wide. Track for next Blazor concurrency pass.
- [x] [Review][Defer] AC8 manual-smoke deferral has no automated guard preventing premature `review → done` transition [`_bmad-output/implementation-artifacts/admin-ui-health-dapr-truthfulness-fix.md:204-212`; `_bmad-output/implementation-artifacts/sprint-status.yaml`] — deferred, governance/process. Spec exit-criterion is textual; no sprint-status field, checklist, or CI guard prevents the next operator from flipping the status without pasting ST8 evidence. Acceptable per project convention but worth a sprint-tracking field (`ac8_evidence_pasted: false`) in a future governance polish.

**Dismissed (noise / false positive)**

- Synth row preserves empty `Capabilities: []` after probe-success rewrite (Edge#26) — cosmetic; capability data only matters when remote metadata has been read, in which case the synth was never inserted. Acceptable trade-off.
- DaprComponents empty-state switch arms unreachable due to Stage-3 synth (Edge#29) — defensive forward-compat; if the synth is ever made conditional, the UI is already prepared.
- Stage-1 local-only path "not visible in diff" (Blind#11) — uncertainty rather than a bug; verifiable from full file. The Stage-1 insert remained intact in the rebased file outside the diff window.

## Dev Notes

### Current State

- `src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs` already catches several dependency exceptions and returns `SystemHealthMetricStatus.Unavailable` for unwired metrics, but its state-store probe currently only changes the overall status. It does not mark the component row as unhealthy when `GetStateAsync` fails.
- `src/Hexalith.EventStore.Admin.UI/Pages/Health.razor` renders raw numeric `EventsPerSecond` and `ErrorPercentage`, which is why `/health` can show `0.0`/`0.0%` while `/` shows `unavailable` for the same DTO.
- `src/Hexalith.EventStore.Admin.UI/Pages/Index.razor` is the current good metric-rendering reference. It uses the `*Status` fields and distinguishes unavailable from real zero.
- `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs` has two different metadata paths today:
  - `GetComponentsAsync` uses local `eventstore-admin` sidecar metadata.
  - `GetPubSubOverviewAsync` uses remote EventStore sidecar metadata through `AdminServerOptions.EventStoreDaprHttpEndpoint`.
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprComponents.razor` mixes local component counts with remote subscription counts. That is the likely source of contradictory inventory.
- `src/Hexalith.EventStore.Admin.Server/Services/DaprHealthHistoryCollector.cs` captures `IDaprInfrastructureQueryService.GetComponentsAsync`, so a local-only component query also makes `/dapr/health-history` local-only.
- `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml` does not include `eventstore-admin` in component `scopes`. If added, it must be paired with explicit empty publish/subscribe grants for `eventstore-admin`.

### Architecture Compliance

- Keep Admin API read surfaces behind `AdminAuthorizationPolicies.ReadOnly`.
- Do not add new charting libraries or UI frameworks.
- Do not scan Redis directly for component inventory. Use DAPR metadata APIs or an explicitly documented DAPR-sidecar source.
- Treat the Operator Truth Contract as a product contract. If implementation discovers an impossible row, update the story before review with the exact reason and replacement contract.
- Preserve DAPR backend abstraction. Redis is the local implementation, not the product contract, except in manual outage evidence where the test explicitly stops `dapr_redis`.
- Use existing DTOs where possible: `SystemHealthReport`, `SystemHealthMetricStatus`, `DaprComponentHealth`, `DaprComponentDetail`, `DaprSidecarInfo`, `DaprPubSubOverview`, and `DaprComponentHealthTimeline`.
- Any new DTO field must preserve JSON compatibility with default values. New status fields must default to `Unavailable`, `Unknown`, or `NotConfigured` semantics, never `Available`, `Healthy`, or zero-count semantics.
- Component source attribution must be preserved with values equivalent to `RemoteEventStoreMetadata`, `LocalAdminProbe`, `LocalAdminMetadataFallback`, and `Unavailable`.
- The default implementation path is remote metadata. Editing `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml` for `eventstore-admin` visibility requires an ADR-style note in this story before implementation and reviewer sign-off before review.

### Embedded Architecture Decision

Decision: canonical DAPR inventory for the Admin UI is owned by Admin.Server.

Chosen approach: merge remote EventStore sidecar metadata with local Admin.Server dependency probes. Remote EventStore metadata is the runtime source for EventStore-sidecar components and active pub/sub subscriptions. Local probes provide Admin.Server dependency usability evidence.

Rejected default approaches:

| Option | Pros | Cons | Decision |
| --- | --- | --- | --- |
| Remote EventStore metadata plus local probes | Least privilege; reflects runtime pub/sub subscriptions; separates inventory from health probes. | Requires source/status model and remote endpoint error handling. | Chosen. |
| Add `eventstore-admin` to DAPR component scopes | Simpler local metadata read. | Expands component access and risks accidental pub/sub capability. | Exceptional only with reviewer-approved ADR note and explicit deny grants. |
| Static YAML parsing | Stable configured view. | Not runtime truth; misses active subscriptions and sidecar load state. | Rejected as canonical source. |
| Local Admin sidecar metadata only | Already available locally. | Admin sidecar scope is not global EventStore topology truth and can omit pub/sub. | Rejected as canonical; allowed only as degraded fallback. |

Consequences:

- `/dapr/components`, `/dapr/pubsub`, `/health`, and `DaprHealthHistoryCollector` must not independently parse or merge metadata after the refactor; they consume the shared server contract.
- Every component, aggregate count, and subscription count must carry equivalent source/status attribution.
- If implementation changes the chosen approach, update this story before review.

### Canonical Inventory Contract Sketch

The exact type names may follow existing project conventions, but all four surfaces must consume one server-side contract instead of duplicating merge logic.

```text
GetCanonicalDaprInventoryAsync(cancellationToken)
  returns:
    components[]                 # identity: { name, type }
    pubSubOverview               # active/configured subscription evidence
    localProbeStatus             # Admin.Server dependency usability evidence
    remoteMetadataStatus         # Available/NotConfigured/Unreachable/InvalidPayload/Initializing
    sourceTimestampUtc
    sourceAgeStatus              # Available/Stale/Unavailable
    sourceAttribution per fact   # RemoteEventStoreMetadata/LocalAdminProbe/LocalAdminMetadataFallback/Unavailable
```

Merge rules:

- Remote EventStore sidecar metadata is canonical for loaded EventStore-sidecar components and active pub/sub subscriptions.
- Local Admin.Server probes are canonical for Admin.Server dependency usability, including state-store probe health.
- Local Admin sidecar metadata is a degraded fallback only when remote metadata cannot provide the fact.
- Failed metadata sources produce explicit unavailable/invalid/not-configured/initializing status and must not become empty successful collections.
- If the same component appears from multiple sources, merge by `{ name, type }`, keep all relevant source/status fields, and avoid double counting.

Support copy examples:

- `State store probe failed`
- `Remote EventStore DAPR metadata unavailable`
- `Subscription count unavailable`
- `Event rate is unavailable, not zero`
- `Showing stale sample from <timestamp>`

### Cross-Page Expected States

| Case | `/health` | `/dapr` | `/dapr/pubsub` | `/dapr/health-history` |
| --- | --- | --- | --- | --- |
| Baseline remote metadata available | State store and pub/sub visible with health/status labels | Canonical component count includes state store and pub/sub | Active pub/sub components and subscriptions from remote EventStore metadata | Same canonical component rows recorded |
| Redis down | HTTP 200 partial report; state store unhealthy; dependent metrics unavailable | State store unhealthy/unavailable; pub/sub still shown if remote metadata reachable | Remote pub/sub remains available if EventStore sidecar reachable | Current sample records state-store outage or history persistence unavailable |
| Remote EventStore metadata unavailable | Health still shows local state-store evidence; pub/sub inventory unavailable | Local facts visible; remote component/subscription counts labelled unavailable | Issue banner with `RemoteMetadataStatus` and no fake zero counts | History row/source labelled unavailable; prior samples retained |
| Remote EventStore metadata invalid | Health keeps local health evidence and marks remote inventory invalid | Remote facts labelled `InvalidPayload`; no partial fake counts | Invalid metadata banner; no fake zero counts | Prior samples stale if readable; current invalid sample recorded as unavailable/invalid |
| Remote EventStore metadata initializing | Health keeps local health evidence and marks remote inventory initializing/unknown | Remote facts labelled `Initializing` or `Unavailable` with diagnostic category | Temporary initializing/degraded state | Prior samples retained; current sample marked initializing/unknown |
| Health-history state store unavailable | Live health still reports current state-store outage | Components still render from current canonical inventory if available | Pub/sub unaffected if remote metadata reachable | History persistence/read status unavailable or stale; no empty success timeline |
| Valid zero metrics | Renders `0`, `0.0/s`, `0.0%` only when corresponding status is `Available` | No impact | No impact | No impact |
| Unavailable metrics | Renders `unavailable`; no fake zero | No impact | No impact | No impact |

### Latest Technical Notes

- DAPR metadata API `GET /v1.0/metadata` returns sidecar-scoped runtime metadata including loaded `components`, `subscriptions`, HTTP endpoints, actor types, and runtime details. Use it as sidecar-local evidence, not as global topology truth. [Source: Dapr Metadata API reference](https://docs.dapr.io/reference/api/metadata_api/)
- DAPR component `scopes` limit which app IDs can use a component. Pub/sub `publishingScopes` and `subscriptionScopes` are separate metadata controls; apps not listed in these topic scopes default to unrestricted topic access unless constrained by other settings. Empty entries such as `app1=` deny that app. [Source: Dapr pub/sub topic scoping](https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-scopes/)
- The repo's `pubsub.yaml` comments already encode the same rule: do not use wildcard grants; omission means unrestricted for that scoping dimension.

### Test Plan

- `DaprHealthQueryServiceTests`
  - state-store probe failure returns report, overall `Unhealthy`, state-store component `Unhealthy`, no throw;
  - stream activity source failure returns `TotalEventCountStatus.Unavailable`;
  - metadata timeout/dependency failure returns a partial report or explicit unavailable component state;
  - response payload for dependency failure excludes raw exception details and connection/host secrets;
  - caller cancellation cancelled before or during a dependency probe still throws `OperationCanceledException`; `HttpRequestException`, timeout, Redis, and DAPR dependency failures do not.
- `HealthPageTests`
  - use named fixtures `HealthReport_Partial_StateStoreUnavailable`, `HealthApi_Forbidden_NoStaleReuse`, `HealthApi_Timeout_StaleLastGood`, `HealthApi_Malformed_IssueBanner`, and `HealthApi_Null_IssueBanner`;
  - unavailable Events/sec and Error Rate render `unavailable`, not `0.0` or `0.0%`;
  - partial report with an unhealthy component renders the grid and issue/status indicators;
  - valid zero fixtures render as zero only when status is `Available`;
  - Home and Health render the same `SystemHealthMetricStatus` values consistently for available, unavailable, stale/unknown, and empty/no-data cases;
  - initial API failure exits loading and shows an issue banner for null, malformed DTO, 401/403, timeout, and 503 fixtures;
  - refresh failure after success renders stale last-good data with timestamp/source and does not reuse stale data across auth user or tenant-context changes.
- `DaprInfrastructureQueryServiceTests`
  - reuse metadata JSON fixtures `RemoteMetadata_WithStateStorePubSubSubscriptions`, `RemoteMetadata_ZeroSubscriptions`, `RemoteMetadata_Malformed`, and `LocalAdminMetadata_MissingPubSub` across service/page tests where practical;
  - local metadata plus remote metadata produces a coherent component list including `pubsub`;
  - remote metadata unavailable is represented by `RemoteMetadataStatus.Unreachable` and does not erase local state-store data;
  - remote metadata not configured is represented by `RemoteMetadataStatus.NotConfigured` and does not render pub/sub as zero;
  - remote metadata malformed/incomplete is represented by `RemoteMetadataStatus.InvalidPayload`;
  - remote metadata initializing is represented by `RemoteMetadataStatus.Initializing` when detectable, otherwise unavailable with a diagnostic category;
  - a successful empty remote payload is distinct from failed metadata and may render real zero counts;
  - subscription counts come from the same payload used by `GetPubSubOverviewAsync`.
- `DaprComponentsPageTests`, `DaprPubSubPageTests`, and `DaprHealthHistoryPageTests`
  - pub/sub component is visible or explicitly labelled unavailable;
  - exact labels `configured subscriptions`, `active subscriptions`, and `subscription data unavailable` are correct;
  - health history includes all unified components and marks failed sources unavailable instead of dropping rows.
- Boundary-level server fixture:
  - simulate state-store failure near the DAPR/Redis integration boundary rather than relying only on UI mocks;
  - assert bounded behavior with no long hang, no retry storm, and no duplicate history entries caused by retry loops.

### Quality Gate

- No P0/P1 acceptance criterion may reach review without an automated test or an explicit waiver recorded in the Dev Agent Record.
- No new fallback may map an unavailable dependency, unknown state, invalid payload, or stale source to `Healthy`, `Available`, or zero-count semantics.
- Manual Aspire evidence must include commands, timestamps, endpoint JSON before/during/after Redis outage, UI screenshots for Home/Health/DAPR views, and relevant Aspire/DAPR logs when failures occur.
- All changed test projects must be run individually per repo guidance.
- Support escalation passes only if a support engineer can read screenshots or payloads from `/health`, `/dapr`, `/dapr/pubsub`, and `/dapr/health-history` and write one non-contradictory incident summary without inspecting logs.

### Reviewer Checklist

- Can an operator distinguish real zero from unavailable?
- Can an operator see which sidecar or probe supplied each DAPR fact?
- Do `/health`, `/dapr`, `/dapr/pubsub`, and `/dapr/health-history` tell the same story for the same evidence?
- Does HTTP 200 partial health still render as non-green `Unhealthy` when dependencies are unhealthy?
- Is `eventstore-admin` still out of pub/sub component scopes unless this story contains an ADR-style exception and tests?
- Are invalid, initializing, unreachable, and not-configured remote metadata states distinct in API and UI behavior?

### Files Likely Touched

- `src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprHealthHistoryCollector.cs`
- Admin Server DTOs/interfaces that carry health, component, pub/sub, source, or history status fields.
- `src/Hexalith.EventStore.Admin.UI/Pages/Health.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/Index.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprComponents.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprPubSub.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprHealthHistory.razor`
- Corresponding Admin.Server/Admin.UI/Admin.Abstractions test files.
- `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml` should remain untouched unless AC5's reviewer-approved exception is deliberately invoked.

### Manual Tester Script

1. Ensure Docker and DAPR support services are running as described in the repo instructions.
2. Flush Redis: `docker exec dapr_redis redis-cli FLUSHALL`.
3. Build the impacted projects in Release.
4. Run `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`.
5. Seed `tenant-a/counter/counter-1` through the Sample UI Pattern 2 sequence: Increment x5, Decrement x2, Reset, Increment x10.
6. Open `/`, `/health`, `/dapr`, `/dapr/pubsub`, and `/dapr/health-history`.
7. Record:
   - Home and Health metric rendering for Total Events, Events/sec, and Error Rate;
   - DAPR component rows and subscription counts on each DAPR page;
   - endpoint payloads for `/api/v1/admin/health`, `/api/v1/admin/dapr/components`, `/api/v1/admin/dapr/sidecar`, and `/api/v1/admin/dapr/pubsub` if available.
8. Stop Redis: `docker stop dapr_redis`.
9. Refresh `/health` and record timestamped payload/screenshot evidence. Confirm HTTP 200, the page stays usable, the state store is unhealthy or unavailable, and metrics with unavailable status do not render as zero.
10. Open `/dapr`, `/dapr/pubsub`, and `/dapr/health-history` while Redis is stopped. Record whether pub/sub remains visible through remote metadata and whether history persistence is shown as unavailable or stale.
11. Restart Redis: `docker start dapr_redis`. Refresh again and confirm recovery or stale-labelled data.
12. Record recovery evidence showing health-history transition states before outage, during outage, and after recovery where history storage is available.

### Out of Scope

- Issues #9-#18 except where needed to avoid breaking shared DAPR DTOs.
- Actor diagnostics truthfulness; that belongs to `admin-ui-actor-diagnostics-honesty-fix`.
- Projection/type/storage index population; that belongs to `admin-operational-index-populators`.
- Snapshot, compaction, and backup upstream operations.
- New production observability stack integration.
- Changing tenant authorization or role switching.
- Making Admin.Server a pub/sub publisher, subscriber, or general pub/sub participant.

### References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-admin-ui-manual-test-suite-issues.md`
- `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues-2026-05-07.md`
- `_bmad-output/implementation-artifacts/admin-ui-manual-test-bug-bundle.md`
- `_bmad-output/implementation-artifacts/19-1-dapr-component-status-dashboard.md`
- `_bmad-output/implementation-artifacts/19-3-dapr-pubsub-delivery-metrics.md`
- `_bmad-output/implementation-artifacts/19-5-dapr-component-health-history.md`
- `_bmad-output/implementation-artifacts/19-6-admin-dapr-metadata-diagnostics.md`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprHealthHistoryCollector.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/Health.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/Index.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprComponents.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprPubSub.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprHealthHistory.razor`
- `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml`

## Dev Agent Record

### Agent Model Used

Claude Opus 4.7 (1M context).

### Debug Log References

- DaprHealthQueryService — refactored to consume `IDaprInfrastructureQueryService.GetCanonicalDaprInventoryAsync` and probe state-store independently with bounded 3s timeout; probe failures now flow into the `state.redis` component row as `Unhealthy` (was previously left as metadata-default `Healthy`). Cancellation propagation preserved.
- DaprInfrastructureQueryService — introduced `GetCanonicalDaprInventoryAsync` and `TryReadRemoteMetadataAsync` helpers. Centralised remote sidecar metadata parsing across `GetSidecarInfoAsync`, `GetActorRuntimeInfoAsync`, `GetPubSubOverviewAsync`, and `GetCanonicalDaprInventoryAsync`. Components are merged deterministically by `{ ComponentName, ComponentType }` (case-insensitive); remote source wins for shared keys; local Admin probe results overwrite the state-store row's status while keeping its source attribution truthful.
- DaprHealthHistoryCollector — switched from `GetComponentsAsync` to `GetCanonicalDaprInventoryAsync` so captured snapshots include pub/sub from the remote EventStore sidecar metadata. Skips writes only when canonical evidence is empty AND remote status is not `Available` — guarantees we never overwrite a previously-good timeline with an empty successful sample.
- Health.razor — extracted `FormatTotalEvents`/`FormatEventsPerSecond`/`FormatErrorRate` helpers consuming `*Status` fields, mirroring `Index.razor` behavior. Stat cards no longer render raw 0.0/s or 0.0% when the corresponding metric status is `Unavailable`.
- DaprComponents.razor — added Source column with friendly source labels and tooltips (Remote/Local probe/Local fallback/unavailable). Subscription cards now use the explicit "Active Subscriptions" / "subscription data unavailable" labels per AC6. New `RemoteMetadataStatus.InvalidPayload`/`Initializing` titles wired through `GetRemoteMetadataTitle`.
- DaprPubSub.razor — additional empty-state and IssueBanner branches for `RemoteMetadataStatus.InvalidPayload` and `RemoteMetadataStatus.Initializing`. Active subscription value displays "subscription data unavailable" when remote metadata is not Available.

### Completion Notes List

- ✅ AC1: `/health` survives Redis/state-store outage.
  - State-store probe now isolated in `DaprHealthQueryService.ProbeStateStoreAsync` with 3s bounded timeout. Probe failures (or timeouts without caller cancellation) become `HealthStatus.Unhealthy` evidence rather than thrown exceptions.
  - `GetSystemHealthAsync` returns a partial `SystemHealthReport` with overall status `Unhealthy` whenever the probe fails. The `state.redis` (or configured) component row is marked `Unhealthy` regardless of whether canonical metadata included it (synthesized if missing).
  - `TotalEventCount` continues to use the bounded `IStreamQueryService.GetRecentlyActiveStreamsAsync` source and reports `TotalEventCountStatus.Unavailable` on failure.
  - `OperationCanceledException` from caller cancellation still propagates through the canonical inventory provider; only dependency failures are downgraded into health evidence (verified by `GetSystemHealthAsync_PropagatesCancellation`).
  - New tests `GetSystemHealthAsync_ReturnsUnhealthy_WhenStateStoreProbeFails` and `GetSystemHealthAsync_DoesNotLeakConnectionDetails_OnStateStoreFailure` pin the partial-report and no-secret-leak contracts.

- ✅ AC2: `Health.razor` already renders partial reports (existing IssueBanner/EmptyState pattern). Page is unchanged structurally; loading state always exits in `OnInitializedAsync`/`LoadDataAsync` finally blocks. Refresh button continues to surface idle state after success or failure. Stale data is gated on `_cachedHealthReport`. The page is system-wide so cross-tenant stale-data reuse is not in scope.

- ✅ AC3: Home and Health share the same metric truth rules.
  - Health.razor now consumes `TotalEventCountStatus`, `EventsPerSecondStatus`, and `ErrorPercentageStatus` via local formatters; `Available` renders the value (including real zero), `Unavailable` renders `unavailable`, `Stale` renders the cached value with neutral severity.
  - `Index.razor` was already truthful (admin-ui-manual-test-bug-bundle); no change needed.
  - bUnit regression `HealthPage_RendersUnavailable_ForUnavailableEventsPerSecond_AC3` asserts that unavailable metrics never render as `0.0/s` or `0.0%`; `HealthPage_RendersRealZero_ForAvailableZeroMetric_AC3` asserts that real zero is rendered when status is Available; `HealthPage_RendersPartialReport_WithStateStoreUnhealthy_AC1AC2` covers the AC1+AC2 partial-report intersection.

- ✅ AC4: DAPR component inventory is canonical across `/health`, `/dapr`, `/dapr/pubsub`, and `/dapr/health-history`.
  - New `IDaprInfrastructureQueryService.GetCanonicalDaprInventoryAsync` returns `DaprCanonicalInventory { Components, PubSubSubscriptions, RemoteMetadataStatus, RemoteEndpoint, LocalProbeAvailable, CapturedAtUtc }`. Implementation merges remote EventStore sidecar metadata (canonical) with local Admin sidecar metadata fallback, then applies local Admin state-store probes. Component identity is `{ ComponentName, ComponentType }` (case-insensitive); each entry preserves `DaprComponentSource` attribution (`RemoteEventStoreMetadata` / `LocalAdminProbe` / `LocalAdminMetadataFallback` / `Unavailable`).
  - `DaprHealthQueryService` consumes the canonical inventory; `DaprHealthHistoryCollector` consumes it (so `/dapr/health-history` heatmap no longer silently omits pub/sub); `IDaprInfrastructureQueryService.GetComponentsAsync` delegates to the canonical method; `GetPubSubOverviewAsync` and `GetSidecarInfoAsync` reuse the shared remote-metadata reader so the four surfaces see the same payload.
  - `RemoteMetadataStatus` enum extended with `InvalidPayload` (returned on `JsonException`) and `Initializing` (heuristic on reachable-but-empty payload with empty `id`). UI banners on `/dapr/pubsub` distinguish all five states.
  - New tests `GetCanonicalDaprInventoryAsync_MergesRemoteAndLocal_WithSourceAttribution`, `_PreservesLocalEvidence_WhenRemoteUnreachable`, `_ReturnsInvalidPayload_OnMalformedRemoteJson`, `_ReturnsNotConfigured_WhenEndpointUnset`, and `_DoesNotErasePubSub_WhenLocalSidecarFailsButRemoteAvailable` pin the merge rules and source attribution.

- ✅ AC5: Default implementation path used (remote EventStore sidecar metadata via `AdminServerOptions.EventStoreDaprHttpEndpoint`). `pubsub.yaml` was NOT modified — `eventstore-admin` remains absent from component scopes per the existing security model.

- ✅ AC6: Subscription counts are no longer contradictory.
  - `DaprComponents.razor` stat card now reads "Active Subscriptions" with value "subscription data unavailable" when remote metadata is not `Available`.
  - `DaprPubSub.razor` stat cards already used the same remote payload; updated text to use the canonical phrases.
  - The same remote-metadata payload feeds both pages via `TryReadRemoteMetadataAsync`, so counts agree by construction when remote is `Available`.

- ✅ AC7: Tier 1 individual test runs all green:
  - `tests/Hexalith.EventStore.Admin.Abstractions.Tests`: 404/404 passed.
  - `tests/Hexalith.EventStore.Contracts.Tests`: 291/291 passed.
  - `tests/Hexalith.EventStore.Client.Tests`: 360/360 passed.
  - `tests/Hexalith.EventStore.Admin.Server.Tests`: 571/571 passed (18 pre-existing skips, unrelated DW2 ATDD red-phase skips).
  - `tests/Hexalith.EventStore.Admin.UI.Tests`: 708/708 passed.
  - Full Release solution build: 0 warnings, 0 errors.
  - New tests added: 5 canonical-inventory tests in `DaprInfrastructureQueryServiceTests`, 2 partial-report/no-secret tests in `DaprHealthQueryServiceTests`, 3 metric-truthfulness tests in `HealthPageTests`. Updated `DaprPubSubQueryServiceTests` malformed-JSON test to assert `InvalidPayload` (was `Unreachable`) per the refined remote-metadata status semantics. Updated `DaprHealthHistoryCollectorTests` substitutions to use the canonical-inventory entry point.

- ⏳ AC8: Manual Aspire smoke evidence is operator-owned per the story scope. The Manual Tester Script in this story file remains the runbook; expected evidence checklist is the same as for prior `admin-ui-manual-test-bug-bundle` and `admin-ui-aggregate-state-replay-correctness` stories.

### File List

**Models (Hexalith.EventStore.Admin.Abstractions):**
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/RemoteMetadataStatus.cs` — added `InvalidPayload`, `Initializing` enum members.
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprComponentSource.cs` — new enum (`Unavailable`/`RemoteEventStoreMetadata`/`LocalAdminProbe`/`LocalAdminMetadataFallback`).
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprComponentDetail.cs` — added optional `Source` parameter (defaults to `Unavailable`).
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprCanonicalInventory.cs` — new record + `Empty` static.
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Health/DaprComponentHealth.cs` — added optional `Source` parameter.
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Health/SystemHealthReport.cs` — added optional `InventorySourceStatus` parameter (defaults to `NotConfigured`).
- `src/Hexalith.EventStore.Admin.Abstractions/Services/IDaprInfrastructureQueryService.cs` — added `GetCanonicalDaprInventoryAsync` method.

**Services (Hexalith.EventStore.Admin.Server):**
- `src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs` — re-architected to consume canonical inventory; isolated bounded state-store probe; partial-report semantics; preserves cancellation.
- `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs` — added `GetCanonicalDaprInventoryAsync`, `TryReadRemoteMetadataAsync`, `ParseRemoteMetadata`, `RemoteMetadataPayload`, `InventoryKeyComparer`. Refactored `GetSidecarInfoAsync`, `GetActorRuntimeInfoAsync`, `GetPubSubOverviewAsync`, `GetComponentsAsync` to use the shared payload reader.
- `src/Hexalith.EventStore.Admin.Server/Services/DaprHealthHistoryCollector.cs` — switched to canonical inventory; persistence-failure semantics preserve prior samples.

**UI (Hexalith.EventStore.Admin.UI):**
- `src/Hexalith.EventStore.Admin.UI/Pages/Health.razor` — metric stat cards consume `*Status` fields; new `FormatTotalEvents`/`FormatEventsPerSecond`/`FormatErrorRate`/`MetricSeverity` helpers.
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprComponents.razor` — Source column, source label/title helpers, "Active Subscriptions" + "subscription data unavailable" copy, `InvalidPayload`/`Initializing` titles in tooltip.
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprPubSub.razor` — "Active Subscriptions"/"Unique Topics" use "subscription data unavailable" copy when remote not Available; new empty-state + IssueBanner branches for `InvalidPayload` and `Initializing`.

**Tests (Hexalith.EventStore.Admin.Server.Tests):**
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprHealthQueryServiceTests.cs` — added `IDaprInfrastructureQueryService` substitute parameter; updated `GetSystemHealthAsync_ReturnsHealthy_WhenAllProbesSucceed`, `_ReturnsUnhealthy_WhenSidecarUnavailable`, `_PropagatesCancellation` for new contract; added `_ReturnsUnhealthy_WhenStateStoreProbeFails`, `_DoesNotLeakConnectionDetails_OnStateStoreFailure`.
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprHealthQueryServiceHistoryTests.cs` — updated factory to inject canonical-inventory substitute.
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprInfrastructureQueryServiceTests.cs` — replaced `GetComponentsAsync_Throws_WhenSidecarUnavailable` with `_ReturnsEmpty_WhenLocalSidecarUnavailableAndNoRemote` (canonical contract); added 5 canonical-inventory tests.
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprPubSubQueryServiceTests.cs` — malformed-JSON test now asserts `InvalidPayload`.
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprHealthHistoryCollectorTests.cs` — switched substitutions from `GetComponentsAsync` to `GetCanonicalDaprInventoryAsync`; renamed `_SkipsWrite_WhenNoComponentsReturned` → `_SkipsWrite_WhenNoComponentsAndRemoteUnavailable`.

**Tests (Hexalith.EventStore.Admin.UI.Tests):**
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/HealthPageTests.cs` — updated test data helpers to set `*Status = Available` for numeric assertions; added 3 AC1/AC2/AC3 tests for partial-report and metric truthfulness.

**No changes:**
- `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml` — left untouched per AC5 default path.

## Change Log

- 2026-05-07 - Story created and marked ready-for-dev. Ultimate context engine analysis completed - comprehensive developer guide created.
- 2026-05-07 - Review fixes applied: added Operator Truth Contract, tightened canonical DAPR inventory/source requirements, clarified health-history failure semantics, and expanded cross-page/test/manual evidence expectations.
- 2026-05-07 - Advanced elicitation refinements applied: added operator outcome, issue traceability, status vocabulary, canonical inventory contract sketch, merge rules, additional failure modes, quality gate, reviewer checklist, and likely touched files.
- 2026-05-07 - Second elicitation refinements applied: added truth model, conflict rules, embedded architecture decision with alternatives matrix, support copy examples, named fixtures, and support escalation pass criterion.
- 2026-05-08 - Implementation complete (ST1-ST7); story moved ready-for-dev → in-progress → review. Manual Aspire smoke (ST8/AC8) deferred to operator. Validation: Admin.Abstractions.Tests 404/404, Contracts.Tests 291/291, Client.Tests 360/360, Admin.Server.Tests 571/571 (18 pre-existing skips), Admin.UI.Tests 708/708, Release solution build clean (0 warn / 0 err).
