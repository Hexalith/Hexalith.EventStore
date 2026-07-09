---
project: eventstore
date: 2026-07-09
workflow: bmad-correct-course
mode: batch
status: draft-pending-approval
trigger: Implementation readiness assessment reported NOT READY for broad Phase 4 implementation.
scope_classification: moderate
recommended_path: direct-adjustment
artifacts_reviewed:
  - _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-09.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/index.md
  - _bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - _bmad-output/implementation-artifacts/spec-1-3-generic-read-models-and-query-cursors.md
  - _bmad-output/implementation-artifacts/spec-1-6-sample-and-tenants-domain-centric-adoption.md
  - _bmad-output/implementation-artifacts/spec-2-2-rest-api-generator-discovery-and-controller-emission.md
  - _bmad-output/implementation-artifacts/spec-2-4-tenants-external-api-host-adoption.md
  - _bmad-output/implementation-artifacts/spec-3-7-shared-ci-cd-security-gates-and-supply-chain-backlog.md
  - _bmad-output/project-context.md
preserves_existing_file: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-09.md
---

# Sprint Change Proposal - Phase 4 Implementation Readiness Corrections

## 1. Issue Summary

The 2026-07-09 implementation-readiness assessment marked Phase 4 as **NOT READY** for broad implementation. The assessment did not find a PRD traceability failure: `prd.md` contains 35 functional requirements and 18 non-functional requirements, and `epics.md` maps every FR1-FR35.

The blocker is execution readiness:

- Epic 2 generated REST/API and Tenants UI/API work depends on query-response provenance behavior currently owned by later Story 4.7.
- Coordinated-slice stories are still too large unless implementation story files split them or carry the coordinated-slice gate exactly.
- Story 1.3 still has acceptance criteria that pull Tenants adoption into the read-model/cursor story, while the coordinated-slice gate moves Tenants adoption to Story 1.6.
- Story 4.7 mixes EventStore-owned provenance enforcement with a Tenants submodule approval dependency.
- The canonical UX handoff path is inconsistent: planning documents refer to `_bmad-output/planning-artifacts/ux.md`, while the actual final UX source is the sharded folder under `ux-designs/ux-eventstore-2026-07-05/`.
- Several stories need classification cleanup so planning, validation tooling, and runtime implementation are not conflated.

This proposal keeps the Phase 4 MVP scope intact and changes sequencing, classification, and document handoff only.

## 2. Change Analysis Checklist

| Item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering story | [x] Done | Trigger is the implementation-readiness report, not one implementation story. Primary affected stories: 1.3, 2.2, 2.4, 3.7, 4.7, 5.6, 6.1, 6.3, 6.5, 7.2, 7.3, 7.4, 7.5. |
| 1.2 Core problem | [x] Done | Issue type: execution-readiness and sequencing defect in planning artifacts. |
| 1.3 Evidence | [x] Done | Evidence is the readiness report plus source planning artifacts cited in front matter. |
| 2.1 Current epic impact | [x] Done | Epic 2 and Epic 4 need provenance re-sequencing. Epic 1 needs Story 1.3 boundary cleanup. Epics 3, 5, and 7 need slice-gate enforcement. Epic 6 needs gate classification. |
| 2.2 Epic-level changes | [x] Done | Add an earlier EventStore-owned provenance story, split/reclassify Story 4.7, and preserve coordinated-slice gates. |
| 2.3 Remaining epics | [x] Done | All future epics remain in scope; execution gates change before broad implementation. |
| 2.4 New/obsolete epics | [N/A] Skip | No new epic is required; story-level reorganization is enough. |
| 2.5 Epic order | [x] Done | Query provenance enforcement must move before dependent UI/generated API work is considered complete. |
| 3.1 PRD conflict | [x] Done | No FR/NFR scope change. PRD follow-on readiness section should reference the approved course correction after approval. |
| 3.2 Architecture conflict | [x] Done | AD-15 remains valid; ownership moves earlier. AD-14/AD-15 rules still govern metadata/provenance. |
| 3.3 UX conflict | [x] Done | Create the top-level UX handoff path expected by PRD, architecture, and epics. |
| 3.4 Other artifacts | [x] Done | `sprint-status.yaml` needs story status/ID changes after approval. |
| 3.5 Affected story files | [x] Done | Active rewrite gate applies to Story 3.7 and future generated story files. Completed Story 1.3 spec already excludes Tenants edits; the stale boundary is in `epics.md`. |
| 4.1 Direct adjustment | [x] Viable | Medium effort, lower risk than rollback or MVP reduction. |
| 4.2 Rollback | [N/A] Not viable | Completed Epic 1/2 work need not be reverted; the defect is planning/sequencing. |
| 4.3 MVP review | [N/A] Not viable | MVP scope is traceable and still valid. |
| 4.4 Recommended path | [x] Done | Direct adjustment with a mandatory story rewrite/readiness gate. |
| 5.1 Issue summary | [x] Done | Captured in this proposal. |
| 5.2 Impact and artifact needs | [x] Done | Captured below. |
| 5.3 Recommended path | [x] Done | Direct adjustment. |
| 5.4 PRD MVP impact | [x] Done | No scope reduction. |
| 5.5 Handoff plan | [x] Done | Moderate scope: Product Owner plus Developer, with Architect/UX review where noted. |
| 5.6 Story rewrite gate | [x] Done | Mandatory gate is defined in Section 4. |
| 6.1 Checklist review | [x] Done | Remaining action is explicit approval. |
| 6.2 Proposal accuracy | [x] Done | Draft pending user review. |
| 6.3 User approval | [!] Action-needed | Required before editing PRD, epics, sprint-status, or UX handoff. |
| 6.4 Sprint-status update | [!] Action-needed | Apply only after approval. |
| 6.5 Handoff plan | [x] Done | See Section 5. |

