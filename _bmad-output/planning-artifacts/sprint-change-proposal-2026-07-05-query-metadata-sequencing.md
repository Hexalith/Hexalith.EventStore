---
project: eventstore
date: 2026-07-05
generated_at: 2026-07-05T18:44:44+02:00
workflow: bmad-correct-course
mode: batch
status: approved
trigger:
  report: _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-05.md
  readiness: needs_work
scope_classification: moderate
artifacts_reviewed:
  - _bmad-output/project-context.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/ux.md
  - _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-05.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-query-metadata-propagation.md
  - docs/reference/query-api.md
  - docs/operations/query-operational-evidence.md
  - docs/brownfield/api-contracts.md
  - docs/brownfield/integration-architecture.md
artifacts_updated:
  - _bmad-output/planning-artifacts/epics.md
approval: approved
approved_by: Administrator
approved_at: 2026-07-05T18:48:29+02:00
---

# Sprint Change Proposal - Query Metadata Sequencing Correction

## 1. Issue Summary

The 2026-07-05 implementation readiness assessment returned **NEEDS WORK** even though the PRD, architecture, UX, and epics artifacts exist and FR coverage is 100%.

The blocker is story sequencing quality. Story 7.6, **Query Metadata Propagation Contract And Gateway Evidence**, implements behavior that earlier stories already depend on:

- Story 1.2 requires `QueryResponseMetadata` propagation for domain query-handler routing.
- Story 1.3 requires authoritative paging metadata and safe cursor behavior.
- Story 2.2 requires generated REST actions to map ETag, `304`, freshness, projection version, degraded/warning state, and paging metadata.
- Story 2.4 requires Tenants generated REST/UI evidence to be backed by the real platform query metadata path.
- Story 7.5 currently carries a scheduling rule for Story 7.6 even though Story 7.5 is classified as a backlog-artifact story.

Evidence comes from `_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-05.md`, especially CRITICAL-1 and MAJOR-2. The prior approved query metadata proposal, `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-query-metadata-propagation.md`, correctly defined the contract, but the later epics document placed the implementation story too late.

## 2. Impact Analysis

### Epic Impact

| Epic | Impact |
| --- | --- |
| Epic 1 - Domain Author Self-Service Platform | Must own platform query metadata propagation, gateway merge policy, client typed result exposure, and authoritative paging/cursor metadata before Stories 1.2 and 1.3 close. |
| Epic 2 - External Integration Surfaces | Must own generated REST metadata/header forwarding and Tenants proof evidence in Stories 2.2 and 2.4 instead of waiting for Epic 7. |
| Epic 7 - Operator Trust, Admin Honesty, And Future Capabilities | Should no longer own the general query metadata platform contract. Story 7.5 should stay focused on backlog artifacts; Story 7.6 should be deleted after its ACs are redistributed. |

No epic is obsolete. No MVP scope reduction is required. The correction changes sequencing and ownership only.

### Story Impact

Affected stories:

- Story 1.2: add the platform metadata propagation and gateway merge/freshness-policy scope.
- Story 1.3: tighten paging metadata and cursor/problem-detail evidence.
- Story 2.2: add generated REST support-safe metadata headers and `304` proof backed by gateway metadata.
- Story 2.4: keep Tenants metadata proof but make it depend on the earlier Epic 1/Epic 2 slices, not Story 7.6.
- Story 7.5: remove the Story 7.6 scheduling acceptance criterion.
- Story 7.6: delete as a separate Epic 7 story after its behavior is redistributed.

### Artifact Conflicts

PRD: no FR/NFR scope change is required. The PRD already includes FR4, FR12, FR15, and NFR8 query metadata requirements.

Architecture: no architecture rewrite is required. AD-14 already defines the platform metadata invariant and merge rules.

UX: no UX rewrite is required. `ux.md` already requires projection-confirmed, support-safe state and treats SignalR/HTTP acceptance as evidence-pending until query/read-model evidence exists.

Epics: `epics.md` must change. This is the blocking artifact.

Docs: `docs/reference/query-api.md` should remain cautious until implementation lands. It should be updated by the implementation story, not by this planning correction.

### Technical Impact

