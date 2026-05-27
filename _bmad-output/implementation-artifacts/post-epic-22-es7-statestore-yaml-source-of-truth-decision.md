# Post-Epic 22 ES-7: State-store Config Source-of-Truth Decision

Status: done

Context created: 2026-05-27
Story key: `post-epic-22-es7-statestore-yaml-source-of-truth-decision`
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-27-eventstore-parties-review-residuals.md` (finding ES-7)
Epic: Post-Epic-22 EventStore<->Parties Review Residuals
Scope: Moderate (Developer + Architect note). Decide and enforce whether the checked-in DAPR state-store YAML or the Aspire `WithMetadata` component override is the authoritative source for local state-store metadata, then align docs, validation, and downstream handoff.

## Story

As an EventStore platform maintainer,
I want the local DAPR `statestore` component to have one explicit source of truth,
so that EventStore, deployment docs, tests, and downstream Parties validation do not disagree about the Redis host, actor-state flag, key-prefix behavior, or backend-swap contract.

## Background & Verified Residual

ES-7 is confirmed in current code and docs:

- `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs:69-84` creates the local state-store component with `AddDaprComponent("statestore", "state.redis")` and sets metadata in C#: `actorStateStore=true`, `redisHost=127.0.0.1:6379`, and `keyPrefix=none`.
- `src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml` also declares a Redis `statestore`, but it uses `{env:REDIS_HOST|127.0.0.1:6379}` and does not include `keyPrefix`.
- Documentation presents checked-in YAML as the deployment/config source. `docs/guides/dapr-component-reference.md` says Redis state store source is `src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml`, but the example is stale: it says `localhost:6379`, scopes only `eventstore`, and omits `eventstore-admin`.
- The PRD and epics require backend switching through DAPR component config only: FR43 and NFR29 say environment/backend changes must not require application code changes, recompilation, or redeployment.
- The source proposal says Parties deploy validation currently asserts the YAML. ES-7 must either make that assertion correct or document that the Aspire-generated metadata override is authoritative and align the validator contract afterward.

This is not a new epic, topology expansion, or product-scope change. It is a post-Epic-22 hardening row that prevents a future operator or downstream validator from trusting one file while the runtime uses another.

## Decision Contract

The first implementation act is a mini-ADR decision. The implementation must name one of these positions in the story's Dev Agent Record and in the architecture note, then make the selected position mechanically true:

| Decision | Required edits | Risk to check |
| --- | --- | --- |
| YAML authoritative | Remove or stop duplicating contradictory Aspire app model metadata; ensure checked-in `statestore.yaml` contains every runtime metadata value the local DAPR component needs; update docs/tests around YAML | Aspire-generated local component behavior must not reintroduce drift |
| Aspire app model metadata authoritative | Generate, remove, or clearly mark checked-in YAML as non-authoritative for `aspire run`; update docs/tests so they validate the Aspire-generated metadata contract rather than stale YAML assumptions | Checked-in YAML must not contradict runtime composition or mislead downstream validators |

Whichever path is selected, the non-authoritative source must be one of:

- removed if it is obsolete,
- generated or derived from the authoritative source,
- or validated against the authoritative source so drift fails a test/review gate.

Leaving both surfaces independently editable is not a valid outcome.

### Allowed Decisions

1. **YAML authoritative (preferred if practical).**
   - The checked-in DAPR component YAML files define the component metadata operators and validators should trust.
   - AppHost generation must not silently override semantic metadata with a different C# value.
   - Any required local-only metadata such as `keyPrefix=none` must be present in the authoritative local YAML or derived from it by a clearly tested path.

2. **Aspire `WithMetadata` authoritative.**
   - The C# AppHost component declaration is explicitly documented as the local Aspire-run source of truth.
   - Checked-in YAML is treated as a deploy/template artifact, not as the runtime source for `aspire run`.
   - Docs and validation must stop claiming that local Aspire runtime metadata is proved by reading `statestore.yaml` alone.

Do not leave the system in a "both are sources" state. If both locations remain, one must be marked as generated/template/mirror and the other as authoritative, with a regression test or validation rule that catches drift.

### Decision Rubric & Minimum Evidence

Default to **YAML authoritative** unless ST0 proves that local Aspire runtime composition cannot preserve Aspire dashboard/resource visibility, DAPR sidecar references, and component semantics without duplicating metadata in C#.

Before changing code, score the two allowed decisions against these criteria in the Dev Agent Record:

| Criterion | YAML authoritative | Aspire app model metadata authoritative |
| --- | --- | --- |
| FR43/NFR29 portability | Does changing DAPR component YAML remain enough for backend/environment changes? | Does the decision still avoid application redeploy/recompile for backend metadata changes? |
| Drift resistance | Is duplicate semantic metadata removed, derived, or guarded by a failing test? | Is checked-in YAML renamed, reworded, generated, removed, or guarded so it cannot mislead? |
| Aspire compatibility | Does the AppHost still expose useful resources/references and sidecar wiring? | Does AppHost metadata stay visible and stable without contradicting deployment docs? |
| Testability | Can structured tests prove actor state, host, scope, and key-prefix expectations? | Can tests prove docs/tests no longer treat YAML as Aspire-run truth by accident? |
| Documentation clarity | Can an operator identify the trusted source without reading C# comments? | Is the deploy/template versus Aspire-run distinction explicit enough for operators? |
| Parties validator compatibility | Can Parties keep asserting YAML? | Does Parties receive a concrete validator semantics update note? |

Minimum evidence for either decision:

- Structured YAML/component tests cover `metadata.name=statestore`, `spec.type=state.redis`, `actorStateStore=true`, scoped access, and `keyPrefix` if preserved.
- A doc cleanup grep or test covers stale `localhost:6379`, missing `eventstore-admin`, contradictory `statestore.yaml` source-of-truth language, and incorrect `EventStore:CommandStatus:StateStoreName` defaults.
- AppHost build evidence is recorded when AppHost code changes.
- Targeted DAPR component tests are run or their pre-existing build blocker is recorded.
- Aspire runtime evidence is attempted only when Docker/DAPR prerequisites are available; otherwise the exact blocker is recorded.

Red-team the final implementation before marking review-ready:

- Could an operator edit one surface and believe runtime changed when it did not?
- Could `keyPrefix=none` preservation or removal silently change Redis key layout for actors, command status, streams/admin activity, projections, snapshots, or tenant bootstrap?
- Could `eventstore-admin` lose required direct state-store access?
- Could sample/domain services accidentally gain state-store/pubsub scope or AppHost references?
- Could downstream Parties validation keep asserting the wrong artifact after ES-7?

## Must Preserve

- Component `metadata.name` remains `statestore`; application defaults use this store name.
- Local development state-store type remains `state.redis`.
- `actorStateStore` remains `"true"` so DAPR actors continue to activate.
- `eventstore`, `eventstore-admin`, and `tenants` remain the only checked-in local `statestore.yaml` scopes unless a separate architecture decision changes shared storage access.
- `sample`, `sample-blazor-ui`, and `eventstore-admin-ui` must not receive direct state-store/pubsub component references.
- Tenants may continue to receive EventStore's state store/pubsub references through AppHost because the current topology explicitly wires the Tenants service to shared EventStore infrastructure.
- Pub/sub wiring, DAPR access-control YAML, resiliency policy, command/query contracts, and public REST contracts remain unchanged.

## Acceptance Criteria

1. **Source-of-truth decision is explicit and durable.**
   - Given ES-7 is implemented
   - When a maintainer reads `_bmad-output/planning-artifacts/architecture.md` and the updated operational/deployment docs
   - Then they can identify the authoritative source for local DAPR `statestore` metadata without reading code comments in `HexalithEventStoreExtensions.cs`
   - And the selected source is named as either `YAML authoritative` or `Aspire app model metadata authoritative`
   - And the non-authoritative surface is removed, generated/derived, or validated against the authoritative source
   - And the Dev Agent Record states which path was chosen, why, and which alternative was rejected
   - And the note references FR43/NFR29 and the local Aspire/DAPR constraints that drove the decision.

2. **Runtime and declared component metadata no longer contradict each other.**
   - If YAML is authoritative, AppHost local DAPR resource creation must use metadata that matches `src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml`, including `actorStateStore`, `redisHost`, and `keyPrefix` if retained.
   - If `WithMetadata` is authoritative, `statestore.yaml`, docs, and tests must be renamed/reworded or guarded so they are not mistaken for the Aspire-run source of runtime truth.
   - In either path, `metadata.name` remains `statestore`; `spec.type` remains `state.redis` for local development; `actorStateStore` remains `"true"`; domain services still have zero direct state-store access.
   - `eventstore`, `eventstore-admin`, and `tenants` may continue to receive the component through AppHost references where required; `sample`, `sample-blazor-ui`, and `eventstore-admin-ui` must not gain state-store/pubsub access.

3. **`keyPrefix=none` is treated deliberately, not accidentally.**
   - Given current AppHost sets `keyPrefix=none`
   - When ES-7 lands
   - Then the final source of truth either includes `keyPrefix=none` with a documented reason, or omits it with a documented reason that accepts the DAPR default
   - And tests or review notes verify that the selected behavior does not unexpectedly change existing actor state, command status, stream/admin activity, projection checkpoint, snapshot policy, or tenant bootstrap key layout
   - And docs do not imply the opposite behavior
- And the implementation explains whether domain-service isolation depends on DAPR scopes/AppHost references rather than Redis key prefixing
   - And the story implementation does not change command status key builders, stream/event key builders, projection checkpoint keys, tenant bootstrap keys, or admin state readers unless the selected decision proves a necessary local compatibility fix.

4. **Deployment portability docs align with the chosen source.**
   - `docs/guides/dapr-component-reference.md` must stop showing stale local state-store metadata and scopes.
   - `docs/guides/configuration-reference.md` must keep the DAPR infrastructure guidance consistent with actual defaults; in particular, `EventStore:CommandStatus:StateStoreName` must not claim a default other than `statestore`.
   - Docker Compose guidance in `docs/guides/deployment-docker-compose.md` must say when `redisHost` is edited in copied YAML and must not imply that editing YAML changes AppHost-generated metadata unless the selected implementation truly makes that happen.
   - If the chosen source is `WithMetadata`, docs must explain the distinction between `aspire run` local component generation and deploy/publish component YAMLs.
   - Documentation cleanup must search at least for `localhost:6379`, `127.0.0.1:6379`, `REDIS_HOST`, `statestore.yaml`, `actorStateStore`, `keyPrefix`, `eventstore-admin`, and source-of-truth language.

5. **Validation catches future drift.**
   - Add or update focused tests in `tests/Hexalith.EventStore.Server.Tests/DaprComponents/` or an AppHost/Aspire-focused test location that already exists.
   - The tests must fail if the authoritative `statestore` metadata drifts from the runtime/deployment contract selected in AC 1.
   - Keep existing YAML parsing through `DaprYamlTestHelper`; do not add brittle string-only YAML assertions where structured parsing is already available.
   - Preserve existing `DaprComponentValidationTests` and `ProductionDaprComponentValidationTests` checks for component name, actor state-store flag, scopes, and production parity.
   - If the implementation chooses `WithMetadata` authoritative, include at least one test or assertion that proves docs/tests no longer treat `statestore.yaml` as the Aspire-run truth by mistake.
   - Include negative/static coverage that fails if stale local docs keep `localhost:6379` where the selected source requires the env fallback/default form, if `eventstore-admin` disappears from local state-store access, if sample/domain services gain direct state-store scope, or if component name changes from `statestore`.

6. **Parties-side follow-up is recorded without editing absent code.**
   - Given the source proposal says Parties `deploy/validate-deployment.ps1` asserts the YAML
   - When ES-7 completes in EventStore
   - Then the Dev Agent Record includes a dedicated downstream handoff note naming the final source-of-truth decision, files changed, whether Parties should keep asserting YAML or update to Aspire-generated metadata semantics, and any assumptions about scoped DAPR components
   - And if this repo has a follow-up backlog/deferred-work convention available in the touched context, the implementation either records a follow-up item there or explicitly says no local follow-up artifact exists
   - And no `Hexalith.Parties` code is edited from this repository unless the repo/path is explicitly present and in scope for the implementation session.

7. **Runtime behavior remains stable.**
   - EventStore still uses the state-store component named `statestore` for actor state, command status, command archive, stream/admin activity, snapshot policy storage, projection checkpoints, and admin storage/query services.
   - Pub/sub wiring, DAPR access-control YAML, resiliency policy, query routing, domain service resolver behavior, command processing, protected-data redaction, and public REST contracts are unchanged.
   - If AppHost code changes, the implementation records that an Aspire restart is required and attempts the documented `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` validation when Docker/DAPR prerequisites are available.

## Tasks / Subtasks

- [x] **ST0 - Reconfirm the active state-store topology.** (AC: 1, 2, 3, 7)
  - [x] Re-read `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs:69-110` and confirm how `stateStore` is created and referenced by EventStore/Admin.Server.
  - [x] Re-read `src/Hexalith.EventStore.AppHost/Program.cs:40-48`, `:122-148`, and `:156-167` to confirm which sidecars receive the state store and which intentionally do not.
  - [x] Re-read `src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml`, `pubsub.yaml`, and `resiliency.yaml`.
  - [x] Re-read production state-store YAMLs in `deploy/dapr/statestore-postgresql.yaml` and `deploy/dapr/statestore-cosmosdb.yaml`.
  - [x] Capture whether Docker/Aspire can run. This story creation baseline found AppHost build green, but `aspire run` failed because Docker was not running.

- [x] **ST1 - Choose and record the source-of-truth path.** (AC: 1, 2, 3)
  - [x] Add a mini-ADR style note to the story's Dev Agent Record before code changes: selected source, rejected source, required edits, risk to check.
  - [x] Complete the Decision Rubric & Minimum Evidence matrix for both allowed decisions before selecting the path.
  - [x] Prefer YAML authoritative if it can be implemented without weakening Aspire dashboard/resource visibility or sidecar reference behavior.
  - [x] If YAML authoritative, decide whether to load component YAMLs through `DaprSidecarOptions.ResourcesPaths`, mirror YAML metadata into `AddDaprComponent` with a guard, or use another existing CommunityToolkit-supported pattern.
  - [x] If `WithMetadata` authoritative, explicitly document why AppHost-generated local metadata is the runtime truth and mark checked-in YAML as deploy/template documentation for non-Aspire or publish flows.
  - [x] Ensure the non-authoritative surface is removed, generated/derived, or covered by a drift test.
  - [x] Record how the selected path satisfies FR43/NFR29 without reintroducing hidden application-code configuration for backend metadata.
  - [x] Record the rejected path and rationale in the Dev Agent Record.

- [x] **ST2 - Implement the selected source-of-truth enforcement.** (AC: 2, 3, 7)
  - [x] Keep component names stable: `statestore` and `pubsub`.
  - [x] Keep local state store type `state.redis`.
  - [x] Keep `actorStateStore=true`.
  - [x] Preserve or explicitly remove `keyPrefix=none` with evidence. If preserving, put it in the authoritative source and docs.
  - [x] Keep `eventstore-admin` access to the state store for admin reads/probes and admin indexes.
  - [x] Keep `sample`, `sample-blazor-ui`, and `eventstore-admin-ui` isolated from direct state-store/pubsub component references.
  - [x] Verify local checked-in state-store scopes remain exactly `eventstore`, `eventstore-admin`, and `tenants` unless a later architecture decision changes shared infrastructure access.
  - [x] Avoid introducing a YAML parser dependency into production/runtime code unless there is no existing safer pattern; if parsing is needed for tests, keep it in tests using existing YamlDotNet helpers.

- [x] **ST3 - Align docs and architecture notes.** (AC: 1, 4, 6)
  - [x] Update `_bmad-output/planning-artifacts/architecture.md` with a short "DAPR state-store source of truth" note near the AppHost/DAPR component topology section.
  - [x] Update `docs/guides/dapr-component-reference.md` local Redis state-store section so metadata, scopes, host value, and source-of-truth language match the implementation.
  - [x] Update `docs/guides/configuration-reference.md` so `EventStore:CommandStatus:StateStoreName` defaults to `statestore`, matching `CommandStatusConstants.DefaultStateStoreName`.
  - [x] Update `docs/guides/deployment-docker-compose.md` only where it explains copied YAML and `redisHost` rewriting; keep the Docker-specific warning if still accurate.
  - [x] Search docs for `localhost:6379`, `127.0.0.1:6379`, `REDIS_HOST`, `statestore.yaml`, `actorStateStore`, `keyPrefix`, and `eventstore-admin`; update only references that contradict the chosen source-of-truth decision.
  - [x] Add a downstream Parties handoff note to the Dev Agent Record. Do not invent a Parties file path if it is not present locally.

- [x] **ST4 - Add regression coverage.** (AC: 2, 3, 5, 7)
  - [x] Add/update tests in `tests/Hexalith.EventStore.Server.Tests/DaprComponents/DaprComponentValidationTests.cs` for local state-store metadata required by the selected decision.
  - [x] Add/update `ProductionDaprComponentValidationTests` only if production YAML parity expectations change.
  - [x] Include a `keyPrefix` assertion if `keyPrefix=none` is preserved as required runtime behavior.
  - [x] Include a negative/static assertion that sample/domain app IDs are not granted local state-store scope.
  - [x] Include a red-team regression check for hidden duplicated metadata, misleading source-of-truth docs, missing admin access, unintended sample/domain access, and wrong Parties validator semantics.
  - [x] Include a doc consistency assertion or a documented manual grep result for stale `localhost:6379`, missing `eventstore-admin`, and contradictory source-of-truth wording.
  - [x] Include doc/default consistency coverage if practical, or record why a manual grep/doc review is the better fit.
  - [x] Keep tests structured through YAML parsing helpers and Shouldly.

- [x] **ST5 - Validate and record evidence.** (AC: all)
  - [x] Run `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~DaprComponentValidationTests|FullyQualifiedName~ProductionDaprComponentValidationTests"`.
  - [x] If AppHost/Aspire code changes, run `dotnet build src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --no-restore` first, then `dotnet build Hexalith.EventStore.slnx --configuration Release` if focused validation is green and output locks allow it.
  - [x] If Docker is running, restart or run Aspire with `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`, then inspect resources for `eventstore`, `eventstore-admin`, `tenants`, `statestore`, and `pubsub`.
  - [x] If Docker is not running, record the exact block and keep validation to build/tests/docs.
  - [x] Separate required static/build/test evidence from optional/manual Aspire evidence in the Dev Agent Record.
  - [x] Update Dev Agent Record, File List, Verification Status, and Change Log before moving to `review`.

