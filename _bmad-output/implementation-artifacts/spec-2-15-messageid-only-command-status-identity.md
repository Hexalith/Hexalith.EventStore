---
title: '2.15 MessageId-only command-status identity'
type: 'feature'
created: '2026-11-18'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'def48eb38f87ad4a0e0be832d0e7f5a6ffca6866'
story_id: '2.15'
story_key: '2-15-messageid-only-command-status-identity'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Generated command controllers build the `202` status `Location` from `MessageId ?? CorrelationId`. A correlation identifier can therefore address a status resource, and no grammar check binds the `MessageId` to the contract's declared version (FR12-C3, FR12-C4, OR21, G-STATUS-ID).

**Approach:** Add the PRD's v1/v2 MessageId grammars to `Contracts`. The generated-host route filter publishes the version from the compiled declaration it already digest-matches. Generated actions then emit `Location` only for a response `MessageId` that is valid under that version, and never fall back to `CorrelationId`.

**Owner decisions (2026-11-18):**
- **Gateway lookup stays out of scope.** FR12 is the generated-REST slice. The gateway's `GET /api/v1/commands/status/{id}` keeps Story 4.2's documented bounded correlation-index compatibility. Its tension with AD-17/AD-32 is logged in `deferred-work.md`.
- **The routing catalog is the manifest.** The drift-checked `deploy/dapr/eventstore-routing-catalog.json` serves as the version manifest; no new manifest file. NFR12 classifications are recorded in this spec's Design Notes for Story 3.18 to import.
- **Authorization.** This `/bmad-build 2.15` request is the owner's G-STATUS-ID authorization, mirroring the 9.2/9.3 clarification; deleted Story 9.1 tooling is not a prerequisite.
- **Closure is bounded.** The work may reach `review`. `done` requires the Phase 4 completion validator to accept the G-STATUS-ID assurance: owner attestation plus the seal work deferred from 9.2.
- **Spec size.** The full spec is kept, above the token target.

## Boundaries & Constraints

**Always:** Each command contract's `Contracts` declaration (`RouteContractDeclaration.MessageIdVersion`) is the only version source. No header, query, body or other caller input selects or overrides it. v2 means exactly 26 uppercase Crockford characters, where `Ulid.TryParse` succeeds and the round-trip text is byte-identical. v1 means 1–128 ASCII alphanumeric or hyphen characters with no edge hyphen, preserved byte-for-byte. Keep Story 2.9's absolute-or-absent `Location`, `Retry-After: 1`, and no `Location` on failures. The accepted `202` is still returned when `Location` is omitted.

