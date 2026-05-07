# Sprint Change Proposal: Deferred-Work OPEN Cleanup (DW7-DW9)

Date: 2026-05-07
Project: Hexalith.EventStore
Trigger: 9 `disposition: [OPEN]` entries in `_bmad-output/implementation-artifacts/deferred-work.md` after the DW1-DW6 cleanup package closed.
Mode: Batch
Prepared by: Amelia (Developer)

## 1. Issue Summary

The DW1-DW6 grouped cleanup package (sprint-change-proposal-2026-05-04-deferred-work-triage.md) closed 2026-05-06 and emptied `sprint-status.yaml` of in-flight rows. The DW6 governance sweep classified the bulk of `deferred-work.md`, but 9 entries remain `[OPEN]` and 2 of them are real Admin UI bugs (infinite-retry + stuck-open dialog gate in `StateInspectorModal`). Without a routing decision the open items will drift back into the same prose-backlog problem the DW1-DW6 package was meant to solve.

This is not a product or scope change. It is a planning correction that converts the remaining 9 OPEN deferrals into three small follow-up stories plus one operator action.

## 2. Checklist Findings

| Item | Status | Finding |
| ---- | ------ | ------- |
| 1.1 Triggering issue | Done | 9 entries in `deferred-work.md` carry `disposition: [OPEN]` (governance counter at 2026-05-06: 6 OPEN; 3 more added by the 2026-05-07 admin-ui-state-inspection-cluster-fix code review). |
| 1.2 Core problem | Done | Routing/process: open items mix server hardening, Admin UI bugs, validator polish, and CI tooling polish; no story owns them. |
| 1.3 Evidence | Done | Items at `deferred-work.md:38, 39, 44, 528, 569, 570, 575, 601, 602`. Identified by `grep "disposition: \[OPEN\]" _bmad-output/implementation-artifacts/deferred-work.md`. |
| 2.1 Current epic impact | N/A | All numbered epics (1-21) and post-epic packages are `done`. No active epic blocked. |
| 2.2 Epic-level changes | N/A | No completed epic must be reopened. |
| 2.3 Remaining epics | Done | No planned epic invalidated. |
| 2.4 New epic needed | Not viable | Three small stories suffice. |
| 2.5 Priority/order | Done | Admin UI bug fix first (real user-visible defect), then server hardening, then governance polish. |
| 3.1 PRD impact | Done | None. Strengthens existing reliability and developer-experience goals. |
| 3.2 Architecture impact | Done | One small note: drain failure-classifier taxonomy (DrainReason* values) is the public observability contract; document the addition. |
| 3.3 UX impact | Done | StateInspectorModal lifecycle bug fix has no UX redesign. Item #7 (manual Aspire smoke evidence) requires operator browser action; not implementable headless. |
| 3.4 Other artifacts | Action-needed | `sprint-status.yaml`, `deferred-work.md` (mark items routed), and the DW6 checker counts after sweep. |
| 4.1 Direct adjustment | Viable | Best path. Three grouped stories. Effort small-medium, risk low. |
| 4.2 Rollback | Not viable | Nothing to roll back. |
| 4.3 MVP review | Not viable | MVP unchanged. |
| 4.4 Recommended path | Done | Direct adjustment via DW7+DW8+DW9 plus one operator-action follow-up. |
| 5.1-5.5 Proposal components | Done | Below. |
| 6.1-6.5 Approval/handoff | Action-needed | Awaits Jerome approval before sprint-status edits and story-file creation. |

## 3. Impact Analysis

### Epic Impact

None. All numbered epics 1-21 and post-epic packages DW1-DW6 are `done`. The proposal extends the post-epic deferred cleanup package with DW7-DW9.

### PRD Impact

No PRD requirement removed or redefined. Supports existing goals:

- Reliability: clearer drain failure classification reduces `drain_failure_unknown` ambiguity in production traces.
- Identifier correctness: ULID enforcement on `correlationId`/`messageId`/`aggregateId`/`causationId` preserves R2-A7.
- Developer experience: closes the lifecycle-bug feedback loop on `StateInspectorModal`.

### Architecture Impact

One clarifying note only:

- Drain failure-classifier expansion (DrainReason*) is part of the public observability contract; document the new reason codes and their stable wire form before code adds them. No new component or boundary.

### UI/UX Impact

