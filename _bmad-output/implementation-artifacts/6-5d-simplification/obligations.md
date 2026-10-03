# Story 6.5d obligations and exact wire reference

This file is normative supporting documentation for the compact candidate. Archived reviewed defect evidence is preserved unchanged; replacement targets below describe the new contract, without making archived local model mechanisms binding. All listed local function proofs use fixture authorization/transactions only; production cryptography and provider crash behavior require D9's approved runtime gate.

## Pass-1 owned groups

| Group | Replacement |
| --- | --- |
| G-A legacy scope | D5 scopes/shards/cutover/tombstones and D3 capsule identity |
| G-B drain limit | D3 immutable source/resolution and same-owner successor |
| G-C hold reasons | D4 precedence and D8 complete exit table |
| G-D resume intent | D3 same committed roster/MessageIds, no command execution |
| G-E capacity queue | D6 whole batch and one global ticket queue |
| G-F long stream | D5 activation/limits/idle capability exit |
| G-G schema/capability | D5 destination and D6 exact quota capability |
| G-H held delivery | D7 same owner capture/redrive/repair/quarantine |
| G-I inventory | D8 reserve-before-owner registry/current reason/global epoch |
| G-J codecs | retained exact wire schemas/literals below and canonical internal controls |

## All 54 routed pass-2 dispositions

Each row replaces a verified acceptance concern from the archived original disposition. A redundant raw report still has its own row and one owner. Raw VG2-1/5/6/7, BH2-11/12/13/14/15/17, E2-27/33/37 were not routed among these 54; they remain owned by Story 6.5/unowned source review. E2-27's charge-attach behavior is nevertheless retained in D6; no rejected raw assertion is silently counted as a routed finding. None of the routed findings was historically rejected: the archived register classifies all 54 as replacement.

<!-- pass2-dispositions-start -->

