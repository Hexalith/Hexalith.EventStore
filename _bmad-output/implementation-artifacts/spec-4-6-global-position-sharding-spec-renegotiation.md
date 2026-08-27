---
title: 'Story 4.6 Global Position Sharding Spec Renegotiation'
type: 'feature'
created: '2026-08-27'
status: awaiting-operator
baseline_revision: '5ddda34f2ff0ffb0f72a60c44b265f2e4838a332'
baseline_commit: '5ddda34f2ff0ffb0f72a60c44b265f2e4838a332'
review_loop_iteration: 6
followup_review_recommended: false
normative_sha256: '2310f121dc48f59713a9c6bbc6ffe2e63be374d4d8ecc6e8d710a0d9cf3674de'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
warnings:
  - 'The v2 successor was re-derived after review pass 6; v1 remains authoritative and no prior draft digest is approvable.'
  - 'oversized'
deferred: []
operator_actions:
  - 'Commission the immutable capacity, bootstrap, committed-source, provider, cursor, backup, vector, and scope evidence required by sections 14 through 16 of the v2 successor.'
  - 'Obtain exact-content architecture-owner approval for candidate digest 2310f121dc48f59713a9c6bbc6ffe2e63be374d4d8ecc6e8d710a0d9cf3674de through the native GitHub provenance contract in section 17.'
  - 'Keep the v1 allocator authoritative and reject runtime, migration, deployment, topology, or cutover work until a separate authorized implementation story consumes the verified approval record.'
---

<intent-contract>

## Intent

**Problem:** The single DAPR actor allocator is a contention boundary, while the frozen ordering specification makes its scalar positions appear globally comparable. Sharding without a successor contract would silently weaken ordering, cursor, rebuild, and migration semantics.

**Approach:** Create a content-bound v2 successor specification selecting composite tenant+domain shards, preserving immutable v1 global positions, and making cross-shard ordering explicitly unsupported. Define compatibility, rollout, evidence, and authorization boundaries without changing runtime behavior; absent exact-content human approval, finish at `awaiting-operator`.

**Escalation resolution:** Baseline commit `5ddda34f2ff0ffb0f72a60c44b265f2e4838a332` already contains the mandatory regenerated Epic 4 context. The tracked `bmad-build-auto-result-4-6-global-position-sharding-spec-renegotiation.md` records a superseded pre-baseline clean-tree failure and MUST NOT be treated as the current story outcome. Preserve the current wrapper and v2 successor drafts as candidate agent work, then revalidate and complete them through a fresh dev-stage drive. This clarification supplies no approval and authorizes no runtime, migration, deployment, or topology change.

## Boundaries & Constraints

**Always:** Bind the successor to the current frozen specification by Git blob and SHA-256; disposition every frozen clause; preserve gapless aggregate sequence, stable `MessageId`/CloudEvent identity, immutable historical positions, and current production authority until approval. Compare tenant, domain, and composite options against measurable criteria. Keep the normative range LF/no-BOM and approval evidence outside it.

**Block If:** The selected design requires append fencing or provider write-semantics changes not authorized by Story 4.5; canonical tenant or domain identity is not stable at allocation time; or the predecessor frozen bytes no longer match the investigated identities.

**Never:** Edit `_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering.md` or `_bmad-output/implementation-artifacts/sprint-status.yaml`. Do not change `src/`, tests, public contracts, persisted state, migration code/data, DAPR/Aspire topology, deployment, or generated API documentation. Do not treat story completion, a status row, self-approval, or a stale digest as authorization for implementation.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Same composite shard | Matching scheme, tenant, domain, and positive local counters | Equality uses the full tuple; ordering is permitted by local counter | Reject invalid version, shard identity, or non-positive counter |
| Cross-shard positions | Different tenant/domain shard identities | Positions are unordered; no scalar fallback or `Max` interpretation exists | Fail closed with an explicit unsupported-comparison result |
| Mixed v1/v2 history | Immutable legacy global scalar plus v2 composite positions | Both remain interpretable in versioned form; neither is rewritten or falsely compared | Reject cross-scheme ordering while allowing identity-preserving reads |
| Partial fleet or downgrade | Old writer is present after v2 cutover starts | Old writer cannot allocate or commit new v1 authority | Fence the writer and require forward recovery; never reuse an identity |
| Rollback | No v2 write versus any durable v2 write | Pre-write rollback may restore v1; post-write rollback is forbidden | Use v2-capable forward-fix after the first durable v2 event |
| Re-drive after stale clean-tree result | Recorded baseline already contains the regenerated Epic 4 context; current wrapper and v2 successor drafts exist | Treat the old blocked result as historical, preserve both candidate drafts, and revalidate them from the dev stage | Do not restore a patch, resume directly at review, or treat either draft as approved |

</intent-contract>

## Code Map

