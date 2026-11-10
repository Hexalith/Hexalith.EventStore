---
title: '4.17 OQ8 governing-design authority import'
type: 'feature'
created: '2026-11-10'
status: 'done'
route: 'dispatch'
story_key: '4-17-oq8-governing-design-authority-import'
review_loop_iteration: 0
planning_baseline: 'b2cab541ee3227e1e7a1600cb1d3da83f407dbf0'
baseline_commit: 'b2cab541ee3227e1e7a1600cb1d3da83f407dbf0'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** G-OQ8/OR11 remains blocked because EventStore cannot reproduce its governing design. Existing planning text also requires validators and packets removed by the approved October evidence simplification.

**Approach:** Retain the exact design and its owner's MIT permission notice as immutable documentation. Add a small standalone identity check, mutation tests, and a dedicated CI workflow; reconcile the governing artifacts under the current tests-plus-CI policy.

## Boundaries & Constraints

**Always:** Bind repository `github.com/Hexalith/Hexalith.Folders`, path `docs/exit-criteria/oq8-idempotency-design.md`, commit `a9cfea91c8a987ef7a836c216e633a92321fc3c2`, version `1.0.0`, and SHA-256 `1a55b0302e91233e12db91e6e245f0a22d6bf13fcf6cdf5ee0cbe5759f08dcd8`. Copy Git-object bytes, retaining the pinned MIT notice as the recorded owner permission. Verify entirely inside EventStore without Folders, network access, or submodule initialization. Report authority availability separately from runtime conformance and other gates.

**Never:** Change the design, source repository, runtime, dependencies, historical Story 4.14/4.15 records, `.github/workflows/ci.yml`, or `docs/ci.md`. Recreate deleted packets, manifests, seals, or authorization machinery. Grant release, deployment, consumer migration, Folders closure, or readiness authority. Commit, push, or trigger CI from this workflow without a further user instruction.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Exact authority | Original design, permission notice, matching governed identities | Offline validation passes | Exit 0 |
| Missing or changed bytes | Absent, edited, truncated, or newline-normalized design/license | Authority check fails | Exit nonzero with file named |
| Identity drift | Repository/path/commit/version/digest differs in any governed binding | Reject, including coordinated document/provenance drift | Pinned validator identity remains authoritative |
| Missing permission/binding | Missing provenance permission or artifact identity | Reject | Identify missing field/artifact |
| Unrelated gates | Authority check passes; other gates remain blocked | No readiness or publication claim | Preserve their existing dispositions |

</frozen-after-approval>

## Code Map

- `/home/administrator/projects/hexalith/folders` -- read-only source; exact commit exists locally. Its design is 10,474 bytes with the required digest. Pinned `LICENSE` SHA-256 is `6af66baf053023d456706de321e3cea6a6edbb288ffec10f673186b1befe7ac2`; it explicitly permits copying associated documentation with its notice.
- `_bmad-output/planning-artifacts/{prd.md,architecture.md,epics.md}` -- identity, G-OQ8/OR11 disposition, and Story 4.17 requirements. AD-12 and the epic evidence-policy preamble supersede removed packet/validator tasks.
- `_bmad-output/implementation-artifacts/spec-evidence-storage-simplification.md` -- approved policy; Story 9.1's validator was deleted and its spec explicitly superseded. Proof belongs in this spec plus CI.
- `_bmad-output/implementation-artifacts/spec-4-15-oq8-lifecycle-closure-v4.md` -- completed source-only result and limits remain historical. Its deleted default validator cannot run and will not be restored.
- `tools/tests/`, `.github/workflows/dapr-broker-qualification.yml` -- existing Python unittest and pinned-action conventions. `.gitattributes` governs exact imported bytes.

## Tasks & Acceptance

**Execution:**

- [x] `docs/architecture/oq8/{oq8-idempotency-design.md,LICENSE,README.md}` -- retain exact Git-object design/license bytes; record full source identity, license identity, permission basis, local paths, and limited authority in the README.
- [x] `.gitattributes` -- preserve both imported files byte-for-byte across checkouts.
- [x] `tools/validate-oq8-governing-design.py` -- implement offline fail-closed validation of pinned bytes, permission, and explicit current identity bindings in provenance, planning documents, this story, and the new workflow. Use fixed pins independent of editable provenance; keep the check specific to governing-design availability.
- [x] `tools/tests/test_oq8_governing_design.py` -- exercise the matrix with temporary repository fixtures, real imported bytes, per-field/per-artifact mutations, coordinated drift, missing permission, and CLI exit codes.
- [x] `.github/workflows/oq8-governing-design.yml` -- run tests and default validation on pushes/PRs to the existing CI target branches and manual dispatch; read-only permissions, SHA-pinned checkout, no credentials/submodules, no path filters or promotion. Bind the complete source identity explicitly.
- [x] `_bmad-output/planning-artifacts/{prd.md,architecture.md,epics.md}` -- propagate the full identity and local authority reference; reconcile superseded packet/9.1/default-validator requirements locally. Close only the verified authority-availability gap, conditional on CI; retain all separate controls.
- [x] This spec and `sprint-status.yaml` -- record actual commands/counts/source SHA and CI availability; synchronize lifecycle through the build workflow. Refresh Epic 4 context after planning changes.

