# Epic 6 Context: Bounded Cost And Event Evolution

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Keep long-lived streams operable as they grow: snapshots stay bounded, projections skip avoidable full replay while remaining sequence-safe, and persisted events can evolve through deterministic upcasting, validated identity, and cancellation-aware public seams. Spec-first gates authorize each runtime slice; the epic claims delivered runtime value only after folded snapshots, projection cost/sequence guards, and event versioning/upcasting all ship.

## Stories

- Story 6.1: Folded Snapshot Frozen Spec
- Story 6.2: Folded Snapshot Implementation
- Story 6.3: Projection Delivery Cost And Sequence Guard Spec
- Story 6.4: Projection Cost And Sequence Guard Implementation
- Story 6.5: Event Versioning And Upcasting Spec
- Story 6.6: Event Versioning And Upcasting Implementation

## Requirements & Constraints

- Snapshots persist only folded aggregate state at one exact sequence plus a minimal versioned envelope. They must not embed event history, prior snapshots, command/result payloads, or mutable runtime graphs. Identical state must serialize to identical folded-state bytes regardless of source event count, and full snapshot size must stay within a numeric envelope-overhead bound frozen in the approved specification.
- Rehydration from a folded snapshot plus later events must equal canonical full replay for the same readable prefix. Legacy, corrupt, unreadable-protected, cancelled, and infrastructure-failure paths have typed fail-closed or safe-bypass outcomes; protected evidence is retained and partial state is never treated as authoritative.
- When a projection-scoped checkpoint equals the admitted stream head, delivery is a metadata-only short-circuit: no event reads, handler calls, state writes, or checkpoint advancement. When it lags, only the ordered contiguous tail from checkpoint+1 is processed, with cost measured against delta and page size—not total stream length.
- Duplicate, stale, gap, conflict, and multi-replica races fail closed or retry without treating aggregate sequence as global order. Dedup identity is the persisted message id; guard identity is tenant, domain, aggregate, and projection type. Checkpoint and `Current` advance only after durable handler completion and required readback.
- Cost work must not weaken existing production-path duplicate, gap, page-safety, staging, promotion, lifecycle, or replay-equivalence guarantees. A page is never a complete stream; staged or page-only output must not replace the last complete live model.
- New events carry a canonical kebab-case contract type and a positive payload schema version. CLR/assembly names are allow-listed runtime mappings, not persisted public identity. Read-time upcasting is a shared, contiguous, uniquely ordered, hop-bounded chain that never rewrites stored bytes or message/sequence/correlation evidence.
- Event metadata identity (tenant, domain, aggregate, contract type, sequence) is validated against the addressed stream before append and again before trusted replay or dispatch. Unknown, ambiguous, or non-allow-listed types fail closed without arbitrary type loading or silent skip.
- Published processing, query, projection, replay, and adapter seams propagate cancellation; cancellation stays distinguishable from domain or infrastructure failure. Pre-commit cancel leaves zero mutation; post-commit cancel preserves committed truth.
- Additive public/package and generic-gateway compatibility must be preserved. AOT/trimming is not a target while reflection discovery remains load-bearing, and that posture must stay explicit.
- High-risk proof is persisted production-path evidence (state, checkpoints, snapshot bytes, replay equivalence, live-sidecar behavior), not HTTP status or mock counts.
- Optional production payload-protection engine work is out of scope; current no-op/legacy protection behavior remains valid.

## Technical Decisions

- Cost and evolution changes are spec-first. Each runtime slice starts only after a named approved specification exists, records closed decisions, a numeric/quantitative bound where required, named approval, and explicit authorization. Required artifacts: `_bmad-output/implementation-artifacts/spec-folded-snapshot.md`, `spec-projection-cost-sequence-guard.md`, and `spec-event-versioning-upcasting.md`. Those artifacts are currently absent, so the implementation stories remain unauthorized.
- After admission, `AggregateActor` remains the sole snapshot-mutation and durable event-mutation coordinator. Domain code returns a domain result and never writes EventStore state. The stable event stream is replay authority; a snapshot must not claim uncommitted events or become authority for an uncommitted append.
- Projection handlers are identified by `(Domain, ProjectionType)`. Dispatch is asynchronous and cancellation-aware. Checkpoint advancement requires a completed or already-completed route plus proven durable persistence; cancellation fabricates no result.
- Every handler declares full-replay or incremental semantics. Incremental-capable routes may consume prior durable state plus a contiguous tail; unsupported, ambiguous, or failed incremental routes use the approved full-replay/rebuild path.
- Read-model writes, erasure, and checkpoints stay on platform seams. Query freshness/version is authoritative only for projection-backed provenance; missing freshness is unknown, not current.
- Event discovery is allow-listed and deterministic. Duplicate contract keys, gapped/cyclic/downgrade upcast edges, or nondeterministic assembly order fail startup. No new AOT/trimming commitment.
- One shared evolution pipeline (identity/readability, format validation, chained upcast, allow-listed deserialize, dispatch) is used by replay, projection, subscription, reconstruction, and inspection.

## UX & Interaction Patterns

- Storage & Snapshots may show support-safe sequence, size/bound status, age, protection/readability, and failure classification. Snapshot actions stay disabled unless implemented and current, and accepted or manual creation is not success until persisted readback. Raw folded state, events, secrets, and stack traces stay hidden.
- Projections may show head/checkpoint/lag, delivery mode, lifecycle, fallback, guard-rejection, and cost evidence. Lifecycle renders `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, `LocalOnly`, or `Unknown`; the six concrete states only for projection-backed provenance. `Current` is never claimed because an optimized attempt started or returned locally. Mutations stay disabled except on authoritative `Current` unless a named exception exists.
- Type Catalog, streams, and replay may show stable contract type, stored/current version, legacy/upcast outcome, hop count, and cancellation/failure reason—not assembly-qualified CLR names as public identity, and not raw or protected payloads.
- Admin surfaces use the single Event Store Admin dashboard (FrontComposer and Fluent UI V5). Success is evidence-confirmed, not HTTP 202 or SignalR. Denied views do not confirm hidden-resource existence.

## Cross-Story Dependencies

- Each runtime slice is gated by its spec: 6.1 authorizes 6.2; 6.3 authorizes 6.4; 6.5 authorizes 6.6. The three pairs stay isolated—snapshot work must not pull in projection optimization or upcasting, projection work must not pull in upcasting or cancellation-interface changes, and versioning work must not redesign snapshots or projection cost.
- Stories 6.3 and 6.4 require the production-path projection correctness already owned by Stories 1.18 and 1.19; they may optimize within those invariants only.
- Epic 8 is not a prerequisite. Exposed or administrative evidence surfaces still inherit Epic 5 fail-closed boundaries. Epic 7 may later present snapshot, projection, and type-catalog evidence but does not own these runtime contracts.
