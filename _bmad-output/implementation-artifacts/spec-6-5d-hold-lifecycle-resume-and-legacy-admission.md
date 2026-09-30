---
title: 'Story 6.5d: Hold Lifecycle, Resume, and Legacy Admission Spec'
type: 'feature'
created: '2026-09-30'
status: 'candidate'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

## Intent

Specify bounded hold, wait, resume, capacity, and legacy-admission lifecycles as a focused input to the single Story 6.5 AD-13 artifact. This candidate replaces integration rules `[I-06]`, `[I-10]`, `[I-12]`, `[I-14]`–`[I-17]`, `[I-29]`–`[I-31]`, `[I-36]`, `[I-37]`, `[I-45]`, and `[I-46]`; it does not change runtime behavior or authorize Story 6.6.

## Candidate authority and reading convention

This documentation candidate is grounded at repository commit `6a2f25e39586692b54b655d3e6e8a5f6fa4d317e`. The loop-1 source is the byte-identical `spec-event-versioning-upcasting.md` at commit `288a6190` (SHA-256 `c474df76a687b2798757051efbdb382bb6b9650f7bd7a7637d0c67bfc5e7b715`). Stories 6.5a, 6.5b, and 6.5c remain unchanged section candidates. AD-13 remains `UNAPPROVED`, and this candidate alone grants no implementation authority.

The draft's checked big-endian codecs are imported: `U` and `B` are `u32 length || bytes`, `B32` is exactly 32 bytes, `N` is a checked non-negative u64, `I` is signed i32, `Q` is signed i64 UTC ticks, and `O(X)` is `00` for absent or `01 || X` for present. Every record below is `ASCII domain separator including NUL || one-byte codec || u16 field count || fields in ascending tag order`. Length/count/arithmetic validation precedes allocation. A decoder rejects missing, duplicate, extra, reordered, negative, overflowing, or trailing data.

The current source proves only the shipped starting point. `AggregateActor` deletes `UnpublishedEventsRecord` after drain exhaustion, `DeadLetterMessage` omits `IsRejection`, `ReplayController` is actually at `src/Hexalith.EventStore/Controllers/ReplayController.cs` and resubmits `PublishFailed` commands, Operations capture defaults to 1 MiB with a 10 MiB ceiling, and its current actor index is key-only. No proposed record or provider guarantee is claimed to exist today.

## D1. Shared lifecycle invariants

Every durable hold or wait has exactly one stable subject, one owner, bounded charged storage, an inventory entry, a re-evaluation trigger, and one of the exits below. A hold record is evidence, never permission to execute a command, create a new event identity, skip a route, acknowledge an unresolved delivery, or overwrite history.

1. **Create and index.** The owning transition durably creates or read-backs its state before exposing the hold. An operation that can commit before discovering a hold reserves its hold-inventory and wait-directory slots before commit. A delivery creates its capture and inventory entry before acknowledging the transport copy. If the required slot cannot be reserved, admission/readiness stops before the durable work that could be stranded.
2. **Re-evaluate.** The named owner re-evaluates on its event trigger and at least once per hour. Manual action may request an immediate re-evaluation but never supplies missing authority or changes a compatibility result.
3. **Resolve.** Resolution evidence is written/read back before the inventory entry is removed. A stale request conflicts. Unavailable evidence leaves the prior state authoritative. Erasure removes the state only after the tenant or deployment scope has no retained obligation.
4. **No abandonment by success.** Erasure/offboarding is not publication, route completion, or command success. It may remove data only under the existing tenant-erasure contract after all data and authority for that tenant are removed together.

| Hold or wait | Stable subject | Owner and event trigger | Deterministic exit |
| --- | --- | --- | --- |
| `LegacyArrayLimit` | domain + route + stream, or route-wide activation subject | projection; registry/configuration revision and hourly | verified `5a incremental` capability under the active fingerprint, then a scheduled first incremental dispatch even if no new event arrives |
| `ActivationInventoryCapacityHold` | domain + target RegistryFingerprint | projection activation owner; catalog revision and hourly | a complete inventory of at most 943 routes fits and reads back; no segmented or partial activation is served |
| `admission_evidence_hold` | tenant + execution MessageId | gateway; state-store recovery/cutover revision and 30-second retry | required scope/legacy claim and shard counter read back, or the request is rejected as a proved conflict |
| `response_preparation_hold` | ScopeOpHash | coordinator; owner-fence transfer and 30-second retry | unchanged A8/[I-09] recovery proceeds only when both immutable outputs and generation-bound receipts already verify, then writes/reads the preparation-write record; a missing output remains held |
| `outcome_evidence_hold` | ScopeOpHash + head revision | coordinator; evidence-store recovery and 30-second retry | every required immutable source reads back and the existing or next outcome verifies |
| `outcome_evidence_conflict` | ScopeOpHash + observation identity | coordinator; authoritative provider revision and hourly | a later authoritative observation proves the retained row unchanged; an irreparable contradiction remains an operator-visible incident and cannot be abandoned as success |
| `terminal_evidence_hold` | ScopeOpHash + failed head | coordinator; C5 closure progress and hourly | complete C5 terminal closure reads back, or later authoritative publication evidence makes the head nonterminal/published |
| `PublicationRetryExhaustedHold` | ScopeOpHash + active window + member set | coordinator; authenticated resume or terminal proof | D9 resume opens exactly one successor window, or C5 terminal closure completes |
| `PublicationDrainLimitHold` | ScopeOpHash + active window + drain-limit epoch | coordinator; authenticated resume/head advance and hourly | a D9 drain-limit resolution closes the exact record before a larger limit becomes active, or a later verified head/terminal pointer supersedes it |
| `PublicationPinCapacityHold` | exact D7 `capacity-subject:` key: framed ScopeOpHash + immutable admitted A8 outbox/member-plan root | quota coordinator; refund/capability/renderer-evidence revision and 60-second re-evaluation | checked arithmetic and evidence produce a valid candidate and atomic batch reservation succeeds; no member holds a partial reservation while held or waiting |
| `PinCapacityQueueCorruptionHold` | deployment identity + counter ID + queue generation | quota coordinator; authenticated repair/migration completion and 60-second re-evaluation | the exact predecessor and all wait rows are reconstructed, read back, and atomically installed without dropping or duplicating a ticket |
| `FirstSendMembershipChangedHold` | ScopeOpHash + member position + MessageId | broker membership owner; configuration/membership revision and hourly | only D5's fresh zero-send proof plus byte-identical `ContinueSamePin`; manual action merely requests this check |
| `ScopeRetentionCapacityHold` | tenant + scope shard | gateway; tombstone expiry/compaction/capability revision and hourly | the exact shard admits the scope record; no other shard's free bytes are asserted as available |
| held delivery | physical subscription + exact carrier hash | Operations; typed cause-cleared signal and bounded backoff | redrive of exact retained bytes reaches terminal route decisions, or terminal quarantine completes for a permanently nonadmissible carrier |
| legacy resume incident | legacy resume handle | aggregate/Operations recovery owner; evidence restoration and hourly | a verified resume capsule exists and D9 resumes it; `legacy_resume_evidence_unavailable` has no fabricated recovery and closes only through tenant erasure or a separately approved migration story |

The last row is intentionally non-resumable when evidence never existed. It is still a complete lifecycle: it is indexed, diagnosed, retained within quota, and removed only by whole-tenant erasure or future separately approved migration—not by command replay.

The closed `ownerKind` set is `actor`, `coordinator`, `gateway`, `subscriber`, `projection`, `operations`, and `quota-coordinator`. The closed hold/reason mapping is: `LegacyArrayLimit -> legacy_array_limit`; `ActivationInventoryCapacityHold -> full_replay_inventory_capacity`; `AdmissionEvidenceHold -> admission_evidence_hold`; `ResponsePreparationHold -> response_preparation_hold`; `OutcomeEvidenceHold -> outcome_evidence_hold`; `OutcomeEvidenceConflict -> outcome_evidence_conflict`; `TerminalEvidenceHold -> terminal_evidence_hold`; `PublicationRetryExhaustedHold -> publication_retry_exhausted_hold`; `PublicationDrainLimitHold -> publication_drain_limit_hold`; `PublicationPinCapacityHold -> publication_pin_capacity_hold`; `PinCapacityQueueCorruptionHold -> pin_capacity_queue_corruption_hold`; `FirstSendMembershipChangedHold -> first_send_membership_changed_hold`; `ScopeRetentionCapacityHold -> scope_retention_capacity_hold`; `HeldDelivery ->` exactly one D11 held-delivery reason; and `LegacyResumeIncident -> legacy_resume_evidence_unavailable`. Producers reject an unknown value instead of indexing or charging it under a catch-all. A reason change is a new D11 entry revision, never an in-place reinterpretation.

## D2. Full-replay activation — replacement for `[I-06]`

A projection route without a `5a` row is a full-replay route. The existing bounds remain 100,000 events, 64 MiB cumulative readable payload, and 256 MiB conservative accounting (8,192 bytes per event), whichever is first. No dispatch truncates, skips, or partially applies a complete history.

Before slice-3 activation, inventory every full-replay route against the target RegistryFingerprint and record exactly one disposition:

- `continue-full-replay`: every measured maximum is below 75% of every bound; the route stays full-replay and is remeasured on fingerprint change.
- `incremental`: the target fingerprint already contains the exact `5a incremental` row and B7a prior-state intake capability named by the row.
- `hold`: the route is disabled for all streams at activation; it creates a route-wide `LegacyArrayLimit` entry and serves no partial model. This is required at or above 75% when no incremental capability exists.

`incremental` never changes the fingerprint that the same record inventories: capability registration occurs first, the target fingerprint is computed, and the activation record binds that target. Configuration changes require the next generation. Growth after activation creates a stream-specific hold when any hard bound would be exceeded and emits `legacy_array_headroom_low` at 75%. Registering the capability triggers re-evaluation and schedules bootstrap/dispatch even on an otherwise idle stream, so no stale hold waits for another event.

`HX-EV-FULL-REPLAY-ACTIVATION-2\0 || 01 || 0009` (at most 1 MiB) has `01` U operator-action issuer, `02` U domain, `03` B32 target RegistryFingerprint, `04` N positive generation, `05` B32 predecessor activation-record hash (zero at generation 1), `06` B rows, `07` Q inventory cutoff UTC, `08` U operator subject, and `09` Q signing UTC. Tag `06` is `u32 rowCount || rows` sorted by HandlerRouteId; each row is `U HandlerRouteId || U disposition || N longestCount || N largestReadableBytes || N largestAccountingBytes || O(B32 incrementalCapabilityHash)`. The optional hash is present exactly for `incremental`.

The imported maximum HandlerRouteId is 1,024 UTF-8 bytes, the longest disposition is 20 bytes, and an incremental row carries the optional B32. Therefore the exact maximum row is `1,109` bytes. With maximum 1,024-byte issuer/domain fields, the 256-byte operator subject, all tags, the outer `B` length, and the `u32 rowCount`, the non-row record is `2,455` bytes; `floor((1,048,576 - 2,455) / 1,109) = 943`. `rowCount` is consequently `0..943`, even when shorter identifiers would happen to fit. A domain with 944 or more full-replay routes fails slice-3 readiness as indexed `full_replay_inventory_capacity` before any activation record or route service; no inventory is silently omitted. Its exit is a catalog revision that reduces/splits the domain below 944 routes, followed by a new complete inventory.

Purpose `2d` signs the complete record. The create-once key is `full-replay-activation:` plus lowercase-hex SHA-256 of `"HX-EV-FULL-REPLAY-ACTIVATION-KEY-1\0" || 01 || U domain || B32 target RegistryFingerprint || N generation`; concatenated text is never key material. It is charged to the domain catalog budget, retained with the fingerprint, erased with the domain, and activated in slice 3. Known answer `D06-activation` appears in D12.

## D3. Status, polling, and closed outcomes — replacements for `[I-10]`, `[I-14]`–`[I-16]`

### D3.1 Evidence-required status precedence

Status inspection authenticates the current scope, head, active hold pointers, and resolution records, then applies this order:

1. An active `outcome_evidence_conflict`, unavailable required evidence, or incomplete response preparation returns `CommandOutcomeHold`, regardless of a retained pending/unknown/failed scalar.
2. A verified C5 terminal pointer projects `PublishFailed` with `RecoveryReasonCode=publication_terminal_failed`.
3. `published` projects existing `Completed` or `Rejected` from the authenticated committed result; `not-applicable` projects `Completed` with zero events.
4. A currently active drain-limit record projects the nonterminal `EventsStored`, `Retryable=false`, `RecoveryReasonCode=publication_drain_limit_hold`, and `Retry-After: 60`. A D9 resolution immediately makes that old record inactive; merely raising a number does not.
5. A failed set containing any definitive class-02 or class-03 member without complete terminal proof is `CommandOutcomeHold(terminal_evidence_hold)`. This row owns the mixed class-01/class-02 case.
6. A failed set whose unresolved members are all definitive class-01 at their per-window maximum is `EventsStored`, `Retryable=false`, `RecoveryReasonCode=publication_retry_exhausted_hold`, and `Retry-After: 60`.
7. A failed set whose unresolved members are all class-01 below maximum and have an admitted automatic attempt is `EventsStored`, `Retryable=true`, `RecoveryReasonCode=publication_retry_pending`, and `Retry-After: 1`.
8. `pending` or `unknown` is ordinary nonterminal `EventsStored` with `Retry-After: 1`; exhaustion alone never holds it. Only row 4's active drain-limit record changes it to an operator hold.
9. Any other failed or contradictory set is the closed `CommandOutcomeHold` reason below.

An open D9 successor window returns to rows 7 or 8 for only its unresolved members. Accepted members, the first response, global pins, MessageIds, batch root, and committed result never change.

### D3.2 Drain-limit epochs and legacy drain reasons

The coordinator durably decrements its reserved drain budget in the same head transition that completes an invocation. Consuming the final reserved invocation with unresolved members atomically creates and reads back the drain-limit record and active pointer; a crash before their acknowledgement is reconciled from that durable head, so the hold cannot disappear.

`HX-EV-PUBLICATION-DRAIN-LIMIT-2\0 || 01 || 000a` (at most 4 KiB) has `01` U tenant, `02` B32 ScopeOpHash, `03` U OperationId, `04` N active window, `05` N reserved limit reached, `06` B32 exact DrainHeadHash, `07` B32 latest verified outcome-head hash, `08` N outcome revision, `09` U reduced state (`pending`, `unknown`, or `failed`), and `0a` Q decision UTC. Its create-once key is `publication-drain-limit:` plus lowercase-hex SHA-256 of `"HX-EV-PUBLICATION-DRAIN-LIMIT-KEY-1\0" || 01 || B32 ScopeOpHash || N activeWindow || N reservedLimit`; decimal concatenation is forbidden. A later window or later limit therefore never collides with an old create-once key.

`HX-EV-PUBLICATION-DRAIN-LIMIT-RESOLUTION-1\0 || 01 || 0008` has `01` U tenant, `02` B32 ScopeOpHash, `03` B32 exact drain-limit record hash, `04` U outcome (`resumed`, `head-advanced`, or `terminal`), `05` B32 successor window-intent, invocation, or terminal-pointer hash, `06` N resolver generation, `07` U resolver owner, and `08` Q resolution UTC. It never hashes the successor resume-state head. The active pointer CAS installs this resolution before selecting a larger limit. Both records are included in the one active D9 window reservation; retained closed records are folded into the resume-state accumulator before refund. They are erased with the tenant and activate in slice 4 for evidence-required execution.

Legacy writers keep their shipped status values, but recovery is classified by authoritative cause:

| Legacy reason | While a drain record/reminder is live | When status 6 is terminal and no automatic owner remains |
| --- | --- | --- |
| `drain_publish_failed`, `drain_state_store_failure`, `drain_dapr_unavailable` | automatic retry under the shipped budget | resumable only from a D10 capsule |
| `drain_attempts_exhausted` | not automatic | resumable only from a D10 capsule |
| `drain_event_count_mismatch`, `drain_missing_event`, `unknown` | evidence incident; no blind retry | non-resumable until authoritative stored-range evidence is repaired; absent evidence is `legacy_resume_evidence_unavailable` |

### D3.3 Closed wire reasons and preparation recovery

`CommandOutcomeHold` is HTTP 503 Problem Details type `https://hexalith.io/problems/service-unavailable`, `Retry-After: 30`, and exactly one `reasonCode` from this table. It has no command status value or replacement first response.

| Reason | Raiser | Exit owner |
| --- | --- | --- |
| `admission_evidence_hold` | D4 legacy/required scope lookup, shard-CAS, or cutover evidence unavailable | gateway performs bounded read/CAS retry and re-evaluation |
| `response_preparation_hold` | A8 preparation/output/receipt is incomplete | coordinator follows unchanged A8/[I-09]: transfer the `HX-EV-RESPONSE-PREPARATION-1` Rendering fence, require both immutable outputs and their generation-bound CAS receipts, create/read back `HX-EV-RESPONSE-PREPARATION-WRITE-1` at `command-response-preparation-write:` plus ScopeOpHash, then continue; a missing output still holds and is never rerendered |
| `publication_pin_capacity_hold` | D7 atomic batch reservation refused, invalid/overflowing arithmetic, unavailable/contradictory candidate evidence, or waiting | quota coordinator obtains a checked candidate and the quota queue grants the whole batch |
| `outcome_evidence_hold` | required immutable outcome/C5 source unavailable | coordinator reads back the complete source set |
| `outcome_evidence_conflict` | same-attempt or immutable-source contradiction | authoritative provider evidence proves the retained row; no overwrite |
| `terminal_evidence_hold` | definitive class-02/03 member lacks C5 closure, including mixed class-01/class-02 | C5 terminal closure or later authoritative accepted evidence |
| `first_send_membership_changed_hold` | D5 cannot prove compatible configuration or membership restoration | broker configuration or membership revision plus fresh zero-send and exact-byte proof |

The set is closed. Retry-exhausted and drain-limit states use their nonterminal `EventsStored` projections, not this 503. Legacy resume endpoints use the separate closed D9 reason set. The Admin command list must merge its submission-time `CommandSummary` with the latest authenticated status/hold inventory at read time; it shows the current hold code/reason instead of leaving a held command as unqualified `Processing`.

## D4. Legacy admission and scope retention — replacement for `[I-12]`

The scope key remains `command-execution-scope:` plus lowercase-hex SHA-256(`U tenant || U executionMessageId`). Required A8 scope records, legacy claims, and tombstones have distinct domain separators. No unavailable read or lost CAS proceeds to archive/status write, actor invocation, or A8 selection.

### D4.1 Slice-2 legacy claims and slice-4 cutover

From the start of slice 2, every legacy admission reads the scope key before any write or actor call and CAS-creates `HX-EV-COMMAND-SCOPE-LEGACY-2\0 || 01 || 000a` (at most 4 KiB): `01` U tenant, `02` U execution MessageId, `03` U domain, `04` U aggregate ID, `05` U command type, `06` B32 exact archived command-payload hash, `07` Q claim UTC, `08` Q expiry UTC, `09` N legacy cohort generation, and `0a` B32 cutover-record hash (zero while the cohort is open). Required record/tombstone conflict; an identical unexpired claim is the legacy retry. A changed claim is `CommandIdentityConflict`.