- `_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering.md:11-36,67-71` -- immutable predecessor authority; bind and disposition its actual frozen clauses without editing it.
- `_bmad-output/planning-artifacts/epics.md:3085-3131` -- authoritative Story 4.6 scope and acceptance contract.
- `src/Hexalith.EventStore.Server/Events/DaprGlobalPositionAllocator.cs:8-20` and `src/Hexalith.EventStore.Server/Actors/GlobalPositionActor.cs:15-36` -- read-only v1 allocator seam, fixed actor id, scalar state, and checked range behavior.
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs:93-142` and `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:650-727` -- read-only reservation-before-commit path explaining allowed gaps and rollback limits.
- `src/Hexalith.EventStore.Contracts/Events/EventMetadata.cs:22-56`, `src/Hexalith.EventStore.Client/Queries/QueryCursorScope.cs:62-80`, and `src/Hexalith.EventStore.DomainService/DomainSharedProjectionRebuildFingerprint.cs:9-44` -- scalar public, cursor, and rebuild assumptions the successor must version.
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/ProjectionWatermarkRebuildIntegrationTests.cs:28-98,185-195` -- read-only evidence of an unsafe cross-stream `Max(long)` consumer pattern; no test change in this story.
- `_bmad-output/implementation-artifacts/1-20-github-approval-role-allowlist.json` and `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:386-434` -- read-only precedent for immutable GitHub approval identity and role verification; do not claim editable Markdown is authenticated approval.
- `_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md` -- new normative successor and detached approval record to create.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md` -- create the LF/no-BOM successor with predecessor identities, normative-range-only exact clause mapping, option matrix and a non-circular reproducible full-path benchmark gate, selected composite strategy, canonical collision-free physical keys and end-to-end limits, a complete versioned metadata/comparison/diagnostics contract, generation-independent command idempotency and bounded full planned-batch binding, scalable collision-verifiable permanent retry indexing, canonical shard-set identity and crash-recoverable catalog/admission/retirement authority, exact bounded confidential cursor/rebuild schemas and liveness, executable bootstrap inventory/transcript/proof completeness, resumable quiescent provisional/irreversible rollout/rollback, consistent-snapshot monotonic full-state restore, exact test-vector-backed multi-generation fingerprint bytes, separate immutable specification/evidence/approval-record identities with strict manifests and native GitHub provenance, exact normative SHA-256, and executable scope validation.
- [x] `_bmad-output/implementation-artifacts/spec-4-6-global-position-sharding-spec-renegotiation.md` -- record reproducible pre-commit and baseline-to-candidate verification, then finish as `awaiting-operator` with a non-empty imperative `operator_actions` list after reviewed agent work is committed.

**Acceptance Criteria:**
- Given the frozen v1 authority, when the successor is inspected, then every old clause is retained, amended, or superseded and its predecessor blob, file digest, and frozen-block digest are exact.
- Given tenant, domain, and composite candidates, when the decision matrix is reviewed, then ownership, contention, uniqueness, monotonicity, gaps, commit order, hot shards, recovery, scaling, and provider dependencies support a measurable composite selection and explicit rejections.
- Given versioned v1 and v2 positions, when equality, ordering, cursors, checkpoints, projection/rebuild, and diagnostics are evaluated, then same-shard behavior is explicit and cross-shard or cross-scheme scalar sorting fails closed.
- Given mixed history, partial deployment, failure, or rollback, when the rollout contract is followed, then historical positions remain immutable, old writers cannot regain authority after cutover, and no identity or aggregate sequence can be reused or regress.
- Given a later implementation proposal, when its evidence matrix is applied, then multi-host persisted-state, restart/failover, mixed-version, migration, overflow, hot-shard, and failure cases prove stable identity, gapless aggregate sequence, and shard-local uniqueness/monotonicity.
- Given no exact-content human approval exists, when all agent-authored work is verified and committed, then the story is `awaiting-operator`, the current allocator remains authoritative, and downstream implementation/deployment/migration remains unauthorized.
- Given the re-drive baseline already contains the regenerated Epic 4 context, when the story is re-armed, then the superseded blocked-result artifact is not accepted as the current outcome, both candidate drafts are preserved and revalidated from the dev stage, and no restore patch or approval is inferred.

## Spec Change Log

- **Review loop 1 (2026-08-27, `bad_spec`):** Review found the first successor
  treated reservation-order counters as resumable committed-event watermarks,
  relied on a non-atomic epoch check while claiming no fencing dependency,
  could not distinguish new shard state from lost state, globally sealed v1
  before defining availability for non-canary pairs, and left wire, cursor,
  recovery, approval, and verification semantics under-specified. The task and
  design requirements now require a committed enumeration source independent
  of allocation counters, complete writer/sidecar quiescence rather than an
  optimistic check-before-save fence, a durable shard registry and lifecycle,
  one global all-pairs cutover after shadow evidence, exact decimal-string wire
  encoding, authenticated cursors and approvals, canonical fingerprints, and
  baseline-bound scope verification. **KEEP:** composite tenant+domain shard
  selection; immutable v1 bytes and authority; exact predecessor Git/SHA
  binding; clause-by-clause disposition; full tagged position identity;
  explicit unsupported cross-shard/cross-scheme ordering; no runtime or test
  changes; future persisted-state evidence matrix; detached exact-digest human
  approval; and planning-only approval effect.

- **Review loop 2 (2026-08-27, `bad_spec`):** Review confirmed loop 1 removed
  allocation-label cursors and non-atomic epoch fencing, but found the repaired
  successor still allowed registry admission to split across crash-prone
  records, left generation minting and recovery ceilings without one durable
  authority, provided no finite committed-source snapshot or v1-checkpoint
  migration, contradicted provisional rollback with permanent v1 revocation,
  and under-specified shard-set rebase, fingerprint framing, benchmark failure,
  approval supersession, and its own validators. The task and design notes now
  require an idempotent registry state machine; checked monotonic generations
  and immutable lineage; explicit provisional/irreversible authority phases;
  terminal committed-source tokens and cursor renewal; mandatory before-first
  migration; catch-up/rebase and retained-history rules; full tagged history
  fingerprint bytes; benchmark invalidation transitions; current immutable
  approval identity; and fail-closed content/scope verification. **KEEP:** every
  loop-1 KEEP item plus allocation-label-only positions; opaque committed-
  source cursors; 19 predecessor dispositions; canonical decimal-string
  counters; absent v2 `globalPosition`; explicit unknown comparison; durable
  never-allocated/active/retired lifecycle; global quiescent all-pairs cutover;
  no-runtime/test scope; and digest `a2a14a27...` only as historical evidence
  of the superseded loop-1 draft, never as an approval target.

- **Review loop 2 implementation (2026-08-27, superseded):** Created the content-bound v2
  successor with composite shard identity, an idempotent provisioning registry,
  monotonic generation lineage, provisional and irreversible authority phases,
  committed-source finite snapshots, before-first checkpoint migration,
  atomic shard-set rebase, fully framed mixed-history fingerprints, an
  invalidating capacity gate, and supersession-aware approval consumption. The
  resulting normative digest was
  `b521fab8ff96bf7e7b53377d6598981ec209bc2616ea59e622a779ab65c34530`.
  Review pass 3 found that draft still under-specified reservation atomicity,
  registry concurrency, pair admission, snapshot/fingerprint generations, and
  cutover recovery. Digest `b521fab8...` is historical evidence only and MUST
  NOT be accepted as the current approval target.

- **Review loop 3 (2026-08-27, `bad_spec`):** Review found the loop-2 successor
  split each reservation across an allocator counter and authoritative registry
  ceiling without an atomic or resumable protocol, risked recreating the global
  hot path in one registry authority, and left admission/cutover generation
  propagation, pre-v2 inventory discovery, collision-free physical keys, and
  post-cutover pair admission incomplete. It also left the complete metadata
  envelope, deterministic event-to-counter assignment, cursor pre/post-auth
  taxonomy and bounds, mixed-partition paging, immutable boundary identity,
  multi-generation fingerprint framing, restore/rollback ceilings, retired-pair
  behavior, benchmark measurement windows, approval-policy binding, and exact
  validators under-specified. The amended requirements now demand pair-local
  atomic reservation state with idempotent reservation identities; a partitioned
  registry/catalog concurrency model included in the benchmark; one explicit
  generation propagation path; a content-bound bootstrap inventory gate;
  length-framed physical keys; serialized post-cutover admission with captured
  admission boundaries and atomic shard-set publication; a complete metadata
  union and total comparison precedence; stable grouped source pages; immutable
  snapshot boundary identifiers; complete generation-set and persisted-history
  fingerprints; resumable restore/cutover phases; strict v1 counter continuation;
  reproducible rate-based capacity evidence; immutable role-policy evidence;
  and executable cross-document validators. The intent matrix's `durable v2
  write` / `durable v2 event` shorthand MUST be interpreted consistently as the
  first successful durable production reservation evidence, not provisioning
  metadata. **KEEP:** every loop-1 and loop-2 KEEP item; all 19 exact predecessor
  mappings and identities; composite tenant+domain selection as conditional on
  the capacity gate; allocation-label-only positions; immutable v1 history;
  unsupported cross-shard/cross-scheme ordering; no runtime, test, migration,
  topology, or sprint-status changes; finite committed-source snapshots;
  provisional rollback only before production reservation; exact-content human
  approval outside normative bytes; and planning-only approval effect.

- **Review loop 3 implementation (2026-08-27, superseded):** Re-derived the
  successor with pair-local atomic reservation state, a partitioned registry,
  evidenced bootstrap inventory, injective physical keys, strict metadata and
  comparison, grouped committed-source pages, multi-generation fingerprints,
  resumable restore/cutover, reproducible capacity gates, immutable role policy,
  and executable content checks. Its normative digest was
  `b73d0cff627394eb4c0fa165a22f0fa6864b123c06d53ca7fcdaf2bc259eda77`.
  Review pass 4 superseded it; that digest is historical evidence only and MUST
  NOT be approved.

- **Review loop 4 (2026-08-27, `bad_spec`):** Review found the loop-3 successor
  derived reservation identity from mutable recovery generations, bound only
  MessageIds rather than the complete planned event batch, and expired replay
  details without a deterministic late-retry outcome. It also left metadata and
  shard copies inconsistent, valid v1 and future-canonicalization comparison
  incomplete, pair/catalog admission and retirement cross-partition crashes
  under-specified, cursor indirection and long-rebuild liveness unaudited,
  fingerprint bytes and restore unions incomplete, and saturation confidence
  intervals undefined. Finally, benchmark/bootstrap evidence was incorrectly
  bound as though it lived in the two-file specification commit, approval did
  not bind native GitHub review provenance and the current protected blob,
  bootstrap completeness remained non-executable, physical-key testing stopped
  at the provider, diagnostics were unspecified, and the post-commit tab check
  was syntactically wrong. The amended requirements now demand a generation-
  independent command key with cross-lineage lookup; a canonical complete-batch
  digest and exact routing-copy equality; bounded retained range proofs and a
  permanent late-retry result; explicit v1/future-version comparison; durable
  prepare/publish/activate and retirement phase machines; fully specified
  cursor state storage, collision, failover, renewal, and shrink rules; an exact
  fingerprint grammar with vectors; monotonic union of every authoritative
  restore field; a reproducible saturation estimator; separate immutable spec
  and evidence commits; native GitHub API validation; executable bootstrap
  completeness; end-to-end key transport tests; structured diagnostics; and a
  literal-tab-safe scope validator. **KEEP:** all prior KEEP instructions plus
  loop-3's pair-local hot path, partitioned catalog, physical-key test vectors,
  strict metadata tagged union, grouped non-temporal history, immutable source
  boundary, multi-generation intent, resumable cutover, role-policy identity,
  exact 19-clause mapping, content digest validation, no runtime/test/topology/
  migration/sprint-status changes, and conditional fail-closed eligibility when
  no committed enumeration source or capacity evidence exists.

- **Review loop 4 implementation (2026-08-27, superseded):** Re-derived the
  successor with generation-independent command keys and full-batch binding,
  permanent retry tombstones, cross-partition lifecycle phases, recoverable
  cursor state, bootstrap schema, exact fingerprint vectors, full-state restore,
  separate specification/evidence identities, native GitHub provenance,
  structured diagnostics, and literal-tab-safe validation. After correcting its
  physical-key maximum from 205 to 204 bytes, its normative digest was
  `38f6eb00cd34aeaf7921c8678cfbbad86e2d4980c02d3bb8baf5c370cc924842`.
  Review pass 5 superseded it; that digest is historical evidence only and MUST
  NOT be approved.

- **Review loop 5 (2026-08-27, `bad_spec`):** Review found the loop-4 successor
  made benchmark seeding circular and did not define paired repetition ceilings,
  low-rate saturation, or the control-queue statistic. It also allowed valid
  event plans to exceed transaction bounds, missed sequence preflight and exact
  timestamp bytes, lost collision-verification bytes during compaction, and used
  a permanent finite tombstone cap that would eventually halt hot pairs. Catalog
  operation discovery, lifecycle exclusion, and shard-set identity were not
  fully reproducible; cursor schema, confidentiality, size, paging, key/state
  integrity, and worst-case rebuild liveness remained incomplete; bootstrap
  hashes and proof booleans lacked transcripts and executable custom formats;
  fingerprint inputs could alias persisted timestamps, partitions, or authority
  sets; and backup/restore lacked one consistent snapshot boundary. Finally,
  approval-policy removals, evidence-manifest shape, latest-review ordering, and
  detached-record immutability were incomplete, while the clause validator did
  not restrict dispositions to the normative range. The amended requirements
  now demand a pre-evidence seed and paired-run estimator; pre-allocation plan
  bounds, sequence and identity validation; scalable exact-key retry archival;
  CAS-discovered lifecycle operations and canonical shard-set bytes; strict
  cursor JSON/state schemas, exact AES-GCM wire framing, and confidentiality rules; transcript-bound
  bootstrap attestations; exact persisted timestamp bytes and source mappings;
  quiescent boundary-bound backups; immutable external approval records and
  manifests; prior/current-policy comparison; and normative-slice-only mapping
  validation. **KEEP:** every previous KEEP item plus the corrected 204-byte key
  maximum, generation-independent command key, full persisted-batch intent,
  pair-local atomic hot path, no catalog reservation update, finite grouped
  committed-source semantics, strict cursor authentication order, exact
  fingerprint tags/vectors, separate candidate/evidence identities, native
  GitHub provenance, all 19 predecessor dispositions, immutable v1 authority,
  no runtime/test/migration/topology/sprint-status changes, and fail-closed
  ineligibility until external evidence and human approval exist.

- **Review loop 5 implementation (2026-08-27, pre-review):** Re-derived the
  successor with a pre-evidence deterministic benchmark seed, paired repetition
  ceilings and numeric control-queue tests; bounded pre-allocation plans and
  exact persisted timestamp bytes; a scalable permanent collision-verification
  archive; CAS-discoverable lifecycle operations and canonical shard-set bytes;
  strict confidential cursor/state/page contracts; transcript-bound bootstrap;
  consistent-boundary backup and full-state restore; exact multi-generation
  fingerprint and planned-batch vectors; and immutable evidence/approval
  manifests with prior/current role-policy comparison. Its normative digest is
  `e7f0c0d58aecc4c4057cd3505322973a746e4bb9b0099b41c2d30fb5b9fed6da`.
  External evidence and exact-content human approval remain absent, so this
  digest authorizes no runtime, migration, deployment, or topology work.

- **Review loop 6 implementation (2026-08-27, awaiting operator):** Re-derived
  the successor from the spec and its declared context without importing an
  external review payload. The normative contract now uses a stable lifecycle
  locator and frozen operation identity, a crash-recoverable first-reservation
  permit instead of impossible cross-partition atomicity, and a resumable
  archive compaction protocol. It adds exact committed-source page schemas,
  confidential bounded cursor state, unique nonce allocation, a satisfiable
  cursor time contract, transcript-bound bootstrap and backup manifests,
  complete v1/v2 metadata shapes, unambiguous sequence/map fingerprint bytes,
  reproducible unbiased benchmark sampling and paired confidence gates,
  immutable evidence/approval locators, merged-PR provenance, and executable
  pre/post-commit scope validation. Its normative digest is
  `2310f121dc48f59713a9c6bbc6ffe2e63be374d4d8ecc6e8d710a0d9cf3674de`.
  External evidence and exact-content human approval remain absent; v1 remains
  authoritative and no runtime, migration, deployment, or topology work is
  authorized.

## Review Triage Log

### 2026-08-27 — Review pass
- intent_gap: 0
- bad_spec: 16: (high 9, medium 7, low 0)
- patch: 0
- defer: 0
- reject: 7: (high 0, medium 4, low 3)
- addressed_findings:
  - `[high]` `[bad_spec]` Allocation reservations can commit out of counter order; require a committed enumeration token independent of allocation counters and forbid counters as lossless cursors or watermarks.
  - `[high]` `[bad_spec]` Check-before-save epoch validation is not atomic and does not drain pre-seal v1 work; require complete host/sidecar/provider quiescence and permanent old-authority revocation before sealing.
  - `[high]` `[bad_spec]` Zero-reservation rollback can race an in-flight v2 allocation; require v2 quiescence, durable reservation evidence, and one-way authority revocation before reopening v1.
  - `[high]` `[bad_spec]` Lazy initialization cannot distinguish a new shard from deleted state and retirement can reuse tuples; require a durable registry with never-allocated, active, retired-tombstone, and recovery-generation states.
  - `[high]` `[bad_spec]` Global v1 sealing before post-seal canary expansion leaves non-canary pairs unavailable; move comparative/canary evidence before the seal and enable every registered pair under one cutover.
  - `[medium]` `[bad_spec]` JSON numbers lose 64-bit precision and v2 legacy-member presence was ambiguous; require canonical decimal strings and an explicit omitted legacy `globalPosition` member.
  - `[medium]` `[bad_spec]` Unknown-position comparisons lacked a result; define explicit unknown and invalid comparison outcomes.
  - `[high]` `[bad_spec]` Cursors were not bound to principal, audience, authorization policy, current reauthorization, or expiry handling; make all bindings and restart outcomes normative.
  - `[high]` `[bad_spec]` Multi-shard cursors lacked before-first state, finite-size handling, shard-set transition/liveness, and atomic behavior when a shard is unavailable; specify each fail-closed behavior.
  - `[high]` `[bad_spec]` Backup/restore freshness relied on undefined durable evidence; define monotonic recovery generations and their authoritative registry/state comparison.
  - `[medium]` `[bad_spec]` Rebuild fingerprints lacked canonical tuple encoding and unordered-input handling; define canonical bytes, sorting, duplicates, and shard-set binding.
  - `[high]` `[bad_spec]` Approval wording could elevate v2 authority and editable Markdown did not authenticate humans; require immutable external approval identity/role evidence while keeping approval planning-only.
  - `[medium]` `[bad_spec]` The performance decision used an undefined representative trace and threshold exception; specify source/window, concurrency, warm-up, repetitions, statistics, provider profile, and accountable exception authority.
  - `[medium]` `[bad_spec]` Paraphrased predecessor clauses had no stable identifiers or completeness proof; assign exact source-bound IDs and validate the full set.
  - `[medium]` `[bad_spec]` Allocation did not require count to equal the command's event count; make the equality a fail-closed invariant.
  - `[medium]` `[bad_spec]` Commit scope validation checked only `HEAD^..HEAD` and mixed pre/post-commit expectations; separate them and bind the candidate range to the recorded baseline.

### 2026-08-27 — Review pass 2
- intent_gap: 0
- bad_spec: 19: (high 12, medium 7, low 0)
- patch: 0
- defer: 0
- reject: 5: (high 0, medium 3, low 2)
- addressed_findings:
  - `[medium]` `[bad_spec]` Post-commit scope validation excluded deletions and did not prove a clean/untracked-free worktree; require an exact baseline-to-candidate name-status set including `D` plus a clean-tree check.
  - `[medium]` `[bad_spec]` The predecessor validator counted arbitrary clause IDs; bind the exact expected ID, source range, source-byte digest, and disposition mapping.
  - `[high]` `[bad_spec]` The composite benchmark gate lacked a reproducible failure transition, lower-bound handling, expiring exception semantics, and named authority; make any unsatisfied or expired gate invalidate the selection for implementation planning.
  - `[high]` `[bad_spec]` Composite identity depended on an unversioned external canonicalization policy; freeze exact case-sensitive UTF-8 bytes, normalization/rename behavior, bounds, and a canonicalization version.
  - `[high]` `[bad_spec]` Registry inventory and pair admission could split across crashes; define one idempotent provisioning/activation state machine with nonce-bound recovery and no allocation before activation.
  - `[high]` `[bad_spec]` Cutover, allocator, and recovery generations lacked one minting authority, checked bounds, immutable accepted lineage, and a trustworthy reservation ceiling; make the registry the checked monotonic authority and bind quiescent provider snapshots to pair-state ceilings.
  - `[high]` `[bad_spec]` Permanent v1 revocation contradicted pre-write rollback and unused retirement; distinguish provisional seal from irreversible first reservation, define rearm with a fresh generation, and classify retired-unallocated separately from allocated tombstones.
  - `[high]` `[bad_spec]` Cutover could proceed without a committed enumeration source while v1 scalar checkpoints cannot translate; require an approved source or an evidenced no-consumer mode and mandate before-first rebuild instead of scalar migration.
  - `[high]` `[bad_spec]` Repeated pages and full replays lacked a fixed committed-record boundary and renewable snapshot lease; bind terminal source tokens so identical cursors are idempotent and finite scans converge.
  - `[high]` `[bad_spec]` Shard admission, projection freshness, and retired history had conflicting snapshot semantics; define atomic catch-up/rebase, a latest-set restart result, and replay inclusion for every retained historical shard.
  - `[high]` `[bad_spec]` Projection checkpoint/read-model atomicity named no transaction or recovery boundary; require co-located atomic batch support or fail the projection mode as unsupported.
  - `[high]` `[bad_spec]` Fingerprint framing omitted header fields, v1 records, event identity/content, and duplicate base-position rules; define one fully framed tagged-union history encoding and integrity claim.
  - `[medium]` `[bad_spec]` Mixed-history multi-shard pages did not place v1 records; define an explicit `global-v1` committed-source partition and its non-temporal relation to v2 groups.
  - `[medium]` `[bad_spec]` Metadata duplicate properties could split parser identity; reject duplicates before schema and identity validation.
  - `[medium]` `[bad_spec]` Same-shard positions from different authority generations had no result; add an explicit cross-generation outcome and checked generation bounds.
  - `[medium]` `[bad_spec]` Cursor failures had no integrity-first precedence; authenticate the envelope before consuming claims, then apply one ordered error taxonomy.
  - `[high]` `[bad_spec]` Retirement could race a late allocation; revoke and quiesce routing authority before the terminal tombstone and read back the final pair counter.
  - `[high]` `[bad_spec]` Approval could survive a later change request, role removal, or login reassignment; require the latest actionable review, current role membership, and immutable GitHub user ID at consumption.
  - `[medium]` `[bad_spec]` Digest validation did not prove marker order, unique digest declaration, or strict UTF-8; harden all three checks before accepting the content binding.

### 2026-08-27 — Review pass 3
- intent_gap: 0
- bad_spec: 22: (high 15, medium 7, low 0)
- patch: 0
- defer: 0
- reject: 1: (high 0, medium 1, low 0)
- addressed_findings:
  - `[high]` `[bad_spec]` Allocator counters and registry ceilings were separate durable writes; require pair-local atomic state or a crash-resumable prepare/commit protocol keyed by an idempotent reservation identity.
  - `[high]` `[bad_spec]` A single registry update on every reservation could recreate the global bottleneck; define partitioned catalog/pair-record concurrency and benchmark the complete reservation path.
  - `[high]` `[bad_spec]` Admission and cutover could mint replacement generations without propagating them atomically to pair state; define one monotonic mint, installation, read-back, and activation transition.
  - `[high]` `[bad_spec]` The first v2 inventory had no authoritative discovery and freeze protocol; require a content-bound historical and active-pair inventory source, reconciliation, quiescence boundary, and fail-closed completeness proof.
  - `[high]` `[bad_spec]` Logical shard bytes had no collision-free physical actor/key encoding; freeze a versioned length-framed derivation and its bounds.
  - `[high]` `[bad_spec]` Post-irreversible pair admission, concurrent admissions, captured admission terminals, multi-shard rebase, and shard-set publication were incomplete; define one serialized admission state machine and atomic set transition.
  - `[medium]` `[bad_spec]` The v2 JSON example was not a complete versioned metadata union; specify the exact parent/version/member/casing/nullability and strict v1/v2 negotiation contract without implementing it here.
  - `[medium]` `[bad_spec]` A reserved range did not bind stable ordered events; bind ordered MessageIds before reservation and assign event index `i` to `firstCounter + i` across retries.
  - `[medium]` `[bad_spec]` Cursor determinism, size/time bounds, renewal, and integrity-first errors were internally incomplete; separate minimal pre-auth failures from ordered post-auth failures and bind authorization-state changes.
  - `[high]` `[bad_spec]` Mixed v1/v2 enumeration was unordered without a deterministic page shape or continuation traversal; require stable grouped partitions and token-bound traversal state.
  - `[high]` `[bad_spec]` Fingerprints hashed renewable token bytes; bind a source-issued immutable canonical boundary identifier instead.
  - `[high]` `[bad_spec]` Singular fingerprint canonicalization/cutover/recovery fields could not describe retained multi-generation history; encode canonical sorted authority and per-shard generation sets.
  - `[high]` `[bad_spec]` Fingerprint framing omitted relevant persisted metadata, length overflow, and aggregate-sequence conflict handling; bind all claimed persisted state or explicitly narrow the claim, and reject ambiguous inputs.
  - `[high]` `[bad_spec]` Pre-write rollback did not require the v1 allocator to resume strictly above the captured scalar ceiling; make the stored ceiling monotonic and the next grant strictly greater.
  - `[high]` `[bad_spec]` Restore lacked an idempotent phase record and precise stored/next-counter semantics; require resumable restore identity, phase, generation, ceiling reconciliation, and strict next-grant behavior.
  - `[medium]` `[bad_spec]` Retired-pair traffic and cutover treatment of retained tombstones were ambiguous; reject exact-identity reuse explicitly and distinguish allocatable active pairs from retained historical pairs.
  - `[high]` `[bad_spec]` Benchmark repetitions lacked measured windows, rate units, offered-load control, and saturation detection; make every threshold reproducible and include registry overhead.
  - `[high]` `[bad_spec]` Approval relied on a mutable unnamed role source and did not resolve conflicting current-owner decisions; bind an immutable role-policy identity and deterministic decision aggregation.
  - `[medium]` `[bad_spec]` Wrapper rollback shorthand conflicted with the normative first-reservation boundary; define its durable-write/event terms as first successful durable production reservation evidence.
  - `[medium]` `[bad_spec]` Verification asserted byte-range, frozen-block, allowlist, and digest consistency without recording all executable validators; include exact commands and cross-check wrapper, declaration, and computed digest.
  - `[medium]` `[bad_spec]` Comparison omitted cross-canonicalization and total outcome precedence; define both explicitly.
  - `[high]` `[bad_spec]` Cutover could crash between revocation, sealing, per-pair enablement, and irreversibility; require resumable durable phases and one atomic `all-pairs-enabled` allocation gate, validating tombstones separately.

### 2026-08-27 — Review pass 4
- intent_gap: 0
- bad_spec: 17: (high 11, medium 6, low 0)
- patch: 0
- defer: 0
- reject: 2: (high 0, medium 2, low 0)
- addressed_findings:
  - `[high]` `[bad_spec]` Reservation identity included mutable recovery generations; require one generation-independent command idempotency key and cross-lineage lookup before any new grant.
  - `[high]` `[bad_spec]` Retry identity bound only ordered MessageIds; bind a canonical digest of the full planned event batch, aggregate/sequence/type/content/extensions, and require all metadata, position, aggregate, and allocator shard bytes to agree.
  - `[high]` `[bad_spec]` Ledger details expired without preserving exact retry behavior; define the horizon, compact retained range proof, permanent late-retry result, lookup semantics, and provider-size limits.
  - `[medium]` `[bad_spec]` Comparison left positive v1 pairs, valid future canonicalization versions, and legacy zero-position fingerprinting ambiguous; define explicit branches and canonical encodings.
  - `[high]` `[bad_spec]` Pair activation, shard-set publication, retirement, and admission between all-pairs enablement and irreversibility crossed partitions without durable recovery; require persisted prepare/publish/activate or retire phases and allocation gates at every state.
  - `[high]` `[bad_spec]` Server-side cursor indirection had no provider, atomicity, authorization, retention, key-rotation, collision, failover, or cleanup contract; specify it or make oversize snapshots unsupported.
  - `[medium]` `[bad_spec]` A 24-hour absolute snapshot could livelock long rebuilds and retained-shard removal lacked a model-rebase rule; add measured completion eligibility, boundary-preserving continuation, and atomic shrink behavior.
  - `[high]` `[bad_spec]` Cursor validation could probe source state before caller authorization and omitted future-time/not-before handling; order authenticated caller/policy checks before source access and bound every timestamp.
  - `[high]` `[bad_spec]` Fingerprint tags, nesting, duplicate count, timestamp, extension-map, payload bytes, per-shard allocator lineage, and test vectors were incomplete; define an exact byte grammar and canonical examples.
  - `[high]` `[bad_spec]` Restore reconciled only ceilings; monotonically union and conflict-check every ledger/tombstone, MessageId binding, retired identity, accepted lineage, and per-pair field across multi-pair restores.
  - `[medium]` `[bad_spec]` The capacity gate referenced a saturation confidence bound without a saturation estimator or censored-run rule; define both reproducibly.
  - `[high]` `[bad_spec]` Bootstrap and benchmark artifacts could not live in the exact two-file spec commit; distinguish immutable specification commit/digest from later evidence commit/blob/digest and bind approval to both.
  - `[high]` `[bad_spec]` Approval trusted review-body JSON without native PR repository, commit, state, dismissal, head/base, submitted time, latest malformed decision, or current-main blob checks; bind all native provenance fail closed.
  - `[high]` `[bad_spec]` Bootstrap inventory lacked an exact schema, canonical digest, authoritative source completeness rules, and consistent precheck/finalize quiescence ordering; make the gate executable.
  - `[medium]` `[bad_spec]` Physical-key decoding did not reject noncanonical base64url and tests stopped at the state provider; require canonical re-encoding plus DAPR, routing, proxy, telemetry, and backing-key limits.
  - `[medium]` `[bad_spec]` Diagnostics could still present a misleading scalar order; define mandatory structured scheme/shard/generation/counter/comparison fields and forbidden global-order metrics or displays.
  - `[medium]` `[bad_spec]` The post-commit scope validator compared Git's tab-separated output to literal backslash-t text; use actual tab bytes and validate the command itself.

### 2026-08-27 — Review pass 5
- intent_gap: 0
- bad_spec: 13: (high 10, medium 3, low 0)
- patch: 0
- defer: 0
- reject: 2: (high 0, medium 2, low 0)
- addressed_findings:
  - `[high]` `[bad_spec]` Capacity evidence seeded its estimator with its own commit and did not pair repetitions across rates, define all-lowest-rate saturation, or quantify control-plane queue stability; use a pre-evidence input identity and exact paired estimators/statistics.
  - `[medium]` `[bad_spec]` Recognized future schemes could fall through composite comparison fields; require each immutable comparison-registry entry to define its own schema and comparator, with current v2 dispatched explicitly.
  - `[high]` `[bad_spec]` Valid plans could exceed the 64-KiB transaction detail and omitted pre-allocation aggregate-sequence overflow/contiguity, distinct MessageIds, and exact persisted timestamp bytes; bound and validate every plan before counter mutation or use an authorized external detail protocol.
  - `[high]` `[bad_spec]` Compaction discarded collision-verification bytes, had an ambiguous horizon race and activation-only scheduling, and a permanent finite tombstone cap that eventually halted busy pairs; retain exact framed keys in a scalable authoritative archive/index with deterministic boundary and continuous compaction.
  - `[high]` `[bad_spec]` Concurrent admission/retirement lacked deterministic operation discovery, CAS conflict rules, late-arrival behavior, and same-pair lifecycle exclusion; define stable operation keys, leader acquisition, serialized successor operations, and pair-generation guards.
  - `[high]` `[bad_spec]` Shard-set identity had no canonical membership bytes or digest; define reproducible active/retired membership, generation, set-transition, and source-boundary encoding used consistently by catalog, cursor, and fingerprint.
  - `[high]` `[bad_spec]` Paging and cursor contracts lacked split-group/oversize-record behavior, exact JSON member schemas, total token bounds, confidentiality rules, header/payload key consistency, and authenticated server-state bytes; make every representation and failure executable.
  - `[medium]` `[bad_spec]` P99-only rebuild eligibility left tail executions without a lease path; require worst-case or all-run boundary-preserving renewal evidence and deterministic shrink rebase after retained history expires.
  - `[high]` `[bad_spec]` Bootstrap precheck/source hashes, count bounds, capture ordering, source independence, custom formats, and completeness proof were not reproducible; bind exact filtered and union grammars plus immutable transcripts, drain evidence, and signed attestations rather than booleans alone.
  - `[high]` `[bad_spec]` Fingerprints canonicalized logical timestamps instead of exact persisted bytes and did not validate record-to-source partition or authoritative header sets; bind exact bytes, partition mapping, and equality to snapshot authority.
  - `[high]` `[bad_spec]` Backup could mix independently partitioned states and restore referenced an undefined registry ceiling while omitting cutover permit/control union; quiesce or use one immutable boundary and reconcile pair-local ceilings plus all control authority before reactivation.
  - `[high]` `[bad_spec]` Approval could forget a removed dissenting owner, mutate the reviewed successor through its detached record, and lacked exact evidence-manifest and latest-review ordering rules; compare prior/current policies and store immutable approval evidence outside the candidate file.
  - `[medium]` `[bad_spec]` The predecessor-row validator scanned the whole successor, allowing detached disposition rows to satisfy validation; restrict parsing to the normative byte slice and reject disposition-shaped rows outside it.

### 2026-08-27 — Review pass 6
- intent_gap: 0
- bad_spec: 29: (high 19, medium 10, low 0)
- patch: 0
- defer: 0
- reject: 1: (high 0, medium 0, low 1)
- addressed_findings:
  - none

## Design Notes

Composite tenant+domain sharding uses identity already present on every event and aggregate. It avoids cross-tenant coupling in domain-only shards and hot-tenant contention in tenant-only shards. A position is a versioned branded tuple, never an encoded sortable scalar; multi-shard checkpoints are bounded vectors or opaque protocol values rather than `Max(long)`.

Re-derivation must apply these constraints:

1. A v2 position is an allocation label only. Reservation order is not commit
   order, so neither its counter nor a vector of counters is a lossless
   committed-event cursor. Single- and multi-shard cursors use authenticated,
   opaque continuation tokens from a separately identified committed
   enumeration source; if no such source is approved, position-based resume is
   explicitly unsupported.
2. Cutover uses a unique monotonic generation and a durable registry. Before
   sealing v1, every old writer and sidecar is stopped, provider operations are
   observed quiescent, old credentials/routing are revoked independently of
   application code, and the final v1 reservation ceiling is read back. This
   specification must not substitute a non-atomic epoch check for fencing or
   claim approval itself changes production authority.
3. The registry owns exact pair admission and states `never-allocated`,
   `active`, and permanently retained `retired`; records authoritative counter
   generation, successful-reservation evidence, cutover generation, and
   recovery generation; and prevents lazy initialization, restore, retirement,
   or recreation from reusing a tuple. Shadow/capacity canaries occur before a
   single global seal; after the seal, every registered active pair is v2-
   enabled so non-canary traffic is not stranded.
4. After stopping and draining all v2 authorities, v1 may reopen only when the
   registry and every admitted pair positively prove no successful v2
   reservation. Any reservation, event, missing record, stale generation, or
   incomplete inventory makes cutover one-way and requires forward recovery.
5. Persist v2 counters as canonical base-10 strings, omit the legacy
   `globalPosition` member for metadata v2, require allocation count to equal
   event count, and return explicit results for unknown, invalid, cross-shard,
   and cross-scheme comparison.
6. Cursor contracts bind mode, principal, audience, current authorization
   scope/policy version, expiry, committed source, cutover generation, shard-set
   snapshot, query scope, and integrity. Define a before-first state, bounded
   vector/server-side indirection, deterministic shard admission transition,
   restart outcomes, and no checkpoint advance on unavailable or partial pages.
7. Canonical rebuild fingerprints bind protocol, shard-set identity, full
   tagged tuples encoded as length-prefixed UTF-8 plus unsigned decimal counter,
   deterministic tuple order, and duplicate treatment. Recovery evidence uses
   an authoritative monotonic generation shared by registry, allocator state,
   events, and backups.
8. Approval records remain outside normative bytes but must reference immutable
   external evidence whose authenticated login is authorized for the named
   role and whose exact candidate commit and digest reproduce. Approval changes
   only downstream planning eligibility; a separate story authorizes every
   implementation or cutover.
9. The option benchmark defines trace provenance/window, concurrency, warm-up,
   repetitions, percentile/statistical method, provider profile, raw evidence,
   capacity ceiling, and named exception authority. Predecessor rows use stable
   source-bound IDs and a machine-checkable complete set.
10. Freeze shard canonicalization as a versioned byte contract: exact
    case-sensitive persisted TenantId and Domain UTF-8, no trimming, aliasing,
    Unicode normalization, or rename-in-place. A rename creates a new shard;
    the prior shard remains a retained historical identity.
11. One durable registry authority mints checked positive Int64 cutover and
    recovery generations and preserves accepted lineage. Pair admission is an
    idempotent `provisioning -> active` protocol with a nonce: allocator state
    is created/read back during provisioning, allocation remains forbidden
    until activation, and crash recovery resumes the same nonce. Quiescent
    provider snapshots bind registry and pair-state counters; events never
    reconstruct missing reservation ceilings.
12. Authority states distinguish `v1-active`, `v1-provisionally-sealed`, and
    `v2-irreversible`. Pre-write rollback first quiesces/revokes v2, positively
    proves every pair unallocated, then rearms v1 under a fresh monotonic
    generation. The first reservation is irreversible. Retired-unallocated
    pairs preserve tombstones without pretending a write occurred; retirement
    revokes and drains routing before reading back and sealing the counter.
13. V2 cutover requires either an approved committed-enumeration source with
    all consumers migrated or a content-bound proof that no enabled consumer
    needs cross-aggregate resume and every such surface rejects v2. The source
    issues immutable finite-snapshot terminal tokens; identical cursor+query
    inputs reproduce the same page; renewal extends expiry without changing the
    snapshot. Persisted v1 scalar checkpoints never translate and require a
    before-first rebuild or a separately proved source-specific mapping.
14. Snapshot semantics exclude later shard admissions from an existing finite
    scan but mark long-lived freshness as the old set. A projection adopts a
    new set only after catching the new shard from before-first through its
    admission terminal token and atomically rebasing model, checkpoint, and set
    identity. Retired shards remain in all new replay snapshots while retained
    events exist. If checkpoint and read model cannot share one atomic batch or
    an idempotent recovery protocol, that projection mode is unsupported.
15. Fingerprint bytes frame every header field and a tagged union of v1/v2
    records. Each record binds position, MessageId, aggregate identity,
    aggregate sequence, and payload/content digest; source, generations, and
    shard-set are length-prefixed before the sorted record sequence. Duplicate
    base position with conflicting provenance/content is invalid. Mixed-history
    enumeration places v1 in an explicit `global-v1` source partition, unordered
    relative to v2 groups.
16. Reject duplicate JSON properties before schema validation; bound every
    generation to positive Int64 and fail closed on overflow; define
    `UnsupportedCrossGeneration`; and verify cursor integrity before reading
    claims, then apply one documented mismatch/authorization/expiry precedence.
17. Capacity gates record a tested lower bound if saturation is not reached and
    require production peak below half that bound. Any failed/expired gate
    invalidates implementation eligibility until the architecture is reapproved.
    Approval consumption fetches all reviews, requires the approver's latest
    actionable decision to approve, validates current role membership, and
    binds immutable GitHub user ID as well as login.
18. Content validation strictly decodes UTF-8, proves begin-before-end and one
    digest declaration, verifies the exact 19-ID/source-range/source-byte-hash/
    disposition mapping, and checks baseline-to-candidate name-status including
    deletions plus an empty tracked and untracked worktree after commit.
19. Reservation durability is pair-local: the counter ceiling, successful-
    reservation flag, reservation identity, ordered MessageId binding, and
    accepted generations commit atomically in one partition, or an explicit
    persisted prepare/commit/reconcile protocol resumes every crash point. The
    global catalog is never updated synchronously per reservation. Its logical
    authority may use independently keyed pair records, but admission/catalog
    serialization and every pair-local write are measured in the full capacity
    path.
20. Bootstrap inventory names a content-bound authoritative source for every
    historical and currently admissible TenantId/Domain pair, reconciles it
    against the active ingress inventory while v1 commands are quiesced, and
    freezes an immutable complete set. Missing, concurrent, or unverifiable
    pairs fail the gate. Events may inform this one-time evidenced import but
    never reconstruct missing registry state after activation.
21. Physical shard identity uses an exact versioned, collision-free derivation
    from the canonical bytes (length framing plus a named digest/encoding), with
    provider-length bounds and test vectors. Admission installs exactly one
    minted allocator/recovery lineage into pair state before read-back and
    activation; cutover MUST NOT remint unexplained replacements.
22. After irreversible cutover, a triggering command for a new pair receives a
    deterministic retryable provisioning result until serialized admission has
    captured the source's immutable before-allocation admission boundary,
    installed/verified pair state, and atomically published the new shard-set
    identity. One transition handles any number of concurrent new shards;
    allocations remain gated until the transition commits.
23. The successor defines the complete strict v1/v2 metadata tagged union and
    comparison precedence, including cross-canonicalization. It binds ordered
    MessageIds before reservation and stamps event `i` with `firstCounter + i`.
    Cursor rules bound page size, clock skew, lifetime, expiry and renewal;
    separate pre-auth parse/integrity errors from post-auth claim errors; qualify
    page determinism by current authorization state; and define grouped mixed-
    partition response order and continuation state without implying time order.
24. Finite snapshots expose a stable canonical boundary identity distinct from
    renewable signed token bytes. Fingerprints encode sorted canonicalization,
    cutover, allocator, and per-shard recovery-generation sets; reject framing
    lengths beyond UInt32; reject conflicting aggregate identity+sequence; and
    bind every persisted field included in the declared integrity claim. If the
    fingerprint intentionally excludes a field, narrow the claim explicitly.
25. Restore is a durable idempotent state machine keyed by restore identity and
    persisted phase/target generation. The restored stored ceiling is at least
    the authoritative ceiling and its next granted counter is strictly greater.
    Pre-write rollback likewise restores v1's stored ceiling to at least its
    captured final scalar ceiling and grants only a strictly greater value.
26. Retired tombstones are historical inventory, not active cutover targets.
    Resumed traffic for the exact retired identity is rejected permanently and
    requires an explicitly new canonical identity; no alias silently bypasses
    the tombstone. Cutover validates active allocatable pairs separately from
    retained tombstones.
27. Capacity evidence fixes measured repetition duration/operation count,
    throughput units, offered-load control, saturation detection, and confidence
    treatment, and includes catalog admission plus pair-local reservation work.
    Approval binds the exact immutable role-policy path, commit/blob/digest, and
    defines the outcome when multiple current architecture owners have
    conflicting latest actionable decisions.
28. Cutover persists resumable `quiescing`, `provisionally-sealed`,
    `all-pairs-enabled`, and `irreversible` phases. Allocation remains globally
    gated until one durable all-pairs-enabled transition follows complete pair
    read-back. Exact validators recompute every predecessor byte range and
    frozen digest, compare wrapper/declaration/computed normative digests, and
    prove the complete pre/post-commit path allowlist.
29. Command idempotency is independent of cutover, allocator, and recovery
    generations. Before any reservation, every accepted lineage is searched by
    the stable command key. A canonical complete planned-batch digest binds
    aggregate identity, exact ordered MessageIds, sequence, event type, payload,
    extensions, and all persisted metadata; any retry difference is permanent
    conflict. Metadata TenantId/Domain, position tuple, aggregate routing, and
    allocator canonical bytes MUST be byte-identical.
30. Reservation range/detail proofs remain reproducible for the explicit
    command-idempotency horizon, whose duration and provider capacity are
    bounded and evidenced. Compaction retains a permanent key tombstone plus a
    deterministic `ReservationExpiredPermanent` outcome after range details are
    gone; it never allocates again. The successor specifies maximum ledger size,
    compaction concurrency, and recovery behavior.
31. Structural position validity is distinct from the recognized
    canonicalization-version registry. Positive v1-to-v1 scalar comparison has
    an explicit branch; valid recognized future canonicalizations can produce
    `UnsupportedCrossCanonicalization`; legacy v1 zero has one canonical
    unknown-position fingerprint representation.
32. Catalog/pair admission and retirement use durable cross-partition phase
    records with prepare, pair install/drain, publish/seal, activation, and
    reconcile states. Allocation checks both pair activation and the published
    catalog/set generation. New pairs during `all-pairs-enabled` but before the
    first reservation follow the same serialized admission or fail with the
    documented retry result; no state is inferred from partial writes.
33. Any server-side cursor indirection names an authorized durable provider and
    defines authenticated state shape, compare-and-set creation with random-ID
    collision retry, replication/failover, integrity-key rotation, authorization
    binding, TTL, cleanup, and unavailable-state outcome. Snapshot liveness has
    a measured completion gate and boundary-preserving successor mechanism for
    long rebuilds. Removing an expired retained shard atomically rebases or
    removes its read-model contribution before publishing the smaller set.
34. Cursor timestamp and failure precedence rejects future issued/not-before,
    inverted, overlong, expired, or late-renewal values within a fixed skew;
    authenticated audience, principal, and current policy checks occur before
    any committed-source availability probe. Fingerprints define exact tags,
    nesting, set/record counts after deduplication, timestamp bytes, extension-
    map canonicalization, persisted payload byte scope, per-shard allocator and
    recovery lineage, unknown v1 encoding, and complete test vectors.
35. Restore unions every authoritative field monotonically for every target
    pair: ceilings, reservation proofs and tombstones, MessageId/batch bindings,
    retired identities, catalog/set state, and all accepted lineage. Any
    conflicting equal key or missing newer entry fails closed. Capacity evidence
    defines the per-repetition saturation estimator, resampling unit, censored
    lower-bound treatment, and confidence interval used by the gate.
36. The specification candidate commit/digest is distinct from later capacity,
    bootstrap, or implementation evidence commit/blob/digests. Exact approval
    binds both immutable identities without requiring evidence inside the
    two-file candidate. Native GitHub validation binds repository/PR, review
    `commit_id`, state, dismissal, head/base, submitted/updated time, current
    main successor blob, and the latest actionable decision; malformed or
    unavailable latest evidence fails closed.
37. Bootstrap evidence defines an exact strict JSON schema, canonical bytes and
    digest, named historical and active-ingress source interfaces, independent
    completeness proofs, and precheck-before-quiescence plus final freeze-during-
    quiescence. Physical-key validation rejects noncanonical encodings and tests
    maximum identities through actor APIs, HTTP/gRPC, sidecars, proxies,
    telemetry, and backing-key composition. Diagnostics expose structured
    scheme, canonicalization, shard, generations, counter, and comparison
    outcome while forbidding implied global order. Post-commit validation uses
    actual tab bytes and a self-test that would fail for literal `\\t`.
38. Benchmark inputs have a content identity that exists before the evidence
    commit and supplies the deterministic seed. Repetition index `1..N` binds
    the same trace shuffle across every offered rate and option; each index
    yields one stable ceiling. The estimator defines no-passing-lowest-rate and
    right-censored outcomes, resampling unit, interval, and a numeric control-
    queue slope/observation test for every catalog partition.
39. Before reservation, exact serialized detail size, event count, payload and
    string bounds, distinct ordered MessageIds, and `startingSequence + i`
    positive-Int64 contiguity are checked with no mutation. Planned and
    fingerprint bytes bind the exact persisted RFC-3339 timestamp UTF-8 as well
    as parsed time semantics. Oversize plans return one named permanent result
    unless a separately authorized content-addressed detail provider is bound.
40. Compaction preserves full collision-verification command bytes or an
    equivalently injective content-addressed record in a scalable authoritative
    archive/index. Lookup covers live detail and archive before allocation;
    archived records cannot be evicted merely for capacity. Horizon equality,
    scheduling independent of actor activation, backpressure, partitioning,
    provider limits, and disaster recovery are exact and measurable.
41. Admission and retirement operation identities derive canonically from
    catalog version plus sorted request set and lifecycle generation. CAS leader
    acquisition, join-before-publish, queue-after-publish, same-pair mutual
    exclusion, and deterministic reconciliation are explicit. Shard-set identity
    is SHA-256 over exact versioned bytes for sorted active and retained members,
    physical keys, membership/lifecycle generations, transition ID, and source
    boundary; every catalog, cursor, projection, and fingerprint validates it.
42. Committed-source pages may split a group only at a source token and return a
    named permanent record-too-large result before emitting a page. Cursor header,
    payload, state, renewal, and traversal use exact strict schemas, member names,
    types, byte encodings, a total pre-parse size limit, and matching key IDs.
    State integrity authenticates JCS bytes plus state ID. Either all inline
    claims are classified non-secret with an explicit disclosure contract, or
    an authenticated-encryption profile is exact; “opaque” never implies secrecy.
43. Every measured rebuild, not only p99, completes inside the lease/recovery
    window or proves the boundary-preserving successor path. Bootstrap schemas
    distinguish precheck, per-source filtered, and final-union pair lists and
    exact hash grammars; cap counts at UInt32; order timestamps strictly; define
    custom-format algorithms; require independent authority IDs; and reference
    immutable enumeration transcripts, quiescence/drain artifacts, and signed
    attestations whose identities are themselves validated.
44. Fingerprints include exact persisted timestamp UTF-8 and prove each event's
    committed-source partition from canonical identity. Header sets equal the
    snapshot's complete authoritative lineage, including eventless registered
    shards. Backup first revokes/drains affected allocation or uses one provider-
    proven consistent boundary binding catalog, generation, operation log,
    cursor state, pair ceilings/ledgers, permits, routing, source, and tombstones;
    restore unions those fields and reactivates only after full read-back.
45. Role-policy verification compares the candidate-bound policy with current
    policy and treats removal, addition, or changed dissent as requiring a new
    candidate approval. Evidence and approval records live in immutable external
    artifacts, never by editing the approved successor; their exact strict
    schema, path/URI, canonical bytes, complete required artifact set, and digest
    validation are normative. Native review order uses `submitted_at` then review
    ID; malformed latest actionable evidence fails closed. Content validation
    parses the 19 disposition rows only from normative bytes and rejects any
    disposition-shaped row outside that range.

## Verification

**Commands:**
- `python3 -c 'from pathlib import Path; import hashlib, re; p=Path("_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md").read_bytes(); p.decode("utf-8", errors="strict"); b=b"<!-- HX-GPOS-V2-NORMATIVE-BEGIN -->\n"; e=b"<!-- HX-GPOS-V2-NORMATIVE-END -->\n"; assert p.count(b)==p.count(e)==1 and p.index(b)<p.index(e) and b"\r" not in p and not p.startswith(b"\xef\xbb\xbf"); assert len(re.findall(rb"Normative content SHA-256 \| `([0-9a-f]{64})`", p))==1; body=p[p.index(b)+len(b):p.index(e)]; declared=re.search(rb"Normative content SHA-256 \| `([0-9a-f]{64})`", p).group(1).decode(); assert hashlib.sha256(body).hexdigest()==declared; print(declared)'` -- expected: the sole declared digest matches the ordered unique strict-UTF-8 LF/no-BOM normative range.
- `python3 -c 'from pathlib import Path; import re; s=re.sub(r"\s+", " ", Path("_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md").read_text()); required=("allocation label only","committed enumeration","provisioning","v1-provisionally-sealed","v2-irreversible","terminal token","before-first rebuild","global-v1","UnsupportedCrossGeneration","latest actionable","immutable GitHub user ID","current role membership"); missing=[x for x in required if x not in s]; assert not missing, missing; print("review-loop-2 guardrails: OK")'` -- expected: every repaired normative guardrail is present.
- `python3 -c 'from pathlib import Path; import re; s=Path("_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering-v2.md").read_text(); expected={"V1-PROBLEM-01","V1-PROBLEM-02","V1-APPROACH-01","V1-APPROACH-02","V1-APPROACH-03",*(f"V1-ALWAYS-{n:02d}" for n in range(1,5)),*(f"V1-ASK-{n:02d}" for n in range(1,4)),*(f"V1-NEVER-{n:02d}" for n in range(1,4)),*(f"V1-MATRIX-{n:02d}" for n in range(1,5))}; rows=re.findall(r"\| `(V1-[A-Z]+-[0-9]{2})` \| `([^`]+)` \| `([0-9a-f]{64})` \| ([^|]+) \|", s); assert {r[0] for r in rows}==expected and len(rows)==19 and all(r[1].strip() and r[3].strip() for r in rows), rows; print("predecessor mapping: 19/19 exact IDs with ranges and hashes")'` -- expected: the complete exact predecessor mapping is present; the implementation evidence must also recompute every row hash from predecessor bytes.
- `git rev-parse HEAD:_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering.md` and `git hash-object _bmad-output/implementation-artifacts/spec-dapr-global-event-ordering.md` -- expected: both return predecessor blob `4c9edb37a8616aa373bd0054057c9e8eace6e0fa`.
- `sha256sum _bmad-output/implementation-artifacts/spec-dapr-global-event-ordering.md` plus exact inner-block and complete-element SHA-256 checks -- expected: full file `4bcff794a3c926f45e790ce462d562799db998feeab24aab1527ac6cf9ef1893`, frozen inner block `90be324c35d1545fd7c4dd53393ef27b08d2e6a3891d1bc9c6f38c9145740c10`, and complete frozen element `c827761ba1f58aa6fde85ca8acedfdfdcc5097cbcbd470d2887a1e4d073d5d2c`.
- `git diff --quiet HEAD -- _bmad-output/implementation-artifacts/spec-dapr-global-event-ordering.md _bmad-output/implementation-artifacts/sprint-status.yaml src tests .github` plus an exact untracked-path allowlist and `git diff --no-index --check /dev/null <file>` for each story file -- expected before commit: only the wrapper and v2 successor exist as clean changes.
- `git diff --name-status --diff-filter=ACDMRTUXB 5ddda34f2ff0ffb0f72a60c44b265f2e4838a332..HEAD` plus `test -z "$(git status --porcelain --untracked-files=all)"` -- expected after commit: exactly two added files (the wrapper and v2 successor), with no deletion or other tracked/untracked change.

**Review-loop-2 implementation evidence (2026-08-27):**

- Normative content validation passed with digest
  `b521fab8ff96bf7e7b53377d6598981ec209bc2616ea59e622a779ab65c34530`;
  strict UTF-8, LF/no-BOM, unique ordered markers, and the sole declaration all
  reproduced.
- The review-loop-2 guardrail command passed, including allocation-label-only,
  committed-enumeration, registry, authority-phase, terminal-token,
  before-first, mixed-history, comparison, and approval requirements.
- The intent-matrix audit passed 5/5 against the normative same-shard,
  cross-shard, mixed-history, partial-fleet, and rollback clauses.
- The predecessor mapping command passed with 19/19 exact IDs. A second
  byte-range validator parsed every `L<line>:B<start>-B<end>` range against the
  predecessor and reproduced all 19 source-byte hashes.
- `git rev-parse` and `git hash-object` both returned
  `4c9edb37a8616aa373bd0054057c9e8eace6e0fa`. Complete-file, frozen-inner, and
  complete-frozen-element SHA-256 validation reproduced all three declared
  predecessor identities.
- The protected-path pre-commit diff command returned exit 0. The exact
  untracked allowlist contains only this wrapper and the v2 successor.
  `git diff --no-index --check /dev/null <file>` emitted no whitespace errors
  for either file; its exit 1 is the expected no-index content-difference
  result.
- The baseline-to-candidate command was executed before commit: the range is
  empty because `HEAD` still equals the recorded baseline, and the clean-tree
  check correctly returned exit 1 for the two allowed untracked story files.
  After reviewed commit, rerun the exact command and require two `A` rows plus
  an empty tracked and untracked worktree before checking the second task or
  changing this wrapper to `awaiting-operator`.

**Review-loop-3 implementation evidence (2026-08-27, pre-review):**

- Created the re-derived successor with pair-local atomic reservation identity,
  partitioned registry/catalog authority, content-bound bootstrap inventory,
  injective physical keys and test vectors, serialized post-cutover admission,
  a strict generation-bearing metadata union, deterministic grouped committed-
  source pages, immutable snapshot boundaries, multi-generation fingerprints,
  idempotent restore, strict v1 ceiling continuation, reproducible capacity
  evidence, immutable role-policy binding, and resumable all-pairs cutover.
- Strict UTF-8, LF/no-BOM, unique ordered normative markers, the sole digest
  declaration, v2 frontmatter, and this wrapper all reproduced normative digest
  `b73d0cff627394eb4c0fa165a22f0fa6864b123c06d53ca7fcdaf2bc259eda77`.
- The exact 19-ID predecessor table passed both the declaration-set validator
  and the byte-range recomputation validator. Predecessor Git blob, complete
  file SHA-256, frozen inner-block SHA-256, and complete frozen-element SHA-256
  all reproduced their declared identities.
- The review-loop guardrail command passed, including allocation-label-only,
  committed-enumeration, provisioning, authority-phase, terminal-token,
  before-first, mixed-history, comparison, and authenticated approval terms.
- The protected-path and complete tracked diffs were empty. The exact untracked
  allowlist contains only this wrapper and the v2 successor, and both no-index
  whitespace checks emitted no errors with the expected content-difference exit
  status.
- The baseline-to-candidate name-status range is empty because `HEAD` still
  equals the recorded baseline. The non-empty clean-tree check correctly proves
  the two allowed files remain untracked before review. The second task, final
  status transition, operator actions, and two-added-file clean-tree gate remain
  intentionally pending reviewed commit.

**Review-loop-4 implementation evidence (2026-08-27, pre-review):**

- Re-derived the successor from the frozen predecessor after the loop-3 draft
  was superseded. The new contract defines a generation-independent stable
  command key, exact planned and persisted batch bytes, bounded replay detail
  and permanent late-retry tombstones, complete metadata/copy validation,
  explicit v1/future-canonicalization comparison, durable admission/retirement
  phases, cursor-state failover and liveness, full restore union, structured
  diagnostics, separate specification/evidence identities, and native GitHub
  review provenance.
- Strict UTF-8, LF/no-BOM, marker order/uniqueness, the sole declaration, v2
  frontmatter, and this wrapper reproduced normative digest
  `38f6eb00cd34aeaf7921c8678cfbbad86e2d4980c02d3bb8baf5c370cc924842`.
- The exact predecessor Git blob and complete file, frozen inner-block, and
  complete frozen-element SHA-256 values reproduced. All 19 source ranges
  reproduced their declared byte hashes and had a non-empty disposition.
- The strict bootstrap JSON Schema parsed successfully. Independent generators
  reproduced both physical-key vectors, the exact maximum arithmetic (142
  canonical bytes, 190 unpadded base64url bytes, 14 prefix bytes, 204 total
  ASCII bytes), and the 117-byte empty and 713-byte mixed-history fingerprint
  vectors and SHA-256 values.
- The loop-4 requirement audit passed for command/batch idempotency, compaction,
  comparison, cross-partition phases, cursor indirection/authorization/liveness,
  fingerprinting, restore, saturation confidence, evidence separation, native
  approval, diagnostics, and the actual-tab validator self-test.
- Tracked and protected diffs are empty. The exact untracked allowlist contains
  only this wrapper and the v2 successor; both no-index whitespace checks have
  the expected content-difference exit and no whitespace output. The
  baseline-to-candidate range remains empty because `HEAD` is still the recorded
  baseline. Reviewed commit, the second task, `awaiting-operator` transition,
  imperative operator actions, and the clean two-added-file post-commit gate
  remain intentionally pending.

**Review-loop-5 implementation evidence (2026-08-27, pre-review):**

- Strict UTF-8, LF/no-BOM, unique ordered markers, frontmatter/declaration
  equality, and direct normative-range hashing reproduced
  `e7f0c0d58aecc4c4057cd3505322973a746e4bb9b0099b41c2d30fb5b9fed6da`
  over 64,580 normative bytes. The fresh dev-stage re-drive also executed the
  mandated exact guardrail command, corrected its two missing normative phrases
  (`allocation label only` and `immutable GitHub user ID`), and re-established
  wrapper/frontmatter/declaration equality at the digest above.
- Closure review reduced aggregate sequence validation to one non-weakened
  preflight and froze one binary cursor envelope for both payload modes:
  `HXGC`/v2, big-endian header and ciphertext lengths, exact header JCS as AAD,
  96-bit nonce, ciphertext of the payload JCS, and 128-bit tag. The audit also
  proved the 8,192-raw-byte / 10,923-unpadded-character relationship and
  fail-closed rejection of noncanonical encoding, trailing bytes, truncation,
  and length mismatches before allocation, state, or source access.
- Predecessor blob `4c9edb37a8616aa373bd0054057c9e8eace6e0fa`, complete-file SHA-256,
  frozen-inner SHA-256, and complete-frozen-element SHA-256 all reproduced.
  The validator parsed only the normative slice and recomputed all 19 exact
  source ranges, hashes, and non-empty dispositions; no disposition row exists
  outside that slice.
- Canonical vector validation decoded and hashed the 225-byte empty and
  2,124-byte mixed multi-generation fingerprints plus both timestamp-sensitive
  planned batches. Physical-key validation reproduced the small vector and the
  exact 142-byte frame / 190-byte encoding / 204-byte maximum key.
- The loop-5 guardrail audit passed for non-circular paired capacity evidence,
  lowest-rate and control-queue failure, bounded plan preflight, permanent retry
  archival, CAS lifecycle exclusion, shard-set identity, encrypted bounded
  cursor state, oversize paging, worst-case rebuild liveness, transcript-bound
  bootstrap, exact timestamp/source fingerprints, consistent backup, role-policy
  evolution, latest-review ordering, and actual-tab scope validation.
- Protected tracked paths are unchanged. The exact pre-commit untracked
  allowlist contains only this wrapper and the v2 successor. Both no-index
  whitespace checks emitted no errors with the expected content-difference exit.
  The baseline-to-candidate range remains empty because `HEAD` still equals the
  recorded baseline. Reviewed commit, the second task, `awaiting-operator`
  transition, imperative operator actions, and the clean two-added-file
  post-commit gate remain intentionally pending.

**Review-loop-6 implementation evidence (2026-08-27, candidate):**

- Strict UTF-8, LF/no-BOM, unique ordered normative markers, successor
  frontmatter/content-table equality, wrapper equality, and direct range hashing
  reproduced 82,892 normative bytes and digest
  `2310f121dc48f59713a9c6bbc6ffe2e63be374d4d8ecc6e8d710a0d9cf3674de`.
  The wrapper is `awaiting-operator`, all execution tasks are checked, and its
  three imperative operator actions retain v1 authority while commissioning
  evidence, native approval, and a separately authorized implementation story.
- Predecessor worktree and baseline Git blobs both reproduced
  `4c9edb37a8616aa373bd0054057c9e8eace6e0fa`. Complete-file, frozen-inner,
  and complete-frozen-element SHA-256 values reproduced. A normative-slice-only
  parser found exactly the required 19 disposition IDs, recomputed every exact
  line/byte-range digest, required nonempty dispositions, and rejected
  disposition rows outside the normative range.
- Independent decoders reproduced the declared byte counts and SHA-256 values
  for the two fingerprint and two planned-batch vectors. Independent physical
  key construction reproduced the small key and maximum 142-byte frame,
  204-byte key, and frame digest.
- The hardened guardrail audit found the allocation-label, committed-source,
  stable lifecycle locator, first-reservation recovery, archive reconciliation,
  cursor, mixed-history, comparison, immutable approval, and current-role terms.
  Both no-index whitespace checks emitted no errors with the expected
  content-difference exit.
- The exact pre-commit scope validator passed: protected tracked paths are
  unchanged, the tracked baseline range is empty, and the complete untracked
  allowlist contains only this wrapper and the v2 successor. Section 18 records
  the exact post-commit validator requiring two actual-tab `A` rows relative to
  baseline and a completely empty tracked/untracked worktree; that gate must be
  rerun after the candidate commit and cannot constitute approval.
