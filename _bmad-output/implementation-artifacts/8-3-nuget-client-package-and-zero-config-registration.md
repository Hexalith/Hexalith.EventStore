# Story 8.3: NuGet Client Package & Zero-Config Registration

Status: done

## Story

As a domain service developer,
I want to install EventStore client packages via NuGet with zero-configuration quickstart,
so that I can register my domain service with a single extension method call.

## Acceptance Criteria

1. **Given** the EventStore client NuGet package,
   **When** a developer adds it to their project and calls `AddEventStore()` with no arguments,
   **Then** convention-based registration discovers all `EventStoreAggregate<T>` and `EventStoreProjection<T>` types in the calling assembly without manual listing (FR42, UX-DR17)
   **And** `UseEventStore()` populates `EventStoreActivationContext` with correct domain names and DAPR resource names
   **And** `GetRequiredKeyedService<IDomainProcessor>(domainName)` resolves the correct aggregate type.

2. **Given** all public types in the Client and Contracts NuGet packages,
   **When** the packages are built with `GenerateDocumentationFile=true`,
   **Then** every public class, interface, record, and enum has XML `<summary>` documentation (UX-DR19)
   **And** IntelliSense shows useful descriptions for all public types when consumed via NuGet.

3. **Given** the Client and Contracts package public API surface,
   **When** audited for minimal exposure,
   **Then** only domain-service-developer-facing types are public (UX-DR20)
   **And** internal pipeline types (e.g., state rehydrator, assembly scanner) are `internal` with `InternalsVisibleTo` for framework and test projects.

4. **Given** all Tier 1 test suites,
   **When** executed after this story's changes,
   **Then** all tests in Client.Tests, Contracts.Tests, Sample.Tests, and Testing.Tests pass with zero regressions.

## Context: What Already Exists

The Client NuGet package is **already substantially implemented** with a mature codebase. This story validates completeness, verifies the zero-config developer experience end-to-end, and closes any remaining gaps in XML documentation and public API surface.

### Existing Client SDK (`src/Hexalith.EventStore.Client/`)

- **Registration**: `AddEventStore()` with 4 overloads (zero-arg via `Assembly.GetCallingAssembly()`, with options, explicit assemblies, both). `AddEventStoreClient<T>()` for manual single-processor registration.
- **Discovery**: `AssemblyScanner` with `ConcurrentDictionary` caching, cross-assembly duplicate detection, reflection-based discovery of `EventStoreAggregate<TState>` and `EventStoreProjection<TReadModel>` subclasses.
- **Conventions**: `NamingConventionEngine` — PascalCase-to-kebab-case, suffix stripping (`Aggregate`, `Projection`, `Processor`), attribute override via `[EventStoreDomain]`, kebab-case validation regex, DAPR resource name helpers (`GetStateStoreName`, `GetPubSubTopic`, etc.).
- **Activation**: `UseEventStore()` with 5-layer cascade configuration (convention defaults, global options, domain self-config, external config, explicit override).
- **DI**: Keyed scoped registration per domain name + non-keyed scoped for backward compatibility. Projections with post-construction initialization (Notifier, Logger).
- **Base classes**: `EventStoreAggregate<TState>` with reflection-based Handle/Apply method discovery, `EventStoreProjection<TReadModel>`, `DomainProcessorBase<TState>`.
- **State rehydration**: `DomainProcessorStateRehydrator` handling JsonElement, DomainServiceCurrentState, IEnumerable, and raw typed state.
- **Package config**: `.csproj` has `<GenerateDocumentationFile>true</GenerateDocumentationFile>`, `Description`, `InternalsVisibleTo` for tests.

### Existing Tests (`tests/Hexalith.EventStore.Client.Tests/`)

