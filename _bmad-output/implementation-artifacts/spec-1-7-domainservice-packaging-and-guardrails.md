---
title: '1.7 DomainService Packaging And Guardrails'
type: 'chore'
created: '2026-07-07T07:45:24+02:00'
status: 'done'
baseline_revision: '2dc7d5a84bb9b5db57e10ed695f7c5d8f0e7704c'
final_revision: '318c4faa75093af85bb23450417592d1dea35c42'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-1-6-sample-and-tenants-domain-centric-adoption-2.md'
warnings: []
---

<intent-contract>

## Intent

**Problem:** Epic 1 now has the DomainService and ServiceDefaults SDK surfaces, but release and authoring governance can still drift: the manifest entries are not explicitly protected, active docs contain stale package-count wording, and guardrails do not yet cover every prohibited domain-module boilerplate seam.

**Approach:** Add focused package-manifest and authoring-governance tests, refresh active instructions/project context to match the 14-package manifest and domain-centric model, and keep the checks scoped to EventStore-owned source plus initialized domain roots without editing submodules.

## Boundaries & Constraints

**Always:** Keep `tools/release-packages.json` as the release inventory; keep `Hexalith.EventStore.DomainService` and `Hexalith.EventStore.ServiceDefaults` packable through the manifest; preserve the Sample as the clean reference domain module; keep interactive UI hosts free of generated or hand-written per-message MVC controllers; keep generated REST APIs in dedicated external API hosts.

**Block If:** Passing the guardrails requires editing `references/Hexalith.Tenants` source, changing release semantics outside the manifest scripts, publishing packages not listed in the manifest, or deciding the deferred Tenants/Gateway package-mode posture.

**Never:** Do not use `.sln`; do not run solution-level `dotnet test`; do not initialize nested submodules; do not remove Tenants' current bespoke `/project` allowance; do not broaden this story into Epic 3 package-mode dependency cleanup.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| SDK package inventory | Manifest is inspected by tests | `DomainService` and `ServiceDefaults` exist with exact project paths, unique IDs, and packable projects | Test failure names the missing or mismatched package entry |
| Manifest-only release output | Release config and scripts are inspected | Semantic-release delegates pack/validate to manifest scripts and validation rejects missing or extra `.nupkg` files | Script/test failure reports missing/extra packages |
| Stale package docs | Active instructions/docs mention obsolete package counts or omit `Gateway` | Tests fail until docs describe the manifest-driven 14-package set and include the current package names | Failure names the offending active doc |
| Domain boilerplate regression | Sample or another scanned domain root adds own `*.AppHost`, `*.Aspire`, `*.ServiceDefaults`, cursor codec, state-store wrapper, telemetry source, health check, or canonical endpoint mapping | Guardrail test fails and points to the platform seam to use instead | Current Tenants-specific `/project` pre-map remains allowed |
| UI controller leakage | Interactive UI host adds generated REST generator, MVC controller registration, or controller types | Guardrail test fails and instructs using a dedicated external API host plus EventStore client libraries | Existing UI hosts remain controller-free |

</intent-contract>

## Code Map

