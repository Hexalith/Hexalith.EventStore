---
project: eventstore
date: 2026-08-01
workflow: bmad-correct-course
mode: incremental
scope_classification: moderate
status: approved-and-applied
trigger: implementation-readiness-report-2026-08-01
incremental_edits_approved_by: Administrator
final_approved_by: Administrator
final_approved_on: 2026-08-01
---

# Sprint Change Proposal — Implementation Readiness Recovery

**Author:** Amelia (Developer) via `bmad-correct-course`
**Trigger status:** `NOT READY`
**Finding count:** 18 — 4 Critical, 6 Major, 4 Minor, and 4 documentation cautions
**Functional coverage:** 37/37 FRs (100%)
**Change scope:** Moderate planning correction; no top-level epic, MVP, runtime, release, deployment, or external-repository mutation
**Status:** APPROVED AND APPLIED

## 1. Issue Summary

The 2026-08-01 implementation-readiness assessment found every required planning artifact and complete functional coverage, with substantive PRD, UX, and architecture alignment. The plan is nevertheless not implementation-ready because four structural blockers make execution ordering or review scope unsafe:

1. Story 1.20 has a forward dependency on Epic 3 / Story 3.12.
2. Story 2.6 depends on later Story 2.11.
3. Story 4.8 is an epic-sized active story.
4. Story 8.2 is an epic-sized post-MVP story.

The assessment also found six Major issues: no story owns NFR2's reserved `system` tenant rule; Stories 3.5 and 3.11 have open-ended repository/family scope; Epic 7 and Story 7.14 are omnibus slices; Story 8.2 incorrectly treats `AGENTS.md` as package inventory; Story 2.10 and AD-18 name a stale registration API; and Epic 6 spec stories are enablers rather than runtime increments.

Four Minor issues and four documentation cautions concern BDD shape, copied requirement drift, reused story identifiers, non-measurable bounds, UX ownership wording, Architecture source traceability, stale UX metadata, and the intentionally absent UI performance budget.

The trigger is a planning and decomposition failure, not a failed implementation or changed product strategy. Completed and in-flight work remains valuable and must be preserved through evidence crosswalks.

### Additional Drift Confirmed During Analysis

- The canonical readiness SPEC permits the forward Story 1.20 → Story 3.12 dependency that this correction removes.
- The SPEC pins FrontComposer `3.2.2`, while Architecture and epics use the Builds-controlled `HexalithFrontComposerVersion`, currently `4.0.1`.
- SPEC traceability omits committed post-MVP FR37/NFR19.
- The July story-migration map predates the new decompositions and uses legacy/current Story 1.6 ambiguously.
- The payload-protection specification binds all implementation to omnibus Story 8.2 and therefore requires a new normative digest after decomposition.

## 2. Impact Analysis

### Epic Impact

| Epic | Impact | Disposition |
| --- | --- | --- |
| Epic 1 | Story 1.20's deployed evidence is incorrectly gated by later work. | Keep Epic 1 and Story 1.20 `done`; move deployed runtime parity closure to new Story 3.13. |
| Epic 2 | Story 2.6's presentation scope is coupled to later Story 2.11; Story 2.10 names a stale API. | Make 2.6 independently complete; retain 2.11 as production provenance proof; correct 2.10/AD-18 without reopening completed work. |
| Epic 3 | Needs deployed runtime parity closure and immutable audit boundaries. | Add 3.13; freeze 3.5 repository scope and 3.11 audit/family scope. |
| Epic 4 | Active Story 4.8 combines admission, fencing, retention, migration, production evidence, and closure. | Preserve 4.8 as an evidence ledger and replace active work with 4.9–4.15. |
| Epic 5 | NFR2 reserves `system`, but no story tests provisioning rejection. | Add focused backlog Story 5.10. |
| Epic 6 | Spec gates are counted like runtime value; Story 6.2's bound is qualitative. | Classify enablers explicitly and add a numeric snapshot-envelope invariant. |
| Epic 7 | One epic mixes five delivery classes; Story 7.14 combines three validation domains. | Keep Epic 7 as a program with five release tracks; split 7.14 into 7.14, 7.19, and 7.20. |
| Epic 8 | Story 8.2 spans the entire security platform and G5 closure. | Replace it with gated Stories 8.2–8.11; retain Epic 8 as committed post-MVP scope. |

