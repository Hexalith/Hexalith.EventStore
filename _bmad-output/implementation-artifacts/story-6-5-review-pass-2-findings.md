# Story 6.5 review pass 2: raw findings (input to Story 6.5d)

These are the raw, untriaged findings from review pass 2 (2026-09-30) of the Story 6.5 loop-1 re-derivation. Line numbers refer to the uncommitted AD-13 candidate whose whole-file SHA-256 is `c474df76a687b2798757051efbdb382bb6b9650f7bd7a7637d0c67bfc5e7b715`. Three layers reviewed it: Verification Gap (VG2), Blind Hunter (BH2) and Edge Case Hunter (E2).

After this pass the owner chose to split the integration-invented mechanisms into a focused child story, Story 6.5d; see `spec-6-5-event-versioning-and-upcasting-spec.md`, decision D-SPLIT. Only E2-34 was verified by the orchestrator: C5 closure step (2) (`:1070`) fences the whole operation namespace, while `[I-45]` scopes that same closure to window-w send IDs without amending C5 in place. Story 6.5d triage must verify every other claim before acting on it.

## Verification Gap (VG2)

Each gap was demonstrated by a mutation that passes all five bash blocks.

- **VG2-1:** The `[I-11]` transition table at `:2617-2623` is hand-typed. Unfreezing accepted rows while keeping 9 entries still passes. Fix: derive `ALLOWED` from the pinned 6.5a K09 `reduce_set` over all 16 pairs.
- **VG2-2:** The capacity model never exercises the `[I-30]` `unidentifiedCaptureCeiling`. Removing the ceiling still passes.
- **VG2-3:** The status model has no asserts for the drain-limit resume exit (pending plus drain limit with the window open) and returns no `RecoveryReasonCode`, so the (4b) precedence goes unchecked.
- **VG2-4:** The queue model has no per-tenant queue case and no case where a deployment-queue head is refused by its tenant.
- **VG2-5:** The B-series check is a substring match (`digest in b_block`), so swapping two labels' digests still passes.
- **VG2-6:** C1's inline BH37-9 and `destinationId` known answers (`:802`, `:824`) are never compared with the 6.5c model.
- **VG2-7:** Verification command 1 never checks the block count. Changing one fence to `python` drops a verifier and still reports success.
- **VG2-O1:** The queue model at `:2663-2698` contradicts `[I-31]`. It uses one shared tenant queue, never moves a head from the deployment queue to a tenant queue, and has no 50,000-entry limit and no parking.
- **VG2-O2:** For pending or unknown heads, the status model holds on `exhausted`. `[I-10]` case (3) holds such heads only under a drain-limit record, and `[I-45]` makes them ineligible for resume.
- **VG2-O3:** The drain-limit exit is ambiguous. A resume that only raises the limit closes no window, so the drain-limit record still binds the latest head.

## Blind Hunter (BH2)

- **BH2-1:** A resume request cannot be built.
  - Nothing a caller can read exposes `expectedHoldSourceHash`, the ScopeOpHash, the head hash, the next ordinal or the predecessor audit hash.
  - For a legacy record, tag `06` must name the `[I-46]` reconciliation record, but that record is keyed by the request's own ordinal, so the request is circular.
- **BH2-2:** The deployment scope's reserved name `deployment` is itself a legal tenant ID, so the `HoldInventoryActor` IDs and `/holds/{tenantId}` routes collide. The redrive route also has no tenant value to use for deployment-scoped entries.
- **BH2-3:** The `[I-06]` activation record has three problems:
  - it has no disposition for routes that stay below the bound;
  - `incremental` is circular, because it changes the fingerprint the record binds;
  - it never says what `hold` means at activation.
- **BH2-4:** Pin-capacity charging can deadlock. Pins are charged one at a time and partial pins keep their charges, while refunds happen only after final closure. There is no atomic batch reservation and no deadlock detection.
- **BH2-5:** `FirstSendMembershipChangedHold` projects case (3) with `Retry-After: 1`, which means clients poll every second indefinitely. Its exit is undefined in AD-13, and it is excluded from resume.
- **BH2-6:** `outcome_evidence_conflict` and `outcome_evidence_hold` have no exit, and `[I-45]` excludes every `CommandOutcomeHold` cause.
- **BH2-7:** `[I-46]` legacy resume may have no source to reconcile from:
  - drain exhaustion removes `UnpublishedEventsRecord` (`AggregateActor.cs:2925`);
  - dead letters above `MaxBodyBytes` are unretainable, and operators may skip or archive them;
  - a first-failure status-6 record may have no drain record at all.

  There is also no fence against the Admin dead-letter retry replaying the same message.
- **BH2-8:** Resume charges are unbounded:
  - an audit record plus a 4 KiB charge is written for every request, including rejections;
  - legacy charges are released only at erasure;
  - the drain-limit record reservation grows without bound once resumes raise the limit;
  - the next drain-limit record is missing from the charge.