- `tools/release-packages.json` -- source of truth for manifest-governed package publication; already contains the SDK packages and `Gateway`.
- `tools/pack-release-packages.py` -- manifest-driven pack command with `UseHexalithProjectReferences=false`.
- `tools/validate-release-packages.py` -- exact release output validator for missing/extra `.nupkg` files.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- package-manifest and active-doc release governance tests to extend for Story 1.7.
- `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` -- structural guardrails for domain-centric authoring, Sample host shape, and UI-host controller separation.
- `src/Hexalith.EventStore.DomainService/Hexalith.EventStore.DomainService.csproj` -- domain-service host SDK package project; depends on Client and ServiceDefaults.
- `src/Hexalith.EventStore.ServiceDefaults/Hexalith.EventStore.ServiceDefaults.csproj` -- shared service defaults package project.
- `AGENTS.md` and `CLAUDE.md` -- active repository instructions with stale 13-package wording to update.
- `docs/reference/nuget-packages.md`, `docs/brownfield/project-overview.md`, `docs/brownfield/architecture.md`, `docs/brownfield/integration-architecture.md`, `docs/brownfield/development-guide.md` -- active package/authoring docs that should remain aligned with the domain-centric guidance.
- `_bmad-output/project-context.md` -- generated agent context with stale 6-package release wording to refresh.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- add explicit assertions that `Hexalith.EventStore.DomainService` and `Hexalith.EventStore.ServiceDefaults` are present at their expected paths, are unique, and point to packable projects -- prevents Story 1.7's SDK packages from silently dropping out of the manifest.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- tighten active-doc stale-count coverage so `13 packages` and older `6/8` release-count phrasing fail, and require active package docs/instructions to include the current manifest count/package names -- keeps release guidance aligned with `tools/release-packages.json`.
- [x] `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` -- update the domain-module project guardrail/commentary to include own `*.AppHost` plus `*.Aspire` and `*.ServiceDefaults` projects under scanned domain roots -- matches the current domain-centric rule.
- [x] `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` -- add structural checks for custom `IQueryCursorCodec`, custom `IReadModelStore`/state-store wrappers, custom `IHealthCheck`, new `ActivitySource`/`Meter`, and hand-mapped canonical SDK endpoints other than the allowed Tenants `/project` pre-map -- closes the anti-boilerplate gaps without forbidding normal use of platform abstractions.
- [x] `AGENTS.md`, `CLAUDE.md`, `docs/reference/nuget-packages.md`, `docs/brownfield/*`, and `_bmad-output/project-context.md` -- refresh active wording for the 14-package manifest, DomainService/ServiceDefaults package roles, external API host split, and anti-boilerplate rules -- gives future agents and maintainers the same policy the tests enforce.

**Acceptance Criteria:**
- Given the release package manifest is inspected, when package governance tests run, then `Hexalith.EventStore.DomainService` and `Hexalith.EventStore.ServiceDefaults` are explicitly required and no stale package-count docs pass.
- Given a scanned domain module reintroduces a covered platform-owned project, state, cursor, telemetry, health, or canonical endpoint seam, when DomainService guardrail tests run, then the failure explains the EventStore platform seam to use instead; broad transitional host wiring and cross-file/computed route resolution remain explicitly deferred.
- Given active repository instructions are read by a future domain author, when they follow the documented package and host guidance, then the domain-service host references DomainService for platform hosting, keeps optional contracts libraries contracts-only, and keeps generated REST controllers out of interactive UI hosts.
- Given release packaging is validated, when the manifest scripts run, then package output is reproducible from the manifest and packages outside that inventory are rejected.

## Spec Change Log

## Review Triage Log

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 8: (high 0, medium 5, low 3)
- defer: 1: (high 0, medium 1, low 0)
- reject: 2: (high 0, medium 0, low 2)
- addressed_findings:
  - `[medium]` `[patch]` The Tenants `/project` allowance accepted any `Program.cs` mapping; narrowed it to the known transitional `ProjectionDispatcher` path while keeping the Sample malformed-projection fault injector allowance.
  - `[medium]` `[patch]` State-store wrapper detection depended on narrow class names; expanded wrapper name markers and added DAPR state-method detection without flagging platform `IReadModelStore` use.
  - `[medium]` `[patch]` Per-domain telemetry detection missed qualified `System.Diagnostics.ActivitySource` and `Meter` construction; updated the regex guards to catch qualified constructors.
  - `[medium]` `[patch]` SDK package packability was inferred from raw XML; changed the package governance test to evaluate Release `IsPackable` and `PackageId` through MSBuild imports/targets.
  - `[medium]` `[patch]` Release packaging verification only used a dry-run; ran actual manifest packing and `validate-release-packages.py` against 14 generated `.nupkg` files.
  - `[low]` `[patch]` Custom cursor/read-model/health interface detection missed `struct` and `record struct` implementations; expanded declaration patterns.
  - `[low]` `[patch]` Canonical endpoint route detection missed common string-constant mappings; added constant-assignment route detection.
  - `[low]` `[patch]` Exact package-inventory doc alignment omitted the changed architecture document; added `docs/brownfield/architecture.md` to the manifest-name check.
- deferred_findings:
  - `[medium]` A broad ban on all domain-root DAPR/host wiring markers would currently fail Tenants transitional host composition; recorded in `deferred-work.md` for completion when Tenants remaining host wiring moves behind platform seams or gets a permanent exception.