No top-level epic is added, removed, reordered, or moved into or out of MVP scope.

### Artifact Impact

| Artifact | Required correction |
| --- | --- |
| `prd.md` | Update story traceability, SM4 decomposition evidence, NFR2 mapping, deployed-runtime boundary, and Epic 8 story references. Requirement prose remains authoritative here. |
| `epics.md` | Apply all story additions, decompositions, dependency corrections, bounded inventories, classifications, API wording, BDD conversion, and delivery tracks. Replace copied FR/NFR prose with ID/category references to the PRD. |
| `architecture.md` | Correct AD-18 registration, deployed-runtime story ownership, UI/UX sources, Story 8 package owner, and affected story references. |
| `ux.md` and canonical UX shard | State that `src/Hexalith.EventStore.Admin.UI` evolves in place; refresh metadata and preserve the no-performance-budget caution. |
| Canonical readiness SPEC package | Synchronize capabilities, gates, migrations, coverage, FrontComposer ownership/version source, all new story IDs, FR37/NFR19, and readiness success. |
| Payload-protection specification | Rebind normative responsibilities to Stories 8.2–8.11, recompute the normative digest, and retain `NOT AUTHORIZED`. |
| Story 8.1 artifact | Reference the decomposed sequence and recomputed digest; preserve incomplete approval/validation tasks. |
| Story 4.8 artifact | Retain as historical/evidence ledger and map completed versus unfinished tasks to children without false status inheritance. |
| Sprint status | Add the approved children/statuses, remove parent 4.8 from active tracking, and preserve all accepted completed/current states. |
| Story migration | Preserve the 2026-07-15 file and add a dated 2026-08-01 crosswalk. |

Historical readiness reports and previously approved sprint-change proposals remain unchanged. `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`, application code, deployment files, CI workflows, and `tools/release-packages.json` are not changed by this planning correction.

### Technical And Operational Impact

The correction itself changes no runtime behavior. It creates smaller future implementation/review units and aligns documentation with behavior already implemented for AD-18. Future stories retain their existing architecture, security, test, UX, release, and external-maintainer approval boundaries.

Existing Story 4.8 work is not discarded. Completed trusted-admission and digest-directory work moves to focused review children; unfinished work moves to implementation/backlog children. Epic 8 remains unauthorized and post-MVP.

## 3. Recommended Approach

### Selected — Direct Adjustment

- **Effort:** Medium. Multiple planning artifacts, two active story records, one content-digest-bound specification, and sprint tracking must move together.
- **Risk:** Medium. Incorrect status inheritance, stale story references, or a mismatched payload-spec digest could create false authorization.
- **Timeline:** Broad remaining Phase 4 work stays gated until the corrected planning set passes a fresh readiness assessment. Existing safe in-flight work is transitioned, not rolled back.
- **Sustainability:** Removes forward dependencies, gives each high-risk slice one review boundary, freezes audit scope, and restores one authority for requirement wording.

### Rejected — Rollback

Rollback is high effort and high risk. Completed Stories 1.20, 2.6, 2.10, 3.5, and 3.12 and completed Story 4.8 tasks contain valid implementation/evidence. Reverting them would not correct planning structure.

### Rejected — MVP Scope Review

The assessment found 37/37 functional coverage and substantive product/design alignment. No requirement or top-level epic is removed, and Epic 8 remains separately committed post-MVP scope. Product strategy escalation is not required.

## 4. Detailed Change Proposals

### 4.1 Remove the Epic 1 Forward Dependency

