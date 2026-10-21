---
title: 'Story 9.1 live authorization bootstrap for ES-DIAG'
type: 'test'
created: '2026-10-20'
status: 'in-progress'
route: 'dispatch'
baseline_commit: '54261aeccb1d8d8c5c99beb2437e2e8a5ad69eaa'
context:
  - '/home/administrator/projects/hexalith/eventstore/AGENTS.md'
  - '/home/administrator/projects/hexalith/eventstore/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-3-21-local-broker-failure-diagnosis.md'
---

<frozen-after-approval reason="bootstrap preparation explicitly authorized in the 2026-10-20 user request">

## Intent

**Problem:** The approved ES-DIAG slice has recorded implementation consent but lacks the baseline registry and authenticated receipts required by Story 9.1.

**Approach:** Prepare a narrowly scoped local bootstrap baseline and exact approval payloads in an isolated worktree, then run the unchanged authorization preflight. Resume ES-DIAG only on PASS. Preserve exact failures and remaining actions otherwise.

## Boundaries & Constraints

**Always:** Preserve the original workspace, approved frozen intent, historical packets and validator/schema. Verify GitHub numeric human identity and issue locations. Bind the current approved planning inputs and a truthful G-BASELINE failure. Validate every exact commit message with pinned commitlint before narrowly scoped local commits. Keep receipts, command output and caches outside the authorization subject while preparing it. GitHub receipts must be posted by the human owner; discovered admin identity is not an attestation.

**Never:** Push, post attestations, fabricate owner approval, reuse Story 3.15 authority, refresh dependencies/gitlinks, change sealed workflows, change Platform/Builds, choose a broker, touch production, or authorize unrelated files. Do not run Docker experiments before corrective preflight passes. A bootstrap result cannot close G-BASELINE or ES-DIAG.

## I/O & Edge-Case Matrix

| Input | Required behavior |
| --- | --- |
| Missing issue locations or owner receipts | Retain truthful FAIL and exact remaining action; no guessed numbers or receipt |
| Isolated Python and regular evidence file | Execute a digest-bound standalone archive adapter, without sibling imports |
| Complete rejected technical capture | Verify all 24 executed observations and preservation; report technical FAIL honestly |
| Missing vectors, blocked execution, polluted inventory or changed unrelated containers | Reject evidence |
| Unrelated changed path or immutable input drift | Unchanged Story 9.1 validator rejects; never broaden scope |

</frozen-after-approval>

## Code Map

- `tools/corrective-work-authorization.schema.json` and `tools/validate-corrective-work-authorization.py`: authoritative, unchanged. Exact paths only, at most 128. Evidence is a regular file, executed with `-I -S -B`.
- `tools/dapr_broker_qualification.py`: reuse the baseline verifier's exact bytes, SHA-256 `7357c189c12ce284196b84ceba47a435f21003d898f65ef6a9b272030fd80afa`; never execute archived code or mutable current verifier code.
- `evidence/story-3-21-platform-host-reconciliation-2026-10-20/approval/`: retained exact approved subjects, frozen-block digest and `finalization-validation.json` for the six amended planning files.
- `spec-3-21-local-broker-failure-diagnosis.md`: full future ES-DIAG scope and acceptance, unchanged in this bootstrap.

## Tasks & Acceptance

**Execution:**

- [x] `tools/validate-es-diag-capture.py` — accept one `packet.tar.gz` file and required full `--baseline` commit; load `tools/dapr_broker_qualification.py` using bounded read-only `git cat-file` and check the digest above. Extract only canonical relative regular files to a temporary directory, rejecting duplicates, links, devices, traversal, backslashes, controls and excessive member/byte counts. Require exactly `packet.json` plus its artifact inventory. Invoke unchanged baseline `validate_packet(..., allow_failures=True)`. Read the baseline candidate lock through bounded Git object reads and require the archived runtime, image and OpenBao identity sections to match; changed diagnostic input snapshots are permitted. Additionally require successful preflight, both control-plane identities, both loaded sidecar roles for each broker, exactly 24 nonblocked executed observations with independent persisted HTTP application readbacks and broker diagnostics. Emit deterministic JSON; technical failure remains visible and production eligibility stays FAIL. This adapter verifies capture evidence only; the complete ES-DIAG acceptance still requires implementation, tests, review and owner completion.
- [x] `tools/tests/test_es_diag_capture.py` — meaningful isolated-CLI controls for valid rejected capture, missing/blocked vectors, preservation/inventory corruption, unsafe archives and verifier digest mismatch. Use clearly synthetic disposable fixtures, never edit historical evidence.
- [x] `evidence/story-9-1-es-diag-bootstrap-2026-10-20/approved-inputs.json` — bind actual source revision and exact current planning/approval hashes; independently check retained approval copies, six amendment digests and frozen intent. Distinguish later status annotations from approved frozen intent.
- [x] `evidence/story-9-1-es-diag-bootstrap-2026-10-20/failed-gate.json` — closed schema object for G-BASELINE FAIL, grounded in the current PRD §11.4. Add provenance to `approved-inputs.json`; do not claim a missing general baseline validator ran.
- [ ] Parent coordinator: verify owner and issue locations, create the single-owner live registry, commit only bootstrap inputs, derive the exact ES-DIAG core/scope and canonical receipt bodies, and retain real preflight output externally. Registry approval must precede issuance, and authorization approval must follow issuance. If locations are still missing, preserve non-live preparation and report that specific blocker.