**Never:** Change any contract's declared version or the routing catalog, the catalog digest/lease semantics, `ValidateTenant`'s signature, `ICommandStatusLocationBuilder`, the gateway `CommandsController` or `CommandStatusController`, or `.github/workflows/**` (owned by CiCd). Do not add evidence packets, new CI lanes, mutation guards or a second manifest.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Failure |
| --- | --- | --- | --- |
| v1 valid | Declared 1; response `MessageId` legacy-valid | Absolute `Location` from `MessageId` | None |
| v2 valid | Declared 2; canonical uppercase ULID | Absolute `Location` from `MessageId` | None |
| Invalid for version | v2 with lowercase/non-canonical/legacy id; v1 with edge hyphen or >128 | `202`, no `Location` | Omit safely |
| Missing identity | `MessageId` null/blank; `CorrelationId` only | `202`, no `Location` | Omit safely |
| Divergent ids | Valid `MessageId` ≠ `CorrelationId` | `Location` uses `MessageId` only | None |
| Undeclared/ambiguous | No version published, non-int, not 1/2 | `202`, no `Location` | Omit safely |
| Caller-selected | Header/query claiming another version | Ignored; declared version decides | None |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs:292-296` — the fallback line and `TryBuild` call. Replace them, keeping `Retry-After` and the failure path.
- `src/Hexalith.EventStore.Contracts/Routing/RouteCatalogGeneratedAction.cs` — static bridge between generated code and hosting (`ValidatorKey`). Add the version key and status selector here.
- `src/Hexalith.EventStore.ServiceDefaults/Routing/GeneratedRouteCatalogFilter.cs` — `:179-187`: `compiled[0]` is the digest-matched compiled declaration, the only trusted version source. `:60-66`: cleanup of `Items`, where the new key must be removed too.
- `src/Hexalith.EventStore.Server/Identity/ActorRegistryTransitions.cs:18-19` — the ByteAether `Ulid.TryParse` + ordinal round-trip pattern. Contracts already references `Hexalith.Commons.UniqueIds` (see `Contracts/Streams/SourcePublicationHead.cs`). `EventIdentityValidator.IsUlid` is case-insensitive, so do not reuse it.
- `src/Hexalith.EventStore/Validation/SubmitCommandRequestValidator.cs:40-41` — the v1 regex shape (gateway-only; leave it unchanged).
- Tests that pin the fallback:
  - `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs:65-66,519`.
  - `RestApiGeneratedControllerErrorSemanticsTests.cs:1007-1027` and the `:1168` fixture `Items`.
  - The `FakeEventStoreGatewayClient.cs` fakes (Generators `:31`, Sample `:43`), which return CorrelationId-only responses.
- `tests/Hexalith.EventStore.Sample.Tests/SampleApi/SampleApiGeneratedControllerRuntimeTests.cs:185-271` — uses the real filter via `SampleApiGeneratedFilterFixture.cs`. Sample declares v1.
- `tests/Hexalith.EventStore.Server.Tests/Routing/GeneratedCatalogDeclarationTests.cs` — real-filter test home.
- Docs: `docs/reference/command-api.md:163`, `docs/guides/troubleshooting.md:1024` and `docs/reference/problems/bad-request.md:14`.
- `_bmad-output/implementation-artifacts/phase-4-gate-risk-matrix.json` — the G-STATUS-ID row is unbound. Validator: `tools/validate-phase-4-gate-risk-matrix.py`. Model the binding on the G-OQ8 row.
- CI: `.github/workflows/ci.yml` job `ci` already runs the Generators, Sample, Server and ServiceDefaults tests; job `ci / contracts` runs the Contracts tests.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.Contracts/Identity/MessageIdGrammar.cs`, with `tests/Hexalith.EventStore.Contracts.Tests/Identity/MessageIdGrammarTests.cs` — add a public `IsValid(string? messageId, int version)` covering v1 and v2, so `Contracts` owns the grammar. Test every grammar row of the matrix plus non-Crockford letters.
- [x] `RouteCatalogGeneratedAction.cs` — add a public `MessageIdVersionKey` and `TrySelectStatusMessageId(executionContext, messageId, out statusMessageId)`. It returns true only when the published version is the int 1 or 2 and the id is valid for that version.
- [x] `GeneratedRouteCatalogFilter.cs` — for commands, after validation succeeds, publish `compiled[0].MessageIdVersion` under the key, and remove it in `finally`. The declaration stays the sole source.
- [x] `RestApiControllerEmitter.cs` — emit `TrySelectStatusMessageId(HttpContext.Items, __hexalithResponse.MessageId, …)` and call `TryBuild` only when it succeeds. No `CorrelationId` token may remain in the status path.
- [x] Generators tests — replace the fallback pins and make the fakes return `MessageId`. Add a compiled-source assertion that no `CorrelationId` fallback is emitted, and runtime tests for every matrix row under both grammars.
- [x] Sample and Server tests:
  - Move the Sample runtime tests to `MessageId` responses, and add CorrelationId-only and divergent cases.
  - Add a Server real-filter test proving the published version equals the compiled declaration's, ignores a caller header, and is removed after the action.
- [x] Docs (the three listed in the Code Map) — state that generated hosts emit `Location` only from a version-valid `messageId`, and fix the `<correlation-id>` status wording.
- [x] `phase-4-gate-risk-matrix.json` — bind G-STATUS-ID's `command`, `trigger` (the existing `ci` jobs), `subject_paths`, `evidence_identity` and `pass_condition`, and set `binding_owner` to null.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` and `_bmad-output/planning-artifacts/epics.md` (Story 2.15) — record the owner decisions:
  - `deferred-work.md`: a new entry for the gateway correlation-lookup tension.
  - `epics.md`: one execution-clarification line (authorization, catalog as manifest).

**Acceptance Criteria:**
- Given the generator and its tests, when they are searched, then no `MessageId ?? CorrelationId` (or equivalent) status fallback remains.
- Given each command contract in the routing catalog, when its declaration is validated, then it has exactly one version (1 or 2), and no request input changes the version used for `Location`.
- Given the bound CI lane, when it runs, then every matrix row passes, compiled and at runtime, under both grammars, and the matrix validator reports no G-STATUS-ID binding finding.