| Finding | Disposition / evidence | Replacement and verification |
| --- | --- | --- |
| VG2-2 | replacement; archived D7 enforces `unidentifiedCaptureCeiling`; archived D12 exercises exact fill and +1 refusal. | D6; ledger_fit/reserve_charge/batch, literal charge/counter schemas |
| VG2-3 | replacement; archived D3 checks drain-limit pending exit and reason; archived D12 covers active versus resolved. | D3/D4; authenticated status precedence, exact drain source/resolution and bounded owner completion |
| VG2-4 | replacement; Historical two-directory move mechanics are obsolete; the acceptance concern is tenant blocking and deployment fairness, now proved in one queue. | D6; one queue, queue_reserve/queue_materialize/queue_turn, ticket decode |
| VG2-O1 | replacement; Historical model/contract contradiction is replaced by the same one-queue function and wire schema used for tests; no queue moves remain. | D6; one queue, queue_reserve/queue_materialize/queue_turn, ticket decode |
| VG2-O2 | replacement; archived D3 says pending/unknown ignore exhaustion unless an active drain-limit record exists. | D3/D4; authenticated status precedence, exact drain source/resolution and bounded owner completion |
| VG2-O3 | replacement; archived D3's resolution closes the exact drain-limit epoch before a larger limit activates. | D3/D4; authenticated status precedence, exact drain source/resolution and bounded owner completion |
| BH2-1 | replacement; archived D9 precondition exposes a buildable handle/source/head/ordinal chain; server signing removes caller reconstruction. | D3; begin_resume/resume_step/cancel/reclaim and capsule_make/restore/legacy_transition |
| BH2-2 | replacement; archived D11 uses discriminated hashed actor IDs and separate tenant/deployment routes. | D8; reserved original subject, one global epoch, authoritative owner views, inventory_page |
| BH2-3 | replacement; archived D2 supplies below-bound disposition, noncircular target fingerprint, and activation-hold semantics. | D5; replay below/at/above boundaries and idle incremental-bootstrap |
| BH2-4 | replacement; archived D7 atomically reserves the whole pin batch; archived D8 waits hold no resource and cannot deadlock. | D6; ledger_fit/reserve_charge/batch, literal charge/counter schemas |
| BH2-5 | replacement; D5 permits only configuration restoration plus fresh zero-send/exact-byte proof. | D4; membership exact-byte/fresh-zero-send function |
| BH2-6 | replacement; archived D1/archived D3 name owners, triggers, and evidence/conflict exits; no hold is silently abandoned. | D3/D4; authenticated status precedence, exact drain source/resolution and bounded owner completion |
| BH2-7 | replacement; archived D10 writes/read-backs the capsule before cleanup and fences Operations retry against resume. | D3; begin_resume/resume_step/cancel/reclaim and capsule_make/restore/legacy_transition |
| BH2-8 | replacement; archived D9 retains one rolling active charge/state, includes the next limit, refunds closed windows, and records only success. | D3; begin_resume/resume_step/cancel/reclaim and capsule_make/restore/legacy_transition |
| BH2-9 | replacement; archived D4 uses 256 shards, bounded retries, a measured horizon, and a cutover that ends legacy fallback reads. | D5; scope_admit/compact/status/expire; eight-loss/cutover normative gate |
| BH2-10 | replacement; archived D1/archived D11 define gateway subjects, re-evaluation, removal evidence, and capacity reservation. | D8; reserved original subject, one global epoch, authoritative owner views, inventory_page |
| BH2-16 | replacement; Same verified defect as VG2-O2; archived D3 owns its single resolution. | D3/D4; authenticated status precedence, exact drain source/resolution and bounded owner completion |
| BH2-18 | replacement; Quota/quarantine external binary identities are retained literally; private hold entry/index codecs are obsolete and map to canonical registry/current owner. | D6; ledger_fit/reserve_charge/batch, literal charge/counter schemas |
| BH2-19 | replacement; archived D4 returns `admission_evidence_hold`; archived D13 classifies it as BC-15 in slice 2. | D5; scope_admit/compact/status/expire; eight-loss/cutover normative gate |
| E2-1 | replacement; archived D3 keys drain-limit records by window and limit epoch. | D3; begin_resume/resume_step/cancel/reclaim and capsule_make/restore/legacy_transition |
| E2-2 | replacement; archived D3 atomically records budget consumption/hold pointer and reconciles lost acknowledgement. | D3; begin_resume/resume_step/cancel/reclaim and capsule_make/restore/legacy_transition |
| E2-3 | replacement; archived D3 evidence conflict precedes pending/unknown scalar projection and is inventory-visible. | D3/D4; authenticated status precedence, exact drain source/resolution and bounded owner completion |
| E2-4 | replacement; archived D10 capsule stores authenticated rejection classification before drain evidence disappears. | D3; begin_resume/resume_step/cancel/reclaim and capsule_make/restore/legacy_transition |
| E2-5 | replacement; archived D10 missing-source policy is stable non-resumable incident, not BC-02 command replay. | D3; begin_resume/resume_step/cancel/reclaim and capsule_make/restore/legacy_transition |
| E2-6 | replacement; archived D10 handle binds tenant/domain/aggregate/tracking/range independent of expiring status or reused MessageId. | D3; begin_resume/resume_step/cancel/reclaim and capsule_make/restore/legacy_transition |
| E2-7 | replacement; Same verified defect as BH2-1; archived D9.1 is the single replacement. | D3; begin_resume/resume_step/cancel/reclaim and capsule_make/restore/legacy_transition |
| E2-8 | replacement; archived D11 requires bounded direct/dead-letter capture even when broker redelivery is unbounded. | D7; capture/redrive/reconcile/erase functions and exact carrier interval/provider-quarantine gate |
| E2-9 | replacement; archived D11 requires exact provider quarantine through 256 MiB or broker pre-rejection/readiness failure. | D7; capture/redrive/reconcile/erase functions and exact carrier interval/provider-quarantine gate |
| E2-10 | replacement; archived D11 defines a deployment-scoped redrive route with no tenant placeholder. | D8; reserved original subject, one global epoch, authoritative owner views, inventory_page |
| E2-11 | replacement; archived D11's actor IDs and routes cannot collide with tenant `deployment`. | D8; reserved original subject, one global epoch, authoritative owner views, inventory_page |
| E2-12 | replacement; Same verified defect as BH2-10; archived D11 entry lifecycle owns it. | D8; reserved original subject, one global epoch, authoritative owner views, inventory_page |
| E2-13 | replacement; archived D11's bounded actor directory and one epoch lease make reconciliation/gauges complete and single-owner. | D8; reserved original subject, one global epoch, authoritative owner views, inventory_page |
| E2-14 | replacement; Separately synchronized reason tag is obsolete; reason derives from current authenticated owner, cursor generation includes that owner revision. | D8; reserved original subject, one global epoch, authoritative owner views, inventory_page |
| E2-15 | replacement; Parking keeps the original row in the one enumerable deployment queue; no key-store scan or side parked set. | D6; one queue, queue_reserve/queue_materialize/queue_turn, ticket decode |
| E2-16 | replacement; archived D7 uses ledger reservation plus reservation-bound pin CAS across different backends. | D6; ledger_fit/reserve_charge/batch, literal charge/counter schemas |
| E2-17 | replacement; The old duplicate binary count is obsolete; exact canonical JSON array and row/ticket/byte invariants have one parse authority. | D6; one queue, queue_reserve/queue_materialize/queue_turn, ticket decode |
| E2-18 | replacement; archived D4 names the compactor, hourly/75% trigger, authenticated expiry, and decrement. | D5; scope_admit/compact/status/expire; eight-loss/cutover normative gate |
| E2-19 | replacement; Same verified defect as BH2-9; archived D4 sharding and bounded loser outcome own it. | D5; scope_admit/compact/status/expire; eight-loss/cutover normative gate |
| E2-20 | replacement; archived D4 status on a tombstone returns stable 410 and never falls back. | D5; scope_admit/compact/status/expire; eight-loss/cutover normative gate |
| E2-21 | replacement; Same verified defect as BH2-19; archived D4/archived D13 own its outcome and slice. | D5; scope_admit/compact/status/expire; eight-loss/cutover normative gate |
| E2-22 | replacement; Same verified defect as BH2-3; archived D2 requires `continue-full-replay` below 75%. | D5; replay below/at/above boundaries and idle incremental-bootstrap |
| E2-23 | replacement; archived D2 configuration revision schedules re-evaluation/bootstrap even with no new events. | D5; replay below/at/above boundaries and idle incremental-bootstrap |
| E2-24 | replacement; Verified unchanged A8 policy: both preexisting immutable outputs required; missing output is non-resumable incident, never rerendered. | D3/D4; authenticated status precedence, exact drain source/resolution and bounded owner completion |
| E2-25 | replacement; archived D9's active window charge explicitly includes the next drain-limit record/resolution. | D3; begin_resume/resume_step/cancel/reclaim and capsule_make/restore/legacy_transition |
| E2-26 | replacement; archived D9 rejection writes no audit/charge; exact successful retry reuses one audit. | D3; begin_resume/resume_step/cancel/reclaim and capsule_make/restore/legacy_transition |
| E2-28 | replacement; archived D3 classifies every shipped drain reason as automatic, capsule-resumable, or evidence incident. | D3/D4; authenticated status precedence, exact drain source/resolution and bounded owner completion |
| E2-29 | replacement; archived D3 requires Admin list-time join with authoritative status/hold evidence. | D3/D4; authenticated status precedence, exact drain source/resolution and bounded owner completion |
| E2-30 | replacement; Same verified defect as VG2-O2; archived D3's row 8 owns it. | D3/D4; authenticated status precedence, exact drain source/resolution and bounded owner completion |
| E2-31 | replacement; Same verified defect as VG2-O1; archived D8/archived D12 own it. | D6; one queue, queue_reserve/queue_materialize/queue_turn, ticket decode |
| E2-32 | replacement; archived D7 maps negative/overflow arithmetic to stable `publication_pin_capacity_hold` before comparison with zero mutation; archived D12 executes negative and u64-sum-overflow cases and proves counters unchanged. | D6; ledger_fit/reserve_charge/batch, literal charge/counter schemas |
| E2-34 | replacement; Verified exact C5 contradiction: permanent operation fence belongs only to terminal closure; disable/reject resume phases are window-scoped. | D3; begin_resume/resume_step/cancel/reclaim and capsule_make/restore/legacy_transition |
| E2-35 | replacement; archived D10 capsule predates the request, so archived D9 tag `06` is noncircular. | D3; begin_resume/resume_step/cancel/reclaim and capsule_make/restore/legacy_transition |
| E2-36 | replacement; archived D3 precedence maps mixed class-01/class-02 to `terminal_evidence_hold`. | D3/D4; authenticated status precedence, exact drain source/resolution and bounded owner completion |
| E2-38 | replacement; archived D1 plus archived D2–archived D11 give G-B, G-E, and G-I storage, bounds, activation, inventory, owner, and exits. | D1–D8 lifecycle table, one-owner phases and ledger queue; all four scenario rows |

