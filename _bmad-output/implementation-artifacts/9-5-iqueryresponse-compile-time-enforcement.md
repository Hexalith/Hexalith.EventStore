# Story 9.5: IQueryResponse Compile-Time Enforcement

Status: done

## Story

As a platform developer,
I want `IQueryResponse<T>` to enforce at compile time that every response includes a non-empty ProjectionType,
so that silent caching degradation is impossible when a microservice omits projection mapping.

## Context: What Already Exists

This is a **validation and gap-analysis story**, not a greenfield story. The `IQueryResponse<T>` interface, `IQueryContract` static abstracts, `QueryContractResolver`, and runtime `ValidateProjectionTypeOrNull()` validation are already substantially implemented. The work is to **audit every acceptance criterion against the existing code, fill any gaps, and ensure full test coverage**.

**Why this audit matters:** If `IQueryResponse<T>` fails to enforce ProjectionType at compile time, a microservice developer can implement a query response that returns null or empty ProjectionType. This causes `CachingProjectionActor` to fall back to `envelope.Domain` for ETag lookups — which may be wrong for cross-domain projection queries (e.g., an "order-summary" projection served by the "orders" domain but tracked by `order-summary` ETag actor). The result: either perpetually stale data (ETag actor mismatch) or cache thrashing (ETag always misses). FR62 exists specifically to prevent this silent degradation.

### Existing IQueryResponse & Enforcement Infrastructure

| Component | Location | Status |
|-----------|----------|--------|
| IQueryResponse<T> (interface, FR62) | `src/Hexalith.EventStore.Contracts/Queries/IQueryResponse.cs` | Built |
| IQueryContract (static abstract metadata) | `src/Hexalith.EventStore.Contracts/Queries/IQueryContract.cs` | Built |
| QueryContractMetadata (resolved metadata) | `src/Hexalith.EventStore.Contracts/Queries/QueryContractMetadata.cs` | Built |
| QueryContractResolver (compile-time validation) | `src/Hexalith.EventStore.Client/Queries/QueryContractResolver.cs` | Built |
| EventStoreQueryTypeAttribute (override) | `src/Hexalith.EventStore.Contracts/Queries/EventStoreQueryTypeAttribute.cs` | Built |
| QueryResult (DAPR-serializable, optional ProjectionType) | `src/Hexalith.EventStore.Server/Actors/QueryResult.cs` | Built |
| CachingProjectionActor.ValidateProjectionTypeOrNull() | `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs` | Built |
| GetCounterStatusQuery (sample IQueryContract) | `samples/Hexalith.EventStore.Sample/Counter/Queries/GetCounterStatusQuery.cs` | Built |
| SubmitQueryResponse (HTTP response, no ProjectionType) | `src/Hexalith.EventStore.Contracts/Queries/SubmitQueryResponse.cs` | Built |
| SubmitQueryResult (MediatR result, carries ProjectionType) | `src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs` | Built |
| QueryRouterResult (router output, carries ProjectionType) | `src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs` | Built |

### Existing Test Coverage

| Test File | Tier | Tests |
|-----------|------|-------|
| `Contracts.Tests/Queries/IQueryResponseTests.cs` | 1 | 3 tests (concrete impl, different types, covariance) |
| `Client.Tests/Queries/QueryContractResolverTests.cs` | 1 | 7+ tests (valid contract, cross-domain, cached, invalid kebab, empty domain, null type, ETag actor ID) |
| `Server.Tests/Actors/CachingProjectionActorTests.cs` | 2 | 21 tests (runtime discovery, null/empty/colon/length validation, flip-flop, cache lifecycle) |

## Acceptance Criteria

1. **Given** a microservice implements `IQueryResponse<T>`,
   **When** the response is constructed,
   **Then** a non-empty `ProjectionType` field is required at compile time (FR62).
   **Note:** "Compile time" means: the interface mandates `string ProjectionType { get; }` as a non-nullable member. A class implementing `IQueryResponse<T>` MUST provide this property to compile. The C# type system prevents returning `null` from a non-nullable `string` property (with nullable reference types enabled, which is the project default). Empty string is a compile-time-valid but semantically invalid value — it is caught at runtime (AC #2).

