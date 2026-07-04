---
project: eventstore
date: 2026-07-05
workflow: bmad-correct-course
mode: batch
status: approved
trigger:
  report: _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-05.md
  readiness: not_ready
scope_classification: major
artifacts_reviewed:
  - _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-05.md
  - _bmad-output/planning-artifacts/epics.md
  - docs/concepts/architecture-overview.md
  - docs/guides/sample-blazor-ui.md
  - docs/guides/security-model.md
  - docs/guides/dapr-component-reference.md
approval: approved
approved_by: Administrator
approved_at: 2026-07-05T00:27:44+02:00
---

# Sprint Change Proposal - Implementation Readiness Recovery

## 1. Issue Summary

The implementation readiness assessment completed on 2026-07-05 reported the Phase 4 planning package as **NOT READY**.

The trigger was not a failing implementation story. It was a readiness assessment finding that the current planning package is not sufficiently traceable or sliced for clean execution:

- No standalone PRD was found under `_bmad-output/planning-artifacts`.
- No standalone architecture document was found under `_bmad-output/planning-artifacts`.
- No standalone UX document was found under `_bmad-output/planning-artifacts`.
- `epics.md` contains an internal FR1-FR35 and NFR1-NFR18 inventory, but that file is carrying the roles of PRD, architecture proxy, UX proxy, and implementation plan.
- Several stories are too large or mix unrelated concerns, increasing review and completion risk.
- NFR traceability is weaker than FR traceability, especially for security, tenant isolation, release reproducibility, integration evidence, and operational hardening.

Evidence:

- `_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-05.md` lists PRD, architecture, and UX discovery as empty.
- The same report identifies 18 issues across missing planning artifacts, blocked PRD traceability, missing UX alignment evidence, and epic/story quality defects.
- `_bmad-output/planning-artifacts/epics.md` states that formal PRD and standalone architecture documents were not present and that approved sprint-change proposals were used as the active planning baseline.

## 2. Impact Analysis

### Epic Impact

All seven epics remain directionally valid. No epic is obsolete, and the readiness report found no forward dependencies or circular dependencies.

The impact is on planning traceability and story execution quality:

| Epic | Impact |
| --- | --- |
| Epic 1 - Domain Author Self-Service Platform | Split oversized platform adoption and shared seam stories before implementation. |
| Epic 2 - External Integration Surfaces | Split Tenants generated API work from Tenants UI alignment and compatibility validation. |
| Epic 3 - Release And Repository Reliability | Split CI migration from supply-chain backlog documentation. |
| Epic 4 - Event Correctness And Recovery | No blocking story split required by the assessment. Keep existing sequencing. |
| Epic 5 - Security And Tenant Isolation | Tighten security AC wording and split runtime topology parity if not handled by one owner. |
| Epic 6 - Bounded Cost And Event Evolution | Keep spec-first sequencing, but require explicit approval artifact paths before implementation stories start. |
| Epic 7 - Operator Trust, Admin Honesty, And Future Capabilities | Split oversized admin/deploy/test stories and reclassify backlog tracking as planning work. |

### Story Impact

The following stories require direct changes before implementation:

- Story 1.3
- Story 1.6
- Story 2.4
- Story 3.7
- Story 5.2
- Story 5.6
- Stories 6.1, 6.3, and 6.5
- Story 7.2
- Story 7.3
- Story 7.4
- Story 7.5

### Artifact Conflicts

PRD:

- A formal PRD is absent. Traceability from PRD FR/NFR requirements to epics cannot be validated.
- `epics.md` embeds requirements inventory, but that is not enough for readiness unless the team explicitly accepts it as the requirements baseline or extracts it into a PRD.

Architecture:

- A standalone planning architecture artifact is absent.
- Existing project docs describe topology and security architecture, but they are product documentation, not a Phase 4 planning architecture decision record.

UX:

