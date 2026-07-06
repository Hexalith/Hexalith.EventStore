# Epic 1 Context: Domain Author Self-Service Platform

<!-- Generated from planning artifacts. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Epic 1 makes EventStore domain modules domain-centric and reusable: domain authors should write aggregates, commands, events, projections, query handlers, validators, options, and contracts while the platform supplies hosting, DAPR endpoints, query routing, projection dispatch, read-model storage, cursor protection, telemetry, health checks, Aspire wiring, and packaging. The value is lower boilerplate for new domains, consistent runtime behavior across Sample/Tenants/future modules, and fewer opportunities for each domain to reinvent incompatible infrastructure.

## Stories

- Story 1.1: Canonical Domain-Service SDK Host
- Story 1.2: Domain Query Handler Routing
- Story 1.3: Generic Read Models And Query Cursors
- Story 1.4: Projection And Domain Event Consumer Seams
- Story 1.5: Domain Module Hosting Observability
- Story 1.6: Sample And Tenants Domain-Centric Adoption
- Story 1.7: DomainService Packaging And Guardrails

## Requirements & Constraints

Domain modules must stay focused on domain behavior and must not own reusable hosting, request routing, ServiceDefaults, Aspire wiring, projection/query actors, cursor codecs, DAPR state-store wrappers, telemetry sources, or health-check infrastructure when the EventStore platform supplies those seams.

The domain-service SDK must provide the canonical host extension shape and own the standard DAPR-facing endpoints for command processing, state replay, query dispatch, projection dispatch, and operational index metadata. The SDK must allow an application-owned projection route to remain in place where a domain already has bespoke `/project` behavior.

Query behavior must be implementable through discovered domain query handlers. The gateway must use operational metadata to route handler-served queries to domain services and preserve domain-produced query metadata end to end, including freshness, projection version, ETag, served-at, degraded or warning state, and paging evidence. Unknown freshness must stay unknown, and freshness-dependent requests must fail closed instead of assuming current data.

Persisted read models must use platform storage and write-policy abstractions with ETag-aware operations, optimistic-concurrency retry behavior, multi-key or index support, DAPR-backed implementation, and deterministic in-memory test support. Paging cursors must be protected, scoped, bounded, opaque, and safe on malformed input, tampering, wrong scope, wrong query type, oversize payloads, and key rotation.

Projection and event-consumer behavior must reuse platform dispatch, endpoint mapping, envelope/context handling, marker-based deduplication, and payload identity validation while keeping domain-specific logic in the domain. Event delivery remains at-least-once and unordered, so consumers must deduplicate by EventStore message identity.

Sample and Tenants must prove the model without losing existing domain semantics. Sample is the minimal reference; Tenants is the non-trivial reference that must preserve tenant RBAC, audit, read-model, pagination, and projection behavior while removing duplicated infrastructure.

Release packaging must include the DomainService and ServiceDefaults packages only through the EventStore release manifest. Package output must not depend on local submodule checkout state or publish packages outside the manifest-governed EventStore inventory.

Higher-risk tests must assert persisted state-store, read-model, CloudEvent, topology, or package-output evidence where applicable; HTTP success codes and mock calls are not enough for integration-quality proof.

## Technical Decisions

EventStore remains a DAPR-backed CQRS, DDD, and event-sourcing platform. The gateway is the command/query policy boundary, DAPR actors own durable aggregate mutation, and domain services are pure domain adapters that return domain results rather than writing EventStore state directly.

Domain-service hosts conform by calling the SDK host extensions. Reusable platform code belongs in EventStore Client, DomainService, ServiceDefaults, Aspire, and related platform packages, not in individual domains.

Read-model and cursor work must use `IReadModelStore`, `ReadModelWritePolicy`, `IQueryCursorCodec`, and `QueryCursorScope`. Cursors are DataProtection-backed and must remain implementation details: do not parse, log, render, or treat cursor contents as ordering proof.

Query evidence crosses the platform as `QueryResponseMetadata`; it must not be re-created as ad hoc payload fields. Domain/projection metadata is authoritative for freshness, projection version, paging, degraded state, and warnings. The gateway owns HTTP validator behavior, fills served-at only when absent, derives not-modified status from the HTTP outcome, and treats paging metadata as evidence only when produced by the query handler or projection.

Runtime topology changes must keep AppHost resources, DAPR component/configuration YAML, app IDs, state-store scopes, pub/sub scopes, ACL posture, resiliency paths, and topology tests aligned. Aspire-related host model changes require restarting the Aspire application.

Release and package validation run in package-reference mode by default. Source project references require an explicit opt-in and are not used for package publication. `tools/release-packages.json` is the release inventory.

AOT/trimming is not a target while reflection-based discovery remains load-bearing; implementation should keep that constraint explicit rather than trying to optimize around it.

## UX & Interaction Patterns

Interactive UI hosts must consume EventStore client libraries and must not host generated or hand-written per-message MVC command/query controllers. Any Sample or Tenants UI work that touches Epic 1 metadata must treat HTTP 202, command acceptance, and SignalR notifications as accepted/evidence-pending states, not success.

User-visible success requires read-model or projection evidence carried through platform metadata. Fresh, stale, unknown, paging, projection-version, and ETag evidence must remain support-safe; UIs must not render raw metadata, payloads, cursor internals, ETag internals, stack traces, tokens, or secrets. UI-affecting work uses FrontComposer and Blazor Fluent UI V5 conventions.

## Cross-Story Dependencies

Story 1.2 is the platform metadata foundation for Story 1.3 paging evidence and for later generated REST/UI metadata behavior outside this epic.

Story 1.3 provides the reusable read-model and cursor seams that the Tenants adoption proof in Story 1.6 depends on.

Story 1.4 and Story 1.5 provide projection/event-consumer, hosting, telemetry, health, and Aspire seams that Sample and Tenants adoption in Story 1.6 must consume rather than duplicate.

Story 1.7 should package and guardrail the seams after the SDK host, query, read-model, cursor, projection, observability, and adoption proofs have stabilized.
