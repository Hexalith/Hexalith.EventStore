# Epic 4 Context: Operators Can Trust Command and Event Integrity

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Make command and event processing trustworthy under retries, concurrency, replay, expiry, crashes, and partial failure. Stable identity, durable admission, recoverable publication, and explicit ordering guarantees must protect against silent loss and duplicate side effects. Observed failures and completed planning documents remain distinct from implemented, production-proven prevention.

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
- Story 4.16: Append Write-Once Conformance Through Atomic Conflict Protection
- Story 4.17: OQ8 Governing-Design Authority Import

## Requirements & Constraints

- Aggregate sequence remains gapless. Global positions are nonzero, unique allocations that may have reservation gaps and do not promise strict commit order. Persisted event `MessageId` supplies the stable CloudEvent ID; duplicate replies preserve the original result.
- Resume requires ordinal matching of `(MessageId, normalized CausationId, CommandType)` before a persisted stage resumes; correlation is diagnostic metadata. Tenant authorization precedes state access. Transient outcomes remain retryable, and consumed, unavailable, corrupt, ambiguous, or unsafe legacy state never becomes a fresh miss.
- Admission accepts only trusted, versioned canonical intent and a fixed retention class. Callers cannot choose descriptor, digest, actor, fence, expiry, or policy authority. Raw keys, protected intent, results, and secrets remain absent from identifiers, envelopes, telemetry, and evidence.
- Replay dispatch resolves types deterministically, detects ambiguity, and preserves supported legacy names through one platform serialization path. Committed-but-unpublished events remain recoverable without command resubmission; republishing preserves identity.
- At-least-once, unordered delivery requires MessageId deduplication. Sequence ordering is meaningful only within its documented domain boundary and must be proven through production projection paths.
- Silent-loss prevention must cover staged-state loss, stale pipelines, append races, unpublished commits, and duplicate side effects. Reproducing loss or deferring a guard does not deliver prevention, readiness, or MVP completion.

## Technical Decisions

- Durable admission precedes aggregate or external effects. Its tenant/key authority serializes reservation, lifecycle transitions, and monotonically increasing fences. Only the current nonzero internal fence authorizes execution or terminal finalization; uncertainty never creates fresh authority. `AggregateActor` retains sole event-append ownership.
- Digest rotation and legacy migration preserve one executable authority through durable, idempotent transitions. Inclusive expiry atomically replaces live intent and replay results with a metadata-minimized tombstone containing no fence. All expired reuse fails identically before downstream work.
- Internal admission fencing does not prove physical write-once storage. The approved append scope requires a qualified mandatory Dapr-owned actor transaction that compares durable revisions, creates event identities only once, and commits or rejects all staged state atomically. Writer isolation remains a prerequisite. Raw same-identity calls, deletion, TTL, stale writers, and failover cannot bypass protection; governed erasure remains available and invalidates stale writers. Missing supported capability or proof leaves the gate blocked. No application SQL, provider access, alternate writer service, general activation fencing, or cross-component transaction is authorized. Runtime minor-version changes require renewed proof.
- The frozen single-allocator specification remains authoritative. Sharding is specification-only work until architecture owners approve the exact committed successor identity. Approval authorizes downstream planning only. Production-provider/topology evidence and separately reviewed implementation authority remain necessary; source, state, contract, migration, and topology changes cannot be inferred from a workflow invocation. The successor must define shard ownership, comparison, mixed history, cursors, checkpoints, initialization, rollout, rollback, and noncomparability without rewriting historical positions.
- High-risk proof inspects persisted end state through the owning Dapr boundary, including restart, concurrency, denial, and zero downstream work for non-execute outcomes. OQ8 production-equivalent proof requires at least two independent EventStore OS processes and sidecars sharing the `oq8-postgresql-v1` PostgreSQL actor-state profile with production resiliency; profile changes require requalification. Same-process fixtures and direct actor calls are supporting evidence only. Backend reads are diagnostics; mocks and HTTP codes alone are insufficient. Infrastructure blockers remain distinct from product failure.
- EventStore owns backend-neutral Dapr requirements, module declarations, reusable tests, and application assertions. Platform owns infrastructure composition, concrete component/provider configuration, profile qualification, Kubernetes rehearsal, restore, and run-owned endpoints and loaded identities. Builds owns catalog, tool, and runner alignment. Repository AppHosts are bounded test fixtures. Each actor-hosting app has an isolated actor-state component and physical target; shared-reader state uses separately scoped components. Production identity requires the complete canonical profile and authenticated Architecture and Platform ratification; observations or candidate publication supply no deployment authority.
- Proof is tests plus CI. Story verification records commands, counts, commit SHA, and CI links; run output belongs in workflow artifacts. Do not recreate removed committed evidence packets, manifests, or tools that merely revalidated them. Remaining assurance and transition rules still apply.
- OQ8 governing-design availability uses the locally retained immutable design and its owner's MIT notice, checked offline against fixed identity pins and current document/workflow bindings. Proof is the owning spec plus dedicated CI; local verification awaits CI confirmation. This closes only availability, conditional on CI. Runtime conformance, bounded historical platform completion, assurance, readiness, release, deployment, consumer migration and Folders closure retain their separate gates. Deleted packets, manifests, seals and authorization machinery are not prerequisites to restore.

## UX & Interaction Patterns

Distinguish accepted, recovering, terminal, and projection-confirmed outcomes. Recovery should preserve operation identity rather than encourage resubmission. Label shard-local ordering accurately. Only projection-backed provenance supports lifecycle claims; other routes remain `Unknown`. Support-safe views never expose opaque keys, canonical intent, digests, fences, or protected results.

## Cross-Story Dependencies

- Stable identity and exact resume semantics precede publication recovery. Append-race evidence gates sharding only when the selected design changes append fencing or provider write semantics.
- Admission delivery follows its ordered contract, rotation, state-machine, expiry, migration, multi-host proof, and handoff chain. Later completion cannot retroactively authorize an earlier unsafe outcome.
- Append conformance depends on infrastructure/writer inventory and supported runtime capability qualification, separately owned component isolation and reader/data migration, authorized erasure, restore compatibility, profile ratification, and remaining assurance controls. Governing-design import has its own authority gate; bounded platform closure does not waive it.
- Tenants provenance follow-up relies on delivered EventStore routing/consumer seams and separate Tenants-owner authority. EventStore completion grants no external-repository mutation authority.
