---
baseline_commit: 76f122332216cc5d9b44a421bdbed3ab20d35f5e
created: 2026-07-16
story_id: "8.1"
story_key: 8-1-shared-payload-protection-security-spec-and-adr
epic: "Epic 8 - Shared Payload Protection"
requirements:
  - FR37
  - NFR19
story_type: security-architecture-specification-gate
status: in-progress
creation_note: Ultimate context engine analysis completed - comprehensive developer guide created
source_files:
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/ux.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - docs/guides/payload-protection-and-crypto-shredding.md
  - src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs
  - src/Hexalith.EventStore.Contracts/Security/EventStorePayloadProtectionMetadata.cs
  - src/Hexalith.EventStore.Contracts/Security/EventStorePayloadProtectionMetadataCarrier.cs
  - src/Hexalith.EventStore.Contracts/Security/UnreadableProtectedDataReason.cs
  - src/Hexalith.EventStore.Contracts/Security/ProtectedDataReadabilityDecision.cs
  - src/Hexalith.EventStore.Contracts/Security/ProtectedDataReadabilityDecisionFactory.cs
  - src/Hexalith.EventStore.Contracts/Security/CryptoShreddingWorkflowIdentity.cs
  - src/Hexalith.EventStore.Contracts/Security/CryptoShreddingWorkflowTransitions.cs
  - src/Hexalith.EventStore.Contracts/Security/RestoredBackupAdmissionTransitions.cs
  - src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs
  - src/Hexalith.EventStore.Server/Diagnostics/ProtectedDataDiagnosticRedactor.cs
  - src/Hexalith.EventStore.Server/Events/EventPersister.cs
  - src/Hexalith.EventStore.Server/Events/EventPublisher.cs
  - src/Hexalith.EventStore.Server/Events/SnapshotManager.cs
  - src/Hexalith.EventStore.Testing/Security/ProtectedDataLeakSentinel.cs
  - tools/release-packages.json
---

# Story 8.1: Shared Payload-Protection Security Spec And ADR

Status: in-progress

<!-- Note: Validation is mandatory for this security gate. Story 8.2 remains blocked until the
     exact specification is complete, independently reviewed, and explicitly authorized. -->

## Story

As a platform security owner,
I want the shared payload-protection ownership and durable security contract approved before implementation,
so that the engine cannot make story-local choices that strand persisted history or weaken key custody.

## Story Context

This is a **security/architecture specification gate**, not a runtime implementation story. It creates one authoritative decision artifact at `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`. That file must carry the specification, ADR decisions, threat model, deterministic vectors, migration/rollback contract, approval evidence, and the explicit decision whether Story 8.2 is authorized.

Epic 8 is committed post-MVP work. It does not block the Phase 4 MVP, but Story 8.2 blocks Parties Story 8.7. Story 1.20 is projection/query parity for Parties Story 8.6 only; it neither blocks nor authorizes payload-protection G5. Historical Stories 22.7a-d delivered provider-neutral hooks, metadata, typed unreadable outcomes, workflow/restore contracts, and no-leak behavior—not an encryption engine, `pdenc-v2`, policy seams, reusable key mechanics, or a production backend.

AD-23 has already settled the ownership boundary:

- EventStore owns the optional engine, durable format/AAD authority, backward readers, reusable key mechanics, backend abstraction/conformance, goldens, release provenance, and G5 proof.
- Providers/operators own production root-key and KEK custody, credentials, KMS/HSM/secret-store operation, availability, and environment policy.
- Parties retains legal/retention policy, erasure orchestration semantics, certificates/reports, Art.20/Art.30 behavior, and UX/copy unless a later approved ADR moves a named concern.

The July 16 planning approval did **not** approve this security specification, authorize Story 8.2, change the current 14-package release inventory, provision a provider, modify Parties, or permit deletion of its rollback path.

## Acceptance Criteria

### AC1 — Ownership, package, and dependency boundaries are approved

**Given** the EventStore-owned optional engine is proposed

**When** the security specification is approved

**Then** it fixes package and dependency boundaries, backend ownership, operator custody, Parties-retained policy, and whether an ADR-selected adapter requires a companion package

**And** it does not treat an interface-only or development backend as production.

### AC2 — The durable `pdenc-v2` wire contract is test-vector ready

**Given** `pdenc-v2` will persist durable data

**When** the wire contract is frozen

**Then** the canonical envelope, AES-GCM parameters, nonce/tag representation, algorithm identifiers, byte-stable AAD encoding, canonical property-path encoding, and format/version rules are test-vector ready

**And** AAD binds tenant, domain, aggregate, event or snapshot type, property path, key version, and format, or records the explicitly approved equivalent.

### AC3 — Historical compatibility, rollout, downgrade, and rollback are exact

**Given** historical data must remain usable

**When** compatibility is specified

**Then** `json+pdenc-v1`, `json-redacted`, legacy-unprotected, current Story 22.7 metadata, and snapshot reads have explicit routing and typed failure behavior

**And** rollout, mixed history, downgrade, and rollback-after-v2-write policies are defined.

### AC4 — Policy and key-lifecycle contracts are frozen

**Given** policy and key lifecycle are shared

**When** contracts are frozen

**Then** `IPersonalDataPolicy`, `IErasureStateProvider`, policy discovery including `PersonalDataAttribute` or an approved equivalent, key paths, state keys, actor/reminder names, metric names, audit fields, and versioning rules are exact

**And** storage, wrapping, rotation, retry, circuit breaking, cache invalidation, and key zeroing responsibilities are assigned.

### AC5 — A real production backend and its restrictions are selected

**Given** a production backend is required

**When** backend selection and restrictions are approved

**Then** at least one non-development adapter, its integration environment, credentials/custody boundary, failure taxonomy, and conformance evidence are named

