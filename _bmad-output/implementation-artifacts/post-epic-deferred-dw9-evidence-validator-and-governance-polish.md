# Post-Epic Deferred DW9: Evidence Validator and Governance Polish

Status: done

<!-- Source: sprint-change-proposal-2026-05-07-deferred-work-open-cleanup.md - Proposal D / DW9 -->
<!-- Source: deferred-work.md - operational evidence validator and DW6 governance routed entries -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer of EventStore evidence and governance tooling,
I want the operational evidence validator and deferred-work governance checks to cover the remaining routed gaps,
so that CI and local validation exercise the same entrypoints developers rely on.

## Story Context

DW9 is the third story from the approved Deferred-Work OPEN cleanup package. It owns four tooling-polish follow-ups that were routed from DW4 and DW6:

- The operational evidence validator checks that control fields are present, but it does not yet prove that a control result belongs to the same evidence run or a clearly linked control run.
- Repo-wide audit mode is currently risky because `validate_paths()` recursively scans every `*.md` under a directory and would process intentional template placeholders unless templates can opt out.
- GitHub docs validation invokes `scripts/check-deferred-work.py` directly, while local docs validation invokes wrapper scripts. Wrapper failures can therefore escape CI.
- `_bmad-output/test-artifacts/deferred-work-governance/entrypoint.txt` is committed, but the README still says future contributors declare it when implementation starts. The policy must become one source of truth.

This is a narrow tooling story. It must not reopen DW4 schema design, DW6 governance vocabulary, DW7 Admin UI lifecycle work, DW8 server classifier or identifier audit work, or any product/runtime behavior.

Current HEAD at story creation: `09ef237a`.

## Acceptance Criteria

1. **Cross-field control linkage is validated.** Given query or SignalR operational evidence records a required control result, when `scripts/validate-operational-evidence.py` validates the file, then it rejects controls that are blank, unrelated to the evidence run, or not explicitly tied to the same `evidence_run_id` or a clearly linked control run.

2. **Control-link diagnostics are precise.** Given a control-linkage failure, when diagnostics are emitted in text or JSON mode, then the rule id, section, field, line when available, and hint identify which control relationship is missing. Do not reuse a generic parser or missing-metadata diagnostic for this rule.

3. **Curated fixtures prove the new validator rule.** Add positive and negative fixtures under `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/` and update every fixture catalog/expected-rule source so `--self-test` proves the control-linkage pass and failure paths. Negative fixture assertions must check the new rule id, not only non-zero exit.

4. **Template opt-out is supported before repo-wide audits.** Given a Markdown file contains `<!-- evidence-validator: skip -->`, or its path matches the default template skip-list such as `**/*-template.md`, when a directory is validated, then the validator skips that file without producing placeholder, schema, or required-field diagnostics. Explicit validation of a skipped file should either skip with a clear informational result or fail with a documented "skipped by marker" diagnostic; choose one behavior and test it.

5. **Default fixture validation remains unchanged.** Given `scripts/validate-evidence.ps1 --self-test` or `bash scripts/validate-evidence.sh --self-test` runs, then the validator still runs only the curated fixture matrix and does not recursively audit historical evidence or repository templates by default.

6. **Docs-validation CI uses the deferred-work wrapper.** Given `.github/workflows/docs-validation.yml` runs the deferred-work governance report, then it invokes `scripts/check-deferred-work.sh` on Linux instead of `python scripts/check-deferred-work.py` directly. Local PowerShell and Bash docs-validation wrappers must remain aligned with that CI entrypoint policy.

7. **Wrapper exit-code semantics stay visible.** Given the deferred-work governance step is advisory today, when CI invokes the wrapper, then the workflow still documents whether exit code `1` is advisory and whether exit code `2` usage errors should be allowed to pass or fail. Do not accidentally make legacy advisory findings block PRs unless a human explicitly approves that policy change.

8. **Deferred-work governance entrypoint policy has one source of truth.** Given `_bmad-output/test-artifacts/deferred-work-governance/entrypoint.txt` is committed, then either `.gitignore` excludes that file and tests/readme explain that it is local-only, or the README declares the committed value canonical. Pick one policy and align README, tests, and any Dev Agent Record language with it.

9. **Deferred-work dispositions are narrow and auditable.** When development starts or completes, update only DW9-owned entries in `_bmad-output/implementation-artifacts/deferred-work.md`: the two DW4 validator entries and DW6-CR5/DW6-CR6. Do not sweep DW6-CR7, future parser-refinement, accepted-debt, DW7, DW8, or unrelated legacy bullets.

10. **Validation is targeted and recorded.** Before moving to `review`, run the operational evidence validator self-test, the focused operational-evidence validator test project if practical, the deferred-work governance wrapper smoke, and markdown/YAML checks for changed docs. Record exact commands and results in the Dev Agent Record. Do not run solution-level `dotnet test`.

11. **Bookkeeping is closed.** At dev handoff, update this story's Dev Agent Record, File List, Verification Status, and Change Log. Move this story and its sprint-status row to `review` only after validator fixtures, wrapper CI alignment, README or `.gitignore` policy alignment, narrow deferred-work dispositions, and validation evidence are recorded. Move both to `done` only after code-review signoff.

## Scope Boundaries

- Do not change query or SignalR operational evidence schema versions unless the story records a deferred architecture/product decision.
- Do not broaden docs validation from fixture self-test to full repository audit unless the skip-list behavior is implemented, tested, and still advisory.
- Do not rewrite the deferred-work checker parser, disposition vocabulary, owner taxonomy, stale-date policy, or accepted-debt approval model.
- Do not change product code, Admin UI, Server, DAPR components, Aspire apphost, package versions, or submodules.
- Do not initialize or update nested submodules.
- Do not edit generated preflight JSON audit files.

