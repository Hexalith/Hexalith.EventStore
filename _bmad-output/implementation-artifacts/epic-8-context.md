# Epic 8 Context: Domains Can Opt Into Portable Payload Protection - Post-MVP

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Deliver an optional, EventStore-owned, provider-neutral payload-protection capability that gives domain modules one durable cryptographic protocol instead of domain-specific envelope encryption. The capability must preserve historical readability, keep legal policy with domains and production key custody with operators, prove a real production backend, and supply release, consumer-parity, rollback, and approval evidence before the Parties G5 availability gate can close. This is a separately gated post-MVP commitment and must not block or be counted toward Phase 4 MVP completion.

## Stories

- Story 8.1: Shared Payload-Protection Security Spec And ADR
- Story 8.2: Payload-Protection Contracts And Golden Vectors
- Story 8.3: pdenc-v2 Core Cryptographic Engine
- Story 8.4: Compatibility Readers And Mixed-History Routing
- Story 8.5: Policy And Key-Lifecycle Mechanics
- Story 8.6: Azure Key Vault Production Adapter Conformance
- Story 8.7: Server Persistence And Snapshot Integration
- Story 8.8: Package And Release Integration
- Story 8.9: Parties Dual-Provider Parity
- Story 8.10: Post-v2-Write Rollback Rehearsal
- Story 8.11: G5 Evidence And Approval Closure

## Requirements & Constraints

- The engine is additive, opt-in, and disabled by default. Existing consumers and providers retain their public, binary, source, serialization, and no-op behavior unless an approved breaking-change process and SemVer-major release authorize otherwise.
- `pdenc-v2` has one versioned, byte-stable authenticated-data and envelope contract. Golden vectors must make its output independently reproducible and prevent culture, serializer, default-value, or implementation-specific drift.
- Readers must preserve `json+pdenc-v1`, `json-redacted`, legacy-unprotected, protected-snapshot, `pdenc-v2`, mixed-history, and bounded unknown/opaque behavior. Unreadable or invalid protected content is never treated as plaintext, redacted success, absent data, or safely skipped data.
- Deleted, missing, denied, unavailable, malformed, tampered, or opaque states produce bounded typed outcomes and fail closed. Sensitive payloads, plaintext, protected bytes, keys, credentials, provider-private errors, unsafe metadata, and tenant data must not leak through diagnostics, telemetry, evidence, exports, or support surfaces.
- Key buffers are zeroed before leaving the decrypting or deriving scope, lifecycle changes invalidate caches, and rollout, historical reads, downgrade, and rollback after `pdenc-v2` writes require integration evidence.
- Domain extensions use `IPersonalDataPolicy` and `IErasureStateProvider`. Domains retain legal-basis, retention, erasure-orchestration, certificate/report, export, and UX decisions; EventStore owns durable formats and reusable mechanics; providers/operators retain production key custody and provisioning authority.
- High-risk proof inspects persisted state, real topology/backend behavior, package output, security denials, and restart/concurrency outcomes. Mocks, interface-only tests, HTTP status, or story completion do not establish production readiness.
- EventStore owner goldens, a production backend, Parties dual-provider compatibility, immutable release identity, historical-read proof, and successful rollback after v2 writes are all required before G5 can become available.

## Technical Decisions

- EventStore Contracts remains the single provider-neutral public authority. The approved future package boundary is a provider-neutral `Hexalith.EventStore.PayloadProtection` engine plus a companion `Hexalith.EventStore.PayloadProtection.AzureKeyVault` adapter; neither package enters the release inventory until the atomic release story.
- AES-256-GCM payload protection uses the frozen `pdenc-v2` envelope and byte-stable authenticated-data rules. Stable persisted event identity, aggregate-local sequence, property-path identity, payload kind, key version, and trusted persistence context bind protected bytes; caller-controlled metadata cannot redefine that identity.
- Azure Key Vault is the selected production adapter. It uses workload identity and exact-version cryptographic operations, while runtime identities remain unable to administer or export key-encryption keys. Development-only backends cannot count as production proof.
- The existing no-op provider remains the default until explicit, valid registration succeeds. Typed routing handles every supported historical format and fails closed for unsupported or unreadable content.
- AggregateActor remains the sole durable event-append coordinator. Protection and lifecycle mechanics must respect admission fences, complete protection before staging event state, and never let domain policies write EventStore infrastructure state.
- Release remains manifest-governed and package mode remains the default. Engine and adapter publication, external resources, consumer mutations, production enablement, and approval decisions each require their own content-bound authority.

## Cross-Story Dependencies

Story 8.1's approved normative digest and detached authorization permit only Story 8.2's exact-digest/source preflight and bounded contracts/goldens work. Story 8.3 requires Story 8.2 evidence; Stories 8.4 and 8.5 require Story 8.3 and may then proceed independently; Story 8.6 requires both compatibility routing and lifecycle mechanics; Story 8.7 follows real-backend conformance; Story 8.8 follows persisted Server-path evidence; Story 8.9 additionally requires separate Parties maintainer authority; Story 8.10 follows release and consumer-parity evidence; and Story 8.11 alone may close G5 after all prior evidence and named approvals are complete. Any changed normative byte, incompatible source drift, missing predecessor evidence, or absent external authority blocks the dependent slice rather than selecting a default.
