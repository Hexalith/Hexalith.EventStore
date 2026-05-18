# Story 22.7a: Payload and Snapshot Protection Hooks

Status: done

Context created: 2026-05-13
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-requirements-gaps-current.md`
Epic: Epic 22 - Public Gateway and Downstream Integration Contracts
Scope: FR102 only, with dependency awareness for Stories 22.1-22.6 and clear deferral of unreadable-data behavior, crypto-shredding, restored-backup safety, and operational redaction details to Stories 22.7b-22.7d.

## Story

As a platform owner handling PII,
I want event payload and snapshot protection extension points with explicit metadata,
so that protected state can be persisted, published, rehydrated, replayed, and rebuilt without exposing protected content.

## Protection Hook Contract

- EventStore already has `IEventPayloadProtectionService`, `PayloadProtectionResult`, `NoOpEventPayloadProtectionService`, `EventPersister`, `EventPublisher`, and `SnapshotManager`. This story must harden those extension points instead of replacing the event pipeline or introducing a second protection abstraction.
- Protection metadata is part of the public contract. It must identify whether a payload or snapshot is unprotected, protected, or produced by a protection provider in a way EventStore cannot interpret, without including plaintext, key material, raw IVs/nonces, or provider secrets.
- Event payload and snapshot protection must have equivalent metadata semantics. Event metadata can use envelope extensions or a typed contract-layer descriptor, but ST0 must record the chosen shape before code edits. Snapshot state currently has no typed metadata wrapper, so this story must decide and implement a safe equivalent.
- The hook contract must preserve EventStore's schema-ignorant domain payload model: domain services still return payload events only, EventStore owns envelope metadata, and protection providers are infrastructure extensions registered through DI.
- The default behavior remains no-op and backward-compatible. Existing callers without a custom protection service must continue to persist, publish, rehydrate, replay, and rebuild JSON payloads and snapshots exactly as before, except for harmless metadata defaults if the story explicitly chooses them.
- Protection metadata must flow through the same public surfaces that carry protected data: storage envelopes, DAPR publication envelopes, command-time rehydration, stream replay/read APIs from Story 22.6, projection rebuild DTOs, Testing builders/fakes, package docs, and API docs.
- This story must not implement key deletion/invalidation policy, missing-key behavior, restored-backup validation, or full redaction coverage. It must leave those as explicit follow-up contracts for 22.7b, 22.7c, and 22.7d.
- Provider-neutral metadata is the contract. EventStore-owned metadata may describe state, version, provider/scheme family, safe key alias, serialization/content hint, and compatibility flags, but must not embed provider-private crypto blobs or make promises about unreadable data, key lifecycle, backup restore safety, or operational redaction that belong to 22.7b-22.7d.
- ST0 must define metadata interpretation as a state machine, not a best-effort dictionary read. Missing legacy metadata may map to the approved legacy compatibility state, but malformed metadata, unknown required fields, unknown versions, or contradictory metadata/content pairings must fail closed or become `provider-opaque` according to the ST0 table; they must never be inferred as safe `unprotected` data.
- Protection metadata and transformed content must stay coupled. EventStore must not publish, replay, rebuild, store, or expose protected bytes with `unprotected` metadata, nor plaintext/unprotected bytes with `protected` metadata, unless ST0 records an explicit compatibility transition and focused tests prove the transition is intentional.

## Current Implementation Intelligence

- `src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs` currently exposes event protect/unprotect methods returning `PayloadProtectionResult` and snapshot protect/unprotect methods returning raw `object`. The event result contains only transformed bytes and serialization format; snapshots return no metadata.
- `src/Hexalith.EventStore.Contracts/Security/PayloadProtectionResult.cs` has no protection-state or provider metadata fields. If metadata is added, compatibility must be deliberate and tests must cover JSON serialization and binary/source compatibility expectations where practical.
- `src/Hexalith.EventStore.Server/Events/NoOpEventPayloadProtectionService.cs` validates inputs and returns payload bytes, serialization format, and snapshot state unchanged. Keep it as the default registration in `AddEventStoreServer`.
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs` calls `ProtectEventPayloadAsync` before writing the server `EventEnvelope`, stores `PayloadProtectionResult.PayloadBytes`, and writes `PayloadProtectionResult.SerializationFormat`. It currently sets `Extensions: null`, so protection metadata is not persisted.
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` calls `UnprotectEventPayloadAsync` before DAPR publish and publishes a new `EventEnvelope` with the returned bytes and serialization format. ST0 must decide whether protected content should remain protected on publish, be unprotected only under a documented policy, or expose a mode flag. Do not silently keep the current plaintext-publish behavior if protection is configured.
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs` calls `ProtectSnapshotStateAsync` before snapshot storage and `UnprotectSnapshotStateAsync` after snapshot load. Snapshot failures are advisory on create and fall back to full replay on load; do not change unreadable-key semantics in this story beyond recording metadata needed by 22.7b.
- `src/Hexalith.EventStore.Server/Events/SnapshotRecord.cs` stores `SequenceNumber`, `State`, `CreatedAt`, `Domain`, `AggregateId`, and `TenantId`; it has no protection metadata field.
- Contract event envelopes have typed `EventMetadata`, raw payload bytes, and an `Extensions` dictionary. Server event envelopes are flat DAPR actor-remoting records with the same payload and extensions pattern. Both `ToString()` implementations redact payload bytes.
- Existing security tests already pin no-payload logging in `tests/Hexalith.EventStore.Server.Tests/Security/PayloadProtectionTests.cs` and `tests/Hexalith.EventStore.Server.Tests/Logging/PayloadProtectionTests.cs`. Extend these tests rather than creating a parallel logging audit suite.
- Story 22.6 owns public stream replay/read APIs and projection rebuild checkpoints. This story must add protection metadata compatibility to those public replay DTOs if they already exist when implementation starts; otherwise record the required cross-story contract in the dev notes and tests.
- `docs/reference/nuget-packages.md` and `docs/reference/api/index.md` already list the Security package surface. A new guide is expected at `docs/guides/payload-protection-and-crypto-shredding.md`, but 22.7a should document only hook registration, metadata shape, and current no-op/default behavior.

## Acceptance Criteria