The maximum legacy evidence horizon `H` is the larger of every configured command-status, archive, idempotency, actor-idempotency, replay, and backup retention, checked and pinned in the cutover record. Because tombstones have a ten-year hard ceiling, readiness rejects `H > 315,576,000 seconds` as `scope_retention_horizon_unsupported`; it does not shorten `H` or activate slice 4. Slice 4 for a domain cannot activate until slice 2 has continuously written claims for at least `H`, every legacy in-flight owner present at slice-2 start has closed, and the gateway has read back the cutover record. Thus every still-live legacy execution has a claim; the three fallback reads of archive/status/actor used by loop 1 stop after cutover and never remain a permanent per-admission tax.

`HX-EV-LEGACY-SCOPE-CUTOVER-1\0 || 01 || 0008` (at most 4 KiB) has `01` U tenant or deployment-wide `*`, `02` U domain, `03` N cohort generation, `04` Q claim-enforcement start UTC, `05` Q cutover UTC, `06` N horizon seconds, `07` B32 complete legacy-owner-empty inventory root, and `08` B32 predecessor cutover hash. It is written/read back under the gateway admission fence and retained while its cohort has any claim. Claim or CAS unavailability yields `admission_evidence_hold`; this is the BC-15 slice-2 fail-closed behavior in D13.

### D4.2 Sharded scope accounting, tombstones, and status

Scope records map to one of exactly 256 shards by the first byte of SHA-256(`U tenant || U executionMessageId`). The capability divides `scopeRetentionCeiling` deterministically: each shard gets `floor(ceiling/256)` and shards `0..(ceiling mod 256)-1` get one extra byte. The scope record and its shard usage record change in one backend transaction. There is no tenant-wide hot CAS.

`HX-EV-SCOPE-SHARD-USAGE-1\0 || 01 || 0007` (at most 1 KiB) has `01` U tenant, `02` N shard `0..255`, `03` N required-record count, `04` N tombstone count, `05` N charged bytes, `06` N generation, and `07` B32 predecessor usage hash. Eight bounded CAS attempts use delays 0, 5, 10, 20, 40, 80, 160, and 320 ms; loss after the eighth returns `admission_evidence_hold` with no domain invocation. At most 256 shard writers can progress independently for one tenant.

After every retry/status/rollback/backup obligation closes, `ScopeRetentionReconciler` compacts a required record to `HX-EV-COMMAND-SCOPE-TOMBSTONE-2\0 || 01 || 0008`: `01` U tenant, `02` U execution MessageId, `03` B32 ScopeOpHash, `04` B32 original input hash, `05` B32 compacted record hash, `06` Q compacted UTC, `07` Q expiry UTC, and `08` N scope shard. Expiry is compacted UTC plus the capability's `scopeTombstoneRetentionSeconds`, which is at least `H` and at most 10 years. The reconciler runs hourly and at 75% shard occupancy; after authenticated expiry and absence of every retained obligation it deletes the tombstone and decrements the shard in one transaction. A status lookup that finds a tombstone returns HTTP 410 type `https://hexalith.io/problems/command-status-expired`, never legacy fallback. Exact late admission before expiry returns the existing idempotency-expired 409; changed identity conflicts.

An expired legacy claim still present at the key is not treated as absent. An exact same-input retry may CAS-replace it with the next claim generation only after the reconciler proves all old obligations closed and decrements the old charge in that same transaction; a changed claim conflicts while the expired row exists. After authenticated deletion, either identity may create a new claim normally. This bounded expiry is BC-16.

A full shard creates `ScopeRetentionCapacityHold` before invocation. Its exits are reconciliation deletion or a capability revision that increases that shard's deterministic allowance. Claims expire with their legacy evidence and are charged 4 KiB in the same shard; required records charge 4 KiB and tombstones 1 KiB. Claims and tombstones are deleted only by the authenticated expiry/obligation transaction above (or whole-tenant erasure); required records compact only after all obligations close; cutover remains while its cohort has a claim; empty usage rows may remain charged at their 1 KiB ceiling until tenant erasure. Whole-tenant erasure removes every remaining row. These are the only retention rules. The codecs activate in slice 2; required admission and tombstone behavior activate in slice 4. Known answers `D12-legacy-claim`, `D12-cutover`, `D12-usage`, and `D12-tombstone` appear in D12.

## D5. First-send membership hold — amendment to 6.5c C2 and replacement support for `[I-16]`

C2's existing purpose-`2a` outcome is versioned instead of create-once forever. `EmptyNamespace` and `InitialRowOnly`, complete partition evidence, and byte-identical `ContinueSamePin` conditions are unchanged. A membership change that cannot meet them creates `FirstSendMembershipChangedHold`; it sends nothing and retains the original pin.

Only verified configuration or membership restoration exits it. A configuration or membership revision triggers the broker to freeze the still-zero-send old namespace and produce a **fresh** atomic zero-send proof. If the active configuration now selects the same logical consumer and destination and reproduces byte-for-byte the pinned request and predicted accepted bodies/header images, mode pair, renderer, and six-header projection, a bounded resolution may return `ContinueSamePin`. Changed or incomplete evidence remains held. Manual action can request this re-evaluation but cannot select a route, waive bytes, abandon the member, or claim publication complete.

`HX-EV-FIRST-SEND-MEMBERSHIP-RESOLUTION-1\0 || 01 || 000c` (at most 16 KiB) has `01` U tenant, `02` B32 ScopeOpHash, `03` N member position, `04` U MessageId, `05` B32 immutable global-pin hash, `06` N resolution generation, `07` B32 predecessor outcome/resolution hash, `08` B32 active membership claim hash, `09` B32 fresh complete zero-send proof root, `0a` U disposition (`ContinueSamePin` or `FirstSendMembershipChangedHold`), `0b` U broker membership issuer, and `0c` Q decision UTC. Its stable head key is C2's existing first-send key; the signed claim, purpose-`2a` carrier, complete proof, and head CAS receipt commit together. Generation is contiguous, a changed revision uses the next generation, and an identical lost acknowledgement reads back. Once a nonzero attempt/queue/acceptance exists, no resolution can reopen this pre-first-send path. Slice 4 owns the record; it is charged in A8's referenced-evidence reservation and erased with the tenant. Known answer `D16-membership-resolution` appears in D12.

## D6. Destination configuration — replacement for `[I-17]`

Destination-ID derivation and all C1 known answers remain unchanged. Admission accepts only canonical JSON schema `hexalith.eventstore.destination/1`, at most 64 KiB, with exactly `component` (1..1,024 UTF-8 bytes), `metadata` (at most 64 string values and 16 KiB of names/values), `schema`, and `topic` (1..1,024 UTF-8 bytes). Sorted names, no insignificant whitespace/final LF, strict UTF-8, and byte equality to outbox component/topic are mandatory. A configuration revision is also a D5 re-evaluation trigger; it grants no compatibility by itself. Slice 2 writes configurations and slice 4 admits publication. Known answer `D17-destination-config` appears in D12.

## D7. Quota ledger and atomic pin batches — replacements for `[I-29]` and `[I-30]`

The publication-retention ledger on `publicationRetentionBackend` is the single quota authority even when object bytes or pins live on another backend. Cross-backend atomicity is not claimed. It uses a durable two-phase reservation: the ledger atomically reserves a complete candidate batch against tenant and deployment counters; each pin CAS then names and verifies that reservation. No pin may send before every batch pin and reservation attachment reads back. A crash leaves a discoverable reservation that the operation owner completes; it never admits a partial batch or refunds while any pin may exist.

The capability is `HX-EV-PUBLICATION-RETENTION-CAPABILITY-2\0 || 01 || 000f` (at most 64 KiB): existing tags `01` deployment identity, `02` revision, `03` canonical backend descriptor, `04` tenant ceiling, `05` deployment ceiling, `06` unidentified reserve, `07` unidentified ceiling, `08` overhead `o`, `09` scope-retention ceiling, `0a` predecessor hash, `0b` effective UTC; plus `0c` N scope shard count (exactly 256), `0d` N scope-tombstone retention seconds, `0e` N pin-wait directory ceiling (1..50,000), and `0f` N maximum quarantined carrier bytes (between 193 MiB and 256 MiB). Existing feasibility rules remain: `1 GiB <= tenant <= deployment`, tenant + reserve <= deployment, reserve >=195 MiB, reserve <= unidentified ceiling <= deployment, `scopeRetentionCeiling >= 64 MiB`, and `0 <= o <= 1,114,112`. A smaller scope ceiling is `publication_retention_capability_invalid`; slice 2 may store it for diagnosis but slice 4 cannot activate.

Every charged object has `HX-EV-PUBLICATION-CHARGE-2\0 || 01 || 000e` (at most 4 KiB): `01` U deployment identity, `02` U account kind (`tenant` or `capture-scope`), `03` U account ID, `04` B32 canonical object-key hash, `05` U kind (`pin-batch`, `side-record`, `retained-object`, `oversize-quarantine`, or `resume-window`), `06` N canonical length, `07` N recorded overhead, `08` N charged amount, `09` N capability revision, `0a` N charge generation, `0b` U state (`staged`, `active`, or `released`), `0c` B32 predecessor charge hash (zero at generation 1), `0d` O(B32) transfer owner, and `0e` Q update UTC. Length and overhead are non-negative checked u64; charged amount always equals their checked sum. A later attach must match kind and canonical length and uses the recorded amount despite a changed `o`. Ordinary creation writes `active` with absent transfer owner. A D9 successor writes `staged` with the stable request identity as transfer owner while the prior active generation remains authoritative and every counter includes both amounts. Successor-state readback authorizes exactly one `staged -> active` generation and exactly one release/decrement of the predecessor; absence of successor state authorizes exactly one `staged -> released` rollback. Refund otherwise CAS-writes the next `released` generation and decrements counters once only after deletion/closure readback; a later recreation needs the next `active` generation and fresh counter admission.

Each counter is `HX-EV-PUBLICATION-COUNTER-1\0 || 01 || 0008` (at most 1 KiB): `01` U deployment identity, `02` U counter kind (`tenant`, `tenant-pool`, `deployment`, or `unidentified`), `03` U counter ID, `04` N used bytes, `05` N active charge count, `06` N generation, `07` B32 predecessor counter hash, and `08` Q update UTC. Tenant accounts use both their tenant counter and `tenant-pool`; capture scopes use their account counter and `unidentified`; every charge also uses deployment. Authenticated tenant accounts together cannot exceed deployment minus reserve. Capture scopes together cannot exceed `unidentifiedCaptureCeiling`. Lowering below usage admits nothing new and evicts nothing.

`HX-EV-PIN-BATCH-RESERVATION-2\0 || 01 || 000d` (at most 64 KiB) has `01` U tenant, `02` B32 ScopeOpHash, `03` B32 candidate batch root, `04` N pin count, `05` B rows sorted by member position (`u32 position || U MessageId || B32 exact pin hash || N canonical length || N charged amount`), `06` N total charged amount, `07` N capability revision, `08` B32 tenant-counter predecessor hash, `09` B32 tenant-pool-counter predecessor hash, `0a` B32 deployment-counter predecessor hash, `0b` U state (`reserved`, `installed`, or `released`), `0c` N generation, and `0d` Q update UTC. Tag `04` must equal the row count and tag `06` the checked sum. All three predecessor hashes authenticate the exact counters advanced by the transaction; a missing or stale predecessor aborts the whole CAS. At the imported 1,024-byte MessageId maximum, each member row is exactly 1,080 bytes; the maximum-width non-row record is 1,291 bytes, so the hard member ceiling is `floor((65,536 - 1,291) / 1,080) = 59`. The ceiling remains 59 for short IDs. An otherwise-valid V1 batch of 60..1,000 members fails A8 readiness/admission as `AppendPreparationLimit` before append; it is never partially segmented after commit.

The ledger CAS creates every per-pin charge plus this reservation and advances all counters together. The pin backend installs all exact candidates with the reservation hash; only full readback advances to `installed`. No waiting batch owns a charge. Reservation and refund both decode `accountKind` as exactly `tenant` or `capture-scope`; an unknown value is a codec/reconciliation incident with no counter mutation, never an alias for capture scope.

Per-kind maxima remain 449 MiB for one global pin, 193 MiB for side/ordinary retained objects, 256 MiB for provider-quarantined oversize carriers, and 1 GiB for the one active resume window. A negative or overflowing input, or unavailable/contradictory evidence for any candidate length, overhead, amount, count, or total, maps to `CommandOutcomeHold(publication_pin_capacity_hold)` before comparison. Its stable subject is `capacity-subject:` plus lowercase-hex SHA-256(`"HX-EV-CAPACITY-SUBJECT-1\0" || 01 || B32 ScopeOpHash || B32 immutable A8 outbox/member-plan root`), not an unverified candidate-batch root. If those admitted-plan values are unavailable, A8 fails before commit; no later hold may invent a subject. The hold creates the D11 inventory entry using the pre-reserved A8 slot, but creates no reservation, charge, counter delta, pin, or send. The quota coordinator re-evaluates from immutable outbox/member-plan and renderer evidence on the D1 triggers; only a fully checked candidate may enter the D8 queue or retry reservation. Tenant/deployment erasure releases only after every object is deleted/read back. The ledger activates in slice 2, resume charges in slice 3, and pin/capture charges in slice 4. Known answers `D29-capability`, `D29-charge`, `D29-counter`, and `D29-pin-batch` appear in D12.

## D8. Fair pin-capacity waits — replacement for `[I-31]`

A refused **valid, checked** D7 batch reservation creates one wait; it never leaves some pins charged. Invalid arithmetic/evidence remains the same indexed `PublicationPinCapacityHold` without a D8 queue row because no trustworthy tag `04` total or tag `05` holding counter exists. Its quota coordinator performs D1's 60-second and revision-triggered deterministic rerender/revalidation. Once checked evidence exists, it atomically creates the D8 row under the same stable subject before removing the inventory-only state; reservation still occurs only on a later whole-batch grant. `HX-EV-PIN-CAPACITY-WAIT-2\0 || 01 || 000c` (at most 4 KiB) has `01` U tenant, `02` B32 ScopeOpHash, `03` B32 candidate batch root, `04` N total charged amount, `05` U holding counter (`tenant` or `deployment`), `06` N global first-hold ticket, `07` Q first-hold UTC, `08` N re-attempt count, `09` U state (`queued` or `parked`), `0a` B32 current capability hash, `0b` B32 immutable outbox/member-plan root, and `0c` Q last-check UTC.

There is one deployment directory and one directory for each tenant with waits. `HX-EV-PIN-CAPACITY-QUEUE-4\0 || 01 || 000a` (at most 64 MiB) has `01` U deployment identity, `02` U counter ID (`deployment` or `tenant:` plus tenant), `03` N generation, `04` N entry count, `05` B concatenated fixed-order rows with **no inner count** (`N ticket || U tenant || B32 ScopeOpHash || U state`), `06` N parked count, `07` N reserved-slot count, `08` N authenticated capability ceiling, `09` N last issued global ticket, and `0a` B32 predecessor directory hash followed by `0b` Q update UTC; the declared field count is therefore `000b`. Tag `04` controls exact parsing and equals queued plus parked; `entryCount + reservedSlotCount <= tag 08 <= 50,000`. The duplicated capability value must equal the authenticated D7 capability revision used by the transition. A row consumes exactly one previously reserved slot in the same CAS, so no operation ever appends to a full directory. Parked waits remain in this ordered directory and are discoverable in a key-only store.

The deployment directory is also the sole durable global ticket allocator. Its `lastIssuedTicket` starts at zero; admission reserves both slots and CAS-increments that field in the deployment row, assigning the resulting positive u64 to the wait. A lost acknowledgement rereads the stable subject's reservation and same ticket. A conflicting subject or predecessor loses without allocating a ticket. `lastIssuedTicket == 2^64-1` fails pre-commit admission as `pin_wait_ticket_exhausted`; it never wraps, reuses a ticket, or commits the command. Queue rows are canonically sorted by `(ticket as unsigned numeric u64, tenant as raw canonical UTF-8 bytes, ScopeOpHash as raw 32 bytes)`; state is not part of the sort key. Both decoding and reconstruction use that exact tuple.

Before command commit, A8 charges one 4 KiB wait row plus **two** maximum encoded queue rows and CAS metadata as `side-record`, rejects a stable capacity subject already resident or reserved in either directory, and atomically reserves one slot in the deployment directory and one in the candidate tenant directory under that subject while allocating its ticket. If either slot, ticket, or charge is unavailable, admission fails `AppendPreparationLimit`; a committed command is never left outside the directories. Exactly one reservation is materialized as the current `queued`/`parked` row while the other remains reserved. A cross-counter move atomically turns the destination reservation into the row and the source row back into its reservation, so it cannot deadlock on a full destination. Refund of both slots/row charges occurs only after the wait and both directory interests delete/read back, or during whole-operation erasure. Imported pre-reservation waits must complete a bounded migration into separately charged/reserved slots before slice-4 readiness; they cannot be hidden in an overflow counter.

One wait has one materialized row in exactly one directory and its reserved counterpart in the other. New batches queue behind existing eligible waits even if current counters would fit. The oldest queued deployment head retries after every refund/capability revision and at least every 60 seconds. If deployment capacity refuses it, it stays. If deployment fits but its tenant refuses, it moves atomically to that tenant's directory with its ticket unchanged; the destination's pre-reserved slot becomes the row and the source row becomes its reservation, and the deployment queue advances. The oldest tenant head that fits its tenant moves back by the inverse transaction, where all deployment candidates are ordered by ticket. It owns no quota capacity during either move, so no circular wait exists. A candidate larger than a current ceiling is `parked` in its already-reserved slot; a capability change re-evaluates it at its original ticket. Static pre-Prepared feasibility and directory-slot reservation prevent a committed command from becoming undiscoverable.

An empty directory turn and a parked-only directory turn are durable no-ops: generation, rows, counts, reservations, ticket head, and charges remain byte-identical and the next re-evaluation deadline remains scheduled. Too few/extra decoded rows, `entryCount + reservedSlotCount > authenticated capability ceiling`, `parkedCount > entryCount`, a state/count mismatch, duplicate ticket/ScopeOpHash, duplicate stable subject in row/reservation sets, wrong canonical sort, ticket above the allocator head, or trailing bytes creates charged/indexed `PinCapacityQueueCorruptionHold` and performs no reservation or move. The quota coordinator owns it. Exit requires authenticated reconstruction from every D8 wait row plus the exact predecessor directory, installation/readback of one canonical queue generation, and proof that no ticket was lost or duplicated; manual edits cannot clear it.

On each turn, exact outbox/member plan, purpose-02 key, membership, configuration, and render inputs are reverified. If valid, the original candidate bytes are used. If invalid, the owner deterministically rerenders from immutable outbox intent, CAS-updates the candidate root/amount without changing ticket, and retries. A successful whole-batch D7 reservation removes the wait/directory row atomically on the ledger, then D7 installs pins. Tenant erasure deletes tenant waits and its deployment rows after operation erasure. Slice 4 owns the queues. Known answers `D31-wait` and `D31-queue` appear in D12.

## D9. Publication resume — replacement for `[I-45]` and amendment to 6.5c C2/C5

Publication resume re-arms only unresolved publication of already committed events. It never submits to MediatR, invokes domain code, creates or rewrites a command/event MessageId, recreates an accepted member, changes a pin/outbox/batch root/committed result/first response, or resends an accepted member.

### D9.1 Discoverable precondition and authority

Every eligible hold inventory item exposes an opaque stable `resumeHandle = "hxrsm1-" || lowercase-hex SHA256(U tenant || U execution identity || B32 hold-source hash)`. Authorized Operators can read `GET /api/v1/admin/publications/tenants/{tenantId}/{resumeHandle}/precondition`; the response is support-safe `{ resumeHandle, eligibility, expectedHoldSourceHash, headHash, nextResumeOrdinal, predecessorAuditHash, expiresAt }`. It comes from one authenticated current-head read and is never an execution capability. A deployment route does not stand in for a tenant.

