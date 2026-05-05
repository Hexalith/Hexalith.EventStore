# Post-Epic Deferred DW6: Deferred-Work Governance

Status: ready-for-dev

<!-- Source: sprint-change-proposal-2026-05-04-deferred-work-triage.md - Proposal G / DW6 -->
<!-- Source: deferred-work.md - accumulated review deferrals and post-epic cleanup dispositions through 2026-05-05 -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a product owner and maintainer,
I want deferred-work entries to carry explicit dispositions, owners, review dates, grouping hints, and unresolved-count checks,
so that review notes remain auditable backlog input instead of growing back into an unowned prose dump.

## Story Context

`deferred-work.md` has become the repository's long-running ledger for code-review deferrals, accepted debt, follow-up story routing, and resolved historical notes. The deferred-work triage proposal created DW1-DW5 to close the largest current risk clusters, but it also identified a meta-problem: without a convention, future stories will keep appending bullets that are hard to count, hard to route, and easy to mistake for open work.

This story is the governance pass for the ledger itself. It should add a small, parseable disposition convention, update obvious legacy entries so old resolved notes no longer look open, and add a lightweight count/check script or checklist that reviewers and retrospectives can run. It must preserve the raw historical text, avoid mass-rewriting unrelated evidence, and must not absorb DW1-DW5 product, runtime, Admin UI, evidence-validator, DAPR, MCP, SignalR, projection, or release-governance work.

Current HEAD at story creation: `7ade91f4`.

## Acceptance Criteria

1. **Governance vocabulary is explicit.** Given `deferred-work.md` is read by humans and automation, when DW6 closes, then a top-level governance section defines the canonical dispositions `OPEN`, `STORY:<id>`, `ACCEPTED-DEBT`, `RESOLVED`, `DUPLICATE`, and `NO-ACTION`. The section must explain when each disposition is valid, where the marker appears, whether matching is case-sensitive, and must preserve any existing historical wording that provides rationale. The canonical `STORY:<id>` form must use an existing or proposed story key in repository story-key style; if existence validation is not implemented in DW6, the checker must report that as advisory rather than silently accepting unknown IDs.

2. **Owner and next-review-date rules are bounded.** Given a deferred-work item remains unresolved, when its disposition is `OPEN` or `STORY:<id>`, then the entry must include an owner or owning role, `next-review-date` in `YYYY-MM-DD` format, grouping, and a short rationale or evidence link. Given an item is `ACCEPTED-DEBT`, then it must include a rationale and should include owner and review-date metadata unless the governance section records why the debt is intentionally not scheduled. Given an item is `RESOLVED`, `DUPLICATE`, or `NO-ACTION`, then owner and next-review-date are optional and must not be required by the checker unless the governance section explicitly marks a local exception.

3. **New deferrals include grouping guidance.** Given a future code-review or retrospective appends a deferred-work bullet, when it is added, then the entry must include a recommended grouping such as a story key, post-epic bucket, epic, component, or `needs-triage`. The grouping guidance must help sprint planning decide whether to promote the item to a story, accept the debt, or close it as duplicate/no-action.

4. **Unresolved-count tooling is small and deterministic.** Given `deferred-work.md` exists, when the new script or checklist runs, then it reports counts for at least `OPEN`, `STORY`, `ACCEPTED-DEBT`, `RESOLVED`, `DUPLICATE`, `NO-ACTION`, and `unclassified`. For reporting, `OPEN`, `STORY:<id>`, and `ACCEPTED-DEBT` are tracked as unresolved or still-owned work; `RESOLVED`, `DUPLICATE`, and `NO-ACTION` are closed; missing or invalid disposition markers are `unclassified`. The output must be stable, sorted, concise, free of environment-specific absolute paths, and non-zero only for intentionally fail-closed cases such as unclassified live bullets or missing required metadata on `OPEN`/`STORY` items.