- **BH2-9:** Under `[I-12]`(5), one per-tenant usage record is part of every admission's CAS. The loser of a lost race has no stated outcome, there is no throughput bound, and the legacy checks never sunset.
- **BH2-10:** Hold-inventory entries raised by the gateway (for example `ScopeRetentionCapacityHold` and `admission_evidence_hold`) are never re-evaluated or removed. The subject key is unstated. Entries pile up to the 10,000 cap, after which `overflowCount` hides real holds.
- **BH2-11:** The ledger status `dispositioned pending approval` is not a recognised status. The reopen rule has no trigger or owner, and O-01..O-20 are tracked nowhere.
- **BH2-12:** The story spec's re-planned tasks and ACs dropped the named-approval completion condition. The loop-1 Spec Change Log "Amendment: pending" line is stale, and the D-RESUME frozen change is not recorded in the change log.
- **BH2-13:** The re-derivation base exists only in the scratchpad, so the base cannot be reproduced after the session ends.
- **BH2-14:** `[I-47]` departs from D-CLOSE without owner sign-off: it closes ledger :5110/:5114/:5189 by rule and withdraws O-06/O-07/O-11. It also removes the only automated check on the documentation-only boundary.
- **BH2-15:** The `[I-41]`(3) release hold has no hotfix or security-release path while Story 6.6 is in progress.
- **BH2-16:** Same as VG2-O2.
- **BH2-17:** The 22 integration answers are produced and checked by the same Python code, with no independent recomputation. The B-series substring problem is VG2-5.
- **BH2-18:** Several records have no codec or known answer:
  - the `[I-28]`/`[I-29]` per-object charge records and counters;
  - the `[I-23]` terminal quarantine entry;
  - the `[I-37]` ordered index and `overflowCount`.
- **BH2-19:** From slice 2, legacy admission has a new failure mode when the claim read or CAS is unavailable. Its outcome is unstated, and the change is not in §10.2.

## Edge Case Hunter (E2)

- **E2-1:** `[I-14]` drain-limit records are create-once per key. A later head with unresolved members finds the key already taken, so no new record and no new hold are created.
- **E2-2:** A crash after the last reserved drain but before the drain-limit record is created leaves nothing to re-drive the creation.
- **E2-3:** An `[I-11]` conflict while the head maps to pending or unknown projects case (3) with `Retry-After: 1`, which polls forever and leaves the conflict invisible.
- **E2-4:** A legacy resume sourced from a `DeadLetterMessage` has no `IsRejection` field, so a drain success writes `Completed` for a rejection.
- **E2-5:** Same as BH2-7: legacy status-6 records with no source stay unpublished permanently, while BC-02 answers 409.
- **E2-6:** Legacy resume after the status record has expired, or after a MessageId was reused across aggregates, cannot address the drain actor or the right range.
- **E2-7:** Same as BH2-1.
- **E2-8:** A non-admissible carrier on a subscription in unbounded-redelivery mode (a) is redelivered forever with no capture.
- **E2-9:** A dead-lettered carrier above 193 MiB keeps returning non-2xx and never gets an index entry.
- **E2-10:** Same as BH2-2's redrive part.
- **E2-11:** Same as BH2-2's name collision.
- **E2-12:** Same as BH2-10: gateway-owned entries are never removed.
- **E2-13:** Inventory actors spread across replicas. The reconciler has no tenant list, so the gauge under- or double-counts.
- **E2-14:** An existing entry whose cause changes keeps its stale tag `05` reason code.
- **E2-15:** Parked waits cannot be found in a key-only store, so they never re-enter the queue.
- **E2-16:** The single CAS is impossible when the pin store's backend differs from `publicationRetentionBackend`.
- **E2-17:** The queue record's tag `03` count and the u32 count inside tag `04` can disagree, and no rule decides which wins.
- **E2-18:** Tombstones fill `scopeRetentionCeiling`, and neither the compaction owner nor its trigger is named.
- **E2-19:** Same as BH2-9: the ETag race on the usage record.
- **E2-20:** Status inspection that finds a compacted tombstone has undefined behavior.
- **E2-21:** Same as BH2-19: legacy admission when the store is unavailable.
- **E2-22:** Same as BH2-3: routes below 75% have no disposition.
- **E2-23:** A held stream that receives no new events after the incremental capability is registered keeps a stale `LegacyArrayLimit` entry.
- **E2-24:** If the Rendering owner crashes after writing one of the two outputs, the `response_preparation_hold` exit is unreachable.
- **E2-25:** A resumed operation that reaches its raised drain limit again writes a record that falls outside A8's reservation.
- **E2-26:** Same as BH2-8: repeated rejected resume requests accumulate charges.
- **E2-27:** The prose leaves a later attach with a different kind or length undefined, although the model raises `Conflict`.
- **E2-28:** Other legacy drain-retry reason codes (`ClassifyDrainFailure`, `AggregateActor.cs:3039-3044`) are unclassified in BC-01b and `[I-14]`.
- **E2-29:** The Admin `CommandSummary` activity row is written once at submission (`SubmitCommandHandler.cs:501`), so held commands appear as processing with no reason.
- **E2-30:** Same as VG2-O2.
- **E2-31:** Same as VG2-O1.
- **E2-32:** The capacity model accepts a negative length, which lowers the counters.
- **E2-33:** Same as VG2-5.
- **E2-34 (confirmed):** C2's attempt cap and C5's closure (`:1070`: "freezing the complete member/send-ID namespace", "broker reject fence for that operation namespace") are not amended by `[I-45]`, which re-arms window w+1 under the same MessageId. As written, the resume re-arms nothing.
- **E2-35:** Same as BH2-1: the legacy resume tag `06` is circular.
- **E2-36:** `[I-10]` (4d) with mixed class-01 and class-02 failed members matches no `[I-16]` row, so it returns a 503 with an undefined `reasonCode`.
- **E2-37:** The filter helpers' exact-status branch (`status=PublishFailed` or `status=EventsStored`) changes meaning, and no BC row classifies it.
- **E2-38:** The completeness bar is not met: G-B (drain limit after a later head), G-E (parked waits) and G-I (gateway-owned entries) each leave a state with no exit.
