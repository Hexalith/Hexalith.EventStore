# Sprint Change Proposal — Outbound DAPR Routing-Header Policy Closure

- **Date:** 2026-07-13
- **Author:** Developer
- **Trigger:** Reassess and enforce the Epic 2 retrospective action, "Decide and enforce the outbound DAPR routing-header replacement policy for generated API host clients."
- **Workflow:** `bmad-correct-course` (Incremental mode)
- **Status:** **Approved and implemented**
- **Approval:** User approved on 2026-07-13
- **Change scope classification:** **Minor** — implementation is complete; only stale sprint bookkeeping remains.
- **Approved incremental decision:** Close the shipped action and Epic 2 bookkeeping without changing production code, PRD scope, architecture, stories, or UX.

---

## Section 1 — Issue Summary

### Original issue

Story 2.3 review found that host-local outbound DAPR handlers used bare
`TryAddWithoutValidation` calls for `dapr-app-id` and `dapr-api-token`. A request that already
carried either header could therefore reach the sidecar with duplicate values. These are
sidecar control-plane headers and must be owned authoritatively by the outbound client transport,
not influenced by caller-supplied or inbound-forwarded values.

The 2026-07-07 correction decided the policy and formalized architecture invariant AD-18:

1. Remove any existing `dapr-app-id`, then set exactly one configured app id.
2. Remove any existing `dapr-api-token`; set the configured token only when present.
3. Register the platform handler last so it is innermost and has final ownership after forwarding handlers.
4. Prevent hosts from reintroducing local routing-header handlers or bare setters.

### Current trigger

Story 2.7 implemented that policy on 2026-07-10 and is recorded `done`, but the corresponding
retrospective action and `epic-2` remain `in-progress` in `sprint-status.yaml`. The action note
explicitly says it closes when Story 2.7 ships. The remaining problem is therefore stale planning
state, not missing EventStore production implementation.

### Current evidence

- `Hexalith.EventStore.Client` contains the canonical internal
  `DaprServiceInvocationHandler`, which removes then sets `dapr-app-id`, removes
  `dapr-api-token` unconditionally, and adds the configured token only when present.
- `AddEventStoreDaprServiceInvocation` exposes the single supported wiring seam and documents
  that it must be registered last/innermost.
- Sample.Api, Sample.BlazorUI, and Admin.UI use the platform extension; their three local
  `DaprAppIdHandler` files are absent.
- Replacement, token-stripping, handler-ordering, and structural guardrail tests exist.
- Fresh 2026-07-13 Release verification passed:
  - `Hexalith.EventStore.Client.Tests`: 651 passed, 0 failed.
  - `Hexalith.EventStore.Sample.Tests`: 117 passed, 0 failed.
  - `Hexalith.EventStore.Admin.UI.Tests`: 841 passed, 0 failed.
- The Tenants submodule still carries its historical local append-only handler. Story 2.7 and
  the deferred-work ledger explicitly classify that copy as a coordinated follow-up requiring
  submodule maintainer approval; this proposal preserves that boundary and does not modify the submodule.

---

## Section 2 — Impact Analysis

### Epic impact

- **Epic 2:** All Stories 2.1-2.8 and the Epic 2 retrospective are `done`. Once the stale action
  is closed, Epic 2 can move from `in-progress` to `done`.
- **Epics 1 and 3-7:** No scope, sequence, status, or dependency changes are required.
- **Epic 5:** AD-18 continues to reinforce its trust-boundary posture; no Epic 5 edit is required.

### Story impact

- Story 2.7 remains `done`; its acceptance criteria and implementation record require no change.
- Stories 2.3 and 2.4 remain historical completed stories and are not rewritten.
- No new EventStore story is required.
- The Tenants submodule work remains the existing coordinated maintainer-approval follow-up.

### Artifact conflicts

