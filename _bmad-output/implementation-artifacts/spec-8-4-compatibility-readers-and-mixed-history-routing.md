---
title: 'Story 8.4: Compatibility Readers And Mixed-History Routing'
type: 'feature'
created: '2026-10-09'
status: 'done'
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
- [x] `src/Hexalith.EventStore.PayloadProtection/` -- Add these internal types, one per file:
  - The `CompatibilityReadRoute` enum.
  - `ILegacyPayloadReader`, with `ReaderId`, an event read and a snapshot read, each returning `CoreUnprotectionResult`.
  - The `CompatibilityEventRecord` and `CompatibilitySnapshotRecord` inputs.
  - The event, stream and snapshot read results.
  - `CompatibilityReaderCapability`.
  - `SnapshotTypeRegistration` and `SnapshotTypeRegistry`, where an id or alias that collides with another fails construction.
  - The pure `PayloadCompatibilityClassifier` and the async `PayloadCompatibilityRouter`.

  These implement the matrix and §12.2/12.3.
- [x] `tests/Hexalith.EventStore.PayloadProtection.Tests/Compatibility{EventRouting,MixedHistory,SnapshotRouting}Tests.cs` -- Cover every matrix row:
  - The V107–V119 cases.
  - Zero resolver and reader calls on local mismatches.
  - Calls stop after the first unreadable record.
  - Cancellation.
  - A sentinel no-leak check on unreadable results and exceptions.
  - A fake v1 reader.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- Mark DW-519 resolved by the format-aware route. Add one entry for Story 8.7: identity-history records (`json+identity-history-v1`) need an explicit route or must stay outside the router when it is wired in.

**Acceptance Criteria:**
- Given a registered reader set, when capability is read, then it lists the exact reader ids, formats and versions. A reader that is not registered is never advertised.
- Given a carrier with a duplicate member or an undefined numeric state, when it is classified, then the result is `MalformedMetadata`, even though `Carrier.Read` accepts it.
- Given the existing test lane, when it runs, then all prior cases still pass and the new cases run, with zero skips.

### Review Findings

- [x] [Review][Patch] Reject incomplete v2 snapshot carrier shapes before legacy pass-through [src/Hexalith.EventStore.PayloadProtection/PayloadCompatibilityClassifier.cs:513]
- [x] [Review][Patch] Validate all required v1 snapshot wrapper fields before calling the registered reader [src/Hexalith.EventStore.PayloadProtection/PayloadCompatibilityClassifier.cs:522]
- [x] [Review][Patch] Keep legacy-reader failure reasons inside the defined reason taxonomy [src/Hexalith.EventStore.PayloadProtection/PayloadCompatibilityRouter.cs:284]
- [x] [Review][Patch] Propagate caller cancellation when a legacy reader returns an unreadable result [src/Hexalith.EventStore.PayloadProtection/PayloadCompatibilityRouter.cs:284]
- [x] [Review][Patch] Recheck cancellation after v1 and v2 snapshot materialization [src/Hexalith.EventStore.PayloadProtection/PayloadCompatibilityRouter.cs:429]
- [x] [Review][Patch] Classify numeric future carrier versions beyond `Int32.MaxValue` as `UnknownMetadataVersion` [src/Hexalith.EventStore.PayloadProtection/PayloadCompatibilityClassifier.cs:410]
- [x] [Review][Patch] Verify that aborted streams zero earlier v2 plaintext buffers [tests/Hexalith.EventStore.PayloadProtection.Tests/CompatibilityMixedHistoryTests.cs:209]
- [x] [Review][Defer] Check stored legacy history against the new JSON bounds before Server wiring [src/Hexalith.EventStore.PayloadProtection/PayloadCompatibilityClassifier.cs:170] — deferred: no stored-data inventory is available; a scan for oversized, deep, duplicate-member or invalid-UTF-8 events and snapshots will establish whether replay or snapshot loads are affected. This is also recorded in the existing Story 8.4 deferred-work entry.

#### Rejected

- BH1 (`false`): Stream shape validation is an explicit precondition that the story requires before routing; it does not classify or read later payloads.
- BH2 (`low`): Augmented `Unprotected` metadata over plain bytes is permissive by the story's prior review decision; tightening it could strand existing foreign-writer history without detecting stripped ciphertext.
- BH8 (`false`): The advertised v2 capability describes the event reader, which works with an empty snapshot registry; snapshot type support is a separate concern.
- BH9 (`false`): Real Parties reader conformance is outside Story 8.4's authorized unit-test scope and belongs to the separately authorized consumer work.
- BH10 (`false`): The focused payload-protection test assembly now passes 589/589 with zero skips; the claim of unverified test success does not hold for this review.
- BH11 (`low`): The router has no Server consumer yet; buffer ownership is documented and successful-read cleanup belongs to the Story 8.7 call site.
- VG-other (`low`): The augmented `Unprotected` metadata case repeats BH2 and the prior review's deliberate compatibility decision.
- AA2 (`low`): The story explicitly treats blank carriers as legacy, including whitespace; changing the ceiling precedence needs an intent decision for a negligible case.
- EC5 (`false`): Snapshot input intentionally uses typed `ProtectionMetadata`, as the story's implementation notes state; no raw snapshot carrier is promised.

#### Pass 3 (2026-10-09)

Layers: blind hunter (BH), edge-case hunter (EC), verification gap (VG), acceptance auditor (AA). 33 raw findings: 3 patch, 2 defer, 16 rejected entries (25 raw findings). No decision is needed. The auditor confirmed all three acceptance criteria are met, with 618/618 tests passing.

- [x] [Review][Patch] Make snapshot plaintext zeroing observable and tested (VG1, BH7) [src/Hexalith.EventStore.PayloadProtection/PayloadCompatibilityRouter.cs:493]
  - Deleting `ZeroMemory(plaintext)` from the v2 snapshot `finally` passes every test, because no test can reach the buffer the core hands over.
  - The v1 snapshot failure rows (`json-null`, `wrapper-no-enc`) never inspect their returned buffers.
  - Fix:
    - Call `_bufferObserver?.BufferCleared(SensitiveBufferKind.DecryptedPlaintext, plaintext)` after the v2 clear, the same way `ClearOwnedPayloads` does.
    - Add a theory for the readable, deserialization-failure and cancellation outcomes that asserts a zeroed `DecryptedPlaintext` buffer was observed.
    - Make the two v1 failure rows assert that their buffers are zeroed.
- [x] [Review][Patch] Pin that a v1 snapshot wrapper also carrying `$pdenc` never reaches the reader (VG2) [src/Hexalith.EventStore.PayloadProtection/PayloadCompatibilityClassifier.cs:559]
  - Removing `!hasV2Wrapper &&` sends a mixed-marker wrapper to the registered reader, and no test fails.
  - Fix: add a `v1-wrapper-with-pdenc` shape under exact Parties v1 metadata to `V118_ProtectedMetadataShapeDisagreement_IsLocalDecisionAsync`. It should expect `BytesMetadataMismatch` and `SnapshotCalls == 0`.
