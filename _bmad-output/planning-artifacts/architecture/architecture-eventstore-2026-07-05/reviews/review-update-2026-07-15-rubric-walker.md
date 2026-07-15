# Reviewer Gate - 2026-07-15 Architecture Update Rubric Walker

## Gate Verdict

**CHANGES REQUIRED.** The updated spine correctly fixes the two architecture changes directed by the approved 2026-07-15 proposal—AD-19 selects a single versioned dispatch contract and deterministic checkpoint reconciliation, and AD-21 fixes consolidated UI ownership—but the spine still does not state the PRD's load-bearing FR36 consumer-removal gate as an enforceable rule. One medium proposal-reconciliation ambiguity and one medium technology-pin gap should also be closed before handoff.

## Scope And Evidence

Reviewed:

- `_bmad-output/planning-artifacts/architecture.md` (updated 2026-07-15)
- `_bmad-output/planning-artifacts/prd.md` (updated 2026-07-13)
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-15.md` (approved)
- `_bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05/.memlog.md`
- Focused brownfield evidence for projection dispatch, Admin UI identity, Aspire resource identity, and FrontComposer package/source availability

Mechanical lint was executed directly against the discoverable final mirror because the run workspace has no `ARCHITECTURE-SPINE.md`. The mirror passed with zero findings: no placeholders, duplicate/non-monotonic AD IDs, missing Binds/Prevents/Rule fields, or blank Stack versions.

## Critical Findings

None.

## High Findings

### H1 - FR36's consumer-removal approval and runtime-pin invariant did not land in an AD

- **Evidence:** PRD FR36 requires an owner-reviewed parity packet, an approved EventStore runtime SHA, and exact consumer-checkout SHA matching *before* a consuming module deletes local projection/query infrastructure (`prd.md:196-204`). The spine frontmatter claims FR36, and the capability map associates it with several ADs (`architecture.md:361`), but no Rule states that removal gate. The only consumer migration rule in AD-19 concerns synchronous projection compatibility (`architecture.md:246`), not parity approval or exact-SHA authority.
- **Why this is load-bearing:** Two consuming modules built independently from the spine can choose incompatible migration thresholds—one can delete local infrastructure after API availability, while another waits for production-path evidence, owner approval, and exact runtime pinning. This is the precise divergence FR36 exists to prevent. A capability-map reference is traceability, not an enforceable invariant.
- **Disposition:** **Autofix.** Add the next stable decision (recommended `AD-22`) or amend an existing applicable Rule without renumbering: a consumer may remove local projection/query infrastructure only after the parity packet proves every required capability through production paths, the owning maintainer approves it against an exact EventStore runtime SHA, and the consumer checkout matches that SHA. Bind FR36 and prevent premature or cross-version removal. Keep story IDs and sequencing in `epics.md`.

## Medium Findings

### M1 - AD-19's exact result contract and the proposal's “checkpoint-advance state in the result” wording need one authoritative reconciliation

- **Evidence:** The approved proposal requires one named, versioned, bounded dispatch result containing an ordered route entry, stable outcome code, and explicit checkpoint-advance state (`sprint-change-proposal-2026-07-15.md:234-239`). AD-19 freezes `/project/v2` as `ProjectionDispatchResponse` / `ProjectionDispatchOutcome`, but the serialized outcome shape has no named checkpoint-advance member; instead the server records a separate `Advance` / `DoNotAdvance` reconciliation decision derived from status and legacy-write success (`architecture.md:239-245`). The memlog explicitly says this server-owned interpretation supersedes earlier wording that placed `CheckpointAdvanced` on the response.
- **Assessment:** The resulting runtime rule is deterministic and fail-closed: `Completed`/`AlreadyCompleted` can advance only after required durable work; all other or invalid conditions do not advance. It therefore prevents the stated checkpoint divergence. The remaining defect is source reconciliation: a literal implementer of the approved proposal can still expect checkpoint state inside each result entry.
- **Disposition:** **Discuss, then autofix the source of truth.** Prefer preserving the already-frozen v2 wire shape and amend/add a proposal reconciliation note that “explicit checkpoint-advance state” is the server-owned per-route reconciliation record, not a v2 response member. If the proposal literally requires a wire member, AD-19 must instead select a new contract version; do not mutate v2.

### M2 - The newly named FrontComposer release dependency is not pinned precisely enough for a build substrate

- **Evidence:** AD-21 and the dependency diagram require direct use of `Hexalith.FrontComposer.Shell` and `Hexalith.FrontComposer.Contracts.UI` (`architecture.md:254-258`, `164-168`). The Stack row says only “root-declared source in Debug; centrally pinned NuGet packages in Release” (`architecture.md:296`). Brownfield package props currently pin `Hexalith.FrontComposer.Shell` through `HexalithFrontComposerVersion = 3.1.1`, but do not contain a `Hexalith.FrontComposer.Contracts.UI` package entry. The existing Admin UI project does not yet reference either FrontComposer project/package.
- **Why it matters:** Story 7.14 can otherwise choose a direct source-only reference, rely on Shell's transitive dependency, or invent a release package pin, producing Debug/Release graph drift under AD-11.
- **Disposition:** **Autofix.** Pin the intended FrontComposer version/commit boundary in the Stack and state the direct Debug project-reference / Release package-reference rule for both dependencies, including the central `Contracts.UI` package pin that implementation must add. If `Contracts.UI` is deliberately transitive-only in Release, say so and remove the direct-dependency implication.

## Low Findings

### L1 - The reviewer-gate workspace lacks its canonical spine filename

- **Evidence:** `lint_spine.py --workspace .../architecture-eventstore-2026-07-05` cannot find `ARCHITECTURE-SPINE.md`; only the top-level `architecture.md` mirror and `.memlog.md` remain.
- **Disposition:** **Autofix/process.** Restore or generate the canonical workspace spine during finalization, or change the gate to accept the final mirror explicitly. This does not change the semantic verdict; direct lint of the mirror passed.

## Positive Compliance Evidence

### AD-19 exact dispatch result

- Names one route and one frozen wire family: additive `/project/v2`, `ProjectionDispatchResponse`, version 2, and bounded `ProjectionDispatchOutcome` entries.
- Fixes the outcome cap at 32, ordinal `ProjectionType` ordering, and exactly one outcome per admitted `(Domain, ProjectionType)` route.
- Freezes a closed numeric status set: `Completed = 0`, `AlreadyCompleted = 1`, `Retryable = 2`, `Indeterminate = 3`, `Failed = 4`.
- Makes checkpoint reconciliation fail closed and preserves independent durable sibling success under partial failure.
- Rejects “equivalent” result shapes without a new version and architecture decision.
- Ratifies the brownfield v2 contract types and dispatch/coordinator implementation rather than inventing a conflicting wire shape.

### AD-21 consolidated UI ownership

- Names the owner unambiguously: `src/Hexalith.EventStore.Admin.UI` evolves in place.
- Preserves the existing AppHost resource and container identity `eventstore-admin-ui` and forbids a parallel EventStore UI host.
- Fixes FrontComposer Shell, Contracts.UI, and Fluent UI V5 as the composition boundary.
- Preserves legacy routes through dashboard deep links or redirects under one selected **Event Store Admin** module entry.
- Retains the typed-client boundary and prohibition on per-message MVC controllers.
- Ratifies the existing project/resource/container identity in the brownfield repository.

## PRD Input Reconciliation

The architecture maps all FR1-FR36 and NFR1-NFR18 areas, and the 2026-07-15 changes preserve the PRD's unchanged scope. One load-bearing PRD requirement did not survive distillation as a Rule: **FR36's owner-approved parity packet plus exact runtime-SHA consumer-removal gate** (H1).

No other load-bearing PRD requirement was found missing at feature-spine altitude:

- route-bound lifecycle/provenance is fixed by AD-14/AD-15;
- async named projection delivery and paged replay equivalence are fixed by AD-19/AD-20;
- external API/UI boundaries are fixed by AD-3/AD-4/AD-21;
- state mutation, persisted evidence, fail-closed security, topology parity, release mode, and spec-first cost/evolution are fixed by AD-5 through AD-13 and AD-16 through AD-18;
- story splitting, renumbering, exact acceptance commands, and sprint migration remain correctly deferred to `epics.md` and sprint planning.

## Good-Spine Checklist

| Check | Result | Notes |
| --- | --- | --- |
| Fixes real divergence points one level down | **Fail** | H1 leaves the consumer-removal threshold outside the spine. AD-19 and AD-21 otherwise converge the proposal-directed slices. |
| Misses no owned divergence point | **Fail** | FR36 approval/runtime pin is an owned cross-repository compatibility gate. |
| Every AD Rule is enforceable and prevents its stated divergence | **Pass with clarification** | AD-19 is enforceable, but M1 requires source reconciliation so “checkpoint state” has one meaning. |
| Deferred contains no divergence-producing architecture decision | **Pass** | Story identity/sequence and quantitative UI budgets are correctly downstream; deployment provider values have invariant guardrails. |
| Named technology is verified-current and pinned | **Partial** | Local pins/source fit are current for the brownfield graph, but the new Contracts.UI Release pin is absent (M2). |
| Ratifies rather than contradicts brownfield reality | **Pass with implementation seed** | AD-19 and UI identity ratify existing code; FrontComposer composition is an approved target change, not yet current project wiring. |
| Covers the driving PRD/proposal capabilities | **Fail** | Proposal-directed AD-19/AD-21 changes landed; FR36 is traced but not ruled (H1). |
| Inherited parent invariants remain intact | **N/A** | No parent spine is inherited for this run. |
| Every feature-altitude dimension is decided, deferred, or open | **Pass** | Paradigm, boundaries, dependencies, mutation, shared state, security, topology/environments, operations, release, testing evidence, UI, and evolution are represented. |

## Recommended Gate Closure Order

1. Encode FR36 as an enforceable stable AD without importing story numbering.
2. Reconcile the proposal's checkpoint-state wording with AD-19's frozen v2/server-owned decision.
3. Pin the FrontComposer Shell/Contracts.UI Debug and Release dependency boundary.
4. Re-run mechanical lint from a workspace containing the canonical spine filename.

## Re-review 2026-07-15

**Verdict: PASS.** No critical or high findings remain. Direct mechanical lint of the updated top-level spine again reports zero findings.

- **H1 / PRD FR36 — resolved:** AD-22 now makes removal of consumer-local projection/query infrastructure conditional on an EventStore-owner-reviewed parity packet, persisted production-path evidence, an exact approved EventStore runtime commit SHA, and an exact consumer-checkout match. The no-approval/no-match behavior is explicit and fail-closed.
- **M1 / AD-19 — resolved:** AD-19 preserves the frozen `/project/v2` wire envelope and selects one separately versioned normalized server result, `ProjectionDispatchResult` v1. Each bounded ordered route entry now contains the stable outcome and explicit `ProjectionCheckpointAdvanceState`; `Advanced` is permitted only after required durable work, legacy write where applicable, and checkpoint save. Invalid, failed, or missing evidence normalizes to `NotAdvanced`, eliminating the prior source ambiguity without mutating v2.
- **M2 / FrontComposer — resolved at architecture altitude:** AD-21 and the Stack pin matching Shell and Contracts.UI version `3.2.2`, require equivalent Debug-source and Release-package boundaries, fix one module identity and route owner, and require the pins to move together. The eventual package/project wiring remains implementation work, no longer an architectural choice.
- **L1 / workspace filename — remains process-only:** the run folder still lacks `ARCHITECTURE-SPINE.md`, but this does not affect the semantic gate; the discoverable final mirror is mechanically clean.