## DW9 Contract Constraints

### Validator Rule Contract

Use the repository's existing lower-case validator rule-id style. Additive rule ids are allowed; renaming existing rule ids is out of scope.

| Rule id | Checked fields | Failure trigger | Diagnostic contract | Minimum fixture coverage |
| --- | --- | --- | --- | --- |
| `control-linkage-missing` | Query `false_positive_control`, query `correlation_control`, SignalR `reliability_control` | Required control result is present but no same-run or linked-control-run reference can be found. | `section=Controls`; `field` is the failing control field; line when available; hint names the missing same-run or linked-control-run relationship. | At least one query negative and one SignalR negative fixture. |
| `control-linkage-unrelated` | Query `false_positive_control`, query `correlation_control`, SignalR `reliability_control` | A control reference exists but does not match the evidence file's `evidence_run_id` and is not explicitly linked to a control run named by the evidence. | `section=Controls`; `field` is the failing control field; line when available; hint names the mismatched evidence/control reference. | At least one negative fixture and one positive same-run or linked-run fixture. |

Do not reuse `control-required-missing`, `correlation-control-required-missing`, parser, metadata, or placeholder diagnostics for linkage failures. If implementation discovers that an additional linkage failure class is needed, record it in this table, `EXPECTED_FIXTURE_RULES`, and `Dw4FixtureCatalog` in the same change.

### Template Skip Contract

Directory validation must skip files when either condition is true:

- The file contains the exact marker `<!-- evidence-validator: skip -->`.
- The file path matches the default template pattern `**/*-template.md`.

Skipped files must be visible in human-readable output or JSON output as an informational skip with the reason `marker` or `template-pattern`; they must not count as pass or fail. Explicit validation of a skipped file should return the same informational skip result, not placeholder/schema diagnostics. Add one fixture or focused test proving a skipped template is quiet and one "looks like a template but does not match the marker or `**/*-template.md`" case that is still audited.

### Deferred-Work Allowlist

Only these deferred-work entries may be dispositioned by DW9:

- `_bmad-output/implementation-artifacts/deferred-work.md` line item for "No fixture for blank/whitespace/non-linked control result" routed from DW4.
- `_bmad-output/implementation-artifacts/deferred-work.md` line item for "Templates self-trigger placeholder noise if repo-wide audit mode is later enabled" routed from DW4.
- `DW6-CR5` for CI workflow direct Python invocation of the deferred-work checker.
- `DW6-CR6` for `entrypoint.txt` contradicting the governance README.

Do not fix, rewrite, or reclassify unrelated DW6 parser/vocabulary items, DW6-CR7, future parser-refinement entries, accepted-debt entries, DW7, DW8, Admin UI, Server, product/runtime, or legacy deferred-work bullets except to keep validator compatibility.

### Wrapper Exit-Code Policy

DW9 must document and preserve the current deferred-work checker semantics unless a human explicitly approves stricter PR gating:

| Exit code | Meaning | CI behavior for this story |
| ---: | --- | --- |
| `0` | No blocking governance failure. Advisory findings may still be printed. | Pass. |
| `1` | Blocking governance failure for malformed canonical `OPEN` or `STORY:<id>` entries, missing required metadata on those entries, or unclassified live bullets. | GitHub docs validation may remain reporting-only with `continue-on-error: true`; the step name or comment must make that policy visible. |
| `2` | Usage, configuration, or tool execution error, if the wrapper uses that convention. | Do not intentionally hide this as an advisory finding. If CI remains `continue-on-error: true`, document that usage errors are visible in job output but not yet PR-blocking. |

Local docs validation and GitHub docs validation should use the wrapper path (`scripts/check-deferred-work.sh` on Linux, `scripts/check-deferred-work.ps1` on PowerShell). Direct `python scripts/check-deferred-work.py ...` should remain only an internal/debug form when explicitly labeled as such.

### Governance Entrypoint Policy

Prefer the low-risk policy that the committed `_bmad-output/test-artifacts/deferred-work-governance/entrypoint.txt` is canonical fixture evidence if tests or docs consume it. If implementation instead treats it as local/generated state, it must update `.gitignore`, README, and tests together and record that policy change in the Dev Agent Record.

### Fixture Catalog Contract

Every new or changed fixture must be represented in both `EXPECTED_FIXTURE_RULES` and `tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Fixtures/Dw4FixtureCatalog.cs` with:

- Owning story `DW9`.
- Scenario purpose.
- Expected pass/fail result.
- Expected rule id for each negative fixture.

No unreferenced fixture files or catalog-only rows should be left behind. The `--self-test` fixture matrix may grow only by the intentionally added DW9 fixtures; it must not become a recursive historical evidence or template audit.

### Advanced Elicitation Hardening

#### Batch 1 Findings

- **Self-consistency validation:** Before editing the validator, capture the current curated fixture names, expected rule ids, and self-test count in the Dev Agent Record. The final evidence must explain exactly which DW9 fixtures were added so a larger-than-expected self-test expansion is review-blocking.
- **Red Team vs Blue Team:** Do not satisfy control linkage from adjacent prose, section headings, timestamps alone, or "looks related" text. The validator must use explicit machine-readable fields tied to the evidence `evidence_run_id` or to a named linked control run; ambiguous or multiple conflicting references fail closed with the new linkage rule ids.
- **Architecture decision records:** If implementation introduces a new metadata field for linked control runs, record the chosen field name and rationale in the story Dev Agent Record and evidence README. Keep the decision additive and fixture-local; do not rename existing schema fields or bump schema versions without recording a deferred architecture decision.
- **Security audit personas:** Treat skip markers as a trust boundary. The exact `<!-- evidence-validator: skip -->` marker and template-pattern skip are allowed only to suppress validator diagnostics for intentionally skipped files; they must not suppress wrapper execution failures, unreadable-file errors, or governance checker failures.
- **Failure mode analysis:** Each required control field is validated independently. A valid `false_positive_control` linkage must not satisfy `correlation_control`, and a query fixture must not silently cover the SignalR `reliability_control` path.

