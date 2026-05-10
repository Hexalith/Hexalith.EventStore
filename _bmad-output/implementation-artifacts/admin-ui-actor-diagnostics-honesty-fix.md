# Story: admin-ui-actor-diagnostics-honesty-fix

Status: review

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

### Inventory Semantics

- `TotalKnownTypes` means the bounded set of configured EventStore actor types
  (`AggregateActor`, `ProjectionActor`, and `ETagActor`) considered by this
  story. It is not active actor inventory.
- `ObservedActorIds` or equivalent means actor ids returned by an approved
  source, entered by the operator, seeded by a test fixture, or captured in
  manual evidence. It is not a system-wide total unless the payload explicitly
  marks the source complete.
- `TotalActors` or `Total Active Actors` must not be displayed unless the
  backend marks inventory as complete from an authoritative source. When any
  known actor type is missing, sampled, unavailable, or source-limited, the UI
  must use wording such as `Known Active Actors`, `Observed Actor Instances`,
  `Known actor types checked`, or `Active actor data unavailable`.
- API/DTO changes should carry provenance and completeness explicitly with
  fields such as `ownerAppId`, `inventorySource`, `isComplete`,
  `lookupStatus`, `message`, and `observedAt`, or a locally consistent
  equivalent. Do not infer completeness from non-empty lists, HTTP success, or
  the absence of an exception.
- Redis/keyspace scanning must not be used as a production inventory source in
  this story. It is acceptable only for local/manual evidence capture or behind
  a separately approved, named configuration setting plus documented
  architecture decision.

### Advanced Elicitation Hardening

The 2026-05-07 advanced elicitation pass tightened this story around evidence
precedence and implementation traps:

- Classify actor diagnostics from explicit evidence tuples before rendering:
  actor type, actor id, owner app id, inventory source, lookup source, state-key
  family, failure category, observed time, and completeness. UI labels must not
  infer exactness from HTTP success, non-empty rows, or a cached previous value.
- When evidence conflicts, prefer the safer diagnostic state: `LookupUnavailable`
  over `NotFound`, `Partial` over exact totals, and manual Redis evidence over
  nothing only for evidence capture, never for production completeness claims.
- Treat `AdminServerOptions.EventStoreAppId` as a configurable contract, not
  only a default. Tests must prove the default `eventstore` path and an explicit
  non-default owner app id both compose the owner-sidecar key correctly.
- Inspect requests must be stale-result safe. If the operator changes actor
  type or actor id while a lookup is in flight, a late response must not replace
  the current form state or show a false not-found/unavailable banner for the
  new input.
- Live/manual evidence may include sample tenant/domain/aggregate identifiers
  and Redis key names, but must redact or omit raw customer state, event payload
  values, bearer tokens, connection strings, and secrets.
- If DAPR changes or hides the internal Redis actor key convention, this story
  should fail closed with `LookupUnavailable` plus a deferred architecture note;
  it must not compensate by adding broad Redis keyspace scans to production
  code.

## Acceptance Criteria

1. **Actor counts are source-aware and never silently partial.**
   - Given the EventStore sidecar metadata returns active counts for only a subset of actor types
   - When `/api/v1/admin/dapr/actors` builds `DaprActorRuntimeInfo`
   - Then the response exposes whether each actor type count is exact, unavailable, or source-limited.
   - And `TotalActiveActors` is not presented as authoritative when any known/registered actor type has an unknown or unavailable count.
   - And the UI label for the summary card is one of `Total Active Actors`, `Known Active Actors`, or `Active actor data unavailable` according to the payload evidence.
   - And the UI does not use the words `total` or `all actors` for partial,
     sampled, observed, source-limited, or incomplete actor data.
   - And the API payload includes count provenance/completeness fields so
     clients can tell exact inventory from observed or unavailable evidence.

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
   - And at least one regression test would fail if the Admin UI/server app id
     were used instead of the owner app id `eventstore`.

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
   - And `NotFound` is produced only when the owner app/type/id lookup
     completed definitively and no known state key exists.
   - And `LookupUnavailable` is produced for sidecar, app-id, network,
     authorization, timeout, malformed response, or inconclusive key-format
     failures.
   - And the UI renders these as different text-visible banners; color alone is
     not sufficient.

6. **No new backend-specific inventory promise is introduced without a decision.**
   - This story may continue using DAPR metadata plus state-store lookup for inspection, but it must not pretend DAPR exposes a public active-actor listing API if it does not.
   - If exact active actor inventory is required beyond metadata-provided counts, record a deferred architecture decision for an admin-maintained actor activity index such as `admin:active-actors:{actor-type}`.
   - Do not implement the new index in this story unless Architect review explicitly approves it in the Dev Agent Record before coding starts.
   - If the story keeps count evidence partial, visible copy and tests must make that limitation explicit.
   - Do not add Redis/keyspace enumeration to production/shared-environment
     code as a shortcut for complete inventory unless a named configuration
     setting and explicit architecture decision are added outside this story's
     default path.