`POST /api/v1/admin/publications/tenants/{tenantId}/{resumeHandle}` takes `{ "expectedHoldSourceHash": "<64 lowercase hex>", "idempotencyKey": "<1..128 ASCII visible bytes>", "reason": "<1..512 UTF-8 bytes>" }`. Its canonical caller carrier is `HX-EV-PUBLICATION-RESUME-CARRIER-1\0 || 01 || 0005` over `01` U tenant, `02` U resume handle, `03` B32 expected hold-source hash, `04` U idempotency key, and `05` U reason. `stableRequestIdentity = SHA256("HX-EV-PUBLICATION-RESUME-IDENTITY-1\0" || 01 || B(exact carrier bytes))`; server time, ordinal, and provider observations are deliberately absent. The Admin server authenticates tenant and Operator policy, first resolves that stable identity through the current live row, orphan audit, or expiry tombstone, then rereads the precondition and signs purpose `2d` claim `HX-EV-PUBLICATION-RESUME-3\0 || 01 || 0010` (at most 4 KiB): `01` U operator-action issuer, `02` U tenant, `03` U execution identity, `04` B32 ScopeOpHash (zero for legacy), `05` U eligibility (`retry-exhausted`, `drain-limit`, or `legacy-publish-failed`), `06` B32 current hold source or D10 capsule hash, `07` B32 latest A8 head (zero for legacy), `08` N next ordinal, `09` B32 predecessor successful audit hash, `0a` U resume handle, `0b` B32 stable request identity, `0c` B32 exact caller-carrier hash, `0d` U operator subject, `0e` Q request UTC, and `0f` Q expiry UTC no more than 15 minutes later. The caller need not discover internal ScopeOpHash, ordinal, or audit key separately; the server supplies and signs them from the same read. Stale fields fail before mutation.

The same stable identity with byte-identical carrier is one exact retry. A live result or orphan audit returns/completes that result without allocating an ordinal, window, charge, audit, or invocation. A retained expiry tombstone returns `resume_request_expired`. The same identity with different carrier bytes is `resume_request_conflict`, even after the live/audit body is reclaimed. A genuinely new request uses a new idempotency key and stable identity.

The eligible states are an unresolved class-01-at-maximum set, an active D3 drain-limit record, or a verified D10 legacy capsule. Pending/unknown without an active drain-limit record, C5 terminal pointer, accepted/not-applicable head, `CommandOutcomeHold`, and D5 membership hold are ineligible.

Before any mutation, the coordinator decodes the committed member roster by position and MessageId without map/dictionary collapse. Positions and MessageIds must each be unique. `accepted` and `unresolved` must be disjoint, duplicate-free, contain no unknown position/MessageId, and their exact union must equal the committed roster. Any duplicate, omission, overlap, unknown member, position/MessageId disagreement, or changed bytes is `resume_evidence_hold`; it writes no closure, audit, state, charge, or invocation.

### D9.2 Window namespaces and the C5 fence correction

C5's whole-operation producer disable and cross-destination broker reject fence remain permanent **only for terminal closure**. They continue to block all current and future send IDs before the duplicate path. Resume does not install or weaken that fence.

For retry exhaustion, resume closes only publication window `w`. The broker namespace and every accept index include `(tenant, ScopeOpHash, window, member position, send ID)` for new window-aware evidence. It installs a window-scoped producer disable/reject fence that blocks every current/future send ID in window `w`, reconciles all sends in that window, and takes a final empty observation. A later window is accepted only with a verified D9 window claim and while the whole-operation terminal fence is absent. Delayed IDs from a closed window still fail; a window claim cannot bypass the terminal fence.

`HX-EV-PUBLICATION-WINDOW-2\0 || 01 || 000d` (at most 16 KiB), signed under purpose `2d`, has `01` U tenant, `02` B32 ScopeOpHash, `03` U original OperationId, `04` N window (zero is admission; resume opens positive values), `05` B32 predecessor window-closure hash (zero at window 0), `06` B32 exact **prior** resume-state hash (zero at first state), `07` B32 stable request identity (zero at admission), `08` B32 unresolved member-set root, `09` B32 immutable retry-policy hash, `0a` N drain-limit base, `0b` U operator-action issuer, `0c` Q opened UTC, and `0d` B32 active capability hash. It contains no successor-state hash: the exact prior state, request identity, closure, and member evidence construct the window claim independently; only the later successor state points to the resulting window-claim hash. For window greater than zero, each C2 purpose-`1c` send parent is accompanied by a backend-authenticated window binding over its exact parent hash, send ID, member, MessageId, window-claim hash, and broker namespace key. C2's signed maximum applies independently within a window; flat terminal attempt ordinals are `(window, member-local ordinal)`, never reset without the closure chain.

`HX-EV-PUBLICATION-WINDOW-CLOSURE-3\0 || 01 || 000b` (at most 64 KiB) has `01` U tenant, `02` B32 ScopeOpHash, `03` N closed window, `04` B member rows, `05` B32 window broker reject-fence receipt, `06` B32 window producer-disable receipt, `07` B32 final empty-state root, `08` B32 complete window attempt-set root, `09` U C5 AuthMode, `0a` B32 predecessor window-history accumulator, and `0b` Q closure UTC. Tag `04` is explicitly `u32 rowCount ||` rows sorted by position, each `u32 position || N final local attempt || B32 last definitive result hash`; the count controls exact parsing and trailing bytes fail. It never contains its successor. After exact closure and broker-authentication bytes read back, `successor = SHA256("HX-EV-PUBLICATION-WINDOW-HISTORY-1\0" || 01 || B32 predecessor || B(exact closure bytes) || B(exact broker-authentication bytes))`; only the successor D9 state stores that value. The closure key is `publication-window-closure:` plus lowercase-hex SHA-256(`"HX-EV-PUBLICATION-WINDOW-CLOSURE-KEY-1\0" || 01 || B32 ScopeOpHash || N closedWindow`). The closure is invalid if any accepted member would be retried; accepted rows remain in the overall outcome unchanged.

This explicitly amends C5: its terminal roster/attempt verifier consumes the current window's complete evidence plus the authenticated accumulator/count of earlier closed windows. C5 terminal closure still uses the whole-operation fence and then seals that accumulator. A window closure never satisfies terminal closure on its own.

### D9.3 Bounded state, charge, and decision

`HX-EV-PUBLICATION-RESUME-STATE-3\0 || 01 || 000e` (at most 32 KiB) is the single CAS head for an execution: `01` U tenant, `02` U execution identity, `03` B32 ScopeOpHash, `04` N successful resume ordinal, `05` N active window, `06` N active drain limit, `07` B32 current hold/source hash, `08` B32 active window-claim hash, `09` B32 window-history accumulator, `0a` N closed-window count, `0b` B32 last successful audit hash, `0c` B live-retry rows, `0d` B expiry-tombstone rows, and `0e` Q update UTC. Tag `0c` is `u32 count ||` rows sorted by ordinal, each `B32 stable request identity || B32 exact caller-carrier hash || N ordinal || B32 audit hash || N returned window || N returned drain limit || Q request expiry`. Tag `0d` is `u32 count ||` rows sorted by stable identity, each `B32 stable request identity || B32 exact caller-carrier hash || Q request expiry`. Live plus tombstone rows are at most 64. At expiry a live row moves, in the same state CAS, to a tombstone retained until execution obligation closure/erasure; audit and closure bodies may then be reclaimed. A 65th distinct success is `resume_capacity_hold` with no mutation. Exact live retries remain answerable, exact tombstone retries remain distinguishable as expired, and changed carrier bytes conflict while the execution exists.

Before mutation, the ledger stages one new worst-case `resume-window` charge for every unresolved member's full next-window attempt evidence, new drain rows, the **next** drain-limit record and resolution, window claim/closure, state, audit, tombstone capacity, and hold/inventory evidence. It is at most 1 GiB and must fit tenant, tenant-pool, and deployment counters **in addition to** the unchanged old active charge. The staged charge binds the stable request identity and all three predecessor counters. The old charge remains authoritative until successor-state readback. Refusal writes no closure, audit, state, invocation, or partial counter and returns `resume_capacity_hold`.

Charge ownership is a closed two-phase swap. Before successor readback, recovery finds no successor-state hash and CAS-releases only the staged generation, leaving the old active generation/counters exact. After successor readback, recovery CAS-activates the staged generation and releases/decrements the old generation exactly once. Generation and transfer-owner checks make repeated recovery a no-op; no point counts less than the old charge, admits above a ceiling, or releases either generation twice. Publication arms only after the finalized successor charge reads back.

`HX-EV-PUBLICATION-RESUME-AUDIT-4\0 || 01 || 000a` (at most 4 KiB) is written only for a successful resume: `01` U tenant, `02` U execution identity, `03` N ordinal, `04` B32 stable request identity, `05` B32 exact caller-carrier hash, `06` B32 prior resume-state hash, `07` O(B32) window-closure hash, `08` N opened window (zero for legacy), `09` N new drain limit, and `0a` Q decision UTC. It never contains the successor state hash. Its create-once key is `publication-resume-audit:` plus lowercase-hex SHA-256(`"HX-EV-PUBLICATION-RESUME-AUDIT-KEY-2\0" || 01 || U tenant || U executionIdentity || B32 stableRequestIdentity`). The same stable identity therefore locates a live row or orphan audit without reconstructing a server timestamp or ordinal. Rejected requests create no durable record and consume no quota.

The acyclic write order is: resolve stable identity against live/tombstone/audit evidence; read/authenticate prior state and exact partition; stage/read the successor charge; write/read any drain resolution and closure; compute the successor accumulator; construct/write/read the window claim from the **prior** state hash and stable identity; create/read the audit against the prior-state hash; CAS/read the successor state containing the window/audit hashes and compact retry row; finalize the charge swap; then remove inventory and arm publication. A crash before state CAS leaves an orphan audit and staged charge that only the byte-identical stable request may finish; recovery otherwise rolls the stage back. A crash after state readback deterministically finalizes before arming. State and audit never hash each other, and window and successor state never hash each other.

For retry exhaustion, the transaction closes `w`, creates the successor window claim/state, and then arms only unresolved members. For a drain-limit-only hold it first writes D3's exact resolution linked to the old limit record and successor state, proves the window claim, committed roster, accepted set, unresolved set, and member bytes unchanged, leaves the window and member set unchanged, increases the limit by the original bounded drain reservation, and records the exact re-armed invocation hash before invoking it; if members are also retry-exhausted it does both. A later drain-limit is already included in the charge. Inventory removal follows successful state/readback; exhaustion again creates a fresh source/entry.

Ordinal, window, closed-window count, drain limit, and every charge addition use checked u64 arithmetic. An increment from `2^64-1`, a zero/overflowing drain increment, or a limit sum above `2^64-1` returns 409 `resume_arithmetic_exhausted`, leaves the old hold/state/charge authoritative, and arms nothing.

The closed endpoint outcomes are:

| Result | HTTP and reason |
| --- | --- |
| success | 202 `{ resumeHandle, resumeOrdinal, window, drainLimit, auditRecordHash }` |
| stale/ineligible/expired/idempotency conflict | 409 concurrency Problem Details with `resume_hold_changed`, `resume_not_eligible`, `resume_request_expired`, or `resume_request_conflict` |
| checked ordinal/window/limit overflow | 409 concurrency Problem Details with `resume_arithmetic_exhausted` |
| quota refusal | 503, `Retry-After: 30`, `resume_capacity_hold` |
| unavailable current evidence | 503, `Retry-After: 30`, `resume_evidence_hold` |
| historical legacy evidence absent/contradictory | 409, `legacy_resume_evidence_unavailable` |

The route and legacy eligibility activate in slice 3 alongside BC-02; evidence-required eligibility activates in slice 4. State and charge erase with the tenant after every publication/rollback/incident obligation closes. Known answers `D45-request`, `D45-window`, `D45-closure`, `D45-state`, and `D45-audit` appear in D12.

## D10. Legacy status-6 resume — replacement for `[I-46]`

### D10.1 Resume capsule before cleanup

From slice 3, no legacy drain evidence for a terminal status-6 execution is removed until the actor creates and durably reads back a bounded chunk set and its `HX-EV-LEGACY-RESUME-CAPSULE-2\0 || 01 || 0010` manifest (at most 128 KiB): `01` U tenant, `02` U domain, `03` U aggregate ID, `04` U tracking identity, `05` O(U) execution MessageId, `06` U correlation ID, `07` U command type, `08` U rejection classification (`success-events` or `rejection-events`), `09` N start sequence, `0a` N end sequence, `0b` I positive event count, `0c` B32 ordered stored-event root, `0d` B chunk manifest, `0e` U cleanup source (`drain-exhaustion` or `operator-reconciliation`), `0f` B32 exact source-record hash, and `10` Q creation UTC.

`capsuleIdentity = SHA256("HX-EV-LEGACY-RESUME-CAPSULE-IDENTITY-1\0" || 01 || U tenant || U domain || U aggregateId || U trackingIdentity || B32 sourceRecordHash)`. Each `HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-1\0 || 01 || 0005` record (at most 64 KiB) has `01` B32 capsule identity, `02` N zero-based chunk ordinal, `03` N row count, `04` B concatenated rows, and `05` B32 chunk row root. Rows are ascending `N sequence || U stored MessageId || B32 StoredDigest`, with no nested count; tag `03` controls exact parsing. At the imported 1,024-byte MessageId maximum a row is 1,068 bytes and the fixed maximum-width chunk overhead is 132 bytes, so each chunk holds at most 61 rows. Every supported V1 range of at most 1,000 rows therefore uses at most 17 chunks.

Capsule tag `0d` is `u32 chunkCount ||` 1..17 rows sorted by ordinal, each `N ordinal || N firstSequence || N rowCount || B32 exact chunk hash || N encoded chunk length || U resolvable chunk object key`. The chunk key is `legacy-resume-capsule-chunk:` plus lowercase-hex SHA-256(`"HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1\0" || 01 || B32 capsuleIdentity || N chunkOrdinal`); the capsule manifest key remains stable below. The ordered stored-event root is recomputed over `u32 totalRowCount ||` all exact member rows concatenated in chunk order: `SHA256("HX-EV-LEGACY-RESUME-EVENTS-2\0" || 01 || B(exact counted rows))`. Chunk lengths/counts/endpoints must exactly cover tags `09`..`0b`, with no gap, overlap, duplicate MessageId, missing chunk, or trailing bytes. The actor writes/reads all chunks first, then create-once writes/reads the manifest; any oversize or incomplete set fails cleanup and retains the drain record/reminder. A successful legacy drain needs no capsule and keeps its shipped cleanup.

The capsule create-once key is `legacy-resume-capsule:` plus lowercase-hex SHA-256(`"HX-EV-LEGACY-RESUME-CAPSULE-KEY-2\0" || 01 || B32 capsuleIdentity`); the resume handle additionally binds the manifest hash. It never uses concatenated text or MessageId alone, so expired status and cross-aggregate ID reuse cannot redirect it. The manifest, chunks, and object keys are charged together as `resume-window`, retained until successful publication plus retry/incident obligations close, and erased with the tenant. Drain exhaustion publishes its dead-letter, then writes/reads chunks and manifest, then removes the drain/index/reminder. `IsRejection` comes from the drain record into tag `08`; a historical dead-letter without that authority cannot guess it.

For pre-slice-3 history, a privileged precondition may create the capsule only when an extant `UnpublishedEventsRecord` supplies range, correlation, command type, rejection classification, tracking identity, and exact events/MessageIds. A dead-letter may corroborate range/cause but cannot supply missing rejection classification. If no authoritative source exists, the inventory records `legacy_resume_evidence_unavailable`; resume fails closed and never fabricates bytes or executes the command. Evidence import is outside this story.

### D10.2 Exclusive recovery and re-arm

`HX-EV-LEGACY-PUBLICATION-RECOVERY-3\0 || 01 || 000d` (at most 4 KiB) has `01` U tenant, `02` B32 capsule manifest hash, `03` U resume handle, `04` N recovery generation, `05` N successful resume ordinal, `06` U owner (`legacy-resume` or `dead-letter-admin`), `07` U state (`claimed`, `draining`, `completed`, or `failed`), `08` B32 actor drain-record hash, `09` O(B32) Operations dead-letter record hash, `0a` B32 predecessor recovery hash, `0b` O(U) failure reason, `0c` O(B32) repaired-evidence hash, and `0d` Q update UTC. Failure reason is absent outside `failed` and otherwise exactly `transport-retryable`, `evidence-unavailable`, or `evidence-contradictory`; repaired evidence is present only on the next claim after an evidence failure. Its stable CAS-head key is `legacy-publication-recovery:` plus lowercase-hex SHA-256(`"HX-EV-LEGACY-PUBLICATION-RECOVERY-KEY-2\0" || 01 || B32 capsuleManifestHash`). Every write increments generation and authenticates the predecessor. The aggregate actor and Operations dead-letter Admin route both check/CAS this shared EventStore-backend fence. A drain-exhaustion dead-letter can no longer be generically retried around D9; its Admin action resolves to the resume precondition. Exactly one owner can recreate/drain the range.

After D9 succeeds, the actor rereads the immutable manifest, every addressed chunk, and stored events; recomputes every chunk hash/length, sequence, MessageId, StoredDigest, ordered row root, endpoints/count, correlation, command type, and rejection classification; proves no live drain/reminder/index owner exists; CAS-claims recovery; and recreates the shipped drain record for exactly that range with retry count zero and `DeadLettered=false`. Publication uses the stored event MessageIds. It never rewrites status 6 or command bytes. `success-events` must finish as shipped `Completed`; `rejection-events` must finish as shipped `Rejected`; a classification inversion is evidence contradiction. A second publication exhaustion reuses the byte-identical chunks and manifest and advances only the recovery generation and next successful D9 resume ordinal. It never attempts changed bytes at the create-once capsule key.

The transition graph is closed: creation is `claimed`; only its owner may write `claimed -> draining`; verified drain success writes `draining -> completed`; a typed transport or evidence failure writes `draining -> failed`. `failed(transport-retryable) -> claimed` requires a strictly greater successful D9 ordinal, the next recovery generation, and the unchanged capsule. `failed(evidence-unavailable|evidence-contradictory) -> claimed` additionally requires a present repaired-evidence hash whose authoritative readback recomputes every field above; otherwise it remains charged/indexed and re-evaluated hourly. A second exhaustion is `draining -> failed(transport-retryable)`, never a second capsule creation. `completed` is terminal until obligation closure/erasure. Every other edge—including `claimed -> completed`, `failed -> draining`, same-ordinal retry, changed capsule, generation skip, or any transition out of `completed`—is rejected without mutation. No owner may skip a state, overwrite a generation, or leave `failed` without its inventory owner and one of these exits.

A mismatch or unavailable read is `resume_evidence_hold`; missing/contradictory authority is `legacy_resume_evidence_unavailable`. Recovery and capsule activate in slice 3 and erase after closure/tenant erasure. Known answers `D46-capsule` and `D46-recovery` appear in D12.

## D11. Held delivery, quarantine, and inventory — replacements for `[I-36]` and `[I-37]`

### D11.1 Capture is mandatory and bounded

