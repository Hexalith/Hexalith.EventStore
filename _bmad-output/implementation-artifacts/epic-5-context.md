# Epic 5 Context: Tenants and Administrators Are Protected by Fail-Closed Boundaries

<!-- Generated from planning artifacts. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Give tenant administrators, security engineers, and operators one consistent fail-closed security posture across Admin, internal/domain-service, projection-notification, and runtime infrastructure boundaries. The epic prevents anonymous or cross-tenant disclosure, rejects wire-asserted privilege and unsafe production authentication, preserves state safety on rejected commands, and keeps AppHost, DAPR configuration, deployment guidance, and observed runtime topology aligned.

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

- Infrastructure-failure and exhausted-conflict paths must clear staged actor state before restaging or committing rejection and cleanup state. Verification must inspect durable end state and prove that staged events, metadata, snapshots, publication state, or pending work did not leak into the rejection commit.
- Every Admin surface other than the explicit health probes must authenticate the caller, enforce current role and tenant scope, and reject before protected work. Denials must not disclose tenant or resource existence. Query counts and bodies are bounded at the HTTP boundary; ordinary JSON bodies default to 1 MiB, backup import is limited to 10 MiB, and oversized requests return bounded `413` Problem Details without partial work or payload echo.
- Non-development hosts fail startup when trusted authority, issuer, audience, HTTPS metadata, signature/lifetime validation, or a signing-algorithm allowlist is absent or unsafe. Symmetric-key production authentication requires an explicit, observable, narrowly scoped break-glass option. Committed reusable or production configuration contains no forgeable administrator identity, credentials, bearer tokens, secrets, or decoded token data.
- Internal, domain-service, projection-notification, and admin-computation endpoints require independently verified application credentials. Network location, mTLS, DAPR ACL decisions, plaintext caller-app headers, and caller-supplied administrator or tenant-override flags do not establish privilege. Denials perform no protected downstream or freshness work and emit only bounded, redacted evidence.
- OpenAPI is disabled by default outside Development unless a separately authenticated and operator-approved exposure is configured. Destructive CLI operations require explicit confirmation before a request is created. EventStore correlation handling is ULID-safe and bounded; `Guid.TryParse` is forbidden for EventStore envelope identifiers.
- Managed-tenant provisioning must reject the normalized reserved `system` identity at the authoritative server boundary and all available adapters before command submission or durable effects. Legitimate platform-owned `system` routing remains available only through operation-bound trusted contracts.
- Local and production topology must preserve tenant isolation and least privilege across app IDs, component scopes, topics, subscriptions, ACLs, resiliency, health, placement/scheduler inputs, and key-prefix posture. Missing scopes, placeholders, wildcard grants, generated fallbacks, or default-open omissions fail validation.
- Security and topology completion requires negative-path and production-equivalent evidence: persisted state, loaded component metadata, effective sidecar arguments, subscription inventory, denial observations, and zero downstream work. HTTP success, comments, mocks, and self-reported pass flags are supporting signals only.

## Technical Decisions

- The EventStore gateway remains the command/query policy boundary; external adapters do not bypass it to call domain services, actors, state stores, or query/projection infrastructure directly. `AggregateActor` remains the sole durable event-mutation coordinator.
- Application authorization sits above DAPR scoping. DAPR ACLs and mTLS are defense in depth; protected endpoints independently validate application identity and authorization. Platform-owned outbound `dapr-app-id` and `dapr-api-token` headers replace caller-provided values rather than appending to them.
- `/health`, `/alive`, and `/ready` are the only anonymous endpoints. Each is explicitly anonymous, exposes only support-safe status outside Development, and is introduced no later than any fallback/default-deny policy; the default policy is never weakened to make probes reachable.
- AppHost and DAPR YAML are one governed topology. Tracked configuration paths are canonical and loaded exactly once; service-invocation-only sidecars receive no accidental component access. Topology changes update modeled resources, production scopes, subscriptions, ACLs, resiliency, documentation, and drift tests together.
- Drift verification derives normalized typed topology projections from AppHost and every supported production template, compares them exactly except for explicitly owned environment differences, and corroborates them with a production-equivalent DAPR/Aspire runtime lane. Unavailable provider lanes remain unproven rather than passed.
- Production operational and application secrets follow the adopted DAPR `openbao` architecture, default-deny per-application secret scopes, and `secretKeyRef` usage. Epic 5 aligns topology and documentation with that contract; executable OpenBao retrieval and proof remain owned by Story 7.6 and cannot be claimed here.

## UX & Interaction Patterns

Restricted Admin views and actions render a support-safe denied state without implying that a hidden resource exists, and focus returns to the initiating control. Oversized or reserved-tenant input uses inline Fluent validation without submitting a request. Destructive operations identify their target, effect, and permission context and require confirmation; accepted work is not presented as completed without evidence. Topology surfaces render missing or contradictory runtime evidence as `Unknown`, degraded, or unavailable. Interactive work uses FrontComposer and Fluent UI Blazor V5, text in addition to status color, resource-backed copy, and WCAG 2.2 AA behavior.

## Cross-Story Dependencies

- Story 5.1 is the Phase 0 state-safety gate. Story 5.2 follows it; Stories 5.3 and 5.4 build on the Admin authorization boundary, and Story 5.5 additionally depends on the production credential posture from Story 5.3.
- Story 5.5 establishes the protected application boundary transported by Story 5.6. Story 5.7 aligns production DAPR configuration with Stories 5.5 and 5.6; Story 5.8 binds the AppHost and production slices into the combined static/runtime drift gate. Story 5.9 documents only the topology proven by Stories 5.6-5.8.
- Story 5.10 depends on Story 5.2's authenticated Admin and tenant-filter contract. Story 7.6 retains OpenBao implementation and retrieval-proof ownership, while later Admin/operator stories consume Epic 5 denial and topology evidence without redefining it.