**Acceptance Criteria:**

- Given a standalone EventStore checkout, when validation runs offline, then it verifies the original design digest and retained permission without any Folders checkout or deleted evidence tree.
- Given missing authority or any governed identity mismatch, when the tests and workflow execute, then the governing-design check fails with actionable diagnostics.
- Given the October policy, when Story 4.17 is delivered, then this spec plus CI provide proof; historical closure files and sealed inputs remain untouched and no other gate advances implicitly.

## Implementation Notes

<!-- oq8-governing-design:begin -->
```text
source-repository: github.com/Hexalith/Hexalith.Folders
source-path: docs/exit-criteria/oq8-idempotency-design.md
source-commit: a9cfea91c8a987ef7a836c216e633a92321fc3c2
design-version: 1.0.0
design-sha256: 1a55b0302e91233e12db91e6e245f0a22d6bf13fcf6cdf5ee0cbe5759f08dcd8
local-design: docs/architecture/oq8/oq8-idempotency-design.md
```
<!-- oq8-governing-design:end -->


- Imported `oq8-idempotency-design.md` (10,474 bytes) and `LICENSE` (1,065 bytes) directly from pinned Folders Git objects. Their SHA-256 values match the approved design and license pins; `.gitattributes` disables text conversion and exact-path `.editorconfig` exceptions preserve LF for both.
- Added a dependency-free, offline validator with fixed pins independent of provenance. It checks retained bytes, the MIT permission basis, each current artifact/field binding, and the frozen story identity. Missing, malformed, reversed, duplicate or drifting bindings fail with file/field diagnostics; symbolic inputs are refused. Metadata accepts CRLF while import hashes retain raw-byte semantics.
- Added temporary standalone fixtures using the real imported bytes, independently pinned required paths/permission identity, and CLI rejection checks for changed imports and every binding. The dedicated read-only, SHA-pinned workflow has independent fixture/live-evaluation jobs on both existing CI target branches plus manual dispatch; live PASS/FAIL is reported in logs and the GitHub summary with source SHA and workflow digest. The historical `4-8-eventstore-oq8-platform-evidence.yaml` remains tracked and unchanged with superseded validation requirements; its referenced evidence tree and default validator remain removed. No Folders checkout, submodule or removed machinery is required.
- Reconciled PRD G-OQ8/OR11, AD-25 and Story 4.17 under AD-12; refreshed Epic 4 context. The architecture memlog records that no AD-26 requirement changes and its ratification subject stays `d49f8a061eb6a0faf1d31577d571ceae497a38d67404c4901151d1dc4ea90e16`.
- Local authority availability is verified; closing that availability gap remains conditional on dedicated CI confirmation. Runtime conformance and all separate controls retain their dispositions. Historical Story 4.14/4.15 artifacts, `.github/workflows/ci.yml` and `docs/ci.md` are byte-identical to the planning baseline. No runtime, dependency, commit, push or CI-trigger mutation occurred. Lifecycle remains in progress until the parent build workflow completes review.

## Spec Change Log

- 2026-11-10: Implemented the approved immutable-copy path, offline identity/permission validation and mutation tests, dedicated CI, and local planning-policy reconciliation. Frozen intent is unchanged.
- 2026-11-10: Completed three independent review passes and all retained corrections: marker ordering, metadata newline tolerance, exact-import editor settings, independent artifact/permission pins, CLI rejection, strict executable workflow controls and independent source-bound CI outcomes. Preserved raw-byte import hashes, frozen intent, historical artifacts and AD-26 subject.

## Review Triage Log

