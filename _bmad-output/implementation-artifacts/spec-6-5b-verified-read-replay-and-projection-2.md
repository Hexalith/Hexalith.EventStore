---
title: 'Story 6.5b: Verified Read, Replay, and Projection Candidate'
type: 'feature'
created: '2026-09-27'
status: 'done'
baseline_commit: '68492519b868899e6ab6bf64931f19f0cb1ca6a7'
route: 'dispatch'
review_loop_iteration: 2
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 6.5 leaves timeline continuity, combined memory admission, asynchronous successor ownership and read-compatibility rules unresolved across event consumers.

**Approach:** Extend the existing 6.5b work-story with a source-grounded section candidate for the single AD-13 artifact, resolving `BH37-3`, `BH37-4`, `BH37-5` and `BH37-10` through exact contracts and verification vectors.

## Boundaries & Constraints

**Always:** Preserve stored bytes, identity, offsets, protection, public compatibility and last-good state. Adapt once from authenticated source evidence. Distinguish existing behavior, proposed contracts and future provider tests. Preserve existing workspace edits.

**Never:** Change runtime/tests, historical triage, 6.5a, epic context, the normative artifact or its signed fixtures/`UNAPPROVED` receipt; authorize 6.6; skip poison history or publish partial authoritative state. Epic 8 is not a prerequisite.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Mixed history | Same admitted prefix across consumers | Same effective meaning, adapted once | Typed failure preserves last-good state |
| Paged timeline | Multiple pages, retry or restart | Authenticated private accumulation; final-only public timeline | No partial success or repeated Apply |
| Memory boundary | 64 MiB event plus 64 MiB prior state | Reserve every live capacity before allocation | Bounded execution or explicit hold before Apply |
| Async successor | Caller mutation, cancellation or lost acknowledgement | Persist exactly privately sealed validated bytes | Reconcile without another Apply |
| Compatibility | Write-only F change versus read/Apply drift | Separate attestation refresh from semantic replay | Stale proof or unsupported rollback holds |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` §§2–6, 8–11: reuse proposed contracts, budgets and retention; §12 stays unapproved. These seams are not implemented.
- `_bmad-output/implementation-artifacts/story-6-5-review-triage.md:548` and `_bmad-output/implementation-artifacts/story-6-5-design-notes.md:104`: four findings and contradictory F wording; supersede in the candidate without rewriting history.
- `_bmad-output/implementation-artifacts/spec-6-5a-event-contract-writer-and-migration-evidence.md` A3–A6: consume limits and codec-02 intent, receipt **and bundle-readback hash**; separate fresh source observations from historical committed generations.
- `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs:21` and `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs`: typed reads/free snapshots precede raw proof; inventory actor/coordinated-command callers too.
- `src/Hexalith.EventStore.Client/Aggregates/AggregateReplayer.cs:25` and `src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs`: synchronous CLR Apply, whole arrays and mutable failure state; map router/reconstruction paths.
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:123`: whole history, wire unprotection and dispatch; inventory live/retry/history/shared rebuild and durable completion.
- `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs:50`: existing marker-gated reads lack named-route proof; retain legacy semantics while specifying scoped root visibility.
- `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs:102` and `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs`: export and inspection bypasses; include stream/replay/trace routes and safe diagnostics.

## Tasks & Acceptance

All execution tasks edit `_bmad-output/implementation-artifacts/spec-6-5b-verified-read-replay-and-projection.md`:

- [x] Inventory every source, unprotect, Apply, projection, query, reconstruction, backup and inspection seam and its existing tests; distinguish current from proposed behavior.
- [x] Specify authenticated source/effective views, aliases/hops, route scope, complete-prefix paging, snapshot recovery, named-root/checkpoint proof, typed failures and cancellation. Include zero reads/handler calls/state writes/checkpoint advancement for an already-current projection and explicit full-replay versus incremental handler capability.
- [x] Define authenticated private timeline accumulation with atomic successor/ledger binding, exclusive successor ownership before asynchronous save, compositional preallocation budgets, exact retry, retention and capacity holds. Reconcile 6.5a evidence and write-only F semantics explicitly.
- [x] Record four accepted/rejected dispositions with section/vector links. Add executable local arithmetic/codec/ownership-model checks for the matrix and precise future production vectors; never claim a model proves provider behavior.