### 2026-07-07 — Follow-up review pass
- intent_gap: 0
- bad_spec: 0
- patch: 13: (high 0, medium 9, low 4)
- defer: 1: (high 0, medium 1, low 0)
- reject: 1: (high 0, medium 0, low 1)
- addressed_findings:
  - `[medium]` `[patch]` Active package-count stale-wording checks only scanned a small curated list; broadened them to all active docs under `docs/` plus root instructions and project context, excluding generated API reference output.
  - `[low]` `[patch]` Stale release-count detection missed hyphenated and word-form counts; expanded the regex to catch `13-package`, `thirteen packages`, and equivalent 6/8 wording.
  - `[medium]` `[patch]` MSBuild property evaluation could hang or deadlock on redirected output; added a 60-second timeout and concurrent stdout/stderr reads.
  - `[medium]` `[patch]` DAPR state detection missed bulk, ETag, transaction, try-save, and direct actor-state APIs; expanded marker coverage and added `IStateManager`/`StateManager` invocation detection.
  - `[medium]` `[patch]` Source scans could flag prohibited markers that appeared only in comments or string literals; added comment/string-stripped scan inputs while preserving string literals for route detection.
  - `[medium]` `[patch]` Canonical endpoint route detection missed repeated constants, `MapGroup` composition, and non-Post/Get/Methods `Map*` calls; broadened same-file route detection.
  - `[medium]` `[patch]` The Tenants `/project` allowance was still file-wide; narrowed it to the matched mapping snippet that references `ProjectionDispatcher`.
  - `[medium]` `[patch]` The Sample malformed `/project` allowance was file-wide; narrowed it to the mapping inside the `malformedProjectionResponse` opt-in branch.
  - `[medium]` `[patch]` Active architecture and integration docs understated Tenants' transitional host composition; documented the current DAPR client, controller/query routing, CloudEvents/subscription mapping, and bespoke `/project` exceptions.
  - `[low]` `[patch]` The integration access-control summary still described `sample` and `tenants` as the same POST-only receiver; split Sample from Tenants' transitional endpoint posture.
  - `[medium]` `[patch]` `docs/brownfield/component-inventory.md` still omitted `/query` and `/project` from the DomainService SDK surface and described domain-owned `/project` mapping as current guidance; updated it to the canonical SDK endpoint model with Tenants' exception.
  - `[low]` `[patch]` `docs/reference/nuget-packages.md` listed `Gateway` in the overview but lacked a detailed install/role section; added the missing package section.
  - `[low]` `[patch]` The initial parallel validation attempt hit a shared `obj` file lock; reran the focused test suites sequentially and recorded the green outcomes.
- deferred_findings:
  - `[medium]` Same-file route hardening still does not resolve canonical endpoint route values imported from another type or computed variable; appended a new `deferred-work.md` entry for a future Roslyn-level or convention-level guardrail.
- rejected_findings:
  - `[low]` The sprint-status `done` versus spec `in-review` mismatch was a transient state created by the review workflow itself; finalization restores `status: done`.

### 2026-07-07 — Follow-up review pass
- intent_gap: 0
- bad_spec: 0
- patch: 9: (high 0, medium 8, low 1)
- defer: 0
- reject: 2: (high 0, medium 0, low 2)
- addressed_findings:
  - `[medium]` `[patch]` Grouped root endpoint mappings such as `MapGroup("/project").MapPost("", ...)` could bypass canonical endpoint guardrails; route detection now composes grouped routes with empty child routes.
  - `[medium]` `[patch]` `MapGroup` route detection combined unrelated group prefixes and child `Map*` calls across a file; group composition is now scoped to chained calls or calls on the matched group receiver.
  - `[medium]` `[patch]` Same-file route indirection still missed local/string variables, group prefix constants, and simple string concatenations; added a same-file string assignment resolver for direct and grouped route arguments.
  - `[medium]` `[patch]` Telemetry guardrails missed target-typed `ActivitySource`/`Meter` construction; updated constructor detection to catch `new(...)` assignments.
  - `[medium]` `[patch]` DAPR state access detection missed `GetStateEntryAsync`; added the marker and required invocation on a typed `DaprClient` receiver to avoid unrelated method false positives.
  - `[medium]` `[patch]` Actor state access detection missed common `IActorStateManager` methods; added add/contains/save/get-or-add markers and receiver-scoped invocation detection.
  - `[medium]` `[patch]` State-wrapper detection could flag recommended `IReadModelStore` injection; narrowed wrapper violations to files that also perform direct DAPR or actor state access.
  - `[medium]` `[patch]` Comment stripping could treat comment markers inside string literals as real comments and hide later prohibited code; changed comment removal to preserve string literals before removing comments.
  - `[low]` `[patch]` MSBuild package-property evaluation did not mirror release package mode; added `UseHexalithProjectReferences=false` to the evaluated Release property checks.

