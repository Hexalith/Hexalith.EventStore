---
title: 'Story 3.15 Tracker Lifecycle Reconciliation'
type: 'bugfix'
created: '2026-09-24'
status: 'done'
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
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- retain `review` and add the missing pending G-HIGH-RISK handoff reason while preserving guarded subject comments and unrelated rows.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs` -- keep the existing `review`/`done` assertions and explain the pending independent-control reason.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs` -- bind the PRD's current subject and pending gate to packet evidence so the historical 0/3 account cannot silently become current again.
- [x] `_bmad-output/planning-artifacts/prd.md` -- date the superseded Story 3.15 snapshot and state the current subject, 3/3 technical result, and still-open independent/authority gates.

**Acceptance Criteria:**
- Given the current packet, when the retained Story 3.15 verifier runs, then it passes on exactly the existing subject and selected OCI index without changing packet bytes.
- Given the pending independent control, when the focused Contracts lifecycle test runs, then tracker `review` and spec `done` remain pinned to the recorded decision.
- Given the PRD's former zero-receipt snapshot, when a reader examines Story 3.15 today, then current technical evidence and pending independent authority are distinguishable without asserting release, promotion, readiness, or consumer-removal approval.

## Implementation Notes

- 2026-09-24: The tracker comment, lifecycle-test comment, and refreshed Epic 3 context entered current `main` through an external update at `ab40348def51e03dc020e589dfc183ce2e621409` during implementation. Preserved that commit and its unrelated submodule updates. The PRD reconciliation and packet-bound PRD guard remain in the worktree.
- The retained parity verifier passed on subject `7d64f87e3e6d85163651e7748c751222ca1f0fb4f0c47f21408a2bde4eba5274` and selected only index `sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`. Release Contracts build: zero warnings and errors. Focused lifecycle/PRD tests: 2/2 passed; checked-in packet positive test: 1/1 passed.
- The unqualified method filter in the first verification attempt selected zero tests; the command below uses fully qualified method names. A broader Contracts run encountered OQ8 v5's clean-committed-checkout precondition while the intended PRD edit was uncommitted and was stopped after those unrelated failures. It does not substitute for the passing focused evidence.
- 2026-09-26: The owner renegotiated the Decision (2026-09-24) through `_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-26.md`. The tracker is now `done` for FR36-C2 evidence validation only; G-RUNTIME-PARITY and G-HIGH-RISK stay blocked, and backlog Story 9.2 owns G-HIGH-RISK. The frozen Decision text above is preserved as history.

## Spec Change Log

## Review Triage Log

- **medium · patch · blind 1:** The refreshed Epic 3 context says the Story 3.15 technical pass includes a public API/wire baseline, while PRD G-COMPAT explicitly says that baseline and enforcing lane are absent. This could let a later handoff cite parity as compatibility proof; remove that assertion and name G-COMPAT as separate.
- **medium · defer · blind 2:** `epics.md` still defines FR36 completion using only source/package and deployed-runtime parity although the newer PRD requires release, promotion, and per-consumer authority. The epics rule predated this tracker reconciliation and needs the separate approved baseline review.
- **medium · defer · blind 3:** Story 3.15's `epics.md` acceptance says three receipts complete its bounded result without naming the separate tracker review handoff. That older acceptance predates the present change; the approved planning proposal already assigns epics reconciliation to a later owner review.
- **false · rejected · blind 4:** The PRD still retains the old publication-authority path and validator name as dated historical text and explicitly leaves the current-subject record unapproved; it does not grant or silently replace the blocked publication gate. Keeping the old path as a current executable requirement would point at a superseded subject.
- **medium · defer · blind 5:** The PRD's Story 5.4 OR15 wording still calls its older `review`/`in-progress` values current although the tracker and wrapper now say `done`. This pre-existing unrelated story lifecycle needs Story 5.4 owner disposition; the edited §11.3 table explicitly dates its other rows to 2026-09-10.
- **low · patch · blind 6:** The PRD now uses the approved 2026-09-23 proposal but omits it from `source_artifacts`; adding that source makes the basis of this reconciliation traceable.
- **medium · patch · blind 7:** The new PRD guard reads only the packet subject, so a changed receipt count or selected index could leave its documentation assertions green. Bind those values to the packet and retain the existing positive verifier test.
- **medium · patch · blind 8:** The new guard checks the high-risk blocked text but not the changed PRD's publication/consumer gate claims; a contradictory authority claim could pass this focused check. Assert their blocked state and the packet's false authority flags.
- **low · patch · blind 9:** The lifecycle guard pins the Story 3.15 status pair but not the new tracker explanation; deleting that explanation would leave its purpose unclear while the test passes. Assert the pending-control text.
- **medium · defer · verification-gap other 1:** The externally updated FrontComposer gitlink selects EventStore `b15ad59a`, while its main-push successor gate defaults to `bf03d57c`; the checked-out source comparison will reject that pair. This arose in an unrelated external submodule update, so its owning repository must reconcile the successor pin.
- **medium · patch · edge 1:** PRD G-RUNTIME-PARITY requires the role registry itself to name the subject, but the registry schema has no subject field; the subject instead binds the registry SHA-256 at `subject.json:authority.owner_role_registry_sha256`. State that actual verifier relationship.
- **medium · patch · edge 2:** The new test separately finds superseded and current subjects and `3 of 3` on one PRD line, so it could accept receipts attributed to the old subject. Assert the current dated clause pairs the current subject with the three receipts.

## Verification

**Commands:**
- `python3 tools/validate-corrected-deployed-runtime-parity.py _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json --packet-root _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d` -- expected: exit 0, current subject `7d64f87e…` and pinned OCI index.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release` -- expected: zero errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method Hexalith.EventStore.Contracts.Tests.Packaging.DeployedRuntimeParityClosureTests.CorrectedLifecycleRowsRetainTheirCorrectedStatus -method Hexalith.EventStore.Contracts.Tests.Packaging.CorrectedDeployedRuntimeParityClosureTests.PlanningRuntimeParityAccountMatchesCurrentPacketAndPendingControl -noLogo` -- expected: both pass.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method Hexalith.EventStore.Contracts.Tests.Packaging.CorrectedDeployedRuntimeParityClosureTests.CheckedInPacketClosesAtThreeRosterBoundReceipts -noLogo` -- expected: pass.
- `git diff --check` -- expected: no whitespace errors.