**Acceptance Criteria:**
- Given current sources and the four findings, when the candidate is reviewed, then every proposed rule has a source or draft anchor and each finding has a testable disposition.
- Given paging, tampering, mutable buffers, oversized input or cancellation, when the vectors are applied, then adaptation, failure, memory admission and durable progress have deterministic outcomes without partial authority.
- Given integration with 6.5a/6.5c, when the candidate is handed off, then replacements and shared contracts are explicit while AD-13 remains unapproved and 6.6 unauthorized.

## Implementation Notes

2026-09-27: Implemented the [section candidate](spec-6-5b-verified-read-replay-and-projection.md) with B1–B10, four BH37 dispositions and VB-01–VB-14 future production vectors. Parent diff/matrix audit added query-router/cache inventory and LB-06 mixed-history plus LB-07 complete transition/transcript fixtures. All seven embedded local checks ran successfully; they cover all five matrix rows at the explicitly limited model level. Runtime/provider behavior remains future verification. The approved frozen block and protected documents retain their original bytes.

2026-09-27, loop 1: Parent inspected the complete revision against the preserved first candidate and confirmed the four tasks and five approved matrix rows. Independently extracted and ran LB-01–LB-14 (`python3 /tmp/6-5b-parent-loop1-checks.py`, exit 0), ran the link/disposition/vector/protected-file audit (`python3 /tmp/6-5b-loop1-audit.py`, exit 0: 100 links, 19 vectors, 8,011 protected files), and passed `git diff --check`. Separately verified the protected-document hashes and exact frozen block. Sandbox, selector/zero-completion/manifest, signed scalar, prior-root and cache requirements now have deterministic contracts and model/vector evidence. VB-01–VB-19 remain unexecuted runtime/provider work.

2026-09-27, loop 2: Parent inspected the revision against the saved candidate, confirmed all execution tasks and approved matrix rows, and independently ran `python3 /tmp/6-5b-parent-loop2-checks.py` (LB-01–LB-18, exit 0), `python3 /tmp/6-5b-loop2-audit.py` (110 links, 20 vectors, 8,011 protected files, exit 0), frozen-block/protected-document comparisons and `git diff --check`. Query catalog/intake, final TTL, recovery, shared quota and the direct consistency corrections are now specified. VB-01–VB-20 remain future runtime/provider work. The user authorized reuse of idle research agents for remaining reviews when fresh thread capacity is unavailable.

## Spec Change Log


2026-09-27, loop 1: review found insufficient non-frozen guidance for exact replay/manifest/negotiation records, diagnostic sandbox compatibility, incremental prior-root intake and derived query-cache authority/capacity. The candidate was preserved at `/tmp/bmad-6-5b-before-review-1.md` before reverting only that implementation file to the baseline. Restore its sound content, then re-derive the requirements below. The approved frozen block is unchanged.

### Review-loop implementation requirements

