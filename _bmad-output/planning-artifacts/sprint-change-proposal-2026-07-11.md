---
title: Sprint Change Proposal - Parties Projection/Query SDK Parity Completion
status: approved
created: 2026-07-11
approved: 2026-07-11
approved_by: Administrator
project: eventstore
trigger_story: 1.8 Projection/Query SDK Owner Parity Proof
scope_classification: major
recommended_path: direct-adjustment
review_mode: incremental
---

# Sprint Change Proposal - Parties Projection/Query SDK Parity Completion

## 1. Issue Summary

Story 1.8 successfully completed its investigation and owner-proof purpose, but its reviewed packet concludes `still blocked`. The proof established that cursor scope compatibility is available while five required parity capabilities and two architectural seams remain unavailable:

1. Coordinated read-model and projection-checkpoint erasure.
2. Generic detail/index batch writes or an approved equivalent with explicit partial-failure, idempotency, and flush semantics.
3. Lossless mapping for `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly`.
4. Duplicate and out-of-order safety through the production projection-handler path.
5. Correct full rebuild behavior across paging boundaries, verified against canonical aggregate replay.
6. An asynchronous projection persistence seam.
7. Multiple named projections per domain.

Administrative closure is also incomplete:

- Proof-result owner review and approval are pending.
- The latest proof packet remains `still blocked`.
- The proof packet records intended runtime SHA `f31777ae8dd3902f65a27777a04ee49d790a6e8f`, while the inspected checkout is `404201a9363c1b80121ecbf5e72ca4fb71f6ac79`.
- Epic 1 remains `in-progress`, but Stories 1.1-1.8 are all marked `done`; no implementation stories currently close the blocked result.

The trigger is a technical limitation discovered during parity investigation, compounded by missing implementation planning. Story 1.8 should remain `done` because the investigation and blocked proof packet were completed. Its status must not be interpreted as SDK availability.

### Evidence

