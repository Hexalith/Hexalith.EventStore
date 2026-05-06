# Post-Epic Deferred DW6: Deferred-Work Governance

Status: done

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

4. **Unresolved-count tooling is small and deterministic.** Given `deferred-work.md` exists, when the new script or checklist runs, then it reports counts for at least `OPEN`, `STORY`, `ACCEPTED-DEBT`, `RESOLVED`, `DUPLICATE`, `NO-ACTION`, and `unclassified`. For reporting, `OPEN`, `STORY:<id>`, and `ACCEPTED-DEBT` are tracked as unresolved or still-owned work; `RESOLVED`, `DUPLICATE`, and `NO-ACTION` are closed; missing or invalid disposition markers are `unclassified`. The output must be stable, sorted, concise, free of environment-specific absolute paths, and non-zero only for intentionally fail-closed cases such as unclassified live bullets or missing required metadata on `OPEN`/`STORY` items. The checker or checklist must be read-only by default and must not auto-normalize ledger text, add markers, or rewrite historical entries unless a future story explicitly introduces a separate fix mode.

5. **Legacy compatibility is deliberate.** Given existing entries already use mixed forms such as `STORY:<id> / ACCEPTED-DEBT`, `DW1 disposition`, `RESOLVED-IN-*`, checkmark-prefixed resolved lines, and free-text notes, when DW6 implements the checker, then it must either recognize those forms or document them as legacy-advisory. When more than one recognizable disposition appears on one entry, the checker must choose a deterministic primary disposition, report any secondary marker as compatibility context, and document the precedence rule. The checker must include at least one legacy fixture, sample input, or documented checklist case that proves irregular historical entries are reported without forcing a wholesale ledger rewrite. The checker must not turn every old historical bullet into a blocking failure before the story's curated sweep is complete.

6. **Resolved sweep is narrow and auditable.** Given some old entries are visibly resolved or already routed by DW1-DW5, when DW6 sweeps the ledger, then it may add disposition markers and grouping metadata, but it must not delete raw review text, reorder sections broadly, or rewrite unrelated technical detail. Each touched group must have a one-line rationale in the Dev Agent Record.

7. **Unclassified live work is visible.** Given a bullet has no recognizable disposition after the curated sweep, when the checker runs, then the item must appear in an `unclassified` or `needs-triage` report with enough context for a human to find it. The report must include the heading, bullet text excerpt, and line number or stable locator when the implementation can provide one.

8. **Retrospective and story-review handoff is documented.** Given sprint retrospectives and code reviews create most deferred work, when DW6 closes, then the repository must contain concise guidance telling reviewers how to append a new deferred-work item and telling retrospectives to summarize current `OPEN` count plus items promoted to stories. This may live in `deferred-work.md`, a process note, a script help output, or another existing BMAD process artifact, but it must be linked from the story.

9. **Automation scope stays local.** Given this is a governance story, when implementing the checker, then prefer built-in PowerShell, Bash, Python, or .NET code already available in the repo over adding new packages. If CI/local docs validation is changed, update PowerShell and Bash paths consistently and explain whether the check is blocking or advisory. Default to advisory behavior for legacy/historical findings; hard-fail only malformed canonical entries or current-scope missing metadata unless a human explicitly approves broader CI enforcement. The implementation must document the exit-code contract so reviewers can tell which findings are blocking, advisory, or informational without reading the script source.

10. **Evidence and validation are recorded.** Given DW6 changes the ledger or adds tooling, when the story moves to review, then the Dev Agent Record must include before/after unresolved counts, command output or checklist output, files touched, validation commands run, legacy sweep scope, intentionally unclassified legacy sections left for later triage, confirmation that no product/runtime fixes were made, and confirmation that no nested submodules were initialized.

11. **Evidence excerpts avoid accidental leakage.** Given deferred-work entries may quote review notes, paths, URLs, or captured operational evidence, when the checker reports excerpts for unclassified or malformed entries, then normal output must cap excerpt length and redact obvious token-like query strings or credential-looking values. This redaction is for report output only; the checker must not mutate the source ledger while redacting diagnostics.

