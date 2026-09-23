---
title: 'Story 4.15 Story-File Diff Review Patches'
type: 'bugfix'
created: '2026-09-20'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
story_key: '4-15-oq8-platform-closure-and-handoff'
baseline_commit: '0092d0f9824e500dc936d26500c86a4851047dea'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The uncommitted Story 4.15 story-file diffs retain three unchecked validator/test patches and one duplicate-ledger cleanup after their later rejected and deferred findings are excluded. The current Git historical-blob reader can leave a child alive on a non-standard exception, PyYAML bootstrap inputs bypass the existing bounded repository snapshot reader, and the focused CTRF success shape lacks a direct executable proof.

**Approach:** Apply only those surviving story-diff patches, reuse the existing bounded-read and subprocess-cleanup patterns, add narrow mutation-sensitive Contracts coverage, and remove only the two un-IDed deferred-work blocks that duplicate DW-527 through DW-529.

## Boundaries & Constraints

**Always:** Preserve the current digest, limit, diagnostic, focused-command, xUnit 4 label/tag, and support-safe behavior. Keep canonical DW-527, DW-528, and DW-529 plus the later ID-based reconfirmation. Treat `tools/validate-oq8-platform-evidence.py` and `Oq8PlatformClosureTests.cs` as v3 hash-bound inputs; any completed source patch must end in a truthful evidence state.

**Never:** Do not implement findings that the story diffs reject or defer, including PostgreSQL index/local-identity equality, v1 install-command rewriting, lifecycle changes, scanner/redactor expansion, source-binding redesign, or TOCTOU refactoring. Do not rewrite sealed receipts or claim approval without an independently reviewed successor.

**Decision:** Implement all three surviving validator/test patches and the ledger cleanup, then create and independently review a fresh successor/reseal in this run. Update the sealed Contracts count from 2068 to 2069, propagate new evidence identities and receipts only after approval, and complete the full final verification sequence.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Historical Git interruption | `sha256_git_file` receives `KeyboardInterrupt` after starting its child | Child is killed and waited; interruption propagates | No orphaned Git process or replacement evidence error |
| Bootstrap source is unsafe | Requirements or either CI workflow is a symlink or exceeds 524,288 bytes | Repository-bound snapshot rejects it before semantic comparison | Stable bounded/symlink diagnostic; no traceback |
| Valid focused xUnit 4 CTRF | One passing record with exact labels and tags | Portable result uses `-result-ctrf`, array traits, status, and finite duration | Output is written only after validation |
| Duplicate ledger notes | Two un-IDed blocks restate DW-527/528/529 | Duplicate blocks are removed; canonical and ID-based entries remain | No unrelated ledger rewrite |

</frozen-after-approval>

## Code Map

- `tools/validate-oq8-platform-evidence.py:891-1077` -- copy `run_subprocess_bounded`'s `BaseException` child cleanup into `sha256_git_file`; do not change digest or limit behavior.
- `tools/validate-oq8-platform-evidence.py:1136-1196,3686-3717` -- reuse `read_bounded_text_snapshot` with `MAX_V2_BOUND_SOURCE_BYTES` for the requirement and workflow inputs; retain their semantic drift checks.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs:824-852,1629-1749,2840-3040` -- add isolated unsafe-bootstrap, valid focused-CTRF, and historical-child cleanup proofs near the existing lanes.
- `_bmad-output/implementation-artifacts/deferred-work.md:4439-4525` -- retain canonical DW-527/528/529 and the ID-based third-pass reconfirmation; remove only the first two un-IDed duplicate blocks.
- `_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v3/` and `_bmad-output/implementation-artifacts/4-15-oq8-platform-closure-successor.json` -- current sealed identities affected by either bound source change; touch only if the evidence-lifecycle decision authorizes a new successor.

## Tasks & Acceptance

**Execution:**
- [x] `tools/validate-oq8-platform-evidence.py` -- add fail-safe historical-child cleanup and bounded dependency-input snapshots without changing established semantics.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs` -- prove all three surviving validator/test findings with focused behavioral cases.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- remove only the duplicate un-IDed blocks identified by the final story-file passes.
- [x] Story 4.15 successor evidence -- follow the approved lifecycle option and leave no false current-source claim.

**Acceptance Criteria:**
- Given the unchanged rejected/deferred list, when the patch is inspected, then no excluded finding is implemented and no canonical deferred-work item is removed.
- Given the approved evidence-lifecycle option, when verification completes, then every validator mode and required Contracts lane that remains claimed is truthful for the final bytes.

## Implementation Notes

- Protected every post-`Popen` historical Git initialization/read path with `BaseException` kill-and-wait cleanup, including selector construction and deadline initialization.
- Reused `read_bounded_text_snapshot` at the existing 524,288-byte bound for the pinned requirement and both CI bootstrap workflows.
- Added discovered Contracts cases for the valid xUnit 4 CTRF conversion and the standalone cross-platform historical-child interruption proof, and extended the existing bootstrap case for every unsafe input. Final sealed counts are 467 focused closure cases and 2070 full Contracts cases.
- Removed only the first and second un-IDed deferred-work restatements; canonical DW-527 through DW-529 and the later identifier-based reconfirmation remain.
- Independently approved successor subject `583269ac56f7ca75371b6c70a507a820bf21a4ff1fada790ac91feea34ca8b7e`; final manifest is `f20a01ba874eaca8e3d99eef065534a8211eef16db601bf9dc1723ef0cc90493`.

## Spec Change Log

- 2026-09-20: Implemented the surviving story-file review patches, obtained independent architecture/security/test approval, resealed the v3 successor, and completed final verification.

