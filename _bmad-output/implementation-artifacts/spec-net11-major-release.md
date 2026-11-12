---
title: 'Publish EventStore with the .NET 11 runtime contract'
type: 'feature'
created: '2026-11-12'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'bc19f934467ee8d3a7c402ef40aa8b6c14c162b9'
context: []
---

<frozen-after-approval reason="User explicitly chose the .NET 11 major release and authorized bypassing human validation">

## Intent

**Problem:** The latest shared tooling targets .NET 11, while EventStore still publishes a .NET 10 runtime contract. Updating only the SDK leaves consumers on the previous runtime and prevents coherent adoption of current ecosystem packages.

**Approach:** Migrate active product, test, consumer, and container targets to .NET 11; adapt affected APIs and runtime identity preparation; then publish the resulting breaking major through the normal automated release. Preserve the completed CI repairs and historical compatibility evidence.

## Boundaries & Constraints

**Always:** Use SDK 11.0.100, current qualified shared tooling and genuinely published dependencies. Preserve full selected-owner assembly identity, loaded MVID, prebuild SHA-256, immutable snapshots, and source/snapshot drift refusal. Distinguish framework-selected contracts from app-local package contracts using the actual selected host, runtime configuration, dependency graph, and SDK-owned probe. Retain package-based Release validation, warnings as errors, persistence assertions, and verified teardown. Preserve existing authorization, privacy, JSON scratch-budget, and bootstrap-refusal repairs. Declare the runtime compatibility break with a validated Conventional Commit and normal semantic major versioning. Coordinate shared producer releases with their active owners.

**Never:** Relabel current binaries as an older published SDK, fake image or native authority, broaden unrelated assembly sharing, suppress failing gates, retarget historical consumers indiscriminately, initialize nested submodules, overwrite concurrent work, remove environment protection settings, or publish before the required exact-source checks pass. Only the root agent owns actual EventStore commits, pushes, release dispatch, and independent publication verification.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Selected .NET 11 owner | Ordinary selected launch with framework-provided JSON contracts | Actual selected host reports exact parent identity/MVID/bytes; immutable contract snapshot prepares | Refuse any mismatch before resource startup |
| App-local contracts | Declared dependency file is present or missing | Accept only exact selected bytes; missing declared files refuse | Keep direct and indirect dependency controls |
| Changed launch inputs | Host, PATH, runtime variables, runtimeconfig, deps, framework, or original contract presence changes | Preparation or immediate pre-start verification refuses | No owner inventory or effects accepted |
| Published consumer | Fresh package-only .NET 11 application | Restores, builds, and executes supported APIs from packed packages | No source-reference fallback |
| Historical compatibility | Old published package and old wire/binary fixtures | Preserve genuine historical target and documented cross-version wire checks | No fabricated runtime compatibility claim |

</frozen-after-approval>

## Code Map

- `Directory.Build.props`, `Directory.Build.targets`, project files, workflow paths, and executable scripts select active framework and container outputs. The generator's netstandard2.0 target and historical net10.0 consumers have separate purposes.
- `src/Hexalith.EventStore.Aspire/DevelopmentRouteCatalogOwnerPreparationExtensions.cs` and `DevelopmentRouteCatalogOwnerLoadContext.cs` own selected-child preparation and bounded shared contract identity.
- New `DevelopmentRouteCatalogRuntimeSnapshot.cs` and `tools/Hexalith.EventStore.RuntimeIdentityProbe/` authenticate actual framework selection without running application startup; the probe must be available from the shipped Aspire package.
- OpenAPI transformers and `src/Hexalith.EventStore.Admin.UI/` require API migration for current ASP.NET/OpenAPI/Fluent components. Preserve visible behavior and accessibility.
- `.config/dotnet-tools.json`, root gitlinks, central package imports, and package-governance tests bind the qualified Platform/Builds tuple. Workflow executors are independently pinned and must be assessed separately.
- `tools/folded-snapshot-compatibility/` retains a genuine old net10.0 consumer while its current consumer moves to net11.0.
- New `tools/verify-release-api-compatibility.py` and its focused `tools/tests/test_release_api_compatibility.py` must make the portable 14-family API comparison mandatory in `.releaserc.json` after packing/validation and before publishing. Reuse the genuine signed public 4.2.0/source `aa16227bd07f0564a3026d619a5abd973e02656f` baseline and actual private-pack experiment under `/tmp/eventstore-net11-api-proof-20261112`; validate the canonical baseline SHA directly from Git before implementation. Pin the real APICompat tool, bind exact candidate package IDs/version/source, use isolated resolved reference closures, reject unresolved references and removed members, and retain a real compatible/removed-member control. Temporary local receipts are evidence, never a replacement for the mandatory gate.
- The externally prepared migration is isolated at `/tmp/eventstore-net11-preparation-20261112`, based on the exact baseline above. Its owning preparer must hand off a concrete patch and evidence before integration; no concurrent edits to that draft.
- Additional active output paths occur in `tests/Hexalith.EventStore.AppHost.Tests/Configuration/SampleApiCatalogReadinessTests.cs`, the SignalR Dapr distribution fixture/node, Sharding fixture, and LiveSidecar `Oq8PostgresqlFixture.cs`. Locate the tracked owning files before editing. `ContainerPublishingGovernanceTests.cs` must validate the new container base; independent workflow execution pins in package-governance tests retain their own authority.