<!-- pass2-dispositions-end -->

## Exact retained binary framing and schemas

All external records use ASCII domain including one final NUL, codec byte 01, big-endian u16 field count, then one-byte tags 01..count ascending and the ordered typed fields below. U is u32 UTF-8 byte count plus exact bytes, B is u32 byte count plus bytes, B32 exactly 32 bytes, N nonnegative checked u64, P u32, I signed i32, Q signed i64 UTC ticks. O(X) is 00 absent or 01 plus X; no null variant. Reject duplicate/missing/extra/reordered tags, invalid UTF-8, negative/overflow/count inconsistency or trailing bytes before allocation. Ordinary identifiers ≤1,024 UTF-8 bytes, Operator subject ≤256. Imported C1/C2/C5 signatures, P-256/P1363 carrier, purpose/issuer/SPKI/time/revocation are unchanged. Purpose 2d is activation, resume/window and redrive only; first-send resolution keeps purpose 2a.

Table field order is exact tag order; types are given in parentheses. The fixture hex is literal in known-answers.json and independently reconstructed without running the archived verifier. Dynamic roots below have semantic definitions; opaque fixture hashes are identified in the descriptors, never claimed provider proofs.

| Answer / domain (HX-EV- prefix) | Exact fields | Complete cap |
| --- | --- | --- |
| D06-activation / FULL-REPLAY-ACTIVATION-2 | issuer(U),domain(U),fingerprint(B32),generation(N),predecessor(B32),rows(B),inventoryUtc(Q),operator(U),signUtc(Q) | 1 MiB; rowCount 0..943 |
| D12-legacy-claim / COMMAND-SCOPE-LEGACY-2 | tenant(U),execution(U),domain(U),aggregate(U),commandType(U),payloadHash(B32),claimUtc(Q),expiryUtc(Q),cohort(N),cutoverHash(B32) | 8 KiB |
| D12-cutover / LEGACY-SCOPE-CUTOVER-1 | tenantOrStar(U),domain(U),cohort(N),startUtc(Q),cutoverUtc(Q),horizonSeconds(N),emptyOwnerRoot(B32),predecessor(B32) | 4 KiB |
| D12-usage / SCOPE-SHARD-USAGE-1 | tenant(U),shard(N),requiredCount(N),tombstoneCount(N),chargedBytes(N),generation(N),predecessor(B32) | 2 KiB |
| D12-tombstone / COMMAND-SCOPE-TOMBSTONE-2 | tenant(U),execution(U),scope(B32),input(B32),compactedRecord(B32),compactedUtc(Q),expiryUtc(Q),shard(N) | 4 KiB |
| D14-drain-limit / PUBLICATION-DRAIN-LIMIT-2 | tenant(U),scope(B32),operation(U),window(N),limit(N),drainHead(B32),outcomeHead(B32),outcomeRevision(N),state(U),decisionUtc(Q) | 4 KiB |
| D14-drain-resolution / PUBLICATION-DRAIN-LIMIT-RESOLUTION-1 | tenant(U),scope(B32),limitHash(B32),outcome(U),successorIntent(B32),generation(N),owner(U),utc(Q) | 4 KiB |
| D16-membership-resolution / FIRST-SEND-MEMBERSHIP-RESOLUTION-1 | tenant(U),scope(B32),position(N),MessageId(U),pin(B32),generation(N),predecessor(B32),membership(B32),freshZeroSend(B32),disposition(U),issuer(U),utc(Q) | 16 KiB |
| D29-capability / PUBLICATION-RETENTION-CAPABILITY-2 | deployment(U),revision(N),backendDescriptor(B),tenantCeiling(N),deploymentCeiling(N),unidentifiedReserve(N),unidentifiedCeiling(N),overhead(N),scopeCeiling(N),predecessor(B32),effectiveUtc(Q),shards(N),tombstoneSeconds(N),waitCeiling(N),quarantineMax(N) | 64 KiB |
| D29-charge / PUBLICATION-CHARGE-2 | deployment(U),accountKind(U),accountId(U),objectKey(B32),kind(U),length(N),recordedOverhead(N),amount(N),capabilityRevision(N),generation(N),state(U),predecessor(B32),transferOwner(O(B32)),utc(Q),transferred(N) | 4 KiB |
| D29-counter / PUBLICATION-COUNTER-1 | deployment(U),kind(U),id(U),usedBytes(N),chargeCount(N),generation(N),predecessor(B32),utc(Q) | 4 KiB |
| D29-pin-batch / PIN-BATCH-RESERVATION-2 | tenant(U),scope(B32),candidateRoot(B32),pinCount(N),rows(B),total(N),capabilityRevision(N),tenantPredecessor(B32),poolPredecessor(B32),deploymentPredecessor(B32),state(U),generation(N),utc(Q) | 64 KiB; pinCount 1..59 |
| D36-policy / SUBSCRIPTION-DELIVERY-POLICY-3 | deployment(U),component(U),topic(U),subscription(U),revision(N),predecessor(B32),relation(U),captureMode(U),deadLetterTopic(O(U)),localAttempts(N),config(B32),source(U),utc(Q) | 16 KiB |
| D36-quarantine / CARRIER-QUARANTINE-2 | scopeKind(U),tenant(O(U)),subscription(U),reason(U),length(N),bodyHash(B32),headerManifest(B),backend(U),objectKey(U),archiveAuthority(B32),MessageId(O(U)),utc(Q),disposition(U),sourceReceipt(B32) | 128 KiB |
| D36-redrive / REDRIVE-REQUEST-2 | issuer(U),scopeKind(U),tenant(O(U)),heldKey(B32),expectedCount(N),operator(U),utc(Q) | 3 KiB payload plus 8 KiB signature envelope |
| D45-carrier / PUBLICATION-RESUME-CARRIER-1 | tenant(U),handle(U),expectedSource(B32),idempotencyKey(U),reason(U) | 2 KiB |
| D45-request / PUBLICATION-RESUME-3 | issuer(U),tenant(U),execution(U),scope(B32),eligibility(U),holdOrCapsule(B32),A8head(B32),nextOrdinal(N),predecessorAudit(B32),handle(U),stableIdentity(B32),carrierHash(B32),operator(U),utc(Q),expiry(Q) | 4 KiB payload plus 8 KiB signature envelope |
| D45-window / PUBLICATION-WINDOW-2 | tenant(U),scope(B32),operation(U),window(N),previousClosure(B32),priorControlHash(B32),requestIdentity(B32),admissionRoot(B32),retryPolicy(B32),drainBase(N),issuer(U),openedUtc(Q),capability(B32) | 16 KiB |
| D45-closure / PUBLICATION-WINDOW-CLOSURE-3 | tenant(U),scope(B32),closedWindow(N),summaries(B),rejectReceipt(B32),disableReceipt(B32),emptyRoot(B32),attemptRoot(B32),AuthMode(U),previousHistory(B32),utc(Q) | 64 KiB |
| D45-attempt-set / WINDOW-ATTEMPT-SET-1 | tenant(U),scope(B32),window(N),rosterRoot(B32),evidenceCount(N),rows(B),semanticRoot(B32),sealUtc(Q) | 64 MiB; ≤11,328 rows |
| D45-audit / PUBLICATION-RESUME-AUDIT-4 | tenant(U),execution(U),ordinal(N),identity(B32),carrierHash(B32),priorControl(B32),closure(O(B32)),openedWindow(N),newLimit(N),utc(Q) | 4 KiB |
| D46-chunk / LEGACY-RESUME-CAPSULE-CHUNK-1 | capsuleIdentity(B32),ordinal(N),rowCount(N),rows(B),rowRoot(B32) | 64 KiB; 1..61 rows |
| D46-capsule / LEGACY-RESUME-CAPSULE-2 | tenant(U),domain(U),aggregate(U),tracking(U),execution(O(U)),correlation(U),commandType(U),classification(U),start(N),end(N),eventCount(I),eventRoot(B32),chunkManifest(B),cleanupSource(U),sourceHash(B32),utc(Q) | 128 KiB; 1..17 chunks / 1..1,000 events |
| D17-destination-config | exact canonical destination JSON defined in candidate D5 | 65,536 bytes |

