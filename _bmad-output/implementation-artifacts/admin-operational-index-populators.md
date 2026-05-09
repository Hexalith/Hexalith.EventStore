# Story: admin-operational-index-populators

Status: ready-for-dev

Context created: 2026-05-09
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-admin-ui-manual-test-suite-issues.md`
Triggering evidence: `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues-2026-05-07.md`
Scope: Issues #11, #12, #14, and initial #17 retest only.

## Story

As an EventStore operator using the Admin UI,
I want the projection, type-catalog, and storage operational indexes to be populated from the real EventStore runtime,
so that `/projections`, `/types`, `/storage`, and the projection-related consistency checks show trustworthy data instead of placeholder zeros or false anomalies.

## Issue Traceability

| Issue | Failing symptom | Covered by | Required evidence |
| --- | --- | --- | --- |
| #11 | `/projections` reads `admin:projections:*`, but no writer exists; canonical seed shows empty projection cards and grid. | AC1, AC2, AC5, AC6, AC7 | State-store writer tests; `/projections` payload/UI evidence; no confusion with query-cache `ProjectionActor` keys. |
| #12 | `/types` reads `admin:type-catalog:*`, but no writer exists; discovered Counter domain types are not visible. | AC3, AC5, AC6, AC7 | Boot-time catalog writer tests; Counter event/command/aggregate visible under `all` and domain keys. |
| #14 | `/storage` reads `admin:storage-overview:*`, `admin:storage-hot-streams:*`, and `admin:storage-stream-count:*`; no complete writer exists, so seeded events render as 0/0/0/N/A. | AC4, AC5, AC6, AC7 | Storage index writer tests; canonical seed storage page/API evidence; exact vs unavailable semantics. |
| #17 | `/consistency` reported likely false anomalies after canonical seed, probably because `admin:projections:*` was empty. | AC2, AC6, AC7 | Retest after projection index exists; if anomalies remain, create/defer a separate consistency correctness story. |

## Operational Index Truth Contract

- Admin indexes are derived operational views, not event-source-of-truth data. Event streams, actor state, and domain-service declarations remain authoritative.
- Missing writer, failed writer, unavailable source, and genuinely empty data must be distinguishable in logs, tests, API payloads where practical, and UI copy.
- The minimum operational state vocabulary for projection, type-catalog, and storage index reads is `MissingWriter`, `WriterFailed`, `Unavailable`, `Stale`, `Empty`, and `Populated`. These states may be represented through existing DTO fields, additive nullable fields, or endpoint-specific metadata, but they must not collapse into placeholder `0` values or healthy empty copy.
- Query-cache `ProjectionActor` state keys such as `eventstore||ProjectionActor||...||projection-state` are not named projections. Do not expose them as `/projections` rows.
- Type catalog data is boot-time/static enough to populate from the same discovery metadata used by EventStore domain registration. Do not invent a second naming convention.
- Storage metrics may have backend-dependent gaps. `TotalSizeBytes`, per-stream `SizeBytes`, and growth rates may remain unavailable under NFR44, but event counts, stream counts, and hot-stream identities must reflect persisted activity when the canonical seed has run.
- Index updates must be bounded and concurrency-safe. Prefer the existing state-store ETag retry pattern from `DaprStreamActivityTracker` over broad scans or unbounded rewrites.
- The Admin UI must not present placeholder zeros as operational facts. If a source is unavailable, missing, failed, stale, or not configured, render plain non-color-only state labels instead of a healthy empty state. State labels should remain stable enough for localization.

## Acceptance Criteria

1. **Projection admin index has a real writer and clear source model.**
   - Given EventStore starts with discovered or configured named projections
   - When the projection registry/populator runs
   - Then it writes `admin:projections:all` and any tenant-scoped keys required by `DaprProjectionQueryService.ListProjectionsAsync`.
   - And `/api/v1/admin/projections` and `/projections` show named projection rows with status, lag, throughput, error count, last processed position, and last processed time when evidence exists.
   - And the implementation does not treat query-cache `ProjectionActor` instances as named projections.
   - And tests prove query-cache `ProjectionActor` keys are rejected as registry input, not just hidden by UI copy.
   - And projection index writes are idempotent across restart or repeated discovery with stable key count and representative payload equality.
   - And if the sample has no named projection after explicit architecture review, the story records that decision and updates the manual guide expectation to an honest empty state rather than silently passing.

2. **Projection index feeds consistency checks without creating false positives.**
   - Given `DaprConsistencyCommandService` reads projection indexes for projection-position checks
   - When the projection index writer is available
   - Then consistency checks use the populated projection registry instead of an absent index.
   - And a canonical-seed retest for `tenant-a/counter` records whether Issue #17 is resolved by exercising the path from discovered projection metadata through stored operational index to Admin consistency read/display behavior.
   - And if anomalies remain after the projection index exists, this story records a deferred consistency-correctness item with check id, payload summary, and suspected remaining cause rather than tuning the consistency algorithm in this story.
   - And this story does not change the broader consistency algorithm except for reading the populated projection index and recording the retest result.

3. **Type catalog indexes are populated at boot from discovered domain metadata.**
   - Given the EventStore host loads domain assemblies through the existing `AddEventStore` / `AssemblyScanner` discovery path
   - When startup completes
   - Then it writes `admin:type-catalog:events:all`, `admin:type-catalog:commands:all`, and `admin:type-catalog:aggregates:all`.
   - And it writes per-domain keys such as `admin:type-catalog:events:counter` when the domain is known.
   - And `/types` shows Counter sample event, command, and aggregate types after canonical seed or boot, including correct domain and stable schema version defaults.
   - And type catalog population is idempotent across restarts, duplicate discovery results, and repeated host startup without broad rescanning or a second naming convention.

4. **Storage operational indexes are populated from event activity.**
   - Given commands append events for one or more streams
   - When the existing stream activity tracker or a narrowly scoped companion writer runs
   - Then `admin:storage-overview:all`, `admin:storage-hot-streams:all`, and `admin:storage-stream-count:all` reflect total events, distinct streams, tenant breakdown, and hot streams.
   - And tenant-scoped keys are written or the query service derives tenant scopes from the all-index deterministically.
   - And `/storage` shows the canonical `tenant-a/counter/counter-1` seed with non-zero event and stream counts.
   - And hot-stream ordering is either deterministic by persisted event activity or explicitly recorded as approximate recent-activity ordering in the Dev Agent Record.
   - And unavailable byte-size or growth-rate values remain unavailable/`N/A` without blocking event-count and stream-count truthfulness.
   - And byte-size and growth-rate estimation remain out of scope unless a backend-agnostic authoritative source already exists.

5. **Index write placement respects architecture boundaries.**
   - Admin.Server may continue reading admin indexes through DAPR state store, but it must not become the primary writer by scraping UI/API reads.
   - EventStore-side pipeline/startup writers are preferred because EventStore owns domain discovery and event persistence evidence; a dedicated EventStore-owned background indexing service is acceptable if it is apphost-wired from those same sources.
   - Admin.UI and Admin.Server query paths must not infer persisted activity from cache actor keys, UI-time aggregation, or read-time state-store scans.
   - Any Admin.Server-side writer requires an explicit Dev Agent Record rationale showing why EventStore cannot populate the index, how duplicate writers are avoided, and which product/architecture decision approved the exception.
   - All writes use the configured state store component name and preserve DAPR backend portability under NFR44.
   - Any additive API fields used for data-quality state must be nullable/defaulted or otherwise backward compatible for existing Admin UI, CLI, and MCP consumers.

6. **Automated tests pin the populated and unavailable paths.**
   - Server/client tests cover boot-time type-catalog population, projection index population, storage overview/hot-stream population, deterministic ETag conflict retry behavior, missing-source/unavailable-source/malformed-or-partial-source behavior, duplicate discovery, DTO serialization compatibility, and idempotent restart.
   - ETag retry tests must force at least one stale-write conflict and assert bounded retry count, retryable conditions, logging or diagnostic behavior, and failure surfacing after retry exhaustion.
   - Admin.Server tests cover existing readers against populated indexes and ensure missing, unavailable, stale, and valid-empty indexes produce honest outcomes where the API shape supports it.
   - Admin.UI tests cover `/projections`, `/types`, and `/storage` rendering populated data and not rendering missing writer paths as healthy zero data.
   - Consistency tests cover the projection-index path enough to prove Issue #17 is retested without expanding this story into a full consistency algorithm rewrite.

7. **Manual Aspire evidence is captured before review.**
   - With `EnableKeycloak=false` Aspire dev mode, Redis flushed, and the canonical sample flow seeded, record endpoint payloads or screenshots for `/projections`, `/types`, `/storage`, and `/consistency`.
   - Capture state-store key evidence for `admin:projections:*`, `admin:type-catalog:*`, `admin:storage-overview:*`, `admin:storage-hot-streams:*`, and `admin:storage-stream-count:*`.
   - Record the Issue #17 retest result, including check id and anomaly count.
   - Save sanitized evidence under `_bmad-output/test-artifacts/admin-operational-index-populators/`, including endpoint JSON, UI screenshots when UI was exercised, state-store key listings or explicit absence proof, relevant structured log excerpts, the seed command/log summary, and a short README mapping artifacts to ACs.
   - If Aspire manual evidence is unavailable, record the exact blocker and substitute only bounded automated evidence; do not mark AC7 complete without either manual evidence or explicit reviewer acceptance of the blocker.

## Tasks / Subtasks

- [ ] **ST0 - Baseline existing readers, writers, and manual evidence.** (AC: 1, 3, 4, 7)
  - [ ] Re-read Issues #11, #12, #14, and #17 in the manual-test issue file before editing.
  - [ ] Inspect `DaprProjectionQueryService`, `DaprTypeCatalogService`, `DaprStorageQueryService`, `DaprConsistencyCommandService`, `DaprStreamActivityTracker`, `EventStoreServiceCollectionExtensions`, `AssemblyScanner`, and relevant controllers/UI pages.
  - [ ] Confirm current state-store keys and existing writer coverage with tests or local evidence.
  - [ ] Capture a short baseline in the Dev Agent Record if Aspire is unavailable.

- [ ] **ST1 - Design and implement the index writer boundary.** (AC: 1, 3, 4, 5)
  - [ ] Choose the minimal EventStore-side service(s) needed for projection, type-catalog, and storage index population.
  - [ ] Record the chosen writer boundary and any rejected Admin.Server/Admin.UI writer option in the Dev Agent Record.
  - [ ] Reuse existing discovery and stream activity metadata; do not duplicate naming convention logic.
  - [ ] Use bounded DAPR state-store writes with ETag retry where multiple writers can touch the same index.
  - [ ] Define retryable conflict conditions, retry exhaustion behavior, and diagnostic fields before wiring the writer.
  - [ ] Add source/status fields only where required to avoid fake zero semantics; keep DTO changes backward compatible where practical.

- [ ] **ST2 - Populate projection indexes.** (AC: 1, 2)
  - [ ] Add or wire a named projection registry source; if the sample lacks one, add a minimal Counter named projection only if it is within the approved product/architecture scope.
  - [ ] Write `admin:projections:all` and tenant/domain scoped variants needed by current readers.
  - [ ] Ensure query-cache `ProjectionActor` state is excluded by tests and comments.
  - [ ] Re-run or unit-test the projection-position consistency path against the populated index.

- [ ] **ST3 - Populate type catalog indexes.** (AC: 3)
  - [ ] Build `EventTypeInfo`, `CommandTypeInfo`, and `AggregateTypeInfo` from existing discovery/convention metadata.
  - [ ] Write `all` and per-domain keys.
  - [ ] Define schema version fallback explicitly; do not infer unsupported version history.
  - [ ] Add tests for Counter sample event/command/aggregate catalog entries and idempotent restart.

- [ ] **ST4 - Populate storage overview and hot-stream indexes.** (AC: 4)
  - [ ] Extend `DaprStreamActivityTracker` or a companion pipeline writer to update storage overview, tenant breakdown, exact stream count, and hot-stream rows from append evidence.
  - [ ] Preserve existing `admin:stream-activity:all` behavior and tests.
  - [ ] Treat size bytes and growth rate as unavailable unless a backend-agnostic source exists.
  - [ ] Add tests for distinct stream counting, per-tenant breakdown, hot-stream ordering, and ETag retry failure logging.

- [ ] **ST5 - Update UI/API honesty only where the writer contract exposes unavailable states.** (AC: 4, 6)
  - [ ] Keep `/projections`, `/types`, and `/storage` layouts stable unless copy must distinguish unavailable from empty.
  - [ ] Replace misleading zero/empty copy only where missing index data can still occur after this story.
  - [ ] Avoid broad visual redesign or unrelated Fluent UI cleanup.

- [ ] **ST6 - Validate and record evidence.** (AC: 6, 7)
  - [ ] Run impacted unit test projects individually per repository guidance.
  - [ ] Run canonical Aspire dev-mode seed when environment allows.
  - [ ] Save sanitized payload/screenshot/key evidence under `_bmad-output/test-artifacts/admin-operational-index-populators/`.
  - [ ] Include a dated baseline-vs-post-change evidence note in the Dev Agent Record with test command timestamps or run IDs.
  - [ ] Record Issue #17 retest result and any deferred follow-up in `deferred-work.md`.
  - [ ] Update this story's Dev Agent Record, File List, Verification Status, and Change Log.

## Developer Notes

Current code intelligence from story creation:

- `DaprProjectionQueryService.ListProjectionsAsync` reads `admin:projections:{tenantId ?? "all"}` and logs that index population is required when missing.
- `DaprTypeCatalogService` reads `admin:type-catalog:events:*`, `admin:type-catalog:commands:*`, and `admin:type-catalog:aggregates:*`; its comment says the event publication pipeline populates these indexes, but no writer was found.
- `DaprStorageQueryService` reads `admin:storage-overview:*`, `admin:storage-hot-streams:*`, and optional `admin:storage-stream-count:*`. It currently returns `new StorageOverview(0, null, [], 0)` when the overview index is missing, which can render a fake zero operational picture.
- `DaprStreamActivityTracker` already maintains `admin:stream-activity:all` with `GetStateAndETagAsync` / `TrySaveStateAsync`, max 1000 entries, and 3 optimistic-concurrency retries. Use this as the local pattern for bounded admin-index updates.
- `EventStoreServiceCollectionExtensions.AddEventStoreCore` stores `DiscoveryResult` as a singleton and registers discovered aggregates/projections from `AssemblyScanner`. Prefer this source for type-catalog population instead of rescanning unrelated assemblies.
- `AssemblyScanner` discovers concrete `EventStoreAggregate<TState>` and `EventStoreProjection<TReadModel>` types and resolves domain names through `NamingConventionEngine`.
- `/projections`, `/types`, `/storage`, and `/consistency` UI surfaces already exist. The primary defect is data-flow population and truthfulness, not missing pages.
- `admin-ui-health-dapr-truthfulness-fix` already established the project rule that unavailable evidence must not render as healthy, zero, or empty. Reuse that posture here.

Architecture and product guardrails:

- ADR-P4 in `_bmad-output/planning-artifacts/architecture.md` keeps Admin.Server as the shared admin API backing Web UI, CLI, and MCP. Read access may use DAPR state store; writes that mutate EventStore behavior go through EventStore. This story's index writes should live where EventStore has source evidence.
- PRD FR73 requires projection status/lag/throughput/error/position visibility; FR74 requires event/command/aggregate type catalog; FR76 requires storage growth and hot-stream management; FR79 requires shared Admin API access across UI, CLI, and MCP.
- PRD NFR40 requires admin reads to stay within 500ms p99, and NFR44 requires DAPR backend portability. Avoid broad state-store scans on read paths.
- Do not widen Issue #15. Snapshot, compaction, backup command endpoints and job indexes belong to `admin-storage-snapshot-compaction-backup-operations`.
- Do not absorb Issue #16. Consistency subtitle stale binding belongs to `admin-ui-consistency-and-tenant-clarity-polish`.

## Files Likely Touched

- `src/Hexalith.EventStore/Commands/DaprStreamActivityTracker.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionDiscoveryHostedService.cs`
- `src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs`
- `src/Hexalith.EventStore.Client/Discovery/AssemblyScanner.cs` only if a public/internal access seam is required
- `src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprTypeCatalogService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStorageQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprConsistencyCommandService.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Projections/*`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/TypeCatalog/*`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/*`
- `src/Hexalith.EventStore.Admin.UI/Pages/Projections.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/Storage.razor`
- `tests/Hexalith.EventStore.Client.Tests/Commands/DaprStreamActivityTrackerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionDiscoveryHostedServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprProjectionQueryServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprTypeCatalogServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStorageQueryServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprConsistencyCommandServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ProjectionsPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TypeCatalogPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/StoragePageTests.cs`
- `_bmad-output/test-artifacts/admin-operational-index-populators/`
- `_bmad-output/test-artifacts/admin-ui-manual-test-guide.md` only if expected manual-test outcomes change.

## Out of Scope

- Issues #6, #7, and #8 health/DAPR truthfulness.
- Issues #9 and #13 operator action dialog and dev role switching.
- Issue #10 actor diagnostics.
- Issue #15 snapshot, compaction, and backup upstream endpoint implementation.
- Issue #16 consistency stat-card subtitle stale binding.
- Issue #18 tenant delete clarity.
- Broad consistency algorithm rewrites after Issue #17 retest; record a deferred story if needed.
- Production Redis keyspace scanning as an index population strategy.
- DAPR component YAML, access-control policy, AppHost topology, or auth changes unless a narrow index-writer wiring defect is proven.

## References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-admin-ui-manual-test-suite-issues.md`
- `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues-2026-05-07.md`
- `_bmad-output/implementation-artifacts/admin-ui-health-dapr-truthfulness-fix.md`
- `_bmad-output/implementation-artifacts/admin-ui-actor-diagnostics-honesty-fix.md`
- `_bmad-output/planning-artifacts/prd.md#Functional Requirements`
- `_bmad-output/planning-artifacts/prd.md#Administration Tooling (NFR40-NFR46)`
- `_bmad-output/planning-artifacts/architecture.md#D19 Admin tooling as a separate application layer`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprTypeCatalogService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStorageQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprConsistencyCommandService.cs`
- `src/Hexalith.EventStore/Commands/DaprStreamActivityTracker.cs`
- `src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs`
- `src/Hexalith.EventStore.Client/Discovery/AssemblyScanner.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/Projections.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/Storage.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor`

## Dev Agent Record

### Agent Model Used

TBD by dev agent.

### Debug Log References

TBD by dev agent.

### Completion Notes List

- Story created and marked ready-for-dev by the BMAD pre-dev hardening automation.
- No `project-context.md` file was present in the repository at story creation.
- 2026-05-09 party-mode review applied pre-dev clarifications for operational state vocabulary, writer placement boundaries, retry/idempotency expectations, Issue #17 retest scope, and AC7 evidence shape.
- No implementation work has been performed for this story.

### File List

TBD by dev agent.

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Preflight passed before story creation.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, or submodules.
- Party-mode review completed on 2026-05-09 and is recorded below.
- Advanced elicitation has NOT yet been run for this story.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-09 | 0.2 | Applied party-mode review hardening for index state vocabulary, writer boundaries, retry/test expectations, and manual evidence requirements. | Codex automation |
| 2026-05-09 | 0.1 | Created ready-for-dev story for Admin operational index population across projections, type catalog, storage, and initial consistency retest. | Codex automation |

## Party-Mode Review

- Date/time: 2026-05-09T15:09:46+02:00
- Selected story key: `admin-operational-index-populators`
- Command/skill invocation used: `/bmad-party-mode admin-operational-index-populators; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary:
  - The story was directionally ready, but the handoff needed a shared operational state vocabulary so missing writer, writer failure, unavailable source, stale data, valid empty data, and populated data cannot collapse into placeholder zeros.
  - Writer placement needed sharper boundaries: EventStore-owned discovery and persisted-activity signals should populate derived admin indexes; Admin UI and read-time Admin.Server paths must not infer source truth from cache actor keys, UI aggregation, or broad scans.
  - ETag retry, idempotent restart, duplicate discovery, DTO compatibility, and Issue #17 retest criteria needed more observable test expectations before `bmad-dev-story`.
  - Manual evidence needed named artifacts and AC mapping so reviewers can distinguish endpoint payloads, UI state, state-store key evidence, logs, and seed-command proof.
- Changes applied:
  - Added the `MissingWriter`, `WriterFailed`, `Unavailable`, `Stale`, `Empty`, and `Populated` vocabulary to the Operational Index Truth Contract.
  - Tightened AC1 through AC7 with projection key filtering, idempotency, Issue #17 retest boundary, type-catalog duplicate discovery, storage ordering and byte-size non-goals, writer placement, backward-compatible API fields, deterministic ETag conflict tests, source-state tests, and explicit manual evidence artifacts.
  - Added task requirements for writer-boundary recording, retry diagnostics, and dated baseline-vs-post-change evidence notes.
  - Updated Verification Status and Change Log with this dated party-mode review.
- Findings deferred:
  - Exact writer deployment form remains a dev/architecture decision within the allowed EventStore-owned boundaries: EventStore server project, dedicated EventStore-owned indexing service, or apphost-wired background service.
  - Whether failed-writer state appears as a shared data-quality metadata shape or endpoint-specific status remains a backward-compatible API design decision.
  - Whether storage hot-stream ordering is exact or approximate recent activity remains a Dev Agent Record decision, provided it is deterministic or honestly labeled.
  - Any broader consistency algorithm fix after the Issue #17 retest remains out of scope and must be deferred separately with check id, payload summary, and suspected cause.
  - Any byte-size or growth-rate estimation remains deferred unless an authoritative backend-agnostic source already exists.
- Final recommendation: ready-for-dev