## Review Triage Log

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| ECH-01 | medium | defer | `read_bounded_regular_snapshot` checks path components and metadata before reopening the pathname, so a concurrent replacement can redirect the later `open`; this is the pre-existing TOCTOU class that the frozen intent explicitly excludes from refactoring. |
| ECH-02 | low | rejected | A second `BaseException` from `poll`, `kill`, or `wait` could mask the triggering exception and interrupt cleanup, but this requires another exceptional event inside the narrow cleanup window; making cleanup recursively interruption-resistant adds guards and policy beyond the requested existing cleanup pattern. |
| ECH-03 | low | rejected | The post-kill `wait()` has no timeout and could exceed the Git deadline if a killed child never becomes waitable, but `SIGKILL` reaping normally completes immediately and a bounded fallback would add a new lifecycle branch for an extraordinary host failure. |
| ECH-04 | low | rejected | A close failure could mask the original exception and skip later closes, but this finalization sequence predates the patch and ordinary selector/pipe close operations do not raise; independently guarding every close adds complexity for a negligible path. |
| ECH-05 | low | patch | `RunHistoricalGitInterruptProbe` is called only after the POSIX-only blob-limit assertions, so Windows skips the new cleanup proof even though the probe itself is portable; moving it to its own fact is a direct correction. |
| ECH-06 | low | rejected | This duplicates ECH-02: nested cleanup failures can interrupt kill-and-wait, but the demonstrated story defect is the first non-standard exception after launch, not repeated exceptions during cleanup. |
| BH-01 | low | rejected | This duplicates ECH-02 and is real only if cleanup itself suffers another exceptional failure; preserving the original exception under arbitrarily repeated interruption needs additional policy and branching beyond the approved pattern. |
| BH-02 | low | patch | The cleanup probe is embedded in a POSIX-only fact and can be bypassed by its skip or an earlier assertion; a standalone fact makes the proof isolated and cross-platform as the spec intended. |
| BH-03 | false | rejected | The complete post-launch body is inside the protected `try`; selector-creation and clock-initialization mutations exercise both sides of the moved boundary, so every later initialization/read operation reaches the same `BaseException` handler without needing one test per statement. |
| BH-04 | low | rejected | A platform without symbolic-link support can skip the combined bootstrap fact after earlier checks, but required CI and the completed verification run on Linux executed all three inputs and both shapes; splitting the matrix adds structure for a non-gating environment. |
| BH-05 | false | rejected | “All unsafe bootstrap inputs” refers to the three frozen matrix inputs, each tested oversized and symlinked; missing files, directories, invalid UTF-8, and ancestor swaps are outside the frozen matrix, while the shared reader already fails closed for those static shapes. |
| BH-06 | medium | defer | This is the same verified pre-open pathname-replacement race as ECH-01; the reader and race predate this change, and the frozen intent expressly excludes TOCTOU refactoring. |
| BH-07 | false | rejected | The frozen matrix requires one passing record with exact xUnit 4 labels, tags, status, and finite duration; the constructed record executes that sanitizer contract directly, while requiring runner-produced fixture generation would expand the requested proof. |
| BH-08 | medium | patch | `V3_REVIEW_SCOPES` replaces prior live redaction, limitation, and rejected-receipt obligations with only the new patch topics; because the successor reauthorizes changed validator bytes, the new topics must extend the existing scope rather than narrow it. |
| BH-09 | false | rejected | The focused cases are included in the recorded 467-case class run, and historical/actionlint commands are recorded in `pre-review-execution.json`; adding final self-validation commands to the receipt would create the lifecycle/source-binding redesign that the frozen intent excludes. |
| BH-10 | low | rejected | The story command list omits several exact commands later reported under Observed, but their executable records live in the pre-review and test-receipt artifacts; the proposed fix edits this build's spec and is therefore rejected by the review workflow. |
| BH-11 | false | rejected | The deleted blocks were un-IDed restatements: scheme-prefixed `file://corp/...` evidence remains explicitly recorded in the DW-524 note and the DW-526 settlement, while canonical DW-528 and later reconfirmations remain intact. |
| BH-12 | false | rejected | DW-525, DW-526, and DW-533 are dated historical ledger entries whose text explicitly describes the round-4 state; chronological evidence is not made false when a later successor changes counts and hashes. |
| BH-13 | false | rejected | `limitations.json` is content-bound to a specific review subject, so “this successor subject” is unambiguous within that sealed packet; predecessor and withdrawn-approval identities are also bound through the subject and handoff lineage. |
| BH-14 | false | rejected | “Independently approved” reports the review process that occurred, while DW-532 separately and explicitly says those reviews occurred but the receipt format cannot authenticate reviewer identity; the statements do not conflict. |

## Design Notes

The code changes are individually small, but both target files are v3 gate inputs and the new test changes the sealed full Contracts count. Evidence lifecycle is therefore part of correctness rather than post-implementation bookkeeping.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.Oq8PlatformClosureTests -noColor` -- expected: all focused closure cases pass with no skip or failure.
- `python3 tools/validate-oq8-platform-evidence.py` plus `--lifecycle-mode final`, `--historical-v1-only`, and `--historical-v2-only` -- expected: every applicable mode exits 0 against the final truthful evidence state.
- `git diff --check` -- expected: no whitespace errors.

**Observed:**
- Contracts Release build: succeeded with 0 warnings and 0 errors.
- Focused closure class: 467 passed, 0 failed, 0 skipped.
- Full Contracts assembly: 2070 passed, 0 failed, 0 skipped.
- Default, final lifecycle, historical-v1-only, and historical-v2-only validators: all exited 0.
- `actionlint`, `sha256sum -c closure-sha256.txt`, ledger-preservation checks, and `git diff --check`: all passed.
