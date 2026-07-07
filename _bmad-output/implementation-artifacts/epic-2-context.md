# Epic 2 Context: External Integration Surfaces

<!-- Generated from planning artifacts. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Epic 2 gives external application developers typed, generated REST APIs while preserving the platform boundary: generated controllers live in dedicated API hosts, interactive UI hosts use EventStore client libraries directly, and real-time projection notifications stay scoped, bounded, metadata-only, and backward compatible. This matters because external integrations need stable OpenAPI-visible command and query endpoints without duplicating controller logic, bypassing gateway policy, or letting UI hosts become accidental public API hosts.

## Stories

- Story 2.1: REST Contract Seam For Command And Query Messages
- Story 2.2: REST API Generator Discovery And Controller Emission
- Story 2.3: Sample External API Host Proof
- Story 2.4: Tenants External API Host Adoption
- Story 2.5: Scoped Metadata-Rich Projection Notifications

## Requirements & Constraints

- Command and query contracts intended for generated REST exposure must declare their surface explicitly. Command contracts expose stable domain and command-type identity plus aggregate identity; command and query contracts may declare route metadata; external API hosts opt in with assembly-level API metadata for route prefix, OpenAPI tag, and tenant-source behavior.
- The REST generator emits controllers only for opted-in command/query contracts and ignores non-marker types. Generated controllers must be OpenAPI-visible, compile under nullable and warnings-as-errors rules, use file-scoped namespaces, and preserve the repository's async conventions.
- Generated query actions delegate to `IEventStoreGatewayClient.SubmitQueryAsync` and map gateway outcomes consistently, including success, `304`, ETag handling, freshness, projection version, served-at, degraded/warning state, paging evidence, not found, forbidden, validation failures, and safe problem details.
- Generated query responses forward support-safe metadata headers only when values are present and bounded. Projection-confirmed state must come from platform query metadata, not payload-specific fields, and generated controllers must not expose cursor or ETag internals as support text.
- Generated command actions delegate to `IEventStoreGatewayClient.SubmitCommandAsync`, generate ULID message/correlation identifiers when needed, and return a safe `400` for route/body aggregate mismatches without rewriting command payloads.
- Generated REST controllers belong in dedicated external-facing API hosts. Interactive Sample and Tenants UI hosts must consume EventStore or domain client libraries directly and must not host generated or hand-written per-message MVC command/query controllers.
- Sample must prove the pattern with a contracts-only library shared by the domain service, Sample external API host, and Sample Blazor UI. The shared contract identities must be compiled once rather than duplicated through linked source.
- Tenants must prove the same external API-host pattern for tenant management routes, replacing the former hand-written query controller surface while preserving gateway authorization, validation, status/archive behavior, and observability.
- Projection notifications must retain the existing signal-only path while adding a scoped detail path. Detail metadata is bounded, metadata-only, tenant-authorized, safe for scoped SignalR groups, and not logged above Debug level.
- Tenant isolation must hold across generated REST APIs, query metadata, SignalR groups, state-related identifiers, and authorization checks. Tenant access is validated before generated REST or notification paths disclose resource existence.

## Technical Decisions

- The EventStore gateway is the command and query policy boundary. Generated REST controllers, UI hosts, Admin surfaces, and domain services must call platform seams instead of bypassing gateway authorization, tenant validation, idempotency, status/archive, ETag, problem-details, and observability behavior.
- `Hexalith.EventStore.RestApi.Generators` is the analyzer-driven controller generator. External API hosts reference contracts and the generator, then delegate generated actions through `IEventStoreGatewayClient`.
- Query evidence flows through platform metadata from domain/projection query results, through gateway results, into generated API headers or UI client state. The gateway owns HTTP ETag behavior and may populate metadata from its selected strong validator.
- ETags and cursors remain opaque. They can be forwarded as bounded protocol metadata but must not be parsed, displayed as diagnostic/support text, or treated as business payload.
- Projection delivery is a freshness signal, not proof of completed user-visible work. DAPR pub/sub and projection notifications are at-least-once and unordered; consumers deduplicate by EventStore `MessageId`, filter scoped detail notifications where applicable, and re-query for read-model evidence.
- Read-model freshness, projection version, paging, and ETag evidence used by generated APIs or UIs must come through the platform query metadata path, including the query-handler and cursor work delivered before or alongside this epic.
- High-risk verification for generated API behavior and projection notification delivery must prove real platform behavior where required, not only mock calls or HTTP smoke status.

## UX & Interaction Patterns

- Interactive UI hosts use FrontComposer/Fluent UI patterns and EventStore client libraries; they do not embed generated or hand-written per-message MVC command/query controllers.
- HTTP `202`, SignalR notifications, and command acceptance render as accepted or evidence-pending states, not user-visible success. Success requires read-model or projection evidence returned through the client/query metadata path.
- UI flows that surface freshness, stale state, degraded state, paging, or projection-confirmed success must use platform metadata rather than ad hoc response fields.

## Cross-Story Dependencies

- Story 2.1 establishes the contract seam required by Story 2.2's generator discovery and controller emission.
- Story 2.2 supplies the generator behavior, metadata forwarding, and error semantics required by the Sample and Tenants external API proofs in Stories 2.3 and 2.4.
- Story 2.3 validates the external API-host pattern on the Sample domain before the broader Tenants adoption in Story 2.4.
- Story 2.4 depends on the platform query metadata path from the domain query-handler and cursor work, plus Story 2.2 metadata forwarding, so Tenants APIs and UI do not fake freshness, paging, ETag, or projection-version evidence.
- Story 2.5 preserves existing SignalR consumers while adding scoped detail delivery; consumers must still re-query through the query path for projection-confirmed state.