Drain state pending/unknown/failed; resolution outcome resumed/head-advanced/terminal. Member resolution disposition ContinueSamePin/FirstSendMembershipChangedHold. Charge kind pin-batch/side-record/retained-object/oversize-quarantine/resume-window; state staged/active/released. Transferred marker 0 means absent owner, marker 1 means resume-window with present same stable owner in every generation, including released; successor-readback activates stage and releases predecessor exactly once. Counter kind tenant/capture-scope/tenant-pool/deployment/unidentified; tenant counter ID may contain `tenant:` plus 1,024 bytes (1,031 maximum), other IDs ≤1,024. Pin-batch state reserved/installed/released; root SHA256(exact uncounted rows), pinCount equals row count and total checked sum. Subscription relation initial/successor (generation 1 initial, later successor), capture mode dead-letter-capture/direct-held-capture; dead-letter topic present only for first mode; source dapr-configuration/broker-api. Quarantine reason invalid-header-value/invalid-carrier/oversize-carrier, disposition terminal-quarantine; backend ≤1,024 and key ≤4,096. Capsule classification success-events/rejection-events, cleanup drain-exhaustion/operator-reconciliation; positive count exactly end-start+1. Legacy signed request scope/A8head and window invocation claim hash are zero; no fabricated A8 authority.