## Dev Notes

### Current State Of Files To Update

`src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs`

- Current behavior: creates a DAPR component resource with C# metadata:
  - `AddDaprComponent("statestore", "state.redis")`
  - `.WithMetadata("actorStateStore", "true")`
  - `.WithMetadata("redisHost", "127.0.0.1:6379")`
  - `.WithMetadata("keyPrefix", "none")`
- Required change: depends on the selected source-of-truth decision. Either make this derive from / match the authoritative YAML contract, or document and test this as the authoritative local Aspire-run contract.
- Must preserve: `statestore` resource identity, EventStore/Admin.Server references, `pubsub` wiring, fixed EventStore DAPR HTTP port behavior, run-vs-publish environment gating, and no direct sample state-store access.

`src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml`

- Current behavior: checked-in local Redis component with `actorStateStore=true`, env-substituted `redisHost`, and scopes for `eventstore` + `eventstore-admin` + `tenants`.
- Current drift: does not contain `keyPrefix=none` even though AppHost sets it at runtime; docs show a stale variant with `localhost:6379` and only `eventstore` scope.
- Required change: if YAML authoritative, update it to contain every required local metadata value and make AppHost/doc/test behavior follow it. If `WithMetadata` authoritative, label this file correctly as template/deploy input and guard against mistaken runtime-truth assertions.

