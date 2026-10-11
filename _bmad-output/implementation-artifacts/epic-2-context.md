# Epic 2 Context: API and UI Developers Get Safe Integration Surfaces

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Give developers typed external REST APIs and interactive clients through supported EventStore gateway contracts. Keep generated controllers in dedicated API hosts, UI hosts on client libraries, tenant access fail closed, and query and notification evidence honest about projection state.

## Stories

- Story 2.1: REST Contract Seam for Command and Query Messages
- Story 2.2: REST API Generator Discovery and Controller Emission
- Story 2.3: Sample External API Host Proof
- Story 2.4: Tenants REST Contract Metadata and Routes
- Story 2.5: Dedicated External Tenants API Host
- Story 2.6: Tenants UI Client-Library Alignment and UX Evidence
- Story 2.7: Pre-Authorization Registration and Provenance Correction
- Story 2.8: Scoped Metadata-Rich Projection Notifications
- Story 2.9: Generated Command-Status Location Policy
- Story 2.10: Outbound DAPR Routing-Header Ownership
- Story 2.11: Query Provenance Consumption in Generated REST and Tenants
- Story 2.12: Tenants Runtime Identity Adoption and Package-Mode Validation
- Story 2.13: Dapr Notification Distribution Qualification
- Story 2.14: Canonical Tenant Boundary In Generated Controllers And The Tenants Host
- Story 2.15: MessageId-Only Command-Status Identity
- Story 2.16: Bounded SignalR Detail Metadata
- Story 2.17: Generated-Code And Source-Generator Build Quality

## Requirements & Constraints

- REST exposure is explicit in command/query contracts and API-host metadata. Generated controllers are typed, OpenAPI-visible, and delegate through `IEventStoreGatewayClient`; Sample and Tenants prove the pattern in dedicated external hosts. Interactive UI hosts use client libraries and host no per-message command/query MVC controllers.
- Generated responses preserve safe gateway outcomes, including `304`, problem details, and present, bounded query metadata. Projection freshness, lifecycle, version, ETag, served-at, warnings, and paging come from platform metadata with route-bound provenance. Handler-computed, unknown, absent, or invalid provenance cannot become projection confirmation.
- Every generated controller and Tenants route must resolve one authorized canonical tenant before routing or state access. Normalize request tenants and grants through the shared `Contracts` rule; reject missing, duplicate, conflicting, invalid, unauthorized, or reserved `system` inputs. Platform-wide operations use a separate authenticated, cataloged scope, never a synthetic tenant.
- A generated command's `202` status `Location` is an absolute, gateway-authoritative URI only when a `MessageId` satisfies the endpoint-declared contract version. Otherwise omit the header or reject safely. `CorrelationId` is diagnostic only and cannot select status; callers cannot choose the grammar version.
- Projection notifications preserve signal-only compatibility and add scoped, metadata-only detail. SignalR detail metadata has a ceiling of 16 entries and 2,048 UTF-8 bytes across keys and values; keys are opaque and values appear in logs only at Debug or below. Tenant authorization precedes group membership and cross-replica delivery.
- Generated output and source-generator packages must compile with zero warnings under the repository's nullable, code-style, identifier, and async rules. High-risk tenant, status, provenance, and notification claims need compiled/runtime and persisted-path evidence, including negative cases; earlier story completion labels do not close the corrective gates.

## Technical Decisions

- The gateway owns authorization, tenant policy, idempotency, status, ETag, errors, and observability. External adapters use platform seams rather than reading or mutating operational state directly.
- `Contracts` owns route metadata, tenant canonicalization, and MessageId contract versions. Route-catalog and generated-host identities derive from those declarations; public compatibility changes require the applicable inventory and approval.
- Query metadata carries explicit `ProjectionBacked`, `HandlerComputed`, or `Unknown` provenance. Projection versions are opaque equality tokens scoped to their read-model lineage; consumers neither parse them nor infer lifecycle from ETags, payloads, HTTP success, or SignalR.
- Projection signals are best-effort freshness hints; Dapr pub/sub is at-least-once and unordered. Consumers tolerate loss, duplicates, and reordering, then re-query for persisted projection evidence. Direct notification is the current production-eligible mode. Cross-replica Dapr distribution requires exact-profile qualification; the retained Redis backplane is unresolved, not an accepted production exception.
- The platform outbound Dapr handler replaces caller-supplied `dapr-app-id` and `dapr-api-token` with trusted configuration. AppHost and deployment Dapr components, scopes, ACLs, credentials, and tests change together when transport changes.

## UX & Interaction Patterns

- Command acceptance and notification arrival mean evidence is pending. Show projection-confirmed success only after a query returns authoritative, projection-backed `Current` evidence; stale, rebuilding, degraded, unavailable, local-only, and unknown states stay distinct.
- Reconnect and rejoin fetch authoritative typed evidence. Missing, delayed, duplicated, or reordered notifications cannot establish freshness or completion. Mutation actions remain unavailable without trusted current evidence.

## Cross-Story Dependencies

- The contract seam precedes generator emission; generated controllers precede Sample and Tenants external-host proofs. Tenants UI and generated REST provenance consumption depend on Epic 1's query metadata path.
- Scoped notification contract and authorization behavior from 2.8 underpin 2.13's distribution qualification and 2.16's bounded-detail proof. A transport change must preserve both signal and detail behavior across replicas.
- The corrective tenant boundary in 2.14 depends on Tenants adoption and the route-catalog schema; the MessageId-only correction in 2.15 depends on the contract/catalog declaration. Their gate authorizations remain separate from earlier generator and host completion.