- Complete B1/B2's sandbox inventory and diagnostic-only path. Verify persisted base state (or explicitly scoped empty genesis), validate hypothetical domain output through bounded registered schemas, and Apply privately without fabricating persisted event/source proof, command-state authority, checkpoint, append or publication. Preserve the existing sandbox response contract and separate simulation authority from production reads. Add success/failure/cancellation vectors.
- Decide and specify one exact transcript-v2 rule for empty-stream, witnessed zero-tail and max-sequence completion. Define P/state/timeline hashes, count/range/finality and terminal transcript consistently; explicitly replace the older rule where needed and add literal zero-event examples.
- Specify the transcript version selector's exact authenticated fields/codec, endpoint and registry fingerprint bindings, and gateway/receiver/command-verifier negotiation/rollback behavior. No independently guessed version or silent fallback is allowed.
- Define the bounded private timeline manifest's exact canonical codec, logical entry-list and chunk ordering, plaintext/ciphertext hash domains, protection metadata binding, scope/page binding and size/count caps. Specify recovery rejection and add an executable known-answer manifest example. Existing encryption/protection providers remain conditional capabilities, not a new Epic 8 dependency.
- Finish `VerifiedProjectionPriorState`: exact public member types and presence rules, discriminator for bytes versus certified root, and authenticated immutable root-scoped intake/read session. A root advancing concurrently must never mix a newer model with an older admitted tail; name conflict/hold/retry behavior before handler execution and commit.
- Correct versioned caching for arbitrary derived query results. Either prove an exact result binding to authenticated source root(s), query implementation/options/dependencies, parameters, response bytes and current authorization, or bypass caching while executing the authenticated query. A cached result need not be a persisted leaf. Define retained payload/proof byte budgets and safe admission/eviction/release; cache capacity alone must not fail an otherwise valid uncached query. Add query-code-only drift and retained-capacity vectors.
- Explicitly replace the draft's V2 intent/receipt absence rule with the 6.5a-compatible source evidence contract and explain its effect on the otherwise retained raw-proof codec.
- Fix I/N encoding and every relevant decoded I/N field to enforce nonnegative signed-i32/i64 bounds before use. Keep u32 length/count framing distinct. Add negative, max, max+1, decode-overflow fixtures and keep valid literal codec bytes stable unless the specified contract changes.
- Update local-check/model-to-vector mappings and verification evidence for the corrected contracts; keep model limitations explicit. Every proposed failure above needs a deterministic outcome, not merely a future-test label.

KEEP: B1 source-versus-proposed distinction and query-router/cache inventory; complete 6.5a codec-02 evidence and historical-versus-current head separation; B3 complete-prefix/snapshot/zero-hop compatibility rules; final-only timeline and private ownership; B6 composed budgets and retention; B7 zero-work current projection and explicit fold mode; B8 write-only F distinction; four accepted BH37 dispositions; VB-01–VB-14 and LB-01–LB-07 sound checks. Preserve all protected files and original signed fixtures/receipt byte-for-byte. Add only documentation/embedded checks to the candidate, never runtime/tests, historical artifacts or the user epic context.

### Review loop 2: query intake and narrow consistency corrections

2026-09-27: Saved the full sound loop-1 candidate at `/tmp/bmad-6-5b-before-review-2.md`, then reverted only the implementation candidate to baseline. Restore that saved candidate and make the changes below. These requirements supersede earlier incomplete non-frozen guidance only where stated; preserve the approved frozen block and all protected files.