**And** LocalDev/in-memory startup restrictions outside Development are explicit.

### AC6 — Threat model, misuse cases, vectors, and no-leak boundaries are complete

**Given** the security review evaluates misuse and failure

**When** the threat model and test vectors are inspected

**Then** cross-tenant/cross-aggregate substitution, nonce reuse, path ambiguity, metadata tampering, malformed/oversized envelopes, key deletion, provider denial/unavailability, cache staleness, downgrade, and rollback are covered

**And** no-leak boundaries include logs, traces, metrics, exceptions, ProblemDetails, evidence, exports, processing records, certificates, and reports.

### AC7 — The exact approved artifact gates Story 8.2

**Given** Story 8.1 completes

**When** its output is recorded

**Then** the exact path is `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`

**And** it records approvers, date, accepted scope, rejected alternatives, open decisions, threat model, test vectors, migration posture, and explicit authorization for Story 8.2 to start.

## Tasks / Subtasks

- [x] **Task 1 — Establish the authoritative baseline and traceability (AC1-AC7).**
  - [x] Create `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`; keep the ADR and specification in this one required authority rather than inventing a second mandatory output path.
  - [x] Crosswalk FR37, NFR19, AD-23, every AC, and the July 16 ownership decision to named specification sections and verification evidence.
  - [x] Inventory the existing Story 22.7 contracts and all protect/unprotect persist, publish, rehydrate, projection, stream-read, admin, snapshot, restore, audit, and no-leak paths before proposing additive APIs.
  - [x] Obtain and record the exact Parties repository SHA/artifact identities used to specify `json+pdenc-v1`, `json-redacted`, Parties-local policy/provider behavior, `IPersonalDataPolicy`, `IErasureStateProvider`, and `PersonalDataAttribute` or equivalents. `Hexalith.Parties` is not a root-declared submodule here; absent exact consumer evidence is a blocking open decision, not permission to infer its contract.
  - [x] Verify time-sensitive cryptography and selected-provider facts against official sources; record each source URL, retrieval date, applicable revision/API/SDK version, and supersession status. Reverification before Story 8.2 is a mandatory preflight.

- [x] **Task 2 — Freeze ownership and package architecture (AC1).**
  - [x] Record a responsibility matrix for EventStore, the production provider/operator, Parties, Security, Operations, Release, and Test ownership.
  - [x] Freeze `Hexalith.EventStore.PayloadProtection` as the optional engine boundary, its dependency direction, public-contract ownership, DI/options surface, and explicit opt-in behavior.
  - [x] Decide and justify whether the selected production adapter belongs in the engine package or an exact companion package; record package IDs, target framework, dependency constraints, and future manifest count.
  - [x] Preserve the current no-op provider as the default. Do not let package installation or host registration silently enable encryption or make a development provider production-valid.
  - [x] Freeze a mode/environment matrix that separately covers disabled/no-op, enabled/development provider, enabled/missing or invalid backend, and enabled/approved production adapter behavior.

- [x] **Task 3 — Freeze the canonical `pdenc-v2` wire contract (AC2).**
  - [x] Specify the complete envelope grammar: magic/discriminator, version, algorithm IDs, field order, field types, length widths/endianness, byte/string encoding, null/empty rules, nonce, tag, ciphertext, wrapped-DEK/key references, maximums, unknown-field behavior, and canonical textual encoding where applicable.
  - [x] Select and justify AES key size, the fixed AES-GCM nonce and tag sizes, the exact algorithm identifier, platform support/startup checks, and authenticated-decryption failure mapping.
  - [x] Select one nonce construction; define uniqueness across concurrent writers, processes, restarts, crashes, cloned instances, rotations, and per-key invocation budget. Encryption must fail before use when uniqueness or budget cannot be proved.
  - [x] Freeze AAD as bytes: schema/version, field identifiers/order, length encoding, UTF-8 and Unicode policy, case sensitivity, null/empty distinction, size limits, and the verified source of every bound value. Delimiter concatenation is forbidden.
  - [x] Freeze canonical property-path semantics, escaping, object/array handling, duplicate-name behavior, unresolved paths, Unicode normalization posture, and ambiguity rejection.
  - [x] Resolve whether `keyVersion` means a stable DEK identity/version or a KEK version. If KEK version is inner AAD, rewrap-only rotation is impossible without invalidating the tag; the chosen rewrap/re-encryption rule must be explicit.
  - [x] Define a constructive allowlist for durable metadata fields and their exact semantics; reject opaque extensions and include negative vectors for innocuously named, encoded, or obfuscated secret/provider data. Existing name/substring checks are defense in depth, not proof of safe content.
  - [x] Provide byte-level golden vectors with fixed test-only keys/nonces, exact AAD bytes/hex, plaintext, ciphertext, tag, envelope, machine-readable bytes/hashes, and independently reproducible verification instructions using at least two implementations/toolchains with exact versions and commands.

- [x] **Task 4 — Freeze compatibility and migration semantics (AC3).**
  - [x] Define a precedence/routing table across `json`, `json+pdenc-v1`, `json-redacted`, legacy missing metadata, current metadata v1, protected snapshots, `pdenc-v2`, mixed-version streams, unknown versions, malformed envelopes, and bytes/metadata disagreement.
  - [x] Map every unreadable condition to bounded existing typed outcomes or an explicitly versioned additive taxonomy; never parse provider exception text and never infer plaintext/unprotected state.
  - [x] Define dual-read/single-write rollout, when v2 writes begin, historical-read policy, optional migration/re-encryption posture, and behavior during partial rollout.
  - [x] Define downgrade compatibility and an exercised rollback after real v2 writes. Switching DI before any v2 write is not rollback evidence.
  - [x] Preserve restored-backup admission, irreversible workflow watermarks, quarantine/operator-decision behavior, and the rule that protected unreadable snapshots are retained rather than deleted.

