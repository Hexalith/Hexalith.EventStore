# Epic 6 Context: Bounded Cost And Event Evolution

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Keep long-lived streams operable through bounded snapshot/projection cost, sequence-safe delivery, stable versioned event identity, deterministic historical reads, and cancellation-aware processing. Specifications enable their paired implementations; all three runtime slices must complete before the epic claims delivered capability.

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

- Snapshots contain folded state at one exact sequence plus minimal envelope/protection metadata, excluding history and nested prior snapshots. Equivalent state has stable bytes independent of stream length; maximum envelope overhead is 4,096 bytes. Snapshot-plus-tail recovery equals canonical replay, with safe legacy, corrupt, protected-unreadable, cancellation, and infrastructure outcomes.
- Current projections perform zero event reads or handler calls. Lagging projections consume a contiguous bounded tail through the admitted head; cost depends on tail/page size. Duplicate, gap, stale, conflict, and replica races preserve checkpoints and the last complete model. Sequence is aggregate-local; deduplication uses persisted `MessageId`.
- Version-aware persisted and wire events carry canonical kebab-case contract type and positive payload version. CLR names remain runtime mappings. Addressed-stream identity and metadata must agree before append or trusted consumption; malformed, unknown, ambiguous, and non-allow-listed evidence fails closed.
- Retained history stays immutable. Replay, projection, subscription, reconstruction, and inspection share bounded deterministic evolution. Failed, partial, unreadable, or cancelled work cannot publish partial state, advance checkpoints, or silently lose events.
- Published processing/query/projection seams propagate cancellation to the approved durable boundary. Pre-commit cancellation causes no mutation; post-commit cancellation preserves committed truth. At-least-once unordered delivery preserves message identity and durable poison handling.
- Preserve source/binary/wire/package and gateway compatibility or follow approved migration and breaking-change rules. AOT/trimming remains outside the reflection-based posture. High-risk verification inspects persisted state, bytes, checkpoints, and production paths.

## Technical Decisions

- Each runtime slice follows its reviewed specification and recorded human approval. The owner's 2026-10-05 Dapr-only direction amends Story 6.6; its [amendment](story-6-6-dapr-only-amendment.md) governs conflicts with the earlier approved design. Ordinary commits, unrelated work, and submodule advancement do not invalidate approval.
- `AggregateActor` owns admitted event/snapshot mutation; persisted events remain replay authority. Platform projection/store seams require durable completion and readback before checkpoint advancement or `Current` claims.
- One allow-listed registry/evolution pipeline resolves legacy and current events. Invalid registries/chains and unsupported combinations fail deterministically. Upcasting changes the in-memory view only, preserving original bytes and message/sequence/correlation evidence. Writer admission, authenticated complete-prefix reads, hold/resume exits, publication receipts, rollout, and cancellation follow the approved integrated design.
- Story 6.6 coordinator metadata uses Dapr actor/state APIs. Event/snapshot and drain-registration mutation remains actor-owned. Non-actor controls require qualified Dapr ETag/transaction capability or a single actor owner, bounded values, explicit fences and recovery across independent boundaries. Application code has no PostgreSQL connection, schema or credentials. Deployment/Operations own the Dapr component and two-host crash qualification.
- Existing no-op/legacy protection and protected-readability rules apply; the optional production protection engine is outside this epic.

## UX & Interaction Patterns

- Storage/projection views expose bounded sequence, size, readability, head/checkpoint/lag, delivery-mode and failure evidence. Readback establishes completion. Authoritative provenance governs `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, `LocalOnly`, or `Unknown`.
- Type Catalog/stream/replay views expose stable contract type, stored/current version, legacy/upcast state, bounded hop count, and typed reasons. Hide payloads, cross-tenant data, CLR assembly details, secrets, provider internals, and stack traces.

## Cross-Story Dependencies

- Each implementation requires its paired approved specification. Projection optimization preserves the correctness established by Stories 1.18/1.19.
- Folded-snapshot planning conflicts: PRD recognizes approval, epics claims the artifact absent, and tracker records review. Reconcile the actual artifact before Story 6.2 progress. The missing projection specification keeps Story 6.4 gated.
- Stories 6.5a–6.5d and 6.5 are complete; Jérôme Piquot approved their integrated event-evolution design. Story 6.6 starts on owner request. Runtime/provider verification and implementation/evidence follow-ups remain open; child specifications confer no separate implementation authority.
- The superseded metadata-adapter documentation remains historical evidence and does not establish runtime activation, provider proof, or production-profile approval. Event evolution leaves snapshot, projection-cost, and optional protection-engine redesign outside its scope.
