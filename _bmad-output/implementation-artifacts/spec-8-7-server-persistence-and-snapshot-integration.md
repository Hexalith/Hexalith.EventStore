---
title: 'Story 8.7: Server Persistence And Snapshot Integration'
type: 'feature'
created: '2026-11-06'
status: 'draft'
route: 'dispatch'
execution_disposition: 'dependency-blocked'
review_loop_iteration: 0
baseline_commit: 'fd77eb18b548ca9c1f13b97014441a26d6179ee7'
story_key: '8-7-server-persistence-and-snapshot-integration'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Server has typed payload-protection hooks and an interaction-specific engine, but lacks the shared engine's registration, compatibility routing and lifecycle integration. Story 8.7 requires completed real-backend conformance from Story 8.6, which remains incomplete and deferred post-MVP.

**Approach:** Reuse existing actor persistence/completion hooks to connect the approved shared engine after its prerequisites are satisfied. Resolve the scope question below before approving an implementation plan.

## Boundaries & Constraints

**Always:** Preserve trusted occurrence identity, actor-owned atomic append, durable reservation before payload staging, completion only after proven persistence, monotonic fences, current tenant authorization, typed unreadability and buffer clearing. Retain interaction-specific authority/recovery semantics and exactly one default no-op service.

**Never:** Infer a backend from on-premises hosting; substitute fake crypto for production conformance; change approved formats, frozen approvals or predecessor status; enable protection implicitly; publish packages or provision infrastructure. Hexalith.Platform owns on-premises Kubernetes infrastructure at `192.168.1.30`. Both future projects remain non-packable and the release manifest remains 14 packages.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Failure handling |
|---|---|---|---|
| Default startup | Registration omitted or Disabled | Existing no-op; no backend access | Safe bounded diagnostics |
| Enabled startup | Explicit closed options and current authority | Validate mode, backend, state profile and leases | Refuse startup on ambiguity |
| Event batch | Typed events and trusted sequence/type context | Protect/reserve all before staging; actor commits | No partial staging or plaintext fallback |
| Crash/cancellation | Reserved keys or uncertain commit | Recover using exact durable source evidence | Retain uncertainty; never infer absence |
| Snapshot | Registered type and generated metadata | Authenticate before deserialize; journal completion | Preserve unreadable bytes; safe replay baseline |
| Mixed reads | Plain, legacy, v2, opaque history | Shared typed routing; stop at first unreadable | No disclosure/checkpoint advancement |

</frozen-after-approval>

## Open Questions

1. Which scope should govern the requested work? **Keep the approved dependency** leaves 8.7 deferred until 8.6 and the capacity prerequisite are complete. **Plan a local-only slice** proposes a separately approved limited integration scope without claiming 8.7 production completion. **Plan an on-premises backend path** proposes an architecture/dependency amendment coordinated with Platform; hosting alone does not select key custody. The latter two require concrete revised scope before implementation, and neither authorizes infrastructure mutation.

## Code Map

Paths below are relative to `src/` unless prefixed with `tests/`.

- `Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj` already source-links the shared core. Do not introduce a duplicate engine dependency.
- `Hexalith.EventStore.Server/Events/EventPersister.cs`, `SnapshotManager.cs`: existing preparation and typed interaction hooks; preserve atomic staging and advisory snapshot semantics.
- `Hexalith.EventStore.Server/Actors/AggregateActor.PayloadProtection.cs`: existing completion journal, reminders and exact-source recovery; reuse with `AggregateActor.cs`.
- `Hexalith.EventStore.Server/Events/DaprLogicalEventReader.cs`: read seam; preserve publication, projections, Streams and Admin authorization.
- `Hexalith.EventStore.PayloadProtection/`: reuse `PayloadCompatibilityRouter`, `KeyReservationCoordinator`, `PayloadKeyResolver`, deployment coordinator and capability probe. These generic mechanics are separate from `InteractionPayloadProtectionService`.
- `Hexalith.EventStore.PayloadProtection.AzureKeyVault/AzureKeyVaultServiceCollectionExtensions.cs` registers a backend and probe only; it does not enable Server protection.

## Tasks & Acceptance

**Execution, conditional on resolved scope and prerequisites:**

- [ ] `_bmad-output/implementation-artifacts/deferred-work.md` — verify capacity/index and erasure-memory prerequisites; carry host enrollment, shared state namespace, restore identity and legacy-history obligations into the approved plan. Preserve existing entries.
- [ ] `src/Hexalith.EventStore.Server/Configuration/` — add closed shared-engine registration/startup validation without replacing interaction authority semantics.
- [ ] `src/Hexalith.EventStore.Server/Events/`, `src/Hexalith.EventStore.Server/Actors/AggregateActor.PayloadProtection.cs` — connect generic occurrence, reservation/completion and typed reads through existing atomic boundaries; cover every owning Server/Admin read path before completion.
- [ ] `tests/Hexalith.EventStore.Server.Tests/`, `tests/Hexalith.EventStore.PayloadProtection.Tests/` — extend existing hook, compatibility and lifecycle tests for the matrix and registration ordering.
- [ ] `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/` — prove persisted events, snapshots, reservations, recovery and checkpoints through real Dapr; approved real-backend proof remains required for full 8.7.

**Acceptance Criteria:**

- Given missing predecessor evidence or unresolved scope, when activation is evaluated, then runtime integration remains blocked.
- Given authorized integration, when startup and fault matrices execute, then default behavior and all existing interaction guarantees survive.
- Given failed reads or writes, when durable state is inspected, then no partial staging, unsafe replay, plaintext fallback or false completion occurs.
- Given full 8.7 completion, when assessed against the owning epic criteria, then exact predecessor, source, persisted backend and named approval evidence exists; local-only results cannot close that gate.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

Planning only: inspect draft paths, current predecessor disposition and unchanged sprint status; run `git diff --check`. Runtime tests and CI have not run for this draft. Implementation verification commands and exact affected files must be finalized after the scope decision.