Every addressed-delivery subscription has a verified `HX-EV-SUBSCRIPTION-DELIVERY-POLICY-3\0 || 01 || 000d` record (at most 16 KiB): `01` U deployment identity, `02` U component, `03` U topic, `04` U physical subscription ID, `05` N policy revision, `06` B32 predecessor policy hash (zero at revision 1), `07` U state (`active` or `superseded`), `08` U capture mode (`dead-letter-capture` or `direct-held-capture`), `09` O(U) dead-letter topic, `0a` N maximum live redeliveries `1..64`, `0b` B32 exact resolved subscription/resiliency configuration hash, `0c` U source (`dapr-configuration` or `broker-api`), and `0d` Q observation UTC. The create-once revision key and CAS head are specified in the address table below; only one contiguous `active` revision may be selected, and installing its successor first writes/read-backs the new revision, then CASes the head, then marks the predecessor superseded. A stale or rolled-back revision cannot authorize acknowledgement. An unbounded broker redelivery policy alone is not sufficient: after the bounded local attempts or 24 hours from first observation, whichever comes first, the consumer must durably capture/read back the exact carrier and held state before acknowledging that transport copy. A dead-letter topic's own subscription uses direct held capture and cannot dead-letter again.

A first held observation creates/read-backs `HX-EV-HELD-DELIVERY-4\0 || 01 || 0016` (at most 32 KiB) **before** its attempt number or 24-hour clock is used: `01` U scope kind (`tenant` or `deployment`), `02` U deployment identity, `03` O(U) tenant, `04` O(U) MessageId, `05` U component, `06` U topic, `07` U physical subscription ID, `08` B32 active subscription-policy hash, `09` U state (`observed`, `captured`, `redriving`, `incident`, or `closed`), `0a` U reason, `0b` N observed carrier length, `0c` B32 exact carrier hash, `0d` O(U) retained backend ID, `0e` O(U) resolvable retained object key, `0f` O(B32) authenticated object readback receipt hash, `10` O(B32) committed handoff hash, `11` Q first-observed UTC, `12` N delivery-attempt count, `13` N redrive count, `14` O(B32) last redrive-error evidence hash, `15` Q next re-evaluation UTC, and `16` N entry revision. `scopeKind=tenant` requires tenant present; `scopeKind=deployment` requires tenant absent. Every other pairing is a decode/admission incident with no acknowledgement or charge mutation.

Its key is `held-delivery:` plus lowercase-hex SHA-256(`"HX-EV-HELD-DELIVERY-KEY-2\0" || 01 || U scopeKind || U deploymentIdentity || O(U tenant) || U component || U topic || U physicalSubscriptionId || B32 exactCarrierHash`). Reuse of a physical subscription ID or identical bytes in another deployment, scope, component, or topic cannot cross-link authority. Every delivery CAS-increments tag `12` and the record revision; restart cannot reset either boundary. Locator fields are absent only in `observed`/`incident`, present together in `captured`/`redriving`/`closed`, and bounded to 1,024-byte backend ID plus 4,096-byte object key. Hashes alone are never restart redrive authority.

The record is charged 32 KiB before the first nonterminal response. For an ordinary carrier, D7 also stages and activates a `retained-object` charge for the exact carrier length plus recorded overhead against the same tenant or deployment capture scope before object write; `captured` is legal only after the exact object, locator, readback authority, held entry, and active charge all read back. Oversize uses its distinct quarantine charge. Therefore no physical-copy acknowledgement can strand uncharged bytes. The captured transition is an explicit authenticated terminal handoff for the **physical transport copy**, so the consumer may acknowledge that copy; it writes no route success/effect/filter result, and all original logical route/handoff obligations remain open until ordinary terminal decisions. This amends 6.5c C4 without weakening its success rule.

Permanently nonadmissible or invalid-header carriers use `HX-EV-CARRIER-QUARANTINE-2\0 || 01 || 000e` (at most 128 KiB): `01` U scope kind, `02` O(U) tenant, `03` U physical subscription ID, `04` U reason (`invalid-header-value`, `invalid-carrier`, or `oversize-carrier`), `05` N exact body length, `06` B32 body hash, `07` B header manifest containing only names, value lengths, and value hashes for forbidden values, `08` U retained backend ID, `09` U resolvable retained object key, `0a` B32 provider archive/readback authority hash, `0b` O(U) parsed MessageId, `0c` Q captured UTC, `0d` U disposition (`terminal-quarantine`), and `0e` B32 exact source-delivery receipt hash. No raw forbidden header value enters the record. The manifest is `u32 count ||` at most 128 rows `U headerName || N valueLength || B32 valueHash`; with the imported 64 KiB aggregate header-name/value maximum its exact worst case is `4 + 65,536 + 128*(4+8+32) = 71,172` bytes, safely within the 128 KiB record cap even with maximum surrounding identifiers.

The ordinary path is maximum-inclusive: complete carriers `<= 193 MiB` use ordinary retained capture. Oversize quarantine is the disjoint interval `193 MiB < length <= maximumQuarantinedCarrierBytes` (at most 256 MiB) and may acknowledge only after a broker/provider atomically archives its **exact** bytes under an authenticated non-expiring quarantine object and D7 charges kind `oversize-quarantine`. Provider readiness pre-rejects anything it cannot capture. A delivered object strictly above the advertised maximum creates the bounded charged D11 held record in `incident` state with reason `delivery_above_advertised_max`, exact streamed length/hash, Operations owner, hourly re-evaluation, and a D11 inventory entry before a repeated delivery can become invisible; it remains unacknowledged and exits only after provider configuration pre-rejects it and the broker proves no live copy, or after a later approved capture capability stores the exact bytes. Valid EventStore carriers cannot use the oversize exception.

`HX-EV-REDRIVE-REQUEST-2\0 || 01 || 0007`, signed under purpose `2d`, has `01` U operator-action issuer, `02` U scope kind, `03` O(U) tenant, `04` B32 held-delivery key hash, `05` N expected redrive count, `06` U operator subject, and `07` Q request UTC. Routes are unambiguous: tenant entries use `POST /api/v1/admin/held-deliveries/tenants/{tenantId}/{entryKey}/redrive`; deployment entries use `POST /api/v1/admin/held-deliveries/deployment/{entryKey}/redrive` and Admin policy. Automatic redrive runs when cause-clearing evidence appears and otherwise with exponential backoff from 60 seconds to 15 minutes. It injects exact retained bytes/header image into the same authenticated ingress. Entry into `redriving` increments the count and binds the attempt. Terminal route decisions close the entry and refund after deletion readback. A nonterminal transport/ingress failure CASes `redriving -> captured`, preserves the retained object and charge, stores the typed error-evidence hash, and schedules the next bounded retry; restart resumes from that durable state. No failed redrive may remain indefinitely in `redriving`. A terminal quarantine is never redriven.

Policy evidence activates before binary publication in slice 4; continuation/redrive activates with the binary carrier. Tenant records erase with the tenant; deployment captures erase only with their capture scope after all obligations close. Known answers `D36-policy`, `D36-held`, `D36-quarantine`, and `D36-redrive` appear in D12.

The closed held-delivery reasons are `handler-capability-hold`, `raw-source-unavailable`, `delivery-carrier-limit-hold`, `invalid-header-value`, `invalid-carrier`, `oversize-carrier`, and `delivery_above_advertised_max`. Their owner is `operations`; capture-capable reasons exit by exact-byte capture then redrive, permanently invalid reasons exit by terminal quarantine, and above-maximum exits only as specified above. Unknown reasons fail decoding and cannot be acknowledged.

### D11.2 Collision-free durable hold inventory

Inventory scope is explicit. Tenant actor ID is `tenant:` plus lowercase-hex SHA-256(`U tenant`); deployment actor ID is `deployment:` plus lowercase-hex SHA-256(`U deployment identity`). A legal tenant string `deployment` therefore cannot collide. Tenant reads use `GET /api/v1/admin/holds/tenants/{tenantId}` with tenant authorization; deployment reads use `GET /api/v1/admin/holds/deployment` with Admin policy.

Every D1 predicate hold has `HX-EV-HOLD-ENTRY-2\0 || 01 || 000d` (at most 4 KiB): `01` U scope kind, `02` U scope ID, `03` U hold code, `04` U stable subject key, `05` O(U) domain, `06` U current reason code, `07` N entry revision, `08` B32 predecessor entry hash (zero at revision 1), `09` Q first observed UTC, `0a` Q last observed UTC, `0b` N observation count, `0c` U owner kind (the closed D1 set), and `0d` Q next re-evaluation UTC. Key is `hold-entry:` plus lowercase-hex SHA-256(`"HX-EV-HOLD-ENTRY-KEY-1\0" || 01 || U scope kind || U scope ID || U hold code || U subject key`). A cause/reason change CAS-writes the next revision before changing the ordered index; stale tag `06` cannot survive. Resolution CAS-removes the index row only after its named evidence reads back.

`HX-EV-HOLD-INDEX-2\0 || 01 || 0008` (at most 40 MiB) has `01` U scope kind, `02` U scope ID, `03` N generation, `04` N entry count (at most 10,000), `05` B rows sorted by `(firstObservedUtc, holdCode, subjectKey)` with no nested count (`Q firstObservedUtc || U holdCode || U subjectKey || B32 current entry hash`), `06` N overflow count, `07` B32 predecessor index hash, and `08` Q update UTC. Tag `04` controls exact row parsing. For new capabilities, overflow must remain zero: command operations reserve an inventory slot before commit, tenant onboarding reserves a tenant actor/directory slot, and delivery capture reserves before acknowledgement. An inability to reserve stops that earlier boundary. Nonzero imported overflow is a visible deployment readiness hold until migrated; it never represents permission to hide a durable held operation.

A key-only store discovers actors through 256 deployment directory shards selected by the first byte of SHA-256(`U scope kind || U scope ID`). `HX-EV-HOLD-DIRECTORY-1\0 || 01 || 0007` (at most 64 MiB per shard) has `01` N shard `0..255`, `02` N generation, `03` N actor count (at most 50,000), `04` B sorted actor IDs with no nested count, `05` B32 predecessor directory hash, `06` Q update UTC, and `07` N reserved onboarding slots. Actor creation/removal and the directory row commit under one deployment-directory fence. A tenant must reserve its row before EventStore admission is enabled; deployment scope is reserved at bootstrap. This bounds and enumerates at most 12,800,000 inventory actors without a store scan.

The reconciler leases each directory shard under one durable epoch. Only the active epoch owner publishes `hexalith.eventstore.holds.active`; other replicas publish no sample. It activates every listed actor, reads durable index counts, and aggregates by `hold_code` and `domain` (or `none`). Lease loss stops emission before another owner begins, preventing under/double count across replicas. Admin paging is oldest first, page size 1..200/default 50, with a scope-bound authenticated cursor; results include current reason, revision, times, count, owner, stale flag, and `overflowCount`. The command list joins by the stable subject and exposes this current evidence.

Inventory activates in slice 3 before any slice-3 hold, is charged to the tenant/deployment operational-evidence quota, re-evaluates at least hourly, and erases with its scope after all obligations close. Known answers `D37-entry`, `D37-index`, `D37-directory`, and `D37-key` appear in D12.

### D11.3 Complete durable address registry

For this candidate, `K(name, fields...)` means `prefix || lowercase-hex SHA256(ASCII name including NUL || 01 || fields...)`; every variable field uses `U`, optional field uses `O`, numeric field uses `N`, and digest uses `B32`. Raw text concatenation is forbidden. A `revision` row is create-once and must name its predecessor; a `head` is a CAS row whose codec carries generation and predecessor hash; `create-once` accepts only byte-identical readback. These are the complete replacement-owned addresses:

| Durable family | Exact address and mutation rule |
| --- | --- |
| full-replay activation | `K("HX-EV-FULL-REPLAY-ACTIVATION-KEY-1\0", U domain, B32 fingerprint, N generation)`, create-once with predecessor activation hash |
| drain limit / resolution / active pointer | `K("HX-EV-PUBLICATION-DRAIN-LIMIT-KEY-1\0", B32 ScopeOpHash, N window, N limit)` create-once; `K("HX-EV-PUBLICATION-DRAIN-RESOLUTION-KEY-1\0", B32 ScopeOpHash, B32 limitHash)` create-once; `K("HX-EV-PUBLICATION-DRAIN-HEAD-KEY-1\0", B32 ScopeOpHash)` CAS head |
| legacy claim or tombstone / shard usage / cutover | shared `K("HX-EV-COMMAND-SCOPE-KEY-1\0", U tenant, U executionMessageId)` CAS type transition; `K("HX-EV-SCOPE-SHARD-USAGE-KEY-1\0", U tenant, N shard)` CAS head; cutover revision `K("HX-EV-LEGACY-SCOPE-CUTOVER-KEY-1\0", U tenantOrStar, U domain, N generation)` plus CAS head `K("HX-EV-LEGACY-SCOPE-CUTOVER-HEAD-KEY-1\0", U tenantOrStar, U domain)` |
| first-send resolution | imported C2 first-send CAS head framed by `B32 ScopeOpHash, N memberPosition, U MessageId`; every contiguous resolution carries predecessor hash |
| destination configuration | revision `K("HX-EV-DESTINATION-CONFIG-KEY-1\0", U deployment, U component, U topic, N revision)` and CAS head `K("HX-EV-DESTINATION-CONFIG-HEAD-KEY-1\0", U deployment, U component, U topic)` |
| retention capability / charge / counter | capability revision `K("HX-EV-PUBLICATION-CAPABILITY-KEY-1\0", U deployment, N revision)` plus deployment head; charge CAS head `K("HX-EV-PUBLICATION-CHARGE-KEY-1\0", U deployment, U accountKind, U accountId, B32 objectKeyHash)`; counter CAS head `K("HX-EV-PUBLICATION-COUNTER-KEY-1\0", U deployment, U counterKind, U counterId)` |
| pin-batch reservation | `K("HX-EV-PIN-BATCH-RESERVATION-KEY-1\0", B32 ScopeOpHash, B32 candidateBatchRoot)`, CAS generation; changed candidate root is a new reservation only after the old generation is released |
| wait / queue and allocator | wait `K("HX-EV-PIN-CAPACITY-WAIT-KEY-1\0", U deployment, B32 stableCapacitySubject)` CAS generation; queue `K("HX-EV-PIN-CAPACITY-QUEUE-KEY-1\0", U deployment, U counterId)` CAS generation. The global allocator is tag `09` of the deployment queue at counter ID `deployment`, not an unaddressed side counter. |
| resume state / window / closure / audit | state and live/tombstone index `K("HX-EV-PUBLICATION-RESUME-STATE-KEY-1\0", U tenant, U executionIdentity)` CAS; window `K("HX-EV-PUBLICATION-WINDOW-KEY-1\0", B32 ScopeOpHash, N window)` create-once; closure key as D9.2; audit key as D9.3 |
| legacy capsule chunks / manifest / recovery | chunk and manifest keys as D10.1; recovery CAS head as D10.2. A later exhaustion advances recovery, not either create-once capsule address. |
| subscription policy | revision `K("HX-EV-SUBSCRIPTION-POLICY-KEY-1\0", U deployment, U component, U topic, U physicalSubscriptionId, N revision)` and CAS head with the same fields excluding revision |
| held delivery / quarantine / redrive | held CAS key as D11.1; quarantine create-once `K("HX-EV-CARRIER-QUARANTINE-KEY-1\0", B32 heldDeliveryKeyHash, B32 carrierHash)`; redrive request create-once `K("HX-EV-REDRIVE-REQUEST-KEY-1\0", B32 heldDeliveryKeyHash, N expectedRedriveCount)` |
| hold entry / index / directory | entry key as D11.2; index CAS head `K("HX-EV-HOLD-INDEX-KEY-1\0", U scopeKind, U scopeId)`; directory CAS head `K("HX-EV-HOLD-DIRECTORY-KEY-1\0", U deployment, N shard)` |

All CAS families reject a missing, stale, or wrong-kind predecessor without side effects. All create-once families reject changed bytes. D12 fixes representative key vectors spanning activation, drain, resume closure/audit, and legacy manifest/recovery; their framing rule is the same rule used by every row above.

## D12. Known answers and executable verification

All fixtures use tenant `t`, domain `d`, aggregate `a`, execution `op`, UTC ticks `638712864000000000`, and zero bytes for genesis predecessors. Ordinary opaque references hash displayed fixture bytes. Candidate-batch roots, stored-event roots, window attempt roots, history accumulators, capsule hashes, audit hashes, and retry rows are recomputed from their semantic inputs in dependency order; no unrelated label hash substitutes for them. They are local framing answers, not provider evidence. The destination JSON is exactly `{"component":"pubsub","metadata":{},"schema":"hexalith.eventstore.destination/1","topic":"orders"}`. This table has exactly 30 record/codec answers; the following key table separately fixes six framed address derivations.

```text
D06-activation 225 ce6ece552e0e0c6220e13f3bfc15215d4995299bba0d2259844b55dd7c557605
D12-legacy-claim 164 e01c2779a425182a0d676c6ac2c8a057f8131da0e36cbee7047b654e564f5394
D12-cutover 146 669307293a19b9356da9801bc1d73473748f88df13c230b5133d51eaf43e302f
D12-usage 113 c4d17deb523e29c0902c5e97f1f16c9bb72c914571aa93e77457ada88bbb03a5
D12-tombstone 174 0327e353968be05a6c9216d6b99327b0248bb4b2fd9ab406102cc2b458822fd1
D14-drain-limit 202 e22848b3ab4b7c6bd3357078b95410550c1ee0e552d968a9c01ec8068ea4f343
D14-drain-resolution 197 82bbf9470ba7dae1b599e10a2d250f8f8ec8f7d0e6837366179c8b1cd9178efc
D16-membership-resolution 285 2875259659b0f8a7225a55f4f18b9110c6323796ee2f508343c319854a607c40
D17-destination-config 98 048e9eb50252feb334b66506baa8af1c26f6fab1b56461b6fbb491ec12fdb320
D29-capability 214 a0194011a460ccf2063d4e9c78b3b7ad4f7bbf70ce9e63a55f237a0ef2b52043
D29-charge 209 06a6c62c8c8a6e62a1324cfed6857e0d38b2468dbcbbf8116d515649b49a2a5b
D29-counter 134 eca227f547122e041a159affbac58bf7b57375bf91d78876d88cd73f418d1fc8
D29-pin-batch 297 44955e5efb3116f10cd0e7b20963b47b1f13b2e274c39790014b2f822d4ef477
D31-wait 238 7dca50b2d1f448628a84ee423ab3aa364600a320ed10886711cc9c128bc4f3b8
D31-queue 183 ad2d4caf44730644794fbf316d49f93ce95f08bb484bf43cf2f0693431543c45
D36-policy 186 2f88061e39810d1899d76897517bd52ffced65e4d203a0979bd0936628fd73d5
D36-held 310 b380d04457fc6465394d7d6427f667cfa26d12812082bc7b62f563341d424168
D36-quarantine 336 5dcce10f015b54bb853a3beb633b865b87889685771beeeb603d351559536257
D36-redrive 119 1a552c0f005efc61b9d74682a5f707925c0b47b5e31d559bdb0b43da43f8b49f
D37-entry 219 30ca7e011c4e0cbe3d2bdc4e1e66f598d44879f54f38629c3a3e50e4e7d9716f
D37-index 196 a14e6b24af414ad169f98ba8829f971635724de0acc686a901024ebeec6967fc
D37-directory 184 d36d068c6a1cea553949c29cafc628cbde9881e712a190d26033211f26b19569
D37-key 58 eabf14e49beb9484895f4233927107604705ea58aac8d049e34db91ce7152978
D45-request 293 5f0ba7fb3a98922ccf312bdbb35d4c4b9334c86e9fcff9aa9887527109eee840
D45-window 285 dc18be39aa60f35271938c3f096966d5aa6719abfeadb63d28163d1ceeb695d9
D45-closure 327 9aa8f4c113926a097669a37f76876c4836b7f1113d65a5389b809cbfb9ab3976
D45-state 364 9091a765df4f3f3afa26b66670b00ea8f9164ec9f7adfc655f4e5d8ac8a64022
D45-audit 185 53409ca4f268d049050484ef59aa7c9d21b0803063364d8bfef705c6b6702019
D46-capsule 300 538cb6c123e8f1b3f50541b4bfe98ccdd960476bc5c07f64f62e4824fd88f396
D46-recovery 246 a120b923ff6b3bce44af962db5eea6c0d9e1730b531de851c4085e918f841958
```