`docs/guides/dapr-component-reference.md`

- Current behavior: presents `statestore.yaml` as source for local Redis state-store config.
- Current drift: example omits `eventstore-admin` scope and uses `localhost:6379` while current YAML defaults to `127.0.0.1:6379`.
- Required change: align with selected decision and actual current metadata.

`docs/guides/configuration-reference.md`

- Current behavior: `EventStore:CommandStatus:StateStoreName` table says default `"eventstore"`.
- Current code: `CommandStatusConstants.DefaultStateStoreName` is `"statestore"`.
- Required change: correct the documented default while touching this area so the state-store source-of-truth story does not leave a known state-store doc lie behind.

`tests/Hexalith.EventStore.Server.Tests/DaprComponents/DaprComponentValidationTests.cs`

- Current behavior: asserts local state-store YAML exists, has `actorStateStore=true`, scopes exactly `eventstore`, `eventstore-admin`, and `tenants`, and resiliency targets `statestore`.
- Required change: add the minimum drift-prevention tests for the selected decision. Good candidates: assert `redisHost`, `keyPrefix` if retained, and doc/default consistency if implemented in a testable way.

`tests/Hexalith.EventStore.Server.Tests/DaprComponents/ProductionDaprComponentValidationTests.cs`

- Current behavior: asserts production state-store YAMLs exist, have `actorStateStore=true`, scopes exactly `eventstore` and `eventstore-admin`, and `metadata.name=statestore`.
- Required change: only update if the source-of-truth decision changes production YAML expectations. Do not broaden production scope casually.