- [x] **Task 5 — Freeze policy discovery and key lifecycle (AC4).**
  - [x] Specify exact namespaces, assemblies, signatures, result types, versioning, and discovery/precedence/error rules for `IPersonalDataPolicy`, `IErasureStateProvider`, and `PersonalDataAttribute` or the approved equivalent.
  - [x] Define protection granularity and deterministic selection for event properties and snapshot values without allowing domain-specific policy into EventStore.
  - [x] Freeze key hierarchy and identities (DEK/KEK/root key), key paths, state keys, key-reference/fingerprint rules, actor/reminder names if used, operation/idempotency IDs, audit fields, activity/meter/metric names, and low-cardinality tags.
  - [x] Keep durable opaque key IDs, provider version IDs, redacted display fingerprints, and the existing 16-character `KeyAliasFingerprint` distinct. The fingerprint is audit/idempotency correlation only and must never select, authorize, wrap, or locate a cryptographic key; define collision handling and migration semantics.
  - [x] Assign creation, generation, storage, wrapping/unwrapping, rotation/rewrap, invalidation/deletion, restoration, retry/reconciliation, audit, retention, and break-glass responsibility.
  - [x] Define bounded cache scope/TTL/size, lifecycle epoch/version, invalidation propagation, stale-entry behavior, concurrency, cancellation/failure cleanup, and proof that accepted deletion/denial cannot be bypassed by cache staleness.
  - [x] Define owned-buffer zeroing with mutable buffers and `finally` paths for success, failure, cancellation, rotation, and erasure. State its limit honestly: zeroing owned spans is not proof that the GC, provider, or process retains no copy.
  - [x] Define finite retry/time budgets, backoff/jitter, `Retry-After`, circuit-breaker behavior, and reconciliation for ambiguous lifecycle mutations. Never retry all failures or fall back to no-op/plaintext.

- [x] **Task 6 — Select and constrain a production backend (AC5).**
  - [x] Compare credible production adapters against custody, dependency, cryptographic-operation, outage, deletion/retention, local integration, release, and conformance needs; select one explicitly.
  - [x] Name the real resource/service type—not a generic brand—the key type/size, wrapping algorithm, API/SDK version policy, identity/RBAC permissions, network boundary, credentials/custody split, integration environment, and required failure injection.
  - [x] If Azure is selected, distinguish ordinary Key Vault from Managed HSM: they have different symmetric-key/wrapping capabilities. Reconcile soft-delete/purge-protection retention with claimed crypto-erasure and rollback semantics.
  - [x] Map unauthenticated, denied, missing/deleted, throttled, timeout/network/5xx, cancellation, and ambiguous mutation outcomes to exact retry/permanence behavior.
  - [x] Require startup validation based on `IHostEnvironment.IsDevelopment()`: disabled engine + no-op is allowed in every environment but is never production proof; enabled LocalDev/in-memory is allowed only in `Development`; enabled missing/invalid backend fails startup; enabled approved adapter validates production options/custody. Missing environment defaults to Production. Staging, Test, Production, and custom environments reject enabled development providers.
  - [x] Define real-service conformance evidence. An interface, mock, custom-provider sample, LocalDev/in-memory provider, or DAPR configuration shape cannot satisfy production proof.

- [x] **Task 7 — Complete the threat model, no-leak matrix, and vectors (AC6).**
  - [x] Model assets, actors, trust boundaries, attacker capabilities, abuse cases, mitigations, residual risks, owners, and verification for every AC6 threat.
  - [x] Add positive, negative, mutation, boundary, concurrency, crash/restart, lifecycle, provider-failure, cache, startup, mixed-history, downgrade, and post-v2-write rollback vectors described in Dev Notes.
  - [x] Require authenticated decryption to return plaintext or one bounded failure; no plaintext may be parsed, used, published, cached, or surfaced before authentication succeeds.
  - [x] Use constructive safe-output allowlists for logs, traces, metrics, exceptions, ProblemDetails, evidence, exports, Art.20/Art.30 processing records, certificates, and reports. Never serialize broadly and scrub afterward.
  - [x] Extend—not replace—the existing `ProtectedDataLeakSentinel`, metadata/outcome, readability-decision, and no-leak verification model in the future implementation plan.

- [x] **Task 8 — Define Story 8.2 implementation and verification handoff (AC1-AC7).**
  - [x] Map each frozen decision to likely NEW/UPDATE source, test, package, topology, documentation, release, and consumer-proof artifacts without editing them in Story 8.1.
  - [x] Define owner goldens, real-backend integration, persisted-state evidence, package-only consumer validation, SBOM/provenance, Parties dual-provider tests, and post-v2-write rollback gates.
  - [x] Keep the future release inventory at 14 until Story 8.2 creates approved packable projects; require manifest, inventory guidance/tests, metadata, SBOM/provenance, and package-only consumer checks to change atomically then.
  - [x] Require Story 8.2 code tasks to cite the exact approved spec sections they implement.

- [ ] **Task 9 — Record ADR disposition and approvals (AC7).**
  - [x] Record decision status, accepted scope, non-goals, rejected alternatives, limitations, migration posture, and every open decision with owner and blocking/non-blocking classification.
  - [ ] Obtain named, dated approval from the Architect and Security Reviewer; obtain EventStore, Release, Operations, and Parties-maintainer approval for their boundaries. Do not invent names, dates, evidence, or approval.
  - [ ] Bind every approval and authorization to the approved spec SHA-256, EventStore source SHA, authoritative-source revisions, and approval timestamp. Any spec-content or incorporated-fixture change invalidates all approvals and resets authorization to `not authorized` until re-review.
  - [x] Authorize Story 8.2 only when all durable-format, compatibility, backend, threat/vector, rollback, custody, and package decisions are closed and all mandatory approvals are recorded. Otherwise record `not authorized`, leave Story 8.1 non-`done`, and keep Story 8.2 blocked.

