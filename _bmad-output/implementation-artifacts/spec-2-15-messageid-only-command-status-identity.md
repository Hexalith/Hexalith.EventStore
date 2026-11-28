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

### Review Findings

Review pass 2 (2026-11-28): blind-hunter, edge-case-hunter, verification-gap and acceptance-auditor layers over the owned files `def48eb38..HEAD`, plus the `8a0e3613b` hunks of the shared planning files. Hunks from later commits in shared files (`fa891493a`, `9590106d4`, `4a913165d`, `ba2c67d8c`, `a3e8b8797`, `360ed041d`, `d82ba65a0`) are out of scope.

- [x] [Review][Patch] Reserve the emitted `__hexalithStatusMessageId` local. A regression confirms that the generator reports `HESREST004` for a same-named route parameter and omits the invalid controller.
- [x] [Review][Patch] Add the Sample declaration and committed-catalog fixture, the declaration codec, and the generator harness and controller fixture to `subject_paths`. Update the verifier’s pinned declaration digest. Move the fake-only cancellation test to its own test class so the verifier can match the exact generated-controller inventory.
- [x] [Review][Patch] Supply `Authorization` and `X-Hexalith-Tenant` in the troubleshooting status request.
- [x] [Review][Defer] The real-filter test cannot detect a filter that publishes a constant `1`; no digest-matched v2 compiled declaration runs through `GeneratedRouteCatalogFilter` [tests/Hexalith.EventStore.Server.Tests/Routing/GeneratedCatalogDeclarationTests.cs:96-135] — deferred: no v2 declaration exists; add the v2 real-filter row when the first one is introduced
- [x] [Review][Defer] `bad-request.md` omits the replay endpoint's invalid-identifier 400 ("Correlation ID must be 1-128 characters…") [docs/reference/problems/bad-request.md:14] — deferred: pre-existing; the page never listed replay

Rejected:
- false — Diff mixes commits and whole-file subject paths absorb unrelated edits: review-scope artifact; file-granular subject digests are the matrix model for every gate.
- false — `subject_paths` omit the public `Testing` fake and its tests: no G-STATUS-ID row runs through it, and the `pass_condition` names none of its tests.
- false — Diff includes other stories' hunks (auditor scope note): scope artifact, not a defect.
- false — The two newer deferred-work entries lack `status:`: at HEAD they have `status: done 2026-11-23` with resolutions.
- false — The `\z` deferred entry is stale: resolved 2026-11-23; submission and lookups now use `MessageIdGrammar` v1.
- false — The `epics.md` clarification conflicts with the Dependencies line: at HEAD that line requires "the owner's scoped authorization for G-STATUS-ID recorded in the owning spec under AD-12".
- false — The `bad-request.md` example lacks a scope header: the 400 grammar check (`CommandStatusController.cs:86`) runs before the scope check (`:99`), and the example carries `Authorization`.
- false — Idempotency rows in `command-api.md` are misfiled: added by DW-143/145/146 commits, not 2.15.
- false — `MessageIdGrammar` relies on a transitive ByteAether reference and duplicates `CorrelationIdGrammar`: the spec prescribes the `Ulid.TryParse` round-trip; Contracts already uses it (`SourcePublicationHead.cs`); v1 adds the edge-hyphen rule, so it is a distinct grammar.
- false — `StatusReadOnFakeHonorsCallerCancellation` added twice: from `a3e8b8797`, not 2.15.
- false — `rejectionType` admits non-ASCII letters (two layers): from `9590106d4`, not 2.15.
- false — Non-ASCII `reasonCode` skips display sanitizing: from `9590106d4`, not 2.15.
- false — Query provenance/lifecycle header formatting half moved: from `9590106d4`, not 2.15.
- false — The `Testing` fake throws on a null `CommandResponse`: the property is non-nullable, and no consumer in the workspace sets it to null.
- rejected (spec edit) — NFR12 Design Notes omit the `Testing` fake behavior change (two layers); the commit body documents it.
- rejected (spec edit) — Verification records no CI run link; main run 44350934826 passed on `8a0e3613b`'s push.
- rejected (spec edit) — Design Notes say 18 commands; HEAD has 19, all v1 (after `2efd7ac6b`).