### Files To Read But Avoid Editing Unless Needed

`src/Hexalith.EventStore.AppHost/Program.cs`

- Confirms sidecar/component access boundaries. Tenants references EventStore state store/pubsub; sample uses an empty resources path; sample Blazor UI and admin UI do not receive state store/pubsub.
- Avoid editing unless the selected source-of-truth path requires AppHost resource-path changes.

`src/Hexalith.EventStore.Server/Commands/CommandStatusConstants.cs`

- Defines `DefaultStateStoreName = "statestore"`. Do not change it for ES-7.

`src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs`

- Defines projection checkpoint state store default `"statestore"`. Do not change it for ES-7.

`docs/guides/deployment-docker-compose.md`

- Contains Docker-specific copy/rewrite guidance for local DAPR component YAML. Edit only the source-of-truth wording and stale Redis host implications; do not rewrite the deployment guide wholesale.

### Known Doc Search Targets

Search docs and planning artifacts for these values before closing ES-7:

- `localhost:6379` - stale if the chosen local source uses `{env:REDIS_HOST|127.0.0.1:6379}` or `127.0.0.1:6379`.
- `127.0.0.1:6379` - should be explained as local default or AppHost-generated metadata, not production guidance.
- `REDIS_HOST` - should align with Docker Compose rewrite guidance and selected source-of-truth semantics.
- `statestore.yaml` - must not be described as Aspire-run runtime truth if Aspire app model metadata remains authoritative.
- `actorStateStore` - must stay present in the authoritative source and docs.
- `keyPrefix` - must appear in the decision/docs if preserved or explicitly omitted by decision.
- `eventstore-admin` - must appear in local state-store scopes where YAML remains authoritative for local direct admin reads/probes.

