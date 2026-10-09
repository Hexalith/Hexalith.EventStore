---
title: 'Story 8.4: Compatibility Readers And Mixed-History Routing'
type: 'feature'
created: '2026-10-09'
status: 'in-progress'
baseline_commit: '75a08f0069d8c2495d9dff20a0deb84edb6cc638'
route: 'dispatch'
review_loop_iteration: 0
story_key: '8-4-compatibility-readers-and-mixed-history-routing'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 8.3 delivered the `pdenc-v2` core, but nothing decides, record by record, whether stored bytes are legacy, redacted, `json+pdenc-v1`, `json+pdenc-v2` or a mismatch. Without that decision, new protection could strand history or silently downgrade it. DW-519 is a concrete case: a plain `json` payload looks the same as one whose wrappers were stripped.

**Approach:** Add an internal compatibility router inside the non-packable core, as authority §12 defines it. For each event or snapshot it classifies the metadata carrier, the format and the bounded shape, then picks exactly one route. The v1 route uses a registered legacy reader and the v2 route uses the 8.3 core. A sequence read stops at the first unreadable record.

## Boundaries & Constraints

**Always:**
- Owner decisions on 2026-10-09 (sole owner, `single-maintainer-attested`):
  - Story 8.4 is authorized.
  - Scope is pragmatic: the evidence is unit tests plus the existing `payload-protection.yml` lane.
  - Edits stay inside the core project, the core test project, and this story's tracking files.
- Route each record independently, in the §12.1 order: carrier, then format and shape agreement, then route. A protected route authenticates before any plaintext is returned.
- Every locally decidable mismatch returns its typed reason without calling the key resolver or a legacy reader.
- `OperationCanceledException` propagates.
- Results and exceptions never carry plaintext, ciphertext, envelope or carrier text.
- The router has no write-mode, watermark or clock input.

**Never:**
- Do not change Contracts, frozen fixtures (`vector-ownership.json`, `manifest.json`), `Hexalith.EventStore.slnx`, `tools/release-packages.json`, `payload-protection.yml` or its pinned floor `324`.
- Do not touch Server, Client or domain code, or any file in the in-flight Story 6.6 working-tree changes.
- Out of scope:
  - Server read-path wiring (Story 8.7).
  - Key lifecycle store and resolver implementation (Story 8.5).
  - Azure (Story 8.6).
  - A Parties dependency or a reimplementation of v1 cryptography.
  - New telemetry emission.
  - Fuzz harnesses.
  - A sealed evidence packet or approval ceremony.
  - Any v2 write enablement.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Legacy / unprotected (V107–V109) | Carrier missing or `Unprotected`. Format `json` with valid bounded JSON, or a non-reserved custom format. No `$pdenc` or `$enc` shape. | Bytes are passed through unchanged, with route `LegacyUnprotected` or `Unprotected` and the format preserved. | A `$pdenc` or `$enc` shape gives `BytesMetadataMismatch`. |