- [x] [Review][Patch] Classify every over-version carrier as `UnknownMetadataVersion`, whatever its magnitude (AA3, EC2) [src/Hexalith.EventStore.PayloadProtection/PayloadCompatibilityClassifier.cs:433]
  - Today a version-2 carrier with an unknown member gives `MalformedMetadata`, because `Carrier.Read` reports `unknownField`. A version of 2^31 or more with the same member gives `UnknownMetadataVersion`.
  - Authority §12.2 maps "schema above current version" to `UnknownMetadataVersion`, and a newer schema may add members.
  - Fix:
    - Change `version > int.MaxValue` to `version > EventStorePayloadProtectionMetadata.CurrentMetadataVersion` and update the comment.
    - Add a version-2-plus-unknown-member row to `V115_OverVersionCarrier_IsUnknownMetadataVersionAsync`.
    - Every existing V115 row keeps its expected reason.
- [x] [Review][Defer] The 8.4 cases run only in the non-required `payload-protection` lane, and its floor of 324 cannot detect them disappearing [.github/workflows/payload-protection.yml:67] — deferred: pre-existing; this story's boundaries freeze the lane and its floor. The required-check half is already tracked by the Story 8.3 entry "Add `Payload Protection / payload-protection` to the protected branch's required status checks". The 357 cases that predate 8.4 already clear the floor.
- [x] [Review][Defer] Legacy `json`/`json-redacted` events and legacy snapshots beyond the core JSON bounds become `BytesMetadataMismatch` (EC5, EC7) [src/Hexalith.EventStore.PayloadProtection/PayloadCompatibilityClassifier.cs:177] — deferred: this reconfirms pass 1's BH6/EC10/EC11. The stored-data scan in the existing Story 8.4 deferred-work entry settles it; no new work item is needed.

##### Rejected (pass 3)

- BH1, AA2, EC1, EC12 (`low`): A carrier with no `metadataVersion` member reports `UnknownMetadataVersion`. That is unlikely to occur, because `Carrier.Serialize` always writes the non-nullable integer. Both reasons are permanent, retain the record and call nothing, and the fix would add a presence guard.
- BH2, EC10 (`low`): A respelled or v2-shaped object returned by the v1 snapshot reader would pass the router's output check.
  - The reader is host-registered, and the Parties reader returns deserialized domain state.
  - Reusing `TryInspectSnapshotState` there would also reject legitimate states.
- BH3, AA4 (`false`): Authority §12.3 row 2 prescribes `ConsistencyMismatch`/`BytesMetadataMismatch` for missing or unprotected metadata over a protected wrapper shape. The Parties-v1-metadata subcase is unreadable and retained either way.
- BH4, AA1, EC3, EC13 (`low`): The v2 snapshot read throws `PayloadProtectionCryptographicException` under invariant globalization.
  - No EventStore deployment enables invariant globalization: no `InvariantGlobalization` setting or chiseled image in the csproj files, Builds props or workflows.
  - In that mode, any `SnapshotTypeRegistry` with a registration already fails construction, so the throw is unreachable in a working configuration.
  - The fix would add a catch arm.
- EC4 (`low`): Under invariant globalization the registry constructor throws `PayloadProtectionCryptographicException`. That is a loud startup failure in an unsupported environment, consistent with V029's core write seams.
- BH5 (`false`): Persisted snapshots load through `TryGetStateAsync<SnapshotRecord>`, so `State` is a `JsonElement`. The restriction to `JsonElement` and `ProtectedSnapshotPayloadV2` is a documented decision, and Story 8.7 owns the wiring.
- BH6 (`low`): A custom-format record that fails the bounded parse passes through bytes that still hold ciphertext, so no plaintext leaks. That matches §12.2's "no detectable reserved wrapper" and the documented pass-through of unparseable custom formats. The fix would add a lenient scanner.
- BH7 remainder (`low`): The observer kind names (`AbandonedOutput` vs `DecryptedPlaintext`) are test-seam cosmetics. The core never returns bytes together with a reason, because `CoreUnprotectionResult` builds results only through `Readable` and `Unreadable`.
- BH8 (`low`): The registry accepts any `JsonTypeInfo`, and a misconfigured type reports `ConsistencyMismatch`. The type is developer-configured, and the fix would add validation surface.
- BH9, EC11 (`low`): v1 wrapper member names are matched case-sensitively.
  - Dapr actor state uses the default Web (camelCase) options, and `src` has no `ActorRuntimeOptions.JsonSerializerOptions` override. A stored Parties `ProtectedSnapshotState` is therefore camelCase.
  - A PascalCase wrapper still fails closed: it is detected as protected and retained.
- BH10 (`low`): Sequence 0 is accepted. Stored sequences start at 1, and pass-through only echoes the value; the fix would add a guard.
- BH11 (`low`): `OwnsPayload` is derived from the route. The type is internal and the router never builds an inconsistent result; the fix would add API.
- AA5 (`false`): Authority §12.2 row 1 defines missing metadata as `Legacy()`. Routing a stored `Legacy()` carrier like a missing one therefore matches "Missing legacy metadata plus `json+pdenc-v1` → same registered legacy reader", and the reader still authenticates.
- EC6 (`low`, carried): This is the same finding pass 2 rejected as BH3. No domain state in eventstore, tenants, parties or memories declares an `Envelope` or `SnapshotTypeId` member.
- EC8 (`low`, carried): This is the same finding pass 1 rejected as EC2.
- EC9 (`false`): `CompatibilityEventRecord` and the persisted `EventEnvelope` both declare `SerializationFormat` and `EventTypeName` non-nullable. Failing loudly on a contract violation is correct.
- VG3 (`low`): `ILegacyPayloadReader` returns a non-nullable `ValueTask<CoreUnprotectionResult>`, and the repo builds with `Nullable` and `TreatWarningsAsErrors`, so an in-repo reader cannot return `null`. The `(null, null)` fallback is a two-line defensive branch.

#### Pass 5 (2026-10-09)

Scope: the fix delta `42813102` only (pass-3 and pass-4 fixes, 211 diff lines). Layers: blind hunter (BH), edge-case hunter (EC), verification gap (VG), acceptance auditor (AA). 24 raw findings: 5 patch, 0 decision, 0 defer, 15 rejected. The auditor confirmed all three acceptance criteria still hold, with 625/625 tests passing and zero skips.

- [x] [Review][Patch] Pin cancellation after an unreadable v2 snapshot core result (VG1, BH7, AA1) [src/Hexalith.EventStore.PayloadProtection/PayloadCompatibilityRouter.cs:462]
  - Deleting the pass-4 `ThrowIfCancellationRequested()` passes every test. The only in-call v2 snapshot cancellation test cancels in the resolver, and the core rethrows that itself (`PayloadProtectionCore.cs:842`).
  - Fix: add a snapshot counterpart of `V113_CancellationAfterCoreCompletion_ClearsOutputAsync`. Use `core: new PayloadProtectionCore(observer)` with an observer that cancels when the `DataEncryptionKey` buffer is cleared, and a resolver that returns a wrong 32-byte key. Assert `OperationCanceledException`.
