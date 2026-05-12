---
project: Hexalith.EventStore
date: 2026-05-12
source_report: implementation-readiness-report-2026-05-12.md
status: implemented
scope: moderate
recommended_path: direct-adjustment
approval_status: approved-and-implemented
---

# Sprint Change Proposal: Implementation Readiness Artifact Cleanup

## 1. Issue Summary

The implementation readiness assessment completed on 2026-05-12 found the planning set at **NEEDS WORK**.

The trigger is not a product requirement failure. The PRD, UX specification, and epic coverage are broadly coherent, and all 104 PRD functional requirements are represented in `epics.md`. The blocker is handoff quality: downstream developer agents cannot safely rely on the current planning artifact set because architecture validation is stale, Epics 14-21 are not represented as detailed epic sections in `epics.md`, Epic 21 remains framed as an active technical migration gate even though sprint status marks it complete, and several admin story fragments are structurally misplaced.

Evidence:

- `implementation-readiness-report-2026-05-12.md` reports 104/104 FR coverage, but overall status **NEEDS WORK**.
- `epics.md` has detailed `## Epic` sections for Epics 1-13 and Epic 22, while Epics 14-21 only appear in the Epic List area.
- `epics.md` embeds Stories 15.9, 15.11, and 15.12 immediately after the Epic 15 list entry, before the detailed epic section sequence begins.
- `architecture.md` still opens and validates against "47 FRs across 8 categories" and "32 NFRs across 5 categories", while the current PRD contains 104 FRs and 46 NFRs.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` marks Epics 14-21 and their stories as `done`, while Epic 22 remains `backlog`.

## 2. Impact Analysis

### Epic Impact

Epics 14-20 are not missing from implementation history. They have completed story files under `_bmad-output/implementation-artifacts` and `sprint-status.yaml` marks each epic as `done`. The issue is that `epics.md` does not expose them as detailed, auditable epic sections.

Epic 21 is also marked `done`, but `epics.md` still contains an active-sounding migration gate: "Complete Epic 21 before starting any new UI stories." This creates false blocking risk for Epic 22 and future admin work.

Epic 22 is the only active backlog epic in the relevant range. It depends on Epics 16 and 20, and it should not begin broad implementation until the planning artifact clearly proves those dependencies are completed and discoverable.

Story 22.7 is too broad for a single implementation story. Its scope spans payload/snapshot protection, key invalidation, restored-backup safety, unreadable data behavior, redaction across every operational surface, replay/rebuild behavior, admin APIs, UI, CLI, MCP, and test artifacts.

### Artifact Conflicts

`prd.md`: No functional requirement changes are required. The MVP and post-MVP scope remain valid.

`epics.md`: Requires structural cleanup:

- Add FR Coverage Map rows for FR83-FR104.
- Replace the range-only Epic 22 coverage with row-level auditability.
- Move Epic 15 story fragments out of the Epic List area and into a proper detailed Epic 15 section or an explicit completed-story index.
- Add detailed/index sections for Epics 14-21, preferably as completed-history sections linking to implementation artifacts rather than duplicating every story body.
- Reframe Epic 21 as completed migration history, not an active gate.
- Split Story 22.7 into smaller stories before assignment.

`architecture.md`: Requires an alignment amendment:

- Update requirements overview from 47 FRs / 32 NFRs to 104 FRs / 46 NFRs.
- Add coverage validation for query, SignalR, admin, CLI, MCP, and public gateway/downstream integration scope.
- Explicitly validate UX-DR41 through UX-DR59 or classify them as story-level acceptance criteria backed by existing ADR-P4/P5 constraints.

`ux-design-specification.md`: No change required. It already defines the admin UX detail, including UX-DR41 through UX-DR59.

`sprint-status.yaml`: No immediate status change required. It already records Epics 14-21 as `done` and Epic 22 as `backlog`. After approval, add a short comment or planning sync entry that points to this cleanup proposal.

## 3. Recommended Approach

Use **Direct Adjustment**.

This is a moderate planning-artifact repair, not a scope reset and not a rollback. The implementation record already contains the missing admin and migration story artifacts. The safest path is to make the planning documents accurately reflect completed work, then split the one oversized active story before implementation.

Effort estimate: Medium.

Risk level: Medium if Epic 22 starts before cleanup; Low after cleanup.

Timeline impact: Short planning pause before Epic 22 execution. No PRD MVP change.

Alternatives considered:

- Rollback: Not viable. Completed admin and migration work should not be reverted just because the planning document is poorly indexed.
- PRD/MVP review: Not required. Requirements coverage is complete; the failure is auditability and handoff readiness.

## 4. Detailed Change Proposals

### Change 1: Expand FR Coverage Map for FR83-FR104

Artifact: `epics.md`

Section: `FR Coverage Map`

OLD:

```markdown
| FR82 | 15 | Observability deep links |
```

NEW:

```markdown
| FR82 | 15 | Observability deep links |
| FR83 | 22.1 | API-facing command/query DTOs and stable ProblemDetails extension names |
| FR84 | 22.1 | High-level EventStore client methods for command/query/status/replay/read paths |
| FR85 | 22.1 | Deterministic gateway fakes and builders in EventStore.Testing |
| FR86 | 22.1 | Package ownership documentation for Contracts, Client, Testing, and runtime internals |
| FR87 | 22.2 | Projection adapter or documented generic query actor contract |
| FR88 | 22.2 | Get/List/Search domain query routing through POST /api/v1/queries |
| FR89 | 22.2 | Generic versus domain-specific projection actor guidance |
| FR90 | 22.3 | Gateway tenant lifecycle, membership, role, and permission validation |
| FR91 | 22.3 | Hexalith.Tenants tenant/RBAC validator adapters with fail-closed behavior |
| FR92 | 22.3 | Stable 401/403 ProblemDetails taxonomy |
| FR93 | 22.4 | Query paging, filtering, blank search, and deterministic ordering policy |
| FR94 | 22.4 | Query response metadata contract |
| FR95 | 22.4 | Query error taxonomy |
| FR96 | 22.5 | Durable at-least-once published event guarantees and ordering notes |
| FR97 | 22.5 | Pub/sub deployment matrix, metadata, retry, drain, and dead-letter policy |
| FR98 | 22.5 | Backend-specific publish/order/dead-letter tests |
| FR99 | 22.6 | Stream read/replay APIs for projection rebuild |
| FR100 | 22.6 | Operator-safe projection rebuild flows |
| FR101 | 22.6 | Projection rebuild documentation using public APIs |
| FR102 | 22.7a | Payload and snapshot protection hooks |
| FR103 | 22.7c | Crypto-shredding and restored-backup safety workflows |
| FR104 | 22.7d | Protected-data redaction across logs, admin APIs, UI, CLI, MCP, ProblemDetails, replay, rebuild, and tests |
```

Rationale: Coverage exists in prose, but row-level traceability removes downstream audit ambiguity.

### Change 2: Convert Epics 14-21 Into Completed Detailed Index Sections

Artifact: `epics.md`

Section: after `## Epic 13`, before active `## Epic 22`

