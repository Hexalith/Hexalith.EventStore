# Story 6.6 session checkpoint — 2026-10-08

**Stopped at the owner's explicit request:** “save and stop. I will continue in a fresh session”. Resume only on a new owner instruction. Changes remain in the working tree. No staging, commit, push, branch or dependency update was performed by this continuation.

## Resume state

- Repository: `/home/administrator/projects/hexalith/eventstore`, branch `main`.
- Observed HEAD: `9542d3c9f48bf9ce1c57f2ef68904703eaba56cc`; recheck when resuming.
- Workflow baseline remains `1329b35e52852952ecb2c94aabf100674e9691e3`.
- Parent [implementation spec](spec-6-6-event-versioning-and-upcasting-implementation.md) remains `in-progress`, with all eight tasks unchecked. All O01–O20 obligations remain open. No V2, serving registration or production activation was enabled.
- The owner selected the proposed Dapr logical model and delegated remaining schema/control choices within the existing Dapr-only/trusted-code constraints. [Selected-model amendment](story-6-6-dapr-logical-model-amendment.md) records that decision. No repeated model-choice approval is needed.
- Current workflow is Step 3, implementation. The renderer already ran exactly once for this workflow run. Existing snapshot: `/home/administrator/projects/hexalith/eventstore/_bmad/render/bmad-build/eventstore-5ec6a32020fe/39a9329c8771470c32e0/`. Resume its `workflow.md` and `step-03-implement.md`; do not restart the same run's renderer or advance to Step 4 before parent acceptance is complete.

Read the required Hexalith baseline through the permitted root-declared AI.Tools submodule and inspect tracked repository guidance. Never load skills from `references/`. Preserve external changes. Implementation delegation is required by this workflow and is sequential. Use a fresh no-context implementation agent with the exact Step 3 handoff after loading the updated Code Map.

## Saved work and evidence

1. [Runtime-options admission](evidence/story-6-6/verification-2026-10-08-runtime-options-admission.md): exact retained callable/getter binding, original cancellation and shared loss. Earlier final checks passed 89 focused/1,428 Client cases and eight mutations in both dependency modes.
2. [Selected-model input audit](evidence/story-6-6/verification-2026-10-08-selected-model-audit.md): historical approval is separate from current amendments/model inputs. Twenty-eight policy mutations rejected and 26 Contracts audit cases passed.
3. [Logical source/control protocol](evidence/story-6-6/dapr-logical-model-2026-10-08/verification-2026-10-08-logical-model.md): distinct logical codecs/trust, addressed source, complete page admission and operation-owned ledger/response/final protocol. Its historical exact-source packet remains historical after reconstruction changes.
4. [Reconstruction packet](evidence/story-6-6/dapr-logical-reconstruction-2026-10-08/verification-2026-10-08-logical-reconstruction.md): private complete-response intake, supplied exact local state/codec/Apply bindings, canonical per-event last-good bytes, strict JSON/UTF-8 and round-trip checks, shared capacity partitions, participant recovery, retries, takeover and dormant addressed reconstruction.

The final reconstruction Release/package lane passed 95 tests with zero skips and killed 18 compiling mutants. Three mutants directly restore excessive Fold-read, round-trip-read and writer lifetimes; a yielding addressed metadata check catches retained-facade use and asserts exact persisted final state. The final logical-model Release/package lane passed both controls and killed 15 compiling mutants. Receipts hash source/fixture/helper inputs and executed private DLLs before/after commands and reject drift. The two new CI lanes now select Release/package dependencies.

The latest saved [root receipts](evidence/story-6-6/dapr-logical-reconstruction-2026-10-08/root-checks/summary.json) confirm Debug/source Server build and 103 focused tests passed, plus Client build and 60 affected tests passed, with stable inputs and DLLs. Earlier `/tmp/story66-reconstruction-root-receipt.json` and `/tmp/story66-reconstruction-focused.log` describe a different 102-test run with two failures and must not be confused with the later pass. Earlier surviving/noncompiling mutants and input-drift attempts remain unqualified. The [reconstruction stop record](evidence/story-6-6/dapr-logical-reconstruction-2026-10-08/checkpoint.md) and `stop-status.json` confirm no owned verification process remains; unrelated processes were untouched.

## Unfinished checks and next work

No checks should run until the owner resumes. Shared build assets currently remain Debug/source. The required final command has not completed for the final reconstruction bytes:

```bash
dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -warnaserror -p:UseHexalithProjectReferences=false
```

Final independent model/reconstruction vector checks, workflow lint, whitespace/LF verification and final source seal remain pending. The reconstruction report's links to pending final outputs are not successful evidence. Broad affected regressions, package/API/consumer matrices and actual cross-actor/live qualification remain separate unfinished gates.

Next implementation priority is the callback-fence gap now recorded in the parent Code Map: source evolution lacks actual addressed source/current-trust checks between schema/identity/upcaster callbacks; current validation calls two runtime-options getters before an asynchronous source fence; deserialization combines its options getter and serializer without that fence. Shared capability loss and final release checks already withhold stale results, but subsequent individual callables must be fenced. Keep each borrowed facade expired before the next await. Then continue distinct logical purpose-07 completed command-state proof/intake, full effective chain/transcript and the other M4–M8 consumers. Do not silently reinterpret historical `StoredDigest` or proof carriers.

Missing production profile `deploy/dapr/production-profile.yaml`, AD-26 ratification, authoritative catalogs/artifact closure, real source/key/peer enrollment, framework/native execution, broker/fleet and production topology evidence still prevent activation. They do not prevent the remaining authorized dormant local implementation.

## Preservation and review state

Frozen intent SHA-256 remains `de1751b9887e48f8302be092797eba2ab5bbb2595958dab3d7325bd380cf548d`. Selected model document SHA-256 is `969cfe58dcbd040587cbe22864007610634e812f8b57687bdaa3f4172d34d188`; amendment SHA-256 is `9ce0913f5b442db9a1070d6155c9a34fef4aff69a9df85953d9c2a14184a66f7`. Historical normative approval remains `bc1625e3b8147fb0bc2cd9491fad8379a95c1ce0b597eba70f8d29f2fee3b050`.

Root reviewed the concrete reconstruction code, tests and guards incrementally and requested fixes. This is not the workflow's final parent acceptance or independent Step 4 review. All-baseline diff `/tmp/story-6-6-baseline-as3k1mwm.diff` and scoped diff `/tmp/story-6-6-owned-review-gdst29aa.diff` predate final changes. Refresh the unified diff, including untracked files without mutating the Git index, and read/judge every task, acceptance criterion and matrix row before advancing.

Preserve concurrent Admin 5.2, stream/publication, protection 8.3 and root-declared submodule changes. Do not clean, revert, stage, commit or attribute those changes to this implementation. The reconstruction packet's owned-file inventory is the starting point for scope attribution.
