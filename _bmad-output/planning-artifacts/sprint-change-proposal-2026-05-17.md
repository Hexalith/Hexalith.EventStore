---
workflow: bmad-correct-course
project: Hexalith.EventStore
date: 2026-05-17
trigger: implementation-readiness-report-2026-05-17.md
status: approved
approvedAt: 2026-05-17
scope: moderate
recommendedPath: hybrid-direct-adjustment-and-backlog-reorganization
---

# Sprint Change Proposal - Implementation Readiness Corrections

## 1. Issue Summary

The implementation readiness assessment completed on 2026-05-17 found that the planning artifacts are strong on coverage but not yet clean enough to use as the primary implementation guide without correction.

The trigger is not missing requirements. The PRD, architecture, epics, and UX documents all exist, the PRD contains 104 functional requirements and 46 non-functional requirements, and the epics map 104/104 PRD FRs. The issue is planning quality: implementers can be steered toward technical-layered epics, inconsistent command API route examples, delayed onboarding value, and oversized stories.

Evidence from the assessment:

- Critical: multiple epics are structured as technical milestones rather than user-value slices.
- Major: first-run and onboarding value arrives in Epic 8, after seven technical foundation epics.
- Major: route shape inconsistencies exist across PRD, UX, architecture, and epics.
- Major: several stories are compound and should be split before future execution or follow-up work.
- Major: Epics 14-21 rely on linked implementation artifacts rather than compact inline story summaries in `epics.md`.
- Minor: query/projection caching phase labels conflict between PRD current-release language and epic inventory "v2" labels.
- Minor: historical/migration epics need explicit outcome framing.

## 2. Checklist Results

| Item | Status | Result |
| --- | --- | --- |
| 1.1 Triggering story | [x] Done | Trigger is a readiness assessment, not a single implementation story. Active context includes Epic 22 with Story 22.6 in progress and 22.7 stories blocked/queued. |
| 1.2 Core problem | [x] Done | Misalignment and planning-quality issue: technical sequencing and inconsistent artifact guidance could cause implementation rework. |
| 1.3 Evidence | [x] Done | Evidence is in `_bmad-output/planning-artifacts/implementation-readiness-report-2026-05-17.md`. |
| 2.1 Current epic impact | [x] Done | Epic 22 remains viable, but pending/blocked stories should inherit corrected route and story-slicing guidance. |
| 2.2 Epic-level changes | [!] Action-needed | Add value-outcome framing to technical epics; do not renumber completed epics without explicit approval. |
| 2.3 Future epic impact | [x] Done | Future follow-up stories must use canonical routes and split oversized scope before dev handoff. |
| 2.4 New/obsolete epics | [x] Done | No new product epic required. A planning-correction task or documentation change set is sufficient. |
| 2.5 Epic order/priority | [!] Action-needed | Add an explicit early walking-skeleton milestone to the planning narrative for future readers, even though historical Epics 1-8 are already done. |
| 3.1 PRD conflicts | [x] Done | PRD route table is internally canonical and should remain the source of truth. |
| 3.2 Architecture conflicts | [!] Action-needed | Architecture contains status/replay route variants that conflict with PRD route order. |
| 3.3 UX conflicts | [!] Action-needed | UX examples and error journeys use unversioned command routes. |
| 3.4 Other artifacts | [!] Action-needed | OpenAPI examples, docs, and tests should be checked after route canonicalization. |
| 4.1 Direct adjustment | [x] Viable | Route canonicalization, phase-label normalization, outcome annotations, and summary additions are direct document edits. |
| 4.2 Potential rollback | [x] Not viable | No implementation rollback is justified by these planning findings. |
| 4.3 PRD MVP review | [x] Not viable | MVP scope remains achievable; the issue is execution guidance quality. |
| 4.4 Recommended path | [x] Done | Hybrid: direct artifact correction plus moderate backlog/story hygiene. |
| 5.1-5.5 Proposal components | [x] Done | Covered below. |
| 6.1-6.2 Final review | [x] Done | Proposal is ready for review. |
| 6.3 Approval | [!] Action-needed | User approval is required before editing planning artifacts or sprint status. |
| 6.4 Sprint status update | [N/A] Skip | No sprint-status changes until proposal approval and exact story mutation decision. |
| 6.5 Handoff | [x] Done | Handoff plan included below. |

## 3. Impact Analysis

### Epic Impact

Epics 1-13 remain functionally covered, but their headings and sequence overemphasize implementation layers. This is especially risky for future agents or developers reading the plan as a greenfield implementation sequence. The correction should preserve epic IDs and completed status, then add value-outcome framing and an early walking-skeleton note.

Epics 14-21 are completed and should not be reopened for code work based only on this assessment. Their central documentation should become self-contained enough that a reviewer can understand story intent without opening 67 linked implementation artifacts.

