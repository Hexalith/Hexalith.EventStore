---
title: Domain-Owned Contracts Library Authoring Guidance Correction
status: approved-implemented
created: 2026-07-05
approved_by: Administrator
approved_date: 2026-07-05
project: eventstore
workflow: bmad-correct-course
mode: Batch
scope: Minor
trigger: Epic D retrospective action item 6 and direct user request
---

# Sprint Change Proposal: Domain-Owned Contracts Library Authoring Guidance

## 1. Issue Summary

Epic D's external REST proof corrected the generated API architecture: generated controllers belong in dedicated external API hosts, while interactive UI hosts consume EventStore client libraries. That correction introduced an allowed exception to the domain-centric authoring rule: a domain may own a contracts-only library when the same command/query contract identities must be shared by the domain service, an external generated API host, and UI metadata consumers.

The root `CLAUDE.md` / `AGENTS.md` authoring section already allowed this exception, and guardrail tests already permit the Sample domain to reference `Hexalith.EventStore.Sample.Contracts`. The remaining issue was wording drift in adjacent authoring guidance that still read as an absolute "domain modules reference only DomainService" rule.

Evidence:

- `_bmad-output/implementation-artifacts/epic-D-retro-2026-07-05.md` action item 6 requires documenting the domain-owned contracts-library exception.
- `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` already validates that Sample may reference the DomainService SDK and its own contracts library.
- `docs/brownfield/development-guide.md`, `docs/reference/nuget-packages.md`, and `_bmad-output/project-context.md` contained "references only DomainService" wording that could conflict with the approved exception.

## 2. Impact Analysis

Epic impact:

- Epic 1 remains valid. The domain-service host still depends on the DomainService SDK for hosting/platform boilerplate.
- Epic 2 remains valid. Generated REST controllers still live in dedicated external API hosts and delegate through `IEventStoreGatewayClient`.
- No epic resequencing, story addition, or MVP scope change is required.

Story impact:

- Story 1.7 authoring docs/guardrails are clarified.
- Story 2.3 / 2.4 external API host proofs are reinforced by making the contracts-library exception explicit.
- No implementation story needs rollback.

Artifact impact:

- Update authoring guidance in `CLAUDE.md`, `AGENTS.md`, `docs/brownfield/development-guide.md`, `docs/brownfield/integration-architecture.md`, `docs/reference/nuget-packages.md`, and `_bmad-output/project-context.md`.
- PRD, epics, architecture spine, and UX artifact already support the corrected architecture; no requirements rewrite is needed.
- No code, runtime topology, build, or UI behavior changes are required.

Technical impact:

- Documentation-only.
- Existing guardrail tests already match the desired rule.
- No AppHost/Aspire runtime validation is required because no runtime code changes were made.

## 3. Recommended Approach

Recommended path: Direct Adjustment.

Rationale:

- The change is a narrow wording correction that removes ambiguity without changing product scope.
- The architecture and tests already reflect the intended behavior.
- Rollback and MVP review are not applicable.

Effort estimate: Low.

Risk level: Low.

## 4. Detailed Change Proposals

### Authoring Instructions: `CLAUDE.md` and `AGENTS.md`

OLD:

```text
a domain module references only `DomainService` ... and writes its domain code plus a two-line host
```

NEW:

```text
a domain-service host references `DomainService` for platform hosting ... A domain may also own a contracts-only library when those command/query contract identities must be shared by the domain service, an external generated API host, and UI metadata consumers
```

Rationale: The hosting rule remains strict, while the domain-owned contracts-library exception is explicitly allowed.

### Development Guide: `docs/brownfield/development-guide.md`

OLD:

```text
A domain module references only the `Hexalith.EventStore.DomainService` SDK
```

NEW:

```text
A domain module references the `Hexalith.EventStore.DomainService` SDK for hosting ... A domain-owned contracts-only library is allowed when command/query contract identities must be shared with a dedicated generated API host or UI metadata consumers
```

Rationale: This is the repository's programming-model guidance and should not contradict the approved Sample contracts-library proof.

### Integration Architecture: `docs/brownfield/integration-architecture.md`

NEW:

```text
The domain-service host references the DomainService SDK for platform hosting. A domain-owned contracts-only library is allowed when the same command/query contract identities must be shared by the domain service, the external generated API host, and UI metadata consumers.
```

Rationale: Integration architecture now states the exception at the domain-service/external-API boundary.

### NuGet Package Guide: `docs/reference/nuget-packages.md`

OLD:

```text
domain module references only this one package
```

NEW:

```text
domain-service host references this one package for platform hosting. A domain may also own a contracts-only library when shared command/query identities are needed
```

Rationale: Package guidance should distinguish platform hosting dependencies from domain-owned contract sharing.

### Agent Context: `_bmad-output/project-context.md`

NEW:

```text
Domain-owned contracts library exception: a domain-service host references `Hexalith.EventStore.DomainService` for platform hosting, but the domain may also own a contracts-only library...
```

Rationale: Future agents consume this generated context before implementation; it must carry the exception to prevent repeat regressions.

## 5. Checklist Outcome

| Checklist item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering story | [x] | Epic D retrospective action item 6; direct user request. |
| 1.2 Core problem | [x] | Misunderstanding risk from absolute "only DomainService" wording. |
| 1.3 Evidence | [x] | Retro action item, guardrail test, docs scan. |
| 2.1-2.5 Epic impact | [x] | No scope/sequence changes; Epic 1 and 2 clarified only. |
| 3.1 PRD conflicts | [x] | No PRD conflict; FR1/FR14 already support the model. |
| 3.2 Architecture conflicts | [x] | Architecture already has the exception; integration architecture needed explicit wording. |
| 3.3 UI/UX conflicts | [N/A] | No UI behavior or UX artifact change. |
| 3.4 Other artifacts | [x] | Authoring docs and project-context updated. |
| 4.1 Direct adjustment | [x] | Viable; low effort/low risk. |
| 4.2 Rollback | [N/A] | No implementation rollback needed. |
| 4.3 MVP review | [N/A] | No MVP scope change. |
| 4.4 Recommended path | [x] | Direct documentation adjustment. |
| 5.1-5.5 Proposal components | [x] | Proposal, impact, rationale, actions, and handoff recorded. |
| 6.1-6.5 Final review/handoff | [x] | Minor scope; direct implementation by Developer/Tech Writer role. |

## 6. Implementation Handoff

Scope classification: Minor.

Route to: Developer/Technical Writer direct implementation.

Responsibilities:

- Update authoring guidance so the contracts-library exception is visible wherever "only DomainService" appears.
- Preserve the anti-boilerplate rule: no hosting, DAPR, telemetry, state-store, query/projection actor, or UI code in the contracts library.
- Keep generated REST in external API hosts and UI hosts on client libraries.

Success criteria:

- Authoring guidance explains when a domain-owned contracts library is permitted.
- Guidance explains how the exception remains compatible with the domain-centric rule.
- No code or guardrail test changes are required unless the docs reveal a mismatch.

## 7. Approval

Approved by Administrator on 2026-07-05 after review of the implemented Correct Course proposal.
