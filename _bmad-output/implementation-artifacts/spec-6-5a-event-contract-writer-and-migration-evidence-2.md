---
title: 'Story 6.5a: Event Contract, Writer, and Migration Evidence Candidate'
type: 'feature'
created: '2026-09-27'
status: 'done'
baseline_commit: '02cf007c9860326aa03b32a78541a00b5717bd4a'
route: 'dispatch'
review_loop_iteration: 1
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The unapproved Story 6.5 event-evolution draft leaves five writer and migration evidence findings open. A whole wire response can allocate before admission, and the proposed actor proof, corrupt-history disposition, no-op retirement, and retry response rules are incomplete.

**Approach:** Extend the existing Story 6.5a work-story file with a reviewed, self-contained section candidate for Story 6.5's single normative artifact. Resolve `BH37-1`, `BH37-2`, `BH37-6`, `BH37-7`, and `BH37-8` with exact schemas, numeric bounds, ordered save/readback rules, compatibility and migration branches, typed outcomes, and byte-level verification vectors.

## Boundaries & Constraints

**Always:** Preserve stored V1 bytes, message and sequence identity, original offset, protection metadata, existing positional constructors/deconstruction, and actor-owned save authority. Preserve the draft's signed fixture literals and distinguish current behavior from proposed Story 6.6 behavior. Keep new-writer V1 actor evidence separate from retained-offline V1 evidence. Bind every committed claim to authenticated provider readback, including ambiguous save and post-commit cancellation. Carry candidate terminology and bounds forward consistently for 6.5b/6.5c reconciliation.