## 3. Impact Analysis

### Epic Impact

| Epic | Impact |
| --- | --- |
| Epic 1 - Domain Author Self-Service Platform | Story 1.3 acceptance criteria must stop pulling Tenants migration into the read-model/cursor story. Story 1.6 remains the Tenants adoption owner. |
| Epic 2 - External Integration Surfaces | Needs an EventStore-owned query provenance enforcement story before generated REST/UI consumers can be treated as implementation-ready. |
| Epic 3 - Release And Repository Reliability | Story 3.7 is already active and must carry the coordinated-slice gate before code-review handoff continues. |
| Epic 4 - Event Correctness And Recovery | Existing Story 4.7 should no longer own the prerequisite EventStore platform provenance enforcement. It should be split into a non-blocking Tenants follow-up or removed after the new earlier story is created. |
| Epic 5 - Security And Tenant Isolation | Story 5.6 stays in scope but must split or carry the coordinated-slice gate into its future implementation file. |
| Epic 6 - Bounded Cost And Event Evolution | Stories 6.1, 6.3, and 6.5 remain valid but should be explicitly classified as architecture/readiness gate stories, not runtime implementation progress. |
| Epic 7 - Operator Trust, Admin Honesty, And Future Capabilities | Stories 7.2, 7.3, and 7.4 must split or carry coordinated-slice gates. Story 7.5 stays planning/backlog-only. |

### Story Impact

| Story | Required correction |
| --- | --- |
| 1.3 | Remove Tenants adoption acceptance criteria from `epics.md`; keep production Tenants migration in Story 1.6. Completed implementation spec already says "Do not migrate Tenants". |
| 1.6 | No scope reduction; remains Sample and Tenants adoption owner. |
| 2.2 / 2.4 | Treat generated API/UI metadata consumers as dependent on the new earlier provenance enforcement story. Do not claim projection-backed current/stale evidence for handler-computed routes before that story is done. |
| 2.8 new | Add EventStore-owned "Query Response Provenance Contract And Route-Aware Gateway ETag" story. This absorbs the platform portion of current Story 4.7. |
| 3.7 | Active story file must carry coordinated-slice owner/review/validation gate before review proceeds. |
| 3.8 | Map to FR17/FR34/NFR16 as validation-enablement, or classify outside runtime implementation tracking. |
| 4.7 | Split: Tenants-submodule aliasing/conformance is a follow-up requiring maintainer approval and must not block EventStore platform provenance enforcement. |
| 5.6, 7.2, 7.3, 7.4 | Future implementation story files must either split the named slices or include the coordinated-slice gate verbatim. |
| 6.1, 6.3, 6.5 | Add explicit "Architecture/readiness gate" classification. |
| 7.5 | Keep planning/backlog-only classification and reflect it in sprint-status comments. |

### Artifact Conflicts