- Define the smallest exact authenticated query catalog record and dispatch mapping required by B7b. Bind domain/query route, exact key-space/root inputs, current implementation/options/schema/dependency closure and endpoint to startup admission. State canonical field framing, ordering/uniqueness, fingerprint participation and code-only drift behavior. Keep historical fixtures byte-identical when the new rows are absent. Do not claim the existing event/projection rows already bind query code.
- Define a concrete dispatcher-installed query intake/read session so every contributing read uses the same admitted immutable root. Reuse existing scoped read/store contracts where possible; do not add an unnecessary new public API or a parallel proof protocol. Specify how the existing DomainQueryDispatcher/handler DI seam receives the session, how ordinary latest-root/physical reads are prevented for a versioned query, exact authorization/key/TTL/proof budgets, private result completion and final fence. Multiple-root execution may simply hold before invocation unless an already qualified common-fence root-vector capability is available; do not invent a new distributed coordinator. Preserve non-versioned handlers. Add bounded local drift/root-race models and precise future verification vectors, with limitations.
- Recheck authoritative UTC against every TTL-bearing row used by an incremental handler under the final publication fence, including when generation did not change; an expired/unverifiable read holds without effects/checkpoint. Account the read-set evidence and define its lifetime within existing budgets.
- Give CanonicalState prior input an explicit representation for an aggregate single-state route without a 59 named-key-space declaration. Tie it to the existing 52 backend/checkpoint/state-pointer evidence; no fake key-space registration. Keep discriminated presence rules exact and named-root/session semantics intact.
- Replace the stray projection completion `P` reference with the exact existing projection batch/source-prefix/operation/receipt linkage from the retained draft. Aggregate-replay P remains B4-only.
- Reconcile retained final-pin recovery with selector receiver-instance checks: distinguish authenticated original admitted owner/lease evidence from a live lease required for new Apply/incomplete continuation. Completed exact recovery needs no live owner, while current proof/key/fingerprint/capability checks and complete committed readback still apply; never mint new authority from an expired lease.
- State the 1 GiB combined replay quota's exact shared aggregation boundary and atomic reservation/recovery rule across concurrent operations and all retained generations. It must not become an independently reusable 1 GiB allowance per operation; retain smaller configured tenant/store quotas, all caps, and no early release on ambiguous writes/deletes.
- Add B1 source/test/vector anchors for EventStoreAggregate.ProcessAsync/Replay, DomainQueryDispatcher.ExecuteAsync and EventStoreDomainServiceExtensions endpoint mappings, with their current behavior versus proposed admission roles.
- Extend LB-14's existing negotiation predicate and negatives for positive capability revision, exact registered transcript/manifest codecs and admitted receiver instance. Keep original selector/transition/manifest known-answer bytes unchanged. Do not misrepresent it as a signature verifier.

KEEP: all sound loop-1 contracts, LB-01–LB-14 checks and literal known answers; B2 complete-save evidence and diagnostic sandbox; B3/B4 complete-prefix/zero-completion/selector/manifest/timeline/ownership rules; B6 composed admission; B7 zero-work current projection, fold mode, discriminated pinned prior root and versioned cache bypass; B8 compatibility distinction; four BH37 dispositions and VB-01–VB-19. Keep the existing documentation-only footprint, protected-file hashes and unapproved AD-13/unauthorized 6.6 posture. Do not broaden this into implementation or unrelated architectural redesign.

## Review Triage Log


Review pass 1, 2026-09-27: fresh Blind Hunter, Edge Case Hunter and Verification Gap reviewers completed against `/tmp/bmad-6-5b-review-guz29j61.diff`. Thread capacity required sequential launches; none was skipped or replaced with a context-bearing worker. Verification Gap reported no gaps. Findings are graded individually before grouping below.

