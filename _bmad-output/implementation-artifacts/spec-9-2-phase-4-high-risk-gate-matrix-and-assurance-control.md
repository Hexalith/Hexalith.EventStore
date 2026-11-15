---
title: '9.2 Phase 4 gate risk matrix and Assurance Control validator'
type: 'feature'
created: '2026-11-15'
status: 'done'
route: 'dispatch'
story_key: '9-2-phase-4-high-risk-gate-matrix-and-assurance-control'
review_loop_iteration: 0
baseline_commit: '9d715d480c2a0243f863608bcaa3138bd03c43a6'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-9-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** G-HIGH-RISK (OR10, OR13) is FAIL/BLOCKED. No matrix classifies the 17 PRD Phase 4 gates, and nothing computes or checks the Assurance Control level, so a high-risk result can overstate the assurance behind it.

**Approach:** Add a tracked gate risk matrix, the first owner-role registry, and an offline stdlib validator that rejects incomplete classification and overstated assurance. Prove every rejection with fixtures in a new dedicated workflow whose live job reports a truthful FAIL. Scope is Story 9.2 ACs 1–4 plus the record checks of AC 6 (owner split, 2026-11-15).

**Decisions (owner, 2026-11-15):**
- **Location:** track `_bmad-output/implementation-artifacts/phase-4-gate-risk-matrix.json` and `owner-role-registry.json`. Drop the `evidence/` segment from `prd.md:711` and from the Story 9.2 AC.
- **Story 9.1 dependency:** mirror the approved Story 9.3 clarification into Story 9.2 and the G-HIGH-RISK row. Deleted 9.1 tooling is not a prerequisite. This `/bmad-build 9.2` request is the owner's authorization, recorded here.
- **Classification:** 12 gates are `high-risk`: G-OQ8, G-TENANT, G-STATUS-ID, G-APPEND, G-COMPAT, G-AUTH-HOSTS, G-RUNTIME-PARITY, G-PUBLICATION-AUTH, G-CONSUMER, G-HIGH-RISK, G-MVP-COVERAGE and G-READINESS. Five are `standard-control`: G-BASELINE `planning-consistency`, G-CLAUSE `traceability-accounting`, G-NFR8 `approved-spec-no-seal`, G-NFR18 `documentation-review` and G-NFR-OWNERSHIP `ownership-accounting`.
- **Required check:** the fixture job is added to the `main` ruleset only on the owner's go-ahead, after its first green run on `main`. Record the command; this build does not run it.

## Boundaries & Constraints

**Always:** Pin the 17 gate IDs in the validator independently of the matrix, and also reject any PRD gate-table row the matrix omits. Compute the required level from distinct registry humans: one gives `single-maintainer-attested`, two or more give `independent`; `bmad:*` and CI identities never count. Compute the achieved level from evidence and reject a declared label that differs. A downstream record carries the lowest level of its inputs. Bind the live result to the source SHA, the workflow SHA-256 and every input SHA-256; it is evidence, never a seal. Follow the Story 4.17 validator, test and workflow conventions.

**Never:**
- Seal-retrieving transition workflows, GitHub API calls or predecessor re-validation (deferred in `deferred-work.md`, 2026-11-15).
- The G-RUNTIME-PARITY evaluation (AC 9 is follow-on work).
- Attestations, seals or a passing achieved level for any real row.
- Edits to `.github/workflows/ci.yml` or `docs/ci.md`, or recreating deleted Story 9.1 or 3.15 evidence machinery.
- Readiness, release, deployment or migration authority.
- A commit, push or GitHub settings change without a further instruction.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Real repository | 17 classified rows, registry naming one human, no attestations or seals | Classification passes; each high-risk row fails on unbound fields or achieved `none` below required | Exit 1, truthful FAIL per row |
| Positive control | Fixture with complete bindings, a seal record, an attestation ≥ 24 h after the last subject commit, correct labels | PASS | Exit 0 |
| Inventory drift | Missing, duplicate or unknown gate; PRD row absent from matrix; invalid classification; standard row without reason code | Reject | Gate-ID diagnostic |
| Overstated level | `independent` with one human; approver is the author; approver aliases the author (same GitHub numeric id); `bmad:*` or CI approver | Reject | Per-row code |
| Window and binding | Attestation < 24 h after the last subject change; subject digest mismatch; achieved above `none` without a seal; declared required ≠ computed; achieved below required | Reject | Per-row code |
| Registry switch | Registry fixture naming one human, another naming two | Required level `single-maintainer-attested`, then `independent` | — |
| Downstream | Record declares a level above the lowest of its inputs | Reject | Record diagnostic |
| Bad input | Missing, unreadable, symlinked or malformed JSON | Reject | Exit 2 |

