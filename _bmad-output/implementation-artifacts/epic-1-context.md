# Epic 1 Context: Domain Author Self-Service Platform

<!-- Generated from planning artifacts. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Enable domain authors to build and run EventStore-backed modules using only domain behavior and contracts while EventStore supplies the reusable hosting, DAPR endpoints, query and projection seams, read-model persistence, cursor protection, telemetry, health, Aspire wiring, and packaging. This reduces per-domain boilerplate, keeps Sample, Tenants, and future modules consistent, and permits consumers to remove local projection/query infrastructure only after the platform replacement is proven and pinned.

## Stories

- Story 1.1: Canonical Domain-Service SDK Host
- Story 1.2: Domain Query Handler Routing
- Story 1.3: Generic Read Models And Query Cursors
- Story 1.4: Projection And Domain Event Consumer Seams
- Story 1.5: Domain Module Hosting Observability
- Story 1.6: Sample And Tenants Domain-Centric Adoption
- Story 1.7: DomainService Packaging And Guardrails
- Story 1.8: Projection/Query SDK Owner Parity Proof
- Story 1.9: Read-Model And Projection Checkpoint Erasure
- Story 1.10: Coordinated Read-Model Batch Writes
- Story 1.11: Complete Projection Freshness Lifecycle
- Story 1.12: Asynchronous Multi-Projection Dispatch
- Story 1.13: Projection-Handler Delivery Idempotency
- Story 1.14: Correct Paged Rebuild And Replay Equivalence
- Story 1.15: Owner-Approved Parity Closure And Runtime Pin

## Requirements & Constraints

- Domain modules contain aggregates, commands, events, projections, query handlers, validators, options, and contracts; reusable request routing, projection/query actors, cursor codecs, state-store wrappers, telemetry, health checks, ServiceDefaults, and Aspire plumbing belong to EventStore platform packages.
- The domain-service SDK supplies the canonical host extension shape and owns `/process`, `/replay-state`, `/query`, `/project`, and `/admin/operational-index-metadata`, while yielding safely to an existing bespoke projection route.
- Discovered domain query handlers and operational metadata drive handler-aware gateway routing. `QueryResponseMetadata` must preserve producer evidence for lifecycle, projection version, paging, degraded state, and warnings; missing or non-authoritative evidence remains unknown. Producer paging evidence is authoritative, but gateway-echoed request paging is not.
- Read models require ETag-aware reads/writes, bounded optimistic-concurrency behavior, deterministic DAPR and in-memory semantics, coordinated detail/index writes, and tenant/domain/aggregate/projection-scoped erasure of read-model plus delivery/rebuild checkpoint state. Erasure does not include event streams, snapshots, broker history, backups, audit evidence, or cryptographic keys.
- Cursors are DataProtection-backed, purpose-isolated, scoped, bounded, opaque, and fail safely for malformed or oversized payloads, tampering, wrong scope/query type, and key rotation.
- Projection dispatch and persistence are asynchronous and cancellation-aware, support multiple named projections per domain, and expose distinguishable outcomes. At-least-once unordered delivery must be idempotent through the production handler path: deduplicate by EventStore `MessageId`, never treat sequence as globally ordered, and advance checkpoints only after required durable writes.
- Paged rebuilds must equal canonical replay for the same event prefix. Page boundaries cannot become semantic stream boundaries; incomplete work stays staged, preserves the last complete live model, and resumes safely without reporting success.
- Sample and Tenants adoption must preserve existing domain behavior while removing duplicated infrastructure. Release builds remain warnings-as-errors clean, package versions stay centralized, and DomainService/ServiceDefaults release output is governed only by `tools/release-packages.json`.
- Readiness-critical verification must inspect persisted detail, index, marker, lifecycle, checkpoint, state-store, topology, or package evidence as applicable; status codes and mock calls alone are insufficient.

## Technical Decisions

- The platform remains CQRS, DDD, and event sourcing over DAPR, with Aspire as the local topology seed. Domain code returns domain results and does not write EventStore state directly.
- Query evidence travels through platform metadata rather than ad hoc payload fields. Domain/projection evidence owns lifecycle, version, paging, degraded state, and warnings; the gateway owns the opaque HTTP ETag, fills served-at only when absent, and derives not-modified from the HTTP outcome. ETags never prove freshness or projection version.
- Query provenance is route-bound. Only `ProjectionBacked` responses may carry authoritative `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, or `LocalOnly`; handler-computed, missing, or invalid provenance resolves to `Unknown`.
- Read-model/cursor implementations use the platform `IReadModelStore`, `ReadModelWritePolicy`, `IQueryCursorCodec`, and `QueryCursorScope` seams. Same-store batches use a transaction or a documented resumable equivalent with truthful partial-failure, idempotency, ordering, concurrency, and flush-completion semantics.
- Projection handlers are identified by `(Domain, ProjectionType)`. Rebuild handlers declare full-replay or incremental semantics, use non-live staging, and promote only after all required projections durably complete. Compatibility requires an additive adapter or an explicitly approved breaking-version migration plan.
- Runtime changes keep AppHost resources, DAPR component/configuration YAML, app IDs, scopes, ACLs, topics, sidecar options, and topology tests aligned. Release validation defaults to package-reference mode; source references are explicit opt-in and never used for publication.

## UX & Interaction Patterns

UI consumers use FrontComposer and Blazor Fluent UI V5 and obtain lifecycle evidence through client metadata. Command acceptance, HTTP `202`, and SignalR are evidence-pending signals, not success. Only projection-backed `Current` may enable otherwise-authorized mutations by default; all other lifecycle states disable mutation unless an explicit consumer exception exists, and `LocalOnly` never counts as projection-confirmed success. Render lifecycle with text as well as styling, fall back to `Unknown`, and never expose raw metadata, payloads, cursors, ETags, tokens, stack traces, or secrets.

## Cross-Story Dependencies

- Stories 1.1-1.5 establish the SDK, metadata, persistence, projection, event-consumer, observability, and Aspire seams consumed by the Sample/Tenants adoption and packaging guardrails in Stories 1.6-1.7.
- Story 1.2 establishes metadata propagation; Story 1.3 adds authoritative paging and cursor evidence. Story 2.8 must complete route-aware provenance enforcement before Story 1.11 evidence or any consumer may claim authoritative current/stale state.
- Story 1.8's completed investigation remains a `still blocked` parity result. Stories 1.9-1.14 close its identified gaps; Story 1.15 can declare availability only after those stories are complete and reviewed, all parity items have production-path evidence, and EventStore owner approval names one exact runtime commit.
- Parties Story 8.6 remains blocked until its EventStore submodule matches the Story 1.15 approved SHA. Later projection-cost work in Stories 6.3-6.4 may optimize, but cannot weaken the idempotency and replay-equivalence baseline established by Stories 1.13-1.14.