Epic 22 is actively relevant. Story 22.6 is currently in progress according to `sprint-status.yaml`, Story 22.5 is done, and 22.7 stories are blocked/queued around protection metadata dependencies. For Epic 22, apply story-splitting guidance to pending and future work first; avoid retroactively renumbering completed stories unless a separate backlog cleanup is approved.

### Artifact Conflicts

PRD route source of truth:

```text
POST /api/v1/commands
GET  /api/v1/commands/status/{correlationId}
POST /api/v1/commands/replay/{correlationId}
POST /api/v1/queries
```

Conflicting artifacts:

- UX uses `/api/commands`, `/api/commands/status/{correlationId}`, and examples with `instance: "/api/commands"`.
- Architecture uses `GET /api/v1/commands/{correlationId}/status`, `POST /api/v1/commands/{id}/replay`, and file-tree comments reflecting those variants.
- Epics include UX-DR14 as `/api/commands/status/{correlationId}` even though later command stories use `/api/v1/commands/status/{correlationId}`.

### Technical Impact

No immediate product architecture change is needed. The technical impact is mainly preventing duplicate route shapes, incompatible OpenAPI/test examples, and future story files that encode the wrong endpoint.

### MVP Impact

MVP scope does not need to be reduced. The plan should be corrected so the existing MVP is easier to execute and validate incrementally.

## 4. Recommended Approach

Use a hybrid of direct adjustment and moderate backlog reorganization.

Direct adjustment:

- Canonicalize command routes across UX, architecture, epics, OpenAPI expectations, and docs.
- Normalize query/projection phase labels to "current release" where the PRD says current release.
- Add outcome annotations to technical/historical epics.
- Add compact inline summaries for completed Epics 14-21.

Moderate backlog reorganization:

- For active or future work, split oversized stories before handoff.
- For completed oversized stories, do not rewrite execution history; add retrospective split maps only when they produce useful follow-up tasks or review checklists.
- Add an explicit "walking skeleton" validation milestone to the planning narrative so future readers see clone-to-command-flow value early.

Do not roll back implementation work. Do not reopen completed epics solely for planning-style cleanup.

Effort estimate: Medium.

Risk level: Low to medium. The biggest risk is churn from renumbering completed work; this proposal avoids that by favoring annotations, summaries, and future-story split maps.

## 5. Detailed Change Proposals

### Proposal A - Canonicalize Command API Routes

Affected artifacts:

- `_bmad-output/planning-artifacts/ux-design-specification.md`
- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/epics.md`
- OpenAPI/docs/tests that quote command routes

Canonical route decision:

```text
POST /api/v1/commands
GET  /api/v1/commands/status/{correlationId}
POST /api/v1/commands/replay/{correlationId}
```

Rationale: This matches the PRD route table and current Epic 3 command story acceptance criteria.

#### UX Route Edits

OLD:

```text
POST /api/commands
GET /api/commands/status/{correlationId}
"instance": "/api/commands"
```

NEW:

```text
POST /api/v1/commands
GET /api/v1/commands/status/{correlationId}
"instance": "/api/v1/commands"
```

Rationale: Swagger examples, error journeys, and ProblemDetails `instance` values must align with the public versioned API.

#### Architecture Route Edits

OLD:

```text
GET /api/v1/commands/{correlationId}/status
CommandsController.cs           # POST /api/v1/commands (FR1)
CommandStatusController.cs      # GET /api/v1/commands/{id}/status (FR5)
ReplayController.cs             # POST /api/v1/commands/{id}/replay (FR6)
```

NEW:

```text
GET /api/v1/commands/status/{correlationId}
CommandsController.cs           # POST /api/v1/commands (FR1)
CommandStatusController.cs      # GET /api/v1/commands/status/{correlationId} (FR5)
ReplayController.cs             # POST /api/v1/commands/replay/{correlationId} (FR6)
```

Rationale: The architecture should not introduce route-order variants that can produce duplicate endpoints.

#### Epics Route Edits

OLD:

```text
UX-DR14: Command status endpoint at `/api/commands/status/{correlationId}` returning current lifecycle state with timestamp
```

NEW:

```text
UX-DR14: Command status endpoint at `/api/v1/commands/status/{correlationId}` returning current lifecycle state with timestamp
```

Rationale: Story acceptance criteria and UX-derived requirement references should use the same versioned path.

### Proposal B - Add Value-Outcome Framing to Technical Epics

Affected artifact:

- `_bmad-output/planning-artifacts/epics.md`

Do not renumber or invalidate completed epics. Add outcome lines under the Epic List and optionally retitle in parentheses.

Suggested outcome framing:

| Epic | Current title | Outcome framing to add |
| --- | --- | --- |
| 1 | Domain Contract Foundation | Domain developers can define safe commands, events, identities, and aggregate behavior without infrastructure code. |
| 2 | Event Persistence & Aggregate Processing | API-submitted commands can produce durable, replayable aggregate event streams. |
| 4 | Event Distribution & Pub/Sub | Subscribers can receive persisted events reliably without coupling to storage internals. |
| 5 | Security & Multi-Tenant Isolation | Tenants can submit and process commands without data crossing identity, storage, or pub/sub boundaries. |
| 6 | Observability & Operations | Operators can trace command lifecycle failures using logs, traces, and health endpoints. |
| 7 | Snapshots, Rate Limiting & Performance | Operators can keep command processing responsive under stream growth and abuse pressure. |
| 11 | Server-Managed Projection Builder | Projection owners can get queryable read models without owning pub/sub subscriptions. |
| 14 | Admin API Foundation & Abstractions | Admin users, CLI users, and MCP agents share one secure operational API. |
| 19 | Admin - DAPR Infrastructure Visibility | Operators can diagnose DAPR sidecar, actor, pub/sub, and resiliency health from EventStore context. |
| 21 | Admin UI Fluent UI v5 Stability Migration | Admin users retain stable, accessible, testable workflows on Fluent UI v5. |

Example edit:

OLD:

```text
### Epic 2: Event Persistence & Aggregate Processing
Commands routed to aggregate actors trigger state rehydration...
```

NEW:

```text
### Epic 2: Event Persistence & Aggregate Processing
Outcome: API-submitted commands can produce durable, replayable aggregate event streams.

