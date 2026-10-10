---
title: 'Story 6.7: AOT And Trimming Posture Reference'
type: 'chore'
created: '2026-10-10'
status: 'done'
baseline_commit: 'c7af92d8e96c5a81bd4dc39184a06f96c14fe05a'
route: 'dispatch'
review_loop_iteration: 0
story_key: '6-7-aot-and-trimming-posture-reference'
context:
  - '{project-root}/_bmad-output/planning-artifacts/epics.md'
  - '{project-root}/docs/page-template.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** NFR18 says Native AOT and IL trimming are not targets while reflection conventions remain load-bearing. Only the PRD states this (OR6), and nothing stops a release package from claiming `IsAotCompatible` or `IsTrimmable`.

**Approach:** Publish `docs/reference/aot-and-trimming-posture.md`, which states the posture and inventories the reflection conventions. Add a blocking Contracts.Tests guard that evaluates every `tools/release-packages.json` project and fails on either property evaluating true while the document's posture marker is present. Prove the guard with seeded violations.

## Boundaries & Constraints

**Always:** Evaluate effective MSBuild values, never text-scan. Compare `true` case-insensitively. If the posture marker is missing, the guard fails closed. Report every violating project and property in one failure. Run inside the existing blocking `contracts` CI job. Record the document's SHA-256 and the owner's review in this spec's Verification section. Label that review `single-maintainer-attested`, never `independent`.

**Never:** Annotate code, add source-gen or make anything AOT-safe. Guard `PublishAot`, `PublishTrimmed` or analyzer flags (`PayloadProtection.csproj` keeps `EnableAotAnalyzer`/`EnableTrimAnalyzer`). Add a CI lane, workflow, script, evidence packet or digest-pinning test. Edit `prd.md`, `epics.md`, the architecture or other gate rows.

**Decisions (owner, 2026-10-10):**
- **9.1 dependency:** build now; the gate waits. Story 6.7 closes on its two ACs. G-NFR18 stays FAIL/BLOCKED until Story 9.1 exists and the owner issues the authorization record. No PRD or epics edits.
- **Spec size:** keep the full spec (~2,100 tokens), because the inventory in the Code Map is the document's source material.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Clean | All 14 manifest projects evaluate neither property as true | Guard passes | N/A |
| Seeded | A manifest project evaluated with `-p:IsAotCompatible=true`, `-p:IsTrimmable=true` or `-p:IsTrimmable=True` | Violation names the project and property | Assertion lists all violators |
| Implied | `IsAotCompatible=true` lets the SDK default `IsTrimmable` | Both reported | Same |
| Posture changed | Marker absent from the document | Guard fails and names the document | No silent pass |

</frozen-after-approval>

## Code Map

- `tools/release-packages.json` -- release package set (14 `src/` projects). Read it the way `ReleasePackageManifestTests.LoadReleasePackages` (L1868) does.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- reuse these patterns by copying them, as each class keeps its own private helpers:
  - `FindRepositoryRoot` (L2243)
  - `EvaluatedProjectProperty` (L1883): `dotnet msbuild -getProperty`, `-p:Configuration=Release -p:UseHexalithProjectReferences=false`, 60 s timeout with kill.
- `tests/.../Packaging/DependencyModeEvaluationTests.cs` (~L200-232) -- multi-property `-getProperty:A,B` gives JSON output; use it so each project costs one evaluation.
- `ReleasePackageManifestTests.ActivePackageDocumentationPaths` (L1925), the stale-package-count regex test (~L1245) and the UI-host wording test (~L1290) all scan `docs/**/*.md`. The new page must not state a package count and must pass the UI-host wording rules.
- `docs/page-template.md` -- page conventions:
  - back-link first line, one H1, one-paragraph summary
  - language-tagged fences, `bash` with `$`
  - ends with `## Next Steps` containing `**Next:**`/`**Related:**`
  - lint: `.markdownlint-cli2.jsonc`, `scripts/validate-docs.sh` (local only)
- `docs/index.md` L57-61, `README.md` L128-131 -- reference page lists; add the new page.
- Reflection inventory for the document (packable `src/`; `src/Hexalith.EventStore.*` roots):
  - **Apply:** Client `Aggregates/ApplyMethodResolver.cs` L50-68 (`GetMethods`, `Invoke`, `Deserialize(Type)`); `AggregateReplayer`, `EventStoreProjection`, `Handlers/DomainProcessorStateRehydrator.cs` (also snapshot `GetProperties` L237-252).
  - **Handle:** Client `Aggregates/EventStoreAggregate.cs` L140-298 (`DiscoverHandleMethods`, static `CommandType` `GetProperty`, `Invoke`, `Task.Result` via reflection).
  - **Assembly scanning:** Client `Discovery/AssemblyScanner.cs`, `Registration/EventStoreServiceCollectionExtensions.cs` (`GetCallingAssembly`, `ActivatorUtilities.CreateInstance`), `Conventions/NamingConventionEngine.cs` attributes.
  - **Upcasters:** Client `Registration/EventPayloadEvolutionRegistration.cs`, `Events/EventPayloadEvolutionRegistry.cs` L46-56 (`GetTypes`, `Activator`), `Events/RegisteredEventUpcaster.cs` L25 (`Assembly.Location`).
  - **Subscriptions:** Client `Subscriptions/EventStoreDomainEventProcessor.cs` L27, L294 (`MakeGenericMethod`, `Invoke`); `Registration/EventStoreDomainEventsServiceCollectionExtensions.cs` L111; `EventStoreHostExtensions.cs` L141-150.
  - **DomainService:** `EventStoreDomainServiceExtensions.cs` L559-641 handler scanning; `AdminOperationalIndexMetadata.cs` L140-192.
  - **JSON:** reflection-mode System.Text.Json with no `JsonSerializerContext`. Contracts `Serialization/EventStorePayloadSerialization.cs` L34-42 already says AOT/trimming is out of scope.
  - **Platform:** Dapr actor remoting (Reflection.Emit proxies, Server `.csproj` `InternalsVisibleTo`); MVC controllers in Admin.Server and the generator's emitted controllers; Admin.Cli formatters `GetProperties`.
  - No `RequiresUnreferencedCode`/`RequiresDynamicCode`/`DynamicallyAccessedMembers` annotations exist.