- A standalone UX artifact is absent even though Sample Blazor UI, Tenants UI, and Admin UI behavior are required by FR13, FR15, NFR14, NFR15, and Story 7.2.
- Hexalith UX governance requires FrontComposer and Blazor Fluent UI V5, reuse over raw UI, no theme redefinition, and accordion grouping for multi-section surfaces. The planning package must show that UI-affecting stories will honor those rules.

Secondary artifacts:

- `sprint-status.yaml` should not be updated until this proposal is approved.
- NFR traceability should be added to the PRD or `epics.md`.
- Backlog capability artifacts should be created if Story 7.5 remains in scope.

### Technical Impact

No rollback is recommended. No completed implementation needs to be reverted.

The main technical risk is starting oversized stories before the planning baseline is repaired. That would likely produce partial implementation, unclear review scope, and weak verification evidence.

## 3. Recommended Approach

Recommended path: **Hybrid - MVP baseline recovery followed by direct backlog adjustment.**

1. Create or approve missing planning artifacts before Phase 4 implementation starts:
   - `prd.md`
   - `architecture.md`
   - `ux.md`

2. Update `epics.md` to stop carrying all planning roles by itself:
   - Reference the PRD as the source of FR/NFR truth.
   - Reference the architecture artifact for component and integration decisions.
   - Reference the UX artifact for UI-facing acceptance rules.

3. Split oversized stories into reviewable implementation slices.

4. Add NFR-to-story traceability, especially for NFR1-NFR4, NFR7, NFR10-NFR11, and NFR14-NFR17.

5. Re-run implementation readiness after the artifacts and story splits are complete.

Effort estimate: **Medium to High**.

Risk level: **Medium** if the team pauses implementation and repairs planning first; **High** if implementation starts before artifact and story-slicing corrections.

Timeline impact: one planning/backlog refinement pass before Phase 4 execution. This should be treated as schedule-protecting work because it reduces review churn and implementation ambiguity.

Alternatives considered:

- Direct adjustment only: viable for story splits, but insufficient because PRD/architecture/UX traceability remains blocked.
- Rollback: not viable or useful because no implementation work is identified as needing reversal.
- MVP reduction: not required. The MVP can remain intact if the planning baseline is repaired first.

## 4. Detailed Change Proposals

### Planning Artifact Changes

#### PRD Artifact

Artifact: `_bmad-output/planning-artifacts/prd.md`

OLD:

```markdown
No standalone PRD document exists under _bmad-output/planning-artifacts.
```

NEW:

```markdown
# eventstore - Product Requirements Document

## Planning Baseline

The Phase 4 implementation baseline is derived from the approved sprint-change proposals listed in _bmad-output/planning-artifacts/epics.md and this PRD is the authoritative FR/NFR traceability source.

## Functional Requirements

FR1-FR35 are carried forward from epics.md.

## Non-Functional Requirements

NFR1-NFR18 are carried forward from epics.md.

## MVP Scope

Phase 4 includes the current seven-epic remediation and platform-extension package. No scope reduction is approved by this correction.

## Out of Scope

Deferred capability tracks remain backlog items unless separately approved: GDPR aggregate erasure, Admin interactive OIDC login, aggregate test kit, and REST generator hardening follow-up work.

## Traceability

Each FR and high-risk NFR must map to at least one epic/story before implementation readiness can return READY.
```

Rationale: The readiness report cannot validate PRD-to-epic traceability without a PRD or an explicit replacement baseline.

#### Architecture Artifact

Artifact: `_bmad-output/planning-artifacts/architecture.md`

OLD:

```markdown
No standalone architecture document exists under _bmad-output/planning-artifacts.
```

NEW:

```markdown
# eventstore - Phase 4 Architecture

## Architecture Baseline

Phase 4 extends the existing DAPR-native EventStore topology documented in docs/concepts/architecture-overview.md and the security posture documented in docs/guides/security-model.md.

## Architecture Invariants

- Domain modules remain domain-centric; reusable hosting, telemetry, read-model, cursor, projection, and event-consumer plumbing belongs in EventStore libraries.
- Generated REST controllers live in dedicated external API hosts, not interactive UI hosts.
- DAPR state, pub/sub, service invocation, actors, config, access control, and resiliency remain the infrastructure abstraction boundary.
- Tenant isolation and fail-closed authorization apply before state, command status, projection, admin, or generated REST data can disclose resource existence.
- Integration tests that claim infrastructure coverage must inspect persisted state-store/read-model/CloudEvent evidence.

## Decision Records Needed Before Implementation

- Folded snapshot storage and recovery semantics.
- Projection cost reduction and sequence guards.
- Event versioning, upcasting, and cancellation-token public seam impact.
- Global-position sharding renegotiation.
```

