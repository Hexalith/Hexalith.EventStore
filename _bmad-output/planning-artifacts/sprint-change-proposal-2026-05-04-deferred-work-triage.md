# Sprint Change Proposal: Deferred Work Triage

Date: 2026-05-04
Project: Hexalith.EventStore
Trigger: `_bmad-output/implementation-artifacts/deferred-work.md`
Mode: Batch
Prepared by: Amelia (Developer)

## 1. Issue Summary

`deferred-work.md` has become the repository's shared holding pen for review deferrals, runtime evidence gaps, accepted technical debt, and small polish items. As of this proposal, it contains 291 deferred bullets across post-epic proof stories, Admin UI stories, DAPR diagnostics, SignalR, projection delivery, MCP, documentation, and release governance.

The file is useful as evidence, but it is no longer a good execution surface. Important items are mixed with cosmetic notes, already-resolved entries, speculative risks, and intentionally accepted non-actions. That creates three problems:

- High-signal hardening work can be missed because it is buried among low-risk notes.
- Reviewers repeatedly rediscover the same seams: projection delivery, JSON reconstruction, DAPR runtime evidence, UI browser verification, and operational evidence schemas.
- Sprint planning cannot easily decide what to do next because the file has no severity, owner, disposition, or story mapping.

This is not a product pivot. It is a planning and execution correction: preserve `deferred-work.md` as raw evidence, but route its recurring themes into a small number of explicit follow-up stories.

## 2. Checklist Findings

| Item | Status | Finding |
| ---- | ------ | ------- |
| 1.1 Triggering story | Done | The trigger is cross-story accumulation in `deferred-work.md`, with the latest entries from 2026-05-04 reviews and older unresolved entries from Epics 15-21 plus post-epic proof work. |
| 1.2 Core problem | Done | Technical/process limitation: deferred review findings are tracked as prose, not an actionable backlog. |
| 1.3 Evidence | Done | 291 bullets, 50 deferred sections, repeated themes around projection hardening, Admin diagnostics, operational evidence, DAPR runtime proof, and UI verification. |
| 2.1 Current epic impact | N/A | All primary epics through Epic 21 are marked done. This does not block a current epic. |
| 2.2 Epic-level changes | Action-needed | Add a post-epic deferred-work cleanup package instead of reopening completed epics. |
| 2.3 Remaining epics | Done | No remaining planned epic is invalidated. Future admin/tooling work should consume the proposed follow-up stories first when it touches the same seams. |
| 2.4 New epic needed | Not viable | A full new epic is too heavy. A grouped post-epic cleanup package is sufficient. |
| 2.5 Priority/order | Done | Projection and live-evidence work should lead; cosmetic documentation cleanup can trail. |
| 3.1 PRD impact | Done | No MVP change. This strengthens existing operations, reliability, and developer-experience goals. |
| 3.2 Architecture impact | Action-needed | Architecture notes should be added for accepted DAPR trust-boundary assumptions, large-stream admin query limitations, and projection checkpoint/orchestrator hardening decisions. |
| 3.3 UX impact | Action-needed | UI runtime verification and two concrete Admin UI bugs need explicit follow-up, but UX specifications do not need broad rewrite. |
| 3.4 Other artifacts | Action-needed | `sprint-status.yaml`, story files, operational evidence templates, DAPR deployment docs, and selected tests need updates after approval. |
| 4.1 Direct adjustment | Viable | Best path: create grouped follow-up stories and leave completed epics closed. Effort medium, risk low-medium. |
| 4.2 Rollback | Not viable | Rolling back completed work does not reduce the problem. Deferred items are mostly hardening and evidence gaps. |
| 4.3 MVP review | Not viable | MVP remains achievable; no scope reduction needed. |
| 4.4 Recommended path | Done | Hybrid direct adjustment: create a small backlog package plus one governance story for ongoing triage. |
| 5.1-5.5 Proposal components | Done | Included below. |
| 6.1-6.2 Final review | Done | Proposal is actionable but awaits Jerome approval before sprint-status edits. |
| 6.3-6.5 Approval/handoff | Action-needed | User approval required before tracking changes are applied. |

## 3. Impact Analysis

### Epic Impact

No completed epic needs to be reopened. The affected areas span completed work:

- Epics 4, 9, 10, and 11: event distribution, query/projection proof, SignalR, and projection builder hardening.
- Epics 15, 19, 20, and 21: Admin UI, DAPR visibility, advanced debugging, and Fluent UI migration.
- Epic 18: MCP runtime proof and protocol evidence.
- Cross-cutting process: story-status updates, code-review deferrals, and evidence templates.

The right planning move is a post-epic cleanup package, not a new numbered product epic.