## Tasks & Acceptance

**Execution:**
- [x] `docs/reference/aot-and-trimming-posture.md` -- create per the page template:
  - the posture marker line `**Posture:** Native AOT and IL trimming are not targets for Hexalith.EventStore release packages.`
  - why the posture holds, the inventory table (convention, where, reflection used), consumer guidance, and the guard with its validation command and owner Story 6.7
  - what must change before the posture can change
- [x] `docs/index.md`, `README.md` -- link the page in the reference lists.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/AotTrimmingPostureTests.cs` -- one class, private helpers:
  - a document test: page exists, marker line and inventory heading present
  - a manifest guard: non-empty set, one multi-property evaluation per project, all violators listed
  - a seeded-violation theory per matrix row through the same evaluation path

**Acceptance Criteria:**
- Given the posture page, when reviewed, then it states AOT/trimming are not targets while reflection conventions are load-bearing and inventories them; its SHA-256 and the owner's review are recorded below.
- Given the release package set, when the guard runs in the `contracts` job, then it fails on any `IsAotCompatible`/`IsTrimmable` evaluating true, and each seeded violation is observed failing.

## Spec Change Log

## Review Triage Log

| Layer and finding | Verdict | Evidence and route |
| --- | --- | --- |
| Blind: owner review pending | high | carried: Verification still says pending, so the first AC cannot close. Rejected from code triage because its fix is an owner attestation in this spec; keep the review request open. |
| Blind: Server retained-history deserialization omitted | medium | `RetainedIdentityHistorySourceReader.ReadAsync` deserializes with a runtime `Type`. Patched the JSON inventory row. |
| Blind: Server governance property walk omitted | medium | `GovernanceScopeGuardOwner.ValidateStrings` calls `GetProperties` and `GetValue` recursively. Patched with a governance inventory row. |
| Blind: Admin.Server operational reflection omitted | medium | `DaprConsistencyCommandService.TryExtractLong` reads runtime properties. Patched the platform inventory row. |
| Blind: Admin.Cli JSON serialization omitted | medium | `JsonOutputFormatter` uses `SerializeToNode` with `JsonDefaults.Options` and no generated context. Patched the JSON inventory row. |
| Blind: Gateway MVC controller discovery omitted | medium | `Gateway.csproj` compiles `src/Hexalith.EventStore/Controllers` and the host maps controllers. Patched the platform inventory row. |
| Blind: inventory rows can be removed while the document test passes | low | The test checks the required marker and heading, and this AC also requires owner review of the inventory. Rejected: proving full inventory coverage in a test would mirror the document and source. |
| Blind: marker can appear in historical text | medium | The prior substring check accepted the marker outside Current Posture. Patched the guard to require the marker directly under that heading. |
| Edge: marker can appear only in a fenced example | medium | The prior substring check accepted a fenced example. Same marker-placement patch; the seeded test now rejects this case. |
| Edge: an MSBuild evaluation failure hides later claims | false | An evaluation failure already fails the CI guard, and no effective property value exists for that failed project to report. |
| Edge: running the assembly outside the checkout cannot find the root | low | `FindRepositoryRoot` walks from the current directory, so this manual invocation fails; the documented command and CI run from the checkout root. Rejected: adding another search path for this unsupported invocation would add fallback logic. |
| Blind: Contracts anchored-state JSON omitted | medium | `RecoverableAnchoredState` serializes and deserializes generic state without generated metadata. Patched the JSON inventory row. |
| Blind: ServiceDefaults health JSON omitted | medium | `Extensions.cs` serializes health data using each runtime value's `Type`. Patched the JSON inventory row. |
| Blind: Testing reflection omitted | medium | `FakeEventPersister` serializes by runtime event type and `TerminatableComplianceAssertions` reflects on `Apply`. Patched the JSON and Apply rows. |
| Blind: Testing.Integration benchmark JSON omitted | medium | `BenchmarkDatasetBuilder` serializes state with `value.GetType()`. Patched the JSON inventory row. |
| Blind: Admin.Server exception status reflection omitted | medium | Four Dapr command services read an exception's `StatusCode` property via reflection. Patched the platform row. |
| Blind: generated REST payload JSON omitted | medium | `RestApiControllerEmitter` emits `SerializeToElement` for commands and queries with reflection-mode options. Patched the JSON row. |
| Blind: Admin.Cli input JSON omitted | medium | `AdminApiClient` and `ProfileManager` deserialize through `JsonDefaults.Options`. Patched the JSON row. |
| Edge: fenced heading can satisfy posture guard | medium | The old substring check accepted `## Current Posture` and the marker inside one code fence. Patched the guard to ignore fenced headings and seeded that case. |
| Edge: empty inventory passes document test | medium | The old test asserted only the inventory heading, so all rows could be removed. Patched it to require a data row and seeded an empty table. |
| Edge: gateway validation and admin exception reflection omitted | medium | `ValidateModelFilter` closes `IValidator<>` and reflects on `Tenant`; four admin services inspect exception status. Patched the platform row. |
| Verification gap: later packages lack seeded claims | medium | The old seeded checks used only the first two manifest entries, so a truncated loader could pass them. The aggregate seeded test now exercises the full manifest and requires the later Admin.Server project. |

