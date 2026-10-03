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

After concurrent commits and rebase captured earlier candidate work, the user authorized `01498ac721db7c44f18fcf9591ffbbf30ba245e2` as the resumed build's verification/review baseline. The execution record retains the initial baseline and review history; the protected source hashes below remain pinned.

The draft's checked big-endian codecs are imported: `U` and `B` are `u32 length || bytes`, `B32` is exactly 32 bytes, `N` is a checked non-negative u64, `I` is signed i32, `Q` is signed i64 UTC ticks, and `O(X)` is `00` for absent or `01 || X` for present. Every record below is `ASCII domain separator including NUL || one-byte codec || u16 field count || fields in ascending tag order`. Length/count/arithmetic validation precedes allocation. A decoder rejects missing, duplicate, extra, reordered, negative, overflowing, or trailing data.

The current source proves only the shipped starting point. `AggregateActor` deletes `UnpublishedEventsRecord` after drain exhaustion, `DeadLetterMessage` omits `IsRejection`, `ReplayController` is actually at `src/Hexalith.EventStore/Controllers/ReplayController.cs` and resubmits `PublishFailed` commands, Operations capture defaults to 1 MiB with a 10 MiB ceiling, and its current actor index is key-only. No proposed record or provider guarantee is claimed to exist today.

## D1. Shared lifecycle invariants

Every durable hold or wait has exactly one stable subject, one owner, bounded charged storage, an inventory entry, a re-evaluation trigger, and one of the exits below. A hold record is evidence, never permission to execute a command, create a new event identity, skip a route, acknowledge an unresolved delivery, or overwrite history.

1. **Create and index.** D11.2 inventory/directory/onboarding reservations and gateway re-evaluation are a slice-2 prerequisite, read back before any legacy admission/capacity hold can be exposed. The owning transition durably creates or read-backs its state before exposing the hold. An operation that can commit before discovering a hold reserves its hold-inventory and wait-directory slots before commit. A delivery creates its capture and inventory entry before acknowledging the transport copy. If the required slot cannot be reserved, admission/readiness stops before the durable work that could be stranded.
2. **Re-evaluate.** The named owner re-evaluates on its event trigger and at least once per hour. Manual action may request an immediate re-evaluation but never supplies missing authority or changes a compatibility result.
3. **Resolve.** Resolution evidence is written/read back before the inventory entry is removed. A stale request conflicts. Unavailable evidence leaves the prior state authoritative. Erasure removes the state only after the tenant or deployment scope has no retained obligation.
4. **No abandonment by success.** Erasure/offboarding is not publication, route completion, or command success. It may remove data only under the existing tenant-erasure contract after all data and authority for that tenant are removed together.

| Hold or wait | Stable subject | Owner and event trigger | Deterministic exit |
| --- | --- | --- | --- |
| `LegacyArrayLimit` | domain + route + stream, or route-wide activation subject | projection; registry/configuration revision and hourly | verified `5a incremental` capability under the active fingerprint, then a scheduled first incremental dispatch even if no new event arrives |
| `ActivationInventoryCapacityHold` | domain + target RegistryFingerprint | projection activation owner; catalog revision and hourly | a complete inventory of at most 943 routes fits and reads back; no segmented or partial activation is served |
| `admission_evidence_hold` | tenant + execution MessageId | gateway; state-store recovery/cutover revision and 30-second retry | required scope/legacy claim and shard counter read back, or the request is rejected as a proved conflict |
| `response_preparation_hold` | ScopeOpHash | coordinator; owner-fence transfer and 30-second retry | unchanged A8/[I-09] recovery proceeds only when both immutable outputs and generation-bound receipts already verify, then writes/reads the preparation-write record; a missing immutable output is a non-resumable indexed incident, removed only by whole-tenant erasure or a separately approved migration |
| `outcome_evidence_hold` | ScopeOpHash + head revision | coordinator; evidence-store recovery and 30-second retry | every required immutable source reads back and the existing or next outcome verifies |
| `outcome_evidence_conflict` | ScopeOpHash + observation identity | coordinator; authoritative provider revision and hourly | a later authoritative observation proves the retained row unchanged; an irreparable contradiction remains an operator-visible incident and cannot be abandoned as success |
| `terminal_evidence_hold` | ScopeOpHash + failed head | coordinator; C5 closure progress and hourly | complete C5 terminal closure reads back, or later authoritative publication evidence makes the head nonterminal/published |
| `PublicationRetryExhaustedHold` | ScopeOpHash + active window + member set | coordinator; authenticated resume or terminal proof | D9 resume opens exactly one successor window, or C5 terminal closure completes |
| `PublicationDrainLimitHold` | ScopeOpHash + active window + drain-limit epoch | coordinator; authenticated resume/head advance and hourly | a D9 drain-limit resolution closes the exact record before a larger limit becomes active, or a later verified head/terminal pointer supersedes it |
| `PublicationResumePreparationHold` | tenant + execution + stable request identity | coordinator; exact retry, restart and hourly | D9.4 completes the original prepared success, or authenticated absence and full artifact-deletion readback permit atomic bounded tombstone compaction and free the preparation slot |
| `PublicationPinCapacityHold` | exact D7 `capacity-subject:` key: framed ScopeOpHash + immutable admitted A8 outbox/member-plan root | quota coordinator; refund/capability/renderer-evidence revision and 60-second re-evaluation | checked arithmetic and evidence produce a valid candidate and atomic batch reservation succeeds; no member holds a partial reservation while held or waiting |
| `PinCapacityQueueCorruptionHold` | deployment identity + counter ID + queue generation | quota coordinator; authenticated repair/migration completion and 60-second re-evaluation | the exact predecessor and all wait rows are reconstructed, read back, and atomically installed without dropping or duplicating a ticket |
| `ResumeAttemptCollectionHold` | tenant + execution + window | coordinator; definitive-result/closure readback and hourly | verified complete bounded attempt set permits closure/resume; otherwise retained incident exits only through operation erasure; reason `resume_evidence_hold` |
| `QuotaGenerationIncident` | deployment + counter/charge key + maximum generation | quota coordinator; approved quota migration and hourly | checked next generation after an approved migration, or whole-scope erasure; reason `quota_generation_exhausted` |
| `FirstSendMembershipChangedHold` | ScopeOpHash + member position + MessageId | broker membership owner; configuration/membership revision and hourly | only D5's fresh zero-send proof plus byte-identical `ContinueSamePin`; manual action merely requests this check |
| `ScopeRetentionCapacityHold` | tenant + scope shard | gateway; tombstone expiry/compaction/capability revision and hourly | the exact shard admits the scope record; no other shard's free bytes are asserted as available |
| held delivery | physical subscription + exact carrier hash | Operations; typed cause-cleared signal and bounded backoff | redrive of exact retained bytes reaches terminal route decisions, or terminal quarantine completes for a permanently nonadmissible carrier |
| `RedriveEvidenceRepairHold` | complete held-delivery key + disputed attempt count | Operations; authenticated attempt/carrier repair and hourly | D11.1 reads back the exact repaired attempt, locator and active charge before permitting one next redrive; unavailable or forged repair preserves the indexed prerequisite |
| legacy resume incident | legacy resume handle | aggregate/Operations recovery owner; evidence restoration and hourly | a verified resume capsule exists and D9 resumes it; `legacy_resume_evidence_unavailable` has no fabricated recovery and closes only through tenant erasure or a separately approved migration story |

The last row is intentionally non-resumable when evidence never existed. It is still a complete lifecycle: it is indexed, diagnosed, retained within quota, and removed only by whole-tenant erasure or future separately approved migration—not by command replay.

The closed `ownerKind` set is `actor`, `coordinator`, `gateway`, `subscriber`, `projection`, `operations`, and `quota-coordinator`. The closed hold/reason mapping is: `LegacyArrayLimit -> legacy_array_limit`; `ActivationInventoryCapacityHold -> full_replay_inventory_capacity`; `AdmissionEvidenceHold -> admission_evidence_hold`; `ResponsePreparationHold -> response_preparation_hold`; `OutcomeEvidenceHold -> outcome_evidence_hold`; `OutcomeEvidenceConflict -> outcome_evidence_conflict`; `TerminalEvidenceHold -> terminal_evidence_hold`; `PublicationRetryExhaustedHold -> publication_retry_exhausted_hold`; `PublicationDrainLimitHold -> publication_drain_limit_hold`; `PublicationResumePreparationHold -> publication_resume_preparation_hold`; `PublicationPinCapacityHold -> publication_pin_capacity_hold`; `PinCapacityQueueCorruptionHold -> pin_capacity_queue_corruption_hold`; `FirstSendMembershipChangedHold -> first_send_membership_changed_hold`; `ScopeRetentionCapacityHold -> scope_retention_capacity_hold`; `HeldDelivery ->` exactly one D11 held-delivery reason; `ResumeAttemptCollectionHold -> resume_evidence_hold`; `QuotaGenerationIncident -> quota_generation_exhausted`; `RedriveEvidenceRepairHold -> redrive_evidence_repair_hold`; and `LegacyResumeIncident -> legacy_resume_evidence_unavailable`. Producers reject an unknown value instead of indexing or charging it under a catch-all. A reason change is a new D11 entry revision, never an in-place reinterpretation. The repair prerequisite has its own entry under the same HeldDelivery inventory actor; its reason does not reinterpret the original carrier reason.

Every entry producer, decoder and inventory consumer enforces this complete owner mapping: `LegacyArrayLimit` and `ActivationInventoryCapacityHold` use `projection`; `AdmissionEvidenceHold` and `ScopeRetentionCapacityHold` use `gateway`; `ResponsePreparationHold`, `OutcomeEvidenceHold`, `OutcomeEvidenceConflict`, `TerminalEvidenceHold`, `PublicationRetryExhaustedHold`, `PublicationDrainLimitHold`, `PublicationResumePreparationHold` and `ResumeAttemptCollectionHold` use `coordinator`; `PublicationPinCapacityHold`, `PinCapacityQueueCorruptionHold` and `QuotaGenerationIncident` use `quota-coordinator`; `HeldDelivery` and `RedriveEvidenceRepairHold` use `operations`; `FirstSendMembershipChangedHold` uses `subscriber` as the closed owner kind for the broker membership owner; `LegacyResumeIncident` permits exactly `actor` or `operations` for the aggregate/Operations recovery owner. A valid owner enum alone does not authorize a different hold kind. D12 exercises all 19 kinds against all seven owners, including both permitted legacy recovery owners.

## D2. Full-replay activation — replacement for `[I-06]`

A projection route without a `5a` row is a full-replay route. The existing bounds remain 100,000 events, 64 MiB cumulative readable payload, and 256 MiB conservative accounting (8,192 bytes per event), whichever is first. No dispatch truncates, skips, or partially applies a complete history.

Inventory authentication covers the actual measured longest event count, complete readable-payload byte maximum and conservative accounting for that counted history. Count and byte metrics are nonnegative u64 values; checked multiplication must yield `largestAccountingBytes = longestCount × 8,192`. Overflow, missing measurements or inconsistent accounting stops activation as an indexed `LegacyArrayLimit` before any route dispatch, including when an incremental capability exists. Every consumer derives conservative accounting from count before selecting the disposition or enforcing a hard bound. D12 tests understated and inconsistent accounting, multiplication overflow, readable-byte boundaries and the nearest legal accounting counts below/at/above 75% (24,575/24,576/24,577). The lower accounting bound can hold a route before the independent 100,000-event bound; it never permits partial replay.

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
9. Any other available failed set—including an unknown class, an empty class set, or contradictory class evidence—is `CommandOutcomeHold(outcome_evidence_conflict)`. It can never project retry exhaustion merely because an attempt counter is at maximum.

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
| `response_preparation_hold` | A8 preparation/output/receipt is incomplete | coordinator follows unchanged A8/[I-09]: transfer the `HX-EV-RESPONSE-PREPARATION-1` Rendering fence, require both immutable outputs and their generation-bound CAS receipts, create/read back `HX-EV-RESPONSE-PREPARATION-WRITE-1` at `command-response-preparation-write:` plus ScopeOpHash, then continue; a missing immutable output is a non-resumable indexed incident with no rerender/send/arming exit, removable only by whole-tenant erasure or a separately approved migration |
| `publication_pin_capacity_hold` | D7 atomic batch reservation refused, invalid/overflowing arithmetic, unavailable/contradictory candidate evidence, or waiting | quota coordinator obtains a checked candidate and the quota queue grants the whole batch |
| `outcome_evidence_hold` | required immutable outcome/C5 source unavailable | coordinator reads back the complete source set |
| `outcome_evidence_conflict` | same-attempt or immutable-source contradiction | authoritative provider evidence proves the retained row; no overwrite |
| `resume_evidence_hold` | complete window attempt collection reached a bound | coordinator obtains definitive-result/closure authority or completes operation erasure |
| `quota_generation_exhausted` | a charged record/counter reached u64 maximum | quota coordinator completes an approved migration or whole-scope erasure |
| `terminal_evidence_hold` | definitive class-02/03 member lacks C5 closure, including mixed class-01/class-02 | C5 terminal closure or later authoritative accepted evidence |
| `first_send_membership_changed_hold` | D5 cannot prove compatible configuration or membership restoration | broker configuration or membership revision plus fresh zero-send and exact-byte proof |

The set is closed. Retry-exhausted and drain-limit states use their nonterminal `EventsStored` projections, not this 503. Legacy resume endpoints use the separate closed D9 reason set. The Admin command list must merge its submission-time `CommandSummary` with the latest authenticated status/hold inventory at read time; it shows the current hold code/reason instead of leaving a held command as unqualified `Processing`.

## D4. Legacy admission and scope retention — replacement for `[I-12]`

The scope key remains `command-execution-scope:` plus lowercase-hex SHA-256(`U tenant || U executionMessageId`). Required A8 scope records, legacy claims, and tombstones have distinct domain separators. No unavailable read or lost CAS proceeds to archive/status write, actor invocation, or A8 selection.

### D4.1 Slice-2 legacy claims and slice-4 cutover

The slice-2 gate first authenticates D11.2 inventory/directory bootstrap, tenant onboarding and precommit hold reservations plus gateway re-evaluation ownership; absent/unavailable readiness stops before admission. From the start of slice 2, every legacy admission reads the scope key before any write or actor call and CAS-creates `HX-EV-COMMAND-SCOPE-LEGACY-2\0 || 01 || 000a` (at most 8 KiB): `01` U tenant, `02` U execution MessageId, `03` U domain, `04` U aggregate ID, `05` U command type, `06` B32 exact archived command-payload hash, `07` Q claim UTC, `08` Q expiry UTC, `09` N legacy cohort generation, and `0a` B32 cutover-record hash (zero while the cohort is open). Required record/tombstone conflict; an identical unexpired claim is the legacy retry. A changed claim is `CommandIdentityConflict`.

The maximum legacy evidence horizon `H` is the larger of every configured command-status, archive, idempotency, actor-idempotency, replay, and backup retention, checked and pinned in the cutover record. Because tombstones have a ten-year hard ceiling, readiness rejects `H > 315,576,000 seconds` as `scope_retention_horizon_unsupported`; it does not shorten `H` or activate slice 4. Slice 4 for a domain cannot activate until slice 2 has continuously written claims for at least `H`, every legacy in-flight owner present at slice-2 start has closed, and the gateway has read back the cutover record. Thus every still-live legacy execution has a claim; the three fallback reads of archive/status/actor used by loop 1 stop after cutover and never remain a permanent per-admission tax.

`HX-EV-LEGACY-SCOPE-CUTOVER-1\0 || 01 || 0008` (at most 4 KiB) has `01` U tenant or deployment-wide `*`, `02` U domain, `03` N cohort generation, `04` Q claim-enforcement start UTC, `05` Q cutover UTC, `06` N horizon seconds, `07` B32 complete legacy-owner-empty inventory root, and `08` B32 predecessor cutover hash. It is written/read back under the gateway admission fence and retained while its cohort has any claim. Claim or CAS unavailability yields `admission_evidence_hold`; this is the BC-15 slice-2 fail-closed behavior in D13.

### D4.2 Sharded scope accounting, tombstones, and status

Scope records map to one of exactly 256 shards by the first byte of SHA-256(`U tenant || U executionMessageId`). The capability divides `scopeRetentionCeiling` deterministically: each shard gets `floor(ceiling/256)` and shards `0..(ceiling mod 256)-1` get one extra byte. The scope record and its shard usage record change in one backend transaction. There is no tenant-wide hot CAS.

Each legacy claim, imported required-record producer, exact retry, expired migration, required compaction and expiry/deletion derives this shard anew from the authenticated tenant/execution fields. It authenticates both usage-record tenant/shard and addressed native generation before mutating; a caller-selected shard, mismatched tombstone tag 08 or altered usage authority refuses with exact prior bytes and charges. Replacing an expired legacy claim with required authority additionally authenticates the exact retained predecessor, expiry and closure of all old obligations; its 8 KiB charge becomes the existing required 4 KiB charge in that same transaction.

`HX-EV-SCOPE-SHARD-USAGE-1\0 || 01 || 0007` (at most 2 KiB) has `01` U tenant, `02` N shard `0..255`, `03` N required-record count, `04` N tombstone count, `05` N charged bytes, `06` N generation, and `07` B32 predecessor usage hash. Eight bounded CAS attempts use delays 0, 5, 10, 20, 40, 80, 160, and 320 ms; loss after the eighth returns `admission_evidence_hold` with no domain invocation. At most 256 shard writers can progress independently for one tenant.

After every retry/status/rollback/backup obligation closes, `ScopeRetentionReconciler` compacts a required record to `HX-EV-COMMAND-SCOPE-TOMBSTONE-2\0 || 01 || 0008` (at most 4 KiB): `01` U tenant, `02` U execution MessageId, `03` B32 ScopeOpHash, `04` B32 original input hash, `05` B32 compacted record hash, `06` Q compacted UTC, `07` Q expiry UTC, and `08` N scope shard. Expiry is compacted UTC plus the capability's `scopeTombstoneRetentionSeconds`, which is at least `H` and at most 10 years. The reconciler runs hourly and at 75% shard occupancy; after authenticated expiry and absence of every retained obligation it deletes the tombstone and decrements the shard in one transaction. A status lookup that finds a tombstone returns HTTP 410 type `https://hexalith.io/problems/command-status-expired`, never legacy fallback. Exact late admission before expiry returns the existing idempotency-expired 409; changed identity conflicts.

An expired legacy claim still present at the key is not treated as absent. An exact same-input retry may CAS-replace it with the next claim generation only after the reconciler proves all old obligations closed and decrements the old charge in that same transaction; a changed claim conflicts while the expired row exists. After authenticated deletion, either identity may create a new claim normally. This bounded expiry is BC-16.

For an identical legacy-to-legacy renewal, first compare the authenticated current claim and request UTC. The exact existing bytes are an idempotent retry only while `requestUtc < existingExpiryUtc`; an absent current UTC or an expired identical byte image cannot renew by itself. After expiry, the gateway requires the exact predecessor bytes and native generation, the same tenant/execution/domain/aggregate/command type/payload hash, the original cohort generation and cutover hash, and authenticated closure of retry/status/rollback/backup obligations. The replacement keeps those immutable input/cohort fields, sets claim UTC to the supplied current UTC, gives it a later bounded expiry, and writes the next native generation with the preceding byte hash. The shard usage transaction refunds the old 8 KiB and reserves the new 8 KiB once, with no net occupancy increase; missing closure, changed input, lost readback or exhausted generation refuses unchanged. A lost acknowledgement reads the installed exact renewal and its unchanged shard count. The new UTC never starts a new cohort.

A full shard creates `ScopeRetentionCapacityHold` before invocation. Its exits are reconciliation deletion or a capability revision that increases that shard's deterministic allowance. Claims expire with their legacy evidence and are charged 8 KiB in the same shard; required records and tombstones each charge 4 KiB. A required-to-tombstone replacement releases no bytes; only authenticated deletion/readback permits the exact once-only refund.

The local scope transition consumes the actual imported `HX-EV-COMMAND-SCOPE-1` record at the unchanged scope key. It verifies that tombstone tenant, execution, ScopeOpHash and input hash match that required predecessor, tag 05 hashes those exact bytes, and authenticated retry/status/rollback/backup absence permits compaction at tag 06 UTC. Absent, changed or unrelated predecessors and open obligations refuse before writes. The coupled usage transition decrements required count once, increments tombstone count once and retains 4 KiB; exact lost-ack retry reads the already installed tombstone and changes neither count nor charge. Restart resolves the same addressed bytes and native receipts. Authenticated expiry deletes/readbacks that exact tombstone once, leaving the charged usage header; tenant erasure removes all remaining scope rows and their header. D12 exercises genuine required admission, predecessor/obligation refusals, migration, persisted compaction readback, HTTP 410 status, repeated deletion and erasure. Claims and tombstones are deleted only by the authenticated expiry/obligation transaction above (or whole-tenant erasure); required records compact only after all obligations close; cutover remains while its cohort has a claim; empty usage rows remain charged at their 2 KiB ceiling until authenticated tenant erasure. Whole-tenant erasure removes every remaining row. These are the only retention rules. The codecs activate in slice 2; required admission and tombstone behavior activate in slice 4. Known answers `D12-legacy-claim`, `D12-cutover`, `D12-usage`, and `D12-tombstone` appear in D12.

The complete maximum legacy claim is 5,270 bytes: domain/NUL/codec/count framing, ten tag bytes, five `U` length prefixes and five independent 1,024-byte identifiers, two B32 hashes, two Q times and one N generation. The complete usage record is 1,136 bytes with its maximum tenant; the complete tombstone is 2,219 bytes with both maximum identifiers. Their 8/2/4 KiB caps and equal reservations cover every legal field combination. The 2 KiB usage reservation is included once in each nonempty shard admission; it remains after the last row refund. At the minimum 64 MiB scope ceiling, each shard has 262,144 bytes, including this header, leaving 260,096 bytes: at most 31 maximum legacy reservations or 63 required/tombstone reservations. Mixed occupancy uses the exact sum. D12 checks every identifier below/at/above its byte maximum, each complete family cap, shard fill just below/at/above its exact reservation, authenticated deletion, unchanged refusal and repeated refund. No other shard pays the difference.

## D5. First-send membership hold — amendment to 6.5c C2 and replacement support for `[I-16]`

C2's existing purpose-`2a` outcome is versioned instead of create-once forever. `EmptyNamespace` and `InitialRowOnly`, complete partition evidence, and byte-identical `ContinueSamePin` conditions are unchanged. A membership change that cannot meet them creates `FirstSendMembershipChangedHold`; it sends nothing and retains the original pin.

Only verified configuration or membership restoration exits it. A configuration or membership revision triggers the broker to freeze the still-zero-send old namespace and produce a **fresh** atomic zero-send proof. If the active configuration now selects the same logical consumer and destination and reproduces byte-for-byte the pinned request and predicted accepted bodies/header images, mode pair, renderer, and six-header projection, a bounded resolution may return `ContinueSamePin`. Changed or incomplete evidence remains held. Manual action can request this re-evaluation but cannot select a route, waive bytes, abandon the member, or claim publication complete.

`HX-EV-FIRST-SEND-MEMBERSHIP-RESOLUTION-1\0 || 01 || 000c` (at most 16 KiB) has `01` U tenant, `02` B32 ScopeOpHash, `03` N member position, `04` U MessageId, `05` B32 immutable global-pin hash, `06` N resolution generation, `07` B32 predecessor outcome/resolution hash, `08` B32 active membership claim hash, `09` B32 fresh complete zero-send proof root, `0a` U disposition (`ContinueSamePin` or `FirstSendMembershipChangedHold`), `0b` U broker membership issuer, and `0c` Q decision UTC. Its stable head key is C2's existing first-send key; the signed claim, purpose-`2a` carrier, complete proof, and head CAS receipt commit together. Generation is contiguous, a changed revision uses the next generation, and an identical lost acknowledgement reads back. Once a nonzero attempt/queue/acceptance exists, no resolution can reopen this pre-first-send path. Slice 4 owns the record; it is charged in A8's referenced-evidence reservation and erased with the tenant. Known answer `D16-membership-resolution` appears in D12.

## D6. Destination configuration — replacement for `[I-17]`

Destination-ID derivation and all C1 known answers remain unchanged. The actual D12 consumer is `destination_config(raw: bytes, component: str, topic: str)`. Admission accepts only canonical JSON schema `hexalith.eventstore.destination/1`, at most 65,536 whole-document bytes, with exactly `component`, `metadata`, `schema`, and `topic`. Component/topic must be strings of 1..1,024 UTF-8 bytes and equal the exact outbox component/topic bytes. Metadata is an object with at most 64 unique string names and string values; each decoded name/value and their aggregate UTF-8 byte sum are at most 16,384 bytes. Duplicate keys at either object level, missing/extra fields, wrong types/schema, malformed UTF-8/JSON and outbox mismatch refuse before admission. All object names sort canonically, UTF-8 remains unescaped where JSON permits, and the exact compact re-encoding must equal the input, with no insignificant whitespace or final LF. Escaped control characters can reach the whole-document ceiling while decoded metadata remains within its separate limit. D12 independently checks those limits below/at/above, including exact 65,535/65,536/65,537-byte documents, aggregate metadata and duplicate keys. A configuration revision is also a D5 re-evaluation trigger; it grants no compatibility by itself. Slice 2 writes configurations and slice 4 admits publication. Known answer `D17-destination-config` appears in D12.

## D7. Quota ledger and atomic pin batches — replacements for `[I-29]` and `[I-30]`

The publication-retention ledger on `publicationRetentionBackend` is the single quota authority even when object bytes or pins live on another backend. Cross-backend atomicity is not claimed. It uses a durable two-phase reservation: the ledger atomically reserves a complete candidate batch against tenant and deployment counters; each pin CAS then names and verifies that reservation. No pin may send before every batch pin and reservation attachment reads back. A crash leaves a discoverable reservation that the operation owner completes; it never admits a partial batch or refunds while any pin may exist.

The capability is `HX-EV-PUBLICATION-RETENTION-CAPABILITY-2\0 || 01 || 000f` (at most 64 KiB): existing tags `01` deployment identity, `02` revision, `03` canonical backend descriptor, `04` tenant ceiling, `05` deployment ceiling, `06` unidentified reserve, `07` unidentified ceiling, `08` overhead `o`, `09` scope-retention ceiling, `0a` predecessor hash, `0b` effective UTC; plus `0c` N scope shard count (exactly 256), `0d` N scope-tombstone retention seconds, `0e` N pin-wait directory ceiling (1..50,000), and `0f` N maximum quarantined carrier bytes (between 193 MiB and 256 MiB). Existing feasibility rules remain: `1 GiB <= tenant <= deployment`, tenant + reserve <= deployment, reserve >=195 MiB, reserve <= unidentified ceiling <= deployment, `scopeRetentionCeiling >= 64 MiB`, and `0 <= o <= 1,114,112`. A smaller scope ceiling is `publication_retention_capability_invalid`; slice 2 may store it for diagnosis but slice 4 cannot activate.

Every charged object has `HX-EV-PUBLICATION-CHARGE-2\0 || 01 || 000f` (at most 4 KiB): `01` U deployment identity, `02` U account kind (`tenant` or `capture-scope`), `03` U account ID, `04` B32 canonical object-key hash, `05` U kind (`pin-batch`, `side-record`, `retained-object`, `oversize-quarantine`, or `resume-window`), `06` N canonical length, `07` N recorded overhead, `08` N charged amount, `09` N capability revision, `0a` N charge generation, `0b` U state (`staged`, `active`, or `released`), `0c` B32 predecessor charge hash (zero exactly at generation 1), `0d` O(B32) transfer owner, `0e` Q update UTC, and `0f` N transferred marker (exactly 0 or 1). Marker 0 requires absent owner in every generation; marker 1 requires kind `resume-window` and a present stable transfer owner in every generation, including released. Length and overhead are non-negative checked u64; charged amount always equals their checked sum. A later attach must match kind and canonical length and uses the recorded amount despite a changed `o`. Ordinary creation writes `active` with absent transfer owner. A D9 successor writes `staged` with the stable request identity as transfer owner while the prior active generation remains authoritative and every counter includes both amounts. Successor-state readback authorizes exactly one `staged -> active` generation and exactly one release/decrement of the predecessor; D7 and D9 share one rollback authority: only authenticated absence of both the successful audit and successor permits `staged -> released`; unavailable evidence holds. A successful-audit readback with no successor retains the stage and requires completion of that recorded success. Absence of successor alone never permits refund. Every later generation of a transferred charge retains its authenticated transfer owner. Refund otherwise CAS-writes the next `released` generation and decrements counters once only after deletion/closure readback; a later recreation needs the next `active` generation and fresh counter admission.

Each counter is `HX-EV-PUBLICATION-COUNTER-1\0 || 01 || 0008` (at most 4 KiB): `01` U deployment identity, `02` U counter kind (`tenant`, `capture-scope`, `tenant-pool`, `deployment`, or `unidentified`), `03` U counter ID, `04` N used bytes, `05` N active charge count, `06` N generation, `07` B32 predecessor counter hash, and `08` Q update UTC. Tenant accounts use both their tenant counter and `tenant-pool`; capture scopes use their account counter and `unidentified`; every charge also uses deployment. Authenticated tenant accounts together cannot exceed deployment minus reserve. Capture scopes together cannot exceed `unidentifiedCaptureCeiling`. Lowering below usage admits nothing new and evicts nothing. Account authority is keyed by `(accountKind, accountId)` in usage, native generations, predecessor hashes and reserve/refund receipts; a valid tenant ID and capture-scope ID with identical UTF-8 bytes remain independent. A tenant charge touches its kind-qualified tenant row plus tenant-pool and deployment, while a capture-scope charge touches its distinct kind-qualified account row plus unidentified and deployment. Every pin, capture, redrive cleanup and erasure consumer authenticates that exact kind before debit or refund; a receipt for another kind or an old generation cannot authorize it. The field-derived complete envelope is `2,163 + UTF8Bytes(counterKind)` with independent maximum 1,024-byte deployment and ordinary counter ID, all length prefixes, tags, three N fields, B32 predecessor and Q time. The five enum widths yield 2,169, 2,176, 2,174, 2,173 and 2,175 bytes. The qualified `tenant:` counter ID permits seven prefix bytes plus the full tenant, 1,031 bytes only for that tenant form, yielding 2,176 bytes. All other IDs remain at 1,024 bytes. Every counter decoder, bootstrap, retained receipt and support reservation uses the same 4 KiB cap; the D8 directory support budget below covers the complete counter bodies and bounded native receipts.

`HX-EV-PIN-BATCH-RESERVATION-2\0 || 01 || 000d` (at most 64 KiB) has `01` U tenant, `02` B32 ScopeOpHash, `03` B32 candidate batch root, `04` N pin count, `05` B rows sorted by member position (`u32 position || U MessageId || B32 exact pin hash || N canonical length || N charged amount`), `06` N total charged amount, `07` N capability revision, `08` B32 tenant-counter predecessor hash, `09` B32 tenant-pool-counter predecessor hash, `0a` B32 deployment-counter predecessor hash, `0b` U state (`reserved`, `installed`, or `released`), `0c` N generation, and `0d` Q update UTC. The candidate batch root is SHA256 of the exact uncounted tag `05` member-row bytes. Tag `04` must equal the row count and tag `06` the checked sum. All three predecessor hashes authenticate the exact counters advanced by the transaction; a missing or stale predecessor aborts the whole CAS. At the imported 1,024-byte MessageId maximum, each member row is exactly 1,080 bytes; the maximum-width non-row record is 1,291 bytes, so the hard member ceiling is `floor((65,536 - 1,291) / 1,080) = 59`. The ceiling remains 59 for short IDs. An otherwise-valid V1 batch of 60..1,000 members fails A8 readiness/admission as `AppendPreparationLimit` before append; it is never partially segmented after commit.

Before aggregate fit or ledger mutation, the actual batch-reservation transition authenticates each exact candidate pin/charge and its kind, canonical length, capability revision, recorded `o` and checked `length + o` amount. Each global pin must be at most 449 MiB even when all aggregate counters fit; missing, unavailable, contradictory or oversize candidate authority refuses with byte-identical counters/reservations. Exact retained-reservation retry authenticates the full original candidate rows and amounts without substituting current capability overhead. The ledger CAS creates every per-pin charge plus this reservation and advances all counters together. The retained reservation authenticates its full tenant account, ScopeOpHash, candidate root, exact ordered member rows, capability revision, and original predecessor receipts; its request identity is the hash of those exact reservation-intent fields. Changed account, scope, candidate, request identity, or rows at an existing key conflicts without mutation even when totals match. A fresh CAS still rejects stale counter predecessors; an exact lost acknowledgement instead authenticates the retained reservation and charge attachments without reissuing that stale CAS. Before reserve, refund, transfer, or reconciliation, preflight every changed counter/charge/reservation generation with checked u64 increment and every amount/count sum; a maximum generation fails closed with byte-identical state and an indexed `QuotaGenerationIncident` (`quota_generation_exhausted`). The pin backend installs all exact candidates with the reservation hash; only full readback advances to `installed`. No waiting batch owns a charge. Reservation and refund both decode `accountKind` as exactly `tenant` or `capture-scope`; an unknown value is a codec/reconciliation incident with no counter mutation, never an alias for capture scope.

Per-kind maxima remain 449 MiB for one global pin, 193 MiB for side/ordinary retained objects, 256 MiB for provider-quarantined oversize carriers, and 1 GiB for the one active resume window. A negative or overflowing input, or unavailable/contradictory evidence for any candidate length, overhead, amount, count, or total, maps to `CommandOutcomeHold(publication_pin_capacity_hold)` before comparison. Its stable subject is `capacity-subject:` plus lowercase-hex SHA-256(`"HX-EV-CAPACITY-SUBJECT-1\0" || 01 || B32 ScopeOpHash || B32 immutable A8 outbox/member-plan root`), not an unverified candidate-batch root. If those admitted-plan values are unavailable, A8 fails before commit; no later hold may invent a subject. The hold creates the D11 inventory entry using the pre-reserved A8 slot, but creates no reservation, charge, counter delta, pin, or send. The quota coordinator re-evaluates from immutable outbox/member-plan and renderer evidence on the D1 triggers; only a fully checked candidate may enter the D8 queue or retry reservation. Tenant/deployment erasure releases only after every object is deleted/read back. The ledger activates in slice 2, resume charges in slice 3, and pin/capture charges in slice 4. Known answers `D29-capability`, `D29-charge`, `D29-counter`, and `D29-pin-batch` appear in D12.

Every reservation comparison uses the current authenticated `HX-EV-PUBLICATION-RETENTION-CAPABILITY-2` readback for tenant, tenant-pool, deployment and unidentified-capture limits. Cached ceilings are rederived from that authority on every admission; matching revision/overhead cannot validate independently changed cached numbers. Missing, changed or contradictory authority refuses before any partial batch/counter/charge write. A reduced capability limits new admission and leaves every retained charge's original amount, overhead and ownership unchanged until its existing authenticated refund. D12 uses individually legal 400 MiB pins with a stale larger tenant cache and the lower encoded 1 GiB ceiling, exact account boundaries, all aggregate pools and unavailable/changed authority; every refusal is byte-identical.

## D8. Fair pin-capacity waits — replacement for `[I-31]`

A refused **valid, checked** D7 batch reservation creates one wait; it never leaves some pins charged. Invalid arithmetic/evidence remains the same indexed `PublicationPinCapacityHold` without a D8 queue row because no trustworthy tag `04` total or tag `05` holding counter exists. Its quota coordinator performs D1's 60-second and revision-triggered deterministic rerender/revalidation. Once checked evidence exists, it atomically creates the D8 row under the same stable subject before removing the inventory-only state; reservation still occurs only on a later whole-batch grant. `HX-EV-PIN-CAPACITY-WAIT-2\0 || 01 || 000c` (at most 4 KiB) has `01` U tenant, `02` B32 ScopeOpHash, `03` B32 candidate batch root, `04` N total charged amount, `05` U holding counter (`tenant` or `deployment`), `06` N global first-hold ticket, `07` Q first-hold UTC, `08` N re-attempt count, `09` U state (`queued` or `parked`), `0a` B32 current capability hash, `0b` B32 immutable outbox/member-plan root, and `0c` Q last-check UTC.

There is one deployment directory and one directory for each tenant with waits. `HX-EV-PIN-CAPACITY-QUEUE-4\0 || 01 || 000b` (at most 64 MiB) has `01` U deployment identity, `02` U counter ID (`deployment` or `tenant:` plus tenant), `03` N generation, `04` N entry count, `05` B concatenated fixed-order rows with **no inner count** (`N ticket || U tenant || B32 ScopeOpHash || U state`), `06` N parked count, `07` N reserved-slot count, `08` N authenticated capability ceiling, `09` N last issued global ticket, `0a` B32 predecessor directory hash, and `0b` Q update UTC. Tag `04` controls exact parsing and equals queued plus parked; `entryCount + reservedSlotCount <= tag 08 <= 50,000`. The duplicated capability value must equal the authenticated D7 capability revision used by the transition. A row consumes exactly one previously reserved slot in the same CAS, so no operation ever appends to a full directory. Parked waits remain in this ordered directory and are discoverable in a key-only store.

The deployment directory is also the sole durable global ticket allocator. Its `lastIssuedTicket` starts at zero; admission verifies subject, both slots, state, and charge before CAS-incrementing that field and assigning the resulting positive u64 to the wait. Materialization consumes that subject's exact allocated ticket, which may equal the allocator head; it never requires a second ticket greater than that head. The admission transaction materializes exactly one row and keeps the counterpart slot reserved. A lost acknowledgement rereads the stable subject's admission receipt and same ticket/row without allocating or charging again. A conflicting subject or predecessor loses without allocating a ticket; a refused or invalid admission creates no orphan slot or charge. Pre-commit allocation evidence remains discoverable under the same subject and is either materialized once or released on authenticated preparation rollback; issued tickets are never reused. `lastIssuedTicket == 2^64-1` fails pre-commit admission as `pin_wait_ticket_exhausted`; it never wraps, reuses a ticket, or commits the command. Queue rows are canonically sorted by `(ticket as unsigned numeric u64, tenant as raw canonical UTF-8 bytes, ScopeOpHash as raw 32 bytes)`; state is not part of the sort key. Both decoding and reconstruction use that exact tuple. Conflicting materialization only refuses the caller and leaves the legitimate pending owner, ticket, both slots, and charge byte-identical. Cancellation requires that preparation's authenticated owner, ticket and exact predecessor receipt; its CAS removes both interests and releases its own charge once. Repeating cleanup is a no-op. It cannot cancel another tenant's preparation or a later generation.

Before command commit, A8 charges D8.1's exact 40 KiB owner/wait/paired-row/receipt `side-record` reserve and separately precharged directory/source storage, rejects a stable capacity subject already resident or reserved in either directory, and atomically reserves one slot in the deployment directory and one in the candidate tenant directory under that subject while allocating its ticket. If either slot, ticket, or charge is unavailable, admission fails `AppendPreparationLimit`; a committed command is never left outside the directories. Exactly one reservation is materialized as the current `queued`/`parked` row while the other remains reserved. A cross-counter move atomically turns the destination reservation into the row and the source row back into its reservation, so it cannot deadlock on a full destination. Refund of both slots/row charges occurs only after the wait and both directory interests delete/read back, or during whole-operation erasure. Imported pre-reservation waits must complete a bounded migration into separately charged/reserved slots before slice-4 readiness; they cannot be hidden in an overflow counter.

One wait has one materialized row in exactly one directory and its reserved counterpart in the other. New batches queue behind existing eligible waits even if current counters would fit. The oldest queued deployment head retries after every refund/capability revision and at least every 60 seconds. If deployment capacity refuses it, it stays. If deployment fits but its tenant refuses, it moves atomically to that tenant's directory with its ticket unchanged; the destination's pre-reserved slot becomes the row and the source row becomes its reservation, and the deployment queue advances. The oldest tenant head moves back only after an authenticated current fit result binds that exact tenant counter usage, capability, current candidate amount/wait hash and ticket. Its actual checked `used + candidateAmount <= tenantCeiling` result must be true. A false result keeps the exact tenant residence, paired interests, charge and bytes unchanged while another fitting tenant may progress; missing, unavailable, forged or stale fit authority refuses unchanged. The inverse transaction preserves its original ticket and both interests, and all deployment candidates are ordered by ticket. It owns no quota capacity during either move, so no circular wait exists. A candidate larger than a current ceiling is `parked` in its already-reserved slot; an authenticated capability or immutable-source rerender re-evaluates it at its original ticket. Current wait state is independent of the immutable initial carrier state. Valid feasible re-evaluation CAS-installs `queued`, the current capability hash and current wait/authority hashes together with both interests and directory receipts. It preserves subject, ticket, owner, carrier and original admission receipt/hash; still-infeasible parked-only turns remain byte-identical no-ops. Static pre-Prepared feasibility and directory-slot reservation prevent a committed command from becoming undiscoverable.

An empty directory turn and a parked-only directory turn are durable no-ops: generation, rows, counts, reservations, ticket head, and charges remain byte-identical and the next re-evaluation deadline remains scheduled. Too few/extra decoded rows, `entryCount + reservedSlotCount > authenticated capability ceiling`, `parkedCount > entryCount`, a state/count mismatch, duplicate ticket/ScopeOpHash, duplicate stable subject in row/reservation sets, wrong canonical sort, ticket above the allocator head, or trailing bytes creates charged/indexed `PinCapacityQueueCorruptionHold` and performs no reservation or move. The quota coordinator owns it. Exit requires authenticated reconstruction from every D8 wait row plus D8.1's physically addressed fixed predecessor-directory bytes, owner manifest and authenticated current-version receipt, installation/readback of one canonical queue generation, and proof that no ticket was lost or duplicated; manual edits cannot clear it.

On each turn, exact outbox/member plan, purpose-02 key, membership, configuration, and render inputs are reverified. If valid, the original candidate bytes are used. If invalid, the owner deterministically rerenders from immutable outbox intent, CAS-updates the candidate root/amount and derived current queued/parked state without changing ticket, and retries. A successful whole-batch D7 reservation removes the wait/directory row atomically on the ledger, then D7 installs pins. Tenant erasure deletes tenant waits and its deployment rows after operation erasure. Slice 4 owns the queues. Known answers `D31-wait` and `D31-queue` appear in D12.

### D8.1 Encoded admission, paired ownership, and retained repair sources

The deployment and tenant queue heads, owner indexes, wait authorities, D7 charges/counters and native receipts must share one serializable `publicationRetentionBackend` transaction. Readiness proves that transaction, including deletion/refund and maximum native receipt sizes; different directory backends fail readiness before command commit. Pin installation remains D7's separate reservation-bound phase. No broker or cross-backend atomicity is inferred. Model scope strings are aliases for authenticated admitted OperationIds; production ScopeOpHash and member-plan roots come from immutable A8 authority, never a new identity derivation.

`HX-EV-PIN-WAIT-PREPARATION-1\0 || 01 || 0007` (4 KiB) is the exact admitted carrier: `01` U tenant, `02` U original OperationId, `03` U initial state (`queued` or `parked`), `04` B32 original ScopeOpHash, `05` B32 immutable outbox/member-plan root, `06` B32 initial candidate-batch root, `07` N positive checked candidate total. The stable subject is the existing D7 capacity-subject digest over tags 04/05. Its deterministic owner is `queue-owner:` + lowercase SHA256(`U tenant || U OperationId`), bound to the original authenticated admission fence and provider owner receipt; knowledge of that string grants no cancellation authority.

`HX-EV-PIN-WAIT-AUTHORITY-1\0 || 01 || 0017` (16 KiB) stores: `01` U deployment; `02` U tenant; `03` B32 stable capacity subject; `04` B32 ScopeOpHash; `05` U owner; `06` N positive global ticket; `07` B exact preparation carrier; `08` B32 carrier hash; `09` N positive checked generation; `0a` B32 predecessor authority hash (zero exactly at generation 1); `0b` U phase (`allocated`, `admitted`, `cleanup`); `0c` U residence (`none`, `deployment`, `tenant`); `0d` B32 allocation operation receipt identity; `0e` O(B32) original admission operation receipt identity; `0f` N canonical storage reserve (exactly 40 KiB; D7 charged amount additionally includes recorded `o`); `10` N authenticated directory ceiling; `11` Q first-hold UTC; `12` Q update UTC; `13` B exact original deployment predecessor receipt; `14` B exact original tenant predecessor receipt (empty only for authenticated absent tenant bootstrap); `15` O(B32) wait-deletion receipt hash; `16` B32 current wait hash; `17` B32 original admission-wait hash. Each retained receipt is at most 1 KiB and is authenticated, typed and addressed below. Tags 02..08 are immutable and authenticated against the preparation; changed tenant, owner, ticket, carrier, phase/predecessor or ceiling refuses without mutation. `allocated` owns both reserved interests, has residence `none`, absent admission/deletion receipts and zero wait hashes. `admitted` owns exactly one materialized wait/row and one counterpart reservation; `cleanup` owns two reserved interests until final deletion/refund, no wait body and a read-back deletion receipt. Admission receipt is present for admitted/cleanup; an allocated rollback uses the exact absent-wait deletion receipt and zero admission-wait hash. Initial admission and each move install/read back the current wait hash, exact wait tag 05 (`tenant` iff tenant residence, otherwise `deployment`), both directory interests and their generation receipts together. The immutable admission-wait hash/receipt remains unchanged across moves, permitting exact lost-ack retry without re-admission. Ordinary rerender retains original preparation/admission authority and CAS-updates the current candidate/amount/state/capability/wait hash under the same subject and ticket after complete immutable-source revalidation; it cannot replace the original allocation carrier.

Allocation operation identity is SHA256(`"queue-allocation:" || B32 subject || N ticket || B32 carrierHash || B(exact deployment predecessor receipt) || B(exact tenant predecessor receipt)`). Original admission operation identity is SHA256(`"queue-admission:" || B32 allocationIdentity || B32 originalAdmissionWaitHash`). The backend's authenticated current receipt binds the complete installed authority, exclusive owner and generation to those recorded identities. A lost acknowledgement resolves that original receipt through the stable addressed authority, not a process `pending` or `receipts` map. Rollback authenticates the original owner's allocation receipt, subject, ticket and predecessor; a conflicting caller cannot release legitimate interests. Issued tickets remain in the deployment allocator after cleanup and are never reused.

`HX-EV-PIN-QUEUE-OWNERS-1\0 || 01 || 0008` (4 MiB) is one CAS index beside each queue: `01` U deployment; `02` U counter ID (maximum 1,031 bytes); `03` N generation; `04` N owned-interest count; `05` B rows `N ticket || B32 stable subject || B32 current authority hash`, sorted by unsigned ticket then raw subject; `06` B32 predecessor index hash; `07` Q UTC; `08` N authenticated ceiling. Count is at most 50,000 and rows have unique positive tickets/subjects. At 72 bytes per row, maximum rows occupy 3,600,000 bytes, below 4 MiB including maximum identifiers/header. This index serializes actual owners; queue tag 07 remains only a count. Both indexes reference the same authentic authority generation and agree with its residence, each row/reservation count, immutable tenant and allocator ticket. The deployment owner index enumerates **every** active/cleanup authority before any wait exists and supplies each tenant directory address; no store scan is needed. Tenant directories bootstrap in the allocation transaction and delete/read back at their last owner's final cleanup. D11 tenant onboarding bounds the set of potential tenants; any directory/charge capacity refusal occurs before allocation/commit.

`HX-EV-PIN-QUEUE-PREDECESSOR-1\0 || 01 || 000b` (74 MiB) is one fixed authenticated source slot per directory: `01` U deployment; `02` U counter ID; `03` N intended successor generation; `04` B exact previous queue bytes (64 MiB); `05` B exact previous owner-index bytes (4 MiB); `06` B exact intended successor owner-index bytes (4 MiB); `07` B32 intended successor queue hash; `08` B32 intended owner-index hash; `09` B32 predecessor source-slot hash (zero only at successor generation 2); `0a` Q transaction UTC; `0b` N global allocator ticket selected by this transaction. Previous queue/index generations both equal tag 03 minus one; intended owner generation equals tag 03. Maximum embedded images occupy 72 MiB; all maximum-width header/fields/receipts fit within 74 MiB. A hash at a replaced queue head is not a source. This exact source is at D11.3's physical predecessor address, authenticated by its current typed native receipt. One source replaces its predecessor only in the same serializable transaction that installs/read-backs the complete queue/owner successor; unavailable predecessor/deletion/readback or any u64 generation maximum aborts the entire mutation. There is no accumulating version log. Genesis generation 1 needs no predecessor; a corrupt genesis cannot contain committed waits and fails readiness without fabricating one.

`HX-EV-PIN-QUEUE-RECEIPT-1\0 || 01 || 0008` (1 KiB) declares the bounded native provider fixture: `01` U complete addressed object key (maximum 128 bytes); `02` B32 owner subject (zero for directory/counter heads); `03` N installed generation; `04` B32 exact installed/deleted object hash; `05` B32 previous receipt hash; `06` Q commit UTC; `07` U action (`present` or `deleted`); `08` B32 provider authentication root. The actual qualified provider supplies equivalent authenticated authority; D12's `fixture-queue-provider:` hash is only a local deterministic signing fixture. Exactly one current receipt occupies `K("HX-EV-PIN-QUEUE-RECEIPT-KEY-1", U deployment, U completeObjectAddress)`; each authority retains its two original predecessor receipt bytes within its existing 16 KiB cap. Receipt replacement and original-receipt readback precede reclamation; deleted-wait evidence stays at its one fixed native slot during cleanup, then deletes atomically with authority/index release/refund. No unbounded receipt dictionary is durable authority.

Before allocation, D7 atomically admits one 40 KiB `side-record` reserve per owner: 16 KiB authority (including carrier and original receipts), 4 KiB wait, 4 KiB D7 charge, two maximum queue rows (1,078 bytes each), two 72-byte owner-index rows, and the remaining 14,084 bytes for current authority/wait/charge/deletion native receipts and support envelopes. This replaces the earlier unproved “CAS metadata” allowance. Directory bootstrap separately reserves `64 + 4 + 74 = 142 MiB` plus 32 KiB of canonical support storage, covering current queue, current owner index and one predecessor source, up to four complete 4 KiB counter bodies, the 4 KiB charge body and bounded current native head/charge/counter receipts. The head/support framing fits the remaining allowance. This covers the maximum-width counter envelopes above; a short-ID probe cannot establish the budget. Its components each remain below D7's 193 MiB side-record ceiling. Every fresh owner storage charge is `40 KiB + o`, and every fresh directory storage charge is `142 MiB + 32 KiB + o`, using the authenticated creation capability. The fixture uses actual 1 MiB overhead, once per owner or directory charge. Counter contributions include these exact charged amounts. Readback, movement, repair, replacement, cleanup and refund retain each original charge's canonical length, capability revision and recorded overhead; a later capability changes only fresh charges. Native receipts are covered within those canonical envelopes and do not create an extra overhead charge. Missing original capability authority holds unchanged. Exact retry adds no charge; authenticated cleanup subtracts only the original D8 contribution and preserves unrelated usage. Tenant directories use their tenant account; the deployment directory uses a distinct deployment capture-scope account, never an alias for a legal tenant. All charges/counters use D7's codecs, exact account ownership and physical addresses; the local ledger fixture has only these D8 contributions, while a production transaction preserves all other authenticated ledger usage. Admission authenticates the active encoded capability and every current account/pool/deployment predecessor, preserves unrelated authenticated usage by adding/subtracting only D8 contributions, and preflights all resulting bytes/counts/generations before allocating a ticket. Tenant storage, deployment-minus-reserve tenant-pool, unidentified and deployment ceilings all apply to owner and directory storage. Lowered ceilings preserve existing usage and forbid any increase beyond the new bound. Waiting work reserves side/evidence storage only and owns no pin/publication quota capacity.

Cleanup first authenticates/removes/read-backs the wait (or proves its original absence), keeps both interests, authority, charges and deletion receipt indexed in `cleanup`, then atomically removes/read-backs both directory interests, authority and its receipt and refunds the exact D7 charge/counters once. A partial deletion or unavailable refund remains charged/discoverable with deterministic exact retry; absence after complete cleanup is a no-op and cannot refund again. A successful D7 batch grant uses the same removal/readback fence before pin installation. Whole-tenant erasure authenticates operation erasure first, cleans every tenant owner through the same phases, removes its empty directory/source/receipts/charge and zero-usage counter only after readback; the deployment allocator is retained. Whole-deployment erasure removes the remaining bootstrap directory/source/ledger authority after all obligations close. Every new family activates before the first slice-4 wait producer; precommit preparations are discoverable through their paired owner indexes and the original D11 hold interest. At least every 60 seconds the quota coordinator activates allocated/admitted/cleanup owners, completes original admission or authenticated cleanup, and retries queued/parked capacity without losing fairness.

Corruption repair reads the fixed predecessor source and authenticates its previous directory bytes, intended owner manifest and expected current queue/index receipt generations/hashes. It resolves **every** intended subject to its actual addressed authority/current wait or deletion receipt, full D7 charge/account/counters and original allocation/admission authority. Missing, unavailable, stale, wrong-kind/owner, modified predecessor or changed wait refuses with byte-identical legitimate interests. Reconstruction computes the one exact intended queue from those sources, with unchanged original tickets/reservations, correct holding counters, canonical sort, selected global allocator head and current generation. It compares both exact intended hashes and performs a provider-authenticated repair/readback of that same installed version; no new logical admission, ticket, generation or quota change occurs. Cross-directory moves and source-slot replacement share the ledger transaction, so a crash commits either both old residences or both successors; lost acknowledgement reads both exact successors. The one fixed source/receipt is reused only after complete successor readback and erases with its directory. Authenticated empty-deployment erasure first proves that the owner index is empty, deletes/read-backs its queue/index/source and native receipt slots, then refunds the directory charge/counters in the same ledger transaction; a generation/refund refusal preserves the complete pre-erasure bytes. D12 exercises persisted-only restarts rather than serializing process dictionaries.

## D9. Publication resume — replacement for `[I-45]` and amendment to 6.5c C2/C5

Publication resume re-arms only unresolved publication of already committed events. It never submits to MediatR, invokes domain code, creates or rewrites a command/event MessageId, recreates an accepted member, changes a pin/outbox/batch root/committed result/first response, or resends an accepted member.

### D9.1 Discoverable precondition and authority

Every eligible hold inventory item exposes an opaque stable `resumeHandle = "hxrsm1-" || lowercase-hex SHA256(U tenant || U execution identity || B32 hold-source hash)`. Authorized Operators can read `GET /api/v1/admin/publications/tenants/{tenantId}/{resumeHandle}/precondition`; the response is support-safe `{ resumeHandle, eligibility, expectedHoldSourceHash, headHash, nextResumeOrdinal, predecessorAuditHash, expiresAt }`. It comes from one authenticated current-head read and is never an execution capability. A deployment route does not stand in for a tenant.

`POST /api/v1/admin/publications/tenants/{tenantId}/{resumeHandle}` takes `{ "expectedHoldSourceHash": "<64 lowercase hex>", "idempotencyKey": "<1..128 ASCII visible bytes>", "reason": "<1..512 UTF-8 bytes>" }`. Its canonical caller carrier is `HX-EV-PUBLICATION-RESUME-CARRIER-1\0 || 01 || 0005` over `01` U tenant, `02` U resume handle, `03` B32 expected hold-source hash, `04` U idempotency key, and `05` U reason. `stableRequestIdentity = SHA256("HX-EV-PUBLICATION-RESUME-IDENTITY-1\0" || 01 || U tenant || U resumeHandle || U idempotencyKey)`; the exact carrier hash separately binds expected source and reason, so changed bytes under the same caller key conflict. Server time, ordinal, and provider observations are deliberately absent. The Admin server authenticates tenant and Operator policy, first resolves that stable identity through the current live row, orphan audit, or expiry tombstone, then rereads the precondition and signs purpose `2d` claim `HX-EV-PUBLICATION-RESUME-3\0 || 01 || 000f` (at most 4 KiB): `01` U operator-action issuer, `02` U tenant, `03` U execution identity, `04` B32 ScopeOpHash (zero for legacy), `05` U eligibility (`retry-exhausted`, `drain-limit`, `drain-limit-and-retry-exhausted`, or `legacy-publish-failed`), `06` B32 current hold source or D10 capsule hash, `07` B32 latest A8 head (zero for legacy), `08` N next ordinal, `09` B32 predecessor successful audit hash, `0a` U resume handle, `0b` B32 stable request identity, `0c` B32 exact caller-carrier hash, `0d` U operator subject, `0e` Q request UTC, and `0f` Q expiry UTC no more than 15 minutes later. The caller need not discover internal ScopeOpHash, ordinal, or audit key separately; the server supplies and signs them from the same read. Stale fields fail before mutation. New admission decodes the real carrier, checks every declared bound, recomputes its stable identity and exact hash, and compares tenant, handle and expected source to authenticated current evidence. An availability label is not source authority. Mismatches conflict without mutation; live/orphan/tombstone lookup still precedes current-source or expiry preconditions.

The same stable identity with byte-identical carrier is one exact retry. A live result or orphan audit returns/completes that result without allocating an ordinal, window, charge, audit, or invocation. One unresolved orphan fences new resume admission for that execution until its recorded successor completes; the retained signed claim, audit, and staged charge remain bounded and discoverable under their original reservation even if the request expiry passes during recovery. A retained expiry tombstone returns `resume_request_expired`. The same identity with different carrier bytes is `resume_request_conflict`, even after the live/audit body is reclaimed. A genuinely new request uses a new idempotency key and stable identity.

The eligible states are an unresolved class-01-at-maximum set, an active D3 drain-limit record, or a verified D10 legacy capsule. Pending/unknown without an active drain-limit record, C5 terminal pointer, accepted/not-applicable head, `CommandOutcomeHold`, and D5 membership hold are ineligible.

Before any mutation, the coordinator decodes the committed member roster by position and MessageId without map/dictionary collapse. Positions and MessageIds must each be unique. `accepted` and `unresolved` must be disjoint, duplicate-free, contain no unknown position/MessageId, and their exact union must equal the committed roster. Any duplicate, omission, overlap, unknown member, position/MessageId disagreement, or changed bytes is `resume_evidence_hold`; it writes no closure, audit, state, charge, or invocation.

### D9.2 Window namespaces and the C5 fence correction

C5's whole-operation producer disable and cross-destination broker reject fence remain permanent **only for terminal closure**. They continue to block all current and future send IDs before the duplicate path. Resume does not install or weaken that fence.

For retry exhaustion, resume closes only publication window `w`. The broker namespace and every accept index include `(tenant, ScopeOpHash, window, member position, send ID)` for new window-aware evidence. It installs a window-scoped producer disable/reject fence that blocks every current/future send ID in window `w`, reconciles all sends in that window, and takes a final empty observation. A later window is accepted only with a verified D9 window claim and while the whole-operation terminal fence is absent. Delayed IDs from a closed window still fail; a window claim cannot bypass the terminal fence.

`HX-EV-PUBLICATION-WINDOW-2\0 || 01 || 000d` (at most 16 KiB), signed under purpose `2d`, has `01` U tenant, `02` B32 ScopeOpHash, `03` U original OperationId, `04` N window (zero is admission; resume opens positive values), `05` B32 predecessor window-closure hash (zero at window 0), `06` B32 exact **prior** resume-state hash (zero at first state), `07` B32 stable request identity (zero at admission), `08` B32 unresolved member-set root, `09` B32 immutable retry-policy hash, `0a` N drain-limit base, `0b` U operator-action issuer, `0c` Q opened UTC, and `0d` B32 active capability hash. It contains no successor-state hash: the exact prior state, request identity, closure, and member evidence construct the window claim independently; only the later successor state points to the resulting window-claim hash. For window greater than zero, each C2 purpose-`1c` send parent is accompanied by a backend-authenticated window binding over its exact parent hash, send ID, member, MessageId, window-claim hash, and broker namespace key. C2's signed maximum applies independently within a window; flat terminal attempt ordinals are `(window, member-local ordinal)`, never reset without the closure chain.

`HX-EV-PUBLICATION-WINDOW-CLOSURE-3\0 || 01 || 000b` (at most 64 KiB) has `01` U tenant, `02` B32 ScopeOpHash, `03` N closed window, `04` B member rows, `05` B32 window broker reject-fence receipt, `06` B32 window producer-disable receipt, `07` B32 final empty-state root, `08` B32 complete window attempt-set root, `09` U C5 AuthMode, `0a` B32 predecessor window-history accumulator, and `0b` Q closure UTC. Tag `04` is explicitly `u32 rowCount ||` rows sorted by position, each `u32 position || N final local attempt || B32 last definitive result hash`; the count controls exact parsing and trailing bytes fail. It never contains its successor. After exact closure and broker-authentication bytes read back, `successor = SHA256("HX-EV-PUBLICATION-WINDOW-HISTORY-1\0" || 01 || B32 predecessor || B(exact closure bytes) || B(exact broker-authentication bytes))`; only the successor D9 state stores that value. The closure key is `publication-window-closure:` plus lowercase-hex SHA-256(`"HX-EV-PUBLICATION-WINDOW-CLOSURE-KEY-1\0" || 01 || B32 ScopeOpHash || N closedWindow`). The closure is invalid if any accepted member would be retried; accepted rows remain in the overall outcome unchanged.

The complete attempt authority is **not** tag `04`'s final summaries. `HX-EV-WINDOW-ATTEMPT-SET-1\0 || 01 || 0008` is at most 64 MiB: `01` U tenant, `02` B32 ScopeOpHash, `03` N window, `04` B32 immutable committed-roster root, `05` N evidence-row count, `06` B exact rows, `07` B32 semantic root and `08` Q seal UTC. A row is `u32 position || N member-local ordinal || N observation ordinal || U kind || B32 exact registration-parent hash || B32 send-ID hash || B32 exact evidence hash`; kind is exactly `register`, `unknown` or `result`. Sort is the first three unsigned numeric fields. Each `(position, local ordinal)` starts with observation 0 registration, contains at most one Unknown observation followed by the definitive result, in contiguous observation order (two or three rows including registration), and ends with its definitive result. Every local ordinal from 1 through the final member ordinal is present; registrations/results cannot disappear because a later result exists. All rows in that local attempt bind the same exact parent and send ID, whose authenticated imported C2 bytes bind the committed position and MessageId. The root is SHA256(`"HX-EV-WINDOW-ATTEMPTS-2\0" || 01 || U tenant || B32 ScopeOpHash || N window || B32 rosterRoot || N rowCount || B(exact row bytes)`). Final summaries must exactly equal the last definitive result of every member in that window's exact immutable admitted-member set; missing or extra member coverage fails. A closure decoder obtains/authenticates this addressed set and all referenced C2 record readbacks; missing, changed or incomplete authority holds before closure, history, window or audit acceptance.

The immutable set address is `K("HX-EV-WINDOW-ATTEMPT-SET-KEY-1", U tenant, B32 ScopeOpHash, N window)`. Active collection uses existing generation-fenced C2 registrations/results and their authenticated observation ordinals, not an uncharged secondary log. D7 pre-reserves the worst case: at most 59 admitted members, the unchanged signed C2 attempt maximum (never greater than 64), and at most one Unknown plus registration/result per attempt, hence at most 11,328 rows. Each fixed digest row is at most 128 bytes, so the exact row ceiling is 1,449,984 bytes; the 64 MiB family ceiling includes its header. Byte-identical repeat readback is the same observation, not another row. At any collection bound, no further send or distinct Unknown registration is admitted; the indexed `ResumeAttemptCollectionHold` (`resume_evidence_hold`) retains all existing bytes and charge until the coordinator obtains authoritative definitive-result/closure evidence or operation erasure completes. It never truncates the set. Slice 4 activates collection; the complete sealed set and referenced evidence remain charged until the owning closure/history readback permits reclamation or scope erasure. The D12 1,691-byte known answer has four actual registrations, four Unknown observations and four definitive results; changing an earlier registration changes the root while the final summary remains identical.

This explicitly amends C5: its terminal roster/attempt verifier consumes the current window's complete evidence plus the authenticated accumulator/count of earlier closed windows. C5 terminal closure still uses the whole-operation fence and then seals that accumulator. A window closure never satisfies terminal closure on its own. C2/C5 consumers must resolve the complete set rather than hashing final summaries.

### D9.3 Bounded state, charge, and decision

Window tag `08` commits the immutable admission-time member set for that window: SHA256(`"HX-EV-PUBLICATION-UNRESOLVED-1\0" || 01 || u32 admissionCount ||` rows `u32 unsigned position || U MessageId || B32 SHA256(exact committed member bytes)`, sorted by unsigned position). Successful sends change current accepted/unresolved progress without rewriting that claim or its admission root. The authenticated current unresolved set must be a subset of that exact admitted set; previously accepted members remain accepted, and the current accepted/unresolved partition is exact, disjoint and duplicate-free against the committed roster. Invocation tag `07` hashes only the current unresolved members using the same framing. Drain-only continuation preserves the original claim bytes/hash and rearms only that current unresolved subset. Retry exhaustion closes the current window and constructs the successor claim's admission root from that current subset. Equivalent reordered input is canonicalized before hashing. Closure summaries select, for each member in that window's exact immutable admitted-member set, the greatest local ordinal's last definitive result, then sort by position; earlier results remain in the separately authenticated complete set and do not increase the summary count. A complete set with several local attempts is valid authority for actual closure/history/window/audit/state construction.

The coordinator retains actual bounded admitted-member and current-progress authority under the existing charged active window and authenticated provider readback, addressed by the exact window-claim hash within its execution.

Every new direct resume authenticates the window claim's tenant, ScopeOpHash and window against the current execution before using admission/progress. This is the same binding reconstructed after restart. A self-consistent rehashed claim for another tenant, scope or window is `resume_evidence_hold` without mutation. Live/orphan/tombstone exact-identity lookup still precedes current eligibility. After authenticated current progress partitions the committed roster, a new identity with no unresolved member is `resume_not_eligible`; the immutable original admission set cannot create new success after all members were accepted. Consumed hold/source authority likewise refuses a fresh identity unchanged. The immutable admission blob is `u32 count ||` those rows, at most 62,780 bytes for 59 maximum-width members within 64 KiB. Current progress binds SHA256 of that blob, previous accepted rows, current accepted rows and current unresolved rows, each `u32 count || B exact row bytes`, plus authenticated current-version provider authority. Prior accepted is a subset of current accepted; current accepted plus unresolved equals the committed roster. At the same roster bound this is at most 125,640 bytes within 128 KiB. The actual provider must authenticate scope, claim, owner and contiguous progress/CAS readback; D12's named native source slots and deterministic authentication hash are only qualified-provider fixtures, not shipped record guarantees or additional replacement codec/key families. These complete sources fit within the already charged active-window reservation and remain discoverable without process caches. Preparation retains their exact prior/successor manifest roots and verifies actual native source reads again on restart. Missing, unavailable, stale, changed or regressing authority fails closed with unchanged bytes/counters. A successor window installs/reads its own exact admission and progress sources with its claim. Closed prior sources remain while a full origin can need reconstruction, then delete/read back only with authenticated successful-origin compaction or identity-wide deadline reclamation; active sources remain until window closure/history reclamation or scope erasure. This prevents an accumulating closed-source log.

Window closure requires exactly the immutable members admitted to that window, including any admitted member accepted afterward. Earlier accepted members retain their prior accepted/history authority and have no synthetic current-window send. For each admitted position it resolves the complete last definitive-result chain; a missing member, extra/unadmitted position or changed MessageId refuses. Closure's counted summary contains exactly one final result per admitted member, sorted by committed position; committed-roster identity remains bound separately. Initial and successive windows, a two-of-three admission, later acceptance and persisted closure readback exercise this same consumer.

Each complete attempt resolves the original authenticated C2 purpose-20 parent-member registration and purpose-22 nonce chain. That authority binds tenant, ScopeOpHash, window, admitted member position/MessageId, exact parent row/send identity, nonce, per-ID attempt ordinal and complete registration/Unknown/definitive-result evidence. Distinct parent-member send rows require distinct send IDs; authenticated retries under one send ID remain legal. Per-member send rows and each per-ID nonce ordinal are contiguous; window-local ordinal equals the sum of attempts in preceding send rows plus that per-ID ordinal. Physical `(send ID, nonce)` attempt keys and registration evidence are distinct. Cross-member/window/ordinal reuse, absent authority or mismatched observations refuses even after a collector recomputes its own row root.

Preparation reads these bounded original C2 records through their existing addressed native provider source. It copies no C2 readback image into the 2 MiB preparation slot and adds no public record or key family. Existing C2/A8/window reservations retain and charge that source under their original bounds; a window resolves at most 59 × 64 nonce attempts. D12's canonical native fixture row is at most 16 KiB with 1,024-byte MessageId and the complete three-observation chain; it represents original source authority only. Its qualified-provider locator and authentication hash are local fixtures, not replacement production codecs. Full-origin reconstruction freshly authenticates required source rows without process caches. Successful-origin compaction leaves C2-owned authority untouched; existing window-history/obligation closure reclaims it, and authenticated whole-tenant erasure removes it with original source charges. Missing/changed/unavailable native readback holds unchanged. The legal 3,776-attempt maximum deliberately exceeds 2 MiB in C2 source bytes while the preparation slot remains bounded.

`HX-EV-PUBLICATION-RESUME-STATE-3\0 || 01 || 000e` (at most 32 KiB) is the single CAS head for an execution: `01` U tenant, `02` U execution identity, `03` B32 ScopeOpHash, `04` N successful resume ordinal, `05` N active window, `06` N active drain limit, `07` B32 current hold/source hash, `08` B32 active window-claim hash, `09` B32 window-history accumulator, `0a` N closed-window count, `0b` B32 last successful audit hash, `0c` B live-retry rows, `0d` B expiry-tombstone rows, and `0e` Q update UTC. Tag `0c` is `u32 count ||` rows sorted by ordinal, each `B32 stable request identity || B32 exact caller-carrier hash || N ordinal || B32 audit hash || N returned window || N returned drain limit || Q request expiry`. Tag `0d` is `u32 count ||` rows sorted by stable identity, each `B32 stable request identity || B32 exact caller-carrier hash || Q request expiry || Q delete-after UTC`. Live plus tombstone rows are at most 64. At request expiry a live row moves, in the same state CAS, to a tombstone whose delete-after is exactly 30 days later; audit and closure bodies may then be reclaimed. Hourly reconciliation deletes only an authenticated expired tombstone. A 65th distinct success is `resume_capacity_hold` with no mutation until the oldest tombstone's deterministic delete-after or operation erasure. Exact live retries remain answerable, exact retained-tombstone retries remain distinguishable as expired, and changed carrier bytes conflict throughout that bounded horizon. The tombstone deletion transaction deletes and reads back every stable-identity-addressed claim, origin, reconstruction, audit, retained invocation and progress/import link atomically with that tombstone. No orphan lookup can outlive its identity fence. Key reuse after that authenticated deletion is a fresh request with fresh server UTC, expiry and ordinal; before deletion it remains expired or conflicting.

Before mutation, the ledger stages one new worst-case `resume-window` charge for every unresolved member's full next-window attempt evidence, new drain rows, the **next** drain-limit record and resolution, window claim/closure, state, audit, tombstone capacity, and hold/inventory evidence. It is at most 1 GiB and must fit tenant, tenant-pool, and deployment counters **in addition to** the unchanged old active charge. The staged charge binds the stable request identity and all three predecessor counters. The old charge remains authoritative until successor-state readback. Refusal writes no closure, audit, state, invocation, or partial counter and returns `resume_capacity_hold`.

Charge ownership is a closed two-phase swap. Before a successful audit or successor readback, an authenticated proof that neither exists permits recovery to CAS-release only the staged generation, leaving the old active generation/counters exact. A read-back success audit instead makes its staged charge and recorded successor intent authoritative partial success: recovery retains that stage and completes the exact state/charge without another audit or ordinal. After successor readback, recovery CAS-activates the staged generation and releases/decrements the old generation exactly once. Generation and transfer-owner checks make repeated recovery a no-op; no point counts less than the old charge, admits above a ceiling, or releases either generation twice. Publication arms only after the finalized successor charge reads back. Finalization preflights and stages the remaining successor, progress, charge, invocation and inventory generations and native receipts together before releasing the old charge. A refusal at successor readback, later expiry reconciliation, invocation or completion discards the entire staged finalization image, including all charge and counter generations. Controlled interruption commits only a fully checked phase boundary; restart reads the same immutable intent. The original predecessor is its authenticated exact byte image and update UTC, which may precede request UTC; audit and preparation roots hash those original bytes. A later permitted expiry CAS advances current UTC without rewriting the original predecessor or audit.

`HX-EV-PUBLICATION-RESUME-AUDIT-4\0 || 01 || 000a` (at most 4 KiB) is written only for a successful resume: `01` U tenant, `02` U execution identity, `03` N ordinal, `04` B32 stable request identity, `05` B32 exact caller-carrier hash, `06` B32 prior resume-state hash, `07` O(B32) window-closure hash, `08` N opened window (zero for legacy), `09` N new drain limit, and `0a` Q decision UTC. It never contains the successor state hash. Its create-once key is `publication-resume-audit:` plus lowercase-hex SHA-256(`"HX-EV-PUBLICATION-RESUME-AUDIT-KEY-2\0" || 01 || U tenant || U executionIdentity || B32 stableRequestIdentity`). The same stable identity therefore locates a live row or orphan audit without reconstructing a server timestamp or ordinal. Rejected requests create no durable record and consume no quota.

A successful resume consumes its exact active hold/source: successor tag `07` is zero, and the same serializable state/readback transaction removes the exact active inventory entry while preserving unrelated entries. Required audit and window/resolution readbacks precede this transition; retained preparation continues to own completion until finalized charge/invocation readback. Live, orphan and retained-tombstone identity lookup precedes current hold preconditions, so byte-identical retry still returns its recorded result or expiry without a second audit/ordinal/window/charge/invocation. A new identity targeting the consumed source refuses unchanged, even with a freshly read current head. A later genuine exhaustion creates a new authenticated nonzero source and active entry and can admit a fresh resume.

The acyclic write order is: resolve stable identity against live/tombstone/audit evidence; read/authenticate prior state and exact partition; stage/read the successor charge; retain/read back the exact signed D9 request claim at its create-once address; write/read any drain resolution and closure; compute the successor accumulator; for retry exhaustion construct/write/read the window claim from the **prior** state hash and stable identity (drain-only reuses the exact existing claim); create/read the audit against the prior-state hash; CAS/read the successor state containing the window/audit hashes and compact retry row while consuming the active source/inventory atomically; finalize the charge swap; then remove the preparation inventory and arm publication. The existing bounded signed request claim supplies the exact expiry, resume handle, and source to orphan reconstruction; it is included in the staged reservation, retained through its live retry horizon, and erased with the operation. Before audit creation, recovery may roll back only with authenticated absence of both audit and successor. A crash after audit readback but before state CAS leaves a partial success: the byte-identical stable retry or authenticated hourly owner completes its recorded successor and charge, then returns the same canonical response. Changed carrier bytes cannot finish that success. A crash after state readback deterministically finalizes before arming. State and audit never hash each other, and window and successor state never hash each other. Orphan completion and hourly retry reconciliation use this same CAS head. New resume admission is fenced while an orphan exists; the only permitted intervening index changes are authenticated live-to-tombstone expiry and tombstone deletion. Recovery verifies the audit/owner and every protected prior-state field, recomputes these deterministic expiry changes from the protected prior rows and authenticated head update UTC/CAS receipt, and applies only the recorded success delta to the current head. It preserves current live/tombstone rows, inserts its own exact success row, and immediately tombstones that row if its recorded expiry already passed. CAS loss repeats against fresh authenticated evidence; arbitrary added/changed rows or altered source/carrier/receipt hold unchanged. Recovery never reinstalls the obsolete whole snapshot.

For retry exhaustion, the transaction closes `w`, creates the successor window claim/state, and then arms only unresolved members. For a drain-limit-only hold it first writes D3's exact resolution linked to the old limit record and constructible successor invocation evidence, proves the window claim **bytes and hash**, committed roster, accepted set, unresolved set, and member bytes unchanged, leaves the window, closed-window count, and member set unchanged, increases the limit by the original bounded drain reservation, and records the exact re-armed invocation hash under that unchanged claim before invoking it; combined `drain-limit-and-retry-exhausted` eligibility binds the drain-limit source plus the authenticated failed head, writes the drain resolution with tag 05 equal to SHA256 of the exact constructible successor `HX-EV-PUBLICATION-WINDOW-2` bytes. That claim carries the closure hash, original prior resume-state hash, stable request identity, checked next window, new drain limit, unresolved root and original authorization UTC; it excludes the resolution hash, so the link is acyclic. The coordinator computes those bounded bytes from authenticated predecessor/attempt authority before the resolution write and checks the same hash on window readback, then closes the old window and opens exactly one successor in that same resume. A later drain-limit is already included in the charge. Active hold inventory removal shares the successful source-consuming state/readback transaction; exhaustion again creates a fresh source/entry.

Ordinal, window, closed-window count, drain limit, and every charge addition use checked u64 arithmetic. An increment from `2^64-1`, a zero/overflowing drain increment, or a limit sum above `2^64-1` returns 409 `resume_arithmetic_exhausted`, leaves the old hold/state/charge authoritative, and arms nothing.

The closed endpoint outcomes are:

| Result | HTTP and reason |
| --- | --- |
| success | 202 `{ resumeHandle, resumeOrdinal, window, drainLimit, auditRecordHash }`; the bounded response uses canonical UTF-8 JSON with sorted names and no insignificant whitespace, so live/orphan retry returns byte-identical body bytes reconstructed from the retained claim and compact row/audit |
| stale/ineligible/expired/idempotency conflict | 409 concurrency Problem Details with `resume_hold_changed`, `resume_not_eligible`, `resume_request_expired`, or `resume_request_conflict` |
| checked ordinal/window/limit overflow | 409 concurrency Problem Details with `resume_arithmetic_exhausted` |
| quota refusal or an unresolved preparation/orphan owned by another identity | 503, `Retry-After: 30`, `resume_capacity_hold` |
| unavailable current evidence | 503, `Retry-After: 30`, `resume_evidence_hold` |
| historical legacy evidence absent/contradictory | 409, `legacy_resume_evidence_unavailable` |

The route and legacy eligibility activate in slice 3 alongside BC-02; evidence-required eligibility activates in slice 4. State and charge erase with the tenant after every publication/rollback/incident obligation closes. Known answers `D45-request`, `D45-window`, `D45-closure`, `D45-state`, and `D45-audit` appear in D12.

### D9.4 Persisted reconstruction and preparation lifecycle

An execution permits one unresolved preparation. A8/D7 reserve one 2 MiB operational preparation slot in the old active charge before enabling resume; the next active charge includes that same slot. For legacy work D10 reserves this separate 2 MiB `side-record` charge before drain cleanup or enabling its resume route; the capsule/chunk `resume-window` charge does not include it. It covers the bounded origin, reconstruction images and progress head through recovery or retained retry expiry, independently of the staged next-window amount. The coordinator's `PublicationResumePreparationHold` is keyed by execution and stable request identity, uses reason `publication_resume_preparation_hold`, and remains in D11 inventory during preparation, cleanup or evidence failure. Exact retry, restart and hourly reconciliation activate it. Missing authority holds; authenticated completion or deletion/rollback readback removes it. The slot and all records erase with the execution/scope; no released stage leaves uncharged preparation records.

The slot permits one full origin (256 KiB), one field-derived reconstruction of at most 304 KiB within its 1 MiB framing ceiling, one progress head (8 KiB), at most 64 retained signed claims (16 KiB each, including the exact authentication envelope) and at most 64 retained invocation records (4 KiB each): the total is at most 1,848 KiB, below 2 MiB. Each compact signed claim retains the exact existing purpose-2d payload and envelope at the existing claim address, with the same 8 KiB envelope ceiling and authentication. The provider-native authentication metadata is bounded at that same address: exact envelope bytes, owner, unsigned-payload hash and authenticated readback receipt, under the 16 KiB signed-claim reservation. Payload plus this metadata commit/read back and delete as one signed-carrier row; no separately addressed durable envelope exists. Full-origin reclamation requires exact signed-carrier readback first and preserves it until identity-wide deadline deletion or scope erasure. After authenticated successor/finalized-charge/invocation readback, a successful full origin/reconstruction can be deleted/read back while its compact signed claim and retry/invocation evidence retain the exact response/expiry. After complete unaudited cleanup/readback, rollback atomically compacts the original identity/carrier hash/expiry into one of the bounded 64 tombstone rows, with delete-after at original expiry plus 30 days, deletes the full origin/reconstruction/progress/import records, and frees the single preparation slot. That identity stays expired or conflicting; another caller identity can immediately prepare. If tombstone capacity or cleanup readback is unavailable, rollback retains its charged indexed origin and slot. Hourly authenticated identity-wide deletion at the fixed deadline, or scope erasure, removes the compact tombstone. Compact claim/invocation reclamation follows the existing bounded live/tombstone horizon, not an extended deadline. Unaudited rollback authenticates the current execution head immediately before its final CAS and composes only the original identity's bounded tombstone delta over that current head. Before rollback cleanup or compaction accepts a later current head, its complete canonical state bytes, including both bounded live and tombstone row sets and UTC, must equal the original authenticated predecessor after only deterministic expiry/deletion reconciliation at that UTC. Matching only the non-index fields or receiving an authentic CAS receipt cannot authorize changed, inserted or removed unrelated rows. The current UTC, unrelated live/tombstone rows, permitted deletions and native predecessor generation are retained exactly; an identity already past its fixed delete-after is not resurrected. An occupied 64-row head or exhausted generation retains the indexed preparation and refuses without partial cleanup/charge mutation. Lost acknowledgement resolves the current installed tombstone and once-only deletion receipts, never the protected origin snapshot.

The continuation's optional `invocation_owners` is a canonical tagged tuple of B32 stable request identities parallel to `invocations`; both sequences have the same length, at most 64, with unique owners. Each owner/hash mapping resolves the existing typed invocation's request identity, successful ordinal and exact invocation hash. Retained owner IDs must remain in the authenticated live/tombstone/preparation set. Reconstruction freshly reads the original 4 KiB typed invocation at its existing address and authenticates its original owner receipt; absent, changed, unavailable or mismatched owner/ordinal authority holds before successor mutation. These existing retained records consume the slot's already bounded invocation reservations; no new source family or copied invocation list is introduced. Old images without this field can derive the mapping only from an authenticated unambiguous retained live-result set and typed invocation readback; otherwise recovery holds. Authenticated successor/expiry/deletion reconciliation retains an invocation hash/owner only while its live retry, fixed-deadline tombstone or unfinished preparation still owns it. Reconciliation binds the retained pair of sequences by a fixed B32 `invocation_root`, avoiding a duplicate unbounded list. Unauthenticated reconciliation leaves both sequences unchanged. Identity-wide deadline reclamation deletes/readbacks associated claim/audit/invocation bodies once and releases their existing charge authority last; no retained owner can disappear before its exact-retry horizon. New source exhaustion after old identities close can therefore resume beyond 64 lifetime successes. D12 retains that historical continuation proof and additionally runs 67 genuine admissions against one retained authenticated backend. Every cycle installs and reads a distinct addressed exhaustion source, completes the same committed event identities, checks all surviving native receipts/charges/index rows, compacts the full origin, preserves the old exact retry through expiry, and reclaims the identity/resolution bodies exactly once at the fixed deadline. The 67th success interrupts after successor write and restarts with persisted bytes only; complete preparation and source rows remain within their original reservations.

`HX-EV-RESUME-ORIGIN-1\0 || 01 || 000b` (256 KiB) stores `01` U tenant, `02` U execution, `03` B32 stable identity, `04` B32 exact caller hash, `05` B exact carrier, `06` B exact unsigned request claim, `07` B protected prior continuation image (128 KiB), `08` N staged charge generation, `09` Q original server UTC `0a` Q original expiry, and `0b` B exact original purpose-2d authentication envelope (at most 8 KiB), retained inside this charged origin through the same restart/cleanup/erasure lifecycle. The exact signature is authenticated again on every restart; the local purpose-specific hash is only a signing fixture. The stage admission and origin installation share the ledger's owner CAS. A lost acknowledgement reads this exact origin; no retry creates a new timestamp, expiry, signature or ordinal at the existing claim key. Changed carrier bytes conflict. If stage/origin admission never committed, authenticated absence permits a fresh preparation. An unaudited rollback compacts its admitted origin into the bounded tombstone only after full cleanup readback; its expiry never extends. A retained compact rollback identity returns expired or conflict and cannot restage. Already authoritative fenced/audited work completes from the full retained origin.

`HX-EV-RESUME-PREPARATION-1\0 || 01 || 000c` (1 MiB) stores `01` U tenant, `02` U execution, `03` B32 stable identity, `04` B32 caller hash, `05` B32 prior-image hash, `06` B prior continuation image (128 KiB), `07` B successor continuation intent (256 KiB), `08` Q original UTC, `09` Q original expiry, `0a` N staged amount, `0b` B import/artifact manifest (128 KiB) and `0c` N recorded successful ordinal. It is create-once after all actual required artifact readbacks and before audit. Images use canonical UTF-8 JSON without whitespace, with explicit tagged `bytes` (lowercase hex), `tuple`, `list`, `map` (pairs sorted by canonical encoded key, duplicate keys forbidden), and `scalar` (null/string/integer/boolean); decoder re-encoding must equal the original bytes. The closed continuation projection contains tenant, handle, hold source, ordinal, window, closed count, limit, current/next charge and ceiling, bounded live/tombstone rows and their exact response fields, last audit/count, active claim hash, pending invocation hashes and their stable identity owners (each at most 64) and optional authenticated reconciliation evidence. It excludes process objects, orphan dictionaries and raw event bodies. Each closed continuation image is at most 128,832 bytes: two fully populated 64-row live/reconciliation sets at 768 bytes per row, 12,288 escaped tenant/handle bytes, two 5,120-byte bounded invocation hash/owner sequences and 8,000 fixed bytes. A reconciliation row carries the same bounded result fields and excludes duplicate response bytes, reconstructed from the handle/result. The closed manifest is at most 36,000 bytes, including a 16 KiB window represented as hex and twelve fixed B32 import roots. Two maximum images, that manifest and at most 2,200 bytes of outer fields total 295,864 bytes, below the 304 KiB charged reconstruction ceiling. Admission and reconstruction reject any unsupported shape or larger derived image before writing. The import manifest binds exact prior/successor roots for `roster`, `accepted`, `unresolved`, `window_claim_bytes`, `window_admission` and `window_progress`; imported member tuples are resolved from already charged immutable A8/outbox/pin authority for this execution and compared by position, MessageId and exact-byte hash. The manifest retains the actual new window claim (16 KiB) or unchanged drain-only claim. No image depends on the preparation's own hash; audit names only the original resume-state predecessor, and successor intent includes the already constructible audit/claim hashes.

`HX-EV-RESUME-PREPARATION-HEAD-1\0 || 01 || 000a` (8 KiB) stores `01` U tenant, `02` U execution, `03` B32 owner identity, `04` B32 origin hash, `05` O(B32) reconstruction hash, `06` U phase (`admitted`, `writing`, `cleanup`, `audited`, `completed`, `rolled-back`, `evidence-hold`), `07` N generation, `08` B32 predecessor hash, `09` B progress manifest (4 KiB), and `0a` Q update UTC. The progress manifest is `u32 count ||` at most eight rows `U artifactKind || U framedAddress || B32 exactBytesHash || B32 authenticated readback/deletion receiptHash || U disposition`, with address at most 128 bytes, kind from claim/resolution/fence/closure/window/audit/state/invocation, and disposition pending/present/deleted. Every write intent is indexed before the corresponding create-once write; every readback advances this checked-generation CAS head. Restart enumerates the head through D11, authenticates origin, preparation, imports and provider receipts, then reconstructs protected predecessor and exact successor intent from these bytes alone. It rejects unavailable, changed, stale or mismatched reconstruction unchanged. It authenticates any intervening index CAS against the entire canonical reconciled predecessor image, including both live and tombstone row sets, and composes only permitted expiry/deletion changes. An authenticated head with any other row mutation holds unchanged.

The ordered preparation phases are stage+origin, claim, required resolution/fence/closure, window, reconstruction, audit, successor, finalized charge, invocation. Before audit/successor **and before any irreversible window fence or closure**, rollback requires typed authenticated absence receipts for the exact owner/generation on both audit and successor, then authenticated deletion/readback of every written artifact in the progress manifest. Missing or partial deletion holds with both charges unchanged; cleanup resumes from its retained manifest after restart. Only complete deletion authorizes once-only staged release. Origin/head remain discoverable under the reserved operational slot through partial cleanup. Final rollback atomically compacts the bounded identity and deletes/readbacks the full preparation; that compacted identity cannot restage. Once any fence/closure or successful audit is authoritative, rollback is forbidden: the coordinator retains charges and completes that original preparation, including the audit and recorded successor. It never removes a permanent C5 terminal/window fence or recreates accepted members. Unavailable presence/absence is a hold, never absence.

Orphan completion uses authenticated **current** completion UTC, irrespective of a previous reconciliation timestamp. It inserts its own success row as live only before original expiry, as a tombstone through original expiry plus 30 days, and nowhere at/after deletion. It still completes the recorded success/charge once and returns the original canonical response; it neither resurrects nor extends a retry row. Successful live ordinals must be distinct and strictly increasing, in addition to distinct stable identities and bounds by the successful head. Drain invocation identity is SHA256(`"HX-EV-PUBLICATION-INVOCATION-1\0" || 01 || B32 unchangedWindowClaimHash || N successfulOrdinal || N newDrainLimit || B32 stableRequestIdentity || B32 unresolvedRoot`); its create-once key uses tenant, execution and that identity. Every invocation consumer uses this identity, so fresh drain resumes differ while exact retries reuse one invocation.

`HX-EV-PUBLICATION-INVOCATION-1\0 || 01 || 0009` (4 KiB) records `01` U tenant, `02` U execution, `03` B32 unchanged/selected window-claim hash, `04` N successful ordinal, `05` N new drain limit, `06` B32 stable request identity, `07` B32 unresolved root, `08` B32 invocation identity computed above, and `09` Q original authorization UTC. The unresolved root is SHA256(`"HX-EV-PUBLICATION-UNRESOLVED-1\0" || 01 || u32 count ||` sorted rows `u32 position || U MessageId || B32 exact committed-member byte hash`). The D9.4 slot retains invocation bytes/readback until successor/invocation readback and the relevant retry horizon permit reclamation; staged reservation includes this 4 KiB ceiling. Its progress row binds the exact address/hash before create-once installation. Restart resolves the roster and excludes accepted members through the retained imports; changed identity, count, member or ordinal cannot arm publication. For `legacy-publish-failed`, invocation tag 03 is zero and tag 07 is the capsule's ordered stored-event root; its identity uses those same two fields. Legacy preparation authenticates the exact D10 capsule manifest hash from request tag 06, every chunk at its manifest address, and the existing stored members by sequence, MessageId and StoredDigest. These already charged D10/stored-event readbacks supply the complete ordered range and rejection classification; window fence, closure and window phases are skipped. The independent A8-head, C2-attempt-set and window-broker prerequisites apply to window resume only. Legacy claim ScopeOpHash and A8-head fields are both zero; unknown eligibility refuses before preparation. Slice 3 enables legacy invocation and slice 4 window invocation; scope erasure removes it with the operation. Every consumer resolves this record and finalized charge authority before dispatch. An exact retry reads the existing record and arms no second invocation.

The progress intent's exact state digest and Q completion UTC fence successor installation. If a state write survives before acknowledgement, restart authenticates those original intended bytes and their provider readback, acknowledges them, and finalizes the original charge exactly once. It never regenerates later-UTC bytes at that pending intent or overwrites the manifest before rejecting contradictory evidence. After acknowledgement/finalization, the shared CAS composes current-time live expiry and fixed expiry-plus-30-day deletion against the authenticated successor, preserving unrelated authorized rows; state and manifest receipt links advance together. This reconciliation runs even for an already-present successor or finalized charge and reads back before publication arms or completion returns. Later UTC changes only the permitted retry indexes/update time, never the original claim, audit, response, authorization, invocation or expiry. Unavailable/contradictory reads are preflighted before mutation and preserve exact bytes/counters.

Both initial successor installation and later current-time reconciliation preflight state and progress native ownership, availability, expected predecessor and checked next u64 generation before mutating either. The local backend stages the exact state write/readback, active-source consumption and matching progress receipt/CAS, committing their coupled heads only after all staged reads and generation checks succeed. A maximum state or progress generation refuses byte-identically; the adjacent generation succeeds once and its next attempt refuses. An interruption after the coupled write retains the original intended successor bytes and matching receipt; persisted restart authenticates those bytes before any later-UTC reconciliation. The isolated successor-intent probe crashes after the pending digest/UTC reads back at 1000, restarts at 1001 or 2000 and stops after the coupled successor-write. It checks the originally recorded state bytes, hash, completion UTC and provider receipt before later expiry reconciliation; final-state equality alone cannot prove this invariant. A subsequent finalize composes the current-time live/tombstone transition while retaining original claim/audit/invocation authorization and expiry. D12's staged fixture proves local refusal atomicity only; implementing the equivalent provider transaction remains subject to the existing provider gate. Every later transferred charge generation, including released rollback and restaging, retains the original transfer owner; ordinary never-transferred charges retain absent owner.

## D10. Legacy status-6 resume — replacement for `[I-46]`


### D10.1 Resume capsule before cleanup

From slice 3, no legacy drain evidence for a terminal status-6 execution is removed until the actor creates and durably reads back a bounded chunk set and its `HX-EV-LEGACY-RESUME-CAPSULE-2\0 || 01 || 0010` manifest (at most 128 KiB): `01` U tenant, `02` U domain, `03` U aggregate ID, `04` U tracking identity, `05` O(U) execution MessageId, `06` U correlation ID, `07` U command type, `08` U rejection classification (`success-events` or `rejection-events`), `09` N start sequence, `0a` N end sequence, `0b` I positive event count, `0c` B32 ordered stored-event root, `0d` B chunk manifest, `0e` U cleanup source (`drain-exhaustion` or `operator-reconciliation`), `0f` B32 exact source-record hash, and `10` Q creation UTC.

`capsuleIdentity = SHA256("HX-EV-LEGACY-RESUME-CAPSULE-IDENTITY-1\0" || 01 || U tenant || U domain || U aggregateId || U trackingIdentity || B32 sourceRecordHash)`. Each `HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-1\0 || 01 || 0005` record (at most 64 KiB) has `01` B32 capsule identity, `02` N zero-based chunk ordinal, `03` N row count, `04` B concatenated rows, and `05` B32 chunk row root. Rows are ascending `N sequence || U stored MessageId || B32 StoredDigest`, with no nested count; tag `03` controls exact parsing. At the imported 1,024-byte MessageId maximum a row is 1,068 bytes and the fixed chunk overhead is 128 bytes, so each chunk holds at most 61 rows. Every supported V1 range of at most 1,000 rows therefore uses at most 17 chunks.

Capsule tag `0d` is `u32 chunkCount ||` 1..17 rows sorted by ordinal, each `N ordinal || N firstSequence || N rowCount || B32 exact chunk hash || N encoded chunk length || U resolvable chunk object key`. The chunk key is `legacy-resume-capsule-chunk:` plus lowercase-hex SHA-256(`"HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1\0" || 01 || B32 capsuleIdentity || N chunkOrdinal`); the capsule manifest key remains stable below. The ordered stored-event root is recomputed over `u32 totalRowCount ||` all exact member rows concatenated in chunk order: `SHA256("HX-EV-LEGACY-RESUME-EVENTS-2\0" || 01 || B(exact counted rows))`. Chunk lengths/counts/endpoints must exactly cover tags `09`..`0b`, with no gap, overlap, duplicate MessageId, missing chunk, or trailing bytes. The actor writes/reads all chunks first, then create-once writes/reads the manifest; any oversize or incomplete set fails cleanup and retains the drain record/reminder. A successful legacy drain needs no capsule and keeps its shipped cleanup.

The capsule create-once key is `legacy-resume-capsule:` plus lowercase-hex SHA-256(`"HX-EV-LEGACY-RESUME-CAPSULE-KEY-2\0" || 01 || B32 capsuleIdentity`); the resume handle additionally binds the manifest hash. It never uses concatenated text or MessageId alone, so expired status and cross-aggregate ID reuse cannot redirect it. Before cleanup, a separate active generation-1 2 MiB `side-record` charge reserves D9.4's preparation slot and inventory interest. The manifest, chunks, and object keys are separately charged together as never-transferred `resume-window` (transferred marker 0), retained until successful publication plus retry/incident obligations close, and erased with the tenant. Drain exhaustion publishes its dead-letter, then writes/reads chunks and manifest, then removes the drain/index/reminder. `IsRejection` comes from the drain record into tag `08`; a historical dead-letter without that authority cannot guess it.

For pre-slice-3 history, a privileged precondition may create the capsule only when an extant `UnpublishedEventsRecord` supplies range, correlation, command type, rejection classification, tracking identity, and exact events/MessageIds. A dead-letter may corroborate range/cause but cannot supply missing rejection classification. If no authoritative source exists, the inventory records `legacy_resume_evidence_unavailable`; resume fails closed and never fabricates bytes or executes the command. Evidence import is outside this story.

### D10.2 Exclusive recovery and re-arm

`HX-EV-LEGACY-PUBLICATION-RECOVERY-3\0 || 01 || 000d` (at most 4 KiB) has `01` U tenant, `02` B32 capsule manifest hash, `03` U resume handle, `04` N recovery generation, `05` N successful resume ordinal, `06` U owner (`legacy-resume` or `dead-letter-admin`), `07` U state (`claimed`, `draining`, `completed`, or `failed`), `08` B32 actor drain-record hash, `09` O(B32) Operations dead-letter record hash, `0a` B32 predecessor recovery hash, `0b` O(U) failure reason, `0c` O(B32) repaired-evidence hash, and `0d` Q update UTC. Failure reason is absent outside `failed` and otherwise exactly `transport-retryable`, `evidence-unavailable`, or `evidence-contradictory`; repaired evidence is present only on the next claim after an evidence failure. Its stable CAS-head key is `legacy-publication-recovery:` plus lowercase-hex SHA-256(`"HX-EV-LEGACY-PUBLICATION-RECOVERY-KEY-2\0" || 01 || B32 capsuleManifestHash`). Every write increments generation and authenticates the predecessor. The aggregate actor and Operations dead-letter Admin route both check/CAS this shared EventStore-backend fence. A drain-exhaustion dead-letter can no longer be generically retried around D9; its Admin action resolves to the resume precondition. Exactly one owner can recreate/drain the range.

After D9 succeeds, the actor rereads the immutable manifest, every addressed chunk, and stored events; recomputes every chunk hash/length, sequence, MessageId, StoredDigest, ordered row root, endpoints/count, correlation, command type, and rejection classification; proves no live drain/reminder/index owner exists; CAS-claims recovery; and recreates the shipped drain record for exactly that range with retry count zero and `DeadLettered=false`. Publication uses the stored event MessageIds. It never rewrites status 6 or command bytes. `success-events` must finish as shipped `Completed`; `rejection-events` must finish as shipped `Rejected`; a classification inversion is evidence contradiction. A second publication exhaustion reuses the byte-identical chunks and manifest and advances only the recovery generation and next successful D9 resume ordinal. It never attempts changed bytes at the create-once capsule key.

The transition graph is closed: creation is `claimed`; only its owner may write `claimed -> draining`; verified drain success writes `draining -> completed`; a typed transport or evidence failure writes `draining -> failed`. `failed(transport-retryable) -> claimed` requires a strictly greater successful D9 ordinal, the next recovery generation, and the unchanged capsule. `failed(evidence-unavailable|evidence-contradictory) -> claimed` additionally requires a present repaired-evidence hash whose authoritative readback recomputes every field above; otherwise it remains charged/indexed and re-evaluated hourly. A second exhaustion is `draining -> failed(transport-retryable)`, never a second capsule creation. `completed` is terminal until obligation closure/erasure. Every other edge—including `claimed -> completed`, `failed -> draining`, same-ordinal retry, changed capsule, generation skip, or any transition out of `completed`—is rejected without mutation. No owner may skip a state, overwrite a generation, or leave `failed` without its inventory owner and one of these exits.

A mismatch or unavailable read is `resume_evidence_hold`; missing/contradictory authority is `legacy_resume_evidence_unavailable`. Recovery and capsule activate in slice 3 and erase after closure/tenant erasure. Known answers `D46-capsule` and `D46-recovery` appear in D12.

## D11. Held delivery, quarantine, and inventory — replacements for `[I-36]` and `[I-37]`

### D11.1 Capture is mandatory and bounded

Every addressed-delivery subscription has a verified `HX-EV-SUBSCRIPTION-DELIVERY-POLICY-3\0 || 01 || 000d` record (at most 16 KiB): `01` U deployment identity, `02` U component, `03` U topic, `04` U physical subscription ID, `05` N policy revision, `06` B32 predecessor policy hash (zero at revision 1), `07` U relation (`initial` or `successor`), `08` U capture mode (`dead-letter-capture` or `direct-held-capture`), `09` O(U) dead-letter topic, `0a` N maximum live redeliveries `1..64`, `0b` B32 exact resolved subscription/resiliency configuration hash, `0c` U source (`dapr-configuration` or `broker-api`), and `0d` Q observation UTC. The create-once revision key and CAS head are specified in the address table below; only the contiguous revision selected by the head is active. Installing its successor first writes/read-backs the immutable `successor` revision and then CASes the head; the predecessor remains immutable and is logically superseded by that head. A stale or rolled-back revision cannot authorize acknowledgement. An unbounded broker redelivery policy alone is not sufficient: after the bounded local attempts or 24 hours from first observation, whichever comes first, the consumer must durably capture/read back the exact carrier and held state before acknowledging that transport copy. A dead-letter topic's own subscription uses direct held capture and cannot dead-letter again. The revision-address store is persistent and create-once: two differing successors at the same scope/revision conflict even before head CAS. Only the exact stored revision selected by the authenticated current head authorizes capture; exact lost-ack installation reads back the same revision/head, and stale or competing revisions never become authority.

A first held observation creates/read-backs `HX-EV-HELD-DELIVERY-4\0 || 01 || 0016` (at most 32 KiB) **before** its attempt number or 24-hour clock is used: `01` U scope kind (`tenant` or `deployment`), `02` U deployment identity, `03` O(U) tenant, `04` O(U) MessageId, `05` U component, `06` U topic, `07` U physical subscription ID, `08` B32 active subscription-policy hash, `09` U state (`observed`, `captured`, `redriving`, `incident`, or `closed`), `0a` U reason, `0b` N observed carrier length, `0c` B32 exact carrier hash, `0d` O(U) retained backend ID, `0e` O(U) resolvable retained object key, `0f` O(B32) authenticated object readback receipt hash, `10` O(B32) committed handoff hash, `11` Q first-observed UTC, `12` N delivery-attempt count, `13` N redrive count, `14` O(B32) last redrive-error evidence hash, `15` Q next re-evaluation UTC, and `16` N entry revision. `scopeKind=tenant` requires tenant present; `scopeKind=deployment` requires tenant absent. Every other pairing is a decode/admission incident with no acknowledgement or charge mutation.

Its key is `held-delivery:` plus lowercase-hex SHA-256(`"HX-EV-HELD-DELIVERY-KEY-2\0" || 01 || U scopeKind || U deploymentIdentity || O(U tenant) || U component || U topic || U physicalSubscriptionId || B32 exactCarrierHash`). Reuse of a physical subscription ID or identical bytes in another deployment, scope, component, or topic cannot cross-link authority. Every delivery CAS-increments tag `12` and the record revision; restart cannot reset either boundary. Locator fields are absent only in `observed`/`incident`, present together in `captured`/`redriving`/`closed`, and bounded to 1,024-byte backend ID plus 4,096-byte object key. Hashes alone are never restart redrive authority.

The record is charged 32 KiB before the first nonterminal response. For an ordinary carrier, D7 also creates an active generation-1 `retained-object` charge for the exact carrier length plus recorded overhead against the same tenant or deployment capture scope before object write; `captured` is legal only after the exact object, locator, readback authority, held entry, and active charge all read back. Oversize uses its distinct quarantine charge. Therefore no physical-copy acknowledgement can strand uncharged bytes. The captured transition is an explicit authenticated terminal handoff for the **physical transport copy**, so the consumer may acknowledge that copy; it writes no route success/effect/filter result, and all original logical route/handoff obligations remain open until ordinary terminal decisions. This amends 6.5c C4 without weakening its success rule. Capture authenticates the existing 32 KiB metadata charge's scope/account/owner/generation and the reserved D11 inventory slot against exact current readback, then the immutable retained object, locator and active object charge. A numeric charged-bytes assertion is insufficient; missing/stale/wrong-owner metadata or inventory rejects unchanged. The ordinary object locator is backend `held-delivery-store` with object key `held/` plus the lowercase hex held-delivery key hash, so the full scope and carrier identity own separate objects and charges; create-once rejects changed bytes at any existing locator. Exact retry rereads all original authority and adds no charge.

Permanently nonadmissible or invalid-header carriers use `HX-EV-CARRIER-QUARANTINE-2\0 || 01 || 000e` (at most 128 KiB): `01` U scope kind, `02` O(U) tenant, `03` U physical subscription ID, `04` U reason (`invalid-header-value`, `invalid-carrier`, or `oversize-carrier`), `05` N exact body length, `06` B32 body hash, `07` B header manifest containing only names, value lengths, and value hashes for forbidden values, `08` U retained backend ID, `09` U resolvable retained object key, `0a` B32 provider archive/readback authority hash, `0b` O(U) parsed MessageId, `0c` Q captured UTC, `0d` U disposition (`terminal-quarantine`), and `0e` B32 exact source-delivery receipt hash. No raw forbidden header value enters the record. The manifest is `u32 count ||` at most 128 rows `U headerName || N valueLength || B32 valueHash`; with the imported 64 KiB aggregate header-name/value maximum its exact worst case is `4 + 65,536 + 128*(4+8+32) = 71,172` bytes, safely within the 128 KiB record cap even with maximum surrounding identifiers.

The ordinary path is maximum-inclusive: complete carriers `<= 193 MiB` use ordinary retained capture. Oversize quarantine is the disjoint interval `193 MiB < length <= maximumQuarantinedCarrierBytes` (at most 256 MiB) and may acknowledge only after a broker/provider atomically archives its **exact** bytes under an authenticated non-expiring quarantine object and D7 charges kind `oversize-quarantine`. Provider readiness pre-rejects anything it cannot capture. A delivered object strictly above the advertised maximum creates the bounded charged D11 held record in `incident` state with reason `delivery_above_advertised_max`, exact streamed length/hash, Operations owner, hourly re-evaluation, and a D11 inventory entry before a repeated delivery can become invisible; it remains unacknowledged and exits only after provider configuration pre-rejects it and the broker proves no live copy, or after a later approved capture capability stores the exact bytes. Valid EventStore carriers cannot use the oversize exception.

`HX-EV-REDRIVE-REQUEST-2\0 || 01 || 0007` (3 KiB), signed under existing purpose `2d`, retains the existing exact seven fields: `01` U operator-action issuer, `02` U scope kind, `03` O(U) tenant, `04` B32 held-delivery key hash, `05` N expected redrive count, `06` U operator subject, and `07` Q request UTC. Its field-derived maximum is 2,409 bytes: 27 header bytes, seven tags, issuer 1,028, tenant-scope plus optional tenant at most 1,039 (deployment scope carries no tenant), held key 32, count eight, subject 260 and UTC eight. Authenticate the authorized issuer and operator subject under the existing Admin/Operator policy, purpose `2d` signature, exact scope/tenant/held key, and UTC no earlier than first observation before admission; an automatic action supplies the same authenticated server authority, not unsigned synthetic request bytes. Tag `05` must equal the current authenticated held count, which the transaction checked-increments. Keep the exact original signed bytes/UTC on restart; do not regenerate a pending request from current time. The model's fixed `admin`/`operator` identities and purpose-specific hash receipts are authorization fixtures only, not a production signature algorithm or provider proof. Routes are unambiguous: tenant entries use `POST /api/v1/admin/held-deliveries/tenants/{tenantId}/{entryKey}/redrive`; deployment entries use `POST /api/v1/admin/held-deliveries/deployment/{entryKey}/redrive` and Admin policy. Automatic redrive runs when cause-clearing evidence appears and otherwise with exponential backoff from 60 seconds to 15 minutes. It injects exact retained bytes/header image into the same authenticated ingress. Entry into `redriving` increments the count and binds the exact retained request/attempt. Terminal route decisions close the entry and refund after deletion readback. A nonterminal transport/ingress failure CASes `redriving -> captured`, preserves the retained object and charges, stores the typed error-evidence hash, and schedules the next bounded retry; restart resumes from that durable state. No failed redrive may remain indefinitely in `redriving`. A terminal quarantine is never redriven. A crash in `redriving` is reconciled on restart and hourly: freshly authenticate the persisted count, held identity, exact carrier/locator, entry CAS receipt and addressed signed request/attempt; terminal route readback closes that exact pair, otherwise unknown/unavailable completion returns it to scheduled `captured` with typed error evidence and the same count/carrier/charges. Corrupt or absent request/attempt evidence returns to an indexed captured evidence hold at the 15-minute maximum delay; it cannot authorize a send or completion until repaired. Every such attempt leaves redriving on its first bounded reconciliation. The fixed-slot lifecycle below bounds actual request and attempt bodies, not only error history.

Policy evidence activates before binary publication in slice 4; continuation/redrive activates with the binary carrier. Tenant records erase with the tenant; deployment captures erase only with their capture scope after all obligations close. Known answers `D36-policy`, `D36-held`, `D36-quarantine`, and `D36-redrive` appear in D12.

The closed held-delivery reasons are `handler-capability-hold`, `raw-source-unavailable`, `delivery-carrier-limit-hold`, `invalid-header-value`, `invalid-carrier`, `oversize-carrier`, and `delivery_above_advertised_max`. Their owner is `operations`; capture-capable reasons exit by exact-byte capture then redrive, permanently invalid reasons exit by terminal quarantine, and above-maximum exits only as specified above. Unknown reasons fail decoding and cannot be acknowledged.

Capture preparation is `HX-EV-CAPTURE-PREPARATION-1\0 || 01 || 0009` (8 KiB): `01` B32 held-key hash, `02` B32 exact observed predecessor hash, `03` B32 metadata-charge readback receipt hash, `04` B32 inventory reservation receipt hash, `05` U retained backend, `06` U resolvable object key, `07` B32 exact carrier/header-image hash, `08` N exact canonical length and `09` Q first-observed UTC. Address is `K("HX-EV-CAPTURE-PREPARATION-KEY-1", B32 heldKeyHash)`, create-once. D11's 32 KiB metadata reservation covers this 8 KiB preparation plus the field-derived held-record maximum (less than 12 KiB); inventory capacity is separately pre-reserved. Write/read the preparation under the observed predecessor fence before retained-object installation. Its active D7 object charge, immutable object and receipt remain discoverable through the still-observed hold until the held-state CAS finishes. A retry of matching partial work authenticates preparation/predecessor, metadata, inventory, exact object/locator/readback and charge, then completes captured state once without a new reservation or charge. Charge-only partial work installs/readbacks the exact missing object; object+charge partial work only completes the held CAS/readback. An already captured lost acknowledgement rereads the same complete authority. Changed/missing/unavailable evidence holds unchanged and unacknowledged; Operations reconciles hourly. Fresh object readback is authenticated before any new quota reserve; an absent, changed or unavailable observation leaves every account usage byte and generation unchanged. A genuinely charged partial write remains discoverable and may complete from exact readback. If object presence is unknown during cleanup, an authenticated object deletion or absence receipt is required before releasing its charge; a capture-successor absence receipt alone cannot authorize refund. Before captured authority exists, rollback requires deletion/readback of the exact prepared object and proved absent captured successor, then releases only that object's charge; partial/unavailable cleanup stays indexed and charged. The preparation erases only with verified held/object deletion or whole-scope erasure. The ordinary capture transition checks `length <= 193 MiB` before any object reservation/write; it cannot acknowledge the oversize interval without the separately required provider-quarantine receipt, nor above the advertised maximum without the incident path.

Before **every** redrive, Operations freshly reads the retained object and active charge, verifies exact bytes/header image, length/hash, backend/key, present capture preparation and provider receipt against the persisted held identity, and authenticates all four metadata/inventory/repair-charge/repair-interest obligations. It then persists the exact signed request/attempt and increments count before sending. The addressed D11.1 held-entry CAS stores exact bounded held-record bytes, native generation, predecessor hash and provider readback receipt. Replacement request, typed attempt, predecessor deletion receipts and incremented held count/state stage in one serializable provider-model transaction; a precommit crash or evidence refusal leaves every backend byte, generation and charge unchanged. A lost acknowledgement reads back the exact committed row and pair, and restart uses its count/state instead of a returned process dictionary. A stale count, generation, predecessor or receipt refuses without send. The existing 32 KiB metadata charge covers the typed held record, its native generation/predecessor/receipt, bounded 64-error history, original capture origin/preparation, the fixed cleanup row and compact completion fence. Their independent maxima total 28,864 bytes (4,800 + 8,192 + 12,288 + 2,048 + 256 + 1,024 + 256); the model also checks actual retained bytes before publication. The row stores no duplicate carrier, request, attempt, repair body or process snapshot. Restart reconstructs the exact scope/account, count/state, carrier and charge authority from the typed row plus the authenticated origin, retained object, capture preparation, charge and inventory rows. Missing, changed, stale, wrong account/state/receipt or unavailable authority sends nothing and leaves count and ledger unchanged, including when all obligations are absent. A corrupt request/attempt creates `HX-EV-REDRIVE-REPAIR-1\0 || 01 || 000a` (8 KiB): `01` B32 held-key hash, `02` N failed attempt count, `03` B32 disputed attempt-image hash, `04` B32 carrier hash, `05` U locator (4,096 bytes), `06` N repair generation, `07` B32 predecessor repair hash (zero at generation 1), `08` U state (`required` or `repaired`), `09` O(B32) authenticated repair receipt (present exactly when repaired), and `0a` Q update UTC. Address is `K("HX-EV-REDRIVE-REPAIR-KEY-1", B32 heldKeyHash, N attemptCount)`, CAS generation. One 32 KiB side-record reservation covers the finite signed-request/attempt/repair authority described below, with its separate prerequisite inventory slot pre-reserved during capture before acknowledgement; refusal leaves the physical copy unacknowledged. The same scope inventory actor exposes a separate `RedriveEvidenceRepairHold` with reason `redrive_evidence_repair_hold` and activates Operations hourly/at cause change. The original HeldDelivery carrier reason remains unchanged. There is at most one repair prerequisite per held carrier: before a next attempt can require that slot, the repaired predecessor record/entry must have authenticated deletion readback. Partial cleanup remains indexed and charged, and cannot permit another send.

Reconciliation leaves redriving for bounded captured retry and persists required repair before another send is possible. It writes and reads back the separately addressed repair record and `RedriveEvidenceRepairHold` inventory entry from the retained backend before publishing the captured repair hold. The pre-reserved side-record and inventory interest cover the two bounded bodies; at most one prerequisite remains live. Required-to-repaired CAS binds exact owner, count, attempt, carrier, locator, generation, predecessor and provider-native readback receipt. Cleanup deletes and reads back both addressed rows before clearing the prerequisite. Missing, changed, wrong-owner, partial or unavailable evidence stays charged and indexed, and permits no send. Lost acknowledgements resume from the addressed rows and checked cleanup phase; locally inferred deletion receipts are never authority. Both automatic and manual requests check that record after restart. Only authenticated repaired readback of the exact original request/attempt, count, retained carrier, locator and active charge may CAS `required -> repaired`; its generation/predecessor and repair receipt bind that original evidence. After authenticated cleanup readback it permits one next count/send; exact retry of the still-redriving attempt cannot increment again. Missing or forged repair stays required and scheduled at the 15-minute maximum. Terminal route readback closes the exact attempt; erasure/refund follows ordinary deletion readback. The repair slot/record activate with slice-4 redrive and erase with the held/capture scope. Unknown/unavailable completion retains the ordinary captured retry, error/count/backoff and exact carrier authority; it does not assert route success.

### D11.2 Collision-free durable hold inventory

The capture origin is `HX-EV-CAPTURE-ORIGIN-1\0 || 01 || 0006` (8 KiB): `01` B32 held key, `02` B exact canonical observed-predecessor image (at most 7 KiB), `03` B32 image hash, `04` Q first-observed UTC, `05` N original observation count and `06` B32 selected policy hash. Its create-once address is `K("HX-EV-CAPTURE-ORIGIN-KEY-1", B32 heldKey)`. D11's existing 32 KiB metadata charge covers this origin, the 8 KiB preparation and the field-derived held record below 12 KiB. The image stores only B32 hashes of scope/deployment/tenant/component/topic/subscription and account identifiers, resolved against the authenticated held record; raw user identifiers and their JSON escapes never enter it. Its field-derived maximum is 4,800 bytes including tags, six identity hashes, derived fixed-width keys/receipts, flags and four u64/Q values, below 7 KiB. Unsupported or contradictory images hold before object admission/acknowledgement. The origin preserves exact scope/account/carrier, first UTC, metadata, inventory and selected-policy authority. Its canonical image also contains checked observation revision/count and their authenticated provider observation receipt. Subsequent normal observations retain all protected fields, advance count/revision monotonically, and carry a fresh provider readback. Recovery authenticates the original persisted image and preparation, then composes only these observation changes into captured state. One or multiple observations after either charge-only or object-plus-charge crash cannot require changed create-once preparation bytes, reset the clock/count, or allocate a second object/charge. A changed selected policy cannot replace a partial capture's original policy binding. Forged observation receipts, changed protected fields and unavailable source authority hold unchanged. Origin/preparation remain discoverable under the original inventory interest and erase only after exact held/object deletion or whole-scope erasure; slice 4 activates them.

Capture preflights both inventory interests: the original held entry and a separate reserved `RedriveEvidenceRepairHold` interest owned by the same held key at the same authenticated generation. A charge alone cannot reserve that second slot. One remaining total slot permits observation but refuses capture; two slots permit it. Both interest receipts and metadata/repair/object charge scope, account, owner, generation, amount and active state authenticate before acknowledgement, exact retry, partial completion or any redrive. Repair interest address is `K("HX-EV-REDRIVE-REPAIR-INTEREST-KEY-1", B32 heldKey)`, under the same bounded inventory CAS. Refusal preflights capacity/counters and leaves no leaked interest or charge; unaudited rollback deletes/readbacks partial object/origin/preparation and releases their exact object/repair charges and second interest, preserving original metadata/interest. Partial cleanup remains charged and discoverable. Required capture preparation must be present on both held and provider sides and byte-identical; two absent values are never authority.

Exactly one active/disputed signed request and one attempt are retained per held key, at fixed addresses `K("HX-EV-REDRIVE-REQUEST-KEY-1", B32 heldKey)` and `K("HX-EV-REDRIVE-ATTEMPT-KEY-1", B32 heldKey)`; neither is a count-addressed append log. `HX-EV-REDRIVE-ATTEMPT-1\0 || 01 || 0007` (8 KiB) stores `01` B32 held key, `02` N checked positive count, `03` B32 carrier hash, `04` U locator (4,096 bytes), `05` B32 metadata receipt, `06` Q first-observed UTC and `07` B32 SHA-256 of the exact signed-request payload. Provider readback authenticates both exact rows, the existing request signature and count-to-request binding. Before advancing, authenticate the exact predecessor pair/count, delete both and authenticate their deletion readbacks, then replace them with the successor pair together with the checked held-count CAS. This is one required serializable provider transaction: staged readback/deletion failures or a precommit crash abort with no row/count/charge changes; a committed lost acknowledgement restarts from both exact rows and the committed held count, sends no invented successor and reconciles that attempt once. Readiness must prove this transaction on the actual backend before activation; detached dictionaries only model it. Exactly one compact authenticated deletion receipt per fixed slot replaces its predecessor. A stale original request is fenced by its signed expected count after reclamation. Disputed original request/attempt rows remain available through repair and all four cleanup boundaries; absent/corrupt/unavailable readback or reclamation blocks admission without send or count/ledger mutation. The 32 KiB side reservation covers the maximum staged overlap, not just committed rows: two request payloads at most 3 KiB each, two attempts each below 5 KiB (field-derived maximum 4,278 bytes within the 8 KiB family cap), the fixed-purpose repair record below 6 KiB total at most 22 KiB; the prerequisite `HX-EV-HOLD-ENTRY-2` is charged separately at its full 8 KiB ceiling only to the operational-evidence quota and is excluded from this side reserve. The remaining 10 KiB bounds complete signature envelopes, native readback/deletion receipts and other transaction-support images together. A provider exceeding either this auxiliary ceiling or any record cap fails readiness/admission before capture acknowledgement. Charge stays constant across more than 130 genuine failed sends/restarts while both actual row dictionaries and bytes remain bounded. The pair/receipts activate with redrive, remain original-interest discoverable and erase with terminal held/object deletion or whole-scope erasure; refund follows authenticated deletion of every request, attempt and receipt as well as the held/capture authority.

Authenticated `required -> repaired` remains indexed and charged until both exact repaired record and separate typed prerequisite entry have deletion readback. `HX-EV-REDRIVE-CLEANUP-1\0 || 01 || 0007` (1 KiB, within metadata reserve) stores `01` B32 held key, `02` N disputed count, `03` B32 repaired-record hash, `04` B32 exact prerequisite-entry hash, `05` U phase (`repaired-readback`, `record-deleted`, `record-readback`, `entry-deleted`), `06` O(B32) record deletion receipt and `07` O(B32) entry deletion receipt. Its fixed checked-CAS address is `K("HX-EV-REDRIVE-CLEANUP-KEY-1", B32 heldKey)`. Its compact native phase row persists those typed bytes, a 168-byte support image containing generation, predecessor, original record/entry readback receipts, required predecessor and repair receipt, and an authenticated native readback receipt; the complete serialized row is at most 1 KiB. Each phase CASes the exact predecessor and reads back the successor before continuing. It never duplicates the 8 KiB repair or hold-entry bodies. The same fixed address retains only a compact completion count/receipt fence after both deletion readbacks, and the old native deletion receipts are reclaimed once that fence reads back. A record receipt is present after record deletion; the entry receipt only after entry deletion. Authenticate phase/hash/provider receipts across restart and each write/delete/readback. Missing/unavailable cleanup leaves the same charge/index and blocks automatic/manual sends and reuse of the single repair interest. Completed cleanup retains only a compact authenticated receipt/count fence, removes the prerequisite, and permits one next attempt. A second corruption uses the same bounded slots after that fence, preserving original HeldDelivery reason and genuine repair receipt. Cleanup uses bounded authenticated provider-native deletion/readback metadata at `K("HX-EV-PROVIDER-NATIVE-CLEANUP-KEY-1", U cleanupKind, B32 heldKey)`, with cleanupKind `capture` or `held-erasure`. Each fixed native metadata slot is at most 4 KiB, covered by existing metadata/auxiliary reservations; it contains only owner, bound predecessor/terminal digests, amount and at most nine deletion receipt digests, and survives restart under the original inventory interest. It is reclaimed after final refund, never an append log. The original discovery interest remains until the final atomic charge deletion/counter refund; partial deletion or refund refusal preserves it. Terminal erasure authenticates route completion, all retained obligations and cleanup, deletes/readbacks every origin/preparation/object/attempt/entry/receipt, refunds exactly once and removes both interests; offboarding retains D1's no-success-by-erasure rule.

Inventory scope is explicit. Tenant actor ID is `tenant:` plus lowercase-hex SHA-256(`U tenant`); deployment actor ID is `deployment:` plus lowercase-hex SHA-256(`U deployment identity`). A legal tenant string `deployment` therefore cannot collide. Tenant reads use `GET /api/v1/admin/holds/tenants/{tenantId}` with tenant authorization; deployment reads use `GET /api/v1/admin/holds/deployment` with Admin policy.

Every D1 predicate hold has `HX-EV-HOLD-ENTRY-2\0 || 01 || 000d` (at most 8 KiB, charged at that ceiling): `01` U scope kind, `02` U scope ID, `03` U hold code, `04` U stable subject key (1..4,096 UTF-8 bytes), `05` O(U) domain, `06` U current reason code, `07` N entry revision, `08` B32 predecessor entry hash (zero at revision 1), `09` Q first observed UTC, `0a` Q last observed UTC, `0b` N observation count, `0c` U owner kind (the closed D1 set), and `0d` Q next re-evaluation UTC. Key is `hold-entry:` plus lowercase-hex SHA-256(`"HX-EV-HOLD-ENTRY-KEY-1\0" || 01 || U scope kind || U scope ID || U hold code || U subject key`). A cause/reason change CAS-writes the next revision before changing the ordered index; stale tag `06` cannot survive. Resolution CAS-removes the index row only after its named evidence reads back.

`HX-EV-HOLD-INDEX-2\0 || 01 || 0008` (at most 40 MiB) has `01` U scope kind, `02` U scope ID, `03` N generation, `04` N entry count (at most 10,000), `05` B rows sorted by `(firstObservedUtc, holdCode, subjectKey)` with no nested count (`Q firstObservedUtc || U holdCode || U subjectKey || B32 current entry hash`), `06` N overflow count, `07` B32 predecessor index hash, and `08` Q update UTC. Tag `04` controls exact row parsing. For new capabilities, overflow must remain zero: command operations reserve an inventory slot before commit, tenant onboarding reserves a tenant actor/directory slot, and delivery capture reserves before acknowledgement. An inability to reserve stops that earlier boundary. The reserved overflow tag is exactly zero; no historical producer of this family exists.

A key-only store discovers actors through 256 deployment directory shards selected by the first byte of SHA-256(`U scope kind || U scope ID`). `HX-EV-HOLD-DIRECTORY-1\0 || 01 || 0007` (at most 64 MiB per shard) has `01` N shard `0..255`, `02` N generation, `03` N actor count (at most 50,000), `04` B sorted actor IDs with no nested count, `05` B32 predecessor directory hash, `06` Q update UTC, and `07` N reserved onboarding slots. Actor creation/removal and the directory row commit under one deployment-directory fence. A tenant must reserve its row before EventStore admission is enabled; deployment scope is reserved at bootstrap. This bounds and enumerates at most 12,800,000 inventory actors without a store scan.

The reconciler leases each directory shard under one durable epoch. Only the active epoch owner publishes `hexalith.eventstore.holds.active`; other replicas publish no sample. It activates every listed actor, reads durable index counts, and aggregates by `hold_code` and `domain` (or `none`). Lease loss stops emission before another owner begins, preventing under/double count across replicas. Admin paging is oldest first, page size 1..200/default 50, with a scope-bound authenticated cursor; results include current reason, revision, times, count, owner and stale flag. The command list joins by the stable subject and exposes this current evidence.

Inventory, its 256 directory shards, onboarding/inventory reservations and the gateway re-evaluation owner activate in slice 2 before the first legacy admission/capacity-hold producer. They are charged to the tenant/deployment operational-evidence quota, re-evaluated at least hourly, and erased with their scope after all obligations close. Known answers `D37-entry`, `D37-index`, `D37-directory`, and `D37-key` appear in D12.

### D11.3 Complete durable address registry

For this candidate, `K(name, fields...)` means the literal prefix in the registry below plus lowercase-hex SHA256(ASCII name including NUL || 01 || fields...); `KD` in D12 preserves the nineteen historical digest-only answers. The imported shared command-scope address is explicitly outside K and hashes only `U tenant || U executionMessageId`, exactly as D4 and 6.5a A8; every variable field uses `U`, optional field uses `O`, numeric field uses `N`, and digest uses `B32`. Raw text concatenation is forbidden. A `revision` row is create-once and must name its predecessor; a `head` is a CAS row whose codec carries generation and predecessor hash; `create-once` accepts only byte-identical readback. These are the complete replacement-owned addresses:

| Durable family | Exact address and mutation rule |
| --- | --- |
| full-replay activation | `K("HX-EV-FULL-REPLAY-ACTIVATION-KEY-1", U domain, B32 fingerprint, N generation)`, create-once with predecessor activation hash |
| drain limit / resolution / active pointer | `K("HX-EV-PUBLICATION-DRAIN-LIMIT-KEY-1", B32 ScopeOpHash, N window, N limit)` create-once; `K("HX-EV-PUBLICATION-DRAIN-RESOLUTION-KEY-1", B32 ScopeOpHash, B32 limitHash)` create-once; `K("HX-EV-PUBLICATION-DRAIN-HEAD-KEY-1", B32 ScopeOpHash)` CAS head |
| legacy claim or tombstone / shard usage / cutover | shared `command-execution-scope:` + lowercase SHA256(`U tenant || U executionMessageId`) CAS type transition; no key domain separator or codec byte is added; `K("HX-EV-SCOPE-SHARD-USAGE-KEY-1", U tenant, N shard)` CAS head; cutover revision `K("HX-EV-LEGACY-SCOPE-CUTOVER-KEY-1", U tenantOrStar, U domain, N generation)` plus CAS head `K("HX-EV-LEGACY-SCOPE-CUTOVER-HEAD-KEY-1", U tenantOrStar, U domain)` |
| first-send resolution | imported C2 first-send CAS head `K("HX-EV-FIRST-SEND-MEMBERSHIP-KEY-1", U tenant, B32 ScopeOpHash, N memberPosition, U MessageId, B32 immutablePinHash)`; every contiguous resolution carries predecessor hash |
| destination configuration | revision `K("HX-EV-DESTINATION-CONFIG-KEY-1", U deployment, U component, U topic, N revision)` and CAS head `K("HX-EV-DESTINATION-CONFIG-HEAD-KEY-1", U deployment, U component, U topic)` |
| retention capability / charge / counter | capability revision `K("HX-EV-PUBLICATION-CAPABILITY-KEY-1", U deployment, N revision)` plus CAS head `K("HX-EV-PUBLICATION-CAPABILITY-HEAD-KEY-1", U deployment)`; charge CAS head `K("HX-EV-PUBLICATION-CHARGE-KEY-1", U deployment, U accountKind, U accountId, B32 objectKeyHash)`; counter CAS head `K("HX-EV-PUBLICATION-COUNTER-KEY-1", U deployment, U counterKind, U counterId)` |
| pin-batch reservation | `K("HX-EV-PIN-BATCH-RESERVATION-KEY-1", B32 ScopeOpHash, B32 candidateBatchRoot)`, CAS generation; changed candidate root is a new reservation only after the old generation is released |
| wait / queue and allocator | wait `K("HX-EV-PIN-CAPACITY-WAIT-KEY-1", U deployment, B32 stableCapacitySubject)` CAS generation; queue `K("HX-EV-PIN-CAPACITY-QUEUE-KEY-1", U deployment, U counterId)` CAS generation. The global allocator is tag `09` of the deployment queue at counter ID `deployment`, not an unaddressed side counter. |
| queue owner authority / indexes / predecessor / native receipt | authority `K("HX-EV-PIN-WAIT-AUTHORITY-KEY-1", U deployment, B32 stableCapacitySubject)` checked CAS; owners `K("HX-EV-PIN-QUEUE-OWNERS-KEY-1", U deployment, U counterId)` checked CAS; predecessor `K("HX-EV-PIN-QUEUE-PREDECESSOR-KEY-1", U deployment, U counterId)` one fixed source slot; receipt `K("HX-EV-PIN-QUEUE-RECEIPT-KEY-1", U deployment, U completeObjectAddress)` one fixed typed native slot, with D8.1 reclamation. |
| resume claim / state / window / closure / audit | exact signed request claim `K("HX-EV-PUBLICATION-RESUME-CLAIM-KEY-1", U tenant, U executionIdentity, B32 stableRequestIdentity)` create-once; state and live/tombstone index `K("HX-EV-PUBLICATION-RESUME-STATE-KEY-1", U tenant, U executionIdentity)` CAS; window `K("HX-EV-PUBLICATION-WINDOW-KEY-1", B32 ScopeOpHash, N window)` create-once; closure key as D9.2; audit key as D9.3 |
| resume origin / reconstruction / progress / invocation | origin `K("HX-EV-RESUME-ORIGIN-KEY-1", U tenant, U executionIdentity, B32 stableRequestIdentity)` create-once; reconstruction `K("HX-EV-RESUME-PREPARATION-KEY-1", U tenant, U executionIdentity, B32 stableRequestIdentity)` create-once; progress `K("HX-EV-RESUME-PREPARATION-HEAD-KEY-1", U tenant, U executionIdentity)` checked CAS generation; invocation `K("HX-EV-PUBLICATION-INVOCATION-KEY-1", U tenant, U executionIdentity, B32 invocationIdentity)` create-once. Every row uses D9.4 ownership/cleanup authority. |
| complete window attempts | `K("HX-EV-WINDOW-ATTEMPT-SET-KEY-1", U tenant, B32 ScopeOpHash, N window)` create-once sealed set; pre-seal collection remains in charged imported C2 generation-fenced records. |
| legacy capsule chunks / manifest / recovery | chunk and manifest keys as D10.1; recovery CAS head as D10.2. A later exhaustion advances recovery, not either create-once capsule address. |
| subscription policy | revision `K("HX-EV-SUBSCRIPTION-POLICY-KEY-1", U deployment, U component, U topic, U physicalSubscriptionId, N revision)` and CAS head `K("HX-EV-SUBSCRIPTION-POLICY-HEAD-KEY-1", U deployment, U component, U topic, U physicalSubscriptionId)` |
| held delivery / quarantine / redrive | held CAS key `held-delivery:` plus lowercase-hex heldDeliveryKeyHash with exact bounded record bytes, native generation/predecessor/receipt; quarantine create-once `K("HX-EV-CARRIER-QUARANTINE-KEY-1", B32 heldDeliveryKeyHash, B32 carrierHash)`; signed request fixed checked-CAS slot `K("HX-EV-REDRIVE-REQUEST-KEY-1", B32 heldDeliveryKeyHash)`, replaced only by authenticated predecessor-pair reclamation with the held-count fence |
| capture origin / preparation | origin `K("HX-EV-CAPTURE-ORIGIN-KEY-1", B32 heldDeliveryKeyHash)` and preparation `K("HX-EV-CAPTURE-PREPARATION-KEY-1", B32 heldDeliveryKeyHash)` create-once; D11.2 owns the original inventory interest, authenticated rollback and terminal erasure. |
| redrive current attempt / repair / cleanup | current attempt `K("HX-EV-REDRIVE-ATTEMPT-KEY-1", B32 heldDeliveryKeyHash)` is one fixed replaceable slot, only after authenticated receipt/count and predecessor deletion readback; repair `K("HX-EV-REDRIVE-REPAIR-KEY-1", B32 heldDeliveryKeyHash, N attemptCount)` checked CAS generation; cleanup `K("HX-EV-REDRIVE-CLEANUP-KEY-1", B32 heldDeliveryKeyHash)` checked phase CAS over the exact predecessor bytes. The separately reserved repair inventory interest discovers the repair and typed prerequisite; D11.2 owns exact deletion readback and erasure. |
| repair interest / native capture cleanup | interest `K("HX-EV-REDRIVE-REPAIR-INTEREST-KEY-1", B32 heldDeliveryKeyHash)` under inventory CAS; native cleanup `K("HX-EV-PROVIDER-NATIVE-CLEANUP-KEY-1", U cleanupKind, B32 heldDeliveryKeyHash)` for `capture` or `held-erasure`, at most 4 KiB each fixed slot. |
| hold entry / index / directory | entry key as D11.2; index CAS head `K("HX-EV-HOLD-INDEX-KEY-1", U scopeKind, U scopeId)`; directory CAS head `K("HX-EV-HOLD-DIRECTORY-KEY-1", U deployment, N shard)` |


Literal prefix registry (the displayed `\0` denotes one final NUL byte; K accepts the name without that displayed escape and adds the NUL and codec byte exactly once). D11.3's field table governs every listed family, including head variants. `command-execution-scope:` uses D4's imported exception.

| Exact K name | Literal physical prefix |
| --- | --- |
| `HX-EV-FULL-REPLAY-ACTIVATION-KEY-1\0` | `full-replay-activation:` |
| `HX-EV-PUBLICATION-DRAIN-LIMIT-KEY-1\0` | `publication-drain-limit:` |
| `HX-EV-PUBLICATION-DRAIN-RESOLUTION-KEY-1\0` | `publication-drain-resolution:` |
| `HX-EV-PUBLICATION-DRAIN-HEAD-KEY-1\0` | `publication-drain-head:` |
| `HX-EV-SCOPE-SHARD-USAGE-KEY-1\0` | `scope-shard-usage:` |
| `HX-EV-LEGACY-SCOPE-CUTOVER-KEY-1\0` | `legacy-scope-cutover:` |
| `HX-EV-LEGACY-SCOPE-CUTOVER-HEAD-KEY-1\0` | `legacy-scope-cutover-head:` |
| `HX-EV-FIRST-SEND-MEMBERSHIP-KEY-1\0` | `first-send-membership:` |
| `HX-EV-DESTINATION-CONFIG-KEY-1\0` | `destination-config:` |
| `HX-EV-DESTINATION-CONFIG-HEAD-KEY-1\0` | `destination-config-head:` |
| `HX-EV-PUBLICATION-CAPABILITY-KEY-1\0` | `publication-retention-capability:` |
| `HX-EV-PUBLICATION-CAPABILITY-HEAD-KEY-1\0` | `publication-retention-capability-head:` |
| `HX-EV-PUBLICATION-CHARGE-KEY-1\0` | `publication-charge:` |
| `HX-EV-PUBLICATION-COUNTER-KEY-1\0` | `publication-counter:` |
| `HX-EV-PIN-BATCH-RESERVATION-KEY-1\0` | `pin-batch-reservation:` |
| `HX-EV-CAPACITY-SUBJECT-1\0` | `capacity-subject:` |
| `HX-EV-PIN-CAPACITY-WAIT-KEY-1\0` | `pin-capacity-wait:` |
| `HX-EV-PIN-CAPACITY-QUEUE-KEY-1\0` | `pin-capacity-queue:` |
| `HX-EV-PIN-WAIT-AUTHORITY-KEY-1\0` | `pin-wait-authority:` |
| `HX-EV-PIN-QUEUE-OWNERS-KEY-1\0` | `pin-queue-owners:` |
| `HX-EV-PIN-QUEUE-PREDECESSOR-KEY-1\0` | `pin-queue-predecessor:` |
| `HX-EV-PIN-QUEUE-RECEIPT-KEY-1\0` | `pin-queue-receipt:` |
| `HX-EV-PUBLICATION-RESUME-CLAIM-KEY-1\0` | `publication-resume-claim:` |
| `HX-EV-PUBLICATION-RESUME-STATE-KEY-1\0` | `publication-resume-state:` |
| `HX-EV-PUBLICATION-WINDOW-KEY-1\0` | `publication-window:` |
| `HX-EV-PUBLICATION-WINDOW-CLOSURE-KEY-1\0` | `publication-window-closure:` |
| `HX-EV-PUBLICATION-RESUME-AUDIT-KEY-2\0` | `publication-resume-audit:` |
| `HX-EV-RESUME-ORIGIN-KEY-1\0` | `resume-origin:` |
| `HX-EV-RESUME-PREPARATION-KEY-1\0` | `resume-preparation:` |
| `HX-EV-RESUME-PREPARATION-HEAD-KEY-1\0` | `resume-preparation-head:` |
| `HX-EV-PUBLICATION-INVOCATION-KEY-1\0` | `publication-invocation:` |
| `HX-EV-WINDOW-ATTEMPT-SET-KEY-1\0` | `window-attempt-set:` |
| `HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1\0` | `legacy-resume-capsule-chunk:` |
| `HX-EV-LEGACY-RESUME-CAPSULE-KEY-2\0` | `legacy-resume-capsule:` |
| `HX-EV-LEGACY-PUBLICATION-RECOVERY-KEY-2\0` | `legacy-publication-recovery:` |
| `HX-EV-SUBSCRIPTION-POLICY-KEY-1\0` | `subscription-policy:` |
| `HX-EV-SUBSCRIPTION-POLICY-HEAD-KEY-1\0` | `subscription-policy-head:` |
| `HX-EV-HELD-DELIVERY-KEY-2\0` | `held-delivery:` |
| `HX-EV-CARRIER-QUARANTINE-KEY-1\0` | `carrier-quarantine:` |
| `HX-EV-REDRIVE-REQUEST-KEY-1\0` | `redrive-request:` |
| `HX-EV-CAPTURE-ORIGIN-KEY-1\0` | `capture-origin:` |
| `HX-EV-CAPTURE-PREPARATION-KEY-1\0` | `capture-preparation:` |
| `HX-EV-REDRIVE-ATTEMPT-KEY-1\0` | `redrive-attempt:` |
| `HX-EV-REDRIVE-REPAIR-KEY-1\0` | `redrive-repair:` |
| `HX-EV-REDRIVE-REPAIR-INTEREST-KEY-1\0` | `redrive-repair-interest:` |
| `HX-EV-REDRIVE-CLEANUP-KEY-1\0` | `redrive-cleanup:` |
| `HX-EV-PROVIDER-NATIVE-CLEANUP-KEY-1\0` | `provider-native-cleanup:` |
| `HX-EV-HOLD-ENTRY-KEY-1\0` | `hold-entry:` |
| `HX-EV-HOLD-INDEX-KEY-1\0` | `hold-index:` |
| `HX-EV-HOLD-DIRECTORY-KEY-1\0` | `hold-directory:` |

All CAS families reject a missing, stale, or wrong-kind predecessor without side effects. All create-once families reject changed bytes. D12 fixes representative key vectors spanning activation, drain, resume closure/audit, and legacy manifest/recovery; their framing rule is the same rule used by every row above.

## D12. Known answers and executable verification

The original fixtures use tenant `t`, domain `d`, aggregate `a`, execution `op`, UTC ticks `638712864000000000`, and zero bytes for genesis predecessors. Supplementary resume fixtures use the explicit model times and handle below. Ordinary opaque references hash displayed fixture bytes. Candidate-batch roots, stored-event roots, window attempt roots, history accumulators, capsule hashes, audit hashes, and retry rows are recomputed from their semantic inputs in dependency order; no unrelated label hash substitutes for them. They are local framing answers, not provider evidence. The destination JSON is exactly `{"component":"pubsub","metadata":{},"schema":"hexalith.eventstore.destination/1","topic":"orders"}`. The first table preserves exactly 30 original record/codec answers; the supplementary table adds twelve answers and the key table fixes nineteen framed address derivations.

```text
D06-activation 225 ce6ece552e0e0c6220e13f3bfc15215d4995299bba0d2259844b55dd7c557605
D12-legacy-claim 164 e01c2779a425182a0d676c6ac2c8a057f8131da0e36cbee7047b654e564f5394
D12-cutover 146 669307293a19b9356da9801bc1d73473748f88df13c230b5133d51eaf43e302f
D12-usage 113 dfdeb5df0eb0124072f69e25c0d3e759ef4599c50a39cc9e33dc2f29ca33addf
D12-tombstone 174 f04ab33d882ecba7632d4131688d0a33ec4ef6a485b497aefa08cd2da3f74363
D14-drain-limit 202 e22848b3ab4b7c6bd3357078b95410550c1ee0e552d968a9c01ec8068ea4f343
D14-drain-resolution 197 9fe241c44c658302aac2187226a561138458983ae10d4e2cf81eacd2094b005e
D16-membership-resolution 285 2875259659b0f8a7225a55f4f18b9110c6323796ee2f508343c319854a607c40
D17-destination-config 98 048e9eb50252feb334b66506baa8af1c26f6fab1b56461b6fbb491ec12fdb320
D29-capability 214 a0194011a460ccf2063d4e9c78b3b7ad4f7bbf70ce9e63a55f237a0ef2b52043
D29-charge 220 5b0dcea4582d17f74a4a51c0f148ed7d2e5a7c6529f43c82348ff6d612e2d868
D29-counter 134 eca227f547122e041a159affbac58bf7b57375bf91d78876d88cd73f418d1fc8
D29-pin-batch 330 82e7a4965c94f3be0092cbcb8adb20b8ea560ef13419c88a036f5dc2d62c4517
D31-wait 238 7dca50b2d1f448628a84ee423ab3aa364600a320ed10886711cc9c128bc4f3b8
D31-queue 218 1c3c2b7655f178df7ddd09d33f4c64c66b5220ab0b6831abc088e5371b978584
D36-policy 257 9989075b380937e91cf9e98b27670f195b58aaeb326f9e90e54c8fe6158f6a88
D36-held 371 812399ecf7f18d8ca0c39efeff73d6d6a52b682416c5a14174334a96c62a1675
D36-quarantine 336 5dcce10f015b54bb853a3beb633b865b87889685771beeeb603d351559536257
D36-redrive 119 1a552c0f005efc61b9d74682a5f707925c0b47b5e31d559bdb0b43da43f8b49f
D37-entry 225 bf5562b4dee2cb51dc8a3cdb511b4e7ceb9bfc6a420603419f370a62c7c9e59f
D37-index 196 a14e6b24af414ad169f98ba8829f971635724de0acc686a901024ebeec6967fc
D37-directory 184 d36d068c6a1cea553949c29cafc628cbde9881e712a190d26033211f26b19569
D37-key 58 eabf14e49beb9484895f4233927107604705ea58aac8d049e34db91ce7152978
D45-request 329 17cef46646c6320df270e39b85a6ab05cf568d798de842b5f5f64bf83caa9e50
D45-window 318 573c53d0e7b7511bb5e131b33280e5a5f8e96138ddab0dce1bcc0b380b2ea9df
D45-closure 331 bcd6e3ea2d4ded2afc8a7bad0d9a5e4d6414955df40a9ae500d5e262bdb3e2d1
D45-state 405 b1790d7e41b5c09ec399296e955b448c7d59b65915434911ce94e997fd40035b
D45-audit 218 65a4c355269c0fea578bd2a74522261385e9e0b3c7169c8fd8c3557a85768429
D46-capsule 409 272a728d7d53a0bdfe3c97556e9f17561bab352c3ae230e637bbe12de2eae9e0
D46-recovery 257 e76a4ee829e526bccffa0dbe47baa807d79626832da20d3e989cbea3cefc8e56
```

Twelve supplementary answers preserve the original 30 record/codec answers and cover the lossless legacy chunk, complete attempt set, nine added durable preparation/invocation/repair families and the exact current request in the existing signed-request family. Resume preparation fixtures use the real D12 model input with handle `hxrsm1-other`, server time 1,000 seconds and expiry 1,900 seconds (encoded as Q ticks). Capture/repair use the model's exact 34-byte carrier and declared tenant identity; their provider receipt digests remain opaque fixture inputs, not claimed provider proofs.

```text
D46-chunk 179 b283d4e7b0025e99a6258569483c2d2b8555332de17f355491e3c4da404d4cd8
D45-attempt-set 1691 99c18639aebb9701710692fc62663bee36f9954dd550dfb773f33a7c1bb95fa3
D45-origin 1361 5fc088af9f2eeb9b6e4f06734a74b80be096ef115dcb7c75077ef0dff9c2039e
D45-preparation 4416 592af4e390b03d151caa220de530232c43d1d7d9e3842f091c505e9e3e437010
D45-preparation-head 189 4b754c57126d0a30fb6b858ac982572f26caf6b8c67d53be3ed829c962f942c1
D45-invocation 206 9d2bc55777ae8d592aa4f3cdddbb5a600bda1c787202aab9db5b6ca0b10d7892
D36-capture-preparation 312 500b882b931f39b8dfc8be00e378c8c225d246499d14187dc1c58ff389be3e84
D36-repair 274 b3421ca88730692fdc141d72a6c79ab760a11039b7a2f226e67a1b386fec4564
D36-capture-origin 2046 9e90b64552dc261ffc231adb06dacd93860374ff5030b25acc6a10e95c24ce4c
D36-current-request 119 3a6729276c8058b34686f93af8cd64b5e333feaa42bd7bad4ced1354b6f9c1c2
D36-attempt 251 8d17e410a656deca924613edd8e6d2ed914b5107bf451ecdfe279d78b23f2765
D36-cleanup 161 76db13f026ca36503ed6f0296881c2f6d6224f7f4ef8f2e863d8f1b23dfa7785
```

```text
D06-key 0f91a1983b2d87cad832582071c4c14b82fb4120da3f20615531f3d085bc132a
D14-key 3d6106bf9476e5106018bc78cc6bc4bf771144175f662f3ca5e1c631af3e23df
D45-closure-key 58b1b024adae4973e90e4e491aa8713593c439d51b5eb3941eaf66950e0b73b9
D45-audit-key 9d7734f4f8ccb48cce924620e859cf4ba886f3c82a2948729320e57a99877104
D46-capsule-key bdd34978ca55de1163ea7224dd6cae35ee041002ab6d4cbe655090b4e544291a
D46-recovery-key c3b5c20b7bb59e35990d5f13bd7ec52ade5f62c9be460380ed27307f2b5e9c06
D45-origin-key 422702346fa72ce8f7b750064ea0435e0ac9f57594d002dfb423547bda3d7cbd
D45-preparation-key bf5b1e511ac948812047008f3e7db91643213bbe241d52248d477180ceb8b10f
D45-preparation-head-key 2e00fb6e8b73edf9d9ac9306dc8fc86c336cd50f0f106b396f766ecb9c585f56
D45-invocation-key cf6992d150ef7d649ac08dae934c43ba3d9dcb1059c75f4f952a8f9593e20bb0
D45-attempt-set-key 54c75760d824af7953808abc25bb30e066be7c5198f6946c2ef6a9d7d001a401
D36-capture-origin-key c7c95df2993b3321185727e8c3cdc427194e8cd1b8d89aaa4c4a7da57fe13706
D36-capture-preparation-key e39f31a903caddadb1d05ca07563e85e7725f3363b3a8c31734ec28a3a16c971
D36-repair-key 458929ead8fd934fcfde8d56d2c1c0d18f6dc00502886ce4fa72288a3747d2a0
D36-repair-interest-key c116d8e68e29b4f263793f384503a8bb8587e2fc314caf655c8562f10c4899c3
D36-attempt-key b42756deb69e95ce7e5a1bbbbd8b611f1cfa41a04f1268a1eff54901498b67a0
D36-request-key aa444bf208fe5084a573bdb8650075c5d70fb91d85a8659a6bc8f3b81781d1be
D36-cleanup-key b836aaf894ea3d860a820883a2519732b040ab276996f929240f044e33b1cb64
D46-chunk-key 392440e0e1539887ff9e689b0259ab445652ed87ed69f4cf4e17144296ccd015
```

This verifier reconstructs 30 original, twelve supplementary and five loop-7 answers, checking exact lengths/hashes and 47 digest-changing byte probes as framing evidence. Decoder rejection separately executes 230 missing, duplicate, reordered, overflowing and trailing mutations across 46 framed records covering all 45 domain families (the destination JSON and unframed inventory key are separate answers). 144 semantic/boundary rejections cover signed counts, range/count, enums, optional markers, identifiers, large-family limits, complete attempt authority, unique live ordinals, charge/overhead/policy maxima, canonical continuation images, reconstruction linkage, invocation identity, progress framing and new inventory owners. Ordinary U identifiers, including optional ones, have a 1,024-byte maximum; the derived queue counter ID permits its seven-byte `tenant:` prefix plus that identifier (1,031). Explicit subject/locator fields permit 4,096, reason permits 512, caller key 128 visible ASCII and operator subject 256. B fields use their family ceiling before allocation and exact row/count/root constraints afterward. Capability shards are exactly 256; shard numbers are 0..255; genesis/predecessor and charge ownership follow D7. Canonical tagged images must decode/re-encode exactly; no process snapshot or noncanonical JSON substitutes for the declared continuation fields. Cross-record provider authentication remains future evidence.

```text
D31-carrier 164 35408b5eefe8e039baad7a0643fd01e4ac4ca2dd8ea83479a8f8c42c03a80b62
D31-authority 904 6efdb9e0ca780dc0b55a895f988b733109fc740a0e18e71e809b204d1034279c
D31-owners 206 1152af8e451f1940ef3031225838985de6294eba7bdd0821a3867f101a0f5cba
D31-predecessor 709 62fbe183377dcbb39fff7c44194dc06d69f5a3013709882024cbc3709de25411
D31-receipt 279 1c8f5c00e7036b249cfb7961c201bc705d7483e01e8038a1a0e41986e4560267
```

```text
D11-shared-scope command-execution-scope:92125fdf867b084df2239305327b2c152490754761e8c8965d6add62f699e554
HX-EV-FULL-REPLAY-ACTIVATION-KEY-1 full-replay-activation:0f91a1983b2d87cad832582071c4c14b82fb4120da3f20615531f3d085bc132a
HX-EV-PUBLICATION-DRAIN-LIMIT-KEY-1 publication-drain-limit:3d6106bf9476e5106018bc78cc6bc4bf771144175f662f3ca5e1c631af3e23df
HX-EV-PUBLICATION-DRAIN-RESOLUTION-KEY-1 publication-drain-resolution:0d49a6aeb2337b8e6e19bcba1f1f62c6fac25d4e1bb9bcae8f204033dfc97ab2
HX-EV-PUBLICATION-DRAIN-HEAD-KEY-1 publication-drain-head:aad5387a2d2a2d00ef4cf20b036591c3e07a62f9a0d969bb170ca9faa3243766
HX-EV-SCOPE-SHARD-USAGE-KEY-1 scope-shard-usage:0041967a6df216c97bc623dc18e2ac47f083d029c2af5633989d649fde519bd1
HX-EV-LEGACY-SCOPE-CUTOVER-KEY-1 legacy-scope-cutover:2de73a1cd44a75893e4e42324c64fca205a1acb951203dde64504c5c6cf21ab6
HX-EV-LEGACY-SCOPE-CUTOVER-HEAD-KEY-1 legacy-scope-cutover-head:f7f63d24bc46acdc13d3bc21e47518c346f6ff9f2aaa9883428307ca01a87c8e
HX-EV-FIRST-SEND-MEMBERSHIP-KEY-1 first-send-membership:88826ec0d2e5f74d8af6fa0b2a09956e38ceb1cc31b8e389899fbf99f00e3f36
HX-EV-DESTINATION-CONFIG-KEY-1 destination-config:6a98969b4fc78e6f207dd2cbb8c2c641a5cdcb935d40c28afdc743b7b4232469
HX-EV-DESTINATION-CONFIG-HEAD-KEY-1 destination-config-head:7462a1b314e18c0cfd8cca08fa9debcdd6455dc784c5c39e848754174e456b63
HX-EV-PUBLICATION-CAPABILITY-KEY-1 publication-retention-capability:59523fab707d08dbbd9ce366d8305e42a4f6d2ad58174e495ec7280076aeb0fc
HX-EV-PUBLICATION-CAPABILITY-HEAD-KEY-1 publication-retention-capability-head:b0ff122e5bad1b011dc522903ec42514ab08f826cdfbd649aecf1b634b7c6665
HX-EV-PUBLICATION-CHARGE-KEY-1 publication-charge:fecd61786cf64869dffff0b50d6c0f1be9d882ae1f59adffd33e3c46c458faa1
HX-EV-PUBLICATION-COUNTER-KEY-1 publication-counter:b66ad11deb0a645e172010713db74b71c8af5d3db9ad6eb46b50032555372e59
HX-EV-PIN-BATCH-RESERVATION-KEY-1 pin-batch-reservation:32940c8bce34a3ff4c7bd532ad4de063caafbeef63286ebb3f7536b0a02c7edc
HX-EV-CAPACITY-SUBJECT-1 capacity-subject:535ac4e9970218fc7e176c9b8cf6067dad0f3381c867c21fdf27e332127fb176
HX-EV-PIN-CAPACITY-WAIT-KEY-1 pin-capacity-wait:873fe3cce659783c58247e07dc8820a476137d934cbf3e22d6aa311299c40e24
HX-EV-PIN-CAPACITY-QUEUE-KEY-1 pin-capacity-queue:a10216c2265fd16d68fb3264e56861f831c861bd62fc2478a98af720d300eebe
HX-EV-PIN-WAIT-AUTHORITY-KEY-1 pin-wait-authority:58457b582bff04b077513ffcc97017c607a3316df1e9dbb650bdc9170514fed5
HX-EV-PIN-QUEUE-OWNERS-KEY-1 pin-queue-owners:324e73203400d68e446b7583c1f657b2b8f32ae3a914333c0bfbd7dd9e0b743d
HX-EV-PIN-QUEUE-PREDECESSOR-KEY-1 pin-queue-predecessor:26e21441b867934802755577af6e6d46f8c32a680947e6cd16197888feec8dd4
HX-EV-PIN-QUEUE-RECEIPT-KEY-1 pin-queue-receipt:c60ea12fe9292a3dd0a8ba0ecfad1a26bb5aa35d3fd3ee7d34c390d51a110bbb
HX-EV-PUBLICATION-RESUME-CLAIM-KEY-1 publication-resume-claim:a1b23b002d1e73474277ec1d46c056e4beb78c30d2987e42a28fe8c448c92d80
HX-EV-PUBLICATION-RESUME-STATE-KEY-1 publication-resume-state:88499acb84960e397ba46461a8ecd674b5bcabf9102e2a2bc4e4597eb0717579
HX-EV-PUBLICATION-WINDOW-KEY-1 publication-window:2177d29a302d44fbbcd4469627fba65a751c6b182d1fbf75acf98a35b65688f4
HX-EV-PUBLICATION-WINDOW-CLOSURE-KEY-1 publication-window-closure:58b1b024adae4973e90e4e491aa8713593c439d51b5eb3941eaf66950e0b73b9
HX-EV-PUBLICATION-RESUME-AUDIT-KEY-2 publication-resume-audit:9d7734f4f8ccb48cce924620e859cf4ba886f3c82a2948729320e57a99877104
HX-EV-RESUME-ORIGIN-KEY-1 resume-origin:422702346fa72ce8f7b750064ea0435e0ac9f57594d002dfb423547bda3d7cbd
HX-EV-RESUME-PREPARATION-KEY-1 resume-preparation:bf5b1e511ac948812047008f3e7db91643213bbe241d52248d477180ceb8b10f
HX-EV-RESUME-PREPARATION-HEAD-KEY-1 resume-preparation-head:2e00fb6e8b73edf9d9ac9306dc8fc86c336cd50f0f106b396f766ecb9c585f56
HX-EV-PUBLICATION-INVOCATION-KEY-1 publication-invocation:cf6992d150ef7d649ac08dae934c43ba3d9dcb1059c75f4f952a8f9593e20bb0
HX-EV-WINDOW-ATTEMPT-SET-KEY-1 window-attempt-set:54c75760d824af7953808abc25bb30e066be7c5198f6946c2ef6a9d7d001a401
HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1 legacy-resume-capsule-chunk:392440e0e1539887ff9e689b0259ab445652ed87ed69f4cf4e17144296ccd015
HX-EV-LEGACY-RESUME-CAPSULE-KEY-2 legacy-resume-capsule:bdd34978ca55de1163ea7224dd6cae35ee041002ab6d4cbe655090b4e544291a
HX-EV-LEGACY-PUBLICATION-RECOVERY-KEY-2 legacy-publication-recovery:c3b5c20b7bb59e35990d5f13bd7ec52ade5f62c9be460380ed27307f2b5e9c06
HX-EV-SUBSCRIPTION-POLICY-KEY-1 subscription-policy:b853890829d03903e4923fbb99b13d29a22cccfe60c443cf27c27036a04132fc
HX-EV-SUBSCRIPTION-POLICY-HEAD-KEY-1 subscription-policy-head:6a0899463333f3c6b8c6fa5796599b4bb65ba13474f0ca617776442e805ab5bb
HX-EV-HELD-DELIVERY-KEY-2 held-delivery:0844d50c11812e07883377ab05603413d2adbe4e771fb8d9f5aac2ed65e2da31
HX-EV-CARRIER-QUARANTINE-KEY-1 carrier-quarantine:fcf2853ce51eb6b9361ac5e4b6c7b71cfaa692d11eb0c386c8e17730e880ada8
HX-EV-REDRIVE-REQUEST-KEY-1 redrive-request:aa444bf208fe5084a573bdb8650075c5d70fb91d85a8659a6bc8f3b81781d1be
HX-EV-CAPTURE-ORIGIN-KEY-1 capture-origin:c7c95df2993b3321185727e8c3cdc427194e8cd1b8d89aaa4c4a7da57fe13706
HX-EV-CAPTURE-PREPARATION-KEY-1 capture-preparation:e39f31a903caddadb1d05ca07563e85e7725f3363b3a8c31734ec28a3a16c971
HX-EV-REDRIVE-ATTEMPT-KEY-1 redrive-attempt:b42756deb69e95ce7e5a1bbbbd8b611f1cfa41a04f1268a1eff54901498b67a0
HX-EV-REDRIVE-REPAIR-KEY-1 redrive-repair:458929ead8fd934fcfde8d56d2c1c0d18f6dc00502886ce4fa72288a3747d2a0
HX-EV-REDRIVE-REPAIR-INTEREST-KEY-1 redrive-repair-interest:c116d8e68e29b4f263793f384503a8bb8587e2fc314caf655c8562f10c4899c3
HX-EV-REDRIVE-CLEANUP-KEY-1 redrive-cleanup:b836aaf894ea3d860a820883a2519732b040ab276996f929240f044e33b1cb64
HX-EV-PROVIDER-NATIVE-CLEANUP-KEY-1 provider-native-cleanup:d846ca94b4dfffcf75235f683554659e2e8ab2af6a7527671e5a9e2f0cd74484
HX-EV-HOLD-ENTRY-KEY-1 hold-entry:7cb6172f70d32d253c2c2c99f882741a274b2996599cbe8a6df0ae22bbf5c1cb
HX-EV-HOLD-INDEX-KEY-1 hold-index:c3baa8825ec83a759f41f366f8d4959b8dc1eeb40528c554ecd18bcbc21e5caa
HX-EV-HOLD-DIRECTORY-KEY-1 hold-directory:da5aae42fda58bccb323318bb8df2fa7e0fdc768da29ebe26457a8dec9f0e538
```

```bash
python3 - <<'PY'
from hashlib import sha256
from pathlib import Path
from struct import pack
import re
import json

path = Path('_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md')
text = path.read_text(encoding='utf-8')
block = text.split('```text\nD06-activation ', 1)[1].split('\n```', 1)[0]
answers = {}
for line in ('D06-activation ' + block).splitlines():
    label, length, digest = line.split()
    answers[label] = (int(length), digest)
assert len(answers) == 30, len(answers)
extra_block = text.split('```text\nD46-chunk ',1)[1].split('\n```',1)[0]
extra_answers = {label:(int(length),digest) for label,length,digest in
                 (line.split() for line in ('D46-chunk '+extra_block).splitlines())}
assert len(extra_answers) == 12
key_block = text.split('```text\nD06-key ', 1)[1].split('\n```', 1)[0]
key_answers = {}
for line in ('D06-key ' + key_block).splitlines():
    label, digest = line.split()
    key_answers[label] = digest
assert len(key_answers) == 19

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
def KD(domain, *fields): return sha256(domain.encode() + b'\0\x01' + b''.join(fields)).hexdigest()
# Literal registry. The imported shared scope address is the sole unprefixed digest input.
physical_prefixes = {
'HX-EV-FULL-REPLAY-ACTIVATION-KEY-1':'full-replay-activation:',
'HX-EV-PUBLICATION-DRAIN-LIMIT-KEY-1':'publication-drain-limit:',
'HX-EV-PUBLICATION-DRAIN-RESOLUTION-KEY-1':'publication-drain-resolution:',
'HX-EV-PUBLICATION-DRAIN-HEAD-KEY-1':'publication-drain-head:',
'HX-EV-SCOPE-SHARD-USAGE-KEY-1':'scope-shard-usage:',
'HX-EV-LEGACY-SCOPE-CUTOVER-KEY-1':'legacy-scope-cutover:',
'HX-EV-LEGACY-SCOPE-CUTOVER-HEAD-KEY-1':'legacy-scope-cutover-head:',
'HX-EV-FIRST-SEND-MEMBERSHIP-KEY-1':'first-send-membership:',
'HX-EV-DESTINATION-CONFIG-KEY-1':'destination-config:',
'HX-EV-DESTINATION-CONFIG-HEAD-KEY-1':'destination-config-head:',
'HX-EV-PUBLICATION-CAPABILITY-KEY-1':'publication-retention-capability:',
'HX-EV-PUBLICATION-CAPABILITY-HEAD-KEY-1':'publication-retention-capability-head:',
'HX-EV-PUBLICATION-CHARGE-KEY-1':'publication-charge:',
'HX-EV-PUBLICATION-COUNTER-KEY-1':'publication-counter:',
'HX-EV-PIN-BATCH-RESERVATION-KEY-1':'pin-batch-reservation:',
'HX-EV-CAPACITY-SUBJECT-1':'capacity-subject:',
'HX-EV-PIN-CAPACITY-WAIT-KEY-1':'pin-capacity-wait:',
'HX-EV-PIN-CAPACITY-QUEUE-KEY-1':'pin-capacity-queue:',
'HX-EV-PIN-WAIT-AUTHORITY-KEY-1':'pin-wait-authority:',
'HX-EV-PIN-QUEUE-OWNERS-KEY-1':'pin-queue-owners:',
'HX-EV-PIN-QUEUE-PREDECESSOR-KEY-1':'pin-queue-predecessor:',
'HX-EV-PIN-QUEUE-RECEIPT-KEY-1':'pin-queue-receipt:',
'HX-EV-PUBLICATION-RESUME-CLAIM-KEY-1':'publication-resume-claim:',
'HX-EV-PUBLICATION-RESUME-STATE-KEY-1':'publication-resume-state:',
'HX-EV-PUBLICATION-WINDOW-KEY-1':'publication-window:',
'HX-EV-PUBLICATION-WINDOW-CLOSURE-KEY-1':'publication-window-closure:',
'HX-EV-PUBLICATION-RESUME-AUDIT-KEY-2':'publication-resume-audit:',
'HX-EV-RESUME-ORIGIN-KEY-1':'resume-origin:',
'HX-EV-RESUME-PREPARATION-KEY-1':'resume-preparation:',
'HX-EV-RESUME-PREPARATION-HEAD-KEY-1':'resume-preparation-head:',
'HX-EV-PUBLICATION-INVOCATION-KEY-1':'publication-invocation:',
'HX-EV-WINDOW-ATTEMPT-SET-KEY-1':'window-attempt-set:',
'HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1':'legacy-resume-capsule-chunk:',
'HX-EV-LEGACY-RESUME-CAPSULE-KEY-2':'legacy-resume-capsule:',
'HX-EV-LEGACY-PUBLICATION-RECOVERY-KEY-2':'legacy-publication-recovery:',
'HX-EV-SUBSCRIPTION-POLICY-KEY-1':'subscription-policy:',
'HX-EV-SUBSCRIPTION-POLICY-HEAD-KEY-1':'subscription-policy-head:',
'HX-EV-HELD-DELIVERY-KEY-2':'held-delivery:',
'HX-EV-CARRIER-QUARANTINE-KEY-1':'carrier-quarantine:',
'HX-EV-REDRIVE-REQUEST-KEY-1':'redrive-request:',
'HX-EV-CAPTURE-ORIGIN-KEY-1':'capture-origin:',
'HX-EV-CAPTURE-PREPARATION-KEY-1':'capture-preparation:',
'HX-EV-REDRIVE-ATTEMPT-KEY-1':'redrive-attempt:',
'HX-EV-REDRIVE-REPAIR-KEY-1':'redrive-repair:',
'HX-EV-REDRIVE-REPAIR-INTEREST-KEY-1':'redrive-repair-interest:',
'HX-EV-REDRIVE-CLEANUP-KEY-1':'redrive-cleanup:',
'HX-EV-PROVIDER-NATIVE-CLEANUP-KEY-1':'provider-native-cleanup:',
'HX-EV-HOLD-ENTRY-KEY-1':'hold-entry:',
'HX-EV-HOLD-INDEX-KEY-1':'hold-index:',
'HX-EV-HOLD-DIRECTORY-KEY-1':'hold-directory:',
}
def K(domain,*fields):
    assert domain in physical_prefixes, ('unregistered-prefix',domain)
    return physical_prefixes[domain]+KD(domain,*fields)
def command_scope_address(tenant,execution_message_id):
    return 'command-execution-scope:'+sha256(U(tenant)+U(execution_message_id)).hexdigest()

def R(domain, count, *fields):
    assert len(fields) == count
    return domain.encode() + b'\0\x01' + count.to_bytes(2, 'big') + b''.join(bytes([n]) + field for n, field in enumerate(fields, 1))

def unresolved_root(members):
    rows = sorted(members,key=lambda row:row[0])
    assert len({p for p,m,body in rows}) == len(rows) and len({m for p,m,body in rows}) == len(rows)
    assert all(0 < p < 2**32 for p,m,body in rows)
    encoded = b''.join(pack('>I',p)+U(m)+sha256(body).digest() for p,m,body in rows)
    return sha256(b'HX-EV-PUBLICATION-UNRESOLVED-1\0\x01'+pack('>I',len(rows))+encoded).digest()

def destination_config(raw, component, topic):
    assert isinstance(raw,bytes) and len(raw) <= 64*1024, 'destination-document-length'
    def unique(pairs):
        assert len({key for key,value in pairs}) == len(pairs), 'destination-duplicate-key'
        return dict(pairs)
    value=json.loads(raw.decode('utf-8',errors='strict'),object_pairs_hook=unique)
    assert isinstance(value,dict) and set(value)=={'component','metadata','schema','topic'}, 'destination-fields'
    assert value['schema']=='hexalith.eventstore.destination/1', 'destination-schema'
    for name,expected in (('component',component),('topic',topic)):
        actual=value[name]
        assert isinstance(actual,str) and 1 <= len(actual.encode()) <= 1024, 'destination-identifier-length'
        assert actual.encode()==expected.encode(), 'destination-outbox-binding'
    metadata=value['metadata']
    assert isinstance(metadata,dict) and len(metadata)<=64, 'destination-metadata-count'
    assert all(isinstance(key,str) and isinstance(value,str) and len(key.encode())<=16384
        and len(value.encode())<=16384 for key,value in metadata.items()), 'destination-metadata-string-length'
    assert sum(len(key.encode())+len(value.encode()) for key,value in metadata.items())<=16384, 'destination-metadata-total-length'
    assert json.dumps(value,ensure_ascii=False,sort_keys=True,separators=(',',':')).encode()==raw, 'destination-canonical-bytes'
    return value

def window_admission_bytes(members):
    rows=sorted(members,key=lambda row:row[0])
    raw=pack('>I',len(rows))+b''.join(pack('>I',p)+U(m)+sha256(body).digest() for p,m,body in rows)
    assert 0 < len(rows) <= 59 and len(raw)<=64*1024
    return raw

def window_progress_bytes(admission, accepted, unresolved,previous_accepted=()):
    def rows(members): return sorted((p,m,sha256(body).digest()) for p,m,body in members)
    parts=[sha256(admission).digest()]
    for members in (previous_accepted,accepted,unresolved):
        exact=rows(members)
        parts.append(pack('>I',len(exact))+B(b''.join(pack('>I',p)+U(m)+d for p,m,d in exact)))
    body=b''.join(parts)
    assert len(body)+32<=128*1024
    return body+sha256(b'fixture-authenticated-current-window-progress:'+body).digest()

def window_progress_read(raw,admission,accepted,unresolved):
    assert isinstance(raw,bytes) and 88<=len(raw)<=128*1024
    assert raw[-32:]==sha256(b'fixture-authenticated-current-window-progress:'+raw[:-32]).digest(), 'window-progress-provider-authority'
    assert raw[:32]==sha256(admission).digest(), 'window-progress-admission-binding'
    offset=32; sets=[]
    for _ in range(3):
        assert offset+8<=len(raw)-32
        count=int.from_bytes(raw[offset:offset+4],'big'); length=int.from_bytes(raw[offset+4:offset+8],'big'); offset+=8
        assert length<=len(raw)-32-offset
        rows=decode_rows(raw[offset:offset+length],count,['P','U','B32'],count_ceiling=59); offset+=length
        assert rows==sorted(rows) and len({p for p,m,d in rows})==len(rows) and len({m for p,m,d in rows})==len(rows)
        sets.append(set(rows))
    assert offset==len(raw)-32
    prior,current,pending=sets
    assert prior<=current, 'window-progress-accepted-regression'
    assert current=={(p,m,sha256(body).digest()) for p,m,body in accepted}
    assert pending=={(p,m,sha256(body).digest()) for p,m,body in unresolved}, 'window-progress-current-binding'
    return True

def window_admission_members(raw, claim, committed, accepted, unresolved,progress):
    assert isinstance(raw,bytes) and 4 <= len(raw)<=64*1024, 'window-admission-length'
    count=int.from_bytes(raw[:4],'big')
    rows=decode_rows(raw[4:],count,['P','U','B32'],count_ceiling=59)
    assert rows and rows==sorted(rows) and len({p for p,m,d in rows})==len(rows) and len({m for p,m,d in rows})==len(rows)
    assert all(p>0 for p,m,d in rows)
    assert sha256(b'HX-EV-PUBLICATION-UNRESOLVED-1\0\x01'+raw).digest()==claim[7], 'window-admission-root'
    exact={(p,m,sha256(body).digest()) for p,m,body in committed}
    admitted=set(rows)
    assert admitted<=exact, 'window-admission-outbox-members'
    current={(p,m,sha256(body).digest()) for p,m,body in unresolved}
    accepted_now={(p,m,sha256(body).digest()) for p,m,body in accepted}
    assert current<=admitted and exact-admitted<=accepted_now, 'window-progress-monotonic'
    assert current|accepted_now==exact and not current&accepted_now, 'window-progress-partition'
    window_progress_read(progress,raw,accepted,unresolved)
    return tuple(rows)

t = 638712864000000000
z = bytes(32)
MiB = 1024 * 1024
vectors = {}
arow = pack('>I', 1) + U('route-a') + U('continue-full-replay') + N(100) + N(4096) + N(819200) + O(None)
vectors['D06-activation'] = R('HX-EV-FULL-REPLAY-ACTIVATION-2', 9, U('admin'), U('d'), B32(H('registry')), N(1), B32(z), B(arow), Q(t), U('operator'), Q(t+1))
vectors['D12-legacy-claim'] = R('HX-EV-COMMAND-SCOPE-LEGACY-2', 10, U('t'), U('op'), U('d'), U('a'), U('increment'), B32(H('payload')), Q(t), Q(t+864000000000), N(1), B32(z))
vectors['D12-cutover'] = R('HX-EV-LEGACY-SCOPE-CUTOVER-1', 8, U('*'), U('d'), N(1), Q(t), Q(t+864000000000), N(86400), B32(H('empty-inventory')), B32(z))
vectors['D12-usage'] = R('HX-EV-SCOPE-SHARD-USAGE-1', 7, U('t'), N(7), N(2), N(1), N(14336), N(3), B32(H('usage-prev')))
vectors['D12-tombstone'] = R('HX-EV-COMMAND-SCOPE-TOMBSTONE-2', 8, U('t'), U('op'), B32(H('scope')), B32(H('input')), B32(H('full-scope')), Q(t), Q(t+315360000000000), N(sha256(U('t')+U('op')).digest()[0]))
vectors['D14-drain-limit'] = R('HX-EV-PUBLICATION-DRAIN-LIMIT-2', 10, U('t'), B32(H('scope')), U('operation'), N(2), N(16), B32(H('drain-head')), B32(H('outcome-head')), N(4), U('pending'), Q(t))
vectors['D14-drain-resolution'] = R('HX-EV-PUBLICATION-DRAIN-LIMIT-RESOLUTION-1', 8, U('t'), B32(H('scope')), B32(H('drain-limit')), U('resumed'), B32(H('window-intent')), N(2), U('coordinator'), Q(t))
vectors['D16-membership-resolution'] = R('HX-EV-FIRST-SEND-MEMBERSHIP-RESOLUTION-1', 12, U('t'), B32(H('scope')), N(1), U('event-1'), B32(H('pin')), N(2), B32(H('previous')), B32(H('membership')), B32(H('zero-send')), U('ContinueSamePin'), U('broker'), Q(t))
vectors['D17-destination-config'] = b'{"component":"pubsub","metadata":{},"schema":"hexalith.eventstore.destination/1","topic":"orders"}'
vectors['D29-capability'] = R('HX-EV-PUBLICATION-RETENTION-CAPABILITY-2', 15, U('deployment-a'), N(3), B(b'backend'), N(1024*MiB), N(2048*MiB), N(256*MiB), N(512*MiB), N(MiB), N(128*MiB), B32(H('cap-prev')), Q(t), N(256), N(31536000), N(50000), N(256*MiB))
vectors['D29-charge'] = R('HX-EV-PUBLICATION-CHARGE-2', 15, U('deployment-a'), U('tenant'), U('t'), B32(H('object')), U('pin-batch'), N(10*MiB), N(MiB), N(11*MiB), N(3), N(1), U('active'), B32(z), O(None), Q(t), N(0))
vectors['D29-counter'] = R('HX-EV-PUBLICATION-COUNTER-1', 8, U('deployment-a'), U('tenant'), U('t'), N(11*MiB), N(1), N(4), B32(H('counter-prev')), Q(t))
prow = pack('>I', 1) + U('event-1') + B32(H('pin')) + N(10*MiB) + N(11*MiB)
vectors['D29-pin-batch'] = R('HX-EV-PIN-BATCH-RESERVATION-2', 13, U('t'), B32(H('scope')), B32(sha256(prow).digest()), N(1), B(prow), N(11*MiB), N(3), B32(H('tenant-counter')), B32(H('tenant-pool-counter')), B32(H('deployment-counter')), U('reserved'), N(1), Q(t))
vectors['D31-wait'] = R('HX-EV-PIN-CAPACITY-WAIT-2', 12, U('t'), B32(H('scope')), B32(H('batch')), N(11*MiB), U('deployment'), N(42), Q(t), N(0), U('queued'), B32(H('capability')), B32(H('outbox-plan')), Q(t))
qrow = N(42) + U('t') + B32(H('scope')) + U('queued')
vectors['D31-queue'] = R('HX-EV-PIN-CAPACITY-QUEUE-4', 11, U('deployment-a'), U('deployment'), N(1), N(1), B(qrow), N(0), N(0), N(50000), N(42), B32(z), Q(t))
vectors['D36-policy'] = R('HX-EV-SUBSCRIPTION-DELIVERY-POLICY-3', 13, U('deployment-a'), U('pubsub'), U('orders'), U('sub-a'), N(1), B32(z), U('initial'), U('dead-letter-capture'), O(U('orders-dlq')), N(8), B32(H('subscription-config')), U('dapr-configuration'), Q(t))
carrier = b'exact-carrier-and-headers'
vectors['D36-held'] = R('HX-EV-HELD-DELIVERY-4', 22, U('tenant'), U('deployment-a'), O(U('t')), O(U('event-1')), U('pubsub'), U('orders'), U('sub-a'), B32(H('policy')), U('captured'), U('handler-capability-hold'), N(len(carrier)), B32(sha256(carrier).digest()), O(U('archive-a')), O(U('objects/held-1')), O(B32(H('object-readback'))), O(B32(H('handoff'))), Q(t), N(2), N(1), O(None), Q(t+600000000), N(2))
body = b'quarantined-body'
manifest = pack('>I', 1) + U('x-header') + N(4) + B32(sha256(b'value').digest())
vectors['D36-quarantine'] = R('HX-EV-CARRIER-QUARANTINE-2', 14, U('deployment'), O(None), U('sub-a'), U('invalid-header-value'), N(len(body)), B32(sha256(body).digest()), B(manifest), U('archive-a'), U('objects/quarantine-1'), B32(H('provider-proof')), O(U('event-1')), Q(t), U('terminal-quarantine'), B32(H('delivery-receipt')))
vectors['D36-redrive'] = R('HX-EV-REDRIVE-REQUEST-2', 7, U('admin'), U('tenant'), O(U('t')), B32(H('held-key')), N(2), U('operator'), Q(t))
vectors['D37-entry'] = R('HX-EV-HOLD-ENTRY-2', 13, U('tenant'), U('t'), U('PublicationPinCapacityHold'), U('scope:abc'), O(U('d')), U('publication_pin_capacity_hold'), N(1), B32(z), Q(t), Q(t), N(1), U('quota-coordinator'), Q(t+36000000000))
irow = Q(t) + U('PublicationPinCapacityHold') + U('scope:abc') + B32(H('hold-entry'))
vectors['D37-index'] = R('HX-EV-HOLD-INDEX-2', 8, U('tenant'), U('t'), N(1), N(1), B(irow), N(0), B32(z), Q(t))
actor = U('tenant:' + sha256(U('t')).hexdigest())
vectors['D37-directory'] = R('HX-EV-HOLD-DIRECTORY-1', 7, N(7), N(1), N(1), B(actor), B32(z), Q(t), N(0))
vectors['D37-key'] = U('tenant') + U('t') + U('PublicationPinCapacityHold') + U('scope:abc')
request_carrier = R('HX-EV-PUBLICATION-RESUME-CARRIER-1', 5, U('t'), U('hxrsm1-handle'), B32(H('hold')), U('retry-0001'), U('retry after broker repair'))
request_identity = sha256(b'HX-EV-PUBLICATION-RESUME-IDENTITY-1\0\x01' + U('t') + U('hxrsm1-handle') + U('retry-0001')).digest()
request = R('HX-EV-PUBLICATION-RESUME-3', 15, U('admin'), U('t'), U('op'), B32(H('scope')), U('retry-exhausted'), B32(H('hold')), B32(H('head')), N(2), B32(H('audit-prev')), U('hxrsm1-handle'), B32(request_identity), B32(sha256(request_carrier).digest()), U('operator'), Q(t), Q(t+9000000000))
vectors['D45-request'] = request
attempt_row = pack('>I', 1) + N(4) + B32(sha256(b'definitive-result').digest())
attempt_records = []
for local in range(1,5):
    for observation,kind in enumerate(('register','unknown','result')):
        evidence = sha256(b'definitive-result').digest() if (local,kind) == (4,'result') else H(f'{local}:{kind}')
        attempt_records.append(pack('>I',1)+N(local)+N(observation)+U(kind)+H(f'parent-{local}')+H(f'send-{local}')+evidence)
attempt_rows = b''.join(attempt_records)
attempt_root = sha256(b'HX-EV-WINDOW-ATTEMPTS-2\0\x01'+U('t')+H('scope')+N(1)+H('roster')+N(12)+B(attempt_rows)).digest()
attempt_set = R('HX-EV-WINDOW-ATTEMPT-SET-1',8,U('t'),H('scope'),N(1),H('roster'),N(12),B(attempt_rows),attempt_root,Q(t))
assert len(attempt_set) == 1691 and sha256(attempt_set).hexdigest() == '99c18639aebb9701710692fc62663bee36f9954dd550dfb773f33a7c1bb95fa3'
attempt_evidence_store = {('t',H('scope'),1):attempt_set}
broker_auth = b'authenticated-broker-window-proof'
closure = R('HX-EV-PUBLICATION-WINDOW-CLOSURE-3', 11, U('t'), B32(H('scope')), N(1), B(pack('>I',1)+attempt_row), B32(sha256(b'broker-fence').digest()), B32(sha256(b'producer-disable').digest()), B32(sha256(b'empty-state').digest()), B32(attempt_root), U('SignedCarrier'), B32(z), Q(t))
history = sha256(b'HX-EV-PUBLICATION-WINDOW-HISTORY-1\0\x01' + B32(z) + B(closure) + B(broker_auth)).digest()
prior_state = R('HX-EV-PUBLICATION-RESUME-STATE-3', 14, U('t'), U('op'), B32(H('scope')), N(1), N(1), N(16), B32(H('hold-1')), B32(H('window-1')), B32(z), N(0), B32(z), B(pack('>I', 0)), B(pack('>I', 0)), Q(t-1))
window = R('HX-EV-PUBLICATION-WINDOW-2', 13, U('t'), B32(H('scope')), U('operation'), N(2), B32(sha256(closure).digest()), B32(sha256(prior_state).digest()), B32(request_identity), B32(unresolved_root(((1,'event-1',b'canonical-stored-event'),))), B32(H('policy')), N(16), U('admin'), Q(t), B32(H('capability')))
audit = R('HX-EV-PUBLICATION-RESUME-AUDIT-4', 10, U('t'), U('op'), N(2), B32(request_identity), B32(sha256(request_carrier).digest()), B32(sha256(prior_state).digest()), O(B32(sha256(closure).digest())), N(2), N(24), Q(t))
retry_row = B32(request_identity) + B32(sha256(request_carrier).digest()) + N(2) + B32(sha256(audit).digest()) + N(2) + N(24) + Q(t+9000000000)
state = R('HX-EV-PUBLICATION-RESUME-STATE-3', 14, U('t'), U('op'), B32(H('scope')), N(2), N(2), N(24), B32(z), B32(sha256(window).digest()), B32(history), N(1), B32(sha256(audit).digest()), B(pack('>I', 1)+retry_row), B(pack('>I', 0)), Q(t))
vectors['D45-window'] = window
vectors['D45-closure'] = closure
vectors['D45-state'] = state
vectors['D45-audit'] = audit
stored = b'canonical-stored-event'
member = N(10) + U('event-1') + B32(sha256(stored).digest())
counted_members = pack('>I', 1) + member
event_root = sha256(b'HX-EV-LEGACY-RESUME-EVENTS-2\0\x01' + B(counted_members)).digest()
source_hash = H('drain-source')
capsule_identity = sha256(b'HX-EV-LEGACY-RESUME-CAPSULE-IDENTITY-1\0\x01' + U('t') + U('d') + U('a') + U('tracking') + B32(source_hash)).digest()
chunk_root = sha256(b'HX-EV-LEGACY-RESUME-CHUNK-ROWS-1\0\x01' + B(member)).digest()
chunk = R('HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-1', 5, B32(capsule_identity), N(0), N(1), B(member), B32(chunk_root))
assert len(chunk) == 179 and sha256(chunk).hexdigest() == 'b283d4e7b0025e99a6258569483c2d2b8555332de17f355491e3c4da404d4cd8'
chunk_key = 'legacy-resume-capsule-chunk:' + KD('HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1', B32(capsule_identity), N(0))
manifest_row = N(0) + N(10) + N(1) + B32(sha256(chunk).digest()) + N(len(chunk)) + U(chunk_key)
manifest = pack('>I', 1) + manifest_row
capsule = R('HX-EV-LEGACY-RESUME-CAPSULE-2', 16, U('t'), U('d'), U('a'), U('tracking'), O(U('op')), U('correlation'), U('increment'), U('success-events'), N(10), N(10), I(1), B32(event_root), B(manifest), U('drain-exhaustion'), B32(source_hash), Q(t))
vectors['D46-capsule'] = capsule
vectors['D46-recovery'] = R('HX-EV-LEGACY-PUBLICATION-RECOVERY-3', 13, U('t'), B32(sha256(capsule).digest()), U('hxrsm1-handle'), N(3), N(2), U('legacy-resume'), U('claimed'), B32(H('drain-record')), O(B32(H('dead-letter'))), B32(H('recovery-prev')), O(None), O(None), Q(t))

keys = {
    'D06-key': KD('HX-EV-FULL-REPLAY-ACTIVATION-KEY-1', U('d'), B32(H('registry')), N(1)),
    'D14-key': KD('HX-EV-PUBLICATION-DRAIN-LIMIT-KEY-1', B32(H('scope')), N(2), N(16)),
    'D45-closure-key': KD('HX-EV-PUBLICATION-WINDOW-CLOSURE-KEY-1', B32(H('scope')), N(1)),
    'D45-audit-key': KD('HX-EV-PUBLICATION-RESUME-AUDIT-KEY-2', U('t'), U('op'), B32(request_identity)),
    'D46-capsule-key': KD('HX-EV-LEGACY-RESUME-CAPSULE-KEY-2', B32(capsule_identity)),
    'D46-recovery-key': KD('HX-EV-LEGACY-PUBLICATION-RECOVERY-KEY-2', B32(sha256(capsule).digest())),
    'D45-origin-key': KD('HX-EV-RESUME-ORIGIN-KEY-1',U('t'),U('op'),B32(request_identity)),
    'D45-preparation-key': KD('HX-EV-RESUME-PREPARATION-KEY-1',U('t'),U('op'),B32(request_identity)),
    'D45-preparation-head-key': KD('HX-EV-RESUME-PREPARATION-HEAD-KEY-1',U('t'),U('op')),
    'D45-invocation-key': KD('HX-EV-PUBLICATION-INVOCATION-KEY-1',U('t'),U('op'),H('invocation')),
    'D45-attempt-set-key': KD('HX-EV-WINDOW-ATTEMPT-SET-KEY-1',U('t'),H('scope'),N(1)),
    'D36-capture-origin-key': KD('HX-EV-CAPTURE-ORIGIN-KEY-1',H('held-key')),
    'D36-capture-preparation-key': KD('HX-EV-CAPTURE-PREPARATION-KEY-1',H('held-key')),
    'D36-repair-key': KD('HX-EV-REDRIVE-REPAIR-KEY-1',H('held-key'),N(2)),
    'D36-repair-interest-key': KD('HX-EV-REDRIVE-REPAIR-INTEREST-KEY-1',H('held-key')),
    'D36-attempt-key': KD('HX-EV-REDRIVE-ATTEMPT-KEY-1',H('held-key')),
    'D36-request-key': KD('HX-EV-REDRIVE-REQUEST-KEY-1',H('held-key')),
    'D36-cleanup-key': KD('HX-EV-REDRIVE-CLEANUP-KEY-1',H('held-key')),
    'D46-chunk-key': KD('HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1',B32(capsule_identity),N(0)),
}

assert set(vectors) == set(answers)
computed = {label:(len(value), sha256(value).hexdigest()) for label, value in vectors.items()}
mismatches = {label:(computed[label], answers[label]) for label in vectors if computed[label] != answers[label]}
assert not mismatches, mismatches
for label, value in vectors.items():
    mutant = value[:-1] + bytes([value[-1] ^ 1])
    assert sha256(mutant).hexdigest() != answers[label][1]
assert keys == key_answers
assert KD('HX-EV-PUBLICATION-DRAIN-LIMIT-KEY-1', B32(H('scope')), N(1), N(23)) != KD('HX-EV-PUBLICATION-DRAIN-LIMIT-KEY-1', B32(H('scope')), N(12), N(3))

def decode_rows(raw, count, schema, maxima=None, count_ceiling=50000):
    offset = 0
    maxima = maxima or [1024]*len(schema)
    def take(length):
        nonlocal offset
        assert 0 <= length <= len(raw)-offset
        value = raw[offset:offset+length]; offset += length
        return value
    def field(kind, maximum):
        if isinstance(kind, tuple):
            marker = take(1); assert marker in {b'\x00',b'\x01'}
            return None if marker == b'\x00' else field(kind[1],maximum)
        if kind == 'U':
            length = int.from_bytes(take(4),'big'); assert 1 <= length <= maximum
            value = take(length).decode('utf-8'); assert value.strip() and '\x00' not in value
            return value
        if kind == 'B32': return take(32)
        if kind == 'P': return int.from_bytes(take(4),'big')
        if kind in {'N','Q'}: return int.from_bytes(take(8),'big',signed=kind == 'Q')
        raise AssertionError(kind)
    assert 0 <= count <= count_ceiling
    rows = [tuple(field(kind,maximum) for kind,maximum in zip(schema,maxima)) for _ in range(count)]
    assert offset == len(raw)
    return rows

record_caps = {'HX-EV-COMMAND-SCOPE-LEGACY-2':8192,'HX-EV-COMMAND-SCOPE-TOMBSTONE-2':4096,
    'HX-EV-PIN-WAIT-PREPARATION-1':4096,'HX-EV-PIN-WAIT-AUTHORITY-1':16384,
    'HX-EV-PIN-QUEUE-OWNERS-1':4*MiB,'HX-EV-PIN-QUEUE-PREDECESSOR-1':74*MiB,'HX-EV-PIN-QUEUE-RECEIPT-1':1024,'HX-EV-FULL-REPLAY-ACTIVATION-2':MiB,
    'HX-EV-WINDOW-ATTEMPT-SET-1':64*MiB,'HX-EV-PUBLICATION-INVOCATION-1':4096,
    'HX-EV-RESUME-PREPARATION-1':MiB,
    'HX-EV-RESUME-ORIGIN-1':256*1024,'HX-EV-RESUME-PREPARATION-HEAD-1':8*1024,
    'HX-EV-CAPTURE-PREPARATION-1':8*1024,'HX-EV-REDRIVE-REPAIR-1':8*1024,
    'HX-EV-CAPTURE-ORIGIN-1':8*1024,'HX-EV-REDRIVE-ATTEMPT-1':8*1024,'HX-EV-REDRIVE-CLEANUP-1':1024,
    'HX-EV-REDRIVE-REQUEST-2':3*1024,
    'HX-EV-PIN-CAPACITY-QUEUE-4':64*MiB,'HX-EV-HOLD-INDEX-2':40*MiB,
    'HX-EV-HOLD-DIRECTORY-1':64*MiB,'HX-EV-CARRIER-QUARANTINE-2':128*1024,
    'HX-EV-LEGACY-RESUME-CAPSULE-2':128*1024,'HX-EV-PUBLICATION-RESUME-STATE-3':32*1024,
    'HX-EV-HELD-DELIVERY-4':32*1024,'HX-EV-HOLD-ENTRY-2':8*1024,'HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-1':64*1024,
    'HX-EV-PUBLICATION-WINDOW-CLOSURE-3':64*1024,'HX-EV-PIN-BATCH-RESERVATION-2':64*1024,
    'HX-EV-PUBLICATION-RETENTION-CAPABILITY-2':64*1024,
    'HX-EV-FIRST-SEND-MEMBERSHIP-RESOLUTION-1':16*1024,
    'HX-EV-SUBSCRIPTION-DELIVERY-POLICY-3':16*1024,'HX-EV-PUBLICATION-WINDOW-2':16*1024,
    'HX-EV-SCOPE-SHARD-USAGE-1':2048,'HX-EV-PUBLICATION-COUNTER-1':4096}
# All keys are zero-based field indexes. Other U fields use the imported 1,024-byte bound.
field_maxima = {('HX-EV-PUBLICATION-COUNTER-1',2):1031,
    ('HX-EV-PIN-WAIT-AUTHORITY-1',6):4096,('HX-EV-PIN-WAIT-AUTHORITY-1',18):1024,('HX-EV-PIN-WAIT-AUTHORITY-1',19):1024,
    ('HX-EV-PIN-QUEUE-OWNERS-1',1):1031,('HX-EV-PIN-QUEUE-PREDECESSOR-1',1):1031,('HX-EV-PIN-QUEUE-RECEIPT-1',0):128,('HX-EV-RESUME-ORIGIN-1',10):8192,('HX-EV-HELD-DELIVERY-4',13):4096,('HX-EV-CARRIER-QUARANTINE-2',8):4096,
    ('HX-EV-REDRIVE-REPAIR-1',4):4096,
    ('HX-EV-REDRIVE-ATTEMPT-1',3):4096,
    ('HX-EV-HOLD-ENTRY-2',3):4096,('HX-EV-PIN-CAPACITY-QUEUE-4',1):1031,
    ('HX-EV-PUBLICATION-RESUME-CARRIER-1',3):128,('HX-EV-PUBLICATION-RESUME-CARRIER-1',4):512,
    ('HX-EV-FULL-REPLAY-ACTIVATION-2',7):256,('HX-EV-PUBLICATION-RESUME-3',12):256,
    ('HX-EV-REDRIVE-REQUEST-2',5):256}
hold_reasons = {
    'ResumeAttemptCollectionHold':'resume_evidence_hold','QuotaGenerationIncident':'quota_generation_exhausted',
    'LegacyArrayLimit':'legacy_array_limit','ActivationInventoryCapacityHold':'full_replay_inventory_capacity',
    'AdmissionEvidenceHold':'admission_evidence_hold','ResponsePreparationHold':'response_preparation_hold',
    'OutcomeEvidenceHold':'outcome_evidence_hold','OutcomeEvidenceConflict':'outcome_evidence_conflict',
    'TerminalEvidenceHold':'terminal_evidence_hold','PublicationRetryExhaustedHold':'publication_retry_exhausted_hold',
    'PublicationResumePreparationHold':'publication_resume_preparation_hold',
    'RedriveEvidenceRepairHold':'redrive_evidence_repair_hold',
    'PublicationDrainLimitHold':'publication_drain_limit_hold','PublicationPinCapacityHold':'publication_pin_capacity_hold',
    'PinCapacityQueueCorruptionHold':'pin_capacity_queue_corruption_hold',
    'FirstSendMembershipChangedHold':'first_send_membership_changed_hold',
    'ScopeRetentionCapacityHold':'scope_retention_capacity_hold','LegacyResumeIncident':'legacy_resume_evidence_unavailable'}
held_reasons = {'handler-capability-hold','raw-source-unavailable','delivery-carrier-limit-hold',
    'invalid-header-value','invalid-carrier','oversize-carrier','delivery_above_advertised_max'}

def capture_projection(state):
    result = dict(state)
    result['identity'] = tuple(None if value is None else sha256(U(value)).digest() for value in state['identity'])
    result['account'] = sha256(U(state['account'])).digest()
    return result

def canonical_image_bytes(value):
    def encode(item):
        if isinstance(item,bytes): return ['bytes',item.hex()]
        if isinstance(item,tuple): return ['tuple',[encode(v) for v in item]]
        if isinstance(item,list): return ['list',[encode(v) for v in item]]
        if isinstance(item,dict):
            rows = [(encode(k),encode(v)) for k,v in item.items()]
            return ['map',sorted(rows,key=lambda row:json.dumps(row[0],ensure_ascii=False,separators=(',',':')).encode())]
        assert item is None or type(item) in {str,int,bool}
        return ['scalar',item]
    return json.dumps(encode(value),ensure_ascii=False,separators=(',',':')).encode()

def read_canonical_image(raw):
    def decode(item):
        assert isinstance(item,list) and len(item) == 2
        tag,value = item
        if tag == 'bytes':
            assert isinstance(value,str) and re.fullmatch(r'(?:[0-9a-f]{2})*',value)
            return bytes.fromhex(value)
        if tag in {'tuple','list'}:
            assert isinstance(value,list)
            decoded = [decode(v) for v in value]
            return tuple(decoded) if tag == 'tuple' else decoded
        if tag == 'map':
            assert isinstance(value,list) and all(isinstance(row,list) and len(row) == 2 for row in value)
            rows = [(decode(k),decode(v)) for k,v in value]
            assert len({k for k,v in rows}) == len(rows)
            return dict(rows)
        assert tag == 'scalar' and (value is None or type(value) in {str,int,bool})
        return value
    result = decode(json.loads(raw))
    assert canonical_image_bytes(result) == raw
    return result

def continuation_projection(state):
    from copy import deepcopy
    result = deepcopy(state)
    for row in result.get('live',{}).values(): row.pop('response',None)
    if result.get('reconciliation') is not None:
        for row in result['reconciliation']['live'].values(): row.pop('response',None)
    return result

def continuation_image(raw):
    assert len(raw) <= 128832, 'continuation-derived-length'
    state = read_canonical_image(raw)
    required = {'tenant','handle','hold_source','ordinal','window','closed','limit','active_charge',
                'next_charge','charge_ceiling','live','tombstones','audits','invocations','window_claim'}
    assert isinstance(state,dict) and required <= set(state)
    assert set(state) <= required | {'used_charge','reconciliation','history','last_audit','legacy_root','invocation_owners'}
    for name in ('tenant','handle'):
        assert isinstance(state[name],str) and 1 <= len(state[name].encode()) <= 1024
    for name in ('ordinal','window','closed','limit','active_charge','next_charge','charge_ceiling','audits'):
        assert type(state[name]) is int and 0 <= state[name] < 2**64
    for name in ('hold_source','window_claim','history','last_audit','legacy_root'):
        if name in state: assert isinstance(state[name],bytes) and len(state[name]) == 32
    assert state['limit'] > 0 and len(state['invocations']) <= 64
    assert all(isinstance(value,bytes) and len(value) == 32 for value in state['invocations'])
    if 'invocation_owners' in state:
        assert len(state['invocation_owners'])==len(state['invocations'])<=64
        assert len(set(state['invocation_owners']))==len(state['invocation_owners'])
        assert all(isinstance(v,bytes) and len(v)==32 for v in state['invocation_owners'])
    def bounded_rows(live,expired):
        assert isinstance(live,dict) and isinstance(expired,dict) and len(live)+len(expired) <= 64
        assert not set(live)&set(expired)
        for identity,row in live.items():
            assert isinstance(identity,bytes) and len(identity) == 32
            assert set(row) == {'carrier_hash','result','expires_at'}
            assert len(row['carrier_hash']) == 32 and type(row['expires_at']) is int and 0 <= row['expires_at'] < 2**63
            result = row['result']; assert set(result) == {'ordinal','window','limit','audit_hash'}
            assert len(result['audit_hash']) == 32
            assert all(type(result[key]) is int and 0 <= result[key] < 2**64 for key in ('ordinal','window','limit'))
        for identity,row in expired.items():
            assert isinstance(identity,bytes) and len(identity) == 32 and len(row['carrier_hash']) == 32
            assert set(row) == {'carrier_hash','expires_at','delete_after'}
            assert all(type(row[key]) is int and 0 <= row[key] < 2**63 for key in ('expires_at','delete_after'))
            assert row['delete_after']-row['expires_at'] == 30*86400
    bounded_rows(state['live'],state['tombstones'])
    if 'invocation_owners' in state: assert set(state['invocation_owners'])<=set(state['live'])|set(state['tombstones']), 'continuation-invocation-owner-binding'
    if state.get('reconciliation') is not None:
        proof = state['reconciliation']; assert set(proof) in ({'at','live','tombstones','receipt'},{'at','live','tombstones','receipt','invocation_root'})
        if 'invocation_root' in proof: assert isinstance(proof['invocation_root'],bytes) and len(proof['invocation_root'])==32
        assert type(proof['at']) is int and 0 <= proof['at'] < 2**63 and len(proof['receipt']) == 32
        bounded_rows(proof['live'],proof['tombstones'])
    # Worst canonical image: identifiers <= 12,288 bytes after JSON escaping;
    # each of 64 rows <= 768 bytes, duplicated at most once by reconciliation;
    # 64 invocation hashes <= 5,120 bytes; fixed map/optional fields <= 8,000.
    assert len(raw) <= 12288+2*64*768+2*5120+8000 < 128*1024
    def responses(live):
        for row in live.values():
            result = row['result']
            row['response'] = json.dumps({'resumeHandle':state['handle'],'resumeOrdinal':result['ordinal'],
                'window':result['window'],'drainLimit':result['limit'],'auditRecordHash':result['audit_hash'].hex()},
                sort_keys=True,separators=(',',':')).encode()
    responses(state['live'])
    if state.get('reconciliation') is not None: responses(state['reconciliation']['live'])
    return state

loop7_schemas = {
'D31-carrier':['U','U','U','B32','B32','B32','N'],
'D31-authority':['U','U','B32','B32','U','N','B','B32','N','B32','U','U','B32',('O','B32'),'N','N','Q','Q','B','B',('O','B32'),'B32','B32'],
'D31-owners':['U','U','N','N','B','B32','Q','N'],
'D31-predecessor':['U','U','N','B','B','B','B32','B32','B32','Q','N'],
'D31-receipt':['U','B32','N','B32','B32','Q','U','B32'],
}
loop7_domains = {'D31-carrier':'HX-EV-PIN-WAIT-PREPARATION-1','D31-authority':'HX-EV-PIN-WAIT-AUTHORITY-1',
'D31-owners':'HX-EV-PIN-QUEUE-OWNERS-1','D31-predecessor':'HX-EV-PIN-QUEUE-PREDECESSOR-1',
'D31-receipt':'HX-EV-PIN-QUEUE-RECEIPT-1'}
def validate_loop7(domain,values):
    if domain == 'HX-EV-PIN-WAIT-PREPARATION-1':
        assert values[2] in {'queued','parked'} and 0 < values[6] < 2**64
    elif domain == 'HX-EV-PIN-WAIT-AUTHORITY-1':
        assert values[5] > 0 and values[8] > 0 and (values[8] == 1) == (values[9] == bytes(32))
        assert values[10] in {'allocated','admitted','cleanup'} and values[11] in {'none','deployment','tenant'}
        assert (values[10] == 'admitted') == (values[11] != 'none')
        assert (values[13] is not None) == (values[10] != 'allocated')
        assert (values[20] is not None) == (values[10] == 'cleanup')
        if values[10]=='allocated': assert values[21]==values[22]==bytes(32), 'queue-allocation-wait-absence'
        if values[10]=='admitted': assert values[21]!=bytes(32) and values[22]!=bytes(32), 'queue-admission-wait-binding'
        assert values[14] == 40*1024 and 1 <= values[15] <= 50000
        assert values[17] >= values[16] and len(values[6]) <= 4096
        c = decode_record(values[6],'HX-EV-PIN-WAIT-PREPARATION-1',loop7_schemas['D31-carrier'])
        assert c[0] == values[1] and c[3] == values[3] and values[7] == sha256(values[6]).digest()
        expected = sha256(b'HX-EV-CAPACITY-SUBJECT-1\0\x01'+c[3]+c[4]).digest()
        assert values[2] == expected
        assert values[4] == 'queue-owner:'+sha256(U(c[0])+U(c[1])).hexdigest()
        for raw,counter in zip(values[18:20],('deployment','tenant:'+values[1])):
            if raw:
                receipt=decode_record(raw,'HX-EV-PIN-QUEUE-RECEIPT-1',loop7_schemas['D31-receipt'])
                assert receipt[0]==K('HX-EV-PIN-CAPACITY-QUEUE-KEY-1',U(values[0]),U(counter)) and receipt[1]==bytes(32) and receipt[6]=='present', 'queue-original-predecessor-binding'
        assert values[18], 'queue-original-deployment-predecessor'
        assert values[12] == sha256(b'queue-allocation:'+values[2]+N(values[5])+values[7]+B(values[18])+B(values[19])).digest()
        assert values[13] is None or values[13] == sha256(b'queue-admission:'+values[12]+values[22]).digest()
    elif domain == 'HX-EV-PIN-QUEUE-OWNERS-1':
        assert values[2] > 0 and (values[2] == 1) == (values[5] == bytes(32))
        assert 0 <= values[3] <= values[7] <= 50000 and values[7] > 0
        rows = decode_rows(values[4],values[3],['N','B32','B32'])
        assert all(r[0] > 0 for r in rows) and rows == sorted(rows,key=lambda r:(r[0],r[1]))
        assert len({r[0] for r in rows}) == len({r[1] for r in rows}) == len(rows)
    elif domain == 'HX-EV-PIN-QUEUE-PREDECESSOR-1':
        assert values[2] > 0
        prior_q = decode_record(values[3],'HX-EV-PIN-CAPACITY-QUEUE-4',schemas['D31-queue'])
        prior_o = decode_record(values[4],'HX-EV-PIN-QUEUE-OWNERS-1',loop7_schemas['D31-owners'])
        target_o = decode_record(values[5],'HX-EV-PIN-QUEUE-OWNERS-1',loop7_schemas['D31-owners'])
        assert all(v[0:2] == tuple(values[0:2]) for v in (prior_q,prior_o,target_o))
        assert prior_q[2] == prior_o[2] == values[2]-1 and target_o[2] == values[2]
        assert values[7] == sha256(values[5]).digest()
        assert (values[2] == 2) == (values[8] == bytes(32))
        assert values[10] >= prior_q[8] and all(r[0] <= values[10] for r in decode_rows(target_o[4],target_o[3],['N','B32','B32']))
    elif domain == 'HX-EV-PIN-QUEUE-RECEIPT-1':
        assert 0 < values[2] < 2**64 and values[6] in {'present','deleted'}
        assert values[7] == sha256(b'fixture-queue-provider:'+b''.join(encode_typed(k,v) for k,v in zip(loop7_schemas['D31-receipt'][:-1],values[:-1]))).digest()

c2_readbacks = {}
def c2_fixture_readback(tenant,scope,window,position,message,local,parent,send,observations,send_row=None,nonce_ordinal=1):
    # Authenticated imported C2 provider readback model. The complete immutable
    # purpose-20 key and observation-chain mapping remain existing C2 authority.
    send_row=local if send_row is None else send_row
    nonce=sha256(U(tenant)+scope+N(window)+N(position)+N(local)+parent+send).digest()[:16]
    body=(tenant,scope,window,position,message,local,parent,send,send_row,nonce_ordinal,nonce,tuple(observations))
    receipt=sha256(b'fixture-c2-parent-registration-readback:'+canonical_image_bytes(body)).digest()
    c2_readbacks[(tenant,scope,window,position,local)]=body+(receipt,)
    return body+(receipt,)

def authenticate_c2_groups(tenant,scope,window,groups,sources):
    keys=set(); send_rows={}; registrations=set()
    for (position,local),group in groups.items():
        source=sources.get((tenant,scope,window,position,local))
        assert source is not None and len(source)==13, 'c2-parent-readback-missing'
        assert source[-1]==sha256(b'fixture-c2-parent-registration-readback:'+canonical_image_bytes(source[:-1])).digest(), 'c2-parent-readback-authentication'
        assert source[:4]==(tenant,scope,window,position) and source[5]==local, 'c2-registration-member-window-ordinal'
        assert source[6:8]==group[0][4:6] and source[11]==tuple((r[2],r[3],r[6]) for r in group), 'c2-registration-observation-binding'
        assert isinstance(source[4],str) and 0<len(source[4].encode())<=1024
        assert 0<source[8]<=64 and 0<source[9]<=64 and len(source[10])==16
        assert group[0][6] not in registrations, 'c2-distinct-registration-evidence'; registrations.add(group[0][6])
        attempt_key=(source[7],source[10])
        assert attempt_key not in keys, 'c2-distinct-attempt-key'; keys.add(attempt_key)
        row_identity=(position,source[8])
        if row_identity in send_rows: assert send_rows[row_identity]==source[6:8], 'c2-parent-send-row-consistency'
        else:
            assert source[7] not in {value[1] for value in send_rows.values()}, 'c2-distinct-parent-send-row'
            send_rows[row_identity]=source[6:8]
    for position in {p for p,local in groups}:
        by_row={}
        for (p,local),group in groups.items():
            if p==position:
                source=sources[(tenant,scope,window,p,local)]
                by_row.setdefault(source[8],[]).append((local,source[9]))
        assert sorted(by_row)==list(range(1,len(by_row)+1)), 'c2-parent-send-row-contiguity'
        offset=0
        for send_row,attempts in sorted(by_row.items()):
            assert sorted(nonce for local,nonce in attempts)==list(range(1,len(attempts)+1)), 'c2-per-id-attempt-contiguity'
            assert all(local==offset+nonce for local,nonce in attempts), 'c2-cumulative-local-ordinal'
            offset+=len(attempts)
    return True

hold_owners = {
    'LegacyArrayLimit':{'projection'}, 'ActivationInventoryCapacityHold':{'projection'},
    'AdmissionEvidenceHold':{'gateway'}, 'ResponsePreparationHold':{'coordinator'},
    'OutcomeEvidenceHold':{'coordinator'}, 'OutcomeEvidenceConflict':{'coordinator'},
    'TerminalEvidenceHold':{'coordinator'}, 'PublicationRetryExhaustedHold':{'coordinator'},
    'PublicationDrainLimitHold':{'coordinator'}, 'FirstSendMembershipChangedHold':{'subscriber'},
    'ScopeRetentionCapacityHold':{'gateway'}, 'LegacyResumeIncident':{'actor','operations'},
    'PublicationPinCapacityHold':{'quota-coordinator'}, 'PinCapacityQueueCorruptionHold':{'quota-coordinator'},
    'PublicationResumePreparationHold':{'coordinator'}, 'ResumeAttemptCollectionHold':{'coordinator'},
    'QuotaGenerationIncident':{'quota-coordinator'}, 'RedriveEvidenceRepairHold':{'operations'},
    'HeldDelivery':{'operations'},
}

def decode_record(raw, domain, schema, attempt_store=None, repair_predecessor=None, c2_store=None):
    assert len(raw) <= record_caps.get(domain,4096), ('record-length',domain,record_caps.get(domain,4096))
    prefix = domain.encode() + b'\0\x01'
    assert raw.startswith(prefix)
    offset = len(prefix)
    count = int.from_bytes(raw[offset:offset+2], 'big'); offset += 2
    assert count == len(schema)
    def take(length):
        nonlocal offset
        assert 0 <= length <= len(raw)-offset
        value = raw[offset:offset+length]; offset += length
        return value
    def field(kind, maximum):
        nonlocal offset
        if isinstance(kind, tuple) and kind[0] == 'O':
            marker = take(1)
            assert marker in {b'\x00', b'\x01'}
            return field(kind[1],maximum) if marker == b'\x01' else None
        elif kind in {'U','B'}:
            length = int.from_bytes(take(4), 'big')
            assert length <= maximum, ('field-length',domain,expected_tag-1,maximum)
            value = take(length)
            if kind == 'U':
                assert 1 <= length <= maximum
                value = value.decode('utf-8', errors='strict')
                assert value.strip() and '\x00' not in value
            return value
        elif kind == 'B32': return take(32)
        elif kind == 'N': return int.from_bytes(take(8), 'big')
        elif kind == 'I': return int.from_bytes(take(4), 'big', signed=True)
        elif kind == 'Q': return int.from_bytes(take(8), 'big', signed=True)
        else: raise AssertionError(kind)
    values = []
    for expected_tag, kind in enumerate(schema, 1):
        assert take(1) == bytes([expected_tag])
        base_kind = kind[1] if isinstance(kind,tuple) else kind
        maximum = field_maxima.get((domain,expected_tag-1),
            record_caps.get(domain,4096) if base_kind == 'B' else 1024)
        values.append(field(kind,maximum))
    assert offset == len(raw)
    def genesis(generation, predecessor):
        assert generation > 0 and (generation == 1) == (predecessor == bytes(32))
    if domain == 'HX-EV-PUBLICATION-RETENTION-CAPABILITY-2':
        genesis(values[1],values[9])
        assert values[11] == 256 and 1 <= values[13] <= 50000
        assert 1024*MiB <= values[3] <= values[4] and values[3]+values[5] <= values[4]
        assert 195*MiB <= values[5] <= values[6] <= values[4]
        assert values[8] >= 64*MiB and values[7] <= 1114112
        assert 1 <= values[12] <= 315576000 and 193*MiB <= values[14] <= 256*MiB
    elif domain == 'HX-EV-HOLD-DIRECTORY-1':
        assert 0 <= values[0] <= 255 and values[2]+values[6] <= 50000
        genesis(values[1],values[4])
        rows = decode_rows(values[3],values[2],['U'])
        assert rows == sorted(set(rows))
        assert all(re.fullmatch(r'(tenant|deployment):[0-9a-f]{64}',row[0]) for row in rows)
    elif domain == 'HX-EV-SCOPE-SHARD-USAGE-1':
        assert values[1] <= 255; genesis(values[5],values[6])
    elif domain == 'HX-EV-COMMAND-SCOPE-LEGACY-2':
        assert values[7] > values[6] and values[8] > 0
    elif domain == 'HX-EV-COMMAND-SCOPE-1':
        assert values[8]=='required' and values[5]==sha256(U(values[0])+U(values[2])+U(values[3])+U(values[4])+U(values[1])).digest(), 'required-scope-identity'
    elif domain == 'HX-EV-LEGACY-SCOPE-CUTOVER-1':
        genesis(values[2],values[7]); assert 1 <= values[5] <= 315576000
        assert values[4] >= values[3] + values[5]*10000000
    elif domain == 'HX-EV-COMMAND-SCOPE-TOMBSTONE-2':
        assert values[7] == sha256(U(values[0])+U(values[1])).digest()[0], 'tombstone-derived-shard'
        assert 0 < values[6]-values[5] <= 315576000*10000000
    elif domain == 'HX-EV-FIRST-SEND-MEMBERSHIP-RESOLUTION-1':
        assert values[2] > 0 and values[5] > 0
        assert values[9] in {'ContinueSamePin','FirstSendMembershipChangedHold'}
    elif domain == 'HX-EV-FULL-REPLAY-ACTIVATION-2':
        genesis(values[3],values[4]); assert len(values[5]) >= 4
        count = int.from_bytes(values[5][:4],'big'); assert count <= 943
        rows = decode_rows(values[5][4:],count,['U','U','N','N','N',('O','B32')])
        assert [row[0] for row in rows] == sorted({row[0] for row in rows},key=lambda s:s.encode())
        for route,disposition,events,readable,accounting,capability in rows:
            assert disposition in {'continue-full-replay','incremental','hold'}
            assert (disposition == 'incremental') == (capability is not None)
            assert events <= ((1<<64)-1)//8192 and accounting == events*8192, 'activation-conservative-accounting'
            assert disposition != 'continue-full-replay' or (events < 75000 and readable < 48*MiB and accounting < 192*MiB)
    if domain == 'HX-EV-LEGACY-RESUME-CAPSULE-2':
        assert 1 <= values[10] <= 1000 and values[9] >= values[8]
        assert values[9] - values[8] + 1 == values[10]
        assert values[7] in {'success-events','rejection-events'}
        assert values[13] in {'drain-exhaustion','operator-reconciliation'}
        assert all(len(values[i].encode()) <= 1024 for i in (0,1,2,3,5,6))
        assert len(values[12]) >= 4
        count = int.from_bytes(values[12][:4],'big'); assert 1 <= count <= 17
        rows = decode_rows(values[12][4:],count,['N','N','N','B32','N','U'],[1024]*5+[4096])
        assert [row[0] for row in rows] == list(range(count))
        sequence = values[8]
        for ordinal,first,nrows,digest,length,key in rows:
            assert first == sequence and 1 <= nrows <= 61 and 128 <= length <= 65536
            sequence += nrows
        assert sequence == values[9]+1
    elif domain == 'HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-1':
        assert 1 <= values[2] <= 61 and values[1] < 17 and len(raw) <= 65536
        rows = decode_rows(values[3],values[2],['N','U','B32'])
        assert [row[0] for row in rows] == list(range(rows[0][0],rows[0][0]+len(rows)))
        assert len({row[1] for row in rows}) == len(rows)
        assert values[4] == sha256(b'HX-EV-LEGACY-RESUME-CHUNK-ROWS-1\0\x01'+B(values[3])).digest()
    elif domain == 'HX-EV-HELD-DELIVERY-4':
        assert values[0] in {'tenant','deployment'}
        assert (values[0] == 'tenant') == (values[2] is not None)
        assert values[8] in {'observed','captured','redriving','incident','closed'}
        assert values[9] in {'handler-capability-hold','raw-source-unavailable',
            'delivery-carrier-limit-hold','invalid-header-value','invalid-carrier',
            'oversize-carrier','delivery_above_advertised_max'}
        retained = values[8] in {'captured','redriving','closed'}
        assert all((values[i] is not None) == retained for i in (12,13,14,15))
        assert values[17] > 0 and values[21] > 0
    elif domain == 'HX-EV-SUBSCRIPTION-DELIVERY-POLICY-3':
        assert values[4] > 0 and (values[4] == 1) == (values[5] == bytes(32))
        assert values[6] == ('initial' if values[4] == 1 else 'successor')
        assert values[7] in {'dead-letter-capture','direct-held-capture'}
        assert (values[7] == 'dead-letter-capture') == (values[8] is not None)
        assert 1 <= values[9] <= 64 and values[11] in {'dapr-configuration','broker-api'}
    elif domain == 'HX-EV-PUBLICATION-CHARGE-2':
        assert values[1] in {'tenant','capture-scope'}
        assert values[4] in {'pin-batch','side-record','retained-object','oversize-quarantine','resume-window'}
        ceilings = {'pin-batch':449*MiB,'side-record':193*MiB,'retained-object':193*MiB,
                    'oversize-quarantine':256*MiB,'resume-window':1024*MiB}
        assert values[5] <= ceilings[values[4]] and values[6] <= 1114112
        assert values[5] + values[6] <= (1 << 64)-1 and values[7] == values[5] + values[6]
        assert values[10] in {'staged','active','released'} and values[9] > 0
        genesis(values[9],values[11]); assert values[8] > 0
        assert values[10] != 'staged' or (values[12] is not None and values[4] == 'resume-window')
        assert values[9] != 1 or values[10] != 'active' or values[14] == 0
        assert values[12] is None or values[4] == 'resume-window'
        assert values[14] in {0,1}
        assert (values[12] is not None) == bool(values[14])
        assert not values[14] or values[4] == 'resume-window'
    elif domain == 'HX-EV-LEGACY-PUBLICATION-RECOVERY-3':
        genesis(values[3],values[9]); assert values[4] > 0
        assert values[5] in {'legacy-resume','dead-letter-admin'}
        assert values[6] in {'claimed','draining','completed','failed'}
        assert (values[6] == 'failed') == (values[10] is not None)
        assert values[10] is None or values[10] in {'transport-retryable','evidence-unavailable','evidence-contradictory'}
        assert values[11] is None or values[6] == 'claimed'
    elif domain == 'HX-EV-PIN-CAPACITY-QUEUE-4':
        assert 1 <= values[7] <= 50000 and values[3]+values[6] <= values[7]
        assert values[5] <= values[3]; genesis(values[2],values[9])
        rows = decode_rows(values[4],values[3],['N','U','B32','U'])
        assert all(0 < row[0] <= values[8] and row[3] in {'queued','parked'} for row in rows)
        assert len({row[0] for row in rows}) == len({row[2] for row in rows}) == len(rows)
        assert rows == sorted(rows,key=lambda row:(row[0],row[1].encode(),row[2]))
        assert values[5] == sum(row[3] == 'parked' for row in rows)
    elif domain == 'HX-EV-PIN-CAPACITY-WAIT-2':
        assert values[4] in {'tenant','deployment'} and values[5] > 0
        assert values[8] in {'queued','parked'}
    elif domain == 'HX-EV-PUBLICATION-COUNTER-1':
        assert values[1] in {'tenant','capture-scope','tenant-pool','deployment','unidentified'}
        if len(values[2].encode())>1024:
            assert values[1]=='tenant' and values[2].startswith('tenant:') and len(values[2][7:].encode())<=1024, 'qualified-tenant-counter-id'
        genesis(values[5],values[6])
    elif domain == 'HX-EV-PIN-BATCH-RESERVATION-2':
        assert 1 <= values[3] <= 59 and values[6] > 0 and values[11] > 0
        assert values[10] in {'reserved','installed','released'}
        rows = decode_rows(values[4],values[3],['P','U','B32','N','N'])
        assert rows == sorted(rows,key=lambda row:row[0])
        assert len({row[0] for row in rows}) == len({row[1] for row in rows}) == len(rows)
        assert all(row[0] > 0 and row[3] <= row[4] for row in rows)
        assert values[5] == sum(row[4] for row in rows) <= (1 << 64)-1
        assert values[2] == sha256(values[4]).digest()
    elif domain == 'HX-EV-PUBLICATION-RESUME-CARRIER-1':
        assert all(33 <= byte <= 126 for byte in values[3].encode('ascii'))
    elif domain == 'HX-EV-PUBLICATION-RESUME-3':
        assert values[4] in {'retry-exhausted','drain-limit','drain-limit-and-retry-exhausted','legacy-publish-failed'} and values[7] > 0
        legacy = values[4] == 'legacy-publish-failed'
        assert legacy == (values[3] == bytes(32)) == (values[6] == bytes(32))
        assert values[14] > values[13] and values[14]-values[13] <= 9000000000
    elif domain == 'HX-EV-PUBLICATION-DRAIN-LIMIT-2':
        assert values[8] in {'pending','unknown','failed'} and values[4] > 0 and values[7] > 0
    elif domain == 'HX-EV-PUBLICATION-DRAIN-LIMIT-RESOLUTION-1':
        assert values[3] in {'resumed','head-advanced','terminal'} and values[5] > 0
    elif domain == 'HX-EV-PUBLICATION-WINDOW-2':
        assert values[9] > 0
        assert (values[3] == 0) == (values[4] == bytes(32))
        assert (values[3] == 0) == (values[6] == bytes(32))
    elif domain == 'HX-EV-PUBLICATION-WINDOW-CLOSURE-3':
        assert len(values[3]) >= 4 and values[8] in {'SignedCarrier','BackendCas'}
        count = int.from_bytes(values[3][:4],'big'); assert count <= 1000
        rows = decode_rows(values[3][4:],count,['P','N','B32'])
        assert len({row[0] for row in rows}) == len(rows) and rows == sorted(rows)
        assert all(row[0] > 0 and row[1] > 0 for row in rows)
        authority = (attempt_evidence_store if attempt_store is None else attempt_store).get(tuple(values[:3]))
        assert authority is not None
        complete = decode_record(authority,'HX-EV-WINDOW-ATTEMPT-SET-1',['U','B32','N','B32','N','B','B32','Q'],c2_store=c2_store)
        assert complete[6] == values[7]
        attempts = decode_rows(complete[5],complete[4],['P','N','N','U','B32','B32','B32'],count_ceiling=11328)
        final = {}
        for position,local,observation,kind,parent,send,evidence in attempts:
            if kind == 'result': final[position] = (position,local,evidence)
        assert rows == sorted(final.values())
    elif domain == 'HX-EV-WINDOW-ATTEMPT-SET-1':
        assert 0 < values[4] <= 11328, 'attempt-row-count'
        rows = decode_rows(values[5],values[4],['P','N','N','U','B32','B32','B32'],count_ceiling=11328)
        assert rows == sorted(rows,key=lambda r:r[:3]) and len({r[:3] for r in rows}) == len(rows)
        groups = {}
        for row in rows:
            assert 0 < row[0] <= 59, 'attempt-member-position'
            assert 0 < row[1] <= 64 and row[3] in {'register','unknown','result'}
            groups.setdefault(row[:2],[]).append(row)
        for group in groups.values():
            assert [r[2] for r in group] == list(range(len(group))) and 2 <= len(group) <= 3
            assert group[0][3] == 'register' and group[-1][3] == 'result'
            assert all(r[3] == 'unknown' for r in group[1:-1])
            assert len({(r[4],r[5]) for r in group}) == 1
        authenticate_c2_groups(values[0],values[1],values[2],groups,c2_readbacks if c2_store is None else c2_store)
        for position in {r[0] for r in rows}:
            locals_for_member = sorted(local for member,local in groups if member == position)
            assert locals_for_member == list(range(1,locals_for_member[-1]+1))
        assert values[6] == sha256(b'HX-EV-WINDOW-ATTEMPTS-2\0\x01'+U(values[0])+values[1]+N(values[2])+values[3]+N(values[4])+B(values[5])).digest()
    elif domain == 'HX-EV-PUBLICATION-RESUME-STATE-3':
        assert values[5] > 0
        counts = [int.from_bytes(values[i][:4],'big') for i in (11,12)]
        assert all(len(values[i]) >= 4 for i in (11,12)) and sum(counts) <= 64
        live = decode_rows(values[11][4:],counts[0],['B32','B32','N','B32','N','N','Q'])
        expired = decode_rows(values[12][4:],counts[1],['B32','B32','Q','Q'])
        assert len({row[0] for row in live+expired}) == sum(counts)
        assert live == sorted(live,key=lambda row:row[2]) and expired == sorted(expired)
        assert len({row[2] for row in live}) == len(live)
        assert all(0 < row[2] <= values[3] and row[5] > 0 for row in live)
        assert all(row[3]-row[2] == 30*86400*10000000 for row in expired)
    elif domain == 'HX-EV-PUBLICATION-RESUME-AUDIT-4':
        assert values[2] > 0 and values[8] > 0
    elif domain == 'HX-EV-RESUME-ORIGIN-1':
        assert sha256(values[4]).digest() == values[3] and values[7] > 0
        assert 0 < values[9]-values[8] <= 9000000000
        assert len(values[6]) <= 128*1024, 'origin-prior-image-length'
        carrier_fields = decode_record(values[4],'HX-EV-PUBLICATION-RESUME-CARRIER-1',['U','U','B32','U','U'])
        claim_fields = decode_record(values[5],'HX-EV-PUBLICATION-RESUME-3',schemas['D45-request'])
        assert values[10] == sha256(b'fixture-purpose-2d:'+values[5]).digest()
        assert claim_fields[1:3] == tuple(values[:2]) and claim_fields[10:12] == tuple(values[2:4])
        assert claim_fields[9] == carrier_fields[1] and claim_fields[13:15] == tuple(values[8:10])
        assert values[2] == sha256(b'HX-EV-PUBLICATION-RESUME-IDENTITY-1\0\x01'+U(carrier_fields[0])+U(carrier_fields[1])+U(carrier_fields[3])).digest()
        assert carrier_fields[0] == values[0] and carrier_fields[2] == claim_fields[5]
        prior = continuation_image(values[6])
        assert (prior['tenant'],prior['handle'],prior['hold_source']) == (values[0],carrier_fields[1],carrier_fields[2])
    elif domain == 'HX-EV-RESUME-PREPARATION-1':
        assert values[4] == sha256(values[5]).digest()
        assert len(values[5]) <= 128*1024, 'preparation-prior-image-length'
        assert len(values[6]) <= 256*1024, 'preparation-successor-image-length'
        assert len(values[10]) <= 128*1024, 'preparation-manifest-image-length'
        assert 0 < values[8]-values[7] <= 9000000000 and 0 < values[9] <= 1024*MiB and values[11] > 0
        prior,successor = continuation_image(values[5]),continuation_image(values[6])
        assert len(values[10]) <= 36000, 'manifest-derived-length'
        assert len(raw) <= 304*1024, 'reconstruction-derived-length'
        manifest = read_canonical_image(values[10])
        assert set(manifest) == {'prior','successor','window'}
        assert all(set(manifest[key]) == {'roster','accepted','unresolved','window_claim_bytes','window_admission','window_progress'} for key in ('prior','successor'))
        assert all(isinstance(root,bytes) and len(root) == 32 for key in ('prior','successor') for root in manifest[key].values())
        assert isinstance(manifest['window'],bytes) and len(manifest['window']) <= 16*1024
        assert prior['tenant'] == successor['tenant'] == values[0]
        assert successor['ordinal'] == prior['ordinal']+1 == values[11]
        assert successor['active_charge'] == values[9] and successor['handle'] == prior['handle']
    elif domain == 'HX-EV-RESUME-PREPARATION-HEAD-1':
        genesis(values[6],values[7]); assert len(values[8]) <= 4096
        assert values[5] in {'admitted','writing','cleanup','audited','completed','rolled-back','evidence-hold'}
        assert len(values[8]) >= 4
        count = int.from_bytes(values[8][:4],'big'); assert count <= 8, 'progress-row-count'
        rows = decode_rows(values[8][4:],count,['U','U','B32','B32','U'],[1024,128,1024,1024,1024])
        assert len({row[0] for row in rows}) == len(rows)
        assert all(row[0] in {'claim','resolution','fence','closure','window','audit','state','invocation'}
                   and row[4] in {'pending','present','deleted'} for row in rows)
        assert values[5] not in {'audited','completed'} or values[4] is not None
    elif domain == 'HX-EV-PUBLICATION-INVOCATION-1':
        assert values[3] > 0 and values[4] > 0
        assert values[7] == sha256(b'HX-EV-PUBLICATION-INVOCATION-1\0\x01'+values[2]+N(values[3])+N(values[4])+values[5]+values[6]).digest()
    elif domain == 'HX-EV-CAPTURE-PREPARATION-1':
        assert values[7] <= 193*MiB
    elif domain == 'HX-EV-CAPTURE-ORIGIN-1':
        assert len(values[1]) <= 7*1024, 'capture-origin-image-length'
        state = read_canonical_image(values[1])
        assert values[2] == sha256(values[1]).digest()
        assert state['held_key'] == values[0] and state['first_observed'] == values[3]
        assert len(values[1]) <= 4800
        assert len(state['identity']) == 6 and all(v is None or isinstance(v,bytes) and len(v) == 32 for v in state['identity'])
        assert isinstance(state['account'],bytes) and len(state['account']) == 32
        assert state['delivery_attempt_count'] == state['observation_revision'] == values[4] > 0
        protected = {k:v for k,v in state.items() if k not in {'delivery_attempt_count','observation_revision','observation_receipt'}}
        assert state['observation_receipt'] == sha256(b'provider-monotonic-observation:'+canonical_image_bytes(protected)+N(values[4])+N(values[4])).digest()
    elif domain == 'HX-EV-REDRIVE-ATTEMPT-1':
        assert values[1] > 0
    elif domain == 'HX-EV-REDRIVE-CLEANUP-1':
        assert values[1] > 0 and values[4] in {'repaired-readback','record-deleted','record-readback','entry-deleted'}
        assert (values[4] != 'repaired-readback') == (values[5] is not None)
        assert (values[4] == 'entry-deleted') == (values[6] is not None)
        assert values[5] is None or len(values[5]) == 32
        assert values[6] is None or len(values[6]) == 32
        # Opaque provider receipts are authenticated against addressed backend readback by D11.2.
    elif domain == 'HX-EV-REDRIVE-REPAIR-1':
        assert values[1] > 0; genesis(values[5],values[6])
        assert values[7] in {'required','repaired'}
        assert (values[7] == 'repaired') == (values[8] is not None)
        assert (values[7] == 'repaired') == (values[5] > 1), 'repair-state-generation'
        if values[7] == 'repaired':
            expected = repair_predecessor if repair_predecessor is not None else repair_evidence_store.get((values[0],values[1]))
            assert expected is not None and values[6] == expected
    elif domain == 'HX-EV-HOLD-INDEX-2':
        assert values[0] in {'tenant','deployment'} and values[3] <= 10000 and values[5] == 0
        genesis(values[2],values[6])
        rows = decode_rows(values[4],values[3],['Q','U','U','B32'],[1024,1024,4096,1024])
        assert rows == sorted(rows,key=lambda row:(row[0],row[1].encode(),row[2].encode()))
        assert len({(row[1],row[2]) for row in rows}) == len(rows)
    elif domain == 'HX-EV-HOLD-ENTRY-2':
        assert values[0] in {'tenant','deployment'}; genesis(values[6],values[7])
        assert values[10] > 0 and values[9] >= values[8] and values[12] >= values[9]
        assert values[11] in {'actor','coordinator','gateway','subscriber','projection','operations','quota-coordinator'}
        assert (values[2] == 'HeldDelivery' and values[5] in held_reasons) or hold_reasons.get(values[2]) == values[5]
        separately_guarded={'PublicationPinCapacityHold','PinCapacityQueueCorruptionHold','PublicationResumePreparationHold','ResumeAttemptCollectionHold','QuotaGenerationIncident','RedriveEvidenceRepairHold','HeldDelivery'}
        if values[2] not in separately_guarded:
            assert values[11] in hold_owners[values[2]], 'hold-owner-binding'
        if values[2] in {'PublicationPinCapacityHold','PinCapacityQueueCorruptionHold'}: assert values[11] == 'quota-coordinator'
        if values[2] in {'PublicationResumePreparationHold','ResumeAttemptCollectionHold'}: assert values[11] == 'coordinator'
        if values[2] == 'QuotaGenerationIncident': assert values[11] == 'quota-coordinator'
        if values[2] == 'RedriveEvidenceRepairHold': assert values[11] == 'operations'
        if values[2] == 'HeldDelivery': assert values[11] == 'operations'
    elif domain == 'HX-EV-REDRIVE-REQUEST-2':
        assert values[1] in {'tenant','deployment'} and (values[1] == 'tenant') == (values[2] is not None)
    elif domain == 'HX-EV-CARRIER-QUARANTINE-2':
        assert values[0] in {'tenant','deployment'} and (values[0] == 'tenant') == (values[1] is not None)
        assert values[3] in {'invalid-header-value','invalid-carrier','oversize-carrier'}
        assert values[12] == 'terminal-quarantine' and values[4] <= 256*MiB
        assert len(values[6]) >= 4
        count = int.from_bytes(values[6][:4],'big'); assert count <= 128
        rows = decode_rows(values[6][4:],count,['U','N','B32'],[65536,1024,1024])
        assert len({row[0] for row in rows}) == count and sum(len(row[0].encode()) for row in rows) <= 65536
    if domain in loop7_domains.values(): validate_loop7(domain,values)
    return tuple(values)

def encode_typed(kind, value):
    if isinstance(kind, tuple): return O(None if value is None else encode_typed(kind[1], value))
    return {'U':U,'B':B,'B32':B32,'N':N,'I':I,'Q':Q}[kind](value)

schemas = {
 'D06-activation':['U','U','B32','N','B32','B','Q','U','Q'],
 'D12-legacy-claim':['U','U','U','U','U','B32','Q','Q','N','B32'],
 'D12-cutover':['U','U','N','Q','Q','N','B32','B32'],
 'D12-usage':['U','N','N','N','N','N','B32'],
 'D12-tombstone':['U','U','B32','B32','B32','Q','Q','N'],
 'D14-drain-limit':['U','B32','U','N','N','B32','B32','N','U','Q'],
 'D14-drain-resolution':['U','B32','B32','U','B32','N','U','Q'],
 'D16-membership-resolution':['U','B32','N','U','B32','N','B32','B32','B32','U','U','Q'],
 'D29-capability':['U','N','B','N','N','N','N','N','N','B32','Q','N','N','N','N'],
 'D29-charge':['U','U','U','B32','U','N','N','N','N','N','U','B32',('O','B32'),'Q','N'],
 'D29-counter':['U','U','U','N','N','N','B32','Q'],
 'D29-pin-batch':['U','B32','B32','N','B','N','N','B32','B32','B32','U','N','Q'],
 'D31-wait':['U','B32','B32','N','U','N','Q','N','U','B32','B32','Q'],
 'D31-queue':['U','U','N','N','B','N','N','N','N','B32','Q'],
 'D36-policy':['U','U','U','U','N','B32','U','U',('O','U'),'N','B32','U','Q'],
 'D36-held':['U','U',('O','U'),('O','U'),'U','U','U','B32','U','U','N','B32',('O','U'),('O','U'),('O','B32'),('O','B32'),'Q','N','N',('O','B32'),'Q','N'],
 'D36-quarantine':['U',('O','U'),'U','U','N','B32','B','U','U','B32',('O','U'),'Q','U','B32'],
 'D36-redrive':['U','U',('O','U'),'B32','N','U','Q'],
 'D37-entry':['U','U','U','U',('O','U'),'U','N','B32','Q','Q','N','U','Q'],
 'D37-index':['U','U','N','N','B','N','B32','Q'],
 'D37-directory':['N','N','N','B','B32','Q','N'],
 'D45-request':['U','U','U','B32','U','B32','B32','N','B32','U','B32','B32','U','Q','Q'],
 'D45-window':['U','B32','U','N','B32','B32','B32','B32','B32','N','U','Q','B32'],
 'D45-closure':['U','B32','N','B','B32','B32','B32','B32','U','B32','Q'],
 'D45-state':['U','U','B32','N','N','N','B32','B32','B32','N','B32','B','B','Q'],
 'D45-audit':['U','U','N','B32','B32','B32',('O','B32'),'N','N','Q'],
 'D46-capsule':['U','U','U','U',('O','U'),'U','U','U','N','N','I','B32','B','U','B32','Q'],
 'D46-recovery':['U','B32','U','N','N','U','U','B32',('O','B32'),'B32',('O','U'),('O','B32'),'Q'],
}
extra_schemas = {
    'D45-origin':['U','U','B32','B32','B','B','B','N','Q','Q','B'],
    'D45-preparation':['U','U','B32','B32','B32','B','B','Q','Q','N','B','N'],
    'D45-preparation-head':['U','U','B32','B32',('O','B32'),'U','N','B32','B','Q'],
    'D45-invocation':['U','U','B32','N','N','B32','B32','B32','Q'],
    'D36-capture-preparation':['B32','B32','B32','B32','U','U','B32','N','Q'],
    'D36-repair':['B32','N','B32','B32','U','N','B32','U',('O','B32'),'Q'],
    'D36-capture-origin':['B32','B','B32','Q','N','B32'],
    'D36-current-request':['U','U',('O','U'),'B32','N','U','Q'],
    'D36-attempt':['B32','N','B32','U','B32','Q','B32'],
    'D36-cleanup':['B32','N','B32','B32','U',('O','B32'),('O','B32')],
}
# Reconstruct ten durable answers from explicit model inputs, independently
# of the transition helpers that consume them in the second verifier.
fixture_roster = ((1,'message-1',b'accepted'),(2,'message-2',b'unresolved-a'),(3,'message-3',b'unresolved-b'))
fixture_existing_window = R('HX-EV-PUBLICATION-WINDOW-2',13,U('t'),H('scope'),U('operation'),N(7),H('prior-closure'),
    H('prior-window-state'),H('prior-window-request'),unresolved_root(fixture_roster[1:]),
    H('policy'),N(16),U('admin'),Q(9990000000),H('capability'))
fixture_prior = {'roster':fixture_roster,'accepted':(fixture_roster[0],),'unresolved':fixture_roster[1:],
    'ordinal':1,'window':7,'closed':2,'limit':16,'tenant':'t','handle':'hxrsm1-other',
    'hold_source':H('limit-hash'),'active_charge':300,'next_charge':400,'charge_ceiling':1000,
    'live':{},'tombstones':{},'orphans':{},'audits':0,'invocations':(),
    'window_claim_bytes':fixture_existing_window,'window_claim':sha256(fixture_existing_window).digest(),
    'window_admission':window_admission_bytes(fixture_roster[1:])}
fixture_prior['window_progress']=window_progress_bytes(fixture_prior['window_admission'],fixture_prior['accepted'],fixture_prior['unresolved'])
fixture_carrier = R('HX-EV-PUBLICATION-RESUME-CARRIER-1',5,U('t'),U('hxrsm1-other'),H('limit-hash'),U('custom-handle'),U('retry after repair'))
fixture_identity = sha256(b'HX-EV-PUBLICATION-RESUME-IDENTITY-1\0\x01'+U('t')+U('hxrsm1-other')+U('custom-handle')).digest()
fixture_carrier_hash = sha256(fixture_carrier).digest()
fixture_predecessor = R('HX-EV-PUBLICATION-RESUME-STATE-3',14,U('t'),U('op'),H('scope'),N(1),N(7),N(16),H('limit-hash'),
    fixture_prior['window_claim'],z,N(2),z,B(pack('>I',0)),B(pack('>I',0)),Q(10000000000))
fixture_actual_audit = R('HX-EV-PUBLICATION-RESUME-AUDIT-4',10,U('t'),U('op'),N(2),fixture_identity,fixture_carrier_hash,
    sha256(fixture_predecessor).digest(),O(None),N(7),N(24),Q(10000000000))
fixture_audit = sha256(fixture_actual_audit).digest()
fixture_member_rows = b''.join(pack('>I',p)+U(m)+sha256(body).digest() for p,m,body in fixture_roster[1:])
fixture_unresolved_root = sha256(b'HX-EV-PUBLICATION-UNRESOLVED-1\0\x01'+pack('>I',2)+fixture_member_rows).digest()
fixture_invocation = sha256(b'HX-EV-PUBLICATION-INVOCATION-1\0\x01'+fixture_prior['window_claim']+N(2)+N(24)+fixture_identity+fixture_unresolved_root).digest()
fixture_response = json.dumps({'auditRecordHash':fixture_audit.hex(),'drainLimit':24,'resumeHandle':'hxrsm1-other',
    'resumeOrdinal':2,'window':7},sort_keys=True,separators=(',',':')).encode()
fixture_successor = dict(fixture_prior,ordinal=2,limit=24,hold_source=z,active_charge=400,audits=1,last_audit=fixture_audit,invocations=(fixture_invocation,),invocation_owners=(fixture_identity,),
    live={fixture_identity:{'carrier_hash':fixture_carrier_hash,'result':{'ordinal':2,'window':7,'limit':24,'audit_hash':fixture_audit},
                           'response':fixture_response,'expires_at':1900}})
fixture_imports = ('roster','accepted','unresolved','window_claim_bytes','window_admission','window_progress')
fixture_prior_image = canonical_image_bytes(continuation_projection({k:v for k,v in fixture_prior.items() if k not in fixture_imports and k != 'orphans'}))
fixture_successor_image = canonical_image_bytes(continuation_projection({k:v for k,v in fixture_successor.items() if k not in fixture_imports and k != 'orphans'}))
fixture_manifest = canonical_image_bytes({'prior':{k:sha256(canonical_image_bytes(fixture_prior[k])).digest() for k in fixture_imports},
    'successor':{k:sha256(canonical_image_bytes(fixture_successor[k])).digest() for k in fixture_imports},'window':fixture_prior['window_claim_bytes']})
fixture_claim = R('HX-EV-PUBLICATION-RESUME-3',15,U('admin'),U('t'),U('op'),H('scope'),U('drain-limit'),H('limit-hash'),H('head'),
    N(2),z,U('hxrsm1-other'),fixture_identity,fixture_carrier_hash,U('operator'),Q(10000000000),Q(19000000000))
extra_vectors = {
    'D46-chunk':chunk,'D45-attempt-set':attempt_set,
    'D45-origin':R('HX-EV-RESUME-ORIGIN-1',11,U('t'),U('op'),fixture_identity,fixture_carrier_hash,B(fixture_carrier),
        B(fixture_claim),B(fixture_prior_image),N(1),Q(10000000000),Q(19000000000),B(sha256(b'fixture-purpose-2d:'+fixture_claim).digest())),
    'D45-preparation':R('HX-EV-RESUME-PREPARATION-1',12,U('t'),U('op'),fixture_identity,fixture_carrier_hash,
        sha256(fixture_prior_image).digest(),B(fixture_prior_image),B(fixture_successor_image),Q(10000000000),Q(19000000000),N(400),B(fixture_manifest),N(2)),
    'D45-invocation':R('HX-EV-PUBLICATION-INVOCATION-1',9,U('t'),U('op'),fixture_prior['window_claim'],N(2),N(24),fixture_identity,
        fixture_unresolved_root,fixture_invocation,Q(10000000000)),
}
extra_vectors['D45-preparation-head'] = R('HX-EV-RESUME-PREPARATION-HEAD-1',10,U('t'),U('op'),fixture_identity,
    sha256(extra_vectors['D45-origin']).digest(),O(None),U('admitted'),N(1),z,B(pack('>I',0)),Q(10000000000))
held_digest = bytes.fromhex('13f514aa06580dd6db547e25807b783ef3dfd9649a5a957176c4d00038d755f8')
carrier_digest = bytes.fromhex('a82ba5fcfda582d6e456c2914755f95f6a834a86da6c3d95672c8f6aaf78a351')
fixture_observed = {'identity':('tenant','deployment-a','t','pubsub','orders','sub-a'),'held_key':held_digest,'carrier_hash':carrier_digest,
    'metadata_key':'metadata:'+held_digest.hex(),'inventory_key':'inventory:'+held_digest.hex(),
    'metadata_receipt':bytes.fromhex('216a9f46e35a2e477a0f188a76580798c84f6fba57a8f85752f8462b34daa6c6'),
    'inventory_receipt':bytes.fromhex('0b324da6f8fa095d9a9c18993cd53e6dfade1ed8a954b9e7fdc35e6c09ca9fcf'),
    'account_kind':'tenant','account':'t','state':'observed','transport_copy_acked':False,'route_success':False,'closed':False,
    'first_observed':t,'delivery_attempt_count':2,'charged_bytes':32768,'indexed':True,'operator_visible':True,'observation_revision':2}
fixture_observation_protected = {k:v for k,v in capture_projection(fixture_observed).items() if k not in {'delivery_attempt_count','observation_revision','observation_receipt'}}
fixture_observed['observation_receipt'] = sha256(b'provider-monotonic-observation:'+canonical_image_bytes(fixture_observation_protected)+N(2)+N(2)).digest()
fixture_observed_hash = sha256(canonical_image_bytes(fixture_observed)).digest()
extra_vectors['D36-capture-origin'] = R('HX-EV-CAPTURE-ORIGIN-1',6,held_digest,B(canonical_image_bytes(capture_projection(fixture_observed))),sha256(canonical_image_bytes(capture_projection(fixture_observed))).digest(),Q(t),N(2),sha256(vectors['D36-policy']).digest())
extra_vectors['D36-capture-preparation'] = R('HX-EV-CAPTURE-PREPARATION-1',9,held_digest,
    fixture_observed_hash,
    bytes.fromhex('216a9f46e35a2e477a0f188a76580798c84f6fba57a8f85752f8462b34daa6c6'),
    bytes.fromhex('0b324da6f8fa095d9a9c18993cd53e6dfade1ed8a954b9e7fdc35e6c09ca9fcf'),U('held-delivery-store'),
    U('held/'+held_digest.hex()),carrier_digest,N(34),Q(t))
extra_vectors['D36-current-request'] = R('HX-EV-REDRIVE-REQUEST-2',7,U('admin'),U('tenant'),O(U('t')),held_digest,N(0),U('operator'),Q(t))
extra_vectors['D36-attempt'] = R('HX-EV-REDRIVE-ATTEMPT-1',7,held_digest,N(1),carrier_digest,U('held/'+held_digest.hex()),fixture_observed['metadata_receipt'],Q(t),sha256(extra_vectors['D36-current-request']).digest())
fixture_attempt = {'owner':held_digest,'count':1,'carrier_hash':carrier_digest,'locator':'held/'+held_digest.hex(),
    'metadata_receipt':fixture_observed['metadata_receipt'],'raw':extra_vectors['D36-attempt'],'request_hash':sha256(extra_vectors['D36-current-request']).digest()}
fixture_attempt['receipt'] = sha256(canonical_image_bytes(fixture_attempt)).digest()
fixture_disputed_attempt = dict(fixture_attempt,receipt=bytes(32))
fixture_disputed_hash = sha256(canonical_image_bytes(fixture_disputed_attempt)).digest()
extra_vectors['D36-repair'] = R('HX-EV-REDRIVE-REPAIR-1',10,held_digest,N(1),
    fixture_disputed_hash,carrier_digest,
    U('held/'+held_digest.hex()),N(1),z,U('required'),O(None),Q(t))
fixture_object_receipt = sha256(b'authenticated-object-readback:'+U('held-delivery-store')+U('held/'+held_digest.hex())+B(b'exact-retained-carrier-and-headers')).digest()
fixture_repair_receipt = sha256(b'authenticated-attempt-repair:'+fixture_attempt['receipt']+fixture_object_receipt).digest()
fixture_repaired = R('HX-EV-REDRIVE-REPAIR-1',10,held_digest,N(1),fixture_disputed_hash,carrier_digest,U('held/'+held_digest.hex()),
    N(2),sha256(extra_vectors['D36-repair']).digest(),U('repaired'),O(fixture_repair_receipt),Q(t))
repair_evidence_store={(held_digest,1):sha256(extra_vectors['D36-repair']).digest()}
fixture_repair_entry = R('HX-EV-HOLD-ENTRY-2',13,U('tenant'),U('t'),U('RedriveEvidenceRepairHold'),U(held_digest.hex()+':1'),O(None),
    U('redrive_evidence_repair_hold'),N(1),z,Q(t),Q(t),N(1),U('operations'),Q(t+9000000000))
extra_vectors['D36-cleanup'] = R('HX-EV-REDRIVE-CLEANUP-1',7,held_digest,N(1),sha256(fixture_repaired).digest(),
    sha256(fixture_repair_entry).digest(),U('repaired-readback'),O(None),O(None))
assert set(extra_vectors) == set(extra_answers)
assert {label:(len(raw),sha256(raw).hexdigest()) for label,raw in extra_vectors.items()} == extra_answers
for label,raw in extra_vectors.items():
    assert sha256(raw[:-1]+bytes([raw[-1]^1])).hexdigest() != extra_answers[label][1]
loop7_carrier=R('HX-EV-PIN-WAIT-PREPARATION-1',7,U('t'),U('op'),U('queued'),H('scope'),H('outbox-plan'),H('batch'),N(11*MiB))
loop7_subject=sha256(b'HX-EV-CAPACITY-SUBJECT-1\0\x01'+H('scope')+H('outbox-plan')).digest()
loop7_prior_q=R('HX-EV-PIN-CAPACITY-QUEUE-4',11,U('deployment-a'),U('deployment'),N(1),N(0),B(b''),N(0),N(0),N(50000),N(0),z,Q(t))
loop7_prior_o=R('HX-EV-PIN-QUEUE-OWNERS-1',8,U('deployment-a'),U('deployment'),N(1),N(0),B(b''),z,Q(t),N(50000))
loop7_qkey=K('HX-EV-PIN-CAPACITY-QUEUE-KEY-1',U('deployment-a'),U('deployment'))
loop7_rfields=[U(loop7_qkey),z,N(1),sha256(loop7_prior_q).digest(),z,Q(t),U('present')]
loop7_receipt=R('HX-EV-PIN-QUEUE-RECEIPT-1',8,*loop7_rfields,sha256(b'fixture-queue-provider:'+b''.join(loop7_rfields)).digest())
loop7_allocation=sha256(b'queue-allocation:'+loop7_subject+N(1)+sha256(loop7_carrier).digest()+B(loop7_receipt)+B(b'')).digest()
loop7_authority=R('HX-EV-PIN-WAIT-AUTHORITY-1',23,U('deployment-a'),U('t'),loop7_subject,H('scope'),
    U('queue-owner:'+sha256(U('t')+U('op')).hexdigest()),N(1),B(loop7_carrier),sha256(loop7_carrier).digest(),N(1),z,U('allocated'),U('none'),
    loop7_allocation,O(None),N(40960),N(50000),Q(t),Q(t),B(loop7_receipt),B(b''),O(None),z,z)
loop7_orow=N(1)+loop7_subject+sha256(loop7_authority).digest()
loop7_target_o=R('HX-EV-PIN-QUEUE-OWNERS-1',8,U('deployment-a'),U('deployment'),N(2),N(1),B(loop7_orow),sha256(loop7_prior_o).digest(),Q(t),N(50000))
loop7_target_q=R('HX-EV-PIN-CAPACITY-QUEUE-4',11,U('deployment-a'),U('deployment'),N(2),N(0),B(b''),N(0),N(1),N(50000),N(1),sha256(loop7_prior_q).digest(),Q(t))
loop7_predecessor=R('HX-EV-PIN-QUEUE-PREDECESSOR-1',11,U('deployment-a'),U('deployment'),N(2),B(loop7_prior_q),B(loop7_prior_o),B(loop7_target_o),
    sha256(loop7_target_q).digest(),sha256(loop7_target_o).digest(),z,Q(t),N(1))
loop7_vectors={'D31-carrier':loop7_carrier,'D31-authority':loop7_authority,'D31-owners':loop7_target_o,
    'D31-predecessor':loop7_predecessor,'D31-receipt':loop7_receipt}
for label,raw in loop7_vectors.items():
    assert decode_record(raw,loop7_domains[label],loop7_schemas[label])
    assert sha256(raw[:-1]+bytes([raw[-1]^1])).digest()!=sha256(raw).digest()
loop7_answer_block=text.split('```text\nD31-carrier ',1)[1].split('\n```',1)[0]
loop7_answers={label:(int(length),digest) for label,length,digest in (line.split() for line in ('D31-carrier '+loop7_answer_block).splitlines())}
assert {label:(len(raw),sha256(raw).hexdigest()) for label,raw in loop7_vectors.items()}==loop7_answers
physical_inputs = {
'HX-EV-FULL-REPLAY-ACTIVATION-KEY-1':(U('d'),H('registry'),N(1)),
'HX-EV-PUBLICATION-DRAIN-LIMIT-KEY-1':(H('scope'),N(2),N(16)),
'HX-EV-PUBLICATION-DRAIN-RESOLUTION-KEY-1':(H('scope'),H('drain-limit')),
'HX-EV-PUBLICATION-DRAIN-HEAD-KEY-1':(H('scope'),),
'HX-EV-SCOPE-SHARD-USAGE-KEY-1':(U('t'),N(7)),
'HX-EV-LEGACY-SCOPE-CUTOVER-KEY-1':(U('*'),U('d'),N(1)),
'HX-EV-LEGACY-SCOPE-CUTOVER-HEAD-KEY-1':(U('*'),U('d')),
'HX-EV-FIRST-SEND-MEMBERSHIP-KEY-1':(U('t'),H('scope'),N(1),U('event-1'),H('pin')),
'HX-EV-DESTINATION-CONFIG-KEY-1':(U('deployment-a'),U('pubsub'),U('orders'),N(1)),
'HX-EV-DESTINATION-CONFIG-HEAD-KEY-1':(U('deployment-a'),U('pubsub'),U('orders')),
'HX-EV-PUBLICATION-CAPABILITY-KEY-1':(U('deployment-a'),N(3)),
'HX-EV-PUBLICATION-CAPABILITY-HEAD-KEY-1':(U('deployment-a'),),
'HX-EV-PUBLICATION-CHARGE-KEY-1':(U('deployment-a'),U('tenant'),U('t'),H('object')),
'HX-EV-PUBLICATION-COUNTER-KEY-1':(U('deployment-a'),U('tenant'),U('t')),
'HX-EV-PIN-BATCH-RESERVATION-KEY-1':(H('scope'),sha256(prow).digest()),
'HX-EV-CAPACITY-SUBJECT-1':(H('scope'),H('outbox-plan')),
'HX-EV-PIN-CAPACITY-WAIT-KEY-1':(U('deployment-a'),loop7_subject),
'HX-EV-PIN-CAPACITY-QUEUE-KEY-1':(U('deployment-a'),U('deployment')),
'HX-EV-PIN-WAIT-AUTHORITY-KEY-1':(U('deployment-a'),loop7_subject),
'HX-EV-PIN-QUEUE-OWNERS-KEY-1':(U('deployment-a'),U('deployment')),
'HX-EV-PIN-QUEUE-PREDECESSOR-KEY-1':(U('deployment-a'),U('deployment')),
'HX-EV-PIN-QUEUE-RECEIPT-KEY-1':(U('deployment-a'),U(loop7_qkey)),
'HX-EV-PUBLICATION-RESUME-CLAIM-KEY-1':(U('t'),U('op'),request_identity),
'HX-EV-PUBLICATION-RESUME-STATE-KEY-1':(U('t'),U('op')),
'HX-EV-PUBLICATION-WINDOW-KEY-1':(H('scope'),N(2)),
'HX-EV-PUBLICATION-WINDOW-CLOSURE-KEY-1':(H('scope'),N(1)),
'HX-EV-PUBLICATION-RESUME-AUDIT-KEY-2':(U('t'),U('op'),request_identity),
'HX-EV-RESUME-ORIGIN-KEY-1':(U('t'),U('op'),request_identity),
'HX-EV-RESUME-PREPARATION-KEY-1':(U('t'),U('op'),request_identity),
'HX-EV-RESUME-PREPARATION-HEAD-KEY-1':(U('t'),U('op')),
'HX-EV-PUBLICATION-INVOCATION-KEY-1':(U('t'),U('op'),H('invocation')),
'HX-EV-WINDOW-ATTEMPT-SET-KEY-1':(U('t'),H('scope'),N(1)),
'HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1':(capsule_identity,N(0)),
'HX-EV-LEGACY-RESUME-CAPSULE-KEY-2':(capsule_identity,),
'HX-EV-LEGACY-PUBLICATION-RECOVERY-KEY-2':(sha256(capsule).digest(),),
'HX-EV-SUBSCRIPTION-POLICY-KEY-1':(U('deployment-a'),U('pubsub'),U('orders'),U('sub-a'),N(1)),
'HX-EV-SUBSCRIPTION-POLICY-HEAD-KEY-1':(U('deployment-a'),U('pubsub'),U('orders'),U('sub-a')),
'HX-EV-HELD-DELIVERY-KEY-2':(U('tenant'),U('deployment-a'),O(U('t')),U('pubsub'),U('orders'),U('sub-a'),sha256(carrier).digest()),
'HX-EV-CARRIER-QUARANTINE-KEY-1':(H('held-key'),sha256(carrier).digest()),
'HX-EV-REDRIVE-REQUEST-KEY-1':(H('held-key'),),
'HX-EV-CAPTURE-ORIGIN-KEY-1':(H('held-key'),),
'HX-EV-CAPTURE-PREPARATION-KEY-1':(H('held-key'),),
'HX-EV-REDRIVE-ATTEMPT-KEY-1':(H('held-key'),),
'HX-EV-REDRIVE-REPAIR-KEY-1':(H('held-key'),N(2)),
'HX-EV-REDRIVE-REPAIR-INTEREST-KEY-1':(H('held-key'),),
'HX-EV-REDRIVE-CLEANUP-KEY-1':(H('held-key'),),
'HX-EV-PROVIDER-NATIVE-CLEANUP-KEY-1':(U('capture'),H('held-key')),
'HX-EV-HOLD-ENTRY-KEY-1':(U('tenant'),U('t'),U('PublicationPinCapacityHold'),U('scope:abc')),
'HX-EV-HOLD-INDEX-KEY-1':(U('tenant'),U('t')),
'HX-EV-HOLD-DIRECTORY-KEY-1':(U('deployment-a'),N(7)),
}
assert set(physical_inputs)==set(physical_prefixes)
physical_answer_block=text.split('```text\nD11-shared-scope ',1)[1].split('\n```',1)[0]
physical_answers=dict(line.split() for line in ('D11-shared-scope '+physical_answer_block).splitlines())
assert command_scope_address('t','op') == physical_answers['D11-shared-scope']
for name,fields in physical_inputs.items():
    assert K(name,*fields)==physical_answers[name], ('physical-address-vector',name)
    assert f'| `{name}\\0` | `{physical_prefixes[name]}` |' in text, ('normative-prefix',name)
for section in (text.split('## D4.',1)[1].split('## D5.',1)[0],text.split('### D11.3',1)[1].split('## D12.',1)[0],text.split('\n### D13.1',1)[1].split('### D13.2',1)[0]):
    assert 'command-execution-scope:' in section and 'U tenant || U executionMessageId' in section
assert command_scope_address('t','op') != 'command-execution-scope:'+KD('HX-EV-COMMAND-SCOPE-KEY-1',U('t'),U('op'))
assert K('HX-EV-SCOPE-SHARD-USAGE-KEY-1',U('t'),N(7)) != K('HX-EV-SCOPE-SHARD-USAGE-KEY-1',U('t'),U('7'))
# Canonical Unicode and maximum-width fields remain addressable without delimiter ambiguity.
assert command_scope_address('a:b','c')!=command_scope_address('a','b:c')
assert command_scope_address('é','op')!=command_scope_address('é','op')
wide_tenant='é'*512; wide_execution='x'*1024
assert len(wide_tenant.encode())==len(wide_execution.encode())==1024
assert command_scope_address(wide_tenant,wide_execution)=='command-execution-scope:'+sha256(U(wide_tenant)+U(wide_execution)).hexdigest()
for name,fields in (('HX-EV-HOLD-INDEX-KEY-1',(U('tenant'),U(wide_tenant))),
                    ('HX-EV-FIRST-SEND-MEMBERSHIP-KEY-1',(U(wide_tenant),H('scope'),N(1),U(wide_execution),H('pin')))):
    assert K(name,*fields)==physical_prefixes[name]+KD(name,*fields)
assert all(len(address.encode())<=128 for address in physical_answers.values())
assert K('HX-EV-HELD-DELIVERY-KEY-2',U('tenant'),U('deployment-a'),O(U('t')),U('pubsub'),U('orders'),U('sub-a'),sha256(carrier).digest()) != K('HX-EV-HELD-DELIVERY-KEY-2',U('tenant'),U('deployment-a'),O(None),U('pubsub'),U('orders'),U('sub-a'),sha256(carrier).digest())
for local in range(1,5):
    observations=tuple((i,kind,sha256(b'definitive-result').digest() if (local,kind)==(4,'result') else H(f'{local}:{kind}')) for i,kind in enumerate(('register','unknown','result')))
    c2_fixture_readback('t',H('scope'),1,1,'event-1',local,H(f'parent-{local}'),H(f'send-{local}'),observations)

families = [(vectors[label],vectors[label].split(b'\0',1)[0].decode(),schema)
            for label,schema in schemas.items()]
families.append((chunk,'HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-1',['B32','N','N','B','B32']))
families.append((attempt_set,'HX-EV-WINDOW-ATTEMPT-SET-1',['U','B32','N','B32','N','B','B32','Q']))
families.extend((extra_vectors[label],extra_vectors[label].split(b'\0',1)[0].decode(),schema) for label,schema in extra_schemas.items())
families.append((request_carrier,'HX-EV-PUBLICATION-RESUME-CARRIER-1',['U','U','B32','U','U']))
families.extend((loop7_vectors[label],loop7_domains[label],schema) for label,schema in loop7_schemas.items())
assert len(families) == 46 and len({domain for _,domain,_ in families}) == 45
malformed_rejected = 0
for value, domain, schema in families:
    decoded = decode_record(value, domain, schema)
    segments = [bytes([index])+encode_typed(kind,field) for index,(kind,field) in enumerate(zip(schema,decoded),1)]
    prefix_len = len(domain.encode()) + 1 + 1 + 2
    framed_index = next(i for i,kind in enumerate(schema) if kind in ('U','B'))
    overflow_segments = list(segments)
    overflow_segments[framed_index] = (segments[framed_index][:1]+b'\xff\xff\xff\xff'
                                       +segments[framed_index][5:])
    mutations = [
        value[:-1],                                      # missing/truncated
        value[:prefix_len] + b'\x02' + value[prefix_len+1:], # duplicate tag 02
        value[:prefix_len]+segments[1]+segments[0]+b''.join(segments[2:]), # reordered fields
        value[:prefix_len]+b''.join(overflow_segments),     # overflowing framed length
        value + b'\x00',                               # trailing
    ]
    for malformed in mutations:
        try:
            decode_record(malformed, domain, schema)
        except (AssertionError, UnicodeDecodeError):
            malformed_rejected += 1
        else:
            raise AssertionError((domain, 'malformed accepted'))
assert malformed_rejected == 230
semantic_rejected = 0
assert decode_record(R('HX-EV-SIGNED-PRIMITIVE-PROBE-1',1,I(-1)),
    'HX-EV-SIGNED-PRIMITIVE-PROBE-1',['I']) == (-1,)
capsule_domain = 'HX-EV-LEGACY-RESUME-CAPSULE-2'
capsule_schema = schemas['D46-capsule']
capsule_fields = list(decode_record(capsule,capsule_domain,capsule_schema))
held_schema = ['U','U',('O','U'),('O','U'),'U','U','U','B32','U','U','N','B32',
               ('O','U'),('O','U'),('O','B32'),('O','B32'),'Q','N','N',('O','B32'),'Q','N']
held_fields = list(decode_record(vectors['D36-held'], 'HX-EV-HELD-DELIVERY-4', held_schema))
for field_index, bad in [(10,-1),(10,0),(10,1001),(9,11),(7,'unknown')]:
    changed = list(capsule_fields); changed[field_index] = bad
    raw = R(capsule_domain, len(changed), *(encode_typed(k,v) for k,v in zip(capsule_schema,changed)))
    try: decode_record(raw,capsule_domain,capsule_schema)
    except AssertionError: semantic_rejected += 1
    else: raise AssertionError('bad signed/range/classification accepted')
for field_index, bad in [(2,None),(0,'deployment'),(12,None),(8,'unknown')]:
    changed = list(held_fields); changed[field_index] = bad
    raw = R('HX-EV-HELD-DELIVERY-4', len(changed), *(encode_typed(k,v) for k,v in zip(held_schema,changed)))
    try: decode_record(raw, 'HX-EV-HELD-DELIVERY-4', held_schema)
    except AssertionError: semantic_rejected += 1
    else: raise AssertionError('bad optional/scope/state accepted')
changed = list(capsule_fields); changed[9],changed[10] = 1010,1001
raw = R(capsule_domain,len(changed),*(encode_typed(k,v) for k,v in zip(capsule_schema,changed)))
try: decode_record(raw,capsule_domain,capsule_schema)
except AssertionError: semantic_rejected += 1
else: raise AssertionError('above-maximum count with matching range accepted')
# Mutate a real optional marker, preserving every following byte.
optional_offset = len(capsule_domain.encode()) + 4
for kind,value in zip(capsule_schema[:4],capsule_fields[:4]):
    optional_offset += 1 + len(encode_typed(kind,value))
optional_offset += 1
bad_marker = capsule[:optional_offset] + b'\x02' + capsule[optional_offset+1:]
try: decode_record(bad_marker,capsule_domain,capsule_schema)
except AssertionError: semantic_rejected += 1
else: raise AssertionError('bad optional marker accepted')
assert semantic_rejected == 11
def record_with(label, index, value):
    domain = vectors[label].split(b'\0',1)[0].decode(); schema = schemas[label]
    fields = list(decode_record(vectors[label],domain,schema)); fields[index] = value
    return R(domain,len(fields),*(encode_typed(kind,value) for kind,value in zip(schema,fields))),domain,schema

for label,index in [('D12-legacy-claim',0),('D36-held',2),('D36-held',12),('D37-entry',3),('D36-held',13),('D36-quarantine',8)]:
    maximum = 4096 if (label,index) in {('D37-entry',3),('D36-held',13),('D36-quarantine',8)} else 1024
    for size in (maximum-1,maximum,maximum+1):
        raw,domain,schema = record_with(label,index,'i'*size)
        try: decode_record(raw,domain,schema)
        except AssertionError:
            assert size == maximum+1; semantic_rejected += 1
        else: assert size <= maximum

semantic_cases = [('D29-capability',11,1),('D29-capability',8,64*MiB-1),
    ('D29-capability',13,50001),('D29-capability',12,315576001),
    ('D37-directory',0,256),('D37-directory',4,H('not-genesis')),
    ('D29-charge',11,H('not-genesis')),('D29-charge',12,H('wrong-owner')),
    ('D29-charge',10,'staged'),('D29-counter',5,1),('D12-usage',1,256),
    ('D12-tombstone',7,256),('D31-queue',9,H('not-genesis')),
    ('D36-policy',6,'active'),('D37-entry',11,'unknown'),
    ('D45-window',4,bytes(32)),('D45-audit',2,0),('D29-pin-batch',5,0),
    ('D37-entry',2,'unknown'),('D37-entry',5,'unknown'),('D37-entry',11,'coordinator')]
for label,index,value in semantic_cases:
    raw,domain,schema = record_with(label,index,value)
    try: decode_record(raw,domain,schema)
    except AssertionError: semantic_rejected += 1
    else: raise AssertionError((label,index,'semantic mutation survived'))

# Supported large B fields are decoded as real, canonical family records.
large_rows = b''.join(N(i+1)+U('t'*1024)+H('scope-'+str(i))+U('queued') for i in range(1000))
large_queue = R('HX-EV-PIN-CAPACITY-QUEUE-4',11,U('deployment-a'),U('deployment'),N(1),N(1000),
    B(large_rows),N(0),N(0),N(50000),N(1000),z,Q(t))
assert len(large_queue) == 1078163 > MiB
assert decode_record(large_queue,'HX-EV-PIN-CAPACITY-QUEUE-4',schemas['D31-queue'])[3] == 1000
large_index_rows = b''.join(Q(t+i)+U('HeldDelivery')+U('subject-'+str(i)+'x'*4000)+H(str(i)) for i in range(300))
large_index = R('HX-EV-HOLD-INDEX-2',8,U('tenant'),U('t'),N(1),N(300),B(large_index_rows),N(0),z,Q(t))
assert len(large_index) > MiB and decode_record(large_index,'HX-EV-HOLD-INDEX-2',schemas['D37-index'])[3] == 300
actor_ids = sorted('deployment:'+sha256(str(i).encode()).hexdigest() for i in range(20000))
large_directory = R('HX-EV-HOLD-DIRECTORY-1',7,N(7),N(1),N(20000),B(b''.join(U(a) for a in actor_ids)),z,Q(t),N(0))
assert len(large_directory) > MiB
assert decode_record(large_directory,'HX-EV-HOLD-DIRECTORY-1',schemas['D37-directory'])[2] == 20000
for raw,domain,schema in [(large_queue,'HX-EV-PIN-CAPACITY-QUEUE-4',schemas['D31-queue']),
                         (large_index,'HX-EV-HOLD-INDEX-2',schemas['D37-index']),
                         (large_directory,'HX-EV-HOLD-DIRECTORY-1',schemas['D37-directory'])]:
    malformed = raw + bytes(record_caps[domain]+1-len(raw))
    try: decode_record(malformed,domain,schema)
    except AssertionError: semantic_rejected += 1
    else: raise AssertionError('family ceiling-plus-one accepted')
above_count_fields = list(capsule_fields)
above_count_fields[9],above_count_fields[10] = 1010,1001
sequence = 10; manifest_rows = []
for ordinal,count in enumerate([61]*16+[25]):
    manifest_rows.append(N(ordinal)+N(sequence)+N(count)+H('chunk-'+str(ordinal))+N(128+count*1068)+U('chunks/'+str(ordinal)))
    sequence += count
above_count_fields[12] = pack('>I',17)+b''.join(manifest_rows)
above_count_capsule = R(capsule_domain,len(above_count_fields),*(encode_typed(k,v) for k,v in zip(capsule_schema,above_count_fields)))
try: decode_record(above_count_capsule,capsule_domain,capsule_schema)
except AssertionError: semantic_rejected += 1
else: raise AssertionError('semantically complete 1001-row capsule accepted')
assert semantic_rejected == 42
loop5_semantic_rejected = 0
def reject_semantic(raw,domain,schema,attempt_store=None):
    global loop5_semantic_rejected
    try: decode_record(raw,domain,schema,attempt_store)
    except (AssertionError,UnicodeError): loop5_semantic_rejected += 1
    else: raise AssertionError((domain,'loop-5 semantic defect accepted'))
charge_schema = schemas['D29-charge']
charge_fields = list(decode_record(vectors['D29-charge'],'HX-EV-PUBLICATION-CHARGE-2',charge_schema))
for kind,ceiling in [('pin-batch',449*MiB),('side-record',193*MiB),('retained-object',193*MiB),
                     ('oversize-quarantine',256*MiB),('resume-window',1024*MiB)]:
    for length in (ceiling-1,ceiling,ceiling+1):
        fields = list(charge_fields); fields[4],fields[5],fields[6],fields[7] = kind,length,1114112,length+1114112
        raw = R('HX-EV-PUBLICATION-CHARGE-2',15,*(encode_typed(k,v) for k,v in zip(charge_schema,fields)))
        if length <= ceiling: assert decode_record(raw,'HX-EV-PUBLICATION-CHARGE-2',charge_schema)[5] == length
        else: reject_semantic(raw,'HX-EV-PUBLICATION-CHARGE-2',charge_schema)
for overhead in (1114111,1114112,1114113):
    fields = list(charge_fields); fields[6],fields[7] = overhead,fields[5]+overhead
    raw = R('HX-EV-PUBLICATION-CHARGE-2',15,*(encode_typed(k,v) for k,v in zip(charge_schema,fields)))
    if overhead <= 1114112: assert decode_record(raw,'HX-EV-PUBLICATION-CHARGE-2',charge_schema)[6] == overhead
    else: reject_semantic(raw,'HX-EV-PUBLICATION-CHARGE-2',charge_schema)
for count in (0,1,63,64,65):
    raw,domain,schema = record_with('D36-policy',9,count)
    if 1 <= count <= 64: assert decode_record(raw,domain,schema)[9] == count
    else: reject_semantic(raw,domain,schema)
fields = list(decode_record(state,'HX-EV-PUBLICATION-RESUME-STATE-3',schemas['D45-state']))
other_retry = H('other-request') + retry_row[32:]
fields[11] = pack('>I',2)+retry_row+other_retry
duplicate_ordinal = R('HX-EV-PUBLICATION-RESUME-STATE-3',14,*(encode_typed(k,v) for k,v in zip(schemas['D45-state'],fields)))
reject_semantic(duplicate_ordinal,'HX-EV-PUBLICATION-RESUME-STATE-3',schemas['D45-state'])
attempt_schema = ['U','B32','N','B32','N','B','B32','Q']
assert decode_record(attempt_set,'HX-EV-WINDOW-ATTEMPT-SET-1',attempt_schema)[6] == attempt_root
for altered_rows,nrows in [(attempt_rows[1:],12),(b''.join(attempt_records[3:]),9),
                          (attempt_rows[:96]+bytes([attempt_rows[96]^1])+attempt_rows[97:],12)]:
    root = sha256(b'HX-EV-WINDOW-ATTEMPTS-2\0\x01'+U('t')+H('scope')+N(1)+H('roster')+N(nrows)+B(altered_rows)).digest()
    changed = R('HX-EV-WINDOW-ATTEMPT-SET-1',8,U('t'),H('scope'),N(1),H('roster'),N(nrows),B(altered_rows),root,Q(t))
    reject_semantic(closure,'HX-EV-PUBLICATION-WINDOW-CLOSURE-3',schemas['D45-closure'],{('t',H('scope'),1):changed})
assert loop5_semantic_rejected == 12
def extra_with(label,index,value):
    raw = extra_vectors[label]; domain = raw.split(b'\0',1)[0].decode(); schema = extra_schemas[label]
    fields = list(decode_record(raw,domain,schema)); fields[index] = value
    return R(domain,len(fields),*(encode_typed(k,v) for k,v in zip(schema,fields))),domain,schema
for label,index,bad in [('D45-origin',3,z),('D45-origin',2,z),('D45-origin',6,b'["map",[]]'),
                      ('D45-preparation',4,z),('D45-preparation',11,3),
                      ('D45-preparation-head',5,'unknown'),('D45-preparation-head',8,pack('>I',0)+b'x'),
                      ('D36-capture-preparation',7,193*MiB+1),('D36-repair',8,H('forged')),
                      ('D45-invocation',7,z)]:
    reject_semantic(*extra_with(label,index,bad))
# Keep the predecessor digest correct: only canonical-image validation rejects
# this otherwise parseable whitespace mutation.
fields = list(decode_record(extra_vectors['D45-preparation'],'HX-EV-RESUME-PREPARATION-1',extra_schemas['D45-preparation']))
fields[5] += b' '; fields[4] = sha256(fields[5]).digest()
reject_semantic(R('HX-EV-RESUME-PREPARATION-1',12,*(encode_typed(k,v) for k,v in zip(extra_schemas['D45-preparation'],fields))),
                'HX-EV-RESUME-PREPARATION-1',extra_schemas['D45-preparation'])
fields = list(decode_record(extra_vectors['D45-preparation'],'HX-EV-RESUME-PREPARATION-1',extra_schemas['D45-preparation']))
changed_successor = read_canonical_image(fields[6]); changed_successor['ordinal'] = 4
fields[6] = canonical_image_bytes(changed_successor); fields[11] = 4
reject_semantic(R('HX-EV-RESUME-PREPARATION-1',12,*(encode_typed(k,v) for k,v in zip(extra_schemas['D45-preparation'],fields))),
                'HX-EV-RESUME-PREPARATION-1',extra_schemas['D45-preparation'])
for hold,reason,owner in [('PublicationResumePreparationHold','publication_resume_preparation_hold','coordinator'),
                          ('RedriveEvidenceRepairHold','redrive_evidence_repair_hold','operations')]:
    entry = list(decode_record(vectors['D37-entry'],'HX-EV-HOLD-ENTRY-2',schemas['D37-entry']))
    entry[2],entry[5],entry[11] = hold,reason,owner
    valid = R('HX-EV-HOLD-ENTRY-2',13,*(encode_typed(k,v) for k,v in zip(schemas['D37-entry'],entry)))
    assert decode_record(valid,'HX-EV-HOLD-ENTRY-2',schemas['D37-entry'])[11] == owner
    for index,bad in [(5,'unknown'),(11,'actor')]:
        altered = list(entry); altered[index] = bad
        reject_semantic(R('HX-EV-HOLD-ENTRY-2',13,*(encode_typed(k,v) for k,v in zip(schemas['D37-entry'],altered))),
                        'HX-EV-HOLD-ENTRY-2',schemas['D37-entry'])
assert loop5_semantic_rejected == 28
pass8_semantic_rejected = 0

def pass8_reject(raw,domain,schema,reason=None):
    global pass8_semantic_rejected
    try: decode_record(raw,domain,schema)
    except (AssertionError,UnicodeError,ValueError) as error:
        if reason is not None: assert error.args == (reason,), (domain,error.args,reason)
        pass8_semantic_rejected += 1
    else: raise AssertionError((domain,'pass-8 defect accepted'))
# Family caps have their own rejection cause, independent of trailing/semantic
# rejection. At-cap framing reaches the decoder; cap+1 must fail the cap itself.
for label in ('D36-current-request','D45-preparation-head','D36-capture-origin','D36-repair'):
    raw=extra_vectors[label]; domain=raw.split(b'\0',1)[0].decode(); schema=extra_schemas[label]; cap=record_caps[domain]
    assert decode_record(raw,domain,schema)
    for size in (cap,cap+1):
        padded=raw+bytes(size-len(raw))
        try: decode_record(padded,domain,schema)
        except AssertionError as error:
            if size == cap: assert error.args != (('record-length',domain,cap),)
            else: assert error.args == (('record-length',domain,cap),); pass8_semantic_rejected += 1
        else: raise AssertionError('padded family record accepted')
# Every loop-5/6 variable-field cap and closed enum is observed.
for label,index,maximum in [('D36-repair',4,4096),('D36-attempt',3,4096),('D36-current-request',5,256)]:
    for size in (maximum-1,maximum,maximum+1):
        raw,domain,schema=extra_with(label,index,'x'*size)
        if size <= maximum: assert decode_record(raw,domain,schema)[index] == 'x'*size
        else: pass8_reject(raw,domain,schema)
pass8_reject(*extra_with('D36-attempt',1,0))
cleanup=list(decode_record(extra_vectors['D36-cleanup'],'HX-EV-REDRIVE-CLEANUP-1',extra_schemas['D36-cleanup']))
for phase in ('repaired-readback','record-deleted','record-readback','entry-deleted','unknown'):
    fields=list(cleanup); fields[4]=phase
    fields[5]=None if phase == 'repaired-readback' else sha256(b'provider-repair-deletion:'+fields[2]).digest()
    fields[6]=sha256(b'provider-repair-entry-deletion:'+fields[3]).digest() if phase == 'entry-deleted' else None
    raw=R('HX-EV-REDRIVE-CLEANUP-1',7,*(encode_typed(k,v) for k,v in zip(extra_schemas['D36-cleanup'],fields)))
    if phase == 'unknown': pass8_reject(raw,'HX-EV-REDRIVE-CLEANUP-1',extra_schemas['D36-cleanup'])
    else: assert decode_record(raw,'HX-EV-REDRIVE-CLEANUP-1',extra_schemas['D36-cleanup'])[4] == phase
# Origin envelope cap fails at its own length guard, independently of the
# fixture-only authentication check. At the cap decoding reaches that check.
for size in (8192,8193):
    raw,domain,schema=extra_with('D45-origin',10,b'x'*size)
    try: decode_record(raw,domain,schema)
    except AssertionError as error:
        if size == 8192: assert error.args != (('field-length',domain,10,8192),)
        else: assert error.args == (('field-length',domain,10,8192),); pass8_semantic_rejected += 1
    else: raise AssertionError('unauthenticated fixture envelope accepted')
# Origin retains and verifies the actual signature envelope bytes.
pass8_reject(*extra_with('D45-origin',10,bytes(32)))
repair=list(decode_record(extra_vectors['D36-repair'],'HX-EV-REDRIVE-REPAIR-1',extra_schemas['D36-repair']))
repair[7],repair[8]='repaired',H('receipt')
pass8_reject(R('HX-EV-REDRIVE-REPAIR-1',10,*(encode_typed(k,v) for k,v in zip(extra_schemas['D36-repair'],repair))),
    'HX-EV-REDRIVE-REPAIR-1',extra_schemas['D36-repair'],'repair-state-generation')
# Legacy zero fields hold in both directions.
claim=list(decode_record(vectors['D45-request'],'HX-EV-PUBLICATION-RESUME-3',schemas['D45-request']))
for eligibility,scope,head,valid in [('legacy-publish-failed',z,z,True),('legacy-publish-failed',H('scope'),z,False),
        ('legacy-publish-failed',z,H('head'),False),('retry-exhausted',z,z,False),('drain-limit',z,H('head'),False)]:
    fields=list(claim); fields[4],fields[3],fields[6]=eligibility,scope,head
    raw=R('HX-EV-PUBLICATION-RESUME-3',15,*(encode_typed(k,v) for k,v in zip(schemas['D45-request'],fields)))
    if valid: assert decode_record(raw,'HX-EV-PUBLICATION-RESUME-3',schemas['D45-request'])
    else: pass8_reject(raw,'HX-EV-PUBLICATION-RESUME-3',schemas['D45-request'])
# Full carrier framing/limits and visible ASCII caller key.
carrier_schema=['U','U','B32','U','U']
carrier_fields=list(decode_record(request_carrier,'HX-EV-PUBLICATION-RESUME-CARRIER-1',carrier_schema))
for index,maximum in ((0,1024),(1,1024),(3,128),(4,512)):
    for length in (maximum-1,maximum,maximum+1):
        fields=list(carrier_fields); fields[index]='a'*length
        raw=R('HX-EV-PUBLICATION-RESUME-CARRIER-1',5,*(encode_typed(k,v) for k,v in zip(carrier_schema,fields)))
        if length <= maximum: assert decode_record(raw,'HX-EV-PUBLICATION-RESUME-CARRIER-1',carrier_schema)
        else: pass8_reject(raw,'HX-EV-PUBLICATION-RESUME-CARRIER-1',carrier_schema)
for key in ('space key','\x1f','\x7f','é'):
    fields=list(carrier_fields); fields[3]=key
    pass8_reject(R('HX-EV-PUBLICATION-RESUME-CARRIER-1',5,*(encode_typed(k,v) for k,v in zip(carrier_schema,fields))),
        'HX-EV-PUBLICATION-RESUME-CARRIER-1',carrier_schema)
# Preparation head has all eight kinds and rejects a ninth at its owning bound.
for count in (8,9):
    kinds=['claim','resolution','fence','closure','window','audit','state','invocation']+['extra']
    rows=pack('>I',count)+b''.join(U(kind)+U('address')+H(kind)+z+U('pending') for kind in kinds[:count])
    raw,domain,schema=extra_with('D45-preparation-head',8,rows)
    if count == 8: assert decode_record(raw,domain,schema)
    else: pass8_reject(raw,domain,schema,'progress-row-count')
# Attempt-member and row collection bounds fail at their own guards.
def attempt_record(rows,count):
    exact=b''.join(rows)
    root=sha256(b'HX-EV-WINDOW-ATTEMPTS-2\0\x01'+U('t')+H('scope')+N(1)+H('roster')+N(count)+B(exact)).digest()
    return R('HX-EV-WINDOW-ATTEMPT-SET-1',8,U('t'),H('scope'),N(1),H('roster'),N(count),B(exact),root,Q(t))
saved_c2_readbacks=dict(c2_readbacks)
for position in (58,59,60):
    c2_fixture_readback('t',H('scope'),1,position,'event-'+str(position),1,H('parent'),H('send'),tuple((i,kind,H(kind)) for i,kind in enumerate(('register','result'))))
    rows=[pack('>I',position)+N(1)+N(i)+U(kind)+H('parent')+H('send')+H(kind) for i,kind in enumerate(('register','result'))]
    raw=attempt_record(rows,2)
    if position <= 59: assert decode_record(raw,'HX-EV-WINDOW-ATTEMPT-SET-1',attempt_schema)
    else: pass8_reject(raw,'HX-EV-WINDOW-ATTEMPT-SET-1',attempt_schema,'attempt-member-position')
maximum_rows=[]
for position in range(1,60):
    for local in range(1,65):
        parent=H(f'max-parent-{position}-{local}'); send=H(f'max-send-{position}-{local}')
        observations=tuple((i,kind,H(f'{position}-{local}-{kind}')) for i,kind in enumerate(('register','unknown','result')))
        c2_fixture_readback('t',H('scope'),1,position,'event-'+str(position),local,parent,send,observations)
        maximum_rows.extend(pack('>I',position)+N(local)+N(i)+U(kind)+parent+send+evidence for i,kind,evidence in observations)
assert len(maximum_rows) == 11328
assert decode_record(attempt_record(maximum_rows,11328),'HX-EV-WINDOW-ATTEMPT-SET-1',attempt_schema)[4] == 11328
pass8_reject(attempt_record(maximum_rows+[maximum_rows[-1]],11329),'HX-EV-WINDOW-ATTEMPT-SET-1',attempt_schema,'attempt-row-count')
# A second Unknown is forbidden by the imported C2 two-observation contract.
rows=[pack('>I',1)+N(1)+N(i)+U(kind)+H('parent')+H('send')+H(kind) for i,kind in enumerate(('register','unknown','unknown','result'))]
pass8_reject(attempt_record(rows,4),'HX-EV-WINDOW-ATTEMPT-SET-1',attempt_schema)
c2_readbacks.clear(); c2_readbacks.update(saved_c2_readbacks)
# Image caps reject before parsing: later canonical/shape guards cannot mask removal.
for count in (7168,7169):
    image=canonical_image_bytes({'padding':'a'*count}); image=image[:count]
    fields=list(decode_record(extra_vectors['D36-capture-origin'],'HX-EV-CAPTURE-ORIGIN-1',extra_schemas['D36-capture-origin']))
    fields[1],fields[2]=image,sha256(image).digest()
    raw=R('HX-EV-CAPTURE-ORIGIN-1',6,*(encode_typed(k,v) for k,v in zip(extra_schemas['D36-capture-origin'],fields)))
    if count == 7169: pass8_reject(raw,'HX-EV-CAPTURE-ORIGIN-1',extra_schemas['D36-capture-origin'],'capture-origin-image-length')
# Derived worst-case continuation includes reconciliation/history without responses.
maximum_state=continuation_projection({k:v for k,v in fixture_prior.items() if k not in fixture_imports and k != 'orphans'})
maximum_state.update(tenant='\x01'*1024,handle='\x01'*1024,ordinal=2**64-1,window=2**64-1,closed=2**64-1,limit=2**64-1,
    active_charge=2**64-1,next_charge=2**64-1,charge_ceiling=2**64-1,audits=2**64-1,invocations=tuple(H(str(i)) for i in range(64)),history=H('history'),last_audit=H('audit'))
maximum_state['invocation_owners']=tuple(H(str(i)) for i in range(64))
maximum_state['live']={H(str(i)):{'carrier_hash':H('carrier'),'result':{'ordinal':i+1,'window':2**64-1,'limit':2**64-1,'audit_hash':H('audit')},'expires_at':2**63-1} for i in range(64)}
maximum_state['reconciliation']={'at':2**63-1,'live':dict(maximum_state['live']),'tombstones':{},'receipt':H('receipt')}
maximum_image=canonical_image_bytes(maximum_state)
assert len(maximum_image) <= 128832 < 128*1024
assert continuation_image(maximum_image)['ordinal'] == 2**64-1
maximum_manifest=canonical_image_bytes({'prior':{name:H(name) for name in fixture_imports},'successor':{name:H(name) for name in fixture_imports},'window':b'x'*(16*1024)})
assert len(maximum_manifest) <= 36000
maximum_reconstruction=R('HX-EV-RESUME-PREPARATION-1',12,U('\x01'*1024),U('\x01'*1024),H('identity'),H('carrier'),sha256(maximum_image).digest(),B(maximum_image),B(maximum_image),Q(10000000000),Q(19000000000),N(2**64-1),B(maximum_manifest),N(2**64-1))
assert len(maximum_reconstruction) <= 295864 < 304*1024
for size in (128*1024,128*1024+1):
    raw,domain,schema=extra_with('D45-origin',6,b'x'*size)
    if size > 128*1024: pass8_reject(raw,domain,schema,'origin-prior-image-length')
# Maximum-width/escaped capture identifiers have a constant hashed-image bound.
wide_observed=dict(fixture_observed,identity=('tenant',)+('\x01'*1024,)*5,account='\x01'*1024)
assert len(canonical_image_bytes(capture_projection(wide_observed))) <= 4800
# Matching preparation limits reach their own guards before image parsing.
for index,maximum,reason in ((5,128*1024,'preparation-prior-image-length'),(6,256*1024,'preparation-successor-image-length'),(10,128*1024,'preparation-manifest-image-length')):
    for size in (maximum,maximum+1):
        fields=list(decode_record(extra_vectors['D45-preparation'],'HX-EV-RESUME-PREPARATION-1',extra_schemas['D45-preparation']))
        fields[index]=b'x'*size
        if index == 5: fields[4]=sha256(fields[5]).digest()
        raw=R('HX-EV-RESUME-PREPARATION-1',12,*(encode_typed(k,v) for k,v in zip(extra_schemas['D45-preparation'],fields)))
        if size > maximum: pass8_reject(raw,'HX-EV-RESUME-PREPARATION-1',extra_schemas['D45-preparation'],reason)
        else:
            try: decode_record(raw,'HX-EV-RESUME-PREPARATION-1',extra_schemas['D45-preparation'])
            except (AssertionError,ValueError) as error: assert error.args != (reason,)
            else: raise AssertionError('invalid canonical image accepted')
# Repaired lineage authenticates the exact required predecessor.
fields=list(repair); fields[5],fields[6]=2,H('wrong-required-predecessor')
pass8_reject(R('HX-EV-REDRIVE-REPAIR-1',10,*(encode_typed(k,v) for k,v in zip(extra_schemas['D36-repair'],fields))),
    'HX-EV-REDRIVE-REPAIR-1',extra_schemas['D36-repair'])
# Ordinary generations keep absent transfer ownership; transferred rollback
# generations require both the marker and their original owner.
charge=list(decode_record(vectors['D29-charge'],'HX-EV-PUBLICATION-CHARGE-2',schemas['D29-charge']))
for generation in (1,2):
    ordinary=list(charge); ordinary[9],ordinary[11]=generation,z if generation == 1 else H('previous')
    assert decode_record(R('HX-EV-PUBLICATION-CHARGE-2',15,*(encode_typed(k,v) for k,v in zip(schemas['D29-charge'],ordinary))),
        'HX-EV-PUBLICATION-CHARGE-2',schemas['D29-charge'])[12] is None
for owner,marker in ((None,1),(H('owner'),0)):
    released=list(charge); released[4:8]=['resume-window',0,0,0]; released[9:13]=[2,'released',H('previous'),owner]; released[14]=marker
    pass8_reject(R('HX-EV-PUBLICATION-CHARGE-2',15,*(encode_typed(k,v) for k,v in zip(schemas['D29-charge'],released))),
        'HX-EV-PUBLICATION-CHARGE-2',schemas['D29-charge'])
for hold,reason,owner in [('ResumeAttemptCollectionHold','resume_evidence_hold','coordinator'),('QuotaGenerationIncident','quota_generation_exhausted','quota-coordinator')]:
    entry=list(decode_record(vectors['D37-entry'],'HX-EV-HOLD-ENTRY-2',schemas['D37-entry'])); entry[2],entry[5],entry[11]=hold,reason,owner
    assert decode_record(R('HX-EV-HOLD-ENTRY-2',13,*(encode_typed(k,v) for k,v in zip(schemas['D37-entry'],entry))),
        'HX-EV-HOLD-ENTRY-2',schemas['D37-entry'])
    for index,bad in ((5,'unknown'),(11,'actor')):
        fields=list(entry); fields[index]=bad
        pass8_reject(R('HX-EV-HOLD-ENTRY-2',13,*(encode_typed(k,v) for k,v in zip(schemas['D37-entry'],fields))),
            'HX-EV-HOLD-ENTRY-2',schemas['D37-entry'])
assert pass8_semantic_rejected == 40, pass8_semantic_rejected
loop7_semantic_rejected=0
def loop7_reject(raw,label,reason=None):
    global loop7_semantic_rejected
    try: decode_record(raw,loop7_domains[label],loop7_schemas[label])
    except AssertionError as error:
        if reason is not None: assert error.args and error.args[0]==reason,error.args
        loop7_semantic_rejected+=1
    else: raise AssertionError((label,'loop-7 semantic defect accepted'))
def loop7_record(label,fields):
    return R(loop7_domains[label],len(loop7_schemas[label]),*(encode_typed(k,v) for k,v in zip(loop7_schemas[label],fields)))
# The family guard owns cap+one rejection even before malformed trailing parsing.
for label,raw in loop7_vectors.items():
    cap=record_caps[loop7_domains[label]]
    for size in (cap-1,cap):
        padded=raw+b'x'*(size-len(raw))
        try: decode_record(padded,loop7_domains[label],loop7_schemas[label])
        except AssertionError as error: assert not error.args or not isinstance(error.args[0],tuple) or error.args[0][0]!='record-length'
        else: raise AssertionError('padded record accepted')
    loop7_reject(raw+b'x'*(cap+1-len(raw)),label,('record-length',loop7_domains[label],cap))
for index in (0,1):
    fields=list(decode_record(loop7_carrier,loop7_domains['D31-carrier'],loop7_schemas['D31-carrier']))
    for size in (1023,1024,1025):
        f=list(fields); f[index]='x'*size; raw=loop7_record('D31-carrier',f)
        if size<=1024: assert decode_record(raw,loop7_domains['D31-carrier'],loop7_schemas['D31-carrier'])[index]=='x'*size
        else: loop7_reject(raw,'D31-carrier')
# Legal counter width includes the fixed seven-byte tenant prefix.
for label in ('D31-owners',):
    fields=list(decode_record(loop7_vectors[label],loop7_domains[label],loop7_schemas[label]))
    for size in (1030,1031,1032):
        f=list(fields); f[1]='tenant:'+'x'*(size-7); raw=loop7_record(label,f)
        if size<=1031: assert decode_record(raw,loop7_domains[label],loop7_schemas[label])[1]==f[1]
        else: loop7_reject(raw,label)
for count in (49999,50000,50001):
    rows=b''.join(N(i)+sha256(N(i)).digest()+H('owner-authority') for i in range(1,count+1))
    raw=loop7_record('D31-owners',('deployment-a','deployment',2,count,rows,H('previous'),t,50000))
    if count<=50000: assert decode_record(raw,loop7_domains['D31-owners'],loop7_schemas['D31-owners'])[3]==count
    else: loop7_reject(raw,'D31-owners')
for label,index,value in [('D31-carrier',2,'unknown'),('D31-carrier',6,0),('D31-authority',4,'queue-owner:wrong'),
    ('D31-authority',5,0),('D31-authority',8,0),('D31-authority',10,'unknown'),('D31-authority',11,'tenant'),
    ('D31-authority',12,H('unbound-allocation')),('D31-authority',14,40959),('D31-authority',15,50001),
    ('D31-authority',21,H('invented-wait')),('D31-authority',22,H('invented-original-wait')),
    ('D31-owners',2,0),('D31-owners',3,0),('D31-owners',5,z),('D31-predecessor',2,3),
    ('D31-predecessor',7,H('changed-target')),('D31-predecessor',8,H('wrong-source')),('D31-predecessor',10,0),
    ('D31-receipt',2,0),('D31-receipt',6,'unknown'),('D31-receipt',7,H('forged-provider'))]:
    fields=list(decode_record(loop7_vectors[label],loop7_domains[label],loop7_schemas[label])); fields[index]=value
    loop7_reject(loop7_record(label,fields),label)
# Provider keys have one common 128-byte receipt bound and exact field framing.
fields=list(decode_record(loop7_receipt,loop7_domains['D31-receipt'],loop7_schemas['D31-receipt']))
for size in (127,128,129):
    f=list(fields); f[0]='x'*size
    f[7]=sha256(b'fixture-queue-provider:'+b''.join(encode_typed(k,v) for k,v in zip(loop7_schemas['D31-receipt'][:-1],f[:-1]))).digest()
    raw=loop7_record('D31-receipt',f)
    if size<=128: assert decode_record(raw,loop7_domains['D31-receipt'],loop7_schemas['D31-receipt'])[0]==f[0]
    else: loop7_reject(raw,'D31-receipt')
for absent in (False,True):
    f=list(decode_record(loop7_authority,loop7_domains['D31-authority'],loop7_schemas['D31-authority']))
    if absent: f[18]=b''
    else:
        r=list(decode_record(f[18],loop7_domains['D31-receipt'],loop7_schemas['D31-receipt'])); r[0]=K('HX-EV-PIN-CAPACITY-QUEUE-KEY-1',U('wrong-deployment'),U('deployment'))
        r[7]=sha256(b'fixture-queue-provider:'+b''.join(encode_typed(k,v) for k,v in zip(loop7_schemas['D31-receipt'][:-1],r[:-1]))).digest()
        f[18]=loop7_record('D31-receipt',r)
    f[12]=sha256(b'queue-allocation:'+f[2]+N(f[5])+f[7]+B(f[18])+B(f[19])).digest()
    loop7_reject(loop7_record('D31-authority',f),'D31-authority')
assert loop7_semantic_rejected==34,loop7_semantic_rejected

def verify_loop8_codecs():
    widths={}; boundaries=refusals=json_cases=window_cases=0
    for label,indexes,expected,reserve in (
        ('D12-legacy-claim',(0,1,2,3,4),5270,8192),
        ('D12-usage',(0,),1136,2048),('D12-tombstone',(0,1),2219,4096),
        ('D29-counter',(0,2),2176,4096)):
        domain=vectors[label].split(b'\0',1)[0].decode(); schema=schemas[label]
        fields=list(decode_record(vectors[label],domain,schema))
        for index in indexes: fields[index]='i'*1024
        if label=='D29-counter': fields[1]='tenant'; fields[2]='tenant:'+'i'*1024
        if label=='D12-tombstone': fields[7]=sha256(U(fields[0])+U(fields[1])).digest()[0]
        raw=R(domain,len(schema),*(encode_typed(k,v) for k,v in zip(schema,fields)))
        assert len(raw)==expected and len(raw)<=record_caps[domain]==reserve
        assert decode_record(raw,domain,schema)==tuple(fields)
        widths[label]=len(raw)
        for index in indexes:
            maximum=1031 if label=='D29-counter' and index==2 else 1024
            for length in (maximum-1,maximum,maximum+1):
                payload_length=length-7 if maximum==1031 else length
                for payload in ('i'*payload_length,'é'*(payload_length//2)+'i'*(payload_length%2)):
                    f=list(fields); f[index]=('tenant:'+payload) if maximum==1031 else payload
                    if label=='D12-tombstone': f[7]=sha256(U(f[0])+U(f[1])).digest()[0]
                    candidate=R(domain,len(schema),*(encode_typed(k,v) for k,v in zip(schema,f)))
                    try: decode_record(candidate,domain,schema)
                    except AssertionError: assert length==maximum+1; refusals+=1
                    else: assert length<=maximum
                    boundaries+=1
        try: decode_record(raw+bytes(reserve+1-len(raw)),domain,schema)
        except AssertionError as error: assert error.args[0][0]=='record-length'; refusals+=1
        else: raise AssertionError('loop8 family reserve ceiling bypassed')
    # Enumerated kind widths matter independently of the qualified tenant ID.
    for kind in ('tenant','capture-scope','tenant-pool','deployment','unidentified'):
        fields=['i'*1024,kind,'i'*1024,1,1,1,z,t]
        raw=R('HX-EV-PUBLICATION-COUNTER-1',8,*(encode_typed(k,v) for k,v in zip(schemas['D29-counter'],fields)))
        assert len(raw)==2163+len(kind.encode()) and decode_record(raw,'HX-EV-PUBLICATION-COUNTER-1',schemas['D29-counter'])
        boundaries+=1
    prior=fixture_prior; claim=decode_record(prior['window_claim_bytes'],'HX-EV-PUBLICATION-WINDOW-2',schemas['D45-window'])
    accepted=prior['roster'][:2]; pending=prior['roster'][2:]
    progress=window_progress_bytes(prior['window_admission'],accepted,pending,prior['accepted'])
    window_admission_members(prior['window_admission'],claim,prior['roster'],accepted,pending,progress); window_cases+=1
    max_members=tuple((i+1,'m'*1018+f'{i:06}',b'body') for i in range(59))
    maximum_admission=window_admission_bytes(max_members)
    maximum_progress=window_progress_bytes(maximum_admission,max_members,(),max_members)
    assert len(maximum_admission)==62780 and len(maximum_progress)==125640
    window_progress_read(maximum_progress,maximum_admission,max_members,()); window_cases+=1
    regressed=window_progress_bytes(prior['window_admission'],prior['accepted'],prior['unresolved'],accepted)
    for invalid_progress,current_accepted,current_pending in (
        (regressed,prior['accepted'],prior['unresolved']),
        (progress[:-1]+bytes([progress[-1]^1]),accepted,pending),(progress[:-1],accepted,pending)):
        try: window_admission_members(prior['window_admission'],claim,prior['roster'],current_accepted,current_pending,invalid_progress)
        except AssertionError: refusals+=1
        else: raise AssertionError('loop8 invalid window progress accepted')
        window_cases+=1
    positive=b'{"component":"\xc3\xa9","metadata":{"trace":"ok"},"schema":"hexalith.eventstore.destination/1","topic":"orders"}'
    assert destination_config(positive,'é','orders')['metadata']=={'trace':'ok'}; json_cases+=1
    assert destination_config(vectors['D17-destination-config'],'pubsub','orders'); json_cases+=1
    def canonical(value): return json.dumps(value,ensure_ascii=False,sort_keys=True,separators=(',',':')).encode()
    base={'component':'pubsub','metadata':{},'schema':'hexalith.eventstore.destination/1','topic':'orders'}
    for name in ('component','topic'):
        for size in (1023,1024,1025):
            for identifier in ('i'*size,'é'*(size//2)+'i'*(size%2)):
                value=dict(base); value[name]=identifier; args=(value['component'],value['topic'])
                try: destination_config(canonical(value),*args)
                except AssertionError: assert size==1025; refusals+=1
                else: assert size<=1024
                json_cases+=1
    for count in (63,64,65):
        value=dict(base,metadata={str(i):'' for i in range(count)})
        try: destination_config(canonical(value),'pubsub','orders')
        except AssertionError: assert count==65; refusals+=1
        else: assert count<=64
        json_cases+=1
    for size in (16383,16384,16385):
        for names in (False,True):
            for content in ('i'*size,'é'*(size//2)+'i'*(size%2)):
                value=dict(base,metadata={(content if names else ''):('' if names else content)})
                try: destination_config(canonical(value),'pubsub','orders')
                except AssertionError: assert size==16385; refusals+=1
                else: assert size<=16384
                json_cases+=1
    # Escape expansion reaches the document cap while decoded metadata stays bounded.
    for size in (65535,65536,65537):
        fixed=len(canonical(dict(base,metadata={'':'x'})))-1
        count,remainder=divmod(size-fixed,6)
        value=dict(base,metadata={'':'\x01'*count+'x'*remainder})
        raw=canonical(value); assert len(raw)==size
        try: destination_config(raw,'pubsub','orders')
        except AssertionError: assert size==65537; refusals+=1
        else: assert size<=65536
        json_cases+=1
    invalid=[positive+b'\n',positive+b' ',positive.replace(b'"component":',b'"component":"\xc3\xa9", "component":'),
        b'\xff'+positive,canonical(dict(base,schema='other')),canonical(dict(base,extra=1)),
        canonical({key:value for key,value in base.items() if key!='topic'}),canonical(dict(base,metadata={'k':3})),
        canonical(dict(base,metadata={'a':'x'*8192,'b':'y'*8192})),
        canonical(dict(base,metadata={'k':'v'})).replace(b'"k":"v"',b'"k":"v","k":"v"'),
        canonical(dict(base,component=3)),canonical(dict(base,topic=None)),canonical(dict(base,metadata=[])),
        canonical(dict(base,component='other')),canonical(dict(base,topic='other'))]
    for raw in invalid:
        try: destination_config(raw,'pubsub','orders')
        except (AssertionError,UnicodeError,json.JSONDecodeError): refusals+=1
        else: raise AssertionError('loop8 invalid destination accepted')
        json_cases+=1
    return {'widths':widths,'field_boundaries':boundaries,'window_cases':window_cases,'destination_cases':json_cases,'refusals':refusals}

loop8_codec_metrics=verify_loop8_codecs()
assert loop8_codec_metrics=={'widths':{'D12-legacy-claim':5270,'D12-usage':1136,'D12-tombstone':2219,'D29-counter':2176},
    'field_boundaries':65,'window_cases':5,'destination_cases':47,'refusals':52}
print(f'D12 codec verifier: {len(vectors)} original answers, {len(extra_vectors)} supplementary answers, {len(loop7_vectors)} loop-7 answers, {len(vectors)+len(extra_vectors)+len(loop7_vectors)} byte probes passed, {len(keys)} digest keys, {len(physical_answers)} physical addresses, {malformed_rejected} malformed records rejected, {semantic_rejected+loop5_semantic_rejected+pass8_semantic_rejected+loop7_semantic_rejected} semantic defects rejected')
print('loop8 codec probes:',loop8_codec_metrics)

def verify_loop9_codecs():
    owners=activation=c2_cases=closure_root_cases=0
    def encoded(label,fields):
        raw=vectors[label]; schema=schemas[label]
        return R(raw.split(b'\0',1)[0].decode(),len(schema),*(encode_typed(k,v) for k,v in zip(schema,fields)))
    def refusal(raw,domain,schema,reason=None,**kwargs):
        try: decode_record(raw,domain,schema,**kwargs)
        except AssertionError as error:
            if reason is not None: assert error.args[0]==reason,(reason,error.args)
        else: raise AssertionError('loop9 damaged codec authority accepted')
    base=list(decode_record(vectors['D37-entry'],'HX-EV-HOLD-ENTRY-2',schemas['D37-entry']))
    for code,permitted in hold_owners.items():
        for owner in ('actor','coordinator','gateway','subscriber','projection','operations','quota-coordinator'):
            f=list(base); f[2]=code; f[5]='handler-capability-hold' if code=='HeldDelivery' else hold_reasons[code]; f[11]=owner
            raw=encoded('D37-entry',f)
            if owner in permitted: assert decode_record(raw,'HX-EV-HOLD-ENTRY-2',schemas['D37-entry'])
            else: refusal(raw,'HX-EV-HOLD-ENTRY-2',schemas['D37-entry'])
            owners+=1
    fields=list(decode_record(vectors['D06-activation'],'HX-EV-FULL-REPLAY-ACTIVATION-2',schemas['D06-activation']))
    for events,readable in ((24575,4096),(24576,4096),(24577,4096),(100,48*MiB-1),(100,48*MiB),(100,48*MiB+1)):
        disposition='continue-full-replay' if events*8192<192*MiB and readable<48*MiB else 'hold'
        f=list(fields); f[5]=pack('>I',1)+U('route-a')+U(disposition)+N(events)+N(readable)+N(events*8192)+O(None)
        assert decode_record(encoded('D06-activation',f),'HX-EV-FULL-REPLAY-ACTIVATION-2',schemas['D06-activation']); activation+=1
    for events,accounting in ((30000,1),(100,819199),(100,819201),(((1<<64)-1)//8192+1,0)):
        f=list(fields); f[5]=pack('>I',1)+U('route-a')+U('continue-full-replay')+N(events)+N(4096)+N(accounting)+O(None)
        refusal(encoded('D06-activation',f),'HX-EV-FULL-REPLAY-ACTIVATION-2',schemas['D06-activation'],'activation-conservative-accounting'); activation+=1
    f=list(fields); f[5]=b''; refusal(encoded('D06-activation',f),'HX-EV-FULL-REPLAY-ACTIVATION-2',schemas['D06-activation']); activation+=1
    schema=['U','B32','N','B32','N','B','B32','Q']
    f=list(decode_record(attempt_set,'HX-EV-WINDOW-ATTEMPT-SET-1',schema))
    sources={key:value for key,value in c2_readbacks.items() if key[:3]==tuple(f[:3])}
    # Isolate the closure-root guard from stronger authenticated C2 guards:
    # retain the exact valid complete set/readbacks and change only closure tag 08.
    original_sources={tuple(f[:3]):attempt_set}
    assert decode_record(attempt_set,'HX-EV-WINDOW-ATTEMPT-SET-1',schema,c2_store=sources)[6]==attempt_root
    closure_fields=list(decode_record(closure,'HX-EV-PUBLICATION-WINDOW-CLOSURE-3',schemas['D45-closure'],attempt_store=original_sources,c2_store=sources))
    closure_fields[7]=H('changed-closure-attempt-root-only')
    changed_closure=encoded('D45-closure',closure_fields)
    try: decode_record(changed_closure,'HX-EV-PUBLICATION-WINDOW-CLOSURE-3',schemas['D45-closure'],attempt_store=original_sources,c2_store=sources)
    except AssertionError as error: assert error.args==()
    else: raise AssertionError('loop9 changed closure root accepted with valid C2 authority')
    closure_root_cases+=1
    key=('t',H('scope'),1,1,1)
    for damage in ('missing','receipt','tenant','window','member','ordinal','registration','send','observation'):
        bad=dict(sources)
        if damage=='missing': del bad[key]
        else:
            value=list(bad[key])
            if damage=='receipt': value[-1]=z
            else:
                index={'tenant':0,'window':2,'member':3,'ordinal':5,'registration':6,'send':7,'observation':11}[damage]
                value[index]='other' if damage=='tenant' else 99 if damage in {'window','member','ordinal'} else () if damage=='observation' else H('other')
                value[-1]=sha256(b'fixture-c2-parent-registration-readback:'+canonical_image_bytes(tuple(value[:-1]))).digest()
            bad[key]=tuple(value)
        refusal(attempt_set,'HX-EV-WINDOW-ATTEMPT-SET-1',schema,c2_store=bad); c2_cases+=1
    # Multiple nonce registrations under the same send ID are legal C2 retries.
    decoded=decode_rows(f[5],f[4],['P','N','N','U','B32','B32','B32'],count_ceiling=11328)
    parent,send=H('same-parent'),H('same-send'); raw_rows=[]; same={}
    for local in range(1,5):
        group=[r for r in decoded if r[1]==local]
        observations=tuple((r[2],r[3],r[6]) for r in group)
        source=c2_fixture_readback('t',H('scope'),1,1,'event-1',local,parent,send,observations,send_row=1,nonce_ordinal=local)
        same[('t',H('scope'),1,1,local)]=source
        raw_rows.extend(pack('>I',p)+N(o)+N(i)+U(k)+parent+send+e for p,o,i,k,a,d,e in group)
    f[5]=b''.join(raw_rows); f[6]=sha256(b'HX-EV-WINDOW-ATTEMPTS-2\0\x01'+U(f[0])+f[1]+N(f[2])+f[3]+N(f[4])+B(f[5])).digest()
    raw=R('HX-EV-WINDOW-ATTEMPT-SET-1',8,*(encode_typed(k,v) for k,v in zip(schema,f)))
    assert decode_record(raw,'HX-EV-WINDOW-ATTEMPT-SET-1',schema,c2_store=same); c2_cases+=1
    for damage in ('nonce-reuse','ordinal-reuse','send-row-gap','cumulative-gap'):
        bad=dict(same); key=('t',H('scope'),1,1,2); value=list(bad[key])
        if damage=='nonce-reuse': value[10]=bad[('t',H('scope'),1,1,1)][10]
        elif damage=='ordinal-reuse': value[9]=1
        elif damage=='send-row-gap': value[8]=3
        else: value[8]=2; value[9]=2; value[7]=H('other-row-send'); value[6]=H('other-parent')
        value[-1]=sha256(b'fixture-c2-parent-registration-readback:'+canonical_image_bytes(tuple(value[:-1]))).digest(); bad[key]=tuple(value)
        refusal(raw,'HX-EV-WINDOW-ATTEMPT-SET-1',schema,c2_store=bad); c2_cases+=1
    # Restore the literal known answer's existing provider readbacks.
    c2_readbacks.update(sources)
    return {'owner_cases':owners,'activation_cases':activation,'c2_cases':c2_cases,'closure_root_cases':closure_root_cases}

loop9_codec_metrics=verify_loop9_codecs()
assert loop9_codec_metrics=={'owner_cases':133,'activation_cases':11,'c2_cases':14,'closure_root_cases':1}
print('loop9 codec authority probes:',loop9_codec_metrics)

invocation_fixture_readbacks={fixture_invocation:extra_vectors['D45-invocation']}
PY
```

The lifecycle verifier covers every approved matrix row, status precedence, both pre-reserved queue counterparts, authenticated three-counter mutation, retry/orphan expiry, lossless legacy chunks, persisted preparation, partial capture, redrive/repair, membership resolution and cleanup. It reconstructs only actual bounded byte records and authenticated provider-native readbacks after restart. It executes 46 persisted restart boundaries, four cleanup boundaries, 70 durable-evidence refusals, three current-time completions, ten actual-transition codec matches and 50 repeated malformed rejections. It checks 54 historical dispositions, the 21/20/21/19 loop-3/4/5/6 repair IDs, 33 pass-8 repair IDs, seven loop-7 repair IDs, twelve iteration-8 repair IDs and eleven pass-11 repair IDs exactly once. Loop 7 executes 140 fresh queue restarts, 262 holding-counter moves, 28 authority/rerender/repair/erasure refusals, four cleanup boundaries, two exact predecessor repairs, eight generation boundaries, five encoded-quota refusals and eleven slice-2 readiness refusals. All 134 earlier directed source mutations, 17 iteration-8 guard mutations, thirteen iteration-9 owning mutations and eleven iteration-10 owning mutations (175 historical), plus five iteration-11 held/repair mutations (180 total), must fail their owning assertions as AssertionError; an unexpected interpreter exception fails the verifier. The 27 earlier Boolean checks remain invariant checks. Loop 6 asserts 64 distinct later-UTC/restart cases, four partial captures after observations, 32 continued-authority refusals, 23 signed-request refusals, six request restart boundaries, six erasure refusals and four repair-cleanup boundaries. Pass 8 adds typed membership transitions and lost acknowledgement, shared-backend identity reclamation/rollback admission preserving unrelated rows, original signature readback after full-origin reclamation and refund-last mid-deletion/refusal checks. Local models prove these bytes/transitions; actual provider atomicity remains future acceptance evidence.

```bash
python3 - <<'PY'
from collections import defaultdict
from copy import deepcopy
from hashlib import sha256
from pathlib import Path
from contextlib import redirect_stdout
from io import StringIO
import re
import json

MiB = 1024 * 1024
U64_MAX = (1 << 64) - 1
candidate_text = Path('_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md').read_text()
codec_source = candidate_text.split("python3 - <<'PY'\n",1)[1].split('\nPY\n```',1)[0]
codec = {}
with redirect_stdout(StringIO()):
    exec(codec_source, codec)
U, B, N, O, R, I, Q = (codec[name] for name in ('U','B','N','O','R','I','Q'))
decode_record = codec['decode_record']
unresolved_root = codec['unresolved_root']
supplementary_records = {}
preparation_metrics = {}

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
           prepared=True, terminal=False, drain_active=False, failure_classes=(),
           at_max=False, auto=False):
    if conflict:
        return ('CommandOutcomeHold', 'outcome_evidence_conflict', 30)
    if not evidence:
        return ('CommandOutcomeHold', 'outcome_evidence_hold', 30)
    if not prepared:
        return ('CommandOutcomeHold', 'response_preparation_hold', 30)
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
    classes = set(failure_classes)
    if state == 'failed' and classes & {'class-02', 'class-03'}:
        return ('CommandOutcomeHold', 'terminal_evidence_hold', 30)
    if state == 'failed' and classes and classes == {'class-01'} and at_max:
        return ('EventsStored', 'publication_retry_exhausted_hold', 60)
    if state == 'failed' and classes and classes == {'class-01'} and auto:
        return ('EventsStored', 'publication_retry_pending', 1)
    if state in {'pending', 'unknown'}:
        return ('EventsStored', None, 1)
    return ('CommandOutcomeHold', 'outcome_evidence_conflict', 30)

status_cases = [
    (status('pending'), ('EventsStored', None, 1)),
    (status('unknown', at_max=True), ('EventsStored', None, 1)),
    (status('pending', drain_active=True), ('EventsStored', 'publication_drain_limit_hold', 60)),
    (status('failed', at_max=True, failure_classes=('class-01',)), ('EventsStored', 'publication_retry_exhausted_hold', 60)),
    (status('failed', at_max=True, failure_classes=('class-02',)), ('CommandOutcomeHold', 'terminal_evidence_hold', 30)),
    (status('failed', failure_classes=('class-03',)), ('CommandOutcomeHold', 'terminal_evidence_hold', 30)),
    (status('failed', auto=True, failure_classes=('class-01',)), ('EventsStored', 'publication_retry_pending', 1)),
    (status('failed', terminal=True), ('PublishFailed', 'publication_terminal_failed', None)),
    (status('pending', conflict=True), ('CommandOutcomeHold', 'outcome_evidence_conflict', 30)),
    (status('published'), ('Completed', None, None)),
    (status('published', classification='rejection'), ('Rejected', None, None)),
    (status('published', classification='unknown'), ('CommandOutcomeHold', 'outcome_evidence_conflict', 30)),
    (status('not-applicable'), ('Completed', None, None)),
    (status('failed', at_max=True, failure_classes=('unknown',)), ('CommandOutcomeHold', 'outcome_evidence_conflict', 30)),
]
assert all(actual == expected for actual, expected in status_cases)
for kwargs in [
    {'terminal':True}, {'state':'published'}, {'drain_active':True},
    {'state':'failed', 'failure_classes':('class-01',), 'at_max':True},
    {'state':'failed', 'failure_classes':('class-02',), 'at_max':True},
]:
    state = kwargs.pop('state', 'pending')
    assert status(state, conflict=True, **kwargs)[1] == 'outcome_evidence_conflict'
    assert status(state, evidence=False, **kwargs)[1] == 'outcome_evidence_hold'
assert status('published', prepared=False)[1] == 'response_preparation_hold'

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
        self.generations = defaultdict(int)
        self.charges = {}
        self.reservations = {}
        self.objects = {}
        self.inventory = {}
        self.inventory_ceiling = 10000
        self.pin_capability = codec['vectors']['D29-capability']
        self.pin_evidence_available = True
        self.pin_capability_receipt = sha256(b'fixture-current-pin-capability:'+self.pin_capability).digest()
        self.pin_candidate_authority = {}
    def authenticated_limits(self):
        assert self.pin_evidence_available, 'pin-capability-unavailable'
        assert self.pin_capability_receipt == sha256(b'fixture-current-pin-capability:'+self.pin_capability).digest(), 'pin-capability-readback'
        f=decode_record(self.pin_capability,'HX-EV-PUBLICATION-RETENTION-CAPABILITY-2',codec['schemas']['D29-capability'])
        return f[3],f[4],f[5],f[6]
    def install_capability(self, raw):
        decode_record(raw,'HX-EV-PUBLICATION-RETENTION-CAPABILITY-2',codec['schemas']['D29-capability'])
        self.pin_capability=raw
        self.pin_capability_receipt=sha256(b'fixture-current-pin-capability:'+raw).digest()
    def predecessors(self, account, account_kind='tenant'):
        assert account_kind in {'tenant','capture-scope'}
        pool='tenant-pool' if account_kind=='tenant' else 'unidentified'
        used = {account_kind:self.tenant.get((account_kind,account),0),
                pool:self.tenant_pool if account_kind=='tenant' else self.unidentified,
                'deployment':self.deployment}
        return {kind:sha256(U(kind) + U(account if kind == account_kind else kind)
                    + N(value) + N(self.generations.get((kind,account if kind == account_kind else kind),0))).digest()
                for kind,value in used.items()}
    def pin_identity(self, account, amounts, batch_id, scope=None, candidate=None, request_id=None, candidates=None):
        scope = sha256(b'scope:'+batch_id).digest() if scope is None else scope
        candidate = sha256(b'candidate:'+batch_id).digest() if candidate is None else candidate
        request_id = sha256(U(account)+scope+candidate+b''.join(N(v) for v in amounts)).digest() if request_id is None else request_id
        return ('tenant',account,scope,candidate,request_id,tuple(amounts),tuple(candidates or ()))
    def pin_candidates(self, account, amounts):
        # Authenticated renderer/provider fixture; callers pass these bytes explicitly.
        capability=decode_record(self.pin_capability,'HX-EV-PUBLICATION-RETENTION-CAPABILITY-2',codec['schemas']['D29-capability'])
        overhead=capability[7]
        rows=tuple(R('HX-EV-PUBLICATION-CHARGE-2',15,U('deployment-a'),U('tenant'),U(account),
            sha256(N(i)).digest(),U('pin-batch'),N(amount-overhead),N(overhead),N(amount),N(capability[1]),N(1),
            U('active'),bytes(32),O(None),Q(codec['t']),N(0)) for i,amount in enumerate(amounts))
        for raw in rows:
            self.pin_candidate_authority[sha256(raw).digest()]=sha256(b'fixture-authenticated-pin-candidate:'+self.pin_capability+raw).digest()
        return rows
    def authenticate_pin_candidates(self,account,amounts,candidates):
        try:
            assert self.pin_evidence_available and candidates is not None and 1<=len(candidates)<=59
            capability=decode_record(self.pin_capability,'HX-EV-PUBLICATION-RETENTION-CAPABILITY-2',codec['schemas']['D29-capability'])
            assert len(candidates)==len(amounts)
            for i,(amount,raw) in enumerate(zip(amounts,candidates)):
                assert self.pin_candidate_authority.get(sha256(raw).digest())==sha256(b'fixture-authenticated-pin-candidate:'+self.pin_capability+raw).digest()
                f=decode_record(raw,'HX-EV-PUBLICATION-CHARGE-2',codec['schemas']['D29-charge'])
                assert f[:5]==('deployment-a','tenant',account,sha256(N(i)).digest(),'pin-batch')
                assert f[5]<=449*MiB and f[6]==capability[7] and f[7]==amount and f[8]==capability[1]
                assert f[9:13]==(1,'active',bytes(32),None) and f[14]==0
            return True
        except (AssertionError,KeyError,TypeError,ValueError): return False
    def read_pin_reservation(self, account, amounts, batch_id, scope=None, candidate=None, request_id=None, candidates=None):
        if not valid_amounts(amounts): return False
        identity = self.pin_identity(account,amounts,batch_id,scope,candidate,request_id,candidates)
        row = self.reservations.get(batch_id)
        return bool(row and row['identity'] == identity and row['state'] == 'reserved'
                    and row['receipt'] == state_hash({key:value for key,value in row.items() if key != 'receipt'})
                    and self.charges.get(batch_id) == tuple(amounts))
    def reserve_pin_batch(self, account, amounts, predecessors, batch_id, scope=None, candidate=None, request_id=None, candidates=None):
        before = deepcopy(vars(self))
        if predecessors != self.predecessors(account) or not valid_amounts(amounts) or not self.authenticate_pin_candidates(account,amounts,candidates):
            assert vars(self) == before
            return False
        if batch_id in self.reservations:
            return self.read_pin_reservation(account,amounts,batch_id,scope,candidate,request_id,candidates)
        if not self.reserve('tenant', account, amounts):
            assert vars(self) == before
            return False
        self.charges[batch_id] = tuple(amounts)
        row = {'amounts':tuple(amounts), 'predecessors':dict(predecessors), 'state':'reserved',
               'identity':self.pin_identity(account,amounts,batch_id,scope,candidate,request_id,candidates)}
        row['receipt'] = state_hash(row)
        self.reservations[batch_id] = row
        return True
    def reserve(self, account_kind, account, amounts):
        before = (dict(self.tenant), self.tenant_pool, self.unidentified, self.deployment)
        if account_kind not in {'tenant', 'capture-scope'}:
            assert before == (dict(self.tenant), self.tenant_pool, self.unidentified, self.deployment)
            return False
        if not valid_amounts(amounts):
            assert before == (dict(self.tenant), self.tenant_pool, self.unidentified, self.deployment)
            return False
        try: tenant_limit,deployment_limit,reserve_limit,unidentified_limit=self.authenticated_limits()
        except (AssertionError,KeyError,TypeError,ValueError): return False
        total = sum(amounts)
        account_key=(account_kind,account)
        current = self.tenant.get(account_key, 0)
        if account_kind == 'tenant':
            fits = (current + total <= tenant_limit
                    and self.tenant_pool + total <= deployment_limit - reserve_limit
                    and self.deployment + total <= deployment_limit)
        else:
            fits = (current + total <= tenant_limit
                    and self.unidentified + total <= unidentified_limit
                    and self.deployment + total <= deployment_limit)
        changed = [(account_kind,account),('tenant-pool' if account_kind == 'tenant' else 'unidentified',
                   'tenant-pool' if account_kind == 'tenant' else 'unidentified'),('deployment','deployment')]
        if not fits or any(self.generations.get(key,0) == U64_MAX for key in changed):
            assert before == (dict(self.tenant), self.tenant_pool, self.unidentified, self.deployment)
            return False
        self.tenant[account_key] += total
        if account_kind == 'tenant': self.tenant_pool += total
        else: self.unidentified += total
        self.deployment += total
        for kind, identity in changed:
            self.generations[(kind,identity)] += 1
        return True
    def refund(self, account_kind, account, amount):
        before = (dict(self.tenant), self.tenant_pool, self.unidentified, self.deployment)
        changed = [(account_kind,account),('tenant-pool' if account_kind == 'tenant' else 'unidentified',
                   'tenant-pool' if account_kind == 'tenant' else 'unidentified'),('deployment','deployment')]
        if (account_kind not in {'tenant', 'capture-scope'} or type(amount) is not int
                or not 0 <= amount <= self.tenant.get((account_kind,account),0)
                or amount > (self.tenant_pool if account_kind == 'tenant' else self.unidentified)
                or amount > self.deployment
                or any(self.generations.get(key,0) == U64_MAX for key in changed)):
            assert before == (dict(self.tenant), self.tenant_pool, self.unidentified, self.deployment)
            return False
        self.tenant[(account_kind,account)] -= amount
        if account_kind == 'tenant': self.tenant_pool -= amount
        else: self.unidentified -= amount
        self.deployment -= amount
        for key in changed: self.generations[key] += 1
        return True

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
refund_snapshot = (dict(ledger.tenant), ledger.tenant_pool, ledger.unidentified, ledger.deployment)
assert not ledger.refund('unknown', 't1', 1)
assert refund_snapshot == (dict(ledger.tenant), ledger.tenant_pool, ledger.unidentified, ledger.deployment)
invalid_snapshot = (dict(ledger.tenant), ledger.tenant_pool, ledger.unidentified, ledger.deployment)
assert not ledger.reserve('tenant', 't1', [-1])
assert invalid_snapshot == (dict(ledger.tenant), ledger.tenant_pool, ledger.unidentified, ledger.deployment)

class ChargeSwap:
    def __init__(self, old_amount, ceiling):
        self.old = old_amount
        self.staged = 0
        self.active = old_amount
        self.used = old_amount
        self.ceiling = ceiling
        self.owner = None
        self.finalized = False
    def stage(self, new_amount, owner):
        before = vars(self).copy()
        if self.staged:
            return owner == self.owner and new_amount == self.staged
        if (type(new_amount) is not int or new_amount <= 0
                or new_amount > U64_MAX-self.used or self.used+new_amount > self.ceiling):
            assert before == vars(self)
            return False
        self.staged = new_amount; self.used += new_amount; self.owner = owner
        return True
    def recover(self, owner, successor_read_back, success_audit_read_back=None):
        if owner != self.owner or self.staged == 0:
            return False
        successor = validate_presence(successor_read_back,owner,'successor',getattr(self,'generation',1))
        audit = validate_presence(success_audit_read_back,owner,'audit',getattr(self,'generation',1))
        if successor is None or audit is None:
            return False
        if audit == 'present' and successor == 'absent':
            return False  # a recorded partial success cannot discard its stage
        if successor == 'present':
            if audit != 'present': return False
            if not self.finalized:
                self.used -= self.old
                self.active = self.staged
                self.old = 0
                self.staged = 0
                self.finalized = True
        else:
            self.used -= self.staged
            self.staged = 0
            self.owner = None
        return True

def presence(owner, kind, outcome, generation=1):
    payload = ('authenticated-readback',owner,kind,generation,outcome)
    return payload + (sha256(repr(payload).encode()).digest(),)

def validate_presence(receipt, owner, kind, expected_generation=1):
    if not isinstance(receipt,tuple) or len(receipt) != 6: return None
    label,claimed_owner,claimed_kind,generation,outcome,digest = receipt
    if (label != 'authenticated-readback' or claimed_owner != owner or claimed_kind != kind
            or generation != expected_generation or outcome not in {'present','absent'}
            or digest != sha256(repr(receipt[:-1]).encode()).digest()): return None
    return outcome

rolled_back_swap = ChargeSwap(600, 1000)
assert rolled_back_swap.stage(400, b'request-a') and rolled_back_swap.used == 1000
assert rolled_back_swap.active == 600
assert rolled_back_swap.recover(b'request-a', presence(b'request-a','successor','absent'), presence(b'request-a','audit','absent')) and rolled_back_swap.used == rolled_back_swap.active == 600
completed_swap = ChargeSwap(600, 1200)
assert completed_swap.stage(500, b'request-b') and completed_swap.active == 600
assert completed_swap.recover(b'request-b', presence(b'request-b','successor','present'), presence(b'request-b','audit','present')) and completed_swap.used == completed_swap.active == 500
completed_state = vars(completed_swap).copy()
assert not completed_swap.recover(b'request-b', presence(b'request-b','successor','present'), presence(b'request-b','audit','present')) and completed_state == vars(completed_swap)
refused_swap = ChargeSwap(600, 1000)
refused_state = vars(refused_swap).copy()
assert not refused_swap.stage(401, b'request-c') and refused_state == vars(refused_swap)
audited_swap = ChargeSwap(600,1200)
assert audited_swap.stage(500,b'audited-request')
audited_snapshot = vars(audited_swap).copy()
assert not audited_swap.recover(b'audited-request',presence(b'audited-request','successor','absent'),presence(b'audited-request','audit','present'))
assert vars(audited_swap) == audited_snapshot
assert audited_swap.recover(b'audited-request',presence(b'audited-request','successor','present'),presence(b'audited-request','audit','present'))
assert audited_swap.active == audited_swap.used == 500
audit_finalized = vars(audited_swap).copy()
assert not audited_swap.recover(b'audited-request',presence(b'audited-request','successor','present'),presence(b'audited-request','audit','present'))
assert vars(audited_swap) == audit_finalized and audited_swap.owner == b'audited-request'
assert not rolled_back_swap.recover(b'request-a',False) and rolled_back_swap.used == 600
owned_swap = ChargeSwap(600,1200)
assert owned_swap.stage(500,b'owner')
owned_snapshot = vars(owned_swap).copy()
assert owned_swap.stage(500,b'owner') and vars(owned_swap) == owned_snapshot
assert not owned_swap.stage(500,b'other-owner') and vars(owned_swap) == owned_snapshot
assert not owned_swap.recover(b'other-owner',True,True) and vars(owned_swap) == owned_snapshot
for successor_receipt,audit_receipt in [(None,None),(False,False),(True,True),
        (presence(b'owner','successor','absent'),None),
        (presence(b'other','successor','absent'),presence(b'owner','audit','absent')),
        (presence(b'owner','successor','absent',2),presence(b'owner','audit','absent')),
        (presence(b'owner','successor','absent')[:-1]+(bytes(32),),presence(b'owner','audit','absent'))]:
    assert not owned_swap.recover(b'owner',successor_receipt,audit_receipt)
    assert vars(owned_swap) == owned_snapshot

class QueueBytes:
    """Provider fixture: only addressed record bytes and bounded typed native receipts survive."""
    def __init__(self):
        self.records = {}
        self.native = {}
        self.unavailable = set()
    def __eq__(self,other): return type(other) is type(self) and vars(self) == vars(other)
    def receipt_key(self,key): return codec['K']('HX-EV-PIN-QUEUE-RECEIPT-KEY-1',U('deployment-a'),U(key))
    def native_record(self,key,owner,generation,digest,predecessor,action):
        fields=(key,owner,generation,digest,predecessor,codec['t'],action)
        encoded=[codec['encode_typed'](k,v) for k,v in zip(codec['loop7_schemas']['D31-receipt'][:-1],fields)]
        return R('HX-EV-PIN-QUEUE-RECEIPT-1',8,*encoded,sha256(b'fixture-queue-provider:'+b''.join(encoded)).digest())
    def receipt(self,key):
        rkey=self.receipt_key(key)
        assert key not in self.unavailable and rkey not in self.unavailable
        raw=self.native[rkey]
        fields=decode_record(raw,'HX-EV-PIN-QUEUE-RECEIPT-1',codec['loop7_schemas']['D31-receipt'])
        assert fields[0] == key
        return raw,fields
    def read(self,key,owner=None):
        assert key not in self.unavailable
        raw=self.records[key]; receipt,fields=self.receipt(key)
        assert fields[6] == 'present' and fields[3] == sha256(raw).digest(), 'queue-native-readback'
        assert owner is None or fields[1] == owner, 'queue-native-owner'
        return raw,receipt,fields[2]
    def write(self,key,raw,owner,generation):
        prior=self.native.get(self.receipt_key(key),b'')
        self.records[key]=raw
        self.native[self.receipt_key(key)]=self.native_record(key,owner,generation,sha256(raw).digest(),sha256(prior).digest() if prior else bytes(32),'present')
        assert self.read(key,owner)[0] == raw
    def delete(self,key,owner):
        raw,old,generation=self.read(key,owner)
        assert generation < U64_MAX
        del self.records[key]
        self.native[self.receipt_key(key)]=self.native_record(key,owner,generation+1,sha256(raw).digest(),sha256(old).digest(),'deleted')
        return self.native[self.receipt_key(key)]
    def forget(self,key):
        self.records.pop(key,None); self.native.pop(self.receipt_key(key),None)

class Queues:
    # Forty KiB per admitted owner; directory sources/receipts are precharged separately.
    def __init__(self,ceiling=50000,backend=None):
        self.ceiling=ceiling
        self.backend=backend if backend is not None else QueueBytes()
        if backend is None:
            fields=list(decode_record(codec['vectors']['D29-capability'],'HX-EV-PUBLICATION-RETENTION-CAPABILITY-2',codec['schemas']['D29-capability']))
            fields[13]=ceiling
            cap=R('HX-EV-PUBLICATION-RETENTION-CAPABILITY-2',15,*(codec['encode_typed'](k,v) for k,v in zip(codec['schemas']['D29-capability'],fields)))
            self.backend.write(self.capability_key(),cap,bytes(32),fields[1])
            self._bootstrap('deployment')
            self._ledger(self.backend,{})
    @staticmethod
    def sort_key(row):
        scope_hash=row[2] if isinstance(row[2],bytes) else sha256(U(row[2])).digest()
        return (row[0],row[1].encode(),scope_hash)
    def address(self,kind,value):
        names={'queue':'HX-EV-PIN-CAPACITY-QUEUE-KEY-1','owners':'HX-EV-PIN-QUEUE-OWNERS-KEY-1',
               'predecessor':'HX-EV-PIN-QUEUE-PREDECESSOR-KEY-1','authority':'HX-EV-PIN-WAIT-AUTHORITY-KEY-1',
               'wait':'HX-EV-PIN-CAPACITY-WAIT-KEY-1'}
        field=U(value) if kind in {'queue','owners','predecessor'} else self.subject(value)
        return codec['K'](names[kind],U('deployment-a'),field)
    def capability_key(self,revision=None):
        if revision is None:
            head='fixture-queue-capability-head'
            revision=int.from_bytes(self.backend.read(head,bytes(32))[0],'big') if head in self.backend.records else 3
        return codec['K']('HX-EV-PUBLICATION-CAPABILITY-KEY-1',U('deployment-a'),N(revision))
    def _capability(self,backend=None):
        raw,receipt,g=(self.backend if backend is None else backend).read(self.capability_key(),bytes(32))
        f=decode_record(raw,'HX-EV-PUBLICATION-RETENTION-CAPABILITY-2',codec['schemas']['D29-capability'])
        assert f[0]=='deployment-a' and f[1]==g and f[13]==self.ceiling
        return f
    def charge_key(self,subject,account,account_kind='tenant'):
        return codec['K']('HX-EV-PUBLICATION-CHARGE-KEY-1',U('deployment-a'),U(account_kind),U(account),subject)
    def counter_key(self,kind,account):
        return codec['K']('HX-EV-PUBLICATION-COUNTER-KEY-1',U('deployment-a'),U(kind),U(account))
    def _recorded_capability(self,backend,revision):
        raw,receipt,generation=backend.read(self.capability_key(revision),bytes(32))
        f=decode_record(raw,'HX-EV-PUBLICATION-RETENTION-CAPABILITY-2',codec['schemas']['D29-capability'])
        assert f[0]=='deployment-a' and f[1]==generation==revision, 'queue-original-capability-revision'
        return f
    def _charge(self,backend,subject,account,amount,account_kind='tenant'):
        key=self.charge_key(subject,account,account_kind)
        if key in backend.records:
            raw,receipt,g=backend.read(key,subject)
            f=decode_record(raw,'HX-EV-PUBLICATION-CHARGE-2',codec['schemas']['D29-charge'])
            original=self._recorded_capability(backend,f[8])
            assert f[0:5]==('deployment-a',account_kind,account,subject,'side-record') and f[5]==amount
            assert f[6]==original[7] and f[7]==amount+f[6], 'queue-recorded-overhead'
            assert f[9]==g and f[10]=='active' and f[12] is None and f[14]==0
        else:
            capability=self._capability(backend); overhead=capability[7]
            raw=R('HX-EV-PUBLICATION-CHARGE-2',15,U('deployment-a'),U(account_kind),U(account),subject,U('side-record'),
                N(amount),N(overhead),N(amount+overhead),N(capability[1]),N(1),U('active'),bytes(32),O(None),Q(codec['t']),N(0))
            backend.write(key,raw,subject,1)
        return decode_record(raw,'HX-EV-PUBLICATION-CHARGE-2',codec['schemas']['D29-charge'])[7]
    def _contributions(self,items,backend=None):
        backend=self.backend if backend is None else backend
        owners={a[2]:('tenant',a[1],a[14]) for a in items.values()}
        for target in self._targets(items):
            subject=sha256(U(self.address('queue',target))).digest()
            account_kind,account=('tenant',target[7:]) if target.startswith('tenant:') else ('capture-scope','deployment:'+sha256(U('deployment-a')).hexdigest())
            owners[subject]=(account_kind,account,142*MiB+32768)
        for subject,(kind,account,length) in list(owners.items()):
            key=self.charge_key(subject,account,kind)
            if key in backend.records:
                raw=backend.read(key,subject)[0]
                f=decode_record(raw,'HX-EV-PUBLICATION-CHARGE-2',codec['schemas']['D29-charge'])
                assert f[:5]==('deployment-a',kind,account,subject,'side-record') and f[5]==length and f[10]=='active'
                original=self._recorded_capability(backend,f[8])
                assert f[6]==original[7] and f[7]==length+f[6], 'queue-recorded-overhead'
                amount=f[7]
            else: amount=length+self._capability(backend)[7]
            owners[subject]=(kind,account,amount)
        return owners
    def _counter_values(self,owners,previous=None):
        totals=defaultdict(int); counts=defaultdict(int)
        for kind,account,amount in owners.values(): totals[(kind,account)]+=amount; counts[(kind,account)]+=1
        accounts=set(totals)|{(k,a) for k,a,n in (previous or {}).values()}
        rows=[(k,a,totals[(k,a)],counts[(k,a)]) for k,a in sorted(accounts)]
        rows += [('tenant-pool','*',sum(n for (k,a),n in totals.items() if k=='tenant'),sum(n for (k,a),n in counts.items() if k=='tenant')),
                 ('unidentified','*',sum(n for (k,a),n in totals.items() if k=='capture-scope'),sum(n for (k,a),n in counts.items() if k=='capture-scope')),
                 ('deployment','deployment',sum(totals.values()),sum(counts.values()))]
        return rows
    def _ledger(self,backend,items,old_items=None,release_deployment=False):
        owners=self._contributions(items,backend); prior_owners=self._contributions(old_items or {},backend) if old_items is not None else {}
        if release_deployment:
            assert not items
            owners.pop(sha256(U(self.address('queue','deployment'))).digest())
        current={}; prior={(k,a):(u,n) for k,a,u,n in self._counter_values(prior_owners)}
        capability=self._capability(backend)
        for kind,account,used,count in self._counter_values(owners,prior_owners):
            key=self.counter_key(kind,account); previous=backend.records.get(key)
            old_used,old_count=prior.get((kind,account),(0,0))
            if previous is None: generation,predecessor,installed_used,installed_count=1,bytes(32),0,0
            else:
                raw,receipt,generation=backend.read(key,bytes(32))
                f=decode_record(raw,'HX-EV-PUBLICATION-COUNTER-1',codec['schemas']['D29-counter'])
                assert f[0:3]==('deployment-a',kind,account) and f[5]==generation
                installed_used,installed_count=f[3:5]
                assert installed_used>=old_used and installed_count>=old_count
                used=installed_used-old_used+used; count=installed_count-old_count+count
                if (installed_used,installed_count)==(used,count): continue
                assert generation < U64_MAX, 'queue-counter-generation-exhausted'
                generation+=1; predecessor=sha256(previous).digest()
            ceiling=(capability[3] if kind=='tenant' else capability[4]-capability[5] if kind=='tenant-pool' else
                     capability[4] if kind=='deployment' else capability[6])
            assert used<=ceiling or used<=installed_used, 'queue-storage-capacity-refusal'
            assert used <= U64_MAX and count <= U64_MAX
            raw=R('HX-EV-PUBLICATION-COUNTER-1',8,U('deployment-a'),U(kind),U(account),N(used),N(count),N(generation),predecessor,Q(codec['t']))
            current[key]=(raw,generation)
        # Every current receipt/ceiling/generation preflights before any staged object installation.
        for subject,(kind,account,amount) in owners.items():
            length=items[next(s for s,a in items.items() if a[2]==subject)][14] if any(a[2]==subject for a in items.values()) else 142*MiB+32768
            assert self._charge(backend,subject,account,length,kind)==amount
        for subject,(kind,account,amount) in prior_owners.items():
            if subject not in owners: backend.forget(self.charge_key(subject,account,kind))
        for key,(raw,generation) in current.items(): backend.write(key,raw,bytes(32),generation)
    def subject(self,scope):
        return sha256(b'HX-EV-CAPACITY-SUBJECT-1\0\x01'+sha256(U(scope)).digest()+codec['H']('plan:'+scope)).digest()
    def owner(self,tenant,scope): return 'queue-owner:'+sha256(U(tenant)+U(scope)).hexdigest()
    def encode(self,label,values):
        schema=codec['loop7_schemas'][label]; domain=codec['loop7_domains'][label]
        raw=R(domain,len(schema),*(codec['encode_typed'](k,v) for k,v in zip(schema,values)))
        decode_record(raw,domain,schema)
        return raw
    def decode(self,label,raw): return decode_record(raw,codec['loop7_domains'][label],codec['loop7_schemas'][label])
    def _bootstrap(self,target):
        queue=R('HX-EV-PIN-CAPACITY-QUEUE-4',11,U('deployment-a'),U(target),N(1),N(0),B(b''),N(0),N(0),N(self.ceiling),N(0),bytes(32),Q(codec['t']))
        owners=self.encode('D31-owners',('deployment-a',target,1,0,b'',bytes(32),codec['t'],self.ceiling))
        self.backend.write(self.address('queue',target),queue,bytes(32),1)
        self.backend.write(self.address('owners',target),owners,bytes(32),1)
    def _indexed(self,target):
        key=self.address('owners',target)
        raw,receipt,generation=self.backend.read(key,bytes(32))
        values=self.decode('D31-owners',raw)
        assert values[0:2] == ('deployment-a',target) and values[2] == generation and values[7] == self.ceiling
        return values,codec['decode_rows'](values[4],values[3],['N','B32','B32'])
    def _items(self):
        # Enumerate deployment owner bytes, never the provider's record dictionary.
        _,root=self._indexed('deployment')
        items={}
        for ticket,subject,digest in root:
            key=codec['K']('HX-EV-PIN-WAIT-AUTHORITY-KEY-1',U('deployment-a'),subject)
            raw,receipt,generation=self.backend.read(key,subject)
            a=list(self.decode('D31-authority',raw))
            assert sha256(raw).digest() == digest and a[2] == subject and a[5] == ticket and a[8] == generation
            carrier=self.decode('D31-carrier',a[6]); scope=carrier[1]
            assert self.charge_key(a[2],a[1]) in self.backend.records, 'queue-charge-missing'
            self._charge(self.backend,a[2],a[1],a[14])
            assert self.subject(scope) == subject and scope not in items
            if a[10] == 'admitted':
                wait,wr,wg=self.backend.read(self.address('wait',scope),subject)
                w=decode_record(wait,'HX-EV-PIN-CAPACITY-WAIT-2',codec['schemas']['D31-wait'])
                assert w[0] == a[1] and w[1] == a[3] and w[5] == ticket and sha256(wait).digest() == a[21]
                assert w[8] in {'queued','parked'} and w[10] == carrier[4]
                assert w[4] == ('tenant' if a[11]=='tenant' else 'deployment'), 'queue-wait-residence'
            elif a[10] == 'cleanup':
                assert self.address('wait',scope) not in self.backend.records
                deletion,df=self.backend.receipt(self.address('wait',scope))
                assert df[6] == 'deleted' and df[1] == subject and sha256(deletion).digest() == a[20]
            else: assert self.address('wait',scope) not in self.backend.records
            items[scope]=a
        return items
    def _targets(self,items): return {'deployment'}|{'tenant:'+a[1] for a in items.values()}
    def _rows(self,items,target,backend=None):
        backend=self.backend if backend is None else backend
        return sorted([[a[5],a[1],scope,decode_record(backend.read(self.address('wait',scope),a[2])[0],
            'HX-EV-PIN-CAPACITY-WAIT-2',codec['schemas']['D31-wait'])[8]] for scope,a in items.items()
            if a[10] == 'admitted' and ('deployment' if a[11] == 'deployment' else 'tenant:'+a[1]) == target],key=self.sort_key)
    def _owner_rows(self,items,target):
        return sorted([(a[5],a[2],sha256(self.encode('D31-authority',a)).digest()) for a in items.values()
            if target == 'deployment' or target == 'tenant:'+a[1]],key=lambda r:(r[0],r[1]))
    def _check(self):
        items=self._items()
        for target in self._targets(items):
            raw,receipt,generation=self.backend.read(self.address('queue',target),bytes(32))
            q=decode_record(raw,'HX-EV-PIN-CAPACITY-QUEUE-4',codec['schemas']['D31-queue'])
            o,index=self._indexed(target)
            assert q[0:2] == ('deployment-a',target) and q[2] == generation == o[2] and q[7] == self.ceiling
            expected=self._rows(items,target)
            encoded=b''.join(encode_queue_row(row) for row in expected)
            assert q[3] == len(expected) and q[4] == encoded and q[5] == sum(r[3]=='parked' for r in expected)
            assert index == self._owner_rows(items,target), 'queue-paired-owner-index'
            assert q[6] == len(index)-len(expected) and q[3]+q[6] <= self.ceiling
            assert all(r[0] <= self.last_ticket for r in index)
        self._ledger_read(items)
        return items
    def _ledger_read(self,items):
        self._capability()
        owners=self._contributions(items)
        for subject,(kind,account,amount) in owners.items():
            assert self.charge_key(subject,account,kind) in self.backend.records, 'queue-directory-charge-missing'
            length=next((a[14] for a in items.values() if a[2]==subject),142*MiB+32768)
            assert self._charge(self.backend,subject,account,length,kind)==amount
        for kind,account,used,count in self._counter_values(owners):
            raw,receipt,g=self.backend.read(self.counter_key(kind,account),bytes(32))
            f=decode_record(raw,'HX-EV-PUBLICATION-COUNTER-1',codec['schemas']['D29-counter'])
            assert f[0:3]==('deployment-a',kind,account) and f[5]==g and f[3]>=used and f[4]>=count, 'queue-counter-authority'
    @property
    def last_ticket(self):
        raw=self.backend.read(self.address('queue','deployment'),bytes(32))[0]
        return decode_record(raw,'HX-EV-PIN-CAPACITY-QUEUE-4',codec['schemas']['D31-queue'])[8]
    @last_ticket.setter
    def last_ticket(self,value):
        assert 0 <= value <= U64_MAX
        self._save(self._check(),value)
    @property
    def rows(self):
        items=self._check(); return defaultdict(list,{t:self._rows(items,t) for t in self._targets(items)})
    @property
    def reservations(self):
        items=self._check()
        return defaultdict(set,{t:{s for s,a in items.items() if (t=='deployment' or t=='tenant:'+a[1])
            and not(a[10]=='admitted' and ('deployment' if a[11]=='deployment' else 'tenant:'+a[1])==t)} for t in self._targets(items)})
    @property
    def where(self): return {s:('deployment' if a[11]=='deployment' else 'tenant:'+a[1]) for s,a in self._check().items() if a[10]=='admitted'}
    @property
    def tickets(self): return {s:a[5] for s,a in self._check().items()}
    @property
    def charges(self): return {s:a[14] for s,a in self._check().items()}
    def _claims(self,phase):
        return {s:{'carrier':(a[1],self.decode('D31-carrier',a[6])[2]),'ticket':a[5]} for s,a in self._check().items() if a[10]==phase}
    @property
    def pending(self): return self._claims('allocated')
    @property
    def receipts(self): return self._claims('admitted')
    def _has_room(self,target): return len(self.rows[target])+len(self.reservations[target]) < self.ceiling
    def _save(self,items,last_ticket=None,deleted=None,old_override=None,wait_override=None):
        # Single required ledger transaction. A private staging copy is not persisted authority.
        old=self._items() if old_override is None else old_override; oldtargets=self._targets(old); targets=self._targets(items)|oldtargets
        staged=deepcopy(self.backend)
        for target in targets:
            if self.address('queue',target) not in staged.records:
                temp=Queues.__new__(Queues); temp.ceiling=self.ceiling; temp.backend=staged; temp._bootstrap(target)
        for target in targets:
            for kind in ('queue','owners'):
                assert staged.read(self.address(kind,target),bytes(32))[2] < U64_MAX, 'queue-generation-exhausted'
        for scope,a in items.items():
            key=self.address('authority',scope)
            previous=old.get(scope)
            raw=self.encode('D31-authority',a)
            if previous is None or raw != self.encode('D31-authority',previous):
                expected=1 if previous is None else previous[8]+1
                assert expected <= U64_MAX and a[8] == expected and a[9] == (bytes(32) if previous is None else sha256(self.encode('D31-authority',previous)).digest())
                staged.write(key,raw,a[2],a[8])
            if a[10] == 'admitted':
                wait=(wait_override or {}).get(scope,self._wait(scope,a))
                assert sha256(wait).digest() == a[21]
                wkey=self.address('wait',scope)
                if wkey not in staged.records: staged.write(wkey,wait,a[2],1)
                elif staged.records[wkey] != wait:
                    previous_wait,previous_receipt,wgen=staged.read(wkey,a[2])
                    assert wgen < U64_MAX, 'queue-wait-generation-exhausted'
                    staged.write(wkey,wait,a[2],wgen+1)
            elif a[10] == 'cleanup' and self.address('wait',scope) in staged.records:
                deletion=staged.delete(self.address('wait',scope),a[2])
                assert sha256(deletion).digest() == a[20]
        for scope in set(old)-set(items):
            a=old[scope]
            # No refund/interest release precedes authenticated wait deletion readback.
            assert a[10] == 'cleanup' and self.address('wait',scope) not in staged.records
            deletion,df=staged.receipt(self.address('wait',scope))
            assert df[6]=='deleted' and sha256(deletion).digest()==a[20]
            staged.forget(self.address('wait',scope)); staged.forget(self.address('authority',scope))
        issued=self.last_ticket if last_ticket is None else last_ticket
        for target in targets:
            qkey,okey,pkey=(self.address(k,target) for k in ('queue','owners','predecessor'))
            prior_q,qr,qgen=staged.read(qkey,bytes(32)); prior_o,oreceipt,ogen=staged.read(okey,bytes(32))
            assert qgen==ogen
            generation=qgen+1
            owners=self._owner_rows(items,target); rows=self._rows(items,target,staged)
            assert len(owners)<=self.ceiling and all(r[0] <= issued for r in owners)
            ownerbytes=b''.join(N(ticket)+subject+digest for ticket,subject,digest in owners)
            next_o=self.encode('D31-owners',('deployment-a',target,generation,len(owners),ownerbytes,sha256(prior_o).digest(),codec['t'],self.ceiling))
            next_q=R('HX-EV-PIN-CAPACITY-QUEUE-4',11,U('deployment-a'),U(target),N(generation),N(len(rows)),B(b''.join(encode_queue_row(r) for r in rows)),
                N(sum(r[3]=='parked' for r in rows)),N(len(owners)-len(rows)),N(self.ceiling),N(issued),sha256(prior_q).digest(),Q(codec['t']))
            old_p=staged.records.get(pkey)
            if old_p is not None:
                source,sr,sg=staged.read(pkey,bytes(32)); pf=self.decode('D31-predecessor',source)
                assert sg==pf[2]==qgen and pf[6:8]==(sha256(prior_q).digest(),sha256(prior_o).digest()), 'queue-predecessor-replacement-authority'
            else: assert qgen==1, 'queue-missing-predecessor-source'
            predecessor=self.encode('D31-predecessor',('deployment-a',target,generation,prior_q,prior_o,next_o,sha256(next_q).digest(),sha256(next_o).digest(),
                bytes(32) if old_p is None else sha256(old_p).digest(),codec['t'],issued))
            # Read back all sources first; replacing the one predecessor is the same transaction as successor installation.
            staged.write(pkey,predecessor,bytes(32),generation)
            staged.write(okey,next_o,bytes(32),generation); staged.write(qkey,next_q,bytes(32),generation)
            if target!='deployment' and not owners:
                for kind in ('queue','owners','predecessor'): staged.forget(self.address(kind,target))
        self._ledger(staged,items,old)
        trial=Queues(self.ceiling,staged); trial._check()
        self.backend.records,self.backend.native=staged.records,staged.native
    def _wait(self,scope,a):
        c=self.decode('D31-carrier',a[6]); key=self.address('wait',scope)
        if key in self.backend.records:
            raw=self.backend.read(key,a[2])[0]
            fields=list(decode_record(raw,'HX-EV-PIN-CAPACITY-WAIT-2',codec['schemas']['D31-wait']))
            assert fields[0:2]==[a[1],a[3]] and fields[5]==a[5] and fields[6]==a[16] and fields[10]==c[4]
            fields[4]='tenant' if a[11]=='tenant' else 'deployment'
            return R('HX-EV-PIN-CAPACITY-WAIT-2',12,*(codec['encode_typed'](k,v) for k,v in zip(codec['schemas']['D31-wait'],fields)))
        return R('HX-EV-PIN-CAPACITY-WAIT-2',12,U(a[1]),a[3],c[5],N(c[6]),U('tenant' if a[11]=='tenant' else 'deployment'),N(a[5]),Q(a[16]),N(0),U(c[2]),codec['H']('capability'),c[4],Q(a[16]))
    def rerender_proof(self,scope,candidate,amount):
        a=self._check()[scope]; c=self.decode('D31-carrier',a[6])
        capability=self._capability()
        state='queued' if 0<amount<=min(capability[3],capability[4]-capability[5]) else 'parked'
        caphash=sha256(self.backend.read(self.capability_key(),bytes(32))[0]).digest()
        return sha256(b'fixture-authenticated-immutable-rerender:'+a[3]+c[4]+candidate+N(amount)+caphash+U(state)).digest()
    def rerender(self,scope,candidate,amount,expected_authority,proof):
        try:
            items=self._check(); a=items[scope]; c=self.decode('D31-carrier',a[6])
            assert a[10]=='admitted' and len(candidate)==32 and 0<amount<=U64_MAX
            assert proof==self.rerender_proof(scope,candidate,amount)
            current=self.encode('D31-authority',a)
            assert expected_authority==sha256(current).digest(), 'queue-rerender-current-authority'
            wait=self.backend.read(self.address('wait',scope),a[2])[0]
            fields=list(decode_record(wait,'HX-EV-PIN-CAPACITY-WAIT-2',codec['schemas']['D31-wait']))
            capability=self._capability()
            state='queued' if amount<=min(capability[3],capability[4]-capability[5]) else 'parked'
            caphash=sha256(self.backend.read(self.capability_key(),bytes(32))[0]).digest()
            if fields[2:4]==[candidate,amount] and fields[8:10]==[state,caphash]: return True
            fields[2:4]=[candidate,amount]
            fields[8:10]=[state,caphash]
            successor=R('HX-EV-PIN-CAPACITY-WAIT-2',12,*(codec['encode_typed'](k,v) for k,v in zip(codec['schemas']['D31-wait'],fields)))
            next_a=self._advance(a); next_a[21]=sha256(successor).digest(); items[scope]=next_a
            self._save(items,wait_override={scope:successor})
            return True
        except (AssertionError,KeyError,UnicodeDecodeError): return False
    def _advance(self,a,phase=None,residence=None):
        result=list(a)
        assert a[8] < U64_MAX, 'queue-authority-generation-exhausted'
        result[8]=a[8]+1; result[9]=sha256(self.encode('D31-authority',a)).digest()
        if phase is not None: result[10]=phase
        if residence is not None: result[11]=residence
        return result
    def allocate_ticket(self,scope,tenant,state='queued',charge_available=True):
        try:
            items=self._check()
            if scope in items:
                c=self.decode('D31-carrier',items[scope][6])
                return items[scope][5] if (c[0],c[2])==(tenant,state) and items[scope][10]!='cleanup' else None
            if self.last_ticket==U64_MAX or state not in {'queued','parked'} or not charge_available: return None
            if any(sum(t=='deployment' or t=='tenant:'+a[1] for a in items.values())>=self.ceiling for t in ('deployment','tenant:'+tenant)): return None
            ticket=self.last_ticket+1
            carrier=self.encode('D31-carrier',(tenant,scope,state,sha256(U(scope)).digest(),codec['H']('plan:'+scope),codec['H']('batch:'+scope),11*MiB))
            qr=self.backend.read(self.address('queue','deployment'),bytes(32))[1]
            tr=self.backend.read(self.address('queue','tenant:'+tenant),bytes(32))[1] if self.address('queue','tenant:'+tenant) in self.backend.records else b''
            subject=self.subject(scope); allocation=sha256(b'queue-allocation:'+subject+N(ticket)+sha256(carrier).digest()+B(qr)+B(tr)).digest()
            items[scope]=['deployment-a',tenant,subject,sha256(U(scope)).digest(),self.owner(tenant,scope),ticket,carrier,sha256(carrier).digest(),1,bytes(32),
                'allocated','none',allocation,None,40*1024,self.ceiling,codec['t'],codec['t'],qr,tr,None,bytes(32),bytes(32)]
            self._save(items,ticket)
            return ticket
        except (AssertionError,KeyError,UnicodeDecodeError): return None
    def add(self,ticket,tenant,scope,state='queued'):
        try:
            items=self._check()
            if scope not in items:
                if type(ticket) is not int or ticket!=self.last_ticket+1 or self.allocate_ticket(scope,tenant,state)!=ticket: return False
                items=self._check()
            a=items[scope]; c=self.decode('D31-carrier',a[6])
            if (type(ticket) is not int or ticket!=a[5] or (tenant,state)!=(c[0],c[2]) or a[10]=='cleanup'):
                # An untrusted conflicting caller cannot cancel the authenticated owner.
                return False
            if a[10]=='admitted': return True
            next_a=self._advance(a,'admitted','deployment')
            next_a[21]=sha256(self._wait(scope,next_a)).digest(); next_a[22]=next_a[21]
            next_a[13]=sha256(b'queue-admission:'+a[12]+next_a[21]).digest()
            items[scope]=next_a; self._save(items)
            return True
        except (AssertionError,KeyError,UnicodeDecodeError): return False
    def preparation_authority(self,scope):
        try:
            a=self._check().get(scope)
            return None if a is None else sha256(b'queue-owner-rollback:'+a[2]+U(a[4])+N(a[5])+a[12]).digest()
        except (AssertionError,KeyError): return None
    def rollback(self,scope,tenant,ticket,expected_authority,stop=None):
        try:
            items=self._check(); a=items.get(scope)
            if (a is None or a[1]!=tenant or a[5]!=ticket or expected_authority!=self.preparation_authority(scope)): return False
            if a[10]=='allocated':
                # Install a declared wait deletion receipt even when the wait never materialized.
                staged=deepcopy(self.backend); key=self.address('wait',scope)
                staged.native[staged.receipt_key(key)]=staged.native_record(key,a[2],1,bytes(32),bytes(32),'deleted')
                deletion=staged.native[staged.receipt_key(key)]
            elif a[10]=='admitted':
                staged=deepcopy(self.backend); deletion=staged.delete(self.address('wait',scope),a[2])
            else: staged=None; deletion=None
            if a[10]!='cleanup':
                next_a=self._advance(a,'cleanup','none'); next_a[20]=sha256(deletion).digest()
                if next_a[13] is None: next_a[13]=sha256(b'queue-admission:'+a[12]+a[22]).digest()
                # No cross-backend boundary: deletion and owner progress share the serializable ledger.
                original=self.backend; self.backend=staged
                try: old_items=deepcopy(items); items[scope]=next_a; self._save(items,old_override=old_items)
                except (AssertionError,KeyError): self.backend=original; raise
                original.records,original.native=self.backend.records,self.backend.native; self.backend=original
                if stop=='wait-deleted': return 'cleanup-hold'
                items=self._check(); a=items[scope]
            if stop=='refund-unavailable': return 'cleanup-hold'
            del items[scope]; self._save(items)
            return True
        except (AssertionError,KeyError,UnicodeDecodeError): return False
    def erasure_authority(self,tenant):
        items=self._check()
        return sha256(b'fixture-authenticated-tenant-erasure:'+U(tenant)+b''.join(a[2]+N(a[5])+a[12]
            for scope,a in sorted(items.items()) if a[1]==tenant)).digest()
    def erase_tenant(self,tenant,authority):
        try:
            assert authority==self.erasure_authority(tenant)
            staged=deepcopy(self.backend); trial=Queues(self.ceiling,staged)
            for scope,a in list(trial._check().items()):
                if a[1]==tenant: assert trial.rollback(scope,tenant,a[5],trial.preparation_authority(scope)) is True
            key=self.counter_key('tenant',tenant)
            if key in staged.records:
                f=decode_record(staged.read(key,bytes(32))[0],'HX-EV-PUBLICATION-COUNTER-1',codec['schemas']['D29-counter'])
                assert f[3:5]==(0,0); staged.forget(key)
            trial._check()
            self.backend.records,self.backend.native=staged.records,staged.native
            return True
        except (AssertionError,KeyError,UnicodeDecodeError): return False
    def erase_empty_deployment_directory(self,authority):
        try:
            assert not self._check()
            assert authority==sha256(b'fixture-authenticated-deployment-directory-erasure:'+self.backend.read(self.address('queue','deployment'),bytes(32))[1]).digest()
            staged=deepcopy(self.backend)
            for kind in ('predecessor','owners','queue'):
                key=self.address(kind,'deployment')
                if key in staged.records:
                    deletion=staged.delete(key,bytes(32)); assert staged.receipt(key)[0]==deletion
                    staged.forget(key)
            self._ledger(staged,{}, {},release_deployment=True)
            self.backend.records,self.backend.native=staged.records,staged.native
            return True
        except (AssertionError,KeyError,UnicodeDecodeError): return False
    def deployment_turn(self,deployment_fits,tenant_fits):
        try:
            items=self._check(); eligible=[r for r in self._rows(items,'deployment') if r[3]=='queued']
            if not eligible: return 'noop'
            head=eligible[0]
            if not deployment_fits(head): return 'deployment'
            if not tenant_fits(head):
                a=items[head[2]]; items[head[2]]=self._advance(a,residence='tenant')
                items[head[2]][21]=sha256(self._wait(head[2],items[head[2]])).digest(); self._save(items)
                return 'tenant:'+head[1]
            return 'reserve'
        except (AssertionError,KeyError): return 'pin_capacity_queue_corruption_hold'
    def tenant_fit_receipt(self,tenant,fits):
        assert type(fits) is bool
        items=self._check(); eligible=[r for r in self._rows(items,'tenant:'+tenant) if r[3]=='queued']
        if not eligible: return None
        a=items[eligible[0][2]]
        source=self.backend.read(self.counter_key('tenant',tenant),bytes(32))[0]
        cap=self.backend.read(self.capability_key(),bytes(32))[0]
        used=decode_record(source,'HX-EV-PUBLICATION-COUNTER-1',codec['schemas']['D29-counter'])[3]
        ceiling=decode_record(cap,'HX-EV-PUBLICATION-RETENTION-CAPABILITY-2',codec['schemas']['D29-capability'])[3]
        wait=decode_record(self.backend.read(self.address('wait',eligible[0][2]),a[2])[0],
            'HX-EV-PIN-CAPACITY-WAIT-2',codec['schemas']['D31-wait'])
        assert fits == (used+wait[3]<=ceiling), 'queue-tenant-fit-current-usage'
        return (fits,sha256(b'fixture-authenticated-tenant-fit:'+U(tenant)+source+cap+a[21]+N(a[5])+bytes([fits])).digest())
    def tenant_turn(self,tenant,fit_receipt=None):
        try:
            items=self._check(); eligible=[r for r in self._rows(items,'tenant:'+tenant) if r[3]=='queued']
            if not eligible: return 'noop'
            assert fit_receipt is not None and fit_receipt==self.tenant_fit_receipt(tenant,fit_receipt[0]), 'queue-tenant-fit-authority'
            if not fit_receipt[0]: return 'tenant:'+tenant
            a=items[eligible[0][2]]; items[eligible[0][2]]=self._advance(a,residence='deployment')
            items[eligible[0][2]][21]=sha256(self._wait(eligible[0][2],items[eligible[0][2]])).digest(); self._save(items)
            return 'deployment'
        except (AssertionError,KeyError): return 'pin_capacity_queue_corruption_hold'
    def repair(self,target='deployment'):
        before=deepcopy(vars(self.backend))
        try:
            pkey=self.address('predecessor',target)
            raw,receipt,generation=self.backend.read(pkey,bytes(32))
            p=self.decode('D31-predecessor',raw)
            assert p[0:2]==('deployment-a',target) and p[2]==generation
            qkey,okey=self.address('queue',target),self.address('owners',target)
            qr,qf=self.backend.receipt(qkey); ore,of=self.backend.receipt(okey)
            assert qf[2]==of[2]==p[2] and qf[3]==p[6] and of[3]==p[7], 'queue-repair-current-version'
            assert sha256(p[5]).digest()==p[7]
            # Sources enumerated from an authenticated, addressed target-owner manifest in the fixed predecessor.
            o=self.decode('D31-owners',p[5]); index=codec['decode_rows'](o[4],o[3],['N','B32','B32'])
            items={}
            for ticket,subject,digest in index:
                ak=codec['K']('HX-EV-PIN-WAIT-AUTHORITY-KEY-1',U('deployment-a'),subject)
                ar,rr,ag=self.backend.read(ak,subject); a=list(self.decode('D31-authority',ar)); c=self.decode('D31-carrier',a[6])
                assert sha256(ar).digest()==digest and a[5]==ticket and a[8]==ag
                if a[10]=='admitted':
                    wr=self.backend.read(self.address('wait',c[1]),subject)[0]
                    assert sha256(wr).digest()==a[21] and wr==self._wait(c[1],a)
                elif a[10]=='cleanup':
                    deletion,df=self.backend.receipt(self.address('wait',c[1]))
                    assert df[6]=='deleted' and sha256(deletion).digest()==a[20]
                items[c[1]]=a
            prior_q=decode_record(p[3],'HX-EV-PIN-CAPACITY-QUEUE-4',codec['schemas']['D31-queue'])
            rows=self._rows(items,target)
            next_q=R('HX-EV-PIN-CAPACITY-QUEUE-4',11,U('deployment-a'),U(target),N(p[2]),N(len(rows)),B(b''.join(encode_queue_row(r) for r in rows)),
                N(sum(r[3]=='parked' for r in rows)),N(len(index)-len(rows)),N(self.ceiling),N(p[10]),sha256(p[3]).digest(),Q(p[9]))
            assert sha256(next_q).digest()==p[6], 'queue-repair-exact-successor'
            staged=deepcopy(self.backend); staged.records[qkey]=next_q; staged.records[okey]=p[5]
            trial=Queues(self.ceiling,staged); trial._check()
            self.backend.records=staged.records
            return 'repaired'
        except (AssertionError,KeyError,UnicodeDecodeError):
            assert vars(self.backend)==before
            return 'pin_capacity_queue_corruption_hold'

def restart_queues(queue):
    # Intentionally rebuild no where/tickets/pending/receipt/reservation dictionaries.
    restarted=Queues(queue.ceiling,queue.backend)
    restarted._check()
    return restarted

def decode_queue(rows, entry_count, parked_count, reserved_count=0, ceiling=50000,
                 last_ticket=U64_MAX, trailing=b''):
    if (trailing or len(rows) != entry_count or not 1 <= ceiling <= 50000
            or reserved_count < 0 or entry_count + reserved_count > ceiling):
        return 'pin_capacity_queue_corruption_hold'
    if parked_count != sum(row[3] == 'parked' for row in rows):
        return 'pin_capacity_queue_corruption_hold'
    if len({row[0] for row in rows}) != len(rows) or len({row[2] for row in rows}) != len(rows):
        return 'pin_capacity_queue_corruption_hold'
    if any(not 0 < row[0] <= last_ticket or row[3] not in {'queued','parked'} for row in rows):
        return 'pin_capacity_queue_corruption_hold'
    if rows != sorted(rows, key=Queues.sort_key):
        return 'pin_capacity_queue_corruption_hold'
    return 'valid'

def encode_queue_row(row):
    ticket, tenant, scope, state = row
    tenant_bytes = tenant.encode()
    state_bytes = state.encode()
    return (ticket.to_bytes(8, 'big')
            + len(tenant_bytes).to_bytes(4, 'big') + tenant_bytes
            + sha256(U(scope)).digest()
            + len(state_bytes).to_bytes(4, 'big') + state_bytes)

def encode_queue_record(rows, reserved_count, ceiling, last_ticket):
    return R('HX-EV-PIN-CAPACITY-QUEUE-4',11,U('deployment-a'),U('deployment'),N(1),
        N(len(rows)),B(b''.join(encode_queue_row(row) for row in rows)),
        N(sum(row[3] == 'parked' for row in rows)),N(reserved_count),N(ceiling),N(last_ticket),bytes(32),Q(codec['t']))

def decode_queue_bytes(raw, authenticated_ceiling=3):
    offset = 0
    rows = []
    try:
        fields = decode_record(raw,'HX-EV-PIN-CAPACITY-QUEUE-4',codec['schemas']['D31-queue'])
        entry_count,parked_count,reserved_count,ceiling,last_ticket = (fields[i] for i in (3,5,6,7,8))
        if ceiling != authenticated_ceiling: raise ValueError('capability mismatch')
        raw = fields[4]
        def take(length):
            nonlocal offset
            if length < 0 or offset + length > len(raw):
                raise ValueError('truncated')
            value = raw[offset:offset+length]
            offset += length
            return value
        def take_u():
            length = int.from_bytes(take(4), 'big')
            if not 1 <= length <= 1024: raise ValueError('identifier bounds')
            return take(length).decode('utf-8', errors='strict')
        if entry_count > 50000 or entry_count + reserved_count > ceiling or ceiling > 50000:
            raise ValueError('ceiling')
        for _ in range(entry_count):
            ticket = int.from_bytes(take(8), 'big')
            tenant = take_u()
            scope_hash = take(32)
            state = take_u()
            if state not in {'queued', 'parked'}:
                raise ValueError('state')
            rows.append([ticket, tenant, scope_hash, state])
        if offset != len(raw):
            raise ValueError('trailing')
        return decode_queue(rows, entry_count, parked_count, reserved_count, ceiling, last_ticket)
    except (AssertionError,UnicodeDecodeError, ValueError):
        return 'pin_capacity_queue_corruption_hold'

queues = Queues(3)
assert queues.add(1, 't1', 'a')
assert queues.add(2, 't2', 'b')
assert 'a' in queues.reservations['tenant:t1'] and 'a' not in queues.reservations['deployment']
assert queues.deployment_turn(lambda _: True, lambda row: row[1] != 't1') == 'tenant:t1'
assert queues.where == {'a':'tenant:t1', 'b':'deployment'}
assert 'a' in queues.reservations['deployment'] and 'a' not in queues.reservations['tenant:t1']
assert queues.deployment_turn(lambda _: True, lambda _: True) == 'reserve'
assert queues.tenant_turn('t1',queues.tenant_fit_receipt('t1',True)) == 'deployment'
assert 'a' in queues.reservations['tenant:t1'] and 'a' not in queues.reservations['deployment']
assert [row[2] for row in queues.rows['deployment']] == ['a', 'b']
assert queues.add(3, 't3', 'c', 'parked')
assert not queues.add(4, 't4', 'd')
assert not queues.add(4, 't3', 'c')  # duplicate stable subject.
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
assert full_destination.tenant_turn('t1',full_destination.tenant_fit_receipt('t1',True)) == 'deployment'
assert full_destination.where['x'] == 'deployment' and 'x' in full_destination.reservations['tenant:t1']
valid_rows = [[1, 't', 's', 'queued']]
assert decode_queue(valid_rows, 1, 0) == 'valid'
assert decode_queue(valid_rows, 0, 0) == 'pin_capacity_queue_corruption_hold'
assert decode_queue(valid_rows, 2, 0) == 'pin_capacity_queue_corruption_hold'
assert decode_queue(valid_rows, 1, 1) == 'pin_capacity_queue_corruption_hold'
assert decode_queue(valid_rows, 1, 0, trailing=b'x') == 'pin_capacity_queue_corruption_hold'
assert decode_queue(valid_rows, 1, 0, reserved_count=3, ceiling=3) == 'pin_capacity_queue_corruption_hold'
valid_raw = encode_queue_record(valid_rows, 1, 3, 1)
assert decode_queue_bytes(valid_raw) == 'valid'
assert decode_queue_bytes(valid_raw,authenticated_ceiling=4) == 'pin_capacity_queue_corruption_hold'
assert decode_queue_bytes(valid_raw[:-1]) == 'pin_capacity_queue_corruption_hold'
assert decode_queue_bytes(valid_raw+b'x') == 'pin_capacity_queue_corruption_hold'
overflow_raw = encode_queue_record(valid_rows, 3, 3, 1)
assert decode_queue_bytes(overflow_raw) == 'pin_capacity_queue_corruption_hold'
valid_fields = list(decode_record(valid_raw,'HX-EV-PIN-CAPACITY-QUEUE-4',codec['schemas']['D31-queue']))
bad_row = valid_fields[4][:8] + b'\xff\xff\xff\xff' + valid_fields[4][12:]
malformed_fields = list(valid_fields); malformed_fields[4] = bad_row
malformed = R('HX-EV-PIN-CAPACITY-QUEUE-4',11,*(codec['encode_typed'](kind,value)
              for kind,value in zip(codec['schemas']['D31-queue'],malformed_fields)))
assert decode_queue_bytes(malformed) == 'pin_capacity_queue_corruption_hold'
for field_index,bad in [(3,0),(3,2),(5,1),(4,valid_fields[4]+valid_fields[4]),(4,valid_fields[4][:-1])]:
    corrupted = list(valid_fields); corrupted[field_index] = bad
    malformed = R('HX-EV-PIN-CAPACITY-QUEUE-4',11,*(codec['encode_typed'](kind,value)
        for kind,value in zip(codec['schemas']['D31-queue'],corrupted)))
    assert decode_queue_bytes(malformed) == 'pin_capacity_queue_corruption_hold'
assert decode_queue_bytes(codec['vectors']['D31-queue'],authenticated_ceiling=50000) == 'valid'
ticket_exhausted = Queues()
ticket_exhausted.last_ticket = U64_MAX
assert not ticket_exhausted.add(U64_MAX, 't', 'never-wraps')
allocator = Queues()
ticket = allocator.allocate_ticket('stable-subject', 't')
assert ticket == 1 and allocator.allocate_ticket('stable-subject', 't') == ticket
assert allocator.add(ticket, 't', 'stable-subject')
admitted_snapshot = deepcopy(vars(allocator))
assert allocator.add(ticket, 't', 'stable-subject')
assert vars(allocator) == admitted_snapshot  # successful admission lost-ack retry
assert allocator.allocate_ticket('stable-subject', 'other') is None
assert vars(allocator) == admitted_snapshot
assert len(allocator.rows['deployment']) == 1 and not allocator.pending
assert 'stable-subject' in allocator.reservations['tenant:t']
refused = Queues(1)
assert refused.add(1, 't', 'first')
refused_snapshot = deepcopy(vars(refused))
assert refused.allocate_ticket('full', 't') is None
assert vars(refused) == refused_snapshot
assert refused.allocate_ticket('uncharged', 'other', charge_available=False) is None
assert vars(refused) == refused_snapshot
assert refused.allocate_ticket('invalid', 'other', 'invalid') is None
assert vars(refused) == refused_snapshot
pending = Queues()
pending_ticket = pending.allocate_ticket('owned-preparation','tenant-a')
pending_authority = pending.preparation_authority('owned-preparation')
pending_snapshot = deepcopy(vars(pending))
for tenant,ticket,state in [('tenant-b',pending_ticket,'queued'),('tenant-a',pending_ticket+1,'queued'),
                            ('tenant-a',pending_ticket,'parked')]:
    assert not pending.add(ticket,tenant,'owned-preparation',state)
    assert vars(pending) == pending_snapshot
assert not pending.rollback('owned-preparation','tenant-b',pending_ticket,pending_authority)
assert not pending.rollback('owned-preparation','tenant-a',pending_ticket,bytes(32))
assert vars(pending) == pending_snapshot
assert pending.rollback('owned-preparation','tenant-a',pending_ticket,pending_authority)
assert not pending.pending and not pending.charges and not pending.tickets
assert all(not values for values in pending.reservations.values())
cleanup_snapshot = deepcopy(vars(pending))
assert not pending.rollback('owned-preparation','tenant-a',pending_ticket,pending_authority)
assert vars(pending) == cleanup_snapshot and pending.last_ticket == pending_ticket
allocator.last_ticket = U64_MAX
exhausted_snapshot = deepcopy(vars(allocator))
assert allocator.allocate_ticket('new-subject', 't') is None
assert vars(allocator) == exhausted_snapshot

def slice2_inventory_fixture():
    store=QueueBytes(); kind,scope='tenant','slice2-tenant'
    actor=kind+':'+sha256(U(kind)+U(scope)).hexdigest(); shard=sha256(U(kind)+U(scope)).digest()[0]
    ikey=codec['K']('HX-EV-HOLD-INDEX-KEY-1',U(kind),U(scope))
    dkey=codec['K']('HX-EV-HOLD-DIRECTORY-KEY-1',U('deployment-a'),N(shard))
    index=R('HX-EV-HOLD-INDEX-2',8,U(kind),U(scope),N(1),N(0),B(b''),N(0),bytes(32),Q(codec['t']))
    directory=R('HX-EV-HOLD-DIRECTORY-1',7,N(shard),N(1),N(1),B(U(actor)),bytes(32),Q(codec['t']),N(0))
    store.write(ikey,index,bytes(32),1); store.write(dkey,directory,bytes(32),1)
    # This typed native reservation is the pre-existing onboarding/admission grant;
    # it proves one charged 8 KiB operational-evidence slot without a new codec.
    rkey=codec['K']('HX-EV-PUBLICATION-CHARGE-KEY-1',U('deployment-a'),U(kind),U(scope),codec['H']('slice2-inventory-reservation'))
    f=list(decode_record(codec['vectors']['D29-charge'],'HX-EV-PUBLICATION-CHARGE-2',codec['schemas']['D29-charge']))
    f[1:5]=['tenant',scope,codec['H']('slice2-inventory-reservation'),'side-record']; f[5:8]=[8192,0,8192]
    grant=R('HX-EV-PUBLICATION-CHARGE-2',15,*(codec['encode_typed'](k,v) for k,v in zip(codec['schemas']['D29-charge'],f)))
    store.write(rkey,grant,codec['H']('slice2-inventory-reservation'),1)
    return store,(ikey,dkey,rkey)

def slice2_scope_gate(store,keys,full=True):
    before=deepcopy(vars(store)); ikey,dkey,rkey=keys
    try:
        index=store.read(ikey,bytes(32))[0]; directory=store.read(dkey,bytes(32))[0]
        i=decode_record(index,'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index'])
        d=decode_record(directory,'HX-EV-HOLD-DIRECTORY-1',codec['schemas']['D37-directory'])
        grant=store.read(rkey,codec['H']('slice2-inventory-reservation'))[0]
        charge=decode_record(grant,'HX-EV-PUBLICATION-CHARGE-2',codec['schemas']['D29-charge'])
        assert i[0:2]==('tenant','slice2-tenant') and i[5]==0 and i[3]==0
        assert d[0]==sha256(U(i[0])+U(i[1])).digest()[0] and d[2]==1 and d[3]==U('tenant:'+sha256(U(i[0])+U(i[1])).hexdigest())
        assert charge[4]=='side-record' and charge[5:8]==(8192,0,8192) and charge[10]=='active', 'slice2-inventory-reservation'
        if not full: return 'scope-admitted'
        entry=R('HX-EV-HOLD-ENTRY-2',13,U(i[0]),U(i[1]),U('ScopeRetentionCapacityHold'),U('shard:7'),O(U('d')),
            U('scope_retention_capacity_hold'),N(1),bytes(32),Q(codec['t']),Q(codec['t']),N(1),U('gateway'),Q(codec['t']+36000000000))
        decode_record(entry,'HX-EV-HOLD-ENTRY-2',codec['schemas']['D37-entry'])
        row=Q(codec['t'])+U('ScopeRetentionCapacityHold')+U('shard:7')+sha256(entry).digest()
        successor=R('HX-EV-HOLD-INDEX-2',8,U(i[0]),U(i[1]),N(2),N(1),B(row),N(0),sha256(index).digest(),Q(codec['t']))
        decode_record(successor,'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index'])
        ekey=codec['K']('HX-EV-HOLD-ENTRY-KEY-1',U(i[0]),U(i[1]),U('ScopeRetentionCapacityHold'),U('shard:7'))
        staged=deepcopy(store); staged.write(ekey,entry,codec['H']('slice2-inventory-reservation'),1)
        staged.write(ikey,successor,bytes(32),2)
        store.records,store.native=staged.records,staged.native
        return 'scope_retention_capacity_hold'
    except (AssertionError,KeyError,UnicodeDecodeError):
        assert vars(store)==before
        return 'slice2-readiness-hold'

def verify_loop7_slice2():
    store,keys=slice2_inventory_fixture()
    grant=store.read(keys[2],codec['H']('slice2-inventory-reservation'))[0]
    refusals=0
    for key in keys:
        for damage in ('missing','unavailable','changed'):
            bad=deepcopy(store)
            if damage=='missing': del bad.records[key]
            elif damage=='unavailable': bad.unavailable.add(key)
            else: bad.records[key]+=b'changed'
            before=deepcopy(vars(bad)); assert slice2_scope_gate(bad,keys)=='slice2-readiness-hold'
            assert vars(bad)==before; refusals+=1
    for kind,amount in (('pin-batch',8192),('side-record',8193)):
        bad=deepcopy(store); fields=list(decode_record(grant,'HX-EV-PUBLICATION-CHARGE-2',codec['schemas']['D29-charge']))
        fields[4],fields[5],fields[7]=kind,amount,amount
        raw=R('HX-EV-PUBLICATION-CHARGE-2',15,*(codec['encode_typed'](k,v) for k,v in zip(codec['schemas']['D29-charge'],fields)))
        bad.write(keys[2],raw,codec['H']('slice2-inventory-reservation'),1)
        before=deepcopy(vars(bad)); assert slice2_scope_gate(bad,keys)=='slice2-readiness-hold' and vars(bad)==before
        refusals+=1
    before=deepcopy(vars(store)); assert slice2_scope_gate(store,keys,False)=='scope-admitted' and vars(store)==before
    assert slice2_scope_gate(store,keys)=='scope_retention_capacity_hold'
    # Restart from encoded rows: listed actor/index/entry show gateway ownership and hourly exit.
    restarted=QueueBytes(); restarted.records=deepcopy(store.records); restarted.native=deepcopy(store.native)
    i=decode_record(restarted.read(keys[0],bytes(32))[0],'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index'])
    rows=codec['decode_rows'](i[4],i[3],['Q','U','U','B32'])
    ekey=codec['K']('HX-EV-HOLD-ENTRY-KEY-1',U('tenant'),U('slice2-tenant'),U('ScopeRetentionCapacityHold'),U('shard:7'))
    entry=restarted.read(ekey,codec['H']('slice2-inventory-reservation'))[0]
    e=decode_record(entry,'HX-EV-HOLD-ENTRY-2',codec['schemas']['D37-entry'])
    assert rows==[(codec['t'],'ScopeRetentionCapacityHold','shard:7',sha256(entry).digest())]
    assert e[11]=='gateway' and e[12]-e[9]==36000000000
    return {'refusals':refusals,'indexed_holds':1,'without_binary_publication':1}

loop7_slice_metrics=verify_loop7_slice2() if not globals().get('fault_probe') or str(globals().get('fault_name','')).startswith('loop7 ') else {}
if loop7_slice_metrics: assert loop7_slice_metrics=={'refusals':11,'indexed_holds':1,'without_binary_publication':1}

def verify_loop7_queues():
    restarts=refusals=cleanup=repairs=bounds=0
    q=Queues(4)
    assert q.allocate_ticket('owner-a','tenant-a')==1
    assert q.allocate_ticket('owner-b','tenant-b','parked')==2
    assert q.address('authority','owner-a')!=q.address('authority','owner-b')
    assert set(vars(q))=={'ceiling','backend'}
    q=restart_queues(q); restarts+=1
    assert set(q.pending)=={'owner-a','owner-b'} and q.tickets=={'owner-a':1,'owner-b':2}
    assert all(q.charges[x]==40960 for x in q.tickets)
    assert q.reservations['deployment']=={'owner-a','owner-b'}
    assert q.add(1,'tenant-a','owner-a')
    admitted=deepcopy(vars(q.backend)); q=restart_queues(q); restarts+=1
    assert q.add(1,'tenant-a','owner-a') and vars(q.backend)==admitted
    original=q.decode('D31-authority',q.backend.read(q.address('authority','owner-a'),q.subject('owner-a'))[0])
    candidate=codec['H']('verified-current-rerender'); amount=13*MiB
    current_hash=sha256(q.encode('D31-authority',original)).digest()
    proof=q.rerender_proof('owner-a',candidate,amount)
    for changed_candidate,changed_amount,authority,source in ((candidate,amount,bytes(32),proof),
        (candidate,amount,current_hash,bytes(32)),(candidate,0,current_hash,proof)):
        before=deepcopy(vars(q.backend)); assert not q.rerender('owner-a',changed_candidate,changed_amount,authority,source)
        assert vars(q.backend)==before; refusals+=1
    assert q.rerender('owner-a',candidate,amount,current_hash,proof)
    rerendered=q._check()['owner-a']; assert rerendered[6:8]==list(original[6:8]) and rerendered[13]==original[13] and rerendered[22]==original[22]
    before=deepcopy(vars(q.backend)); assert q.rerender('owner-a',candidate,amount,sha256(q.encode('D31-authority',rerendered)).digest(),proof)
    assert vars(q.backend)==before
    for turn,counter in ((lambda:q.deployment_turn(lambda _:True,lambda _:False),'tenant'),
                         (lambda:q.tenant_turn('tenant-a',q.tenant_fit_receipt('tenant-a',True)),'deployment')):
        before_wait=q.backend.read(q.address('wait','owner-a'),q.subject('owner-a'))
        assert turn() in {'tenant:tenant-a','deployment'}
        q=restart_queues(q); restarts+=1
        wait,receipt,generation=q.backend.read(q.address('wait','owner-a'),q.subject('owner-a'))
        w=decode_record(wait,'HX-EV-PIN-CAPACITY-WAIT-2',codec['schemas']['D31-wait'])
        a=q.decode('D31-authority',q.backend.read(q.address('authority','owner-a'),q.subject('owner-a'))[0])
        assert w[2:4]==(candidate,amount) and w[4]==counter and w[5]==1 and a[11]==counter
        assert wait!=before_wait[0] and generation==before_wait[2]+1
        assert a[21]==sha256(wait).digest() and a[13]==original[13] and a[22]==original[22]
        assert q.backend.receipt(q.address('wait','owner-a'))[1][3]==a[21]
    for tenant,ticket,state in [('other',1,'queued'),('tenant-a',2,'queued'),('tenant-a',1,'parked')]:
        before=deepcopy(vars(q.backend)); assert not q.add(ticket,tenant,'owner-a',state)
        assert vars(q.backend)==before; refusals+=1
    grant=q.preparation_authority('owner-b')
    for tenant,ticket,auth in [('other',2,grant),('tenant-b',3,grant),('tenant-b',2,bytes(32))]:
        before=deepcopy(vars(q.backend)); assert not q.rollback('owner-b',tenant,ticket,auth)
        assert vars(q.backend)==before; refusals+=1
    # Allocation-only rollback proves authenticated absent wait, then refund last.
    for scope,tenant,ticket in [('owner-b','tenant-b',2),('owner-a','tenant-a',1)]:
        grant=q.preparation_authority(scope)
        assert q.rollback(scope,tenant,ticket,grant,stop='wait-deleted')=='cleanup-hold'
        q=restart_queues(q); restarts+=1; cleanup+=1
        assert scope in q.charges and scope in q.reservations['deployment'] and scope in q.reservations['tenant:'+tenant]
        a=q.decode('D31-authority',q.backend.read(q.address('authority',scope),q.subject(scope))[0])
        assert a[10]=='cleanup' and q.address('wait',scope) not in q.backend.records
        assert sha256(q.backend.receipt(q.address('wait',scope))[0]).digest()==a[20]
        before=deepcopy(vars(q.backend)); assert q.rollback(scope,tenant,ticket,grant,stop='refund-unavailable')=='cleanup-hold'
        assert vars(q.backend)==before; cleanup+=1
        assert q.rollback(scope,tenant,ticket,grant) is True
        before=deepcopy(vars(q.backend)); assert not q.rollback(scope,tenant,ticket,grant) and vars(q.backend)==before
    assert not q.tickets and q.last_ticket==2
    empty=deepcopy(q); grant=sha256(b'fixture-authenticated-deployment-directory-erasure:'+empty.backend.read(empty.address('queue','deployment'),bytes(32))[1]).digest()
    assert empty.erase_empty_deployment_directory(grant)
    assert empty.address('queue','deployment') not in empty.backend.records
    assert not any(key.startswith('publication-charge:') for key in empty.backend.records)
    for kind,account in (('unidentified','*'),('deployment','deployment')):
        f=decode_record(empty.backend.read(empty.counter_key(kind,account),bytes(32))[0],'HX-EV-PUBLICATION-COUNTER-1',codec['schemas']['D29-counter'])
        assert f[3:5]==(0,0)

    # A complete restart has only encoded provider records/readbacks; never serialize process dictionaries.
    def fixture():
        model=Queues(3); assert model.add(1,'tenant-a','repair-a'); assert model.add(2,'tenant-b','repair-b')
        assert model.deployment_turn(lambda _:True,lambda _:False)=='tenant:tenant-a'
        return model
    for target in ('deployment','tenant:tenant-a'):
        model=fixture(); key=model.address('queue',target); good=model.backend.records[key]
        model.backend.records[key]=good[:-1]+bytes([good[-1]^1])
        assert model.repair(target)=='repaired' and model.backend.records[key]==good
        model=restart_queues(model); restarts+=1; repairs+=1
        assert model.tickets=={'repair-a':1,'repair-b':2} and model.where['repair-a']=='tenant:tenant-a'
        assert model.backend.read(model.address('wait','repair-a'),model.subject('repair-a'))[0]==model._wait('repair-a',model._check()['repair-a'])
    for damage in ('source-missing','source-unavailable','source-changed','source-wrong-kind','source-stale',
                   'authority-missing','authority-unavailable','authority-owner','wait-changed','wait-unavailable',
                   'charge-missing','charge-unavailable','counter-changed','index-receipt'):
        model=fixture(); key=model.address('queue','deployment'); model.backend.records[key]+=b'corrupt'
        pkey=model.address('predecessor','deployment'); akey=model.address('authority','repair-a'); wkey=model.address('wait','repair-a')
        if damage=='source-missing': del model.backend.records[pkey]
        elif damage=='source-unavailable': model.backend.unavailable.add(pkey)
        elif damage=='source-changed': model.backend.records[pkey]+=b'changed'
        elif damage=='source-wrong-kind': model.backend.records[pkey]=codec['vectors']['D31-wait']
        elif damage=='source-stale':
            r,fields=model.backend.receipt(pkey); model.backend.native[model.backend.receipt_key(pkey)]=model.backend.native_record(pkey,bytes(32),fields[2]-1,fields[3],fields[4],'present')
        elif damage=='authority-missing': del model.backend.records[akey]
        elif damage=='authority-unavailable': model.backend.unavailable.add(akey)
        elif damage=='authority-owner':
            r,f=model.backend.receipt(akey); model.backend.native[model.backend.receipt_key(akey)]=model.backend.native_record(akey,bytes(32),f[2],f[3],f[4],'present')
        elif damage=='wait-changed': model.backend.records[wkey]+=b'changed'
        elif damage=='wait-unavailable': model.backend.unavailable.add(wkey)
        elif damage.startswith('charge-'):
            key=model.charge_key(model.subject('repair-a'),'tenant-a')
            if damage=='charge-missing': del model.backend.records[key]
            else: model.backend.unavailable.add(key)
        elif damage=='counter-changed': model.backend.records[model.counter_key('tenant-pool','*')]+=b'changed'
        else:
            key=model.address('owners','deployment'); r,f=model.backend.receipt(key)
            model.backend.native[model.backend.receipt_key(key)]=model.backend.native_record(key,bytes(32),f[2]+1,f[3],f[4],'present')
        before=deepcopy(vars(model.backend)); assert model.repair()=='pin_capacity_queue_corruption_hold',damage
        assert vars(model.backend)==before,damage; refusals+=1
    for damage in ('missing','unavailable','changed','receipt-generation'):
        model=fixture(); key=model.address('predecessor','deployment')
        if damage=='missing': del model.backend.records[key]
        elif damage=='unavailable': model.backend.unavailable.add(key)
        elif damage=='changed': model.backend.records[key]+=b'changed'
        else:
            r,f=model.backend.receipt(key)
            model.backend.native[model.backend.receipt_key(key)]=model.backend.native_record(key,bytes(32),f[2]-1,f[3],f[4],'present')
        before=deepcopy(vars(model.backend)); assert model.allocate_ticket('source-conflict','tenant-b') is None
        assert vars(model.backend)==before and model.last_ticket==2; refusals+=1
    # Authenticated installed versions at u64 maximum refuse checked successor writes atomically.
    def installed(model,key,label,index,generation_index,owner=bytes(32)):
        raw=model.backend.read(key,owner)[0]
        schema=codec['loop7_schemas'].get(label,codec['schemas'].get(label))
        domain=codec['loop7_domains'].get(label,raw.split(b'\0',1)[0].decode())
        f=list(decode_record(raw,domain,schema)); f[generation_index]=U64_MAX
        f[index]=codec['H']('authenticated-predecessor')
        raw=R(domain,len(schema),*(codec['encode_typed'](k,v) for k,v in zip(schema,f)))
        model.backend.write(key,raw,owner,U64_MAX)
        return raw
    for kind in ('queue','owners','authority','wait','tenant-counter','pool-counter','deployment-counter','unidentified-counter'):
        model=Queues(3); assert model.add(1,'tenant-a','bound-a')
        if kind in {'queue','owners'}:
            installed(model,model.address('queue','deployment'),'D31-queue',9,2)
            installed(model,model.address('owners','deployment'),'D31-owners',5,2)
        elif kind=='authority':
            raw=installed(model,model.address('authority','bound-a'),'D31-authority',9,8,model.subject('bound-a'))
            for target in ('deployment','tenant:tenant-a'):
                key=model.address('owners',target); old=model.backend.read(key,bytes(32)); f=list(model.decode('D31-owners',old[0]))
                f[4]=N(1)+model.subject('bound-a')+sha256(raw).digest()
                model.backend.write(key,model.encode('D31-owners',f),bytes(32),old[2])
                pkey=model.address('predecessor',target); pr,receipt,pg=model.backend.read(pkey,bytes(32)); pf=list(model.decode('D31-predecessor',pr))
                pf[5]=model.backend.records[key]; pf[7]=sha256(pf[5]).digest()
                model.backend.write(pkey,model.encode('D31-predecessor',pf),bytes(32),pg)
        elif kind=='wait':
            key=model.address('wait','bound-a'); raw,receipt,g=model.backend.read(key,model.subject('bound-a'))
            model.backend.write(key,raw,model.subject('bound-a'),U64_MAX)
        else:
            counter={'tenant-counter':('tenant','tenant-a'),'pool-counter':('tenant-pool','*'),
                'deployment-counter':('deployment','deployment'),'unidentified-counter':('unidentified','*')}[kind]
            installed(model,model.counter_key(*counter),'D29-counter',6,5)
        before=deepcopy(vars(model.backend))
        if kind in {'authority','wait'}: result=model.deployment_turn(lambda _:True,lambda _:False); assert result=='pin_capacity_queue_corruption_hold',kind
        elif kind=='unidentified-counter':
            assert model.rollback('bound-a','tenant-a',1,model.preparation_authority('bound-a'))
            before=deepcopy(vars(model.backend))
            grant=sha256(b'fixture-authenticated-deployment-directory-erasure:'+model.backend.read(model.address('queue','deployment'),bytes(32))[1]).digest()
            assert not model.erase_empty_deployment_directory(grant)
        else: assert model.allocate_ticket('bound-b','tenant-a') is None,kind
        assert vars(model.backend)==before,kind; bounds+=1
    # Repeated moves replace one actual predecessor/native slot; retained bytes and charges stay bounded.
    model=fixture(); initial_keys=set(model.backend.records); initial_native=set(model.backend.native)
    charge_bytes={key:raw for key,raw in model.backend.records.items() if key.startswith('publication-charge:')}
    for _ in range(131):
        assert model.tenant_turn('tenant-a',model.tenant_fit_receipt('tenant-a',True))=='deployment'
        assert model.deployment_turn(lambda _:True,lambda _:False)=='tenant:tenant-a'
        model=restart_queues(model); restarts+=1
        assert set(model.backend.records)==initial_keys and set(model.backend.native)==initial_native
        assert {key:raw for key,raw in model.backend.records.items() if key.startswith('publication-charge:')}==charge_bytes
        assert all(len(raw)<=1024 for raw in model.backend.native.values())
        assert all(len(raw)<=74*MiB for key,raw in model.backend.records.items() if key.startswith('pin-queue-predecessor:'))
    assert model.tickets=={'repair-a':1,'repair-b':2}
    # Actual encoded capability/counters admit directory and owner storage together.
    capacity_refusals=0
    crowded=Queues()
    for i in range(12): assert crowded.allocate_ticket('directory-'+str(i),'tenant-'+str(i))==i+1
    crowded=restart_queues(crowded)
    assert crowded._capability()[4]-crowded._capability()[5]==1792*MiB
    before=deepcopy(vars(crowded.backend))
    assert crowded.allocate_ticket('directory-13','tenant-13') is None
    assert vars(crowded.backend)==before and crowded.last_ticket==12
    assert crowded.address('queue','tenant:tenant-13') not in crowded.backend.records; capacity_refusals+=1
    def external_usage(model,tenant,amount,label,account_kind='tenant'):
        subject=codec['H'](label); key=model.charge_key(subject,tenant,account_kind)
        raw=R('HX-EV-PUBLICATION-CHARGE-2',15,U('deployment-a'),U(account_kind),U(tenant),subject,U('resume-window'),
            N(amount),N(0),N(amount),N(3),N(1),U('active'),bytes(32),O(None),Q(codec['t']),N(0))
        decode_record(raw,'HX-EV-PUBLICATION-CHARGE-2',codec['schemas']['D29-charge'])
        model.backend.write(key,raw,subject,1)
        counters=(('tenant',tenant),('tenant-pool','*'),('deployment','deployment')) if account_kind=='tenant' else (('unidentified',tenant),('unidentified','*'),('deployment','deployment'))
        for kind,account in counters:
            key=model.counter_key(kind,account)
            if key not in model.backend.records:
                zero=R('HX-EV-PUBLICATION-COUNTER-1',8,U('deployment-a'),U(kind),U(account),N(0),N(0),N(1),bytes(32),Q(codec['t']))
                model.backend.write(key,zero,bytes(32),1)
            old,receipt,g=model.backend.read(key,bytes(32))
            f=list(decode_record(old,'HX-EV-PUBLICATION-COUNTER-1',codec['schemas']['D29-counter']))
            f[3]+=amount; f[4]+=1; f[5]+=1; f[6]=sha256(old).digest()
            model.backend.write(key,R('HX-EV-PUBLICATION-COUNTER-1',8,*(codec['encode_typed'](k,v) for k,v in zip(codec['schemas']['D29-counter'],f))),bytes(32),f[5])
        return model.charge_key(subject,tenant,account_kind),raw
    limited=Queues(); assert limited.add(1,'tenant-a','base-owner')
    foreign,foreign_raw=external_usage(limited,'tenant-a',1024*MiB-(142*MiB+32768+MiB)-2*(40960+MiB),'unrelated-existing-usage')
    assert limited.allocate_ticket('last-fitting-owner','tenant-a')==2
    tenant_raw=limited.backend.read(limited.counter_key('tenant','tenant-a'),bytes(32))[0]
    assert decode_record(tenant_raw,'HX-EV-PUBLICATION-COUNTER-1',codec['schemas']['D29-counter'])[3]==1024*MiB
    before=deepcopy(vars(limited.backend)); assert limited.allocate_ticket('over-owner','tenant-a') is None
    assert vars(limited.backend)==before and limited.last_ticket==2 and limited.backend.records[foreign]==foreign_raw; capacity_refusals+=1
    grant=limited.preparation_authority('last-fitting-owner'); assert limited.rollback('last-fitting-owner','tenant-a',2,grant)
    assert limited.backend.records[foreign]==foreign_raw
    assert limited.allocate_ticket('replacement-owner','tenant-a')==3
    # Existing authenticated usage survives CAS; lowering a ceiling never deletes it.
    lowered=deepcopy(limited); cap=list(lowered._capability()); cap[4]=1280*MiB; cap[5]=256*MiB
    lowered.backend.write(lowered.capability_key(),R('HX-EV-PUBLICATION-RETENTION-CAPABILITY-2',15,
        *(codec['encode_typed'](k,v) for k,v in zip(codec['schemas']['D29-capability'],cap))),bytes(32),3)
    before=deepcopy(vars(lowered.backend)); assert lowered.allocate_ticket('deployment-over','tenant-a') is None
    assert vars(lowered.backend)==before and lowered.backend.records[foreign]==foreign_raw; capacity_refusals+=1
    deployment_limited=Queues(); assert deployment_limited.add(1,'tenant-a','deployment-base')
    cap=list(deployment_limited._capability()); cap[6]=cap[4]
    deployment_limited.backend.write(deployment_limited.capability_key(),R('HX-EV-PUBLICATION-RETENTION-CAPABILITY-2',15,
        *(codec['encode_typed'](k,v) for k,v in zip(codec['schemas']['D29-capability'],cap))),bytes(32),3)
    initial=decode_record(deployment_limited.backend.read(deployment_limited.counter_key('deployment','deployment'),bytes(32))[0],
        'HX-EV-PUBLICATION-COUNTER-1',codec['schemas']['D29-counter'])[3]
    capture1,raw1=external_usage(deployment_limited,'existing-capture',1024*MiB,'existing-capture-a','capture-scope')
    capture2,raw2=external_usage(deployment_limited,'existing-capture',2048*MiB-initial-1024*MiB-(40960+MiB),'existing-capture-b','capture-scope')
    assert deployment_limited.allocate_ticket('deployment-last-fitting','tenant-a')==2
    f=decode_record(deployment_limited.backend.read(deployment_limited.counter_key('deployment','deployment'),bytes(32))[0],
        'HX-EV-PUBLICATION-COUNTER-1',codec['schemas']['D29-counter'])
    assert f[3]==2048*MiB
    before=deepcopy(vars(deployment_limited.backend)); assert deployment_limited.allocate_ticket('deployment-over-only','tenant-a') is None
    assert vars(deployment_limited.backend)==before and deployment_limited.last_ticket==2
    assert deployment_limited.backend.records[capture1]==raw1 and deployment_limited.backend.records[capture2]==raw2; capacity_refusals+=1
    badcap=deepcopy(limited); badcap.backend.unavailable.add(badcap.capability_key())
    before=deepcopy(vars(badcap.backend)); assert badcap.allocate_ticket('missing-capability','tenant-a') is None
    assert vars(badcap.backend)==before; capacity_refusals+=1
    # Authenticated whole-tenant erasure removes exactly that owner/directory/charge/receipt set.
    unrelated={key:raw for key,raw in model.backend.records.items() if key in {model.address('authority','repair-b'),model.address('wait','repair-b'),model.charge_key(model.subject('repair-b'),'tenant-b')}}
    before=deepcopy(vars(model.backend)); assert not model.erase_tenant('tenant-a',bytes(32)) and vars(model.backend)==before; refusals+=1
    grant=model.erasure_authority('tenant-a'); assert model.erase_tenant('tenant-a',grant)
    model=restart_queues(model); restarts+=1
    assert model.tickets=={'repair-b':2} and model.last_ticket==2
    assert all(model.backend.records[key]==raw for key,raw in unrelated.items())
    assert model.address('queue','tenant:tenant-a') not in model.backend.records
    assert model.counter_key('tenant','tenant-a') not in model.backend.records
    assert not any(key in model.backend.records for key in (model.address('authority','repair-a'),model.address('wait','repair-a'),model.charge_key(model.subject('repair-a'),'tenant-a')))
    return {'restarts':restarts,'refusals':refusals,'cleanup':cleanup,'repairs':repairs,'bounds':bounds,'moves':262,'capacity_refusals':capacity_refusals}

loop7_queue_metrics=verify_loop7_queues() if not globals().get('fault_probe') or str(globals().get('fault_name','')).startswith('loop7 ') else {}
if loop7_queue_metrics: assert loop7_queue_metrics=={'restarts':140,'refusals':28,'cleanup':4,'repairs':2,'bounds':8,'moves':262,'capacity_refusals':5},loop7_queue_metrics

def membership_exit(trigger, zero_send, same_bytes):
    return 'ContinueSamePin' if trigger in {'configuration-revision', 'membership-revision'} and zero_send and same_bytes else 'FirstSendMembershipChangedHold'
assert membership_exit('manual', True, True) == 'FirstSendMembershipChangedHold'
assert membership_exit('configuration-revision', True, False) == 'FirstSendMembershipChangedHold'
assert membership_exit('configuration-revision', True, True) == 'ContinueSamePin'
assert membership_exit('membership-revision', True, True) == 'ContinueSamePin'
assert membership_exit('membership-revision', False, True) == 'FirstSendMembershipChangedHold'

def membership_resolution(namespace, trigger, proof, current_bytes, expected_generation, expected_predecessor, now):
    before = deepcopy(namespace)
    if trigger not in {'configuration-revision','membership-revision'}: return 'FirstSendMembershipChangedHold',before
    if namespace['attempts'] != 0: return 'FirstSendMembershipChangedHold',before
    payload = {key:value for key,value in proof.items() if key != 'receipt'}
    if (proof['receipt'] != sha256(codec['canonical_image_bytes'](payload)).digest()
            or not proof['zero'] or proof['at'] != now or proof['head'] != expected_predecessor):
        return 'FirstSendMembershipChangedHold',before
    if current_bytes != namespace['pin_bytes']: return 'FirstSendMembershipChangedHold',before
    if expected_generation == namespace['generation'] and namespace['head'].startswith(b'HX-EV-FIRST-SEND-MEMBERSHIP-RESOLUTION-1\0'):
        fields=decode_record(namespace['head'],'HX-EV-FIRST-SEND-MEMBERSHIP-RESOLUTION-1',codec['schemas']['D16-membership-resolution'])
        if fields[5:9] == (expected_generation,expected_predecessor,proof['membership'],proof['root']) and fields[11] == now:
            return 'ContinueSamePin',before
        return 'FirstSendMembershipChangedHold',before
    if expected_generation != namespace['generation']+1 or expected_predecessor != sha256(namespace['head']).digest():
        return 'FirstSendMembershipChangedHold',before
    raw = R('HX-EV-FIRST-SEND-MEMBERSHIP-RESOLUTION-1',12,U('t'),codec['H']('scope'),N(1),U('event-1'),
        sha256(namespace['pin_bytes']).digest(),N(expected_generation),expected_predecessor,proof['membership'],
        proof['root'],U('ContinueSamePin'),U('broker'),Q(now))
    fields = decode_record(raw,'HX-EV-FIRST-SEND-MEMBERSHIP-RESOLUTION-1',codec['schemas']['D16-membership-resolution'])
    assert fields[5:7] == (expected_generation,expected_predecessor) and fields[8] == proof['root']
    successor = deepcopy(namespace); successor.update(head=raw,generation=expected_generation)
    return 'ContinueSamePin',successor

def verify_membership_resolution():
    initial = {'head':b'authenticated-initial-C2-outcome','generation':1,'attempts':0,'pin_bytes':b'exact-pin-and-six-headers'}
    for trigger in ('configuration-revision','membership-revision'):
        proof = {'zero':True,'at':1000,'head':sha256(initial['head']).digest(),'root':codec['H']('fresh-zero-send'),
                 'membership':codec['H'](trigger)}
        def sign(value):
            value = deepcopy(value); value.pop('receipt',None)
            value['receipt'] = sha256(codec['canonical_image_bytes'](value)).digest(); return value
        proof = sign(proof)
        outcome,resolved = membership_resolution(initial,trigger,proof,initial['pin_bytes'],2,proof['head'],1000)
        assert outcome == 'ContinueSamePin' and resolved['generation'] == 2 and initial['generation'] == 1
        for damage in ('old-time','nonzero-proof','forged-receipt','changed-pin','skipped-generation','stale-predecessor','already-sent'):
            state = deepcopy(initial); input_proof = deepcopy(proof); pin = initial['pin_bytes']; generation=2; predecessor=proof['head']
            if damage == 'old-time': input_proof['at']=999; input_proof=sign(input_proof)
            elif damage == 'nonzero-proof': input_proof['zero']=False; input_proof=sign(input_proof)
            elif damage == 'forged-receipt': input_proof['receipt']=bytes(32)
            elif damage == 'changed-pin': pin += b'changed'
            elif damage == 'skipped-generation': generation=3
            elif damage == 'stale-predecessor': predecessor=bytes(32); input_proof['head']=predecessor; input_proof=sign(input_proof)
            else: state['attempts']=1
            assert membership_resolution(state,trigger,input_proof,pin,generation,predecessor,1000) == ('FirstSendMembershipChangedHold',state),damage
        next_proof = sign(dict(proof,head=sha256(resolved['head']).digest(),at=1001))
        outcome,next_state = membership_resolution(resolved,trigger,next_proof,initial['pin_bytes'],3,next_proof['head'],1001)
        assert outcome == 'ContinueSamePin' and decode_record(next_state['head'],'HX-EV-FIRST-SEND-MEMBERSHIP-RESOLUTION-1',codec['schemas']['D16-membership-resolution'])[6] == sha256(resolved['head']).digest()
        # Exact retry reads the authenticated retained head without a new CAS.
        assert membership_resolution(next_state,trigger,next_proof,initial['pin_bytes'],3,next_proof['head'],1001) == ('ContinueSamePin',next_state)
    return 2
assert verify_membership_resolution() == 2

def legacy_resume(capsule, root_matches, rejection_known):
    if not capsule or not rejection_known: return 'legacy_resume_evidence_unavailable'
    if not root_matches: return 'resume_evidence_hold'
    return 'rearm-stored-range'
assert legacy_resume(True, True, True) == 'rearm-stored-range'
assert legacy_resume(False, True, True) == 'legacy_resume_evidence_unavailable'
assert legacy_resume(True, True, False) == 'legacy_resume_evidence_unavailable'
assert legacy_resume(True, False, True) == 'resume_evidence_hold'

def render_recovery(first, second, first_receipt, second_receipt, immutable, generation):
    if first != immutable+'-response' or second != immutable+'-outcome':
        return 'response_preparation_hold'
    expected = ('receipt', immutable, generation)
    if first_receipt != expected or second_receipt != expected:
        return 'response_preparation_hold'
    return (first, second, 'prepared')
receipt = ('receipt', 'x', 7)
assert render_recovery('x-response', 'x-outcome', receipt, receipt, 'x', 7) == ('x-response', 'x-outcome', 'prepared')
assert render_recovery('x-response', None, receipt, receipt, 'x', 7) == 'response_preparation_hold'
assert render_recovery(None, 'x-outcome', receipt, receipt, 'x', 7) == 'response_preparation_hold'
assert render_recovery('x-response', 'x-outcome', None, receipt, 'x', 7) == 'response_preparation_hold'
assert render_recovery('x-response', 'x-outcome', receipt, ('receipt','x',6), 'x', 7) == 'response_preparation_hold'

def checked_add(left, right, *, positive=False):
    if (type(left) is not int or type(right) is not int or left < 0 or right < 0
            or (positive and right == 0) or left > U64_MAX-right):
        return None
    return left + right

def request(identity_key, reason=b'retry after repair', source=sha256(b'limit-hash').digest(), tenant='t', handle='h'):
    carrier = R('HX-EV-PUBLICATION-RESUME-CARRIER-1',5,U(tenant),U(handle),source,U(identity_key),U(reason))
    identity = sha256(b'HX-EV-PUBLICATION-RESUME-IDENTITY-1\0\x01'+U(tenant)+U(handle)+U(identity_key)).digest()
    return identity, carrier

def state_hash(state):
    return sha256(image_bytes(state)).digest()

def publication_invocation(claim, ordinal, limit, request_identity, unresolved, legacy_root=None):
    rows = b''.join(codec['pack']('>I',position)+U(message)+sha256(body).digest()
                    for position,message,body in sorted(unresolved))
    root = sha256(b'HX-EV-PUBLICATION-UNRESOLVED-1\0\x01'+codec['pack']('>I',len(unresolved))+rows).digest()
    root = root if legacy_root is None else legacy_root
    return sha256(b'HX-EV-PUBLICATION-INVOCATION-1\0\x01'+claim+N(ordinal)+N(limit)+request_identity+root).digest()

def image_bytes(value):
    return codec['canonical_image_bytes'](value)

def read_image(raw):
    return codec['read_canonical_image'](raw)

def existing_window_bytes(tenant, roster, unresolved=None):
    return R('HX-EV-PUBLICATION-WINDOW-2',13,U(tenant),codec['H']('scope'),U('operation'),N(7),codec['H']('prior-closure'),
        codec['H']('prior-window-state'),codec['H']('prior-window-request'),unresolved_root(roster[1:] if unresolved is None else unresolved),
        codec['H']('policy'),N(16),U('admin'),Q(9990000000),codec['H']('capability'))

def publication_state_bytes(state, now):
    live = b''.join(identity+row['carrier_hash']+N(row['result']['ordinal'])+row['result']['audit_hash']+
        N(row['result']['window'])+N(row['result']['limit'])+Q(row['expires_at']*10000000)
        for identity,row in sorted(state['live'].items(),key=lambda item:item[1]['result']['ordinal']))
    expired = b''.join(identity+row['carrier_hash']+Q(row['expires_at']*10000000)+Q(row['delete_after']*10000000)
        for identity,row in sorted(state['tombstones'].items()))
    latest = max(state['live'].values(),key=lambda row:row['result']['ordinal'],default=None)
    raw = R('HX-EV-PUBLICATION-RESUME-STATE-3',14,U(state['tenant']),U('op'),bytes(32) if 'legacy_root' in state else codec['H']('scope'),
        N(state['ordinal']),N(state['window']),N(state['limit']),state['hold_source'],state['window_claim'],
        state.get('history',bytes(32)),N(state['closed']),state.get('last_audit',bytes(32) if latest is None else latest['result']['audit_hash']),
        B(codec['pack']('>I',len(state['live']))+live),B(codec['pack']('>I',len(state['tombstones']))+expired),Q(now*10000000))
    decode_record(raw,'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
    return raw

def existing_attempt_bytes(state, now):
    # Fixture C2 provider authority is keyed by the actual committed roster and
    # old window. It models already-existing registrations/results, never a
    # planned D9 success record.
    rows = []
    claim=decode_record(state['window_claim_bytes'],'HX-EV-PUBLICATION-WINDOW-2',codec['schemas']['D45-window'])
    admitted=codec['window_admission_members'](state['window_admission'],claim,state['roster'],state['accepted'],state['unresolved'],state['window_progress'])
    selected={p for p,m,d in admitted}
    for position,message,body in state['roster']:
        if position not in selected: continue
        parent = sha256(b'fixture-existing-registration:'+N(position)+U(message)+B(body)).digest()
        send = sha256(b'fixture-existing-send:'+parent+N(state['window'])).digest()
        codec['c2_fixture_readback'](state['tenant'],codec['H']('scope'),state['window'],position,message,1,parent,send,
            tuple((i,kind,sha256(b'fixture-existing-'+kind.encode()+parent).digest()) for i,kind in enumerate(('register','result'))))
        rows.extend(codec['pack']('>I',position)+N(1)+N(observation)+U(kind)+parent+send+
            sha256(b'fixture-existing-'+kind.encode()+parent).digest()
            for observation,kind in enumerate(('register','result')))
    exact = b''.join(rows); roster_root = sha256(image_bytes(state['roster'])).digest()
    root = sha256(b'HX-EV-WINDOW-ATTEMPTS-2\0\x01'+U(state['tenant'])+codec['H']('scope')+
        N(state['window'])+roster_root+N(len(rows))+B(exact)).digest()
    return R('HX-EV-WINDOW-ATTEMPT-SET-1',8,U(state['tenant']),codec['H']('scope'),N(state['window']),
        roster_root,N(len(rows)),B(exact),root,Q(now*10000000))

def publication_closure_bytes(prior, authority, broker, now, c2_authority=None):
    fields = decode_record(authority,'HX-EV-WINDOW-ATTEMPT-SET-1',['U','B32','N','B32','N','B','B32','Q'],c2_store=c2_authority)
    assert fields[:3] == (prior['tenant'],codec['H']('scope'),prior['window'])
    assert fields[3] == sha256(image_bytes(prior['roster'])).digest()
    rows = codec['decode_rows'](fields[5],fields[4],['P','N','N','U','B32','B32','B32'],count_ceiling=11328)
    last = {}
    for p,local,observation,kind,parent,send,evidence in rows:
        if kind == 'result' and (p not in last or (local,observation) > last[p][:2]):
            last[p] = (local,observation,evidence)
    claim=decode_record(prior['window_claim_bytes'],'HX-EV-PUBLICATION-WINDOW-2',codec['schemas']['D45-window'])
    admitted=codec['window_admission_members'](prior['window_admission'],claim,prior['roster'],prior['accepted'],prior['unresolved'],prior['window_progress'])
    assert set(last) == {row[0] for row in admitted}, 'closure-admitted-member-coverage'
    sources=codec['c2_readbacks'] if c2_authority is None else c2_authority
    messages={p:m for p,m,d in admitted}
    assert all(sources[(fields[0],fields[1],fields[2],p,local)][4]==messages[p] for p,local in {(r[0],r[1]) for r in rows}), 'closure-c2-message-binding'
    final = b''.join(codec['pack']('>I',p)+N(local)+evidence for p,(local,observation,evidence) in sorted(last.items()))
    raw = R('HX-EV-PUBLICATION-WINDOW-CLOSURE-3',11,U(prior['tenant']),codec['H']('scope'),N(prior['window']),
        B(codec['pack']('>I',len(admitted))+final),sha256(b'fence:'+broker).digest(),
        sha256(b'disable:'+broker).digest(),sha256(b'empty:'+broker).digest(),fields[6],U('SignedCarrier'),
        prior.get('history',bytes(32)),Q(now*10000000))
    decode_record(raw,'HX-EV-PUBLICATION-WINDOW-CLOSURE-3',codec['schemas']['D45-closure'],
        attempt_store={(prior['tenant'],codec['H']('scope'),prior['window']):authority},c2_store=c2_authority)
    return raw

def publication_success_material(prior, identity, carrier_hash, hold, ordinal, window, limit, now, attempt_authority=None, c2_authority=None, predecessor_bytes=None):
    predecessor = publication_state_bytes(prior,now) if predecessor_bytes is None else predecessor_bytes
    predecessor_fields=decode_record(predecessor,'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
    assert predecessor_fields[13]<=now*10000000 and predecessor==publication_state_bytes(prior,predecessor_fields[13]//10000000), 'exact-original-predecessor-utc'
    closure = None
    claim = prior['window_claim_bytes']
    if hold in {'publication_retry_exhausted_hold','publication_drain_limit_and_retry_exhausted_hold'}:
        broker = b'fixture-existing-window-authority:'+codec['H']('scope')+N(prior['window'])
        closure = publication_closure_bytes(prior,existing_attempt_bytes(prior,now) if attempt_authority is None else attempt_authority,broker,now,c2_authority)
        claim = R('HX-EV-PUBLICATION-WINDOW-2',13,U(prior['tenant']),codec['H']('scope'),U('operation'),N(window),
            sha256(closure).digest(),sha256(predecessor).digest(),identity,unresolved_root(prior['unresolved']),
            codec['H']('policy'),N(limit),U('admin'),Q(now*10000000),codec['H']('capability'))
    audit = R('HX-EV-PUBLICATION-RESUME-AUDIT-4',10,U(prior['tenant']),U('op'),N(ordinal),identity,carrier_hash,
        sha256(predecessor).digest(),O(None if closure is None else sha256(closure).digest()),N(window),N(limit),Q(now*10000000))
    return predecessor,closure,claim,audit

imported_fields = ('roster','accepted','unresolved','window_claim_bytes','window_admission','window_progress')
def prepare_bytes(prior, successor, identity, carrier_hash, now, expiry, amount):
    # Images contain only bounded continuation fields. Imported bytes stay at
    # their existing immutable addresses; preparation retains exact field roots.
    def project(state): return codec['continuation_projection']({k:v for k,v in state.items() if k not in imported_fields and k != 'orphans'})
    imports = {name:sha256(image_bytes(prior[name])).digest() for name in imported_fields}
    # A successor window is a newly prepared immutable artifact, not a changed import.
    successor_imports = {name:sha256(image_bytes(successor[name])).digest() for name in imported_fields}
    manifest = image_bytes({'prior':imports,'successor':successor_imports,
                            'window':successor['window_claim_bytes']})
    raw = R('HX-EV-RESUME-PREPARATION-1',12,U(prior['tenant']),U('op'),identity,carrier_hash,
        sha256(image_bytes(project(prior))).digest(),B(image_bytes(project(prior))),B(image_bytes(project(successor))),
        Q(now*10000000),Q(expiry*10000000),N(amount),B(manifest),N(successor['ordinal']))
    assert len(raw) <= MiB
    return raw

def read_preparation(raw, state, identity, carrier_hash):
    schema = ['U','U','B32','B32','B32','B','B','Q','Q','N','B','N']
    fields = decode_record(raw,'HX-EV-RESUME-PREPARATION-1',schema)
    assert fields[0] == state['tenant'] and fields[1] == 'op'
    assert fields[2] == identity and fields[3] == carrier_hash
    assert fields[4] == sha256(fields[5]).digest() and fields[8] > fields[7]
    assert fields[8]-fields[7] <= 9000000000 and fields[9] > 0 and fields[11] > 0
    prior,successor,manifest = codec['continuation_image'](fields[5]),codec['continuation_image'](fields[6]),read_image(fields[10])
    for name in imported_fields:
        assert manifest['prior'][name] == sha256(image_bytes(state[name])).digest()
        prior[name] = deepcopy(state[name])
        value = (manifest['window'] if name == 'window_claim_bytes' else
                 codec['window_admission_bytes'](state['unresolved']) if name == 'window_admission'
                 and manifest['window'] != state['window_claim_bytes'] else
                 codec['window_progress_bytes'](codec['window_admission_bytes'](state['unresolved']),state['accepted'],state['unresolved'],state['accepted'])
                 if name == 'window_progress' and manifest['window'] != state['window_claim_bytes'] else state[name])
        assert manifest['successor'][name] == sha256(image_bytes(value)).digest()
        successor[name] = deepcopy(value)
    prior['orphans'],successor['orphans'] = {},{}
    assert successor['ordinal'] == fields[11] and successor['active_charge'] == fields[9]
    return prior,successor

def owned_resume_preparation(result, identity):
    # Consume only an explicitly successful preparation owned by this request.
    # A refused producer result is an owning assertion failure, never a lookup.
    assert isinstance(result,dict) and result.get('outcome')=='orphaned-success', 'expected-owned-resume-preparation'
    state=result.get('state')
    assert isinstance(state,dict) and isinstance(state.get('orphans'),dict) and identity in state['orphans'], 'owned-resume-preparation-missing'
    row=state['orphans'][identity]
    assert isinstance(row,dict) and row.get('owner')==identity and isinstance(row.get('preparation'),bytes) and row['preparation'], 'owned-resume-preparation-authority'
    return row['preparation']

def resume_publication(state, request_identity, carrier, hold, evidence, drain_increment=8,
                       now=1000, expires_at=1900, crash_after_audit=False, attempt_authority=None, c2_authority=None, predecessor_bytes=None):
    before = deepcopy(state)
    carrier_hash = sha256(carrier).digest()
    try:
        tenant,handle,source,key,reason = decode_record(carrier,'HX-EV-PUBLICATION-RESUME-CARRIER-1',
                                                       ['U','U','B32','U','U'])
        recomputed = sha256(b'HX-EV-PUBLICATION-RESUME-IDENTITY-1\0\x01'+U(tenant)+U(handle)+U(key)).digest()
        if request_identity != recomputed:
            return {'outcome':'resume_request_conflict','state':before,'command_executions':0}
    except (AssertionError,UnicodeError):
        return {'outcome':'resume_evidence_hold','state':before,'command_executions':0}
    for collection, outcome in [('live','exact-retry'), ('orphans','orphan-audit-retry')]:
        if request_identity in state[collection]:
            row = state[collection][request_identity]
            if row['carrier_hash'] != carrier_hash:
                return {'outcome':'resume_request_conflict','state':before,'command_executions':0}
            if collection == 'live':
                if now >= row['expires_at']:
                    return {'outcome':'resume_request_expired','state':reconcile_tombstones(state,now),
                            'command_executions':0}
                return {'outcome':outcome,'state':before,'response':row['response'],'command_executions':0}
            # An authenticated audit is a partially committed success. Its exact
            # successor and staged charge complete before any recorded invocation arms.
            immutable = {key:value for key,value in row.items() if key != 'receipt'}
            if row['receipt'] != state_hash(immutable) or row['owner'] != request_identity:
                return {'outcome':'resume_evidence_hold','state':before,'command_executions':0}
            try:
                prior,successor_intent = read_preparation(row['preparation'],state,request_identity,carrier_hash)
            except (AssertionError,ValueError,TypeError,KeyError,UnicodeError):
                return {'outcome':'resume_evidence_hold','state':before,'command_executions':0}
            protected = set(prior) - {'live','tombstones','orphans','audits','used_charge','reconciliation','invocations','invocation_owners'}
            if any(state.get(key) != prior[key] for key in protected):
                return {'outcome':'resume_hold_changed','state':before,'command_executions':0}
            expected_indexes = prior
            if state.get('reconciliation') is not None:
                proof = state['reconciliation']
                payload = {key:value for key,value in proof.items() if key != 'receipt'}
                if proof['receipt'] != state_hash(payload):
                    return {'outcome':'resume_evidence_hold','state':before,'command_executions':0}
                expected_indexes = reconcile_tombstones(prior,proof['at'])
            if any(state.get(key) != expected_indexes.get(key) for key in ('live','tombstones','invocations','invocation_owners')):
                return {'outcome':'resume_evidence_hold','state':before,'command_executions':0}
            # CAS applies only the recorded protected delta to the current head.
            recovered = deepcopy(successor_intent)
            recovered['live'] = deepcopy(state['live'])
            recovered['tombstones'] = deepcopy(state['tombstones'])
            try:
                own_row = successor_intent['live'][request_identity]
                assert successor_intent['ordinal'] == row['result']['ordinal']
                assert successor_intent['active_charge'] == row['staged_charge']
            except (AssertionError,KeyError,TypeError):
                return {'outcome':'resume_evidence_hold','state':before,'command_executions':0}
            recovered['live'][request_identity] = deepcopy(own_row)
            if state.get('reconciliation') is not None:
                recovered['reconciliation'] = deepcopy(state['reconciliation'])
            recovered = reconcile_tombstones(recovered,now)
            assert recovered['ordinal'] == row['result']['ordinal']
            assert recovered['active_charge'] == row['staged_charge']
            return {'outcome':outcome,'state':recovered,'response':row['response'],
                    'command_executions':0}
    if request_identity in state['tombstones']:
        row = state['tombstones'][request_identity]
        result = 'resume_request_expired' if row['carrier_hash'] == carrier_hash else 'resume_request_conflict'
        return {'outcome':result, 'state':before, 'command_executions':0}
    if state['orphans']:
        return {'outcome':'resume_capacity_hold','state':before,'command_executions':0}
    if evidence == 'stale':
        return {'outcome':'resume_hold_changed', 'state':before, 'command_executions':0}
    if evidence == 'unavailable':
        return {'outcome':'resume_evidence_hold', 'state':before, 'command_executions':0}
    if tenant != state['tenant'] or handle != state['handle'] or source != state['hold_source'] or source == bytes(32):
        return {'outcome':'resume_hold_changed','state':before,'command_executions':0}
    if not now < expires_at <= now+900:
        return {'outcome':'resume_request_expired','state':before,'command_executions':0}
    if hold not in {'publication_retry_exhausted_hold','publication_drain_limit_hold',
                    'publication_drain_limit_and_retry_exhausted_hold','legacy_publish_failed'}:
        return {'outcome':'resume_not_eligible', 'state':before, 'command_executions':0}
    legacy = hold == 'legacy_publish_failed'
    retry_exhausted = hold in {'publication_retry_exhausted_hold','publication_drain_limit_and_retry_exhausted_hold'}
    drain_limited = hold in {'publication_drain_limit_hold','publication_drain_limit_and_retry_exhausted_hold'}
    if not legacy and sha256(state['window_claim_bytes']).digest() != state['window_claim']:
        return {'outcome':'resume_evidence_hold','state':before,'command_executions':0}
    committed = state['roster']; unresolved = state['unresolved']; accepted = state['accepted']
    positions = [row[0] for row in committed]; message_ids = [row[1] for row in committed]
    selected = tuple(unresolved) + tuple(accepted)
    if (len(set(positions)) != len(committed) or len(set(message_ids)) != len(committed)
            or len(set(unresolved)) != len(unresolved) or len(set(accepted)) != len(accepted)
            or set(unresolved) & set(accepted) or set(selected) != set(committed)
            or len(selected) != len(committed)):
        return {'outcome':'resume_evidence_hold', 'state':before, 'command_executions':0}
    try:
        if legacy:
            rows = b''.join(N(p)+U(m)+sha256(body).digest() for p,m,body in sorted(committed))
            expected_root = sha256(b'HX-EV-LEGACY-RESUME-EVENTS-2\0\x01'+B(codec['pack']('>I',len(committed))+rows)).digest()
            assert state['legacy_root'] == expected_root and not accepted
            assert state['window'] == 0 and state['window_claim'] == bytes(32) and state['window_claim_bytes'] == b''
        else:
            fields = decode_record(state['window_claim_bytes'],'HX-EV-PUBLICATION-WINDOW-2',codec['schemas']['D45-window'])
            assert fields[:2] == (state['tenant'],codec['H']('scope')) and fields[3] == state['window'], 'direct-window-execution-binding'
            codec['window_admission_members'](state['window_admission'],fields,committed,accepted,unresolved,state['window_progress'])
    except (AssertionError,TypeError,ValueError,KeyError):
        return {'outcome':'resume_evidence_hold','state':before,'command_executions':0}
    if not unresolved:
        return {'outcome':'resume_not_eligible','state':before,'command_executions':0}
    if len(state['live']) + len(state['tombstones']) >= 64:
        return {'outcome':'resume_capacity_hold', 'state':before, 'command_executions':0}
    ordinal = checked_add(state['ordinal'], 1)
    window = checked_add(state['window'], 1) if retry_exhausted else state['window']
    closed = checked_add(state['closed'], 1) if retry_exhausted else state['closed']
    limit = (checked_add(state['limit'], drain_increment, positive=True)
             if drain_limited else state['limit'])
    new_charge = state['next_charge']
    overlap = checked_add(state['active_charge'], new_charge)
    if None in {ordinal, window, closed, limit, overlap}:
        return {'outcome':'resume_arithmetic_exhausted', 'state':before, 'command_executions':0}
    if overlap > state['charge_ceiling']:
        return {'outcome':'resume_capacity_hold', 'state':before, 'command_executions':0}
    try:
        predecessor,closure,window_claim_bytes,audit = publication_success_material(
            before,request_identity,carrier_hash,hold,ordinal,window,limit,now,attempt_authority,c2_authority,predecessor_bytes)
        invocation_owners(state)
    except (AssertionError,KeyError,ValueError,TypeError,UnicodeError):
        return {'outcome':'resume_evidence_hold','state':before,'command_executions':0}
    prior_hash = sha256(predecessor).digest()
    window_intent = sha256(window_claim_bytes).digest()
    window_claim = bytes(32) if legacy else sha256(window_claim_bytes).digest()
    invocation = publication_invocation(window_claim,ordinal,limit,request_identity,unresolved,state.get('legacy_root') if legacy else None)
    result = {
        'ordinal':ordinal, 'window':window, 'limit':limit,
        'audit_hash':sha256(audit).digest(),
    }
    response = json.dumps({'resumeHandle':handle,'resumeOrdinal':ordinal,'window':window,
                           'drainLimit':limit,'auditRecordHash':result['audit_hash'].hex()},
                          sort_keys=True,separators=(',',':')).encode('utf-8')
    successor = deepcopy(state)
    successor.update(ordinal=ordinal, window=window, closed=closed, limit=limit,hold_source=bytes(32),
                     active_charge=new_charge, window_claim=window_claim,window_claim_bytes=window_claim_bytes)
    if retry_exhausted:
        successor['window_admission']=codec['window_admission_bytes'](unresolved)
        successor['window_progress']=codec['window_progress_bytes'](successor['window_admission'],accepted,unresolved,accepted)
    if closure is not None:
        broker = b'fixture-existing-window-authority:'+codec['H']('scope')+N(before['window'])
        successor['history'] = sha256(b'HX-EV-PUBLICATION-WINDOW-HISTORY-1\0\x01'+
            before.get('history',bytes(32))+B(closure)+B(broker)).digest()
    successor['audits'] += 1
    successor['last_audit'] = result['audit_hash']
    owners=invocation_owners(state)
    successor['invocations'] = tuple(state['invocations']) + (invocation,)
    successor['invocation_owners'] = owners + (request_identity,)
    invocation_raw=R('HX-EV-PUBLICATION-INVOCATION-1',9,U(state['tenant']),U('op'),window_claim,N(ordinal),N(limit),request_identity,
        state['legacy_root'] if hold=='legacy_publish_failed' else unresolved_root(unresolved),invocation,Q(now*10000000))
    decode_record(invocation_raw,'HX-EV-PUBLICATION-INVOCATION-1',codec['extra_schemas']['D45-invocation'])
    codec.setdefault('invocation_fixture_readbacks',{})[invocation]=invocation_raw
    successor['live'][request_identity] = {'carrier_hash':carrier_hash, 'result':result,
                                          'response':response,'expires_at':expires_at}
    resolution = None
    if drain_limited:
        resolution = (state['hold_source'],window_intent if retry_exhausted else invocation,invocation,state['limit'],limit)
    if crash_after_audit:
        row = {'carrier_hash':carrier_hash,'result':result,'response':response,
               'expires_at':expires_at,'preparation':prepare_bytes(before,successor,request_identity,carrier_hash,now,expires_at,new_charge),
               'staged_charge':new_charge,'owner':request_identity}
        row['receipt'] = state_hash(row)
        crashed = deepcopy(before)
        crashed['orphans'][request_identity] = row
        crashed['audits'] += 1
        crashed['used_charge'] = overlap
        # Invocation authority is recorded, but publication remains unarmed.
        return {'outcome':'orphaned-success','state':crashed,'response':response,'command_executions':0}
    return {
        'outcome':'resumed', 'state':successor,
        'response':response,
        'rearmed':tuple((row[1], row[2]) for row in unresolved),
        'accepted_unchanged':accepted, 'resolution':resolution,
        'command_executions':0,
    }

def invocation_owners(state, records=None):
    if not state['invocations']:
        assert not state.get('invocation_owners'), 'invocation-owner-binding'
        return ()
    if 'invocation_owners' in state: owners=tuple(state['invocation_owners'])
    else:
        owners=tuple(identity for identity,row in sorted(state['live'].items(),key=lambda item:item[1]['result']['ordinal']))
        assert not state['tombstones'] or not state['invocations'], 'invocation-owner-authority'
    assert len(owners)==len(state['invocations']) and len(set(owners))==len(owners), 'invocation-owner-binding'
    retained=set(state['live'])|set(state['tombstones'])|set(state.get('orphans',{}))
    assert set(owners)<=retained, 'invocation-retained-owner-binding'
    if records is not None:
        for owner,identity in zip(owners,state['invocations']):
            assert identity in records, 'invocation-native-source-missing'
            fields=decode_record(records[identity],'HX-EV-PUBLICATION-INVOCATION-1',codec['extra_schemas']['D45-invocation'])
            assert fields[:2]==(state['tenant'],'op') and fields[5]==owner and fields[7]==identity and fields[3]<=state['ordinal'], 'invocation-native-owner-binding'
            if owner in state['live']: assert fields[3]==state['live'][owner]['result']['ordinal']
    return owners

def reconcile_tombstones(state, now, authenticated=True):
    successor = deepcopy(state)
    if not authenticated:
        return successor
    for identity,row in list(successor['live'].items()):
        if now >= row['expires_at']:
            expiry = row['expires_at']
            successor['tombstones'][identity] = {'carrier_hash':row['carrier_hash'],
                'expires_at':expiry,'delete_after':expiry+30*86400}
            del successor['live'][identity]
    successor['tombstones'] = {
        identity:row for identity,row in successor['tombstones'].items()
        if row['delete_after'] > now}
    owners=invocation_owners(state)
    retained=set(successor['live'])|set(successor['tombstones'])|set(successor.get('orphans',{}))
    pairs=tuple((owner,value) for owner,value in zip(owners,state['invocations']) if owner in retained)
    successor['invocations']=tuple(value for owner,value in pairs)
    if 'invocation_owners' in state or owners: successor['invocation_owners']=tuple(owner for owner,value in pairs)
    if successor['live'] != state['live'] or successor['tombstones'] != state['tombstones']:
        proof = {'at':now,'live':deepcopy(successor['live']),'tombstones':deepcopy(successor['tombstones'])}
        if 'invocation_owners' in successor:
            proof['invocation_root']=sha256(image_bytes((successor['invocation_owners'],successor['invocations']))).digest()
        proof['receipt'] = state_hash(proof)
        successor['reconciliation'] = proof
    return successor

class PreparationBackend:
    """Provider model: durable byte rows and authenticated readback receipts only."""
    def __init__(self):
        self.rows = {}; self.receipts = {}; self.deletions = {}; self.signatures = {}; self.unavailable = set()
        self.c2_sources = {}  # Existing C2-owned bytes; excluded from the resume slot charge.
    def receipt(self, key, raw, owner, generation):
        return sha256(b'provider-record-readback:'+U(key)+owner+N(generation)+B(raw)).digest()
    def read(self, key, owner, optional=False):
        assert key not in self.unavailable
        if key not in self.rows:
            assert optional and key not in self.receipts
            if key in self.deletions:
                recorded_owner,digest,receipt = self.deletions[key]
                assert recorded_owner == owner and receipt == self.deletion_receipt(key,owner,digest)
            return None
        raw = self.rows[key]; recorded_owner,generation,receipt = self.receipts[key]
        assert recorded_owner == owner and receipt == self.receipt(key,raw,owner,generation)
        return raw,generation,receipt
    def write(self, key, raw, owner, generation=1, expected=None, create_once=False):
        assert type(generation) is int and 1<=generation<=U64_MAX, 'provider-native-generation-bound'
        existing = self.read(key,owner,optional=True)
        if existing is not None:
            if create_once:
                assert existing[0] == raw
                return existing
            assert expected == sha256(existing[0]).digest() and generation == existing[1]+1
        else: assert expected is None and generation == 1
        self.rows[key] = raw
        self.deletions.pop(key,None)
        self.receipts[key] = (owner,generation,self.receipt(key,raw,owner,generation))
        return self.read(key,owner)
    def write_signed(self, key, raw, signature, owner):
        assert 0 < len(signature) <= 8192 and signature == sha256(b'fixture-purpose-2d:'+raw).digest()
        authority=(owner,sha256(raw).digest(),signature,sha256(b'provider-signature-readback:'+U(key)+owner+B(raw)+B(signature)).digest())
        assert key not in self.signatures or self.signatures[key] == authority
        # One provider-native signed-carrier write: bytes and exact envelope
        # metadata commit/read back at the same existing claim address.
        readback=self.write(key,raw,owner,create_once=True)
        self.signatures[key]=authority
        assert self.read_signed(key,owner) == (raw,signature)
        return readback
    def read_signed(self, key, owner):
        raw=self.read(key,owner)[0]
        assert key in self.signatures, 'signed-carrier-authentication-missing'
        recorded_owner,payload_hash,signature,receipt=self.signatures[key]
        assert recorded_owner == owner and payload_hash == sha256(raw).digest() and 0 < len(signature) <= 8192
        assert signature == sha256(b'fixture-purpose-2d:'+raw).digest()
        assert receipt == sha256(b'provider-signature-readback:'+U(key)+owner+B(raw)+B(signature)).digest()
        return raw,signature
    def delete(self, key, owner, expected):
        existing = self.read(key,owner)
        assert sha256(existing[0]).digest() == expected
        del self.rows[key]; del self.receipts[key]; self.signatures.pop(key,None)
        receipt = self.deletion_receipt(key,owner,expected)
        self.deletions[key] = (owner,expected,receipt)
        return receipt
    def deletion_receipt(self, key, owner, digest):
        return sha256(b'provider-deletion-readback:'+U(key)+owner+digest).digest()
    def snapshot(self):
        return deepcopy((self.rows,self.receipts,self.deletions,self.signatures,self.unavailable,self.c2_sources))

class PreparationStore:
    stages = ('claim','resolution','fence','closure','window','reconstruction','audit','successor','finalize','invocation')
    head_schema = codec['extra_schemas']['D45-preparation-head']
    origin_schema = codec['extra_schemas']['D45-origin']
    charge_schema = codec['schemas']['D29-charge']
    counter_schema = codec['schemas']['D29-counter']
    aliases = {'successor':'state'}
    def __init__(self, owner, carrier, prepared, prior, now=1000, expiry=1900, crash_after=None, eligible='drain-limit', attempt_authority=None, legacy_bundle=None, predecessor_utc=None):
        assert eligible in {'drain-limit','retry-exhausted','drain-limit-and-retry-exhausted','legacy-publish-failed'}
        self.backend = PreparationBackend(); self.tenant = prior['tenant']; self.execution = 'op'
        legacy = eligible == 'legacy-publish-failed'
        predecessor = publication_state_bytes(prior,now if predecessor_utc is None else predecessor_utc)
        # Retained existing typed invocations already consume their 4 KiB slot
        # reservations. Import exact original addressed bytes/owner receipts.
        owners=invocation_owners(prior)
        records=codec.get('invocation_fixture_readbacks',{})
        invocation_owners(prior,records)
        for previous_owner,identity in zip(owners,prior['invocations']):
            key=codec['K']('HX-EV-PUBLICATION-INVOCATION-KEY-1',U(self.tenant),U(self.execution),identity)
            self.backend.write(key,records[identity],previous_owner,create_once=True)
        a8_head = b'head'  # independently authenticated existing A8 fixture readback
        previous_audit = decode_record(predecessor,'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])[10]
        claim = R('HX-EV-PUBLICATION-RESUME-3',15,U('admin'),U(prior['tenant']),U('op'),bytes(32) if legacy else codec['H']('scope'),
            U(eligible),prior['hold_source'],bytes(32) if legacy else sha256(a8_head).digest(),N(prior['ordinal']+1),previous_audit,U(prior['handle']),
            owner,sha256(carrier).digest(),U('operator'),Q(now*10000000),Q(expiry*10000000))
        origin = R('HX-EV-RESUME-ORIGIN-1',11,U(prior['tenant']),U('op'),owner,sha256(carrier).digest(),
            B(carrier),B(claim),B(image_bytes(codec['continuation_projection']({k:v for k,v in prior.items() if k not in imported_fields and k != 'orphans'}))),N(1),Q(now*10000000),Q(expiry*10000000),B(sha256(b'fixture-purpose-2d:'+claim).digest()))
        decode_record(origin,'HX-EV-RESUME-ORIGIN-1',self.origin_schema)
        # Read-only fixture-provider imports resolve existing charged authority.
        # Legacy uses D10 capsule/chunks/stored events; it has no A8/C2/window
        # prerequisite and retains no synthetic empty window-authority record.
        inputs={'predecessor':predecessor}
        if legacy:
            verified=validate_capsule(legacy_bundle)
            assert verified is not None and verified['hash'] == prior['hold_source'] and verified['root'] == prior['legacy_root']
            members=tuple((sequence,message,sha256(body).digest()) for sequence,message,body in prior['roster'])
            assert verified['rows'] == members and prior['accepted'] == () and prior['unresolved'] == prior['roster']
            inputs.update({'legacy-capsule':legacy_bundle['manifest'],'legacy-stored-events':image_bytes(prior['roster'])})
        else:
            for kind in ('window_admission','window_progress'):
                self.backend.write(self.window_source_key(prior['window_claim'],kind),prior[kind],bytes(32),create_once=True)
            inputs.update({name:image_bytes(prior[name]) for name in imported_fields})
            inputs.update(**{'a8-head':a8_head},attempts=existing_attempt_bytes(prior,now) if attempt_authority is None else attempt_authority,
                broker=b'fixture-existing-window-authority:'+codec['H']('scope')+N(prior['window']))
        if not legacy:
            # Resolve original C2-owned addressed records; never copy these into
            # a preparation import or charge their bytes to the 2 MiB slot.
            for locator,source in codec['c2_readbacks'].items():
                if locator[:3]==(self.tenant,codec['H']('scope'),prior['window']):
                    self.backend.c2_sources[locator]=image_bytes(source)
        origin_hash = sha256(origin).digest()
        for kind,raw in inputs.items():
            self.backend.write(self.import_key(owner,origin_hash,kind),raw,owner,create_once=True)
        if legacy:
            for key,raw in legacy_bundle['objects'].items():
                self.backend.write(key,raw,owner,create_once=True)
        # The existing execution CAS head is real predecessor authority, not a
        # cached future successor. Its replacement must name this exact readback.
        self.backend.write(self.key('HX-EV-PUBLICATION-RESUME-STATE-KEY-1',U(self.tenant),U(self.execution)),
            predecessor,owner,create_once=True)
        subject = self.execution+':'+owner.hex()
        entry = R('HX-EV-HOLD-ENTRY-2',13,U('tenant'),U(self.tenant),U('PublicationResumePreparationHold'),U(subject),
            O(None),U('publication_resume_preparation_hold'),N(1),bytes(32),Q(now*10000000),Q(now*10000000),
            N(1),U('coordinator'),Q((now+3600)*10000000))
        self.backend.write(self.entry_key(subject),entry,owner,create_once=True)
        active_kind=('PublicationDrainLimitHold' if eligible in {'drain-limit','drain-limit-and-retry-exhausted'} else
            'PublicationRetryExhaustedHold' if eligible=='retry-exhausted' else 'LegacyResumeIncident')
        active_subject='active:'+prior['hold_source'].hex()
        active_reason=codec['hold_reasons'][active_kind]
        active_entry=R('HX-EV-HOLD-ENTRY-2',13,U('tenant'),U(self.tenant),U(active_kind),U(active_subject),O(None),
            U(active_reason),N(1),bytes(32),Q(now*10000000),Q(now*10000000),N(1),
            U('coordinator' if eligible!='legacy-publish-failed' else 'actor'),Q((now+3600)*10000000))
        decode_record(active_entry,'HX-EV-HOLD-ENTRY-2',codec['schemas']['D37-entry'])
        self.backend.write(self.active_entry_key(active_kind,active_subject),active_entry,owner,create_once=True)
        inventory_rows=sorted([(now*10000000,'PublicationResumePreparationHold',subject,sha256(entry).digest()),
            (now*10000000,active_kind,active_subject,sha256(active_entry).digest())],key=lambda row:(row[0],row[1].encode(),row[2].encode()))
        index = R('HX-EV-HOLD-INDEX-2',8,U('tenant'),U(self.tenant),N(1),N(2),
            B(b''.join(Q(at)+U(kind)+U(subject)+digest for at,kind,subject,digest in inventory_rows)),
            N(0),bytes(32),Q(now*10000000))
        self.backend.write(self.inventory_key,index,owner,create_once=True)
        self.backend.write(self.origin_key(owner),origin,owner,create_once=True)
        # This bounded provider-model admission is one ledger-owner CAS: its
        # origin, reserved inventory and typed stage/counters commit together.
        self.save_charges(owner,prior['active_charge'],prior['next_charge'],'staged',prior['active_charge']+prior['next_charge'],initial=True)
        assert self.reconstruct(owner) == prepared
        if crash_after == 'origin': return
        self.progress(owner,'admitted',{},None)
        if crash_after == 'progress': return
    def key(self, domain, *fields):
        return codec['K'](domain,*fields)
    @property
    def inventory_key(self): return self.key('HX-EV-HOLD-INDEX-KEY-1',U('tenant'),U(self.tenant))
    def entry_key(self, subject): return self.key('HX-EV-HOLD-ENTRY-KEY-1',U('tenant'),U(self.tenant),U('PublicationResumePreparationHold'),U(subject))
    def active_entry_key(self, kind, subject):
        return self.key('HX-EV-HOLD-ENTRY-KEY-1',U('tenant'),U(self.tenant),U(kind),U(subject))
    def consume_active_hold(self,backend):
        claim=decode_record(self.origin_fields()[5],'HX-EV-PUBLICATION-RESUME-3',codec['schemas']['D45-request'])
        subject='active:'+claim[5].hex()
        previous=backend.read(self.inventory_key,self.owner)
        f=list(decode_record(previous[0],'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index']))
        rows=codec['decode_rows'](f[4],f[3],['Q','U','U','B32'],[1024,1024,4096,1024])
        active=[row for row in rows if row[2]==subject]
        assert len(active)==1, 'resume-active-hold-authority'
        at,kind,name,digest=active[0]; key=self.active_entry_key(kind,name)
        raw=backend.read(key,self.owner)[0]
        assert sha256(raw).digest()==digest
        assert backend.delete(key,self.owner,digest)==backend.deletion_receipt(key,self.owner,digest)
        backend.deletions.pop(key,None)
        rows=[row for row in rows if row[2]!=subject]
        f[2],f[3],f[4],f[6]=checked_add(previous[1],1),len(rows),b''.join(Q(at)+U(kind)+U(name)+digest for at,kind,name,digest in rows),sha256(previous[0]).digest()
        assert f[2] is not None
        raw=R('HX-EV-HOLD-INDEX-2',8,*(codec['encode_typed'](k,v) for k,v in zip(codec['schemas']['D37-index'],f)))
        backend.write(self.inventory_key,raw,self.owner,f[2],sha256(previous[0]).digest())
    @property
    def head_key(self): return self.key('HX-EV-RESUME-PREPARATION-HEAD-KEY-1',U(self.tenant),U(self.execution))
    def origin_key(self, owner): return self.key('HX-EV-RESUME-ORIGIN-KEY-1',U(self.tenant),U(self.execution),owner)
    def preparation_key(self, owner): return self.key('HX-EV-RESUME-PREPARATION-KEY-1',U(self.tenant),U(self.execution),owner)
    def import_key(self, owner, origin_hash, kind):
        # Existing provider fixture address: its literal prefix and digest framing are imported.
        return 'fixture-provider-import:'+codec['KD']('HX-EV-EXISTING-PROVIDER-INPUT',owner,origin_hash,U(kind))
    def window_source_key(self, claim_hash, kind):
        assert kind in {'window_admission','window_progress'}
        return 'fixture-'+kind.replace('_','-')+':'+claim_hash.hex()
    def charge_key(self, owner, kind):
        return self.key('HX-EV-PUBLICATION-CHARGE-KEY-1',U('deployment-a'),U('tenant'),U(self.tenant),
                        sha256(U(kind)+owner).digest())
    def counter_key(self, kind):
        account = self.tenant if kind == 'tenant' else kind
        return self.key('HX-EV-PUBLICATION-COUNTER-KEY-1',U('deployment-a'),U(kind),U(account))
    @property
    def owner(self):
        raw = self.backend.rows[self.inventory_key]
        index = decode_record(raw,'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index'])
        assert index[:2] == ('tenant',self.tenant) and index[5] == 0
        rows = codec['decode_rows'](index[4],index[3],['Q','U','U','B32'],[1024,1024,4096,1024])
        own_rows = [row for row in rows if row[1] == 'PublicationResumePreparationHold' and row[2].startswith(self.execution+':')]
        assert len(own_rows) <= 1
        if not own_rows:
            head = decode_record(self.backend.rows[self.head_key],'HX-EV-RESUME-PREPARATION-HEAD-1',self.head_schema)
            assert head[:2] == (self.tenant,self.execution) and head[5] in {'completed','rolled-back'}
            owner = head[2]
            assert self.backend.read(self.inventory_key,owner)[1] == index[2]
            assert self.backend.read(self.head_key,owner)[1] == head[6]
            return owner
        observed,hold,subject,entry_hash = own_rows[0]
        assert hold == 'PublicationResumePreparationHold' and subject.startswith(self.execution+':')
        owner = bytes.fromhex(subject[len(self.execution)+1:]); assert len(owner) == 32
        assert self.backend.read(self.inventory_key,owner)[1] == index[2]
        entry_row = self.backend.read(self.entry_key(subject),owner)
        assert sha256(entry_row[0]).digest() == entry_hash
        entry = decode_record(entry_row[0],'HX-EV-HOLD-ENTRY-2',codec['schemas']['D37-entry'])
        assert entry[:4] == ('tenant',self.tenant,hold,subject) and entry[8] == observed
        assert entry[5] == 'publication_resume_preparation_hold' and entry[11] == 'coordinator'
        assert entry[6] == entry_row[1]
        return owner
    @property
    def origin(self): return self.backend.read(self.origin_key(self.owner),self.owner)[0]
    @property
    def carrier(self): return self.origin_fields()[4]
    def origin_fields(self):
        fields = decode_record(self.origin,'HX-EV-RESUME-ORIGIN-1',self.origin_schema)
        assert fields[:3] == (self.tenant,self.execution,self.owner)
        return fields
    def head(self):
        row = self.backend.read(self.head_key,self.owner,optional=True)
        if row is None: return None
        fields = decode_record(row[0],'HX-EV-RESUME-PREPARATION-HEAD-1',self.head_schema)
        assert fields[:3] == (self.tenant,self.execution,self.owner)
        assert fields[3] == sha256(self.origin).digest() and fields[6] == row[1]
        return fields
    def manifest(self):
        head = self.head()
        if head is None: return {}
        count = int.from_bytes(head[8][:4],'big')
        rows = codec['decode_rows'](head[8][4:],count,['U','U','B32','B32','U'],[1024,128,1024,1024,1024])
        return {kind:(address,digest,receipt,disposition) for kind,address,digest,receipt,disposition in rows}
    def progress(self, owner, phase, rows, preparation_hash, now=None):
        assert owner == self.owner and len(rows) <= 8
        predecessor = self.backend.read(self.head_key,owner,optional=True)
        generation = 1 if predecessor is None else checked_add(predecessor[1],1)
        assert generation is not None
        manifest = codec['pack']('>I',len(rows))+b''.join(U(kind)+U(address)+digest+receipt+U(disposition)
            for kind,(address,digest,receipt,disposition) in sorted(rows.items()))
        raw = R('HX-EV-RESUME-PREPARATION-HEAD-1',10,U(self.tenant),U(self.execution),owner,
            sha256(self.origin).digest(),O(preparation_hash),U(phase),N(generation),
            bytes(32) if predecessor is None else sha256(predecessor[0]).digest(),B(manifest),
            Q(self.origin_fields()[8] if now is None else now*10000000))
        decode_record(raw,'HX-EV-RESUME-PREPARATION-HEAD-1',self.head_schema)
        self.backend.write(self.head_key,raw,owner,generation,
            expected=None if predecessor is None else sha256(predecessor[0]).digest())
    def imported(self, kind):
        claim=decode_record(self.origin_fields()[5],'HX-EV-PUBLICATION-RESUME-3',codec['schemas']['D45-request'])
        if claim[4] == 'legacy-publish-failed' and kind in imported_fields:
            verified,members=self.legacy_authority()
            return image_bytes({'roster':members,'accepted':(),'unresolved':members,'window_claim_bytes':b'','window_admission':b'','window_progress':b''}[kind])
        raw=self.backend.read(self.import_key(self.owner,sha256(self.origin).digest(),kind),self.owner)[0]
        if kind in {'window_admission','window_progress'}:
            prior=codec['continuation_image'](self.origin_fields()[6])
            assert self.backend.read(self.window_source_key(prior['window_claim'],kind),bytes(32))[0]==read_image(raw), 'window-source-provider-readback'
        return raw
    def c2_authority(self, attempts):
        # Fresh native reads from the original C2 owner. Native fixture locator
        # framing is imported, not an additional public record/key family.
        prior=codec['continuation_image'](self.origin_fields()[6])
        selected={k:v for k,v in self.backend.c2_sources.items() if k[:3]==(self.tenant,codec['H']('scope'),prior['window'])}
        assert len(selected)<=59*64, 'c2-native-source-window-bound'
        sources={}
        for locator,raw in selected.items():
            assert locator not in self.backend.unavailable and len(raw)<=16*1024, 'c2-native-source-unavailable'
            sources[locator]=read_image(raw)
        return sources
    def erase_c2_sources(self, authority):
        # Existing C2 tenant-erasure owner, independent of resume compaction.
        selected={k:v for k,v in self.backend.c2_sources.items() if k[0]==self.tenant}
        assert authority==sha256(b'fixture-c2-tenant-erasure:'+U(self.tenant)+image_bytes(selected)).digest()
        self.backend.c2_sources={k:v for k,v in self.backend.c2_sources.items() if k[0]!=self.tenant}
        return len(selected)
    def legacy_authority(self):
        claim=decode_record(self.origin_fields()[5],'HX-EV-PUBLICATION-RESUME-3',codec['schemas']['D45-request'])
        assert claim[4] == 'legacy-publish-failed' and claim[3] == claim[6] == bytes(32)
        raw=self.imported('legacy-capsule')
        assert sha256(raw).digest() == claim[5]
        fields=decode_record(raw,'HX-EV-LEGACY-RESUME-CAPSULE-2',codec['schemas']['D46-capsule'])
        assert fields[0] == self.tenant and fields[4] in {None,self.execution}
        rows=codec['decode_rows'](fields[12][4:],int.from_bytes(fields[12][:4],'big'),['N','N','N','B32','N','U'],count_ceiling=17)
        objects={row[5]:self.backend.read(row[5],self.owner)[0] for row in rows}
        verified=validate_capsule({'manifest':raw,'objects':objects})
        assert verified is not None and verified['hash'] == claim[5]
        members=read_image(self.imported('legacy-stored-events'))
        assert isinstance(members,tuple) and 1 <= len(members) <= 1000
        exact=tuple((sequence,message,sha256(body).digest()) for sequence,message,body in members)
        assert verified['rows'] == exact and exact_legacy_root(exact) == verified['root']
        prior=codec['continuation_image'](self.origin_fields()[6])
        assert prior['hold_source'] == claim[5] and prior['legacy_root'] == verified['root']
        assert prior['window'] == prior['closed'] == 0 and prior['window_claim'] == bytes(32)
        return verified,members
    def reconstruct(self, owner):
        assert owner == self.owner
        fields = self.origin_fields(); prior = codec['continuation_image'](fields[6])
        owners=invocation_owners(prior); invocation_sources={}
        for previous_owner,identity in zip(owners,prior['invocations']):
            key=codec['K']('HX-EV-PUBLICATION-INVOCATION-KEY-1',U(self.tenant),U(self.execution),identity)
            invocation_sources[identity]=self.backend.read(key,previous_owner)[0]
        invocation_owners(prior,invocation_sources)
        for kind in imported_fields: prior[kind] = read_image(self.imported(kind))
        prior['orphans'] = {}
        predecessor=self.imported('predecessor')
        predecessor_fields=decode_record(predecessor,'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
        assert predecessor_fields[13] <= fields[8] and predecessor == publication_state_bytes(prior,predecessor_fields[13]//10000000)
        claim = decode_record(fields[5],'HX-EV-PUBLICATION-RESUME-3',codec['schemas']['D45-request'])
        legacy = claim[4] == 'legacy-publish-failed'
        if not legacy:
            window = decode_record(prior['window_claim_bytes'],'HX-EV-PUBLICATION-WINDOW-2',codec['schemas']['D45-window'])
            assert window[:2] == (self.tenant,codec['H']('scope')) and window[3] == prior['window']
            codec['window_admission_members'](prior['window_admission'],window,prior['roster'],prior['accepted'],prior['unresolved'],prior['window_progress'])
        assert claim[6] == (bytes(32) if legacy else sha256(self.imported('a8-head')).digest()) and claim[7] == prior['ordinal']+1
        assert claim[8] == decode_record(self.imported('predecessor'),'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])[10]
        attempt_authority=None; c2_authority=None
        if legacy:
            verified,members=self.legacy_authority()
            assert prior['roster'] == prior['unresolved'] == members and prior['accepted'] == ()
            assert claim[5] == verified['hash'] == prior['hold_source']
        else:
            attempt_authority=self.imported('attempts')
            c2_authority=self.c2_authority(attempt_authority)
            attempts = decode_record(attempt_authority,'HX-EV-WINDOW-ATTEMPT-SET-1',['U','B32','N','B32','N','B','B32','Q'],c2_store=c2_authority)
            assert attempts[:4] == (self.tenant,codec['H']('scope'),prior['window'],sha256(image_bytes(prior['roster'])).digest())
            assert self.imported('broker') == b'fixture-existing-window-authority:'+codec['H']('scope')+N(prior['window'])
        hold = {'drain-limit':'publication_drain_limit_hold','retry-exhausted':'publication_retry_exhausted_hold',
                'drain-limit-and-retry-exhausted':'publication_drain_limit_and_retry_exhausted_hold',
                'legacy-publish-failed':'legacy_publish_failed'}[claim[4]]
        result = resume_publication(prior,owner,fields[4],hold,'current',
            now=fields[8]//10000000,expires_at=fields[9]//10000000,crash_after_audit=True,attempt_authority=attempt_authority,c2_authority=c2_authority,
            predecessor_bytes=predecessor)
        assert result['outcome'] == 'orphaned-success' and result['command_executions'] == 0
        return owned_resume_preparation(result,owner)
    @property
    def preparation(self):
        row = self.backend.read(self.preparation_key(self.owner),self.owner,optional=True)
        return None if row is None else row[0]
    def predecessor_readback(self, raw):
        fields = decode_record(raw,'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
        prior = codec['continuation_image'](self.origin_fields()[6]); prior['orphans'] = {}
        completion = fields[13]//10000000
        original=self.imported('predecessor')
        if raw == original: return True
        original_utc=decode_record(original,'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])[13]//10000000
        expected=decode_record(publication_state_bytes(reconcile_tombstones(prior,completion),completion),
            'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
        return completion >= original_utc and fields==expected
    def intended(self, kind, completion_time=None):
        if kind == 'claim': return self.origin_fields()[5]
        fields = self.origin_fields(); prior = codec['continuation_image'](fields[6]); now = fields[8]//10000000
        for name in imported_fields: prior[name] = read_image(self.imported(name))
        prior['orphans'] = {}
        prepared = self.reconstruct(self.owner)
        reconstruction = decode_record(prepared,'HX-EV-RESUME-PREPARATION-1',codec['extra_schemas']['D45-preparation'])
        successor = codec['continuation_image'](reconstruction[6])
        successor['window_claim_bytes'] = read_image(reconstruction[10])['window']
        claim = decode_record(fields[5],'HX-EV-PUBLICATION-RESUME-3',codec['schemas']['D45-request'])
        closure = None
        if claim[4] in {'retry-exhausted','drain-limit-and-retry-exhausted'}:
            closure = publication_closure_bytes(prior,self.imported('attempts'),self.imported('broker'),now,self.c2_authority(self.imported('attempts')))
        if kind == 'fence': return self.imported('broker')
        if kind == 'closure': assert closure is not None; return closure
        if kind == 'window': return successor['window_claim_bytes']
        if kind == 'resolution':
            assert claim[4] in {'drain-limit','drain-limit-and-retry-exhausted'}
            return R('HX-EV-PUBLICATION-DRAIN-LIMIT-RESOLUTION-1',8,U(self.tenant),claim[3],prior['hold_source'],
                U('resumed'),sha256(successor['window_claim_bytes']).digest() if claim[4] == 'drain-limit-and-retry-exhausted' else successor['invocations'][-1],N(successor['ordinal']),U('coordinator'),Q(fields[8]))
        if kind == 'audit':
            raw = R('HX-EV-PUBLICATION-RESUME-AUDIT-4',10,U(self.tenant),U(self.execution),N(successor['ordinal']),
                self.owner,fields[3],sha256(self.imported('predecessor')).digest(),
                O(None if closure is None else sha256(closure).digest()),N(successor['window']),N(successor['limit']),Q(fields[8]))
            assert sha256(raw).digest() == successor['live'][self.owner]['result']['audit_hash']
            return raw
        if kind == 'state':
            for name in ('roster','accepted','unresolved'): successor[name] = prior[name]
            if completion_time is None:
                existing = self.backend.read(self.artifact_key('state'),self.owner,optional=True)
                if existing is not None and not self.predecessor_readback(existing[0]):
                    completion_time = decode_record(existing[0],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])[13]//10000000
                else: completion_time = now if self.head() is None else self.head()[9]//10000000
            assert completion_time >= now
            successor = reconcile_tombstones(successor,completion_time)
            return publication_state_bytes(successor,completion_time)
        if kind == 'invocation':
            unresolved = prior['unresolved']
            rows = b''.join(codec['pack']('>I',p)+U(message)+sha256(body).digest() for p,message,body in sorted(unresolved))
            root = prior['legacy_root'] if claim[4] == 'legacy-publish-failed' else sha256(b'HX-EV-PUBLICATION-UNRESOLVED-1\0\x01'+codec['pack']('>I',len(unresolved))+rows).digest()
            return R('HX-EV-PUBLICATION-INVOCATION-1',9,U(self.tenant),U(self.execution),successor['window_claim'],
                N(successor['ordinal']),N(successor['limit']),self.owner,root,successor['invocations'][-1],Q(fields[8]))
        raise AssertionError(kind)
    def artifact_key(self, kind):
        claim = decode_record(self.origin_fields()[5],'HX-EV-PUBLICATION-RESUME-3',codec['schemas']['D45-request'])
        scope = claim[3]
        if kind == 'claim': return self.key('HX-EV-PUBLICATION-RESUME-CLAIM-KEY-1',U(self.tenant),U(self.execution),self.owner)
        if kind == 'resolution': return self.key('HX-EV-PUBLICATION-DRAIN-RESOLUTION-KEY-1',scope,claim[5])
        prior = codec['continuation_image'](self.origin_fields()[6])
        # Imported C5 fence fixture keeps its existing address; it is outside replacement K.
        if kind == 'fence': return 'fixture-c5-window-fence:'+codec['KD']('HX-EV-WINDOW-FENCE',scope,N(prior['window']))
        if kind == 'closure': return self.key('HX-EV-PUBLICATION-WINDOW-CLOSURE-KEY-1',scope,N(prior['window']))
        if kind == 'window': return self.key('HX-EV-PUBLICATION-WINDOW-KEY-1',scope,N(prior['window']+1))
        if kind == 'audit': return self.key('HX-EV-PUBLICATION-RESUME-AUDIT-KEY-2',U(self.tenant),U(self.execution),self.owner)
        if kind == 'state': return self.key('HX-EV-PUBLICATION-RESUME-STATE-KEY-1',U(self.tenant),U(self.execution))
        if kind == 'invocation':
            fields = decode_record(self.intended(kind),'HX-EV-PUBLICATION-INVOCATION-1',codec['extra_schemas']['D45-invocation'])
            return self.key('HX-EV-PUBLICATION-INVOCATION-KEY-1',U(self.tenant),U(self.execution),fields[7])
        raise AssertionError(kind)
    @property
    def artifacts(self):
        found = {}
        for kind,(address,digest,receipt,disposition) in self.manifest().items():
            if disposition == 'deleted': continue
            row = self.backend.read(address,self.owner,optional=True)
            if row is not None:
                if kind == 'state' and disposition == 'pending' and self.predecessor_readback(row[0]): continue
                assert digest == sha256(row[0]).digest()
                found['successor' if kind == 'state' else kind] = row[0]
        return found
    @property
    def cleanup(self): return {kind for kind,values in self.manifest().items() if values[3] == 'deleted'}
    @property
    def indexed(self):
        fields = decode_record(self.backend.read(self.inventory_key,self.owner)[0],'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index'])
        rows = codec['decode_rows'](fields[4],fields[3],['Q','U','U','B32'],[1024,1024,4096,1024])
        return any(row[1:3] == ('PublicationResumePreparationHold',self.execution+':'+self.owner.hex()) for row in rows)
    def inventory_membership(self, present):
        owner = self.owner; previous = self.backend.read(self.inventory_key,owner)
        fields = decode_record(previous[0],'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index'])
        all_rows = codec['decode_rows'](fields[4],fields[3],['Q','U','U','B32'],[1024,1024,4096,1024])
        subject = self.execution+':'+owner.hex()
        own = [row for row in all_rows if row[1:3] == ('PublicationResumePreparationHold',subject)]
        if bool(own) == present: return
        generation = checked_add(previous[1],1); assert generation is not None
        subject = self.execution+':'+owner.hex()
        entry = self.backend.read(self.entry_key(subject),owner)[0]
        all_rows = [row for row in all_rows if row[1:3] != ('PublicationResumePreparationHold',subject)]
        if present: all_rows.append((self.origin_fields()[8],'PublicationResumePreparationHold',subject,sha256(entry).digest()))
        all_rows.sort(key=lambda row:(row[0],row[1].encode(),row[2].encode()))
        rows = b''.join(Q(at)+U(hold)+U(name)+digest for at,hold,name,digest in all_rows)
        raw = R('HX-EV-HOLD-INDEX-2',8,U('tenant'),U(self.tenant),N(generation),N(len(all_rows)),B(rows),
            N(0),sha256(previous[0]).digest(),Q(self.origin_fields()[8]))
        decode_record(raw,'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index'])
        self.backend.write(self.inventory_key,raw,owner,generation,sha256(previous[0]).digest())
    @property
    def metadata_charge(self):
        fields = self.charge_fields('metadata')
        assert fields[10] == 'active' and fields[7] == 2*MiB
        return fields[7]
    def charge_fields(self, kind):
        row = self.backend.read(self.charge_key(self.owner,kind),self.owner)
        fields = decode_record(row[0],'HX-EV-PUBLICATION-CHARGE-2',self.charge_schema)
        assert fields[:3] == ('deployment-a','tenant',self.tenant)
        assert fields[3] == sha256(U(kind)+self.owner).digest() and fields[9] == row[1]
        return fields
    @property
    def swap(self):
        old,new = self.charge_fields('old'),self.charge_fields('new')
        prior = codec['continuation_image'](self.origin_fields()[6])
        assert old[10] in {'active','released'} and new[10] in {'staged','active','released'}
        assert new[12] == self.owner
        assert old[7] == (0 if new[10] == 'active' else prior['active_charge'])
        assert new[7] == (0 if new[10] == 'released' else prior['next_charge'])
        view = ChargeSwap(old[7],prior['charge_ceiling'])
        view.generation = new[9]
        view.owner = self.owner if new[10] != 'released' else None
        view.staged = new[7] if new[10] == 'staged' else 0
        view.finalized = new[10] == 'active'
        view.active = new[7] if view.finalized else old[7]
        amounts = []
        for kind in ('tenant','tenant-pool','deployment'):
            row = self.backend.read(self.counter_key(kind),self.owner)
            fields = decode_record(row[0],'HX-EV-PUBLICATION-COUNTER-1',self.counter_schema)
            assert fields[:3] == ('deployment-a',kind,self.tenant if kind == 'tenant' else kind)
            assert fields[5] == row[1] and fields[4] == 1+(old[7] > 0)+(new[7] > 0)
            amounts.append(fields[3]-self.metadata_charge)
        assert amounts[0] == amounts[1] == amounts[2] == old[7]+new[7]
        view.used = amounts[0]
        return view
    def save_charges(self, owner, old_amount, new_amount, new_state, used, initial=False):
        assert used == old_amount+new_amount
        pending = []
        for kind,amount,state in [('metadata',2*MiB,'active'),('old',old_amount,'active' if old_amount else 'released'),('new',new_amount,new_state)]:
            key = self.charge_key(owner,kind); previous = self.backend.read(key,owner,optional=True)
            if previous is not None:
                fields = decode_record(previous[0],'HX-EV-PUBLICATION-CHARGE-2',self.charge_schema)
                if fields[7] == amount and fields[10] == state: continue
            generation = 1 if previous is None else checked_add(previous[1],1)
            assert generation is not None
            raw = R('HX-EV-PUBLICATION-CHARGE-2',15,U('deployment-a'),U('tenant'),U(self.tenant),sha256(U(kind)+owner).digest(),
                U('resume-window' if kind == 'new' or kind == 'old' and decode_record(self.origin_fields()[5],'HX-EV-PUBLICATION-RESUME-3',codec['schemas']['D45-request'])[4] == 'legacy-publish-failed' else 'side-record'),N(amount),N(0),N(amount),N(1),N(generation),U(state),
                bytes(32) if previous is None else sha256(previous[0]).digest(),O(owner if kind == 'new' else None),Q(self.origin_fields()[8]),N(int(kind == 'new')))
            decode_record(raw,'HX-EV-PUBLICATION-CHARGE-2',self.charge_schema)
            pending.append((key,raw,generation,None if previous is None else sha256(previous[0]).digest()))
        for kind in ('tenant','tenant-pool','deployment'):
            key = self.counter_key(kind); previous = self.backend.read(key,owner,optional=True)
            if previous is not None:
                fields = decode_record(previous[0],'HX-EV-PUBLICATION-COUNTER-1',self.counter_schema)
                if fields[3] == used+2*MiB: continue
            generation = 1 if previous is None else checked_add(previous[1],1)
            assert generation is not None
            raw = R('HX-EV-PUBLICATION-COUNTER-1',8,U('deployment-a'),U(kind),U(self.tenant if kind == 'tenant' else kind),
                N(used+2*MiB),N(1+(old_amount > 0)+(new_amount > 0)),N(generation),bytes(32) if previous is None else sha256(previous[0]).digest(),Q(self.origin_fields()[8]))
            decode_record(raw,'HX-EV-PUBLICATION-COUNTER-1',self.counter_schema)
            pending.append((key,raw,generation,None if previous is None else sha256(previous[0]).digest()))
        # One provider-model ledger transaction: every read/CAS/generation is
        # preflighted before any row changes. No ChargeSwap object is persisted.
        for key,raw,generation,expected in pending:
            existing = self.backend.read(key,owner,optional=True)
            assert (existing is None and expected is None) or (existing is not None and sha256(existing[0]).digest() == expected)
        for key,raw,generation,expected in pending: self.backend.write(key,raw,owner,generation,expected)
    def validate(self):
        owner = self.owner; self.origin_fields(); self.metadata_charge; self.swap; self.reconstruct(owner)
        head = self.head()
        if head is None:
            assert self.preparation is None
            eligible = decode_record(self.origin_fields()[5],'HX-EV-PUBLICATION-RESUME-3',codec['schemas']['D45-request'])[4]
            kinds = tuple(kind for kind in ('claim','resolution','fence','closure','window','audit','state','invocation')
                if not (eligible in {'drain-limit','legacy-publish-failed'} and kind in {'fence','closure','window'}
                        or eligible in {'retry-exhausted','legacy-publish-failed'} and kind == 'resolution'))
            assert all((lambda row: row is None or kind == 'state' and self.predecessor_readback(row[0]))(
                self.backend.read(self.artifact_key(kind),owner,optional=True)) for kind in kinds)
            return
        rows = self.manifest()
        if 'window' in rows and rows['window'][3]=='present':
            claim=self.backend.read(rows['window'][0],self.owner)[0]
            prior=codec['continuation_image'](self.origin_fields()[6])
            for name in imported_fields: prior[name]=read_image(self.imported(name))
            admission=codec['window_admission_bytes'](prior['unresolved'])
            progress=codec['window_progress_bytes'](admission,prior['accepted'],prior['unresolved'],prior['accepted'])
            for kind,value in (('window_admission',admission),('window_progress',progress)):
                assert self.backend.read(self.window_source_key(sha256(claim).digest(),kind),bytes(32))[0]==value
        for kind,(address,digest,receipt,disposition) in rows.items():
            assert address == self.artifact_key(kind) and digest == sha256(self.intended(kind)).digest()
            actual = self.backend.read(address,owner,optional=True)
            if disposition == 'present':
                if actual is None:
                    assert head[5] == 'cleanup' and self.backend.deletions.get(address) == (
                        owner,digest,self.backend.deletion_receipt(address,owner,digest))
                else:
                    assert actual[2] == receipt and sha256(actual[0]).digest() == digest
                    if kind == 'claim': assert self.backend.read_signed(address,owner) == (actual[0],self.origin_fields()[10])
            elif disposition == 'deleted':
                assert actual is None and receipt == sha256(b'provider-deletion-readback:'+U(address)+owner+digest).digest()
            elif actual is not None:
                assert sha256(actual[0]).digest() == digest or kind == 'state' and self.predecessor_readback(actual[0])
        if head[4] is not None:
            assert self.preparation is not None and sha256(self.preparation).digest() == head[4]
            assert self.preparation == self.reconstruct(owner)
        elif self.preparation is not None:
            assert self.preparation == self.reconstruct(owner)  # write survived before head readback
    def turn(self, crash_after=None, now=1000, _finalize_staged=False):
        try:
            self.validate()
            # Preflight every provider read before progress or ledger mutation.
            eligible = decode_record(self.origin_fields()[5],'HX-EV-PUBLICATION-RESUME-3',codec['schemas']['D45-request'])[4]
            kinds = tuple(kind for kind in ('claim','resolution','fence','closure','window','audit','state','invocation')
                if not (eligible in {'drain-limit','legacy-publish-failed'} and kind in {'fence','closure','window'}
                        or eligible in {'retry-exhausted','legacy-publish-failed'} and kind == 'resolution'))
            for kind in kinds: self.backend.read(self.artifact_key(kind),self.owner,optional=True)
            self.backend.read(self.preparation_key(self.owner),self.owner,optional=True)
            if self.head() is not None and self.head()[5] == 'cleanup': return 'cleanup-hold'
            if now*10000000 >= self.origin_fields()[9] and not any(
                    name in self.artifacts for name in ('fence','closure','audit','successor')):
                return 'resume_request_expired'
            if self.head() is None:
                self.progress(self.owner,'admitted',{},None)
                if crash_after == 'progress': return 'interrupted'
            view = self.swap
            if not view.staged and not view.finalized:
                amount = codec['continuation_image'](self.origin_fields()[6])['next_charge']
                assert view.stage(amount,self.owner)
                self.inventory_membership(True)
                self.save_charges(self.owner,view.old,view.staged,'staged',view.used)
            for name in self.stages:
                eligible = decode_record(self.origin_fields()[5],'HX-EV-PUBLICATION-RESUME-3',codec['schemas']['D45-request'])[4]
                if (eligible in {'drain-limit','legacy-publish-failed'} and name in {'fence','closure','window'}
                        or eligible in {'retry-exhausted','legacy-publish-failed'} and name == 'resolution'): continue
                if name == 'reconstruction':
                    prepared = self.reconstruct(self.owner)
                    self.backend.write(self.preparation_key(self.owner),prepared,self.owner,create_once=True)
                    if crash_after == 'reconstruction-write': return 'interrupted'
                    if self.head()[4] is None: self.progress(self.owner,'writing',self.manifest(),sha256(prepared).digest())
                elif name == 'finalize':
                    if not _finalize_staged:
                        staged_final=deepcopy(self)
                        outcome=staged_final.turn(crash_after,now,_finalize_staged=True)
                        if outcome in {'completed','interrupted'}:
                            self.backend.rows,self.backend.receipts,self.backend.deletions,self.backend.signatures=staged_final.backend.rows,staged_final.backend.receipts,staged_final.backend.deletions,staged_final.backend.signatures
                        return outcome
                    # Finalization and current-time progress readback are one
                    # staged provider-model turn. Exhausted progress authority
                    # cannot leave a released predecessor charge behind.
                    staged_final=deepcopy(self)
                    view = staged_final.swap
                    if not view.finalized:
                        assert view.recover(self.owner,presence(self.owner,'successor','present',view.generation),presence(self.owner,'audit','present',view.generation))
                        staged_final.save_charges(self.owner,view.old,view.active,'active',view.used)
                    staged_final.reconcile_successor(now)
                    self.backend.rows,self.backend.receipts,self.backend.deletions,self.backend.signatures=staged_final.backend.rows,staged_final.backend.receipts,staged_final.backend.deletions,staged_final.backend.signatures
                else:
                    kind = self.aliases.get(name,name); key = self.artifact_key(kind)
                    rows = self.manifest()
                    # A persisted intent fixes its original completion UTC. A
                    # surviving write acknowledges those bytes before a later CAS.
                    raw = self.intended(kind,now if kind == 'state' and kind not in rows else None)
                    if kind in rows and rows[kind][3] == 'present':
                        if crash_after == name: return 'interrupted'
                        continue
                    if kind not in rows or rows[kind][3] == 'deleted':
                        rows[kind] = (key,sha256(raw).digest(),bytes(32),'pending')
                        self.progress(self.owner,'writing',rows,self.head()[4],now=now if kind == 'state' else None)
                        if crash_after == name+'-intent': return 'interrupted'
                    if kind == 'state':
                        current = self.backend.read(key,self.owner)
                        progress_head=self.backend.read(self.head_key,self.owner)
                        assert checked_add(progress_head[1],1) is not None, 'successor-progress-generation'
                        staged_store=deepcopy(self); staged=staged_store.backend
                        if current[0] != raw:
                            assert self.predecessor_readback(current[0])
                            assert now*10000000 >= decode_record(current[0],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])[13]
                            generation=checked_add(current[1],1); assert generation is not None
                            staged.write(key,raw,self.owner,generation,sha256(current[0]).digest())
                            assert staged.read(key,self.owner)[0]==raw
                            self.consume_active_hold(staged)
                        readback=staged.read(key,self.owner)
                        coupled_rows=staged_store.manifest(); coupled_rows[kind]=(key,sha256(raw).digest(),readback[2],'present')
                        staged_store.progress(self.owner,'audited',coupled_rows,staged_store.head()[4])
                        assert staged_store.manifest()[kind][1:3]==(sha256(raw).digest(),readback[2])
                        self.backend.rows,self.backend.receipts,self.backend.deletions,self.backend.signatures=staged.rows,staged.receipts,staged.deletions,staged.signatures
                    elif kind == 'window':
                        prior=codec['continuation_image'](self.origin_fields()[6])
                        for imported_name in imported_fields: prior[imported_name]=read_image(self.imported(imported_name))
                        staged=deepcopy(self.backend)
                        admission=codec['window_admission_bytes'](prior['unresolved'])
                        progress=codec['window_progress_bytes'](admission,prior['accepted'],prior['unresolved'],prior['accepted'])
                        staged.write(key,raw,self.owner,create_once=True)
                        for source,value in (('window_admission',admission),('window_progress',progress)):
                            address=self.window_source_key(sha256(raw).digest(),source)
                            staged.write(address,value,bytes(32),create_once=True)
                            assert staged.read(address,bytes(32))[0]==value
                        self.backend.rows,self.backend.receipts,self.backend.deletions,self.backend.signatures=staged.rows,staged.receipts,staged.deletions,staged.signatures
                    elif kind == 'claim': self.backend.write_signed(key,raw,self.origin_fields()[10],self.owner)
                    else: self.backend.write(key,raw,self.owner,create_once=True)
                    if crash_after == name+'-write': return 'interrupted'
                    readback = self.backend.read(key,self.owner)
                    rows = self.manifest(); rows[kind] = (key,sha256(readback[0]).digest(),readback[2],'present')
                    phase = 'audited' if kind in {'audit','state','invocation'} else 'writing'
                    if kind != 'state': self.progress(self.owner,phase,rows,self.head()[4])
                if crash_after == name: return 'interrupted'
            if self.head()[5] != 'completed': self.progress(self.owner,'completed',self.manifest(),self.head()[4])
            self.inventory_membership(False)
            return 'completed'
        except (AssertionError,KeyError,ValueError,TypeError,UnicodeError): return 'evidence-hold'
    def reconcile_successor(self, now):
        key = self.artifact_key('state'); current = self.backend.read(key,self.owner)
        fields = decode_record(current[0],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
        assert now*10000000 >= fields[13]
        raw = self.intended('state',now)
        if raw == current[0]: return
        # The shared CAS serializes the recorded success with permitted expiry
        # updates. State and manifest receipt advance as one provider-model turn.
        generation = checked_add(current[1],1); assert generation is not None
        progress_head=self.backend.read(self.head_key,self.owner)
        assert checked_add(progress_head[1],1) is not None, 'reconcile-progress-generation'
        staged_store=deepcopy(self)
        readback = staged_store.backend.write(key,raw,self.owner,generation,sha256(current[0]).digest())
        rows = staged_store.manifest(); rows['state'] = (key,sha256(raw).digest(),readback[2],'present')
        phase = 'completed' if staged_store.head()[5] == 'completed' else 'audited'
        staged_store.progress(self.owner,phase,rows,staged_store.head()[4],now=now)
        assert staged_store.backend.read(key,self.owner)[0]==raw and staged_store.manifest()['state'][2]==readback[2]
        self.backend.rows,self.backend.receipts,self.backend.deletions,self.backend.signatures=staged_store.backend.rows,staged_store.backend.receipts,staged_store.backend.deletions,staged_store.backend.signatures
    def rollback_artifacts(self, successor_receipt, audit_receipt, deletion_available=True, crash_after=None):
        try:
            self.validate()
            if (validate_presence(successor_receipt,self.owner,'successor',self.swap.generation) != 'absent'
                    or validate_presence(audit_receipt,self.owner,'audit',self.swap.generation) != 'absent'
                    or any(name in self.artifacts for name in ('fence','closure','audit','successor'))): return 'completion-required'
            for kind,(address,digest,receipt,disposition) in list(self.manifest().items()):
                if disposition == 'deleted': continue
                if not deletion_available: return 'cleanup-hold'
                if self.head()[5] != 'cleanup': self.progress(self.owner,'cleanup',self.manifest(),self.head()[4])
                actual = self.backend.read(address,self.owner,optional=True)
                deletion = (self.backend.delete(address,self.owner,digest) if actual is not None else
                            self.backend.deletion_receipt(address,self.owner,digest))
                if actual is None and disposition == 'present': assert address in self.backend.deletions
                if crash_after == kind+'-delete': return 'cleanup-hold'
                rows = self.manifest(); rows[kind] = (address,digest,deletion,'deleted')
                self.progress(self.owner,'cleanup',rows,self.head()[4])
                if crash_after == kind: return 'cleanup-hold'
            view = self.swap
            assert view.recover(self.owner,successor_receipt,audit_receipt)
            self.save_charges(self.owner,view.old,0,'released',view.used)
            self.progress(self.owner,'rolled-back',self.manifest(),self.head()[4])
            self.inventory_membership(False)
            return 'rolled-back'
        except (AssertionError,KeyError,ValueError,TypeError,UnicodeError): return 'evidence-hold'

    def rollback(self, successor_receipt, audit_receipt, deletion_available=True):
        # The final rollback transaction includes bounded tombstone compaction.
        # Historical cleanup-boundary probes call rollback_artifacts explicitly;
        # that intermediate phase is never the completed rollback exit.
        before=self.backend.snapshot()
        if not deletion_available: return 'cleanup-hold'
        staged=deepcopy(self)
        try:
            if staged.rollback_artifacts(successor_receipt,audit_receipt) != 'rolled-back': return 'cleanup-hold'
            compact=compact_rolled_back_preparation(staged)
        except (AssertionError,KeyError,ValueError,TypeError,UnicodeError):
            assert self.backend.snapshot() == before
            return 'cleanup-hold'
        self.backend.rows,self.backend.receipts,self.backend.deletions,self.backend.signatures=staged.backend.rows,staged.backend.receipts,staged.backend.deletions,staged.backend.signatures
        return 'rolled-back',compact

def identity_record_keys(backend, tenant, execution, identity):
    # Select only identity-addressed bodies. Window fences/closures and the
    # active quota lineages remain under their separate obligation lifecycles.
    domains={'HX-EV-RESUME-ORIGIN-1','HX-EV-RESUME-PREPARATION-1',
             'HX-EV-PUBLICATION-RESUME-3','HX-EV-PUBLICATION-RESUME-AUDIT-4',
             'HX-EV-PUBLICATION-INVOCATION-1','HX-EV-PUBLICATION-DRAIN-LIMIT-RESOLUTION-1'}
    keys=[]
    for key,raw in backend.rows.items():
        owner,generation,receipt=backend.receipts[key]
        if owner != identity: continue
        domain=raw.split(b'\0',1)[0].decode(errors='replace')
        if domain in domains or key.startswith('fixture-provider-import:'):
            backend.read(key,identity); keys.append(key)
    return keys

def add_unrelated_inventory(store):
    current=store.backend.read(store.inventory_key,store.owner)
    fields=list(decode_record(current[0],'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index']))
    unrelated=(10000000001,'HeldDelivery','unrelated',codec['H']('unrelated-entry'))
    rows=codec['decode_rows'](fields[4],fields[3],['Q','U','U','B32'],[1024,1024,4096,1024])+[unrelated]
    rows.sort(key=lambda row:(row[0],row[1].encode(),row[2].encode()))
    fields[2],fields[3],fields[4],fields[6]=current[1]+1,len(rows),b''.join(Q(at)+U(hold)+U(subject)+digest for at,hold,subject,digest in rows),sha256(current[0]).digest()
    raw=R('HX-EV-HOLD-INDEX-2',8,*(codec['encode_typed'](k,v) for k,v in zip(codec['schemas']['D37-index'],fields)))
    store.backend.write(store.inventory_key,raw,store.owner,current[1]+1,sha256(current[0]).digest())
    return unrelated

def inventory_rows(store):
    fields=decode_record(store.backend.rows[store.inventory_key],'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index'])
    return codec['decode_rows'](fields[4],fields[3],['Q','U','U','B32'],[1024,1024,4096,1024])

def compact_rolled_back_preparation(store):
    store.validate(); assert store.head()[5] == 'rolled-back' and not store.artifacts and not store.indexed
    owner=store.owner; fields=store.origin_fields(); prior=codec['continuation_image'](fields[6]); prior['orphans']={}
    for name in imported_fields: prior[name]=read_image(store.imported(name))
    key=store.artifact_key('state'); current=store.backend.read(key,owner)
    assert store.predecessor_readback(current[0]), 'rollback-reconciled-current-head'
    head=list(decode_record(current[0],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state']))
    assert head[0:3]==[store.tenant,store.execution,bytes(32) if 'legacy_root' in prior else codec['H']('scope')]
    live_rows=codec['decode_rows'](head[11][4:],int.from_bytes(head[11][:4],'big'),['B32','B32','N','B32','N','N','Q'],count_ceiling=64)
    tomb_rows=codec['decode_rows'](head[12][4:],int.from_bytes(head[12][:4],'big'),['B32','B32','Q','Q'],count_ceiling=64)
    assert all(row[0]!=owner for row in live_rows), 'rollback-current-live-identity'
    current_utc=head[13]//10000000
    expiry=fields[9]//10000000; deadline=expiry+30*86400
    assert all(row[0]!=owner or row==(owner,fields[3],expiry*10000000,deadline*10000000) for row in tomb_rows)
    tomb_rows=[row for row in tomb_rows if row[0]!=owner]
    if current_utc<deadline: tomb_rows.append((owner,fields[3],expiry*10000000,deadline*10000000))
    tomb_rows.sort(key=lambda row:row[0])
    assert len(live_rows)+len(tomb_rows)<=64 and current[1]<U64_MAX, 'rollback-current-capacity-generation'
    head[12]=codec['pack']('>I',len(tomb_rows))+b''.join(identity+carrier+Q(expires)+Q(delete) for identity,carrier,expires,delete in tomb_rows)
    raw=R('HX-EV-PUBLICATION-RESUME-STATE-3',14,*(codec['encode_typed'](kind,value) for kind,value in zip(codec['schemas']['D45-state'],head)))
    owners=invocation_owners(prior)
    prior['live']={identity:{'carrier_hash':carrier,'result':{'ordinal':ordinal,'audit_hash':audit,'window':window,'limit':limit},
        'response':json.dumps({'resumeHandle':prior['handle'],'resumeOrdinal':ordinal,'window':window,
            'drainLimit':limit,'auditRecordHash':audit.hex()},sort_keys=True,separators=(',',':')).encode(),
        'expires_at':expires//10000000} for identity,carrier,ordinal,audit,window,limit,expires in live_rows}
    prior['tombstones']={identity:{'carrier_hash':carrier,'expires_at':expires//10000000,'delete_after':delete//10000000} for identity,carrier,expires,delete in tomb_rows}
    retained=set(prior['live'])|set(prior['tombstones'])
    pairs=tuple((identity,value) for identity,value in zip(owners,prior['invocations']) if identity in retained)
    prior['invocations']=tuple(value for identity,value in pairs)
    prior['invocation_owners']=tuple(identity for identity,value in pairs)
    keys=identity_record_keys(store.backend,store.tenant,store.execution,owner)
    keys.append(store.head_key); keys.append(store.entry_key(store.execution+':'+owner.hex()))
    # One authenticated provider-model transaction: no full-origin slot remains.
    staged=deepcopy(store.backend)
    for address in keys:
        if address in staged.rows: staged.delete(address,owner,sha256(staged.rows[address]).digest())
        staged.deletions.pop(address,None)
    for address,(recorded_owner,digest,receipt) in list(staged.deletions.items()):
        if recorded_owner == owner: del staged.deletions[address]
    staged.write(key,raw,owner,current[1]+1,sha256(current[0]).digest())
    store.backend.rows,store.backend.receipts,store.backend.deletions,store.backend.signatures=staged.rows,staged.receipts,staged.deletions,staged.signatures
    assert store.origin_key(owner) not in store.backend.rows and store.head_key not in store.backend.rows
    return prior

def admit_after_compaction(backend, owner, carrier, prior, now=1000, expiry=1900):
    # Atomically transfer the existing execution's metadata/old charge and
    # shared counter/index heads into a newly admitted request. Every input is
    # a provider byte readback; the temporary provider is only the proposed
    # transaction image, and cannot commit on refusal.
    snapshot=backend.snapshot()
    for raw in backend.rows.values():
        if raw.startswith(b'HX-EV-RESUME-ORIGIN-1\0'):
            fields=decode_record(raw,'HX-EV-RESUME-ORIGIN-1',codec['extra_schemas']['D45-origin'])
            if fields[:2] == (prior['tenant'],'op'): return None
    state_key=codec['K']('HX-EV-PUBLICATION-RESUME-STATE-KEY-1',U(prior['tenant']),U('op'))
    old_owner=backend.receipts[state_key][0]
    current=backend.read(state_key,old_owner)
    old_fields=decode_record(current[0],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
    assert current[0] == publication_state_bytes(prior,old_fields[13]//10000000)
    result=resume_publication(prior,owner,carrier,'publication_drain_limit_hold','current',now=now,expires_at=expiry,crash_after_audit=True,
        predecessor_bytes=current[0])
    if result['outcome'] != 'orphaned-success': assert backend.snapshot() == snapshot; return None
    proposed=PreparationStore(owner,carrier,owned_resume_preparation(result,owner),prior,now=now,expiry=expiry,
        predecessor_utc=old_fields[13]//10000000)
    staged=deepcopy(backend)
    old_charge_keys=[]; old_active=0
    for kind in ('metadata','old','new'):
        key=proposed.charge_key(old_owner,kind); readback=backend.read(key,old_owner)
        fields=decode_record(readback[0],'HX-EV-PUBLICATION-CHARGE-2',codec['schemas']['D29-charge'])
        if kind == 'metadata': assert fields[7] == 2*MiB and fields[10] == 'active'
        elif fields[10] == 'active': old_active += fields[7]
        else: assert fields[10] == 'released' and fields[7] == 0
        old_charge_keys.append(key)
    assert old_active == prior['active_charge']
    for key in old_charge_keys: del staged.rows[key]; del staged.receipts[key]; staged.deletions.pop(key,None)
    for key,raw in proposed.backend.rows.items():
        generation=proposed.backend.receipts[key][1]
        if key in staged.rows:
            existing_owner=staged.receipts[key][0]; existing=backend.read(key,existing_owner)
            if key.startswith(('fixture-window-admission:','fixture-window-progress:')):
                assert existing[0]==raw and existing_owner==bytes(32)
                continue
            generation=checked_add(existing[1],1); assert generation is not None
            domain=raw.split(b'\0',1)[0].decode()
            if domain == 'HX-EV-PUBLICATION-COUNTER-1':
                fields=list(decode_record(raw,domain,codec['schemas']['D29-counter']))
                previous_fields=decode_record(existing[0],domain,codec['schemas']['D29-counter'])
                assert previous_fields[3] == 2*MiB+prior['active_charge']
                fields[5],fields[6]=generation,sha256(existing[0]).digest()
                raw=R(domain,8,*(codec['encode_typed'](k,v) for k,v in zip(codec['schemas']['D29-counter'],fields)))
            elif domain == 'HX-EV-HOLD-INDEX-2':
                fields=list(decode_record(raw,domain,codec['schemas']['D37-index']))
                previous_fields=decode_record(existing[0],domain,codec['schemas']['D37-index'])
                previous_rows=codec['decode_rows'](previous_fields[4],previous_fields[3],['Q','U','U','B32'],[1024,1024,4096,1024])
                new_rows=codec['decode_rows'](fields[4],fields[3],['Q','U','U','B32'],[1024,1024,4096,1024])
                rows=sorted({(r[1],r[2]):r for r in previous_rows+new_rows}.values(),key=lambda row:(row[0],row[1].encode(),row[2].encode()))
                fields[2],fields[3],fields[4],fields[6]=generation,len(rows),b''.join(Q(at)+U(hold)+U(subject)+digest for at,hold,subject,digest in rows),sha256(existing[0]).digest()
                raw=R(domain,8,*(codec['encode_typed'](k,v) for k,v in zip(codec['schemas']['D37-index'],fields)))
            else: assert domain in {'HX-EV-PUBLICATION-RESUME-STATE-3','HX-EV-HOLD-ENTRY-2'}
        installed_owner=bytes(32) if key.startswith(('fixture-window-admission:','fixture-window-progress:')) else owner
        staged.rows[key]=raw; staged.receipts[key]=(installed_owner,generation,staged.receipt(key,raw,installed_owner,generation)); staged.deletions.pop(key,None)
    proposed.backend=staged; proposed.validate()
    backend.rows,backend.receipts,backend.deletions,backend.signatures=staged.rows,staged.receipts,staged.deletions,staged.signatures
    proposed.backend=backend
    assert proposed.swap.used == prior['active_charge']+prior['next_charge']
    return proposed

def reclaim_resume_identity(backend, tenant, execution, identity, now, available=True):
    if not available: return False
    state_key=codec['K']('HX-EV-PUBLICATION-RESUME-STATE-KEY-1',U(tenant),U(execution))
    owner=backend.receipts[state_key][0]; current=backend.read(state_key,owner)
    fields=list(decode_record(current[0],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state']))
    expired=codec['decode_rows'](fields[12][4:],int.from_bytes(fields[12][:4],'big'),['B32','B32','Q','Q'])
    row=next((row for row in expired if row[0] == identity),None)
    if row is None or row[3] > now*10000000: return False
    keys=identity_record_keys(backend,tenant,execution,identity)
    entry=codec['K']('HX-EV-HOLD-ENTRY-KEY-1',U('tenant'),U(tenant),U('PublicationResumePreparationHold'),U(execution+':'+identity.hex()))
    if entry in backend.rows:
        backend.read(entry,identity); keys.append(entry)
    head=codec['K']('HX-EV-RESUME-PREPARATION-HEAD-KEY-1',U(tenant),U(execution))
    if head in backend.rows and backend.receipts[head][0] == identity:
        head_fields=decode_record(backend.read(head,identity)[0],'HX-EV-RESUME-PREPARATION-HEAD-1',codec['extra_schemas']['D45-preparation-head'])
        assert head_fields[5] in {'completed','rolled-back'}; keys.append(head)
    expired=[row for row in expired if row[0] != identity]
    fields[12]=codec['pack']('>I',len(expired))+b''.join(i+c+Q(expiry)+Q(deadline) for i,c,expiry,deadline in expired)
    fields[13]=now*10000000
    raw=R('HX-EV-PUBLICATION-RESUME-STATE-3',14,*(codec['encode_typed'](k,v) for k,v in zip(codec['schemas']['D45-state'],fields)))
    staged=deepcopy(backend)
    origin_key=codec['K']('HX-EV-RESUME-ORIGIN-KEY-1',U(tenant),U(execution),identity)
    if origin_key in staged.rows:
        origin=decode_record(staged.read(origin_key,identity)[0],'HX-EV-RESUME-ORIGIN-1',codec['extra_schemas']['D45-origin'])
        prior=codec['continuation_image'](origin[6])
        if prior['window_claim']!=fields[7] and prior['window_claim']!=bytes(32):
            for kind in ('window_admission','window_progress'):
                address='fixture-window-'+kind.removeprefix('window_')+':'+prior['window_claim'].hex()
                source=staged.read(address,bytes(32))[0]
                staged.delete(address,bytes(32),sha256(source).digest()); staged.deletions.pop(address,None)
    for key in keys:
        staged.delete(key,identity,sha256(staged.rows[key]).digest()); staged.deletions.pop(key,None)
    # Native readback/deletion receipts for these identity keys are reclaimed too.
    for key,(recorded_owner,digest,receipt) in list(staged.deletions.items()):
        if recorded_owner == identity and key in keys: del staged.deletions[key]
    staged.write(state_key,raw,owner,current[1]+1,sha256(current[0]).digest())
    backend.rows,backend.receipts,backend.deletions,backend.signatures=staged.rows,staged.receipts,staged.deletions,staged.signatures
    assert all(key not in backend.rows and key not in backend.receipts and key not in backend.deletions for key in keys)
    return True

def compact_successful_origin(store):
    store.validate(); assert store.head()[5] == 'completed'
    owner=store.owner; key=store.artifact_key('claim')
    original=store.backend.read_signed(key,owner)
    assert original == (store.origin_fields()[5],store.origin_fields()[10])
    removable=[store.origin_key(owner),store.preparation_key(owner),store.head_key,store.entry_key(store.execution+':'+owner.hex())]
    removable += [address for address in store.backend.rows if address.startswith('fixture-provider-import:') and store.backend.receipts[address][0] == owner]
    staged=deepcopy(store.backend)
    prior=codec['continuation_image'](store.origin_fields()[6])
    current=decode_record(store.artifacts['successor'],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
    if prior['window_claim']!=current[7] and prior['window_claim']!=bytes(32):
        for kind in ('window_admission','window_progress'):
            key_to_delete=store.window_source_key(prior['window_claim'],kind)
            raw=staged.read(key_to_delete,bytes(32))[0]
            staged.delete(key_to_delete,bytes(32),sha256(raw).digest()); staged.deletions.pop(key_to_delete,None)
    for address in removable:
        current=staged.read(address,owner); staged.delete(address,owner,sha256(current[0]).digest()); staged.deletions.pop(address,None)
    assert staged.read_signed(key,owner) == original
    store.backend.rows,store.backend.receipts,store.backend.deletions,store.backend.signatures=staged.rows,staged.receipts,staged.deletions,staged.signatures
    return key,original

def restart_preparation(store):
    # Only the external provider's durable byte rows/receipts survive. The new
    # process receives an inventory-derived execution locator, no cached state.
    restored = PreparationStore.__new__(PreparationStore)
    restored.backend = store.backend
    restored.tenant,restored.execution = store.tenant,store.execution
    try: restored.validate()
    except (AssertionError,KeyError,ValueError,TypeError,UnicodeError): return None
    return restored

def verify_eligible_resume_matrix():
    committed = ((1, 'message-1', b'accepted'), (2, 'message-2', b'unresolved-a'),
                 (3, 'message-3', b'unresolved-b'))
    expected = (('message-2', b'unresolved-a'), ('message-3', b'unresolved-b'))
    base = {'roster':committed, 'accepted':(committed[0],), 'unresolved':committed[1:],
            'ordinal':1, 'window':7, 'closed':2, 'limit':16,
            'tenant':'t','handle':'h','hold_source':sha256(b'limit-hash').digest(), 'active_charge':300, 'next_charge':400,
            'charge_ceiling':1000, 'live':{}, 'tombstones':{}, 'orphans':{},
            'audits':0, 'invocations':(), 'window_claim_bytes':existing_window_bytes('t',committed),
            'window_claim':sha256(existing_window_bytes('t',committed)).digest(),'window_admission':codec['window_admission_bytes'](committed[1:])}
    base['window_progress']=codec['window_progress_bytes'](base['window_admission'],base['accepted'],base['unresolved'])
    request_id, carrier = request(b'request-1')
    exhausted = resume_publication(base, request_id, carrier,
                                   'publication_retry_exhausted_hold', 'current')
    drained = resume_publication(base, request_id, carrier,
                                 'publication_drain_limit_hold', 'current')
    assert exhausted['outcome'] == 'resumed' and exhausted['rearmed'] == expected
    for bad_id,bad_carrier in [request(b'new-source',source=bytes(32)),request(b'changed-source',source=codec['H']('different-authenticated-source')),request(b'other-tenant',tenant='other'),
                              request(b'other-handle',handle='other')]:
        rejected = resume_publication(base,bad_id,bad_carrier,'publication_retry_exhausted_hold','current')
        assert rejected['outcome'] == 'resume_hold_changed' and rejected['state'] == base
    assert resume_publication(base,bytes(32),carrier,'publication_retry_exhausted_hold','current')['outcome'] == 'resume_request_conflict'
    assert exhausted['accepted_unchanged'] == (committed[0],) and exhausted['command_executions'] == 0
    assert exhausted['state']['window'] == 8 and exhausted['state']['closed'] == 3
    assert drained['rearmed'] == expected and drained['command_executions'] == 0
    assert drained['state']['window'] == base['window'] and drained['state']['limit'] == 24
    assert drained['state']['closed'] == base['closed']
    assert drained['state']['window_claim'] == base['window_claim']
    assert drained['state']['window_claim_bytes'] == base['window_claim_bytes']
    assert drained['state']['roster'] == base['roster'] and drained['state']['accepted'] == base['accepted']
    assert drained['state']['unresolved'] == base['unresolved']
    assert drained['resolution'][0] == base['hold_source']
    assert drained['resolution'][1] == drained['state']['invocations'][-1]
    assert drained['resolution'][2] == drained['state']['invocations'][-1]
    assert drained['resolution'][3:] == (16,24)
    expected_rows = b''.join(codec['pack']('>I',row[0])+U(row[1])+sha256(row[2]).digest() for row in base['unresolved'])
    expected_root = sha256(b'HX-EV-PUBLICATION-UNRESOLVED-1\0\x01'+codec['pack']('>I',2)+expected_rows).digest()
    assert drained['state']['invocations'][-1] == sha256(b'HX-EV-PUBLICATION-INVOCATION-1\0\x01'+base['window_claim']+N(2)+N(24)+request_id+expected_root).digest()
    for evidence, expected_outcome in [('stale','resume_hold_changed'), ('unavailable','resume_evidence_hold')]:
        result = resume_publication(base, request_id, carrier,
                                    'publication_retry_exhausted_hold', evidence)
        assert result['outcome'] == expected_outcome and result['state'] == base
    for accepted, unresolved in [
        ((committed[0],), (committed[1],)),
        ((committed[0],committed[2]), (committed[1],committed[1])),
        ((committed[0],committed[1]), (committed[1],committed[2])),
        ((committed[0],), ((2,'message-3',b'unresolved-a'), committed[2])),
    ]:
        corrupt = deepcopy(base); corrupt['accepted'] = accepted; corrupt['unresolved'] = unresolved
        assert resume_publication(corrupt, request_id, carrier,
                                  'publication_retry_exhausted_hold', 'current')['outcome'] == 'resume_evidence_hold'
    live_retry = exhausted['state']
    live_snapshot = deepcopy(live_retry)
    retry = resume_publication(live_retry, request_id, carrier,
                               'publication_retry_exhausted_hold', 'current')
    assert retry['outcome'] == 'exact-retry' and retry['response'] == exhausted['response']
    assert retry['state'] == live_retry
    assert live_retry == live_snapshot
    for changed_id,changed_carrier in [request(b'request-1',reason=b'changed'),
                                      request(b'request-1',source=sha256(b'changed-source').digest())]:
        assert changed_id == request_id and changed_carrier != carrier
        conflict_result = resume_publication(live_retry, changed_id, changed_carrier,
                                            'publication_retry_exhausted_hold','current')
        assert conflict_result['outcome'] == 'resume_request_conflict' and conflict_result['state'] == live_retry
    crashed = resume_publication(base, request_id, carrier,
                                 'publication_retry_exhausted_hold','current',crash_after_audit=True)
    orphan = crashed['state']; orphan_snapshot = deepcopy(orphan)
    assert orphan['ordinal'] == 1 and orphan['used_charge'] == 700 and orphan['audits'] == 1
    assert orphan['invocations'] == ()
    recovered = resume_publication(orphan,request_id,carrier,'publication_retry_exhausted_hold','current')
    assert recovered['outcome'] == 'orphan-audit-retry' and recovered['response'] == exhausted['response']
    assert recovered['state'] == exhausted['state'] and recovered['state']['ordinal'] == 2
    assert recovered['state']['active_charge'] == 400 and recovered['state']['audits'] == 1
    assert len(recovered['state']['invocations']) == 1 and orphan == orphan_snapshot
    for corruption in ('receipt','owner','hold_source','roster'):
        invalid = deepcopy(orphan)
        if corruption in {'receipt','owner'}: invalid['orphans'][request_id][corruption] = bytes(32)
        elif corruption == 'hold_source': invalid['hold_source'] = bytes(32)
        else: invalid['roster'] = ()
        rejected = resume_publication(invalid,request_id,carrier,'publication_retry_exhausted_hold','current')
        assert rejected['outcome'] in {'resume_evidence_hold','resume_hold_changed'} and rejected['state'] == invalid
    altered_prior = deepcopy(orphan)
    altered_prior['orphans'][request_id]['preparation'] = altered_prior['orphans'][request_id]['preparation'][:-1]
    prior_rejection = resume_publication(altered_prior,request_id,carrier,'publication_retry_exhausted_hold','current')
    assert prior_rejection['outcome'] == 'resume_evidence_hold' and prior_rejection['state'] == altered_prior
    restarted_orphan = read_image(image_bytes(orphan))
    assert 'prior' not in restarted_orphan['orphans'][request_id] and 'successor' not in restarted_orphan['orphans'][request_id]
    assert resume_publication(restarted_orphan,request_id,carrier,'publication_retry_exhausted_hold','current')['state'] == exhausted['state']
    for damage in ('missing-own-row','charge-mismatch'):
        damaged = deepcopy(orphan); row=damaged['orphans'][request_id]
        fields=list(decode_record(row['preparation'],'HX-EV-RESUME-PREPARATION-1',codec['extra_schemas']['D45-preparation']))
        successor=read_image(fields[6])
        if damage == 'missing-own-row': successor['live'].pop(request_id)
        else: row['staged_charge'] += 1
        fields[6]=image_bytes(successor)
        row['preparation']=R('HX-EV-RESUME-PREPARATION-1',12,*(codec['encode_typed'](kind,value) for kind,value in zip(codec['extra_schemas']['D45-preparation'],fields)))
        row['receipt']=state_hash({k:v for k,v in row.items() if k != 'receipt'})
        result=resume_publication(damaged,request_id,carrier,'publication_retry_exhausted_hold','current')
        assert result['outcome'] == 'resume_evidence_hold' and result['state'] == damaged
    combined = resume_publication(base,request_id,carrier,'publication_drain_limit_and_retry_exhausted_hold','current',crash_after_audit=True)
    combined_store=PreparationStore(request_id,carrier,owned_resume_preparation(combined,request_id),base,eligible='drain-limit-and-retry-exhausted')
    assert combined_store.turn() == 'completed'
    resolution=decode_record(combined_store.artifacts['resolution'],'HX-EV-PUBLICATION-DRAIN-LIMIT-RESOLUTION-1',codec['schemas']['D14-drain-resolution'])
    successor_claim=combined_store.artifacts['window']
    successor_fields=decode_record(successor_claim,'HX-EV-PUBLICATION-WINDOW-2',codec['schemas']['D45-window'])
    assert successor_fields[3] == base['window']+1 and successor_fields[5:7] == (sha256(combined_store.imported('predecessor')).digest(),request_id)
    assert resolution[4] == sha256(successor_claim).digest() and {'resolution','fence','closure','window'} <= set(combined_store.artifacts)
    legacy_members=((10,'event-1',b'canonical-stored-event'),)
    legacy_bundle=capsule_chunks(tuple((sequence,message,sha256(body).digest()) for sequence,message,body in legacy_members))
    assert legacy_bundle['manifest'] == codec['capsule']
    verified_legacy=validate_capsule(legacy_bundle)
    capsule_charge=len(legacy_bundle['manifest'])+sum(len(raw) for raw in legacy_bundle['objects'].values())
    legacy_base=deepcopy(base); legacy_base.update(roster=legacy_members,accepted=(),unresolved=legacy_members,
        window=0,closed=0,window_claim=bytes(32),window_claim_bytes=b'',window_admission=b'',window_progress=b'',legacy_root=verified_legacy['root'],hold_source=verified_legacy['hash'],active_charge=capsule_charge,next_charge=capsule_charge+400,charge_ceiling=MiB)
    legacy_id,legacy_carrier=request(b'legacy-request',source=verified_legacy['hash'])
    legacy=resume_publication(legacy_base,legacy_id,legacy_carrier,'legacy_publish_failed','current',crash_after_audit=True)
    assert legacy['outcome'] == 'orphaned-success'
    legacy_store=PreparationStore(legacy_id,legacy_carrier,owned_resume_preparation(legacy,legacy_id),legacy_base,
        eligible='legacy-publish-failed',legacy_bundle=legacy_bundle)
    for kind in ('a8-head','attempts','broker')+imported_fields:
        assert legacy_store.import_key(legacy_id,sha256(legacy_store.origin).digest(),kind) not in legacy_store.backend.rows
    original_capsule_charge=legacy_store.charge_fields('old')
    original_capsule_charge_bytes=legacy_store.backend.read(legacy_store.charge_key(legacy_id,'old'),legacy_id)[0]
    assert original_capsule_charge[4] == 'resume-window' and original_capsule_charge[9] == 1
    assert original_capsule_charge[5:8] == (capsule_charge,0,capsule_charge)
    assert original_capsule_charge[12] is None and original_capsule_charge[14] == 0
    # Persisted restart uses only the same native capsule/chunk/stored-event
    # backend. No A8 head, C2 attempt set or window broker can be read.
    legacy_store=restart_preparation(legacy_store); assert legacy_store is not None
    assert legacy_store.turn('audit') == 'interrupted'
    legacy_store=restart_preparation(legacy_store); assert legacy_store is not None and legacy_store.turn() == 'completed'
    invocation=decode_record(legacy_store.artifacts['invocation'],'HX-EV-PUBLICATION-INVOCATION-1',codec['extra_schemas']['D45-invocation'])
    assert invocation[2] == bytes(32) and invocation[6] == verified_legacy['root'] and not {'fence','closure','window','resolution'} & set(legacy_store.artifacts)
    claim=decode_record(legacy_store.origin_fields()[5],'HX-EV-PUBLICATION-RESUME-3',codec['schemas']['D45-request'])
    assert claim[3] == claim[6] == bytes(32) and claim[5] == sha256(legacy_bundle['manifest']).digest()
    released_capsule_charge=legacy_store.charge_fields('old')
    assert released_capsule_charge[4] == 'resume-window' and released_capsule_charge[9:11] == (2,'released')
    assert released_capsule_charge[11] == sha256(original_capsule_charge_bytes).digest()
    assert released_capsule_charge[12] is None and released_capsule_charge[14] == 0
    assert legacy_store.charge_fields('metadata')[4] == 'side-record' and legacy_store.metadata_charge == 2*MiB
    assert legacy_store.charge_fields('new')[12] == legacy_id and legacy_store.charge_fields('new')[14] == 1
    legacy_addresses=(legacy_store.import_key(legacy_id,sha256(legacy_store.origin).digest(),'legacy-capsule'),
        next(iter(legacy_bundle['objects'])),legacy_store.import_key(legacy_id,sha256(legacy_store.origin).digest(),'legacy-stored-events'))
    for address in legacy_addresses:
        for damage in ('missing','changed','stale','unavailable'):
            damaged=deepcopy(legacy_store)
            if damage == 'missing': del damaged.backend.rows[address]
            elif damage == 'changed':
                damaged.backend.rows[address] += b'contradictory'
                owner,generation,receipt=damaged.backend.receipts[address]
                damaged.backend.receipts[address]=(owner,generation,damaged.backend.receipt(address,damaged.backend.rows[address],owner,generation))
            elif damage == 'stale':
                owner,generation,receipt=damaged.backend.receipts[address]; damaged.backend.receipts[address]=(owner,generation+1,receipt)
            else: damaged.backend.unavailable.add(address)
            snapshot=damaged.backend.snapshot()
            assert restart_preparation(damaged) is None and damaged.turn() == 'evidence-hold' and damaged.backend.snapshot() == snapshot
    # Authenticated but absent/contradictory capsule source hashes cannot satisfy
    # the signed exact manifest source; the finalized ledger remains unchanged.
    for bad_source in (bytes(32),codec['H']('other-drain-source')):
        damaged=deepcopy(legacy_store); address=legacy_addresses[0]
        fields=list(decode_record(damaged.backend.rows[address],'HX-EV-LEGACY-RESUME-CAPSULE-2',codec['schemas']['D46-capsule'])); fields[14]=bad_source
        raw=R('HX-EV-LEGACY-RESUME-CAPSULE-2',16,*(codec['encode_typed'](kind,value) for kind,value in zip(codec['schemas']['D46-capsule'],fields)))
        owner,generation,receipt=damaged.backend.receipts[address]; damaged.backend.rows[address]=raw
        damaged.backend.receipts[address]=(owner,generation,damaged.backend.receipt(address,raw,owner,generation))
        snapshot=damaged.backend.snapshot()
        assert restart_preparation(damaged) is None and damaged.turn() == 'evidence-hold' and damaged.backend.snapshot() == snapshot
    assert resume_publication(base,request_id,carrier,'unknown-eligibility','current')['outcome'] == 'resume_not_eligible'
    # An unrelated tenant hold survives restart, own removal and re-addition.
    index_store=PreparationStore(request_id,carrier,owned_resume_preparation(crashed,request_id),base,eligible='retry-exhausted')
    previous=index_store.backend.read(index_store.inventory_key,request_id); fields=list(decode_record(previous[0],'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index']))
    unrelated_row=(10000000001,'HeldDelivery','unrelated',codec['H']('unrelated-entry'))
    own_rows=codec['decode_rows'](fields[4],fields[3],['Q','U','U','B32'],[1024,1024,4096,1024]); all_rows=sorted(own_rows+[unrelated_row],key=lambda row:(row[0],row[1].encode(),row[2].encode()))
    fields[2],fields[3],fields[4],fields[6]=2,3,b''.join(Q(at)+U(hold)+U(subject)+digest for at,hold,subject,digest in all_rows),sha256(previous[0]).digest()
    raw=R('HX-EV-HOLD-INDEX-2',8,*(codec['encode_typed'](kind,value) for kind,value in zip(codec['schemas']['D37-index'],fields)))
    index_store.backend.write(index_store.inventory_key,raw,request_id,2,sha256(previous[0]).digest())
    index_store=restart_preparation(index_store); assert index_store is not None and index_store.turn() == 'completed'
    fields=decode_record(index_store.backend.rows[index_store.inventory_key],'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index'])
    assert codec['decode_rows'](fields[4],fields[3],['Q','U','U','B32'],[1024,1024,4096,1024]) == [unrelated_row]
    # Missing/changed persisted preparation cannot be replaced by process snapshots.
    for replacement in (b'',restarted_orphan['orphans'][request_id]['preparation'][:-1]):
        damaged = deepcopy(restarted_orphan); damaged['orphans'][request_id]['preparation'] = replacement
        row = damaged['orphans'][request_id]; row['receipt'] = state_hash({k:v for k,v in row.items() if k != 'receipt'})
        failed = resume_publication(damaged,request_id,carrier,'publication_retry_exhausted_hold','current')
        assert failed['outcome'] == 'resume_evidence_hold' and failed['state'] == damaged
    # Current recovery time, including deletion boundary, is authoritative even
    # when no prior reconciliation exists or its timestamp is older.
    for completion_time in (1899,1900,1901,1900+30*86400-1,1900+30*86400,1900+30*86400+1):
        for with_reconciliation in (False,True):
            pending = reconcile_tombstones(restarted_orphan,1100) if with_reconciliation else restarted_orphan
            result = resume_publication(pending,request_id,carrier,'publication_retry_exhausted_hold','current',now=completion_time)
            assert result['outcome'] == 'orphan-audit-retry' and result['response'] == exhausted['response']
            assert (request_id in result['state']['live']) == (completion_time < 1900)
            assert (request_id in result['state']['tombstones']) == (1900 <= completion_time < 1900+30*86400)
            assert result['state']['ordinal'] == 2 and len(result['state']['invocations']) == int(completion_time<1900+30*86400)
    fresh_handle = 'hxrsm1-other'
    custom = deepcopy(base); custom['handle'] = fresh_handle
    custom_id,custom_carrier = request(b'custom-handle',handle=fresh_handle)
    custom_result = resume_publication(custom,custom_id,custom_carrier,'publication_drain_limit_hold','current')
    # Independent bytes pin every field rather than comparing the encoder to itself.
    expected_body = ('{"auditRecordHash":"'+custom_result['state']['live'][custom_id]['result']['audit_hash'].hex()+
                     '","drainLimit":24,"resumeHandle":"hxrsm1-other","resumeOrdinal":2,"window":7}').encode()
    assert custom_result['response'] == expected_body
    custom_retry = resume_publication(custom_result['state'],custom_id,custom_carrier,'publication_drain_limit_hold','current')
    custom_crash = resume_publication(custom,custom_id,custom_carrier,'publication_drain_limit_hold','current',crash_after_audit=True)
    custom_recovery = resume_publication(read_image(image_bytes(custom_crash['state'])),custom_id,custom_carrier,'publication_drain_limit_hold','current')
    assert custom_retry['response'] == custom_recovery['response'] == expected_body
    prepared = owned_resume_preparation(custom_crash,custom_id)
    supplementary_records['D45-preparation'] = prepared
    supplementary_records['D45-invocation'] = R('HX-EV-PUBLICATION-INVOCATION-1',9,U('t'),U('op'),
        custom['window_claim'],N(2),N(24),custom_id,expected_root,custom_result['state']['invocations'][-1],Q(10000000000))
    retry_prepared = owned_resume_preparation(resume_publication(custom,custom_id,custom_carrier,'publication_retry_exhausted_hold','current',
        crash_after_audit=True),custom_id)
    restart_boundaries = []
    for eligible,selected_prepared in [('drain-limit',prepared),('retry-exhausted',retry_prepared)]:
      required = ('claim','resolution','reconstruction','audit','successor','finalize','invocation') if eligible == 'drain-limit' else (
          'claim','fence','closure','window','reconstruction','audit','successor','finalize','invocation')
      boundaries = ('origin','progress')+required+tuple(name+suffix for name in required if name not in {'reconstruction','finalize'}
          for suffix in ('-intent','-write'))+('reconstruction-write',)
      for crash_side in boundaries:
        preparation_store = PreparationStore(custom_id,custom_carrier,selected_prepared,custom,
            eligible=eligible,crash_after=crash_side if crash_side in {'origin','progress'} else None)
        if eligible == 'drain-limit':
            supplementary_records['D45-origin'] = preparation_store.origin
            supplementary_records['D45-preparation-head'] = R('HX-EV-RESUME-PREPARATION-HEAD-1',10,
                U(custom['tenant']),U('op'),custom_id,sha256(preparation_store.origin).digest(),O(None),
                U('admitted'),N(1),bytes(32),B(codec['pack']('>I',0)),Q(10000000000))
        if crash_side not in {'origin','progress'}: assert preparation_store.turn(crash_side) == 'interrupted', (eligible,crash_side)
        original_claim = preparation_store.origin_fields()[5]; original_origin = preparation_store.origin
        preparation_store.raw_artifacts = {'claim':b'poisoned process cache','audit':codec['audit'],'state':codec['state']}
        preparation_store.cached_swap = {'used':0,'owner':bytes(32),'finalized':True}
        preparation_store.progress_flags = {'completed':True}
        restarted_store = restart_preparation(preparation_store)
        assert restarted_store is not None and set(vars(restarted_store)) == {'backend','tenant','execution'}
        if any(name in restarted_store.artifacts for name in ('fence','closure','audit','successor')):
            assert restarted_store.rollback_artifacts(presence(custom_id,'successor','absent'),presence(custom_id,'audit','absent')) == 'completion-required'
        assert restarted_store.turn() == 'completed', (eligible,crash_side)
        assert restarted_store.origin == original_origin and restarted_store.artifacts['claim'] == original_claim
        audit_fields = decode_record(restarted_store.artifacts['audit'],'HX-EV-PUBLICATION-RESUME-AUDIT-4',codec['schemas']['D45-audit'])
        state_fields = decode_record(restarted_store.artifacts['successor'],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
        intended_state = read_image(decode_record(selected_prepared,'HX-EV-RESUME-PREPARATION-1',codec['extra_schemas']['D45-preparation'])[6])
        assert audit_fields[3] == custom_id and audit_fields[4] == sha256(custom_carrier).digest()
        assert audit_fields[5] == sha256(restarted_store.imported('predecessor')).digest()
        assert audit_fields[2] == state_fields[3] == intended_state['ordinal']
        assert audit_fields[7:9] == (intended_state['window'],intended_state['limit']) == state_fields[4:6]
        assert state_fields[10] == sha256(restarted_store.artifacts['audit']).digest() == intended_state['live'][custom_id]['result']['audit_hash']
        assert state_fields[7] == intended_state['window_claim']
        live_rows = codec['decode_rows'](state_fields[11][4:],int.from_bytes(state_fields[11][:4],'big'),['B32','B32','N','B32','N','N','Q'])
        assert live_rows[-1] == (custom_id,sha256(custom_carrier).digest(),2,state_fields[10],intended_state['window'],intended_state['limit'],19000000000)
        if eligible == 'drain-limit':
            assert audit_fields[6] is None and not {'closure','fence','window'} & set(restarted_store.artifacts)
            assert state_fields[4] == custom['window'] and state_fields[9] == custom['closed']
            assert state_fields[7] == custom['window_claim']
            resolution = decode_record(restarted_store.artifacts['resolution'],'HX-EV-PUBLICATION-DRAIN-LIMIT-RESOLUTION-1',codec['schemas']['D14-drain-resolution'])
            assert resolution[2] == custom['hold_source'] and resolution[4] == intended_state['invocations'][-1]
        else:
            assert audit_fields[6] == sha256(restarted_store.artifacts['closure']).digest()
            assert sha256(restarted_store.artifacts['window']).digest() == state_fields[7]
        assert restarted_store.swap.used == restarted_store.swap.active == custom['next_charge']
        assert restarted_store.metadata_charge == 2*MiB and not restarted_store.indexed
        finalized = restarted_store.backend.snapshot(); assert restarted_store.turn() == 'completed'
        assert restarted_store.backend.snapshot() == finalized
        restart_boundaries.append((eligible,crash_side))
    assert len(restart_boundaries) == 46
    cleanup_count = 0
    for cleanup_side in ('claim','resolution','claim-delete','resolution-delete'):
        preparation_store = PreparationStore(custom_id,custom_carrier,prepared,custom)
        assert preparation_store.turn('resolution') == 'interrupted'
        origin = preparation_store.origin
        unavailable_before = deepcopy(preparation_store.artifacts)
        assert preparation_store.rollback_artifacts(None,None) == 'completion-required'
        assert preparation_store.artifacts == unavailable_before and preparation_store.swap.used == 700
        assert preparation_store.rollback_artifacts(presence(custom_id,'successor','absent'),presence(custom_id,'audit','absent'),False) == 'cleanup-hold'
        assert preparation_store.artifacts == unavailable_before and preparation_store.swap.used == 700
        assert preparation_store.rollback_artifacts(presence(custom_id,'successor','absent'),presence(custom_id,'audit','absent'),crash_after=cleanup_side) == 'cleanup-hold'
        restored = restart_preparation(preparation_store)
        assert restored.swap.used == 700 and restored.metadata_charge == 2*MiB and restored.indexed
        cleanup_before = restored.backend.snapshot()
        assert restored.turn() == 'cleanup-hold' and restored.backend.snapshot() == cleanup_before
        assert restored.rollback_artifacts(presence(custom_id,'successor','absent'),presence(custom_id,'audit','absent')) == 'rolled-back'
        assert not restored.artifacts and restored.swap.used == custom['active_charge'] and not restored.indexed
        assert restored.turn() == 'completed' and restored.origin == origin
        assert restored.artifacts['claim'] == preparation_store.origin_fields()[5]
        cleanup_count += 1
    # At expiry a rolled-back preparation cannot acquire a new authorization.
    expired_store = PreparationStore(custom_id,custom_carrier,prepared,custom)
    assert expired_store.turn('resolution') == 'interrupted'
    assert expired_store.rollback_artifacts(presence(custom_id,'successor','absent'),presence(custom_id,'audit','absent')) == 'rolled-back'
    expired_store = restart_preparation(expired_store); expired_before = expired_store.backend.snapshot()
    assert expired_store.turn(now=1900) == 'resume_request_expired' and expired_store.backend.snapshot() == expired_before
    obligated=deepcopy(custom); unrelated_identity=codec['H']('unrelated-retry')
    obligated['tombstones'][unrelated_identity]={'carrier_hash':codec['H']('unrelated-carrier'),'expires_at':3000,'delete_after':3000+30*86400}
    obligated_result=resume_publication(obligated,custom_id,custom_carrier,'publication_drain_limit_hold','current',crash_after_audit=True)
    obligated_prepared=owned_resume_preparation(obligated_result,custom_id)
    rolled_store=PreparationStore(custom_id,custom_carrier,obligated_prepared,obligated)
    unrelated_inventory=add_unrelated_inventory(rolled_store)
    assert rolled_store.turn('resolution') == 'interrupted'
    rollback_before=rolled_store.backend.snapshot()
    assert rolled_store.rollback(presence(custom_id,'successor','absent'),presence(custom_id,'audit','absent'),False) == 'cleanup-hold'
    assert rolled_store.backend.snapshot() == rollback_before
    rollback_result=rolled_store.rollback(presence(custom_id,'successor','absent'),presence(custom_id,'audit','absent'))
    assert isinstance(rollback_result,tuple) and len(rollback_result) == 2 and rollback_result[0] == 'rolled-back'
    outcome,compact=rollback_result
    assert compact['tombstones'][custom_id]['carrier_hash'] == sha256(custom_carrier).digest()
    assert not any(raw.startswith(b'HX-EV-RESUME-ORIGIN-1\0') for raw in rolled_store.backend.rows.values())
    different_id,different_carrier=request(b'after-rollback',handle=fresh_handle)
    # The same provider has no pending/full slot, and admits a distinct full
    # origin at its stable address while retaining the compact old identity.
    next_store=admit_after_compaction(rolled_store.backend,different_id,different_carrier,compact)
    assert next_store is not None and next_store.backend is rolled_store.backend and next_store.turn() == 'completed'
    assert custom_id in compact['tombstones'] and unrelated_identity in compact['tombstones'] and next_store.swap.used == 400
    assert inventory_rows(next_store) == [unrelated_inventory]
    # Identity-wide tombstone deletion atomically reclaims old bodies; stable
    # key reuse is fresh and may create different original claim UTC bytes.
    reclaim_store=PreparationStore(custom_id,custom_carrier,obligated_prepared,obligated)
    unrelated_inventory=add_unrelated_inventory(reclaim_store)
    reference_claim=reclaim_store.origin_fields()[5]
    assert reclaim_store.turn(now=2000) == 'resume_request_expired'
    assert reclaim_store.turn(now=1000) == 'completed'
    reclaim_store.reconcile_successor(2000)
    fresh_prior=read_preparation(reclaim_store.preparation,obligated,custom_id,sha256(custom_carrier).digest())[1]
    fresh_prior=reconcile_tombstones(fresh_prior,1900+30*86400)
    snapshot=reclaim_store.backend.snapshot()
    assert not reclaim_resume_identity(reclaim_store.backend,'t','op',custom_id,1900+30*86400-1)
    assert reclaim_store.backend.snapshot() == snapshot
    assert not reclaim_resume_identity(reclaim_store.backend,'t','op',custom_id,1900+30*86400,False)
    assert reclaim_store.backend.snapshot() == snapshot
    old_keys=identity_record_keys(reclaim_store.backend,'t','op',custom_id)
    assert reclaim_resume_identity(reclaim_store.backend,'t','op',custom_id,1900+30*86400)
    assert all(key not in reclaim_store.backend.rows for key in old_keys)
    claim_key=codec['K']('HX-EV-PUBLICATION-RESUME-CLAIM-KEY-1',U('t'),U('op'),custom_id)
    fresh_prior['tombstones'].pop(custom_id,None)
    next_time=1900+30*86400
    assert unrelated_identity in fresh_prior['tombstones']
    # A new authenticated drain-limit hold changes the source under the same
    # persisted state CAS; the stable caller identity may now be reused.
    fresh_prior['hold_source']=codec['H']('fresh-drain-limit')
    state_key=codec['K']('HX-EV-PUBLICATION-RESUME-STATE-KEY-1',U('t'),U('op'))
    current=reclaim_store.backend.read(state_key,custom_id)
    reclaim_store.backend.write(state_key,publication_state_bytes(fresh_prior,next_time),custom_id,current[1]+1,sha256(current[0]).digest())
    reused_id,fresh_carrier=request(b'custom-handle',handle=fresh_handle,source=fresh_prior['hold_source'])
    assert reused_id == custom_id and fresh_carrier != custom_carrier
    fresh_store=admit_after_compaction(reclaim_store.backend,custom_id,fresh_carrier,fresh_prior,now=next_time,expiry=next_time+900)
    assert fresh_store is not None and fresh_store.backend is reclaim_store.backend
    assert fresh_store.turn(now=next_time) == 'completed'
    assert inventory_rows(fresh_store) == [unrelated_inventory]
    state_fields=decode_record(fresh_store.artifacts['successor'],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
    assert unrelated_identity in state_fields[12]
    new_claim=fresh_store.backend.read(claim_key,custom_id)[0]
    assert new_claim != reference_claim and decode_record(new_claim,'HX-EV-PUBLICATION-RESUME-3',codec['schemas']['D45-request'])[13] == next_time*10000000
    # Full-origin reclamation retains exact signed-claim authority at its
    # existing address, including the original envelope bytes and readback.
    signature_store=PreparationStore(custom_id,custom_carrier,prepared,custom)
    assert signature_store.turn() == 'completed'
    original_signature=signature_store.origin_fields()[10]
    origin_address=signature_store.origin_key(custom_id)
    claim_address,(retained_claim,retained_signature)=compact_successful_origin(signature_store)
    assert origin_address not in signature_store.backend.rows and retained_signature == original_signature
    assert signature_store.backend.read_signed(claim_address,custom_id) == (reference_claim,original_signature)
    damaged_signature=deepcopy(signature_store.backend)
    owner,payload_hash,signature,receipt=damaged_signature.signatures[claim_address]
    damaged_signature.signatures[claim_address]=(owner,payload_hash,bytes(32),receipt)
    try: damaged_signature.read_signed(claim_address,custom_id)
    except AssertionError: pass
    else: raise AssertionError('changed retained signature accepted after origin deletion')
    # Missing, changed, stale or unavailable durable evidence never uses the
    # poisoned process dictionaries and never mutates/refunds any byte row.
    reference = PreparationStore(custom_id,custom_carrier,prepared,custom)
    assert reference.turn('successor') == 'interrupted'
    durable_keys = (reference.inventory_key,reference.entry_key('op:'+custom_id.hex()),reference.origin_key(custom_id),
        reference.head_key,reference.preparation_key(custom_id),reference.import_key(custom_id,sha256(reference.origin).digest(),'predecessor'),
        reference.import_key(custom_id,sha256(reference.origin).digest(),'roster'),reference.artifact_key('claim'),
        reference.artifact_key('audit'),reference.artifact_key('state'))+tuple(reference.charge_key(custom_id,kind)
        for kind in ('metadata','old','new'))+tuple(reference.counter_key(kind) for kind in ('tenant','tenant-pool','deployment'))
    durable_refusals = 0
    for key in durable_keys:
      for damage in ('missing','changed','stale','unavailable'):
        damaged = deepcopy(reference)
        if damage == 'missing': del damaged.backend.rows[key]
        elif damage == 'changed': damaged.backend.rows[key] += b'changed'
        elif damage == 'stale':
            owner,generation,receipt = damaged.backend.receipts[key]
            damaged.backend.receipts[key] = (owner,generation+1,receipt)
        else: damaged.backend.unavailable.add(key)
        snapshot = damaged.backend.snapshot()
        assert restart_preparation(damaged) is None and damaged.turn() == 'evidence-hold', (key,damage)
        assert damaged.backend.snapshot() == snapshot
        durable_refusals += 1
    assert durable_refusals == 64
    # Provider-authenticated but contradictory records also fail unchanged.
    for kind,index,replacement in [('audit',3,bytes(32)),('audit',4,bytes(32)),('state',3,3)]:
        damaged = deepcopy(reference); key = damaged.artifact_key(kind)
        domain = 'HX-EV-PUBLICATION-RESUME-AUDIT-4' if kind == 'audit' else 'HX-EV-PUBLICATION-RESUME-STATE-3'
        schema = codec['schemas']['D45-audit' if kind == 'audit' else 'D45-state']
        fields = list(decode_record(damaged.backend.rows[key],domain,schema)); fields[index] = replacement
        raw = R(domain,len(schema),*(codec['encode_typed'](tag,value) for tag,value in zip(schema,fields)))
        owner,generation,receipt = damaged.backend.receipts[key]
        damaged.backend.rows[key] = raw
        damaged.backend.receipts[key] = (owner,generation,damaged.backend.receipt(key,raw,owner,generation))
        rows = damaged.manifest(); rows[kind] = (key,sha256(raw).digest(),damaged.backend.receipts[key][2],'present')
        damaged.progress(owner,'audited',rows,damaged.head()[4])
        snapshot = damaged.backend.snapshot()
        assert restart_preparation(damaged) is None and damaged.turn() == 'evidence-hold'
        assert damaged.backend.snapshot() == snapshot
        durable_refusals += 1
    damaged = PreparationStore(custom_id,custom_carrier,prepared,custom)
    assert damaged.turn('claim') == 'interrupted'
    del damaged.backend.rows[damaged.head_key]; del damaged.backend.receipts[damaged.head_key]
    snapshot = damaged.backend.snapshot()
    assert restart_preparation(damaged) is None and damaged.turn() == 'evidence-hold'
    assert damaged.backend.snapshot() == snapshot
    durable_refusals += 1
    assert durable_refusals == 68
    # Authoritative audit completion uses current UTC, never the old live image.
    expired_completion_count = 0
    for completion in (1900,1901,1900+30*86400):
        delayed = PreparationStore(custom_id,custom_carrier,prepared,custom)
        assert delayed.turn('audit') == 'interrupted'
        delayed = restart_preparation(delayed)
        assert delayed.turn('successor-intent',now=completion) == 'interrupted'
        delayed = restart_preparation(delayed)
        assert delayed.turn('successor-write',now=completion) == 'interrupted'
        delayed = restart_preparation(delayed)
        assert delayed.turn(now=completion) == 'completed'
        fields = decode_record(delayed.artifacts['successor'],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
        assert int.from_bytes(fields[11][:4],'big') == 0
        assert int.from_bytes(fields[12][:4],'big') == (completion < 1900+30*86400)
        assert fields[10] == sha256(delayed.artifacts['audit']).digest() and fields[13] == completion*10000000
        assert delayed.swap.used == 400
        expired_completion_count += 1
    after_prior = deepcopy(custom_result['state']); after_prior['hold_source'] = sha256(b'after-prior-success-hold').digest()
    after_id,after_carrier = request(b'after-prior-success',source=after_prior['hold_source'],handle=fresh_handle)
    after_prepared = owned_resume_preparation(resume_publication(after_prior,after_id,after_carrier,'publication_drain_limit_hold','current',
        crash_after_audit=True),after_id)
    after_store = PreparationStore(after_id,after_carrier,after_prepared,after_prior)
    assert after_store.turn('claim-write') == 'interrupted'
    after_store = restart_preparation(after_store)
    after_claim = decode_record(after_store.origin_fields()[5],'HX-EV-PUBLICATION-RESUME-3',codec['schemas']['D45-request'])
    assert after_claim[6] == sha256(after_store.imported('a8-head')).digest()
    assert after_claim[6] != sha256(after_store.imported('predecessor')).digest()
    assert after_claim[8] == after_prior['last_audit'] != bytes(32)
    for kind in ('a8-head','predecessor'):
        damaged = deepcopy(after_store); key = damaged.import_key(after_id,sha256(damaged.origin).digest(),kind)
        if kind == 'a8-head': raw = b'changed-existing-a8-head'
        else:
            fields = list(decode_record(damaged.backend.rows[key],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state']))
            fields[10] = bytes(32)
            raw = R('HX-EV-PUBLICATION-RESUME-STATE-3',14,*(codec['encode_typed'](tag,value) for tag,value in zip(codec['schemas']['D45-state'],fields)))
        owner,generation,receipt = damaged.backend.receipts[key]
        damaged.backend.rows[key] = raw
        damaged.backend.receipts[key] = (owner,generation,damaged.backend.receipt(key,raw,owner,generation))
        snapshot = damaged.backend.snapshot()
        assert restart_preparation(damaged) is None and damaged.turn() == 'evidence-hold'
        assert damaged.backend.snapshot() == snapshot
        durable_refusals += 1
    assert after_store.turn() == 'completed'
    after_audit = decode_record(after_store.artifacts['audit'],'HX-EV-PUBLICATION-RESUME-AUDIT-4',codec['schemas']['D45-audit'])
    assert after_audit[5] == sha256(after_store.imported('predecessor')).digest() != after_claim[6]
    assert durable_refusals == 70
    preparation_metrics.update(restarts=len(restart_boundaries),cleanup=cleanup_count,refusals=durable_refusals,expired_completions=expired_completion_count)
    assert preparation_metrics['cleanup'] == 4 and preparation_metrics['expired_completions'] == 3
    next_hold = deepcopy(drained['state']); next_hold['hold_source'] = sha256(b'new-drain-limit').digest()
    next_id,next_carrier = request(b'next-drain',source=next_hold['hold_source'])
    next_drain = resume_publication(next_hold,next_id,next_carrier,'publication_drain_limit_hold','current')
    assert next_drain['state']['ordinal'] == 3 and next_drain['state']['limit'] == 32
    assert len(set(next_drain['state']['invocations'])) == 2
    assert next_drain['state']['window_claim_bytes'] == base['window_claim_bytes']
    assert next_drain['state']['unresolved'] == base['unresolved'] and next_drain['command_executions'] == 0
    assert resume_publication(next_drain['state'],next_id,next_carrier,'publication_drain_limit_hold','current')['state'] == next_drain['state']
    # A real hourly live-to-tombstone CAS intervenes between audit and recovery.
    prior_identity,prior_carrier = request(b'prior-success')
    prior_success = resume_publication(base,prior_identity,prior_carrier,'publication_retry_exhausted_hold','current',expires_at=1100)
    prior_success['state']['hold_source']=sha256(b'fresh-composed-retry-exhaustion').digest()
    crash_id,crash_carrier = request(b'crash-after-prior',source=prior_success['state']['hold_source'])
    composed_crash = resume_publication(prior_success['state'],crash_id,crash_carrier,
        'publication_retry_exhausted_hold','current',crash_after_audit=True)['state']
    reconciled_crash = reconcile_tombstones(composed_crash,1100)
    assert prior_identity not in reconciled_crash['live'] and prior_identity in reconciled_crash['tombstones']
    composed = resume_publication(reconciled_crash,crash_id,crash_carrier,'publication_retry_exhausted_hold','current',now=1100)
    assert composed['outcome'] == 'orphan-audit-retry' and composed['state']['ordinal'] == 3
    assert composed['state']['tombstones'] == reconciled_crash['tombstones']
    assert prior_identity not in composed['state']['live'] and crash_id in composed['state']['live']
    assert composed['state']['audits'] == 2 and len(composed['state']['invocations']) == 2
    repeated = resume_publication(composed['state'],crash_id,crash_carrier,'publication_retry_exhausted_hold','current',now=1100)
    assert repeated['outcome'] == 'exact-retry' and repeated['response'] == composed['response'] and repeated['state'] == composed['state']
    for completion_time in (1899,1900,1901,1900+30*86400-1,1900+30*86400,1900+30*86400+1):
        recovered_at = resume_publication(read_image(image_bytes(reconciled_crash)),crash_id,crash_carrier,
            'publication_retry_exhausted_hold','current',now=completion_time)
        assert recovered_at['outcome'] == 'orphan-audit-retry' and recovered_at['state']['ordinal'] == 3
        assert (crash_id in recovered_at['state']['live']) == (completion_time < 1900)
        assert (crash_id in recovered_at['state']['tombstones']) == (1900 <= completion_time < 1900+30*86400)
        assert recovered_at['response'] == composed['response'] and len(recovered_at['state']['invocations']) == int(completion_time<1100+30*86400)+int(completion_time<1900+30*86400)
    corrupt_reconciliation = deepcopy(reconciled_crash); corrupt_reconciliation['reconciliation']['receipt'] = bytes(32)
    assert resume_publication(corrupt_reconciliation,crash_id,crash_carrier,'publication_retry_exhausted_hold','current')['state'] == corrupt_reconciliation
    unrelated = deepcopy(orphan); unrelated['tombstones'][bytes(32)] = {'carrier_hash':bytes(32),'expires_at':1,'delete_after':9999}
    assert resume_publication(unrelated,request_id,carrier,'publication_retry_exhausted_hold','current')['outcome'] == 'resume_evidence_hold'
    next_id,next_carrier = request(b'competing-request')
    competing = resume_publication(orphan,next_id,next_carrier,'publication_retry_exhausted_hold','current')
    assert competing['outcome'] == 'resume_capacity_hold' and competing['state'] == orphan
    changed_id,changed_carrier = request(b'request-1',reason=b'changed')
    assert resume_publication(orphan,changed_id,changed_carrier,
                              'publication_retry_exhausted_hold','current')['state'] == orphan
    assert resume_publication(orphan,changed_id,changed_carrier,
                              'publication_retry_exhausted_hold','current')['outcome'] == 'resume_request_conflict'
    assert request_id in reconcile_tombstones(live_retry,1899)['live']
    tombstone = reconcile_tombstones(live_retry,1900)
    assert request_id not in tombstone['live']
    delete_after = 1900+30*86400
    assert tombstone['tombstones'][request_id]['delete_after'] == delete_after
    for retry_id,retry_carrier,outcome in [(request_id,carrier,'resume_request_expired'),
            (*request(b'request-1',reason=b'changed'),'resume_request_conflict')]:
        tombstone_retry = resume_publication(tombstone,retry_id,retry_carrier,'publication_retry_exhausted_hold','current',now=1901)
        assert tombstone_retry['outcome'] == outcome and tombstone_retry['state'] == tombstone
    for now in (1900,1901):
        assert resume_publication(live_retry,request_id,carrier,
            'publication_retry_exhausted_hold','current',now=now)['outcome'] == 'resume_request_expired'
    assert request_id in reconcile_tombstones(tombstone,delete_after-1)['tombstones']
    assert request_id not in reconcile_tombstones(tombstone,delete_after)['tombstones']
    assert request_id not in reconcile_tombstones(tombstone,delete_after+1)['tombstones']
    assert reconcile_tombstones(tombstone,delete_after,authenticated=False) == tombstone
    for field, hold in [('ordinal','publication_drain_limit_hold'), ('limit','publication_drain_limit_hold'),
                        ('window','publication_retry_exhausted_hold'), ('closed','publication_retry_exhausted_hold')]:
        overflow = deepcopy(base); overflow[field] = U64_MAX
        if field=='window':
            f=list(decode_record(overflow['window_claim_bytes'],'HX-EV-PUBLICATION-WINDOW-2',codec['schemas']['D45-window'])); f[3]=U64_MAX
            overflow['window_claim_bytes']=R('HX-EV-PUBLICATION-WINDOW-2',13,*(codec['encode_typed'](k,v) for k,v in zip(codec['schemas']['D45-window'],f)))
            overflow['window_claim']=sha256(overflow['window_claim_bytes']).digest()
        snapshot = deepcopy(overflow)
        assert resume_publication(overflow, request_id, carrier, hold, 'current')['outcome'] == 'resume_arithmetic_exhausted'
        assert overflow == snapshot
    zero_increment = deepcopy(base)
    assert resume_publication(zero_increment, request_id, carrier,
                              'publication_drain_limit_hold', 'current', 0)['outcome'] == 'resume_arithmetic_exhausted'
    charge_overflow = deepcopy(base); charge_overflow['active_charge'] = U64_MAX; charge_overflow['next_charge'] = 1
    charge_snapshot = deepcopy(charge_overflow)
    assert resume_publication(charge_overflow, request_id, carrier,
                              'publication_drain_limit_hold', 'current')['outcome'] == 'resume_arithmetic_exhausted'
    assert charge_overflow == charge_snapshot
    capacity = deepcopy(base); capacity['charge_ceiling'] = 699
    assert resume_publication(capacity, request_id, carrier,
                              'publication_drain_limit_hold', 'current')['outcome'] == 'resume_capacity_hold'
    full_retry_index = deepcopy(base)
    full_retry_index['live'] = {sha256(str(i).encode()).digest():{'carrier_hash':bytes(32),
                                                              'expires_at':1900} for i in range(64)}
    assert resume_publication(full_retry_index, request_id, carrier,
                              'publication_drain_limit_hold', 'current')['outcome'] == 'resume_capacity_hold'
    recovered_capacity = reconcile_tombstones(full_retry_index,delete_after)
    assert not recovered_capacity['live'] and not recovered_capacity['tombstones']
    assert resume_publication(recovered_capacity,request_id,carrier,
        'publication_drain_limit_hold','current',now=delete_after,expires_at=delete_after+900)['outcome'] == 'resumed'
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
    return sha256(b'HX-EV-LEGACY-RESUME-EVENTS-2\0\x01'
                  + len(encoded).to_bytes(4, 'big') + encoded).digest()

def capsule_chunks(rows, classification='success-events'):
    if not 1 <= len(rows) <= 1000 or exact_legacy_rows(rows) is None:
        return None
    if any(len(row[1].encode()) > 1024 for row in rows):
        return None
    identity = codec['capsule_identity']
    chunks, objects, manifest_rows = [], {}, b''
    for ordinal,index in enumerate(range(0,len(rows),61)):
        part = rows[index:index+61]
        raw_rows = exact_legacy_rows(part)[4:]
        root = sha256(b'HX-EV-LEGACY-RESUME-CHUNK-ROWS-1\0\x01' + B(raw_rows)).digest()
        raw = R('HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-1',5,identity,N(ordinal),N(len(part)),B(raw_rows),root)
        key = codec['K']('HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1',identity,N(ordinal))
        assert key == 'legacy-resume-capsule-chunk:'+codec['KD']('HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1',identity,N(ordinal)), 'legacy-chunk-physical-address'
        assert len(raw) <= 64*1024
        objects[key] = raw
        chunks.append((ordinal,part[0][0],len(part),root))
        manifest_rows += N(ordinal)+N(part[0][0])+N(len(part))+sha256(raw).digest()+N(len(raw))+U(key)
    manifest_rows = len(chunks).to_bytes(4,'big') + manifest_rows
    manifest = R('HX-EV-LEGACY-RESUME-CAPSULE-2',16,U('t'),U('d'),U('a'),U('tracking'),O(U('op')),
        U('correlation'),U('increment'),U(classification),N(rows[0][0]),N(rows[-1][0]),I(len(rows)),
        exact_legacy_root(rows),B(manifest_rows),U('drain-exhaustion'),codec['source_hash'],Q(codec['t']))
    assert len(manifest) <= 128*1024
    return {'manifest':manifest,'objects':objects,'chunks':tuple(chunks)}

class Cursor:
    def __init__(self, raw): self.raw, self.offset = raw, 0
    def take(self, length):
        assert 0 <= length <= len(self.raw)-self.offset
        value = self.raw[self.offset:self.offset+length]; self.offset += length
        return value
    def number(self, width=8): return int.from_bytes(self.take(width),'big')
    def string(self):
        size = self.number(4)
        assert 1 <= size <= 1024
        return self.take(size).decode('utf-8',errors='strict')
    def complete(self): assert self.offset == len(self.raw)

def validate_capsule(bundle):
    try:
        raw = bundle['manifest']
        assert len(raw) <= 128*1024
        fields = decode_record(raw,'HX-EV-LEGACY-RESUME-CAPSULE-2',codec['schemas']['D46-capsule'])
        identity = sha256(b'HX-EV-LEGACY-RESUME-CAPSULE-IDENTITY-1\0\x01'+
                          b''.join(U(fields[i]) for i in (0,1,2,3))+fields[14]).digest()
        manifest = Cursor(fields[12]); count = manifest.number(4)
        assert 1 <= count <= 17
        rows, seen_keys = [], set()
        for expected_ordinal in range(count):
            ordinal, first, row_count = (manifest.number() for _ in range(3))
            digest, length, key = manifest.take(32), manifest.number(), manifest.string()
            assert ordinal == expected_ordinal and 1 <= row_count <= 61
            expected_key = codec['K']('HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1',identity,N(ordinal))
            assert expected_key == 'legacy-resume-capsule-chunk:'+codec['KD']('HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-KEY-1',identity,N(ordinal)), 'legacy-chunk-physical-address'
            assert key == expected_key and key not in seen_keys
            seen_keys.add(key)
            chunk = bundle['objects'][key]
            assert len(chunk) == length and length <= 64*1024 and sha256(chunk).digest() == digest
            c = decode_record(chunk,'HX-EV-LEGACY-RESUME-CAPSULE-CHUNK-1',['B32','N','N','B','B32'])
            assert c[:3] == (identity,ordinal,row_count)
            assert c[4] == sha256(b'HX-EV-LEGACY-RESUME-CHUNK-ROWS-1\0\x01'+B(c[3])).digest()
            reader = Cursor(c[3]); part = []
            for _ in range(row_count):
                part.append((reader.number(),reader.string(),reader.take(32)))
            reader.complete()
            assert part[0][0] == first
            rows.extend(part)
        manifest.complete()
        assert set(bundle['objects']) == seen_keys
        assert len(rows) == fields[10] and (rows[0][0],rows[-1][0]) == (fields[8],fields[9])
        assert exact_legacy_root(rows) == fields[11]
        return {'rows':tuple(rows),'classification':fields[7],'root':fields[11],
                'hash':sha256(raw).digest(),'correlation':fields[5],'command_type':fields[6]}
    except (AssertionError,KeyError,TypeError,UnicodeDecodeError):
        return None

def recovery_hash(record):
    return state_hash(record) if record is not None else bytes(32)

def repair_receipt(record, bundle):
    verified = validate_capsule(bundle)
    if not verified or verified['hash'] != record['capsule']:
        return None
    return sha256(b'authoritative-repaired-evidence:' + recovery_hash(record) +
                  verified['hash'] + exact_legacy_rows(verified['rows'])).digest()

def recovery_transition(record, target, ordinal, *, owner, expected_generation,
                        expected_predecessor, failure=None, repair=None, bundle=None, capsule):
    before = deepcopy(record)
    if (owner not in {'legacy-resume','dead-letter-admin'} or type(ordinal) is not int
            or not 1 <= ordinal <= U64_MAX or type(expected_generation) is not int
            or not 1 <= expected_generation <= U64_MAX
            or expected_predecessor != recovery_hash(record)):
        return None
    if not isinstance(capsule,bytes) or len(capsule) != 32:
        return None
    if target == 'claimed':
        verified = validate_capsule(bundle)
        if verified is None or verified['hash'] != capsule:
            return None
    if record is None:
        return ({'generation':1, 'state':'claimed', 'ordinal':ordinal,
                 'failure':None, 'capsule':capsule,'owner':owner,
                 'predecessor':bytes(32),'repair':None}
                if target == 'claimed' and expected_generation == 1 and repair is None else None)
    next_generation = checked_add(record['generation'],1)
    if (capsule != record['capsule'] or ordinal < record['ordinal']
            or owner != record['owner'] or next_generation is None
            or expected_generation != next_generation):
        return None
    state = record['state']
    allowed = ((state == 'claimed' and target == 'draining' and ordinal == record['ordinal'])
               or (state == 'draining' and target == 'completed' and ordinal == record['ordinal'])
               or (state == 'draining' and target == 'failed' and ordinal == record['ordinal']
                   and failure in {'transport-retryable','evidence-unavailable','evidence-contradictory'})
               or (state == 'failed' and target == 'claimed' and ordinal > record['ordinal']
                   and (record['failure'] == 'transport-retryable'
                        or (repair is not None and repair == repair_receipt(record,bundle)))))
    if target != 'failed' and failure is not None:
        allowed = False
    if repair is not None and not (state == 'failed' and target == 'claimed'
                                  and record['failure'] != 'transport-retryable'):
        allowed = False
    if not allowed:
        assert before == record
        return None
    return {'generation':next_generation, 'state':target, 'ordinal':ordinal,
            'failure':failure if target == 'failed' else None, 'capsule':capsule,
            'owner':owner,'predecessor':recovery_hash(record),'repair':repair}

def recover_legacy_range(capsule, stored_rows, evidence):
    if not isinstance(capsule, dict) or evidence == 'contradictory-authority':
        return {'outcome':'legacy_resume_evidence_unavailable', 'rearmed':(), 'command_executions':0}
    source = capsule.get('source')
    if (not isinstance(source, dict)
            or not {'correlation', 'command_type', 'classification'} <= set(source)):
        return {'outcome':'legacy_resume_evidence_unavailable', 'rearmed':(), 'command_executions':0}
    root = exact_legacy_root(stored_rows)
    verified_bundle = validate_capsule(capsule.get('bundle'))
    try:
        contradicts = (evidence == 'unavailable-read' or root is None or not verified_bundle
            or verified_bundle['rows'] != tuple(stored_rows)
            or verified_bundle['classification'] != capsule['classification']
            or verified_bundle['correlation'] != capsule['correlation']
            or verified_bundle['command_type'] != capsule['command_type']
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
               'command_type':'increment', 'classification':'success-events', 'source':source,
               'bundle':capsule_chunks(rows)}
    resumed = recover_legacy_range(capsule, rows, 'verified')
    assert resumed == {
        'outcome':'resumed', 'stored_range':(41, 42), 'rearmed':rows,
        'terminal_status':'Completed', 'command_executions':0}
    rejection = dict(capsule, classification='rejection-events',
                     source=dict(source, classification='rejection-events'),bundle=capsule_chunks(rows,'rejection-events'))
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
    maximum_rows = tuple((index+1, str(index).zfill(4)+'m'*1020, sha256(str(index).encode()).digest()) for index in range(1000))
    chunks = capsule_chunks(maximum_rows)
    assert len(chunks['chunks']) == 17 and chunks['chunks'][0][2] == 61 and chunks['chunks'][-1][2] == 24
    assert validate_capsule(chunks)['rows'] == maximum_rows
    assert all(len(raw) <= 65536 for raw in chunks['objects'].values())
    assert len(chunks['manifest']) <= 128*1024
    fixture_rows = ((10,'event-1',sha256(codec['stored']).digest()),)
    fixture = capsule_chunks(fixture_rows)
    assert fixture['manifest'] == codec['capsule']
    assert tuple(fixture['objects'].values()) == (codec['chunk'],)
    assert fixture['chunks'][0][3] == codec['chunk_root']
    object_keys = tuple(chunks['objects'])
    for mutation in ('missing','swapped','changed','root'):
        corrupted = deepcopy(chunks)
        if mutation == 'missing': del corrupted['objects'][object_keys[0]]
        elif mutation == 'swapped':
            corrupted['objects'][object_keys[0]],corrupted['objects'][object_keys[1]] = (
                corrupted['objects'][object_keys[1]],corrupted['objects'][object_keys[0]])
        elif mutation == 'changed':
            corrupted['objects'][object_keys[0]] += b'changed'
        else:
            corrupted['manifest'] = corrupted['manifest'].replace(exact_legacy_root(maximum_rows),bytes(32),1)
        assert validate_capsule(corrupted) is None
    assert capsule_chunks(maximum_rows + ((1001, 'x', sha256(b'x').digest()),)) is None
    binding = validate_capsule(capsule['bundle'])['hash']
    def move(record,target,ordinal,**kwargs):
        args = {'owner':'legacy-resume','expected_generation':1 if record is None else record['generation']+1,
                'expected_predecessor':recovery_hash(record),'capsule':binding,'bundle':capsule['bundle']}
        args.update(kwargs)
        snapshot = deepcopy(record)
        result = recovery_transition(record,target,ordinal,**args)
        assert record == snapshot
        return result
    claimed = move(None,'claimed',1)
    draining = move(claimed,'draining',1)
    completed = move(draining,'completed',1)
    assert completed['state'] == 'completed' and move(completed,'claimed',2) is None
    transport_failed = move(draining,'failed',1,failure='transport-retryable')
    reclaimed = move(transport_failed,'claimed',2)
    assert reclaimed['generation'] == transport_failed['generation']+1 and reclaimed['capsule'] == transport_failed['capsule']
    for failure in ('evidence-unavailable','evidence-contradictory'):
        evidence_failed = move(draining,'failed',1,failure=failure)
        assert move(evidence_failed,'claimed',2) is None
        repair = repair_receipt(evidence_failed,capsule['bundle'])
        assert repair is not None
        assert move(evidence_failed,'claimed',2,repair=bytes(32),bundle=capsule['bundle']) is None
        assert move(evidence_failed,'claimed',2,repair=repair,bundle=None) is None
        assert move(evidence_failed,'claimed',2,repair=repair,bundle=capsule['bundle'])['state'] == 'claimed'
    assert move(transport_failed,'draining',2) is None
    assert move(transport_failed,'claimed',1) is None
    assert move(transport_failed,'claimed',2,capsule=b'changed') is None
    for record,target,ordinal in [(claimed,'draining',1),(draining,'failed',1),(transport_failed,'claimed',2)]:
        assert move(record,target,ordinal,owner='dead-letter-admin') is None
        assert move(record,target,ordinal,expected_predecessor=bytes(32)) is None
        assert move(record,target,ordinal,expected_generation=record['generation']+2) is None
    assert move(None,'claimed',0) is None and move(None,'claimed',U64_MAX+1) is None
    assert move(None,'claimed',1,bundle=None) is None
    overflow = dict(transport_failed,generation=U64_MAX)
    assert move(overflow,'claimed',2) is None
    assert move(draining,'failed',1,failure='unknown') is None
    assert move(claimed,'completed',1) is None
    second_exhaustion = move(reclaimed,'draining',2)
    second_exhaustion = move(second_exhaustion,'failed',2,failure='transport-retryable')
    assert second_exhaustion['capsule'] == claimed['capsule'] and second_exhaustion['generation'] > transport_failed['generation']
    records = {'claimed':claimed,'draining':draining,'completed':completed,'failed':transport_failed}
    allowed_edges = {('claimed','draining'),('draining','completed'),('draining','failed'),('failed','claimed')}
    for source_state,record in records.items():
        for target in records:
            ordinal = 2 if source_state == 'failed' and target == 'claimed' else 1
            kwargs = {'failure':'transport-retryable'} if target == 'failed' else {}
            assert (move(record,target,ordinal,**kwargs) is not None) == ((source_state,target) in allowed_edges)
    return 'legacy-resume'

def capacity_admit(ledger, account, amounts, evidence=True):
    before = (dict(ledger.tenant), ledger.tenant_pool, ledger.unidentified, ledger.deployment)
    if not evidence or not valid_amounts(amounts):
        return ('publication_pin_capacity_hold', before == (
            dict(ledger.tenant), ledger.tenant_pool, ledger.unidentified, ledger.deployment))
    admitted = ledger.reserve('tenant', account, amounts)
    return ('admitted' if admitted else 'capacity-wait', before)

def capability_ready(scope_retention_ceiling):
    return scope_retention_ceiling >= 64*MiB

def verify_capacity_wait_matrix():
    assert not capability_ready(64*MiB-1) and capability_ready(64*MiB) and capability_ready(64*MiB+1)
    authenticated = Ledger()
    predecessors = authenticated.predecessors('tenant-a')
    candidate_rows=authenticated.pin_candidates('tenant-a',[MiB+10,MiB+20])
    predecessor_snapshot = deepcopy(vars(authenticated))
    for kind in ('tenant','tenant-pool','deployment'):
        forged = dict(predecessors); forged[kind] = bytes(32)
        assert not authenticated.reserve_pin_batch('tenant-a',[MiB+10,MiB+20],forged,b'batch',candidates=candidate_rows)
        assert vars(authenticated) == predecessor_snapshot
    assert not authenticated.reserve_pin_batch('tenant-a',[MiB+10,MiB+20],{'tenant':predecessors['tenant']},b'batch',candidates=candidate_rows)
    assert vars(authenticated) == predecessor_snapshot
    assert authenticated.reserve_pin_batch('tenant-a',[MiB+10,MiB+20],predecessors,b'batch',candidates=candidate_rows)
    assert authenticated.tenant[('tenant','tenant-a')] == authenticated.tenant_pool == authenticated.deployment == 2*MiB+30
    assert authenticated.charges == {b'batch':(MiB+10,MiB+20)}
    stale_snapshot = deepcopy(vars(authenticated))
    assert not authenticated.reserve_pin_batch('tenant-a',[1],predecessors,b'other-batch')
    assert vars(authenticated) == stale_snapshot
    assert not authenticated.reserve_pin_batch('tenant-a',[MiB+10,MiB+20],predecessors,b'batch',candidates=candidate_rows)
    assert vars(authenticated) == stale_snapshot
    assert authenticated.read_pin_reservation('tenant-a',[MiB+10,MiB+20],b'batch',candidates=candidate_rows)
    for account,scope,candidate,request_id in [('tenant-b',None,None,None),('tenant-a',bytes(32),None,None),
        ('tenant-a',None,bytes(32),None),('tenant-a',None,None,bytes(32))]:
        assert not authenticated.reserve_pin_batch(account,[MiB+10,MiB+20],authenticated.predecessors(account),b'batch',scope,candidate,request_id,candidates=candidate_rows)
        assert vars(authenticated) == stale_snapshot
    for kind,identity in [('tenant','t'),('tenant-pool','tenant-pool'),('deployment','deployment'),('unidentified','unidentified')]:
        overflow_ledger = Ledger(); account_kind = 'capture-scope' if kind == 'unidentified' else 'tenant'
        overflow_ledger.generations[(kind,identity)] = U64_MAX
        overflow_snapshot = deepcopy(vars(overflow_ledger))
        assert not overflow_ledger.reserve(account_kind,'t',[1]) and vars(overflow_ledger) == overflow_snapshot
        overflow_ledger = Ledger(); assert overflow_ledger.reserve(account_kind,'t',[1])
        overflow_ledger.generations[(kind,identity)] = U64_MAX; overflow_snapshot = deepcopy(vars(overflow_ledger))
        assert not overflow_ledger.refund(account_kind,'t',1) and vars(overflow_ledger) == overflow_snapshot
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
    fair.tenant_turn('tenant-a',fair.tenant_fit_receipt('tenant-a',True))
    assert fair.where['pin-batch-a'] == 'deployment'
    assert capacity_admit(capacity, 'tenant-a', batch)[0] == 'admitted'
    capacity.refund('tenant', 'tenant-a', sum(batch))
    assert capacity.tenant[('tenant','tenant-a')] == capacity.tenant_pool == capacity.deployment == 0
    empty = (dict(capacity.tenant), capacity.tenant_pool,
             capacity.unidentified, capacity.deployment)
    assert capacity_admit(capacity, 'tenant-a', [1, -1]) == ('publication_pin_capacity_hold', True)
    assert capacity_admit(capacity, 'tenant-a', [U64_MAX, 1]) == ('publication_pin_capacity_hold', True)
    assert empty == (dict(capacity.tenant), capacity.tenant_pool,
                     capacity.unidentified, capacity.deployment)
    assert capacity.tenant[('tenant','tenant-a')] == capacity.tenant_pool == capacity.deployment == 0
    return 'capacity-wait'

def observe_delivery(previous, observed_at, ledger=None, identity=None, carrier=None):
    if previous is None:
        if ledger is None or identity is None or carrier is None: return None
        scope_kind,deployment,tenant,component,topic,subscription = identity
        subject = held_key(*identity,carrier)
        if subject is None: return None
        account_kind = 'tenant' if scope_kind == 'tenant' else 'capture-scope'
        account = tenant if scope_kind == 'tenant' else 'capture:'+subject.hex()
        metadata_key,inventory_key = 'metadata:'+subject.hex(),'inventory:'+subject.hex()
        if (metadata_key in ledger.charges or inventory_key in ledger.inventory
                or len(ledger.inventory) >= ledger.inventory_ceiling): return None
        if not ledger.reserve(account_kind,account,[32*1024]): return None
        metadata = {'owner':subject,'account_kind':account_kind,'account':account,
                    'amount':32*1024,'state':'active','generation':1}
        metadata['receipt'] = state_hash(metadata)
        inventory = {'owner':subject,'reserved':True,'generation':1}
        inventory['receipt'] = state_hash(inventory)
        ledger.charges[metadata_key] = metadata
        ledger.inventory[inventory_key] = inventory
        state = {'identity':identity,'held_key':subject,'carrier_hash':sha256(carrier).digest(),
                 'metadata_key':metadata_key,'inventory_key':inventory_key,
                 'metadata_receipt':metadata['receipt'],'inventory_receipt':inventory['receipt'],
                 'account_kind':account_kind,'account':account,
                 'state':'observed','transport_copy_acked':False,'route_success':False,'closed':False,
        'first_observed':observed_at,
        'delivery_attempt_count':0,
        'charged_bytes':32*1024,
        'indexed':True,
        'operator_visible':True,
        }
    else: state = deepcopy(previous)
    count = checked_add(state['delivery_attempt_count'],1)
    if count is None: return deepcopy(previous)
    state['delivery_attempt_count'] = count
    state['observation_revision'] = count
    state['observation_receipt'] = observation_receipt(state)
    return state

def observation_receipt(state):
    protected = {k:v for k,v in codec['capture_projection'](state).items() if k not in {'delivery_attempt_count','observation_revision','observation_receipt'}}
    return sha256(b'provider-monotonic-observation:'+image_bytes(protected)+N(state['delivery_attempt_count'])+N(state['observation_revision'])).digest()

def capture_origin(previous, subject, policy_hash):
    projection = codec['capture_projection'](previous)
    raw = R('HX-EV-CAPTURE-ORIGIN-1',6,subject,B(image_bytes(projection)),state_hash(projection),Q(previous['first_observed']),N(previous['delivery_attempt_count']),policy_hash)
    decode_record(raw,'HX-EV-CAPTURE-ORIGIN-1',['B32','B','B32','Q','N','B32'])
    return raw

def original_observation(raw, current):
    fields = decode_record(raw,'HX-EV-CAPTURE-ORIGIN-1',['B32','B','B32','Q','N','B32'])
    original = read_image(fields[1])
    excluded = {'delivery_attempt_count','observation_revision','observation_receipt'}
    assert {k:v for k,v in original.items() if k not in excluded} == {k:v for k,v in codec['capture_projection'](current).items() if k not in excluded}
    assert current['delivery_attempt_count'] >= original['delivery_attempt_count']
    assert current['observation_revision'] == current['delivery_attempt_count']
    assert current['observation_receipt'] == observation_receipt(current)
    restored = dict(current)
    for key in excluded: restored[key] = original[key]
    return restored

def held_key(scope_kind, deployment, tenant, component, topic, subscription, carrier):
    if (scope_kind == 'tenant') != (tenant is not None) or scope_kind not in {'tenant','deployment'}:
        return None
    material = (U(scope_kind)+U(deployment)+O(None if tenant is None else U(tenant))+
                U(component)+U(topic)+U(subscription)+sha256(carrier).digest())
    return sha256(b'HX-EV-HELD-DELIVERY-KEY-2\0\x01' + material).digest()

def policy_revision(previous, revision, expected_head, config=None):
    predecessor = bytes(32) if previous is None else previous['hash']
    expected_revision = 1 if previous is None else checked_add(previous['revision'],1)
    if (type(revision) is not int or revision != expected_revision
            or not 1 <= revision <= U64_MAX or expected_head != predecessor
            or (previous is not None and not policy_selected(previous,expected_head))):
        return None
    config = codec['H']('subscription-config') if config is None else config
    raw = R('HX-EV-SUBSCRIPTION-DELIVERY-POLICY-3',13,U('deployment-a'),U('pubsub'),U('orders'),
        U('sub-a'),N(revision),predecessor,U('initial' if revision == 1 else 'successor'),
        U('dead-letter-capture'),O(U('orders-dlq')),N(8),config,U('dapr-configuration'),Q(codec['t']))
    return {'revision':revision,'predecessor':predecessor,'raw':raw,'hash':sha256(raw).digest()}

def policy_selected(record, head):
    try:
        fields = decode_record(record['raw'],'HX-EV-SUBSCRIPTION-DELIVERY-POLICY-3',
            ['U','U','U','U','N','B32','U','U',('O','U'),'N','B32','U','Q'])
        return (record['hash'] == head == sha256(record['raw']).digest()
                and fields[4] == record['revision'] and fields[5] == record['predecessor'])
    except (AssertionError,KeyError,TypeError): return False

class PolicyStore:
    def __init__(self):
        self.revisions = {}; self.head = bytes(32)
    def address(self, revision):
        return codec['K']('HX-EV-SUBSCRIPTION-POLICY-KEY-1',U('deployment-a'),U('pubsub'),U('orders'),U('sub-a'),N(revision))
    def install(self, previous, revision, expected_head, config=None):
        candidate = policy_revision(previous,revision,expected_head,config)
        if candidate is None: return None
        address = self.address(revision)
        existing = self.revisions.get(address)
        if existing is not None and existing != candidate: return None
        if self.head == candidate['hash'] and existing == candidate: return deepcopy(existing)
        if self.head != expected_head: return None
        self.revisions[address] = deepcopy(candidate)
        self.head = candidate['hash']
        return deepcopy(candidate)
    def selected(self, record):
        return (self.revisions.get(self.address(record['revision'])) == record
                and policy_selected(record,self.head))

def object_receipt(backend, key, retained):
    return sha256(b'authenticated-object-readback:'+U(backend)+U(key)+B(retained)).digest()

def capture_delivery(previous, retained, ledger, readback, policy, current_head, crash_at=None):
    state = deepcopy(previous)
    if len(retained) > 193*MiB: return state
    if not isinstance(current_head,PolicyStore) or not current_head.selected(policy):
        return state
    identity = previous['identity']; subject = held_key(*identity,retained)
    if subject != previous['held_key'] or sha256(retained).digest() != previous['carrier_hash']:
        return state
    policy_fields = decode_record(policy['raw'],'HX-EV-SUBSCRIPTION-DELIVERY-POLICY-3',codec['schemas']['D36-policy'])
    if (identity[1],*identity[3:]) != tuple(policy_fields[:4]): return state
    metadata = ledger.charges.get(previous['metadata_key'])
    inventory = ledger.inventory.get(previous['inventory_key'])
    def authenticated(record,receipt):
        return bool(record and record.get('receipt') == receipt == state_hash({k:v for k,v in record.items() if k != 'receipt'}))
    if (not authenticated(metadata,previous['metadata_receipt'])
            or metadata['owner'] != subject or metadata['amount'] != 32*1024 or metadata['state'] != 'active'
            or (metadata['account_kind'],metadata['account']) != (previous['account_kind'],previous['account'])
            or not authenticated(inventory,previous['inventory_receipt'])
            or inventory['owner'] != subject or not inventory['reserved']):
        return state
    backend, key = 'held-delivery-store','held/'+subject.hex()
    object_owner = {'owner':subject,'account_kind':previous['account_kind'],'account':previous['account'],
                    'amount':len(retained),'state':'active','generation':1}
    object_owner['receipt'] = state_hash(object_owner)
    repair_key = 'redrive-repair:'+subject.hex()
    repair_slot = {'owner':subject,'account_kind':previous['account_kind'],'account':previous['account'],
                   'amount':32*1024,'state':'active','generation':1}
    repair_slot['receipt'] = state_hash(repair_slot)
    repair_inventory_key = 'repair-inventory:'+subject.hex()
    repair_inventory = {'owner':subject,'reserved':True,'generation':1,'role':'redrive-repair'}
    repair_inventory['receipt'] = state_hash(repair_inventory)
    expected = {'backend':backend,'key':key,'bytes':retained,
                'receipt':object_receipt(backend,key,retained)}
    origins = getattr(ledger,'capture_origins',{})
    if previous.get('state') == 'observed':
        try:
            original = original_observation(origins[key],previous) if key in origins else previous
            if original['observation_receipt'] != observation_receipt(original): return state
            origin = capture_origin(original,subject,policy['hash'])
            if key in origins and origins[key] != origin: return state
        except (AssertionError,KeyError,TypeError,ValueError): return state
    else:
        origin = origins.get(key)
        if origin is None: return state
        try: original = read_image(decode_record(origin,'HX-EV-CAPTURE-ORIGIN-1',['B32','B','B32','Q','N','B32'])[1])
        except (AssertionError,KeyError,TypeError,ValueError): return state
    capture_preparation = R('HX-EV-CAPTURE-PREPARATION-1',9,subject,state_hash(original),
        previous['metadata_receipt'],previous['inventory_receipt'],U(backend),U(key),previous['carrier_hash'],N(len(retained)),Q(previous['first_observed']))
    if previous.get('state') in {'captured','redriving','closed'}:
        return state if (readback == expected and ledger.objects.get(key) == expected and ledger.charges.get(key) == object_owner
            and ledger.charges.get(repair_key) == repair_slot
            and ledger.inventory.get(repair_inventory_key) == repair_inventory
            and getattr(ledger,'capture_preparations',{}).get(key) == previous['capture_preparation']) else dict(state,transport_copy_acked=False)
    existing_object,existing_charge = ledger.objects.get(key),ledger.charges.get(key)
    if existing_charge is None:
        if (key in origins or getattr(ledger,'capture_preparations',{}).get(key) is not None
                or repair_key in ledger.charges or repair_inventory_key in ledger.inventory
                or len(ledger.inventory) >= ledger.inventory_ceiling): return state
    if existing_object is not None or existing_charge is not None:
        if (existing_charge != object_owner or (existing_object is not None and existing_object != expected) or readback != expected
                or getattr(ledger,'capture_preparations',{}).get(key) != capture_preparation
                or ledger.charges.get(repair_key) != repair_slot):
            return state
        if ledger.inventory.get(repair_inventory_key) != repair_inventory: return state
        # Matching retained partial authority completes once; no second reservation.
        ledger.objects[key] = deepcopy(expected)
    # Preflight the release edge too; a failed provider write must remain recoverable.
    changed = [(previous['account_kind'],previous['account']),
               ('tenant-pool','tenant-pool') if previous['account_kind'] == 'tenant' else ('unidentified','unidentified'),
               ('deployment','deployment')]
    if existing_charge is None and any(ledger.generations.get(key,0) > U64_MAX-2 for key in changed): return state
    if readback != expected:
        return state
    if existing_charge is None and not ledger.reserve(previous['account_kind'],previous['account'],[len(retained),32*1024]):
        return state
    ledger.charges[key] = object_owner
    ledger.charges[repair_key] = repair_slot
    ledger.inventory[repair_inventory_key] = repair_inventory
    if not hasattr(ledger,'capture_origins'): ledger.capture_origins = {}
    ledger.capture_origins[key] = origin
    if not hasattr(ledger,'capture_preparations'): ledger.capture_preparations = {}
    ledger.capture_preparations[key] = capture_preparation
    if crash_at == 'charge': return state
    ledger.objects[key] = deepcopy(expected)
    if crash_at == 'object': return state
    state.update(state='captured',charged_bytes=previous['charged_bytes']+len(retained)+32*1024,
        transport_copy_acked=True,retained_bytes=retained,redrive_bytes=None,redrive_count=0,
        last_redrive_error=None,error_history=(),retained_backend_id=backend,
        retained_object_key=key,readback_authority=expected['receipt'],
        next_recheck_seconds=60,policy_hash=policy['hash'],current_head=current_head.head,capture_preparation=capture_preparation,repair_charge_key=repair_key,
        repair_inventory_key=repair_inventory_key,repair_inventory_receipt=repair_inventory['receipt'],capture_origin_hash=sha256(origin).digest())
    if not hasattr(ledger,'held_entries'): ledger.held_entries={}
    address=held_entry_address(state)
    if address in ledger.held_entries: return deepcopy(previous)
    ledger.held_entries[address]=held_entry_row(state)
    assert held_entry_authority(state,ledger) and held_metadata_bound(state,ledger), 'capture-held-entry-readback'
    return state

def rollback_capture(previous, retained, ledger, successor_absence, deletion_available=True, crash_after=None, object_absence=None):
    state = deepcopy(previous); owner = previous['held_key']; key = 'held/'+owner.hex()
    if (previous['state'] != 'observed' or validate_presence(successor_absence,owner,'capture',1) != 'absent'
            or not deletion_available): return state
    charge = ledger.charges.get(key)
    if charge is None: return state
    cleanup_key=codec['K']('HX-EV-PROVIDER-NATIVE-CLEANUP-KEY-1',U('capture'),owner)
    cleanup_rows = getattr(ledger,'capture_cleanup',{})
    cleanup = cleanup_rows.get(cleanup_key)
    try:
        if cleanup is not None:
            assert cleanup['receipt'] == state_hash({k:v for k,v in cleanup.items() if k != 'receipt'})
            assert cleanup['owner'] == owner and cleanup['amount'] == len(retained)+32768
        else:
            origin = ledger.capture_origins[key]; original = original_observation(origin,previous)
            expected_preparation = R('HX-EV-CAPTURE-PREPARATION-1',9,owner,state_hash(original),previous['metadata_receipt'],
                previous['inventory_receipt'],U('held-delivery-store'),U(key),previous['carrier_hash'],N(len(retained)),Q(previous['first_observed']))
            assert ledger.capture_preparations[key] == expected_preparation and held_key(*previous['identity'],retained) == owner
            for address,amount in ((key,len(retained)),('redrive-repair:'+owner.hex(),32768)):
                row = ledger.charges[address]
                assert row['receipt'] == state_hash({k:v for k,v in row.items() if k != 'receipt'})
                assert (row['owner'],row['account_kind'],row['account'],row['amount'],row['state'],row['generation']) == (
                    owner,previous['account_kind'],previous['account'],amount,'active',1)
            interest = ledger.inventory['repair-inventory:'+owner.hex()]
            assert interest['owner'] == owner and interest['generation'] == 1 and interest['role'] == 'redrive-repair' and interest['reserved']
            assert interest['receipt'] == state_hash({k:v for k,v in interest.items() if k != 'receipt'})
            if key in ledger.objects:
                assert ledger.objects[key] == {'backend':'held-delivery-store','key':key,'bytes':retained,'receipt':object_receipt('held-delivery-store',key,retained)}
            else:
                assert validate_presence(object_absence,owner,'object',1)=='absent', 'capture-object-closure-readback'
        if cleanup is None:
            cleanup = {'owner':owner,'amount':len(retained)+32768,'deleted':{}}
            cleanup['receipt']=state_hash(cleanup)
            cleanup_rows[cleanup_key] = cleanup; ledger.capture_cleanup = cleanup_rows
        # Read back every immutable-byte/interest deletion while charges remain.
        for kind,rows,address in [('object',ledger.objects,key),('origin',ledger.capture_origins,key),
                ('preparation',ledger.capture_preparations,key),('repair-interest',ledger.inventory,'repair-inventory:'+owner.hex())]:
            if kind not in cleanup['deleted']:
                value = rows.get(address)
                digest = state_hash(value)
                rows.pop(address,None)
                cleanup['deleted'][kind] = sha256(b'provider-capture-deletion:'+owner+U(kind)+digest).digest()
                assert len(image_bytes(cleanup)) <= 4096
                cleanup['receipt'] = state_hash({k:v for k,v in cleanup.items() if k != 'receipt'})
                if crash_after == kind: return state
            assert len(cleanup['deleted'][kind]) == 32 and address not in rows
        trial = deepcopy(ledger)
        if not trial.refund(previous['account_kind'],previous['account'],cleanup['amount']): return state
        # Deletion receipts and all counter generations are preflighted. Final
        # charge deletion plus refund is one serializable ledger transaction.
        del ledger.charges[key]; del ledger.charges['redrive-repair:'+owner.hex()]
        assert ledger.refund(previous['account_kind'],previous['account'],cleanup['amount'])
        del cleanup_rows[cleanup_key]
        return state
    except (AssertionError,KeyError,TypeError,ValueError): return state

def preparation_authority(previous, ledger):
    preparation = previous.get('capture_preparation'); key = previous.get('retained_object_key')
    if not isinstance(preparation,bytes) or not preparation: return False
    try:
        fields = decode_record(preparation,'HX-EV-CAPTURE-PREPARATION-1',codec['extra_schemas']['D36-capture-preparation'])
        origin = getattr(ledger,'capture_origins',{}).get(key)
        if origin is None or sha256(origin).digest() != previous['capture_origin_hash']: return False
        origin_fields = decode_record(origin,'HX-EV-CAPTURE-ORIGIN-1',['B32','B','B32','Q','N','B32'])
        if origin_fields[5] != previous['policy_hash']: return False
        original = read_image(origin_fields[1])
        assert original['identity'] == codec['capture_projection'](previous)['identity'] and original['account'] == sha256(U(previous['account'])).digest()
        original['identity'],original['account'] = previous['identity'],previous['account']
        original_hash = state_hash(original)
        if fields != (previous['held_key'],original_hash,previous['metadata_receipt'],previous['inventory_receipt'],
                      'held-delivery-store',key,previous['carrier_hash'],len(previous['retained_bytes']),previous['first_observed']): return False
    except (AssertionError,KeyError,TypeError,ValueError): return False
    return getattr(ledger,'capture_preparations',{}).get(key) == previous.get('capture_preparation')

def retained_authority(previous, ledger):
    if ledger is None: return False
    retained = previous.get('retained_bytes'); key = previous.get('retained_object_key')
    if (not isinstance(retained,bytes) or not key or previous.get('retained_backend_id') != 'held-delivery-store'
            or sha256(retained).digest() != previous['carrier_hash']
            or held_key(*previous['identity'],retained) != previous['held_key']): return False
    expected = {'backend':'held-delivery-store','key':key,'bytes':retained,
                'receipt':object_receipt('held-delivery-store',key,retained)}
    charge = ledger.charges.get(key)
    def charge_authority(address, amount, receipt=None):
        row = ledger.charges.get(address)
        return bool(row and row['receipt'] == state_hash({k:v for k,v in row.items() if k != 'receipt'})
            and (receipt is None or row['receipt'] == receipt) and row['owner'] == previous['held_key']
            and row['generation'] == 1 and row['state'] == 'active'
            and (row['account_kind'],row['account'],row['amount']) == (previous['account_kind'],previous['account'],amount))
    def interest_authority(address, receipt, role=None):
        row = ledger.inventory.get(address)
        return bool(row and row['receipt'] == receipt == state_hash({k:v for k,v in row.items() if k != 'receipt'})
            and row['owner'] == previous['held_key'] and row['generation'] == 1 and row['reserved']
            and row.get('role') == role)
    if not preparation_authority(previous,ledger): return False
    if (not charge_authority(previous['metadata_key'],32*1024,previous['metadata_receipt'])
            or not interest_authority(previous['inventory_key'],previous['inventory_receipt'])
            or not charge_authority(previous['repair_charge_key'],32*1024)
            or not interest_authority(previous['repair_inventory_key'],previous['repair_inventory_receipt'],'redrive-repair')): return False
    return (key == 'held/'+previous['held_key'].hex() and ledger.objects.get(key) == expected
            and previous['readback_authority'] == expected['receipt']
            and charge is not None and charge['receipt'] == state_hash({k:v for k,v in charge.items() if k != 'receipt'})
            and charge['owner'] == previous['held_key'] and charge['state'] == 'active' and charge['generation'] == 1
            and (charge['account_kind'],charge['account'],charge['amount']) ==
                (previous['account_kind'],previous['account'],len(retained)))

def signed_redrive_request(previous, issuer='admin', subject='operator', request_utc=None):
    count = checked_add(previous['redrive_count'],1)
    utc = previous['first_observed'] if request_utc is None else request_utc
    if count is None or type(utc) is not int or not previous['first_observed'] <= utc < (1<<63): return None
    scope,_,tenant,*_ = previous['identity']
    raw = R('HX-EV-REDRIVE-REQUEST-2',7,U(issuer),U(scope),O(None if tenant is None else U(tenant)),
        previous['held_key'],N(previous['redrive_count']),U(subject),Q(utc))
    decode_record(raw,'HX-EV-REDRIVE-REQUEST-2',codec['schemas']['D36-redrive'])
    row = {'owner':previous['held_key'],'count':count,'raw':raw,
           'signature':sha256(b'authenticated-purpose-2d-redrive:'+raw).digest()}
    row['receipt'] = sha256(b'provider-redrive-request-readback:'+image_bytes(row)).digest()
    return row

def redrive_request_authority(previous, row, count):
    if not isinstance(row,dict) or set(row) != {'owner','count','raw','signature','receipt'}: return False
    try:
        fields = decode_record(row['raw'],'HX-EV-REDRIVE-REQUEST-2',codec['schemas']['D36-redrive'])
        scope,_,tenant,*_ = previous['identity']
        return (row['owner'] == previous['held_key'] and row['count'] == count > 0
            and fields[:4] == ('admin',scope,tenant,previous['held_key']) and fields[4] == count-1
            and fields[5] == 'operator' and previous['first_observed'] <= fields[6] < (1<<63)
            and row['signature'] == sha256(b'authenticated-purpose-2d-redrive:'+row['raw']).digest()
            and row['receipt'] == sha256(b'provider-redrive-request-readback:'+image_bytes({k:v for k,v in row.items() if k != 'receipt'})).digest())
    except (AssertionError,KeyError,TypeError,ValueError): return False

def redrive_attempt_authority(previous, row, request, count):
    if not isinstance(row,dict) or not redrive_request_authority(previous,request,count): return False
    try:
        fields = decode_record(row['raw'],'HX-EV-REDRIVE-ATTEMPT-1',codec['extra_schemas']['D36-attempt'])
        request_hash = sha256(request['raw']).digest()
        return (fields == (previous['held_key'],count,previous['carrier_hash'],previous['retained_object_key'],
            previous['metadata_receipt'],previous['first_observed'],request_hash)
            and (row['owner'],row['count'],row['carrier_hash'],row['locator'],row['metadata_receipt'],row['request_hash']) ==
                (fields[0],fields[1],fields[2],fields[3],fields[4],fields[6])
            and row['receipt'] == state_hash({k:v for k,v in row.items() if k != 'receipt'}))
    except (AssertionError,KeyError,TypeError,ValueError): return False

def held_entry_address(state):
    return 'held-delivery:'+state['held_key'].hex()

def held_record_bytes(state, revision):
    scope,deployment,tenant,component,topic,subscription=state['identity']
    raw=R('HX-EV-HELD-DELIVERY-4',22,U(scope),U(deployment),O(None if tenant is None else U(tenant)),O(None),
        U(component),U(topic),U(subscription),state['policy_hash'],U(state['state']),
        U('handler-capability-hold'),N(len(state['retained_bytes'])),state['carrier_hash'],
        O(U(state['retained_backend_id'])),O(U(state['retained_object_key'])),O(state['readback_authority']),O(state['readback_authority']),
        Q(state['first_observed']),N(state['delivery_attempt_count']),N(state['redrive_count']),
        O(state['last_redrive_error']),Q(state['first_observed']+state['next_recheck_seconds']*10000000),N(revision))
    decode_record(raw,'HX-EV-HELD-DELIVERY-4',codec['schemas']['D36-held'])
    assert len(raw)<=12*1024
    return raw

def held_entry_row(state,old=None):
    generation=1 if old is None else checked_add(old['generation'],1)
    assert generation is not None
    predecessor=bytes(32) if old is None else sha256(old['raw']).digest()
    key=held_entry_address(state)
    errors=tuple(state.get('error_history',()))
    assert len(errors)<=64 and all(isinstance(v,bytes) and len(v)==32 for v in errors)
    row={'owner':state['held_key'],'raw':held_record_bytes(state,generation),'generation':generation,
         'predecessor':predecessor,'count':state['redrive_count'],'state':state['state'],'errors':b''.join(errors),
         'repair_open':bool(state.get('repair_indexed') or state.get('repair_required') or state.get('repaired_record') or state.get('repair_cleanup'))}
    row['receipt']=sha256(b'provider-held-entry-readback:'+U(key)+image_bytes(row)).digest()
    return row

def held_entry_authority(state,ledger):
    if ledger is None: return False
    row=getattr(ledger,'held_entries',{}).get(held_entry_address(state))
    if not isinstance(row,dict): return False
    try:
        if not isinstance(row['errors'],bytes) or len(row['errors'])>2048 or len(row['errors'])%32: return False
        if row['owner']!=state['held_key'] or row['count']!=state['redrive_count'] or row['state']!=state['state']: return False
        if row['raw']!=held_record_bytes(state,row['generation']): return False
        if row['generation']<1 or (row['generation']==1)!=(row['predecessor']==bytes(32)): return False
        return row['receipt']==sha256(b'provider-held-entry-readback:'+U(held_entry_address(state))+
            image_bytes({k:v for k,v in row.items() if k!='receipt'})).digest()
    except (AssertionError,KeyError,TypeError,ValueError): return False

def persist_held_transition(previous,state,ledger):
    if not held_entry_authority(previous,ledger): return False
    key=held_entry_address(previous); old=ledger.held_entries[key]
    row=held_entry_row(state,old)
    staged=deepcopy(ledger.held_entries); staged[key]=row
    assert staged[key]['receipt']==row['receipt'] and staged[key]['predecessor']==sha256(old['raw']).digest()
    ledger.held_entries=staged
    return True

def restart_held_delivery(held_key,ledger):
    # Address plus typed held record and authenticated original backend rows;
    # no process-returned held dictionary is read.
    address='held-delivery:'+held_key.hex()
    row=getattr(ledger,'held_entries',{}).get(address)
    if not isinstance(row,dict) or row.get('owner')!=held_key: return None
    if row.get('receipt')!=sha256(b'provider-held-entry-readback:'+U(address)+
            image_bytes({k:v for k,v in row.items() if k!='receipt'})).digest(): return None
    try:
        f=decode_record(row['raw'],'HX-EV-HELD-DELIVERY-4',codec['schemas']['D36-held'])
        identity=(f[0],f[1],f[2],f[4],f[5],f[6])
        assert held_key==row['owner'] and f[18]==row['count'] and f[8]==row['state']
        object_key=f[13]; obj=ledger.objects[object_key]
        assert obj['backend']==f[12] and obj['key']==object_key and obj['receipt']==f[14]
        carrier=obj['bytes']; assert sha256(carrier).digest()==f[11] and len(carrier)==f[10]
        assert held_key==held_key_for_restart(identity,carrier)
        metadata_key='metadata:'+held_key.hex(); inventory_key='inventory:'+held_key.hex()
        repair_key='redrive-repair:'+held_key.hex(); repair_inventory_key='repair-inventory:'+held_key.hex()
        metadata=ledger.charges[metadata_key]; inventory=ledger.inventory[inventory_key]
        repair_interest=ledger.inventory[repair_inventory_key]
        account_kind='tenant' if f[0]=='tenant' else 'capture-scope'
        account=f[2] if f[0]=='tenant' else 'capture:'+held_key.hex()
        prep=ledger.capture_preparations[object_key]; origin=ledger.capture_origins[object_key]
        state={'identity':identity,'held_key':held_key,'carrier_hash':f[11],
            'metadata_key':metadata_key,'inventory_key':inventory_key,
            'metadata_receipt':metadata['receipt'],'inventory_receipt':inventory['receipt'],
            'account_kind':account_kind,'account':account,'state':f[8],
            'transport_copy_acked':True,'route_success':f[8]=='closed','closed':f[8]=='closed',
            'first_observed':f[16],'delivery_attempt_count':f[17],
            'charged_bytes':64*1024+len(carrier),'indexed':True,'operator_visible':True,
            'retained_bytes':carrier,'redrive_bytes':None,'redrive_count':f[18],
            'last_redrive_error':f[19],'error_history':tuple(row['errors'][i:i+32] for i in range(0,len(row['errors']),32)),
            'retained_backend_id':f[12],'retained_object_key':object_key,
            'readback_authority':f[14],'next_recheck_seconds':(f[20]-f[16])//10000000,
            'policy_hash':f[7],'current_head':f[7],'capture_preparation':prep,
            'repair_charge_key':repair_key,'repair_inventory_key':repair_inventory_key,
            'repair_inventory_receipt':repair_interest['receipt'],'capture_origin_hash':sha256(origin).digest()}
        if f[18]:
            key=(held_key,1)
            state['request']=deepcopy(ledger.redrive_requests[key])
            state['attempt']=deepcopy(ledger.redrive_attempts[key])
        rkey,ekey=repair_record_address(state),repair_entry_address(state)
        repair=getattr(ledger,'repair_records',{}).get(rkey)
        entry=getattr(ledger,'repair_entries',{}).get(ekey)
        cleanup_row=getattr(ledger,'repair_cleanup_rows',{}).get(repair_cleanup_address(state))
        cleanup=repair_cleanup_view(state,cleanup_row) if cleanup_row is not None else None
        if cleanup_row is not None and cleanup is None: return None
        if row.get('repair_open') and repair is None and entry is None and cleanup is None: return None
        completion=getattr(ledger,'repair_completion',{}).get(repair_cleanup_address(state))
        if completion is not None:
            completed=repair_completion_view(state,completion)
            if completed is None: return None
            if completed[0]==row['count']: state['repair_cleanup_receipt']=completed[1]
        if cleanup is not None:
            assert cleanup['phase'] in {'repaired-readback','record-deleted','record-readback','entry-deleted'}
            assert repair_index_hash_authority(state,ledger,cleanup['entry'])
            assert len(image_bytes(cleanup))<=1024
            if repair is None:
                assert cleanup['phase']!='repaired-readback'
                assert ledger.repair_deletions[('record',rkey)]==cleanup['record_deletion']
            else:
                assert cleanup['phase']=='repaired-readback'
                assert repair_native_valid(rkey,repair,held_key) and sha256(repair['raw']).digest()==cleanup['record']
            if entry is None:
                assert cleanup['phase']=='entry-deleted'
                assert ledger.repair_deletions[('entry',ekey)]==cleanup['entry_deletion']
            else:
                assert repair_native_valid(ekey,entry,held_key) and sha256(entry['raw']).digest()==cleanup['entry']
            state['repair_cleanup']=deepcopy(cleanup)
            state['repair_cleanup_bytes']=cleanup_row['raw']
            state['repair_required']=None
            state['repaired_record']=None if repair is None else repair['raw']
            state['repair_entry']=None if entry is None else entry['raw']
            state['repair_required_record_hash']=cleanup['required_hash']
            state['repair_receipt']=cleanup['repair_receipt']
            state['repair_indexed']=True
        elif repair is not None or entry is not None:
            if not repair_native_valid(rkey,repair,held_key) or not repair_native_valid(ekey,entry,held_key): return None
            assert repair_index_authority(state,ledger,entry['raw'])
            fields=decode_record(repair['raw'],'HX-EV-REDRIVE-REPAIR-1',codec['extra_schemas']['D36-repair'],
                repair_predecessor=None if repair['generation']==1 else repair['predecessor'])
            state['repair_entry']=entry['raw']; state['repair_indexed']=True
            if fields[7]=='required':
                state['repair_required']={'owner':held_key,'count':row['count'],'carrier_hash':f[11],
                    'locator':object_key,'raw':repair['raw']}
            else:
                state['repair_required']=None; state['repaired_record']=repair['raw']
                state['repair_required_record_hash']=repair['predecessor']; state['repair_receipt']=fields[8]
        assert held_entry_authority(state,ledger) and retained_authority(state,ledger)
        assert bool(state.get('repair_indexed'))==row['repair_open']
        return state
    except (AssertionError,KeyError,TypeError,ValueError): return None

def held_key_for_restart(identity,carrier):
    return held_key(*identity,carrier)

def held_metadata_bound(state,ledger):
    key=state['retained_object_key']
    row=getattr(ledger,'held_entries',{}).get(held_entry_address(state))
    origin=getattr(ledger,'capture_origins',{}).get(key)
    preparation=getattr(ledger,'capture_preparations',{}).get(key)
    if not isinstance(row,dict) or not isinstance(origin,bytes) or not isinstance(preparation,bytes): return False
    return (len(row['raw'])+len(row['errors'])+256+
        len(origin)+len(preparation)+
        len(image_bytes(getattr(ledger,'repair_cleanup_rows',{}).get(repair_cleanup_address(state),{})))+
        len(image_bytes(getattr(ledger,'repair_completion',{}).get(repair_cleanup_address(state),{})))<=32*1024)

def held_delivery(previous, cause_cleared=False, routes_terminal=False, redrive_failed=False, ledger=None,
                  request=None, request_readback_available=True, request_delete_available=True,
                  held_readback_available=True, crash_after=None):
    state = deepcopy(previous)
    if state['state'] == 'closed': return state
    if ledger is not None and not held_entry_authority(previous,ledger): return deepcopy(previous)
    if cause_cleared and state['state'] == 'captured':
        if state.get('repair_required') or not retained_authority(previous,ledger): return deepcopy(previous)
        if (ledger.held_entries[held_entry_address(previous)]['repair_open']
                or getattr(ledger,'repair_records',{}).get(repair_record_address(previous)) is not None
                or getattr(ledger,'repair_entries',{}).get(repair_entry_address(previous)) is not None
                or getattr(ledger,'repair_cleanup_rows',{}).get(repair_cleanup_address(previous)) is not None): return deepcopy(previous)
        if state.get('repaired_record') or state.get('repair_cleanup'): return deepcopy(previous)
        state['state'] = 'redriving'
        next_count = checked_add(state['redrive_count'],1)
        if next_count is None: return deepcopy(previous)
        if not request_readback_available: return deepcopy(previous)
        requests = deepcopy(getattr(ledger,'redrive_requests',{}))
        address = (state['held_key'],1)  # fixed active/disputed request slot
        old_request = requests.get(address)
        if previous['redrive_count'] == 0:
            if old_request is not None or previous.get('request') is not None: return deepcopy(previous)
        else:
            if not redrive_request_authority(previous,old_request,previous['redrive_count']): return deepcopy(previous)
            if old_request != previous.get('request'): return deepcopy(previous)
            if previous['attempt'].get('request_hash') != sha256(old_request['raw']).digest(): return deepcopy(previous)
        request = signed_redrive_request(previous) if request is None else request
        if not redrive_request_authority(previous,request,next_count): return deepcopy(previous)
        request_deletions = deepcopy(getattr(ledger,'request_deletions',{}))
        if old_request is not None:
            if not request_delete_available: return deepcopy(previous)
            request_deletions[address] = sha256(b'provider-redrive-request-deletion:'+old_request['receipt']).digest()
            del requests[address]
        assert address not in requests
        if crash_after == 'request-delete-readback': return deepcopy(previous)
        requests[address] = deepcopy(request)
        if crash_after == 'request-readback': return deepcopy(previous)
        state['redrive_count'] = next_count
        state['request'] = deepcopy(request)
        state['redrive_bytes'] = state['retained_bytes']
        attempt = {'owner':state['held_key'],'count':next_count,'carrier_hash':state['carrier_hash'],
                   'locator':state['retained_object_key'],'metadata_receipt':state['metadata_receipt'],
                   'request_hash':sha256(request['raw']).digest()}
        attempt['raw'] = R('HX-EV-REDRIVE-ATTEMPT-1',7,state['held_key'],N(next_count),state['carrier_hash'],
            U(state['retained_object_key']),state['metadata_receipt'],Q(state['first_observed']),attempt['request_hash'])
        decode_record(attempt['raw'],'HX-EV-REDRIVE-ATTEMPT-1',codec['extra_schemas']['D36-attempt'])
        attempt['receipt'] = state_hash(attempt)
        state['attempt'] = attempt
        attempts = deepcopy(getattr(ledger,'redrive_attempts',{}))
        address = (state['held_key'],1)  # one current/disputed authority slot
        old = attempts.get(address)
        if (old is None and previous['redrive_count'] != 0) or (old is not None and old != previous.get('attempt')):
            return deepcopy(previous)
        if old is not None:
            if (old['receipt'] != state_hash({k:v for k,v in old.items() if k != 'receipt'})
                    or old['count'] != previous['redrive_count']): return deepcopy(previous)
            if not redrive_attempt_authority(previous,old,old_request,previous['redrive_count']): return deepcopy(previous)
        attempt_deletions = deepcopy(getattr(ledger,'attempt_deletions',{}))
        if old is not None:
            attempt_deletions[address] = sha256(b'provider-attempt-deletion:'+old['receipt']).digest()
            del attempts[address]
        assert address not in attempts
        attempts[address] = deepcopy(attempt)
        # Provider-model serializable transaction: detached writes/deletion
        # readbacks publish together with the held-count CAS, or abort unchanged.
        # One detached serializable model transaction: no backend attribute is
        # changed until every count, generation and pair readback passes.
        old_held=ledger.held_entries[held_entry_address(previous)]
        if not held_readback_available or not held_entry_authority(previous,ledger): return deepcopy(previous)
        next_held=held_entry_row(state,old_held)
        staged=deepcopy(ledger)
        staged.redrive_requests,staged.request_deletions=requests,request_deletions
        staged.redrive_attempts,staged.attempt_deletions=attempts,attempt_deletions
        staged.held_entries[held_entry_address(previous)]=next_held
        if (next_held['predecessor']!=sha256(old_held['raw']).digest() or next_held['count']!=next_count
                or not held_entry_authority(state,staged) or not held_metadata_bound(state,staged)
                or staged.redrive_requests[address]!=request or staged.redrive_attempts[address]!=attempt):
            return deepcopy(previous)
        if crash_after == 'held-precommit': return deepcopy(previous)
        ledger.__dict__=staged.__dict__
        assert held_entry_authority(state,ledger), 'redrive-held-count-pair-readback'
        if crash_after == 'request-attempt-commit':
            state['redrive_bytes'] = None
            return state
        # The send state is committed above; terminal route evidence follows
        # as a separate checked held-row transition below.
    if state['state'] == 'redriving':
        transition_prior=deepcopy(state)
        if routes_terminal:
            state.update(state='closed',closed=True,route_success=True)
        elif redrive_failed:
            state['state'] = 'captured'
            state['last_redrive_error'] = sha256(b'typed-redrive-error'+N(state['redrive_count'])).digest()
            state['error_history'] = (state['error_history'] + (state['last_redrive_error'],))[-64:]
            state['next_recheck_seconds'] = min(900,60*(2**min(state['redrive_count'],4)))
        if ledger is not None and state['state'] != transition_prior['state']:
            if not persist_held_transition(transition_prior,state,ledger): return deepcopy(previous)
    return state

def repair_cleanup_address(state):
    return codec['K']('HX-EV-REDRIVE-CLEANUP-KEY-1',state['held_key'])

def repair_cleanup_native_row(state,cleanup,old=None):
    address=repair_cleanup_address(state)
    if old is not None: assert repair_cleanup_view(state,old) is not None, 'repair-cleanup-predecessor-readback'
    generation=1 if old is None else checked_add(int.from_bytes(old['support'][:8],'big'),1)
    assert generation is not None
    raw=R('HX-EV-REDRIVE-CLEANUP-1',7,state['held_key'],N(state['redrive_count']),cleanup['record'],cleanup['entry'],
        U(cleanup['phase']),O(cleanup.get('record_deletion')),O(cleanup.get('entry_deletion')))
    decode_record(raw,'HX-EV-REDRIVE-CLEANUP-1',codec['extra_schemas']['D36-cleanup'])
    support=(generation.to_bytes(8,'big')+(bytes(32) if old is None else sha256(old['raw']).digest())+
        cleanup['record_receipt']+cleanup['entry_receipt']+cleanup['required_hash']+cleanup['repair_receipt'])
    row={'raw':raw,'support':support}
    row['receipt']=sha256(b'provider-repair-cleanup-readback:'+U(address)+image_bytes(row)).digest()
    assert len(image_bytes(row))<=1024, 'repair-cleanup-native-bound'
    assert repair_cleanup_view(state,row)==cleanup, 'repair-cleanup-native-readback'
    return row

def repair_cleanup_view(state,row):
    if not isinstance(row,dict): return None
    try:
        address=repair_cleanup_address(state)
        support=row['support']; assert isinstance(support,bytes) and len(support)==168
        generation=int.from_bytes(support[:8],'big'); predecessor=support[8:40]
        assert 1<=generation<=U64_MAX
        assert (generation==1)==(predecessor==bytes(32))
        assert len(image_bytes(row))<=1024
        assert row['receipt']==sha256(b'provider-repair-cleanup-readback:'+U(address)+
            image_bytes({k:v for k,v in row.items() if k!='receipt'})).digest()
        fields=decode_record(row['raw'],'HX-EV-REDRIVE-CLEANUP-1',codec['extra_schemas']['D36-cleanup'])
        assert fields[:2]==(state['held_key'],state['redrive_count'])
        cleanup={'record':fields[2],'entry':fields[3],'record_receipt':support[40:72],
            'entry_receipt':support[72:104],'required_hash':support[104:136],
            'repair_receipt':support[136:168],'phase':fields[4]}
        if fields[5] is not None: cleanup['record_deletion']=fields[5]
        if fields[6] is not None: cleanup['entry_deletion']=fields[6]
        return cleanup
    except (AssertionError,KeyError,TypeError,ValueError): return None

def repair_completion_view(state,row):
    if not isinstance(row,dict): return None
    try:
        raw=row['raw']; assert isinstance(raw,bytes) and len(raw)==48
        generation=int.from_bytes(raw[:8],'big'); count=int.from_bytes(raw[8:16],'big')
        assert 1<generation<=U64_MAX and 0<count<=state['redrive_count']
        assert len(image_bytes(row))<=256
        assert row['native_receipt']==sha256(b'provider-repair-completion-readback:'+U(repair_cleanup_address(state))+
            image_bytes({k:v for k,v in row.items() if k!='native_receipt'})).digest()
        return count,raw[16:]
    except (AssertionError,KeyError,TypeError,ValueError): return None

def repair_index_address(state):
    scope=state['identity'][0]
    scope_id=state['identity'][2] if scope=='tenant' else state['identity'][1]
    return codec['K']('HX-EV-HOLD-INDEX-KEY-1',U(scope),U(scope_id))

def repair_index_scope(state):
    scope=state['identity'][0]
    return scope,state['identity'][2] if scope=='tenant' else state['identity'][1]

def repair_index_member(state,entry_hash):
    return (state['first_observed'],'RedriveEvidenceRepairHold',
        state['held_key'].hex()+':'+str(state['redrive_count']),entry_hash)

def repair_index_view(state,row):
    if not isinstance(row,dict): return None
    try:
        address=repair_index_address(state); scope,scope_id=repair_index_scope(state)
        raw=row['raw']; fields=decode_record(raw,'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index'])
        assert fields[:2]==(scope,scope_id) and fields[5]==0 and len(raw)<=40*MiB
        assert type(row['generation']) is int and row['generation']==fields[2]
        assert row['predecessor']==fields[6]
        assert row['receipt']==sha256(b'provider-hold-index-readback:'+U(address)+
            image_bytes({k:v for k,v in row.items() if k!='receipt'})).digest()
        return codec['decode_rows'](fields[4],fields[3],['Q','U','U','B32'],[1024,1024,4096,1024])
    except (AssertionError,KeyError,TypeError,ValueError): return None

def repair_index_row(state,rows,old=None):
    prior=repair_index_view(state,old) if old is not None else None
    if old is not None: assert prior is not None, 'repair-index-old-readback'
    generation=1 if old is None else checked_add(old['generation'],1)
    assert generation is not None and len(rows)<=10000
    rows=sorted(rows,key=lambda row:(row[0],row[1].encode(),row[2].encode()))
    assert len({(row[1],row[2]) for row in rows})==len(rows)
    scope,scope_id=repair_index_scope(state)
    predecessor=bytes(32) if old is None else sha256(old['raw']).digest()
    updated_at=state['first_observed'] if old is None else max(state['first_observed'],
        decode_record(old['raw'],'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index'])[7])
    raw=R('HX-EV-HOLD-INDEX-2',8,U(scope),U(scope_id),N(generation),N(len(rows)),
        B(b''.join(Q(at)+U(kind)+U(subject)+digest for at,kind,subject,digest in rows)),N(0),predecessor,Q(updated_at))
    row={'raw':raw,'generation':generation,'predecessor':predecessor}
    row['receipt']=sha256(b'provider-hold-index-readback:'+U(repair_index_address(state))+image_bytes(row)).digest()
    assert repair_index_view(state,row)==rows, 'repair-index-typed-readback'
    return row

def repair_index_row_valid(state,row):
    return repair_index_view(state,row) is not None

def repair_index_authority(state,ledger,entry_raw):
    return repair_index_hash_authority(state,ledger,sha256(entry_raw).digest())

def repair_index_hash_authority(state,ledger,entry_hash):
    address=repair_index_address(state)
    index=getattr(ledger,'hold_indexes',{}).get(address)
    rows=repair_index_view(state,index)
    return rows is not None and repair_index_member(state,entry_hash) in rows

def repair_record_address(state):
    return codec['K']('HX-EV-REDRIVE-REPAIR-KEY-1',state['held_key'],N(state['redrive_count']))

def repair_entry_address(state):
    scope=state['identity'][0]
    scope_id=state['identity'][2] if scope=='tenant' else state['identity'][1]
    subject=state['held_key'].hex()+':'+str(state['redrive_count'])
    return codec['K']('HX-EV-HOLD-ENTRY-KEY-1',U(scope),U(scope_id),U('RedriveEvidenceRepairHold'),U(subject))

def repair_native_row(address,owner,raw,generation=1,predecessor=None):
    predecessor=bytes(32) if predecessor is None else predecessor
    row={'owner':owner,'raw':raw,'generation':generation,'predecessor':predecessor}
    row['receipt']=sha256(b'provider-repair-readback:'+U(address)+image_bytes(row)).digest()
    return row

def repair_native_valid(address,row,owner,raw=None):
    return bool(isinstance(row,dict) and row.get('owner')==owner and
        (raw is None or row.get('raw')==raw) and type(row.get('generation')) is int and 1<=row['generation']<=U64_MAX and
        isinstance(row.get('predecessor'),bytes) and len(row['predecessor'])==32 and
        row.get('receipt')==sha256(b'provider-repair-readback:'+U(address)+
            image_bytes({k:v for k,v in row.items() if k!='receipt'})).digest())

def repair_backend_authority(state,ledger,required=True):
    if ledger is None: return False
    rkey,ekey=repair_record_address(state),repair_entry_address(state)
    r=getattr(ledger,'repair_records',{}).get(rkey)
    e=getattr(ledger,'repair_entries',{}).get(ekey)
    if not repair_native_valid(rkey,r,state['held_key']) or not repair_native_valid(ekey,e,state['held_key']): return False
    if not repair_index_authority(state,ledger,e['raw']): return False
    try:
        fields=decode_record(r['raw'],'HX-EV-REDRIVE-REPAIR-1',codec['extra_schemas']['D36-repair'],
            repair_predecessor=None if r['generation']==1 else r['predecessor'])
        entry=decode_record(e['raw'],'HX-EV-HOLD-ENTRY-2',codec['schemas']['D37-entry'])
        if (fields[0],fields[1],fields[3],fields[4],fields[5],fields[6],fields[7]) != (
            state['held_key'],state['redrive_count'],state['carrier_hash'],state['retained_object_key'],
            r['generation'],r['predecessor'],'required' if required else 'repaired'): return False
        if entry[2:4] != ('RedriveEvidenceRepairHold',state['held_key'].hex()+':'+str(state['redrive_count'])): return False
        if e['generation']!=1 or e['predecessor']!=bytes(32) or entry[11]!='operations': return False
        if required and r['raw']!=state['repair_required']['raw']: return False
        if not required and r['raw']!=state['repaired_record']: return False
        return True
    except (AssertionError,KeyError,TypeError,ValueError): return False

def provider_delete_repair(ledger,kind,address,expected):
    rows=ledger.repair_records if kind=='record' else ledger.repair_entries
    if address not in rows or rows[address]!=expected: return None
    receipt=sha256(b'provider-native-repair-deletion:'+U(kind)+U(address)+expected['receipt']).digest()
    del rows[address]
    if not hasattr(ledger,'repair_deletions'): ledger.repair_deletions={}
    ledger.repair_deletions[(kind,address)]=receipt
    return receipt if address not in rows else None

def reconcile_redrive(previous, evidence='unavailable', evidence_receipt=None, ledger=None, crash_after=None):
    if previous['state'] != 'redriving': return deepcopy(previous)
    attempt = previous.get('attempt'); state = deepcopy(previous)
    current_request = getattr(ledger,'redrive_requests',{}).get((previous['held_key'],1)) if ledger is not None else None
    current_attempt = getattr(ledger,'redrive_attempts',{}).get((previous['held_key'],1)) if ledger is not None else None
    if (not redrive_request_authority(previous,current_request,previous['redrive_count'])
            or current_request != previous.get('request')
            or current_attempt != attempt
            or not redrive_attempt_authority(previous,current_attempt,current_request,previous['redrive_count'])
            or not attempt or attempt.get('request_hash') != sha256(current_request['raw']).digest()
            or attempt['receipt'] != state_hash({k:v for k,v in attempt.items() if k != 'receipt'})
            or attempt['owner'] != previous['held_key'] or attempt['count'] != previous['redrive_count']
            or attempt['carrier_hash'] != previous['carrier_hash']
            or attempt['locator'] != previous['retained_object_key']
            or attempt['metadata_receipt'] != previous['metadata_receipt']):
        state.update(state='captured',last_redrive_error=sha256(b'redrive-attempt-evidence-conflict').digest(),next_recheck_seconds=900,
                     repair_required={'owner':previous['held_key'],'count':previous['redrive_count'],
                                      'carrier_hash':previous['carrier_hash'],'locator':previous['retained_object_key']})
        state['repair_required']['raw'] = R('HX-EV-REDRIVE-REPAIR-1',10,previous['held_key'],N(previous['redrive_count']),
            state_hash(attempt),previous['carrier_hash'],U(previous['retained_object_key']),N(1),bytes(32),U('required'),O(None),Q(previous['first_observed']))
        scope = previous['identity'][0]; scope_id = previous['identity'][2] if scope == 'tenant' else previous['identity'][1]
        subject = previous['held_key'].hex()+':'+str(previous['redrive_count'])
        state['repair_entry'] = R('HX-EV-HOLD-ENTRY-2',13,U(scope),U(scope_id),U('RedriveEvidenceRepairHold'),U(subject),O(None),
            U('redrive_evidence_repair_hold'),N(1),bytes(32),Q(previous['first_observed']),Q(previous['first_observed']),
            N(1),U('operations'),Q(previous['first_observed']+9000000000))
        decode_record(state['repair_entry'],'HX-EV-HOLD-ENTRY-2',codec['schemas']['D37-entry'])
        state['repair_indexed'] = True
        if ledger is None: return deepcopy(previous)
        if not held_entry_authority(previous,ledger):
            recovered=restart_held_delivery(previous['held_key'],ledger)
            if (recovered is not None and recovered.get('repair_required') is not None
                    and recovered['redrive_count']==previous['redrive_count']
                    and recovered['repair_required']['raw']==state['repair_required']['raw']
                    and recovered['repair_entry']==state['repair_entry']): return recovered
            return deepcopy(previous)
        rkey,ekey=repair_record_address(state),repair_entry_address(state)
        if getattr(ledger,'repair_records',{}).get(rkey) is not None or getattr(ledger,'repair_entries',{}).get(ekey) is not None:
            return deepcopy(previous)
        record=repair_native_row(rkey,state['held_key'],state['repair_required']['raw'])
        entry=repair_native_row(ekey,state['held_key'],state['repair_entry'])
        staged_records=deepcopy(getattr(ledger,'repair_records',{})); staged_entries=deepcopy(getattr(ledger,'repair_entries',{}))
        staged_records[rkey]=record; staged_entries[ekey]=entry
        if crash_after=='repair-record-stage': return deepcopy(previous)
        ikey=repair_index_address(state); old_index=getattr(ledger,'hold_indexes',{}).get(ikey)
        members=[] if old_index is None else repair_index_view(state,old_index)
        if members is None: return deepcopy(previous)
        member=repair_index_member(state,sha256(entry['raw']).digest())
        if any(row[1:3]==member[1:3] for row in members): return deepcopy(previous)
        members.append(member)
        staged_indexes=deepcopy(getattr(ledger,'hold_indexes',{}))
        staged_indexes[ikey]=repair_index_row(state,members,old_index)
        if crash_after=='repair-index-stage': return deepcopy(previous)
        old_held=ledger.held_entries[held_entry_address(previous)]
        next_held=held_entry_row(state,old_held)
        held_rows=deepcopy(ledger.held_entries); held_rows[held_entry_address(previous)]=next_held
        if crash_after=='repair-held-stage': return deepcopy(previous)
        staged=deepcopy(ledger)
        staged.repair_records,staged.repair_entries,staged.held_entries=staged_records,staged_entries,held_rows
        staged.hold_indexes=staged_indexes
        if not repair_backend_authority(state,staged) or not held_entry_authority(state,staged): return deepcopy(previous)
        ledger.__dict__=staged.__dict__
        assert repair_backend_authority(state,ledger), 'repair-required-native-readback'
        supplementary_records['D36-repair'] = state['repair_required']['raw']
        return state
    expected = sha256(b'terminal-redrive-readback:'+attempt['receipt']).digest()
    if evidence == 'terminal' and evidence_receipt == expected:
        return held_delivery(previous,routes_terminal=True,ledger=ledger)
    # Restart never waits indefinitely for an unknown/unavailable completion.
    # The exact persisted attempt returns to captured with bounded retry/error evidence.
    return held_delivery(previous,redrive_failed=True,ledger=ledger)

def repair_redrive(previous, ledger, receipt, readback_available=True, crash_after=None):
    state = deepcopy(previous); repair = previous.get('repair_required')
    if not repair or not retained_authority(previous,ledger): return state
    if not repair_backend_authority(previous,ledger):
        recovered=restart_held_delivery(previous['held_key'],ledger)
        if (recovered is not None and recovered.get('repaired_record') is not None
                and recovered.get('repair_required_record_hash')==sha256(repair['raw']).digest()
                and recovered.get('repair_receipt')==receipt and recovered['redrive_count']==repair['count']):
            return recovered
        return state
    try:
        fields = decode_record(repair['raw'],'HX-EV-REDRIVE-REPAIR-1',['B32','N','B32','B32','U','N','B32','U',('O','B32'),'Q'])
        if (fields[0],fields[1],fields[3],fields[4]) != (repair['owner'],repair['count'],repair['carrier_hash'],repair['locator']): return state
    except (AssertionError,ValueError,KeyError,TypeError): return state
    attempt = getattr(ledger,'redrive_attempts',{}).get((repair['owner'],1))
    if not attempt or (attempt['owner'],attempt['count'],attempt['carrier_hash'],attempt['locator']) != (
            repair['owner'],repair['count'],repair['carrier_hash'],repair['locator']): return state
    if attempt['receipt'] != state_hash({k:v for k,v in attempt.items() if k != 'receipt'}): return state
    request = getattr(ledger,'redrive_requests',{}).get((repair['owner'],1))
    if not redrive_request_authority(previous,request,repair['count']): return state
    if not redrive_attempt_authority(previous,attempt,request,repair['count']): return state
    if attempt.get('request_hash') != sha256(request['raw']).digest(): return state
    expected = sha256(b'authenticated-attempt-repair:'+attempt['receipt']+previous['readback_authority']).digest()
    if receipt != expected: return state
    state['attempt'] = deepcopy(attempt); state['request'] = deepcopy(request); state['repair_required'] = None
    state['repair_receipt'] = expected
    state['repair_required_record_hash'] = sha256(repair['raw']).digest()
    state['repaired_record'] = R('HX-EV-REDRIVE-REPAIR-1',10,repair['owner'],N(repair['count']),fields[2],repair['carrier_hash'],
        U(repair['locator']),N(2),sha256(repair['raw']).digest(),U('repaired'),O(expected),Q(previous['first_observed']))
    rkey=repair_record_address(previous); old=ledger.repair_records[rkey]
    if old['generation']!=1 or old['raw']!=repair['raw']: return deepcopy(previous)
    next_row=repair_native_row(rkey,repair['owner'],state['repaired_record'],2,sha256(old['raw']).digest())
    staged=deepcopy(ledger)
    staged.repair_records[rkey]=next_row
    if not readback_available or crash_after=='repair-repaired-stage': return deepcopy(previous)
    if not repair_backend_authority(state,staged,False): return deepcopy(previous)
    ledger.__dict__=staged.__dict__
    assert repair_backend_authority(state,ledger,False), 'repair-repaired-native-readback'
    return state

def cleanup_redrive_repair(previous, ledger, available=True, crash_after=None):
    state=deepcopy(previous)
    if ledger is None or not available: return state
    rkey,ekey=repair_record_address(state),repair_entry_address(state)
    address=repair_cleanup_address(state)
    stored=getattr(ledger,'repair_cleanup_rows',{}).get(address)
    if stored is None:
        if state.get('repaired_record') is None or not repair_backend_authority(state,ledger,False): return state
        record=ledger.repair_records[rkey]; entry=ledger.repair_entries[ekey]
        cleanup={'record':sha256(record['raw']).digest(),'entry':sha256(entry['raw']).digest(),
                 'record_receipt':record['receipt'],'entry_receipt':entry['receipt'],'required_hash':record['predecessor'],
                 'repair_receipt':state['repair_receipt'],'phase':'repaired-readback'}
        staged=deepcopy(ledger)
        staged.repair_cleanup_rows=getattr(staged,'repair_cleanup_rows',{})
        staged.repair_cleanup_rows[address]=repair_cleanup_native_row(state,cleanup)
        # The fixed address replaces an older completion fence only when the
        # new typed cleanup phase publishes and reads back successfully.
        getattr(staged,'repair_completion',{}).pop(address,None)
        if repair_cleanup_view(state,staged.repair_cleanup_rows[address])!=cleanup: return state
        ledger.__dict__=staged.__dict__
    else:
        cleanup=repair_cleanup_view(state,stored)
        if cleanup is None: return state
        if previous.get('repaired_record') is not None and cleanup['record']!=sha256(previous['repaired_record']).digest(): return state
        if previous.get('repair_entry') is not None and cleanup['entry']!=sha256(previous['repair_entry']).digest(): return state
        if ('record_deletion' in cleanup and getattr(ledger,'repair_deletions',{}).get(('record',rkey))!=cleanup['record_deletion']): return state
        if ('entry_deletion' in cleanup and getattr(ledger,'repair_deletions',{}).get(('entry',ekey))!=cleanup['entry_deletion']): return state
    def persist(staged=None):
        staged=deepcopy(ledger) if staged is None else staged
        old=ledger.repair_cleanup_rows[address]
        if repair_cleanup_view(state,old) is None: return False
        next_row=repair_cleanup_native_row(state,cleanup,old)
        staged.repair_cleanup_rows[address]=next_row
        if repair_cleanup_view(state,staged.repair_cleanup_rows[address])!=cleanup: return False
        ledger.__dict__=staged.__dict__
        state['repair_cleanup']=deepcopy(cleanup)
        state['repair_cleanup_bytes']=next_row['raw']
        return True
    state['repair_cleanup']=deepcopy(cleanup)
    state['repair_cleanup_bytes']=ledger.repair_cleanup_rows[address]['raw']
    if crash_after=='repaired-readback': return state
    if 'record_deletion' not in cleanup:
        record=getattr(ledger,'repair_records',{}).get(rkey)
        if (not repair_native_valid(rkey,record,state['held_key']) or sha256(record['raw']).digest()!=cleanup['record']
                or record['receipt']!=cleanup['record_receipt']): return state
        staged=deepcopy(ledger)
        native=provider_delete_repair(staged,'record',rkey,record)
        if native is None: return state
        cleanup['record_deletion']=native; cleanup['phase']='record-deleted'
        if not persist(staged): return deepcopy(previous)
        state['repaired_record']=None
    if crash_after=='record-deleted': return state
    if (rkey in ledger.repair_records or ledger.repair_deletions.get(('record',rkey))!=cleanup['record_deletion']): return state
    if cleanup['phase'] in {'record-deleted','record-readback'}:
        cleanup['phase']='record-readback'
        if not persist(): return deepcopy(previous)
        if crash_after=='record-readback': return state
    if 'entry_deletion' not in cleanup:
        entry=getattr(ledger,'repair_entries',{}).get(ekey)
        if (not repair_native_valid(ekey,entry,state['held_key']) or sha256(entry['raw']).digest()!=cleanup['entry']
                or entry['receipt']!=cleanup['entry_receipt']): return state
        if not repair_index_authority(state,ledger,entry['raw']): return state
        staged=deepcopy(ledger)
        native=provider_delete_repair(staged,'entry',ekey,entry)
        if native is None: return state
        cleanup['entry_deletion']=native; cleanup['phase']='entry-deleted'
        if not persist(staged): return deepcopy(previous)
        state['repair_entry']=None
    if crash_after=='entry-deleted': return state
    if (ekey in ledger.repair_entries or ledger.repair_deletions.get(('entry',ekey))!=cleanup['entry_deletion']): return state
    ikey=repair_index_address(state); old_index=getattr(ledger,'hold_indexes',{}).get(ikey)
    members=repair_index_view(state,old_index)
    member=repair_index_member(state,cleanup['entry'])
    if members is None or member not in members: return state
    members.remove(member)
    successor=repair_index_row(state,members,old_index)
    if repair_cleanup_view(state,ledger.repair_cleanup_rows[address])!=cleanup: return deepcopy(previous)
    state['repair_cleanup_receipt']=sha256(ledger.repair_cleanup_rows[address]['raw']).digest()
    state['repair_cleanup']=None; state['repair_indexed']=False; state['repair_entry']=None; state['repaired_record']=None
    old_held=ledger.held_entries[held_entry_address(previous)]
    if not old_held['repair_open'] or old_held['count']!=state['redrive_count']: return deepcopy(previous)
    staged=deepcopy(ledger)
    staged.hold_indexes[ikey]=successor
    staged.held_entries[held_entry_address(previous)]=held_entry_row(state,old_held)
    completion_generation=checked_add(int.from_bytes(ledger.repair_cleanup_rows[address]['support'][:8],'big'),1)
    if completion_generation is None: return deepcopy(previous)
    completion={'raw':completion_generation.to_bytes(8,'big')+state['redrive_count'].to_bytes(8,'big')+
        state['repair_cleanup_receipt']}
    completion['native_receipt']=sha256(b'provider-repair-completion-readback:'+U(repair_cleanup_address(state))+image_bytes(completion)).digest()
    if repair_completion_view(state,completion)!=(state['redrive_count'],state['repair_cleanup_receipt']): return deepcopy(previous)
    staged.repair_completion=getattr(staged,'repair_completion',{})
    staged.repair_completion[repair_cleanup_address(state)]=completion
    del staged.repair_cleanup_rows[repair_cleanup_address(state)]
    staged.repair_deletions.pop(('record',rkey)); staged.repair_deletions.pop(('entry',ekey))
    indexed=repair_index_view(state,staged.hold_indexes[ikey])
    if (indexed is None or member in indexed or not held_entry_authority(state,staged)
            or staged.hold_indexes[ikey]['receipt']!=successor['receipt']): return deepcopy(previous)
    ledger.__dict__=staged.__dict__
    return state

def erase_held_delivery(previous, ledger, terminal_receipt, crash_after=None):
    if previous.get('state') == 'erased': return deepcopy(previous)
    expected = sha256(b'terminal-held-erasure:'+previous['held_key']+N(previous['redrive_count'])).digest()
    cleanup_key=codec['K']('HX-EV-PROVIDER-NATIVE-CLEANUP-KEY-1',U('held-erasure'),previous['held_key'])
    cleanup_rows = getattr(ledger,'held_erasure_cleanup',{})
    cleanup = cleanup_rows.get(cleanup_key)
    if cleanup is None:
        if (previous['state'] != 'closed' or terminal_receipt != expected or not retained_authority(previous,ledger)
                or previous.get('repair_required') or previous.get('repaired_record') or previous.get('repair_cleanup')
                or not held_entry_authority(previous,ledger) or ledger.held_entries[held_entry_address(previous)]['repair_open']): return deepcopy(previous)
        address = (previous['held_key'],1)
        request = getattr(ledger,'redrive_requests',{}).get(address)
        attempt = getattr(ledger,'redrive_attempts',{}).get(address)
        if (not redrive_request_authority(previous,request,previous['redrive_count'])
                or request != previous.get('request') or attempt != previous.get('attempt')
                or not redrive_attempt_authority(previous,attempt,request,previous['redrive_count'])
                or attempt.get('request_hash') != sha256(request['raw']).digest()): return deepcopy(previous)
        cleanup = {'owner':previous['held_key'],'terminal':expected,'previous':state_hash(previous),'deleted':{}}
        cleanup['receipt']=state_hash(cleanup)
        cleanup_rows[cleanup_key] = cleanup; ledger.held_erasure_cleanup = cleanup_rows
    assert cleanup['owner'] == previous['held_key'] and cleanup['terminal'] == expected and terminal_receipt == expected
    assert cleanup['previous'] == state_hash(previous)
    assert cleanup.get('receipt') == state_hash({k:v for k,v in cleanup.items() if k != 'receipt'})
    key = previous['retained_object_key']; address = (previous['held_key'],1)
    targets = [('object',ledger.objects,key),('origin',ledger.capture_origins,key),('preparation',ledger.capture_preparations,key),
        ('attempt',getattr(ledger,'redrive_attempts',{}),address),('attempt-receipt',getattr(ledger,'attempt_deletions',{}),address),
        ('request',getattr(ledger,'redrive_requests',{}),address),('request-receipt',getattr(ledger,'request_deletions',{}),address),
        ('repair-interest',ledger.inventory,previous['repair_inventory_key']),
        ('held-entry',ledger.held_entries,held_entry_address(previous)),
        ('repair-completion',getattr(ledger,'repair_completion',{}),repair_cleanup_address(previous))]
    for kind,rows,address in targets:
        if kind not in cleanup['deleted']:
            value = rows.pop(address,None)
            cleanup['deleted'][kind] = sha256(b'provider-held-erasure:'+previous['held_key']+U(kind)+state_hash(value)).digest()
            assert len(image_bytes(cleanup)) <= 4096
            cleanup['receipt'] = state_hash({k:v for k,v in cleanup.items() if k != 'receipt'})
            if crash_after == kind: return deepcopy(previous)
        assert address not in rows and len(cleanup['deleted'][kind]) == 32
    trial = deepcopy(ledger)
    if not trial.refund(previous['account_kind'],previous['account'],previous['charged_bytes']): return deepcopy(previous)
    assert previous['inventory_key'] in ledger.inventory
    for address in (key,previous['metadata_key'],previous['repair_charge_key']): del ledger.charges[address]
    del ledger.inventory[previous['inventory_key']]
    assert ledger.refund(previous['account_kind'],previous['account'],previous['charged_bytes'])
    del cleanup_rows[cleanup_key]
    return {'state':'erased','held_key':previous['held_key'],'redrive_count':previous['redrive_count'],
            'erasure_receipt':expected,'charged_bytes':0,'closed':True}


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
    measured=all(type(v) is int and 0<=v<=U64_MAX for v in (event_count,readable_bytes,accounting_bytes))
    consistent=measured and event_count<=U64_MAX//8192 and accounting_bytes==event_count*8192
    if not consistent:
        return {'outcome':'LegacyArrayLimit','indexed':True,'applied_events':0,'truncated':False,'next_recheck_seconds':3600}
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
    accounting_bytes=max(accounting_bytes,event_count*8192)
    return (event_count > 100000, readable_bytes > 64*MiB, accounting_bytes > 256*MiB)

def retention_readiness(horizon_seconds):
    return {'ready':horizon_seconds <= 315576000,
            'reason':None if horizon_seconds <= 315576000 else 'scope_retention_horizon_unsupported',
            'slice4_active':horizon_seconds <= 315576000}

def post_activation_growth(event_count, readable_bytes, accounting_bytes):
    exceeded = hard_bound_exceeded(event_count, readable_bytes, accounting_bytes)
    valid=all(type(v) is int and 0<=v<=U64_MAX for v in (event_count,readable_bytes,accounting_bytes)) and event_count<=U64_MAX//8192 and accounting_bytes==event_count*8192
    if not valid: return {'dispatch':False,'hold':'LegacyArrayLimit','applied_events':0,'exceeded':exceeded}
    return {'dispatch':not any(exceeded), 'hold':'LegacyArrayLimit' if any(exceeded) else None,
            'applied_events':0 if any(exceeded) else event_count, 'exceeded':exceeded}

def verify_held_delivery_and_long_stream_matrix():
    carrier = b'exact-retained-carrier-and-headers'
    capture_ledger = Ledger()
    identity = ('tenant','deployment-a','t','pubsub','orders','sub-a')
    first = observe_delivery(None, 638712864000000000,capture_ledger,identity,carrier)
    restarted = observe_delivery(dict(first), 638712864600000000)
    assert first['first_observed'] == restarted['first_observed'] == 638712864000000000
    assert first['delivery_attempt_count'] == 1 and restarted['delivery_attempt_count'] == 2
    assert restarted['charged_bytes'] == 32*1024 and restarted['indexed'] and restarted['operator_visible']
    object_key = 'held/'+first['held_key'].hex()
    readback = {'backend':'held-delivery-store','key':object_key,'bytes':carrier,
                'receipt':object_receipt('held-delivery-store',object_key,carrier)}
    policy_store = PolicyStore()
    policy = policy_store.install(None,1,bytes(32))
    for crash_side in ('charge','object'):
        partial_ledger = Ledger()
        observed = observe_delivery(None,638712864000000000,partial_ledger,identity,carrier)
        persisted = capture_delivery(observed,carrier,partial_ledger,readback,policy,policy_store,crash_at=crash_side)
        assert persisted == observed and not persisted['transport_copy_acked']
        charged_before = partial_ledger.deployment
        completed_partial = capture_delivery(read_image(image_bytes(persisted)),carrier,partial_ledger,readback,policy,policy_store)
        assert completed_partial['transport_copy_acked'] and completed_partial['state'] == 'captured'
        assert partial_ledger.deployment == charged_before == 64*1024+len(carrier)
        partial_snapshot = deepcopy(vars(partial_ledger))
        assert capture_delivery(completed_partial,carrier,partial_ledger,readback,policy,policy_store) == completed_partial
        assert vars(partial_ledger) == partial_snapshot
        for conflict in (None,dict(readback,receipt=bytes(32)),dict(readback,bytes=b'changed')):
            blocked = capture_delivery(persisted,carrier,partial_ledger,conflict,policy,policy_store)
            assert blocked == persisted and vars(partial_ledger) == partial_snapshot
    if not globals().get('fault_probe') or globals().get('fault_name') == 'ordinary capture accepts oversize':
        for length in (193*MiB-1,193*MiB,193*MiB+1,256*MiB,256*MiB+1):
            boundary_carrier = b'b'*length
            boundary_ledger = Ledger()
            boundary = observe_delivery(None,638712864000000000,boundary_ledger,identity,boundary_carrier)
            boundary_key = 'held/'+boundary['held_key'].hex()
            boundary_readback = {'backend':'held-delivery-store','key':boundary_key,'bytes':boundary_carrier,
                                'receipt':object_receipt('held-delivery-store',boundary_key,boundary_carrier)}
            counters_before = deepcopy(vars(boundary_ledger))
            outcome = capture_delivery(boundary,boundary_carrier,boundary_ledger,boundary_readback,policy,policy_store)
            if length <= 193*MiB:
                assert outcome['transport_copy_acked'] and outcome['state'] == 'captured'
                assert boundary_ledger.deployment == 64*1024+length
            else:
                assert outcome == boundary and vars(boundary_ledger) == counters_before
                assert not outcome['transport_copy_acked']
            del boundary_carrier,boundary_readback,boundary_ledger,outcome,counters_before,boundary
    held = capture_delivery(restarted,carrier,capture_ledger,readback,policy,policy_store)
    supplementary_records['D36-capture-preparation'] = held['capture_preparation']
    supplementary_records['D36-capture-origin'] = capture_ledger.capture_origins[held['retained_object_key']]
    assert held['state'] == 'captured' and held['retained_bytes'] == carrier
    assert held['charged_bytes'] == capture_ledger.tenant[('tenant','t')] == 64*1024+len(carrier)
    assert capture_ledger.tenant_pool == capture_ledger.deployment == held['charged_bytes']
    captured_snapshot = deepcopy(vars(capture_ledger))
    assert capture_delivery(held,carrier,capture_ledger,readback,policy,policy_store) == held
    assert vars(capture_ledger) == captured_snapshot
    assert (held['transport_copy_acked'] and held['retained_backend_id']
            and held['retained_object_key'] and held['readback_authority']
            and not held['route_success'])
    captured_store = deepcopy(capture_ledger)
    # A stale process image may claim a prerequisite even while the exact
    # backend held row and retained authority are still clean. Its claim
    # cannot authorize a send or publish a new held-count CAS.
    claimed_repair=deepcopy(held)
    claimed_repair['repair_required']={'owner':held['held_key'],'count':1,
        'carrier_hash':held['carrier_hash'],'locator':held['retained_object_key'],
        'raw':R('HX-EV-REDRIVE-REPAIR-1',10,held['held_key'],N(1),sha256(b'process-disputed-attempt').digest(),
            held['carrier_hash'],U(held['retained_object_key']),N(1),bytes(32),U('required'),O(None),Q(held['first_observed']))}
    assert (held_entry_authority(claimed_repair,captured_store) and retained_authority(claimed_repair,captured_store)
        and not captured_store.held_entries[held_entry_address(claimed_repair)]['repair_open'])
    clean_snapshot=deepcopy(vars(captured_store))
    assert (held_delivery(claimed_repair,cause_cleared=True,ledger=captured_store)==claimed_repair
        and vars(captured_store)==clean_snapshot), 'claimed-process-repair-prerequisite-blocks-redrive'
    claimed_repaired=deepcopy(held); required_hash=sha256(b'process-required-repair').digest()
    claimed_repaired['repaired_record']=R('HX-EV-REDRIVE-REPAIR-1',10,held['held_key'],N(1),
        sha256(b'process-disputed-attempt').digest(),held['carrier_hash'],U(held['retained_object_key']),
        N(2),required_hash,U('repaired'),O(sha256(b'process-repair-receipt').digest()),Q(held['first_observed']))
    decode_record(claimed_repaired['repaired_record'],'HX-EV-REDRIVE-REPAIR-1',
        codec['extra_schemas']['D36-repair'],repair_predecessor=required_hash)
    assert (held_entry_authority(claimed_repaired,captured_store) and retained_authority(claimed_repaired,captured_store)
        and not captured_store.held_entries[held_entry_address(claimed_repaired)]['repair_open'])
    assert (held_delivery(claimed_repaired,cause_cleared=True,ledger=captured_store)==claimed_repaired
        and vars(captured_store)==clean_snapshot), 'claimed-process-repaired-evidence-blocks-redrive'
    redriven = held_delivery(held, cause_cleared=True, routes_terminal=False,ledger=capture_ledger)
    supplementary_records['D36-current-request'] = redriven['request']['raw']
    supplementary_records['D36-attempt'] = redriven['attempt']['raw']
    assert redriven['redrive_bytes'] == carrier and not redriven['closed'] and not redriven['route_success']
    first_redrive_store=deepcopy(capture_ledger)
    for evidence in ('unavailable','unknown','failed'):
        recovery_store=deepcopy(first_redrive_store)
        recovery = reconcile_redrive(deepcopy(redriven),evidence,ledger=recovery_store)
        assert recovery['state'] == 'captured' and recovery['redrive_count'] == 1
        assert recovery['next_recheck_seconds'] == 120 and recovery['last_redrive_error']
        for field in ('charged_bytes','retained_bytes','retained_object_key','readback_authority'):
            assert recovery[field] == redriven[field]
        assert reconcile_redrive(recovery,evidence,ledger=recovery_store) == recovery
    terminal_receipt = sha256(b'terminal-redrive-readback:'+redriven['attempt']['receipt']).digest()
    assert reconcile_redrive(deepcopy(redriven),'terminal',terminal_receipt,ledger=deepcopy(first_redrive_store))['state'] == 'closed'
    assert reconcile_redrive(deepcopy(redriven),'terminal',bytes(32),ledger=deepcopy(first_redrive_store))['state'] == 'captured'
    invalid_attempt = deepcopy(redriven); invalid_attempt['attempt']['receipt'] = bytes(32)
    repair_hold = reconcile_redrive(invalid_attempt,ledger=capture_ledger)
    assert repair_hold['state'] == 'captured' and repair_hold['repair_required']
    restarted_repair = read_image(image_bytes(repair_hold))
    for _ in range(3):
        assert held_delivery(restarted_repair,cause_cleared=True,ledger=capture_ledger) == restarted_repair
    assert repair_redrive(restarted_repair,capture_ledger,bytes(32)) == restarted_repair
    genuine_attempt = capture_ledger.redrive_attempts[(held['held_key'],1)]
    # Authenticated attempt readback alone cannot clear the separately persisted
    # repair prerequisite before its exact repaired CAS/cleanup finishes.
    readback_only = deepcopy(restarted_repair); readback_only['attempt'] = deepcopy(genuine_attempt)
    repair_snapshot = deepcopy(vars(capture_ledger))
    assert held_delivery(readback_only,cause_cleared=True,ledger=capture_ledger) == readback_only
    assert vars(capture_ledger) == repair_snapshot
    repair_receipt = sha256(b'authenticated-attempt-repair:'+genuine_attempt['receipt']+held['readback_authority']).digest()
    repaired = repair_redrive(restarted_repair,capture_ledger,repair_receipt)
    assert repaired['repair_required'] is None and repaired['repair_receipt'] == repair_receipt
    assert held_delivery(repaired,cause_cleared=True,ledger=capture_ledger) == repaired
    cleanup_start = cleanup_redrive_repair(repaired,capture_ledger,crash_after='repaired-readback')
    supplementary_records['D36-cleanup'] = cleanup_start['repair_cleanup_bytes']
    repaired = cleanup_redrive_repair(cleanup_start,capture_ledger)
    repaired_send = held_delivery(repaired,cause_cleared=True,ledger=capture_ledger)
    assert repaired_send['state'] == 'redriving' and repaired_send['redrive_count'] == 2 and repaired_send['redrive_bytes'] == carrier
    assert held_delivery(repaired_send,cause_cleared=True,ledger=capture_ledger) == repaired_send
    # Freshly authenticated bytes, object, locator and active charge precede send.
    for damage in ('retained-bytes','object-bytes','missing-object','locator','readback','charge','unavailable'):
        invalid_state = deepcopy(held); invalid_store = deepcopy(captured_store)
        if damage == 'retained-bytes': invalid_state['retained_bytes'] += b'changed'
        elif damage == 'object-bytes': invalid_store.objects[object_key]['bytes'] += b'changed'
        elif damage == 'missing-object': del invalid_store.objects[object_key]
        elif damage == 'locator': invalid_state['retained_object_key'] = 'wrong-locator'
        elif damage == 'readback': invalid_store.objects[object_key]['receipt'] = bytes(32)
        elif damage == 'charge': invalid_store.charges[object_key]['state'] = 'released'
        else: invalid_store = None
        failed_send = held_delivery(invalid_state,cause_cleared=True,ledger=invalid_store)
        assert failed_send == invalid_state and failed_send['redrive_bytes'] is None and failed_send['redrive_count'] == 0
    failure_ledger=deepcopy(first_redrive_store)
    failed_redrive = held_delivery(redriven, redrive_failed=True,ledger=failure_ledger)
    assert (failed_redrive['state'] == 'captured' and failed_redrive['redrive_count'] == 1
            and failed_redrive['last_redrive_error'] and failed_redrive['next_recheck_seconds'] == 120)
    restarted_redrive = deepcopy(failed_redrive)
    second_failure = held_delivery(restarted_redrive,cause_cleared=True,redrive_failed=True,ledger=failure_ledger)
    assert second_failure['redrive_count'] == 2 and second_failure['next_recheck_seconds'] == 240
    assert second_failure['last_redrive_error'] != failed_redrive['last_redrive_error']
    assert second_failure['error_history'] == (failed_redrive['last_redrive_error'],second_failure['last_redrive_error'])
    for field in ('charged_bytes','retained_bytes','retained_backend_id','retained_object_key','readback_authority','first_observed','delivery_attempt_count'):
        assert second_failure[field] == held[field]
    bounded = deepcopy(second_failure)
    for _ in range(140):
        bounded = held_delivery(read_image(image_bytes(bounded)),cause_cleared=True,redrive_failed=True,ledger=failure_ledger)
        assert len(failure_ledger.redrive_attempts) == 1
    assert bounded['redrive_count'] == 142 and len(bounded['error_history']) == 64
    assert bounded['next_recheck_seconds'] == 900
    completed = held_delivery(bounded,cause_cleared=True,routes_terminal=True,ledger=failure_ledger)
    assert completed['redrive_bytes'] == carrier and completed['closed'] and completed['route_success']
    key = held_key('tenant', 'deployment-a', 't', 'pubsub', 'orders', 'sub-a', carrier)
    assert key and key != held_key('tenant', 'deployment-b', 't', 'pubsub', 'orders', 'sub-a', carrier)
    assert key != held_key('tenant', 'deployment-a', 't', 'pubsub', 'other', 'sub-a', carrier)
    for fields in [('tenant','deployment-a','other-tenant','pubsub','orders','sub-a',carrier),
                   ('tenant','deployment-a','t','other-component','orders','sub-a',carrier),
                   ('tenant','deployment-a','t','pubsub','orders','other-subscription',carrier),
                   ('tenant','deployment-a','t','pubsub','orders','sub-a',carrier+b'other'),
                   ('deployment','deployment-a',None,'pubsub','orders','sub-a',carrier)]:
        assert held_key(*fields) != key
    assert held_key('tenant', 'deployment-a', None, 'pubsub', 'orders', 'sub-a', carrier) is None
    assert held_key('deployment', 'deployment-a', 't', 'pubsub', 'orders', 'sub-a', carrier) is None
    for bad_readback in [None,dict(readback,bytes=b'changed'),dict(readback,receipt=bytes(32)),
                         dict(readback,backend='other'),dict(readback,key='other')]:
        failed_ledger = Ledger()
        failed_observed = observe_delivery(None,638712864000000000,failed_ledger,identity,carrier)
        failed_before=deepcopy(vars(failed_ledger))
        rejected_capture = capture_delivery(failed_observed,carrier,failed_ledger,bad_readback,policy,policy_store)
        assert not rejected_capture['transport_copy_acked'] and rejected_capture['state'] == 'observed'
        assert vars(failed_ledger)==failed_before, 'capture-refusal-ledger-generations'
        assert failed_ledger.tenant[('tenant','t')] == failed_ledger.tenant_pool == failed_ledger.deployment == 32*1024
        assert set(failed_ledger.charges) == {failed_observed['metadata_key']} and not failed_ledger.objects
    refused_ledger = Ledger(); refused_ledger.pin_evidence_available = False
    snapshot = deepcopy(vars(refused_ledger))
    assert not capture_delivery(restarted,carrier,refused_ledger,readback,policy,policy_store)['transport_copy_acked']
    assert vars(refused_ledger) == snapshot
    genuine_refusal = Ledger()
    refusal_observed = observe_delivery(None,638712864000000000,genuine_refusal,identity,carrier)
    genuine_refusal.tenant[('tenant','t')]=genuine_refusal.tenant_ceiling-len(carrier)+1
    genuine_refusal.tenant_pool=genuine_refusal.deployment=genuine_refusal.tenant[('tenant','t')]
    genuine_snapshot = deepcopy(vars(genuine_refusal))
    assert capture_delivery(refusal_observed,carrier,genuine_refusal,readback,policy,policy_store) == refusal_observed
    assert vars(genuine_refusal) == genuine_snapshot
    unavailable = capture_delivery(restarted,carrier,Ledger(),readback,policy,PolicyStore())
    assert not unavailable['transport_copy_acked']
    # Missing, stale, and wrong-owner metadata/inventory cannot authorize capture.
    for target,corruption in [('metadata','missing'),('metadata','owner'),('metadata','stale'),
                              ('inventory','missing'),('inventory','owner'),('inventory','stale')]:
        invalid_ledger = Ledger()
        observed = observe_delivery(None,638712864000000000,invalid_ledger,identity,carrier)
        collection = invalid_ledger.charges if target == 'metadata' else invalid_ledger.inventory
        authority_key = observed['metadata_key' if target == 'metadata' else 'inventory_key']
        if corruption == 'missing': del collection[authority_key]
        else:
            collection[authority_key]['owner' if corruption == 'owner' else 'generation'] = bytes(32) if corruption == 'owner' else 2
            row = collection[authority_key]; row['receipt'] = state_hash({k:v for k,v in row.items() if k != 'receipt'})
        invalid_snapshot = deepcopy(vars(invalid_ledger)); observed_snapshot = deepcopy(observed)
        rejected = capture_delivery(observed,carrier,invalid_ledger,readback,policy,policy_store)
        assert rejected == observed_snapshot and vars(invalid_ledger) == invalid_snapshot
    # Two carriers and two scope identities share one persistent store/ledger.
    first_object = deepcopy(capture_ledger.objects[object_key]); first_charge = deepcopy(capture_ledger.charges[object_key])
    for second_identity,second_carrier in [(identity,carrier+b'-second'),
            (('tenant','deployment-a','tenant-b','pubsub','orders','sub-a'),carrier),
            (('deployment','deployment-a',None,'pubsub','orders','sub-a'),carrier)]:
        observed = observe_delivery(None,638712864000000000,capture_ledger,second_identity,second_carrier)
        second_key = 'held/'+observed['held_key'].hex(); assert second_key != object_key
        second_readback = {'backend':'held-delivery-store','key':second_key,'bytes':second_carrier,
                          'receipt':object_receipt('held-delivery-store',second_key,second_carrier)}
        second_capture = capture_delivery(observed,second_carrier,capture_ledger,second_readback,policy,policy_store)
        assert second_capture['state'] == 'captured' and second_capture['transport_copy_acked']
        assert capture_ledger.objects[object_key] == first_object and capture_ledger.charges[object_key] == first_charge
        shared_snapshot = deepcopy(vars(capture_ledger))
        assert capture_delivery(second_capture,second_carrier,capture_ledger,second_readback,policy,policy_store) == second_capture
        assert vars(capture_ledger) == shared_snapshot
    assert len(capture_ledger.objects) == 4 and capture_ledger.unidentified == 64*1024+len(carrier)
    advanced_policy = policy_store.install(policy,2,policy['hash'])
    stale_capture = capture_delivery(restarted,carrier,capture_ledger,readback,policy,policy_store)
    assert stale_capture == restarted and advanced_policy and not stale_capture['transport_copy_acked']
    over_bound = full_replay_exit(100001, 64*MiB, 256*MiB, False)
    assert over_bound == {
        'outcome':'LegacyArrayLimit', 'indexed':True, 'applied_events':0,
        'truncated':False, 'next_recheck_seconds':3600}
    capability_exit = full_replay_exit(100001, 64*MiB, 100001*8192, True)
    assert capability_exit == {
        'outcome':'incremental-scheduled', 'indexed':False, 'applied_events':0,
        'truncated':False, 'requires_new_event':False}
    for count,readable,accounting in ((30000,4096,1),(30000,4096,None),
            (U64_MAX,4096,U64_MAX),(1,4096,U64_MAX+1)):
        for incremental in (False,True):
            assert full_replay_exit(count,readable,accounting,incremental) == {
                'outcome':'LegacyArrayLimit','indexed':True,'applied_events':0,
                'truncated':False,'next_recheck_seconds':3600}
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
        if index==0:
            # Accounting reaches its threshold before the independent count bound.
            for count in (below,at,above):
                assert full_replay_exit(count,1024,count*8192,False)['outcome']=='LegacyArrayLimit'
        elif index==1:
            values[1]=below; assert full_replay_exit(*values,False)['outcome']=='continue-full-replay'
            for size in (at,above):
                values[1]=size; assert full_replay_exit(*values,False)['outcome']=='LegacyArrayLimit'
        else:
            # The nearest legal accounting points differ by one complete event.
            for count,outcome in ((24575,'continue-full-replay'),(24576,'LegacyArrayLimit'),(24577,'LegacyArrayLimit')):
                assert full_replay_exit(count,1024,count*8192,False)['outcome']==outcome
            for size in (below,at+1,above):
                assert full_replay_exit(24576,1024,size,False)['outcome']=='LegacyArrayLimit'
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
            valid_values=[values[0],values[1],values[0]*8192]
            override = full_replay_exit(*valid_values, True)
            assert override['outcome'] == 'incremental-scheduled' and override['applied_events'] == 0
        assert full_replay_exit(*above_values, False)['applied_events'] == 0
        growth = post_activation_growth(*above_values)
        assert not growth['dispatch'] and growth['hold'] == 'LegacyArrayLimit'
        assert growth['applied_events'] == 0 and growth['exceeded'][index]
    assert retention_readiness(315576000-1) == {'ready':True, 'reason':None, 'slice4_active':True}
    assert retention_readiness(315576000) == {'ready':True, 'reason':None, 'slice4_active':True}
    assert retention_readiness(315576000+1) == {
        'ready':False, 'reason':'scope_retention_horizon_unsupported', 'slice4_active':False}
    return 'held-delivery-or-long-stream'

def verify_loop6_transitions():
    roster = ((1,'message-1',b'accepted'),(2,'message-2',b'unresolved-a'),(3,'message-3',b'unresolved-b'))
    window = existing_window_bytes('t',roster)
    base = {'roster':roster,'accepted':(roster[0],),'unresolved':roster[1:],'ordinal':1,'window':7,'closed':2,'limit':16,
        'tenant':'t','handle':'h','hold_source':sha256(b'limit-hash').digest(),'active_charge':300,'next_charge':400,'charge_ceiling':1000,
        'live':{},'tombstones':{},'orphans':{},'audits':0,'invocations':(),'window_claim_bytes':window,'window_claim':sha256(window).digest(),'window_admission':codec['window_admission_bytes'](roster[1:])}
    base['window_progress']=codec['window_progress_bytes'](base['window_admission'],base['accepted'],base['unresolved'])
    identity,carrier = request(b'loop6')
    prepared = owned_resume_preparation(resume_publication(base,identity,carrier,'publication_drain_limit_hold','current',crash_after_audit=True),identity)
    utc_cases = 0; distinct_cases = set()
    # Every intervening run expires actual existing live evidence at UTC 1900,
    # including intent/write boundaries; no labelled no-op counts as a case.
    utc_base=deepcopy(base); prior_identity=codec['H']('utc-prior-success')
    prior_result={'ordinal':1,'window':7,'limit':16,'audit_hash':codec['H']('utc-prior-audit')}
    prior_response=json.dumps({'resumeHandle':'h','resumeOrdinal':1,'window':7,'drainLimit':16,
        'auditRecordHash':prior_result['audit_hash'].hex()},sort_keys=True,separators=(',',':')).encode()
    utc_base['live'][prior_identity]={'carrier_hash':codec['H']('utc-prior-carrier'),'result':prior_result,'response':prior_response,'expires_at':1900}
    utc_base['last_audit']=prior_result['audit_hash']
    utc_prepared=owned_resume_preparation(resume_publication(utc_base,identity,carrier,'publication_drain_limit_hold','current',crash_after_audit=True),identity)
    for boundary in ('successor-intent','successor-write','successor','finalize'):
      for completion in (1900,1901,2000,2001,1900+30*86400-1,1900+30*86400,1900+30*86400+1,1900+30*86400+2):
       for intervening in (False,True):
        case=(boundary,completion,intervening); assert case not in distinct_cases; distinct_cases.add(case)
        store = PreparationStore(identity,carrier,utc_prepared,utc_base)
        assert store.turn(boundary,now=1000) == 'interrupted'
        audit = store.intended('audit'); claim = store.origin_fields()[5]
        if intervening:
            key=store.artifact_key('state'); current=store.backend.read(key,identity)
            if store.predecessor_readback(current[0]):
                expired_prior=reconcile_tombstones(utc_base,1900)
                raw=publication_state_bytes(expired_prior,1900)
                store.backend.write(key,raw,identity,current[1]+1,sha256(current[0]).digest())
            else:
                rows=store.manifest(); rows['state']=(key,sha256(current[0]).digest(),current[2],'present')
                store.progress(identity,'audited',rows,store.head()[4])
                store.reconcile_successor(1900)
            assert store.backend.rows[key] != current[0], ('intervening expiry did not change bytes',case)
            actual=decode_record(store.backend.rows[key],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
            assert int.from_bytes(actual[11][:4],'big') == 0 and int.from_bytes(actual[12][:4],'big') >= 1
        restored = restart_preparation(store); assert restored is not None,case
        assert restored.turn(now=completion) == 'completed', case
        fields = decode_record(restored.backend.rows[restored.artifact_key('state')],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
        counts = tuple(int.from_bytes(fields[i][:4],'big') for i in (11,12))
        assert counts == ((0,2) if completion < 1900+30*86400 else (0,0)),case
        assert fields[13] == completion*10000000 and restored.artifacts['audit'] == audit and restored.artifacts['claim'] == claim
        assert restored.swap.used == restored.swap.active == 400 and len(restored.artifacts) == 5
        snapshot = restored.backend.snapshot()
        repeated = restart_preparation(restored); assert repeated is not None and repeated.turn(now=completion) == 'completed'
        assert repeated.backend.snapshot() == snapshot
        utc_cases += 1
    assert len(distinct_cases) == utc_cases == 64
    # Preserve distinct 1000 -> 1001 and before-expiry recovery evidence too.
    for boundary in ('successor-intent','successor-write','successor','finalize'):
        store=PreparationStore(identity,carrier,prepared,base); assert store.turn(boundary) == 'interrupted'
        restored=restart_preparation(store); assert restored.turn(now=1001) == 'completed'
        fields=decode_record(restored.artifacts['successor'],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
        assert int.from_bytes(fields[11][:4],'big') == 1 and fields[13] == 10010000000
    for boundary in ('successor-intent','successor-write','successor','finalize'):
        store = PreparationStore(identity,carrier,prepared,base); assert store.turn(boundary) == 'interrupted'
        for damage in ('unavailable','contradictory'):
            damaged = deepcopy(store); key = damaged.artifact_key('state')
            if damage == 'unavailable': damaged.backend.unavailable.add(key)
            else: damaged.backend.rows[key] += b'changed'
            snapshot = damaged.backend.snapshot()
            assert damaged.turn(now=2000) == 'evidence-hold' and damaged.backend.snapshot() == snapshot
    rolled = PreparationStore(identity,carrier,prepared,base); assert rolled.turn('resolution') == 'interrupted'
    assert rolled.rollback_artifacts(presence(identity,'successor','absent'),presence(identity,'audit','absent')) == 'rolled-back'
    assert rolled.charge_fields('new')[9:13] == (2,'released',rolled.charge_fields('new')[11],identity)
    assert restart_preparation(rolled).turn() == 'completed' and rolled.charge_fields('new')[12] == identity
    # Equivalent partitions produce identical admitted roots, including persisted preparation.
    reversed_state = deepcopy(base); reversed_state['unresolved'] = tuple(reversed(base['unresolved']))
    normal = resume_publication(base,identity,carrier,'publication_retry_exhausted_hold','current')
    permuted = resume_publication(reversed_state,identity,carrier,'publication_retry_exhausted_hold','current')
    assert normal['outcome']==permuted['outcome']=='resumed', 'expected-successful-window-results'
    assert normal['state']['invocations'] == permuted['state']['invocations']
    decoded = decode_record(normal['state']['window_claim_bytes'],'HX-EV-PUBLICATION-WINDOW-2',codec['schemas']['D45-window'])
    independent_rows = b''.join(codec['pack']('>I',p)+U(m)+sha256(body).digest() for p,m,body in sorted(base['unresolved']))
    expected = sha256(b'HX-EV-PUBLICATION-UNRESOLVED-1\0\x01'+codec['pack']('>I',2)+independent_rows).digest()
    assert decoded[7] == expected != sha256(image_bytes(roster)).digest()
    reversed_preparation = owned_resume_preparation(resume_publication(reversed_state,identity,carrier,'publication_drain_limit_hold','current',crash_after_audit=True),identity)
    reversed_store = PreparationStore(identity,carrier,reversed_preparation,reversed_state)
    assert reversed_store.turn() == 'completed'
    invocation = decode_record(reversed_store.artifacts['invocation'],'HX-EV-PUBLICATION-INVOCATION-1',codec['extra_schemas']['D45-invocation'])
    assert invocation[6] == expected and reversed_store.imported('window_claim_bytes') == image_bytes(base['window_claim_bytes'])
    singleton = deepcopy(base); singleton['accepted'] = roster[:2]; singleton['unresolved'] = roster[2:]
    singleton['window_admission']=codec['window_admission_bytes'](singleton['unresolved'])
    singleton['window_progress']=codec['window_progress_bytes'](singleton['window_admission'],singleton['accepted'],singleton['unresolved'])
    singleton['window_claim_bytes'] = existing_window_bytes('t',roster,singleton['unresolved']); singleton['window_claim'] = sha256(singleton['window_claim_bytes']).digest()
    one = resume_publication(singleton,identity,carrier,'publication_retry_exhausted_hold','current')
    assert one['outcome'] == 'resumed' and decode_record(one['state']['window_claim_bytes'],'HX-EV-PUBLICATION-WINDOW-2',codec['schemas']['D45-window'])[7] == unresolved_root(roster[2:])
    # Multiple local attempts retain every registration/Unknown/result while summaries select only the greatest local result.
    rows = []
    for p,m,body in base['unresolved']:
      for local in range(1,p+2):
        parent = sha256(b'loop6-registration:'+N(p)+N(local)).digest(); send = sha256(b'send:'+parent).digest()
        observations=tuple((observation,kind,sha256(kind.encode()+parent+N(observation)).digest()) for observation,kind in enumerate(('register',)+('unknown',)*(local%2)+('result',)))
        codec['c2_fixture_readback']('t',codec['H']('scope'),7,p,m,local,parent,send,observations)
        for observation,kind,evidence in observations:
            rows.append(codec['pack']('>I',p)+N(local)+N(observation)+U(kind)+parent+send+sha256(kind.encode()+parent+N(observation)).digest())
    exact = b''.join(rows); roster_root = sha256(image_bytes(roster)).digest()
    root = sha256(b'HX-EV-WINDOW-ATTEMPTS-2\0\x01'+U('t')+codec['H']('scope')+N(7)+roster_root+N(len(rows))+B(exact)).digest()
    authority = R('HX-EV-WINDOW-ATTEMPT-SET-1',8,U('t'),codec['H']('scope'),N(7),roster_root,N(len(rows)),B(exact),root,Q(10000000000))
    complete = resume_publication(base,identity,carrier,'publication_retry_exhausted_hold','current',crash_after_audit=True,attempt_authority=authority)
    multi = PreparationStore(identity,carrier,owned_resume_preparation(complete,identity),base,eligible='retry-exhausted',attempt_authority=authority)
    assert multi.turn() == 'completed'
    closure = decode_record(multi.artifacts['closure'],'HX-EV-PUBLICATION-WINDOW-CLOSURE-3',codec['schemas']['D45-closure'],attempt_store={('t',codec['H']('scope'),7):authority})
    summaries = codec['decode_rows'](closure[3][4:],2,['P','N','B32'])
    assert [row[1] for row in summaries] == [3,4] and closure[7] == root
    changed = authority[:-1]
    try: publication_closure_bytes(base,changed,multi.imported('broker'),1000)
    except AssertionError: pass
    else: raise AssertionError('missing earlier attempt authority accepted')
    capture_cases = 0
    carrier = b'loop6-retained-carrier'; held_identity = ('tenant','deployment-a','t','pubsub','orders','sub-a')
    policy_store = PolicyStore(); policy = policy_store.install(None,1,bytes(32))
    def capture_fixture(ceiling=10000,crash=None):
        ledger = Ledger(); ledger.inventory_ceiling = ceiling
        observed = observe_delivery(None,638712864000000000,ledger,held_identity,carrier)
        key = 'held/'+observed['held_key'].hex()
        readback = {'backend':'held-delivery-store','key':key,'bytes':carrier,'receipt':object_receipt('held-delivery-store',key,carrier)}
        return ledger,observed,readback,capture_delivery(observed,carrier,ledger,readback,policy,policy_store,crash_at=crash)
    ledger,observed,readback,refused = capture_fixture(1)
    assert refused == observed and len(ledger.inventory) == 1 and ledger.deployment == 32768 and len(ledger.charges) == 1
    ledger,observed,readback,held = capture_fixture(2)
    assert held['transport_copy_acked'] and len(ledger.inventory) == 2
    for boundary in ('charge','object'):
      for increments in (1,3):
        ledger,observed,readback,partial = capture_fixture(2,boundary)
        prior_charges = deepcopy(ledger.charges); prior_usage = ledger.deployment
        for _ in range(increments): partial = observe_delivery(read_image(image_bytes(partial)),partial['first_observed']+1)
        restored = read_image(image_bytes(partial)); persisted_ledger = read_image(image_bytes(vars(ledger)))
        ledger.__dict__ = persisted_ledger
        completed = capture_delivery(restored,carrier,ledger,readback,policy,policy_store)
        assert completed['state'] == 'captured' and completed['delivery_attempt_count'] == 1+increments
        assert completed['observation_revision'] == 1+increments and completed['first_observed'] == observed['first_observed']
        assert ledger.charges == prior_charges and ledger.deployment == prior_usage and len(ledger.objects) == 1 and len(ledger.inventory) == 2
        snapshot = deepcopy(vars(ledger)); assert capture_delivery(completed,carrier,ledger,readback,policy,policy_store) == completed and vars(ledger) == snapshot
        for field in ('first_observed','observation_receipt','metadata_receipt'):
            bad = deepcopy(restored); bad[field] = 0 if field == 'first_observed' else bytes(32)
            assert capture_delivery(bad,carrier,ledger,readback,policy,policy_store) == bad and vars(ledger) == snapshot
        capture_cases += 1
    for boundary in ('charge','object'):
        store,prior,readback,partial = capture_fixture(2,boundary)
        changed_head = deepcopy(policy_store); changed_policy = changed_head.install(policy,2,policy['hash'])
        snapshot = deepcopy(vars(store))
        assert capture_delivery(partial,carrier,store,readback,changed_policy,changed_head) == partial and vars(store) == snapshot
    for boundary in ('charge','object'):
        store,prior,readback,partial = capture_fixture(2,boundary)
        snapshot = deepcopy(vars(store))
        assert rollback_capture(partial,carrier,store,None) == partial and vars(store) == snapshot
        absence = presence(partial['held_key'],'capture','absent')
        assert rollback_capture(partial,carrier,store,absence,False) == partial and vars(store) == snapshot
        if boundary=='charge':
            assert rollback_capture(partial,carrier,store,absence) == partial and vars(store) == snapshot
        pending_store=deepcopy(store); pending_used=pending_store.deployment
        object_absence=presence(partial['held_key'],'object','absent') if boundary=='charge' else None
        assert rollback_capture(partial,carrier,pending_store,absence,crash_after='origin',object_absence=object_absence) == partial
        assert pending_store.deployment == pending_used and partial['inventory_key'] in pending_store.inventory and pending_store.charges
        assert rollback_capture(partial,carrier,pending_store,absence,object_absence=object_absence) == partial
        assert pending_store.deployment == 32768 and partial['inventory_key'] in pending_store.inventory
        assert rollback_capture(partial,carrier,store,absence,object_absence=object_absence) == partial
        assert store.deployment == 32768 and len(store.inventory) == len(store.charges) == 1 and not store.objects
        refunded = deepcopy(vars(store)); assert rollback_capture(partial,carrier,store,absence,object_absence=object_absence) == partial and vars(store) == refunded
    ledger,observed,readback,held = capture_fixture(2)
    obligations = [('metadata',held['metadata_key']),('inventory',held['inventory_key']),('repair-charge',held['repair_charge_key']),('repair-interest',held['repair_inventory_key'])]
    refusals = 0
    for kind,key in obligations:
      for damage in ('missing','owner','generation','account','state','receipt'):
        store = deepcopy(ledger); prior = deepcopy(held); collection = store.inventory if kind in {'inventory','repair-interest'} else store.charges
        if damage == 'missing': del collection[key]
        elif damage == 'receipt': collection[key]['receipt'] = bytes(32)
        else:
            row = collection[key]
            if damage == 'owner': row['owner'] = bytes(32)
            elif damage == 'generation': row['generation'] = 2
            elif damage == 'account': row['account'] = 'wrong-account'
            elif kind in {'inventory','repair-interest'}: row['reserved'] = False
            else: row['state'] = 'released'
            row['receipt'] = state_hash({k:v for k,v in row.items() if k != 'receipt'})
        snapshot = deepcopy(vars(store)); assert held_delivery(prior,cause_cleared=True,ledger=store) == prior and vars(store) == snapshot
        refusals += 1
    store = deepcopy(ledger); prior = deepcopy(held)
    store.charges.clear(); store.inventory.clear(); store.objects.clear(); store.capture_origins.clear(); store.capture_preparations.clear()
    snapshot = deepcopy(vars(store))
    assert held_delivery(prior,cause_cleared=True,ledger=store) == prior and prior['redrive_count'] == 0 and prior['redrive_bytes'] is None and vars(store) == snapshot
    refusals += 1
    for damage in ('ledger-missing','held-missing','both-missing','ledger-changed','held-changed','both-changed','origin-missing'):
        store = deepcopy(ledger); prior = deepcopy(held); key = held['retained_object_key']
        if damage in {'ledger-missing','both-missing'}: del store.capture_preparations[key]
        if damage in {'held-missing','both-missing'}: prior.pop('capture_preparation')
        if damage in {'ledger-changed','both-changed'}: store.capture_preparations[key] += b'changed'
        if damage in {'held-changed','both-changed'}: prior['capture_preparation'] += b'changed'
        if damage == 'origin-missing': del store.capture_origins[key]
        assert not retained_authority(prior,store), ('loop6-damaged-preparation-origin-authority',damage)
        snapshot = deepcopy(vars(store)); assert held_delivery(prior,cause_cleared=True,ledger=store) == prior and vars(store) == snapshot
        refusals += 1
    def signed_row(row):
        row = deepcopy(row); row['signature'] = sha256(b'authenticated-purpose-2d-redrive:'+row['raw']).digest()
        row['receipt'] = sha256(b'provider-redrive-request-readback:'+image_bytes({k:v for k,v in row.items() if k != 'receipt'})).digest()
        return row
    request_cases = 0
    first_request = signed_redrive_request(held)
    request_schema = codec['schemas']['D36-redrive']
    for damage in ('issuer','subject','scope','tenant','held-key','count','time','signature','receipt','owner','row-count'):
        row = deepcopy(first_request); values = list(decode_record(row['raw'],'HX-EV-REDRIVE-REQUEST-2',request_schema))
        if damage == 'issuer': values[0] = 'unauthorized-issuer'
        elif damage == 'subject': values[5] = 'unauthorized-subject'
        elif damage == 'scope': values[1:3] = ['deployment',None]
        elif damage == 'tenant': values[2] = 'other-tenant'
        elif damage == 'held-key': values[3] = bytes(32)
        elif damage == 'count': values[4] = 1
        elif damage == 'time': values[6] = held['first_observed']-1
        elif damage == 'owner': row['owner'] = bytes(32)
        elif damage == 'row-count': row['count'] = 2
        row['raw'] = R('HX-EV-REDRIVE-REQUEST-2',7,*(codec['encode_typed'](kind,value) for kind,value in zip(request_schema,values)))
        row = signed_row(row)
        if damage == 'signature': row['signature'] = bytes(32); row['receipt'] = sha256(b'provider-redrive-request-readback:'+image_bytes({k:v for k,v in row.items() if k != 'receipt'})).digest()
        elif damage == 'receipt': row['receipt'] = bytes(32)
        store = deepcopy(ledger); snapshot = deepcopy(vars(store))
        assert held_delivery(held,cause_cleared=True,ledger=store,request=row) == held and vars(store) == snapshot, damage
        request_cases += 1
    once_store = deepcopy(ledger)
    once = held_delivery(held,cause_cleared=True,redrive_failed=True,ledger=once_store)
    address = (held['held_key'],1)
    for damage in ('provider-missing','held-missing','both-missing','signature-both','receipt-both','raw-both'):
        store = deepcopy(once_store); prior = deepcopy(once)
        if damage in {'provider-missing','both-missing'}: del store.redrive_requests[address]
        if damage in {'held-missing','both-missing'}: prior.pop('request')
        if damage == 'signature-both':
            row = deepcopy(prior['request']); row['signature'] = bytes(32)
            row['receipt'] = sha256(b'provider-redrive-request-readback:'+image_bytes({k:v for k,v in row.items() if k != 'receipt'})).digest()
            store.redrive_requests[address] = prior['request'] = row
        elif damage == 'receipt-both': store.redrive_requests[address]['receipt'] = prior['request']['receipt'] = bytes(32)
        elif damage == 'raw-both':
            row = deepcopy(prior['request']); row['raw'] += b'changed'; row = signed_row(row)
            store.redrive_requests[address] = prior['request'] = row
        snapshot = deepcopy(vars(store))
        assert held_delivery(prior,cause_cleared=True,ledger=store) == prior and vars(store) == snapshot, damage
        request_cases += 1
    for flag in ('request_readback_available','request_delete_available'):
        store = deepcopy(once_store); snapshot = deepcopy(vars(store))
        assert held_delivery(once,cause_cleared=True,ledger=store,**{flag:False}) == once and vars(store) == snapshot
        request_cases += 1
    request_restarts = 0
    erasure_refusals = 0
    for prior,original_store in ((held,ledger),(once,once_store)):
      for boundary in ('request-readback','request-delete-readback','request-attempt-commit'):
        store = deepcopy(original_store); snapshot = deepcopy(vars(store))
        pending = held_delivery(prior,cause_cleared=True,ledger=store,crash_after=boundary)
        persisted = read_image(image_bytes(vars(store))); store.__dict__ = persisted
        pending = read_image(image_bytes(pending))
        if boundary != 'request-attempt-commit':
            assert pending == prior and vars(store) == snapshot
        else:
            assert pending['state'] == 'redriving' and pending['redrive_bytes'] is None and pending['redrive_count'] == prior['redrive_count']+1
            assert store.redrive_requests[address] == pending['request'] and store.redrive_attempts[address] == pending['attempt']
            committed = deepcopy(vars(store))
            assert held_delivery(pending,cause_cleared=True,ledger=store) == pending and vars(store) == committed
            recovered = reconcile_redrive(pending,ledger=store)
            assert recovered['state'] == 'captured' and not recovered.get('repair_required') and recovered['redrive_count'] == pending['redrive_count']
            closed = held_delivery(recovered,cause_cleared=True,routes_terminal=True,ledger=store)
            receipt = sha256(b'terminal-held-erasure:'+held['held_key']+N(closed['redrive_count'])).digest()
            # Every deletion/readback precedes refund: bad route authority leaves the exact ledger.
            before_erase = deepcopy(vars(store)); assert erase_held_delivery(closed,store,bytes(32)) == closed and vars(store) == before_erase
            for damage in ('request-missing','attempt-missing','request-receipt'):
                damaged = deepcopy(store)
                if damage == 'request-missing': del damaged.redrive_requests[address]
                elif damage == 'attempt-missing': del damaged.redrive_attempts[address]
                else: damaged.redrive_requests[address]['receipt'] = bytes(32)
                unchanged = deepcopy(vars(damaged)); assert erase_held_delivery(closed,damaged,receipt) == closed and vars(damaged) == unchanged
                erasure_refusals += 1
            erased = erase_held_delivery(closed,store,receipt)
            assert erased['state'] == 'erased' and store.deployment == 0 and not store.redrive_requests and not store.redrive_attempts and not store.request_deletions and not store.attempt_deletions
            refunded = deepcopy(vars(store)); assert erase_held_delivery(erased,store,receipt) == erased and vars(store) == refunded
        request_restarts += 1
    initial_charges = deepcopy(ledger.charges); initial_usage = ledger.deployment; repeated = deepcopy(held)
    for _ in range(141):
        ledger.__dict__ = read_image(image_bytes(vars(ledger)))
        old_request = deepcopy(getattr(ledger,'redrive_requests',{}).get(address))
        old_attempt = deepcopy(getattr(ledger,'redrive_attempts',{}).get(address))
        repeated = held_delivery(read_image(image_bytes(repeated)),cause_cleared=True,redrive_failed=True,ledger=ledger)
        assert len(ledger.redrive_attempts) == 1 and len(getattr(ledger,'attempt_deletions',{})) <= 1
        assert len(ledger.redrive_requests) == 1 and len(getattr(ledger,'request_deletions',{})) <= 1
        if old_request is not None:
            assert ledger.request_deletions[address] == sha256(b'provider-redrive-request-deletion:'+old_request['receipt']).digest()
            assert ledger.attempt_deletions[address] == sha256(b'provider-attempt-deletion:'+old_attempt['receipt']).digest()
        assert all(len(receipt) == 32 for receipt in list(ledger.request_deletions.values())+list(ledger.attempt_deletions.values()))
        retained_request = ledger.redrive_requests[address]; retained_attempt = ledger.redrive_attempts[address]
        assert len(retained_request['raw']) <= 3072 and len(retained_request['signature']) == len(retained_request['receipt']) == 32
        assert retained_request == repeated['request'] and redrive_request_authority(repeated,retained_request,repeated['redrive_count'])
        assert retained_attempt['request_hash'] == sha256(retained_request['raw']).digest()
        assert decode_record(retained_attempt['raw'],'HX-EV-REDRIVE-ATTEMPT-1',codec['extra_schemas']['D36-attempt'])[6] == retained_attempt['request_hash']
        assert len(retained_attempt['raw']) <= 8192 and len(image_bytes(retained_request))+len(image_bytes(retained_attempt)) <= 19*1024
        assert ledger.charges == initial_charges and ledger.deployment == initial_usage == repeated['charged_bytes'] == 64*1024+len(carrier)
    assert repeated['redrive_count'] == 141
    snapshot = deepcopy(vars(ledger))
    assert held_delivery(repeated,cause_cleared=True,ledger=ledger,request=first_request) == repeated and vars(ledger) == snapshot
    request_cases += 1
    for damage in ('missing','receipt','owner'):
        store = deepcopy(ledger); address = (held['held_key'],1)
        if damage == 'missing': del store.redrive_attempts[address]
        elif damage == 'receipt': store.redrive_attempts[address]['receipt'] = bytes(32)
        else:
            row = store.redrive_attempts[address]; row['owner'] = bytes(32)
            row['receipt'] = state_hash({k:v for k,v in row.items() if k != 'receipt'})
        snapshot = deepcopy(vars(store)); assert held_delivery(repeated,cause_cleared=True,ledger=store) == repeated and vars(store) == snapshot
    for damage in ('malformed','typed-count','typed-request'):
        store = deepcopy(ledger); prior = deepcopy(repeated); row = deepcopy(prior['attempt'])
        if damage == 'malformed': row['raw'] += b'changed'
        else:
            fields = list(decode_record(row['raw'],'HX-EV-REDRIVE-ATTEMPT-1',codec['extra_schemas']['D36-attempt']))
            fields[1 if damage == 'typed-count' else 6] = 1 if damage == 'typed-count' else bytes(32)
            row['raw'] = R('HX-EV-REDRIVE-ATTEMPT-1',7,*(codec['encode_typed'](kind,value) for kind,value in zip(codec['extra_schemas']['D36-attempt'],fields)))
        row['receipt'] = state_hash({k:v for k,v in row.items() if k != 'receipt'})
        store.redrive_attempts[address] = prior['attempt'] = row
        snapshot = deepcopy(vars(store)); assert held_delivery(prior,cause_cleared=True,ledger=store) == prior and vars(store) == snapshot
    redriving = held_delivery(repeated,cause_cleared=True,ledger=ledger); bad = deepcopy(redriving); bad['attempt']['receipt'] = bytes(32)
    pre_repair_store=deepcopy(ledger)
    required = reconcile_redrive(bad,ledger=ledger); genuine = ledger.redrive_attempts[(held['held_key'],1)]
    assert repair_backend_authority(required,ledger) and restart_held_delivery(held['held_key'],ledger)['repair_required']
    for damage in ('missing','signature','receipt'):
        store = deepcopy(pre_repair_store)
        if damage == 'missing': del store.redrive_requests[address]
        else: store.redrive_requests[address][damage] = bytes(32)
        snapshot = deepcopy(vars(store))
        disputed = reconcile_redrive(read_image(image_bytes(redriving)),ledger=store)
        assert disputed['state'] == 'captured' and disputed['repair_required'] and disputed['redrive_count'] == redriving['redrive_count']
        assert vars(store) != snapshot and repair_backend_authority(disputed,store)
        snapshot=deepcopy(vars(store))
        receipt = sha256(b'authenticated-attempt-repair:'+genuine['receipt']+held['readback_authority']).digest()
        assert repair_redrive(disputed,store,receipt) == disputed and vars(store) == snapshot
        request_cases += 1
    repaired = repair_redrive(required,ledger,sha256(b'authenticated-attempt-repair:'+genuine['receipt']+held['readback_authority']).digest())
    assert repaired['repair_required'] is None and repaired['repaired_record'] and repaired['repair_indexed']
    cleanup_cases = 0
    for boundary in ('repaired-readback','record-deleted','record-readback','entry-deleted'):
        store=deepcopy(ledger)
        pending = cleanup_redrive_repair(repaired,store,crash_after=boundary)
        store.__dict__=read_image(image_bytes(vars(store)))
        pending=restart_held_delivery(held['held_key'],store)
        assert pending is not None and pending['repair_indexed'] and held_delivery(pending,cause_cleared=True,ledger=store)==pending
        assert cleanup_redrive_repair(pending,store,False)==pending
        finished=cleanup_redrive_repair(pending,store)
        assert not finished['repair_indexed'] and finished['repair_entry'] is None
        assert cleanup_redrive_repair(finished,store)==finished
        assert store.redrive_requests[address]==repaired['request'] and store.redrive_attempts[address]==repaired['attempt']
        cleanup_cases += 1
    finished = cleanup_redrive_repair(repaired,ledger)
    sent = held_delivery(finished,cause_cleared=True,ledger=ledger); assert sent['redrive_count'] == 143 and sent['redrive_bytes'] == carrier
    bad = deepcopy(sent); bad['attempt']['receipt'] = bytes(32); required = reconcile_redrive(bad,ledger=ledger)
    assert required['repair_required']['count'] == 143 and required['repair_indexed'] and not required.get('repaired_record')
    genuine = ledger.redrive_attempts[(held['held_key'],1)]; receipt = sha256(b'authenticated-attempt-repair:'+genuine['receipt']+held['readback_authority']).digest()
    finished = cleanup_redrive_repair(repair_redrive(required,ledger,receipt),ledger)
    closed = held_delivery(finished,cause_cleared=True,routes_terminal=True,ledger=ledger)
    interrupted_erasure=deepcopy(ledger); held_used=interrupted_erasure.deployment
    terminal=sha256(b'terminal-held-erasure:'+held['held_key']+N(closed['redrive_count'])).digest()
    assert erase_held_delivery(closed,interrupted_erasure,terminal,crash_after='repair-interest') == closed
    assert interrupted_erasure.deployment == held_used and closed['inventory_key'] in interrupted_erasure.inventory
    refused=deepcopy(interrupted_erasure); refused.generations[('deployment','deployment')]=U64_MAX
    assert erase_held_delivery(closed,refused,terminal) == closed and refused.deployment == held_used and closed['inventory_key'] in refused.inventory
    assert erase_held_delivery(closed,interrupted_erasure,terminal)['state'] == 'erased' and interrupted_erasure.deployment == 0
    erased = erase_held_delivery(closed,ledger,sha256(b'terminal-held-erasure:'+held['held_key']+N(closed['redrive_count'])).digest())
    assert erased['state'] == 'erased' and ledger.deployment == 0 and not ledger.objects and not ledger.charges and not ledger.inventory and not ledger.redrive_attempts and not ledger.redrive_requests and not ledger.request_deletions and not ledger.attempt_deletions
    refunded = deepcopy(vars(ledger)); assert erase_held_delivery(erased,ledger,erased['erasure_receipt']) == erased and vars(ledger) == refunded
    return {'utc':utc_cases,'partial_capture':capture_cases,'redrive_refusals':refusals,'request_refusals':request_cases,'request_restarts':request_restarts,'erasure_refusals':erasure_refusals,'cleanup':cleanup_cases,'failed_redrives':141}

class ScopeShardModel:
    # Existing A8 required authority, D4 claims and tombstones share one scope key.
    reserves={'HX-EV-COMMAND-SCOPE-LEGACY-2':8192,'HX-EV-COMMAND-SCOPE-TOMBSTONE-2':4096}
    required_schema=['U','U','U','U','U','B32','B32','B32','U','U']
    def __init__(self,tenant,shard,ceiling):
        self.tenant,self.shard,self.ceiling=tenant,shard,ceiling; self.backend=QueueBytes()
        self.key=codec['K']('HX-EV-SCOPE-SHARD-USAGE-KEY-1',U(tenant),N(shard))
        assert ceiling>=2048
        raw=R('HX-EV-SCOPE-SHARD-USAGE-1',7,U(tenant),N(shard),N(0),N(0),N(2048),N(1),bytes(32))
        decode_record(raw,'HX-EV-SCOPE-SHARD-USAGE-1',codec['schemas']['D12-usage'])
        self.backend.write(self.key,raw,bytes(32),1)
    def typed(self,raw):
        domain=raw.split(b'\0',1)[0].decode()
        if domain=='HX-EV-COMMAND-SCOPE-1': schema=self.required_schema; reserve=4096
        else:
            reserve=self.reserves[domain]
            schema=codec['schemas']['D12-legacy-claim' if domain=='HX-EV-COMMAND-SCOPE-LEGACY-2' else 'D12-tombstone']
        fields=decode_record(raw,domain,schema)
        shard=sha256(U(fields[0])+U(fields[1])).digest()[0]
        assert fields[0]==self.tenant and shard==self.shard, 'scope-derived-shard-authority'
        if domain=='HX-EV-COMMAND-SCOPE-TOMBSTONE-2': assert fields[7]==shard
        return domain,fields,reserve
    def compaction_proof(self,predecessor,now):
        # Native fixture authority covers all retry/status/rollback/backup obligations.
        return sha256(b'fixture-scope-compaction-obligations-absent:'+predecessor+Q(now)).digest()
    def change(self,raw,delete=False,now=None,absence=None,predecessor=None,obligations=None):
        before=deepcopy(vars(self.backend))
        try:
            domain,fields,reserve=self.typed(raw)
            address=codec['command_scope_address'](fields[0],fields[1]); owner=sha256(U(address)).digest()
            previous,receipt,generation=self.backend.read(self.key,bytes(32))
            usage=list(decode_record(previous,'HX-EV-SCOPE-SHARD-USAGE-1',codec['schemas']['D12-usage']))
            assert usage[:2]==[self.tenant,self.shard] and usage[5]==generation, 'scope-usage-shard-binding'
            staged=deepcopy(self.backend); required_delta=tombstone_delta=0
            existing=staged.read(address,owner) if address in staged.records else None
            if delete:
                assert existing is not None and existing[0]==raw and domain!='HX-EV-COMMAND-SCOPE-1'
                expiry=fields[7] if domain=='HX-EV-COMMAND-SCOPE-LEGACY-2' else fields[6]
                assert now>=expiry and absence==sha256(b'fixture-scope-obligations-absent:'+raw+Q(now)).digest()
                deleted=staged.delete(address,owner); assert staged.receipt(address)[0]==deleted
                staged.forget(address); delta=-reserve
                tombstone_delta=-int(domain=='HX-EV-COMMAND-SCOPE-TOMBSTONE-2')
            elif existing is not None and existing[0]==raw:
                if domain=='HX-EV-COMMAND-SCOPE-LEGACY-2':
                    assert now is not None and now<fields[7], 'legacy-exact-retry-expiry'
                if predecessor is not None:
                    assert fields[4]==sha256(predecessor).digest() and obligations==self.compaction_proof(predecessor,fields[5])
                return True
            elif domain=='HX-EV-COMMAND-SCOPE-TOMBSTONE-2':
                assert existing is not None and predecessor==existing[0], 'scope-compaction-required-predecessor'
                original_kind,original,original_reserve=self.typed(predecessor)
                assert original_kind=='HX-EV-COMMAND-SCOPE-1' and now==fields[5]
                assert fields[:4]==original[:2]+(original[5],original[6]) and fields[4]==sha256(predecessor).digest(), 'scope-compaction-binding'
                assert obligations==self.compaction_proof(predecessor,now), 'scope-compaction-obligations'
                assert original_reserve==reserve==4096, 'scope-compaction-unchanged-charge'
                staged.write(address,raw,owner,existing[2]+1)
                required_delta=-1; tombstone_delta=1; delta=0
            elif existing is not None:
                original_kind,original,original_reserve=self.typed(existing[0])
                if domain==original_kind=='HX-EV-COMMAND-SCOPE-1':
                    assert fields[:7]==original[:7]
                    return True
                assert original_kind=='HX-EV-COMMAND-SCOPE-LEGACY-2' and predecessor==existing[0]
                assert now>=original[7] and obligations==self.compaction_proof(predecessor,now), 'scope-migration-obligations'
                if domain=='HX-EV-COMMAND-SCOPE-LEGACY-2':
                    assert fields[:6]==original[:6] and fields[8:]==original[8:], 'legacy-renewal-cohort-input'
                    assert fields[6]==now and fields[7]>now and existing[2]<U64_MAX, 'legacy-renewal-utc-generation'
                    assert original_reserve==reserve==8192
                    staged.write(address,raw,owner,existing[2]+1)
                    delta=required_delta=0
                else:
                    assert domain=='HX-EV-COMMAND-SCOPE-1' and fields[:3]==original[:3] and fields[4]==original[3] and fields[6]==original[5]
                    deleted=staged.delete(address,owner); assert staged.receipt(address)[0]==deleted
                    staged.forget(address); staged.write(address,raw,owner,1)
                    delta=reserve-original_reserve; required_delta=1
            else:
                assert domain!='HX-EV-COMMAND-SCOPE-TOMBSTONE-2', 'scope-tombstone-needs-required'
                assert len(raw)<=reserve and usage[4]+reserve<=self.ceiling, 'scope-shard-charged-capacity'
                staged.write(address,raw,owner,1); delta=reserve
                required_delta=int(domain=='HX-EV-COMMAND-SCOPE-1')
            assert generation<U64_MAX and usage[4]+delta<=self.ceiling
            usage[2]+=required_delta; usage[3]+=tombstone_delta; usage[4]+=delta
            usage[5]=generation+1; usage[6]=sha256(previous).digest()
            assert min(usage[2:5])>=0
            successor=R('HX-EV-SCOPE-SHARD-USAGE-1',7,*(codec['encode_typed'](k,v) for k,v in zip(codec['schemas']['D12-usage'],usage)))
            decode_record(successor,'HX-EV-SCOPE-SHARD-USAGE-1',codec['schemas']['D12-usage'])
            staged.write(self.key,successor,bytes(32),generation+1)
            self.backend.records,self.backend.native=staged.records,staged.native
            return True
        except (AssertionError,KeyError,ValueError,TypeError):
            assert vars(self.backend)==before
            return False
    def status(self,execution):
        address=codec['command_scope_address'](self.tenant,execution); owner=sha256(U(address)).digest()
        domain,fields,reserve=self.typed(self.backend.read(address,owner)[0])
        return (410,'https://hexalith.io/problems/command-status-expired') if domain=='HX-EV-COMMAND-SCOPE-TOMBSTONE-2' else (200,'required' if domain=='HX-EV-COMMAND-SCOPE-1' else 'legacy')
    def erase(self,authority):
        before=deepcopy(vars(self.backend))
        try:
            expected=sha256(b'fixture-scope-tenant-erasure:'+U(self.tenant)+N(self.shard)+image_bytes(self.backend.records)).digest()
            assert authority==expected, 'scope-tenant-erasure-authority'
            staged=deepcopy(self.backend)
            for address in list(staged.records):
                if address==self.key: owner=bytes(32)
                else:
                    self.typed(staged.records[address]); owner=sha256(U(address)).digest()
                staged.read(address,owner); staged.delete(address,owner); staged.forget(address)
            self.backend.records,self.backend.native=staged.records,staged.native
            return True
        except (AssertionError,KeyError,ValueError,TypeError):
            assert vars(self.backend)==before; return False

def verify_loop8_lifecycles():
    pin_cases=scope_cases=resume_cases=queue_cases=refusals=0
    queue_case_ids=[]
    def queue_case(label):
        assert label not in queue_case_ids
        queue_case_ids.append(label)
    # Actual batch admission, with fit counters, rejects one oversize pin first.
    for length in (449*MiB-1,449*MiB,449*MiB+1):
        model=Ledger(); amounts=[length+MiB]; rows=model.pin_candidates('t',amounts)
        before=deepcopy(vars(model)); admitted=model.reserve_pin_batch('t',amounts,model.predecessors('t'),b'width',candidates=rows)
        assert admitted==(length<=449*MiB)
        if admitted:
            assert model.tenant[('tenant','t')]==model.tenant_pool==model.deployment==length+MiB
            after=deepcopy(vars(model)); assert model.read_pin_reservation('t',amounts,b'width',candidates=rows)
            assert vars(model)==after
        else: assert vars(model)==before; refusals+=1
        pin_cases+=1
    model=Ledger(); amounts=[11*MiB]; rows=model.pin_candidates('t',amounts)
    for damage in ('missing','unavailable','length','overhead','amount','kind','capability'):
        bad=deepcopy(model); candidates=rows
        if damage=='missing': candidates=None
        elif damage=='unavailable': bad.pin_evidence_available=False
        else:
            fields=list(decode_record(rows[0],'HX-EV-PUBLICATION-CHARGE-2',codec['schemas']['D29-charge']))
            index={'length':5,'overhead':6,'amount':7,'kind':4,'capability':8}[damage]
            fields[index]=('retained-object' if damage=='kind' else fields[index]+1)
            raw=R('HX-EV-PUBLICATION-CHARGE-2',15,*(codec['encode_typed'](k,v) for k,v in zip(codec['schemas']['D29-charge'],fields)))
            candidates=(raw,)
            bad.pin_candidate_authority[sha256(raw).digest()]=sha256(b'fixture-authenticated-pin-candidate:'+bad.pin_capability+raw).digest()
        before=deepcopy(vars(bad))
        assert not bad.reserve_pin_batch('t',amounts,bad.predecessors('t'),b'bad',candidates=candidates)
        assert vars(bad)==before; pin_cases+=1; refusals+=1
    for label in ('D12-legacy-claim','D12-tombstone'):
        domain=codec['vectors'][label].split(b'\0',1)[0].decode(); schema=codec['schemas'][label]
        fields=list(decode_record(codec['vectors'][label],domain,schema))
        for index in ((0,1,2,3,4) if label=='D12-legacy-claim' else (0,1)): fields[index]='i'*1024
        raw=R(domain,len(schema),*(codec['encode_typed'](k,v) for k,v in zip(schema,fields)))
        if label=='D12-tombstone': fields[7]=sha256(U(fields[0])+U(fields[1])).digest()[0]; raw=R(domain,len(schema),*(codec['encode_typed'](k,v) for k,v in zip(schema,fields)))
        reserve=ScopeShardModel.reserves[domain]
        assert reserve==({'D12-legacy-claim':8192,'D12-tombstone':4096}[label])
        for ceiling in (2048+reserve-1,2048+reserve,2048+reserve+1):
            shard=ScopeShardModel(fields[0],sha256(U(fields[0])+U(fields[1])).digest()[0],ceiling); before=deepcopy(vars(shard.backend))
            if label=='D12-tombstone':
                scope=sha256(U(fields[0])+U('d')+U('counter')+U('a')+U(fields[1])).digest()
                required=R('HX-EV-COMMAND-SCOPE-1',10,U(fields[0]),U(fields[1]),U('d'),U('counter'),U('a'),scope,fields[3],codec['H']('admission'),U('required'),U('corr'))
                fields[2]=scope; fields[4]=sha256(required).digest(); raw=R(domain,len(schema),*(codec['encode_typed'](k,v) for k,v in zip(schema,fields)))
                admitted=shard.change(required)
                if admitted: assert shard.change(raw,now=fields[5],predecessor=required,obligations=shard.compaction_proof(required,fields[5]))
            else: admitted=shard.change(raw)
            assert admitted==(ceiling>=2048+reserve)
            if ceiling<2048+reserve: assert vars(shard.backend)==before; refusals+=1
            else:
                installed=deepcopy(vars(shard.backend)); assert shard.change(raw,now=fields[6] if label=='D12-legacy-claim' else None) and vars(shard.backend)==installed
                expiry=fields[7] if label=='D12-legacy-claim' else fields[6]
                assert not shard.change(raw,True,expiry,None) and vars(shard.backend)==installed
                proof=sha256(b'fixture-scope-obligations-absent:'+raw+Q(expiry)).digest()
                assert shard.change(raw,True,expiry,proof)
                usage=decode_record(shard.backend.read(shard.key,bytes(32))[0],'HX-EV-SCOPE-SHARD-USAGE-1',codec['schemas']['D12-usage'])
                assert usage[4]==2048 and len(shard.backend.records)==1
                refunded=deepcopy(vars(shard.backend)); assert not shard.change(raw,True,expiry,proof) and vars(shard.backend)==refunded
                refusals+=2
            scope_cases+=1
    base=deepcopy(codec['fixture_prior']); base['handle']='h'
    identity,carrier=request(b'loop8-progress')
    # One accepted send advances within the same admission set and same claim.
    progressed=deepcopy(base); progressed['accepted']=base['roster'][:2]; progressed['unresolved']=base['roster'][2:]
    progressed['window_progress']=codec['window_progress_bytes'](progressed['window_admission'],progressed['accepted'],progressed['unresolved'],base['accepted'])
    for hold in ('publication_drain_limit_hold','publication_retry_exhausted_hold'):
        result=resume_publication(progressed,identity,carrier,hold,'current')
        assert result['outcome']=='resumed' and result['rearmed']==(('message-3',b'unresolved-b'),)
        assert result['accepted_unchanged']==progressed['accepted'] and result['state']['hold_source']==bytes(32)
        if hold=='publication_drain_limit_hold': assert result['state']['window_claim_bytes']==base['window_claim_bytes']
        else: assert decode_record(result['state']['window_claim_bytes'],'HX-EV-PUBLICATION-WINDOW-2',codec['schemas']['D45-window'])[7]==unresolved_root(progressed['unresolved'])
        next_id,next_carrier=request(b'loop8-source-reuse')
        before=deepcopy(result['state']); refusal=resume_publication(before,next_id,next_carrier,hold,'current')
        assert refusal['outcome']=='resume_hold_changed' and refusal['state']==before
        same=resume_publication(before,identity,carrier,hold,'stale'); assert same['outcome']=='exact-retry' and same['response']==result['response'] and same['state']==before
        fresh=deepcopy(before); fresh['hold_source']=codec['H']('fresh-exhaustion:'+hold)
        fresh_id,fresh_carrier=request(b'loop8-fresh',source=fresh['hold_source'])
        assert resume_publication(fresh,fresh_id,fresh_carrier,hold,'current')['outcome']=='resumed'
        eligible='drain-limit' if hold=='publication_drain_limit_hold' else 'retry-exhausted'
        prepared=owned_resume_preparation(resume_publication(progressed,identity,carrier,hold,'current',crash_after_audit=True),identity)
        for boundary in ('audit','successor-intent','successor-write','finalize'):
            store=PreparationStore(identity,carrier,prepared,progressed,eligible=eligible)
            assert store.turn(boundary)=='interrupted'
            store=restart_preparation(store); assert store is not None and store.turn()=='completed'
            state=decode_record(store.artifacts['successor'],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
            assert state[6]==bytes(32) and not any(row[2]=='active:'+progressed['hold_source'].hex() for row in inventory_rows(store))
            before=store.backend.snapshot(); assert store.turn()=='completed' and store.backend.snapshot()==before
            resume_cases+=1
        for name in ('window_admission','window_progress'):
            for damage in ('missing','changed'):
                bad=deepcopy(progressed)
                if damage=='missing': del bad[name]
                else: bad[name]=bad[name][:-1]+bytes([bad[name][-1]^1])
                before=deepcopy(bad); refused=resume_publication(bad,identity,carrier,hold,'current')
                assert refused['outcome']=='resume_evidence_hold' and refused['state']==before and bad==before
                refusals+=1; resume_cases+=1
        store=PreparationStore(identity,carrier,prepared,progressed,eligible=eligible)
        for kind in ('window_admission','window_progress'):
            address=store.window_source_key(progressed['window_claim'],kind)
            for damage in ('missing','changed','stale','unavailable'):
                bad=deepcopy(store)
                if damage=='missing': del bad.backend.rows[address]
                elif damage=='changed': bad.backend.rows[address]+=b'changed'
                elif damage=='stale':
                    owner,generation,receipt=bad.backend.receipts[address]
                    bad.backend.receipts[address]=(owner,generation+1,receipt)
                else: bad.backend.unavailable.add(address)
                before=bad.backend.snapshot(); assert bad.turn()=='evidence-hold' and bad.backend.snapshot()==before
                refusals+=1; resume_cases+=1
        assert store.turn()=='completed'
        old_sources=[store.window_source_key(progressed['window_claim'],kind) for kind in ('window_admission','window_progress')]
        current_claim=decode_record(store.artifacts['successor'],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])[7]
        current_sources=[store.window_source_key(current_claim,kind) for kind in ('window_admission','window_progress')]
        compact_successful_origin(store)
        assert all(address in store.backend.rows for address in current_sources)
        assert all((address in store.backend.rows)==(eligible=='drain-limit') for address in old_sources)
        resume_cases+=1
    regressed=deepcopy(progressed); regressed['accepted']=base['accepted']; regressed['unresolved']=base['unresolved']
    regressed['window_progress']=codec['window_progress_bytes'](regressed['window_admission'],regressed['accepted'],regressed['unresolved'],progressed['accepted'])
    assert resume_publication(regressed,identity,carrier,'publication_drain_limit_hold','current')['outcome']=='resume_evidence_hold'; refusals+=1; resume_cases+=1
    # Parked current state can change while the exact admission carrier remains immutable.
    q=Queues(3); assert q.add(1,'tenant-a','parked-a','parked') and q.add(2,'tenant-b','fit-b')
    original=q._check()['parked-a']; initial_carrier,initial_receipt,initial_wait=original[6],original[13],original[22]
    candidate=codec['H']('loop8-parked-candidate'); amount=13*MiB
    current=sha256(q.encode('D31-authority',original)).digest(); proof=q.rerender_proof('parked-a',candidate,amount)
    for authority,source in ((bytes(32),proof),(current,bytes(32))):
        before=deepcopy(vars(q.backend)); assert not q.rerender('parked-a',candidate,amount,authority,source) and vars(q.backend)==before; refusals+=1; queue_cases+=1
    assert q.rerender('parked-a',candidate,amount,current,proof)
    queue_case('authenticated parked exit')
    q=restart_queues(q); assert q.rows['deployment'][0]==[1,'tenant-a','parked-a','queued']
    queue_case('current state restart')
    a=q._check()['parked-a']; assert (a[6],a[13],a[22])==(initial_carrier,initial_receipt,initial_wait)
    queue_case('immutable admission evidence')
    assert q.add(1,'tenant-a','parked-a','parked') and q.deployment_turn(lambda _:True,lambda _:False)=='tenant:tenant-a'
    queue_case('lost acknowledgement and paired move')
    # Authenticate genuinely full tenant usage while another tenant continues.
    key=q.counter_key('tenant','tenant-a'); raw,receipt,g=q.backend.read(key,bytes(32)); f=list(decode_record(raw,'HX-EV-PUBLICATION-COUNTER-1',codec['schemas']['D29-counter']))
    f[3],f[5],f[6]=1024*MiB,g+1,sha256(raw).digest()
    q.backend.write(key,R('HX-EV-PUBLICATION-COUNTER-1',8,*(codec['encode_typed'](k,v) for k,v in zip(codec['schemas']['D29-counter'],f))),bytes(32),g+1)
    blocked=q.tenant_fit_receipt('tenant-a',False); before=deepcopy(vars(q.backend))
    assert q.tenant_turn('tenant-a',blocked)=='tenant:tenant-a' and vars(q.backend)==before
    queue_case('blocked tenant unchanged')
    assert q.deployment_turn(lambda _:True,lambda _:True)=='reserve' and q.where['fit-b']=='deployment'
    queue_case('other tenant advances')
    raw,receipt,g=q.backend.read(key,bytes(32)); f=list(decode_record(raw,'HX-EV-PUBLICATION-COUNTER-1',codec['schemas']['D29-counter']))
    f[3],f[5],f[6]=200*MiB,g+1,sha256(raw).digest()
    q.backend.write(key,R('HX-EV-PUBLICATION-COUNTER-1',8,*(codec['encode_typed'](k,v) for k,v in zip(codec['schemas']['D29-counter'],f))),bytes(32),g+1)
    before=deepcopy(vars(q.backend)); assert q.tenant_turn('tenant-a',blocked)=='pin_capacity_queue_corruption_hold' and vars(q.backend)==before
    queue_case('stale fit authority')
    assert q.tenant_turn('tenant-a',q.tenant_fit_receipt('tenant-a',True))=='deployment'
    queue_case('current fit return')
    q=restart_queues(q); assert q.tickets['parked-a']==1
    queue_case('ticket restart')
    corrupt=q.address('queue','deployment'); good=q.backend.records[corrupt]; q.backend.records[corrupt]+=b'corrupt'
    assert q.repair()=='repaired' and q.backend.records[corrupt]==good
    queue_case('repair uses current state')
    q=restart_queues(q); before=deepcopy(vars(q.backend)); assert q.rerender('parked-a',candidate,amount,sha256(q.encode('D31-authority',q._check()['parked-a'])).digest(),proof) and vars(q.backend)==before
    queue_case('exact rerender no-op')
    assert q.rollback('parked-a','tenant-a',1,q.preparation_authority('parked-a'))
    queue_case('parked-exit cleanup')
    infeasible=Queues(); assert infeasible.add(1,'t','still-parked','parked')
    candidate=codec['H']('too-large'); amount=1024*MiB+1; a=infeasible._check()['still-parked']
    assert infeasible.rerender('still-parked',candidate,amount,sha256(infeasible.encode('D31-authority',a)).digest(),infeasible.rerender_proof('still-parked',candidate,amount))
    before=deepcopy(vars(infeasible.backend)); assert infeasible.deployment_turn(lambda _:True,lambda _:True)=='noop' and vars(infeasible.backend)==before
    queue_case('still infeasible parked no-op')
    # Fresh o is once per owner/directory. A capability successor preserves old provenance.
    model=Queues(); assert model.add(1,'t','overhead-a')
    old={key:raw for key,raw in model.backend.records.items() if key.startswith('publication-charge:')}
    for raw in old.values():
        f=decode_record(raw,'HX-EV-PUBLICATION-CHARGE-2',codec['schemas']['D29-charge']); assert f[6]==MiB and f[7]==f[5]+MiB and f[8]==3
    old_cap=model._capability(); f=list(old_cap); f[1],f[7],f[9]=4,MiB+1024,sha256(model.backend.records[model.capability_key()]).digest()
    raw=R('HX-EV-PUBLICATION-RETENTION-CAPABILITY-2',15,*(codec['encode_typed'](k,v) for k,v in zip(codec['schemas']['D29-capability'],f)))
    model.backend.write(model.capability_key(4),raw,bytes(32),4)
    model.backend.write('fixture-queue-capability-head',N(4),bytes(32),1)
    assert model.add(2,'t','overhead-b')
    assert all(model.backend.records[key]==raw for key,raw in old.items())
    queue_case('original overhead provenance')
    new=decode_record(model.backend.read(model.charge_key(model.subject('overhead-b'),'t'),model.subject('overhead-b'))[0],'HX-EV-PUBLICATION-CHARGE-2',codec['schemas']['D29-charge'])
    assert new[5:9]==(40960,MiB+1024,40960+MiB+1024,4)
    queue_case('fresh capability overhead')
    owner_usage=2*40960+2*MiB+1024+142*MiB+32768+MiB
    tenant=decode_record(model.backend.read(model.counter_key('tenant','t'),bytes(32))[0],'HX-EV-PUBLICATION-COUNTER-1',codec['schemas']['D29-counter'])
    assert tenant[3]==owner_usage
    queue_case('charged totals include overhead once')
    assert model.erase_tenant('t',model.erasure_authority('t'))
    assert not any(key in model.backend.records for key in old if key!=model.charge_key(sha256(U(model.address('queue','deployment'))).digest(),'deployment:'+sha256(U('deployment-a')).hexdigest(),'capture-scope'))
    queue_case('erasure refunds original charges')
    queue_cases+=len(queue_case_ids)
    return {'pin_cases':pin_cases,'scope_cases':scope_cases,'resume_cases':resume_cases,'queue_cases':queue_cases,'refusals':refusals}

def verify_loop10_repairs():
    counts={'rollback':0,'finalize':0,'renewal':0,'account':0,'capture':0,'predecessor':0}
    base=deepcopy(codec['fixture_prior']); base['handle']='h'
    # Original predecessor UTC belongs to the authenticated old head, not to
    # the later request. Its bytes bind audit and persisted reconstruction.
    identity,carrier=request(b'loop10-earlier-predecessor',source=base['hold_source'])
    old=publication_state_bytes(base,999)
    result=resume_publication(base,identity,carrier,'publication_drain_limit_hold','current',
        crash_after_audit=True,predecessor_bytes=old)
    prepared=owned_resume_preparation(result,identity)
    earlier=PreparationStore(identity,carrier,prepared,base,predecessor_utc=999)
    assert earlier.imported('predecessor')==old and earlier.turn('audit-write')=='interrupted'
    restored=restart_preparation(earlier); assert restored is not None and restored.turn()=='completed'
    audit=decode_record(restored.artifacts['audit'],'HX-EV-PUBLICATION-RESUME-AUDIT-4',codec['schemas']['D45-audit'])
    assert audit[5]==sha256(old).digest(); counts['predecessor']+=1
    equal=resume_publication(base,identity,carrier,'publication_drain_limit_hold','current',
        crash_after_audit=True,predecessor_bytes=publication_state_bytes(base,1000))
    assert equal['outcome']=='orphaned-success'; counts['predecessor']+=1
    for wrong in (old[:-1]+bytes([old[-1]^1]),publication_state_bytes(base,1001)):
        refused=resume_publication(base,identity,carrier,'publication_drain_limit_hold','current',predecessor_bytes=wrong)
        assert refused['outcome']=='resume_evidence_hold' and refused['state']==base; counts['predecessor']+=1
    # Finalization cannot release an old charge before a later progress write
    # has passed its checked native-generation preflight.
    prepared=owned_resume_preparation(resume_publication(base,identity,carrier,'publication_drain_limit_hold','current',crash_after_audit=True),identity)
    final=PreparationStore(identity,carrier,prepared,base)
    assert final.turn('successor-write')=='interrupted'
    for generation in (U64_MAX-1,U64_MAX):
        boundary=deepcopy(final); head_key=boundary.head_key; head_raw=boundary.backend.rows[head_key]
        head=list(decode_record(head_raw,'HX-EV-RESUME-PREPARATION-HEAD-1',boundary.head_schema)); head[6]=generation
        head_raw=R('HX-EV-RESUME-PREPARATION-HEAD-1',len(head),*(codec['encode_typed'](kind,value) for kind,value in zip(boundary.head_schema,head)))
        boundary.backend.rows[head_key]=head_raw
        boundary.backend.receipts[head_key]=(identity,generation,boundary.backend.receipt(head_key,head_raw,identity,generation))
        before=boundary.backend.snapshot(); assert boundary.turn(now=2000)=='evidence-hold' and boundary.backend.snapshot()==before
        counts['finalize']+=1
    # A fresh claim and a capture scope with the same legal account ID have
    # independent usage, generations and predecessor receipts.
    account='a'*64; ledger=Ledger()
    assert ledger.reserve('tenant',account,[900*MiB])
    tenant_receipt=ledger.predecessors(account)
    assert ledger.reserve('capture-scope',account,[200*MiB])
    capture_receipt=ledger.predecessors(account,'capture-scope')
    assert ledger.tenant[('tenant',account)]==900*MiB and ledger.tenant[('capture-scope',account)]==200*MiB
    assert ledger.generations[('tenant',account)]==ledger.generations[('capture-scope',account)]==1
    assert tenant_receipt['tenant']!=capture_receipt['capture-scope']
    assert ledger.refund('capture-scope',account,200*MiB)
    assert ledger.tenant[('tenant',account)]==900*MiB and ledger.tenant[('capture-scope',account)]==0
    assert ledger.predecessors(account)['tenant']==tenant_receipt['tenant']
    assert ledger.predecessors(account,'capture-scope')['capture-scope']!=capture_receipt['capture-scope']
    counts['account']+=1
    # Expired identical bytes require closure, and a renewal preserves the
    # original cohort and cutover while advancing the addressed native row.
    claim=codec['vectors']['D12-legacy-claim']
    claim_fields=list(decode_record(claim,'HX-EV-COMMAND-SCOPE-LEGACY-2',codec['schemas']['D12-legacy-claim']))
    shard=sha256(U(claim_fields[0])+U(claim_fields[1])).digest()[0]
    scope=ScopeShardModel('t',shard,1024*MiB); expiry=claim_fields[7]
    assert scope.change(claim,now=claim_fields[6])
    snapshot=deepcopy(vars(scope.backend))
    assert not scope.change(claim,now=expiry) and vars(scope.backend)==snapshot
    claim_fields[6]=expiry; claim_fields[7]=expiry+864000000000
    renewal=R('HX-EV-COMMAND-SCOPE-LEGACY-2',10,*(codec['encode_typed'](kind,value) for kind,value in zip(codec['schemas']['D12-legacy-claim'],claim_fields)))
    assert not scope.change(renewal,now=expiry,predecessor=claim) and vars(scope.backend)==snapshot
    assert scope.change(renewal,now=expiry,predecessor=claim,obligations=scope.compaction_proof(claim,expiry))
    address=codec['command_scope_address']('t','op'); owner=sha256(U(address)).digest()
    assert scope.backend.read(address,owner)[2]==2
    usage=decode_record(scope.backend.read(scope.key,bytes(32))[0],'HX-EV-SCOPE-SHARD-USAGE-1',codec['schemas']['D12-usage'])
    assert usage[4]==2048+8192
    restarted=ScopeShardModel('t',shard,1024*MiB); restarted.backend=deepcopy(scope.backend)
    snapshot=deepcopy(vars(restarted.backend)); assert restarted.change(renewal,now=expiry+1) and vars(restarted.backend)==snapshot
    claim_fields[5]=codec['H']('changed-command-input')
    changed=R('HX-EV-COMMAND-SCOPE-LEGACY-2',10,*(codec['encode_typed'](kind,value) for kind,value in zip(codec['schemas']['D12-legacy-claim'],claim_fields)))
    assert not restarted.change(changed,now=expiry+1,predecessor=renewal,obligations=restarted.compaction_proof(renewal,expiry+1))
    counts['renewal']+=1
    # The capture matrix checks every fresh refusal against the entire ledger,
    # including generations; a charge-only rollback needs object-absence proof.
    assert verify_held_delivery_and_long_stream_matrix()=='held-delivery-or-long-stream'; counts['capture']+=1
    capture_ledger=Ledger(); capture_identity=('tenant','deployment-a','t','pubsub','orders','sub-a')
    capture_carrier=b'loop10-capture-object-absence'
    observed=observe_delivery(None,638712864000000000,capture_ledger,capture_identity,capture_carrier)
    capture_key='held/'+observed['held_key'].hex()
    readback={'backend':'held-delivery-store','key':capture_key,'bytes':capture_carrier,
        'receipt':object_receipt('held-delivery-store',capture_key,capture_carrier)}
    policy_store=PolicyStore(); policy=policy_store.install(None,1,bytes(32))
    partial=capture_delivery(observed,capture_carrier,capture_ledger,readback,policy,policy_store,crash_at='charge')
    assert partial==observed and capture_key not in capture_ledger.objects
    absence=presence(observed['held_key'],'capture','absent'); before=deepcopy(vars(capture_ledger))
    assert rollback_capture(partial,capture_carrier,capture_ledger,absence)==partial and vars(capture_ledger)==before
    assert rollback_capture(partial,capture_carrier,capture_ledger,absence,
        object_absence=presence(observed['held_key'],'object','absent'))==partial
    counts['capture']+=1
    # A permitted later expiry CAS remains the authority for rollback. Only
    # this identity's bounded tombstone is added over that current head.
    first_owner,first_carrier=request(b'loop10-rollback-existing',source=base['hold_source'])
    existing=resume_publication(base,first_owner,first_carrier,'publication_drain_limit_hold','current')['state']
    existing['hold_source']=codec['H']('loop10-rollback-source')
    rollback_owner,rollback_carrier=request(b'loop10-rollback-new',source=existing['hold_source'])
    rollback_prepared=owned_resume_preparation(resume_publication(existing,rollback_owner,rollback_carrier,
        'publication_drain_limit_hold','current',crash_after_audit=True),rollback_owner)
    rollback=PreparationStore(rollback_owner,rollback_carrier,rollback_prepared,existing)
    assert rollback.turn('resolution')=='interrupted'
    rollback_template=deepcopy(rollback)
    state_key=rollback.artifact_key('state'); old,generation,_=rollback.backend.read(state_key,rollback_owner)
    advanced=reconcile_tombstones(existing,2000)
    rollback.backend.write(state_key,publication_state_bytes(advanced,2000),rollback_owner,generation+1,sha256(old).digest())
    result=rollback.rollback(presence(rollback_owner,'successor','absent'),presence(rollback_owner,'audit','absent'))
    assert isinstance(result,tuple) and result[0]=='rolled-back'
    current=decode_record(rollback.backend.read(state_key,rollback_owner)[0],
        'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
    assert current[13]==2000*10000000 and int.from_bytes(current[11][:4],'big')==0
    tombs=codec['decode_rows'](current[12][4:],int.from_bytes(current[12][:4],'big'),['B32','B32','Q','Q'])
    assert {row[0] for row in tombs}=={first_owner,rollback_owner}; counts['rollback']+=1
    for current_time,expected_live,expected_tombs in ((1001,1,1),(1900+30*86400,0,0)):
        case=deepcopy(rollback_template)
        old,generation,_=case.backend.read(state_key,rollback_owner)
        current_state=reconcile_tombstones(existing,current_time)
        case.backend.write(state_key,publication_state_bytes(current_state,current_time),rollback_owner,generation+1,sha256(old).digest())
        restarted=restart_preparation(case); assert restarted is not None
        answer=restarted.rollback(presence(rollback_owner,'successor','absent'),presence(rollback_owner,'audit','absent'))
        assert isinstance(answer,tuple) and answer[0]=='rolled-back'
        current=decode_record(restarted.backend.read(state_key,rollback_owner)[0],
            'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
        assert current[13]==current_time*10000000
        assert (int.from_bytes(current[11][:4],'big'),int.from_bytes(current[12][:4],'big'))==(expected_live,expected_tombs)
        counts['rollback']+=1
    # A provider-authenticated CAS cannot replace an unrelated live or
    # tombstone row. Only exact expiry/deletion reconciliation may intervene.
    for current_time,kind in ((1001,'live'),(2000,'tombstones')):
        case=deepcopy(rollback_template)
        old,generation,_=case.backend.read(state_key,rollback_owner)
        changed=reconcile_tombstones(existing,current_time)
        changed[kind][first_owner]['carrier_hash']=codec['H']('unauthorized-'+kind)
        changed_raw=publication_state_bytes(changed,current_time)
        case.backend.write(state_key,changed_raw,rollback_owner,generation+1,sha256(old).digest())
        before=case.backend.snapshot()
        assert not case.predecessor_readback(changed_raw), 'rollback-intervening-index-authority'
        assert case.rollback(presence(rollback_owner,'successor','absent'),presence(rollback_owner,'audit','absent'))=='cleanup-hold'
        assert case.backend.snapshot()==before
        counts['rollback']+=1
    for defect in ('generation','capacity'):
        case=deepcopy(rollback_template)
        old,generation,_=case.backend.read(state_key,rollback_owner)
        if defect=='generation':
            case.backend.receipts[state_key]=(rollback_owner,U64_MAX,case.backend.receipt(state_key,old,rollback_owner,U64_MAX))
        else:
            fields=list(decode_record(old,'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state']))
            rows=tuple((codec['H']('capacity-'+str(i)),codec['H']('carrier-'+str(i)),1000*10000000,(1000+30*86400)*10000000) for i in range(64))
            fields[11]=codec['pack']('>I',0)
            fields[12]=codec['pack']('>I',64)+b''.join(identity+carrier+Q(expiry)+Q(deadline) for identity,carrier,expiry,deadline in sorted(rows))
            raw=R('HX-EV-PUBLICATION-RESUME-STATE-3',14,*(codec['encode_typed'](kind,value) for kind,value in zip(codec['schemas']['D45-state'],fields)))
            case.backend.write(state_key,raw,rollback_owner,generation+1,sha256(old).digest())
        before=case.backend.snapshot()
        assert case.rollback(presence(rollback_owner,'successor','absent'),presence(rollback_owner,'audit','absent'))=='cleanup-hold'
        assert case.backend.snapshot()==before; counts['rollback']+=1
    return counts

def verify_loop10_durable_lifetime(cycles=67):
    state=deepcopy(codec['fixture_prior']); state['handle']='h'
    backend=None; state_key=None; at=1000; prior_owner=None
    for number in range(1,cycles+1):
        # A distinct addressed drain-limit source is authenticated before each
        # admission. This source is closed after the consumed state reads back.
        source=R('HX-EV-PUBLICATION-DRAIN-LIMIT-2',10,U('t'),codec['H']('scope'),U('operation'),
            N(state['window']),N(state['limit']),codec['H']('drain-head-'+str(number)),
            codec['H']('outcome-head-'+str(number)),N(number),U('pending'),Q(at*10000000))
        source_key=codec['K']('HX-EV-PUBLICATION-DRAIN-LIMIT-KEY-1',codec['H']('scope'),N(state['window']),N(state['limit']))
        before_source=deepcopy(state)
        state['hold_source']=sha256(source).digest()
        owner,carrier=request(('loop10-durable-'+str(number)).encode(),source=state['hold_source'])
        expiry=at+900
        if backend is None:
            result=resume_publication(state,owner,carrier,'publication_drain_limit_hold','current',now=at,expires_at=expiry,crash_after_audit=True)
            store=PreparationStore(owner,carrier,owned_resume_preparation(result,owner),state,now=at,expiry=expiry)
            backend=store.backend; state_key=store.artifact_key('state')
        else:
            old,generation,receipt=backend.read(state_key,prior_owner)
            assert old==publication_state_bytes(before_source,at-1), 'durable-lifetime-current-head'
            current=publication_state_bytes(state,at-1)
            backend.write(state_key,current,prior_owner,generation+1,sha256(old).digest())
            store=admit_after_compaction(backend,owner,carrier,state,now=at,expiry=expiry)
            assert store is not None, 'durable-lifetime-admission'
        backend.write(source_key,source,bytes(32),create_once=True)
        assert backend.read(source_key,bytes(32))[0]==source
        if number==cycles:
            assert store.turn('successor-write',now=at)=='interrupted'
            store=restart_preparation(store); assert store is not None, 'durable-lifetime-persisted-restart'
        assert store.turn(now=at)=='completed', 'durable-lifetime-completion'
        backend=store.backend
        exact=resume_publication(state,owner,carrier,'publication_drain_limit_hold','current',now=at,expires_at=expiry,
            predecessor_bytes=store.imported('predecessor'))
        state=exact['state']; assert exact['outcome']=='resumed'
        assert backend.read(state_key,owner)[0]==publication_state_bytes(state,at)
        retry=resume_publication(state,owner,carrier,'publication_drain_limit_hold','unavailable',now=at+1)
        assert retry['outcome']=='exact-retry' and retry['response']==exact['response']
        claim_key=store.artifact_key('claim'); assert backend.read_signed(claim_key,owner)
        assert compact_successful_origin(store) and store.origin_key(owner) not in backend.rows
        backend.delete(source_key,bytes(32),sha256(source).digest()); backend.deletions.pop(source_key,None)
        for key,(recorded_owner,generation,receipt) in backend.receipts.items():
            assert backend.read(key,recorded_owner)[2]==receipt and 0<generation<=U64_MAX
        assert len(backend.rows)<=40 and len(backend.receipts)<=40, ('durable-lifetime-body-bound',number,len(backend.rows),len(backend.receipts))
        state=reconcile_tombstones(state,expiry)
        old,generation,_=backend.read(state_key,owner)
        backend.write(state_key,publication_state_bytes(state,expiry),owner,generation+1,sha256(old).digest())
        deadline=expiry+30*86400
        assert not reclaim_resume_identity(backend,'t','op',owner,deadline-1)
        assert reclaim_resume_identity(backend,'t','op',owner,deadline), 'durable-lifetime-once-only-reclaim'
        snapshot=backend.snapshot(); assert not reclaim_resume_identity(backend,'t','op',owner,deadline) and backend.snapshot()==snapshot
        assert claim_key not in backend.rows and claim_key not in backend.receipts
        state=reconcile_tombstones(state,deadline)
        assert backend.read(state_key,owner)[0]==publication_state_bytes(state,deadline)
        prior_owner=owner; at=deadline+1
    assert cycles>64
    return cycles

def verify_loop10_maximum():
    base=deepcopy(codec['fixture_prior']); base['handle']='h'
    cached=dict(codec['c2_readbacks'])
    schema=['U','B32','N','B32','N','B','B32','Q']
    # Exercise the legal complete-set and actual preparation/restart consumers
    # with 59 distinct maximum-width committed identities and 64 nonce attempts.
    maximum_prior=deepcopy(base)
    maximum_prior['roster']=tuple((position,'m'*1020+f'{position:04d}',b'b'*8192) for position in range(1,60))
    maximum_prior['accepted']=(); maximum_prior['unresolved']=maximum_prior['roster']
    maximum_prior['window_admission']=codec['window_admission_bytes'](maximum_prior['roster'])
    maximum_prior['window_progress']=codec['window_progress_bytes'](maximum_prior['window_admission'],(),maximum_prior['roster'])
    maximum_prior['window_claim_bytes']=existing_window_bytes('t',maximum_prior['roster'],maximum_prior['roster'])
    maximum_prior['window_claim']=sha256(maximum_prior['window_claim_bytes']).digest()
    assert len({message for position,message,body in maximum_prior['roster']})==59
    assert len(maximum_prior['window_admission'])<=64*1024 and len(maximum_prior['window_progress'])<=128*1024
    maximum={}; maximum_rows=[]
    codec['c2_readbacks'].clear()
    for position,message,body in maximum_prior['roster']:
        parent=codec['H'](f'native-parent-{position}'); send=codec['H'](f'native-send-{position}')
        for local in range(1,65):
            observations=tuple((i,kind,codec['H'](f'native-max-{position}-{local}-{kind}')) for i,kind in enumerate(('register','unknown','result')))
            source=codec['c2_fixture_readback']('t',codec['H']('scope'),7,position,message,local,parent,send,observations,send_row=1,nonce_ordinal=local)
            maximum[('t',codec['H']('scope'),7,position,local)]=image_bytes(source)
            maximum_rows.extend(codec['pack']('>I',position)+N(local)+N(observation)+U(kind)+parent+send+evidence
                for observation,kind,evidence in observations)
    maximum_body=b''.join(maximum_rows); maximum_count=len(maximum_rows)
    maximum_root=sha256(b'HX-EV-WINDOW-ATTEMPTS-2\0\x01'+U('t')+codec['H']('scope')+N(7)+
        sha256(image_bytes(maximum_prior['roster'])).digest()+N(maximum_count)+B(maximum_body)).digest()
    maximum_attempts=R('HX-EV-WINDOW-ATTEMPT-SET-1',8,U('t'),codec['H']('scope'),N(7),
        sha256(image_bytes(maximum_prior['roster'])).digest(),N(maximum_count),B(maximum_body),maximum_root,Q(1000*10000000))
    assert len(maximum)==59*64 and maximum_count==59*64*3 and max(map(len,maximum.values()))<=16*1024
    assert sum(map(len,maximum.values()))>2*MiB and len(maximum_attempts)<=64*MiB
    decoded=decode_record(maximum_attempts,'HX-EV-WINDOW-ATTEMPT-SET-1',schema,c2_store=codec['c2_readbacks'])
    assert decoded[6]==maximum_root
    maximum_closure=publication_closure_bytes(maximum_prior,maximum_attempts,b'broker',1000,codec['c2_readbacks'])
    assert decode_record(maximum_closure,'HX-EV-PUBLICATION-WINDOW-CLOSURE-3',codec['schemas']['D45-closure'],
        attempt_store={('t',codec['H']('scope'),7):maximum_attempts},c2_store=codec['c2_readbacks'])[7]==maximum_root
    maximum_owner,maximum_carrier=request(b'loop10-maximum-c2',source=maximum_prior['hold_source'])
    maximum_result=resume_publication(maximum_prior,maximum_owner,maximum_carrier,'publication_retry_exhausted_hold',
        'current',crash_after_audit=True,attempt_authority=maximum_attempts,c2_authority=codec['c2_readbacks'])
    assert maximum_result['outcome']=='orphaned-success'
    maximum_prepared=owned_resume_preparation(maximum_result,maximum_owner)
    assert len(maximum_prepared)<=2*MiB
    maximum_store=PreparationStore(maximum_owner,maximum_carrier,maximum_prepared,maximum_prior,
        eligible='retry-exhausted',attempt_authority=maximum_attempts)
    assert maximum_store.turn('closure-write')=='interrupted'
    codec['c2_readbacks'].clear()
    maximum_restarted=restart_preparation(maximum_store)
    assert maximum_restarted is not None and maximum_restarted.turn()=='completed'
    assert len(maximum_restarted.backend.c2_sources)==59*64
    preparation_slots=(maximum_restarted.origin_key(maximum_owner),maximum_restarted.preparation_key(maximum_owner),maximum_restarted.head_key)
    assert sum(len(maximum_restarted.backend.rows[key]) for key in preparation_slots)<2*MiB
    codec['c2_readbacks'].update(cached)
    return {'members':59,'attempts':59*64,'observations':maximum_count,'prepared_bytes':len(maximum_prepared)}

def verify_loop9_lifecycles():
    counts={'closure':0,'shard':0,'binding':0,'eligibility':0,'lifetime':0,'limits':0,'compaction':0,'atomic':0,'c2_native':0}
    base=deepcopy(codec['fixture_prior']); base['handle']='h'
    schema=['U','B32','N','B32','N','B','B32','Q']
    def attempt_image(fields,rows):
        fields=list(fields); fields[4]=len(rows)
        fields[5]=b''.join(codec['pack']('>I',p)+N(local)+N(obs)+U(kind)+parent+send+evidence for p,local,obs,kind,parent,send,evidence in rows)
        fields[6]=sha256(b'HX-EV-WINDOW-ATTEMPTS-2\0\x01'+U(fields[0])+fields[1]+N(fields[2])+fields[3]+N(fields[4])+B(fields[5])).digest()
        return R('HX-EV-WINDOW-ATTEMPT-SET-1',8,*(codec['encode_typed'](k,v) for k,v in zip(schema,fields)))
    # Initial and successive windows cover only their immutable admitted members.
    state=deepcopy(base)
    for index in range(3):
        state['hold_source']=codec['H']('loop9-closure-source-'+str(index))
        identity,carrier=request(('loop9-closure-'+str(index)).encode(),source=state['hold_source'])
        authority=existing_attempt_bytes(state,1000)
        fields=decode_record(authority,'HX-EV-WINDOW-ATTEMPT-SET-1',schema)
        rows=codec['decode_rows'](fields[5],fields[4],['P','N','N','U','B32','B32','B32'])
        assert {r[0] for r in rows}=={2,3} and not any(r[0]==1 for r in rows)
        closed=publication_closure_bytes(state,authority,b'broker',1000)
        decoded=decode_record(closed,'HX-EV-PUBLICATION-WINDOW-CLOSURE-3',codec['schemas']['D45-closure'],attempt_store={tuple(fields[:3]):authority})
        assert {r[0] for r in codec['decode_rows'](decoded[3][4:],2,['P','N','B32'])}=={2,3}
        counts['closure']+=1
        for missing in (2,3):
            partial=attempt_image(fields,[r for r in rows if r[0]!=missing])
            result=resume_publication(state,identity,carrier,'publication_retry_exhausted_hold','current',attempt_authority=partial)
            assert result['outcome']=='resume_evidence_hold' and result['state']==state; counts['closure']+=1
        parent,send=codec['H']('unadmitted-parent-'+str(index)),codec['H']('unadmitted-send-'+str(index))
        observations=((0,'register',codec['H']('unadmitted-registration-'+str(index))),(1,'result',codec['H']('unadmitted-result-'+str(index))))
        codec['c2_fixture_readback']('t',codec['H']('scope'),state['window'],1,'accepted-earlier',1,parent,send,observations)
        extra=attempt_image(fields,sorted(rows+[(1,1,o,k,parent,send,e) for o,k,e in observations],key=lambda row:row[:3]))
        refused=resume_publication(state,identity,carrier,'publication_retry_exhausted_hold','current',attempt_authority=extra)
        assert refused['outcome']=='resume_evidence_hold' and refused['state']==state; counts['closure']+=1
        result=resume_publication(state,identity,carrier,'publication_retry_exhausted_hold','current',crash_after_audit=True,attempt_authority=authority)
        store=PreparationStore(identity,carrier,owned_resume_preparation(result,identity),state,eligible='retry-exhausted',attempt_authority=authority)
        assert store.turn('closure-write')=='interrupted'
        restored=restart_preparation(store); assert restored is not None and restored.turn()=='completed'
        assert restored.artifacts['closure']==closed if b'broker'==restored.imported('broker') else decode_record(restored.artifacts['closure'],'HX-EV-PUBLICATION-WINDOW-CLOSURE-3',codec['schemas']['D45-closure'],attempt_store={tuple(fields[:3]):authority})[3]==decoded[3]
        state=resume_publication(state,identity,carrier,'publication_retry_exhausted_hold','current')['state']
    progressed=deepcopy(base); progressed['accepted']=base['roster'][:2]; progressed['unresolved']=base['roster'][2:]
    progressed['window_progress']=codec['window_progress_bytes'](progressed['window_admission'],progressed['accepted'],progressed['unresolved'],base['accepted'])
    authority=existing_attempt_bytes(progressed,1000)
    assert decode_record(authority,'HX-EV-WINDOW-ATTEMPT-SET-1',schema)[4]==4
    counts['closure']+=1
    # C2 authority is freshly read from original durable source rows on restart.
    identity,carrier=request(b'loop9-native-c2'); authority=existing_attempt_bytes(base,1000)
    prepared=owned_resume_preparation(resume_publication(base,identity,carrier,'publication_retry_exhausted_hold','current',crash_after_audit=True,attempt_authority=authority),identity)
    owned=PreparationStore(identity,carrier,prepared,base,eligible='retry-exhausted',attempt_authority=authority)
    assert not any('c2-readbacks' in key for key in owned.backend.rows)
    assert owned.turn('closure-write')=='interrupted'
    cached=dict(codec['c2_readbacks']); codec['c2_readbacks'].clear()
    try:
        restored=restart_preparation(owned); assert restored is not None and restored.turn()=='completed'; counts['c2_native']+=1
        locator=next(iter(owned.backend.c2_sources))
        for damage in ('missing','changed','unavailable'):
            bad=deepcopy(owned)
            if damage=='missing': del bad.backend.c2_sources[locator]
            elif damage=='changed': bad.backend.c2_sources[locator]=image_bytes(('forged',))
            else: bad.backend.unavailable.add(locator)
            before=bad.backend.snapshot(); assert restart_preparation(bad) is None and bad.turn()=='evidence-hold' and bad.backend.snapshot()==before; counts['c2_native']+=1
        source_reader=deepcopy(restored); sources=deepcopy(owned.backend.c2_sources); assert compact_successful_origin(owned) and owned.backend.c2_sources==sources; counts['c2_native']+=1
        erasure=sha256(b'fixture-c2-tenant-erasure:'+U(owned.tenant)+image_bytes(sources)).digest()
        assert owned.erase_c2_sources(erasure)==len(sources) and not owned.backend.c2_sources; counts['c2_native']+=1
    finally: codec['c2_readbacks'].update(cached)
    # Legal maximum C2 recovery is exercised separately from the historical
    # loop-9 owner and restart probes.
    # Authentication and member/window binding survive recomputed self-consistent hashes.
    identity,carrier=request(b'loop9-binding')
    for index,value in ((0,'another-tenant'),(1,codec['H']('other-scope')),(3,99)):
        bad=deepcopy(base); f=list(decode_record(bad['window_claim_bytes'],'HX-EV-PUBLICATION-WINDOW-2',codec['schemas']['D45-window'])); f[index]=value
        bad['window_claim_bytes']=R('HX-EV-PUBLICATION-WINDOW-2',13,*(codec['encode_typed'](k,v) for k,v in zip(codec['schemas']['D45-window'],f))); bad['window_claim']=sha256(bad['window_claim_bytes']).digest()
        for hold in ('publication_drain_limit_hold','publication_retry_exhausted_hold'):
            result=resume_publication(bad,identity,carrier,hold,'current')
            assert result['outcome']=='resume_evidence_hold' and result['state']==bad and result['command_executions']==0; counts['binding']+=1
    # Current all-accepted progress and consumed hold sources cannot create new success.
    all_accepted=deepcopy(base); all_accepted['accepted']=base['roster']; all_accepted['unresolved']=()
    all_accepted['window_progress']=codec['window_progress_bytes'](all_accepted['window_admission'],all_accepted['accepted'],(),base['accepted'])
    for hold in ('publication_drain_limit_hold','publication_retry_exhausted_hold'):
        result=resume_publication(all_accepted,identity,carrier,hold,'current')
        assert result['outcome']=='resume_not_eligible' and result['state']==all_accepted; counts['eligibility']+=1
    success=resume_publication(base,identity,carrier,'publication_drain_limit_hold','current')
    second,second_carrier=request(b'loop9-consumed')
    result=resume_publication(success['state'],second,second_carrier,'publication_drain_limit_hold','current')
    assert result['outcome']=='resume_hold_changed' and result['state']==success['state']; counts['eligibility']+=1
    exact=resume_publication(success['state'],identity,carrier,'publication_drain_limit_hold','stale')
    assert exact['outcome']=='exact-retry' and exact['state']==success['state']; counts['eligibility']+=1
    # More than 64 lifetime successes, with persisted bounded continuation recovery.
    state=deepcopy(base)
    for index in range(1,67):
        now=1000+index*31*86400
        unchanged=reconcile_tombstones(state,now,False); assert unchanged==state
        state=reconcile_tombstones(state,now)
        assert not state['live'] and not state['tombstones'] and not state['invocations']
        state['hold_source']=codec['H']('loop9-lifetime-source-'+str(index))
        own,raw=request(('loop9-lifetime-'+str(index)).encode(),source=state['hold_source'])
        result=resume_publication(state,own,raw,'publication_drain_limit_hold','current',now=now,expires_at=now+900)
        assert result['outcome']=='resumed' and result['state']['audits']==index and len(result['state']['invocations'])==1
        retry=resume_publication(result['state'],own,raw,'publication_drain_limit_hold','unavailable',now=now+1)
        assert retry['outcome']=='exact-retry' and retry['response']==result['response']
        imports={key:deepcopy(result['state'][key]) for key in imported_fields}
        image=image_bytes(codec['continuation_projection']({key:value for key,value in result['state'].items() if key not in imported_fields and key!='orphans'}))
        assert len(image)<=128832
        state=codec['continuation_image'](image); state.update(imports); state['orphans']={}
        counts['lifetime']+=1
    now+=31*86400; state=reconcile_tombstones(state,now); state['hold_source']=codec['H']('loop9-lifetime-next')
    own,raw=request(b'loop9-lifetime-next',source=state['hold_source'])
    result=resume_publication(state,own,raw,'publication_drain_limit_hold','current',now=now,expires_at=now+900,crash_after_audit=True)
    prepared=owned_resume_preparation(result,own); assert len(prepared)<=304*1024
    store=PreparationStore(own,raw,prepared,state,now=now,expiry=now+900)
    assert store.turn('successor-write',now=now)=='interrupted'
    restored=restart_preparation(store); assert restored is not None and restored.turn(now=now+1)=='completed'
    assert len(restored.backend.rows)<=32 and sum(map(len,restored.backend.rows.values()))<2*MiB; counts['lifetime']+=1
    # Typed authority is deleted once only after its existing retry deadline.
    prepared=owned_resume_preparation(resume_publication(base,identity,carrier,'publication_drain_limit_hold','current',crash_after_audit=True),identity)
    owned=PreparationStore(identity,carrier,prepared,base); assert owned.turn(now=1000)=='completed' and owned.turn(now=1900)=='completed'
    assert compact_successful_origin(owned)
    keys=identity_record_keys(owned.backend,'t','op',identity); snapshot=owned.backend.snapshot()
    assert not reclaim_resume_identity(owned.backend,'t','op',identity,1900+30*86400-1) and owned.backend.snapshot()==snapshot
    assert reclaim_resume_identity(owned.backend,'t','op',identity,1900+30*86400)
    assert all(key not in owned.backend.rows and key not in owned.backend.receipts and key not in owned.backend.deletions for key in keys)
    snapshot=owned.backend.snapshot(); assert not reclaim_resume_identity(owned.backend,'t','op',identity,1900+30*86400+1) and owned.backend.snapshot()==snapshot
    counts['lifetime']+=1
    # Owners map to original typed invocation rows at their charged addresses.
    current=deepcopy(success['state']); current['hold_source']=codec['H']('loop9-owner-next')
    own,raw=request(b'loop9-owner-next',source=current['hold_source'])
    next_success=resume_publication(current,own,raw,'publication_drain_limit_hold','current',crash_after_audit=True)
    next_prepared=owned_resume_preparation(next_success,own); model=PreparationStore(own,raw,next_prepared,current)
    previous_owner=current['invocation_owners'][0]; previous_invocation=current['invocations'][0]
    key=codec['K']('HX-EV-PUBLICATION-INVOCATION-KEY-1',U('t'),U('op'),previous_invocation)
    assert model.backend.read(key,previous_owner)[0]==codec['invocation_fixture_readbacks'][previous_invocation]; counts['lifetime']+=1
    for damage in ('missing','changed','unavailable'):
        bad=deepcopy(model)
        if damage=='missing': del bad.backend.rows[key]; del bad.backend.receipts[key]
        elif damage=='changed':
            original,generation,receipt=bad.backend.read(key,previous_owner); f=list(decode_record(original,'HX-EV-PUBLICATION-INVOCATION-1',codec['extra_schemas']['D45-invocation'])); f[5]=codec['H']('wrong-owner')
            changed=R('HX-EV-PUBLICATION-INVOCATION-1',9,*(codec['encode_typed'](k,v) for k,v in zip(codec['extra_schemas']['D45-invocation'],f)))
            bad.backend.rows[key]=changed; bad.backend.receipts[key]=(previous_owner,generation,bad.backend.receipt(key,changed,previous_owner,generation))
        else: bad.backend.unavailable.add(key)
        before=bad.backend.snapshot(); assert restart_preparation(bad) is None and bad.turn()=='evidence-hold' and bad.backend.snapshot()==before; counts['lifetime']+=1
    malformed=deepcopy(current); malformed['invocation_owners']=(codec['H']('wrong-owner'),)
    result=resume_publication(malformed,own,raw,'publication_drain_limit_hold','current')
    assert result['outcome']=='resume_evidence_hold' and result['state']==malformed; counts['lifetime']+=1
    # The encoded authority, including account/pool/deployment bounds, wins over stale caches.
    model=Ledger(); model.tenant_ceiling=2*1024*MiB
    amounts=[400*MiB]*3; rows=model.pin_candidates('t',amounts); before=deepcopy(vars(model))
    assert not model.reserve_pin_batch('t',amounts,model.predecessors('t'),b'loop9-stale',candidates=rows) and vars(model)==before; counts['limits']+=1
    for delta in (-1,0,1):
        model=Ledger(); model.tenant_ceiling=2*1024*MiB
        amounts=[400*MiB,400*MiB,224*MiB+delta]; rows=model.pin_candidates('t',amounts); before=deepcopy(vars(model))
        result=model.reserve_pin_batch('t',amounts,model.predecessors('t'),b'loop9-bound',candidates=rows)
        assert result==(delta<=0)
        if not result: assert vars(model)==before
        counts['limits']+=1
    for damage in ('unavailable','receipt','malformed'):
        model=Ledger(); rows=model.pin_candidates('t',[11*MiB])
        if damage=='unavailable': model.pin_evidence_available=False
        elif damage=='receipt': model.pin_capability_receipt=bytes(32)
        else: model.pin_capability+=b'changed'
        before=deepcopy(vars(model)); assert not model.reserve_pin_batch('t',[11*MiB],model.predecessors('t'),b'loop9-bad',candidates=rows) and vars(model)==before; counts['limits']+=1
    for kind in ('tenant-pool','deployment','unidentified'):
        model=Ledger(); model.tenant_ceiling=model.deployment_ceiling=model.unidentified_ceiling=4*1024*MiB; model.reserve_bytes=0
        if kind=='tenant-pool':
            assert model.reserve('tenant','one',[1024*MiB]) and model.reserve('tenant','two',[768*MiB]); target=('tenant','three')
        elif kind=='deployment':
            assert model.reserve('capture-scope','one',[512*MiB]) and model.reserve('tenant','two',[1024*MiB]) and model.reserve('tenant','three',[512*MiB]); target=('tenant','four')
        else:
            assert model.reserve('capture-scope','one',[512*MiB]); target=('capture-scope','two')
        before=deepcopy(vars(model)); assert not model.reserve(*target,[1]) and vars(model)==before; counts['limits']+=1
    # Actual imported A8 required record compacts with unchanged charge and exact shard.
    tenant='t'; execution='op'; shard=sha256(U(tenant)+U(execution)).digest()[0]
    scope=sha256(U(tenant)+U('d')+U('counter')+U('a')+U(execution)).digest()
    required=R('HX-EV-COMMAND-SCOPE-1',10,U(tenant),U(execution),U('d'),U('counter'),U('a'),scope,codec['H']('input'),codec['H']('admission'),U('required'),U('corr'))
    t=codec['t']; tombstone=R('HX-EV-COMMAND-SCOPE-TOMBSTONE-2',8,U(tenant),U(execution),scope,codec['H']('input'),sha256(required).digest(),Q(t),Q(t+10000000),N(shard))
    for raw_record in (codec['vectors']['D12-legacy-claim'],required,tombstone):
        bad=ScopeShardModel(tenant,(shard+1)%256,1024*MiB); before=deepcopy(vars(bad.backend))
        assert not bad.change(raw_record) and vars(bad.backend)==before; counts['shard']+=1
    model=ScopeShardModel(tenant,shard,1024*MiB); before=deepcopy(vars(model.backend))
    assert not model.change(tombstone) and vars(model.backend)==before; counts['compaction']+=1
    assert model.change(required); before=deepcopy(vars(model.backend))
    assert model.change(required) and vars(model.backend)==before; counts['shard']+=1
    for predecessor,proof in ((None,None),(required+b'changed',None),(required,None),(required,bytes(32))):
        assert not model.change(tombstone,now=t,predecessor=predecessor,obligations=proof) and vars(model.backend)==before; counts['compaction']+=1
    proof=model.compaction_proof(required,t)
    assert model.change(tombstone,now=t,predecessor=required,obligations=proof)
    usage=decode_record(model.backend.read(model.key,bytes(32))[0],'HX-EV-SCOPE-SHARD-USAGE-1',codec['schemas']['D12-usage'])
    assert usage[2:5]==(0,1,6144) and model.status(execution)==(410,'https://hexalith.io/problems/command-status-expired'); counts['compaction']+=1
    model.backend=deepcopy(model.backend); before=deepcopy(vars(model.backend))
    assert model.change(tombstone,now=t,predecessor=required,obligations=proof) and vars(model.backend)==before; counts['compaction']+=1
    expiry=t+10000000; absence=sha256(b'fixture-scope-obligations-absent:'+tombstone+Q(expiry)).digest()
    assert not model.change(tombstone,True,expiry-1,absence) and vars(model.backend)==before; counts['compaction']+=1
    assert model.change(tombstone,True,expiry,absence); refunded=deepcopy(vars(model.backend))
    assert not model.change(tombstone,True,expiry,absence) and vars(model.backend)==refunded; counts['compaction']+=1
    for raw_record in (codec['vectors']['D12-legacy-claim'],required):
        erased=ScopeShardModel(tenant,shard,1024*MiB); assert erased.change(raw_record)
        authority=sha256(b'fixture-scope-tenant-erasure:'+U(tenant)+N(shard)+image_bytes(erased.backend.records)).digest()
        assert erased.erase(authority) and not erased.backend.records and not erased.backend.native; counts['shard']+=1
    # Legacy migration proves expiry and original obligation closure on the derived shard.
    legacy=codec['vectors']['D12-legacy-claim']; lf=decode_record(legacy,'HX-EV-COMMAND-SCOPE-LEGACY-2',codec['schemas']['D12-legacy-claim'])
    migrated=R('HX-EV-COMMAND-SCOPE-1',10,U(tenant),U(execution),U(lf[2]),U('counter'),U(lf[3]),scope,lf[5],codec['H']('migration-admission'),U('required'),U('corr'))
    migration=ScopeShardModel(tenant,shard,1024*MiB); assert migration.change(legacy)
    before=deepcopy(vars(migration.backend)); end=lf[7]
    assert not migration.change(migrated,now=end-1,predecessor=legacy,obligations=migration.compaction_proof(legacy,end-1)) and vars(migration.backend)==before; counts['compaction']+=1
    assert not migration.change(migrated,now=end,predecessor=legacy,obligations=None) and vars(migration.backend)==before; counts['compaction']+=1
    wrong=deepcopy(migration); wrong.shard=(shard+1)%256; before_wrong=deepcopy(vars(wrong.backend))
    assert not wrong.change(migrated,now=end,predecessor=legacy,obligations=wrong.compaction_proof(legacy,end)) and vars(wrong.backend)==before_wrong; counts['shard']+=1
    assert migration.change(migrated,now=end,predecessor=legacy,obligations=migration.compaction_proof(legacy,end))
    usage=decode_record(migration.backend.read(migration.key,bytes(32))[0],'HX-EV-SCOPE-SHARD-USAGE-1',codec['schemas']['D12-usage'])
    assert usage[2:5]==(1,0,6144); counts['compaction']+=1
    original=ScopeShardModel(tenant,shard,1024*MiB); assert original.change(required)
    wrong=deepcopy(original); wrong.shard=(shard+1)%256; before_wrong=deepcopy(vars(wrong.backend))
    assert not wrong.change(tombstone,now=t,predecessor=required,obligations=wrong.compaction_proof(required,t)) and vars(wrong.backend)==before_wrong; counts['shard']+=1
    legacy_owner=ScopeShardModel(tenant,shard,1024*MiB); assert legacy_owner.change(legacy)
    wrong=deepcopy(legacy_owner); wrong.shard=(shard+1)%256; before_wrong=deepcopy(vars(wrong.backend))
    absence=sha256(b'fixture-scope-obligations-absent:'+legacy+Q(end)).digest()
    assert not wrong.change(legacy,True,end,absence) and vars(wrong.backend)==before_wrong; counts['shard']+=1
    assert legacy_owner.change(legacy,True,end,absence); counts['compaction']+=1
    # Authenticated native generation refusal cannot mutate either coupled head.
    owned=PreparationStore(identity,carrier,prepared,base); assert owned.turn()=='completed'
    for target in ('state','progress'):
        for generation in (U64_MAX-1,U64_MAX):
            model=deepcopy(owned); key=model.artifact_key('state') if target=='state' else model.head_key
            raw,oldgen,receipt=model.backend.read(key,identity)
            if target=='progress':
                fields=list(decode_record(raw,'HX-EV-RESUME-PREPARATION-HEAD-1',model.head_schema)); fields[6]=generation
                raw=R('HX-EV-RESUME-PREPARATION-HEAD-1',10,*(codec['encode_typed'](k,v) for k,v in zip(model.head_schema,fields)))
            model.backend.rows[key]=raw; model.backend.receipts[key]=(identity,generation,model.backend.receipt(key,raw,identity,generation))
            if target=='state':
                headrow=model.backend.read(model.head_key,identity); h=list(decode_record(headrow[0],'HX-EV-RESUME-PREPARATION-HEAD-1',model.head_schema))
                rows=model.manifest(); entry=list(rows['state']); entry[2]=model.backend.read(key,identity)[2]; rows['state']=tuple(entry)
                h[8]=codec['pack']('>I',len(rows))+b''.join(U(k)+U(address)+digest+receipt+U(disposition) for k,(address,digest,receipt,disposition) in sorted(rows.items()))
                headraw=R('HX-EV-RESUME-PREPARATION-HEAD-1',10,*(codec['encode_typed'](k,v) for k,v in zip(model.head_schema,h)))
                model.backend.rows[model.head_key]=headraw; model.backend.receipts[model.head_key]=(identity,headrow[1],model.backend.receipt(model.head_key,headraw,identity,headrow[1]))
            before=model.backend.snapshot()
            try: model.reconcile_successor(1001)
            except AssertionError: assert generation==U64_MAX and model.backend.snapshot()==before
            else:
                assert generation==U64_MAX-1 and model.backend.read(key,identity)[1]==U64_MAX
                before=model.backend.snapshot()
                try: model.reconcile_successor(1002)
                except AssertionError: assert model.backend.snapshot()==before
                else: raise AssertionError('loop9 generation exhaustion did not refuse')
            restored=restart_preparation(model); assert restored is not None
            snapshot=model.backend.snapshot(); result=restored.turn(now=1002)
            assert result=='evidence-hold' and model.backend.snapshot()==snapshot; counts['atomic']+=1
    for unavailable in ('state','progress'):
        model=deepcopy(owned); key=model.artifact_key('state') if unavailable=='state' else model.head_key
        model.backend.unavailable.add(key); before=model.backend.snapshot()
        assert model.turn(now=1001)=='evidence-hold' and model.backend.snapshot()==before; counts['atomic']+=1
    return counts

def verify_loop9_pending_intent():
    counts={'intent_cases':0,'original_write_cases':0,'later_reconcile_cases':0,'refusals':0,'head_absence_cases':0}
    base=deepcopy(codec['fixture_prior']); base['handle']='h'
    for eligible,hold in (('drain-limit','publication_drain_limit_hold'),('retry-exhausted','publication_retry_exhausted_hold')):
        for later in (1001,2000):
            identity,carrier=request(('loop9-pending-'+eligible+'-'+str(later)).encode())
            prepared=owned_resume_preparation(resume_publication(base,identity,carrier,hold,'current',crash_after_audit=True),identity)
            model=PreparationStore(identity,carrier,prepared,base,eligible=eligible)
            assert model.turn('successor-intent',now=1000)=='interrupted'
            row=model.manifest()['state']; original=model.intended('state'); original_fields=decode_record(original,'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
            assert row[3]=='pending' and row[1]==sha256(original).digest() and original_fields[13]==1000*10000000
            claim=model.artifacts['claim']; audit=model.artifacts['audit']; origin=model.origin
            assert model.predecessor_readback(model.backend.read(row[0],identity)[0]); counts['intent_cases']+=1
            # Only provider receipt ownership changes; intended byte rows remain valid.
            for key in (row[0],model.head_key):
                damaged=deepcopy(model); raw,generation,receipt=damaged.backend.read(key,identity); wrong=codec['H']('wrong-pending-provider-owner')
                damaged.backend.receipts[key]=(wrong,generation,damaged.backend.receipt(key,raw,wrong,generation))
                before=damaged.backend.snapshot()
                assert restart_preparation(damaged) is None and damaged.turn('successor-write',now=later)=='evidence-hold' and damaged.backend.snapshot()==before
                counts['refusals']+=1
            model.raw_artifacts={'state':b'poisoned-pending-cache'}; model.progress_flags={'completed':True}
            restored=restart_preparation(model); assert restored is not None and set(vars(restored))=={'backend','tenant','execution'}
            assert restored.turn('successor-write',now=later)=='interrupted'
            raw,generation,receipt=restored.backend.read(row[0],identity); installed=restored.manifest()['state']
            # Observe the coupled write before finalize/current-time reconciliation.
            assert raw==original and sha256(raw).digest()==row[1] and installed[1:3]==(row[1],receipt) and installed[3]=='present'
            fields=decode_record(raw,'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
            assert fields[13]==1000*10000000 and int.from_bytes(fields[11][:4],'big')==1 and int.from_bytes(fields[12][:4],'big')==0
            assert restored.origin==origin and restored.artifacts['claim']==claim and restored.artifacts['audit']==audit
            counts['original_write_cases']+=1
            restored=restart_preparation(restored); assert restored is not None and restored.turn('finalize',now=later)=='interrupted'
            fields=decode_record(restored.backend.read(row[0],identity)[0],'HX-EV-PUBLICATION-RESUME-STATE-3',codec['schemas']['D45-state'])
            assert fields[13]==later*10000000
            assert tuple(int.from_bytes(fields[index][:4],'big') for index in (11,12))==((1,0) if later<1900 else (0,1))
            completed=restart_preparation(restored); assert completed is not None and completed.turn(now=later)=='completed'
            assert completed.origin==origin and completed.artifacts['claim']==claim and completed.artifacts['audit']==audit
            invocation=decode_record(completed.artifacts['invocation'],'HX-EV-PUBLICATION-INVOCATION-1',codec['extra_schemas']['D45-invocation'])
            assert invocation[8]==1000*10000000 and completed.origin_fields()[9]==1900*10000000
            counts['later_reconcile_cases']+=1
    # Written work with a missing head must not bootstrap a replacement intent.
    identity,carrier=request(b'loop9-pending-missing-head')
    prepared=owned_resume_preparation(resume_publication(base,identity,carrier,'publication_drain_limit_hold','current',crash_after_audit=True),identity)
    model=PreparationStore(identity,carrier,prepared,base); assert model.turn('claim-write')=='interrupted'
    del model.backend.rows[model.head_key]; del model.backend.receipts[model.head_key]; before=model.backend.snapshot()
    assert restart_preparation(model) is None and model.turn()=='evidence-hold' and model.backend.snapshot()==before
    counts['head_absence_cases']+=1
    return counts

def verify_loop9_owned_preparation():
    counts={'positive_cases':0,'refusal_cases':0}; base=deepcopy(codec['fixture_prior']); base['handle']='h'
    for hold in ('publication_drain_limit_hold','publication_retry_exhausted_hold','publication_drain_limit_and_retry_exhausted_hold'):
        identity,carrier=request(('loop9-owned-result-'+hold).encode())
        result=resume_publication(base,identity,carrier,hold,'current',crash_after_audit=True)
        prepared=owned_resume_preparation(result,identity)
        assert decode_record(prepared,'HX-EV-RESUME-PREPARATION-1',codec['extra_schemas']['D45-preparation'])[2]==identity
        counts['positive_cases']+=1
    for damage in ('outcome','state','orphans','identity','owner','preparation','type'):
        bad=deepcopy(result)
        if damage=='outcome': bad['outcome']='resume_evidence_hold'
        elif damage=='state': del bad['state']
        elif damage=='orphans': del bad['state']['orphans']
        elif damage=='identity': del bad['state']['orphans'][identity]
        elif damage=='owner': bad['state']['orphans'][identity]['owner']=codec['H']('other-result-owner')
        elif damage=='preparation': del bad['state']['orphans'][identity]['preparation']
        else: bad['state']['orphans'][identity]['preparation']='untyped-preparation'
        before=deepcopy(bad)
        try: owned_resume_preparation(bad,identity)
        except AssertionError: assert bad==before; counts['refusal_cases']+=1
        else: raise AssertionError('invalid owned preparation consumer accepted')
    return counts

owned_preparation_metrics=verify_loop9_owned_preparation() if not globals().get('fault_probe') or str(globals().get('fault_name','')).startswith('loop9 ') else {}
if owned_preparation_metrics:
    assert owned_preparation_metrics=={'positive_cases':3,'refusal_cases':7}
    print('loop9 owned-preparation consumer probes:',owned_preparation_metrics)

pending_intent_faults={'loop6 later UTC regenerates pending successor','loop6 acknowledged successor skips expiry',
    'persisted completion resurrects expired live retry','provider readback ignores owner and generation',
    'missing progress head bootstraps over written work'}
pending_intent_metrics=verify_loop9_pending_intent() if not globals().get('fault_probe') or str(globals().get('fault_name','')).startswith('loop9 ') or globals().get('fault_name') in pending_intent_faults else {}
if pending_intent_metrics:
    assert pending_intent_metrics=={'intent_cases':4,'original_write_cases':4,'later_reconcile_cases':4,'refusals':8,'head_absence_cases':1}
    print('loop9 isolated pending-intent probes:',pending_intent_metrics)

loop10_repairs_metrics=verify_loop10_repairs() if not globals().get('fault_probe') or str(globals().get('fault_name','')).startswith('loop10 repair ') else {}
if loop10_repairs_metrics:
    assert loop10_repairs_metrics=={'rollback':7,'finalize':2,'renewal':1,'account':1,'capture':2,'predecessor':4}
    print('loop10 core repair probes:',loop10_repairs_metrics)
loop10_lifetime_metrics=verify_loop10_durable_lifetime() if not globals().get('fault_probe') or str(globals().get('fault_name','')).startswith('loop10 durable ') else 0
if loop10_lifetime_metrics:
    assert loop10_lifetime_metrics==67
    print('loop10 shared-backend successes:',loop10_lifetime_metrics)
loop10_maximum_metrics=verify_loop10_maximum() if not globals().get('fault_probe') or str(globals().get('fault_name','')).startswith('loop10 maximum ') else {}
if loop10_maximum_metrics:
    assert loop10_maximum_metrics['members']==59 and loop10_maximum_metrics['attempts']==3776 and loop10_maximum_metrics['observations']==11328
    assert loop10_maximum_metrics['prepared_bytes']<=2*MiB
    print('loop10 legal maximum C2 recovery:',loop10_maximum_metrics)

loop9_lifecycle_metrics=verify_loop9_lifecycles() if not globals().get('fault_probe') or str(globals().get('fault_name','')).startswith('loop9 ') else {}
if loop9_lifecycle_metrics:
    assert loop9_lifecycle_metrics=={'closure':13,'shard':9,'binding':6,'eligibility':4,'lifetime':73,'limits':10,'compaction':13,'atomic':6,'c2_native':6}
    print('loop9 focused lifecycle probes:',loop9_lifecycle_metrics)

loop8_lifecycle_metrics=verify_loop8_lifecycles() if not globals().get('fault_probe') or str(globals().get('fault_name','')).startswith('loop8 ') else {}
if loop8_lifecycle_metrics:
    assert loop8_lifecycle_metrics=={'pin_cases':10,'scope_cases':6,'resume_cases':35,'queue_cases':19,'refusals':45}
    print('loop8 focused lifecycle probes:',loop8_lifecycle_metrics)

def verify_loop11_repairs():
    carrier=b'loop11-held-carrier'
    identity=('tenant','deployment-a','t','pubsub','orders','sub-a')
    policy_store=PolicyStore(); policy=policy_store.install(None,1,bytes(32))
    def fixture(value=carrier,backend=None):
        backend=Ledger() if backend is None else backend
        observed=observe_delivery(None,638712864000000000,backend,identity,value)
        key='held/'+observed['held_key'].hex()
        readback={'backend':'held-delivery-store','key':key,'bytes':value,
            'receipt':object_receipt('held-delivery-store',key,value)}
        held=capture_delivery(observed,value,backend,readback,policy,policy_store)
        assert held['state']=='captured' and held_entry_authority(held,backend) and held_metadata_bound(held,backend)
        return held,backend
    held,backend=fixture()
    recovered=restart_held_delivery(held['held_key'],backend)
    assert recovered is not None and recovered['state']=='captured' and recovered['redrive_count']==0
    assert retained_authority(recovered,backend)
    refusals=0
    for kwargs in ({'held_readback_available':False},{'crash_after':'held-precommit'},
                   {'crash_after':'request-readback'},{'crash_after':'request-delete-readback'}):
        store=deepcopy(backend); before=deepcopy(vars(store))
        assert held_delivery(held,cause_cleared=True,ledger=store,**kwargs)==held and vars(store)==before
        refusals+=1
    for damage in ('count','generation','receipt'):
        store=deepcopy(backend); row=store.held_entries[held_entry_address(held)]
        if damage=='count': row['count']+=1
        elif damage=='generation': row['generation']+=1
        else: row['receipt']=bytes(32)
        before=deepcopy(vars(store))
        assert held_delivery(held,cause_cleared=True,ledger=store)==held and vars(store)==before
        refusals+=1
    redriving=held_delivery(held,cause_cleared=True,ledger=backend,crash_after='request-attempt-commit')
    assert redriving['redrive_count']==1 and redriving['state']=='redriving' and redriving['redrive_bytes'] is None
    backend.__dict__=read_image(image_bytes(vars(backend)))
    recovered=restart_held_delivery(held['held_key'],backend)
    assert recovered is not None and recovered['redrive_count']==1 and recovered['state']=='redriving'
    assert recovered['request']==redriving['request'] and recovered['attempt']==redriving['attempt']
    before=deepcopy(vars(backend))
    assert held_delivery(recovered,cause_cleared=True,ledger=backend)==recovered and vars(backend)==before
    # The held count fence survives loss of both repair rows, even if process
    # state tries to omit the prerequisite.
    bad=deepcopy(recovered); bad['attempt']['receipt']=bytes(32)
    for boundary in ('repair-record-stage','repair-index-stage','repair-held-stage'):
        store=deepcopy(backend); before=deepcopy(vars(store))
        assert reconcile_redrive(bad,ledger=store,crash_after=boundary)==bad and vars(store)==before
    required=reconcile_redrive(bad,ledger=backend)
    assert required.get('repair_required') is not None and repair_backend_authority(required,backend), 'loop11-required-record-and-index-published'
    committed=deepcopy(vars(backend))
    retried_required=reconcile_redrive(bad,ledger=backend)
    assert retried_required['repair_required']['raw']==required['repair_required']['raw'] and vars(backend)==committed
    assert required['repair_required'] and repair_backend_authority(required,backend)
    assert restart_held_delivery(held['held_key'],backend)['repair_required']
    ikey=repair_index_address(required); ekey=repair_entry_address(required); rkey=repair_record_address(required)
    index=backend.hold_indexes[ikey]; index_fields=decode_record(index['raw'],'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index'])
    assert index_fields[:4]==('tenant','t',1,1) and index_fields[5]==0 and index_fields[6]==bytes(32)
    assert repair_index_member(required,sha256(required['repair_entry']).digest()) in repair_index_view(required,index)
    first_index_raw=index['raw']
    other,backend=fixture(b'other-loop11-carrier',backend)
    other_redriving=held_delivery(other,cause_cleared=True,ledger=backend)
    other_bad=deepcopy(other_redriving); other_bad['attempt']['receipt']=bytes(32)
    other_required=reconcile_redrive(other_bad,ledger=backend)
    # Both repairs and the shared tenant index are live on one backend.
    assert repair_backend_authority(required,backend) and repair_backend_authority(other_required,backend)
    index=backend.hold_indexes[ikey]; index_fields=decode_record(index['raw'],'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index'])
    index_rows=repair_index_view(required,index)
    assert (index_fields[:4]==('tenant','t',2,2) and index_fields[5]==0 and
        index_fields[6]==index['predecessor']==sha256(first_index_raw).digest())
    assert (len(index_rows)==2 and repair_index_member(required,sha256(required['repair_entry']).digest()) in index_rows
        and repair_index_member(other_required,sha256(other_required['repair_entry']).digest()) in index_rows
        and retained_authority(other_required,backend))
    for damage in ('record-missing','entry-missing','index-missing','record-owner','entry-receipt','index-receipt',
                   'index-malformed','index-wrong-owner','index-stale'):
        store=deepcopy(backend)
        if damage=='record-missing': del store.repair_records[rkey]
        elif damage=='entry-missing': del store.repair_entries[ekey]
        elif damage=='index-missing':
            rows=[row for row in index_rows if row[1:3]!=repair_index_member(required,sha256(required['repair_entry']).digest())[1:3]]
            store.hold_indexes[ikey]=repair_index_row(required,rows,store.hold_indexes[ikey])
        elif damage=='record-owner': store.repair_records[rkey]['owner']=bytes(32)
        elif damage=='entry-receipt': store.repair_entries[ekey]['receipt']=bytes(32)
        elif damage=='index-receipt': store.hold_indexes[ikey]['receipt']=bytes(32)
        else:
            row=store.hold_indexes[ikey]
            if damage=='index-malformed': row['raw']+=b'changed'
            else:
                fields=list(decode_record(row['raw'],'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index']))
                if damage=='index-wrong-owner': fields[1]='wrong-tenant'
                else: fields[2]+=1; fields[6]=sha256(row['raw']).digest()
                row['raw']=R('HX-EV-HOLD-INDEX-2',8,*(codec['encode_typed'](kind,value)
                    for kind,value in zip(codec['schemas']['D37-index'],fields)))
            row['receipt']=sha256(b'provider-hold-index-readback:'+U(ikey)+
                image_bytes({k:v for k,v in row.items() if k!='receipt'})).digest()
        before=deepcopy(vars(store))
        assert restart_held_delivery(held['held_key'],store) is None
        assert held_delivery(required,cause_cleared=True,ledger=store)==required and vars(store)==before
        refusals+=1
    genuine=backend.redrive_attempts[(held['held_key'],1)]
    receipt=sha256(b'authenticated-attempt-repair:'+genuine['receipt']+held['readback_authority']).digest()
    for kwargs in ({'readback_available':False},{'crash_after':'repair-repaired-stage'}):
        store=deepcopy(backend); before=deepcopy(vars(store))
        assert repair_redrive(required,store,receipt,**kwargs)==required and vars(store)==before
        refusals+=1
    repaired=repair_redrive(required,backend,receipt)
    assert repaired['repair_required'] is None and repair_backend_authority(repaired,backend,False)
    committed=deepcopy(vars(backend))
    retried_repaired=repair_redrive(required,backend,receipt)
    assert retried_repaired['repaired_record']==repaired['repaired_record'] and vars(backend)==committed
    for generation,phase in enumerate(('repaired-readback','record-deleted','record-readback','entry-deleted'),1):
        store=deepcopy(backend)
        cleanup_redrive_repair(repaired,store,crash_after=phase)
        store.__dict__=read_image(image_bytes(vars(store)))
        pending=restart_held_delivery(held['held_key'],store)
        assert pending is not None and pending['repair_indexed']
        cleanup_row=store.repair_cleanup_rows[repair_cleanup_address(pending)]
        assert (pending['repair_cleanup_bytes']==cleanup_row['raw'] and
            repair_cleanup_view(pending,cleanup_row)==pending['repair_cleanup'] and
            int.from_bytes(cleanup_row['support'][:8],'big')==generation and len(image_bytes(cleanup_row))<=1024)
        assert held_delivery(pending,cause_cleared=True,ledger=store)==pending
        for damage in ('typed-bytes','native-support','native-receipt'):
            tampered=deepcopy(store); row=tampered.repair_cleanup_rows[repair_cleanup_address(pending)]
            if damage=='typed-bytes': row['raw']+=b'changed'
            elif damage=='native-support': row['support']=row['support'][:8]+bytes([1])*32+row['support'][40:]
            else: row['receipt']=bytes(32)
            assert restart_held_delivery(held['held_key'],tampered) is None
            refusal_before=deepcopy(vars(tampered))
            assert cleanup_redrive_repair(pending,tampered)==pending and vars(tampered)==refusal_before
        if phase in {'record-deleted','record-readback','entry-deleted'}:
            tampered=deepcopy(store); tampered.repair_deletions[('record',rkey)]=bytes(32)
            assert restart_held_delivery(held['held_key'],tampered) is None
            refusal_before=deepcopy(vars(tampered))
            assert cleanup_redrive_repair(pending,tampered)==pending and vars(tampered)==refusal_before
        if phase=='entry-deleted':
            tampered=deepcopy(store); tampered.repair_deletions[('entry',ekey)]=bytes(32)
            assert restart_held_delivery(held['held_key'],tampered) is None
            refusal_before=deepcopy(vars(tampered))
            assert cleanup_redrive_repair(pending,tampered)==pending and vars(tampered)==refusal_before
        prior_index_raw=store.hold_indexes[ikey]['raw']
        complete=cleanup_redrive_repair(pending,store)
        remaining=repair_index_view(complete,store.hold_indexes[ikey])
        removed_fields=decode_record(store.hold_indexes[ikey]['raw'],'HX-EV-HOLD-INDEX-2',codec['schemas']['D37-index'])
        assert removed_fields[2:4]==(3,1) and removed_fields[6]==sha256(prior_index_raw).digest()
        assert (not complete['repair_indexed'] and remaining is not None and
            all(row[1:3]!=('RedriveEvidenceRepairHold',held['held_key'].hex()+':1') for row in remaining) and
            repair_index_member(other_required,sha256(other_required['repair_entry']).digest()) in remaining)
        assert rkey not in store.repair_records and ekey not in store.repair_entries
        assert ('record',rkey) not in store.repair_deletions and ('entry',ekey) not in store.repair_deletions
        assert store.held_entries[held_entry_address(complete)]['repair_open'] is False
        next_send=held_delivery(complete,cause_cleared=True,ledger=store)
        assert next_send['redrive_count']==2 and next_send['state']=='redriving'
        restarted_next=restart_held_delivery(held['held_key'],store)
        assert restarted_next is not None and restarted_next['redrive_count']==2 and restarted_next['state']=='redriving'
        assert held_delivery(next_send,cause_cleared=True,ledger=store)==next_send
    return {'refusals':refusals,'cleanup_boundaries':4,'count':2}

loop11_metrics=verify_loop11_repairs() if not globals().get('fault_probe') or str(globals().get('fault_name','')).startswith('loop11 ') else {}
if loop11_metrics:
    assert loop11_metrics=={'refusals':18,'cleanup_boundaries':4,'count':2}
    print('loop11 addressed held/repair probes:',loop11_metrics)

loop6_metrics = verify_loop6_transitions() if not globals().get('fault_probe') or str(globals().get('fault_name','')).startswith('loop6 ') else {}
if loop6_metrics:
    assert tuple(loop6_metrics[key] for key in ('utc','partial_capture','redrive_refusals','request_refusals','request_restarts','erasure_refusals','cleanup')) == (64,4,32,23,6,6,4)
matrix_cases = [
    verify_eligible_resume_matrix(),
    verify_legacy_resume_matrix(),
    verify_capacity_wait_matrix(),
    verify_held_delivery_and_long_stream_matrix(),
]
assert matrix_cases == [
    'eligible-resume', 'legacy-resume', 'capacity-wait',
    'held-delivery-or-long-stream']

supplementary_schemas = {
    'D45-origin':['U','U','B32','B32','B','B','B','N','Q','Q','B'],
    'D45-preparation':['U','U','B32','B32','B32','B','B','Q','Q','N','B','N'],
    'D45-preparation-head':['U','U','B32','B32',('O','B32'),'U','N','B32','B','Q'],
    'D36-capture-preparation':['B32','B32','B32','B32','U','U','B32','N','Q'],
    'D36-repair':['B32','N','B32','B32','U','N','B32','U',('O','B32'),'Q'],
    'D36-capture-origin':['B32','B','B32','Q','N','B32'],
    'D36-current-request':['U','U',('O','U'),'B32','N','U','Q'],
    'D36-attempt':['B32','N','B32','U','B32','Q','B32'],
    'D36-cleanup':['B32','N','B32','B32','U',('O','B32'),('O','B32')],
    'D45-invocation':['U','U','B32','N','N','B32','B32','B32','Q'],
}
supplementary_malformed = 0
assert set(supplementary_records) == set(supplementary_schemas) == set(codec['extra_schemas'])
assert all(raw == codec['extra_vectors'][label] for label,raw in supplementary_records.items())
for label,raw in supplementary_records.items():
    domain = raw.split(b'\0',1)[0].decode(); schema = supplementary_schemas[label]
    fields = decode_record(raw,domain,schema)
    segments = [bytes([i+1])+codec['encode_typed'](kind,value) for i,(kind,value) in enumerate(zip(schema,fields))]
    prefix = len(domain.encode())+4
    framed = next(i for i,k in enumerate(schema) if k in ('U','B'))
    changed = list(segments); changed[framed] = changed[framed][:1]+b'\xff'*4+changed[framed][5:]
    for mutant in (raw[:-1],raw[:prefix]+b'\x02'+raw[prefix+1:],
                   raw[:prefix]+segments[1]+segments[0]+b''.join(segments[2:]),
                   raw[:prefix]+b''.join(changed),raw+b'\x00'):
        try: decode_record(mutant,domain,schema)
        except (AssertionError,UnicodeError): supplementary_malformed += 1
        else: raise AssertionError((label,'malformed supplementary record accepted'))
assert supplementary_malformed == 50

# Exact construction bounds and acyclic/retry invariants.
assert 2455 + 943*1109 <= MiB and 2455 + 944*1109 > MiB
assert 1291 + 59*1080 <= 64*1024 and 1291 + 60*1080 > 64*1024
assert 128 + 61*1068 <= 64*1024 and 128 + 62*1068 > 64*1024
counter_predecessors = Ledger().predecessors('t')
assert set(counter_predecessors) == {'tenant','tenant-pool','deployment'}

closure_bytes = b'closure-without-successor'
successor_accumulator = sha256(b'predecessor' + closure_bytes + b'broker-auth').digest()
assert successor_accumulator not in closure_bytes
prior_state_hash = sha256(b'prior-state').digest()
audit_bytes = b'audit' + prior_state_hash
audit_hash = sha256(audit_bytes).digest()
successor_state_bytes = b'state' + successor_accumulator + audit_hash
assert sha256(successor_state_bytes).digest() not in audit_bytes and audit_hash in successor_state_bytes
stable_request_identity = sha256(b'caller-stable').digest()
window_bytes = b'window' + prior_state_hash + stable_request_identity
window_hash = sha256(window_bytes).digest()
successor_with_window = b'state' + window_hash + audit_hash
assert sha256(successor_with_window).digest() not in window_bytes and window_hash in successor_with_window

policy1 = policy_revision(None,1,bytes(32))
assert policy1['raw'] == codec['vectors']['D36-policy']
policy2 = policy_revision(policy1,2,policy1['hash'])
assert policy2 and policy_revision(None,2,bytes(32)) is None
assert policy_revision(policy1,3,policy1['hash']) is None
assert policy_revision(policy1,2,bytes(32)) is None
assert policy_revision(policy2,2,policy2['hash']) is None
assert policy_selected(policy2,policy2['hash']) and not policy_selected(policy1,policy2['hash'])
other_policy1 = policy_revision(None,1,bytes(32),config=sha256(b'other-config').digest())
other_policy2 = policy_revision(other_policy1,2,other_policy1['hash'])
assert policy1['hash'] != bytes(32) != other_policy1['hash']
assert policy2['hash'] != other_policy2['hash']
expected_policy2 = R('HX-EV-SUBSCRIPTION-DELIVERY-POLICY-3',13,U('deployment-a'),U('pubsub'),U('orders'),
    U('sub-a'),N(2),policy1['hash'],U('successor'),U('dead-letter-capture'),O(U('orders-dlq')),
    N(8),codec['H']('subscription-config'),U('dapr-configuration'),Q(codec['t']))
assert policy2['hash'] == sha256(expected_policy2).digest() and policy2['raw'] == expected_policy2
durable_policies = PolicyStore()
installed1 = durable_policies.install(None,1,bytes(32))
installed2 = durable_policies.install(installed1,2,installed1['hash'])
assert installed2 == policy2 and durable_policies.selected(installed2)
policy_snapshot = deepcopy(vars(durable_policies))
assert durable_policies.install(installed1,2,installed1['hash']) == installed2
assert vars(durable_policies) == policy_snapshot
assert durable_policies.install(installed1,2,installed1['hash'],sha256(b'competing-policy').digest()) is None
assert vars(durable_policies) == policy_snapshot and len(durable_policies.revisions) == 2
assert durable_policies.install(installed1,3,installed1['hash']) is None
assert durable_policies.install(installed2,3,installed1['hash']) is None
assert not durable_policies.selected(installed1) and durable_policies.selected(installed2)

# Mutation killers: each expression describes an acceptance-breaking mutant and must be rejected.
mutants_rejected = [
    status('failed', failure_classes=('unknown',), at_max=True)[1] == 'outcome_evidence_conflict',
    not ledger.reserve('capture-scope', 'scope-c', [513*MiB]),        # unidentified ceiling retained
    not ledger.reserve('unknown', 'scope-c', [1]),                    # closed account kind
    not ledger.refund('unknown', 't1', 1),                            # closed refund kind
    capacity_admit(ledger, 't1', [-1])[0] == 'publication_pin_capacity_hold', # stable arithmetic hold
    set(counter_predecessors) == {'tenant','tenant-pool','deployment'},
    queues.where['a'] == 'deployment',                                # cross-counter return exists
    queues.where['b'] == 'deployment',                                # deployment head refused by own tenant does not block t2
    not queues.add(4, 't3', 'c'),                                     # duplicate stable subject rejected
    not ticket_exhausted.add(U64_MAX, 't', 'never-wraps'),
    decode_queue(valid_rows, 1, 0, reserved_count=3, ceiling=3) == 'pin_capacity_queue_corruption_hold',
    membership_exit('manual', True, True) != 'ContinueSamePin',
    membership_exit('membership-revision', True, True) == 'ContinueSamePin',
    status('pending', drain_active=False)[1] != 'publication_drain_limit_hold',
    status('failed', failure_classes=('class-02',), at_max=True)[1] == 'terminal_evidence_hold',
    status('failed', failure_classes=('class-03',))[1] == 'terminal_evidence_hold',
    status('published', classification='rejection')[0] == 'Rejected',
    status('published', classification='unknown')[1] == 'outcome_evidence_conflict',
    render_recovery('x-response', None, receipt, receipt, 'x', 7) == 'response_preparation_hold',
    render_recovery('x-response', 'x-outcome', receipt, ('receipt','x',6), 'x', 7) == 'response_preparation_hold',
    len(queues.rows['deployment']) + len(queues.reservations['deployment']) <= queues.ceiling,
    parked_only.deployment_turn(lambda _: True, lambda _: True) == 'noop',
    full_replay_exit(75000, 1, 1, False)['outcome'] == 'LegacyArrayLimit',
    full_replay_exit(100001, 1, 1, False)['applied_events'] == 0,
    not post_activation_growth(100001, 1, 1)['dispatch'],
    1291 + 60*1080 > 64*1024,
    sha256(successor_with_window).digest() not in window_bytes,
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
repair_section = text.split('### Review-loop-3 ' + 'repair register',1)[1].split('## Protected-path',1)[0]
repair_ids = re.findall(r'\b(?:BHR3|ECR3|VGR3)-\d{2}\b',repair_section)
expected_repairs = ({f'BHR3-{i:02}' for i in range(1,13)}
                    | {f'ECR3-{i:02}' for i in range(1,7)}
                    | {f'VGR3-{i:02}' for i in range(1,4)})
assert len(repair_ids) == len(set(repair_ids)) == 21 and set(repair_ids) == expected_repairs
loop4_section = text.split('### Review-loop-4 ' + 'repair register',1)[1].split('## Protected-path',1)[0]
loop4_ids = re.findall(r'\b(?:BHR4|ECR4|VGR4)-\d{2}\b',loop4_section)
expected_loop4 = ({f'BHR4-{i:02}' for i in range(1,13)}
                 | {f'ECR4-{i:02}' for i in range(1,7)} | {f'VGR4-{i:02}' for i in range(1,3)})
assert len(loop4_ids) == len(set(loop4_ids)) == 20 and set(loop4_ids) == expected_loop4
loop5_section = text.split('### Review-loop-5 ' + 'repair register',1)[1].split('## Protected-path',1)[0]
loop5_ids = re.findall(r'\b(?:(?:BHR5|ECR5)-\d{2}|VGR5-(?:01|O1))\b',loop5_section)
expected_loop5 = ({f'BHR5-{i:02}' for i in range(1,11)} | {f'ECR5-{i:02}' for i in range(1,10)} | {'VGR5-01','VGR5-O1'})
assert len(loop5_ids) == len(set(loop5_ids)) == 21 and set(loop5_ids) == expected_loop5
loop6_section = text.split('### Review-loop-6 ' + 'repair register',1)[1].split('## Protected-path',1)[0]
loop6_ids = re.findall(r'\b(?:(?:BHR6|ECR6)-\d{2}|VGR6-(?:01|O1|O2))\b',loop6_section)
expected_loop6 = {f'BHR6-{i:02}' for i in range(1,11)} | {f'ECR6-{i:02}' for i in range(1,7)} | {'VGR6-01','VGR6-O1','VGR6-O2'}
assert len(loop6_ids) == len(set(loop6_ids)) == 19 and set(loop6_ids) == expected_loop6

pass8_section=text.split('### Review-loop-8 '+'repair register',1)[1].split('## Protected-path',1)[0]
pass8_ids=re.findall(r'^\| (P(?:D)?\d+) \|',pass8_section,re.M)
expected_pass8={f'P{i}' for i in range(1,22)} | {f'PD{i}' for i in range(1,13)}
assert len(pass8_ids) == len(set(pass8_ids)) == 33 and set(pass8_ids) == expected_pass8

loop7_section=text.split('### Review-loop-7 '+'repair register',1)[1].split('### Review-loop-8 '+'repair register',1)[0]
loop7_ids=re.findall(r'^\| ((?:BHR9|ECR9)-\d{2}) \|',loop7_section,re.M)
assert len(loop7_ids)==len(set(loop7_ids))==7 and set(loop7_ids)=={'BHR9-01','BHR9-02','BHR9-03','BHR9-04','ECR9-12','ECR9-13','ECR9-14'}

loop8_section=text.split('### Review-loop-9 '+'repair register',1)[1].split('## Protected-path',1)[0]
loop8_ids=re.findall(r'^\| ((?:BHR10|ECR10)-\d{2}) \|',loop8_section,re.M)
assert len(loop8_ids)==len(set(loop8_ids))==12 and set(loop8_ids)=={f'BHR10-{i:02}' for i in range(1,11)}|{'ECR10-01','ECR10-02'}

loop9_section=text.split('### Review-pass-11 '+'implementation register',1)[1].split('## Protected-path',1)[0]
loop9_ids=re.findall(r'^\| ((?:BHR11|ECR11)-\d{2}) \|',loop9_section,re.M)
assert len(loop9_ids)==len(set(loop9_ids))==11 and set(loop9_ids)=={f'BHR11-{i:02}' for i in range(1,11)}|{'ECR11-01'}

loop10_section=text.split('### Review-pass-12 '+'iteration-10 implementation register',1)[1].split('## Protected-path',1)[0]
loop10_ids=re.findall(r'^\| (BHR12-\d{2}|VGR12-\d{2}) \|',loop10_section,re.M)
assert len(loop10_ids)==len(set(loop10_ids))==9 and set(loop10_ids)=={f'BHR12-{i:02}' for i in range(1,9)}|{'VGR12-01'}

# Directed source mutations rerun the owning executable assertions in isolation.
# Splitting before this marker prevents recursively running the mutation harness.
lifecycle_source = re.findall(r'```bash\npython3 - <<\'PY\'\n(.*?)\nPY\n```',candidate_text,re.S)[1]
probe_source = lifecycle_source.split('# Directed source mutations rerun',1)[0]
faults = [
 ('allocator rejects its allocated ticket','lifecycle',[("ticket!=a[5]","ticket<=a[5]")]),
 ('drain-only replaces claim','lifecycle',[("claim = prior['window_claim_bytes']","claim = b'changed-drain-only-window'")]),
 ('live retry loses response','lifecycle',[("'response':row['response'],'command_executions':0}","'response':b'label-only','command_executions':0}")]),
 ('orphan leaves prior state','lifecycle',[("recovered = deepcopy(successor_intent)","recovered = deepcopy(prior)")]),
 ('live expiry never converts','lifecycle',[("for identity,row in list(successor['live'].items()):\n        if now >= row['expires_at']:","for identity,row in list(successor['live'].items()):\n        if False:")]),
 ('chunk uses counted-root domain','lifecycle',[("root = sha256(b'HX-EV-LEGACY-RESUME-CHUNK-ROWS-1\\0\\x01' + B(raw_rows)).digest()","root = sha256(b'HX-EV-LEGACY-RESUME-EVENTS-2\\0\\x01' + B(raw_rows)).digest()")]),
 ('recovery ordinal exceeds u64','lifecycle',[("or not 1 <= ordinal <= U64_MAX","or ordinal < 1")]),
 ('recovery generation exceeds u64','lifecycle',[("or not 1 <= expected_generation <= U64_MAX","or expected_generation < 1"),("next_generation = checked_add(record['generation'],1)","next_generation = record['generation']+1")]),
 ('competing recovery owner wins','lifecycle',[("or owner != record['owner'] or next_generation is None","or next_generation is None")]),
 ('unverified repair clears failure','lifecycle',[("repair == repair_receipt(record,bundle)","True")]),
 ('redrive resets durable count','lifecycle',[("state = deepcopy(previous)\n    if state['state'] == 'closed': return state","state = deepcopy(previous)\n    state['redrive_count'] = 0\n    if state['state'] == 'closed': return state")]),
 ('capture skips quota','lifecycle',[("if existing_charge is None and not ledger.reserve(previous['account_kind'],previous['account'],[len(retained),32*1024]):","if False:")]),
 ('capture skips object readback','lifecycle',[("if readback != expected:","if False:")]),
 ('policy skips a revision','lifecycle',[("revision != expected_revision","False")]),
 ('pin reservation ignores predecessors','lifecycle',[("predecessors != self.predecessors(account)","False")]),
 ('request identity includes reason','lifecycle',[("+U(tenant)+U(handle)+U(identity_key)).digest()","+U(tenant)+U(handle)+U(identity_key)+U(reason)).digest()")]),
 ('held key omits tenant','lifecycle',[("O(None if tenant is None else U(tenant))","O(None)")]),
 ('policy hash omits predecessor','lifecycle',[("N(revision),predecessor,U('initial'","N(revision),bytes(32),U('initial'")]),
 ('signed decoder returns bytes','codec',[("elif kind == 'I': return int.from_bytes(take(4), 'big', signed=True)","elif kind == 'I': return int.from_bytes(take(4), 'big', signed=False)")]),
 ('semantic legacy count ceiling removed','codec',[("assert 1 <= values[10] <= 1000 and values[9] >= values[8]","assert values[9] >= values[8]")]),
 ('rollback discards audited stage','lifecycle',[("if audit == 'present' and successor == 'absent':","if False:")]),
 ('pin reservation ignores owner','lifecycle',[("row['identity'] == identity","True")]),
 ('queue conflict cancels another preparation','lifecycle',[("# An untrusted conflicting caller cannot cancel the authenticated owner.\n                return False","# Broken rollback\n                self.backend.records[self.address('authority',scope)]+=b'cancelled-by-conflict'\n                return False")]),
 ('resume ignores decoded source','lifecycle',[("or source != state['hold_source']","or False")]),
 ('orphan loses reconciled indexes','lifecycle',[("recovered['tombstones'] = deepcopy(state['tombstones'])","recovered['tombstones'] = deepcopy(prior['tombstones'])")]),
 ('orphan bypasses authentication','lifecycle',[("if row['receipt'] != state_hash(immutable) or row['owner'] != request_identity:","if False:")]),
 ('expired tombstone lookup removed','lifecycle',[("if request_identity in state['tombstones']:","if False:")]),
 ('capture bypasses metadata and inventory','lifecycle',[("if (not authenticated(metadata,previous['metadata_receipt'])","if False and (not authenticated(metadata,previous['metadata_receipt'])")]),
 ('capture reuses one object locator','lifecycle',[("backend, key = 'held-delivery-store','held/'+subject.hex()","backend, key = 'held-delivery-store','held/constant'")]),
 ('crashed redrive remains stranded','lifecycle',[("return held_delivery(previous,redrive_failed=True,ledger=ledger)","return deepcopy(previous)")]),
 ('policy revision store permits replacement','lifecycle',[("if existing is not None and existing != candidate: return None","if False: return None"),("if self.head != expected_head: return None","if False: return None")]),
 ('reserve counter generation wraps','lifecycle',[("if not fits or any(self.generations.get(key,0) == U64_MAX for key in changed):","if not fits:")]),
 ('refund counter generation wraps','lifecycle',[("or amount > self.deployment\n                or any(self.generations.get(key,0) == U64_MAX for key in changed)","or amount > self.deployment")]),
 ('large family fields capped at one MiB','codec',[("assert length <= maximum, ('field-length',domain,expected_tag-1,maximum)","assert length <= 1024*1024")]),
 ('ordinary identifiers widened to 4096','codec',[("record_caps.get(domain,4096) if base_kind == 'B' else 1024)","record_caps.get(domain,4096) if base_kind == 'B' else 4096)")]),
 ('capability allows one shard','codec',[("assert values[11] == 256 and 1 <= values[13] <= 50000","assert 1 <= values[11] <= 256 and 1 <= values[13] <= 50000")]),
 ('directory shard 256 allowed','codec',[("assert 0 <= values[0] <= 255 and values[2]+values[6] <= 50000","assert 0 <= values[0] <= 256 and values[2]+values[6] <= 50000")]),
 ('charge genesis predecessor ignored','codec',[("genesis(values[9],values[11]); assert values[8] > 0","assert values[9] > 0 and values[8] > 0")]),
 ('complete attempt root ignored','codec',[("assert complete[6] == values[7]","assert True")]),
 ('successful live ordinals duplicated','codec',[("assert len({row[2] for row in live}) == len(live)","assert True")]),
 ('per-kind charge ceilings ignored','codec',[("assert values[5] <= ceilings[values[4]] and values[6] <= 1114112","assert values[6] <= 1114112")]),
 ('recorded overhead ceiling ignored','codec',[("assert values[5] <= ceilings[values[4]] and values[6] <= 1114112","assert values[5] <= ceilings[values[4]]")]),
 ('policy allows sixty-five attempts','codec',[("assert 1 <= values[9] <= 64","assert 1 <= values[9] <= 65")]),
 ('orphan ignores current expiry','lifecycle',[("recovered = reconcile_tombstones(recovered,now)","recovered = reconcile_tombstones(recovered,1000)")]),
 ('drain invocation omits authorization epoch','lifecycle',[("+claim+N(ordinal)+N(limit)+request_identity+root","+claim+root")]),
 ('response omits decoded handle','lifecycle',[("{'resumeHandle':handle,'resumeOrdinal':ordinal,'window':window","{'resumeOrdinal':ordinal,'window':window")]),
 ('response substitutes fixture handle','lifecycle',[("{'resumeHandle':handle,'resumeOrdinal':ordinal","{'resumeHandle':'h','resumeOrdinal':ordinal")]),
 ('partial capture refuses its existing charge','lifecycle',[("if existing_object is not None or existing_charge is not None:\n        if (existing_charge != object_owner","if existing_object is not None or existing_charge is not None: return state\n    if existing_object is not None or existing_charge is not None:\n        if (existing_charge != object_owner")]),
 ('corrupt redrive forgets repair prerequisite','lifecycle',[("if state.get('repair_required') or not retained_authority(previous,ledger)","if not retained_authority(previous,ledger)")]),
 ('redrive ignores retained-byte authority','lifecycle',[("if state.get('repair_required') or not retained_authority(previous,ledger)","if state.get('repair_required')")]),
 ('forged repair receipt clears evidence hold','lifecycle',[("if receipt != expected: return state","if False: return state")]),
 ('unavailable readback permits rollback','lifecycle',[("if successor is None or audit is None:\n            return False","if successor is None or audit is None:\n            successor = successor or 'absent'; audit = audit or 'absent'")]),
 ('ordinary capture accepts oversize','lifecycle',[("if len(retained) > 193*MiB: return state","if False: return state")]),
 ('preparation refunds before deletion readback','lifecycle',[("if not deletion_available: return 'cleanup-hold'","if not deletion_available:\n                    view = self.swap\n                    view.recover(self.owner,successor_receipt,audit_receipt)\n                    self.save_charges(self.owner,view.old,0,'released',view.used)\n                    return 'cleanup-hold'")]),
 ('preparation re-signs original claim','lifecycle',[("if kind == 'claim': return self.origin_fields()[5]","if kind == 'claim': return self.origin_fields()[5]+b'changed-time'")]),
 ('reconstruction accepts noncanonical images','codec',[("assert canonical_image_bytes(result) == raw","assert True")]),
 ('invocation ignores authorization identity','codec',[("assert values[7] == sha256(b'HX-EV-PUBLICATION-INVOCATION-1\\0\\x01'+values[2]+N(values[3])+N(values[4])+values[5]+values[6]).digest()","assert True")]),
 ('preparation hold accepts wrong owner','codec',[("if values[2] in {'PublicationResumePreparationHold','ResumeAttemptCollectionHold'}: assert values[11] == 'coordinator'","if False: assert values[11] == 'coordinator'")]),
 ('repair hold accepts wrong owner','codec',[("if values[2] == 'RedriveEvidenceRepairHold': assert values[11] == 'operations'","if False: assert values[11] == 'operations'")]),
 ('restart restores process artifact cache','lifecycle',[("restored.backend = store.backend","restored.backend = store.backend\n    restored.raw_artifacts = getattr(store,'raw_artifacts',{})")]),
 ('preparation installs unrelated audit fixture','lifecycle',[("if kind == 'audit':\n            raw = R(","if kind == 'audit':\n            return codec['audit']\n            raw = R(")]),
 ('preparation installs unrelated state fixture','lifecycle',[("return publication_state_bytes(successor,completion_time)","return codec['state']")]),
 ('provider readback ignores owner and generation','lifecycle',[("assert recorded_owner == owner and receipt == self.receipt(key,raw,owner,generation)","assert True")]),
 ('persisted completion resurrects expired live retry','lifecycle',[("successor = reconcile_tombstones(successor,completion_time)","successor = successor")]),
 ('missing progress head bootstraps over written work','lifecycle',[("assert all((lambda row: row is None or kind == 'state' and self.predecessor_readback(row[0]))(","assert True or all((lambda row: row is None or kind == 'state' and self.predecessor_readback(row[0]))(")]),
 ('claim confuses A8 head with D9 predecessor','lifecycle',[("sha256(a8_head).digest(),N(prior['ordinal']+1),previous_audit","sha256(predecessor).digest(),N(prior['ordinal']+1),previous_audit")]),
 ('claim loses predecessor successful audit','lifecycle',[("N(prior['ordinal']+1),previous_audit,U(prior['handle'])","N(prior['ordinal']+1),bytes(32),U(prior['handle'])")]),
 ('loop6 later UTC regenerates pending successor','lifecycle',[("now if kind == 'state' and kind not in rows else None","now if kind == 'state' else None")]),
 ('loop6 acknowledged successor skips expiry','lifecycle',[("staged_final.reconcile_successor(now)","pass")]),
 ('loop6 later observations replace original capture','lifecycle',[("original = original_observation(origins[key],previous) if key in origins else previous","original = previous")]),
 ('loop6 capture omits repair inventory capacity','lifecycle',[("or repair_key in ledger.charges or repair_inventory_key in ledger.inventory\n                or len(ledger.inventory) >= ledger.inventory_ceiling","or repair_key in ledger.charges or repair_inventory_key in ledger.inventory\n                or False")]),
 ('loop6 redrive omits metadata charge','lifecycle',[("not charge_authority(previous['metadata_key'],32*1024,previous['metadata_receipt'])","False")]),
 ('loop6 redrive omits original interest','lifecycle',[("not interest_authority(previous['inventory_key'],previous['inventory_receipt'])","False")]),
 ('loop6 redrive omits repair charge','lifecycle',[("not charge_authority(previous['repair_charge_key'],32*1024)","False")]),
 ('loop6 redrive omits repair interest','lifecycle',[("not interest_authority(previous['repair_inventory_key'],previous['repair_inventory_receipt'],'redrive-repair')","False")]),
 ('loop6 preparation comparison removed','lifecycle',[("return getattr(ledger,'capture_preparations',{}).get(key) == previous.get('capture_preparation')","return True")]),
 ('loop6 preparation presence obligation removed','lifecycle',[("if not preparation_authority(previous,ledger): return False","if False: return False")]),
 ('loop6 repaired prerequisite bypasses cleanup','lifecycle',[("if state.get('repaired_record') or state.get('repair_cleanup'): return deepcopy(previous)","if False: return deepcopy(previous)")]),
 ('loop6 reclamation deletion omitted','lifecycle',[("del attempts[address]","pass")]),
 ('loop6 request authority omitted','lifecycle',[("and row['signature'] == sha256(b'authenticated-purpose-2d-redrive:'+row['raw']).digest()","and True")]),
 ('loop6 request reclamation omitted','lifecycle',[("del requests[address]","pass")]),
 ('loop6 unbounded addressed attempt log','lifecycle',[("address = (state['held_key'],1)  # one current/disputed authority slot","address = (state['held_key'],next_count)")]),
 ('loop6 window includes accepted members','lifecycle',[("identity,unresolved_root(prior['unresolved'])","identity,sha256(image_bytes(prior['roster'])).digest()")]),
 ('loop6 incoming invocation order changes root','lifecycle',[("for position,message,body in sorted(unresolved)","for position,message,body in unresolved")]),
 ('loop6 persisted invocation order changes root','lifecycle',[("for p,message,body in sorted(unresolved)","for p,message,body in unresolved")]),
 ('loop6 every historical result becomes final','lifecycle',[("final = b''.join(codec['pack']('>I',p)+N(local)+evidence for p,(local,observation,evidence) in sorted(last.items()))","final = b''.join(codec['pack']('>I',p)+N(local)+evidence for p,local,observation,kind,parent,send,evidence in rows if kind == 'result')")]),
 ('loop6 released transfer owner removed','lifecycle',[("O(owner if kind == 'new' else None)","O(owner if kind == 'new' and state != 'released' else None)")]),
 ('pass8 HX-EV-REDRIVE-REQUEST-2 family cap', 'codec', [("assert len(raw) <= record_caps.get(domain,4096), ('record-length',domain,record_caps.get(domain,4096))", "assert domain == 'HX-EV-REDRIVE-REQUEST-2' or len(raw) <= record_caps.get(domain,4096), ('record-length',domain,record_caps.get(domain,4096))")]),
 ('pass8 HX-EV-RESUME-PREPARATION-HEAD-1 family cap', 'codec', [("assert len(raw) <= record_caps.get(domain,4096), ('record-length',domain,record_caps.get(domain,4096))", "assert domain == 'HX-EV-RESUME-PREPARATION-HEAD-1' or len(raw) <= record_caps.get(domain,4096), ('record-length',domain,record_caps.get(domain,4096))")]),
 ('pass8 HX-EV-CAPTURE-ORIGIN-1 family cap', 'codec', [("assert len(raw) <= record_caps.get(domain,4096), ('record-length',domain,record_caps.get(domain,4096))", "assert domain == 'HX-EV-CAPTURE-ORIGIN-1' or len(raw) <= record_caps.get(domain,4096), ('record-length',domain,record_caps.get(domain,4096))")]),
 ('pass8 HX-EV-REDRIVE-REPAIR-1 family cap', 'codec', [("assert len(raw) <= record_caps.get(domain,4096), ('record-length',domain,record_caps.get(domain,4096))", "assert domain == 'HX-EV-REDRIVE-REPAIR-1' or len(raw) <= record_caps.get(domain,4096), ('record-length',domain,record_caps.get(domain,4096))")]),
 ('pass8 preparation eight rows', 'codec', [("assert count <= 8, 'progress-row-count'", "assert count <= 9, 'progress-row-count'")]),
 ('pass8 attempt member ceiling', 'codec', [("assert 0 < row[0] <= 59, 'attempt-member-position'", "assert 0 < row[0] <= 60, 'attempt-member-position'")]),
 ('pass8 attempt row ceiling', 'codec', [("assert 0 < values[4] <= 11328, 'attempt-row-count'", "assert 0 < values[4] <= 11329, 'attempt-row-count'")]),
 ('pass8 capture image cap', 'codec', [("assert len(values[1]) <= 7*1024, 'capture-origin-image-length'", 'assert True')]),
 ('pass8 origin prior image cap', 'codec', [("assert len(values[6]) <= 128*1024, 'origin-prior-image-length'", 'assert True')]),
 ('pass8 preparation prior image cap', 'codec', [("assert len(values[5]) <= 128*1024, 'preparation-prior-image-length'", 'assert True')]),
 ('pass8 preparation successor image cap', 'codec', [("assert len(values[6]) <= 256*1024, 'preparation-successor-image-length'", 'assert True')]),
 ('pass8 preparation manifest image cap', 'codec', [("assert len(values[10]) <= 128*1024, 'preparation-manifest-image-length'", 'assert True')]),
 ('pass8 repair locator cap', 'codec', [("('HX-EV-REDRIVE-REPAIR-1',4):4096", "('HX-EV-REDRIVE-REPAIR-1',4):8192")]),
 ('pass8 attempt locator cap', 'codec', [("('HX-EV-REDRIVE-ATTEMPT-1',3):4096", "('HX-EV-REDRIVE-ATTEMPT-1',3):8192")]),
 ('pass8 request operator subject cap', 'codec', [("('HX-EV-REDRIVE-REQUEST-2',5):256", "('HX-EV-REDRIVE-REQUEST-2',5):512")]),
 ('pass8 signature envelope cap', 'codec', [("('HX-EV-RESUME-ORIGIN-1',10):8192", "('HX-EV-RESUME-ORIGIN-1',10):16384")]),
 ('pass8 attempt positive count', 'codec', [("elif domain == 'HX-EV-REDRIVE-ATTEMPT-1':\n        assert values[1] > 0", "elif domain == 'HX-EV-REDRIVE-ATTEMPT-1':\n        assert True")]),
 ('pass8 cleanup phase enum', 'codec', [("assert values[1] > 0 and values[4] in {'repaired-readback','record-deleted','record-readback','entry-deleted'}", 'assert values[1] > 0')]),
 ('pass8 repaired genesis excluded', 'codec', [("assert (values[7] == 'repaired') == (values[5] > 1), 'repair-state-generation'", 'assert True')]),
 ('pass8 repair predecessor authenticated', 'codec', [('assert expected is not None and values[6] == expected', 'assert True')]),
 ('pass8 transferred marker ownership', 'codec', [('assert (values[12] is not None) == bool(values[14])', 'assert True')]),
 ('pass8 membership fresh zero proof', 'lifecycle', [("or not proof['zero'] or proof['at'] != now or proof['head'] != expected_predecessor", "or proof['head'] != expected_predecessor")]),
 ('pass8 membership contiguous generation', 'lifecycle', [("if expected_generation != namespace['generation']+1 or expected_predecessor != sha256(namespace['head']).digest():", "if expected_predecessor != sha256(namespace['head']).digest():")]),
 ('pass8 membership immutable pin', 'lifecycle', [("if current_bytes != namespace['pin_bytes']: return 'FirstSendMembershipChangedHold',before", "if False: return 'FirstSendMembershipChangedHold',before")]),
 ('pass8 membership refuses after attempt', 'lifecycle', [("if namespace['attempts'] != 0: return 'FirstSendMembershipChangedHold',before", "if False: return 'FirstSendMembershipChangedHold',before")]),
 ('pass8 compaction deletes full origin', 'lifecycle', [('if address in staged.rows: staged.delete(address,owner,sha256(staged.rows[address]).digest())', 'if False: staged.delete(address,owner,sha256(staged.rows[address]).digest())')]),
 ('pass8 reclamation deletes identity bodies', 'lifecycle', [('staged.delete(key,identity,sha256(staged.rows[key]).digest()); staged.deletions.pop(key,None)', 'pass')]),
 ('loop6 pass8 capture premature refund', 'lifecycle', [("cleanup = {'owner':owner,'amount':len(retained)+32768,'deleted':{}}", "assert ledger.refund(previous['account_kind'],previous['account'],len(retained)+32768)\n            cleanup = {'owner':owner,'amount':len(retained)+32768,'deleted':{}}")]),
 ('loop6 pass8 held premature refund', 'lifecycle', [("cleanup = {'owner':previous['held_key'],'terminal':expected,'previous':state_hash(previous),'deleted':{}}", "assert ledger.refund(previous['account_kind'],previous['account'],previous['charged_bytes'])\n        cleanup = {'owner':previous['held_key'],'terminal':expected,'previous':state_hash(previous),'deleted':{}}")]),
 ('pass8 origin reclamation loses signature', 'lifecycle', [('assert staged.read_signed(key,owner) == original', 'staged.signatures.pop(key,None)\n    assert staged.read_signed(key,owner) == original')]),
 ('loop7 wait counter stays deployment', 'lifecycle', [("U('tenant' if a[11]=='tenant' else 'deployment')", "U('deployment')"), ("fields[4]='tenant' if a[11]=='tenant' else 'deployment'", "fields[4]='deployment'")]),
 ('loop7 move keeps stale wait body', 'lifecycle', [('elif staged.records[wkey] != wait:', 'elif False:')]),
 ('loop7 queue pending authority omitted', 'lifecycle', [("if target == 'deployment' or target == 'tenant:'+a[1]],key=lambda r:(r[0],r[1]))", "if a[10]=='admitted' and (target == 'deployment' or target == 'tenant:'+a[1])],key=lambda r:(r[0],r[1]))")]),
 ('loop7 source replacement skips native read', 'lifecycle', [("source,sr,sg=staged.read(pkey,bytes(32)); pf=self.decode('D31-predecessor',source)", "source=old_p; pf=self.decode('D31-predecessor',source); sg=qgen")]),
 ('loop7 wrong rollback authority accepted', 'lifecycle', [('or expected_authority!=self.preparation_authority(scope)', 'or False')]),
 ('loop7 cleanup refunds at wait deletion', 'lifecycle', [("if stop=='wait-deleted': return 'cleanup-hold'", "if stop=='wait-deleted':\n                    self.backend.forget(self.charge_key(next_a[2],tenant))\n                    return 'cleanup-hold'")]),
 ('loop7 repair installs unrelated queue fixture', 'lifecycle', [('staged.records[qkey]=next_q', "staged.records[qkey]=codec['vectors']['D31-queue']")]),
 ('loop7 storage ignores encoded ceilings', 'lifecycle', [("assert used<=ceiling or used<=installed_used, 'queue-storage-capacity-refusal'", 'assert True')]),
 ('loop7 ledger replaces unrelated usage', 'lifecycle', [('used=installed_used-old_used+used; count=installed_count-old_count+count', 'used=used; count=count')]),
 ('loop7 owner generation reuses exhausted version', 'lifecycle', [("assert a[8] < U64_MAX, 'queue-authority-generation-exhausted'", "if a[8]==U64_MAX: return list(a)")]),
 ('loop7 slice2 inventory reservation ignored', 'lifecycle', [("assert charge[4]=='side-record' and charge[5:8]==(8192,0,8192) and charge[10]=='active', 'slice2-inventory-reservation'", 'assert True')]),
 ('loop7 authority original predecessor wrong key', 'codec', [("assert receipt[0]==K('HX-EV-PIN-CAPACITY-QUEUE-KEY-1',U(values[0]),U(counter)) and receipt[1]==bytes(32) and receipt[6]=='present', 'queue-original-predecessor-binding'", 'assert True')]),
 ('loop7 HX-EV-PIN-WAIT-PREPARATION-1 family cap', 'codec', [("assert len(raw) <= record_caps.get(domain,4096), ('record-length',domain,record_caps.get(domain,4096))", "assert domain == 'HX-EV-PIN-WAIT-PREPARATION-1' or len(raw) <= record_caps.get(domain,4096), ('record-length',domain,record_caps.get(domain,4096))")]),
 ('loop7 HX-EV-PIN-WAIT-AUTHORITY-1 family cap', 'codec', [("assert len(raw) <= record_caps.get(domain,4096), ('record-length',domain,record_caps.get(domain,4096))", "assert domain == 'HX-EV-PIN-WAIT-AUTHORITY-1' or len(raw) <= record_caps.get(domain,4096), ('record-length',domain,record_caps.get(domain,4096))")]),
 ('loop7 HX-EV-PIN-QUEUE-OWNERS-1 family cap', 'codec', [("assert len(raw) <= record_caps.get(domain,4096), ('record-length',domain,record_caps.get(domain,4096))", "assert domain == 'HX-EV-PIN-QUEUE-OWNERS-1' or len(raw) <= record_caps.get(domain,4096), ('record-length',domain,record_caps.get(domain,4096))")]),
 ('loop7 HX-EV-PIN-QUEUE-PREDECESSOR-1 family cap', 'codec', [("assert len(raw) <= record_caps.get(domain,4096), ('record-length',domain,record_caps.get(domain,4096))", "assert domain == 'HX-EV-PIN-QUEUE-PREDECESSOR-1' or len(raw) <= record_caps.get(domain,4096), ('record-length',domain,record_caps.get(domain,4096))")]),
 ('loop7 HX-EV-PIN-QUEUE-RECEIPT-1 family cap', 'codec', [("assert len(raw) <= record_caps.get(domain,4096), ('record-length',domain,record_caps.get(domain,4096))", "assert domain == 'HX-EV-PIN-QUEUE-RECEIPT-1' or len(raw) <= record_caps.get(domain,4096), ('record-length',domain,record_caps.get(domain,4096))")]),
 ('loop8 legacy maximum envelope', 'codec', [("'HX-EV-COMMAND-SCOPE-LEGACY-2':8192", "'HX-EV-COMMAND-SCOPE-LEGACY-2':4096")]),
 ('loop8 counter maximum envelope', 'codec', [("'HX-EV-PUBLICATION-COUNTER-1':4096", "'HX-EV-PUBLICATION-COUNTER-1':1024")]),
 ('loop8 usage maximum envelope', 'codec', [("'HX-EV-SCOPE-SHARD-USAGE-1':2048", "'HX-EV-SCOPE-SHARD-USAGE-1':1024")]),
 ('loop8 tombstone exact reserve', 'lifecycle', [("'HX-EV-COMMAND-SCOPE-TOMBSTONE-2':4096}", "'HX-EV-COMMAND-SCOPE-TOMBSTONE-2':1024}")]),
 ('loop8 accepted progress regresses', 'codec', [("assert prior<=current, 'window-progress-accepted-regression'", 'assert True')]),
 ('loop8 window native source bypass', 'lifecycle', [("assert self.backend.read(self.window_source_key(prior['window_claim'],kind),bytes(32))[0]==read_image(raw), 'window-source-provider-readback'", 'assert True')]),
 ('loop8 window root follows current unresolved', 'lifecycle', [("codec['window_admission_members'](state['window_admission'],fields", "codec['window_admission_members'](codec['window_admission_bytes'](unresolved),fields")]),
 ('loop8 successful source remains active', 'lifecycle', [('limit=limit,hold_source=bytes(32),', "limit=limit,hold_source=before['hold_source'],")]),
 ('loop8 active hold inventory survives', 'lifecycle', [('self.consume_active_hold(staged)', 'pass')]),
 ('loop8 pin batch skips authenticated per pin bounds', 'lifecycle', [('or not self.authenticate_pin_candidates(account,amounts,candidates)', 'or False')]),
 ('loop8 parked state never exits', 'lifecycle', [('fields[8:10]=[state,caphash]', 'fields[8:10]=[fields[8],caphash]')]),
 ('loop8 queue creation omits actual overhead', 'lifecycle', [('capability=self._capability(backend); overhead=capability[7]', 'capability=self._capability(backend); overhead=0')]),
 ('loop8 blocked tenant moves anyway', 'lifecycle', [("if not fit_receipt[0]: return 'tenant:'+tenant", "if False: return 'tenant:'+tenant")]),
 ('loop8 tenant fit ignores current authority', 'lifecycle', [("assert fit_receipt is not None and fit_receipt==self.tenant_fit_receipt(tenant,fit_receipt[0]), 'queue-tenant-fit-authority'", 'assert True')]),
 ('loop8 destination aggregate metadata widened', 'codec', [("for key,value in metadata.items())<=16384, 'destination-metadata-total-length'", "for key,value in metadata.items())<=65536, 'destination-metadata-total-length'")]),
 ('loop8 destination duplicate canonical authority bypass', 'codec', [("assert len({key for key,value in pairs}) == len(pairs), 'destination-duplicate-key'", 'assert True'), ("assert json.dumps(value,ensure_ascii=False,sort_keys=True,separators=(',',':')).encode()==raw, 'destination-canonical-bytes'", 'assert True')]),
 ('loop8 destination document widened', 'codec', [("len(raw) <= 64*1024, 'destination-document-length'", "len(raw) <= 65*1024, 'destination-document-length'")]),
 ('loop9 closure omits admitted member', 'lifecycle', [("assert set(last) == {row[0] for row in admitted}, 'closure-admitted-member-coverage'", 'assert True'), ("B(codec['pack']('>I',len(admitted))+final)", "B(codec['pack']('>I',len(last))+final)")]),
 ('loop9 producer trusts selected shard', 'lifecycle', [("assert fields[0]==self.tenant and shard==self.shard, 'scope-derived-shard-authority'", 'assert fields[0]==self.tenant')]),
 ('loop9 direct resume skips execution binding', 'lifecycle', [("assert fields[:2] == (state['tenant'],codec['H']('scope')) and fields[3] == state['window'], 'direct-window-execution-binding'", 'assert True')]),
 ('loop9 C2 registration authority bypass', 'codec', [('authenticate_c2_groups(values[0],values[1],values[2],groups,c2_readbacks if c2_store is None else c2_store)', 'pass')]),
 ('loop9 current empty partition creates success', 'lifecycle', [("if not unresolved:\n        return {'outcome':'resume_not_eligible'", "if False:\n        return {'outcome':'resume_not_eligible'")]),
 ('loop9 invocation indexes outlive retry owners', 'lifecycle', [("pairs=tuple((owner,value) for owner,value in zip(owners,state['invocations']) if owner in retained)", "pairs=tuple(zip(owners,state['invocations']))")]),
 ('loop9 reservation uses mutable ceiling caches', 'lifecycle', [('return f[3],f[4],f[5],f[6]', 'return self.tenant_ceiling,self.deployment_ceiling,self.reserve_bytes,self.unidentified_ceiling')]),
 ('loop9 hold owner mapping bypass', 'codec', [("assert values[11] in hold_owners[values[2]], 'hold-owner-binding'", 'assert True')]),
 ('loop9 activation accepts understated accounting', 'codec', [("assert events <= ((1<<64)-1)//8192 and accounting == events*8192, 'activation-conservative-accounting'", 'assert events <= ((1<<64)-1)//8192')]),
 ('loop9 tombstone invented without required predecessor', 'lifecycle', [("elif domain=='HX-EV-COMMAND-SCOPE-TOMBSTONE-2':\n                assert existing is not None", "elif domain=='HX-EV-COMMAND-SCOPE-TOMBSTONE-2' and existing is not None:\n                assert existing is not None"), ("assert domain!='HX-EV-COMMAND-SCOPE-TOMBSTONE-2', 'scope-tombstone-needs-required'", 'assert True')]),
 ('loop9 compaction ignores retained obligations', 'lifecycle', [("assert obligations==self.compaction_proof(predecessor,now), 'scope-compaction-obligations'", 'assert True')]),
 ('loop9 successor commits before exhausted progress', 'lifecycle', [("assert checked_add(progress_head[1],1) is not None, 'reconcile-progress-generation'", 'assert True'), ('staged_store=deepcopy(self)\n        readback = staged_store.backend.write', 'staged_store=self\n        readback = staged_store.backend.write')]),
 ('loop9 C2 restart trusts process cache', 'lifecycle', [("selected={k:v for k,v in self.backend.c2_sources.items() if k[:3]==(self.tenant,codec['H']('scope'),prior['window'])}", "selected={k:image_bytes(v) for k,v in codec['c2_readbacks'].items() if k[:3]==(self.tenant,codec['H']('scope'),prior['window'])}")]),
 ('loop10 repair rollback uses obsolete head', 'lifecycle', [("head=list(decode_record(current[0],'HX-EV-PUBLICATION-RESUME-STATE-3'", "head=list(decode_record(store.imported('predecessor'),'HX-EV-PUBLICATION-RESUME-STATE-3'")]),
 ('loop10 repair finalization releases before all progress', 'lifecycle', [("if not _finalize_staged:\n                        staged_final=deepcopy(self)", "if False:\n                        staged_final=deepcopy(self)")]),
 ('loop10 repair expired legacy renewal omitted', 'lifecycle', [("if domain=='HX-EV-COMMAND-SCOPE-LEGACY-2':\n                    assert fields[:6]==original[:6]", "if False:\n                    assert fields[:6]==original[:6]")]),
 ('loop10 repair account kind aliases identity', 'lifecycle', [("account_key=(account_kind,account)", "account_key=account")]),
 ('loop10 repair unknown capture object refunds', 'lifecycle', [("assert validate_presence(object_absence,owner,'object',1)=='absent', 'capture-object-closure-readback'", "assert True, 'capture-object-closure-readback'")]),
 ('loop10 repair original predecessor UTC ignored', 'lifecycle', [("predecessor_bytes=predecessor)", "predecessor_bytes=None)")]),
 ('loop10 durable origin compaction skipped', 'lifecycle', [("assert compact_successful_origin(store) and store.origin_key(owner) not in backend.rows", "assert True and store.origin_key(owner) not in backend.rows")]),
 ('loop10 maximum C2 attempt missing', 'lifecycle', [("for position,message,body in maximum_prior['roster']:\n        parent=codec['H'](f'native-parent-{position}'); send=codec['H'](f'native-send-{position}')\n        for local in range(1,65):", "for position,message,body in maximum_prior['roster']:\n        parent=codec['H'](f'native-parent-{position}'); send=codec['H'](f'native-send-{position}')\n        for local in range(1,64):")]),
 ('loop10 maximum committed identities collapse', 'lifecycle', [("maximum_prior['roster']=tuple((position,'m'*1020+f'{position:04d}',b'b'*8192)", "maximum_prior['roster']=tuple((position,'m'*1024,b'b'*8192)")]),
 ('loop10 repair incremental invalid accounting scheduled', 'lifecycle', [("if not consistent:\n        return {'outcome':'LegacyArrayLimit'", "if not consistent and not incremental_capability:\n        return {'outcome':'LegacyArrayLimit'")]),
 ('loop10 repair reconciled index accepts changed rows', 'lifecycle', [("fields==expected", "fields[:11]==expected[:11]")]),
 ('loop11 held pair omits count CAS','lifecycle',[("staged.held_entries[held_entry_address(previous)]=next_held","pass")]),
 ('loop11 required repair record omitted','lifecycle',[("staged_records[rkey]=record; staged_entries[ekey]=entry","staged_entries[ekey]=entry")]),
 ('loop11 repair inventory membership omitted','lifecycle',[("staged.hold_indexes=staged_indexes","pass")]),
 ('loop11 record deletion readback bypassed','lifecycle',[("if ('record_deletion' in cleanup and getattr(ledger,'repair_deletions',{}).get(('record',rkey))!=cleanup['record_deletion']): return state","if False: return state"),("if (rkey in ledger.repair_records or ledger.repair_deletions.get(('record',rkey))!=cleanup['record_deletion']): return state","if False: return state")]),
 ('loop11 inventory removal skipped','lifecycle',[("staged.hold_indexes[ikey]=successor","pass")]),
]
source_mutations_rejected = 0
for name,target,replacements in faults:
    mutated = codec_source if target == 'codec' else probe_source
    for old,new in replacements:
        assert old in mutated, (name,'missing mutation target')
        mutated = mutated.replace(old,new,1)
    try:
        with redirect_stdout(StringIO()): exec(mutated,{'fault_probe':True,'fault_name':name})
    except AssertionError:
        source_mutations_rejected += 1
        if source_mutations_rejected % 20 == 0: print(f'D12 owning source mutations: {source_mutations_rejected}/{len(faults)} rejected',flush=True)
    else:
        raise AssertionError((name,'source mutation survived owning verifier'))
assert source_mutations_rejected == 180
print(f'D12 lifecycle verifier: {len(status_cases)} status cases, {len(matrix_cases)} matrix rows, {len(mutants_rejected)} invariant checks, {source_mutations_rejected} source mutations rejected, {len(found)} dispositions, {len(supplementary_records)} transition codec matches and {supplementary_malformed} transition malformed rejections, {len(loop5_ids)} loop-5, {len(loop6_ids)} loop-6 and {len(pass8_ids)} pass-8 repairs and {len(loop7_ids)} loop-7 repairs passed; loop7 queue {loop7_queue_metrics}, slice2 {loop7_slice_metrics}; {preparation_metrics["restarts"]} persisted-only restart boundaries, {preparation_metrics["cleanup"]} cleanup boundaries, {preparation_metrics["refusals"]} durable-evidence refusals and {preparation_metrics["expired_completions"]} current-time completions; loop6 {loop6_metrics}')
PY
```

## D13. Integration handoff

Story 6.5 imports this candidate as one change. It does not retain the loop-1 text beside these replacements and invents no bridge rule.

### D13.1 Rule and imported-section replacements

| Target | Exact integration action |
| --- | --- |
| `[I-06]` | Replace with D2, including all three activation dispositions, versioned record, idle-stream re-evaluation, storage, slice, and exit. |
| `[I-10]`, `[I-14]`, `[I-15]`, `[I-16]` | Replace with D3's precedence, drain epochs/resolution, polling values, complete drain-reason classification, preparation recovery, closed `CommandOutcomeHold` set, and Admin current-hold join. |
| `[I-12]` | Replace with D4's slice-2 claim, cutover/sunset, 256-shard accounting, exact-predecessor/closed-obligation expiry renewal, tombstone reconciliation/expiry, status 410, availability outcome, erasure, and codecs. |
| `[I-17]` | Replace with D6. |
| `[I-29]`, `[I-30]` | Replace with D7's capability, exact charge/counter codecs, kind-qualified tenant/capture-scope account rows and receipts, scope-ceiling readiness, all-three-counter predecessor authentication, staged charge transfer, atomic batch reservation, quarantine kind, checked arithmetic, stable fail-closed `publication_pin_capacity_hold`, zero partial mutation, closed reservation/refund kinds, and activation. `[I-26]` and `[I-28]` cite these codecs and maxima rather than defining another charge. |
| `[I-31]` | Replace with D8's inventory-only invalid-candidate state, durable global ticket allocator/exhaustion, duplicate-subject rejection, canonical order, authenticated ceiling/reserved-slot decode, single-residence fair queue, cross-counter moves, parked discovery, exact count framing, rerender rule, storage, activation, and exits. |
| `[I-36]` | Replace with D11.1, including policy and scope identity, maximum-inclusive ordinary capture, addressed charged partial-capture completion, fresh retained-byte/locator/charge authority before any reservation and every redrive, authenticated object-absence proof before a partial-charge refund, one serializable addressed held-count/request/attempt/deletion commit with exact CAS/readback, persistent separately addressed repair prerequisite and precharged inventory slot, terminal/oversize quarantine, unambiguous routes, refund, erasure and activation. |
| `[I-37]` | Replace with D11.2's scope-discriminated IDs/routes, versioned reason, ordered index with persisted repair membership/removal, bounded directory, zero-overflow readiness, reconciler lease, metrics, Admin join, storage, activation, and erasure. |
| `[I-45]` | Replace with D9. Purpose `2d`'s assignment becomes: D2 activation, D9 resume/window claims and D11 redrive only. Import D9.4's bounded origin/reconstruction/progress/invocation codecs, slots/addresses/cleanup authority, complete attempt-set root, exact original predecessor bytes/UTC, current-head rollback compaction guarded by the complete reconciled predecessor image, whole-turn staged finalization, current-time retry expiry and independent canonical response. Every invocation consumer uses the ordinal/limit/request-bound identity. Caller identity, acyclic hashes, staged charge swap, audit, unchanged drain-only claim and replies are inseparable. |
| `[I-46]` | Replace with D10's separate 2 MiB `side-record` preparation-slot charge, pre-cleanup chunk/manifest capsule charge and generation/ordinal-fenced exclusive recovery. No reconstruction from source-less historical status/dead letter is permitted, and repeated exhaustion reuses the immutable capsule. |
| 6.5c C1 | Replace “pin charge atomically at pin CAS” with D7's ledger batch reservation plus reservation-bound pin installation. Preserve exact global pins, ceilings, and no-send-before-readback. Add `oversize-quarantine` only for invalid carriers under D11. |
| 6.5c C2 | Amend the first-send outcome in place with D5's versioned resolution. Add D9's window namespace/binding to new resumed sends; the whole-operation terminal fence still precedes duplicates. |
| 6.5c C4 | Amend acknowledgement in place with D11's authenticated captured-copy terminal handoff: it may acknowledge that physical copy while explicitly leaving every logical route/effect obligation open; it is not a successful route decision and cannot satisfy C4's ordinary success proof. |
| 6.5c C5 | Amend closure in place exactly as D9.2: terminal closure uses the permanent operation fence; resume uses a permanent window fence, addressed complete registration/Unknown/result set and authenticated closure accumulator. C2/C5 verify complete evidence against final summaries and consume the window chain. BC-02 points to D9/D10, never command re-execution. |
| A8 preparation | Preserve the existing `command-execution-scope:` + lowercase SHA256(`U tenant || U executionMessageId`) for legacy and required admission, with no address domain/codec byte. Keep unchanged A8/[I-09] authority by exact name: `HX-EV-RESPONSE-PREPARATION-WRITE-1` at `command-response-preparation-write:` plus ScopeOpHash. Recovery requires both existing immutable outputs and generation-bound receipts; a missing immutable output remains the indexed non-resumable `response_preparation_hold` incident until whole-tenant erasure or a separately approved migration. |

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
| 2 | first activate/read back D11.2 hold inventory/directory/telemetry lease, onboarding and hold reservations and gateway re-evaluation; then deploy/read D7 capability/ledger and begin D4 legacy claims; pin `H`; validate all codecs/readbacks while behavior remains legacy |
| 3 | activate D2 full-replay inventory, retain slice-2 D11.2 discovery, activate D9 tenant resume routes and ReplayController safety gate, and D10 capsule-before-cleanup/recovery fence; wait the D4 horizon and read back cutover before slice 4 |
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

Replace the loop-1 disposition rows for owned rules with the pass-1/pass-2 tables below. In §11.6 remove the old owned `I06`, `I12`, `I14`, `I17`, `I29`, `I31`, `I36`, `I37`, `I45`, `I46` answers and import only the 30 original, twelve supplementary and five loop-7 literal `D*` answers, nineteen historical digest keys and 51 complete physical addresses. Retain unowned A/B/C and integration answers unchanged. Cite this candidate's canonical committed source: the full 40-hex repository revision, candidate path and SHA-256, and SHA-256 of each exact fenced bash block body (UTF-8, from `python3` through the terminating `PY`, including its final newline). Obtain those values from the actual committed candidate when importing; this uncommitted repair creates no future revision or self-referential candidate hash. Record the first two blocks' outputs from that source revision, running them in its separate source worktree because they intentionally read the fixed 6.5d candidate path. Do not splice those blocks into AD-13. Their expected output is 47 byte answers/digest probes, nineteen digest keys, 51 complete physical addresses, 230 malformed and 144 semantic rejections; 14 statuses, four matrix rows, 27 invariants, 180 rejected source mutations, 54 historical dispositions, ten transition matches/50 malformed rejections, 46 restart/four cleanup boundaries, 70 evidence refusals, three current-time completions, the 21/20/21/19 earlier repair IDs and 33 pass-8 IDs and seven loop-7 IDs and twelve iteration-8 repair IDs, plus the asserted loop-7 queue/slice-2 metrics and loop-6 metrics `(64,4,32,23,6,6,4)` and 141 bounded failed redrives. Iteration 10 additionally requires the eight BHR12 repair cases, VGR12-01, eleven owning mutations, 67 actual successes on one durable backend, and the distinct-identity maximum C2 case (59 members, 3,776 attempts, 11,328 observations). Iteration 11 additionally requires addressed held-count/pair atomicity, genuine two-carrier repair/index coexistence, three precommit repair-stage aborts, required/repaired exact retries, four persisted cleanup boundaries, eighteen new unchanged-refusal probes and five owning source-fault rejections. The third protected-path block is historical candidate-source evidence and is not ported or run against an edited AD-13. Its source pins describe the reviewed 6.5d workspace, not Story 6.5 integration's authorized surfaces. Integration applies AD-13 §11.4/[I-47] to its own surfaces, verifies the cited source outputs and imported literals, then recomputes only its own content-bound digest after splicing and verification.

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
| E2-24 | replacement | D3 names unchanged A8/[I-09] authority: only two already-present verified outputs permit the recovery owner to create the preparation-write record; a missing immutable output is a non-resumable indexed incident whose only exits are whole-tenant erasure or a separately approved migration. |
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
| VG-1 | D12 now executes typed D5 resolution transitions for both revision triggers, authenticates fresh zero-send/readback/predecessor and contiguous generations, and refuses reopening after a send. The earlier truth table alone was insufficient. |
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
| EC-8 | Same root as VG-1; the typed membership-revision transition and its directed failures are now executed, replacing the earlier truth-table-only claim. |
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

### Review-loop-2 repair register

| Finding | Verified repair |
| --- | --- |
| BHR2-01 / VGR2-O1 / ECR2-04 | D9 window v2 binds the prior state and caller-stable identity; only the later state binds the window hash. D12 constructs that dependency and rejects a successor-state back-edge. |
| BHR2-02 / BHR2-03 / VGR2-05 / ECR2-03 | D9 separates caller-stable identity from exact carrier bytes, indexes live/orphan/tombstone evidence by that identity, returns byte-identical retries without new effects, conflicts changed bytes, and retains bounded tombstones until their exact expiry-plus-30-day deletion boundary or erasure. |
| BHR2-04 | D7 reservation v2 carries tenant, tenant-pool, and deployment predecessor hashes; D12 requires the complete authenticated set. |
| BHR2-05 / ECR2-02 | D8 queue v4 owns a monotonic durable deployment allocator, lost-ack readback, canonical ordering, and fail-closed u64 exhaustion. |
| BHR2-06 | D8 fixes the sort tuple as unsigned ticket, raw canonical tenant UTF-8, then raw ScopeOpHash. |
| BHR2-07 | D7 readiness restores `scopeRetentionCeiling >= 64 MiB`; D12 exercises below/at/above. |
| BHR2-08 / ECR2-01 | D11.3 gives every replacement-owned revision, head, counter, reservation, wait, policy, quarantine, index, and directory an exact framed address and mutation rule. |
| BHR2-09 / ECR2-05 | D10 uses at most 17 independently addressed 61-row chunks plus a bounded manifest for all 1,000 maximum-width V1 members; D12 constructs that maximum. |
| BHR2-10 | D11 held keys bind scope, deployment, tenant presence, component, topic, physical subscription, and carrier hash; D12 proves cross-identity separation. |
| BHR2-11 | D11 requires tenant exactly for tenant scope and forbids it for deployment scope; D12 rejects both invalid pairings. |
| BHR2-12 | D11 activates the ordinary retained-object charge before physical-copy acknowledgement and keeps it until logical closure/deletion readback. |
| BHR2-13 | D11 policy v3 has a framed revision key, CAS head, predecessor chain, and explicit supersession; D12 rejects a revision-2 genesis. |
| BHR2-14 / ECR2-08 | D3 maps only a nonempty all-class-01 set to exhaustion; unknown/empty/contradictory classes map to evidence conflict even at maximum. |
| BHR2-15 / VGR2-01 / ECR2-11 | D3 requires both pre-existing outputs and generation receipts. D12 never synthesizes either and exercises each missing/mismatched case. |
| BHR2-16 / ECR2-12 | D9/D12 partition exact `(position, MessageId, bytes)` tuples and reject a position/MessageId swap. |
| BHR2-17 | D12's generic record decoder rejects missing, duplicate, reordered, overflowing-length, and trailing-field mutations across resume, legacy optional/signed, and queue byte families. |
| BHR2-18 | D7/D9 define staged overlap, old/new ownership, every crash side, single release, and no transient ceiling bypass; D12 executes rollback, finalize, refusal, and repeated recovery. |
| VGR2-02 | D12 exercises conflict/unavailable precedence pairwise over terminal, published, drain, class-01, and class-02 states. |
| VGR2-03 | The drain-only matrix consumes `resume_publication` output and proves unchanged roster/window, exact source-to-intent resolution, checked limit, and recorded invocation. |
| VGR2-04 | D12 drives ordinal, window, closed-window count, drain-limit, zero increment, and charge-sum failures through the transition with identical pre/post state. |
| VGR2-06 | D8/D12 decode the persisted reserved-slot count and authenticated ceiling before any move, rejecting combined overflow. |
| VGR2-07 | D4/D12 exercise below/at/above ten years and prove slice 4 stays inactive above the maximum. |
| VGR2-08 | D10/D12 exercise every allowed recovery edge, transport/evidence repair, generation/ordinal advance, and forbidden skip/change. |
| VGR2-09 | D2/D12 separately cross each post-activation count/readable/accounting hard bound with zero partial dispatch. |
| ECR2-06 | D10 reuses the immutable chunks/manifest on second exhaustion and advances only recovery generation/ordinal. |
| ECR2-07 | D11 makes failed redrive return durably to `captured` with incremented count, typed error hash, retained charge/object, and bounded next retry. |
| ECR2-09 | D7/D12 validate closed account kind on both reservation and refund with unchanged counters. |
| ECR2-10 | D8 rejects a stable capacity subject already resident or reserved before ticket/slot/charge mutation. |

### Review-loop-3 repair register

These repairs preserve D1–D13, the approved baseline and initial baseline evidence, all historical dispositions, protected child/source hashes, and the open D-SPLIT/in-progress tracker state. The evidence below is executable local codec/transition evidence; provider atomicity and crash guarantees remain future acceptance vectors.

| Finding | Verified repair |
| --- | --- |
| BHR3-01 / ECR3-01 | D8 consumes the exact allocated ticket, materializes one row plus its counterpart reservation, and reuses the admission receipt on lost acknowledgement. D12 executes allocation followed by admission, exact retry, duplicate/changed subject, full destination, unavailable charge, invalid state, and allocator exhaustion without new leaked slots or charges. Its allocator source mutation is killed by that actual caller path. |
| BHR3-02 / ECR3-02 | D9 drain-only retains the exact active claim/hash, window, closed count, roster, accepted and unresolved sets; its resolution binds old source/old limit, constructible successor invocation, and checked new limit. D12 derives and checks the invocation from the unchanged claim and rejects the replacing-claim source mutation. |
| BHR3-03 | D9 retains the bounded exact signed claim for reconstruction and makes an orphan audit authoritative partial success. D12 models the crash after audit/stage but before state CAS, completes the recorded ordinal/window/charge, returns identical canonical response bytes, fences competing requests, and creates no second audit/invocation. Label-only response and prior-state orphan mutations both fail. |
| BHR3-04 / ECR3-03 | Every successful live row stores request expiry. Authenticated reconciliation moves it to a tombstone at expiry, sets delete-after exactly 30 days later, and recovers capacity only at deletion. D12 covers before/at/after expiry and deletion, unauthenticated deletion refusal, reconstructed conflicts, and capacity recovery; disabling conversion fails. |
| BHR3-05 / ECR3-06 | D10 constructs 17 actual bounded chunk records and a real manifest for 1,000 maximum-width members. D12 validates every addressed length/hash/count/endpoint, uncounted chunk-row domain, counted total root, and the independent 179-byte codec fixture; missing/swapped/changed chunks and bad roots fail. The wrong-domain source mutation is rejected. |
| BHR3-06 / ECR3-04 | D10 recovery checks positive u64 ordinal and contiguous checked generation before mutation. D12 rejects zero/above-max ordinals and generation exhaustion with identical input; directed ordinal and generation overflow mutations fail. |
| BHR3-07 | Recovery carries owner, expected next generation, exact predecessor, immutable capsule binding, and a recomputed authenticated repair receipt. D12 checks every allowed/forbidden graph edge, all three typed failures, competing/stale owners, skipped generation/state, missing/forged repair, changed capsule, and second exhaustion. Owner-bypass and Boolean-repair source mutations fail. |
| BHR3-08 | D11 redrive consumes the previous durable state. D12 follows captured -> redriving -> captured, restarts, fails again, and verifies incremented counts, distinct errors, preserved carrier/locator/charge/readback, bounded history/backoff, and terminal close. Resetting the count fails the second transition. |
| BHR3-09 | Capture reserves ordinary object quota and authenticates exact backend/key/bytes/readback before acknowledgement; failure refunds the attempted object amount while retaining the observed state charge. D12 covers quota refusal, absent/mismatched readback, stale policy, and exact capture retry without a second charge. Skipping quota or readback is rejected. |
| BHR3-10 / ECR3-05 | D11 policy installation consumes the exact selected predecessor/head and requires its next contiguous revision. D12 rejects genesis-at-2, skipped/stale/competing installation and acknowledgement under a superseded head, while comparing successor bytes/hash exactly. The skipped-revision source mutation fails. |
| BHR3-11 | D7's real reservation transition authenticates tenant, tenant-pool, and deployment receipts against current ledger state before one batch mutation. D12 independently forges each predecessor and a missing set, proving all counters/charges/reservations unchanged; valid admission mutates all three once, stale evidence fails, and the predecessor-bypass mutation is killed. |
| BHR3-12 | D12 returns typed signed/numeric/string/optional values, enforces positive bounded legacy count/range agreement, closed enums/identifier bounds and presence relationships, and decodes malformed framing in every record family. Signed-byte decoding and semantic-count-ceiling mutations fail concrete owning assertions. |
| VGR3-01 | D12 reconstructs caller requests with the same key and changed reason/source, proves equal stable identity but different carrier, and rejects conflict without mutation. Including reason in identity fails. |
| VGR3-02 | D12 independently varies scope, deployment, two valid tenants, component, topic, physical subscription, and carrier, requiring different exact framed held keys. Omitting tenant fails the cross-tenant assertion. |
| VGR3-03 | D12 uses two different nonzero policy predecessors and compares the exact successor codec/hash, selected head, and known-answer genesis bytes. Omitting the predecessor from policy bytes fails. |

### Review-loop-4 repair register

The 20 findings below each have one disposition. Seventeen bad-spec repairs and two moot verifier patches are implemented; the stale-CAS claim is rejected against the unchanged D7 contract. Earlier repairs, all 54 historical dispositions, D1–D13, source/baseline pins and external checkpoint checks remain intact. Evidence comes from the actual codec/transition assertions and 38 directed source mutations; it establishes no provider atomicity.

| Finding | Disposition and verified evidence |
| --- | --- |
| BHR4-01 | Repaired in D7/D9: rollback requires authenticated absence of both audit and successor. ChargeSwap executes unaudited rollback, audited partial-success retention, final activation/refund, owner conflicts, and repeated recovery with exact usage and ownership. Removing the audit guard fails. |
| BHR4-02 | Repaired in D12: B-field bounds follow family caps. A canonical 1,000-row queue at 1,078,163 bytes decodes, alongside large hold-index and directory records; cap-plus-one rejects. Restoring a universal 1 MiB guard fails these real records. |
| BHR4-03 | Rejected: D7 explicitly rejects stale predecessors for a new CAS. The successful first reserve changes all three predecessors; repeating that stale CAS must fail unchanged. The separate authenticated retained-reservation readback succeeds without another reservation or charge. |
| BHR4-04 | Repaired in D7/D12: the retained reservation binds tenant account, ScopeOpHash, candidate root, request identity, ordered amounts and original predecessors. Same-key changes to each identity component reject unchanged; ignoring identity fails. |
| BHR4-05 | Repaired in D8/D12: changed tenant/state/ticket materialization refuses without deleting the legitimate pending owner. Only the authenticated preparation owner/predecessor may roll back both slots and its charge, once. The destructive-conflict mutation fails. |
| BHR4-06 | Repaired in D9/D12: new admission decodes the caller carrier, recomputes its stable identity/hash, and authenticates tenant/handle/source against current evidence. Changed source with current availability conflicts unchanged; removing source comparison fails. |
| BHR4-07 | Repaired in D9/D12: orphan recovery validates audit/owner and protected prior state, then applies the recorded success delta to the current CAS head. It preserves authenticated intervening retry-index changes. Arbitrary tombstones, corrupt reconciliation/audit receipt, wrong owner, changed source and carrier hold unchanged. |
| BHR4-08 | Repaired in D11/D12: capture authenticates the pre-existing 32 KiB metadata charge and inventory reservation, with matching scope/account/owner/generation receipts, before object authority and acknowledgement. Missing/stale/wrong-owner metadata or inventory rejects unchanged; bypassing them fails. |
| BHR4-09 | Repaired in D11/D12: immutable object locator and quota owner derive from the full held key. Two carriers, two tenants and a deployment scope share one ledger/store while preserving the first object and charge; exact retries are once-only. A constant locator fails. |
| BHR4-10 | Repaired in D11/D12: restarted redriving authenticates the persisted attempt. Verified terminal readback closes it; unknown/unavailable/failed completion schedules captured retry while retaining carrier/count/charge. Corrupt evidence produces a bounded captured evidence hold. The stranded-redrive mutation fails. |
| BHR4-11 | Repaired in D12: typed semantic validation covers all framed families, including exactly 256 capability shards, directory shard 0..255, generation/predecessor genesis, charge presence/ownership, rows/roots/counts, enums, ranges and scope/locator relationships. Invalid hold-owner and counted-closure fixtures were corrected and independently recomputed; guards were preserved. |
| BHR4-12 | Repaired in D11/D12: PolicyStore persists create-once revision addresses and one CAS head. Differing successors cannot both install; exact lost acknowledgement is stable and only the selected contiguous revision authorizes capture. Permitting revision overwrite/head bypass fails. |
| ECR4-01 | Repaired with the family-bound contract in D12: the supported queue above 1 MiB, large index/directory and record-cap rejection assertions execute the real decoder. The universal-field-limit mutation fails. |
| ECR4-02 | Repaired with D7 reservation ownership: tenant-b cannot reuse tenant-a's key/amounts with fresh predecessor receipts. Complete counters/charges/reservation state remains unchanged, while exact authorized readback succeeds. |
| ECR4-03 | Repaired in D7/D12: every changed tenant, pool, deployment and unidentified counter preflights checked generation advance on both reserve and refund. Each maximum-generation case rejects byte-identical state; independent reserve/refund overflow mutations fail. |
| ECR4-04 | Repaired in D9/D12: an actual earlier live success expires through reconciliation while a later success is orphaned. Recovery retains its tombstone, never resurrects the expired live row, completes ordinal/charge exactly and returns the same response once. Replacing current tombstones with prior ones fails. |
| ECR4-05 | Repaired in D11/D12: four actual immutable carrier objects and their independently owned charges coexist in one persistent model ledger/store. Object keys differ by carrier and full scope identity; the original bytes/charge survive every later capture. |
| ECR4-06 | Repaired in D12: ordinary identifiers and optional identifiers/backend IDs run below/at/above 1,024 bytes; subject/locator fields separately run below/at/above 4,096. Widening ordinary identifiers to 4,096 fails its actual boundary assertion. The hold entry reserves 8 KiB so a legal 4,096-byte subject remains encodable. |
| VGR4-01 | Implemented under re-derivation: exact and changed-carrier retries traverse the reconciled tombstone state before deletion, returning expired/conflict with unchanged state. Removing the tombstone branch fails. |
| VGR4-02 | Implemented under re-derivation: corrupt audit receipt and wrong orphan owner cannot complete success; changed protected state/carrier and forged reconciliation also hold unchanged. Removing orphan authentication fails these owning assertions. |

### Review-loop-5 repair register

Each of the 21 routed findings appears once. The two excluded checkpoint defects remain outside the frozen specification scope; no runtime artifact is changed.

| Finding | Disposition and executed evidence |
| --- | --- |
| BHR5-01 | Repaired by actual D9.4 origin/reconstruction/progress byte rows, typed D7 ledger and D11 inventory rows, native provider receipts and bounded addresses. Restart discards poisoned process artifact/charge/progress caches. New artifacts derive from original origin and recorded successor intent, not unrelated planned fixture bytes; actual audit/state owner, carrier, predecessor, ordinal/result and hash links are checked. Seventy missing/changed/stale/unavailable or authenticated-but-contradictory evidence cases hold unchanged. Signed claims keep the independently authenticated A8 head and nonzero predecessor audit distinct from the D9 predecessor; both authority corruptions refuse. Six new families have independently constructed fixed answers and typed malformed/semantic probes. |
| BHR5-02 | Repaired by the charged original origin and actual bounded progress manifest. Forty-six origin/progress/intent/write/readback boundaries resume original claim bytes from durable evidence alone. Safe unaudited rollback requires exact staged-generation audit/successor absence plus every artifact deletion readback; four cleanup boundaries include deletion before progress CAS. Partial cleanup remains charged and cannot arm work, interrupted artifact cleanup retains original bytes until the final atomic rollback compaction, and fence/closure/audit forces completion. Completed/rolled-back inventory removal uses checked CAS; exact retries preserve all byte rows and charges. Re-signing, premature-refund, process-cache, unrelated-artifact and expired-successor mutations fail. |
| BHR5-03 | Repaired by D9.2's framed complete attempt set, contiguous registrations/Unknown observations/results and charged 11,328-row ceiling. The known answer includes all twelve evidence rows; changing/removing earlier evidence fails closure verification even with unchanged final summaries. Closure/history/window/state/audit hashes follow the exact complete root. |
| BHR5-04 | Repaired by the create-once capture preparation and matching charge-only/object-plus-charge recovery. Both restart boundaries authenticate the observed predecessor, metadata/inventory, object/locator/readback and active charges before completing captured state once. No second quota reservation occurs; changed/unavailable authority holds unchanged. |
| BHR5-05 | Repaired by the durable required/repaired record and separately indexed repair prerequisite. Three restarted automatic turns and manual repair refuse before authenticated repair; forged repair fails and exact repaired evidence permits one next count/send. The carrier reason remains intact. The current 32 KiB side reservation covers the bounded signed request/attempt overlap, repair record and auxiliary authority; the prerequisite inventory entry is charged separately at its 8 KiB ceiling to the operational-evidence quota under PD7. |
| BHR5-06 | Repaired by the ordinal/limit/request-bound invocation identity and exact invocation codec/address. Two actual fresh drain resumes advance ordinal/limit to 2/24 and 3/32 with different invocations, unchanged claim/member bytes and zero command executions. Exact retry preserves one invocation; removing the authorization epoch fails. |
| BHR5-07 | Repaired by authenticated current-time orphan completion. Twelve cases cross expiry and deletion boundaries with/without preceding reconciliation: completion returns the original response and retains the row only in the correct live/tombstone/deleted state. Retention never extends; the stale-time mutation fails. |
| BHR5-08 | Repaired by distinct, sorted successful live ordinals in the real resume-state decoder. Different identities at one ordinal reject; disabling that guard fails the actual framed-record assertion. |
| BHR5-09 | Repaired by per-kind canonical-length and recorded-overhead ceilings in the actual charge decoder. Below/at/above every 449/193/256/1,024 MiB maximum and 1,114,112-byte overhead are tested, with checked amount/generation handling. Independent ceiling/overhead weakening mutations fail. |
| BHR5-10 | Repaired by typed, owner/generation/kind-bound authenticated presence/absence receipts. Unavailable, boolean, forged, stale-generation and wrong-owner reads preserve stage/old charge/counters. Only proved two-sided absence permits safe cleanup/refund; audited partial success completes. Unavailable-as-absence mutation fails. |
| ECR5-01 | Repaired by the same current-time completion tests; recovery without earlier reconciliation tombstones/deletes its own expired row immediately, never resurrecting live evidence. |
| ECR5-02 | Repaired by using the decoded authenticated handle. Initial, live and orphan replies for `hxrsm1-other` match independently written complete canonical JSON bytes; substituting fixture `h` fails. |
| ECR5-03 | Repaired by both real partial-capture restart cases, exact once-only object/charge completion and conflicting readback refusal; the existing-partial-work rejection mutation fails. |
| ECR5-04 | Repaired by the ordinary capture transition's maximum-inclusive 193 MiB guard before quota or object mutation. Tests at 193 MiB minus one, exactly 193 MiB, plus one, 256 MiB and plus one prove no ordinary acknowledgement or counter change above its maximum; distinct quarantine/incident paths remain required. |
| ECR5-05 | Repaired by persisted required repair across restart and repeated turns. The original attempt/carrier/locator/count and active charge must authenticate before repaired readback permits another send; bypassing the prerequisite or repair receipt fails. |
| ECR5-06 | Repaired by fresh retained authority before every actual send. Changed restored bytes/object, missing object, wrong locator/readback/charge and unavailable store preserve count/state and emit no redrive bytes. Removing this guard fails the owning output assertions. |
| ECR5-07 | Rejected only under the frozen prohibition on runtime changes. The checkpoint reminder overflow is not repaired or represented as safe by this candidate; its exact external path remains pinned. |
| ECR5-08 | Rejected only under the frozen prohibition on runtime changes. The checkpoint reminder discovery defect is not repaired or represented as safe; its exact external path remains pinned. |
| ECR5-09 | Implemented under the full repair: policy redelivery count tests 0/1/63/64/65 against the real decoder. Zero and 65 reject; widening maximum 64 to 65 fails. |
| VGR5-01 | Implemented under the full repair: independently expected complete canonical bytes pin all five response fields on initial/live/orphan paths. Omitting the handle field or changing it fails separate directed mutations. |
| VGR5-O1 | Repaired by the same twelve authenticated current-time orphan cases and current-index composition; prior reconciliation time cannot retain an expired live row. |

### Review-loop-6 repair register

Each of the 19 routed findings has one disposition. Executed evidence is the 64 UTC/restart combinations, four partial-capture observation cases, 32 continued-authority refusals, 23 signed-request authority refusals, six request transaction/restart boundaries, six terminal-erasure authority refusals, four cleanup boundaries, bounded 141-failure/restart run and directed owning mutations in D12. Earlier registers and frozen runtime exclusions remain intact. Local models prove neither runtime behavior nor provider atomicity.

| Finding | Disposition and executed evidence |
| --- | --- |
| BHR6-01 | Repaired: pending intent fixes original state bytes/UTC; surviving writes acknowledge before current-time CAS. All four successor/finalize boundaries complete at distinct UTCs including 1001/2000, repeat without changes and refuse unavailable/contradictory evidence unchanged. |
| BHR6-02 | Repaired: acknowledged successor and finalized charge both reconcile/read back current expiry/deletion before completion. Before/at/after both deadlines preserve original claim/audit/response and once-only charge/invocation. |
| BHR6-03 | Repaired: bounded typed origin authenticates the original observed image and preparation while composing only monotonic provider-authenticated count/revision changes. Charge/object partial storage followed by one/three observations completes once without added charges. |
| BHR6-04 | Repaired: capture separately reserves/authenticates original and future repair interests. One-slot capacity refuses unacknowledged without leaked charge; two slots succeed, exact retry remains stable, both crash sides retain the same interests. |
| BHR6-05 | Repaired: fresh metadata/original interest/repair charge/repair interest and preparation checks precede every send. Independent missing/owner/generation mutations refuse unchanged; retained object/readback/locator guards remain exercised. |
| BHR6-06 | Repaired: required-to-repaired keeps the exact typed prerequisite indexed until record and entry deletion/readback complete. All four cleanup restart sides block send, unavailable cleanup preserves state, second corruption safely reuses the bounded slots. |
| BHR6-07 | Repaired: one fixed exact signed request, one typed request-bound attempt and one replacement deletion receipt for each slot bound actual persistence. 141 genuine failed redrives with persisted-only ledger/held restarts retain request payload at most 3 KiB and attempt at most 8 KiB with identical justified charges; 23 request-authority refusals, six staged/committed transaction boundaries, stale-count fencing, corruption/repair/cleanup, terminal deletion and exact refund execute. |
| BHR6-08 | Repaired: window tag 08 uses exact sorted unresolved tuple root. Independent decoded assertions cover accepted plus two unresolved and a singleton, with partition-corruption refusals retained. All affected known-answer dependencies were recomputed. |
| BHR6-09 | Implemented: ordinary and persisted invocation construction sort unsigned positions. Reversed equivalent partitions admit the same root/identity and keep unchanged drain-only claim bytes; independent incoming-order mutations fail. |
| BHR6-10 | Repaired: summary construction selects greatest local ordinal's last definitive result. A decoder-valid history with differing local maxima and Unknown counts completes actual closure/history/window/audit/state; historical-result-as-summary mutation fails. |
| ECR6-01 | Repaired by original-intent acknowledgement and the same later-UTC cases; progress/receipt links remain authentic across repeat restart, with unavailable/contradictory rows unchanged. |
| ECR6-02 | Repaired by the complete multi-local-attempt construction and exact [2,3,4] final summary ordinals. Missing earlier authority refuses while the complete set retains every registration/Unknown/result. |
| ECR6-03 | Repaired by both partial-capture storage sides after normal monotonic observations, persisted-only restoration and exact object/charge completion. Protected-field/observation-receipt changes refuse unchanged. |
| ECR6-04 | Implemented: released generation retains the original transfer owner. Staged-to-released-to-restaged/active is executable; owner-removal mutation fails, with existing owner/generation/predecessor and counter checks retained. |
| ECR6-05 | Repaired by persistent repair record/entry cleanup and deletion receipts before the next count/send, including restart at every cleanup phase and a second corruption. |
| ECR6-06 | Repaired by fixed addressed authority, authenticated reclamation and the 141-failure bounded row/byte/charge assertions; reclamation/log mutations fail. |
| VGR6-01 | Implemented: missing/changed preparation on either side and origin loss exercise the actual send gate. Removing the ledger comparison fails the owning refusal assertions. |
| VGR6-O1 | Implemented: preparation is a required typed authority, including the both-missing case. Removing that obligation fails; nullable equality alone cannot admit send/count. |
| VGR6-O2 | Repaired: the actual persisted addressed rows and deletion receipts are bounded independently of the 64-entry error tuple, including >130 failures/restarts and terminal erasure. |

### Review-loop-7 repair register

The seven authorized routed findings cover five root causes. D12 preserves all earlier literal answers, historical dispositions, registers and source-mutation intents. Queue evidence is persisted record/readback evidence in a local qualified-provider fixture; runtime/provider acceptance remains owned by the parent.

| Finding | Repair and executed evidence |
|---|---|
| BHR9-01 | D4/D11.3/D13 import the exact shared `command-execution-scope:` address over `U tenant || U executionMessageId`; scope inventory uses that address and does not invent a separator or private family. The shared literal and collision probes bind the import. |
| ECR9-12 | The one imported shared scope address is recomputed with the same canonical U framing; original digest-only literals are retained separately from complete physical addresses. |
| BHR9-02 | D8.1 declares bounded preparation/authority, both encoded owner manifests, immutable admission authority, current wait hash/counter, typed native receipts, exact 40 KiB ownership and precharged directory storage. Allocation/materialization/move/rollback/whole-tenant erasure survive fresh byte-only restart; caller conflicts, actual D7 ceilings and generation failures refuse with unchanged bytes. |
| BHR9-03 | One fixed 74 MiB predecessor source retains actual prior queue/index and intended owner bytes, current target hashes and allocator ticket. Replacement authenticates current source/readback; repair authenticates every owner/wait/charge/counter and recreates the exact installed version. Missing/changed/stale/unavailable authority refuses without clearing interests or refunding. |
| BHR9-04 | D1/D4/D11.2/D13 activate and reserve operational inventory before the first slice-2 scope gate. The slice-2 byte fixture indexes the full-shard gateway hold and shows hourly re-evaluation while binary publication remains inactive. |
| ECR9-13 | D11.3 maps every replacement K family to an exact literal physical prefix, including all heads and new queue authority/source/receipt slots. Fifty complete address vectors plus the imported shared address preserve unambiguous Unicode/variable framing and maximum identifier use. |
| ECR9-14 | Slice-2 admission verifies actual typed directory/index/reservation readbacks before creating the capacity hold; eleven missing/changed/unavailable or contradictory prerequisites stop readiness without mutation. |

### Review-loop-8 repair register

These 33 user-resolved repairs are implemented in the unsuffixed candidate and allowed bookkeeping. Parent acceptance passed; the three-layer review remains pending.

| ID | Implemented evidence |
| --- | --- |
| P1 | Own-bound/enum rejection causes, full carrier framing/limits, 19 independently framed addresses and directed guard relaxations. |
| P2 | Executed cleanup/current-completion increments and fixed asserted loop-6 metric tuple. |
| P3 | 64 distinct cases, all four boundaries with an actual authenticated expiry reconciliation, plus 1000 -> 1001 checks. |
| P4 | Typed membership resolution with fresh zero proof, unchanged pin, contiguous predecessor lineage, no reopening and exact lost acknowledgement. |
| P5 | Authenticated native deletion/readback before final refund; mid-delete and exhausted refund-generation preserve charge/discovery. |
| P6 | Own execution row selection/update preserves unrelated tenant hold rows across restart and completion. |
| P7 | Missing own successor row and staged-charge contradiction return unchanged resume_evidence_hold. |
| P8 | Repaired state excludes genesis and authenticates the exact required predecessor in decoder and cleanup. |
| P9 | Legacy signed claim ScopeOpHash/A8 zeros are enforced in both directions. |
| P10 | 128 KiB prior image on both codecs; closed projection, bounded reconciliation/history and field-derived maximum. |
| P11 | D-SPLIT remains open; removed premature integration-import claims from its current ledger entry. |
| P12 | Import literal answers/keys only; cite canonical committed candidate and exact block hashes/output; protected block remains historical. |
| P13 | Current prose, counts and handoff match the asserted executable coverage and current execution/review state. |
| P14 | Seven-field request maximum is 2,409 bytes with a 27-byte header and mutually exclusive scope/tenant alternatives. |
| P15 | Ordinary capture creates an active generation-1 retained-object charge. |
| P16 | Closed redrive_evidence_repair_hold reason and recomputed cleanup answer. |
| P17 | Refund-generation mutation targets refund-specific amount/deployment guard rather than reserve. |
| P18 | Unsigned signed-count mutant and AssertionError-only mutation kills; other exceptions fail verification. |
| P19 | Reserved overflow is zero; no undeployed imported-overflow producer or Admin field is claimed. |
| P20 | Historical-hash, AD-13 label and ApprovalScope checks have identifying failure messages. |
| P21 | Sixteen reminder deferrals have a separate pass-7 heading and explicit open statuses. |
| PD1 | Missing immutable response is a non-resumable indexed incident until tenant erasure or separately approved migration. |
| PD2 | C2 permits registration, at most one Unknown and definitive result; maximum 11,328 rows. |
| PD3 | Actual capsule/chunk/stored-event readbacks bind the signed exact manifest source and ordered stored-event root with zero window claim. Legacy restart completes on the same backend without window-authority imports; missing, contradictory, stale and unavailable authority holds unchanged. |
| PD4 | Combined exhaustion closes one window, raises the limit and binds exact constructible successor-window bytes in resolution tag 05. |
| PD5 | Charge tag 0f records transferred provenance; every transferred generation retains its owner, including released. The actual never-transferred legacy capsule/chunk resume-window charge also passes generation-2 released readback with marker 0 and owner absent; its 2 MiB metadata slot remains separately charged. |
| PD6 | Capture image resolves fixed-width identity/account hashes against authenticated current record, keeping escaped identifiers bounded. |
| PD7 | Repair prerequisite entry uses separate operational-evidence quota; side and native auxiliary budgets are bounded. |
| PD8 | Atomic identity-wide body/receipt reclamation at fixed deadline permits fresh same-key admission on the same backend. |
| PD9 | Completed rollback atomically compacts the bounded identity, frees the full slot and admits another identity on the same backend. |
| PD10 | Existing resume_capacity_hold represents an unresolved orphan admission fence. |
| PD11 | ResumeAttemptCollectionHold and QuotaGenerationIncident have exact reason/owner/subject/discovery/exit mappings. |
| PD12 | Origin tag 0b stores the original bounded envelope; compact signed-carrier readback preserves its exact bytes after origin deletion. |

### Review-loop-9 repair register

The user approved twelve current findings as eleven roots for bounded iteration 8. Each ID appears once below; only the two wait-state findings share a root. The six external reviewed rows remain parent-owned external triage and do not expand this candidate's authority.

| Finding | Current repair and actual verifier |
| --- | --- |
| BHR10-01 | D4's complete 5,270-byte maximum legacy envelope fits the 8 KiB cap and shard charge; maximum-width fields and exact charge fill/refund probes pass. |
| BHR10-02 | D7's complete enum/qualified-ID counter envelopes reach 2,176 bytes; every decoder/bootstrap/support envelope uses the coherent 4 KiB cap. D8 reserves complete bodies and native support. |
| BHR10-03 | D4's 1,136-byte maximum usage envelope fits the 2 KiB cap, charged once per shard and retained after authenticated row refunds. |
| BHR10-04 | D4's 2,219-byte maximum tombstone fits a 4 KiB cap/charge; replacement preserves the charge and authenticated expiry/deletion refunds it once. |
| BHR10-05 | D9 separates immutable admission root from authenticated monotonic current progress, resolves actual retained sources on restart, and preserves drain-only claims after accepted progress; successor admission uses the current unresolved subset. Missing/changed/stale/unavailable/regressing authority holds unchanged. |
| BHR10-06 | D9 success consumes the source and its active inventory in the persisted state transaction. Exact retry precedes hold preconditions; a new identity cannot reuse that source, while a fresh exhaustion source remains eligible. |
| BHR10-07 | Actual Ledger.reserve_pin_batch authenticates every candidate kind/length/capability/overhead/amount before aggregate reservation and refuses an oversize pin even with fitting aggregate counters; retained retry binds exact original rows. |
| BHR10-08 | D8 current queued/parked state is independent of the immutable admission carrier. Authenticated feasible rerender exits parked at the same ticket with both interests, current hashes and original receipt intact across restart/repair/cleanup. Shared root with the current wait-state edge finding below. |
| BHR10-09 | Actual D8 fresh owner/directory charges include authenticated o once; original capability provenance survives successor revisions, and counters/refunds preserve unrelated usage and original totals. Canonical directory support is 142 MiB + 32 KiB. |
| BHR10-10 | Actual destination_config consumes strict canonical exact-field UTF-8 JSON and binds outbox bytes, decoded metadata/count limits, duplicates and whole-document bounds, including independent exact 65,535/65,536/65,537-byte cases. |
| ECR10-01 | Actual tenant_turn requires authenticated current fit authority; false fit retains tenant residence and bytes while another fitting tenant progresses, and fresh true fit returns the original ticket with paired interests. |
| ECR10-02 | The same mutable current wait-state repair supports authenticated parked-to-queued exit and byte-identical still-infeasible parked-only turns. |

### Review-pass-11 implementation register

| Finding | Concrete repair and owning verifier evidence |
| --- | --- |
| BHR11-01 | Actual immutable admission coverage; `publication_closure_bytes` rejects missing/extra members, earlier accepted sends stay absent, and `verify_loop9_lifecycles` checks three successive windows plus persisted closure readback (13 closure cases). Successful-result preparation consumers additionally enforce the owned outcome before lookup (three positive/seven refusal cases). |
| BHR11-02 | `ScopeShardModel.typed` derives tenant/execution shard for every lifecycle direction; wrong-shard admission/migration/compaction/deletion preserves bytes (nine shard cases); tombstone codec validates its derived tag. |
| BHR11-03 | `resume_publication` binds direct window tenant/scope/window before new success; six rehashed mismatches refuse exactly as reconstruction does. |
| BHR11-04 | `authenticate_c2_groups` consumes actual registered source authority, legal same-send nonce retry and cumulative per-ID mapping; fourteen codec cases, one isolated valid-source/changed-closure-root case killing the retained historical root-binding fault, plus six retained native-source/restart/retention/erasure cases; iteration 10 runs the legal maximum separately. |
| BHR11-05 | Current authenticated all-accepted progress refuses new success; consumed source refuses while existing exact retry remains unchanged (four eligibility cases). |
| BHR11-06 | Bounded parallel invocation owner/hash sequences reconcile at original deadlines; 66 lifetime successes and next interrupted persisted preparation plus exact once-only typed-body reclamation (73 lifetime cases). |
| BHR11-07 | `Ledger.authenticated_limits` supplies every aggregate reservation bound; stale larger caches, all account/pool limits and unavailable/changed authority are byte-identical refusals (ten limit cases). |
| BHR11-08 | Complete `hold_owners` mapping is consumed by real entry decoding; all 19 kinds × seven owners execute valid-owner and wrong-owner cases (133 cases). |
| BHR11-09 | Activation decoder and route consumers validate checked count × 8,192 before thresholds; eleven codec consistency/missing/overflow/threshold cases preserve complete-history exit behavior. |
| BHR11-10 | Actual imported required scope admission, expiry-qualified migration, authenticated compaction/predecessor/obligation readback, count transfer with unchanged 4 KiB, lost-ack retry, HTTP 410, deletion/refund/erasure (13 compaction cases). |
| ECR11-01 | Successor and progress generations/receipt writes stage together; u64 maximum/adjacent plus unavailable-head cases preserve both durable heads (six atomic cases; four isolated original pending-intent writes, four later reconciliations, eight native-owner refusals and one missing-head refusal). |

### Review-pass-12 iteration-10 implementation register

| Finding | Concrete repair and owning verifier evidence |
| --- | --- |
| BHR12-01 | `compact_rolled_back_preparation` authenticates and decodes the current state head, applies one bounded tombstone delta, and CASes that head. Seven rollback cases cover a later expiry CAS, live/expired/deleted unrelated identities, authenticated but changed live and tombstone index rows, u64 generation refusal and 64-row capacity refusal; all refusals preserve backend bytes. |
| BHR12-02 | `PreparationStore.turn` stages completion through charge release and progress reconciliation, publishing the staged backend only after the whole turn succeeds. Two near-u64 progress heads refuse finalization with unchanged native backend bytes and charges. |
| BHR12-03 | `ScopeShardModel.change` accepts an expired legacy-to-legacy renewal only with exact predecessor, closed-obligation proof, unchanged cohort/input/cutover, next UTC/expiry/generation, and unchanged 8 KiB shard use. The direct renewal/restart case also refuses expired exact retry and changed input. |
| BHR12-04 | Account usage, native generations, predecessor receipts and counter keys include account kind. A legal identical 64-hex tenant/capture-scope ID carries independent 900 MiB and 200 MiB usage; refund leaves tenant authority unchanged. The counter codec admits explicit `capture-scope` kind, and the kind-width boundary passes. |
| BHR12-05 | `capture_delivery` authenticates fresh object/locator/charge readbacks before reservation; all failed fresh readbacks retain the full ledger image including generations. Partial-charge refund requires authenticated object absence as well as capture absence; two focused capture probes cover the matrix and charge-only rollback refusal. |
| BHR12-06 | Resume preparation stores the exact authenticated predecessor bytes and original predecessor UTC across write, persisted-only restart and audit hash; later request UTC cannot substitute for prior bytes. Four cases cover earlier/equal UTC success and changed-byte/later-UTC refusal. |
| BHR12-07 | The bounded lifetime probe performs 67 actual admissions, compactions and deadline reclamations against one retained backend, checking native receipts/bodies and one persisted-only crash/restart after the 67th admission. The drain-limit resolution row participates in identity cleanup. |
| BHR12-08 | The maximum C2 probe builds 59 distinct 1,024-byte MessageIds with 3,776 legal nonce attempts and 11,328 observations, authenticates the complete closure and preparation through a persisted-only restart, and checks preparation stays within 2 MiB. |
| VGR12-01 | The actual held-delivery matrix now refuses invalid or overflow accounting before scheduling under both incremental capability values. The new owning guard mutation makes the True case fail. |

The historical `capture skips object readback` tuple remains byte-identical. Its owning assertion now runs at the pre-reservation readback boundary, where it also proves ledger generations remain unchanged. The historical `loop6 acknowledged successor skips expiry` fault retains its name and `pass` mutant but targets `staged_final.reconcile_successor(now)` in the staged finalization path, replacing its obsolete `self.reconcile_successor(now)` source target. The original pending-intent later-UTC completion assertion still owns the mutant. The eleven new directed faults are one each for rollback, finalization, renewal, account-kind aliasing, partial capture refund, predecessor UTC, durable compaction, missing maximum C2 attempt, duplicate maximum MessageIds, incremental invalid-accounting scheduling, and accepting an authenticated changed current index. No runtime/provider atomicity is inferred from these local probes.

D12 retains all 164 historical directed source fault identities and effective assertions, with the one explicit staged-call target mapping above, and adds eleven iteration-10 owning faults (175 total). The historical `capture skips object readback` fault keeps its name and replacement tuple but is now owned by the earlier fresh-readback assertion. These executed codec/local transition probes qualify local models; provider transaction/authentication guarantees remain gated by AD-13. Parent acceptance and formal three-layer review remain outstanding; this register changes no workflow or approval state.

### Review-pass-13 iteration-11 implementation register

| Root / finding | Concrete repair and owning verifier evidence |
| --- | --- |
| R13-01 / BHR13-11 | D11.1 writes one addressed, generation/predecessor/receipt-bound `HX-EV-HELD-DELIVERY-4` row with the signed request, typed attempt and both predecessor-deletion receipts in one detached serializable model transaction. Before-publication crashes and missing/stale readback preserve every backend byte/charge; a committed lost acknowledgement reconstructs count/state and the pair from typed native records and retained authority. `verify_loop11_repairs` checks seven initial unchanged refusals, persisted-only restart, exact retry and once-only count; its held-CAS omission fault fails the owning assertion. The 32 KiB metadata ceiling includes origin, preparation, held row, 64 errors, native head and cleanup/completion. |
| R13-02 / BHR13-12 | D11.1/D11.2 persist the separately addressed repair record and prerequisite entry, plus authenticated typed `HX-EV-HOLD-INDEX-2` scope-index membership at the D11.2 address, before exposing the captured hold. Three precommit stage crashes leave the backend unchanged; required lost-ack retry reads the same bytes. A second carrier genuinely retains a distinct prerequisite on the same backend/index; the typed predecessor hashes the prior typed bytes and exact sorted rows survive cleanup of the first carrier. Missing, changed, wrong-owner or unavailable record/entry/index evidence stays no-send. Record-write and index-membership omission faults fail their owning assertions. |
| R13-02 / BHR13-13 | Required-to-repaired CAS uses exact count/owner/carrier/locator/predecessor/native receipt. Repaired lost-ack retry reads the same record. Four persisted-only cleanup boundaries reconstruct from the compact addressed phase row and surviving record/entry or native deletion receipts; tampering either receipt refuses unchanged. Cleanup CAS-removes and reads back the exact typed index membership before clearing the held prerequisite, compacts receipt slots to a fixed completion fence, and then permits one next send. Record deletion-readback and index-removal faults fail their owning assertions. |

The five loop-11 source faults supplement all 175 historical fault identities. The local model does not prove a DAPR/provider transaction or broker delivery guarantee; Story 6.5 must qualify the provider before activation. D-SPLIT remains open, sprint in-progress and AD-13 `UNAPPROVED`.

## Protected-path and source-integrity verification

This block proves that the unapproved parent/children remain unchanged and that this story edits only its candidate and bookkeeping artifacts, with unrelated concurrent changes pinned to the checkpoint below.
The approved review baseline stays `01498ac7`. On 2026-10-01 the user first approved checkpoint `6dededdecd62dd6dc6d1f15810108d860ec70c8f`, then exact pins and the `.gitmodules` registration image. After review pass 7 they approved the current external state at commit `d9504aa0c72ba578f9bd1f2bf1396d3018b77dc7` as the external-workspace checkpoint. That checkpoint accounts for exactly 26 external paths, not a blanket exception:

- 16 reminder documentation, source and test paths, including the new `src/Hexalith.EventStore.Client/Reminders/IReminderIntentSource.cs`;
- the three committed `review-6-5d-loop6-*-standalone.md` review-prompt copies;
- the `.gitmodules` blob `c62b48798894bb3576f02fdf4ebb8c552756e35b`;
- six gitlinks: Builds `21ce044ab465ccb2adab58b3d66e394ffbecf3c2`, Commons `c13dc6679aa91144b6d541078f3f20019d79c2eb`, FrontComposer `b6a4536fc12b64927ad6dfbc46a5f45c8b7f229e`, McpCli `7e3226ba612a3e7fb3a8969c4197a8f1e4c0c2ed`, Platform `7c2f0f89f29c79f5d7ab4b731155e2cb07fc690f` and Tenants `3d7c07363d7a06a24cbba0d777899b2071b02f06`.

On 2026-10-01 the user approved the exact replacement checkpoint `ed11fd62eb736af6cf28a80654cf421f0764e03e` and its 128-path manifest (SHA-256 `06eb14c433039b7e039d7349a9d9981d0f7aa028450a6ba317ed5ef1f1458261`). They subsequently approved checkpoint `8096455e4f23f2912998e36738058b8e3d961be6` and its exact 137-path manifest (SHA-256 `a7628d2a3b9feb2e6dc636350232b071caa12ac7c54e4ae0f207474d627843f3`) together with bounded iteration 7. On 2026-10-02 the user approved the exact replacement checkpoint `2c58ffda41759e895ace4b9625c9bd931a217672` and its 156-path manifest (SHA-256 `1a5039792adeec971c5452cfa5c412b259ba7e7c09618ccb9fdbb22b8e75c300`). It preserves the preceding 137 paths, adds nineteen, and authenticates changed committed bytes at eleven existing paths. The current gate pins 149 external regular files, the unchanged `.gitmodules` registration blob and the same six gitlinks above. The exact regular-file set is enumerated in `checkpoint_paths` below; each path must match its committed checkpoint bytes. This replaces only the gate checkpoint after concurrent Story 6.1-P2 evidence/source changes and preserves every external file, index entry and submodule checkout. The approved review baseline and four protected specification hashes remain unchanged.

Each gitlink must match the checkpoint tree entry, the root index entry and the clean checkout HEAD. The checkpoint, index and worktree must all authenticate the full `.gitmodules` blob, and the McpCli and Platform registrations and URLs must be exact. Every other committed external file must remain byte-identical to the checkpoint. Any new path, content, pin or worktree drift fails. No external path, index, dependency or Git history is modified, and the four specification hashes and approval checks remain pinned.

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
assert sha256(historical).hexdigest() == pins['_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md'], 'historical AD-13 hash'
parent = (root/'_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md').read_text(encoding='utf-8')
for label in ['ApprovalDigest','Approver','ApprovalDateUtc','Authorization','ApprovalEvidence']:
    assert f'{label}: UNAPPROVED' in parent, ('AD-13 approval label',label)
assert 'ApprovalScope: Story 6.5 AD-13 normative artifact' in parent, 'AD-13 approval scope'
allowed = {
    '_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md',
    '_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission-2.md',
    '_bmad-output/implementation-artifacts/deferred-work.md',
    '_bmad-output/implementation-artifacts/sprint-status.yaml',
}
changed = set(subprocess.check_output(['git','diff','--name-only','01498ac721db7c44f18fcf9591ffbbf30ba245e2']).decode().splitlines())
untracked = {line[3:] for line in subprocess.check_output(['git','status','--porcelain']).decode().splitlines() if line.startswith('?? ')}
checkpoint = '2c58ffda41759e895ace4b9625c9bd931a217672'
checkpoint_paths = {
    '_bmad-output/implementation-artifacts/6-1-p2-query-security-projection-capability-acceptance-record.md',
    '_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/README.md',
    '_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/consumer/Consumer.csproj',
    '_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/consumer/NuGet.Config',
    '_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/consumer/PublishedApiSmoke.cs',
    '_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/public-packages.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verify_public_packages.py',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/admin-denial.ctrf.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/admin-denial.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/admin-denial.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/client.ctrf.json.gz',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/client.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/client.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/client.xml.gz',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-blockers.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-build-initial.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-build-initial.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-build.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-build.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-full.ctrf.json.gz',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-full.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-full.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-p2.ctrf.json.gz',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-p2.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-p2.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-p2.xml',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/diff-check.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/diff-check.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/final-solution-build.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/final-solution-build.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/g4-runner.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/g4-runner.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/live-sidecar-build-initial.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/live-sidecar-build-initial.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/live-sidecar-build.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/live-sidecar-build.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/manifest-before-rebuild-proof-correction.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/manifest.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/manifest.sha256',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/matrix-tests-after-rebuild-proof-correction.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/matrix-tests.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/process-restart.ctrf.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/process-restart.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/process-restart.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/process-restart.xml',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/projection-rebuild.ctrf.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/projection-rebuild.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/projection-rebuild.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/projection-rebuild.xml',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/public-signatures.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/public-signatures.txt',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/query-routing.ctrf.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/query-routing.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/query-routing.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/query-routing.xml',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-build-rerun.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-build-rerun.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-build.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-build.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-diff-check.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-diff-check.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-process-restart-rerun.ctrf.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-process-restart-rerun.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-process-restart-rerun.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-process-restart-rerun.xml',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-process-restart.ctrf.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-process-restart.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-process-restart.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-process-restart.xml',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/reminder-fixture-build.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/reminder-fixture-build.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/reminder-fixture-rerun.ctrf.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/reminder-fixture-rerun.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/reminder-fixture-rerun.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/reminder-fixture.ctrf.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/reminder-fixture.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/reminder-fixture.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/secret-guard-rerun.ctrf.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/secret-guard-rerun.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/secret-guard-rerun.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/secret-guard.ctrf.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/secret-guard.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/secret-guard.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/server-full-rerun.ctrf.json.gz',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/server-full-rerun.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/server-full-rerun.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/server-full.ctrf.json.gz',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/server-full.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/server-full.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/server-p2.ctrf.json.gz',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/server-p2.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/server-p2.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/server-p2.xml.gz',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/solution-build.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/solution-build.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/solution-restore.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/solution-restore.log',
    '_bmad-output/implementation-artifacts/review-6-5d-loop6-blind-hunter-standalone.md',
    '_bmad-output/implementation-artifacts/review-6-5d-loop6-edge-case-hunter-standalone.md',
    '_bmad-output/implementation-artifacts/review-6-5d-loop6-verification-gap-standalone.md',
    'docs/guides/configuration-reference.md',
    'docs/guides/typed-reminders.md',
    'src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj',
    'src/Hexalith.EventStore.Client/Reminders/IReminderIntentSource.cs',
    'src/Hexalith.EventStore.Client/Reminders/ReminderDelegationRequest.cs',
    'src/Hexalith.EventStore.Contracts/Reminders/ReminderIntent.cs',
    'src/Hexalith.EventStore.DomainService/EventStoreReminderOptions.cs',
    'src/Hexalith.EventStore.DomainService/EventStoreReminderServiceCollectionExtensions.cs',
    'src/Hexalith.EventStore.DomainService/ReminderActor.cs',
    'src/Hexalith.EventStore.DomainService/ReminderCoordinator.cs',
    'src/Hexalith.EventStore.DomainService/ReminderDispositionRecord.cs',
    'src/Hexalith.EventStore.DomainService/ReminderEntry.cs',
    'src/Hexalith.EventStore.DomainService/ReminderFailClosedException.cs',
    'src/Hexalith.EventStore.DomainService/ReminderIntentIndex.cs',
    'src/Hexalith.EventStore.DomainService/ReminderLog.cs',
    'src/Hexalith.EventStore.DomainService/ReminderReconciler.cs',
    'src/Hexalith.EventStore.DomainService/ReminderReconciliationPass.cs',
    'src/Hexalith.EventStore.DomainService/ReminderRuntimeStatus.cs',
    'tests/Hexalith.EventStore.Contracts.Tests/Queries/ProjectionAdapterContractTests.cs',
    'tests/Hexalith.EventStore.Contracts.Tests/Reminders/ReminderIdentityCodecTests.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/EventStoreReminderCompositionTests.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/Fixtures/DispositionFailingReadModelStore.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/Fixtures/FakeReminderIntentSource.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/Fixtures/FakeReminderScheduler.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/Fixtures/FakeTrustedEffectSubmitter.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/Fixtures/ReminderDiagnosticLogger.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/Fixtures/ReminderEnvironmentCollection.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/Fixtures/ReminderTestHarness.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/ReminderCallbackAdmissionTests.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/ReminderCoordinatorTests.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/ReminderDiagnosticsTests.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/ReminderReconcilerTests.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8DiagnosticRecordingLogger.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8DiagnosticResponseHandler.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8DiscoveryConfiguration.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8DiscoveryConfigurationTests.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8HostingStartup.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8InvocationDiagnosticHandler.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8InvocationDiagnosticHandlerTests.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8OwnedContainerLaunch.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8OwnedContainerLaunchTests.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8QualificationOverrides.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8QualificationOverridesTests.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ProjectionWatermarkProcessHandler.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ProjectionWatermarkProcessRestartTests.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ProjectionWatermarkProcessWorkerTests.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ReminderRecoveryLiveSidecarTests.cs',
    'tools/validate-oq8-platform-evidence.py',
}
submodule_pins = {
    'references/Hexalith.Builds': '21ce044ab465ccb2adab58b3d66e394ffbecf3c2',
    'references/Hexalith.Commons': 'c13dc6679aa91144b6d541078f3f20019d79c2eb',
    'references/Hexalith.FrontComposer': 'b6a4536fc12b64927ad6dfbc46a5f45c8b7f229e',
    'references/Hexalith.McpCli': '7e3226ba612a3e7fb3a8969c4197a8f1e4c0c2ed',
    'references/Hexalith.Platform': '7c2f0f89f29c79f5d7ab4b731155e2cb07fc690f',
    'references/Hexalith.Tenants': '3d7c07363d7a06a24cbba0d777899b2071b02f06',
}
registration_paths = {'.gitmodules'}
gitmodules_blob = 'c62b48798894bb3576f02fdf4ebb8c552756e35b'
assert subprocess.check_output(['git','rev-parse',f'{checkpoint}:.gitmodules']).decode().strip() == gitmodules_blob, 'checkpoint .gitmodules blob'
assert subprocess.check_output(['git','ls-files','--stage','--','.gitmodules']).decode().split() == ['100644',gitmodules_blob,'0','.gitmodules'], 'staged .gitmodules blob'
assert subprocess.check_output(['git','hash-object','--no-filters','.gitmodules']).decode().strip() == gitmodules_blob, 'worktree .gitmodules blob'
assert (root/'.gitmodules').read_bytes() == subprocess.check_output(['git','show',gitmodules_blob]), '.gitmodules bytes'
added_registrations = {
    'references/Hexalith.McpCli': ('Hexalith.McpCli','https://github.com/Hexalith/Hexalith.McpCli.git'),
    'references/Hexalith.Platform': ('Hexalith.Platform','https://github.com/Hexalith/Hexalith.Platform.git'),
}
for name,(section,url) in added_registrations.items():
    assert subprocess.check_output(['git','config','--file','.gitmodules','--get',f'submodule.{section}.path']).decode().strip() == name, ('registration path',section)
    assert subprocess.check_output(['git','config','--file','.gitmodules','--get',f'submodule.{section}.url']).decode().strip() == url, ('registration url',section)
declared_rows = subprocess.check_output([
    'git','config','--file','.gitmodules','--get-regexp',r'^submodule\..*\.path$'
]).decode().splitlines()
declared_paths = {row.split(None,1)[1] for row in declared_rows}
assert set(submodule_pins) <= declared_paths, sorted(set(submodule_pins) - declared_paths)
external_paths = checkpoint_paths | set(submodule_pins) | registration_paths
assert len(external_paths) == 156, len(external_paths)
subprocess.check_call(['git','merge-base','--is-ancestor',
                       '01498ac721db7c44f18fcf9591ffbbf30ba245e2',checkpoint])
checkpoint_changes = set(subprocess.check_output([
    'git','diff','--name-only','01498ac721db7c44f18fcf9591ffbbf30ba245e2',checkpoint
]).decode().splitlines())
assert checkpoint_changes - allowed == external_paths, sorted((checkpoint_changes - allowed) ^ external_paths)
for name in sorted(external_paths):
    if name.startswith('references/'):
        entry = subprocess.check_output(['git','ls-tree',checkpoint,'--',name]).decode().split()
        expected = submodule_pins[name]
        assert entry == ['160000','commit',expected,name], ('checkpoint gitlink',name,entry,expected)
        actual = subprocess.check_output(['git','-C',name,'rev-parse','HEAD']).decode().strip()
        assert actual == expected, ('checkout',name,actual,expected)
        staged = subprocess.check_output(['git','ls-files','--stage','--',name]).decode().split()
        assert staged == ['160000',expected,'0',name], ('root gitlink',name,staged,expected)
        assert not subprocess.check_output(['git','-C',name,'status','--porcelain']), ('dirty submodule',name)
    elif name not in registration_paths:
        expected = subprocess.check_output(['git','show',f'{checkpoint}:{name}'])
        assert (root/name).read_bytes() == expected, ('checkpoint content',name)
external_drift = set(subprocess.check_output([
    'git','diff','--name-only',checkpoint,'--',*sorted(external_paths - set(submodule_pins) - registration_paths)
]).decode().splitlines())
assert not external_drift, sorted(external_drift)
story_changed = changed - external_paths
assert story_changed | untracked <= allowed, sorted((story_changed | untracked) - allowed)
print(f'protected-path verifier: {len(pins)} hashes, AD-13 UNAPPROVED, {len(story_changed | untracked)} allowed paths, {len(external_paths)} pinned external paths')
PY
```

## Verification expectations

Run the three fenced `bash` blocks for candidate acceptance. Expected codec output: 30 original/twelve supplementary/five loop-7 answers, 47 digest probes, nineteen digest keys, 51 physical addresses, 230 malformed and 144 semantic rejections. Expected lifecycle output: 14 statuses, four matrix rows, 27 invariants, 180 rejected source mutations, 54 historical dispositions, ten transition matches/50 malformed rejections, 46 persisted restart/four cleanup boundaries, 70 evidence refusals, three current-time completions, 21/20/21/19 earlier repair IDs and 33 pass-8 IDs and seven loop-7 IDs and twelve iteration-8 repair IDs. Loop 7 asserts 140 persisted queue restarts, 262 holding-counter moves, 28 authority/rerender/repair/erasure refusals, four cleanup boundaries, two exact repairs, eight generation boundaries, five encoded-quota refusals and eleven slice-2 readiness refusals. Loop 6 asserts `(utc,partial_capture,redrive_refusals,request_refusals,request_restarts,erasure_refusals,cleanup) = (64,4,32,23,6,6,4)` and 141 genuinely failed redrives with bounded signed request/attempt rows and terminal erasure. Pass 8 also verifies atomic rollback compaction and deadline reclamation on the same backend, unrelated row preservation, retained original signature authority and refund-last interruption/refusal. Iteration 8 asserts complete widths 5,270/1,136/2,219/2,176 bytes, 65 ASCII/UTF-8 field-boundary cases, five admission/progress codec cases, 47 destination cases and 52 codec refusals. Its focused lifecycle probes assert ten pin, six shard, 35 resume and 19 queue cases with 45 refusals, plus 17 directed guard mutations and the twelve iteration-8 repair IDs. The new metrics count executed assertions separately from the preserved historical totals. Iteration 9 additionally executes codec metrics `{owner_cases:133, activation_cases:11, c2_cases:14, closure_root_cases:1}` and lifecycle metrics `{closure:13, shard:9, binding:6, eligibility:4, lifetime:73, limits:10, compaction:13, atomic:6, c2_native:6}`, with thirteen owning new faults and eleven unique pass-11 IDs. Iteration 10 adds `{rollback:7, finalize:2, renewal:1, account:1, capture:2, predecessor:4}`, 67 shared-backend successes and a separate maximum C2 recovery `{members:59, attempts:3776, observations:11328, prepared_bytes:4494}`; eleven owning faults bring the historical total to 175; five loop-11 owning faults bring the candidate total to 180. Loop-11 focused metrics are `{refusals:18, cleanup_boundaries:4, count:2}`, with provider-only reconstruction, a genuine shared-backend two-carrier prerequisite, three precommit repair-stage aborts, exact required/repaired retries, native receipt refusals and once-only next send. Separate pending-intent metrics are `{intent_cases:4, original_write_cases:4, later_reconcile_cases:4, refusals:8, head_absence_cases:1}`: a later-UTC restart first preserves the exact recorded state/hash/UTC at coupled successor-write, then composes current expiry at finalize. The original later-UTC historical fault and directly related expiry/native-readback/head-absence faults execute these owning probes without altering historical totals. Owned-preparation consumer metrics are `{positive_cases:3, refusal_cases:7}`. Every successful-result preparation lookup first requires `orphaned-success`, the exact owned orphan and typed preparation bytes; a refused producer result fails that owning assertion before dereference. The late multi-local-attempt consumer and adjacent successful-result consumers use the same guard. Historical result-summary fault 86 therefore fails by AssertionError rather than an incidental missing-key exception; all historical targets and totals remain unchanged. The isolated closure-root case preserves the original complete-set bytes and authenticated C2 readbacks while changing only closure tag 08; the retained historical root-ignored fault must fail this owning assertion. Outer mutation progress reports each 20 successful kills. Only tombstone shard and preparation-owner fixture dependency bytes change; all 47 family labels, nineteen digests and 51 addresses remain represented. The protected gate still requires four exact hashes, the approved 156-path checkpoint and AD-13 `UNAPPROVED`; new external drift remains a failed gate until explicit checkpoint authorization. Story 6.5 uses the citation/literal-import procedure in D13.4, not ported verifier code.

Historical session evidence remains preserved: `/tmp/verify-6-5d-loop6-independent.mjs` SHA-256 `529d8cfc772a58b5dcad2e28908d3961ae1351fa8d697db84fd5a6b504186527`, and `/tmp/export-6-5d-loop5-codec-fixtures.py` SHA-256 `b54a811e9fe64777d90473c353a438fc46e5c2fd5c79393b140d554a1c53f865`. For historical pass-8 repairs, the parent independently recomputed all 42 lengths/hashes, nineteen keys, affected dependency roots and ten durable fixtures plus the transferred-marker charge using `/tmp/bmad-6-5d-parent-independent-pass8.mjs` SHA-256 `a3694bfc265a478f37a0b6f3157e406663116215f42ddd3d748061a9fd4d6628` with that preserved exporter. These are session-local evidence files, uncommitted and unavailable after host restart; the candidate's fenced blocks remain the repeatable source evidence. Local bytes/transitions do not prove provider atomicity. Run `python3 scripts/check-deferred-work.py`, frozen-intent comparison and `git diff --check`. The execution record owns the current build status: D-SPLIT is open, sprint is in-progress, and iteration 11 candidate acceptance is in progress. No local result changes AD-13 approval or authorizes Story 6.6.