No code rollback is recommended. No runtime implementation starts from this proposal alone.

The later implementation still touches Contracts, Server/Gateway, Client, generated REST, and tests, but those implementation concerns should now be owned by earlier dependent stories instead of a late Epic 7 story.

## 3. Recommended Approach

Recommended path: **Direct Adjustment**.

Delete Story 7.6 as a standalone Epic 7 story and redistribute its acceptance criteria into the earlier stories that already depend on the behavior. This repairs the forward dependency without renumbering the seven epics, without reducing MVP scope, and without creating a new late prerequisite gate.

Effort estimate: **Medium planning refinement**.

Risk level: **Low to Medium**. The main risk is losing part of Story 7.6 during redistribution. Mitigate by copying each acceptance theme into one owning story and rerunning implementation readiness immediately after the epics edit.

Alternatives considered:

- Keep Story 7.6 and mark Stories 1.2, 1.3, 2.2, and 2.4 blocked until it lands. This is less disruptive, but still leaves a future-story dependency in the plan.
- Move Story 7.6 wholesale into Epic 1 with a new story number. This avoids Epic 7 ownership but either creates awkward numbering or forces broad renumbering.
- Rollback or reduce MVP scope. Not useful; the requirements are valid and already in scope.

## 4. Detailed Change Proposals

### 4.1 Add Query Metadata Sequencing Rule

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: `Implementation Readiness Execution Gates`

OLD:

```markdown
The 2026-07-05 implementation readiness assessment found complete FR traceability but identified story-quality gates that must be closed before broad Phase 4 implementation starts.
```

NEW:

```markdown
The 2026-07-05 implementation readiness assessment found complete FR traceability but identified story-quality gates that must be closed before broad Phase 4 implementation starts.

### Query Metadata Sequencing Gate

The platform-owned query metadata propagation contract must be implemented by the earliest stories that depend on it, not by a later Epic 7 story.

- Story 1.2 owns platform result metadata propagation, gateway merge rules, freshness policy enforcement, and typed client metadata exposure.
- Story 1.3 owns authoritative paging metadata, cursor opacity, invalid cursor handling, and read-model/end-state evidence for paging.
- Story 2.2 owns generated REST query metadata headers, `304` behavior, and safe problem-detail behavior.
- Story 2.4 owns Tenants API/UI proof against the real platform query metadata path.
- Story 7.5 remains backlog-artifact work only.

Story 7.6 is deleted after the acceptance criteria above are redistributed into the owning earlier stories.
```

Rationale: makes sequencing explicit and gives readiness review a single place to verify ownership.

### 4.2 Story 1.2 - Domain Query Handler Routing

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: `Story 1.2: Domain Query Handler Routing`

OLD:

```markdown
**Given** query routing is tested
**When** focused unit tests execute
**Then** domain-side dispatch, operational metadata capture, handler-aware routing, fallback behavior, and `QueryResponseMetadata` propagation are verified
**And** the implementation remains backward compatible with projection-actor query routing.
```

NEW:

```markdown
**Given** a domain query handler or projection actor returns query metadata
**When** the result crosses `QueryResult`, `QueryRouterResult`, `SubmitQueryResult`, `SubmitQueryResponse`, and `EventStoreQueryResult`
**Then** `QueryResponseMetadata` is preserved additively through each platform type
**And** the gateway no longer drops domain-produced freshness, projection version, paging, warning, or degraded-state metadata.

**Given** the gateway creates HTTP response metadata
**When** domain metadata and gateway metadata both exist
**Then** metadata is merged by explicit rules: domain/projection evidence wins for freshness, projection version, paging, degraded state, and warnings; gateway ETag header value wins for the HTTP validator; gateway fills `ServedAt` only when absent; `IsNotModified` is set by the HTTP outcome.

**Given** freshness metadata is unavailable
**When** a query response is returned or `RequireFresh` / `MaxStaleness` is requested
**Then** freshness is represented as unknown, not current
**And** freshness-dependent requests fail closed according to the existing `query_projection_stale` taxonomy instead of silently treating unknown freshness as current.

**Given** query routing is tested
**When** focused unit tests execute
**Then** domain-side dispatch, operational metadata capture, handler-aware routing, fallback behavior, metadata propagation, gateway merge behavior, typed client metadata exposure, and backward-compatible projection-actor routing are verified.
```

