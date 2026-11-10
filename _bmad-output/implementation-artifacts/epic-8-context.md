# Epic 8 Context: Domains Can Opt Into Portable Payload Protection - Post-MVP

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Give domain modules an optional, EventStore-owned payload-protection engine. It provides stable durable formats, shared policy and key-lifecycle mechanics, a qualified production backend, historical-read compatibility, a governed release and exercised rollback. Domains keep legal policy and operators keep key custody. The capability is post-MVP, additive and disabled by default, and it never blocks the Phase 4 MVP. The owner-approved 2026-11-08 course correction picked the on-premises production path: stock Dapr Cryptography (`crypto.dapr.kubernetes.secrets/v1`) with software KEKs held in versioned Kubernetes Secrets. The target runtime and CLI are 1.19.1 and the .NET SDK is 1.19.0. Story 8.12 must specify and qualify this profile before Story 8.7 wires the Server. The Azure Key Vault path (8.6) is kept but stays incomplete and deferred, and it is not on the selected path. Planning approval does not qualify the Alpha crypto component and does not authorize any runtime, infrastructure, release or G5 action.

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
- Story 8.12: Dapr Kubernetes Cryptography And Wrapped-Key Conformance

## Requirements & Constraints

- **Opt-in only.** No-op behavior stays the default until a domain registers explicitly. MVP code must never require engine packages, provider credentials or engine runtime services (FR37).
- **Byte-stable formats.** The `pdenc-v2` event and snapshot envelope stays fixed: AES-256-GCM, the 11-field authenticated data, canonical property paths and golden vectors. It must stay byte-identical under the Dapr successor. Ambiguity, tampering, nonce reuse and substitution across scopes all fail closed (NFR19).
- **Historical reads.** Each event and snapshot is classified on its own (`json+pdenc-v1`, `json-redacted`, legacy, mixed history). Unreadable content becomes a bounded typed outcome. It never becomes plaintext, a skipped input or an advanced checkpoint.
- **V1 wrapped-key records.** V1 records keep their exact meaning and their 512-byte bound. A Dapr envelope is never truncated or stripped to fit V1.
- **Failure outcomes.** Deleted, missing, denied, unavailable, malformed, tampered and opaque are separate typed outcomes. Stock Dapr errors map to conservative outcomes: Unavailable, ConsistencyMismatch or Ambiguous. Error text, a generic status, a missing Secret or a cache miss never proves deletion or absence. If a crypto result is uncertain, it stays Ambiguous or requires an operator. Exception (8.12 amendment revision 2): the Dapr Kubernetes backend's Reconcile returns Missing without transport, because Encrypt and Decrypt create no provider-side state, so the exact mutation can never exist natively. Durable state alone then decides recovery. Wrap, unwrap and transport failures still never yield Missing.
- **Bounded work.** Caller cancellation is honored. One combined budget covers all client, sidecar and component retries. gRPC inputs and outputs are bounded. The approved 8.12 amendment freezes the decoded `WrappedDekV2` envelope at 2,048 bytes; real envelopes are 666 bytes. `WrappedDekV1` keeps 512. The 65,536-byte state value limit and the existing transaction budgets still apply. The planning text's "16 KiB" was the OpenBao wire bound.
- **Buffers and caches.** Buffers that the application owns are zeroed before they leave scope. A test proves this by inspecting the buffer after disposal. Caches are invalidated on lifecycle changes. Copies held in sidecar memory, Kubernetes/etcd, backups and restores are disclosed as limitations, never claimed as all-copy erasure.
- **No leaks.** Plaintext, ciphertext, key material, key paths, credentials and backend errors never reach logs, traces, metrics, exceptions, ProblemDetails, Admin, exports or support output. Tenant IDs are never metric labels. Diagnostic sentinels must cover every channel.
- **Compatibility.** FR37 read formats and the new key-record reader capability are protected compatibility surfaces. Additive changes need baseline evidence (NFR12).
- **Evidence.** Tests assert the persisted key, lifecycle, fence and index end-state, read through the owning Dapr state or actor boundary (NFR16). Direct backend reads are diagnostics only. Localstorage, mocks, HTTP probes and configuration shape cannot stand in for proof. Selected live cases run with zero skips. Proof means repository tests plus CI. The owning spec records the commands run, pass/fail counts, the SHA and the CI run link. No committed evidence packets.
- **Kubernetes Secrets exception.** Kubernetes Secrets may hold dedicated, versioned, operator-owned payload KEKs, consumed only by the scoped Dapr crypto component. Application code never retrieves private KEKs. Ordinary operational and application secrets stay in OpenBao through the Dapr Secrets API (NFR17).

## Technical Decisions

- **Ownership.** EventStore owns the format, mechanics, provider registry, conformance tests and release proof. `IPersonalDataPolicy` and `IErasureStateProvider` select values only. Parties owns legal basis, retention, erasure orchestration, certificates, reports and UX copy. Shared mechanics preserve tenant isolation, monotonic epochs and watermarks, current authorization, bounded leases and indexes, and reconciliation of ambiguous mutations. Cache, retries, restore and break-glass cannot override irreversible denial.
- **Dapr boundary.**
  - Use Dapr wherever it supports the operation. Unknown suitability requires qualification.
  - After a Dapr failure, never fall back to direct infrastructure. Any exception needs an architecture-owner decision that names its exact paths.
  - Application code has no provider SDK, provider HTTP call, Kubernetes client or exported KEK.
