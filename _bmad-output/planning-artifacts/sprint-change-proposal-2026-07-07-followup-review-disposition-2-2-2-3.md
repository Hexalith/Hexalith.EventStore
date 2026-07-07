# Sprint Change Proposal — Follow-up-review disposition for Story 2.2 and Story 2.3

- **Date:** 2026-07-07
- **Author:** Amelia (Developer) — Correct-Course workflow
- **Trigger:** Epic 2 retrospective action item #1 (owner: Amelia) — *"Resolve the open follow-up review recommendations for Story 2.2 and Story 2.3 or record deliberate acceptance decisions."*
- **Scope classification:** **Minor** (tracking/disposition only — direct developer implementation)
- **Mode:** Batch

## Section 1 — Issue Summary

Two Epic 2 stories closed (`status: done`) with `followup_review_recommended: true`:

- **Story 2.2** — REST API Generator Discovery And Controller Emission
- **Story 2.3** — Sample External API Host Proof

The recommendation is recorded in the deferred-work ledger as two `origin: review-budget-followup`, `severity: low`, `status: open` entries:

- **DW-2** (`deferred-work.md`) — follow-up review still recommended for Story 2.2.
- **DW-3** (`deferred-work.md`) — follow-up review still recommended for Story 2.3.

Both entries state, verbatim, that the *review budget (3 cycles) was exhausted with the story finalized (status: done, verify green) while the review pass kept recommending an independent follow-up*. The flag is a **process artifact of an exhausted automated review budget, not an unaddressed defect** — it was set because the final review pass materially changed behavior (a heuristic "someone fresh should look again"), not because a specific known finding remained open.

The Epic 2 retrospective (`epic-2-retro-2026-07-07.md`) records the same action item with completion gate: *"`deferred-work.md` no longer has open follow-up-review-only entries for Story 2.2 or Story 2.3."*

## Section 2 — Impact Analysis

- **Epic impact:** None. Epic 2 is `done` and retrospected; Epic 3 planning is unaffected.
- **Story impact:** None to story scope. Both stories remain `done`. No acceptance criteria, tasks, Dev Notes, or design assumptions change. **The Correct-Course Story Rewrite Gate does not trigger** — this is a disposition decision, not an architectural pivot and it does not supersede any active story's ACs.
- **Artifact conflicts:** None. The change updates tracking metadata only: two ledger entries, two spec frontmatter flags, and one sprint-status action item.
- **Technical impact:** None to product code. No source files change.

### Convergence evidence (from the specs' Review Triage Logs)

| Story | Review passes | Total patches | HIGH findings (recent passes) | Final pass |
|-------|---------------|---------------|-------------------------------|-----------|
| 2.2 | 4 | ~30 | 0 | 5 patches (0 high / 4 med / 1 low) |
| 2.3 | 4 | ~46 | 0 | 8 patches (0 high / 6 med / 2 low) |

The reviews converged: **0 HIGH findings across every recent pass**, and the final passes only tightened bounds and added tests. No finding was left un-triaged — each was patched, deferred (tracked), or rejected.

### Substantive residuals are already tracked elsewhere (closing DW-2/DW-3 loses nothing)

Every concrete residual from the Story 2.2/2.3 review passes has its **own** deferred-work entry **and** a named-owner sprint-status action item:

| Residual finding | Ledger entry | Owning action item |
|---|---|---|
| Generated command 1 MiB request-size limit | `deferred-work.md` (Story 2.2) | REST generator hardening — Winston |
| Command-rejection extension forwarding (`rejectionType`, `correctiveAction`) | `deferred-work.md` (Story 2.2) | REST generator hardening — Winston |
| Hard-coded `/api/v1/commands/status/{id}` Location | `deferred-work.md` (Story 2.2 & 2.3) | Command-status Location policy — Winston |
| Query `ArgumentException` message sanitization | `deferred-work.md` (Story 2.2) | REST generator hardening — Winston |
| Sample DAPR app-id header append-vs-replace | `deferred-work.md` (Story 2.3) | Outbound DAPR routing-header policy — Amelia |

Each owning story touches the same generated-controller / Sample-host code and will receive fresh adversarial review when implemented, which is the natural place for the "independent look" the follow-up flag asks for.

### Acceptance evidence captured for this disposition (2026-07-07, HEAD `fc0f1de8`)

