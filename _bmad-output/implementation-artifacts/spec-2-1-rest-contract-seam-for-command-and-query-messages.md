---
title: '2.1 REST Contract Seam For Command And Query Messages'
type: 'feature'
created: '2026-07-07'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: false
baseline_revision: 'b33ca265fa575d12b72e4d71f5aaa91480ec7fc8'
final_revision: 'beb6b18727850c5d56117504fdefeee66e345905'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-2-context.md'
warnings: []
---

<intent-contract>

## Intent

**Problem:** The REST contract seam from the earlier generator work exists, but Story 2.1 needs it hardened as the Epic 2 baseline: command/query contracts must expose explicit REST metadata, referenced-contract scoping must remain fail-closed, and Contracts tests must cover the seam rather than relying on later generator tests.

**Approach:** Keep this story limited to the Contracts package and its unit tests. Preserve existing public semantics, tighten only metadata normalization that prevents accidental referenced-contract mismatches, add missing Rest query-binding coverage, and clean focused style drift so the seam is reviewable before generator and external API-host stories build on it.

## Boundaries & Constraints

**Always:** Use `Hexalith.EventStore.slnx` only for restore/build; run tests by project. Preserve `ICommandContract` static `Domain`/`CommandType` plus instance `AggregateId`, existing `IQueryContract` behavior, `RestRouteAttribute.ApiScope` trimming/null normalization, and the minimal contract-layer validation model where route shape validation stays in the generator.

**Block If:** A change would require renaming public seam members, removing existing enum values, changing command/query identity rules, changing generator controller emission, or modifying Sample/Tenants/submodule code.

**Never:** Do not implement Story 2.2 generator behavior, Sample/Tenants external API hosts, UI host changes, query metadata propagation, REST generator hardening backlog diagnostics, or domain-service runtime plumbing in this story.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Command seam | A command record implements `ICommandContract` with static domain/type and projected aggregate id | Static metadata and instance `AggregateId` remain accessible through compile-time and interface usage | No error expected |
| Route scope normalization | `RestRouteAttribute.ApiScope` is set to null, empty, whitespace, or padded text | Null/empty/whitespace become null; padded text is trimmed | No exception for optional scope |
| API host tag normalization | `RestApiAttribute` receives null, empty, whitespace, or padded tag text | Null/empty/whitespace become null; padded text is trimmed for stable OpenAPI tag and referenced-contract scope comparison | No exception for optional tag |
| Query binding validation | `RestQueryBindingAttribute` uses `Constant` or `Route` sources with values, or invalid `None` aggregate source / missing values | Valid sources preserve values; unsupported aggregate source or missing required values throws | Throw `ArgumentOutOfRangeException` for unsupported aggregate source and `ArgumentException` for required missing values |

</intent-contract>

## Code Map