| Finding | Verdict and verified evidence | Route |
| --- | --- | --- |
| BH1-1 | high — `AdminStreamQueryController.SandboxCommandAsync` synthesizes unpersisted output and calls reconstruction at lines 1183–1201. B2 admits only persisted-source authority, so the existing diagnostic sandbox has no valid path. | bad_spec |
| BH1-2 | medium — B4 applies P/transcript-step rules to page completion without resolving imported draft §6's genesis-only zero-page rule. Empty and snapshot zero-tail implementations can sign different terminal transcripts. | bad_spec |
| BH1-3 | high — B4 requires explicit transcript-v2 capability selection but supplies no authenticated selector representation; gateway, receiver and command verifier cannot deterministically negotiate the new tag-11 interpretation. | bad_spec |
| BH1-4 | high — B4 requires exact manifest/chunk verification but defines no manifest codec or plaintext/ciphertext hash domains. Independent recovery implementations cannot agree on the selected durable bytes. | bad_spec |
| BH1-5 | high — B7 prior-state input permits bytes or a root without a discriminant or pinned-root reader. Imported named queries select the latest root, which can differ from the checkpoint used to admit an incremental tail. | bad_spec |
| BH1-6 | high — the cache stores arbitrary `ExecuteQueryAsync` response bytes (`CachingProjectionActor.cs:124`), not necessarily a row. B7's cached-value membership requirement cannot prove a derived result, nor invalidate it on query-code-only changes. | bad_spec |
| BH1-7 | medium — current cache count is bounded at 32 but retained byte capacity is not; B6's request lifetime charges do not cover the new versioned cache after requests end. Query-sized payload/proof copies can accumulate outside the operation budget. | bad_spec |
| BH1-8 | medium — parent reproduced I(2**31), N(2**63), and decoded pageStart=2**63 being accepted despite draft §4 signed scalar bounds. | patch; incorporated in re-derivation |
| BH1-9 | false — LB-07 is explicitly a framing/known-answer decoder, not an admission verifier. B4 separately requires matching authenticated scope/request/range/chains and committed objects; accepting opaque B scope during structural decoding does not authorize that transition. | reject |
| BH1-10 | low — a linked two-page commit model would strengthen the example, but LB-02 already checks split-page timeline equivalence and the candidate explicitly limits LB-03/07 to ownership/framing models. No production multipage CAS verification is claimed; VB-02/07 require it later. Adding a new state machine is beyond a direct correction and no everyday defect in the claimed local checks was established. | reject |
| EC1-1 | medium — independently confirms BH1-8: unsigned struct helpers and decoder accept out-of-range signed scalars. Parent reproduced the exact trigger. | patch; same scalar root cause |
| P1-1 | medium — B2 imports 6.5a complete V2 evidence while broadly retaining draft §3, whose raw record contract requires V2 intent/receipt absence. Name the replaced absence rule and required source fields explicitly so readers cannot choose opposite V2 admission rules. | bad_spec |

Grouped re-derivation: sandbox diagnostics (BH1-1); exact replay protocol including empty completion/selection/manifest (BH1-2/3/4); incremental prior-root intake (BH1-5); derived cache authority and retained capacity (BH1-6/7); V2 source-evidence reconciliation (P1-1). The scalar patch group (BH1-8/EC1-1) is carried into the same re-derivation. No frozen-intent change or intent gap is required.

Review pass 2, 2026-09-27: fresh reviewers completed against `/tmp/bmad-6-5b-review2-z9wjpr9q.diff`; thread capacity again required sequential launches. Edge Case Hunter returned `[]`; Verification Gap reported no gaps. The ten Blind Hunter findings are individually graded below before grouping. None repeats an unchanged rejected claim from pass 1.