## Tasks & Acceptance

**Execution:**

- [ ] `Directory.Build.*`, active projects, workflows, scripts, and current documentation — migrate active runtime/output paths and container base images; preserve historical targets.
- [ ] Aspire preparation, runtime probe, and associated AppHost tests — implement genuine selected-runtime identity snapshots and all refusal cases in the matrix.
- [ ] OpenAPI and Admin UI sources/tests — adapt current APIs while preserving authorization, accessibility, state changes, and visible behavior.
- [ ] Root dependency pins and governance assertions — adopt a genuinely qualified .NET 11 Platform/Builds tuple and current compatible package versions without invented provenance.
- [ ] `tools/verify-release-api-compatibility.py`, focused tests, and `.releaserc.json` — enforce a portable authenticated 14-family cross-framework API comparison before publication, keeping the intentional runtime-major break separate from unexpected API removal.
- [ ] Focused tests, package consumers, full solution build, and exact-source hosted workflows — complete automated validation with required tests running and existing inventory-dependent skips documented.
- [ ] Release workflow and `tools/release-packages.json` — publish the semantic major and independently verify all 14 public packages, exact source identity, signatures, and release artifacts.

**Acceptance Criteria:**

- Given the .NET 11 migration, when the complete solution builds in Release with package dependencies and warnings as errors, then active outputs target .NET 11 and supported generator/historical targets retain their purpose.
- Given genuine selected-owner outputs, when preparation and startup occur, then the matrix's positive and negative runtime identity checks pass without weakening existing contract checks.
- Given current dependencies, when product and hosted integration suites execute, then required tests, persistence assertions, and owned cleanup pass.
- Given successful exact-source CI, when the authorized automated release completes, then GitHub contains the new major release and every manifest package is public with the correct version, source commit, and valid NuGet signature.

## Implementation Notes

The user's explicit answer, “Migrate to .NET 11 major release,” supersedes the previous net10.0 compatibility boundary for new publication. The original completed repair spec remains unchanged as historical evidence. The user already authorized pushes, releases, and bypassing human validation; no additional interactive approval is required. Actual immutable publication still follows successful automated gates.

The official-feed audit checked 119 relevant package IDs, including 105 central pins reached by existing draft restore graphs. SDK 11.0.100 is current. The remaining genuine updates are Commons.UniqueIds 3.0.0, MediatR 15.0.0, SwaggerUI 11.0.1, and the qualified Builds 5 tool. PolymorphicSerializations 2.0.0 is a catalog update without a new EventStore dependency edge. All eight npm dependencies and DefaultDocumentation are current. The active owner is preparing a genuine shared producer release; no prospective SDK version is entered into the catalog.

The isolated prepared migration passes 62 runtime controls, 25 fresh private package-only controls, 1,140 Admin UI cases, and a full solution build with zero warnings/errors. These are preparation evidence, not final-source or signed-publication acceptance. The root added eight disjoint active output-path/container assertion corrections; historical consumers and the finite external-protection .NET 10 runtime protocol remain intact. The published compatibility matrix's 239 cases retain their genuine historical targets and package/source identities; its coordinator moves to .NET 11.

The private Tenants 7.0.7 image-binding prerequisite was tested on local candidate `ee3c503c5948d153994f75296d2ed7c5018114dd`, exact parent `de255a2a8e0dbe938c3781ec0281699db06c3879`, with genuine Builds 5.0.0, public SDK 4.2.0 and Tenants 7.0.7. All 83 component controls passed without skips. The first native attempt refused before resources because the model builder skipped central transitive pinning and selected Options.ConfigurationExtensions 10 instead of 11. Exact owned cleanup passed with no resources or workspace remaining. That producer bug is being repaired without suppressing downgrades; no native or SDK 5 acceptance is claimed from component tests.

## Spec Change Log

## Review Triage Log

All three independent reviewers completed before triage. Duplicate reports are combined below; each finding was traced against the actual source. The review's numerical finding floor is not acceptance evidence.