7. **Automated and live validation pin the diagnostic contract.**
   - Server tests cover local metadata partial counts, remote metadata Available/Unavailable status, known actor type fallback, owner-app-id key composition, and lookup-unavailable classification.
   - UI tests cover partial/unavailable count labels, known type rows with unavailable counts, successful found-state rendering, not-found rendering, and lookup-unavailable rendering.
   - API/client tests cover DTO shape or equivalent typed result mapping,
     `404`/not-found mapping, timeout/network failure mapping,
     malformed/unavailable responses, and successful partial responses.
   - Tests include negative assertions that partial responses are not labeled
     as total inventory and that production inventory does not enumerate Redis
     unless an explicit approved option exists.
   - Live Aspire evidence captures:
     - Redis key scan for the seeded AggregateActor id;
     - `/api/v1/admin/dapr/actors` payload;
     - `/api/v1/admin/dapr/actors/AggregateActor/state?id=tenant-a%3Acounter%3Acounter-1` payload;
     - `/dapr/actors` UI observation or screenshot showing the inspected actor state.
   - Tests cover stale inspect responses so a late result for a previous
     actor type/id cannot overwrite the current input's status banner or state
     rows.
   - Evidence artifacts redact raw state values, event payload data, secrets,
     bearer tokens, and connection strings while preserving key names and
     status/provenance fields needed to prove the diagnostic contract.
   - Unit/API/UI tests are the normal PR gate. Live Aspire/DAPR evidence is
     manual or integration evidence and must distinguish "endpoint reachable"
     from "inventory complete".

## Tasks / Subtasks

- [x] **ST0 - Baseline the live defect and current seams.** (AC: 1, 3, 4, 6)
  - [x] Re-read issue #10 in the manual-test evidence and this story before editing.
  - [x] Inspect `DaprInfrastructureQueryService.GetActorRuntimeInfoAsync`, `GetActorInstanceStateAsync`, `KnownActorTypes`, `AdminDaprController`, `AdminActorApiClient`, and `DaprActors.razor`.
  - [x] Confirm current `AdminServerOptions.EventStoreAppId` default and AppHost wiring for the actor-hosting service.
  - [x] Capture the current live failure if Aspire is available; if blocked, record the exact runtime blocker before relying on unit-level evidence.

- [x] **ST1 - Make actor count semantics honest.** (AC: 1, 2, 6)
  - [x] Extend or adapt actor runtime DTOs to carry count source/status without breaking existing JSON consumers unnecessarily.
  - [x] Add explicit provenance/completeness fields or an equivalent typed
        result contract for source, completeness, lookup status, owner app id,
        message, and observation time.
  - [x] Treat `-1`, missing actor types, failed remote metadata, or mixed local/remote sources as unavailable/partial instead of exact totals.
  - [x] Keep known actor types visible even when live metadata omits their counts.
  - [x] Add a concise deferred decision if exact inventory requires a new admin-maintained actor index.

- [x] **ST2 - Harden owner-app-id actor state lookup.** (AC: 3, 4, 5)
  - [x] Verify `ComposeActorStateKey` uses `AdminServerOptions.EventStoreAppId`, defaulting to `eventstore`.
  - [x] Add focused tests for AggregateActor metadata lookup with `tenant-a:counter:counter-1`.
  - [x] Add a regression test that fails if lookup composes keys with the Admin
        app id instead of `eventstore`.
  - [x] Add a configurable-owner regression proving a non-default
        `EventStoreAppId` composes keys against the owner app id rather than a
        hardcoded value.
  - [x] Ensure state-store failures classify as lookup-unavailable, not not-found.
  - [x] Preserve the DAPR internal-key warning and migration path to an EventStore-owned read proxy if DAPR changes the convention.

- [x] **ST3 - Update UI copy and state handling.** (AC: 1, 2, 5)
  - [x] Replace ambiguous `N/A` rendering with explicit `unavailable` or equivalent visible copy.
  - [x] Make the summary card title/value reflect exact vs partial count status.
  - [x] Add separate issue banners for actor not found and lookup unavailable.
  - [x] Ensure degraded states are text-visible and accessible, not conveyed by
        color alone.
  - [x] Guard inspect result rendering so late responses for an older
        actor type/id cannot replace the currently selected actor's banner,
        rows, or loading state.
  - [x] Follow the existing localization/resource pattern if this page has one;
        otherwise record localization as deferred rather than inventing a new
        resource scheme in this story.
  - [x] Keep the lookup form, deep links, refresh button, and existing dynamic-key-family rendering stable.