### PRD Impact

No PRD requirement is removed or redefined. The proposal supports existing requirements:

- Reliability and operational goals by hardening projection delivery, event-drain handling, DAPR runtime proof, and admin diagnostics.
- Developer experience by reducing deferred-work ambiguity.
- Observability goals by making evidence artifacts falsifiable instead of prose-only.

### Architecture Impact

Architecture updates are needed only as clarifying notes:

- Admin query endpoints using `[AllowAnonymous]` behind DAPR/internal service boundaries need one explicit trust-boundary decision.
- Large-stream admin diagnostics need a stated limitation around `GetEventsAsync(0)`, full-stream JSON reconstruction, tail-scan caps, and expected degradation.
- Projection delivery needs a hardening decision record covering checkpoint drift, per-aggregate serialization, HTTP timeout/error classification, and tracker scaling.

### UI/UX Impact

No broad UX redesign is required. Two concrete runtime issues and one verification gap should be tracked:

- TypeCatalog `/types` can block sidebar navigation.
- Ctrl+B sidebar toggle throws at runtime.
- CommandSandbox/EventDebugger dialog `aria-label` runtime and assistive-technology verification remains incomplete.

### Testing and Evidence Impact

The largest process gap is evidence quality:

- Query and SignalR operational evidence templates need schema or lint validation.
- DAPR Admin and Epic 20 debugging tools need live smoke checklists.
- MCP needs live startup, `tools/list`, representative read, write-preview, and session evidence.
- Tier 3 helper cancellation and timeout diagnostics should be hardened once, not patched per proof.

## 4. Recommended Approach

Recommended path: Direct adjustment through a grouped post-epic deferred-work cleanup package.

Do not reopen completed epics. Do not dump all 291 bullets into sprint-status. Instead, create a small number of follow-up stories that absorb repeated themes and leave low-value notes in `deferred-work.md` as accepted debt.

Priority order:

1. Projection and event-drain hardening.
2. Admin/DAPR/MCP live evidence.
3. Admin debugging JSON and large-stream limitations.
4. Operational evidence schema validation.
5. UI runtime bug and verification cleanup.
6. Deferred-work governance and disposition tagging.

## 5. Detailed Change Proposals

### Proposal A: Add Post-Epic Deferred-Work Cleanup Package

Story/status changes:

OLD:

```yaml
# No grouped deferred-work cleanup package exists.
```

NEW:

```yaml
# Post-Epic Deferred Work Cleanup (sprint-change-proposal-2026-05-04-deferred-work-triage.md)
post-epic-deferred-dw1-projection-and-drain-hardening: backlog
post-epic-deferred-dw2-admin-dapr-mcp-live-evidence: backlog
post-epic-deferred-dw3-admin-debugging-json-large-stream-hardening: backlog
post-epic-deferred-dw4-operational-evidence-schema-validation: backlog
post-epic-deferred-dw5-admin-ui-runtime-follow-ups: backlog
post-epic-deferred-dw6-deferred-work-governance: backlog
```

Rationale: Six grouped stories are small enough to plan, but broad enough to prevent 291 one-off tracking rows.

### Proposal B: DW1 Projection and Drain Hardening

Scope:

- Projection checkpoint drift when checkpoint exceeds actor sequence.
- Concurrent projection trigger/state-regression risk.
- HTTP `/project` timeout, content-type, charset, 4xx/5xx diagnostics, and cancellation classification.
- Tracker enumeration and identity-page corruption hardening.
- Event-drain poison/integrity disposition and stable activity failure reason codes.

Acceptance direction:

- Add focused tests around checkpoint drift, domain-service 4xx/5xx, invalid projection payload shapes, and per-aggregate serialization.
- Add or document explicit timeout and diagnostic policy for projection service invocation.
- Decide whether poison drain records terminally stop, back off, or move to a disposition record.

Rationale: This is the most operationally significant cluster. It affects projection freshness, eventual consistency, and recovery confidence.

### Proposal C: DW2 Admin, DAPR, and MCP Live Evidence

Scope:

- DAPR Admin runtime smoke checklist for components, actors, pub/sub, resiliency, health history, and remote metadata states.
- Epic 20 live debugging smoke checklist for blame, bisect, step-through, sandbox, and trace map against a seeded stream.
- MCP live smoke evidence: startup, `tools/list`, representative read tool, write-preview tool, and session fallback.
- NFR43 runtime evidence plan for MCP single-resource tool latency.

Acceptance direction:

- Store evidence under `_bmad-output/test-artifacts/`.
- Include Aspire resource state, endpoint URLs, commands executed, expected result, observed result, and failure classification.
- Update deferred-work entries with a disposition marker after evidence is captured.