- `AddEventStoreTests.cs` — 18 tests: zero-config discovery, explicit assemblies, options config, idempotency, keyed registration
- `UseEventStoreTests.cs` — 16 tests: activation context, cascade resolution, diagnostics, throws-without-AddEventStore
- `EventStoreAggregateTests.cs` — 54 tests: command dispatch, state rehydration, Handle discovery, Apply methods
- `DomainProcessorTests.cs` — 8 tests: typed state casting, JsonElement deserialization
- `ServiceCollectionExtensionsTests.cs` — 5 tests: `AddEventStoreClient<T>()` registration
- `AssemblyScannerTests.cs` — scanner edge cases
- `NamingConventionEngineTests.cs` — kebab-case conversion, suffix stripping, attribute override
- `CascadeConfigurationTests.cs` — 5-layer priority
- `EventStoreDomainAttributeTests.cs` — attribute validation
- `QueryContractResolverTests.cs` — query type resolution

### Existing Integration Tests (`tests/Hexalith.EventStore.Sample.Tests/`)

- `FluentApiRegistrationIntegrationTests.cs` — 13 tests: real Counter sample discovery-to-activation workflow, Layer 2-5 cascade, AppSettings, backward compat

### What This Story Must Complete

1. **Verify XML documentation** on all public types in Client and Contracts packages (UX-DR19)
2. **Verify minimal public API surface** — only developer-facing types public, internal pipeline types `internal` (UX-DR20)
3. **Validate zero-config quickstart** — `AddEventStore()` with no args discovers all domain types in calling assembly
4. **Validate NuGet packaging** — packages are properly configured for publish (metadata, readme, dependencies)
5. **Add missing tests** for any uncovered zero-config scenarios
6. **Verify all existing Client and Sample tests pass** — zero regressions

## Tasks / Subtasks

