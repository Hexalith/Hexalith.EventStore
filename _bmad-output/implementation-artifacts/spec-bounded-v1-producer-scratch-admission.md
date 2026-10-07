---
title: 'Story 6.6 prerequisite: Bounded V1 producer scratch admission'
type: 'bugfix'
created: '2026-10-07'
status: 'done'
route: 'oneshot'
review_loop_iteration: 1
baseline_commit: '27ac3c628db75ac6a410c88689546edd4de60ae4'
context: []
---

<frozen-after-approval reason="authorized Story 6.6 verification prerequisite">

## Intent

**Problem:** The existing bounded V1 producer tests do not independently protect its 128 MiB private-copy scratch admission. Their large-result rejection also exceeds the encoded-response limit, so removing the scratch guard can pass those controls. The open verification-gap entry in `deferred-work.md` identifies 42 versus 43 one-MiB serialized events as the discriminating boundary.

**Approach:** Add a passing 42-event control and a refused 43-event control in one dedicated test class, reusing existing payload fixtures. Include a tiny regular-event sentinel so refusal proves zero serializer callbacks; independently admit the maximum-size wire image in both cases to separate encoded capacity from scratch. Verify original serialized bytes and detached successful output. Demonstrate that removing only the scratch guard makes the refused control fail in an isolated temporary test project. Record commands, inputs, results, review and the exact remaining serializer/catalog dependencies. This closes a local verification prerequisite, leaving Story 6.6, M1–M8 and activation requirements open.

</frozen-after-approval>

## Implementation Notes

The user authorized continuing Story 6.6 with ongoing Story 5.5 edits preserved. The root-owned required baseline, tracked build/test configuration, existing producer/admission code and tests, current 6.6 amendments and latest evidence were inspected. No intent gaps, irreversible action or public API change is required. All durable changes stay in EventStore; Folders Story 4.19 remains queued. Preserve existing files and hidden Git flags; perform no staging, commit, push, dependency or submodule mutation.

Use `tests/Hexalith.EventStore.DomainService.Tests/BoundedV1DomainResultProducerScratchTests.cs`, existing `BoundedProducerTestEvent`/`BoundedProducerSerializedTestEvent`, and the unchanged producer, declarations, payload stream and wire admission. Mutation compilation must use isolated copies and the actual pinned local test dependencies; never edit the production guard. Run the focused class and affected DomainService project in Debug/source-reference mode after restore. Keep raw local logs under `evidence/story-6-6/bounded-v1-scratch-admission-2026-10-07/`, add a verification Markdown record, and append a scoped note to the parent 6.6 execution record. Resolve only the exact scratch-gap ledger entry once independently verified. The parent sprint row remains in progress; this subordinate spec has no sprint key. Skip the one-shot commit instruction because the repository/user scope does not authorize Git mutations.

The isolated EventStore Aspire AppHost started on 2026-10-07. CLI resource inspection showed running/healthy resources and one finished Tenants resource; these are uncaptured session tool observations, not persisted verification or event-evolution live qualification. The owned EventStore AppHost was stopped before test builds; the existing ChatBot AppHost was untouched.

Implemented one new test class with the real maximum-size wire-admission control, callback sentinel, passing 42/refused 43 cases, original source digest and detached output checks. Added an isolated source-copy mutation verifier using actual pinned build/package inputs, exact theory/failure checks, bounded subprocesses and fresh run directories. Its explicit verification remains active under `python3 -O`. All unchanged local source copies pass; removing only the scratch guard makes the owning 43-event assertion fail, while 42 remains passing.

Debug/source build completed with zero warnings/errors. The new actual-assembly class passed both cases and the affected full DomainService assembly passed 494 tests without skips. [Evidence and commands](evidence/story-6-6/verification-2026-10-07-bounded-v1-scratch-admission.md) retain initial and final verification output. Added one parent change-log note and resolved only the original scratch-gap ledger entry, preserving its source, summary and evidence text. No parent task, sprint status or activation fence changed. The real per-domain declarations remain unavailable in the inspected inputs, so runtime fallback replacement cannot be completed coherently in this run.