- `dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/` → **Passed: 108, Failed: 0** (Story 2.2).
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/` → **Passed: 115, Failed: 0** (Story 2.3).
- Working tree clean; no product code changed by this proposal.

## Section 3 — Recommended Approach

**Direct Adjustment — record a deliberate acceptance decision** and close DW-2 and DW-3. No further blocking review is required.

Rationale:
1. The follow-up recommendations are review-budget process artifacts, not open defects.
2. The reviews converged to 0 HIGH across multiple consecutive passes.
3. Every substantive residual is separately tracked with a named owner and will be re-reviewed with fresh context when its owning story lands.
4. Running a 5th full adversarial review now has diminishing returns and re-opens closed, retrospected Epic 2 work.

**Rejected alternative:** run one more independent review now. Not chosen — the cost/benefit is poor given (2)–(4) above, and no evidence suggests the converged passes missed a class of defect.

- **Effort:** ~1 developer session (tracking edits only).
- **Risk:** Low. Residuals remain visible and owned; nothing is silently dropped.
- **Timeline impact:** None. Unblocks closure of Epic 2 retro action item #1.

## Section 4 — Detailed Change Proposals

### 4.1 `_bmad-output/implementation-artifacts/deferred-work.md` — DW-2

```
OLD:
status: open

NEW:
status: accepted (deliberate acceptance 2026-07-07)
disposition: Correct-Course deliberate acceptance — no further blocking review required.
  Reviews converged to 0 HIGH; all substantive residuals are separately tracked
  (generated command request-size limit, command-rejection extension forwarding,
  status Location policy, query ArgumentException sanitization) under the REST
  generator hardening and command-status Location action items (owner: Winston).
  Evidence: RestApi.Generators.Tests 108/108 passed on 2026-07-07 at HEAD fc0f1de8.
  See sprint-change-proposal-2026-07-07-followup-review-disposition-2-2-2-3.md.
```

Rationale: closes the follow-up-review-only entry per the retro completion gate while preserving the audit trail.

### 4.2 `_bmad-output/implementation-artifacts/deferred-work.md` — DW-3

```
OLD:
status: open

NEW:
status: accepted (deliberate acceptance 2026-07-07)
disposition: Correct-Course deliberate acceptance — no further blocking review required.
  Reviews converged to 0 HIGH; the substantive residuals (status Location dependency,
  Sample DAPR app-id header append-vs-replace) are separately tracked under the
  command-status Location policy (owner: Winston) and outbound DAPR routing-header
  policy (owner: Amelia) action items. Evidence: Sample.Tests 115/115 passed on
  2026-07-07 at HEAD fc0f1de8.
  See sprint-change-proposal-2026-07-07-followup-review-disposition-2-2-2-3.md.
```

### 4.3 `spec-2-2-rest-api-generator-discovery-and-controller-emission.md` (frontmatter + Spec Change Log)

```
OLD (frontmatter):
followup_review_recommended: true

NEW (frontmatter):
followup_review_recommended: false
followup_review_disposition: 'accepted 2026-07-07 — deliberate acceptance via Correct-Course; see deferred-work.md DW-2 and sprint-change-proposal-2026-07-07-followup-review-disposition-2-2-2-3.md'
```

Plus a dated Spec Change Log entry recording the disposition.

### 4.4 `spec-2-3-sample-external-api-host-proof.md` (frontmatter + Spec Change Log)

```
OLD (frontmatter):
followup_review_recommended: true

NEW (frontmatter):
followup_review_recommended: false
followup_review_disposition: 'accepted 2026-07-07 — deliberate acceptance via Correct-Course; see deferred-work.md DW-3 and sprint-change-proposal-2026-07-07-followup-review-disposition-2-2-2-3.md'
```

Plus a dated Spec Change Log entry recording the disposition.

### 4.5 `sprint-status.yaml` — Epic 2 action item

```
OLD:
  - epic: 2
    action: "Resolve the open follow-up review recommendations for Story 2.2 and Story 2.3 or record deliberate acceptance decisions."
    owner: "Amelia (Developer)"
    status: open

NEW:
  - epic: 2
    action: "Resolve the open follow-up review recommendations for Story 2.2 and Story 2.3 or record deliberate acceptance decisions."
    owner: "Amelia (Developer)"
    status: done
    note: "Deliberate acceptance recorded 2026-07-07 (sprint-change-proposal-2026-07-07-followup-review-disposition-2-2-2-3). DW-2/DW-3 closed as accepted; reviews converged to 0 HIGH, all substantive residuals tracked under REST-generator-hardening, command-status-Location, and outbound-DAPR-routing-header action items. Evidence: RestApi.Generators.Tests 108/108 + Sample.Tests 115/115 green at HEAD fc0f1de8."
```

## Section 5 — Implementation Handoff

- **Scope:** **Minor** — implemented directly by the Developer (Amelia) as part of this Correct-Course run.
- **Deliverables:** this Sprint Change Proposal + the five tracking edits in Section 4.
- **Success criteria (retro completion gate):** `deferred-work.md` has no open follow-up-review-only entries for Story 2.2 or Story 2.3. Met by 4.1 and 4.2.
- **No handoff to PO/PM/Architect required.** The substantive residuals remain owned by their existing named-owner action items (Winston / Amelia); no new backlog is created by this proposal.

## Note on the parallel Epic 1 item

Story 1.7 carries an analogous open follow-up-review entry (**DW-1**) and its own open Epic 1 action item. It is **out of scope** for this proposal (Epic 1, different owner-tracked item) and is intentionally left untouched.
