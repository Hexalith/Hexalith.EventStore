# Sprint Change Proposal — Epic 2 Retrospective Carry-Over Fixes

**Date:** 2026-04-26
**Author:** Bob (Scrum Master)
**Project Lead:** Jerome
**Trigger:** Epic 2 retrospective (`epic-2-retro-2026-04-26.md`) action items R2-A1..R2-A10
**Mode:** Batch
**Scope Classification:** **Moderate** — backlog reorganization (3 new fix stories) + Epic 3 spec amendments (3 stories) + 1 retrospective correction. No PRD or architecture redirection.

---

## 1. Issue Summary

Epic 2 retrospective on 2026-04-26 produced 10 action items. Three are critical before Story 3.1 starts (R2-A1, R2-A7, R2-A8); four more must clear before Epic 3 closes (R2-A2, R2-A3, R2-A4, R2-A5, R2-A9); three are process changes (R2-A6, R2-A10, plus R2-A3/A4 routing).

This proposal also **corrects a factual error in `epic-2-retro-2026-04-26.md`** discovered while drafting:

> Retro §2 ("What Went Well") and §6 (R2-A1) claim Story 2.2 cleared the Epic 1 D1-1 carry-over (`AggregateType: "unknown"` placeholder).
>
> **This is wrong.** Story 2.2's senior-review patch changed `EventEnvelope.AggregateType` from `"unknown"` → `identity.Domain`. Per Story 1.1 dev notes (and the existing `sprint-change-proposal-2026-04-26.md` for Epic 1 cleanup), Domain (e.g., `counter-domain`) and AggregateType (e.g., `counter`) are intentionally separate fields. Story 2.2's fix replaced one wrong value with another.
>
> The standalone `post-epic-1-r1a1-aggregatetype-pipeline` story (already in backlog) is **not** duplicative — it is the real fix. R2-A1 must be reframed: do NOT mark it `done`; do NOT close as duplicate. Confirm the story is still scheduled, and ensure its acceptance criteria reject *both* `"unknown"` and `identity.Domain` as `AggregateType` values.

| Action | Symptom | Verified location | Status |
|--------|---------|--------------------|--------|
| R2-A1 (corrected) | `EventEnvelope.AggregateType` populated from `identity.Domain` instead of registered aggregate type. Story 2.2's patch fixed the placeholder symptom but not the semantic bug | `src/Hexalith.EventStore.Server/Events/EventPersister.cs:83`<br>`src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs:61` | Already covered by `post-epic-1-r1a1-aggregatetype-pipeline` (backlog) |
| R2-A2 | `CommandStatus.IsTerminal()` extension never shipped. Two controllers each carry a private `IsTerminalStatus(...)` helper | `src/Hexalith.EventStore/Controllers/CommandStatusController.cs:171`<br>`src/Hexalith.EventStore/Controllers/AdminTraceQueryController.cs:223` | Needs new fix story |
| R2-A7 | Epic 3 stories 3.2 / 3.3 / 3.5 will exercise the same controller-validation seam where Story 2.4's GUID-only ULID-rejection bug lived. No story spec currently rules `Guid.TryParse` out for ID validation | `_bmad-output/planning-artifacts/epics.md` (Epic 3 stories) | Needs spec amendments |
| R2-A8 | 4 Tier 2 tests fail in `Server.Tests` with `SubmitCommandHandler.Handle` `NullReferenceException`. Pipeline scope. Tests: `PayloadProtectionTests`, `CausationIdLoggingTests`, `LogLevelConventionTests`, `StructuredLoggingCompletenessTests`. Failures stable across all of Epic 2 (4 stories logged the same count) | `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs` (line numbers shifted; failure signal is the 4 named tests) | Needs new fix story |
| R2-A5 | DAPR SDK version drift: `CLAUDE.md` says 1.17.0, `Directory.Packages.props` pins 1.16.1 | `CLAUDE.md`, `Directory.Packages.props` | One-line fix |
| R2-A3 | R1-A2 + R1-A6 (compliance helper + missing-Apply exception) still in backlog as `post-epic-1-r1a2-...` and `post-epic-1-r1a6-...` | `_bmad-output/implementation-artifacts/sprint-status.yaml` lines 263–266 | Already scheduled; this proposal confirms sequencing |
| R2-A4 | R1-A7 (Tier 2 tombstoning lifecycle test) still in backlog as `post-epic-1-r1a7-tier2-tombstoning-lifecycle` | sprint-status.yaml line 266 | Already scheduled; confirm sequencing |
| R2-A6 | Story spec template has no checklist item enforcing "integration tests inspect state-store end-state, not return values" | `.claude/skills/bmad-create-story/` (or equivalent) | Process change |
| R2-A9 | Pre-Epic-3 audit of D1-1..D1-5 + D2-1..D2-9 not yet done | n/a | Process task |
| R2-A10 | Senior-review-as-load-bearing pattern not documented anywhere | `CLAUDE.md` | Process change |