#### Batch 2 Findings

- **Chaos monkey scenarios:** Directory validation must behave deterministically when skipped files, valid evidence, invalid evidence, and unreadable or malformed files are mixed. The output should make skipped files visible without changing pass/fail totals for non-skipped files.
- **Occam's razor application:** Prefer the smallest reusable helper for control-link validation and the smallest path-normalization helper for skip patterns. Do not introduce a Markdown parser, broad schema framework, or repository-wide audit mode for this story.
- **First principles analysis:** DW9 succeeds only when the same developer-facing entrypoints are exercised in CI/local validation and in tests. Direct Python entrypoints may remain internal, but the acceptance evidence must prove wrapper symmetry where the story says wrappers are canonical.
- **5 Whys deep dive:** Deferred-work dispositions must be closed because the validator or wrapper behavior is proven by targeted evidence, not because the story touched nearby files. Each DW9-owned deferred item needs a command result or fixture/test reference in the Dev Agent Record.
- **Lessons learned extraction:** Reviewers should reject the story if new fixtures are not represented in both catalogs, skip diagnostics are invisible, wrapper exit-code behavior is not stated, unrelated deferred-work entries are edited, or preflight JSON audit files are staged.

## Implementation Inventory

| Area | File / artifact | Expected use |
| --- | --- | --- |
| Planning source | `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-deferred-work-open-cleanup.md` | Proposal D / DW9 scope and acceptance direction |
| Deferred source | `_bmad-output/implementation-artifacts/deferred-work.md` | DW9 routed validator and governance entries |
| Evidence validator | `scripts/validate-operational-evidence.py` | primary validator rules and `--self-test` fixture catalog |
| Evidence wrappers | `scripts/validate-evidence.ps1` and `scripts/validate-evidence.sh` | local validator entrypoints |
| Evidence fixtures | `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/` | positive and negative fixtures |
| Evidence README | `_bmad-output/test-artifacts/operational-evidence-validator/README.md` | fixture and validator behavior documentation |
| Evidence tests | `tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/` | fixture catalog, wiring, and validator contract tests |
| Deferred checker | `scripts/check-deferred-work.py`, `.ps1`, `.sh` | governance report and wrapper smoke target |
| Deferred governance docs | `_bmad-output/test-artifacts/deferred-work-governance/README.md` and `entrypoint.txt` | entrypoint policy alignment |
| CI workflow | `.github/workflows/docs-validation.yml` | wrapper-based governance report invocation |
| Local docs validation | `scripts/validate-docs.ps1` and `scripts/validate-docs.sh` | local validation symmetry checks |
| Sprint status | `_bmad-output/implementation-artifacts/sprint-status.yaml` | story status bookkeeping only |
| Run log | `_bmad-output/process-notes/predev-hardening-runs.log` | automation-created run trace |

## Current Code Intelligence

- `validate_operational_evidence.py` currently supports only `query-operational-evidence/v1` and `signalr-operational-evidence/v1`; unsupported schemas fail closed.
- `validate_required_value()` proves required control fields are non-empty using `clean_value()`, but it does not prove same-run or linked-run identity.
- Query evidence has two required controls today: `false_positive_control` and `correlation_control`. SignalR evidence has `reliability_control`.
- `validate_paths()` recursively expands directory arguments with `path.rglob("*.md")`. There is no skip marker or path skip-list today.
- `EXPECTED_FIXTURE_RULES` in the Python validator and `Dw4FixtureCatalog` in tests both pin fixture names and expected rule ids. Additions must update both or fixture coverage drifts.
- `scripts/validate-evidence.ps1` and `.sh` already delegate to `scripts/validate-operational-evidence.py`; keep that wrapper shape rather than adding a second validator.
- `.github/workflows/docs-validation.yml` currently runs `python scripts/check-deferred-work.py _bmad-output/implementation-artifacts/deferred-work.md` with `continue-on-error: true`.
- `scripts/validate-docs.ps1` and `.sh` already invoke `scripts/check-deferred-work.ps1` and `.sh` respectively, so CI is the asymmetric path.
- `_bmad-output/test-artifacts/deferred-work-governance/entrypoint.txt` currently declares `pwsh:scripts/check-deferred-work.ps1`; the README still describes the file as something to declare when implementation starts.

## Tasks / Subtasks

