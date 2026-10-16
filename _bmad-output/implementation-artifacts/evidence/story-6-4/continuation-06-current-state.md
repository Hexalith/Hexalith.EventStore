# Story 6.4: measured ranges, exact scopes and retry continuation

**In progress, incomplete and unqualified. No environment was admitted or cut over.**
Continue the existing parent-owned sequential step-03 implementation. The receiving
agent remains the sole active implementer; the parent does not edit runtime files.
Do not restart planning or spawn parallel implementers. The approved normative
specification controls every disagreement. No story/task/acceptance criterion is
complete.

## Completed in this continuation

- Added the DataContract `SourceEventRange` and
  `IAggregateActor.ReadMeasuredEventsBoundedRangeAsync`. The actor returns its exact
  compact event-array byte length. The existing bounded-range signature delegates
  and remains available. The guard reader enforces independent page/pass limits
  from this trusted owning-actor measurement without serializing envelopes again.
  Updated the fake, production fixture and real actor-remoting tests.
- Removed the early minimum-size shortcut. Every already-read envelope is fully
  streamed through the sizing counter, and its source-decode bytes are recorded
  before scope/page rejection. A first oversized envelope now charges its complete
  read/decode/sizing work while returning no envelopes and invoking no handlers.
- Enforced the independent 4096-byte source metadata-body cap. Exact/one-over
  regressions retain the raw bytes and perform no event reads. Metadata sizing
  serialization is separately measured. Inspection rejects required cache,
  pending-finalizer or pending-counter recovery before any state access, preserving
  the existing recovery flags/turn instead of hiding durable repair inside Current.
- The mixed large-payload fixture now measures 28,802,976 source-sizing bytes,
  within B+Blook=28,803,003. The previous duplicate-sizing failure is fixed.
  The exact 64-MiB source pass and one-over rejection continue to pass.
- Current inspection examines verified ordinary routes even when all descriptors
  disappear. Missing, corrupt, unknown-version or unreadable sibling scope evidence
  cannot disable an independently activated route or produce whole-catalog Current.
  Supported sibling tails may complete without modifying the invalid scope.
  Qualification requires only the requested scope's implemented capability.
- Added internal query inspection by actual projection type. Normal ordinary
  scopes retain the same proof path/read budget. Removed/rebuild-only queried
  scopes inspect their retained row and both markers and remain Unknown when
  guarded. Unguarded schema1 query behavior is preserved. Tests cover missing
  sibling admission, descriptor removal, route removal and rebuild-only routing.
- Restored historical schema1 size compatibility beyond 4 MiB. Only guarded
  schema2 evidence receives the fixed S(H,R)/absolute limits. A production store
  regression writes and reads a larger valid legacy identity row without pruning
  its receipt/digest, while guarded exact/one-over tests remain enforced.
- Guarded retry admission/cleanup failure now defers original pending work with
  saturated Attempt/backoff semantics. The original sampled revision prevents
  double deferral after tail recovery updates work. Exact owned cleanup has one
  maximum-30-second budget; thrown cleanup faults retain/defer the original or
  already-claimed revision without aborting the worker's remaining due loop.
  Caller cancellation remains propagated. Real scheduler tests cover nine
  activations for missing proof, inactive admission, failed/thrown cleanup and a
  newer current head, plus stale-revision prevention of a second defer.
- Added `RetryActivations` separately from the stored saturated Attempt and the
  tail's own activation measurement. `ManifestOperations` now identifies prepared
  intent; `ModelMutations` is emitted at successful visibility commit/transaction,
  while physical prepared/candidate/compensation operations stay charged at the
  raw store seam. A staged-batch regression proves preparation is not completion
  and repeating commit does not increment the logical counter again.
- Added optional `EVENTSTORE_TEST_DAPRD_PATH` to the existing live fixture. Used
  isolated official daprd/placement/scheduler 1.18.2 binaries, checked by the parent
  against official release SHA-256s; installed 1.18.7 services were untouched.
  Two real Dapr/Redis source tests pass with the new measured carrier. A process-wide
  meter listener captures actual actor work across HTTP contexts. A real
  `ActorProxyFactory` HTTP handler also captures native DataContract request/response
  body bytes separately from canonical compact JSON array bytes. All owned isolated
  infrastructure was terminated after each run. These are source-primitive tests,
  not guarded current/tail/fallback/crash qualification.