### Implementation Guardrails

- Do not "fix" the drift by duplicating magic strings in more places without a test that catches drift.
- Do not remove `eventstore-admin` from state-store scopes. Admin.Server uses direct DAPR state reads for health, admin indexes, storage, stream, snapshot, and tenant operational surfaces.
- Do not grant state-store/pubsub access to domain services or UI sidecars.
- Do not rename `statestore`; DAPR state APIs use the component `metadata.name` as the store name, and code defaults rely on `statestore`.
- Do not change production backend selection or introduce a new backend in this story.
- Do not change command status TTL behavior. TTL is application-level request metadata, not component-level state-store metadata.
- Do not introduce appsettings as the source for DAPR component backend connection details. FR43/NFR29 require backend swapping through DAPR component configuration.
- Do not edit downstream Parties validator code unless the code is present and explicitly in scope. Record the handoff instead.

### Previous Story Intelligence

- Story 8.6 established the backend-swap promise: changing DAPR component YAML should be enough for environment/backend changes, with component `metadata.name` stable as `statestore`/`pubsub`.
- Post-Epic-22 R22A1 removed a misleading AppHost DAPR config-store YAML because it was declared but not wired. ES-7 should learn from that: a checked-in DAPR file must either be active/authoritative or clearly marked as a template.
- ES-1 through ES-6 in the current cluster kept scope narrow and evidence-focused. ES-7 should not reopen query routing, result payload privacy, actor-not-found classification, or Contracts/package boundaries.