**Acceptance Criteria:**

- Given the authorized bootstrap request, when local preparation completes, then original files and approved intent remain unchanged and every Git mutation has retained commitlint evidence.
- Given real issue locations and receipts, when unchanged preflight runs, then its actual verdict controls resumption; no conversation approval substitutes for GitHub authority.

## Implementation Notes

The parent coordinator owns registry, authorization, Git operations and external preparation records. The implementation dispatch owns the adapter, its tests and the two baseline evidence JSON files. Work only in this isolated worktree. Read-only original-workspace guidance is allowed; do not discover skills in references.

## Verification

Run the focused adapter tests in normal and optimized Python and the unchanged corrective-work fixture suite. Retain output outside the repository. Run `git diff --check`, compare approved inputs and original workspace preservation, and run the exact authorization preflight. PASS is not expected before human receipts exist.


## Review Triage Log

All three workflow review layers ran against the local preparation diff. Findings are recorded individually; none changes the approved ES-DIAG intent or grants authority.

| Finding | Verdict and route | Evidence / required correction |
| --- | --- | --- |
| Blind 1: missing source inventory | medium / patch | Resealed removal of baseline input keys passes the inherited verifier; require baseline-key coverage. |
| Blind 2: cross-broker evidence alias | medium / patch | Reused message identity and command indices pass despite distinct file names; require independent identities/receipts. |
| Blind 3: publication HTTP method/exit | medium / patch | Publication GET/exit 17 can pass old predicates; require POST and producer status/exit consistency. |
| Blind 4: loaded-metadata HTTP method/exit | medium / patch | Metadata POST/exit 31 can masquerade as successful GET; validate the actual HTTP receipt. |
| Blind 5: control-plane identity drift | medium / patch | Self-declared zero hashes satisfy old shape checks; bind exact immutable baseline identities. Actual worker/process attribution remains mandatory ES-DIAG work, outside this adapter's capture-only claim. |
| Blind 6: missing preservation enumeration | medium / patch | Empty snapshots with no Docker inventory receipts pass old checks; require command-bound complete enumeration, including emptiness. |
| Blind 7: duplicate delegated JSON fields | medium / patch | Duplicate cleanup members can hide a contradictory first value; strict-load inventoried JSON before delegation. |
| Blind 8: uncaught gzip error | low / patch | Invalid appended DEFLATE fails with a traceback; catch the demonstrated parser error and emit deterministic JSON FAIL. |
| Blind 9: mutable verifier test source | medium / patch | Permitted future verifier edits would invalidate the fixture's trusted baseline; use retained pinned bytes. |
| Blind 10: JSON checkout conversion | medium / patch | Authority hashes bind raw bytes but new JSON paths have text-auto attributes; add narrow byte-preservation rules. |
| Edge 1: invalid tar terminator | medium / patch | Tarfile can swallow a nonzero invalid final header; validate the complete bounded tar termination. |
| Edge 2: malformed gzip output claim | low / patch | Same demonstrated gzip parser issue as Blind 8; retain its own finding row and share the fix. |
| Verification 1: successful-readback guard untested | medium / patch | Removing the GET/exit-zero check leaves existing tests green; add method and nonzero-exit negative controls. |


Review corrections verified before baseline freeze: 32 focused adapter tests pass in normal and optimized Python, including every demonstrated review bypass, empty-inventory and additive-snapshot positives. The unchanged corrective-authorization suite passes 18 tests in each Python mode. Exact pinned commitlint 21.1.0 evidence and all operation receipts are retained outside this source subject in the sibling records directory. The original workspace HEAD, index, status and historical unlisted bytecode remain unchanged. The remaining coordinator operation results and human-receipt status are deliberately external to this immutable checkpoint; no authority or ES-DIAG completion is asserted here.