Commands routed to aggregate actors trigger state rehydration...
```

Rationale: This satisfies the user-value slicing standard without destabilizing historical epic IDs or traceability.

### Proposal C - Add an Early Walking-Skeleton Validation Milestone

Affected artifact:

- `_bmad-output/planning-artifacts/epics.md`

Add a short "Walking Skeleton Gate" note before or after the Epic List.

NEW:

```text
## Walking Skeleton Gate

Before treating the foundation sequence as implementation-ready in a new execution pass, prove a thin clone-to-command-flow path:

- AppHost starts EventStore and one sample domain service.
- A single sample command is submitted through `POST /api/v1/commands`.
- One event is persisted for the aggregate.
- Command status is observable through `GET /api/v1/commands/status/{correlationId}`.
- At least one structured log or trace carries the same correlation ID.

Later epics deepen persistence, auth, distribution, testing, and operations. This gate exists to keep onboarding value visible early.
```

Rationale: The current historical implementation has already delivered this, but the central plan should still communicate that first-run value is an early proof, not a late surprise.

### Proposal D - Split Oversized Stories Before Future Handoff

Affected artifact:

- `_bmad-output/planning-artifacts/epics.md`
- Future story files created from these sections

#### Story 3.5 Split Map

Current story is completed. Do not renumber historical work unless a cleanup is approved. Add a note that future follow-ups should split by error class:

```text
Split map for future follow-ups:
- 3.5a Authentication failure ProblemDetails and WWW-Authenticate behavior
- 3.5b Tenant authorization failure ProblemDetails
- 3.5c Optimistic concurrency conflict and retry hints
- 3.5d Infrastructure unavailable behavior and terminology redaction
- 3.5e Correlation ID serialization consistency across errors
```

#### Story 8.5 Split Map

Current story is completed. Add a retrospective split map:

```text
Split map for future test-platform follow-ups:
- 8.5a Tier 1 pure domain unit test harness
- 8.5b Tier 2 DAPR actor integration test harness
- 8.5c Tier 3 Aspire E2E contract test harness
- 8.5d Test execution guidance and evidence collection
```

#### Story 22.1 Split Map

If Story 22.1 is still being reviewed or receives follow-up work, split by package ownership:

```text
- 22.1a Contracts gateway DTOs and ProblemDetails extension names
- 22.1b Client high-level command/query methods
- 22.1c Testing fakes and builders
- 22.1d Package ownership docs and generated API refresh
```

#### Story 22.5 Split Map

Story 22.5 is done according to sprint status. Use this split map for follow-up findings only:

```text
- 22.5a Durable publish-after-persist semantics
- 22.5b Backend deployment matrix and ordering/session policy
- 22.5c Drain, retry, and dead-letter behavior
- 22.5d Backend-specific proof tests and evidence
```

#### Story 22.7d Split Recommendation

Story 22.7d is blocked/queued and should be split before implementation.

OLD:

```text
### Story 22.7d: Protected Data Redaction Across Operational Surfaces
As a security reviewer,
I want diagnostics and admin surfaces to avoid protected payload disclosure,
So that logs, errors, replay, rebuild, backup validation, UI, CLI, MCP, and tests remain useful without leaking protected data.
```

NEW:

```text
### Story 22.7d-1: Protected Data Redaction in Logs and ProblemDetails
As a security reviewer,
I want logs and ProblemDetails to expose only protected-data metadata,
So that API and runtime diagnostics never leak protected payload or snapshot content.

