Conduct a review of CONTENT.
Look for what's missing, not only what's wrong.
Compute your finding floor N from the diff file's size: N = min(floor(sqrt(kB) + 1), 10), where kB is the file's size in kilobytes. State the arithmetic in one line, then find at least N issues to fix or improve.
Output a Markdown list of findings only — no severity, priority, or ranking.
If the content is empty, stop and say so.
If you have zero findings, re-check and keep thinking; do not stop with an empty list.

CONTENT: the unified diff at `diff --git a/.gitmodules b/.gitmodules
index a983fd083008ea948398876e56ef6a19fef48e6d..c62b48798894bb3576f02fdf4ebb8c552756e35b 100644
--- a/.gitmodules
+++ b/.gitmodules
@@ -19,3 +19,9 @@
 [submodule "Hexalith.Memories"]
 	path = references/Hexalith.Memories
 	url = https://github.com/Hexalith/Hexalith.Memories.git
+[submodule "Hexalith.Platform"]
+	path = references/Hexalith.Platform
+	url = https://github.com/Hexalith/Hexalith.Platform.git
+[submodule "Hexalith.McpCli"]
+	path = references/Hexalith.McpCli
+	url = https://github.com/Hexalith/Hexalith.McpCli.git
diff --git a/_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission-2.md b/_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission-2.md
index 2b7a3828ae8a696768159740f43bdfc40a7d4840..ddf10e91269de7ae2a174161f5c475ab472e48da 100644
--- a/_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission-2.md
+++ b/_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission-2.md
@@ -2,10 +2,19 @@
 title: 'Story 6.5d: Hold Lifecycle, Resume, and Legacy Admission'
 type: 'feature'
 created: '2026-09-30'
-status: 'in-progress'
+status: 'in-review'
 route: 'dispatch'
-review_loop_iteration: 2
-baseline_commit: '6a2f25e39586692b54b655d3e6e8a5f6fa4d317e'
+review_loop_iteration: 6
+baseline_commit: '01498ac721db7c44f18fcf9591ffbbf30ba245e2'
+initial_baseline_commit: '6a2f25e39586692b54b655d3e6e8a5f6fa4d317e'
+external_workspace_checkpoint: '6dededdecd62dd6dc6d1f15810108d860ec70c8f'
+external_gitmodules_blob: 'c62b48798894bb3576f02fdf4ebb8c552756e35b'
+external_submodule_pins:
+  references/Hexalith.Builds: '21ce044ab465ccb2adab58b3d66e394ffbecf3c2'
+  references/Hexalith.FrontComposer: 'e01aea27df39fd22aa3677900290027c603af543'
+  references/Hexalith.McpCli: '29cf33a8927b12ef1232f25663d9daf5c1ad8369'
+  references/Hexalith.Platform: '7c2f0f89f29c79f5d7ab4b731155e2cb07fc690f'
+  references/Hexalith.Tenants: 'a4a1ce13873128607e4e43abd3f038674e9dabf0'
 context:
   - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
 ---
@@ -52,7 +61,7 @@ context:
 ## Tasks & Acceptance
 
 **Execution:**
-- [x] `_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md` -- re-derive the candidate under review-loop-1 requirements; verify and disposition every routed finding; specify the owned mechanisms, exact codecs/keys/charges/exits/slices, independently recomputed known answers, mutation-killing verifier blocks, and complete integration handoff.
+- [x] `_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md` -- re-derive the candidate under all recorded review-loop requirements; verify and disposition every routed finding; specify the owned mechanisms, exact codecs/keys/charges/exits/slices, independently recomputed known answers, mutation-killing verifier blocks, and complete integration handoff.
 - [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- leave D-SPLIT open during implementation and review; the parent closes it only after a review pass has no surviving defect.
 - [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- advance only `6-5d-hold-lifecycle-resume-and-legacy-admission-spec` through the workflow states.
 
@@ -67,18 +76,49 @@ context:
 
 ## Implementation Notes
 
+- 2026-10-01 iteration-6 parent acceptance audit completed at candidate SHA-256 `04dbef7ab69108d04a54179ada427d97df71c530b3502abf832af020d0d5195e`: read the complete approved-baseline change through every correction delta, including the final signed-request lifecycle, and the entire current independent encoder/exporter. Verified all three execution tasks, seven acceptance criteria and four frozen matrix rows against actual candidate mechanisms, replacement targets and registered assertions. Independently ran all three fenced blocks verbatim; all exited 0. Codec: 30 original plus 12 supplementary answers, 42 byte probes, six keys, 200 malformed and 70 semantic rejections. Full lifecycle session `88175`: 14 statuses, four matrix rows, 27 invariants, all 87 rejected source mutations, 54 dispositions, ten transition matches/50 repeated malformed rejections, all 21 loop-5/19 loop-6 repairs, 46 persisted-only restart boundaries, four preparation-cleanup boundaries, 70 durable-evidence refusals and three current-time completions; loop-6 metrics UTC 64, partial capture 4, continued-authority refusals 32, request refusals 23, request transaction restarts 6, terminal-erasure refusals 6, repair cleanup 4 and failed redrives 141. The independent Node encoder passed all 42 exact answers/six keys, semantic dependency graphs and ten explicitly constructed durable answers. Protected verification authenticated four hashes, two allowed story paths and 21 exact external paths with AD-13 UNAPPROVED. Deferred validation exited 0 with 337 existing advisories; frozen comparison and `git diff --check` passed. Candidate hash remained unchanged throughout the run. The previously recorded signed-request acceptance gap is complete; historical checkpoint-only results remain historical. These local checks prove neither provider atomicity/signing nor runtime behavior. Formal three-layer review is next; counter stays 6, D-SPLIT open and no Story 6.6 or approval authority is granted.
+- 2026-10-01 parent checkpoint verification (not final acceptance): after reading the full baseline diff through both iteration-6 correction deltas and the full independent encoder, the parent independently ran candidate blocks 1, 2 and 3 and the Node encoder at candidate SHA-256 `56e58997b118f417748c7a7bf94b88067f27f0eb18a51d854952db1555f0e32d`. All exited 0: 41 answers/probes, six keys, 195 malformed and 70 semantic rejections; 14 statuses, four frozen matrix rows, 27 invariants, 85 rejected source mutations, 54 dispositions, nine transition codec matches/45 malformed repeats, 21 loop-5/19 loop-6 repairs, 46 persisted-only restarts, four preparation-cleanup boundaries, 70 durable-evidence refusals, three current-time completions, and loop-6 metrics UTC 64/partial capture 4/continued-authority refusals 19/repair cleanup 4/failed redrives 141. Protected verification authenticated four hashes, two allowed story paths and 21 exact external paths with AD-13 UNAPPROVED; the independent encoder recomputed all 41 answers/six keys/nine explicit durable families. Deferred validation exited 0 with 337 existing advisories; frozen comparison and whitespace passed. These checks cover the preserved checkpoint only: the signed-request lifecycle and broader authority cases described below remain incomplete and must be verified against their final source before task completion or formal review. No provider atomicity/runtime behavior is proved. Counter stays 6, task unchecked, D-SPLIT open and sprint/execution in-progress.
+- 2026-10-01 iteration-6 parent acceptance audit remains incomplete: the full repair delta and protected-gate correction have been read, and parent codec/independent Node/protected/frozen/whitespace/deferred checks pass. D11 still requires the exact signed `HX-EV-REDRIVE-REQUEST-2` and recovery of its addressed request, while the addressing table keeps a separate count-addressed create-once request key. The new one-slot attempt family bounds/reclaims attempt rows only; its model/charge/erasure assertions do not retain or authenticate that separate request. Complete the already-approved iteration-6 finite active/disputed **request/attempt** requirement before review: align the request address/lifecycle with a fixed finite retained request ceiling, exact existing signed request bytes and count/scope/issuer/subject/time authority, a justified field-derived charged reserve, fresh authenticated restart/readback, refusal and authenticated predecessor deletion before advancing, stale-count fencing, repair-slot cleanup and terminal erasure. Exercise actual request retention/reclamation across the >130-failure run and restart/repair/refund sides, with a directed request-reclamation/authority mutation; bound all actual rows/bytes, not only the attempt dictionary. Preserve purpose `2d`, public identity, all compatible previous repairs, exact external pins, frozen intent and original family/key coverage, independently recomputing every affected answer/count if needed. Also explicitly execute the existing continued-authority requirement's all-obligations-missing case and wrong account/state/receipt cases, preserving no-send/no-count/no-ledger-mutation rather than relying only on individual owner/generation cases. This is completion of step-3 acceptance in the same iteration 6, not a review loopback, reversion, re-derivation, new scope or nested workflow. Keep the task unchecked and D-SPLIT open; no formal review yet. The current parent full block-2 run is checkpoint evidence only if its source changes for this completion and must not be reported as final-current proof.
+- 2026-10-01 user-authorized exact external registration exception: the user answered yes to the three-path checkpoint question. The unchanged approved checkpoint remains `6dededdecd62dd6dc6d1f15810108d860ec70c8f`, review baseline stays `01498ac721db7c44f18fcf9591ffbbf30ba245e2`, and counter stays 6. Permit only the exact staged `.gitmodules` image (Git blob `c62b48798894bb3576f02fdf4ebb8c552756e35b`) adding Platform/McpCli registrations, and exact clean root gitlinks McpCli `29cf33a8927b12ef1232f25663d9daf5c1ad8369` and Platform `7c2f0f89f29c79f5d7ab4b731155e2cb07fc690f`, in addition to the previously approved three pins and 17 committed external paths. Update only the candidate's protected verification and associated current-count/handoff prose to recognize these 21 distinct external paths. Authenticate `.gitmodules` by its full blob in both index and worktree; authenticate both new index entries as mode 160000 at the exact approved pin, their root registrations/URLs and clean worktree HEADs. Existing declared gitlinks and all other external bytes remain under the previous exact checks; new path/content/pin/worktree drift must still fail. Do not change/stage/commit/push or initialize/update any external path, index, Git history or submodule. This removes only the recorded checkpoint blocker; resume the current preserved iteration-6 implementation without reversion/re-derivation, loop increment or nested workflow. Finish remaining in-scope verification and report it honestly; parent acceptance and all three independent reviews remain required. No frozen scope, AD-13, runtime, Story 6.6 or D-SPLIT approval is granted.
+- 2026-10-01 iteration-6 external-checkpoint blocker: the parent reproduced `awk '/^```bash$/{block++; next} block == 3 && /^```$/{exit} block == 3 {print}' _bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md | bash`, exit 1, `AssertionError: ['.gitmodules', 'references/Hexalith.McpCli', 'references/Hexalith.Platform']`. These concurrent staged user changes are outside the approved exact 18-path checkpoint: `.gitmodules` adds the two root registrations (Git blob `c62b48798894bb3576f02fdf4ebb8c552756e35b`), McpCli adds clean pin `29cf33a8927b12ef1232f25663d9daf5c1ad8369`, and Platform adds clean pin `7c2f0f89f29c79f5d7ab4b731155e2cb07fc690f`. HEAD remains `6dededdecd62dd6dc6d1f15810108d860ec70c8f`. No assistant staged, reverted, altered, committed or exempted these paths. User checkpoint direction has been requested; parent acceptance and formal review cannot proceed until that authority is resolved. Preserve the current iteration-6 implementation and its verification checkpoint without resetting the counter or re-deriving again. Focused child results and independent recomputation are not a completed parent acceptance audit. D-SPLIT remains open, sprint and execution remain in-progress, AD-13 remains UNAPPROVED, and Story 6.6 is not authorized.
+- 2026-10-01 user-authorized bounded iteration 6: the user answered yes to another repair/review pass after the loop-limit escalation. Keep the counter at 6; do not reset it or increment again for the already-recorded loopback. Preserved the full pre-revert implementation and execution evidence at `/tmp/bmad-build-6.5d-loop5-pre-loop6-full.patch`, then restored only the unsuffixed candidate to approved baseline `01498ac721db7c44f18fcf9591ffbbf30ba245e2` using a scoped recoverable file patch. Re-derive it under the requirements below, selectively restoring every compatible KEEP repair. This authorization covers one implementation/acceptance/three-layer review pass only. A further bad-spec or intent-gap loopback must increment to 7 and halt before another reversion or re-derivation. No frozen intent, approved external state, runtime, dependency, Git history or AD-13 permission changes. D-SPLIT stays open and sprint in-progress until the parent completes review.
+- 2026-10-01 formal loop-5 review reached the automatic-loop limit: all three independent layers reviewed `/tmp/bmad-build-6.5d-loop5-review-input.patch` before any triage. All 19 findings survive: 15 bad-spec findings form nine roots and four findings are direct patches, moot under the required loopback. The parent reproduced every non-gap claim with `python3 /tmp/bmad-build-6.5d-loop5-parent-review-probes.py` (exit 0); the verification-gap claim is trusted as pre-verified. The green acceptance/mutation results above did not cover these additional reachable paths. Incremented the counter from 5 to 6 and HALTED before reversion, re-derivation, patches or new implementation instructions. The current candidate and full review diff are preserved. Human direction is required for another bounded repair/review pass. D-SPLIT remains open, sprint remains in-progress, execution remains in-review and AD-13 remains UNAPPROVED; Story 6.5d is not complete and Story 6.6 is not authorized.
+- 2026-10-01 loop-5 parent acceptance audit completed: read the entire approved-baseline unified diff, including every later correction delta, and confirmed the external checkpoint tail remained byte-identical to the already audited snapshot. Verified all three execution tasks, seven acceptance criteria and all four frozen matrix rows against the candidate and executed assertions. Independently reran all three final candidate blocks: 38 exact answers/probes, six keys, 180 malformed-record and 70 semantic rejections; 14 statuses, four matrix rows, 27 invariants, 67 rejected source mutations, 54 dispositions, six transition codec matches/30 repeated malformed rejections, 46 persisted-only restart boundaries, four cleanup boundaries, 70 durable-evidence refusals and three current-time completions. Separately ran the independent Node encoder after reading its full current implementation: all 38 lengths/hashes, six keys, semantic dependencies and six independently constructed durable families passed. Protected verification passed with four exact hashes, two allowed story paths, 18 pinned external paths and AD-13 UNAPPROVED; deferred validation exited 0 with 337 existing advisories, frozen-block comparison and whitespace passed. The nonfixture-handle claim/audit/state bindings, prior-success audit and distinct A8/D9 authorities now pass their actual assertions. These local models establish no provider atomicity. Formal review is next; D-SPLIT stays open and no approval or runtime implementation is authorized.
+- 2026-10-01 completed the bounded loop-5 restart/claim correction for parent acceptance audit: restart now consumes actual origin/reconstruction/progress, D7 charge/counter and D11 entry/index byte rows plus addressed artifacts and authenticated provider-native receipts; poisoned process artifact/charge/progress caches are discarded. New D9 artifacts derive from authenticated origin and successor intent, not planned fixture-success bytes. Stored audit/state fields bind the nonfixture request's exact owner, carrier, predecessor, ordinal/result and hashes. Claim tag 07 independently authenticates the A8 head; tag 09 preserves the predecessor successful audit, including a second preparation after prior success. Changed A8/audit authority refuses unchanged. Checked state/inventory CAS, staged-generation ownership, deletion-before-progress recovery, no-arm-during-cleanup, original authorization/expiry and current-time completion are exercised. All three final blocks pass with 67 rejected source mutations, 46 persisted-only restart boundaries, four cleanup boundaries, 70 durable-evidence refusals and three late completions; complete counts are below. The independent Node encoder passes all 38 lengths/hashes, six keys, existing dependency graphs and all six new durable families constructed from explicit inputs. Rerun with `python3 /tmp/export-6-5d-loop5-codec-fixtures.py | node /tmp/verify-6-5d-loop5-independent.mjs`. Deferred validation exits 0 with 337 existing advisories; frozen/state and whitespace checks pass. The original 30 codec answers and protected/external work are untouched by this final correction. Provider atomicity/runtime behavior remains unproved. Candidate task completion and review transition remain parent-owned; D-SPLIT stays open, sprint in-progress, loop 5 and AD-13 UNAPPROVED.
+- 2026-10-01 final claim-field check within the same incomplete parent acceptance audit: D9.1 declares request tag `07` as the latest A8 head and tag `09` as the predecessor successful audit hash. The new `PreparationStore` constructor and supplementary origin fixture instead put the D9 resume-state predecessor hash in tag `07` and always put zero in tag `09`. Preserve the declared contract: resolve/authenticate the actual existing A8-head fixture authority separately from the D9 state predecessor, put that A8 hash in the claim, and copy/check the predecessor successful audit from the authenticated current state. Exercise a preparation after a prior success (nonzero prior audit), and refuse altered A8/audit authority unchanged. Audit tag `06` continues to bind the exact prior D9 resume-state hash; do not conflate these two heads or change D9.1 to match the model. Update and independently recompute only the affected supplementary answers and expected Node fixture bytes. This is still completion of the existing step-3 claim/restart acceptance requirements, with loop 5, frozen scope and all prior successful repairs preserved.
+- 2026-10-01 continuing the same incomplete loop-5 parent acceptance audit: the new byte-backed `PreparationStore` still installs fixture `codec['audit']` and `codec['state']` as supposed existing provider imports before origin admission. A direct parent probe ran the lifecycle definitions, prepared the existing `hxrsm1-other`/`custom-handle` drain-only case, called `PreparationStore.turn()` and decoded its stored audit: the turn returned `completed`, but both `audit[3] == owner` and `audit[4] == sha256(carrier).digest()` were false. New D9 audit/state/resolution/window intent cannot be smuggled in as an undeclared durable planned-artifact cache. Complete the existing restart requirement using real predecessor imports and declared bounded origin/preparation/progress evidence; derive new D9 artifacts from the authenticated original origin and recorded successor intent, bind their decoded fields/hashes to that exact owner/carrier/predecessor/result, and refuse mismatch unchanged. Existing C2/C5 fixture-provider authority may model actual pre-existing immutable readbacks, not unrelated future success bytes. Verify the stored audit and successor actually describe the nonfixture-handle request and reconstruction result, in addition to once-only recovery and crash/cleanup coverage. This is still step-3 completion, not a new review loop or permission to alter frozen scope.
+- 2026-10-01 loop-5 parent acceptance audit, still incomplete: the complete approved-baseline diff has been read. `restart_preparation` currently serializes `vars(store)` and restores `raw_artifacts`, process-owned charge-swap fields and progress flags; `PreparationStore.turn` consumes that restored cache rather than deriving unfinished work solely from the declared origin/reconstruction/progress records and authenticated existing ledger/artifact readbacks. This does not satisfy the existing loop-5 restart requirement. Finish this missing implementation before step-4 review: persist and consume the actual bounded declared records; discard all process caches at every modeled restart; reconstruct and authenticate original claim/times/expiry, remaining artifact bytes, progress/cleanup and charge ownership from those records and provider-model readbacks. Include actual origin/progress write boundaries and missing/changed durable evidence refusal. Preserve all compatible fixtures, repairs, limits and frozen intent, independently recompute any changed answers, rerun every block and update counts/evidence honestly. Do not revert/re-derive, increment the loop or start a nested workflow: this is completion of the existing step-3 acceptance work. D-SPLIT stays open, sprint in-progress, loop 5 and AD-13 UNAPPROVED.
+- 2026-10-01 loop-5 resumed implementation handoff: completed six bounded origin/reconstruction/progress/invocation/capture-preparation/repair codec fixtures and eight supplementary answers without changing the original 30 families. Aligned new hold reasons/owners, framed addresses, precharged repair/inventory storage and handoff counts; added the 21-entry loop-5 repair register with both frozen runtime exclusions preserved. Final candidate blocks report 30 original plus eight supplementary answers, 38 digest probes, six framed keys, 180 malformed-record and 70 semantic rejections; 14 status cases, four matrix rows, 27 invariants, 59 source mutations rejected, 54 historical dispositions, six actual-transition codec matches, 30 repeated malformed rejections and all 21 loop-5 repairs. A separate Node encoder recomputed all 38 exact lengths/hashes, six keys and the affected semantic dependency graphs. Full parent audit/review is still required; D-SPLIT stays open, sprint in-progress, loop 5 and AD-13 UNAPPROVED. The child does not complete the candidate task or advance review state.
+- 2026-10-01 resume loop 5: the user approved the three exact external submodule pins above, removing the workspace-checkpoint pause without changing any submodule or Git history. Resume the interrupted implementation from the current unsuffixed candidate; do not revert/re-derive it again or increment the loop. Finish any missing durable-record codecs/known answers, normative/address/inventory alignment, 21-entry loop-5 repair register, current verifier counts and final verification required below. Preserve every completed compatible repair. The earlier implementation reported four matrix scenarios and 55 source mutations passing, 30 original codec answers and 54 semantic rejections, and an independent Node recomputation of complete attempt-set and four affected closure/window/state/audit answers; these partial reports are not a completed parent audit. This is an implementation-child handoff, not authorization to start a nested build workflow. D-SPLIT remains open, sprint in-progress and AD-13 UNAPPROVED until the parent completes audit/review. External pin verification now covers 18 distinct external paths, with exactly three separately authorized submodule revision overrides and clean submodule worktrees; any further drift still fails.
+- 2026-10-01 checkpoint-update result: the approved checkpoint is now `6dededde`, with exactly 17 committed external paths and unchanged review baseline/protected hashes. Running the third candidate bash block verbatim with `awk '/^```bash$/{block++; next} block == 3 && /^```$/{exit} block == 3 {print}' _bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md | bash` still exits 1: `AssertionError: ('references/Hexalith.Builds', '21ce044ab465ccb2adab58b3d66e394ffbecf3c2', '0831915495485a97c8ccb691618418898bf7ae8b')`. Read-only checks also find external-only pointer drift at `references/Hexalith.FrontComposer` to `e01aea27df39fd22aa3677900290027c603af543` and `references/Hexalith.Tenants` to `a4a1ce13873128607e4e43abd3f038674e9dabf0`; all three submodule working trees are clean. None is altered or exempted. Pause remains in effect pending a human decision whether these exact three revisions may be separately pinned as external workspace state. Loop-5 implementation, parent acceptance audit and independent reviews remain unfinished; D-SPLIT stays open and AD-13 UNAPPROVED.
+- Review-loop-5 checkpoint pause: concurrent reminder commits `d0f8b241` and `6dededdecd62dd6dc6d1f15810108d860ec70c8f` changed external paths after the pinned checkpoint `a3c2d543`. The parent ran `awk '/^```bash$/{block++; next} block == 3 && /^```$/{exit} block == 3 {print}' _bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md | bash`; it exited 1 with `AssertionError: docs/guides/typed-reminders.md`. Preserve all concurrent changes and current story-local edits. Implementation is paused pending human permission to update only the external checkpoint and its exact enumerated paths; approved baseline `01498ac7`, protected specification hashes, frozen intent, loop iteration 5, D-SPLIT open, sprint in-progress and AD-13 UNAPPROVED remain unchanged. Full parent acceptance audit and the three loop-5 reviews have not run. The implementation agent reported passing focused lifecycle/mutation checks and partial codec recomputation, but those reports do not establish complete verification.
+- Review-loop-4 parent audit: read the complete approved-baseline diff and verified all tasks and the four frozen matrix rows against the executed assertions. Independently reran all three candidate blocks: 30 known answers, 30 byte probes, six framed keys, 145 malformed-record and 42 semantic rejections; 14 status cases, four matrix rows, 27 invariant checks, 38 rejected source mutations, 54 historical dispositions and every loop-3/loop-4 repair ID. Protected verification passed with four exact hashes, AD-13 `UNAPPROVED`, two allowed story paths and 14 pinned external paths. Deferred validation exited 0 with 337 existing advisories; frozen-block comparison and whitespace passed. Corrected the integration handoff's stale verifier counts to match the executed loop-4 assertions. All three independent review layers remain required before D-SPLIT closure; provider atomicity is not established.
+- Review-loop-4 implementation: selectively restored compatible loop-3 material from the preserved patch and repaired all thirteen root causes without changing the frozen intent or protected artifacts. D7/D9 now share audited charge rollback authority; reservations retain full authenticated identity and check every counter generation; conflicting queue callers cannot cancel another preparation; new resume decodes/authenticates the real carrier and orphan recovery composes with authenticated hourly retry reconciliation. Capture authenticates pre-existing metadata/inventory and preserves four distinct immutable objects/charges on one ledger; crashed redrive has a bounded recovery exit; policy revisions install through a persistent create-once store/CAS head. Family bounds/semantics preserve supported large records and the 1,024/4,096-byte identifier distinction. Corrected the invalid hold owner and counted closure fixtures and independently recomputed their five affected known answers with a separate Node encoder. D1–D13, all earlier repair registers, 54 historical dispositions, baseline/source pins, and the 14-path checkpoint remain intact. D-SPLIT stays open and sprint stays in-progress for parent review.
+- Review-loop-4 verification: all three candidate bash blocks exited 0: 30 known answers, 30 byte probes, six framed keys, 145 malformed-record rejections and 42 semantic/boundary rejections; 14 status cases, four matrix rows, 27 invariant checks, 38 directed source-mutation rejections, 54 historical dispositions and all 20 loop-4 IDs exactly once; protected-path output reports four hashes, AD-13 UNAPPROVED, two allowed story paths and 14 pinned external paths. The independent Node encoder passed all five changed known answers. Deferred-work validation exited 0 with 337 existing advisory entries; whitespace and frozen-block checks passed. These local models do not verify provider atomicity.
+- Review-loop-3 parent audit: read the complete approved-baseline diff, verified all tasks and the four frozen matrix rows against the executed codec/lifecycle assertions, and reran all three candidate blocks independently. Results: 30 known answers, 30 byte probes, six framed keys, 145 malformed-record rejections, 11 semantic rejections; 14 status cases, four matrix rows, 27 invariant checks, 20 source-mutation rejections, 54 historical dispositions, and all 21 loop-3 repair IDs. Protected-path verification passed with four exact source hashes, AD-13 `UNAPPROVED`, two allowed story paths and 14 immutable external checkpoint paths. `python3 scripts/check-deferred-work.py` exited 0 with 337 existing advisories; frozen-block comparison and `git diff --check` passed. Provider atomicity remains explicitly unverified by these local models.
 - Built the candidate as D1–D13 against `6a2f25e39586692b54b655d3e6e8a5f6fa4d317e`, preserving the loop-1 source at `288a6190` and all three child-candidate hashes.
 - Replaced all 14 owned integration rules and amended imported C1/C2/C5/A8 behavior in the handoff. The central correction keeps C5's permanent fence for terminal closure and gives resume a separately authenticated, permanently fenced window namespace.
 - Added 30 exact record/codec answers, six framed-key answers, a 30-byte-mutation codec verifier, four named executable matrix scenarios, 14 status branches, 27 explicit defect mutations, and uniqueness checks over all 54 routed pass-2 findings.
 - Re-derived exact 943-route and 59-member ceilings; removed both resume hash cycles; added a bounded live-retry cache, exact duplicate-free member partition, checked overflow outcomes, classification-preserving legacy recovery, precharged queue slots, queue-corruption recovery, durable delivery observation, resolvable retained-object locators, and the C4 captured-copy amendment.
 - Closed the final acceptance-matrix audit gaps: D1 now reuses D7's admitted-plan capacity subject; both queue destinations are pre-reserved and exercised at a full destination; raw-byte queue corruption, all hard-bound transitions, the exact framed legacy root, and restart/capture/above-maximum delivery state are executable assertions.
+- Implemented review loop 2 with caller-stable resume identity and expiry tombstones, prior-state window construction, staged charge ownership, three-counter pin authentication, a durable ticket allocator, chunked 1,000-member legacy capsules, scope-complete held-delivery accounting, complete framed addressing, and transition-level retry/recovery models.
 - Left D-SPLIT open as required. The sprint row remains `in-progress`; final review-state and ledger transitions remain the parent workflow's responsibility.
 
 ## Spec Change Log
 
+- 2026-10-01 authorized review loop 6: the user approved exactly one additional bounded repair/review pass for BHR6-01..10, ECR6-01..06, VGR6-01 and VGR6-O1..O2. Added the non-frozen iteration-6 requirements below to prevent failed acknowledgement across advancing UTC, stale completed retry retention, stranded capture after valid observation increments, unreserved repair discovery, redrive without required authority, premature cleanup-slot reuse, unbounded attempt persistence, incorrect window membership roots and invalid multi-attempt summaries. Include the four previously moot direct corrections coherently in re-derivation. The preserved source is `/tmp/bmad-build-6.5d-loop5-pre-loop6-full.patch`; only the candidate was restored to the unchanged approved baseline. KEEP all compatible prior constraints and repairs listed in the escalation, at least the existing 38-answer/six-key family coverage, all historical and review registers, and complete independently recomputed affected dependencies. Counter remains 6, frozen content and external pins remain exact, D-SPLIT remains open, sprint remains in-progress and AD-13 remains UNAPPROVED. This supersedes the earlier no-automatic-loop-6 stop only for this user-approved pass; any next loopback increments to 7 and stops for the human.
+- 2026-10-01 review-loop-limit escalation, not a re-derivation: review pass 6 (build loop 5) below verified late-UTC successor acknowledgement and expiry failures; capture recovery blocked by legitimate delivery observations; missing repair-inventory capacity; redrive without metadata/inventory/repair authority; premature repair-slot reuse; unbounded retained attempts; an incorrect unresolved window root; and rejection of valid multi-attempt closure. Four direct corrections cover invocation sorting, released transfer-owner retention, capture-preparation verification coverage and missing-preparation admission. The next loop would be 6, so no implementation requirements were amended and no code was reverted or patched. KEEP the current D1–D13 organization, all compatible prior repairs/registers and 54 historical dispositions, frozen owner decisions, exact baseline/checkpoint/submodule/protected hashes, AD-13 UNAPPROVED, 943/59 ceilings, complete attempt evidence, same committed events/MessageIds and immutable accepted members, unchanged drain-only claim, request/ordinal/limit-bound invocation identity, independently authenticated A8/prior-audit fields, persisted-only reconstruction, authenticated charge/cleanup authority, disjoint ordinary/oversize capture, exact retained-byte redrive, canonical response and all four frozen matrix scenarios. Preserve 38-answer/six-key family coverage and independently recompute affected bytes/counts rather than retaining a defective answer. Await explicit human direction before another bounded repair/review pass; preserve all current work and external state.
+- 2026-10-01 authorized external submodule pins: the user approved recording only Builds `21ce044ab465ccb2adab58b3d66e394ffbecf3c2`, FrontComposer `e01aea27df39fd22aa3677900290027c603af543` and Tenants `a4a1ce13873128607e4e43abd3f038674e9dabf0` as external workspace state, without modifying them. Preserve the committed checkpoint `6dededdecd62dd6dc6d1f15810108d860ec70c8f`, its exact 17-path change set, baseline `01498ac7`, all protected hashes, frozen intent and loop iteration 5. The protected gate must validate the exact three overridden revisions, their root-declared gitlink identities and clean submodule worktrees, together with byte-identical committed external files; no blanket drift exemption. Resume current loop-5 work, with no automatic loop 6.
+- 2026-10-01 authorized external checkpoint update: the user approved replacing only the external-workspace checkpoint with `6dededdecd62dd6dc6d1f15810108d860ec70c8f`. Keep approved review baseline `01498ac721db7c44f18fcf9591ffbbf30ba245e2`, all four protected specification hashes, frozen intent and loop iteration 5 unchanged. The checkpoint now accounts for exactly 17 external paths: the earlier 14 plus `ReminderActor.cs`, `ReminderIntentIndex.cs` and `ReminderLog.cs` under `src/Hexalith.EventStore.DomainService/`. Exact checkpoint-content and submodule-cleanliness checks remain mandatory. This authorization does not exempt later uncommitted submodule-pointer changes; preserve and report them rather than changing dependencies or silently accepting drift.
+- 2026-09-30 review loop 5: individually triaged BHR5-01..10, ECR5-01..09, VGR5-01 and VGR5-O1 below. Sixteen bad-spec findings expose twelve roots: undeclared durable orphan reconstruction; pre-audit immutable-preparation cleanup/retry; incomplete attempt evidence root; partial-capture completion; persistent corrupt-redrive repair hold; distinct drain invocation identity; current-time orphan expiry; unique live ordinals; per-kind charge/overhead bounds; authenticated absence versus unavailable rollback reads; disjoint ordinary/oversize capture; and retained-byte authority before redrive. Three small response/bound-verification corrections are moot under re-derivation. Two checkpoint reminder findings are rejected only because the frozen intent explicitly excludes runtime edits; do not change their protected files. Amend the non-frozen implementation requirements below to avoid restart dependence on undeclared snapshots, uncharged artifacts, partial-evidence acceptance, stranded capture, unverified retransmission, duplicate invocation identities, stale live retries and unsupported codec authority. KEEP D1–D13 and every prior compatible repair/register, 54 historical dispositions, all 30 existing codec families, independently recomputed answers, 943/59 ceilings, actual maximum-width legacy chunks, source/baseline/checkpoint pins, unchanged drain-only claim, caller-stable identity and exact response retry, authenticated queue/reservation/counter/charge ownership, real large-family bounds, four frozen matrix rows, AD-13 UNAPPROVED and D-SPLIT open. The pre-revert implementation and current review rows are preserved in `/tmp/bmad-build-6.5d-loop4-full.patch`. Re-derive only the unsuffixed candidate from the approved baseline; leave the frozen execution block and all external checkpoint paths untouched. This is the last permitted automatic loop: a further intent/bad-spec loopback must increment to 6 and halt for the human before reversion or re-derivation.
+- 2026-09-30 review loop 4: BHR4-01..12, ECR4-01..06 and VGR4-01..02 were individually triaged below. Seventeen bad-spec findings expose thirteen root causes: shared charge rollback contradiction; family-specific decoder bounds; reservation ownership; conflicting queue rollback; decoded resume-source authentication; orphan/reconciliation CAS composition; capture metadata/inventory authority; retained-object identity; crashed redrive recovery; semantic decoder invariants; durable policy installation; counter-generation overflow; and identifier-bound mutation coverage. Two verification patches are moot under re-derivation; BHR4-03 is rejected because D7 explicitly aborts a fresh CAS with stale predecessors. Amend the non-frozen implementation requirements below; avoid quota cross-ownership, lost retry evidence, uncharged acknowledgement, stale policy authority, unencodable counters and verification that blesses weakened guards. KEEP D1–D13, all prior compatible repairs, exact source/checkpoint pins, both baseline records, the frozen intent, all 54 historical dispositions and review repair IDs, 30 codec-family coverage, actual maximum-width chunks, unchanged drain-only claim, exact retry responses, authenticated staged charge ownership, and the four matrix rows. Preserve the pre-revert candidate at `/tmp/bmad-build-6.5d-loop3-full.patch`; re-derive only this story's candidate, never the external checkpoint paths. D-SPLIT remains open and sprint remains in-progress.
+- 2026-09-30 external workspace checkpoint: concurrent fast-forward `fcdae3c61ac4a730e8b1362e0a56f4aeb83d5ed4` changed 12 reminder documentation/source/test paths; concurrent commit `a3c2d5434fc262c6d24fbc77d2fe01da775a3fc4` captured much of this story and changed two submodule pointers. Keep approved baseline `01498ac7` for full review. The protected-path verifier may exempt only those 14 enumerated external paths, and only after proving their working-tree content still matches checkpoint `a3c2d543`; any additional or uncommitted external difference remains a failure. The four protected specification hashes and AD-13 approval checks remain exact.
+- 2026-09-30 review loop 3: BHR3-01..12, ECR3-01..06, and VGR3-01..03 exposed model gaps in allocator admission, unchanged drain window identity, exact retry results/orphan completion, expiry conversion, actual chunk codecs/roots, exclusive checked recovery, repeated redrive, capture quota, policy continuity, counter predecessor verification, and semantic decoding. Re-derive the candidate from the authorized baseline with the requirements below and the prior preserved diff at `/tmp/bmad-build-6.5d-loop2-reanchored.patch`. KEEP the normative D1–D13 decisions, bounded codecs and chunk design, caller-key identity, framed addresses, source hashes, approved baseline, 54 historical dispositions, and all prior successful repairs. Avoid changing those decisions to match a permissive model; repair the transition and test the actual result.
+- 2026-09-30 authorized baseline change: the user approved re-anchoring this resumed build to `01498ac721db7c44f18fcf9591ffbbf30ba245e2` after concurrent `/pushall` commits and rebase captured earlier story work alongside unrelated Story 6.1 evidence and submodule changes. Preserve the initial baseline above and earlier review evidence; use the new baseline for remaining diff review and protected-path checks. No Git history or unrelated content is changed by this re-anchor.
 - 2026-09-30 implementation: expanded the 68-line backlog candidate into the reviewed section candidate; defined bounded activation, scope, quota, queue, resume, legacy capsule, held-delivery, quarantine, and inventory lifecycles; added the integration handoff and complete pass-1/pass-2 disposition registers. AD-13 and its receipt were not edited.
 - 2026-09-30 review-loop-1 implementation: rebuilt the candidate from its backlog base and selectively restored the D1–D13 design with exact sizing, key framing, acyclic hash/write order, bounded retry evidence, non-overflowing charged waits, exhaustive resume/legacy validation, durable delivery capture authority, and one disposition for every loop-1 finding.
 - 2026-09-30 acceptance-matrix audit: aligned the D1/D3/D12 wording, modeled both queue-slot reservations and full-destination moves, decoded corrupt queue bytes directly, exercised each hard bound with the incremental override, recomputed the D10 domain-separated row root, and asserted durable held-delivery state across restart, capture acknowledgement, and above-maximum incidents.
+- 2026-09-30 review-loop-2 implementation: removed the resume window/state cycle; added stable idempotency and retained expiry evidence; made charge transfer crash-safe; authenticated every changed quota counter; made queue tickets, ordering, duplicate rejection, and ceiling decoding executable; chunked legacy capsules; completed policy/held/redrive identity and accounting; and added decoder-level malformed-family rejection.
 - 2026-09-30 review loop 1: VG-1 through VG-5, VG-O1, BH-2 through BH-18, and EC-1 through EC-17 plus EC-19 through EC-23 exposed non-frozen specification gaps in record sizing/framing, key and hash dependency graphs, retention and charge lifecycles, resume state transitions, held-delivery authority, and executable verification; BH-1 and EC-18 were smaller patches but are moot under re-derivation. The requirements below replace those incomplete instructions, keep D-SPLIT open until review succeeds, and avoid unaddressable records, hash cycles, capacity overflow, silently omitted members, classification drift, and false verification claims. KEEP the D1–D13 organization, permanent C5 terminal fence plus window-scoped resume, fail-closed source-less legacy policy, quota-ledger two-phase reservation, immutable accepted members/pins/first response, 30 codec-family coverage, four named matrix scenarios, all 54 historical dispositions, protected paths/hashes, and AD-13 `UNAPPROVED`.
 - 2026-09-30 review loop 2: BHR2-01 through BHR2-18, VGR2-01 through VGR2-09 plus VGR2-O1, and ECR2-01 through ECR2-12 exposed a new resume-state/window hash cycle, non-idempotent retry identity and expiry, incomplete durable addressing/accounting, unrepresentable legacy capsules, incomplete held-delivery state, and verifier models disconnected from the normative transitions. The requirements below replace those incomplete instructions and avoid cyclic construction, duplicate resume windows, unauthenticated quota movement, stranded legacy ranges, cross-scope capture collisions, and executable tests that pass without exercising the claimed rule. KEEP the D1–D13 organization; exact 943-route and 59-member ceilings; framed keys already proven; acyclic closure/history and audit/prior-state dependencies; stable admitted-plan capacity subject; closed owner/reason mappings; two-counterpart queue reservations and full-destination moves; exact duplicate-free resume union; legacy row-root and success/rejection preservation; durable capture/readback authority; the disjoint 193 MiB boundary; hard-bound no-partial-apply behavior; 30 record families, 54 historical dispositions, protected paths/hashes, and AD-13 `UNAPPROVED`.
 
@@ -114,6 +154,72 @@ context:
 - Decode the full persisted queue record, including reserved-slot count and capability ceiling, and reject combined ceiling overflow before movement. Add below/at/above ten-year retention readiness and slice activation checks, plus distinct post-activation stream growth transitions for each hard replay bound.
 - Replace digest-only byte flips with decoder-level malformed-record checks for missing, duplicate, reordered, overflowing, and trailing fields across the owned codec families. Keep exact length/hash known answers as framing evidence, but do not describe digest inequality as decoder rejection.
 
+### Review loop 3 implementation requirements
+
+- Re-derive only the 6.5d candidate from authorized baseline `01498ac721db7c44f18fcf9591ffbbf30ba245e2`; selectively restore the compatible loop-2 repairs from the preserved diff. Keep both original and current baseline evidence, the frozen execution block, all historical dispositions, and protected sources.
+- Queue admission must consume its own allocated ticket and atomically materialize exactly one row plus the counterpart reservation. Test allocation followed by admission, duplicate subject, full destination, allocator exhaustion, and lost-ack retry of the same successful admission; invalid or refused admission must not leave a leaked ticket/slot/charge.
+- A drain-only resume preserves the active window claim bytes/hash as well as window/closed count/roster/accepted/unresolved identity. Its resolution links the exact old record to constructible successor evidence and its invocation uses that unchanged claim. Test this through the actual transition result.
+- Live retries return the exact stored response. An orphan audit represents a partially committed success: recovery completes the recorded successor state/charge and returns that same response while creating no second audit or invocation. Model the crash boundary explicitly; a label-only retry is insufficient.
+- Store request expiry on success and reconcile live rows to tombstones at expiry, with exact 30-day delete-after and authenticated hourly deletion. Test before/at/after expiry, before/at deletion, conflicts from reconstructed caller requests, and capacity recovery after deletion.
+- Build actual legacy chunk records and a real bounded manifest for the 1,000 maximum-width member case. Compute chunk-row roots using `HX-EV-LEGACY-RESUME-CHUNK-ROWS-1` over exact uncounted row bytes, compute the total event root with the counted events domain, validate each addressed chunk length/hash/count/endpoint, and reject missing/swapped/changed chunks and bad roots. Compare helpers to codec known answers; do not let a root mutation pass.
+- Recovery carries owner, expected generation/predecessor hash, exact capsule binding, and verified repaired-evidence receipt. Use checked u64 generation and ordinal transitions. Reject competing/stale owners, skipped states, failed repairs, changed capsule, zero/above-max ordinal, and generation overflow with unchanged input state; test every failure class and second exhaustion.
+- Held-delivery transition consumes the previous durable record. Repeated failed redrives and restart preserve/increment counts, errors, charges, carrier, locator, and bounded backoff. Capture must reserve ordinary retained-object quota and authenticate exact object readback before acknowledging; refusal/unavailable or mismatched readback leaves the copy unacknowledged and counters correct.
+- Policy transition consumes predecessor revision/hash and expected current head. Require contiguous revision, reject stale/competing/skipped installation, and verify acknowledgement against the selected head. Test independently different nonzero predecessors and compare the exact successor hash.
+- Pin reservation consumes and authenticates tenant, tenant-pool, and deployment predecessor receipts against actual ledger state before one batch mutation. Mutate each predecessor independently and assert complete unchanged counters/charges/reservation on rejection.
+- The record decoder returns typed values and performs semantic checks, including positive bounded legacy event count, range/count agreement, valid optional markers/presence relationships, enum/identifier bounds, and exact trailing rejection. Add concrete malformed signed and optional cases alongside framing checks; do not describe framing-only checks as full semantic validation.
+- Reconstruct stable resume requests with the same caller key and different reason/source: identity remains equal, carrier changes, and the transition conflicts without mutation. Independently vary every held-key identity component, especially two valid tenants, and require different hashes.
+- Preserve D-SPLIT `open` and sprint `in-progress` until the parent completes review; no commit, remote operation, or unrelated edits.
+
+### Review loop 4 implementation requirements
+
+- Re-derive only the unsuffixed candidate from approved baseline `01498ac721db7c44f18fcf9591ffbbf30ba245e2`, selectively restoring compatible material from `/tmp/bmad-build-6.5d-loop3-full.patch`. Preserve every prior change-log constraint and KEEP item, including the 14-path external checkpoint verifier. The frozen execution block and protected artifacts remain untouched.
+- D7 and D9 must share one charge-rollback authority: an absent successor alone never releases a staged charge after authenticated successful-audit readback. Execute both crash sides and repeated recovery with exact counters/ownership. Do not weaken D7's stale-predecessor rejection for a new CAS; lost acknowledgement may read back an already authenticated retained reservation instead of reissuing a stale transaction.
+- Pin reservations must retain and authenticate their complete account/scope/candidate/request identity, not just amounts or an opaque batch label. A different tenant or changed identity under an existing key must reject without counter, charge or reservation mutation. Checked u64 generation advancement must cover every changed counter before any reserve/refund mutation; at maximum, fail closed with byte-identical state rather than writing an unencodable generation.
+- A conflicting queue materialization cannot release a different legitimate pending preparation. Bind rollback to that preparation's authenticated owner and predecessor, distinguish refusal from owner-authorized rollback, and test changed tenant/state/ticket plus exact retry and complete once-only cleanup.
+- Decode variable fields using the declared family/field maxima, not a universal 1 MiB ceiling. Real supported queues above 1 MiB and bounded hold-index/directory blobs must decode while their ceiling-plus-one counterparts reject. Cover ordinary 1,024-byte identifiers and the explicitly permitted 4,096-byte locator/subject fields separately. Execute below/at/above identifier limits and a directed bound-weakening mutation.
+- Semantic record validation must enforce the normative family invariants, including exactly 256 capability shards, directory shard 0..255, genesis/predecessor relationships and charge ownership/presence. Review all owned family constraints rather than patching only the three probes. Keep independently recomputed known answers; correct an invalid fixture rather than relaxing its guard. Add concrete malformed-semantic and source-mutation assertions.
+- A new resume must decode its real caller carrier, recompute stable identity and exact hash, authenticate tenant/handle/expected hold source against current evidence, and reject mismatches without mutation. Passing a label such as `evidence='current'` cannot substitute for checking those fields. Exact live/orphan/tombstone lookup still precedes new-admission preconditions as D9 requires.
+- Orphan recovery and hourly live/tombstone reconciliation share the same CAS head. Specify and execute an authenticated composition/serialization rule that preserves every authorized intervening retry-index/tombstone update while completing the recorded success, rather than restoring an obsolete entire snapshot. Corrupt receipt, wrong owner, altered protected prior state and changed carrier must hold unchanged; valid recovery remains exact and once-only. Test recovery after real live-to-tombstone reconciliation, not only a fabricated empty predecessor.
+- Exercise exact and changed-carrier retries against the reconciled tombstone state before deletion, with unchanged state and expired/conflict outcomes. Include tombstone-branch removal and orphan-authentication bypass in the directed source mutations; each must fail its actual owning assertions.
+- Capture can acknowledge only after authenticated readback of the pre-existing 32 KiB held-record charge, inventory reservation, exact immutable retained object, locator and ordinary object charge. Missing/stale/wrong-owner metadata or inventory authority rejects unchanged and cannot be replaced by a numeric `charged_bytes` claim. Derive object address and quota owner from the complete held identity, not one constant tenant/key. Capture two different carriers and two scope identities on one persistent store/ledger and prove the first immutable object and charge survive the second; exact retry remains once-only.
+- Add durable crashed-redrive reconciliation: after `captured -> redriving` and restart with no completion/failure recorded, authenticate the persisted attempt, either complete terminal readback or return to a scheduled captured retry without losing charge/carrier/count. Define bounded unknown/unavailable evidence behavior and verify no entry remains indefinitely stranded in redriving. Preserve existing repeated-failure history/backoff and terminal-close tests.
+- Model policy installation through actual persistent create-once revision addresses and a CAS head. Two differing successors from one predecessor cannot both install; exact lost-ack readback is stable, stale/competing revisions cannot authorize capture, and only the authenticated selected contiguous revision is active. Keep the exact predecessor-hash and genesis known-answer assertions.
+- Record every BHR4/ECR4/VGR4 finding exactly once in a new repair/disposition register, including the rejected stale-CAS claim with its reason. Rerun all candidate blocks and narrow bookkeeping checks; do not claim provider atomicity from these executable local models. Leave D-SPLIT open and sprint in-progress for parent review.
+
+### Review loop 5 implementation requirements
+
+- Re-derive only the unsuffixed candidate from approved baseline `01498ac721db7c44f18fcf9591ffbbf30ba245e2`, selectively restoring compatible loop-4 material from `/tmp/bmad-build-6.5d-loop4-full.patch`. Respect every earlier change-log constraint and KEEP item. Keep D-SPLIT open, sprint in-progress, the frozen intent untouched and every checkpoint/protected hash exact; no runtime, test, product-documentation, Git-history or remote changes.
+- Make orphan recovery reconstructible solely from declared persisted records, not retained process dictionaries containing entire prior/successor snapshots. Specify bounded exact codecs/addresses, ownership, charge, discovery, erasure, activation and crash transitions for all reconstruction evidence or prove how existing retained codecs suffice. After a restart that discards process state, authenticate and reconstruct the exact protected predecessor, recorded successor intent, retry-index composition, canonical response and charge completion from actual persisted bytes/receipts. Reject missing/changed evidence unchanged. Do not introduce a successor hash cycle.
+- Define the complete pre-audit preparation lifecycle. Immutable signed claims, drain resolutions, closures and window records written before audit must remain charged and discoverable until authenticated deletion or completion; merely releasing the stage cannot abandon them. Exact stable retries must recover the original server timestamps/expiry/bytes rather than signing different bytes at the same create-once address. Model every write/crash boundary and safe rollback side, including retry after unaudited rollback, closure/fence evidence already written, partial cleanup, failed readback and successful-audit fencing. Preserve accepted members and permanent C5 terminal/window fences.
+- Replace the final-summary-only attempt root with a precisely framed complete window attempt-evidence set, including each registration, Unknown observation, definitive result and relevant member/local ordinal binding. Specify bounded storage and authentication for that set and its relation to closure member summaries. Construct known answers from actual complete evidence; changing/removing an earlier attempt must fail even when the final member row is unchanged. Recompute dependent closure/history/window/state/audit answers independently rather than relaxing validation.
+- Capture recovery must finish matching partial work after object/active-charge storage but before held-state CAS/readback. On restart from persisted observed state, authenticate the exact immutable object, locator, object charge, metadata charge, inventory slot and predecessor authority, then complete captured state/acknowledgement once without another reservation or charge. Conflicting or unavailable evidence holds unchanged; incomplete work has an indexed bounded completion/rollback owner. Test each actual storage/held-state crash side on one persistent model store/ledger.
+- Corrupt redrive attempt evidence must persist an indexed repair prerequisite that survives restart and blocks the next cause-cleared automatic/manual send. Only authenticated repair of the exact attempt/carrier/locator/count authority clears it. Test reconciliation of a corrupt attempt followed by repeated automatic turns, refusal before repair, genuine repair and exact once-only resend. Preserve unknown/unavailable completion's bounded captured retry, count/error/backoff and terminal-close behavior.
+- Every redrive must freshly authenticate retained object bytes/header image, length/hash, locator/readback receipt and active charge against the persisted held identity before any send. An altered restored byte image, missing/changed object, wrong locator or unavailable readback must not send, mutate count or bypass evidence repair; test actual redrive output rather than hash-only helpers.
+- Distinguish separately authorized drain-only resumptions in invocation identity/addressing using existing authenticated ordinal/drain epoch/request evidence, while exact retries reuse one invocation. Preserve the unchanged window claim bytes/hash, roster, accepted/unresolved members and member bytes. Execute two actual successive drain holds/resumptions with fresh source/request identities and increased limits; prove different invocation identities, stable exact retry and no duplicate command execution. Update every invocation consumer/handoff coherently.
+- Orphan completion must apply authenticated current completion time, not merely a prior reconciliation timestamp. Test recovery before/at/after its own expiry and before/at/after expiry-plus-30-day deletion, both without and with intervening reconciliation. Complete the recorded success once, immediately place an expired row into its correct retained/deleted state, preserve all authorized current indexes and return the same response without extending retention or resurrecting an old live row.
+- Resume-state decoding must enforce distinct strictly increasing successful live ordinals in addition to distinct request identities, ordinal/head bounds and exact sorted framing. Reject duplicate-ordinal rows with different identities and add a directed guard-removal mutation. Preserve live/tombstone capacity and exact current-time lifecycle constraints.
+- Enforce declared per-kind canonical-length ceilings and recorded-overhead maximum in actual charge decoding/admission/reconciliation, with maximum-inclusive below/at/above tests for every kind and checked amount/generation handling. Preserve the 449 MiB pin, 193 MiB ordinary side/retained, 256 MiB quarantine and 1 GiB resume ceilings; do not widen a guard to bless an invalid fixture. Add below/at/above policy redelivery count tests and a weakening mutation for its maximum 64.
+- Charge rollback/finalization consumes typed authenticated audit/successor presence/absence receipts for the exact owner/generation, not truthiness or unauthenticated booleans. Unavailable, invalid, stale or mismatched readback must retain exact stage/old ownership/counters and hold; only proved absence on both sides permits the defined safe unaudited rollback, and proved success must complete rather than release. Execute unavailable and forged absence paths plus every crash side and repeat.
+- Ordinary capture must enforce `length <= 193 MiB` before any retained-object reservation/write/acknowledgement. Larger carriers enter only the distinct provider-quarantine/above-advertised incident rules with their required exact atomic archive authority and charges. Test below/at/above 193 MiB and the configured maximum against the actual capture transition, proving no ordinary acknowledgement above its ceiling and unchanged/refund-safe counters on refusal.
+- Response encoding must use the authenticated decoded handle, not a fixture literal. Independently specify/assert complete canonical success bytes and all required fields for initial success, live retry and orphan recovery, with at least one valid nonfixture handle. Omitting a field or changing the handle must fail the owning source-mutation assertion.
+- Record each of the 21 BHR5/ECR5/VGR5 findings exactly once in a new repair/disposition register, preserving the two frozen-scope exclusions without touching runtime. Keep all earlier registers/coverage and update every integration handoff/verification count to the actual executed checks. Rerun all candidate blocks, deferred validation, whitespace and frozen comparison; local model results do not establish provider atomicity.
+
+### Review loop 6 implementation requirements
+
+- Re-derive only the unsuffixed candidate from approved baseline `01498ac721db7c44f18fcf9591ffbbf30ba245e2`, selectively restoring compatible material from `/tmp/bmad-build-6.5d-loop5-pre-loop6-full.patch`. Respect every earlier KEEP/change-log constraint and the latest exact 18-path external checkpoint gate. Preserve all completed compatible repairs, actual bounded byte records and provider-model authentication; never restore a flawed cache or weaken a contract to fit a model. This is implementation of this execution spec, not authorization to start a nested build workflow.
+- Recover an authentic successor that survived its write before acknowledgement using the recorded exact intent/readback, even when UTC advances. Do not regenerate different completion-time bytes and misclassify that successor as a predecessor, nor overwrite a manifest before rejecting evidence. Complete the recorded success/charge exactly once, then compose the required current-time expiry transition through the authenticated shared CAS head. Test successor-intent/write/readback/finalize boundaries with distinct restart/completion UTCs, including 1000 -> 1001 and 1000 -> 2000, repeat restart and exact retry, unchanged original audit/claim/response, and unavailable/contradictory evidence with byte-identical state.
+- An acknowledged successor or finalized charge cannot bypass current-time reconciliation through a present-row shortcut. Before recovery reports completed/arms work, apply and read back the exact current-time live-to-tombstone/deletion update without extending expiry or resurrecting an old row; keep authoritative manifest/receipt links coherent. Exercise before/at/after expiry and expiry-plus-30-day deletion across those actual crash boundaries, both with and without intervening authenticated reconciliation, preserving unrelated authorized rows and once-only charge/invocation behavior.
+- Capture preparation must survive legitimate later delivery-count/revision observations. Retain enough declared bounded original-predecessor authority to authenticate and complete its fenced preparation while composing only permitted monotonic observation changes; a hash of an unreconstructible old process image is insufficient. Preserve first-observed UTC, exact scope/carrier/policy/locator/charges and all newer legitimate counts/revisions. Test actual charge-only and object-plus-charge crashes followed by one and multiple normal `observe_delivery` increments, persisted-only restart and exact completion/readback, with no second object/reservation/charge. Changed protected fields or forged observation authority hold unchanged; specify codecs/addresses/charge/discovery/erasure for any added evidence and independently recompute its answers.
+- Before ordinary capture acknowledges, authenticate/reserve both the original held inventory interest and the separate future repair-prerequisite interest at their declared ceilings, ownership and generation. A repair-record byte charge does not reserve an inventory slot. Preflight all affected capacity/counter authority before mutation; refusal leaves the physical copy unacknowledged and has no leaked interest/charge. Exercise a one-slot inventory refusing capture, a two-slot inventory succeeding, conflicting/missing/stale reservations, exact retry and partial-write/restart/refund sides. Keep the repair interest reusable only under authenticated cleanup, not an unbounded additional-slot allocation.
+- Every redrive freshly authenticates the current metadata charge, original inventory interest, repair charge/interest and exact capture preparation as well as retained object/locator/length/hash/readback and active object charge. Required preparation must actually be present and valid: absence on both ledger and held sides cannot pass a nullable equality. Missing, altered, unavailable, wrong-owner/account/generation or stale authority sends nothing, preserves count and leaves evidence/charges unchanged. Test each obligation independently, all missing together, and preparation missing/changed on one or both sides. The owning assertions must kill removal of the preparation comparison/presence and any required retained-authority guard.
+- Authenticated required -> repaired does not itself waive D11's cleanup fence. Delete/read back the exact repaired predecessor record and prerequisite entry before another send or reuse of the single repair interest; partial/unavailable cleanup stays charged, indexed and cannot increment/send. Define and execute restart boundaries around repaired write/readback and each deletion/readback, exact cleanup retry and a second corrupt attempt. Preserve original HeldDelivery reason, the genuine repair receipt, bounded unknown/failure backoff and exact once-only resend; automatic and manual turns enforce the same persisted prerequisite/cleanup authority.
+- Eliminate the unbounded count-addressed redrive log. Specify a fixed finite ceiling for retained active/disputed request/attempt authority, exact typed bytes/addresses, ownership, charge, recovery and authenticated reclamation before advancing. Retain necessary current/disputed evidence without relying on process dictionaries; the monotonic held count fences stale attempts after their bodies are reclaimed. Demonstrate more than 130 genuine failed redrives/restarts with bounded persistent row/byte counts and correct unchanged or explicitly justified bounded charges, then corruption/repair/cleanup and terminal erasure. Bound the actual addressed rows, not just the error-history tuple; a directed reclamation-removal mutation must fail.
+- D9 window tag 08 must bind exactly the unresolved member set, never the whole roster including accepted members. State its precise framed canonical root and use the same definition in window construction, invocation, reconstruction, decoder/consumer checks and integration handoff. Derive it from actual committed member tuples. Test a nonempty accepted set, singleton/multiple unresolved sets, omissions/overlap/unknown members and an independent decoded-field assertion. Independently recompute every affected window/state/audit/preparation/known-answer dependency; preserve same committed events/MessageIds and immutable accepted members.
+- All admitted invocation roots use the declared sorted unsigned-position/member-row encoding. Canonicalize a valid input partition or reject noncanonical input before mutation; never admit equivalent reordered sets with a differently framed invocation root. Exercise both ordinary and persisted-preparation construction with reversed equivalent unresolved rows, exact retries and unchanged drain-only window bytes, and add a directed incoming-order mutation. Keep the already-correct request/ordinal/limit binding.
+- Closure final summaries select exactly the greatest local ordinal's last definitive result for every member, sorted by position. The separately authenticated complete attempt set still includes every earlier registration, Unknown observation and result. Exercise decoder-valid multi-local-attempt histories with different final ordinals/Unknown counts, actual closure/history/window/audit/state construction, and missing/changed earlier authority. No valid multiple-attempt set may be rejected merely because its historical results outnumber members; an all-results-as-final-summary mutation must fail.
+- Every later generation of a transferred resume charge retains its authenticated transfer owner, including released rollback generations. Align writer, decoder/reconciliation/readback expectations and exact retry/restage authority with D7; ordinary never-transferred charges retain their separate absent-owner rule. Test staged -> released -> restaged/active, missing/changed owner and generation/predecessor mismatches with exact counters, plus a directed released-owner removal mutation. Preserve authenticated audit/successor absence requirements and all checked per-kind/overhead/generation bounds.
+- Record each of the 19 BHR6/ECR6/VGR6 findings exactly once in a new repair/disposition register, with executed evidence. Keep all earlier registers and frozen runtime exclusions. Rerun all three candidate blocks, an independent encoder for all affected exact answers/dependency graphs, deferred validation, whitespace and frozen comparison; update every handoff/count to actual output. Preserve at least the existing family/key coverage, add any truly necessary bounded family with full codec/address/lifecycle evidence, and never claim runtime/provider atomicity from local models. Leave D-SPLIT open and sprint in-progress for the parent acceptance audit and all three reviews. Counter stays 6; no automatic seventh pass.
+
 ## Review Triage Log
 
 ### 2026-09-30 — Review pass 1
@@ -217,6 +323,123 @@ Grouped routing: 45 `bad_spec` findings form 32 root-cause entries: membership-t
 
 Grouped routing: 29 `bad_spec` findings form 21 root causes: window/state construction cycle (BHR2-01/VGR2-O1/ECR2-04); caller-stable retry identity, expiry evidence, and transition verification (BHR2-02/BHR2-03/VGR2-05/ECR2-03); counter authentication (BHR2-04); ticket allocation/exhaustion (BHR2-05/ECR2-02); canonical queue ordering (BHR2-06); retained scope-ceiling feasibility (BHR2-07); durable record addressing (BHR2-08/ECR2-01); legacy capsule capacity (BHR2-09/ECR2-05); held-delivery address identity (BHR2-10); held scope/tenant invariants (BHR2-11); ordinary retained-object charging (BHR2-12); subscription-policy lifecycle (BHR2-13); closed failed-set classification (BHR2-14); decoder-level codec verification (BHR2-17); staged charge ownership (BHR2-18); drain-only transition verification (VGR2-03); checked resume transition arithmetic (VGR2-04); legacy recovery graph verification (VGR2-08); repeat legacy exhaustion (ECR2-06); redrive failure lifecycle (ECR2-07); and duplicate wait admission (ECR2-10). The 11 `patch` findings form eight direct verifier/model corrections: preparation recovery (BHR2-15/VGR2-01/ECR2-11); position/MessageId partition (BHR2-16/ECR2-12); status precedence (VGR2-02); persisted queue reserved-slot ceiling (VGR2-06); retention-horizon readiness (VGR2-07); post-activation hard-bound growth (VGR2-09); unknown failed-class handling (ECR2-08); and refund account-kind validation (ECR2-09). Cascading `bad_spec` review makes every patch moot under full re-derivation. There are no `intent_gap`, `defer`, or rejected findings in this pass.
 
+### 2026-09-30 — Review pass 3 (build loop 2, authorized baseline)
+
+| Finding | Verdict and verified evidence | Route |
+| --- | --- | --- |
+| BHR3-01 | high — allocation advances `last_ticket`, then `add` requires a strictly larger ticket; allocator output cannot enter the queue, breaking the admission caller. | bad_spec |
+| BHR3-02 | high — drain-only `resume_publication` computes and installs a new window claim despite the normative unchanged-claim requirement; existing send binding identity diverges. | patch |
+| BHR3-03 | high — live/orphan retries return labels without the stored response, and an orphan with recorded ordinal 2 leaves state at ordinal 1 instead of completing recovery. | bad_spec |
+| BHR3-04 | medium — successful live rows contain no expiry and reconciliation only deletes tombstones, so expiry conversion and retry capacity recovery cannot occur. | bad_spec |
+| BHR3-05 | high — `capsule_chunks` uses the counted total-events domain instead of the codec fixture's uncounted chunk-row domain; actual chunks and model roots disagree. | bad_spec |
+| BHR3-06 | medium — recovery increments generation above u64 maximum and accepts an above-max initial ordinal, returning unencodable durable state. | bad_spec |
+| BHR3-07 | high — recovery has no owner/predecessor inputs, and boolean `repaired=True` clears an evidence failure without authoritative proof, so exclusivity and repair cannot be verified. | bad_spec |
+| BHR3-08 | medium — held-delivery helper reconstructs state on every call; successive failed redrives reset count/error history and cannot exercise restart persistence. | bad_spec |
+| BHR3-09 | high — capture acknowledges immediately after a byte-sum calculation without quota admission or object/readback failure paths, allowing the verifier to bless uncharged/unretained acknowledgement. | bad_spec |
+| BHR3-10 | high — `policy_revision(policy1, 3)` accepts a skipped revision, contrary to contiguous policy/head authority used by readiness and acknowledgement. | bad_spec |
+| BHR3-11 | high — the three-counter proof is only a dictionary-key assertion; no reservation transition authenticates predecessors, so forged tenant-pool evidence passes. | bad_spec |
+| BHR3-12 | medium — decoder reads signed `I` as untyped bytes and accepts legacy count -1; the semantic validation claimed by D12 is absent. | bad_spec |
+| VGR3-01 | medium — pre-verified: adding reason to request identity still passes because conflicts reuse the old identity manually rather than reconstructing caller requests. | patch |
+| VGR3-02 | high — pre-verified: removing tenant from held-key material passes because all valid fixtures use tenant `t`; cross-tenant isolation is not observed. | patch |
+| VGR3-03 | medium — pre-verified: removing predecessor hash from policy hash passes because tests observe only truthiness and the genesis guard. | patch |
+| ECR3-01 | high — independently confirms allocator output is rejected by queue admission's `last_ticket < ticket` guard. | bad_spec |
+| ECR3-02 | high — independently confirms drain-only resume replaces the retained window claim. | patch |
+| ECR3-03 | medium — independently confirms live success lacks expiry and cannot move to a tombstone. | bad_spec |
+| ECR3-04 | medium — independently confirms recovery generation and ordinal produce above-u64 state. | bad_spec |
+| ECR3-05 | high — independently confirms a policy successor may skip the previous revision. | bad_spec |
+| ECR3-06 | medium — replacing every chunk root with zeros still passes because tests observe only chunk count/size, not the root/codec. | bad_spec |
+
+Grouped routing: 16 `bad_spec` findings form 11 root causes: allocator/admission (BHR3-01/ECR3-01); exact retry response/orphan completion (BHR3-03); live expiry conversion (BHR3-04/ECR3-03); actual chunk root verification (BHR3-05/ECR3-06); recovery arithmetic (BHR3-06/ECR3-04); recovery ownership/repair (BHR3-07); persistent redrive (BHR3-08); capture admission/readback (BHR3-09); policy continuity (BHR3-10/ECR3-05); counter predecessor authentication (BHR3-11); and typed semantic decoding (BHR3-12). Five patch findings form four corrections: drain-only claim preservation (BHR3-02/ECR3-02), reconstructed request identity (VGR3-01), cross-tenant key assertions (VGR3-02), and policy hash binding assertions (VGR3-03). Patches are moot under re-derivation. No carried, rejected, intent-gap, or deferred finding remains in this pass.
+
+### 2026-09-30 — Review pass 4 (build loop 3)
+
+| Finding | Verdict and verified evidence | Route |
+| --- | --- | --- |
+| BHR4-01 | high — D7 authorizes staged rollback on absent successor alone, while D9's read-back successful audit requires retaining that stage and completing recovery; an implementer following D7 releases authoritative partial success. | bad_spec |
+| BHR4-02 | high — direct probe encodes a canonical 1,000-row queue at 1,078,163 bytes, within D8's 64 MiB/50,000-row limits, but the universal 1 MiB B-field guard maps it to corruption. | bad_spec |
+| BHR4-03 | false — the exact repeated call is rejected, but D7 explicitly says a stale predecessor aborts the whole CAS; the first call advances those predecessors. No rule requires resubmitting that stale CAS to succeed; authenticated retained-reservation readback is a different recovery operation. | rejected |
+| BHR4-04 | high — direct probe reserves batch for tenant-a, then reuses its key/amounts with tenant-b's fresh predecessors; success is returned while tenant-b has zero charge because the retained record lacks account/scope identity. | bad_spec |
+| BHR4-05 | high — direct probe allocates a subject for tenant-a, then conflicting tenant-b materialization refuses but deletes tenant-a's pending claim, both slots and charge; unrelated conflict can destroy legitimate preparation. | bad_spec |
+| BHR4-06 | high — direct probe submits a framed request naming a different hold source with current-evidence label and receives resumed; the new-admission path never decodes or compares its source field. | bad_spec |
+| BHR4-07 | high — direct probe adds an intervening tombstone to crashed state; orphan recovery returns the saved successor and deletes that tombstone because the prior check omits retry-index/source fields. | bad_spec |
+| BHR4-08 | high — direct probe captures from observed state and an empty ledger; acknowledgement reports a 32 KiB metadata charge although only carrier bytes are reserved, so the gate blesses missing metadata/inventory authority. | bad_spec |
+| BHR4-09 | high — two captured carriers on one ledger share held/exact-carrier-1 and overwrite the single object-charge entry, despite both records claiming immutable retained authority. | bad_spec |
+| BHR4-10 | high — captured-to-redriving followed by restart without outcome leaves state identical and has no attempt reconciliation path; the required deterministic failed/unknown exit is absent. | bad_spec |
+| BHR4-11 | medium — direct codec probes accept capability shard count 1, directory shard 256 and a generation-1 charge's nonzero predecessor; future capability/inventory/charge validators can diverge from declared family invariants while the gate passes. | bad_spec |
+| BHR4-12 | high — direct probe creates two differing revision-2 successors from the same predecessor and both succeed; no persistent revision store/head CAS excludes conflicting policy authority. | bad_spec |
+| ECR4-01 | high — independently confirms BHR4-02: supported queue blobs above 1 MiB are rejected by the shared field-length guard before authenticated row parsing. | bad_spec |
+| ECR4-02 | high — independently confirms BHR4-04: an existing reservation is accepted by amounts alone for a different account without charging it. | bad_spec |
+| ECR4-03 | medium — direct probe starts tenant counter generation at u64 maximum; reserve mutates it to maximum plus one, and subsequent predecessor encoding cannot represent it. | bad_spec |
+| ECR4-04 | high — independently confirms BHR4-07 for the legitimate hourly live-to-tombstone transition: restoring the saved orphan successor loses the tombstone and resurrects expired live evidence. | bad_spec |
+| ECR4-05 | high — independently confirms BHR4-09: both captured objects use one constant locator and the later charge replaces the earlier entry. | bad_spec |
+| ECR4-06 | medium — parent source-mutation probe weakens ordinary identifier maximum from 1,024 to 4,096 and the complete codec verifier still passes; no owning assertion pins the declared limit. | bad_spec |
+| VGR4-01 | high — pre-verified: expiry tests call still-live state, not the reconciled tombstone; removing the tombstone lookup passes every directed mutation and lets a retained retry resume ordinal 3. | patch; moot under loopback |
+| VGR4-02 | high — pre-verified: all orphan fixtures have authentic receipt/owner; disabling that guard passes every assertion and completes successor authority from a zero receipt. | patch; moot under loopback |
+
+Grouped routing: 17 bad-spec findings form 13 root causes: rollback authority (BHR4-01); family bounds (BHR4-02/ECR4-01); reservation ownership (BHR4-04/ECR4-02); queue preparation rollback (BHR4-05); caller-source authentication (BHR4-06); orphan/reconciliation composition (BHR4-07/ECR4-04); capture metadata/inventory authority (BHR4-08); retained-object identity (BHR4-09/ECR4-05); crashed redrive (BHR4-10); semantic validation (BHR4-11); durable policy CAS (BHR4-12); counter generation (ECR4-03); and identifier mutation coverage (ECR4-06). Two patches cover tombstone and orphan-authentication assertions and are moot under re-derivation. BHR4-03 is rejected on its contract refutation. No carried, intent-gap, deferred or unverified finding remains.
+
+### 2026-09-30 — Review pass 5 (build loop 4)
+
+All three reports were collected before triage. Every finding below was checked against earlier rows; none has both the same claim and unchanged offending code. The verification-gap finding is trusted as pre-verified; its other finding and all blind/edge claims were checked independently. Read-only model probes exited 0 and reproduced the listed outcomes without changing any candidate, runtime or protected artifact.
+
+| Finding | Verdict and verified evidence | Route |
+| --- | --- | --- |
+| BHR5-01 | high — the orphan transition reads complete `prior` and `successor` dictionaries, while D9's declared durable state/audit/request codecs and D11 address registry provide neither retained snapshot nor equivalent reconstructible evidence. After restart with only declared records, the owner cannot perform the modeled authenticated comparison and successor completion. | bad_spec |
+| BHR5-02 | high — D9 writes immutable signed claim/closure/resolution/window artifacts before audit, yet unaudited rollback releases their staged charge without a cleanup/readback or original-preparation recovery protocol. A direct codec probe keeps stable identity unchanged but changes server UTC/expiry, yielding different bytes at the same create-once claim address; an exact retry can strand or leave uncharged artifacts. | bad_spec |
+| BHR5-03 | high — the closure fixture and decoder compute the window attempt root from final member summary rows alone; changing or omitting earlier registrations/results does not change that input. C2/C5 consumers can accept an incomplete attempt set as the required complete-window evidence. | bad_spec |
+| BHR5-04 | high — direct probe discards the first capture's returned held-state update while preserving its exact object and active charge. Retrying the persisted observed record refuses at the existing-object guard and remains unacknowledged indefinitely, with no second charge or recovery exit. | bad_spec |
+| BHR5-05 | high — direct probe corrupts a persisted attempt receipt, reconciles to captured, and calls the next cause-cleared turn; it immediately enters redriving at count 2 without repair. The model contradicts the declared no-send-until-repair evidence hold. | bad_spec |
+| BHR5-06 | high — two authorized drain-only resumes with fresh request/source identities produce ordinals 2/3 and limits 24/32 but identical invocation hashes, because the identity contains only unchanged claim/unresolved members. A create-once invocation consumer cannot distinguish separate resumptions from an exact retry. | bad_spec |
+| BHR5-07 | medium — direct probes recover an orphan at time 2000 after expiry 1900, with and without intervening reconciliation; both return a live row and no tombstone. Completion uses only a previous proof timestamp and omits current-time expiry, violating bounded retry lifecycle/capacity. | bad_spec |
+| BHR5-08 | medium — a direct framed-record probe adds a distinct live identity at the same successful ordinal and the real decoder accepts it. The resume-state consumer can admit two successful decisions at one ordinal despite the ordered single-success CAS contract. | bad_spec |
+| BHR5-09 | high — a direct canonical charge probe with 450 MiB pin length and matching amount decodes successfully despite the declared 449 MiB maximum; recorded overhead is also unchecked against its maximum. Admission/reconciliation can bless unsupported object authority. | bad_spec |
+| BHR5-10 | high — direct `ChargeSwap.recover(owner, None, None)` releases a staged 500-byte charge and returns usage to 600; unavailable audit/successor reads are treated as authenticated absence. The crash-recovery gate permits refund without the required absence authority. | bad_spec |
+| ECR5-01 | medium — independently reproduces BHR5-07: recovery inserts its expired row but skips current-time tombstone conversion when no earlier reconciliation exists. | bad_spec |
+| ECR5-02 | medium — direct request with authenticated handle `hxrsm1-other` succeeds but returns `resumeHandle` equal to fixture literal `h`, including retries. Replacing that literal with the decoded handle and adding a concrete assertion is a direct correction. | patch; moot under loopback |
+| ECR5-03 | high — independently reproduces BHR5-04 at the object/charge-before-held-CAS crash boundary: matching retained authority is refused rather than completed. | bad_spec |
+| ECR5-04 | high — direct probe captures and acknowledges an ordinary carrier of 193 MiB plus one byte with sufficient quota and no provider quarantine receipt. The capture transition omits the disjoint ordinary/oversize boundary required by D11. | bad_spec |
+| ECR5-05 | high — independently reproduces BHR5-05: the corrupt-attempt captured result has no persistent repair prerequisite, so the next automatic cause-cleared call sends again. | bad_spec |
+| ECR5-06 | high — direct probe changes restored retained bytes while preserving their recorded carrier hash; redrive enters redriving and sends the changed bytes. No caller or transition authenticates the retained object/hash before send, violating exact-carrier replay. | bad_spec |
+| ECR5-07 | medium — the checkpoint's reminder caller accepts nonnegative `int.MaxValue` attempts and unchecked retry increments produce a negative value rejected on the next load. This is a real pre-existing external-path issue, but the frozen intent explicitly forbids runtime edits and the exact checkpoint remains protected. | rejected — excluded by frozen intent; no external change |
+| ECR5-08 | high — the checkpoint's callback normalizes corrupt state to retained quarantine, then audits/cancels its last reminder without restoring a lost discovery index; neither its wrapper nor persistence restores that index. This real external-path issue is excluded by the frozen prohibition on runtime changes, not merely the implementation plan. | rejected — excluded by frozen intent; no external change |
+| ECR5-09 | medium — a parent source-mutation probe widens the policy redelivery ceiling from 64 to 65; the complete codec block passes and a 65-attempt policy decodes. The existing bounded-policy assertion lacks its below/at/above acceptance check. | patch; moot under loopback |
+| VGR5-01 | medium — pre-verified: deleting `resumeHandle` from the response encoder passes all lifecycle assertions and directed mutations because retries are compared only to the same encoder's output. Initial/live/orphan responses need independent expected canonical bytes and a nonfixture handle. | patch; moot under loopback |
+| VGR5-O1 | medium — independently verified the non-gap expiry defect: orphan completion at 2000 with expiry 1900 retains a live row even after reconciliation, because recovery uses only an older proof timestamp. | bad_spec |
+
+Grouped routing: 16 bad-spec findings form 12 roots: durable orphan reconstruction (BHR5-01); pre-audit preparation lifecycle (BHR5-02); complete attempt root (BHR5-03); partial capture (BHR5-04/ECR5-03); corrupt-redrive repair hold (BHR5-05/ECR5-05); drain invocation identity (BHR5-06); current-time orphan expiry (BHR5-07/ECR5-01/VGR5-O1); unique live ordinals (BHR5-08); charge bounds (BHR5-09); authenticated rollback evidence (BHR5-10); oversize ordinary capture (ECR5-04); and exact retained-byte redrive (ECR5-06). Three patch entries are the hardcoded response handle (ECR5-02), policy-bound assertion (ECR5-09), and independent response verification (VGR5-01); they are included in loop-5 requirements and otherwise moot under re-derivation. The two external-path findings are rejected on the explicit frozen runtime prohibition. There are no carried, intent-gap, deferred or unverified findings.
+
+### 2026-10-01 — Review pass 6 (build loop 5; escalation to iteration 6)
+
+All three reports were collected before classification. The full review input was `/tmp/bmad-build-6.5d-loop5-review-input.patch` (578,267 bytes), not a narrowed story-only diff. Checked every finding against earlier triage rows first; none has both the same claim and unchanged offending code. These findings concern the unsuffixed candidate deliverable or its executable verifier, not edits to this human-owned execution spec. No finding needs new frozen intent, a runtime edit or a deferred scope expansion.
+
+The verification-gap claim VGR6-01 is trusted as pre-verified. Every other claim was independently traced through the current helpers, callers and guards and reproduced by `python3 /tmp/bmad-build-6.5d-loop5-parent-review-probes.py`, which exited 0. The probes read/execute current definitions and use fresh in-memory provider fixtures; they do not edit candidate, runtime, dependencies or Git history. Each finding receives its own verdict below before grouping.
+
+| Finding | Verdict and verified evidence | Route |
+| --- | --- | --- |
+| BHR6-01 | high — successor-write at UTC 1000 followed by persisted-only restart and turn at 1001 or 2000 returns `evidence-hold`, changes the progress manifest, and makes the following restart refuse. The stored authentic successor is compared with regenerated later-UTC bytes and then incorrectly required to be a predecessor; recorded success and its staged charge become stranded. | bad_spec |
+| BHR6-02 | medium — after the acknowledged `successor` or `finalize` boundary at UTC 1000, completion at 2000 returns `completed` with one live row, no tombstone and update UTC 1000 although expiry is 1900. The present-row shortcut omits current-time reconciliation, violating the immediate bounded retry-retention rule. | bad_spec |
+| BHR6-03 | high — both charge-only and object-plus-charge partial capture followed by ordinary `observe_delivery` increment to count 2 remain observed/unacknowledged on retry, with unchanged retained object/charges. Preparation is recomputed from the changed predecessor rather than completing the authentic original preparation while preserving the later observation. | bad_spec |
+| BHR6-04 | high — with inventory ceiling 1, the first observation reserves its sole slot and capture nevertheless acknowledges while only that original reservation exists. Operations cannot guarantee the separately required repair-prerequisite inventory entry after physical-copy acknowledgement. | bad_spec |
+| BHR6-05 | high — removing metadata charge, inventory reservation or repair-slot charge individually, and all three together, still admits redriving at count 1 with send bytes. The redrive guard authenticates object authority but not these continued charged/discoverable obligations, so Operations can send without their required authority. | bad_spec |
+| BHR6-06 | high — authenticated repair clears the prerequisite, and the next call sends at count 2 while retaining the exact old `repaired_record`, with no deletion/readback phase. The single reserved repair slot can be reused before its declared cleanup fence; another corrupt attempt cannot meet that bounded ownership contract. | bad_spec |
+| BHR6-07 | high — 130 legitimate failed redrives retain 130 addressed attempt rows while all charge rows and deployment usage remain unchanged at 49,175 bytes for the probe carrier. The bounded error-history check does not reclaim this separate durable log, contrary to D11's explicit no-unbounded-attempt-log rule. | bad_spec |
+| BHR6-08 | high — decoded successor window tag 08 equals the whole committed-roster image hash and differs from the canonical unresolved root for the accepted-plus-two-unresolved fixture. The window consumer receives authority over the wrong member set and cannot consistently enforce the declared unresolved-only publication boundary. | bad_spec |
+| BHR6-09 | medium — reversing the same duplicate-free valid unresolved partition yields two `resumed` results with different invocation hashes; the upstream partition guard accepts both. The declared sorted-row invocation consumer disagrees with the incoming-order encoder. Sorting the existing root comprehensions and asserting the permutation is a direct correction with no new public surface. | patch; moot under loopback |
+| BHR6-10 | high — a complete, decoder-valid 18-row set for three members with two local attempts each, including Unknown observations, makes closure construction raise `AssertionError`. Every historical result is emitted into a summary counted as three final members, blocking valid retry-exhausted resume. | bad_spec |
+| ECR6-01 | high — independently reproduced the later-UTC successor-write failure at 1001 and 2000, including manifest mutation and subsequent persisted restart refusal. This is the same authenticated-successor acknowledgement defect as BHR6-01. | bad_spec |
+| ECR6-02 | high — independently verified that the complete two-local-attempt set decodes before closure rejects it; no upstream guard makes this legitimate C2 history unreachable. The final-summary construction is the same defect as BHR6-10. | bad_spec |
+| ECR6-03 | high — independently reproduced both partial-capture storage sides after the ordinary observed-count increment. Neither policy selection nor object/charge authority resolves the changed-predecessor comparison; this is BHR6-03's recovery defect. | bad_spec |
+| ECR6-04 | medium — safe unaudited rollback writes a decoder-accepted generation-2 `released` resume-window charge with transfer owner absent. D7 requires every later transferred generation to retain that owner, so a conforming charge-recovery consumer cannot authenticate the released transfer. Correcting the owner assignment and matching readback assertion is direct and adds no public surface or unshown state. | patch; moot under loopback |
+| ECR6-05 | high — independently reproduced authenticated count-1 repair followed by a count-2 send with the old repaired record unchanged and no deletion receipt. This is the same premature repair-slot reuse as BHR6-06. | bad_spec |
+| ECR6-06 | high — independently traced the count-addressed insertion and reproduced 130 retained attempt rows with unchanged charges/counters. No caller or transition reclaims them; this is the same unbounded persistence as BHR6-07. | bad_spec |
+| VGR6-01 | medium — pre-verified: removing only the capture-preparation comparison passes the owning lifecycle assertions; the current damage matrix omits missing/changed preparation, allowing that regression to send and increment count. Add those demonstrated cases and a directed guard-removal mutation; the existing contract already settles the expected behavior. | patch; moot under loopback |
+| VGR6-O1 | high — independently removed preparation from both the ledger and persisted held fixture; the unmodified guard accepts `None == None`, enters redriving, increments to 1 and populates send bytes. Requiring preparation presence before the existing equality is a direct correction for the demonstrated missing-authority state. | patch; moot under loopback |
+| VGR6-O2 | high — independently reproduced the unmodified model's growing addressed attempt log with 130 failures and unchanged 49,175-byte charge. The separately bounded error-history tuple does not bound or erase that log; this is BHR6-07/ECR6-06's persistence defect. | bad_spec |
+
+Grouped routing: 15 bad-spec findings form nine roots: later-UTC successor acknowledgement (BHR6-01/ECR6-01); acknowledged-successor current-time reconciliation (BHR6-02); partial capture plus legitimate observations (BHR6-03/ECR6-03); repair inventory capacity (BHR6-04); continued redrive metadata/inventory/repair authority (BHR6-05); repaired-prerequisite cleanup (BHR6-06/ECR6-05); bounded addressed attempt retention (BHR6-07/ECR6-06/VGR6-O2); unresolved-only window authority (BHR6-08); and multi-attempt final-summary construction (BHR6-10/ECR6-02). Four separate patches cover canonical invocation sorting (BHR6-09), retained released-transfer owner (ECR6-04), preparation fault coverage (VGR6-01) and missing-preparation admission (VGR6-O1). They are moot under the required bad-spec loopback. There are no carried, false, maybe-false, intent-gap, deferred or rejected findings in this pass.
+
+The workflow requires incrementing before any loopback. Incremented `review_loop_iteration` from 5 to 6, exceeding the maximum automatic iteration, and HALTED before reversion, requirement amendment, patch dispatch or re-derivation. The candidate, baseline, full review input, frozen intent and approved external state remain intact. Human decision required: authorize another bounded repair/review pass using the verified roots and KEEP instructions above, or retain this candidate as incomplete. D-SPLIT stays open, sprint stays in-progress and AD-13 stays UNAPPROVED. No final-presentation/completion step has run.
+
 ## Design Notes
 
 Follow the established two-artifact pattern: this `-2` file records execution and review; the unsuffixed file becomes the `candidate`. Keep all provider/crash vectors explicitly future-facing.
@@ -235,5 +458,10 @@ The pin-capacity protocol uses a quota-ledger batch reservation followed by rese
 
 **Implementation evidence:**
 
-- Candidate fenced blocks: `D12 codec verifier: 30 answers, 30 byte mutations rejected, 6 framed keys`; `D12 lifecycle verifier: 14 status cases, 4 matrix rows, 27 mutants, 54 dispositions passed`; protected-path verifier: `4 hashes, AD-13 UNAPPROVED, 3 allowed paths`.
+- Iteration-6 signed-request completion checkpoint (2026-10-01; parent acceptance/review still pending): candidate SHA-256 `04dbef7ab69108d04a54179ada427d97df71c530b3502abf832af020d0d5195e` now retains exactly one original purpose-`2d` signed `HX-EV-REDRIVE-REQUEST-2` and one typed request-bound attempt at fixed slots. Checked count/scope/issuer/subject/UTC, fresh exact readback, authenticated predecessor-pair reclamation, staged-abort/committed-restart semantics, stale-count fencing, repair cleanup and erasure are aligned with a field-derived 32 KiB side reserve that includes staged overlap and bounded native receipts. Actual request/attempt rows and receipts remain bounded across 141 persisted-only held/ledger restarts; missing all obligations and wrong account/state/receipt independently refuse without send/count/ledger mutation. Final-current codec block 1 and independent Node encoder exit 0 with 30 original plus 12 supplementary answers, 42 byte probes, six keys, 200 malformed and 70 semantic rejections; ten durable answers are independently constructed in nine added families plus the existing request family. Focused block 2 (through its directed-mutation section, not the full mutation loop) exits 0: four matrix rows, 46 persisted-only preparation restarts/four cleanup boundaries/70 durable refusals/three late completions; loop-6 UTC 64, partial capture 4, continued-authority refusals 32, signed-request refusals 23, request transaction restarts 6, terminal-erasure refusals 6, repair cleanup 4, failed redrives 141; ten transition codec matches and 50 malformed repeats. Those final-current executable sources have block-2 SHA-256 `455f78c8504363d256efe9f2d90deabd3daf11b513c5fcb57057a4f5e3a95146`; the final prose-only checkpoint correction did not change any fenced source. `/tmp/verify-6-5d-loop6-request-probes.py` separately passes its exact request/attempt baseline and rejects request-signature authority removal, request reclamation omission and attempt reclamation omission. All 87 declared source faults have present textual targets and compile; their final-current full execution is pending the parent, with no overlapping child full run. Protected block 3 exits 0 with four hashes, AD-13 UNAPPROVED, two allowed paths and 21 exact external paths. Deferred validation exits 0 with 337 existing advisories; frozen intent, unchecked task/iteration 6/in-progress execution and sprint, open D-SPLIT and whitespace checks pass. Earlier child session 38940 and parent run 28575 completed the previous 85-mutation checkpoint only; neither proves this new source. No provider transaction, signature implementation or runtime behavior is claimed. Task remains unchecked, D-SPLIT open, counter 6 and sprint/execution in-progress for the parent acceptance audit and three independent reviews.
+- Iteration 6 implementation checkpoint (2026-10-01; not parent acceptance): selectively restored compatible prior repairs and re-derived the unsuffixed candidate's 19 loop-6 findings. Pending successor state retains its original intended bytes before later-UTC reconciliation; acknowledged completion still reconciles current expiry. Publication windows bind sorted unresolved tuples, invocations are permutation-stable, final summaries use each member's last definitive result at its greatest local ordinal, and released transferred charges retain owner. Capture persists bounded original observation/selected-policy authority, reserves a separate repair inventory interest, completes both partial-storage sides after legitimate observations, and refuses changed policy/protected evidence. Redrive freshly authenticates all four retained obligations and present preparations, retains one bounded typed attempt slot, and requires restartable typed repair/prerequisite deletion readback before reuse; terminal erasure and both unaudited partial-capture refund sides execute. Verbatim codec block 1 exits 0 with 30 original plus 11 supplementary answers, 41 byte probes, six keys, 195 malformed and 70 semantic rejections. The independent Node encoder at `/tmp/verify-6-5d-loop6-independent.mjs` exits 0 for all 41 exact answers, six keys, semantic dependency graphs and nine explicitly constructed durable families. The final focused lifecycle (block 2 through the directed-mutation section) passes its original matrix and 64 later-UTC combinations, four partial-capture completions after observations, 19 independent authority refusals, four repair-cleanup boundaries and 141 genuine failed redrives with bounded rows/bytes and exact erasure/refund. An earlier diagnostic run rejected all 85 source mutations; the final verbatim block-2 rerun is still pending. Protected block 3 exits 1 on exactly `.gitmodules`, `references/Hexalith.McpCli` and `references/Hexalith.Platform`, with no gate exemption or external-path change. Deferred validation exits 0 with 337 existing advisories; frozen-intent comparison, iteration/task/D-SPLIT/sprint checks and `git diff --check` pass. Parent acceptance and formal review remain blocked/pending; candidate task stays unchecked, iteration stays 6, D-SPLIT open, sprint/execution in-progress and AD-13 UNAPPROVED. These local models prove no provider atomicity or runtime behavior.
+- Loop 5 corrected final blocks: codec `30 original answers, 8 supplementary answers, 38 byte probes, 6 framed keys, 180 malformed records, 70 semantic defects`; lifecycle `14 status cases, 4 matrix rows, 27 invariant checks, 67 source mutations rejected, 54 dispositions, 6 transition codec matches, 30 transition malformed rejections, 21 loop-5 repairs, 46 persisted-only restart boundaries, 4 cleanup boundaries, 70 durable-evidence refusals, 3 current-time completions`; protected `4 hashes, AD-13 UNAPPROVED, 2 allowed paths, 18 pinned external paths`. Independent Node recomputation, deferred validation (337 existing advisories), frozen/workflow-state and whitespace checks pass. Parent acceptance audit and all formal review layers remain required.
+- Loop 4 final blocks: `D12 codec verifier: 30 answers, 30 byte mutations rejected, 6 framed keys, 145 malformed records rejected, 42 semantic defects rejected`; `D12 lifecycle verifier: 14 status cases, 4 matrix rows, 27 invariant checks, 38 source mutations rejected, 54 dispositions passed`; `protected-path verifier: 4 hashes, AD-13 UNAPPROVED, 2 allowed paths, 14 pinned external paths`. The five changed known answers also passed a separately written Node encoder. D-SPLIT remains open, sprint remains in-progress, and final review is the parent workflow's responsibility.
+- Candidate fenced blocks: `D12 codec verifier: 30 answers, 30 byte mutations rejected, 6 framed keys, 15 malformed records rejected`; `D12 lifecycle verifier: 14 status cases, 4 matrix rows, 27 mutants, 54 dispositions passed`. The earlier protected-path gate encountered concurrent commits after the initial baseline; the user-authorized baseline change above enables the remaining scoped verification.
 - `python3 scripts/check-deferred-work.py` exited 0 and reported 337 pre-existing unclassified legacy advisories; the 6.5d D-SPLIT row remained `open`. `git diff --check` passed.
+- Resumed verification against authorized baseline `01498ac7`: all three candidate blocks exited 0; protected-path output was `4 hashes, AD-13 UNAPPROVED, 2 allowed paths`. Deferred-work validation and `git diff --check` also exited 0.
diff --git a/_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md b/_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md
index d4a885f67dd4592169213bd20a5cc0d57a74d9c2..0e0fbea3c4181a20133e8ab8cc90f27bf41def6b 100644
--- a/_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md
+++ b/_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md
@@ -15,6 +15,8 @@ Specify bounded hold, wait, resume, capacity, and legacy-admission lifecycles as
 
 This documentation candidate is grounded at repository commit `6a2f25e39586692b54b655d3e6e8a5f6fa4d317e`. The loop-1 source is the byte-identical `spec-event-versioning-upcasting.md` at commit `288a6190` (SHA-256 `c474df76a687b2798757051efbdb382bb6b9650f7bd7a7637d0c67bfc5e7b715`). Stories 6.5a, 6.5b, and 6.5c remain unchanged section candidates. AD-13 remains `UNAPPROVED`, and this candidate alone grants no implementation authority.
 
+After concurrent commits and rebase captured earlier candidate work, the user authorized `01498ac721db7c44f18fcf9591ffbbf30ba245e2` as the resumed build's verification/review baseline. The execution record retains the initial baseline and review history; the protected source hashes below remain pinned.
+
 The draft's checked big-endian codecs are imported: `U` and `B` are `u32 length || bytes`, `B32` is exactly 32 bytes, `N` is a checked non-negative u64, `I` is signed i32, `Q` is signed i64 UTC ticks, and `O(X)` is `00` for absent or `01 || X` for present. Every record below is `ASCII domain separator including NUL || one-byte codec || u16 field count || fields in ascending tag order`. Length/count/arithmetic validation precedes allocation. A decoder rejects missing, duplicate, extra, reordered, negative, overflowing, or trailing data.
 
 The current source proves only the shipped starting point. `AggregateActor` deletes `UnpublishedEventsRecord` after drain exhaustion, `DeadLetterMessage` omits `IsRejection`, `ReplayController` is actually at `src/Hexalith.EventStore/Controllers/ReplayController.cs` and resubmits `PublishFailed` commands, Operations capture defaults to 1 MiB with a 10 MiB ceiling, and its current actor index is key-only. No proposed record or provider guarantee is claimed to exist today.
@@ -39,16 +41,18 @@ Every durable hold or wait has exactly one stable subject, one owner, bounded ch
 | `terminal_evidence_hold` | ScopeOpHash + failed head | coordinator; C5 closure progress and hourly | complete C5 terminal closure reads back, or later authoritative publication evidence makes the head nonterminal/published |
 | `PublicationRetryExhaustedHold` | ScopeOpHash + active window + member set | coordinator; authenticated resume or terminal proof | D9 resume opens exactly one successor window, or C5 terminal closure completes |
 | `PublicationDrainLimitHold` | ScopeOpHash + active window + drain-limit epoch | coordinator; authenticated resume/head advance and hourly | a D9 drain-limit resolution closes the exact record before a larger limit becomes active, or a later verified head/terminal pointer supersedes it |
+| `PublicationResumePreparationHold` | tenant + execution + stable request identity | coordinator; exact retry, restart and hourly | D9.4 completes the original prepared success, or authenticated absence and full artifact-deletion readback permit rollback; its charged origin remains retryable through its fixed retention horizon |
 | `PublicationPinCapacityHold` | exact D7 `capacity-subject:` key: framed ScopeOpHash + immutable admitted A8 outbox/member-plan root | quota coordinator; refund/capability/renderer-evidence revision and 60-second re-evaluation | checked arithmetic and evidence produce a valid candidate and atomic batch reservation succeeds; no member holds a partial reservation while held or waiting |
 | `PinCapacityQueueCorruptionHold` | deployment identity + counter ID + queue generation | quota coordinator; authenticated repair/migration completion and 60-second re-evaluation | the exact predecessor and all wait rows are reconstructed, read back, and atomically installed without dropping or duplicating a ticket |
 | `FirstSendMembershipChangedHold` | ScopeOpHash + member position + MessageId | broker membership owner; configuration/membership revision and hourly | only D5's fresh zero-send proof plus byte-identical `ContinueSamePin`; manual action merely requests this check |
 | `ScopeRetentionCapacityHold` | tenant + scope shard | gateway; tombstone expiry/compaction/capability revision and hourly | the exact shard admits the scope record; no other shard's free bytes are asserted as available |
 | held delivery | physical subscription + exact carrier hash | Operations; typed cause-cleared signal and bounded backoff | redrive of exact retained bytes reaches terminal route decisions, or terminal quarantine completes for a permanently nonadmissible carrier |
+| `RedriveEvidenceRepairHold` | complete held-delivery key + disputed attempt count | Operations; authenticated attempt/carrier repair and hourly | D11.1 reads back the exact repaired attempt, locator and active charge before permitting one next redrive; unavailable or forged repair preserves the indexed prerequisite |
 | legacy resume incident | legacy resume handle | aggregate/Operations recovery owner; evidence restoration and hourly | a verified resume capsule exists and D9 resumes it; `legacy_resume_evidence_unavailable` has no fabricated recovery and closes only through tenant erasure or a separately approved migration story |
 
 The last row is intentionally non-resumable when evidence never existed. It is still a complete lifecycle: it is indexed, diagnosed, retained within quota, and removed only by whole-tenant erasure or future separately approved migration—not by command replay.
 
-The closed `ownerKind` set is `actor`, `coordinator`, `gateway`, `subscriber`, `projection`, `operations`, and `quota-coordinator`. The closed hold/reason mapping is: `LegacyArrayLimit -> legacy_array_limit`; `ActivationInventoryCapacityHold -> full_replay_inventory_capacity`; `AdmissionEvidenceHold -> admission_evidence_hold`; `ResponsePreparationHold -> response_preparation_hold`; `OutcomeEvidenceHold -> outcome_evidence_hold`; `OutcomeEvidenceConflict -> outcome_evidence_conflict`; `TerminalEvidenceHold -> terminal_evidence_hold`; `PublicationRetryExhaustedHold -> publication_retry_exhausted_hold`; `PublicationDrainLimitHold -> publication_drain_limit_hold`; `PublicationPinCapacityHold -> publication_pin_capacity_hold`; `PinCapacityQueueCorruptionHold -> pin_capacity_queue_corruption_hold`; `FirstSendMembershipChangedHold -> first_send_membership_changed_hold`; `ScopeRetentionCapacityHold -> scope_retention_capacity_hold`; `HeldDelivery ->` exactly one D11 held-delivery reason; and `LegacyResumeIncident -> legacy_resume_evidence_unavailable`. Producers reject an unknown value instead of indexing or charging it under a catch-all. A reason change is a new D11 entry revision, never an in-place reinterpretation.
+The closed `ownerKind` set is `actor`, `coordinator`, `gateway`, `subscriber`, `projection`, `operations`, and `quota-coordinator`. The closed hold/reason mapping is: `LegacyArrayLimit -> legacy_array_limit`; `ActivationInventoryCapacityHold -> full_replay_inventory_capacity`; `AdmissionEvidenceHold -> admission_evidence_hold`; `ResponsePreparationHold -> response_preparation_hold`; `OutcomeEvidenceHold -> outcome_evidence_hold`; `OutcomeEvidenceConflict -> outcome_evidence_conflict`; `TerminalEvidenceHold -> terminal_evidence_hold`; `PublicationRetryExhaustedHold -> publication_retry_exhausted_hold`; `PublicationDrainLimitHold -> publication_drain_limit_hold`; `PublicationResumePreparationHold -> publication_resume_preparation_hold`; `PublicationPinCapacityHold -> publication_pin_capacity_hold`; `PinCapacityQueueCorruptionHold -> pin_capacity_queue_corruption_hold`; `FirstSendMembershipChangedHold -> first_send_membership_changed_hold`; `ScopeRetentionCapacityHold -> scope_retention_capacity_hold`; `HeldDelivery ->` exactly one D11 held-delivery reason; `RedriveEvidenceRepairHold -> redrive-evidence-repair-hold`; and `LegacyResumeIncident -> legacy_resume_evidence_unavailable`. Producers reject an unknown value instead of indexing or charging it under a catch-all. A reason change is a new D11 entry revision, never an in-place reinterpretation. The repair prerequisite has its own entry under the same HeldDelivery inventory actor; its reason does not reinterpret the original carrier reason.
 
 ## D2. Full-replay activation — replacement for `[I-06]`
 
@@ -82,7 +86,7 @@ Status inspection authenticates the current scope, head, active hold pointers, a
 6. A failed set whose unresolved members are all definitive class-01 at their per-window maximum is `EventsStored`, `Retryable=false`, `RecoveryReasonCode=publication_retry_exhausted_hold`, and `Retry-After: 60`.
 7. A failed set whose unresolved members are all class-01 below maximum and have an admitted automatic attempt is `EventsStored`, `Retryable=true`, `RecoveryReasonCode=publication_retry_pending`, and `Retry-After: 1`.
 8. `pending` or `unknown` is ordinary nonterminal `EventsStored` with `Retry-After: 1`; exhaustion alone never holds it. Only row 4's active drain-limit record changes it to an operator hold.
-9. Any other failed or contradictory set is the closed `CommandOutcomeHold` reason below.
+9. Any other available failed set—including an unknown class, an empty class set, or contradictory class evidence—is `CommandOutcomeHold(outcome_evidence_conflict)`. It can never project retry exhaustion merely because an attempt counter is at maximum.
 
 An open D9 successor window returns to rows 7 or 8 for only its unresolved members. Accepted members, the first response, global pins, MessageIds, batch root, and committed result never change.
 
@@ -160,13 +164,13 @@ The publication-retention ledger on `publicationRetentionBackend` is the single
 
 The capability is `HX-EV-PUBLICATION-RETENTION-CAPABILITY-2\0 || 01 || 000f` (at most 64 KiB): existing tags `01` deployment identity, `02` revision, `03` canonical backend descriptor, `04` tenant ceiling, `05` deployment ceiling, `06` unidentified reserve, `07` unidentified ceiling, `08` overhead `o`, `09` scope-retention ceiling, `0a` predecessor hash, `0b` effective UTC; plus `0c` N scope shard count (exactly 256), `0d` N scope-tombstone retention seconds, `0e` N pin-wait directory ceiling (1..50,000), and `0f` N maximum quarantined carrier bytes (between 193 MiB and 256 MiB). Existing feasibility rules remain: `1 GiB <= tenant <= deployment`, tenant + reserve <= deployment, reserve >=195 MiB, reserve <= unidentified ceiling <= deployment, `scopeRetentionCeiling >= 64 MiB`, and `0 <= o <= 1,114,112`. A smaller scope ceiling is `publication_retention_capability_invalid`; slice 2 may store it for diagnosis but slice 4 cannot activate.
 
-Every charged object has `HX-EV-PUBLICATION-CHARGE-2\0 || 01 || 000e` (at most 4 KiB): `01` U deployment identity, `02` U account kind (`tenant` or `capture-scope`), `03` U account ID, `04` B32 canonical object-key hash, `05` U kind (`pin-batch`, `side-record`, `retained-object`, `oversize-quarantine`, or `resume-window`), `06` N canonical length, `07` N recorded overhead, `08` N charged amount, `09` N capability revision, `0a` N charge generation, `0b` U state (`staged`, `active`, or `released`), `0c` B32 predecessor charge hash (zero at generation 1), `0d` O(B32) transfer owner, and `0e` Q update UTC. Length and overhead are non-negative checked u64; charged amount always equals their checked sum. A later attach must match kind and canonical length and uses the recorded amount despite a changed `o`. Ordinary creation writes `active` with absent transfer owner. A D9 successor writes `staged` with the stable request identity as transfer owner while the prior active generation remains authoritative and every counter includes both amounts. Successor-state readback authorizes exactly one `staged -> active` generation and exactly one release/decrement of the predecessor; absence of successor state authorizes exactly one `staged -> released` rollback. Refund otherwise CAS-writes the next `released` generation and decrements counters once only after deletion/closure readback; a later recreation needs the next `active` generation and fresh counter admission.
+Every charged object has `HX-EV-PUBLICATION-CHARGE-2\0 || 01 || 000e` (at most 4 KiB): `01` U deployment identity, `02` U account kind (`tenant` or `capture-scope`), `03` U account ID, `04` B32 canonical object-key hash, `05` U kind (`pin-batch`, `side-record`, `retained-object`, `oversize-quarantine`, or `resume-window`), `06` N canonical length, `07` N recorded overhead, `08` N charged amount, `09` N capability revision, `0a` N charge generation, `0b` U state (`staged`, `active`, or `released`), `0c` B32 predecessor charge hash (zero exactly at generation 1), `0d` O(B32) transfer owner, and `0e` Q update UTC. Length and overhead are non-negative checked u64; charged amount always equals their checked sum. A later attach must match kind and canonical length and uses the recorded amount despite a changed `o`. Ordinary creation writes `active` with absent transfer owner. A D9 successor writes `staged` with the stable request identity as transfer owner while the prior active generation remains authoritative and every counter includes both amounts. Successor-state readback authorizes exactly one `staged -> active` generation and exactly one release/decrement of the predecessor; D7 and D9 share one rollback authority: only authenticated absence of both the successful audit and successor permits `staged -> released`; unavailable evidence holds. A successful-audit readback with no successor retains the stage and requires completion of that recorded success. Absence of successor alone never permits refund. Every later generation of a transferred charge retains its authenticated transfer owner. Refund otherwise CAS-writes the next `released` generation and decrements counters once only after deletion/closure readback; a later recreation needs the next `active` generation and fresh counter admission.
 
 Each counter is `HX-EV-PUBLICATION-COUNTER-1\0 || 01 || 0008` (at most 1 KiB): `01` U deployment identity, `02` U counter kind (`tenant`, `tenant-pool`, `deployment`, or `unidentified`), `03` U counter ID, `04` N used bytes, `05` N active charge count, `06` N generation, `07` B32 predecessor counter hash, and `08` Q update UTC. Tenant accounts use both their tenant counter and `tenant-pool`; capture scopes use their account counter and `unidentified`; every charge also uses deployment. Authenticated tenant accounts together cannot exceed deployment minus reserve. Capture scopes together cannot exceed `unidentifiedCaptureCeiling`. Lowering below usage admits nothing new and evicts nothing.
 
-`HX-EV-PIN-BATCH-RESERVATION-2\0 || 01 || 000d` (at most 64 KiB) has `01` U tenant, `02` B32 ScopeOpHash, `03` B32 candidate batch root, `04` N pin count, `05` B rows sorted by member position (`u32 position || U MessageId || B32 exact pin hash || N canonical length || N charged amount`), `06` N total charged amount, `07` N capability revision, `08` B32 tenant-counter predecessor hash, `09` B32 tenant-pool-counter predecessor hash, `0a` B32 deployment-counter predecessor hash, `0b` U state (`reserved`, `installed`, or `released`), `0c` N generation, and `0d` Q update UTC. Tag `04` must equal the row count and tag `06` the checked sum. All three predecessor hashes authenticate the exact counters advanced by the transaction; a missing or stale predecessor aborts the whole CAS. At the imported 1,024-byte MessageId maximum, each member row is exactly 1,080 bytes; the maximum-width non-row record is 1,291 bytes, so the hard member ceiling is `floor((65,536 - 1,291) / 1,080) = 59`. The ceiling remains 59 for short IDs. An otherwise-valid V1 batch of 60..1,000 members fails A8 readiness/admission as `AppendPreparationLimit` before append; it is never partially segmented after commit.
+`HX-EV-PIN-BATCH-RESERVATION-2\0 || 01 || 000d` (at most 64 KiB) has `01` U tenant, `02` B32 ScopeOpHash, `03` B32 candidate batch root, `04` N pin count, `05` B rows sorted by member position (`u32 position || U MessageId || B32 exact pin hash || N canonical length || N charged amount`), `06` N total charged amount, `07` N capability revision, `08` B32 tenant-counter predecessor hash, `09` B32 tenant-pool-counter predecessor hash, `0a` B32 deployment-counter predecessor hash, `0b` U state (`reserved`, `installed`, or `released`), `0c` N generation, and `0d` Q update UTC. The candidate batch root is SHA256 of the exact uncounted tag `05` member-row bytes. Tag `04` must equal the row count and tag `06` the checked sum. All three predecessor hashes authenticate the exact counters advanced by the transaction; a missing or stale predecessor aborts the whole CAS. At the imported 1,024-byte MessageId maximum, each member row is exactly 1,080 bytes; the maximum-width non-row record is 1,291 bytes, so the hard member ceiling is `floor((65,536 - 1,291) / 1,080) = 59`. The ceiling remains 59 for short IDs. An otherwise-valid V1 batch of 60..1,000 members fails A8 readiness/admission as `AppendPreparationLimit` before append; it is never partially segmented after commit.
 
-The ledger CAS creates every per-pin charge plus this reservation and advances all counters together. The pin backend installs all exact candidates with the reservation hash; only full readback advances to `installed`. No waiting batch owns a charge. Reservation and refund both decode `accountKind` as exactly `tenant` or `capture-scope`; an unknown value is a codec/reconciliation incident with no counter mutation, never an alias for capture scope.
+The ledger CAS creates every per-pin charge plus this reservation and advances all counters together. The retained reservation authenticates its full tenant account, ScopeOpHash, candidate root, exact ordered member rows, capability revision, and original predecessor receipts; its request identity is the hash of those exact reservation-intent fields. Changed account, scope, candidate, request identity, or rows at an existing key conflicts without mutation even when totals match. A fresh CAS still rejects stale counter predecessors; an exact lost acknowledgement instead authenticates the retained reservation and charge attachments without reissuing that stale CAS. Before reserve, refund, transfer, or reconciliation, preflight every changed counter/charge/reservation generation with checked u64 increment and every amount/count sum; a maximum generation fails closed with byte-identical state and an indexed quota incident. The pin backend installs all exact candidates with the reservation hash; only full readback advances to `installed`. No waiting batch owns a charge. Reservation and refund both decode `accountKind` as exactly `tenant` or `capture-scope`; an unknown value is a codec/reconciliation incident with no counter mutation, never an alias for capture scope.
 
 Per-kind maxima remain 449 MiB for one global pin, 193 MiB for side/ordinary retained objects, 256 MiB for provider-quarantined oversize carriers, and 1 GiB for the one active resume window. A negative or overflowing input, or unavailable/contradictory evidence for any candidate length, overhead, amount, count, or total, maps to `CommandOutcomeHold(publication_pin_capacity_hold)` before comparison. Its stable subject is `capacity-subject:` plus lowercase-hex SHA-256(`"HX-EV-CAPACITY-SUBJECT-1\0" || 01 || B32 ScopeOpHash || B32 immutable A8 outbox/member-plan root`), not an unverified candidate-batch root. If those admitted-plan values are unavailable, A8 fails before commit; no later hold may invent a subject. The hold creates the D11 inventory entry using the pre-reserved A8 slot, but creates no reservation, charge, counter delta, pin, or send. The quota coordinator re-evaluates from immutable outbox/member-plan and renderer evidence on the D1 triggers; only a fully checked candidate may enter the D8 queue or retry reservation. Tenant/deployment erasure releases only after every object is deleted/read back. The ledger activates in slice 2, resume charges in slice 3, and pin/capture charges in slice 4. Known answers `D29-capability`, `D29-charge`, `D29-counter`, and `D29-pin-batch` appear in D12.
 
@@ -176,7 +180,7 @@ A refused **valid, checked** D7 batch reservation creates one wait; it never lea
 
 There is one deployment directory and one directory for each tenant with waits. `HX-EV-PIN-CAPACITY-QUEUE-4\0 || 01 || 000b` (at most 64 MiB) has `01` U deployment identity, `02` U counter ID (`deployment` or `tenant:` plus tenant), `03` N generation, `04` N entry count, `05` B concatenated fixed-order rows with **no inner count** (`N ticket || U tenant || B32 ScopeOpHash || U state`), `06` N parked count, `07` N reserved-slot count, `08` N authenticated capability ceiling, `09` N last issued global ticket, `0a` B32 predecessor directory hash, and `0b` Q update UTC. Tag `04` controls exact parsing and equals queued plus parked; `entryCount + reservedSlotCount <= tag 08 <= 50,000`. The duplicated capability value must equal the authenticated D7 capability revision used by the transition. A row consumes exactly one previously reserved slot in the same CAS, so no operation ever appends to a full directory. Parked waits remain in this ordered directory and are discoverable in a key-only store.
 
-The deployment directory is also the sole durable global ticket allocator. Its `lastIssuedTicket` starts at zero; admission reserves both slots and CAS-increments that field in the deployment row, assigning the resulting positive u64 to the wait. A lost acknowledgement rereads the stable subject's reservation and same ticket. A conflicting subject or predecessor loses without allocating a ticket. `lastIssuedTicket == 2^64-1` fails pre-commit admission as `pin_wait_ticket_exhausted`; it never wraps, reuses a ticket, or commits the command. Queue rows are canonically sorted by `(ticket as unsigned numeric u64, tenant as raw canonical UTF-8 bytes, ScopeOpHash as raw 32 bytes)`; state is not part of the sort key. Both decoding and reconstruction use that exact tuple.
+The deployment directory is also the sole durable global ticket allocator. Its `lastIssuedTicket` starts at zero; admission verifies subject, both slots, state, and charge before CAS-incrementing that field and assigning the resulting positive u64 to the wait. Materialization consumes that subject's exact allocated ticket, which may equal the allocator head; it never requires a second ticket greater than that head. The admission transaction materializes exactly one row and keeps the counterpart slot reserved. A lost acknowledgement rereads the stable subject's admission receipt and same ticket/row without allocating or charging again. A conflicting subject or predecessor loses without allocating a ticket; a refused or invalid admission creates no orphan slot or charge. Pre-commit allocation evidence remains discoverable under the same subject and is either materialized once or released on authenticated preparation rollback; issued tickets are never reused. `lastIssuedTicket == 2^64-1` fails pre-commit admission as `pin_wait_ticket_exhausted`; it never wraps, reuses a ticket, or commits the command. Queue rows are canonically sorted by `(ticket as unsigned numeric u64, tenant as raw canonical UTF-8 bytes, ScopeOpHash as raw 32 bytes)`; state is not part of the sort key. Both decoding and reconstruction use that exact tuple. Conflicting materialization only refuses the caller and leaves the legitimate pending owner, ticket, both slots, and charge byte-identical. Cancellation requires that preparation's authenticated owner, ticket and exact predecessor receipt; its CAS removes both interests and releases its own charge once. Repeating cleanup is a no-op. It cannot cancel another tenant's preparation or a later generation.
 
 Before command commit, A8 charges one 4 KiB wait row plus **two** maximum encoded queue rows and CAS metadata as `side-record`, rejects a stable capacity subject already resident or reserved in either directory, and atomically reserves one slot in the deployment directory and one in the candidate tenant directory under that subject while allocating its ticket. If either slot, ticket, or charge is unavailable, admission fails `AppendPreparationLimit`; a committed command is never left outside the directories. Exactly one reservation is materialized as the current `queued`/`parked` row while the other remains reserved. A cross-counter move atomically turns the destination reservation into the row and the source row back into its reservation, so it cannot deadlock on a full destination. Refund of both slots/row charges occurs only after the wait and both directory interests delete/read back, or during whole-operation erasure. Imported pre-reservation waits must complete a bounded migration into separately charged/reserved slots before slice-4 readiness; they cannot be hidden in an overflow counter.
 
@@ -194,9 +198,9 @@ Publication resume re-arms only unresolved publication of already committed even
 
 Every eligible hold inventory item exposes an opaque stable `resumeHandle = "hxrsm1-" || lowercase-hex SHA256(U tenant || U execution identity || B32 hold-source hash)`. Authorized Operators can read `GET /api/v1/admin/publications/tenants/{tenantId}/{resumeHandle}/precondition`; the response is support-safe `{ resumeHandle, eligibility, expectedHoldSourceHash, headHash, nextResumeOrdinal, predecessorAuditHash, expiresAt }`. It comes from one authenticated current-head read and is never an execution capability. A deployment route does not stand in for a tenant.
 
-`POST /api/v1/admin/publications/tenants/{tenantId}/{resumeHandle}` takes `{ "expectedHoldSourceHash": "<64 lowercase hex>", "idempotencyKey": "<1..128 ASCII visible bytes>", "reason": "<1..512 UTF-8 bytes>" }`. Its canonical caller carrier is `HX-EV-PUBLICATION-RESUME-CARRIER-1\0 || 01 || 0005` over `01` U tenant, `02` U resume handle, `03` B32 expected hold-source hash, `04` U idempotency key, and `05` U reason. `stableRequestIdentity = SHA256("HX-EV-PUBLICATION-RESUME-IDENTITY-1\0" || 01 || B(exact carrier bytes))`; server time, ordinal, and provider observations are deliberately absent. The Admin server authenticates tenant and Operator policy, first resolves that stable identity through the current live row, orphan audit, or expiry tombstone, then rereads the precondition and signs purpose `2d` claim `HX-EV-PUBLICATION-RESUME-3\0 || 01 || 000f` (at most 4 KiB): `01` U operator-action issuer, `02` U tenant, `03` U execution identity, `04` B32 ScopeOpHash (zero for legacy), `05` U eligibility (`retry-exhausted`, `drain-limit`, or `legacy-publish-failed`), `06` B32 current hold source or D10 capsule hash, `07` B32 latest A8 head (zero for legacy), `08` N next ordinal, `09` B32 predecessor successful audit hash, `0a` U resume handle, `0b` B32 stable request identity, `0c` B32 exact caller-carrier hash, `0d` U operator subject, `0e` Q request UTC, and `0f` Q expiry UTC no more than 15 minutes later. The caller need not discover internal ScopeOpHash, ordinal, or audit key separately; the server supplies and signs them from the same read. Stale fields fail before mutation.
+`POST /api/v1/admin/publications/tenants/{tenantId}/{resumeHandle}` takes `{ "expectedHoldSourceHash": "<64 lowercase hex>", "idempotencyKey": "<1..128 ASCII visible bytes>", "reason": "<1..512 UTF-8 bytes>" }`. Its canonical caller carrier is `HX-EV-PUBLICATION-RESUME-CARRIER-1\0 || 01 || 0005` over `01` U tenant, `02` U resume handle, `03` B32 expected hold-source hash, `04` U idempotency key, and `05` U reason. `stableRequestIdentity = SHA256("HX-EV-PUBLICATION-RESUME-IDENTITY-1\0" || 01 || U tenant || U resumeHandle || U idempotencyKey)`; the exact carrier hash separately binds expected source and reason, so changed bytes under the same caller key conflict. Server time, ordinal, and provider observations are deliberately absent. The Admin server authenticates tenant and Operator policy, first resolves that stable identity through the current live row, orphan audit, or expiry tombstone, then rereads the precondition and signs purpose `2d` claim `HX-EV-PUBLICATION-RESUME-3\0 || 01 || 000f` (at most 4 KiB): `01` U operator-action issuer, `02` U tenant, `03` U execution identity, `04` B32 ScopeOpHash (zero for legacy), `05` U eligibility (`retry-exhausted`, `drain-limit`, or `legacy-publish-failed`), `06` B32 current hold source or D10 capsule hash, `07` B32 latest A8 head (zero for legacy), `08` N next ordinal, `09` B32 predecessor successful audit hash, `0a` U resume handle, `0b` B32 stable request identity, `0c` B32 exact caller-carrier hash, `0d` U operator subject, `0e` Q request UTC, and `0f` Q expiry UTC no more than 15 minutes later. The caller need not discover internal ScopeOpHash, ordinal, or audit key separately; the server supplies and signs them from the same read. Stale fields fail before mutation. New admission decodes the real carrier, checks every declared bound, recomputes its stable identity and exact hash, and compares tenant, handle and expected source to authenticated current evidence. An availability label is not source authority. Mismatches conflict without mutation; live/orphan/tombstone lookup still precedes current-source or expiry preconditions.
 
-The same stable identity with byte-identical carrier is one exact retry. A live result or orphan audit returns/completes that result without allocating an ordinal, window, charge, audit, or invocation. A retained expiry tombstone returns `resume_request_expired`. The same identity with different carrier bytes is `resume_request_conflict`, even after the live/audit body is reclaimed. A genuinely new request uses a new idempotency key and stable identity.
+The same stable identity with byte-identical carrier is one exact retry. A live result or orphan audit returns/completes that result without allocating an ordinal, window, charge, audit, or invocation. One unresolved orphan fences new resume admission for that execution until its recorded successor completes; the retained signed claim, audit, and staged charge remain bounded and discoverable under their original reservation even if the request expiry passes during recovery. A retained expiry tombstone returns `resume_request_expired`. The same identity with different carrier bytes is `resume_request_conflict`, even after the live/audit body is reclaimed. A genuinely new request uses a new idempotency key and stable identity.
 
 The eligible states are an unresolved class-01-at-maximum set, an active D3 drain-limit record, or a verified D10 legacy capsule. Pending/unknown without an active drain-limit record, C5 terminal pointer, accepted/not-applicable head, `CommandOutcomeHold`, and D5 membership hold are ineligible.
 
@@ -212,21 +216,27 @@ For retry exhaustion, resume closes only publication window `w`. The broker name
 
 `HX-EV-PUBLICATION-WINDOW-CLOSURE-3\0 || 01 || 000b` (at most 64 KiB) has `01` U tenant, `02` B32 ScopeOpHash, `03` N closed window, `04` B member rows, `05` B32 window broker reject-fence receipt, `06` B32 window producer-disable receipt, `07` B32 final empty-state root, `08` B32 complete window attempt-set root, `09` U C5 AuthMode, `0a` B32 predecessor window-history accumulator, and `0b` Q closure UTC. Tag `04` is explicitly `u32 rowCount ||` rows sorted by position, each `u32 position || N final local attempt || B32 last definitive result hash`; the count controls exact parsing and trailing bytes fail. It never contains its successor. After exact closure and broker-authentication bytes read back, `successor = SHA256("HX-EV-PUBLICATION-WINDOW-HISTORY-1\0" || 01 || B32 predecessor || B(exact closure bytes) || B(exact broker-authentication bytes))`; only the successor D9 state stores that value. The closure key is `publication-window-closure:` plus lowercase-hex SHA-256(`"HX-EV-PUBLICATION-WINDOW-CLOSURE-KEY-1\0" || 01 || B32 ScopeOpHash || N closedWindow`). The closure is invalid if any accepted member would be retried; accepted rows remain in the overall outcome unchanged.
 
-This explicitly amends C5: its terminal roster/attempt verifier consumes the current window's complete evidence plus the authenticated accumulator/count of earlier closed windows. C5 terminal closure still uses the whole-operation fence and then seals that accumulator. A window closure never satisfies terminal closure on its own.
+The complete attempt authority is **not** tag `04`'s final summaries. `HX-EV-WINDOW-ATTEMPT-SET-1\0 || 01 || 0008` is at most 64 MiB: `01` U tenant, `02` B32 ScopeOpHash, `03` N window, `04` B32 immutable committed-roster root, `05` N evidence-row count, `06` B exact rows, `07` B32 semantic root and `08` Q seal UTC. A row is `u32 position || N member-local ordinal || N observation ordinal || U kind || B32 exact registration-parent hash || B32 send-ID hash || B32 exact evidence hash`; kind is exactly `register`, `unknown` or `result`. Sort is the first three unsigned numeric fields. Each `(position, local ordinal)` starts with observation 0 registration, contains every distinct Unknown observation in contiguous observation order, and ends with its definitive result. Every local ordinal from 1 through the final member ordinal is present; registrations/results cannot disappear because a later result exists. All rows in that local attempt bind the same exact parent and send ID, whose authenticated imported C2 bytes bind the committed position and MessageId. The root is SHA256(`"HX-EV-WINDOW-ATTEMPTS-2\0" || 01 || U tenant || B32 ScopeOpHash || N window || B32 rosterRoot || N rowCount || B(exact row bytes)`). Final summaries must exactly equal the last definitive result of every member in this complete set. A closure decoder obtains/authenticates this addressed set and all referenced C2 record readbacks; missing, changed or incomplete authority holds before closure, history, window or audit acceptance.
+
+The immutable set address is `K("HX-EV-WINDOW-ATTEMPT-SET-KEY-1\0", U tenant, B32 ScopeOpHash, N window)`. Active collection uses existing generation-fenced C2 registrations/results and their authenticated observation ordinals, not an uncharged secondary log. D7 pre-reserves the worst case: at most 59 admitted members, the unchanged signed C2 attempt maximum (never greater than 64), and at most 64 distinct Unknown observations plus registration/result per attempt, hence at most 249,216 rows. Each fixed digest row is at most 128 bytes, below 32 MiB; the 64 MiB family ceiling includes its header. Byte-identical repeat readback is the same observation, not another row. At any collection bound, no further send or distinct Unknown registration is admitted; the indexed `resume_evidence_hold` retains all existing bytes and charge until the coordinator obtains authoritative definitive-result/closure evidence or operation erasure completes. It never truncates the set. Slice 4 activates collection; the complete sealed set and referenced evidence remain charged until the owning closure/history readback permits reclamation or scope erasure. The D12 1,691-byte known answer has four actual registrations, four Unknown observations and four definitive results; changing an earlier registration changes the root while the final summary remains identical.
+
+This explicitly amends C5: its terminal roster/attempt verifier consumes the current window's complete evidence plus the authenticated accumulator/count of earlier closed windows. C5 terminal closure still uses the whole-operation fence and then seals that accumulator. A window closure never satisfies terminal closure on its own. C2/C5 consumers must resolve the complete set rather than hashing final summaries.
 
 ### D9.3 Bounded state, charge, and decision
 
-`HX-EV-PUBLICATION-RESUME-STATE-3\0 || 01 || 000e` (at most 32 KiB) is the single CAS head for an execution: `01` U tenant, `02` U execution identity, `03` B32 ScopeOpHash, `04` N successful resume ordinal, `05` N active window, `06` N active drain limit, `07` B32 current hold/source hash, `08` B32 active window-claim hash, `09` B32 window-history accumulator, `0a` N closed-window count, `0b` B32 last successful audit hash, `0c` B live-retry rows, `0d` B expiry-tombstone rows, and `0e` Q update UTC. Tag `0c` is `u32 count ||` rows sorted by ordinal, each `B32 stable request identity || B32 exact caller-carrier hash || N ordinal || B32 audit hash || N returned window || N returned drain limit || Q request expiry`. Tag `0d` is `u32 count ||` rows sorted by stable identity, each `B32 stable request identity || B32 exact caller-carrier hash || Q request expiry`. Live plus tombstone rows are at most 64. At expiry a live row moves, in the same state CAS, to a tombstone retained until execution obligation closure/erasure; audit and closure bodies may then be reclaimed. A 65th distinct success is `resume_capacity_hold` with no mutation. Exact live retries remain answerable, exact tombstone retries remain distinguishable as expired, and changed carrier bytes conflict while the execution exists.
+Window tag `08` and invocation tag `07` use exactly SHA256(`"HX-EV-PUBLICATION-UNRESOLVED-1\0" || 01 || u32 unresolvedCount ||` rows `u32 unsigned position || U MessageId || B32 SHA256(exact committed member bytes)`, sorted by unsigned position). The accepted set is excluded. All constructors, reconstruction and consumers authenticate the duplicate-free exact committed partition and this root; equivalent reordered input is canonicalized before hashing. Drain-only continuation preserves the original claim bytes/hash. Closure summaries select, for each committed member, the greatest local ordinal's last definitive result, then sort by position; earlier results remain in the separately authenticated complete set and do not increase the summary count. A complete set with several local attempts is valid authority for actual closure/history/window/audit/state construction.
+
+`HX-EV-PUBLICATION-RESUME-STATE-3\0 || 01 || 000e` (at most 32 KiB) is the single CAS head for an execution: `01` U tenant, `02` U execution identity, `03` B32 ScopeOpHash, `04` N successful resume ordinal, `05` N active window, `06` N active drain limit, `07` B32 current hold/source hash, `08` B32 active window-claim hash, `09` B32 window-history accumulator, `0a` N closed-window count, `0b` B32 last successful audit hash, `0c` B live-retry rows, `0d` B expiry-tombstone rows, and `0e` Q update UTC. Tag `0c` is `u32 count ||` rows sorted by ordinal, each `B32 stable request identity || B32 exact caller-carrier hash || N ordinal || B32 audit hash || N returned window || N returned drain limit || Q request expiry`. Tag `0d` is `u32 count ||` rows sorted by stable identity, each `B32 stable request identity || B32 exact caller-carrier hash || Q request expiry || Q delete-after UTC`. Live plus tombstone rows are at most 64. At request expiry a live row moves, in the same state CAS, to a tombstone whose delete-after is exactly 30 days later; audit and closure bodies may then be reclaimed. Hourly reconciliation deletes only an authenticated expired tombstone. A 65th distinct success is `resume_capacity_hold` with no mutation until the oldest tombstone's deterministic delete-after or operation erasure. Exact live retries remain answerable, exact retained-tombstone retries remain distinguishable as expired, and changed carrier bytes conflict throughout that bounded horizon. Callers must never reuse an idempotency key for the same resume handle; a genuinely new request always uses a new key.
 
 Before mutation, the ledger stages one new worst-case `resume-window` charge for every unresolved member's full next-window attempt evidence, new drain rows, the **next** drain-limit record and resolution, window claim/closure, state, audit, tombstone capacity, and hold/inventory evidence. It is at most 1 GiB and must fit tenant, tenant-pool, and deployment counters **in addition to** the unchanged old active charge. The staged charge binds the stable request identity and all three predecessor counters. The old charge remains authoritative until successor-state readback. Refusal writes no closure, audit, state, invocation, or partial counter and returns `resume_capacity_hold`.
 
-Charge ownership is a closed two-phase swap. Before successor readback, recovery finds no successor-state hash and CAS-releases only the staged generation, leaving the old active generation/counters exact. After successor readback, recovery CAS-activates the staged generation and releases/decrements the old generation exactly once. Generation and transfer-owner checks make repeated recovery a no-op; no point counts less than the old charge, admits above a ceiling, or releases either generation twice. Publication arms only after the finalized successor charge reads back.
+Charge ownership is a closed two-phase swap. Before a successful audit or successor readback, an authenticated proof that neither exists permits recovery to CAS-release only the staged generation, leaving the old active generation/counters exact. A read-back success audit instead makes its staged charge and recorded successor intent authoritative partial success: recovery retains that stage and completes the exact state/charge without another audit or ordinal. After successor readback, recovery CAS-activates the staged generation and releases/decrements the old generation exactly once. Generation and transfer-owner checks make repeated recovery a no-op; no point counts less than the old charge, admits above a ceiling, or releases either generation twice. Publication arms only after the finalized successor charge reads back.
 
 `HX-EV-PUBLICATION-RESUME-AUDIT-4\0 || 01 || 000a` (at most 4 KiB) is written only for a successful resume: `01` U tenant, `02` U execution identity, `03` N ordinal, `04` B32 stable request identity, `05` B32 exact caller-carrier hash, `06` B32 prior resume-state hash, `07` O(B32) window-closure hash, `08` N opened window (zero for legacy), `09` N new drain limit, and `0a` Q decision UTC. It never contains the successor state hash. Its create-once key is `publication-resume-audit:` plus lowercase-hex SHA-256(`"HX-EV-PUBLICATION-RESUME-AUDIT-KEY-2\0" || 01 || U tenant || U executionIdentity || B32 stableRequestIdentity`). The same stable identity therefore locates a live row or orphan audit without reconstructing a server timestamp or ordinal. Rejected requests create no durable record and consume no quota.
 
-The acyclic write order is: resolve stable identity against live/tombstone/audit evidence; read/authenticate prior state and exact partition; stage/read the successor charge; write/read any drain resolution and closure; compute the successor accumulator; construct/write/read the window claim from the **prior** state hash and stable identity; create/read the audit against the prior-state hash; CAS/read the successor state containing the window/audit hashes and compact retry row; finalize the charge swap; then remove inventory and arm publication. A crash before state CAS leaves an orphan audit and staged charge that only the byte-identical stable request may finish; recovery otherwise rolls the stage back. A crash after state readback deterministically finalizes before arming. State and audit never hash each other, and window and successor state never hash each other.
+The acyclic write order is: resolve stable identity against live/tombstone/audit evidence; read/authenticate prior state and exact partition; stage/read the successor charge; retain/read back the exact signed D9 request claim at its create-once address; write/read any drain resolution and closure; compute the successor accumulator; for retry exhaustion construct/write/read the window claim from the **prior** state hash and stable identity (drain-only reuses the exact existing claim); create/read the audit against the prior-state hash; CAS/read the successor state containing the window/audit hashes and compact retry row; finalize the charge swap; then remove inventory and arm publication. The existing bounded signed request claim supplies the exact expiry, resume handle, and source to orphan reconstruction; it is included in the staged reservation, retained through its live retry horizon, and erased with the operation. Before audit creation, recovery may roll back only with authenticated absence of both audit and successor. A crash after audit readback but before state CAS leaves a partial success: the byte-identical stable retry or authenticated hourly owner completes its recorded successor and charge, then returns the same canonical response. Changed carrier bytes cannot finish that success. A crash after state readback deterministically finalizes before arming. State and audit never hash each other, and window and successor state never hash each other. Orphan completion and hourly retry reconciliation use this same CAS head. New resume admission is fenced while an orphan exists; the only permitted intervening index changes are authenticated live-to-tombstone expiry and tombstone deletion. Recovery verifies the audit/owner and every protected prior-state field, recomputes these deterministic expiry changes from the protected prior rows and authenticated head update UTC/CAS receipt, and applies only the recorded success delta to the current head. It preserves current live/tombstone rows, inserts its own exact success row, and immediately tombstones that row if its recorded expiry already passed. CAS loss repeats against fresh authenticated evidence; arbitrary added/changed rows or altered source/carrier/receipt hold unchanged. Recovery never reinstalls the obsolete whole snapshot.
 
-For retry exhaustion, the transaction closes `w`, creates the successor window claim/state, and then arms only unresolved members. For a drain-limit-only hold it first writes D3's exact resolution linked to the old limit record and successor state, proves the window claim, committed roster, accepted set, unresolved set, and member bytes unchanged, leaves the window and member set unchanged, increases the limit by the original bounded drain reservation, and records the exact re-armed invocation hash before invoking it; if members are also retry-exhausted it does both. A later drain-limit is already included in the charge. Inventory removal follows successful state/readback; exhaustion again creates a fresh source/entry.
+For retry exhaustion, the transaction closes `w`, creates the successor window claim/state, and then arms only unresolved members. For a drain-limit-only hold it first writes D3's exact resolution linked to the old limit record and constructible successor invocation evidence, proves the window claim **bytes and hash**, committed roster, accepted set, unresolved set, and member bytes unchanged, leaves the window, closed-window count, and member set unchanged, increases the limit by the original bounded drain reservation, and records the exact re-armed invocation hash under that unchanged claim before invoking it; if members are also retry-exhausted it does both. A later drain-limit is already included in the charge. Inventory removal follows successful state/readback; exhaustion again creates a fresh source/entry.
 
 Ordinal, window, closed-window count, drain limit, and every charge addition use checked u64 arithmetic. An increment from `2^64-1`, a zero/overflowing drain increment, or a limit sum above `2^64-1` returns 409 `resume_arithmetic_exhausted`, leaves the old hold/state/charge authoritative, and arms nothing.
 
@@ -234,7 +244,7 @@ The closed endpoint outcomes are:
 
 | Result | HTTP and reason |
 | --- | --- |
-| success | 202 `{ resumeHandle, resumeOrdinal, window, drainLimit, auditRecordHash }` |
+| success | 202 `{ resumeHandle, resumeOrdinal, window, drainLimit, auditRecordHash }`; the bounded response uses canonical UTF-8 JSON with sorted names and no insignificant whitespace, so live/orphan retry returns byte-identical body bytes reconstructed from the retained claim and compact row/audit |
 | stale/ineligible/expired/idempotency conflict | 409 concurrency Problem Details with `resume_hold_changed`, `resume_not_eligible`, `resume_request_expired`, or `resume_request_conflict` |
 | checked ordinal/window/limit overflow | 409 concurrency Problem Details with `resume_arithmetic_exhausted` |
 | quota refusal | 503, `Retry-After: 30`, `resume_capacity_hold` |
@@ -243,13 +253,34 @@ The closed endpoint outcomes are:
 
 The route and legacy eligibility activate in slice 3 alongside BC-02; evidence-required eligibility activates in slice 4. State and charge erase with the tenant after every publication/rollback/incident obligation closes. Known answers `D45-request`, `D45-window`, `D45-closure`, `D45-state`, and `D45-audit` appear in D12.
 
+### D9.4 Persisted reconstruction and preparation lifecycle
+
+An execution permits one unresolved preparation. A8/D7 reserve one 2 MiB operational preparation slot in the old active charge before enabling resume; the next active charge includes that same slot. For legacy work D10's capsule reservation supplies this slot before drain cleanup or enabling its resume route. It covers the bounded origin, reconstruction images and progress head through recovery or retained retry expiry, independently of the staged next-window amount. The coordinator's `PublicationResumePreparationHold` is keyed by execution and stable request identity, uses reason `publication_resume_preparation_hold`, and remains in D11 inventory during preparation, cleanup or evidence failure. Exact retry, restart and hourly reconciliation activate it. Missing authority holds; authenticated completion or deletion/rollback readback removes it. The slot and all records erase with the execution/scope; no released stage leaves uncharged preparation records.
+
+The slot permits one full origin (256 KiB), one reconstruction (1 MiB), one progress head (8 KiB), at most 64 retained signed claims (4 KiB each) and at most 64 retained invocation records (4 KiB each): the total is at most 1,800 KiB, below 2 MiB. After authenticated successor/finalized-charge/invocation readback, a successful full origin/reconstruction can be deleted/read back while its compact signed claim and retry/invocation evidence retain the exact response/expiry. A rolled-back origin occupies the one full-origin slot through its original expiry plus 30 days; an exact unexpired retry may reuse it, but a different identity returns `resume_capacity_hold` without mutation until authenticated reclamation. Thus repeated failed caller keys cannot accumulate uncharged origins. Hourly authenticated deletion at the fixed deadline or scope erasure frees that slot. Compact claim/invocation reclamation follows the existing bounded live/tombstone horizon, not an extended deadline.
+
+`HX-EV-RESUME-ORIGIN-1\0 || 01 || 000a` (256 KiB) stores `01` U tenant, `02` U execution, `03` B32 stable identity, `04` B32 exact caller hash, `05` B exact carrier, `06` B exact signed request claim including its purpose-2d authentication, `07` B protected prior continuation image (128 KiB), `08` N staged charge generation, `09` Q original server UTC and `0a` Q original expiry. The stage admission and origin installation share the ledger's owner CAS. A lost acknowledgement reads this exact origin; no retry creates a new timestamp, expiry, signature or ordinal at the existing claim key. Changed carrier bytes conflict. If stage/origin admission never committed, authenticated absence permits a fresh preparation. An admitted origin survives an unaudited rollback until its original expiry plus 30 days, or operation erasure; its expiry never extends. Before expiry the same carrier may restage and reproduce the original claim bytes. At/after expiry it returns expired and can only complete already authoritative fenced/audited work.
+
+`HX-EV-RESUME-PREPARATION-1\0 || 01 || 000c` (1 MiB) stores `01` U tenant, `02` U execution, `03` B32 stable identity, `04` B32 caller hash, `05` B32 prior-image hash, `06` B prior continuation image (256 KiB), `07` B successor continuation intent (256 KiB), `08` Q original UTC, `09` Q original expiry, `0a` N staged amount, `0b` B import/artifact manifest (128 KiB) and `0c` N recorded successful ordinal. It is create-once after all actual required artifact readbacks and before audit. Images use canonical UTF-8 JSON without whitespace, with explicit tagged `bytes` (lowercase hex), `tuple`, `list`, `map` (pairs sorted by canonical encoded key, duplicate keys forbidden), and `scalar` (null/string/integer/boolean); decoder re-encoding must equal the original bytes. The closed continuation projection contains tenant, handle, hold source, ordinal, window, closed count, limit, current/next charge and ceiling, bounded live/tombstone rows and their exact response fields, last audit/count, active claim hash, pending invocation hashes (at most 64) and optional authenticated reconciliation evidence. It excludes process objects, orphan dictionaries and raw event bodies. The import manifest binds exact prior/successor roots for `roster`, `accepted`, `unresolved` and `window_claim_bytes`; imported member tuples are resolved from already charged immutable A8/outbox/pin authority for this execution and compared by position, MessageId and exact-byte hash. The manifest retains the actual new window claim (16 KiB) or unchanged drain-only claim. No image depends on the preparation's own hash; audit names only the original resume-state predecessor, and successor intent includes the already constructible audit/claim hashes.
+
+`HX-EV-RESUME-PREPARATION-HEAD-1\0 || 01 || 000a` (8 KiB) stores `01` U tenant, `02` U execution, `03` B32 owner identity, `04` B32 origin hash, `05` O(B32) reconstruction hash, `06` U phase (`admitted`, `writing`, `cleanup`, `audited`, `completed`, `rolled-back`, `evidence-hold`), `07` N generation, `08` B32 predecessor hash, `09` B progress manifest (4 KiB), and `0a` Q update UTC. The progress manifest is `u32 count ||` at most eight rows `U artifactKind || U framedAddress || B32 exactBytesHash || B32 authenticated readback/deletion receiptHash || U disposition`, with address at most 128 bytes, kind from claim/resolution/fence/closure/window/audit/state/invocation, and disposition pending/present/deleted. Every write intent is indexed before the corresponding create-once write; every readback advances this checked-generation CAS head. Restart enumerates the head through D11, authenticates origin, preparation, imports and provider receipts, then reconstructs protected predecessor and exact successor intent from these bytes alone. It rejects unavailable, changed, stale or mismatched reconstruction unchanged. It authenticates any intervening index CAS and composes only permitted expiry/deletion changes.
+
+The ordered preparation phases are stage+origin, claim, required resolution/fence/closure, window, reconstruction, audit, successor, finalized charge, invocation. Before audit/successor **and before any irreversible window fence or closure**, rollback requires typed authenticated absence receipts for the exact owner/generation on both audit and successor, then authenticated deletion/readback of every written artifact in the progress manifest. Missing or partial deletion holds with both charges unchanged; cleanup resumes from its retained manifest after restart. Only complete deletion authorizes once-only staged release. Origin/head remain discoverable under the reserved operational slot so an exact stable retry recovers original bytes. Once any fence/closure or successful audit is authoritative, rollback is forbidden: the coordinator retains charges and completes that original preparation, including the audit and recorded successor. It never removes a permanent C5 terminal/window fence or recreates accepted members. Unavailable presence/absence is a hold, never absence.
+
+Orphan completion uses authenticated **current** completion UTC, irrespective of a previous reconciliation timestamp. It inserts its own success row as live only before original expiry, as a tombstone through original expiry plus 30 days, and nowhere at/after deletion. It still completes the recorded success/charge once and returns the original canonical response; it neither resurrects nor extends a retry row. Successful live ordinals must be distinct and strictly increasing, in addition to distinct stable identities and bounds by the successful head. Drain invocation identity is SHA256(`"HX-EV-PUBLICATION-INVOCATION-1\0" || 01 || B32 unchangedWindowClaimHash || N successfulOrdinal || N newDrainLimit || B32 stableRequestIdentity || B32 unresolvedRoot`); its create-once key uses tenant, execution and that identity. Every invocation consumer uses this identity, so fresh drain resumes differ while exact retries reuse one invocation.
+
+`HX-EV-PUBLICATION-INVOCATION-1\0 || 01 || 0009` (4 KiB) records `01` U tenant, `02` U execution, `03` B32 unchanged/selected window-claim hash, `04` N successful ordinal, `05` N new drain limit, `06` B32 stable request identity, `07` B32 unresolved root, `08` B32 invocation identity computed above, and `09` Q original authorization UTC. The unresolved root is SHA256(`"HX-EV-PUBLICATION-UNRESOLVED-1\0" || 01 || u32 count ||` sorted rows `u32 position || U MessageId || B32 exact committed-member byte hash`). The D9.4 slot retains invocation bytes/readback until successor/invocation readback and the relevant retry horizon permit reclamation; staged reservation includes this 4 KiB ceiling. Its progress row binds the exact address/hash before create-once installation. Restart resolves the roster and excludes accepted members through the retained imports; changed identity, count, member or ordinal cannot arm publication. Slice 3 enables legacy invocation and slice 4 window invocation; scope erasure removes it with the operation. Every consumer resolves this record and finalized charge authority before dispatch. An exact retry reads the existing record and arms no second invocation.
+
+The progress intent's exact state digest and Q completion UTC fence successor installation. If a state write survives before acknowledgement, restart authenticates those original intended bytes and their provider readback, acknowledges them, and finalizes the original charge exactly once. It never regenerates later-UTC bytes at that pending intent or overwrites the manifest before rejecting contradictory evidence. After acknowledgement/finalization, the shared CAS composes current-time live expiry and fixed expiry-plus-30-day deletion against the authenticated successor, preserving unrelated authorized rows; state and manifest receipt links advance together. This reconciliation runs even for an already-present successor or finalized charge and reads back before publication arms or completion returns. Later UTC changes only the permitted retry indexes/update time, never the original claim, audit, response, authorization, invocation or expiry. Unavailable/contradictory reads are preflighted before mutation and preserve exact bytes/counters. Every later transferred charge generation, including released rollback and restaging, retains the original transfer owner; ordinary never-transferred charges retain absent owner.
+
 ## D10. Legacy status-6 resume — replacement for `[I-46]`
 
+
 ### D10.1 Resume capsule before cleanup
 
 From slice 3, no legacy drain evidence for a terminal status-6 execution is removed until the actor creates and durably reads back a bounded chunk set and its `HX-EV-LEGACY-RESUME-CAPSULE-2\0 || 01 || 0010` manifest (at most 128 KiB): `01` U tenant, `02` U domain, `03` U aggregate ID, `04` U tracking identity, `05` O(U) execution MessageId, `06` U correlation ID, `07` U command type, `08` U rejection classification (`success-events` or `rejection-events`), `09` N start sequence, `0a` N end sequence, `0b` I positive event count, `0c` B32 ordered stored-event root, `0d` B chunk manifest, `0e` U cleanup source (`drain-exhaustion` or `operator-reconciliation`), `0f` B32 exact source-record hash, and `10` Q creation UTC.
 
-`capsuleIdentity = SHA256("HX-EV-LEGACY-RESUME-CAPSULE-IDENTITY-1\0" || 01 || U tenant || U domain || U aggregateId || U trackingIdentity || B32 sourceRecordHash)`. Each `HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-1\0 || 01 || 0005` record (at most 64 KiB) has `01` B32 capsule identity, `02` N zero-based chunk ordinal, `03` N row count, `04` B concatenated rows, and `05` B32 chunk row root. Rows are ascending `N sequence || U stored MessageId || B32 StoredDigest`, with no nested count; tag `03` controls exact parsing. At the imported 1,024-byte MessageId maximum a row is 1,068 bytes and the fixed maximum-width chunk overhead is 132 bytes, so each chunk holds at most 61 rows. Every supported V1 range of at most 1,000 rows therefore uses at most 17 chunks.
+`capsuleIdentity = SHA256("HX-EV-LEGACY-RESUME-CAPSULE-IDENTITY-1\0" || 01 || U tenant || U domain || U aggregateId || U trackingIdentity || B32 sourceRecordHash)`. Each `HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-1\0 || 01 || 0005` record (at most 64 KiB) has `01` B32 capsule identity, `02` N zero-based chunk ordinal, `03` N row count, `04` B concatenated rows, and `05` B32 chunk row root. Rows are ascending `N sequence || U stored MessageId || B32 StoredDigest`, with no nested count; tag `03` controls exact parsing. At the imported 1,024-byte MessageId maximum a row is 1,068 bytes and the fixed chunk overhead is 128 bytes, so each chunk holds at most 61 rows. Every supported V1 range of at most 1,000 rows therefore uses at most 17 chunks.
 
 Capsule tag `0d` is `u32 chunkCount ||` 1..17 rows sorted by ordinal, each `N ordinal || N firstSequence || N rowCount || B32 exact chunk hash || N encoded chunk length || U resolvable chunk object key`. The chunk key is `legacy-resume-capsule-chunk:` plus lowercase-hex SHA-256(`"HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1\0" || 01 || B32 capsuleIdentity || N chunkOrdinal`); the capsule manifest key remains stable below. The ordered stored-event root is recomputed over `u32 totalRowCount ||` all exact member rows concatenated in chunk order: `SHA256("HX-EV-LEGACY-RESUME-EVENTS-2\0" || 01 || B(exact counted rows))`. Chunk lengths/counts/endpoints must exactly cover tags `09`..`0b`, with no gap, overlap, duplicate MessageId, missing chunk, or trailing bytes. The actor writes/reads all chunks first, then create-once writes/reads the manifest; any oversize or incomplete set fails cleanup and retains the drain record/reminder. A successful legacy drain needs no capsule and keeps its shipped cleanup.
 
@@ -271,29 +302,43 @@ A mismatch or unavailable read is `resume_evidence_hold`; missing/contradictory
 
 ### D11.1 Capture is mandatory and bounded
 
-Every addressed-delivery subscription has a verified `HX-EV-SUBSCRIPTION-DELIVERY-POLICY-3\0 || 01 || 000d` record (at most 16 KiB): `01` U deployment identity, `02` U component, `03` U topic, `04` U physical subscription ID, `05` N policy revision, `06` B32 predecessor policy hash (zero at revision 1), `07` U state (`active` or `superseded`), `08` U capture mode (`dead-letter-capture` or `direct-held-capture`), `09` O(U) dead-letter topic, `0a` N maximum live redeliveries `1..64`, `0b` B32 exact resolved subscription/resiliency configuration hash, `0c` U source (`dapr-configuration` or `broker-api`), and `0d` Q observation UTC. The create-once revision key and CAS head are specified in the address table below; only one contiguous `active` revision may be selected, and installing its successor first writes/read-backs the new revision, then CASes the head, then marks the predecessor superseded. A stale or rolled-back revision cannot authorize acknowledgement. An unbounded broker redelivery policy alone is not sufficient: after the bounded local attempts or 24 hours from first observation, whichever comes first, the consumer must durably capture/read back the exact carrier and held state before acknowledging that transport copy. A dead-letter topic's own subscription uses direct held capture and cannot dead-letter again.
+Every addressed-delivery subscription has a verified `HX-EV-SUBSCRIPTION-DELIVERY-POLICY-3\0 || 01 || 000d` record (at most 16 KiB): `01` U deployment identity, `02` U component, `03` U topic, `04` U physical subscription ID, `05` N policy revision, `06` B32 predecessor policy hash (zero at revision 1), `07` U relation (`initial` or `successor`), `08` U capture mode (`dead-letter-capture` or `direct-held-capture`), `09` O(U) dead-letter topic, `0a` N maximum live redeliveries `1..64`, `0b` B32 exact resolved subscription/resiliency configuration hash, `0c` U source (`dapr-configuration` or `broker-api`), and `0d` Q observation UTC. The create-once revision key and CAS head are specified in the address table below; only the contiguous revision selected by the head is active. Installing its successor first writes/read-backs the immutable `successor` revision and then CASes the head; the predecessor remains immutable and is logically superseded by that head. A stale or rolled-back revision cannot authorize acknowledgement. An unbounded broker redelivery policy alone is not sufficient: after the bounded local attempts or 24 hours from first observation, whichever comes first, the consumer must durably capture/read back the exact carrier and held state before acknowledging that transport copy. A dead-letter topic's own subscription uses direct held capture and cannot dead-letter again. The revision-address store is persistent and create-once: two differing successors at the same scope/revision conflict even before head CAS. Only the exact stored revision selected by the authenticated current head authorizes capture; exact lost-ack installation reads back the same revision/head, and stale or competing revisions never become authority.
 
 A first held observation creates/read-backs `HX-EV-HELD-DELIVERY-4\0 || 01 || 0016` (at most 32 KiB) **before** its attempt number or 24-hour clock is used: `01` U scope kind (`tenant` or `deployment`), `02` U deployment identity, `03` O(U) tenant, `04` O(U) MessageId, `05` U component, `06` U topic, `07` U physical subscription ID, `08` B32 active subscription-policy hash, `09` U state (`observed`, `captured`, `redriving`, `incident`, or `closed`), `0a` U reason, `0b` N observed carrier length, `0c` B32 exact carrier hash, `0d` O(U) retained backend ID, `0e` O(U) resolvable retained object key, `0f` O(B32) authenticated object readback receipt hash, `10` O(B32) committed handoff hash, `11` Q first-observed UTC, `12` N delivery-attempt count, `13` N redrive count, `14` O(B32) last redrive-error evidence hash, `15` Q next re-evaluation UTC, and `16` N entry revision. `scopeKind=tenant` requires tenant present; `scopeKind=deployment` requires tenant absent. Every other pairing is a decode/admission incident with no acknowledgement or charge mutation.
 
 Its key is `held-delivery:` plus lowercase-hex SHA-256(`"HX-EV-HELD-DELIVERY-KEY-2\0" || 01 || U scopeKind || U deploymentIdentity || O(U tenant) || U component || U topic || U physicalSubscriptionId || B32 exactCarrierHash`). Reuse of a physical subscription ID or identical bytes in another deployment, scope, component, or topic cannot cross-link authority. Every delivery CAS-increments tag `12` and the record revision; restart cannot reset either boundary. Locator fields are absent only in `observed`/`incident`, present together in `captured`/`redriving`/`closed`, and bounded to 1,024-byte backend ID plus 4,096-byte object key. Hashes alone are never restart redrive authority.
 
-The record is charged 32 KiB before the first nonterminal response. For an ordinary carrier, D7 also stages and activates a `retained-object` charge for the exact carrier length plus recorded overhead against the same tenant or deployment capture scope before object write; `captured` is legal only after the exact object, locator, readback authority, held entry, and active charge all read back. Oversize uses its distinct quarantine charge. Therefore no physical-copy acknowledgement can strand uncharged bytes. The captured transition is an explicit authenticated terminal handoff for the **physical transport copy**, so the consumer may acknowledge that copy; it writes no route success/effect/filter result, and all original logical route/handoff obligations remain open until ordinary terminal decisions. This amends 6.5c C4 without weakening its success rule.
+The record is charged 32 KiB before the first nonterminal response. For an ordinary carrier, D7 also stages and activates a `retained-object` charge for the exact carrier length plus recorded overhead against the same tenant or deployment capture scope before object write; `captured` is legal only after the exact object, locator, readback authority, held entry, and active charge all read back. Oversize uses its distinct quarantine charge. Therefore no physical-copy acknowledgement can strand uncharged bytes. The captured transition is an explicit authenticated terminal handoff for the **physical transport copy**, so the consumer may acknowledge that copy; it writes no route success/effect/filter result, and all original logical route/handoff obligations remain open until ordinary terminal decisions. This amends 6.5c C4 without weakening its success rule. Capture authenticates the existing 32 KiB metadata charge's scope/account/owner/generation and the reserved D11 inventory slot against exact current readback, then the immutable retained object, locator and active object charge. A numeric charged-bytes assertion is insufficient; missing/stale/wrong-owner metadata or inventory rejects unchanged. The ordinary object locator is backend `held-delivery-store` with object key `held/` plus the lowercase hex held-delivery key hash, so the full scope and carrier identity own separate objects and charges; create-once rejects changed bytes at any existing locator. Exact retry rereads all original authority and adds no charge.
 
 Permanently nonadmissible or invalid-header carriers use `HX-EV-CARRIER-QUARANTINE-2\0 || 01 || 000e` (at most 128 KiB): `01` U scope kind, `02` O(U) tenant, `03` U physical subscription ID, `04` U reason (`invalid-header-value`, `invalid-carrier`, or `oversize-carrier`), `05` N exact body length, `06` B32 body hash, `07` B header manifest containing only names, value lengths, and value hashes for forbidden values, `08` U retained backend ID, `09` U resolvable retained object key, `0a` B32 provider archive/readback authority hash, `0b` O(U) parsed MessageId, `0c` Q captured UTC, `0d` U disposition (`terminal-quarantine`), and `0e` B32 exact source-delivery receipt hash. No raw forbidden header value enters the record. The manifest is `u32 count ||` at most 128 rows `U headerName || N valueLength || B32 valueHash`; with the imported 64 KiB aggregate header-name/value maximum its exact worst case is `4 + 65,536 + 128*(4+8+32) = 71,172` bytes, safely within the 128 KiB record cap even with maximum surrounding identifiers.
 
 The ordinary path is maximum-inclusive: complete carriers `<= 193 MiB` use ordinary retained capture. Oversize quarantine is the disjoint interval `193 MiB < length <= maximumQuarantinedCarrierBytes` (at most 256 MiB) and may acknowledge only after a broker/provider atomically archives its **exact** bytes under an authenticated non-expiring quarantine object and D7 charges kind `oversize-quarantine`. Provider readiness pre-rejects anything it cannot capture. A delivered object strictly above the advertised maximum creates the bounded charged D11 held record in `incident` state with reason `delivery_above_advertised_max`, exact streamed length/hash, Operations owner, hourly re-evaluation, and a D11 inventory entry before a repeated delivery can become invisible; it remains unacknowledged and exits only after provider configuration pre-rejects it and the broker proves no live copy, or after a later approved capture capability stores the exact bytes. Valid EventStore carriers cannot use the oversize exception.
 
-`HX-EV-REDRIVE-REQUEST-2\0 || 01 || 0007`, signed under purpose `2d`, has `01` U operator-action issuer, `02` U scope kind, `03` O(U) tenant, `04` B32 held-delivery key hash, `05` N expected redrive count, `06` U operator subject, and `07` Q request UTC. Routes are unambiguous: tenant entries use `POST /api/v1/admin/held-deliveries/tenants/{tenantId}/{entryKey}/redrive`; deployment entries use `POST /api/v1/admin/held-deliveries/deployment/{entryKey}/redrive` and Admin policy. Automatic redrive runs when cause-clearing evidence appears and otherwise with exponential backoff from 60 seconds to 15 minutes. It injects exact retained bytes/header image into the same authenticated ingress. Entry into `redriving` increments the count and binds the attempt. Terminal route decisions close the entry and refund after deletion readback. A nonterminal transport/ingress failure CASes `redriving -> captured`, preserves the retained object and charge, stores the typed error-evidence hash, and schedules the next bounded retry; restart resumes from that durable state. No failed redrive may remain indefinitely in `redriving`. A terminal quarantine is never redriven.
+`HX-EV-REDRIVE-REQUEST-2\0 || 01 || 0007` (3 KiB), signed under existing purpose `2d`, retains the existing exact seven fields: `01` U operator-action issuer, `02` U scope kind, `03` O(U) tenant, `04` B32 held-delivery key hash, `05` N expected redrive count, `06` U operator subject, and `07` Q request UTC. Its field-derived maximum is 2,413 bytes: 25 header bytes, seven tags, issuer 1,028, scope 14, optional tenant 1,029, held key 32, count eight, subject 260 and UTC eight. Authenticate the authorized issuer and operator subject under the existing Admin/Operator policy, purpose `2d` signature, exact scope/tenant/held key, and UTC no earlier than first observation before admission; an automatic action supplies the same authenticated server authority, not unsigned synthetic request bytes. Tag `05` must equal the current authenticated held count, which the transaction checked-increments. Keep the exact original signed bytes/UTC on restart; do not regenerate a pending request from current time. The model's fixed `admin`/`operator` identities and purpose-specific hash receipts are authorization fixtures only, not a production signature algorithm or provider proof. Routes are unambiguous: tenant entries use `POST /api/v1/admin/held-deliveries/tenants/{tenantId}/{entryKey}/redrive`; deployment entries use `POST /api/v1/admin/held-deliveries/deployment/{entryKey}/redrive` and Admin policy. Automatic redrive runs when cause-clearing evidence appears and otherwise with exponential backoff from 60 seconds to 15 minutes. It injects exact retained bytes/header image into the same authenticated ingress. Entry into `redriving` increments the count and binds the exact retained request/attempt. Terminal route decisions close the entry and refund after deletion readback. A nonterminal transport/ingress failure CASes `redriving -> captured`, preserves the retained object and charges, stores the typed error-evidence hash, and schedules the next bounded retry; restart resumes from that durable state. No failed redrive may remain indefinitely in `redriving`. A terminal quarantine is never redriven. A crash in `redriving` is reconciled on restart and hourly: freshly authenticate the persisted count, held identity, exact carrier/locator, entry CAS receipt and addressed signed request/attempt; terminal route readback closes that exact pair, otherwise unknown/unavailable completion returns it to scheduled `captured` with typed error evidence and the same count/carrier/charges. Corrupt or absent request/attempt evidence returns to an indexed captured evidence hold at the 15-minute maximum delay; it cannot authorize a send or completion until repaired. Every such attempt leaves redriving on its first bounded reconciliation. The fixed-slot lifecycle below bounds actual request and attempt bodies, not only error history.
 
 Policy evidence activates before binary publication in slice 4; continuation/redrive activates with the binary carrier. Tenant records erase with the tenant; deployment captures erase only with their capture scope after all obligations close. Known answers `D36-policy`, `D36-held`, `D36-quarantine`, and `D36-redrive` appear in D12.
 
 The closed held-delivery reasons are `handler-capability-hold`, `raw-source-unavailable`, `delivery-carrier-limit-hold`, `invalid-header-value`, `invalid-carrier`, `oversize-carrier`, and `delivery_above_advertised_max`. Their owner is `operations`; capture-capable reasons exit by exact-byte capture then redrive, permanently invalid reasons exit by terminal quarantine, and above-maximum exits only as specified above. Unknown reasons fail decoding and cannot be acknowledged.
 
+Capture preparation is `HX-EV-CAPTURE-PREPARATION-1\0 || 01 || 0009` (8 KiB): `01` B32 held-key hash, `02` B32 exact observed predecessor hash, `03` B32 metadata-charge readback receipt hash, `04` B32 inventory reservation receipt hash, `05` U retained backend, `06` U resolvable object key, `07` B32 exact carrier/header-image hash, `08` N exact canonical length and `09` Q first-observed UTC. Address is `K("HX-EV-CAPTURE-PREPARATION-KEY-1\0", B32 heldKeyHash)`, create-once. D11's 32 KiB metadata reservation covers this 8 KiB preparation plus the field-derived held-record maximum (less than 12 KiB); inventory capacity is separately pre-reserved. Write/read the preparation under the observed predecessor fence before retained-object installation. Its active D7 object charge, immutable object and receipt remain discoverable through the still-observed hold until the held-state CAS finishes. A retry of matching partial work authenticates preparation/predecessor, metadata, inventory, exact object/locator/readback and charge, then completes captured state once without a new reservation or charge. Charge-only partial work installs/readbacks the exact missing object; object+charge partial work only completes the held CAS/readback. An already captured lost acknowledgement rereads the same complete authority. Changed/missing/unavailable evidence holds unchanged and unacknowledged; Operations reconciles hourly. Before captured authority exists, rollback requires deletion/readback of the exact prepared object and proved absent captured successor, then releases only that object's charge; partial/unavailable cleanup stays indexed and charged. The preparation erases only with verified held/object deletion or whole-scope erasure. The ordinary capture transition checks `length <= 193 MiB` before any object reservation/write; it cannot acknowledge the oversize interval without the separately required provider-quarantine receipt, nor above the advertised maximum without the incident path.
+
+Before **every** redrive, Operations freshly reads the retained object and active charge, verifies exact bytes/header image, length/hash, backend/key, present capture preparation and provider receipt against the persisted held identity, and authenticates all four metadata/inventory/repair-charge/repair-interest obligations. It then persists the exact signed request/attempt and increments count before sending. Missing, changed, stale, wrong account/state/receipt or unavailable authority sends nothing and leaves count and ledger unchanged, including when all obligations are absent. A corrupt request/attempt creates `HX-EV-REDRIVE-REPAIR-1\0 || 01 || 000a` (8 KiB): `01` B32 held-key hash, `02` N failed attempt count, `03` B32 disputed attempt-image hash, `04` B32 carrier hash, `05` U locator (4,096 bytes), `06` N repair generation, `07` B32 predecessor repair hash (zero at generation 1), `08` U state (`required` or `repaired`), `09` O(B32) authenticated repair receipt (present exactly when repaired), and `0a` Q update UTC. Address is `K("HX-EV-REDRIVE-REPAIR-KEY-1\0", B32 heldKeyHash, N attemptCount)`, CAS generation. One 32 KiB side-record reservation covers the finite signed-request/attempt/repair authority described below, with its separate prerequisite inventory slot pre-reserved during capture before acknowledgement; refusal leaves the physical copy unacknowledged. The same scope inventory actor exposes a separate `RedriveEvidenceRepairHold` with reason `redrive-evidence-repair-hold` and activates Operations hourly/at cause change. The original HeldDelivery carrier reason remains unchanged. There is at most one repair prerequisite per held carrier: before a next attempt can require that slot, the repaired predecessor record/entry must have authenticated deletion readback. Partial cleanup remains indexed and charged, and cannot permit another send.
+
+Reconciliation leaves redriving for bounded captured retry and persists required repair before another send is possible. Both automatic and manual requests check that record after restart. Only authenticated repaired readback of the exact original request/attempt, count, retained carrier, locator and active charge may CAS `required -> repaired`; its generation/predecessor and repair receipt bind that original evidence. After authenticated cleanup readback it permits one next count/send; exact retry of the still-redriving attempt cannot increment again. Missing or forged repair stays required and scheduled at the 15-minute maximum. Terminal route readback closes the exact attempt; erasure/refund follows ordinary deletion readback. The repair slot/record activate with slice-4 redrive and erase with the held/capture scope. Unknown/unavailable completion retains the ordinary captured retry, error/count/backoff and exact carrier authority; it does not assert route success.
+
 ### D11.2 Collision-free durable hold inventory
 
+The capture origin is `HX-EV-CAPTURE-ORIGIN-1\0 || 01 || 0006` (8 KiB): `01` B32 held key, `02` B exact canonical observed-predecessor image (at most 7 KiB), `03` B32 image hash, `04` Q first-observed UTC, `05` N original observation count and `06` B32 selected policy hash. Its create-once address is `K("HX-EV-CAPTURE-ORIGIN-KEY-1\0", B32 heldKey)`. D11's existing 32 KiB metadata charge covers this origin, the 8 KiB preparation and the field-derived held record below 12 KiB. Unsupported image size holds before object admission/acknowledgement. The origin preserves exact scope/account/carrier, first UTC, metadata, inventory and selected-policy authority. Its canonical image also contains checked observation revision/count and their authenticated provider observation receipt. Subsequent normal observations retain all protected fields, advance count/revision monotonically, and carry a fresh provider readback. Recovery authenticates the original persisted image and preparation, then composes only these observation changes into captured state. One or multiple observations after either charge-only or object-plus-charge crash cannot require changed create-once preparation bytes, reset the clock/count, or allocate a second object/charge. A changed selected policy cannot replace a partial capture's original policy binding. Forged observation receipts, changed protected fields and unavailable source authority hold unchanged. Origin/preparation remain discoverable under the original inventory interest and erase only after exact held/object deletion or whole-scope erasure; slice 4 activates them.
+
+Capture preflights both inventory interests: the original held entry and a separate reserved `RedriveEvidenceRepairHold` interest owned by the same held key at the same authenticated generation. A charge alone cannot reserve that second slot. One remaining total slot permits observation but refuses capture; two slots permit it. Both interest receipts and metadata/repair/object charge scope, account, owner, generation, amount and active state authenticate before acknowledgement, exact retry, partial completion or any redrive. Repair interest address is `K("HX-EV-REDRIVE-REPAIR-INTEREST-KEY-1\0", B32 heldKey)`, under the same bounded inventory CAS. Refusal preflights capacity/counters and leaves no leaked interest or charge; unaudited rollback deletes/readbacks partial object/origin/preparation and releases their exact object/repair charges and second interest, preserving original metadata/interest. Partial cleanup remains charged and discoverable. Required capture preparation must be present on both held and provider sides and byte-identical; two absent values are never authority.
+
+Exactly one active/disputed signed request and one attempt are retained per held key, at fixed addresses `K("HX-EV-REDRIVE-REQUEST-KEY-1\0", B32 heldKey)` and `K("HX-EV-REDRIVE-ATTEMPT-KEY-1\0", B32 heldKey)`; neither is a count-addressed append log. `HX-EV-REDRIVE-ATTEMPT-1\0 || 01 || 0007` (8 KiB) stores `01` B32 held key, `02` N checked positive count, `03` B32 carrier hash, `04` U locator (4,096 bytes), `05` B32 metadata receipt, `06` Q first-observed UTC and `07` B32 SHA-256 of the exact signed-request payload. Provider readback authenticates both exact rows, the existing request signature and count-to-request binding. Before advancing, authenticate the exact predecessor pair/count, delete both and authenticate their deletion readbacks, then replace them with the successor pair together with the checked held-count CAS. This is one required serializable provider transaction: staged readback/deletion failures or a precommit crash abort with no row/count/charge changes; a committed lost acknowledgement restarts from both exact rows and the committed held count, sends no invented successor and reconciles that attempt once. Readiness must prove this transaction on the actual backend before activation; detached dictionaries only model it. Exactly one compact authenticated deletion receipt per fixed slot replaces its predecessor. A stale original request is fenced by its signed expected count after reclamation. Disputed original request/attempt rows remain available through repair and all four cleanup boundaries; absent/corrupt/unavailable readback or reclamation blocks admission without send or count/ledger mutation. The 32 KiB side reservation covers the maximum staged overlap, not just committed rows: two request payloads at most 3 KiB each, two attempts each below 5 KiB (field-derived maximum 4,278 bytes within the 8 KiB family cap), the fixed-purpose repair record below 6 KiB and prerequisite entry below 2 KiB total at most 24 KiB; the remaining 8 KiB bounds complete signature envelopes, native readback/deletion receipts and other transaction-support images together. A provider exceeding either this auxiliary ceiling or any record cap fails readiness/admission before capture acknowledgement. Charge stays constant across more than 130 genuine failed sends/restarts while both actual row dictionaries and bytes remain bounded. The pair/receipts activate with redrive, remain original-interest discoverable and erase with terminal held/object deletion or whole-scope erasure; refund follows authenticated deletion of every request, attempt and receipt as well as the held/capture authority.
+
+Authenticated `required -> repaired` remains indexed and charged until both exact repaired record and separate typed prerequisite entry have deletion readback. `HX-EV-REDRIVE-CLEANUP-1\0 || 01 || 0007` (1 KiB, within metadata reserve) stores `01` B32 held key, `02` N disputed count, `03` B32 repaired-record hash, `04` B32 exact prerequisite-entry hash, `05` U phase (`repaired-readback`, `record-deleted`, `record-readback`, `entry-deleted`), `06` O(B32) record deletion receipt and `07` O(B32) entry deletion receipt. Its fixed checked-CAS address is `K("HX-EV-REDRIVE-CLEANUP-KEY-1\0", B32 heldKey)`. A record receipt is present after record deletion; the entry receipt only after entry deletion. Authenticate phase/hash/provider receipts across restart and each write/delete/readback. Missing/unavailable cleanup leaves the same charge/index and blocks automatic/manual sends and reuse of the single repair interest. Completed cleanup retains only a compact authenticated receipt/count fence, removes the prerequisite, and permits one next attempt. A second corruption uses the same bounded slots after that fence, preserving original HeldDelivery reason and genuine repair receipt. Terminal erasure authenticates route completion, all retained obligations and cleanup, deletes/readbacks every origin/preparation/object/attempt/entry/receipt, refunds exactly once and removes both interests; offboarding retains D1's no-success-by-erasure rule.
+
 Inventory scope is explicit. Tenant actor ID is `tenant:` plus lowercase-hex SHA-256(`U tenant`); deployment actor ID is `deployment:` plus lowercase-hex SHA-256(`U deployment identity`). A legal tenant string `deployment` therefore cannot collide. Tenant reads use `GET /api/v1/admin/holds/tenants/{tenantId}` with tenant authorization; deployment reads use `GET /api/v1/admin/holds/deployment` with Admin policy.
 
-Every D1 predicate hold has `HX-EV-HOLD-ENTRY-2\0 || 01 || 000d` (at most 4 KiB): `01` U scope kind, `02` U scope ID, `03` U hold code, `04` U stable subject key, `05` O(U) domain, `06` U current reason code, `07` N entry revision, `08` B32 predecessor entry hash (zero at revision 1), `09` Q first observed UTC, `0a` Q last observed UTC, `0b` N observation count, `0c` U owner kind (the closed D1 set), and `0d` Q next re-evaluation UTC. Key is `hold-entry:` plus lowercase-hex SHA-256(`"HX-EV-HOLD-ENTRY-KEY-1\0" || 01 || U scope kind || U scope ID || U hold code || U subject key`). A cause/reason change CAS-writes the next revision before changing the ordered index; stale tag `06` cannot survive. Resolution CAS-removes the index row only after its named evidence reads back.
+Every D1 predicate hold has `HX-EV-HOLD-ENTRY-2\0 || 01 || 000d` (at most 8 KiB, charged at that ceiling): `01` U scope kind, `02` U scope ID, `03` U hold code, `04` U stable subject key (1..4,096 UTF-8 bytes), `05` O(U) domain, `06` U current reason code, `07` N entry revision, `08` B32 predecessor entry hash (zero at revision 1), `09` Q first observed UTC, `0a` Q last observed UTC, `0b` N observation count, `0c` U owner kind (the closed D1 set), and `0d` Q next re-evaluation UTC. Key is `hold-entry:` plus lowercase-hex SHA-256(`"HX-EV-HOLD-ENTRY-KEY-1\0" || 01 || U scope kind || U scope ID || U hold code || U subject key`). A cause/reason change CAS-writes the next revision before changing the ordered index; stale tag `06` cannot survive. Resolution CAS-removes the index row only after its named evidence reads back.
 
 `HX-EV-HOLD-INDEX-2\0 || 01 || 0008` (at most 40 MiB) has `01` U scope kind, `02` U scope ID, `03` N generation, `04` N entry count (at most 10,000), `05` B rows sorted by `(firstObservedUtc, holdCode, subjectKey)` with no nested count (`Q firstObservedUtc || U holdCode || U subjectKey || B32 current entry hash`), `06` N overflow count, `07` B32 predecessor index hash, and `08` Q update UTC. Tag `04` controls exact row parsing. For new capabilities, overflow must remain zero: command operations reserve an inventory slot before commit, tenant onboarding reserves a tenant actor/directory slot, and delivery capture reserves before acknowledgement. An inability to reserve stops that earlier boundary. Nonzero imported overflow is a visible deployment readiness hold until migrated; it never represents permission to hide a durable held operation.
 
@@ -317,17 +362,21 @@ For this candidate, `K(name, fields...)` means `prefix || lowercase-hex SHA256(A
 | retention capability / charge / counter | capability revision `K("HX-EV-PUBLICATION-CAPABILITY-KEY-1\0", U deployment, N revision)` plus deployment head; charge CAS head `K("HX-EV-PUBLICATION-CHARGE-KEY-1\0", U deployment, U accountKind, U accountId, B32 objectKeyHash)`; counter CAS head `K("HX-EV-PUBLICATION-COUNTER-KEY-1\0", U deployment, U counterKind, U counterId)` |
 | pin-batch reservation | `K("HX-EV-PIN-BATCH-RESERVATION-KEY-1\0", B32 ScopeOpHash, B32 candidateBatchRoot)`, CAS generation; changed candidate root is a new reservation only after the old generation is released |
 | wait / queue and allocator | wait `K("HX-EV-PIN-CAPACITY-WAIT-KEY-1\0", U deployment, B32 stableCapacitySubject)` CAS generation; queue `K("HX-EV-PIN-CAPACITY-QUEUE-KEY-1\0", U deployment, U counterId)` CAS generation. The global allocator is tag `09` of the deployment queue at counter ID `deployment`, not an unaddressed side counter. |
-| resume state / window / closure / audit | state and live/tombstone index `K("HX-EV-PUBLICATION-RESUME-STATE-KEY-1\0", U tenant, U executionIdentity)` CAS; window `K("HX-EV-PUBLICATION-WINDOW-KEY-1\0", B32 ScopeOpHash, N window)` create-once; closure key as D9.2; audit key as D9.3 |
+| resume claim / state / window / closure / audit | exact signed request claim `K("HX-EV-PUBLICATION-RESUME-CLAIM-KEY-1\0", U tenant, U executionIdentity, B32 stableRequestIdentity)` create-once; state and live/tombstone index `K("HX-EV-PUBLICATION-RESUME-STATE-KEY-1\0", U tenant, U executionIdentity)` CAS; window `K("HX-EV-PUBLICATION-WINDOW-KEY-1\0", B32 ScopeOpHash, N window)` create-once; closure key as D9.2; audit key as D9.3 |
+| resume origin / reconstruction / progress / invocation | origin `K("HX-EV-RESUME-ORIGIN-KEY-1\0", U tenant, U executionIdentity, B32 stableRequestIdentity)` create-once; reconstruction `K("HX-EV-RESUME-PREPARATION-KEY-1\0", U tenant, U executionIdentity, B32 stableRequestIdentity)` create-once; progress `K("HX-EV-RESUME-PREPARATION-HEAD-KEY-1\0", U tenant, U executionIdentity)` checked CAS generation; invocation `K("HX-EV-PUBLICATION-INVOCATION-KEY-1\0", U tenant, U executionIdentity, B32 invocationIdentity)` create-once. Every row uses D9.4 ownership/cleanup authority. |
+| complete window attempts | `K("HX-EV-WINDOW-ATTEMPT-SET-KEY-1\0", U tenant, B32 ScopeOpHash, N window)` create-once sealed set; pre-seal collection remains in charged imported C2 generation-fenced records. |
 | legacy capsule chunks / manifest / recovery | chunk and manifest keys as D10.1; recovery CAS head as D10.2. A later exhaustion advances recovery, not either create-once capsule address. |
 | subscription policy | revision `K("HX-EV-SUBSCRIPTION-POLICY-KEY-1\0", U deployment, U component, U topic, U physicalSubscriptionId, N revision)` and CAS head with the same fields excluding revision |
-| held delivery / quarantine / redrive | held CAS key as D11.1; quarantine create-once `K("HX-EV-CARRIER-QUARANTINE-KEY-1\0", B32 heldDeliveryKeyHash, B32 carrierHash)`; redrive request create-once `K("HX-EV-REDRIVE-REQUEST-KEY-1\0", B32 heldDeliveryKeyHash, N expectedRedriveCount)` |
+| held delivery / quarantine / redrive | held CAS key as D11.1; quarantine create-once `K("HX-EV-CARRIER-QUARANTINE-KEY-1\0", B32 heldDeliveryKeyHash, B32 carrierHash)`; signed request fixed checked-CAS slot `K("HX-EV-REDRIVE-REQUEST-KEY-1\0", B32 heldDeliveryKeyHash)`, replaced only by authenticated predecessor-pair reclamation with the held-count fence |
+| capture origin / preparation | origin `K("HX-EV-CAPTURE-ORIGIN-KEY-1\0", B32 heldDeliveryKeyHash)` and preparation `K("HX-EV-CAPTURE-PREPARATION-KEY-1\0", B32 heldDeliveryKeyHash)` create-once; D11.2 owns the original inventory interest, authenticated rollback and terminal erasure. |
+| redrive current attempt / repair / cleanup | current attempt `K("HX-EV-REDRIVE-ATTEMPT-KEY-1\0", B32 heldDeliveryKeyHash)` is one fixed replaceable slot, only after authenticated receipt/count and predecessor deletion readback; repair `K("HX-EV-REDRIVE-REPAIR-KEY-1\0", B32 heldDeliveryKeyHash, N attemptCount)` checked CAS generation; cleanup `K("HX-EV-REDRIVE-CLEANUP-KEY-1\0", B32 heldDeliveryKeyHash)` checked phase CAS over the exact predecessor bytes. The separately reserved repair inventory interest discovers the repair and typed prerequisite; D11.2 owns exact deletion readback and erasure. |
 | hold entry / index / directory | entry key as D11.2; index CAS head `K("HX-EV-HOLD-INDEX-KEY-1\0", U scopeKind, U scopeId)`; directory CAS head `K("HX-EV-HOLD-DIRECTORY-KEY-1\0", U deployment, N shard)` |
 
 All CAS families reject a missing, stale, or wrong-kind predecessor without side effects. All create-once families reject changed bytes. D12 fixes representative key vectors spanning activation, drain, resume closure/audit, and legacy manifest/recovery; their framing rule is the same rule used by every row above.
 
 ## D12. Known answers and executable verification
 
-All fixtures use tenant `t`, domain `d`, aggregate `a`, execution `op`, UTC ticks `638712864000000000`, and zero bytes for genesis predecessors. Ordinary opaque references hash displayed fixture bytes. Candidate-batch roots, stored-event roots, window attempt roots, history accumulators, capsule hashes, audit hashes, and retry rows are recomputed from their semantic inputs in dependency order; no unrelated label hash substitutes for them. They are local framing answers, not provider evidence. The destination JSON is exactly `{"component":"pubsub","metadata":{},"schema":"hexalith.eventstore.destination/1","topic":"orders"}`. This table has exactly 30 record/codec answers; the following key table separately fixes six framed address derivations.
+The original fixtures use tenant `t`, domain `d`, aggregate `a`, execution `op`, UTC ticks `638712864000000000`, and zero bytes for genesis predecessors. Supplementary resume fixtures use the explicit model times and handle below. Ordinary opaque references hash displayed fixture bytes. Candidate-batch roots, stored-event roots, window attempt roots, history accumulators, capsule hashes, audit hashes, and retry rows are recomputed from their semantic inputs in dependency order; no unrelated label hash substitutes for them. They are local framing answers, not provider evidence. The destination JSON is exactly `{"component":"pubsub","metadata":{},"schema":"hexalith.eventstore.destination/1","topic":"orders"}`. The first table preserves exactly 30 original record/codec answers; the supplementary table adds twelve answers and the key table fixes six framed address derivations.
 
 ```text
 D06-activation 225 ce6ece552e0e0c6220e13f3bfc15215d4995299bba0d2259844b55dd7c557605
@@ -345,39 +394,58 @@ D29-counter 134 eca227f547122e041a159affbac58bf7b57375bf91d78876d88cd73f418d1fc8
 D29-pin-batch 330 82e7a4965c94f3be0092cbcb8adb20b8ea560ef13419c88a036f5dc2d62c4517
 D31-wait 238 7dca50b2d1f448628a84ee423ab3aa364600a320ed10886711cc9c128bc4f3b8
 D31-queue 218 1c3c2b7655f178df7ddd09d33f4c64c66b5220ab0b6831abc088e5371b978584
-D36-policy 256 2b60c1df10f1968c8c6e5afbd50cf8dab7b2c7c1b7f067ce8a8157496a26e983
+D36-policy 257 9989075b380937e91cf9e98b27670f195b58aaeb326f9e90e54c8fe6158f6a88
 D36-held 371 812399ecf7f18d8ca0c39efeff73d6d6a52b682416c5a14174334a96c62a1675
 D36-quarantine 336 5dcce10f015b54bb853a3beb633b865b87889685771beeeb603d351559536257
 D36-redrive 119 1a552c0f005efc61b9d74682a5f707925c0b47b5e31d559bdb0b43da43f8b49f
-D37-entry 219 30ca7e011c4e0cbe3d2bdc4e1e66f598d44879f54f38629c3a3e50e4e7d9716f
+D37-entry 225 bf5562b4dee2cb51dc8a3cdb511b4e7ceb9bfc6a420603419f370a62c7c9e59f
 D37-index 196 a14e6b24af414ad169f98ba8829f971635724de0acc686a901024ebeec6967fc
 D37-directory 184 d36d068c6a1cea553949c29cafc628cbde9881e712a190d26033211f26b19569
 D37-key 58 eabf14e49beb9484895f4233927107604705ea58aac8d049e34db91ce7152978
-D45-request 329 207bfdb0cfe95b3eb41158eb38e77a5c40d0d089ad08ce34ec0bd757cddd22b0
-D45-window 318 e530b0097f279d297c79160eb166cf44d06f5855058837088e773c4a2b7b7e7b
-D45-closure 327 9aa8f4c113926a097669a37f76876c4836b7f1113d65a5389b809cbfb9ab3976
-D45-state 405 20533a3edc199970c7c0108d2fccd4c07c6dd0f87610aa32c54a122c6acf8a0a
-D45-audit 218 a24a2940f578339336cd45a15f9a6a26090ba9baebad18285026d6886429cc20
-D46-capsule 338 4f8b6f1fdb402ac61d457571811506f28b612897bd2738e0e7091da0fa8e7d8c
-D46-recovery 257 0ed58bb5acb8f735b937768a593da7ac2deb9385b2868af33e5fe2e947e14944
+D45-request 329 17cef46646c6320df270e39b85a6ab05cf568d798de842b5f5f64bf83caa9e50
+D45-window 318 573c53d0e7b7511bb5e131b33280e5a5f8e96138ddab0dce1bcc0b380b2ea9df
+D45-closure 331 bcd6e3ea2d4ded2afc8a7bad0d9a5e4d6414955df40a9ae500d5e262bdb3e2d1
+D45-state 405 76e878e69f91b644f7c11596d78d0031913ecc6011730a5d553b133c8083089c
+D45-audit 218 65a4c355269c0fea578bd2a74522261385e9e0b3c7169c8fd8c3557a85768429
+D46-capsule 409 272a728d7d53a0bdfe3c97556e9f17561bab352c3ae230e637bbe12de2eae9e0
+D46-recovery 257 e76a4ee829e526bccffa0dbe47baa807d79626832da20d3e989cbea3cefc8e56
+```
+
+Twelve supplementary answers preserve the original 30 record/codec answers and cover the lossless legacy chunk, complete attempt set, nine added durable preparation/invocation/repair families and the exact current request in the existing signed-request family. Resume preparation fixtures use the real D12 model input with handle `hxrsm1-other`, server time 1,000 seconds and expiry 1,900 seconds (encoded as Q ticks). Capture/repair use the model's exact 34-byte carrier and declared tenant identity; their provider receipt digests remain opaque fixture inputs, not claimed provider proofs.
+
+```text
+D46-chunk 179 b283d4e7b0025e99a6258569483c2d2b8555332de17f355491e3c4da404d4cd8
+D45-attempt-set 1691 99c18639aebb9701710692fc62663bee36f9954dd550dfb773f33a7c1bb95fa3
+D45-origin 1324 1d343b9cd7785e52bb3c39c9ca727948c036eab3d0400cb973bb2b9edf3d7aef
+D45-preparation 4219 d41382e6dc9bbbd7eda7673e987ead18483994f8416a897bf6cf36a2302b8a7f
+D45-preparation-head 189 4c2f8aaf984836bc794263d13223a08c951fd9d6db6536a3639ad6d38164d49b
+D45-invocation 206 9d2bc55777ae8d592aa4f3cdddbb5a600bda1c787202aab9db5b6ca0b10d7892
+D36-capture-preparation 312 040cce016e39d71123d9e5ee318707fdd4b1ff22e19db7bc2b7d602c7fd913dc
+D36-repair 274 b3421ca88730692fdc141d72a6c79ab760a11039b7a2f226e67a1b386fec4564
+D36-capture-origin 1642 a6fd2bbcb7bab856ce6a727e764fcee540a5b69799e683877e50bac37e61c160
+D36-current-request 119 3a6729276c8058b34686f93af8cd64b5e333feaa42bd7bad4ced1354b6f9c1c2
+D36-attempt 251 8d17e410a656deca924613edd8e6d2ed914b5107bf451ecdfe279d78b23f2765
+D36-cleanup 161 f49b5b5df13ffe90a3bca9acb308e70edb3140989b7ea959866ce6f456e5c264
 ```
 
 ```text
 D06-key 0f91a1983b2d87cad832582071c4c14b82fb4120da3f20615531f3d085bc132a
 D14-key 3d6106bf9476e5106018bc78cc6bc4bf771144175f662f3ca5e1c631af3e23df
 D45-closure-key 58b1b024adae4973e90e4e491aa8713593c439d51b5eb3941eaf66950e0b73b9
-D45-audit-key 1f05e253de2946318746839a6153cd8d1fbed21ad163610269f9d98e10c3198d
+D45-audit-key 9d7734f4f8ccb48cce924620e859cf4ba886f3c82a2948729320e57a99877104
 D46-capsule-key bdd34978ca55de1163ea7224dd6cae35ee041002ab6d4cbe655090b4e544291a
-D46-recovery-key c217ad1a103ef00f16682630cb53fd7a6a26d93262d1d7a06115839c7fa27985
+D46-recovery-key c3b5c20b7bb59e35990d5f13bd7ec52ade5f62c9be460380ed27307f2b5e9c06
 ```
 
-This verifier independently reconstructs all 30 records and checks both byte length and SHA-256. Changing a domain separator, codec, field count/order/framing, fixture, length, or answer fails.
+This verifier reconstructs 30 original and twelve supplementary answers, checking exact lengths/hashes and 42 digest-changing byte probes as framing evidence. Decoder rejection separately executes 200 missing, duplicate, reordered, overflowing and trailing mutations across 40 framed records covering all 39 domain families (the destination JSON and unframed inventory key are separate answers). Seventy semantic/boundary rejections cover signed counts, range/count, enums, optional markers, identifiers, large-family limits, complete attempt authority, unique live ordinals, charge/overhead/policy maxima, canonical continuation images, reconstruction linkage, invocation identity, progress framing and new inventory owners. Ordinary U identifiers, including optional ones, have a 1,024-byte maximum; the derived queue counter ID permits its seven-byte `tenant:` prefix plus that identifier (1,031). Explicit subject/locator fields permit 4,096, reason permits 512, caller key 128 visible ASCII and operator subject 256. B fields use their family ceiling before allocation and exact row/count/root constraints afterward. Capability shards are exactly 256; shard numbers are 0..255; genesis/predecessor and charge ownership follow D7. Canonical tagged images must decode/re-encode exactly; no process snapshot or noncanonical JSON substitutes for the declared continuation fields. Cross-record provider authentication remains future evidence.
 
 ```bash
 python3 - <<'PY'
 from hashlib import sha256
 from pathlib import Path
 from struct import pack
+import re
+import json
 
 path = Path('_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md')
 text = path.read_text(encoding='utf-8')
@@ -387,6 +455,10 @@ for line in ('D06-activation ' + block).splitlines():
     label, length, digest = line.split()
     answers[label] = (int(length), digest)
 assert len(answers) == 30, len(answers)
+extra_block = text.split('```text\nD46-chunk ',1)[1].split('\n```',1)[0]
+extra_answers = {label:(int(length),digest) for label,length,digest in
+                 (line.split() for line in ('D46-chunk '+extra_block).splitlines())}
+assert len(extra_answers) == 12
 key_block = text.split('```text\nD06-key ', 1)[1].split('\n```', 1)[0]
 key_answers = {}
 for line in ('D06-key ' + key_block).splitlines():
@@ -413,6 +485,13 @@ def R(domain, count, *fields):
     assert len(fields) == count
     return domain.encode() + b'\0\x01' + count.to_bytes(2, 'big') + b''.join(bytes([n]) + field for n, field in enumerate(fields, 1))
 
+def unresolved_root(members):
+    rows = sorted(members,key=lambda row:row[0])
+    assert len({p for p,m,body in rows}) == len(rows) and len({m for p,m,body in rows}) == len(rows)
+    assert all(0 < p < 2**32 for p,m,body in rows)
+    encoded = b''.join(pack('>I',p)+U(m)+sha256(body).digest() for p,m,body in rows)
+    return sha256(b'HX-EV-PUBLICATION-UNRESOLVED-1\0\x01'+pack('>I',len(rows))+encoded).digest()
+
 t = 638712864000000000
 z = bytes(32)
 MiB = 1024 * 1024
@@ -435,30 +514,39 @@ vectors['D29-pin-batch'] = R('HX-EV-PIN-BATCH-RESERVATION-2', 13, U('t'), B32(H(
 vectors['D31-wait'] = R('HX-EV-PIN-CAPACITY-WAIT-2', 12, U('t'), B32(H('scope')), B32(H('batch')), N(11*MiB), U('deployment'), N(42), Q(t), N(0), U('queued'), B32(H('capability')), B32(H('outbox-plan')), Q(t))
 qrow = N(42) + U('t') + B32(H('scope')) + U('queued')
 vectors['D31-queue'] = R('HX-EV-PIN-CAPACITY-QUEUE-4', 11, U('deployment-a'), U('deployment'), N(1), N(1), B(qrow), N(0), N(0), N(50000), N(42), B32(z), Q(t))
-vectors['D36-policy'] = R('HX-EV-SUBSCRIPTION-DELIVERY-POLICY-3', 13, U('deployment-a'), U('pubsub'), U('orders'), U('sub-a'), N(1), B32(z), U('active'), U('dead-letter-capture'), O(U('orders-dlq')), N(8), B32(H('subscription-config')), U('dapr-configuration'), Q(t))
+vectors['D36-policy'] = R('HX-EV-SUBSCRIPTION-DELIVERY-POLICY-3', 13, U('deployment-a'), U('pubsub'), U('orders'), U('sub-a'), N(1), B32(z), U('initial'), U('dead-letter-capture'), O(U('orders-dlq')), N(8), B32(H('subscription-config')), U('dapr-configuration'), Q(t))
 carrier = b'exact-carrier-and-headers'
 vectors['D36-held'] = R('HX-EV-HELD-DELIVERY-4', 22, U('tenant'), U('deployment-a'), O(U('t')), O(U('event-1')), U('pubsub'), U('orders'), U('sub-a'), B32(H('policy')), U('captured'), U('handler-capability-hold'), N(len(carrier)), B32(sha256(carrier).digest()), O(U('archive-a')), O(U('objects/held-1')), O(B32(H('object-readback'))), O(B32(H('handoff'))), Q(t), N(2), N(1), O(None), Q(t+600000000), N(2))
 body = b'quarantined-body'
 manifest = pack('>I', 1) + U('x-header') + N(4) + B32(sha256(b'value').digest())
 vectors['D36-quarantine'] = R('HX-EV-CARRIER-QUARANTINE-2', 14, U('deployment'), O(None), U('sub-a'), U('invalid-header-value'), N(len(body)), B32(sha256(body).digest()), B(manifest), U('archive-a'), U('objects/quarantine-1'), B32(H('provider-proof')), O(U('event-1')), Q(t), U('terminal-quarantine'), B32(H('delivery-receipt')))
 vectors['D36-redrive'] = R('HX-EV-REDRIVE-REQUEST-2', 7, U('admin'), U('tenant'), O(U('t')), B32(H('held-key')), N(2), U('operator'), Q(t))
-vectors['D37-entry'] = R('HX-EV-HOLD-ENTRY-2', 13, U('tenant'), U('t'), U('PublicationPinCapacityHold'), U('scope:abc'), O(U('d')), U('publication_pin_capacity_hold'), N(1), B32(z), Q(t), Q(t), N(1), U('coordinator'), Q(t+36000000000))
+vectors['D37-entry'] = R('HX-EV-HOLD-ENTRY-2', 13, U('tenant'), U('t'), U('PublicationPinCapacityHold'), U('scope:abc'), O(U('d')), U('publication_pin_capacity_hold'), N(1), B32(z), Q(t), Q(t), N(1), U('quota-coordinator'), Q(t+36000000000))
 irow = Q(t) + U('PublicationPinCapacityHold') + U('scope:abc') + B32(H('hold-entry'))
 vectors['D37-index'] = R('HX-EV-HOLD-INDEX-2', 8, U('tenant'), U('t'), N(1), N(1), B(irow), N(0), B32(z), Q(t))
 actor = U('tenant:' + sha256(U('t')).hexdigest())
 vectors['D37-directory'] = R('HX-EV-HOLD-DIRECTORY-1', 7, N(7), N(1), N(1), B(actor), B32(z), Q(t), N(0))
 vectors['D37-key'] = U('tenant') + U('t') + U('PublicationPinCapacityHold') + U('scope:abc')
 request_carrier = R('HX-EV-PUBLICATION-RESUME-CARRIER-1', 5, U('t'), U('hxrsm1-handle'), B32(H('hold')), U('retry-0001'), U('retry after broker repair'))
-request_identity = sha256(b'HX-EV-PUBLICATION-RESUME-IDENTITY-1\0\x01' + B(request_carrier)).digest()
+request_identity = sha256(b'HX-EV-PUBLICATION-RESUME-IDENTITY-1\0\x01' + U('t') + U('hxrsm1-handle') + U('retry-0001')).digest()
 request = R('HX-EV-PUBLICATION-RESUME-3', 15, U('admin'), U('t'), U('op'), B32(H('scope')), U('retry-exhausted'), B32(H('hold')), B32(H('head')), N(2), B32(H('audit-prev')), U('hxrsm1-handle'), B32(request_identity), B32(sha256(request_carrier).digest()), U('operator'), Q(t), Q(t+9000000000))
 vectors['D45-request'] = request
 attempt_row = pack('>I', 1) + N(4) + B32(sha256(b'definitive-result').digest())
-attempt_root = sha256(b'HX-EV-WINDOW-ATTEMPTS-1\0\x01' + B(attempt_row)).digest()
+attempt_records = []
+for local in range(1,5):
+    for observation,kind in enumerate(('register','unknown','result')):
+        evidence = sha256(b'definitive-result').digest() if (local,kind) == (4,'result') else H(f'{local}:{kind}')
+        attempt_records.append(pack('>I',1)+N(local)+N(observation)+U(kind)+H(f'parent-{local}')+H(f'send-{local}')+evidence)
+attempt_rows = b''.join(attempt_records)
+attempt_root = sha256(b'HX-EV-WINDOW-ATTEMPTS-2\0\x01'+U('t')+H('scope')+N(1)+H('roster')+N(12)+B(attempt_rows)).digest()
+attempt_set = R('HX-EV-WINDOW-ATTEMPT-SET-1',8,U('t'),H('scope'),N(1),H('roster'),N(12),B(attempt_rows),attempt_root,Q(t))
+assert len(attempt_set) == 1691 and sha256(attempt_set).hexdigest() == '99c18639aebb9701710692fc62663bee36f9954dd550dfb773f33a7c1bb95fa3'
+attempt_evidence_store = {('t',H('scope'),1):attempt_set}
 broker_auth = b'authenticated-broker-window-proof'
-closure = R('HX-EV-PUBLICATION-WINDOW-CLOSURE-3', 11, U('t'), B32(H('scope')), N(1), B(attempt_row), B32(sha256(b'broker-fence').digest()), B32(sha256(b'producer-disable').digest()), B32(sha256(b'empty-state').digest()), B32(attempt_root), U('SignedCarrier'), B32(z), Q(t))
+closure = R('HX-EV-PUBLICATION-WINDOW-CLOSURE-3', 11, U('t'), B32(H('scope')), N(1), B(pack('>I',1)+attempt_row), B32(sha256(b'broker-fence').digest()), B32(sha256(b'producer-disable').digest()), B32(sha256(b'empty-state').digest()), B32(attempt_root), U('SignedCarrier'), B32(z), Q(t))
 history = sha256(b'HX-EV-PUBLICATION-WINDOW-HISTORY-1\0\x01' + B32(z) + B(closure) + B(broker_auth)).digest()
 prior_state = R('HX-EV-PUBLICATION-RESUME-STATE-3', 14, U('t'), U('op'), B32(H('scope')), N(1), N(1), N(16), B32(H('hold-1')), B32(H('window-1')), B32(z), N(0), B32(z), B(pack('>I', 0)), B(pack('>I', 0)), Q(t-1))
-window = R('HX-EV-PUBLICATION-WINDOW-2', 13, U('t'), B32(H('scope')), U('operation'), N(2), B32(sha256(closure).digest()), B32(sha256(prior_state).digest()), B32(request_identity), B32(H('members')), B32(H('policy')), N(16), U('admin'), Q(t), B32(H('capability')))
+window = R('HX-EV-PUBLICATION-WINDOW-2', 13, U('t'), B32(H('scope')), U('operation'), N(2), B32(sha256(closure).digest()), B32(sha256(prior_state).digest()), B32(request_identity), B32(unresolved_root(((1,'event-1',b'canonical-stored-event'),))), B32(H('policy')), N(16), U('admin'), Q(t), B32(H('capability')))
 audit = R('HX-EV-PUBLICATION-RESUME-AUDIT-4', 10, U('t'), U('op'), N(2), B32(request_identity), B32(sha256(request_carrier).digest()), B32(sha256(prior_state).digest()), O(B32(sha256(closure).digest())), N(2), N(24), Q(t))
 retry_row = B32(request_identity) + B32(sha256(request_carrier).digest()) + N(2) + B32(sha256(audit).digest()) + N(2) + N(24) + Q(t+9000000000)
 state = R('HX-EV-PUBLICATION-RESUME-STATE-3', 14, U('t'), U('op'), B32(H('scope')), N(2), N(2), N(24), B32(H('hold-2')), B32(sha256(window).digest()), B32(history), N(1), B32(sha256(audit).digest()), B(pack('>I', 1)+retry_row), B(pack('>I', 0)), Q(t))
@@ -474,7 +562,9 @@ source_hash = H('drain-source')
 capsule_identity = sha256(b'HX-EV-LEGACY-RESUME-CAPSULE-IDENTITY-1\0\x01' + U('t') + U('d') + U('a') + U('tracking') + B32(source_hash)).digest()
 chunk_root = sha256(b'HX-EV-LEGACY-RESUME-CHUNK-ROWS-1\0\x01' + B(member)).digest()
 chunk = R('HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-1', 5, B32(capsule_identity), N(0), N(1), B(member), B32(chunk_root))
-manifest_row = N(0) + N(10) + N(1) + B32(sha256(chunk).digest()) + N(len(chunk)) + U('legacy-resume/chunk-0')
+assert len(chunk) == 179 and sha256(chunk).hexdigest() == 'b283d4e7b0025e99a6258569483c2d2b8555332de17f355491e3c4da404d4cd8'
+chunk_key = 'legacy-resume-capsule-chunk:' + K('HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1', B32(capsule_identity), N(0))
+manifest_row = N(0) + N(10) + N(1) + B32(sha256(chunk).digest()) + N(len(chunk)) + U(chunk_key)
 manifest = pack('>I', 1) + manifest_row
 capsule = R('HX-EV-LEGACY-RESUME-CAPSULE-2', 16, U('t'), U('d'), U('a'), U('tracking'), O(U('op')), U('correlation'), U('increment'), U('success-events'), N(10), N(10), I(1), B32(event_root), B(manifest), U('drain-exhaustion'), B32(source_hash), Q(t))
 vectors['D46-capsule'] = capsule
@@ -499,7 +589,118 @@ for label, value in vectors.items():
 assert keys == key_answers
 assert K('HX-EV-PUBLICATION-DRAIN-LIMIT-KEY-1', B32(H('scope')), N(1), N(23)) != K('HX-EV-PUBLICATION-DRAIN-LIMIT-KEY-1', B32(H('scope')), N(12), N(3))
 
-def decode_record(raw, domain, schema):
+def decode_rows(raw, count, schema, maxima=None, count_ceiling=50000):
+    offset = 0
+    maxima = maxima or [1024]*len(schema)
+    def take(length):
+        nonlocal offset
+        assert 0 <= length <= len(raw)-offset
+        value = raw[offset:offset+length]; offset += length
+        return value
+    def field(kind, maximum):
+        if isinstance(kind, tuple):
+            marker = take(1); assert marker in {b'\x00',b'\x01'}
+            return None if marker == b'\x00' else field(kind[1],maximum)
+        if kind == 'U':
+            length = int.from_bytes(take(4),'big'); assert 1 <= length <= maximum
+            value = take(length).decode('utf-8'); assert value.strip() and '\x00' not in value
+            return value
+        if kind == 'B32': return take(32)
+        if kind == 'P': return int.from_bytes(take(4),'big')
+        if kind in {'N','Q'}: return int.from_bytes(take(8),'big',signed=kind == 'Q')
+        raise AssertionError(kind)
+    assert 0 <= count <= count_ceiling
+    rows = [tuple(field(kind,maximum) for kind,maximum in zip(schema,maxima)) for _ in range(count)]
+    assert offset == len(raw)
+    return rows
+
+record_caps = {'HX-EV-FULL-REPLAY-ACTIVATION-2':MiB,
+    'HX-EV-WINDOW-ATTEMPT-SET-1':64*MiB,'HX-EV-PUBLICATION-INVOCATION-1':4096,
+    'HX-EV-RESUME-PREPARATION-1':MiB,
+    'HX-EV-RESUME-ORIGIN-1':256*1024,'HX-EV-RESUME-PREPARATION-HEAD-1':8*1024,
+    'HX-EV-CAPTURE-PREPARATION-1':8*1024,'HX-EV-REDRIVE-REPAIR-1':8*1024,
+    'HX-EV-CAPTURE-ORIGIN-1':8*1024,'HX-EV-REDRIVE-ATTEMPT-1':8*1024,'HX-EV-REDRIVE-CLEANUP-1':1024,
+    'HX-EV-REDRIVE-REQUEST-2':3*1024,
+    'HX-EV-PIN-CAPACITY-QUEUE-4':64*MiB,'HX-EV-HOLD-INDEX-2':40*MiB,
+    'HX-EV-HOLD-DIRECTORY-1':64*MiB,'HX-EV-CARRIER-QUARANTINE-2':128*1024,
+    'HX-EV-LEGACY-RESUME-CAPSULE-2':128*1024,'HX-EV-PUBLICATION-RESUME-STATE-3':32*1024,
+    'HX-EV-HELD-DELIVERY-4':32*1024,'HX-EV-HOLD-ENTRY-2':8*1024,'HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-1':64*1024,
+    'HX-EV-PUBLICATION-WINDOW-CLOSURE-3':64*1024,'HX-EV-PIN-BATCH-RESERVATION-2':64*1024,
+    'HX-EV-PUBLICATION-RETENTION-CAPABILITY-2':64*1024,
+    'HX-EV-FIRST-SEND-MEMBERSHIP-RESOLUTION-1':16*1024,
+    'HX-EV-SUBSCRIPTION-DELIVERY-POLICY-3':16*1024,'HX-EV-PUBLICATION-WINDOW-2':16*1024,
+    'HX-EV-SCOPE-SHARD-USAGE-1':1024,'HX-EV-PUBLICATION-COUNTER-1':1024}
+# All keys are zero-based field indexes. Other U fields use the imported 1,024-byte bound.
+field_maxima = {('HX-EV-HELD-DELIVERY-4',13):4096,('HX-EV-CARRIER-QUARANTINE-2',8):4096,
+    ('HX-EV-REDRIVE-REPAIR-1',4):4096,
+    ('HX-EV-REDRIVE-ATTEMPT-1',3):4096,
+    ('HX-EV-HOLD-ENTRY-2',3):4096,('HX-EV-PIN-CAPACITY-QUEUE-4',1):1031,
+    ('HX-EV-PUBLICATION-RESUME-CARRIER-1',3):128,('HX-EV-PUBLICATION-RESUME-CARRIER-1',4):512,
+    ('HX-EV-FULL-REPLAY-ACTIVATION-2',7):256,('HX-EV-PUBLICATION-RESUME-3',12):256,
+    ('HX-EV-REDRIVE-REQUEST-2',5):256}
+hold_reasons = {
+    'LegacyArrayLimit':'legacy_array_limit','ActivationInventoryCapacityHold':'full_replay_inventory_capacity',
+    'AdmissionEvidenceHold':'admission_evidence_hold','ResponsePreparationHold':'response_preparation_hold',
+    'OutcomeEvidenceHold':'outcome_evidence_hold','OutcomeEvidenceConflict':'outcome_evidence_conflict',
+    'TerminalEvidenceHold':'terminal_evidence_hold','PublicationRetryExhaustedHold':'publication_retry_exhausted_hold',
+    'PublicationResumePreparationHold':'publication_resume_preparation_hold',
+    'RedriveEvidenceRepairHold':'redrive-evidence-repair-hold',
+    'PublicationDrainLimitHold':'publication_drain_limit_hold','PublicationPinCapacityHold':'publication_pin_capacity_hold',
+    'PinCapacityQueueCorruptionHold':'pin_capacity_queue_corruption_hold',
+    'FirstSendMembershipChangedHold':'first_send_membership_changed_hold',
+    'ScopeRetentionCapacityHold':'scope_retention_capacity_hold','LegacyResumeIncident':'legacy_resume_evidence_unavailable'}
+held_reasons = {'handler-capability-hold','raw-source-unavailable','delivery-carrier-limit-hold',
+    'invalid-header-value','invalid-carrier','oversize-carrier','delivery_above_advertised_max'}
+
+def canonical_image_bytes(value):
+    def encode(item):
+        if isinstance(item,bytes): return ['bytes',item.hex()]
+        if isinstance(item,tuple): return ['tuple',[encode(v) for v in item]]
+        if isinstance(item,list): return ['list',[encode(v) for v in item]]
+        if isinstance(item,dict):
+            rows = [(encode(k),encode(v)) for k,v in item.items()]
+            return ['map',sorted(rows,key=lambda row:json.dumps(row[0],ensure_ascii=False,separators=(',',':')).encode())]
+        assert item is None or type(item) in {str,int,bool}
+        return ['scalar',item]
+    return json.dumps(encode(value),ensure_ascii=False,separators=(',',':')).encode()
+
+def read_canonical_image(raw):
+    def decode(item):
+        assert isinstance(item,list) and len(item) == 2
+        tag,value = item
+        if tag == 'bytes':
+            assert isinstance(value,str) and re.fullmatch(r'(?:[0-9a-f]{2})*',value)
+            return bytes.fromhex(value)
+        if tag in {'tuple','list'}:
+            assert isinstance(value,list)
+            decoded = [decode(v) for v in value]
+            return tuple(decoded) if tag == 'tuple' else decoded
+        if tag == 'map':
+            assert isinstance(value,list) and all(isinstance(row,list) and len(row) == 2 for row in value)
+            rows = [(decode(k),decode(v)) for k,v in value]
+            assert len({k for k,v in rows}) == len(rows)
+            return dict(rows)
+        assert tag == 'scalar' and (value is None or type(value) in {str,int,bool})
+        return value
+    result = decode(json.loads(raw))
+    assert canonical_image_bytes(result) == raw
+    return result
+
+def continuation_image(raw):
+    state = read_canonical_image(raw)
+    required = {'tenant','handle','hold_source','ordinal','window','closed','limit','active_charge',
+                'next_charge','charge_ceiling','live','tombstones','audits','invocations','window_claim'}
+    assert isinstance(state,dict) and required <= set(state)
+    assert set(state) <= required | {'used_charge','reconciliation','history','last_audit'}
+    for name in ('ordinal','window','closed','limit','active_charge','next_charge','charge_ceiling','audits'):
+        assert type(state[name]) is int and 0 <= state[name] < 2**64
+    assert state['limit'] > 0 and len(state['invocations']) <= 64
+    assert isinstance(state['live'],dict) and isinstance(state['tombstones'],dict)
+    assert len(state['live'])+len(state['tombstones']) <= 64
+    return state
+
+def decode_record(raw, domain, schema, attempt_store=None):
+    assert len(raw) <= record_caps.get(domain,4096)
     prefix = domain.encode() + b'\0\x01'
     assert raw.startswith(prefix)
     offset = len(prefix)
@@ -510,40 +711,443 @@ def decode_record(raw, domain, schema):
         assert 0 <= length <= len(raw)-offset
         value = raw[offset:offset+length]; offset += length
         return value
-    def field(kind):
+    def field(kind, maximum):
         nonlocal offset
         if isinstance(kind, tuple) and kind[0] == 'O':
             marker = take(1)
             assert marker in {b'\x00', b'\x01'}
-            if marker == b'\x01': field(kind[1])
+            return field(kind[1],maximum) if marker == b'\x01' else None
         elif kind in {'U','B'}:
             length = int.from_bytes(take(4), 'big')
-            assert length <= 1024*1024
-            take(length)
-        elif kind == 'B32': take(32)
-        elif kind == 'N': take(8)
-        elif kind == 'I': take(4)
-        elif kind == 'Q': take(8)
+            assert length <= maximum
+            value = take(length)
+            if kind == 'U':
+                assert 1 <= length <= maximum
+                value = value.decode('utf-8', errors='strict')
+                assert value.strip() and '\x00' not in value
+            return value
+        elif kind == 'B32': return take(32)
+        elif kind == 'N': return int.from_bytes(take(8), 'big')
+        elif kind == 'I': return int.from_bytes(take(4), 'big', signed=True)
+        elif kind == 'Q': return int.from_bytes(take(8), 'big', signed=True)
         else: raise AssertionError(kind)
+    values = []
     for expected_tag, kind in enumerate(schema, 1):
         assert take(1) == bytes([expected_tag])
-        field(kind)
+        base_kind = kind[1] if isinstance(kind,tuple) else kind
+        maximum = field_maxima.get((domain,expected_tag-1),
+            record_caps.get(domain,4096) if base_kind == 'B' else 1024)
+        values.append(field(kind,maximum))
     assert offset == len(raw)
-
-families = [
-    (request, 'HX-EV-PUBLICATION-RESUME-3', ['U','U','U','B32','U','B32','B32','N','B32','U','B32','B32','U','Q','Q']),
-    (capsule, 'HX-EV-LEGACY-RESUME-CAPSULE-2', ['U','U','U','U',('O','U'),'U','U','U','N','N','I','B32','B','U','B32','Q']),
-    (vectors['D31-queue'], 'HX-EV-PIN-CAPACITY-QUEUE-4', ['U','U','N','N','B','N','N','N','N','B32','Q']),
-]
+    def genesis(generation, predecessor):
+        assert generation > 0 and (generation == 1) == (predecessor == bytes(32))
+    if domain == 'HX-EV-PUBLICATION-RETENTION-CAPABILITY-2':
+        genesis(values[1],values[9])
+        assert values[11] == 256 and 1 <= values[13] <= 50000
+        assert 1024*MiB <= values[3] <= values[4] and values[3]+values[5] <= values[4]
+        assert 195*MiB <= values[5] <= values[6] <= values[4]
+        assert values[8] >= 64*MiB and values[7] <= 1114112
+        assert 1 <= values[12] <= 315576000 and 193*MiB <= values[14] <= 256*MiB
+    elif domain == 'HX-EV-HOLD-DIRECTORY-1':
+        assert 0 <= values[0] <= 255 and values[2]+values[6] <= 50000
+        genesis(values[1],values[4])
+        rows = decode_rows(values[3],values[2],['U'])
+        assert rows == sorted(set(rows))
+        assert all(re.fullmatch(r'(tenant|deployment):[0-9a-f]{64}',row[0]) for row in rows)
+    elif domain == 'HX-EV-SCOPE-SHARD-USAGE-1':
+        assert values[1] <= 255; genesis(values[5],values[6])
+    elif domain == 'HX-EV-COMMAND-SCOPE-LEGACY-2':
+        assert values[7] > values[6] and values[8] > 0
+    elif domain == 'HX-EV-LEGACY-SCOPE-CUTOVER-1':
+        genesis(values[2],values[7]); assert 1 <= values[5] <= 315576000
+        assert values[4] >= values[3] + values[5]*10000000
+    elif domain == 'HX-EV-COMMAND-SCOPE-TOMBSTONE-2':
+        assert values[7] <= 255 and 0 < values[6]-values[5] <= 315576000*10000000
+    elif domain == 'HX-EV-FIRST-SEND-MEMBERSHIP-RESOLUTION-1':
+        assert values[2] > 0 and values[5] > 0
+        assert values[9] in {'ContinueSamePin','FirstSendMembershipChangedHold'}
+    elif domain == 'HX-EV-FULL-REPLAY-ACTIVATION-2':
+        genesis(values[3],values[4]); assert len(values[5]) >= 4
+        count = int.from_bytes(values[5][:4],'big'); assert count <= 943
+        rows = decode_rows(values[5][4:],count,['U','U','N','N','N',('O','B32')])
+        assert [row[0] for row in rows] == sorted({row[0] for row in rows},key=lambda s:s.encode())
+        for route,disposition,events,readable,accounting,capability in rows:
+            assert disposition in {'continue-full-replay','incremental','hold'}
+            assert (disposition == 'incremental') == (capability is not None)
+            assert disposition != 'continue-full-replay' or (events < 75000 and readable < 48*MiB and accounting < 192*MiB)
+    if domain == 'HX-EV-LEGACY-RESUME-CAPSULE-2':
+        assert 1 <= values[10] <= 1000 and values[9] >= values[8]
+        assert values[9] - values[8] + 1 == values[10]
+        assert values[7] in {'success-events','rejection-events'}
+        assert values[13] in {'drain-exhaustion','operator-reconciliation'}
+        assert all(len(values[i].encode()) <= 1024 for i in (0,1,2,3,5,6))
+        assert len(values[12]) >= 4
+        count = int.from_bytes(values[12][:4],'big'); assert 1 <= count <= 17
+        rows = decode_rows(values[12][4:],count,['N','N','N','B32','N','U'],[1024]*5+[4096])
+        assert [row[0] for row in rows] == list(range(count))
+        sequence = values[8]
+        for ordinal,first,nrows,digest,length,key in rows:
+            assert first == sequence and 1 <= nrows <= 61 and 128 <= length <= 65536
+            sequence += nrows
+        assert sequence == values[9]+1
+    elif domain == 'HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-1':
+        assert 1 <= values[2] <= 61 and values[1] < 17 and len(raw) <= 65536
+        rows = decode_rows(values[3],values[2],['N','U','B32'])
+        assert [row[0] for row in rows] == list(range(rows[0][0],rows[0][0]+len(rows)))
+        assert len({row[1] for row in rows}) == len(rows)
+        assert values[4] == sha256(b'HX-EV-LEGACY-RESUME-CHUNK-ROWS-1\0\x01'+B(values[3])).digest()
+    elif domain == 'HX-EV-HELD-DELIVERY-4':
+        assert values[0] in {'tenant','deployment'}
+        assert (values[0] == 'tenant') == (values[2] is not None)
+        assert values[8] in {'observed','captured','redriving','incident','closed'}
+        assert values[9] in {'handler-capability-hold','raw-source-unavailable',
+            'delivery-carrier-limit-hold','invalid-header-value','invalid-carrier',
+            'oversize-carrier','delivery_above_advertised_max'}
+        retained = values[8] in {'captured','redriving','closed'}
+        assert all((values[i] is not None) == retained for i in (12,13,14,15))
+        assert values[17] > 0 and values[21] > 0
+    elif domain == 'HX-EV-SUBSCRIPTION-DELIVERY-POLICY-3':
+        assert values[4] > 0 and (values[4] == 1) == (values[5] == bytes(32))
+        assert values[6] == ('initial' if values[4] == 1 else 'successor')
+        assert values[7] in {'dead-letter-capture','direct-held-capture'}
+        assert (values[7] == 'dead-letter-capture') == (values[8] is not None)
+        assert 1 <= values[9] <= 64 and values[11] in {'dapr-configuration','broker-api'}
+    elif domain == 'HX-EV-PUBLICATION-CHARGE-2':
+        assert values[1] in {'tenant','capture-scope'}
+        assert values[4] in {'pin-batch','side-record','retained-object','oversize-quarantine','resume-window'}
+        ceilings = {'pin-batch':449*MiB,'side-record':193*MiB,'retained-object':193*MiB,
+                    'oversize-quarantine':256*MiB,'resume-window':1024*MiB}
+        assert values[5] <= ceilings[values[4]] and values[6] <= 1114112
+        assert values[5] + values[6] <= (1 << 64)-1 and values[7] == values[5] + values[6]
+        assert values[10] in {'staged','active','released'} and values[9] > 0
+        genesis(values[9],values[11]); assert values[8] > 0
+        assert values[10] != 'staged' or (values[12] is not None and values[4] == 'resume-window')
+        assert values[9] != 1 or values[10] != 'active' or values[12] is None
+        assert values[12] is None or values[4] == 'resume-window'
+        assert values[4] != 'resume-window' or values[9] == 1 or values[12] is not None
+    elif domain == 'HX-EV-LEGACY-PUBLICATION-RECOVERY-3':
+        genesis(values[3],values[9]); assert values[4] > 0
+        assert values[5] in {'legacy-resume','dead-letter-admin'}
+        assert values[6] in {'claimed','draining','completed','failed'}
+        assert (values[6] == 'failed') == (values[10] is not None)
+        assert values[10] is None or values[10] in {'transport-retryable','evidence-unavailable','evidence-contradictory'}
+        assert values[11] is None or values[6] == 'claimed'
+    elif domain == 'HX-EV-PIN-CAPACITY-QUEUE-4':
+        assert 1 <= values[7] <= 50000 and values[3]+values[6] <= values[7]
+        assert values[5] <= values[3]; genesis(values[2],values[9])
+        rows = decode_rows(values[4],values[3],['N','U','B32','U'])
+        assert all(0 < row[0] <= values[8] and row[3] in {'queued','parked'} for row in rows)
+        assert len({row[0] for row in rows}) == len({row[2] for row in rows}) == len(rows)
+        assert rows == sorted(rows,key=lambda row:(row[0],row[1].encode(),row[2]))
+        assert values[5] == sum(row[3] == 'parked' for row in rows)
+    elif domain == 'HX-EV-PIN-CAPACITY-WAIT-2':
+        assert values[4] in {'tenant','deployment'} and values[5] > 0
+        assert values[8] in {'queued','parked'}
+    elif domain == 'HX-EV-PUBLICATION-COUNTER-1':
+        assert values[1] in {'tenant','tenant-pool','deployment','unidentified'}
+        genesis(values[5],values[6])
+    elif domain == 'HX-EV-PIN-BATCH-RESERVATION-2':
+        assert 1 <= values[3] <= 59 and values[6] > 0 and values[11] > 0
+        assert values[10] in {'reserved','installed','released'}
+        rows = decode_rows(values[4],values[3],['P','U','B32','N','N'])
+        assert rows == sorted(rows,key=lambda row:row[0])
+        assert len({row[0] for row in rows}) == len({row[1] for row in rows}) == len(rows)
+        assert all(row[0] > 0 and row[3] <= row[4] for row in rows)
+        assert values[5] == sum(row[4] for row in rows) <= (1 << 64)-1
+        assert values[2] == sha256(values[4]).digest()
+    elif domain == 'HX-EV-PUBLICATION-RESUME-CARRIER-1':
+        assert all(33 <= byte <= 126 for byte in values[3].encode('ascii'))
+    elif domain == 'HX-EV-PUBLICATION-RESUME-3':
+        assert values[4] in {'retry-exhausted','drain-limit','legacy-publish-failed'} and values[7] > 0
+        assert values[14] > values[13] and values[14]-values[13] <= 9000000000
+    elif domain == 'HX-EV-PUBLICATION-DRAIN-LIMIT-2':
+        assert values[8] in {'pending','unknown','failed'} and values[4] > 0 and values[7] > 0
+    elif domain == 'HX-EV-PUBLICATION-DRAIN-LIMIT-RESOLUTION-1':
+        assert values[3] in {'resumed','head-advanced','terminal'} and values[5] > 0
+    elif domain == 'HX-EV-PUBLICATION-WINDOW-2':
+        assert values[9] > 0
+        assert (values[3] == 0) == (values[4] == bytes(32))
+        assert (values[3] == 0) == (values[6] == bytes(32))
+    elif domain == 'HX-EV-PUBLICATION-WINDOW-CLOSURE-3':
+        assert len(values[3]) >= 4 and values[8] in {'SignedCarrier','BackendCas'}
+        count = int.from_bytes(values[3][:4],'big'); assert count <= 1000
+        rows = decode_rows(values[3][4:],count,['P','N','B32'])
+        assert len({row[0] for row in rows}) == len(rows) and rows == sorted(rows)
+        assert all(row[0] > 0 and row[1] > 0 for row in rows)
+        authority = (attempt_evidence_store if attempt_store is None else attempt_store).get(tuple(values[:3]))
+        assert authority is not None
+        complete = decode_record(authority,'HX-EV-WINDOW-ATTEMPT-SET-1',['U','B32','N','B32','N','B','B32','Q'])
+        assert complete[6] == values[7]
+        attempts = decode_rows(complete[5],complete[4],['P','N','N','U','B32','B32','B32'],count_ceiling=249216)
+        final = {}
+        for position,local,observation,kind,parent,send,evidence in attempts:
+            if kind == 'result': final[position] = (position,local,evidence)
+        assert rows == sorted(final.values())
+    elif domain == 'HX-EV-WINDOW-ATTEMPT-SET-1':
+        assert 0 < values[4] <= 249216
+        rows = decode_rows(values[5],values[4],['P','N','N','U','B32','B32','B32'],count_ceiling=249216)
+        assert rows == sorted(rows,key=lambda r:r[:3]) and len({r[:3] for r in rows}) == len(rows)
+        groups = {}
+        for row in rows:
+            assert 0 < row[0] <= 59 and 0 < row[1] <= 64 and row[3] in {'register','unknown','result'}
+            groups.setdefault(row[:2],[]).append(row)
+        for group in groups.values():
+            assert [r[2] for r in group] == list(range(len(group))) and 2 <= len(group) <= 66
+            assert group[0][3] == 'register' and group[-1][3] == 'result'
+            assert all(r[3] == 'unknown' for r in group[1:-1])
+            assert len({(r[4],r[5]) for r in group}) == 1
+        for position in {r[0] for r in rows}:
+            locals_for_member = sorted(local for member,local in groups if member == position)
+            assert locals_for_member == list(range(1,locals_for_member[-1]+1))
+        assert values[6] == sha256(b'HX-EV-WINDOW-ATTEMPTS-2\0\x01'+U(values[0])+values[1]+N(values[2])+values[3]+N(values[4])+B(values[5])).digest()
+    elif domain == 'HX-EV-PUBLICATION-RESUME-STATE-3':
+        assert values[5] > 0
+        counts = [int.from_bytes(values[i][:4],'big') for i in (11,12)]
+        assert all(len(values[i]) >= 4 for i in (11,12)) and sum(counts) <= 64
+        live = decode_rows(values[11][4:],counts[0],['B32','B32','N','B32','N','N','Q'])
+        expired = decode_rows(values[12][4:],counts[1],['B32','B32','Q','Q'])
+        assert len({row[0] for row in live+expired}) == sum(counts)
+        assert live == sorted(live,key=lambda row:row[2]) and expired == sorted(expired)
+        assert len({row[2] for row in live}) == len(live)
+        assert all(0 < row[2] <= values[3] and row[5] > 0 for row in live)
+        assert all(row[3]-row[2] == 30*86400*10000000 for row in expired)
+    elif domain == 'HX-EV-PUBLICATION-RESUME-AUDIT-4':
+        assert values[2] > 0 and values[8] > 0
+    elif domain == 'HX-EV-RESUME-ORIGIN-1':
+        assert sha256(values[4]).digest() == values[3] and values[7] > 0
+        assert 0 < values[9]-values[8] <= 9000000000 and len(values[6]) <= 128*1024
+        carrier_fields = decode_record(values[4],'HX-EV-PUBLICATION-RESUME-CARRIER-1',['U','U','B32','U','U'])
+        claim_fields = decode_record(values[5],'HX-EV-PUBLICATION-RESUME-3',schemas['D45-request'])
+        assert claim_fields[1:3] == tuple(values[:2]) and claim_fields[10:12] == tuple(values[2:4])
+        assert claim_fields[9] == carrier_fields[1] and claim_fields[13:15] == tuple(values[8:10])
+        assert values[2] == sha256(b'HX-EV-PUBLICATION-RESUME-IDENTITY-1\0\x01'+U(carrier_fields[0])+U(carrier_fields[1])+U(carrier_fields[3])).digest()
+        assert carrier_fields[0] == values[0] and carrier_fields[2] == claim_fields[5]
+        prior = continuation_image(values[6])
+        assert (prior['tenant'],prior['handle'],prior['hold_source']) == (values[0],carrier_fields[1],carrier_fields[2])
+    elif domain == 'HX-EV-RESUME-PREPARATION-1':
+        assert values[4] == sha256(values[5]).digest()
+        assert len(values[5]) <= 256*1024 and len(values[6]) <= 256*1024 and len(values[10]) <= 128*1024
+        assert 0 < values[8]-values[7] <= 9000000000 and 0 < values[9] <= 1024*MiB and values[11] > 0
+        prior,successor = continuation_image(values[5]),continuation_image(values[6])
+        manifest = read_canonical_image(values[10])
+        assert set(manifest) == {'prior','successor','window'}
+        assert all(set(manifest[key]) == {'roster','accepted','unresolved','window_claim_bytes'} for key in ('prior','successor'))
+        assert all(isinstance(root,bytes) and len(root) == 32 for key in ('prior','successor') for root in manifest[key].values())
+        assert isinstance(manifest['window'],bytes) and len(manifest['window']) <= 16*1024
+        assert prior['tenant'] == successor['tenant'] == values[0]
+        assert successor['ordinal'] == prior['ordinal']+1 == values[11]
+        assert successor['active_charge'] == values[9] and successor['handle'] == prior['handle']
+    elif domain == 'HX-EV-RESUME-PREPARATION-HEAD-1':
+        genesis(values[6],values[7]); assert len(values[8]) <= 4096
+        assert values[5] in {'admitted','writing','cleanup','audited','completed','rolled-back','evidence-hold'}
+        assert len(values[8]) >= 4
+        count = int.from_bytes(values[8][:4],'big'); assert count <= 8
+        rows = decode_rows(values[8][4:],count,['U','U','B32','B32','U'],[1024,128,1024,1024,1024])
+        assert len({row[0] for row in rows}) == len(rows)
+        assert all(row[0] in {'claim','resolution','fence','closure','window','audit','state','invocation'}
+                   and row[4] in {'pending','present','deleted'} for row in rows)
+        assert values[5] not in {'audited','completed'} or values[4] is not None
+    elif domain == 'HX-EV-PUBLICATION-INVOCATION-1':
+        assert values[3] > 0 and values[4] > 0
+        assert values[7] == sha256(b'HX-EV-PUBLICATION-INVOCATION-1\0\x01'+values[2]+N(values[3])+N(values[4])+values[5]+values[6]).digest()
+    elif domain == 'HX-EV-CAPTURE-PREPARATION-1':
+        assert values[7] <= 193*MiB
+    elif domain == 'HX-EV-CAPTURE-ORIGIN-1':
+        state = read_canonical_image(values[1])
+        assert len(values[1]) <= 7*1024 and values[2] == sha256(values[1]).digest()
+        assert state['held_key'] == values[0] and state['first_observed'] == values[3]
+        assert state['delivery_attempt_count'] == state['observation_revision'] == values[4] > 0
+        protected = {k:v for k,v in state.items() if k not in {'delivery_attempt_count','observation_revision','observation_receipt'}}
+        assert state['observation_receipt'] == sha256(b'provider-monotonic-observation:'+canonical_image_bytes(protected)+N(values[4])+N(values[4])).digest()
+    elif domain == 'HX-EV-REDRIVE-ATTEMPT-1':
+        assert values[1] > 0
+    elif domain == 'HX-EV-REDRIVE-CLEANUP-1':
+        assert values[1] > 0 and values[4] in {'repaired-readback','record-deleted','record-readback','entry-deleted'}
+        assert (values[4] != 'repaired-readback') == (values[5] is not None)
+        assert (values[4] == 'entry-deleted') == (values[6] is not None)
+        assert values[5] is None or values[5] == sha256(b'provider-repair-deletion:'+values[2]).digest()
+        assert values[6] is None or values[6] == sha256(b'provider-repair-entry-deletion:'+values[3]).digest()
+    elif domain == 'HX-EV-REDRIVE-REPAIR-1':
+        assert values[1] > 0; genesis(values[5],values[6])
+        assert values[7] in {'required','repaired'}
+        assert (values[7] == 'repaired') == (values[8] is not None)
+    elif domain == 'HX-EV-HOLD-INDEX-2':
+        assert values[0] in {'tenant','deployment'} and values[3] <= 10000 and values[5] == 0
+        genesis(values[2],values[6])
+        rows = decode_rows(values[4],values[3],['Q','U','U','B32'],[1024,1024,4096,1024])
+        assert rows == sorted(rows,key=lambda row:(row[0],row[1].encode(),row[2].encode()))
+        assert len({(row[1],row[2]) for row in rows}) == len(rows)
+    elif domain == 'HX-EV-HOLD-ENTRY-2':
+        assert values[0] in {'tenant','deployment'}; genesis(values[6],values[7])
+        assert values[10] > 0 and values[9] >= values[8] and values[12] >= values[9]
+        assert values[11] in {'actor','coordinator','gateway','subscriber','projection','operations','quota-coordinator'}
+        assert (values[2] == 'HeldDelivery' and values[5] in held_reasons) or hold_reasons.get(values[2]) == values[5]
+        if values[2] in {'PublicationPinCapacityHold','PinCapacityQueueCorruptionHold'}: assert values[11] == 'quota-coordinator'
+        if values[2] == 'PublicationResumePreparationHold': assert values[11] == 'coordinator'
+        if values[2] == 'RedriveEvidenceRepairHold': assert values[11] == 'operations'
+        if values[2] == 'HeldDelivery': assert values[11] == 'operations'
+    elif domain == 'HX-EV-REDRIVE-REQUEST-2':
+        assert values[1] in {'tenant','deployment'} and (values[1] == 'tenant') == (values[2] is not None)
+    elif domain == 'HX-EV-CARRIER-QUARANTINE-2':
+        assert values[0] in {'tenant','deployment'} and (values[0] == 'tenant') == (values[1] is not None)
+        assert values[3] in {'invalid-header-value','invalid-carrier','oversize-carrier'}
+        assert values[12] == 'terminal-quarantine' and values[4] <= 256*MiB
+        assert len(values[6]) >= 4
+        count = int.from_bytes(values[6][:4],'big'); assert count <= 128
+        rows = decode_rows(values[6][4:],count,['U','N','B32'],[65536,1024,1024])
+        assert len({row[0] for row in rows}) == count and sum(len(row[0].encode()) for row in rows) <= 65536
+    return tuple(values)
+
+def encode_typed(kind, value):
+    if isinstance(kind, tuple): return O(None if value is None else encode_typed(kind[1], value))
+    return {'U':U,'B':B,'B32':B32,'N':N,'I':I,'Q':Q}[kind](value)
+
+schemas = {
+ 'D06-activation':['U','U','B32','N','B32','B','Q','U','Q'],
+ 'D12-legacy-claim':['U','U','U','U','U','B32','Q','Q','N','B32'],
+ 'D12-cutover':['U','U','N','Q','Q','N','B32','B32'],
+ 'D12-usage':['U','N','N','N','N','N','B32'],
+ 'D12-tombstone':['U','U','B32','B32','B32','Q','Q','N'],
+ 'D14-drain-limit':['U','B32','U','N','N','B32','B32','N','U','Q'],
+ 'D14-drain-resolution':['U','B32','B32','U','B32','N','U','Q'],
+ 'D16-membership-resolution':['U','B32','N','U','B32','N','B32','B32','B32','U','U','Q'],
+ 'D29-capability':['U','N','B','N','N','N','N','N','N','B32','Q','N','N','N','N'],
+ 'D29-charge':['U','U','U','B32','U','N','N','N','N','N','U','B32',('O','B32'),'Q'],
+ 'D29-counter':['U','U','U','N','N','N','B32','Q'],
+ 'D29-pin-batch':['U','B32','B32','N','B','N','N','B32','B32','B32','U','N','Q'],
+ 'D31-wait':['U','B32','B32','N','U','N','Q','N','U','B32','B32','Q'],
+ 'D31-queue':['U','U','N','N','B','N','N','N','N','B32','Q'],
+ 'D36-policy':['U','U','U','U','N','B32','U','U',('O','U'),'N','B32','U','Q'],
+ 'D36-held':['U','U',('O','U'),('O','U'),'U','U','U','B32','U','U','N','B32',('O','U'),('O','U'),('O','B32'),('O','B32'),'Q','N','N',('O','B32'),'Q','N'],
+ 'D36-quarantine':['U',('O','U'),'U','U','N','B32','B','U','U','B32',('O','U'),'Q','U','B32'],
+ 'D36-redrive':['U','U',('O','U'),'B32','N','U','Q'],
+ 'D37-entry':['U','U','U','U',('O','U'),'U','N','B32','Q','Q','N','U','Q'],
+ 'D37-index':['U','U','N','N','B','N','B32','Q'],
+ 'D37-directory':['N','N','N','B','B32','Q','N'],
+ 'D45-request':['U','U','U','B32','U','B32','B32','N','B32','U','B32','B32','U','Q','Q'],
+ 'D45-window':['U','B32','U','N','B32','B32','B32','B32','B32','N','U','Q','B32'],
+ 'D45-closure':['U','B32','N','B','B32','B32','B32','B32','U','B32','Q'],
+ 'D45-state':['U','U','B32','N','N','N','B32','B32','B32','N','B32','B','B','Q'],
+ 'D45-audit':['U','U','N','B32','B32','B32',('O','B32'),'N','N','Q'],
+ 'D46-capsule':['U','U','U','U',('O','U'),'U','U','U','N','N','I','B32','B','U','B32','Q'],
+ 'D46-recovery':['U','B32','U','N','N','U','U','B32',('O','B32'),'B32',('O','U'),('O','B32'),'Q'],
+}
+extra_schemas = {
+    'D45-origin':['U','U','B32','B32','B','B','B','N','Q','Q'],
+    'D45-preparation':['U','U','B32','B32','B32','B','B','Q','Q','N','B','N'],
+    'D45-preparation-head':['U','U','B32','B32',('O','B32'),'U','N','B32','B','Q'],
+    'D45-invocation':['U','U','B32','N','N','B32','B32','B32','Q'],
+    'D36-capture-preparation':['B32','B32','B32','B32','U','U','B32','N','Q'],
+    'D36-repair':['B32','N','B32','B32','U','N','B32','U',('O','B32'),'Q'],
+    'D36-capture-origin':['B32','B','B32','Q','N','B32'],
+    'D36-current-request':['U','U',('O','U'),'B32','N','U','Q'],
+    'D36-attempt':['B32','N','B32','U','B32','Q','B32'],
+    'D36-cleanup':['B32','N','B32','B32','U',('O','B32'),('O','B32')],
+}
+# Reconstruct ten durable answers from explicit model inputs, independently
+# of the transition helpers that consume them in the second verifier.
+fixture_roster = ((1,'message-1',b'accepted'),(2,'message-2',b'unresolved-a'),(3,'message-3',b'unresolved-b'))
+fixture_existing_window = R('HX-EV-PUBLICATION-WINDOW-2',13,U('t'),H('scope'),U('operation'),N(7),H('prior-closure'),
+    H('prior-window-state'),H('prior-window-request'),unresolved_root(fixture_roster[1:]),
+    H('policy'),N(16),U('admin'),Q(9990000000),H('capability'))
+fixture_prior = {'roster':fixture_roster,'accepted':(fixture_roster[0],),'unresolved':fixture_roster[1:],
+    'ordinal':1,'window':7,'closed':2,'limit':16,'tenant':'t','handle':'hxrsm1-other',
+    'hold_source':H('limit-hash'),'active_charge':300,'next_charge':400,'charge_ceiling':1000,
+    'live':{},'tombstones':{},'orphans':{},'audits':0,'invocations':(),
+    'window_claim_bytes':fixture_existing_window,'window_claim':sha256(fixture_existing_window).digest()}
+fixture_carrier = R('HX-EV-PUBLICATION-RESUME-CARRIER-1',5,U('t'),U('hxrsm1-other'),H('limit-hash'),U('custom-handle'),U('retry after repair'))
+fixture_identity = sha256(b'HX-EV-PUBLICATION-RESUME-IDENTITY-1\0\x01'+U('t')+U('hxrsm1-other')+U('custom-handle')).digest()
+fixture_carrier_hash = sha256(fixture_carrier).digest()
+fixture_predecessor = R('HX-EV-PUBLICATION-RESUME-STATE-3',14,U('t'),U('op'),H('scope'),N(1),N(7),N(16),H('limit-hash'),
+    fixture_prior['window_claim'],z,N(2),z,B(pack('>I',0)),B(pack('>I',0)),Q(10000000000))
+fixture_actual_audit = R('HX-EV-PUBLICATION-RESUME-AUDIT-4',10,U('t'),U('op'),N(2),fixture_identity,fixture_carrier_hash,
+    sha256(fixture_predecessor).digest(),O(None),N(7),N(24),Q(10000000000))
+fixture_audit = sha256(fixture_actual_audit).digest()
+fixture_member_rows = b''.join(pack('>I',p)+U(m)+sha256(body).digest() for p,m,body in fixture_roster[1:])
+fixture_unresolved_root = sha256(b'HX-EV-PUBLICATION-UNRESOLVED-1\0\x01'+pack('>I',2)+fixture_member_rows).digest()
+fixture_invocation = sha256(b'HX-EV-PUBLICATION-INVOCATION-1\0\x01'+fixture_prior['window_claim']+N(2)+N(24)+fixture_identity+fixture_unresolved_root).digest()
+fixture_response = json.dumps({'auditRecordHash':fixture_audit.hex(),'drainLimit':24,'resumeHandle':'hxrsm1-other',
+    'resumeOrdinal':2,'window':7},sort_keys=True,separators=(',',':')).encode()
+fixture_successor = dict(fixture_prior,ordinal=2,limit=24,active_charge=400,audits=1,last_audit=fixture_audit,invocations=(fixture_invocation,),
+    live={fixture_identity:{'carrier_hash':fixture_carrier_hash,'result':{'ordinal':2,'window':7,'limit':24,'audit_hash':fixture_audit},
+                           'response':fixture_response,'expires_at':1900}})
+fixture_imports = ('roster','accepted','unresolved','window_claim_bytes')
+fixture_prior_image = canonical_image_bytes({k:v for k,v in fixture_prior.items() if k not in fixture_imports and k != 'orphans'})
+fixture_successor_image = canonical_image_bytes({k:v for k,v in fixture_successor.items() if k not in fixture_imports and k != 'orphans'})
+fixture_manifest = canonical_image_bytes({'prior':{k:sha256(canonical_image_bytes(fixture_prior[k])).digest() for k in fixture_imports},
+    'successor':{k:sha256(canonical_image_bytes(fixture_successor[k])).digest() for k in fixture_imports},'window':fixture_prior['window_claim_bytes']})
+fixture_claim = R('HX-EV-PUBLICATION-RESUME-3',15,U('admin'),U('t'),U('op'),H('scope'),U('drain-limit'),H('limit-hash'),H('head'),
+    N(2),z,U('hxrsm1-other'),fixture_identity,fixture_carrier_hash,U('operator'),Q(10000000000),Q(19000000000))
+extra_vectors = {
+    'D46-chunk':chunk,'D45-attempt-set':attempt_set,
+    'D45-origin':R('HX-EV-RESUME-ORIGIN-1',10,U('t'),U('op'),fixture_identity,fixture_carrier_hash,B(fixture_carrier),
+        B(fixture_claim),B(fixture_prior_image),N(1),Q(10000000000),Q(19000000000)),
+    'D45-preparation':R('HX-EV-RESUME-PREPARATION-1',12,U('t'),U('op'),fixture_identity,fixture_carrier_hash,
+        sha256(fixture_prior_image).digest(),B(fixture_prior_image),B(fixture_successor_image),Q(10000000000),Q(19000000000),N(400),B(fixture_manifest),N(2)),
+    'D45-invocation':R('HX-EV-PUBLICATION-INVOCATION-1',9,U('t'),U('op'),fixture_prior['window_claim'],N(2),N(24),fixture_identity,
+        fixture_unresolved_root,fixture_invocation,Q(10000000000)),
+}
+extra_vectors['D45-preparation-head'] = R('HX-EV-RESUME-PREPARATION-HEAD-1',10,U('t'),U('op'),fixture_identity,
+    sha256(extra_vectors['D45-origin']).digest(),O(None),U('admitted'),N(1),z,B(pack('>I',0)),Q(10000000000))
+held_digest = bytes.fromhex('13f514aa06580dd6db547e25807b783ef3dfd9649a5a957176c4d00038d755f8')
+carrier_digest = bytes.fromhex('a82ba5fcfda582d6e456c2914755f95f6a834a86da6c3d95672c8f6aaf78a351')
+fixture_observed = {'identity':('tenant','deployment-a','t','pubsub','orders','sub-a'),'held_key':held_digest,'carrier_hash':carrier_digest,
+    'metadata_key':'metadata:'+held_digest.hex(),'inventory_key':'inventory:'+held_digest.hex(),
+    'metadata_receipt':bytes.fromhex('216a9f46e35a2e477a0f188a76580798c84f6fba57a8f85752f8462b34daa6c6'),
+    'inventory_receipt':bytes.fromhex('0b324da6f8fa095d9a9c18993cd53e6dfade1ed8a954b9e7fdc35e6c09ca9fcf'),
+    'account_kind':'tenant','account':'t','state':'observed','transport_copy_acked':False,'route_success':False,'closed':False,
+    'first_observed':t,'delivery_attempt_count':2,'charged_bytes':32768,'indexed':True,'operator_visible':True,'observation_revision':2}
+fixture_observation_protected = {k:v for k,v in fixture_observed.items() if k not in {'delivery_attempt_count','observation_revision','observation_receipt'}}
+fixture_observed['observation_receipt'] = sha256(b'provider-monotonic-observation:'+canonical_image_bytes(fixture_observation_protected)+N(2)+N(2)).digest()
+fixture_observed_hash = sha256(canonical_image_bytes(fixture_observed)).digest()
+extra_vectors['D36-capture-origin'] = R('HX-EV-CAPTURE-ORIGIN-1',6,held_digest,B(canonical_image_bytes(fixture_observed)),fixture_observed_hash,Q(t),N(2),sha256(vectors['D36-policy']).digest())
+extra_vectors['D36-capture-preparation'] = R('HX-EV-CAPTURE-PREPARATION-1',9,held_digest,
+    fixture_observed_hash,
+    bytes.fromhex('216a9f46e35a2e477a0f188a76580798c84f6fba57a8f85752f8462b34daa6c6'),
+    bytes.fromhex('0b324da6f8fa095d9a9c18993cd53e6dfade1ed8a954b9e7fdc35e6c09ca9fcf'),U('held-delivery-store'),
+    U('held/'+held_digest.hex()),carrier_digest,N(34),Q(t))
+extra_vectors['D36-current-request'] = R('HX-EV-REDRIVE-REQUEST-2',7,U('admin'),U('tenant'),O(U('t')),held_digest,N(0),U('operator'),Q(t))
+extra_vectors['D36-attempt'] = R('HX-EV-REDRIVE-ATTEMPT-1',7,held_digest,N(1),carrier_digest,U('held/'+held_digest.hex()),fixture_observed['metadata_receipt'],Q(t),sha256(extra_vectors['D36-current-request']).digest())
+fixture_attempt = {'owner':held_digest,'count':1,'carrier_hash':carrier_digest,'locator':'held/'+held_digest.hex(),
+    'metadata_receipt':fixture_observed['metadata_receipt'],'raw':extra_vectors['D36-attempt'],'request_hash':sha256(extra_vectors['D36-current-request']).digest()}
+fixture_attempt['receipt'] = sha256(canonical_image_bytes(fixture_attempt)).digest()
+fixture_disputed_attempt = dict(fixture_attempt,receipt=bytes(32))
+fixture_disputed_hash = sha256(canonical_image_bytes(fixture_disputed_attempt)).digest()
+extra_vectors['D36-repair'] = R('HX-EV-REDRIVE-REPAIR-1',10,held_digest,N(1),
+    fixture_disputed_hash,carrier_digest,
+    U('held/'+held_digest.hex()),N(1),z,U('required'),O(None),Q(t))
+fixture_object_receipt = sha256(b'authenticated-object-readback:'+U('held-delivery-store')+U('held/'+held_digest.hex())+B(b'exact-retained-carrier-and-headers')).digest()
+fixture_repair_receipt = sha256(b'authenticated-attempt-repair:'+fixture_attempt['receipt']+fixture_object_receipt).digest()
+fixture_repaired = R('HX-EV-REDRIVE-REPAIR-1',10,held_digest,N(1),fixture_disputed_hash,carrier_digest,U('held/'+held_digest.hex()),
+    N(2),sha256(extra_vectors['D36-repair']).digest(),U('repaired'),O(fixture_repair_receipt),Q(t))
+fixture_repair_entry = R('HX-EV-HOLD-ENTRY-2',13,U('tenant'),U('t'),U('RedriveEvidenceRepairHold'),U(held_digest.hex()+':1'),O(None),
+    U('redrive-evidence-repair-hold'),N(1),z,Q(t),Q(t),N(1),U('operations'),Q(t+9000000000))
+extra_vectors['D36-cleanup'] = R('HX-EV-REDRIVE-CLEANUP-1',7,held_digest,N(1),sha256(fixture_repaired).digest(),
+    sha256(fixture_repair_entry).digest(),U('repaired-readback'),O(None),O(None))
+assert set(extra_vectors) == set(extra_answers)
+assert {label:(len(raw),sha256(raw).hexdigest()) for label,raw in extra_vectors.items()} == extra_answers
+for label,raw in extra_vectors.items():
+    assert sha256(raw[:-1]+bytes([raw[-1]^1])).hexdigest() != extra_answers[label][1]
+families = [(vectors[label],vectors[label].split(b'\0',1)[0].decode(),schema)
+            for label,schema in schemas.items()]
+families.append((chunk,'HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-1',['B32','N','N','B','B32']))
+families.append((attempt_set,'HX-EV-WINDOW-ATTEMPT-SET-1',['U','B32','N','B32','N','B','B32','Q']))
+families.extend((extra_vectors[label],extra_vectors[label].split(b'\0',1)[0].decode(),schema) for label,schema in extra_schemas.items())
+assert len(families) == 40 and len({domain for _,domain,_ in families}) == 39
 malformed_rejected = 0
 for value, domain, schema in families:
-    decode_record(value, domain, schema)
+    decoded = decode_record(value, domain, schema)
+    segments = [bytes([index])+encode_typed(kind,field) for index,(kind,field) in enumerate(zip(schema,decoded),1)]
     prefix_len = len(domain.encode()) + 1 + 1 + 2
+    framed_index = next(i for i,kind in enumerate(schema) if kind in ('U','B'))
+    overflow_segments = list(segments)
+    overflow_segments[framed_index] = (segments[framed_index][:1]+b'\xff\xff\xff\xff'
+                                       +segments[framed_index][5:])
     mutations = [
         value[:-1],                                      # missing/truncated
         value[:prefix_len] + b'\x02' + value[prefix_len+1:], # duplicate tag 02
-        value[:prefix_len] + b'\x03' + value[prefix_len+1:], # reordered/unknown first tag
-        value[:prefix_len+1] + b'\xff\xff\xff\xff' + value[prefix_len+5:], # overflowing framed length
+        value[:prefix_len]+segments[1]+segments[0]+b''.join(segments[2:]), # reordered fields
+        value[:prefix_len]+b''.join(overflow_segments),     # overflowing framed length
         value + b'\x00',                               # trailing
     ]
     for malformed in mutations:
@@ -553,22 +1157,202 @@ for value, domain, schema in families:
             malformed_rejected += 1
         else:
             raise AssertionError((domain, 'malformed accepted'))
-assert malformed_rejected == 15
-print(f'D12 codec verifier: {len(vectors)} answers, {len(vectors)} byte mutations rejected, {len(keys)} framed keys, {malformed_rejected} malformed records rejected')
+assert malformed_rejected == 200
+semantic_rejected = 0
+assert decode_record(R('HX-EV-SIGNED-PRIMITIVE-PROBE-1',1,I(-1)),
+    'HX-EV-SIGNED-PRIMITIVE-PROBE-1',['I']) == (-1,)
+capsule_domain = 'HX-EV-LEGACY-RESUME-CAPSULE-2'
+capsule_schema = schemas['D46-capsule']
+capsule_fields = list(decode_record(capsule,capsule_domain,capsule_schema))
+held_schema = ['U','U',('O','U'),('O','U'),'U','U','U','B32','U','U','N','B32',
+               ('O','U'),('O','U'),('O','B32'),('O','B32'),'Q','N','N',('O','B32'),'Q','N']
+held_fields = list(decode_record(vectors['D36-held'], 'HX-EV-HELD-DELIVERY-4', held_schema))
+for field_index, bad in [(10,-1),(10,0),(10,1001),(9,11),(7,'unknown')]:
+    changed = list(capsule_fields); changed[field_index] = bad
+    raw = R(capsule_domain, len(changed), *(encode_typed(k,v) for k,v in zip(capsule_schema,changed)))
+    try: decode_record(raw,capsule_domain,capsule_schema)
+    except AssertionError: semantic_rejected += 1
+    else: raise AssertionError('bad signed/range/classification accepted')
+for field_index, bad in [(2,None),(0,'deployment'),(12,None),(8,'unknown')]:
+    changed = list(held_fields); changed[field_index] = bad
+    raw = R('HX-EV-HELD-DELIVERY-4', len(changed), *(encode_typed(k,v) for k,v in zip(held_schema,changed)))
+    try: decode_record(raw, 'HX-EV-HELD-DELIVERY-4', held_schema)
+    except AssertionError: semantic_rejected += 1
+    else: raise AssertionError('bad optional/scope/state accepted')
+changed = list(capsule_fields); changed[9],changed[10] = 1010,1001
+raw = R(capsule_domain,len(changed),*(encode_typed(k,v) for k,v in zip(capsule_schema,changed)))
+try: decode_record(raw,capsule_domain,capsule_schema)
+except AssertionError: semantic_rejected += 1
+else: raise AssertionError('above-maximum count with matching range accepted')
+# Mutate a real optional marker, preserving every following byte.
+optional_offset = len(capsule_domain.encode()) + 4
+for kind,value in zip(capsule_schema[:4],capsule_fields[:4]):
+    optional_offset += 1 + len(encode_typed(kind,value))
+optional_offset += 1
+bad_marker = capsule[:optional_offset] + b'\x02' + capsule[optional_offset+1:]
+try: decode_record(bad_marker,capsule_domain,capsule_schema)
+except AssertionError: semantic_rejected += 1
+else: raise AssertionError('bad optional marker accepted')
+assert semantic_rejected == 11
+def record_with(label, index, value):
+    domain = vectors[label].split(b'\0',1)[0].decode(); schema = schemas[label]
+    fields = list(decode_record(vectors[label],domain,schema)); fields[index] = value
+    return R(domain,len(fields),*(encode_typed(kind,value) for kind,value in zip(schema,fields))),domain,schema
+
+for label,index in [('D12-legacy-claim',0),('D36-held',2),('D36-held',12),('D37-entry',3),('D36-held',13),('D36-quarantine',8)]:
+    maximum = 4096 if (label,index) in {('D37-entry',3),('D36-held',13),('D36-quarantine',8)} else 1024
+    for size in (maximum-1,maximum,maximum+1):
+        raw,domain,schema = record_with(label,index,'i'*size)
+        try: decode_record(raw,domain,schema)
+        except AssertionError:
+            assert size == maximum+1; semantic_rejected += 1
+        else: assert size <= maximum
+
+semantic_cases = [('D29-capability',11,1),('D29-capability',8,64*MiB-1),
+    ('D29-capability',13,50001),('D29-capability',12,315576001),
+    ('D37-directory',0,256),('D37-directory',4,H('not-genesis')),
+    ('D29-charge',11,H('not-genesis')),('D29-charge',12,H('wrong-owner')),
+    ('D29-charge',10,'staged'),('D29-counter',5,1),('D12-usage',1,256),
+    ('D12-tombstone',7,256),('D31-queue',9,H('not-genesis')),
+    ('D36-policy',6,'active'),('D37-entry',11,'unknown'),
+    ('D45-window',4,bytes(32)),('D45-audit',2,0),('D29-pin-batch',5,0),
+    ('D37-entry',2,'unknown'),('D37-entry',5,'unknown'),('D37-entry',11,'coordinator')]
+for label,index,value in semantic_cases:
+    raw,domain,schema = record_with(label,index,value)
+    try: decode_record(raw,domain,schema)
+    except AssertionError: semantic_rejected += 1
+    else: raise AssertionError((label,index,'semantic mutation survived'))
+
+# Supported large B fields are decoded as real, canonical family records.
+large_rows = b''.join(N(i+1)+U('t'*1024)+H('scope-'+str(i))+U('queued') for i in range(1000))
+large_queue = R('HX-EV-PIN-CAPACITY-QUEUE-4',11,U('deployment-a'),U('deployment'),N(1),N(1000),
+    B(large_rows),N(0),N(0),N(50000),N(1000),z,Q(t))
+assert len(large_queue) == 1078163 > MiB
+assert decode_record(large_queue,'HX-EV-PIN-CAPACITY-QUEUE-4',schemas['D31-queue'])[3] == 1000
+large_index_rows = b''.join(Q(t+i)+U('HeldDelivery')+U('subject-'+str(i)+'x'*4000)+H(str(i)) for i in range(300))
+large_index = R('HX-EV-HOLD-INDEX-2',8,U('tenant'),U('t'),N(1),N(300),B(large_index_rows),N(0),z,Q(t))
+assert len(large_index) > MiB and decode_record(large_index,'HX-EV-HOLD-INDEX-2',schemas['D37-index'])[3] == 300
+actor_ids = sorted('deployment:'+sha256(str(i).encode()).hexdigest() for i in range(20000))
+large_directory = R('HX-EV-HOLD-DIRECTORY-1',7,N(7),N(1),N(20000),B(b''.join(U(a) for a in actor_ids)),z,Q(t),N(0))
+assert len(large_directory) > MiB
+assert decode_record(large_directory,'HX-EV-HOLD-DIRECTORY-1',schemas['D37-directory'])[2] == 20000
+for raw,domain,schema in [(large_queue,'HX-EV-PIN-CAPACITY-QUEUE-4',schemas['D31-queue']),
+                         (large_index,'HX-EV-HOLD-INDEX-2',schemas['D37-index']),
+                         (large_directory,'HX-EV-HOLD-DIRECTORY-1',schemas['D37-directory'])]:
+    malformed = raw + bytes(record_caps[domain]+1-len(raw))
+    try: decode_record(malformed,domain,schema)
+    except AssertionError: semantic_rejected += 1
+    else: raise AssertionError('family ceiling-plus-one accepted')
+above_count_fields = list(capsule_fields)
+above_count_fields[9],above_count_fields[10] = 1010,1001
+sequence = 10; manifest_rows = []
+for ordinal,count in enumerate([61]*16+[25]):
+    manifest_rows.append(N(ordinal)+N(sequence)+N(count)+H('chunk-'+str(ordinal))+N(128+count*1068)+U('chunks/'+str(ordinal)))
+    sequence += count
+above_count_fields[12] = pack('>I',17)+b''.join(manifest_rows)
+above_count_capsule = R(capsule_domain,len(above_count_fields),*(encode_typed(k,v) for k,v in zip(capsule_schema,above_count_fields)))
+try: decode_record(above_count_capsule,capsule_domain,capsule_schema)
+except AssertionError: semantic_rejected += 1
+else: raise AssertionError('semantically complete 1001-row capsule accepted')
+assert semantic_rejected == 42
+loop5_semantic_rejected = 0
+def reject_semantic(raw,domain,schema,attempt_store=None):
+    global loop5_semantic_rejected
+    try: decode_record(raw,domain,schema,attempt_store)
+    except (AssertionError,UnicodeError): loop5_semantic_rejected += 1
+    else: raise AssertionError((domain,'loop-5 semantic defect accepted'))
+charge_schema = schemas['D29-charge']
+charge_fields = list(decode_record(vectors['D29-charge'],'HX-EV-PUBLICATION-CHARGE-2',charge_schema))
+for kind,ceiling in [('pin-batch',449*MiB),('side-record',193*MiB),('retained-object',193*MiB),
+                     ('oversize-quarantine',256*MiB),('resume-window',1024*MiB)]:
+    for length in (ceiling-1,ceiling,ceiling+1):
+        fields = list(charge_fields); fields[4],fields[5],fields[6],fields[7] = kind,length,1114112,length+1114112
+        raw = R('HX-EV-PUBLICATION-CHARGE-2',14,*(encode_typed(k,v) for k,v in zip(charge_schema,fields)))
+        if length <= ceiling: assert decode_record(raw,'HX-EV-PUBLICATION-CHARGE-2',charge_schema)[5] == length
+        else: reject_semantic(raw,'HX-EV-PUBLICATION-CHARGE-2',charge_schema)
+for overhead in (1114111,1114112,1114113):
+    fields = list(charge_fields); fields[6],fields[7] = overhead,fields[5]+overhead
+    raw = R('HX-EV-PUBLICATION-CHARGE-2',14,*(encode_typed(k,v) for k,v in zip(charge_schema,fields)))
+    if overhead <= 1114112: assert decode_record(raw,'HX-EV-PUBLICATION-CHARGE-2',charge_schema)[6] == overhead
+    else: reject_semantic(raw,'HX-EV-PUBLICATION-CHARGE-2',charge_schema)
+for count in (0,1,63,64,65):
+    raw,domain,schema = record_with('D36-policy',9,count)
+    if 1 <= count <= 64: assert decode_record(raw,domain,schema)[9] == count
+    else: reject_semantic(raw,domain,schema)
+fields = list(decode_record(state,'HX-EV-PUBLICATION-RESUME-STATE-3',schemas['D45-state']))
+other_retry = H('other-request') + retry_row[32:]
+fields[11] = pack('>I',2)+retry_row+other_retry
+duplicate_ordinal = R('HX-EV-PUBLICATION-RESUME-STATE-3',14,*(encode_typed(k,v) for k,v in zip(schemas['D45-state'],fields)))
+reject_semantic(duplicate_ordinal,'HX-EV-PUBLICATION-RESUME-STATE-3',schemas['D45-state'])
+attempt_schema = ['U','B32','N','B32','N','B','B32','Q']
+assert decode_record(attempt_set,'HX-EV-WINDOW-ATTEMPT-SET-1',attempt_schema)[6] == attempt_root
+for altered_rows,nrows in [(attempt_rows[1:],12),(b''.join(attempt_records[3:]),9),
+                          (attempt_rows[:96]+bytes([attempt_rows[96]^1])+attempt_rows[97:],12)]:
+    root = sha256(b'HX-EV-WINDOW-ATTEMPTS-2\0\x01'+U('t')+H('scope')+N(1)+H('roster')+N(nrows)+B(altered_rows)).digest()
+    changed = R('HX-EV-WINDOW-ATTEMPT-SET-1',8,U('t'),H('scope'),N(1),H('roster'),N(nrows),B(altered_rows),root,Q(t))
+    reject_semantic(closure,'HX-EV-PUBLICATION-WINDOW-CLOSURE-3',schemas['D45-closure'],{('t',H('scope'),1):changed})
+assert loop5_semantic_rejected == 12
+def extra_with(label,index,value):
+    raw = extra_vectors[label]; domain = raw.split(b'\0',1)[0].decode(); schema = extra_schemas[label]
+    fields = list(decode_record(raw,domain,schema)); fields[index] = value
+    return R(domain,len(fields),*(encode_typed(k,v) for k,v in zip(schema,fields))),domain,schema
+for label,index,bad in [('D45-origin',3,z),('D45-origin',2,z),('D45-origin',6,b'["map",[]]'),
+                      ('D45-preparation',4,z),('D45-preparation',11,3),
+                      ('D45-preparation-head',5,'unknown'),('D45-preparation-head',8,pack('>I',0)+b'x'),
+                      ('D36-capture-preparation',7,193*MiB+1),('D36-repair',8,H('forged')),
+                      ('D45-invocation',7,z)]:
+    reject_semantic(*extra_with(label,index,bad))
+# Keep the predecessor digest correct: only canonical-image validation rejects
+# this otherwise parseable whitespace mutation.
+fields = list(decode_record(extra_vectors['D45-preparation'],'HX-EV-RESUME-PREPARATION-1',extra_schemas['D45-preparation']))
+fields[5] += b' '; fields[4] = sha256(fields[5]).digest()
+reject_semantic(R('HX-EV-RESUME-PREPARATION-1',12,*(encode_typed(k,v) for k,v in zip(extra_schemas['D45-preparation'],fields))),
+                'HX-EV-RESUME-PREPARATION-1',extra_schemas['D45-preparation'])
+fields = list(decode_record(extra_vectors['D45-preparation'],'HX-EV-RESUME-PREPARATION-1',extra_schemas['D45-preparation']))
+changed_successor = read_canonical_image(fields[6]); changed_successor['ordinal'] = 4
+fields[6] = canonical_image_bytes(changed_successor); fields[11] = 4
+reject_semantic(R('HX-EV-RESUME-PREPARATION-1',12,*(encode_typed(k,v) for k,v in zip(extra_schemas['D45-preparation'],fields))),
+                'HX-EV-RESUME-PREPARATION-1',extra_schemas['D45-preparation'])
+for hold,reason,owner in [('PublicationResumePreparationHold','publication_resume_preparation_hold','coordinator'),
+                          ('RedriveEvidenceRepairHold','redrive-evidence-repair-hold','operations')]:
+    entry = list(decode_record(vectors['D37-entry'],'HX-EV-HOLD-ENTRY-2',schemas['D37-entry']))
+    entry[2],entry[5],entry[11] = hold,reason,owner
+    valid = R('HX-EV-HOLD-ENTRY-2',13,*(encode_typed(k,v) for k,v in zip(schemas['D37-entry'],entry)))
+    assert decode_record(valid,'HX-EV-HOLD-ENTRY-2',schemas['D37-entry'])[11] == owner
+    for index,bad in [(5,'unknown'),(11,'actor')]:
+        altered = list(entry); altered[index] = bad
+        reject_semantic(R('HX-EV-HOLD-ENTRY-2',13,*(encode_typed(k,v) for k,v in zip(schemas['D37-entry'],altered))),
+                        'HX-EV-HOLD-ENTRY-2',schemas['D37-entry'])
+assert loop5_semantic_rejected == 28
+print(f'D12 codec verifier: {len(vectors)} original answers, {len(extra_vectors)} supplementary answers, {len(vectors)+len(extra_vectors)} byte probes passed, {len(keys)} framed keys, {malformed_rejected} malformed records rejected, {semantic_rejected+loop5_semantic_rejected} semantic defects rejected')
 PY
 ```
 
-The lifecycle verifier covers every approved matrix row, all status precedence branches, tenant/deployment queue movement with both counterpart slots pre-reserved, the unidentified ceiling, stable arithmetic/evidence holds with unchanged counters, exact refunds, legacy evidence failure, both configuration- and membership-revision exits, and the 54 routed finding IDs. Its explicit mutants are contract defects, not alternative implementations.
+The lifecycle verifier covers every approved matrix row, status precedence, both pre-reserved queue counterparts, authenticated three-counter mutation, exact retry/current-time orphan expiry, lossless chunks/exclusive legacy recovery, charged preparation cleanup, partial capture, retained-byte redrive/repair, selected policies and configuration/membership exits. Its preparation provider retains only actual bounded origin/reconstruction/progress, D7 charge/counter and D11 entry/index bytes, addressed artifacts and authenticated native readback/deletion receipts; restart discards deliberately poisoned process caches. Existing A8, predecessor and C2/C5 imports are authenticated readbacks, never planned future success bytes. New audit/state/resolution/window/closure records derive from the original request and recorded successor intent. It exercises 46 restart/write boundaries, four partial-cleanup boundaries, 70 durable-evidence refusals, three current-time completions, and a prior-success claim with distinct A8/D9 heads and nonzero predecessor audit. It verifies 54 historical dispositions and all 21 loop-3, 20 loop-4 and 21 loop-5 repair IDs. Six records produced by the actual transitions must equal the independently constructed codec fixtures; their 30 repeated malformed probes also reject. Sixty-seven directed source mutations rerun their owning assertions; the older 27 Boolean checks remain invariant checks, not source fault injection.
 
 ```bash
 python3 - <<'PY'
 from collections import defaultdict
+from copy import deepcopy
 from hashlib import sha256
 from pathlib import Path
+from contextlib import redirect_stdout
+from io import StringIO
 import re
+import json
 
 MiB = 1024 * 1024
 U64_MAX = (1 << 64) - 1
+candidate_text = Path('_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md').read_text()
+codec_source = candidate_text.split("python3 - <<'PY'\n",1)[1].split('\nPY\n```',1)[0]
+codec = {}
+with redirect_stdout(StringIO()):
+    exec(codec_source, codec)
+U, B, N, O, R, I, Q = (codec[name] for name in ('U','B','N','O','R','I','Q'))
+decode_record = codec['decode_record']
+unresolved_root = codec['unresolved_root']
+supplementary_records = {}
+preparation_metrics = {}
 
 def valid_amounts(amounts):
     if not amounts or any(type(value) is not int or value < 0 or value > U64_MAX for value in amounts):
@@ -647,6 +1431,46 @@ class Ledger:
         self.deployment_ceiling = 2048 * MiB
         self.reserve_bytes = 256 * MiB
         self.unidentified_ceiling = 512 * MiB
+        self.generations = defaultdict(int)
+        self.charges = {}
+        self.reservations = {}
+        self.objects = {}
+        self.inventory = {}
+        self.inventory_ceiling = 10000
+    def predecessors(self, account):
+        used = {'tenant':self.tenant.get(account,0), 'tenant-pool':self.tenant_pool,
+                'deployment':self.deployment}
+        return {kind:sha256(U(kind) + U(account if kind == 'tenant' else kind)
+                    + N(value) + N(self.generations.get((kind,account if kind == 'tenant' else kind),0))).digest()
+                for kind,value in used.items()}
+    def pin_identity(self, account, amounts, batch_id, scope=None, candidate=None, request_id=None):
+        scope = sha256(b'scope:'+batch_id).digest() if scope is None else scope
+        candidate = sha256(b'candidate:'+batch_id).digest() if candidate is None else candidate
+        request_id = sha256(U(account)+scope+candidate+b''.join(N(v) for v in amounts)).digest() if request_id is None else request_id
+        return ('tenant',account,scope,candidate,request_id,tuple(amounts))
+    def read_pin_reservation(self, account, amounts, batch_id, scope=None, candidate=None, request_id=None):
+        if not valid_amounts(amounts): return False
+        identity = self.pin_identity(account,amounts,batch_id,scope,candidate,request_id)
+        row = self.reservations.get(batch_id)
+        return bool(row and row['identity'] == identity and row['state'] == 'reserved'
+                    and row['receipt'] == state_hash({key:value for key,value in row.items() if key != 'receipt'})
+                    and self.charges.get(batch_id) == tuple(amounts))
+    def reserve_pin_batch(self, account, amounts, predecessors, batch_id, scope=None, candidate=None, request_id=None):
+        before = deepcopy(vars(self))
+        if predecessors != self.predecessors(account) or not valid_amounts(amounts):
+            assert vars(self) == before
+            return False
+        if batch_id in self.reservations:
+            return self.read_pin_reservation(account,amounts,batch_id,scope,candidate,request_id)
+        if not self.reserve('tenant', account, amounts):
+            assert vars(self) == before
+            return False
+        self.charges[batch_id] = tuple(amounts)
+        row = {'amounts':tuple(amounts), 'predecessors':dict(predecessors), 'state':'reserved',
+               'identity':self.pin_identity(account,amounts,batch_id,scope,candidate,request_id)}
+        row['receipt'] = state_hash(row)
+        self.reservations[batch_id] = row
+        return True
     def reserve(self, account_kind, account, amounts):
         before = (dict(self.tenant), self.tenant_pool, self.unidentified, self.deployment)
         if account_kind not in {'tenant', 'capture-scope'}:
@@ -665,23 +1489,34 @@ class Ledger:
             fits = (current + total <= self.tenant_ceiling
                     and self.unidentified + total <= self.unidentified_ceiling
                     and self.deployment + total <= self.deployment_ceiling)
-        if not fits:
+        changed = [('tenant',account),('tenant-pool' if account_kind == 'tenant' else 'unidentified',
+                   'tenant-pool' if account_kind == 'tenant' else 'unidentified'),('deployment','deployment')]
+        if not fits or any(self.generations.get(key,0) == U64_MAX for key in changed):
             assert before == (dict(self.tenant), self.tenant_pool, self.unidentified, self.deployment)
             return False
         self.tenant[account] += total
         if account_kind == 'tenant': self.tenant_pool += total
         else: self.unidentified += total
         self.deployment += total
+        for kind, identity in changed:
+            self.generations[(kind,identity)] += 1
         return True
     def refund(self, account_kind, account, amount):
         before = (dict(self.tenant), self.tenant_pool, self.unidentified, self.deployment)
-        if account_kind not in {'tenant', 'capture-scope'} or not isinstance(amount, int) or not 0 <= amount <= self.tenant[account]:
+        changed = [('tenant',account),('tenant-pool' if account_kind == 'tenant' else 'unidentified',
+                   'tenant-pool' if account_kind == 'tenant' else 'unidentified'),('deployment','deployment')]
+        if (account_kind not in {'tenant', 'capture-scope'} or type(amount) is not int
+                or not 0 <= amount <= self.tenant.get(account,0)
+                or amount > (self.tenant_pool if account_kind == 'tenant' else self.unidentified)
+                or amount > self.deployment
+                or any(self.generations.get(key,0) == U64_MAX for key in changed)):
             assert before == (dict(self.tenant), self.tenant_pool, self.unidentified, self.deployment)
             return False
         self.tenant[account] -= amount
         if account_kind == 'tenant': self.tenant_pool -= amount
         else: self.unidentified -= amount
         self.deployment -= amount
+        for key in changed: self.generations[key] += 1
         return True
 
 ledger = Ledger()
@@ -712,16 +1547,25 @@ class ChargeSwap:
         self.finalized = False
     def stage(self, new_amount, owner):
         before = vars(self).copy()
-        if (not isinstance(new_amount, int) or new_amount < 0
+        if self.staged:
+            return owner == self.owner and new_amount == self.staged
+        if (type(new_amount) is not int or new_amount <= 0
                 or new_amount > U64_MAX-self.used or self.used+new_amount > self.ceiling):
             assert before == vars(self)
             return False
         self.staged = new_amount; self.used += new_amount; self.owner = owner
         return True
-    def recover(self, owner, successor_read_back):
+    def recover(self, owner, successor_read_back, success_audit_read_back=None):
         if owner != self.owner or self.staged == 0:
             return False
-        if successor_read_back:
+        successor = validate_presence(successor_read_back,owner,'successor',getattr(self,'generation',1))
+        audit = validate_presence(success_audit_read_back,owner,'audit',getattr(self,'generation',1))
+        if successor is None or audit is None:
+            return False
+        if audit == 'present' and successor == 'absent':
+            return False  # a recorded partial success cannot discard its stage
+        if successor == 'present':
+            if audit != 'present': return False
             if not self.finalized:
                 self.used -= self.old
                 self.active = self.staged
@@ -734,31 +1578,85 @@ class ChargeSwap:
             self.owner = None
         return True
 
+def presence(owner, kind, outcome, generation=1):
+    payload = ('authenticated-readback',owner,kind,generation,outcome)
+    return payload + (sha256(repr(payload).encode()).digest(),)
+
+def validate_presence(receipt, owner, kind, expected_generation=1):
+    if not isinstance(receipt,tuple) or len(receipt) != 6: return None
+    label,claimed_owner,claimed_kind,generation,outcome,digest = receipt
+    if (label != 'authenticated-readback' or claimed_owner != owner or claimed_kind != kind
+            or generation != expected_generation or outcome not in {'present','absent'}
+            or digest != sha256(repr(receipt[:-1]).encode()).digest()): return None
+    return outcome
+
 rolled_back_swap = ChargeSwap(600, 1000)
 assert rolled_back_swap.stage(400, b'request-a') and rolled_back_swap.used == 1000
 assert rolled_back_swap.active == 600
-assert rolled_back_swap.recover(b'request-a', False) and rolled_back_swap.used == rolled_back_swap.active == 600
+assert rolled_back_swap.recover(b'request-a', presence(b'request-a','successor','absent'), presence(b'request-a','audit','absent')) and rolled_back_swap.used == rolled_back_swap.active == 600
 completed_swap = ChargeSwap(600, 1200)
 assert completed_swap.stage(500, b'request-b') and completed_swap.active == 600
-assert completed_swap.recover(b'request-b', True) and completed_swap.used == completed_swap.active == 500
+assert completed_swap.recover(b'request-b', presence(b'request-b','successor','present'), presence(b'request-b','audit','present')) and completed_swap.used == completed_swap.active == 500
 completed_state = vars(completed_swap).copy()
-assert not completed_swap.recover(b'request-b', True) and completed_state == vars(completed_swap)
+assert not completed_swap.recover(b'request-b', presence(b'request-b','successor','present'), presence(b'request-b','audit','present')) and completed_state == vars(completed_swap)
 refused_swap = ChargeSwap(600, 1000)
 refused_state = vars(refused_swap).copy()
 assert not refused_swap.stage(401, b'request-c') and refused_state == vars(refused_swap)
+audited_swap = ChargeSwap(600,1200)
+assert audited_swap.stage(500,b'audited-request')
+audited_snapshot = vars(audited_swap).copy()
+assert not audited_swap.recover(b'audited-request',presence(b'audited-request','successor','absent'),presence(b'audited-request','audit','present'))
+assert vars(audited_swap) == audited_snapshot
+assert audited_swap.recover(b'audited-request',presence(b'audited-request','successor','present'),presence(b'audited-request','audit','present'))
+assert audited_swap.active == audited_swap.used == 500
+audit_finalized = vars(audited_swap).copy()
+assert not audited_swap.recover(b'audited-request',presence(b'audited-request','successor','present'),presence(b'audited-request','audit','present'))
+assert vars(audited_swap) == audit_finalized and audited_swap.owner == b'audited-request'
+assert not rolled_back_swap.recover(b'request-a',False) and rolled_back_swap.used == 600
+owned_swap = ChargeSwap(600,1200)
+assert owned_swap.stage(500,b'owner')
+owned_snapshot = vars(owned_swap).copy()
+assert owned_swap.stage(500,b'owner') and vars(owned_swap) == owned_snapshot
+assert not owned_swap.stage(500,b'other-owner') and vars(owned_swap) == owned_snapshot
+assert not owned_swap.recover(b'other-owner',True,True) and vars(owned_swap) == owned_snapshot
+for successor_receipt,audit_receipt in [(None,None),(False,False),(True,True),
+        (presence(b'owner','successor','absent'),None),
+        (presence(b'other','successor','absent'),presence(b'owner','audit','absent')),
+        (presence(b'owner','successor','absent',2),presence(b'owner','audit','absent')),
+        (presence(b'owner','successor','absent')[:-1]+(bytes(32),),presence(b'owner','audit','absent'))]:
+    assert not owned_swap.recover(b'owner',successor_receipt,audit_receipt)
+    assert vars(owned_swap) == owned_snapshot
 
 class Queues:
     def __init__(self, ceiling=50000):
         self.ceiling = ceiling
         self.last_ticket = 0
         self.where = {}
+        self.tickets = {}
+        self.pending = {}
+        self.receipts = {}
+        self.charges = {}
         self.rows = defaultdict(list)
         self.reservations = defaultdict(set)
     @staticmethod
     def sort_key(row):
-        return (row[0], row[1].encode('utf-8'), sha256(row[2].encode()).digest())
+        scope_hash = row[2] if isinstance(row[2],bytes) else sha256(row[2].encode()).digest()
+        return (row[0], row[1].encode('utf-8'), scope_hash)
     def _has_room(self, target):
         return len(self.rows[target]) + len(self.reservations[target]) < self.ceiling
+    def allocate_ticket(self, scope, tenant, state='queued', charge_available=True):
+        carrier = (tenant, state)
+        if scope in self.tickets:
+            known = self.pending.get(scope, self.receipts.get(scope))
+            return self.tickets[scope] if known['carrier'] == carrier else None
+        if (self.last_ticket == U64_MAX or state not in {'queued','parked'}
+                or not charge_available or not self.reserve_pair(tenant, scope)):
+            return None
+        self.last_ticket += 1
+        self.tickets[scope] = self.last_ticket
+        self.pending[scope] = {'carrier':carrier, 'ticket':self.last_ticket}
+        self.charges[scope] = 4096 + 2*(8+4+1024+32+4+6)
+        return self.last_ticket
     def reserve_pair(self, tenant, scope):
         targets = ('deployment', 'tenant:' + tenant)
         if (scope in self.where or any(scope in values for values in self.reservations.values())
@@ -769,18 +1667,40 @@ class Queues:
         return True
     def add(self, ticket, tenant, scope, state='queued'):
         target = 'deployment'
-        if (not isinstance(ticket, int) or not self.last_ticket < ticket <= U64_MAX
-                or state not in {'queued','parked'} or not self.reserve_pair(tenant, scope)):
+        carrier = (tenant, state)
+        if scope in self.receipts:
+            return self.receipts[scope] == {'carrier':carrier, 'ticket':ticket}
+        if scope not in self.pending:
+            # Compatibility callers still traverse allocation and reservation.
+            if type(ticket) is not int or ticket != self.last_ticket+1:
+                return False
+            if self.allocate_ticket(scope, tenant, state) != ticket:
+                return False
+        claim = self.pending[scope]
+        if (type(ticket) is not int or not 0 < ticket <= self.last_ticket
+                or claim != {'carrier':carrier, 'ticket':ticket}):
+            # An untrusted conflicting caller cannot cancel the authenticated owner.
             return False
-        self.last_ticket = ticket
         self.reservations[target].remove(scope)
         row = [ticket, tenant, scope, state]
         self.rows[target].append(row); self.rows[target].sort(key=self.sort_key)
         self.where[scope] = target
+        self.receipts[scope] = self.pending.pop(scope)
         assert scope in self.reservations['tenant:' + tenant]
         assert all(len(self.rows[name]) + len(self.reservations[name]) <= self.ceiling
                    for name in (target, 'tenant:' + tenant))
         return True
+    def preparation_authority(self, scope):
+        claim = self.pending.get(scope)
+        return None if claim is None else sha256(U(scope)+repr(claim).encode()).digest()
+    def rollback(self, scope, tenant, ticket, expected_authority):
+        claim = self.pending.get(scope)
+        if (claim is None or claim['carrier'][0] != tenant or claim['ticket'] != ticket
+                or expected_authority != self.preparation_authority(scope)):
+            return False
+        for values in self.reservations.values(): values.discard(scope)
+        del self.pending[scope]; del self.tickets[scope]; del self.charges[scope]
+        return True
     def deployment_turn(self, deployment_fits, tenant_fits):
         eligible = [row for row in self.rows['deployment'] if row[3] == 'queued']
         if not eligible:
@@ -813,14 +1733,14 @@ class Queues:
 
 def decode_queue(rows, entry_count, parked_count, reserved_count=0, ceiling=50000,
                  last_ticket=U64_MAX, trailing=b''):
-    if (trailing or len(rows) != entry_count or not 0 <= ceiling <= 50000
+    if (trailing or len(rows) != entry_count or not 1 <= ceiling <= 50000
             or reserved_count < 0 or entry_count + reserved_count > ceiling):
         return 'pin_capacity_queue_corruption_hold'
     if parked_count != sum(row[3] == 'parked' for row in rows):
         return 'pin_capacity_queue_corruption_hold'
     if len({row[0] for row in rows}) != len(rows) or len({row[2] for row in rows}) != len(rows):
         return 'pin_capacity_queue_corruption_hold'
-    if any(row[0] > last_ticket for row in rows):
+    if any(not 0 < row[0] <= last_ticket or row[3] not in {'queued','parked'} for row in rows):
         return 'pin_capacity_queue_corruption_hold'
     if rows != sorted(rows, key=Queues.sort_key):
         return 'pin_capacity_queue_corruption_hold'
@@ -836,15 +1756,18 @@ def encode_queue_row(row):
             + len(state_bytes).to_bytes(4, 'big') + state_bytes)
 
 def encode_queue_record(rows, reserved_count, ceiling, last_ticket):
-    return (b'Q4' + len(rows).to_bytes(8,'big')
-            + sum(row[3] == 'parked' for row in rows).to_bytes(8,'big')
-            + reserved_count.to_bytes(8,'big') + ceiling.to_bytes(8,'big')
-            + last_ticket.to_bytes(8,'big') + b''.join(encode_queue_row(row) for row in rows))
+    return R('HX-EV-PIN-CAPACITY-QUEUE-4',11,U('deployment-a'),U('deployment'),N(1),
+        N(len(rows)),B(b''.join(encode_queue_row(row) for row in rows)),
+        N(sum(row[3] == 'parked' for row in rows)),N(reserved_count),N(ceiling),N(last_ticket),bytes(32),Q(codec['t']))
 
-def decode_queue_bytes(raw):
+def decode_queue_bytes(raw, authenticated_ceiling=3):
     offset = 0
     rows = []
     try:
+        fields = decode_record(raw,'HX-EV-PIN-CAPACITY-QUEUE-4',codec['schemas']['D31-queue'])
+        entry_count,parked_count,reserved_count,ceiling,last_ticket = (fields[i] for i in (3,5,6,7,8))
+        if ceiling != authenticated_ceiling: raise ValueError('capability mismatch')
+        raw = fields[4]
         def take(length):
             nonlocal offset
             if length < 0 or offset + length > len(raw):
@@ -854,19 +1777,14 @@ def decode_queue_bytes(raw):
             return value
         def take_u():
             length = int.from_bytes(take(4), 'big')
+            if not 1 <= length <= 1024: raise ValueError('identifier bounds')
             return take(length).decode('utf-8', errors='strict')
-        if take(2) != b'Q4': raise ValueError('domain')
-        entry_count = int.from_bytes(take(8), 'big')
-        parked_count = int.from_bytes(take(8), 'big')
-        reserved_count = int.from_bytes(take(8), 'big')
-        ceiling = int.from_bytes(take(8), 'big')
-        last_ticket = int.from_bytes(take(8), 'big')
         if entry_count > 50000 or entry_count + reserved_count > ceiling or ceiling > 50000:
             raise ValueError('ceiling')
         for _ in range(entry_count):
             ticket = int.from_bytes(take(8), 'big')
             tenant = take_u()
-            scope_hash = take(32).hex()
+            scope_hash = take(32)
             state = take_u()
             if state not in {'queued', 'parked'}:
                 raise ValueError('state')
@@ -874,7 +1792,7 @@ def decode_queue_bytes(raw):
         if offset != len(raw):
             raise ValueError('trailing')
         return decode_queue(rows, entry_count, parked_count, reserved_count, ceiling, last_ticket)
-    except (UnicodeDecodeError, ValueError):
+    except (AssertionError,UnicodeDecodeError, ValueError):
         return 'pin_capacity_queue_corruption_hold'
 
 queues = Queues(3)
@@ -918,15 +1836,67 @@ assert decode_queue(valid_rows, 1, 0, trailing=b'x') == 'pin_capacity_queue_corr
 assert decode_queue(valid_rows, 1, 0, reserved_count=3, ceiling=3) == 'pin_capacity_queue_corruption_hold'
 valid_raw = encode_queue_record(valid_rows, 1, 3, 1)
 assert decode_queue_bytes(valid_raw) == 'valid'
+assert decode_queue_bytes(valid_raw,authenticated_ceiling=4) == 'pin_capacity_queue_corruption_hold'
 assert decode_queue_bytes(valid_raw[:-1]) == 'pin_capacity_queue_corruption_hold'
 assert decode_queue_bytes(valid_raw+b'x') == 'pin_capacity_queue_corruption_hold'
 overflow_raw = encode_queue_record(valid_rows, 3, 3, 1)
 assert decode_queue_bytes(overflow_raw) == 'pin_capacity_queue_corruption_hold'
-malformed = valid_raw[:42] + (2**32-1).to_bytes(4, 'big') + valid_raw[46:]
+valid_fields = list(decode_record(valid_raw,'HX-EV-PIN-CAPACITY-QUEUE-4',codec['schemas']['D31-queue']))
+bad_row = valid_fields[4][:8] + b'\xff\xff\xff\xff' + valid_fields[4][12:]
+malformed_fields = list(valid_fields); malformed_fields[4] = bad_row
+malformed = R('HX-EV-PIN-CAPACITY-QUEUE-4',11,*(codec['encode_typed'](kind,value)
+              for kind,value in zip(codec['schemas']['D31-queue'],malformed_fields)))
 assert decode_queue_bytes(malformed) == 'pin_capacity_queue_corruption_hold'
+for field_index,bad in [(3,0),(3,2),(5,1),(4,valid_fields[4]+valid_fields[4]),(4,valid_fields[4][:-1])]:
+    corrupted = list(valid_fields); corrupted[field_index] = bad
+    malformed = R('HX-EV-PIN-CAPACITY-QUEUE-4',11,*(codec['encode_typed'](kind,value)
+        for kind,value in zip(codec['schemas']['D31-queue'],corrupted)))
+    assert decode_queue_bytes(malformed) == 'pin_capacity_queue_corruption_hold'
+assert decode_queue_bytes(codec['vectors']['D31-queue'],authenticated_ceiling=50000) == 'valid'
 ticket_exhausted = Queues()
 ticket_exhausted.last_ticket = U64_MAX
 assert not ticket_exhausted.add(U64_MAX, 't', 'never-wraps')
+allocator = Queues()
+ticket = allocator.allocate_ticket('stable-subject', 't')
+assert ticket == 1 and allocator.allocate_ticket('stable-subject', 't') == ticket
+assert allocator.add(ticket, 't', 'stable-subject')
+admitted_snapshot = deepcopy(vars(allocator))
+assert allocator.add(ticket, 't', 'stable-subject')
+assert vars(allocator) == admitted_snapshot  # successful admission lost-ack retry
+assert allocator.allocate_ticket('stable-subject', 'other') is None
+assert vars(allocator) == admitted_snapshot
+assert len(allocator.rows['deployment']) == 1 and not allocator.pending
+assert 'stable-subject' in allocator.reservations['tenant:t']
+refused = Queues(1)
+assert refused.add(1, 't', 'first')
+refused_snapshot = deepcopy(vars(refused))
+assert refused.allocate_ticket('full', 't') is None
+assert vars(refused) == refused_snapshot
+assert refused.allocate_ticket('uncharged', 'other', charge_available=False) is None
+assert vars(refused) == refused_snapshot
+assert refused.allocate_ticket('invalid', 'other', 'invalid') is None
+assert vars(refused) == refused_snapshot
+pending = Queues()
+pending_ticket = pending.allocate_ticket('owned-preparation','tenant-a')
+pending_authority = pending.preparation_authority('owned-preparation')
+pending_snapshot = deepcopy(vars(pending))
+for tenant,ticket,state in [('tenant-b',pending_ticket,'queued'),('tenant-a',pending_ticket+1,'queued'),
+                            ('tenant-a',pending_ticket,'parked')]:
+    assert not pending.add(ticket,tenant,'owned-preparation',state)
+    assert vars(pending) == pending_snapshot
+assert not pending.rollback('owned-preparation','tenant-b',pending_ticket,pending_authority)
+assert not pending.rollback('owned-preparation','tenant-a',pending_ticket,bytes(32))
+assert vars(pending) == pending_snapshot
+assert pending.rollback('owned-preparation','tenant-a',pending_ticket,pending_authority)
+assert not pending.pending and not pending.charges and not pending.tickets
+assert all(not values for values in pending.reservations.values())
+cleanup_snapshot = deepcopy(vars(pending))
+assert not pending.rollback('owned-preparation','tenant-a',pending_ticket,pending_authority)
+assert vars(pending) == cleanup_snapshot and pending.last_ticket == pending_ticket
+allocator.last_ticket = U64_MAX
+exhausted_snapshot = deepcopy(vars(allocator))
+assert allocator.allocate_ticket('new-subject', 't') is None
+assert vars(allocator) == exhausted_snapshot
 
 def membership_exit(trigger, zero_send, same_bytes):
     return 'ContinueSamePin' if trigger in {'configuration-revision', 'membership-revision'} and zero_send and same_bytes else 'FirstSendMembershipChangedHold'
@@ -959,76 +1929,1165 @@ assert render_recovery(None, 'x-outcome', receipt, receipt, 'x', 7) == 'response
 assert render_recovery('x-response', 'x-outcome', None, receipt, 'x', 7) == 'response_preparation_hold'
 assert render_recovery('x-response', 'x-outcome', receipt, ('receipt','x',6), 'x', 7) == 'response_preparation_hold'
 
-def resume_publication(committed, unresolved, accepted, hold, evidence):
+def checked_add(left, right, *, positive=False):
+    if (type(left) is not int or type(right) is not int or left < 0 or right < 0
+            or (positive and right == 0) or left > U64_MAX-right):
+        return None
+    return left + right
+
+def request(identity_key, reason=b'retry after repair', source=sha256(b'limit-hash').digest(), tenant='t', handle='h'):
+    carrier = R('HX-EV-PUBLICATION-RESUME-CARRIER-1',5,U(tenant),U(handle),source,U(identity_key),U(reason))
+    identity = sha256(b'HX-EV-PUBLICATION-RESUME-IDENTITY-1\0\x01'+U(tenant)+U(handle)+U(identity_key)).digest()
+    return identity, carrier
+
+def state_hash(state):
+    return sha256(image_bytes(state)).digest()
+
+def publication_invocation(claim, ordinal, limit, request_identity, unresolved):
+    rows = b''.join(codec['pack']('>I',position)+U(message)+sha256(body).digest()
+                    for position,message,body in sorted(unresolved))
+    root = sha256(b'HX-EV-PUBLICATION-UNRESOLVED-1\0\x01'+codec['pack']('>I',len(unresolved))+rows).digest()
+    return sha256(b'HX-EV-PUBLICATION-INVOCATION-1\0\x01'+claim+N(ordinal)+N(limit)+request_identity+root).digest()
+
+def image_bytes(value):
+    return codec['canonical_image_bytes'](value)
+
+def read_image(raw):
+    return codec['read_canonical_image'](raw)
+
+def existing_window_bytes(tenant, roster, unresolved=None):
+    return R('HX-EV-PUBLICATION-WINDOW-2',13,U(tenant),codec['H']('scope'),U('operation'),N(7),codec['H']('prior-closure'),
+        codec['H']('prior-window-state'),codec['H']('prior-window-request'),unresolved_root(roster[1:] if unresolved is None else unresolved),
+        codec['H']('policy'),N(16),U('admin'),Q(9990000000),codec['H']('capability'))
+
+def publication_state_bytes(state, now):
+    live = b''.join(identity+row['carrier_hash']+N(row['result']['ordinal'])+row['result']['audit_hash']+
+        N(row['result']['window'])+N(row['result']['limit'])+Q(row['expires_at']*10000000)
+        for identity,row in sorted(state['live'].items(),key=lambda item:item[1]['result']['ordinal']))
+    expired = b''.join(identity+row['carrier_hash']+Q(row['expires_at']*10000000)+Q(row['delete_after']*10000000)
+        for identity,row in sorted(state['tombstones'].items()))
+    latest = max(state['live'].values(),key=lambda row:row['result']['ordinal'],default=None)
+    raw = R('HX-EV-PUBLICATION-RESUME-STATE-3',14,U(state['tenant']),U('op'),codec['H']('scope'),
+        N(state['ordinal']),N(state['window']),N(state['limit']),state['hold_source'],state['window_claim'],
+        state.get('history',bytes(32)),N(state['closed']),state.get('last_audit',bytes(32) if latest is None else latest['result']['audit_hash']),
+        B(codec['pack']('>I',len(state['live']))+live),B(codec['pack']('>I',len(state['tombstones']))+expired),Q(now*10000000))
+    decode_record(raw,'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
+    return raw
+
+def existing_attempt_bytes(state, now):
+    # Fixture C2 provider authority is keyed by the actual committed roster and
+    # old window. It models already-existing registrations/results, never a
+    # planned D9 success record.
+    rows = []
+    for position,message,body in state['roster']:
+        parent = sha256(b'fixture-existing-registration:'+N(position)+U(message)+B(body)).digest()
+        send = sha256(b'fixture-existing-send:'+parent+N(state['window'])).digest()
+        rows.extend(codec['pack']('>I',position)+N(1)+N(observation)+U(kind)+parent+send+
+            sha256(b'fixture-existing-'+kind.encode()+parent).digest()
+            for observation,kind in enumerate(('register','result')))
+    exact = b''.join(rows); roster_root = sha256(image_bytes(state['roster'])).digest()
+    root = sha256(b'HX-EV-WINDOW-ATTEMPTS-2\0\x01'+U(state['tenant'])+codec['H']('scope')+
+        N(state['window'])+roster_root+N(len(rows))+B(exact)).digest()
+    return R('HX-EV-WINDOW-ATTEMPT-SET-1',8,U(state['tenant']),codec['H']('scope'),N(state['window']),
+        roster_root,N(len(rows)),B(exact),root,Q(now*10000000))
+
+def publication_closure_bytes(prior, authority, broker, now):
+    fields = decode_record(authority,'HX-EV-WINDOW-ATTEMPT-SET-1',['U','B32','N','B32','N','B','B32','Q'])
+    assert fields[:3] == (prior['tenant'],codec['H']('scope'),prior['window'])
+    assert fields[3] == sha256(image_bytes(prior['roster'])).digest()
+    rows = codec['decode_rows'](fields[5],fields[4],['P','N','N','U','B32','B32','B32'],count_ceiling=249216)
+    last = {}
+    for p,local,observation,kind,parent,send,evidence in rows:
+        if kind == 'result' and (p not in last or (local,observation) > last[p][:2]):
+            last[p] = (local,observation,evidence)
+    assert set(last) == {row[0] for row in prior['roster']}
+    final = b''.join(codec['pack']('>I',p)+N(local)+evidence for p,(local,observation,evidence) in sorted(last.items()))
+    raw = R('HX-EV-PUBLICATION-WINDOW-CLOSURE-3',11,U(prior['tenant']),codec['H']('scope'),N(prior['window']),
+        B(codec['pack']('>I',len(prior['roster']))+final),sha256(b'fence:'+broker).digest(),
+        sha256(b'disable:'+broker).digest(),sha256(b'empty:'+broker).digest(),fields[6],U('SignedCarrier'),
+        prior.get('history',bytes(32)),Q(now*10000000))
+    decode_record(raw,'HX-EV-PUBLICATION-WINDOW-CLOSURE-3',codec['schemas']['D45-closure'],
+        attempt_store={(prior['tenant'],codec['H']('scope'),prior['window']):authority})
+    return raw
+
+def publication_success_material(prior, identity, carrier_hash, hold, ordinal, window, limit, now, attempt_authority=None):
+    predecessor = publication_state_bytes(prior,now)
+    closure = None
+    claim = prior['window_claim_bytes']
+    if hold == 'publication_retry_exhausted_hold':
+        broker = b'fixture-existing-window-authority:'+codec['H']('scope')+N(prior['window'])
+        closure = publication_closure_bytes(prior,existing_attempt_bytes(prior,now) if attempt_authority is None else attempt_authority,broker,now)
+        claim = R('HX-EV-PUBLICATION-WINDOW-2',13,U(prior['tenant']),codec['H']('scope'),U('operation'),N(window),
+            sha256(closure).digest(),sha256(predecessor).digest(),identity,unresolved_root(prior['unresolved']),
+            codec['H']('policy'),N(limit),U('admin'),Q(now*10000000),codec['H']('capability'))
+    audit = R('HX-EV-PUBLICATION-RESUME-AUDIT-4',10,U(prior['tenant']),U('op'),N(ordinal),identity,carrier_hash,
+        sha256(predecessor).digest(),O(None if closure is None else sha256(closure).digest()),N(window),N(limit),Q(now*10000000))
+    return predecessor,closure,claim,audit
+
+imported_fields = ('roster','accepted','unresolved','window_claim_bytes')
+def prepare_bytes(prior, successor, identity, carrier_hash, now, expiry, amount):
+    # Images contain only bounded continuation fields. Imported bytes stay at
+    # their existing immutable addresses; preparation retains exact field roots.
+    def project(state): return {k:v for k,v in state.items() if k not in imported_fields and k != 'orphans'}
+    imports = {name:sha256(image_bytes(prior[name])).digest() for name in imported_fields}
+    # A successor window is a newly prepared immutable artifact, not a changed import.
+    successor_imports = {name:sha256(image_bytes(successor[name])).digest() for name in imported_fields}
+    manifest = image_bytes({'prior':imports,'successor':successor_imports,
+                            'window':successor['window_claim_bytes']})
+    raw = R('HX-EV-RESUME-PREPARATION-1',12,U(prior['tenant']),U('op'),identity,carrier_hash,
+        sha256(image_bytes(project(prior))).digest(),B(image_bytes(project(prior))),B(image_bytes(project(successor))),
+        Q(now*10000000),Q(expiry*10000000),N(amount),B(manifest),N(successor['ordinal']))
+    assert len(raw) <= MiB
+    return raw
+
+def read_preparation(raw, state, identity, carrier_hash):
+    schema = ['U','U','B32','B32','B32','B','B','Q','Q','N','B','N']
+    fields = decode_record(raw,'HX-EV-RESUME-PREPARATION-1',schema)
+    assert fields[0] == state['tenant'] and fields[1] == 'op'
+    assert fields[2] == identity and fields[3] == carrier_hash
+    assert fields[4] == sha256(fields[5]).digest() and fields[8] > fields[7]
+    assert fields[8]-fields[7] <= 9000000000 and fields[9] > 0 and fields[11] > 0
+    prior,successor,manifest = read_image(fields[5]),read_image(fields[6]),read_image(fields[10])
+    for name in imported_fields:
+        assert manifest['prior'][name] == sha256(image_bytes(state[name])).digest()
+        prior[name] = deepcopy(state[name])
+        value = manifest['window'] if name == 'window_claim_bytes' else state[name]
+        assert manifest['successor'][name] == sha256(image_bytes(value)).digest()
+        successor[name] = deepcopy(value)
+    prior['orphans'],successor['orphans'] = {},{}
+    assert successor['ordinal'] == fields[11] and successor['active_charge'] == fields[9]
+    return prior,successor
+
+def resume_publication(state, request_identity, carrier, hold, evidence, drain_increment=8,
+                       now=1000, expires_at=1900, crash_after_audit=False, attempt_authority=None):
+    before = deepcopy(state)
+    carrier_hash = sha256(carrier).digest()
+    try:
+        tenant,handle,source,key,reason = decode_record(carrier,'HX-EV-PUBLICATION-RESUME-CARRIER-1',
+                                                       ['U','U','B32','U','U'])
+        recomputed = sha256(b'HX-EV-PUBLICATION-RESUME-IDENTITY-1\0\x01'+U(tenant)+U(handle)+U(key)).digest()
+        if request_identity != recomputed:
+            return {'outcome':'resume_request_conflict','state':before,'command_executions':0}
+    except (AssertionError,UnicodeError):
+        return {'outcome':'resume_evidence_hold','state':before,'command_executions':0}
+    for collection, outcome in [('live','exact-retry'), ('orphans','orphan-audit-retry')]:
+        if request_identity in state[collection]:
+            row = state[collection][request_identity]
+            if row['carrier_hash'] != carrier_hash:
+                return {'outcome':'resume_request_conflict','state':before,'command_executions':0}
+            if collection == 'live':
+                if now >= row['expires_at']:
+                    return {'outcome':'resume_request_expired','state':reconcile_tombstones(state,now),
+                            'command_executions':0}
+                return {'outcome':outcome,'state':before,'response':row['response'],'command_executions':0}
+            # An authenticated audit is a partially committed success. Its exact
+            # successor and staged charge complete before any recorded invocation arms.
+            immutable = {key:value for key,value in row.items() if key != 'receipt'}
+            if row['receipt'] != state_hash(immutable) or row['owner'] != request_identity:
+                return {'outcome':'resume_evidence_hold','state':before,'command_executions':0}
+            try:
+                prior,successor_intent = read_preparation(row['preparation'],state,request_identity,carrier_hash)
+            except (AssertionError,ValueError,TypeError,KeyError,UnicodeError):
+                return {'outcome':'resume_evidence_hold','state':before,'command_executions':0}
+            protected = set(prior) - {'live','tombstones','orphans','audits','used_charge','reconciliation'}
+            if any(state.get(key) != prior[key] for key in protected):
+                return {'outcome':'resume_hold_changed','state':before,'command_executions':0}
+            expected_indexes = prior
+            if state.get('reconciliation') is not None:
+                proof = state['reconciliation']
+                payload = {key:value for key,value in proof.items() if key != 'receipt'}
+                if proof['receipt'] != state_hash(payload):
+                    return {'outcome':'resume_evidence_hold','state':before,'command_executions':0}
+                expected_indexes = reconcile_tombstones(prior,proof['at'])
+            if any(state[key] != expected_indexes[key] for key in ('live','tombstones')):
+                return {'outcome':'resume_evidence_hold','state':before,'command_executions':0}
+            # CAS applies only the recorded protected delta to the current head.
+            recovered = deepcopy(successor_intent)
+            recovered['live'] = deepcopy(state['live'])
+            recovered['tombstones'] = deepcopy(state['tombstones'])
+            recovered['live'][request_identity] = deepcopy(successor_intent['live'][request_identity])
+            if state.get('reconciliation') is not None:
+                recovered['reconciliation'] = deepcopy(state['reconciliation'])
+            recovered = reconcile_tombstones(recovered,now)
+            assert recovered['ordinal'] == row['result']['ordinal']
+            assert recovered['active_charge'] == row['staged_charge']
+            return {'outcome':outcome,'state':recovered,'response':row['response'],
+                    'command_executions':0}
+    if request_identity in state['tombstones']:
+        row = state['tombstones'][request_identity]
+        result = 'resume_request_expired' if row['carrier_hash'] == carrier_hash else 'resume_request_conflict'
+        return {'outcome':result, 'state':before, 'command_executions':0}
+    if state['orphans']:
+        return {'outcome':'resume_evidence_hold','state':before,'command_executions':0}
     if evidence == 'stale':
-        return {'outcome':'resume_hold_changed', 'rearmed':(), 'command_executions':0}
+        return {'outcome':'resume_hold_changed', 'state':before, 'command_executions':0}
     if evidence == 'unavailable':
-        return {'outcome':'resume_evidence_hold', 'rearmed':(), 'command_executions':0}
+        return {'outcome':'resume_evidence_hold', 'state':before, 'command_executions':0}
+    if tenant != state['tenant'] or handle != state['handle'] or source != state['hold_source']:
+        return {'outcome':'resume_hold_changed','state':before,'command_executions':0}
+    if not now < expires_at <= now+900:
+        return {'outcome':'resume_request_expired','state':before,'command_executions':0}
     if hold not in {'publication_retry_exhausted_hold', 'publication_drain_limit_hold'}:
-        return {'outcome':'resume_not_eligible', 'rearmed':(), 'command_executions':0}
-    committed_positions = [row[0] for row in committed]
-    committed_ids = [row[1] for row in committed]
-    if len(set(committed_positions)) != len(committed) or len(set(committed_ids)) != len(committed):
-        return {'outcome':'resume_evidence_hold', 'rearmed':(), 'command_executions':0}
-    committed_by_position = {row[0]: row for row in committed}
+        return {'outcome':'resume_not_eligible', 'state':before, 'command_executions':0}
+    if sha256(state['window_claim_bytes']).digest() != state['window_claim']:
+        return {'outcome':'resume_evidence_hold','state':before,'command_executions':0}
+    committed = state['roster']; unresolved = state['unresolved']; accepted = state['accepted']
+    positions = [row[0] for row in committed]; message_ids = [row[1] for row in committed]
     selected = tuple(unresolved) + tuple(accepted)
-    if (len(set(unresolved)) != len(unresolved) or len(set(accepted)) != len(accepted)
-            or set(unresolved) & set(accepted)
-            or set(selected) != set(committed_positions)
+    if (len(set(positions)) != len(committed) or len(set(message_ids)) != len(committed)
+            or len(set(unresolved)) != len(unresolved) or len(set(accepted)) != len(accepted)
+            or set(unresolved) & set(accepted) or set(selected) != set(committed)
             or len(selected) != len(committed)):
-        return {'outcome':'resume_evidence_hold', 'rearmed':(), 'command_executions':0}
+        return {'outcome':'resume_evidence_hold', 'state':before, 'command_executions':0}
+    try:
+        fields = decode_record(state['window_claim_bytes'],'HX-EV-PUBLICATION-WINDOW-2',codec['schemas']['D45-window'])
+        assert fields[7] == unresolved_root(unresolved)
+    except (AssertionError,TypeError,ValueError):
+        return {'outcome':'resume_evidence_hold','state':before,'command_executions':0}
+    if len(state['live']) + len(state['tombstones']) >= 64:
+        return {'outcome':'resume_capacity_hold', 'state':before, 'command_executions':0}
+    ordinal = checked_add(state['ordinal'], 1)
+    window = checked_add(state['window'], 1) if hold == 'publication_retry_exhausted_hold' else state['window']
+    closed = checked_add(state['closed'], 1) if hold == 'publication_retry_exhausted_hold' else state['closed']
+    limit = (checked_add(state['limit'], drain_increment, positive=True)
+             if hold == 'publication_drain_limit_hold' else state['limit'])
+    new_charge = state['next_charge']
+    overlap = checked_add(state['active_charge'], new_charge)
+    if None in {ordinal, window, closed, limit, overlap}:
+        return {'outcome':'resume_arithmetic_exhausted', 'state':before, 'command_executions':0}
+    if overlap > state['charge_ceiling']:
+        return {'outcome':'resume_capacity_hold', 'state':before, 'command_executions':0}
+    predecessor,closure,window_claim_bytes,audit = publication_success_material(
+        before,request_identity,carrier_hash,hold,ordinal,window,limit,now,attempt_authority)
+    prior_hash = sha256(predecessor).digest()
+    window_intent = sha256(b'window-intent:'+prior_hash+request_identity).digest()
+    window_claim = sha256(window_claim_bytes).digest()
+    invocation = publication_invocation(window_claim,ordinal,limit,request_identity,unresolved)
+    result = {
+        'ordinal':ordinal, 'window':window, 'limit':limit,
+        'audit_hash':sha256(audit).digest(),
+    }
+    response = json.dumps({'resumeHandle':handle,'resumeOrdinal':ordinal,'window':window,
+                           'drainLimit':limit,'auditRecordHash':result['audit_hash'].hex()},
+                          sort_keys=True,separators=(',',':')).encode('utf-8')
+    successor = deepcopy(state)
+    successor.update(ordinal=ordinal, window=window, closed=closed, limit=limit,
+                     active_charge=new_charge, window_claim=window_claim,window_claim_bytes=window_claim_bytes)
+    if closure is not None:
+        broker = b'fixture-existing-window-authority:'+codec['H']('scope')+N(before['window'])
+        successor['history'] = sha256(b'HX-EV-PUBLICATION-WINDOW-HISTORY-1\0\x01'+
+            before.get('history',bytes(32))+B(closure)+B(broker)).digest()
+    successor['audits'] += 1
+    successor['last_audit'] = result['audit_hash']
+    successor['invocations'] = tuple(state['invocations']) + (invocation,)
+    successor['live'][request_identity] = {'carrier_hash':carrier_hash, 'result':result,
+                                          'response':response,'expires_at':expires_at}
+    resolution = None
+    if hold == 'publication_drain_limit_hold':
+        resolution = (state['hold_source'], window_intent, invocation, state['limit'], limit)
+    if crash_after_audit:
+        row = {'carrier_hash':carrier_hash,'result':result,'response':response,
+               'expires_at':expires_at,'preparation':prepare_bytes(before,successor,request_identity,carrier_hash,now,expires_at,new_charge),
+               'staged_charge':new_charge,'owner':request_identity}
+        row['receipt'] = state_hash(row)
+        crashed = deepcopy(before)
+        crashed['orphans'][request_identity] = row
+        crashed['audits'] += 1
+        crashed['used_charge'] = overlap
+        # Invocation authority is recorded, but publication remains unarmed.
+        return {'outcome':'orphaned-success','state':crashed,'response':response,'command_executions':0}
     return {
-        'outcome':'resumed',
-        'rearmed':tuple((committed_by_position[position][1], committed_by_position[position][2]) for position in unresolved),
-        'accepted_unchanged':tuple(accepted),
+        'outcome':'resumed', 'state':successor,
+        'response':response,
+        'rearmed':tuple((row[1], row[2]) for row in unresolved),
+        'accepted_unchanged':accepted, 'resolution':resolution,
+        'window_intent_inputs':(prior_hash, request_identity),
         'command_executions':0,
     }
 
+def reconcile_tombstones(state, now, authenticated=True):
+    successor = deepcopy(state)
+    if not authenticated:
+        return successor
+    for identity,row in list(successor['live'].items()):
+        if now >= row['expires_at']:
+            expiry = row['expires_at']
+            successor['tombstones'][identity] = {'carrier_hash':row['carrier_hash'],
+                'expires_at':expiry,'delete_after':expiry+30*86400}
+            del successor['live'][identity]
+    successor['tombstones'] = {
+        identity:row for identity,row in successor['tombstones'].items()
+        if row['delete_after'] > now}
+    if successor['live'] != state['live'] or successor['tombstones'] != state['tombstones']:
+        proof = {'at':now,'live':deepcopy(successor['live']),'tombstones':deepcopy(successor['tombstones'])}
+        proof['receipt'] = state_hash(proof)
+        successor['reconciliation'] = proof
+    return successor
+
+class PreparationBackend:
+    """Provider model: durable byte rows and authenticated readback receipts only."""
+    def __init__(self):
+        self.rows = {}; self.receipts = {}; self.deletions = {}; self.unavailable = set()
+    def receipt(self, key, raw, owner, generation):
+        return sha256(b'provider-record-readback:'+U(key)+owner+N(generation)+B(raw)).digest()
+    def read(self, key, owner, optional=False):
+        assert key not in self.unavailable
+        if key not in self.rows:
+            assert optional and key not in self.receipts
+            if key in self.deletions:
+                recorded_owner,digest,receipt = self.deletions[key]
+                assert recorded_owner == owner and receipt == self.deletion_receipt(key,owner,digest)
+            return None
+        raw = self.rows[key]; recorded_owner,generation,receipt = self.receipts[key]
+        assert recorded_owner == owner and receipt == self.receipt(key,raw,owner,generation)
+        return raw,generation,receipt
+    def write(self, key, raw, owner, generation=1, expected=None, create_once=False):
+        existing = self.read(key,owner,optional=True)
+        if existing is not None:
+            if create_once:
+                assert existing[0] == raw
+                return existing
+            assert expected == sha256(existing[0]).digest() and generation == existing[1]+1
+        else: assert expected is None and generation == 1
+        self.rows[key] = raw
+        self.deletions.pop(key,None)
+        self.receipts[key] = (owner,generation,self.receipt(key,raw,owner,generation))
+        return self.read(key,owner)
+    def delete(self, key, owner, expected):
+        existing = self.read(key,owner)
+        assert sha256(existing[0]).digest() == expected
+        del self.rows[key]; del self.receipts[key]
+        receipt = self.deletion_receipt(key,owner,expected)
+        self.deletions[key] = (owner,expected,receipt)
+        return receipt
+    def deletion_receipt(self, key, owner, digest):
+        return sha256(b'provider-deletion-readback:'+U(key)+owner+digest).digest()
+    def snapshot(self):
+        return deepcopy((self.rows,self.receipts,self.deletions,self.unavailable))
+
+class PreparationStore:
+    stages = ('claim','resolution','fence','closure','window','reconstruction','audit','successor','finalize','invocation')
+    head_schema = codec['extra_schemas']['D45-preparation-head']
+    origin_schema = codec['extra_schemas']['D45-origin']
+    charge_schema = codec['schemas']['D29-charge']
+    counter_schema = codec['schemas']['D29-counter']
+    aliases = {'successor':'state'}
+    def __init__(self, owner, carrier, prepared, prior, now=1000, expiry=1900, crash_after=None, eligible='drain-limit', attempt_authority=None):
+        self.backend = PreparationBackend(); self.tenant = prior['tenant']; self.execution = 'op'
+        predecessor = publication_state_bytes(prior,now)
+        a8_head = b'head'  # independently authenticated existing A8 fixture readback
+        previous_audit = decode_record(predecessor,'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])[10]
+        claim = R('HX-EV-PUBLICATION-RESUME-3',15,U('admin'),U(prior['tenant']),U('op'),codec['H']('scope'),
+            U(eligible),prior['hold_source'],sha256(a8_head).digest(),N(prior['ordinal']+1),previous_audit,U(prior['handle']),
+            owner,sha256(carrier).digest(),U('operator'),Q(now*10000000),Q(expiry*10000000))
+        origin = R('HX-EV-RESUME-ORIGIN-1',10,U(prior['tenant']),U('op'),owner,sha256(carrier).digest(),
+            B(carrier),B(claim),B(image_bytes({k:v for k,v in prior.items() if k not in imported_fields and k != 'orphans'})),N(1),Q(now*10000000),Q(expiry*10000000))
+        decode_record(origin,'HX-EV-RESUME-ORIGIN-1',self.origin_schema)
+        # These are fixture-provider imports already charged by A8/C2/C5, not
+        # retained Python state or an undocumented reconstruction snapshot.
+        inputs = {name:image_bytes(prior[name]) for name in imported_fields}
+        inputs.update(predecessor=predecessor,**{'a8-head':a8_head},attempts=existing_attempt_bytes(prior,now) if attempt_authority is None else attempt_authority,
+            broker=b'fixture-existing-window-authority:'+codec['H']('scope')+N(prior['window']))
+        origin_hash = sha256(origin).digest()
+        for kind,raw in inputs.items():
+            self.backend.write(self.import_key(owner,origin_hash,kind),raw,owner,create_once=True)
+        # The existing execution CAS head is real predecessor authority, not a
+        # cached future successor. Its replacement must name this exact readback.
+        self.backend.write(self.key('HX-EV-PUBLICATION-RESUME-STATE-KEY-1',U(self.tenant),U(self.execution)),
+            predecessor,owner,create_once=True)
+        subject = self.execution+':'+owner.hex()
+        entry = R('HX-EV-HOLD-ENTRY-2',13,U('tenant'),U(self.tenant),U('PublicationResumePreparationHold'),U(subject),
+            O(None),U('publication_resume_preparation_hold'),N(1),bytes(32),Q(now*10000000),Q(now*10000000),
+            N(1),U('coordinator'),Q((now+3600)*10000000))
+        self.backend.write(self.entry_key(subject),entry,owner,create_once=True)
+        index = R('HX-EV-HOLD-INDEX-2',8,U('tenant'),U(self.tenant),N(1),N(1),
+            B(Q(now*10000000)+U('PublicationResumePreparationHold')+U(subject)+sha256(entry).digest()),
+            N(0),bytes(32),Q(now*10000000))
+        self.backend.write(self.inventory_key,index,owner,create_once=True)
+        self.backend.write(self.origin_key(owner),origin,owner,create_once=True)
+        # This bounded provider-model admission is one ledger-owner CAS: its
+        # origin, reserved inventory and typed stage/counters commit together.
+        self.save_charges(owner,prior['active_charge'],prior['next_charge'],'staged',prior['active_charge']+prior['next_charge'],initial=True)
+        assert self.reconstruct(owner) == prepared
+        if crash_after == 'origin': return
+        self.progress(owner,'admitted',{},None)
+        if crash_after == 'progress': return
+    def key(self, domain, *fields):
+        return codec['K'](domain,*fields)
+    @property
+    def inventory_key(self): return self.key('HX-EV-HOLD-INDEX-KEY-1',U('tenant'),U(self.tenant))
+    def entry_key(self, subject): return 'hold-entry:'+self.key('HX-EV-HOLD-ENTRY-KEY-1',U('tenant'),U(self.tenant),U('PublicationResumePreparationHold'),U(subject))
+    @property
+    def head_key(self): return self.key('HX-EV-RESUME-PREPARATION-HEAD-KEY-1',U(self.tenant),U(self.execution))
+    def origin_key(self, owner): return self.key('HX-EV-RESUME-ORIGIN-KEY-1',U(self.tenant),U(self.execution),owner)
+    def preparation_key(self, owner): return self.key('HX-EV-RESUME-PREPARATION-KEY-1',U(self.tenant),U(self.execution),owner)
+    def import_key(self, owner, origin_hash, kind):
+        return 'fixture-provider-import:'+self.key('HX-EV-EXISTING-PROVIDER-INPUT',owner,origin_hash,U(kind))
+    def charge_key(self, owner, kind):
+        return self.key('HX-EV-PUBLICATION-CHARGE-KEY-1',U('deployment-a'),U('tenant'),U(self.tenant),
+                        sha256(U(kind)+owner).digest())
+    def counter_key(self, kind):
+        account = self.tenant if kind == 'tenant' else kind
+        return self.key('HX-EV-PUBLICATION-COUNTER-KEY-1',U('deployment-a'),U(kind),U(account))
+    @property
+    def owner(self):
+        raw = self.backend.rows[self.inventory_key]
+        index = decode_record(raw,'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index'])
+        assert index[:2] == ('tenant',self.tenant) and index[3] in {0,1} and index[5] == 0
+        if index[3] == 0:
+            head = decode_record(self.backend.rows[self.head_key],'HX-EV-RESUME-PREPARATION-HEAD-1',self.head_schema)
+            assert head[:2] == (self.tenant,self.execution) and head[5] in {'completed','rolled-back'}
+            owner = head[2]
+            assert self.backend.read(self.inventory_key,owner)[1] == index[2]
+            assert self.backend.read(self.head_key,owner)[1] == head[6]
+            return owner
+        rows = codec['decode_rows'](index[4],index[3],['Q','U','U','B32'],[1024,1024,4096,1024])
+        observed,hold,subject,entry_hash = rows[0]
+        assert hold == 'PublicationResumePreparationHold' and subject.startswith(self.execution+':')
+        owner = bytes.fromhex(subject[len(self.execution)+1:]); assert len(owner) == 32
+        assert self.backend.read(self.inventory_key,owner)[1] == index[2]
+        entry_row = self.backend.read(self.entry_key(subject),owner)
+        assert sha256(entry_row[0]).digest() == entry_hash
+        entry = decode_record(entry_row[0],'HX-EV-HOLD-ENTRY-2',codec['schemas']['D37-entry'])
+        assert entry[:4] == ('tenant',self.tenant,hold,subject) and entry[8] == observed
+        assert entry[5] == 'publication_resume_preparation_hold' and entry[11] == 'coordinator'
+        assert entry[6] == entry_row[1]
+        return owner
+    @property
+    def origin(self): return self.backend.read(self.origin_key(self.owner),self.owner)[0]
+    @property
+    def carrier(self): return self.origin_fields()[4]
+    def origin_fields(self):
+        fields = decode_record(self.origin,'HX-EV-RESUME-ORIGIN-1',self.origin_schema)
+        assert fields[:3] == (self.tenant,self.execution,self.owner)
+        return fields
+    def head(self):
+        row = self.backend.read(self.head_key,self.owner,optional=True)
+        if row is None: return None
+        fields = decode_record(row[0],'HX-EV-RESUME-PREPARATION-HEAD-1',self.head_schema)
+        assert fields[:3] == (self.tenant,self.execution,self.owner)
+        assert fields[3] == sha256(self.origin).digest() and fields[6] == row[1]
+        return fields
+    def manifest(self):
+        head = self.head()
+        if head is None: return {}
+        count = int.from_bytes(head[8][:4],'big')
+        rows = codec['decode_rows'](head[8][4:],count,['U','U','B32','B32','U'],[1024,128,1024,1024,1024])
+        return {kind:(address,digest,receipt,disposition) for kind,address,digest,receipt,disposition in rows}
+    def progress(self, owner, phase, rows, preparation_hash, now=None):
+        assert owner == self.owner and len(rows) <= 8
+        predecessor = self.backend.read(self.head_key,owner,optional=True)
+        generation = 1 if predecessor is None else checked_add(predecessor[1],1)
+        assert generation is not None
+        manifest = codec['pack']('>I',len(rows))+b''.join(U(kind)+U(address)+digest+receipt+U(disposition)
+            for kind,(address,digest,receipt,disposition) in sorted(rows.items()))
+        raw = R('HX-EV-RESUME-PREPARATION-HEAD-1',10,U(self.tenant),U(self.execution),owner,
+            sha256(self.origin).digest(),O(preparation_hash),U(phase),N(generation),
+            bytes(32) if predecessor is None else sha256(predecessor[0]).digest(),B(manifest),
+            Q(self.origin_fields()[8] if now is None else now*10000000))
+        decode_record(raw,'HX-EV-RESUME-PREPARATION-HEAD-1',self.head_schema)
+        self.backend.write(self.head_key,raw,owner,generation,
+            expected=None if predecessor is None else sha256(predecessor[0]).digest())
+    def imported(self, kind):
+        return self.backend.read(self.import_key(self.owner,sha256(self.origin).digest(),kind),self.owner)[0]
+    def reconstruct(self, owner):
+        assert owner == self.owner
+        fields = self.origin_fields(); prior = read_image(fields[6])
+        for kind in imported_fields: prior[kind] = read_image(self.imported(kind))
+        prior['orphans'] = {}
+        assert self.imported('predecessor') == publication_state_bytes(prior,fields[8]//10000000)
+        window = decode_record(prior['window_claim_bytes'],'HX-EV-PUBLICATION-WINDOW-2',codec['schemas']['D45-window'])
+        assert window[:2] == (self.tenant,codec['H']('scope')) and window[3] == prior['window']
+        assert window[7] == unresolved_root(prior['unresolved'])
+        claim = decode_record(fields[5],'HX-EV-PUBLICATION-RESUME-3',codec['schemas']['D45-request'])
+        assert claim[6] == sha256(self.imported('a8-head')).digest() and claim[7] == prior['ordinal']+1
+        assert claim[8] == decode_record(self.imported('predecessor'),'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])[10]
+        attempts = decode_record(self.imported('attempts'),'HX-EV-WINDOW-ATTEMPT-SET-1',['U','B32','N','B32','N','B','B32','Q'])
+        assert attempts[:4] == (self.tenant,claim[3],prior['window'],sha256(image_bytes(prior['roster'])).digest())
+        assert self.imported('broker') == b'fixture-existing-window-authority:'+claim[3]+N(prior['window'])
+        hold = 'publication_drain_limit_hold' if claim[4] == 'drain-limit' else 'publication_retry_exhausted_hold'
+        result = resume_publication(prior,owner,fields[4],hold,'current',
+            now=fields[8]//10000000,expires_at=fields[9]//10000000,crash_after_audit=True,attempt_authority=self.imported('attempts'))
+        assert result['outcome'] == 'orphaned-success' and result['command_executions'] == 0
+        return result['state']['orphans'][owner]['preparation']
+    @property
+    def preparation(self):
+        row = self.backend.read(self.preparation_key(self.owner),self.owner,optional=True)
+        return None if row is None else row[0]
+    def predecessor_readback(self, raw):
+        fields = decode_record(raw,'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
+        prior = read_image(self.origin_fields()[6]); prior['orphans'] = {}
+        completion = fields[13]//10000000
+        return completion >= self.origin_fields()[8]//10000000 and raw == publication_state_bytes(
+            reconcile_tombstones(prior,completion),completion)
+    def intended(self, kind, completion_time=None):
+        if kind == 'claim': return self.origin_fields()[5]
+        fields = self.origin_fields(); prior = read_image(fields[6]); now = fields[8]//10000000
+        for name in imported_fields: prior[name] = read_image(self.imported(name))
+        prior['orphans'] = {}
+        prepared = self.reconstruct(self.owner)
+        reconstruction = decode_record(prepared,'HX-EV-RESUME-PREPARATION-1',codec['extra_schemas']['D45-preparation'])
+        successor = read_image(reconstruction[6])
+        successor['window_claim_bytes'] = read_image(reconstruction[10])['window']
+        claim = decode_record(fields[5],'HX-EV-PUBLICATION-RESUME-3',codec['schemas']['D45-request'])
+        closure = None
+        if claim[4] == 'retry-exhausted':
+            closure = publication_closure_bytes(prior,self.imported('attempts'),self.imported('broker'),now)
+        if kind == 'fence': return self.imported('broker')
+        if kind == 'closure': assert closure is not None; return closure
+        if kind == 'window': return successor['window_claim_bytes']
+        if kind == 'resolution':
+            assert claim[4] == 'drain-limit'
+            return R('HX-EV-PUBLICATION-DRAIN-LIMIT-RESOLUTION-1',8,U(self.tenant),claim[3],prior['hold_source'],
+                U('resumed'),successor['invocations'][-1],N(successor['ordinal']),U('coordinator'),Q(fields[8]))
+        if kind == 'audit':
+            raw = R('HX-EV-PUBLICATION-RESUME-AUDIT-4',10,U(self.tenant),U(self.execution),N(successor['ordinal']),
+                self.owner,fields[3],sha256(self.imported('predecessor')).digest(),
+                O(None if closure is None else sha256(closure).digest()),N(successor['window']),N(successor['limit']),Q(fields[8]))
+            assert sha256(raw).digest() == successor['live'][self.owner]['result']['audit_hash']
+            return raw
+        if kind == 'state':
+            for name in ('roster','accepted','unresolved'): successor[name] = prior[name]
+            if completion_time is None:
+                existing = self.backend.read(self.artifact_key('state'),self.owner,optional=True)
+                if existing is not None and not self.predecessor_readback(existing[0]):
+                    completion_time = decode_record(existing[0],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])[13]//10000000
+                else: completion_time = now if self.head() is None else self.head()[9]//10000000
+            assert completion_time >= now
+            successor = reconcile_tombstones(successor,completion_time)
+            return publication_state_bytes(successor,completion_time)
+        if kind == 'invocation':
+            unresolved = prior['unresolved']
+            rows = b''.join(codec['pack']('>I',p)+U(message)+sha256(body).digest() for p,message,body in sorted(unresolved))
+            root = sha256(b'HX-EV-PUBLICATION-UNRESOLVED-1\0\x01'+codec['pack']('>I',len(unresolved))+rows).digest()
+            return R('HX-EV-PUBLICATION-INVOCATION-1',9,U(self.tenant),U(self.execution),successor['window_claim'],
+                N(successor['ordinal']),N(successor['limit']),self.owner,root,successor['invocations'][-1],Q(fields[8]))
+        raise AssertionError(kind)
+    def artifact_key(self, kind):
+        claim = decode_record(self.origin_fields()[5],'HX-EV-PUBLICATION-RESUME-3',codec['schemas']['D45-request'])
+        scope = claim[3]
+        if kind == 'claim': return self.key('HX-EV-PUBLICATION-RESUME-CLAIM-KEY-1',U(self.tenant),U(self.execution),self.owner)
+        if kind == 'resolution': return self.key('HX-EV-PUBLICATION-DRAIN-RESOLUTION-KEY-1',scope,claim[5])
+        prior = read_image(self.origin_fields()[6])
+        if kind == 'fence': return 'fixture-c5-window-fence:'+self.key('HX-EV-WINDOW-FENCE',scope,N(prior['window']))
+        if kind == 'closure': return self.key('HX-EV-PUBLICATION-WINDOW-CLOSURE-KEY-1',scope,N(prior['window']))
+        if kind == 'window': return self.key('HX-EV-PUBLICATION-WINDOW-KEY-1',scope,N(prior['window']+1))
+        if kind == 'audit': return self.key('HX-EV-PUBLICATION-RESUME-AUDIT-KEY-2',U(self.tenant),U(self.execution),self.owner)
+        if kind == 'state': return self.key('HX-EV-PUBLICATION-RESUME-STATE-KEY-1',U(self.tenant),U(self.execution))
+        if kind == 'invocation':
+            fields = decode_record(self.intended(kind),'HX-EV-PUBLICATION-INVOCATION-1',codec['extra_schemas']['D45-invocation'])
+            return self.key('HX-EV-PUBLICATION-INVOCATION-KEY-1',U(self.tenant),U(self.execution),fields[7])
+        raise AssertionError(kind)
+    @property
+    def artifacts(self):
+        found = {}
+        for kind,(address,digest,receipt,disposition) in self.manifest().items():
+            if disposition == 'deleted': continue
+            row = self.backend.read(address,self.owner,optional=True)
+            if row is not None:
+                if kind == 'state' and disposition == 'pending' and self.predecessor_readback(row[0]): continue
+                assert digest == sha256(row[0]).digest()
+                found['successor' if kind == 'state' else kind] = row[0]
+        return found
+    @property
+    def cleanup(self): return {kind for kind,values in self.manifest().items() if values[3] == 'deleted'}
+    @property
+    def indexed(self):
+        return decode_record(self.backend.read(self.inventory_key,self.owner)[0],'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index'])[3] == 1
+    def inventory_membership(self, present):
+        owner = self.owner; previous = self.backend.read(self.inventory_key,owner)
+        fields = decode_record(previous[0],'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index'])
+        if fields[3] == int(present): return
+        generation = checked_add(previous[1],1); assert generation is not None
+        subject = self.execution+':'+owner.hex()
+        entry = self.backend.read(self.entry_key(subject),owner)[0]
+        rows = Q(self.origin_fields()[8])+U('PublicationResumePreparationHold')+U(subject)+sha256(entry).digest() if present else b''
+        raw = R('HX-EV-HOLD-INDEX-2',8,U('tenant'),U(self.tenant),N(generation),N(int(present)),B(rows),
+            N(0),sha256(previous[0]).digest(),Q(self.origin_fields()[8]))
+        decode_record(raw,'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index'])
+        self.backend.write(self.inventory_key,raw,owner,generation,sha256(previous[0]).digest())
+    @property
+    def metadata_charge(self):
+        fields = self.charge_fields('metadata')
+        assert fields[10] == 'active' and fields[7] == 2*MiB
+        return fields[7]
+    def charge_fields(self, kind):
+        row = self.backend.read(self.charge_key(self.owner,kind),self.owner)
+        fields = decode_record(row[0],'HX-EV-PUBLICATION-CHARGE-2',self.charge_schema)
+        assert fields[:3] == ('deployment-a','tenant',self.tenant)
+        assert fields[3] == sha256(U(kind)+self.owner).digest() and fields[9] == row[1]
+        return fields
+    @property
+    def swap(self):
+        old,new = self.charge_fields('old'),self.charge_fields('new')
+        prior = read_image(self.origin_fields()[6])
+        assert old[10] in {'active','released'} and new[10] in {'staged','active','released'}
+        assert new[12] == self.owner
+        assert old[7] == (0 if new[10] == 'active' else prior['active_charge'])
+        assert new[7] == (0 if new[10] == 'released' else prior['next_charge'])
+        view = ChargeSwap(old[7],prior['charge_ceiling'])
+        view.generation = new[9]
+        view.owner = self.owner if new[10] != 'released' else None
+        view.staged = new[7] if new[10] == 'staged' else 0
+        view.finalized = new[10] == 'active'
+        view.active = new[7] if view.finalized else old[7]
+        amounts = []
+        for kind in ('tenant','tenant-pool','deployment'):
+            row = self.backend.read(self.counter_key(kind),self.owner)
+            fields = decode_record(row[0],'HX-EV-PUBLICATION-COUNTER-1',self.counter_schema)
+            assert fields[:3] == ('deployment-a',kind,self.tenant if kind == 'tenant' else kind)
+            assert fields[5] == row[1] and fields[4] == 1+(old[7] > 0)+(new[7] > 0)
+            amounts.append(fields[3]-self.metadata_charge)
+        assert amounts[0] == amounts[1] == amounts[2] == old[7]+new[7]
+        view.used = amounts[0]
+        return view
+    def save_charges(self, owner, old_amount, new_amount, new_state, used, initial=False):
+        assert used == old_amount+new_amount
+        pending = []
+        for kind,amount,state in [('metadata',2*MiB,'active'),('old',old_amount,'active' if old_amount else 'released'),('new',new_amount,new_state)]:
+            key = self.charge_key(owner,kind); previous = self.backend.read(key,owner,optional=True)
+            if previous is not None:
+                fields = decode_record(previous[0],'HX-EV-PUBLICATION-CHARGE-2',self.charge_schema)
+                if fields[7] == amount and fields[10] == state: continue
+            generation = 1 if previous is None else checked_add(previous[1],1)
+            assert generation is not None
+            raw = R('HX-EV-PUBLICATION-CHARGE-2',14,U('deployment-a'),U('tenant'),U(self.tenant),sha256(U(kind)+owner).digest(),
+                U('resume-window' if kind == 'new' else 'side-record'),N(amount),N(0),N(amount),N(1),N(generation),U(state),
+                bytes(32) if previous is None else sha256(previous[0]).digest(),O(owner if kind == 'new' else None),Q(self.origin_fields()[8]))
+            decode_record(raw,'HX-EV-PUBLICATION-CHARGE-2',self.charge_schema)
+            pending.append((key,raw,generation,None if previous is None else sha256(previous[0]).digest()))
+        for kind in ('tenant','tenant-pool','deployment'):
+            key = self.counter_key(kind); previous = self.backend.read(key,owner,optional=True)
+            if previous is not None:
+                fields = decode_record(previous[0],'HX-EV-PUBLICATION-COUNTER-1',self.counter_schema)
+                if fields[3] == used+2*MiB: continue
+            generation = 1 if previous is None else checked_add(previous[1],1)
+            assert generation is not None
+            raw = R('HX-EV-PUBLICATION-COUNTER-1',8,U('deployment-a'),U(kind),U(self.tenant if kind == 'tenant' else kind),
+                N(used+2*MiB),N(1+(old_amount > 0)+(new_amount > 0)),N(generation),bytes(32) if previous is None else sha256(previous[0]).digest(),Q(self.origin_fields()[8]))
+            decode_record(raw,'HX-EV-PUBLICATION-COUNTER-1',self.counter_schema)
+            pending.append((key,raw,generation,None if previous is None else sha256(previous[0]).digest()))
+        # One provider-model ledger transaction: every read/CAS/generation is
+        # preflighted before any row changes. No ChargeSwap object is persisted.
+        for key,raw,generation,expected in pending:
+            existing = self.backend.read(key,owner,optional=True)
+            assert (existing is None and expected is None) or (existing is not None and sha256(existing[0]).digest() == expected)
+        for key,raw,generation,expected in pending: self.backend.write(key,raw,owner,generation,expected)
+    def validate(self):
+        owner = self.owner; self.origin_fields(); self.metadata_charge; self.swap; self.reconstruct(owner)
+        head = self.head()
+        if head is None:
+            assert self.preparation is None
+            eligible = decode_record(self.origin_fields()[5],'HX-EV-PUBLICATION-RESUME-3',codec['schemas']['D45-request'])[4]
+            kinds = ('claim','resolution','audit','state','invocation') if eligible == 'drain-limit' else ('claim','fence','closure','window','audit','state','invocation')
+            assert all((lambda row: row is None or kind == 'state' and self.predecessor_readback(row[0]))(
+                self.backend.read(self.artifact_key(kind),owner,optional=True)) for kind in kinds)
+            return
+        rows = self.manifest()
+        for kind,(address,digest,receipt,disposition) in rows.items():
+            assert address == self.artifact_key(kind) and digest == sha256(self.intended(kind)).digest()
+            actual = self.backend.read(address,owner,optional=True)
+            if disposition == 'present':
+                if actual is None:
+                    assert head[5] == 'cleanup' and self.backend.deletions.get(address) == (
+                        owner,digest,self.backend.deletion_receipt(address,owner,digest))
+                else: assert actual[2] == receipt and sha256(actual[0]).digest() == digest
+            elif disposition == 'deleted':
+                assert actual is None and receipt == sha256(b'provider-deletion-readback:'+U(address)+owner+digest).digest()
+            elif actual is not None:
+                assert sha256(actual[0]).digest() == digest or kind == 'state' and self.predecessor_readback(actual[0])
+        if head[4] is not None:
+            assert self.preparation is not None and sha256(self.preparation).digest() == head[4]
+            assert self.preparation == self.reconstruct(owner)
+        elif self.preparation is not None:
+            assert self.preparation == self.reconstruct(owner)  # write survived before head readback
+    def turn(self, crash_after=None, now=1000):
+        try:
+            self.validate()
+            # Preflight every provider read before progress or ledger mutation.
+            eligible = decode_record(self.origin_fields()[5],'HX-EV-PUBLICATION-RESUME-3',codec['schemas']['D45-request'])[4]
+            kinds = ('claim','resolution','audit','state','invocation') if eligible == 'drain-limit' else ('claim','fence','closure','window','audit','state','invocation')
+            for kind in kinds: self.backend.read(self.artifact_key(kind),self.owner,optional=True)
+            self.backend.read(self.preparation_key(self.owner),self.owner,optional=True)
+            if self.head() is not None and self.head()[5] == 'cleanup': return 'cleanup-hold'
+            if now*10000000 >= self.origin_fields()[9] and not any(
+                    name in self.artifacts for name in ('fence','closure','audit','successor')):
+                return 'resume_request_expired'
+            if self.head() is None:
+                self.progress(self.owner,'admitted',{},None)
+                if crash_after == 'progress': return 'interrupted'
+            view = self.swap
+            if not view.staged and not view.finalized:
+                amount = read_image(self.origin_fields()[6])['next_charge']
+                assert view.stage(amount,self.owner)
+                self.inventory_membership(True)
+                self.save_charges(self.owner,view.old,view.staged,'staged',view.used)
+            for name in self.stages:
+                eligible = decode_record(self.origin_fields()[5],'HX-EV-PUBLICATION-RESUME-3',codec['schemas']['D45-request'])[4]
+                if (eligible == 'drain-limit' and name in {'fence','closure','window'}
+                        or eligible == 'retry-exhausted' and name == 'resolution'): continue
+                if name == 'reconstruction':
+                    prepared = self.reconstruct(self.owner)
+                    self.backend.write(self.preparation_key(self.owner),prepared,self.owner,create_once=True)
+                    if crash_after == 'reconstruction-write': return 'interrupted'
+                    if self.head()[4] is None: self.progress(self.owner,'writing',self.manifest(),sha256(prepared).digest())
+                elif name == 'finalize':
+                    view = self.swap
+                    if not view.finalized:
+                        assert view.recover(self.owner,presence(self.owner,'successor','present',view.generation),presence(self.owner,'audit','present',view.generation))
+                        self.save_charges(self.owner,view.old,view.active,'active',view.used)
+                    self.reconcile_successor(now)
+                else:
+                    kind = self.aliases.get(name,name); key = self.artifact_key(kind)
+                    rows = self.manifest()
+                    # A persisted intent fixes its original completion UTC. A
+                    # surviving write acknowledges those bytes before a later CAS.
+                    raw = self.intended(kind,now if kind == 'state' and kind not in rows else None)
+                    if kind in rows and rows[kind][3] == 'present':
+                        if crash_after == name: return 'interrupted'
+                        continue
+                    if kind not in rows or rows[kind][3] == 'deleted':
+                        rows[kind] = (key,sha256(raw).digest(),bytes(32),'pending')
+                        self.progress(self.owner,'writing',rows,self.head()[4],now=now if kind == 'state' else None)
+                        if crash_after == name+'-intent': return 'interrupted'
+                    if kind == 'state':
+                        current = self.backend.read(key,self.owner)
+                        if current[0] != raw:
+                            assert self.predecessor_readback(current[0])
+                            assert now*10000000 >= decode_record(current[0],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])[13]
+                            self.backend.write(key,raw,self.owner,checked_add(current[1],1),sha256(current[0]).digest())
+                    else: self.backend.write(key,raw,self.owner,create_once=True)
+                    if crash_after == name+'-write': return 'interrupted'
+                    readback = self.backend.read(key,self.owner)
+                    rows = self.manifest(); rows[kind] = (key,sha256(readback[0]).digest(),readback[2],'present')
+                    phase = 'audited' if kind in {'audit','state','invocation'} else 'writing'
+                    self.progress(self.owner,phase,rows,self.head()[4])
+                if crash_after == name: return 'interrupted'
+            if self.head()[5] != 'completed': self.progress(self.owner,'completed',self.manifest(),self.head()[4])
+            self.inventory_membership(False)
+            return 'completed'
+        except (AssertionError,KeyError,ValueError,TypeError,UnicodeError): return 'evidence-hold'
+    def reconcile_successor(self, now):
+        key = self.artifact_key('state'); current = self.backend.read(key,self.owner)
+        fields = decode_record(current[0],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
+        assert now*10000000 >= fields[13]
+        raw = self.intended('state',now)
+        if raw == current[0]: return
+        # The shared CAS serializes the recorded success with permitted expiry
+        # updates. State and manifest receipt advance as one provider-model turn.
+        generation = checked_add(current[1],1); assert generation is not None
+        readback = self.backend.write(key,raw,self.owner,generation,sha256(current[0]).digest())
+        rows = self.manifest(); rows['state'] = (key,sha256(raw).digest(),readback[2],'present')
+        phase = 'completed' if self.head()[5] == 'completed' else 'audited'
+        self.progress(self.owner,phase,rows,self.head()[4],now=now)
+    def rollback(self, successor_receipt, audit_receipt, deletion_available=True, crash_after=None):
+        try:
+            self.validate()
+            if (validate_presence(successor_receipt,self.owner,'successor',self.swap.generation) != 'absent'
+                    or validate_presence(audit_receipt,self.owner,'audit',self.swap.generation) != 'absent'
+                    or any(name in self.artifacts for name in ('fence','closure','audit','successor'))): return 'completion-required'
+            for kind,(address,digest,receipt,disposition) in list(self.manifest().items()):
+                if disposition == 'deleted': continue
+                if not deletion_available: return 'cleanup-hold'
+                if self.head()[5] != 'cleanup': self.progress(self.owner,'cleanup',self.manifest(),self.head()[4])
+                actual = self.backend.read(address,self.owner,optional=True)
+                deletion = (self.backend.delete(address,self.owner,digest) if actual is not None else
+                            self.backend.deletion_receipt(address,self.owner,digest))
+                if actual is None and disposition == 'present': assert address in self.backend.deletions
+                if crash_after == kind+'-delete': return 'cleanup-hold'
+                rows = self.manifest(); rows[kind] = (address,digest,deletion,'deleted')
+                self.progress(self.owner,'cleanup',rows,self.head()[4])
+                if crash_after == kind: return 'cleanup-hold'
+            view = self.swap
+            assert view.recover(self.owner,successor_receipt,audit_receipt)
+            self.save_charges(self.owner,view.old,0,'released',view.used)
+            self.progress(self.owner,'rolled-back',self.manifest(),self.head()[4])
+            self.inventory_membership(False)
+            return 'rolled-back'
+        except (AssertionError,KeyError,ValueError,TypeError,UnicodeError): return 'evidence-hold'
+
+def restart_preparation(store):
+    # Only the external provider's durable byte rows/receipts survive. The new
+    # process receives an inventory-derived execution locator, no cached state.
+    restored = PreparationStore.__new__(PreparationStore)
+    restored.backend = store.backend
+    restored.tenant,restored.execution = store.tenant,store.execution
+    try: restored.validate()
+    except (AssertionError,KeyError,ValueError,TypeError,UnicodeError): return None
+    return restored
+
 def verify_eligible_resume_matrix():
     committed = ((1, 'message-1', b'accepted'), (2, 'message-2', b'unresolved-a'),
                  (3, 'message-3', b'unresolved-b'))
     expected = (('message-2', b'unresolved-a'), ('message-3', b'unresolved-b'))
-    exhausted = resume_publication(
-        committed, (2, 3), (1,),
-        'publication_retry_exhausted_hold', 'current')
-    drained = resume_publication(
-        committed, (2, 3), (1,),
-        'publication_drain_limit_hold', 'current')
-    assert exhausted == {
-        'outcome':'resumed', 'rearmed':expected,
-        'accepted_unchanged':(1,), 'command_executions':0}
+    base = {'roster':committed, 'accepted':(committed[0],), 'unresolved':committed[1:],
+            'ordinal':1, 'window':7, 'closed':2, 'limit':16,
+            'tenant':'t','handle':'h','hold_source':sha256(b'limit-hash').digest(), 'active_charge':300, 'next_charge':400,
+            'charge_ceiling':1000, 'live':{}, 'tombstones':{}, 'orphans':{},
+            'audits':0, 'invocations':(), 'window_claim_bytes':existing_window_bytes('t',committed),
+            'window_claim':sha256(existing_window_bytes('t',committed)).digest()}
+    request_id, carrier = request(b'request-1')
+    exhausted = resume_publication(base, request_id, carrier,
+                                   'publication_retry_exhausted_hold', 'current')
+    drained = resume_publication(base, request_id, carrier,
+                                 'publication_drain_limit_hold', 'current')
+    assert exhausted['outcome'] == 'resumed' and exhausted['rearmed'] == expected
+    for bad_id,bad_carrier in [request(b'new-source',source=bytes(32)),request(b'other-tenant',tenant='other'),
+                              request(b'other-handle',handle='other')]:
+        rejected = resume_publication(base,bad_id,bad_carrier,'publication_retry_exhausted_hold','current')
+        assert rejected['outcome'] == 'resume_hold_changed' and rejected['state'] == base
+    assert resume_publication(base,bytes(32),carrier,'publication_retry_exhausted_hold','current')['outcome'] == 'resume_request_conflict'
+    assert exhausted['accepted_unchanged'] == (committed[0],) and exhausted['command_executions'] == 0
+    assert exhausted['state']['window'] == 8 and exhausted['state']['closed'] == 3
     assert drained['rearmed'] == expected and drained['command_executions'] == 0
-    assert resume_publication(committed, (2, 3), (1,),
-                              'publication_retry_exhausted_hold', 'stale') == {
-        'outcome':'resume_hold_changed', 'rearmed':(), 'command_executions':0}
-    assert resume_publication(committed, (2, 3), (1,),
-                              'publication_retry_exhausted_hold', 'unavailable') == {
-        'outcome':'resume_evidence_hold', 'rearmed':(), 'command_executions':0}
-    assert resume_publication(committed, (2,), (1,), 'publication_retry_exhausted_hold', 'current')['outcome'] == 'resume_evidence_hold'
-    assert resume_publication(committed, (2, 2), (1, 3), 'publication_retry_exhausted_hold', 'current')['outcome'] == 'resume_evidence_hold'
-    assert resume_publication(committed, (2, 3), (1, 2), 'publication_retry_exhausted_hold', 'current')['outcome'] == 'resume_evidence_hold'
-    corrupt = committed + ((4, 'message-3', b'contradictory'),)
-    assert resume_publication(corrupt, (2, 3, 4), (1,), 'publication_retry_exhausted_hold', 'current')['outcome'] == 'resume_evidence_hold'
-    drain_before = {'window':7, 'accepted':(1,), 'unresolved':(2,3), 'limit':16,
-                    'source':'limit-hash', 'members':committed}
-    increment = 8
-    drain_after = dict(drain_before, limit=drain_before['limit'] + increment,
-                       resolution=('limit-hash', 'successor-state'),
-                       invocation=('rearm', 2, 3))
-    assert drain_after['window'] == drain_before['window']
-    assert drain_after['accepted'] == drain_before['accepted']
-    assert drain_after['unresolved'] == drain_before['unresolved']
-    assert drain_after['members'] == drain_before['members']
-    assert drain_after['limit'] == 24 and drain_after['resolution'][0] == drain_before['source']
-    assert drain_after['invocation'] == ('rearm', 2, 3)
-    def checked_add(left, right):
-        return None if left < 0 or right <= 0 or left > U64_MAX-right else left+right
-    assert checked_add(16, 8) == 24
-    assert checked_add(U64_MAX, 1) is None and checked_add(1, 0) is None
-    live_retry_rows = list(range(64))
-    assert len(live_retry_rows) == 64
-    assert 0 in live_retry_rows  # an old exact live retry remains addressable.
-    assert len(live_retry_rows) + 1 > 64  # the 65th is held, never evicted silently.
+    assert drained['state']['window'] == base['window'] and drained['state']['limit'] == 24
+    assert drained['state']['closed'] == base['closed']
+    assert drained['state']['window_claim'] == base['window_claim']
+    assert drained['state']['window_claim_bytes'] == base['window_claim_bytes']
+    assert drained['state']['roster'] == base['roster'] and drained['state']['accepted'] == base['accepted']
+    assert drained['state']['unresolved'] == base['unresolved']
+    assert drained['resolution'][0] == base['hold_source']
+    assert drained['resolution'][1] == sha256(b'window-intent:' + b''.join(drained['window_intent_inputs'])).digest()
+    assert drained['resolution'][2] == drained['state']['invocations'][-1]
+    assert drained['resolution'][3:] == (16,24)
+    expected_rows = b''.join(codec['pack']('>I',row[0])+U(row[1])+sha256(row[2]).digest() for row in base['unresolved'])
+    expected_root = sha256(b'HX-EV-PUBLICATION-UNRESOLVED-1\0\x01'+codec['pack']('>I',2)+expected_rows).digest()
+    assert drained['state']['invocations'][-1] == sha256(b'HX-EV-PUBLICATION-INVOCATION-1\0\x01'+base['window_claim']+N(2)+N(24)+request_id+expected_root).digest()
+    for evidence, expected_outcome in [('stale','resume_hold_changed'), ('unavailable','resume_evidence_hold')]:
+        result = resume_publication(base, request_id, carrier,
+                                    'publication_retry_exhausted_hold', evidence)
+        assert result['outcome'] == expected_outcome and result['state'] == base
+    for accepted, unresolved in [
+        ((committed[0],), (committed[1],)),
+        ((committed[0],committed[2]), (committed[1],committed[1])),
+        ((committed[0],committed[1]), (committed[1],committed[2])),
+        ((committed[0],), ((2,'message-3',b'unresolved-a'), committed[2])),
+    ]:
+        corrupt = deepcopy(base); corrupt['accepted'] = accepted; corrupt['unresolved'] = unresolved
+        assert resume_publication(corrupt, request_id, carrier,
+                                  'publication_retry_exhausted_hold', 'current')['outcome'] == 'resume_evidence_hold'
+    live_retry = exhausted['state']
+    live_snapshot = deepcopy(live_retry)
+    retry = resume_publication(live_retry, request_id, carrier,
+                               'publication_retry_exhausted_hold', 'current')
+    assert retry['outcome'] == 'exact-retry' and retry['response'] == exhausted['response']
+    assert retry['state'] == live_retry
+    assert live_retry == live_snapshot
+    for changed_id,changed_carrier in [request(b'request-1',reason=b'changed'),
+                                      request(b'request-1',source=sha256(b'changed-source').digest())]:
+        assert changed_id == request_id and changed_carrier != carrier
+        conflict_result = resume_publication(live_retry, changed_id, changed_carrier,
+                                            'publication_retry_exhausted_hold','current')
+        assert conflict_result['outcome'] == 'resume_request_conflict' and conflict_result['state'] == live_retry
+    crashed = resume_publication(base, request_id, carrier,
+                                 'publication_retry_exhausted_hold','current',crash_after_audit=True)
+    orphan = crashed['state']; orphan_snapshot = deepcopy(orphan)
+    assert orphan['ordinal'] == 1 and orphan['used_charge'] == 700 and orphan['audits'] == 1
+    assert orphan['invocations'] == ()
+    recovered = resume_publication(orphan,request_id,carrier,'publication_retry_exhausted_hold','current')
+    assert recovered['outcome'] == 'orphan-audit-retry' and recovered['response'] == exhausted['response']
+    assert recovered['state'] == exhausted['state'] and recovered['state']['ordinal'] == 2
+    assert recovered['state']['active_charge'] == 400 and recovered['state']['audits'] == 1
+    assert len(recovered['state']['invocations']) == 1 and orphan == orphan_snapshot
+    for corruption in ('receipt','owner','hold_source','roster'):
+        invalid = deepcopy(orphan)
+        if corruption in {'receipt','owner'}: invalid['orphans'][request_id][corruption] = bytes(32)
+        elif corruption == 'hold_source': invalid['hold_source'] = bytes(32)
+        else: invalid['roster'] = ()
+        rejected = resume_publication(invalid,request_id,carrier,'publication_retry_exhausted_hold','current')
+        assert rejected['outcome'] in {'resume_evidence_hold','resume_hold_changed'} and rejected['state'] == invalid
+    altered_prior = deepcopy(orphan)
+    altered_prior['orphans'][request_id]['preparation'] = altered_prior['orphans'][request_id]['preparation'][:-1]
+    prior_rejection = resume_publication(altered_prior,request_id,carrier,'publication_retry_exhausted_hold','current')
+    assert prior_rejection['outcome'] == 'resume_evidence_hold' and prior_rejection['state'] == altered_prior
+    restarted_orphan = read_image(image_bytes(orphan))
+    assert 'prior' not in restarted_orphan['orphans'][request_id] and 'successor' not in restarted_orphan['orphans'][request_id]
+    assert resume_publication(restarted_orphan,request_id,carrier,'publication_retry_exhausted_hold','current')['state'] == exhausted['state']
+    # Missing/changed persisted preparation cannot be replaced by process snapshots.
+    for replacement in (b'',restarted_orphan['orphans'][request_id]['preparation'][:-1]):
+        damaged = deepcopy(restarted_orphan); damaged['orphans'][request_id]['preparation'] = replacement
+        row = damaged['orphans'][request_id]; row['receipt'] = state_hash({k:v for k,v in row.items() if k != 'receipt'})
+        failed = resume_publication(damaged,request_id,carrier,'publication_retry_exhausted_hold','current')
+        assert failed['outcome'] == 'resume_evidence_hold' and failed['state'] == damaged
+    # Current recovery time, including deletion boundary, is authoritative even
+    # when no prior reconciliation exists or its timestamp is older.
+    for completion_time in (1899,1900,1901,1900+30*86400-1,1900+30*86400,1900+30*86400+1):
+        for with_reconciliation in (False,True):
+            pending = reconcile_tombstones(restarted_orphan,1100) if with_reconciliation else restarted_orphan
+            result = resume_publication(pending,request_id,carrier,'publication_retry_exhausted_hold','current',now=completion_time)
+            assert result['outcome'] == 'orphan-audit-retry' and result['response'] == exhausted['response']
+            assert (request_id in result['state']['live']) == (completion_time < 1900)
+            assert (request_id in result['state']['tombstones']) == (1900 <= completion_time < 1900+30*86400)
+            assert result['state']['ordinal'] == 2 and len(result['state']['invocations']) == 1
+    fresh_handle = 'hxrsm1-other'
+    custom = deepcopy(base); custom['handle'] = fresh_handle
+    custom_id,custom_carrier = request(b'custom-handle',handle=fresh_handle)
+    custom_result = resume_publication(custom,custom_id,custom_carrier,'publication_drain_limit_hold','current')
+    # Independent bytes pin every field rather than comparing the encoder to itself.
+    expected_body = ('{"auditRecordHash":"'+custom_result['state']['live'][custom_id]['result']['audit_hash'].hex()+
+                     '","drainLimit":24,"resumeHandle":"hxrsm1-other","resumeOrdinal":2,"window":7}').encode()
+    assert custom_result['response'] == expected_body
+    custom_retry = resume_publication(custom_result['state'],custom_id,custom_carrier,'publication_drain_limit_hold','current')
+    custom_crash = resume_publication(custom,custom_id,custom_carrier,'publication_drain_limit_hold','current',crash_after_audit=True)
+    custom_recovery = resume_publication(read_image(image_bytes(custom_crash['state'])),custom_id,custom_carrier,'publication_drain_limit_hold','current')
+    assert custom_retry['response'] == custom_recovery['response'] == expected_body
+    prepared = custom_crash['state']['orphans'][custom_id]['preparation']
+    supplementary_records['D45-preparation'] = prepared
+    supplementary_records['D45-invocation'] = R('HX-EV-PUBLICATION-INVOCATION-1',9,U('t'),U('op'),
+        custom['window_claim'],N(2),N(24),custom_id,expected_root,custom_result['state']['invocations'][-1],Q(10000000000))
+    retry_prepared = resume_publication(custom,custom_id,custom_carrier,'publication_retry_exhausted_hold','current',
+        crash_after_audit=True)['state']['orphans'][custom_id]['preparation']
+    restart_boundaries = []
+    for eligible,selected_prepared in [('drain-limit',prepared),('retry-exhausted',retry_prepared)]:
+      required = ('claim','resolution','reconstruction','audit','successor','finalize','invocation') if eligible == 'drain-limit' else (
+          'claim','fence','closure','window','reconstruction','audit','successor','finalize','invocation')
+      boundaries = ('origin','progress')+required+tuple(name+suffix for name in required if name not in {'reconstruction','finalize'}
+          for suffix in ('-intent','-write'))+('reconstruction-write',)
+      for crash_side in boundaries:
+        preparation_store = PreparationStore(custom_id,custom_carrier,selected_prepared,custom,
+            eligible=eligible,crash_after=crash_side if crash_side in {'origin','progress'} else None)
+        if eligible == 'drain-limit':
+            supplementary_records['D45-origin'] = preparation_store.origin
+            supplementary_records['D45-preparation-head'] = R('HX-EV-RESUME-PREPARATION-HEAD-1',10,
+                U(custom['tenant']),U('op'),custom_id,sha256(preparation_store.origin).digest(),O(None),
+                U('admitted'),N(1),bytes(32),B(codec['pack']('>I',0)),Q(10000000000))
+        if crash_side not in {'origin','progress'}: assert preparation_store.turn(crash_side) == 'interrupted', (eligible,crash_side)
+        original_claim = preparation_store.origin_fields()[5]; original_origin = preparation_store.origin
+        preparation_store.raw_artifacts = {'claim':b'poisoned process cache','audit':codec['audit'],'state':codec['state']}
+        preparation_store.cached_swap = {'used':0,'owner':bytes(32),'finalized':True}
+        preparation_store.progress_flags = {'completed':True}
+        restarted_store = restart_preparation(preparation_store)
+        assert restarted_store is not None and set(vars(restarted_store)) == {'backend','tenant','execution'}
+        if any(name in restarted_store.artifacts for name in ('fence','closure','audit','successor')):
+            assert restarted_store.rollback(presence(custom_id,'successor','absent'),presence(custom_id,'audit','absent')) == 'completion-required'
+        assert restarted_store.turn() == 'completed', (eligible,crash_side)
+        assert restarted_store.origin == original_origin and restarted_store.artifacts['claim'] == original_claim
+        audit_fields = decode_record(restarted_store.artifacts['audit'],'HX-EV-PUBLICATION-RESUME-AUDIT-4',codec['schemas']['D45-audit'])
+        state_fields = decode_record(restarted_store.artifacts['successor'],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
+        intended_state = read_image(decode_record(selected_prepared,'HX-EV-RESUME-PREPARATION-1',codec['extra_schemas']['D45-preparation'])[6])
+        assert audit_fields[3] == custom_id and audit_fields[4] == sha256(custom_carrier).digest()
+        assert audit_fields[5] == sha256(restarted_store.imported('predecessor')).digest()
+        assert audit_fields[2] == state_fields[3] == intended_state['ordinal']
+        assert audit_fields[7:9] == (intended_state['window'],intended_state['limit']) == state_fields[4:6]
+        assert state_fields[10] == sha256(restarted_store.artifacts['audit']).digest() == intended_state['live'][custom_id]['result']['audit_hash']
+        assert state_fields[7] == intended_state['window_claim']
+        live_rows = codec['decode_rows'](state_fields[11][4:],int.from_bytes(state_fields[11][:4],'big'),['B32','B32','N','B32','N','N','Q'])
+        assert live_rows[-1] == (custom_id,sha256(custom_carrier).digest(),2,state_fields[10],intended_state['window'],intended_state['limit'],19000000000)
+        if eligible == 'drain-limit':
+            assert audit_fields[6] is None and not {'closure','fence','window'} & set(restarted_store.artifacts)
+            assert state_fields[4] == custom['window'] and state_fields[9] == custom['closed']
+            assert state_fields[7] == custom['window_claim']
+            resolution = decode_record(restarted_store.artifacts['resolution'],'HX-EV-PUBLICATION-DRAIN-LIMIT-RESOLUTION-1',codec['schemas']['D14-drain-resolution'])
+            assert resolution[2] == custom['hold_source'] and resolution[4] == intended_state['invocations'][-1]
+        else:
+            assert audit_fields[6] == sha256(restarted_store.artifacts['closure']).digest()
+            assert sha256(restarted_store.artifacts['window']).digest() == state_fields[7]
+        assert restarted_store.swap.used == restarted_store.swap.active == custom['next_charge']
+        assert restarted_store.metadata_charge == 2*MiB and not restarted_store.indexed
+        finalized = restarted_store.backend.snapshot(); assert restarted_store.turn() == 'completed'
+        assert restarted_store.backend.snapshot() == finalized
+        restart_boundaries.append((eligible,crash_side))
+    assert len(restart_boundaries) == 46
+    for cleanup_side in ('claim','resolution','claim-delete','resolution-delete'):
+        preparation_store = PreparationStore(custom_id,custom_carrier,prepared,custom)
+        assert preparation_store.turn('resolution') == 'interrupted'
+        origin = preparation_store.origin
+        unavailable_before = deepcopy(preparation_store.artifacts)
+        assert preparation_store.rollback(None,None) == 'completion-required'
+        assert preparation_store.artifacts == unavailable_before and preparation_store.swap.used == 700
+        assert preparation_store.rollback(presence(custom_id,'successor','absent'),presence(custom_id,'audit','absent'),False) == 'cleanup-hold'
+        assert preparation_store.artifacts == unavailable_before and preparation_store.swap.used == 700
+        assert preparation_store.rollback(presence(custom_id,'successor','absent'),presence(custom_id,'audit','absent'),crash_after=cleanup_side) == 'cleanup-hold'
+        restored = restart_preparation(preparation_store)
+        assert restored.swap.used == 700 and restored.metadata_charge == 2*MiB and restored.indexed
+        cleanup_before = restored.backend.snapshot()
+        assert restored.turn() == 'cleanup-hold' and restored.backend.snapshot() == cleanup_before
+        assert restored.rollback(presence(custom_id,'successor','absent'),presence(custom_id,'audit','absent')) == 'rolled-back'
+        assert not restored.artifacts and restored.swap.used == custom['active_charge'] and not restored.indexed
+        assert restored.turn() == 'completed' and restored.origin == origin
+        assert restored.artifacts['claim'] == preparation_store.origin_fields()[5]
+    # At expiry a rolled-back preparation cannot acquire a new authorization.
+    expired_store = PreparationStore(custom_id,custom_carrier,prepared,custom)
+    assert expired_store.turn('resolution') == 'interrupted'
+    assert expired_store.rollback(presence(custom_id,'successor','absent'),presence(custom_id,'audit','absent')) == 'rolled-back'
+    expired_store = restart_preparation(expired_store); expired_before = expired_store.backend.snapshot()
+    assert expired_store.turn(now=1900) == 'resume_request_expired' and expired_store.backend.snapshot() == expired_before
+    # Missing, changed, stale or unavailable durable evidence never uses the
+    # poisoned process dictionaries and never mutates/refunds any byte row.
+    reference = PreparationStore(custom_id,custom_carrier,prepared,custom)
+    assert reference.turn('successor') == 'interrupted'
+    durable_keys = (reference.inventory_key,reference.entry_key('op:'+custom_id.hex()),reference.origin_key(custom_id),
+        reference.head_key,reference.preparation_key(custom_id),reference.import_key(custom_id,sha256(reference.origin).digest(),'predecessor'),
+        reference.import_key(custom_id,sha256(reference.origin).digest(),'roster'),reference.artifact_key('claim'),
+        reference.artifact_key('audit'),reference.artifact_key('state'))+tuple(reference.charge_key(custom_id,kind)
+        for kind in ('metadata','old','new'))+tuple(reference.counter_key(kind) for kind in ('tenant','tenant-pool','deployment'))
+    durable_refusals = 0
+    for key in durable_keys:
+      for damage in ('missing','changed','stale','unavailable'):
+        damaged = deepcopy(reference)
+        if damage == 'missing': del damaged.backend.rows[key]
+        elif damage == 'changed': damaged.backend.rows[key] += b'changed'
+        elif damage == 'stale':
+            owner,generation,receipt = damaged.backend.receipts[key]
+            damaged.backend.receipts[key] = (owner,generation+1,receipt)
+        else: damaged.backend.unavailable.add(key)
+        snapshot = damaged.backend.snapshot()
+        assert restart_preparation(damaged) is None and damaged.turn() == 'evidence-hold', (key,damage)
+        assert damaged.backend.snapshot() == snapshot
+        durable_refusals += 1
+    assert durable_refusals == 64
+    # Provider-authenticated but contradictory records also fail unchanged.
+    for kind,index,replacement in [('audit',3,bytes(32)),('audit',4,bytes(32)),('state',3,3)]:
+        damaged = deepcopy(reference); key = damaged.artifact_key(kind)
+        domain = 'HX-EV-PUBLICATION-RESUME-AUDIT-4' if kind == 'audit' else 'HX-EV-PUBLICATION-RESUME-STATE-3'
+        schema = codec['schemas']['D45-audit' if kind == 'audit' else 'D45-state']
+        fields = list(decode_record(damaged.backend.rows[key],domain,schema)); fields[index] = replacement
+        raw = R(domain,len(schema),*(codec['encode_typed'](tag,value) for tag,value in zip(schema,fields)))
+        owner,generation,receipt = damaged.backend.receipts[key]
+        damaged.backend.rows[key] = raw
+        damaged.backend.receipts[key] = (owner,generation,damaged.backend.receipt(key,raw,owner,generation))
+        rows = damaged.manifest(); rows[kind] = (key,sha256(raw).digest(),damaged.backend.receipts[key][2],'present')
+        damaged.progress(owner,'audited',rows,damaged.head()[4])
+        snapshot = damaged.backend.snapshot()
+        assert restart_preparation(damaged) is None and damaged.turn() == 'evidence-hold'
+        assert damaged.backend.snapshot() == snapshot
+        durable_refusals += 1
+    damaged = PreparationStore(custom_id,custom_carrier,prepared,custom)
+    assert damaged.turn('claim') == 'interrupted'
+    del damaged.backend.rows[damaged.head_key]; del damaged.backend.receipts[damaged.head_key]
+    snapshot = damaged.backend.snapshot()
+    assert restart_preparation(damaged) is None and damaged.turn() == 'evidence-hold'
+    assert damaged.backend.snapshot() == snapshot
+    durable_refusals += 1
+    assert durable_refusals == 68
+    # Authoritative audit completion uses current UTC, never the old live image.
+    for completion in (1900,1901,1900+30*86400):
+        delayed = PreparationStore(custom_id,custom_carrier,prepared,custom)
+        assert delayed.turn('audit') == 'interrupted'
+        delayed = restart_preparation(delayed)
+        assert delayed.turn('successor-intent',now=completion) == 'interrupted'
+        delayed = restart_preparation(delayed)
+        assert delayed.turn('successor-write',now=completion) == 'interrupted'
+        delayed = restart_preparation(delayed)
+        assert delayed.turn(now=completion) == 'completed'
+        fields = decode_record(delayed.artifacts['successor'],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
+        assert int.from_bytes(fields[11][:4],'big') == 0
+        assert int.from_bytes(fields[12][:4],'big') == (completion < 1900+30*86400)
+        assert fields[10] == sha256(delayed.artifacts['audit']).digest() and fields[13] == completion*10000000
+        assert delayed.swap.used == 400
+    after_prior = deepcopy(custom_result['state']); after_prior['hold_source'] = sha256(b'after-prior-success-hold').digest()
+    after_id,after_carrier = request(b'after-prior-success',source=after_prior['hold_source'],handle=fresh_handle)
+    after_prepared = resume_publication(after_prior,after_id,after_carrier,'publication_drain_limit_hold','current',
+        crash_after_audit=True)['state']['orphans'][after_id]['preparation']
+    after_store = PreparationStore(after_id,after_carrier,after_prepared,after_prior)
+    assert after_store.turn('claim-write') == 'interrupted'
+    after_store = restart_preparation(after_store)
+    after_claim = decode_record(after_store.origin_fields()[5],'HX-EV-PUBLICATION-RESUME-3',codec['schemas']['D45-request'])
+    assert after_claim[6] == sha256(after_store.imported('a8-head')).digest()
+    assert after_claim[6] != sha256(after_store.imported('predecessor')).digest()
+    assert after_claim[8] == after_prior['last_audit'] != bytes(32)
+    for kind in ('a8-head','predecessor'):
+        damaged = deepcopy(after_store); key = damaged.import_key(after_id,sha256(damaged.origin).digest(),kind)
+        if kind == 'a8-head': raw = b'changed-existing-a8-head'
+        else:
+            fields = list(decode_record(damaged.backend.rows[key],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state']))
+            fields[10] = bytes(32)
+            raw = R('HX-EV-PUBLICATION-RESUME-STATE-3',14,*(codec['encode_typed'](tag,value) for tag,value in zip(codec['schemas']['D45-state'],fields)))
+        owner,generation,receipt = damaged.backend.receipts[key]
+        damaged.backend.rows[key] = raw
+        damaged.backend.receipts[key] = (owner,generation,damaged.backend.receipt(key,raw,owner,generation))
+        snapshot = damaged.backend.snapshot()
+        assert restart_preparation(damaged) is None and damaged.turn() == 'evidence-hold'
+        assert damaged.backend.snapshot() == snapshot
+        durable_refusals += 1
+    assert after_store.turn() == 'completed'
+    after_audit = decode_record(after_store.artifacts['audit'],'HX-EV-PUBLICATION-RESUME-AUDIT-4',codec['schemas']['D45-audit'])
+    assert after_audit[5] == sha256(after_store.imported('predecessor')).digest() != after_claim[6]
+    assert durable_refusals == 70
+    preparation_metrics.update(restarts=len(restart_boundaries),cleanup=4,refusals=durable_refusals,expired_completions=3)
+    next_hold = deepcopy(drained['state']); next_hold['hold_source'] = sha256(b'new-drain-limit').digest()
+    next_id,next_carrier = request(b'next-drain',source=next_hold['hold_source'])
+    next_drain = resume_publication(next_hold,next_id,next_carrier,'publication_drain_limit_hold','current')
+    assert next_drain['state']['ordinal'] == 3 and next_drain['state']['limit'] == 32
+    assert len(set(next_drain['state']['invocations'])) == 2
+    assert next_drain['state']['window_claim_bytes'] == base['window_claim_bytes']
+    assert next_drain['state']['unresolved'] == base['unresolved'] and next_drain['command_executions'] == 0
+    assert resume_publication(next_drain['state'],next_id,next_carrier,'publication_drain_limit_hold','current')['state'] == next_drain['state']
+    # A real hourly live-to-tombstone CAS intervenes between audit and recovery.
+    prior_identity,prior_carrier = request(b'prior-success')
+    prior_success = resume_publication(base,prior_identity,prior_carrier,'publication_retry_exhausted_hold','current',expires_at=1100)
+    crash_id,crash_carrier = request(b'crash-after-prior')
+    composed_crash = resume_publication(prior_success['state'],crash_id,crash_carrier,
+        'publication_retry_exhausted_hold','current',crash_after_audit=True)['state']
+    reconciled_crash = reconcile_tombstones(composed_crash,1100)
+    assert prior_identity not in reconciled_crash['live'] and prior_identity in reconciled_crash['tombstones']
+    composed = resume_publication(reconciled_crash,crash_id,crash_carrier,'publication_retry_exhausted_hold','current',now=1100)
+    assert composed['outcome'] == 'orphan-audit-retry' and composed['state']['ordinal'] == 3
+    assert composed['state']['tombstones'] == reconciled_crash['tombstones']
+    assert prior_identity not in composed['state']['live'] and crash_id in composed['state']['live']
+    assert composed['state']['audits'] == 2 and len(composed['state']['invocations']) == 2
+    repeated = resume_publication(composed['state'],crash_id,crash_carrier,'publication_retry_exhausted_hold','current',now=1100)
+    assert repeated['outcome'] == 'exact-retry' and repeated['response'] == composed['response'] and repeated['state'] == composed['state']
+    for completion_time in (1899,1900,1901,1900+30*86400-1,1900+30*86400,1900+30*86400+1):
+        recovered_at = resume_publication(read_image(image_bytes(reconciled_crash)),crash_id,crash_carrier,
+            'publication_retry_exhausted_hold','current',now=completion_time)
+        assert recovered_at['outcome'] == 'orphan-audit-retry' and recovered_at['state']['ordinal'] == 3
+        assert (crash_id in recovered_at['state']['live']) == (completion_time < 1900)
+        assert (crash_id in recovered_at['state']['tombstones']) == (1900 <= completion_time < 1900+30*86400)
+        assert recovered_at['response'] == composed['response'] and len(recovered_at['state']['invocations']) == 2
+    corrupt_reconciliation = deepcopy(reconciled_crash); corrupt_reconciliation['reconciliation']['receipt'] = bytes(32)
+    assert resume_publication(corrupt_reconciliation,crash_id,crash_carrier,'publication_retry_exhausted_hold','current')['state'] == corrupt_reconciliation
+    unrelated = deepcopy(orphan); unrelated['tombstones'][bytes(32)] = {'carrier_hash':bytes(32),'expires_at':1,'delete_after':9999}
+    assert resume_publication(unrelated,request_id,carrier,'publication_retry_exhausted_hold','current')['outcome'] == 'resume_evidence_hold'
+    next_id,next_carrier = request(b'competing-request')
+    competing = resume_publication(orphan,next_id,next_carrier,'publication_retry_exhausted_hold','current')
+    assert competing['outcome'] == 'resume_evidence_hold' and competing['state'] == orphan
+    changed_id,changed_carrier = request(b'request-1',reason=b'changed')
+    assert resume_publication(orphan,changed_id,changed_carrier,
+                              'publication_retry_exhausted_hold','current')['state'] == orphan
+    assert resume_publication(orphan,changed_id,changed_carrier,
+                              'publication_retry_exhausted_hold','current')['outcome'] == 'resume_request_conflict'
+    assert request_id in reconcile_tombstones(live_retry,1899)['live']
+    tombstone = reconcile_tombstones(live_retry,1900)
+    assert request_id not in tombstone['live']
+    delete_after = 1900+30*86400
+    assert tombstone['tombstones'][request_id]['delete_after'] == delete_after
+    for retry_id,retry_carrier,outcome in [(request_id,carrier,'resume_request_expired'),
+            (*request(b'request-1',reason=b'changed'),'resume_request_conflict')]:
+        tombstone_retry = resume_publication(tombstone,retry_id,retry_carrier,'publication_retry_exhausted_hold','current',now=1901)
+        assert tombstone_retry['outcome'] == outcome and tombstone_retry['state'] == tombstone
+    for now in (1900,1901):
+        assert resume_publication(live_retry,request_id,carrier,
+            'publication_retry_exhausted_hold','current',now=now)['outcome'] == 'resume_request_expired'
+    assert request_id in reconcile_tombstones(tombstone,delete_after-1)['tombstones']
+    assert request_id not in reconcile_tombstones(tombstone,delete_after)['tombstones']
+    assert request_id not in reconcile_tombstones(tombstone,delete_after+1)['tombstones']
+    assert reconcile_tombstones(tombstone,delete_after,authenticated=False) == tombstone
+    for field, hold in [('ordinal','publication_drain_limit_hold'), ('limit','publication_drain_limit_hold'),
+                        ('window','publication_retry_exhausted_hold'), ('closed','publication_retry_exhausted_hold')]:
+        overflow = deepcopy(base); overflow[field] = U64_MAX; snapshot = deepcopy(overflow)
+        assert resume_publication(overflow, request_id, carrier, hold, 'current')['outcome'] == 'resume_arithmetic_exhausted'
+        assert overflow == snapshot
+    zero_increment = deepcopy(base)
+    assert resume_publication(zero_increment, request_id, carrier,
+                              'publication_drain_limit_hold', 'current', 0)['outcome'] == 'resume_arithmetic_exhausted'
+    charge_overflow = deepcopy(base); charge_overflow['active_charge'] = U64_MAX; charge_overflow['next_charge'] = 1
+    charge_snapshot = deepcopy(charge_overflow)
+    assert resume_publication(charge_overflow, request_id, carrier,
+                              'publication_drain_limit_hold', 'current')['outcome'] == 'resume_arithmetic_exhausted'
+    assert charge_overflow == charge_snapshot
+    capacity = deepcopy(base); capacity['charge_ceiling'] = 699
+    assert resume_publication(capacity, request_id, carrier,
+                              'publication_drain_limit_hold', 'current')['outcome'] == 'resume_capacity_hold'
+    full_retry_index = deepcopy(base)
+    full_retry_index['live'] = {sha256(str(i).encode()).digest():{'carrier_hash':bytes(32),
+                                                              'expires_at':1900} for i in range(64)}
+    assert resume_publication(full_retry_index, request_id, carrier,
+                              'publication_drain_limit_hold', 'current')['outcome'] == 'resume_capacity_hold'
+    recovered_capacity = reconcile_tombstones(full_retry_index,delete_after)
+    assert not recovered_capacity['live'] and not recovered_capacity['tombstones']
+    assert resume_publication(recovered_capacity,request_id,carrier,
+        'publication_drain_limit_hold','current',now=delete_after,expires_at=delete_after+900)['outcome'] == 'resumed'
     return 'eligible-resume'
 
 def exact_legacy_rows(rows):
@@ -1055,9 +3114,137 @@ def exact_legacy_root(rows):
     encoded = exact_legacy_rows(rows)
     if encoded is None:
         return None
-    return sha256(b'HX-EV-LEGACY-RESUME-EVENTS-1\0\x01'
+    return sha256(b'HX-EV-LEGACY-RESUME-EVENTS-2\0\x01'
                   + len(encoded).to_bytes(4, 'big') + encoded).digest()
 
+def capsule_chunks(rows, classification='success-events'):
+    if not 1 <= len(rows) <= 1000 or exact_legacy_rows(rows) is None:
+        return None
+    if any(len(row[1].encode()) > 1024 for row in rows):
+        return None
+    identity = codec['capsule_identity']
+    chunks, objects, manifest_rows = [], {}, b''
+    for ordinal,index in enumerate(range(0,len(rows),61)):
+        part = rows[index:index+61]
+        raw_rows = exact_legacy_rows(part)[4:]
+        root = sha256(b'HX-EV-LEGACY-RESUME-CHUNK-ROWS-1\0\x01' + B(raw_rows)).digest()
+        raw = R('HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-1',5,identity,N(ordinal),N(len(part)),B(raw_rows),root)
+        key = 'legacy-resume-capsule-chunk:' + codec['K']('HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1',identity,N(ordinal))
+        assert len(raw) <= 64*1024
+        objects[key] = raw
+        chunks.append((ordinal,part[0][0],len(part),root))
+        manifest_rows += N(ordinal)+N(part[0][0])+N(len(part))+sha256(raw).digest()+N(len(raw))+U(key)
+    manifest_rows = len(chunks).to_bytes(4,'big') + manifest_rows
+    manifest = R('HX-EV-LEGACY-RESUME-CAPSULE-2',16,U('t'),U('d'),U('a'),U('tracking'),O(U('op')),
+        U('correlation'),U('increment'),U(classification),N(rows[0][0]),N(rows[-1][0]),I(len(rows)),
+        exact_legacy_root(rows),B(manifest_rows),U('drain-exhaustion'),codec['source_hash'],Q(codec['t']))
+    assert len(manifest) <= 128*1024
+    return {'manifest':manifest,'objects':objects,'chunks':tuple(chunks)}
+
+class Cursor:
+    def __init__(self, raw): self.raw, self.offset = raw, 0
+    def take(self, length):
+        assert 0 <= length <= len(self.raw)-self.offset
+        value = self.raw[self.offset:self.offset+length]; self.offset += length
+        return value
+    def number(self, width=8): return int.from_bytes(self.take(width),'big')
+    def string(self):
+        size = self.number(4)
+        assert 1 <= size <= 1024
+        return self.take(size).decode('utf-8',errors='strict')
+    def complete(self): assert self.offset == len(self.raw)
+
+def validate_capsule(bundle):
+    try:
+        raw = bundle['manifest']
+        assert len(raw) <= 128*1024
+        fields = decode_record(raw,'HX-EV-LEGACY-RESUME-CAPSULE-2',codec['schemas']['D46-capsule'])
+        identity = sha256(b'HX-EV-LEGACY-RESUME-CAPSULE-IDENTITY-1\0\x01'+
+                          b''.join(U(fields[i]) for i in (0,1,2,3))+fields[14]).digest()
+        manifest = Cursor(fields[12]); count = manifest.number(4)
+        assert 1 <= count <= 17
+        rows, seen_keys = [], set()
+        for expected_ordinal in range(count):
+            ordinal, first, row_count = (manifest.number() for _ in range(3))
+            digest, length, key = manifest.take(32), manifest.number(), manifest.string()
+            assert ordinal == expected_ordinal and 1 <= row_count <= 61
+            expected_key = 'legacy-resume-capsule-chunk:' + codec['K']('HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1',identity,N(ordinal))
+            assert key == expected_key and key not in seen_keys
+            seen_keys.add(key)
+            chunk = bundle['objects'][key]
+            assert len(chunk) == length and length <= 64*1024 and sha256(chunk).digest() == digest
+            c = decode_record(chunk,'HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-1',['B32','N','N','B','B32'])
+            assert c[:3] == (identity,ordinal,row_count)
+            assert c[4] == sha256(b'HX-EV-LEGACY-RESUME-CHUNK-ROWS-1\0\x01'+B(c[3])).digest()
+            reader = Cursor(c[3]); part = []
+            for _ in range(row_count):
+                part.append((reader.number(),reader.string(),reader.take(32)))
+            reader.complete()
+            assert part[0][0] == first
+            rows.extend(part)
+        manifest.complete()
+        assert set(bundle['objects']) == seen_keys
+        assert len(rows) == fields[10] and (rows[0][0],rows[-1][0]) == (fields[8],fields[9])
+        assert exact_legacy_root(rows) == fields[11]
+        return {'rows':tuple(rows),'classification':fields[7],'root':fields[11],
+                'hash':sha256(raw).digest(),'correlation':fields[5],'command_type':fields[6]}
+    except (AssertionError,KeyError,TypeError,UnicodeDecodeError):
+        return None
+
+def recovery_hash(record):
+    return state_hash(record) if record is not None else bytes(32)
+
+def repair_receipt(record, bundle):
+    verified = validate_capsule(bundle)
+    if not verified or verified['hash'] != record['capsule']:
+        return None
+    return sha256(b'authoritative-repaired-evidence:' + recovery_hash(record) +
+                  verified['hash'] + exact_legacy_rows(verified['rows'])).digest()
+
+def recovery_transition(record, target, ordinal, *, owner, expected_generation,
+                        expected_predecessor, failure=None, repair=None, bundle=None, capsule):
+    before = deepcopy(record)
+    if (owner not in {'legacy-resume','dead-letter-admin'} or type(ordinal) is not int
+            or not 1 <= ordinal <= U64_MAX or type(expected_generation) is not int
+            or not 1 <= expected_generation <= U64_MAX
+            or expected_predecessor != recovery_hash(record)):
+        return None
+    if not isinstance(capsule,bytes) or len(capsule) != 32:
+        return None
+    if target == 'claimed':
+        verified = validate_capsule(bundle)
+        if verified is None or verified['hash'] != capsule:
+            return None
+    if record is None:
+        return ({'generation':1, 'state':'claimed', 'ordinal':ordinal,
+                 'failure':None, 'capsule':capsule,'owner':owner,
+                 'predecessor':bytes(32),'repair':None}
+                if target == 'claimed' and expected_generation == 1 and repair is None else None)
+    next_generation = checked_add(record['generation'],1)
+    if (capsule != record['capsule'] or ordinal < record['ordinal']
+            or owner != record['owner'] or next_generation is None
+            or expected_generation != next_generation):
+        return None
+    state = record['state']
+    allowed = ((state == 'claimed' and target == 'draining' and ordinal == record['ordinal'])
+               or (state == 'draining' and target == 'completed' and ordinal == record['ordinal'])
+               or (state == 'draining' and target == 'failed' and ordinal == record['ordinal']
+                   and failure in {'transport-retryable','evidence-unavailable','evidence-contradictory'})
+               or (state == 'failed' and target == 'claimed' and ordinal > record['ordinal']
+                   and (record['failure'] == 'transport-retryable'
+                        or (repair is not None and repair == repair_receipt(record,bundle)))))
+    if target != 'failed' and failure is not None:
+        allowed = False
+    if repair is not None and not (state == 'failed' and target == 'claimed'
+                                  and record['failure'] != 'transport-retryable'):
+        allowed = False
+    if not allowed:
+        assert before == record
+        return None
+    return {'generation':next_generation, 'state':target, 'ordinal':ordinal,
+            'failure':failure if target == 'failed' else None, 'capsule':capsule,
+            'owner':owner,'predecessor':recovery_hash(record),'repair':repair}
+
 def recover_legacy_range(capsule, stored_rows, evidence):
     if not isinstance(capsule, dict) or evidence == 'contradictory-authority':
         return {'outcome':'legacy_resume_evidence_unavailable', 'rearmed':(), 'command_executions':0}
@@ -1066,8 +3253,13 @@ def recover_legacy_range(capsule, stored_rows, evidence):
             or not {'correlation', 'command_type', 'classification'} <= set(source)):
         return {'outcome':'legacy_resume_evidence_unavailable', 'rearmed':(), 'command_executions':0}
     root = exact_legacy_root(stored_rows)
+    verified_bundle = validate_capsule(capsule.get('bundle'))
     try:
-        contradicts = (evidence == 'unavailable-read' or root is None
+        contradicts = (evidence == 'unavailable-read' or root is None or not verified_bundle
+            or verified_bundle['rows'] != tuple(stored_rows)
+            or verified_bundle['classification'] != capsule['classification']
+            or verified_bundle['correlation'] != capsule['correlation']
+            or verified_bundle['command_type'] != capsule['command_type']
             or tuple(stored_rows) != tuple(capsule['rows'])
             or root != capsule['root']
             or not isinstance(capsule['range'], tuple)
@@ -1100,13 +3292,14 @@ def verify_legacy_resume_matrix():
     source = {'correlation':'correlation', 'command_type':'increment',
               'classification':'success-events'}
     capsule = {'range':(41, 42), 'rows':rows, 'root':root, 'correlation':'correlation',
-               'command_type':'increment', 'classification':'success-events', 'source':source}
+               'command_type':'increment', 'classification':'success-events', 'source':source,
+               'bundle':capsule_chunks(rows)}
     resumed = recover_legacy_range(capsule, rows, 'verified')
     assert resumed == {
         'outcome':'resumed', 'stored_range':(41, 42), 'rearmed':rows,
         'terminal_status':'Completed', 'command_executions':0}
     rejection = dict(capsule, classification='rejection-events',
-                     source=dict(source, classification='rejection-events'))
+                     source=dict(source, classification='rejection-events'),bundle=capsule_chunks(rows,'rejection-events'))
     assert recover_legacy_range(rejection, rows, 'verified')['terminal_status'] == 'Rejected'
     inverted = dict(capsule, classification='unknown')
     assert recover_legacy_range(inverted, rows, 'verified')['outcome'] == 'resume_evidence_hold'
@@ -1136,6 +3329,77 @@ def verify_legacy_resume_matrix():
         assert recover_legacy_range(malformed_capsule, rows, 'verified')['outcome'] == 'resume_evidence_hold'
     missing_source = dict(capsule); missing_source.pop('source')
     assert recover_legacy_range(missing_source, rows, 'verified')['outcome'] == 'legacy_resume_evidence_unavailable'
+    maximum_rows = tuple((index+1, str(index).zfill(4)+'m'*1020, sha256(str(index).encode()).digest()) for index in range(1000))
+    chunks = capsule_chunks(maximum_rows)
+    assert len(chunks['chunks']) == 17 and chunks['chunks'][0][2] == 61 and chunks['chunks'][-1][2] == 24
+    assert validate_capsule(chunks)['rows'] == maximum_rows
+    assert all(len(raw) <= 65536 for raw in chunks['objects'].values())
+    assert len(chunks['manifest']) <= 128*1024
+    fixture_rows = ((10,'event-1',sha256(codec['stored']).digest()),)
+    fixture = capsule_chunks(fixture_rows)
+    assert fixture['manifest'] == codec['capsule']
+    assert tuple(fixture['objects'].values()) == (codec['chunk'],)
+    assert fixture['chunks'][0][3] == codec['chunk_root']
+    object_keys = tuple(chunks['objects'])
+    for mutation in ('missing','swapped','changed','root'):
+        corrupted = deepcopy(chunks)
+        if mutation == 'missing': del corrupted['objects'][object_keys[0]]
+        elif mutation == 'swapped':
+            corrupted['objects'][object_keys[0]],corrupted['objects'][object_keys[1]] = (
+                corrupted['objects'][object_keys[1]],corrupted['objects'][object_keys[0]])
+        elif mutation == 'changed':
+            corrupted['objects'][object_keys[0]] += b'changed'
+        else:
+            corrupted['manifest'] = corrupted['manifest'].replace(exact_legacy_root(maximum_rows),bytes(32),1)
+        assert validate_capsule(corrupted) is None
+    assert capsule_chunks(maximum_rows + ((1001, 'x', sha256(b'x').digest()),)) is None
+    binding = validate_capsule(capsule['bundle'])['hash']
+    def move(record,target,ordinal,**kwargs):
+        args = {'owner':'legacy-resume','expected_generation':1 if record is None else record['generation']+1,
+                'expected_predecessor':recovery_hash(record),'capsule':binding,'bundle':capsule['bundle']}
+        args.update(kwargs)
+        snapshot = deepcopy(record)
+        result = recovery_transition(record,target,ordinal,**args)
+        assert record == snapshot
+        return result
+    claimed = move(None,'claimed',1)
+    draining = move(claimed,'draining',1)
+    completed = move(draining,'completed',1)
+    assert completed['state'] == 'completed' and move(completed,'claimed',2) is None
+    transport_failed = move(draining,'failed',1,failure='transport-retryable')
+    reclaimed = move(transport_failed,'claimed',2)
+    assert reclaimed['generation'] == transport_failed['generation']+1 and reclaimed['capsule'] == transport_failed['capsule']
+    for failure in ('evidence-unavailable','evidence-contradictory'):
+        evidence_failed = move(draining,'failed',1,failure=failure)
+        assert move(evidence_failed,'claimed',2) is None
+        repair = repair_receipt(evidence_failed,capsule['bundle'])
+        assert repair is not None
+        assert move(evidence_failed,'claimed',2,repair=bytes(32),bundle=capsule['bundle']) is None
+        assert move(evidence_failed,'claimed',2,repair=repair,bundle=None) is None
+        assert move(evidence_failed,'claimed',2,repair=repair,bundle=capsule['bundle'])['state'] == 'claimed'
+    assert move(transport_failed,'draining',2) is None
+    assert move(transport_failed,'claimed',1) is None
+    assert move(transport_failed,'claimed',2,capsule=b'changed') is None
+    for record,target,ordinal in [(claimed,'draining',1),(draining,'failed',1),(transport_failed,'claimed',2)]:
+        assert move(record,target,ordinal,owner='dead-letter-admin') is None
+        assert move(record,target,ordinal,expected_predecessor=bytes(32)) is None
+        assert move(record,target,ordinal,expected_generation=record['generation']+2) is None
+    assert move(None,'claimed',0) is None and move(None,'claimed',U64_MAX+1) is None
+    assert move(None,'claimed',1,bundle=None) is None
+    overflow = dict(transport_failed,generation=U64_MAX)
+    assert move(overflow,'claimed',2) is None
+    assert move(draining,'failed',1,failure='unknown') is None
+    assert move(claimed,'completed',1) is None
+    second_exhaustion = move(reclaimed,'draining',2)
+    second_exhaustion = move(second_exhaustion,'failed',2,failure='transport-retryable')
+    assert second_exhaustion['capsule'] == claimed['capsule'] and second_exhaustion['generation'] > transport_failed['generation']
+    records = {'claimed':claimed,'draining':draining,'completed':completed,'failed':transport_failed}
+    allowed_edges = {('claimed','draining'),('draining','completed'),('draining','failed'),('failed','claimed')}
+    for source_state,record in records.items():
+        for target in records:
+            ordinal = 2 if source_state == 'failed' and target == 'claimed' else 1
+            kwargs = {'failure':'transport-retryable'} if target == 'failed' else {}
+            assert (move(record,target,ordinal,**kwargs) is not None) == ((source_state,target) in allowed_edges)
     return 'legacy-resume'
 
 def capacity_admit(ledger, account, amounts, evidence=True):
@@ -1146,7 +3410,41 @@ def capacity_admit(ledger, account, amounts, evidence=True):
     admitted = ledger.reserve('tenant', account, amounts)
     return ('admitted' if admitted else 'capacity-wait', before)
 
+def capability_ready(scope_retention_ceiling):
+    return scope_retention_ceiling >= 64*MiB
+
 def verify_capacity_wait_matrix():
+    assert not capability_ready(64*MiB-1) and capability_ready(64*MiB) and capability_ready(64*MiB+1)
+    authenticated = Ledger()
+    predecessors = authenticated.predecessors('tenant-a')
+    predecessor_snapshot = deepcopy(vars(authenticated))
+    for kind in ('tenant','tenant-pool','deployment'):
+        forged = dict(predecessors); forged[kind] = bytes(32)
+        assert not authenticated.reserve_pin_batch('tenant-a',[10,20],forged,b'batch')
+        assert vars(authenticated) == predecessor_snapshot
+    assert not authenticated.reserve_pin_batch('tenant-a',[10,20],{'tenant':predecessors['tenant']},b'batch')
+    assert vars(authenticated) == predecessor_snapshot
+    assert authenticated.reserve_pin_batch('tenant-a',[10,20],predecessors,b'batch')
+    assert authenticated.tenant['tenant-a'] == authenticated.tenant_pool == authenticated.deployment == 30
+    assert authenticated.charges == {b'batch':(10,20)}
+    stale_snapshot = deepcopy(vars(authenticated))
+    assert not authenticated.reserve_pin_batch('tenant-a',[1],predecessors,b'other-batch')
+    assert vars(authenticated) == stale_snapshot
+    assert not authenticated.reserve_pin_batch('tenant-a',[10,20],predecessors,b'batch')
+    assert vars(authenticated) == stale_snapshot
+    assert authenticated.read_pin_reservation('tenant-a',[10,20],b'batch')
+    for account,scope,candidate,request_id in [('tenant-b',None,None,None),('tenant-a',bytes(32),None,None),
+        ('tenant-a',None,bytes(32),None),('tenant-a',None,None,bytes(32))]:
+        assert not authenticated.reserve_pin_batch(account,[10,20],authenticated.predecessors(account),b'batch',scope,candidate,request_id)
+        assert vars(authenticated) == stale_snapshot
+    for kind,identity in [('tenant','t'),('tenant-pool','tenant-pool'),('deployment','deployment'),('unidentified','unidentified')]:
+        overflow_ledger = Ledger(); account_kind = 'capture-scope' if kind == 'unidentified' else 'tenant'
+        overflow_ledger.generations[(kind,identity)] = U64_MAX
+        overflow_snapshot = deepcopy(vars(overflow_ledger))
+        assert not overflow_ledger.reserve(account_kind,'t',[1]) and vars(overflow_ledger) == overflow_snapshot
+        overflow_ledger = Ledger(); assert overflow_ledger.reserve(account_kind,'t',[1])
+        overflow_ledger.generations[(kind,identity)] = U64_MAX; overflow_snapshot = deepcopy(vars(overflow_ledger))
+        assert not overflow_ledger.refund(account_kind,'t',1) and vars(overflow_ledger) == overflow_snapshot
     capacity = Ledger()
     assert capacity.reserve('tenant', 'tenant-a', [300*MiB])
     batch = [400*MiB, 400*MiB]
@@ -1179,36 +3477,525 @@ def verify_capacity_wait_matrix():
     assert capacity.tenant['tenant-a'] == capacity.tenant_pool == capacity.deployment == 0
     return 'capacity-wait'
 
-def observe_delivery(previous, observed_at):
-    state = dict(previous) if previous is not None else {
+def observe_delivery(previous, observed_at, ledger=None, identity=None, carrier=None):
+    if previous is None:
+        if ledger is None or identity is None or carrier is None: return None
+        scope_kind,deployment,tenant,component,topic,subscription = identity
+        subject = held_key(*identity,carrier)
+        if subject is None: return None
+        account_kind = 'tenant' if scope_kind == 'tenant' else 'capture-scope'
+        account = tenant if scope_kind == 'tenant' else 'capture:'+subject.hex()
+        metadata_key,inventory_key = 'metadata:'+subject.hex(),'inventory:'+subject.hex()
+        if (metadata_key in ledger.charges or inventory_key in ledger.inventory
+                or len(ledger.inventory) >= ledger.inventory_ceiling): return None
+        if not ledger.reserve(account_kind,account,[32*1024]): return None
+        metadata = {'owner':subject,'account_kind':account_kind,'account':account,
+                    'amount':32*1024,'state':'active','generation':1}
+        metadata['receipt'] = state_hash(metadata)
+        inventory = {'owner':subject,'reserved':True,'generation':1}
+        inventory['receipt'] = state_hash(inventory)
+        ledger.charges[metadata_key] = metadata
+        ledger.inventory[inventory_key] = inventory
+        state = {'identity':identity,'held_key':subject,'carrier_hash':sha256(carrier).digest(),
+                 'metadata_key':metadata_key,'inventory_key':inventory_key,
+                 'metadata_receipt':metadata['receipt'],'inventory_receipt':inventory['receipt'],
+                 'account_kind':account_kind,'account':account,
+                 'state':'observed','transport_copy_acked':False,'route_success':False,'closed':False,
         'first_observed':observed_at,
         'delivery_attempt_count':0,
         'charged_bytes':32*1024,
         'indexed':True,
         'operator_visible':True,
-    }
-    state['delivery_attempt_count'] += 1
+        }
+    else: state = deepcopy(previous)
+    count = checked_add(state['delivery_attempt_count'],1)
+    if count is None: return deepcopy(previous)
+    state['delivery_attempt_count'] = count
+    state['observation_revision'] = count
+    state['observation_receipt'] = observation_receipt(state)
     return state
 
-def held_delivery(retained, cause_cleared=False, routes_terminal=False):
-    state = {
-        'state':'captured', 'charged_bytes':32*1024,
-        'indexed':True, 'operator_visible':True, 'transport_copy_acked':True,
-        'route_success':False, 'closed':False, 'redrive_bytes':None,
-        'retained_backend_id':'held-delivery-store',
-        'retained_object_key':'held/exact-carrier-1',
-        'readback_authority':sha256(b'authenticated-readback:' + retained).digest(),
-        'next_recheck_seconds':60,
-    }
-    if cause_cleared:
+def observation_receipt(state):
+    protected = {k:v for k,v in state.items() if k not in {'delivery_attempt_count','observation_revision','observation_receipt'}}
+    return sha256(b'provider-monotonic-observation:'+image_bytes(protected)+N(state['delivery_attempt_count'])+N(state['observation_revision'])).digest()
+
+def capture_origin(previous, subject, policy_hash):
+    raw = R('HX-EV-CAPTURE-ORIGIN-1',6,subject,B(image_bytes(previous)),state_hash(previous),Q(previous['first_observed']),N(previous['delivery_attempt_count']),policy_hash)
+    decode_record(raw,'HX-EV-CAPTURE-ORIGIN-1',['B32','B','B32','Q','N','B32'])
+    return raw
+
+def original_observation(raw, current):
+    fields = decode_record(raw,'HX-EV-CAPTURE-ORIGIN-1',['B32','B','B32','Q','N','B32'])
+    original = read_image(fields[1])
+    excluded = {'delivery_attempt_count','observation_revision','observation_receipt'}
+    assert {k:v for k,v in original.items() if k not in excluded} == {k:v for k,v in current.items() if k not in excluded}
+    assert current['delivery_attempt_count'] >= original['delivery_attempt_count']
+    assert current['observation_revision'] == current['delivery_attempt_count']
+    assert current['observation_receipt'] == observation_receipt(current)
+    return original
+
+def held_key(scope_kind, deployment, tenant, component, topic, subscription, carrier):
+    if (scope_kind == 'tenant') != (tenant is not None) or scope_kind not in {'tenant','deployment'}:
+        return None
+    material = (U(scope_kind)+U(deployment)+O(None if tenant is None else U(tenant))+
+                U(component)+U(topic)+U(subscription)+sha256(carrier).digest())
+    return sha256(b'HX-EV-HELD-DELIVERY-KEY-2\0\x01' + material).digest()
+
+def policy_revision(previous, revision, expected_head, config=None):
+    predecessor = bytes(32) if previous is None else previous['hash']
+    expected_revision = 1 if previous is None else checked_add(previous['revision'],1)
+    if (type(revision) is not int or revision != expected_revision
+            or not 1 <= revision <= U64_MAX or expected_head != predecessor
+            or (previous is not None and not policy_selected(previous,expected_head))):
+        return None
+    config = codec['H']('subscription-config') if config is None else config
+    raw = R('HX-EV-SUBSCRIPTION-DELIVERY-POLICY-3',13,U('deployment-a'),U('pubsub'),U('orders'),
+        U('sub-a'),N(revision),predecessor,U('initial' if revision == 1 else 'successor'),
+        U('dead-letter-capture'),O(U('orders-dlq')),N(8),config,U('dapr-configuration'),Q(codec['t']))
+    return {'revision':revision,'predecessor':predecessor,'raw':raw,'hash':sha256(raw).digest()}
+
+def policy_selected(record, head):
+    try:
+        fields = decode_record(record['raw'],'HX-EV-SUBSCRIPTION-DELIVERY-POLICY-3',
+            ['U','U','U','U','N','B32','U','U',('O','U'),'N','B32','U','Q'])
+        return (record['hash'] == head == sha256(record['raw']).digest()
+                and fields[4] == record['revision'] and fields[5] == record['predecessor'])
+    except (AssertionError,KeyError,TypeError): return False
+
+class PolicyStore:
+    def __init__(self):
+        self.revisions = {}; self.head = bytes(32)
+    def address(self, revision):
+        return codec['K']('HX-EV-SUBSCRIPTION-POLICY-KEY-1',U('deployment-a'),U('pubsub'),U('orders'),U('sub-a'),N(revision))
+    def install(self, previous, revision, expected_head, config=None):
+        candidate = policy_revision(previous,revision,expected_head,config)
+        if candidate is None: return None
+        address = self.address(revision)
+        existing = self.revisions.get(address)
+        if existing is not None and existing != candidate: return None
+        if self.head == candidate['hash'] and existing == candidate: return deepcopy(existing)
+        if self.head != expected_head: return None
+        self.revisions[address] = deepcopy(candidate)
+        self.head = candidate['hash']
+        return deepcopy(candidate)
+    def selected(self, record):
+        return (self.revisions.get(self.address(record['revision'])) == record
+                and policy_selected(record,self.head))
+
+def object_receipt(backend, key, retained):
+    return sha256(b'authenticated-object-readback:'+U(backend)+U(key)+B(retained)).digest()
+
+def capture_delivery(previous, retained, ledger, readback, policy, current_head, crash_at=None):
+    state = deepcopy(previous)
+    if len(retained) > 193*MiB: return state
+    if not isinstance(current_head,PolicyStore) or not current_head.selected(policy):
+        return state
+    identity = previous['identity']; subject = held_key(*identity,retained)
+    if subject != previous['held_key'] or sha256(retained).digest() != previous['carrier_hash']:
+        return state
+    policy_fields = decode_record(policy['raw'],'HX-EV-SUBSCRIPTION-DELIVERY-POLICY-3',codec['schemas']['D36-policy'])
+    if (identity[1],*identity[3:]) != tuple(policy_fields[:4]): return state
+    metadata = ledger.charges.get(previous['metadata_key'])
+    inventory = ledger.inventory.get(previous['inventory_key'])
+    def authenticated(record,receipt):
+        return bool(record and record.get('receipt') == receipt == state_hash({k:v for k,v in record.items() if k != 'receipt'}))
+    if (not authenticated(metadata,previous['metadata_receipt'])
+            or metadata['owner'] != subject or metadata['amount'] != 32*1024 or metadata['state'] != 'active'
+            or (metadata['account_kind'],metadata['account']) != (previous['account_kind'],previous['account'])
+            or not authenticated(inventory,previous['inventory_receipt'])
+            or inventory['owner'] != subject or not inventory['reserved']):
+        return state
+    backend, key = 'held-delivery-store','held/'+subject.hex()
+    object_owner = {'owner':subject,'account_kind':previous['account_kind'],'account':previous['account'],
+                    'amount':len(retained),'state':'active','generation':1}
+    object_owner['receipt'] = state_hash(object_owner)
+    repair_key = 'redrive-repair:'+subject.hex()
+    repair_slot = {'owner':subject,'account_kind':previous['account_kind'],'account':previous['account'],
+                   'amount':32*1024,'state':'active','generation':1}
+    repair_slot['receipt'] = state_hash(repair_slot)
+    repair_inventory_key = 'repair-inventory:'+subject.hex()
+    repair_inventory = {'owner':subject,'reserved':True,'generation':1,'role':'redrive-repair'}
+    repair_inventory['receipt'] = state_hash(repair_inventory)
+    expected = {'backend':backend,'key':key,'bytes':retained,
+                'receipt':object_receipt(backend,key,retained)}
+    origins = getattr(ledger,'capture_origins',{})
+    if previous.get('state') == 'observed':
+        try:
+            original = original_observation(origins[key],previous) if key in origins else previous
+            if original['observation_receipt'] != observation_receipt(original): return state
+            origin = capture_origin(original,subject,policy['hash'])
+            if key in origins and origins[key] != origin: return state
+        except (AssertionError,KeyError,TypeError,ValueError): return state
+    else:
+        origin = origins.get(key)
+        if origin is None: return state
+        try: original = read_image(decode_record(origin,'HX-EV-CAPTURE-ORIGIN-1',['B32','B','B32','Q','N','B32'])[1])
+        except (AssertionError,KeyError,TypeError,ValueError): return state
+    capture_preparation = R('HX-EV-CAPTURE-PREPARATION-1',9,subject,state_hash(original),
+        previous['metadata_receipt'],previous['inventory_receipt'],U(backend),U(key),previous['carrier_hash'],N(len(retained)),Q(previous['first_observed']))
+    if previous.get('state') in {'captured','redriving','closed'}:
+        return state if (readback == expected and ledger.objects.get(key) == expected and ledger.charges.get(key) == object_owner
+            and ledger.charges.get(repair_key) == repair_slot
+            and ledger.inventory.get(repair_inventory_key) == repair_inventory
+            and getattr(ledger,'capture_preparations',{}).get(key) == previous['capture_preparation']) else dict(state,transport_copy_acked=False)
+    existing_object,existing_charge = ledger.objects.get(key),ledger.charges.get(key)
+    if existing_charge is None:
+        if (key in origins or getattr(ledger,'capture_preparations',{}).get(key) is not None
+                or repair_key in ledger.charges or repair_inventory_key in ledger.inventory
+                or len(ledger.inventory) >= ledger.inventory_ceiling): return state
+    if existing_object is not None or existing_charge is not None:
+        if (existing_charge != object_owner or (existing_object is not None and existing_object != expected) or readback != expected
+                or getattr(ledger,'capture_preparations',{}).get(key) != capture_preparation
+                or ledger.charges.get(repair_key) != repair_slot):
+            return state
+        if ledger.inventory.get(repair_inventory_key) != repair_inventory: return state
+        # Matching retained partial authority completes once; no second reservation.
+        ledger.objects[key] = deepcopy(expected)
+    # Preflight the release edge too; a failed provider write must remain recoverable.
+    changed = [('tenant',previous['account']),
+               ('tenant-pool','tenant-pool') if previous['account_kind'] == 'tenant' else ('unidentified','unidentified'),
+               ('deployment','deployment')]
+    if existing_charge is None and any(ledger.generations.get(key,0) > U64_MAX-2 for key in changed): return state
+    if existing_charge is None and not ledger.reserve(previous['account_kind'],previous['account'],[len(retained),32*1024]):
+        return state
+    if readback != expected:
+        if existing_charge is None:
+            assert ledger.refund(previous['account_kind'],previous['account'],len(retained)+32*1024)
+        return state
+    ledger.charges[key] = object_owner
+    ledger.charges[repair_key] = repair_slot
+    ledger.inventory[repair_inventory_key] = repair_inventory
+    if not hasattr(ledger,'capture_origins'): ledger.capture_origins = {}
+    ledger.capture_origins[key] = origin
+    if not hasattr(ledger,'capture_preparations'): ledger.capture_preparations = {}
+    ledger.capture_preparations[key] = capture_preparation
+    if crash_at == 'charge': return state
+    ledger.objects[key] = deepcopy(expected)
+    if crash_at == 'object': return state
+    state.update(state='captured',charged_bytes=previous['charged_bytes']+len(retained)+32*1024,
+        transport_copy_acked=True,retained_bytes=retained,redrive_bytes=None,redrive_count=0,
+        last_redrive_error=None,error_history=(),retained_backend_id=backend,
+        retained_object_key=key,readback_authority=expected['receipt'],
+        next_recheck_seconds=60,policy_hash=policy['hash'],current_head=current_head.head,capture_preparation=capture_preparation,repair_charge_key=repair_key,
+        repair_inventory_key=repair_inventory_key,repair_inventory_receipt=repair_inventory['receipt'],capture_origin_hash=sha256(origin).digest())
+    return state
+
+def rollback_capture(previous, retained, ledger, successor_absence, deletion_available=True):
+    state = deepcopy(previous); owner = previous['held_key']; key = 'held/'+owner.hex()
+    if (previous['state'] != 'observed' or validate_presence(successor_absence,owner,'capture',1) != 'absent'
+            or not deletion_available): return state
+    charge = ledger.charges.get(key)
+    if charge is None: return state
+    try:
+        origin = ledger.capture_origins[key]; original = original_observation(origin,previous)
+        expected_preparation = R('HX-EV-CAPTURE-PREPARATION-1',9,owner,state_hash(original),previous['metadata_receipt'],
+            previous['inventory_receipt'],U('held-delivery-store'),U(key),previous['carrier_hash'],N(len(retained)),Q(previous['first_observed']))
+        assert ledger.capture_preparations[key] == expected_preparation and held_key(*previous['identity'],retained) == owner
+        for address,amount in ((key,len(retained)),('redrive-repair:'+owner.hex(),32768)):
+            row = ledger.charges[address]
+            assert row['receipt'] == state_hash({k:v for k,v in row.items() if k != 'receipt'})
+            assert (row['owner'],row['account_kind'],row['account'],row['amount'],row['state'],row['generation']) == (
+                owner,previous['account_kind'],previous['account'],amount,'active',1)
+        interest = ledger.inventory['repair-inventory:'+owner.hex()]
+        assert interest['owner'] == owner and interest['generation'] == 1 and interest['role'] == 'redrive-repair' and interest['reserved']
+        assert interest['receipt'] == state_hash({k:v for k,v in interest.items() if k != 'receipt'})
+        if key in ledger.objects:
+            assert ledger.objects[key] == {'backend':'held-delivery-store','key':key,'bytes':retained,'receipt':object_receipt('held-delivery-store',key,retained)}
+        # Provider-model serializable cleanup reads every deletion before refund.
+        if not ledger.refund(previous['account_kind'],previous['account'],len(retained)+32768): return state
+        ledger.objects.pop(key,None); del ledger.charges[key]; del ledger.charges['redrive-repair:'+owner.hex()]
+        del ledger.inventory['repair-inventory:'+owner.hex()]; del ledger.capture_origins[key]; del ledger.capture_preparations[key]
+        return state
+    except (AssertionError,KeyError,TypeError,ValueError): return state
+
+def preparation_authority(previous, ledger):
+    preparation = previous.get('capture_preparation'); key = previous.get('retained_object_key')
+    if not isinstance(preparation,bytes) or not preparation: return False
+    try:
+        fields = decode_record(preparation,'HX-EV-CAPTURE-PREPARATION-1',codec['extra_schemas']['D36-capture-preparation'])
+        origin = getattr(ledger,'capture_origins',{}).get(key)
+        if origin is None or sha256(origin).digest() != previous['capture_origin_hash']: return False
+        origin_fields = decode_record(origin,'HX-EV-CAPTURE-ORIGIN-1',['B32','B','B32','Q','N','B32'])
+        if origin_fields[5] != previous['policy_hash']: return False
+        original = read_image(origin_fields[1])
+        if fields != (previous['held_key'],state_hash(original),previous['metadata_receipt'],previous['inventory_receipt'],
+                      'held-delivery-store',key,previous['carrier_hash'],len(previous['retained_bytes']),previous['first_observed']): return False
+    except (AssertionError,KeyError,TypeError,ValueError): return False
+    return getattr(ledger,'capture_preparations',{}).get(key) == previous.get('capture_preparation')
+
+def retained_authority(previous, ledger):
+    if ledger is None: return False
+    retained = previous.get('retained_bytes'); key = previous.get('retained_object_key')
+    if (not isinstance(retained,bytes) or not key or previous.get('retained_backend_id') != 'held-delivery-store'
+            or sha256(retained).digest() != previous['carrier_hash']
+            or held_key(*previous['identity'],retained) != previous['held_key']): return False
+    expected = {'backend':'held-delivery-store','key':key,'bytes':retained,
+                'receipt':object_receipt('held-delivery-store',key,retained)}
+    charge = ledger.charges.get(key)
+    def charge_authority(address, amount, receipt=None):
+        row = ledger.charges.get(address)
+        return bool(row and row['receipt'] == state_hash({k:v for k,v in row.items() if k != 'receipt'})
+            and (receipt is None or row['receipt'] == receipt) and row['owner'] == previous['held_key']
+            and row['generation'] == 1 and row['state'] == 'active'
+            and (row['account_kind'],row['account'],row['amount']) == (previous['account_kind'],previous['account'],amount))
+    def interest_authority(address, receipt, role=None):
+        row = ledger.inventory.get(address)
+        return bool(row and row['receipt'] == receipt == state_hash({k:v for k,v in row.items() if k != 'receipt'})
+            and row['owner'] == previous['held_key'] and row['generation'] == 1 and row['reserved']
+            and row.get('role') == role)
+    if not preparation_authority(previous,ledger): return False
+    if (not charge_authority(previous['metadata_key'],32*1024,previous['metadata_receipt'])
+            or not interest_authority(previous['inventory_key'],previous['inventory_receipt'])
+            or not charge_authority(previous['repair_charge_key'],32*1024)
+            or not interest_authority(previous['repair_inventory_key'],previous['repair_inventory_receipt'],'redrive-repair')): return False
+    return (key == 'held/'+previous['held_key'].hex() and ledger.objects.get(key) == expected
+            and previous['readback_authority'] == expected['receipt']
+            and charge is not None and charge['receipt'] == state_hash({k:v for k,v in charge.items() if k != 'receipt'})
+            and charge['owner'] == previous['held_key'] and charge['state'] == 'active' and charge['generation'] == 1
+            and (charge['account_kind'],charge['account'],charge['amount']) ==
+                (previous['account_kind'],previous['account'],len(retained)))
+
+def signed_redrive_request(previous, issuer='admin', subject='operator', request_utc=None):
+    count = checked_add(previous['redrive_count'],1)
+    utc = previous['first_observed'] if request_utc is None else request_utc
+    if count is None or type(utc) is not int or not previous['first_observed'] <= utc < (1<<63): return None
+    scope,_,tenant,*_ = previous['identity']
+    raw = R('HX-EV-REDRIVE-REQUEST-2',7,U(issuer),U(scope),O(None if tenant is None else U(tenant)),
+        previous['held_key'],N(previous['redrive_count']),U(subject),Q(utc))
+    decode_record(raw,'HX-EV-REDRIVE-REQUEST-2',codec['schemas']['D36-redrive'])
+    row = {'owner':previous['held_key'],'count':count,'raw':raw,
+           'signature':sha256(b'authenticated-purpose-2d-redrive:'+raw).digest()}
+    row['receipt'] = sha256(b'provider-redrive-request-readback:'+image_bytes(row)).digest()
+    return row
+
+def redrive_request_authority(previous, row, count):
+    if not isinstance(row,dict) or set(row) != {'owner','count','raw','signature','receipt'}: return False
+    try:
+        fields = decode_record(row['raw'],'HX-EV-REDRIVE-REQUEST-2',codec['schemas']['D36-redrive'])
+        scope,_,tenant,*_ = previous['identity']
+        return (row['owner'] == previous['held_key'] and row['count'] == count > 0
+            and fields[:4] == ('admin',scope,tenant,previous['held_key']) and fields[4] == count-1
+            and fields[5] == 'operator' and previous['first_observed'] <= fields[6] < (1<<63)
+            and row['signature'] == sha256(b'authenticated-purpose-2d-redrive:'+row['raw']).digest()
+            and row['receipt'] == sha256(b'provider-redrive-request-readback:'+image_bytes({k:v for k,v in row.items() if k != 'receipt'})).digest())
+    except (AssertionError,KeyError,TypeError,ValueError): return False
+
+def redrive_attempt_authority(previous, row, request, count):
+    if not isinstance(row,dict) or not redrive_request_authority(previous,request,count): return False
+    try:
+        fields = decode_record(row['raw'],'HX-EV-REDRIVE-ATTEMPT-1',codec['extra_schemas']['D36-attempt'])
+        request_hash = sha256(request['raw']).digest()
+        return (fields == (previous['held_key'],count,previous['carrier_hash'],previous['retained_object_key'],
+            previous['metadata_receipt'],previous['first_observed'],request_hash)
+            and (row['owner'],row['count'],row['carrier_hash'],row['locator'],row['metadata_receipt'],row['request_hash']) ==
+                (fields[0],fields[1],fields[2],fields[3],fields[4],fields[6])
+            and row['receipt'] == state_hash({k:v for k,v in row.items() if k != 'receipt'}))
+    except (AssertionError,KeyError,TypeError,ValueError): return False
+
+def held_delivery(previous, cause_cleared=False, routes_terminal=False, redrive_failed=False, ledger=None,
+                  request=None, request_readback_available=True, request_delete_available=True, crash_after=None):
+    state = deepcopy(previous)
+    if state['state'] == 'closed': return state
+    if cause_cleared and state['state'] == 'captured':
+        if state.get('repair_required') or not retained_authority(previous,ledger): return deepcopy(previous)
+        if state.get('repaired_record') or state.get('repair_cleanup'): return deepcopy(previous)
         state['state'] = 'redriving'
-        state['redrive_bytes'] = retained
+        next_count = checked_add(state['redrive_count'],1)
+        if next_count is None: return deepcopy(previous)
+        if not request_readback_available: return deepcopy(previous)
+        requests = deepcopy(getattr(ledger,'redrive_requests',{}))
+        address = (state['held_key'],1)  # fixed active/disputed request slot
+        old_request = requests.get(address)
+        if previous['redrive_count'] == 0:
+            if old_request is not None or previous.get('request') is not None: return deepcopy(previous)
+        else:
+            if not redrive_request_authority(previous,old_request,previous['redrive_count']): return deepcopy(previous)
+            if old_request != previous.get('request'): return deepcopy(previous)
+            if previous['attempt'].get('request_hash') != sha256(old_request['raw']).digest(): return deepcopy(previous)
+        request = signed_redrive_request(previous) if request is None else request
+        if not redrive_request_authority(previous,request,next_count): return deepcopy(previous)
+        request_deletions = deepcopy(getattr(ledger,'request_deletions',{}))
+        if old_request is not None:
+            if not request_delete_available: return deepcopy(previous)
+            request_deletions[address] = sha256(b'provider-redrive-request-deletion:'+old_request['receipt']).digest()
+            del requests[address]
+        assert address not in requests
+        if crash_after == 'request-delete-readback': return deepcopy(previous)
+        requests[address] = deepcopy(request)
+        if crash_after == 'request-readback': return deepcopy(previous)
+        state['redrive_count'] = next_count
+        state['request'] = deepcopy(request)
+        state['redrive_bytes'] = state['retained_bytes']
+        attempt = {'owner':state['held_key'],'count':next_count,'carrier_hash':state['carrier_hash'],
+                   'locator':state['retained_object_key'],'metadata_receipt':state['metadata_receipt'],
+                   'request_hash':sha256(request['raw']).digest()}
+        attempt['raw'] = R('HX-EV-REDRIVE-ATTEMPT-1',7,state['held_key'],N(next_count),state['carrier_hash'],
+            U(state['retained_object_key']),state['metadata_receipt'],Q(state['first_observed']),attempt['request_hash'])
+        decode_record(attempt['raw'],'HX-EV-REDRIVE-ATTEMPT-1',codec['extra_schemas']['D36-attempt'])
+        attempt['receipt'] = state_hash(attempt)
+        state['attempt'] = attempt
+        attempts = deepcopy(getattr(ledger,'redrive_attempts',{}))
+        address = (state['held_key'],1)  # one current/disputed authority slot
+        old = attempts.get(address)
+        if (old is None and previous['redrive_count'] != 0) or (old is not None and old != previous.get('attempt')):
+            return deepcopy(previous)
+        if old is not None:
+            if (old['receipt'] != state_hash({k:v for k,v in old.items() if k != 'receipt'})
+                    or old['count'] != previous['redrive_count']): return deepcopy(previous)
+            if not redrive_attempt_authority(previous,old,old_request,previous['redrive_count']): return deepcopy(previous)
+        attempt_deletions = deepcopy(getattr(ledger,'attempt_deletions',{}))
+        if old is not None:
+            attempt_deletions[address] = sha256(b'provider-attempt-deletion:'+old['receipt']).digest()
+            del attempts[address]
+        assert address not in attempts
+        attempts[address] = deepcopy(attempt)
+        # Provider-model serializable transaction: detached writes/deletion
+        # readbacks publish together with the held-count CAS, or abort unchanged.
+        ledger.redrive_requests,ledger.request_deletions = requests,request_deletions
+        ledger.redrive_attempts,ledger.attempt_deletions = attempts,attempt_deletions
+        if crash_after == 'request-attempt-commit':
+            state['redrive_bytes'] = None
+            return state
         state['closed'] = routes_terminal
         state['route_success'] = routes_terminal
         if routes_terminal:
             state['state'] = 'closed'
+    if state['state'] == 'redriving':
+        if routes_terminal:
+            state.update(state='closed',closed=True,route_success=True)
+        elif redrive_failed:
+            state['state'] = 'captured'
+            state['last_redrive_error'] = sha256(b'typed-redrive-error'+N(state['redrive_count'])).digest()
+            state['error_history'] = (state['error_history'] + (state['last_redrive_error'],))[-64:]
+            state['next_recheck_seconds'] = min(900,60*(2**min(state['redrive_count'],4)))
     return state
 
+def reconcile_redrive(previous, evidence='unavailable', evidence_receipt=None, ledger=None):
+    if previous['state'] != 'redriving': return deepcopy(previous)
+    attempt = previous.get('attempt'); state = deepcopy(previous)
+    current_request = getattr(ledger,'redrive_requests',{}).get((previous['held_key'],1)) if ledger is not None else None
+    current_attempt = getattr(ledger,'redrive_attempts',{}).get((previous['held_key'],1)) if ledger is not None else None
+    if (not redrive_request_authority(previous,current_request,previous['redrive_count'])
+            or current_request != previous.get('request')
+            or current_attempt != attempt
+            or not redrive_attempt_authority(previous,current_attempt,current_request,previous['redrive_count'])
+            or not attempt or attempt.get('request_hash') != sha256(current_request['raw']).digest()
+            or attempt['receipt'] != state_hash({k:v for k,v in attempt.items() if k != 'receipt'})
+            or attempt['owner'] != previous['held_key'] or attempt['count'] != previous['redrive_count']
+            or attempt['carrier_hash'] != previous['carrier_hash']
+            or attempt['locator'] != previous['retained_object_key']
+            or attempt['metadata_receipt'] != previous['metadata_receipt']):
+        state.update(state='captured',last_redrive_error=sha256(b'redrive-attempt-evidence-conflict').digest(),next_recheck_seconds=900,
+                     repair_required={'owner':previous['held_key'],'count':previous['redrive_count'],
+                                      'carrier_hash':previous['carrier_hash'],'locator':previous['retained_object_key']})
+        state['repair_required']['raw'] = R('HX-EV-REDRIVE-REPAIR-1',10,previous['held_key'],N(previous['redrive_count']),
+            state_hash(attempt),previous['carrier_hash'],U(previous['retained_object_key']),N(1),bytes(32),U('required'),O(None),Q(previous['first_observed']))
+        scope = previous['identity'][0]; scope_id = previous['identity'][2] if scope == 'tenant' else previous['identity'][1]
+        subject = previous['held_key'].hex()+':'+str(previous['redrive_count'])
+        state['repair_entry'] = R('HX-EV-HOLD-ENTRY-2',13,U(scope),U(scope_id),U('RedriveEvidenceRepairHold'),U(subject),O(None),
+            U('redrive-evidence-repair-hold'),N(1),bytes(32),Q(previous['first_observed']),Q(previous['first_observed']),
+            N(1),U('operations'),Q(previous['first_observed']+9000000000))
+        decode_record(state['repair_entry'],'HX-EV-HOLD-ENTRY-2',codec['schemas']['D37-entry'])
+        state['repair_indexed'] = True
+        supplementary_records['D36-repair'] = state['repair_required']['raw']
+        return state
+    expected = sha256(b'terminal-redrive-readback:'+attempt['receipt']).digest()
+    if evidence == 'terminal' and evidence_receipt == expected:
+        return held_delivery(previous,routes_terminal=True,ledger=ledger)
+    # Restart never waits indefinitely for an unknown/unavailable completion.
+    # The exact persisted attempt returns to captured with bounded retry/error evidence.
+    return held_delivery(previous,redrive_failed=True)
+
+def repair_redrive(previous, ledger, receipt):
+    state = deepcopy(previous); repair = previous.get('repair_required')
+    if not repair or not retained_authority(previous,ledger): return state
+    try:
+        fields = decode_record(repair['raw'],'HX-EV-REDRIVE-REPAIR-1',['B32','N','B32','B32','U','N','B32','U',('O','B32'),'Q'])
+        if (fields[0],fields[1],fields[3],fields[4]) != (repair['owner'],repair['count'],repair['carrier_hash'],repair['locator']): return state
+    except (AssertionError,ValueError,KeyError,TypeError): return state
+    attempt = getattr(ledger,'redrive_attempts',{}).get((repair['owner'],1))
+    if not attempt or (attempt['owner'],attempt['count'],attempt['carrier_hash'],attempt['locator']) != (
+            repair['owner'],repair['count'],repair['carrier_hash'],repair['locator']): return state
+    if attempt['receipt'] != state_hash({k:v for k,v in attempt.items() if k != 'receipt'}): return state
+    request = getattr(ledger,'redrive_requests',{}).get((repair['owner'],1))
+    if not redrive_request_authority(previous,request,repair['count']): return state
+    if not redrive_attempt_authority(previous,attempt,request,repair['count']): return state
+    if attempt.get('request_hash') != sha256(request['raw']).digest(): return state
+    expected = sha256(b'authenticated-attempt-repair:'+attempt['receipt']+previous['readback_authority']).digest()
+    if receipt != expected: return state
+    state['attempt'] = deepcopy(attempt); state['request'] = deepcopy(request); state['repair_required'] = None
+    state['repair_receipt'] = expected
+    state['repaired_record'] = R('HX-EV-REDRIVE-REPAIR-1',10,repair['owner'],N(repair['count']),fields[2],repair['carrier_hash'],
+        U(repair['locator']),N(2),sha256(repair['raw']).digest(),U('repaired'),O(expected),Q(previous['first_observed']))
+    return state
+
+def cleanup_redrive_repair(previous, available=True, crash_after=None):
+    state = deepcopy(previous)
+    raw = state.get('repaired_record'); cleanup = state.get('repair_cleanup')
+    if raw is None and cleanup is None: return state
+    if not available: return state
+    def persist():
+        state['repair_cleanup_bytes'] = R('HX-EV-REDRIVE-CLEANUP-1',7,state['held_key'],N(state['redrive_count']),cleanup['record'],cleanup['entry'],
+            U(cleanup['phase']),O(cleanup.get('record_deletion')),O(cleanup.get('entry_deletion')))
+        decode_record(state['repair_cleanup_bytes'],'HX-EV-REDRIVE-CLEANUP-1',codec['extra_schemas']['D36-cleanup'])
+    if cleanup is not None:
+        fields = decode_record(state['repair_cleanup_bytes'],'HX-EV-REDRIVE-CLEANUP-1',codec['extra_schemas']['D36-cleanup'])
+        assert fields == (state['held_key'],state['redrive_count'],cleanup['record'],cleanup['entry'],cleanup['phase'],cleanup.get('record_deletion'),cleanup.get('entry_deletion'))
+    if cleanup is None:
+        fields = decode_record(raw,'HX-EV-REDRIVE-REPAIR-1',codec['extra_schemas']['D36-repair'])
+        assert fields[7] == 'repaired' and fields[8] == state['repair_receipt']
+        entry = decode_record(state['repair_entry'],'HX-EV-HOLD-ENTRY-2',codec['schemas']['D37-entry'])
+        assert entry[2:4] == ('RedriveEvidenceRepairHold',state['held_key'].hex()+':'+str(state['redrive_count']))
+        cleanup = {'record':sha256(raw).digest(),'entry':sha256(state['repair_entry']).digest(),'phase':'repaired-readback'}
+        state['repair_cleanup'] = cleanup
+        persist()
+    if crash_after == cleanup['phase']: return state
+    if raw is not None:
+        state['repaired_record'] = None
+        cleanup['record_deletion'] = sha256(b'provider-repair-deletion:'+cleanup['record']).digest()
+        cleanup['phase'] = 'record-deleted'
+        persist()
+        if crash_after == 'record-deleted': return state
+    assert cleanup['record_deletion'] == sha256(b'provider-repair-deletion:'+cleanup['record']).digest()
+    if cleanup['phase'] != 'entry-deleted':
+        cleanup['phase'] = 'record-readback'
+        persist()
+        if crash_after == 'record-readback': return state
+    cleanup['entry_deletion'] = sha256(b'provider-repair-entry-deletion:'+cleanup['entry']).digest()
+    state['repair_entry'] = None
+    cleanup['phase'] = 'entry-deleted'
+    persist()
+    if crash_after == 'entry-deleted': return state
+    assert cleanup['entry_deletion'] == sha256(b'provider-repair-entry-deletion:'+cleanup['entry']).digest()
+    state['repair_cleanup_receipt'] = state_hash(cleanup)
+    state['repair_cleanup'] = None
+    state['repair_indexed'] = False
+    return state
+
+def erase_held_delivery(previous, ledger, terminal_receipt):
+    if previous.get('state') == 'erased': return deepcopy(previous)
+    expected = sha256(b'terminal-held-erasure:'+previous['held_key']+N(previous['redrive_count'])).digest()
+    if (previous['state'] != 'closed' or terminal_receipt != expected or not retained_authority(previous,ledger)
+            or previous.get('repair_required') or previous.get('repaired_record') or previous.get('repair_cleanup')): return deepcopy(previous)
+    address = (previous['held_key'],1)
+    request = getattr(ledger,'redrive_requests',{}).get(address)
+    attempt = getattr(ledger,'redrive_attempts',{}).get(address)
+    if (not redrive_request_authority(previous,request,previous['redrive_count'])
+            or request != previous.get('request') or attempt != previous.get('attempt')
+            or not redrive_attempt_authority(previous,attempt,request,previous['redrive_count'])
+            or attempt.get('request_hash') != sha256(request['raw']).digest()): return deepcopy(previous)
+    if not ledger.refund(previous['account_kind'],previous['account'],previous['charged_bytes']): return deepcopy(previous)
+    key = previous['retained_object_key']
+    for address in (key,previous['metadata_key'],previous['repair_charge_key']): del ledger.charges[address]
+    for address in (previous['inventory_key'],previous['repair_inventory_key']): del ledger.inventory[address]
+    del ledger.objects[key]; del ledger.capture_origins[key]; del ledger.capture_preparations[key]
+    getattr(ledger,'redrive_attempts',{}).pop((previous['held_key'],1),None)
+    getattr(ledger,'attempt_deletions',{}).pop((previous['held_key'],1),None)
+    getattr(ledger,'redrive_requests',{}).pop((previous['held_key'],1),None)
+    getattr(ledger,'request_deletions',{}).pop((previous['held_key'],1),None)
+    return {'state':'erased','held_key':previous['held_key'],'redrive_count':previous['redrive_count'],
+            'erasure_receipt':expected,'charged_bytes':0,'closed':True}
+
 def above_max_delivery(observed_length):
     return {
         'state':'incident', 'reason':'delivery_above_advertised_max',
@@ -1234,29 +4021,210 @@ def full_replay_exit(event_count, readable_bytes, accounting_bytes, incremental_
 def hard_bound_exceeded(event_count, readable_bytes, accounting_bytes):
     return (event_count > 100000, readable_bytes > 64*MiB, accounting_bytes > 256*MiB)
 
+def retention_readiness(horizon_seconds):
+    return {'ready':horizon_seconds <= 315576000,
+            'reason':None if horizon_seconds <= 315576000 else 'scope_retention_horizon_unsupported',
+            'slice4_active':horizon_seconds <= 315576000}
+
+def post_activation_growth(event_count, readable_bytes, accounting_bytes):
+    exceeded = hard_bound_exceeded(event_count, readable_bytes, accounting_bytes)
+    return {'dispatch':not any(exceeded), 'hold':'LegacyArrayLimit' if any(exceeded) else None,
+            'applied_events':0 if any(exceeded) else event_count, 'exceeded':exceeded}
+
 def verify_held_delivery_and_long_stream_matrix():
     carrier = b'exact-retained-carrier-and-headers'
-    first = observe_delivery(None, 638712864000000000)
+    capture_ledger = Ledger()
+    identity = ('tenant','deployment-a','t','pubsub','orders','sub-a')
+    first = observe_delivery(None, 638712864000000000,capture_ledger,identity,carrier)
     restarted = observe_delivery(dict(first), 638712864600000000)
     assert first['first_observed'] == restarted['first_observed'] == 638712864000000000
     assert first['delivery_attempt_count'] == 1 and restarted['delivery_attempt_count'] == 2
     assert restarted['charged_bytes'] == 32*1024 and restarted['indexed'] and restarted['operator_visible']
-    held = held_delivery(carrier)
-    assert held == {
-        'state':'captured', 'charged_bytes':32*1024,
-        'indexed':True, 'operator_visible':True, 'transport_copy_acked':True,
-        'route_success':False, 'closed':False, 'redrive_bytes':None,
-        'retained_backend_id':'held-delivery-store',
-        'retained_object_key':'held/exact-carrier-1',
-        'readback_authority':sha256(b'authenticated-readback:' + carrier).digest(),
-        'next_recheck_seconds':60}
+    object_key = 'held/'+first['held_key'].hex()
+    readback = {'backend':'held-delivery-store','key':object_key,'bytes':carrier,
+                'receipt':object_receipt('held-delivery-store',object_key,carrier)}
+    policy_store = PolicyStore()
+    policy = policy_store.install(None,1,bytes(32))
+    for crash_side in ('charge','object'):
+        partial_ledger = Ledger()
+        observed = observe_delivery(None,638712864000000000,partial_ledger,identity,carrier)
+        persisted = capture_delivery(observed,carrier,partial_ledger,readback,policy,policy_store,crash_at=crash_side)
+        assert persisted == observed and not persisted['transport_copy_acked']
+        charged_before = partial_ledger.deployment
+        completed_partial = capture_delivery(read_image(image_bytes(persisted)),carrier,partial_ledger,readback,policy,policy_store)
+        assert completed_partial['transport_copy_acked'] and completed_partial['state'] == 'captured'
+        assert partial_ledger.deployment == charged_before == 64*1024+len(carrier)
+        partial_snapshot = deepcopy(vars(partial_ledger))
+        assert capture_delivery(completed_partial,carrier,partial_ledger,readback,policy,policy_store) == completed_partial
+        assert vars(partial_ledger) == partial_snapshot
+        for conflict in (None,dict(readback,receipt=bytes(32)),dict(readback,bytes=b'changed')):
+            blocked = capture_delivery(persisted,carrier,partial_ledger,conflict,policy,policy_store)
+            assert blocked == persisted and vars(partial_ledger) == partial_snapshot
+    if not globals().get('fault_probe') or globals().get('fault_name') == 'ordinary capture accepts oversize':
+        for length in (193*MiB-1,193*MiB,193*MiB+1,256*MiB,256*MiB+1):
+            boundary_carrier = b'b'*length
+            boundary_ledger = Ledger()
+            boundary = observe_delivery(None,638712864000000000,boundary_ledger,identity,boundary_carrier)
+            boundary_key = 'held/'+boundary['held_key'].hex()
+            boundary_readback = {'backend':'held-delivery-store','key':boundary_key,'bytes':boundary_carrier,
+                                'receipt':object_receipt('held-delivery-store',boundary_key,boundary_carrier)}
+            counters_before = deepcopy(vars(boundary_ledger))
+            outcome = capture_delivery(boundary,boundary_carrier,boundary_ledger,boundary_readback,policy,policy_store)
+            if length <= 193*MiB:
+                assert outcome['transport_copy_acked'] and outcome['state'] == 'captured'
+                assert boundary_ledger.deployment == 64*1024+length
+            else:
+                assert outcome == boundary and vars(boundary_ledger) == counters_before
+                assert not outcome['transport_copy_acked']
+            del boundary_carrier,boundary_readback,boundary_ledger,outcome,counters_before,boundary
+    held = capture_delivery(restarted,carrier,capture_ledger,readback,policy,policy_store)
+    supplementary_records['D36-capture-preparation'] = held['capture_preparation']
+    supplementary_records['D36-capture-origin'] = capture_ledger.capture_origins[held['retained_object_key']]
+    assert held['state'] == 'captured' and held['retained_bytes'] == carrier
+    assert held['charged_bytes'] == capture_ledger.tenant['t'] == 64*1024+len(carrier)
+    assert capture_ledger.tenant_pool == capture_ledger.deployment == held['charged_bytes']
+    captured_snapshot = deepcopy(vars(capture_ledger))
+    assert capture_delivery(held,carrier,capture_ledger,readback,policy,policy_store) == held
+    assert vars(capture_ledger) == captured_snapshot
     assert (held['transport_copy_acked'] and held['retained_backend_id']
             and held['retained_object_key'] and held['readback_authority']
             and not held['route_success'])
-    redriven = held_delivery(carrier, cause_cleared=True, routes_terminal=False)
+    captured_store = deepcopy(capture_ledger)
+    redriven = held_delivery(held, cause_cleared=True, routes_terminal=False,ledger=capture_ledger)
+    supplementary_records['D36-current-request'] = redriven['request']['raw']
+    supplementary_records['D36-attempt'] = redriven['attempt']['raw']
     assert redriven['redrive_bytes'] == carrier and not redriven['closed'] and not redriven['route_success']
-    completed = held_delivery(carrier, cause_cleared=True, routes_terminal=True)
+    for evidence in ('unavailable','unknown','failed'):
+        recovery = reconcile_redrive(deepcopy(redriven),evidence,ledger=capture_ledger)
+        assert recovery['state'] == 'captured' and recovery['redrive_count'] == 1
+        assert recovery['next_recheck_seconds'] == 120 and recovery['last_redrive_error']
+        for field in ('charged_bytes','retained_bytes','retained_object_key','readback_authority'):
+            assert recovery[field] == redriven[field]
+        assert reconcile_redrive(recovery,evidence,ledger=capture_ledger) == recovery
+    terminal_receipt = sha256(b'terminal-redrive-readback:'+redriven['attempt']['receipt']).digest()
+    assert reconcile_redrive(deepcopy(redriven),'terminal',terminal_receipt,ledger=capture_ledger)['state'] == 'closed'
+    assert reconcile_redrive(deepcopy(redriven),'terminal',bytes(32),ledger=capture_ledger)['state'] == 'captured'
+    invalid_attempt = deepcopy(redriven); invalid_attempt['attempt']['receipt'] = bytes(32)
+    repair_hold = reconcile_redrive(invalid_attempt,ledger=capture_ledger)
+    assert repair_hold['state'] == 'captured' and repair_hold['repair_required']
+    restarted_repair = read_image(image_bytes(repair_hold))
+    for _ in range(3):
+        assert held_delivery(restarted_repair,cause_cleared=True,ledger=capture_ledger) == restarted_repair
+    assert repair_redrive(restarted_repair,capture_ledger,bytes(32)) == restarted_repair
+    genuine_attempt = capture_ledger.redrive_attempts[(held['held_key'],1)]
+    # Authenticated attempt readback alone cannot clear the separately persisted
+    # repair prerequisite before its exact repaired CAS/cleanup finishes.
+    readback_only = deepcopy(restarted_repair); readback_only['attempt'] = deepcopy(genuine_attempt)
+    repair_snapshot = deepcopy(vars(capture_ledger))
+    assert held_delivery(readback_only,cause_cleared=True,ledger=capture_ledger) == readback_only
+    assert vars(capture_ledger) == repair_snapshot
+    repair_receipt = sha256(b'authenticated-attempt-repair:'+genuine_attempt['receipt']+held['readback_authority']).digest()
+    repaired = repair_redrive(restarted_repair,capture_ledger,repair_receipt)
+    assert repaired['repair_required'] is None and repaired['repair_receipt'] == repair_receipt
+    assert held_delivery(repaired,cause_cleared=True,ledger=capture_ledger) == repaired
+    cleanup_start = cleanup_redrive_repair(repaired,crash_after='repaired-readback')
+    supplementary_records['D36-cleanup'] = cleanup_start['repair_cleanup_bytes']
+    repaired = cleanup_redrive_repair(cleanup_start)
+    repaired_send = held_delivery(repaired,cause_cleared=True,ledger=capture_ledger)
+    assert repaired_send['state'] == 'redriving' and repaired_send['redrive_count'] == 2 and repaired_send['redrive_bytes'] == carrier
+    assert held_delivery(repaired_send,cause_cleared=True,ledger=capture_ledger) == repaired_send
+    # Freshly authenticated bytes, object, locator and active charge precede send.
+    for damage in ('retained-bytes','object-bytes','missing-object','locator','readback','charge','unavailable'):
+        invalid_state = deepcopy(held); invalid_store = deepcopy(captured_store)
+        if damage == 'retained-bytes': invalid_state['retained_bytes'] += b'changed'
+        elif damage == 'object-bytes': invalid_store.objects[object_key]['bytes'] += b'changed'
+        elif damage == 'missing-object': del invalid_store.objects[object_key]
+        elif damage == 'locator': invalid_state['retained_object_key'] = 'wrong-locator'
+        elif damage == 'readback': invalid_store.objects[object_key]['receipt'] = bytes(32)
+        elif damage == 'charge': invalid_store.charges[object_key]['state'] = 'released'
+        else: invalid_store = None
+        failed_send = held_delivery(invalid_state,cause_cleared=True,ledger=invalid_store)
+        assert failed_send == invalid_state and failed_send['redrive_bytes'] is None and failed_send['redrive_count'] == 0
+    failed_redrive = held_delivery(redriven, redrive_failed=True)
+    assert (failed_redrive['state'] == 'captured' and failed_redrive['redrive_count'] == 1
+            and failed_redrive['last_redrive_error'] and failed_redrive['next_recheck_seconds'] == 120)
+    restarted_redrive = deepcopy(failed_redrive)
+    failure_ledger = deepcopy(capture_ledger)
+    failure_ledger.redrive_requests = {(held['held_key'],1):deepcopy(redriven['request'])}
+    failure_ledger.redrive_attempts = {(held['held_key'],1):deepcopy(redriven['attempt'])}
+    second_failure = held_delivery(restarted_redrive,cause_cleared=True,redrive_failed=True,ledger=failure_ledger)
+    assert second_failure['redrive_count'] == 2 and second_failure['next_recheck_seconds'] == 240
+    assert second_failure['last_redrive_error'] != failed_redrive['last_redrive_error']
+    assert second_failure['error_history'] == (failed_redrive['last_redrive_error'],second_failure['last_redrive_error'])
+    for field in ('charged_bytes','retained_bytes','retained_backend_id','retained_object_key','readback_authority','first_observed','delivery_attempt_count'):
+        assert second_failure[field] == held[field]
+    bounded = deepcopy(second_failure)
+    for _ in range(140):
+        bounded = held_delivery(read_image(image_bytes(bounded)),cause_cleared=True,redrive_failed=True,ledger=failure_ledger)
+        assert len(failure_ledger.redrive_attempts) == 1
+    assert bounded['redrive_count'] == 142 and len(bounded['error_history']) == 64
+    assert bounded['next_recheck_seconds'] == 900
+    completed = held_delivery(bounded,cause_cleared=True,routes_terminal=True,ledger=failure_ledger)
     assert completed['redrive_bytes'] == carrier and completed['closed'] and completed['route_success']
+    key = held_key('tenant', 'deployment-a', 't', 'pubsub', 'orders', 'sub-a', carrier)
+    assert key and key != held_key('tenant', 'deployment-b', 't', 'pubsub', 'orders', 'sub-a', carrier)
+    assert key != held_key('tenant', 'deployment-a', 't', 'pubsub', 'other', 'sub-a', carrier)
+    for fields in [('tenant','deployment-a','other-tenant','pubsub','orders','sub-a',carrier),
+                   ('tenant','deployment-a','t','other-component','orders','sub-a',carrier),
+                   ('tenant','deployment-a','t','pubsub','orders','other-subscription',carrier),
+                   ('tenant','deployment-a','t','pubsub','orders','sub-a',carrier+b'other'),
+                   ('deployment','deployment-a',None,'pubsub','orders','sub-a',carrier)]:
+        assert held_key(*fields) != key
+    assert held_key('tenant', 'deployment-a', None, 'pubsub', 'orders', 'sub-a', carrier) is None
+    assert held_key('deployment', 'deployment-a', 't', 'pubsub', 'orders', 'sub-a', carrier) is None
+    for bad_readback in [None,dict(readback,bytes=b'changed'),dict(readback,receipt=bytes(32)),
+                         dict(readback,backend='other'),dict(readback,key='other')]:
+        failed_ledger = Ledger()
+        failed_observed = observe_delivery(None,638712864000000000,failed_ledger,identity,carrier)
+        rejected_capture = capture_delivery(failed_observed,carrier,failed_ledger,bad_readback,policy,policy_store)
+        assert not rejected_capture['transport_copy_acked'] and rejected_capture['state'] == 'observed'
+        assert failed_ledger.tenant['t'] == failed_ledger.tenant_pool == failed_ledger.deployment == 32*1024
+        assert set(failed_ledger.charges) == {failed_observed['metadata_key']} and not failed_ledger.objects
+    refused_ledger = Ledger(); refused_ledger.tenant_ceiling = 0
+    snapshot = deepcopy(vars(refused_ledger))
+    assert not capture_delivery(restarted,carrier,refused_ledger,readback,policy,policy_store)['transport_copy_acked']
+    assert vars(refused_ledger) == snapshot
+    genuine_refusal = Ledger()
+    refusal_observed = observe_delivery(None,638712864000000000,genuine_refusal,identity,carrier)
+    genuine_refusal.tenant_ceiling = 32*1024+len(carrier)-1
+    genuine_snapshot = deepcopy(vars(genuine_refusal))
+    assert capture_delivery(refusal_observed,carrier,genuine_refusal,readback,policy,policy_store) == refusal_observed
+    assert vars(genuine_refusal) == genuine_snapshot
+    unavailable = capture_delivery(restarted,carrier,Ledger(),readback,policy,PolicyStore())
+    assert not unavailable['transport_copy_acked']
+    # Missing, stale, and wrong-owner metadata/inventory cannot authorize capture.
+    for target,corruption in [('metadata','missing'),('metadata','owner'),('metadata','stale'),
+                              ('inventory','missing'),('inventory','owner'),('inventory','stale')]:
+        invalid_ledger = Ledger()
+        observed = observe_delivery(None,638712864000000000,invalid_ledger,identity,carrier)
+        collection = invalid_ledger.charges if target == 'metadata' else invalid_ledger.inventory
+        authority_key = observed['metadata_key' if target == 'metadata' else 'inventory_key']
+        if corruption == 'missing': del collection[authority_key]
+        else:
+            collection[authority_key]['owner' if corruption == 'owner' else 'generation'] = bytes(32) if corruption == 'owner' else 2
+            row = collection[authority_key]; row['receipt'] = state_hash({k:v for k,v in row.items() if k != 'receipt'})
+        invalid_snapshot = deepcopy(vars(invalid_ledger)); observed_snapshot = deepcopy(observed)
+        rejected = capture_delivery(observed,carrier,invalid_ledger,readback,policy,policy_store)
+        assert rejected == observed_snapshot and vars(invalid_ledger) == invalid_snapshot
+    # Two carriers and two scope identities share one persistent store/ledger.
+    first_object = deepcopy(capture_ledger.objects[object_key]); first_charge = deepcopy(capture_ledger.charges[object_key])
+    for second_identity,second_carrier in [(identity,carrier+b'-second'),
+            (('tenant','deployment-a','tenant-b','pubsub','orders','sub-a'),carrier),
+            (('deployment','deployment-a',None,'pubsub','orders','sub-a'),carrier)]:
+        observed = observe_delivery(None,638712864000000000,capture_ledger,second_identity,second_carrier)
+        second_key = 'held/'+observed['held_key'].hex(); assert second_key != object_key
+        second_readback = {'backend':'held-delivery-store','key':second_key,'bytes':second_carrier,
+                          'receipt':object_receipt('held-delivery-store',second_key,second_carrier)}
+        second_capture = capture_delivery(observed,second_carrier,capture_ledger,second_readback,policy,policy_store)
+        assert second_capture['state'] == 'captured' and second_capture['transport_copy_acked']
+        assert capture_ledger.objects[object_key] == first_object and capture_ledger.charges[object_key] == first_charge
+        shared_snapshot = deepcopy(vars(capture_ledger))
+        assert capture_delivery(second_capture,second_carrier,capture_ledger,second_readback,policy,policy_store) == second_capture
+        assert vars(capture_ledger) == shared_snapshot
+    assert len(capture_ledger.objects) == 4 and capture_ledger.unidentified == 64*1024+len(carrier)
+    advanced_policy = policy_store.install(policy,2,policy['hash'])
+    stale_capture = capture_delivery(restarted,carrier,capture_ledger,readback,policy,policy_store)
+    assert stale_capture == restarted and advanced_policy and not stale_capture['transport_copy_acked']
     over_bound = full_replay_exit(100001, 64*MiB, 256*MiB, False)
     assert over_bound == {
         'outcome':'LegacyArrayLimit', 'indexed':True, 'applied_events':0,
@@ -1302,8 +4270,325 @@ def verify_held_delivery_and_long_stream_matrix():
             override = full_replay_exit(*values, True)
             assert override['outcome'] == 'incremental-scheduled' and override['applied_events'] == 0
         assert full_replay_exit(*above_values, False)['applied_events'] == 0
+        growth = post_activation_growth(*above_values)
+        assert not growth['dispatch'] and growth['hold'] == 'LegacyArrayLimit'
+        assert growth['applied_events'] == 0 and growth['exceeded'][index]
+    assert retention_readiness(315576000-1) == {'ready':True, 'reason':None, 'slice4_active':True}
+    assert retention_readiness(315576000) == {'ready':True, 'reason':None, 'slice4_active':True}
+    assert retention_readiness(315576000+1) == {
+        'ready':False, 'reason':'scope_retention_horizon_unsupported', 'slice4_active':False}
     return 'held-delivery-or-long-stream'
 
+def verify_loop6_transitions():
+    roster = ((1,'message-1',b'accepted'),(2,'message-2',b'unresolved-a'),(3,'message-3',b'unresolved-b'))
+    window = existing_window_bytes('t',roster)
+    base = {'roster':roster,'accepted':(roster[0],),'unresolved':roster[1:],'ordinal':1,'window':7,'closed':2,'limit':16,
+        'tenant':'t','handle':'h','hold_source':sha256(b'limit-hash').digest(),'active_charge':300,'next_charge':400,'charge_ceiling':1000,
+        'live':{},'tombstones':{},'orphans':{},'audits':0,'invocations':(),'window_claim_bytes':window,'window_claim':sha256(window).digest()}
+    identity,carrier = request(b'loop6')
+    prepared = resume_publication(base,identity,carrier,'publication_drain_limit_hold','current',crash_after_audit=True)['state']['orphans'][identity]['preparation']
+    utc_cases = 0
+    for boundary in ('successor-intent','successor-write','successor','finalize'):
+      for completion in (1001,1899,1900,1901,2000,1900+30*86400-1,1900+30*86400,1900+30*86400+1):
+       for intervening in (False,True):
+        store = PreparationStore(identity,carrier,prepared,base)
+        assert store.turn(boundary,now=1000) == 'interrupted'
+        audit = store.intended('audit'); claim = store.origin_fields()[5]
+        if intervening and boundary in {'successor','finalize'}: store.reconcile_successor(1001)
+        restored = restart_preparation(store); assert restored is not None
+        assert restored.turn(now=completion) == 'completed', (boundary,completion,intervening)
+        fields = decode_record(restored.backend.rows[restored.artifact_key('state')],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
+        counts = tuple(int.from_bytes(fields[i][:4],'big') for i in (11,12))
+        assert counts == ((1,0) if completion < 1900 else (0,1) if completion < 1900+30*86400 else (0,0))
+        assert fields[13] == completion*10000000 and restored.artifacts['audit'] == audit and restored.artifacts['claim'] == claim
+        assert restored.swap.used == restored.swap.active == 400 and len(restored.artifacts) == 5
+        snapshot = restored.backend.snapshot()
+        repeated = restart_preparation(restored); assert repeated is not None and repeated.turn(now=completion) == 'completed'
+        assert repeated.backend.snapshot() == snapshot
+        utc_cases += 1
+    for boundary in ('successor-intent','successor-write','successor','finalize'):
+        store = PreparationStore(identity,carrier,prepared,base); assert store.turn(boundary) == 'interrupted'
+        for damage in ('unavailable','contradictory'):
+            damaged = deepcopy(store); key = damaged.artifact_key('state')
+            if damage == 'unavailable': damaged.backend.unavailable.add(key)
+            else: damaged.backend.rows[key] += b'changed'
+            snapshot = damaged.backend.snapshot()
+            assert damaged.turn(now=2000) == 'evidence-hold' and damaged.backend.snapshot() == snapshot
+    rolled = PreparationStore(identity,carrier,prepared,base); assert rolled.turn('resolution') == 'interrupted'
+    assert rolled.rollback(presence(identity,'successor','absent'),presence(identity,'audit','absent')) == 'rolled-back'
+    assert rolled.charge_fields('new')[9:13] == (2,'released',rolled.charge_fields('new')[11],identity)
+    assert restart_preparation(rolled).turn() == 'completed' and rolled.charge_fields('new')[12] == identity
+    # Equivalent partitions produce identical admitted roots, including persisted preparation.
+    reversed_state = deepcopy(base); reversed_state['unresolved'] = tuple(reversed(base['unresolved']))
+    normal = resume_publication(base,identity,carrier,'publication_retry_exhausted_hold','current')
+    permuted = resume_publication(reversed_state,identity,carrier,'publication_retry_exhausted_hold','current')
+    assert normal['state']['invocations'] == permuted['state']['invocations']
+    decoded = decode_record(normal['state']['window_claim_bytes'],'HX-EV-PUBLICATION-WINDOW-2',codec['schemas']['D45-window'])
+    independent_rows = b''.join(codec['pack']('>I',p)+U(m)+sha256(body).digest() for p,m,body in sorted(base['unresolved']))
+    expected = sha256(b'HX-EV-PUBLICATION-UNRESOLVED-1\0\x01'+codec['pack']('>I',2)+independent_rows).digest()
+    assert decoded[7] == expected != sha256(image_bytes(roster)).digest()
+    reversed_preparation = resume_publication(reversed_state,identity,carrier,'publication_drain_limit_hold','current',crash_after_audit=True)['state']['orphans'][identity]['preparation']
+    reversed_store = PreparationStore(identity,carrier,reversed_preparation,reversed_state)
+    assert reversed_store.turn() == 'completed'
+    invocation = decode_record(reversed_store.artifacts['invocation'],'HX-EV-PUBLICATION-INVOCATION-1',codec['extra_schemas']['D45-invocation'])
+    assert invocation[6] == expected and reversed_store.imported('window_claim_bytes') == image_bytes(base['window_claim_bytes'])
+    singleton = deepcopy(base); singleton['accepted'] = roster[:2]; singleton['unresolved'] = roster[2:]
+    singleton['window_claim_bytes'] = existing_window_bytes('t',roster,singleton['unresolved']); singleton['window_claim'] = sha256(singleton['window_claim_bytes']).digest()
+    one = resume_publication(singleton,identity,carrier,'publication_retry_exhausted_hold','current')
+    assert one['outcome'] == 'resumed' and decode_record(one['state']['window_claim_bytes'],'HX-EV-PUBLICATION-WINDOW-2',codec['schemas']['D45-window'])[7] == unresolved_root(roster[2:])
+    # Multiple local attempts retain every registration/Unknown/result while summaries select only the greatest local result.
+    rows = []
+    for p,m,body in roster:
+      for local in range(1,p+2):
+        parent = sha256(b'loop6-registration:'+N(p)+N(local)).digest(); send = sha256(b'send:'+parent).digest()
+        for observation,kind in enumerate(('register',)+('unknown',)*(local-1)+('result',)):
+            rows.append(codec['pack']('>I',p)+N(local)+N(observation)+U(kind)+parent+send+sha256(kind.encode()+parent+N(observation)).digest())
+    exact = b''.join(rows); roster_root = sha256(image_bytes(roster)).digest()
+    root = sha256(b'HX-EV-WINDOW-ATTEMPTS-2\0\x01'+U('t')+codec['H']('scope')+N(7)+roster_root+N(len(rows))+B(exact)).digest()
+    authority = R('HX-EV-WINDOW-ATTEMPT-SET-1',8,U('t'),codec['H']('scope'),N(7),roster_root,N(len(rows)),B(exact),root,Q(10000000000))
+    complete = resume_publication(base,identity,carrier,'publication_retry_exhausted_hold','current',crash_after_audit=True,attempt_authority=authority)
+    multi = PreparationStore(identity,carrier,complete['state']['orphans'][identity]['preparation'],base,eligible='retry-exhausted',attempt_authority=authority)
+    assert multi.turn() == 'completed'
+    closure = decode_record(multi.artifacts['closure'],'HX-EV-PUBLICATION-WINDOW-CLOSURE-3',codec['schemas']['D45-closure'],attempt_store={('t',codec['H']('scope'),7):authority})
+    summaries = codec['decode_rows'](closure[3][4:],3,['P','N','B32'])
+    assert [row[1] for row in summaries] == [2,3,4] and closure[7] == root
+    changed = authority[:-1]
+    try: publication_closure_bytes(base,changed,multi.imported('broker'),1000)
+    except AssertionError: pass
+    else: raise AssertionError('missing earlier attempt authority accepted')
+    capture_cases = 0
+    carrier = b'loop6-retained-carrier'; held_identity = ('tenant','deployment-a','t','pubsub','orders','sub-a')
+    policy_store = PolicyStore(); policy = policy_store.install(None,1,bytes(32))
+    def capture_fixture(ceiling=10000,crash=None):
+        ledger = Ledger(); ledger.inventory_ceiling = ceiling
+        observed = observe_delivery(None,638712864000000000,ledger,held_identity,carrier)
+        key = 'held/'+observed['held_key'].hex()
+        readback = {'backend':'held-delivery-store','key':key,'bytes':carrier,'receipt':object_receipt('held-delivery-store',key,carrier)}
+        return ledger,observed,readback,capture_delivery(observed,carrier,ledger,readback,policy,policy_store,crash_at=crash)
+    ledger,observed,readback,refused = capture_fixture(1)
+    assert refused == observed and len(ledger.inventory) == 1 and ledger.deployment == 32768 and len(ledger.charges) == 1
+    ledger,observed,readback,held = capture_fixture(2)
+    assert held['transport_copy_acked'] and len(ledger.inventory) == 2
+    for boundary in ('charge','object'):
+      for increments in (1,3):
+        ledger,observed,readback,partial = capture_fixture(2,boundary)
+        prior_charges = deepcopy(ledger.charges); prior_usage = ledger.deployment
+        for _ in range(increments): partial = observe_delivery(read_image(image_bytes(partial)),partial['first_observed']+1)
+        restored = read_image(image_bytes(partial)); persisted_ledger = read_image(image_bytes(vars(ledger)))
+        ledger.__dict__ = persisted_ledger
+        completed = capture_delivery(restored,carrier,ledger,readback,policy,policy_store)
+        assert completed['state'] == 'captured' and completed['delivery_attempt_count'] == 1+increments
+        assert completed['observation_revision'] == 1+increments and completed['first_observed'] == observed['first_observed']
+        assert ledger.charges == prior_charges and ledger.deployment == prior_usage and len(ledger.objects) == 1 and len(ledger.inventory) == 2
+        snapshot = deepcopy(vars(ledger)); assert capture_delivery(completed,carrier,ledger,readback,policy,policy_store) == completed and vars(ledger) == snapshot
+        for field in ('first_observed','observation_receipt','metadata_receipt'):
+            bad = deepcopy(restored); bad[field] = 0 if field == 'first_observed' else bytes(32)
+            assert capture_delivery(bad,carrier,ledger,readback,policy,policy_store) == bad and vars(ledger) == snapshot
+        capture_cases += 1
+    for boundary in ('charge','object'):
+        store,prior,readback,partial = capture_fixture(2,boundary)
+        changed_head = deepcopy(policy_store); changed_policy = changed_head.install(policy,2,policy['hash'])
+        snapshot = deepcopy(vars(store))
+        assert capture_delivery(partial,carrier,store,readback,changed_policy,changed_head) == partial and vars(store) == snapshot
+    for boundary in ('charge','object'):
+        store,prior,readback,partial = capture_fixture(2,boundary)
+        snapshot = deepcopy(vars(store))
+        assert rollback_capture(partial,carrier,store,None) == partial and vars(store) == snapshot
+        absence = presence(partial['held_key'],'capture','absent')
+        assert rollback_capture(partial,carrier,store,absence,False) == partial and vars(store) == snapshot
+        assert rollback_capture(partial,carrier,store,absence) == partial
+        assert store.deployment == 32768 and len(store.inventory) == len(store.charges) == 1 and not store.objects
+        refunded = deepcopy(vars(store)); assert rollback_capture(partial,carrier,store,absence) == partial and vars(store) == refunded
+    ledger,observed,readback,held = capture_fixture(2)
+    obligations = [('metadata',held['metadata_key']),('inventory',held['inventory_key']),('repair-charge',held['repair_charge_key']),('repair-interest',held['repair_inventory_key'])]
+    refusals = 0
+    for kind,key in obligations:
+      for damage in ('missing','owner','generation','account','state','receipt'):
+        store = deepcopy(ledger); prior = deepcopy(held); collection = store.inventory if kind in {'inventory','repair-interest'} else store.charges
+        if damage == 'missing': del collection[key]
+        elif damage == 'receipt': collection[key]['receipt'] = bytes(32)
+        else:
+            row = collection[key]
+            if damage == 'owner': row['owner'] = bytes(32)
+            elif damage == 'generation': row['generation'] = 2
+            elif damage == 'account': row['account'] = 'wrong-account'
+            elif kind in {'inventory','repair-interest'}: row['reserved'] = False
+            else: row['state'] = 'released'
+            row['receipt'] = state_hash({k:v for k,v in row.items() if k != 'receipt'})
+        snapshot = deepcopy(vars(store)); assert held_delivery(prior,cause_cleared=True,ledger=store) == prior and vars(store) == snapshot
+        refusals += 1
+    store = deepcopy(ledger); prior = deepcopy(held)
+    store.charges.clear(); store.inventory.clear(); store.objects.clear(); store.capture_origins.clear(); store.capture_preparations.clear()
+    snapshot = deepcopy(vars(store))
+    assert held_delivery(prior,cause_cleared=True,ledger=store) == prior and prior['redrive_count'] == 0 and prior['redrive_bytes'] is None and vars(store) == snapshot
+    refusals += 1
+    for damage in ('ledger-missing','held-missing','both-missing','ledger-changed','held-changed','both-changed','origin-missing'):
+        store = deepcopy(ledger); prior = deepcopy(held); key = held['retained_object_key']
+        if damage in {'ledger-missing','both-missing'}: del store.capture_preparations[key]
+        if damage in {'held-missing','both-missing'}: prior.pop('capture_preparation')
+        if damage in {'ledger-changed','both-changed'}: store.capture_preparations[key] += b'changed'
+        if damage in {'held-changed','both-changed'}: prior['capture_preparation'] += b'changed'
+        if damage == 'origin-missing': del store.capture_origins[key]
+        snapshot = deepcopy(vars(store)); assert held_delivery(prior,cause_cleared=True,ledger=store) == prior and vars(store) == snapshot
+        refusals += 1
+    def signed_row(row):
+        row = deepcopy(row); row['signature'] = sha256(b'authenticated-purpose-2d-redrive:'+row['raw']).digest()
+        row['receipt'] = sha256(b'provider-redrive-request-readback:'+image_bytes({k:v for k,v in row.items() if k != 'receipt'})).digest()
+        return row
+    request_cases = 0
+    first_request = signed_redrive_request(held)
+    request_schema = codec['schemas']['D36-redrive']
+    for damage in ('issuer','subject','scope','tenant','held-key','count','time','signature','receipt','owner','row-count'):
+        row = deepcopy(first_request); values = list(decode_record(row['raw'],'HX-EV-REDRIVE-REQUEST-2',request_schema))
+        if damage == 'issuer': values[0] = 'unauthorized-issuer'
+        elif damage == 'subject': values[5] = 'unauthorized-subject'
+        elif damage == 'scope': values[1:3] = ['deployment',None]
+        elif damage == 'tenant': values[2] = 'other-tenant'
+        elif damage == 'held-key': values[3] = bytes(32)
+        elif damage == 'count': values[4] = 1
+        elif damage == 'time': values[6] = held['first_observed']-1
+        elif damage == 'owner': row['owner'] = bytes(32)
+        elif damage == 'row-count': row['count'] = 2
+        row['raw'] = R('HX-EV-REDRIVE-REQUEST-2',7,*(codec['encode_typed'](kind,value) for kind,value in zip(request_schema,values)))
+        row = signed_row(row)
+        if damage == 'signature': row['signature'] = bytes(32); row['receipt'] = sha256(b'provider-redrive-request-readback:'+image_bytes({k:v for k,v in row.items() if k != 'receipt'})).digest()
+        elif damage == 'receipt': row['receipt'] = bytes(32)
+        store = deepcopy(ledger); snapshot = deepcopy(vars(store))
+        assert held_delivery(held,cause_cleared=True,ledger=store,request=row) == held and vars(store) == snapshot, damage
+        request_cases += 1
+    once_store = deepcopy(ledger)
+    once = held_delivery(held,cause_cleared=True,redrive_failed=True,ledger=once_store)
+    address = (held['held_key'],1)
+    for damage in ('provider-missing','held-missing','both-missing','signature-both','receipt-both','raw-both'):
+        store = deepcopy(once_store); prior = deepcopy(once)
+        if damage in {'provider-missing','both-missing'}: del store.redrive_requests[address]
+        if damage in {'held-missing','both-missing'}: prior.pop('request')
+        if damage == 'signature-both':
+            row = deepcopy(prior['request']); row['signature'] = bytes(32)
+            row['receipt'] = sha256(b'provider-redrive-request-readback:'+image_bytes({k:v for k,v in row.items() if k != 'receipt'})).digest()
+            store.redrive_requests[address] = prior['request'] = row
+        elif damage == 'receipt-both': store.redrive_requests[address]['receipt'] = prior['request']['receipt'] = bytes(32)
+        elif damage == 'raw-both':
+            row = deepcopy(prior['request']); row['raw'] += b'changed'; row = signed_row(row)
+            store.redrive_requests[address] = prior['request'] = row
+        snapshot = deepcopy(vars(store))
+        assert held_delivery(prior,cause_cleared=True,ledger=store) == prior and vars(store) == snapshot, damage
+        request_cases += 1
+    for flag in ('request_readback_available','request_delete_available'):
+        store = deepcopy(once_store); snapshot = deepcopy(vars(store))
+        assert held_delivery(once,cause_cleared=True,ledger=store,**{flag:False}) == once and vars(store) == snapshot
+        request_cases += 1
+    request_restarts = 0
+    erasure_refusals = 0
+    for prior,original_store in ((held,ledger),(once,once_store)):
+      for boundary in ('request-readback','request-delete-readback','request-attempt-commit'):
+        store = deepcopy(original_store); snapshot = deepcopy(vars(store))
+        pending = held_delivery(prior,cause_cleared=True,ledger=store,crash_after=boundary)
+        persisted = read_image(image_bytes(vars(store))); store.__dict__ = persisted
+        pending = read_image(image_bytes(pending))
+        if boundary != 'request-attempt-commit':
+            assert pending == prior and vars(store) == snapshot
+        else:
+            assert pending['state'] == 'redriving' and pending['redrive_bytes'] is None and pending['redrive_count'] == prior['redrive_count']+1
+            assert store.redrive_requests[address] == pending['request'] and store.redrive_attempts[address] == pending['attempt']
+            committed = deepcopy(vars(store))
+            assert held_delivery(pending,cause_cleared=True,ledger=store) == pending and vars(store) == committed
+            recovered = reconcile_redrive(pending,ledger=store)
+            assert recovered['state'] == 'captured' and not recovered.get('repair_required') and recovered['redrive_count'] == pending['redrive_count']
+            closed = held_delivery(recovered,cause_cleared=True,routes_terminal=True,ledger=store)
+            receipt = sha256(b'terminal-held-erasure:'+held['held_key']+N(closed['redrive_count'])).digest()
+            # Every deletion/readback precedes refund: bad route authority leaves the exact ledger.
+            before_erase = deepcopy(vars(store)); assert erase_held_delivery(closed,store,bytes(32)) == closed and vars(store) == before_erase
+            for damage in ('request-missing','attempt-missing','request-receipt'):
+                damaged = deepcopy(store)
+                if damage == 'request-missing': del damaged.redrive_requests[address]
+                elif damage == 'attempt-missing': del damaged.redrive_attempts[address]
+                else: damaged.redrive_requests[address]['receipt'] = bytes(32)
+                unchanged = deepcopy(vars(damaged)); assert erase_held_delivery(closed,damaged,receipt) == closed and vars(damaged) == unchanged
+                erasure_refusals += 1
+            erased = erase_held_delivery(closed,store,receipt)
+            assert erased['state'] == 'erased' and store.deployment == 0 and not store.redrive_requests and not store.redrive_attempts and not store.request_deletions and not store.attempt_deletions
+            refunded = deepcopy(vars(store)); assert erase_held_delivery(erased,store,receipt) == erased and vars(store) == refunded
+        request_restarts += 1
+    initial_charges = deepcopy(ledger.charges); initial_usage = ledger.deployment; repeated = deepcopy(held)
+    for _ in range(141):
+        ledger.__dict__ = read_image(image_bytes(vars(ledger)))
+        old_request = deepcopy(getattr(ledger,'redrive_requests',{}).get(address))
+        old_attempt = deepcopy(getattr(ledger,'redrive_attempts',{}).get(address))
+        repeated = held_delivery(read_image(image_bytes(repeated)),cause_cleared=True,redrive_failed=True,ledger=ledger)
+        assert len(ledger.redrive_attempts) == 1 and len(getattr(ledger,'attempt_deletions',{})) <= 1
+        assert len(ledger.redrive_requests) == 1 and len(getattr(ledger,'request_deletions',{})) <= 1
+        if old_request is not None:
+            assert ledger.request_deletions[address] == sha256(b'provider-redrive-request-deletion:'+old_request['receipt']).digest()
+            assert ledger.attempt_deletions[address] == sha256(b'provider-attempt-deletion:'+old_attempt['receipt']).digest()
+        assert all(len(receipt) == 32 for receipt in list(ledger.request_deletions.values())+list(ledger.attempt_deletions.values()))
+        retained_request = ledger.redrive_requests[address]; retained_attempt = ledger.redrive_attempts[address]
+        assert len(retained_request['raw']) <= 3072 and len(retained_request['signature']) == len(retained_request['receipt']) == 32
+        assert retained_request == repeated['request'] and redrive_request_authority(repeated,retained_request,repeated['redrive_count'])
+        assert retained_attempt['request_hash'] == sha256(retained_request['raw']).digest()
+        assert decode_record(retained_attempt['raw'],'HX-EV-REDRIVE-ATTEMPT-1',codec['extra_schemas']['D36-attempt'])[6] == retained_attempt['request_hash']
+        assert len(retained_attempt['raw']) <= 8192 and len(image_bytes(retained_request))+len(image_bytes(retained_attempt)) <= 19*1024
+        assert ledger.charges == initial_charges and ledger.deployment == initial_usage == repeated['charged_bytes'] == 64*1024+len(carrier)
+    assert repeated['redrive_count'] == 141
+    snapshot = deepcopy(vars(ledger))
+    assert held_delivery(repeated,cause_cleared=True,ledger=ledger,request=first_request) == repeated and vars(ledger) == snapshot
+    request_cases += 1
+    for damage in ('missing','receipt','owner'):
+        store = deepcopy(ledger); address = (held['held_key'],1)
+        if damage == 'missing': del store.redrive_attempts[address]
+        elif damage == 'receipt': store.redrive_attempts[address]['receipt'] = bytes(32)
+        else:
+            row = store.redrive_attempts[address]; row['owner'] = bytes(32)
+            row['receipt'] = state_hash({k:v for k,v in row.items() if k != 'receipt'})
+        snapshot = deepcopy(vars(store)); assert held_delivery(repeated,cause_cleared=True,ledger=store) == repeated and vars(store) == snapshot
+    for damage in ('malformed','typed-count','typed-request'):
+        store = deepcopy(ledger); prior = deepcopy(repeated); row = deepcopy(prior['attempt'])
+        if damage == 'malformed': row['raw'] += b'changed'
+        else:
+            fields = list(decode_record(row['raw'],'HX-EV-REDRIVE-ATTEMPT-1',codec['extra_schemas']['D36-attempt']))
+            fields[1 if damage == 'typed-count' else 6] = 1 if damage == 'typed-count' else bytes(32)
+            row['raw'] = R('HX-EV-REDRIVE-ATTEMPT-1',7,*(codec['encode_typed'](kind,value) for kind,value in zip(codec['extra_schemas']['D36-attempt'],fields)))
+        row['receipt'] = state_hash({k:v for k,v in row.items() if k != 'receipt'})
+        store.redrive_attempts[address] = prior['attempt'] = row
+        snapshot = deepcopy(vars(store)); assert held_delivery(prior,cause_cleared=True,ledger=store) == prior and vars(store) == snapshot
+    redriving = held_delivery(repeated,cause_cleared=True,ledger=ledger); bad = deepcopy(redriving); bad['attempt']['receipt'] = bytes(32)
+    required = reconcile_redrive(bad,ledger=ledger); genuine = ledger.redrive_attempts[(held['held_key'],1)]
+    for damage in ('missing','signature','receipt'):
+        store = deepcopy(ledger)
+        if damage == 'missing': del store.redrive_requests[address]
+        else: store.redrive_requests[address][damage] = bytes(32)
+        snapshot = deepcopy(vars(store))
+        disputed = reconcile_redrive(read_image(image_bytes(redriving)),ledger=store)
+        assert disputed['state'] == 'captured' and disputed['repair_required'] and disputed['redrive_count'] == redriving['redrive_count'] and vars(store) == snapshot
+        receipt = sha256(b'authenticated-attempt-repair:'+genuine['receipt']+held['readback_authority']).digest()
+        assert repair_redrive(disputed,store,receipt) == disputed and vars(store) == snapshot
+        request_cases += 1
+    repaired = repair_redrive(required,ledger,sha256(b'authenticated-attempt-repair:'+genuine['receipt']+held['readback_authority']).digest())
+    assert repaired['repair_required'] is None and repaired['repaired_record'] and repaired['repair_indexed']
+    cleanup_cases = 0
+    for boundary in ('repaired-readback','record-deleted','record-readback','entry-deleted'):
+        snapshot = deepcopy(vars(ledger))
+        pending = cleanup_redrive_repair(repaired,crash_after=boundary); pending = read_image(image_bytes(pending))
+        ledger.__dict__ = read_image(image_bytes(vars(ledger)))
+        assert held_delivery(pending,cause_cleared=True,ledger=ledger) == pending
+        assert cleanup_redrive_repair(pending,False) == pending
+        finished = cleanup_redrive_repair(pending); assert not finished['repair_indexed'] and finished['repair_entry'] is None
+        assert cleanup_redrive_repair(finished) == finished
+        assert vars(ledger) == snapshot and ledger.redrive_requests[address] == repaired['request'] and ledger.redrive_attempts[address] == repaired['attempt']
+        cleanup_cases += 1
+    finished = cleanup_redrive_repair(repaired)
+    sent = held_delivery(finished,cause_cleared=True,ledger=ledger); assert sent['redrive_count'] == 143 and sent['redrive_bytes'] == carrier
+    bad = deepcopy(sent); bad['attempt']['receipt'] = bytes(32); required = reconcile_redrive(bad,ledger=ledger)
+    assert required['repair_required']['count'] == 143 and required['repair_indexed'] and not required.get('repaired_record')
+    genuine = ledger.redrive_attempts[(held['held_key'],1)]; receipt = sha256(b'authenticated-attempt-repair:'+genuine['receipt']+held['readback_authority']).digest()
+    finished = cleanup_redrive_repair(repair_redrive(required,ledger,receipt))
+    closed = held_delivery(finished,cause_cleared=True,routes_terminal=True,ledger=ledger)
+    erased = erase_held_delivery(closed,ledger,sha256(b'terminal-held-erasure:'+held['held_key']+N(closed['redrive_count'])).digest())
+    assert erased['state'] == 'erased' and ledger.deployment == 0 and not ledger.objects and not ledger.charges and not ledger.inventory and not ledger.redrive_attempts and not ledger.redrive_requests and not ledger.request_deletions and not ledger.attempt_deletions
+    refunded = deepcopy(vars(ledger)); assert erase_held_delivery(erased,ledger,erased['erasure_receipt']) == erased and vars(ledger) == refunded
+    return {'utc':utc_cases,'partial_capture':capture_cases,'redrive_refusals':refusals,'request_refusals':request_cases,'request_restarts':request_restarts,'erasure_refusals':erasure_refusals,'cleanup':cleanup_cases,'failed_redrives':141}
+
+loop6_metrics = verify_loop6_transitions() if not globals().get('fault_probe') or str(globals().get('fault_name','')).startswith('loop6 ') else {}
 matrix_cases = [
     verify_eligible_resume_matrix(),
     verify_legacy_resume_matrix(),
@@ -1314,13 +4599,42 @@ assert matrix_cases == [
     'eligible-resume', 'legacy-resume', 'capacity-wait',
     'held-delivery-or-long-stream']
 
+supplementary_schemas = {
+    'D45-origin':['U','U','B32','B32','B','B','B','N','Q','Q'],
+    'D45-preparation':['U','U','B32','B32','B32','B','B','Q','Q','N','B','N'],
+    'D45-preparation-head':['U','U','B32','B32',('O','B32'),'U','N','B32','B','Q'],
+    'D36-capture-preparation':['B32','B32','B32','B32','U','U','B32','N','Q'],
+    'D36-repair':['B32','N','B32','B32','U','N','B32','U',('O','B32'),'Q'],
+    'D36-capture-origin':['B32','B','B32','Q','N','B32'],
+    'D36-current-request':['U','U',('O','U'),'B32','N','U','Q'],
+    'D36-attempt':['B32','N','B32','U','B32','Q','B32'],
+    'D36-cleanup':['B32','N','B32','B32','U',('O','B32'),('O','B32')],
+    'D45-invocation':['U','U','B32','N','N','B32','B32','B32','Q'],
+}
+supplementary_malformed = 0
+assert set(supplementary_records) == set(supplementary_schemas) == set(codec['extra_schemas'])
+assert all(raw == codec['extra_vectors'][label] for label,raw in supplementary_records.items())
+for label,raw in supplementary_records.items():
+    domain = raw.split(b'\0',1)[0].decode(); schema = supplementary_schemas[label]
+    fields = decode_record(raw,domain,schema)
+    segments = [bytes([i+1])+codec['encode_typed'](kind,value) for i,(kind,value) in enumerate(zip(schema,fields))]
+    prefix = len(domain.encode())+4
+    framed = next(i for i,k in enumerate(schema) if k in ('U','B'))
+    changed = list(segments); changed[framed] = changed[framed][:1]+b'\xff'*4+changed[framed][5:]
+    for mutant in (raw[:-1],raw[:prefix]+b'\x02'+raw[prefix+1:],
+                   raw[:prefix]+segments[1]+segments[0]+b''.join(segments[2:]),
+                   raw[:prefix]+b''.join(changed),raw+b'\x00'):
+        try: decode_record(mutant,domain,schema)
+        except (AssertionError,UnicodeError): supplementary_malformed += 1
+        else: raise AssertionError((label,'malformed supplementary record accepted'))
+assert supplementary_malformed == 50
+
 # Exact construction bounds and acyclic/retry invariants.
 assert 2455 + 943*1109 <= MiB and 2455 + 944*1109 > MiB
-assert 1258 + 59*1080 <= 64*1024 and 1258 + 60*1080 > 64*1024
-request_hash = b'request-hash'
-retry_cache = {request_hash: {'window':2, 'limit':24, 'expiry':100}}
-assert retry_cache[request_hash] == {'window':2, 'limit':24, 'expiry':100}
-assert request_hash not in {}  # expired rows have one closed unavailable outcome, not fabricated replay.
+assert 1291 + 59*1080 <= 64*1024 and 1291 + 60*1080 > 64*1024
+assert 128 + 61*1068 <= 64*1024 and 128 + 62*1068 > 64*1024
+counter_predecessors = Ledger().predecessors('t')
+assert set(counter_predecessors) == {'tenant','tenant-pool','deployment'}
 
 closure_bytes = b'closure-without-successor'
 successor_accumulator = sha256(b'predecessor' + closure_bytes + b'broker-auth').digest()
@@ -1330,36 +4644,70 @@ audit_bytes = b'audit' + prior_state_hash
 audit_hash = sha256(audit_bytes).digest()
 successor_state_bytes = b'state' + successor_accumulator + audit_hash
 assert sha256(successor_state_bytes).digest() not in audit_bytes and audit_hash in successor_state_bytes
+stable_request_identity = sha256(b'caller-stable').digest()
+window_bytes = b'window' + prior_state_hash + stable_request_identity
+window_hash = sha256(window_bytes).digest()
+successor_with_window = b'state' + window_hash + audit_hash
+assert sha256(successor_with_window).digest() not in window_bytes and window_hash in successor_with_window
+
+policy1 = policy_revision(None,1,bytes(32))
+assert policy1['raw'] == codec['vectors']['D36-policy']
+policy2 = policy_revision(policy1,2,policy1['hash'])
+assert policy2 and policy_revision(None,2,bytes(32)) is None
+assert policy_revision(policy1,3,policy1['hash']) is None
+assert policy_revision(policy1,2,bytes(32)) is None
+assert policy_revision(policy2,2,policy2['hash']) is None
+assert policy_selected(policy2,policy2['hash']) and not policy_selected(policy1,policy2['hash'])
+other_policy1 = policy_revision(None,1,bytes(32),config=sha256(b'other-config').digest())
+other_policy2 = policy_revision(other_policy1,2,other_policy1['hash'])
+assert policy1['hash'] != bytes(32) != other_policy1['hash']
+assert policy2['hash'] != other_policy2['hash']
+expected_policy2 = R('HX-EV-SUBSCRIPTION-DELIVERY-POLICY-3',13,U('deployment-a'),U('pubsub'),U('orders'),
+    U('sub-a'),N(2),policy1['hash'],U('successor'),U('dead-letter-capture'),O(U('orders-dlq')),
+    N(8),codec['H']('subscription-config'),U('dapr-configuration'),Q(codec['t']))
+assert policy2['hash'] == sha256(expected_policy2).digest() and policy2['raw'] == expected_policy2
+durable_policies = PolicyStore()
+installed1 = durable_policies.install(None,1,bytes(32))
+installed2 = durable_policies.install(installed1,2,installed1['hash'])
+assert installed2 == policy2 and durable_policies.selected(installed2)
+policy_snapshot = deepcopy(vars(durable_policies))
+assert durable_policies.install(installed1,2,installed1['hash']) == installed2
+assert vars(durable_policies) == policy_snapshot
+assert durable_policies.install(installed1,2,installed1['hash'],sha256(b'competing-policy').digest()) is None
+assert vars(durable_policies) == policy_snapshot and len(durable_policies.revisions) == 2
+assert durable_policies.install(installed1,3,installed1['hash']) is None
+assert durable_policies.install(installed2,3,installed1['hash']) is None
+assert not durable_policies.selected(installed1) and durable_policies.selected(installed2)
 
 # Mutation killers: each expression describes an acceptance-breaking mutant and must be rejected.
 mutants_rejected = [
-    status('unknown', at_max=True)[1] is None,                         # no exhaustion hold for unknown
+    status('failed', failure_classes=('unknown',), at_max=True)[1] == 'outcome_evidence_conflict',
     not ledger.reserve('capture-scope', 'scope-c', [513*MiB]),        # unidentified ceiling retained
     not ledger.reserve('unknown', 'scope-c', [1]),                    # closed account kind
+    not ledger.refund('unknown', 't1', 1),                            # closed refund kind
     capacity_admit(ledger, 't1', [-1])[0] == 'publication_pin_capacity_hold', # stable arithmetic hold
-    ledger.tenant['t1'] == 700*MiB and ledger.tenant_pool == 700*MiB and ledger.deployment == 700*MiB,
+    set(counter_predecessors) == {'tenant','tenant-pool','deployment'},
     queues.where['a'] == 'deployment',                                # cross-counter return exists
     queues.where['b'] == 'deployment',                                # deployment head refused by own tenant does not block t2
+    not queues.add(4, 't3', 'c'),                                     # duplicate stable subject rejected
+    not ticket_exhausted.add(U64_MAX, 't', 'never-wraps'),
+    decode_queue(valid_rows, 1, 0, reserved_count=3, ceiling=3) == 'pin_capacity_queue_corruption_hold',
     membership_exit('manual', True, True) != 'ContinueSamePin',
     membership_exit('membership-revision', True, True) == 'ContinueSamePin',
-    legacy_resume(False, True, True) != 'rearm-stored-range',
     status('pending', drain_active=False)[1] != 'publication_drain_limit_hold',
-    status('failed', failure_class='class-02', at_max=True)[1] == 'terminal_evidence_hold',
-    status('failed', failure_class='class-03')[1] == 'terminal_evidence_hold',
+    status('failed', failure_classes=('class-02',), at_max=True)[1] == 'terminal_evidence_hold',
+    status('failed', failure_classes=('class-03',))[1] == 'terminal_evidence_hold',
     status('published', classification='rejection')[0] == 'Rejected',
     status('published', classification='unknown')[1] == 'outcome_evidence_conflict',
-    legacy_resume(True, True, False) == 'legacy_resume_evidence_unavailable',
-    render_recovery('bad', None, 'x') == 'response_preparation_hold',
+    render_recovery('x-response', None, receipt, receipt, 'x', 7) == 'response_preparation_hold',
+    render_recovery('x-response', 'x-outcome', receipt, ('receipt','x',6), 'x', 7) == 'response_preparation_hold',
     len(queues.rows['deployment']) + len(queues.reservations['deployment']) <= queues.ceiling,
-    decode_queue(valid_rows, 1, 1) == 'pin_capacity_queue_corruption_hold',
     parked_only.deployment_turn(lambda _: True, lambda _: True) == 'noop',
-    ledger.unidentified <= ledger.unidentified_ceiling,
     full_replay_exit(75000, 1, 1, False)['outcome'] == 'LegacyArrayLimit',
     full_replay_exit(100001, 1, 1, False)['applied_events'] == 0,
-    2455 + 944*1109 > MiB,
-    1258 + 60*1080 > 64*1024,
-    successor_accumulator not in closure_bytes,
-    sha256(successor_state_bytes).digest() not in audit_bytes,
+    not post_activation_growth(100001, 1, 1)['dispatch'],
+    1291 + 60*1080 > 64*1024,
+    sha256(successor_with_window).digest() not in window_bytes,
 ]
 assert all(mutants_rejected), mutants_rejected
 
@@ -1372,7 +4720,133 @@ expected = ({'VG2-2','VG2-3','VG2-4','VG2-O1','VG2-O2','VG2-O3'}
             | {f'BH2-{n}' for n in list(range(1,11))+[16,18,19]}
             | {f'E2-{n}' for n in list(range(1,27))+[28,29,30,31,32,34,35,36,38]})
 assert len(found) == 54 and set(found) == expected and len(found) == len(set(found)), (len(found), expected-set(found), set(found)-expected)
-print(f'D12 lifecycle verifier: {len(status_cases)} status cases, {len(matrix_cases)} matrix rows, {len(mutants_rejected)} mutants, {len(found)} dispositions passed')
+repair_section = text.split('### Review-loop-3 ' + 'repair register',1)[1].split('## Protected-path',1)[0]
+repair_ids = re.findall(r'\b(?:BHR3|ECR3|VGR3)-\d{2}\b',repair_section)
+expected_repairs = ({f'BHR3-{i:02}' for i in range(1,13)}
+                    | {f'ECR3-{i:02}' for i in range(1,7)}
+                    | {f'VGR3-{i:02}' for i in range(1,4)})
+assert len(repair_ids) == len(set(repair_ids)) == 21 and set(repair_ids) == expected_repairs
+loop4_section = text.split('### Review-loop-4 ' + 'repair register',1)[1].split('## Protected-path',1)[0]
+loop4_ids = re.findall(r'\b(?:BHR4|ECR4|VGR4)-\d{2}\b',loop4_section)
+expected_loop4 = ({f'BHR4-{i:02}' for i in range(1,13)}
+                 | {f'ECR4-{i:02}' for i in range(1,7)} | {f'VGR4-{i:02}' for i in range(1,3)})
+assert len(loop4_ids) == len(set(loop4_ids)) == 20 and set(loop4_ids) == expected_loop4
+loop5_section = text.split('### Review-loop-5 ' + 'repair register',1)[1].split('## Protected-path',1)[0]
+loop5_ids = re.findall(r'\b(?:(?:BHR5|ECR5)-\d{2}|VGR5-(?:01|O1))\b',loop5_section)
+expected_loop5 = ({f'BHR5-{i:02}' for i in range(1,11)} | {f'ECR5-{i:02}' for i in range(1,10)} | {'VGR5-01','VGR5-O1'})
+assert len(loop5_ids) == len(set(loop5_ids)) == 21 and set(loop5_ids) == expected_loop5
+loop6_section = text.split('### Review-loop-6 ' + 'repair register',1)[1].split('## Protected-path',1)[0]
+loop6_ids = re.findall(r'\b(?:(?:BHR6|ECR6)-\d{2}|VGR6-(?:01|O1|O2))\b',loop6_section)
+expected_loop6 = {f'BHR6-{i:02}' for i in range(1,11)} | {f'ECR6-{i:02}' for i in range(1,7)} | {'VGR6-01','VGR6-O1','VGR6-O2'}
+assert len(loop6_ids) == len(set(loop6_ids)) == 19 and set(loop6_ids) == expected_loop6
+
+# Directed source mutations rerun the owning executable assertions in isolation.
+# Splitting before this marker prevents recursively running the mutation harness.
+lifecycle_source = re.findall(r'```bash\npython3 - <<\'PY\'\n(.*?)\nPY\n```',candidate_text,re.S)[1]
+probe_source = lifecycle_source.split('# Directed source mutations rerun',1)[0]
+faults = [
+ ('allocator rejects its allocated ticket','lifecycle',[("not 0 < ticket <= self.last_ticket","not self.last_ticket < ticket <= U64_MAX")]),
+ ('drain-only replaces claim','lifecycle',[("claim = prior['window_claim_bytes']","claim = b'changed-drain-only-window'")]),
+ ('live retry loses response','lifecycle',[("'response':row['response'],'command_executions':0}","'response':b'label-only','command_executions':0}")]),
+ ('orphan leaves prior state','lifecycle',[("recovered = deepcopy(successor_intent)","recovered = deepcopy(prior)")]),
+ ('live expiry never converts','lifecycle',[("for identity,row in list(successor['live'].items()):\n        if now >= row['expires_at']:","for identity,row in list(successor['live'].items()):\n        if False:")]),
+ ('chunk uses counted-root domain','lifecycle',[("root = sha256(b'HX-EV-LEGACY-RESUME-CHUNK-ROWS-1\\0\\x01' + B(raw_rows)).digest()","root = sha256(b'HX-EV-LEGACY-RESUME-EVENTS-2\\0\\x01' + B(raw_rows)).digest()")]),
+ ('recovery ordinal exceeds u64','lifecycle',[("or not 1 <= ordinal <= U64_MAX","or ordinal < 1")]),
+ ('recovery generation exceeds u64','lifecycle',[("or not 1 <= expected_generation <= U64_MAX","or expected_generation < 1"),("next_generation = checked_add(record['generation'],1)","next_generation = record['generation']+1")]),
+ ('competing recovery owner wins','lifecycle',[("or owner != record['owner'] or next_generation is None","or next_generation is None")]),
+ ('unverified repair clears failure','lifecycle',[("repair == repair_receipt(record,bundle)","True")]),
+ ('redrive resets durable count','lifecycle',[("state = deepcopy(previous)\n    if state['state'] == 'closed': return state","state = deepcopy(previous)\n    state['redrive_count'] = 0\n    if state['state'] == 'closed': return state")]),
+ ('capture skips quota','lifecycle',[("if existing_charge is None and not ledger.reserve(previous['account_kind'],previous['account'],[len(retained),32*1024]):","if False:")]),
+ ('capture skips object readback','lifecycle',[("if readback != expected:","if False:")]),
+ ('policy skips a revision','lifecycle',[("revision != expected_revision","False")]),
+ ('pin reservation ignores predecessors','lifecycle',[("predecessors != self.predecessors(account)","False")]),
+ ('request identity includes reason','lifecycle',[("+U(tenant)+U(handle)+U(identity_key)).digest()","+U(tenant)+U(handle)+U(identity_key)+U(reason)).digest()")]),
+ ('held key omits tenant','lifecycle',[("O(None if tenant is None else U(tenant))","O(None)")]),
+ ('policy hash omits predecessor','lifecycle',[("N(revision),predecessor,U('initial'","N(revision),bytes(32),U('initial'")]),
+ ('signed decoder returns bytes','codec',[("elif kind == 'I': return int.from_bytes(take(4), 'big', signed=True)","elif kind == 'I': return take(4)")]),
+ ('semantic legacy count ceiling removed','codec',[("assert 1 <= values[10] <= 1000 and values[9] >= values[8]","assert values[9] >= values[8]")]),
+ ('rollback discards audited stage','lifecycle',[("if audit == 'present' and successor == 'absent':","if False:")]),
+ ('pin reservation ignores owner','lifecycle',[("row['identity'] == identity","True")]),
+ ('queue conflict cancels another preparation','lifecycle',[("# An untrusted conflicting caller cannot cancel the authenticated owner.\n            return False","# Broken rollback\n            for values in self.reservations.values(): values.discard(scope)\n            self.pending.pop(scope); self.tickets.pop(scope); self.charges.pop(scope)\n            return False")]),
+ ('resume ignores decoded source','lifecycle',[("or source != state['hold_source']","or False")]),
+ ('orphan loses reconciled indexes','lifecycle',[("recovered['tombstones'] = deepcopy(state['tombstones'])","recovered['tombstones'] = deepcopy(prior['tombstones'])")]),
+ ('orphan bypasses authentication','lifecycle',[("if row['receipt'] != state_hash(immutable) or row['owner'] != request_identity:","if False:")]),
+ ('expired tombstone lookup removed','lifecycle',[("if request_identity in state['tombstones']:","if False:")]),
+ ('capture bypasses metadata and inventory','lifecycle',[("if (not authenticated(metadata,previous['metadata_receipt'])","if False and (not authenticated(metadata,previous['metadata_receipt'])")]),
+ ('capture reuses one object locator','lifecycle',[("backend, key = 'held-delivery-store','held/'+subject.hex()","backend, key = 'held-delivery-store','held/constant'")]),
+ ('crashed redrive remains stranded','lifecycle',[("return held_delivery(previous,redrive_failed=True)","return deepcopy(previous)")]),
+ ('policy revision store permits replacement','lifecycle',[("if existing is not None and existing != candidate: return None","if False: return None"),("if self.head != expected_head: return None","if False: return None")]),
+ ('reserve counter generation wraps','lifecycle',[("if not fits or any(self.generations.get(key,0) == U64_MAX for key in changed):","if not fits:")]),
+ ('refund counter generation wraps','lifecycle',[("or any(self.generations.get(key,0) == U64_MAX for key in changed)","or False")]),
+ ('large family fields capped at one MiB','codec',[("assert length <= maximum","assert length <= 1024*1024")]),
+ ('ordinary identifiers widened to 4096','codec',[("record_caps.get(domain,4096) if base_kind == 'B' else 1024)","record_caps.get(domain,4096) if base_kind == 'B' else 4096)")]),
+ ('capability allows one shard','codec',[("assert values[11] == 256 and 1 <= values[13] <= 50000","assert 1 <= values[11] <= 256 and 1 <= values[13] <= 50000")]),
+ ('directory shard 256 allowed','codec',[("assert 0 <= values[0] <= 255 and values[2]+values[6] <= 50000","assert 0 <= values[0] <= 256 and values[2]+values[6] <= 50000")]),
+ ('charge genesis predecessor ignored','codec',[("genesis(values[9],values[11]); assert values[8] > 0","assert values[9] > 0 and values[8] > 0")]),
+ ('complete attempt root ignored','codec',[("assert complete[6] == values[7]","assert True")]),
+ ('successful live ordinals duplicated','codec',[("assert len({row[2] for row in live}) == len(live)","assert True")]),
+ ('per-kind charge ceilings ignored','codec',[("assert values[5] <= ceilings[values[4]] and values[6] <= 1114112","assert values[6] <= 1114112")]),
+ ('recorded overhead ceiling ignored','codec',[("assert values[5] <= ceilings[values[4]] and values[6] <= 1114112","assert values[5] <= ceilings[values[4]]")]),
+ ('policy allows sixty-five attempts','codec',[("assert 1 <= values[9] <= 64","assert 1 <= values[9] <= 65")]),
+ ('orphan ignores current expiry','lifecycle',[("recovered = reconcile_tombstones(recovered,now)","recovered = reconcile_tombstones(recovered,1000)")]),
+ ('drain invocation omits authorization epoch','lifecycle',[("+claim+N(ordinal)+N(limit)+request_identity+root","+claim+root")]),
+ ('response omits decoded handle','lifecycle',[("{'resumeHandle':handle,'resumeOrdinal':ordinal,'window':window","{'resumeOrdinal':ordinal,'window':window")]),
+ ('response substitutes fixture handle','lifecycle',[("{'resumeHandle':handle,'resumeOrdinal':ordinal","{'resumeHandle':'h','resumeOrdinal':ordinal")]),
+ ('partial capture refuses its existing charge','lifecycle',[("if existing_object is not None or existing_charge is not None:\n        if (existing_charge != object_owner","if existing_object is not None or existing_charge is not None: return state\n    if existing_object is not None or existing_charge is not None:\n        if (existing_charge != object_owner")]),
+ ('corrupt redrive forgets repair prerequisite','lifecycle',[("if state.get('repair_required') or not retained_authority(previous,ledger)","if not retained_authority(previous,ledger)")]),
+ ('redrive ignores retained-byte authority','lifecycle',[("if state.get('repair_required') or not retained_authority(previous,ledger)","if state.get('repair_required')")]),
+ ('forged repair receipt clears evidence hold','lifecycle',[("if receipt != expected: return state","if False: return state")]),
+ ('unavailable readback permits rollback','lifecycle',[("if successor is None or audit is None:\n            return False","if successor is None or audit is None:\n            successor = successor or 'absent'; audit = audit or 'absent'")]),
+ ('ordinary capture accepts oversize','lifecycle',[("if len(retained) > 193*MiB: return state","if False: return state")]),
+ ('preparation refunds before deletion readback','lifecycle',[("if not deletion_available: return 'cleanup-hold'","if not deletion_available:\n                    view = self.swap\n                    view.recover(self.owner,successor_receipt,audit_receipt)\n                    self.save_charges(self.owner,view.old,0,'released',view.used)\n                    return 'cleanup-hold'")]),
+ ('preparation re-signs original claim','lifecycle',[("if kind == 'claim': return self.origin_fields()[5]","if kind == 'claim': return self.origin_fields()[5]+b'changed-time'")]),
+ ('reconstruction accepts noncanonical images','codec',[("assert canonical_image_bytes(result) == raw","assert True")]),
+ ('invocation ignores authorization identity','codec',[("assert values[7] == sha256(b'HX-EV-PUBLICATION-INVOCATION-1\\0\\x01'+values[2]+N(values[3])+N(values[4])+values[5]+values[6]).digest()","assert True")]),
+ ('preparation hold accepts wrong owner','codec',[("if values[2] == 'PublicationResumePreparationHold': assert values[11] == 'coordinator'","if False: assert values[11] == 'coordinator'")]),
+ ('repair hold accepts wrong owner','codec',[("if values[2] == 'RedriveEvidenceRepairHold': assert values[11] == 'operations'","if False: assert values[11] == 'operations'")]),
+ ('restart restores process artifact cache','lifecycle',[("restored.backend = store.backend","restored.backend = store.backend\n    restored.raw_artifacts = store.raw_artifacts")]),
+ ('preparation installs unrelated audit fixture','lifecycle',[("if kind == 'audit':\n            raw = R(","if kind == 'audit':\n            return codec['audit']\n            raw = R(")]),
+ ('preparation installs unrelated state fixture','lifecycle',[("return publication_state_bytes(successor,completion_time)","return codec['state']")]),
+ ('provider readback ignores owner and generation','lifecycle',[("assert recorded_owner == owner and receipt == self.receipt(key,raw,owner,generation)","assert True")]),
+ ('persisted completion resurrects expired live retry','lifecycle',[("successor = reconcile_tombstones(successor,completion_time)","successor = successor")]),
+ ('missing progress head bootstraps over written work','lifecycle',[("assert all((lambda row: row is None or kind == 'state' and self.predecessor_readback(row[0]))(","assert True or all((lambda row: row is None or kind == 'state' and self.predecessor_readback(row[0]))(")]),
+ ('claim confuses A8 head with D9 predecessor','lifecycle',[("sha256(a8_head).digest(),N(prior['ordinal']+1),previous_audit","sha256(predecessor).digest(),N(prior['ordinal']+1),previous_audit")]),
+ ('claim loses predecessor successful audit','lifecycle',[("N(prior['ordinal']+1),previous_audit,U(prior['handle'])","N(prior['ordinal']+1),bytes(32),U(prior['handle'])")]),
+ ('loop6 later UTC regenerates pending successor','lifecycle',[("now if kind == 'state' and kind not in rows else None","now if kind == 'state' else None")]),
+ ('loop6 acknowledged successor skips expiry','lifecycle',[("self.reconcile_successor(now)","pass")]),
+ ('loop6 later observations replace original capture','lifecycle',[("original = original_observation(origins[key],previous) if key in origins else previous","original = previous")]),
+ ('loop6 capture omits repair inventory capacity','lifecycle',[("or repair_key in ledger.charges or repair_inventory_key in ledger.inventory\n                or len(ledger.inventory) >= ledger.inventory_ceiling","or repair_key in ledger.charges or repair_inventory_key in ledger.inventory\n                or False")]),
+ ('loop6 redrive omits metadata charge','lifecycle',[("not charge_authority(previous['metadata_key'],32*1024,previous['metadata_receipt'])","False")]),
+ ('loop6 redrive omits original interest','lifecycle',[("not interest_authority(previous['inventory_key'],previous['inventory_receipt'])","False")]),
+ ('loop6 redrive omits repair charge','lifecycle',[("not charge_authority(previous['repair_charge_key'],32*1024)","False")]),
+ ('loop6 redrive omits repair interest','lifecycle',[("not interest_authority(previous['repair_inventory_key'],previous['repair_inventory_receipt'],'redrive-repair')","False")]),
+ ('loop6 preparation comparison removed','lifecycle',[("return getattr(ledger,'capture_preparations',{}).get(key) == previous.get('capture_preparation')","return True")]),
+ ('loop6 preparation presence obligation removed','lifecycle',[("if not preparation_authority(previous,ledger): return False","if False: return False")]),
+ ('loop6 repaired prerequisite bypasses cleanup','lifecycle',[("if state.get('repaired_record') or state.get('repair_cleanup'): return deepcopy(previous)","if False: return deepcopy(previous)")]),
+ ('loop6 reclamation deletion omitted','lifecycle',[("del attempts[address]","pass")]),
+ ('loop6 request authority omitted','lifecycle',[("and row['signature'] == sha256(b'authenticated-purpose-2d-redrive:'+row['raw']).digest()","and True")]),
+ ('loop6 request reclamation omitted','lifecycle',[("del requests[address]","pass")]),
+ ('loop6 unbounded addressed attempt log','lifecycle',[("address = (state['held_key'],1)  # one current/disputed authority slot","address = (state['held_key'],next_count)")]),
+ ('loop6 window includes accepted members','lifecycle',[("identity,unresolved_root(prior['unresolved'])","identity,sha256(image_bytes(prior['roster'])).digest()")]),
+ ('loop6 incoming invocation order changes root','lifecycle',[("for position,message,body in sorted(unresolved)","for position,message,body in unresolved")]),
+ ('loop6 persisted invocation order changes root','lifecycle',[("for p,message,body in sorted(unresolved)","for p,message,body in unresolved")]),
+ ('loop6 every historical result becomes final','lifecycle',[("final = b''.join(codec['pack']('>I',p)+N(local)+evidence for p,(local,observation,evidence) in sorted(last.items()))","final = b''.join(codec['pack']('>I',p)+N(local)+evidence for p,local,observation,kind,parent,send,evidence in rows if kind == 'result')")]),
+ ('loop6 released transfer owner removed','lifecycle',[("O(owner if kind == 'new' else None)","O(owner if kind == 'new' and state != 'released' else None)")]),
+]
+source_mutations_rejected = 0
+for name,target,replacements in faults:
+    mutated = codec_source if target == 'codec' else probe_source
+    for old,new in replacements:
+        assert old in mutated, (name,'missing mutation target')
+        mutated = mutated.replace(old,new,1)
+    try:
+        with redirect_stdout(StringIO()): exec(mutated,{'fault_probe':True,'fault_name':name})
+    except (AssertionError,KeyError,TypeError,ValueError,AttributeError):
+        source_mutations_rejected += 1
+    else:
+        raise AssertionError((name,'source mutation survived owning verifier'))
+assert source_mutations_rejected == 87
+print(f'D12 lifecycle verifier: {len(status_cases)} status cases, {len(matrix_cases)} matrix rows, {len(mutants_rejected)} invariant checks, {source_mutations_rejected} source mutations rejected, {len(found)} dispositions, {len(supplementary_records)} transition codec matches and {supplementary_malformed} transition malformed rejections, {len(loop5_ids)} loop-5 and {len(loop6_ids)} loop-6 repairs passed; {preparation_metrics["restarts"]} persisted-only restart boundaries, {preparation_metrics["cleanup"]} cleanup boundaries, {preparation_metrics["refusals"]} durable-evidence refusals and {preparation_metrics["expired_completions"]} current-time completions; loop6 {loop6_metrics}')
 PY
 ```
 
@@ -1388,16 +4862,16 @@ Story 6.5 imports this candidate as one change. It does not retain the loop-1 te
 | `[I-10]`, `[I-14]`, `[I-15]`, `[I-16]` | Replace with D3's precedence, drain epochs/resolution, polling values, complete drain-reason classification, preparation recovery, closed `CommandOutcomeHold` set, and Admin current-hold join. |
 | `[I-12]` | Replace with D4's slice-2 claim, cutover/sunset, 256-shard accounting, tombstone reconciliation/expiry, status 410, availability outcome, erasure, and codecs. |
 | `[I-17]` | Replace with D6. |
-| `[I-29]`, `[I-30]` | Replace with D7's capability, exact charge/counter codecs, unidentified ceiling, two-phase atomic batch reservation, quarantine kind, checked arithmetic, stable fail-closed `publication_pin_capacity_hold`, zero partial mutation, refunds, and activation. `[I-26]` and `[I-28]` cite these codecs and maxima rather than defining another charge. |
-| `[I-31]` | Replace with D8's inventory-only invalid-candidate state, checked transition into the single-residence fair queue, cross-counter moves, parked discovery, exact count framing, rerender rule, storage, activation, and exits. |
-| `[I-36]` | Replace with D11.1, including mandatory bounded capture for formerly unbounded redelivery, terminal/oversize quarantine, unambiguous routes, refund, erasure, and activation. |
+| `[I-29]`, `[I-30]` | Replace with D7's capability, exact charge/counter codecs, scope-ceiling readiness, all-three-counter predecessor authentication, staged charge transfer, atomic batch reservation, quarantine kind, checked arithmetic, stable fail-closed `publication_pin_capacity_hold`, zero partial mutation, closed reservation/refund kinds, and activation. `[I-26]` and `[I-28]` cite these codecs and maxima rather than defining another charge. |
+| `[I-31]` | Replace with D8's inventory-only invalid-candidate state, durable global ticket allocator/exhaustion, duplicate-subject rejection, canonical order, authenticated ceiling/reserved-slot decode, single-residence fair queue, cross-counter moves, parked discovery, exact count framing, rerender rule, storage, activation, and exits. |
+| `[I-36]` | Replace with D11.1, including policy and scope identity, maximum-inclusive ordinary capture, addressed charged partial-capture completion, fresh retained-byte/locator/charge authority before every redrive, persistent repair prerequisite and its precharged inventory slot, terminal/oversize quarantine, unambiguous routes, refund, erasure and activation. |
 | `[I-37]` | Replace with D11.2's scope-discriminated IDs/routes, versioned reason, ordered index, bounded directory, zero-overflow readiness, reconciler lease, metrics, Admin join, storage, activation, and erasure. |
-| `[I-45]` | Replace with D9. Purpose `2d`'s assignment becomes: D2 activation, D9 resume/window claims, and D11 redrive only. Request discovery, bounded rolling state, capacity swap, success-only audit, drain-limit resolution, and replies are inseparable. |
-| `[I-46]` | Replace with D10's pre-cleanup capsule and exclusive recovery. No reconstruction from source-less historical status/dead letter is permitted. |
+| `[I-45]` | Replace with D9. Purpose `2d`'s assignment becomes: D2 activation, D9 resume/window claims and D11 redrive only. Import D9.4's bounded origin/reconstruction/progress/invocation codecs, slots/addresses/cleanup authority, complete attempt-set root, current-time retry expiry and independent canonical response. Every invocation consumer uses the ordinal/limit/request-bound identity. Caller identity, acyclic hashes, staged charge swap, audit, unchanged drain-only claim and replies are inseparable. |
+| `[I-46]` | Replace with D10's pre-cleanup chunk/manifest capsule and generation/ordinal-fenced exclusive recovery. No reconstruction from source-less historical status/dead letter is permitted, and repeated exhaustion reuses the immutable capsule. |
 | 6.5c C1 | Replace “pin charge atomically at pin CAS” with D7's ledger batch reservation plus reservation-bound pin installation. Preserve exact global pins, ceilings, and no-send-before-readback. Add `oversize-quarantine` only for invalid carriers under D11. |
 | 6.5c C2 | Amend the first-send outcome in place with D5's versioned resolution. Add D9's window namespace/binding to new resumed sends; the whole-operation terminal fence still precedes duplicates. |
 | 6.5c C4 | Amend acknowledgement in place with D11's authenticated captured-copy terminal handoff: it may acknowledge that physical copy while explicitly leaving every logical route/effect obligation open; it is not a successful route decision and cannot satisfy C4's ordinary success proof. |
-| 6.5c C5 | Amend closure in place exactly as D9.2: terminal closure uses the permanent operation fence; resume uses a window fence and authenticated closure accumulator. C5's terminal verifier consumes the window chain. BC-02 points to D9/D10, never command re-execution. |
+| 6.5c C5 | Amend closure in place exactly as D9.2: terminal closure uses the permanent operation fence; resume uses a permanent window fence, addressed complete registration/Unknown/result set and authenticated closure accumulator. C2/C5 verify complete evidence against final summaries and consume the window chain. BC-02 points to D9/D10, never command re-execution. |
 | A8 preparation | Keep unchanged A8/[I-09] authority by exact name: `HX-EV-RESPONSE-PREPARATION-WRITE-1` at `command-response-preparation-write:` plus ScopeOpHash. Recovery requires both existing immutable outputs and generation-bound receipts; a missing output remains `response_preparation_hold`. |
 
 ### D13.2 §8.1 outcome rows
@@ -1441,7 +4915,7 @@ All new routes and record fields are otherwise additive under §10.3. Provider/c
 
 ### D13.4 §11.5 and §11.6
 
-Replace the loop-1 disposition rows for the owned rules with the pass-1 and pass-2 tables below. In §11.6, remove the old owned `I06`, `I12`, `I14`, `I17`, `I29`, `I31`, `I36`, `I37`, `I45`, and `I46` known answers and import all 30 `D*` answers, six framed-key answers, and both D12 verifier blocks. Retain unowned A/B/C and integration answers unchanged. Verification must assert 30 codec answers, 30 codec mutations, six keys, 14 status cases, 4 matrix rows, 27 lifecycle mutants, and 54 unique pass-2 dispositions.
+Replace the loop-1 disposition rows for owned rules with the pass-1/pass-2 tables below. In §11.6 remove the old owned `I06`, `I12`, `I14`, `I17`, `I29`, `I31`, `I36`, `I37`, `I45`, `I46` answers and import 30 original plus twelve supplementary `D*` answers, six framed keys and both D12 blocks. Retain unowned A/B/C and integration answers unchanged. Assert 42 exact answers/digest probes, six keys, 200 distinct framed-malformation rejections, 70 semantic rejections, 14 status cases, four matrix rows, 27 invariant checks, 87 rejected source mutations, ten actual-transition codec matches with 50 repeated malformed rejections, 46 persisted-only restart boundaries, four cleanup boundaries, 70 durable-evidence refusals, three current-time completions, 54 unique pass-2 dispositions all 21 loop-3, 20 loop-4, 21 loop-5 and 19 loop-6 repair IDs exactly once, plus 64 later-UTC/restart combinations, four observed-after-partial-capture completions, 32 independent continued-authority refusals, 23 signed-request authority refusals, six request transaction/restart boundaries, six terminal-erasure authority refusals, four repair-cleanup boundaries and 141 failed redrives with bounded persisted rows/bytes and exact terminal erasure. Run the protected block with four specification hashes, 21 exact external paths and AD-13 `UNAPPROVED`; integration recomputes only its own content-bound digest after splicing and verification.
 
 ## Disposition register
 
@@ -1577,9 +5051,146 @@ Every routed finding appears once. No row defers a contract decision to Story 6.
 | EC-22 | D12 expands executable mutations to the repaired status, bound, queue, hash, classification, and account invariants. |
 | EC-23 | D8 gives queue corruption a charged inventory record, owner, triggers, reconstruction proof, and deterministic exit. |
 
+### Review-loop-2 repair register
+
+| Finding | Verified repair |
+| --- | --- |
+| BHR2-01 / VGR2-O1 / ECR2-04 | D9 window v2 binds the prior state and caller-stable identity; only the later state binds the window hash. D12 constructs that dependency and rejects a successor-state back-edge. |
+| BHR2-02 / BHR2-03 / VGR2-05 / ECR2-03 | D9 separates caller-stable identity from exact carrier bytes, indexes live/orphan/tombstone evidence by that identity, returns byte-identical retries without new effects, conflicts changed bytes, and retains bounded tombstones until their exact expiry-plus-30-day deletion boundary or erasure. |
+| BHR2-04 | D7 reservation v2 carries tenant, tenant-pool, and deployment predecessor hashes; D12 requires the complete authenticated set. |
+| BHR2-05 / ECR2-02 | D8 queue v4 owns a monotonic durable deployment allocator, lost-ack readback, canonical ordering, and fail-closed u64 exhaustion. |
+| BHR2-06 | D8 fixes the sort tuple as unsigned ticket, raw canonical tenant UTF-8, then raw ScopeOpHash. |
+| BHR2-07 | D7 readiness restores `scopeRetentionCeiling >= 64 MiB`; D12 exercises below/at/above. |
+| BHR2-08 / ECR2-01 | D11.3 gives every replacement-owned revision, head, counter, reservation, wait, policy, quarantine, index, and directory an exact framed address and mutation rule. |
+| BHR2-09 / ECR2-05 | D10 uses at most 17 independently addressed 61-row chunks plus a bounded manifest for all 1,000 maximum-width V1 members; D12 constructs that maximum. |
+| BHR2-10 | D11 held keys bind scope, deployment, tenant presence, component, topic, physical subscription, and carrier hash; D12 proves cross-identity separation. |
+| BHR2-11 | D11 requires tenant exactly for tenant scope and forbids it for deployment scope; D12 rejects both invalid pairings. |
+| BHR2-12 | D11 activates the ordinary retained-object charge before physical-copy acknowledgement and keeps it until logical closure/deletion readback. |
+| BHR2-13 | D11 policy v3 has a framed revision key, CAS head, predecessor chain, and explicit supersession; D12 rejects a revision-2 genesis. |
+| BHR2-14 / ECR2-08 | D3 maps only a nonempty all-class-01 set to exhaustion; unknown/empty/contradictory classes map to evidence conflict even at maximum. |
+| BHR2-15 / VGR2-01 / ECR2-11 | D3 requires both pre-existing outputs and generation receipts. D12 never synthesizes either and exercises each missing/mismatched case. |
+| BHR2-16 / ECR2-12 | D9/D12 partition exact `(position, MessageId, bytes)` tuples and reject a position/MessageId swap. |
+| BHR2-17 | D12's generic record decoder rejects missing, duplicate, reordered, overflowing-length, and trailing-field mutations across resume, legacy optional/signed, and queue byte families. |
+| BHR2-18 | D7/D9 define staged overlap, old/new ownership, every crash side, single release, and no transient ceiling bypass; D12 executes rollback, finalize, refusal, and repeated recovery. |
+| VGR2-02 | D12 exercises conflict/unavailable precedence pairwise over terminal, published, drain, class-01, and class-02 states. |
+| VGR2-03 | The drain-only matrix consumes `resume_publication` output and proves unchanged roster/window, exact source-to-intent resolution, checked limit, and recorded invocation. |
+| VGR2-04 | D12 drives ordinal, window, closed-window count, drain-limit, zero increment, and charge-sum failures through the transition with identical pre/post state. |
+| VGR2-06 | D8/D12 decode the persisted reserved-slot count and authenticated ceiling before any move, rejecting combined overflow. |
+| VGR2-07 | D4/D12 exercise below/at/above ten years and prove slice 4 stays inactive above the maximum. |
+| VGR2-08 | D10/D12 exercise every allowed recovery edge, transport/evidence repair, generation/ordinal advance, and forbidden skip/change. |
+| VGR2-09 | D2/D12 separately cross each post-activation count/readable/accounting hard bound with zero partial dispatch. |
+| ECR2-06 | D10 reuses the immutable chunks/manifest on second exhaustion and advances only recovery generation/ordinal. |
+| ECR2-07 | D11 makes failed redrive return durably to `captured` with incremented count, typed error hash, retained charge/object, and bounded next retry. |
+| ECR2-09 | D7/D12 validate closed account kind on both reservation and refund with unchanged counters. |
+| ECR2-10 | D8 rejects a stable capacity subject already resident or reserved before ticket/slot/charge mutation. |
+
+### Review-loop-3 repair register
+
+These repairs preserve D1–D13, the approved baseline and initial baseline evidence, all historical dispositions, protected child/source hashes, and the open D-SPLIT/in-progress tracker state. The evidence below is executable local codec/transition evidence; provider atomicity and crash guarantees remain future acceptance vectors.
+
+| Finding | Verified repair |
+| --- | --- |
+| BHR3-01 / ECR3-01 | D8 consumes the exact allocated ticket, materializes one row plus its counterpart reservation, and reuses the admission receipt on lost acknowledgement. D12 executes allocation followed by admission, exact retry, duplicate/changed subject, full destination, unavailable charge, invalid state, and allocator exhaustion without new leaked slots or charges. Its allocator source mutation is killed by that actual caller path. |
+| BHR3-02 / ECR3-02 | D9 drain-only retains the exact active claim/hash, window, closed count, roster, accepted and unresolved sets; its resolution binds old source/old limit, constructible successor invocation, and checked new limit. D12 derives and checks the invocation from the unchanged claim and rejects the replacing-claim source mutation. |
+| BHR3-03 | D9 retains the bounded exact signed claim for reconstruction and makes an orphan audit authoritative partial success. D12 models the crash after audit/stage but before state CAS, completes the recorded ordinal/window/charge, returns identical canonical response bytes, fences competing requests, and creates no second audit/invocation. Label-only response and prior-state orphan mutations both fail. |
+| BHR3-04 / ECR3-03 | Every successful live row stores request expiry. Authenticated reconciliation moves it to a tombstone at expiry, sets delete-after exactly 30 days later, and recovers capacity only at deletion. D12 covers before/at/after expiry and deletion, unauthenticated deletion refusal, reconstructed conflicts, and capacity recovery; disabling conversion fails. |
+| BHR3-05 / ECR3-06 | D10 constructs 17 actual bounded chunk records and a real manifest for 1,000 maximum-width members. D12 validates every addressed length/hash/count/endpoint, uncounted chunk-row domain, counted total root, and the independent 179-byte codec fixture; missing/swapped/changed chunks and bad roots fail. The wrong-domain source mutation is rejected. |
+| BHR3-06 / ECR3-04 | D10 recovery checks positive u64 ordinal and contiguous checked generation before mutation. D12 rejects zero/above-max ordinals and generation exhaustion with identical input; directed ordinal and generation overflow mutations fail. |
+| BHR3-07 | Recovery carries owner, expected next generation, exact predecessor, immutable capsule binding, and a recomputed authenticated repair receipt. D12 checks every allowed/forbidden graph edge, all three typed failures, competing/stale owners, skipped generation/state, missing/forged repair, changed capsule, and second exhaustion. Owner-bypass and Boolean-repair source mutations fail. |
+| BHR3-08 | D11 redrive consumes the previous durable state. D12 follows captured -> redriving -> captured, restarts, fails again, and verifies incremented counts, distinct errors, preserved carrier/locator/charge/readback, bounded history/backoff, and terminal close. Resetting the count fails the second transition. |
+| BHR3-09 | Capture reserves ordinary object quota and authenticates exact backend/key/bytes/readback before acknowledgement; failure refunds the attempted object amount while retaining the observed state charge. D12 covers quota refusal, absent/mismatched readback, stale policy, and exact capture retry without a second charge. Skipping quota or readback is rejected. |
+| BHR3-10 / ECR3-05 | D11 policy installation consumes the exact selected predecessor/head and requires its next contiguous revision. D12 rejects genesis-at-2, skipped/stale/competing installation and acknowledgement under a superseded head, while comparing successor bytes/hash exactly. The skipped-revision source mutation fails. |
+| BHR3-11 | D7's real reservation transition authenticates tenant, tenant-pool, and deployment receipts against current ledger state before one batch mutation. D12 independently forges each predecessor and a missing set, proving all counters/charges/reservations unchanged; valid admission mutates all three once, stale evidence fails, and the predecessor-bypass mutation is killed. |
+| BHR3-12 | D12 returns typed signed/numeric/string/optional values, enforces positive bounded legacy count/range agreement, closed enums/identifier bounds and presence relationships, and decodes malformed framing in every record family. Signed-byte decoding and semantic-count-ceiling mutations fail concrete owning assertions. |
+| VGR3-01 | D12 reconstructs caller requests with the same key and changed reason/source, proves equal stable identity but different carrier, and rejects conflict without mutation. Including reason in identity fails. |
+| VGR3-02 | D12 independently varies scope, deployment, two valid tenants, component, topic, physical subscription, and carrier, requiring different exact framed held keys. Omitting tenant fails the cross-tenant assertion. |
+| VGR3-03 | D12 uses two different nonzero policy predecessors and compares the exact successor codec/hash, selected head, and known-answer genesis bytes. Omitting the predecessor from policy bytes fails. |
+
+### Review-loop-4 repair register
+
+The 20 findings below each have one disposition. Seventeen bad-spec repairs and two moot verifier patches are implemented; the stale-CAS claim is rejected against the unchanged D7 contract. Earlier repairs, all 54 historical dispositions, D1–D13, source/baseline pins and external checkpoint checks remain intact. Evidence comes from the actual codec/transition assertions and 38 directed source mutations; it establishes no provider atomicity.
+
+| Finding | Disposition and verified evidence |
+| --- | --- |
+| BHR4-01 | Repaired in D7/D9: rollback requires authenticated absence of both audit and successor. ChargeSwap executes unaudited rollback, audited partial-success retention, final activation/refund, owner conflicts, and repeated recovery with exact usage and ownership. Removing the audit guard fails. |
+| BHR4-02 | Repaired in D12: B-field bounds follow family caps. A canonical 1,000-row queue at 1,078,163 bytes decodes, alongside large hold-index and directory records; cap-plus-one rejects. Restoring a universal 1 MiB guard fails these real records. |
+| BHR4-03 | Rejected: D7 explicitly rejects stale predecessors for a new CAS. The successful first reserve changes all three predecessors; repeating that stale CAS must fail unchanged. The separate authenticated retained-reservation readback succeeds without another reservation or charge. |
+| BHR4-04 | Repaired in D7/D12: the retained reservation binds tenant account, ScopeOpHash, candidate root, request identity, ordered amounts and original predecessors. Same-key changes to each identity component reject unchanged; ignoring identity fails. |
+| BHR4-05 | Repaired in D8/D12: changed tenant/state/ticket materialization refuses without deleting the legitimate pending owner. Only the authenticated preparation owner/predecessor may roll back both slots and its charge, once. The destructive-conflict mutation fails. |
+| BHR4-06 | Repaired in D9/D12: new admission decodes the caller carrier, recomputes its stable identity/hash, and authenticates tenant/handle/source against current evidence. Changed source with current availability conflicts unchanged; removing source comparison fails. |
+| BHR4-07 | Repaired in D9/D12: orphan recovery validates audit/owner and protected prior state, then applies the recorded success delta to the current CAS head. It preserves authenticated intervening retry-index changes. Arbitrary tombstones, corrupt reconciliation/audit receipt, wrong owner, changed source and carrier hold unchanged. |
+| BHR4-08 | Repaired in D11/D12: capture authenticates the pre-existing 32 KiB metadata charge and inventory reservation, with matching scope/account/owner/generation receipts, before object authority and acknowledgement. Missing/stale/wrong-owner metadata or inventory rejects unchanged; bypassing them fails. |
+| BHR4-09 | Repaired in D11/D12: immutable object locator and quota owner derive from the full held key. Two carriers, two tenants and a deployment scope share one ledger/store while preserving the first object and charge; exact retries are once-only. A constant locator fails. |
+| BHR4-10 | Repaired in D11/D12: restarted redriving authenticates the persisted attempt. Verified terminal readback closes it; unknown/unavailable/failed completion schedules captured retry while retaining carrier/count/charge. Corrupt evidence produces a bounded captured evidence hold. The stranded-redrive mutation fails. |
+| BHR4-11 | Repaired in D12: typed semantic validation covers all framed families, including exactly 256 capability shards, directory shard 0..255, generation/predecessor genesis, charge presence/ownership, rows/roots/counts, enums, ranges and scope/locator relationships. Invalid hold-owner and counted-closure fixtures were corrected and independently recomputed; guards were preserved. |
+| BHR4-12 | Repaired in D11/D12: PolicyStore persists create-once revision addresses and one CAS head. Differing successors cannot both install; exact lost acknowledgement is stable and only the selected contiguous revision authorizes capture. Permitting revision overwrite/head bypass fails. |
+| ECR4-01 | Repaired with the family-bound contract in D12: the supported queue above 1 MiB, large index/directory and record-cap rejection assertions execute the real decoder. The universal-field-limit mutation fails. |
+| ECR4-02 | Repaired with D7 reservation ownership: tenant-b cannot reuse tenant-a's key/amounts with fresh predecessor receipts. Complete counters/charges/reservation state remains unchanged, while exact authorized readback succeeds. |
+| ECR4-03 | Repaired in D7/D12: every changed tenant, pool, deployment and unidentified counter preflights checked generation advance on both reserve and refund. Each maximum-generation case rejects byte-identical state; independent reserve/refund overflow mutations fail. |
+| ECR4-04 | Repaired in D9/D12: an actual earlier live success expires through reconciliation while a later success is orphaned. Recovery retains its tombstone, never resurrects the expired live row, completes ordinal/charge exactly and returns the same response once. Replacing current tombstones with prior ones fails. |
+| ECR4-05 | Repaired in D11/D12: four actual immutable carrier objects and their independently owned charges coexist in one persistent model ledger/store. Object keys differ by carrier and full scope identity; the original bytes/charge survive every later capture. |
+| ECR4-06 | Repaired in D12: ordinary identifiers and optional identifiers/backend IDs run below/at/above 1,024 bytes; subject/locator fields separately run below/at/above 4,096. Widening ordinary identifiers to 4,096 fails its actual boundary assertion. The hold entry reserves 8 KiB so a legal 4,096-byte subject remains encodable. |
+| VGR4-01 | Implemented under re-derivation: exact and changed-carrier retries traverse the reconciled tombstone state before deletion, returning expired/conflict with unchanged state. Removing the tombstone branch fails. |
+| VGR4-02 | Implemented under re-derivation: corrupt audit receipt and wrong orphan owner cannot complete success; changed protected state/carrier and forged reconciliation also hold unchanged. Removing orphan authentication fails these owning assertions. |
+
+### Review-loop-5 repair register
+
+Each of the 21 routed findings appears once. The two excluded checkpoint defects remain outside the frozen specification scope; no runtime artifact is changed.
+
+| Finding | Disposition and executed evidence |
+| --- | --- |
+| BHR5-01 | Repaired by actual D9.4 origin/reconstruction/progress byte rows, typed D7 ledger and D11 inventory rows, native provider receipts and bounded addresses. Restart discards poisoned process artifact/charge/progress caches. New artifacts derive from original origin and recorded successor intent, not unrelated planned fixture bytes; actual audit/state owner, carrier, predecessor, ordinal/result and hash links are checked. Seventy missing/changed/stale/unavailable or authenticated-but-contradictory evidence cases hold unchanged. Signed claims keep the independently authenticated A8 head and nonzero predecessor audit distinct from the D9 predecessor; both authority corruptions refuse. Six new families have independently constructed fixed answers and typed malformed/semantic probes. |
+| BHR5-02 | Repaired by the charged original origin and actual bounded progress manifest. Forty-six origin/progress/intent/write/readback boundaries resume original claim bytes from durable evidence alone. Safe unaudited rollback requires exact staged-generation audit/successor absence plus every artifact deletion readback; four cleanup boundaries include deletion before progress CAS. Partial cleanup remains charged and cannot arm work, fixed-expiry retry restages original bytes, and fence/closure/audit forces completion. Completed/rolled-back inventory removal uses checked CAS; exact retries preserve all byte rows and charges. Re-signing, premature-refund, process-cache, unrelated-artifact and expired-successor mutations fail. |
+| BHR5-03 | Repaired by D9.2's framed complete attempt set, contiguous registrations/Unknown observations/results and charged 249,216-row ceiling. The known answer includes all twelve evidence rows; changing/removing earlier evidence fails closure verification even with unchanged final summaries. Closure/history/window/state/audit hashes follow the exact complete root. |
+| BHR5-04 | Repaired by the create-once capture preparation and matching charge-only/object-plus-charge recovery. Both restart boundaries authenticate the observed predecessor, metadata/inventory, object/locator/readback and active charges before completing captured state once. No second quota reservation occurs; changed/unavailable authority holds unchanged. |
+| BHR5-05 | Repaired by the durable required/repaired record and separately indexed repair prerequisite. Three restarted automatic turns and manual repair refuse before authenticated repair; forged repair fails and exact repaired evidence permits one next count/send. The carrier reason remains intact. The current 32 KiB reservation covers both repair and inventory entry, the finite signed request/attempt pair and staged replacement before acknowledgement. |
+| BHR5-06 | Repaired by the ordinal/limit/request-bound invocation identity and exact invocation codec/address. Two actual fresh drain resumes advance ordinal/limit to 2/24 and 3/32 with different invocations, unchanged claim/member bytes and zero command executions. Exact retry preserves one invocation; removing the authorization epoch fails. |
+| BHR5-07 | Repaired by authenticated current-time orphan completion. Twelve cases cross expiry and deletion boundaries with/without preceding reconciliation: completion returns the original response and retains the row only in the correct live/tombstone/deleted state. Retention never extends; the stale-time mutation fails. |
+| BHR5-08 | Repaired by distinct, sorted successful live ordinals in the real resume-state decoder. Different identities at one ordinal reject; disabling that guard fails the actual framed-record assertion. |
+| BHR5-09 | Repaired by per-kind canonical-length and recorded-overhead ceilings in the actual charge decoder. Below/at/above every 449/193/256/1,024 MiB maximum and 1,114,112-byte overhead are tested, with checked amount/generation handling. Independent ceiling/overhead weakening mutations fail. |
+| BHR5-10 | Repaired by typed, owner/generation/kind-bound authenticated presence/absence receipts. Unavailable, boolean, forged, stale-generation and wrong-owner reads preserve stage/old charge/counters. Only proved two-sided absence permits safe cleanup/refund; audited partial success completes. Unavailable-as-absence mutation fails. |
+| ECR5-01 | Repaired by the same current-time completion tests; recovery without earlier reconciliation tombstones/deletes its own expired row immediately, never resurrecting live evidence. |
+| ECR5-02 | Repaired by using the decoded authenticated handle. Initial, live and orphan replies for `hxrsm1-other` match independently written complete canonical JSON bytes; substituting fixture `h` fails. |
+| ECR5-03 | Repaired by both real partial-capture restart cases, exact once-only object/charge completion and conflicting readback refusal; the existing-partial-work rejection mutation fails. |
+| ECR5-04 | Repaired by the ordinary capture transition's maximum-inclusive 193 MiB guard before quota or object mutation. Tests at 193 MiB minus one, exactly 193 MiB, plus one, 256 MiB and plus one prove no ordinary acknowledgement or counter change above its maximum; distinct quarantine/incident paths remain required. |
+| ECR5-05 | Repaired by persisted required repair across restart and repeated turns. The original attempt/carrier/locator/count and active charge must authenticate before repaired readback permits another send; bypassing the prerequisite or repair receipt fails. |
+| ECR5-06 | Repaired by fresh retained authority before every actual send. Changed restored bytes/object, missing object, wrong locator/readback/charge and unavailable store preserve count/state and emit no redrive bytes. Removing this guard fails the owning output assertions. |
+| ECR5-07 | Rejected only under the frozen prohibition on runtime changes. The checkpoint reminder overflow is not repaired or represented as safe by this candidate; its exact external path remains pinned. |
+| ECR5-08 | Rejected only under the frozen prohibition on runtime changes. The checkpoint reminder discovery defect is not repaired or represented as safe; its exact external path remains pinned. |
+| ECR5-09 | Implemented under the full repair: policy redelivery count tests 0/1/63/64/65 against the real decoder. Zero and 65 reject; widening maximum 64 to 65 fails. |
+| VGR5-01 | Implemented under the full repair: independently expected complete canonical bytes pin all five response fields on initial/live/orphan paths. Omitting the handle field or changing it fails separate directed mutations. |
+| VGR5-O1 | Repaired by the same twelve authenticated current-time orphan cases and current-index composition; prior reconciliation time cannot retain an expired live row. |
+
+### Review-loop-6 repair register
+
+Each of the 19 routed findings has one disposition. Executed evidence is the 64 UTC/restart combinations, four partial-capture observation cases, 32 continued-authority refusals, 23 signed-request authority refusals, six request transaction/restart boundaries, six terminal-erasure authority refusals, four cleanup boundaries, bounded 141-failure/restart run and directed owning mutations in D12. Earlier registers and frozen runtime exclusions remain intact. Local models prove neither runtime behavior nor provider atomicity.
+
+| Finding | Disposition and executed evidence |
+| --- | --- |
+| BHR6-01 | Repaired: pending intent fixes original state bytes/UTC; surviving writes acknowledge before current-time CAS. All four successor/finalize boundaries complete at distinct UTCs including 1001/2000, repeat without changes and refuse unavailable/contradictory evidence unchanged. |
+| BHR6-02 | Repaired: acknowledged successor and finalized charge both reconcile/read back current expiry/deletion before completion. Before/at/after both deadlines preserve original claim/audit/response and once-only charge/invocation. |
+| BHR6-03 | Repaired: bounded typed origin authenticates the original observed image and preparation while composing only monotonic provider-authenticated count/revision changes. Charge/object partial storage followed by one/three observations completes once without added charges. |
+| BHR6-04 | Repaired: capture separately reserves/authenticates original and future repair interests. One-slot capacity refuses unacknowledged without leaked charge; two slots succeed, exact retry remains stable, both crash sides retain the same interests. |
+| BHR6-05 | Repaired: fresh metadata/original interest/repair charge/repair interest and preparation checks precede every send. Independent missing/owner/generation mutations refuse unchanged; retained object/readback/locator guards remain exercised. |
+| BHR6-06 | Repaired: required-to-repaired keeps the exact typed prerequisite indexed until record and entry deletion/readback complete. All four cleanup restart sides block send, unavailable cleanup preserves state, second corruption safely reuses the bounded slots. |
+| BHR6-07 | Repaired: one fixed exact signed request, one typed request-bound attempt and one replacement deletion receipt for each slot bound actual persistence. 141 genuine failed redrives with persisted-only ledger/held restarts retain request payload at most 3 KiB and attempt at most 8 KiB with identical justified charges; 23 request-authority refusals, six staged/committed transaction boundaries, stale-count fencing, corruption/repair/cleanup, terminal deletion and exact refund execute. |
+| BHR6-08 | Repaired: window tag 08 uses exact sorted unresolved tuple root. Independent decoded assertions cover accepted plus two unresolved and a singleton, with partition-corruption refusals retained. All affected known-answer dependencies were recomputed. |
+| BHR6-09 | Implemented: ordinary and persisted invocation construction sort unsigned positions. Reversed equivalent partitions admit the same root/identity and keep unchanged drain-only claim bytes; independent incoming-order mutations fail. |
+| BHR6-10 | Repaired: summary construction selects greatest local ordinal's last definitive result. A decoder-valid history with differing local maxima and Unknown counts completes actual closure/history/window/audit/state; historical-result-as-summary mutation fails. |
+| ECR6-01 | Repaired by original-intent acknowledgement and the same later-UTC cases; progress/receipt links remain authentic across repeat restart, with unavailable/contradictory rows unchanged. |
+| ECR6-02 | Repaired by the complete multi-local-attempt construction and exact [2,3,4] final summary ordinals. Missing earlier authority refuses while the complete set retains every registration/Unknown/result. |
+| ECR6-03 | Repaired by both partial-capture storage sides after normal monotonic observations, persisted-only restoration and exact object/charge completion. Protected-field/observation-receipt changes refuse unchanged. |
+| ECR6-04 | Implemented: released generation retains the original transfer owner. Staged-to-released-to-restaged/active is executable; owner-removal mutation fails, with existing owner/generation/predecessor and counter checks retained. |
+| ECR6-05 | Repaired by persistent repair record/entry cleanup and deletion receipts before the next count/send, including restart at every cleanup phase and a second corruption. |
+| ECR6-06 | Repaired by fixed addressed authority, authenticated reclamation and the 141-failure bounded row/byte/charge assertions; reclamation/log mutations fail. |
+| VGR6-01 | Implemented: missing/changed preparation on either side and origin loss exercise the actual send gate. Removing the ledger comparison fails the owning refusal assertions. |
+| VGR6-O1 | Implemented: preparation is a required typed authority, including the both-missing case. Removing that obligation fails; nullable equality alone cannot admit send/count. |
+| VGR6-O2 | Repaired: the actual persisted addressed rows and deletion receipts are bounded independently of the 64-entry error tuple, including >130 failures/restarts and terminal erasure. |
+
 ## Protected-path and source-integrity verification
 
-This block proves that the unapproved parent/children and protected implementation paths remain unchanged while this story edits only its candidate and bookkeeping artifacts.
+This block proves that the unapproved parent/children remain unchanged and that this story edits only its candidate and bookkeeping artifacts, with unrelated concurrent changes pinned to the checkpoint below.
+The approved review baseline stays `01498ac7`. The user approved the external-workspace checkpoint update to `6dededdecd62dd6dc6d1f15810108d860ec70c8f` on 2026-10-01. Concurrent commits changed 15 reminder paths and two submodule pointers while capturing part of this candidate. Those exact 17 committed paths remain pinned below. The user separately approved the exact current Builds, FrontComposer and Tenants revisions, adding FrontComposer for 18 external paths, then approved only the exact staged `.gitmodules` blob `c62b48798894bb3576f02fdf4ebb8c552756e35b` and clean root gitlinks McpCli `29cf33a8927b12ef1232f25663d9daf5c1ad8369` and Platform `7c2f0f89f29c79f5d7ab4b731155e2cb07fc690f`. This is 21 distinct external paths, not a blanket exception. Both index and worktree must authenticate the full `.gitmodules` blob; the two added index entries must be mode 160000 at their exact pins, with exact root registrations/URLs and clean worktree HEADs. Only the five pinned gitlinks and pinned registration image may differ from the committed checkpoint. Every other committed external file remains byte-identical; any new path/content/pin/worktree drift fails. No external path, index, dependency or Git history is modified; the four specification hashes and approval checks remain pinned.
 
 ```bash
 python3 - <<'PY'
@@ -1608,13 +5219,85 @@ allowed = {
     '_bmad-output/implementation-artifacts/deferred-work.md',
     '_bmad-output/implementation-artifacts/sprint-status.yaml',
 }
-changed = set(subprocess.check_output(['git','diff','--name-only','6a2f25e39586692b54b655d3e6e8a5f6fa4d317e']).decode().splitlines())
+changed = set(subprocess.check_output(['git','diff','--name-only','01498ac721db7c44f18fcf9591ffbbf30ba245e2']).decode().splitlines())
 untracked = {line[3:] for line in subprocess.check_output(['git','status','--porcelain']).decode().splitlines() if line.startswith('?? ')}
-assert changed | untracked <= allowed, sorted((changed | untracked) - allowed)
-print(f'protected-path verifier: {len(pins)} hashes, AD-13 UNAPPROVED, {len(changed | untracked)} allowed paths')
+checkpoint = '6dededdecd62dd6dc6d1f15810108d860ec70c8f'
+checkpoint_paths = {
+    'docs/guides/typed-reminders.md',
+    'references/Hexalith.Builds',
+    'references/Hexalith.Tenants',
+    'src/Hexalith.EventStore.DomainService/EventStoreReminderServiceCollectionExtensions.cs',
+    'src/Hexalith.EventStore.DomainService/ReminderActor.cs',
+    'src/Hexalith.EventStore.DomainService/ReminderCoordinator.cs',
+    'src/Hexalith.EventStore.DomainService/ReminderIntentIndex.cs',
+    'src/Hexalith.EventStore.DomainService/ReminderLog.cs',
+    'src/Hexalith.EventStore.DomainService/ReminderReconciler.cs',
+    'tests/Hexalith.EventStore.DomainService.Tests/EventStoreReminderCompositionTests.cs',
+    'tests/Hexalith.EventStore.DomainService.Tests/Fixtures/DispositionFailingReadModelStore.cs',
+    'tests/Hexalith.EventStore.DomainService.Tests/Fixtures/FakeReminderIntentSource.cs',
+    'tests/Hexalith.EventStore.DomainService.Tests/Fixtures/FakeReminderScheduler.cs',
+    'tests/Hexalith.EventStore.DomainService.Tests/Fixtures/ReminderTestHarness.cs',
+    'tests/Hexalith.EventStore.DomainService.Tests/ReminderCallbackAdmissionTests.cs',
+    'tests/Hexalith.EventStore.DomainService.Tests/ReminderCoordinatorTests.cs',
+    'tests/Hexalith.EventStore.DomainService.Tests/ReminderReconcilerTests.cs',
+}
+submodule_pins = {
+    'references/Hexalith.Builds': '21ce044ab465ccb2adab58b3d66e394ffbecf3c2',
+    'references/Hexalith.FrontComposer': 'e01aea27df39fd22aa3677900290027c603af543',
+    'references/Hexalith.McpCli': '29cf33a8927b12ef1232f25663d9daf5c1ad8369',
+    'references/Hexalith.Platform': '7c2f0f89f29c79f5d7ab4b731155e2cb07fc690f',
+    'references/Hexalith.Tenants': 'a4a1ce13873128607e4e43abd3f038674e9dabf0',
+}
+registration_paths = {'.gitmodules'}
+gitmodules_blob = 'c62b48798894bb3576f02fdf4ebb8c552756e35b'
+assert subprocess.check_output(['git','ls-files','--stage','--','.gitmodules']).decode().split() == ['100644',gitmodules_blob,'0','.gitmodules']
+assert subprocess.check_output(['git','hash-object','--no-filters','.gitmodules']).decode().strip() == gitmodules_blob
+assert (root/'.gitmodules').read_bytes() == subprocess.check_output(['git','show',gitmodules_blob])
+added_registrations = {
+    'references/Hexalith.McpCli': ('Hexalith.McpCli','https://github.com/Hexalith/Hexalith.McpCli.git'),
+    'references/Hexalith.Platform': ('Hexalith.Platform','https://github.com/Hexalith/Hexalith.Platform.git'),
+}
+for name,(section,url) in added_registrations.items():
+    assert subprocess.check_output(['git','config','--file','.gitmodules','--get',f'submodule.{section}.path']).decode().strip() == name
+    assert subprocess.check_output(['git','config','--file','.gitmodules','--get',f'submodule.{section}.url']).decode().strip() == url
+    assert subprocess.check_output(['git','ls-files','--stage','--',name]).decode().split() == ['160000',submodule_pins[name],'0',name]
+declared_rows = subprocess.check_output([
+    'git','config','--file','.gitmodules','--get-regexp',r'^submodule\..*\.path$'
+]).decode().splitlines()
+declared_paths = {row.split(None,1)[1] for row in declared_rows}
+assert set(submodule_pins) <= declared_paths
+external_paths = checkpoint_paths | set(submodule_pins) | registration_paths
+assert len(external_paths) == 21
+subprocess.check_call(['git','merge-base','--is-ancestor',
+                       '01498ac721db7c44f18fcf9591ffbbf30ba245e2',checkpoint])
+checkpoint_changes = set(subprocess.check_output([
+    'git','diff','--name-only','01498ac721db7c44f18fcf9591ffbbf30ba245e2',checkpoint
+]).decode().splitlines())
+assert checkpoint_changes - allowed == checkpoint_paths
+for name in sorted(external_paths):
+    if name.startswith('references/'):
+        if name in added_registrations:
+            entry = ['160000','commit',submodule_pins[name]]
+        else:
+            entry = subprocess.check_output(['git','ls-tree',checkpoint,'--',name]).decode().split()
+            assert entry[:2] == ['160000','commit'], name
+        actual = subprocess.check_output(['git','-C',name,'rev-parse','HEAD']).decode().strip()
+        expected = submodule_pins.get(name,entry[2])
+        assert actual == expected, (name,actual,expected)
+        assert not subprocess.check_output(['git','-C',name,'status','--porcelain']), name
+    elif name not in registration_paths:
+        expected = subprocess.check_output(['git','show',f'{checkpoint}:{name}'])
+        assert (root/name).read_bytes() == expected, name
+external_drift = set(subprocess.check_output([
+    'git','diff','--name-only',checkpoint,'--',*sorted(external_paths - set(submodule_pins) - registration_paths)
+]).decode().splitlines())
+assert not external_drift, sorted(external_drift)
+story_changed = changed - external_paths
+assert story_changed | untracked <= allowed, sorted((story_changed | untracked) - allowed)
+print(f'protected-path verifier: {len(pins)} hashes, AD-13 UNAPPROVED, {len(story_changed | untracked)} allowed paths, {len(external_paths)} pinned external paths')
 PY
 ```
 
 ## Verification expectations
 
-Run all three fenced `bash` blocks verbatim. Expected output is 30 codec answers, 30 byte mutations rejected, and six framed keys; 14 status cases, 4 approved matrix rows, 27 lifecycle mutants, and 54 dispositions passed; then four protected hashes with AD-13 still `UNAPPROVED`. Run `python3 scripts/check-deferred-work.py` and `git diff --check`. Story 6.5 integration must rerun these blocks after splicing and before recomputing its own §12 digest.
+Run all three fenced `bash` blocks verbatim. Expect 30 original/twelve supplementary answers, 42 digest probes, six keys, 200 malformed and 70 semantic rejections; 14 statuses, four approved matrix rows, 27 invariants, 87 rejected source mutations, 54 dispositions, ten actual-transition codec matches/50 repeated malformed rejections, 46 persisted-only restart boundaries, four cleanup boundaries, 70 durable-evidence refusals, three current-time completions and all 21 loop-3, 20 loop-4, 21 loop-5 and 19 loop-6 repair IDs exactly once. Iteration 6 additionally executes 64 later-UTC/restart combinations, four partial-capture completions after legitimate observations, 32 continued-authority refusals, 23 signed-request authority refusals, six request transaction/restart boundaries, six terminal-erasure authority refusals, four repair-cleanup restart boundaries, 141 genuine failed redrives with one bounded signed request and one typed attempt, bounded deletion receipts, and terminal erasure, plus both unaudited capture-refund sides. The protected gate requires four exact hashes, the authorized 21 external paths and AD-13 `UNAPPROVED`; any new concurrent external path remains a failed gate until explicit checkpoint authority. A separate Node encoder at `/tmp/verify-6-5d-loop6-independent.mjs`, fed by `python3 /tmp/export-6-5d-loop5-codec-fixtures.py`, passes all 42 lengths/hashes, six keys, affected semantic dependency graphs and ten independently constructed durable answers (nine added families plus the existing request family). These results prove local bytes and transitions, not provider atomicity. Run `python3 scripts/check-deferred-work.py`, frozen-intent comparison and `git diff --check`. Integration reruns the blocks before recomputing its content-bound digest. D-SPLIT stays open, sprint stays in-progress and iteration stays 6 for parent audit and all three reviews.
diff --git a/docs/guides/typed-reminders.md b/docs/guides/typed-reminders.md
index 4fe80134dcf4d1b4759e45bd864977fe014567c1..846850e3f82a348e006e94274b9f692b5fe2ee51 100644
--- a/docs/guides/typed-reminders.md
+++ b/docs/guides/typed-reminders.md
@@ -130,8 +130,10 @@ Callback admission runs in this order:
    needs a configured purpose. Without one, the callback is `Denied`, the work
    is retained, and a backoff reminder is re-armed.
 4. **Currency.** The intent source must still report the exact witness. A
-   superseded witness is audited as `Stale`. Nothing is submitted, the reminder
-   is cancelled, and the item re-converges with the intents just re-folded. If
+   superseded witness is audited as `Stale`. Nothing is submitted for that
+   witness. The item re-converges with the intents just re-folded, indexing and
+   persisting replacements while the stale witness and its Scheduler reminder
+   are still held. Cancellation then releases the stale witness. If
    more than one current intent carries the name with different evidence, or
    another current intent shares the effect identity, the witness is
    quarantined before any submission.
@@ -149,17 +151,23 @@ A durable receipt (`Success`, `Rejection`, or `NoOp`) releases the witness in
 this order:
 
 1. Write the `Submitted` audit record with the effect identifier.
-2. Delete the pending state.
-3. Cancel the Scheduler reminder.
-4. Remove the index entry.
-
-If the audit write fails, the witness stays. A later retry replays the same
-receipt. An exception, a mismatched receipt, or a missing submitter or
+2. Cancel the Scheduler reminder successfully.
+3. Delete the pending witness.
+4. Re-fold the stream before removing the index entry. Retain discovery when
+   the stream still reports intents or the fold is unavailable.
+
+If the audit write or Scheduler cancellation fails, the witness stays and
+counts as unresolved. A later retry replays the same receipt. An exception,
+a mismatched receipt, or a missing submitter or
 delegation keeps the witness as `Retrying`. It re-arms a backoff reminder that
 doubles from `RetryInitialDelay` up to `RetryMaxDelay`, and the work counts as
 unresolved. A callback never reports failure by throwing. Resubmitting the same
 witness after a restart returns the same effect identifier with `Replayed = true`.
 
+If the final stream fold returns null or throws after a receipt or stale
+disposition is settled, the discovery candidate stays and counts as unresolved.
+Readiness remains `Degraded` until a later convergence can re-fold the stream.
+
 Timing belongs to the Scheduler. The callback authenticates origin and witness;
 it does not re-check the due instant.
 
@@ -267,8 +275,8 @@ pass after a restart.
 `Retrying` witnesses carry their last reason code. The codes are
 `submission-uncertain`, `receipt-mismatch`, `submitter-unavailable`,
 `delegation-unavailable`, `delegation-failed`, `purpose-unconfigured`,
-`workload-unconfigured`, `source-unavailable`, and `audit-unavailable`. Fix the
-cause; the next firing or pass resubmits under the same effect identifier. A
+`workload-unconfigured`, `source-unavailable`, `audit-unavailable`, and
+`cancel-failed`. Fix the cause; the next firing or pass resubmits under the same effect identifier. A
 target that already holds the receipt replays it, so retries never create a
 second logical effect.
 
@@ -278,7 +286,9 @@ Quarantine reason codes are `tuple-mismatch`, `witness-collision`,
 `effect-collision`, `actor-collision`, `translation-failed`, `translation-invalid`,
 `effect-identity-invalid`, and the malformed-intent codes `intent-missing`,
 `target-mismatch`, `kind-unsupported`, `due-not-utc`, `revision-invalid`,
-`source-sequence-invalid`, `payload-invalid`, and `identity-invalid`.
+`source-sequence-invalid`, `payload-invalid`, and `identity-invalid`. Malformed
+or duplicate restored state uses `stored-entry-invalid`,
+`stored-entry-duplicate`, or `stored-quarantine-invalid`.
 
 1. Find the item from the `200207` log, which carries the `wra-` actor
    identifier and the subject. The subject is the reminder name or a 52-character
diff --git a/references/Hexalith.Builds b/references/Hexalith.Builds
index fdebf0fa7505ebc18c500a4da88eeee05df1203a..21ce044ab465ccb2adab58b3d66e394ffbecf3c2 160000
--- a/references/Hexalith.Builds
+++ b/references/Hexalith.Builds
@@ -1 +1 @@
-Subproject commit fdebf0fa7505ebc18c500a4da88eeee05df1203a
+Subproject commit 21ce044ab465ccb2adab58b3d66e394ffbecf3c2
diff --git a/references/Hexalith.FrontComposer b/references/Hexalith.FrontComposer
index d679ef5ce1beea22ed0adbcae709d8bda01af85b..e01aea27df39fd22aa3677900290027c603af543 160000
--- a/references/Hexalith.FrontComposer
+++ b/references/Hexalith.FrontComposer
@@ -1 +1 @@
-Subproject commit d679ef5ce1beea22ed0adbcae709d8bda01af85b
+Subproject commit e01aea27df39fd22aa3677900290027c603af543
diff --git a/references/Hexalith.McpCli b/references/Hexalith.McpCli
new file mode 160000
index 0000000000000000000000000000000000000000..29cf33a8927b12ef1232f25663d9daf5c1ad8369
--- /dev/null
+++ b/references/Hexalith.McpCli
@@ -0,0 +1 @@
+Subproject commit 29cf33a8927b12ef1232f25663d9daf5c1ad8369
diff --git a/references/Hexalith.Platform b/references/Hexalith.Platform
new file mode 160000
index 0000000000000000000000000000000000000000..7c2f0f89f29c79f5d7ab4b731155e2cb07fc690f
--- /dev/null
+++ b/references/Hexalith.Platform
@@ -0,0 +1 @@
+Subproject commit 7c2f0f89f29c79f5d7ab4b731155e2cb07fc690f
diff --git a/references/Hexalith.Tenants b/references/Hexalith.Tenants
index d728ff46ca7df5f3c64f90ea057d7d62e7d8a652..a4a1ce13873128607e4e43abd3f038674e9dabf0 160000
--- a/references/Hexalith.Tenants
+++ b/references/Hexalith.Tenants
@@ -1 +1 @@
-Subproject commit d728ff46ca7df5f3c64f90ea057d7d62e7d8a652
+Subproject commit a4a1ce13873128607e4e43abd3f038674e9dabf0
diff --git a/src/Hexalith.EventStore.DomainService/EventStoreReminderServiceCollectionExtensions.cs b/src/Hexalith.EventStore.DomainService/EventStoreReminderServiceCollectionExtensions.cs
index aaf99b9ef5a7de11944c5e89be872f5c3b236d7c..e06f2fd0d5bdd8112897f807e386a0f6675d00ce 100644
--- a/src/Hexalith.EventStore.DomainService/EventStoreReminderServiceCollectionExtensions.cs
+++ b/src/Hexalith.EventStore.DomainService/EventStoreReminderServiceCollectionExtensions.cs
@@ -64,11 +64,10 @@ public static class EventStoreReminderServiceCollectionExtensions
         _ = services.AddSingleton<IPostConfigureOptions<EventStoreReminderOptions>>(serviceProvider =>
             new PostConfigureOptions<EventStoreReminderOptions>(Options.DefaultName, options =>
             {
-                if (string.IsNullOrWhiteSpace(options.Workload))
-                {
-                    options.Workload = Environment.GetEnvironmentVariable("DAPR_APP_ID")
-                        ?? serviceProvider.GetService<IHostEnvironment>()?.ApplicationName;
-                }
+                options.Workload = ResolveWorkload(
+                    options.Workload,
+                    Environment.GetEnvironmentVariable("DAPR_APP_ID"),
+                    serviceProvider.GetService<IHostEnvironment>()?.ApplicationName);
             }));
 
         _ = services.AddEventStoreReadModelStore();
@@ -118,4 +117,18 @@ public static class EventStoreReminderServiceCollectionExtensions
 
         return services;
     }
+
+    /// <summary>Resolves the configured workload, ignoring blank environment values before using the host name.</summary>
+    /// <param name="configuredWorkload">The explicitly configured workload.</param>
+    /// <param name="daprApplicationId">The <c>DAPR_APP_ID</c> environment value.</param>
+    /// <param name="applicationName">The host application name.</param>
+    /// <returns>The first non-blank workload candidate, or <see langword="null"/>.</returns>
+    internal static string? ResolveWorkload(string? configuredWorkload, string? daprApplicationId, string? applicationName)
+        => !string.IsNullOrWhiteSpace(configuredWorkload)
+            ? configuredWorkload
+            : !string.IsNullOrWhiteSpace(daprApplicationId)
+                ? daprApplicationId
+                : !string.IsNullOrWhiteSpace(applicationName)
+                    ? applicationName
+                    : null;
 }
diff --git a/src/Hexalith.EventStore.DomainService/ReminderActor.cs b/src/Hexalith.EventStore.DomainService/ReminderActor.cs
index 1244809d0ae27d7babc49938570d9b577c528fb7..b420f4f145c23cb7505f1b4541ba50c446c64a74 100644
--- a/src/Hexalith.EventStore.DomainService/ReminderActor.cs
+++ b/src/Hexalith.EventStore.DomainService/ReminderActor.cs
@@ -28,7 +28,10 @@ public sealed class ReminderActor : Actor, IReminderActor, IRemindable, IReminde
 
     /// <inheritdoc/>
     public Task<ReminderConvergenceResult> ConvergeAsync(ReminderTarget target)
-        => _coordinator.ConvergeAsync(Id.GetId(), target, this, CancellationToken.None);
+    {
+        ArgumentNullException.ThrowIfNull(target);
+        return _coordinator.ConvergeAsync(Id.GetId(), target, this, CancellationToken.None);
+    }
 
     /// <inheritdoc/>
     public async Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
diff --git a/src/Hexalith.EventStore.DomainService/ReminderCoordinator.cs b/src/Hexalith.EventStore.DomainService/ReminderCoordinator.cs
index 257a07c77dc65f7f3f9d35503bcf7468263963da..054352407490b6281a37d5754e814167e90c2f5a 100644
--- a/src/Hexalith.EventStore.DomainService/ReminderCoordinator.cs
+++ b/src/Hexalith.EventStore.DomainService/ReminderCoordinator.cs
@@ -27,9 +27,10 @@ namespace Hexalith.EventStore.DomainService;
 /// witness (otherwise an audited no-op cancels the reminder).
 /// </para>
 /// <para>
-/// A durable target receipt releases pending state after its audit record is written; the scheduler
-/// reminder is cancelled next and the index entry goes last. An uncertain outcome keeps the state, re-arms a
-/// backoff reminder, and counts as unresolved. A callback never reports failure by throwing.
+/// A durable target receipt releases pending state after its audit record is written and the scheduler
+/// reminder is successfully cancelled. The index entry goes last only after a fresh fold reports no current intents.
+/// An uncertain outcome keeps the state, re-arms a backoff reminder, and counts as unresolved. A callback
+/// never reports failure by throwing.
 /// </para>
 /// </remarks>
 internal sealed class ReminderCoordinator
@@ -90,6 +91,11 @@ internal sealed class ReminderCoordinator
     public static string ComputeActorId(ReminderTarget target)
     {
         ArgumentNullException.ThrowIfNull(target);
+        if (string.IsNullOrWhiteSpace(target.Domain))
+        {
+            throw new ArgumentException("The target domain is required.", nameof(target));
+        }
+
         var identity = new AggregateIdentity(target.Tenant, target.Domain, target.Aggregate);
         if (!string.Equals(identity.TenantId, target.Tenant, StringComparison.Ordinal)
             || !string.Equals(identity.Domain, target.Domain, StringComparison.Ordinal))
@@ -116,14 +122,20 @@ internal sealed class ReminderCoordinator
         CancellationToken cancellationToken)
     {
         ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
+        ArgumentNullException.ThrowIfNull(target);
         ArgumentNullException.ThrowIfNull(scheduler);
+        if (string.IsNullOrWhiteSpace(target.Domain))
+        {
+            throw new ArgumentException("The target domain is required.", nameof(target));
+        }
+
         if (!string.Equals(ComputeActorId(target), actorId, StringComparison.Ordinal))
         {
             throw new ArgumentException("The target does not derive this reminder actor.", nameof(target));
         }
 
         string key = ReminderStateKeys.Item(_options.ActorTypeName, actorId);
-        (ReminderItemState? stored, string? etag) = await LoadAsync(key, cancellationToken).ConfigureAwait(false);
+        (ReminderItemState? stored, string? etag) = await LoadAsync(key, actorId, cancellationToken).ConfigureAwait(false);
         try
         {
             DateTimeOffset now = _time.GetUtcNow();
@@ -133,24 +145,35 @@ internal sealed class ReminderCoordinator
                     .ConfigureAwait(false);
             }
 
-            IReadOnlyList<ReminderIntent> intents = await _source
+            IReadOnlyList<ReminderIntent>? intents = await _source
                 .GetCurrentIntentsAsync(target, cancellationToken)
-                .ConfigureAwait(false) ?? [];
+                .ConfigureAwait(false);
+            if (intents is null)
+            {
+                // A null fold is not an empty stream. Treating it as empty would cancel stored reminders.
+                throw new ReminderFailClosedException("source-unavailable");
+            }
+
             return await ConvergeCoreAsync(actorId, key, stored, etag, target, intents, now, scheduler, cancellationToken)
                 .ConfigureAwait(false);
         }
         catch (ReminderFailClosedException exception)
         {
             ReminderLog.FailedClosed(_logger, actorId, exception.ReasonCode);
-            if (stored is null)
+            (ReminderItemState? current, _) = await LoadAsync(key, actorId, cancellationToken).ConfigureAwait(false);
+            if (current is null || !HasWork(current))
             {
                 // Nothing durable holds this item yet, so no index entry or pass can rediscover it: surface the
                 // failure so the caller's at-least-once delivery retries the convergence.
                 throw;
             }
 
-            int unresolved = Math.Max(stored.Entries.Count(static e => e.Status != ReminderEntryStatus.Quarantined), 1);
-            int quarantined = CountQuarantined(stored);
+            // A failure after an earlier successful state write must be judged from the durable state now, not
+            // the snapshot loaded at the start of this turn. Re-ensure discovery before acknowledging it.
+            var currentTarget = new ReminderTarget(current.Tenant, current.Domain, current.Aggregate);
+            await _index.EnsureCandidateAsync(currentTarget, actorId, cancellationToken).ConfigureAwait(false);
+            int unresolved = Math.Max(current.Entries.Count(static e => e.Status != ReminderEntryStatus.Quarantined), 1);
+            int quarantined = CountQuarantined(current);
             _status.RecordItem(actorId, unresolved, quarantined);
             return new ReminderConvergenceResult(0, 0, 0, unresolved, quarantined);
         }
@@ -368,6 +391,124 @@ internal sealed class ReminderCoordinator
         return EffectIdentityCodec.RenderDigest(hash.GetHashAndReset());
     }
 
+    private static string EvidenceDigest(ReminderEntry? entry, int ordinal)
+    {
+        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
+        AppendText(hash, "persisted-reminder-entry-v1");
+        AppendLong(hash, ordinal);
+        if (entry is not null)
+        {
+            AppendText(hash, entry.ReminderName);
+            AppendText(hash, entry.ScheduleToken);
+            AppendText(hash, entry.Kind);
+            AppendLong(hash, entry.DueUtc.UtcTicks);
+            AppendLong(hash, entry.DueUtc.Offset.Ticks);
+            AppendLong(hash, entry.ScheduleRevision);
+            AppendText(hash, entry.SourceDomain);
+            AppendText(hash, entry.SourceAggregate);
+            AppendLong(hash, entry.SourceSequence);
+            AppendText(hash, entry.PayloadType);
+            AppendText(hash, entry.PayloadDigest);
+            AppendLong(hash, (int)entry.Status);
+            AppendLong(hash, entry.Attempts);
+            AppendText(hash, entry.LastReasonCode);
+            AppendLong(hash, entry.UpdatedAt.UtcTicks);
+            AppendLong(hash, entry.UpdatedAt.Offset.Ticks);
+        }
+
+        return EffectIdentityCodec.RenderDigest(hash.GetHashAndReset());
+    }
+
+    private static string EvidenceDigest(ReminderQuarantineRecord? record, int ordinal)
+    {
+        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
+        AppendText(hash, "persisted-reminder-quarantine-v1");
+        AppendLong(hash, ordinal);
+        if (record is not null)
+        {
+            AppendText(hash, record.EvidenceDigest);
+            AppendText(hash, record.ReasonCode);
+            AppendText(hash, record.ReminderName);
+            AppendLong(hash, record.RecordedAt.UtcTicks);
+            AppendLong(hash, record.RecordedAt.Offset.Ticks);
+        }
+
+        return EffectIdentityCodec.RenderDigest(hash.GetHashAndReset());
+    }
+
+    private static bool IsDigest(string? value)
+    {
+        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
+        return value is { Length: 52 } && value.All(character => alphabet.Contains(character, StringComparison.Ordinal));
+    }
+
+    private static bool TryValidatePersistedEntry(
+        ReminderItemState state,
+        string actorId,
+        ReminderEntry? entry,
+        out string effectId)
+    {
+        effectId = string.Empty;
+        if (entry is null
+            || !Enum.IsDefined(entry.Status)
+            || entry.DueUtc.Offset != TimeSpan.Zero
+            || entry.UpdatedAt.Offset != TimeSpan.Zero
+            || entry.ScheduleRevision < 0
+            || entry.SourceSequence <= 0
+            || entry.Attempts < 0
+            || string.IsNullOrWhiteSpace(entry.SourceDomain)
+            || string.IsNullOrWhiteSpace(entry.SourceAggregate)
+            || string.IsNullOrWhiteSpace(entry.PayloadType)
+            || !IsDigest(entry.PayloadDigest)
+            || !ReminderIdentityCodec.TryParseReminderName(entry.ReminderName, out string parsedKind, out string parsedToken)
+            || !string.Equals(parsedKind, entry.Kind, StringComparison.Ordinal)
+            || !string.Equals(parsedToken, entry.ScheduleToken, StringComparison.Ordinal)
+            || !ReminderIdentityCodec.Rederives(
+                state.Tenant,
+                state.Aggregate,
+                entry.Kind,
+                entry.DueUtc,
+                entry.ScheduleRevision,
+                actorId,
+                entry.ReminderName))
+        {
+            return false;
+        }
+
+        try
+        {
+            if (string.IsNullOrWhiteSpace(state.Domain))
+            {
+                // Effect identity cannot be derived. Leave the entry in place so the callback
+                // quarantines it as domain-invalid instead of repairing it into a nameless drop.
+                effectId = string.Empty;
+                return true;
+            }
+
+            effectId = EffectIdentityCodec.ComputeEffectId(new EffectIdentity(
+                state.Tenant,
+                entry.SourceDomain,
+                entry.SourceAggregate,
+                entry.SourceSequence,
+                entry.Kind,
+                state.Domain,
+                state.Aggregate,
+                EffectKindCatalog.PrimaryTargetOrdinal));
+            return true;
+        }
+        catch (ArgumentException)
+        {
+            return false;
+        }
+    }
+
+    private static bool IsValidPersistedQuarantine(ReminderQuarantineRecord? record)
+        => record is not null
+            && IsDigest(record.EvidenceDigest)
+            && !string.IsNullOrWhiteSpace(record.ReasonCode)
+            && record.RecordedAt.Offset == TimeSpan.Zero
+            && (record.ReminderName is null || ReminderIdentityCodec.TryParseReminderName(record.ReminderName, out _, out _));
+
     private static void AppendText(IncrementalHash hash, string? value)
         => AppendBytes(hash, value is null ? null : Encoding.UTF8.GetBytes(value));
 
@@ -417,13 +558,13 @@ internal sealed class ReminderCoordinator
         IReadOnlyList<ReminderIntent> intents,
         DateTimeOffset now,
         IReminderScheduler scheduler,
-        CancellationToken cancellationToken)
+        CancellationToken cancellationToken,
+        IReadOnlySet<string>? retainedReminderNames = null)
     {
         ReminderItemState state = stored ?? new ReminderItemState(target.Tenant, target.Domain, target.Aggregate, 0, [], []);
         var quarantine = new List<ReminderQuarantineRecord>(state.Quarantine);
         var desired = new Dictionary<string, (ReminderIntent Intent, ReminderEntry Witness)>(StringComparer.Ordinal);
         var collided = new Dictionary<string, string>(StringComparer.Ordinal);
-        var auditQuarantine = new List<(string Subject, string ReasonCode)>();
 
         // Classify the stream's intents. Malformed evidence is never dropped: it is quarantined by digest.
         foreach (ReminderIntent? intent in intents)
@@ -432,10 +573,7 @@ internal sealed class ReminderCoordinator
             if (reason is not null)
             {
                 string digest = EvidenceDigest(intent);
-                if (AddQuarantine(quarantine, digest, reason, null, now))
-                {
-                    auditQuarantine.Add((digest, reason));
-                }
+                _ = AddQuarantine(quarantine, digest, reason, null, now);
 
                 continue;
             }
@@ -447,10 +585,7 @@ internal sealed class ReminderCoordinator
                 {
                     collided[witness.ReminderName] = "witness-collision";
                     string digest = EvidenceDigest(intent);
-                    if (AddQuarantine(quarantine, digest, "witness-collision", witness.ReminderName, now))
-                    {
-                        auditQuarantine.Add((digest, "witness-collision"));
-                    }
+                    _ = AddQuarantine(quarantine, digest, "witness-collision", witness.ReminderName, now);
                 }
 
                 continue;
@@ -476,18 +611,26 @@ internal sealed class ReminderCoordinator
         foreach ((string name, string reasonCode) in collided)
         {
             string digest = EvidenceDigest(desired[name].Intent);
-            if (AddQuarantine(quarantine, digest, reasonCode, name, now))
-            {
-                auditQuarantine.Add((digest, reasonCode));
-            }
+            _ = AddQuarantine(quarantine, digest, reasonCode, name, now);
 
             _ = desired.Remove(name);
         }
 
+        // A persisted entry that normalized into quarantine must not be recreated from the stream in this
+        // turn. Operator-visible evidence remains authoritative until it is explicitly disposed of.
+        foreach (ReminderQuarantineRecord record in quarantine)
+        {
+            if (record.ReminderName is not null
+                && record.ReasonCode is "stored-entry-invalid" or "stored-entry-duplicate")
+            {
+                _ = desired.Remove(record.ReminderName);
+            }
+        }
+
         // Merge with persisted witnesses.
         var entries = new List<ReminderEntry>();
         var obsolete = new List<ReminderEntry>();
-        var newlyQuarantined = new List<ReminderEntry>();
+        var newlyQuarantined = new List<(ReminderEntry Original, ReminderEntry Quarantined)>();
         var storedNames = new HashSet<string>(StringComparer.Ordinal);
         foreach (ReminderEntry entry in state.Entries)
         {
@@ -500,23 +643,27 @@ internal sealed class ReminderCoordinator
             else if (collided.TryGetValue(entry.ReminderName, out string? collisionReason))
             {
                 ReminderEntry quarantined = Quarantine(entry, collisionReason, now);
-                entries.Add(quarantined);
-                newlyQuarantined.Add(quarantined);
+                entries.Add(entry);
+                newlyQuarantined.Add((entry, quarantined));
+            }
+            else if (!desired.ContainsKey(entry.ReminderName)
+                && retainedReminderNames?.Contains(entry.ReminderName) == true)
+            {
+                // The caller still owns this witness. Do not cancel it until that caller's later step succeeds.
+                entries.Add(entry);
             }
             else if (!desired.TryGetValue(entry.ReminderName, out (ReminderIntent Intent, ReminderEntry Witness) current))
             {
+                entries.Add(entry);
                 obsolete.Add(entry);
             }
             else if (!SameWitness(entry, current.Witness))
             {
                 ReminderEntry quarantined = Quarantine(entry, "witness-collision", now);
-                entries.Add(quarantined);
-                newlyQuarantined.Add(quarantined);
+                entries.Add(entry);
+                newlyQuarantined.Add((entry, quarantined));
                 string digest = EvidenceDigest(current.Intent);
-                if (AddQuarantine(quarantine, digest, "witness-collision", entry.ReminderName, now))
-                {
-                    auditQuarantine.Add((digest, "witness-collision"));
-                }
+                _ = AddQuarantine(quarantine, digest, "witness-collision", entry.ReminderName, now);
 
                 _ = desired.Remove(entry.ReminderName);
             }
@@ -537,11 +684,6 @@ internal sealed class ReminderCoordinator
         }
 
         ReminderItemState next = WithEntries(state, entries, quarantine);
-        bool changed = added
-            || obsolete.Count > 0
-            || newlyQuarantined.Count > 0
-            || quarantine.Count != state.Quarantine.Count
-            || (stored is not null && !HasWork(next));
 
         // The index is written before anything is scheduled, and re-written whenever the item holds work, so an
         // index restored from an older backup regains the candidate. It does not write when the entry exists.
@@ -550,41 +692,100 @@ internal sealed class ReminderCoordinator
             await _index.EnsureCandidateAsync(target, actorId, cancellationToken).ConfigureAwait(false);
         }
 
-        // Audit before releasing or recording: cancellations and new quarantine evidence.
+        // Audit before releasing or recording. Obsolete witnesses are retained until both the audit and
+        // Scheduler cancellation succeed. Quarantine transitions do not cancel the reminder when audit is
+        // unavailable, so the next convergence can retry without losing evidence.
+        var entryUpdates = new Dictionary<string, ReminderEntry?>(StringComparer.Ordinal);
+        int cancelled = 0;
         foreach (ReminderEntry entry in obsolete)
         {
-            _ = await TryWriteDispositionAsync(state, actorId, entry.ReminderName, ReminderDisposition.Cancelled, "obsolete", null, null, entry.Attempts, now, cancellationToken)
+            bool audited = await TryWriteDispositionAsync(
+                state, actorId, entry.ReminderName, ReminderDisposition.Cancelled, "obsolete", null, null, entry.Attempts, now, cancellationToken)
                 .ConfigureAwait(false);
+            if (audited
+                && await TryCancelAsync(scheduler, actorId, entry.ReminderName, entry.ReminderName, cancellationToken).ConfigureAwait(false))
+            {
+                entryUpdates[entry.ReminderName] = null;
+                cancelled++;
+                ReminderLog.Cancelled(_logger, actorId, entry.ReminderName);
+            }
+            else
+            {
+                string reasonCode = audited ? "cancel-failed" : "audit-unavailable";
+                entryUpdates[entry.ReminderName] = entry with
+                {
+                    Status = ReminderEntryStatus.Retrying,
+                    Attempts = entry.Attempts + 1,
+                    LastReasonCode = reasonCode,
+                    UpdatedAt = now,
+                };
+            }
         }
 
-        foreach (ReminderEntry entry in newlyQuarantined)
+        foreach ((ReminderEntry original, ReminderEntry quarantined) in newlyQuarantined)
         {
-            string reasonCode = entry.LastReasonCode ?? "witness-collision";
-            ReminderLog.Quarantined(_logger, actorId, entry.ReminderName, reasonCode);
-            _ = await TryWriteDispositionAsync(state, actorId, entry.ReminderName, ReminderDisposition.Quarantined, reasonCode, null, null, entry.Attempts, now, cancellationToken)
+            string reasonCode = quarantined.LastReasonCode ?? "witness-collision";
+            bool audited = await TryWriteDispositionAsync(
+                state, actorId, original.ReminderName, ReminderDisposition.Quarantined, reasonCode, null, null, original.Attempts, now, cancellationToken)
                 .ConfigureAwait(false);
+            if (audited)
+            {
+                ReminderLog.Quarantined(_logger, actorId, original.ReminderName, reasonCode);
+                entryUpdates[original.ReminderName] = quarantined;
+                _ = await TryCancelAsync(scheduler, actorId, original.ReminderName, original.ReminderName, cancellationToken)
+                    .ConfigureAwait(false);
+            }
+            else
+            {
+                entryUpdates[original.ReminderName] = original with
+                {
+                    Status = ReminderEntryStatus.Retrying,
+                    Attempts = original.Attempts + 1,
+                    LastReasonCode = "audit-unavailable",
+                    UpdatedAt = now,
+                };
+            }
         }
 
-        foreach ((string subject, string reasonCode) in auditQuarantine)
-        {
-            ReminderLog.Quarantined(_logger, actorId, subject, reasonCode);
-            _ = await TryWriteDispositionAsync(state, actorId, subject, ReminderDisposition.Quarantined, reasonCode, null, null, 0, now, cancellationToken)
-                .ConfigureAwait(false);
+        foreach (ReminderQuarantineRecord record in quarantine)
+        {
+            ReminderLog.Quarantined(_logger, actorId, record.EvidenceDigest, record.ReasonCode);
+            bool audited = await TryWriteDispositionAsync(
+                state,
+                actorId,
+                record.EvidenceDigest,
+                ReminderDisposition.Quarantined,
+                record.ReasonCode,
+                null,
+                null,
+                0,
+                now,
+                cancellationToken).ConfigureAwait(false);
+            if (audited
+                && record.ReminderName is not null
+                && !entries.Exists(entry => string.Equals(entry.ReminderName, record.ReminderName, StringComparison.Ordinal)))
+            {
+                _ = await TryCancelAsync(scheduler, actorId, record.ReminderName, record.ReminderName, cancellationToken)
+                    .ConfigureAwait(false);
+            }
         }
 
+        entries = [.. entries
+            .Select(entry => entryUpdates.TryGetValue(entry.ReminderName, out ReminderEntry? updated) ? updated : entry)
+            .OfType<ReminderEntry>()];
+        next = WithEntries(state, entries, quarantine);
+        bool changed = added
+            || entryUpdates.Count > 0
+            || quarantine.Count != state.Quarantine.Count
+            || (stored is not null && !HasWork(next));
+
         ReminderItemState? persisted = stored;
         if (changed)
         {
             (persisted, etag) = await PersistAsync(key, next, etag, cancellationToken).ConfigureAwait(false);
         }
 
-        var toCancel = new List<string>(obsolete.Select(static e => e.ReminderName));
-        toCancel.AddRange(newlyQuarantined.Select(static e => e.ReminderName));
-        int cancelled = obsolete.Count;
-        foreach (ReminderEntry entry in obsolete)
-        {
-            ReminderLog.Cancelled(_logger, actorId, entry.ReminderName);
-        }
+        var toCancel = new List<string>();
 
         // Arm future witnesses and submit due ones through the same path a callback uses.
         int armed = 0;
@@ -600,18 +801,40 @@ internal sealed class ReminderCoordinator
                 continue;
             }
 
-            if (current.Intent.DueUtc <= now)
+            DateTimeOffset operationNow = _time.GetUtcNow();
+            if (current.Intent.DueUtc <= operationNow)
             {
-                if (entry.Status == ReminderEntryStatus.Retrying && entry.UpdatedAt + Backoff(entry.Attempts) > now)
+                if (entry.Status == ReminderEntryStatus.Retrying && entry.UpdatedAt + Backoff(entry.Attempts) > operationNow)
                 {
-                    // Inside its backoff window: the armed backoff reminder retries it; replicas must not.
+                    // Inside its backoff window replicas must not submit, but reconciliation still repairs a
+                    // reminder the Scheduler lost so the work is not stranded until a later full pass.
+                    if (!await IsHeldAsync(scheduler, actorId, entry.ReminderName, cancellationToken).ConfigureAwait(false))
+                    {
+                        TimeSpan retryDue = entry.UpdatedAt + Backoff(entry.Attempts) - operationNow;
+                        try
+                        {
+                            await scheduler.ArmAsync(entry.ReminderName, retryDue, _options.RetryMaxDelay, cancellationToken)
+                                .ConfigureAwait(false);
+                            armed++;
+                            ReminderLog.Armed(_logger, actorId, entry.ReminderName);
+                        }
+                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
+                        {
+                            throw;
+                        }
+                        catch (Exception exception)
+                        {
+                            ReminderLog.ArmFailed(_logger, actorId, entry.ReminderName, exception.GetType().Name);
+                        }
+                    }
+
                     continue;
                 }
 
                 ReminderSubmissionOutcome outcome = await SubmitAsync(working, entry, current.Intent, cancellationToken)
                     .ConfigureAwait(false);
                 (ReminderEntry? settled, ReminderDisposition applied) = await SettleAsync(
-                    working, actorId, entry, outcome, now, toCancel, toRearm, cancellationToken)
+                    working, actorId, entry, outcome, operationNow, scheduler, toCancel, toRearm, cancellationToken)
                     .ConfigureAwait(false);
                 settledEntries[entry.ReminderName] = settled;
                 if (applied == ReminderDisposition.Submitted)
@@ -630,15 +853,33 @@ internal sealed class ReminderCoordinator
 
             try
             {
-                await scheduler.ArmAsync(entry.ReminderName, current.Intent.DueUtc - now, _options.RetryMaxDelay, cancellationToken)
+                await scheduler.ArmAsync(entry.ReminderName, current.Intent.DueUtc - operationNow, _options.RetryMaxDelay, cancellationToken)
                     .ConfigureAwait(false);
                 armed++;
                 ReminderLog.Armed(_logger, actorId, entry.ReminderName);
                 if (entry.Status != ReminderEntryStatus.Armed)
                 {
-                    settledEntries[entry.ReminderName] = entry with { Status = ReminderEntryStatus.Armed, LastReasonCode = null, UpdatedAt = now };
-                    _ = await TryWriteDispositionAsync(working, actorId, entry.ReminderName, ReminderDisposition.Registered, "armed", null, null, entry.Attempts, now, cancellationToken)
+                    bool audited = await TryWriteDispositionAsync(
+                        working,
+                        actorId,
+                        entry.ReminderName,
+                        ReminderDisposition.Registered,
+                        "armed",
+                        null,
+                        null,
+                        entry.Attempts,
+                        operationNow,
+                        cancellationToken)
                         .ConfigureAwait(false);
+                    settledEntries[entry.ReminderName] = audited
+                        ? entry with { Status = ReminderEntryStatus.Armed, LastReasonCode = null, UpdatedAt = operationNow }
+                        : entry with
+                        {
+                            Status = ReminderEntryStatus.Pending,
+                            Attempts = entry.Attempts + 1,
+                            LastReasonCode = "audit-unavailable",
+                            UpdatedAt = operationNow,
+                        };
                 }
             }
             catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
@@ -652,7 +893,7 @@ internal sealed class ReminderCoordinator
                 if (entry.Status != ReminderEntryStatus.Pending
                     || !string.Equals(entry.LastReasonCode, "arm-failed", StringComparison.Ordinal))
                 {
-                    settledEntries[entry.ReminderName] = entry with { Status = ReminderEntryStatus.Pending, LastReasonCode = "arm-failed", UpdatedAt = now };
+                    settledEntries[entry.ReminderName] = entry with { Status = ReminderEntryStatus.Pending, LastReasonCode = "arm-failed", UpdatedAt = operationNow };
                 }
             }
         }
@@ -670,13 +911,13 @@ internal sealed class ReminderCoordinator
 
         await RunSchedulerAsync(scheduler, actorId, toCancel, toRearm, cancellationToken).ConfigureAwait(false);
 
+        int unresolved = CountUnresolved(persisted);
         if (persisted is null || !HasWork(persisted))
         {
-            await _index.RemoveCandidateAsync(target, actorId, cancellationToken).ConfigureAwait(false);
+            unresolved += await ReleaseDiscoveryIfIdleAsync(actorId, target, cancellationToken).ConfigureAwait(false);
             persisted = null;
         }
 
-        int unresolved = CountUnresolved(persisted);
         int quarantinedCount = CountQuarantined(persisted);
         _status.RecordItem(actorId, unresolved, quarantinedCount);
         return new ReminderConvergenceResult(armed, submitted, cancelled, unresolved, quarantinedCount);
@@ -699,13 +940,31 @@ internal sealed class ReminderCoordinator
         if (AddQuarantine(quarantine, digest, "actor-collision", null, now))
         {
             ReminderLog.Quarantined(_logger, actorId, digest, "actor-collision");
-            _ = await TryWriteDispositionAsync(stored, actorId, digest, ReminderDisposition.Quarantined, "actor-collision", null, null, 0, now, cancellationToken)
-                .ConfigureAwait(false);
             (ReminderItemState? saved, _) = await PersistAsync(key, WithEntries(stored, stored.Entries, quarantine), etag, cancellationToken)
                 .ConfigureAwait(false);
             persisted = saved ?? stored;
         }
 
+        _ = await TryWriteDispositionAsync(
+            persisted,
+            actorId,
+            digest,
+            ReminderDisposition.Quarantined,
+            "actor-collision",
+            null,
+            null,
+            0,
+            now,
+            cancellationToken).ConfigureAwait(false);
+
+        if (HasWork(persisted))
+        {
+            await _index.EnsureCandidateAsync(
+                new ReminderTarget(persisted.Tenant, persisted.Domain, persisted.Aggregate),
+                actorId,
+                cancellationToken).ConfigureAwait(false);
+        }
+
         int unresolved = CountUnresolved(persisted);
         int quarantined = CountQuarantined(persisted);
         _status.RecordItem(actorId, unresolved, quarantined);
@@ -720,25 +979,62 @@ internal sealed class ReminderCoordinator
         CancellationToken cancellationToken)
     {
         string key = ReminderStateKeys.Item(_options.ActorTypeName, actorId);
-        (ReminderItemState? state, string? etag) = await LoadAsync(key, cancellationToken).ConfigureAwait(false);
+        (ReminderItemState? state, string? etag) = await LoadAsync(key, actorId, cancellationToken).ConfigureAwait(false);
         ReminderEntry? entry = state?.Entries.FirstOrDefault(
             e => string.Equals(e.ReminderName, reminderName, StringComparison.Ordinal));
         if (state is null || entry is null)
         {
+            ReminderQuarantineRecord? repaired = state?.Quarantine.FirstOrDefault(
+                record => string.Equals(record.ReminderName, reminderName, StringComparison.Ordinal));
+            if (state is not null && repaired is not null)
+            {
+                bool audited = await TryWriteDispositionAsync(
+                    state,
+                    actorId,
+                    repaired.EvidenceDigest,
+                    ReminderDisposition.Quarantined,
+                    repaired.ReasonCode,
+                    null,
+                    null,
+                    0,
+                    _time.GetUtcNow(),
+                    cancellationToken).ConfigureAwait(false);
+                if (audited)
+                {
+                    _ = await TryCancelAsync(scheduler, actorId, reminderName, loggedName, cancellationToken).ConfigureAwait(false);
+                }
+
+                _status.RecordItem(actorId, CountUnresolved(state), CountQuarantined(state));
+                return audited ? ReminderDisposition.Quarantined : ReminderDisposition.Retrying;
+            }
+
             // No witness: nothing is disclosed or mutated, and the scheduler stops firing it.
             ReminderLog.Orphan(_logger, actorId, loggedName);
-            await TryCancelAsync(scheduler, actorId, reminderName, loggedName, cancellationToken).ConfigureAwait(false);
+            _ = await TryCancelAsync(scheduler, actorId, reminderName, loggedName, cancellationToken).ConfigureAwait(false);
+            if (state is not null)
+            {
+                _status.RecordItem(actorId, CountUnresolved(state), CountQuarantined(state));
+            }
+
             return null;
         }
 
         DateTimeOffset now = _time.GetUtcNow();
         if (entry.Status == ReminderEntryStatus.Quarantined)
         {
-            await TryCancelAsync(scheduler, actorId, reminderName, loggedName, cancellationToken).ConfigureAwait(false);
+            _ = await TryCancelAsync(scheduler, actorId, reminderName, loggedName, cancellationToken).ConfigureAwait(false);
             _status.RecordItem(actorId, CountUnresolved(state), CountQuarantined(state));
             return ReminderDisposition.Quarantined;
         }
 
+        if (string.IsNullOrWhiteSpace(state.Domain))
+        {
+            // A blank domain cannot name a stream. Retiring the witness as stale would cancel a reminder
+            // the real stream may still hold.
+            return await SettleCallbackAsync(key, state, etag, actorId, entry, Outcome(ReminderDisposition.Quarantined, "domain-invalid"), now, scheduler, cancellationToken)
+                .ConfigureAwait(false);
+        }
+
         // Step 2: the stored full tuple must re-derive both the actor identifier and the reminder name.
         if (!ReminderIdentityCodec.Rederives(state.Tenant, state.Aggregate, entry.Kind, entry.DueUtc, entry.ScheduleRevision, actorId, reminderName)
             || !reminderName.EndsWith(entry.ScheduleToken, StringComparison.Ordinal))
@@ -756,10 +1052,10 @@ internal sealed class ReminderCoordinator
 
         // Step 4: the stream must still report this exact witness.
         var target = new ReminderTarget(state.Tenant, state.Domain, state.Aggregate);
-        IReadOnlyList<ReminderIntent> current;
+        IReadOnlyList<ReminderIntent>? current;
         try
         {
-            current = await _source.GetCurrentIntentsAsync(target, cancellationToken).ConfigureAwait(false) ?? [];
+            current = await _source.GetCurrentIntentsAsync(target, cancellationToken).ConfigureAwait(false);
         }
         catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
         {
@@ -767,6 +1063,12 @@ internal sealed class ReminderCoordinator
                 .ConfigureAwait(false);
         }
 
+        if (current is null)
+        {
+            return await SettleCallbackAsync(key, state, etag, actorId, entry, Outcome(ReminderDisposition.Retrying, "source-unavailable"), now, scheduler, cancellationToken)
+                .ConfigureAwait(false);
+        }
+
         List<ReminderIntent> valid = [.. current.Where(intent => ValidateIntent(intent, target) is null)];
         List<ReminderIntent> matches = [.. valid.Where(intent => string.Equals(
             ReminderIdentityCodec.ComputeReminderName(intent), reminderName, StringComparison.Ordinal))];
@@ -796,6 +1098,12 @@ internal sealed class ReminderCoordinator
                 .ConfigureAwait(false);
         }
 
+        if (entry.Status == ReminderEntryStatus.Retrying && entry.UpdatedAt + Backoff(entry.Attempts) > now)
+        {
+            _status.RecordItem(actorId, CountUnresolved(state), CountQuarantined(state));
+            return ReminderDisposition.Retrying;
+        }
+
         ReminderSubmissionOutcome outcome = await SubmitAsync(state, entry, match, cancellationToken).ConfigureAwait(false);
         return await SettleCallbackAsync(key, state, etag, actorId, entry, outcome, now, scheduler, cancellationToken)
             .ConfigureAwait(false);
@@ -821,23 +1129,81 @@ internal sealed class ReminderCoordinator
         if (!await TryWriteDispositionAsync(state, actorId, entry.ReminderName, ReminderDisposition.Stale, "witness-not-current", null, null, entry.Attempts, now, cancellationToken)
             .ConfigureAwait(false))
         {
-            _status.RecordItem(actorId, CountUnresolved(state), CountQuarantined(state));
+            ReminderItemState retained = WithEntries(
+                state,
+                state.Entries.Select(candidate => string.Equals(candidate.ReminderName, entry.ReminderName, StringComparison.Ordinal)
+                    ? candidate with
+                    {
+                        Status = ReminderEntryStatus.Retrying,
+                        Attempts = candidate.Attempts + 1,
+                        LastReasonCode = "audit-unavailable",
+                        UpdatedAt = now,
+                    }
+                    : candidate),
+                state.Quarantine);
+            (ReminderItemState? persistedRetry, _) = await PersistAsync(key, retained, etag, cancellationToken).ConfigureAwait(false);
+            _status.RecordItem(actorId, CountUnresolved(persistedRetry), CountQuarantined(persistedRetry));
+            return ReminderDisposition.Retrying;
+        }
+
+        // Index and persist the replacement while this witness and its scheduler reminder are still held.
+        // A fail-closed index or state write then leaves the firing in place for another callback.
+        _ = await ConvergeCoreAsync(
+            actorId,
+            key,
+            state,
+            etag,
+            target,
+            current,
+            now,
+            scheduler,
+            cancellationToken,
+            new HashSet<string>(StringComparer.Ordinal) { entry.ReminderName }).ConfigureAwait(false);
+
+        if (!await TryCancelAsync(scheduler, actorId, entry.ReminderName, entry.ReminderName, cancellationToken).ConfigureAwait(false))
+        {
+            (ReminderItemState? held, string? heldETag) = await LoadAsync(key, actorId, cancellationToken).ConfigureAwait(false);
+            if (held is null)
+            {
+                return ReminderDisposition.Retrying;
+            }
+
+            ReminderItemState retained = WithEntries(
+                held,
+                held.Entries.Select(candidate => string.Equals(candidate.ReminderName, entry.ReminderName, StringComparison.Ordinal)
+                    ? candidate with
+                    {
+                        Status = ReminderEntryStatus.Retrying,
+                        Attempts = candidate.Attempts + 1,
+                        LastReasonCode = "cancel-failed",
+                        UpdatedAt = now,
+                    }
+                    : candidate),
+                held.Quarantine);
+            (ReminderItemState? persistedRetry, _) = await PersistAsync(key, retained, heldETag, cancellationToken).ConfigureAwait(false);
+            _status.RecordItem(actorId, CountUnresolved(persistedRetry), CountQuarantined(persistedRetry));
             return ReminderDisposition.Retrying;
         }
 
         ReminderLog.Stale(_logger, actorId, entry.ReminderName);
+        (ReminderItemState? latest, string? latestETag) = await LoadAsync(key, actorId, cancellationToken).ConfigureAwait(false);
+        if (latest is null)
+        {
+            return ReminderDisposition.Stale;
+        }
+
         ReminderItemState next = WithEntries(
-            state,
-            state.Entries.Where(e => !string.Equals(e.ReminderName, entry.ReminderName, StringComparison.Ordinal)),
-            state.Quarantine);
-        (ReminderItemState? persisted, string? persistedETag) = await PersistAsync(key, next, etag, cancellationToken).ConfigureAwait(false);
-        await TryCancelAsync(scheduler, actorId, entry.ReminderName, entry.ReminderName, cancellationToken).ConfigureAwait(false);
+            latest,
+            latest.Entries.Where(e => !string.Equals(e.ReminderName, entry.ReminderName, StringComparison.Ordinal)),
+            latest.Quarantine);
+        (ReminderItemState? persisted, _) = await PersistAsync(key, next, latestETag, cancellationToken).ConfigureAwait(false);
+        int unresolved = CountUnresolved(persisted);
+        if (persisted is null || !HasWork(persisted))
+        {
+            unresolved += await ReleaseDiscoveryIfIdleAsync(actorId, target, cancellationToken).ConfigureAwait(false);
+        }
 
-        // The stream was just re-folded: converge with it so a witness that replaced the stale one is indexed
-        // and armed even if its own registration was lost. Convergence also removes the index entry last when
-        // the item holds nothing more.
-        _ = await ConvergeCoreAsync(actorId, key, persisted, persistedETag, target, current, now, scheduler, cancellationToken)
-            .ConfigureAwait(false);
+        _status.RecordItem(actorId, unresolved, CountQuarantined(persisted));
         return ReminderDisposition.Stale;
     }
 
@@ -855,7 +1221,7 @@ internal sealed class ReminderCoordinator
         var toCancel = new List<string>();
         var toRearm = new List<(string Name, TimeSpan DueTime)>();
         (ReminderEntry? settled, ReminderDisposition applied) = await SettleAsync(
-            state, actorId, entry, outcome, now, toCancel, toRearm, cancellationToken)
+            state, actorId, entry, outcome, now, scheduler, toCancel, toRearm, cancellationToken)
             .ConfigureAwait(false);
         ReminderItemState next = WithEntries(
             state,
@@ -865,13 +1231,14 @@ internal sealed class ReminderCoordinator
             state.Quarantine);
         (ReminderItemState? persisted, _) = await PersistAsync(key, next, etag, cancellationToken).ConfigureAwait(false);
         await RunSchedulerAsync(scheduler, actorId, toCancel, toRearm, cancellationToken).ConfigureAwait(false);
-        if (persisted is null)
+        int unresolved = CountUnresolved(persisted);
+        if (persisted is null || !HasWork(persisted))
         {
-            await _index.RemoveCandidateAsync(new ReminderTarget(state.Tenant, state.Domain, state.Aggregate), actorId, cancellationToken)
+            unresolved += await ReleaseDiscoveryIfIdleAsync(actorId, new ReminderTarget(state.Tenant, state.Domain, state.Aggregate), cancellationToken)
                 .ConfigureAwait(false);
         }
 
-        _status.RecordItem(actorId, CountUnresolved(persisted), CountQuarantined(persisted));
+        _status.RecordItem(actorId, unresolved, CountQuarantined(persisted));
         return applied;
     }
 
@@ -881,6 +1248,7 @@ internal sealed class ReminderCoordinator
         ReminderEntry entry,
         ReminderSubmissionOutcome outcome,
         DateTimeOffset now,
+        IReminderScheduler scheduler,
         List<string> toCancel,
         List<(string Name, TimeSpan DueTime)> toRearm,
         CancellationToken cancellationToken)
@@ -900,18 +1268,27 @@ internal sealed class ReminderCoordinator
                         outcome.EffectId ?? string.Empty,
                         outcome.Receipt?.Disposition.ToString() ?? string.Empty,
                         outcome.Receipt?.Replayed ?? false);
-                    toCancel.Add(entry.ReminderName);
-                    return (null, ReminderDisposition.Submitted);
+                    if (await TryCancelAsync(scheduler, actorId, entry.ReminderName, entry.ReminderName, cancellationToken)
+                        .ConfigureAwait(false))
+                    {
+                        return (null, ReminderDisposition.Submitted);
+                    }
+
+                    return Retry(entry, "cancel-failed", now, toRearm, actorId);
                 }
 
                 return Retry(entry, "audit-unavailable", now, toRearm, actorId);
 
             case ReminderDisposition.Quarantined:
-                ReminderLog.Quarantined(_logger, actorId, entry.ReminderName, outcome.ReasonCode);
-                _ = await TryWriteDispositionAsync(state, actorId, entry.ReminderName, ReminderDisposition.Quarantined, outcome.ReasonCode, outcome.EffectId, null, entry.Attempts, now, cancellationToken)
-                    .ConfigureAwait(false);
-                toCancel.Add(entry.ReminderName);
-                return (Quarantine(entry, outcome.ReasonCode, now), ReminderDisposition.Quarantined);
+                if (await TryWriteDispositionAsync(state, actorId, entry.ReminderName, ReminderDisposition.Quarantined, outcome.ReasonCode, outcome.EffectId, null, entry.Attempts, now, cancellationToken)
+                    .ConfigureAwait(false))
+                {
+                    ReminderLog.Quarantined(_logger, actorId, entry.ReminderName, outcome.ReasonCode);
+                    toCancel.Add(entry.ReminderName);
+                    return (Quarantine(entry, outcome.ReasonCode, now), ReminderDisposition.Quarantined);
+                }
+
+                return Retry(entry, "audit-unavailable", now, toRearm, actorId);
 
             default:
                 int attempts = entry.Attempts + 1;
@@ -1025,9 +1402,14 @@ internal sealed class ReminderCoordinator
         {
             throw;
         }
-        catch (Exception)
+        catch (Exception exception)
         {
             // The target may or may not hold a receipt. Retrying with the same effect identity replays it.
+            ReminderLog.SubmissionUncertain(
+                _logger,
+                ReminderIdentityCodec.ComputeActorId(state.Tenant, state.Aggregate),
+                entry.ReminderName,
+                exception.GetType().Name);
             return new ReminderSubmissionOutcome(ReminderDisposition.Retrying, "submission-uncertain", effectId, null);
         }
 
@@ -1041,14 +1423,144 @@ internal sealed class ReminderCoordinator
         return new ReminderSubmissionOutcome(ReminderDisposition.Submitted, "receipt", effectId, receipt);
     }
 
-    private async Task<(ReminderItemState? State, string? ETag)> LoadAsync(string key, CancellationToken cancellationToken)
+    /// <summary>
+    /// Drops the discovery candidate only when a fresh fold reports no current intents. A null or failed fold,
+    /// or a stream that still reports work, keeps the candidate so reconciliation can converge it again.
+    /// </summary>
+    /// <returns>One unresolved item when the fold is unavailable or discovery cannot be ensured; zero otherwise.</returns>
+    private async Task<int> ReleaseDiscoveryIfIdleAsync(string actorId, ReminderTarget target, CancellationToken cancellationToken)
+    {
+        if (string.IsNullOrWhiteSpace(target.Domain))
+        {
+            return 1;
+        }
+
+        IReadOnlyList<ReminderIntent>? intents;
+        try
+        {
+            intents = await _source.GetCurrentIntentsAsync(target, cancellationToken).ConfigureAwait(false);
+        }
+        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
+        {
+            throw;
+        }
+        catch (Exception exception)
+        {
+            ReminderLog.CallbackFailed(_logger, actorId, MalformedName, exception.GetType().Name);
+            return 1;
+        }
+
+        if (intents is null || intents.Count > 0)
+        {
+            try
+            {
+                await _index.EnsureCandidateAsync(target, actorId, cancellationToken).ConfigureAwait(false);
+            }
+            catch (ReminderFailClosedException exception)
+            {
+                ReminderLog.FailedClosed(_logger, actorId, exception.ReasonCode);
+                return 1;
+            }
+
+            return intents is null ? 1 : 0;
+        }
+
+        await _index.RemoveCandidateAsync(target, actorId, cancellationToken).ConfigureAwait(false);
+        return 0;
+    }
+
+    private async Task<(ReminderItemState? State, string? ETag)> LoadAsync(
+        string key,
+        string actorId,
+        CancellationToken cancellationToken)
     {
         ReadModelEntry<ReminderItemState> entry = await _store
             .GetAsync<ReminderItemState>(_options.StateStoreName, key, cancellationToken)
             .ConfigureAwait(false);
-        return entry.Value is null
-            ? (null, null)
-            : (entry.Value with { Entries = entry.Value.Entries ?? [], Quarantine = entry.Value.Quarantine ?? [] }, entry.ETag);
+        if (entry.Value is null)
+        {
+            return (null, null);
+        }
+
+        ReminderItemState loaded = entry.Value;
+        DateTimeOffset now = _time.GetUtcNow();
+        var candidateEntries = new List<(ReminderEntry Entry, int Ordinal, string EffectId)>();
+        var validQuarantine = new List<ReminderQuarantineRecord>();
+        bool repaired = loaded.Entries is null || loaded.Quarantine is null;
+
+        int ordinal = 0;
+        foreach (ReminderEntry? candidate in loaded.Entries ?? [])
+        {
+            if (TryValidatePersistedEntry(loaded, actorId, candidate, out string effectId))
+            {
+                candidateEntries.Add((candidate!, ordinal, effectId));
+            }
+            else
+            {
+                repaired = true;
+                string digest = EvidenceDigest(candidate, ordinal);
+                validQuarantine.Add(new ReminderQuarantineRecord(
+                    digest,
+                    "stored-entry-invalid",
+                    candidate is not null && ReminderIdentityCodec.TryParseReminderName(candidate.ReminderName, out _, out _)
+                        ? candidate.ReminderName
+                        : null,
+                    now));
+            }
+
+            ordinal++;
+        }
+
+        var duplicateOrdinals = new HashSet<int>(candidateEntries
+            .GroupBy(static candidate => candidate.Entry.ReminderName, StringComparer.Ordinal)
+            .Where(static group => group.Count() > 1)
+            .SelectMany(static group => group.Select(static candidate => candidate.Ordinal)));
+        duplicateOrdinals.UnionWith(candidateEntries
+            .GroupBy(static candidate => candidate.EffectId, StringComparer.Ordinal)
+            .Where(static group => group.Count() > 1)
+            .SelectMany(static group => group.Select(static candidate => candidate.Ordinal)));
+
+        var validEntries = new List<ReminderEntry>();
+        foreach ((ReminderEntry candidate, int candidateOrdinal, _) in candidateEntries)
+        {
+            if (!duplicateOrdinals.Contains(candidateOrdinal))
+            {
+                validEntries.Add(candidate);
+                continue;
+            }
+
+            repaired = true;
+            validQuarantine.Add(new ReminderQuarantineRecord(
+                EvidenceDigest(candidate, candidateOrdinal),
+                "stored-entry-duplicate",
+                candidate.ReminderName,
+                now));
+        }
+
+        ordinal = 0;
+        foreach (ReminderQuarantineRecord? candidate in loaded.Quarantine ?? [])
+        {
+            if (IsValidPersistedQuarantine(candidate))
+            {
+                validQuarantine.Add(candidate!);
+            }
+            else
+            {
+                repaired = true;
+                validQuarantine.Add(new ReminderQuarantineRecord(
+                    EvidenceDigest(candidate, ordinal),
+                    "stored-quarantine-invalid",
+                    null,
+                    now));
+            }
+
+            ordinal++;
+        }
+
+        ReminderItemState normalized = WithEntries(loaded, validEntries, validQuarantine);
+        return repaired
+            ? await PersistAsync(key, normalized, entry.ETag, cancellationToken).ConfigureAwait(false)
+            : (normalized, entry.ETag);
     }
 
     private async Task<(ReminderItemState? State, string? ETag)> PersistAsync(
@@ -1177,7 +1689,7 @@ internal sealed class ReminderCoordinator
         }
     }
 
-    private async Task TryCancelAsync(
+    private async Task<bool> TryCancelAsync(
         IReminderScheduler scheduler,
         string actorId,
         string reminderName,
@@ -1187,6 +1699,7 @@ internal sealed class ReminderCoordinator
         try
         {
             await scheduler.CancelAsync(reminderName, cancellationToken).ConfigureAwait(false);
+            return true;
         }
         catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
         {
@@ -1195,6 +1708,7 @@ internal sealed class ReminderCoordinator
         catch (Exception exception)
         {
             ReminderLog.CancelFailed(_logger, actorId, loggedName, exception.GetType().Name);
+            return false;
         }
     }
 }
diff --git a/src/Hexalith.EventStore.DomainService/ReminderIntentIndex.cs b/src/Hexalith.EventStore.DomainService/ReminderIntentIndex.cs
index 9acec0308c22b3db809435a90bd7536f7cb4c65b..57ad9ea399919ddd41c5c89879c533120f5e1dc2 100644
--- a/src/Hexalith.EventStore.DomainService/ReminderIntentIndex.cs
+++ b/src/Hexalith.EventStore.DomainService/ReminderIntentIndex.cs
@@ -75,9 +75,17 @@ internal sealed class ReminderIntentIndex(IReadModelStore store, IOptions<EventS
         var candidate = new ReminderCandidate(target.Domain, target.Aggregate, actorId);
         return UpdateAsync<ReminderTenantCandidates>(
             ReminderStateKeys.TenantCandidates(_options.ActorTypeName, target.Tenant),
-            current => current is null || !current.Candidates.Contains(candidate)
-                ? null
-                : current with { Candidates = [.. current.Candidates.Where(c => c != candidate)] },
+            current =>
+            {
+                // A null list is corrupt discovery data, not a list that contains this candidate.
+                if (current?.Candidates is not IReadOnlyList<ReminderCandidate> candidates
+                    || !candidates.Contains(candidate))
+                {
+                    return null;
+                }
+
+                return current with { Candidates = [.. candidates.Where(c => c != candidate)] };
+            },
             cancellationToken);
     }
 
diff --git a/src/Hexalith.EventStore.DomainService/ReminderLog.cs b/src/Hexalith.EventStore.DomainService/ReminderLog.cs
index 14aca5839baae9d88743ab191b182e0a31ac3c2d..4afcaf800f27cfce4e66c6d843e769c02856a95c 100644
--- a/src/Hexalith.EventStore.DomainService/ReminderLog.cs
+++ b/src/Hexalith.EventStore.DomainService/ReminderLog.cs
@@ -83,4 +83,8 @@ internal static partial class ReminderLog
     [LoggerMessage(EventId = 200218, Level = LogLevel.Debug,
         Message = "Reminder lookup did not confirm the scheduler holds it; re-arming: ActorId={ActorId}, ReminderName={ReminderName}, ExceptionType={ExceptionType}")]
     public static partial void LookupFailed(ILogger logger, string actorId, string reminderName, string exceptionType);
+
+    [LoggerMessage(EventId = 200219, Level = LogLevel.Warning,
+        Message = "Reminder submission outcome is uncertain; the witness is retained for replay: ActorId={ActorId}, ReminderName={ReminderName}, ExceptionType={ExceptionType}")]
+    public static partial void SubmissionUncertain(ILogger logger, string actorId, string reminderName, string exceptionType);
 }
diff --git a/src/Hexalith.EventStore.DomainService/ReminderReconciler.cs b/src/Hexalith.EventStore.DomainService/ReminderReconciler.cs
index cf9396547e28952aa0c9df599a145290bd3b7a5b..140ec17ca3710665a14afa7e237c0d29f68b4009 100644
--- a/src/Hexalith.EventStore.DomainService/ReminderReconciler.cs
+++ b/src/Hexalith.EventStore.DomainService/ReminderReconciler.cs
@@ -71,14 +71,29 @@ internal sealed class ReminderReconciler(
                 continue;
             }
 
-            foreach (ReminderCandidate candidate in tenantCandidates)
+            foreach (ReminderCandidate? candidate in tenantCandidates)
             {
                 candidates++;
-                observed.Add(candidate.ActorId);
                 try
                 {
+                    if (candidate is null)
+                    {
+                        ReminderLog.ScanFailed(_logger, "tenant-candidate", "candidate-missing");
+                        incomplete++;
+                        continue;
+                    }
+
+                    var target = new ReminderTarget(tenant, candidate.Domain, candidate.Aggregate);
+                    if (!string.Equals(ReminderCoordinator.ComputeActorId(target), candidate.ActorId, StringComparison.Ordinal))
+                    {
+                        ReminderLog.CandidateFailed(_logger, candidate.ActorId, "actor-id-mismatch");
+                        incomplete++;
+                        continue;
+                    }
+
+                    observed.Add(candidate.ActorId);
                     ReminderConvergenceResult result = await _registrar
-                        .ConvergeAsync(new ReminderTarget(tenant, candidate.Domain, candidate.Aggregate), cancellationToken)
+                        .ConvergeAsync(target, cancellationToken)
                         .ConfigureAwait(false);
                     armed += result.Armed;
                     submitted += result.Submitted;
@@ -88,7 +103,7 @@ internal sealed class ReminderReconciler(
                 }
                 catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                 {
-                    ReminderLog.CandidateFailed(_logger, candidate.ActorId, exception.GetType().Name);
+                    ReminderLog.CandidateFailed(_logger, candidate?.ActorId ?? "candidate-missing", exception.GetType().Name);
                     incomplete++;
                 }
             }
diff --git a/tests/Hexalith.EventStore.DomainService.Tests/EventStoreReminderCompositionTests.cs b/tests/Hexalith.EventStore.DomainService.Tests/EventStoreReminderCompositionTests.cs
index 6cdb4d237d01fbbe63830f93cd81464fd57ce5f9..ab2c8eb08e373f8594af35d70e47b68e797670ab 100644
--- a/tests/Hexalith.EventStore.DomainService.Tests/EventStoreReminderCompositionTests.cs
+++ b/tests/Hexalith.EventStore.DomainService.Tests/EventStoreReminderCompositionTests.cs
@@ -78,6 +78,24 @@ public sealed class EventStoreReminderCompositionTests
         provider.GetRequiredService<IOptions<EventStoreReminderOptions>>().Value.RetryInitialDelay.ShouldBe(TimeSpan.FromSeconds(5));
     }
 
+    /// <summary>A blank Dapr application identifier does not suppress the host application-name fallback.</summary>
+    [Fact]
+    public void BlankDaprApplicationIdUsesHostApplicationName()
+    {
+        string? previous = Environment.GetEnvironmentVariable("DAPR_APP_ID");
+        try
+        {
+            Environment.SetEnvironmentVariable("DAPR_APP_ID", " \t");
+            using ServiceProvider provider = CreateServices().BuildServiceProvider();
+
+            provider.GetRequiredService<IOptions<EventStoreReminderOptions>>().Value.Workload.ShouldBe("widget-host");
+        }
+        finally
+        {
+            Environment.SetEnvironmentVariable("DAPR_APP_ID", previous);
+        }
+    }
+
     /// <summary>Invalid options fail validation instead of producing unscoped or unsafe keys.</summary>
     [Theory]
     [InlineData("ActorTypeName", "")]
diff --git a/tests/Hexalith.EventStore.DomainService.Tests/Fixtures/DispositionFailingReadModelStore.cs b/tests/Hexalith.EventStore.DomainService.Tests/Fixtures/DispositionFailingReadModelStore.cs
index ec9ece04a5391b09a6d116eebb734049f1dc7bb9..5af224c70d71f94a39b4805d1ebf4c760ac8ae4f 100644
--- a/tests/Hexalith.EventStore.DomainService.Tests/Fixtures/DispositionFailingReadModelStore.cs
+++ b/tests/Hexalith.EventStore.DomainService.Tests/Fixtures/DispositionFailingReadModelStore.cs
@@ -9,6 +9,9 @@ internal sealed class DispositionFailingReadModelStore(InMemoryReadModelStore in
     /// <summary>Gets or sets a value indicating whether writes to <c>:disposition:</c> keys throw.</summary>
     public bool FailDispositionWrites { get; set; }
 
+    /// <summary>Gets or sets a predicate that rejects selected conditional writes.</summary>
+    public Func<string, bool>? RejectTrySave { get; set; }
+
     /// <inheritdoc/>
     public Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key, CancellationToken cancellationToken = default)
         where TValue : class
@@ -24,7 +27,9 @@ internal sealed class DispositionFailingReadModelStore(InMemoryReadModelStore in
     /// <inheritdoc/>
     public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag, CancellationToken cancellationToken = default)
         where TValue : class
-        => inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
+        => RejectTrySave?.Invoke(key) == true
+            ? Task.FromResult(false)
+            : inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
 
     /// <inheritdoc/>
     public Task<bool> TryEraseAsync(string storeName, string key, string etag, CancellationToken cancellationToken = default)
diff --git a/tests/Hexalith.EventStore.DomainService.Tests/Fixtures/FakeReminderIntentSource.cs b/tests/Hexalith.EventStore.DomainService.Tests/Fixtures/FakeReminderIntentSource.cs
index e3b81e1a6fb589ec51bb6d75ab8f6ada6d1ea283..c5391abc5c289c071d235a180fbdb6d79d67c65e 100644
--- a/tests/Hexalith.EventStore.DomainService.Tests/Fixtures/FakeReminderIntentSource.cs
+++ b/tests/Hexalith.EventStore.DomainService.Tests/Fixtures/FakeReminderIntentSource.cs
@@ -14,8 +14,16 @@ internal sealed class FakeReminderIntentSource : IReminderIntentSource
     /// <summary>Gets or sets an optional translator override.</summary>
     public Func<ReminderIntent, ReminderCommand>? Translator { get; set; }
 
+    /// <summary>Gets or sets a value indicating whether the next fold returns null instead of a list.</summary>
+    public bool ReturnNull { get; set; }
+
+    private int _reads;
+
+    /// <summary>Gets or sets an observer invoked after each stream re-fold starts.</summary>
+    public Action<int>? OnRead { get; set; }
+
     /// <summary>Gets the number of stream re-folds.</summary>
-    public int Reads { get; private set; }
+    public int Reads => Volatile.Read(ref _reads);
 
     /// <summary>Replaces the target's current intents.</summary>
     /// <param name="target">The target.</param>
@@ -25,17 +33,25 @@ internal sealed class FakeReminderIntentSource : IReminderIntentSource
     /// <inheritdoc/>
     public Task<IReadOnlyList<ReminderIntent>> GetCurrentIntentsAsync(ReminderTarget target, CancellationToken cancellationToken = default)
     {
-        Reads++;
+        int reads = Interlocked.Increment(ref _reads);
+        OnRead?.Invoke(reads);
         if (Failing.Contains(target))
         {
             throw new InvalidOperationException("Synthetic stream read failure.");
         }
 
+        if (ReturnNull)
+        {
+            return Task.FromResult<IReadOnlyList<ReminderIntent>>(null!);
+        }
+
         return Task.FromResult<IReadOnlyList<ReminderIntent>>(
             _intents.TryGetValue(target, out List<ReminderIntent>? intents) ? [.. intents] : []);
     }
 
     /// <inheritdoc/>
     public ReminderCommand TranslateDueIntent(ReminderIntent intent)
-        => Translator?.Invoke(intent) ?? new ReminderCommand("ResumeWidget", [.. intent.Payload]);
+        => Translator is null
+            ? new ReminderCommand("ResumeWidget", [.. intent.Payload])
+            : Translator.Invoke(intent);
 }
diff --git a/tests/Hexalith.EventStore.DomainService.Tests/Fixtures/FakeReminderScheduler.cs b/tests/Hexalith.EventStore.DomainService.Tests/Fixtures/FakeReminderScheduler.cs
index 0f7ba9bdcf06b5ad1e9087775490e20a184df9a8..423457ddb7be0383ff5dcad3eafd5f29f1b240c3 100644
--- a/tests/Hexalith.EventStore.DomainService.Tests/Fixtures/FakeReminderScheduler.cs
+++ b/tests/Hexalith.EventStore.DomainService.Tests/Fixtures/FakeReminderScheduler.cs
@@ -12,6 +12,12 @@ internal sealed class FakeReminderScheduler : IReminderScheduler
     /// <summary>Gets or sets the failure every arm call throws, simulating an unavailable scheduler.</summary>
     public Exception? ArmFailure { get; set; }
 
+    /// <summary>Gets or sets the failure every reminder lookup throws, simulating an unavailable scheduler.</summary>
+    public Exception? LookupFailure { get; set; }
+
+    /// <summary>Gets or sets the failure every cancellation throws, simulating an unavailable scheduler.</summary>
+    public Exception? CancelFailure { get; set; }
+
     /// <summary>Gets or sets a probe invoked before each accepted arm call.</summary>
     public Action<string>? OnArm { get; set; }
 
@@ -34,11 +40,18 @@ internal sealed class FakeReminderScheduler : IReminderScheduler
 
     /// <inheritdoc/>
     public Task<bool> IsArmedAsync(string reminderName, CancellationToken cancellationToken)
-        => Task.FromResult(Armed.ContainsKey(reminderName));
+        => LookupFailure is not null
+            ? throw LookupFailure
+            : Task.FromResult(Armed.ContainsKey(reminderName));
 
     /// <inheritdoc/>
     public Task CancelAsync(string reminderName, CancellationToken cancellationToken)
     {
+        if (CancelFailure is not null)
+        {
+            throw CancelFailure;
+        }
+
         _ = Armed.Remove(reminderName);
         Cancelled.Add(reminderName);
         return Task.CompletedTask;
diff --git a/tests/Hexalith.EventStore.DomainService.Tests/Fixtures/ReminderTestHarness.cs b/tests/Hexalith.EventStore.DomainService.Tests/Fixtures/ReminderTestHarness.cs
index e5dac946460b54ba9d80ff9e2e8f7c352168bdcd..633f7bd67fb71c711b20d46287ec9b30f172c2f3 100644
--- a/tests/Hexalith.EventStore.DomainService.Tests/Fixtures/ReminderTestHarness.cs
+++ b/tests/Hexalith.EventStore.DomainService.Tests/Fixtures/ReminderTestHarness.cs
@@ -143,14 +143,15 @@ internal sealed class ReminderTestHarness
 
     /// <summary>Creates a reconciler for one host.</summary>
     /// <param name="status">The host-local readiness view; defaults to <see cref="Status"/>.</param>
+    /// <param name="timeProvider">The timer provider; defaults to the controllable test clock.</param>
     /// <returns>The reconciler.</returns>
-    public ReminderReconciler CreateReconciler(ReminderRuntimeStatus? status = null)
+    public ReminderReconciler CreateReconciler(ReminderRuntimeStatus? status = null, TimeProvider? timeProvider = null)
         => new(
             CreateIndex(),
             CreateRegistrar(status),
             status ?? Status,
             Microsoft.Extensions.Options.Options.Create(Options),
-            Time,
+            timeProvider ?? Time,
             NullLogger<ReminderReconciler>.Instance);
 
     /// <summary>Delivers a scheduler callback inside the actor's serialized turn.</summary>
diff --git a/tests/Hexalith.EventStore.DomainService.Tests/ReminderCallbackAdmissionTests.cs b/tests/Hexalith.EventStore.DomainService.Tests/ReminderCallbackAdmissionTests.cs
index 46d6d9c5377801a54eced59d7338701b7e7c90c6..6bfdd4f15e1e0b7358533d2a4dd4de90166d30a5 100644
--- a/tests/Hexalith.EventStore.DomainService.Tests/ReminderCallbackAdmissionTests.cs
+++ b/tests/Hexalith.EventStore.DomainService.Tests/ReminderCallbackAdmissionTests.cs
@@ -1,3 +1,4 @@
+using Hexalith.EventStore.Client.Reminders;
 using Hexalith.EventStore.Contracts.Effects;
 using Hexalith.EventStore.Contracts.Reminders;
 using Hexalith.EventStore.DomainService.Tests.Fixtures;
@@ -103,6 +104,41 @@ public sealed class ReminderCallbackAdmissionTests
         harness.SchedulerFor(actorId).Cancelled.ShouldNotContain(ReminderTestHarness.Name(stale));
     }
 
+    /// <summary>A stale witness stays retrying until Scheduler cancellation succeeds on a later callback.</summary>
+    [Fact]
+    public async Task StaleWitnessIsRetainedUntilCancellationRecovers()
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        ReminderIntent stale = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1), revision: 1);
+        ReminderIntent current = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(8), revision: 2, sequence: 6);
+        string staleName = ReminderTestHarness.Name(stale);
+        harness.Source.Set(Item, stale);
+        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
+        harness.Source.Set(Item, current);
+        harness.Time.Advance(TimeSpan.FromHours(1));
+        harness.SchedulerFor(actorId).CancelFailure = new InvalidOperationException("Synthetic Scheduler outage.");
+
+        ReminderDisposition? retained = await harness.FireAsync(actorId, staleName);
+
+        retained.ShouldBe(ReminderDisposition.Retrying);
+        ReminderItemState held = harness.ItemState(actorId).ShouldNotBeNull();
+        ReminderEntry retrying = held.Entries.Single(entry => entry.ReminderName == staleName);
+        retrying.Status.ShouldBe(ReminderEntryStatus.Retrying);
+        retrying.LastReasonCode.ShouldBe("cancel-failed");
+        held.Entries.ShouldContain(entry => entry.ReminderName == ReminderTestHarness.Name(current));
+        harness.SchedulerFor(actorId).Armed.ShouldContainKey(staleName);
+        harness.Candidates().ShouldHaveSingleItem();
+
+        harness.SchedulerFor(actorId).CancelFailure = null;
+        ReminderDisposition? recovered = await harness.FireAsync(actorId, staleName);
+
+        recovered.ShouldBe(ReminderDisposition.Stale);
+        harness.SchedulerFor(actorId).Cancelled.ShouldContain(staleName);
+        harness.SchedulerFor(actorId).Armed.Keys.ShouldBe([ReminderTestHarness.Name(current)]);
+        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().ReminderName.ShouldBe(ReminderTestHarness.Name(current));
+    }
+
     /// <summary>A stream that cannot be re-folded during a callback retains the witness and counts the attempt.</summary>
     [Fact]
     public async Task UnreadableStreamDuringCallbackIsRetained()
@@ -184,10 +220,12 @@ public sealed class ReminderCallbackAdmissionTests
 
         disposition.ShouldBe(ReminderDisposition.Quarantined);
         harness.Submitter.Calls.ShouldBeEmpty();
-        ReminderEntry quarantined = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
-        quarantined.Status.ShouldBe(ReminderEntryStatus.Quarantined);
-        quarantined.LastReasonCode.ShouldBe("tuple-mismatch");
-        harness.Disposition(actorId, name).ShouldNotBeNull().Disposition.ShouldBe(ReminderDisposition.Quarantined);
+        ReminderItemState quarantined = harness.ItemState(actorId).ShouldNotBeNull();
+        quarantined.Entries.ShouldBeEmpty();
+        ReminderQuarantineRecord evidence = quarantined.Quarantine.ShouldHaveSingleItem();
+        evidence.ReasonCode.ShouldBe("stored-entry-invalid");
+        evidence.ReminderName.ShouldBe(name);
+        harness.Disposition(actorId, evidence.EvidenceDigest).ShouldNotBeNull().Disposition.ShouldBe(ReminderDisposition.Quarantined);
         harness.SchedulerFor(actorId).Armed.ShouldNotContainKey(name);
         harness.Candidates().ShouldHaveSingleItem();
         harness.Status.Snapshot().Quarantined.ShouldBe(1);
@@ -195,7 +233,7 @@ public sealed class ReminderCallbackAdmissionTests
         // Quarantine is retained across later firings and convergence until an operator disposes of it.
         (await harness.FireAsync(actorId, name)).ShouldBe(ReminderDisposition.Quarantined);
         _ = await harness.CreateRegistrar().ConvergeAsync(Item);
-        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().Status.ShouldBe(ReminderEntryStatus.Quarantined);
+        harness.ItemState(actorId).ShouldNotBeNull().Quarantine.ShouldHaveSingleItem();
         harness.Submitter.Calls.ShouldBeEmpty();
     }
 
@@ -323,6 +361,62 @@ public sealed class ReminderCallbackAdmissionTests
         harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().LastReasonCode.ShouldBe("translation-failed");
     }
 
+    /// <summary>A null or blank translation is quarantined and is not submitted.</summary>
+    [Theory]
+    [InlineData(true)]
+    [InlineData(false)]
+    public async Task BlankTranslationIsQuarantined(bool nullCommand)
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
+        harness.Source.Set(Item, intent);
+        harness.Source.Translator = nullCommand
+            ? _ => null!
+            : _ => new ReminderCommand(" ", null!);
+        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
+        harness.Time.Advance(TimeSpan.FromHours(1));
+
+        ReminderDisposition? disposition = await harness.FireAsync(actorId, ReminderTestHarness.Name(intent));
+
+        disposition.ShouldBe(ReminderDisposition.Quarantined);
+        harness.Submitter.Calls.ShouldBeEmpty();
+        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().LastReasonCode.ShouldBe("translation-invalid");
+    }
+
+    /// <summary>
+    /// A stale firing whose replacement cannot be indexed leaves the original reminder armed.
+    /// </summary>
+    [Fact]
+    public async Task StaleCallbackKeepsReminderWhenReplacementIndexIsFull()
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        ReminderIntent stale = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2), revision: 1);
+        string staleName = ReminderTestHarness.Name(stale);
+        harness.Source.Set(Item, stale);
+        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
+        ReminderTarget other = ReminderTestHarness.Target("item-2");
+        harness.Source.Set(other, ReminderTestHarness.Intent(other, harness.Time.Now.AddHours(3)));
+        _ = await harness.CreateRegistrar().ConvergeAsync(other);
+        harness.Options.MaxCandidatesPerTenant = 1;
+        harness.Store.SeedRaw(
+            harness.Options.StateStoreName,
+            ReminderStateKeys.TenantCandidates(harness.Options.ActorTypeName, ReminderTestHarness.Tenant),
+            new ReminderTenantCandidates(
+                ReminderTestHarness.Tenant,
+                [new ReminderCandidate(other.Domain, other.Aggregate, ReminderTestHarness.ActorId(other))]));
+        harness.Source.Set(Item, ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(8), revision: 2, sequence: 6));
+
+        ReminderDisposition? disposition = await harness.FireAsync(actorId, staleName);
+
+        disposition.ShouldBe(ReminderDisposition.Retrying);
+        harness.SchedulerFor(actorId).Cancelled.ShouldNotContain(staleName);
+        harness.SchedulerFor(actorId).Armed.ShouldContainKey(staleName);
+        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldContain(entry => entry.ReminderName == staleName);
+        harness.Submitter.Calls.ShouldBeEmpty();
+    }
+
     /// <summary>Outside Development the reminder routes require the exact app-channel token.</summary>
     [Theory]
     [InlineData(null, "token-missing")]
@@ -346,6 +440,31 @@ public sealed class ReminderCallbackAdmissionTests
         context.Response.Body.Length.ShouldBe(0);
     }
 
+    /// <summary>A repeated app-channel token is denied even when the first value matches.</summary>
+    [Fact]
+    public async Task TokenFilterRejectsRepeatedAppChannelToken()
+    {
+        ReminderCallbackTokenFilter filter = CreateFilter(Environments.Production, "app-token");
+        var context = new DefaultHttpContext();
+        context.Request.Method = HttpMethods.Put;
+        context.Request.Path = ReminderRoute;
+        context.Response.Body = new MemoryStream();
+        context.Request.Headers.Append(ReminderCallbackTokenFilter.HeaderName, "app-token");
+        context.Request.Headers.Append(ReminderCallbackTokenFilter.HeaderName, "second-token");
+        bool reachedActor = false;
+
+        await filter.InvokeAsync(context, _ =>
+        {
+            reachedActor = true;
+            return Task.CompletedTask;
+        });
+
+        filter.GetDenialReason(context.Request).ShouldBe("token-missing");
+        reachedActor.ShouldBeFalse();
+        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
+        context.Response.Body.Length.ShouldBe(0);
+    }
+
     /// <summary>Without a configured token the filter fails closed outside Development and reports itself unconfigured.</summary>
     [Fact]
     public async Task TokenFilterFailsClosedWhenTokenIsUnconfigured()
diff --git a/tests/Hexalith.EventStore.DomainService.Tests/ReminderCoordinatorTests.cs b/tests/Hexalith.EventStore.DomainService.Tests/ReminderCoordinatorTests.cs
index 38591b68d5cc1076f3ea90576b59ea688a4a6cbe..3817285ed876cfa7e219bdb06c42865d77022b3b 100644
--- a/tests/Hexalith.EventStore.DomainService.Tests/ReminderCoordinatorTests.cs
+++ b/tests/Hexalith.EventStore.DomainService.Tests/ReminderCoordinatorTests.cs
@@ -93,6 +93,67 @@ public sealed class ReminderCoordinatorTests
         harness.Disposition(actorId, ReminderTestHarness.Name(first)).ShouldNotBeNull().Disposition.ShouldBe(ReminderDisposition.Cancelled);
     }
 
+    /// <summary>An obsolete witness stays durable and scheduled until its cancellation audit can be written.</summary>
+    [Fact]
+    public async Task ObsoleteWitnessIsRetainedWhenAuditFails()
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
+        string name = ReminderTestHarness.Name(intent);
+        harness.Source.Set(Item, intent);
+        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
+        harness.Source.Set(Item);
+        harness.CoordinatorStore.FailDispositionWrites = true;
+
+        ReminderConvergenceResult retained = await harness.CreateRegistrar().ConvergeAsync(Item);
+
+        retained.Cancelled.ShouldBe(0);
+        retained.Unresolved.ShouldBe(1);
+        ReminderEntry entry = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
+        entry.ReminderName.ShouldBe(name);
+        entry.LastReasonCode.ShouldBe("audit-unavailable");
+        harness.SchedulerFor(actorId).Armed.ShouldContainKey(name);
+        harness.SchedulerFor(actorId).Cancelled.ShouldBeEmpty();
+        harness.Candidates().ShouldHaveSingleItem();
+
+        harness.CoordinatorStore.FailDispositionWrites = false;
+        ReminderConvergenceResult released = await harness.CreateRegistrar().ConvergeAsync(Item);
+
+        released.Cancelled.ShouldBe(1);
+        harness.ItemState(actorId).ShouldBeNull();
+        harness.SchedulerFor(actorId).Armed.ShouldBeEmpty();
+        harness.Candidates().ShouldBeEmpty();
+    }
+
+    /// <summary>An obsolete witness stays durable while Scheduler cancellation is unavailable.</summary>
+    [Fact]
+    public async Task ObsoleteWitnessIsRetainedWhenCancellationFails()
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
+        string name = ReminderTestHarness.Name(intent);
+        harness.Source.Set(Item, intent);
+        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
+        harness.Source.Set(Item);
+        harness.SchedulerFor(actorId).CancelFailure = new InvalidOperationException("Synthetic Scheduler outage.");
+
+        ReminderConvergenceResult retained = await harness.CreateRegistrar().ConvergeAsync(Item);
+
+        retained.Cancelled.ShouldBe(0);
+        ReminderEntry entry = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
+        entry.LastReasonCode.ShouldBe("cancel-failed");
+        harness.Candidates().ShouldHaveSingleItem();
+
+        harness.SchedulerFor(actorId).CancelFailure = null;
+        ReminderConvergenceResult released = await harness.CreateRegistrar().ConvergeAsync(Item);
+
+        released.Cancelled.ShouldBe(1);
+        harness.ItemState(actorId).ShouldBeNull();
+        harness.Candidates().ShouldBeEmpty();
+    }
+
     /// <summary>A scheduler outage leaves the persisted, indexed witness pending and the item unresolved until re-armed.</summary>
     [Fact]
     public async Task PendingStateSurvivesSchedulerFailure()
@@ -119,6 +180,37 @@ public sealed class ReminderCoordinatorTests
         harness.Status.Snapshot().Unresolved.ShouldBe(0);
     }
 
+    /// <summary>An armed reminder whose registration audit failed stays pending until that audit can be retried.</summary>
+    [Fact]
+    public async Task RegisteredAuditFailureKeepsArmedWitnessUnresolved()
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
+        string name = ReminderTestHarness.Name(intent);
+        harness.Source.Set(Item, intent);
+        harness.CoordinatorStore.FailDispositionWrites = true;
+
+        ReminderConvergenceResult retained = await harness.CreateRegistrar().ConvergeAsync(Item);
+
+        retained.Armed.ShouldBe(1);
+        retained.Unresolved.ShouldBe(1);
+        ReminderEntry pending = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
+        pending.Status.ShouldBe(ReminderEntryStatus.Pending);
+        pending.LastReasonCode.ShouldBe("audit-unavailable");
+        harness.SchedulerFor(actorId).Armed.ShouldContainKey(name);
+        harness.Disposition(actorId, name).ShouldBeNull();
+        harness.Candidates().ShouldHaveSingleItem();
+
+        harness.CoordinatorStore.FailDispositionWrites = false;
+        ReminderConvergenceResult recovered = await harness.CreateRegistrar().ConvergeAsync(Item);
+
+        recovered.Armed.ShouldBe(1);
+        recovered.Unresolved.ShouldBe(0);
+        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().Status.ShouldBe(ReminderEntryStatus.Armed);
+        harness.Disposition(actorId, name).ShouldNotBeNull().Disposition.ShouldBe(ReminderDisposition.Registered);
+    }
+
     /// <summary>When the stream no longer holds an intent, the state is released, the reminder cancelled, and the index entry removed last.</summary>
     [Fact]
     public async Task RemovedIntentReleasesStateThenIndex()
@@ -170,7 +262,7 @@ public sealed class ReminderCoordinatorTests
         audit.TargetDisposition.ShouldBe(TrustedEffectDisposition.Success);
         audit.Replayed.ShouldBeFalse();
         harness.ItemState(actorId).ShouldBeNull();
-        harness.Candidates().ShouldBeEmpty();
+        harness.Candidates().ShouldHaveSingleItem();
         harness.SchedulerFor(actorId).Cancelled.ShouldContain(name);
     }
 
@@ -230,7 +322,74 @@ public sealed class ReminderCoordinatorTests
         audit.EffectId.ShouldBe(harness.Submitter.Receipts.Keys.Single());
         harness.Submitter.Receipts.Count.ShouldBe(1);
         harness.ItemState(actorId).ShouldBeNull();
-        harness.Candidates().ShouldBeEmpty();
+        harness.Candidates().ShouldHaveSingleItem();
+    }
+
+    /// <summary>A callback keeps a submitted receipt unresolved until its Scheduler reminder is cancelled.</summary>
+    [Fact]
+    public async Task SubmittedCallbackWaitsForCancellationBeforeRelease()
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
+        string name = ReminderTestHarness.Name(intent);
+        harness.Source.Set(Item, intent);
+        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
+        harness.Time.Advance(TimeSpan.FromHours(1));
+        harness.SchedulerFor(actorId).CancelFailure = new InvalidOperationException("Synthetic Scheduler outage.");
+
+        ReminderDisposition? retained = await harness.FireAsync(actorId, name);
+
+        retained.ShouldBe(ReminderDisposition.Retrying);
+        harness.Submitter.Receipts.Count.ShouldBe(1);
+        ReminderEntry retrying = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
+        retrying.Status.ShouldBe(ReminderEntryStatus.Retrying);
+        retrying.LastReasonCode.ShouldBe("cancel-failed");
+        harness.SchedulerFor(actorId).Armed.ShouldContainKey(name);
+        harness.Candidates().ShouldHaveSingleItem();
+        harness.Status.Snapshot().Unresolved.ShouldBe(1);
+
+        harness.SchedulerFor(actorId).CancelFailure = null;
+        harness.Time.Advance(TimeSpan.FromSeconds(31));
+        ReminderDisposition? released = await harness.FireAsync(actorId, name);
+
+        released.ShouldBe(ReminderDisposition.Submitted);
+        harness.Disposition(actorId, name).ShouldNotBeNull().Replayed.ShouldBeTrue();
+        harness.ItemState(actorId).ShouldBeNull();
+        harness.SchedulerFor(actorId).Armed.ShouldBeEmpty();
+        harness.Candidates().ShouldHaveSingleItem();
+        harness.Status.Snapshot().Unresolved.ShouldBe(0);
+    }
+
+    /// <summary>Convergence also retains a submitted receipt until Scheduler cancellation recovers.</summary>
+    [Fact]
+    public async Task SubmittedConvergenceWaitsForCancellationBeforeRelease()
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddMinutes(-1));
+        string name = ReminderTestHarness.Name(intent);
+        harness.Source.Set(Item, intent);
+        harness.SchedulerFor(actorId).CancelFailure = new InvalidOperationException("Synthetic Scheduler outage.");
+
+        ReminderConvergenceResult retained = await harness.CreateRegistrar().ConvergeAsync(Item);
+
+        retained.Submitted.ShouldBe(0);
+        retained.Unresolved.ShouldBe(1);
+        harness.Submitter.Receipts.Count.ShouldBe(1);
+        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().LastReasonCode.ShouldBe("cancel-failed");
+        harness.SchedulerFor(actorId).Armed.ShouldContainKey(name);
+        harness.Candidates().ShouldHaveSingleItem();
+
+        harness.SchedulerFor(actorId).CancelFailure = null;
+        harness.Time.Advance(TimeSpan.FromSeconds(31));
+        ReminderConvergenceResult released = await harness.CreateRegistrar().ConvergeAsync(Item);
+
+        released.Submitted.ShouldBe(1);
+        harness.Disposition(actorId, name).ShouldNotBeNull().Replayed.ShouldBeTrue();
+        harness.ItemState(actorId).ShouldBeNull();
+        harness.SchedulerFor(actorId).Armed.ShouldBeEmpty();
+        harness.Candidates().ShouldHaveSingleItem();
     }
 
     /// <summary>An uncertain receipt keeps the witness, re-arms a backoff reminder, and is never acknowledged by throwing.</summary>
@@ -259,11 +418,21 @@ public sealed class ReminderCoordinatorTests
         harness.Candidates().ShouldHaveSingleItem();
         harness.Status.Snapshot().Unresolved.ShouldBe(1);
 
+        ReminderDisposition? duplicate = await harness.FireAsync(actorId, name);
+
+        duplicate.ShouldBe(ReminderDisposition.Retrying);
+        harness.Submitter.Calls.Count.ShouldBe(1);
+        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().Attempts.ShouldBe(1);
+        harness.SchedulerFor(actorId).Armed[name].DueTime.ShouldBe(TimeSpan.FromSeconds(30));
+
+        harness.Time.Advance(TimeSpan.FromSeconds(31));
         _ = await harness.FireAsync(actorId, name);
         harness.SchedulerFor(actorId).Armed[name].DueTime.ShouldBe(TimeSpan.FromSeconds(60));
+        harness.Time.Advance(TimeSpan.FromSeconds(61));
         _ = await harness.FireAsync(actorId, name);
         harness.SchedulerFor(actorId).Armed[name].DueTime.ShouldBe(TimeSpan.FromSeconds(120));
 
+        harness.Time.Advance(TimeSpan.FromSeconds(121));
         ReminderDisposition? settled = await harness.FireAsync(actorId, name);
 
         settled.ShouldBe(ReminderDisposition.Submitted);
@@ -296,6 +465,103 @@ public sealed class ReminderCoordinatorTests
         harness.Status.Snapshot().Unresolved.ShouldBe(1);
     }
 
+    /// <summary>A blank workload is denied before delegation or submission, and the witness stays retained.</summary>
+    [Fact]
+    public async Task BlankWorkloadIsDeniedAndRetained()
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
+        harness.Source.Set(Item, intent);
+        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
+        harness.Options.Workload = " ";
+        harness.Time.Advance(TimeSpan.FromHours(2));
+
+        ReminderDisposition? disposition = await harness.FireAsync(actorId, ReminderTestHarness.Name(intent));
+
+        disposition.ShouldBe(ReminderDisposition.Denied);
+        harness.Submitter.Calls.ShouldBeEmpty();
+        ReminderEntry retained = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
+        retained.Status.ShouldBe(ReminderEntryStatus.Retrying);
+        retained.LastReasonCode.ShouldBe("workload-unconfigured");
+    }
+
+    /// <summary>A null fold is not an empty stream: stored reminders stay armed.</summary>
+    [Fact]
+    public async Task NullIntentSourceDoesNotCancelStoredReminders()
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
+        string name = ReminderTestHarness.Name(intent);
+        harness.Source.Set(Item, intent);
+        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
+        harness.Source.ReturnNull = true;
+
+        ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);
+        ReminderDisposition? callback = await harness.FireAsync(actorId, name);
+
+        result.Cancelled.ShouldBe(0);
+        callback.ShouldBe(ReminderDisposition.Retrying);
+        harness.SchedulerFor(actorId).Cancelled.ShouldBeEmpty();
+        ReminderEntry retained = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
+        retained.ReminderName.ShouldBe(name);
+        retained.LastReasonCode.ShouldBe("source-unavailable");
+    }
+
+    /// <summary>A blank domain is rejected before it can be stored.</summary>
+    [Fact]
+    public async Task BlankTargetDomainIsRejected()
+    {
+        var harness = new ReminderTestHarness();
+        var blank = new ReminderTarget(ReminderTestHarness.Tenant, " ", "item-1");
+        harness.Source.Set(blank, ReminderTestHarness.Intent(blank, harness.Time.Now.AddHours(2)));
+
+        ArgumentException failure = await Should.ThrowAsync<ArgumentException>(
+            () => harness.CreateRegistrar().ConvergeAsync(blank));
+
+        failure.ParamName.ShouldBe("target");
+        harness.ItemState(ReminderTestHarness.ActorId(blank)).ShouldBeNull();
+        harness.Candidates().ShouldBeEmpty();
+    }
+
+    /// <summary>A restored blank domain is quarantined instead of being retired as a stale witness.</summary>
+    [Fact]
+    public async Task BlankStoredDomainIsQuarantined()
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
+        string name = ReminderTestHarness.Name(intent);
+        harness.Source.Set(Item, intent);
+        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
+        ReminderItemState stored = harness.ItemState(actorId).ShouldNotBeNull();
+        harness.SeedItemState(actorId, stored with { Domain = " " });
+        harness.Time.Advance(TimeSpan.FromHours(1));
+
+        ReminderDisposition? disposition = await harness.FireAsync(actorId, name);
+
+        disposition.ShouldBe(ReminderDisposition.Quarantined);
+        harness.Submitter.Calls.ShouldBeEmpty();
+        harness.Disposition(actorId, name).ShouldNotBeNull().Disposition.ShouldNotBe(ReminderDisposition.Stale);
+        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().LastReasonCode.ShouldBe("domain-invalid");
+    }
+
+    /// <summary>A null persisted candidate list does not throw when a candidate is removed.</summary>
+    [Fact]
+    public async Task NullCandidateListIsIgnoredOnRemoval()
+    {
+        var harness = new ReminderTestHarness();
+        harness.Store.SeedRaw(
+            harness.Options.StateStoreName,
+            ReminderStateKeys.TenantCandidates(harness.Options.ActorTypeName, ReminderTestHarness.Tenant),
+            new ReminderTenantCandidates(ReminderTestHarness.Tenant, null!));
+
+        await harness.CreateIndex().RemoveCandidateAsync(Item, ReminderTestHarness.ActorId(Item), CancellationToken.None);
+
+        harness.Candidates().ShouldBeEmpty();
+    }
+
     /// <summary>Concurrent registrations from two hosts serialize in the actor turn and produce one registration.</summary>
     [Fact]
     public async Task TwoHostsRacingRegisterOnce()
@@ -415,6 +681,36 @@ public sealed class ReminderCoordinatorTests
         harness.SchedulerFor(actorId).Armed.ShouldBeEmpty();
     }
 
+    /// <summary>A quarantine audit outage retains the original witness and does not cancel its Scheduler reminder.</summary>
+    [Fact]
+    public async Task QuarantineAuditFailureRetainsOriginalWitness()
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        ReminderIntent original = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2), sequence: 3);
+        string name = ReminderTestHarness.Name(original);
+        harness.Source.Set(Item, original);
+        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
+        harness.Source.Set(Item, original with { SourceSequence = 9 });
+        harness.CoordinatorStore.FailDispositionWrites = true;
+
+        ReminderConvergenceResult retained = await harness.CreateRegistrar().ConvergeAsync(Item);
+
+        retained.Unresolved.ShouldBe(1);
+        ReminderEntry entry = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
+        entry.Status.ShouldBe(ReminderEntryStatus.Retrying);
+        entry.LastReasonCode.ShouldBe("audit-unavailable");
+        harness.SchedulerFor(actorId).Armed.ShouldContainKey(name);
+        harness.SchedulerFor(actorId).Cancelled.ShouldBeEmpty();
+
+        harness.CoordinatorStore.FailDispositionWrites = false;
+        ReminderConvergenceResult quarantined = await harness.CreateRegistrar().ConvergeAsync(Item);
+
+        quarantined.Quarantined.ShouldBe(2);
+        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().Status.ShouldBe(ReminderEntryStatus.Quarantined);
+        harness.SchedulerFor(actorId).Armed.ShouldBeEmpty();
+    }
+
     /// <summary>Due retained work waits out its backoff window on every pass, then is resubmitted after it.</summary>
     [Fact]
     public async Task RetryingWorkWaitsOutBackoffAcrossPasses()
@@ -444,6 +740,29 @@ public sealed class ReminderCoordinatorTests
         harness.ItemState(actorId).ShouldBeNull();
     }
 
+    /// <summary>A retrying witness whose backoff reminder was lost is re-armed without an early submission.</summary>
+    [Fact]
+    public async Task LostRetryReminderIsRearmedDuringBackoff()
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddMinutes(-1));
+        string name = ReminderTestHarness.Name(intent);
+        harness.Source.Set(Item, intent);
+        harness.Submitter.FailuresRemaining = 1;
+        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
+        harness.SchedulerFor(actorId).Lose(name);
+        harness.Time.Advance(TimeSpan.FromSeconds(10));
+
+        ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);
+
+        result.Submitted.ShouldBe(0);
+        result.Armed.ShouldBe(1);
+        harness.Submitter.Calls.Count.ShouldBe(1);
+        harness.SchedulerFor(actorId).Armed[name].DueTime.ShouldBe(TimeSpan.FromSeconds(20));
+        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().Status.ShouldBe(ReminderEntryStatus.Retrying);
+    }
+
     /// <summary>An armed reminder the scheduler still holds is not re-registered by a later convergence.</summary>
     [Fact]
     public async Task HeldArmedReminderIsNotRearmed()
@@ -459,6 +778,39 @@ public sealed class ReminderCoordinatorTests
         harness.SchedulerFor(actorId).ArmCalls.ShouldBe(1);
     }
 
+    /// <summary>An inconclusive Scheduler lookup re-registers the deterministic reminder name.</summary>
+    [Fact]
+    public async Task SchedulerLookupFailureRearmsFutureWitness()
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
+        harness.Source.Set(Item, intent);
+        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
+        harness.SchedulerFor(actorId).LookupFailure = new HttpRequestException("Synthetic lookup outage.");
+
+        ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);
+
+        result.Armed.ShouldBe(1);
+        harness.SchedulerFor(actorId).ArmCalls.ShouldBe(2);
+        harness.SchedulerFor(actorId).Armed.ShouldContainKey(ReminderTestHarness.Name(intent));
+    }
+
+    /// <summary>The Scheduler due time is calculated from a fresh clock after the authoritative stream fold.</summary>
+    [Fact]
+    public async Task FutureIntentUsesFreshClockWhenArmed()
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
+        harness.Source.Set(Item, intent);
+        harness.Source.OnRead = _ => harness.Time.Advance(TimeSpan.FromHours(1));
+
+        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
+
+        harness.SchedulerFor(actorId).Armed[ReminderTestHarness.Name(intent)].DueTime.ShouldBe(TimeSpan.FromHours(1));
+    }
+
     /// <summary>Item state that holds work but lost its index entry, as after an older index restore, is re-indexed.</summary>
     [Fact]
     public async Task MissingIndexEntryIsRestored()
@@ -478,6 +830,148 @@ public sealed class ReminderCoordinatorTests
         harness.Candidates().ShouldBe([new ReminderCandidate(Item.Domain, Item.Aggregate, actorId)]);
     }
 
+    /// <summary>The fail-closed catch retries discovery for existing unindexed state before acknowledging it.</summary>
+    [Fact]
+    public async Task FailClosedCatchRestoresDiscoveryAfterIndexConflict()
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
+        harness.Source.Set(Item, intent);
+        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
+        harness.Store.SeedRaw(
+            harness.Options.StateStoreName,
+            ReminderStateKeys.TenantCandidates(harness.Options.ActorTypeName, ReminderTestHarness.Tenant),
+            new ReminderTenantCandidates(ReminderTestHarness.Tenant, []));
+        harness.Options.IndexWriteAttempts = 1;
+        bool conflicted = false;
+        harness.Store.ConcurrentWriteBeforeTrySave = () =>
+        {
+            if (conflicted)
+            {
+                return;
+            }
+
+            conflicted = true;
+            harness.Store.ConcurrentWriteBeforeTrySave = null;
+            harness.Store.SeedRaw(
+                harness.Options.StateStoreName,
+                ReminderStateKeys.TenantCandidates(harness.Options.ActorTypeName, ReminderTestHarness.Tenant),
+                new ReminderTenantCandidates(ReminderTestHarness.Tenant, []));
+        };
+
+        ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);
+
+        conflicted.ShouldBeTrue();
+        result.Unresolved.ShouldBe(1);
+        harness.Candidates().ShouldBe([new ReminderCandidate(Item.Domain, Item.Aggregate, actorId)]);
+        harness.ItemState(actorId).ShouldNotBeNull();
+        harness.SchedulerFor(actorId).ArmCalls.ShouldBe(1);
+    }
+
+    /// <summary>An actor collision re-indexes the restored state under its stored target, not the colliding caller.</summary>
+    [Fact]
+    public async Task ActorCollisionRestoresStoredTargetIndex()
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        var foreign = new ReminderItemState(Item.Tenant, "other-domain", Item.Aggregate, 3, [], []);
+        harness.SeedItemState(actorId, foreign);
+        harness.Source.Set(Item, ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2)));
+
+        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
+
+        harness.Candidates().ShouldBe([new ReminderCandidate("other-domain", Item.Aggregate, actorId)]);
+    }
+
+    /// <summary>Malformed persisted collection elements become durable quarantine instead of terminating convergence.</summary>
+    [Fact]
+    public async Task MalformedPersistedCollectionElementsAreQuarantined()
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
+        harness.SeedItemState(actorId, new ReminderItemState(
+            Item.Tenant,
+            Item.Domain,
+            Item.Aggregate,
+            3,
+            [null!],
+            [null!]));
+        harness.Source.Set(Item, intent);
+
+        ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);
+
+        result.Armed.ShouldBe(1);
+        result.Quarantined.ShouldBe(2);
+        ReminderItemState state = harness.ItemState(actorId).ShouldNotBeNull();
+        state.Entries.ShouldHaveSingleItem().ReminderName.ShouldBe(ReminderTestHarness.Name(intent));
+        state.Quarantine.Select(static record => record.ReasonCode).Order().ShouldBe([
+            "stored-entry-invalid",
+            "stored-quarantine-invalid",
+        ]);
+        state.Quarantine.ShouldAllBe(record => harness.Disposition(actorId, record.EvidenceDigest) != null);
+        harness.Candidates().ShouldHaveSingleItem();
+    }
+
+    /// <summary>Duplicate persisted reminder identities become audited quarantine before any execution.</summary>
+    [Fact]
+    public async Task DuplicatePersistedReminderIdentitiesAreQuarantined()
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
+        string name = ReminderTestHarness.Name(intent);
+        harness.Source.Set(Item, intent);
+        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
+        ReminderItemState stored = harness.ItemState(actorId).ShouldNotBeNull();
+        ReminderEntry entry = stored.Entries.ShouldHaveSingleItem();
+        harness.SeedItemState(actorId, stored with { Entries = [entry, entry] });
+
+        ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);
+
+        result.Armed.ShouldBe(0);
+        result.Submitted.ShouldBe(0);
+        result.Quarantined.ShouldBe(2);
+        ReminderItemState quarantined = harness.ItemState(actorId).ShouldNotBeNull();
+        quarantined.Entries.ShouldBeEmpty();
+        quarantined.Quarantine.Count.ShouldBe(2);
+        quarantined.Quarantine.ShouldAllBe(record => record.ReasonCode == "stored-entry-duplicate" && record.ReminderName == name);
+        quarantined.Quarantine.ShouldAllBe(record =>
+            harness.Disposition(actorId, record.EvidenceDigest) != null
+            && harness.Disposition(actorId, record.EvidenceDigest)!.Disposition == ReminderDisposition.Quarantined);
+        harness.Submitter.Calls.ShouldBeEmpty();
+        harness.SchedulerFor(actorId).Armed.ShouldNotContainKey(name);
+        harness.Candidates().ShouldHaveSingleItem();
+    }
+
+    /// <summary>A fail-closed error after a durable write reports the reloaded state rather than the turn's stale snapshot.</summary>
+    [Fact]
+    public async Task ExistingStateFailClosedUsesReloadedSnapshot()
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        ReminderIntent colliding = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1), sequence: 3);
+        ReminderIntent due = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2), revision: 2, sequence: 4);
+        harness.Source.Set(Item, colliding, due);
+        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
+        harness.Source.Set(Item, colliding with { SourceSequence = 9 }, due);
+        harness.Time.Advance(TimeSpan.FromHours(3));
+        string itemKey = ReminderStateKeys.Item(harness.Options.ActorTypeName, actorId);
+        int itemWrites = 0;
+        harness.CoordinatorStore.RejectTrySave = key => string.Equals(key, itemKey, StringComparison.Ordinal)
+            && ++itemWrites == 2;
+
+        ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);
+
+        result.Unresolved.ShouldBe(1);
+        result.Quarantined.ShouldBe(2);
+        ReminderItemState state = harness.ItemState(actorId).ShouldNotBeNull();
+        state.Entries.Count(static entry => entry.Status != ReminderEntryStatus.Quarantined).ShouldBe(1);
+        state.Entries.Count(static entry => entry.Status == ReminderEntryStatus.Quarantined).ShouldBe(1);
+        state.Quarantine.ShouldHaveSingleItem();
+    }
+
     /// <summary>Without a durable audit record a durable receipt does not release the witness; it is retried after backoff.</summary>
     [Fact]
     public async Task UnwritableAuditKeepsReceiptWitnessRetrying()
@@ -566,7 +1060,7 @@ public sealed class ReminderCoordinatorTests
         audit.Disposition.ShouldBe(ReminderDisposition.Submitted);
         audit.TargetDisposition.ShouldBe(targetDisposition);
         harness.ItemState(actorId).ShouldBeNull();
-        harness.Candidates().ShouldBeEmpty();
+        harness.Candidates().ShouldHaveSingleItem();
     }
 
     /// <summary>A complete pass prunes items it no longer discovers from readiness; an incomplete pass prunes nothing.</summary>
diff --git a/tests/Hexalith.EventStore.DomainService.Tests/ReminderReconcilerTests.cs b/tests/Hexalith.EventStore.DomainService.Tests/ReminderReconcilerTests.cs
index 2d3d7c9bce8bb18e34a913fe22f8698c6fd3fe44..1ffe44e947c8d11482c415b91e2fdb55b352361f 100644
--- a/tests/Hexalith.EventStore.DomainService.Tests/ReminderReconcilerTests.cs
+++ b/tests/Hexalith.EventStore.DomainService.Tests/ReminderReconcilerTests.cs
@@ -1,3 +1,4 @@
+using Hexalith.EventStore.Client.Reminders;
 using Hexalith.EventStore.Contracts.Reminders;
 using Hexalith.EventStore.DomainService.Tests.Fixtures;
 
@@ -39,7 +40,82 @@ public sealed class ReminderReconcilerTests
         harness.Submitter.Receipts.Count.ShouldBe(1);
         harness.Disposition(actorId, ReminderTestHarness.Name(intent)).ShouldNotBeNull().Disposition.ShouldBe(ReminderDisposition.Submitted);
         harness.ItemState(actorId).ShouldBeNull();
+        harness.Candidates().ShouldHaveSingleItem();
+        (await CheckAsync(harness)).Status.ShouldBe(HealthStatus.Healthy);
+    }
+
+    /// <summary>An unavailable cleanup fold retains discovery and degrades readiness until a later pass succeeds.</summary>
+    [Theory]
+    [InlineData("callback", false)]
+    [InlineData("callback", true)]
+    [InlineData("convergence", false)]
+    [InlineData("convergence", true)]
+    [InlineData("stale-callback", false)]
+    [InlineData("stale-callback", true)]
+    public async Task UnavailableCleanupFoldRetainsUnresolvedReadiness(string operation, bool throws)
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
+        string name = ReminderTestHarness.Name(intent);
+        harness.Source.Set(Item, intent);
+        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
+        harness.Status.CompletePass(harness.Time.Now, 0, [actorId]);
+        (await CheckAsync(harness)).Status.ShouldBe(HealthStatus.Healthy);
+        harness.Time.Advance(TimeSpan.FromHours(1));
+        if (operation == "stale-callback")
+        {
+            harness.Source.Set(Item);
+        }
+
+        int cleanupRead = harness.Source.Reads + 2;
+        harness.Source.OnRead = reads =>
+        {
+            if (reads == cleanupRead)
+            {
+                if (throws)
+                {
+                    _ = harness.Source.Failing.Add(Item);
+                }
+                else
+                {
+                    harness.Source.ReturnNull = true;
+                }
+            }
+        };
+
+        if (operation == "convergence")
+        {
+            ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);
+            result.Submitted.ShouldBe(1);
+            result.Unresolved.ShouldBe(1);
+        }
+        else
+        {
+            (await harness.FireAsync(actorId, name)).ShouldBe(operation == "stale-callback"
+                ? ReminderDisposition.Stale
+                : ReminderDisposition.Submitted);
+        }
+
+        harness.ItemState(actorId).ShouldBeNull();
+        harness.SchedulerFor(actorId).Armed.ShouldBeEmpty();
+        harness.Candidates().ShouldHaveSingleItem().ActorId.ShouldBe(actorId);
+        harness.Submitter.Receipts.Count.ShouldBe(operation == "stale-callback" ? 0 : 1);
+        harness.Status.Snapshot().Unresolved.ShouldBe(1);
+        HealthCheckResult retained = await CheckAsync(harness);
+        retained.Status.ShouldBe(HealthStatus.Degraded);
+        retained.Data["unresolved"].ShouldBe(1);
+
+        harness.Source.OnRead = null;
+        harness.Source.ReturnNull = false;
+        harness.Source.Failing.Clear();
+        harness.Source.Set(Item);
+        ReminderReconciliationPass recovered = await harness.CreateReconciler().RunPassAsync(CancellationToken.None);
+
+        recovered.Unresolved.ShouldBe(0);
+        recovered.Incomplete.ShouldBe(0);
         harness.Candidates().ShouldBeEmpty();
+        harness.Status.Snapshot().Unresolved.ShouldBe(0);
         (await CheckAsync(harness)).Status.ShouldBe(HealthStatus.Healthy);
     }
 
@@ -95,8 +171,9 @@ public sealed class ReminderReconcilerTests
         ReminderReconciliationPass complete = await harness.CreateReconciler().RunPassAsync(CancellationToken.None);
 
         complete.Incomplete.ShouldBe(0);
-        complete.Submitted.ShouldBe(1);
-        harness.Candidates("tenant-b").ShouldBeEmpty();
+        // The fake source still reports both intents, so the recovered item submits and the healthy item submits again.
+        complete.Submitted.ShouldBe(2);
+        harness.Candidates("tenant-b").ShouldHaveSingleItem();
         (await CheckAsync(harness)).Status.ShouldBe(HealthStatus.Healthy);
     }
 
@@ -120,6 +197,42 @@ public sealed class ReminderReconcilerTests
         harness.Source.Reads.ShouldBe(1);
     }
 
+    /// <summary>Null and actor-mismatched candidates are skipped independently without invoking their registrar.</summary>
+    [Fact]
+    public async Task CorruptCandidatesDoNotInvokeRegistrarAndPassContinues()
+    {
+        var harness = new ReminderTestHarness();
+        string actorId = ReminderTestHarness.ActorId(Item);
+        harness.Store.SeedRaw(
+            harness.Options.StateStoreName,
+            ReminderStateKeys.TenantRegistry(harness.Options.ActorTypeName),
+            new ReminderTenantRegistry([ReminderTestHarness.Tenant]));
+        harness.Store.SeedRaw(
+            harness.Options.StateStoreName,
+            ReminderStateKeys.TenantCandidates(harness.Options.ActorTypeName, ReminderTestHarness.Tenant),
+            new ReminderTenantCandidates(ReminderTestHarness.Tenant, [
+                null!,
+                new ReminderCandidate(Item.Domain, Item.Aggregate, "wra-MISMATCH"),
+                new ReminderCandidate(Item.Domain, Item.Aggregate, actorId),
+            ]));
+        IReminderRegistrar registrar = Substitute.For<IReminderRegistrar>();
+        registrar.ConvergeAsync(Item, Arg.Any<CancellationToken>())
+            .Returns(new ReminderConvergenceResult(1, 0, 0, 0, 0));
+        using var reconciler = new ReminderReconciler(
+            harness.CreateIndex(),
+            registrar,
+            harness.Status,
+            Options.Create(harness.Options),
+            harness.Time,
+            NullLogger<ReminderReconciler>.Instance);
+
+        ReminderReconciliationPass pass = await reconciler.RunPassAsync(CancellationToken.None);
+
+        pass.ShouldBe(new ReminderReconciliationPass(1, 3, 1, 0, 0, 0, 0, 2));
+        _ = registrar.Received(1).ConvergeAsync(Item, Arg.Any<CancellationToken>());
+        registrar.ReceivedCalls().Count().ShouldBe(1);
+    }
+
     /// <summary>After a restart readiness stays Degraded until the first pass rebuilds it from durable state.</summary>
     [Fact]
     public async Task RestartRebuildsReadinessFromDurableState()
@@ -227,6 +340,36 @@ public sealed class ReminderReconcilerTests
         harness.Status.Snapshot().PassCompleted.ShouldBeTrue();
     }
 
+    /// <summary>An incomplete hosted pass retries on the short retry cadence instead of the normal interval.</summary>
+    [Fact]
+    public async Task IncompleteHostedPassUsesRetryCadence()
+    {
+        var harness = new ReminderTestHarness();
+        harness.Options.RetryInitialDelay = TimeSpan.FromMilliseconds(20);
+        harness.Options.ReconciliationInterval = TimeSpan.FromHours(1);
+        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
+        harness.Source.Set(Item, intent);
+        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
+        harness.Source.Failing.Add(Item);
+        int expectedReads = harness.Source.Reads + 2;
+        var retried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
+        harness.Source.OnRead = reads =>
+        {
+            if (reads >= expectedReads)
+            {
+                _ = retried.TrySetResult();
+            }
+        };
+        using ReminderReconciler reconciler = harness.CreateReconciler(timeProvider: TimeProvider.System);
+
+        await reconciler.StartAsync(CancellationToken.None);
+        await retried.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
+        await reconciler.StopAsync(CancellationToken.None);
+
+        harness.Source.Reads.ShouldBeGreaterThanOrEqualTo(expectedReads);
+        harness.Status.Snapshot().IncompleteScans.ShouldBe(1);
+    }
+
     private static Task<HealthCheckResult> CheckAsync(
         ReminderTestHarness harness,
         ReminderRuntimeStatus? status = null,
`. Read that file — it is the content under review.

Do not invoke any skill, and do not spawn subagents of your own — you are the reviewer. Return your findings as text in your final message; do not route them through any findings-reporting tool the host may offer.