### 2026-07-07 — Follow-up review pass
- intent_gap: 0
- bad_spec: 0
- patch: 9: (high 0, medium 9, low 0)
- defer: 0
- reject: 3: (high 0, medium 0, low 3)
- addressed_findings:
  - `[medium]` `[patch]` The acceptance criterion overstated guardrail completeness relative to the already-recorded broad host-wiring and cross-file/computed route deferrals; narrowed the criterion to covered seams and named the explicit deferrals.
  - `[medium]` `[patch]` The own AppHost/Aspire/ServiceDefaults project guardrail was suffix-only; added csproj content detection for AppHost SDK and Aspire hosting package markers.
  - `[medium]` `[patch]` DAPR state access detection missed `QueryStateAsync` and generic state method invocations; added marker and generic-call support with focused helper coverage.
  - `[medium]` `[patch]` DAPR/actor state access detection missed null-conditional, null-forgiving, DI-resolved, and helper-returned receivers; added receiver-shape and call-result detection.
  - `[medium]` `[patch]` Canonical endpoint route detection missed named route arguments; added named-argument resolution and executable coverage.
  - `[medium]` `[patch]` Same-file route indirection missed multi-declarator string constants; added declarator splitting for simple comma-separated string assignments.
  - `[medium]` `[patch]` Grouped canonical endpoint detection missed multiline assignments, assigned nested groups, inline nested `MapGroup` chains, and empty child routes; added group-prefix composition with focused route-shape tests.
  - `[medium]` `[patch]` Raw string literal handling only recognized exactly triple-quoted strings and trimmed raw contents; expanded raw literal matching to 3+ quotes and decoded contents without trimming.
  - `[medium]` `[patch]` The Tenants `/project` allowance could be satisfied by `ProjectionDispatcher` inside a string literal; now the allowance checks the comment/string-stripped mapping snippet.

## Design Notes

