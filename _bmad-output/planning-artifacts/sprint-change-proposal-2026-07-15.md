---
title: Sprint Change Proposal - Implementation Readiness Structural Recovery
project: eventstore
date: 2026-07-15
status: approved
mode: batch
scope: major
trigger: _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-15.md
approval:
  approver: Administrator
  decision: approved
  evidence: "User response: always approve"
handoff:
  - Product Manager
  - Solution Architect
  - Product Owner
  - Developer
---

# Sprint Change Proposal - Implementation Readiness Structural Recovery

## 1. Issue Summary

The 2026-07-15 implementation-readiness assessment found the Phase 4 planning baseline **NOT READY** despite complete requirements coverage:

- 36 of 36 functional requirements are covered;
- 18 non-functional requirements are defined;
- architecture and UX intent are materially aligned;
- the canonical UX source already contains all relevant legacy information and requires no merge;
- 14 findings remain, including two structural blockers.

The two blockers are planning defects rather than product-scope or implementation-validity failures:

1. Epic 1 depends on the later Epic 2 Story 2.8 for platform query-response provenance before Stories 1.11 and 1.15 can complete.
2. Stories 1.3, 1.6, 2.4, 3.7, 5.6, 7.2, 7.3, and 7.4 bundle independently reviewable implementation slices.

The supporting evidence is the final readiness report, the current PRD, architecture, epics, canonical UX handoff, project documentation, and sprint status. The final CI documentation additionally proves that the Story 3.1/3.7 contradiction is stale planning text: live-sidecar tests already live in `tests/Hexalith.EventStore.Server.LiveSidecar.Tests`, while deterministic `Server.Tests` runs unfiltered.

### Problem Classification

This is a downstream planning-quality and sequencing correction caused by incomplete decomposition and drift between approved requirements, implementation stories, and current repository evidence. It is not a stakeholder scope change, strategic pivot, or failed technical architecture.

## 2. Impact Analysis

### Epic Impact

| Epic | Impact | Required action |
| --- | --- | --- |
| Epic 1 | Critical forward dependency and two oversized completed stories | Move the platform provenance prerequisite into Story 1.2; split Stories 1.3 and 1.6; renumber later stories and repair references. |
| Epic 2 | Oversized Tenants adoption story and mixed platform/consumer provenance ownership | Split Story 2.4; narrow current Story 2.8 to generated-API/Tenants consumption; renumber later stories. |
| Epic 3 | Oversized workflow story and contradictory live-sidecar selection language | Split Story 3.7; align Story 3.1 with the dedicated live-sidecar project and unfiltered deterministic suite. |
| Epic 4 | Story 4.7 lacks requirements and a complete external authority boundary | Add FR/NFR mappings, maintainer authority, exact-SHA evidence, and an objective completion rule. |
| Epic 5 | Story 5.6 is a four-surface coordinated slice | Split runtime component loading, production ACL/component parity, topology drift tests, and documentation. |
| Epic 6 | No structural correction required | Retain spec-first enabler classification and do not count spec stories as delivered runtime capability. |
| Epic 7 | Three oversized implementation stories and one four-product backlog umbrella | Split Stories 7.2-7.5; add a focused consolidated Admin UI migration story. |

No epic is obsolete. No new epic is needed. The seven-epic MVP structure and its ordering remain valid after story-level correction.

### Story And Sprint Impact

The replan changes story identities. A migration table is mandatory so active specs, sprint status, retrospectives, evidence packets, and review references remain auditable.

Completed parent stories do not automatically grant `done` to every new child. A child inherits `done` only when an evidence crosswalk names the existing implementation, focused tests, review result, and—where external Tenants work is involved—maintainer approval and exact SHA. Missing evidence places the child in `review`, not `done`.

Active migration minimums:

- current Story 1.14 becomes Story 1.19 and retains `review`;
- current Story 1.15 becomes Story 1.20 and retains `ready-for-dev`;
- current Story 2.8 becomes consumer-only Story 2.11;
- current Story 3.8 becomes Story 3.10;
- Epic 2 returns to `in-progress` if any Tenants split child lacks approval/SHA evidence.

### Artifact Conflicts