OLD:

```markdown
## Epic 13: Documentation & Developer Onboarding
...
## Epic 22: Public Gateway and Downstream Integration Contracts
```

NEW:

```markdown
## Epic 14: Admin API Foundation & Abstractions

Status: Completed. See `_bmad-output/implementation-artifacts/sprint-status.yaml`.

Completed stories:

- 14.1 Admin abstractions service interfaces and DTOs: `_bmad-output/implementation-artifacts/14-1-admin-abstractions-service-interfaces-and-dtos.md`
- 14.2 Admin server DAPR-backed service implementations: `_bmad-output/implementation-artifacts/14-2-admin-server-dapr-backed-service-implementations.md`
- 14.3 Admin server REST API controllers with JWT auth: `_bmad-output/implementation-artifacts/14-3-admin-server-rest-api-controllers-with-jwt-auth.md`
- 14.4 Admin server Aspire resource integration: `_bmad-output/implementation-artifacts/14-4-admin-server-aspire-resource-integration.md`
- 14.5 Admin API OpenAPI spec and Swagger UI: `_bmad-output/implementation-artifacts/14-5-admin-api-openapi-spec-and-swagger-ui.md`

## Epic 15: Admin Web UI - Core Developer Experience

Status: Completed. See `_bmad-output/implementation-artifacts/sprint-status.yaml`.

Completed stories:

- 15.1 Blazor shell, Fluent UI layout, command palette, dark mode
- 15.2 Activity feed and recently active streams
- 15.3 Stream browser command/event/query timeline
- 15.4 Aggregate state inspector and diff viewer
- 15.5 Projection dashboard status, lag, errors, controls
- 15.6 Event type catalog searchable registry
- 15.7 Health dashboard with observability deep links
- 15.8 Deep linking and breadcrumbs
- 15.9 Commands page cross-stream command list
- 15.10 Admin UI data pipeline fixes
- 15.11 Persistent state store and command activity
- 15.12 Events page cross-stream browser
- 15.12a Missing timeline endpoint
- 15.12b Missing event detail endpoint
- 15.13 Stream activity tracker writer

## Epic 16: Admin Web UI - DBA Operations
Status: Completed. Link completed stories 16.1-16.7.

## Epic 17: Admin CLI (`eventstore-admin`)
Status: Completed. Link completed stories 17.1-17.8.

## Epic 18: Admin MCP Server
Status: Completed. Link completed stories 18.1-18.5.

## Epic 19: Admin - DAPR Infrastructure Visibility
Status: Completed. Link completed stories 19.1-19.6.

## Epic 20: Admin - Advanced Debugging
Status: Completed. Link completed stories 20.1-20.5.

## Epic 21: Admin UI Fluent UI v5 Stability Migration
Status: Completed historical migration. This is not an active gate for new UI stories.
Link completed stories 21.0-21.13 and the related sprint change proposals.

## Epic 22: Public Gateway and Downstream Integration Contracts
```