**Why now.** Epic 3 has not started. The "ULID-not-GUID" rule (R2-A7) is meaningfully cheaper to bake into Epic 3 specs *before* story 3.1 is created than to retrofit at review time. The 4 Pipeline NullRef failures (R2-A8) sit directly in Story 3.1's path. Sequencing matters.

---

## 2. Impact Analysis

### Epic Impact

- **Epic 1, Epic 2** (`done`): retrospectives recorded. No reopen. Existing Epic 1 carry-over stories (`post-epic-1-r1a1`/`r1a2`/`r1a6`/`r1a7`) stand as already-scheduled.
- **Epic 3** (`backlog`, not started): 3 stories (3.2, 3.3, 3.5) get spec amendments for ULID validation. No new Epic 3 stories; no resequencing within Epic 3.
- **Future epics**: no impact.

### Story Impact

**3 new standalone fix stories** under banner **"Post-Epic-2 Retro Cleanup"**:

| New Story Key | Title | Owner | Sequence |
|---------------|-------|-------|----------|
| `post-epic-2-r2a8-pipeline-nullref-fix` | Root-cause and fix 4 SubmitCommandHandler NullRef test failures | Dev | 1 (blocks Epic 3 Story 3.1) |
| `post-epic-2-r2a2-commandstatus-isterminal-extension` | Ship `CommandStatus.IsTerminal()` extension; deduplicate controller helpers | Dev | 2 (independent) |
| `post-epic-2-r2a5-dapr-sdk-version-reconcile` | Resolve DAPR SDK drift between `CLAUDE.md` and `Directory.Packages.props` | Jerome | 3 (one-line fix; can be inlined into any commit) |

**3 Epic 3 story specs amended** (Story 3.2, 3.3, 3.5) before stories are created from `epics.md`:

| Story | Amendment |
|-------|-----------|
| 3.2 (Command Validation & 400 Errors) | Add explicit acceptance criterion: "ID fields (messageId, correlationId, aggregateId) must validate as ULID via `Ulid.TryParse` (or accept arbitrary non-whitespace strings per `AggregateIdentity`). `Guid.TryParse` is forbidden for ID validation." |
| 3.3 (Command Status Query) | Add same criterion to status-endpoint validation. Reference Story 2.4's senior-review fix as the precedent. |
| 3.5 (Concurrency / Auth / Infrastructure Errors) | Add same criterion to 401/403/409 paths where IDs surface in ProblemDetails extensions. |

**Existing backlog stories** (no edit, just confirm sequencing per R2-A3/A4):

- `post-epic-1-r1a1-aggregatetype-pipeline` — runs first within post-epic-1 cluster (already scheduled)
- `post-epic-1-r1a6-missing-apply-method-exception` — second
- `post-epic-1-r1a2-terminatable-compliance-helper` — third (depends on r1a6)
- `post-epic-1-r1a7-tier2-tombstoning-lifecycle` — fourth (validates the prior three end-to-end)

### Artifact Conflicts

- **PRD:** No change. R2-A7 (ULID validation) is implicit in FR1's "valid ULID format" language; the Epic 3 spec amendments make the rule explicit at the story level.
- **Architecture (`architecture.md`):** No change. R2-A1's deeper fix (`post-epic-1-r1a1-...`) is already covered by the prior Epic 1 sprint-change-proposal (which adds a one-paragraph note clarifying AggregateType ≠ Domain to §Data Flow / §Event Persistence). No additional architecture text needed for Epic 2 cleanup.
- **UX Design:** No change.
- **Epics document (`_bmad-output/planning-artifacts/epics.md`):** Stories 3.2, 3.3, 3.5 acceptance criteria amended. No new stories; no renumbering.
- **`epic-2-retro-2026-04-26.md`:** Add an erratum block correcting the R2-A1 misanalysis. Do NOT rewrite the retro inline (preserve audit trail of original analysis); append an "Erratum" section below §11 with a forward-pointer to this proposal.
- **`CLAUDE.md`:** Two additions. (a) DAPR SDK version reconcile (R2-A5). (b) One-paragraph note that senior code review is mandatory and review-driven patches are the norm, not the exception (R2-A10).
- **Story template / `.claude/skills/bmad-create-story/`:** Add R2-A6 checklist item — "if the story creates or modifies a Tier 2/3 test, the test must inspect state-store end-state (e.g., Redis keys) and not only assert API return codes or call counts."
- **Sprint-status.yaml:** Add 3 new keys for the post-epic-2 fix stories with status `backlog`. Update `last_updated` annotation.