Review corrections give each source distinct bytes and compare output digests by index, capture unexpected successful refusal output for cleanup, and cover wire assertions with `finally`. The final isolated runner imports root targets/editorconfig, snapshots consumed configuration, records Python `-O`, persists commands before launch and compares dependency/managed image hashes. The reviewed actual assembly's managed images and runtime descriptors were pinned after build and unchanged through both test runs. Final focused controls passed 2/2, full DomainService passed 494/494, and the optimized isolated mutation retained the 42-event pass and killed the 43-event assertion. Initial captures remain historical; final hashes and captures are separately named.

The review follow-up independently checked the corrections and final hashes/results, confirmed BH-2 through BH-10 addressed and BH-1 accurately deferred, and found no remaining material defect. The final audit preserved the parent's frozen intent, canonical baseline and eight open tasks, checked authored whitespace/evidence links, and confirmed the accepted release YAML remains absent. Only this subordinate verification prerequisite is done; no Git mutation was performed.

## Review Triage Log

Blind Hunter reviewed the owned test/evidence change without implementation context. Its finding floor was 10 (`min(floor(sqrt(706.479) + 1), 10)`). Other review layers were skipped for this focused one-shot prerequisite. Each finding was checked against the local test, verifier or evidence it cited; no runtime/API defect was introduced.

- BH-1 — **low / defer:** The coarse 42/43 controls do not distinguish exact-capacity `>` from `>=` or small accounting omissions; neither case is exactly at the cap. This is an additional verification idea outside the guard-removal prerequisite. Added an open M8 refinement to `deferred-work.md`; no exhaustive accounting claim is made.
- BH-2 — **medium / patch, resolved:** Identical sources could conceal duplication/reordering in successful output. Every event now has a distinct byte pattern and per-index source/output digest comparison; both actual-assembly controls and the isolated controls pass.
- BH-3 — **low / patch, resolved:** An unexpected successful refusal result was discarded and wire assertions sat outside cleanup. The delegate retains any returned result, wire assertions run inside `try`, and `finally` clears every retained output/source payload, including the killed mutant's successful output.
- BH-4 — **medium / patch, resolved:** Linked Contracts/dependency inputs could change between isolated builds. The final result pins every consumed managed image/runtime descriptor, requires identical dependency hashes between builds and unchanged images during each execution, and verifies source/configuration bytes afterward. The final mutation run passed those checks.
- BH-5 — **low / patch, resolved:** The isolated project omitted root targets/editorconfig while claiming repository build inputs. It now imports root targets and copies the root editorconfig; the final warnings-as-errors control and mutant builds pass.
- BH-6 — **low / patch, resolved:** The prior capture did not prove the optimized Python invocation. The final result records `sys.orig_argv`, executable and `sys.flags.optimize = 1`, plus the exact verifier digest; the `python3 -O` run executes both builds and tests.
- BH-7 — **low / patch, resolved:** Subprocess timeout/launch failure could leave no command metadata. Command records are written before execution and updated in `finally`, including failure type/details. Completed final commands retain explicit exit codes and logs.
- BH-8 — **medium / patch, resolved:** Actual-assembly verification used mutable binary paths without image hashes. The review-patched build now has an image manifest; focused/full command records retain matching before/after managed-image and runtime-descriptor hashes. Focused 2/2 and full 494/494 passed against that built image.
- BH-9 — **low / patch, resolved:** Aspire observations had no persisted command/resource capture. Both notes explicitly label them uncaptured session observations; they grant no live/fleet qualification.
- BH-10 — **low / patch, resolved:** The evidence linked an unfinished review disposition. This log records all ten findings and their checked outcomes; the subordinate status is finalized only after review/check completion. Parent 6.6 remains in progress.