## Remaining mandatory work

1. **Compatibility and partial activation remain incomplete.** Supported guarded
   siblings now progress and queries fail closed, but ordinary unguarded siblings
   still need their compatible upgraded-writer delivery lane. The orchestrator
   returns when any guarded work owns the catalog. Descriptor-absent legacy v1
   delivery still uses the existing GetEventsAsync/full dispatch/direct actor write;
   the approved bounded canonical staged fallback has not been established there.
   Preserve old public behavior and wider configured streamed rebuilds.
2. Complete every approved crash/cancellation/lease/generation/competing-writer
   interruption with persisted semantic outcomes. New retry tests use production
   scheduler logic over the existing state seam and synthetic guard results; they
   do not replace the required full production-handler/live W6 fixture. Original
   older-head work is now preserved/backed off, but retained-receipt R/R+1
   classification/settlement and every thrown/lost scheduler response need full
   original-identity coverage.
3. Complete all cost categories and limits: every proof copying/validation visit,
   metadata sizing, cold migration/activation, outbox/shared/fold/rebuild/parts and
   cleanup operations; exact wire-array 64-MiB/one-over; protected W4; proof absolute
   caps and R/R+1 at 256/4096; A=8/32 complete CAS vectors; event/proof working bytes.
   The native HTTP observer measures real body bytes but buffers for observation;
   it does not establish memory or serialization-CPU qualification. Canonical JSON
   and native DataContract bytes overlap and must not be summed as independent I/O.
   Full model/control byte counters also overlap. An ambiguous commit whose response
   is lost can be observed only by later durable readback; complete logical-effect
   diagnostic accounting for that boundary remains unproven.
4. Finish W0's P=1/16/256 cartesian scope, W1 three-route production-owner costs,
   every W2/W3 boundary and W5 every actor/detail/index/freshness surface. The
   guarded production fixture covers many selected vectors, not the complete matrix.
   Shared-family Current remains intentionally unsupported; do not infer support
   from a per-source fold receipt, interfaces or journal acceptance.
5. Extend actual guard current/tail/fallback/crash orchestration and model ownership
   into the pinned live lane. The two pinned source tests alone do not prove
   optimization or staged replay. The parent-provided binaries remain at
   `/tmp/story-6-4-dapr-1.18.2-wpll4ztg/{daprd,placement,scheduler}`. Exact hashes,
   startup version/commit, commands and measurements are retained in the pinned-lane
   JSON. A real DelegatingHandler is supported by the pinned ActorProxyFactory;
   DaprClientBuilder.UseGrpcChannelOptions can likewise provide a real gRPC HTTP
   observer for later owning-store transport measurements.
6. Complete the final approved gates after runtime work is finished. Full Client
   and DomainService suites passed here; full Server is still the continuation-05
   result, predating these changes. Full/pinned live and Release/package gates remain
   unproven. Client's 10 existing crypto advisory warnings remain; dependencies and
   audit policy were not changed. Contracts packaging skips require their declared
   package inventory. Parent final diff/matrix/review work still follows all required
   implementation and verification.
7. Assess authenticated schema2 same-C rehydration after a legitimate completed
   operator rebuild. `QualifyAsync` currently rejects any existing schema2
   `GuardEvidence` that differs from the newly proven source/model/Idle binding.
   A rebuild can legitimately change selection generation, freshness and Idle
   revision at the same delivery C. Add a real rebuild-to-reconciliation regression
   and permit only approved same-C hydration after complete canonical source,
   selected receipt/readback and unchanged IDs/receipts/fence proof. This root-audit
   item was retained without changing runtime during final verification.

See [Continuation 06 verification](continuation-06-verification.md) for exact
commands/results, limitations, intermediate failures and retained evidence.