## Design Notes

**Manifest:** the drift-checked routing catalog. All 18 commands are v1, and no contract changes version.

**NFR12 classification:**

| Surface | Change | Class |
| --- | --- | --- |
| Generated `202 Location` (RestApi.Generators output) | Omitted when the response lacks a version-valid `MessageId`; never built from `CorrelationId`. Identical output with the current gateway, which always returns `messageId`. AD-17 already permits absence. | Compatible behavioral tightening |
| `Contracts.Identity.MessageIdGrammar` | New public type | Additive (minor) |
| `RouteCatalogGeneratedAction.MessageIdVersionKey` and `TrySelectStatusMessageId` | New public members | Additive (minor) |
| `GeneratedRouteCatalogFilter` | Publishes the version in `Items` | Internal behavior, no API change |

## Verification

**Commands:**
- `dotnet build Hexalith.EventStore.slnx -c Release` — expected: 0 warnings, 0 errors.
- The built test assemblies for Contracts, RestApi.Generators, Sample and Server, run in place, first with `-class` filters for the touched classes and then as full projects — expected: all pass.
- `python3 -B tools/validate-phase-4-gate-risk-matrix.py _bmad-output/implementation-artifacts/phase-4-gate-risk-matrix.json` — expected: no G-STATUS-ID binding findings.
- `git grep -n "MessageId ?? " -- src/Hexalith.EventStore.RestApi.Generators tests/Hexalith.EventStore.RestApi.Generators.Tests tests/Hexalith.EventStore.Sample.Tests` — expected: no status-key hits.

**Local results (2026-11-18, after review patches, baseline `def48eb38`):**
- Release build: 0 warnings, 0 errors.
- Touched classes: MessageIdGrammarTests 65/65; Generators status and generation classes 172/172 (71 `CommandStatus_` rows); Sample runtime 20/20; Server GeneratedCatalogDeclarationTests 8/8; Testing FakeEventStoreGatewayClientTests 28/28.
- Full projects: Contracts 1,722 (0 failed, 2 skipped for `EVENTSTORE_PACKAGE_CONTRACT_DIR`); Generators 274/274; Sample 230/230; Testing 342/342; Server 8,520 with 1 failure, `PublishedTenantQuerySelectorAbiTests.AuthenticPublicTenantSevenConstructsOriginalDeclarationsAgainstExtendedContracts`, whose local NuGet-cache `Hexalith.Tenants.Contracts` 7.0.0 archive hash differs (environment, file untouched).
- Matrix validator: no G-STATUS-ID binding finding; the gate stays FAIL on `SEAL_MISSING`, `ATTESTATION_MISSING`, `ACHIEVED_BELOW_REQUIRED`. Fallback grep: no hits.
- Completion: the spec stays `in-review` until the Phase 4 completion validator accepts G-STATUS-ID assurance. The CI run link is recorded after push.

## Review Triage Log

Review pass 1 (2026-11-18): blind-hunter, edge-case-hunter and verification-gap layers.