- [x] [Review][Patch] Pin the snapshot deserialization exception filter with a throwing converter (VG2, BH5, AA1) [src/Hexalith.EventStore.PayloadProtection/PayloadCompatibilityRouter.cs:477]
  - Nothing in the test project throws from inside `JsonSerializer.Deserialize`. Removing the pass-4 `OutOfMemoryException` arm, or the foreign-cancellation arm, passes every test.
  - Fix: add a converter in its own file that throws a supplied exception. Add rows to `V118_V2SnapshotPlaintext_IsZeroedForEveryOutcomeAsync`:
    - `OutOfMemoryException` propagates.
    - `OperationCanceledException` with an uncancelled caller token gives `ConsistencyMismatch`.
    - Both observe one zeroed `DecryptedPlaintext` buffer.
- [x] [Review][Patch] Pin the stream prevalidation cancellation check (VG3, AA1) [src/Hexalith.EventStore.PayloadProtection/PayloadCompatibilityRouter.cs:163]
  - Every stream test that passes a token cancels inside a reader callback, after prevalidation. With a valid list, the second loop throws anyway, so removing the pass-4 line is unobservable.
  - Fix: add `V119_PrevalidationObservesCancellationAsync`. Use a pre-cancelled token and `[V2Event(1), null]`. Assert `OperationCanceledException`, zero resolver calls and zero reader calls.
- [x] [Review][Patch] An empty stream with a cancelled token returns `Readable([])` (EC4, BH8) [src/Hexalith.EventStore.PayloadProtection/PayloadCompatibilityRouter.cs:160]
  - Neither loop body runs, so the cancellation is never observed. `ReadEventAsync` and `ReadSnapshotAsync` both throw for a pre-cancelled token.
  - Fix: call `cancellationToken.ThrowIfCancellationRequested()` once before the validation loop, and add an empty-list row to the test above.
- [x] [Review][Patch] Pin AC2 at metadata version 2 (AA3) [tests/Hexalith.EventStore.PayloadProtection.Tests/CompatibilityEventRoutingTests.cs:482]
  - Pass 3 lowered the `futureVersion` threshold from `int.MaxValue` to `CurrentMetadataVersion`, so every version-2 carrier now takes the new precedence.
  - Every undefined-numeric-state row uses `metadataVersion: 1`. Three reviewers in this pass proposed letting future state names win over malformation, and a change like that could also turn `{"metadataVersion":2,"state":"7"}` into `UnknownMetadataVersion`, against AC2.
  - Fix: add `{"metadataVersion":2,"state":"7"}` and `{"state":1,"metadataVersion":2}` rows to `V115_MalformedCarrier_IsMalformedMetadataWithoutCallsAsync`. They cannot go in `V115_DuplicateMemberOrUndefinedNumericState…`, because that test asserts `Carrier.Read` accepts the carrier, and a version-2 carrier is `ProviderOpaque` there.

##### Rejected (pass 5)

- AA2, EC2, BH2 (`low`, carried from pass 1 EC9): a version-2 carrier with a new state name, a duplicate flag, or depth above 8 is `MalformedMetadata`, not `UnknownMetadataVersion`.
  - No writer emits a version above 1, because `Carrier.Serialize` writes the current version.
  - Both reasons are permanent, retain the record and call nothing.
  - The fix would add a deferred-state flag, and AC2 still requires numeric states to stay malformed.
  - The inline comment at classifier `:431-432` documents the member precedence, and the `ClassifyCarrier` remarks do not claim to be exhaustive.
- EC3, BH3 (`low`): an over-version carrier holding a secret-shaped member is `UnknownMetadataVersion` instead of `MalformedMetadata`.
  - The result carries no carrier text, so nothing leaks.
  - No writer produces such a carrier.
  - The fix would add a forbidden-name scan branch.
- VG-other, BH4 (`low`): for a version-2 carrier with an unknown member, the unwired router reports `UnknownMetadataVersion` while the Server paths that call `Carrier.Read` report `MalformedMetadata`.
  - Story 8.7 replaces those paths with this router ("share one typed compatibility router"), so the divergence ends by construction.
  - Aligning the reader would edit Contracts, which this story's boundaries forbid.
- EC1, BH1 (`low`): an observer that throws in the v2 snapshot `finally` replaces the outcome.
  - `ISensitiveBufferObserver` is test-only, and production passes none.
  - The call runs after `ZeroMemory`, so no plaintext is left uncleared.
  - The router's existing calls (`:275`, `:360`) have the same unguarded shape.
  - The fix would add a guard.
- BH8, order part (`low`): with a cancelled token, an invalid stream list now throws `OperationCanceledException` before `ArgumentException`. That is the intended effect of pass-4 BH6, which keeps a long validation loop responsive. Restoring `ArgumentException` precedence would undo it.
- AA4 (`low`): the spec frontmatter says `done` while sprint-status says `review`. The status sync at the end of this review resolves it, and the finding's fix is an edit to the spec under review.
- BH6 (`false`): the v1 reader catch blocks map `OutOfMemoryException` to `ProviderUnavailable`, an availability class, not a corrupt-data reason. The design notes pin reader exceptions to `ProviderUnavailable`, matching the core's resolver catches (`PayloadProtectionCore.cs:602`, `:846`, `:964`).
- BH9 (`false`): the `partial` v1 snapshot row goes through the shared `AcceptLegacyPlaintext`. Its zeroing of output that still carries `$enc` is already pinned by `CompatibilityEventRoutingTests.cs:295`.
- BH10 (`false`): no harm is named.
  - The observer kind names are test-seam cosmetics, as in pass 3 BH7.
  - A v2 snapshot buffer discarded after deserialization fits "abandoned plaintext".
  - v1 snapshot buffers are asserted zeroed directly.
- BH11 (`false`): every plain format goes through one shared `TryInspectJson` call (classifier `:177`), so the new boundary row pins the node limit for all of them. Other tests pin the `Rejected` route for `BytesMetadataMismatch`.
- BH12 (`false`): nested `$pdenc` detection is covered by `V116_EscapedOrNestedMarker_IsDetectedInJsonAsync` and the `nested-pdenc` snapshot row. The duplicated converter setup names no harm.

#### Pass 7 (2026-10-09)

Scope: the pass-5 fix delta `9ebccd38` only (152 diff lines); the owner chose delta-only. Layers: blind hunter (BH), edge-case hunter (EC), verification gap (VG), acceptance auditor (AA). 13 raw findings: 0 decision, 4 patch, 0 defer, 6 rejected. VG reported no gaps. The auditor found no acceptance-criterion violation: Release build 0 warnings, 632/632 passing, zero skips. All four patches are test-only. Each was confirmed with a mutant that still passes all 632 tests.