</frozen-after-approval>

## Code Map

- `_bmad-output/planning-artifacts/prd.md:697-715` -- gate table with the 17 rows; `:711` G-HIGH-RISK, `:216` Assurance Control, `:734` and `:736` OR10/OR13.
- `_bmad-output/planning-artifacts/epics.md:7902-7962` -- Story 9.2 ACs; `:7975` the Story 9.3 clarification wording.
- `tools/validate-oq8-governing-design.py`, `tools/tests/test_oq8_governing_design.py`, `.github/workflows/oq8-governing-design.yml` -- reuse their conventions:
  - stdlib only, with `read_local` refusing symlinks;
  - temporary-repository fixtures;
  - `actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1` with `persist-credentials: false`;
  - separate fixtures and live-evaluation jobs, reporting to `$GITHUB_STEP_SUMMARY`.
- `.gitattributes`, `.editorconfig` -- `*.py` is already LF; add LF rules for the new JSON files so their digests are stable.
- `scripts/check-committed-files.sh` -- new tracked files must stay outside `evidence/` and under 1 MiB.
- Identity format from the deleted 3.15 registry (`git show cdad82781`; do not restore it): `github:<login>` and `bmad:<persona>`. The owner is `github:jpiquot`, numeric id 6775094.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/owner-role-registry.json` -- identities (`id`, `kind`: human, tool-persona or ci; GitHub numeric id for humans) and each PRD owner role mapped to an identity; the owner holds every role.
- [x] `_bmad-output/implementation-artifacts/phase-4-gate-risk-matrix.json` -- the 17 rows, classified as decided.
  - High-risk rows bind command, trigger, subject paths, evidence identity, pass condition, author and evaluator roles, required and achieved level, guarded transitions, `attestations: []` and `seal: null`.
  - A binding not yet defined is `null`, with its owning story named, so it fails truthfully.
  - Add one READY downstream record over all high-risk rows.
- [x] `tools/validate-phase-4-gate-risk-matrix.py` -- the validator, taking `<matrix>` with the registry path read from the matrix: exit codes 0/1/2, stable per-row codes, and `--result <path>` writing a JSON result with input digests.
- [x] `tools/tests/test_phase_4_gate_risk_matrix.py` -- one negative fixture per code beside a positive control, temporary Git repositories with fixed commit dates, CLI exit codes, and a real-repository truthful-FAIL assertion.
- [x] `.github/workflows/phase-4-gate-risk-matrix.yml` -- with `contents: read`:
  - job `fixtures`, named `Phase 4 gate risk matrix fixtures` (the ruleset context), runs on push and PR to `main` and on dispatch;
  - `live-evaluation` runs on push to `main` and on dispatch with `fetch-depth: 0`, exits with the validator status, and uploads the result artifact `if: always()`.
- [x] `.gitattributes`, `.editorconfig` -- LF rules for both JSON inputs.
- [x] `prd.md:711`, Story 9.2 in `epics.md`, `epic-9-context.md` -- apply the location and Story 9.1 decisions. Change nothing else in the gate rows.

**Acceptance Criteria:**
- Given the fixture suite, when it runs, then each rejection path fails with its code and the positive control passes.
- Given the real matrix, when the live job runs on `main`, then it publishes FAIL bound to the SHA and digests, merges stay unblocked, and no row claims an achieved level above `none`.
- Given an edit to any input, when the validator re-runs, then the recorded digests change.

## Implementation Notes

2026-11-15 build, uncommitted on baseline `9d715d480`.

**Choices the spec left open:**
- The validator pins each gate's classification and standard-control reason code, beside the 17 IDs. Reclassifying a high-risk gate to escape the control therefore fails with `CLASSIFICATION_MISMATCH` or `REASON_MISMATCH`.
- It also rejects a pinned gate missing from the PRD table (`PRD_GATE_ABSENT`), an attestation dated after the evaluation (`ATTESTATION_FUTURE`), and any uncommitted, untracked or ignored subject change, because the last authored change is then not a commit (`SUBJECT_HISTORY_UNAVAILABLE`).
- Each high-risk row names an `author` identity, so the independence check can compare GitHub numeric ids. A null binding names its owner in `binding_owner` (`Story <n.m>`, or `OR1` for G-READINESS, which has no owning story).
- An attestation counts only if its role is one of the row's evaluator roles and the approver holds it. Registry roles are held by humans only.
- Review fixes (2026-11-15): each attestation also binds `required_level` (must equal the registry-computed level, `ATTESTATION_LEVEL_MISMATCH`) and `row_sha256`, the SHA-256 of the row's canonical JSON (`sort_keys`, separators `,` and `:`) without `attestations`, `seal` and `achieved_level` (`ATTESTATION_ROW_MISMATCH`). The registry path is pinned to `_bmad-output/implementation-artifacts/owner-role-registry.json`. Exactly one READY record is required, and a non-READY record must consume every high-risk gate whose `guarded_transitions` names it.
- Registry roles: the six PRD glossary owner roles, plus the evaluator roles that the high-risk rows bind (`test-owner`, `architecture-owner`, `epic-owner`, `eventstore-api-owner`, `persistence-owner`, `tenants-owner`, `folders-content-owner`). `github:jpiquot` holds all of them. `bmad:murat` (tool persona) and `github:github-actions[bot]` (CI, the only valid seal identity) hold none.
- Bound rows: G-OQ8 and G-HIGH-RISK. The G-OQ8 subject is the imported design, licence and provenance plus its validator, test and workflow. The G-HIGH-RISK subject is this validator, its tests, workflow and the registry. Both workflows are LF in `.gitattributes` and `.editorconfig`. The matrix is excluded because attestations live inside it, which would make the subject digest circular. The other ten high-risk rows are null bindings: G-RUNTIME-PARITY and G-CONSUMER name deleted or absent tools under the ignored evidence folder.
- The PRD G-HIGH-RISK result cell's first sentence now says the versioned matrix, owner-role registry, offline validator, classifications and assurance-level checks exist (Story 9.2, CI pending), while sealed validation and guarded transitions remain absent. OR10 and OR13 name Story 9.2 as in progress.

**Owner go-ahead step, run 2026-11-15 on the owner's go-ahead** after the first green `fixtures` run on `main`:

```bash
export GH_CONFIG_DIR=~/.config/gh
gh api repos/Hexalith/Hexalith.EventStore/rulesets/14479516 > ruleset.json
jq '{name, target, enforcement, conditions, bypass_actors, rules: [.rules[] | if .type == "required_status_checks" then .parameters.required_status_checks += [{"context": "Phase 4 gate risk matrix fixtures", "integration_id": 15368}] else . end]}' ruleset.json > ruleset-put.json
gh api --method PUT repos/Hexalith/Hexalith.EventStore/rulesets/14479516 --input ruleset-put.json
```

**Local verification (2026-11-15):**
- `python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_gate_risk_matrix.py'`: 147 tests OK. This includes 113 generated negative fixtures over all 45 codes. Separate scratch-copy mutation checks disabled 26 guards one at a time, and each turned the suite red.
- `python3 -B tools/validate-phase-4-gate-risk-matrix.py _bmad-output/implementation-artifacts/phase-4-gate-risk-matrix.json`: exit 1. All 12 high-risk rows FAIL at achieved `none`, with `SEAL_MISSING`, `ATTESTATION_MISSING` and `ACHIEVED_BELOW_REQUIRED`, and ten add `BINDING_UNBOUND`. The 5 standard rows and the READY record (`none`) pass.
- `python3 -B -m unittest discover -s tools/tests -p 'test_oq8_governing_design.py'`: 111 OK. The OQ8 live check exits 0.
- `actionlint`, `bash scripts/check-committed-files.sh` (tracked, and `--staged` on a temporary index holding the new files), `git diff --check` and `git diff --exit-code HEAD -- .github/workflows/ci.yml docs/ci.md`: all pass.
- CI: see **CI run** under Verification.