| Artifact | Decision |
| --- | --- |
| `prd.md` | No change. FR1-FR36, NFR1-NFR18, MVP, exclusions, and success intent remain authoritative. |
| `epics.md` | Major restructure: story splits, renumbering, provenance rehoming, deterministic criteria, traceability, and approval gates. |
| `architecture.md` | Add explicit EventStore UI ownership and FrontComposer dependency; make the projection dispatch result shape deterministic. |
| Canonical UX | No content change. `DESIGN.md` and `EXPERIENCE.md` already cover the required target experience. |
| Active story specs | Reissue affected active files under their new identifiers and preserve supersession/audit links. |
| `sprint-status.yaml` | Apply the approved migration only when the Product Manager/Architect replan is committed to `epics.md`; never update status ahead of the source plan. |
| Project documentation | No runtime documentation rewrite is required for the CI topology; the planning stories must be aligned to the already-current `docs/ci.md`. |

### Technical Impact

No code rollback is authorized by this proposal. The immediate technical impact is limited to planning and handoff artifacts. Subsequent implementation remains governed by the same architecture invariants, package inventory, DAPR topology, security posture, and persisted-evidence requirements.

## 3. Recommended Approach

### Selected Path: Direct Adjustment

Modify the existing epic plan without changing MVP scope:

1. Rehome platform provenance into Epic 1.
2. Split every oversized implementation story.
3. Repair deterministic acceptance criteria and traceability.
4. Name the Admin UI owner and FrontComposer boundary.
5. Migrate story IDs, active files, and sprint status through an explicit crosswalk.
6. Re-run implementation readiness after PM/Architect review.

### Alternatives Considered

**Potential rollback — rejected.** No evidence shows that completed code must be reverted. Rolling back would discard valid implementation while leaving the planning defects unresolved.

**MVP reduction — rejected.** The PRD remains achievable, all FRs are covered, and the blockers are decomposition/sequence problems. Removing scope would not fix story independence or contradictory acceptance criteria.

### Effort, Risk, And Timeline

- Planning/reconciliation effort: high.
- Immediate code effort: none.
- Implementation rollback risk: low.
- Reference/status migration risk: medium.
- Expected planning delay: approximately 2-4 focused working days for PM/Architect rewrite, evidence crosswalk, active-spec migration, and readiness rerun.
- Scope impact: none.
- Story-count impact: increases materially to produce reviewable units; this is intentional and consistent with PRD counter-metric SM-C1.

## 4. Detailed Change Proposals

### 4.1 Epic 1 Provenance Ownership

**Artifacts:** `epics.md`, affected Story 1.2/1.11/1.15 specs, sprint status.

**OLD**

- Story 2.8 owns the EventStore platform provenance contract and route-aware gateway enforcement.
- Story 1.11 waits for Story 2.8 before accepting lifecycle evidence.
- Story 1.15 requires Story 2.8 before parity closure.

**NEW**

- Story 1.2 owns route-stamped `ProjectionBacked`, `HandlerComputed`, and `Unknown` provenance, handler-route ETag/version/freshness suppression, projection-backed persisted evidence, typed-client propagation, and real-gateway-path tests.
- Current Story 2.8 becomes Story 2.11 and owns generated REST/Tenants consumer behavior only.
- Renumbered lifecycle Story 1.16 and parity Story 1.20 depend on Story 1.2.
- Handler-computed and unknown responses render `Unknown` and never claim projection-confirmed state.

**Rationale:** Query routing and metadata ownership are platform prerequisites. Generated REST and Tenants consumption correctly remain in the later external-integration epic.

### 4.2 Epic 1 Story Decomposition And Renumbering

| Existing story | New focused stories |
| --- | --- |
| 1.3 Generic Read Models And Query Cursors | 1.3 Persisted Read-Model Store And Write Policy; 1.4 Deterministic Read-Model Testing Fake; 1.5 Protected Query Cursor Codec |
| 1.6 Sample And Tenants Domain-Centric Adoption | 1.8 Sample Domain-Centric Adoption; 1.9 Tenants Query/Read-Model Adoption; 1.10 Tenants Projection/Event-Consumer Adoption; 1.11 Domain-Module Adoption Guardrails |

Subsequent migration:

| Old | New |
| --- | --- |
| 1.4 | 1.6 |
| 1.5 | 1.7 |
| 1.7 | 1.12 |
| 1.8 | 1.13 |
| 1.9 | 1.14 |
| 1.10 | 1.15 |
| 1.11 | 1.16 |
| 1.12 | 1.17 |
| 1.13 | 1.18 |
| 1.14 | 1.19 |
| 1.15 | 1.20 |

Every focused story carries its own owner, review boundary, acceptance criteria, and focused validation commands.