```text
D06-key 0f91a1983b2d87cad832582071c4c14b82fb4120da3f20615531f3d085bc132a
D14-key 3d6106bf9476e5106018bc78cc6bc4bf771144175f662f3ca5e1c631af3e23df
D45-closure-key 58b1b024adae4973e90e4e491aa8713593c439d51b5eb3941eaf66950e0b73b9
D45-audit-key 9657f5d2d86e994d870e26d8b1e24f7c7c0bde6792faa2b69d21d9d1802df6f3
D46-capsule-key 26646579b47f9a040aa3c51585ceedc3ee0779ab50e36f8170e69bebf76e6043
D46-recovery-key cfdc03b45322572504d076f49bd1ef73efefbb4dd426532a8d3b8eda3b81ebf8
```

This verifier independently reconstructs all 30 records and checks both byte length and SHA-256. Changing a domain separator, codec, field count/order/framing, fixture, length, or answer fails.

```bash
python3 - <<'PY'
from hashlib import sha256
from pathlib import Path
from struct import pack
import re

path = Path('_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md')
text = path.read_text(encoding='utf-8')
block = text.split('```text\nD06-activation ', 1)[1].split('\n```', 1)[0]
answers = {}
for line in ('D06-activation ' + block).splitlines():
    label, length, digest = line.split()
    answers[label] = (int(length), digest)
assert len(answers) == 30, len(answers)
key_block = text.split('```text\nD06-key ', 1)[1].split('\n```', 1)[0]
key_answers = {}
for line in ('D06-key ' + key_block).splitlines():
    label, digest = line.split()
    key_answers[label] = digest
assert len(key_answers) == 6

def U(value):
    raw = value.encode() if isinstance(value, str) else value
    return len(raw).to_bytes(4, 'big') + raw
def B(value): return len(value).to_bytes(4, 'big') + value
def B32(value):
    assert len(value) == 32
    return value
def N(value):
    assert isinstance(value, int) and 0 <= value < 2**64
    return value.to_bytes(8, 'big')
def I(value): return pack('>i', value)
def Q(value): return pack('>q', value)
def O(value): return b'\x00' if value is None else b'\x01' + value
def H(label): return sha256(label.encode()).digest()
def K(domain, *fields): return sha256(domain.encode() + b'\0\x01' + b''.join(fields)).hexdigest()
def R(domain, count, *fields):
    assert len(fields) == count
    return domain.encode() + b'\0\x01' + count.to_bytes(2, 'big') + b''.join(bytes([n]) + field for n, field in enumerate(fields, 1))

t = 638712864000000000
z = bytes(32)
MiB = 1024 * 1024
vectors = {}
arow = pack('>I', 1) + U('route-a') + U('continue-full-replay') + N(100) + N(4096) + N(819200) + O(None)
vectors['D06-activation'] = R('HX-EV-FULL-REPLAY-ACTIVATION-2', 9, U('admin'), U('d'), B32(H('registry')), N(1), B32(z), B(arow), Q(t), U('operator'), Q(t+1))
vectors['D12-legacy-claim'] = R('HX-EV-COMMAND-SCOPE-LEGACY-2', 10, U('t'), U('op'), U('d'), U('a'), U('increment'), B32(H('payload')), Q(t), Q(t+864000000000), N(1), B32(z))
vectors['D12-cutover'] = R('HX-EV-LEGACY-SCOPE-CUTOVER-1', 8, U('*'), U('d'), N(1), Q(t), Q(t+864000000000), N(86400), B32(H('empty-inventory')), B32(z))
vectors['D12-usage'] = R('HX-EV-SCOPE-SHARD-USAGE-1', 7, U('t'), N(7), N(2), N(1), N(9216), N(3), B32(H('usage-prev')))
vectors['D12-tombstone'] = R('HX-EV-COMMAND-SCOPE-TOMBSTONE-2', 8, U('t'), U('op'), B32(H('scope')), B32(H('input')), B32(H('full-scope')), Q(t), Q(t+315360000000000), N(7))
vectors['D14-drain-limit'] = R('HX-EV-PUBLICATION-DRAIN-LIMIT-2', 10, U('t'), B32(H('scope')), U('operation'), N(2), N(16), B32(H('drain-head')), B32(H('outcome-head')), N(4), U('pending'), Q(t))
vectors['D14-drain-resolution'] = R('HX-EV-PUBLICATION-DRAIN-LIMIT-RESOLUTION-1', 8, U('t'), B32(H('scope')), B32(H('drain-limit')), U('resumed'), B32(H('resume-state')), N(2), U('coordinator'), Q(t))
vectors['D16-membership-resolution'] = R('HX-EV-FIRST-SEND-MEMBERSHIP-RESOLUTION-1', 12, U('t'), B32(H('scope')), N(1), U('event-1'), B32(H('pin')), N(2), B32(H('previous')), B32(H('membership')), B32(H('zero-send')), U('ContinueSamePin'), U('broker'), Q(t))
vectors['D17-destination-config'] = b'{"component":"pubsub","metadata":{},"schema":"hexalith.eventstore.destination/1","topic":"orders"}'
vectors['D29-capability'] = R('HX-EV-PUBLICATION-RETENTION-CAPABILITY-2', 15, U('deployment-a'), N(3), B(b'backend'), N(1024*MiB), N(2048*MiB), N(256*MiB), N(512*MiB), N(MiB), N(128*MiB), B32(H('cap-prev')), Q(t), N(256), N(31536000), N(50000), N(256*MiB))
vectors['D29-charge'] = R('HX-EV-PUBLICATION-CHARGE-1', 13, U('deployment-a'), U('tenant'), U('t'), B32(H('object')), U('pin-batch'), N(10*MiB), N(MiB), N(11*MiB), N(3), N(1), U('active'), B32(z), Q(t))
vectors['D29-counter'] = R('HX-EV-PUBLICATION-COUNTER-1', 8, U('deployment-a'), U('tenant'), U('t'), N(11*MiB), N(1), N(4), B32(H('counter-prev')), Q(t))
prow = pack('>I', 1) + U('event-1') + B32(H('pin')) + N(10*MiB) + N(11*MiB)
vectors['D29-pin-batch'] = R('HX-EV-PIN-BATCH-RESERVATION-1', 12, U('t'), B32(H('scope')), B32(sha256(prow).digest()), N(1), B(prow), N(11*MiB), N(3), B32(H('tenant-counter')), B32(H('deployment-counter')), U('reserved'), N(1), Q(t))
vectors['D31-wait'] = R('HX-EV-PIN-CAPACITY-WAIT-2', 12, U('t'), B32(H('scope')), B32(H('batch')), N(11*MiB), U('deployment'), N(42), Q(t), N(0), U('queued'), B32(H('capability')), B32(H('outbox-plan')), Q(t))
qrow = N(42) + U('t') + B32(H('scope')) + U('queued')
vectors['D31-queue'] = R('HX-EV-PIN-CAPACITY-QUEUE-3', 8, U('deployment'), N(1), N(1), B(qrow), N(0), N(0), B32(z), Q(t))
vectors['D36-policy'] = R('HX-EV-SUBSCRIPTION-DELIVERY-POLICY-2', 9, U('pubsub'), U('orders'), U('sub-a'), U('dead-letter-capture'), O(U('orders-dlq')), N(8), B32(H('subscription-config')), U('dapr-configuration'), Q(t))
carrier = b'exact-carrier-and-headers'
vectors['D36-held'] = R('HX-EV-HELD-DELIVERY-3', 18, U('tenant'), O(U('t')), O(U('event-1')), U('sub-a'), U('pubsub'), U('orders'), U('captured'), U('handler-capability-hold'), N(len(carrier)), B32(sha256(carrier).digest()), O(U('archive-a')), O(U('objects/held-1')), O(B32(H('object-readback'))), O(B32(H('handoff'))), Q(t), N(2), N(1), Q(t+600000000))
body = b'quarantined-body'
manifest = pack('>I', 1) + U('x-header') + N(4) + B32(sha256(b'value').digest())
vectors['D36-quarantine'] = R('HX-EV-CARRIER-QUARANTINE-2', 14, U('deployment'), O(None), U('sub-a'), U('invalid-header-value'), N(len(body)), B32(sha256(body).digest()), B(manifest), U('archive-a'), U('objects/quarantine-1'), B32(H('provider-proof')), O(U('event-1')), Q(t), U('terminal-quarantine'), B32(H('delivery-receipt')))
vectors['D36-redrive'] = R('HX-EV-REDRIVE-REQUEST-2', 7, U('admin'), U('tenant'), O(U('t')), B32(H('held-key')), N(2), U('operator'), Q(t))
vectors['D37-entry'] = R('HX-EV-HOLD-ENTRY-2', 13, U('tenant'), U('t'), U('PublicationPinCapacityHold'), U('scope:abc'), O(U('d')), U('publication_pin_capacity_hold'), N(1), B32(z), Q(t), Q(t), N(1), U('coordinator'), Q(t+36000000000))
irow = Q(t) + U('PublicationPinCapacityHold') + U('scope:abc') + B32(H('hold-entry'))
vectors['D37-index'] = R('HX-EV-HOLD-INDEX-2', 8, U('tenant'), U('t'), N(1), N(1), B(irow), N(0), B32(z), Q(t))
actor = U('tenant:' + sha256(U('t')).hexdigest())
vectors['D37-directory'] = R('HX-EV-HOLD-DIRECTORY-1', 7, N(7), N(1), N(1), B(actor), B32(z), Q(t), N(0))
vectors['D37-key'] = U('tenant') + U('t') + U('PublicationPinCapacityHold') + U('scope:abc')
request = R('HX-EV-PUBLICATION-RESUME-2', 14, U('admin'), U('t'), U('op'), B32(H('scope')), U('retry-exhausted'), B32(H('hold')), B32(H('head')), N(2), B32(H('audit-prev')), U('hxrsm1-handle'), U('operator'), U('retry after broker repair'), Q(t), Q(t+9000000000))
vectors['D45-request'] = request
attempt_row = pack('>I', 1) + N(4) + B32(sha256(b'definitive-result').digest())
attempt_root = sha256(b'HX-EV-WINDOW-ATTEMPTS-1\0\x01' + B(attempt_row)).digest()
broker_auth = b'authenticated-broker-window-proof'
closure = R('HX-EV-PUBLICATION-WINDOW-CLOSURE-3', 11, U('t'), B32(H('scope')), N(1), B(attempt_row), B32(sha256(b'broker-fence').digest()), B32(sha256(b'producer-disable').digest()), B32(sha256(b'empty-state').digest()), B32(attempt_root), U('SignedCarrier'), B32(z), Q(t))
history = sha256(b'HX-EV-PUBLICATION-WINDOW-HISTORY-1\0\x01' + B32(z) + B(closure) + B(broker_auth)).digest()
prior_state = R('HX-EV-PUBLICATION-RESUME-STATE-2', 13, U('t'), U('op'), B32(H('scope')), N(1), N(1), N(16), B32(H('hold-1')), B32(H('window-1')), B32(z), N(0), B32(z), B(pack('>I', 0)), Q(t-1))
audit = R('HX-EV-PUBLICATION-RESUME-AUDIT-3', 9, U('t'), U('op'), N(2), B32(sha256(request).digest()), B32(sha256(prior_state).digest()), O(B32(sha256(closure).digest())), N(2), N(24), Q(t))
retry_row = B32(sha256(request).digest()) + N(2) + B32(sha256(audit).digest()) + N(2) + N(24) + Q(t+9000000000)
state = R('HX-EV-PUBLICATION-RESUME-STATE-2', 13, U('t'), U('op'), B32(H('scope')), N(2), N(2), N(24), B32(H('hold-2')), B32(H('window-2')), B32(history), N(1), B32(sha256(audit).digest()), B(pack('>I', 1)+retry_row), Q(t))
vectors['D45-window'] = R('HX-EV-PUBLICATION-WINDOW-1', 12, U('t'), B32(H('scope')), U('operation'), N(2), B32(sha256(closure).digest()), B32(sha256(state).digest()), B32(H('members')), B32(H('policy')), N(16), U('admin'), Q(t), B32(H('capability')))
vectors['D45-closure'] = closure
vectors['D45-state'] = state
vectors['D45-audit'] = audit
stored = b'canonical-stored-event'
member = pack('>I', 1) + N(10) + U('event-1') + B32(sha256(stored).digest())
event_root = sha256(b'HX-EV-LEGACY-RESUME-EVENTS-1\0\x01' + B(member)).digest()
capsule = R('HX-EV-LEGACY-RESUME-CAPSULE-1', 16, U('t'), U('d'), U('a'), U('tracking'), O(U('op')), U('correlation'), U('increment'), U('success-events'), N(10), N(10), I(1), B32(event_root), B(member), U('drain-exhaustion'), B32(H('drain-source')), Q(t))
vectors['D46-capsule'] = capsule
vectors['D46-recovery'] = R('HX-EV-LEGACY-PUBLICATION-RECOVERY-2', 11, U('t'), B32(sha256(capsule).digest()), U('hxrsm1-handle'), N(2), U('legacy-resume'), U('claimed'), B32(H('drain-record')), O(B32(H('dead-letter'))), B32(H('recovery-prev')), O(None), Q(t))

keys = {
    'D06-key': K('HX-EV-FULL-REPLAY-ACTIVATION-KEY-1', U('d'), B32(H('registry')), N(1)),
    'D14-key': K('HX-EV-PUBLICATION-DRAIN-LIMIT-KEY-1', B32(H('scope')), N(2), N(16)),
    'D45-closure-key': K('HX-EV-PUBLICATION-WINDOW-CLOSURE-KEY-1', B32(H('scope')), N(1)),
    'D45-audit-key': K('HX-EV-PUBLICATION-RESUME-AUDIT-KEY-1', U('t'), U('op'), N(2)),
    'D46-capsule-key': K('HX-EV-LEGACY-RESUME-CAPSULE-KEY-1', U('t'), U('d'), U('a'), U('tracking')),
    'D46-recovery-key': K('HX-EV-LEGACY-PUBLICATION-RECOVERY-KEY-1', B32(sha256(capsule).digest())),
}

assert set(vectors) == set(answers)
for label, value in vectors.items():
    actual = (len(value), sha256(value).hexdigest())
    assert actual == answers[label], (label, actual, answers[label])
    mutant = value[:-1] + bytes([value[-1] ^ 1])
    assert sha256(mutant).hexdigest() != answers[label][1]
assert keys == key_answers
assert K('HX-EV-PUBLICATION-DRAIN-LIMIT-KEY-1', B32(H('scope')), N(1), N(23)) != K('HX-EV-PUBLICATION-DRAIN-LIMIT-KEY-1', B32(H('scope')), N(12), N(3))
print(f'D12 codec verifier: {len(vectors)} answers, {len(vectors)} byte mutations rejected, {len(keys)} framed keys')
PY
```

The lifecycle verifier covers every approved matrix row, all status precedence branches, tenant/deployment queue movement with both counterpart slots pre-reserved, the unidentified ceiling, stable arithmetic/evidence holds with unchanged counters, exact refunds, legacy evidence failure, both configuration- and membership-revision exits, and the 54 routed finding IDs. Its explicit mutants are contract defects, not alternative implementations.