1. **Event payload protection metadata is explicit and serialized.**
   - Given a custom event payload protection service is configured
   - When EventStore persists, publishes, rehydrates, replays, or rebuilds events
   - Then the event contract carries protection metadata that identifies protection state, provider or scheme identifier, metadata version, and compatibility hints without exposing plaintext or key material.
   - And metadata survives storage, DAPR publication, JSON serialization, contract envelope conversion, public stream read/replay DTOs, Testing builders/fakes, and docs examples.
   - And unprotected/no-op payloads have a stable default state that downstream consumers can distinguish from missing metadata.
   - And ST0 freezes the serialized metadata contract before implementation: field names, required versus optional fields, valid protection states, default values, allowed unknown-field behavior, and client dependency expectations.
   - And old event envelopes with missing or null protection metadata remain readable and map to the ST0-approved legacy compatibility state instead of failing or being treated as protected by inference.
   - And malformed metadata, unknown required metadata versions, missing required fields, and contradictory payload/metadata states are never downgraded to `unprotected`; they either fail closed through the existing infrastructure path or map to an explicit provider-opaque state chosen in ST0.

2. **Snapshot protection metadata has parity with event payload metadata.**
   - Given snapshot protection hooks are configured
   - When snapshots are created, loaded, and used for command-time rehydration or rebuild flows
   - Then snapshot records carry protection metadata with the same state/version/provider semantics as event payload metadata.
   - And snapshot metadata is stored separately from protected state content and never requires EventStore to inspect domain state plaintext.
   - And existing no-op snapshots remain backward-compatible, including previously stored snapshots that do not yet contain protection metadata.
   - And snapshot protection uses an explicit result/record shape that carries protected state plus metadata for new code, rather than relying on an ambiguous raw `object` return path.
   - And old snapshots with no metadata have a documented compatibility path and focused test fixture coverage.

3. **Protection hook behavior is deterministic across pipeline stages.**
   - Given a payload or snapshot moves through persist, publish, rehydrate, replay, or rebuild paths
   - When protection is configured
   - Then EventStore invokes the correct protect or unprotect hook exactly where the story's ST0 decision table says it should.
   - And EventStore never performs ad hoc encryption, decryption, serialization, or provider-specific key lookup outside the registered protection service.
   - And hook cancellation is honored, `OperationCanceledException` is not swallowed, and non-cancellation failures map to the existing infrastructure failure behavior without payload disclosure.
   - And the ST0 decision table explicitly states the publish-time policy for protected, unprotected, and provider-opaque payloads. If 22.7a preserves today's unprotect-before-DAPR-publish behavior for domain-service compatibility, the story must document that as protected-at-rest-only behavior and test that metadata exposure is intentional and safe.
   - And hook boundaries are documented at the persistence/read boundary: after conversion to the persisted representation and before state write, and after state read and before domain dispatch/publish, unless ST0 records a narrower compatible alternative.
   - And EventStore never emits a payload whose bytes and metadata state disagree; provider returns, legacy compatibility reads, publish transformations, replay DTO projection, and snapshot load paths must all assert the same invariant.

4. **Public contracts do not leak key material or protected content.**
   - Given protection metadata is serialized through Contracts, Client, Testing, Server envelopes, DAPR publication, replay/read APIs, docs, and tests
   - When metadata is inspected by downstream consumers, operators, or developers
   - Then only non-secret fields are visible: state, provider/scheme identifier, metadata version, optional key identifier alias, content type/serialization hint, and safe compatibility flags.
   - And raw keys, plaintext, IVs/nonces, authentication tags, secret material, provider-specific internal blobs, state-store keys, and connection strings are excluded.
   - And `ToString()`, ProblemDetails, log messages, docs examples, and test evidence continue to use payload redaction.
   - And tests use explicit sentinel values such as `PLAINTEXT_SECRET_MARKER`, `KEY_ALIAS_SECRET_MARKER`, and provider-opaque marker content to prove public envelopes, published events, logs, exception messages, assertion output, and docs examples do not leak protected material or provider-private values.
   - And any key alias or provider identifier is treated as sensitive-by-default until ST0 marks the exact field as safe for the exact public surface; unsupported aliases must be redacted or omitted rather than copied into logs, ProblemDetails, assertion messages, or docs examples.

5. **Default no-op behavior and package boundaries remain stable.**
   - Given no custom protection service is registered
   - When existing command, event persistence, publication, snapshot, replay, and projection flows run
   - Then behavior remains equivalent to today and tests prove the default no-op path still works.
   - And public DTOs and builders belong in `Hexalith.EventStore.Contracts`, `Hexalith.EventStore.Client`, and `Hexalith.EventStore.Testing`; provider implementations and runtime wiring belong outside the domain service contract.
   - And no new dependency on a specific encryption provider, Key Vault, cloud KMS, or DAPR secret store is required by the default package.
   - And the no-op provider returns deterministic metadata behavior chosen in ST0, with tests covering both new no-op records and legacy records where metadata is absent.

6. **Implementation guidance and evidence separate 22.7a from later protection stories.**
   - Given key deletion, invalidation, missing keys, backup restore, admin redaction, CLI/MCP redaction, and full failure taxonomy are needed
   - When this story is implemented
   - Then those topics are recorded as deferred to 22.7b-22.7d unless the minimal metadata hook shape is required for compatibility.
   - And docs state that 22.7a creates hook and metadata contracts, not a complete crypto-shredding workflow or operational redaction program.
   - And Dev Agent Record, File List, Verification Status, and Change Log are updated before moving the story to review.

## Tasks / Subtasks