| Artifact | Conflict / gap | Required handling |
| --- | --- | --- |
| `epics.md` | Query Response Provenance Gate points Epic 2 reconciliation at later Story 4.7. | Add Story 2.8 as the prerequisite EventStore-owned provenance story; split/reclassify Story 4.7. |
| `epics.md` | Story 1.3 accepts Tenants migration despite the slice gate moving Tenants adoption to Story 1.6. | Rewrite Story 1.3 acceptance criteria. |
| `epics.md` | Coordinated-slice gate exists but is not yet enforced at story-file creation/review time. | Add explicit implementation-story gate language and require current/future story files to carry it. |
| `epics.md` / `sprint-status.yaml` | Story 3.8, Epic 6 spec stories, and Story 7.5 classification can be misread as runtime implementation progress. | Add classification metadata/comments. |
| `prd.md`, `architecture.md`, `epics.md` | They expect `_bmad-output/planning-artifacts/ux.md`; only sharded UX source exists. | Create top-level `ux.md` as a canonical handoff index that delegates to the final UX folder. |
| `sprint-status.yaml` | New story/reclassified story statuses are not represented. | Update after approval. |

### Technical Impact

No code changes are authorized by this proposal yet. After approval, implementation planning changes will alter story sequencing and story files. The most technical follow-on is the new provenance enforcement story, which will later touch gateway metadata handling, generated REST header behavior, and route-provenance tests.

## 4. Detailed Change Proposals

### Proposal A - Move EventStore Query Provenance Enforcement Earlier

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: `Query Response Provenance Gate`

OLD:

```markdown
Query-response provenance is governed by architecture invariant AD-15. No new UI or generated-API story that renders current/stale state or projection version may proceed until it cites AD-15 or explicitly avoids projection-backed evidence claims for handler-computed routes. Story 4.7 owns the platform contract and route-aware gateway enforcement; the shipped Epic 2 stories (2.2, 2.4) are reconciled by Story 4.7 guardrails.
```

NEW:

```markdown
Query-response provenance is governed by architecture invariant AD-15. Story 2.8 owns the EventStore platform contract and route-aware gateway enforcement before generated REST or UI consumers may claim current/stale projection evidence. No UI or generated-API story that renders current/stale state or projection version may proceed unless Story 2.8 is done, or the story explicitly renders handler-computed and unknown provenance as Unknown and avoids projection-backed evidence claims.

Story 4.7 no longer owns the EventStore platform prerequisite. Any Tenants producer aliasing fix that requires submodule maintainer approval is tracked as a non-blocking Tenants follow-up; until approved, affected Tenants routes must render Unknown rather than Current/Stale unless they source genuine projection-backed freshness.
```

Rationale: This removes the Epic 2 -> Epic 4 forward dependency without reducing AD-15 scope.

### Proposal B - Add Story 2.8 For EventStore-Owned Provenance Enforcement

Artifact: `_bmad-output/planning-artifacts/epics.md`

Location: after Story 2.7

NEW:

```markdown
### Story 2.8: Query Response Provenance Contract And Route-Aware Gateway ETag

**Requirements covered:** FR4, FR12, FR15, FR34, NFR8, NFR16 (EventStore-owned query-response provenance slice); governed by AD-14 and AD-15.

As a consumer of platform query metadata,
I want every query response to declare explicit route provenance and the gateway to stop attaching projection ETags to handler-computed responses,
So that generated REST and UI code never present a gateway ETag or fabricated version as projection-backed current/stale evidence.

**Acceptance Criteria:**

**Given** a response from a domain query handler (`HandlerAwareQueryRouter`, `ProjectionType` null)
**When** the gateway builds `QueryResponseMetadata`
**Then** provenance is `HandlerComputed`
**And** the gateway attaches no projection-actor ETag, projection version, or `IsStale` derived from `request.Domain`/`request.ProjectionType`
**And** a Tier 2/3 test asserts on the real gateway path that no projection ETag and no `X-Hexalith-Projection-Version`/`X-Hexalith-Is-Stale` header is emitted for the handler route.

**Given** a response from a projection actor / read model with persisted `IReadModelFreshness`
**When** the gateway builds `QueryResponseMetadata`
**Then** provenance is `ProjectionBacked`
**And** `ProjectionVersion`/`IsStale` are sourced from the persisted read model, never aliased from the ETag
**And** a Tier 2/3 test asserts the genuine version/freshness values traverse the gateway.

**Given** a consumer, including generated REST headers or UI freshness indicators
**When** provenance is `HandlerComputed` or `Unknown`
**Then** it renders `Unknown`, never `Current`/`Stale`, and does not claim projection-confirmed success.

**Given** a Tenants producer still aliases `ProjectionVersion := ETag`
**When** the EventStore-owned platform enforcement lands without submodule maintainer approval for the producer fix
**Then** the affected route is classified `HandlerComputed` or `Unknown`
**And** the Tenants producer aliasing fix is tracked as a separate maintainer-approved follow-up, not as a blocker for EventStore platform provenance enforcement.

**Sequencing note:** This story is a Phase 4 readiness blocker for generated REST/UI current-stale evidence. Implement before any new generated API or UI story claims projection-backed freshness.
```

