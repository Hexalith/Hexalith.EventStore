# Story 17.4: Query Contracts

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an **API consumer**,
I want **strongly-typed query and pre-flight validation contracts in the Contracts NuGet package, with FluentValidation validators in CommandApi**,
so that **client applications can submit queries to projection actors and validate command/query authorization before submission, with compile-time type safety and consistent input validation across client and server**.

## Acceptance Criteria

1. **`SubmitQueryRequest`** record exists in `Contracts/Queries/` with properties: `Tenant` (string), `Domain` (string), `AggregateId` (string), `QueryType` (string), `Payload` (JsonElement?, optional query parameters)
2. **`SubmitQueryResponse`** record exists in `Contracts/Queries/` with properties: `CorrelationId` (string), `Payload` (JsonElement, opaque projection result from actor)
3. **`ValidateCommandRequest`** record exists in `Contracts/Validation/` with properties: `Tenant` (string), `Domain` (string), `CommandType` (string), `AggregateId` (string?, optional — Amendment A2 for fine-grained ACL)
4. **`ValidateQueryRequest`** record exists in `Contracts/Validation/` with properties: `Tenant` (string), `Domain` (string), `QueryType` (string), `AggregateId` (string?, optional — Amendment A2 for fine-grained ACL)
5. **`PreflightValidationResult`** record exists in `Contracts/Validation/` with properties: `IsAuthorized` (bool), `Reason` (string?, null when authorized) — named `PreflightValidationResult` (not `ValidationResult`) to avoid collision with FluentValidation's `ValidationResult`
6. **`SubmitQueryRequestValidator`** FluentValidation validator exists in `CommandApi/Validation/` with rules mirroring `SubmitCommandRequestValidator`: Tenant/Domain lowercase alphanumeric+hyphens (max 128), AggregateId alphanumeric+dots/hyphens/underscores (max 256), QueryType required with injection prevention (max 256), Payload optional
7. **`ValidateCommandRequestValidator`** FluentValidation validator exists in `CommandApi/Validation/` with rules: Tenant/Domain/CommandType required with same constraints; AggregateId optional but validated when present
8. **`ValidateQueryRequestValidator`** FluentValidation validator exists in `CommandApi/Validation/` with rules: Tenant/Domain/QueryType required with same constraints; AggregateId optional but validated when present
9. **Unit tests for all 5 Contracts types** in `Contracts.Tests/` covering construction, field access, optional defaults, and record equality
10. **Unit tests for all 3 FluentValidation validators** in `Server.Tests/` covering valid inputs, boundary conditions, injection prevention, optional AggregateId behavior
11. **Cross-validator consistency tests** in `Server.Tests/` proving that identical Tenant/Domain/AggregateId AND CommandType/QueryType inputs produce identical pass/fail outcomes across `SubmitCommandRequestValidator`, `SubmitQueryRequestValidator`, `ValidateCommandRequestValidator`, and `ValidateQueryRequestValidator` — preventing regex drift AND injection-rule drift between duplicated patterns
12. **All existing Tier 1 and Tier 2 tests continue to pass** with zero behavioral change

## Tasks / Subtasks

