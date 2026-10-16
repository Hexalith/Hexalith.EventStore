# Story 6.4: staged replay and shared safety handoff

**In progress, incomplete and unqualified. No environment was activated.**
Continue the parent-owned sequential step-03 implementation; do not restart the
workflow or spawn parallel implementers. The wrapper and approved normative
specifications remain authoritative. This is implementation context, not an
acceptance record. The normative bytes and approval were preserved.

## Implemented in this continuation

- Qualified named `full-replay-v1` owners and missing/cold mapped owners with
  existing guarded progress can take bounded complete-prefix canonical staging.
  The guard reads and authenticates 1..H, checks every completed-prefix ID and
  retained receipt, and retains readable bytes for the single preparation.
  The delivery reservation still admits the exact C-relative suffix. Verification
  rereads the frozen complete prefix; completion uses the existing pending,
  actual-Idle, conditional row/readback boundary. No delivery checkpoint advances
  during staging or model promotion.
- Guarded replay requests bind the head MessageId and exact owner identities and
  generations before publication. Every candidate operation must use expected
  ETag/create-only ownership. An unexpected head, owner, generation or
  unconditional plan is rejected before staging. Wider operator rebuilds without
  these explicit guarded fields retain their established path and configuration.
- The first preparation is stored as canonical immutable manifest bytes and a
  <=16-KiB reference in the existing private rebuild transport namespace. Its
  separate canonical manifest ceiling is 1 MiB. Stage/commit/verify/abort and
  restarts restore exact original values, ordering and concurrency without another
  handler preparation. Orphan/ambiguous material fails closed. These private
  replay-plan records are retained; their post-completion cleanup is unfinished.
- Cold mapped replay uses the existing immutable <=262,144-byte part protocol,
  complete streamed handler input and owning part cleanup. A lost stage or commit
  response reuses the same original reservation, head dispatch, owner/generation
  and manifest; it does not prepare another candidate or repeat logical output.
- Metadata requires explicit `projectionSourceGuardVersion: 1` negotiation before
  returning descriptor-bound fingerprints. Old request bodies receive the exact
  historical fingerprint. Compatible dispatch/rebuild paths accept that original
  catalog independently; proof enrollment still requires the descriptor-bound
  catalog. Unknown requested versions fail closed. Identity-bound dispatch on a
  restarted replica derives this choice from the complete configured catalog,
  without depending on a previously populated process-local registry.
- `SharedProjectionEpochCoordinator.ReadAppliedDeliveryProofAsync` is strictly
  read-only: it samples epoch, original owner checkpoint, immutable terminal fold
  receipt, and the unchanged epoch again. It invokes no registration, status,
  control-index repair, fold, model read or mutation. Exact original source
  content/sequence/position, selected generation and all required writers must
  agree. Enrolled shared dispatch requires this existing fold proof before a
  successful acknowledgement; redelivery can prove completion without another
  model increment.
- Shared selected/current proof remains intentionally unsupported. The per-source
  applied inspection needs four SDK metadata reads before lifecycle checks and
  does not establish complete shared-family owner/freshness coverage. It therefore
  cannot satisfy the approved current-profile proof operation. Generic batch
  metadata cannot substitute for that owning evidence. Mixed catalogs cannot
  report current or start qualified sibling effects while a required shared route
  is unproven. Existing nonoptimized shared protocols remain available.

## Focused evidence and its limits

Final Server Debug/source build has 0 warnings/errors. The approved focused set,
plus lifecycle and delivery-documentation classes, passes **402 tests**, with no
failures/skips. Final DomainService Debug/source build has 0 warnings/errors and
its six focused compatibility/shared classes pass **200 tests**, without
failures/skips. The exact commands and remaining focused results are retained in
[Continuation 04 verification](continuation-04-verification.md).

Client's focused batch/fingerprint/catalog preservation set passes **59 tests**
without failures/skips. Its Debug/source build has 0 errors and the same 10
existing NU1902/NU1903 crypto advisories; no dependencies or audit rules changed.

These are deterministic production orchestration/DomainService/SDK/store seams
over existing recording state and actor/lifecycle substitutes. The recording Dapr
fixture now has opt-in stateful typed CAS for private rebuild transport records;
its existing default stub behavior is unchanged. This is not live Dapr evidence,
complete workload qualification, full regression or Release/package evidence.

## Actual remaining work

1. Complete the already-current outbox/retry cleanup lane and conditional guarded
   replay-plan cleanup after proven completion. Pure current inspection must stay
   zero-write. Preserve original work/material on cleanup uncertainty.
2. Complete remaining fallback routing: an explicit single-event rebuild-required
   result before effects does not yet switch to canonical mode, and mixed
   mapped/nonmapped replay groups without a common advertised part protocol remain
   reconcilable rather than publishing separate colliding `rebuild` manifests.
   Missing/zero nonempty scopes still require authorized reconciliation/rebuild;
   no new owner progress may be inferred. Verify every required actor/detail/index
   and freshness surface for each compatible legacy/named fallback.
3. Keep the intentionally unsupported shared-current profile distinct from any
   new shared feature. Do not force it into the profile with a parallel receipt
   protocol. The existing per-source applied proof does not authorize complete
   family freshness, captured-owner completeness or guard activation. Any further
   supported shared mode must use the existing inventory/selection/receipt fences.
4. Finish every remaining section-6 crash/cancellation/lease/generation/competing
   writer boundary. This continuation covers lost stage/commit responses and
   pre-publication invalid plans, not the complete interruption matrix.
5. Instrument actual actor/state/model/handler/scheduler operations and all bytes,
   hash/serialization/proof visits, CAS/mutations, D/J and N independently of the
   saturated Attempt. Finish W0–W6, exact/one-over limits, R/R+1, A=8/32 and
   64x64/128 shard occupancy with blocking assertions. No complete cost claim is
   established by the current counters in these focused tests.
6. Execute the full approved section-10 affected/live and Release/package gates.
   No live sidecar was started here. Installed daprd 1.18.7 versus pinned 1.18.2
   and existing crypto advisories remain explicit unqualified gates.

No commit, branch, staging, push, dependency/submodule update or maintenance
activation occurred. Unrelated workspace processes were left untouched.
