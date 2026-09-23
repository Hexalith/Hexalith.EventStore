# Epic 5 Context: Tenants and Administrators Are Protected by Fail-Closed Boundaries

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Give tenants and administrators one fail-closed posture for authentication, authorization, tenant isolation, internal endpoint protection, and runtime topology. This epic protects command and event evidence and its access paths so anonymous, cross-tenant, over-privileged, wire-asserted, staged-state, or drifted-topology paths cannot disclose or mutate protected data. Phase 0 protections precede exposed or administrative surfaces. No product brief is in the planning set; this context uses the epic, PRD, architecture, and UX handoff.

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

## Requirements & Constraints

- Authorize the current role and tenant before protected reads, mutations, admission, actor, DAPR, or audit work. Denials reveal neither protected data nor hidden-resource existence. Network location, DAPR identity, mTLS, ACLs, and caller-supplied administrator flags are not authorization.
- Only `/health`, `/alive`, and `/ready` are anonymous, with explicit anonymous metadata. Every other public, Admin, internal, domain-service, projection-notification, and admin-computation endpoint requires application credentials. Probe reachability never weakens default-deny. Outside Development, probe bodies are status-only: `Healthy`, `Degraded`, or `Unhealthy`.
- Each tenant-scoped request and `eventstore:tenant` grant resolves exactly one explicit tenant: invariant lowercase, then `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$` (1–64 characters), with no trim or repair. Missing, duplicate, conflicting, invalid, unauthorized, and reserved `system` values fail before routing, state access, or existence disclosure. Platform operations use a distinct authenticated scope and must not synthesize a tenant. User provisioning rejects `system`; platform-owned `system` routing stays on its trusted path.
- JWT-binding hosts share one versioned contract and expose its fingerprint. Issuer, audience, signature, lifetime, role, and tenant checks are mandatory, with 60-second clock skew. Authority mode requires HTTPS metadata outside Development and an explicit RS/PS/ES 256/384/512 allowlist. Production rejects symmetric keys even when break-glass is set; HS256 is only for Development or a neither-Development-nor-Production break-glass environment. Closing this epic’s hosts does not close Tenants, generated, or later JWT hosts.
- Committed configuration, templates, fixtures, and generated artifacts contain no forgeable administrator identity, signing material, password, token, client secret, decoded JWT, or operational secret. Credentials enter only through user-secrets, environment variables, runtime-generated fixtures, or Aspire AppHost secrets, and cannot load outside Development.
- Infrastructure failure and persistence-conflict retry must not commit previously staged actor state. A safe discard is claimed only after durable inspection. Admin JSON bodies are limited before service work: `1_048_576` bytes, except `10 * 1024 * 1024` for `AdminBackupsController.ImportStream`, with bounded `413` Problem Details when exceeded. Recent-command counts clamp to `1..1000` (default `1000`).
- Destructive commands refuse without an explicit confirm flag and separate acceptance from confirmed completion. Admin OpenAPI/Swagger stays unmapped outside Development unless a named, authenticated operator option enables it. Correlation accepts one bounded value or mints a ULID-safe replacement; GUID parsing is not validation.
- AppHost, DAPR components, production templates, ACLs, resiliency, health, placement/scheduler, docs, and tests are one topology. Missing scopes, placeholders, default-open omissions, broad grants, and generated fallbacks fail closed. Local-only resources get no silent production access, and connection secrets are not committed.
- Proof uses real pipelines and durable observations: persisted state, loaded components, sidecar arguments, subscriptions, denials, and zero protected downstream work. HTTP status, mocks, and self-reported passes are not enough.

## Technical Decisions

- The gateway is the command/query policy boundary. External entry points delegate to platform APIs. `AggregateActor` is the sole durable event-mutation coordinator.
- Application authorization sits above infrastructure scoping. Internal credentials must prove the caller. A plaintext app ID, loopback, ACL success, or network location never mints claims. Wire flags such as `IsGlobalAdmin` are untrusted.
- One handler replaces outbound `dapr-app-id` and `dapr-api-token` from trusted configuration and discards inbound control-plane headers. Non-Development DAPR app endpoints check the startup app-channel token in constant time; a missing token fails readiness. That token does not authenticate a claimed caller app ID.
- Every HTTP host uses an authenticated fallback. Only the three probes are explicitly anonymous.
- Topology identity, scopes, ACLs, subscriptions, resiliency, docs, and drift tests change together. Canonical profile artifacts override stale deployment prose.
- Production secrets use DAPR component `openbao`, the Secrets API, `secretKeyRef`, default-deny scopes, and TLS. Kubernetes Secrets are bootstrap-only. This epic may describe current availability; Story 7.6 owns executable OpenBao retrieval.
- Authorizing production evidence is self-managed Kubernetes with independent sidecars, PostgreSQL actor state, approved resiliency and broker, and OpenBao. Redis is Development/test only. Local convenience and Cosmos templates are not production authority.

## UX & Interaction Patterns

Denied screens do not confirm hidden tenants or resources, and focus returns to the initiator or a stable heading. Unauthenticated and expired sessions clear protected state. An unavailable authentication provider shows a bounded state with no fake login and no tokens, claims, or provider internals. Invalid or oversized input does not echo payloads. Unavailable operations stay hidden, disabled, or `501`. Destructive actions name target, effect, and required role, and they refuse without confirmation. Accepted work is not labeled complete without evidence.

Topology labels come from actual sidecar and component evidence. Missing or contradictory topology is unknown, degraded, or unavailable, without secrets or tenant/topic inventories. If a Create Tenant dialog exists, reserved `system` fails inline on the tenant-ID field, returns focus there, and sends no request; the server guard remains authoritative. The UX handoff assumes no provisioning control on Tenants & Access; that assumption does not waive the guard. Admin UI uses FrontComposer and Fluent UI Blazor V5. Never render tokens, claims, payloads, secrets, stack traces, or cursor and ETag internals.

## Cross-Story Dependencies

Story 5.1 is the Phase 0 staged-state gate. Story 5.2 is the Admin authorization and tenant-filter boundary for Stories 5.3, 5.4, and 5.10. Story 5.3 is the authentication prerequisite for Story 5.5. Story 5.5 defines the trust boundary Story 5.6 transports; Story 5.7 aligns production YAML and deny-by-default ACLs; Story 5.8 proves modeled, configured, and running parity; Story 5.9 documents only that verified topology.

Epic 4 produces trustworthy command and event evidence; this epic protects it and its access paths; Epic 7 presents and acts on it. Fail-closed denial evidence is required here before Epic 7 displays those contracts. Story 7.6 retains OpenBao retrieval proof.