Rationale: This keeps `epics.md` useful as an implementation handoff document while avoiding large duplication of already completed story files.

### Change 3: Move Epic 15 Embedded Story Fragments

Artifact: `epics.md`

Section: `Epic List`

OLD:

```markdown
### Epic 15: Admin Web UI -- Core Developer Experience
...
### Story 15.9: Commands Page -- Cross-Stream Command List with Filters
...
### Story 15.11: Persistent State Store and Command Activity Tracking
...
### Story 15.12: Events Page -- Cross-Stream Event Browser
...
### Epic 16: Admin Web UI -- DBA Operations
```

NEW:

```markdown
### Epic 15: Admin Web UI -- Core Developer Experience
Blazor Fluent UI shell with activity feed, stream browser, aggregate state inspector, projection dashboard, event type catalog, health dashboard with observability deep links, command palette, and dark mode.
**FRs covered:** FR68, FR69, FR70, FR71, FR73, FR74, FR75
**NFRs covered:** NFR41, NFR45
**Also:** UX-DR34-DR49
**Dependencies:** Epic 14

### Epic 16: Admin Web UI -- DBA Operations
```

Then place Stories 15.9, 15.11, 15.12, and their follow-up notes under the completed `## Epic 15` section or replace them with links to their implementation artifacts.

Rationale: The Epic List should remain a summary. Detailed story content belongs in detailed epic sections or linked implementation artifacts.

### Change 4: Reframe Epic 21 as Completed Historical Work

Artifact: `epics.md`

Section: `Epic 21`

OLD:

```markdown
### Epic 21: Fluent UI Blazor v4 -> v5 Migration
...
**MIGRATION GATE:** Complete Epic 21 before starting any new UI stories
```

NEW:

```markdown
### Epic 21: Admin UI Fluent UI v5 Stability Migration
The admin UI remains stable, accessible, and testable on Fluent UI Blazor v5. This completed migration preserved existing admin workflows while updating package, component, layout, theme, dialog, toast, DataGrid, and CSS token usage.
**Status:** Completed historical migration
**Outcome:** New UI stories may proceed using Fluent UI v5 patterns already established in the completed Epic 21 implementation artifacts.
```

Rationale: Sprint status marks Epic 21 complete. Keeping an active-sounding gate in the live epic list creates false sequencing risk.

### Change 5: Split Story 22.7 Before Implementation

Artifact: `epics.md`

Section: `Story 22.7`

OLD:

```markdown
### Story 22.7: Payload Protection, Snapshot Encryption, and Crypto-Shredding Safety
```

NEW:

```markdown
### Story 22.7a: Payload and Snapshot Protection Hooks

As a platform owner handling sensitive data,
I want payload and snapshot protection extension points with explicit metadata,
So that protected state can be persisted, published, rehydrated, replayed, and rebuilt without exposing protected content.

### Story 22.7b: Unreadable Protected Data Behavior

As an operator,
I want missing, invalidated, or inconsistent protection keys to produce explicit safe behavior,
So that live reads, replay, rebuild, backup restore, and admin inspection fail closed without corrupting state or leaking data.

### Story 22.7c: Crypto-Shredding Workflow and Restored-Backup Safety

As a platform owner,
I want key deletion/invalidation and restored-backup checks to be documented and tested,
So that GDPR deletion workflows remain safe after backup restore and operational recovery.

### Story 22.7d: Protected Data Redaction Across Operational Surfaces

As a security reviewer,
I want logs, ProblemDetails, admin APIs, Web UI, CLI, MCP, replay, rebuild, backup validation, and test artifacts to avoid protected payload disclosure,
So that diagnostics stay useful without leaking protected data.
```

Rationale: The current Story 22.7 is too broad for one developer agent and has too much blast radius for reliable acceptance.

### Change 6: Amend Architecture Coverage Summary

Artifact: `architecture.md`

Sections: `Requirements Overview`, `Requirements Coverage`

OLD:

```markdown
#### Functional Requirements: 47 FRs across 8 categories
#### Non-Functional Requirements: 32 NFRs across 5 categories
...
#### Functional Requirements: 47/47 covered
#### Non-Functional Requirements: 32/32 covered
```