Nested exact payloads: activation rows `u32 count ||` sorted `U route || U disposition || N count || N readable || N accounting || O(B32 incrementalCapability)`; hash present exactly for incremental. Pin rows `u32 position || U MessageId || B32 pin || N length || N amount`, counted by pinCount (no inner count). Closure summary `u32 count ||` sorted `u32 position || N finalLocalAttempt || B32 lastDefinitiveResult`. Attempt rows `u32 position || N localOrdinal || N observationOrdinal || U register|unknown|result || B32 parent || B32 sendId || B32 evidence`, counted by evidenceCount; sorted first three fields, every attempt starts registration/0, optional Unknown/1 then definitive result, contiguous attempts/no Accepted successor. Semantic root SHA256(`"HX-EV-WINDOW-ATTEMPTS-2\0" || 01 || U tenant || B32 scope || N window || B32 roster || N count || B rows`). Window unresolved root SHA256(`"HX-EV-PUBLICATION-UNRESOLVED-1\0" || 01 || u32 count ||` sorted `u32 position || U MessageId || B32 committedByteHash`). History successor SHA256(`"HX-EV-PUBLICATION-WINDOW-HISTORY-1\0" || 01 || B32 previous || B exactClosure || B exactBrokerAuthentication`). Neither window nor audit includes successor control hash.