- `src/Hexalith.EventStore.Contracts/Commands/ICommandContract.cs` -- command marker interface for generated REST/gateway command identity.
- `src/Hexalith.EventStore.Contracts/Queries/IQueryContract.cs` -- existing query marker that must remain backward compatible.
- `src/Hexalith.EventStore.Contracts/Rest/RestApiAttribute.cs` -- assembly-level generated REST opt-in and tenant-source/tag metadata.
- `src/Hexalith.EventStore.Contracts/Rest/RestRouteAttribute.cs` -- per-contract HTTP verb/template metadata and referenced-contract `ApiScope`.
- `src/Hexalith.EventStore.Contracts/Rest/RestQueryBindingAttribute.cs` -- query envelope aggregate/entity binding metadata used by generated REST query actions.
- `tests/Hexalith.EventStore.Contracts.Tests/Commands/ICommandContractTests.cs` -- command seam unit coverage.
- `tests/Hexalith.EventStore.Contracts.Tests/Queries/IQueryContractTests.cs` -- query seam backward-compatibility coverage.
- `tests/Hexalith.EventStore.Contracts.Tests/Rest/RestApiAttributeTests.cs` -- API opt-in metadata tests.
- `tests/Hexalith.EventStore.Contracts.Tests/Rest/RestRouteAttributeTests.cs` -- route metadata and `ApiScope` tests.
- `tests/Hexalith.EventStore.Contracts.Tests/Rest/RestQueryBindingAttributeTests.cs` -- new focused tests for query binding metadata validation.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.Contracts/Commands/ICommandContract.cs` -- preserve the public command seam and reformat focused style drift to repo Allman conventions -- keeps Story 2.1's command identity contract stable and reviewable.
- [x] `src/Hexalith.EventStore.Contracts/Queries/IQueryContract.cs` -- preserve query marker behavior and reformat focused style drift without semantic changes -- protects the required backward compatibility.
- [x] `src/Hexalith.EventStore.Contracts/Rest/RestApiAttribute.cs` -- normalize optional `Tag` like `RestRouteAttribute.ApiScope` while leaving route-prefix validation minimal -- prevents whitespace-scoped referenced-contract mismatches without moving generator validation into Contracts.
- [x] `src/Hexalith.EventStore.Contracts/Rest/RestRouteAttribute.cs` and `src/Hexalith.EventStore.Contracts/Rest/RestQueryBindingAttribute.cs` -- preserve existing public behavior and align focused formatting to repo conventions -- avoids broad generator-hardening creep.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Commands/ICommandContractTests.cs`, `tests/Hexalith.EventStore.Contracts.Tests/Queries/IQueryContractTests.cs`, `tests/Hexalith.EventStore.Contracts.Tests/Rest/RestApiAttributeTests.cs`, and `tests/Hexalith.EventStore.Contracts.Tests/Rest/RestRouteAttributeTests.cs` -- add/adjust coverage for the I/O matrix and clean local style drift -- proves command marker, query compatibility, tenant-source, route metadata, and tag/scope normalization.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Rest/RestQueryBindingAttributeTests.cs` -- add focused unit coverage for valid constant/route bindings and invalid aggregate/value cases -- closes the missing Contracts-level query binding seam coverage.

**Acceptance Criteria:**
- Given a command intended for generated REST exposure, when it implements `ICommandContract`, then tests prove static `Domain`/`CommandType` and instance `AggregateId` are accessible and unchanged.
- Given existing query contracts, when Contracts tests run, then `IQueryContract` static `QueryType`/`Domain`/`ProjectionType` behavior and metadata record behavior remain backward compatible.
- Given route metadata is applied, when `RestRouteAttribute` is constructed and `ApiScope` is assigned, then verb/template values are preserved, optional scope is normalized, and null template remains the only attribute-layer route-template error.
- Given an external API host applies `RestApiAttribute`, when route prefix, tag, and tenant source are supplied, then the generator-facing properties expose the route prefix, normalized optional tag, and tenant source options.
- Given query binding metadata is configured, when Contracts tests exercise valid and invalid binding source/value combinations, then the contract layer preserves valid aggregate/entity binding values and rejects unsupported or missing required metadata.

## Spec Change Log

## Review Triage Log

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 5: (high 0, medium 1, low 4)
- defer: 3: (high 0, medium 3, low 0)
- reject: 2: (high 0, medium 0, low 2)
- addressed_findings:
  - `[medium]` `[patch]` Generator read raw `RestApiAttribute.Tag` constructor text, so runtime tag normalization did not stabilize referenced-contract scope matching. Patched `RestApiAttributeParser` to trim/null-normalize the generator option and added manifest/referenced-contract tests with padded host tags.
  - `[low]` `[patch]` `RestQueryBindingAttribute` lacked enum and `AttributeUsage` tests. Added focused coverage for all `RestQueryBindingSource` values and class-only, non-multiple, non-inherited usage.
  - `[low]` `[patch]` Edited `RestApiAttribute.cs` still contained both `RestTenantSource` and `RestApiAttribute`. Split `RestTenantSource` into its own file.
  - `[low]` `[patch]` Edited command seam tests kept command stub records in the test class file. Split `CreateCounter` and `CreateTenant` into focused test stub files.
  - `[low]` `[patch]` Edited query seam tests kept query stub classes in the test class file. Split `GetCounterStatusQuery` and `GetOrderSummaryQuery` into focused test stub files.

## Design Notes

The contract layer intentionally validates only values it owns. Route-template syntax, unsupported contract shapes, duplicate JSON names, route/body reconciliation, and generated controller behavior remain generator responsibilities for Story 2.2 and the REST generator hardening backlog. `RestApiAttribute.Tag` is part OpenAPI metadata and part referenced-contract scope key, so whitespace normalization belongs in Story 2.1 because it stabilizes the seam without changing route-shape semantics.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` -- expected: Contracts unit tests pass.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- expected: solution builds with warnings as errors; do not use `.sln`.