- [x] Task 0: Baseline routed defects and lock scope (AC: #1, #4, #6, #8, #9)
    - [x] 0.1 Re-read Proposal D / DW9 and the four DW9-owned deferred-work bullets.
    - [x] 0.2 Confirm the current evidence validator control checks and directory traversal behavior before editing.
    - [x] 0.3 Confirm current docs-validation CI and local docs-validation wrapper behavior.
    - [x] 0.4 Decide whether `entrypoint.txt` is committed canonical state or local-only state before editing README or `.gitignore`.
    - [x] 0.5 Confirm DW7 and DW8 entries remain out of scope.

- [x] Task 1: Add control-linkage validation (AC: #1, #2, #3)
    - [x] 1.1 Define the smallest explicit metadata convention that links a control result to the same `evidence_run_id` or a named linked control run.
    - [x] 1.2 Implement rule ids for missing or unrelated control linkage without weakening existing missing-control diagnostics.
    - [x] 1.3 Add query and SignalR positive/negative fixtures, or document why one schema cannot exercise the rule safely.
    - [x] 1.4 Update Python `EXPECTED_FIXTURE_RULES`, C# fixture catalog expectations, and rule-vocabulary tests together.

- [x] Task 2: Add template skip behavior (AC: #4, #5)
    - [x] 2.1 Add `<!-- evidence-validator: skip -->` marker detection before schema parsing.
    - [x] 2.2 Add default skip-list coverage for `**/*-template.md`.
    - [x] 2.3 Add fixtures or focused tests proving directory validation skips template files and does not emit placeholder noise.
    - [x] 2.4 Preserve `--self-test` behavior as curated fixtures only.

- [x] Task 3: Align deferred-work governance CI with wrappers (AC: #6, #7)
    - [x] 3.1 Change `.github/workflows/docs-validation.yml` to invoke `bash scripts/check-deferred-work.sh _bmad-output/implementation-artifacts/deferred-work.md`.
    - [x] 3.2 Preserve or explicitly revise `continue-on-error` behavior and document the exit-code trade-off.
    - [x] 3.3 Confirm local `scripts/validate-docs.ps1` and `.sh` still invoke wrapper scripts.
    - [x] 3.4 Run a local wrapper smoke and record the result.

- [x] Task 4: Reconcile deferred-work governance entrypoint policy (AC: #8)
    - [x] 4.1 Choose canonical committed entrypoint or local-only ignored entrypoint.
    - [x] 4.2 Align `_bmad-output/test-artifacts/deferred-work-governance/README.md` and either `.gitignore` or test expectations with that choice.
    - [x] 4.3 Avoid touching unrelated DW6 parser/vocabulary accepted-debt items.

- [x] Task 5: Update deferred-work dispositions narrowly (AC: #9)
    - [x] 5.1 Update only the cross-field control-linkage entry after validator rule coverage lands.
    - [x] 5.2 Update only the template skip-list entry after skip behavior lands.
    - [x] 5.3 Update only DW6-CR5 after CI wrapper invocation is aligned.
    - [x] 5.4 Update only DW6-CR6 after README or `.gitignore` policy is aligned.

- [x] Task 6: Validate and capture evidence (AC: #10, #11)
    - [x] 6.1 Run `.\scripts\validate-evidence.ps1 --self-test` and/or `bash scripts/validate-evidence.sh --self-test`.
    - [x] 6.2 Run `dotnet test tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests --configuration Release -p:NuGetAudit=false` if practical.
    - [x] 6.3 Run a deferred-work wrapper smoke through `.\scripts\check-deferred-work.ps1` or `bash scripts/check-deferred-work.sh`.
    - [x] 6.4 Run Markdown/YAML validation for changed BMAD artifacts.
    - [x] 6.5 Confirm generated preflight JSON files remain unstaged and no nested submodules were initialized or updated.

- [x] Task 7: Close story bookkeeping (AC: #11)
    - [x] 7.1 Update Dev Agent Record, File List, Verification Status, and Change Log.
    - [x] 7.2 Move this story and `sprint-status.yaml` to `review` only after implementation evidence is present.
    - [x] 7.3 Move both to `done` only after code-review signoff.

### Review Findings

Code review run on 2026-05-09 — Blind Hunter + Edge Case Hunter + Acceptance Auditor (parallel layers).

- [x] [Review][Decision] Canonical entrypoint policy contradicts CI invocation path — **Resolved 2026-05-09: option A (co-canonical).** Updated `_bmad-output/test-artifacts/deferred-work-governance/README.md` to declare `scripts/check-deferred-work.{ps1,sh}` co-canonical thin wrappers over `scripts/check-deferred-work.py`, with `entrypoint.txt` keeping the `pwsh:` value as the local-dev default and CI using the bash wrapper because GitHub Actions runners are Linux. Each wrapper must keep behavioural parity; changes to either must update `entrypoint.txt`, README, wrapper smoke commands, and ATDD test expectations together.
- [x] [Review][Patch] **HIGH** Skip marker substring match poisons the evidence-validator README, deferred-work ledger, and snapshot — `skip_reason_for` does a plain `SKIP_MARKER in text` substring scan, so any markdown that *describes* the marker (the validator's own README, `_bmad-output/implementation-artifacts/deferred-work.md:43`, `_bmad-output/test-artifacts/deferred-work-governance/deferred-work-snapshot.md:6`) is silently shadow-banned. Fix: anchor the scan to the pre-frontmatter region (first ~20 lines) or strip fenced code blocks before searching. Edge Case Hunter reproduced via `python scripts/validate-operational-evidence.py _bmad-output/test-artifacts/operational-evidence-validator/README.md` returning `INFO ... evidence-file-skipped` and exit 0. [`scripts/validate-operational-evidence.py:248-250, 293-301`]
- [x] [Review][Patch] **MEDIUM** `validate-docs.{sh,ps1}` print `PASSED:` after the deferred-work stage even when checker exits 2 (usage/tool error) — the PASSED banner masks real bugs (unparseable args, Python crash) into a green line, recreating the asymmetric-CI-vs-local risk DW6-CR5 was meant to close. Fix: print `FAILED:` (or `ADVISORY (exit N):`) when `exit_code -ne 0`. [`scripts/validate-docs.sh +83..+87`, `scripts/validate-docs.ps1 +55..+60`]
- [x] [Review][Patch] **MEDIUM** `extract_references` regex captures dot-suffixed ids and rstrips them, but the YAML metadata value retains the dot — yields false `control-linkage-unrelated` for ids like `dw9-edge-dot-001.`. Edge Case Hunter reproduced. Fix: stop rstripping (the regex character class already excludes `,;)` so only `.` survives) OR normalize both sides identically. [`scripts/validate-operational-evidence.py:557-561`]
- [x] [Review][Patch] **MEDIUM** `extract_references` rejects quoted YAML reference values — when authors write `evidence_run_id: "dw9-edge-quoted-001"` and `false_positive_control: ... evidence_run_id:"dw9-edge-quoted-001" ...`, YAML strips quotes from metadata but the inline scan does not (regex char class excludes `"`/`'`), producing false `control-linkage-missing`. Edge Case Hunter reproduced. Fix: extend the pattern to allow optional surrounding quotes: `(?:["']?)([A-Za-z0-9_.:-]+)(?:["']?)`. [`scripts/validate-operational-evidence.py:557-561`]
- [x] [Review][Patch] **MEDIUM** `*-template.md` skip pattern is case-sensitive on a Windows-first project (CRLF + UTF-8 per CLAUDE.md) — `Foo-Template.md` is fully audited while `foo-template.md` is skipped. Fix: `path.name.lower().endswith("-template.md")`. [`scripts/validate-operational-evidence.py:299`]
- [x] [Review][Patch] **LOW** Skip marker requires byte-exact spelling — `<!--   evidence-validator: skip   -->` (with HTML-comment-idiomatic inner whitespace) does NOT skip. Fix: tolerant regex `r"<!--\s*evidence-validator:\s*skip\s*-->"` and document that exact spelling is required if not relaxing. [`scripts/validate-operational-evidence.py:293-298`]
- [x] [Review][Patch] **LOW** New `level` JSON field is undocumented in the C# diagnostic contract — `Diagnostic.to_dict` now emits `"level"` always, but `Dw4Diagnostic` has no `Level` member, `ShellScriptValidatorInvoker` XML doc still lists only `file/schema/rule/section/field/line/hint`, and only DW9-specific tests rely on `Rule == "evidence-file-skipped"` to detect skips. Fix: add `Level` to `Dw4Diagnostic`, parse it in the invoker, update XML doc. [`scripts/validate-operational-evidence.py:131-146`, `tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/{Dw4Diagnostic.cs,ShellScriptValidatorInvoker.cs}`]
- [x] [Review][Patch] **LOW** New `skip-marker-optout.md`, `skip-template.md`, and `template-looking-invalid.md` fixtures live under the curated `fixtures/` directory but are not registered in `EXPECTED_FIXTURE_RULES` or `Dw4FixtureCatalog`, contradicting the spec's Fixture Catalog Contract ("every new or changed fixture must be in both registries"). The `--self-test` carve-out is necessary to preserve AC-5, but it is undocumented in the README/Dev Agent Record. Fix: add a one-paragraph explicit "skip-fixture catalog exception" to the validator README and Dev Agent Record so future fixture audits do not flag them as orphans. [`_bmad-output/test-artifacts/operational-evidence-validator/README.md`, story Dev Agent Record]
- [x] [Review][Patch] **LOW** `linked_control_run_ids` / `control_run_id` field-name choice missing rationale — spec asks for "field name AND rationale" in Dev Agent Record/README; current entries record only the names. Fix: add a 2-3 line rationale paragraph (additive, fixture-local, no schema bump) to the Dev Agent Record. [story Dev Agent Record line 272, `_bmad-output/test-artifacts/operational-evidence-validator/README.md`]
- [x] [Review][Patch] **LOW** File List does not annotate which positive fixtures were modified vs newly added — `query-valid-minimal.md`, `query-valid-not-applicable-aspire.md`, and `signalr-valid-minimal.md` had their control fields rewritten to use the new `evidence_run_id:<id>` linkage form, but the File List does not distinguish them from new files. Fix: add `(modified)` / `(new)` markers in the File List entries. [story File List]
- [x] [Review][Patch] **LOW** `_bmad-output/process-notes/predev-hardening-runs.log` listed in File List but not present in `git status` — either the file was edited and is unstaged (need to verify and stage) or it was inherited from the story template and should be removed from File List. [story File List line 284]
- [x] [Review][Defer] **LOW** `run_self_test` filters info diagnostics out of the rules set, so a future regression that silently relabels an error rule as info or makes skip over-eager on a pinned valid fixture would silently report PASS — speculative regression risk; no concrete failure mode in the diff. Track for follow-up validator hardening. [`scripts/validate-operational-evidence.py:200-212`] — deferred, follow-up
- [x] [Review][Defer] **LOW** `check-deferred-work.py` `load_sources` does not guard `path.exists()`, so missing-input usage errors propagate as exit 1 (FileNotFoundError) instead of the documented exit 2 — pre-existing in `check-deferred-work.py`, related to DW6-CR7 ACCEPTED-DEBT and out of DW9 scope (DW9 only required wrapper symmetry). [`scripts/check-deferred-work.py:215-221`] — deferred, pre-existing
- [x] [Review][Defer] **LOW** `ShellScriptValidatorInvoker.LocateRepoRoot` walks up from `AppContext.BaseDirectory` with no max-depth guard or `HEXALITH_REPOROOT`/`[CallerFilePath]` fallback — same pattern DW8 retro patched in `FindRepositoryRoot`; reintroduces a less-resilient sibling. Test-only impact under shadow-copy/extracted-test-runner scenarios. [`tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/ShellScriptValidatorInvoker.cs:109-128`] — deferred, follow-up

## Dev Notes

### Architecture Guardrails

- Treat validator rule ids and deferred-work disposition behavior as documentation tooling contracts. Additive rule ids are acceptable; renaming existing rule ids requires updating both Python and C# fixture catalogs.
- Keep the validator small and standard-library based. Do not add a Markdown parser dependency only for skip markers or simple control-link checks.
- Prefer explicit evidence metadata over inference from prose. Same-run control linkage should be machine-checkable and easy for reviewers to recognize.
- Keep wrapper behavior testable in CI. The point of DW9 is to exercise the shell wrapper path, not to bypass it with direct Python.
- Advisory governance checks may still return exit code 1. If CI should continue on advisory findings, keep that policy visible in workflow comments or step naming.
- Do not regenerate or commit preflight JSON audit files as part of this story's validation.

### Testing Guidance

- Start with the existing DW4 fixture catalog and self-test. Add fixtures before broadening validator behavior.
- Assert expected rule ids for each negative fixture so a parser failure cannot masquerade as control-linkage coverage.
- Use directory-level test inputs for skip-list behavior; file-level-only tests will not prove repo-wide audit safety.
- For wrapper CI alignment, test the wrapper command directly from repo root and inspect both exit code and readable output.
- Include a before/after self-test note in the Dev Agent Record: fixture count/output may change only by the explicitly added DW9 fixtures, and default validation must remain curated fixture validation rather than repo-wide audit.
- Do not run solution-level `dotnet test`.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-deferred-work-open-cleanup.md#Proposal-D-DW9-Evidence-Validator-Governance-CI-Docs-Polish`] - DW9 scope and acceptance direction.
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md#Deferred-from-code-review-of-post-epic-deferred-dw4-operational-evidence-schema-validation-2026-05-05`] - routed control-linkage and template skip-list entries.
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md#Deferred-from-code-review-of-post-epic-deferred-dw6-deferred-work-governance-2026-05-06`] - routed DW6-CR5 and DW6-CR6 entries.
- [Source: `_bmad-output/implementation-artifacts/post-epic-deferred-dw4-operational-evidence-schema-validation.md`] - original evidence-validator story and accepted boundaries.
- [Source: `_bmad-output/implementation-artifacts/post-epic-deferred-dw6-deferred-work-governance.md`] - governance checker implementation, review patches, and deferred follow-ups.
- [Source: `scripts/validate-operational-evidence.py`] - validator to extend.
- [Source: `_bmad-output/test-artifacts/operational-evidence-validator/README.md`] - fixture and validator command documentation.
- [Source: `tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Fixtures/Dw4FixtureCatalog.cs`] - C# fixture expectation source.
- [Source: `.github/workflows/docs-validation.yml`] - CI deferred-work governance report step.
- [Source: `_bmad-output/test-artifacts/deferred-work-governance/README.md`] - entrypoint policy to reconcile.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Pre-dev hardening preflight: `_bmad-output/process-notes/predev-preflight-2026-05-07T060109Z.json`, timestamp `2026-05-07T06:01:09Z`, result `pass`.
- Create-story activation: resolved workflow customization with no prepend/append steps; no `project-context.md` file was present in the workspace.
- Aspire pre-edit baseline: `aspire run --detach --non-interactive --apphost src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json` succeeded and returned dashboard URL `https://localhost:17017/login?t=ba423de6424509000c7433aef5e657c2`. Aspire MCP resource snapshot showed sample, DAPR sidecars, statestore, and pubsub running/healthy; Keycloak was running with the known HTTPS readiness health-check failure; EventStore/Admin/Tenants projects were waiting on Keycloak. No apphost code was changed.
- Dev-story activation on 2026-05-09: resolved workflow customization with no prepend/append steps; no `project-context.md` file was present in the workspace. `aspire run --detach --non-interactive --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json` succeeded, stopped the previous instance, and returned dashboard URL `https://localhost:17017/login?t=0134b282facee0328c32a5b5b28b9b28`. Sprint status moved `ready-for-dev` -> `in-progress`.

### Completion Notes List

- Created ready-for-dev story from the first backlog row in the Post-Epic Deferred Work OPEN Cleanup package.
- Scoped DW9 to four routed tooling items: evidence-validator control linkage, evidence-validator template skip behavior, deferred-work CI wrapper symmetry, and deferred-work governance entrypoint policy.
- Recorded current implementation targets for the Python validators, wrappers, fixture catalogs, docs-validation workflow, and governance README.
- Party-mode review applied pre-dev contract hardening for validator rule ids, template skip reporting, deferred-work entry allowlist, wrapper exit-code policy, entrypoint source of truth, and fixture catalog invariants.
- Advanced elicitation applied pre-dev hardening for machine-readable control linkage, skip-marker trust boundaries, deterministic mixed directory validation, wrapper symmetry evidence, and review rejection criteria.
- Implemented explicit control-link validation using `evidence_run_id:<same-run>` or `control_run_id:<linked-control-run>` references plus optional `linked_control_run_ids`; added precise `control-linkage-missing` and `control-linkage-unrelated` diagnostics.
- **Field-name ADR (DW9, additive only):** Chose `evidence_run_id:<id>` and `control_run_id:<id>` inline reference tokens plus an optional metadata field `linked_control_run_ids` (whitespace/comma/semicolon-separated). Rationale: (1) inline tokens reuse the existing free-form metadata payloads that authors already write into `false_positive_control` / `correlation_control` / `reliability_control`, so no schema bump is required and existing schema versions (`query-operational-evidence/v1`, `signalr-operational-evidence/v1`) remain stable; (2) names mirror the existing `evidence_run_id` metadata key so the same identifier travels both ways with no translation; (3) the optional `linked_control_run_ids` set lives in metadata rather than per-control because a single evidence run typically links a small fixed set of control runs reused across multiple controls. Alternatives considered and rejected: bumping schema to v2 with structured `controls[]` (too invasive for a polish story; reopens DW4 schema design); using opaque `link:<id>` tokens (loses the same-run vs linked-run distinction the validator must enforce). Decision recorded here so a future schema-v2 story has the prior art.
- Preserved existing required-control diagnostics by running linkage checks only after older schema, required-field, profile, placeholder, redaction, and parse checks are clean.
- Added DW9 query and SignalR linked-control positive fixtures plus missing/unrelated negative fixtures; curated self-test grew intentionally from 37 to 43 fixtures.
- Added informational skip handling for exact `<!-- evidence-validator: skip -->` marker and `*-template.md` paths, with focused tests covering skipped files and template-looking files that remain audited.
- Aligned deferred-work governance CI to call `bash scripts/check-deferred-work.sh`; local docs wrappers remain wrapper-based and now document advisory exit-code behavior.
- Declared committed deferred-work governance `entrypoint.txt` canonical in the README rather than local-only generated state.
- Resolved only the four DW9-owned deferred-work entries: two operational-evidence-validator rows plus DW6-CR5 and DW6-CR6.

### File List

- `_bmad-output/implementation-artifacts/post-epic-deferred-dw9-evidence-validator-and-governance-polish.md` (modified — story bookkeeping)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified — moved DW9 row to `review`)
- `.github/workflows/docs-validation.yml` (modified — wrapper invocation)
- `_bmad-output/implementation-artifacts/deferred-work.md` (modified — narrow DW9-allowlisted dispositions only)
- `_bmad-output/test-artifacts/deferred-work-governance/README.md` (modified — entrypoint policy clarified)
- `_bmad-output/test-artifacts/operational-evidence-validator/README.md` (modified — control-linkage and skip-marker semantics)
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-invalid-control-linkage-missing.md` (new — DW9 negative fixture)
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-invalid-control-linkage-unrelated.md` (new — DW9 negative fixture)
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-valid-linked-control-run.md` (new — DW9 positive linked-run fixture)
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-valid-minimal.md` (modified — control fields rewritten to use new explicit `evidence_run_id:<id>` linkage form)
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/query-valid-not-applicable-aspire.md` (modified — control fields rewritten to use new explicit `evidence_run_id:<id>` linkage form)
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/signalr-invalid-control-linkage-missing.md` (new — DW9 negative fixture)
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/signalr-invalid-control-linkage-unrelated.md` (new — DW9 negative fixture)
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/signalr-valid-linked-control-run.md` (new — DW9 positive linked-run fixture)
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/signalr-valid-minimal.md` (modified — control fields rewritten to use new explicit `evidence_run_id:<id>` linkage form)
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/skip-marker-optout.md` (new — DW9 skip-marker fixture; intentionally outside `EXPECTED_FIXTURE_RULES` to preserve AC-5 self-test invariant)
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/skip-template.md` (new — DW9 template-pattern skip fixture; intentionally outside `EXPECTED_FIXTURE_RULES` to preserve AC-5 self-test invariant)
- `_bmad-output/test-artifacts/operational-evidence-validator/fixtures/template-looking-invalid.md` (new — DW9 template-looking fixture that remains audited; intentionally outside `EXPECTED_FIXTURE_RULES`)
- `scripts/validate-docs.ps1` (modified — wrapper PASSED banner is now FAILED on non-zero exit, plus deferred-work wrapper invocation)
- `scripts/validate-docs.sh` (modified — wrapper PASSED banner is now FAILED on non-zero exit, plus deferred-work wrapper invocation)
- `scripts/validate-operational-evidence.py` (modified — added `control-linkage-missing`/`control-linkage-unrelated` rules; tolerant skip-marker matcher anchored to first 20 lines and stand-alone HTML comment; quote-tolerant `extract_references`; case-insensitive `*-template.md`)
- `tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Dw4Diagnostic.cs` (modified — added `Level` field for the new info/error JSON contract)
- `tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Dw4RuleVocabulary.cs` (modified — registered `control-linkage-missing` and `control-linkage-unrelated`)
- `tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Dw9EvidenceValidatorPolishTests.cs` (new — DW9 polish tests for linkage rules and skip handling)
- `tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Fixtures/Dw4FixtureCatalog.cs` (modified — added DW9 linkage fixture rows)
- `tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/ShellScriptValidatorInvoker.cs` (modified — repo-root-relative working directory; XML doc + JSON parser now include `level` field)

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Preflight passed before story creation.
- AppHost baseline run succeeded before edits; Keycloak showed the known HTTPS readiness health-check failure while supporting resources were running/healthy.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, package versions, or submodules.
- `.\scripts\validate-evidence.ps1 --self-test` passed: 43 fixtures checked; baseline before DW9 was 37 fixtures.
- `bash scripts/validate-evidence.sh --self-test` passed: 43 fixtures checked.
- `dotnet test tests\Hexalith.EventStore.OperationalEvidence.Validator.Tests --configuration Release -p:NuGetAudit=false` passed: 9 passed, 50 skipped, 0 failed.
- `.\scripts\check-deferred-work.ps1 _bmad-output/implementation-artifacts/deferred-work.md` returned exit code 1 as expected for current advisory ledger findings.
- `bash scripts/check-deferred-work.sh _bmad-output/implementation-artifacts/deferred-work.md` returned exit code 1 as expected for current advisory ledger findings.
- `python scripts\validate-operational-evidence.py --json _bmad-output\test-artifacts\operational-evidence-validator\fixtures | python -m json.tool` passed, proving directory-mode JSON output stays well-formed with skip diagnostics mixed in.
- Focused markdownlint passed for changed validator/governance READMEs and clean DW9 fixtures. Broad fixture-glob markdownlint still reports intentionally malformed parse fixtures and pre-existing story-list indentation; full `scripts/validate-docs.sh` is blocked in this workspace because `lychee` is not installed.
- `git diff --check` passed with line-ending warnings only; no generated preflight JSON files were staged or modified, and no submodules were initialized or updated.
- **Code review (2026-05-09):** Three-layer parallel review (Blind Hunter + Edge Case Hunter + Acceptance Auditor). 1 HIGH, 4 MEDIUM, 7 LOW patches applied; 1 decision-needed resolved (co-canonical entrypoint policy); 3 LOW deferred to follow-up. Post-patch validation: `python scripts/validate-operational-evidence.py --self-test` 43/43 PASS; `dotnet test tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests --configuration Release -p:NuGetAudit=false --no-build` 9 passed / 50 skipped / 0 failed. Subprocess regression checks confirm: `Foo-Template.md` now skips on case-insensitive `*-template.md`; `<!--   evidence-validator: skip   -->` (whitespace-tolerant) skips; prose mention of the marker buried in body or in inline backticks no longer shadow-bans the file; quoted YAML reference values are recognized by `extract_references`; trailing punctuation on inline references no longer produces false `control-linkage-unrelated`.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-07 | 0.1 | Created ready-for-dev DW9 evidence validator and governance polish story. | Codex automation |
| 2026-05-07 | 0.2 | Applied party-mode pre-dev contract hardening for validator, wrapper, fixture, and governance boundaries. | Codex automation |
| 2026-05-07 | 0.3 | Applied advanced elicitation hardening for validator linkage, skip, wrapper, and disposition evidence. | Codex automation |
| 2026-05-09 | 1.0 | Implemented DW9 validator linkage, template skip behavior, deferred-work wrapper CI alignment, canonical governance entrypoint docs, narrow deferred-work dispositions, and validation evidence; moved story to review. | Codex |
| 2026-05-09 | 1.1 | Applied bmad-code-review patches: skip-marker line-anchored to first 20 lines (prose-poison fix), whitespace-tolerant marker regex, case-insensitive `*-template.md`, quote-tolerant `extract_references`, symmetric trailing-punctuation normalization on both reference sides, `validate-docs.{sh,ps1}` no longer prints PASSED on non-zero exit, JSON `level` field plumbed through `Dw4Diagnostic`/`ShellScriptValidatorInvoker`, validator README documents skip-fixture catalog carve-out, governance README declares pwsh and bash co-canonical wrappers, Dev Agent Record records field-name ADR rationale, File List annotated `(new)`/`(modified)`, `predev-hardening-runs.log` removed from File List. Three LOW findings deferred. | Claude (bmad-code-review) |

## Party-Mode Review

- Date/time: 2026-05-07T09:04:22+02:00
- Selected story key: `post-epic-deferred-dw9-evidence-validator-and-governance-polish`
- Command/skill invocation used: `/bmad-party-mode post-epic-deferred-dw9-evidence-validator-and-governance-polish; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary:
  - Validator linkage behavior was too prose-driven and needed stable rule ids, diagnostic contracts, and fixture mapping before development.
  - Template skip behavior needed exact predicates and visible skip reporting to avoid silent audit gaps.
  - Deferred-work wrapper exit-code behavior and CI reporting policy needed an explicit truth table.
  - `entrypoint.txt` needed a clear canonical-or-generated policy before implementation.
  - DW9-owned deferred-work entries needed an allowlist to prevent cleanup of unrelated DW6/DW7/DW8 debt.
  - Fixture catalog alignment and self-test preservation needed stronger definition of done.
- Changes applied:
  - Added `## DW9 Contract Constraints` with validator rule contract, template skip contract, deferred-work allowlist, wrapper exit-code policy, governance entrypoint policy, and fixture catalog contract.
  - Added Dev Notes guardrail against regenerating or committing preflight JSON during this story.
  - Added Testing Guidance for before/after self-test fixture-shape evidence.
  - Updated Completion Notes and Change Log with the party-mode hardening result.
- Findings deferred:
  - Any change to make deferred-work governance block PRs is deferred for explicit human approval.
  - Any broader deferred-work parser, vocabulary, owner taxonomy, stale-date, accepted-debt, DW6-CR7, DW7, DW8, product/runtime, Aspire, DAPR, package, or submodule cleanup remains out of scope.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- Date/time: 2026-05-07T10:41:18+02:00
- Selected story key: `post-epic-deferred-dw9-evidence-validator-and-governance-polish`
- Command/skill invocation used: `/bmad-advanced-elicitation post-epic-deferred-dw9-evidence-validator-and-governance-polish`
- Batch 1 method names: Self-Consistency Validation; Red Team vs Blue Team; Architecture Decision Records; Security Audit Personas; Failure Mode Analysis
- Reshuffled Batch 2 method names: Chaos Monkey Scenarios; Occam's Razor Application; First Principles Analysis; 5 Whys Deep Dive; Lessons Learned Extraction
- Findings summary:
  - Control-linkage validation needed an explicit machine-readable basis and independent per-control checks.
  - Skip-marker behavior needed trust-boundary wording and deterministic mixed-directory output expectations.
  - Wrapper symmetry needed acceptance evidence from the same developer-facing entrypoints used locally and in CI.
  - Deferred-work dispositions needed proof-backed closure criteria and reviewer rejection signals.
- Changes applied:
  - Added `### Advanced Elicitation Hardening` with batch findings for validator linkage, skip handling, fixture drift, wrapper symmetry, deferred-work closure, and review rejection criteria.
  - Updated Completion Notes and Change Log with the elicitation result.
- Findings deferred:
  - Any schema version bump or existing schema-field rename remains deferred for explicit architecture approval.
  - Any stricter PR-blocking governance policy remains deferred for human approval.
  - Repository-wide audit mode and broad deferred-work parser or vocabulary cleanup remain out of scope.
- Final recommendation: ready-for-dev
