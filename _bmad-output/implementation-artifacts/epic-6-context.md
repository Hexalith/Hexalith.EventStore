# Epic 6 Context: Bounded Cost And Event Evolution

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Keep long-lived event streams operable as they grow. Folded snapshots and projection delivery need bounded cost without weakening replay or sequence safety. Event evolution needs stable versioned identity, deterministic handling of retained history, and cancellation-aware processing. Specifications authorize their paired runtime work; the epic delivers runtime value only when all three implementations are complete.

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

- A snapshot contains folded aggregate state at one exact sequence and only the envelope and protection metadata needed to validate and read it. It excludes event history, prior snapshots, command results, and mutable runtime graphs. Equivalent folded state must have stable serialized bytes regardless of source stream length, and the recorded maximum envelope overhead is 4,096 bytes.
- Snapshot-plus-tail recovery must equal canonical replay of the same readable prefix. Legacy, corrupt, unreadable protected, cancelled, and infrastructure-failure paths need explicit safe outcomes; partial state is never authoritative.
- An already-current projection performs zero event reads or handler calls. A lagging projection processes only the contiguous, page-bounded tail through its admitted head. Cost is measured against tail length and page size, while duplicate, gap, stale, conflict, and multi-replica cases preserve durable checkpoint and last-complete-model guarantees. Aggregate sequence is local to an aggregate; deduplication uses persisted `MessageId`.
- Version-aware events use a canonical kebab-case contract type and positive payload schema version across persisted and wire representations. CLR and assembly names are runtime mappings, never the public event identity. Addressed-stream identity and event metadata must agree before append and before trusted consumption; malformed, unknown, ambiguous, or non-allow-listed evidence fails closed.
- Retained history must remain immutable. Legacy and new events need one deterministic, bounded evolution/read design across replay, projection, subscription, reconstruction, and inspection. A failed, incomplete, unreadable, or cancelled read must not publish partial state, advance a checkpoint, or silently drop an event.
- Public processing, query, and projection seams propagate cancellation through the approved durable boundary. Pre-commit cancellation causes no mutation; post-commit cancellation preserves committed truth. At-least-once, unordered delivery retains stable message identity and durable poison handling.
- Preserve source, binary, wire, package, and generic-gateway compatibility or use an approved migration/breaking-change path. AOT and trimming are outside the current reflection-based posture. High-risk proof inspects persisted state, event bytes, checkpoints, and production-path behavior, not only responses or mock calls.

## Technical Decisions

- Cost and event-evolution work is specification-first. Each runtime slice requires a versioned, content-bound approval that explicitly authorizes it; event-evolution approval must name the human approver. An approved specification alone is not runtime delivery, and focused event-evolution child specifications do not independently authorize implementation.
- `AggregateActor` owns durable event and snapshot mutation after admission. The stable event stream is replay authority; snapshots cannot claim uncommitted events. Projection handlers and read-model/checkpoint operations use platform seams, with durable completion and readback before checkpoint advancement or a `Current` claim.
- The event-evolution design must reconcile exact event metadata and registry rules; bounded writer admission; actor readback, no-op, and command outcomes; V1/V2 and retained-history migration; authenticated complete-prefix reads; upcast chain and payload bounds; publication, subscription, route-effect and poison receipts; mixed-fleet rollout; typed failures; cancellation; and verification vectors. It must close these decisions before runtime implementation rather than leave a consumer to choose locally.
- New and legacy event resolution uses an allow-listed registry and one shared evolution pipeline. Registry conflicts, invalid chains, and unsupported migration combinations fail deterministically. Upcasting changes only the in-memory effective view; original payload bytes and message, sequence, and correlation evidence remain intact.
- The optional production payload-protection engine is outside this epic. Existing no-op, legacy, and protected-readability boundaries still apply.

## UX & Interaction Patterns

- Storage and projection views show support-safe sequence, size/bound, protection/readability, head/checkpoint/lag, delivery mode, lifecycle, fallback, and failure evidence. Initiation or transport acknowledgement is not success; persisted readback establishes completion. Projection lifecycle uses `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, `LocalOnly`, or `Unknown` according to authoritative provenance.
- Type Catalog, streams, and replay may show stable contract type, stored/current version, legacy or upcast outcome, bounded hop count, and typed failure reason. Hide raw or protected payloads, cross-tenant data, CLR assembly identity, secrets, provider internals, and stack traces.

## Cross-Story Dependencies

- The snapshot, projection, and event-evolution implementation slices each require their own approved specification. Projection optimization preserves the production-path correctness established by earlier projection stories; snapshot work does not depend on the optional protection engine.
- Planning records disagree on whether the folded-snapshot specification is absent or already approved with a bound digest and 4,096-byte overhead. Reconcile that gate against the actual artifact and approval before treating Story 6.2 as authorized. The projection specification remains missing, and the integrated event-evolution artifact remains an unapproved draft, so neither dependent runtime slice is authorized by planning status alone.
- Stories 6.5a, 6.5b, 6.5c, and 6.5d provide reviewed candidates for one normative Story 6.5 artifact. The writer/identity contract feeds verified reads; both feed publication and rollout; 6.5d designs the hold, resume, capacity, and legacy-admission mechanisms the first three left to integration. Story 6.5 splices and cites the four candidates without inventing mechanisms, closes the review findings, and obtains exact-content human approval before Story 6.6 can start.