The inventory omissions share one incomplete inventory root cause and were corrected together. The displaced-marker findings share one guard root cause and were corrected together.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false` -- expected: 0 warnings, 0 errors.
- Run the built test assembly in place with `-class Hexalith.EventStore.Contracts.Tests.Packaging.AotTrimmingPostureTests`, then `-class ...ReleasePackageManifestTests` -- expected: all pass.
- `scripts/validate-docs.sh` (or markdownlint-cli2 on the page if lychee is unavailable) -- expected: clean.
- `sha256sum docs/reference/aot-and-trimming-posture.md` -- record the digest here with the owner review date.

**Observed 2026-10-10:**
- Release Contracts.Tests build: passed, 0 warnings and 0 errors.
- `AotTrimmingPostureTests`: 7 passed, including explicit AOT/trimming claims, case-insensitive `True`, SDK-implied trimming, multi-project aggregation, and missing-marker rejection.
- `ReleasePackageManifestTests`: 127 passed.
- `npx markdownlint-cli2 docs/reference/aot-and-trimming-posture.md docs/index.md README.md`: 0 issues. `lychee --config /dev/null docs/reference/aot-and-trimming-posture.md`: 4 links OK. `lychee --config lychee.toml` could not parse line 48 with installed lychee 0.24.2.
- `bash scripts/validate-docs.sh`: blocked at Markdown linting by 9 issues in 5 unchanged files: `docs/brownfield/architecture.md`, `docs/brownfield/project-overview.md`, `docs/brownfield/source-tree-analysis.md`, `docs/guides/deployment-docker-compose.md`, and `docs/guides/trusted-effects.md`.
- Page SHA-256 after review fixes: `a3df87f8bdf3d73fc4560738d8659b69dd6d06947f33cad9b207681d62171912`.
- Post-review focused build: passed with 0 warnings and 0 errors; `AotTrimmingPostureTests`: 7 passed; `ReleasePackageManifestTests`: 127 passed; touched-page markdownlint: 0 issues.
- Second review corrections: Contracts.Tests Release build passed with 0 warnings and 0 errors; `AotTrimmingPostureTests`: 8 passed; `ReleasePackageManifestTests`: 127 passed; touched-page markdownlint: 0 issues; page links: 4 OK; `git diff --check`: passed. `bash scripts/validate-docs.sh` remains blocked at Markdown linting by the same 9 issues in the same 5 unchanged files listed above.
- Current page SHA-256 after second review corrections: `babd1cd1fb70596996417410ddf03eb71a411832f456a21cd01de2877d2cc77d`.
- Owner review: `single-maintainer-attested` on 2026-10-10. The owner confirmed "I reviewed it today" in response to the review request for `docs/reference/aot-and-trimming-posture.md` at SHA-256 `babd1cd1fb70596996417410ddf03eb71a411832f456a21cd01de2877d2cc77d`. This is an owner review, not an independent review.
- Post-closure follow-up 2026-10-10 (owner-requested; addresses review findings the closing triage did not cover):
  - The test now uses Shouldly instead of raw `Assert.*`.
  - `MissingOrDisplacedPostureMarkerFailsClosed` now runs through `EvaluateViolations`. A mutation check that removed the marker check from the guard path turned it red.
  - The page gained a single-file-publish note (`Assembly.Location`) and a sentence on the guard's MSBuild-only scope. `nuget-packages.md` now links the page.
  - Verification: Release build with `-warnaserror` passed with 0 warnings and 0 errors. `AotTrimmingPostureTests`: 8 passed. `ReleasePackageManifestTests`: 127 passed. markdownlint on the touched pages: 0 issues. Offline lychee: 0 errors.
  - The page SHA-256 is now `99395b0adf8f86c8c18bf58f57e863c52f91bf03d068eca7f000965972126a61`. The owner attestation above covers `babd1cd1…`, so re-attestation of this digest is pending.

## Implementation Notes