- [x] **ST0 - Baseline current hook behavior and freeze metadata decisions.** (AC: 1, 2, 3, 4, 5, 6)
    - [x] Read this story, Epic 22, PRD FR102-FR104, architecture `Payload and Snapshot Protection`, Stories 22.1-22.6, and `_bmad-output/project-context.md` before code edits.
    - [x] Inventory `IEventPayloadProtectionService`, `PayloadProtectionResult`, `NoOpEventPayloadProtectionService`, `EventPersister`, `EventPublisher`, `SnapshotManager`, `SnapshotRecord`, server and contract `EventEnvelope`, and existing payload-redaction tests.
    - [x] Inventory public package surfaces affected by Story 22.6 if present: stream read/replay DTOs, event page DTOs, gateway client methods, Testing builders/fakes, and replay/rebuild docs.
    - [x] Record a decision table for event payload metadata shape, snapshot metadata shape, no-op default state, publish-time protect/unprotect policy, replay/read metadata exposure, and backward compatibility for existing envelopes/snapshots.
    - [x] Record exact field names or equivalents for protection state, provider/scheme identifier, metadata version, safe key alias, serialization/content hint, and compatibility flags.
    - [x] Reserve a stable metadata namespace or carrier key, such as an EventStore-owned protection extension key if `Extensions` remains the event storage carrier, and document how unknown future metadata fields are preserved or rejected.
    - [x] Define the exact meanings of `unprotected`/`no-op`, `protected`, and `provider-opaque`, including whether each state can be published, replayed, rebuilt, or exposed through EventStore-owned read DTOs.
    - [x] Define legacy behavior for persisted events with `Extensions: null` and snapshots with no metadata; missing metadata must map to an explicit compatibility state, not an implementation accident.
    - [x] Define fail-closed behavior for malformed metadata, unknown required metadata versions, missing required fields, unsafe key aliases, and bytes/metadata state mismatches.
    - [x] Define the invariant that transformed content and metadata state travel together across persist, publish, read/replay, rebuild, and snapshot paths, including the exact tests that prove mismatches cannot be emitted.
    - [x] Record explicit out-of-scope items for 22.7b-22.7d: missing-key behavior, key invalidation, backup restore safety, and full operational redaction.

- [x] **ST1 - Add contract-layer protection metadata types.** (AC: 1, 2, 4, 5)
    - [x] Add a small immutable contract type for protection metadata under `src/Hexalith.EventStore.Contracts/Security` or an equivalent established namespace.
    - [x] Extend `PayloadProtectionResult` to return transformed bytes, serialization format, and protection metadata while preserving the no-op construction path.
    - [x] Add or adapt snapshot protection result semantics so snapshot state and snapshot metadata travel together without using ambiguous raw `object` return values for new code.
    - [x] Add validation that metadata fields are non-secret, bounded, serialization-safe, and stable across JSON round trips.
    - [x] Reject or sanitize metadata values that use forbidden secret-shaped field names or values, including raw keys, plaintext, IVs/nonces, authentication tags, provider-private blobs, state-store keys, and connection strings.
    - [x] Centralize metadata parsing/validation so all callers get identical legacy, malformed, unknown-version, and provider-opaque behavior instead of duplicating dictionary/string checks.
    - [x] Keep provider-specific implementations out of Contracts; Contracts owns shape and semantics only.

- [x] **ST2 - Persist and convert event protection metadata.** (AC: 1, 3, 4, 5)
    - [x] Update `EventPersister` so protected-event metadata is written with the persisted server `EventEnvelope` using the ST0-approved location.
    - [x] Update server-to-contract envelope conversion so metadata reaches public event envelopes and replay/read DTOs without exposing payload bytes.
    - [x] Update `EventPublisher` to follow the ST0 publish-time policy and preserve or transform metadata consistently.
    - [x] Add checked legacy event fixtures or equivalent serialized test data proving pre-22.7a records with missing metadata still deserialize, convert, and replay/read through the compatibility path.
    - [x] Ensure failures from protection hooks route through existing infrastructure failure/dead-letter behavior and do not log payload bytes or secret metadata.
    - [x] Add negative tests for persisted/published event records where metadata claims `unprotected` while bytes came from a protected provider, metadata claims `protected` while bytes are the no-op/plain payload, metadata version is unknown, or provider-opaque state is exposed through read/replay DTOs.
    - [x] Add focused tests proving protected metadata is present after persistence, publication, conversion to contract envelope, and no-op flows.

- [x] **ST3 - Persist and load snapshot protection metadata.** (AC: 2, 3, 4, 5)
    - [x] Update `SnapshotRecord` or an equivalent wrapper to carry snapshot protection metadata without breaking old no-metadata snapshots.
    - [x] Update `SnapshotManager.CreateSnapshotAsync` to store protected state and metadata from the protection service.
    - [x] Update `SnapshotManager.LoadSnapshotAsync` to pass metadata back to the protection service or use the ST0-approved compatibility path.
    - [x] Add checked legacy snapshot fixtures or equivalent serialized test data covering old raw snapshots, new no-op snapshots, new protected snapshots, missing metadata, unknown metadata version, and provider-opaque metadata.
    - [x] Keep snapshot creation advisory and load fallback behavior consistent with current semantics; do not implement missing-key failure policy beyond metadata needed by 22.7b.
    - [x] Add mismatch tests for snapshots where state content and metadata state disagree, and require the same fail-closed/provider-opaque handling chosen for events unless ST0 records a snapshot-specific reason.
    - [x] Add tests for no-op snapshot compatibility, protected snapshot metadata persistence, old snapshot load compatibility, and payload/state redaction in failure logs.

- [x] **ST4 - Update public Testing and documentation surfaces.** (AC: 1, 2, 4, 5, 6)
    - [x] Add or update Testing builders/fakes for protected and unprotected event envelopes, payload protection results, snapshot records, and replay/read page items.
    - [x] Update package reference docs and API index entries for the protection metadata contract.
    - [x] Create or update `docs/guides/payload-protection-and-crypto-shredding.md` with only the 22.7a hook registration, metadata, no-op default, and deferral boundaries.
    - [x] Update any Story 22.6 replay/read docs so protected metadata appears in examples without showing protected payload content.
    - [x] Keep examples provider-neutral; do not require Azure Key Vault, DAPR secret store, local certificates, or a specific encryption algorithm in this story.