### Technical Impact

| Item | Code | Tests | Migration | Risk |
|------|------|-------|-----------|------|
| R2-A8 fix | Root-cause investigation in `SubmitCommandHandler.Handle`. Likely null guard or DI registration issue surfacing only when activity trackers / payload protection / logging interceptors are wired. Fix is local; no contract change | Re-enable the 4 named tests; add regression tests if root cause isn't fully covered | None | Low–Medium (root cause unknown until investigated) |
| R2-A2 fix | New: `CommandStatus.IsTerminal()` extension method on `CommandStatus` enum (or static class in `Hexalith.EventStore.Contracts.Commands`). Refactor `CommandStatusController:171` and `AdminTraceQueryController:223` to use it. ~5 file edits | New Tier 1 test for the extension; controller tests unchanged (behavior identical) | None | Trivial |
| R2-A5 fix | One-line edit to `CLAUDE.md` (`1.17.0` → `1.16.1`) OR upgrade `Directory.Packages.props` (16+ files affected if upgrading; verify Tier 2 still passes) | Existing test suite | None — pre-release | Trivial (downgrade-doc path) / Low (upgrade-package path) |
| Epic 3 spec amendments (R2-A7) | None — planning-artifacts edit only | n/a | n/a | None |
| `epic-2-retro-...md` erratum (R2-A1 correction) | None — planning-artifacts edit only | n/a | n/a | None |
| `CLAUDE.md` review-pattern note (R2-A10) | One-paragraph addition | n/a | n/a | None |
| Story template checklist (R2-A6) | Edit story-creation skill | Future stories enforce checklist | n/a | None |

---

## 3. Recommended Approach

**Direct adjustment + spec amendment hybrid.** Three small standalone fix stories + three Epic 3 spec amendments + one retrospective erratum + two doc additions.

**Rationale.**

1. **Sequencing window.** Epic 3 is unstarted; folding the ULID rule into specs now is meaningfully cheaper than discovering it at Story 3.x review time (which is the failure mode that produced R2-A7 in the first place).
2. **Sunk cost on R2-A1.** The standalone `post-epic-1-r1a1-aggregatetype-pipeline` story is already in backlog with full specs in the prior sprint-change-proposal. The Epic 2 retro's claim that it's duplicative was incorrect — correct it via erratum and let the existing story execute.
3. **R2-A8 root-cause first.** The 4 Pipeline NullRefs are stable failures across all of Epic 2. They are hidden by Tier 2 budget but Story 3.1 (Command Submission Endpoint) lives in the same handler. Fix before Epic 3 commits.
4. **R2-A6, R2-A10 are doc-only.** No code risk; ship as part of the same commit as the spec amendments.

**Sequencing.**

```
Step 1 — pre-Epic-3 critical (must clear before Story 3.1 starts):
  a. Apply this proposal: write erratum into epic-2-retro, amend Epic 3 specs,
     update CLAUDE.md (R2-A5 + R2-A10), update story template (R2-A6),
     update sprint-status.yaml.
  b. Execute post-epic-2-r2a8-pipeline-nullref-fix.
  c. Execute post-epic-2-r2a5-dapr-sdk-version-reconcile (can fold into 1a if trivial).

Step 2 — pre-Epic-3-closes (parallel with Epic 3 stories):
  a. Execute post-epic-2-r2a2-commandstatus-isterminal-extension.
  b. Execute the existing Epic 1 carry-over cluster in declared order:
     post-epic-1-r1a1 → r1a6 → r1a2 → r1a7.
  c. R2-A9 audit folds naturally into Epic 3 story-creation review.
```

**Risk assessment.** Cumulative risk: **Low** for everything except R2-A8 (Medium until root cause identified). The R2-A8 fix could surface a deeper Pipeline issue, in which case it would warrant its own scope discussion — but the 4 failing tests give a precise reproduction signal.

**Effort.** Per-story: small. Total cluster (Epic 1 + Epic 2 cleanup + spec amendments): one focused implementation session.

---

## 4. Detailed Change Proposals

### Proposal 1 — Erratum to `epic-2-retro-2026-04-26.md` (corrects R2-A1)

