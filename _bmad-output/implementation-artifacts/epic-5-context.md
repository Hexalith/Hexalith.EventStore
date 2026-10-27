# Epic 5 Context: Tenants and Administrators Are Protected by Fail-Closed Boundaries

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Protect tenant and administrator operations through consistent application authentication, authorization, tenant isolation, internal-call credentials, and verified runtime topology. Immediate security fixes precede exposed dependent surfaces; local success or a completed story never supplies production authority by itself.

## Stories

- Story 5.1: Infrastructure Failure Cache Clear
- Story 5.2: Admin Endpoint Authorization And Tenant Filters
- Story 5.3: Production Authentication Guards And Secret Stripping
- Story 5.4: Admin Surface Safety Hygiene
- Story 5.5: Internal And Domain-Service Trust Boundary
- Story 5.6: AppHost Component Loading And Sidecar-Argument Parity
- Story 5.7: Production DAPR Component And ACL Parity
- Story 5.8: Runtime Topology Drift Tests
- Story 5.9: Deployment And Operator Documentation Alignment
- Story 5.10: Reserved System Tenant Provisioning Guard
- Story 5.11: Shared Versioned JWT Contract And All-Host Conformance
- Story 5.12: Route And Idempotency Catalog Schema, Codec, And Envelope
- Story 5.13: Route Catalog Activation And Topology Binding
- Story 5.14: Authenticated Fallback And Dapr Framework-Route Protection On Every HTTP Host

## Requirements & Constraints

- Authenticate and authorize the current operation and scope before admission, routing, actor calls, state access, or existence disclosure. Network location, app IDs, mTLS, ACLs, and wire administrator flags never grant application authority. Infrastructure failures must discard staged state without committing partial work.
- Require one explicit request scope: a canonical tenant or a cataloged platform-operation namespace, never both. Grants authorize scope but never select it. The shared Contracts canonicalizer lowercases request tenants and tenant grants, then validates 1–64 characters against `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`. Missing, duplicate, conflicting, malformed, unauthorized, or reserved `system` scope fails before downstream work; whitespace is never repaired.
- Public boundaries reject `system` even for administrators. Platform operations use a disjoint Contracts-owned namespace and operation authorization. Historical storage identities, streams, and topics remain readable and attributable under an owner-approved migration; rejecting request scope does not authorize rewriting stored events.
- Every externally reachable or JWT-binding host uses the shared versioned JWT profiles. Signature, issuer, audience, lifetime, role, and tenant rules remain mandatory. Production rejects symmetric mode; authority mode requires HTTPS metadata outside Development and an explicit approved asymmetric algorithm allowlist. Credentials and signing material are injected through approved channels, never committed or disclosed.
- Admin reads and mutations enforce tenant filters and bounded inputs before upstream work. Destructive actions require explicit confirmation; acceptance is not completion. Swagger exposure is governed, correlation identifiers are ULID-safe, and denial responses disclose no protected data.
- AppHost models, loaded Dapr components, sidecar arguments, ACLs, subscriptions, resiliency, operator documentation, and tests agree. Default-open grants, unresolved placeholders, configuration drift, and unsupported production fallbacks fail closed.
- Proof is repository tests plus CI. Stories/specs record commands, counts, source revision, and CI link. Evidence directories are ignored scratch; do not restore deleted authorization/evidence machinery from historical instructions. Remaining assurance and production gates still bind. Isolation and mutation proof includes persisted state through the owning Dapr boundary, not merely response codes or mocks.

## Technical Decisions

- Contracts owns one versioned routing/idempotency envelope and canonical UTF-8 codec. Stable route IDs join route and admission facets under one root digest and generation. Commands/queries resolve by domain and message type; projections by domain and projection type. Exact entries precede explicitly bounded fallbacks. Unknown fields, unsupported versions, noncanonical bytes, ambiguous keys, facet mismatch, and runtime overrides outside Development are rejected.
- Catalog entries derive MessageId versions/declaration digests, the platform namespace, and admitted credential kind/operation from declarations. They govern the target hop; message/projection entries use workload credentials. Delegated-user calls relay the human credential; workload calls use one short-lived audience/operation-bound assertion. Mixed kinds fail. Workload principals never become human, tenant, or administrator principals.
- Dapr app-channel authentication proves passage through the receiver's sidecar, not caller authority. Internal invocation also validates its admitted credential and operation. Channel-only framework callbacks use an explicit policy that cannot satisfy the public authenticated fallback. Protected actor methods require bound execution contexts until runtime restriction is qualified.
- ServiceDefaults owns JWT/assertion validation contracts; hosts do not invent signing protocols. Before the catalog specification freezes, the owner settles the shared operation-vocabulary owner, human-bearer operation claim type, and whether sidecar-reachable ingress endpoints join the catalog. Existing ingress credential requirements remain endpoint metadata until that decision. Current architecture adopts the catalog fields; older story prose calling them assumptions is stale.
- Catalog construction and activation are separate deliverables. Construction supplies schema, codec, validation, and the Development/test envelope. Activation later coordinates prepare/ready/commit/rollback and topology digest agreement. The deployment owner supplies the production instance; an unactivated catalog grants no production readiness.

## UX & Interaction Patterns

Denied views reveal no hidden resource existence. Authentication loss clears protected state; failures show bounded, support-safe outcomes. Destructive actions identify target and effect, require confirmation, and await authoritative completion evidence. Tenant creation rejects reserved identifiers before submission while preserving server enforcement. Interactive-host static assets/login callbacks remain subject to the unresolved PRD/architecture anonymous-exception reconciliation; do not invent an exemption.

## Cross-Story Dependencies

Phase 0 fixes precede dependent surfaces. Story 5.3 precedes internal trust-boundary and all-host JWT conformance; 5.5 supplies credentials and channel policies consumed by topology and framework-route work. Stories 5.6–5.9 align and prove runtime topology. Story 5.10 owns provisioning rejection, while 2.14 owns corrected request boundaries and legacy `system` migration.

Story 5.12 precedes 2.14 and 2.15; 5.13 consumes 5.12 plus topology/ACL work. Platform production-instance signing belongs to the publication-authority work. Earlier host/generator completion does not close corrected tenant, command-status, or all-host-authentication gates. Changes in Tenants belong to its repository owner.