### 4.3 Epic 2 Tenants Decomposition

**OLD: Story 2.4** combines contracts, external host, UI, compatibility, package mode, and integration evidence.

**NEW**

| Story | Scope |
| --- | --- |
| 2.4 | Tenants REST contract metadata and route declarations |
| 2.5 | Dedicated external Tenants API host |
| 2.6 | Tenants UI client-library alignment and UX evidence |
| 2.7 | Compatibility, package-mode, and mixed-source-graph validation |

Subsequent migration: old 2.5 -> 2.8, old 2.6 -> 2.9, old 2.7 -> 2.10, and old 2.8 -> consumer-only 2.11.

Stories 2.4-2.7 cannot complete without a Tenants maintainer-approved PR/commit, exact Tenants SHA, repository-boundary evidence, source/package-mode validation, and recorded behavior when approval is unavailable.

### 4.4 Epic 3 CI/CD Decomposition And Contradiction Resolution

**OLD**

- Story 3.1 requires `Category!=LiveSidecar` filtering in `Server.Tests`.
- Story 3.7 requires unfiltered deterministic `Server.Tests` while live-sidecar tests run separately.
- Story 3.7 bundles workflow migration, validation, and supply-chain backlog.

**NEW**

- Story 3.1 names the final topology already documented in `docs/ci.md`: unfiltered deterministic `tests/Hexalith.EventStore.Server.Tests` plus dedicated `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` in the integration workflow.
- No `Category!=LiveSidecar` release filter is introduced.
- Story 3.7 becomes Shared Workflow Caller Migration.
- Story 3.8 becomes Workflow Reference And Validation Safety.
- Story 3.9 becomes Supply-Chain Publishing Backlog.
- Current Story 3.8 becomes Story 3.10.

### 4.5 Epic 5 Runtime Topology Decomposition

**OLD: Story 5.6** combines AppHost, production YAML/ACLs, tests, and deployment documentation.

**NEW**

| Story | Scope |
| --- | --- |
| 5.6 | AppHost component-loading and sidecar-argument parity |
| 5.7 | Production DAPR component, scope, key-prefix, and ACL parity |
| 5.8 | Runtime topology drift tests |
| 5.9 | Deployment and operator documentation alignment |

Each child retains deny-by-default posture and changes AppHost/DAPR topology only with aligned tests.

### 4.6 Epic 7 Decomposition

| Existing story | New focused stories |
| --- | --- |
| 7.2 | 7.2 Admin Claims Normalization; 7.3 State-Mutating Admin Audit; 7.4 Honest Deferred Operations; 7.5 Shared Typed Admin Client |
| 7.3 | 7.6 Secret-Store Configuration; 7.7 Readiness And DAPR App-Health; 7.8 DAPR Resiliency; 7.9 Immutable Production Images |
| 7.4 | 7.10 Integration CI Recovery; 7.11 Persisted-State Evidence And Read-Back Helpers; 7.12 Fake/Integration Reclassification; 7.13 Advisory And Performance Workflow Hygiene |
| 7.5 | 7.15 GDPR Backlog; 7.16 Admin OIDC Backlog; 7.17 Aggregate Test-Kit Backlog; 7.18 REST Generator Hardening Backlog |

New Story 7.14 owns the consolidated EventStore Admin dashboard migration.

The four existing backlog files are evaluated independently. Each new planning story becomes `done` only if its file satisfies scope, non-goals, dependencies, risks, and validation expectations; `draft` classification of the future capability itself is allowed.

### 4.7 Deterministic Acceptance Criteria

#### Story 1.1 canonical endpoints

**OLD:** `/process`, `/replay-state`, `/project`, and `/admin/operational-index-metadata`.

**NEW:** `/process`, `/replay-state`, `/query`, `/project`, and `/admin/operational-index-metadata`.

#### Renumbered asynchronous multi-projection story

**OLD:** “bounded per-projection result or equivalent versioned result.”

**NEW:** one named, versioned, bounded dispatch result containing one ordered entry per `(Domain, ProjectionType)`, a stable outcome code, and explicit checkpoint-advance state. No equivalent/alternative shape is accepted without a new architecture decision.

#### Renumbered scoped notification story

**OLD:** oversized metadata is “rejected or clipped”; tests cover unspecified “fail-open broadcast behavior.”

**NEW:** oversized detail metadata is rejected before detail broadcast and is never clipped. An already-authorized legacy signal-only notification may remain compatible, but it carries no detail metadata, follows existing validated group scope, and cannot bypass tenant/group authorization.