### Party-Mode Review Hardening

Party-mode review on 2026-05-27 reached consensus that the story is valid but needs tighter execution boundaries before implementation:

- Winston: the non-authoritative source must be removed, mechanically derived, or validated; `keyPrefix=none` needs an architectural reason; Parties handoff must name an artifact or explicit no-artifact decision.
- Amelia: ACs must lock `keyPrefix`, local scopes, source-of-truth selection, docs cleanup targets, and Docker-independent validation.
- Murat: configuration drift is medium-high risk despite low code complexity; add negative/static coverage for stale docs, wrong scopes, wrong component name, and contradictory metadata.
- Paige: make the decision fork a visible mini-ADR, elevate "Must Preserve" invariants, and separate already-known Aspire baseline from dev-agent validation requirements.

### Aspire Baseline

Before creating this story, the repo instruction to establish Aspire state was attempted on 2026-05-27:

- `aspire run --detach --non-interactive --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json` stopped a previous AppHost instance, then failed.
- `dotnet build src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --no-restore` succeeded with 0 warnings and 0 errors.
- Retrying `EnableKeycloak=false` Aspire run built successfully but AppHost exited because Docker was not running or not installed from the CLI's perspective.
- Implementation should rerun Aspire after any AppHost/resource-path change when Docker is available. If Docker is still unavailable, record the same blocker and rely on build/tests/docs validation.

### Latest Technical Information

- Official Aspire DAPR integration docs say the integration adds DAPR sidecars and wires in state store, pub/sub, and component resources. The same page's actor-state-store example configures `actorStateStore` in component YAML and references the components directory from `DaprSidecarOptions`. Source: <https://aspire.dev/integrations/frameworks/dapr/>
- Official DAPR Redis state-store docs list `redisHost` as required metadata and `actorStateStore` as optional metadata that marks the Redis state store for actors. Source: <https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-redis/>
- Official DAPR State API docs define `metadata.name` as the state-store name used in API routes, and note DAPR key schemes include app-id prefixes by default (`<App ID>||<state key>` for general state; actor keys include app ID, actor type, actor id, and state key). This is why any `keyPrefix=none` decision must be deliberate. Source: <https://docs.dapr.io/reference/api/state_api/>
- Local package source of truth is `Directory.Packages.props`: DAPR packages are `1.17.9`; Aspire hosting packages are `13.3.5`; `CommunityToolkit.Aspire.Hosting.Dapr` is `13.3.0-preview.1.260514-0647`; xUnit v3 is `3.2.2`; Shouldly is `4.3.0`; YamlDotNet is `18.0.0`.

### Project Context Reference

Apply `_bmad-output/project-context.md`:

- Aspire owns runtime composition; AppHost edits require an Aspire restart to take effect.
- Use official Aspire, Microsoft, DAPR, and NuGet docs for version-sensitive infrastructure.
- DAPR actor state must go through actor state/state-store boundaries; do not bypass actor isolation.
- Domain services should not receive direct state store/pubsub access.
- DAPR access control is deny-by-default in production and must be changed deliberately.
- Treat warnings as build-breaking.
- Run targeted test projects individually before broader validation.

### Review Findings

- [x] [Review][Patch] Tenants shared-state-store contract is inconsistent with YAML-authoritative scopes; Tenants access was confirmed as the intended contract, so the authoritative YAML/docs/tests/Parties handoff need to include `tenants` [src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml:43]
- [x] [Review][Patch] `LocalPath` broadens associated sidecar resource paths to the whole `DaprComponents` folder, so ES-7 can duplicate or change non-statestore resources while generated `pubsub` is still referenced [src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs:89]
- [x] [Review][Patch] `AddHexalithEventStore` changed a packable public method signature instead of preserving the old overload, creating a binary compatibility break for existing `Hexalith.EventStore.Aspire` consumers [src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs:58]
- [x] [Review][Patch] The fallback C# metadata remains an independently editable second source for `actorStateStore`, `redisHost`, and `keyPrefix` instead of being removed, derived, or validated against the authoritative YAML [src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs:80]
- [x] [Review][Patch] DAPR access-control docs still claim `accesscontrol.eventstore-admin.yaml` has empty policies even though the current local config and tests allow `eventstore-admin-ui` [docs/guides/dapr-component-reference.md:902]

