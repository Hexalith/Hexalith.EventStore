# Story 6.4: compatible siblings, proof recovery and actual costs

**In progress, incomplete and unqualified. No environment was admitted or cut over.**
Continue the existing parent-owned sequential step-03 implementation. The receiving
agent is the sole active implementer; the parent remains read-only. Do not restart
planning or spawn parallel implementers. The approved normative specification is
the sole source of truth. No story/task/acceptance criterion is complete.

## Implemented in continuation 07

- Added the exact-route compatible named delivery lane. An unguarded schema1 or
  absent sibling can use ordinary compatible delivery while guarded/corrupt/schema2
  siblings remain independently fenced. Complete bounded readable history precedes
  generic compatible handler invocation; explicit single-event/incremental handlers
  receive per-event suffixes. Retry work retains full catalog pending routes and
  merges existing sibling fencing tokens rather than overwriting them. Whole-catalog
  Current remains false for an unqualified sibling. Legacy v1 staging is still open.
- Same-C schema2 qualification can replace a stale selected-model/Idle binding after
  a real ordinary operator rebuild, preserving C, head ID, complete digests, receipts,
  retained floor and marker bytes. The selected rebuild receipt must be authentic,
  the source/catalog/semantics unchanged, and Idle revision strictly newer. The
  production regression uses actual named stage/commit/verify and persisted owner
  receipt; no fabricated selected-proof change. Ordinary operator rebuilds may bind
  coordinated owner selections even without explicit guarded generation maps.
- Preserved wider ordinary rebuild behavior when a descriptor is enrolled but the
  guard is inactive. A normal manifest without usable bounded owner evidence, or
  wider than the guarded one-MiB profile, remains on the existing ordinary protocol
  and is unqualified for guard proof. Explicit guarded generation requests remain
  strict. The regression executes an actual two-write output larger than one MiB
  under an explicitly configured two-MiB SDK profile, with descriptor and no guard
  marker; original delivery state is unchanged and Current is false.
- Added actor-owned SourceErasurePending metadata (omitted when false), atomically
  saved with existing erasure progress before event deletion. Inspection, bounded
  history, bootstrap and append reject erasure intent. Missing-metadata inspection
  charges returned erasure-progress bytes and distinguishes pending/completed erasure
  from EmptyNoWork without adding a read to normal nonempty Current.
- Source erasure persists its exact route set on existing TrustedEffectErasureProgress
  before acquiring cross-actor lifecycle leases, then acquires all existing erase
  fences before deleting history. Active/pending guarded reservations defer erasure.
  Stable owned source-erasure tokens are released via CompleteEraseAsync, whose
  existing durable outcome is Idle with manifest/outcomes cleared. Restart tests
  cover partial batch/save response loss, acquire-before-progress-marker-save,
  completed-before-release, route removal before resume, and subsequent distinct
  projection-model erasure. No private backend scans or new registry were introduced.
- Bootstrap also rejects legacy nonempty erasure progress without the new flag,
  even if every envelope is still present, before anchoring or reading events.
  Added cold source metadata/progress/event read, decode/sizing, anchor-write,
  save-attempt and readback categories. Ambiguous save readback is charged.
- Owner/applied/recovery proof routes recompute deterministic current configured
  route/descriptor fingerprints on fresh replicas rather than relying on the issued
  process-local catalog cache. Single-domain and all-installed-domain catalogs are
  supported, including a two-domain fixture. Changed catalogs fail closed. Public
  metadata proper subsets with more than one but fewer than all installed domains
  remain typed unsupported/unqualified; that combination is not complete.
- Receipt owner/applied/recovery reads reject duplicate properties recursively,
  including case collisions under Web deserialization, and require the frozen
  v1 plus canonical 43-character base64url SHA256 fingerprint. They retain existing
  key-read counts and perform no writes. Synthetic test proofs were corrected to
  the frozen SDK format; the SDK fingerprint golden bytes are unchanged.
- Added exact original-work pending-route reclamation. The worker intersects independently
  proven current routes with its original identity/head/catalog/pending set, verifies
  each row, both markers, source, actual selected owner and Idle read-only, then
  reclaims existing private replay material and prunes only those routes under the
  sampled durable revision/lease. Unguarded/incomplete/terminal siblings are retained.
  Failed cleanup keeps recovery material and defers the original work. A production
  subset regression proves failed cleanup preserves both manifest/reference, and a
  later success deletes them while retaining the unguarded sibling and false Current.
- Actual worker admission now requests P=256, immediate/poller default remains P=1.
  The execution page-size path is internal/additive; public defaults/interfaces for
  cancellation are unchanged. A real worker plus actual aggregate/store/handler
  observes 12 range requests for delta771 at P256 versus 2313 at P1, with exactly
  2313 envelope reads and identical persisted final row/model/Idle semantics.
