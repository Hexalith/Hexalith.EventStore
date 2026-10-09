# Epic 6 Context: Bounded Cost And Event Evolution

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Support long-lived streams with bounded snapshot/projection cost, sequence-safe delivery, stable event versions, deterministic upcasting, validated identity, and cancellation-aware processing. Specification approval authorizes only its paired implementation; delivered runtime value requires Stories 6.2, 6.4, and 6.6 together.

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
- Story 6.7: AOT And Trimming Posture Reference

## Requirements & Constraints

- Snapshots contain only folded state at one exact sequence and minimal versioned/protection metadata. Equivalent state has identical bytes regardless of history length, within an approved overhead bound. Automatic/manual snapshot-plus-tail recovery equals canonical replay, preserving legacy/protected readability. Byte bounds, migration, and protection decisions must close before implementation; historical approval alone supplies no authority.
- Current projections read zero events; incremental delivery reads a contiguous bounded tail through the admitted head, with cost following lag/page size. Unsupported handlers use canonical replay. Duplicate/gap/race/cancellation/rebuild failures preserve checkpoints and the complete live model. Paged rebuild equals canonical replay.
- New event types may declare a payload version (absent = 1); version-1 events persist unchanged. Event identity metadata (tenant/domain/aggregate grammar, aggregate and event type, ULID MessageId, AD-32 correlation, causation, sequence) is validated on append and read and fails closed.
- Every domain-service read path shares one client-side pipeline of contiguous n → n+1 JSON upcasters (optional rename) before deserialization. Stored bytes, identity and sequence stay immutable; failures commit no partial state, checkpoint, or handler effect.
- Published processing, query, and projection seams receive the caller's cancellation token; legacy implementations stay source-compatible.
- Source/binary/wire/package compatibility requires inventory-backed baselines and mixed-version/rollback evidence. Breaking changes need approved migration and SemVer-major release. AOT/trimming remains unsupported while reflection is load-bearing; release packages must reject declarations that claim either capability.

## Technical Decisions

- `AggregateActor` alone mutates admitted aggregate/event/snapshot state through `IActorStateManager` under a current execution fence. Events, metadata, snapshots, results, and outbox stage together and save once. Ambiguous saves require fresh addressed Dapr actor readback before retry/acknowledgement; unavailable readback proves no absence and permits no repeat effect.
- Story 6.6 follows the AD-13 pragmatic scope (2026-10-09): story ACs govern; evidence is unit tests, one end-to-end persisted-path test, and the existing CI.

## UX & Interaction Patterns

Expose support-safe snapshot size/sequence/readability, projection head/checkpoint/lag/lifecycle, and stored event type and payload version. Hide payloads, CLR assembly details, secrets, provider internals, and traces. Persisted authoritative evidence establishes completion/freshness; acknowledgement or notification does not. Only projection-backed query provenance supplies lifecycle authority. Open capabilities stay unavailable; missing provenance is `Unknown`.

## Cross-Story Dependencies

- Story 6.1 is done for the specification gate after Jérôme Piquot (`jpiquot`) approved normative SHA-256 `a4ca9686628b284fb74da931e8cfb1466e80de45fd3d4e89a3c62358a4498ca5`, the 4096-byte overhead bound, explicit Story 6.2 authorization, and completion-record reconciliation on 2026-10-08. The inventory baseline is `7d76df4981fb070c4d84d817bf6fc800f27d0adb`. Story 6.2 stays backlog with authorization to implement the approved sections; normative byte/design drift voids that authorization. No runtime bounded-cost outcome is delivered. Story 6.3's approved specification is absent; Story 6.4 stays unauthorized and preserves Stories 1.18/1.19 correctness.
- Story 6.6 was rescoped on 2026-10-09 (sprint-change-proposal-2026-10-09); Story 6.5's design is historical context. Upcasting does not authorize snapshot or projection-cost redesign.
- Corrective stories require Story 9.1 authorization within their bounded lane; general readiness remains blocked. Story 6.7 supplies the AOT/trimming posture and package guard. The optional Epic 8 protection engine is not a prerequisite. Event evolution does not authorize snapshot or projection-cost redesign.