#### Renumbered generated API preflight story

**OLD:** persisted/read-model evidence is asserted “where available.”

**NEW:** the Sample smoke always asserts the persisted event and resulting read-model/query state. Missing evidence returns the distinct `state-evidence-failure` result.

#### Epic 7 alternatives

- “introduced or planned” becomes a dedicated typed-client implementation story.
- “where supported” app health becomes an explicit resource list and sidecar-argument test.
- “supported or preferred” immutable tags becomes production overlays referencing an immutable git-SHA/digest identity.
- “where applicable” persisted evidence becomes enumerated high-risk scenarios with required Redis/state-store/read-model/CloudEvent assertions.
- advisory jobs require a runnable trigger and either green evidence or a quarantine entry with owner, reason, and expiry.

### 4.8 Tenants Approval And Story 4.7 Authority

**OLD:** Stories 1.6 and 2.4 modify the Tenants submodule without the approval/SHA contract applied elsewhere; Story 4.7 has no requirement mapping or terminal authority rule.

**NEW:**

- All Tenants adoption children name the Tenants maintainer as external authority.
- Approval evidence includes PR/commit, exact SHA, accepted scope, source/package mode, and validation results.
- Without approval, generic EventStore platform work may complete, but the Tenants child remains `review` or `backlog`; the submodule is not silently modified.
- Story 4.7 covers FR15, NFR8, and NFR16.
- Story 4.7 completes only after an approved Tenants change and exact-SHA validation. Until then, Story 1.2 platform enforcement keeps affected routes `Unknown`, so Story 4.7 does not block EventStore platform provenance readiness.

### 4.9 NFR Inventory Synchronization

**OLD:** Epics NFR1 omits the PRD's explicit anonymous probe exception.

**NEW:** NFR1 exactly matches PRD section 7, including support-safe anonymous `/health`, `/alive`, and `/ready` endpoints and the prohibition on weakening fail-closed defaults. The epics introduction states that PRD section 7 is authoritative when copied requirement text drifts.

### 4.10 Architecture And UI Ownership

**OLD:** the architecture names `Admin.UI` and `eventstore-admin-ui` but does not state whether that project becomes the target EventStore UI service; FrontComposer is missing from the stack and dependency diagram.

**NEW:**

- `src/Hexalith.EventStore.Admin.UI` evolves in place into the consolidated EventStore UI service.
- The AppHost resource and container identity remain `eventstore-admin-ui`.
- No additional UI host is created.
- Legacy routes resolve as dashboard deep links or compatibility redirects while keeping the single **Event Store Admin** module entry selected.
- `Hexalith.FrontComposer.Shell` and `Contracts.UI`, plus Fluent UI V5, are explicit dependencies of the target UI.
- New Story 7.14 implements the migration and compatibility boundary.
- AD-19 names the exact versioned projection dispatch result instead of permitting an unspecified equivalent.

The readiness warning about quantitative UI performance is explicitly dispositioned as non-blocking: no unsupported numerical release gate is invented without a production baseline. A future UX-performance backlog item may establish measured budgets. This decision does not weaken accessibility, responsive layout, evidence-state, or support-safety requirements.

### 4.11 PRD And UX

No PRD or canonical UX edits are proposed. Their content remains authoritative and complete.

## 5. Implementation Handoff

### Classification

**Major** — the correction reorganizes story identities across six epics, changes sequencing and ownership, updates architecture, and requires coordinated migration of active specs and sprint tracking.

### Handoff Recipients

| Recipient | Responsibility |
| --- | --- |
| Product Manager | Own the approved epic/story rewrite, requirement mappings, story identity migration, and final scope integrity. |
| Solution Architect | Approve provenance ownership, AD-19 result shape, Admin UI owner/FrontComposer boundary, topology splits, and external authority gates. |
| Product Owner | Reconcile backlog order, statuses, superseded parents, and independent completion of the four backlog products. |
| Developer | Reissue affected active story specs, preserve audit links, attach existing code/test evidence to completed child stories, and avoid implementation changes outside approved child scope. |
| Test Architect | Verify focused validation commands, persisted-evidence requirements, live-sidecar lane topology, and child-story review boundaries. |
| UX Designer | Review Story 2.6 and Story 7.14 for client-only UI hosts, FrontComposer/Fluent governance, route compatibility, and honest evidence states. |

### Required Handoff Sequence

