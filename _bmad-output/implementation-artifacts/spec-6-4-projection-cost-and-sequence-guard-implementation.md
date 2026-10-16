---
title: 'Story 6.4: Projection Cost And Sequence Guard Implementation'
type: 'feature'
created: '2026-10-15'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'e17e7294f540ebba42fe25192998689f0ecd89c1'
story_key: '6-4-projection-cost-and-sequence-guard-implementation'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md'
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/evidence/story-6-4/implementation-checkpoint.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Live delivery still reads history when current and cannot safely optimize arbitrary tails.

**Approach:** Implement the complete owner-approved specification referenced above, version 1, normative SHA-256 `78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711`. Its sections 1–10, W0–W6, budgets and preservation matrix are mandatory acceptance requirements.

## Boundaries & Constraints

**Always:** Preserve Stories 1.18/1.19, public compatibility, scoped MessageId identity, writer-v2 fences, receipts, complete ID digests and staged promotion. Use owning Dapr seams. Charge all passes, lookahead, proof and scheduler work.

**Never:** Change approved bytes/budgets, infer tail capability, truncate proof, publish partial replay or bypass reconciliation. No snapshots/upcasting/interface cancellation redesign, dependencies, UI, topology, deployment or execution of maintenance cutover.

## I/O & Edge-Case Matrix

| State | Required behavior |
| --- | --- |
| Proven C=H | Double source inspection and bound Idle/model proof; zero envelopes, handlers, writes, notifications or cleanup |
| Valid C<H | Admit complete contiguous readable tail; match every application event before effects; finalize once |
| Unsupported handler | Bounded complete-prefix staged fallback; no page treated as whole history |
| Duplicate/gap/conflict/race/crash | Typed no-op/retry/reconciliation; preserve fences, stable work and proven state |
| Empty/unknown/erased | Distinct outcomes; EmptyNoWork retains Unknown; never manufacture receipts |

</frozen-after-approval>

## Authorized Dependency Exception

On 2026-10-16, the user replied **“I authorize”** to the prepared five-pin update. This explicitly authorizes a narrow exception to the dependency boundary above, solely in root `Directory.Packages.props`:

| Declaration | Package | Version |
| --- | --- | --- |
| Include | Microsoft.AspNetCore.SignalR.Common | 10.0.13 |
| Include | System.Security.Cryptography.Pkcs | 10.0.13 |
| Include | System.Security.Cryptography.Xml | 10.0.13 |
| Update | Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.13 |
| Update | Microsoft.Extensions.Options | 10.0.13 |

Resolve their required transitive closure normally. Do not add other central declarations or change shared reference files or audit policy. Apply the approved update and refresh affected section 10 source/package/build/test evidence, then continue the existing BMAD acceptance and review workflow. Preserve the original normative bytes, budgets, approval and all historical run bindings. The approved exception is unapplied at this handoff; old passing tests and ten-warning builds describe the pre-update packages.

## Code Map

- `src/Hexalith.EventStore.Server/Events/`: hash the stored EventEnvelope, preserving null extensions; AggregateMetadata supports additive init evidence; EventPersister owns its only reconstruction.
- `src/Hexalith.EventStore.Server/Actors/`: AggregateActor owns witnessed saves/cache barriers; retain old actor signatures and erase semantics.
- `src/Hexalith.EventStore.Server/Projections/`: reuse idempotency, retry, lifecycle, rebuild and cutover protocols; existing completion precedes lifecycle release and needs pending finalization.
- `src/Hexalith.EventStore.DomainService/AdminOperationalIndexMetadata.cs` advertises capabilities; `src/Hexalith.EventStore/Indexes/AdminOperationalIndexHostedService.cs` verifies ingestion. Client ProjectionRouteCatalogFingerprint.cs binds descriptors; preserve descriptor-absent bytes.

## Tasks & Acceptance

**Execution:**

