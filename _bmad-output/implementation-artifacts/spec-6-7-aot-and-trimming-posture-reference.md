---
title: 'Story 6.7: AOT And Trimming Posture Reference'
type: 'chore'
created: '2026-10-10'
status: 'in-review'
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
- Page SHA-256: `65fd929e99115fd59abccc50358050c9b027ef4b34480cc1d9394dbbb7669e9f`.
- Owner review: pending. The owner must review this digest and record a dated `single-maintainer-attested` attestation; this implementation does not claim an independent review.

## Implementation Notes
