---
title: 'Story 4.15 Story-File Diff Review Patches'
type: 'bugfix'
created: '2026-09-20'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
story_key: '4-15-oq8-platform-closure-and-handoff'
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
- [ ] `tools/validate-oq8-platform-evidence.py` -- add fail-safe historical-child cleanup and bounded dependency-input snapshots without changing established semantics.
- [ ] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs` -- prove all three surviving validator/test findings with focused behavioral cases.
- [ ] `_bmad-output/implementation-artifacts/deferred-work.md` -- remove only the duplicate un-IDed blocks identified by the final story-file passes.
- [ ] Story 4.15 successor evidence -- follow the approved lifecycle option and leave no false current-source claim.

**Acceptance Criteria:**
- Given the unchanged rejected/deferred list, when the patch is inspected, then no excluded finding is implemented and no canonical deferred-work item is removed.
- Given the approved evidence-lifecycle option, when verification completes, then every validator mode and required Contracts lane that remains claimed is truthful for the final bytes.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

The code changes are individually small, but both target files are v3 gate inputs and the new test changes the sealed full Contracts count. Evidence lifecycle is therefore part of correctness rather than post-implementation bookkeeping.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.Oq8PlatformClosureTests -noColor` -- expected: all focused closure cases pass with no skip or failure.
- `python3 tools/validate-oq8-platform-evidence.py` plus `--lifecycle-mode final`, `--historical-v1-only`, and `--historical-v2-only` -- expected: every applicable mode exits 0 against the final truthful evidence state.
- `git diff --check` -- expected: no whitespace errors.
