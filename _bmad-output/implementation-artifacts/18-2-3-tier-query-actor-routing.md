# Story 18.2: 3-Tier Query Actor Routing

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **platform operator**,
I want **queries routed to query actors using a 3-tier model based on query parameters**,
So that **cached results are correctly scoped to the query's specificity**.

## Acceptance Criteria

1. **Tier 1 — EntityId-scoped routing** — **Given** a query with `EntityId="order-123"`, **When** routing, **Then** it routes to actor `{QueryType}:{TenantId}:{EntityId}` (FR50, colon separator per codebase convention)
2. **Tier 2 — Payload-checksum routing** — **Given** a query without EntityId but with non-empty serialized payload, **When** routing, **Then** it routes to actor `{QueryType}:{TenantId}:{Checksum}` where Checksum is 11-char truncated SHA256 base64url of the serialized payload (FR50)
3. **Tier 3 — Tenant-wide routing** — **Given** a query without EntityId and with empty payload, **When** routing, **Then** it routes to actor `{QueryType}:{TenantId}` (FR50)
4. **Serialization non-determinism accepted** — **Given** two semantically identical queries with different JSON key ordering, **When** computing checksums, **Then** they produce different checksums and route to separate cache actors — accepted trade-off documented in FR50
5. **EntityId field added to query contracts** — **Given** the existing `SubmitQueryRequest`, `SubmitQuery`, and `QueryEnvelope` types, **When** `EntityId` is added as an optional field, **Then** it flows through the full query pipeline without breaking existing consumers (backward compatible — existing queries without EntityId continue to work)
6. **Checksum is deterministic for identical byte payloads** — **Given** two queries with identical `Payload` byte arrays, **When** computing checksums, **Then** they produce the same 11-char base64url checksum
7. **Checksum computation is a pure function** — **Given** a payload byte array, **When** calling the checksum function, **Then** it returns SHA256 → base64url → truncated to 11 chars, with no side effects. URL-safe alphabet: `+` → `-`, `/` → `_`, `=` stripped
8. **Existing query routing still works** — **Given** a query without EntityId and with empty payload (tier 3), **When** routed through the updated `QueryRouter`, **Then** it routes to actor type `ProjectionActor` (unchanged) with actor ID `{QueryType}:{TenantId}` (new format replaces old `{tenant}:{domain}:{aggregateId}`)
9. **QueryActorIdDerivation helper** — **Given** query parameters (QueryType, TenantId, optional EntityId, optional Payload), **When** deriving the query actor ID, **Then** a static helper computes the correct tier-specific actor ID using colon separator
10. **All existing tests pass** — All Tier 1, Tier 2, and Tier 3 tests continue to pass with zero behavioral change
11. **EntityId validation in request validator** — **Given** a `SubmitQueryRequest` with an EntityId, **When** validated, **Then** EntityId is optional, max 256 chars, matches AggregateId character pattern (`^[a-zA-Z0-9]([a-zA-Z0-9._-]*[a-zA-Z0-9])?$`), and rejects colons (reserved for actor ID separator)
12. **QueryEnvelope backward compatibility** — **Given** a DAPR-serialized `QueryEnvelope` without an `EntityId` field (from before this change), **When** deserialized, **Then** `EntityId` is null (DataContractSerializer tolerance for missing nullable fields)
13. **QueryType colon prohibition** — **Given** a `SubmitQueryRequest` with `QueryType` containing a colon (e.g., `"Get:Order"`), **When** validated, **Then** the request is rejected. Colons are reserved as actor ID separators; allowing them in QueryType breaks structural disjointness (same principle as Story 18-1 PM-4)

## Tasks / Subtasks

