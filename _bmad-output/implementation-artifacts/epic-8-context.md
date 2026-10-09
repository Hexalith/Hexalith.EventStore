# Epic 8 Context: Domains Can Opt Into Portable Payload Protection - Post-MVP

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Give domain modules an optional, EventStore-owned, provider-neutral payload-protection engine. It provides the versioned `pdenc-v2` format with byte-stable authenticated data, reusable policy and key-lifecycle mechanics, and one integration-proven production backend, so domains (first consumer: Parties) stop building incompatible envelope encryption and key custody. The capability is additive, opt-in, and disabled by default: the no-op provider stays the default until explicitly registered. It is post-MVP and does not block Phase 4 MVP. However, Parties' migration cannot proceed until Story 8.11 closes the G5 gate as `available`. G5 is currently `needs-additive-api`.

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

- Build on the existing `IEventPayloadProtectionService` and the provider-neutral metadata, outcome, workflow, and redaction contracts. Contracts stays the single provider-neutral authority, and every change is additive for current providers and consumers.
- `pdenc-v2` is implemented exactly as frozen: AES-256-GCM, the frozen nonce/tag representation, an 11-field byte-stable AAD, the canonical property-path grammar, and identity and key-version binding with bounds. Golden, negative, and mutation vectors define the durable bytes. Each of the following fails closed and never creates another format:
  ambiguity, alternate or culture-dependent serialization, nonce reuse, tenant/domain/aggregate/property substitution, tampering, and malformed or oversized data.
- Reads stay compatible with `json+pdenc-v1`, `json-redacted`, legacy-unprotected data, existing protection metadata, protected snapshots, and mixed history, with each record classified independently. Unreadable protected content never becomes plaintext, redacted success, absent data, skipped projection input, an advanced checkpoint, or downgraded output. It stops dependent work with a typed outcome.
- Deleted, missing, denied, unavailable, throttled, malformed, tampered, and opaque states are all bounded typed outcomes.
- Key material is zeroed before its buffer leaves the scope that decrypted or derived it. A test must inspect the buffer after disposal to prove this.
- Caches are invalidated on lifecycle changes.
- No-op, LocalDevelopment, in-memory, mock, and Dapr secret-store implementations are never production proof.
- Nothing leaks: plaintext, ciphertext, key material, credentials, provider-private errors or identities, vault or key URIs, and payloads never reach logs, traces, metrics, exceptions, ProblemDetails, Admin, evidence, exports, or support bundles. Tenant IDs are never metric labels.
- The domain seams `PersonalDataAttribute`, `IPersonalDataPolicy`, and `IErasureStateProvider` let a domain select policy values only. EventStore never decides legal basis, retention, erasure orchestration, certificate or report meaning, Art.20/Art.30 behavior, or user-facing copy.
- Proof must be persisted production-path state read through Dapr boundaries, not mocks or HTTP status codes. Rollout, historical reads, downgrade, and rollback after real `pdenc-v2` writes must all be integration-tested.

## Technical Decisions

- **Content-bound authority:** The approved spec is `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`, at normative SHA-256 `de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`. Approval `AR-20260913-01` covers that spec, and approval `AR-20260914-01` approves Story 8.2 and authorizes Story 8.3. Every story must reverify that exact digest and the predecessor evidence packet that explicitly authorizes it. Story or ledger status, epic approval, and boolean flags never authorize work. A normative gap or an incompatible source change goes back to the spec and blocks the work.
- **Packages:** `Hexalith.EventStore.PayloadProtection` is the provider-neutral core. A companion Azure Key Vault production adapter will join it. Dependencies point inward to Contracts, and both packages stay `IsPackable=false` through Story 8.7. The release inventory stays at 14 packages until Story 8.8 atomically moves it to 16 through `tools/release-packages.json`. Dependency versions stay centralized.
- **Dapr boundary:**
  - Qualify key operations through a Dapr cryptography component (the `crypto.azure.keyvault` candidate) before selecting any provider SDK.
  - An operation Dapr cannot perform requires a narrowly scoped, owner-approved architecture exception.
  - Never fall back silently to direct infrastructure.
  - Dapr Crypto Scheme v1 never replaces `pdenc-v2` or its AAD, and a Dapr secret store is not a key-operation component.
  - The detached 2026-10-05 Dapr amendment is a draft, is unapproved, and conveys no authority.
- **Custody:** Azure Key Vault is the backend, using Premium/RSA-HSM, workload identity, and exact-version operations. Runtime identities cannot administer or export KEKs. Operators own provisioning and IaC. Payload KEK custody is separate from OpenBao application secrets.
- **Server integration:**
  - `AggregateActor` remains the sole append coordinator.
  - All event protection completes before staging, and occurrence identity comes from the trusted persistence context.
  - Rehydrate, publish, projection, replay, Streams, Admin, snapshot, and restore paths share one typed compatibility router.
  - Lifecycle mechanics use exact tenant-scoped Dapr state contracts. No cache, retry, or operator action may bypass irreversible state.
- **Rollback:** Immutable history is read in place. Format and lifecycle watermarks never decrease. Rollback keeps every required v2 reader, the backend, and the keys.

## UX & Interaction Patterns

There is no EventStore UI. Public and Admin surfaces return only the existing bounded protected-outcome readability decisions with safe reason classes. They never show protected bytes, plaintext on failure, key or provider details, or a blind retry. Docs and status text must clearly separate disabled, configured, conformant, experimental, available/G5, unreadable, rollback, and erasure states. Parties owns all legal-policy UX and copy.

## Cross-Story Dependencies

- The stories run strictly in sequence: 8.1 → 8.2 → 8.3 → (8.4 and 8.5 in parallel) → 8.6, which needs both → 8.7 → 8.8 → 8.9 → 8.10 → 8.11. Status as of 2026-10-05: 8.1 and 8.2 are done, 8.3 is in progress, and 8.4–8.11 are backlog and blocked.
- Story 8.6 also needs approval of the Dapr amendment and named Security, Operations, and IaC authority for Azure resources. It coordinates its Dapr-boundary inventory with Story 3.17.
- Story 8.8 also needs separate release-owner authority for publication, signing, and tags.
- Story 8.9: Parties is not a root-declared submodule. Any Parties read or change needs the Parties maintainer to authorize the exact repository, SHA, and scope. EventStore takes no source dependency on Parties, and Parties keeps its `json+pdenc-v1` path.
- Story 8.11 writes `_bmad-output/implementation-artifacts/8-11-g5-evidence-and-approval-closure.md`. While the owner-role registry names one human, that person may hold every role, but each role decision is recorded separately. The verdict is labelled `single-maintainer-attested`, never `independent`.