- [x] [Review][Patch] Pin the in-loop stream prevalidation cancellation check (EC1, AA1, BH1) [src/Hexalith.EventStore.PayloadProtection/PayloadCompatibilityRouter.cs:164]
  - The pass-5 empty-stream fix added a pre-loop check at `:161`. It throws first for any pre-cancelled token, so `V119_PrevalidationObservesCancellationAsync` never reaches the pass-4 line. Deleting `:164` passes every test, so pass-5 VG3 is checked off but not achieved.
  - Fix: add a test-only `IReadOnlyList<CompatibilityEventRecord>`, in its own file, whose indexer cancels a supplied source when element 0 is read. Use `[V2Event(1), null]` with an uncancelled token. Assert `OperationCanceledException` (without `:164` the result is `ArgumentException`), zero resolver calls and zero reader calls.
  - Keep `:164`. Pass-4 BH6 and pass-5 BH8 record mid-loop responsiveness as intended.
- [x] [Review][Patch] Make the `foreign-cancellation` row model a foreign cancellation (AA2, BH5) [tests/Hexalith.EventStore.PayloadProtection.Tests/CompatibilitySnapshotRoutingTests.cs:164]
  - The pass-5 VG2 fix specified an uncancelled caller token. Instead, the row throws a token-less `OperationCanceledException` and calls `ReadSnapshotAsync(record)` with `CancellationToken.None`.
  - Two filter mutants at router `:479` pass every test: `!cancellationToken.CanBeCanceled`, and checking the exception's own token. Once Story 8.7 passes live request tokens, the first would report a converter's foreign cancellation as caller cancellation instead of `ConsistencyMismatch`.
  - Fix: throw `new OperationCanceledException(foreign.Token)` from an already-cancelled foreign source, and call the router with `source.Token` left uncancelled. Keep asserting `ConsistencyMismatch` and one zeroed `DecryptedPlaintext` buffer.
- [x] [Review][Patch] Pin cancellation after an unreadable v2 event core result (BH3) [src/Hexalith.EventStore.PayloadProtection/PayloadCompatibilityRouter.cs:365]
  - Pass 2 added an `else { cancellationToken.ThrowIfCancellationRequested(); }` branch in `ReadSharedV2EventAsync`, and no test covers it. `V113_CancellationAfterCoreCompletion_ClearsOutputAsync` uses the correct key, so it reaches only the readable branch. Deleting the `else` block passes every test.
  - This is the event-side twin of the pass-5 snapshot patch.
  - Fix: add `V113_CancellationAfterUnreadableCoreCompletion_PropagatesAsync`, modelled on `V118_CancellationAfterUnreadableV2CoreCompletion_PropagatesAsync`: a resolver that returns a wrong 32-byte key, and a core observer that cancels when the `DataEncryptionKey` buffer is cleared. Assert `OperationCanceledException`, one resolver call, and an empty router observer (no `AbandonedOutput`).
- [x] [Review][Patch] Pin the typed snapshot metadata precedence at metadata version 2 (BH7) [tests/Hexalith.EventStore.PayloadProtection.Tests/CompatibilitySnapshotRoutingTests.cs:461]
  - The pass-5 version-2 rows cover only the raw event carrier. In the typed `ClassifyMetadata` path (classifier `:87-95`), swapping the undefined-state and over-version checks passes every test. After that swap, an undefined-state version-2 snapshot would report `UnknownMetadataVersion` while the matching event reports `MalformedMetadata`.
  - Fix: add an `undefined-state-over-version` row to `V118_SnapshotMetadataFailure_IsRejectedWithoutCallsAsync`, using `v2 with { State = (PayloadProtectionState)7, MetadataVersion = 2 }` and expecting `MalformedMetadata`.

##### Rejected (pass 7)

- EC2, BH4 (`false`): `V118_CancellationAfterUnreadableV2CoreCompletion_PropagatesAsync` does not need to assert that it took the unreadable path. Its zero key differs from the frozen G-001 `TestFixture.Dek()` (bytes 0–31), so decryption cannot succeed. Deleting the router check at `:463` fails exactly this test.
- EC3 (`low`): pass-5 VG3 is checked off although its target is unpinned, and the spec says `done` while sprint-status says `review`.
  - The unpinned check is the first patch above.
  - Both fixes would edit the spec under review, and this review's status sync settles the mismatch.
- BH2 (`false`, carried from pass-5 BH8): stream cancellation ranks above element validation by design (pass-4 BH6). The delta only extends that rule to empty lists.
- BH6 (`false`): the filter's caller-cancellation clause is an equivalent mutant, not a defect. Without the clause, `:484` still turns a swallowed caller cancellation into `OperationCanceledException`. The clause only preserves the original exception instance.
- BH1, `:189` part (`false`): the stream loop's check duplicates `ReadEventAsync`'s own check at `:117`. It is redundant but harmless, and it predates the delta.
- BH8 (`false`): the documentation claims do not hold.
  - Neither the router nor the core documents `OperationCanceledException` or `OutOfMemoryException` on any method.
  - The "one bounded unreadable reason" wording on `ReadSnapshotAsync` already covers `ConsistencyMismatch`.
  - The summary of `V119_EmptyStream_IsReadableAsync` accurately describes its uncancelled scenario.

#### Pass 9 (2026-10-10)

Scope: the pass-7 fix delta `bc3a8dd8` only (`src/` and `tests/`, 181 diff lines, test-only). Pass 8 was build-internal. Layers: blind hunter (BH), edge-case hunter (EC), verification gap (VG), acceptance auditor (AA). 15 raw findings: 0 decision, 0 patch, 1 defer, 13 rejected. The auditor found no acceptance-criterion violation: Release build 0 warnings, 635/635 passing, zero skips. Three reviewers ran mutants independently. Each of the four pass-7 survivors (router `:164`, `:365`, both `:479` filter mutants, and the classifier `:87`/`:92` swap) now fails exactly its intended new test, as do token-less throws at `:164` and `:367`.

- [x] [Review][Defer] The v1 legacy-reader foreign-cancellation filters are still tested with a token-less exception and `CancellationToken.None` (VG1, BH1) [src/Hexalith.EventStore.PayloadProtection/PayloadCompatibilityRouter.cs:401] — deferred: pre-existing and outside the ACs. `V111_ReaderForeignCancellation_MapsToProviderUnavailableAsync` predates the delta, and no v1 snapshot test throws a foreign cancellation. At `:401` the mutants `when (cancellationToken.CanBeCanceled)` and `when (oce.CancellationToken.IsCancellationRequested)` pass 635/635; at `:519` those two mutants and removing the filter also pass. The router has no production consumer yet. Story 8.7, which first passes live request tokens, owns the two pins (see deferred-work).

##### Rejected (pass 9)

- BH2, BH3 (`low`): four older cancellation tests accept any `OperationCanceledException`. They are `V118_CancellationAfterUnreadableV2CoreCompletion_PropagatesAsync`, `V113_CancellationAfterCoreCompletion_ClearsOutputAsync`, the `cancellation` row of `V118_V2SnapshotPlaintext_IsZeroedForEveryOutcomeAsync`, and the pre-cancelled `V119_PrevalidationObservesCancellationAsync`.
  - Every one of those router sites throws through `cancellationToken.ThrowIfCancellationRequested()`. A regression to a token-less throw changes only the token carried, and no caller compares it.
  - Sweeping the assertion across older tests extends coverage rather than correcting a defect.
