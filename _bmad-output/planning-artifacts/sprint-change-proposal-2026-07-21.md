---
project: eventstore
date: 2026-07-21
workflow: bmad-correct-course
mode: batch
scope_classification: minor
status: proposed
approval: pending
trigger: commitlint header and body-line maximums must both be 200 characters
---

# Sprint Change Proposal — Commitlint 200-Character Limits

**Author:** Amelia (Developer) via `bmad-correct-course`
**Change scope:** Minor direct adjustment; no product, runtime, backlog, MVP, or sequencing change
**Status:** PROPOSED — REVIEW REQUIRED

## 1. Issue Summary

The repository commit-message contract requires commitlint to enforce an entire
header length of at most 200 characters and each body line at most 200
characters. This is a repository-governance requirement, not a product or
runtime requirement.

Commit `4f4906b3f30a3d4ed2658effc1c4f189f2f647c0` applied the requested limits on
2026-07-20 by updating `commitlint.config.mjs`, `CONTRIBUTING.md`, and the
contributor-guidance assertions in `CommitMessagePolicyTests.cs`. The active
configuration now contains:

```javascript
'header-max-length': [2, 'always', 200],
'body-max-line-length': [2, 'always', 200],
```

The implementation behaves correctly at both boundaries:

| Input | Observed result |
| --- | --- |
| 200-character header | Accepted |
| 201-character header | Rejected by `header-max-length` |
| 200-character body line | Accepted |
| 201-character body line | Rejected by `body-max-line-length` |

One verification gap remains. The focused repository contract test verifies
that contributor guidance says “200 characters or fewer,” but it does not pin
the two exact rules in `commitlint.config.mjs`. Removing either explicit rule
could restore a stricter inherited default without failing that test, leaving
the documented and executable policies inconsistent.

### Trigger Classification

- **Type:** New stakeholder repository-governance requirement.
- **Triggering story:** None; the change was applied as a direct maintenance
  commit rather than through a Phase 4 implementation story.
- **Core problem:** Make 200 the explicit mechanical ceiling for both headers
  and body lines, and prevent the configuration from drifting away from the
  documented contract.

## 2. Impact Analysis

### Epic And Story Impact

| Area | Impact | Disposition |
| --- | --- | --- |
| Epic 3 — Release And Repository Reliability | The policy belongs to the same repository-governance concern as Stories 3.7 and 3.8. | No epic or story edit. Both stories remain done; this direct maintenance correction does not change their scope or acceptance. |
| Epics 1–2 and 4–8 | No dependency on commit-message line limits. | No change. |
| Epic ordering and priorities | No implementation dependency is introduced. | No resequencing or reprioritization. |

No epic or story is added, removed, redefined, reopened, renumbered, or
deferred.

### Artifact Impact

| Artifact | Impact |
| --- | --- |
| `commitlint.config.mjs` | Already correct; retain both explicit severity-2, `always`, 200 rules. |
| `CONTRIBUTING.md` | Already correct; retain the 200-character hard ceiling and the separate preference for a concise header near 50 characters. |
| `CommitMessagePolicyTests.cs` | Add direct assertions for both exact commitlint rules. |
| PRD | No change. Commit-message formatting is not a functional or non-functional product requirement. |
| Epics | No change. Existing Epic 3 governance scope is sufficient. |
| Architecture | No change. Runtime components, contracts, topology, data, and integration decisions are unaffected. |
| UX | Not applicable. No interface, flow, accessibility, or interaction impact. |
| Project documentation | No additional change. `CONTRIBUTING.md` is the authoritative contributor-facing statement and is already aligned. |
| `sprint-status.yaml` | No change because no epic/story identity or status changes. |

### Technical Impact

- No runtime code, API, package, deployment, infrastructure, or data change.
- Local Husky and shared CI continue invoking the repository-pinned commitlint.
- Conventional Commit format, allowed types, lowercase description, trailing
  punctuation, semantic-release behavior, and the prohibition on `chore`
  remain unchanged.
- The 200-character values are hard maximums. Existing advice to prefer a
  concise header remains authoring guidance, not a conflicting lint limit.

## 3. Recommended Approach

**Selected: Option 1 — Direct Adjustment.**

- **Effort:** Low — one focused test edit and boundary verification.
- **Risk:** Low — the executable policy is already active; the follow-up only
  closes its regression-test gap.
- **Timeline impact:** None expected.
- **MVP impact:** None.
- **Sustainability:** The executable config, contributor guidance, and
  repository contract test will encode the same two limits explicitly.

**Option 2 — Potential Rollback:** Not viable. Rolling back commit `4f4906b3`
would restore the unwanted policy and create documentation/configuration
drift.

**Option 3 — PRD MVP Review:** Not applicable. The requirement affects only
repository maintenance governance and does not change product scope, goals, or
delivery sequencing.

## 4. Detailed Change Proposal

### Repository Contract Test

**File:** `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CommitMessagePolicyTests.cs`

**Method:** `CommitMessageHookExecutesCommitlintAndRemainsUnixCompatible`

**OLD:**

```csharp
commitlintConfig.ShouldContain("extends: ['@commitlint/config-conventional']");
commitlintConfig.ShouldContain("'type-enum'");
commitlintConfig.ShouldNotContain("'chore'");
```

**NEW:**