- [x] **ST5 - Prove no-leak and compatibility behavior.** (AC: 1, 2, 3, 4, 5, 6)
    - [x] Extend Contracts tests for metadata validation, JSON round trips, no-op defaults, and event envelope serialization.
    - [x] Extend Server tests for `EventPersister`, `EventPublisher`, `SnapshotManager`, contract envelope conversion, and protection hook invocation order.
    - [x] Add tests that prove every protection/unprotection hook receives the caller's `CancellationToken`, and that `OperationCanceledException` propagates without being mapped to non-cancellation infrastructure failure.
    - [x] Extend existing payload-redaction source/log tests to cover new metadata fields and ensure forbidden field names or secret values are not logged.
    - [x] Extend Client/Testing tests only where public replay/read or gateway DTOs expose the metadata.
    - [x] Run focused test projects individually and record evidence. At minimum: `tests/Hexalith.EventStore.Contracts.Tests`, relevant `tests/Hexalith.EventStore.Server.Tests` slices, and `tests/Hexalith.EventStore.Testing.Tests` if Testing builders/fakes change.

## Test Evidence Required

- Contract metadata validation and JSON serialization prove no-op, protected, and provider-opaque states.
- Event persistence tests prove `ProtectEventPayloadAsync` output metadata is stored and survives conversion to public envelopes.
- Event publication tests prove the ST0 publish-time policy and metadata propagation without payload/key leakage.
- Snapshot manager tests prove protected snapshot metadata persistence, no-op compatibility, and old snapshot compatibility.
- Source/log redaction tests prove new metadata does not introduce payload or secret leakage through logs, `ToString()`, ProblemDetails text, docs examples, or test artifacts.
- Testing package tests prove builders/fakes can create protected/unprotected fixtures without requiring a live protection provider.
- Legacy event and snapshot fixtures prove missing metadata remains readable and maps to the ST0-approved compatibility state.
- Metadata mismatch fixtures prove EventStore does not emit protected bytes as unprotected, plaintext/no-op bytes as protected, unknown versions as unprotected, or provider-opaque records without the ST0-approved policy.
- ATDD-style no-leak scenarios prove sentinel plaintext, key-alias, and provider-opaque markers never appear in public envelopes, published events, logs, exception messages, assertion output, or docs examples.
- Malformed or unsafe metadata fails closed according to the ST0 contract and is never treated as unprotected data by default.

## Developer Notes

- Preserve the architecture rule that EventStore owns envelope metadata while domain services return only payload events. Protection providers are infrastructure hooks; domain services must not supply EventStore metadata or key material.
- Prefer small immutable records with explicit validation over stringly typed dictionaries for new public metadata. If envelope `Extensions` remains the storage carrier, wrap well-known keys behind typed helpers and tests.
- Keep metadata safe to log only if it is explicitly non-secret. A key alias or key ID must be treated as an alias, not raw key material; if in doubt, redact it.
- Do not add provider-specific cryptography, key lifecycle, or cloud KMS dependencies in this story. A fake/custom provider for tests is acceptable.
- Do not bypass `IActorStateManager` or read DAPR state directly when proving replay, rehydration, or snapshot behavior.
- Do not add ad hoc retry loops around protection providers. Use existing pipeline failure handling and cancellation semantics.
- Keep existing no-op behavior working for old envelopes/snapshots. This matters for current users and for tests that use `NoOpEventPayloadProtectionService`.
- Treat `EventPublisher` carefully: current code unprotects before publish. The implementation must make a deliberate policy decision and test it; accidental plaintext publication when protection is configured is not acceptable.
- Do not expose protection metadata through domain service contracts. EventStore-owned read/admin/replay DTOs may expose the safe metadata selected in ST0; domain services continue to receive payload events only.
- Prefer one reusable provider-neutral metadata contract for events and snapshots. If event storage must use `Extensions`, wrap the well-known EventStore key behind typed helpers rather than scattering string keys.
- A key alias is safe only if it is deliberately non-secret. Treat aliases as sensitive-by-default in tests unless ST0 explicitly marks the field as safe to expose.
- Treat `provider-opaque` as a compatibility boundary, not a license to guess. EventStore may carry opaque metadata when ST0 permits it, but must not decrypt, serialize-inspect, publish as plaintext, or silently rebuild from it outside the registered protection service.
- If compatibility pressure conflicts with no-leak guarantees, preserve the no-leak guarantee and record the compatibility exception as a deferred decision for 22.7b-22.7d rather than weakening the metadata contract in 22.7a.
- If Story 22.6 has not yet added public replay/read DTOs when this story starts, document the required metadata compatibility in the story record and add tests to the first available public event DTO surface.
- `Hexalith.EventStore.Server.Tests` has known pre-existing CA2007 build failures in this workspace; run focused slices and record exact commands/results rather than claiming a clean full project run unless verified.

## Files Likely Touched

- `src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs`
- `src/Hexalith.EventStore.Contracts/Security/PayloadProtectionResult.cs`
- `src/Hexalith.EventStore.Contracts/Security/*ProtectionMetadata*.cs`
- `src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs`
- `src/Hexalith.EventStore.Server/Events/NoOpEventPayloadProtectionService.cs`
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs`
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs`
- `src/Hexalith.EventStore.Server/Events/SnapshotRecord.cs`
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
- `src/Hexalith.EventStore.Testing/Builders/EventEnvelopeBuilder.cs`
- `src/Hexalith.EventStore.Testing/Fakes/FakeSnapshotManager.cs`
- `docs/reference/nuget-packages.md`
- `docs/reference/api/index.md`
- `docs/guides/payload-protection-and-crypto-shredding.md`
- `tests/Hexalith.EventStore.Contracts.Tests/Events/EventEnvelopeTests.cs`
- `tests/Hexalith.EventStore.Contracts.Tests/Security/*`
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPersisterTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotManagerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Security/PayloadProtectionTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Logging/PayloadProtectionTests.cs`
- `tests/Hexalith.EventStore.Testing.Tests/Builders/EventEnvelopeBuilderTests.cs`

## Out of Scope

- Real encryption provider implementation, key vault integration, cloud KMS integration, local certificate management, or DAPR secret-store setup.
- Missing, deleted, invalidated, rotated, or inconsistent key behavior beyond metadata needed for later stories.
- Crypto-shredding workflow, restored-backup validation, and irreversible deletion semantics.
- Full redaction coverage for admin UI, CLI, MCP, ProblemDetails, replay/rebuild diagnostics, and backup validation beyond protecting newly added metadata from obvious leaks.
- Rewriting command processing, event storage, projection rebuild, or DAPR pub/sub architecture.
- Changing domain service contracts so domain services own protection metadata or EventStore key material.

## References

- `_bmad-output/planning-artifacts/epics.md` - Story 22.7a requirements and Epic 22 story split.
- `_bmad-output/planning-artifacts/prd.md` - FR102-FR104 and NFR12.
- `_bmad-output/planning-artifacts/architecture.md` - Payload and Snapshot Protection; SEC-1, SEC-5; Publishing, Replay & Protection Contracts.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-requirements-gaps-current.md` - payload-protection backlog and docs expectations.
- `_bmad-output/project-context.md` - package boundaries, logging, DAPR, actor state, and testing rules.
- `_bmad-output/implementation-artifacts/22-5-event-publishing-guarantees-and-backend-deployment-matrix.md` - publish guarantee boundary and Story 22.7 deferral notes.
- `_bmad-output/implementation-artifacts/22-6-stream-replay-read-apis-and-projection-rebuild-checkpoints.md` - replay/read API compatibility and protected-payload failure taxonomy awareness.
- `src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs`
- `src/Hexalith.EventStore.Contracts/Security/PayloadProtectionResult.cs`
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs`
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs`
- `src/Hexalith.EventStore.Server/Events/SnapshotRecord.cs`