| Finding | Verdict and verified evidence | Route |
| --- | --- | --- |
| BH2-1 | medium — B7b requires authenticated query code/options/dependencies and query-to-key-space classification, but the retained §5 rows enumerate no query route/implementation mapping. `DomainQueryDispatcher.ExecuteAsync` selects only domain/query type from DI. Independent implementations lack common catalog bytes for query-only drift. | bad_spec |
| BH2-2 | high — B7b requires every contributing read to share one root, but ordinary named queries resolve latest roots and the only new session belongs to projection requests. `DomainQueryDispatcher` invokes the legacy handler with no pinned query intake. Two sequential source reads could each verify while crossing generations. | bad_spec |
| BH2-3 | medium — B7a checks TTL while reading but its final publication check lists pointer/generation/source/capabilities only. A previously read leaf can expire during handler execution without a root change. Require authoritative-time recheck of the read set before commit. | patch; incorporated in re-derivation |
| BH2-4 | medium — B7a requires KeySpaceId/OwnershipMode for CanonicalState, while retained §5 permits a single-state route with no 59 row. No representation ties that variant to its existing checkpoint/state pointer without fabricating a named key space. | patch; incorporated in re-derivation |
| BH2-5 | medium — B7 binds handler completion to source/P/operation, but only aggregate replay defines P. A projection has no matching replay transition. Use the existing authenticated projection batch/prefix/operation and receipt binding. | patch; incorporated in re-derivation |
| BH2-6 | medium — B4 final-pin recovery expressly needs no live scratch/owner, while B4b command verification repeats receiver checks including its lease. Make historical admitted lease evidence sufficient for retained completed recovery without weakening live continuation or current-proof gates. | patch; incorporated in re-derivation |
| BH2-7 | medium — B6 calls the 1 GiB allowance an operation quota, while retained §6 calls request/response storage shared and names no aggregation scope. Concurrent operations could allocate one allowance each. Specify one shared backend/deployment pool, atomic reservations and retained-generation accounting. | patch; incorporated in re-derivation |
| BH2-8 | low — B1 omits concrete EventStoreAggregate, DomainQueryDispatcher and endpoint mapping wrappers; the first delegates to covered rehydration/replay, while the latter execute named handlers. Adding direct source/test/vector rows makes each admission responsibility discoverable. | patch; incorporated in re-derivation |
| BH2-9 | medium — parent executed LB-14 with revision 0, unsupported transcript/manifest codecs and a different receiver instance; all returned ModelSelected2. The negotiation model can directly check these already specified conditions without becoming a signature verifier. | patch; incorporated in re-derivation |
| BH2-10 | low — SelectedRequestHash has an explicit acyclic exact-byte formula; local fixture limitations explicitly disclose synthetic request hashes and no signed/provider evidence. No incorrect formula or claimed executed full-request verifier was demonstrated. A new wire/carrier codec fixture would strengthen coverage but adds a separate model beyond a direct correction. | reject |

Grouped re-derivation: authenticated query catalog/intake (BH2-1/2). Carry the independent direct corrections (BH2-3 through BH2-9) into the same revision. No intent gap, runtime change or new approval is needed. BH2-10 remains an explicitly limited future verification obligation, not a claim of completed production verification.

Review pass 3, 2026-09-27: reviewed `/tmp/bmad-6-5b-review3-wsohmidl.diff`. Blind Hunter was fresh; with the user's explicit approval, idle research agents performed Edge Case Hunter and Verification Gap concurrently using the exact layer prompts. Edge Case Hunter returned `[]`; Verification Gap reported no gaps. Each Blind Hunter finding is graded below.

| Finding | Verdict and verified evidence | Route |
| --- | --- | --- |
| BH3-1 | medium — B7d requires an admitted row schema, but B7c names request/response descriptors only. The retained logical-operation witness binds ValueTypeName, while an input reader still needs the corresponding schema/serializer/options mapping. Specify that mapping in the already authenticated registration/query options; no new row, public API or proof format is needed. | patch |
| BH3-2 | medium — a route can have several 59 declarations, but B7a installs only one prior root/session. Explicitly restrict incremental capability to the supported single-key-space case and hold a multi-key-space route before handler invocation. | patch |
| BH3-3 | medium — parent reproduced zero_completion(7, head=9) rejecting the unsupported zero-tail form. B3 permits fixed historical targets but does not explicitly route this ineligible snapshot to full replay. Add the fallback without altering signed prefix forms or known answers. | patch |
| BH3-4 | false — B4c explicitly assigns state-only L=00000000 one protected chunk and the same manifest/quota rules. Its provider rule returns TimelineProtectionHold whenever encryption support is unavailable; the later timeline-mode sentence grants no state-only exception. The described unprotected state-only path is forbidden already. | reject |
| BH3-5 | medium — retained draft §6 renews ReplayOwnerLease/next generation before the next token, whereas B4b's new recovery sentence says every transition belongs to the originally admitted generation. Clarify validation of the existing lease/token/ledger generation chain; fixed receiver identity remains required. | patch |
| BH3-6 | medium — per-root index caps and a bounded ordinal archive do not bound all retained row versions/nodes/roots across generations. Reuse the existing 64 GiB retained named-state ceiling and fenced accounting discipline for their combined aggregate, with hold before allocation/publication. No new storage protocol or public limit parameter is required. | patch |
| BH3-7 | medium — B9's private durable export staging is newly proposed, but the replay storage paragraph does not assign it a quota/cleanup lifecycle. Charge exports to the existing shared storage reservation pool and reuse its ambiguity/retention/deletion rules, with no additional public export surface. | patch |
| BH3-8 | medium — parent reproduced acceptance of a 65-character query discriminator and empty implementation ID. NamingConventionEngine bounds registered names to 64; the empty implementation cannot resolve. Add those existing required-name constraints and direct negatives to B7c/LB-15. | patch |
| BH3-9 | false — LB-14 explicitly models scalar/capability selection and signature-independent fixed values, not wall-clock admission. With no supplied current time, ticks 1..2 are not a demonstrated expired interval; B4b independently requires current admission/operation expiry and VB-16 carries provider time verification. No claim that this predicate fully admits a selector exists. | reject |
| BH3-10 | low — LB-09 is explicitly a small simulation model; B2a specifies rejection and VB-15 covers its production behavior. Adding a new rejection discriminator to the model would extend coverage, but no claimed local rejection-path verification or incorrect normative outcome was demonstrated. The extra branch is beyond a direct correction. | reject |

