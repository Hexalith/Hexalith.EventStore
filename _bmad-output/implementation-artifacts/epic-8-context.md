# Epic 8 Context: Domains Can Opt Into Portable Payload Protection - Post-MVP

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Provide an optional EventStore-owned payload-protection engine with stable cryptographic formats, shared policy and key-lifecycle mechanics, production-backend conformance, compatibility, governed release and exercised rollback. Domains retain legal-policy ownership and operators retain production key custody. This additive, disabled-by-default capability is post-MVP; it cannot block Phase 4 MVP completion. Parties migration requires a separately approved G5 availability decision.

**Owner scope clarification (2026-11-06):** Azure is outside the MVP. Staging/production targets on-premises Kubernetes at `192.168.1.30`, with infrastructure managed by [Hexalith.Platform](../../references/Hexalith.Platform/README.md). Story 8.6's Azure conformance is deferred post-MVP, as recorded in its [owner disposition](spec-8-6-azure-key-vault-production-adapter-conformance.md#owner-scope-disposition--2026-11-06). Do not request Azure provisioning or authority/custody documents as MVP prerequisites. The Azure story remains incomplete, its approved design is retained, and Platform's own deployment prerequisites still apply.

## Stories

- Story 8.1: Shared Payload-Protection Security Spec And ADR
- Story 8.2: Payload-Protection Contracts And Golden Vectors
- Story 8.3: pdenc-v2 Core Cryptographic Engine
- Story 8.4: Compatibility Readers And Mixed-History Routing
- Story 8.5: Policy And Key-Lifecycle Mechanics
- Story 8.6: Azure Key Vault Production Adapter Conformance
- Story 8.7: Server Persistence And Snapshot Integration
- Story 8.8: Package And Release Integration
- Story 8.9: Parties Dual-Provider Parity
- Story 8.10: Post-v2-Write Rollback Rehearsal
- Story 8.11: G5 Evidence And Approval Closure

## Requirements & Constraints

- Preserve existing provider-neutral metadata, readability outcomes, workflows, redaction contracts and no-op behavior. Public changes are additive and require source, binary, wire, serialization and package compatibility checks.
- Implement the approved `pdenc-v2` envelope, AES-256-GCM parameters, byte-stable authenticated data, canonical paths, identity/key-version bindings and bounds exactly. Independent golden reproduction and hostile-input tests govern durable bytes; ambiguity, tampering, nonce reuse and cross-scope substitution fail closed.
- Route each event and snapshot independently. Retain `json+pdenc-v1`, `json-redacted`, unprotected legacy and mixed histories. Unreadable protected content remains a bounded typed outcome, preserves stored bytes and stops dependent work without partial state, skipped input or checkpoint advancement.
- Keep deleted, missing, denied, unavailable, malformed, tampered and opaque states distinct. Preserve cancellation, inspect sensitive buffers after disposal to prove zeroing, and invalidate cached material on lifecycle changes.
- Domain policy selects protected values and owns legal basis, retention, erasure orchestration, certificates, reports and user copy. Shared mechanics preserve tenant isolation, monotonic epochs/watermarks, current authorization, bounded leases/indexes and reconciliation of ambiguous mutations. Cache, retries, restore and break-glass cannot override irreversible denial.
- No plaintext, protected bytes, key material, credentials, provider-private errors/identities or vault/key URIs escape through telemetry, exceptions, Admin, exports or support output. Diagnostics use closed safe fields and low-cardinality metric tags without tenant identities.
- Prove production behavior through real persisted state read through its owning Dapr state/actor boundary. Mocks, status codes, development stores, configuration and a single successful probe cannot establish production custody or conformance. Online wrapped-DEK invalidation never proves destruction of every recoverable replica, backup or restore copy.

## Technical Decisions

