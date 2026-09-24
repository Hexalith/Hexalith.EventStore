---
title: 'Story 3.15 Tracker Lifecycle Reconciliation'
type: 'bugfix'
created: '2026-09-24'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'b15ad59abca82d5980ef92a510c2379e05f4d46f'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-23.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.15's spec is `done` and its retained verifier passes with three receipts, while the sprint tracker remains `review`. The current PRD still reports a superseded subject, zero receipts, and an `in-progress` tracker; readers cannot tell which state is current or which separate approval is pending.

**Approach:** Reconcile Story 3.15's tracker record, its pinned lifecycle test, and the dated planning account against the retained current-subject evidence and the approved 2026-09-23 handoff. Keep technical evidence validation distinct from independent high-risk approval, release availability, production promotion, and consumer-removal authority.

**Decision (2026-09-24):** Keep the tracker at `review` while the independent G-HIGH-RISK control is absent. Clarify this pending handoff in the tracker and its lifecycle guard, and correct the PRD's superseded zero-receipt account. The spec remains `done` for its bounded technical review.

## Boundaries & Constraints

**Always:** Re-run the retained validator and use its exact subject, receipt count, and selected identity. Preserve the sprint row's guarded subject-comment block and the frozen Story 3.15 packet and acceptance bytes. Keep Epic 3 `in-progress` while Story 3.16 is backlog. Record any change to the 2026-09-23 review handoff as a dated decision rather than rewriting its history.

**Never:** Treat 3/3 receipts, a spec `done` token, or a tracker edit as G-HIGH-RISK approval, `release-available`, `production-promoted`, FR36 closure, readiness, deployment, or consumer-removal authority.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Current technical packet | Subject `7d64f87e…`, three packet-bound receipts, validator exit 0 | Planning account names the verified bounded `evidence-validated` result and the chosen tracker state | Preserve separate blocked gates and authority flags |
| Superseded snapshot | PRD cites `aafe9040…`, zero receipts, and a failed verifier | Date that account as historical and identify the current packet without moving receipts | Never treat stale subject or receipt count as current |
| Missing independent control | G-HIGH-RISK matrix, validator, and sealed CI control absent | Tracker remains `review` despite the bounded spec being `done` | No inferred approval or broader completion claim |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/sprint-status.yaml:131-149` -- Story 3.15 `review` row, current-subject guarded comments, and separate Epic 3 `in-progress` row; preserve the guard's ordered text and digest.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4250-4283` -- `CorrectedLifecycleRowsRetainTheirCorrectedStatus` pins the intentional `spec=done`/`tracker=review` pair; retain the expectation and clarify the pending independent-control reason.
- `_bmad-output/implementation-artifacts/spec-3-15-corrected-deployed-runtime-parity-closure.md:525-533` -- completion history already says the tracker remains `review`; preserve this history and frozen intent.
- `_bmad-output/planning-artifacts/prd.md:328,613,644,682` -- superseded 0/3 subject, G-RUNTIME-PARITY, and OR15 account; distinguish dated history from current technical evidence and keep G-HIGH-RISK separately blocked.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-23.md:81-92` -- approved reviewer-disposition and independent-control prerequisite for a common status; preserve its historical decision.
- `tools/validate-corrected-deployed-runtime-parity.py` and `_bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json` -- read-only current technical proof; selected identity is the pinned OCI index.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- retain `review` and add the missing pending G-HIGH-RISK handoff reason while preserving guarded subject comments and unrelated rows.
- [ ] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs` -- keep the existing `review`/`done` assertions and explain the pending independent-control reason.
- [ ] `_bmad-output/planning-artifacts/prd.md` -- date the superseded Story 3.15 snapshot and state the current subject, 3/3 technical result, and still-open independent/authority gates.

**Acceptance Criteria:**
- Given the current packet, when the retained Story 3.15 verifier runs, then it passes on exactly the existing subject and selected OCI index without changing packet bytes.
- Given the pending independent control, when the focused Contracts lifecycle test runs, then tracker `review` and spec `done` remain pinned to the recorded decision.
- Given the PRD's former zero-receipt snapshot, when a reader examines Story 3.15 today, then current technical evidence and pending independent authority are distinguishable without asserting release, promotion, readiness, or consumer-removal approval.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `python3 tools/validate-corrected-deployed-runtime-parity.py _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json --packet-root _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d` -- expected: exit 0, current subject `7d64f87e…` and pinned OCI index.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release` -- expected: zero errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.DeployedRuntimeParityClosureTests -method CorrectedLifecycleRowsRetainTheirCorrectedStatus -noLogo` -- expected: pass.
- `git diff --check` -- expected: no whitespace errors.