Review pass 3 (2026-11-28): blind-hunter, edge-case-hunter, verification-gap and acceptance-auditor layers over the Story 2.15 hunks of `821b779d6` (the emitter, generator and Sample tests, gate script, matrix, two docs, and this spec's `deferred-work.md` section). The Story 2.18 query hunks in the same commit are out of scope. No acceptance-criteria or Never-list violation was found.

- [x] [Review][Patch] Make the platform-scope status guidance usable: name the two accepted `X-Hexalith-Scope` values and the permission each needs (`platform:tenants` → `tenants:read`, `platform:global-administrators` → `global-administrators:read`), say the `eventstore:permission` claim must appear exactly once, and say `tenant`/`tenantId` query parameters are refused [docs/guides/troubleshooting.md:1031; docs/reference/problems/bad-request.md:53]
- [x] [Review][Patch] Restore the explicit statement that status is addressed by `messageId`, never by the correlation ID, and that generated hosts omit `Location` instead of falling back to it [docs/guides/troubleshooting.md:1021]
- [x] [Review][Defer] `forbidden.md` still lists "Command status query attempted without any tenant claims" and none of the explicit-scope 403 causes enforced by `TryResolveExplicitStatusScope` [docs/reference/problems/forbidden.md:17] — deferred: pre-existing; the page is not a 2.15 deliverable

Rejected (pass 3):
- false — The collision and query-binding generator tests are outside the gate's `CASES` (two layers): the gate requires the `Unit tests (Tier 1…)` job to complete successfully (`phase_4_tenant_gate.py`, "missing successful job"), and that job runs them, so a reservation regression fails the gate.
- low — No CI step checks a real Sample TRX against the closed inventory: a stray test fails the gate loudly ("unexpected compiled case"), and the fix needs a story-evidence step in a CiCd-owned workflow, which this spec's Never list and the baseline exclude.
- false — The cancellation-test move is unexplained: this section's pass-2 patch line records the reason.
- false — `FakeEventStoreGatewayClientTests` reuses the Testing class name: a different namespace and assembly, named for the Sample `FakeEventStoreGatewayClient` beside it.
- false — `"__hexalithStatusMessageId"` is typed twice: every reserved local follows the same literal pattern, and no diverging caller exists.
- false — `bad-request.md` step 3 belongs on another page and the example lacks a scope header: the retried request meets the scope check next, and the example shows the failing request, whose 400 precedes that check.
- low — The new completion-writer ledger entry has no severity tag and unnamed targets: cosmetic; no consumer reads the tag.
- low — The query-binding test does not assert that query actions omit the status block: unlikely regression; the fix adds an assertion for a path no query emits.
- low — Query-only locals (`__hexalithETag`, `__hexalithProvenance`, …) are not reserved, so a parameter with such a name gives CS0136 instead of `HESREST004`: pre-existing (`f8b616f84`, `86c2f7b95`), needs a `__hexalith`-prefixed parameter, and the fix needs a new query-only branch.

Review pass 4 (2026-11-28): blind-hunter, edge-case-hunter, verification-gap and acceptance-auditor layers over commit `84ee94e8b` (`165d11c5f..HEAD`, the pass-3 documentation patch commit). No acceptance-criteria, `Never`-list, or verification-gap finding was raised.

- [x] [Review][Patch] Restore the explicit statement that callers poll status with `messageId` (not `correlationId`) and that generated hosts never fall back to `correlationId` when building `Location` [docs/guides/troubleshooting.md:1021]
- [x] [Review][Patch] Align Step 4's `Location` omission causes with `RouteCatalogGeneratedAction.TrySelectStatusMessageId` and `command-api.md` by adding missing published declaration version (`1` or `2`) and noting that `202 Accepted` with `Retry-After: 1` is still returned [docs/guides/troubleshooting.md:1021]
- [x] [Review][Patch] Clarify the JWT `permissions` normalization preconditions and shape: `EventStoreClaimsTransformation` only reads `permissions` when the principal has no existing `eventstore:tenant`, `eventstore:domain`, or `eventstore:permission` claim, and reads only a single `permissions` claim (JSON array or space-delimited) [docs/guides/troubleshooting.md:1033; docs/reference/problems/bad-request.md:55]
- [x] [Review][Patch] Reconcile Step 1's two-ID split with the intro and Steps 2–4 (clarify which ID is used in HTTP vs command-processing logs/spans), note that synchronous `4xx`/`5xx` failures have no `202 Accepted` body (use `X-Correlation-ID` / ProblemDetails `correlationId`), and note that `X-Correlation-ID` is emitted by the gateway [docs/guides/troubleshooting.md:1005-1021]
- [x] [Review][Patch] Add the tenant-grant validation rule (valid tenant IDs, excluding reserved `system`, unique after normalization, enforced before tenant vs platform scope branching) to `bad-request.md` and link `./forbidden.md` under `## Related` [docs/reference/problems/bad-request.md:53-61]
- [x] [Review][Patch] Clarify scope-header and tenant-grant error/casing semantics: missing/combined/repeated scope headers and invalid/reserved `system`/duplicate `eventstore:tenant` grants return `403 Forbidden` (even on platform scope); `X-Hexalith-Scope` and `eventstore:permission` are case-sensitive; and a valid platform scope for the wrong command domain returns `404 Not Found` [docs/guides/troubleshooting.md:1023-1033; docs/reference/problems/bad-request.md:53-55]

Rejected (pass 4):
- rejected (spec edit) — `Pass 3 verification` omits exact commands run and commit SHA required by `project-context.md:68` (`acceptance-auditor`): accurate observation, but the only fix edits the spec under review.
- rejected (spec edit) — `Pass 3 verification` is non-reproducible and the `Review of the pass-3 documentation patches` triage table leaves completion timing implicit (`blind-hunter`): accurate observation, but the only fix edits the spec under review.

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

**Resumed review verification (2026-11-28)**

- The isolated Aspire gateway reached `Healthy` before implementation. The AppHost was stopped after inspection.
- `dotnet build Hexalith.EventStore.slnx -c Release -warnaserror -m:4` passed with zero warnings and errors in an isolated checkout of `cf79e5cc2bb695649fbd96f0bfc7bf0ff7ea801e` with the Story 2.15 patch. The Server suite used the signed Tenants Contracts 7.0.0 archive pinned by its ABI fixture in a private package root.
- The six full Release suites passed 11,512 tests with zero failures. Contracts passed 1,762 with two package-inventory skips. Server passed 8,656 with 25 existing skips. Generators passed 322, Sample 268, ServiceDefaults 160, and Testing 344 with zero skips in those four suites.
- The offline G-STATUS-ID verifier matched all 166 required cases in the four locally emitted Release TRXs, including 71 generated-action cases across both grammars. The command diagnostic and the two query binding forms passed their compile regressions.
- The targeted transition and gate-decision fixtures and all 147 matrix fixtures passed. The matrix validator reported no G-STATUS-ID binding finding and still requires an owner attestation and seal. The fallback search and `git diff --check` passed.
- Both read-only completion checks returned `ordinary-slice` with no verification. The spec and tracker remain in review pending verified G-STATUS-ID assurance. The completion writer gap is recorded in `deferred-work.md`.

**Pass 3 verification (2026-11-28)**

- The two documentation patches passed Markdown lint and `git diff --check`. The fallback search returned no matches.
- Four focused Release projects built with zero warnings and errors. All 355 selected tests passed, including the grammar, compiled controller, generated status response, sample host, declaration and gateway scope cases. The local G-STATUS-ID verifier matched all 166 required cases in their xUnit TRX reports.
- The gate risk matrix reported no G-STATUS-ID binding finding. Its assurance findings remain `SEAL_MISSING`, `ATTESTATION_MISSING` and `ACHIEVED_BELOW_REQUIRED`.

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

Review of the 2026-11-28 patches

| Layer | Finding | Verdict | Evidence | Route |
| --- | --- | --- | --- | --- |
| Blind | The reserved local suppresses valid generated queries | medium | The shared identifier set applies to command and query actions. | patch |
| Blind | The fixed gate inventory omits the command route collision test | false | The gate requires the ordinary CI job to succeed and pins the tests that prove status-link identity. The collision test runs in that job as a generator diagnostic check. | reject |
| Blind | The completion writer accepts an ordinary slice without G-STATUS-ID assurance | high | Both read-only completion targets returned `ordinary-slice` with `verification: null` while the gate has neither an attestation nor a seal. | defer to the Phase 4 completion owner |
| Blind | A corrected status identifier still requires explicit scope | low | The gateway validates scope after the identifier and returns 403 if scope is missing. | patch the Bad Request guidance |
| Blind | The troubleshooting example omits platform scope | low | A platform command uses `X-Hexalith-Scope` and the matching read permission. | patch |
| Blind | The real filter fixture never publishes v2 | low | Carried from pass 2. The successful filter fixture uses the committed v1 declaration. | defer, existing entry |
| Blind | The public Testing fake fills a configured null MessageId | low | The public fake documents the gateway-like default. The dedicated generated-action fakes still exercise the missing identifier cases. | reject |
| Blind | Runtime headers alone cannot prove that the builder was never called | false | The compiled-source assertion pins the selector and builder call in the same short-circuit condition. Runtime tests verify the resulting headers. | reject |
| Blind | Sample query cases share the fixed report inventory | low | The existing verifier intentionally requires an exact Sample runtime inventory. The unrelated cancellation test exercises only the fake and now has its own class. Changing the report contract requires the gate owner. | reject |
| Blind | The status action fixtures use substitute URL builders | low | The Client tests cover the real builder and its service registration with configured and absent gateway bases. The generated-action fixtures exercise the MessageId passed to that interface. | reject |
| Edge case | A generated GET can collide with the command-only reservation | medium | The shared identifier set blocks both route-bound and query-bound properties before query generation. | patch with the Blind reservation finding |
| Edge case | Platform status requests fail with the tenant example | low | The gateway requires the authorized platform scope for these records. | patch with the Blind troubleshooting finding |
| Verification gap | The compiled generator rejected two previously valid GET bindings | medium | The reviewer reproduced `HESREST004` for both bindings and compiled both against the pre-patch generator. | patch with the Blind reservation finding |

Review of the pass-3 documentation patches

| Layer | Finding | Verdict | Evidence | Route |
| --- | --- | --- | --- | --- |
| Blind | Both pages imply that the raw JWT must contain `eventstore:permission` | low | `EventStoreClaimsTransformation` can derive the claim from `permissions`. The scope check counts only claims with the required value. | patch both pages |
| Blind | The `tenant` and `tenantId` restriction reads as platform-only and omits the response status | low | `TryResolveExplicitStatusScope` refuses either query parameter before distinguishing scope. A valid identifier reaches the check and returns 403. | patch both pages |
| Blind | The guide leaves out `Location` omission when no usable gateway status base is configured | low | `CommandStatusLocationBuilder.TryBuild` returns false for an absent or relative base even when the selector accepted the `MessageId`. | patch troubleshooting |
| Blind | The tracing steps use the request header to trace command processing | low | `X-Correlation-ID` identifies the HTTP request. The accepted body's `correlationId` is the command's correlation ID. | patch troubleshooting |
| Edge case | The guide can lead callers to interpret a missing `Location` as an invalid `MessageId` | low | A valid `MessageId` with an unconfigured status base still produces `202` without `Location`. | patch with the Blind gateway-base finding |
| Edge case | Troubleshooting implies a literal JWT permission claim is required | low | A native JWT `permissions` claim can produce the matching `eventstore:permission` after normalization. | patch with the Blind claim finding |
| Edge case | Bad Request can cause callers to discard a valid token while correcting the identifier | low | The scope check reads the normalized principal, which can supply the matching permission from the token's `permissions` claim. | patch with the Blind claim finding |
| Edge case | Malformed or duplicate normalized tenant grants can cause a platform-scope refusal | low | The gateway canonicalizes all tenant grants before either scope branch. An invalid or duplicate canonical value refuses the request. | patch troubleshooting |