12. **Scope boundaries stay intact.** DW6 must not implement the underlying product fixes described by deferred bullets, must not reopen completed epics, must not change application runtime behavior, must not edit generated preflight JSON audit files, and must not initialize or update nested submodules.

13. **Bookkeeping is closed.** At dev handoff, update this story's Dev Agent Record, File List, Change Log, Verification Status, and sprint-status row. Move this story and its sprint-status row to `review` only after the governance convention, checker or checklist, ledger sweep, and validation evidence are recorded. Move both to `done` only after code review signoff.

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

- [x] Task 0: Baseline the ledger and define the governance shape (AC: #1, #2, #3, #5, #12)
    - [x] 0.1 Re-read Proposal G / DW6 and scan `deferred-work.md` headings and existing disposition forms.
    - [x] 0.2 Record a baseline count of recognizable dispositions and unclassified bullets before editing the ledger.
    - [x] 0.3 Decide the canonical marker format for new entries and document how legacy markers are interpreted.
    - [x] 0.4 Define required metadata for `OPEN` and `STORY:<id>` entries: owner or owning role, `next-review-date`, and grouping.
    - [x] 0.5 Confirm DW6 will not change any underlying product/runtime behavior described by deferred bullets.
    - [x] 0.6 Define whether `STORY:<id>` existence checks and stale review dates are advisory or blocking for this story.

- [x] Task 1: Add governance guidance to the ledger or process docs (AC: #1, #2, #3, #8)
    - [x] 1.1 Add a concise top-level governance section to `deferred-work.md` or a linked process note.
    - [x] 1.2 Define each disposition and the allowed examples.
    - [x] 1.3 Define owner, next-review-date, grouping, and rationale requirements.
    - [x] 1.4 Add reviewer guidance for appending new deferred-work bullets.
    - [x] 1.5 Add retrospective guidance to summarize `OPEN` counts and story promotions.
    - [x] 1.6 Add one compact canonical deferred-work entry example.

- [x] Task 2: Implement the unresolved-count checker or checklist (AC: #4, #5, #7, #9, #11)
    - [x] 2.1 Choose the smallest maintainable implementation path and record the choice.
    - [x] 2.2 Count canonical and accepted legacy dispositions.
    - [x] 2.3 Report `unclassified` bullets with heading, excerpt, and locator where practical.
    - [x] 2.4 Fail closed only for selected current-scope rules, such as missing owner/date on new `OPEN` or `STORY` entries.
    - [x] 2.5 Add a help or usage output that explains blocking versus advisory behavior.
    - [x] 2.5a Document the checker exit-code contract, including blocking, advisory, and informational findings.
    - [x] 2.6 If wired into local or CI validation, update PowerShell, Bash, and workflow paths consistently or record why the integration is advisory/deferred.
    - [x] 2.7 Prove deterministic output with a fixture, sample input, golden output, or documented before/after transcript.
    - [x] 2.8 Include at least one legacy compatibility case covering mixed or historical marker forms.
    - [x] 2.9 Keep checker execution read-only by default and prove report redaction/truncation for excerpts that contain URL query strings or credential-looking values.

- [x] Task 3: Sweep old entries narrowly (AC: #5, #6, #7, #10, #12)
    - [x] 3.1 Mark clearly resolved historical entries as `RESOLVED`, `DUPLICATE`, or `NO-ACTION` without deleting original wording.
    - [x] 3.2 Mark already-routed DW1-DW5 entries with `STORY:<id>` or accepted legacy interpretation where appropriate.
    - [x] 3.3 Leave ambiguous technical follow-ups as `OPEN` or `needs-triage` rather than inventing closure.
    - [x] 3.4 Do not reorder sections broadly or normalize every sentence style.
    - [x] 3.5 Record each touched heading group and rationale in the Dev Agent Record.

- [x] Task 4: Validate and capture evidence (AC: #4, #7, #9, #10, #11)
    - [x] 4.1 Run the checker before and after the curated sweep and save or paste the concise output in the Dev Agent Record.
    - [x] 4.2 Run Markdown validation for changed Markdown files if tooling is available.
    - [x] 4.3 Run script self-checks or focused tests if the checker has testable parsing logic.
    - [x] 4.4 Confirm generated preflight JSON files remain unstaged.
    - [x] 4.5 If any unclassified legacy entries remain, list their headings and planned follow-up path.
    - [x] 4.6 Record whether local docs validation and CI validation are advisory or blocking after DW6.
    - [x] 4.7 Record the default read-only checker behavior and the diagnostic redaction/truncation evidence.

- [x] Task 5: Close story bookkeeping (AC: #10, #13)
    - [x] 5.1 Update this story's Dev Agent Record, File List, Change Log, and Verification Status.
    - [x] 5.2 Update `sprint-status.yaml` only when moving from implementation to review.
    - [x] 5.3 Ensure final deferred-work edits are narrow and auditable.
    - [x] 5.4 Move the story to `review` only after the governance convention, checker/checklist, curated sweep, and evidence are present.
    - [x] 5.5 Confirm no product/runtime fixes were made and no nested submodules were initialized.

### Review Findings

Code review on 2026-05-06 (multi-layer adversarial: Blind Hunter + Edge Case Hunter + Acceptance Auditor). Acceptance Auditor verdict: 13/13 ACs PASS. No CRITICAL findings. 6 MEDIUM and 6 LOW patches; 7 deferred items. Findings below.

- [x] [Review][Patch] `--legacy-advisory` flag is dead code — argparse declares it but `args.legacy_advisory` is never read; CI workflow and both validate-docs scripts pass it expecting behavior change. Either implement (e.g., gate `dw6-unclassified-legacy-advisory` emission on this flag) or remove it from argparse + all four callers. Behavior is correct by default because `fixture_mode=False` already maps unclassified ledger bullets to the advisory rule. [scripts/check-deferred-work.py:106]
- [x] [Review][Patch] C# test `ProcessStartInfo` lacks `StandardOutputEncoding = Encoding.UTF8` — on Windows with default OEM console code page, JSON output containing non-ASCII bullet text (em-dashes are widespread in `deferred-work.md`) can be corrupted before `JsonDocument.Parse`. Set `startInfo.StandardOutputEncoding = startInfo.StandardErrorEncoding = Encoding.UTF8;`. [tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Dw6GovernanceCheckerInvokerFactory.cs:61-66]
- [x] [Review][Patch] `pwsh:` entrypoint hard-coded — `entrypoint.txt` pins `pwsh:scripts/check-deferred-work.ps1`; on Linux/macOS hosts without PowerShell Core installed, every checker-shell ATDD test throws `Win32Exception` from `Process.Start`. Either auto-detect OS in `Dw6GovernanceCheckerInvokerFactory` and select `sh:scripts/check-deferred-work.sh` on non-Windows, OR document `pwsh` as a hard test prerequisite and surface a clear error message in the factory. [_bmad-output/test-artifacts/deferred-work-governance/entrypoint.txt:1]
- [x] [Review][Patch] `RESOLVED` precedence can override explicit canonical `[STORY:foo]` — when a bullet contains both a canonical bracketed `[STORY:...]` marker AND prose that contains the bare word `RESOLVED` (e.g., "previously RESOLVED in DW1"), `classify()` adds both markers to the set and `DISPOSITION_PRECEDENCE` picks RESOLVED first, silently dropping the bullet from STORY metadata enforcement. Make the line-start canonical bracketed marker authoritative: when `[OPEN|STORY:&lt;id&gt;|ACCEPTED-DEBT|RESOLVED|DUPLICATE|NO-ACTION]` is present, use that disposition and downgrade prose-derived markers to secondary. Currently latent (no canonical `[STORY:...]` entries in the ledger today) but will fire as soon as new entries adopt the canonical form. [scripts/check-deferred-work.py:33-40,232-274]
- [x] [Review][Patch] `LegacyCompatibility_DocumentsRecognizedMixedForms` asserts string presence in markdown, not checker behavior — the test only checks that the prose phrases ("checkmark-prefixed resolved lines", "free-text notes documented as legacy-advisory") appear in `deferred-work.md`. It does not run the checker against any legacy-form bullet. This is the "ship to pass review" anti-pattern explicitly warned against in CLAUDE.md (Code Review Process / Integration test rule R2-A6). Replace or augment with a behavior assertion: invoke the checker on the `legacy-mixed-marker` fixture and assert each declared legacy form actually classifies as expected. [tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Dw6GovernanceVocabularyAtddTests.cs:LegacyCompatibility_DocumentsRecognizedMixedForms]
- [x] [Review][Patch] `sanitize_excerpt` redaction misses common credential patterns — current regex covers `?query=...` URL strings and `name=value` for `token|secret|password|pwd|apikey|api-key|access[_-]?key`. It does NOT redact: Bearer/JWT (`Authorization: Bearer eyJ...`), GitHub PATs (`ghp_...`, `github_pat_...`), AWS keys (`AKIA...`), Slack tokens (`xoxb-...`), basic-auth in URL userinfo (`https://user:pass@host`), or colon-form `password: hunter2` (the regex requires `=`). AC #11 says "obvious token-like query strings or credential-looking values" — defensible PASS but worth hardening. Extend the regex set and document the redaction policy in the script `--help` epilog. [scripts/check-deferred-work.py:366-372]
- [x] [Review][Patch] PS1 stage numbering is inconsistent (`/4` then `/5`) — lines 37, 43, 49 say `Stage 1/4`, `2/4`, `3/4`; lines 55, 63 say `Stage 4/5`, `5/5`. The denominator switches mid-script. Update all five stage labels to `X/5`. [scripts/validate-docs.ps1:37,43,49,55,63]
- [x] [Review][Patch] PS1↔SH stage asymmetry preservation not noted in Verification Status — the story spec at story-context line 79 explicitly directs DW6 to "note any existing asymmetry it leaves untouched". `validate-docs.sh` has 6 stages including `Stage 4/6: DAPR SDK Version Pin Consistency` (`bash scripts/check-doc-versions.sh`). `validate-docs.ps1` has only 5 stages and omits the DAPR SDK stage. This asymmetry is pre-existing (not introduced by DW6) but the Dev Agent Record / Verification Status does not acknowledge it. Add one line to Verification Status: "Pre-existing PS1↔SH asymmetry preserved: PS1 lacks the DAPR SDK version-pin stage that SH stage 4 runs via `scripts/check-doc-versions.sh`. Out of DW6 scope; flagged for future doc-validation symmetry story." [Verification Status]
- [x] [Review][Patch] `--help` epilog does not summarize the exit-code contract — AC #9 requires "documented in the script `--help` AND in a process note". The contract IS in `--help-json` JSON output and in the governance section of `deferred-work.md`, but a user running `python scripts/check-deferred-work.py --help` only sees argparse's flag list. Add an `epilog="Exit code 1 = blocking ... Exit code 0 = success or advisory."` to the `ArgumentParser` constructor at line 100. [scripts/check-deferred-work.py:100-109]
- [x] [Review][Patch] C# test process invocation has no timeout — `WaitForExitAsync(cancellationToken)` only returns when the process exits or the test runner cancels. xUnit default has no per-test timeout, so a hung Python interpreter (e.g., a future code change introducing `input()`) hangs the test suite indefinitely. Wrap with `CancellationTokenSource` (`TimeSpan.FromMinutes(1)`) and `process.Kill(entireProcessTree: true)` on timeout. [tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Dw6GovernanceCheckerInvokerFactory.cs:68-73]
- [x] [Review][Patch] `--max-excerpt &lt; 4` produces inverted slice — argparse accepts any int (default 160). With `--max-excerpt 0`, line 372 computes `redacted[:-3]` (drops last 3 chars then appends `"..."`); with `--max-excerpt 2` similarly inverts. Add validation: argparse `type=lambda v: int(v) if int(v) &gt;= 8 else parser.error(...)`. Edge but real input-validation gap. [scripts/check-deferred-work.py:108,372]
- [x] [Review][Patch] Re-indentation of sub-bullets in `deferred-work.md` not noted in Completion Notes — visible in the diff: multiple lines change from 2-space to 4-space indentation under DW1/DW2 sections. The story scope says "Do not reorder sections broadly or normalize every sentence style." Sub-bullet re-indentation is a normalization edit. If it was a markdownlint MD007 fix, document it; otherwise consider reverting. Smallest fix: one-line rationale in Completion Notes List. [_bmad-output/implementation-artifacts/deferred-work.md (multiple sections)]
- [x] [Review][Defer] Sub-bullet double-counting and code-fence non-skipping in `parse_bullets` — regex matches at any indentation; fence state not tracked. Works for the current ledger (sibling-bullet pattern, single fenced PowerShell example) but will misclassify if future content uses nested bullets or describes markers inside code fences. — deferred, future parser refinement [scripts/check-deferred-work.py:217-229]
- [x] [Review][Defer] `[x]` GitHub task-list silently classifies bullet as RESOLVED — `if re.match(r"^\[[xX]\]\s+", text): markers.add("RESOLVED")`. The ledger contains no `[x]` checklists today; if any are added, they will be miscounted. — deferred, no current trigger [scripts/check-deferred-work.py:260-261]
- [x] [Review][Defer] `load_known_story_keys` uses `Path.cwd()` not script-relative — wrappers always `cd $REPO_ROOT` before invoking, so this works in supported call paths; direct `python scripts/check-deferred-work.py` from another cwd silently disables story-key validation. — deferred, wrappers compensate [scripts/check-deferred-work.py:142,350-363]
- [x] [Review][Defer] Compound disposition suffixes added in this PR (`/RESOLVED-IN-VALIDATOR`, `/RESOLVED-NO-ACTION`, `/DEFERRED-WITH-TARGET-AND-REASON`, `/RESOLVED-REGRESSION-GUARD`, `/FUTURE-ACTOR-API`) are undocumented in the governance section's legacy-form list — they accidentally classify via the `\bRESOLVED\b` substring scan, which is "probably what the author wanted" but the suffix vocabulary is not part of the documented contract. — deferred, future governance-vocabulary story [_bmad-output/implementation-artifacts/deferred-work.md]
- [x] [Review][Defer] CI workflow invokes Python directly, not the wrapper — `.github/workflows/docs-validation.yml:43` runs `python scripts/check-deferred-work.py ...` while local `validate-docs.ps1`/`.sh` use the wrapper. Wrapper bugs (Python resolution order, `Push-Location` race, `$LASTEXITCODE = $null`) are invisible in CI. — deferred, asymmetric invocation path [.github/workflows/docs-validation.yml:43]
- [x] [Review][Defer] `entrypoint.txt` committed contradicts README's "set when implementation starts" instruction — `_bmad-output/test-artifacts/deferred-work-governance/README.md` tells future contributors to declare the entrypoint at implementation time, but this PR commits the chosen value, freezing it. Either `.gitignore` it or update the README. — deferred, doc-consistency cleanup [_bmad-output/test-artifacts/deferred-work-governance/README.md]
- [x] [Review][Defer] CI advisory step `continue-on-error: true` swallows ALL non-zero exits including real CLI errors (exit 2) — a checker traceback or argparse error is treated identically to advisory governance findings. AC #9 says exit 1 = blocking advisory and exit 2 = usage error; the CI design erases that distinction. — deferred, CI design trade-off; revisit when advisory becomes blocking [.github/workflows/docs-validation.yml:42-43]

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
- If multiple disposition markers are present on a legacy entry, require deterministic precedence and visible compatibility context instead of letting parser order depend on regex match order.
- Do not require every historical closed item to gain owner/date metadata; add metadata only to current or touched entries unless the story records a narrower rationale.
- Keep any CI enforcement modest. Advisory reports are acceptable for legacy debt; hard failures should be limited to malformed canonical entries or missing metadata in the curated current scope.
- Checker diagnostics should be useful but conservative: cap excerpts, avoid absolute paths in normal output, and redact obvious query-token or credential-like values without editing the source Markdown.

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
- Dev-story activation 2026-05-06: resolved workflow customization with no prepend/append steps; no `project-context.md` file was present in the workspace.
- Aspire pre-edit baseline 2026-05-06: `aspire run --detach --non-interactive --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json` succeeded; Aspire MCP showed sample, statestore, pubsub, and Dapr sidecars running/healthy, with Keycloak running but unhealthy on the known HTTPS readiness check. No apphost code changed.
- Red phase: unskipped `tests/Hexalith.EventStore.DeferredWorkGovernance.Tests` and confirmed 14 failures / 4 passes before implementation because the checker, governance section, docs-validation wiring, and Dev Agent evidence were absent.
- Baseline checker output before ledger edits used `_bmad-output/test-artifacts/deferred-work-governance/deferred-work-snapshot.md`: OPEN 0, STORY 14, ACCEPTED-DEBT 2, RESOLVED 22, DUPLICATE 1, NO-ACTION 2, unclassified 306, exit code 0 in legacy-advisory mode.
- After checker output for `_bmad-output/implementation-artifacts/deferred-work.md`: OPEN 3, STORY 14, ACCEPTED-DEBT 4, RESOLVED 24, DUPLICATE 1, NO-ACTION 3, unclassified 298, exit code 0 in legacy-advisory mode.
- Checker fixture output: `--fixture missing-open-metadata --json` exits 1 with `dw6-open-missing-owner`, `dw6-open-missing-next-review-date`, `dw6-story-missing-owner`, `dw6-story-missing-next-review-date`, and `dw6-missing-grouping`.
- Redaction/truncation evidence: `--fixture unclassified-live-bullet --json` exits 1 and reports `https://example.test/path?[redacted-query]` plus `password=[redacted]` in a capped excerpt.

### Completion Notes List

- Created ready-for-dev story from first backlog row after DW5 in the Post-Epic Deferred Work Cleanup package.
- No `project-context.md` file was present in the repository at story creation.
- Party-mode review tightened the governance handoff around disposition syntax, unresolved counts, legacy compatibility, validation boundaries, and required evidence fields.
- Advanced elicitation tightened the checker handoff around read-only execution, multi-marker precedence, exit-code documentation, diagnostic redaction, and reviewer evidence obligations.
- Implemented a read-only Python deferred-work checker with PowerShell and Bash wrappers, JSON/text output, built-in fixtures, deterministic sorting, line/heading/excerpt diagnostics, capped/redacted excerpts, advisory unknown-story checks, and explicit blocking/advisory exit-code behavior.
- Added `_bmad-output/implementation-artifacts/deferred-work.md` governance guidance for canonical case-sensitive markers, owner/date/grouping/rationale rules, legacy compatibility, multi-marker precedence, reviewer handoff, retrospective handoff, and checker usage.
- Wired the checker into PowerShell, Bash, and GitHub docs validation as advisory for legacy ledger findings.
- Touched deferred-work groups: DW4 operational-evidence validator follow-ups were marked OPEN with Test Architect ownership; DW1 projection/drain entries were marked accepted-debt/open/no-action according to existing DW1 rationale; DW2 governance-specific bullets were resolved/open-routed without editing generated preflight JSON; DW5 CR20 was resolved as a clarification-only ledger contradiction.
- Intentionally unclassified legacy sections remain as `dw6-unclassified-legacy-advisory`; planned follow-up path is future curated sweeps by heading group, not broad DW6 auto-normalization.
- Before unresolved count: OPEN 0 + STORY 14 + ACCEPTED-DEBT 2 = 16 owned/unresolved items in the snapshot. After unresolved count: OPEN 3 + STORY 14 + ACCEPTED-DEBT 4 = 21 owned/unresolved items after DW6 governance markers made hidden work explicit.
- Checker output confirms no blocking diagnostics in full-ledger legacy-advisory mode; unclassified historical bullets are advisory and visible with heading/excerpt/line.
- Confirmation: no product/runtime fixes were made, no generated preflight JSON audit files were edited, and no nested submodules were initialized or updated.
- Sub-bullet re-indentation in `deferred-work.md` (DW1/DW2/DW5 sections, 2-space to 4-space) was applied to satisfy markdownlint MD007 list-indent under the new MD038 disable comment; raw bullet text and ordering were preserved per AC #6, and the snapshot equality test enforces that no bullet content was deleted.

### File List

- `.github/workflows/docs-validation.yml`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/post-epic-deferred-dw6-deferred-work-governance.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/test-artifacts/deferred-work-governance/deferred-work-snapshot.md`
- `_bmad-output/test-artifacts/deferred-work-governance/entrypoint.txt`
- `scripts/check-deferred-work.py`
- `scripts/check-deferred-work.ps1`
- `scripts/check-deferred-work.sh`
- `scripts/validate-docs.ps1`
- `scripts/validate-docs.sh`
- `tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Dw6BookkeepingAtddTests.cs`
- `tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Dw6CheckerReportAtddTests.cs`
- `tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Dw6GovernanceVocabularyAtddTests.cs`
- `tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Dw6LedgerSweepAtddTests.cs`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Party-mode review trace is recorded inline; no sprint-status change was required.
- Advanced elicitation trace is recorded inline; no sprint-status change was required.
- `dotnet test tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Hexalith.EventStore.DeferredWorkGovernance.Tests.csproj --configuration Release -p:NuGetAudit=false` red phase: 14 failed, 4 passed before implementation. Post-review: 19 passed, 0 failed (one behavior-assertion test added in code-review patch P5).
- Checker before snapshot: OPEN 0, STORY 14, ACCEPTED-DEBT 2, RESOLVED 22, DUPLICATE 1, NO-ACTION 2, unclassified 306, exit 0 in legacy-advisory mode.
- Checker after sweep: OPEN 3, STORY 14, ACCEPTED-DEBT 4, RESOLVED 24, DUPLICATE 1, NO-ACTION 3, unclassified 298, exit 0 in legacy-advisory mode.
- Checker after code-review patches (canonical-brackets-authoritative rule + 7 new DW6-CR defer entries appended to ledger): OPEN 6, STORY 16, ACCEPTED-DEBT 6, RESOLVED 24, DUPLICATE 1, NO-ACTION 3, unclassified 298, exit 0. Net delta vs post-sweep: +3 OPEN / +2 STORY / +2 ACCEPTED-DEBT (= 7 new defer rows) and one previously-prose-classified STORY bullet now correctly classifies as OPEN under the new canonical-precedence rule.
- Local docs validation and CI validation run the deferred-work governance report as advisory, not blocking, for legacy ledger findings.
- The checker is read-only by default; fixture output proves diagnostic redaction for URL query strings and credential-looking values.
- Final focused validation: DW6 ATDD tests 18/18 passed; markdownlint on changed Markdown files reported 0 errors; `python -m py_compile scripts/check-deferred-work.py` passed; PowerShell and Bash checker wrappers produced valid help/report output.
- Pre-existing PS1↔SH asymmetry preserved: `validate-docs.ps1` (5 stages: lint, link, evidence, deferred-work, sample build) lacks the DAPR SDK version-pin stage that `validate-docs.sh` runs as Stage 4/6 via `scripts/check-doc-versions.sh`. This asymmetry pre-dates DW6 and is left untouched per story scope; flagged for a future doc-validation symmetry story.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-05 | 0.1 | Created ready-for-dev DW6 deferred-work governance story. | Codex automation |
| 2026-05-05 | 0.2 | Applied party-mode hardening for disposition semantics, checker determinism, legacy handling, validation boundaries, and evidence obligations. | Codex automation |
| 2026-05-05 | 0.3 | Applied advanced elicitation hardening for read-only checker behavior, multi-marker precedence, exit-code contract, and diagnostic redaction. | Codex automation |
| 2026-05-06 | 1.0 | Implemented DW6 governance convention, read-only checker, advisory validation wiring, curated ledger sweep, and validation evidence. | Codex |
| 2026-05-06 | 1.1 | Moved story to review after final focused validation passed. | Codex |
| 2026-05-06 | 1.2 | Applied 12 code-review patches (dead `--legacy-advisory` removed; canonical-bracket markers made authoritative in `classify()`; redaction extended for Bearer/JWT/PAT/AWS/basic-auth/colon-form credentials; ProcessStartInfo gets UTF-8 encoding + 1-min timeout; OS-dispatch fallback from pwsh → sh on non-Windows hosts without pwsh; behavior-assertion test added against `legacy-mixed-marker` fixture; PS1 stage numbering normalized to `/5`; argparse epilog documents exit-code contract; `--max-excerpt` minimum bound enforced; PS1↔SH asymmetry note + sub-bullet re-indentation rationale added to Verification Status / Completion Notes). 7 deferred items recorded as DW6-CR1..CR7. DW6 ATDD tests 19/19 pass; `py_compile` clean. | Claude |

## Party-Mode Review

- Date/time: 2026-05-05T18:06:03+02:00
- Selected story key: `post-epic-deferred-dw6-deferred-work-governance`
- Command/skill invocation used: `/bmad-party-mode post-epic-deferred-dw6-deferred-work-governance; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer).
- Findings summary: Reviewers agreed DW6 is directionally implementable but needed tighter pre-dev contracts so the governance work does not become a broad ledger rewrite, noisy CI migration, or judgment-heavy checker. The main risks were undefined disposition syntax, ambiguous unresolved-count semantics, unclear advisory versus blocking validation behavior, weak legacy compatibility proof, and incomplete Dev Agent Record evidence obligations.
- Changes applied: Added Party-Mode Hardening Notes; tightened Acceptance Criteria 1, 2, 4, 5, 9, and 10; added tasks for `STORY:<id>` existence and stale-date policy, canonical examples, deterministic output proof, legacy compatibility coverage, validation-boundary evidence, and no-runtime/no-nested-submodule confirmation; updated Dev Notes, Completion Notes, Verification Status, and Change Log.
- Findings deferred: Whether `ACCEPTED-DEBT` needs product-owner approval, whether `STORY:<id>` existence checks should be blocking, whether stale `next-review-date` should fail validation, the long-term owner taxonomy for deferred work, and whether unresolved-count thresholds should ever block CI require human product or architecture judgment.
- Final recommendation: needs-story-update

## Advanced Elicitation

- Date/time: 2026-05-05T18:09:43+02:00
- Selected story key: `post-epic-deferred-dw6-deferred-work-governance`
- Command/skill invocation used: `/bmad-advanced-elicitation post-epic-deferred-dw6-deferred-work-governance`
- Batch 1 method names: Self-Consistency Validation; Red Team vs Blue Team; Architecture Decision Records; Security Audit Personas; Failure Mode Analysis.
- Reshuffled Batch 2 method names: Chaos Monkey Scenarios; Occam's Razor Application; First Principles Analysis; 5 Whys Deep Dive; Lessons Learned Extraction.
- Findings summary: The story was already coherent after party-mode review, but the checker contract still had four failure-prone edges: report runs could accidentally become mutating cleanup, mixed legacy markers could depend on parser order, developers could wire CI without a readable exit-code contract, and unclassified excerpts could leak sensitive-looking evidence text into logs. The root cause was under-specifying the checker as a diagnostic tool rather than an editor.
- Changes applied: Tightened AC #4 to require read-only default behavior; tightened AC #5 to require deterministic primary disposition and compatibility context for multi-marker legacy entries; tightened AC #9 to require an explicit blocking/advisory/informational exit-code contract; added AC #11 for capped and redacted diagnostic excerpts; added tasks for exit-code documentation, read-only proof, and redaction/truncation evidence; updated Architecture Guardrails, Completion Notes, Verification Status, and Change Log.
- Findings deferred: Product-owner approval for `ACCEPTED-DEBT`, blocking `STORY:<id>` existence validation, stale `next-review-date` enforcement, long-term owner taxonomy, and unresolved-count thresholds remain deferred to human product or architecture judgment.
- Final recommendation: needs-story-update