Rationale: puts the platform metadata contract in the first story that requires it.

### 4.3 Story 1.3 - Generic Read Models And Query Cursors

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: `Story 1.3: Generic Read Models And Query Cursors`

OLD:

```markdown
**And** successful paged responses can return `QueryPagingMetadata` with effective page size, offset or next cursor, total count when known, and has-more evidence without exposing cursor internals.
```

NEW:

```markdown
**And** successful paged responses can return authoritative `QueryPagingMetadata` with effective page size, offset or next cursor, total count when known, and has-more evidence without exposing cursor internals
**And** request paging echoed by the gateway is not treated as proof of total count, next cursor, or page completeness unless the query handler or projection produced that metadata.

**Given** cursor or paging inputs are invalid, malformed, wrong-scope, oversized, tampered, or expired after key rotation
**When** the query path rejects them
**Then** the response uses support-safe validation/problem details
**And** tests prove cursors remain opaque and are not parsed, logged, displayed as support text, or treated as ordering proof.
```

Rationale: Story 1.3 already owns read-model and cursor behavior, so it should own paging evidence rather than relying on Story 7.6.

### 4.4 Story 2.2 - REST API Generator Discovery And Controller Emission

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: `Story 2.2: REST API Generator Discovery And Controller Emission`

OLD:

```markdown
**And** it maps success, `304`, ETag, freshness, projection version, served-at, degraded/warning state, paging metadata, not-found, forbidden, and validation outcomes consistently with gateway query semantics.
```

NEW:

```markdown
**And** it maps success, `304`, ETag, freshness, projection version, served-at, degraded/warning state, paging metadata, not-found, forbidden, and validation outcomes consistently with gateway query semantics.

**Given** a generated external API action receives `EventStoreQueryResult.Metadata`
**When** it returns `200` or `304`
**Then** it forwards canonical support-safe headers for ETag, projection version, served-at, stale state, degraded state, warning codes, and bounded paging evidence only when values are present and bounded
**And** no generated controller relies on payload-specific fields to decide projection-confirmed state.

**Given** generator tests validate query metadata behavior
**When** generated query actions are exercised
**Then** tests cover real gateway-client metadata, `304`, header omission for absent metadata, safe problem details, and no exposure of cursor or ETag internals as support text.
```

Rationale: generated REST metadata behavior belongs with the generator story that emits and tests the controllers.

### 4.5 Story 2.4 - Tenants External API Host Adoption

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: `Story 2.4: Tenants External API Host Adoption`

OLD:

```markdown
**And** freshness, projection-version, ETag, and paging evidence is backed by the real platform query metadata path rather than only mocked gateway-client metadata
```

NEW:

```markdown
**And** freshness, projection-version, ETag, and paging evidence is backed by the real platform query metadata path implemented by Stories 1.2, 1.3, and 2.2 rather than by mocked gateway-client metadata
**And** Tenants UI and generated API evidence must not rely on ad hoc payload fields or missing freshness metadata to claim projection-confirmed success.
```

Rationale: keeps Tenants proof as a consumer of earlier platform/API metadata work, not a blocker waiting for Epic 7.

### 4.6 Story 7.5 - Track Future Capability Backlog

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: `Story 7.5: Track Future Capability Backlog`

OLD:

```markdown
**Given** generated query freshness metadata remains partial
**When** downstream stories depend on stale/current state, projection version, ETag, or paging evidence
**Then** Story 7.6 must remain scheduled and must define and implement the platform-owned query metadata propagation contract before those downstream stories rely on it
**And** UI or generated REST acceptance criteria must not rely on ad hoc payload fields for projection-confirmed state.
```

NEW:

```markdown
Remove this acceptance criterion from Story 7.5.
```

Rationale: Story 7.5 is a planning/backlog artifact story. Query metadata sequencing belongs in the implementation readiness gates and earlier dependent stories.

### 4.7 Story 7.6 - Query Metadata Propagation Contract And Gateway Evidence

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: `Story 7.6: Query Metadata Propagation Contract And Gateway Evidence`