- [ ] **Task 10 — Validate scope integrity and the completed gate (AC1-AC7).**
  - [ ] Run an independent structure/traceability review, an independent security/threat-model review, independent vector reproduction, and approval-evidence validation; record reviewer identity, date, method, findings, and disposition.
  - [ ] Require Story 8.2 preflight to recompute and match the approved spec/fixture digests and source identities before implementation starts; mismatch means `not authorized`.
  - [ ] Confirm Story 8.1 changed no runtime source, solution/project/package file, package manifest, DAPR/AppHost/deployment topology, provider resource, credential, Parties source, or persisted data.
  - [ ] Confirm no engine/G5 availability claim was issued and the Parties local provider/DI rollback path remains retained.

## Dev Notes

### Top Guardrails

- **Specification only.** Do not implement `Hexalith.EventStore.PayloadProtection`, add an adapter, modify runtime code, provision a backend, change `Hexalith.EventStore.slnx`, add package versions, alter `tools/release-packages.json`, update DAPR/AppHost topology, or touch persisted data in this story.
- **One contract authority.** The required spec file carries the ADR decisions. Do not create a second competing wire/security authority or move stable contracts into Parties/a new shared module without an approved replacement ADR.
- **No fake production proof.** Existing hooks, interface-only code, mocks, the custom-provider guide example, LocalDev, and in-memory implementations are prerequisites/test aids only.
- **No fabricated approval.** A complete draft without named approvals is not an approved gate. Open material decisions mean Story 8.2 remains `not authorized`.
- **Content-bound approval.** Approval follows the recorded SHA-256 and exact source identities, not merely the artifact path. Any approved-content change requires a new review and approval record.
- **No silent downgrade.** Missing, deleted, denied, unavailable, malformed, tampered, unknown, or opaque states remain distinct bounded outcomes. They never become plaintext or no-op by inference.
- **Preserve opt-in behavior.** `NoOpEventPayloadProtectionService` remains the default unless explicit engine registration and valid environment/options select the shared engine.
- **Preserve domain boundaries.** Parties policy, erasure semantics, reports/certificates, and UX stay in Parties. EventStore owns reusable infrastructure only.
- **Exact consumer provenance.** Because Parties is not a root-declared local submodule, the spec must cite the exact external Parties source SHA/artifacts it used. Do not initialize an undeclared or nested submodule to fill the gap.

### Mandatory Specification Structure

The exact output should be scannable and include at least:

1. Document control, decision status, baseline identities, spec/fixture SHA-256 values, and content-bound approval/authorization table.
2. FR37/NFR19/AD-23/AC traceability matrix.
3. Scope, non-goals, ownership, trust boundaries, and dependency/package diagram.
4. Existing Story 22.7 baseline and preservation constraints.
5. Canonical `pdenc-v2` envelope grammar and bounded parser rules.
6. Canonical AAD schema and property-path grammar with exact byte encodings.
7. Algorithm/key/nonce/tag/wrap identifiers, limits, budgets, and platform checks.
8. Policy/discovery and erasure-state contracts with exact public API inventory.
9. Key hierarchy, storage/naming, lifecycle, cache, resilience, audit, and zeroing rules.
10. Selected production backend, custody/credentials/environment boundary, failure taxonomy, and conformance plan.
11. Compatibility/read-routing matrix and typed outcomes.
12. Rollout, mixed history, historical reads, downgrade, migration, and rollback after v2 writes.
13. Threat model, no-leak matrix, positive/negative vectors, and expected evidence.
14. Story 8.2 source/test/package/release/Parties handoff.
15. Accepted/rejected alternatives, limitations, open decisions, named approvals, date, and explicit `authorized`/`not authorized` decision.

### Existing Implementation Baseline — Reuse And Preserve

| Existing seam | Current state | Spec requirement |
| --- | --- | --- |
| `IEventPayloadProtectionService` | Provider-neutral event/snapshot protect/unprotect methods; additive default interface methods preserve older providers; cancellation propagates and otherwise-unclassified failures become `ProviderUnavailable`. The protect call has no property path, key version, sequence, message ID, or global position. | Decide whether new context is supplied through additive default methods/contracts or derived inside the engine. Do not break existing providers. |
| `EventStorePayloadProtectionMetadata` + carrier | Metadata v1 declares non-secret reference-only intent. The bounded `eventstore.protection` carrier applies printable/length/name/substring heuristics and maps detected malformed/unknown/forbidden data to opaque; missing metadata maps to legacy. Those heuristics are defense in depth, not proof that innocuously named or encoded secret/provider content cannot pass. | Prefer nonce/tag/cipher inside the bounded `pdenc-v2` payload envelope. Freeze a constructive allowlist with exact field semantics and no opaque extensions; add encoded/obfuscated-secret negatives. Any metadata change needs an intentional version/read migration. Preserve fail-closed parsing. |
| `PayloadProtectionState`, typed outcomes, unreadable taxonomy | Stable unprotected/protected/opaque states and separate missing, invalidated/deleted, unavailable, denied, consistency, malformed, unknown-version, opaque, and bytes/metadata mismatch reasons. | Extend additively or version explicitly. Map provider failures without exception-text parsing. |
| Readability decisions/workflows | Canonical EventStore-owned decisions cover rehydrate, publish, replay, rebuild, snapshots, restore admission, quarantine, and operator action. | Engine outcomes flow through this layer; do not create a parallel failure model or bypass irreversible restore/workflow state. |
| `EventPersister` | Serializes, protects every event, then allocates global positions and stages actor state; metadata and transformed bytes stay coupled. | Preserve all-protection-before-write and atomic actor flush. AAD cannot depend on later sequence/message/global-position values unless the call order/API is deliberately changed and proven. Never persist plaintext fallback. |
| `SnapshotManager` + `SnapshotRecord` | Protects before staging; durable snapshot state is currently `object`; unreadable protected snapshots are retained and load falls back to replay. | Freeze a deterministic v2 snapshot wrapper/serialization. Preserve retained unreadable snapshots and no plaintext fallback; decide whether advisory snapshot failure remains acceptable. |
| No-op DI registration | `TryAddSingleton<IEventPayloadProtectionService, NoOpEventPayloadProtectionService>` lets an explicit earlier registration win. | Preserve explicit opt-in; add production-safe validation later without silently changing default behavior. |
| Publisher/read/rehydrate/projection paths | Unprotect before domain use or plaintext publication; opaque/unreadable data fails closed; public/admin failures are bounded and support-safe. | Preserve these generic boundaries and freeze post-unprotect metadata semantics so plaintext is never paired with misleading protection state. |
| Crypto-shredding workflow/audit contracts | Provider-neutral lifecycle, restored-backup admission, operator decisions, and a 16-character SHA-256-prefix `KeyAliasFingerprint` exist; they do not execute a provider. | Bridge the engine to these contracts without deleting immutable events/audit history or absorbing Parties legal policy. Treat that truncated fingerprint only as redacted audit/idempotency correlation—never as durable key identity, lookup, selection, authorization, or wrapping input. |