NEW:

```markdown
#### Functional Requirements: 104 FRs across 13 categories
#### Non-Functional Requirements: 46 NFRs across 6 categories
...
#### Functional Requirements: 104/104 covered or intentionally delegated
#### Non-Functional Requirements: 46/46 covered or intentionally delegated
```

Add coverage rows for:

- Query pipeline and ETag caching: FR50-FR64, NFR35-NFR39
- Administration tooling: FR68-FR82, NFR40-NFR46
- Public gateway and downstream integration contracts: FR83-FR104
- SignalR and Blazor refresh patterns: FR55-FR60
- Admin UX architecture validation: UX-DR41-UX-DR59, anchored by ADR-P4 and ADR-P5 where architectural, and by story acceptance criteria where interaction-level

Rationale: The architecture decisions are mostly present, but the validation math and coverage summary are stale and undermine handoff confidence.

## 5. Checklist Result

| Checklist Item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering story | Done | Trigger is readiness assessment, not a single implementation story. |
| 1.2 Core problem | Done | Planning artifact hygiene and implementation handoff quality failure. |
| 1.3 Evidence | Done | Report plus `epics.md`, `architecture.md`, and `sprint-status.yaml`. |
| 2.1 Current epic impact | Done | Epic 22 should remain backlog until cleanup completes. |
| 2.2 Epic-level changes | Action-needed | Add completed index sections for Epics 14-21; split 22.7. |
| 2.3 Remaining epics | Done | Primary future impact is Epic 22. |
| 2.4 Obsolete/new epics | Done | No new product epic needed; Epic 21 should be historical. |
| 2.5 Order/priority | Done | Cleanup before Epic 22 broad implementation. |
| 3.1 PRD conflicts | Done | No PRD scope change required. |
| 3.2 Architecture conflicts | Action-needed | Update stale 47/32 validation to 104/46. |
| 3.3 UX conflicts | Done | UX is strong; validation traceability needs linking. |
| 3.4 Other artifacts | Action-needed | Add a planning sync comment to sprint status after approval. |
| 4.1 Direct adjustment | Viable | Recommended. |
| 4.2 Rollback | Not viable | Completed work should not be reverted. |
| 4.3 PRD MVP review | Not viable | Requirements scope is intact. |
| 4.4 Path selection | Done | Direct adjustment. |
| 5.1-5.5 Proposal components | Done | Covered in this document. |
| 6.1-6.2 Final review | Done | Proposal is internally consistent and actionable. |
| 6.3 Approval | Done | Jerome approved implementation in the current thread on 2026-05-12. |
| 6.4 Sprint status update | Done | Epic 21 gate wording was retired and Story 22.7 was split into four Epic 22 backlog entries. |
| 6.5 Handoff | Done | Planning artifacts now route Epic 22 implementation through smaller stories and completed admin epic indexes. |

## 6. Implementation Handoff

Scope classification: **Moderate**.

Route to:

- Product Owner / Developer agent: repair `epics.md`, expand FR83-FR104 map, move Epic 15 fragments, add completed index sections for Epics 14-21, split Story 22.7.
- Architect: amend `architecture.md` requirement overview and validation coverage for 104 FRs, 46 NFRs, admin UX, and public gateway/downstream integration.
- Developer agent: after planning cleanup approval, execute the artifact edits and run markdown validation/readiness rerun.

Success criteria:

- `epics.md` has a clean structure: Requirements Inventory, FR Coverage Map through FR104, Epic List summaries, detailed/index sections for all epics, active Epic 22 split into implementable stories.
- Epic 21 no longer appears as an active migration gate.
- `architecture.md` no longer contains stale 47/32 coverage claims.
- `sprint-status.yaml` remains the status source of truth and agrees with the planning document.
- Re-running implementation readiness moves the planning set from **NEEDS WORK** to **READY** or leaves only explicitly accepted warnings.

## 7. Approval and Implementation Result

Jerome approved this proposal on 2026-05-12, and the planning artifact cleanup was implemented.

Implemented updates:

- `epics.md`: expanded the FR Coverage Map through FR104, removed misplaced Epic 15 story fragments from the Epic List, added completed index sections for Epics 14-21, reframed Epic 21 as completed historical migration work, and split Story 22.7 into 22.7a-22.7d.
- `architecture.md`: updated stale 47 FR / 32 NFR overview and validation sections to the current 104 FR / 46 NFR baseline, including query, SignalR, admin, public gateway, replay, and protection scope.
- `sprint-status.yaml`: retired the active Epic 21 migration gate wording and replaced the single 22-7 backlog entry with four smaller backlog stories.