- `_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-parity-proof.md`
- `_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/planning-artifacts/prd.md`
- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/planning-artifacts/backlog/gdpr-1-aggregate-erasure.md`

## 2. Impact Analysis

### 2.1 Epic Impact

Epic 1 cannot complete as currently planned. It owns the platform projection/query/read-model seams, and Story 1.8 demonstrated that those seams cannot yet replace Parties' local mechanics. The recommended correction is to add Stories 1.9-1.15 within Epic 1 rather than create a disconnected new epic.

No existing epic becomes obsolete. Two overlaps require explicit reconciliation:

- Story 2.8 owns query-route provenance and gateway ETag enforcement. It does not own the six-state projection lifecycle contract.
- Stories 6.3/6.4 own later replay-cost and sequence-guard specification/optimization. They must build on the correctness baseline established by Stories 1.13/1.14 and must not redefine it.

Story 7.5 and GDPR-1 continue to own deferred aggregate/event tombstoning. Generic read-model and checkpoint erasure moves into active Epic 1 implementation scope without authorizing event-stream, broker-history, backup, audit, or crypto-shred erasure.

### 2.2 Story Impact And Sequencing

Add these Epic 1 stories:

1. **1.9 Read-Model And Projection Checkpoint Erasure**
2. **1.10 Coordinated Read-Model Batch Writes**
3. **1.11 Complete Projection Freshness Lifecycle**
4. **1.12 Asynchronous Multi-Projection Dispatch**
5. **1.13 Projection-Handler Delivery Idempotency**
6. **1.14 Correct Paged Rebuild And Replay Equivalence**
7. **1.15 Owner-Approved Parity Closure And Runtime Pin**

Required order:

1. Stories 1.9-1.11 establish storage and metadata contracts. Story 1.11 consumes the completed Story 2.8 provenance contract.
2. Story 1.12 establishes asynchronous named projection dispatch and one-to-many outcomes.
3. Stories 1.13 and 1.14 prove delivery and rebuild correctness using the earlier seams.
4. Story 1.15 reruns parity, records owner approval, and binds the approved runtime SHA.

Parties Story 8.6 remains blocked until Story 1.15 records `available` and Parties verifies its checked-out EventStore SHA matches the approved runtime SHA.

### 2.3 PRD Impact

The PRD currently permits the incomplete APIs to appear compliant. Required changes:

- Expand FR4 with the complete projection lifecycle representation and provenance-safe mapping.
- Expand FR5 with coordinated model/checkpoint erasure and batch-write semantics.
- Expand FR7 with asynchronous one-to-many projection dispatch, production-path idempotency, and rebuild correctness.
- Add FR36 for owner-reviewed consumer parity closure tied to an exact runtime SHA.
- Strengthen NFR6, NFR8, and NFR16 with handler-path, rebuild-equivalence, and persisted-state proof requirements.
- Update MVP scope, success metrics, and traceability.
- Clarify that generic projection-state erasure is in scope while full GDPR aggregate/event erasure remains deferred.

### 2.4 Architecture Impact

Required architecture changes:

- Extend AD-7 with coordinated lifecycle erasure and generic batch semantics.
- Strengthen AD-8 so at-least-once/unordered safety is proven through the actual projection handler.
- Add AD-19: asynchronous handlers keyed by `(Domain, ProjectionType)` with one-to-many dispatch and truthful outcomes.
- Add AD-20: paged rebuilds are staged and replay-equivalent; page-only state can never overwrite a complete live model.
- Extend AD-14/AD-15 so lifecycle state is orthogonal to route provenance and preserves all six Parties states.
- Extend AD-13 so Epic 6 optimization starts from, and cannot weaken, the new correctness baseline.

### 2.5 UX Impact

No new screen or navigation entry is required. The current three-state `current`/`stale`/`unknown` UX is insufficient. The projection freshness indicator, state tables, issue banners, mutation enablement, accessibility behavior, and projection-confirmation flows must support:

| State | Default UX | Mutation policy |
| --- | --- | --- |
| `Current` | Current projection evidence | Allowed when otherwise authorized |
| `Stale` | Last-known data plus warning and confirmation time | Disabled |
| `Rebuilding` | In-progress state and bounded progress when available | Disabled |
| `Degraded` | Warning naming affected capability and consequence | Disabled unless explicitly approved |
| `Unavailable` | Unavailable state with safe retry/support action | Disabled |
| `LocalOnly` | Explicit non-authoritative/local-only state | Disabled unless a documented consumer exception exists |
| `Unknown` | Neutral absence of qualifying evidence | Disabled |

Only `ProjectionBacked` responses may carry authoritative lifecycle states. Handler-computed or missing/invalid provenance renders `Unknown`.

### 2.6 Secondary Artifact And Technical Impact

- Add Stories 1.9-1.15 to `epics.md` and `sprint-status.yaml` as backlog items while retaining Epic 1 as `in-progress`.
- Preserve Story 1.8 and its blocked packet as historical evidence; Story 1.15 produces a versioned successor.
- Update GDPR-1 to separate generic projection-state cleanup from full aggregate/event erasure.
- Update query/projection API documentation for lifecycle, async named handlers, multi-projection outcomes, batching, duplicate/gap behavior, rebuild staging, and runtime-pin verification.
- Add bounded, support-safe telemetry for batch recovery, fan-out failure, duplicate/gap decisions, rebuild lifecycle, checkpoint drift, and freshness transitions.
- Add deterministic unit/contract lanes and persisted-state/live-DAPR evidence lanes without moving live-DAPR tests into the deterministic release gate.
- Keep `tools/release-packages.json` unchanged at 14 packages. New APIs belong in existing Contracts, Client, DomainService, Server, Testing, and Testing.Integration packages.
- Do not modify Parties or Hexalith.AI.Tools submodules through this change. Cross-repository handoffs require their owners' approval and separate commits.
- Preserve active user-owned Story 2.8 work; do not fold six-state freshness into that provenance-only slice.

## 3. Recommended Approach

### Selected Path: Direct Adjustment

Add the seven implementation/closure stories within Epic 1, update the governing PRD and architecture, reconcile Epic 6 and GDPR boundaries, then rerun implementation readiness.

This path preserves completed, useful SDK work and turns Story 1.8's findings into explicit implementation ownership. It avoids destabilizing shipped work and keeps the correction aligned with the epic that already owns the seams.

### Alternatives Considered

#### Potential Rollback - Not Viable

Story 1.8 changed no runtime SDK behavior. Reverting Stories 1.3, 1.4, or 1.6 would remove valid capabilities without adding the missing ones. Estimated effort and risk are both high with no parity benefit.

#### MVP Scope Reduction - Not Viable Under The Required Scope

Reducing scope would mean explicitly deferring Parties Story 8.6 and accepting continued local projection infrastructure. It is low immediate effort but carries high strategic and maintenance risk and conflicts with the stated parity requirement.

### Effort, Risk, And Timeline

- **Scope:** Major; requires Product Manager and Architect involvement plus Developer, Test Architect, and Technical Writer execution.
- **Estimated effort:** 25-40 engineer-days.
- **Estimated elapsed time:** approximately 4-8 calendar weeks depending on parallel capacity and live-DAPR findings.
- **Risk:** High.

Primary risks:

- Public API and wire compatibility when replacing synchronous single-response projection handling.
- DAPR transactional capability differences and recovery behavior for multi-key batches.
- Checkpoint/dedup state growth and late-event behavior.
- Rebuild staging, cancellation, resume, and atomic promotion.
- Incorrect lifecycle/provenance collapse in clients or generated APIs.
- Owner approval or consumer-pin drift occurring after technical work completes.

Risk controls:

- Additive compatibility adapters or an explicit breaking-version decision.
- Persisted-state evidence, not mock-only proof.
- Live-DAPR validation for transactions, partial failure, and rebuild behavior.
- Per-story review boundaries and an independent closure review in Story 1.15.
- No consumer migration authorization until the exact approved SHA matches.

## 4. Detailed Change Proposals

### 4.1 PRD Edits

#### FR4 - Freshness Semantics

**OLD:** Query metadata propagates freshness, projection version, ETag, served-at, degraded/warning state, paging, and provenance.

**NEW:** Add a lossless lifecycle representation or owner-approved mapping for `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly`. Consumers must not infer lifecycle from ETags or claim projection-confirmed success without projection-backed provenance.

#### FR5 - Read-Model Lifecycle And Coordinated Writes

**OLD:** Generic ETag-aware store/write policy with optimistic merge and multi-key support.

**NEW:** Add coordinated read-model/checkpoint erasure and detail/index batches or an approved equivalent. Define partial failure, idempotency, ordering, optimistic concurrency, flush completion, DAPR behavior, and deterministic fake semantics.

#### FR7 - Projection Seam

**OLD:** Generic synchronous projection-handler seam and event consumer pipeline.

**NEW:** Require asynchronous, cancellation-aware, named multi-projection dispatch and persistence, production-path duplicate/out-of-order safety, and rebuild correctness across paging boundaries.

#### FR36 - Consumer Parity Closure

**NEW:** Before a consumer deletes local projection/query infrastructure, EventStore must produce an owner-reviewed parity packet proving required capabilities through production paths, record an approved runtime SHA, and require the consumer checkout to match.

#### NFR And MVP Edits

- NFR6: require duplicate/out-of-order safety through the production projection-handler path.
- NFR8: require paged rebuild output to equal canonical replay and prohibit partial-page overwrite.
- NFR16: require persisted detail/index/checkpoint evidence for erasure, batching, idempotency, and rebuild equivalence.
- Add the seven parity capabilities and closure gate to MVP scope.
- Keep full GDPR aggregate/event tombstoning, broker-history deletion, backup erasure, and crypto-shredding out of scope.
- Add FR36 traceability to Epic 1 Stories 1.9-1.15 and a success metric requiring an approved `available` packet and matching consumer pin.

### 4.2 Architecture Edits

#### AD-7

**OLD:** Persisted read models use `IReadModelStore` and `ReadModelWritePolicy`.

**NEW:** Erasure removes model and companion checkpoint as one logical operation. Detail/index changes use a transaction or explicitly approved resumable equivalent with documented partial-failure, idempotency, ordering, and flush semantics.

#### AD-8

**OLD:** Consumers deduplicate by `MessageId`.

**NEW:** Duplicate and out-of-order safety is enforced and proven through the actual dispatcher/handler/persistence path. Sequence guards are projection-scoped and never globally ordered.

#### AD-19 - Async One-To-Many Dispatch

**NEW:** Handlers are identified by `(Domain, ProjectionType)`, may fan out for one domain, are asynchronous/cancellation-aware, and return truthful per-projection outcomes. Legacy synchronous handling requires an additive adapter or approved breaking-version plan.

#### AD-20 - Replay-Equivalent Rebuilds

**NEW:** Paging is a read optimization, never a semantic projection boundary. Rebuilds use non-live staging/equivalent isolation, promote only complete outputs, and must equal canonical aggregate replay for detail, index, version, and checkpoint state.

#### AD-14/AD-15 And AD-13

- Treat lifecycle and provenance as orthogonal; only projection-backed responses carry authoritative lifecycle.
- Preserve all six Parties states and keep handler-computed/unknown responses `Unknown`.
- Make Stories 1.13/1.14 the correctness baseline that Stories 6.3/6.4 may optimize but not weaken.

### 4.3 UX Edits

**OLD:** Projection indicators support current/stale/unknown.

**NEW:** Add the lifecycle table in section 2.5, provenance gating, accessible state text, live-region announcements for restrictive transitions, focus preservation when actions become disabled, stable state selectors, and support-safe copy. No new navigation or screen is required.

### 4.4 Story 1.9 - Read-Model And Projection Checkpoint Erasure

**OLD:** No implementation story; GDPR-1 defers aggregate erasure.

**NEW:** Implement asynchronous ETag-aware read-model erasure plus a platform-owned tenant/domain/aggregate/projection-scoped operation that removes companion delivery/rebuild checkpoints. Absent deletion is idempotent. Same-store work uses a transaction where supported; otherwise it uses a resumable protocol. DAPR and in-memory implementations must match. Persisted tests prove erase/read-back/recreate behavior and cross-tenant isolation. Event streams, snapshots, broker history, backups, audit evidence, and keys remain out of scope.

### 4.5 Story 1.10 - Coordinated Read-Model Batch Writes

**OLD:** Sequential single-key writes with no approved batch-equivalent contract.

**NEW:** Implement an async batch for typed writes/deletes within one configured store. Define transaction or resumable-equivalent semantics, incomplete/conflict results, stable batch identity, duplicate success, conflict behavior, flush completion, cancellation, deterministic failure injection, and persisted detail/index/batch-marker evidence. Never describe cross-store work as atomic.

### 4.6 Story 1.11 - Complete Projection Freshness Lifecycle

**OLD:** `Unknown`, `Current`, `Aging`, and `Stale` plus separate Boolean metadata.

**NEW:** Add a stable fail-safe contract containing `Unknown`, `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly`; define authoritative state sources; gate state on projection-backed provenance; retain legacy Boolean compatibility without fabricating evidence; propagate through gateway/client/generated API/UI metadata; document the Parties mapping; and verify serialization, omission, headers, and persisted-path propagation. Build on Story 2.8 without reopening its route-selection scope.

### 4.7 Story 1.12 - Asynchronous Multi-Projection Dispatch

**OLD:** Synchronous `Project`, domain-only uniqueness, one response.

**NEW:** Add an asynchronous, cancellation-aware handler contract keyed by `(Domain, ProjectionType)`, invoke every applicable handler, expose bounded distinguishable outcomes, keep checkpoint truth under partial failure, and prove both detail and index persistence for one domain. Preserve legacy consumers through an adapter or approved version change. No Parties-specific logic enters EventStore.

### 4.8 Story 1.13 - Projection-Handler Delivery Idempotency

**OLD:** No duplicate/out-of-order proof through the production handler path.

**NEW:** Persist projection-scoped `MessageId` identity and sequence/checkpoint state; make completed duplicates no-ops, in-progress duplicates retryable, lower applied sequences harmless, gaps retryable, and sequence/message conflicts safe failures. Coordinate writes and completion markers. Bound dedup state and route out-of-window delivery to rebuild/reconciliation. Prove final detail/index/checkpoint state through the complete production path for duplicate, reversed, gap, partial-failure, and conflict scenarios.

### 4.9 Story 1.14 - Correct Paged Rebuild And Replay Equivalence

**OLD:** A 256-event page may be sent to a full-replay handler and overwrite live state.

**NEW:** Declare/adapt full-replay versus incremental handlers; use operation-scoped staging/equivalent isolation; read every page without skip/duplication/reordering; keep live models intact on incomplete work; resume safely; and promote only when every required projection completes. Compare a stream larger than two pages against canonical replay, including detail, index, versions, checkpoints, bounded positions, cancellation, failure, and resume.

### 4.10 Story 1.15 - Owner-Approved Parity Closure And Runtime Pin

**OLD:** Story 1.8 is done with pending approval, a blocked packet, and a mismatched checkout.

**NEW:** Require Stories 1.9-1.14 and Story 2.8 prerequisites; re-evaluate every original and newly identified requirement; cite production-path evidence; record owner, date, approval source, limitations, rollback, and one exact implementation SHA; require Parties to match that SHA; and keep the story `in-progress` with the blocking condition recorded while the packet remains blocked. Epic 1 becomes done only after the packet is `available`.

### 4.11 Planning And Tracking Reconciliation

- Add a Parties parity execution gate to `epics.md`.
- Keep Story 1.8 `done` but label it a completed blocked investigation.
- Add Stories 1.9-1.15 as `backlog` under Epic 1 in `sprint-status.yaml`.
- Keep Epic 1 `in-progress` until Story 1.15 succeeds.
- Add FR36 to the inventory and Epic 1 coverage.
- Update Stories 6.3/6.4 to depend on and preserve Stories 1.13/1.14.
- Update GDPR-1 to distinguish active generic read-model cleanup from deferred aggregate/event erasure.

### 4.12 Documentation, Evidence, And Release Boundaries

- Preserve the Story 1.8 packet as historical blocked evidence and link a versioned successor only after approval.
- Update query/projection documentation and durable project context after implementation establishes the new contracts.
- Add support-safe observability and persisted-state/live-DAPR evidence.
- Keep the 14-package release manifest unchanged and run API/package compatibility validation.
- Rerun implementation readiness after planning artifact changes; verify FR1-FR36, updated NFR traceability, and the new sprint entries.

## 5. Implementation Handoff

### Scope Classification

**Major** - the correction changes PRD requirements, architecture invariants, public SDK contracts, persistence/rebuild semantics, UX state mapping, testing evidence, and sprint sequencing.

### Recipients And Responsibilities

| Recipient | Responsibility |
| --- | --- |
| Product Manager / Product Owner | Approve FR36, MVP clarification, story order, and backlog reconciliation; keep Parties 8.6 blocked until closure. |
| Solution Architect | Own AD-7/8/13/14/15/19/20 changes, batch recovery semantics, async compatibility path, lifecycle contract, and rebuild promotion design. |
| Developer | Implement Stories 1.9-1.14 in dependency order without modifying unrelated user-owned Story 2.8 work. |
| Test Architect | Define persisted-state and live-DAPR evidence, failure injection, replay-equivalence corpus, and package/API compatibility gates. |
| Technical Writer | Update query/projection documentation, migration guidance, state mapping, and runtime-pin handoff after contracts land. |
| EventStore Owner/Reviewer | Independently review the final packet and record explicit approval or continued blockage in Story 1.15. |
| Parties Maintainer | After approval, pin the exact EventStore SHA, rerun consumer checks, update the Parties prerequisite matrix, and only then resume Story 8.6. |

### Success Criteria

1. PRD, architecture, UX, epics, GDPR backlog, and sprint status reflect the approved correction.
2. Stories 1.9-1.14 implement and prove every technical requirement through production paths.
3. Detail, index, marker, and checkpoint persisted states converge under duplicates, gaps, conflicts, partial failures, and rebuild resume.
4. Paged rebuild output equals canonical replay for streams exceeding 256 events.
5. Every required lifecycle state propagates without provenance or ETag inference.
6. Existing packages and compatible consumers remain valid, or an explicit versioned migration is approved.
7. Story 1.15 records an owner-approved `available` packet and exact runtime SHA.
8. Parties checks out the approved SHA before Story 8.6 resumes or local rollback code is removed.

## 6. Change Navigation Checklist Status

### Understand The Trigger And Context

- [x] 1.1 Triggering story identified.
- [x] 1.2 Core problem categorized and stated.
- [x] 1.3 Supporting evidence collected.

### Epic Impact Assessment

- [x] 2.1 Epic 1 completion impact assessed.
- [x] 2.2 Epic-level changes identified.
- [x] 2.3 Remaining epics reviewed.
- [x] 2.4 No obsolete epic; no new epic required.
- [x] 2.5 Priority and sequencing changes proposed.

### Artifact Conflict And Impact Analysis

- [x] 3.1 PRD conflicts and MVP clarification identified.
- [x] 3.2 Architecture conflicts and new invariants identified.
- [x] 3.3 UX six-state impact identified.
- [x] 3.4 Documentation, testing, CI, observability, packaging, and ownership impacts identified.

### Path Forward Evaluation

- [x] 4.1 Direct adjustment selected as viable.
- [x] 4.2 Rollback rejected.
- [x] 4.3 MVP reduction rejected under required scope.
- [x] 4.4 Direct adjustment approved incrementally.

### Proposal Components

- [x] 5.1 Issue summary completed.
- [x] 5.2 Epic/artifact impact documented.
- [x] 5.3 Recommended approach and alternatives documented.
- [x] 5.4 MVP impact, action plan, dependencies, and sequencing documented.
- [x] 5.5 Handoff roles and responsibilities defined.

### Final Review And Handoff

- [x] 6.1 Applicable checklist items addressed; pending actions are documented.
- [x] 6.2 Complete proposal accuracy confirmed by user.
- [x] 6.3 Explicit final implementation approval recorded on 2026-07-11.
- [x] 6.4 Approved epic/story changes applied to `sprint-status.yaml` and planning artifacts.
- [x] 6.5 Final handoff recipients and success criteria confirmed by the approved proposal.

## 7. Approval Record

Administrator approved the complete Sprint Change Proposal for implementation on 2026-07-11 after incremental review of the trigger/epic analysis, artifact impact, path, PRD changes, architecture changes, UX changes, Stories 1.9-1.15, planning reconciliation, and documentation/evidence boundaries.

## 8. Workflow Execution Log

- Issue addressed: Story 1.8 proved that Parties projection/query SDK parity remains blocked and exposed seven missing technical capabilities plus incomplete owner/SHA closure.
- Change scope: Major.
- Artifacts modified: PRD, architecture spine, canonical UX design/experience contracts, epic/story plan, GDPR backlog boundary, sprint status, and this proposal.
- Routed to: Product Manager/Product Owner, Solution Architect, Developer, Test Architect, Technical Writer, EventStore owner/reviewer, and Parties maintainer in the responsibilities defined in section 5.
- Implementation boundary: planning and sprint tracking only in this workflow; runtime code, active Story 2.8 work, query API documentation, and submodules remain unchanged.
