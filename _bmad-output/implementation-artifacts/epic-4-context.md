# Epic 4 Context: Event Correctness And Recovery

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Make persisted event metadata and command processing trustworthy under duplicate, concurrent, expired-key, replay, append-race, and crash conditions, so operators and consumers can rely on event identity, idempotent admission, command status, projection dispatch, and recovery instead of hoping the happy path held. The epic stabilizes event identity and duplicate result fidelity, keeps stale pipeline state from hijacking or blocking commands, makes replay dispatch and serialization deterministic, recovers events committed but never published, and gathers real evidence before any append-fencing or global-position-sharding design is chosen. Its largest thread is a durable, tenant-scoped idempotency admission contract delivered as an ordered chain of focused stories that ends in one reviewed platform-closure packet.

## Stories

- Story 4.1: Event Identity And Duplicate Result Fidelity
- Story 4.2: Resume And Idempotency Integrity
- Story 4.3: Deterministic Replay Dispatch And Serialization
- Story 4.4: Committed Event Publication Recovery
- Story 4.5: Append Durability Race Evidence
- Story 4.6: Global Position Sharding Spec Renegotiation
- Story 4.7: Tenants Query Provenance Follow-Up
- Story 4.8: Durable Admission Evidence Ledger (non-executable history; superseded by 4.9-4.15)
- Story 4.9: Trusted Admission Contract And Protected Identity
- Story 4.10: Digest Directory Rotation And Key Retirement
- Story 4.11: Admission State Machine And Current-Fence Enforcement
- Story 4.12: Expiry Compaction And Tombstone Retention
- Story 4.13: Legacy Admission Migration And Fail-Closed Reconciliation
- Story 4.14: OQ8 Multi-Host Production Evidence
- Story 4.15: OQ8 Platform Closure And Handoff

## Requirements & Constraints

- Persisted events receive non-zero, actor-allocated global positions while aggregate sequence numbers stay gapless and aggregate-local. Published CloudEvent IDs use the persisted event `MessageId`. Duplicate command replies reproduce the original result fields — event count, payload, backpressure, accepted/error state, correlation — with no degraded duplicate response.
- Resume and idempotency decisions must match the exact command being processed (message, causation, and command type), not correlation alone. Tenant authorization is evaluated before any idempotency or status data is read. Command status and archive identity are keyed by tenant plus message ID with correlation as an index only. Transient and infrastructure failures stay retryable; terminal domain outcomes stay deduplicated.
- Durable admission accepts only a server-trusted, versioned canonical-intent descriptor and a fixed retention class. Public input supplies nothing but an opaque idempotency key and can never select descriptor, digest, actor, fence, state, expiry, policy, or tier authority. Raw keys and canonical intent must never reach persisted state, envelopes, status/archive records, logs, traces, metrics, errors, or evidence artifacts.
- Admission must prevent duplicate side effects across reservation, fencing, execution, retry, recovery, expiry, compaction, restart, and concurrent hosts. Live conflicting intent is rejected, and every expired-key reuse returns an indistinguishable non-retryable expired outcome before aggregate, domain, or external work. Consumed, unavailable, corrupt, unknown, or uninventoried legacy state must fail closed and never become a fresh miss.
- Replay apply-method resolution requires an exact full-name match or a namespace-boundary-safe match, detects ambiguity with a clear diagnostic, and keeps unambiguous short-name compatibility. Command, rehydrate, projection, and pub/sub payload paths share one serializer-options definition so casing or converter drift cannot silently produce empty payloads on one path.
- Events committed but not published must be detected and published, drained, or made explicitly recoverable, with stable identity across retries and without requiring resubmission under the same correlation ID.
- Append fencing may not be implemented until a live-sidecar two-writer race and the observed DAPR conflict-exception surface are recorded and reviewed. That live test belongs outside the deterministic release gate.
- Global-position sharding is spec-first: the frozen ordering contract must be re-approved, including that positions may be gappy and are not strictly commit-ordered, before allocation changes.
- Completion evidence for these behaviors must inspect persisted production-path state — state store, read model, markers, checkpoints, CloudEvent bodies, before/after snapshots. HTTP status codes and mock call counts are smoke signals only. Durable-admission evidence additionally requires multi-host serialization, restart/failover survival, inclusive expiry boundaries, atomic compaction, rotation, migration safety, leakage absence, and zero downstream work on every non-execute disposition.