Rationale: This preserves Story 4.7's useful platform substance but moves it to the earliest owning consumer epic.

### Proposal C - Split Or Reclassify Story 4.7

Artifact: `_bmad-output/planning-artifacts/epics.md`

Story: `4.7 Query Response Provenance Contract And Route-Aware Gateway ETag`

OLD:

```markdown
### Story 4.7: Query Response Provenance Contract And Route-Aware Gateway ETag

**Requirements covered:** FR4, FR15, FR34, NFR8, NFR16 (query-response provenance slice); governed by AD-14, AD-15.

...

**Given** a producer that aliases `ProjectionVersion := ETag` (current Tenants `TenantQueryResult`)
**When** the contract is enforced
**Then** the aliasing is removed or the route is classified `HandlerComputed`/`Unknown`
**And** guardrail coverage prevents reintroduction.
**Note:** the Tenants change is a submodule edit and requires explicit maintainer approval before it lands.
```

NEW:

```markdown
### Story 4.7: Tenants Query Provenance Follow-Up

**Classification:** Coordinated follow-up requiring Tenants submodule maintainer approval. This story is not the EventStore platform provenance prerequisite; that work is owned by Story 2.8.

As a platform maintainer coordinating with Tenants maintainers,
I want Tenants producer-side query freshness aliases removed or explicitly classified as non-projection-backed,
So that Tenants never presents an opaque ETag as projection version or current/stale evidence.

**Acceptance Criteria:**

**Given** Tenants producer code aliases `ProjectionVersion := ETag`
**When** maintainer-approved submodule work is scheduled
**Then** the aliasing is removed and genuine projection-backed freshness is sourced from persisted read-model evidence
**Or** the route is explicitly classified `HandlerComputed` or `Unknown` and consumers render Unknown.

**Given** maintainer approval is not yet available
**When** EventStore platform provenance enforcement ships through Story 2.8
**Then** EventStore blocks fabricated Current/Stale claims by route classification
**And** this Tenants follow-up remains visible without blocking the EventStore-owned platform story.
```

Rationale: EventStore can enforce safe provenance behavior without waiting on a submodule edit. The submodule fix remains visible and auditable.

### Proposal D - Rewrite Story 1.3 Boundary

Artifact: `_bmad-output/planning-artifacts/epics.md`

Story: `1.3 Generic Read Models And Query Cursors`

OLD:

```markdown
**Given** the read-model and cursor seams are adopted by a non-trivial domain
**When** existing Tenants-style read-model and cursor behavior is migrated
**Then** domain-specific RBAC, index, audit, and pagination semantics are preserved
**And** the hand-written state-store and cursor-codec infrastructure is removed.
```

NEW:

```markdown
**Given** the read-model and cursor seams are proven against non-trivial platform scenarios
**When** the implementation validates ETag-aware reads/writes, merge-on-write behavior, deterministic conflict injection, protected cursor encoding/decoding, invalid cursor rejection, and paging metadata propagation
**Then** the platform read-model and cursor contracts are proven without modifying the Tenants submodule
**And** production Tenants read-model/cursor adoption remains out of Story 1.3 and is owned by Story 1.6.
```

Rationale: The completed Story 1.3 implementation spec already says "Do not migrate Tenants or edit `references/Hexalith.Tenants`." `epics.md` should match that boundary.

### Proposal E - Enforce Coordinated-Slice Gate At Story-File Handoff

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: `Coordinated-Slice Gates For Oversized Stories`

OLD:

```markdown
The stories below may proceed in one of two ways:

- Split the story into the named implementation slices before creating implementation story files.
- Keep the story as a coordinated slice only if the named owner, review boundary, and validation commands are carried into the implementation story file.
```

NEW:

```markdown
The stories below may proceed in one of two ways:

- Split the story into the named implementation slices before creating implementation story files.
- Keep the story as a coordinated slice only if the named owner, review boundary, and validation commands are carried into the implementation story file.

Implementation handoff gate: a story file for any row in this table is not ready-for-dev or ready-for-review unless its active content includes the row's required slices/coordinated boundary, owner/review boundary, and validation commands. A superseded-scope note is insufficient if active acceptance criteria or tasks still instruct the abandoned design.
```