## Spec Change Log

## Review Triage Log

Review pass 1 (Blind Hunter BH, Edge Case Hunter ECH, Verification Gap VG; 2026-11-15):

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH-01 | medium | patch P1 | Reproduced: changing a passing row's `command`, `pass_condition`, `trigger` or `evidence_identity` after attestation keeps exit 0. The PRD row invalidates the row on any binding change. Attestations bind a canonical row digest. |
| BH-02 | low | patch P4 / reject | The G-OQ8 subject omits its validator, test and pinned-digest workflow, so a weakened validator keeps the attestation; added to the subject. Not adding the PRD to G-HIGH-RISK's subject: the gate table is re-checked live on every run, and frequent PRD edits would restart the window. The matrix is covered by P1 row digests plus the classifications pinned in the validator. |
| BH-03 | medium | reject (intent) | Real: a shape-valid seal record plus an attestation computes `single-maintainer-attested`. The frozen I/O positive control requires exactly this offline PASS, and Never forbids GitHub API calls. Seal retrieval is the deferred transition work, and results say `seal: false` with authority `none`. |
| BH-04 | medium | patch P1 | AC 6 requires the record to bind its required level; `ATTESTATION_KEYS` lacks it, and an attestation can be copied between rows. Same fix as BH-01. |
| BH-05 | low | reject | One `seal` per row cannot hold per-transition seals. Designing them is the deferred seal-workflow work, which may revise the schema; recorded in D1. |
| BH-06 | low | reject | A shallow clone makes the last change later (fails closed: false `ATTESTATION_WINDOW`, never an overstated level), and the live job uses `fetch-depth: 0`. Self-declared Git and attestation times need platform verification, which the intent defers (D1). |
| BH-07 | medium | patch P2 | Reproduced: `registry: other/registry.json` with two humans gives `independent` PASS. Pin the path like the PRD path. |
| BH-08 | medium | patch P3 | Reproduced: a `consumer-removal` record whose only input is `G-OQ8` passes, and a duplicate or absent READY passes. Require exactly one READY, and require non-READY inputs to cover every gate guarding that transition. |
| BH-09 | false / reject | — | One approver is enough: the PRD Assurance Control needs one time-separated owner attestation, or one non-author human for `independent`. Matching the declared `author` to commit identities needs the GitHub API, which the intent excludes (D1). |
| BH-10 | medium | patch P12 | Real-repository tests pin `attestations == []`, `seal is None` and achieved `none`. The first legitimate attestation would then fail the soon-required fixture job and force a test edit, which changes G-HIGH-RISK's subject. Keep the truthful-FAIL exit and structural invariants; drop the frozen row state. |
| BH-11 | low | patch P13 | `.github/dependabot.yml` bumps `github-actions` weekly, and the exact workflow match pins the action SHAs, so every bump PR fails the fixture job. Normalize pinned SHAs in the comparison. |
| BH-12 | low | patch P14 / defer D2 | The G-HIGH-RISK result cell still says the matrix and validator "are absent", and OR10/OR13 say "(backlog)". Refresh only those cells. The Implementation Notes wrongly say the cell is unchanged. OR28 and the `epics.md:397` 9.1-workflow wording predate this change (D2). |
| BH-13 | low | defer D1 | OR13's `high-risk-nfr-story-done` enforcement is transition work. The high-risk NFR set is inconsistent in the PRD (NFR7/12/16 at `:711` vs SM3 at `:546`). |
| BH-14 | false / note | — | `source_spec: none` and the missing `reason` follow the step-01 split template. Lifecycle: the sprint row stays `in-progress` after this spec because ACs 5, 7 and 8 are deferred. |
| BH-15 | low | patch P15 | `REQUIRED_LEVELS` is unused, and the report's closing line omits consumer-removal authority. |
| ECH-01 | low | patch P9 | Probe: a NUL byte in `subject_paths` or `registry` raises an uncaught `ValueError`: exit 1 with no result. Reject control characters in paths. |
| ECH-02 | low | patch P9 | Probe: deeply nested JSON raises `RecursionError`: exit 1 with no result. Map it to exit 2. |
| ECH-03 | medium | patch P2 | Same as BH-07. |
| ECH-04 | medium | patch P3 | Same root cause as BH-08 (empty `downstream` passes). |
| ECH-05 | medium | patch P3 | Same as BH-08. |
| ECH-06 | low | reject | A duplicated PRD gate row is unlikely, and detecting it needs a new code and branch. |
| ECH-07 | low | patch P10 | The separator regex rejects `:---`, causing a false FAIL. The real PRD uses `---`, but the fix is a one-line correction. |
| ECH-08 | low | patch P11 | Raw matrix strings (unknown gate ids, downstream record names, binding owners) reach stderr and the step-summary fence. Render them with `repr`. |
| ECH-09 | low | reject | A dirty-tree source SHA only arises locally, and every input carries its own SHA-256; CI checkouts are clean. |
| ECH-10 | false | — | `report()` reads the malformed record's `scope` and `result`, which are present, and uses `.get` for the rest; no other consumer exists. |
| ECH-11 | medium | patch P12 | Same as BH-10. |
| ECH-12 | medium | patch P16 | Confirmed against `HEAD`: the regenerated epic context dropped the 9.3–9.5 constraints (baseline reconciliation, clause primary slice, MVP totality, content-semantic guards, minting prerequisites). Restore them under the current policy. |
| ECH-13 | medium | reject (intent) | Same as BH-03. |
| ECH-14 | low | patch P9 | Same as ECH-01 and ECH-02. |
| ECH-15 | false | — | Only high-risk rows resolve roles, and every role they name exists. Standard rows and the `bmad:*` Test Architect hold no roles by rule. |
| VG-01 | medium | patch P6 | Pre-verified: removing `--ignored` leaves 126/126 green, and an ignored subject file bypasses the window. Add a fixture. |
| VG-02 | medium | patch P7 | Pre-verified: a committer-only window start leaves 126/126 green. Add an author-later-than-committer fixture. |
| VG-03 | medium | patch P8 | Pre-verified: removing either `holds()` check leaves 126/126 green, and a non-holder could produce `independent`. Add fixtures. |
| VG-04 | low | patch P5 | The hashed workflow is `text=auto` under a global CRLF EditorConfig, so local and CI subject digests can diverge. Add LF rules and cover them in the LF test. |

