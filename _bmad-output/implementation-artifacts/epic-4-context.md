# Epic 4 Context: Event Correctness And Recovery

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Make persisted event metadata and command processing trustworthy under duplicate, concurrent, expired-key, replay, append-race, and crash conditions. Operators and consumers must be able to rely on stable event identity, faithful duplicate results, tenant-scoped idempotency admission, deterministic replay, recoverable publication, and explicitly specified ordering behavior without silent data loss or duplicate side effects.

## Stories

- Story 4.1: Event Identity And Duplicate Result Fidelity
- Story 4.2: Resume And Idempotency Integrity
- Story 4.3: Deterministic Replay Dispatch And Serialization
- Story 4.4: Committed Event Publication Recovery
- Story 4.5: Append Durability Race Evidence
- Story 4.6: Global Position Sharding Spec Renegotiation
- Story 4.7: Tenants Query Provenance Follow-Up
- Story 4.8: Durable Admission Evidence Ledger
- Story 4.9: Trusted Admission Contract And Protected Identity
- Story 4.10: Digest Directory Rotation And Key Retirement
- Story 4.11: Admission State Machine And Current-Fence Enforcement
- Story 4.12: Expiry Compaction And Tombstone Retention
- Story 4.13: Legacy Admission Migration And Fail-Closed Reconciliation
- Story 4.14: OQ8 Multi-Host Production Evidence
- Story 4.15: OQ8 Platform Closure And Handoff

## Requirements & Constraints

- Persisted events receive non-zero actor-allocated global positions while aggregate sequence numbers remain gapless. CloudEvent IDs use the persisted event `MessageId`, and duplicate command replies reproduce every original result field without degradation.
- Resume and idempotency decisions match the exact message, causation, and command type rather than correlation alone. Command status and archive identity use tenant plus message ID, with correlation retained only as an index. Tenant authorization precedes every status or idempotency read; transient failures remain retryable and terminal domain outcomes remain deduplicated.
- Durable admission accepts only trusted, versioned canonical intent and a fixed retention class. Public callers provide only an opaque idempotency key and cannot choose descriptor, digest, actor, fence, state, expiry, policy, or tier authority. Raw keys and canonical intent never enter actor or event state, envelopes, status/archive records, logs, traces, metrics, errors, or evidence artifacts.
- Admission prevents duplicate side effects through reservation, fencing, execution, recovery, expiry, compaction, restart, and concurrent hosts. Conflicting live intent is rejected. Any expired-key reuse returns the same non-retryable `idempotency_key_expired` outcome before downstream work. Consumed, unavailable, corrupt, ambiguous, or uninventoried legacy state never becomes a fresh miss.
- Replay dispatch requires exact or namespace-boundary-safe event-type matching, rejects ambiguity clearly, and preserves unambiguous legacy short-name behavior. Command, rehydrate, projection, and pub/sub paths share one serializer-options definition.
- Committed but unpublished events must be detected and published, drained, or made explicitly recoverable without requiring resubmission under the same correlation ID. Stable `MessageId` values make retry publication duplicate-safe.
- Append fencing cannot change until a real DAPR/Redis live-sidecar two-writer race and actual conflict-exception surface are recorded and reviewed. Global-position sharding likewise cannot change until the frozen ordering specification is revised and approved, including the gappy, not strictly commit-ordered semantics.
- High-risk validation inspects persisted production-path state, read models, markers, checkpoints, before/after snapshots, and CloudEvent bodies, not only status codes or mocks. Durable-admission evidence must prove restart/failover survival, multi-host serialization, inclusive expiry, atomic compaction, rotation and migration safety, leakage absence, and zero downstream work for every non-execute result.

## Technical Decisions

- The gateway remains the command/query policy boundary. Admission precedes durable mutation: a dedicated admission actor owns tenant/key serialization, reservation, and monotonic fence issuance, while `AggregateActor` remains the durable event-mutation coordinator and accepts only an internal current-fence context.
- Exactly the current non-zero fence may cross aggregate, domain-service, provider, repository, projection, audit, or scheduling side-effect boundaries or finalize a terminal result. A fence is reused only for safe resume.
- Admission identity is partitioned by managed tenant, digest-key version, and a domain-separated HMAC-SHA-256 digest of the opaque key. A separate verification tag detects collisions; comparisons are constant-time and sensitive buffers are zeroed.
- Digest rotation uses a tenant-scoped directory and a persisted prepare/copy/source-redirect/directory-flip protocol that maintains one canonical authority. Unknown, colliding, or retired versions fail closed; incompatible hosts fail readiness.
- Expiry atomically replaces replay payload and live intent with a fence-free minimal tombstone. Mutation results retain replay for exactly 86,400 seconds; commit results use `DateTimeOffset.AddYears(7)`. Tombstones persist for tenant lifetime plus the governed 400-day post-deletion period, subject to legal holds.
- Legacy migration is allowed only from a closed, versioned inventory. Targets remain non-executable until acknowledged, redirects are durable, and ambiguous evidence permits read-only diagnosis only.
- Event delivery is at-least-once and unordered; consumers deduplicate by `MessageId` and use sequence ordering only where domain semantics define it. The approved multi-host admission proof uses at least two independent EventStore sidecars sharing the `oq8-postgresql-v1` PostgreSQL actor-state profile and production resiliency policy.
- Reflection-based dispatch remains load-bearing, so AOT and trimming are out of scope. Message, correlation, causation, and aggregate identifiers remain ULID-safe and must not be parsed as GUIDs.

## UX & Interaction Patterns

Command lifecycle surfaces distinguish Received, Processing, EventsStored, EventsPublished, Completed, Rejected, PublishFailed, and TimedOut with text as well as status styling. A committed-but-unpublished command routes to recovery instead of encouraging resubmission as though persistence failed. Projection lifecycle states are authoritative only for projection-backed provenance; handler-computed, missing, or invalid provenance renders `Unknown`.

## Cross-Story Dependencies

- Stories 4.9-4.15 are a backward-only dependency chain. Story 4.8 is historical evidence, not active implementation, and only Story 4.15 can close the EventStore OQ8 platform gate. The downstream consumer retains final cross-repository OQ8 authority.
- Story 4.1 stable identity supports Story 4.4 duplicate-safe recovery, but publication recovery remains separate from admission resume.
- Story 4.5 evidence gates append-fencing work. Story 4.6 approval gates sharding, which must preserve event identity and CloudEvent ID behavior.
- Story 4.7 requires Tenants maintainer approval, an exact Tenants SHA, and production-path evidence. It does not block EventStore platform provenance enforcement, which fails safe to `Unknown` until producer evidence exists.