5. **Legacy compatibility is deliberate.** Given existing entries already use mixed forms such as `STORY:<id> / ACCEPTED-DEBT`, `DW1 disposition`, `RESOLVED-IN-*`, checkmark-prefixed resolved lines, and free-text notes, when DW6 implements the checker, then it must either recognize those forms or document them as legacy-advisory. The checker must include at least one legacy fixture, sample input, or documented checklist case that proves irregular historical entries are reported without forcing a wholesale ledger rewrite. The checker must not turn every old historical bullet into a blocking failure before the story's curated sweep is complete.

6. **Resolved sweep is narrow and auditable.** Given some old entries are visibly resolved or already routed by DW1-DW5, when DW6 sweeps the ledger, then it may add disposition markers and grouping metadata, but it must not delete raw review text, reorder sections broadly, or rewrite unrelated technical detail. Each touched group must have a one-line rationale in the Dev Agent Record.

7. **Unclassified live work is visible.** Given a bullet has no recognizable disposition after the curated sweep, when the checker runs, then the item must appear in an `unclassified` or `needs-triage` report with enough context for a human to find it. The report must include the heading, bullet text excerpt, and line number or stable locator when the implementation can provide one.

8. **Retrospective and story-review handoff is documented.** Given sprint retrospectives and code reviews create most deferred work, when DW6 closes, then the repository must contain concise guidance telling reviewers how to append a new deferred-work item and telling retrospectives to summarize current `OPEN` count plus items promoted to stories. This may live in `deferred-work.md`, a process note, a script help output, or another existing BMAD process artifact, but it must be linked from the story.

9. **Automation scope stays local.** Given this is a governance story, when implementing the checker, then prefer built-in PowerShell, Bash, Python, or .NET code already available in the repo over adding new packages. If CI/local docs validation is changed, update PowerShell and Bash paths consistently and explain whether the check is blocking or advisory. Default to advisory behavior for legacy/historical findings; hard-fail only malformed canonical entries or current-scope missing metadata unless a human explicitly approves broader CI enforcement.

10. **Evidence and validation are recorded.** Given DW6 changes the ledger or adds tooling, when the story moves to review, then the Dev Agent Record must include before/after unresolved counts, command output or checklist output, files touched, validation commands run, legacy sweep scope, intentionally unclassified legacy sections left for later triage, confirmation that no product/runtime fixes were made, and confirmation that no nested submodules were initialized.

11. **Scope boundaries stay intact.** DW6 must not implement the underlying product fixes described by deferred bullets, must not reopen completed epics, must not change application runtime behavior, must not edit generated preflight JSON audit files, and must not initialize or update nested submodules.

12. **Bookkeeping is closed.** At dev handoff, update this story's Dev Agent Record, File List, Change Log, Verification Status, and sprint-status row. Move this story and its sprint-status row to `review` only after the governance convention, checker or checklist, ledger sweep, and validation evidence are recorded. Move both to `done` only after code review signoff.

## Scope Boundaries

- Do not fix the deferred technical issues themselves.
- Do not rewrite the whole deferred-work ledger into a new tracker format.
- Do not delete historical review text, evidence, or rationale.
- Do not change DW1-DW5 story artifacts except to link to governance if a narrow bookkeeping link is required.
- Do not add new third-party dependencies solely to parse Markdown.
- Do not make CI fail on every historical legacy entry unless the curated sweep has first classified that class of entry.
- Do not edit generated preflight JSON audit files.
- Do not initialize or update nested submodules.

## Implementation Inventory