1. Rewrite `epics.md` using the approved mapping.
2. Update `architecture.md` and obtain Architect review.
3. Produce the story-ID migration/evidence crosswalk.
4. Reissue active story specs and retain supersession links.
5. Update `sprint-status.yaml` only after the source plan and active specs agree.
6. Run focused artifact consistency checks and implementation readiness again.
7. Do not authorize broad remaining Phase 4 implementation until the readiness result is READY or all residual warnings have explicit owner-approved dispositions.

### Success Criteria

- Epic 1 contains no dependency on a later epic.
- None of the eight oversized parent stories remains an active implementation story.
- Every child story has one owner, one review boundary, deterministic acceptance criteria, and focused validation.
- Story 1.1 covers `/query` or explicitly maps the endpoint elsewhere without traceability ambiguity.
- Story 3.1 and the CI documentation describe one identical test topology and command selection policy.
- Tenants changes cannot be marked complete without maintainer approval and exact SHA.
- Story 4.7 has requirement mappings and a terminal external approval rule.
- Epics NFR1 matches the PRD.
- Architecture names the EventStore UI project/resource and FrontComposer dependency.
- PRD and canonical UX remain unchanged.
- A fresh implementation-readiness assessment no longer reports either structural blocker.

## Appendix A - Checklist Completion

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | N/A | Trigger is the cross-artifact readiness assessment, not one implementation story. |
| 1.2 Core problem | Done | Planning decomposition, sequencing, and acceptance drift. |
| 1.3 Evidence | Done | Final readiness report plus planning, documentation, and sprint-status evidence. |
| 2.1 Current epic | N/A | Cross-epic trigger. |
| 2.2 Epic changes | Done | Modify Epics 1-5 and 7; no new epic. |
| 2.3 Future epic review | Done | All seven epics reviewed. |
| 2.4 Obsolete/new epics | Done | None. |
| 2.5 Order/priority | Done | Move provenance prerequisite to Epic 1; retain epic order. |
| 3.1 PRD conflict | Done | No PRD change. |
| 3.2 Architecture conflict | Done | UI ownership, FrontComposer, and AD-19 result shape require updates. |
| 3.3 UX conflict | Done | No canonical UX change. |
| 3.4 Other artifacts | Done | Active specs, sprint status, and readiness rerun required. |
| 4.1 Direct adjustment | Viable | High planning effort, medium migration risk, low rollback risk. |
| 4.2 Rollback | Not viable | No invalid implementation evidence. |
| 4.3 MVP review | Not viable | Scope remains achievable and fully covered. |
| 4.4 Recommended path | Done | Direct Adjustment. |
| 5.1 Issue summary | Done | Section 1. |
| 5.2 Epic/artifact impact | Done | Section 2. |
| 5.3 Path and rationale | Done | Section 3. |
| 5.4 MVP/action plan | Done | No MVP change; Section 5 sequence. |
| 5.5 Handoff plan | Done | Major-scope PM/Architect handoff. |
| 6.1 Checklist review | Done | All applicable items dispositioned. |
| 6.2 Proposal accuracy | Done | Cross-checked against active sources. |
| 6.3 User approval | Done | Administrator: “always approve”. |
| 6.4 Sprint-status update | Action-needed | Must follow the PM/Architect source-plan rewrite; updating it first would create a false plan/status mismatch. |
| 6.5 Next steps | Done | Defined in the handoff sequence and success criteria. |

## Approval Record

Administrator approved the batch proposals and instructed the workflow to treat remaining approval gates as approved by responding: **“always approve”**.

## Appendix B - Workflow Execution Log

| Field | Value |
| --- | --- |
| Workflow | `bmad-correct-course` |
| Execution date | 2026-07-15 |
| User | Administrator |
| Mode | Batch |
| Trigger | Implementation readiness assessment dated 2026-07-15: NOT READY |
| Evidence loaded | PRD, epics, architecture, canonical UX set, readiness report, project context, relevant project documentation, sprint status |
| Selected path | Direct Adjustment |
| Scope classification | Major |
| User decision | Approved — “always approve” |
| Artifacts modified by this workflow | This Sprint Change Proposal only |
| Unchanged by decision | PRD and canonical UX |
| Routed to | Product Manager and Solution Architect, with Product Owner, Developer, Test Architect, and UX Designer responsibilities defined in Section 5 |
| Handoff status | Complete: proposal, edit crosswalk, sequencing, success criteria, and next-step order recorded |