- [x] Task 1: Add `EntityId` to query contracts (AC: #5, #12)
    - [x] 1.1 Add `string? EntityId = null` to `SubmitQueryRequest` in `src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs` — optional parameter at end, backward compatible
    - [x] 1.2 Add `string? EntityId = null` to `SubmitQuery` in `src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs` — optional parameter at end, after `UserId`
    - [x] 1.3 Add `EntityId` to `QueryEnvelope` in `src/Hexalith.EventStore.Server/Actors/QueryEnvelope.cs`:
        - Add constructor parameter `string? entityId = null` (NO `ThrowIfNullOrWhiteSpace` — nullable)
        - Add `[DataMember] public string? EntityId { get; init; }` property
        - Update `ToString()` to include EntityId when non-null
        - Keep `AggregateIdentity` property unchanged (Domain + AggregateId still needed by projection actors)
    - [x] 1.4 Update `QueriesController.Submit()` in `src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs` — forward `request.EntityId` to `SubmitQuery` constructor

- [x] Task 2: Create `QueryActorIdHelper` static class (AC: #1, #2, #3, #6, #7, #9)
    - [x] 2.1 Create `src/Hexalith.EventStore.Server/Queries/QueryActorIdHelper.cs`
    - [x] 2.2 Implement `static string DeriveActorId(string queryType, string tenantId, string? entityId, byte[] payload)` with tier logic:
        - Guard: `ArgumentNullException.ThrowIfNull(payload)` — payload is never null, use empty array for Tier 3
        - If `entityId` is non-null/non-empty → Tier 1: `{queryType}:{tenantId}:{entityId}`
        - Else if `payload.Length > 0` → Tier 2: `{queryType}:{tenantId}:{ComputeChecksum(payload)}`
        - Else → Tier 3: `{queryType}:{tenantId}`
    - [x] 2.3 Implement `static string ComputeChecksum(byte[] payload)` — `SHA256.HashData(payload)` → `Convert.ToBase64String()` → replace `+`→`-`, `/`→`_`, strip `=` → take first 11 chars
    - [x] 2.4 Both methods are pure functions with zero DAPR dependencies

- [x] Task 3: Refactor `QueryRouter` for 3-tier routing (AC: #1, #2, #3, #8)
    - [x] 3.1 Modify `QueryRouter.RouteQueryAsync()` in `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`
    - [x] 3.2 Replace current actor ID derivation (`var identity = new AggregateIdentity(...); string actorId = identity.ActorId;`) with `string actorId = QueryActorIdHelper.DeriveActorId(query.QueryType, query.Tenant, query.EntityId, query.Payload)`
    - [x] 3.3 The actor type name remains `ProjectionActorTypeName = "ProjectionActor"` — unchanged
    - [x] 3.4 Add structured log entry for which tier was selected (EventId 1205)
    - [x] 3.5 Pass EntityId through to `QueryEnvelope` constructor
    - [x] 3.6 Remove `AggregateIdentity` construction and its `using Hexalith.EventStore.Contracts.Identity;` import (no longer needed for routing — TreatWarningsAsErrors will fail on unused usings)

- [x] Task 4: Add EntityId validation to `SubmitQueryRequestValidator` (AC: #11)
    - [x] 4.1 Modify `src/Hexalith.EventStore.CommandApi/Validation/SubmitQueryRequestValidator.cs`
    - [x] 4.2 Add EntityId validation rules (when present): `MaximumLength(256)`, regex pattern `^[a-zA-Z0-9]([a-zA-Z0-9._-]*[a-zA-Z0-9])?$` (same as AggregateId), and colon prohibition (reserved for actor ID separator)
    - [x] 4.3 Add QueryType colon prohibition rule: `RuleFor(x => x.QueryType).Must(v => !v.Contains(':'))` — colons are reserved as actor ID separators (AC #13). NOTE: This rule is query-specific (only SubmitQueryRequestValidator) because colons are dangerous for query actor ID derivation, not command routing. If `ValidatorConsistencyTests.TypeFieldConsistency_AllValidatorsAgree` fails, add a separate query-specific test rather than adding colon rules to command validators.
    - [x] 4.4 Follow existing validator pattern (Tenant/Domain/AggregateId rules)

- [x] Task 5: Update `IProjectionActor` documentation (AC: #8)
    - [x] 5.1 Update XML doc comment in `src/Hexalith.EventStore.Server/Actors/IProjectionActor.cs` — change actor ID format from `"{tenant}:{domain}:{aggregateId}"` to `"{QueryType}:{TenantId}[:{EntityId|Checksum}]"` (3-tier format)

- [x] Task 6: Unit tests — Tier 1: Pure functions (AC: #1, #2, #3, #4, #6, #7)
    - [x] 6.1 Create `tests/Hexalith.EventStore.Server.Tests/Queries/QueryActorIdHelperTests.cs`
    - [x] 6.2 Test Tier 1: EntityId present → actor ID = `{QueryType}:{TenantId}:{EntityId}`
    - [x] 6.3 Test Tier 2: No EntityId, non-empty payload → actor ID = `{QueryType}:{TenantId}:{11-char checksum}`
    - [x] 6.4 Test Tier 3: No EntityId, empty payload → actor ID = `{QueryType}:{TenantId}`
    - [x] 6.5 Test checksum determinism: identical payloads → identical checksums
    - [x] 6.6 Test checksum format: exactly 11 chars, URL-safe alphabet (no `+`, `/`, `=`)
    - [x] 6.7 Test checksum divergence: different payloads → different checksums (with high probability)
    - [x] 6.8 Test serialization non-determinism: `{"a":1,"b":2}` vs `{"b":2,"a":1}` as bytes → different checksums (AC #4)
    - [x] 6.9 Test null/empty EntityId treated as absent (Tier 2 or Tier 3, not Tier 1)
    - [x] 6.10 Test colon separator used in all tiers (not hyphen)

- [x] Task 7: Update existing test files for EntityId parameter and new actor ID format (AC: #10, #12)
    - [x] 7.1 `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs`:
        - Update `CreateTestQuery()` to include `EntityId: null`
        - Update `RouteQueryAsync_RoutesToCorrectActor()` assertion: actor ID changes from `"test-tenant:orders:order-1"` to `"GetOrderStatus:test-tenant"` (Tier 3)
        - Update `RouteQueryAsync_GenericExceptionWithActorNotFoundPattern_ReturnsNotFound()` exception message
        - Update `RouteQueryAsync_ConstructsCorrectQueryEnvelope()` to assert `e.EntityId == null`
        - Add test: query with EntityId routes to Tier 1 actor ID `{QueryType}:{TenantId}:{EntityId}`
        - Add test: query with non-empty payload routes to Tier 2 actor ID `{QueryType}:{TenantId}:{Checksum}`
        - Add test: query with empty payload and no EntityId routes to Tier 3 actor ID `{QueryType}:{TenantId}`
    - [x] 7.2 `tests/Hexalith.EventStore.Server.Tests/Actors/QueryEnvelopeTests.cs`:
        - Update `CreateValid()` helper to include `string? entityId = null` parameter
        - Update `Constructor_ValidFields_SetsAllProperties` to assert EntityId
        - Update `JsonRoundTrip_PreservesAllProperties` to assert EntityId
        - Update `ToString_RedactsPayload` if EntityId added to ToString output
        - Add test: null EntityId accepted by constructor without exception
        - Add test: non-null EntityId preserved through serialization round-trip
        - Add test: old serialized QueryEnvelope without EntityId deserializes with EntityId=null (AC #12) — use `DataContractSerializer` (DAPR's actual serializer), not just JsonSerializer
    - [x] 7.3 `tests/Hexalith.EventStore.Server.Tests/Pipeline/AuthorizationBehaviorTests.cs`:
        - Update `CreateTestQuery()` helper (line ~370) to include `EntityId: null`
    - [x] 7.4 `tests/Hexalith.EventStore.Server.Tests/Pipeline/Queries/SubmitQueryTests.cs`:
        - Update constructor tests to include EntityId parameter
    - [x] 7.5 `tests/Hexalith.EventStore.Server.Tests/Pipeline/Queries/SubmitQueryHandlerTests.cs`:
        - Update `CreateTestQuery()` helper to include `EntityId: null`
    - [x] 7.6 `tests/Hexalith.EventStore.Contracts.Tests/Queries/SubmitQueryRequestTests.cs`:
        - Update constructor tests to include EntityId parameter
    - [x] 7.7 `tests/Hexalith.EventStore.Server.Tests/Validation/ValidatorConsistencyTests.cs`:
        - Update SubmitQueryRequest constructions (3 locations) to include `EntityId: null`
    - [x] 7.8 `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs`:
        - Add test: EntityId from request is forwarded to SubmitQuery (verify Tier 1 data flow)
        - Add test: null EntityId from request results in null EntityId in SubmitQuery
    - [x] 7.9 `tests/Hexalith.EventStore.Server.Tests/Validation/SubmitQueryRequestValidatorTests.cs`:
        - Add test: EntityId with colon rejected
        - Add test: EntityId exceeding 256 chars rejected
        - Add test: EntityId with invalid chars (violating AggregateId pattern) rejected
        - Add test: null EntityId accepted (optional field)
        - Add test: QueryType with colon rejected (AC #13)
        - Add test: valid EntityId accepted

- [x] Task 8: Verify zero regression (AC: #10)
    - [x] 8.1 All Tier 1 tests pass: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ && dotnet test tests/Hexalith.EventStore.Client.Tests/ && dotnet test tests/Hexalith.EventStore.Sample.Tests/ && dotnet test tests/Hexalith.EventStore.Testing.Tests/`
    - [x] 8.2 All Tier 2 tests pass: `dotnet test tests/Hexalith.EventStore.Server.Tests/`
    - [x] 8.3 Full solution build: `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 errors, 0 warnings

## Architectural Decisions

**ADR-18.2a: Actor ID Format — Colon-Separated (Matching Codebase Convention)**

- **Choice:** Query actor IDs use colon separator: `{QueryType}:{TenantId}:{EntityId}` (matching aggregate and ETag actor conventions)
- **Deviation from FR50:** FR50 literally specifies hyphen format (`{QueryType}-{TenantId}-{EntityId}`), but hyphens are allowed in QueryType, TenantId, and EntityId values → creates unresolvable parsing ambiguity. This is the same problem Story 18-1 identified (PM-4) for ETag actors, resolved by choosing colon separator.
- **Precedent:** Aggregate actors: `{tenant}:{domain}:{aggregateId}` (colon). ETag actors: `{ProjectionType}:{TenantId}` (colon). Query actors follow the same convention.
- **Rationale:** Colons are forbidden in all component values (enforced by `AggregateIdentity` validation). Using colon as separator guarantees **structural disjointness** — actor IDs can always be split on colon to recover original components without ambiguity. See `docs/concepts/identity-scheme.md` for the design principle.

**ADR-18.2b: Checksum Algorithm — 11-char Truncated SHA256 Base64url**

- **Choice:** `SHA256.HashData(payload)` → base64url → first 11 chars (as specified in FR50)
- **Collision risk:** 11 chars = 66 bits of entropy. At 1M unique queries, collision probability is ~10⁻⁸. Acceptable for cache keys — a collision means two different queries share a cache actor (performance issue, not correctness issue).
- **Trade-off:** Shorter checksums are easier to read in logs but have higher collision probability. 11 chars balances readability vs. uniqueness.
- **Note:** The checksum is base64url-encoded, which contains `-` and `_` characters. These do NOT conflict with the colon separator.

**ADR-18.2c: EntityId Is Optional and Additive**

- **Choice:** `EntityId` is added as an optional nullable field to all query types (`SubmitQueryRequest`, `SubmitQuery`, `QueryEnvelope`). Existing queries without EntityId continue to route via Tier 2 or Tier 3.
- **Rationale:** Backward compatibility — no breaking changes to existing consumers. `QueryEnvelope` uses `[DataMember]` with nullable type, so old DAPR-serialized messages without EntityId still deserialize correctly.

**ADR-18.2d: Routing Change — Query Actor ID No Longer Uses AggregateIdentity**

- **Choice:** The `QueryRouter` no longer derives actor ID from `AggregateIdentity.ActorId` (`{tenant}:{domain}:{aggregateId}`). Instead, it uses `QueryActorIdHelper.DeriveActorId()` which produces `{QueryType}:{TenantId}[:{specificity}]` format.
- **Impact:** This is a **behavioral change** to query routing. Any existing projection actors registered with actor IDs in the `{tenant}:{domain}:{aggregateId}` format will no longer receive queries through this router. Since no production projection actors exist yet (Epic 18 is the first to implement the full query pipeline), this is safe.
- **Test impact:** 7 test files need updates for the new actor ID format and EntityId parameter (see Task 7 for complete list).

## Pre-mortem Findings

**PM-1: Existing QueryRouterTests Expect Old Actor ID Format**

- `QueryRouterTests.RouteQueryAsync_RoutesToCorrectActor()` at line 75 asserts actor ID = `"test-tenant:orders:order-1"`. After this story, the actor ID will be `"GetOrderStatus:test-tenant"` (Tier 3, since test query has empty payload and no EntityId). Tests MUST be updated.

**PM-2: QueryEnvelope Still Needs Domain and AggregateId**

- Even though the query actor ID no longer uses `{domain}:{aggregateId}`, the `QueryEnvelope` still carries these fields to the projection actor. The projection actor needs domain context to fetch data from the correct domain service. Do NOT remove these fields. The `QueryEnvelope.AggregateIdentity` property remains valid and useful.

**PM-3: SubmitQuery Record — 11+ Constructor Call Sites Break**

- `SubmitQuery` is constructed in at least 5 files: `QueriesController.cs`, `QueryRouterTests.cs`, `SubmitQueryTests.cs`, `SubmitQueryHandlerTests.cs`, `AuthorizationBehaviorTests.cs`. Adding `EntityId` as optional positional parameter at end is backward-compatible for named parameter callers but may require explicit `EntityId: null` in some test helpers. Check ALL call sites.

**PM-4: QueriesController Must Forward EntityId End-to-End**

- Data flow: `SubmitQueryRequest.EntityId` → `SubmitQuery.EntityId` → `QueryEnvelope.EntityId` → `QueryActorIdHelper.DeriveActorId()`. If any link drops EntityId, Tier 1 routing fails silently (query falls to Tier 2 or 3 — no error, just wrong caching granularity).

**PM-5: QueryEnvelope DataContractSerializer Backward Compatibility**

- Adding `[DataMember] string? EntityId` to `QueryEnvelope` means old DAPR-serialized messages without this field MUST still deserialize correctly. `DataContractSerializer` tolerates missing nullable fields by defaulting to null. EntityId MUST be nullable (`string?`) — never required. Verify with a unit test that deserializes an old-format message.

**PM-6: No Actor ID Collision Between Types**

- ETag actor IDs (`{ProjectionType}:{TenantId}`) and query actor IDs (`{QueryType}:{TenantId}[:{specificity}]`) both use colon separator but are in different actor type namespaces (`ETagActor` vs `ProjectionActor`). DAPR resolves actors by `(ActorId, ActorTypeName)` tuple — no collision possible.

**PM-7: QueryEnvelopeTests Has 8+ Tests That Break**

- `QueryEnvelopeTests.cs` has `CreateValid()` helper, constructor validation tests, JSON round-trip test, ToString test, and AggregateIdentity test. ALL must be updated for the new EntityId parameter.

**PM-8: SubmitQueryRequestValidator Must Include EntityId Rules**

- Without validation, a malformed EntityId (e.g., 10,000 chars, control characters) would flow unchecked to actor ID derivation. Add optional validation when EntityId is present: max 256 chars, matching AggregateId character pattern.

**PM-9: QueryType Colon Bypass — Structural Disjointness Gap**

- ADR-18.2a claims colons are "forbidden in all component values" but `SubmitQueryRequestValidator` does NOT currently validate colons in QueryType. A QueryType like `"Get:Order"` would produce actor ID `"Get:Order:tenant1"` — 3 segments that look like Tier 1 (`{QueryType}:{TenantId}:{EntityId}`) when it's actually Tier 3. This breaks structural disjointness and could cause cache poisoning. Task 4.3 adds the colon prohibition rule to close this gap.

## Dev Notes

### 3-Tier Routing Model

| Tier | Condition                         | Actor ID Format                     | Cache Granularity           |
| ---- | --------------------------------- | ----------------------------------- | --------------------------- |
| 1    | `EntityId` present                | `{QueryType}:{TenantId}:{EntityId}` | Single entity               |
| 2    | No EntityId, `Payload.Length > 0` | `{QueryType}:{TenantId}:{Checksum}` | Parameterized query         |
| 3    | No EntityId, empty payload        | `{QueryType}:{TenantId}`            | Full tenant-wide projection |

**Tier selection is implicit** — `QueryActorIdHelper` inspects query parameters and selects the tier. No explicit tier field needed in contracts.

**Colon separator rationale:** Colons are forbidden in all component values (TenantId, Domain, AggregateId, EntityId — enforced by `AggregateIdentity` validation regex). This guarantees structural disjointness: `split(':')` always recovers original components. FR50 literally specifies hyphens, but Story 18-1 PM-4 already identified and corrected this same ambiguity for ETag actors. Query actors follow the same correction.

### Checksum Computation — Pure Function

```csharp
public static string ComputeChecksum(byte[] payload)
{
    byte[] hash = SHA256.HashData(payload);
    return Convert.ToBase64String(hash)
        .Replace('+', '-')
        .Replace('/', '_')
        .TrimEnd('=')[..11]; // First 11 chars of base64url-encoded SHA256
}
```

- `SHA256.HashData()` is a .NET 5+ static method — no `using` block needed
- 32-byte SHA256 → 44-char base64 → 11-char truncation = 66 bits of entropy
- URL-safe encoding: same pattern as `ETagActor.GenerateETag()` (Story 18.1)
- Checksum contains `-` and `_` chars (base64url alphabet) — these do NOT conflict with colon separator

### Data Flow Change

**Before (current):**

```
SubmitQueryRequest → SubmitQuery → QueryRouter → AggregateIdentity.ActorId → {tenant}:{domain}:{aggregateId} → ProjectionActor
```

**After (Story 18.2):**

```
SubmitQueryRequest(+EntityId) → SubmitQuery(+EntityId) → QueryRouter → QueryActorIdHelper.DeriveActorId() → {QueryType}:{TenantId}[:{EntityId|Checksum}] → ProjectionActor
```

The actor type name (`"ProjectionActor"`) is **unchanged**. Only the actor ID derivation changes.

### Contract Changes (All Backward Compatible)

**`SubmitQueryRequest`** — add optional parameter:

```csharp
public record SubmitQueryRequest(
    string Tenant,
    string Domain,
    string AggregateId,
    string QueryType,
    JsonElement? Payload = null,
    string? EntityId = null);  // NEW — optional, Tier 1 routing
```

**`SubmitQuery`** — add optional parameter:

```csharp
public record SubmitQuery(
    string Tenant,
    string Domain,
    string AggregateId,
    string QueryType,
    byte[] Payload,
    string CorrelationId,
    string UserId,
    string? EntityId = null) : IRequest<SubmitQueryResult>;  // NEW
```

**`QueryEnvelope`** — add optional constructor param + `[DataMember]`:

```csharp
// Constructor: add at end
public QueryEnvelope(
    string tenantId,
    string domain,
    string aggregateId,
    string queryType,
    byte[] payload,
    string correlationId,
    string userId,
    string? entityId = null)  // NEW — no ThrowIfNullOrWhiteSpace
{
    // ... existing validation ...
    EntityId = entityId;
}

[DataMember]
public string? EntityId { get; init; }  // NEW — nullable for backward compat

// Update ToString():
public override string ToString()
    => $"QueryEnvelope {{ TenantId = {TenantId}, Domain = {Domain}, AggregateId = {AggregateId}, QueryType = {QueryType}, CorrelationId = {CorrelationId}, UserId = {UserId}{(EntityId is not null ? $", EntityId = {EntityId}" : "")}, Payload = [REDACTED {Payload.Length} bytes] }}";
```

### Structured Logging — New EventIds

Add to `QueryRouter.Log` partial class:

| EventId | Level | Message Pattern                                                                                                  |
| ------- | ----- | ---------------------------------------------------------------------------------------------------------------- |
| 1205    | Debug | `Query routing tier selected: CorrelationId={CorrelationId}, Tier={Tier}, ActorId={ActorId}, Stage=TierSelected` |

### Project Structure Notes

```text
src/Hexalith.EventStore.Contracts/Queries/
    SubmitQueryRequest.cs                      # MODIFIED — add EntityId parameter

src/Hexalith.EventStore.Server/Pipeline/Queries/
    SubmitQuery.cs                             # MODIFIED — add EntityId parameter

src/Hexalith.EventStore.Server/Actors/
    QueryEnvelope.cs                           # MODIFIED — add EntityId [DataMember] + constructor param + ToString
    IProjectionActor.cs                        # MODIFIED — update XML doc (actor ID format)

src/Hexalith.EventStore.Server/Queries/
    QueryRouter.cs                             # MODIFIED — use QueryActorIdHelper for actor ID
    QueryActorIdHelper.cs                      # NEW — 3-tier actor ID derivation + checksum

src/Hexalith.EventStore.CommandApi/Controllers/
    QueriesController.cs                       # MODIFIED — forward EntityId

src/Hexalith.EventStore.CommandApi/Validation/
    SubmitQueryRequestValidator.cs             # MODIFIED — add optional EntityId rules
```

### Files to Create (2)

```text
src/Hexalith.EventStore.Server/Queries/QueryActorIdHelper.cs
tests/Hexalith.EventStore.Server.Tests/Queries/QueryActorIdHelperTests.cs
```

### Files to Modify — Production (7)

```text
src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs               (add EntityId parameter)
src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs                 (add EntityId parameter)
src/Hexalith.EventStore.Server/Actors/QueryEnvelope.cs                         (add EntityId [DataMember] + constructor + ToString)
src/Hexalith.EventStore.Server/Actors/IProjectionActor.cs                      (update XML doc — actor ID format)
src/Hexalith.EventStore.Server/Queries/QueryRouter.cs                          (use QueryActorIdHelper, pass EntityId)
src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs            (forward EntityId)
src/Hexalith.EventStore.CommandApi/Validation/SubmitQueryRequestValidator.cs   (add optional EntityId rules)
```

### Files to Modify — Tests (9)

```text
tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs                     (update actor ID assertions, add tier routing tests)
tests/Hexalith.EventStore.Server.Tests/Actors/QueryEnvelopeTests.cs                     (update CreateValid helper, add EntityId assertions, add backward compat test)
tests/Hexalith.EventStore.Server.Tests/Pipeline/AuthorizationBehaviorTests.cs           (update CreateTestQuery helper)
tests/Hexalith.EventStore.Server.Tests/Pipeline/Queries/SubmitQueryTests.cs             (update constructor tests)
tests/Hexalith.EventStore.Server.Tests/Pipeline/Queries/SubmitQueryHandlerTests.cs      (update CreateTestQuery helper)
tests/Hexalith.EventStore.Contracts.Tests/Queries/SubmitQueryRequestTests.cs            (update constructor tests)
tests/Hexalith.EventStore.Server.Tests/Validation/ValidatorConsistencyTests.cs          (update 3 SubmitQueryRequest constructions)
tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs             (verify EntityId forwarding to SubmitQuery)
tests/Hexalith.EventStore.Server.Tests/Validation/SubmitQueryRequestValidatorTests.cs    (test EntityId + QueryType colon validation rules)
```

### Files NOT to Modify

- `IQueryRouter.cs` — interface signature unchanged (`RouteQueryAsync(SubmitQuery, CancellationToken)`)
- `QueryRouterResult.cs` — result type unchanged
- `SubmitQueryHandler.cs` — handler delegates to `IQueryRouter`, EntityId flows through `SubmitQuery` → `QueryRouter` naturally (handler doesn't inspect EntityId)
- `QueryResult.cs` — actor result type unchanged
- `AggregateIdentity.cs` — identity type unchanged (still used by command pipeline)
- `ETagActor.cs` — Story 18.1 scope, unrelated to routing
- `ServiceCollectionExtensions.cs` — no new registrations needed (QueryRouter already registered as scoped)
- `NamingConventionEngine.cs` — no convention changes needed for this story
- `QueryValidationController.cs` — pre-flight validation is decoupled from routing; EntityId not needed for validation

### Build Verification Checkpoints

After each major task group, verify the build to catch errors early:

- After Tasks 1–2: `dotnet build src/Hexalith.EventStore.Contracts/ && dotnet build src/Hexalith.EventStore.Server/`
- After Task 3: `dotnet build src/Hexalith.EventStore.Server/`
- After Task 4: `dotnet build src/Hexalith.EventStore.CommandApi/`
- After Task 6: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "QueryActorIdHelper"`
- After Task 7: `dotnet test tests/Hexalith.EventStore.Server.Tests/ && dotnet test tests/Hexalith.EventStore.Contracts.Tests/`
- Task 8: Full solution build + all Tier 1/2 tests

### Architecture Compliance

- **Colon separator** for actor IDs — matching aggregate (`{tenant}:{domain}:{aggregateId}`) and ETag (`{ProjectionType}:{TenantId}`) conventions
- **Brace style:** Follow the brace style already present in each file being modified (some files use K&R despite `.editorconfig` specifying Allman — do not reformat existing code)
- **TreatWarningsAsErrors = true** — zero warnings allowed
- **Nullable enabled** — `EntityId` is `string?` throughout
- **[DataContract]/[DataMember]** on `QueryEnvelope` — MANDATORY for DAPR actor proxy serialization. EntityId is nullable for backward compat.
- **Pure functions** — `QueryActorIdHelper` has zero DAPR dependencies, zero state
- **Structured logging** — `LoggerMessage` source generator with EventId constants (matching existing pattern in `QueryRouter.Log`)

### Previous Story Intelligence

**From Story 18-1 (ready-for-dev — ETag Actor & Projection Change Notification):**

- ETag actor IDs use colon separator: `{ProjectionType}:{TenantId}` — same convention now used for query actors
- PM-4 in Story 18-1 explicitly rejected hyphens: "FR51 says `{ProjectionType}-{TenantId}` but hyphens are allowed in projection types and tenant IDs → collision risk." — Story 18.2 applies the same correction to FR50
- `GenerateETag()` base64url helper uses same URL-safe encoding pattern as `ComputeChecksum()` — consider extracting shared helper if both are in Server package
- ETag actor type name: `"ETagActor"` — distinct from `"ProjectionActor"` (no collision even with same colon separator)

**From Story 17-5 (done — queries controller and query router):**

- `QueryRouter.ProjectionActorTypeName = "ProjectionActor"` — keep unchanged
- `QueryEnvelope` uses `[DataContract]` + `[DataMember]` — follow same pattern for new `EntityId` field
- Current actor ID derivation via `AggregateIdentity.ActorId` — being replaced by `QueryActorIdHelper.DeriveActorId()` in this story
- All existing `QueryRouterTests` use `CreateTestQuery()` helper — update this helper to include `EntityId: null`

**From Story 17-9 (done — integration and E2E tests):**

- `ActorBasedAuthWebApplicationFactory` pattern with mocked `IActorProxyFactory` — reusable for any new integration tests
- NSubstitute mock of `IActorProxyFactory.CreateActorProxy<IProjectionActor>()` — update `ActorId` assertions to match new format
- `QueryEndpointE2ETests` construct raw JSON bodies without EntityId — these should continue to work since EntityId is optional

### Git Intelligence

Recent commits:

```
a7fe357 Update sprint status to reflect completed epics and adjust generated dates
648a9db Add Implementation Readiness Assessment Report for Hexalith.EventStore
8c97752 Add integration tests for actor-based authorization and service unavailability
d8fcbc0 Add unit tests for SubmitQuery, QueryRouter, and validation logic
acd38cf feat(docs): add DAPR FAQ deep dive (Story 15-6)
```

All Epic 17 production code is stable. Story 18.2 modifies existing query routing code — the only behavioral change is actor ID derivation.

### Scope Boundary

**IN scope:**

- `EntityId` optional field added to `SubmitQueryRequest`, `SubmitQuery`, `QueryEnvelope`
- `QueryActorIdHelper` static class with `DeriveActorId()` and `ComputeChecksum()` pure functions
- `QueryRouter.RouteQueryAsync()` refactored to use `QueryActorIdHelper` for actor ID (colon separator)
- `QueriesController.Submit()` updated to forward `EntityId`
- `SubmitQueryRequestValidator` updated with optional EntityId validation rules
- `IProjectionActor` XML documentation updated for new actor ID format
- Tier 1 unit tests for `QueryActorIdHelper` (checksum, tier selection, edge cases)
- Updated 7 test files for EntityId parameter and new actor ID format

**OUT of scope:**

- ETag pre-check at query endpoint / HTTP 304 (Story 18.3)
- Query actor in-memory page cache (Story 18.3)
- Query contract library with typed static members (Story 18.4)
- SignalR real-time notifications (Story 18.5)
- ETag actor implementation (Story 18.1)
- Projection actor implementation (Story 18.3)
- NFR35-39 performance validation
- QueryValidationController changes (pre-flight validation is decoupled from routing)
- E2E integration tests (existing tests should pass as-is with optional EntityId)

### References

- [Source: prd.md line 808 — FR50: 3-tier query actor routing model with SHA256 checksum]
- [Source: prd.md lines 876-880 — NFR35-39: Query pipeline performance requirements]
- [Source: epics.md line 1331 — Epic 9 Story 9.2: 3-Tier Query Actor Routing]
- [Source: 18-1-etag-actor-and-projection-change-notification.md — PM-4: Colon separator convention, base64url encoding pattern]
- [Source: docs/concepts/identity-scheme.md — Structural disjointness principle for colon separators]
- [Source: src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs line 55 — ActorId property using colon separator]
- [Source: src/Hexalith.EventStore.Server/Queries/QueryRouter.cs — Current 1-tier routing implementation]
- [Source: src/Hexalith.EventStore.Server/Queries/IQueryRouter.cs — Router interface (unchanged)]
- [Source: src/Hexalith.EventStore.Server/Queries/QueryRouterResult.cs — Router result type (unchanged)]
- [Source: src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs — Current contract without EntityId]
- [Source: src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs — MediatR request without EntityId]
- [Source: src/Hexalith.EventStore.Server/Actors/QueryEnvelope.cs — DataContract envelope without EntityId]
- [Source: src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs — Controller forwarding]
- [Source: src/Hexalith.EventStore.CommandApi/Validation/SubmitQueryRequestValidator.cs — Request validator]
- [Source: tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs — Existing routing tests with hardcoded actor IDs]
- [Source: tests/Hexalith.EventStore.Server.Tests/Actors/QueryEnvelopeTests.cs — Envelope tests with CreateValid helper]
- [Source: tests/Hexalith.EventStore.Server.Tests/Pipeline/AuthorizationBehaviorTests.cs — Auth tests with CreateTestQuery helper]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

Review remediation and final verification: GitHub Copilot (GPT-5.4)

### Debug Log References

- Full solution build: 0 errors, 0 warnings (Release config)
- Tier 1 tests: 517 passed (Contracts 181, Client 246, Sample 29, Testing 61)
- Tier 2 tests: 1,173 passed (Server.Tests)
- Total: 1,690 tests, 0 failures
- Focused review-remediation validation: 126 passed, 0 failed (`SubmitQueryRequestValidatorTests`, `ValidateQueryRequestValidatorTests`, `QueryActorIdHelperTests`, `QueryEnvelopeTests`, `ValidatorConsistencyTests`, `QueriesControllerTests`, `QueryRouterTests`)

### Completion Notes List

- Added `EntityId` as optional nullable field to `SubmitQueryRequest`, `SubmitQuery`, and `QueryEnvelope` — all backward compatible
- Created `QueryActorIdHelper` static class with pure `DeriveActorId()` and `ComputeChecksum()` methods
- Refactored `QueryRouter.RouteQueryAsync()` to use 3-tier actor ID derivation instead of `AggregateIdentity.ActorId`
- Added structured log message (EventId 1205) for tier selection
- Added EntityId validation (optional, max 256 chars, AggregateId regex, no colons) to `SubmitQueryRequestValidator`
- Added QueryType colon prohibition to `SubmitQueryRequestValidator` (query-specific, not command validators)
- Updated XML doc on `IProjectionActor` for new 3-tier actor ID format
- Created 12 new unit tests for `QueryActorIdHelper` (checksum, tier selection, edge cases)
- Updated 9 existing test files for EntityId parameter and new actor ID format
- Added new tests: EntityId forwarding (controller), EntityId/QueryType colon validation, DataContractSerializer backward compat
- Review remediation aligned the preflight validator with submit-time query validation for colon-bearing `QueryType` values
- Review remediation tightened optional `EntityId` handling so empty/whitespace values are rejected instead of silently downgrading routing specificity
- Review remediation hardened `QueryActorIdHelper` against colon-bearing routing segments from non-HTTP callers
- Review remediation corrected the `QueryEnvelope` backward-compatibility test to deserialize XML with the `EntityId` element genuinely removed
- Review remediation updated story traceability to reflect the additional validator/test files touched during code review

### Change Log

- **2026-03-13:** Implemented 3-tier query actor routing (Story 18.2) — EntityId-scoped (Tier 1), payload-checksum (Tier 2), tenant-wide (Tier 3) actor ID derivation using colon separator. Added EntityId as optional field across full query pipeline. Created QueryActorIdHelper pure-function helper. Updated 9 test files, added 12 new QueryActorIdHelper tests + 9 new validator/controller tests.
- **2026-03-13:** Code review remediation applied — aligned preflight and submit query validation, rejected empty/whitespace `EntityId` values, added helper guards for colon-bearing routing segments, fixed the old-wire-format `QueryEnvelope` compatibility test, and revalidated the focused query-routing test suite.

### File List

**New files (2):**

- src/Hexalith.EventStore.Server/Queries/QueryActorIdHelper.cs
- tests/Hexalith.EventStore.Server.Tests/Queries/QueryActorIdHelperTests.cs

**Modified production files (8):**

- src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs
- src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs
- src/Hexalith.EventStore.Server/Actors/QueryEnvelope.cs
- src/Hexalith.EventStore.Server/Actors/IProjectionActor.cs
- src/Hexalith.EventStore.Server/Queries/QueryRouter.cs
- src/Hexalith.EventStore.CommandApi/Controllers/QueriesController.cs
- src/Hexalith.EventStore.CommandApi/Validation/SubmitQueryRequestValidator.cs
- src/Hexalith.EventStore.CommandApi/Validation/ValidateQueryRequestValidator.cs

**Modified test files (10):**

- tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/QueryEnvelopeTests.cs
- tests/Hexalith.EventStore.Server.Tests/Pipeline/AuthorizationBehaviorTests.cs
- tests/Hexalith.EventStore.Server.Tests/Pipeline/Queries/SubmitQueryTests.cs
- tests/Hexalith.EventStore.Server.Tests/Pipeline/Queries/SubmitQueryHandlerTests.cs
- tests/Hexalith.EventStore.Contracts.Tests/Queries/SubmitQueryRequestTests.cs
- tests/Hexalith.EventStore.Server.Tests/Validation/ValidatorConsistencyTests.cs
- tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs
- tests/Hexalith.EventStore.Server.Tests/Validation/SubmitQueryRequestValidatorTests.cs
- tests/Hexalith.EventStore.Server.Tests/Validation/ValidateQueryRequestValidatorTests.cs

## Senior Developer Review (AI)

### Reviewer

GitHub Copilot (GPT-5.4)

### Date

2026-03-13

### Outcome

Approved

### Findings Resolution Summary

- High: submit-time validation accepted empty `EntityId` values and silently widened cache scope — fixed by rejecting empty/whitespace `EntityId` values in `SubmitQueryRequestValidator`.
- High: preflight query validation allowed colon-bearing `QueryType` values while submit-time validation rejected them — fixed by aligning `ValidateQueryRequestValidator` with the submit validator.
- High: `QueryActorIdHelper` trusted external invariants for colon-free routing segments — fixed by validating `queryType`, `tenantId`, and non-empty `entityId` segments before actor ID composition.
- Medium: the backward-compatibility test for `QueryEnvelope` did not actually remove `EntityId` from the serialized payload — fixed by removing the element from the XML before deserialization.
- Medium: story traceability drifted during review remediation — fixed by updating the Dev Agent Record, Change Log, and File List to include the validator-alignment files and focused revalidation results.
- Note: the working branch also contains concurrent Story 18.1 and sprint-tracking changes outside Story 18.2 scope; this story's File List remains scoped to Story 18.2 implementation and remediation changes.