- EC1, BH5 (`low`): when the caller is already cancelled and a v1 reader or converter raises its own `OperationCanceledException`, it propagates with the component's token. The rethrow happens at `:401` and `:519` (`throw;`), and at `:479`, where the filter is false.
  - The caller still receives cancellation, so only the token's identity differs.
  - Normalizing it would add catch branches for a rare race.
- EC2 (`low`, pre-existing): `ReadStreamAsync` re-reads `records[index]` after prevalidation, so a non-idempotent `IReadOnlyList` indexer could route records that skipped validation.
  - The method is internal. Its only future caller (Story 8.7) passes materialized history, and this is not a trust boundary.
  - A defensive copy adds an allocation per stream.
- EC3 (`low`): replacing the token-less row with the foreign-token row drops the token-less case. Both map to `ConsistencyMismatch` today, and only a contrived filter would treat them differently. Both realistic filter mutants die.
- BH4 (`low`): `V113_CancellationAfterUnreadableCoreCompletion_PropagatesAsync`, and its pass-5 snapshot twin, reach the router branch only because the core clears the DEK after `CheckFailureCancellation`. A core refactor that reorders that cleanup would make them vacuous without failing. That needs a core change, and the proposed `MeterListener` precondition adds test machinery.
- BH6 (`false`): the new `AllowsCorruptLegacyDeletion.ShouldBeFalse()` is not inert. It pins the spec rule that an unreadable protected snapshot must be kept against a change to the property's definition (`CompatibilitySnapshotReadResult.cs:33-34`). That it cannot fail on correct code is true of every passing assertion.
- BH7 (`false`): the `empty: false` row of `V119_PrevalidationObservesCancellationAsync` is not redundant. Its pre-cancelled token plus a null element is the only case that pins cancellation precedence over element validation (pass-4 BH6, pass-5 BH8); the empty row has no elements to validate. Its summary is accurate.
- BH8, AA1, AA2 (`low`): the spec says `done` while sprint-status says `review`. The pass-8 patches appear only in the pass-8 triage table, and pass-8 BH6 ("no post-fix mutation run") is now stale. Each fix edits the spec under review. This review's status sync settles the mismatch, and this section records the mutation evidence.
- VG-other (`false`): a scratch-folder collision during the review invalidated one reviewer's intermediate mutation log. That is not a defect in the diff, and the VG and BH clean-copy runs independently reproduce every mutant result above.

## Implementation Notes

- 2026-10-09: Added the listed internal types, one per file, plus a `CompatibilityClassification` helper record that carries the pure classifier output. Other core changes:
  - `PayloadProtectionWireFormat` gained `json-redacted`, `json+pdenc-v1` and the `json+pdenc-` prefix.
  - `BoundedJsonDocument` gained `ContainsLegacyProtectedMember`. The `$enc` scan runs in the same bounded parse as `$pdenc`, so it shares the 16 MiB, 65,536-node and depth-64 limits, strict UTF-8 and unique member names.
  - `AadCodec.ValidateSnapshotTypeId` is now `internal`, so the registry reuses the §6.1 grammar.
  - `TryUnprotectEventAsync` and `TryUnprotectSnapshotAsync` are unchanged.
- Carrier strictness: before `Carrier.Read` runs, these return `MalformedMetadata`:
  - a duplicate top-level or flag member;
  - any `state` spelling other than an exact defined name (numeric, comma-list or padded);
  - `metadataVersion` below 1, which `Carrier.Read` would otherwise report as an unknown version;
  - carrier text longer than 65,536 characters.

  A blank carrier stays legacy, matching `Carrier.Read`. A stored carrier equal to `Legacy()` keeps the legacy route.
- Allowlists:
  - v2 metadata is the exact §8.4 allowlist.
  - Parties v1 metadata requires the exact scheme and exactly the two flags; `KeyAlias` and `ContentHint` are left to the reader.
  - A known scheme outside its allowlist is `MalformedMetadata`. An unknown protected scheme is `ProviderOpaqueUnsupportedOperation`.
- Reserved formats: any format starting with `json+pdenc-`, containing `+pdenc-`, or starting with `protected+` is reserved, ignoring case. These are the markers the no-op provider already refuses. Only exact `json+pdenc-v1` and `json+pdenc-v2` are routable; every other reserved spelling is `ProviderOpaqueUnsupportedOperation` under any metadata.
- Legacy readers: only reader id `parties-pdenc-v1` is accepted. An unknown, respelled, duplicate or null reader fails router construction.
  - Reader output is checked before it is returned. A reader that returns the stored buffer gets `ConsistencyMismatch`. So does output that is not bounded JSON or still carries `$enc` or `$pdenc`; that output is zeroed.
  - A v1 snapshot is returned as a `JsonElement`.
- Snapshot input:
  - State must be the stored `JsonElement` or an in-process `ProtectedSnapshotPayloadV2`. Any other type is an `ArgumentException`.
  - The three v2 carrier member names match ignoring case, because Dapr actor state stores them in camelCase (`JsonSerializerOptions.Web`). The exactly-three-members rule still applies.
  - Snapshot metadata is the typed `EventStorePayloadProtectionMetadata?` that `SnapshotRecord` stores, not a carrier string.
  - The v2 AAD type id is the stored id, which may be an alias. Unknown ids, and authenticated plaintext that fails to deserialize or deserializes to null, give `ConsistencyMismatch`.
  - `AllowsCorruptLegacyDeletion` is true only for readable legacy or unprotected pass-through.
- Streams:
  - Records must belong to one aggregate in contiguous ascending sequence: each record's sequence is its predecessor's plus one. A violation, including a gap, is an `ArgumentException`, raised before any routing.
  - On the first unreadable record, the router zeroes the decrypted plaintext it produced for earlier v1/v2 records. Pass-through buffers belong to the caller and are never zeroed.