Capsule rows `N sequence || U MessageId || B32 StoredDigest` ascending unique contiguous sequence/MessageId. Chunk root SHA256(`"HX-EV-LEGACY-RESUME-CHUNK-ROWS-1\0" || 01 || B exactRows`). Manifest `u32 chunkCount ||` sorted `N ordinal || N firstSequence || N rowCount || B32 chunkHash || N encodedLength || U chunkAddress`; exact endpoints/count/length and no gaps/overlap/trailing bytes. Event root SHA256(`"HX-EV-LEGACY-RESUME-EVENTS-2\0" || 01 || B(u32 totalCount || all exactRows)`). Quarantine header manifest `u32 count ||` ≤128 rows `U headerName || N valueLength || B32 valueHash`; aggregate names/value lengths ≤64 KiB, exact worst encoded manifest 71,172 bytes, no raw forbidden value. AuthMode exactly SignedCarrier/BackendCas with unchanged C5 authentication framing and source order. C1 destinationId, imported A/B/C public bytes and signature carrier remain byte-identical and are referenced directly in the unchanged 6.5c C1/C2/C5 definitions.

## Retained addresses

K(name, fields) is literal prefix below plus lowercase SHA256(`ASCII name including one NUL || 01 || fields`). Never raw concatenated variable strings. Shared scope key is the imported exception, SHA256(U tenant || U execution) without domain/codec. Create-once accepts byte-identical readback only; heads use exact predecessor and checked generation. New canonical controls have these stable existing keys, with no migration from private fixtures that were never deployed.