- [x] **ST4 - Add targeted tests.** (AC: 1, 2, 3, 5, 7)
  - [x] Extend `DaprActorQueryServiceTests` for success, count status,
        known-type fallback, owner-app-id key composition, malformed response,
        timeout/unavailable behavior, definitive not-found, and
        lookup-unavailable behavior.
  - [x] Extend `DaprActorsPageTests` for unavailable/partial labels and differentiated failure states.
  - [x] Add UI tests for stale inspect-result discard after actor type/id
        changes during an in-flight lookup.
  - [x] Extend `AdminActorApiClientTests` or controller tests for API error
        differentiation, `404` mapping, timeout/network failures, and partial
        payloads.
  - [x] Keep tests focused; do not add broad live-DAPR tests unless the unit seam cannot prove the contract.

- [x] **ST5 - Capture manual/live evidence and bookkeeping.** (AC: 4, 7)
  - [x] Run the project-standard Aspire dev mode with `EnableKeycloak=false` if the environment allows it.
  - [x] Seed `tenant-a/counter/counter-1` through the canonical sample flow.
  - [x] Save sanitized evidence under `_bmad-output/test-artifacts/admin-ui-actor-diagnostics-honesty-fix/`.
  - [x] Record a short redaction note in the evidence folder describing which
        fields were preserved and which raw state/payload/secret fields were
        omitted.
  - [x] Update this story's Dev Agent Record, File List, Verification Status, Change Log, and any narrowly required deferred-work entry.
  - [x] Move sprint status to `review` only after implementation and evidence are complete.

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
- The 2026-05-07 party-mode review recommended tightening inventory
  semantics before development. Treat exact actor inventory as unavailable
  unless an authoritative source explicitly proves completeness; observed
  counts and known-type counts are bounded diagnostics, not global totals.

Architecture and product guardrails:

- Admin tooling follows ADR-P4: Admin.Server backs Web UI, CLI, and MCP; direct admin reads may use DAPR state store, while command-pipeline writes stay delegated to EventStore.
- NFR44 requires admin data access to remain DAPR-backend-agnostic where practical. Direct Redis key scanning is allowed only as manual evidence or a carefully bounded dev/test diagnostic, not as a production feature unless explicitly decided.
- FR75 and Epic 19 require operational health/DAPR diagnostics to be truthful. Unknown evidence must not render as zero, exact, or healthy.
- Keep EventStore actor data tenant-safe. Evidence may include tenant/domain/aggregate identifiers from the canonical sample, but do not log event payload data, secrets, bearer tokens, or raw customer state.
- Prefer a testable service/client seam around DAPR actor lookup so server,
  controller, client, and Razor tests do not require a live sidecar for the
  normal PR gate.

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

Codex GPT-5.

### Debug Log References

- `aspire run --detach --non-interactive --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json` baseline: Keycloak unhealthy with default settings; EventStore/Admin resources waited behind Keycloak.
- `EnableKeycloak=false aspire run --detach --non-interactive --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json`: live evidence run with EventStore, Admin Server, Admin UI, Dapr sidecars, sample, tenants, statestore, and pubsub healthy.
- `dotnet test tests\Hexalith.EventStore.Admin.Server.Tests\Hexalith.EventStore.Admin.Server.Tests.csproj --no-restore --filter "FullyQualifiedName~DaprActorQueryServiceTests|FullyQualifiedName~AdminDaprControllerActorTests"`: passed 32/32.
- `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests\Hexalith.EventStore.Admin.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~AdminActorApiClientTests|FullyQualifiedName~DaprActorsPageTests"`: passed 18/18.
- `dotnet test tests\Hexalith.EventStore.Admin.Abstractions.Tests\Hexalith.EventStore.Admin.Abstractions.Tests.csproj --no-restore --filter "FullyQualifiedName~DaprActor"`: passed 33/33.
- Full no-build sweeps: Admin.Server.Tests 585 passed / 18 skipped; Admin.UI.Tests 744 passed; Admin.Abstractions.Tests 404 passed.
- Deferred-work checker: `.\scripts\check-deferred-work.ps1 _bmad-output\implementation-artifacts\deferred-work.md --legacy-advisory` is not supported by this wrapper; without the flag it exits 1 on historical legacy-advisory bullets unrelated to the new canonical entry.
- Live UI observation and sanitized API/Redis evidence: `_bmad-output/test-artifacts/admin-ui-actor-diagnostics-honesty-fix/`.