- Risk: legacy `json` and `json-redacted` payloads must be valid bounded JSON as §8 defines it. Stored legacy JSON with duplicate member names, more than 65,536 nodes, depth above 64 or invalid UTF-8 is now `BytesMetadataMismatch`. Custom formats that cannot be parsed still pass through.
- Concurrent commit `aaf4a5e0` (`fix: eventstore`, 15:19 +02:00) absorbed the in-progress core source files during implementation. That history was left untouched; the later classifier edits remain in the working tree.
- 2026-10-09 review fixes: case-insensitive v2 snapshot member names; contiguous stream sequences; only caller cancellation escapes snapshot deserialization; registry exceptions name `registrations`; stronger oversized-carrier, classification-order, cancellation-zeroing and `OwnsPayload` tests; corrected DW-519 and DW-544 text. Focused run of the three compatibility test classes: 232 passed, 0 failed, 0 skipped. Full-suite verification is run by the coordinator.
- 2026-10-09 follow-up review fixes: incomplete v2 snapshot carrier members and incomplete v1 wrapper fields fail locally; large positive integer carrier versions report `UnknownMetadataVersion`; legacy-reader results preserve the bounded reason taxonomy and caller cancellation; snapshot materialization rechecks cancellation. The router uses the core's cleared-buffer observer contract for abandoned stream output, including v2. Release build: 0 warnings, 0 errors. Payload-protection assembly: 609 passed, 0 failed, 0 skipped. Contracts package-manifest lane: 13 passed, 0 failed, 0 skipped.
- 2026-10-09 review pass 2 fixes: writer-produced v2 events with an unrelated `$enc` property authenticate; malformed v2 snapshot IDs return `BytesMetadataMismatch`; v1 snapshot reader output cannot be an undecrypted wrapper or JSON null; v2 event output is cleared when cancellation arrives during core cleanup; registry aliases are frozen. The cancellation test converter lives in its own C# file. Final Release build: 0 warnings, 0 errors. Payload-protection assembly: 618 passed, 0 failed, 0 skipped. Invariant-globalization V029: 1 passed. Contracts package-manifest lane: 13 passed, 0 failed, 0 skipped.
- 2026-10-09 review pass 3 fixes: v2 snapshot plaintext clear is observed after readable, deserialization-failure, and caller-cancellation outcomes; both v1 snapshot failure buffers are asserted zeroed; a v1 wrapper also carrying `$pdenc` is rejected locally; and future carrier versions take precedence over unknown schema members. Release build: 0 warnings, 0 errors. Payload-protection assembly: 623 passed, 0 failed, 0 skipped. Contracts package-manifest lane: 13 passed, 0 failed, 0 skipped.
- 2026-10-09 review pass 4 fixes: stream prevalidation checks cancellation; failed v2 snapshot reads recheck cancellation after core cleanup; snapshot deserialization propagates `OutOfMemoryException`; and the legacy event router test covers the 65,536/65,537-node boundary. Release builds: 0 warnings, 0 errors. Payload-protection assembly: 625 passed, 0 failed, 0 skipped. Contracts package-manifest lane: 13 passed, 0 failed, 0 skipped.
- 2026-10-09 review pass 5 fixes: cancelled empty streams now throw; tests pin stream prevalidation, cancellation after an unreadable v2 snapshot core result, snapshot deserialization exception handling and plaintext clearing, and malformed numeric states at metadata version 2. Release builds: 0 warnings, 0 errors. Payload-protection assembly: 632 passed, 0 failed, 0 skipped. Contracts package-manifest lane: 13 passed, 0 failed, 0 skipped.
- 2026-10-09 review pass 7 fixes: tests pin cancellation raised during stream prevalidation, a foreign cancellation token during snapshot deserialization, cancellation after an unreadable v2 event core result, and undefined snapshot state at metadata version 2. Release builds: 0 warnings, 0 errors. Payload-protection assembly: 635 passed, 0 failed, 0 skipped. Contracts package-manifest lane: 13 passed, 0 failed, 0 skipped.
- Verification (before the review fixes): Release build with `-warnaserror` and the AOT/trim analyzers: 0 warnings, 0 errors. Test assembly: 575 passed, 0 failed, 0 skipped (357 prior plus 218 new). `ReleasePackageManifestTests.Payload_protection*`: 13/13 passed. The invariant-globalization V029 lane: 1/1 passed. Mutation checks: 18 mutants, each failed at least one new test.

## Spec Change Log

## Review Triage Log

Pass 1 (2026-10-09). Layers: blind hunter (BH), edge-case hunter (EC) and verification gap (VG). Severities assigned by reviewers were ignored. Routes: P = patch, D = defer, R = reject.

| ID | Finding | Verdict | Evidence | Route |
|---|---|---|---|---|
| BH1, EC1 | A v2 snapshot read back from Dapr actor state has camelCase members, but `TryInspectSnapshotState` matches only PascalCase. | high | `AggregateActor.cs:111-114` falls back to `JsonSerializerOptions.Web`. The existing `NoOpEventPayloadProtectionService.IsProtectedSnapshot` matches `format` with `OrdinalIgnoreCase`. Authority §6.1 requires the "JsonElement object shape after DAPR object deserialization". The `camel-case-v2` test rows pin a mismatch. | P1 |
| BH2, VG-other | The reviewed diff lacks the `metadataVersion < 1` guard, so a V115 row fails. | false | The diff was captured at 15:32:59, inside the 15:33:12 autostash rebase window of a concurrent session. The worktree has the guard (`PayloadCompatibilityClassifier.cs:412`), and the diff from before the race is byte-identical to the current tree. 575/575 pass. | R |
| BH3, EC3 | `ReadStreamAsync` accepts sequence gaps such as 1, 2, 4. | low | `PayloadCompatibilityRouter.cs:167` checks only `<=`. §12.1 says "do not skip", and the existing `RetainedIdentityHistorySourceReader.cs:89` enforces `+1` contiguity. The fix is a direct correction. | P2 |
| BH4, EC8 | Any `Unprotected`-state metadata, including metadata with v2 scheme or flags, routes to plaintext pass-through. | low | `TryValidate` accepts these fields under Unprotected. The pass-through returns the stored bytes, which contain no wrapper and therefore no ciphertext. The only harmful case is stripped wrappers, which an unauthenticated carrier already cannot reveal (DW-519 residual). Requiring exact metadata could strand foreign writers' history; that is a policy choice, not a direct correction. | R |
| BH5a | The DW-519 resolution omits Story 8.7's writer obligation: a zero-wrapper protect must persist a missing or `Unprotected()` carrier. | low | The router rejects `json` under v2 metadata per §12.2, and `CoreProtectionResult` carries no metadata. A text-only fix in this story's own entry. | P8 |
| BH5b | The DW-519 residual ("storage-integrity controls") names no owner. | low | Cosmetic, and fixed in the same text as P8. | P8 |
| BH6, EC10, EC11 | Legacy `json`/`json-redacted` events and legacy snapshots beyond the core bounds become `BytesMetadataMismatch`, and those snapshots are never deletable. | medium (unverified) | This is the behaviour the epics AC requires ("valid bounded bytes"). Whether stored history exceeds the bounds is pre-existing and unknown; a scan of stored data before 8.7 wiring would settle it. | D1 |
| BH7 | `Cancellation_PropagatesFromEveryRouteAsync` uses a pre-cancelled token, so it never reaches the route-level handling. | low | `Cancellation_DuringReaderOrResolverPropagatesAsync` covers in-call propagation. A regression is unlikely, and the fix is extra test rows. | R |
| BH8, VG3 | Plaintext zeroing on a stopped or cancelled stream is tested only for v1. | low | Pre-verified by VG: mutants A (no `finally`) and B (`OwnsPayload` v1-only) survive. | P5 |
| BH9 | The registry rejects two ids that share one CLR type, and has no registry version. | low | The authority lists the failure cases but does not require shared CLR types, and the 8.7 writer's type-to-id lookup needs uniqueness. The ids are themselves versioned (`hx-snapshot-v1:`). | R |
| BH10 | `SnapshotTypeRegistry.Add` throws with `nameof(registration)` instead of `registrations`. | low | `SnapshotTypeRegistry.cs:80,85`. A direct correction. | P7 |
| BH11 | `ReadStreamAsync` has no memory bound and no public clean-up. | low | All-or-nothing is the spec's contract, and Server replay already holds every event. Clearing owned buffers is a loop over `OwnsPayload` at the 8.7 call sites. The fix would add API. | R |
| BH12 | Every read pays a copy and parse, and v2 is parsed twice. | low | Per-record shape classification is mandated by §12.1. No performance requirement applies. | R |
| BH13 | Respelled plaintext formats (`JSON-REDACTED`, `Json`) pass through as custom. | low | Formats come from code constants, and the spec requires exact `json-redacted`. | R |
| BH14, EC12 | `Capabilities` does not advertise custom-format pass-through. | low | Custom formats are open-ended, and the XML doc states the behaviour. Listing them would invent a capability model. | R |
| EC2 | Mutating the caller's list during the awaits bypasses validation. | low | This is an internal API, and concurrent mutation of an input list is a caller bug. The fix adds a defensive copy. | R |
| EC4 | A foreign `OperationCanceledException` from a `JsonTypeInfo` converter escapes. | low | `PayloadCompatibilityRouter.cs` catch filter `when (exception is not OperationCanceledException)`. The legacy reader path already maps foreign cancellation. A direct correction. | P6 |
| EC5 | A legacy reader can return an undefined reason value. | low | Readers are trusted, host-registered components. The fix adds a guard. | R |
| EC6 | A disposed `JsonElement` throws `ObjectDisposedException`. | false | That input is a caller bug, and failing loudly is correct for that state. | R |
| EC7 | `ValidateSnapshotTypeId` can throw `PayloadProtectionCryptographicException` (`CanonicalText.cs:48`). | low | Startup fails loudly either way; only the exception type differs. | R |
| EC9 | A carrier with schema version 2 and a new member or state gives `MalformedMetadata`, not `UnknownMetadataVersion`. | low | No newer-schema writer exists, and both reasons are permanent. Reordering is more than a direct correction. | R |
| EC13 | The DW-544 text misstates identity-history failure reasons. | low | Exact v2/v1 carriers give `BytesMetadataMismatch`, and a non-allowlisted known scheme gives `MalformedMetadata`. A text-only fix. | P8 |
| VG1 | The oversized-carrier test cannot detect removal of the 65,536-character limit. | low | Pre-verified mutant: the test's 65,536-character `scheme` is already rejected by `TryValidate`. | P3 |
| VG2 | No test checks that the carrier and metadata are classified before format and shape. | low | Pre-verified: both order-swap mutants survive. | P4 |