| ID | Finding and trace | Decision and resolution |
| --- | --- | --- |
| R1 | Ordinary host roll-forward can select a newly installed framework after preparation without changing an existing tracked file. | Confirmed and fixed. Rechecks the actual effect-free selected runtime before startup; compatible 11.0.0→11.0.1 installation refuses and unrelated NET10 installation remains supported. Full 87 controls pass; final LD_AUDIT-only addition passes 21 focused cases. `/tmp/eventstore-net11-runtime-review-fix-receipt.json`. |
| R2 | Runtime environment filtering omits inherited/resource `LD_PRELOAD`, `LD_LIBRARY_PATH`, and `DYLD_*` overrides. | Confirmed by both code reviewers. Refuses unsupported inherited/resource native loader inputs, including LD_AUDIT, and binds finite selected native runtime inputs. Full 87 and final 21 focused refusal controls pass. |
| R3 | Complete owner/declaration/adapter/SDK source and snapshot inventory is verified before returning preparation but omitted from the new startup observer. | Confirmed. Retains the complete existing source and immutable dependency inventory through startup. Actual owner/dependency/declaration/adapter drift controls pass in the full 87-case run. |
| R4 | An asynchronous event-detail request is hidden until state and causation requests also complete. | Confirmed independently by two actual bUnit probes. Fixed in lifecycle orchestration without suppressing BL0012. New staged async test fails before/fixes after; full UI 1,141/1,141 passes, zero skips. `/tmp/eventstore-net11-ui-progressive-evidence.json`. |
| R5 | Default APICompat arguments permit public parameter-name changes that break named-argument callers. | Confirmed by real CP0017 control. Enabled the rule. All 14 families and real compatible/CP0002/CP0017 controls pass in `/tmp/eventstore-api-gate-review-fixes-verification-20261112.json`. |
| R6 | API gate evidence defaults outside the release workflow's uploaded directory. | Confirmed. Retained receipts, identity/signature logs and reference inventories under uploaded release evidence; only scratch binary caches are excluded. Actual retained evidence: 148 files, 19.44 MiB, no binary cache bodies. The real 14-family rerun passes. |
| R7 | New focused API-gate tests are absent from mandatory CI invocation. | Confirmed. Wired the focused controls with exact APICompat 11.0.100 and SDK 10.0.404 reference-pack prerequisites into the existing contract job. All 20 focused controls and 2 package-contract tests pass; both changed workflows pass actionlint. |
| R8 | Unix PATH lookup stops at a nonexecutable first file while ordinary lookup skips it. | Confirmed. Skips nonexecutable candidates to match ordinary Unix lookup; the actual executable-selection control passes. |
| R9 | Contributor prerequisites omit the SDK 10 feature band needed by the explicit unsupported-generator-host fixture. | Confirmed by both code reviewers. Restored SDK 10.0.4xx (10.0.403 or later patch) alongside current SDK 11 and historical runtime requirements in CONTRIBUTING and prerequisites. |
| R10 | Current troubleshooting, deployment, projection and test README instructions retain old runtime/output targets. | Confirmed. Updated active instructions to .NET 11; historical recorded evidence and frozen consumers remain unchanged. |
| R11 | Active external-protection helpers force net11 output while the selected Platform checkout is still net10; developer sibling checkout can mask this. | Confirmed pending dependency qualification. Helpers now evaluate the owning project's actual TargetPath with the same build properties. Actual root-only nine-sibling Debug source graph now builds 0 warnings/errors with normal CI flags; all seven topology controls pass without skips. `/tmp/eventstore-net11-root-debug-source-receipt.json`. Final qualified Platform NET11 gitlink remains required; no public Tenant8 authority is implied. |
| R12 | Canonical build configuration still waives all NU1901–NU1904 from warnings-as-errors despite strict temporary commands. | Confirmed in preparer handoff. Removed the canonical waiver while retaining audit true/all/low. Actual normal full NET11 Release/package-mode solution passes 0 warnings/errors in 62.54s with the canonical policy, without a command-only waiver. |

## Verification

- `dotnet build Hexalith.EventStore.slnx --configuration Release -warnaserror -m:1 -p:UseHexalithProjectReferences=false` — zero warnings/errors.
- Build and run affected test projects individually, including full AppHost, Admin UI, OpenAPI/generator, package governance, and runtime preparation controls — no unexpected failures or missing required cases.
- Run fresh package-only consumers and folded snapshot/wire compatibility, retaining the old consumer's actual target.
- Require successful push CI and commitlint at the final source, complete the configured integration workflows, then verify all manifest packages through the official NuGet feed with source and signature checks.
