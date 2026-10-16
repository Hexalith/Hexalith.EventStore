# Story 6.4: frozen legacy recovery primitives and exact replay bounds

**In progress, incomplete and unqualified. No environment was admitted or cut over.**
This continues the existing parent-owned sequential step-03 implementation. Do not
restart planning or the workflow and do not spawn parallel implementers. The approved
normative specification remains the sole source of truth. No story/task/acceptance
criterion is complete. The next implementer takes sole ownership; the parent remains
read-only until that handoff.

## Implemented in continuation 08

- Fixed complete readable-array admission across multiple pages. Page arrays repeat
  one framing byte per boundary relative to the single complete array. The history
  reader now admits the exact complete-array length independently of P, while charging
  actual serialization and returned page bytes without subtracting their real work.
  `ProjectionSourceGuardPage.WireBytes` and admitted history's total are complete-array
  contributions; the cost counters `WireBytes`/`WireSizingBytes` remain actual page
  array bytes. Exact 64 MiB and +1 now run with actual protected payload conversion at
  both P1 and P256. P1 actual successful sizing/returned bytes are 3*(67,108,864+47),
  while each logical complete array is exactly 67,108,864. This distinction must remain
  explicit in future byte-budget reports; source response framing was not normalized.
- Full-replay routes share one immutable complete readable DTO array after independent
  source/checkpoint admission. Every additional route still authenticates the entire
  source and compares every readable field, including byte arrays and timestamp offset,
  against the shared prefix. Changed data fails closed. Verification no longer builds
  another complete readable array only to discard it. Production history tests prove
  shared array identity, independent scoped fingerprints, no retained verification
  prefix and conflicting shared data rejection. An actual three-route full replay
  selects equivalent detail/index models with one coordinated receipt and one required
  preparation per route. This removes concrete duplicate retention; it does not prove
  all-mode working-byte, CPU or elapsed acceptance.
- Instrumented actual previously uncharged digest/receipt enumeration in completion,
  appended receipt construction, compaction, full digest copying, exact completed receipt
  lookup, supplied-ID conflict traversal and serialized size's identifier scan. Frozen
  numeric budgets were not changed. Focused production vectors still pass with the
  higher truthful `ProofVisits` observations. This was a concrete-hole fix, not a claim
  that every remaining proof/control/working-byte vector has been qualified.
- Finalization now accepts a safe actor-proven retained-floor advance when the frozen
  immutable prefix is unchanged and every admitted tail event remains available
  (floor<=base C+1). It retains the original source binding, finishes C->H, and reports
  `head_changed` until a later coherent Current inspection. Actual unread-tail loss
  preserves old C, pending evidence, reservation and original retry work. The production
  aggregate/lifecycle/model/store fixture covers both floor changes during release;
  the safe case's later Current is read-only and starts no additional handler.

## Legacy recovery primitives: deliberately not wired into delivery

- Added bounded payload-free `ProjectionLegacyDeliveryIntent` references on the existing
  activation item, with an additive init property omitted when null. Existing positional
  work-item constructor/deconstruction and public `IProjectionActivationOutbox` ABI are
  unchanged. The owning Dapr implementation exposes an internal conditional binding and
  exact-intent cleanup interface. An append's Ensure preserves the original frozen work;
  ordinary completion cannot delete an activation carrying it. Exact cleanup preserves
  any newer activation revision. Immutable app/version/head/operation/source/candidate
  bindings cannot be retargeted. A bounded sizing stream rejects references over4096
  serialized bytes before persistence. This is not yet a publication state machine or
  complete lease/recovery protocol; callers and lifecycle/publication wiring remain open.
- Added separate additive actor and server gateway interfaces for read-only inspection
  of the original rebuild candidate/promotion receipt. Existing actor writer methods,
  signatures, constructor and public interfaces remain intact. The operation must match;
  promoted evidence must agree with actual live bytes. Foreign/mismatched evidence returns
  Conflict without returning model bytes. No handler, repair, notification or persistence
  mutation occurs during inspection.