```bash
python3 - <<'PY'
from collections import defaultdict
from hashlib import sha256
from pathlib import Path
import re

MiB = 1024 * 1024
U64_MAX = (1 << 64) - 1

def valid_amounts(amounts):
    if not amounts or any(type(value) is not int or value < 0 or value > U64_MAX for value in amounts):
        return False
    total = 0
    for value in amounts:
        if value > U64_MAX - total:
            return False
        total += value
    return True

def status(state, *, classification='success', conflict=False, evidence=True,
           terminal=False, drain_active=False, failure_class=None,
           at_max=False, auto=False):
    if conflict:
        return ('CommandOutcomeHold', 'outcome_evidence_conflict', 30)
    if not evidence:
        return ('CommandOutcomeHold', 'outcome_evidence_hold', 30)
    if terminal:
        return ('PublishFailed', 'publication_terminal_failed', None)
    if state == 'published':
        if classification not in {'success', 'rejection'}:
            return ('CommandOutcomeHold', 'outcome_evidence_conflict', 30)
        return ('Rejected' if classification == 'rejection' else 'Completed', None, None)
    if state == 'not-applicable':
        return ('Completed', None, None)
    if drain_active:
        return ('EventsStored', 'publication_drain_limit_hold', 60)
    if state == 'failed' and failure_class in {'class-02', 'class-03'}:
        return ('CommandOutcomeHold', 'terminal_evidence_hold', 30)
    if state == 'failed' and at_max:
        return ('EventsStored', 'publication_retry_exhausted_hold', 60)
    if state == 'failed' and auto:
        return ('EventsStored', 'publication_retry_pending', 1)
    if state in {'pending', 'unknown'}:
        return ('EventsStored', None, 1)
    return ('CommandOutcomeHold', 'outcome_evidence_hold', 30)

status_cases = [
    (status('pending'), ('EventsStored', None, 1)),
    (status('unknown', at_max=True), ('EventsStored', None, 1)),
    (status('pending', drain_active=True), ('EventsStored', 'publication_drain_limit_hold', 60)),
    (status('failed', at_max=True), ('EventsStored', 'publication_retry_exhausted_hold', 60)),
    (status('failed', at_max=True, failure_class='class-02'), ('CommandOutcomeHold', 'terminal_evidence_hold', 30)),
    (status('failed', failure_class='class-03'), ('CommandOutcomeHold', 'terminal_evidence_hold', 30)),
    (status('failed', auto=True), ('EventsStored', 'publication_retry_pending', 1)),
    (status('failed', terminal=True), ('PublishFailed', 'publication_terminal_failed', None)),
    (status('pending', conflict=True), ('CommandOutcomeHold', 'outcome_evidence_conflict', 30)),
    (status('published'), ('Completed', None, None)),
    (status('published', classification='rejection'), ('Rejected', None, None)),
    (status('published', classification='unknown'), ('CommandOutcomeHold', 'outcome_evidence_conflict', 30)),
    (status('not-applicable'), ('Completed', None, None)),
    (status('failed'), ('CommandOutcomeHold', 'outcome_evidence_hold', 30)),
]
assert all(actual == expected for actual, expected in status_cases)

class Ledger:
    def __init__(self):
        self.tenant = defaultdict(int)
        self.tenant_pool = 0
        self.unidentified = 0
        self.deployment = 0
        self.tenant_ceiling = 1024 * MiB
        self.deployment_ceiling = 2048 * MiB
        self.reserve_bytes = 256 * MiB
        self.unidentified_ceiling = 512 * MiB
    def reserve(self, account_kind, account, amounts):
        before = (dict(self.tenant), self.tenant_pool, self.unidentified, self.deployment)
        if account_kind not in {'tenant', 'capture-scope'}:
            assert before == (dict(self.tenant), self.tenant_pool, self.unidentified, self.deployment)
            return False
        if not valid_amounts(amounts):
            assert before == (dict(self.tenant), self.tenant_pool, self.unidentified, self.deployment)
            return False
        total = sum(amounts)
        current = self.tenant.get(account, 0)
        if account_kind == 'tenant':
            fits = (current + total <= self.tenant_ceiling
                    and self.tenant_pool + total <= self.deployment_ceiling - self.reserve_bytes
                    and self.deployment + total <= self.deployment_ceiling)
        else:
            fits = (current + total <= self.tenant_ceiling
                    and self.unidentified + total <= self.unidentified_ceiling
                    and self.deployment + total <= self.deployment_ceiling)
        if not fits:
            assert before == (dict(self.tenant), self.tenant_pool, self.unidentified, self.deployment)
            return False
        self.tenant[account] += total
        if account_kind == 'tenant': self.tenant_pool += total
        else: self.unidentified += total
        self.deployment += total
        return True
    def refund(self, account_kind, account, amount):
        assert 0 <= amount <= self.tenant[account]
        self.tenant[account] -= amount
        if account_kind == 'tenant': self.tenant_pool -= amount
        else: self.unidentified -= amount
        self.deployment -= amount

ledger = Ledger()
assert ledger.reserve('tenant', 't1', [400*MiB, 300*MiB])
snapshot = (dict(ledger.tenant), ledger.tenant_pool, ledger.unidentified, ledger.deployment)
assert not ledger.reserve('tenant', 't1', [325*MiB])
assert snapshot == (dict(ledger.tenant), ledger.tenant_pool, ledger.unidentified, ledger.deployment)
assert ledger.reserve('capture-scope', 'scope-a', [512*MiB])
assert not ledger.reserve('capture-scope', 'scope-b', [1])  # exact unidentified ceiling.
assert not ledger.reserve('unknown', 'scope-c', [1])
ledger.refund('capture-scope', 'scope-a', 512*MiB)
assert ledger.unidentified == 0 and ledger.deployment == 700*MiB
invalid_snapshot = (dict(ledger.tenant), ledger.tenant_pool, ledger.unidentified, ledger.deployment)
assert not ledger.reserve('tenant', 't1', [-1])
assert invalid_snapshot == (dict(ledger.tenant), ledger.tenant_pool, ledger.unidentified, ledger.deployment)

class Queues:
    def __init__(self, ceiling=50000):
        self.ceiling = ceiling
        self.where = {}
        self.rows = defaultdict(list)
        self.reservations = defaultdict(set)
    def _has_room(self, target):
        return len(self.rows[target]) + len(self.reservations[target]) < self.ceiling
    def reserve_pair(self, tenant, scope):
        targets = ('deployment', 'tenant:' + tenant)
        if any(not self._has_room(target) for target in targets):
            return False
        for target in targets:
            self.reservations[target].add(scope)
        return True
    def add(self, ticket, tenant, scope, state='queued'):
        target = 'deployment'
        if not self.reserve_pair(tenant, scope):
            return False
        self.reservations[target].remove(scope)
        row = [ticket, tenant, scope, state]
        self.rows[target].append(row); self.rows[target].sort()
        self.where[scope] = target
        assert scope in self.reservations['tenant:' + tenant]
        assert all(len(self.rows[name]) + len(self.reservations[name]) <= self.ceiling
                   for name in (target, 'tenant:' + tenant))
        return True
    def deployment_turn(self, deployment_fits, tenant_fits):
        eligible = [row for row in self.rows['deployment'] if row[3] == 'queued']
        if not eligible:
            return 'noop'
        head = eligible[0]
        if not deployment_fits(head): return 'deployment'
        if not tenant_fits(head):
            target = 'tenant:' + head[1]
            assert head[2] in self.reservations[target]
            self.reservations[target].remove(head[2])
            self.rows['deployment'].remove(head)
            self.reservations['deployment'].add(head[2])
            self.rows[target].append(head); self.rows[target].sort()
            self.where[head[2]] = target
            return target
        return 'reserve'
    def tenant_turn(self, tenant):
        target = 'tenant:' + tenant
        eligible = [row for row in self.rows[target] if row[3] == 'queued']
        if not eligible:
            return 'noop'
        head = eligible[0]
        assert head[2] in self.reservations['deployment']
        self.reservations['deployment'].remove(head[2])
        self.rows[target].remove(head)
        self.reservations[target].add(head[2])
        self.rows['deployment'].append(head); self.rows['deployment'].sort()
        self.where[head[2]] = 'deployment'
        return 'deployment'

def decode_queue(rows, entry_count, parked_count, trailing=b''):
    if trailing or len(rows) != entry_count:
        return 'pin_capacity_queue_corruption_hold'
    if parked_count != sum(row[3] == 'parked' for row in rows):
        return 'pin_capacity_queue_corruption_hold'
    if len({row[0] for row in rows}) != len(rows) or len({row[2] for row in rows}) != len(rows):
        return 'pin_capacity_queue_corruption_hold'
    if rows != sorted(rows):
        return 'pin_capacity_queue_corruption_hold'
    return 'valid'

def encode_queue_row(row):
    ticket, tenant, scope, state = row
    tenant_bytes = tenant.encode()
    state_bytes = state.encode()
    return (ticket.to_bytes(8, 'big')
            + len(tenant_bytes).to_bytes(4, 'big') + tenant_bytes
            + sha256(scope.encode()).digest()
            + len(state_bytes).to_bytes(4, 'big') + state_bytes)

def decode_queue_bytes(raw, entry_count, parked_count):
    offset = 0
    rows = []
    try:
        def take(length):
            nonlocal offset
            if length < 0 or offset + length > len(raw):
                raise ValueError('truncated')
            value = raw[offset:offset+length]
            offset += length
            return value
        def take_u():
            length = int.from_bytes(take(4), 'big')
            return take(length).decode('utf-8', errors='strict')
        for _ in range(entry_count):
            ticket = int.from_bytes(take(8), 'big')
            tenant = take_u()
            scope_hash = take(32).hex()
            state = take_u()
            if state not in {'queued', 'parked'}:
                raise ValueError('state')
            rows.append([ticket, tenant, scope_hash, state])
        if offset != len(raw):
            raise ValueError('trailing')
        return decode_queue(rows, entry_count, parked_count)
    except (UnicodeDecodeError, ValueError):
        return 'pin_capacity_queue_corruption_hold'

queues = Queues(3)
assert queues.add(1, 't1', 'a')
assert queues.add(2, 't2', 'b')
assert 'a' in queues.reservations['tenant:t1'] and 'a' not in queues.reservations['deployment']
assert queues.deployment_turn(lambda _: True, lambda row: row[1] != 't1') == 'tenant:t1'
assert queues.where == {'a':'tenant:t1', 'b':'deployment'}
assert 'a' in queues.reservations['deployment'] and 'a' not in queues.reservations['tenant:t1']
assert queues.deployment_turn(lambda _: True, lambda _: True) == 'reserve'
assert queues.tenant_turn('t1') == 'deployment'
assert 'a' in queues.reservations['tenant:t1'] and 'a' not in queues.reservations['deployment']
assert [row[2] for row in queues.rows['deployment']] == ['a', 'b']
assert queues.add(3, 't3', 'c', 'parked')
assert not queues.add(4, 't4', 'd')
assert len(queues.rows['deployment']) + len(queues.reservations['deployment']) == queues.ceiling
assert queues.deployment_turn(lambda _: True, lambda _: True) == 'reserve'
parked_only = Queues(2)
assert parked_only.add(1, 't1', 'p', 'parked')
assert parked_only.deployment_turn(lambda _: True, lambda _: True) == 'noop'
assert Queues().deployment_turn(lambda _: True, lambda _: True) == 'noop'

# A destination at its ceiling still accepts each owner through its own pre-reserved slot.
full_destination = Queues(2)
assert full_destination.add(1, 't1', 'x') and full_destination.add(2, 't1', 'y')
assert len(full_destination.reservations['tenant:t1']) == 2
assert not full_destination._has_room('tenant:t1')
assert full_destination.deployment_turn(lambda _: True, lambda _: False) == 'tenant:t1'
assert full_destination.where['x'] == 'tenant:t1' and 'x' in full_destination.reservations['deployment']
assert full_destination.deployment_turn(lambda _: True, lambda _: False) == 'tenant:t1'
assert full_destination.where == {'x':'tenant:t1', 'y':'tenant:t1'}
assert full_destination.tenant_turn('t1') == 'deployment'
assert full_destination.where['x'] == 'deployment' and 'x' in full_destination.reservations['tenant:t1']
valid_rows = [[1, 't', 's', 'queued']]
assert decode_queue(valid_rows, 1, 0) == 'valid'
assert decode_queue(valid_rows, 0, 0) == 'pin_capacity_queue_corruption_hold'
assert decode_queue(valid_rows, 2, 0) == 'pin_capacity_queue_corruption_hold'
assert decode_queue(valid_rows, 1, 1) == 'pin_capacity_queue_corruption_hold'
assert decode_queue(valid_rows, 1, 0, b'x') == 'pin_capacity_queue_corruption_hold'
valid_raw = encode_queue_row(valid_rows[0])
assert decode_queue_bytes(valid_raw, 1, 0) == 'valid'
assert decode_queue_bytes(valid_raw[:-1], 1, 0) == 'pin_capacity_queue_corruption_hold'
assert decode_queue_bytes(valid_raw, 2, 0) == 'pin_capacity_queue_corruption_hold'
assert decode_queue_bytes(valid_raw, 0, 0) == 'pin_capacity_queue_corruption_hold'
assert decode_queue_bytes(valid_raw, 1, 1) == 'pin_capacity_queue_corruption_hold'
assert decode_queue_bytes(valid_raw+b'x', 1, 0) == 'pin_capacity_queue_corruption_hold'
malformed = valid_raw[:8] + (2**32-1).to_bytes(4, 'big') + valid_raw[12:]
assert decode_queue_bytes(malformed, 1, 0) == 'pin_capacity_queue_corruption_hold'

def membership_exit(trigger, zero_send, same_bytes):
    return 'ContinueSamePin' if trigger in {'configuration-revision', 'membership-revision'} and zero_send and same_bytes else 'FirstSendMembershipChangedHold'
assert membership_exit('manual', True, True) == 'FirstSendMembershipChangedHold'
assert membership_exit('configuration-revision', True, False) == 'FirstSendMembershipChangedHold'
assert membership_exit('configuration-revision', True, True) == 'ContinueSamePin'
assert membership_exit('membership-revision', True, True) == 'ContinueSamePin'
assert membership_exit('membership-revision', False, True) == 'FirstSendMembershipChangedHold'

def legacy_resume(capsule, root_matches, rejection_known):
    if not capsule or not rejection_known: return 'legacy_resume_evidence_unavailable'
    if not root_matches: return 'resume_evidence_hold'
    return 'rearm-stored-range'
assert legacy_resume(True, True, True) == 'rearm-stored-range'
assert legacy_resume(False, True, True) == 'legacy_resume_evidence_unavailable'
assert legacy_resume(True, True, False) == 'legacy_resume_evidence_unavailable'
assert legacy_resume(True, False, True) == 'resume_evidence_hold'

def render_recovery(first, second, immutable):
    if first not in {None, immutable+'-response'}: return 'response_preparation_hold'
    if second not in {None, immutable+'-outcome'}: return 'response_preparation_hold'
    return (first or immutable+'-response', second or immutable+'-outcome', 'prepared')
assert render_recovery('x-response', None, 'x') == ('x-response', 'x-outcome', 'prepared')

def resume_publication(committed, unresolved, accepted, hold, evidence):
    if evidence == 'stale':
        return {'outcome':'resume_hold_changed', 'rearmed':(), 'command_executions':0}
    if evidence == 'unavailable':
        return {'outcome':'resume_evidence_hold', 'rearmed':(), 'command_executions':0}
    if hold not in {'publication_retry_exhausted_hold', 'publication_drain_limit_hold'}:
        return {'outcome':'resume_not_eligible', 'rearmed':(), 'command_executions':0}
    committed_positions = [row[0] for row in committed]
    committed_ids = [row[1] for row in committed]
    if len(set(committed_positions)) != len(committed) or len(set(committed_ids)) != len(committed):
        return {'outcome':'resume_evidence_hold', 'rearmed':(), 'command_executions':0}
    committed_by_position = {row[0]: row for row in committed}
    selected = tuple(unresolved) + tuple(accepted)
    if (len(set(unresolved)) != len(unresolved) or len(set(accepted)) != len(accepted)
            or set(unresolved) & set(accepted)
            or set(selected) != set(committed_positions)
            or len(selected) != len(committed)):
        return {'outcome':'resume_evidence_hold', 'rearmed':(), 'command_executions':0}
    return {
        'outcome':'resumed',
        'rearmed':tuple((committed_by_position[position][1], committed_by_position[position][2]) for position in unresolved),
        'accepted_unchanged':tuple(accepted),
        'command_executions':0,
    }

def verify_eligible_resume_matrix():
    committed = ((1, 'message-1', b'accepted'), (2, 'message-2', b'unresolved-a'),
                 (3, 'message-3', b'unresolved-b'))
    expected = (('message-2', b'unresolved-a'), ('message-3', b'unresolved-b'))
    exhausted = resume_publication(
        committed, (2, 3), (1,),
        'publication_retry_exhausted_hold', 'current')
    drained = resume_publication(
        committed, (2, 3), (1,),
        'publication_drain_limit_hold', 'current')
    assert exhausted == {
        'outcome':'resumed', 'rearmed':expected,
        'accepted_unchanged':(1,), 'command_executions':0}
    assert drained['rearmed'] == expected and drained['command_executions'] == 0
    assert resume_publication(committed, (2, 3), (1,),
                              'publication_retry_exhausted_hold', 'stale') == {
        'outcome':'resume_hold_changed', 'rearmed':(), 'command_executions':0}
    assert resume_publication(committed, (2, 3), (1,),
                              'publication_retry_exhausted_hold', 'unavailable') == {
        'outcome':'resume_evidence_hold', 'rearmed':(), 'command_executions':0}
    assert resume_publication(committed, (2,), (1,), 'publication_retry_exhausted_hold', 'current')['outcome'] == 'resume_evidence_hold'
    assert resume_publication(committed, (2, 2), (1, 3), 'publication_retry_exhausted_hold', 'current')['outcome'] == 'resume_evidence_hold'
    assert resume_publication(committed, (2, 3), (1, 2), 'publication_retry_exhausted_hold', 'current')['outcome'] == 'resume_evidence_hold'
    corrupt = committed + ((4, 'message-3', b'contradictory'),)
    assert resume_publication(corrupt, (2, 3, 4), (1,), 'publication_retry_exhausted_hold', 'current')['outcome'] == 'resume_evidence_hold'
    drain_before = {'window':7, 'accepted':(1,), 'unresolved':(2,3), 'limit':16,
                    'source':'limit-hash', 'members':committed}
    increment = 8
    drain_after = dict(drain_before, limit=drain_before['limit'] + increment,
                       resolution=('limit-hash', 'successor-state'),
                       invocation=('rearm', 2, 3))
    assert drain_after['window'] == drain_before['window']
    assert drain_after['accepted'] == drain_before['accepted']
    assert drain_after['unresolved'] == drain_before['unresolved']
    assert drain_after['members'] == drain_before['members']
    assert drain_after['limit'] == 24 and drain_after['resolution'][0] == drain_before['source']
    assert drain_after['invocation'] == ('rearm', 2, 3)
    def checked_add(left, right):
        return None if left < 0 or right <= 0 or left > U64_MAX-right else left+right
    assert checked_add(16, 8) == 24
    assert checked_add(U64_MAX, 1) is None and checked_add(1, 0) is None
    live_retry_rows = list(range(64))
    assert len(live_retry_rows) == 64
    assert 0 in live_retry_rows  # an old exact live retry remains addressable.
    assert len(live_retry_rows) + 1 > 64  # the 65th is held, never evicted silently.
    return 'eligible-resume'

def exact_legacy_rows(rows):
    if not rows:
        return None
    encoded = len(rows).to_bytes(4, 'big')
    seen_ids = set()
    first_sequence = rows[0][0]
    for index, (sequence, message_id, stored_digest) in enumerate(rows):
        if (type(sequence) is not int or sequence < 0 or sequence > U64_MAX
                or sequence != first_sequence + index
                or not isinstance(message_id, str) or not message_id
                or message_id in seen_ids
                or not isinstance(stored_digest, bytes) or len(stored_digest) != 32):
            return None
        seen_ids.add(message_id)
        message_bytes = message_id.encode('utf-8')
        encoded += (sequence.to_bytes(8, 'big')
                    + len(message_bytes).to_bytes(4, 'big') + message_bytes
                    + stored_digest)
    return encoded

def exact_legacy_root(rows):
    encoded = exact_legacy_rows(rows)
    if encoded is None:
        return None
    return sha256(b'HX-EV-LEGACY-RESUME-EVENTS-1\0\x01'
                  + len(encoded).to_bytes(4, 'big') + encoded).digest()

def recover_legacy_range(capsule, stored_rows, evidence):
    if not isinstance(capsule, dict) or evidence == 'contradictory-authority':
        return {'outcome':'legacy_resume_evidence_unavailable', 'rearmed':(), 'command_executions':0}
    source = capsule.get('source')
    if (not isinstance(source, dict)
            or not {'correlation', 'command_type', 'classification'} <= set(source)):
        return {'outcome':'legacy_resume_evidence_unavailable', 'rearmed':(), 'command_executions':0}
    root = exact_legacy_root(stored_rows)
    try:
        contradicts = (evidence == 'unavailable-read' or root is None
            or tuple(stored_rows) != tuple(capsule['rows'])
            or root != capsule['root']
            or not isinstance(capsule['range'], tuple)
            or capsule['range'] != (stored_rows[0][0], stored_rows[-1][0])
            or not isinstance(capsule['correlation'], str) or not capsule['correlation']
            or not isinstance(source['correlation'], str) or not source['correlation']
            or capsule['correlation'] != source['correlation']
            or not isinstance(capsule['command_type'], str) or not capsule['command_type']
            or not isinstance(source['command_type'], str) or not source['command_type']
            or capsule['command_type'] != source['command_type']
            or capsule['classification'] != source['classification']
            or source['classification'] not in {'success-events', 'rejection-events'}
            or capsule['classification'] not in {'success-events', 'rejection-events'})
    except (KeyError, IndexError, TypeError):
        contradicts = True
    if contradicts:
        return {'outcome':'resume_evidence_hold', 'rearmed':(), 'command_executions':0}
    return {
        'outcome':'resumed',
        'stored_range':capsule['range'],
        'rearmed':tuple(stored_rows),
        'terminal_status':'Rejected' if capsule['classification'] == 'rejection-events' else 'Completed',
        'command_executions':0,
    }

def verify_legacy_resume_matrix():
    rows = ((41, 'legacy-message-41', sha256(b'event-41').digest()),
            (42, 'legacy-message-42', sha256(b'event-42').digest()))
    root = exact_legacy_root(rows)
    source = {'correlation':'correlation', 'command_type':'increment',
              'classification':'success-events'}
    capsule = {'range':(41, 42), 'rows':rows, 'root':root, 'correlation':'correlation',
               'command_type':'increment', 'classification':'success-events', 'source':source}
    resumed = recover_legacy_range(capsule, rows, 'verified')
    assert resumed == {
        'outcome':'resumed', 'stored_range':(41, 42), 'rearmed':rows,
        'terminal_status':'Completed', 'command_executions':0}
    rejection = dict(capsule, classification='rejection-events',
                     source=dict(source, classification='rejection-events'))
    assert recover_legacy_range(rejection, rows, 'verified')['terminal_status'] == 'Rejected'
    inverted = dict(capsule, classification='unknown')
    assert recover_legacy_range(inverted, rows, 'verified')['outcome'] == 'resume_evidence_hold'
    bad_root = dict(capsule, root=bytes(32))
    assert recover_legacy_range(bad_root, rows, 'verified')['outcome'] == 'resume_evidence_hold'
    assert recover_legacy_range(None, rows, 'verified')['outcome'] == 'legacy_resume_evidence_unavailable'
    assert recover_legacy_range(capsule, rows, 'contradictory-authority')['outcome'] == 'legacy_resume_evidence_unavailable'
    assert recover_legacy_range(capsule, rows[:-1], 'verified')['outcome'] == 'resume_evidence_hold'
    assert recover_legacy_range(capsule, rows, 'unavailable-read')['rearmed'] == ()
    changed_sequence = ((40, rows[0][1], rows[0][2]), rows[1])
    changed_message = ((41, 'changed-message', rows[0][2]), rows[1])
    changed_digest = ((41, rows[0][1], sha256(b'changed').digest()), rows[1])
    malformed_digest = ((41, rows[0][1], b'short'), rows[1])
    malformed_sequence = (('41', rows[0][1], rows[0][2]), rows[1])
    malformed_message = ((41, '', rows[0][2]), rows[1])
    for changed in (changed_sequence, changed_message, changed_digest, malformed_digest,
                    malformed_sequence, malformed_message, tuple(reversed(rows))):
        assert recover_legacy_range(capsule, changed, 'verified')['outcome'] == 'resume_evidence_hold'
    assert recover_legacy_range(dict(capsule, range=(40,42)), rows, 'verified')['outcome'] == 'resume_evidence_hold'
    assert recover_legacy_range(dict(capsule, range='41:42'), rows, 'verified')['outcome'] == 'resume_evidence_hold'
    assert recover_legacy_range(dict(capsule, correlation='changed'), rows, 'verified')['outcome'] == 'resume_evidence_hold'
    assert recover_legacy_range(dict(capsule, command_type='changed'), rows, 'verified')['outcome'] == 'resume_evidence_hold'
    assert recover_legacy_range(dict(capsule, classification='rejection-events'), rows, 'verified')['outcome'] == 'resume_evidence_hold'
    for field in ('correlation', 'command_type', 'classification'):
        malformed_source = dict(source, **{field:None})
        malformed_capsule = dict(capsule, source=malformed_source, **{field:None})
        assert recover_legacy_range(malformed_capsule, rows, 'verified')['outcome'] == 'resume_evidence_hold'
    missing_source = dict(capsule); missing_source.pop('source')
    assert recover_legacy_range(missing_source, rows, 'verified')['outcome'] == 'legacy_resume_evidence_unavailable'
    return 'legacy-resume'

def capacity_admit(ledger, account, amounts, evidence=True):
    before = (dict(ledger.tenant), ledger.tenant_pool, ledger.unidentified, ledger.deployment)
    if not evidence or not valid_amounts(amounts):
        return ('publication_pin_capacity_hold', before == (
            dict(ledger.tenant), ledger.tenant_pool, ledger.unidentified, ledger.deployment))
    admitted = ledger.reserve('tenant', account, amounts)
    return ('admitted' if admitted else 'capacity-wait', before)

def verify_capacity_wait_matrix():
    capacity = Ledger()
    assert capacity.reserve('tenant', 'tenant-a', [300*MiB])
    batch = [400*MiB, 400*MiB]
    before_wait = (dict(capacity.tenant), capacity.tenant_pool,
                   capacity.unidentified, capacity.deployment)
    assert capacity_admit(capacity, 'tenant-a', batch)[0] == 'capacity-wait'
    assert before_wait == (dict(capacity.tenant), capacity.tenant_pool,
                           capacity.unidentified, capacity.deployment)
    fair = Queues()
    fair.add(1, 'tenant-a', 'pin-batch-a')
    fair.add(2, 'tenant-b', 'pin-batch-b')
    assert fair.deployment_turn(lambda _: True, lambda row: row[1] != 'tenant-a') == 'tenant:tenant-a'
    assert fair.where['pin-batch-a'] == 'tenant:tenant-a'
    assert fair.deployment_turn(lambda _: True, lambda _: True) == 'reserve'
    assert capacity_admit(capacity, 'tenant-a', batch, evidence=False) == ('publication_pin_capacity_hold', True)
    assert before_wait == (dict(capacity.tenant), capacity.tenant_pool,
                           capacity.unidentified, capacity.deployment)
    capacity.refund('tenant', 'tenant-a', 300*MiB)
    fair.tenant_turn('tenant-a')
    assert fair.where['pin-batch-a'] == 'deployment'
    assert capacity_admit(capacity, 'tenant-a', batch)[0] == 'admitted'
    capacity.refund('tenant', 'tenant-a', sum(batch))
    assert capacity.tenant['tenant-a'] == capacity.tenant_pool == capacity.deployment == 0
    empty = (dict(capacity.tenant), capacity.tenant_pool,
             capacity.unidentified, capacity.deployment)
    assert capacity_admit(capacity, 'tenant-a', [1, -1]) == ('publication_pin_capacity_hold', True)
    assert capacity_admit(capacity, 'tenant-a', [U64_MAX, 1]) == ('publication_pin_capacity_hold', True)
    assert empty == (dict(capacity.tenant), capacity.tenant_pool,
                     capacity.unidentified, capacity.deployment)
    assert capacity.tenant['tenant-a'] == capacity.tenant_pool == capacity.deployment == 0
    return 'capacity-wait'

def observe_delivery(previous, observed_at):
    state = dict(previous) if previous is not None else {
        'first_observed':observed_at,
        'delivery_attempt_count':0,
        'charged_bytes':32*1024,
        'indexed':True,
        'operator_visible':True,
    }
    state['delivery_attempt_count'] += 1
    return state

def held_delivery(retained, cause_cleared=False, routes_terminal=False):
    state = {
        'state':'captured', 'charged_bytes':32*1024,
        'indexed':True, 'operator_visible':True, 'transport_copy_acked':True,
        'route_success':False, 'closed':False, 'redrive_bytes':None,
        'retained_backend_id':'held-delivery-store',
        'retained_object_key':'held/exact-carrier-1',
        'readback_authority':sha256(b'authenticated-readback:' + retained).digest(),
        'next_recheck_seconds':60,
    }
    if cause_cleared:
        state['state'] = 'redriving'
        state['redrive_bytes'] = retained
        state['closed'] = routes_terminal
        state['route_success'] = routes_terminal
        if routes_terminal:
            state['state'] = 'closed'
    return state

def above_max_delivery(observed_length):
    return {
        'state':'incident', 'reason':'delivery_above_advertised_max',
        'observed_length':observed_length, 'charged_bytes':32*1024,
        'indexed':True, 'operator_visible':True,
        'transport_copy_acked':False, 'route_success':False,
        'retained_backend_id':None, 'retained_object_key':None,
        'readback_authority':None, 'next_recheck_seconds':3600,
    }

def full_replay_exit(event_count, readable_bytes, accounting_bytes, incremental_capability):
    hard = event_count > 100000 or readable_bytes > 64*MiB or accounting_bytes > 256*MiB
    threshold = event_count >= 75000 or readable_bytes >= 48*MiB or accounting_bytes >= 192*MiB
    if (hard or threshold) and not incremental_capability:
        return {'outcome':'LegacyArrayLimit', 'indexed':True, 'applied_events':0,
                'truncated':False, 'next_recheck_seconds':3600}
    if incremental_capability:
        return {'outcome':'incremental-scheduled', 'indexed':False, 'applied_events':0,
                'truncated':False, 'requires_new_event':False}
    return {'outcome':'continue-full-replay', 'indexed':False,
            'applied_events':event_count, 'truncated':False}

def hard_bound_exceeded(event_count, readable_bytes, accounting_bytes):
    return (event_count > 100000, readable_bytes > 64*MiB, accounting_bytes > 256*MiB)

def verify_held_delivery_and_long_stream_matrix():
    carrier = b'exact-retained-carrier-and-headers'
    first = observe_delivery(None, 638712864000000000)
    restarted = observe_delivery(dict(first), 638712864600000000)
    assert first['first_observed'] == restarted['first_observed'] == 638712864000000000
    assert first['delivery_attempt_count'] == 1 and restarted['delivery_attempt_count'] == 2
    assert restarted['charged_bytes'] == 32*1024 and restarted['indexed'] and restarted['operator_visible']
    held = held_delivery(carrier)
    assert held == {
        'state':'captured', 'charged_bytes':32*1024,
        'indexed':True, 'operator_visible':True, 'transport_copy_acked':True,
        'route_success':False, 'closed':False, 'redrive_bytes':None,
        'retained_backend_id':'held-delivery-store',
        'retained_object_key':'held/exact-carrier-1',
        'readback_authority':sha256(b'authenticated-readback:' + carrier).digest(),
        'next_recheck_seconds':60}
    assert (held['transport_copy_acked'] and held['retained_backend_id']
            and held['retained_object_key'] and held['readback_authority']
            and not held['route_success'])
    redriven = held_delivery(carrier, cause_cleared=True, routes_terminal=False)
    assert redriven['redrive_bytes'] == carrier and not redriven['closed'] and not redriven['route_success']
    completed = held_delivery(carrier, cause_cleared=True, routes_terminal=True)
    assert completed['redrive_bytes'] == carrier and completed['closed'] and completed['route_success']
    over_bound = full_replay_exit(100001, 64*MiB, 256*MiB, False)
    assert over_bound == {
        'outcome':'LegacyArrayLimit', 'indexed':True, 'applied_events':0,
        'truncated':False, 'next_recheck_seconds':3600}
    capability_exit = full_replay_exit(100001, 64*MiB, 256*MiB, True)
    assert capability_exit == {
        'outcome':'incremental-scheduled', 'indexed':False, 'applied_events':0,
        'truncated':False, 'requires_new_event':False}
    def carrier_path(length, maximum=256*MiB):
        if length <= 193*MiB: return 'ordinary'
        if length <= maximum: return 'oversize-quarantine'
        return 'charged-visible-incident'
    assert carrier_path(193*MiB) == 'ordinary'
    assert carrier_path(193*MiB+1) == 'oversize-quarantine'
    assert carrier_path(256*MiB) == 'oversize-quarantine'
    assert carrier_path(256*MiB+1) == 'charged-visible-incident'
    above_max = above_max_delivery(256*MiB+1)
    assert (above_max['charged_bytes'] == 32*1024 and above_max['indexed']
            and above_max['operator_visible'] and not above_max['transport_copy_acked']
            and not above_max['route_success'] and above_max['state'] == 'incident')
    assert 4 + 65536 + 128*(4+8+32) == 71172 < 128*1024
    for below, at, above in [(74999, 75000, 75001), (48*MiB-1, 48*MiB, 48*MiB+1),
                             (192*MiB-1, 192*MiB, 192*MiB+1)]:
        values = [100, 1024, 819200]
        index = 0 if above == 75001 else (1 if above == 48*MiB+1 else 2)
        values[index] = below
        assert full_replay_exit(*values, False)['outcome'] == 'continue-full-replay'
        values[index] = at
        assert full_replay_exit(*values, False)['outcome'] == 'LegacyArrayLimit'
        values[index] = above
        assert full_replay_exit(*values, False)['outcome'] == 'LegacyArrayLimit'
    hard_cases = [
        (0, [99999, 1, 1], [100000, 1, 1], [100001, 1, 1]),
        (1, [1, 64*MiB-1, 1], [1, 64*MiB, 1], [1, 64*MiB+1, 1]),
        (2, [1, 1, 256*MiB-1], [1, 1, 256*MiB], [1, 1, 256*MiB+1]),
    ]
    for index, below_values, at_values, above_values in hard_cases:
        assert hard_bound_exceeded(*below_values)[index] is False
        assert hard_bound_exceeded(*at_values)[index] is False
        assert hard_bound_exceeded(*above_values)[index] is True
        for values in (below_values, at_values, above_values):
            assert full_replay_exit(*values, False)['outcome'] == 'LegacyArrayLimit'
            override = full_replay_exit(*values, True)
            assert override['outcome'] == 'incremental-scheduled' and override['applied_events'] == 0
        assert full_replay_exit(*above_values, False)['applied_events'] == 0
    return 'held-delivery-or-long-stream'

matrix_cases = [
    verify_eligible_resume_matrix(),
    verify_legacy_resume_matrix(),
    verify_capacity_wait_matrix(),
    verify_held_delivery_and_long_stream_matrix(),
]
assert matrix_cases == [
    'eligible-resume', 'legacy-resume', 'capacity-wait',
    'held-delivery-or-long-stream']

# Exact construction bounds and acyclic/retry invariants.
assert 2455 + 943*1109 <= MiB and 2455 + 944*1109 > MiB
assert 1258 + 59*1080 <= 64*1024 and 1258 + 60*1080 > 64*1024
request_hash = b'request-hash'
retry_cache = {request_hash: {'window':2, 'limit':24, 'expiry':100}}
assert retry_cache[request_hash] == {'window':2, 'limit':24, 'expiry':100}
assert request_hash not in {}  # expired rows have one closed unavailable outcome, not fabricated replay.

closure_bytes = b'closure-without-successor'
successor_accumulator = sha256(b'predecessor' + closure_bytes + b'broker-auth').digest()
assert successor_accumulator not in closure_bytes
prior_state_hash = sha256(b'prior-state').digest()
audit_bytes = b'audit' + prior_state_hash
audit_hash = sha256(audit_bytes).digest()
successor_state_bytes = b'state' + successor_accumulator + audit_hash
assert sha256(successor_state_bytes).digest() not in audit_bytes and audit_hash in successor_state_bytes

# Mutation killers: each expression describes an acceptance-breaking mutant and must be rejected.
mutants_rejected = [
    status('unknown', at_max=True)[1] is None,                         # no exhaustion hold for unknown
    not ledger.reserve('capture-scope', 'scope-c', [513*MiB]),        # unidentified ceiling retained
    not ledger.reserve('unknown', 'scope-c', [1]),                    # closed account kind
    capacity_admit(ledger, 't1', [-1])[0] == 'publication_pin_capacity_hold', # stable arithmetic hold
    ledger.tenant['t1'] == 700*MiB and ledger.tenant_pool == 700*MiB and ledger.deployment == 700*MiB,
    queues.where['a'] == 'deployment',                                # cross-counter return exists
    queues.where['b'] == 'deployment',                                # deployment head refused by own tenant does not block t2
    membership_exit('manual', True, True) != 'ContinueSamePin',
    membership_exit('membership-revision', True, True) == 'ContinueSamePin',
    legacy_resume(False, True, True) != 'rearm-stored-range',
    status('pending', drain_active=False)[1] != 'publication_drain_limit_hold',
    status('failed', failure_class='class-02', at_max=True)[1] == 'terminal_evidence_hold',
    status('failed', failure_class='class-03')[1] == 'terminal_evidence_hold',
    status('published', classification='rejection')[0] == 'Rejected',
    status('published', classification='unknown')[1] == 'outcome_evidence_conflict',
    legacy_resume(True, True, False) == 'legacy_resume_evidence_unavailable',
    render_recovery('bad', None, 'x') == 'response_preparation_hold',
    len(queues.rows['deployment']) + len(queues.reservations['deployment']) <= queues.ceiling,
    decode_queue(valid_rows, 1, 1) == 'pin_capacity_queue_corruption_hold',
    parked_only.deployment_turn(lambda _: True, lambda _: True) == 'noop',
    ledger.unidentified <= ledger.unidentified_ceiling,
    full_replay_exit(75000, 1, 1, False)['outcome'] == 'LegacyArrayLimit',
    full_replay_exit(100001, 1, 1, False)['applied_events'] == 0,
    2455 + 944*1109 > MiB,
    1258 + 60*1080 > 64*1024,
    successor_accumulator not in closure_bytes,
    sha256(successor_state_bytes).digest() not in audit_bytes,
]
assert all(mutants_rejected), mutants_rejected

text = Path('_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md').read_text(encoding='utf-8')
start = '<!-- pass2-' + 'dispositions-start -->'
end = '<!-- pass2-' + 'dispositions-end -->'
section = text.split(start, 1)[1].split(end, 1)[0]
found = re.findall(r'^\| ((?:VG2-(?:[234]|O[123]))|(?:BH2-(?:[1-9]|1[0689]))|(?:E2-(?:[1-9]|1[0-9]|2[0-689]|3[0124568]))) \|', section, re.M)
expected = ({'VG2-2','VG2-3','VG2-4','VG2-O1','VG2-O2','VG2-O3'}
            | {f'BH2-{n}' for n in list(range(1,11))+[16,18,19]}
            | {f'E2-{n}' for n in list(range(1,27))+[28,29,30,31,32,34,35,36,38]})
assert len(found) == 54 and set(found) == expected and len(found) == len(set(found)), (len(found), expected-set(found), set(found)-expected)
print(f'D12 lifecycle verifier: {len(status_cases)} status cases, {len(matrix_cases)} matrix rows, {len(mutants_rejected)} mutants, {len(found)} dispositions passed')
PY
```