### Completion Notes List

- Story created and marked ready-for-dev by the BMAD pre-dev hardening automation.
- 2026-05-07 pre-dev party-mode review applied story clarifications for
  inventory completeness/provenance, owner app-id proof, NotFound vs
  LookupUnavailable taxonomy, Redis production-inventory guardrails,
  operator-facing copy, accessibility/localization, and evidence boundaries.
- 2026-05-07 pre-dev advanced elicitation applied story clarifications for
  evidence precedence, configurable owner app-id proof, stale inspect result
  handling, redaction boundaries, and fail-closed DAPR key-convention drift.
- No `project-context.md` file was present in the repository at story creation.
- Added explicit actor count status, lookup status, provenance, completeness, owner app id, lookup source, and observation fields across actor diagnostics DTOs.
- Runtime actor inventory now reads remote EventStore sidecar metadata first, keeps all known EventStore actor types visible, classifies missing counts as unavailable/partial, and avoids presenting partial data as an authoritative total.
- Actor inspection now uses the configured owner sidecar actor-state API when available; the internal state-store key convention remains tested as a fallback and fails closed as `LookupUnavailable` when Dapr rejects illegal `||` keys.
- Admin UI now renders exact/source-limited/unavailable count states, separates not-found from lookup-unavailable banners, replaces `N/A` with explicit copy, and discards stale inspect responses.
- Added canonical deferred-work entry for an admin-maintained actor activity index if exact active actor inventory becomes a requirement.

### File List

- `_bmad-output/implementation-artifacts/admin-ui-actor-diagnostics-honesty-fix.md`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/test-artifacts/admin-ui-actor-diagnostics-honesty-fix/actors-runtime.json`
- `_bmad-output/test-artifacts/admin-ui-actor-diagnostics-honesty-fix/aggregate-state.json`
- `_bmad-output/test-artifacts/admin-ui-actor-diagnostics-honesty-fix/admin-ui-actor-diagnostics-honesty-fix.png`
- `_bmad-output/test-artifacts/admin-ui-actor-diagnostics-honesty-fix/redaction-note.md`
- `_bmad-output/test-artifacts/admin-ui-actor-diagnostics-honesty-fix/redis-actor-keys.txt`
- `_bmad-output/test-artifacts/admin-ui-actor-diagnostics-honesty-fix/redis-flush-result.txt`
- `_bmad-output/test-artifacts/admin-ui-actor-diagnostics-honesty-fix/seed-command-statuses.json`
- `_bmad-output/test-artifacts/admin-ui-actor-diagnostics-honesty-fix/ui-observation.md`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprActorCountStatus.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprActorInstanceState.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprActorLookupStatus.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprActorRuntimeInfo.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprActorTypeInfo.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprActors.razor`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminActorApiClient.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprActorQueryServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DaprActorsPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminActorApiClientTests.cs`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Preflight passed before story creation.
- Party-mode review completed on 2026-05-07 and story hardened before
  development; recommendation remains ready-for-dev after applying low-risk
  clarifications.
- Advanced elicitation completed on 2026-05-07 and story hardened before
  development; recommendation remains ready-for-dev after applying low-risk
  clarifications.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, or submodules.
- Red phase: focused `DaprActorQueryServiceTests` failed before implementation on missing lookup/count status fields, proving the new contract was test-driven.
- Focused server/controller actor tests passed 32/32 after implementation.
- Focused UI actor page/client tests passed 18/18 after implementation.
- Focused actor DTO serialization tests passed 33/33 after implementation.
- Full Admin.Server.Tests passed 585/585 with 18 skipped on 2026-05-10.
- Full Admin.UI.Tests passed 744/744 on 2026-05-10.
- Full Admin.Abstractions.Tests passed 404/404 on 2026-05-10.
- Live Aspire evidence with `EnableKeycloak=false` seeded `tenant-a/counter/counter-1`, captured 8 Redis actor keys, runtime actor metadata, sanitized aggregate state lookup with `lookupStatus=Available`, and a UI screenshot showing found state rows without not-found or lookup-unavailable banners.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-10 | 0.4 | Implemented source-aware actor diagnostics, owner-sidecar actor state inspection, UI honesty states, targeted tests, live Aspire evidence, and deferred exact-inventory note. | Codex |
| 2026-05-07 | 0.3 | Advanced elicitation completed and story hardened for evidence precedence, owner app-id configurability, stale inspect results, redaction, and DAPR key-convention drift. | Codex automation |
| 2026-05-07 | 0.2 | Party-mode review completed and story hardened for actor inventory semantics, lookup taxonomy, owner app-id proof, Redis guardrails, UI copy, accessibility, and evidence boundaries. | Codex automation |
| 2026-05-07 | 0.1 | Created ready-for-dev story for Admin UI actor diagnostics honesty and canonical AggregateActor inspection. | Codex automation |

## Party-Mode Review

- ISO date and time: 2026-05-07T21:35:46+02:00
- Selected story key: `admin-ui-actor-diagnostics-honesty-fix`
- Command / skill invocation used:
  `/bmad-party-mode admin-ui-actor-diagnostics-honesty-fix; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior
  Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige
  (Technical Writer)