- **Crypto mechanism.**
  - Stock 1.19.1 returns 501 for subtle get-key/wrap/unwrap, so never depend on them. Never use a custom runtime built with the `subtlecrypto` tag.
  - Use the supported streaming Encrypt/Decrypt over the .NET gRPC client, only to wrap the EventStore-generated 32-byte DEK. Dapr Crypto Scheme v1 lives only in the opaque wrapped-DEK field and never replaces `pdenc-v2` or its AAD.
  - Use the standard component-inclusive release artifact. The Alpha support posture must be accepted explicitly.
  - The planning-time HTTP/localstorage probe was diagnostic only. The 8.12 kind lane measured SDK-written envelopes at a fixed 666 bytes, with no embedded key name.
- **Successor key record.**
  - A new closed schema version and algorithm identifier.
  - Immutable key-version addresses, for example `namespace/secretName/dataKey`, with one Secret per KEK version. Version bytes are never replaced, and there is no mutable `latest` alias.
  - Names supplied in ciphertext never select a key; decrypt uses the recorded trusted identity.
  - Platform publishes the current write version as a value-free inventory and generation, and changes it through a controlled rollout.
- **Authority.**
  - The new authority profile binds to the approved successor supplement and to the key-record reader capability. A process that reads v2 events but not the new key schema cannot join that generation.
  - Roots on the new schema reject older readers and writers. Existing roots are never silently restamped or converted.
  - Earlier approvals keep their original scope only: the base spec, the OpenBao supplement and the Azure exception. Old digest fields are never updated to make changed bytes look approved.
- **Persistence.**
  - `AggregateActor` stays the sole append coordinator, behind admission and the current fence. Protection and the durable key reservation happen before staging.
  - Completion is recorded only after durable persistence. Crypto and state share no distributed transaction, so reconcile from exact durable evidence.
  - Trusted persistence context supplies occurrence identity.
- **Spec first.** An approved, versioned spec and vectors come before runtime work. Approving a spec authorizes the next slice; it does not prove delivery.
  - Story 8.12 begins by drafting and approving `_bmad-output/implementation-artifacts/spec-8-12-dapr-kubernetes-cryptography.md` and its separately identified normative amendment.
  - It then extends the existing core, state codecs and coordinators, `PayloadProtection.Tests` and `PayloadProtection.LiveSidecar.Tests`.
  - If the real Kubernetes/gRPC lane fails, 8.12 stays blocked and the design goes back to review. Never switch to another runtime, a provider fallback, another format or weaker lifecycle rules.
- **Packaging.** The engine and the Azure companion stay non-packable until 8.8. Story 8.8 changes the inventory from 14 to 15 packages in one atomic step, adding only the engine with its existing Dapr dependency. No third adapter package, custody service or evidence subsystem. Hexalith.Builds owns version pins. One type per C# file.
- **Assurance.** Solo approvals are labelled `single-maintainer-attested`, never `independent`. For crypto evidence, "independent" means re-derived by separate toolchains.

## UX & Interaction Patterns

No EventStore UI is added. Public, Admin and operator surfaces show only bounded readability outcomes and safe reason codes. Wording keeps these states distinct: disabled, configured, conformant, experimental, available/G5, unreadable, rollback and erasure. Installing the component never implies protection. Parties keeps legal-policy and user-facing copy.

## Cross-Story Dependencies

- **Execution order:** 8.1 → 8.2 → 8.3 → 8.4 ∥ 8.5 → **8.12 → 8.7** → 8.8 → 8.9 → 8.10 → 8.11. Every step needs compatible predecessor evidence and explicit authorization.
  - The tracker shows 8.1–8.5 done. 8.7 is in progress with its neutral-binding first phase kept.
  - 8.6 is outside the selected path, but Epic 8 stays open while 8.6 is incomplete.
- **8.12 needs:**
  - compatible 8.1–8.5 evidence;
  - the accepted Kubernetes-KEK policy;
  - an approved successor supplement;
  - the qualified 1.19.1 runtime input from Story 3.21/Builds.
- **8.7 also needs** the index-capacity and erasure-memory work, plus shared-state, fleet, fence and restore admission.
- **Platform** works in its own repository. It provisions the versioned Secrets and owns RBAC and component scopes, sidecar API authentication, encryption at rest, rotation and retirement, backup and restore, and the real Kubernetes rehearsal. EventStore owns runtime semantics and tests. Platform results alone never establish EventStore conformance.
- **Epic 3 work:**
  - Story 3.17 maps the crypto operations into the Dapr infrastructure inventory.
  - Story 3.18 assesses the new key-record reader capability.
  - Sealed CI lanes keep their original runtime until a planned reseal. A new lane may qualify the latest runtime on its own.
- **8.8–8.11:** 8.10 rehearses rollback after real v2 writes while keeping keys, backend, readers and nondecreasing watermarks. Its rollback target must read every key-record schema that was written. Parties changes need the Parties maintainer's separate authority, and publication needs the release owner's. Only 8.11 may close G5.

## Story 8.6 retained pointers

- **Scope.** Azure is outside the MVP; do not request Azure provisioning or authority/custody documents as MVP prerequisites (owner clarification, 2026-11-06).
- **Diagnostic supplement.** The approved [selected-client-ID diagnostic policy](spec-8-6-identity-diagnostics-disposition.md), SHA-256 `a0a746aeab4de17c1b1b95cfa37fbf42a119e29af0c7354fe174d09512b389d9`, permits one exception. The exact configured `ManagedIdentityClientId` may appear only in the pinned vendor's Azure-Identity `LogMsalAlways` (event 24) and `ManagedIdentityCredentialSelected` (event 26) payloads. It must not appear in any other event, channel, log, exception, activity or metric, and Azure-Identity/MSAL events must never be broadly exempted. Live authority must bind that digest explicitly. The decision supplies no Azure mutation/live-run, production, Server, release or G5 authority.
