---
title: 'Story 8.5 review correction proposal'
status: pending-user-approval
current_amendment_sha256: aed1eb3f6cd438ff0691db5d449dab4d9e5186e3a0dc4d2d7c0eecc56b9fbe7a
proposed_amendment_sha256: a853201a6df1e8e88730a40a582b87e20c5e5cf7a223f01ca905e9c9ee7df94d
proposed_effective_authority_sha256: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9
---

This is the exact proposed addition before the owning amendment's normative end marker, with one final blank line. Current approved bytes are unchanged. Approval permits applying this addition, rebinding implementation authority hashes, and re-deriving the implementation to address the recorded review findings. It grants no release, Azure, Server-integration or G5 approval.

The pragmatic correction keeps two catalogs and one bounded worker. It adds two durable bindings, reuses notBeforeUtc for claim ownership, resolves confirmed outcomes without crossing the invalidation barrier, and makes existing restore-workflow persistence compare its supporting authority atomically.

```text

## 8. Review corrections: retained intent and bounded ownership

Append `targetProviderVersionDigest:sha256-or-empty` after `updatedAtUtc` in the closed `ReconciliationV1` schema. It is required and nonempty only for Rewrap, and empty for every other kind. For Rewrap it is lowercase SHA-256 over strict UTF-8 of the exact selected provider key-version ID, persisted atomically with Started intent before any backend mutation. Both direct execution and restart reconciliation must compare the returned provider-version digest with this retained target before changing wrapped material or indexes. No full provider ID is added outside WrappedDekV1. A missing or mismatched target cannot authorize recovery; no implicit migration or guessed target is permitted.

Append required `workflowId:ulid` after `memberCutoff` in InvalidationDrainV1. Begin binds the approved existing workflow ID into that immutable record. Pending and terminal scope audits, including restarted and per-key continuations, use this same workflow association. It does not authorize the engine to advance the domain workflow.

Use the existing ReconciliationV1.notBeforeUtc for bounded work ownership. Initial foreground Create and Rewrap intents defer worker eligibility for 90 seconds. Explicit foreground completion or failure recording may release that delay when foreground mutation has stopped. A worker claims only due work, atomically increments attempt, sets Running and moves notBeforeUtc 90 seconds forward. Another worker cannot replace that claim before it is due. Every durable mutation still compares the exact claim, so expired owners cannot mutate through a replacement; time alone never proves persistence, absence or clearing. Scope drains still wait a fresh full monotonic 60 seconds on every resume. Work that cannot finish within its ownership interval remains indexed for a new claim.

Confirmed persistence during Invalidating resolves the Create/Activate operation as Persisted/Succeeded without activating its Reserved key. Complete the corresponding reconciliation/work bookkeeping atomically under the current claim and lifecycle barrier while retaining the key and both indexes for the approved invalidation/deletion. Unknown evidence remains Pending. A Reconcile backend result of Missing means affirmative absence of that exact stable mutation, never inability to query it or a missing KEK. On confirmed absent Rewrap, atomically restore the original Reserved/Active state under current authority, retain original wrapped material and inventory membership, and terminalize the failed operation/work with not-persisted. Unavailable or Ambiguous never takes this path.

Restore verification joins the same shared local-operation and distributed read-lease protocol as ordinary reads and retains abandoned backend work until its buffers are cleared. Accepted restore decisions must be conditionally persisted by the existing workflow seam in the same qualified transaction as unchanged-value comparisons for every observed lifecycle, fence, key, scope-index and KEK-inventory head/page needed by that decision. Failure or uncertainty returns no Accepted result. This changes only the internal handoff to the existing workflow; no new public contract, admission record, catalog or evidence framework is introduced. Host wiring remains Story 8.7.

Required regression checks cover changing typed getters, canonical/ordinary callback ordering and subtree suppression, two competing workers, foreground/recovery races, persisted evidence after an invalidation barrier, confirmed missing and wrong-target rewrap recovery, different-DEK replacement rejection, restore races at unwrap and decision persistence, cancellation-ignoring restore backends, provider-ID bounds, and every failed half-open probe. Existing frozen wire/golden fixtures, package inventory, packability and production/G5 boundaries remain unchanged.
```
