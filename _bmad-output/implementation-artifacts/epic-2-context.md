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
- Generated controllers, Tenants routes, the gateway, and Admin Server must resolve one explicit authorized scope before routing or state access. Normalize request tenants and grants to lowercase through the shared `Contracts` canonicalizer, then apply the 1–64-character grammar `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`. Reject missing, duplicate, conflicting, invalid, unauthorized, or reserved `system` inputs, including for global administrators. Grants authorize scope but never supply it. Platform operations use a separate authenticated, cataloged namespace with grammar disjoint from tenant IDs; a request naming both scopes fails.
- A generated command's `202` status `Location` is an absolute, gateway-authoritative URI only when a `MessageId` satisfies the endpoint-declared contract version. Otherwise omit the header or reject safely. `CorrelationId` is diagnostic only and cannot select status; callers cannot choose the grammar version.
- Projection notifications preserve signal-only compatibility and add scoped, metadata-only detail. `Contracts` owns ceilings of 16 entries and 2,048 UTF-8 bytes of the compact default System.Text.Json metadata object, including punctuation and escaping. Options may lower those ceilings; broadcasters clip and receivers reject oversized detail. Keys are opaque and values appear in logs only at Debug or below. Tenant authorization precedes group membership and cross-replica delivery.
- Generated output and source-generator packages must compile with zero warnings under the repository's nullable, code-style, identifier, and async rules. High-risk tenant, status, provenance, and notification claims need compiled/runtime and persisted-path evidence, including negative cases; earlier story completion labels do not close the corrective gates.

## Technical Decisions

- The gateway owns authorization, tenant policy, idempotency, status, ETag, errors, and observability. External adapters use platform seams rather than reading or mutating operational state directly.
- `Contracts` owns route metadata, tenant canonicalization, the platform-operation namespace, and MessageId contract versions. Catalog and generated-host identities derive from these declarations through one canonical codec. Existing `system` streams and topics must remain readable and attributable without rewriting events. Migration requires owner approval and public-surface compatibility classification; incompatible removal or behavior changes require an approved SemVer-major proposal.
- Query metadata carries explicit `ProjectionBacked`, `HandlerComputed`, or `Unknown` provenance. Projection versions are opaque equality tokens scoped to their read-model lineage; consumers neither parse them nor infer lifecycle from ETags, payloads, HTTP success, or SignalR.
- Projection signals are best-effort freshness hints; Dapr pub/sub is at-least-once and unordered. Consumers tolerate loss, duplicates, and reordering, then re-query for persisted projection evidence. Direct notification remains the only production-eligible transport. Story 2.13 closed on 2026-10-25 with an accepted, isolated opt-in Redis backplane exception; it is excluded from production profiles and grants no production multi-replica freshness claim. Extension or retirement requires the exception's review/removal trigger and renewed qualification.
- The platform outbound Dapr handler replaces caller-supplied `dapr-app-id` and `dapr-api-token` with trusted configuration. AppHost and deployment Dapr components, scopes, ACLs, credentials, and tests change together when transport changes.

## UX & Interaction Patterns

- Command acceptance and notification arrival mean evidence is pending. Show projection-confirmed success only after a query returns authoritative, projection-backed `Current` evidence; stale, rebuilding, degraded, unavailable, local-only, and unknown states stay distinct.
- Reconnect and rejoin fetch authoritative typed evidence. Missing, delayed, duplicated, or reordered notifications cannot establish freshness or completion. Mutation actions remain unavailable without trusted current evidence.

## Cross-Story Dependencies

- The contract seam precedes generator emission; generated controllers precede Sample and Tenants external-host proofs. Tenants UI and generated REST provenance consumption depend on Epic 1's query metadata path.
- Scoped notification contract and authorization behavior from 2.8 underpin 2.13's distribution qualification and 2.16's bounded-detail proof. A transport change must preserve both signal and detail behavior across replicas.
- Story 2.14 depends on Story 2.12, accepted Story 5.12 declaration/schema/codec delivery, and a Story 9.1 authorization record for G-TENANT. Story 2.15 consumes the same declaration delivery with its separate G-STATUS-ID authorization. This prerequisite excludes 5.13's runtime certification/activation: 5.13 owns rejection of undeclared targets, exhaustive runtime parity, metadata-outage target stability or rejection, and cross-host agreement. Package/validation delivery, owner-approved migration, NFR12 compatibility and assurance gates remain required; declaration success does not automatically unblock either story. Story 5.12 remains in-progress and 2.14 remains draft/pending. Tenants changes belong to its repository owner; Admin endpoint policy remains with 5.2 and the managed-tenant provisioning guard with 5.10. Earlier generator/host completion authorizes neither corrected contract.