| Artifact | Impact |
| --- | --- |
| `sprint-status.yaml` | Stale `epic-2` and retrospective-action statuses must be closed and the action note updated with completion evidence. |
| `architecture.md` | No change; AD-18 already governs the final policy. |
| `epics.md` | No change; Story 2.7 already contains the required acceptance criteria. |
| `prd.md` | No change; MVP scope and functional requirements remain valid. |
| `ux.md` | No change; there is no UI behavior impact. |
| `project-context.md` | No change; the AD-18 agent guardrail is already present. |
| `deferred-work.md` | No change; the Tenants follow-up and low-risk guardrail hardening items are already recorded. |

### Technical impact

No EventStore production-code, test-code, topology, package, deployment, or API-contract change is
required. The implementation is present and the focused Release gates are green.

---

## Section 3 — Recommended Approach

**Selected path: Option 1 — Direct Adjustment.** Reconcile `sprint-status.yaml` with the shipped
and verified state by closing the target action and Epic 2.

- **Effort:** Low.
- **Risk:** Low.
- **Timeline impact:** None.
- **MVP impact:** None.

### Alternatives considered

- **Potential rollback:** Not viable or useful. The centralized replacement behavior is correct,
  shipped, and tested.
- **MVP review:** Not applicable. The correction does not change product scope.
- **New implementation story:** Not required. Story 2.7 already delivered the EventStore-owned work;
  duplicating it would create false backlog state.
- **Silent Tenants submodule edit:** Rejected. Submodule changes require explicit maintainer approval
  and are already tracked as a coordinated follow-up.

---

## Section 4 — Detailed Change Proposal

### Sprint status — Epic 2 and routing-header action

**Artifact:** `_bmad-output/implementation-artifacts/sprint-status.yaml`

**OLD:**

```yaml
epic-2: in-progress

- epic: 2
  action: "Decide and enforce the outbound DAPR routing-header replacement policy for generated API host clients."
  owner: "Amelia (Developer)"
  status: in-progress
  note: "Policy DECIDED 2026-07-07 ... Closes to done when Story 2.7 ships."
```

**NEW:**

```yaml
epic-2: done

- epic: 2
  action: "Decide and enforce the outbound DAPR routing-header replacement policy for generated API host clients."
  owner: "Amelia (Developer)"
  status: done
  note: "Completed by Story 2.7 on 2026-07-10. AD-18 is implemented by the centralized Client handler; the three in-repository hosts use it and replacement, stripping, ordering, and structural guardrails are verified. Fresh 2026-07-13 Release evidence: Client 651/651, Sample 117/117, Admin UI 841/841. The Tenants submodule copy remains the explicitly coordinated maintainer-approval follow-up."
```

**Rationale:** The implementation completion gate named by the current note has been met. Keeping
the action or epic open misrepresents shipped scope and obscures the genuinely separate Tenants
submodule follow-up.

---

## Section 5 — Implementation Handoff

- **Scope:** Minor.
- **Recipient:** Developer for the approved `sprint-status.yaml` reconciliation.
- **Product Owner responsibility:** Treat Epic 2 as complete under the EventStore repository scope;
  keep the Tenants submodule follow-up visible until its maintainers authorize that separate change.
- **Architect responsibility:** None for this correction; AD-18 is already adopted.

### Success criteria

1. The target retrospective action is `done` and its note cites Story 2.7 plus fresh verification.
2. `epic-2` is `done`, consistent with its completed stories and retrospective.
3. No production code, tests, PRD, architecture, epics, UX, or submodule files are changed.
4. The Tenants coordinated follow-up remains explicit and is not falsely reported as completed.

### Workflow execution log

- 2026-07-13 — User approved the incremental `sprint-status.yaml` edit.
- 2026-07-13 — User continued after reviewing the complete Sprint Change Proposal.
- 2026-07-13 — User explicitly approved the proposal for implementation.
- 2026-07-13 — Developer handoff completed: the target action and Epic 2 were changed to `done`.
- 2026-07-13 — No production code, tests, PRD, architecture, epics, UX, or submodule files were changed.

---

*Produced by `bmad-correct-course` (Incremental mode), 2026-07-13.*
