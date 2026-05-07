# Story: admin-ui-actor-diagnostics-honesty-fix

Status: ready-for-dev

Context created: 2026-05-07
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-admin-ui-manual-test-suite-issues.md`
Triggering evidence: `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues-2026-05-07.md`
Scope: Issue #10 only.

## Story

As an EventStore operator using the Admin UI,
I want the `/dapr/actors` page to report actor inventory and inspection limits honestly and to inspect known active EventStore actors using the owning sidecar/app id,
so that actor diagnostics help incident investigation instead of showing partial counts as totals or real actors as not found.

## Issue Traceability

| Issue | Failing symptom | Covered by | Required evidence |
| --- | --- | --- | --- |
| #10A | `/dapr/actors` shows `Total Active Actors = 1` while Redis contains at least five active actor instances across AggregateActor, ProjectionActor, and ETagActor. | AC1, AC2, AC6, AC7 | Runtime-info tests for partial/unknown counts; UI tests proving unavailable/partial labels; live Redis vs Admin UI evidence. |
| #10B | Inspecting `AggregateActor` id `tenant-a:counter:counter-1`, present in Redis under `eventstore||AggregateActor||tenant-a:counter:counter-1||...`, returns "Actor instance not found". | AC3, AC4, AC5, AC7 | State-key lookup tests using `eventstore` owner app id; live canonical seed inspection evidence. |
| #10C | The page has a correct graceful empty state for truly missing actor ids, but it uses the same not-found path for real actors when lookup source/key composition is wrong. | AC4, AC5, AC6 | Differentiated not-found vs lookup-unavailable tests and UI copy. |

## Actor Diagnostics Truth Contract

The actor page must distinguish these states in API payloads, UI labels, tests, and evidence:

| State | Meaning | UI rule |
| --- | --- | --- |
| `Available` | The EventStore sidecar or a supported actor index returned current actor metadata or state evidence. | Display exact values and source. |
| `Unavailable` | The actor count or state source cannot be queried through the current DAPR/Admin path. | Show `unavailable`; do not contribute it to exact totals. |
| `Partial` | Some actor types returned counts but at least one registered/known actor type has unknown count. | Label totals as partial or avoid a total; never present the sum as an authoritative total. |
| `NotFound` | The lookup path was available and no known state keys existed for the requested actor id. | Show an honest not-found banner and keep the inspected id visible. |
| `LookupUnavailable` | State-store read, remote metadata, owner app id, or key-format evidence was unavailable or inconclusive. | Show an issue banner explaining the unavailable source; do not claim the actor is inactive. |

Avoid ambiguous `N/A` when the operator needs to know whether the value is unknown, unavailable, not configured, or intentionally not countable.

## Acceptance Criteria

1. **Actor counts are source-aware and never silently partial.**
   - Given the EventStore sidecar metadata returns active counts for only a subset of actor types
   - When `/api/v1/admin/dapr/actors` builds `DaprActorRuntimeInfo`
   - Then the response exposes whether each actor type count is exact, unavailable, or source-limited.
   - And `TotalActiveActors` is not presented as authoritative when any known/registered actor type has an unknown or unavailable count.
   - And the UI label for the summary card is one of `Total Active Actors`, `Known Active Actors`, or `Active actor data unavailable` according to the payload evidence.

2. **Known EventStore actor types stay visible even when counts are unavailable.**
   - Given `AggregateActor`, `ProjectionActor`, and `ETagActor` are the known EventStore actor types
   - When remote metadata omits a known type or returns an unknown count
   - Then the page still shows the known type with description and actor-id format.
   - And the Active Instances cell renders `unavailable`, not `N/A`, `0`, or a blank cell.
   - And unknown actor types from live metadata are still shown with a warning log so the registry can be updated.

3. **Actor state inspection uses the owner app id and DAPR Redis actor key convention.**
   - Given EventStore actors are hosted by the `eventstore` app id in Aspire
   - When Admin.Server reads actor state for `AggregateActor`, `ProjectionActor`, or `ETagActor`
   - Then it composes state-store keys as `{EventStoreAppId}||{actorType}||{actorId}||{stateKey}` using `AdminServerOptions.EventStoreAppId`.
   - And default `EventStoreAppId` remains `eventstore`; do not regress to `eventstore-admin` or legacy `commandapi` assumptions.
   - And tests pin colon-delimited aggregate ids such as `tenant-a:counter:counter-1`.

4. **Known active AggregateActor inspection succeeds for canonical seed evidence.**
   - Given Aspire runs in project-standard dev mode, Redis is flushed, and the canonical sample flow creates `tenant-a/counter/counter-1`
   - And Redis contains `eventstore||AggregateActor||tenant-a:counter:counter-1||tenant-a:counter:counter-1:metadata` plus event keys
   - When the operator selects `AggregateActor`, enters `tenant-a:counter:counter-1`, and clicks Inspect
   - Then the page displays at least the metadata state entry as found.
   - And the state viewer does not show the global "Actor instance not found" banner.
   - And dynamic key families such as `{actorId}:events:{N}` remain labelled as dynamic families unless a bounded enumeration path is deliberately implemented.

5. **Not-found and lookup-unavailable states are different.**
   - Given a truly missing actor id is inspected and the lookup path is available
   - Then the page shows `Actor instance not found` with copy that the actor may be inactive or the id may be incorrect.
   - Given the state-store read path fails, times out, returns unauthorized/unavailable, or cannot verify the owner app id/key convention
   - Then the page shows lookup-unavailable copy and does not claim the actor is inactive.
   - And API error handling preserves status/category in a safe ProblemDetails or typed result path where practical.

6. **No new backend-specific inventory promise is introduced without a decision.**
   - This story may continue using DAPR metadata plus state-store lookup for inspection, but it must not pretend DAPR exposes a public active-actor listing API if it does not.
   - If exact active actor inventory is required beyond metadata-provided counts, record a deferred architecture decision for an admin-maintained actor activity index such as `admin:active-actors:{actor-type}`.
   - Do not implement the new index in this story unless Architect review explicitly approves it in the Dev Agent Record before coding starts.
   - If the story keeps count evidence partial, visible copy and tests must make that limitation explicit.

7. **Automated and live validation pin the diagnostic contract.**
   - Server tests cover local metadata partial counts, remote metadata Available/Unavailable status, known actor type fallback, owner-app-id key composition, and lookup-unavailable classification.
   - UI tests cover partial/unavailable count labels, known type rows with unavailable counts, successful found-state rendering, not-found rendering, and lookup-unavailable rendering.
   - Live Aspire evidence captures:
     - Redis key scan for the seeded AggregateActor id;
     - `/api/v1/admin/dapr/actors` payload;
     - `/api/v1/admin/dapr/actors/AggregateActor/state?id=tenant-a%3Acounter%3Acounter-1` payload;
     - `/dapr/actors` UI observation or screenshot showing the inspected actor state.

## Tasks / Subtasks

- [ ] **ST0 - Baseline the live defect and current seams.** (AC: 1, 3, 4, 6)
  - [ ] Re-read issue #10 in the manual-test evidence and this story before editing.
  - [ ] Inspect `DaprInfrastructureQueryService.GetActorRuntimeInfoAsync`, `GetActorInstanceStateAsync`, `KnownActorTypes`, `AdminDaprController`, `AdminActorApiClient`, and `DaprActors.razor`.
  - [ ] Confirm current `AdminServerOptions.EventStoreAppId` default and AppHost wiring for the actor-hosting service.
  - [ ] Capture the current live failure if Aspire is available; if blocked, record the exact runtime blocker before relying on unit-level evidence.

- [ ] **ST1 - Make actor count semantics honest.** (AC: 1, 2, 6)
  - [ ] Extend or adapt actor runtime DTOs to carry count source/status without breaking existing JSON consumers unnecessarily.
  - [ ] Treat `-1`, missing actor types, failed remote metadata, or mixed local/remote sources as unavailable/partial instead of exact totals.
  - [ ] Keep known actor types visible even when live metadata omits their counts.
  - [ ] Add a concise deferred decision if exact inventory requires a new admin-maintained actor index.

- [ ] **ST2 - Harden owner-app-id actor state lookup.** (AC: 3, 4, 5)
  - [ ] Verify `ComposeActorStateKey` uses `AdminServerOptions.EventStoreAppId`, defaulting to `eventstore`.
  - [ ] Add focused tests for AggregateActor metadata lookup with `tenant-a:counter:counter-1`.
  - [ ] Ensure state-store failures classify as lookup-unavailable, not not-found.
  - [ ] Preserve the DAPR internal-key warning and migration path to an EventStore-owned read proxy if DAPR changes the convention.

- [ ] **ST3 - Update UI copy and state handling.** (AC: 1, 2, 5)
  - [ ] Replace ambiguous `N/A` rendering with explicit `unavailable` or equivalent visible copy.
  - [ ] Make the summary card title/value reflect exact vs partial count status.
  - [ ] Add separate issue banners for actor not found and lookup unavailable.
  - [ ] Keep the lookup form, deep links, refresh button, and existing dynamic-key-family rendering stable.

- [ ] **ST4 - Add targeted tests.** (AC: 1, 2, 3, 5, 7)
  - [ ] Extend `DaprActorQueryServiceTests` for count status, known-type fallback, owner-app-id key composition, and lookup-unavailable behavior.
  - [ ] Extend `DaprActorsPageTests` for unavailable/partial labels and differentiated failure states.
  - [ ] Extend `AdminActorApiClientTests` or controller tests if API error differentiation changes.
  - [ ] Keep tests focused; do not add broad live-DAPR tests unless the unit seam cannot prove the contract.

- [ ] **ST5 - Capture manual/live evidence and bookkeeping.** (AC: 4, 7)
  - [ ] Run the project-standard Aspire dev mode with `EnableKeycloak=false` if the environment allows it.
  - [ ] Seed `tenant-a/counter/counter-1` through the canonical sample flow.
  - [ ] Save sanitized evidence under `_bmad-output/test-artifacts/admin-ui-actor-diagnostics-honesty-fix/`.
  - [ ] Update this story's Dev Agent Record, File List, Verification Status, Change Log, and any narrowly required deferred-work entry.
  - [ ] Move sprint status to `review` only after implementation and evidence are complete.

## Developer Notes

Current code intelligence from story creation:

- `AdminDaprController` exposes `GET /api/v1/admin/dapr/actors` and `GET /api/v1/admin/dapr/actors/{actorType}/state?id={actorId}` under the read-only admin policy.
- `DaprInfrastructureQueryService.GetActorRuntimeInfoAsync` first reads local Admin sidecar metadata, then reads remote EventStore sidecar metadata only when local actor metadata is empty. It sums all `ActiveCount >= 0` into `TotalActiveActors`.
- `DaprInfrastructureQueryService.GetActorInstanceStateAsync` reads known state keys from the DAPR state store with a 5-second timeout and returns a `DaprActorInstanceState` even when every key is missing.
- `ComposeActorStateKey` currently uses `{appId}||{actorType}||{actorId}||{stateKey}` and passes `_options.EventStoreAppId`; this is the right shape to preserve, but it must be proven against live Redis evidence.
- `KnownActorTypes` currently knows `AggregateActor`, `ETagActor`, and `ProjectionActor`. Aggregate metadata resolves to `{actorId}:metadata`; event, idempotency, pipeline, and drain keys are dynamic families.
- `DaprActors.razor` currently renders unknown counts as `N/A`, calculates the total from the runtime DTO, and treats all-not-found state entries as `Actor instance not found`.
- DW2 live evidence saved `_bmad-output/test-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence/admin-dapr-actors-response.json` with `remoteMetadataStatus=Available`, one count per known actor type, and `totalActiveActors=3`. The 2026-05-07 manual session later found a richer seeded runtime where Redis had at least five actor keys but the page showed only one active actor.
- Story 19.2 already documented that DAPR actor placement is not publicly queryable and that the Redis key convention is internal. Preserve that honesty instead of broadening claims.

Architecture and product guardrails:

- Admin tooling follows ADR-P4: Admin.Server backs Web UI, CLI, and MCP; direct admin reads may use DAPR state store, while command-pipeline writes stay delegated to EventStore.
- NFR44 requires admin data access to remain DAPR-backend-agnostic where practical. Direct Redis key scanning is allowed only as manual evidence or a carefully bounded dev/test diagnostic, not as a production feature unless explicitly decided.
- FR75 and Epic 19 require operational health/DAPR diagnostics to be truthful. Unknown evidence must not render as zero, exact, or healthy.
- Keep EventStore actor data tenant-safe. Evidence may include tenant/domain/aggregate identifiers from the canonical sample, but do not log event payload data, secrets, bearer tokens, or raw customer state.

## Files Likely Touched

- `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprActorRuntimeInfo.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprActorTypeInfo.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprActorInstanceState.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/KnownActorTypes.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminDaprController.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprActors.razor`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminActorApiClient.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprActorQueryServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminDaprControllerActorTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DaprActorsPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminActorApiClientTests.cs`
- `_bmad-output/implementation-artifacts/deferred-work.md` only if exact actor inventory/index work is deferred.