## Technical Decisions

- The gateway stays the command/query policy boundary; admission precedes durable mutation. The admission actor owns tenant/key serialization, reservation, and monotonic fence issuance, and `AggregateActor` remains the sole durable event-mutation coordinator, accepting only an internal current-fence execution context. Domain code returns results and never writes platform state.
- Exactly one current non-zero fence may cross an aggregate, domain-service, provider, repository, projection, audit, or scheduling side-effect boundary or finalize a terminal result. Fences are reused only for safe resume.
- Admission identity is partitioned by managed tenant, digest-key version, and a domain-separated HMAC-SHA-256 digest of the opaque key, with a separate verification tag for collisions, constant-time comparison, and buffer zeroing. Identity is independent of message ID and aggregate identity.
- Rotation runs through a tenant-scoped admission directory with a recoverable prepare/copy/source-redirect/directory-flip promotion protocol that preserves exactly one canonical authority; unknown, colliding, or retired versions fail closed. Hosts that do not implement directory routing are deployment-incompatible.
- Expiry is inclusive and atomically replaces replay payload and live intent with a deliberately fence-free minimal tombstone. Mutation replay retention and commit-result retention are fixed and distinct; tombstones persist for tenant lifetime plus a governed post-deletion window, subject to legal-hold pause/resume semantics.
- Legacy migration works only from a closed, versioned inventory that binds each record to its source identity, schema, aliases, result, and phase; targets are prepared non-executable, redirects are durable, and ambiguous evidence is diagnosed read-only rather than promoted.
- Event delivery is at-least-once and unordered; deduplication uses `MessageId` and sequence guards are scoped to tenant/domain/aggregate/projection identity, never treated as globally ordered.
- Multi-host admission evidence runs on the approved production-equivalent profile: PostgreSQL actor state store with the production resiliency policy and at least two EventStore hosts with independent sidecars sharing one backend.
- Reflection-based dispatch stays load-bearing, so AOT and trimming are out of scope. Message, correlation, causation, and aggregate identifiers are ULID-safe and must not be parsed as GUIDs.

## UX & Interaction Patterns

Command lifecycle surfaces distinguish received, processing, events-stored, events-published, completed, rejected, publish-failed, and timed-out as separate text-labeled states, so a committed-but-unpublished event routes to recovery rather than inviting resubmission as if persistence failed. Projection freshness renders a lifecycle state only for projection-backed provenance; handler-computed, missing, or invalid provenance renders the fail-safe unknown state instead of a fabricated current/stale claim.

## Cross-Story Dependencies

- Stories 4.9 through 4.15 form a strictly ordered, backward-only chain; each depends on its predecessor, and only 4.15 closes the EventStore OQ8 platform gate. Story 4.8 is a retained evidence ledger with no sprint execution status — its checked tasks are evidence inputs for 4.9 and 4.10 review only and confer no completion on later children.
- Stories 4.9 and 4.10 carry additional independent architect, security, and test-review boundaries; 4.14 requires real multi-host production evidence, and 4.15 requires a crosswalked packet plus separately authorized identity handoff. The downstream consumer repository retains final cross-repository OQ8 closure, so the packet must not claim it.
- Stable `MessageId` behavior from Story 4.1 enables duplicate-safe publication recovery in Story 4.4; publication recovery stays separate from admission and is not replaced by admission resume.
- Story 4.5 evidence gates any append-fencing work; Story 4.6 spec re-approval gates any global-position allocation change, and that change must not disturb existing per-event identity or CloudEvent ID behavior.
- Story 4.7 is an external-authority follow-up requiring named Tenants maintainer approval, an exact consumer SHA, and production-path validation. It is not the EventStore platform provenance prerequisite; absent that authority, EventStore closes its own risk through the unknown-provenance fallback rather than declaring the producer fixed.
