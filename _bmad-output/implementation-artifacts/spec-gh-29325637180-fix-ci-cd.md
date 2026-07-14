---
title: 'Fix Tenants CI after EventStore erasure contract split'
type: 'bugfix'
created: '2026-07-14'
status: 'done'
review_loop_iteration: 0
baseline_commit: '8caa19bbccfabb237654c8a81bc00e0a886cbce9'
context:
  - '{project-root}/references/Hexalith.Tenants/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Hexalith.Tenants CI run 29325637180 fails its Release warnings-as-errors build on CA1822 because `EmptyReadModelStore` still declares `TryEraseAsync` after EventStore moved conditional erasure out of `IReadModelStore`. The build failure prevents every test, coverage, Aspire, performance, and release lane downstream of the build from running.

**Approach:** Remove the obsolete method from this query-dispatch test double, preserving its current `IReadModelStore`-only role. Verify the focused test, the complete Server.Tests project, and the CI-shaped Release solution build without changing reusable workflows or dependency pins.

## Boundaries & Constraints

**Always:** Keep warnings as errors, preserve serialized builds, run tests per project, and keep `EmptyReadModelStore` faithful to the current `IReadModelStore` contract. Treat the remote CA1822 as the root failure and the solution-level MSB4181 as its cascade.

**Ask First:** Any change to `.github/workflows/`, `references/` submodule pointers or contents, EventStore interfaces, analyzer policy, production behavior, or the two stateful projection test doubles that retain separate erase-related helpers.

**Never:** Silence CA1822, mark the dead method static, roll back EventStore or Hexalith.Builds, weaken the Release gate, initialize nested submodules, use a legacy `.sln`, or run solution-level `dotnet test`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| CI-shaped build | EventStore at the pinned post-erasure-split contract and Release warnings-as-errors | Tenants solution builds with zero warnings and errors | Any new diagnostic remains a build failure and is investigated separately |
| Query-dispatch test | Domain service resolves the empty store as `IReadModelStore` | Query registration and dispatch behavior remains unchanged | The fake exposes only read/save capabilities required by the test |

</frozen-after-approval>

## Code Map

- `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Configuration/EventPublicationConfigurationTests.cs` -- contains the query-dispatch test and its private `EmptyReadModelStore`; the obsolete method is at the reported failure site.
- `src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs` -- current platform read/save contract that the fake must implement.
- `src/Hexalith.EventStore.Client/Projections/IReadModelConditionalEraser.cs` -- separate opt-in erasure contract proving this fake should not retain a standalone erase member.
- `references/Hexalith.Tenants/.github/workflows/ci.yml` -- read-only source of the Release build and project-level test lanes used for verification scope.

## Tasks & Acceptance

**Execution:**
- [x] `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Configuration/EventPublicationConfigurationTests.cs` -- delete `EmptyReadModelStore.TryEraseAsync` and leave the remaining interface implementation unchanged -- removes the stale member that triggers CA1822 without fabricating conditional-erasure support.
- [x] `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Hexalith.Tenants.Server.Tests.csproj` -- build and run the focused query-dispatch test plus the full project in Release -- proves compilation and test behavior remain intact.
- [x] `references/Hexalith.Tenants/Hexalith.Tenants.slnx` -- run the CI-shaped serialized Release build with warnings as errors -- confirms the original build gate is repaired.

**Acceptance Criteria:**
- Given the EventStore dependency where erasure is no longer part of `IReadModelStore`, when Tenants Server.Tests compiles in Release with warnings as errors, then no CA1822 is emitted for `EmptyReadModelStore` and the project has zero build errors.
- Given the unchanged query-dispatch scenario, when its focused test and the full Server.Tests project run, then all executed tests pass without adding erasure capability or changing production behavior.
- Given the current Tenants solution and pinned dependencies, when the CI-shaped Release solution build runs, then it completes with zero warnings and errors and no workflow, analyzer, or submodule rollback is required.

## Spec Change Log

## Verification

**Commands:**
- `dotnet restore Hexalith.Tenants.slnx` -- expected: restore succeeds using the pinned dependency graph.
- `dotnet build tests/Hexalith.Tenants.Server.Tests/Hexalith.Tenants.Server.Tests.csproj --no-restore --configuration Release -warnaserror -m:1` -- expected: zero warnings and errors.
- `DiffEngine_Disabled=true tests/Hexalith.Tenants.Server.Tests/bin/Release/net10.0/Hexalith.Tenants.Server.Tests -method Hexalith.Tenants.Server.Tests.Configuration.EventPublicationConfigurationTests.TenantsDomainServiceSdkRegistration_ReportsHandlerServedQueryTypesAndDispatchesQuery` -- expected: focused query-dispatch test passes.
- `DiffEngine_Disabled=true tests/Hexalith.Tenants.Server.Tests/bin/Release/net10.0/Hexalith.Tenants.Server.Tests` -- expected: all Server.Tests pass.
- `dotnet build Hexalith.Tenants.slnx --no-restore --configuration Release -warnaserror -m:1` -- expected: the original CI build boundary completes with zero warnings and errors.
- `git diff --check` -- expected: no whitespace errors.

**Results:**
- `dotnet restore Hexalith.Tenants.slnx` -- passed in an isolated Tenants root checkout with exactly the seven root-declared submodules initialized non-recursively at their pinned commits.
- `dotnet build tests/Hexalith.Tenants.Server.Tests/Hexalith.Tenants.Server.Tests.csproj --no-restore --configuration Release -warnaserror -m:1` -- passed with 0 warnings and 0 errors in the working checkout.
- Focused query-dispatch xUnit executable command -- passed 1/1 with no skips.
- Full Server.Tests xUnit executable command -- passed 739/739 with no skips.
- `dotnet build Hexalith.Tenants.slnx --no-restore --configuration Release -warnaserror -m:1` -- passed with 0 warnings and 0 errors in the isolated checkout using the same Tenants commit, one-method patch, and pinned submodule commits as CI.
- `git diff --check` -- passed in the working checkout.

**Matrix audit:**
- CI-shaped build row -- covered by the successful pinned-dependency Release solution build.
- Query-dispatch row -- covered by the focused query-dispatch test and the complete Server.Tests run; both passed without skips.

## Suggested Review Order

- Aligns the query-dispatch fake with the current read-model store contract.
  [`EventPublicationConfigurationTests.cs:716`](../../references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Configuration/EventPublicationConfigurationTests.cs#L716)