OLD:

```markdown
### Story 7.6: Query Metadata Propagation Contract And Gateway Evidence
...
```

NEW:

```markdown
Delete Story 7.6 after its acceptance criteria are redistributed into Stories 1.2, 1.3, 2.2, and 2.4.
```

Rationale: removes the forward dependency and keeps Epic 7 focused on operator/admin/deployment/test recovery and backlog artifacts.

## 5. Implementation Handoff

Change scope classification: **Moderate**.

Handoff recipients:

- Product Owner / Developer: update `_bmad-output/planning-artifacts/epics.md` with the approved story redistribution.
- Developer agent: preserve the redistributed ACs when creating implementation story files.
- Test Architect: verify that query metadata tests include real domain-handler metadata, generated REST headers, `304`, invalid cursor/problem details, and persisted read-model/end-state evidence where applicable.
- UX reviewer: verify UI-facing stories still treat missing freshness as unknown and never display cursor/ETag internals.

Success criteria:

- Story 7.6 no longer appears as a late Epic 7 prerequisite for Epic 1 or Epic 2 work.
- Stories 1.2, 1.3, 2.2, and 2.4 own the metadata behavior they depend on.
- Story 7.5 contains only backlog artifact deliverables.
- A rerun of implementation readiness no longer reports CRITICAL-1 or MAJOR-2 for query metadata sequencing.

## 6. Checklist Results

| Checklist item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering story | Done | Trigger is readiness CRITICAL-1 involving Story 7.6. |
| 1.2 Core problem | Done | Issue type: failed planning sequence / forward dependency. |
| 1.3 Evidence | Done | Readiness report, epics, PRD, architecture, UX, and prior query metadata proposal loaded. |
| 2.1 Current epic impact | Done | Epic 7 still valid but should not own the general query metadata platform contract. |
| 2.2 Epic-level changes | Done | Direct epic/story ownership changes proposed; no new epic. |
| 2.3 Remaining epics | Done | Epics 1 and 2 are directly affected; Epic 7 is narrowed. |
| 2.4 New/obsolete epics | Done | None. |
| 2.5 Priority/order | Done | Query metadata behavior moves before dependent Epic 1/Epic 2 completion. |
| 3.1 PRD conflicts | Done | No PRD scope conflict; requirements already exist. |
| 3.2 Architecture conflicts | Done | AD-14 already supports the proposed ownership. |
| 3.3 UX conflicts | Done | UX remains aligned with projection-confirmed and support-safe state rules. |
| 3.4 Other artifacts | Done | Query API docs should wait for implementation; epics is the blocking artifact. |
| 4.1 Direct adjustment | Viable | Selected. |
| 4.2 Rollback | Not viable | No implementation rollback needed. |
| 4.3 MVP review | Not viable | No scope reduction needed. |
| 4.4 Path forward | Done | Delete/split Story 7.6 into earlier owning stories. |
| 5.1 Issue summary | Done | Included above. |
| 5.2 Impact summary | Done | Included above. |
| 5.3 Recommendation | Done | Direct adjustment. |
| 5.4 MVP impact | Done | MVP unchanged. |
| 5.5 Handoff | Done | Moderate backlog/story coordination. |
| 6.1 Checklist completion | Done | All applicable analysis sections completed. |
| 6.2 Proposal accuracy | Done | Proposal is consistent with loaded artifacts. |
| 6.3 User approval | Done | Approved by Administrator on 2026-07-05T18:48:29+02:00. |
| 6.4 Sprint status update | N/A | No sprint-status update until approved; no story IDs are added if Story 7.6 is redistributed. |
| 6.5 Next steps | Done | Epics updated; implementation readiness rerun follows. |

## 7. Approval

Approved by Administrator at 2026-07-05T18:48:29+02:00.

## 8. Handoff Log

| Time | Route | Notes |
| --- | --- | --- |
| 2026-07-05T18:48:29+02:00 | Product Owner / Developer | Approved direct adjustment. `_bmad-output/planning-artifacts/epics.md` updated to redistribute Story 7.6 acceptance criteria into Stories 1.2, 1.3, 2.2, and 2.4 and keep Story 7.5 backlog-only. |
