# Epic 5 Context: Tenants and Administrators Are Protected by Fail-Closed Boundaries

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Protect tenant and administrator operations through consistent application authentication, authorization, tenant isolation, internal-call credentials, and verified runtime topology. Immediate security corrections precede dependent exposed surfaces. Local success, declaration delivery, or a completed implementation never supplies production authority by itself.

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

Authenticate and authorize the current operation and scope before admission, routing, actor calls, state access, or existence disclosure. App IDs, mTLS, ACLs, network location, and caller-asserted administrator flags never grant application authority. Infrastructure failures discard staged actor state before persisting permitted rejection state; proof inspects durable end-state rather than mock counts alone.

Require exactly one explicit canonical tenant or cataloged platform-operation namespace. Grants authorize scope without selecting it. Tenant identifiers are lowercase, 1–64 characters, matching `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`; whitespace is never repaired. Missing, duplicate, conflicting, malformed, unauthorized, or reserved `system` scope fails before downstream work, including for administrators. Historical storage identities remain readable and attributable; request-boundary correction does not authorize rewriting events.

All externally reachable or JWT-binding hosts use shared validation profiles for signature, issuer, audience, lifetime, roles and tenants. Production rejects symmetric authentication; authority mode requires HTTPS metadata outside Development and approved asymmetric algorithms. Fingerprint mismatch fails conformance/readiness. Secrets never enter committed configuration, logs or catalog bytes.

AppHost, rendered components, sidecar arguments, ACLs, subscriptions, resiliency, documentation and loaded state agree. Default-open grants, placeholders, unsupported fallbacks and drift fail closed. Production topology requires owner ratification; repository fixtures grant no shared-host deployment authority.

Proof is tests plus CI, recorded with commands, results, source revision and CI link. Evidence directories remain ignored scratch; do not recreate historical evidence machinery. Corrective-work, compatibility, assurance and production gates bind independently.

## Technical Decisions

Contracts owns the versioned routing/idempotency envelope and canonical UTF-8 codec. Stable entry IDs join route and AD-25 admission facets under one generation and root digest. Commands/queries map by domain and message type, projections by domain and projection type, to one app ID, method, and contract version. Exact keys precede explicitly declared bounded fallbacks. Unknown fields, unsupported versions, noncanonical bytes, ambiguous entries, facet mismatch, and runtime overrides outside Development are rejected.

Declarations supply MessageId version/digest, platform namespace and exact human permission, and target-hop credential kind/operation. Operation identifiers belong to Contracts; ServiceDefaults retains authentication and compatible forwarding constants. Human platform grants use exact `eventstore:permission` matching without wildcard or administrator bypass. General ingress credentials remain endpoint metadata; downstream message/projection hops admit workload credentials. Delegated-user and workload kinds never mix; workload principals never become human or tenant principals.

Dapr channel authentication proves receiver-sidecar passage, not caller authority. Invocation additionally validates credential and operation. Framework callbacks use an explicit channel-only policy distinct from human authentication. Hosts share registered validation profiles and signing protocols.

Declaration construction and runtime certification are separate. Construction derives deterministic Development/test bytes from compiled owner declarations, capabilities, policies, configuration and executable targets; exact-byte checks detect drift. Runtime authority rejects undeclared query/projection/actor targets before invocation, including after readiness succeeds. Missing or stale handler metadata preserves the committed declared target or rejects; it never silently selects another target.

Activation uses retained canonical bytes, prepare/ready/commit and rollback to a prior complete generation. Every required host, including generated API hosts enumerated by the production profile, agrees on generation, targets, and root/facet digests. Partial generations, signature/trust failures, fingerprint mismatches, and undeclared ACL operations/app IDs fail readiness. Cross-host drills read served generation through Dapr paths. Without an activated production instance, production readiness fails.

## UX & Interaction Patterns

Denials disclose no resource existence; authentication loss clears protected state. Destructive actions identify target/effect, require confirmation and await authoritative completion. Reserved-tenant validation prevents submission without existence disclosure. Interactive anonymous assets/callbacks await upstream architecture reconciliation; invent no exemption.

## Cross-Story Dependencies

Phase 0 safeguards precede dependent surfaces. Story 5.3 precedes internal-boundary and shared JWT conformance work; 5.5 supplies credentials/channel policies consumed by topology and framework-route work. Stories 5.6–5.9 establish and verify topology. Story 5.10 owns provisioning rejection; 2.14 owns request boundaries and historical `system` migration.

Stories 2.14/2.15 consume accepted 5.12 declaration/schema/codec delivery subject to remaining package, migration, compatibility and assurance gates; declaration consumption does not require 5.13 activation. Story 5.13 consumes declaration delivery and 5.6/5.7 topology/ACL contracts and owns exhaustive runtime parity, metadata-outage stability/rejection, cross-host agreement and activation. Platform supplies/signs the production instance through publication-authority work. Story 3.19 production issuance remains separate; historical Story 9.1 references do not require recreating owner-retired authorization machinery. Catalog implementation grants no production authority. Tenants changes belong to its repository owner.
