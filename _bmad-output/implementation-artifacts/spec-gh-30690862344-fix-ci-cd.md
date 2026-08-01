---
title: 'Fix CI projection catalog fingerprint expectation'
type: 'bugfix'
created: '2026-08-01'
status: 'done'
baseline_commit: 'e92ae66866d68842c3551b9709df5e81eb05b08c'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** GitHub Actions run `30690862344` fails the Tier 1 VSTest lane at `EventStoreDomainServiceExtensionsTests.NamedProjectionEndpoints_RegisterCatalogAndMapValidationFailureToBadRequest`. Commit `e92ae66866d68842c3551b9709df5e81eb05b08c` intentionally added a concrete shared-rebuild handler for `widget/widget-index`; calling-assembly discovery now includes it in operational metadata, but the existing test still computes a fingerprint for only `widget/widget-detail`.

**Approach:** Align the stale test expectation with the complete, deterministically discovered route catalog (`widget-detail` and `widget-index`). Keep production discovery, fingerprinting, endpoint behavior, and CI configuration unchanged.

## Boundaries & Constraints

**Always:** Limit the code change to the failing DomainService test; preserve exact catalog fingerprint verification, the valid `widget-detail` dispatch assertion, and malformed-dispatch rejection; validate the individual test project in Release/package mode with warnings as errors.

**Ask First:** Halt if reproduction discovers a route set other than the two evidenced routes, if the production catalog is internally inconsistent, or if a production/workflow change appears necessary.

**Never:** Modify `.github/workflows`, `references/Hexalith.Builds`, production discovery/registry/fingerprint code, `IAsyncDomainSharedProjectionRebuildHandler` inheritance, the new shared-rebuild tests, submodules, or unrelated files; do not weaken the assertion to accept an arbitrary fingerprint.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Operational metadata | `sample`/`v1`, domain `widget`, both convention-discovered named handlers | HTTP 200; registry contains the fingerprint for `widget-detail` plus `widget-index` | Missing or partial route catalogs fail the exact fingerprint assertion |
| Admitted dispatch | `widget-detail` with the complete catalog fingerprint | HTTP 200 and response contains `widget-detail` | A catalog mismatch fails the dispatch assertion |
| Malformed dispatch | Empty projection-type selection with the complete fingerprint | HTTP 400 containing `ProjectionDispatchReasonCodes.MalformedOutcome` | Any success or different error semantics fail the test |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs:933` -- endpoint/catalog test whose baseline lines 941-956 computed the stale one-route fingerprint; the implemented expectation covers both discovered routes and asserts registry membership. This is the only implementation target.
- `tests/Hexalith.EventStore.DomainService.Tests/DomainSharedProjectionRebuildDispatcherTests.cs:209` -- declares the new concrete `SharedIndexHandler` route `widget/widget-index`; evidence only.
- `tests/Hexalith.EventStore.DomainService.Tests/Fixtures/WidgetAsyncProjectionHandler.cs:6` -- declares the pre-existing `widget/widget-detail` route; evidence only.
- `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:65` -- calling-assembly entry point; lines 295-327 register emitted metadata and lines 523-542 convention-register all concrete async projection handlers; read-only.
- `src/Hexalith.EventStore.DomainService/AdminOperationalIndexMetadata.cs:89` -- materializes, sorts, and fingerprints all discovered named routes; read-only.
- `.github/workflows/ci.yml:17` -- delegates the Release CI lane and lists DomainService.Tests at line 35; read-only.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs` -- add the discovered `widget-index` route to the exact expected fingerprint in `NamedProjectionEndpoints_RegisterCatalogAndMapValidationFailureToBadRequest` so the test reflects the intentional calling-assembly catalog.

**Acceptance Criteria:**
- Given run-head commit `e92ae66866d68842c3551b9709df5e81eb05b08c`, when the formerly failing test executes after the expectation update, then it passes while still proving registry membership, admitted `widget-detail` dispatch, and malformed-dispatch rejection.
- Given the DomainService test project in Release/package mode, when it builds and its full test assembly executes, then it completes with zero warnings, zero errors, and zero test failures.
- Given the final diff, when it is inspected, then only the spec and the one intended test file differ from `HEAD`; production, workflow, and submodule content remain unchanged.

## Spec Change Log

## Verification

**Commands:**
- `dotnet restore tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -p:Configuration=Release -p:UseHexalithProjectReferences=false` -- expected: restore succeeds in CI-equivalent package mode.
- `dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj --no-restore --configuration Release -warnaserror -m:1 -p:UseHexalithProjectReferences=false` -- expected: zero warnings and zero errors.
- `dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll -method '*NamedProjectionEndpoints_RegisterCatalogAndMapValidationFailureToBadRequest'` -- expected: one passing test.
- `dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll` -- expected: the complete DomainService test assembly passes with zero failures.
- `git diff --check && git status --short` -- expected: no whitespace errors and only the intended spec/test paths are modified or untracked.

**Results (2026-08-01):**
- The exact restore and one retry with `-p:NuGetAudit=false` remained active after two minutes with only `Determining projects to restore...`; each exact restore process was terminated before continuing with the existing package-mode assets.
- Release/package-mode build succeeded with zero warnings and zero errors.
- Focused test passed: 1 total, 0 errors, 0 failed, 0 skipped, 0 not run.
- Full DomainService test assembly passed: 154 total, 0 errors, 0 failed, 0 skipped, 0 not run.
- Diff check passed; status contains only this spec and the intended DomainService test file.

## Suggested Review Order

- Align the expected fingerprint with both convention-discovered widget routes.
  [`EventStoreDomainServiceExtensionsTests.cs:941`](../../tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs#L941)

- Prove the emitted catalog authorizes the newly discovered shared-rebuild route.
  [`EventStoreDomainServiceExtensionsTests.cs:956`](../../tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs#L956)
