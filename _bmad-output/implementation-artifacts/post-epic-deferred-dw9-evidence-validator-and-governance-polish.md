# Post-Epic Deferred DW9: Evidence Validator and Governance Polish

Status: ready-for-dev

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

- [ ] Task 0: Baseline routed defects and lock scope (AC: #1, #4, #6, #8, #9)
    - [ ] 0.1 Re-read Proposal D / DW9 and the four DW9-owned deferred-work bullets.
    - [ ] 0.2 Confirm the current evidence validator control checks and directory traversal behavior before editing.
    - [ ] 0.3 Confirm current docs-validation CI and local docs-validation wrapper behavior.
    - [ ] 0.4 Decide whether `entrypoint.txt` is committed canonical state or local-only state before editing README or `.gitignore`.
    - [ ] 0.5 Confirm DW7 and DW8 entries remain out of scope.

- [ ] Task 1: Add control-linkage validation (AC: #1, #2, #3)
    - [ ] 1.1 Define the smallest explicit metadata convention that links a control result to the same `evidence_run_id` or a named linked control run.
    - [ ] 1.2 Implement rule ids for missing or unrelated control linkage without weakening existing missing-control diagnostics.
    - [ ] 1.3 Add query and SignalR positive/negative fixtures, or document why one schema cannot exercise the rule safely.
    - [ ] 1.4 Update Python `EXPECTED_FIXTURE_RULES`, C# fixture catalog expectations, and rule-vocabulary tests together.

- [ ] Task 2: Add template skip behavior (AC: #4, #5)
    - [ ] 2.1 Add `<!-- evidence-validator: skip -->` marker detection before schema parsing.
    - [ ] 2.2 Add default skip-list coverage for `**/*-template.md`.
    - [ ] 2.3 Add fixtures or focused tests proving directory validation skips template files and does not emit placeholder noise.
    - [ ] 2.4 Preserve `--self-test` behavior as curated fixtures only.

- [ ] Task 3: Align deferred-work governance CI with wrappers (AC: #6, #7)
    - [ ] 3.1 Change `.github/workflows/docs-validation.yml` to invoke `bash scripts/check-deferred-work.sh _bmad-output/implementation-artifacts/deferred-work.md`.
    - [ ] 3.2 Preserve or explicitly revise `continue-on-error` behavior and document the exit-code trade-off.
    - [ ] 3.3 Confirm local `scripts/validate-docs.ps1` and `.sh` still invoke wrapper scripts.
    - [ ] 3.4 Run a local wrapper smoke and record the result.

- [ ] Task 4: Reconcile deferred-work governance entrypoint policy (AC: #8)
    - [ ] 4.1 Choose canonical committed entrypoint or local-only ignored entrypoint.
    - [ ] 4.2 Align `_bmad-output/test-artifacts/deferred-work-governance/README.md` and either `.gitignore` or test expectations with that choice.
    - [ ] 4.3 Avoid touching unrelated DW6 parser/vocabulary accepted-debt items.

- [ ] Task 5: Update deferred-work dispositions narrowly (AC: #9)
    - [ ] 5.1 Update only the cross-field control-linkage entry after validator rule coverage lands.
    - [ ] 5.2 Update only the template skip-list entry after skip behavior lands.
    - [ ] 5.3 Update only DW6-CR5 after CI wrapper invocation is aligned.
    - [ ] 5.4 Update only DW6-CR6 after README or `.gitignore` policy is aligned.

- [ ] Task 6: Validate and capture evidence (AC: #10, #11)
    - [ ] 6.1 Run `.\scripts\validate-evidence.ps1 --self-test` and/or `bash scripts/validate-evidence.sh --self-test`.
    - [ ] 6.2 Run `dotnet test tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests --configuration Release -p:NuGetAudit=false` if practical.
    - [ ] 6.3 Run a deferred-work wrapper smoke through `.\scripts\check-deferred-work.ps1` or `bash scripts/check-deferred-work.sh`.
    - [ ] 6.4 Run Markdown/YAML validation for changed BMAD artifacts.
    - [ ] 6.5 Confirm generated preflight JSON files remain unstaged and no nested submodules were initialized or updated.

- [ ] Task 7: Close story bookkeeping (AC: #11)
    - [ ] 7.1 Update Dev Agent Record, File List, Verification Status, and Change Log.
    - [ ] 7.2 Move this story and `sprint-status.yaml` to `review` only after implementation evidence is present.
    - [ ] 7.3 Move both to `done` only after code-review signoff.

## Dev Notes

### Architecture Guardrails

- Treat validator rule ids and deferred-work disposition behavior as documentation tooling contracts. Additive rule ids are acceptable; renaming existing rule ids requires updating both Python and C# fixture catalogs.
- Keep the validator small and standard-library based. Do not add a Markdown parser dependency only for skip markers or simple control-link checks.
- Prefer explicit evidence metadata over inference from prose. Same-run control linkage should be machine-checkable and easy for reviewers to recognize.
- Keep wrapper behavior testable in CI. The point of DW9 is to exercise the shell wrapper path, not to bypass it with direct Python.
- Advisory governance checks may still return exit code 1. If CI should continue on advisory findings, keep that policy visible in workflow comments or step naming.

### Testing Guidance

- Start with the existing DW4 fixture catalog and self-test. Add fixtures before broadening validator behavior.
- Assert expected rule ids for each negative fixture so a parser failure cannot masquerade as control-linkage coverage.
- Use directory-level test inputs for skip-list behavior; file-level-only tests will not prove repo-wide audit safety.
- For wrapper CI alignment, test the wrapper command directly from repo root and inspect both exit code and readable output.
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

### Completion Notes List

- Created ready-for-dev story from the first backlog row in the Post-Epic Deferred Work OPEN Cleanup package.
- Scoped DW9 to four routed tooling items: evidence-validator control linkage, evidence-validator template skip behavior, deferred-work CI wrapper symmetry, and deferred-work governance entrypoint policy.
- Recorded current implementation targets for the Python validators, wrappers, fixture catalogs, docs-validation workflow, and governance README.

### File List

- `_bmad-output/implementation-artifacts/post-epic-deferred-dw9-evidence-validator-and-governance-polish.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/process-notes/predev-hardening-runs.log`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Preflight passed before story creation.
- AppHost baseline run succeeded before edits; Keycloak showed the known HTTPS readiness health-check failure while supporting resources were running/healthy.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, package versions, or submodules.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-07 | 0.1 | Created ready-for-dev DW9 evidence validator and governance polish story. | Codex automation |