### Hard Decisions That May Not Be Deferred To Story 8.2

- Exact engine/adapter package graph and public API ownership.
- Exact provider resource type and real integration environment.
- Exact format and algorithm identifiers; AES key, nonce, tag, and wrap parameters.
- Complete bounded envelope bytes and parser behavior.
- Complete injective AAD bytes and property-path grammar.
- DEK versus KEK identity/version semantics and rewrap/re-encryption consequences.
- Separate durable key ID, provider version ID, and display/audit fingerprint semantics with collision/migration rules.
- Nonce uniqueness design, crash/restart semantics, and per-key invocation budget.
- Exact historical routing/typed failures for every required format and snapshot state.
- Exact policy/erasure/discovery contract inventory and operational names.
- Key hierarchy/lifecycle/cache/retry/breaker/zeroing/audit responsibilities.
- Exact disabled/no-op, development-provider, invalid-backend, and approved-adapter mode/environment matrix plus real-backend conformance proof.
- Rollout, mixed-history, downgrade, and rollback after v2 writes.
- Threat model, vector corpus, no-leak contract, and all named approvals.

### Current Cryptographic And Provider Guidance

These are design inputs verified from the linked official sources on 2026-07-16, not pre-approved architecture choices. Story 8.1 must record its own retrieval/version evidence and Story 8.2 must reverify it:

- NIST SP 800-38D remains the current final GCM standard. It recommends 96-bit IVs, requires nonce uniqueness, fixes the tag length per key, and bounds RBG-based use across instances. NIST opened a second pre-draft revision call on 2026-06-01; recheck Rev.1 before Story 8.2 implementation. Use the current final as normative until a replacement is final.
- On .NET 10, `AesGcm` uses a 12-byte nonce. Cross-platform tag support converges on 16 bytes because Apple supports only 16 while Windows/Linux support 12-16. If the ADR adopts this portable choice, use the tag-size constructor and check `AesGcm.IsSupported`; the story must still approve key length and algorithm ID.
- RFC 5116 requires nonce uniqueness and authenticated decryption to yield plaintext or FAIL. AAD must be injective/uniquely parseable, which rules out delimiter concatenation.
- If RFC 6901 JSON Pointer is selected for property paths, freeze escaping, arrays, duplicates, Unicode, and non-resolving behavior exactly. If JCS, deterministic CBOR, or a binary schema is selected for canonical bytes, name the standard/profile and application schema explicitly.
- NIST SP 800-57 Part 1 Rev.5 distinguishes new-write cryptoperiod from historical decrypt retention. Rotation must make new writes select the new version while retaining exact old-version identity until approved re-encryption/destruction.
- NIST SP 800-88 Rev.2 makes crypto-erasure confidence depend on all key copies, caches, backups/escrow, and wrapping strength. Deleting one primary row is not sufficient proof.
- `CryptographicOperations.ZeroMemory` protects only the supplied mutable span from dead-store elimination. Managed GC/provider copies and immutable strings prevent a claim of absolute process-memory erasure; document owned-buffer zeroization precisely.
- For an Azure candidate, ordinary Key Vault and Managed HSM support different key/wrap choices. Soft-delete/purge protection delay irrecoverable key destruction, and versioned key identity must remain available for old wrapped DEKs. The ADR must name the resource type, key type/size, wrap algorithm, API/SDK policy, RBAC/network boundary, and deletion semantics.
- Retry only understood transient/idempotent operations with bounded attempts and elapsed time, backoff/jitter, `Retry-After`, and a breaker. Lifecycle mutations require operation identities and reconciliation because timeout may leave completion ambiguous.
- When the engine is enabled, development providers must be rejected by startup validation outside exact `Development`; an absent environment is Production. The disabled default no-op remains allowed in every environment and makes no protection/production-readiness claim.

### Minimum Golden And Negative Vector Matrix

The spec must define expected inputs, outputs, typed failure, retry/permanence, zeroing, and no-leak evidence for at least:

- A fixed positive vector with exact DEK, nonce, AAD fields/bytes, plaintext, ciphertext, 16-byte tag if selected, envelope, and wrapped-key metadata; reproduce through at least two independent implementations/toolchains, record exact versions/commands and output hashes, and compare with applicable NIST CAVP vectors.
- One-bit changes, truncation, extension, and substitution of nonce, tag, ciphertext, algorithm, version, key reference, tenant, domain, aggregate, event/snapshot type, path, key version, and format.
- AAD ambiguities: missing/null/empty, delimiter-bearing fields, case, composed/decomposed Unicode, escaped path segments, array indices, duplicate property names, and NUL.
- Envelope duplicate fields, non-canonical text encodings, invalid characters/padding, integer overflow, huge declared/actual lengths, unknown identifiers, and rejection before unbounded allocation/provider calls.
- Parallel nonce use, injected duplicate, process restart/crash, cloned instance, rotation, budget edge/exhaustion, and uncertain counter recovery.
- Latest/new write, old-version read, DEK/KEK rewrap semantics, cancellation/crash between wrap/persist and rotate/invalidate, deleted/missing/denied/recovered key, stale cache, and restored backup after erasure.
- Provider unauthenticated/denied/not-found/throttled/timeout/5xx/cancellation, `Retry-After`, breaker transitions, thundering herd, and ambiguous lifecycle mutation completion.
- Owned-buffer zeroing after success, auth failure, provider failure, cancellation, rotation, erasure, and disposal—worded as owned-buffer evidence, not total memory proof.
- Startup in absent/Production/Staging/Test/custom environments versus Development, unsupported cryptography, and real-adapter proof distinct from LocalDev.
- Legacy, v1, redacted, unprotected, snapshot, mixed history, unknown v2, downgrade, and rollback after an actual v2 write.

### Testing And Evidence Requirements

- Story 8.1 validation is document/security review: structure and traceability, threat-model review, independent vector reproduction, compatibility/rollback review, provider/operations review, package/release review, and named approval validation.
- Runtime/build tests do not substitute for this approval gate. Conversely, Story 8.1 should not edit runtime code merely to make vectors executable.
- Machine-readable vector companions are allowed only when their exact digests are incorporated into the approved spec. Record reproducible result hashes for both independent toolchains.
- The future Story 8.2 plan must extend, not replace, the existing Contract, Server, Client, Testing, and Integration security suites. Tests use xUnit v3, Shouldly, and NSubstitute; run projects individually and use `.slnx` only for restore/build.
- Real-backend and G5 evidence must inspect persisted state, provider state/identity, package outputs, and rollback results—not only HTTP status, mock calls, interface shape, or LocalDev.
- Future code must use centralized package versions, `ConfigureAwait(false)`, nullable/analyzer rules, one C# type per file, source-generated logging, and no secret/raw payload logging.
- Record exact commands, tool/library/provider versions, environment identity, result counts, persisted evidence paths, limitations, and reviewer disposition in the spec.

### Expected Story 8.1 File Changes

- **NEW:** `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`.
- **UPDATE during execution record only:** this story file's Dev Agent Record and normal sprint tracking performed by the development workflow.
- **No source/package/topology/consumer files are expected to change.** If a frozen decision contradicts AD-23 or requires a scope/ownership change, stop and run formal change control instead of editing architecture or code ad hoc.

### Story 8.2 Planning Targets — Do Not Create Here

Depending on approved decisions, the later implementation will likely create the engine project, optional adapter project, test projects, options/DI, codecs, policy discovery, key lifecycle/cache/resilience, real-backend integration, vectors, package-only consumer proof, release evidence, and G5 packet. It may update contracts and current persist/snapshot/publish/read paths only where the frozen additive design requires it.

When Story 8.2 creates approved packable projects, it must update the manifest, all inventory statements/tests, package metadata, package-only consumer validation, SBOM/provenance, and release evidence atomically. The inventory remains exactly 14 during Story 8.1.

### Project Structure Notes