2. **Given** a query actor receives a response with empty or whitespace-only ProjectionType,
   **When** processing the response,
   **Then** it is treated as an error equivalent to a missing response (FR62).
   **Note:** "Error equivalent to a missing response" means: `ValidateProjectionTypeOrNull()` returns null for empty/whitespace, causing the runtime discovery path to fall back to `envelope.Domain` — the same behavior as if the response didn't include ProjectionType at all. The key audit question is: does the current implementation log a warning AND skip caching when ProjectionType is empty? (This prevents silent degradation.)

## Definition of Done

Story is complete when:
- **Required:** Tasks 0-5 pass — all audit checks verified, gaps filled, tests green
- **Conditional:** Task 5 — run Tier 1+2 tests only if any `src/` or `tests/` files were modified during Tasks 0-4
- All two acceptance criteria verified against actual code (not epics wording)
- Build passes: `dotnet build Hexalith.EventStore.slnx --configuration Release`
- No regressions in existing Tier 1 or Tier 2 tests
- Audit results table produced in Completion Notes (AC # / Expected / Actual / Pass-Fail)
- Branch: `feat/story-9-5-iqueryresponse-compile-time-enforcement` — create before making any code or test changes
- Scope boundary: up to 3 minor fixes (<1hr each) and missing tests are in scope; algorithmic changes or >3 gaps trigger a follow-up story

## Tasks / Subtasks

- [x] Task 0: Audit IQueryResponse<T> compile-time enforcement (AC: #1)
  - [x] Create branch `feat/story-9-5-iqueryresponse-compile-time-enforcement` before any code or test changes
  - [x] Verify `IQueryResponse<T>` interface in `Contracts/Queries/IQueryResponse.cs` has `string ProjectionType { get; }` as non-nullable member
  - [x] Verify nullable reference types are enabled project-wide (check `Hexalith.EventStore.Contracts.csproj` for `<Nullable>enable</Nullable>` or `Directory.Build.props`)
  - [x] Verify that with NRT enabled, implementing `string ProjectionType` forces the implementor to return a non-null string at compile time (CS8766 warning if returning nullable)
  - [x] Verify `TreatWarningsAsErrors=true` in `Directory.Build.props` — this elevates CS8766 (nullable return from non-nullable member) from warning to compilation error, making NRT enforcement binding rather than advisory
  - [x] Verify XML documentation on `ProjectionType` property mentions constraints: no colons, max 100 chars, short kebab-case names recommended
  - [x] Verify `IQueryResponse<T>` uses covariant `out T` for type parameter (allows `IQueryResponse<Derived>` assignable to `IQueryResponse<Base>`)
  - [x] Verify test stubs in `IQueryResponseTests.cs` all return non-empty ProjectionType values
  - [x] Verify sample `GetCounterStatusQuery` implements `IQueryContract` with `static string ProjectionType => "counter"` (compile-time static abstract enforcement on the request side)
  - [x] Document: compile-time enforcement means the C# compiler requires implementors to provide the property; NRT prevents null returns. Empty string is the remaining gap, caught at runtime (AC #2)

- [x] Task 1: Audit IQueryContract compile-time validation (AC: #1, related)
  - [x] Verify `IQueryContract` interface has `static abstract string ProjectionType { get; }` — C# static abstract enforces implementors MUST define this at compile time
  - [x] Verify `QueryContractResolver.Resolve<TQuery>()` validates ProjectionType at startup:
    - Non-null (ArgumentNullException from NamingConventionEngine if null)
    - Non-empty (ArgumentException from NamingConventionEngine if empty)
    - Kebab-case format (NamingConventionEngine.ValidateKebabCase)
    - No colons (explicit check in Resolve)
  - [x] Verify `QueryContractResolver.GetETagActorId<TQuery>(tenantId)` uses `Resolve<TQuery>().ProjectionType` (not Domain) for ETag actor ID derivation
  - [x] Verify `QueryContractResolverTests.cs` covers: valid resolution, cross-domain (ProjectionType != Domain), cached results, invalid kebab-case, empty domain, null query type, ETag actor ID format
  - [x] Document: IQueryContract and IQueryResponse enforce ProjectionType independently — no runtime bridge validates that IQueryContract.ProjectionType matches the IQueryResponse.ProjectionType returned by the microservice. This is by design (separation of request-side and response-side contracts) and should be noted in Completion Notes, not treated as a gap
  - [x] Note: IQueryContract validates the REQUEST side; IQueryResponse validates the RESPONSE side. Both enforce ProjectionType but at different points in the pipeline

- [x] Task 2: Audit runtime empty/whitespace ProjectionType handling (AC: #2)
  - [x] Verify `CachingProjectionActor.ValidateProjectionTypeOrNull()` in `Server/Actors/CachingProjectionActor.cs`:
    - Returns null for null input
    - Returns null for empty/whitespace input AND logs warning with EventId 1076 (InvalidProjectionType, reason: "empty or whitespace")
    - Returns null for input containing `:` AND logs warning (reason: "contains ':'")
    - Returns null for input > 100 chars AND logs warning (reason: "exceeds 100 characters")
  - [x] Verify `QueryAsync()` flow when ValidateProjectionTypeOrNull returns null for empty ProjectionType:
    - Discovery path: falls back to `envelope.Domain` via `GetEffectiveProjectionType()`
    - Caching behavior: does NOT discover a projection type → continues using fallback domain
    - This IS "treated as an error equivalent to a missing response" per FR62: the response's explicit ProjectionType is rejected, same as if it never existed
  - [x] Verify that empty ProjectionType does NOT cause an exception or 500 error — fail-open degradation is the contract
  - [x] Verify test coverage: `CachingProjectionActorTests` has a test for empty string ProjectionType (`QueryAsync_EmptyProjectionType_FallsBackToDomain` or equivalent)
  - [x] Verify test coverage: `CachingProjectionActorTests` has a test for whitespace-only ProjectionType
  - [x] Verify that existing test for empty ProjectionType asserts BOTH the behavioral outcome (fallback to envelope.Domain) AND the warning log emission (EventId 1076, reason "empty or whitespace") — behavioral-only assertions miss the observability contract

- [x] Task 3: Audit ProjectionType flow through the full query pipeline (AC: #1, #2)
  - [x] Trace ProjectionType from `ExecuteQueryAsync` return → `CachingProjectionActor.QueryAsync` → `QueryResult.ProjectionType` → `QueryRouter.RouteQueryAsync` → `QueryRouterResult.ProjectionType` → `SubmitQueryHandler` → `SubmitQueryResult.ProjectionType` → `QueriesController` Gate 3 ETag header
  - [x] Verify `QueryResult.ProjectionType` is nullable (`string?`) — backward compatible with DataContract deserialization
  - [x] Verify `QueryRouterResult.ProjectionType` is nullable (`string?`) — passed through from actor
  - [x] Verify `SubmitQueryResult.ProjectionType` is nullable (`string?`) — passed through from router
  - [x] Verify `QueriesController` Gate 3: uses `result.ProjectionType ?? request.Domain` for ETag lookup — correct fallback
  - [x] Verify `SubmitQueryResponse` (HTTP response to client) does NOT carry ProjectionType — it's in the ETag response header instead (correct: ProjectionType is infrastructure, not client-facing data)

- [x] Task 4: Validate test coverage completeness
  - [x] Run IQueryResponse tests: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --filter "FullyQualifiedName~IQueryResponseTests"`
  - [x] Run QueryContractResolver tests: `dotnet test tests/Hexalith.EventStore.Client.Tests/ --filter "FullyQualifiedName~QueryContractResolverTests"`
  - [x] Run CachingProjectionActor tests: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~CachingProjectionActorTests"`
  - [x] Verify IQueryResponseTests covers:
    - Concrete implementation with valid ProjectionType
    - Different ProjectionType values across implementations
    - Covariance: `IQueryResponse<Derived>` assignable to `IQueryResponse<Base>`
  - [x] Check for GAP: test that verifies ProjectionType cannot be null at compile time with NRT enabled — may need a compile-time enforcement note rather than a runtime test
  - [x] Check for GAP: test that verifies IQueryResponse implementations with empty ProjectionType are treated as error at runtime
  - [x] Check for GAP: test that verifies the full pipeline flow from IQueryResponse → CachingProjectionActor discovery → QueriesController ETag header

- [x] Task 5: Fill any gaps identified during audit (bounded: up to 3 minor gaps, <1hr each)
  - [x] Fix any acceptance criteria violations found
  - [x] Add any missing tests
  - [x] Prioritize gaps by risk: (1) missing log assertions on empty/whitespace tests, (2) missing whitespace-only ProjectionType test if absent, (3) cross-contract documentation. Do NOT fill slots with low-value variants of existing tests
  - [x] No more than 3 gaps allowed — additional gaps trigger a follow-up story
  - [x] Ensure build passes: `dotnet build Hexalith.EventStore.slnx --configuration Release`
  - [x] Run full Tier 1+2 tests only if `src/` or `tests/` files were modified

## Dev Notes

### Architecture: Two-Layer ProjectionType Safety

The system enforces ProjectionType through two complementary layers:

**Layer 1 — Compile-Time (IQueryResponse<T> + IQueryContract):**
- `IQueryResponse<T>.ProjectionType` is `string` (non-nullable with NRT enabled) → implementors MUST provide a value
- `IQueryContract.ProjectionType` is `static abstract string` → query definitions MUST declare ProjectionType as a compile-time constant
- `QueryContractResolver.Resolve<T>()` validates kebab-case, no colons, non-empty at app startup
- **Remaining gap:** Empty string `""` passes the compiler but is semantically invalid → caught at runtime (Layer 2)

**Layer 2 — Runtime (CachingProjectionActor):**
- `ValidateProjectionTypeOrNull()` rejects: null, empty/whitespace, contains `:`, >100 chars
- Invalid values → warning log (EventId 1076) + fallback to `envelope.Domain`
- Fail-open: invalid ProjectionType never causes exceptions, just suboptimal caching
- First valid discovery wins: `_discoveredProjectionType` set once, subsequent mismatches logged

### Key Design Decisions

1. **Non-nullable `string` vs. required constructor parameter:** The interface uses `string ProjectionType { get; }` — with NRT enabled, returning null produces CS8766 warning/error. This is the C# idiomatic way to enforce "must provide a value." A stronger approach (e.g., `[Required]` attribute or constructor validation) is unnecessary because:
   - NRT catches null at compile time
   - Empty string is caught at runtime by ValidateProjectionTypeOrNull
   - The interface can't enforce non-empty at compile time without custom analyzers

2. **Covariance (`out T`):** The `out` modifier on `IQueryResponse<out T>` allows polymorphic query response handling. This is correctly designed — `IQueryResponse<DerivedDto>` is assignable to `IQueryResponse<BaseDto>`.

3. **QueryResult.ProjectionType is nullable (`string?`):** This is correct — the DAPR actor proxy serialization (DataContract) must handle backward compatibility with old-format results that lack ProjectionType. The nullability is at the transport layer, not the domain contract.

4. **ProjectionType NOT exposed in HTTP response:** `SubmitQueryResponse` deliberately excludes ProjectionType — it's embedded in the ETag response header (`{base64url(projectionType)}.{guid}`). This keeps the wire format clean and avoids leaking infrastructure concerns.

5. **FR62 enforcement model — deliberate two-layer design:** FR62 says "enforces at compile time that every response includes non-empty ProjectionType." The current design partially satisfies this: non-null is enforced at compile time (NRT + TreatWarningsAsErrors); non-empty is enforced at runtime (ValidateProjectionTypeOrNull). This is a deliberate design trade-off — compile-time non-empty string enforcement in C# requires custom Roslyn analyzers, which is disproportionate for a library contract. The dev agent should document this as a design trade-off in the audit table, not as a gap.

### Previous Story Intelligence (Story 9-4)

Story 9-4 was a validation/audit story for query actor in-memory page cache. Key learnings:
- **Audit-only stories work well:** All 3 ACs were verified against code with no source changes needed. 3 test gaps filled.
- **Test counts at Story 9-4 completion:** CachingProjectionActor: 21 tests; Query pipeline filter: 103 tests; Full Server.Tests: 1526 passed; Tier 1: 698 passed
- **CachingProjectionActor runtime validation confirmed comprehensive:** Empty, colon, length checks all have tests
- **Clone() pattern critical for JsonElement caching** — important context if any query result modifications are needed
- **First discovery wins** for projection type — do not attempt to change this behavior
- **Pre-existing test failure to ignore:** `ErrorReferenceEndpointTests.AllProblemTypeUris_HaveCorrespondingErrorModel` — ignore if still failing

### Key Code Paths to Audit

**IQueryResponse compile-time enforcement chain:**
```
Developer implements IQueryResponse<T>
    → MUST provide `string ProjectionType { get; }` (compiler enforces)
    → NRT: returning null produces CS8766 warning (compiler enforces)
    → Empty string: caught at runtime by CachingProjectionActor.ValidateProjectionTypeOrNull()
```

**IQueryContract compile-time enforcement chain:**
```
Developer implements IQueryContract
    → MUST provide `static abstract string ProjectionType { get; }` (compiler enforces)
    → QueryContractResolver.Resolve<T>() validates at startup:
        - NamingConventionEngine.ValidateKebabCase() — non-null, non-empty, kebab format
        - Colon check — ArgumentException if contains ':'
```

**Runtime empty ProjectionType flow:**
```
ExecuteQueryAsync returns QueryResult { ProjectionType = "" }
    → CachingProjectionActor.QueryAsync: projectionType = ValidateProjectionTypeOrNull("")
    → Returns null (empty/whitespace check)
    → Log warning: InvalidProjectionType with reason "empty or whitespace"
    → Discovery path: _discoveredProjectionType remains null
    → GetEffectiveProjectionType() returns envelope.Domain (fallback)
    → Caching works with fallback domain — suboptimal but safe
```

### Testing Pattern

- **xUnit** with **Shouldly** assertions (Tier 1), **NSubstitute** for mocking (Tier 2)
- Test naming: `{Method}_{Scenario}_{ExpectedResult}`
- IQueryResponse tests are Tier 1 (pure contracts, no DAPR)
- CachingProjectionActor tests are Tier 2 (use `ActorHost.CreateForTest<T>()`)
- QueryContractResolver tests are Tier 1 (pure validation logic)

### Project Structure Notes

- `IQueryResponse<T>` in `Hexalith.EventStore.Contracts/Queries/`
- `IQueryContract` in `Hexalith.EventStore.Contracts/Queries/`
- `QueryContractResolver` in `Hexalith.EventStore.Client/Queries/`
- `CachingProjectionActor` (runtime validation) in `Hexalith.EventStore.Server/Actors/`
- `QueryResult` (transport type) in `Hexalith.EventStore.Server/Actors/`
- `QueriesController` (Gate 1/3) in `Hexalith.EventStore.CommandApi/Controllers/`
- Tests mirror source structure in respective test projects

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 9.5: IQueryResponse Compile-Time Enforcement]
- [Source: _bmad-output/planning-artifacts/epics.md#FR62]
- [Source: _bmad-output/implementation-artifacts/9-4-query-actor-in-memory-page-cache.md]
- [Source: src/Hexalith.EventStore.Contracts/Queries/IQueryResponse.cs]
- [Source: src/Hexalith.EventStore.Contracts/Queries/IQueryContract.cs]
- [Source: src/Hexalith.EventStore.Contracts/Queries/QueryContractMetadata.cs]
- [Source: src/Hexalith.EventStore.Client/Queries/QueryContractResolver.cs]
- [Source: src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs]
- [Source: src/Hexalith.EventStore.Server/Actors/QueryResult.cs]
- [Source: src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs]
- [Source: tests/Hexalith.EventStore.Contracts.Tests/Queries/IQueryResponseTests.cs]
- [Source: tests/Hexalith.EventStore.Client.Tests/Queries/QueryContractResolverTests.cs]
- [Source: tests/Hexalith.EventStore.Server.Tests/Actors/CachingProjectionActorTests.cs]
- [Source: samples/Hexalith.EventStore.Sample/Counter/Queries/GetCounterStatusQuery.cs]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

No debug issues encountered. All audit checks passed against existing code. Two test gaps filled.

### Completion Notes List

**Audit Results Table:**

| AC # | Expected | Actual | Pass/Fail |
|------|----------|--------|-----------|
| #1 — Compile-time enforcement | `IQueryResponse<T>.ProjectionType` is `string` (non-nullable with NRT), covariant `out T`, XML docs mention constraints | `IQueryResponse.cs:23` — `string ProjectionType { get; }` non-nullable; `IQueryResponse.cs:11` — `out T`; XML docs mention colons, 100 chars, kebab-case. `Directory.Build.props` — `Nullable=enable`, `TreatWarningsAsErrors=true` (CS8766 → error). `IQueryContract.cs:29` — `static abstract string ProjectionType` for request side. | **PASS** |
| #2 — Runtime empty/whitespace handling | Empty/whitespace ProjectionType → warning log (EventId 1076) + fallback to `envelope.Domain` (fail-open) | `CachingProjectionActor.cs:122-125` — `IsNullOrWhiteSpace` check returns null + logs InvalidProjectionType (EventId 1076, reason "empty or whitespace"). `GetEffectiveProjectionType()` falls back to `_discoveredProjectionType ?? fallbackDomain`. No exception thrown. | **PASS** |

**Gaps Found and Filled (2 of 3 slots used):**

1. **Gap #1 (filled):** Added `QueryAsync_EmptyProjectionType_FallsBackToEnvelopeDomainAndLogsWarning` — verifies empty ProjectionType has both behavioral fallback to `envelope.Domain` and warning log emission (EventId 1076, reason "empty or whitespace") in one test.
2. **Gap #2 (filled):** Added `QueryAsync_WhitespaceOnlyProjectionType_FallsBackToEnvelopeDomainAndLogsWarning` — verifies whitespace-only `"   "` ProjectionType is treated identically to empty string: behavioral fallback to envelope.Domain + warning log (EventId 1076).
3. **Gap #3 (not a gap):** NRT compile-time null enforcement is a compiler guarantee, not a runtime test. Documented as design trade-off per Dev Notes #5.
4. **Gap #4 (not a gap):** Full pipeline flow from IQueryResponse → CachingProjectionActor → QueriesController is already covered by existing integration-level tests. The audit traced the flow manually across source files.

**Design Trade-off Note:** IQueryContract and IQueryResponse enforce ProjectionType independently — no runtime bridge validates that `IQueryContract.ProjectionType` matches `IQueryResponse.ProjectionType`. This is by design (separation of request-side and response-side contracts).

**Test Results:**
- IQueryResponse tests: 3 passed
- QueryContractResolver tests: 13 passed
- CachingProjectionActor tests: 23 passed (21 existing + 2 new)
- Full Tier 1: 698 passed
- Full Tier 2 (Server.Tests): 1528 passed
- Build: Release — 0 warnings, 0 errors
- No source code changes — only test file modified

### Change Log

- 2026-03-19: Story 9-5 audit completed and closed. 2 test gaps filled in CachingProjectionActorTests: empty ProjectionType and whitespace-only ProjectionType tests both assert behavioral fallback + observability.

### File List

- `tests/Hexalith.EventStore.Server.Tests/Actors/CachingProjectionActorTests.cs` (modified — added 2 tests, TestLoggerInstance helper, logger-accepting constructor overload)
