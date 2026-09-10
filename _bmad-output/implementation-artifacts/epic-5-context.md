# Epic 5 Context: Tenants and Administrators Are Protected by Fail-Closed Boundaries

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Establish one fail-closed security posture: authentication, authorization, tenant isolation, internal endpoint protection, and runtime topology stay aligned across hosts, Admin surfaces, DAPR, and deployment. This epic protects command and event evidence so anonymous, cross-tenant, over-privileged, wire-asserted, staged-state, or topology-drifted paths cannot disclose or mutate protected data. Phase 0 protections must precede any exposed or administrative surface.

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

- Authenticate and authorize current role and tenant before any protected read, mutation, admission, actor, DAPR, or audit work. Denials disclose neither protected data nor whether a hidden tenant or resource exists. Network location, DAPR identity, mTLS, ACLs, and caller-supplied administrator flags are not authorization.
- Only `/health`, `/alive`, and `/ready` are anonymous, and they carry explicit anonymous metadata. Every other public, Admin, internal, domain-service, projection-notification, and admin-computation endpoint fails closed with application-layer credentials. Probe reachability never weakens a fallback or default-deny policy.
- Tenant isolation covers state keys, actor identities, topics, Admin queries, generated APIs, SignalR groups, and deployment configuration. Each tenant-scoped request resolves exactly one explicit tenant through the shared canonicalizer: invariant lowercase, grammar `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$` (1–64 characters), no trim or repair. Missing, duplicate, conflicting, invalid, unauthorized, and reserved `system` values fail before routing, state access, or existence disclosure. Platform-wide operations use a distinct authenticated platform scope and must not synthesize a tenant. User-controlled provisioning rejects `system` without changing platform-owned `system` routing.
- JWT-binding hosts consume one shared versioned validation contract. Issuer, audience, signature, lifetime, role, and tenant checks remain mandatory, with 60-second clock skew. Authority/OIDC mode requires HTTPS metadata outside Development and a nonempty explicit asymmetric-algorithm allowlist. Production always rejects symmetric-key mode, including when `AllowInsecureSymmetricKey` is set; that break-glass admits HS256 only in neither-Development-nor-Production environments. Closing this epic’s bound hosts does not close Tenants, generated, or later JWT hosts.
- Committed configuration and reusable fixtures contain no forgeable administrator identity, signing material, passwords, tokens, client secrets, decoded JWT data, or operational secrets. Development and test credentials enter only through user-secrets, environment variables, runtime-generated fixtures, or the Aspire AppHost secret mechanism, and cannot load as a non-Development fallback.
- Rejected infrastructure and conflict paths must not commit previously staged actor state. Admin JSON bodies are bounded at the HTTP edge: `1_048_576` bytes by default, with `AdminBackupsController.ImportStream` the sole `10 * 1024 * 1024` exception. Destructive tools require explicit intent. Production API discovery stays off unless a separately named, authenticated operator option enables it.
- AppHost, DAPR YAML, deployment templates, and tests must agree on app identities, sidecar inputs, component and topic scopes, ACLs, key-prefix posture, resiliency, health, and placement/scheduler behavior. Missing scopes, placeholders, broad grants, generated fallbacks, and default-open omissions fail validation.
- High-risk proof uses real host/runtime paths and durable observations: persisted state, loaded component metadata, effective sidecar arguments, subscription inventory, security denials, and zero protected downstream work. HTTP status, mocks, and self-reported pass flags are not enough.

## Technical Decisions

- The EventStore gateway remains the command/query policy edge. External adapters do not bypass it to call domain services, actors, state stores, or query/projection infrastructure. `AggregateActor` remains the sole durable event-mutation coordinator.
- Application authorization sits above infrastructure scoping. DAPR ACLs and mTLS are defense in depth. Internal credentials must prove sidecar/workload origin; a plaintext caller app ID, loopback, ACL success, or network location never mints administrator, tenant, or domain claims. Wire assertions such as `IsGlobalAdmin` are untrusted data.
- One platform handler replaces outbound `dapr-app-id` and `dapr-api-token` from trusted configuration and discards inbound control-plane headers. Non-Development DAPR app endpoints authenticate the app channel with the startup app API token; that token does not authenticate a claimed caller app ID.
- Every HTTP host uses an authenticated fallback. Only the three probes are explicitly anonymous and, outside Development, return status-only `Healthy`, `Degraded`, or `Unhealthy`.
- AppHost and DAPR YAML form one governed topology. Resource, scope, ACL, subscription, resiliency, documentation, and drift-test changes land together, with only explicit owned environment differences. Envelope and Admin correlation identifiers use ULID-safe handling; GUID parsing is not a substitute.
- Production secrets follow the adopted DAPR `openbao` contract and default-deny application scopes. This epic aligns topology and documentation; executable OpenBao retrieval remains Story 7.6. Kubernetes Secrets may hold bootstrap credentials only; they are not an application-secret backend.

## UX & Interaction Patterns

Restricted views and actions render a canonical support-safe denied state without implying hidden-resource existence; focus returns to the initiating control. Unauthenticated or expired sessions clear protected state and never offer fake login or expose tokens, claims, or provider internals. Invalid or oversized requests show concise validation without echoing payloads. Unavailable operations stay hidden, disabled, or explicitly `501`, never successful. Destructive actions identify target, impact, required permission, and confirmation before work starts; non-interactive automation uses an explicit confirm flag. Accepted work is not labeled complete without evidence. Topology and health labels come from actual sidecar/component evidence; missing or contradictory topology is unknown, degraded, or unavailable.

Tenant-bearing inputs use one explicit canonical tenant. If a Create Tenant dialog is present, reserved `system` fails with accessible inline Fluent validation on the tenant-ID field, returns focus there, sends no request, and does not confirm resource existence; the server guard remains authoritative. The UX handoff currently assumes Tenants & Access has no provisioning control; that assumption does not waive the reserved-name guard. Admin experiences use FrontComposer and Fluent UI Blazor V5, resource-backed copy, and WCAG 2.2 AA behavior. Tokens, decoded claims, raw payloads, secrets, stack traces, catalog internals, cursors, and ETag internals are never rendered.

## Cross-Story Dependencies

Story 5.1 is the staged-state Phase 0 gate. Story 5.2 establishes the Admin authorization and tenant-filter boundary used by Stories 5.3, 5.4, and 5.10. Story 5.3 is the authentication prerequisite for Story 5.5. Story 5.5 defines the application trust boundary that Story 5.6 transports; Story 5.7 aligns production YAML and ACLs; Story 5.8 proves modeled, configured, and runtime parity; Story 5.9 documents only that verified topology.

Epic 4 produces trustworthy command/event evidence; this epic protects that evidence and its access paths; Epic 7 presents and acts on it. Fail-closed denial semantics must be evidenced here before Epic 7 integrates or displays them. Story 7.6 retains OpenBao retrieval proof.
