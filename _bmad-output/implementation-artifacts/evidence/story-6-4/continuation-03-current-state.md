# Story 6.4: current sequential handoff

**In progress, incomplete and unqualified. No environment was activated.**
Continue the existing parent-owned step-03 implementation. Do not restart the
workflow or spawn parallel implementers. The wrapper specification and approved
normative specification remain authoritative; this file is implementation context.
Earlier details remain in [the historical checkpoint](implementation-checkpoint-through-continuation-02.md).
Exact commands and current evidence are in [verification](continuation-03-verification.md).

## Implemented in this continuation

- Explicit mapped incremental routes now share guarded per-event tail application,
  original receipt recovery and finalization with single-event routes. Both the
  catalog route declarations and capability descriptor must agree. DomainService
  applied-receipt operations verify the mapped handler interface and use its
  existing EventStore-MessageId batch receipts. Qualification accepts implemented
  mapped routes; unsupported full-replay/shared modes remain closed.
- Existing coordinated `rebuild` batches can bind multiple route selections via
  optional `ReadModelBatch.OwnerSelections` and terminal receipt `owners` metadata.
  Model metadata `ProjectionOwnerSelection.ReceiptProjectionName` locates that
  same receipt. Selections must occur in fingerprint-bound model operations.
  The SDK dispatcher and transport-plan restore preserve this metadata. Existing
  batch fingerprints and descriptor-absent/default serialization remain unchanged.
  This supplies proof foundations, not guarded canonical fallback itself.
- Guarded original manifests exceeding the inline control budget use the private
  `ReadModelBatchKeys.ManifestKey` in the existing batch namespace. Their raw
  canonical bytes retain the original fingerprint, operation order/concurrency
  and separate 1 MiB bound; the active marker keeps a <=16 KiB reference.
  Recovery accepts only that scope's exact immutable bytes. After terminal
  model/receipt proof the owning protocol conditionally cleans the private key;
  uncertainty retains immutable cleanup material. Read-only proof performs no repair.
- Fresh single-event owners require explicit catalog-bound
  `InitialOwnerProtocol = aggregate-first-event/v1`. Authenticated empty-source
  bootstrap retains one source seed/incarnation; empty maintenance can initialize
  schema2 C=0 and exact scope admission without a model or completion receipt.
  Empty inspection remains Unknown. Unknown nonempty scopes cannot initialize
  progress. First-event MessageId fixes the initial generation and aggregate ID
  fixes owner identity; same-work recovery keeps both. Other initialization
  protocols are unsupported. Existing descriptors without this field keep their bytes.
- `SourceProofCodec.IsQualifiedEmpty` admits only an exact validated seed through
  command rehydration/first append. Unproven zero metadata, orphan snapshots,
  erasing/erased source bootstrap and changed source evidence fail closed.
  Source encoding, stored event bytes and snapshot formats are unchanged.
- Owning-store proof-limit rejection now propagates from coordinator writes and
  finalization as `delivery_proof_limit_exceeded`, without spending every CAS
  attempt. The two delivery documents describe these implemented/unqualified paths.

## Current validation

- Debug/source Server build: 0 warnings/errors; approved focused Server set plus
  lifecycle gateway: **385 passed, no failures/skips**.
- Debug/source Client build: 0 errors, the same **10 existing NU1902/NU1903 crypto
  advisories**; focused batch/fingerprint/catalog set: **59 passed, no failures/skips**.
- Debug/source DomainService build: 0 warnings/errors; approved focused
  compatibility/shared set: **105 passed, no failures/skips**, 97.560 seconds.
- These are deterministic production SDK/handler/store seams over existing
  recording state and actor/lifecycle substitutes. They do not qualify live Dapr
  behavior, complete production costs or the full W0–W6 matrix.
- No new broad Debug, Contracts, live-sidecar or Release/package result is claimed.
  The historical full regressions predate these changes and require rerunning.

## Required next work

1. Implement bounded canonical staged full-prefix fallback through the existing
   prepare/stage/commit/verify/abort/promotion protocols. Include cold mapped
   owners and full-replay-only/unsupported tail handlers. Preserve every owned
   actor/detail/index/freshness surface and wider configured rebuilds. A single
   preparation must retain its original manifest across later actions/restarts;
   repeated handler preparation or a newly derived manifest after effects is unsafe.
   Bind source head MessageId and selected generation before publication. The new
   `BindRebuildBatch` proof helper still needs full fallback-specific admission
   checks; its existence alone is not authorization to enable full-replay mode.
2. Add shared-epoch selected/applied proof through `SharedProjectionEpochCoordinator`
   and DomainService's existing shared protocols. Require complete registered
   writer/owner coverage, generation selection and durable fold receipts.
   Journal/registration acceptance and generic batch metadata alone cannot prove it.
3. Finish every section-6 crash/cancellation/lease/generation/competing-writer
   boundary, including new-owner and external-manifest cleanup uncertainty.
   Retain original reservations and immutable work on ambiguity; no fallback after
   unresolved incremental effects. Verify all end-to-end independent deadlines.
4. Complete the separate already-current outbox/retry cleanup lane, including
   mixed routes and delayed old-head work. Pure current inspection still does none.
5. Complete production instrumentation/export at actor/state/model/handler and
   scheduler seams: actual key/RPC/lookahead/decode/hash/serialization operations,
   all bytes, proof visits, CAS/mutation attempts, J/D, activation N independent
   of saturated Attempt, and actual 64-shard occupancy/pre/post sizes.
6. Complete W0–W6 and exact/one-over proof/control/manifest/page/source limits,
   R=256/4096 and R+1, A=8/32, 64x64/128 shard fixtures, all compatibility and
   persisted live vectors. Current W1/W2 and new-owner fixtures are partial evidence.
7. Execute every approved section-10 broad affected/live and Release/package
   gate sequentially. Pinned runtime 1.18.2 versus installed 1.18.7 remains
   unqualified; existing crypto warnings remain explicit. Do not weaken gates.

No commit, branch, staging, push, dependency/submodule update, production cutover
or non-fixture activation occurred. Original user changes remain preserved.