| Area | File / artifact | Expected use |
| --- | --- | --- |
| Planning source | `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-04-deferred-work-triage.md` | Proposal G scope and acceptance direction |
| Deferred ledger | `_bmad-output/implementation-artifacts/deferred-work.md` | main governance target and raw historical text |
| Sprint status | `_bmad-output/implementation-artifacts/sprint-status.yaml` | story status bookkeeping only |
| Run log | `_bmad-output/process-notes/predev-hardening-runs.log` | automation-created run trace |
| Local docs validation | `scripts/validate-docs.ps1` and `scripts/validate-docs.sh` | possible local hook if the governance check becomes part of docs validation |
| CI docs validation | `.github/workflows/docs-validation.yml` | possible CI hook if the governance check becomes blocking |
| Process notes | `_bmad-output/process-notes/story-creation-lessons.md` or a new process note | optional place to link reviewer/retro guidance if `deferred-work.md` alone is insufficient |
| Future script location | `scripts/` or `_bmad-output/process-notes/` | small checker or report script; choose one location and document the command |

## Current Code Intelligence

- `deferred-work.md` already contains several disposition styles: explicit `STORY:<id> / ACCEPTED-DEBT`, `DW1 disposition`, `NO-ACTION`, checkmark-prefixed resolved bullets, and older free-text follow-ups without a marker. The checker must account for mixed legacy forms instead of assuming a clean new schema from line 1.
- DW1-DW5 established the cleanup pattern: close only the selected cluster, write narrow disposition markers, and route unrelated pressure to another story rather than sweeping the whole file opportunistically.
- `scripts/validate-docs.ps1` and `scripts/validate-docs.sh` are not fully symmetric today: the Bash script includes DAPR SDK version pin consistency as stage 4, while the PowerShell script lists three stages. If DW6 wires a new check into local validation, it should update both paths deliberately and note any existing asymmetry it leaves untouched.
- `.github/workflows/docs-validation.yml` currently runs Markdown lint, DAPR SDK version checks, link checking, and sample build/test. A governance check can start as advisory or curated-only if blocking every legacy entry would create false failures.
- `package.json` is release-tooling focused and does not currently define documentation validation scripts. Avoid adding Node dependencies unless there is a strong reason.
- `Directory.Packages.props` includes `YamlDotNet` for tests and current .NET test dependencies, but DW6 does not need product code changes. A script with standard-library parsing is likely enough unless the developer records a different reason.

## Latest Technical Notes

- The repository already uses Markdown as the ledger source of truth. Treat Markdown parsing as a focused lint/report problem, not as a product data model migration.
- Prefer stable text output that can be copied into retrospectives: counts by disposition, top unclassified headings, and optional `OPEN`/`STORY` owner/date rows.
- If line numbers are emitted by a checker, they are diagnostic conveniences, not permanent identifiers. The raw bullet text and containing heading should remain enough to locate the item after nearby edits.

## Party-Mode Hardening Notes

The 2026-05-05 party-mode review found DW6 directionally ready but recommended tightening the governance contract before development. These notes are binding for dev-story execution unless a human product or architecture decision supersedes them.

### Disposition Contract

- Define exact marker placement and accepted values in the ledger guidance. Treat canonical markers as case-sensitive unless the implemented checker explicitly documents otherwise.
- Use this semantic table unless product changes it before implementation: `OPEN` means unresolved and not yet routed; `STORY:<id>` means a named story owns follow-up and remains tracked as unresolved or transferred work; `ACCEPTED-DEBT` means consciously retained risk and remains counted separately from closed work; `RESOLVED`, `DUPLICATE`, and `NO-ACTION` are closed.
- Add one compact canonical entry example that includes disposition, owner or owning role, `next-review-date`, grouping, rationale, and evidence or story link.
- Preserve historical prose. During the narrow sweep, add adjacent metadata or a wrapper marker to touched entries; do not normalize every legacy sentence or delete rationale.

### Checker and Validation Boundaries

- Prefer a small deterministic checker over a manual checklist if it can be implemented without dependencies. If the implementation chooses a checklist, record why that is sufficient and make the checklist output copyable into retrospectives.
- Checker output must be stable: sorted counts, stable relative paths or headings, deterministic excerpts, and no timestamp-dependent or environment-specific absolute paths in normal report output.
- Include a legacy fixture, sample input, or checklist case covering irregular existing forms so compatibility is proved instead of assumed.
- If the checker is wired into `scripts/validate-docs.ps1`, `scripts/validate-docs.sh`, or `.github/workflows/docs-validation.yml`, default historical/legacy findings to advisory and make any hard-fail rule explicit.

