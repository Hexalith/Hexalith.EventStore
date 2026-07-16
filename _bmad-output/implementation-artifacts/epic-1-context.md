# Epic 1 Context: Domain Author Self-Service Platform

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Enable domain authors to build and run EventStore-backed modules from domain behavior and contracts while EventStore supplies reusable hosting, DAPR endpoints, query and projection seams, read-model persistence, cursor protection, telemetry, health, Aspire wiring, testing support, and packaging. This reduces per-domain boilerplate without weakening operational safety, and permits consumers to remove local projection/query infrastructure only after the generic replacement is proven through production paths, owner-approved, and tied to an exact runtime identity.

## Stories

- Story 1.1: Canonical Domain-Service SDK Host
- Story 1.2: Domain Query Routing And Response Provenance
- Story 1.3: Persisted Read-Model Store And Write Policy
- Story 1.4: Deterministic Read-Model Testing Fake
- Story 1.5: Protected Query Cursor Codec
- Story 1.6: Projection And Domain Event Consumer Seams
- Story 1.7: Domain Module Hosting Observability
- Story 1.8: Sample Domain-Centric Adoption
- Story 1.9: Tenants Query And Read-Model Adoption
- Story 1.10: Tenants Projection And Event-Consumer Adoption
- Story 1.11: Domain-Module Adoption Guardrails
- Story 1.12: DomainService Packaging And Guardrails
- Story 1.13: Projection/Query SDK Owner Parity Proof
- Story 1.14: Read-Model And Projection Checkpoint Erasure
- Story 1.15: Coordinated Read-Model Batch Writes
- Story 1.16: Complete Projection Freshness Lifecycle
- Story 1.17: Asynchronous Multi-Projection Dispatch
- Story 1.18: Projection-Handler Delivery Idempotency
- Story 1.19: Correct Paged Rebuild And Replay Equivalence
- Story 1.20: Owner-Approved Parity Closure And Runtime Pin

## Requirements & Constraints

- Domain modules contain domain behavior and contracts only. Reusable request routing, projection/query actors, cursor codecs, state-store wrappers, telemetry, health checks, ServiceDefaults, Aspire plumbing, and per-message UI controllers belong to EventStore platform packages.
- The domain-service SDK owns the canonical host shape and DAPR-facing `/process`, `/replay-state`, `/query`, `/project`, and `/admin/operational-index-metadata` endpoints. It yields safely to an existing bespoke projection route, which must remain compatible.
- Query handlers are discovered and routed by the platform. Producer-authored freshness, lifecycle, projection version, paging, degraded state, and warnings must survive the gateway and client path; missing or non-authoritative evidence remains unknown. Gateway-echoed request paging is not authoritative evidence.
- Read-model contracts require ETag-aware reads and writes, bounded optimistic concurrency, deterministic production-equivalent testing semantics, coordinated same-store detail/index changes, and tenant/domain/aggregate/projection-scoped erasure of read-model plus delivery/rebuild checkpoint state. Erasure does not include event streams, snapshots, broker history, backups, audit evidence, or cryptographic keys.
- Cursors are purpose-isolated, scoped, bounded, opaque, DataProtection-backed, and fail safely for malformed or oversized data, tampering, wrong scope or query type, and key rotation.
- Projection dispatch and persistence are asynchronous and cancellation-aware, support multiple named projections per domain, expose truthful per-projection outcomes, and advance checkpoints only after all required durable work. At-least-once unordered delivery deduplicates by EventStore `MessageId`; sequence is never treated as globally ordered.
- Paged rebuilds must be semantically equivalent to canonical replay for the same event prefix. Incomplete work remains isolated from the last complete live model and resumes safely without claiming completion.
- Public, internal, domain-service, projection-notification, and admin-computation surfaces fail closed with application-layer credentials and tenant authorization before disclosure. Only `/health`, `/alive`, and `/ready` are explicitly anonymous, and their responses remain support-safe; the deny-by-default posture is never weakened to expose them.
- Sample and Tenants adoption preserves existing domain behavior while removing duplicated infrastructure. Release builds remain warnings-as-errors clean, package versions stay centralized, and DomainService/ServiceDefaults release output is governed only by `tools/release-packages.json`.
- Readiness-critical verification inspects persisted detail, index, marker, lifecycle, checkpoint, state-store, topology, or package evidence as applicable. HTTP status and mock-call evidence alone are insufficient.

## Technical Decisions

- The platform remains CQRS, DDD, and event sourcing over DAPR, with Aspire defining the local topology seed. Domain code returns domain results; durable event mutation remains platform-owned.
- Query evidence travels in platform metadata rather than ad hoc payload fields. Domain/projection evidence owns lifecycle, version, paging, degraded state, and warnings; the gateway owns the opaque HTTP ETag, fills served-at only when absent, and derives not-modified from the HTTP outcome. ETags never prove freshness or projection version.
- Query provenance is route-bound: only `ProjectionBacked` responses may carry authoritative `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, or `LocalOnly` lifecycle evidence. Handler-computed, missing, invalid, or unknown provenance resolves to `Unknown`.
- Read models and cursors use the platform seams. Same-store batches use a transaction or a documented resumable equivalent with explicit atomicity, partial-failure, idempotency, ordering, concurrency, and completion semantics; DAPR and in-memory implementations expose equivalent observable behavior.
- Projection handlers are keyed by `(Domain, ProjectionType)`. Partial fan-out success preserves independently durable sibling work, but failed or indeterminate routes do not advance. Full-replay and incremental rebuild semantics are explicit, work is staged, and promotion occurs only after every required projection completes durably. Compatibility requires an additive adapter or an explicitly approved breaking-version migration plan.
- AppHost resources, DAPR configuration, app IDs, scopes, ACLs, topics, sidecar options, and topology tests change together. Release output is governed by `tools/release-packages.json`; package validation uses package-reference mode by default, and source references never drive publication.

## UX & Interaction Patterns

UI consumers use FrontComposer and Blazor Fluent UI V5 and read lifecycle from client metadata. Command acceptance, HTTP `202`, and SignalR are evidence-pending signals, not success. Only authoritative projection-backed `Current` may enable otherwise-authorized mutations by default; every other lifecycle state disables mutation unless a documented consumer exception applies, and `LocalOnly` never counts as projection-confirmed success. Lifecycle is rendered with text, not color alone, and support surfaces never expose raw metadata, payloads, cursors, ETags, tokens, stack traces, or secrets.

## Cross-Story Dependencies

- Stories 1.1-1.7 establish the SDK, persistence, cursor, projection/consumer, observability, and Aspire seams used by the Sample and Tenants adoption stories and enforced by the guardrail and packaging stories.
- Story 1.2 establishes provenance and metadata propagation before Story 1.16 lifecycle evidence can be accepted. Story 1.17 builds on Stories 1.6 and 1.14-1.16; Stories 1.18-1.19 extend that production handler path with delivery correctness and replay equivalence.
- Story 1.13 records the original blocked parity proof; Stories 1.14-1.19 close its generic capability gaps. Story 1.20 remains blocked until those stories are complete and reviewed, every parity item is available through persisted production-path evidence, and owner approval names one exact EventStore source commit plus corresponding package and image identities.
- Consumer migration remains blocked unless its resolved EventStore source, packages, or deployed image match the approved Story 1.20 runtime identity. Later delivery-cost work may optimize the baseline but cannot weaken its idempotency, checkpoint, staging, promotion, or replay-equivalence guarantees.