**Edit:** Append new section after §11 of `epic-2-retro-2026-04-26.md`:

```
File: _bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md
Section: NEW - Erratum (append after §11 Significant Discoveries)

NEW SECTION:

## 12. Erratum (added 2026-04-26 by sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md)

**Affects:** §2 finding 2 ("D1-1 cleared incidentally"), §6 R2-A1.

**Original claim.** "Story 2.2's reviewer caught D1-1 (`AggregateType: "unknown"` placeholder) and fixed `EventPersister` + `FakeEventPersister`. The standalone `post-epic-1-r1a1-aggregatetype-pipeline` backlog story is now duplicative."

**Correction.** Story 2.2's senior-review patch changed `EventEnvelope.AggregateType` from `"unknown"` to `identity.Domain`. Per Story 1.1 dev notes — and as documented in the prior `sprint-change-proposal-2026-04-26.md` for Epic 1 cleanup — Domain (e.g., `counter-domain`) and AggregateType (e.g., `counter`) are intentionally separate fields. Story 2.2 replaced the placeholder symptom with a different wrong value; the semantic bug remains.

**Action.** R2-A1's "mark as duplicate" recommendation is withdrawn. The standalone `post-epic-1-r1a1-aggregatetype-pipeline` story stands as the real fix and stays scheduled in `sprint-status.yaml` as `backlog`. Its acceptance criteria must reject both `"unknown"` and `identity.Domain` as `AggregateType` values.

**Lesson.** The senior-review process catches surface symptoms reliably; semantic correctness still requires reading dev notes and prior decision records. R2-A9 (pre-Epic-3 audit of D1/D2 deferred items) explicitly mitigates this in the next epic.

Rationale: Preserve the audit trail of the original analysis instead of silently rewriting; future readers should see how the misanalysis was identified and corrected.
```

**Acceptance criteria:**
1. Section 12 ("Erratum") appended to `epic-2-retro-2026-04-26.md`.
2. Original §2 and §6 text untouched (audit-trail preserved).
3. Forward-pointer to this proposal present in the erratum.

---

### Proposal 2 — `post-epic-2-r2a8-pipeline-nullref-fix`

**Problem.** `SubmitCommandHandler.Handle` raises `NullReferenceException` in 4 specific Tier 2 tests (`PayloadProtectionTests`, `CausationIdLoggingTests`, `LogLevelConventionTests`, `StructuredLoggingCompletenessTests`). The failures appeared at "line 81" in Story 2.1's review notes; the file has since grown so the exact line has shifted. The 4 named tests are the canonical reproduction signal. They have stayed stable across all of Epic 2 — never investigated, never fixed.

**Edits (investigation-driven; the exact fix depends on root cause, but the story owns the work):**

```
File: src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs
Section: investigate the call sequence in Handle that produces NRE under
         the test setups for the 4 named tests.

Likely root causes (to verify):
  (a) Activity tracker (`activityTracker` / `streamActivityTracker`) is null
      in test DI but Handle assumes one is present at a code path.
  (b) Payload-protection result wrapper unwraps to null before downstream
      use (CausationId / log enrichment).
  (c) Logger source-generated method (Log.StatusWriteFailed etc.) called
      before the logger field is initialized in a constructor path.

Fix shape: add explicit null guards at the surfaced site OR fix the DI
registration to ensure the missing dependency is non-null when the test
fixture wires it. Whichever lands closer to the root cause.
```

```
File: tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerTests.cs
       (and the 4 named test classes wherever they live)

Re-enable the 4 currently-failing tests. Add 1 regression test that
exercises the exact null path identified in the root-cause investigation
(do not rely on the existing tests catching the regression next time —
the existing tests have been failing silently in CI's "out-of-scope"
bucket for a full epic).
```

**Acceptance criteria:**
1. Root cause documented in story dev notes (one paragraph, references the specific code path).
2. The 4 named tests pass in the standard `dotnet test tests/Hexalith.EventStore.Server.Tests/` invocation after `dapr init`.
3. Pre-existing-failure count in Server.Tests drops by exactly 4 (15 → 11). Remaining 11 (validator × 1, auth integration × 10) are explicitly Epic-5 scope and stay deferred.
4. New regression test asserts the exact null guard / DI fix.
5. Tier 1 ratchet (656 tests) holds; full Release build 0/0.

---

### Proposal 3 — Epic 3 Story Specs Amended for ULID Validation (R2-A7)