Rationale: Runtime-sensitive features cannot be fully trusted from unit tests alone.

### Proposal D: DW3 Admin Debugging JSON and Large-Stream Hardening

Scope:

- JSON merge/delete limitations across blame, bisect, step-through, sandbox, and diff.
- Array diff semantics and nested removal detection.
- Full-stream `GetEventsAsync(0)` memory/performance limits.
- Max parameter bounds for admin debug endpoints.
- Trust-boundary note for CommandApi internal admin endpoints using `[AllowAnonymous]`.

Acceptance direction:

- Add an architecture note describing current JSON reconstruction limitations and accepted degradation.
- Add upper-bound validation to admin debug query parameters where missing.
- Decide whether actor range/snapshot-aware state APIs are required now or only documented as future performance work.

Rationale: These risks span multiple Admin debugging tools and should be handled as one system-level decision.

### Proposal E: DW4 Operational Evidence Schema Validation

Scope:

- Query operational evidence template validator.
- SignalR operational evidence template validator.
- Required-field checks, placeholder detection, classification taxonomy, and redaction rules.
- Single source of truth for duplicated reviewer checklists.

Acceptance direction:

- Add a lightweight lint script or test that fails on missing required fields and unreplaced placeholders.
- Align docs and templates so reviewer taxonomy does not drift.
- Keep Aspire-specific fields optional or profile-scoped so non-Aspire deployments can still produce valid evidence.

Rationale: Evidence templates currently rely on honor-system checks; that weakens future proof stories.

### Proposal F: DW5 Admin UI Runtime Follow-Ups

Scope:

- TypeCatalog `/types` sidebar navigation block.
- Ctrl+B sidebar toggle runtime failure.
- Remaining FluentDialog `aria-label` DOM and assistive-technology verification for CommandSandbox and EventDebugger.
- Any still-relevant post-Epic-21 runtime verification gates that are not already resolved.

Acceptance direction:

- Reproduce in a live Aspire run.
- Add focused bUnit or browser-level tests where feasible.
- Capture screenshots or DOM evidence under `_bmad-output/test-artifacts/`.
- Mark obsolete Epic 21 resolved entries as such in `deferred-work.md`.

Rationale: These are user-visible bugs or accessibility confidence gaps, not just review nits.

### Proposal G: DW6 Deferred-Work Governance

Scope:

- Add a disposition convention to `deferred-work.md`: `OPEN`, `STORY:<id>`, `ACCEPTED-DEBT`, `RESOLVED`, `DUPLICATE`, `NO-ACTION`.
- Add owner and next-review-date fields only for `OPEN` or `STORY` items.
- Add a small script or checklist to count unresolved bullets by disposition.
- Sweep old resolved entries so they no longer look open.

Acceptance direction:

- Existing raw text remains available.
- New deferrals must include disposition and recommended grouping.
- Sprint retros should summarize new `OPEN` counts and items promoted to stories.

Rationale: Without governance, the file will grow back into the same problem.

## 6. Implementation Handoff

Scope classification: Moderate.

This does not require PM/architect replanning, but it does require backlog organization plus several Developer/QA/Tech Writer follow-ups.

Recommended owners:

| Work | Owner |
| ---- | ----- |
| DW1 Projection and drain hardening | Developer + Architect review |
| DW2 Admin/DAPR/MCP live evidence | QA/Test Architect + Developer |
| DW3 Admin debugging JSON/large-stream hardening | Architect + Developer |
| DW4 Operational evidence schema validation | Test Architect + Tech Writer |
| DW5 Admin UI runtime follow-ups | Developer + UX review |
| DW6 Deferred-work governance | Developer + Product Owner |

## 7. Sprint-Status Update Plan

Do not update `sprint-status.yaml` until Jerome approves this proposal.

After approval:

1. Add the six post-epic deferred cleanup rows under a new section after Epic 21 or near other post-epic cleanup groups.
2. Set all six rows to `backlog`.
3. Update `last_updated` with a concise attribution.
4. Create story files only when the user chooses which cleanup item to run first.

## 8. Success Criteria

- Deferred work is no longer only a prose backlog.
- The top recurring risks are represented by 6 or fewer actionable stories.
- `deferred-work.md` keeps raw review evidence but gains clear disposition markers.
- Runtime-sensitive admin, DAPR, MCP, SignalR, and projection evidence has a repeatable proof path.
- No completed epic needs to be reopened.

## 9. Recommendation

Approve this proposal and begin with DW1 or DW2.

DW1 is the best engineering-risk first move because projection and drain hardening affect correctness and recovery.

DW2 is the best confidence-first move because it proves already-built Admin/DAPR/MCP/debugging features under live conditions.