### Review Handoff

- Reviewers should reject DW6 completion if the Dev Agent Record lacks before/after counts, checker or checklist command output, validation commands, files touched, legacy sweep scope, intentionally unclassified sections, and explicit confirmation that no product/runtime fixes or nested submodule initialization occurred.
- Defer rather than invent policy when ownership taxonomy, accepted-debt approval authority, stale review-date enforcement, or `STORY:<id>` existence validation requires product or architecture judgment.

## Tasks / Subtasks

- [ ] Task 0: Baseline the ledger and define the governance shape (AC: #1, #2, #3, #5, #11)
    - [ ] 0.1 Re-read Proposal G / DW6 and scan `deferred-work.md` headings and existing disposition forms.
    - [ ] 0.2 Record a baseline count of recognizable dispositions and unclassified bullets before editing the ledger.
    - [ ] 0.3 Decide the canonical marker format for new entries and document how legacy markers are interpreted.
    - [ ] 0.4 Define required metadata for `OPEN` and `STORY:<id>` entries: owner or owning role, `next-review-date`, and grouping.
    - [ ] 0.5 Confirm DW6 will not change any underlying product/runtime behavior described by deferred bullets.
    - [ ] 0.6 Define whether `STORY:<id>` existence checks and stale review dates are advisory or blocking for this story.

- [ ] Task 1: Add governance guidance to the ledger or process docs (AC: #1, #2, #3, #8)
    - [ ] 1.1 Add a concise top-level governance section to `deferred-work.md` or a linked process note.
    - [ ] 1.2 Define each disposition and the allowed examples.
    - [ ] 1.3 Define owner, next-review-date, grouping, and rationale requirements.
    - [ ] 1.4 Add reviewer guidance for appending new deferred-work bullets.
    - [ ] 1.5 Add retrospective guidance to summarize `OPEN` counts and story promotions.
    - [ ] 1.6 Add one compact canonical deferred-work entry example.

- [ ] Task 2: Implement the unresolved-count checker or checklist (AC: #4, #5, #7, #9)
    - [ ] 2.1 Choose the smallest maintainable implementation path and record the choice.
    - [ ] 2.2 Count canonical and accepted legacy dispositions.
    - [ ] 2.3 Report `unclassified` bullets with heading, excerpt, and locator where practical.
    - [ ] 2.4 Fail closed only for selected current-scope rules, such as missing owner/date on new `OPEN` or `STORY` entries.
    - [ ] 2.5 Add a help or usage output that explains blocking versus advisory behavior.
    - [ ] 2.6 If wired into local or CI validation, update PowerShell, Bash, and workflow paths consistently or record why the integration is advisory/deferred.
    - [ ] 2.7 Prove deterministic output with a fixture, sample input, golden output, or documented before/after transcript.
    - [ ] 2.8 Include at least one legacy compatibility case covering mixed or historical marker forms.

- [ ] Task 3: Sweep old entries narrowly (AC: #5, #6, #7, #10, #11)
    - [ ] 3.1 Mark clearly resolved historical entries as `RESOLVED`, `DUPLICATE`, or `NO-ACTION` without deleting original wording.
    - [ ] 3.2 Mark already-routed DW1-DW5 entries with `STORY:<id>` or accepted legacy interpretation where appropriate.
    - [ ] 3.3 Leave ambiguous technical follow-ups as `OPEN` or `needs-triage` rather than inventing closure.
    - [ ] 3.4 Do not reorder sections broadly or normalize every sentence style.
    - [ ] 3.5 Record each touched heading group and rationale in the Dev Agent Record.

- [ ] Task 4: Validate and capture evidence (AC: #4, #7, #9, #10)
    - [ ] 4.1 Run the checker before and after the curated sweep and save or paste the concise output in the Dev Agent Record.
    - [ ] 4.2 Run Markdown validation for changed Markdown files if tooling is available.
    - [ ] 4.3 Run script self-checks or focused tests if the checker has testable parsing logic.
    - [ ] 4.4 Confirm generated preflight JSON files remain unstaged.
    - [ ] 4.5 If any unclassified legacy entries remain, list their headings and planned follow-up path.
    - [ ] 4.6 Record whether local docs validation and CI validation are advisory or blocking after DW6.

- [ ] Task 5: Close story bookkeeping (AC: #10, #12)
    - [ ] 5.1 Update this story's Dev Agent Record, File List, Change Log, and Verification Status.
    - [ ] 5.2 Update `sprint-status.yaml` only when moving from implementation to review.
    - [ ] 5.3 Ensure final deferred-work edits are narrow and auditable.
    - [ ] 5.4 Move the story to `review` only after the governance convention, checker/checklist, curated sweep, and evidence are present.
    - [ ] 5.5 Confirm no product/runtime fixes were made and no nested submodules were initialized.

## Dev Notes

### ATDD Artifacts

- Checklist: `_bmad-output/test-artifacts/atdd-checklist-post-epic-deferred-dw6-deferred-work-governance.md`
- Red-phase test project: `tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Hexalith.EventStore.DeferredWorkGovernance.Tests.csproj`
- Checker handoff notes: `_bmad-output/test-artifacts/deferred-work-governance/README.md`

### Architecture Guardrails

- Treat `deferred-work.md` as a human-readable ledger with light machine checks, not as a database migration.
- Preserve historical review text. Add metadata beside or below bullets instead of deleting the reason the item was deferred.
- Keep the convention simple enough that reviewers can write it during code review without a tool.
- `OPEN` means unresolved and not yet routed. `STORY:<id>` means a story owns follow-up, even when the actual disposition is still pending implementation.
- `ACCEPTED-DEBT` requires a rationale and a reopen trigger when practical.
- `RESOLVED`, `DUPLICATE`, and `NO-ACTION` require a short reason or source reference so future readers can tell why the bullet is no longer open.
- Do not let the checker silently mark unknown text as resolved. Unknown current entries should surface as unclassified or needs-triage.
- Treat `OPEN`, `STORY:<id>`, and `ACCEPTED-DEBT` as still visible in unresolved/owned-work reporting unless the final governance text deliberately separates `ACCEPTED-DEBT` into its own risk count.
- Do not require every historical closed item to gain owner/date metadata; add metadata only to current or touched entries unless the story records a narrower rationale.
- Keep any CI enforcement modest. Advisory reports are acceptable for legacy debt; hard failures should be limited to malformed canonical entries or missing metadata in the curated current scope.

### Previous Story Intelligence

- DW1 closed projection/drain items with explicit accepted-debt and patch-now dispositions while leaving unrelated concerns untouched.
- DW2 captured live Admin/DAPR/MCP evidence and marked its owned deferred-work bullets without claiming DW3-DW6 work fixed.
- DW3, DW4, and DW5 ready stories already contain explicit warnings not to sweep unrelated deferred-work sections into their scopes.
- R10-A7 and other review entries show why governance matters: sprint-status transition discipline and doc-policy follow-ups can linger as prose unless they are routed or accepted.
- Epic 21 follow-up entries mix resolved, deferred-cold-start, and runtime bug notes in one section. DW6 should classify status, not redo the browser work.

### Testing Guidance

- Primary validation is the checker/checklist output plus Markdown validation of changed docs.
- If the checker is a script, include deterministic sample output and at least one intentional unclassified or missing-metadata case when practical.
- If local validation is updated, run the affected script path directly rather than solution-level tests.
- Product unit tests are not required unless product code changes unexpectedly.
- Do not run solution-level `dotnet test`.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-04-deferred-work-triage.md#Proposal-G-DW6-Deferred-Work-Governance`] - DW6 scope and acceptance direction.
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md`] - raw deferred-work ledger and mixed legacy disposition forms.
- [Source: `_bmad-output/implementation-artifacts/post-epic-deferred-dw1-projection-and-drain-hardening.md`] - first grouped cleanup story and disposition pattern.
- [Source: `_bmad-output/implementation-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence.md`] - evidence-driven closure pattern and narrow deferred-work dispositions.
- [Source: `_bmad-output/implementation-artifacts/post-epic-deferred-dw3-admin-debugging-json-large-stream-hardening.md`] - adjacent JSON/large-stream story and scope boundary.
- [Source: `_bmad-output/implementation-artifacts/post-epic-deferred-dw4-operational-evidence-schema-validation.md`] - adjacent evidence-validator story and CI/local validation considerations.
- [Source: `_bmad-output/implementation-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md`] - adjacent Admin UI runtime story and non-governance boundary.
- [Source: `scripts/validate-docs.ps1`] - existing local PowerShell documentation validation path.
- [Source: `scripts/validate-docs.sh`] - existing local Bash documentation validation path.
- [Source: `.github/workflows/docs-validation.yml`] - current documentation validation CI jobs.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Pre-dev hardening preflight: `_bmad-output/process-notes/predev-preflight-latest.json`, timestamp `2026-05-05T12:13:09Z`, result `pass`.
- Create-story activation: resolved workflow customization with no prepend/append steps; no `project-context.md` file was present in the workspace.

### Completion Notes List

- Created ready-for-dev story from first backlog row after DW5 in the Post-Epic Deferred Work Cleanup package.
- No implementation work has been performed for this story.
- No `project-context.md` file was present in the repository at story creation.
- Party-mode review tightened the governance handoff around disposition syntax, unresolved counts, legacy compatibility, validation boundaries, and required evidence fields.

### File List

- `_bmad-output/implementation-artifacts/post-epic-deferred-dw6-deferred-work-governance.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/process-notes/predev-hardening-runs.log`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Party-mode review trace is recorded inline; no sprint-status change was required.
- Markdown and YAML validation should be run before dev handoff if local tooling is available.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-05 | 0.1 | Created ready-for-dev DW6 deferred-work governance story. | Codex automation |
| 2026-05-05 | 0.2 | Applied party-mode hardening for disposition semantics, checker determinism, legacy handling, validation boundaries, and evidence obligations. | Codex automation |

## Party-Mode Review

- Date/time: 2026-05-05T18:06:03+02:00
- Selected story key: `post-epic-deferred-dw6-deferred-work-governance`
- Command/skill invocation used: `/bmad-party-mode post-epic-deferred-dw6-deferred-work-governance; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer).
- Findings summary: Reviewers agreed DW6 is directionally implementable but needed tighter pre-dev contracts so the governance work does not become a broad ledger rewrite, noisy CI migration, or judgment-heavy checker. The main risks were undefined disposition syntax, ambiguous unresolved-count semantics, unclear advisory versus blocking validation behavior, weak legacy compatibility proof, and incomplete Dev Agent Record evidence obligations.
- Changes applied: Added Party-Mode Hardening Notes; tightened Acceptance Criteria 1, 2, 4, 5, 9, and 10; added tasks for `STORY:<id>` existence and stale-date policy, canonical examples, deterministic output proof, legacy compatibility coverage, validation-boundary evidence, and no-runtime/no-nested-submodule confirmation; updated Dev Notes, Completion Notes, Verification Status, and Change Log.
- Findings deferred: Whether `ACCEPTED-DEBT` needs product-owner approval, whether `STORY:<id>` existence checks should be blocking, whether stale `next-review-date` should fail validation, the long-term owner taxonomy for deferred work, and whether unresolved-count thresholds should ever block CI require human product or architecture judgment.
- Final recommendation: needs-story-update