- [x] Task 1: Audit public API surface for minimal exposure (AC: #3)
  - [x] 1.1 Scan all `.cs` files in `src/Hexalith.EventStore.Client/` for all public type declarations: `public class`, `public record`, `public struct`, `public enum`, `public interface`, `public static class`, and `public delegate`. Identify types that should be `internal` because they are NOT needed by domain service developers. The following MUST be public: `EventStoreAggregate<T>`, `EventStoreProjection<T>`, `IDomainProcessor`, `DomainProcessorBase<T>`, `EventStoreDomainAttribute`, `DomainResult` (Contracts), `IEventPayload` (Contracts), `IRejectionEvent` (Contracts), `CommandEnvelope` (Contracts), `EventStoreOptions`, `EventStoreDomainOptions`, `AddEventStore()` extensions, `UseEventStore()` extensions, `NamingConventionEngine` (public for advanced users), `IProjectionChangeNotifier`, `IProjectionChangedBroadcaster`, `IEventStoreProjection`, `DiscoveryResult`, `DiscoveredDomain`, `DomainKind`, `EventStoreActivationContext`, `EventStoreDomainActivation`, `QueryContractResolver`
  - [x] 1.2 For any internal-only types found `public`, follow this **strict order of operations**: (1) Check if the type is already `internal` — if so, document in Completion Notes and proceed, no changes needed. (2) Grep `src/Hexalith.EventStore.Server/` and `tests/Hexalith.EventStore.Sample.Tests/` for direct references to the type. (3) If referenced by Server, add `<InternalsVisibleTo Include="Hexalith.EventStore.Server" />` to Client.csproj FIRST. Verify existing `InternalsVisibleTo` for `Hexalith.EventStore.Client.Tests` is present. (4) THEN change visibility to `internal`. (5) Build to verify no breaks. **Safety rule: when in doubt, leave the type `public` — making a public type internal is a breaking change for external NuGet consumers. Err on the side of caution.** Candidates — these are the **ONLY two candidates** for `internal`; do not make any other types `internal` (the MUST-be-public list in Task 1.1 is comprehensive): `DomainProcessorStateRehydrator` (implementation detail, consumed only by base classes), `AssemblyScanner` (consumed only by `AddEventStore()`)

- [x] Task 2: Audit XML documentation on public types (AC: #2)
  - [x] 2.1 Verify `<GenerateDocumentationFile>true</GenerateDocumentationFile>` is set in BOTH `Hexalith.EventStore.Client.csproj` (already present) AND `Hexalith.EventStore.Contracts.csproj`. **If missing from Contracts.csproj, add it NOW** — the build-based validation in 2.3 depends on this being present in both packages.
  - [x] 2.2 **Before trusting the build as XML doc validation**, verify CS1591 is NOT suppressed in `<NoWarn>` for the default (non-`ApiReferenceBuild`) build configuration. Check `.editorconfig`, `Directory.Build.props`, and individual `.csproj` files. `Directory.Build.props` only suppresses CS1591 when `ApiReferenceBuild=true` — confirm that condition is NOT set by default.
  - [x] 2.3 Run `dotnet build src/Hexalith.EventStore.Client/ --configuration Release` and `dotnet build src/Hexalith.EventStore.Contracts/ --configuration Release`. If CS1591 warnings/errors appear, add missing XML docs. If build succeeds with zero warnings, XML docs are complete.
  - [x] 2.4 For any public types missing XML docs, add concise `<summary>` tags that describe the type's purpose for a domain service developer consuming the NuGet package

- [x] Task 3: Validate NuGet package configuration (AC: #1, #2)
  - [x] 3.1 Run `dotnet pack src/Hexalith.EventStore.Client/ --configuration Release` and `dotnet pack src/Hexalith.EventStore.Contracts/ --configuration Release`. If both produce `.nupkg` files successfully, the NuGet metadata (Authors, License, Description, README, XML docs) is validated — these are already configured in `Directory.Build.props` and individual `.csproj` files. The CI/CD pipeline (`release.yml`) validates package count on every release.
  - [x] 3.2 Spot-check: verify `README.md` exists at repo root (already confirmed in `Directory.Build.props` via `<PackageReadmeFile>`) and both `.csproj` files have meaningful `<Description>` tags.

- [x] Task 4: Add zero-config quickstart validation tests IF NOT ALREADY COVERED (AC: #1, #4)
  - [x] 4.1 **FIRST:** Review existing tests in `AddEventStoreTests.cs` and `FluentApiRegistrationIntegrationTests.cs` to determine which zero-config scenarios are already covered. Only add tests for scenarios NOT already tested. If the zero-config path (no-arg discovery → activation → keyed resolution) is fully covered, document the existing coverage in Completion Notes and skip new test creation.
  - [x] 4.2 If gaps exist, add test `tests/Hexalith.EventStore.Client.Tests/Registration/ZeroConfigQuickstartTests.cs` with ONLY the uncovered scenarios from:
    - `AddEventStore_NoArgs_DiscoversTypesInCallingAssembly` — verify that `AddEventStore()` with no arguments finds domain types defined in the test assembly (use test stub aggregates already in `AssemblyScannerSmokeStubs.cs`)
    - `AddEventStore_ThenUseEventStore_ProducesActivationContext` — verify the full `AddEventStore()` → build host → `UseEventStore()` flow produces a populated `EventStoreActivationContext` with correct domain names and DAPR resource names
    - `AddEventStore_ThenResolveKeyedService_ReturnsDomainProcessor` — verify `GetRequiredKeyedService<IDomainProcessor>(domainName)` returns the correct aggregate type
  - [x] 4.3 If `AssemblyScannerSmokeStubs.cs` already contains suitable test aggregate types, reuse them. If not, add a minimal `TestAggregate : EventStoreAggregate<TestState>` stub in the same file.

- [x] Task 5: Validate existing tests — zero regressions (AC: #4)
  - [x] 5.1 Run `dotnet test tests/Hexalith.EventStore.Client.Tests/` — all tests must pass
  - [x] 5.2 Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` — all tests must pass
  - [x] 5.3 Run `dotnet test tests/Hexalith.EventStore.Sample.Tests/` — all tests must pass (including Story 8.2 changes if merged)
  - [x] 5.4 Run `dotnet test tests/Hexalith.EventStore.Testing.Tests/` — all tests must pass
  - [x] 5.5 If any test failures are caused by this story's changes (e.g., visibility changes from Task 1), fix them. Pre-existing failures unrelated to this story should be documented but NOT fixed.

- [x] Task 6: Verify Contracts package public API (AC: #2, #3)
  - [x] 6.1 `GenerateDocumentationFile` for Contracts is already handled in Task 2.1. Verify that Task 2.3's Contracts build passed with zero CS1591 warnings — if so, Contracts XML docs are complete.
  - [x] 6.2 Verify Contracts has zero dependencies on other Hexalith.EventStore packages (only depends on `Hexalith.Commons.UniqueIds`) — this is the architectural boundary rule
  - [x] 6.3 Scan Contracts public types using same methodology as Task 1.1. All Contracts types are developer-facing by design — no visibility changes expected, but verify no internal implementation types leaked into the public API.

## Dev Notes

### THIS IS A VALIDATION STORY, NOT A REWRITE

The Client package is already feature-complete. The core work is:
1. **Audit** public API surface and XML docs
2. **Validate** NuGet packaging configuration
3. **Add** targeted tests for uncovered zero-config scenarios
4. **Fix** any gaps found during audit

### Optimal Execution Order

Start with **Task 2.3** (build both packages in Release mode). If the build passes with zero warnings, Tasks 2 and 6 are done in seconds — the XML docs are already complete. If it fails, you know exactly what to fix. This immediately reveals whether this story is a 30-minute validation or a multi-hour doc-writing effort. **If the build reveals dozens of CS1591 errors** (especially in Contracts after adding `GenerateDocumentationFile`), don't panic — use existing Client XML docs as a style template: concise one-liner `<summary>` tags, e.g., `/// <summary>Discovers domain types in assemblies via reflection.</summary>`. Then proceed: Task 1 → Task 5 → Task 3 → Task 4 → Task 6.

### Debugging Zero-Config Discovery Issues

If the dev agent or a domain service developer needs to debug what `AddEventStore()` discovered, set `EnableRegistrationDiagnostics = true` in `EventStoreOptions`:
```csharp
builder.Services.AddEventStore(options => options.EnableRegistrationDiagnostics = true);
```
This enables detailed logging during `UseEventStore()` showing each discovered domain, its resolved resource names, and which cascade layers applied. Already implemented and tested in `UseEventStoreTests`.

Do NOT:
- Rewrite `AddEventStore()`, `AssemblyScanner`, or `NamingConventionEngine` — they work correctly
- Change the 5-layer cascade configuration system — it is correct
- Add new features not in the acceptance criteria
- Modify the DI registration patterns — they are production-tested
- Change `UseEventStore()` activation flow — it is correct

### Visibility Change Risk Assessment

If `DomainProcessorStateRehydrator` or `AssemblyScanner` are changed from `public` to `internal`:
- **Server project impact**: Check if `Hexalith.EventStore.Server` references these types directly. If so, add `InternalsVisibleTo` in Client.csproj for Server project.
- **Test project impact**: Already covered by existing `InternalsVisibleTo` for Client.Tests.
- **Breaking change for external consumers**: If these types were never meant to be public API (implementation details), making them `internal` is correct. Check if any tests in Sample.Tests or IntegrationTests reference them directly.

**Decision framework**: If a type is ONLY used by framework code (AddEventStore, UseEventStore, aggregate base classes) and NOT by domain service developers, it should be `internal`. The `InternalsVisibleTo` mechanism allows framework projects and tests to access them.

**Safety rule**: When in doubt, leave a type `public`. Making a public type `internal` is a **breaking change** for any external NuGet consumer who may have referenced it. The cost of an unnecessary `public` type is minor (slightly larger API surface); the cost of breaking an external consumer is high (semver major bump required). Err on the side of caution.

### XML Documentation Strategy

The project has `TreatWarningsAsErrors=true` globally. When `GenerateDocumentationFile=true`:
- **CS1591** (missing XML comment) becomes an error
- This means: if the build passes, all public types have XML docs
- The Client.csproj already has `GenerateDocumentationFile=true` — verify the build passes
- The Contracts.csproj should also have it — check and add if missing

**Note:** `Directory.Build.props` only enables `GenerateDocumentationFile` when `ApiReferenceBuild=true`. Individual project `.csproj` files may override this with unconditional `<GenerateDocumentationFile>true</GenerateDocumentationFile>`. The Client.csproj already does this. Check if CS1591 is suppressed anywhere.

### NuGet Package Verification

Run `dotnet pack` to verify packages. Expected 5 packages:
1. `Hexalith.EventStore.Contracts`
2. `Hexalith.EventStore.Client`
3. `Hexalith.EventStore.Server`
4. `Hexalith.EventStore.Testing`
5. `Hexalith.EventStore.Aspire`

The CI/CD pipeline (`release.yml`) validates the expected package count. This story only needs to verify Client and Contracts are correctly configured — the other packages are out of scope.

### Zero-Config Developer Experience

The "zero-config" promise means a domain service developer can:
```csharp
// Program.cs — entire registration in 2 lines
builder.Services.AddEventStore();
// ...
app.UseEventStore();
```

This works because:
1. `AddEventStore()` with no args uses `[MethodImpl(NoInlining)]` + `Assembly.GetCallingAssembly()` to scan the calling assembly
2. `AssemblyScanner` discovers all `EventStoreAggregate<T>` and `EventStoreProjection<T>` subclasses
3. `NamingConventionEngine` derives domain names from type names (e.g., `CounterAggregate` → `counter`)
4. DI registers each domain as keyed `IDomainProcessor` + non-keyed for enumeration
5. `UseEventStore()` resolves the 5-layer cascade for each domain and populates `EventStoreActivationContext`

This is already proven by the Counter sample (`samples/Hexalith.EventStore.Sample/Program.cs`) and by `FluentApiRegistrationIntegrationTests`.

### Key Package Versions (from Directory.Packages.props)

| Package | Version |
|---|---|
| xUnit | 2.9.3 |
| Shouldly | 4.3.0 |
| NSubstitute | 5.3.0 |
| coverlet.collector | 6.0.4 |
| .NET SDK | 10.0.103 |
| Dapr.Client | 1.16.1 |
| MinVer | 7.0.0 |

### WARNING: Pre-Existing Test Failures

There are 75 pre-existing Tier 3 test failures and 1 pre-existing Tier 2 failure (`ErrorReferenceEndpointTests.AllProblemTypeUris_HaveCorrespondingErrorModel`). These existed BEFORE this story and are NOT regressions. Do NOT attempt to fix them. Only fix failures directly caused by changes in this story.

### Coding Conventions (from .editorconfig)

- File-scoped namespaces: `namespace X.Y.Z;`
- Allman braces (new line before `{`)
- Private fields: `_camelCase`
- Async suffix on async methods
- 4-space indentation, CRLF, UTF-8
- Nullable enabled, implicit usings enabled
- Warnings as errors (`TreatWarningsAsErrors = true`)

### Project Structure Notes

- Client package: `src/Hexalith.EventStore.Client/` — published NuGet
- Contracts package: `src/Hexalith.EventStore.Contracts/` — published NuGet
- Client tests: `tests/Hexalith.EventStore.Client.Tests/`
- Integration tests with real sample: `tests/Hexalith.EventStore.Sample.Tests/`
- Test stubs: `tests/Hexalith.EventStore.Client.Tests/Discovery/AssemblyScannerSmokeStubs.cs`

### Assembly.GetCallingAssembly() Cross-NuGet Boundary Behavior

The zero-arg `AddEventStore()` uses `[MethodImpl(NoInlining)]` + `Assembly.GetCallingAssembly()` to discover types in the **consumer's** assembly — not the Client package assembly. This works correctly across NuGet package boundaries because:
- `NoInlining` prevents the JIT from inlining the call (which would change the calling assembly)
- The calling assembly is the domain service project (e.g., `Hexalith.EventStore.Sample`), not `Hexalith.EventStore.Client`
- This is already proven by `FluentApiRegistrationIntegrationTests` which test via the Sample project (a separate assembly referencing Client via project reference — same mechanism as NuGet)

The explicit assembly overload `AddEventStore(params Assembly[] assemblies)` provides an escape hatch for developers who don't trust auto-discovery or need to scan multiple assemblies.

### Existing Files to Potentially MODIFY

| File | Change |
|---|---|
| `src/Hexalith.EventStore.Client/**/*.cs` | Visibility changes (public → internal) if audit finds internal-only types exposed publicly |
| `src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj` | Add `InternalsVisibleTo` for Server if needed |
| `src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj` | No changes expected, but verify it doesn't break if Client internals change |
| `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj` | Add `<GenerateDocumentationFile>true</GenerateDocumentationFile>` if missing |
| Various public types missing XML docs | Add `<summary>` XML doc comments |

### Existing Files — DO NOT MODIFY (logic)

**Carve-out:** Adding XML `<summary>` doc comments to public types in these files IS permitted — that is documentation, not logic change. Changing `public` to `internal` on type declarations is also permitted per Task 1. No other modifications allowed.

| File | Reason |
|---|---|
| `src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs` | Registration logic is correct and battle-tested |
| `src/Hexalith.EventStore.Client/Registration/EventStoreHostExtensions.cs` | Cascade activation is correct |
| `src/Hexalith.EventStore.Client/Discovery/AssemblyScanner.cs` | Discovery logic is correct (may change visibility only) |
| `src/Hexalith.EventStore.Client/Conventions/NamingConventionEngine.cs` | Convention engine is correct |
| `samples/Hexalith.EventStore.Sample/**/*` | Sample is correct — validates the zero-config experience |
| `Directory.Build.props` | Build configuration is correct |

### New Files to Create (conditional — only if Task 4.1 identifies gaps)

| File | Purpose |
|---|---|
| `tests/Hexalith.EventStore.Client.Tests/Registration/ZeroConfigQuickstartTests.cs` | End-to-end zero-config registration validation — ONLY created if existing tests don't already cover these scenarios (see Task 4.1) |

### Previous Story Intelligence (Story 8.2)

- Story 8.2 adds `GreetingAggregate` as a second domain — if merged, `FluentApiRegistrationIntegrationTests` will have updated assertions expecting 2 aggregates instead of 1
- Story 8.2 updates count assertions in existing tests — be aware of merge conflicts if both stories are in flight
- Key learning: this epic is about validation and completion, NOT rewriting existing code
- Pattern: audit and fix gaps, don't refactor working code

### Git Intelligence

Recent commits (2026-03-18/19):
- `f22e5ee` feat: Update sprint status and add Story 8.2 for Counter Sample Domain Service
- `96e725f` feat: Complete Story 8.1 Aspire AppHost & DAPR topology with prerequisite validation
- `93d0230` Implement per-consumer rate limiting (Story 7.3)

All Epic 8 work has been validation/completion pattern — minimal changes to working code.

### References

- [Source: _bmad-output/planning-artifacts/epics.md — Epic 8, Story 8.3]
- [Source: _bmad-output/planning-artifacts/prd.md — FR42, UX-DR17, UX-DR19, UX-DR20]
- [Source: _bmad-output/planning-artifacts/architecture.md — Package boundaries, naming conventions, DI registration patterns]
- [Source: _bmad-output/implementation-artifacts/8-2-counter-sample-domain-service.md — Previous story intelligence]
- [Source: src/Hexalith.EventStore.Client/ — Complete Client SDK implementation]
- [Source: tests/Hexalith.EventStore.Client.Tests/ — Existing test suite]
- [Source: tests/Hexalith.EventStore.Sample.Tests/Registration/FluentApiRegistrationIntegrationTests.cs — Integration validation]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

None — clean execution, no halts or retries needed.

### Completion Notes List

- **Task 1 (Public API surface):** All 20 public types in Client are correctly public and on the must-be-public list. Both candidates for `internal` (`DomainProcessorStateRehydrator`, `AssemblyScanner`) were already `internal`. `IEventStoreProjection` was also already `internal`. No visibility changes needed.
- **Task 2 (XML documentation):** Client.csproj already had `GenerateDocumentationFile=true` and built clean. Contracts.csproj was MISSING `GenerateDocumentationFile` — added it, which revealed 28 CS1591 errors in 5 files. Added XML `<summary>` and `<param>` docs to: `SubmitQueryRequest`, `SubmitQueryResponse`, `PreflightValidationResult`, `ValidateCommandRequest`, `ValidateQueryRequest`. After fix, both packages build with 0 warnings. Verified CS1591 is NOT suppressed in default builds (only under `ApiReferenceBuild=true`).
- **Task 3 (NuGet packaging):** Both Client and Contracts `dotnet pack` succeed. Both .csproj files have meaningful `<Description>` tags. README.md exists at repo root. NuGet metadata inherited from Directory.Build.props (Authors, License, PackageReadmeFile).
- **Task 4 (Zero-config tests):** All three zero-config scenarios are ALREADY COVERED by existing tests — no new tests needed:
  - Scenario A (no-arg discovery): `AddEventStoreTests.AddEventStore_ZeroConfigOverload_DiscoversAndRegistersAggregatesFromCallingAssembly`
  - Scenario B (full flow → activation context): `FluentApiRegistrationIntegrationTests.UseEventStore_SampleAssembly_ActivationContextHasCorrectProperties`
  - Scenario C (keyed resolution): `AddEventStoreTests.AddEventStore_KeyedServiceRegistration_ResolvesCorrectAggregateByDomainName` and `FluentApiRegistrationIntegrationTests.UseEventStore_SampleAssembly_KeyedDomainProcessorResolvesCounterAggregate`
- **Task 5 (Regression tests):** All Tier 1 tests pass: Contracts.Tests (267), Client.Tests (293), Sample.Tests (43), Testing.Tests (67). Total: 670 tests, 0 failures, 0 regressions.
- **Task 6 (Contracts API):** All 31 public types are developer-facing domain contracts. Only internal type is `KebabConverter` (correctly internal). Only dependency is `Hexalith.Commons.UniqueIds` — architectural boundary rule satisfied. After XML doc fix, Contracts builds clean with 0 CS1591 warnings.

### Change Log

- 2026-03-19: Added `<GenerateDocumentationFile>true</GenerateDocumentationFile>` to Contracts.csproj
- 2026-03-19: Added XML documentation to 5 Contracts files: SubmitQueryRequest.cs, SubmitQueryResponse.cs, PreflightValidationResult.cs, ValidateCommandRequest.cs, ValidateQueryRequest.cs

### File List

- `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj` (modified — added GenerateDocumentationFile)
- `src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs` (modified — added XML docs)
- `src/Hexalith.EventStore.Contracts/Queries/SubmitQueryResponse.cs` (modified — added XML docs)
- `src/Hexalith.EventStore.Contracts/Validation/PreflightValidationResult.cs` (modified — added XML docs)
- `src/Hexalith.EventStore.Contracts/Validation/ValidateCommandRequest.cs` (modified — added XML docs)
- `src/Hexalith.EventStore.Contracts/Validation/ValidateQueryRequest.cs` (modified — added XML docs)