### Story 22.7d-2: Protected Data Redaction in Admin API and Web UI
As an operator,
I want admin API and Web UI surfaces to show protected-data status without plaintext,
So that investigation workflows remain useful and safe.

### Story 22.7d-3: Protected Data Redaction in CLI and MCP
As an automation user,
I want CLI and MCP outputs to return structured redacted metadata,
So that scripts and AI agents can diagnose issues without receiving protected content.

### Story 22.7d-4: Protected Data Redaction in Replay, Rebuild, Backup Validation, and Tests
As a platform owner,
I want replay, rebuild, backup validation, and test artifacts to preserve redaction guarantees,
So that operational recovery cannot bypass protection boundaries.
```

Rationale: The current 22.7d scope spans too many independently testable surfaces. Splitting it will improve acceptance, code review, and security validation.

### Proposal E - Make Completed Epics 14-21 Self-Contained

Affected artifact:

- `_bmad-output/planning-artifacts/epics.md`

For each completed story link under Epics 14-21, add a compact inline summary with persona/outcome/key acceptance criteria. Preserve existing links as evidence and detail.

Template:

```text
- 15.4 Aggregate state inspector and diff viewer
  - Outcome: A developer can inspect aggregate state at a selected position and compare two positions.
  - Key acceptance: state is loaded through Admin API, diff output highlights changed fields, payload disclosure rules are respected, and links to detailed artifact remain.
  - Detail: `_bmad-output/implementation-artifacts/15-4-aggregate-state-inspector-and-diff-viewer.md`
```

Rationale: The current artifact links are valid, but the central epics document should remain reviewable without opening dozens of files.

### Proposal F - Normalize Query Pipeline Phase Labels

Affected artifacts:

- `_bmad-output/planning-artifacts/epics.md`
- Any sprint-status or planning summary that says query/projection caching is purely v2

OLD:

```text
Query Pipeline & Projection Caching - v2 (FR50-FR64)
Query Pipeline Performance - v2 (NFR35-NFR39)
```

NEW:

```text
Query Pipeline & Projection Caching - current release (FR50-FR64)
Query Pipeline Performance - current release (NFR35-NFR39)
```

Rationale: The PRD states that the current release includes the query/projection caching pipeline. v2 is now focused on DAPR Workflow migration and comprehensive administration tooling.

### Proposal G - Preserve Admin UX Interaction Requirements in Story Validation

Affected artifacts:

- `_bmad-output/planning-artifacts/epics.md`
- Future story files or reviews for Epics 15, 17, 18, and 20 follow-up work

Add an explicit note:

```text
Admin UX interaction requirements UX-DR41 through UX-DR59 remain story-level acceptance criteria. Architecture ADR-P4 and ADR-P5 support the interface model and observability deep-link strategy, but command palette, breadcrumbs, virtualized rendering, keyboard shortcuts, CLI profiles/REPL/completions, MCP tenant context, and investigation session state must stay visible in story validation.
```

Rationale: This prevents architecture-level support from being mistaken for implemented interaction-level acceptance.

## 6. Implementation Handoff

Scope classification: Moderate.

Recommended routing:

- Product Owner or planning agent: approve the canonical route decision and story split policy.
- Architect: review route canonicalization against implemented controllers/OpenAPI if needed.
- Developer agent: after approval, apply document edits to PRD/UX/architecture/epics and run text searches to verify no route variants remain.
- Test Architect or QA agent: ensure future tests and generated OpenAPI examples use canonical routes.

Do not update `sprint-status.yaml` until the user approves whether story splits should create new story IDs or remain as split maps and notes.

## 7. Success Criteria

This proposal is complete when:

- `/api/commands` no longer appears in planning examples except as historical text or explicit compatibility notes.
- `/api/v1/commands/{id}/status` and `/api/v1/commands/{id}/replay` no longer appear as canonical route examples.
- `epics.md` includes outcome framing for technical/historical epics without breaking FR coverage.
- The early walking-skeleton gate is visible in the epic sequence.
- Query/projection phase labels match the PRD.
- Story 22.7d is split before implementation, or an explicit user decision accepts it as intentionally broad.
- Epics 14-21 have compact inline story summaries while retaining artifact links.

## 8. Approval Request

Approval needed before implementation:

1. Approve the PRD route table as canonical: `POST /api/v1/commands`, `GET /api/v1/commands/status/{correlationId}`, `POST /api/v1/commands/replay/{correlationId}`.
2. Approve non-renumbering corrections for completed epics and completed stories.
3. Approve splitting pending Story 22.7d before implementation.
4. Approve document-only correction first, with `sprint-status.yaml` unchanged until story ID changes are explicitly chosen.