```csharp
commitlintConfig.ShouldContain("extends: ['@commitlint/config-conventional']");
commitlintConfig.ShouldContain("'type-enum'");
commitlintConfig.ShouldContain("'header-max-length': [2, 'always', 200]");
commitlintConfig.ShouldContain("'body-max-line-length': [2, 'always', 200]");
commitlintConfig.ShouldNotContain("'chore'");
```

**Rationale:** The test should fail if either hard limit is deleted, weakened,
or changed independently from the contributor-facing contract. Real commitlint
boundary checks remain the behavioral verification.

### Explicit Non-Edits

- Do not edit the already-correct values in `commitlint.config.mjs`.
- Do not duplicate the repository-specific numeric limits into the shared
  `AGENTS.md`, `CLAUDE.md`, or Copilot entry points; their contract is to
  delegate to the shared baseline and repository guidance.
- Do not edit the PRD, epics, architecture, UX, implementation story statuses,
  or shared submodules.
- Preserve all unrelated working-tree and merge-conflict state.

## 5. Implementation Handoff

**Scope classification:** Minor — direct implementation by the Developer agent
after explicit approval.

| Recipient | Responsibility |
| --- | --- |
| Developer agent | Add the two exact configuration assertions without changing the active policy or unrelated files. |
| Reviewer | Confirm the hard limits are both 200 while the concise-header preference remains non-mechanical guidance. |
| Test owner | Run the focused repository contract test and real 200/201 boundary checks. |

### Implementation Sequence

1. Add the two assertions to `CommitMessagePolicyTests.cs`.
2. Run the focused Contracts test for `CommitMessagePolicyTests`.
3. Verify real commitlint accepts 200 and rejects 201 for both the header and a
   body line.
4. Inspect the final diff and confirm no planning, runtime, dependency,
   submodule, or sprint-status files changed.

### Success Criteria

1. `commitlint.config.mjs` contains exactly:
   - `'header-max-length': [2, 'always', 200]`
   - `'body-max-line-length': [2, 'always', 200]`
2. A 200-character header passes and a 201-character header fails with
   `header-max-length`.
3. A 200-character body line passes and a 201-character body line fails with
   `body-max-line-length`.
4. The focused contract test fails if either exact configuration rule drifts.
5. `CONTRIBUTING.md` continues documenting the shared 200-character maximum
   while recommending concise headers.
6. No epic, story, PRD, architecture, UX, sprint-status, runtime, dependency,
   or submodule change is made.

## Change Analysis Checklist

### 1. Understand The Trigger And Context

- [N/A] **1.1** No triggering story; direct maintenance commit `4f4906b3`
  implements the stakeholder policy.
- [x] **1.2** Core problem classified as a repository-governance requirement
  plus a regression-verification gap.
- [x] **1.3** Evidence collected from the active config, contributor guidance,
  contract test, commit history, and real 200/201 boundary execution.

### 2. Epic Impact Assessment

- [N/A] **2.1** No trigger story requires repair; Epic 3 remains completable as
  planned.
- [x] **2.2** No epic-level change is required.
- [x] **2.3** All remaining epics reviewed; none depends on the limits.
- [N/A] **2.4** No epic is invalidated and no new epic is needed.
- [N/A] **2.5** No priority or sequencing change.

### 3. Artifact Conflict And Impact Analysis

- [x] **3.1** PRD checked; no conflict or edit.
- [x] **3.2** Architecture checked; no component, contract, topology, data, or
  technology impact.
- [N/A] **3.3** No UI/UX impact.
- [!] **3.4** The focused contract test needs exact rule assertions; config and
  contributor guidance are already aligned.

### 4. Path Forward Evaluation

- [x] **4.1** Direct Adjustment viable — Low effort / Low risk.
- [N/A] **4.2** Rollback is not viable or useful.
- [N/A] **4.3** MVP review is not applicable.
- [x] **4.4** Direct Adjustment selected as the smallest complete correction.

### 5. Sprint Change Proposal Components

- [x] **5.1** Issue summary complete.
- [x] **5.2** Epic and artifact impacts documented.
- [x] **5.3** Recommended path and alternatives documented.
- [x] **5.4** MVP unaffected; implementation sequence defined.
- [x] **5.5** Minor-scope Developer handoff defined.

### 6. Final Review And Handoff

- [x] **6.1** Applicable checklist items addressed; the remaining test action is
  explicit.
- [x] **6.2** Proposal checked against PRD, epics, architecture, UX, sprint
  status, current repository policy, and executable boundary behavior.
- [!] **6.3** Explicit Administrator approval is pending.
- [N/A] **6.4** Sprint-status update is not applicable.
- [!] **6.5** Handoff execution awaits proposal approval.

---

## Workflow Execution Log

| Date | Event | Result |
| --- | --- | --- |
| 2026-07-21 | Correct Course activated; baseline, customization, project context, and planning artifacts loaded. | Complete |
| 2026-07-21 | Administrator selected Batch mode. | Complete |
| 2026-07-21 | Change-analysis checklist completed against repository and planning evidence. | Complete |
| 2026-07-21 | Real commitlint boundary checks executed for 200/201-character headers and body lines. | Passed as expected |
| 2026-07-21 | Minor direct-adjustment proposal produced. | Awaiting review |

**Current result:** The desired 200/200 executable policy is already active.
Approval authorizes the remaining exact-rule contract-test hardening only.