### Review Findings

- [x] [Review][Patch] Snapshot metadata is persisted and loaded without carrier validation, so malformed or secret-shaped typed metadata can bypass the event fail-closed path [src/Hexalith.EventStore.Server/Events/SnapshotManager.cs:44]
- [x] [Review][Patch] Serialized `eventstore.protection` JSON silently ignores unknown top-level fields, allowing forbidden provider-private fields to be accepted instead of mapped to `ProviderOpaque` [src/Hexalith.EventStore.Contracts/Security/EventStorePayloadProtectionMetadataCarrier.cs:196]
- [x] [Review][Patch] Event unprotect receives bytes and format only, so `EventPublisher` reads stored metadata but cannot pass key alias/scheme/legacy state back to the provider [src/Hexalith.EventStore.Server/Events/EventPublisher.cs:95]
- [x] [Review][Patch] `FakeEventPersister` still emits `Extensions: null`, so the Testing package fake does not preserve the new explicit no-op metadata contract [src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs:61]
- [x] [Review][Patch] Checked-in API reference docs are stale and omit the new metadata/result/state public contract surface [docs/reference/api/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.Security.md:8]
- [x] [Review][Patch] Provider-opaque publish documentation contradicts itself by saying opaque records are never republished and later that they are published verbatim [docs/guides/payload-protection-and-crypto-shredding.md:113]

## Party-Mode Review

### 2026-05-13T16:22:13+02:00

- Selected story key: `22-7a-payload-and-snapshot-protection-hooks`
- Command/skill invocation used: `/bmad-party-mode 22-7a-payload-and-snapshot-protection-hooks; review;`
- Participating BMAD agents: John (Product Manager), Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor)
- Findings summary:
  - Metadata semantics were still too implicit for a public/storage contract story. The review asked for ST0 to freeze field names, requiredness, states, default values, unknown-field behavior, and legacy interpretation.
  - Snapshot parity needed stronger implementation guidance because the current snapshot hook returns a raw `object` and snapshot records have no metadata field.
  - Event publication policy is the highest-risk implementation boundary because current code unprotects before DAPR publication. The story now requires an explicit ST0 policy and tests rather than accidental plaintext publication.
  - Compatibility needed concrete old-envelope and old-snapshot fixture coverage.
  - No-leak proof needed sentinel-based tests across public envelopes, published events, logs, exception messages, assertion output, and docs examples.
- Changes applied:
  - Added provider-neutral metadata contract guardrails and forbidden provider-private metadata examples.
  - Tightened AC1-AC5 with explicit schema, legacy compatibility, snapshot result, publish-policy, hook-boundary, no-leak, and no-op determinism requirements.
  - Expanded ST0-ST5 tasks for metadata namespace ownership, state semantics, legacy fixtures, snapshot matrices, cancellation propagation, and malformed metadata handling.
  - Added developer notes clarifying EventStore-owned metadata exposure, domain-service isolation, typed helpers over scattered extension keys, and key-alias sensitivity.
- Findings deferred:
  - Actual crypto provider behavior, key wrapping, tenant key lookup, key deletion/invalidation, crypto-shredding, restored-backup safety, broader operational redaction, and provider-specific metadata interpretation remain deferred to 22.7b-22.7d.
  - Whether EventStore should ever publish protected payloads internally remains a future compatibility decision unless ST0 records a 22.7a-safe policy before implementation.
- Final recommendation: `ready-for-dev`

## Advanced Elicitation

### 2026-05-13T18:03:48+02:00

- Selected story key: `22-7a-payload-and-snapshot-protection-hooks`
- Command/skill invocation used: `/bmad-advanced-elicitation 22-7a-payload-and-snapshot-protection-hooks`
- Batch 1 methods: Self-Consistency Validation; Red Team vs Blue Team; Architecture Decision Records; Failure Mode Analysis; Comparative Analysis Matrix
- Batch 2 methods: Chaos Monkey Scenarios; First Principles Analysis; Occam's Razor Application; 5 Whys Deep Dive; Lessons Learned Extraction
- Findings summary:
  - The story had strong schema and no-leak language, but still allowed implementers to treat metadata as a loose dictionary instead of a deterministic state machine.
  - The highest-risk hidden failure was a mismatch between transformed content and metadata state, especially around legacy reads, no-op providers, provider-opaque data, and publish-time transformations.
  - Key aliases and provider identifiers needed a default sensitivity stance because "safe alias" can become an accidental secret if copied into logs or public diagnostics without an ST0 decision.
  - Provider-opaque records needed clearer boundaries so 22.7a can carry metadata safely without guessing at crypto semantics reserved for 22.7b-22.7d.
- Changes applied:
  - Added state-machine interpretation requirements for missing, malformed, unknown-version, and contradictory metadata.
  - Added acceptance criteria and tasks proving EventStore never emits protected bytes as unprotected or plaintext/no-op bytes as protected.
  - Added centralized parsing/validation guidance so callers do not duplicate metadata dictionary checks.
  - Added event and snapshot mismatch fixture requirements and tightened key-alias sensitivity guidance.
  - Added developer notes preserving no-leak guarantees over compatibility pressure and treating provider-opaque as a carry-only boundary.