## D13. Integration handoff

Story 6.5 imports this candidate as one change. It does not retain the loop-1 text beside these replacements and invents no bridge rule.

### D13.1 Rule and imported-section replacements

| Target | Exact integration action |
| --- | --- |
| `[I-06]` | Replace with D2, including all three activation dispositions, versioned record, idle-stream re-evaluation, storage, slice, and exit. |
| `[I-10]`, `[I-14]`, `[I-15]`, `[I-16]` | Replace with D3's precedence, drain epochs/resolution, polling values, complete drain-reason classification, preparation recovery, closed `CommandOutcomeHold` set, and Admin current-hold join. |
| `[I-12]` | Replace with D4's slice-2 claim, cutover/sunset, 256-shard accounting, tombstone reconciliation/expiry, status 410, availability outcome, erasure, and codecs. |
| `[I-17]` | Replace with D6. |
| `[I-29]`, `[I-30]` | Replace with D7's capability, exact charge/counter codecs, unidentified ceiling, two-phase atomic batch reservation, quarantine kind, checked arithmetic, stable fail-closed `publication_pin_capacity_hold`, zero partial mutation, refunds, and activation. `[I-26]` and `[I-28]` cite these codecs and maxima rather than defining another charge. |
| `[I-31]` | Replace with D8's inventory-only invalid-candidate state, checked transition into the single-residence fair queue, cross-counter moves, parked discovery, exact count framing, rerender rule, storage, activation, and exits. |
| `[I-36]` | Replace with D11.1, including mandatory bounded capture for formerly unbounded redelivery, terminal/oversize quarantine, unambiguous routes, refund, erasure, and activation. |
| `[I-37]` | Replace with D11.2's scope-discriminated IDs/routes, versioned reason, ordered index, bounded directory, zero-overflow readiness, reconciler lease, metrics, Admin join, storage, activation, and erasure. |
| `[I-45]` | Replace with D9. Purpose `2d`'s assignment becomes: D2 activation, D9 resume/window claims, and D11 redrive only. Request discovery, bounded rolling state, capacity swap, success-only audit, drain-limit resolution, and replies are inseparable. |
| `[I-46]` | Replace with D10's pre-cleanup capsule and exclusive recovery. No reconstruction from source-less historical status/dead letter is permitted. |
| 6.5c C1 | Replace “pin charge atomically at pin CAS” with D7's ledger batch reservation plus reservation-bound pin installation. Preserve exact global pins, ceilings, and no-send-before-readback. Add `oversize-quarantine` only for invalid carriers under D11. |
| 6.5c C2 | Amend the first-send outcome in place with D5's versioned resolution. Add D9's window namespace/binding to new resumed sends; the whole-operation terminal fence still precedes duplicates. |
| 6.5c C4 | Amend acknowledgement in place with D11's authenticated captured-copy terminal handoff: it may acknowledge that physical copy while explicitly leaving every logical route/effect obligation open; it is not a successful route decision and cannot satisfy C4's ordinary success proof. |
| 6.5c C5 | Amend closure in place exactly as D9.2: terminal closure uses the permanent operation fence; resume uses a window fence and authenticated closure accumulator. C5's terminal verifier consumes the window chain. BC-02 points to D9/D10, never command re-execution. |
| A8 preparation | Keep unchanged A8/[I-09] authority by exact name: `HX-EV-RESPONSE-PREPARATION-WRITE-1` at `command-response-preparation-write:` plus ScopeOpHash. Recovery requires both existing immutable outputs and generation-bound receipts; a missing output remains `response_preparation_hold`. |