## Out of Scope

- Issues #6, #7, and #8 health/DAPR truthfulness; they belong to `admin-ui-health-dapr-truthfulness-fix`.
- Issues #9 and #13 operator dialog/role-switch work; they belong to `admin-ui-operator-action-and-dev-role-testability-fix`.
- Issues #11, #12, #14, and #17 admin operational index population and consistency retest.
- Issue #15 snapshot, compaction, and backup upstream endpoint implementation.
- Issues #16 and #18 consistency subtitle and tenant-delete clarity polish.
- Building a production actor placement visualizer or claiming exact active actor inventory from DAPR placement.
- Scanning Redis keys in production code as the default inventory source without a documented architecture decision.
- Changing DAPR component YAML, access-control policy, actor runtime configuration, or AppHost topology unless a narrow wiring defect is proven and documented.
- Changing aggregate/event persistence, actor processing, projection cache behavior, or query cache actor semantics.

## References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-admin-ui-manual-test-suite-issues.md`
- `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues-2026-05-07.md`
- `_bmad-output/implementation-artifacts/19-2-dapr-actor-inspector.md`
- `_bmad-output/implementation-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence.md`
- `_bmad-output/test-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence/admin-dapr-actors-response.json`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminDaprController.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/KnownActorTypes.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprActors.razor`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminActorApiClient.cs`
- `src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprActorQueryServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DaprActorsPageTests.cs`

## Dev Agent Record

### Agent Model Used

TBD by dev agent.

### Debug Log References

TBD by dev agent.

### Completion Notes List

- Story created and marked ready-for-dev by the BMAD pre-dev hardening automation.
- No implementation work has been performed for this story.
- No `project-context.md` file was present in the repository at story creation.

### File List

TBD by dev agent.

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Preflight passed before story creation.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, or submodules.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-07 | 0.1 | Created ready-for-dev story for Admin UI actor diagnostics honesty and canonical AggregateActor inspection. | Codex automation |