**Problem.** Story 2.4's senior review caught `CommandStatusController` rejecting valid ULID correlation IDs because validation used `Guid.TryParse`. The system uses ULIDs end-to-end. Epic 3 stories 3.2, 3.3, 3.5 will exercise the same controller-validation seam. Without an explicit story-spec rule, the same bug class will be discovered at review time.

**Edits to `_bmad-output/planning-artifacts/epics.md`:**

```
File: _bmad-output/planning-artifacts/epics.md
Section: Story 3.2 (Command Validation & 400 Error Responses)

ADD acceptance criterion (after the existing 4 Given/When/Then blocks):

**Given** a request body or path parameter contains an ID field
(messageId, correlationId, aggregateId, causationId),
**When** the controller validates the ID,
**Then** validation MUST use `Ulid.TryParse` (or accept any non-whitespace
string per `AggregateIdentity` rules)
**And** validation MUST NOT use `Guid.TryParse` for any ID field
**And** a ULID input that fails GUID parsing MUST be accepted as valid
**And** a malformed input MUST surface as a 400 with
`type = https://hexalith.io/problems/validation-error` (UX-DR1).

Reference: Story 2.4's senior-review fix removed GUID-only correlation
ID validation from `CommandStatusController`. This rule generalizes that
fix across all Epic 3 controller seams.

Rationale: ULID and GUID share a 36-char string shape only by coincidence.
The system's identifiers are ULIDs; the rule eliminates a class of latent
contract bugs at story-spec time, not at code-review time.
```

```
File: _bmad-output/planning-artifacts/epics.md
Section: Story 3.3 (Command Status Query Endpoint)

ADD acceptance criterion (after existing 2 Given/When/Then blocks):

**Given** a `GET /api/v1/commands/status/{correlationId}` request,
**When** the controller validates `correlationId`,
**Then** it MUST accept any ULID-formatted string
**And** MUST NOT enforce GUID format
**And** an empty / whitespace correlationId returns 400 (not 404).

Reference: Story 2.4 already implemented this; the Epic 3 spec ratifies
the rule so future refactors don't regress.
```

```
File: _bmad-output/planning-artifacts/epics.md
Section: Story 3.5 (Concurrency, Auth & Infrastructure Error Responses)

ADD acceptance criterion (after the existing 4 scenario blocks):

**Given** any error response includes a `correlationId` extension in
ProblemDetails (UX-DR2),
**When** the controller serializes the response,
**Then** the correlationId is emitted as a string (no GUID re-parse,
no normalization)
**And** validation of inbound correlation IDs follows Story 3.2's
ULID rule.

Rationale: 401/403/409/503 paths must agree with 200/202/400 paths
on identifier semantics.
```

**Acceptance criteria for the amendment:**
1. All three stories in `epics.md` include the explicit ULID acceptance criterion.
2. Each criterion references either Story 2.4's precedent or Story 3.2's general rule, so reviewers can cross-check.
3. Story-creation (`bmad-create-story` or equivalent) for any of 3.2 / 3.3 / 3.5 picks up the criterion automatically when stories are spawned from the epic file.

---

### Proposal 4 — `post-epic-2-r2a2-commandstatus-isterminal-extension`

**Problem.** Epic 1 retro action item R1-A4 (`CommandStatus.IsTerminal()` extension) was supposed to fold into Story 2.4. It didn't. Two controllers each carry their own private `IsTerminalStatus(...)` helper:

- `src/Hexalith.EventStore/Controllers/CommandStatusController.cs:171`
- `src/Hexalith.EventStore/Controllers/AdminTraceQueryController.cs:223`

Duplicated logic for a stable enum-shape question is exactly the kind of drift that produces version-skew bugs.

**Edits:**

```
File (new or extension to existing): src/Hexalith.EventStore.Contracts/Commands/CommandStatusExtensions.cs

namespace Hexalith.EventStore.Contracts.Commands;

public static class CommandStatusExtensions
{
    /// <summary>
    /// Returns true if the status is terminal (Completed, Rejected,
    /// PublishFailed, or TimedOut). Convention: status >= Completed.
    /// </summary>
    public static bool IsTerminal(this CommandStatus status)
        => status >= CommandStatus.Completed;
}

Rationale: Single source of truth for terminal-status convention.
Implements Epic 1 retro R1-A4. XML doc references the convention
documented in Story 1.5 round-2 dev notes.
```

```
File: src/Hexalith.EventStore/Controllers/CommandStatusController.cs

