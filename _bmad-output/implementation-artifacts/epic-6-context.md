# Epic 6 Context: Bounded Cost And Event Evolution

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Platform users can operate long-lived streams with bounded snapshot and projection cost, sequence-safe projection updates, event schema versioning and upcasting, validated event metadata, and cancellation-aware processing seams. Specification stories authorize their paired runtime slices and do not deliver runtime capability. Runtime value requires the folded-snapshot, projection-cost, and event-evolution implementations together.

## Stories

- Story 6.1: Folded Snapshot Frozen Spec
- Story 6.2: Folded Snapshot Implementation
- Story 6.3: Projection Delivery Cost And Sequence Guard Spec
- Story 6.4: Projection Cost And Sequence Guard Implementation
- Story 6.5: Event Versioning And Upcasting Spec
- Story 6.5a: Event Contract, Writer, and Migration Evidence Spec
- Story 6.5b: Verified Read, Replay, and Projection Spec
- Story 6.5c: Publication, Subscription, and Rollout Spec
- Story 6.5d: Hold Lifecycle, Resume, and Legacy Admission Spec
- Story 6.6: Event Versioning And Upcasting Implementation

## Requirements & Constraints

- Folded snapshots persist aggregate state at one exact sequence plus minimal versioned envelope and protection metadata. Equivalent state has identical folded bytes regardless of event count. Full snapshot size stays within folded-state size plus a 4,096-byte envelope overhead. Snapshot-plus-tail recovery equals canonical full replay. Legacy, corrupt, protected-unreadable, cancelled, and infrastructure-failure paths stay typed and safe.
- One requirements record treats that snapshot specification as approved and authorizing implementation, while leaving lifecycle drift open and withholding runtime delivery. The epic reconciliation still treats the specification as absent and implementation as unauthorized. Reconcile the artifact before snapshot implementation. Approval authorizes the next slice only.
- An already-current projection performs zero event reads. A lagging incremental projection reads only a contiguous, page-bounded tail from the checkpoint through the admitted head. Cost follows that tail and page size. The projection specification must bind a numeric budget, workload model, pass condition, and evidence identity. That specification is absent, so projection-cost implementation stays unauthorized.
- Sequence guards are scoped to tenant, domain, aggregate, and projection. Duplicate identity is the persisted message id. Aggregate sequence is gapless per aggregate and is not global order. Duplicates, gaps, stale attempts, conflicts, and replica races leave checkpoints and the last complete live model intact. Paged rebuild output equals canonical replay.
- Freshness and version evidence is authoritative only for projection-backed provenance. Other provenance stays non-authoritative. Lifecycle remains stale, rebuilding, degraded, unavailable, local-only, or unknown until persisted production-path completion proves current.
- Versioned events carry a canonical kebab-case contract type and a positive payload schema version across command result, storage, read, replay, projection, publication, and operator metadata. CLR names stay allow-listed runtime mappings. Addressed-stream metadata must match tenant, domain, aggregate identity and type, contract type, message identity, and positive contiguous sequence before persistence or trusted use. Absent or non-ULID-safe identity fails closed.
- Retained history stays immutable. Replay, projection, subscription, reconstruction, snapshot recovery, and inspection share one bounded upcast pipeline. Original payload bytes and message, sequence, and correlation evidence stay unchanged. Failed, partial, unreadable, or cancelled work publishes no partial state and advances no checkpoint.
- Published processing, query, and projection seams propagate the caller cancellation token until the durable boundary. Pre-commit cancellation leaves no mutation. Post-commit cancellation preserves committed truth. Cancellation stays distinct from domain rejection and infrastructure failure.
- Public source, binary, wire, and package compatibility stays inventory-backed. Incompatible changes need an approved breaking-change proposal and a major release. High-risk proof inspects persisted production-path state. AOT and trimming stay outside the reflection-based posture. Current no-op and legacy payload protection remain valid.

## Technical Decisions

- Snapshot folding, projection cost and sequence guards, and upcaster ordering require an approved versioned specification and compatibility vectors before runtime work. The owning actor is the sole coordinator of admitted event and snapshot mutation. Persisted events remain replay authority.
- Read-model and checkpoint writes require durable completion and readback before checkpoint advancement or a current claim. Projection dispatch records one outcome per route after durable work. Cost optimizations preserve existing duplicate, gap, and rebuild correctness.
- Event evolution uses one allow-listed registry and pipeline. Invalid registries, branching or cyclic chains, and unsupported version combinations fail readiness or fail closed before incompatible writes. Upcasting changes the in-memory view only.
- Story 6.6 is in progress under the owner’s Dapr-only amendment. Supported persistence, actor, messaging, and invocation stay on Dapr. Aggregate, event, snapshot, and existing drain-registration mutation stay on the actor state manager. Other coordinator state may use Dapr state or actor APIs only after transaction and ETag behavior is qualified at the Dapr API boundary, with no independent physical-byte or historical-generation receipt. Application code keeps PostgreSQL connections, schemas, credentials, and private actor-state keys out of this slice. Withdrawn provider SQL and provider-attestation claims stay withdrawn. The project-wide Dapr rule adds no implementation authority and changes no readiness verdict.
- Deployment and operations own Dapr component configuration and two-host crash qualification.

## UX & Interaction Patterns

- Storage and snapshot views show support-safe sequence, size or bound status, readability, and typed failures. Completion follows persisted readback. Unknown storage cost is labelled. Open snapshot work stays unavailable. Folded state, raw events, secrets, and stack traces stay hidden.
- Projection views show head, checkpoint, lag, delivery mode, lifecycle, fallback, guard rejection, and cost. Missing authoritative provenance renders unknown. Acknowledgement or notification delivery does not establish projection-confirmed success. Transport failure keeps unavailable or stale behavior and tenant-scoped delivery.
- Type Catalog, stream, and replay views may show canonical contract type, stored and current version, legacy or upcast state, bounded hop count, and typed failure reasons. The live type catalog stays under Streams & Events, with events, commands, and aggregates tabs. Assembly-qualified CLR names, payloads, secrets, and provider internals stay out of the UI.
- The Dapr infrastructure-boundary change adds no screen or route. Projections, topology, services, and health keep their existing evidence-based states.

## Cross-Story Dependencies

- Stories 6.1, 6.3, and 6.5 authorize Stories 6.2, 6.4, and 6.6 respectively. Epic runtime value waits until 6.2, 6.4, and 6.6 all complete.
- Snapshot implementation waits on one reconciled, content-bound specification that explicitly authorizes it. Projection-cost implementation also preserves Stories 1.18 and 1.19 and waits on its missing approved specification.
- Stories 6.5a–6.5d supplied reviewed section candidates. Story 6.5 integrated them, and Jérôme Piquot approved that design. Child specifications grant no separate implementation authority. Story 6.6 follows that design only where it remains compatible with the Dapr-only amendment. The shared production evolution pipeline, consumer qualification, and remaining runtime verification stay open. V2 admission stays fenced.
- Event evolution leaves folded-snapshot redesign, projection-cost redesign, and the optional protection engine outside its slice. Epic 8 is not a prerequisite. Operator presentation belongs to the admin experience and must leave unimplemented snapshot or projection behavior unavailable.