- EventStore owns formats, mechanics, provider registry, conformance and release proof; `IPersonalDataPolicy` and `IErasureStateProvider` preserve domain ownership. Reverify exact normative bytes, incorporated fixtures, current-source compatibility and predecessor authorization for every slice. Historical approvals and status labels do not authorize changed content.
- Dapr is the required infrastructure boundary. Qualify exact required cryptographic operations and guarantees before selecting provider dependencies, including supported HTTP/gRPC APIs when an SDK helper is absent. Unknown suitability remains unresolved. Unsupported operations need a separately accepted, narrowly isolated architecture exception; Dapr failure never permits direct-provider fallback.
- The detached Dapr amendment remains draft and non-authorizing in the planning baseline. Changed options, dependencies, operations and evidence require exact-content review. Dapr Crypto Scheme v1 cannot replace `pdenc-v2` or its authenticated bytes; a secret store is not a cryptographic key-operation component.
- The production custody profile uses ordinary Azure Key Vault Premium, provider-generated non-exportable RSA-HSM-3072 KEKs, RSA-OAEP-256, exact historical versions, deterministic workload identity, key-scoped runtime privileges, distinct provisioning authority and private connectivity. Runtime identities cannot administer or export KEKs. OpenBao application-secret custody is separate.
- AggregateActor remains the sole event append coordinator. Protection and durable key reservation precede staging; persistence supplies trusted occurrence identity. Readers, snapshots, replay, publication, projections, Admin and restore preserve compatible typed outcomes. Rotation and rollback never rewrite immutable history.
- Keep core and companion adapter non-packable through Server integration. Package integration alone atomically changes the manifest-governed release set from 14 to 16, with centralized dependencies, provenance and package-only consumer validation; the pair cannot publish partially.
- Proof is repository tests plus CI. Record commands, pass/fail counts, canonical source SHA and CI run link in the owning story/spec. Evidence directories are ignored scratch; run output belongs in workflow artifacts. Do not recreate removed committed evidence packets, manifests or materialization tooling. Remaining assurance and transition-seal requirements still apply.

## UX & Interaction Patterns

No new EventStore UI is required. Existing public/Admin surfaces expose bounded readability outcomes and safe reasons. Operational wording distinguishes disabled, configured, conformant, available, unreadable, rollback and erasure states without implying that package installation proves protection. Parties retains legal-policy and erasure UX; failures cannot offer plaintext fallback or misleading success.

## Cross-Story Dependencies

- Sequence: 8.1 → 8.2 → 8.3 → parallel 8.4/8.5 → 8.6, requiring both → 8.7 → 8.8 → 8.9 → 8.10 → 8.11. Each transition needs its own compatible predecessor evidence and explicit authority. Planning reconciliation paragraphs may describe historical status; reverify current story evidence before activation.
- Production qualification needs approved design and separate Security/Operations/IaC scope for resources, identities, networking, faults and cleanup. Coordinate operation dispositions with the Dapr-boundary inventory without creating a prerequisite cycle.
- External publication, deployment and Parties mutations require their separately bound owner authority. Preserve the retained local provider, historical readers, writer and rollback path.
- Rehearse rollback after real v2 writes while retaining keys, backend, readers and nondecreasing watermarks. Only 8.11 may close G5 after complete backend, release, consumer, rollback and role approvals. One authenticated human may hold all roles while the registry names one person, with separate decisions labelled `single-maintainer-attested`, never independent.

## Story 8.6 approved diagnostic supplement

The [exact selected-client-ID policy](spec-8-6-identity-diagnostics-disposition.md), SHA-256 `a0a746aeab4de17c1b1b95cfa37fbf42a119e29af0c7354fe174d09512b389d9`, is approved by the interactive user in this Codex task through the [retained content-bound choice](story-8-6-identity-diagnostics-owner-approval.json). This narrowly supplements the diagnostic constraint for vendor Azure-Identity events24/26 only. All other protected values, channels, original/frozen approvals, disabled switches and pins remain unchanged; live authority must explicitly bind the digest. The decision supplies no Azure mutation/live-run, production, Server, release or G5 authority.