Rationale: Existing docs explain product architecture, but Phase 4 needs a planning architecture artifact that captures current invariants and pre-implementation decision gates.

#### UX Artifact

Artifact: `_bmad-output/planning-artifacts/ux.md`

OLD:

```markdown
No standalone UX document exists under _bmad-output/planning-artifacts.
```

NEW:

```markdown
# eventstore - Phase 4 UX Specification

## UX Baseline

UI-facing work affects Sample Blazor UI, Tenants UI, and Admin UI.

## Governance

- Use FrontComposer and Blazor Fluent UI V5 components.
- Prefer FrontComposer/Fluent components over raw CSS, HTML, JavaScript, or third-party controls.
- Do not redefine theme primitives.
- Multi-section page-like surfaces use FluentAccordion with the primary section expanded by default.
- UI states must remain support-safe and never render tokens, decoded JWT payloads, raw EventStore metadata, raw payloads, stack traces, cursor internals, or ETag internals.

## Required User Flows

- Sample UI command submission remains a demo of accepted submission, not proof of downstream completion.
- Tenants UI remains a client-library consumer and preserves projection-confirmed success states.
- Admin UI hides or disables deferred operations; any remaining server endpoint returns 501.

## Evidence

UI stories must define smoke or bUnit evidence appropriate to the surface and must record skipped checks with exact blockers.
```

Rationale: UI constraints are currently embedded in epics and docs. A UX artifact is needed to prove readiness for UI-affecting stories.

#### Epics Overview Baseline Text

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: Overview

OLD:

```markdown
Formal EventStore PRD and standalone architecture documents were not present in `_bmad-output/planning-artifacts`. Per user confirmation, the input set for this run is `_bmad-output/planning-artifacts/*.md`; therefore the requirements inventory below is extracted from the approved sprint change proposals in that folder.
```

NEW:

```markdown
The Phase 4 planning baseline is split across `prd.md`, `architecture.md`, `ux.md`, and this epic/story plan. The PRD owns FR/NFR traceability, the architecture artifact owns component and integration decisions, the UX artifact owns UI governance and user-flow evidence, and this document owns implementation slicing and sequencing.
```

Rationale: `epics.md` should no longer be the sole planning baseline after the missing artifacts are created.

#### NFR Traceability

Artifact: `_bmad-output/planning-artifacts/prd.md` or `_bmad-output/planning-artifacts/epics.md`

OLD:

```markdown
NFRs are listed, but high-risk NFRs are not mapped with the same rigor as FRs.
```

NEW:

```markdown
| NFR | Primary story coverage |
| --- | --- |
| NFR1 | 5.2, 5.3, 5.5, 7.2 |
| NFR2 | 2.4, 5.2, 5.5, 5.6 |
| NFR3 | 5.3 |
| NFR4 | 5.3, 7.3 |
| NFR7 | 4.1, 4.2, 4.4, 4.5, 5.1 |
| NFR10 | 3.1, 7.4 |
| NFR11 | 3.6 |
| NFR14 | 2.3, 2.4 |
| NFR15 | 7.2 |
| NFR16 | 7.4 |
| NFR17 | 5.6, 7.3 |
```

Rationale: Readiness remains weak if high-risk NFRs do not trace to concrete acceptance criteria.

### Story Changes

#### Story 1.3

Section: Story scope

OLD:

```markdown
Story 1.3: Generic Read Models And Query Cursors

Combines durable read-model storage, optimistic-concurrency policy, in-memory testing fake, conflict injection, protected query cursor codec, and Tenants migration proof.
```

NEW:

```markdown
Story 1.3a: Generic Read-Model Store And Write Policy
- Owns IReadModelStore, ReadModelWritePolicy, ETag-aware read/write, multi-key/index support, DAPR implementation, and bounded optimistic-concurrency retries.

Story 1.3b: Read-Model Store Testing Fake And Conflict Semantics
- Owns the in-memory testing fake, first-write-wins behavior, ETag conflict simulation, and deterministic tests.

Story 1.3c: Protected Query Cursor Codec
- Owns IQueryCursorCodec, QueryCursorScope, DataProtection purpose isolation, payload limits, tamper handling, wrong-scope handling, malformed cursor handling, and key-rotation behavior.

Tenants migration proof moves to the split Tenants adoption stories under Story 1.6.
```

Rationale: The current story spans multiple reusable seams plus a non-trivial domain migration proof.

#### Story 1.6

Section: Story scope

OLD:

```markdown
Story 1.6: Sample And Tenants Domain-Centric Adoption

Combines Sample adoption, Tenants query/read-model adoption, Tenants projection/event adoption, platform telemetry/health adoption, governance checks, and cross-repository validation.
```

NEW:

```markdown
Story 1.6a: Sample Domain-Centric Adoption
- Adopt SDK host, projection handler, event-consumer seams, and remove moved Sample boilerplate.

Story 1.6b: Tenants Query And Read-Model Adoption
- Adopt IDomainQueryHandler, IReadModelStore, ReadModelWritePolicy, and IQueryCursorCodec while preserving Tenants RBAC, audit, pagination, and freshness semantics.

Story 1.6c: Tenants Projection And Event-Consumer Adoption
- Adopt platform domain-event consumers, projection seams, telemetry, and health checks while preserving Tenants projection semantics.

Story 1.6d: Domain Module Governance Guardrails
- Add guardrails preventing domain modules from reintroducing reusable hosting, Aspire, ServiceDefaults, projection actors, cursor codecs, state-store wrappers, telemetry, or health-check classes.
```

Rationale: Sample and Tenants are different proof sizes and should not be closed by one broad acceptance set.

#### Story 2.4

Section: Story scope

OLD:

```markdown
Story 2.4: Tenants External API Host Adoption

Combines Tenants contract route metadata, external API host generation, replacement of hand-written query controllers, Tenants UI client-library adoption, and submodule integration validation.
```

NEW:

```markdown
Story 2.4a: Tenants REST Contract Route Metadata
- Add contract markers and route metadata for tenant detail, tenant users, user tenants, global administrators, and tenant audit.

Story 2.4b: External Tenants API Host And Generated Controllers
- Build the external host, generate controllers, and prove gateway delegation replaces hand-written controller logic.

Story 2.4c: Tenants UI Client-Library Alignment
- Ensure Tenants UI consumes EventStore/Tenants client libraries and preserves projection-confirmed, support-safe UI states.

Story 2.4d: Tenants Compatibility Validation
- Run or document submodule test lanes, external behavior compatibility, and any DAPR/Aspire blockers with exact commands and failure reasons.
```

Rationale: API generation and UI architecture are related but independently reviewable.

#### Story 3.7

Section: Story scope

OLD:

```markdown
Story 3.7: Shared CI/CD Security Gates And Supply-Chain Backlog

Combines reusable workflow migration, workflow-reference scans, npm cache behavior, NuGet secret documentation, OIDC trusted publishing, attestations, SBOM, and hardening backlog tracking.
```

NEW:

```markdown
Story 3.7a: Shared CI/CD Security Gates Migration
- Convert CodeQL, dependency-review, and commitlint workflows into thin callers of shared Hexalith.Builds reusable workflows using @main.
- Verify caller workflow triggers, permissions, concurrency, and third-party pinning policy.

Story 3.7b: Release Tooling Cache And Workflow Reference Validation
- Validate npm ci cache behavior and scan Hexalith.Builds references across root and submodule workflows.

Story 3.8: Supply-Chain Publishing Backlog
- Document NUGET_API_KEY as the current publishing secret and create follow-up backlog for OIDC trusted publishing, attestations, SBOM, and provenance hardening.
```

Rationale: CI gate migration is implementation work; publishing-hardening backlog is planning/governance work.

#### Story 5.2

Section: Acceptance Criteria

OLD:

```markdown
Given admin write or sandbox endpoints accept request bodies
When request-size limits are applied
Then oversized requests fail safely
And limits are tested or documented.
```

NEW:

```markdown
Given admin write or sandbox endpoints accept request bodies
When request-size limits are applied
Then oversized requests fail safely with a bounded response
And focused tests cover the configured limit, the excessive-request path, and any endpoint-specific exemption.

Given an endpoint has no request body or no active implementation
When request-size limit applicability is reviewed
Then the story records the exact endpoint and the reason the limit is N/A
And the deferred implementation cannot be presented as protected by an untested limit.
```

Rationale: Security-relevant request limits cannot be closed by "tested or documented" ambiguity.

#### Story 5.6

Section: Story scope

OLD:

```markdown
Story 5.6: Runtime Topology And Deploy Parity

Combines AppHost sidecar component loading, production DAPR component parity, topology tests, and documentation/deployment updates.
```

NEW:

```markdown
Story 5.6a: AppHost DAPR Component Loading Parity
- Assert AppHost passes the scoped/dead-letter pubsub component path explicitly and does not load an unscoped generated component.

Story 5.6b: Production DAPR Component And ACL Parity
- Align state-store, pub/sub, key-prefix, tenant scopes, ACLs, app-id rules, and deny-by-default policies in production templates.

Story 5.6c: Runtime Topology Drift Tests
- Add tests that inspect actual sidecar arguments, loaded component paths, ACL posture, app IDs, topics, and route wiring.

Story 5.6d: Deployment Documentation Alignment
- Update DAPR access-control YAML, AppHost resource names, route/topic wiring, and deployment docs when topology changes.
```

Rationale: The current story can remain a coordinated hardening slice only if one owner can validate all deploy surfaces together. Otherwise it should split.

#### Stories 6.1, 6.3, and 6.5

Section: Acceptance Criteria

OLD:

```markdown
Spec stories require approval before implementation, but do not name the approval artifact path or evidence shape.
```

NEW:

```markdown
Each spec-first story must name its output path and approval evidence before the dependent implementation story starts.

- Story 6.1 output: _bmad-output/implementation-artifacts/spec-folded-snapshot.md
- Story 6.3 output: _bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md
- Story 6.5 output: _bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md

Each dependent implementation story must include an AC that verifies the approved spec exists and that code changes conform to it.
```

Rationale: Spec-first sequencing is valid, but implementation readiness requires clear approval artifacts.

#### Story 7.2

Section: Story scope

OLD:

```markdown
Story 7.2: Admin Claims, Audit, And Honest Deferred Operations

Combines admin claims normalization, audit logging, unavailable-operation UI/server honesty, and shared typed-client reduction.
```

NEW:

```markdown
Story 7.2a: Admin Claims Normalization And Fail-Closed Scope
- Normalize tenants and permissions consistently with gateway rules and deny null or missing tenant scope.

Story 7.2b: Admin Audit Logging
- Add structured audit records for state-mutating admin actions without exposing tokens, raw payloads, stack traces, or unsafe internals.

Story 7.2c: Honest Deferred Admin Operations
- Hide or disable unavailable backup, restore, import, compaction, and deferred operations in Admin UI; ensure remaining endpoints return 501.

Story 7.2d: Shared Admin Typed Client Reduction
- Introduce or plan shared typed client reduction without changing authorization semantics.
```

Rationale: Authorization, audit, UX honesty, and client consolidation have different review surfaces.

#### Story 7.3

Section: Story scope

OLD:

```markdown
Story 7.3: Production Deployment Hardening

Combines secret-store usage, readiness and app-health checks, DAPR resiliency policy, and immutable image tags.
```

NEW:

```markdown
Story 7.3a: Secret-Store Deployment Configuration
- Replace production plaintext secret placeholders with secret-store components and secretKeyRef where required.

Story 7.3b: Readiness And DAPR App-Health Checks
- Tag state-store health checks for readiness and enable DAPR app health checks where supported.

Story 7.3c: DAPR Resiliency Policy Coverage
- Validate domain-service invocation targets, retries, timeouts, circuit breakers, and documented behavior.

Story 7.3d: Immutable Image Tag Support
- Ensure deployment manifests support or prefer git-SHA image tags and do not rely only on mutable tags.
```

Rationale: These are independently testable deployment hardening concerns.

#### Story 7.4

Section: Story scope

OLD:

```markdown
Story 7.4: Integration Test Recovery And State Evidence

Combines CI integration-test lane recovery, persisted state evidence assertions, fake/integration test reclassification, and perf-lab workflow setup.
```

NEW:

```markdown
Story 7.4a: Recover Integration Test CI Lanes
- Run infra-free or lightweight IntegrationTests subsets in CI and document the full Aspire-dependent lane or blocker.

Story 7.4b: Persisted State Evidence Assertions
- Refactor integration tests to assert Redis/state-store/read-model/CloudEvent evidence and extract shared helpers into Hexalith.EventStore.Testing.Integration.

Story 7.4c: Fake And Integration Test Reclassification
- Move fake-only tests to unit scope or rewrite them against real infrastructure so integration labels reflect external dependency coverage.

Story 7.4d: Perf-Lab And Advisory Workflow Hygiene
- Add workflow_dispatch perf-lab lane and fix, quarantine with rationale, or remove permanently red advisory jobs and never-driven skips.
```

Rationale: Test-lane recovery and evidence quality should be reviewable in focused increments.

#### Story 7.5

Section: Story classification

OLD:

```markdown
Story 7.5: Track Future Capability Backlog

Acceptance criteria describe backlog tracking for GDPR-1, IAM-1, KIT-1, and REST generator hardening, but the story is presented alongside implementation stories.
```

NEW:

```markdown
Story 7.5: Backlog Capability Artifacts

Classification: planning/backlog artifact, not implementation.

Required outputs:
- _bmad-output/planning-artifacts/backlog/gdpr-1-aggregate-erasure.md
- _bmad-output/planning-artifacts/backlog/iam-1-admin-oidc-login.md
- _bmad-output/planning-artifacts/backlog/kit-1-aggregate-test-kit.md
- _bmad-output/planning-artifacts/backlog/rest-generator-hardening.md

Acceptance evidence:
- Each artifact has scope, non-goals, dependencies, risks, and validation expectations.
- No code implementation is hidden inside this story.
- sprint-status marks this as backlog/planning unless the team explicitly wants it in the implementation sprint.
```

Rationale: Backlog tracking is useful, but it is not an implementation slice unless exact artifact deliverables are defined.

## 5. Implementation Handoff

Change scope classification: **Major**.

Reason: Missing PRD, architecture, and UX artifacts block full implementation readiness and require planning artifact creation plus backlog reorganization before Developer-agent implementation should continue.

Recommended handoff:

| Recipient | Responsibility |
| --- | --- |
| Product Manager / Product Owner | Approve the planning baseline, create or approve `prd.md`, and confirm whether Phase 4 MVP scope remains unchanged. |
| Solution Architect | Create or approve `architecture.md`, including invariants and spec-first gates for Epic 6 and global-position sharding. |
| UX Designer / UI Owner | Create or approve `ux.md`, especially Admin UI unavailable-operation behavior, Tenants UI projection-confirmed states, and FrontComposer/Fluent UI V5 governance. |
| Product Owner / Developer | Split stories in `epics.md`, update NFR traceability, and update `sprint-status.yaml` only after proposal approval. |
| Developer Agent | Implement stories only after the readiness recovery changes are approved and the affected stories are sliced. |

Success criteria:

- `prd.md`, `architecture.md`, and `ux.md` exist under `_bmad-output/planning-artifacts`.
- `epics.md` references those artifacts and no longer states that it is carrying the whole planning baseline alone.
- Oversized stories listed in this proposal are split or explicitly accepted as coordinated slices with named owners and validation commands.
- High-risk NFRs have story-level traceability.
- Story 5.2 no longer permits "tested or documented" for security-sensitive request-size limits.
- Story 7.5 is reclassified as backlog/planning work or given exact implementation deliverables.
- Implementation readiness is re-run and improves from NOT READY to at least NEEDS WORK, ideally READY.

## 6. Checklist Record

| Item | Status | Notes |
| --- | --- | --- |
| 1.1 Trigger story identified | N/A | Trigger is the readiness report, not a single implementation story. |
| 1.2 Core problem defined | Done | Planning package is not ready because required artifacts are missing and several stories are oversized. |
| 1.3 Supporting evidence gathered | Done | Evidence from readiness report and `epics.md`. |
| 2.1 Current epic evaluated | N/A | No current in-progress story identified. |
| 2.2 Epic-level changes determined | Action-needed | Story splits and planning artifact creation required. |
| 2.3 Remaining epics reviewed | Done | All seven epics reviewed through readiness report and `epics.md`. |
| 2.4 Future epic invalidation checked | Done | No epic obsolete; Story 7.5 classification needs correction. |
| 2.5 Priority/order checked | Done | Planning recovery must precede Phase 4 implementation. |
| 3.1 PRD conflicts checked | Action-needed | PRD absent; create or formally approve replacement baseline. |
| 3.2 Architecture conflicts checked | Action-needed | Planning architecture absent; create artifact. |
| 3.3 UX conflicts checked | Action-needed | UX artifact absent; create artifact. |
| 3.4 Other artifact impacts checked | Action-needed | `sprint-status.yaml` update is pending approval. |
| 4.1 Direct adjustment evaluated | Viable | Viable only after baseline artifacts are repaired. |
| 4.2 Rollback evaluated | Not viable | No implementation rollback needed. |
| 4.3 MVP review evaluated | Viable | MVP can remain, but readiness requires artifact recovery. |
| 4.4 Recommended path selected | Done | Hybrid MVP baseline recovery plus direct story adjustment. |
| 5.1 Issue summary created | Done | Included in section 1. |
| 5.2 Epic/artifact impact documented | Done | Included in section 2. |
| 5.3 Recommended path documented | Done | Included in section 3. |
| 5.4 MVP impact/action plan defined | Done | No scope reduction; sequencing changed. |
| 5.5 Handoff plan established | Done | Included in section 5. |
| 6.1 Checklist reviewed | Done | Open action-needed items are documented. |
| 6.2 Proposal accuracy checked | Done | Draft aligns with readiness report and discovered artifacts. |
| 6.3 User approval obtained | Done | Administrator approved the proposal on 2026-07-05. |
| 6.4 Sprint status updated | Action-needed | Deferred to backlog reorganization because no epic/story file changes were applied in this approval step. |
| 6.5 Handoff confirmed | Done | Major-scope handoff route is PM/Architect/UX plus PO/Developer backlog reorganization. |

## 7. Approval State

This proposal is approved by Administrator as of 2026-07-05T00:27:44+02:00.

No changes have been applied to `epics.md`, `sprint-status.yaml`, or backlog files in this approval step. Because this is a Major-scope correction, the approved route is planning artifact creation and backlog reorganization before Developer-agent implementation resumes.

## 8. Workflow Execution Log

| Time | Event |
| --- | --- |
| 2026-07-05T00:27:44+02:00 | Administrator approved the Sprint Change Proposal. |
| 2026-07-05T00:27:44+02:00 | Scope confirmed as Major because missing PRD, architecture, and UX artifacts require planning recovery and backlog reorganization. |
| 2026-07-05T00:27:44+02:00 | Handoff route confirmed: Product Manager/Product Owner for PRD and MVP baseline, Solution Architect for architecture decisions, UX owner for UI evidence, Product Owner/Developer for story splits and sprint-status update, Developer Agent after readiness recovery. |