- Findings summary:
  - Actor inventory semantics were still too easy to implement as another
    misleading total; the story now requires explicit provenance and
    completeness.
  - `NotFound` and `LookupUnavailable` needed first-class API/UI mapping rather
    than inference from empty lists, exceptions, or generic 404 handling.
  - Owner app id `eventstore` needed a regression proof so dev cannot
    accidentally inspect the Admin app/sidecar identity.
  - Redis/keyspace enumeration needed a production guardrail to keep manual
    evidence from becoming a default inventory feature.
  - Tests and live evidence needed sharper negative assertions and CI/local
    boundaries.
  - Operator copy needed constraints so partial, sampled, or unavailable data is
    visible and accessible without implying full inventory.
- Changes applied:
  - Added an `Inventory Semantics` section defining bounded known types,
    observed actor ids, total-label restrictions, provenance fields, and Redis
    scanning limits.
  - Expanded AC1, AC3, AC5, AC6, and AC7 with completeness/provenance,
    owner-app-id regression, lookup taxonomy, production Redis guardrail,
    API/client mapping, negative-label tests, and live-evidence boundaries.
  - Tightened tasks for DTO/result shape, DAPR lookup regression tests,
    accessible degraded-state copy, localization handling, and focused
    unit/API/UI test cases.
  - Added Developer Notes and Dev Agent Record entries capturing the party-mode
    hardening decision before implementation.
- Findings deferred:
  - Production actor placement visualizer.
  - Operational actor index population or Redis-backed complete inventory
    design.
  - Broader DAPR health truthfulness outside `/dapr/actors`.
  - Operator role dialog behavior.
  - Snapshot/backup upstream endpoint work.
- Final recommendation: `ready-for-dev`

## Advanced Elicitation

- ISO date and time: 2026-05-07T23:03:37+02:00
- Selected story key: `admin-ui-actor-diagnostics-honesty-fix`
- Command / skill invocation used:
  `/bmad-advanced-elicitation admin-ui-actor-diagnostics-honesty-fix`
- Batch 1 method names:
  - Self-Consistency Validation
  - Red Team vs Blue Team
  - Architecture Decision Records
  - Security Audit Personas
  - Failure Mode Analysis
- Reshuffled Batch 2 method names:
  - Chaos Monkey Scenarios
  - Occam's Razor Application
  - First Principles Analysis
  - 5 Whys Deep Dive
  - Lessons Learned Extraction
- Findings summary:
  - The story needed an explicit evidence-precedence rule so dev work cannot
    turn partial metadata, cached rows, or manual Redis observations into exact
    production inventory claims.
  - Owner app id behavior needed both default and configurable proofs, because
    hardcoding `eventstore` would pass the current sample while violating the
    intended options contract.
  - The inspect form needed stale-result protection to avoid late async results
    presenting an old actor lookup as the current actor's state.
  - Manual evidence needed redaction boundaries that preserve diagnostic key
    proof without leaking raw state, payloads, tokens, or secrets.
  - DAPR key-convention drift needed an explicit fail-closed outcome rather
    than an implicit production Redis scan fallback.
- Changes applied:
  - Added `Advanced Elicitation Hardening` with evidence tuple, conflict
    precedence, configurable owner app-id, stale-result, redaction, and
    fail-closed key-convention rules.
  - Expanded AC7 with stale inspect-response tests and evidence redaction
    requirements.
  - Tightened ST2, ST3, ST4, and ST5 with configurable-owner regression,
    stale-result rendering guard, UI test coverage, and evidence redaction
    note requirements.
  - Updated Dev Agent Record, Verification Status, and Change Log with the
    advanced-elicitation result.
- Findings deferred:
  - A production actor activity index remains outside this story unless
    separately approved by architecture.
  - Broad Redis/keyspace enumeration remains limited to manual/local evidence
    capture, not default production inventory.
  - Broader localization infrastructure remains deferred unless the page
    already uses an established resource pattern.
- Final recommendation: `ready-for-dev`