- Whole-attempt cost harness now enters Probe for every P; no synthesized row/head
  observation bypasses S1/S2/admission for P>1. W0 covers H{1,256,257,9999,10000}
  x P{1,16,256} x R{256,4096}. Production three-route W1 charges seven real passes.
  Actual one-route tail costs include three source inspections and five proof reads;
  independent asserted ceilings still pass.
- Actual per-event readability/provider attempts and successfully decoded payload bytes
  are charged before later page failure. Rejected attempted wire-sizing bytes are
  separate from completed returned wire bytes. The new fixture provider performs
  actual AES-GCM and Brotli decrypt/decompression at the existing optional provider
  seam; the handler deserializes the admitted domain JSON. Protected mixed one-MiB
  tails and exact readable-wire-array 64 MiB/+1 pass with zero effects on rejection.
  This is fixture-provider coverage, not qualification of any deployed provider.
- Cold writer/guard activation and reconciliation now account actual raw read bytes,
  serialization bytes, key mutations/RPCs and the one transaction separately. Writer
  marker serialized bytes are preserved. Manifest serialization has its own counter.
- Mapped guarded replay uses the established hash/count and bounded part upload streams
  instead of retaining a second complete serialized wire array. Both serialization
  passes and every attempted upload have separate counters. Existing mapped canonical
  fallback tests verify actual uploaded byte totals and exact two-pass accounting.
  This fixes the concrete duplicate 64-MiB array; complete retained-working-byte
  acceptance across all modes remains unproven.

## Mandatory remaining work

1. Descriptor-absent legacy v1 still uses GetEventsAsync(0), full /project and direct
   actor replacement. Establish bounded complete-prefix staged fallback through the
   existing IProjectionRebuildWriteGateway and lifecycle/candidate/readback protocols,
   with durable exact frozen-head recovery and checkpoint outcome. Stage/promote alone
   is insufficient. Preserve old /project/public handler ABI and explicit adapter
   registration. Generic full-route staged compatibility remains unproven. Current
   compatible named delivery must not be represented as completion of this requirement.
2. Complete all normative interruption matrices and W6 persisted outcomes, including
   cancellation, crash, lease expiry/reacquire, owner generation, competing writer,
   source growth/truncation and cleanup. New erasure/subset/same-C/fresh-replica cases
   close concrete gaps but do not establish the entire matrix. Retain explicit N8/N9,
   A8/A32, R/R+1, absolute proof caps and all failed/control/cold/scheduler cost lanes.
3. Qualify all required working-byte, proof traversal, CPU/elapsed and logical/native
   operation vectors independently. Mapped duplicate-array allocation is removed;
   actual retained buffers in every replay/tail mode must still meet 69,206,016 bytes.
   Do not turn operation counts into timing/working-memory evidence.
4. Public metadata supports arbitrary requested-domain subsets. Fresh proof now supports
   single-domain and all-domain fingerprints only. Implement additive verified context
   if required for arbitrary proper subsets, or retain typed unsupported/unqualified
   behavior and report the coverage limit. No issued-memory fallback is authority.
5. Implement full pinned Dapr1.18.2 guarded W0-W6 orchestration, fallback and crash lanes.
   Prior live evidence covers only source primitives. Use actual runtime/transport/app
   attempts: dapr_resiliency_activations_total does not cover BuiltInPolicy callbacks;
   runtime_service_invocation/req_sent_total counts remote attempts. Parent retained
   official v1.18.2 sources at /tmp/story-6-4-dapr-metrics-y8k2d4jb. Canonical bytes and
   native DataContract HTTP bytes overlap and must not be summed as independent I/O.
   Global cost listener also observes hosted work: fixture RetryWorkerInterval10min
   can tick during a long lane. Attribute cold/background phases instead of assuming
   isolation forever. Do not alter installed shared1.18.7 infrastructure.
6. Complete all final section10 suites, Contracts/package inventory, Release/package
   and pinned live gates after remaining runtime work. Current full Client and
   DomainService checks are regression evidence only. Full Server build9 had one
   unchanged random-secret negative-input failure; keep that failed run and isolated
   pass, do not weaken unrelated scanner/security rules. No final passing current
   full Server gate has been established.
7. Update documentation/traceability/story task status only from actual final evidence.
   Existing tasks remain unchecked. No activation/cutover/deployment is authorized or
   executed. No dependency/index/commit/branch/submodule mutation was performed.

## Next execution

Read continuation-07-verification.md and source-assets/process-audit evidence for
exact final checks and assets. Continue runtime fallback work, then broaden checks.
All /tmp/story-6-4-cont07-*.log files retain this continuation's raw command output.
Earlier continuation06 source/live records remain historical, not current acceptance.