## Design Notes

**Achieved level.** It is `none` unless a row has both of the following:
- A seal record: workflow path, run id, 40-hex head SHA, 64-hex workflow SHA-256, and a `ci` identity.
- At least one attestation by a registry human who holds a role. It must be bound to the computed subject digest and created at least 24 h after the latest commit touching the subject paths. That commit time is the later of the author and committer dates from `git log -1`.

`independent` additionally needs at least two registry humans, plus an approver whose GitHub numeric id differs from the author's.

**Offline limits.** The offline validator checks only the shape of the seal record. Retrieving the run from the CI platform is the deferred transition work, so an offline result is never a seal.

**Subject digest.** SHA-256 of the sorted `<sha256>  <path>` lines for the subject paths.

**Ruleset step (owner go-ahead only).** GET `repos/Hexalith/Hexalith.EventStore/rulesets/14479516`. Append `{"context":"Phase 4 gate risk matrix fixtures","integration_id":15368}` to its `required_status_checks` rule, then PUT the full ruleset back.

## Verification

**Commands:**
- `python3 -B -m unittest discover -s tools/tests -p 'test_phase_4_gate_risk_matrix.py'` -- expected: all pass.
- `python3 -B tools/validate-phase-4-gate-risk-matrix.py _bmad-output/implementation-artifacts/phase-4-gate-risk-matrix.json` -- expected: exit 1, a truthful FAIL naming each unachieved high-risk row.
- `actionlint .github/workflows/phase-4-gate-risk-matrix.yml` -- expected: no findings.
- `bash scripts/check-committed-files.sh` and `git diff --check` -- expected: pass.
- `git diff --exit-code HEAD -- .github/workflows/ci.yml docs/ci.md` -- expected: no changes.

**CI run (2026-11-15):** source `c5602a46da2c62665c8aea35e3f4400e16fd6df6` on `main`, [run 43736232943](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/43736232943).
- `Phase 4 gate risk matrix fixtures`: success (147 tests).
- `Phase 4 gate risk matrix live evaluation`: failure, the expected truthful FAIL. Artifact `phase-4-gate-risk-matrix-result-43736232943-1` records `result: FAIL`, exit 1, workflow SHA-256 `2df57bf30b651d75a5806cf0e2dda7c55066450973712583bc24a40013b6f6e3`, `seal: false` and authority `none`. The registry names 1 human (required `single-maintainer-attested`). All 12 high-risk rows fail, with only `BINDING_UNBOUND`, `SEAL_MISSING`, `ATTESTATION_MISSING` and `ACHIEVED_BELOW_REQUIRED`. The 5 standard rows and READY pass.
- Ruleset `14479516`: on the owner's go-ahead (2026-11-15), `Phase 4 gate risk matrix fixtures` (integration 15368) was added as the eighth required status check. A before/after comparison shows that addition is the only change; bypass actors and other rules are unchanged. `rules/branches/main` lists it.
