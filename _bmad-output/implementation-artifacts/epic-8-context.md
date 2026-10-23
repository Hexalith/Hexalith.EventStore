# Epic 8 Context: Domains Can Opt Into Portable Payload Protection - Post-MVP

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Give domain modules an optional EventStore-owned payload-protection engine with portable cryptographic formats, reusable policy and key-lifecycle mechanics, and an integration-proven production backend. Domains retain legal-policy ownership while sharing compatibility, custody, release, and rollback guarantees. This is an additive, disabled-by-default post-MVP capability; Phase 4 MVP readiness cannot depend on it. Parties migration remains gated by an approved G5 availability decision.

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

- Preserve existing provider-neutral protection metadata, readability outcomes, workflows, and redaction contracts. Public changes are additive; source, binary, wire, serialization, and package compatibility require representative consumer checks.
- Implement the approved `pdenc-v2` encoding and byte-stable authenticated data exactly. AES-256-GCM, canonical paths, UTF-8/framing, identity/key-version binding, bounds, and immutable golden/mutation vectors govern durable bytes. Reject ambiguity, tampering, nonce reuse, and cross-scope substitution without fallback.
- Classify every historical event and snapshot independently. Preserve `json+pdenc-v1`, `json-redacted`, legacy-unprotected, protected snapshots, and mixed histories. Unreadable protected content produces a bounded typed failure, preserves stored bytes, and stops dependent work without partial state, skipped input, or checkpoint advance.
- Deleted, missing, denied, unavailable, malformed, tampered, and opaque states remain distinct typed outcomes. Observe caller cancellation. Zero owned sensitive buffers before their owning scope ends, with tests inspecting disposed buffers, and invalidate caches on lifecycle changes.
- Domains select personal-data policy and own legal basis, retention, erasure orchestration, certificates/reports, and user copy. EventStore owns reusable fenced mechanics; successful key-record deletion or projection removal never proves complete legal, physical, broker, backup, or cryptographic erasure.
- Key/state operations must preserve tenant isolation, current lifecycle authority, irreversible epochs/watermarks, bounded leases/indexes, idempotency, and reconciliation of ambiguous mutations. No cache, retry, restore, or break-glass operation may bypass irreversible denial.
- No plaintext, protected bytes, keys, credentials, provider-private errors/identities, vault/key URIs, or raw payloads escape through diagnostics, exceptions, Admin, exports, or support output. Metrics use bounded low-cardinality tags without tenant identities.
- Real production-path evidence uses the owning Dapr state/actor boundary and inspects persisted results. Mocks, HTTP status alone, development stores, and secret-store configuration do not prove production cryptographic conformance.

## Technical Decisions

- The governing shared specification is approved at normative SHA-256 `de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`. Reverify exact approved bytes, relevant current-source compatibility, and predecessor/successor authority for each slice; historical status labels do not grant implementation or production authority.
- Dapr is the required infrastructure boundary. Qualify required key operations through a suitable cryptography API/component before selecting a provider SDK. Unsupported operations need a narrowly scoped accepted architecture exception; unknown suitability stays unresolved and failures never fall back to direct infrastructure. The detached Dapr amendment is draft and non-authorizing. Dapr Crypto Scheme v1 cannot replace `pdenc-v2` or its authenticated data; a secret store is not a key-operation component.
- Azure Key Vault is the planned production custody backend, using workload identity and exact-version operations with non-exportable operator-owned KEKs. Runtime identities cannot administer keys. Payload custody stays separate from OpenBao application-secret storage.
- The provider-neutral core and companion adapter remain non-packable through Server integration. Package/release integration atomically changes the manifest-governed inventory from 14 to 16; dependency versions remain centralized.
- AggregateActor remains the sole event append coordinator. Complete protection and durable key reservation precede event staging; trusted occurrence identity comes from persistence context. Reads, replay, publication, projections, Admin, snapshots, and restore share compatible typed behavior. Immutable history is not rewritten for rotation or rollback.
- Proof is tests plus CI. Record commands, pass/fail counts, canonical source SHA, and CI run link in the owning story/spec Verification section. Do not commit evidence packets/manifests or rebuild deleted evidence tooling; ignored evidence folders are local scratch and workflow artifacts retain run output. Existing assurance requirements still govern protected transitions.

## UX & Interaction Patterns

There is no new EventStore UI in this epic. Existing public/Admin surfaces expose only bounded readability outcomes and safe reasons. Operational copy distinguishes configured, conformant, experimental, available, unreadable, and rollback states. Parties retains legal-policy and erasure UX. No failure offers a plaintext fallback or misleading success claim.

## Cross-Story Dependencies

- Delivery order is 8.1 → 8.2 → 8.3 → parallel 8.4/8.5 → 8.6, requiring both predecessors → 8.7 → 8.8 → 8.9 → 8.10 → 8.11. Planning reconciliation paragraphs contain historical status; current story Verification sections and explicit authority govern activation.
- Production backend qualification also requires the affected architecture/security decisions and separately bound Operations/IaC permissions. Infrastructure inventory coordinates with the Dapr-boundary work without creating a prerequisite cycle.
- External package publication needs release-owner authority. Parties repository access/mutation needs separate maintainer authority; preserve its retained legacy provider and rollback path.
- Rollback proof follows real v2 writes and retains every required reader, key, backend, and nondecreasing watermark. Story 8.11 alone may close G5 after predecessor, backend, package, consumer, rollback, and role approvals pass. While one human holds all roles, record each role decision and label achieved assurance `single-maintainer-attested`, never independent; remaining assurance/seal rules still apply.