The Tenants submodule remains evidence, not this story's write target. Existing Tenants bespoke host and `/project` behavior should be documented as transitional/non-canonical where needed, but the enforceable clean reference for new domains is the EventStore Sample plus the DomainService SDK package.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --filter FullyQualifiedName~ReleasePackageManifestTests` -- passed: 8/8.
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/ --filter FullyQualifiedName~DomainModuleAuthoringGuardrailTests` -- passed: 9/9.
- `python3 tools/pack-release-packages.py /tmp/hexalith-eventstore-packages 0.0.0-story-1-7 --dry-run` -- passed: listed the 14 manifest projects only and exited 0.
- `python3 tools/pack-release-packages.py /tmp/hexalith-eventstore-packages-story-1-7 0.0.0-story.1.7` -- passed: created 14 `.nupkg` files from the manifest.
- `python3 tools/validate-release-packages.py /tmp/hexalith-eventstore-packages-story-1-7 0.0.0-story.1.7` -- passed: validated 14 expected packages and no extras.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- passed: 0 warnings, 0 errors.
- `git diff --check` -- passed: no whitespace errors.

## Auto Run Result

Status: done

Summary:
- Added manifest governance tests that explicitly pin `Hexalith.EventStore.DomainService` and `Hexalith.EventStore.ServiceDefaults`, evaluate their Release `IsPackable` and `PackageId` through MSBuild, and require active package inventory docs to match the 14-package manifest.
- Expanded domain-module authoring guardrails for own `*.AppHost`/`*.Aspire`/`*.ServiceDefaults` projects, custom state/cursor/health/telemetry seams, canonical endpoint mapping, and UI-host controller leakage while narrowing the Sample and Tenants `/project` exceptions to their actual allowed mappings.
- Refreshed active instructions, brownfield docs, package docs, component inventory, and project context for the 14-package release set, `Gateway`, external API host split, Tenants' transitional host composition, and anti-boilerplate rules.

Files changed:
- `AGENTS.md` and `CLAUDE.md` -- updated manifest count, package names, and `Gateway` release guidance.
- `_bmad-output/project-context.md` -- refreshed release and domain-centric guardrail facts for future agents.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- recorded the remaining broad DAPR/host-wiring guardrail gap and the same-file-only endpoint route resolution residual risk.
- `docs/brownfield/architecture.md`, `docs/brownfield/component-inventory.md`, `docs/brownfield/development-guide.md`, `docs/brownfield/integration-architecture.md`, `docs/brownfield/project-overview.md`, `docs/reference/nuget-packages.md` -- aligned domain-authoring, package, Gateway, and Tenants transitional guidance.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- added explicit SDK package governance, active-doc manifest governance, broader stale-count scanning, and timeout-safe MSBuild property evaluation.
- `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` -- added and hardened structural domain-authoring guardrails for state access, source comments/strings, canonical route mapping, and scoped `/project` exceptions.

Review findings breakdown:
- Patches applied: 30 findings across the initial and follow-up review passes; this final follow-up pass applied 9 additional package-mode, route-detection, state-access, telemetry, and comment-scan guardrail fixes.
- Items deferred: 2 medium findings recorded in `deferred-work.md`.
- Items rejected: 5 low findings, including transient in-review sprint-status mismatches and a package-inventory-doc scope finding covered by stale-count scanning.
- Follow-up review recommended: true, because this final pass still produced multiple medium-severity guardrail changes.

Verification performed:
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --filter FullyQualifiedName~ReleasePackageManifestTests` -- passed: 8/8.
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/ --filter FullyQualifiedName~DomainModuleAuthoringGuardrailTests` -- passed: 9/9.
- `python3 tools/pack-release-packages.py /tmp/hexalith-eventstore-packages 0.0.0-story-1-7 --dry-run` -- passed.
- `python3 tools/pack-release-packages.py /tmp/hexalith-eventstore-packages-story-1-7 0.0.0-story.1.7` -- passed, created 14 packages.
- `python3 tools/validate-release-packages.py /tmp/hexalith-eventstore-packages-story-1-7 0.0.0-story.1.7` -- passed, validated 14 packages.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- passed: 0 warnings, 0 errors.
- `git diff --check` -- passed.
- Follow-up review reruns: `dotnet test tests/Hexalith.EventStore.DomainService.Tests/ --filter FullyQualifiedName~DomainModuleAuthoringGuardrailTests` -- passed: 9/9.
- Follow-up review reruns: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --filter FullyQualifiedName~ReleasePackageManifestTests` -- passed: 8/8.
- Final follow-up review rerun: `dotnet test tests/Hexalith.EventStore.DomainService.Tests/` -- passed: 69/69.
- Final follow-up review rerun: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` -- passed: 568/568.
- Final follow-up review build: `dotnet build Hexalith.EventStore.slnx --configuration Release` -- passed: 0 warnings, 0 errors.
- Final follow-up review check: `git diff --check` -- passed.

Residual risks:
- The initialized Tenants domain-service host still carries transitional DAPR/host composition, so broad direct-wiring guardrails remain deferred until that composition either moves fully behind platform seams or gets a permanent documented exception.
- Endpoint route detection is still a lightweight same-file scan; cross-file constants or computed route variables remain deferred for a future Roslyn-level or convention-level guardrail.

### Latest follow-up review pass — 2026-07-07

Status: done

Summary:
- Hardened DomainService authoring guardrails for review-found bypasses in state-access detection, AppHost/Aspire project detection, canonical route parsing, raw string handling, and the Tenants `/project` allowance.
- Narrowed the acceptance criterion so it matches the implemented guardrail coverage and explicitly preserves the already-deferred broad host-wiring and cross-file/computed route risks.

Files changed:
- `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` -- added focused helper coverage and hardened state, project, route, string-literal, group-route, and `/project` exception detection.
- `_bmad-output/implementation-artifacts/spec-1-7-domainservice-packaging-and-guardrails.md` -- recorded this follow-up review triage, clarified the acceptance criterion, and updated the latest run result.

Review findings breakdown:
- Patches applied: 9 medium findings.
- Items deferred: 0 new entries; `deferred-work.md` was not modified in this pass.
- Items rejected: 3 low findings, all workflow-state or scope noise.
- Follow-up review recommended: true, because this pass still made several medium-severity guardrail changes in shared governance logic.

Verification performed:
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/ --filter FullyQualifiedName~DomainModuleAuthoringGuardrailTests` -- passed: 23/23.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --filter FullyQualifiedName~ReleasePackageManifestTests` -- passed: 8/8.
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/` -- passed: 83/83.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- passed: 0 warnings, 0 errors.
- `git diff --check` -- passed.

Residual risks:
- Broad direct DAPR/host wiring and cross-file/computed canonical route resolution remain deferred in the existing ledger entries; no new deferred findings were added.