| Name / prefix | Exact fields / mutation |
| --- | --- |
| HX-EV-FULL-REPLAY-ACTIVATION-KEY-1 / full-replay-activation: | U domain, B32 fingerprint, N generation; immutable |
| HX-EV-PUBLICATION-DRAIN-LIMIT-KEY-1 / publication-drain-limit: | B32 scope, N window, N limit; immutable |
| HX-EV-PUBLICATION-DRAIN-RESOLUTION-KEY-1 / publication-drain-resolution: | B32 scope, B32 limitHash; immutable |
| HX-EV-SCOPE-SHARD-USAGE-KEY-1 / scope-shard-usage: | U tenant, N shard; transactional head |
| HX-EV-LEGACY-SCOPE-CUTOVER-KEY-1 / legacy-scope-cutover: | U tenantOrStar, U domain, N generation; immutable; head variant HX-EV-LEGACY-SCOPE-CUTOVER-HEAD-KEY-1 / legacy-scope-cutover-head: omits generation |
| HX-EV-FIRST-SEND-MEMBERSHIP-KEY-1 / first-send-membership: | U tenant, B32 scope, N position, U MessageId, B32 pin; versioned outcome head |
| HX-EV-DESTINATION-CONFIG-KEY-1 / destination-config: | U deployment, U component, U topic, N revision; immutable; head variant HX-EV-DESTINATION-CONFIG-HEAD-KEY-1 / destination-config-head: omits revision |
| HX-EV-PUBLICATION-CAPABILITY-KEY-1 / publication-retention-capability: | U deployment, N revision; immutable; head variant HX-EV-PUBLICATION-CAPABILITY-HEAD-KEY-1 / publication-retention-capability-head: omits revision |
| HX-EV-PUBLICATION-CHARGE-KEY-1 / publication-charge: | U deployment, U accountKind, U accountId, B32 objectKey; transactional head |
| HX-EV-PUBLICATION-COUNTER-KEY-1 / publication-counter: | U deployment, U counterKind, U counterId; transactional head |
| HX-EV-PIN-BATCH-RESERVATION-KEY-1 / pin-batch-reservation: | B32 scope, B32 candidateRoot; transactional head |
| HX-EV-CAPACITY-SUBJECT-1 / capacity-subject: | B32 scope, B32 immutablePlan; stable subject |
| HX-EV-PIN-CAPACITY-QUEUE-KEY-1 / pin-capacity-queue: | U deployment, U literal deployment; one logical queue head, optional bounded storage shards under same transaction |
| HX-EV-PUBLICATION-RESUME-CLAIM-KEY-1 / publication-resume-claim: | U tenant, U execution, B32 requestIdentity; immutable through retry horizon |
| HX-EV-PUBLICATION-RESUME-STATE-KEY-1 / publication-resume-state: | U tenant, U execution; canonical execution control CAS |
| HX-EV-PUBLICATION-WINDOW-KEY-1 / publication-window: | B32 scope, N window; immutable |
| HX-EV-PUBLICATION-WINDOW-CLOSURE-KEY-1 / publication-window-closure: | B32 scope, N window; immutable |
| HX-EV-PUBLICATION-RESUME-AUDIT-KEY-2 / publication-resume-audit: | U tenant, U execution, B32 requestIdentity; successful immutable audit |
| HX-EV-WINDOW-ATTEMPT-SET-KEY-1 / window-attempt-set: | U tenant, B32 scope, N window; immutable sealed source, active C2 collection separately owned |
| HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1 / legacy-resume-capsule-chunk: | B32 capsuleIdentity, N ordinal; immutable |
| HX-EV-LEGACY-RESUME-CAPSULE-KEY-2 / legacy-resume-capsule: | B32 capsuleIdentity; immutable |
| HX-EV-SUBSCRIPTION-POLICY-KEY-1 / subscription-policy: | U deployment, U component, U topic, U subscription, N revision; immutable; head variant HX-EV-SUBSCRIPTION-POLICY-HEAD-KEY-1 / subscription-policy-head: omits revision |
| HX-EV-HELD-DELIVERY-KEY-2 / held-delivery: | U scopeKind, U deployment, O(U tenant), U component, U topic, U subscription, B32 carrier; canonical held control CAS |
| HX-EV-CARRIER-QUARANTINE-KEY-1 / carrier-quarantine: | B32 heldKey, B32 carrier; immutable |

CapsuleIdentity = SHA256(`"HX-EV-LEGACY-RESUME-CAPSULE-IDENTITY-1\0" || 01 || U tenant || U domain || U aggregate || U tracking || B32 sourceHash`). For legacy execution control, execution identity is the lowercase capsuleIdentity rather than a reused MessageId; retained handle also binds manifest hash. Registry/cursor use their D8 exact canonical schemas under the existing deployment discovery adapter. Actor IDs are scope-discriminated; reserved placeholders use the same eventual owner locator. No provider receipt is minted from a locally computed hash in production.

## Retained and retired verification obligations

