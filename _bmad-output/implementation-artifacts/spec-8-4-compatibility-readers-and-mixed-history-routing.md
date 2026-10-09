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