**OLD:** Story 1.20 may require Story 3.12 before Epic 1 can close.

**NEW:**

- Story 1.20 closes source/package parity within Epic 1 and remains `done`.
- Deployed image proof neither gates nor reopens Epic 1.
- Add Story 3.13, **Deployed Runtime Parity Closure**, covering FR36, NFR12, and NFR16.
- Story 3.13 depends backward on completed Stories 1.20 and 3.12 and maps source SHA to OCI index, child images/configs, package identities, and release provenance.
- Missing or mismatched evidence fails closed and cannot change Epic 1 status.
- Story 3.13 starts `backlog` and may enter `review` only through a focused evidence crosswalk; it never inherits `done` automatically.

### 4.2 Make Story 2.6 Independently Complete

**OLD:** Story 2.6 cites later Story 2.11 to complete its acceptance boundary.

**NEW:**

- Story 2.6 owns typed-client fixtures and UI-host/presentation behavior for `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, `LocalOnly`, and `Unknown`.
- It makes no claim about real-gateway provenance or persisted read-model evidence.
- Story 2.11 remains the later end-to-end owner for provenance preservation, authoritative classification, fail-closed `Unknown`, and production-path evidence.
- Existing accepted Tenants/Sally evidence satisfies the narrowed Story 2.6 boundary, so 2.6 remains `done`.

### 4.3 Decompose Active Story 4.8

**OLD:** Story 4.8 is one active story spanning durable admission through multi-host OQ8 closure.

**NEW:** Keep Story 4.8 as the historical/evidence ledger and create:

| Story | Scope | Initial status |
| --- | --- | --- |
| 4.9 Trusted Admission Contract And Protected Identity | Completed trusted adapter and opaque-key/leakage work (former Tasks 2–3). | `review` |
| 4.10 Digest Directory Rotation And Key Retirement | Completed directory, rotation, collision, and retirement work (former Task 4). | `review` |
| 4.11 Admission State Machine And Current-Fence Enforcement | State machine, current fence, and non-expiry replay/recovery outcomes. | `ready-for-dev` |
| 4.12 Expiry Compaction And Tombstone Retention | Inclusive expiry, replay retention, minimal tombstones, and compaction. | `backlog` |
| 4.13 Legacy Admission Migration And Fail-Closed Reconciliation | Legacy inventory, migration, collision/corruption handling, and unsafe-state blocking. | `backlog` |
| 4.14 OQ8 Multi-Host Production Evidence | Restart/failover and exactly-one eligible execution across two sidecars. | `backlog` |
| 4.15 OQ8 Platform Closure And Handoff | Final reviewed packet, documentation, and downstream handoff. | `backlog` |

Dependency chain: `4.9 → 4.10 → 4.11 → 4.12 → 4.13 → 4.14 → 4.15`.

Former Task 1 remains shared planning history. No unfinished task inherits completion. Focused review findings may be resolved without creating unrelated serial locks, but only Story 4.15 closes the OQ8 platform gate and Epic 4 remains `in-progress`.

### 4.4 Decompose Story 8.2

**OLD:** One post-MVP story owns contracts, cryptography, compatibility, key lifecycle, a real backend, server integration, packaging, Parties migration, rollback, and G5 approval.

**NEW:**

| Story | Scope |
| --- | --- |
| 8.2 | Payload-Protection Contracts And Golden Vectors |
| 8.3 | `pdenc-v2` Core Cryptographic Engine |
| 8.4 | Compatibility Readers And Mixed-History Routing |
| 8.5 | Policy And Key-Lifecycle Mechanics |
| 8.6 | Azure Key Vault Production Adapter Conformance |
| 8.7 | Server Persistence And Snapshot Integration |
| 8.8 | Package And Release Integration |
| 8.9 | Parties Dual-Provider Parity |
| 8.10 | Post-v2-Write Rollback Rehearsal |
| 8.11 | G5 Evidence And Approval Closure |

Dependency chain: `8.1 approval → 8.2 → 8.3 → (8.4 and 8.5) → 8.6 → 8.7 → 8.8 → 8.9 → 8.10 → 8.11`.

All implementation children start as unauthorized `backlog`. Story 8.11 alone closes G5/Epic 8. Parties retains its local provider until 8.9–8.11 complete. The payload specification remains `NOT AUTHORIZED` and Epic 8 remains post-MVP.

### 4.5 Add Reserved Tenant Acceptance Coverage

Add Story 5.10, **Reserved System Tenant Provisioning Guard**, covering NFR2:

- Reject any otherwise-valid tenant identifier that canonicalizes to `system`.
- Reject before persistence, actor creation, topic publication, configuration mutation, or downstream domain-service invocation.
- Return a stable support-safe validation error without tenant-existence leakage.
- Test `system`, normalization-equivalent inputs, valid nearby names, and zero state/downstream effects.
- Start `backlog`; Story 5.10 must complete before Epic 5 closes.

### 4.6 Correct AD-18 And Story 2.10

**OLD:** Routing is described as wired through `AddEventStoreGatewayClient(appId, apiToken?)`.

**NEW:**

- `AddEventStoreGatewayClient(...)` registers only the typed gateway client and command-status builder.
- Sidecar-routed hosts explicitly chain `AddEventStoreDaprServiceInvocation(appId, apiToken)` last so the handler remains innermost.
- Omission is documented as currently fail-open without compile-time/startup diagnostics.
- Structural tests require the platform extension and forbid local routing handlers.
- Story 2.10 remains `done` because source, registrations, replacement tests, and handler-order tests already implement the corrected contract.
- `project-context.md` remains unchanged because it is already correct.

### 4.7 Freeze Stories 3.5 And 3.11

Story 3.5's closed repository inventory is Builds, EventStore, Commons, FrontComposer, Memories, PolymorphicSerializations, and Tenants. Later-discovered repositories become named follow-up stories and do not reopen 3.5. Story 3.5 remains `done`.

Story 3.11 is bound to Builds catalog revision `9dc0fe1ffbf33269fddf195fd12317def86728f0`, 284 package entries, 139 classified families, and five changed rollback groups: IdentityModel, bUnit/AngleSharp, Aspire DAPR hosting, Scriban, and SonarAnalyzer. Later catalog rows are follow-up scope unless the owner explicitly supersedes and reruns the complete packet. Story 3.11 remains `awaiting-operator`.

### 4.8 Restructure Epic 7 And Story 7.14

Keep Epic 7 as a program with independently closable tracks:

- 7A Delivery semantics: 7.1
- 7B Admin trust and UX: 7.2–7.5, 7.14, 7.19, 7.20
- 7C Production operations: 7.6–7.9
- 7D Test evidence: 7.10–7.13
- 7E Planning backlog: 7.15–7.18

Replace Story 7.14's combined scope with:

- **7.14 Admin Shell And Canonical Route Migration:** evolve `Admin.UI` in place and migrate the closed ten-tab inventory (Overview, Commands, Streams & Events, Projections, Tenants & Access, Topology, Storage & Snapshots, Recovery, Deferred & Backlog, Settings), including every enumerated legacy route.
- **7.19 Admin Typed-Client And Evidence-State Integration:** depends on 7.2–7.5 and 7.14; typed clients only, canonical provenance/lifecycle states, evidence-confirmed mutations, support-safe/honest unavailable behavior.
- **7.20 Admin Accessibility, Localization, And Responsive Conformance:** depends on 7.19; WCAG 2.2 AA, keyboard/focus/live-region behavior, resource-backed localization, stable selectors, and the documented `>=1280`, `960–1279`, and `<960` viewport contracts.

All three start `backlog`; 7.15–7.18 remain `done`. No unsupported performance budget is added.

### 4.9 Correct Epic 6 Value Accounting And Snapshot Bound

- Stories 6.1, 6.3, and 6.5 are architecture/readiness enablers and do not count as runtime-capability completion.
- Stories 6.2, 6.4, and 6.6 are independently demonstrable runtime outcomes.
- Each enabler remains the authorization gate for its paired implementation.
- Epic 6 runtime value completes only when 6.2, 6.4, and 6.6 complete.
- Add NFR8 to Story 6.2.
- Story 6.1 must approve a numeric `MaxSnapshotEnvelopeOverheadBytes`.
- For identical folded state under the same schema/serializer, folded-state bytes are identical regardless of event count and `snapshot size <= folded-state size + approved maximum overhead`.
- Snapshots contain neither event-history collections nor nested prior snapshots.
- Test equivalent folded state across at least three snapshot intervals with materially different event counts.

### 4.10 Normalize Story 7.6 To BDD

Convert its eight numbered clauses into four `Given/When/Then` scenarios covering:

1. AppHost and canonical OpenBao component topology.
2. Production custody, TLS, bootstrap exception, and value-free configuration.
3. DAPR Secrets API retrieval, allowlists, fail-closed readiness, and support-safe diagnostics.
4. Structured topology tests, real DAPR-to-OpenBao evidence, deployment examples, and operator guidance.

Every existing obligation remains represented once; scope, dependencies, requirements, and status do not change.

### 4.11 Restore Requirement And Identifier Authority

- Keep complete FR/NFR wording only in `prd.md`.
- Replace copied prose in `epics.md` with a compact ID/category inventory and PRD authority pointer.
- Convert SPEC traceability to ID, PRD anchor, capability/gate, and story coverage.
- Validate exact FR1–FR37 and NFR1–NFR19 sets plus story-reference validity.
- Preserve historical reports/proposals as snapshots.
- Preserve `story-id-migration-2026-07-15.md` and add `story-id-migration-2026-08-01.md`.
- Qualify pre-July Story 1.6 as legacy Sample/Tenants adoption; bare current Story 1.6 means Projection And Domain Event Consumer Seams.
- Record 3.13, 4.9–4.15, 5.10, 7.19–7.20, and 8.2–8.11 with evidence/status rules in the August crosswalk.

### 4.12 Align UX And Architecture Metadata

- Replace “future EventStore UI service” with explicit in-place evolution of `src/Hexalith.EventStore.Admin.UI`.
- Preserve `eventstore-admin-ui` and `event-store-admin`; create no second host or duplicate pages.
- Rename the UX IA row to the actual Admin UI host.
- Update `ux.md`, `index.md`, `DESIGN.md`, and `EXPERIENCE.md` metadata to 2026-08-01.
- Add canonical UX documents to Architecture's source list.
- Retain the absence of a quantitative UI performance budget as a nonblocking documented caution.
- Continue to require FrontComposer and Blazor Fluent UI V5; no theme redefinition is authorized.

### 4.13 Correct Package Governance

- Story 8.8 owns package/release integration.
- `tools/release-packages.json` remains the authoritative release inventory.
- The manifest, project/solution inventory tests, metadata, SBOM/provenance, and package-only validation must agree.
- Preserve the payload specification's atomic 14-to-16 transition: both approved packages must exist and validate before inventory changes.
- Remove `AGENTS.md` from package acceptance criteria.
- Keep `AGENTS.md`, `CLAUDE.md`, and `.github/copilot-instructions.md` unchanged.
- Update Architecture and payload-spec story ownership from 8.2 to 8.8.

### 4.14 Synchronize The Canonical Readiness SPEC

Update `SPEC.md`, `glossary.md`, `readiness-gates.md`, and `requirements-traceability.md` to:

- remove the conditional forward dependency;
- describe Story 3.13's backward-only closure;
- include every approved story sequence and coverage mapping;
- use the Builds-controlled FrontComposer version (currently 4.0.1);
- add a dedicated post-MVP payload-protection capability for FR37/NFR19;
- correct seven epics to eight;
- describe all 37 FRs and 19 NFRs as the committed baseline while retaining Epic 8's post-MVP classification;
- reference both dated story crosswalks; and
- define readiness success as no ungoverned forward dependency or oversized active parent.

### 4.15 Rebind The Payload-Protection Specification

- Replace omnibus Story 8.2 references with ownership by Stories 8.2–8.11.
- Make approved Story 8.1 authorize only 8.2; every later child requires predecessor evidence.
- Rename the authorization field to cover the implementation sequence and retain `NOT AUTHORIZED`.
- Recompute the normative SHA-256 after the story-map edit; future approvals bind the new digest.
- Preserve the embedded golden-wrapper digest unless its actual bytes change.
- Update Story 8.1's artifact to reference the sequence and digest while preserving `in-progress` and its incomplete approval/validation tasks.
- Create no package, manifest, Parties, runtime, or provider mutation in this correction.

### 4.16 Sprint-Status Migration

| Story | Resulting status/action |
| --- | --- |
| 1.20, 2.6, 2.10, 3.5, 3.12 | Preserve `done`. |
| 3.11 | Preserve `awaiting-operator`. |
| 3.13 | Add as `backlog`; evidence crosswalk required before `review`. |
| Parent 4.8 | Remove from active tracking; retain its artifact as the evidence ledger. |
| 4.9–4.10 | Add as `review`. |
| 4.11 | Add as `ready-for-dev`. |
| 4.12–4.15 | Add as `backlog`. |
| 5.10 | Add as `backlog`. |
| 7.14 | Preserve `backlog` with narrowed scope. |
| 7.19–7.20 | Add as `backlog`. |
| 8.1 | Preserve `in-progress`. |
| 8.2–8.11 | Track as unauthorized `backlog`. |

All top-level epic statuses remain unchanged.

## 5. Implementation Handoff

### Classification And Ownership

This is a **Moderate** correction. Product Owner/Scrum Master coordination owns planning integration and sprint tracking. Named Architect, Security, UX, Developer, Test, Release, Operations, and external-maintainer roles retain the review boundaries already assigned to their stories.

### Ordered Handoff

1. Obtain final approval of this complete proposal.
2. Apply the approved edits atomically across PRD, epics, Architecture, UX, canonical SPEC, story ledgers, August crosswalk, and sprint status.
3. Recompute and verify the payload-protection normative digest; keep the implementation sequence `NOT AUTHORIZED`.
4. Validate requirement ID completeness, story-reference integrity, dependency direction, status migration, immutable audit inventories, UX source links, and absence of shared-entry-point edits.
5. Run a fresh `bmad-check-implementation-readiness` assessment against the corrected artifact set.
6. Resume broad remaining Phase 4 execution only after the new assessment reports no structural blocker or an explicitly approved disposition exists.

### Authorization Boundaries

Final approval of this proposal authorizes the planning-artifact correction and sprint-status migration only. It does not authorize commits, pushes, branches, package publication, registry mutation, release-manifest mutation, deployment changes, external-repository edits, provider provisioning, Parties migration, or Epic 8 implementation.

### Success Criteria

- All four Critical and six Major readiness findings are closed by explicit story/dependency/ownership changes.
- All four Minor findings and four documentation cautions have deterministic dispositions.
- Completed and in-flight evidence is preserved without false `done` inheritance.
- FR1–FR37 and NFR1–NFR19 remain completely traceable.
- No active story depends on a later epic/story to finish its own acceptance boundary.
- No active parent remains epic-sized.
- The payload-protection sequence remains post-MVP and unauthorized.
- A fresh readiness report replaces `NOT READY` with a blocker-free decision.

## Approval Record

The Administrator approved each detailed edit incrementally and then approved the consolidated proposal on 2026-08-01. That approval authorizes only the planning-artifact and sprint-status changes described here; all runtime, release, deployment, external-repository, and Epic 8 authorization boundaries remain in force.