Review pass 1 (Blind Hunter, Edge Case Hunter, Verification Gap; 2026-11-10):

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH-01 | low | patch | Reproduced reversed end/begin markers with a valid fence at EOF returning no errors. Require ordered markers and test rejection. |
| BH-02 | medium | patch | Reproduced CRLF metadata failing the text-fence parser although the imported bytes are intact. Normalize metadata newlines only; hashes still use raw imported bytes. |
| BH-03 | medium | patch | Global EditorConfig CRLF conflicts with the immutable LF imports, and Git's `-text` retains editor rewrites. Add exact-path LF exceptions and regression coverage. |
| BH-04 | low | reject | A duplicate workflow `env` passes the text-binding check, but actionlint reproducibly rejects the duplicate key and GitHub cannot execute that malformed workflow. Adding a general YAML interpreter/dependency is disproportionate; this validator checks identity bindings and workflow syntax remains enforced by GitHub/actionlint. |
| BH-05 | medium | patch | Existing substring assertions accept an echo of the check or `if: false`, allowing a green workflow without evaluation. Assert actual run commands and reject skipping conditions in this scoped workflow. |
| BH-06 | medium | patch | Selected substrings do not reject write permissions or an explicit checkout token. Assert the complete permission and checkout-input contract. |
| BH-07 | medium | patch | Required artifact tests derive their list from implementation scope; removing a governed document also removes its tests. Add independent required paths and rejection assertions. Same root cause as VG-02. |
| BH-08 | medium | patch | Permission metadata lacks independent pins: coordinated license-commit drift can pass tests. Assert the exact permission identity and shared source commit. |
| BH-09 | low | patch | The historical `4-8-eventstore-oq8-platform-evidence.yaml` is still tracked. Describe its validation requirements as superseded while distinguishing the deleted evidence tree and validator. |
| BH-10 | medium | patch | AD-12 retains distinct fixture and live-evaluation outcomes. Split the new workflow into independent jobs so fixture failure cannot suppress the availability result; publish a source/workflow-bound result without creating a seal. |
| ECH-01 | low | patch | Same reproduced reversed-marker outcome as BH-01; use the same ordered-marker correction. |
| VG-01 | medium | patch | Pre-verified: filtering digest/binding errors out of CLI results leaves all 106 tests green. Add changed-byte and binding CLI rejection with diagnostic and success-message assertions. |
| VG-02 | medium | patch | Pre-verified: deleting architecture from DOCUMENTS leaves 94 tests passing and a missing architecture accepted. Independently require every governed artifact. Same root cause as BH-07. |

All patch findings are resolved and covered by the final 111-test run; no findings were deferred. Parent review inspected the complete tracked and untracked diff and verified every task and acceptance-matrix row. The bounded implementation is complete; sprint status is `review` for handoff, while G-OQ8/OR11 CI confirmation remains pending. The approved prohibition on commit/push/CI-trigger mutations takes precedence over the build workflow's generic local-commit step, so changes remain uncommitted.

## Verification

**Source:** EventStore `HEAD` and planning baseline `b2cab541ee3227e1e7a1600cb1d3da83f407dbf0`; implementation is an uncommitted working-tree change. Imported source is Folders commit `a9cfea91c8a987ef7a836c216e633a92321fc3c2`. The design and license SHA-256 values are recorded in the immutable binding/provenance above.

**Commands and measured results (2026-11-10):**

- `python3 -m unittest discover -s tools/tests -p 'test_oq8_governing_design.py'` — PASS, 111 tests, zero failures/errors/skips after review corrections. Cases include every independently enumerated field/artifact mutation and CLI rejection, complete permission pins, coordinated drift, changed/truncated/newline-normalized imports, reversed markers, CRLF metadata, exact editor exceptions, executable restricted workflow contracts, and source/workflow-bound live PASS/FAIL reporting.
- `python3 tools/validate-oq8-governing-design.py` — PASS, exit 0; exact authority availability verified offline, with runtime and other gates explicitly separate.
- `actionlint .github/workflows/oq8-governing-design.yml` — PASS, exit 0, no findings.
- `git diff --check` — PASS, exit 0.
- `bash scripts/check-committed-files.sh` — PASS, 10,421 tracked files checked. New files remain untracked because staging/commit was not authorized; their paths and sizes were separately inspected against the same rules.
- `git check-attr text eol -- docs/architecture/oq8/oq8-idempotency-design.md docs/architecture/oq8/LICENSE` — both report `text: unset`; imported Git-object bytes retain their approved digests.
- `git diff --exit-code b2cab541ee3227e1e7a1600cb1d3da83f407dbf0 -- .github/workflows/ci.yml docs/ci.md ':(glob)_bmad-output/implementation-artifacts/*4-1[45]*'` — PASS, zero protected historical-file changes. Direct Git-object byte comparisons also passed for every matched Story 4.14/4.15 artifact.
- A direct baseline/current computation of the defined AD-26 subject — PASS, unchanged `d49f8a061eb6a0faf1d31577d571ceae497a38d67404c4901151d1dc4ea90e16`.

**CI availability:** The dedicated workflow is implemented and locally linted. No CI run exists for these uncommitted changes; run link is unavailable until a separately authorized push and workflow execution. CI confirmation is pending, so no unconditional G-OQ8/OR11 closure, runtime conformance, readiness, publication or external-consumer authority is claimed. No implementation blocker remains.