## Auto Run Result

Status: done

Summary:
- Hardened the REST contract seam baseline by normalizing `RestApiAttribute.Tag` in both runtime construction and the generator's Roslyn parser path.
- Preserved `ICommandContract` and `IQueryContract` public shapes while cleaning focused Allman-style drift.
- Added missing `RestQueryBindingAttribute` contract tests and generator regression coverage for padded host tags used in referenced-contract scope filtering.

Files changed:
- `src/Hexalith.EventStore.Contracts/Commands/ICommandContract.cs` -- formatting-only Allman brace cleanup for the command marker.
- `src/Hexalith.EventStore.Contracts/Queries/IQueryContract.cs` -- formatting-only cleanup and leading blank-line removal.
- `src/Hexalith.EventStore.Contracts/Rest/RestApiAttribute.cs` -- normalized optional `Tag` and split `RestTenantSource`.
- `src/Hexalith.EventStore.Contracts/Rest/RestTenantSource.cs` -- moved tenant-source enum to its own file.
- `src/Hexalith.EventStore.RestApi.Generators/RestApiAttributeParser.cs` -- normalized generator-read REST API tags before manifest/controller generation.
- `tests/Hexalith.EventStore.Contracts.Tests/Commands/*` -- split command test stubs and added static-interface generic constraint coverage.
- `tests/Hexalith.EventStore.Contracts.Tests/Queries/*` -- split query test stubs and added static-interface generic constraint coverage.
- `tests/Hexalith.EventStore.Contracts.Tests/Rest/RestApiAttributeTests.cs` -- covered optional tag normalization.
- `tests/Hexalith.EventStore.Contracts.Tests/Rest/RestRouteAttributeTests.cs` -- covered null `ApiScope` normalization.
- `tests/Hexalith.EventStore.Contracts.Tests/Rest/RestQueryBindingAttributeTests.cs` -- added binding source/value and metadata attribute tests.
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiManifestGenerationTests.cs` -- covered padded host tag manifest normalization.
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs` -- covered padded host tag referenced-contract filtering.
- `_bmad-output/implementation-artifacts/epic-2-context.md` -- compiled Epic 2 planning context for the workflow.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- recorded deferred generator hardening items surfaced by review.

Review findings breakdown:
- Patches applied: 5 (1 medium, 4 low).
- Deferred: 3 medium generator-hardening issues for `RestQueryBindingAttribute`/tenant-source invalid metadata.
- Rejected: 2 low workflow/template observations that are resolved by finalize semantics.

Follow-up review recommendation: false. The review-driven changes were localized, covered by targeted tests, and did not alter controller emission beyond trimming the existing tag option before use.

Verification performed:
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` -- passed, 591 passed.
- `dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/` -- passed, 76 passed.
- `git diff --check` -- passed.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- passed with 0 warnings and 0 errors.

Residual risks:
- Deferred generator-hardening items remain for invalid `RestQueryBindingAttribute` value/source combinations and undefined `RestTenantSource` metadata.