Pass 2 (2026-10-09). The verification-gap layer found no gaps. The findings below were checked against the current classifier, router, core, tests, and the earlier triage rows.

| ID | Finding | Verdict | Evidence | Route |
|---|---|---|---|---|
| BH1 | A v2 event with an unrelated `$enc` property is rejected. | medium | `ProtectEvent` rejects `$pdenc` input but permits `$enc`; `ClassifyEvent` requires no `$enc` even after it finds the required `$pdenc`. A writer-produced record can fail before authentication. | patch |
| BH2 | A plain historical `$enc` field may fail the marker scan. | medium (unverified) | The approved matrix deliberately treats `$enc` as a protected marker. Whether stored plain history contains that field is unknown; a stored-data inventory before 8.7 wiring would settle the replay impact. | defer |
| BH3 | A lone `Envelope` or `SnapshotTypeId` domain field is now treated as a protected shape. | low | The follow-up guard treats those reserved carrier members as evidence of an incomplete v2 wrapper. No affected stored state is known, and distinguishing it from a stripped wrapper would require more policy and shape rules. | reject |
| BH4 | A legacy snapshot outside the new JSON bounds remains retained on failed reads. | medium (unverified) | carried: the earlier BH6/EC10/EC11 row and deferred inventory already cover the same behavior; the router still returns an unreadable result whose deletion flag is false. | defer (carried) |
| BH5 | A malformed v2 snapshot type id returns `ConsistencyMismatch`. | medium | Registry lookup precedes the core's closed-id validation, so an empty or invalid id is misreported as unknown. The normative malformed-ID case requires `BytesMetadataMismatch`. | patch |
| BH6 | An in-process v2 carrier can bring an oversized type id to registry lookup. | low | `ClassifySnapshot` accepts the typed carrier without checking the closed 16–128 byte id grammar. The same local validation as BH5 resolves this before lookup. | patch |
| BH7 | A v1 wrapper with extra fields reaches its registered reader. | false | The v1 reader owns its wrapper details; the approved matrix requires the marker, format, type and payload but does not forbid extensions. No failing behavior was shown. | reject |
| BH8 | Successful streams retain decrypted buffers without a cumulative bound. | low | carried: the earlier BH11 row rejects an API expansion; all-or-nothing stream output and caller cleanup at Story 8.7 are documented. | reject (carried) |
| BH9 | A caller can mutate registered snapshot aliases after construction. | low | The registry retains the original registration and alias list while lookup keys are fixed. A future caller inspecting `Registrations` could see a list that disagrees with resolution. | patch |
| BH10 | The leak assertion misses a sentinel encoded as hex or Base64 text. | low | `ShouldNotLeak` renders bytes in those encodings but compares each rendering only with the raw sentinel. An encoded sentinel inside a safe-looking string escapes this test. | patch |
| BH11 | The legacy bounds scan has a duplicate, untracked ledger line. | low | The structured deferred entry is followed by a second bullet with the same scan and no work id. Removing the duplicate preserves the tracked entry. | patch |
| BH12 | A real Parties v1 reader conformance test is absent. | low | carried: the spec's prior BH9 decision assigns consumer conformance to later authorized work; this story uses a fake registered reader and unit-test evidence. | reject (carried) |
| EC1 | A v2 event read can return plaintext after cancellation lands in the core's finalizer. | medium | The core checks cancellation before its finalizer, then clears buffers and returns; the router does not recheck the token before returning the result. The observer seam can reproduce that timing. | patch |
| EC2 | A v1 reader can return its undecrypted snapshot wrapper as readable state. | medium | The event path rejects returned `$enc`, but the historical snapshot wrapper has a `marker` and opaque payload string with no `$enc`; `ReadRegisteredV1SnapshotAsync` currently clones and returns it. | patch |
| EC3 | A v1 reader can return JSON `null` as a readable snapshot. | medium | `JsonElement.Null` is a non-null boxed object, so `CompatibilitySnapshotReadResult.IsReadable` becomes true despite there being no state. | patch |

### Pass 4 review triage (2026-10-09)

The blind hunter (BH), edge-case hunter (EC), and verification-gap reviewer (VG) reviewed the Story 8.4 diff. Each claim was checked against the classifier, router, tests, authority, and prior triage.