Rationale: This turns the existing mitigation into an enforceable handoff gate.

### Proposal F - Reclassify Story 3.8

Artifact: `_bmad-output/planning-artifacts/epics.md`

Story: `3.8 Generated API DAPR/Aspire Smoke Preflight`

OLD:

```markdown
**Requirements covered:** none directly (retro-driven developer tooling; safeguards FR17 live-sidecar and FR34 integration-evidence quality). Companion to Story 3.1. Re-homed 2026-07-07 from the defunct TEST-1.1.
```

NEW:

```markdown
**Requirements covered:** FR17 and FR34 validation enablement; governs NFR16 evidence quality. This is a validation/tooling story, not a runtime product capability. Companion to Story 3.1. Re-homed 2026-07-07 from the defunct TEST-1.1.
```

Rationale: This preserves the useful tooling story while restoring traceability discipline.

### Proposal G - Classify Epic 6 Spec Stories As Architecture Gates

Artifact: `_bmad-output/planning-artifacts/epics.md`

Stories: 6.1, 6.3, 6.5

OLD:

```markdown
### Story 6.1: Folded Snapshot Frozen Spec

**Requirements covered:** FR33
```

NEW:

```markdown
### Story 6.1: Folded Snapshot Frozen Spec

**Requirements covered:** FR33
**Classification:** Architecture/readiness gate. Completion authorizes Story 6.2 to start but does not count as runtime implementation progress.
```

Apply the same classification pattern to Story 6.3 and Story 6.5, replacing the dependent story number accordingly.

Rationale: The spec gates are valid, but velocity/reporting should not treat them as delivered runtime behavior.

### Proposal H - Preserve Story 7.5 As Planning/Backlog Only

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

OLD:

```yaml
7-5-track-future-capability-backlog: backlog
```

NEW:

```yaml
# Planning/backlog artifact story only; does not authorize runtime implementation of
# GDPR erasure, Admin OIDC login, aggregate test kit, or REST generator hardening.
7-5-track-future-capability-backlog: backlog
```

Rationale: `epics.md` already classifies Story 7.5 correctly. Sprint tracking should make the same distinction visible.

### Proposal I - Create Top-Level UX Handoff

Artifact: `_bmad-output/planning-artifacts/ux.md`

OLD:

```markdown
No top-level `_bmad-output/planning-artifacts/ux.md` file exists.
```

NEW:

```markdown
# UX Handoff - Hexalith.EventStore Phase 4

Status: final
Updated: 2026-07-09

This file is the canonical top-level UX handoff expected by `prd.md`,
`architecture.md`, and `epics.md`.

The final UX source is the sharded artifact rooted at:

- `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/index.md`

Canonical UX documents:

- `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md`

`DESIGN.md` and `EXPERIENCE.md` win on conflict with mockups, screenshots,
validation artifacts, older review findings, archived UX exports, or legacy
`Admin.UI` behavior.

Readiness note: this handoff exists to satisfy the top-level artifact path
required by the PRD, architecture, and epic plan while preserving the sharded
UX source as the detailed canonical contract.
```

Rationale: Creating the expected handoff file is lower-risk than editing every reference and avoids future readiness scanners reporting UX missing.

### Proposal J - Update Sprint Status After Approval

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

OLD:

```yaml
  epic-2: in-progress
  2-7-outbound-dapr-routing-header-ownership: ready-for-dev

  epic-4: backlog
  4-7-query-response-provenance-contract-and-route-aware-gateway-etag: backlog
```

NEW:

```yaml
  epic-2: in-progress
  2-7-outbound-dapr-routing-header-ownership: ready-for-dev
  # Phase 4 readiness correction: EventStore-owned query provenance enforcement
  # moved earlier to remove the Epic 2 -> Epic 4 forward dependency.
  2-8-query-response-provenance-contract-and-route-aware-gateway-etag: backlog

  epic-4: backlog
  # Coordinated Tenants follow-up only; EventStore platform prerequisite is Story 2.8.
  4-7-tenants-query-provenance-follow-up: backlog
```

Rationale: Sprint tracking must match the re-sequenced story ownership.

## 5. Mandatory Story Rewrite Gate

This proposal triggers the Correct-Course Story Rewrite Gate because it supersedes active story assumptions and changes story sequencing.

### Gate Rule

Implementation or review cannot proceed for an affected story unless the active story file contains the current acceptance criteria, current task boundaries, and current validation gates. A superseded-scope banner alone is not enough if active ACs/tasks still instruct the abandoned design.