- Findings deferred:
  - Concrete crypto provider behavior, missing-key runtime behavior, key invalidation, restored-backup safety, operational redaction, and provider-specific opaque metadata interpretation remain deferred to Stories 22.7b-22.7d.
  - The exact publish-time policy still belongs in ST0 before implementation; this pass only tightened the invariants that policy must satisfy.
- Final recommendation: `ready-for-dev`

## Dev Agent Record

### Agent Model Used

Claude Opus 4.7 (1M context)

### ST0 Decision Table (frozen 2026-05-18)

**Metadata type and namespace**

- New sealed immutable record `EventStorePayloadProtectionMetadata` lives in `Hexalith.EventStore.Contracts.Security`. Provider-neutral, JSON-serializable, validated at construction.
- New enum `PayloadProtectionState`: `Unprotected`, `Protected`, `ProviderOpaque`.

**Field shape**

| Field | Required | Description |
| --- | --- | --- |
| `State` | yes | `Unprotected`, `Protected`, or `ProviderOpaque`. |
| `MetadataVersion` | yes | Integer >= 1. Current schema version is 1. Unknown future versions fail-closed to `ProviderOpaque`. |
| `Scheme` | optional | Provider/scheme family identifier (e.g. `"aes-gcm-256"`, `"none"`). Required when `State != Unprotected`. Bounded length, non-secret. |
| `KeyAlias` | optional | Non-secret key reference alias. Treated sensitive-by-default. Bounded length. NEVER raw key material. |
| `ContentHint` | optional | Content-type hint after un/protection (e.g. `"application/json"`). |
| `CompatibilityFlags` | optional | Small bounded `IReadOnlyDictionary<string, string>` for forward-compatible flags. Hard cap (8 entries, 64-char key, 256-char value). Keys must be safe identifiers; secret-shaped keys/values rejected. |

**Forbidden field names / value shapes (rejected at validation)**

Lowercase keyword scan rejecting either as a `CompatibilityFlags` key or as a substring of any string value: `key=` (with non-empty rhs), `iv=`, `nonce=`, `tag=`, `password`, `secret`, `bearer `, `private-key`, `connection-string`, `plaintext`, `dapr-secret`, `vault-uri`. Top-level `KeyAlias` and `Scheme` are also length-bounded and ASCII-printable only.

**Storage carrier**

- **Events:** persisted server `EventEnvelope.Extensions` dictionary carries a single EventStore-owned key `eventstore.protection` whose value is a UTF-8 JSON serialization of `EventStorePayloadProtectionMetadata`. Carrier helpers (`EventStorePayloadProtectionMetadataCarrier`) in `Contracts.Security` centralize read/write/legacy/malformed handling. Other `Extensions` keys are preserved unchanged.
- **Snapshots:** `SnapshotRecord` gains an optional `ProtectionMetadata` typed field (nullable). Legacy snapshots deserialize with `ProtectionMetadata = null` and are mapped to the legacy compatibility state.

**No-op default state**

The default `NoOpEventPayloadProtectionService` returns `EventStorePayloadProtectionMetadata(State: Unprotected, MetadataVersion: 1, Scheme: null, KeyAlias: null, ContentHint: null, CompatibilityFlags: null)` for every protect/unprotect call. The persisted `eventstore.protection` extension is written by `EventPersister` so consumers can always distinguish "this row is explicitly unprotected" from a legacy row.

**Legacy compatibility (missing metadata)**

- Persisted event without `eventstore.protection` extension OR with `Extensions: null` → carrier returns `EventStorePayloadProtectionMetadata(Unprotected, 1, Scheme: null, KeyAlias: null, ContentHint: null, CompatibilityFlags: { "legacy": "missing" })`. Legacy rows behave as `Unprotected` for downstream consumers but the flag preserves provenance.
- Persisted snapshot with `ProtectionMetadata: null` → carrier returns the same `Unprotected + legacy=missing` metadata.

**Fail-closed (malformed metadata)**

- JSON parse failure, unknown `MetadataVersion > 1`, missing required `State`, or `State`/`Scheme` mismatch (e.g. `Protected` without a `Scheme`) → carrier returns `EventStorePayloadProtectionMetadata(ProviderOpaque, 1, Scheme: null, KeyAlias: null, ContentHint: null, CompatibilityFlags: { "parseError": "true" })`. Never inferred to `Unprotected`.
- Forbidden secret-shaped fields in stored metadata → same `ProviderOpaque` mapping with `CompatibilityFlags["forbidden"] = "true"`.

**Bytes/metadata invariant**

EventStore never emits a payload whose `State` claims `Unprotected` while bytes came from a non-no-op `Protect*` call, nor `Protected` metadata on bytes returned by a no-op call. Provider returns, legacy reads, publish-time transformations, replay/read DTO projection, and snapshot load paths all carry the metadata returned by the protection service alongside the bytes it produced. ProviderOpaque bytes are passed through without invoking unprotection.

**Publish-time policy**

