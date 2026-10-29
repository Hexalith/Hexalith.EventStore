# Epic 5 Context: Tenants and Administrators Are Protected by Fail-Closed Boundaries

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Protect tenant and administrator operations through application authentication, authorization, tenant isolation, internal-call credentials and verified topology. Security corrections precede exposed surfaces; local implementation or declaration delivery supplies no production authority.

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

Authenticate and authorize operation and scope before admission, routing, actor calls, state access or existence disclosure. App IDs, mTLS, ACLs and caller administrator flags grant no application authority. Infrastructure failures discard staged actor state before persisting rejection state; proof inspects durable end-state.

Require one explicit canonical tenant or cataloged platform namespace; grants never select scope. Tenant identifiers normalize to lowercase, are 1–64 characters and match `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`; whitespace is never repaired. Missing, duplicate, conflicting, malformed, unauthorized or reserved `system` scope rejects before work, including for administrators. Preserve historical storage identity.

Externally reachable or JWT-binding hosts use shared validation profiles for signature, issuer, audience, lifetime, roles and tenants. Production rejects symmetric authentication; authority mode requires HTTPS metadata outside Development and approved algorithms. Profile fingerprint drift fails readiness. Secrets never enter committed configuration, logs or catalogs.

AppHost, rendered components, sidecar arguments, ACLs, subscriptions, resiliency and loaded state must agree. Default-open grants and unsupported fallbacks reject. Production requires owner ratification; repository fixtures grant no deployment authority.

Proof is tests plus CI, with commands, results, revision and CI link. Evidence directories remain ignored scratch. Compatibility, assurance and production gates bind independently.

## Technical Decisions

Contracts owns the canonical UTF-8 routing/idempotency envelope. Stable entry IDs join routing/admission facets under one generation/root digest. Domain/message or domain/projection keys bind one app ID, method and contract version. Exact keys precede declared bounded fallbacks. Noncanonical, unsupported, ambiguous or mismatched entries and non-Development overrides reject.

Declarations supply MessageId version/digest, platform namespace, exact human permission and target-hop credential kind/operation. Contracts owns operation identifiers; ServiceDefaults owns authentication. Human platform grants match `eventstore:permission` exactly, without wildcard/admin bypass. Workload principals never satisfy human policies or carry tenant/admin claims.

Dapr channel authentication proves receiver-sidecar passage, not caller authority. Invocation additionally validates the single credential kind and operation its endpoint admits; mixed kinds reject before work. A capability needed by both kinds uses distinct operations/keys. The earlier either-kind Operations-route criterion is explicitly superseded. General ingress kind, operation and any required resource-bound family remain endpoint metadata. Framework callbacks use an explicit channel-only policy that cannot satisfy the authenticated fallback. Actor methods that disclose, admit or mutate require an issuer-allow-listed execution context bound to tenant and operation until receiving-runtime restrictions are independently qualified. Hosts share registered validation profiles and signing protocols.

Declaration construction and runtime certification are separate. Compiled owner declarations produce deterministic Development/test bytes and declared-target parity. Runtime authority rejects undeclared targets even after readiness succeeds; missing/stale metadata preserves the committed target or rejects.

Activation uses retained bytes, prepare/ready/commit and complete-generation rollback. Every required host, including generated APIs, agrees on targets and root/facet digests; partial generations, trust failures or ACL drift fail readiness. Cross-host drills read served generations through Dapr. Production requires an activated owner-bound instance.

## UX & Interaction Patterns

Denials disclose no resource existence; authentication loss clears protected state. Destructive actions identify target/effect, require confirmation and await authoritative completion. Reserved-tenant validation prevents submission without existence disclosure.

Interactive-host planning is inconsistent: PRD NFR1 already permits individually enumerated, data-free framework assets and authentication callbacks with explicit anonymous metadata and tests, and excludes interactive hosts from the MVP production profile until human login exists. The UX handoff records this as a settled owner decision requiring upstream reconciliation; architecture and Story 5.14 retain probes-only/pending-decision wording. Do not invent another product choice or claim conformance while the conflict remains. Sources: `prd.md:362,612`; `ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md:53,412`; `architecture.md` AD-16 and authenticated-fallback Implementation Status row; `epics.md` Story 5.14.

## Cross-Story Dependencies

Phase 0 safeguards precede dependent surfaces. Story 5.3 precedes internal-boundary and shared JWT conformance work; 5.5 supplies credentials/channel policies consumed by topology and framework-route work. Stories 5.6–5.9 establish and verify topology. Story 5.14 owns host fallback, framework-route policy and Operations endpoint authorization; 5.7 owns peer ACL restrictions and actor-invocation qualification on the 3.21 candidate runtime; 7.1 retains Operations delivery/capture/acknowledgment semantics. Story 5.10 owns provisioning rejection; 2.14 owns request boundaries and historical `system` migration.

Stories 2.14/2.15 consume accepted 5.12 declaration/schema/codec delivery subject to remaining package, migration, compatibility and assurance gates; declaration consumption does not require 5.13 activation. Story 5.13 consumes declaration delivery and 5.6/5.7 topology/ACL contracts and owns exhaustive runtime parity, metadata-outage stability/rejection, cross-host agreement and activation. Platform supplies/signs the production instance through publication-authority work. Story 3.19 production issuance remains separate; historical Story 9.1 references do not require recreating owner-retired authorization machinery. Catalog implementation grants no production authority. Tenants changes belong to its repository owner.