### Affected Story Files

| Story file | Status | Required rewrite / validation |
| --- | --- | --- |
| `_bmad-output/implementation-artifacts/spec-1-3-generic-read-models-and-query-cursors.md` | Done | No file rewrite required. The file already excludes Tenants migration. `epics.md` must be rewritten so future readers do not pull Tenants work back into Story 1.3. |
| `_bmad-output/implementation-artifacts/spec-1-6-sample-and-tenants-domain-centric-adoption.md` | Done | No file rewrite required. It remains the production Tenants adoption owner. |
| `_bmad-output/implementation-artifacts/spec-2-2-rest-api-generator-discovery-and-controller-emission.md` | Done | No retroactive rewrite required. Future generated REST story/review work must cite Story 2.8/AD-15 before claiming Current/Stale projection evidence. |
| `_bmad-output/implementation-artifacts/spec-2-4-tenants-external-api-host-adoption.md` | Done | No retroactive rewrite required. Future Tenants API/UI evidence must render handler-computed or unknown provenance as Unknown unless Story 2.8 and genuine projection-backed metadata are present. |
| `_bmad-output/implementation-artifacts/spec-3-7-shared-ci-cd-security-gates-and-supply-chain-backlog.md` | Active/review | Must carry the coordinated-slice gate for Story 3.7: shared workflow caller migration, workflow reference/cache validation, supply-chain publishing backlog, owner/review boundary, and validation commands. Code review should halt if those are missing from active content. |
| Future Story 2.8 file | Not created | Must contain the new EventStore-owned provenance ACs from Proposal B before implementation starts. |
| Future Story 4.7 file | Not created | Must be Tenants follow-up only, not EventStore platform provenance prerequisite. |
| Future Story 5.6, 7.2, 7.3, 7.4 files | Not created | Each must split into named slices or include the coordinated-slice row verbatim with owner, review boundary, and validation commands. |

## 6. Recommended Approach

Recommended path: **Direct Adjustment**.

This is a moderate planning correction. It does not require code rollback or MVP reduction. It requires re-sequencing one story, adding one story, reclassifying several planning/validation stories, adding a UX handoff file, and updating sprint-status after approval.

Effort estimate: Medium.

Risk level: Medium before the story rewrite gate is enforced; Low after `epics.md`, `sprint-status.yaml`, the active Story 3.7 file, and `ux.md` are corrected.

Rejected options:

- Rollback completed Epic 1/2 work: not justified. The completed implementation files mostly align with the safer boundaries; the defects are in planning and sequencing.
- MVP scope reduction: not justified. FR/NFR traceability is strong and no requirement is invalidated.
- Leave Story 4.7 in Epic 4 and rely on a note: not sufficient. It preserves the readiness scanner's Epic 2 -> Epic 4 forward dependency.

## 7. Implementation Handoff

Scope classification: **Moderate**.

Handoff recipients:

| Recipient | Responsibility |
| --- | --- |
| Product Owner / PM | Approve story re-sequencing, add Story 2.8, split/reclassify Story 4.7, and preserve MVP scope. |
| Developer agent | Apply approved edits to `epics.md`, create `ux.md`, update `sprint-status.yaml`, and patch active Story 3.7 if its content is missing the coordinated-slice gate. |
| Architect | Review Story 2.8 and Story 4.7 split against AD-14/AD-15 and NFR8/NFR16. |
| UX Designer | Confirm top-level `ux.md` handoff points to the canonical `DESIGN.md` and `EXPERIENCE.md` documents and does not resurrect stale UX review findings. |

Success criteria:

- `epics.md` no longer has Epic 2 dependent on later Story 4.7 for query provenance.
- Story 1.3 no longer instructs Tenants migration.
- Story 2.8 exists and owns EventStore platform query provenance enforcement.
- Story 4.7 is Tenants follow-up only or otherwise non-blocking.
- Coordinated-slice implementation story files either split or carry required gates.
- `_bmad-output/planning-artifacts/ux.md` exists as the top-level UX handoff.
- `sprint-status.yaml` matches story ownership and classification.
- Readiness can be re-run without the four immediate blockers: forward dependency, oversized story handoff gap, Story 1.3 boundary conflict, and missing top-level UX path.

## 8. Approval

This proposal is **pending approval**.

Approval question:

Do you approve this Sprint Change Proposal for implementation? Accepted responses: `yes`, `no`, or `revise`.