OLD (line 123):
if (!IsTerminalStatus(record.Status)) {

NEW:
if (!record.Status.IsTerminal()) {

OLD (lines 171–~178):
private static bool IsTerminalStatus(CommandStatus status) =>
    status is CommandStatus.Completed
        or CommandStatus.Rejected
        or CommandStatus.PublishFailed
        or CommandStatus.TimedOut;

NEW:
[delete the private method entirely]

Rationale: Use the new shared extension; remove the duplicate.
```

```
File: src/Hexalith.EventStore/Controllers/AdminTraceQueryController.cs

Apply the same two edits: line 77 calls IsTerminal(); private method
at line 223 deleted.
```

```
File (new): tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusExtensionsTests.cs

Tests:
- Received -> false
- Processing -> false
- EventsStored -> false
- EventsPublished -> false
- Completed -> true
- Rejected -> true
- PublishFailed -> true
- TimedOut -> true
```

**Acceptance criteria:**
1. `CommandStatus.IsTerminal()` extension exists in `Hexalith.EventStore.Contracts.Commands`.
2. Both controllers consume the extension; private `IsTerminalStatus(...)` helpers deleted.
3. 8 unit tests cover all 8 enum values.
4. Existing Tier 1 / Tier 2 controller tests still green (behavior preserved).
5. Tier 1 ratchet increases by 8 (656 → 664).
6. Mark Epic 1 retro R1-A4 as ✅ completed with the merge commit reference.

---

### Proposal 5 — `post-epic-2-r2a5-dapr-sdk-version-reconcile`

**Problem.** `CLAUDE.md` line in "Key Dependencies" section states "DAPR SDK 1.17.0"; `Directory.Packages.props` pins `1.16.1`. Story 2.1 dev notes flagged this and explicitly chose not to upgrade as part of that story.

**Recommended path: align the doc to the source of truth.**

```
File: CLAUDE.md
Section: ## Key Dependencies

OLD:
- DAPR SDK 1.17.0 (Client, AspNetCore, Actors)

NEW:
- DAPR SDK 1.16.1 (Client, AspNetCore, Actors)

Rationale: Directory.Packages.props is the source of truth for package
versions. CLAUDE.md was ahead of the actual pin. Aligning the doc is
trivial and removes ambiguity. If/when DAPR SDK 1.17.x is intentionally
adopted, the upgrade goes through .props and CLAUDE.md gets re-aligned.
```

**Acceptance criteria:**
1. `CLAUDE.md` and `Directory.Packages.props` agree on `1.16.1`.
2. No code changes; no test changes.
3. Optional: same commit can absorb the R2-A10 review-pattern note (Proposal 7).

---

### Proposal 6 — Story Template Checklist Update (R2-A6)

**Problem.** Story 2.1's routing integration test posted requests without `messageId` and asserted success — the test was green, the routing path was never reached. Story 2.2's persistence integration test asserted command acceptance + pub/sub call counts but never inspected Redis. Both bugs survived to senior review.

**Edits:**

```
File: D:\Hexalith.EventStore\.claude\skills\bmad-create-story\<template-or-checklist-file>
       (find the story-creation skill's template; if none exists locally,
        document the rule in CLAUDE.md instead — see Proposal 7)

ADD checklist item (under Acceptance Criteria or Testing section):

- [ ] Integration tests (Tier 2/3): if the story creates or modifies
      integration tests, each test MUST inspect state-store end-state
      (e.g., Redis key contents, persisted CloudEvent body) and not
      only assert API return codes, mock call counts, or pub/sub call
      invocations. Asserting "the call returned 202" is an API smoke
      test, not an integration test.

Rationale: Epic 2 produced two reviewer-found integration test bugs
that survived because tests asserted return values instead of end-state.
The rule turns the lesson into a story-creation gate.
```

**Acceptance criteria:**
1. Checklist item present in the story-creation skill / template.
2. Future stories that create Tier 2/3 tests must satisfy this gate at story-spec time.

---

### Proposal 7 — `CLAUDE.md` Additions for R2-A10 (and fallback for R2-A6)

**Problem.** Senior review (GPT-5.4 Copilot) found HIGH/MEDIUM patches on 5/5 Epic 2 stories. The review-driven-patch rate is the norm, not the exception. The project's contributing docs / CLAUDE.md don't say so.

**Edits:**

```
File: CLAUDE.md
Section: NEW - Code Review Process (insert before "## Commit Messages"
         or wherever fits)

NEW SECTION:

## Code Review Process

Senior code review (typically GitHub Copilot GPT-5.4 or equivalent) is
a mandatory pipeline stage for every story, not a rubber-stamp formality.
The review-driven-patch rate across Epic 2 was 5/5 stories: every story
had at least one HIGH or MEDIUM reviewer finding that produced a real
patch.

**Implications:**

- Story specs should budget for review-found rework as the norm.
- "Verification" stories (audit existing code) typically uncover one
  design or test gap per story — plan accordingly.
- Reviewer-found patches are applied and validated before the story
  closes. If the reviewer surfaces a CRITICAL finding, verify before
  accepting (false-positive CRITICALs are expensive — see Epic 1 retro
  R1-A8 for the verification-command rule).

**Integration test rule** (see Epic 2 retro R2-A6):

Tier 2 and Tier 3 integration tests must inspect state-store end-state
(e.g., Redis key contents, persisted CloudEvent body), not only API
return codes or mock call counts. "Asserts the call returned 202" is
an API smoke test, not an integration test.

**ID validation rule** (see Epic 2 retro R2-A7):

Controllers and validators that handle `messageId`, `correlationId`,
`aggregateId`, or `causationId` MUST use `Ulid.TryParse` (or accept any
non-whitespace string per `AggregateIdentity` rules). `Guid.TryParse`
on these fields is forbidden — the system's identifiers are ULIDs.

Rationale: Encodes Epic 2's three highest-leverage process learnings
in the file Claude reads on every session start.
```

**Acceptance criteria:**
1. New "Code Review Process" section added to `CLAUDE.md` between Architecture Patterns and Commit Messages (or equivalent natural location).
2. Three sub-rules captured: review-as-load-bearing, integration-test-inspects-state, ULID-not-GUID.
3. Cross-references to Epic 1 R1-A8 and Epic 2 R2-A6/A7 preserved for traceability.

---

### Proposal 8 — Sequencing & Reconciliation for Existing Backlog (R2-A3, R2-A4)

**Problem.** Five backlog stories already exist from the Epic 1 cleanup proposal:

- `post-epic-1-r1a1-aggregatetype-pipeline`
- `post-epic-1-r1a6-missing-apply-method-exception`
- `post-epic-1-r1a2-terminatable-compliance-helper`
- `post-epic-1-r1a7-tier2-tombstoning-lifecycle`

Epic 2 retro R2-A3/A4 asks for these to be "rescheduled into Epic 3 prep." They are already scheduled and sequenced in `sprint-status.yaml` (lines 263–266). This proposal **confirms** the sequencing rather than rescheduling — they are at the same priority as the post-epic-2 cluster, can run in parallel, and should land before Story 3.1 if calendar allows but explicitly do not block Story 3.1 (Story 3.1 is a 202/Location-header presentation-layer change that doesn't touch the rehydration path that R1-A2/A6/A7 protect).

**No edit required.** R2-A3/A4 are administratively closed by this proposal's confirmation.

**Acceptance criteria:**
1. The 4 `post-epic-1-...` stories remain at their declared sequence in `sprint-status.yaml`.
2. The R2-A3/A4 references in the Epic 2 retro point at the existing stories rather than spawning duplicates.
3. Pre-Epic-3 audit (R2-A9) re-checks that these are still on track when Epic 3 story creation begins.

---

### Proposal 9 — `sprint-status.yaml` Updates

**Problem.** Three new fix stories must be added; one annotation must be updated.

**Edits:**

```
File: _bmad-output/implementation-artifacts/sprint-status.yaml

UPDATE last_updated annotation:
  last_updated: 2026-04-26 (Epic 1 retro + sprint-change-proposal-2026-04-26.md
    — 4 post-epic-1 fix stories added to backlog;
    Epic 2 retro completed; sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md
    — 3 post-epic-2 fix stories added to backlog + Epic 3 spec amendments)

ADD section (after existing post-epic-1 cluster):

  # Post-Epic-2 Retro Cleanup (sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md)
  # Standalone fix stories from Epic 2 retrospective (R2-A8, R2-A2, R2-A5).
  # R2-A8 is critical-before-Epic-3-starts.
  # R2-A1 (corrected): NOT a duplicate; covered by post-epic-1-r1a1-aggregatetype-pipeline.
  # R2-A3/A4: confirmed sequencing on existing post-epic-1 stories.
  # R2-A6/A7/A10: process changes captured in CLAUDE.md and story template.
  # R2-A9: pre-Epic-3 audit task — runs at Epic 3 story creation time.
  post-epic-2-r2a8-pipeline-nullref-fix: backlog
  post-epic-2-r2a2-commandstatus-isterminal-extension: backlog
  post-epic-2-r2a5-dapr-sdk-version-reconcile: backlog

Rationale: Make the cluster traceable in sprint-status; preserve the
R2-A1 correction by note rather than by adding a duplicate story.
```

**Acceptance criteria:**
1. 3 new keys present with status `backlog`.
2. `last_updated` annotation extended.
3. STATUS DEFINITIONS comments and structure preserved verbatim.

---

## 5. Implementation Handoff

**Scope:** Moderate — backlog reorganization (3 new stories) + Epic 3 spec amendments + retrospective erratum + 2 doc additions + 1 process-template change.

**Owner:** Dev (single team) for code changes; Jerome for the retro erratum and DAPR SDK doc reconcile.

**Recipients:** Implementation team for Proposals 2 and 4. Project Lead (Jerome) for Proposals 1, 5, 6, 7. Process owner for Proposal 6 (story template).

**Order:**

1. **Now (planning artifacts):** Proposals 1, 3, 5, 6, 7, 9 — all planning-artifact / doc edits. Single commit if convenient.
2. **Pre-Epic-3 critical:** Proposal 2 (`post-epic-2-r2a8-pipeline-nullref-fix`).
3. **Pre-Epic-3 / parallel:** Proposal 4 (`post-epic-2-r2a2-commandstatus-isterminal-extension`).
4. **Pre-Epic-3 / inline:** Proposal 5 (DAPR SDK doc reconcile) — can fold into the Step 1 commit.
5. **Confirm sequencing:** Proposal 8 (no edits) at Epic 3 story creation time, when the audit (R2-A9) runs.

**Deliverables on completion:**

- 3 merged commits / PRs for the new fix stories (R2-A8, R2-A2, R2-A5), each with conventional commit prefix matching content (`fix(server):`, `feat(contracts):`, `chore(deps):` or `docs:`).
- 1 planning-artifact commit for Proposals 1, 3, 6, 7, 9 (`docs(planning):`).
- All Tier 1 + Tier 2 tests green; Tier 1 ratchet 656 → 664 after R2-A2 lands.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` updated with the 3 new keys + annotation.
- `epic-2-retro-2026-04-26.md` carries the §12 erratum.
- `CLAUDE.md` carries the new Code Review Process section and DAPR SDK reconcile.
- `epics.md` Stories 3.2, 3.3, 3.5 carry the ULID-validation acceptance criterion.

**Success criteria:**

1. The 4 named Tier 2 NullRef tests pass; Server.Tests pre-existing failure count drops from 15 → 11 (R2-A8).
2. `CommandStatus.IsTerminal()` extension exists; controllers consume it; private duplicates removed (R2-A2, closes R1-A4).
3. Epic 3 stories 3.2 / 3.3 / 3.5 specs carry explicit ULID-validation criteria (R2-A7) so the validation bug class cannot survive to story review.
4. CLAUDE.md and Directory.Packages.props agree on DAPR SDK 1.16.1 (R2-A5).
5. `epic-2-retro-2026-04-26.md` §12 erratum stands as the authoritative correction of R2-A1; the original text is preserved (audit trail).
6. CLAUDE.md "Code Review Process" section documents the three highest-leverage Epic 2 process learnings (R2-A6, R2-A7, R2-A10) in the file Claude reads first every session.

**Out of scope (handled elsewhere):**

- R2-A1 deeper fix → already covered by `post-epic-1-r1a1-aggregatetype-pipeline` (existing backlog story per prior proposal).
- R2-A3 → confirmed sequencing on `post-epic-1-r1a2-...` and `post-epic-1-r1a6-...` (existing backlog).
- R2-A4 → confirmed sequencing on `post-epic-1-r1a7-tier2-tombstoning-lifecycle` (existing backlog).
- R2-A9 → administrative audit task; runs at Epic 3 story creation time. Not a story.

---

## 6. Approval

This proposal is ready for Jerome's approval. On approval:

1. **Commit the planning-artifact edits** (Proposals 1, 3, 5, 6, 7, 9) as a single `docs(planning):` commit. This is the lowest-risk batch; ship first.
2. **Spawn 3 fix stories** in execution order: R2-A8 → R2-A2 → R2-A5.
3. **Confirm Proposal 8** (existing post-epic-1 sequencing) at Epic 3 story creation, when R2-A9's audit runs.
4. Post-merge of each fix story, mark the corresponding Epic 2 retro action item as ✅ completed with the merge commit reference. Mark Epic 1 R1-A4 as ✅ completed when R2-A2 merges.

**Mode:** Batch (no interactive iteration; matches precedent of `sprint-change-proposal-2026-04-26.md` for Epic 1 cleanup).