- **StateInspectorModal lifecycle**: real bug, behavior-only fix; no UX redesign.
- **AC #9 manual Aspire smoke evidence**: not implementable from a headless dev agent. Tracked as a scheduled operator action, not a story.

### Testing and Evidence Impact

- DW7 needs bUnit coverage for `OnAfterRenderAsync` failure path (currently classified `[ACCEPTED-DEBT]` at `deferred-work.md:610`; promote to `[STORY:dw7]` when the fix lands).
- DW8 needs an identifier-validation regression test that asserts `Ulid.TryParse` (not `Guid.TryParse`) on the four protected fields, plus a drain-classifier listener test for each new reason code.
- DW9 needs validator fixture additions (cross-field control linkage + template skip-list) and CI workflow alignment with the `check-deferred-work` wrapper.

## 4. Recommended Approach

Direct adjustment through three grouped post-epic stories plus one operator action.

Priority order:

1. **DW7** (Admin UI lifecycle bug) - real user-visible defect, smallest blast radius.
2. **DW8** (Server hardening: drain classifier + ULID identifier audit) - correctness; both items touch the server bucket.
3. **DW9** (Evidence validator + governance CI/docs polish) - tooling polish; lowest urgency.
4. **Operator action OA1** - manual Aspire smoke for `StateInspectorModal` clipping evidence; not a story.

## 5. Detailed Change Proposals

### Proposal A: Add DW7-DW9 follow-up rows to `sprint-status.yaml`

OLD (lines 321-328):

```yaml
  # Post-Epic Deferred Work Cleanup (sprint-change-proposal-2026-05-04-deferred-work-triage.md)
  # Grouped cleanup package for deferred-work.md. Create story files only when a bucket is selected for execution.
  post-epic-deferred-dw1-projection-and-drain-hardening: done
  post-epic-deferred-dw2-admin-dapr-mcp-live-evidence: done
  post-epic-deferred-dw3-admin-debugging-json-large-stream-hardening: done
  post-epic-deferred-dw4-operational-evidence-schema-validation: done
  post-epic-deferred-dw5-admin-ui-runtime-follow-ups: done
  post-epic-deferred-dw6-deferred-work-governance: done
```

NEW:

```yaml
  # Post-Epic Deferred Work Cleanup (sprint-change-proposal-2026-05-04-deferred-work-triage.md)
  # Grouped cleanup package for deferred-work.md. Create story files only when a bucket is selected for execution.
  post-epic-deferred-dw1-projection-and-drain-hardening: done
  post-epic-deferred-dw2-admin-dapr-mcp-live-evidence: done
  post-epic-deferred-dw3-admin-debugging-json-large-stream-hardening: done
  post-epic-deferred-dw4-operational-evidence-schema-validation: done
  post-epic-deferred-dw5-admin-ui-runtime-follow-ups: done
  post-epic-deferred-dw6-deferred-work-governance: done

  # Post-Epic Deferred Work OPEN Cleanup (sprint-change-proposal-2026-05-07-deferred-work-open-cleanup.md)
  # Routes the 9 remaining [OPEN] items in deferred-work.md into three follow-up stories.
  # OA1 (manual Aspire smoke evidence for StateInspectorModal) is an operator action, not a story.
  post-epic-deferred-dw7-admin-ui-state-inspector-lifecycle-fix: backlog
  post-epic-deferred-dw8-server-classifier-and-ulid-audit: backlog
  post-epic-deferred-dw9-evidence-validator-and-governance-polish: backlog
```

Rationale: Three small stories cover items #1-6, #8, #9. Item #7 stays in `deferred-work.md` with disposition flipped from `[OPEN]` to `[ACCEPTED-DEBT]` plus a scheduled operator-action review date.

### Proposal B: DW7 - Admin UI StateInspectorModal Lifecycle Fix

Story key: `post-epic-deferred-dw7-admin-ui-state-inspector-lifecycle-fix`

Scope:

- Fix `OnAfterRenderAsync` infinite-retry: ensure `_pendingShow=false` and `_fetchError` is set without re-triggering `ShowAsync` on the next render. (`deferred-work.md:601`, `StateInspectorModal.razor` `OnAfterRenderAsync`).
- Fix stuck-open host gate: when `ShowAsync` throws, fire `OnDialogStateChange.Closed` so the host page's "show modal" state collapses. (`deferred-work.md:602`, same component).
- Promote the `[ACCEPTED-DEBT]` test-coverage bullet at `deferred-work.md:610` ("No bUnit coverage for `OnAfterRenderAsync` `ShowAsync` failure path") to `[STORY:post-epic-deferred-dw7-admin-ui-state-inspector-lifecycle-fix]` and add focused bUnit tests covering: (a) JSDisconnectedException during `ShowAsync` does not loop, (b) host `OnDialogStateChange.Closed` fires once on `ShowAsync` failure, (c) `_fetchError` rendered without re-entry.

Acceptance direction:

- Add a JSInterop-mocked bUnit test that throws `JSDisconnectedException` from `ShowAsync` and asserts `_pendingShow == false` after the next render and the failure path runs exactly once.
- Add a host-gate test that observes `OnDialogStateChange.Closed` fires once when `ShowAsync` throws.
- Validation: `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/` green.
- Update disposition markers in `deferred-work.md` lines 601-602 from `[OPEN]` to `[STORY:post-epic-deferred-dw7-admin-ui-state-inspector-lifecycle-fix]` (then `[RESOLVED]` after the patch lands).

Rationale: Real user-visible Admin UI defect found by code review on 2026-05-07. Smallest blast radius; one component, paired fix.

### Proposal C: DW8 - Server Drain Classifier + ULID Identifier Audit

Story key: `post-epic-deferred-dw8-server-classifier-and-ulid-audit`

Scope:

- **Drain classifier expansion** (`deferred-work.md:44`, `AggregateActor.cs:829-834`): add stable reason codes for `drain_state_store_failure`, `drain_dapr_unavailable`, and any other categories observed in DW1 follow-up traces; keep `drain_failure_unknown` as the residual bucket only. Add architecture note documenting the public reason-code taxonomy.
- **ULID identifier audit** (`deferred-work.md:528`, DW2-DF5): inspect every parser/validator that touches `messageId`, `correlationId`, `aggregateId`, `causationId`. Confirm `Ulid.TryParse` is used (or any non-whitespace string per `AggregateIdentity` rules) and that `Guid.TryParse` is forbidden. Fix the seeded-stream summary fixture or document the acceptance rationale. Cross-check against CLAUDE.md R2-A7.

Acceptance direction:

- Add an activity-tag listener test that exercises each new `DrainReason*` code path and asserts the stable wire value.
- Add a parser audit test (or a Roslyn analyzer hit list) that fails the build if `Guid.TryParse` is reintroduced on the four protected fields.
- Architecture note added under `docs/architecture/` (or appended to existing observability section) describing the reason-code taxonomy.
- Validation: `dotnet test tests/Hexalith.EventStore.Server.Tests/` (Tier 2, requires `dapr init`) green; identifier audit grep clean.
- Update disposition markers in `deferred-work.md` lines 44 and 528 from `[OPEN]` to `[STORY:post-epic-deferred-dw8-server-classifier-and-ulid-audit]`.

Rationale: Both items are server correctness in the same operational bucket. Combining them prevents two near-identical reviewer cycles on the same files.

### Proposal D: DW9 - Evidence Validator + Governance CI/Docs Polish

Story key: `post-epic-deferred-dw9-evidence-validator-and-governance-polish`

Scope:

- **Validator: cross-field control linkage** (`deferred-work.md:38`, AC #6 partial): add a fixture and rule that asserts a control's observed result is tied to the same run or a clearly linked control run. Extends `scripts/validate-operational-evidence.py`.
- **Validator: template skip-list** (`deferred-work.md:39`): add `<!-- evidence-validator: skip -->` opt-out marker support and a default `**/*-template.md` skip-list, so Task 4.6's repo-wide audit mode does not pollute results with placeholder templates.
- **DW6 CI symmetry** (`deferred-work.md:569`, DW6-CR5): change `.github/workflows/docs-validation.yml:43` to invoke the wrapper (`scripts/check-deferred-work.ps1` or `.sh`) instead of `python scripts/check-deferred-work.py` directly, so wrapper bugs are caught in CI.
- **DW6 entrypoint convention** (`deferred-work.md:570`, DW6-CR6): either add `_bmad-output/test-artifacts/deferred-work-governance/entrypoint.txt` to `.gitignore` or update the README to declare the committed value canonical. Pick one and align both sides.

Acceptance direction:

- Validator fixture additions exercise both new rules; self-test count incremented.
- CI workflow change verified by a PR-level dry run (manual workflow_dispatch or branch trigger); wrapper exit code surfaces correctly.
- README/`.gitignore` reconciled; one source of truth for entrypoint policy.
- Update disposition markers in `deferred-work.md` lines 38, 39, 569, 570 from `[OPEN]` to `[STORY:post-epic-deferred-dw9-evidence-validator-and-governance-polish]`.

Rationale: Tooling polish. Lowest urgency; safe to defer behind DW7/DW8 but worth grouping into one story so the validator script is opened only once.

### Proposal E: Operator Action OA1 - Manual Aspire Smoke for StateInspectorModal Clipping

Not a story. The `[OPEN]` entry at `deferred-work.md:575` requires a human at a browser to verify that `StateInspectorModal` body content is not clipped at standard viewport sizes (AC #9 of admin-ui-state-inspection-cluster-fix).

Action: keep the entry in `deferred-work.md`, flip disposition from `[OPEN]` to `[ACCEPTED-DEBT]` with `owner: Operator` and `next-review-date: 2026-06-15`. Capture the screenshot evidence under `_bmad-output/test-artifacts/admin-ui-state-inspection-cluster-fix/manual-smoke/` when the smoke is run.

Rationale: No code change is possible from this dev loop. Tracking it as accepted-debt-with-target preserves the audit trail.

## 6. Implementation Handoff

Scope classification: **Moderate**.

Three Developer-owned stories plus one operator action. No PM/architect replanning.

Recommended owners:

| Work | Owner |
| ---- | ----- |
| DW7 admin-ui state-inspector lifecycle fix | Developer + UX review |
| DW8 server classifier + ULID audit | Developer + Architect review |
| DW9 evidence validator + governance polish | Developer (Test Architect for validator rules) |
| OA1 operator manual smoke | Operator / Jerome |

## 7. Sprint-Status Update Plan

Do not update `sprint-status.yaml` until Jerome approves this proposal.

After approval:

1. Append the three DW7-DW9 backlog rows under the existing post-epic deferred cleanup section (after line 328).
2. Update `last_updated` with a concise attribution that points at this proposal.
3. Create story files only when a bucket is selected for execution (same convention as DW1-DW6).
4. Flip OA1 disposition in `deferred-work.md:575` from `[OPEN]` to `[ACCEPTED-DEBT]` once approval is recorded; do not wait for DW7 to start.
5. Run `scripts/check-deferred-work` after the sweep and record the new OPEN count in the proposal's completion note.

## 8. Success Criteria

- `deferred-work.md` OPEN count drops from 9 to 0 (8 routed to DW7-DW9 stories, 1 flipped to ACCEPTED-DEBT with owner+date).
- DW7 ships the lifecycle fix and removes the infinite-retry / stuck-gate behavior.
- DW8 lands a public reason-code taxonomy and an identifier-parser audit guard.
- DW9 closes the validator and CI gaps without breaking the existing fixture suite.
- No completed epic needs to be reopened.

## 9. Recommendation

Approve this proposal and start with DW7.

DW7 is a real Admin UI defect with a paired fix; the patch is small and the test scaffolding is straightforward. DW8 and DW9 can follow in either order, but DW8 should land before any DW9 work that touches drain evidence.

## 10. Approval

- [x] Approved by Jerome on 2026-05-07
- [x] sprint-status.yaml updated with DW7-DW9 backlog rows under existing post-epic deferred cleanup section; `last_updated` advanced to 2026-05-07T18:00:00+02:00 with attribution to this proposal.
- [x] OA1 disposition flipped to `[ACCEPTED-DEBT]` at `deferred-work.md:575` with `owner: Operator`, `next-review-date: 2026-06-15`, and approval reference.
- [x] Eight remaining `[OPEN]` bullets re-routed to `[STORY:post-epic-deferred-dw7-...]` / `[STORY:post-epic-deferred-dw8-...]` / `[STORY:post-epic-deferred-dw9-...]` with approval reference.
- [x] DW6 checker rerun: post-sweep counts OPEN 0, STORY 25, ACCEPTED-DEBT 44, RESOLVED 24, DUPLICATE 3, NO-ACTION 3, unclassified 298, exit 0 (advisory legacy-unclassified diagnostics only).
- [ ] Story files for DW7-DW9 to be created when each bucket is selected for execution (same convention as DW1-DW6).
