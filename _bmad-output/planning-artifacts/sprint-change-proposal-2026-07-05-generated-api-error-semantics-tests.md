---
title: Sprint Change Proposal - Generated API Error Semantics Tests
date: 2026-07-05
project: eventstore
status: approved
approved_by: Administrator
approved_on: 2026-07-05
source:
  - _bmad-output/implementation-artifacts/deferred-work.md
  - _bmad-output/implementation-artifacts/epic-D-retro-2026-07-05.md
  - _bmad-output/implementation-artifacts/7-5-rest-generator-hardening.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/architecture.md
  - docs/reference/command-api.md
  - docs/reference/query-api.md
  - docs/reference/nuget-packages.md
---

# Sprint Change Proposal: Generated API Error Semantics Tests

## 1. Issue Summary

The trigger is the open Epic D retrospective action item:

```text
Add external generated API error-semantics tests for RBAC, gateway failures, invalid cursor/envelope, ETag/304, and route/body mismatch behavior.
```

Epic D delivered generated REST controllers and external API proofs, but the retired Tenants hand-written controller tests covered more runtime error behavior than the current generated-controller proof. The gap is not generator architecture. It is focused generated-surface coverage for the externally observable error semantics that consumers depend on.

Evidence:

- `_bmad-output/implementation-artifacts/deferred-work.md` records the external REST error-semantics coverage gap from the D7 review.
- `_bmad-output/implementation-artifacts/epic-D-retro-2026-07-05.md` records this as action item 3.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` keeps the action item open.
- Story `7-5-rest-generator-hardening` already contains a Task 7 for generated external API error semantics, but it benefits from a concrete test matrix so the Developer agent does not underspecify the expected cases.

## 2. Impact Analysis

**Epic impact:** Directly affects Epic 2 external generated REST behavior and Epic 7 backlog/hardening follow-through. No epic is invalidated and no new epic is required.

**Story impact:** Tighten Story 7.5, `REST Generator Hardening Backlog`, Acceptance Criterion 7 and Task 7. The story remains the implementation target; no separate story is needed.

**Artifact conflicts:** No PRD, architecture, or UX change is needed. PRD FR12 already requires generated controller tests and gateway delegation. Architecture AD-3 and AD-4 already require generated REST controllers to delegate through the gateway and live in external API hosts. AD-12 already requires high-risk evidence to be stronger than smoke status.

**Technical impact:** Implementation should stay in `tests/Hexalith.EventStore.RestApi.Generators.Tests/`, preferably through compile-and-exercise generated-controller tests using a fake `IEventStoreGatewayClient`. Code changes in `src/Hexalith.EventStore.RestApi.Generators/` should only happen if the tests expose missing behavior.

**Sprint status impact:** No status change yet. The action item should remain `open` until the tests are implemented and verified.

## 3. Recommended Approach

Recommended path: **Direct Adjustment**.

Refine the existing Story 7.5 test scope and route implementation to the Developer agent. This is a minor planning correction because the story already exists and the requested coverage fits its current boundary.

Rollback is not useful because no completed story needs to be undone. MVP review is not required because this is hardening/test evidence for an existing backlog story, not new product scope.

Effort estimate: **Low to Medium**.

Risk level: **Low** for planning, **Medium** for implementation if generated controllers are hard to instantiate. Mitigate by adding a small generated-controller test harness and recording any source-assertion fallback explicitly.

## 4. Detailed Change Proposals

### Story 7.5 Acceptance Criterion 7

Artifact: `_bmad-output/implementation-artifacts/7-5-rest-generator-hardening.md`

Section: Acceptance Criteria, item 7

OLD:

```markdown
7. **Generated external API error semantics have focused coverage at the generated surface.**
   - Add tests that prove generated controller behavior for:
     - 403/RBAC or tenant-claim denial surfaced as `application/problem+json`.
     - Gateway transport/failure exceptions mapped through `ProblemDetails` with safe extensions.
     - Invalid cursor/envelope or gateway validation failures returning safe 400 problem details when surfaced by the gateway client.
     - ETag/`304 Not Modified` response behavior and freshness-header preservation when metadata is available.
     - Route/body mismatch behavior returning 400 for command routes.
   - Prefer generator tests that compile the generated controller and exercise it through a fake `IEventStoreGatewayClient`. Source-string assertions alone are acceptable only where runtime invocation is impractical and the reason is recorded.
```

NEW:

```markdown
7. **Generated external API error semantics have focused coverage at the generated surface.**
   - Add compile-and-exercise generated-controller tests, preferably in a dedicated generated-controller error-semantics test class using a fake `IEventStoreGatewayClient`.
   - Cover this minimum matrix:
     - RBAC or tenant-claim denial: a gateway `403` becomes `application/problem+json` with safe `ProblemDetails` and stable reason metadata.
     - Gateway transport/failure: a gateway exception such as `503` or `500` maps to safe `ProblemDetails`, preserving allowed extensions such as correlation/retry metadata while excluding stack traces, tokens, raw payloads, cursors, and ETag internals.
     - Invalid cursor/envelope: gateway validation failures such as invalid cursor, invalid page, malformed query request, or invalid query envelope return safe `400` problem details at the generated endpoint.
     - ETag/`304 Not Modified`: generated query actions forward `If-None-Match`, return `304` with an empty body when the gateway reports `IsNotModified`, preserve the strong ETag header, and preserve metadata/freshness headers only when available.
     - Route/body mismatch: generated command actions reject aggregate or tenant route/body mismatches with `400` problem details before calling `SubmitCommandAsync`.
   - Source-string assertions alone are acceptable only where runtime invocation is impractical and the reason is recorded in the Dev Agent Record.