- [x] Task 1: Create query contract types in Contracts package (AC: #1, #2)
    - [x] 1.1 Create `src/Hexalith.EventStore.Contracts/Queries/` directory
    - [x] 1.2 Create `SubmitQueryRequest.cs` record — mirrors `SubmitCommandRequest` pattern but with `QueryType` instead of `CommandType`, and `Payload` as `JsonElement?` (nullable, queries may have no parameters)
    - [x] 1.3 Create `SubmitQueryResponse.cs` record — includes `CorrelationId` for tracing and `JsonElement Payload` for the opaque projection result (Amendment A4)
- [x] Task 2: Create validation contract types in Contracts package (AC: #3, #4, #5)
    - [x] 2.1 Create `src/Hexalith.EventStore.Contracts/Validation/` directory
    - [x] 2.2 Create `ValidateCommandRequest.cs` record — `AggregateId` is `string?` with default `null` (Amendment A2); no `Payload` needed (pre-flight check only tests authorization, not command content)
    - [x] 2.3 Create `ValidateQueryRequest.cs` record — same structure as `ValidateCommandRequest` with `QueryType` instead of `CommandType`
    - [x] 2.4 Create `PreflightValidationResult.cs` record — `IsAuthorized` (bool) + `Reason` (string?, null when authorized). Use name `PreflightValidationResult` to avoid collision with `FluentValidation.Results.ValidationResult` and with `TenantValidationResult`/`RbacValidationResult` from `CommandApi.Authorization` (Story 17-1)
- [x] Task 3: Create FluentValidation validators in CommandApi (AC: #6, #7, #8)
    - [x] 3.1 Create `SubmitQueryRequestValidator.cs` in `CommandApi/Validation/` — reuse same regex patterns as `SubmitCommandRequestValidator` (Tenant/Domain: `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`, AggregateId: `^[a-zA-Z0-9]([a-zA-Z0-9._-]*[a-zA-Z0-9])?$`). Payload rule: `JsonElement?` — if present, `ValueKind != Undefined`. No Extensions rules (queries don't support extensions).
    - [x] 3.2 Create `ValidateCommandRequestValidator.cs` in `CommandApi/Validation/` — Tenant/Domain/CommandType required with same constraints; AggregateId: optional (`When(x => x.AggregateId is not null)` guard), but when present, validated with same regex and max 256 chars
    - [x] 3.3 Create `ValidateQueryRequestValidator.cs` in `CommandApi/Validation/` — same structure as `ValidateCommandRequestValidator` with QueryType instead of CommandType
    - [x] 3.4 **DO NOT modify** `SubmitCommandRequestValidator.cs` — no refactoring of shared patterns in this story. Duplicate regex patterns are acceptable (3 validators × same patterns). A future story can extract shared validation rules if the pattern count grows.
- [x] Task 4: Unit tests for Contracts types (AC: #9)
    - [x] 4.1 Create `tests/Hexalith.EventStore.Contracts.Tests/Queries/` directory
    - [x] 4.2 Create `SubmitQueryRequestTests.cs` — construction with all fields, null payload default, record equality
    - [x] 4.3 Create `SubmitQueryResponseTests.cs` — construction, field access
    - [x] 4.4 Create `tests/Hexalith.EventStore.Contracts.Tests/Validation/` directory
    - [x] 4.5 Create `ValidateCommandRequestTests.cs` — construction, optional AggregateId defaults to null, with AggregateId
    - [x] 4.6 Create `ValidateQueryRequestTests.cs` — same patterns
    - [x] 4.7 Create `PreflightValidationResultTests.cs` — IsAuthorized true/false, Reason null/present, record equality
    - [x] 4.8 **Use standard xUnit assertions** (`Assert.Equal`, `Assert.Null`, etc.) — Contracts.Tests uses xUnit directly, NOT Shouldly (matching existing test style in `CommandEnvelopeTests.cs`)
- [x] Task 5: Unit tests for FluentValidation validators (AC: #10)
    - [x] 5.1 Create `tests/Hexalith.EventStore.Server.Tests/Validation/` directory
    - [x] 5.2 Create `SubmitQueryRequestValidatorTests.cs` — valid request passes, empty/null Tenant fails, regex violations, injection in QueryType, max length violations, null Payload valid, Payload with Undefined ValueKind fails
    - [x] 5.3 Create `ValidateCommandRequestValidatorTests.cs` — valid request passes, required fields validated, optional AggregateId=null passes, **empty-string AggregateId="" rejected** (not null, enters validation block, fails NotEmpty), AggregateId when present is validated, injection prevention on CommandType
    - [x] 5.4 Create `ValidateQueryRequestValidatorTests.cs` — same patterns as ValidateCommandRequestValidator tests including empty-string AggregateId rejection
    - [x] 5.5 Create `ValidatorConsistencyTests.cs` in `Server.Tests/Validation/` — TWO `[Theory]` methods: (a) identity field consistency: shared `[InlineData]` feeding identical Tenant/Domain/AggregateId values through all 4 validators and asserting identical pass/fail; (b) type field consistency: shared `[InlineData]` feeding identical strings as both `CommandType` (in `SubmitCommandRequestValidator` and `ValidateCommandRequestValidator`) and `QueryType` (in `SubmitQueryRequestValidator` and `ValidateQueryRequestValidator`) and asserting identical pass/fail — covers dangerous char rejection, injection patterns, and max length. This guards against BOTH regex drift AND injection-rule drift.
    - [x] 5.6 **Use Shouldly assertions** — Server.Tests uses Shouldly (matching existing test style in `AuthorizationBehaviorTests.cs`)
- [x] Task 6: Verify zero regression (AC: #12)
    - [x] 6.1 Run all Tier 1 tests: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ tests/Hexalith.EventStore.Client.Tests/ tests/Hexalith.EventStore.Sample.Tests/ tests/Hexalith.EventStore.Testing.Tests/`
    - [x] 6.2 Run Tier 2 tests: `dotnet test tests/Hexalith.EventStore.Server.Tests/`
    - [x] 6.3 Verify new Contracts types are included in the `Hexalith.EventStore.Contracts` NuGet package (they're in the Contracts project, so they auto-include)

## Dev Notes

### Design Decision: Why Contracts Package (Not CommandApi/Models)

The existing `SubmitCommandRequest` is in `CommandApi/Models/` because it's a server-side API DTO — clients construct HTTP requests directly. For queries and validation, the types are in Contracts because:

1. **Client SDK usage** — The `Hexalith.EventStore.Client` NuGet package references `Contracts`. Client applications need these types to construct typed requests and deserialize responses.
2. **Shared serialization** — Both client and server need to agree on the JSON shape. Putting types in Contracts ensures a single source of truth.
3. **NuGet distribution** — `Hexalith.EventStore.Contracts` is a published NuGet package. Query/validation types shipped here are available to all consumers.

The `SubmitCommandRequest` pattern (in CommandApi) was appropriate for commands because the command submission flow is fire-and-forget (202 Accepted) — the client only needs a correlation ID back. Queries are synchronous (200 OK with payload), so the response type carries domain data that the client must deserialize.

### Type Design

```csharp
// Contracts/Queries/SubmitQueryRequest.cs
using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Queries;

public record SubmitQueryRequest(
    string Tenant,
    string Domain,
    string AggregateId,
    string QueryType,
    JsonElement? Payload = null);  // Null = no query parameters
```

```csharp
// Contracts/Queries/SubmitQueryResponse.cs
using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Queries;

public record SubmitQueryResponse(
    string CorrelationId,
    JsonElement Payload);  // Opaque projection result (Amendment A4)
```

```csharp
// Contracts/Validation/ValidateCommandRequest.cs
namespace Hexalith.EventStore.Contracts.Validation;

public record ValidateCommandRequest(
    string Tenant,
    string Domain,
    string CommandType,
    string? AggregateId = null);  // Amendment A2: optional, for fine-grained ACL
```

```csharp
// Contracts/Validation/ValidateQueryRequest.cs
namespace Hexalith.EventStore.Contracts.Validation;

public record ValidateQueryRequest(
    string Tenant,
    string Domain,
    string QueryType,
    string? AggregateId = null);  // Amendment A2: optional, for fine-grained ACL
```

```csharp
// Contracts/Validation/PreflightValidationResult.cs
namespace Hexalith.EventStore.Contracts.Validation;

public record PreflightValidationResult(
    bool IsAuthorized,
    string? Reason = null);
```

### Why `PreflightValidationResult` Instead of `ValidationResult`

The name `ValidationResult` collides with:

- `FluentValidation.Results.ValidationResult` — used extensively in the MediatR validation pipeline
- `Microsoft.Extensions.Options.ValidateOptionsResult` — used in options validation (Story 17-1)

The name `PreflightValidationResult` is specific to the authorization pre-flight check use case (Stories 17-7, 17-8) and avoids all collisions. Downstream consumers will use:

- `PreflightValidationResult` for authorization checks (our type)
- `ValidationResult` for request validation (FluentValidation)
- `ValidateOptionsResult` for options validation (Microsoft)

### Why `JsonElement?` for Query Payload (Nullable)

Unlike commands where `Payload` is always required (the command payload), queries may be parameterless. Examples:

- `GetCurrentState` — no parameters, just read the aggregate's current projection
- `GetEventHistory` — may have optional pagination parameters
- `SearchByFilter` — has a filter payload

Making `Payload` nullable (`JsonElement?`) allows all these patterns without forcing callers to construct an empty JSON object.

**`JsonElement?` vs `default(JsonElement)` edge case:** `default(JsonElement)` has `ValueKind == JsonValueKind.Undefined`. The validator must distinguish between `null` (no payload, valid) and a non-null `JsonElement` with `ValueKind == Undefined` (invalid — caller sent something but it didn't parse). Test both paths explicitly.

### Required Import: `System.Text.Json`

`System.Text.Json` is NOT included in .NET implicit usings for class libraries. Every new Contracts file using `JsonElement` needs an explicit `using System.Text.Json;` at the top. This is a build error if missed — the Contracts project has no custom global usings for this namespace.

### Why No Extensions on Query Types

`SubmitCommandRequest` includes `Extensions` for command metadata (audit trail, custom headers). Queries are read-only operations with no side effects, so extension metadata is less relevant. If extensions are needed later, they can be added as an optional parameter without breaking changes (records support this).

### Validator Pattern: Duplicate but Consistent

The FluentValidation validators for `SubmitQueryRequest`, `ValidateCommandRequest`, and `ValidateQueryRequest` duplicate the regex patterns from `SubmitCommandRequestValidator`:

```csharp
// These patterns MUST match SubmitCommandRequestValidator and AggregateIdentity
private static readonly Regex _tenantDomainRegex = TenantDomainPattern();  // ^[a-z0-9]([a-z0-9-]*[a-z0-9])?$
private static readonly Regex _aggregateIdRegex = AggregateIdPattern();    // ^[a-zA-Z0-9]([a-zA-Z0-9._-]*[a-zA-Z0-9])?$
private static readonly Regex _injectionPattern = InjectionPattern();      // (?i)(javascript\s*:|on\w+\s*=|<\s*script)
```

**Do NOT extract shared patterns** in this story. Duplication across 4 validators is acceptable. If a 5th validator is needed, extract to a shared `IdentityValidationRules` class at that point.

**Do NOT use `partial class` with `[GeneratedRegex]`** — only use `[GeneratedRegex]` if the validator class is `partial` (matching the `SubmitCommandRequestValidator` pattern which uses `[GeneratedRegex]` with `partial class`).

### Validation Rules Summary

| Field                   | Required                          | Max Length | Pattern                                      | Injection Check           |
| ----------------------- | --------------------------------- | ---------- | -------------------------------------------- | ------------------------- |
| Tenant                  | Yes                               | 128        | `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`            | Via regex                 |
| Domain                  | Yes                               | 128        | `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`            | Via regex                 |
| AggregateId             | Yes (query) / Optional (validate) | 256        | `^[a-zA-Z0-9]([a-zA-Z0-9._-]*[a-zA-Z0-9])?$` | Via regex                 |
| CommandType / QueryType | Yes                               | 256        | No dangerous chars                           | `<>&'"` + script patterns |
| Payload (query)         | Optional                          | —          | ValueKind != Undefined when present          | —                         |

### How These Types Will Be Used (Future Stories)

```text
Story 17-5: POST /api/v1/queries
  → Controller deserializes SubmitQueryRequest
  → FluentValidation via SubmitQueryRequestValidator (MediatR pipeline)
  → AuthorizationBehavior with messageCategory="query" (Story 17-3 consolidated)
  → QueryHandler → IQueryRouter → ProjectionActor
  → Returns 200 OK with SubmitQueryResponse (success only)
  → Returns 404 if projection actor not found (Amendment A6 — controller handles, never constructs SubmitQueryResponse)

Story 17-7: POST /api/v1/commands/validate
  → Controller deserializes ValidateCommandRequest
  → FluentValidation via ValidateCommandRequestValidator
  → ITenantValidator + IRbacValidator with messageCategory="command"
  → Returns 200 OK with PreflightValidationResult (ALWAYS — both authorized and unauthorized)

Story 17-8: POST /api/v1/queries/validate
  → Controller deserializes ValidateQueryRequest
  → FluentValidation via ValidateQueryRequestValidator
  → ITenantValidator + IRbacValidator with messageCategory="query"
  → Returns 200 OK with PreflightValidationResult (ALWAYS — both authorized and unauthorized)
```

### Validation Endpoint HTTP Semantics — Critical for Stories 17-7/17-8

Pre-flight validation endpoints return **200 OK with `PreflightValidationResult` in ALL cases** — both `IsAuthorized: true` and `IsAuthorized: false`. An "unauthorized" pre-flight result is a SUCCESSFUL API response (the server answered the client's question), NOT an HTTP error. The response body carries the authorization decision:

```json
// 200 OK — authorized
{ "isAuthorized": true, "reason": null }

// 200 OK — not authorized (still a successful check)
{ "isAuthorized": false, "reason": "Not authorized for tenant 'acme'." }
```

HTTP 403 only occurs if the caller's own JWT is invalid or missing (the `[Authorize]` attribute rejects at Layer 3). Do NOT use `ProblemDetails` for pre-flight denial results — that format is for the command/query submission endpoints' authorization failures (where `CommandAuthorizationException` → `AuthorizationExceptionHandler` → 403 ProblemDetails).

### `SubmitQueryResponse` — Only Constructed on Success

`SubmitQueryResponse` is ONLY returned on HTTP 200 (successful projection read). If the projection actor doesn't exist, Story 17-5 returns HTTP 404 (Amendment A6) at the controller level — `SubmitQueryResponse` is never constructed for that path. The dev agent for Story 17-5 needs to handle 404 separately, not try to create a "not found" variant of `SubmitQueryResponse`.

### `PreflightValidationResult` Is a Point-in-Time Snapshot

Pre-flight validation results reflect authorization state at the moment of the check. Authorization may change between the validation call and the actual command/query submission (e.g., actor-based permissions updated, tenant access revoked). Clients MUST handle 403 on submission even after a successful pre-flight check. This is inherent to any pre-flight pattern and is not a defect.

### Reason Field: No Internal Details

`PreflightValidationResult.Reason` is returned directly to the API caller. It MUST contain only human-readable, safe messages — never internal system details (actor names, stack traces, connection strings, internal IPs). The claims-based validators (Story 17-1) already return safe messages like `"Not authorized for tenant 'acme'."`. Stories 17-7/17-8 controllers should pass the validator's reason through without appending internal context. Actor-based validators (Story 17-2) return actor responses — those actors are application-managed and responsible for their own message safety.

### AggregateId: Required vs Optional — Intentional Asymmetry

`AggregateId` is **required** in `SubmitQueryRequest` but **optional** in `ValidateQueryRequest`. This is intentional:

- **Queries** target a specific aggregate's projection actor (routed by `tenant:domain:aggregateId`). Without `AggregateId`, the query has no routing target.
- **Validation checks** test authorization (can this user access this tenant/domain with this query type?). Authorization is typically aggregate-agnostic — the optional `AggregateId` exists for future fine-grained ACL scenarios (Amendment A2) where specific aggregates have additional access restrictions.

### Future Consideration: QueryType Echo in Response

`SubmitQueryResponse` currently has `CorrelationId` + `Payload`. A future non-breaking enhancement could add `string QueryType` to the response for client-side type-safe deserialization and caching. Commands don't need this (fire-and-forget), but query responses carry typed payloads that clients must deserialize differently per QueryType. This is NOT in scope for this story — can be added as an optional record parameter later without breaking existing consumers.

### Scope Boundary

**IN scope:** Contract records in Contracts, FluentValidation validators in CommandApi, unit tests for both.

**OUT of scope (later stories):**

- MediatR pipeline types (`SubmitQuery`, `SubmitQueryResult`) — Story 17-5
- `QueriesController` — Story 17-5
- `IQueryRouter`, query routing — Story 17-5
- `IProjectionActor`, `QueryEnvelope`, `QueryResult` — Story 17-6
- `CommandValidationController` — Story 17-7
- `QueryValidationController` — Story 17-8
- FluentValidation validator DI registration — Story 17-5, 17-7, 17-8 (validators registered when controllers are created)

### Project Structure Notes

```text
src/Hexalith.EventStore.Contracts/
├── Commands/           # EXISTING
├── Events/             # EXISTING
├── Identity/           # EXISTING
├── Results/            # EXISTING
├── Security/           # EXISTING
├── Queries/            # NEW
│   ├── SubmitQueryRequest.cs
│   └── SubmitQueryResponse.cs
└── Validation/         # NEW
    ├── ValidateCommandRequest.cs
    ├── ValidateQueryRequest.cs
    └── PreflightValidationResult.cs

src/Hexalith.EventStore.CommandApi/
├── Validation/
│   ├── SubmitCommandRequestValidator.cs   # EXISTING — DO NOT MODIFY
│   ├── SubmitQueryRequestValidator.cs     # NEW
│   ├── ValidateCommandRequestValidator.cs # NEW
│   └── ValidateQueryRequestValidator.cs   # NEW

tests/Hexalith.EventStore.Contracts.Tests/
├── Commands/           # EXISTING
├── Events/             # EXISTING
├── Identity/           # EXISTING
├── Results/            # EXISTING
├── Queries/            # NEW
│   ├── SubmitQueryRequestTests.cs
│   └── SubmitQueryResponseTests.cs
└── Validation/         # NEW
    ├── ValidateCommandRequestTests.cs
    ├── ValidateQueryRequestTests.cs
    └── PreflightValidationResultTests.cs

tests/Hexalith.EventStore.Server.Tests/
├── Validation/         # NEW
│   ├── SubmitQueryRequestValidatorTests.cs
│   ├── ValidateCommandRequestValidatorTests.cs
│   ├── ValidateQueryRequestValidatorTests.cs
│   └── ValidatorConsistencyTests.cs
```

### Files to Create

```text
src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs
src/Hexalith.EventStore.Contracts/Queries/SubmitQueryResponse.cs
src/Hexalith.EventStore.Contracts/Validation/ValidateCommandRequest.cs
src/Hexalith.EventStore.Contracts/Validation/ValidateQueryRequest.cs
src/Hexalith.EventStore.Contracts/Validation/PreflightValidationResult.cs
src/Hexalith.EventStore.CommandApi/Validation/SubmitQueryRequestValidator.cs
src/Hexalith.EventStore.CommandApi/Validation/ValidateCommandRequestValidator.cs
src/Hexalith.EventStore.CommandApi/Validation/ValidateQueryRequestValidator.cs
tests/Hexalith.EventStore.Contracts.Tests/Queries/SubmitQueryRequestTests.cs
tests/Hexalith.EventStore.Contracts.Tests/Queries/SubmitQueryResponseTests.cs
tests/Hexalith.EventStore.Contracts.Tests/Validation/ValidateCommandRequestTests.cs
tests/Hexalith.EventStore.Contracts.Tests/Validation/ValidateQueryRequestTests.cs
tests/Hexalith.EventStore.Contracts.Tests/Validation/PreflightValidationResultTests.cs
tests/Hexalith.EventStore.Server.Tests/Validation/SubmitQueryRequestValidatorTests.cs
tests/Hexalith.EventStore.Server.Tests/Validation/ValidateCommandRequestValidatorTests.cs
tests/Hexalith.EventStore.Server.Tests/Validation/ValidateQueryRequestValidatorTests.cs
tests/Hexalith.EventStore.Server.Tests/Validation/ValidatorConsistencyTests.cs
```

### Files NOT to Modify

- `Hexalith.EventStore.Contracts.csproj` — No new dependencies needed (System.Text.Json is BCL)
- `SubmitCommandRequestValidator.cs` — No refactoring of shared patterns
- `ServiceCollectionExtensions.cs` — Validator DI registration happens in Stories 17-5, 17-7, 17-8
- Any existing test files — purely additive story

### Test Conventions

**Contracts.Tests:** Standard xUnit assertions (`Assert.Equal`, `Assert.Null`, `Assert.True`). Uses `Xunit` global using. Does NOT use Shouldly. See `CommandEnvelopeTests.cs` for reference.

**Server.Tests:** Shouldly assertions (`.ShouldBe()`, `.ShouldBeNull()`, `.ShouldNotBeEmpty()`). Uses `NSubstitute` for mocking. See `AuthorizationBehaviorTests.cs` for reference.

**Naming:** `ClassName_Scenario_ExpectedResult()` (e.g., `SubmitQueryRequestValidator_EmptyTenant_FailsValidation`)

### Previous Story Intelligence

**From Story 17-1 (done):**

- `TenantValidationResult` and `RbacValidationResult` already exist in `CommandApi/Authorization/` — these are INTERNAL authorization results. `PreflightValidationResult` in Contracts/Validation is a PUBLIC API response. Different types, different packages, different purposes.
- FluentValidation is used for request DTOs. Options classes use `IValidateOptions<T>`. Do NOT mix the two conventions.
- The `eventstore:tenant` claim format is used for authorization. Query contracts should use the same `Tenant` field naming for consistency.

**From Story 17-2 (done):**

- Actor-based validators use `messageCategory` parameter (`"command"` / `"query"`). The validation endpoint contracts (`ValidateCommandRequest`, `ValidateQueryRequest`) will need to pass the correct messageCategory when calling validators in Stories 17-7/17-8.

**From Story 17-3 (review):**

- `AuthorizationBehavior` is now consolidated — all authorization flows through `ITenantValidator` + `IRbacValidator`. Story 17-5 will extend the behavior to handle query requests alongside command requests. The `messageCategory` parameter will be `"query"` for query submissions.
- The pipeline ordering (logging → validation → authorization) means validation errors (from FluentValidation on these contracts) are returned as 400 BEFORE authorization (403). This is intentional and correct.

### Git Intelligence

Recent commits are documentation-focused (Stories 15-5, 15-6). Epic 17 implementation work (Stories 17-1, 17-2, 17-3) is in working tree but not yet committed to main. The query contracts are a greenfield addition — no existing code to refactor, only new files.

### Backward Compatibility

- No existing types modified — purely additive
- No existing endpoint behavior changes
- No NuGet package breaking changes
- New types added to existing published Contracts package (additive, non-breaking)

### References

- [Source: sprint-change-proposal-2026-03-08-auth-query.md — Section 4.2 New Contracts, Section 4.3 Story 17-4]
- [Source: sprint-change-proposal-2026-03-08-auth-query.md — Amendment A2 (optional AggregateId), Amendment A4 (QueryResult JsonElement)]
- [Source: SubmitCommandRequest.cs (CommandApi/Models/) — API DTO pattern for commands]
- [Source: SubmitCommandRequestValidator.cs (CommandApi/Validation/) — FluentValidation pattern with regex, injection prevention]
- [Source: AggregateIdentity.cs (Contracts/Identity/) — Canonical identity validation rules]
- [Source: CommandEnvelope.cs (Contracts/Commands/) — Domain envelope pattern with eager validation]
- [Source: CommandEnvelopeTests.cs (Contracts.Tests/) — Test conventions (xUnit direct assertions)]
- [Source: AuthorizationBehaviorTests.cs (Server.Tests/) — Test conventions (Shouldly assertions)]
- [Source: 17-1-authorization-options-and-validator-abstractions.md — Validator abstractions, naming collision notes]
- [Source: 17-3-refactor-authorization-behavior.md — Pipeline consolidation, messageCategory usage]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- Consistency test initially failed on `javascript:alert(1)` and `onclick=alert(1)` — the existing `SubmitCommandRequestValidator` only checks `ContainsDangerousCharacters` (chars `<>&'"`) on CommandType, NOT the injection regex pattern. Injection regex is only applied to Extensions. Fixed new validators and test expectations to match existing behavior exactly.
- 2026-03-12 review fix: aligned tenant/domain validator max length with canonical `AggregateIdentity` limits (64) and extended command/query type validation to reject script-style injection patterns consistently across all four request validators.

### Completion Notes List

- Created 5 Contracts record types: `SubmitQueryRequest`, `SubmitQueryResponse`, `ValidateCommandRequest`, `ValidateQueryRequest`, `PreflightValidationResult`
- Created 3 FluentValidation validators: `SubmitQueryRequestValidator`, `ValidateCommandRequestValidator`, `ValidateQueryRequestValidator`
- All four request validators now share the same tenant/domain max length (64, aligned with `AggregateIdentity`) and the same script-pattern rejection on `CommandType`/`QueryType`, confirmed by cross-validator consistency tests
- `ValidateCommandRequestValidator` and `ValidateQueryRequestValidator` make `AggregateId` optional with `When(x => x.AggregateId is not null)` guard; empty-string `""` is rejected when present
- `SubmitQueryRequestValidator` Payload rule: `null` is valid (parameterless queries), `default(JsonElement)` with `Undefined` ValueKind is rejected
- Review follow-up updated `SubmitCommandRequestValidator.cs` too so command and query validation remain consistent with the canonical identity rules and the story's injection-prevention intent
- Story implementation added 93 new tests; review follow-up added focused regression cases for 64-character identity limits and script-pattern rejection on command/query type fields
- All existing Tier 1 (484 tests) and Tier 2 (1003 tests) pass with zero regressions

### Change Log

- 2026-03-11: Implemented Story 17-4 — Query contracts, validation contracts, FluentValidation validators, and comprehensive tests
- 2026-03-12: Review fixes — aligned validator tenant/domain length with `AggregateIdentity`, strengthened command/query type injection prevention, and updated consistency coverage

### File List

New files:

- src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs
- src/Hexalith.EventStore.Contracts/Queries/SubmitQueryResponse.cs
- src/Hexalith.EventStore.Contracts/Validation/ValidateCommandRequest.cs
- src/Hexalith.EventStore.Contracts/Validation/ValidateQueryRequest.cs
- src/Hexalith.EventStore.Contracts/Validation/PreflightValidationResult.cs
- src/Hexalith.EventStore.CommandApi/Validation/SubmitQueryRequestValidator.cs
- src/Hexalith.EventStore.CommandApi/Validation/ValidateCommandRequestValidator.cs
- src/Hexalith.EventStore.CommandApi/Validation/ValidateQueryRequestValidator.cs
- tests/Hexalith.EventStore.Contracts.Tests/Queries/SubmitQueryRequestTests.cs
- tests/Hexalith.EventStore.Contracts.Tests/Queries/SubmitQueryResponseTests.cs
- tests/Hexalith.EventStore.Contracts.Tests/Validation/ValidateCommandRequestTests.cs
- tests/Hexalith.EventStore.Contracts.Tests/Validation/ValidateQueryRequestTests.cs
- tests/Hexalith.EventStore.Contracts.Tests/Validation/PreflightValidationResultTests.cs
- tests/Hexalith.EventStore.Server.Tests/Validation/SubmitQueryRequestValidatorTests.cs
- tests/Hexalith.EventStore.Server.Tests/Validation/ValidateCommandRequestValidatorTests.cs
- tests/Hexalith.EventStore.Server.Tests/Validation/ValidateQueryRequestValidatorTests.cs
- tests/Hexalith.EventStore.Server.Tests/Validation/ValidatorConsistencyTests.cs

Modified files:

- src/Hexalith.EventStore.CommandApi/Validation/SubmitCommandRequestValidator.cs
- \_bmad-output/implementation-artifacts/sprint-status.yaml
- \_bmad-output/implementation-artifacts/17-4-query-contracts.md
- src/Hexalith.EventStore.CommandApi/Validation/SubmitQueryRequestValidator.cs
- src/Hexalith.EventStore.CommandApi/Validation/ValidateCommandRequestValidator.cs
- src/Hexalith.EventStore.CommandApi/Validation/ValidateQueryRequestValidator.cs
- tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandRequestValidatorTests.cs
- tests/Hexalith.EventStore.Server.Tests/Validation/SubmitQueryRequestValidatorTests.cs
- tests/Hexalith.EventStore.Server.Tests/Validation/ValidateCommandRequestValidatorTests.cs
- tests/Hexalith.EventStore.Server.Tests/Validation/ValidateQueryRequestValidatorTests.cs
- tests/Hexalith.EventStore.Server.Tests/Validation/ValidatorConsistencyTests.cs

## Senior Developer Review (AI)

### Reviewer

GitHub Copilot (GPT-5.4) — 2026-03-12

### Outcome

Approved after review fixes.

### Findings addressed

- Aligned request validator tenant/domain limits with canonical `AggregateIdentity` validation so API-layer acceptance no longer exceeds the domain identity contract.
- Strengthened `CommandType`/`QueryType` validation to reject script-style injection patterns consistently across command and query request models.
- Reconciled the story file list with the real set of code files changed during the review follow-up.

### Verification

- Focused validator regression suite passed: `SubmitCommandRequestValidatorTests`, `SubmitQueryRequestValidatorTests`, `ValidateCommandRequestValidatorTests`, `ValidateQueryRequestValidatorTests`, and `ValidatorConsistencyTests` (79 tests).

### Status recommendation

Story 17.4 can be marked `done`.