| Redacted (V110) | Format `json-redacted`, no marker. | Route `Redacted`. The bytes and format are unchanged, and the payload is never re-protected. | A marker gives `BytesMetadataMismatch`. |
| v1 (V111–V112) | Exact Parties v1 metadata or a missing carrier, plus `json+pdenc-v1` and a `$enc` shape. | The registered `parties-pdenc-v1` reader decides the result. | No reader registered gives `ProviderOpaqueUnsupportedOperation`, and the payload is never treated as plaintext. |
| v2 (V113) | Exact §8.4 v2 allowlist metadata, plus `json+pdenc-v2` and at least one `$pdenc` wrapper. | The 8.3 core authenticates the payload. On success the output is `json` with `Unprotected()` metadata. | The core's typed reason is returned, and the output is never partial. |
| Opaque / carrier (V114–V115) | A reserved `json+pdenc-*` with an unknown version, an unknown protected scheme, or a malformed, duplicate, unknown or over-version carrier member. | `ProviderOpaqueUnsupportedOperation`, `MalformedMetadata` or `UnknownMetadataVersion`. | No resolver or reader is called. |
| Mixed stream (V119) | Legacy, redacted, v1 and v2 records in any order, one of them unreadable. | One decision: the first unreadable sequence and its reason. No later record is examined, and no partial list is returned. | The same input always gives the same result. |
| Snapshot (V117–V118) | Legacy, v1, or v2 `ProtectedSnapshotPayloadV2` with a registered or unknown type id. | A legacy snapshot passes through. v1 goes to the reader. v2 resolves the registered id or alias, authenticates, then deserializes through the registered `JsonTypeInfo`. | A protected unreadable snapshot returns no state and must be kept. Only a corrupt legacy snapshot may be deleted. |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.PayloadProtection/PayloadProtectionCore.cs`:
  - `TryUnprotectEventAsync` (:490) and `TryUnprotectSnapshotAsync` (:776) are the v2 route. Reuse them unchanged.
  - Their `keyResolver` delegate `Func<string,uint,CancellationToken,ValueTask<byte[]?>>` is the seam Story 8.5 will supply.
  - The core maps its own failures to reasons (:521–:728).
  - The snapshot method requires `SnapshotTypeId == context.PayloadTypeId` (:794).
- `src/Hexalith.EventStore.PayloadProtection/PayloadProtectionWireFormat.cs:12-15` has `json` and `json+pdenc-v2`. Add `json-redacted`, `json+pdenc-v1` and the reserved prefix `json+pdenc-` here.
- `src/Hexalith.EventStore.PayloadProtection/PayloadProtectionLimits.cs` and `BoundedJsonDocument.cs`:
  - Limits: 16 MiB, 65,536 nodes, depth 64.
  - `ContainsProtectedMember` detects `$pdenc`.
  - A `$enc` scan must stay within the same limits.
- `src/Hexalith.EventStore.PayloadProtection/CoreUnprotectionResult.cs` is the result shape that legacy readers return.
- `src/Hexalith.EventStore.Contracts/Security/` is used read-only:
  - `EventStorePayloadProtectionMetadataCarrier.Read(string?)` never throws. A missing carrier becomes `Legacy()`, and a parse failure becomes `ProviderOpaque(reason)`.
  - `UnreadableProtectedDataReasonMapper.FromProviderOpaqueMetadata` gives the opaque reason.
  - `EventStorePayloadProtectionMetadata.Unprotected()`, `UnreadableProtectedDataReason` and `ProtectedSnapshotPayloadV2` are used as-is.
- Exact metadata the router matches:
  - v2 allowlist (authority :627-644): `Protected`, version `1`, scheme `hexalith-pdenc-v2`, `KeyAlias` null, `ContentHint` `application/json`, and flags exactly `format=json+pdenc-v2` and `envelope=pdenc-v2`.
  - Parties v1 (:2364): scheme `parties-aes-gcm-json-fields` and flags `format=json+pdenc-v1` and `field-envelope=pdenc-v1`.
  - v1 snapshot wrapper: `{marker:"$protectedSnapshot",typeName,payload,serializationFormat:"json+pdenc-v1"}`.
- `tests/Hexalith.EventStore.PayloadProtection.Tests/TestFixture.cs` has G-001 `Context()`, `SnapshotContext()`, `Dek()`, `ProtectSnapshot` and `WrapperPayloadBytes`. Use these to build v2 inputs.
- `tests/Hexalith.EventStore.PayloadProtection.Tests/DiagnosticsTests.cs:604-648` pins `Trait("Vector")` to the 8.3 set. New tests carry V-numbers in their method names only, never as a `Vector` trait.
- `tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.cs:38-55` scans for credential-shaped literals. Keep key material in `TestFixture` helpers.

## Tasks & Acceptance

**Execution:**
- [ ] `src/Hexalith.EventStore.PayloadProtection/` -- Add these internal types, one per file:
  - The `CompatibilityReadRoute` enum.
  - `ILegacyPayloadReader`, with `ReaderId`, an event read and a snapshot read, each returning `CoreUnprotectionResult`.
  - The `CompatibilityEventRecord` and `CompatibilitySnapshotRecord` inputs.
  - The event, stream and snapshot read results.
  - `CompatibilityReaderCapability`.
  - `SnapshotTypeRegistration` and `SnapshotTypeRegistry`, where an id or alias that collides with another fails construction.
  - The pure `PayloadCompatibilityClassifier` and the async `PayloadCompatibilityRouter`.

  These implement the matrix and §12.2/12.3.
- [ ] `tests/Hexalith.EventStore.PayloadProtection.Tests/Compatibility{EventRouting,MixedHistory,SnapshotRouting}Tests.cs` -- Cover every matrix row:
  - The V107–V119 cases.
  - Zero resolver and reader calls on local mismatches.
  - Calls stop after the first unreadable record.
  - Cancellation.
  - A sentinel no-leak check on unreadable results and exceptions.
  - A fake v1 reader.
- [ ] `_bmad-output/implementation-artifacts/deferred-work.md` -- Mark DW-519 resolved by the format-aware route. Add one entry for Story 8.7: identity-history records (`json+identity-history-v1`) need an explicit route or must stay outside the router when it is wired in.

**Acceptance Criteria:**
- Given a registered reader set, when capability is read, then it lists the exact reader ids, formats and versions. A reader that is not registered is never advertised.
- Given a carrier with a duplicate member or an undefined numeric state, when it is classified, then the result is `MalformedMetadata`, even though `Carrier.Read` accepts it.
- Given the existing test lane, when it runs, then all prior cases still pass and the new cases run, with zero skips.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

- **v1 shape:** any JSON object that has a `$enc` member, found within core limits. The registered reader owns v1 detail.
- **Snapshot reasons:**
  - Unprotected metadata over a protected snapshot shape gives `BytesMetadataMismatch`, matching the NoOp path.
  - An unknown v2 type id, or a deserialization failure after authentication, gives `ConsistencyMismatch`.
- **Reader exceptions:** a legacy reader exception other than cancellation maps to `ProviderUnavailable`, matching how the core handles resolver exceptions.
- **Invalid JSON:** for a `json` or `json-redacted` record, invalid JSON gives `BytesMetadataMismatch`. A non-reserved custom format that is not JSON is passed through.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Release -m:1 -nodeReuse:false -p:UseHexalithProjectReferences=false -warnaserror` -- expected: 0 warnings and 0 errors, with the AOT/trim analyzers on.
- `dotnet tests/Hexalith.EventStore.PayloadProtection.Tests/bin/Release/net10.0/Hexalith.EventStore.PayloadProtection.Tests.dll -failSkips -noColor` -- expected: every test passes, at least 324 prior cases plus the new ones.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method "Hexalith.EventStore.Contracts.Tests.Packaging.ReleasePackageManifestTests.Payload_protection*" -failSkips -noColor` (build Contracts.Tests first) -- expected: pass, which confirms the lane and packability guards are intact.