- Inspection first clears the owning StateManager cache. A failed Stage/Promote save may
  leave pending candidate, receipt and model entries on the same actor instance; those
  entries cannot count as persisted proof. Four actual fault-manager vectors cover
  Stage/Promote x failure-before-commit/lost-committed-response. Inspection distinguishes
  Missing/Staged/Promoted from committed state and a later unrelated save cannot resurrect
  the discarded uncommitted write. ClearCache failure propagates rather than authorizing
  adoption. The fixture substitutes the owning actor state manager; no backend bypass or
  HTTP-success proof is claimed.

There is **no runtime switch to these primitives**. Descriptor-absent legacy delivery
still uses GetEventsAsync(0), /project and its released direct actor replacement path.
Do not report legacy or generic unguarded full-route staged fallback as implemented.

## Immediate next work and design constraints

1. Finish the bounded complete-prefix legacy path and full-route compatibility through
   the existing candidate/promotion/receipt/lifecycle and scoped delivery checkpoint
   protocols. Freeze head and original app/version/work before effects on the outbox;
   restart must resume those exact references and original candidate, without another
   preparation after candidate persistence or a lost stage response. A page is never a
   whole-stream argument. Preserve v1/public handler semantics, explicit legacy adapter
   registration and configured wider unactivated rebuild profiles.
2. Stage/promote prechecks outside the actor are insufficient. Existing ordinary Stage
   is reentrant by operation, and Finalize deletes its receipt. A delayed original
   worker could otherwise restage after cleanup and repeat publication/notification.
   A compatible solution is additive bound publication calls that validate the exact
   supplied lifecycle snapshot through its owning gateway **inside the serialized
   projection actor turn**, plus a bounded same-work completed binding compacted in
   the existing promotion receipt when prior/candidate model bytes are cleared. Carry
   original scope/head/content/operation identity; reject stale/conflicting ownership.
   Ordinary rebuild can replace a prior tombstone, so its changed lifecycle revision
   must still fence an older delayed call. Preserve old actor methods and ordinary
   rebuild semantics. Apply cache barriers and ambiguous-save readback to all new
   bound writes as well as inspection; no cached pending success may advance C.
3. Do not borrow/Reset the real operator rebuild checkpoint for live fallback. Its Save
   protects terminal/different operation identities, and Reset is an operator-authorized
   trust boundary. Live fallback should finalize its own scoped delivery checkpoint
   using existing activation work; operator audit/progress stays separate. Opaque
   tenant-wide ETag regeneration is not a model completion/freshness version authority.
4. Retain every continuation07 remaining requirement: all section6 interruption/W6
   matrices, two replicas, same-work leases/fences, N8/N9, A8/A32, R/R+1, proof/control/
   scheduler/cold/failure budgets, actual complete working bytes and CPU/elapsed vectors,
   arbitrary requested-domain proper-subset fresh catalog proof, pinned Dapr1.18.2 full
   W0-W6/fallback/crash orchestration, then every final section10 Debug/source and
   Release/package suite/restore/build/inventory gate. Current passing focused source
   results are not those gates. No runtime cutover/activation/deployment is authorized.

## Useful retained observations

The parent checked primary Dapr.Client1.18.10 source at repository commit
f3e09addc0572e40175f9683f4af5ddc40981ea5: CreateInvokeMethodRequest sets JsonContent,
which can stream on the production HTTP send path. A test observer's ReadAsStringAsync
buffer is separate from production retained working bytes; do not infer a runtime full
buffer solely from that observer. Earlier pinned native transport/live metrics notes in
continuation07 remain applicable. Ordinary SDK serialization still needs actual measured
working-byte qualification across every required mode.

See continuation-08-verification.md for exact commands/results. Historical full Client
1562 and DomainService899 passes and the failed intermediate full Server run remain as
recorded in continuation07. They were not rerun here and do not become current final gates.