**Never:** Change runtime code, tests, public contracts, the single normative AD-13 artifact, its `UNAPPROVED` receipt, or the historical triage; authorize Story 6.6; require Epic 8's optional protection engine; trust caller-built proof or reinterpret a transient HTTP/result status as committed truth.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| New V1/V2 writer | Admitted registered event and selected mode | Canonical identity/version, bounded serialized wire, actor-saved complete intent and post-save proof | Wrong triplet, registry echo, casing, or bound fails before append/allocation |
| Retained V1 | Immutable event with offline migration evidence | Separate signed origin branch resolves exact old bytes | Missing, corrupt, stale, or ambiguous evidence holds readiness |
| Ambiguous actor save | Lost save acknowledgement | Provider readback reconciles the full staged record set before public outcome or retry | Partial or conflicting readback holds without duplicate append |
| Corrupt event and later append | Stable corrupt item remains at its key | Disposition stays valid after unrelated valid head advancement | Changed corrupt bytes or operator decision conflicts |
| No-op and exact retry | No events; later publication status changes | Same-save no-op witness permits capsule retirement; exact retry returns pinned response bytes | Missing witness or first response pin holds without regeneration |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` §§2, 4, 5, 7, 8, 10–12 -- unapproved candidate's schemas, codecs, budgets, migration and receipt; cite or adapt, do not edit or approve.
- `_bmad-output/implementation-artifacts/story-6-5-review-triage.md:546` -- exact five `BH37` findings; record accepted/rejected dispositions in the 6.5a file without rewriting history.
- `src/Hexalith.EventStore.Contracts/Events/{IEventContract,EventMetadata}.cs`, `src/Hexalith.EventStore.Server/Events/EventEnvelope.cs` -- current kebab contract and CLR-oriented metadata; preserve required metadata version and constructors.
- `src/Hexalith.EventStore.Contracts/Results/DomainServiceWireResult.cs`, `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs` -- whole-event serialization and whole-result deserialization precede current post-allocation limits; specify bounded ingress before materialization.
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs`, `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- persister stages CLR-name events; actor alone saves event/index/witness and reconciles ambiguous saves; account for no-op terminal save and cancellation.
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorInfrastructureFailureTests.cs`, `tests/Hexalith.EventStore.Server.Tests/Events/EventPersisterTests.cs` -- existing failure and persistence proof seams to turn into future verification vectors, not tests to edit now.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/spec-6-5a-event-contract-writer-and-migration-evidence.md` -- inventory the exact source, wire, serializer, append, actor, result, migration, and diagnostic seams; mark current versus proposed behavior.
- [x] `_bmad-output/implementation-artifacts/spec-6-5a-event-contract-writer-and-migration-evidence.md` -- write the bounded metadata/registry/writer, same-save intent/receipt, corrupt-source, no-op, response-revision, V1/V2 migration, compatibility, cancellation and failure section candidate with concrete codecs and limits.
- [x] `_bmad-output/implementation-artifacts/spec-6-5a-event-contract-writer-and-migration-evidence.md` -- give each assigned `BH37` finding an explicit accepted/rejected disposition, candidate-section and vector cross-links, and positive/corrupt/oversize/stale/ambiguous/no-op/retry/mixed-version vectors.

**Acceptance Criteria:**
- Given the current source and unapproved draft, when the 6.5a candidate is reviewed, then each proposed rule is grounded in a named path or existing draft contract and all five assigned findings have testable dispositions.
- Given a response, actor save, retained event, or exact retry at a boundary, when the candidate's vectors are applied, then preallocation admission, same-save proof, source-specific conflict, no-op retirement, and stable public bytes have deterministic pass/hold outcomes.
- Given Story 6.5 integration, when this candidate is handed off, then its schemas and bounds can be reconciled with 6.5b/6.5c while the normative artifact remains `UNAPPROVED` and 6.6 unauthorized.

### Review Findings

Code review 2026-09-27 of `02cf007c..14c51e8b` (review pass 3; Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor). The owner agreed this routing rule: fix now only findings in the AC-breaking categories (ungrounded rule, boundary with non-deterministic outcome, overstated verification); defer other refinements to Story 6.5 integration. Parent re-verification: K01–K12 exit 0 with all 40 length/hash pairs asserted, exactly five BH37 rows, contiguous V01–V24, no diff under `src/`, `tests/`, the normative draft or the historical triage, `git diff --check` clean. The Verification Gap layer reported no gaps.

- [x] [Review][Patch] Declare the tenant-wide execution MessageId rule as an explicit Review-32 replacement [spec-6-5a-event-contract-writer-and-migration-evidence.md:254] — resolved from decision 2026-09-27 (owner chose option 1): within one tenant an execution MessageId maps to at most one scope; cross-scope reuse is `CommandIdentityConflict` at admission, matching current `CommandStatusConstants.BuildKey(tenant, messageId)` and the `SubmitCommandHandler` archive identity check; cross-tenant reuse stays independent. Original finding: A8's `command-execution-scope:` key is SHA-256(`U tenant || U executionMessageId`) and a conflicting same tenant/message is `CommandIdentityConflict` (candidate line 254). The draft's Review-32 vector (`spec-event-versioning-upcasting.md:472`) admits one original command MessageId in different tenant/domain/aggregate scopes with separate preparation keys and batch roots. The candidate does not declare this as a replacement, so integration receives two contradictory inputs. Choose: adopt the tenant-wide rule as an explicit Review-32 replacement; key the lookup by full scope and let the status route return its existing 409 for multiple matches; or defer the choice to 6.5 integration.
- [x] [Review][Patch] Outcome codec replacement cites a codec that does not exist [spec-6-5a-event-contract-writer-and-migration-evidence.md:204] — "Replace outcome codec `02` with `03`", but the draft defines only `HX-EV-COMMAND-OUTCOME-1 … 01` (`spec-event-versioning-upcasting.md:366`) with tag `07` limited to `pending`/`published`/`failed`. Name draft codec `01` as the replaced codec, state that `02` is unassigned, and declare the tag `07` extension to `unknown` and `not-applicable`. K05/K10 hashes stay unchanged.
- [x] [Review][Patch] V07 and A9 give different outcomes for a proven no-commit save with an unchanged source [spec-6-5a-event-contract-writer-and-migration-evidence.md:318] — V07 says proven no-start plus no future commit "permits cleanup"; A9 (line 269) permits only fenced same-capsule continuation when source/version is unchanged, or A5 AbortedStale when changed. Align V07 with A9.
- [x] [Review][Patch] K10/K12 local models are claimed to cover more than they encode [spec-6-5a-event-contract-writer-and-migration-evidence.md:332] — V21 says "K10 encodes every phase and recovery model" and the traceability rows for BH1-5/6/7 (lines 345–347) cite K10/K12 as local verification. `response_recovery` (line 775) takes five booleans: it does not model owner-fence theft, one-of-two blob persistence, preparation-write evidence, or missing admission-bound origin configuration, and it returns `return-pinned` for a pin beside a NeverStarted/Rendering state, which the prose treats as a contradiction. `parse` does not validate preparation state range, fence > 0 or per-state tag presence. K12 `retry` ignores the submitted IDs and has no Replay/Recoverable/Pending/unknown paths. Add a local assertion that a pin with state < 2 holds, and reword V21/V22/V23 and the traceability rows so they name which properties are local-model checks and which remain future provider vectors.
- [x] [Review][Patch] New deferred-work entries omit `status: open` [deferred-work.md:4931] — the three "Story 6.5a candidate review" entries have no status line while adjacent entries carry `status: open`, so ledger triage may not treat them as open.
- [x] [Review][Defer] Integration handoff omits the ActorBundleReadbackHash codec change [spec-6-5a-event-contract-writer-and-migration-evidence.md:153] — deferred: to 6.5 integration. Step 8 replaces the draft's codec-01 preimage (`spec-event-versioning-upcasting.md:331`, referenced at 118/360/370), and the batch-root tag stores it. A5 line 127 says the bundle uses codec 02, but the handoff (line 896) lists only certificate/receipt for 6.5b.
- [x] [Review][Defer] Six new typed outcomes are not listed for integration [spec-6-5a-event-contract-writer-and-migration-evidence.md:266] — deferred: to 6.5 integration. `CommandIdentityConflict`, `AppendPreparationStale`, `CommandOutcomeHold`, `MetadataLimit`, `EventIdentityMismatch` and `UnknownEventContract` do not occur in the draft, and neither the A9 table nor the handoff enumerates them as additions to draft §8.
- [x] [Review][Defer] Outcome-head predecessor/CAS-preparation evidence has no codec [spec-6-5a-event-contract-writer-and-migration-evidence.md:218] — deferred: to 6.5 integration. Recovery of revision `r+1` before the head CAS depends on "durable CAS-preparation evidence binding predecessor key/revision/hash", but no record name, key, fields or cap is defined, unlike every other new record.
- [x] [Review][Defer] K02/K07 fixtures use keys that do not follow the stated key derivations [spec-6-5a-event-contract-writer-and-migration-evidence.md:386] — deferred: to 6.5 integration. K02 stages `batch-member-root:op` instead of `batch-member-root:` plus ScopeOpHash (A5 line 129), and K07 stages `aggregate-operation-result:op` instead of the draft's hashed key (`spec-event-versioning-upcasting.md:336`). The fixed hashes freeze those marker keys, and line 354 disclaims only the marker after-images.
- [x] [Review][Defer] A3 restates V1 64 MiB/128 MiB scratch limits that open BH37-4 contests [spec-6-5a-event-contract-writer-and-migration-evidence.md:91] — deferred: pre-existing draft limits; BH37-4 (`story-6-5-review-triage.md:549`) is routed to integration by the handoff, but A3 does not mark these numbers as provisional for 6.5b.
- [x] [Review][Defer] Case variants of negotiation property names are unspecified [spec-6-5a-event-contract-writer-and-migration-evidence.md:69] — deferred: to 6.5 integration. A wrong-case `WriterMode`/`RegistryFingerprint` is not explicitly rejected in V1 mode, and K06 `negotiated` treats it as implicit V1. The authenticated capability comparison still prevents a V2 downgrade.
- [x] [Review][Defer] A failed publication row at an unchanged attempt can swap its receipt [spec-6-5a-event-contract-writer-and-migration-evidence.md:701] — deferred: to 6.5 integration. K09 `reduce_set` requires only that the state stays failed, and A8 (line 214) does not state that a failed row is immutable for its attempt.
- [x] [Review][Defer] Recovery-token and retry-lifetime numbers are uncited [spec-6-5a-event-contract-writer-and-migration-evidence.md:275] — deferred: to 6.5 integration. The 30-second attempt matches `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:2142` and "at least 24 hours" (line 260) matches the draft's command continuation budget (`spec-event-versioning-upcasting.md:383`), but neither is cited.

Rejected:

- Status values disagree across the execution spec, sprint tracker and candidate — the fix edits the spec under review; `status: candidate` is defined by the candidate's reading convention, and this review sets the final sprint status.
- The execution spec's Verification section omits the K-block command — the fix edits the spec under review; the candidate's Verification evidence records the extraction and execution method.
- The execution spec cites an ephemeral `/tmp` preservation file — the fix edits the spec under review; already rejected as BH2-7.
- "Only this work-story file was edited by this implementation" is inaccurate — false: the sentence attributes the other changed files to pre-existing and parent work.
- "Both metadata records" is ambiguous and `ReplayEventEnvelope` is missing from A1 — false: the draft names both records (`spec-event-versioning-upcasting.md:38`), and replay DTO members are specified at draft line 46 (6.5b scope).
- A 2 MiB publication set cannot hold 1,000 maximum-length members — false: A8 line 212 preflights worst-case accepted rows against 2 MiB before append and reduces or holds the batch.
- K01 does not assert decoded-length rejection — false: the assertion shows the encoded length cannot distinguish +1, which is why A3 step 4 requires a decoded-length check; V03 claims boundary arithmetic only.
- K12 returns 409 for a direct required record — false: line 256 makes multiple tenant/execution matches 409 on the direct path as well.
- K12 maps a legacy null status read to 404 — false: the candidate explicitly preserves the current legacy-only fallback (line 258, V23).
- No `Ready → AbortedStale` transition — false: `Ready` is the reservation-root state; the capsule remains `Prepared` during Pending/Ready (draft line 327), and line 163 releases the reserved root.
- Event-ID reservation rejection after Prepared has no terminal path — false: imported draft §7 (line 329) defines failed-reservation release, and the candidate does not replace it.
- A delete row may carry an absent expected-before — low: an unlikely phantom delete that must match intent and receipt exactly; the fix adds a guard.

#### Review pass 5 (2026-09-27, `14c51e8b..53cbe2dc`)

Scope: the pass-3 patches and the pass-4 BH4-1/BH4-2 fixes only. Reviewers: Blind Hunter, Edge Case Hunter, Verification Gap and Acceptance Auditor; none failed. Verification Gap found no gaps: K01–K12 exit 0, and replacing the new `pin_present and state < 2` guard with `pass` makes the new assertion fail. The pass-3 routing bar still applies.

- [x] [Review][Patch] Keep the first POST reply independent of publication state; limit the private-`failed` hold to status inspection [spec-6-5a-event-contract-writer-and-migration-evidence.md:217] — resolved from decision 2026-09-27 (owner chose option 2). Current code returns 202 on publish failure (`CreatePublishFailedResult` `Accepted = true` for non-rejections, `AggregateActor.cs:4441`); the 202 body `{correlationId, resultPayload, messageId}` and the committed rejection ProblemDetails carry no publication claim; and A8's renderer "may not consult … current publication status". So revision zero records any complete observation, `failed` included, and pins normally. The first-pin bytes are identical to the `pending` case. Failed observations create ordinary private revisions and advance the head. Status inspection of a head at private `failed` yields `CommandOutcomeHold`, never PublishFailed/stop-polling. No permanent-failure public reply is produced from per-attempt failure. Edits: A8 :217 hold sentence; the A9 publication row; restore V13's revision-0 `failed` case (POST retry returns the original 202 bytes; status holds until a later published revision); replace "public outcome selection" with "status inspection" in V20/V23 and the handoff. Original finding: BH4-2's new A8 sentence (candidate :217) says that rendering or exposing a public outcome from private `failed` yields `CommandOutcomeHold`, and that no new outcome pin or head is created. A8 step 1 (:245) renders only after a complete publication observation. Response-input tag `07` binds that observed state, and "Rendering inputs are fixed once" (:249). So when the first complete observation of a committed eventful command is `failed`, the spec leaves three things open: whether the input record is created, whether revision zero or the first pin can ever exist, and whether a later `pending` observation after an admitted retry may become revision zero. A committed domain rejection whose rejection-event publication fails hits the same case. With no integrated mapping, the hold has no bound. The same sentence also conflicts with the earlier text in the same paragraph that "failed … revisions require newly authenticated complete observations", so writers can disagree on whether a failed observation advances `command-outcome-head`. The patch also deleted V13's "Repeat from revision 0 `failed`" case without replacing it, while the BH37-8 disposition row (:306) still cites V13–V15 for that behavior. Sources: edge+blind+auditor.
- [x] [Review][Patch] Take committed-rejection classification from the authenticated result record, and pin before choosing an exception branch [spec-6-5a-event-contract-writer-and-migration-evidence.md:255] — `ThrowDeterministicFailure` uses `processingResult.RejectionEventType ?? advisoryRejectionEventType` (`SubmitCommandHandler.cs:694`). The normal path passes `finalStatus?.RejectionEventType`/`FailureReason` from the advisory TTL status row (`:566-571`), and the replay path passes none (`:651`). The rule is keyed on `DomainCommandRejectedException`. It does not require the rejection type to come from the A7 committed result record, and it does not classify the other branches. Those branches are identity conflict, key expired, backpressure, ConcurrencyConflict and the final `InvalidOperationException`. So the normal and replay paths can classify the same committed rejection differently, which breaks the "Never reinterpret transient result status as committed truth" boundary. Fix: select the pin from the authenticated committed record before `ThrowDeterministicFailure` branches; if that record carries no rejection type, hold; never use advisory `finalStatus`. State that the other branches are existing precommit errors.
- [x] [Review][Patch] Qualify draft §7's cross-scope sentence in the Review-32 replacement [spec-6-5a-event-contract-writer-and-migration-evidence.md:257] — the replacement "replaces only draft §11's Review-32 vector". Draft §7 (`spec-event-versioning-upcasting.md:323`) also says "Equal command MessageIds in different scopes use different preparation keys", which reads as admitting same-tenant cross-scope reuse. So integration still receives two conflicting inputs. Fix: name §7's sentence too and restrict it to cross-tenant scopes.
- [x] [Review][Patch] Create the tenant/message scope lookup with create-if-absent semantics [spec-6-5a-event-contract-writer-and-migration-evidence.md:259] — the Review-32 replacement relies on this record to enforce one scope per tenant/execution MessageId. But the record says only "encode/create/read back", not the immutable CAS-create used for the other new records. Two concurrent same-tenant admissions in different scopes can then both write, or the last write can rebind the MessageId. Fix: specify an immutable CAS create-if-absent; an admitter whose readback differs gets `CommandIdentityConflict`.
- [x] [Review][Patch] Give V21's newly added invalid-state inputs an expected outcome [spec-6-5a-event-contract-writer-and-migration-evidence.md:337] — the input column now adds "inject invalid state/fence or per-state tag presence". The expected column says only that these remain future checks, with no pass/hold result, which fails AC2's requirement for a deterministic outcome. Fix: state that each yields `CommandOutcomeHold`, consistent with A8's "missing/corrupt record … cannot become NeverStarted".
- [x] [Review][Patch] Deferred-ledger entries cite candidate line numbers that were stale when committed [deferred-work.md:4945] — the ten entries added in `53cbe2dc` use `14c51e8b` line numbers, but the same commit shifts the candidate. For example, the handoff "line 896" is now 906 (a K11 hash), `:153` is now step 7, and `:701`, `:218` and `:91` land on the wrong text. Fix: cite section/vector anchors or post-commit lines. (The same stale numbers in the pass-3 Review Findings above were rejected because fixing them edits this spec.)
- [x] [Review][Defer] The tenant-wide one-scope rule has no retention horizon and no legacy coverage [spec-6-5a-event-contract-writer-and-migration-evidence.md:257] — deferred: to Story 6.5 integration (routing bar: refinement, not an AC-breaking contradiction). The only enforcing record is the `required`-class lookup, which is retained only "through all retry/status obligations". Legacy executions have only the 24-hour advisory `BuildKey` row and the archive check, which compares MessageId/CommandType but not scope. Reuse after reclamation, or after a legacy admission, is admitted or rejected depending on reclamation timing. Integration must state the horizon (or add a tombstone) and legacy coverage.

Rejected (pass 5):

- The Review-32 replacement and the lookup disagree on ordering ("before domain invocation" vs "before actor save") — false: the lookup is a joint admission operation with preparation initialization, and A8 creates preparation "before domain invocation, actor save or rendering".
- Tag `07` values outside the five-value set are undefined — false: "Extend tag `07` … to also admit" enumerates a closed admitted domain; nothing admits another value.
- K10 `response_recovery` returns `return-pinned` for state ≥ 4 beside a pin — false: candidate :260 explicitly says the generic parser does not validate state range and routes that check to V21. The model inputs are fixtures, not parsed data.
- The pass-3 Rejected item "the fix adds a guard" claims a fix that doesn't exist — false: the phrase gives the rejection rationale (a low finding whose fix would add a guard), not a claim that a guard was added.
- "These patches changed only this candidate and those ledger lines" is overstated — false: the sentence covers the five pass-3 patches. The additional ledger entries and execution-spec records came from the review step, which the pass-3 Review Findings record.
- The rejection render's `Instance`/TenantId inputs are unbound in response-input, and ownership of the catalog layer is open — low: `Instance` is the fixed command-endpoint path selected by the bound renderer/options identity, and the tenant is in the admitted scope. Adding a tag adds codec surface for an edge case unlikely to be met.
- The frontmatter `status: 'done'`/`review_loop_iteration: 1` disagrees with the sprint tracker — the fix edits the spec under review; this review sets the status.
- The Spec Change Log omits Review-32, codec `01`→`03`, tag `07` and the PublishFailed hold — the fix edits the spec under review.
- Pass-4 findings are table rows rather than checklist items, and "all three review layers" goes unexplained — the fix edits the spec under review.
- The "already recorded" deferrals can't be traced to ledger IDs — low: the summaries match one-to-one. Adding IDs to historical ledger entries buys little.
- High-severity context deferrals have no owner or severity field — low: pre-existing ledger convention with no severity field; the entries are `status: open` and preserve the user's uncommitted context bytes.
- The Review-32 replacement lacks a K-block key check and a statement about existing two-scope data — low/false: a K assertion adds code for a trivial hash property, and A8 already says existing executions need independently approved migration evidence and are never inferred.

## Implementation Notes

Implemented the [6.5a section candidate](spec-6-5a-event-contract-writer-and-migration-evidence.md) with source inventory, A1–A10 contracts, five proposed finding resolutions, 24 future acceptance vectors and K01–K12 executable codec checks (40 fixed length/hash pairs). The candidate's matrix mapping covers all five frozen scenarios. Provider/runtime scenarios remain future acceptance requirements, as this work explicitly excludes runtime implementation and tests.

Parent verification inspected the complete baseline diff including the pre-existing epic-context and execution-spec changes. Those user changes were preserved. Executed K01–K12 after loop-1 corrections, checked exact disposition cardinality, and confirmed the original draft, historical triage and user epic-context hashes are unchanged. `git diff --check` passed. No runtime files changed.

## Spec Change Log

2026-09-27, loop 1: BH1-1/2/3/5/6/7 and EC1-1 exposed incomplete non-frozen implementation guidance for negotiation omission, stale preparation, complete batch outcomes, response preparation and existing idempotency/status seams. Added the exact requirements below; BH1-4/8 are corrected in the same re-derivation. Avoid partial-batch success, stale-result reuse, response regeneration and bypass lookup. KEEP instructions preserve the verified candidate design and protected inputs.

### Review loop 1 implementation requirements

Read the prior candidate at `/tmp/bmad-6-5a-candidate-review-1.md` as preservation evidence. Restore its sound content into the target work-story, then correct the verified defects below. This is still documentation-only and changes no normative artifact, historical triage, runtime/tests, or user epic context.

- Make implicit V1 compatibility serialization omit both negotiation fields and demonstrate exact bytes; mode-bearing serialization emits exact non-null mode/fingerprint.
- Define a safe proven-no-commit stale-preparation terminal transition. Never reuse a stale domain result on a changed actor version or regenerate an existing operation's volatile values. Bind terminal cleanup/release to authenticated no-future-commit evidence; unresolved evidence still holds.
- Define exact bounded complete-batch publication evidence and reduction over every expected event/destination. Partial acceptance is pending, published requires all accepted, failed/unknown/retry rules preserve accepted members. Bind the immutable outcome to a complete ordered receipt-set commitment, with deterministic framing and verified per-member receipts; keep no-op not-applicable distinct.
- Scope first-pin requirements to committed execution replies. Preserve independently authorized precommit errors and truthful unresolved holds without inventing a committed pin or allowing an unpinned success.
- Specify durable fenced first-response preparation states and exact bounded evidence/ordering before rendering, prepared-byte persistence, first-pin CAS and recovery. Lost prepared bytes hold, never silently regenerate; distinguish a genuinely never-started preparation from missing authority.
- Inventory and integrate `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs` (Replay/CreateReplayResult/unknown-outcome recovery and ExecutionMessageId), `src/Hexalith.EventStore/Controllers/CommandsController.cs`, `CommandStatusController.cs`, `Server/Commands/{ICommandStatusStore,DaprCommandStatusStore,ICommandCorrelationIndex}.cs`. Protected idempotency retries with changed submitted message/correlation IDs resolve the original admitted execution and its response pin. Describe request identity versus execution identity and current authorization without changing existing public contracts.
- Define authenticated tenant/message-to-scope lookup for the current command-status route and pinned Location. Retain tenant authorization, ambiguous correlation behavior and legacy-only fallback; an evidence-required missing outcome must not fall back to a stale legacy success.
- Add complete executable known answers for the changed intent/receipt/presave/readback/corrupt/outcome/pin codecs and the corrections above, including derived certificate/origin rows, a deletion, original nonzero timestamp offset, malformed counts/tags/trailing bytes and missing/extra rows. Keep local model checks explicitly separate from future provider tests. Update all scenario maps, hashes and counts.

KEEP: A1 source-versus-proposed separation; A2 compatibility/registry rules; A3 transport and allocation guards; exact A4 V1 branch distinction; A5 complete member-root/control mutation coverage and acyclic dependency order; A6 stable corrupt-source identity; A7 result/witness/no-op retention; first authoritative public response versus later status; 17 existing vectors and K01–K05; original signed fixtures, normative receipt, user context, and runtime files byte-for-byte. Add focused checks without claiming provider conformance or requiring Epic 8.

## Review Triage Log

Loop-1 re-derivation completed: BH1-1 through BH1-8 and EC1-1 have explicit candidate correction/vector mappings; parent reran the entire embedded Python block successfully and inspected the changed schemas and complete fixture bytes. BH1-9/10 remain deferred user-context concerns.

| Finding | Verdict and verified evidence | Route |
| --- | --- | --- |
| BH1-1 | medium — A2 rejects null negotiation fields but leaves nullable-property omission unspecified; the bounded compatibility writer could emit a self-rejected implicit V1 exchange. | bad_spec |
| BH1-2 | high — A5 says clean re-admission after a before-image mismatch while A4 forbids regenerating prepared values; a stale domain result has no explicit safe terminal transition. | bad_spec |
| BH1-3 | high — A8 pending forbids any accepted publication while batches contain multiple members; partial acceptance has no valid outcome and one receipt does not establish all-member success. | bad_spec |
| BH1-4 | medium — A8's unrestricted prohibition on any application response before first pin also reaches admission/authorization/limit/hold responses that have no committed result. | patch, subsumed in re-derivation |
| BH1-5 | medium — A8 permits never-started response preparation recovery without a durable start distinction; a crash between rendering and durable pinning has no specified positive recovery evidence. | bad_spec |
| BH1-6 | high — SubmitCommandHandler.Handle Replay/Recoverable and CreateReplayResult use admitted ExecutionMessageId and reconstruct a response before actor entry; the candidate omits those routes and could lose the original pin. | bad_spec |
| BH1-7 | medium — CommandStatusController resolves tenant/message or correlation via ICommandStatusStore/ICommandCorrelationIndex, while A8 specifies only a scope-hash head. No authenticated bridge selects that head. | bad_spec |
| BH1-8 | medium — K02 checks marker row bytes and K05 checks membership, but complete intent/receipt/bundle/corrupt/outcome/pin known-answer encodings are absent. Divergent implementations could pass the current local fixtures. | patch, subsumed in re-derivation |
| BH1-9 | medium — Pre-existing epic-context edits omit the already-current zero-state-write/checkpoint rule retained in the baseline context. This run did not make those edits, and they must be preserved. | defer: agent-context follow-up |
| BH1-10 | high — Pre-existing epic-context edits omit Current-only mutation and denied-view non-disclosure rules; these remain relevant to future UI consumers. Preserve user edits and refer to authoritative UX during that follow-up. | defer: agent-context follow-up |
| EC1-1 | high — Independently confirms BH1-3: an accepted first member plus pending second member cannot satisfy A8 pending, and the single receipt does not prove complete publication. | bad_spec; grouped with BH1-3 |

Verification-gap reviewer: no verification gaps found. All three review layers completed before triage. Findings above concern this documentation artifact; no runtime implementation was requested or inferred.


### Review pass 2

| Finding | Verdict and verified evidence | Route |
| --- | --- | --- |
| BH2-1 | high — A8/V20/K09 permit two destinations for one MessageId while imported draft §7 fixes one component/topic in outbox/global pin. Remove the candidate-only fan-out and use two event members for the partial-publication vector. | patch |
| BH2-2 | high — A8's encoded set/evidence limits can exceed capacity after append, before revision zero exists. A3/A4 require upfront reservations; explicitly apply them to worst-case complete publication records and provider evidence before append. | patch |
| BH2-3 | high — A4's imported capsule list does not explicitly include A5 control before/after images and derived rows; immutable preparation must seal all of them before Prepared/reservation. Clarify the existing dependency order. | patch |
| BH2-4 | medium — A8's trusted original Location is first encoded after commit; the admission authority must already bind and retain its canonical origin configuration. Reuse that existing authority/config binding instead of a retry host. | patch |
| BH2-5 | medium — Immutable next revision can survive a crash before head CAS. A8 reconciliation must first authenticate and finish that existing revision before using a newer observation at the following revision. | patch |
| BH2-6 | medium — Executed K09 admits failed-to-pending at unchanged attempt and unknown-to-pending without closure evidence; prose forbids both. Correct this local verifier and add negative assertions without claiming provider authentication. | patch |
| BH2-7 | medium — The execution-spec change log records a temporary preservation input that would not exist in a new checkout. This is a workflow spec issue, not a candidate defect; the final candidate already retains the sound content. | reject: workflow explicitly rejects findings whose fix edits this build spec |
| BH2-8 | medium — carried BH1-9: same pre-existing zero-write/checkpoint deletion in the preserved user epic context. | defer, already classified |
| BH2-9 | medium — Pre-existing epic-context condensation also drops explicit full-replay/incremental declarations and safe fallback; downstream projection planning may infer tail support for every handler. | defer: agent-context follow-up |
| BH2-10 | high — carried BH1-10: same preserved context location omits Current-only mutation/denied-view rules; snapshot implemented/current prerequisite is likewise absent. | defer, already classified |
| EC2-1 | high — Independently confirms BH2-1's conflict between candidate fan-out and the unchanged one-destination MessageId pin. | patch; grouped with BH2-1 |
| EC2-2 | medium — carried BH1-9: same zero-state-write/checkpoint context deletion. | defer, already classified |
| EC2-3 | high — carried BH1-10: same Current-only/implemented-action context deletion. | defer, already classified |
| EC2-4 | high — carried BH1-10: same denied-view non-disclosure context deletion. | defer, already classified |

Verification-gap reviewer: no verification gaps found. All layers completed before this classification. The six patch groups are direct corrections to the candidate's existing rules/model; they add no runtime public surface and require no intent change.

Final patch verification on 2026-09-27 resolved BH2-1 through BH2-6 and EC2-1: A8 retains one destination per event and reserves the complete first-outcome capacity before append; A4/A5 seal all actor mutation images before Prepared; admission retains the original origin configuration; outcome recovery finishes an existing immutable revision before advancing; and K09 rejects reused attempts and retries without closure/admission evidence. Parent verification reran K01–K12 successfully (40 fixed length/hash pairs and all negative/model assertions), confirmed exactly five finding dispositions and contiguous V01–V24, and passed `git diff --check`. The normative draft, historical triage and user epic-context hashes remain unchanged; runtime source and tests have no diff. Both review passes completed all three layers, with no verification gaps reported. The three distinct pre-existing context concerns (BH1-9, BH1-10 and BH2-9, including carried findings) are recorded in `deferred-work.md`.

The documentation execution is done and the sprint story is ready for review. The output remains a section candidate for integration with 6.5b/6.5c; this workflow status neither approves the normative artifact nor authorizes Story 6.6.

### Review pass 3 patch completion

2026-09-27: completed all five authorized review patches. A8 and V22 explicitly replace Review-32 with one scope per tenant/execution MessageId while preserving cross-tenant independence. Outcome codec `03` now explicitly replaces draft codec `01`, with `02` unassigned and tag `07` extended. V07 matches A9's unchanged-source continuation versus stale-source abort. K10 holds on pins in NeverStarted/Rendering, and V21–V23 plus BH1-5/6/7 traceability distinguish local assertions from future runtime/provider verification. The three original deferred-context entries now carry `status: open`.

Parent acceptance verification read the full diff against preserved baseline `02cf007c9860326aa03b32a78541a00b5717bd4a`, independently reran the embedded K01–K12 block (40 unchanged length/hash pairs), and confirmed the contradictory-pin assertions fail when their new guard is removed. All five frozen matrix rows retain their explicit document/vector mappings; the runtime/provider vectors remain future acceptance requirements under the frozen documentation-only boundary. The five assigned BH37 dispositions, V01–V24 sequence, three ledger statuses, and `git diff --check` passed. The normative draft, historical triage and pre-existing epic context match this run's initial SHA-256 values; the runtime/test diff remains empty.

### Review pass 4 (resumed build)

All three review layers completed before triage; Verification Gap reported no gaps. The owner-recorded pass-3 routing rule remains in force: correct AC-breaking source omissions, contradictory boundary outcomes and overstated verification; retain the existing integration refinements and user-context deferrals. The full review diff also captured a concurrent, unrelated Hexalith.Commons pointer change; this build did not make or alter it.

| Finding | Verdict and verified evidence | Route |
| --- | --- | --- |
| BH4-1 | medium — `SubmitCommandHandler.CreateReplayResult` calls `ThrowDeterministicFailure` for a rejected result, and `ErrorHandling/DomainCommandRejectedExceptionHandler.cs` rebuilds ProblemDetails using current middleware correlation/catalog. A8 already requires all committed replies to use the first pin but omits this concrete exit seam and vector. | patch: name and route the rejection exit through the existing pin rule, with a future exact-byte retry vector |
| BH4-2 | medium — candidate A8 permits private `failed` to advance to another attempt, while `CommandStatus` and `CommandStatusController` define public `PublishFailed` as terminal and stop polling. "Failed uses the existing failure shape" overstates compatibility. | patch: forbid that inference and explicitly hold when no integrated terminal mapping is established; leave the public mapping decision to 6.5/6.5c |
| BH4-3 | medium — carried pass-3 outcome-head predecessor/CAS evidence codec deferral. The related response preparation-write evidence also lacks an exact authority record contract; A8 already holds without verified authority, so this is integration completeness rather than permission to infer recovery. | defer: retain predecessor deferral; record only the newly identified preparation-write contract refinement |
| BH4-4 | medium — carried pass-3 finding: same-attempt failed publication rows can exchange receipt hashes. Existing owner-approved integration deferral remains unchanged. | defer, already recorded; no duplicate ledger entry |
| BH4-5 | medium — carried pass-3 ActorBundleReadbackHash handoff omission; A5 defines codec 02 but the handoff names only certificate/receipt. | defer, already recorded |
| BH4-6 | medium — carried pass-3 K02/K07 marker-key derivation finding. Fixed fixture hashes and current framing-only disclaimer are unchanged. | defer, already recorded |
| BH4-7 | medium — carried pass-3 V1 scratch-limit/BH37-4 reconciliation finding. | defer, already recorded |
| BH4-8 | medium — carried BH1-9/BH2-8/EC2-2: user-context condensation omits already-current zero writes/checkpoint advancement. | defer, already recorded; preserve user bytes |
| BH4-9 | medium — carried BH2-9: user context omits declared full-replay/incremental capabilities and safe fallback. | defer, already recorded; preserve user bytes |
| BH4-10 | high — carried BH1-10/BH2-10/EC2-3/4: user context omits Current-only actions, implemented snapshot prerequisites and denied-view non-disclosure. | defer, already recorded; preserve user bytes |
| EC4-1 | medium — carried pass-3 negotiation-property casing finding; the authenticated capability check still prevents a V2 downgrade. | defer, already recorded |
| EC4-2 | medium — carried pass-3 failed-receipt immutability finding, same location/claim as BH4-4. | defer, already recorded |
| EC4-3 | medium — carried BH1-9/BH2-8/EC2-2 zero-write/checkpoint context omission. | defer, already recorded |
| EC4-4 | medium — carried BH2-9 full-replay/incremental declaration and fallback omission. | defer, already recorded |
| EC4-5 | high — carried BH1-10/BH2-10/EC2-3 Current-only action/snapshot prerequisites omission. | defer, already recorded |
| EC4-6 | high — carried BH1-10/BH2-10/EC2-4 denied-view non-disclosure omission. | defer, already recorded |

Final resumed-build verification on 2026-09-27: BH4-1 is corrected in A1/A8/V22 by routing committed rejection exceptions through the same original bounded renderer and response pin. BH4-2 is corrected in A8/A9/V20/V23: per-attempt private failure cannot imply terminal public PublishFailed; unmapped outcomes hold, preserving committed truth and existing pins. The exact permanent-failure mapping remains an explicit 6.5/6.5c integration requirement. Both additions are future runtime vectors, not claims of executable provider tests.

After these patches, the parent reran K01–K12 and all 40 unchanged fixed length/hash pairs, confirmed guard-removal regression sensitivity, resolved 36 source/test paths and eight links/anchors, verified five dispositions and contiguous V01–V24, and passed `git diff --check`. Frozen intent, normative draft, historical triage and original user-context bytes remain unchanged; runtime/tests remain unchanged. All three independent review layers completed; no verification gaps were reported. Previously recorded integration/context deferrals remain open, with preparation-write authority and terminal-publication mapping handed to integration. Documentation work is complete and the sprint story is ready for review; the candidate remains unapproved for runtime implementation.

### Review pass 5 patch completion

2026-09-27: applied all six owner-recorded pass-5 patches. The first POST response is independent of publication state, including a first failed observation; private failed revisions/head transitions proceed while unmapped failed status inspection holds. Committed rejection classification and first-pin selection precede every failure branch and use the authenticated result, never advisory status. The tenant-wide execution identity rule explicitly qualifies draft §7 as well as Review-32; its immutable lookup uses create-if-absent and exact readback. V21 now assigns CommandOutcomeHold to invalid state/fence/tag inputs. Ten existing ledger entries use stable section links and retain their open integration dispositions.

Parent verification inspected the complete diff from the preserved baseline and the resumed changes, executed the unchanged K01–K12 block with all 40 fixed length/hash pairs, checked five BH37 dispositions, contiguous V01–V24, five frozen matrix mappings, 36 source/test paths and 25 candidate/ledger links, and passed `git diff --check`. The normative draft/UNAPPROVED receipt, historical triage, epic context and frozen intent match their pre-run hashes; runtime/tests are unchanged. New handler, concurrency and provider expectations remain future acceptance vectors under the documentation-only intent, not executed runtime tests.

### Review pass 6 (pass-5 patch completion)

2026-09-27: Blind Hunter, Edge Case Hunter and Verification Gap completed independently before triage. Verification Gap reported no gaps and independently ran K01–K12/all 40 fixed pairs; removing the existing pin/state contradiction guard causes its assertion to fail. The preserved owner routing bar still applies: correct AC-breaking source omissions, contradictory boundary outcomes and overstated verification; retain integration refinements and user-context deferrals. No new acceptance-breaking defect was found in the six patches.

| Finding | Verdict and verified evidence | Route |
| --- | --- | --- |
| BH6-1 | medium — A8 ExpectedEntries names a destination ID for the pinned component/topic/configuration but neither defines its canonical string derivation nor names an immutable source field. Equivalent destinations could produce different ExpectedSetHash values across implementations; the outbox/pin hashes still bind the exact destination evidence. | defer: new interoperable destination-ID derivation refinement for Story 6.5/6.5c, under the owner routing bar |
| BH6-2 | low — carried pass-5 rejected Instance-input concern: the existing rejection handler uses Request.Path; candidate A8 binds the original endpoint renderer/options and admitted endpoint inputs without a separate raw-path tag. A differently cased/rewritten route is the same uncommon boundary previously considered; another input tag adds codec surface. | reject: retain the owner-recorded low-severity routing; no new changed behavior demonstrated |
| BH6-3 | medium — carried BH4-3: A8 Rendering recovery still requires authenticated preparation-write authority without its complete schema/key/cap/fence contract; missing authority already holds. | defer, already recorded; no duplicate |
| BH6-4 | medium — carried pass-3 predecessor/CAS evidence finding and BH4-3: the exact authority record contract remains integration work. | defer, already recorded; no duplicate |
| BH6-5 | medium — carried BH4-4/EC4-2: K09 permits a changed receipt in a same-attempt failed row; A8 has no exact immutability rule for that row. | defer, already recorded; no duplicate |
| BH6-6 | medium — carried pass-5 tenant-wide identity horizon/legacy-coverage finding: retention and migration coverage remain explicitly open. Immutable create-if-absent resolves the current concurrent-admission race, not that separate lifetime question. | defer, already recorded; no duplicate |
| BH6-7 | medium — carried EC4-1: wrong-case negotiation names remain unspecified in V1 and K06 treats them as implicit V1; authenticated capability comparison still prevents V2 downgrade. | defer, already recorded; no duplicate |
| BH6-8 | medium — carried BH4-5: the handoff does not explicitly name the changed codec-02 ActorBundleReadbackHash preimage defined in A5. | defer, already recorded; no duplicate |
| BH6-9 | medium — carried BH4-6: fixed K02/K07 marker keys differ from production derivations; their exact hashes and framing-only scope remain unchanged. | defer, already recorded; no duplicate |
| BH6-10 | medium — carried BH4-8/BH1-9: the preserved epic-context condensation omits zero state writes/checkpoint advancement for current projections. | defer, already recorded; preserve user context |
| BH6-11 | medium — carried BH4-9/BH2-9: the preserved epic context omits explicit full-replay/incremental declarations and fallback. | defer, already recorded; preserve user context |
| BH6-12 | high — carried BH4-10/BH1-10: the preserved epic context omits Current-only mutation, implemented/current snapshot prerequisites and denied-view non-disclosure. | defer, already recorded; preserve user context |
| EC6-1 | medium — carried EC4-1: same negotiation-property casing finding as BH6-7. | defer, already recorded; no duplicate |
| EC6-2 | medium — carried EC4-3/BH1-9: same current-projection zero-write/checkpoint omission as BH6-10. | defer, already recorded; preserve user context |
| EC6-3 | medium — carried EC4-4/BH2-9: same handler capability/fallback omission as BH6-11. | defer, already recorded; preserve user context |
| EC6-4 | high — carried EC4-5/BH1-10: same Current-only action and snapshot prerequisites omission as BH6-12. | defer, already recorded; preserve user context |
| EC6-5 | high — carried EC4-6/BH1-10: same denied-view non-disclosure omission as BH6-12. | defer, already recorded; preserve user context |

All findings received individual verdicts before grouping. The only new surviving group is BH6-1, recorded once in the deferred ledger; existing routes remain unchanged. No further implementation patch is required. Documentation execution is complete and the sprint story is ready for review. This does not approve the normative artifact or authorize Story 6.6.

## Verification

**Commands:**
- `git diff --check` -- expected: no whitespace errors in changed documents.
- `rg -n 'BH37-(1|2|6|7|8)|UNAPPROVED' _bmad-output/implementation-artifacts/spec-6-5a-event-contract-writer-and-migration-evidence.md _bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` -- expected: five dispositions in 6.5a and the original unapproved receipt.

**Manual checks (if no CLI):**
- Compare the 6.5a candidate's limits and codec references against §§2, 4, 7, 8, and 10 of the existing normative draft; inspect the five triage rows and verification vectors without changing signed fixture bytes.
