# Epic 4 Context: Operators Can Trust Command and Event Integrity

<!-- Generated from planning artifacts. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Make command processing and persisted event behavior trustworthy under retries, concurrency, replay, expiry, crashes, and partial failure. Operators, domain authors, and reliability engineers must be able to rely on stable event identity, durable idempotency admission, deterministic dispatch, recoverable publication, and ordering semantics that are explicitly specified and proven before production behavior changes.

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

- Persisted events have non-zero actor-allocated positions and gapless per-aggregate sequence numbers. Global allocation may contain reservation gaps and does not promise strict commit order. CloudEvent IDs use persisted event `MessageId`; duplicate command replies preserve the original result fields.
- Resume and idempotency use the exact tuple of `MessageId`, normalized `CausationId`, and `CommandType`; correlation remains tenant-scoped tracing metadata. Tenant authorization must precede state access, terminal results replay faithfully, transient pre-commit outcomes remain retryable, and ambiguous, corrupt, unavailable, consumed, or unsafe legacy state never becomes a fresh miss.
- Durable admission accepts only a trusted, versioned canonical-intent descriptor and fixed retention class. Callers provide only an opaque idempotency key and cannot select descriptor, digest, actor, fence, state, expiry, or policy authority. Raw keys, protected intent, results, and secrets must not leak into state identifiers, envelopes, diagnostics, telemetry, or evidence.
- Admission must prevent duplicate side effects across reservation, execution, recovery, expiry, compaction, restart, rotation, migration, and concurrent hosts. Conflicting live intent fails permanently; every expired-key reuse fails identically before protected work.
- Replay dispatch must resolve event types deterministically, detect ambiguity, preserve supported legacy names, and use one immutable serializer-options path across command, rehydrate, projection, and pub/sub readers.
- Committed-but-unpublished events must remain durably discoverable and recoverable without command resubmission. Republishing reuses the persisted event identity so at-least-once delivery remains deduplicatable.
- Append fencing is evidence-first: observe a real live-sidecar two-writer race and conflict behavior before selecting a provider-portable design. Global-position sharding is spec-first: no implementation, persisted-state, public-contract, migration, or topology change may begin until a content-bound successor to the frozen ordering specification is human-approved.
- High-risk verification must inspect persisted state, event bodies, checkpoints, topology, restart/failover behavior, and zero downstream work for non-execute outcomes. HTTP statuses and mock calls alone are insufficient.

## Technical Decisions

- The gateway remains the command/query policy boundary. After authentication, current authorization, and canonical validation, a dedicated tenant/key admission actor owns serialization, reservation, state transitions, and monotonic fencing. `AggregateActor` remains the sole durable event-mutation coordinator and accepts only a current internal fence.
- Exactly the current non-zero fence may cross an aggregate, domain-service, provider, repository, projection, audit, or scheduling side-effect boundary or finalize a terminal result. Safe recovery resumes the persisted identity and fence; uncertainty never creates new execution authority.
- Current global positions are non-zero, unique scalar values from the DAPR-backed allocator, but their gaps mean they are not a strict global commit sequence. A sharding successor must define shard ownership, uniqueness and monotonicity boundaries, representation, comparison rules, cursor/checkpoint behavior, mixed-history compatibility, rollout, rollback, and unsupported cross-shard comparisons.
- Event delivery is at-least-once and unordered. Consumers deduplicate by `MessageId`; sequence ordering is meaningful only within its documented domain boundary. Projection or notification signals do not by themselves prove user-visible success.
- Admission identity is partitioned by tenant and digest-key version with domain-separated HMAC-SHA-256 key digests and collision verification. Rotation and legacy migration preserve one canonical executable authority; expiry atomically replaces live/replay state with a fence-free minimal tombstone.
- Production-equivalent admission proof uses at least two independent EventStore hosts and DAPR sidecars sharing the `oq8-postgresql-v1` PostgreSQL actor-state profile with production resiliency. Same-process fixtures and direct actor calls are supporting evidence only.
- Message, correlation, causation, and aggregate identifiers remain ULID-safe where EventStore envelope semantics require sortable IDs; they must not be validated as GUIDs.

## UX & Interaction Patterns

Operator-facing command states must distinguish acceptance, recovery in progress, terminal success, and terminal failure using support-safe text rather than treating an accepted response as completion. Committed-but-unpublished work routes to recovery rather than encouraging resubmission. Shard-local and globally comparable positions must be labeled accurately. Projection lifecycle claims are authoritative only for projection-backed provenance; otherwise surfaces render `Unknown` and never infer state from an ETag or acceptance response. Opaque keys, canonical intent, digests, payloads, and protected results are never displayed.

## Cross-Story Dependencies

- Story 4.1 establishes stable identity. Story 4.2 adds exact message-keyed recovery state; both precede Story 4.4 publication recovery. Story 4.5 gates append-fencing decisions and gates Story 4.6 only if the selected sharding design changes append fencing or provider write semantics.
- Story 4.8 is a historical, non-executable ledger. Stories 4.9-4.15 form the ordered OQ8 authority and evidence chain; later work cannot retroactively authorize an earlier unsafe outcome, and platform completion does not grant release, deployment, consumer migration, or downstream repository authority.
- Story 4.7 depends on separately authenticated Tenants-maintainer authority and exact external-repository evidence. Existing EventStore provenance enforcement remains fail-safe while that follow-up is incomplete.