| Prior family/probe | Disposition in simplified design |
| --- | --- |
| 24 retained records and framed external keys | retained; known-answers.json literals/descriptor encode/decode/hash plus independent reconstruction; dynamic roots constructed from actual semantic rows |
| D45-state / PUBLICATION-RESUME-STATE-3 | obsolete private head; maps to execution-control/1; historical byte image retained only as literal prior-hash fixture input, no deployed migration |
| D46-recovery / LEGACY-PUBLICATION-RECOVERY-3 | obsolete separate mutable fence; maps to legacy recovery phase in shared execution owner |
| D36-held / HELD-DELIVERY-4 | obsolete private mutable representation; same physical held key/public identity maps to held-control/1 |
| D31-wait / PIN-CAPACITY-WAIT-2 and D31-queue / PIN-CAPACITY-QUEUE-4 | obsolete paired-directory local structures; one canonical deployment queue row/ticket; fairness/refund/parking behavior retained |
| D37-entry/index/directory/key | obsolete synchronized local entry/index/hash families; bounded registry/current-owner derivation and scope-authorized snapshot cursor |
| D45-origin/preparation/preparation-head | obsolete copies of predecessor/successor process images; original request/phase/intent in execution control, byte-only restart tests |
| D45-invocation | obsolete independent local coordination body; exact deterministic existing invocation identity/address retained below, pending intent in execution control |
| D36-capture-origin/capture-preparation | obsolete independent private capture coordination; original observation/policy/capture intent in held control; charge-only/object-written restart tests |
| D36-attempt/current-request | obsolete separate fixed-slot local coordination; exact external signed request bytes and count-bound attempt share held CAS |
| D36-repair/cleanup/repair-interest/native-cleanup | obsolete second charged repair hold/cleanup family; explicit absent/corrupt repair and bounded deletion intent/receipts in original held record |
| D31-preparation/authority/owners/predecessor/receipt | obsolete paired owner indexes and 74 MiB predecessor copies; one queue row under single ledger transaction, immutable operation/ledger recovery authority |
| All historical key-only answers for above private families | obsolete/mapped to same owner keys or registry/queue keys; never deployed wire format, no migration invented |
| Historical 180 source-text guard mutations and fixed finding/probe counts | obsolete; behavioral refusal/restart/corruption assertions exercise actual simplified functions, zero review findings allowed |
| 14 historical status cases / four frozen matrix rows | retained; 16 concrete precedence cases; resume effects, legacy exact range/classification, queue/refund, held redrive and idle replay checks |
| 67 lifetime resumes / >130 failed redrives / monotone counts | retained using same byte-only backend plus fixed-horizon reclamation and fixed owner/charge set |
| Scope/cursor isolation, evidence and pre-effect unchanged refusals | retained; exact persisted bytes inspected after fresh deserialize; unavailable/forged data has no authority |
| Crypto, real broker and Dapr/PostgreSQL atomic/crash behavior | retained as runtime gate, unproved by fixture model; no production readiness claim |

Deterministic invocation identity is the existing SHA256(`"HX-EV-PUBLICATION-INVOCATION-1\0" || 01 || B32 unchangedOrSelectedWindowClaim || N successfulOrdinal || N newDrainLimit || B32 requestIdentity || B32 currentUnresolvedRoot`); address K(HX-EV-PUBLICATION-INVOCATION-KEY-1, U tenant, U execution, B32 invocationIdentity), prefix publication-invocation:. Legacy claim hash is zero and root is capsule eventRoot. Existing coordinator registers this exact identity before dispatch; it is retained in owner's invoke intent and referenced existing drain authority. Exact retry cannot create another identity. No new invocation codec family is needed.

## Evidence and independent checks

Run verify.py directly. Its byte snapshots restart Store from serialized canonical bytes, not returned process dictionaries. Literal frames are reconstructed independently during authoring and match all 23 previously published retained digests (plus newly exposed caller carrier), then checked by the verifier's separate frame implementation. Internal controls have complete literal JSON/hex/digest answers. Corrupted literal digest/framing, exceeded control cap, illegal legacy transition, bad queue ticket and invalid accepted partition each fail their owning assertion/decoder. Actual mutation of a fixture expected result must fail its scenario assertion; tests do not mirror source text.

Native auth strings beginning fixture-provider-only and fixture-purpose-2d-only establish deterministic test authorization only. Production requires approved profile, actual signatures and separately authenticated provider readback. Protected frozen intent/archive SHA audits and source/path audit belong to the parent acceptance pass. All AD-13/unapproved/source/runtime/test files remain unchanged.
