# Epic 6 Context: Bounded Cost And Event Evolution

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Make long-lived event streams practical to operate: bound snapshot and projection work, prevent out-of-order projection updates, and let domain authors evolve event payloads without rewriting stored history. Validate event identity and carry cancellation through public processing seams. The specification stories authorize their paired implementations; runtime value requires Stories 6.2, 6.4, and 6.6.

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

- Folded snapshots must contain state at one exact sequence plus minimal versioned and protection metadata, with no event history. Equal state under the same serializer and schema has equal bytes regardless of stream length; persisted size is bounded by folded-state size plus the approved 4096-byte envelope overhead. Snapshot-plus-tail recovery must equal full replay, including legacy and protected-data cases.
- A projection proven current at the authoritative stream head performs zero event-envelope reads. An incremental projection reads only a contiguous, page-bounded tail from its durable checkpoint through the admitted head; unsupported handlers use canonical replay. Duplicates, gaps, conflicts, cancellation and rebuild failures must not advance a checkpoint or replace a complete live model with partial state.
- A domain event may declare payload version 1–1024; an absent declaration means version 1, whose stored envelope remains unchanged. Newer versions carry `PayloadVersion` through storage, publication and reads. Registered JSON upcasters apply contiguous one-version steps, including optional renames, before deserialization on every domain-service read path. Stored bytes and event identity never change. Missing or invalid steps and unreadable known events fail closed without partial state, checkpoint movement or handler effects; subscriptions retry rather than acknowledge and drop.
- A shared identity validator rejects malformed events before append staging and at stored-event read boundaries. It checks aggregate identity grammar, nonblank aggregate/event type names, ULID `MessageId` on write, constrained correlation and causation IDs, and positive sequence. Historic GUID `MessageId` values remain readable under the defined legacy exception.
- Caller cancellation tokens reach projection handlers, async processors and aggregate `Handle` methods while existing implementations remain source-compatible. A production-path test must prove persisted version-1/version-2 envelopes, unchanged old payload bytes, publication and rehydrated state. AOT and trimming remain unsupported while reflection conventions are required; release packages must not claim either capability.

## Technical Decisions

- Event and snapshot persistence remains actor-owned through Dapr. The EventStore server stores and forwards versions without upcasting; the domain-service client owns one validated, immutable upcaster registry and shared read pipeline. No application SQL or direct database test evidence is introduced.
- Story 6.6 follows the owner-approved 2026-10-09 pragmatic design and acceptance criteria. Older EventStore replicas may lose or refuse a new version; deploy the updated server everywhere, then update every consumer host, then declare a new event version and its upcaster. Older consumers retry versioned events during the final rollout.
- Metadata V2, kebab-case contract identity, provider attestation, bounded scratch/writer models, offline history rewrite, and the earlier Story 6.5 runtime design are outside Story 6.6's current scope. Its evidence uses focused tests, one persisted write/read test and existing CI.

## UX & Interaction Patterns

Snapshot and projection views expose support-safe size, sequence, readability, head, checkpoint, lag and lifecycle evidence without raw payloads, secrets, provider detail or stack traces. Only authoritative persisted evidence can establish `Current` or completion; absent provenance is `Unknown`. Admin stream/event views continue to show stored data and may show the stored payload version where their existing DTOs support it.

## Cross-Story Dependencies

- Story 6.1's approved folded-snapshot specification authorizes Story 6.2, which remains backlog. Story 6.3's approved projection-cost specification is absent, so Story 6.4 cannot start; Stories 1.18 and 1.19 remain its correctness foundation.
- Stories 6.5a–6.5d fed the completed 6.5 design gate. For Story 6.6, the October 9 story design and acceptance criteria supersede that earlier runtime design; 6.6 has no blocking dependency on those earlier implementation obligations or optional Epic 8 protection work.
- Story 6.7 owns the AOT/trimming documentation and release guard. Snapshot, projection and event-evolution changes do not authorize work in one another's implementation lanes.