- The single required spec belongs under `_bmad-output/implementation-artifacts`, not `docs/`, `src/`, or a submodule.
- `docs/` is product documentation, not scratch space. Story 8.1 does not revise the already-correct delivery-boundary guide unless a separately approved documentation correction is discovered.
- No UX file is changed: the July 16 decision has no EventStore UX surface; Parties UX remains consumer evidence.
- Do not initialize or modify an undeclared Parties checkout. Record an exact separately supplied source identity for review.
- Release inventory is manifest-driven and remains the 14 packages listed in `tools/release-packages.json` until Story 8.2 creates approved projects.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Payload-Protection-Security-Gate] and [Source: _bmad-output/planning-artifacts/epics.md#Story-8.1-Shared-Payload-Protection-Security-Spec-And-ADR] — sequencing, story, ACs, ownership, and exact output.
- [Source: _bmad-output/planning-artifacts/prd.md#6.9-Optional-Shared-Payload-Protection] and [Source: _bmad-output/planning-artifacts/prd.md#7-Cross-Cutting-Non-Functional-Requirements] — FR37 and NFR19.
- [Source: _bmad-output/planning-artifacts/architecture.md#AD-23---EventStore-Owns-The-Optional-Shared-Payload-Protection-Engine-ADOPTED] — binding architecture ownership and implementation block.
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16.md#2.1-Explicit-ownership-decision] and `#7-Approval-State` — responsibility split and the limits of planning approval.
- [Source: docs/guides/payload-protection-and-crypto-shredding.md#Payload-and-Snapshot-Protection-Hooks] — current hook, metadata, state, failure, workflow, redaction, and deferred-engine boundaries.
- [Source: src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs] — current provider-neutral API and backward-compatible defaults.
- [Source: src/Hexalith.EventStore.Contracts/Security/EventStorePayloadProtectionMetadata.cs] and [Source: src/Hexalith.EventStore.Contracts/Security/EventStorePayloadProtectionMetadataCarrier.cs] — current safe metadata v1 and fail-closed carrier.
- [Source: src/Hexalith.EventStore.Contracts/Security/UnreadableProtectedDataReason.cs] — typed failure taxonomy.
- [Source: src/Hexalith.EventStore.Contracts/Security/ProtectedDataReadabilityDecision.cs], [Source: src/Hexalith.EventStore.Contracts/Security/ProtectedDataReadabilityDecisionFactory.cs], and [Source: src/Hexalith.EventStore.Contracts/Security/ProtectedDataReadabilityDecisionStageCodes.cs] — canonical read/use decision contract.
- [Source: src/Hexalith.EventStore.Contracts/Security/CryptoShreddingWorkflowIdentity.cs], [Source: src/Hexalith.EventStore.Contracts/Security/CryptoShreddingWorkflowTransitions.cs], and [Source: src/Hexalith.EventStore.Contracts/Security/RestoredBackupAdmissionTransitions.cs] — current fingerprint, irreversible workflow, and restore-admission rules.
- [Source: src/Hexalith.EventStore.Server/Diagnostics/ProtectedDataDiagnosticRedactor.cs] and [Source: src/Hexalith.EventStore.Testing/Security/ProtectedDataLeakSentinel.cs] — current safe diagnostic and no-leak verification boundaries.
- [Source: src/Hexalith.EventStore.Server/Events/EventPersister.cs], [Source: src/Hexalith.EventStore.Server/Events/EventPublisher.cs], [Source: src/Hexalith.EventStore.Server/Events/SnapshotManager.cs], and [Source: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs] — current persistence/readability boundaries to preserve.
- [Source: src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs] — no-op default and opt-in override behavior.
- [Source: tools/release-packages.json] — current 14-package release inventory.
- [NIST SP 800-38D, GCM](https://csrc.nist.gov/pubs/sp/800/38/d/final) and [2026 Rev.1 pre-draft call](https://csrc.nist.gov/pubs/sp/800/38/d/r1/2prd).
- [RFC 5116, authenticated-encryption interface](https://www.rfc-editor.org/rfc/rfc5116.html), [RFC 6901, JSON Pointer](https://www.rfc-editor.org/rfc/rfc6901.html), and [RFC 4648, canonical base encodings](https://www.rfc-editor.org/rfc/rfc4648.html).
- [NIST SP 800-57 Part 1 Rev.5, key management](https://csrc.nist.gov/pubs/sp/800/57/pt1/r5/final), [NIST SP 800-38F, key wrapping](https://csrc.nist.gov/pubs/sp/800/38/f/final), and [NIST SP 800-88 Rev.2, media sanitization/crypto erase](https://csrc.nist.gov/pubs/sp/800/88/r2/final).
- [.NET cross-platform cryptography](https://learn.microsoft.com/dotnet/standard/security/cross-platform-cryptography), [`AesGcm`](https://learn.microsoft.com/dotnet/api/system.security.cryptography.aesgcm.-ctor?view=net-10.0), and [`CryptographicOperations.ZeroMemory`](https://learn.microsoft.com/dotnet/api/system.security.cryptography.cryptographicoperations.zeromemory?view=net-10.0).
- [Azure Key Vault key types/algorithms](https://learn.microsoft.com/azure/key-vault/keys/about-keys-details), [rotation](https://learn.microsoft.com/azure/key-vault/keys/how-to-configure-key-rotation), [soft delete](https://learn.microsoft.com/azure/key-vault/general/soft-delete-overview), and [RBAC guidance](https://learn.microsoft.com/azure/key-vault/general/rbac-guide) — candidate-provider constraints only; Azure is not selected by this story file.
- [ASP.NET Core environments](https://learn.microsoft.com/aspnet/core/fundamentals/environments?view=aspnetcore-10.0) and [.NET options validation](https://learn.microsoft.com/dotnet/core/extensions/options) — fail-start development-provider restrictions.

## Dev Agent Record

### Agent Model Used

OpenAI Codex (GPT-5)

### Implementation Plan / Decisions

- Build the single required spec incrementally in story task order, using normative content markers so approvals can bind a stable SHA-256 without a self-referential digest.
- Preserve every existing Story 22.7 contract and fail-closed read boundary; specify only additive future APIs and no runtime changes in Story 8.1.
- Treat the exact EventStore and Parties source identities plus primary cryptography/provider sources as the baseline before freezing wire, lifecycle, backend, and migration decisions.
- Validate each task with focused structural/security gates, then run the complete document/scope/fixture validation before requesting approval.

### Independent Review And Approval Evidence

### Debug Log References

- 2026-07-16: RED gate confirmed the required spec artifact was absent before implementation.
- 2026-07-16: Inspected EventStore HEAD `b200305978577530ee2e6ba9e92b886d26dc6f6f`, Story 22.7 contracts and every persist/read/publish/projection/admin/snapshot/restore/no-leak path.
- 2026-07-16: Resolved official Parties `main` SHA `4378dede55d92e489caf7aad63d6c2892e6f856d` and commit-pinned blob identities without initializing an undeclared checkout.
- 2026-07-16: Verified NIST, RFC, .NET 10, ASP.NET Core 10, and Azure Key Vault facts from primary sources; registered retrieval/revision/supersession posture and Story 8.2 reverification requirement.
- 2026-07-16: Task 1 focused document gate passed (281 lines, 19 unique external URLs, FR37/NFR19/AD-23/AC1-AC7 coverage, 14-package baseline).
- 2026-07-16: Task 2 RED gate confirmed ownership/package placeholders; focused green gate then verified the responsibility matrix, two-package graph, explicit opt-in/no-op behavior, fail-start environment matrix, and unchanged 14-package manifest.
- 2026-07-16: Task 3 RED gate confirmed wire-contract placeholders; Node.js 26.4.0/OpenSSL 3.5.7 and Python 3.14.4/cryptography 46.0.5 independently reproduced identical G-001 ciphertext/tag/envelope hashes and the NIST CAVP AES-256-GCM Count 0 tag.
- 2026-07-16: Task 3 focused gate verified the binary header/lengths, AAD/path schema, metadata allowlist, embedded wrapper/envelope hashes, and both independent crypto outputs.
- 2026-07-16: Task 4 RED gate confirmed compatibility/rollback placeholders; focused green gate verified the 15-route event matrix, snapshot routing, dual-read/single-write fence, irreversible v2 watermark, seven-step post-v2 rollback, and restore quarantine rules.
- 2026-07-16: Task 5 RED gate confirmed policy/lifecycle placeholders; focused green gate verified exact contract signatures, deterministic selection, ordered durable key reservation, lifecycle/cache deletion safety, bounded resilience, telemetry/audit names, and owned-buffer zeroing limits.
- 2026-07-16: Task 6 RED gate confirmed the backend placeholder; focused green gate selected an ordinary Premium Azure Key Vault with RSA-HSM-3072/RSA-OAEP-256, exact managed-identity/RBAC/private-network boundaries, constructive failures, truthful retention semantics, and a real-service G5 environment.
- 2026-07-16: Task 7 RED gate confirmed threat/no-leak placeholders; focused green gate verified 18 threat/misuse classes, constructive allowlists for every AC6 surface, the authenticated plaintext-or-failure invariant, G-001 key-record companion, and 138 named vector cases/families.
- 2026-07-16: Task 8 RED gate confirmed the handoff placeholder; focused green gate verified the source/test/package/topology/docs/consumer map, section-cited implementation sequence, atomic 14-to-16 package gate, and exact G5 evidence/Parties rollback packet.
- 2026-07-16: Task 9 disposition gate froze accepted/rejected decisions, limitations, migration posture, blocking approval/evidence register, authorization algorithm, and normative digest `efb419b5fa05d0b1d9bbf463261172cce181d5ada2c0c8d305751cc57497f440`; named independent reviews/approvals are absent, so the workflow halted with Story 8.2 not authorized.

### Completion Notes List

- Task 1 complete: created the single authoritative spec, content-bound digest model, traceability matrix, Story 22.7 path inventory, exact Parties provenance, and authoritative source register.
- Task 2 complete: froze ownership/trust boundaries, `Hexalith.EventStore.PayloadProtection` plus `Hexalith.EventStore.PayloadProtection.AzureKeyVault`, dependency/DI/options boundaries, atomic future 14-to-16 inventory change, and exact mode/environment behavior.
- Task 3 complete: froze the binary `pdenc-v2` envelope/carriers, strict bounded parser, AES-256-GCM and fresh-DEK ordinal nonce design, injective AAD/RFC 6901 profile, DEK-versus-KEK semantics, constructive metadata, and reproducible golden/CAVP evidence.
- Task 4 complete: froze per-record legacy/v1/redacted/v2/snapshot routing and typed failures, dual-read/single-write rollout, immutable-history migration posture, fail-closed downgrade constraints, real post-v2-write rollback, and irreversible restore admission.
- Task 5 complete: froze exact policy/erasure public contracts and precedence, deterministic event/snapshot selection, key identities/state keys/reservation protocol, lifecycle/rotation/deletion/restore ownership, cache/lease invalidation, resilience/reconciliation, telemetry/audit names, and honest zeroing guarantees.
- Task 6 complete: selected and constrained the ordinary Premium Azure Key Vault production adapter, RSA-HSM-3072/RSA-OAEP-256 profile, deterministic managed identity and least-privilege RBAC, private network, startup probe, failure mapping, backup-aware erasure limits, and real-service conformance evidence.
- Task 7 complete: froze assets/actors/attacker capabilities and 18 owned threat classes, authenticated plaintext-or-bounded-failure behavior, constructive no-leak allowlists across all required surfaces, an extended sentinel corpus, and 138 positive/negative/mutation/lifecycle/provider/rollback vectors.
- Task 8 complete: mapped every frozen decision to likely Story 8.2 artifacts, required exact section citations and ordered gates, defined the atomic two-package release/provenance/package-only proof, and froze the complete real-provider/Parties/G5 evidence packet.
- Task 9 partially complete: the ADR disposition and content-bound authorization rule are frozen, but mandatory named approvals and independent review evidence were not invented; Story 8.2 remains explicitly not authorized.

### File List

- `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md` (new)
- `_bmad-output/implementation-artifacts/8-1-shared-payload-protection-security-spec-and-adr.md` (updated)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (updated)

### Change Log

- 2026-07-16: Established authoritative Story 8.1 baseline and traceability; status remains in progress and Story 8.2 remains not authorized.
- 2026-07-16: Froze payload-protection ownership, package graph, explicit opt-in/no-op default, and production-safe startup matrix.
- 2026-07-16: Froze the canonical `pdenc-v2` wire/AAD/path/algorithm/nonce/metadata contract and independently reproduced its embedded golden.
- 2026-07-16: Froze historical compatibility, migration/rollout, downgrade, restore, and exercised post-v2-write rollback semantics.
- 2026-07-16: Froze policy discovery and the shared key hierarchy, durable lifecycle, deletion-safe cache, resilience, telemetry/audit, and owned-buffer zeroing contract.
- 2026-07-16: Selected the production Azure Key Vault resource/profile and froze custody, credential, RBAC, network, SDK/API, failure, retention, and real-service conformance restrictions.
- 2026-07-16: Completed the threat/misuse register, no-leak contract, fixed key-record companion, and exhaustive named verification-vector handoff.
- 2026-07-16: Froze the Story 8.2 implementation, package/release, real-backend, consumer, rollback, and G5 evidence handoff without changing runtime or release inventory.
- 2026-07-16: Froze ADR digest/disposition and recorded a hard approval-gate halt; no named approval, independent-review claim, or Story 8.2 authorization was fabricated.