The seven surviving findings are independent direct corrections within the existing contracts: input schema binding, single-root incremental eligibility, historical snapshot fallback, existing lease generation-chain validation, retained named-state accounting, export storage accounting, and required-name validation. Their smallest fixes add no public API, catalog row or proof codec, and address the concrete states above. Re-engage the loop-2 implementation worker for these patches; no loopback or frozen-intent change is needed.

2026-09-27, pass-3 patches complete: Re-engaged the same loop-2 implementation worker and inspected all seven corrections against `/tmp/bmad-6-5b-before-patch3.md`. Parent independently ran `python3 /tmp/6-5b-final-checks.py` (LB-01–LB-18, exit 0), `python3 /tmp/6-5b-final-audit.py` (110 links, four dispositions, 20 vectors, 18 local groups, 8,011 protected files, exit 0), `python3 /tmp/6-5b-loop2-fixture-audit.py` (original fixtures unchanged, exit 0), exact frozen-block/protected-document comparisons and `git diff --check`. The earlier link scanner interpreted an inline regex as a Markdown link; the final scanner excludes inline code and passes without changing the candidate for that false alarm. All accepted findings are resolved; no deferred or pending review entry remains. Future runtime/provider vectors are explicitly unexecuted and do not prevent completing this documentation scope.

Completion: execution record is done and sprint story is review. The section artifact remains a candidate; AD-13 remains unapproved. Changes are left uncommitted, preserving the authorized documentation-only workspace scope and repository instruction against unsolicited staging/committing.

## Design Notes

No intent gaps or irreversible actions were found. The footprint is the candidate, this execution record and the story's tracker status during implementation. Keep final-only public timeline semantics; bind bounded private accumulation to the same committed progress. Individual maxima do not guarantee combined admission. Reserve caller copies, prior/successor state, proof, serialization and timeline together; an unfit single event requires a proved bounded route or readiness hold. Retain pre-Apply last-good state even when Apply mutates then throws. Preserve historical evidence separately from today's head/ETag. Publication rollout remains 6.5c's responsibility.

## Verification

- Run `git diff --check`; resolve candidate source/section links and all four disposition/vector mappings.
- Execute embedded local checks; identify separately the unexecuted future runtime/provider scenarios, including rollback, query/root races and snapshot fallback.
- Compare preserved-file hashes and inspect the changed-file list; runtime/tests, signed fixtures, normative receipt and existing user edits must remain unchanged.
- Ground vectors in `tests/Hexalith.EventStore.Client.Tests/Aggregates/AggregateReplayerTests.cs` and `tests/Hexalith.EventStore.Server.Tests/Events/{EventStreamReaderTests,SnapshotRehydrationTests}.cs`; document gaps without editing tests. Runtime builds do not validate this documentation-only change.