| # | Layer | Finding | Verdict | Evidence | Route |
| --- | --- | --- | --- | --- | --- |
| 1 | verification-gap | The shipped `Testing` fake returns no `MessageId`, so generated controllers driven by it never emit `Location` | low | `FakeEventStoreGatewayClient.cs:43,78,264` returns `new("test-correlation-id")` as-is; the gateway always returns `messageId`. No current consumer asserts a generated `Location` with it (the Tenants `Location` assertion hits the gateway). | patch |
| 2 | edge-case | Same as 1: no `Location` from consumers using the public fake | low | Same root cause as 1. | patch (with 1) |
| 3 | blind | `bad-request.md` half-updated: the example detail, How to Fix and Related still use `correlationId`, and the new bullet names a missing segment that never routes | low | The page now contradicts itself; the real 400 is `CommandStatusController.cs:83-90` "Message or correlation identifier must be 1-128 characters…". The sibling `command-status-not-found.md` part is pre-existing. | patch (bad-request); defer (sibling page) |
| 4 | blind | `epic-2-context.md` regeneration contradicts the diff (9.1 record prerequisite, new workflow file) | false | The cache is stale: `epics.md` (16:42) is newer than `epic-2-context.md` (16:01), so step-01 recompiles it before any build loads it. | reject |
| 5 | blind | `epic-2-context.md` regeneration drops binding Epic 2 constraints | false | Same staleness as 4; no build consumes this copy. | reject |
| 6 | blind | G-STATUS-ID `subject_paths` omit files that decide the gate | low | `RouteContractDeclaration.cs` (version field), `RouteCatalogValidator.cs:266` (1-or-2 rule), `CompiledRouteCatalogDeclarations.cs`, the two internal fakes and two filter fixtures drive gate rows but are not in the subject digest. | patch |
| 7 | blind | Matrix `command`/`evidence_identity` misdescribe job `ci` | false | `domain-ci.yml:477` runs `dotnet test "$proj" --no-build --configuration Release`; `ci.yml` pins it by commit SHA, so its SHA-256 identifies it; no new test carries the excluded trait. | reject |
| 8 | blind | The v2 path never runs through the real filter | false | The filter publishes `compiled[0].MessageIdVersion` with no value branch; the v1 real-filter test exercises that exact path, and no v2 declaration exists. Both grammars run compiled and at runtime. | reject |
| 9 | blind | v1 grammar diverges from the gateway regex (`$` admits a trailing `\n`) | low | `SubmitCommandRequestValidator.cs:109` and `CorrelationIdMiddleware.cs:50` use `$` without `\z`. This is pre-existing gateway code that the spec leaves unchanged. | defer |
| 10 | blind | Valid versions are hard-coded in three places | low | Real but developer-only and no v3 is planned; the fix adds public constants. | reject |
| 11 | blind | Generator→Contracts coupling is unclassified; an old Contracts gives CS0117 | low | The same lockstep coupling already exists for `ValidateTenant`; the failure is a loud compile error; the fix adds a guard and a diagnostic. NFR12 wording would be a spec edit. | reject |
| 12 | blind | Caller-selection tests can't fail; the isolated context sets v1 regardless; the "Sample declares v1" comment is wrong | low | The tests are regression guards that fail if any header/query is ever honored; the Server real-filter tests prove publication only after validation. The comment at `RestApiGeneratedControllerErrorSemanticsTests.cs:1172` is wrong: that fixture compiles `Smoke` source. | patch (comment) |
| 13 | blind | `EveryCatalogCommandDeclaresExactlyOneMessageIdVersion` can't fail on its own | false | Redundant with the codec but harmless. The missing property is enforced: the filter throws when `compiled.Length != 1` or the digest differs, refusing dispatch. | reject |
| 14 | blind | An omitted `Location` leaves no trace | low | Diagnosability only; AD-17 permits absence; the current gateway always returns a valid `messageId`. Logging needs plumbing in generated code. | reject |
| 15 | blind | Verification lacks counts; the grep misses equivalent fallback forms | false | Counts would be a spec edit. The only `CorrelationId` in the generator is the problem extension at `RestApiControllerEmitter.cs:673`, and the compiled-source test bans `CorrelationId`/`??` in every status path. | reject |
| 16 | blind | The `command-api.md` paragraph lists only some omission causes and gives no contract-author guidance | low | The new paragraph omits "no published version" and "no valid status base". Contract-author guidance is not part of this caller reference. | patch (omission causes) |
| 17 | edge-case | New generator with old Contracts → CS0117 | low | Same as 11. | reject |
| 18 | edge-case | The filter's early return skips the scrub for actions without `RouteCatalogEndpoint` | false | Without the attribute no `ValidatorKey` is installed, so emitted `ValidateTenant` (`RestApiControllerEmitter.cs:927-930`) returns 503 before dispatch; `Items` is not caller-writable. | reject |
| 19 | edge-case | Same as 1 (`Testing` fake deletion edge) | low | Same root cause as 1. | patch (with 1) |
| 20 | edge-case | `epic-2-context.md` drops the AD-15 provenance decision | false | Stale cache, as in 4. | reject |
| 21 | edge-case | `epic-2-context.md` drops the system-stream migration constraint | false | Stale cache, as in 4. | reject |
| 22 | edge-case | `epic-2-context.md` drops the SignalR measurement basis | false | Stale cache, as in 4. | reject |
| 23 | edge-case | `epic-2-context.md` drops the transport and deployment decisions | false | Stale cache, as in 4. | reject |
| 24 | edge-case | `epic-2-context.md` drops the named boundaries and UX states | false | Stale cache, as in 4. | reject |
| 25 | edge-case | `epic-2-context.md` lists a 9.1 record as a 2.15 prerequisite | false | Stale cache, as in 4; the 2.15 build is past that point. | reject |