## References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-27-eventstore-parties-review-residuals.md#ES-7`] - residual scope: decide whether `statestore.yaml` or `WithMetadata` override is authoritative and align validator contract.
- [Source: `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs`] - current AppHost component metadata override.
- [Source: `src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml`] - checked-in local Redis state-store YAML.
- [Source: `src/Hexalith.EventStore.AppHost/Program.cs`] - sidecar/component references and isolation boundaries.
- [Source: `deploy/dapr/statestore-postgresql.yaml`] - production PostgreSQL state-store component.
- [Source: `deploy/dapr/statestore-cosmosdb.yaml`] - production Cosmos DB state-store component.
- [Source: `tests/Hexalith.EventStore.Server.Tests/DaprComponents/DaprComponentValidationTests.cs`] - local DAPR component validation.
- [Source: `tests/Hexalith.EventStore.Server.Tests/DaprComponents/ProductionDaprComponentValidationTests.cs`] - production DAPR component validation and NFR29 parity.
- [Source: `_bmad-output/implementation-artifacts/8-6-deployment-manifests-and-environment-portability.md`] - prior FR43/NFR29 portability story and validation pattern.
- [Source: `_bmad-output/implementation-artifacts/post-epic-22-r22a1-query-router-actor-proxy-fix.md`] - precedent for removing misleading AppHost DAPR component files and preserving active topology.
- [Source: `_bmad-output/project-context.md`] - repository rules for Aspire, DAPR, testing, docs, and access boundaries.
- [External: Aspire DAPR integration docs](https://aspire.dev/integrations/frameworks/dapr/)
- [External: DAPR Redis state-store docs](https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-redis/)
- [External: DAPR State API docs](https://docs.dapr.io/reference/api/state_api/)

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Mini-ADR Source-of-Truth Decision

- Selected path: YAML authoritative.
- Rejected path: Aspire app model metadata authoritative, because it would keep backend metadata hidden in C# and make Parties/deployment validation reason about a different artifact than operators edit.
- Decision rubric summary: YAML best satisfies FR43/NFR29 portability, docs clarity, Parties validator compatibility, and drift resistance. Aspire compatibility is preserved by `DaprComponentOptions.LocalPath`, which keeps the DAPR component resource and sidecar references while loading the checked-in YAML. Testability is covered by structured YAML assertions plus static AppHost/doc guard tests.
- Required edits: `statestore.yaml` now includes `keyPrefix=none`; AppHost passes `statestore.yaml` into `AddHexalithEventStore`; the Aspire extension loads it with `DaprComponentOptions.LocalPath`; docs and architecture identify the YAML as authoritative; regression tests guard metadata, scoping, AppHost wiring, and stale docs.
- Non-authoritative surface handling: the AppHost C# metadata surface is derived from the authoritative YAML when the AppHost passes `stateStoreComponentPath`; the old generated metadata remains only as a fallback for external consumers that do not provide a component file.
- `keyPrefix` decision: preserved with reason. `eventstore-admin` and `tenants` must read shared EventStore-owned keys through their own DAPR sidecars, so DAPR app-id key prefixing must stay disabled for the shared local state store. Other domain-service isolation remains enforced by AppHost references, component scopes, and DAPR access-control policy rather than Redis key prefixing.
- Red-team checks completed: operator edits now target the loaded YAML; Redis key layout preserves current admin/state access; `eventstore-admin` remains scoped; sample, sample UI, and admin UI have no direct state-store/pubsub component references; Parties can keep asserting YAML semantics.
- Parties handoff: Parties should keep asserting YAML. The downstream validator should assert `metadata.name=statestore`, local `spec.type=state.redis`, `actorStateStore=true`, `keyPrefix=none`, scopes `eventstore` + `eventstore-admin` + `tenants`, and no direct sample/domain access outside the explicitly shared Tenants topology. A downstream follow-up was recorded in `_bmad-output/implementation-artifacts/deferred-work.md`.

### Implementation Plan

- Use the CommunityToolkit DAPR `LocalPath` pattern with an isolated generated copy so Aspire still owns resource/reference wiring while DAPR loads only the checked-in state-store YAML semantics.
- Keep changes limited to the local state-store source-of-truth decision, docs, drift tests, and downstream handoff. Pub/sub wiring, access-control semantics, resiliency, command/query contracts, and key builders remain unchanged.
- Validate with focused DAPR component tests, recommended unit-test projects, AppHost build, release build, doc grep, and attempted Aspire runtime evidence.

### Debug Log References

- 2026-05-27: Story creation attempted `aspire run --detach --non-interactive --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json`; existing AppHost was stopped, but detached run failed.
- 2026-05-27: `dotnet build src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --no-restore` passed with 0 warnings / 0 errors.
- 2026-05-27: Retried `EnableKeycloak=false` Aspire run; build passed, but AppHost exited because Docker was not running.
- 2026-05-27: Pre-change `EnableKeycloak=false aspire run --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --non-interactive --detach --format Json` rebuilt AppHost successfully, then exited with code 2 because Docker is not running or not installed from this shell. Aspire MCP resource tools were not available in this session.
- 2026-05-27: Official Aspire DAPR docs checked: actor-state-store example uses component YAML plus `DaprSidecarOptions.ResourcesPaths`; local CommunityToolkit XML confirms `DaprComponentOptions.LocalPath`, enabling YAML-authoritative AppHost resource wiring.
- 2026-05-27: Initial focused DAPR component test run exposed a stale local Admin.Server ACL assertion; updated it to the current `eventstore-admin-ui` D13 invocation topology, then reran the focused suite green.
- 2026-05-27: Documentation cleanup grep for stale `{env:REDIS_HOST|localhost:6379}`, local `Default: localhost:6379`, and `EventStore:CommandStatus:StateStoreName` default `"eventstore"` across touched docs returned no matches.

### Completion Notes List

- Selected and implemented `YAML authoritative` for the local DAPR `statestore` component.
- AppHost now resolves `DaprComponents/statestore.yaml`, copies it into an isolated generated resources folder, and passes that copy to `HexalithEventStoreExtensions`; the Aspire extension loads it through `DaprComponentOptions.LocalPath` while retaining a generated-metadata fallback for external consumers.
- Added `keyPrefix=none` to the authoritative YAML and documented why admin shared-state reads rely on it.
- Updated architecture and operational docs for source-of-truth language, `eventstore-admin` scope, Redis host fallback, Docker Compose rewrite guidance, and the `EventStore:CommandStatus:StateStoreName` default.
- Added regression coverage for YAML metadata, AppHost `LocalPath` wiring, no direct sample/domain state-store/pubsub access, stale doc text, and the current Admin.UI to Admin.Server DAPR ACL.
- Recorded the downstream Parties validator handoff in `deferred-work.md`; no Hexalith.Parties code was present or edited.

### File List

- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/post-epic-22-es7-statestore-yaml-source-of-truth-decision.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/planning-artifacts/architecture.md`
- `docs/getting-started/first-domain-service.md`
- `docs/guides/configuration-reference.md`
- `docs/guides/dapr-component-reference.md`
- `docs/guides/deployment-docker-compose.md`
- `src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml`
- `src/Hexalith.EventStore.AppHost/Program.cs`
- `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs`
- `tests/Hexalith.EventStore.Server.Tests/DaprComponents/DaprComponentValidationTests.cs`

## Verification Status

Implemented and ready for review.

- PASS: `EnableKeycloak=false aspire run --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --non-interactive --detach --format Json` built AppHost successfully before failing on Docker prerequisite (`Docker is not running or not installed`).
- PASS: `dotnet build src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --no-restore` (0 warnings, 0 errors).
- PASS: `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~DaprComponentValidationTests|FullyQualifiedName~ProductionDaprComponentValidationTests" --no-restore` (38 passed).
- PASS: `dotnet test tests\Hexalith.EventStore.Client.Tests\Hexalith.EventStore.Client.Tests.csproj --no-restore` (399 passed).
- PASS: `dotnet test tests\Hexalith.EventStore.Contracts.Tests\Hexalith.EventStore.Contracts.Tests.csproj --no-restore` (513 passed).
- PASS: `dotnet test tests\Hexalith.EventStore.Sample.Tests\Hexalith.EventStore.Sample.Tests.csproj --no-restore` (74 passed).
- PASS: `dotnet test tests\Hexalith.EventStore.Testing.Tests\Hexalith.EventStore.Testing.Tests.csproj --no-restore` (144 passed).
- PASS: `dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore` (0 warnings, 0 errors).
- PASS: manual/static doc grep for stale local Redis fallback/default and command-status state-store default returned no matches in the touched docs/architecture scope.
- BLOCKED: live Aspire resource inspection via MCP/dashboard was not possible because Aspire MCP tools were not available and Docker was not running.
- PASS: post-review `dotnet build src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --no-restore` (0 warnings, 0 errors).
- PASS: post-review `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~DaprComponentValidationTests|FullyQualifiedName~ProductionDaprComponentValidationTests" --no-restore` (39 passed).
- BLOCKED: post-review live Aspire run was not attempted because `docker info --format '{{.ServerVersion}}'` failed to connect to Docker Desktop (`dockerDesktopLinuxEngine` pipe not found).

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-27 | 0.1 | Created ready-for-dev post-Epic-22 ES-7 story: state-store config source-of-truth decision, AppHost/YAML drift guardrails, docs/test alignment, Parties validator handoff, and Aspire baseline blocker recorded. | Codex |
| 2026-05-27 | 0.2 | Applied party-mode review hardening: mini-ADR decision table, non-authoritative-source requirement, testable `keyPrefix` contract, explicit preservation invariants, doc search targets, negative coverage expectations, and concrete Parties handoff guidance. | Codex |
| 2026-05-27 | 0.3 | Applied advanced elicitation refinements: decision rubric, default YAML-authoritative rule, minimum evidence gates, red-team checks, and Mini-ADR completion template. | Codex |
| 2026-05-27 | 1.0 | Implemented YAML-authoritative statestore source of truth, AppHost `LocalPath` wiring, docs/architecture alignment, drift tests, validation evidence, and Parties handoff. | Codex |
| 2026-05-27 | 1.1 | Applied code-review fixes: Tenants scope alignment, isolated `LocalPath` copy, public overload preservation, fallback drift guard, and stale ACL docs cleanup. | Codex |

## Story Completion Status

Implementation complete. Status: done.
