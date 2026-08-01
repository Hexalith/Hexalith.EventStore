# Epic 4 Context: Event Correctness And Recovery

<!-- Generated from planning artifacts. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Make persisted-event and command-processing behavior trustworthy under duplicate, concurrent, expired-key, replay, append, and crash-failure conditions. The epic stabilizes event identity and results, prevents stale or unsafe work from executing, makes replay and serialization deterministic, recovers committed delivery, and establishes evidence-backed ordering and durability decisions without weakening tenant isolation.

## Stories

- Story 4.1: Event Identity And Duplicate Result Fidelity
- Story 4.2: Resume And Idempotency Integrity
- Story 4.3: Deterministic Replay Dispatch And Serialization
- Story 4.4: Committed Event Publication Recovery
- Story 4.5: Append Durability Race Evidence
- Story 4.6: Global Position Sharding Spec Renegotiation
- Story 4.7: Tenants Query Provenance Follow-Up
- Story 4.8: Durable Tenant-Scoped Idempotency Admission And Expired-Key Precedence

## Requirements & Constraints

- Persisted events require non-zero actor-allocated global positions while aggregate sequence numbers remain gapless and local to the aggregate. Published CloudEvent IDs use the persisted event `MessageId`, and duplicate command replies preserve the complete original result.
- Resume decisions must match the exact command identity rather than correlation alone. Tenant authorization precedes status or idempotency reads; command status and archive identity are tenant plus message ID; transient failures remain retryable while terminal domain outcomes remain deduplicated.
- Durable admission must serialize opaque idempotency keys within a tenant, compare only server-trusted canonical intent, and prevent duplicate side effects across retries, recovery, expiry, compaction, restart, and concurrent hosts. Live conflicts and every expired-key reuse must stop before downstream execution. Consumed, unreadable, corrupt, unknown, or unsafe legacy state must never become fresh work.
- Replay apply-method resolution must accept exact full names or namespace-boundary-safe matches, detect ambiguity, and retain compatible unambiguous short-name dispatch. Command, rehydrate, projection, and pub/sub payloads share one serializer-options definition.
- Stored-but-unpublished events must be detected and published, drained, or made recoverable without requiring command resubmission. Retry preserves event identity and exposes safe recoverable or terminal diagnostics.
- Append fencing changes require real two-writer DAPR/Redis race evidence and the observed conflict exception/retry surface first. Live-sidecar tests belong outside the deterministic release gate but remain required integration evidence.
- Global-position sharding is spec-first. The frozen ordering contract must be re-approved before implementation, and the chosen tenant or domain boundary must state ordering guarantees, gaps, failure behavior, and migration impact.
- High-risk completion evidence must inspect persisted state, CloudEvent bodies, and downstream side effects through production paths; HTTP status and mock-call assertions are insufficient.

## Technical Decisions

- The gateway remains the authorization, tenant, admission, status, and observability policy boundary. After durable admission grants a current non-zero fence, `AggregateActor` alone coordinates domain invocation, write-once event persistence, recovery state, snapshots, and publication scheduling; domain code never writes EventStore state directly.
- Event delivery is at-least-once and unordered. Consumers deduplicate by `MessageId`; `SequenceNumber` orders only within its aggregate semantics, and global position must not be treated as strict commit order.
- Tenant-scoped admission identity is derived with domain-separated HMAC-SHA-256 and a collision-verification tag. Raw keys and protected intent are excluded from identifiers, persisted diagnostics, logs, traces, metrics, and evidence. Inclusive expiry atomically compacts live intent and replay data to minimized consumed-key evidence; expired requests return the same non-retryable outcome before intent comparison.
- Admission owns monotonic fence issuance, safe resume, terminal replay, rotation-directory authority, and fail-closed legacy migration. Only the current fence may cross any aggregate, domain-service, provider, repository, projection, audit, or scheduling side-effect boundary.
- Reflection-based dispatch remains load-bearing; AOT and trimming are not targets. No replay or transport path may introduce private JSON options for the shared payload family.
- Message, correlation, causation, and aggregate envelope identifiers use ULID-safe handling; GUID parsing is not valid for these identifiers.

## UX & Interaction Patterns

Operational command views distinguish received, processing, events stored, events published, completed, rejected, publish failed, and timed out states. A committed event with missing publication evidence routes to recovery and must not invite resubmission as though persistence failed. Status and recovery evidence remain support-safe and text-labeled. Tenants freshness renders `Current` or other lifecycle states only for projection-backed provenance; handler-computed, missing, or invalid provenance renders `Unknown`.

## Cross-Story Dependencies

- Stable persisted `MessageId` behavior enables duplicate-safe publication recovery; publication recovery remains separate from durable admission and must not be replaced by its resume flow.
- Append fencing cannot start until the live-sidecar conflict spike is reviewed. Global-position allocation cannot change until the frozen ordering spec is re-approved.
- Tenants provenance cleanup depends on the platform provenance contract and Tenants maintainer-approved source work; absent that authority, EventStore closes its boundary with the `Unknown` fallback rather than claiming the producer fixed.
- Durable-admission platform evidence uses the approved shared PostgreSQL-backed multi-host profile. Folders retains ownership of its adapter, canonical evidence packet, and final OQ8 closure.
