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

## Verification

**Commands:**
- `git diff --check` -- expected: no whitespace errors in changed documents.
- `rg -n 'BH37-(1|2|6|7|8)|UNAPPROVED' _bmad-output/implementation-artifacts/spec-6-5a-event-contract-writer-and-migration-evidence.md _bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` -- expected: five dispositions in 6.5a and the original unapproved receipt.

**Manual checks (if no CLI):**
- Compare the 6.5a candidate's limits and codec references against §§2, 4, 7, 8, and 10 of the existing normative draft; inspect the five triage rows and verification vectors without changing signed fixture bytes.