| ID | Verdict | Evidence | Route |
|---|---|---|---|
| BH1 | low | An extra `Envelope` or `SnapshotTypeId` can accompany a valid v1 wrapper, but the registered Parties reader owns v1 wrapper details. This repeats pass 1 BH9 and pass 2 BH7; rejecting extensions would add an unapproved shape rule. | reject (carried) |
| BH2 | low | A v1 snapshot wrapper with `json+pdenc-v3` receives `BytesMetadataMismatch` under exact v1 metadata. It remains unreadable and retained without a reader call; distinguishing future snapshot formats would add a new classification branch. | reject |
| BH3 | medium | The core can return an unreadable v2 snapshot after caller cancellation during its final cleanup, and the router returns the reason without a token check. | patch |
| BH4 | low | A trusted event reader can return a snapshot wrapper lacking `$enc` or `$pdenc`, but rejecting a snapshot-named field in event plaintext could reject legitimate domain JSON. The registered reader's authenticated-output contract owns this case. | reject |
| BH5 | low | A trusted v1 snapshot reader can return a v2-shaped object without the v1 marker. Pass 3 BH2/EC10 already considered this host-reader contract and rejected a broader state scan. | reject (carried) |
| BH6 | low | Stream shape prevalidation loops over a caller list without checking cancellation. A direct token check makes a large validation loop responsive. | patch |
| BH7 | medium | `JsonSerializer.Deserialize` can throw `OutOfMemoryException`, which the broad catch maps to a corrupt-state `ConsistencyMismatch`. The exception should propagate as a resource failure. | patch |
| BH8 | low | The second v1 snapshot parse does not accept a token, but the input is bounded and cancellation is checked after materialization. Avoiding the parse requires changing the validation and clone path. | reject |
| BH9 | low | Custom formats are pass-through but not listed individually in the finite capability list. Pass 1 BH14/EC12 already rejected a wildcard capability; the API documentation states this behavior. | reject (carried) |
| BH10 | false | Bounded JSON parsing checks the token repeatedly and near completion, so cancellation during the costly scan propagates. A cancellation racing after the final check cannot be guaranteed by an extra return-site check. | reject |
| EC1 | low | A mutable caller list can be changed across awaits, but this is the same internal caller-mutation defect rejected in pass 1 EC2. No Server consumer exists yet; the caller must keep the input stable. | reject (carried) |
| VG1 | low | Direct parser tests cover the 65,537-node boundary, but no router test would catch dropping the node limit from event classification. | patch |
| VG-other | medium | The failed v2 snapshot read returns without rechecking caller cancellation after core cleanup. This shares BH3's root cause. | patch (with BH3) |

### Pass 6 review triage (2026-10-09)

The blind hunter (BH), edge-case hunter (EC), and verification-gap reviewer (VG) reviewed the cumulative Story 8.4 diff. The prior triage and current code settle every finding; no new patch or deferred entry is needed. Each row preserves the reviewer's separate claim.

| ID | Verdict | Evidence | Route |
|---|---|---|---|
| BH1 | low | DW-519's original core routing ambiguity is resolved, and its status explicitly names the remaining unauthenticated-carrier rewrite for Story 8.7. Changing the status or spec would not change the behavior. | reject |
| BH2 | medium | Carried from DW-544: `json+identity-history-v1` is a custom pass-through until Story 8.7 either adds an explicit route or keeps that reader outside this router. No Server consumer is wired here. | defer (carried) |
| BH3 | medium (unverified) | Carried from pass 1 BH6/EC10/EC11: bounded JSON can reject stored legacy history that existing readers accepted. The recorded stored-data inventory before 8.7 determines whether any such record exists. | defer (carried) |
| BH4 | medium (unverified) | Carried from pass 2 BH2: a plain historical `$enc` field would be rejected as a protected marker. The existing history scan determines whether one exists. | defer (carried) |
| BH5 | low | `2e0` is valid JSON numeric notation, but it is not the carrier's canonical integer spelling; `HasCanonicalCarrierStructure` rejects it before the integer DTO reader. The safe malformed result retains the record, and supporting alternate spellings adds parsing rules without a known writer. | reject |
| BH6 | low | `BigInteger.TryParse` receives at most the carrier's 65,536-character ceiling. The reviewer showed no reachable resource failure or unbounded input; replacing the parser adds complexity to this internal classifier. | reject |
| BH7 | low | Carried from the approved v1 shape decision: the router detects `$enc` within bounded JSON, while the registered reader owns its envelope and authentication details. A malformed marker cannot pass through as plaintext. | reject (carried) |
| BH8 | low | The v1 snapshot wrapper requires its marker, format, type name and non-null payload; the registered reader owns the payload representation. A boolean or number reaches that reader but cannot bypass its authentication contract. | reject |
| BH9 | false | Stream shape validation is an explicit precondition and occurs before routing. A later malformed record raises `ArgumentException`; the first-unreadable promise applies to valid contiguous streams. This is carried from the earlier BH1 decision. | reject (carried) |
| BH10 | low | The router passes caller-owned plain bytes through unchanged by design. Its API is internal and the caller must keep its input stable; a defensive copy would add a new ownership cost without a demonstrated consumer defect. | reject |
| BH11 | medium | Carried from pass 3 VG4: 357 earlier tests already exceed the lane's 324-test floor, so compatibility-class disappearance would stay green. The existing deferred entry covers the frozen workflow and required-check follow-up. | defer (carried) |
| EC1 | low | Carried from pass 1 BH4/EC8: augmented `Unprotected` metadata over marker-free plain bytes is permitted for foreign-writer compatibility. Tightening it cannot detect a carrier that was maliciously rewritten. | reject (carried) |
| EC2 | false | The malformed-later-record case violates stream shape preconditions, which are validated before any record is read; it is the same claim as BH9. | reject (carried) |
| VG1 | medium | The reviewer confirmed that excluding all three compatibility classes leaves 357 passing tests above the frozen 324 floor. The pass 3 deferred entry already owns the discovery and required-check changes. | defer (carried) |

### Pass 8 review triage (2026-10-09)

The blind hunter (BH) reviewed the pass-7 fix delta. The edge-case hunter returned no findings, and the verification-gap reviewer found no gaps. All six BH claims were checked against the tests, router, and tracking state.

| ID | Verdict | Evidence | Route |
|---|---|---|---|
| BH1 | low | The spec is `in-review` while sprint status is `in-progress`; the workflow synchronizes sprint status when the review transition completes. The proposed change edits this build's spec. | reject |
| BH2 | low | Pass 7 appears before Pass 6 in the review record, which makes the chronology harder to read but does not affect the implementation. The proposed change edits this build's spec. | reject |
| BH3 | low | The in-loop cancellation test accepts any `OperationCanceledException`, so a regression that loses the caller token would pass. The router currently throws from `source.Token.ThrowIfCancellationRequested()`. A direct assertion pins the intended token. | patch |
| BH4 | low | The unreadable event cancellation test likewise accepts an unrelated cancellation token, although the current router throws from the caller token. A direct assertion pins it. | patch |
| BH5 | low | The foreign-cancellation outcome checks the reason and null state but leaves its retention flag unasserted. Other protected snapshot tests cover retention; a direct assertion keeps this row complete. | patch |
| BH6 | low | The pass-7 implementation note records passing tests but no post-fix mutation run for the four former survivors. The proposed change edits this build's spec, and the reviewer did not demonstrate a remaining behavior gap. | reject |

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
