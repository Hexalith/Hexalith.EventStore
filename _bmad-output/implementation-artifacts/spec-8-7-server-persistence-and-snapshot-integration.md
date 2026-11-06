---
title: 'Story 8.7: Server Persistence And Snapshot Integration'
type: 'feature'
created: '2026-11-06'
status: 'draft'
route: 'dispatch'
execution_disposition: 'awaiting-on-premises-custody-decision'
review_loop_iteration: 0
baseline_commit: 'fd77eb18b548ca9c1f13b97014441a26d6179ee7'
planning_revision: '6ef8dcd4fc75e9bf20e403f2baf326e882a05d69'
story_key: '8-7-server-persistence-and-snapshot-integration'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-7-on-premises-backend-amendment.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Server has typed hooks and an interaction-specific engine, but lacks shared-engine registration, compatibility routing and lifecycle integration. Its Azure prerequisite conflicts with the owner's selected on-premises path; no replacement backend is qualified yet.

**Approach:** Qualify a Platform-owned on-premises backend, then connect shared-engine integration through existing actor hooks. The detached OpenBao proposal awaits a custody decision.

**Owner decision (2026-11-07):** Option 2 selects planning an on-premises backend with Hexalith.Platform and revising the Azure dependency. This does not select software custody, authorize deployment or change Epic 8's post-MVP scheduling.

## Boundaries & Constraints

**Always:** Preserve trusted occurrence identity, actor-owned atomic append, durable reservation before payload staging, completion only after proven persistence, monotonic fences, current tenant authorization, typed unreadability and buffer clearing. Retain interaction-specific authority/recovery semantics and exactly one default no-op service.

**Never:** Infer custody from hosting, treat fake crypto as production proof, rewrite frozen approvals or mark Azure 8.6 complete. Platform owns Kubernetes infrastructure at `192.168.1.30`. Use Dapr for the application boundary; provider internals stay in Platform. Both future projects remain non-packable; the manifest remains 14. No deployment or infrastructure mutation is included.

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

1. Which key-custody guarantee should govern this path? **Qualify OpenBao software custody** accepts the proposed change from hardware-protected to software-managed keys. **Preserve hardware custody** requires Platform to identify and qualify a hardware key service. Auto-unseal hardware alone is insufficient. Neither choice deploys infrastructure; the final spec/amendment still needs approval.

## Code Map

Paths below are relative to `src/` unless prefixed with `tests/`.

- `Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj`: existing shared-core source linking; avoid duplicate engine dependencies.
- `Hexalith.EventStore.Server/Events/EventPersister.cs`, `SnapshotManager.cs`: existing preparation and typed interaction hooks; preserve atomic staging and advisory snapshot semantics.
- `Hexalith.EventStore.Server/Actors/AggregateActor.PayloadProtection.cs`: existing completion journal, reminders and exact-source recovery; reuse with `AggregateActor.cs`.
- `Hexalith.EventStore.Server/Events/DaprLogicalEventReader.cs`: read seam; preserve publication, projections, Streams and Admin authorization.
- `Hexalith.EventStore.PayloadProtection/`: reuse the generic compatibility router, reservation/deployment coordinators, key resolver and capability probe. `PayloadProtectionBackend.cs` and `PayloadBackendResult.cs` provide the internal SPI and 512-byte cap; use Dapr without a Platform assembly dependency.

## Tasks & Acceptance

**Execution, conditional on custody decision and exact supplement approval:**

- [ ] `_bmad-output/implementation-artifacts/spec-8-7-on-premises-backend-amendment.md` — finalize custody, transport, authority and conformance scope; bind exact approval before changing effective policy.
- [ ] `_bmad-output/planning-artifacts/epics.md` — reconcile 8.7's dependency to qualified on-premises conformance after approval; preserve Azure 8.6 deferral and reconcile the later 8.8 release assumptions separately.
- [ ] `_bmad-output/implementation-artifacts/deferred-work.md` — verify capacity/erasure-memory prerequisites; carry enrollment, shared namespace, restore identity and legacy-history obligations. Preserve entries. Platform qualifies its component in separately scoped work.
- [ ] `src/Hexalith.EventStore.PayloadProtection/` — implement the approved neutral Dapr backend and bounded exact-version/result checks using the existing SPI; qualify real-service behavior before host wiring.
- [ ] `src/Hexalith.EventStore.Server/Configuration/` — add closed shared-engine registration/startup validation without replacing interaction authority semantics.
- [ ] `src/Hexalith.EventStore.Server/Events/`, `src/Hexalith.EventStore.Server/Actors/AggregateActor.PayloadProtection.cs` — connect generic occurrence, reservation/completion and typed reads through existing atomic boundaries; cover every owning Server/Admin read path before completion.
- [ ] `tests/Hexalith.EventStore.Server.Tests/`, `tests/Hexalith.EventStore.PayloadProtection.Tests/` — extend existing hook, compatibility and lifecycle tests for the matrix and registration ordering.
- [ ] `tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests/`, `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/` — prove the approved real backend, then persisted events/snapshots, reservations, recovery and checkpoints through real Dapr.

**Acceptance Criteria:**

- Given unresolved custody or missing approved backend/capacity evidence, when activation is evaluated, then runtime integration remains blocked.
- Given authorized integration, when startup and fault matrices execute, then default behavior and all existing interaction guarantees survive.
- Given failed reads or writes, when durable state is inspected, then no partial staging, unsafe replay, plaintext fallback or false completion occurs.
- Given full 8.7 completion, when assessed against the reconciled criteria, then exact supplement, predecessor, source, real persisted backend and named approval evidence exists; Azure deferral is not Azure completion.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

Planning checks: paths, detached/base digests, draft structure and `git diff --check`. Runtime/CI/cluster checks remain pending. Final commands/files follow the custody decision. Preserve concurrent changes.