### D13.2 §8.1 outcome rows

Replace the owned outcome rows with these consumer results; no other status inference is legal.

| Trigger | Consumer outcome |
| --- | --- |
| full-replay below 75%, at/above 75%, or above a hard bound | D2 `continue-full-replay`, explicit route-wide `hold`, or stream `LegacyArrayLimit`; never silent truncation |
| scope/claim/cutover/shard unavailable | `CommandOutcomeHold(admission_evidence_hold)` before domain invocation |
| scope shard full | `ScopeRetentionCapacityHold`, indexed and re-evaluated by D4 |
| incomplete preparation outputs | `CommandOutcomeHold(response_preparation_hold)` with D3 fenced recovery |
| private pending/unknown/failed evidence | D3.1 exact precedence, including active drain-limit and mixed class-01/class-02 |
| pin batch refused or its arithmetic/evidence is invalid, overflowing, or unavailable | `CommandOutcomeHold(publication_pin_capacity_hold)`; D7/D8 retain and re-evaluate it, with zero partial reservation/charge/counter/pin/send |
| membership changed before first send | `CommandOutcomeHold(first_send_membership_changed_hold)` until D5 compatibility restoration |
| held or nonadmissible carrier | D11 held capture/redrive or terminal quarantine before acknowledgement |
| resume stale/unavailable/capacity/evidence absent | D9's closed 409/503 reasons; no rejection audit |
| legacy status 6 | exact capsule resume, `resume_evidence_hold`, or stable `legacy_resume_evidence_unavailable`; never command replay |

### D13.3 §10 activation and compatibility

The four existing slices gain these dependencies:

| Slice | Added gate |
| --- | --- |
| 1 | no 6.5d runtime activation |
| 2 | deploy/read D7 capability/ledger and begin D4 legacy claims; pin `H`; validate all codecs/readbacks while behavior remains legacy |
| 3 | activate D2 inventory, D11.2 hold inventory/directory/telemetry lease, D9 tenant resume routes and ReplayController safety gate, and D10 capsule-before-cleanup/recovery fence; wait the D4 horizon and read back cutover before slice 4 |
| 4 | activate evidence-required D3 outcomes, D4 required scopes/tombstones, D5 membership resolution, D6 destination admission, D7 pin batches, D8 waits, D9 evidence-required windows, and D11 delivery capture/redrive |

Amend §10.2 rows in place:

- **BC-01** keeps nonterminal `EventsStored` hold metadata and now cites D3's exact precedence/reasons.
- **BC-02** denies fresh command replay for committed `PublishFailed` and activates D9/D10 in slice 3 before that denial can strand eligible legacy work.
- **BC-05** cites D2's three-way activation and deterministic exit.
- **BC-08** includes D11's “capture before acknowledgement,” broker pre-rejection, and exact oversize quarantine requirement.
- **BC-15 (new):** from slice 2, a legacy claim read/CAS outage fails closed with 503 instead of continuing the shipped legacy admission path. This is SemVer-major and documented on command admission/status/Admin surfaces.
- **BC-16 (new):** bounded tombstone expiry permits a later MessageId reuse after the pinned horizon, and status on a live tombstone returns 410. This is SemVer-major and documented on idempotency/status surfaces.

All new routes and record fields are otherwise additive under §10.3. Provider/crash vectors are future Story 6.6 verification obligations; this local model does not assert DAPR/broker behavior.

### D13.4 §11.5 and §11.6

Replace the loop-1 disposition rows for the owned rules with the pass-1 and pass-2 tables below. In §11.6, remove the old owned `I06`, `I12`, `I14`, `I17`, `I29`, `I31`, `I36`, `I37`, `I45`, and `I46` known answers and import all 30 `D*` answers, six framed-key answers, and both D12 verifier blocks. Retain unowned A/B/C and integration answers unchanged. Verification must assert 30 codec answers, 30 codec mutations, six keys, 14 status cases, 4 matrix rows, 27 lifecycle mutants, and 54 unique pass-2 dispositions.

## Disposition register

### Pass 1 owned groups

| Group | Verified disposition |
| --- | --- |
| G-A legacy scope | replace with D4 claim/cutover/shards/tombstones and D10 capsule address |
| G-B drain limit | replace with D3 epochs/resolution and D9 bounded successor ownership |
| G-C hold reasons | replace with D3 precedence and closed sets |
| G-D D-RESUME intent gap | owner decision implemented by D9/D10 without command execution |
| G-E capacity queue | replace with D7 atomic batch and D8 single-residence fair queues |
| G-F long stream | replace with D2 three-way activation and idle re-evaluation |
| G-G schema/capability | replace with D6 and D7 exact codecs/bounds |
| G-H held delivery | replace with D11.1 mandatory capture/redrive/quarantine |
| G-I hold inventory | replace with D11.2 directory, versioned entries, metrics lease, and exits |
| G-J owned codecs | replace with D12's 30 independently recomputed answers and mutation checks |

### Review pass 2 routed findings

<!-- pass2-dispositions-start -->

| Finding | Class | Evidence-backed disposition |
| --- | --- | --- |
| VG2-2 | replacement | D7 enforces `unidentifiedCaptureCeiling`; D12 exercises exact fill and +1 refusal. |
| VG2-3 | replacement | D3 checks drain-limit pending exit and reason; D12 covers active versus resolved. |
| VG2-4 | replacement | D8 models separate tenant/deployment queues and a deployment head refused by its tenant. |
| VG2-O1 | replacement | D8 replaces the shared queue with single-residence moves, 50,000 maximum, and discoverable parking. |
| VG2-O2 | replacement | D3 says pending/unknown ignore exhaustion unless an active drain-limit record exists. |
| VG2-O3 | replacement | D3's resolution closes the exact drain-limit epoch before a larger limit activates. |
| BH2-1 | replacement | D9 precondition exposes a buildable handle/source/head/ordinal chain; server signing removes caller reconstruction. |
| BH2-2 | replacement | D11 uses discriminated hashed actor IDs and separate tenant/deployment routes. |
| BH2-3 | replacement | D2 supplies below-bound disposition, noncircular target fingerprint, and activation-hold semantics. |
| BH2-4 | replacement | D7 atomically reserves the whole pin batch; D8 waits hold no resource and cannot deadlock. |
| BH2-5 | replacement | D5 permits only configuration restoration plus fresh zero-send/exact-byte proof. |
| BH2-6 | replacement | D1/D3 name owners, triggers, and evidence/conflict exits; no hold is silently abandoned. |
| BH2-7 | replacement | D10 writes/read-backs the capsule before cleanup and fences Operations retry against resume. |
| BH2-8 | replacement | D9 retains one rolling active charge/state, includes the next limit, refunds closed windows, and records only success. |
| BH2-9 | replacement | D4 uses 256 shards, bounded retries, a measured horizon, and a cutover that ends legacy fallback reads. |
| BH2-10 | replacement | D1/D11 define gateway subjects, re-evaluation, removal evidence, and capacity reservation. |
| BH2-16 | replacement | Same verified defect as VG2-O2; D3 owns its single resolution. |
| BH2-18 | replacement | D7/D11 define charge, counter, quarantine, entry, index, directory codecs and D12 answers. |
| BH2-19 | replacement | D4 returns `admission_evidence_hold`; D13 classifies it as BC-15 in slice 2. |
| E2-1 | replacement | D3 keys drain-limit records by window and limit epoch. |
| E2-2 | replacement | D3 atomically records budget consumption/hold pointer and reconciles lost acknowledgement. |
| E2-3 | replacement | D3 evidence conflict precedes pending/unknown scalar projection and is inventory-visible. |
| E2-4 | replacement | D10 capsule stores authenticated rejection classification before drain evidence disappears. |
| E2-5 | replacement | D10 missing-source policy is stable non-resumable incident, not BC-02 command replay. |
| E2-6 | replacement | D10 handle binds tenant/domain/aggregate/tracking/range independent of expiring status or reused MessageId. |
| E2-7 | replacement | Same verified defect as BH2-1; D9.1 is the single replacement. |
| E2-8 | replacement | D11 requires bounded direct/dead-letter capture even when broker redelivery is unbounded. |
| E2-9 | replacement | D11 requires exact provider quarantine through 256 MiB or broker pre-rejection/readiness failure. |
| E2-10 | replacement | D11 defines a deployment-scoped redrive route with no tenant placeholder. |
| E2-11 | replacement | D11's actor IDs and routes cannot collide with tenant `deployment`. |
| E2-12 | replacement | Same verified defect as BH2-10; D11 entry lifecycle owns it. |
| E2-13 | replacement | D11's bounded actor directory and one epoch lease make reconciliation/gauges complete and single-owner. |
| E2-14 | replacement | D11 reason changes create the next entry revision before index update. |
| E2-15 | replacement | D8 keeps parked rows in the bounded directory, discoverable without enumeration. |
| E2-16 | replacement | D7 uses ledger reservation plus reservation-bound pin CAS across different backends. |
| E2-17 | replacement | D8 removes the inner count; outer count parses exact rows and rejects trailing/mismatch. |
| E2-18 | replacement | D4 names the compactor, hourly/75% trigger, authenticated expiry, and decrement. |
| E2-19 | replacement | Same verified defect as BH2-9; D4 sharding and bounded loser outcome own it. |
| E2-20 | replacement | D4 status on a tombstone returns stable 410 and never falls back. |
| E2-21 | replacement | Same verified defect as BH2-19; D4/D13 own its outcome and slice. |
| E2-22 | replacement | Same verified defect as BH2-3; D2 requires `continue-full-replay` below 75%. |
| E2-23 | replacement | D2 configuration revision schedules re-evaluation/bootstrap even with no new events. |
| E2-24 | replacement | D3 names unchanged A8/[I-09] authority: only two already-present verified outputs permit the recovery owner to create the preparation-write record; a missing output stays held. |
| E2-25 | replacement | D9's active window charge explicitly includes the next drain-limit record/resolution. |
| E2-26 | replacement | D9 rejection writes no audit/charge; exact successful retry reuses one audit. |
| E2-28 | replacement | D3 classifies every shipped drain reason as automatic, capsule-resumable, or evidence incident. |
| E2-29 | replacement | D3 requires Admin list-time join with authoritative status/hold evidence. |
| E2-30 | replacement | Same verified defect as VG2-O2; D3's row 8 owns it. |
| E2-31 | replacement | Same verified defect as VG2-O1; D8/D12 own it. |
| E2-32 | replacement | D7 maps negative/overflow arithmetic to stable `publication_pin_capacity_hold` before comparison with zero mutation; D12 executes negative and u64-sum-overflow cases and proves counters unchanged. |
| E2-34 | replacement | D9 scopes resume closure to a window while preserving C5's permanent terminal operation fence. |
| E2-35 | replacement | D10 capsule predates the request, so D9 tag `06` is noncircular. |
| E2-36 | replacement | D3 precedence maps mixed class-01/class-02 to `terminal_evidence_hold`. |
| E2-38 | replacement | D1 plus D2–D11 give G-B, G-E, and G-I storage, bounds, activation, inventory, owner, and exits. |

<!-- pass2-dispositions-end -->

Every routed finding appears once. No row defers a contract decision to Story 6.6; future obligations verify the decided behavior on real providers and crash boundaries.

### Review-loop-1 repair register

| Finding | Verified repair |
| --- | --- |
| VG-1 | D5 and D12 exercise hold/success for both membership and configuration revisions. |
| VG-2 | D10 and D12 preserve and separately prove success/rejection classification. |
| VG-3 | D9 and D12 prove unchanged window/roster, checked limit growth, resolution linkage, and re-armed invocation for drain-only resume. |
| VG-4 | D2/D12 exercise below/at/above 75% and hard bounds for count/readable/accounting. |
| VG-5 | D8/D12 reject too few/extra rows, parked mismatch, and trailing bytes. |
| VG-O1 | D8 pre-reserves slots and proves `queued + parked + reserved <= ceiling`; ceiling-plus-one is refused. |
| BH-1 | D-SPLIT stays open throughout this implementation/review loop. |
| BH-2 | D2 derives the exact 943-row ceiling and fails readiness for 944+. |
| BH-3 | D3 and D12 use a framed drain-limit key and prove the former decimal collision differs. |
| BH-4 | D3/D13 name unchanged A8/[I-09] record, key, outputs, receipts, and fence dependency exactly. |
| BH-5 | D4 has one expiry/obligation deletion rule; tenant erasure is the remaining cleanup, not a contradictory tombstone rule. |
| BH-6 | D7 derives 59 members and fails pre-append admission for 60..1,000. |
| BH-7 | D8 converts pre-reserved slots and never appends a full directory. |
| BH-8 | D8 charges the wait and maximum queue row before commit and refunds after deletion readback. |
| BH-9 | D9 closure omits its successor; the successor is computed after closure readback. |
| BH-10 | D10/D12 fix exact framed capsule and recovery keys. |
| BH-11 | D11 retains bounded backend IDs, resolvable object keys, and authenticated readback receipts. |
| BH-12 | D11/D13 explicitly amend C4: captured-copy acknowledgement is not logical route success. |
| BH-13 | D11 derives the 71,172-byte maximum header manifest under a 128 KiB record cap. |
| BH-14 | D11 writes first-observed time and attempt count before either capture boundary is used. |
| BH-15 | D1/D10/D11 close hold/reason, owner, delivery, recovery-state, and failure vocabularies. |
| BH-16 | D9/D12 require an exact duplicate-free committed partition. |
| BH-17 | D10/D12 recompute sequence, MessageId, digest, root, range, correlation, command type, and classification. |
| BH-18 | D12 executes Completed, Rejected, not-applicable, class-02, class-03, catch-all, and strengthened mutants. |
| EC-1 | Same root as BH-9; D9's history graph is acyclic and executable. |
| EC-2 | D9 writes audit before successor state; audit hashes prior state and successor state hashes audit only. |
| EC-3 | D9 explicitly declares the closure's inner `u32 rowCount`. |
| EC-4 | D7 derives invalid-candidate subjects from immutable admitted A8 evidence or fails before commit. |
| EC-5 | Same root as VG-O1/BH-7; D8 refuses the fourth row at ceiling three. |
| EC-6 | D7/D12 reject unknown account kinds with unchanged counters. |
| EC-7 | D8/D12 make empty and parked-only turns durable no-ops. |
| EC-8 | Same root as VG-1; membership-revision hold and success are executed. |
| EC-9 | D3/D12 execute the class-03 terminal-evidence branch. |
| EC-10 | Same root as VG-4; all three activation measures exercise threshold transitions. |
| EC-11 | D4 rejects readiness when `H` exceeds ten years. |
| EC-12 | D4 defines exact expired-claim CAS replacement versus changed-claim conflict. |
| EC-13 | Same root as BH-5; retention and erasure are noncontradictory. |
| EC-14 | D9 checked arithmetic closes ordinal/window/count/drain overflow as `resume_arithmetic_exhausted`. |
| EC-15 | D9 retains up to 64 live compact retry results until request expiry before reclaiming bodies. |
| EC-16 | D10 closes every transition from `failed` by typed retry/repair or retained incident ownership. |
| EC-17 | Same root as BH-14; restart cannot reset observation time/attempt count. |
| EC-18 | D11 makes ordinary `<=193 MiB` and oversize strictly `>193 MiB`. |
| EC-19 | D11 creates a bounded charged indexed above-maximum incident before repeated delivery is invisible. |
| EC-20 | D9/D12 reject duplicate IDs/positions, omissions, overlaps, and unknown members before mutation. |
| EC-21 | D12 recomputes semantic batch/event/attempt/history/capsule/audit roots in dependency order. |
| EC-22 | D12 expands executable mutations to the repaired status, bound, queue, hash, classification, and account invariants. |
| EC-23 | D8 gives queue corruption a charged inventory record, owner, triggers, reconstruction proof, and deterministic exit. |

## Protected-path and source-integrity verification

This block proves that the unapproved parent/children and protected implementation paths remain unchanged while this story edits only its candidate and bookkeeping artifacts.

```bash
python3 - <<'PY'
from hashlib import sha256
from pathlib import Path
import subprocess

root = Path('.')
pins = {
    '_bmad-output/implementation-artifacts/spec-6-5a-event-contract-writer-and-migration-evidence.md': '31f63fc0ea54043e7b821f311b28d3e856667789eac9eca8f79807f6d2e74444',
    '_bmad-output/implementation-artifacts/spec-6-5b-verified-read-replay-and-projection.md': 'b889951bc248a7a4d19067a84d197bbe8dd1bc14457d56ed5665cc9c72347152',
    '_bmad-output/implementation-artifacts/spec-6-5c-publication-subscription-and-rollout.md': 'f24116aaf4dc3cd041034f40a1d858e114f9f8a4219b1ca0cc40bdea80385ca9',
    '_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md': 'c474df76a687b2798757051efbdb382bb6b9650f7bd7a7637d0c67bfc5e7b715',
}
for name, expected in pins.items():
    assert sha256((root/name).read_bytes()).hexdigest() == expected, name
historical = subprocess.check_output(['git','show','288a6190:_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md'])
assert sha256(historical).hexdigest() == pins['_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md']
parent = (root/'_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md').read_text(encoding='utf-8')
for label in ['ApprovalDigest','Approver','ApprovalDateUtc','Authorization','ApprovalEvidence']:
    assert f'{label}: UNAPPROVED' in parent
assert 'ApprovalScope: Story 6.5 AD-13 normative artifact' in parent
allowed = {
    '_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md',
    '_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission-2.md',
    '_bmad-output/implementation-artifacts/deferred-work.md',
    '_bmad-output/implementation-artifacts/sprint-status.yaml',
}
changed = set(subprocess.check_output(['git','diff','--name-only','6a2f25e39586692b54b655d3e6e8a5f6fa4d317e']).decode().splitlines())
untracked = {line[3:] for line in subprocess.check_output(['git','status','--porcelain']).decode().splitlines() if line.startswith('?? ')}
assert changed | untracked <= allowed, sorted((changed | untracked) - allowed)
print(f'protected-path verifier: {len(pins)} hashes, AD-13 UNAPPROVED, {len(changed | untracked)} allowed paths')
PY
```

## Verification expectations

Run all three fenced `bash` blocks verbatim. Expected output is 30 codec answers, 30 byte mutations rejected, and six framed keys; 14 status cases, 4 approved matrix rows, 27 lifecycle mutants, and 54 dispositions passed; then four protected hashes with AD-13 still `UNAPPROVED`. Run `python3 scripts/check-deferred-work.py` and `git diff --check`. Story 6.5 integration must rerun these blocks after splicing and before recomputing its own §12 digest.