- [x] `src/Hexalith.EventStore.Server/Events/` — add SourceProof/codec files; update AggregateMetadata.cs and EventPersister.cs for source-v1 golden hashes and atomic append evidence (§2).
- [x] `src/Hexalith.EventStore.Server/Actors/` — extend IAggregateActor.cs and AggregateActor with focused inspection/range/bootstrap files; preserve barriers, ambiguous-save readback, erasure/incarnation and retained floor. Update `src/Hexalith.EventStore.Testing/Fakes/FakeAggregateActor.cs` (§2/3/9).
- [x] `src/Hexalith.EventStore.Contracts/Projections/`, `src/Hexalith.EventStore.DomainService/`, `src/Hexalith.EventStore.Client/Projections/` and catalog files above — add fingerprint-bound capabilities and strictly read-only owner/progress proof. Marker interfaces, journal acceptance and mutating status/repair APIs cannot prove completion. Preserve dispatch carriers/per-event identities (§2/4/6).
- [x] `src/Hexalith.EventStore.Server/Projections/` — upgrade ProjectionDeliveryState.cs, reservation/coordinator/store contracts to schema2 and exact scope markers. Add IdleBindingPending, same-reservation partial progress and finalization recovery; fence competing rebuild/erase/reconciliation (§2/5/6).
- [x] `src/Hexalith.EventStore.Server/Projections/`: ProjectionDeliveryCutover.cs and ProjectionDeliveryReconciler.cs — implement authenticated bootstrap/qualification, model-proof hydration and persisted attestations without changing writer-marker bytes. Extend `src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs` maintenance seams; no activation (§9).
- [x] `src/Hexalith.EventStore.Server/Projections/`: ProjectionUpdateOrchestrator.cs, NamedProjectionDispatchCoordinator.cs, ProjectionDeliveryRetryWorker.cs and EventStoreProjectionDeliveryHistoryReader.cs — share current/tail logic, remove guarded head-envelope/full-prefix probes, enforce count/byte pages, pre-effect admission matching and safe staged fallback. Preserve wider configured rebuilds (§2–6).
- [x] `src/Hexalith.EventStore.Server/Projections/`: ProjectionDeliveryDiagnostics.cs and retry diagnostics — measure operations/bytes/proof visits with bounded tags and truthful lifecycle/provenance. Update `docs/operations/projection-delivery-v2-cutover.md` and `docs/concepts/projection-delivery.md` (§8/9).
- [x] `tests/Hexalith.EventStore.Server.Tests/`, `tests/Hexalith.EventStore.DomainService.Tests/`, `tests/Hexalith.EventStore.Client.Tests/`, `tests/Hexalith.EventStore.Contracts.Tests/` and `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/` — extend §7's named production fixtures for every matrix case and W0–W6, including every §6 crash boundary. Retain sanitized results under `_bmad-output/implementation-artifacts/evidence/story-6-4/` (§7/10).

**Acceptance Criteria:**

- Given approved prerequisites, when implementation starts, then digest/approval and preserved 1.18/1.19 evidence are verified before runtime edits.
- Given W0–W6, when production handlers/stores and live sidecars execute, then every §3 ceiling passes and persisted semantic outcomes equal canonical replay; same-work identities remain stable.
- Given completion, when §10 validation runs, then affected suites and Release/package gates pass without unexpected skips, warnings, errors or leaked processes; environment failures remain unproven gates.

## Implementation Notes

Execution tasks are complete on frozen source65. Source proofs, guarded Current/tail delivery, fenced finalization and original-work replay recovery, compatible legacy/named/generic fallback, maintenance seams and cost diagnostics are implemented. The [coverage index](evidence/story-6-4/continuation-10-coverage-index.md) maps every matrix row, W0–W6 and section 6 boundary to executed assertions.

All five affected Debug and all five Release assemblies passed: each configuration selected 10,099 tests, passed 10,073 and declared 26 existing skips, with zero failures/errors/not-run. Exact focused Server/Domain/Live suites passed 548/110/40. All 14 manifest packages and owning-process cleanup were verified. The [final gate packet](evidence/story-6-4/continuation-10-final-gates.json) retains per-run source/assets, actual runtime profiles and intermediate failures/corrections.

Acceptance remains open: the pre-update exact Release build reports zero errors and ten warning occurrences from six pinned-package advisories. The user has explicitly authorized the five-pin exception recorded above. Apply it and refresh qualification evidence before judging the warning-free criterion or starting step-04 review. The earlier prerequisite, matrix and persisted-equality evidence remains bound to its original packages; it does not qualify the updated dependency set.

## Spec Change Log

- 2026-10-16: User replied “I authorize” to the exact five-pin root dependency update. Recorded the narrow exception above; no normative bytes, budgets, shared references or audit-policy changes are authorized. Revalidation is pending.

- 2026-10-16: Completed execution checklist and reconciled actual verification evidence. Approved intent, normative bytes and dependency policy remain unchanged; zero-warning acceptance and subsequent review remain open.

## Review Triage Log

## Design Notes

API shapes are additive implementation choices; no intent gaps remain. Migration/activation are operator work.

## Verification

Use every exact command in approved §10: individual Debug/source assemblies, complete affected regressions, real Dapr/Redis fixtures, then Release/package restore/build and suite execution. Assert persisted source/model/detail/index/receipt/checkpoint/lifecycle/retry evidence through owning seams.

Verified with .NET SDK 10.0.401/runtime 10.0.12. Regular live fixtures use pinned Dapr 1.18.2; the unchanged OQ8 fixture uses installed 1.18.7/shared controls, recorded separately. The [closing audit](evidence/story-6-4/continuation-10-final-audit.json) verifies frozen source/package/runtime identities, hygiene, sanitized evidence and released owned resources.

The exact Release command `dotnet build Hexalith.EventStore.slnx -c Release --no-restore -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -m:1` exits zero with ten NU1902/NU1903 warnings. Do not mark this story done or proceed to review until the approved zero-warning criterion is satisfied. Package and Contracts fixtures republish host outputs; restore/build coherent default-version hosts before whole Live execution, preserving each completed gate's actual assembly binding.
