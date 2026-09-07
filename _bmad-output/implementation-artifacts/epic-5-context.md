# Epic 5 Context: Tenants and Administrators Are Protected by Fail-Closed Boundaries

<!-- Generated from planning artifacts. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Establish one consistent fail-closed security posture for tenants, administrators, internal services, and deployed infrastructure. This epic prevents anonymous or cross-tenant disclosure, unsafe production authentication, wire-asserted privilege, staged-state leakage, and runtime topology drift while keeping operator-facing behavior explicit, support-safe, and verifiable.

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

- Authentication, role authorization, and current tenant scope must be resolved before any protected read, mutation, admission, actor, DAPR, or audit work. Denials disclose neither protected data nor whether a hidden tenant or resource exists. Network location, DAPR identity, and caller-supplied administrator flags are insufficient authorization.
- `/health`, `/alive`, and `/ready` are the only anonymous surfaces. All other public, Admin, internal, domain-service, projection-notification, and admin-computation endpoints fail closed with application-layer credentials.
- Production authentication rejects incomplete or unsafe authority, metadata, issuer, audience, validation, and signing-algorithm configuration. Any narrowly approved symmetric-key exception remains constrained and auditable; it never disables issuer, audience, signature, lifetime, role, or tenant validation.
- Committed configuration and reusable fixtures contain no forgeable administrator identity, signing material, passwords, tokens, client secrets, decoded JWT data, or operational secrets. Development and test credentials enter through user-secrets, environment variables, ephemeral tooling, or runtime-generated fixtures and cannot become a non-Development fallback.
- Tenant isolation applies consistently to state keys, actor identities, topics, Admin queries, generated APIs, SignalR groups, and deployment configuration. User-controlled provisioning rejects the reserved `system` tenant identity without changing legitimate platform-owned `system` routing.
- Rejected infrastructure and conflict paths must not commit previously staged actor state. Admin input is bounded at the HTTP boundary, destructive tools require explicit intent, and production API discovery is not exposed by an unsafe default.
- AppHost, DAPR component/configuration YAML, deployment templates, and tests must agree on app identities, sidecar inputs, component and topic scopes, ACLs, key-prefix posture, resiliency, health, and placement/scheduler behavior. Missing scopes, placeholders, broad grants, generated fallbacks, and default-open omissions fail validation.
- High-risk completion evidence comes from real host/runtime paths and durable observations: persisted state, loaded component metadata, effective sidecar arguments, subscription inventory, security denials, and zero protected downstream work. HTTP status, comments, mocks, and self-reported pass flags are not sufficient proof.

## Technical Decisions

- The EventStore gateway remains the command/query policy edge, and `AggregateActor` remains the sole durable event-mutation coordinator. External adapters do not bypass these boundaries to call domain services, actors, state stores, or query/projection infrastructure directly.
- Application authorization sits above infrastructure scoping. DAPR ACLs and mTLS are defense in depth; each protected endpoint independently verifies application identity and authorization.
- Every probe declares explicit anonymous metadata and returns status-only output outside Development. A fallback or default-deny policy is never weakened to restore probe reachability.
- AppHost and DAPR YAML form one governed topology. Resource, scope, ACL, subscription, resiliency, documentation, and drift-test changes land together, with only explicit and owned environment differences.
- EventStore envelope identifiers use ULID-safe handling; GUID parsing is not a substitute where sortable EventStore identity semantics apply.
- Production operational and application secrets follow the adopted DAPR `openbao` contract and default-deny application scopes. Epic 5 aligns topology and documentation with that architecture; executable OpenBao retrieval and proof remain owned by Story 7.6.

## UX & Interaction Patterns

Restricted views and actions render a canonical support-safe denied state without implying hidden-resource existence, and keyboard focus returns to the initiating control after denial or validation failure. Destructive actions identify target, impact, and required permission before confirmation; accepted work is not labeled complete without evidence. Missing or contradictory topology evidence appears as unknown, degraded, or unavailable. Admin experiences use FrontComposer and Fluent UI Blazor V5, accessible text in addition to color, resource-backed copy, and WCAG 2.2 AA behavior; tokens, decoded claims, raw protected payloads, secret values, stack traces, cursors, and ETag internals are never rendered.

## Cross-Story Dependencies

Story 5.1 is the staged-state safety gate. Story 5.2 establishes the Admin authorization and tenant-filter boundary used by Stories 5.3, 5.4, and 5.10; Story 5.3 supplies the authentication prerequisite for Story 5.5. Story 5.5 establishes the application trust boundary transported by Story 5.6, Story 5.7 aligns production configuration to that model, Story 5.8 proves modeled/configured/runtime parity, and Story 5.9 documents only the topology established by Stories 5.6-5.8. Story 7.6 retains OpenBao implementation and retrieval-proof ownership.