```

Rationale: Makes the user-requested coverage explicit and testable while preserving the current story boundary.

### Story 7.5 Task 7

Artifact: `_bmad-output/implementation-artifacts/7-5-rest-generator-hardening.md`

Section: Tasks / Subtasks, Task 7

OLD:

```markdown
- [ ] **Task 7: Generated external API error-semantics tests** (AC: 7)
  - [ ] Add compile-and-exercise test support for generated controllers if feasible.
  - [ ] Cover gateway exception to `ProblemDetails` mapping.
  - [ ] Cover route/body mismatch, tenant/RBAC denial, invalid cursor/envelope, and ETag/304 behavior.
  - [ ] Keep generated controllers gateway-backed; do not introduce MediatR, DAPR actor, state-store, or domain-service direct calls.
```

NEW:

```markdown
- [ ] **Task 7: Generated external API error-semantics tests** (AC: 7)
  - [ ] Add compile-and-exercise test support for generated controllers if feasible.
  - [ ] Add RBAC/tenant-denial coverage proving gateway `403` maps to safe `application/problem+json`.
  - [ ] Add gateway transport/failure coverage proving `503`/`500` style gateway exceptions map to safe `ProblemDetails`.
  - [ ] Add invalid cursor/envelope coverage proving gateway validation failures surface as safe `400` problem details.
  - [ ] Add ETag/`304` coverage proving `If-None-Match` forwarding, empty 304 body behavior, strong ETag preservation, and metadata-header preservation when metadata exists.
  - [ ] Add route/body mismatch coverage proving generated command actions return `400` before `SubmitCommandAsync`.
  - [ ] Keep generated controllers gateway-backed; do not introduce MediatR, DAPR actor, state-store, or domain-service direct calls.
```

Rationale: Gives the Developer agent a concrete checklist aligned to the open sprint action item.

### PRD, Architecture, UX, Sprint Status

OLD:

Current PRD, architecture, UX readiness review, and sprint status already route the work correctly.

NEW:

No structural change.

Rationale: The work is already covered by FR12, FR35, AD-3, AD-4, AD-12, and the open sprint action item. Sprint status remains open until implementation is complete.

## 5. Implementation Handoff

Change scope: **Minor**.

Route to: **Developer agent** for direct implementation in Story 7.5.

Developer responsibilities:

- Implement the Story 7.5 Task 7 test matrix.
- Prefer runtime generated-controller invocation with a fake `IEventStoreGatewayClient`.
- Keep generated controllers gateway-backed and support-safe.
- Run:

```bash
dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/
dotnet test tests/Hexalith.EventStore.Contracts.Tests/
dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false
```

Success criteria:

- Generated-controller tests cover RBAC/403, gateway failures, invalid cursor/envelope, ETag/304, and route/body mismatch behavior.
- Any behavior gap found by these tests is patched inside the generator or supporting test harness.
- The sprint action item remains open until tests and verification pass, then can be marked done.

## 6. Checklist Results

| Item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering story identified | Done | Epic D retrospective action item 3; Story 7.5 Task 7. |
| 1.2 Core problem defined | Done | Generated external API error semantics need explicit generated-surface tests. |
| 1.3 Evidence gathered | Done | Deferred work, retrospective, sprint status, PRD, epics, architecture, command/query docs. |
| 2.1 Current epic impact | Done | Epic 2 behavior and Epic 7 hardening follow-through; no replan. |
| 2.2 Epic-level changes | Done | No new epic; refine existing Story 7.5. |
| 2.3 Remaining epics reviewed | Done | SEC/COR/OPS/TEST epics are not affected. |
| 2.4 Invalidates future epics | N/A | No epic is invalidated. |
| 2.5 Priority/order | Done | Should be implemented inside Story 7.5 before marking the action item done. |
| 3.1 PRD conflicts | Done | No conflict; FR12 already requires generated controller tests. |
| 3.2 Architecture conflicts | Done | No conflict; AD-3/AD-4/AD-12 already bind the behavior. |
| 3.3 UX conflicts | N/A | No UI or UX surface changes. |
| 3.4 Other artifacts | Done | Story artifact tightened; sprint status intentionally unchanged. |
| 4.1 Direct adjustment | Viable | Chosen. |
| 4.2 Rollback | Not viable | No completed work should be reverted. |
| 4.3 MVP review | Not viable | No MVP scope change. |
| 4.4 Path selected | Done | Direct adjustment. |
| 5.1 Issue summary | Done | See section 1. |
| 5.2 Impact/artifacts | Done | See section 2. |
| 5.3 Path rationale | Done | See section 3. |
| 5.4 MVP impact/action plan | Done | No MVP impact. |
| 5.5 Handoff plan | Done | Developer agent implements Story 7.5 Task 7. |
| 6.1 Checklist completion | Done | All applicable items addressed. |
| 6.2 Proposal accuracy | Done | Proposal matches existing story and planning baseline. |
| 6.3 User approval | Done | User explicitly requested the corrective addition. |
| 6.4 Sprint status update | N/A | No story add/remove/renumber; action item remains open until tests pass. |
| 6.5 Next steps | Done | Dev Story 7.5 or implement Task 7 directly when scheduled. |