`EventPublisher.PublishEventsAsync` continues to call `UnprotectEventPayloadAsync` before DAPR publication (today's behavior, preserved for domain-service compatibility). The published envelope `Extensions` dictionary carries the `eventstore.protection` key with the metadata returned by `UnprotectEventPayloadAsync` (typically `Unprotected` for the no-op provider; a custom unprotecting provider may return any state and the publisher emits exactly what it returned). This makes EventStore's "protected-at-rest only" posture explicit on the wire; subscribers can distinguish payload protection states without inspecting bytes. Note: a fully fail-closed publish-time policy that refuses to publish `Protected` bytes is deferred to Story 22.7b alongside missing-key behavior.

**Hook boundaries**

- `ProtectEventPayloadAsync`: in `EventPersister.PersistEventsAsync`, after the payload bytes/format are computed and before `IActorStateManager.SetStateAsync`. Caller's `CancellationToken` is forwarded.
- `UnprotectEventPayloadAsync`: in `EventPublisher.PublishEventsAsync`, once per stored envelope, before constructing the publish envelope. Caller's `CancellationToken` is forwarded.
- `ProtectSnapshotAsync` (new typed method): in `SnapshotManager.CreateSnapshotAsync`, before `IActorStateManager.SetStateAsync`. Caller's `CancellationToken` is forwarded.
- `UnprotectSnapshotAsync` (new typed method): in `SnapshotManager.LoadSnapshotAsync`, after `TryGetStateAsync` and before returning. Caller's `CancellationToken` is forwarded.

**Snapshot API evolution**

The contract `IEventPayloadProtectionService` keeps existing `ProtectSnapshotStateAsync`/`UnprotectSnapshotStateAsync` (raw `object` in/out) for backward compatibility with any external implementer. Two new typed methods are added as default interface methods so implementers may opt in:

- `Task<SnapshotProtectionResult> ProtectSnapshotAsync(identity, state, ct)`
- `Task<object> UnprotectSnapshotAsync(identity, protectedState, EventStorePayloadProtectionMetadata? metadata, ct)`

Default implementations delegate to the existing object-based methods and wrap the result in `Unprotected` metadata (legacy provider passthrough). The production `NoOpEventPayloadProtectionService` overrides them explicitly to return well-formed `Unprotected` metadata.

**Cancellation propagation**

All four hook invocations forward the caller's `CancellationToken`. `OperationCanceledException` is rethrown by `EventPublisher` and `EventPersister` without being mapped to infrastructure failure.

**Public read/replay DTO compatibility**

`StreamReadEvent` (Story 22.6) gains an optional `ProtectionMetadata` field of type `EventStorePayloadProtectionMetadata?`. `StreamsController.ToStreamReadEvent` populates it from the stored envelope's `Extensions` via the carrier. The `Hexalith.EventStore.Client` HTTP path remains backward-compatible because the field is nullable. `StreamReadPageBuilder` exposes a `WithProtection` helper.

**Out of scope (deferred to 22.7b-d)**

- Missing-key behavior, key invalidation/rotation/restoration, crypto-shredding workflow (22.7c).
- Fail-closed publish-time refusal for `Protected` bytes when the provider cannot unprotect (22.7b).
- Restored-backup safety / cross-environment key reachability (22.7c).
- Full operational redaction across Admin UI, CLI, MCP, ProblemDetails, backup validation tooling (22.7d).
- Real encryption provider implementation, key vault integration, cloud KMS, DAPR secret store wiring.

### Debug Log References

- Story 22.7a implementation pass kicked off 2026-05-18.
- Focused validation:
  - `dotnet test tests/Hexalith.EventStore.Contracts.Tests` → 359/359 passed (incl. 26 new `EventStorePayloadProtectionMetadataTests`).
  - `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "Events|Security|Logging"` → 95/95 focused unit-only slice passed; pre-existing 37 failures (Tier-2 DAPR placement/scheduler tests and ValidationExceptionHandler/QueryNotFoundExceptionHandler/ReplayController/CommandStatusController) verified unchanged from Story 22.6's documented baseline.
  - `dotnet test tests/Hexalith.EventStore.Testing.Tests` → 112/112 passed.
  - `dotnet test tests/Hexalith.EventStore.Client.Tests` → 393/393 passed.
  - `dotnet test tests/Hexalith.EventStore.Sample.Tests` → 74/74 passed.
  - `dotnet test tests/Hexalith.EventStore.SignalR.Tests` → 35/35 passed.
- `dotnet build Hexalith.EventStore.slnx --configuration Debug` → 0 warnings, 0 errors.

### Completion Notes List

- **Contracts contract.** Added `EventStorePayloadProtectionMetadata` (state, version, scheme, key alias, content hint, compatibility flags), `PayloadProtectionState` enum, `SnapshotProtectionResult`, and the `EventStorePayloadProtectionMetadataCarrier` helper. Carrier centralizes serialize/parse/validate/legacy/forbidden-shape logic so every caller gets identical state-machine semantics.
- **PayloadProtectionResult evolution.** Added a `Metadata` field with backward-compatible constructor that emits `Unprotected` metadata when callers don't migrate. No-op call sites compile unchanged.
- **Interface evolution.** Added metadata-aware event unprotect and typed snapshot methods `ProtectSnapshotAsync` / `UnprotectSnapshotAsync` to `IEventPayloadProtectionService` as default-interface implementations that delegate to the existing object-based methods. External implementers continue to compile; production code now uses the typed methods.
- **Server persistence.** `EventPersister` writes the `eventstore.protection` extension on every persisted envelope. `EventPublisher` reads the stored metadata, passes through `ProviderOpaque` records verbatim (never invokes unprotect on bytes EventStore cannot identify), and re-stamps the published envelope with the metadata returned by the provider. Both forward the caller's `CancellationToken` to the protection hooks.
- **Snapshot persistence.** `SnapshotRecord` gains an optional `ProtectionMetadata` field. `SnapshotManager` calls the new typed methods; legacy `ProtectionMetadata == null` snapshots map to the explicit `Legacy()` compatibility record on load; invalid typed metadata maps to `ProviderOpaque`; provider-opaque snapshots are returned without invoking `UnprotectSnapshotAsync`.
- **Read DTO.** `StreamReadEvent` gains an optional `ProtectionMetadata` field. `StreamsController.ToStreamReadEvent` populates it via the carrier so downstream replay consumers see the protection state without inspecting bytes.
- **Testing surfaces.** `EventEnvelopeBuilder.WithProtectionMetadata`, `StreamReadPageBuilder.WithProtection`, `FakeSnapshotManager`, and `FakeEventPersister` now stamp metadata onto fixtures. `FakeEventPersister` and `ISnapshotManager` plumb the optional `CancellationToken` through.
- **Docs.** Added `docs/guides/payload-protection-and-crypto-shredding.md` with the hook contract, state machine, fail-closed rules, and 22.7b/22.7c/22.7d deferral boundaries. Updated `docs/reference/stream-replay-api.md` and the checked-in API reference docs to mention the new metadata/result/state surfaces.
- **No-leak proof.** Sentinel-based tests in `Contracts.Tests.Security.EventStorePayloadProtectionMetadataTests` and `Server.Tests.Security.PayloadProtectionHookTests` verify (a) the carrier rejects metadata containing forbidden secret-shaped substrings before serialization, (b) `Unprotected` / `Protected` / `ProviderOpaque` bytes and metadata stay coupled, (c) the existing `ToString()` redaction still applies after the extension dictionary is populated.
- **Out of scope, explicitly deferred.** Real encryption provider, key lifecycle (deletion/rotation/invalidation), missing-key behavior, crypto-shredding workflow, restored-backup safety, full operational redaction in Admin UI/CLI/MCP/ProblemDetails/backup tooling — all tracked for Stories 22.7b/22.7c/22.7d.

### File List

**Contracts**

- `src/Hexalith.EventStore.Contracts/Security/EventStorePayloadProtectionMetadata.cs` (new)
- `src/Hexalith.EventStore.Contracts/Security/EventStorePayloadProtectionMetadataCarrier.cs` (new)
- `src/Hexalith.EventStore.Contracts/Security/PayloadProtectionState.cs` (new)
- `src/Hexalith.EventStore.Contracts/Security/SnapshotProtectionResult.cs` (new)
- `src/Hexalith.EventStore.Contracts/Security/PayloadProtectionResult.cs` (modified — added `Metadata` field with backward-compatible constructor)
- `src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs` (modified — added metadata-aware event unprotect and typed snapshot methods as default interface methods)
- `src/Hexalith.EventStore.Contracts/Streams/StreamReadEvent.cs` (modified — added optional `ProtectionMetadata`)

**Server**

- `src/Hexalith.EventStore.Server/Events/NoOpEventPayloadProtectionService.cs` (modified — emits typed metadata)
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs` (modified — stamps protection extension, forwards `CancellationToken`)
- `src/Hexalith.EventStore.Server/Events/IEventPersister.cs` (modified — added optional `CancellationToken`)
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` (modified — metadata-aware unprotect, opaque passthrough, re-stamps published extensions)
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs` (modified — typed snapshot hook calls, metadata validation, legacy mapping)
- `src/Hexalith.EventStore.Server/Events/ISnapshotManager.cs` (modified — added optional `CancellationToken`)
- `src/Hexalith.EventStore.Server/Events/SnapshotRecord.cs` (modified — added optional `ProtectionMetadata`)

**Gateway**

- `src/Hexalith.EventStore/Controllers/StreamsController.cs` (modified — projects protection metadata into `StreamReadEvent`)

**Testing**

- `src/Hexalith.EventStore.Testing/Builders/EventEnvelopeBuilder.cs` (modified — `WithProtectionMetadata`)
- `src/Hexalith.EventStore.Testing/Builders/StreamReadPageBuilder.cs` (modified — `WithProtection`)
- `src/Hexalith.EventStore.Testing/Fakes/FakeSnapshotManager.cs` (modified — emits typed metadata)
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs` (modified — stamps no-op metadata, added optional `CancellationToken`)

**Tests**

- `tests/Hexalith.EventStore.Contracts.Tests/Security/EventStorePayloadProtectionMetadataTests.cs` (new — 28 tests)
- `tests/Hexalith.EventStore.Server.Tests/Security/PayloadProtectionHookTests.cs` (new — 13 tests)
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs` (modified — relaxed strict envelope equality to a predicate match that accommodates the new protection extension)

**Documentation**

- `docs/guides/payload-protection-and-crypto-shredding.md` (new)
- `docs/reference/api/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.Security.EventStorePayloadProtectionMetadata.md` (new)
- `docs/reference/api/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.Security.EventStorePayloadProtectionMetadataCarrier.md` (new)
- `docs/reference/api/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.Security.PayloadProtectionState.md` (new)
- `docs/reference/api/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.Security.SnapshotProtectionResult.md` (new)
- `docs/reference/api/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.Security.IEventPayloadProtectionService.md` (modified)
- `docs/reference/api/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.Security.PayloadProtectionResult.md` (modified)
- `docs/reference/api/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.Security.md` (modified)
- `docs/reference/api/index.md` (modified)
- `docs/reference/stream-replay-api.md` (modified — mentions new `protectionMetadata` field)
- `_bmad-output/implementation-artifacts/22-7a-payload-and-snapshot-protection-hooks.md` (this story file)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (status updated)

## Verification Status

- Story context created by BMAD pre-dev hardening automation on 2026-05-13.
- Code review patch pass 2026-05-18 (GPT-5 Codex): all 6 patch findings resolved. Validation: `dotnet test tests/Hexalith.EventStore.Contracts.Tests --filter EventStorePayloadProtectionMetadataTests` (28/28), `dotnet test tests/Hexalith.EventStore.Server.Tests --filter PayloadProtectionHookTests` (13/13), `dotnet test tests/Hexalith.EventStore.Testing.Tests` (112/112), `dotnet build Hexalith.EventStore.slnx --configuration Debug` (0 warnings, 0 errors), and `git diff --check` (line-ending warnings only).
- Implementation pass 2026-05-18 (Claude Opus 4.7 1M): ST0–ST5 complete. Focused validation evidence recorded in Debug Log References.

## Change Log

| Date | Change |
| --- | --- |
| 2026-05-13 | Story created from Epic 22.7a with hook/metadata-only scope, explicit 22.7b-22.7d deferrals, current implementation inventory, and focused test evidence requirements. |
| 2026-05-13 | Party-mode review applied metadata schema, legacy compatibility, publish-policy, snapshot parity, no-leak, and ATDD evidence clarifications. |
| 2026-05-13 | Advanced elicitation applied metadata state-machine, bytes/metadata invariant, provider-opaque, and key-alias sensitivity hardening. |
| 2026-05-18 | Implementation pass complete: new Contracts metadata types + carrier, EventPersister/EventPublisher/SnapshotManager hook integration, StreamReadEvent + Testing builder/fake exposure, payload-protection guide, sentinel-based no-leak tests. 26+12+regression-test updates pass; sprint status moved to review. |
| 2026-05-18 | Code review patches applied: snapshot metadata validation, unknown JSON field fail-closed handling, metadata-aware event unprotect overload, FakeEventPersister no-op metadata, API reference refresh, and provider-opaque docs correction. Story moved to done. |
