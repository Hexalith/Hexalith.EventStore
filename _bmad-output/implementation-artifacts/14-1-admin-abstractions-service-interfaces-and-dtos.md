# Story 14.1: Admin Abstractions — Service Interfaces and DTOs

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer building admin tooling (Web UI, CLI, or MCP),
I want a shared `Hexalith.EventStore.Admin.Abstractions` NuGet package with service interfaces and DTOs,
So that all three admin interfaces consume a single contract, ensuring behavioral consistency and independent evolvability.

## Acceptance Criteria

1. **Given** the solution, **When** built, **Then** a new project `src/Hexalith.EventStore.Admin.Abstractions/` exists, compiles with zero errors/warnings, and is included in `Hexalith.EventStore.slnx`.
2. **Given** the Admin.Abstractions package, **When** inspected, **Then** it defines service interfaces covering all v1 admin domains: streams, projections, event types, health, storage, dead-letters, and tenant delegation. (Note: domain service version management and backup/restore interfaces are deferred to Epic 16 stories where they are specified.)
3. **Given** each service interface, **When** reviewed, **Then** every method returns a DTO or shared enum defined in the same package — no raw primitives, no `JsonElement`, no state-store-specific types leak through the abstraction. Opaque state/payload data uses `string` (JSON), never `JsonElement`.
4. **Given** the DTOs, **When** reviewed, **Then** they are immutable `record` types with inline validation matching the Contracts project pattern (e.g., `ArgumentException` on null/empty required fields).
5. **Given** the DTOs, **When** serialized, **Then** `ToString()` on any DTO containing payload or sensitive data returns `[REDACTED]` per SEC-5.
6. **Given** the project, **When** packaged, **Then** `.csproj` includes `<Description>`, `<GenerateDocumentationFile>true</GenerateDocumentationFile>`, targets `net10.0`, and uses centralized package versions from `Directory.Packages.props`.
7. **Given** Admin.Abstractions, **When** referenced, **Then** it depends only on `Hexalith.EventStore.Contracts` (for identity types, `AggregateIdentity`, `EventEnvelope`, `CommandStatus`) — no DAPR, no ASP.NET Core, no MediatR.
8. **Given** the package, **When** a new Tier 1 test project `tests/Hexalith.EventStore.Admin.Abstractions.Tests/` exists, **Then** it validates DTO construction, validation, serialization round-trip, and `ToString()` redaction.

## Tasks / Subtasks

- [x] Task 0: Prerequisites (AC: all)
  - [x] 0.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` — confirm baseline compiles (0 errors, 0 warnings)
  - [x] 0.2 Read existing patterns: `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj`, `src/Hexalith.EventStore.Contracts/Commands/CommandEnvelope.cs`, `src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs`, `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs` to confirm record/validation patterns
  - [x] 0.3 Read `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig` for build/style conventions
  - [x] 0.4 Read `Hexalith.EventStore.slnx` to understand solution structure for adding new projects

- [x] Task 1: Create Admin.Abstractions project (AC: #1, #6, #7)
  - [x] 1.1 Create `src/Hexalith.EventStore.Admin.Abstractions/Hexalith.EventStore.Admin.Abstractions.csproj` with:
    - `<TargetFramework>net10.0</TargetFramework>`
    - `<Description>Admin service interfaces and DTOs for Hexalith.EventStore — shared contract for Web UI, CLI, and MCP</Description>`
    - `<GenerateDocumentationFile>true</GenerateDocumentationFile>`
    - `<ProjectReference Include="../Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj" />` (sibling project reference — NOT `PackageReference`, which is for published NuGet consumption)
    - No other external dependencies — this is a pure contract package
  - [x] 1.2 Add the project to `Hexalith.EventStore.slnx`
  - [x] 1.3 Verify `dotnet build Hexalith.EventStore.slnx --configuration Release` succeeds

- [x] Task 2: Define stream-related service interface and DTOs (AC: #2, #3, #4, #5)
  - [x] 2.1 Create `Services/IStreamQueryService.cs` — interface for stream browsing and inspection:
    ```
    namespace Hexalith.EventStore.Admin.Abstractions.Services;
    ```
    Methods:
    - `GetRecentlyActiveStreamsAsync(string? tenantId, string? domain, int count = 1000, CancellationToken ct)` → `PagedResult<StreamSummary>` (FR68) — `domain` filter enables scoped investigation (e.g., "show me only inventory streams")
    - `GetStreamTimelineAsync(string tenantId, string domain, string aggregateId, int? fromSequence, int? toSequence, int count = 100, CancellationToken ct)` → `PagedResult<TimelineEntry>` (FR69) — paginated to prevent unbounded results on large aggregates; `count` defaults to 100, use `ContinuationToken` for next page
    - `GetAggregateStateAtPositionAsync(string tenantId, string domain, string aggregateId, long sequenceNumber, CancellationToken ct)` → `AggregateStateSnapshot` (FR70)
    - `DiffAggregateStateAsync(string tenantId, string domain, string aggregateId, long fromSequence, long toSequence, CancellationToken ct)` → `AggregateStateDiff` (FR71)
    - `GetEventDetailAsync(string tenantId, string domain, string aggregateId, long sequenceNumber, CancellationToken ct)` → `EventDetail` — single-event inspection for MCP diagnosis workflows (Journey 9)
    - `TraceCausationChainAsync(string tenantId, string domain, string aggregateId, long sequenceNumber, CancellationToken ct)` → `CausationChain` (FR72)
  - [x] 2.2 Create DTOs in `Models/Streams/`:
    - `StreamSummary.cs` — record: TenantId, Domain, AggregateId, LastEventSequence, LastActivityUtc, EventCount, HasSnapshot, StreamStatus
    - `TimelineEntryType.cs` — enum: Command, Event, Query
    - `TimelineEntry.cs` — record: SequenceNumber, Timestamp, EntryType (`TimelineEntryType`), TypeName, CorrelationId, UserId. Override `ToString()` to redact any payload data
    - `AggregateStateSnapshot.cs` — record: AggregateIdentity info, SequenceNumber, Timestamp, StateJson (`string` — opaque JSON, NOT `JsonElement`). `ToString()` redacts StateJson
    - `AggregateStateDiff.cs` — record: FromSequence, ToSequence, ChangedFields (list of `FieldChange`)
    - `FieldChange.cs` — record: FieldPath (`string`), OldValue (`string` — opaque JSON scalar), NewValue (`string` — opaque JSON scalar). `ToString()` redacts OldValue and NewValue
    - `CausationChain.cs` — record: OriginatingCommandType (`string`), OriginatingCommandId (`string`), CorrelationId (`string`), UserId (`string?`), Events (`IReadOnlyList<CausationEvent>`), AffectedProjections (`IReadOnlyList<string>` — projection names)
    - `CausationEvent.cs` — record: SequenceNumber (`long`), EventTypeName (`string`), Timestamp (`DateTimeOffset`)
    - `EventDetail.cs` — record: TenantId, Domain, AggregateId, SequenceNumber (`long`), EventTypeName (`string`), Timestamp (`DateTimeOffset`), CorrelationId (`string`), CausationId (`string?`), UserId (`string?`), PayloadJson (`string` — opaque JSON). `ToString()` redacts PayloadJson
    - `StreamStatus.cs` — enum: Active, Idle, Tombstoned

  - [x] 2.3 Create `Models/Common/PagedResult.cs` — generic `record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, string? ContinuationToken)`

- [x] Task 3: Define projection service interface and DTOs (AC: #2, #3, #4)
  - [x] 3.1 Create `Services/IProjectionQueryService.cs`:
    - `ListProjectionsAsync(string? tenantId, CancellationToken ct)` → `IReadOnlyList<ProjectionStatus>` (FR73)
    - `GetProjectionDetailAsync(string tenantId, string projectionName, CancellationToken ct)` → `ProjectionDetail`
  - [x] 3.2 Create `Services/IProjectionCommandService.cs` (operator-level write operations):
    - `PauseProjectionAsync(string tenantId, string projectionName, CancellationToken ct)` → `AdminOperationResult` (FR73)
    - `ResumeProjectionAsync(string tenantId, string projectionName, CancellationToken ct)` → `AdminOperationResult`
    - `ResetProjectionAsync(string tenantId, string projectionName, long? fromPosition, CancellationToken ct)` → `AdminOperationResult`
    - `ReplayProjectionAsync(string tenantId, string projectionName, long fromPosition, long toPosition, CancellationToken ct)` → `AdminOperationResult`
  - [x] 3.3 Create DTOs in `Models/Projections/`:
    - `ProjectionStatusType.cs` — enum: Running, Paused, Error, Rebuilding
    - `ProjectionStatus.cs` — record: Name, TenantId, Status (`ProjectionStatusType`), Lag (`long`), Throughput (`double` — events/sec), ErrorCount (`int`), LastProcessedPosition (`long`), LastProcessedUtc (`DateTimeOffset`)
    - `ProjectionError.cs` — record: Position (`long`), Timestamp (`DateTimeOffset`), Message (`string`), EventTypeName (`string?`)
    - `ProjectionDetail.cs` — record inheriting from `ProjectionStatus` (C# record inheritance): adds Errors (`IReadOnlyList<ProjectionError>` — structured, not flat strings), Configuration (`string` — opaque JSON), SubscribedEventTypes (`IReadOnlyList<string>`)

- [x] Task 4: Define event/command type catalog service and DTOs (AC: #2, #3)
  - [x] 4.1 Create `Services/ITypeCatalogService.cs`. Add XML doc: `/// <summary>Type catalog is tenant-agnostic — event/command/aggregate types are registered globally via reflection-based assembly scanning, not per-tenant. No tenantId parameter required.</summary>`:
    - `ListEventTypesAsync(string? domain, CancellationToken ct)` → `IReadOnlyList<EventTypeInfo>` (FR74)
    - `ListCommandTypesAsync(string? domain, CancellationToken ct)` → `IReadOnlyList<CommandTypeInfo>` (FR74)
    - `ListAggregateTypesAsync(string? domain, CancellationToken ct)` → `IReadOnlyList<AggregateTypeInfo>` (FR74)
  - [x] 4.2 Create DTOs in `Models/TypeCatalog/`:
    - `EventTypeInfo.cs` — record: TypeName, Domain, IsRejection, SchemaVersion
    - `CommandTypeInfo.cs` — record: TypeName, Domain, TargetAggregateType
    - `AggregateTypeInfo.cs` — record: TypeName, Domain, EventCount, CommandCount, HasProjections

- [x] Task 5: Define health and observability service and DTOs (AC: #2, #3)
  - [x] 5.1 Create `Services/IHealthQueryService.cs`:
    - `GetSystemHealthAsync(CancellationToken ct)` → `SystemHealthReport` (FR75)
    - `GetDaprComponentStatusAsync(CancellationToken ct)` → `IReadOnlyList<DaprComponentHealth>`
  - [x] 5.2 Create DTOs in `Models/Health/`:
    - `HealthStatus.cs` — enum: Healthy, Degraded, Unhealthy
    - `SystemHealthReport.cs` — record: OverallStatus (`HealthStatus`), TotalEventCount (`long`), EventsPerSecond (`double`), ErrorPercentage (`double`), DaprComponents (`IReadOnlyList<DaprComponentHealth>`), ObservabilityLinks (`ObservabilityLinks`)
    - `DaprComponentHealth.cs` — record: ComponentName (`string`), ComponentType (`string`), Status (`HealthStatus`), LastCheckUtc (`DateTimeOffset`)
    - `ObservabilityLinks.cs` — record: TraceUrl?, MetricsUrl?, LogsUrl? (ADR-P5 deep-link URLs, nullable when not configured)

- [x] Task 6: Define storage and dead-letter services and DTOs (AC: #2, #3)
  - [x] 6.1 Create `Services/IStorageQueryService.cs`:
    - `GetStorageOverviewAsync(string? tenantId, CancellationToken ct)` → `StorageOverview` (FR76)
    - `GetHotStreamsAsync(string? tenantId, int count, CancellationToken ct)` → `IReadOnlyList<StreamStorageInfo>`
    - `GetSnapshotPoliciesAsync(string? tenantId, CancellationToken ct)` → `IReadOnlyList<SnapshotPolicy>` — read counterpart to `SetSnapshotPolicyAsync`
  - [x] 6.2 Create `Services/IStorageCommandService.cs` (operator-level):
    - `TriggerCompactionAsync(string tenantId, string? domain, CancellationToken ct)` → `AdminOperationResult`
    - `CreateSnapshotAsync(string tenantId, string domain, string aggregateId, CancellationToken ct)` → `AdminOperationResult`
    - `SetSnapshotPolicyAsync(string tenantId, string domain, string aggregateType, int intervalEvents, CancellationToken ct)` → `AdminOperationResult`
  - [x] 6.3 Create `Services/IDeadLetterQueryService.cs` (CQRS-split — reads only):
    - `ListDeadLettersAsync(string? tenantId, int count, string? continuationToken, CancellationToken ct)` → `PagedResult<DeadLetterEntry>` (FR78)
  - [x] 6.4a Create `Services/IDeadLetterCommandService.cs` (CQRS-split — writes only, operator-level):
    - `RetryDeadLettersAsync(string tenantId, IReadOnlyList<string> messageIds, CancellationToken ct)` → `AdminOperationResult`
    - `SkipDeadLettersAsync(string tenantId, IReadOnlyList<string> messageIds, CancellationToken ct)` → `AdminOperationResult`
    - `ArchiveDeadLettersAsync(string tenantId, IReadOnlyList<string> messageIds, CancellationToken ct)` → `AdminOperationResult`
  - [x] 6.4 Create DTOs in `Models/Storage/`:
    - `StorageOverview.cs` — record: TotalEventCount (`long`), TotalSizeBytes (`long?` — nullable, state store backend may not support size queries per NFR44), TenantBreakdown (`IReadOnlyList<TenantStorageInfo>`)
    - `TenantStorageInfo.cs` — record: TenantId, EventCount (`long`), SizeBytes (`long?` — nullable per NFR44), GrowthRatePerDay (`double?` — nullable, requires historical tracking which may not be available)
    - `StreamStorageInfo.cs` — record: TenantId, Domain, AggregateId, AggregateType (`string` — enables grouping by type for treemap views per Journey 8), EventCount, SizeBytes (`long?` — nullable, backend-dependent per NFR44), HasSnapshot, SnapshotAge
    - `SnapshotPolicy.cs` — record: TenantId (`string`), Domain (`string`), AggregateType (`string`), IntervalEvents (`int`), CreatedAtUtc (`DateTimeOffset`)
  - [x] 6.5 Create DTOs in `Models/DeadLetters/`:
    - `DeadLetterEntry.cs` — record: MessageId, TenantId, Domain, AggregateId, CorrelationId, FailureReason, FailedAtUtc, RetryCount, OriginalCommandType. `ToString()` redacts payload content

- [x] Task 7: Define tenant delegation service interface (AC: #2, #3)
  - [x] 7.1 Create `Services/ITenantQueryService.cs` — thin delegation interface for Hexalith.Tenants peer API (FR77). Add XML doc: `/// <summary>Tenant queries delegated to Hexalith.Tenants Client SDK at implementation time (Admin.Server). EventStore does NOT own tenant state.</summary>`:
    - `ListTenantsAsync(CancellationToken ct)` → `IReadOnlyList<TenantSummary>`
    - `GetTenantQuotasAsync(string tenantId, CancellationToken ct)` → `TenantQuotas`
    - `CompareTenantUsageAsync(IReadOnlyList<string> tenantIds, CancellationToken ct)` → `TenantComparison`
  - [x] 7.2 Create DTOs in `Models/Tenants/`:
    - `TenantStatusType.cs` — enum: Active, Suspended, Onboarding
    - `TenantSummary.cs` — record: TenantId, DisplayName, Status (`TenantStatusType`), EventCount, DomainCount
    - `TenantQuotas.cs` — record: TenantId, MaxEventsPerDay, MaxStorageBytes, CurrentUsage
    - `TenantComparison.cs` — record: Tenants (list of TenantSummary), ComparedAtUtc

- [x] Task 8: Define shared result types (AC: #3, #4)
  - [x] 8.1 Create `Models/Common/AdminOperationResult.cs` — record: Success (bool), OperationId (string), Message (string?), ErrorCode (string?)
  - [x] 8.2 Create `Models/Common/AdminRole.cs` — enum: ReadOnly, Operator, Admin (NFR46)

- [x] Task 9: Create test project (AC: #8)
  - [x] 9.1 Create `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Hexalith.EventStore.Admin.Abstractions.Tests.csproj` following existing test project patterns (xUnit, Shouldly, coverlet)
  - [x] 9.2 Add test project to `Hexalith.EventStore.slnx`
  - [x] 9.3 Write DTO construction tests: one `{TypeName}Tests.cs` per DTO with required field validation throws `ArgumentException` on null/empty. Follow naming convention from existing `Contracts.Tests` project
  - [x] 9.4 Write `ToString()` redaction tests: verify sensitive DTOs (`AggregateStateSnapshot`, `FieldChange`, `TimelineEntry`, `DeadLetterEntry`) redact payload data
  - [x] 9.5 Write serialization round-trip tests: verify JSON serialize/deserialize produces equal records for representative DTOs from each feature folder
  - [x] 9.6 Write `PagedResult<T>` tests: construction, empty list, null handling
  - [x] 9.7 Write enum tests: verify `StreamStatus`, `TimelineEntryType`, `ProjectionStatusType`, `HealthStatus`, `TenantStatusType`, `AdminRole` enum values match expected members

- [x] Task 10: Build and test (AC: all)
  - [x] 10.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 errors, 0 warnings
  - [x] 10.2 `dotnet test tests/Hexalith.EventStore.Admin.Abstractions.Tests/` — all pass
  - [x] 10.3 Run all existing Tier 1 tests — 0 regressions
  - [x] 10.4 Verify the package can be referenced from a blank console app with `<PackageReference Include="Hexalith.EventStore.Admin.Abstractions" />`

## Dev Notes

### CRITICAL: This Package Is a Pure Contract — Zero Implementation

This is the **shared contract layer** consumed by three independent interfaces (ADR-P4):
- **Admin.Server** (in-process Blazor + REST API) — Story 14-2 implements these interfaces
- **Admin.Cli** (HTTP client) — Epic 17
- **Admin.Mcp** (HTTP client) — Epic 18

The package must have **zero runtime dependencies** beyond Contracts. No DAPR, no ASP.NET Core, no MediatR. Implementations go in `Admin.Server` (Story 14-2).

### Architectural Constraints (ADR-P4)

- **Read-only state store access**: Admin reads go through DAPR state store using `AggregateIdentity` key derivation from Contracts
- **Write delegation**: All writes go through CommandApi via DAPR service invocation — never bypass the command pipeline
- **Tenant operations**: Delegated to `Hexalith.Tenants` peer service via its Client SDK — EventStore does NOT own tenant state (FR77)
- **State store key reuse**: Key derivation identical to `AggregateIdentity.EventStreamKeyPrefix`, `MetadataKey`, `SnapshotKey` etc.

### Authentication/Authorization Model (NFR46)

Three roles defined in `AdminRole` enum:
- **ReadOnly** (developer): stream browsing, state inspection, type catalog, health dashboard
- **Operator** (DBA): everything in ReadOnly + projection controls (pause/resume/reset), snapshot creation, compaction, dead-letter management
- **Admin** (infrastructure): everything in Operator + tenant management, backup/restore

Authorization enforcement happens in Admin.Server controllers (Story 14-3), not in abstractions. But the role model is defined here so all consumers share it.

### Naming and Organization Conventions

Follow existing patterns exactly:
- **Namespace**: `Hexalith.EventStore.Admin.Abstractions.Services` for interfaces, `Hexalith.EventStore.Admin.Abstractions.Models.{Feature}` for DTOs
- **File-scoped namespaces**: `namespace X.Y.Z;` (not block-scoped)
- **One public type per file**: File name = type name
- **Records**: Immutable with inline validation for required fields
- **Interfaces**: `I` prefix, XML documentation on every method
- **Async methods**: `Async` suffix, all return `Task<T>`, all accept `CancellationToken`
- **Braces**: Allman style (opening brace on new line)
- **Private fields**: `_camelCase` (unlikely in records, but applies to any classes)
- **`ToString()` redaction**: Any DTO with payload, state, or sensitive values must override `ToString()` with `[REDACTED]` per SEC-5

### Performance Budget (NFR40)

These are contract-level notes — implementations must respect:
- Read operations: < 500ms at p99
- Write operations: < 2s at p99
Interfaces should be designed for pagination and streaming to support these budgets.

### Dependency Chain

```
Admin.Abstractions → Contracts (only)
Admin.Server → Admin.Abstractions + DAPR + ASP.NET Core (Story 14-2)
Admin.Cli → Admin.Abstractions + System.CommandLine (Epic 17)
Admin.Mcp → Admin.Abstractions (Epic 18)
```

### DO NOT

- Do NOT add DAPR references — this is a pure contract package
- Do NOT add ASP.NET Core references — controllers are in Admin.Server
- Do NOT implement any interface — implementations are in Story 14-2
- Do NOT define REST endpoint attributes — those go in Admin.Server controllers (Story 14-3)
- Do NOT create `ServiceCollectionExtensions.cs` — DI registration is in Admin.Server
- Do NOT reference `Hexalith.EventStore.Server` or `Hexalith.EventStore.Client` — this package sits below them in the dependency chain

### Project Structure Notes

```
src/Hexalith.EventStore.Admin.Abstractions/
  Hexalith.EventStore.Admin.Abstractions.csproj
  Services/
    IStreamQueryService.cs
    IProjectionQueryService.cs
    IProjectionCommandService.cs
    ITypeCatalogService.cs
    IHealthQueryService.cs
    IStorageQueryService.cs
    IStorageCommandService.cs
    IDeadLetterQueryService.cs
    IDeadLetterCommandService.cs
    ITenantQueryService.cs
  Models/
    Common/
      PagedResult.cs
      AdminOperationResult.cs
      AdminRole.cs
    Streams/
      StreamSummary.cs
      TimelineEntryType.cs
      TimelineEntry.cs
      EventDetail.cs
      AggregateStateSnapshot.cs
      AggregateStateDiff.cs
      FieldChange.cs
      CausationChain.cs
      CausationEvent.cs
      StreamStatus.cs
    Projections/
      ProjectionStatusType.cs
      ProjectionStatus.cs
      ProjectionError.cs
      ProjectionDetail.cs
    TypeCatalog/
      EventTypeInfo.cs
      CommandTypeInfo.cs
      AggregateTypeInfo.cs
    Health/
      HealthStatus.cs
      SystemHealthReport.cs
      DaprComponentHealth.cs
      ObservabilityLinks.cs
    Storage/
      StorageOverview.cs
      TenantStorageInfo.cs
      StreamStorageInfo.cs
      SnapshotPolicy.cs
    DeadLetters/
      DeadLetterEntry.cs
    Tenants/
      TenantStatusType.cs
      TenantSummary.cs
      TenantQuotas.cs
      TenantComparison.cs

tests/Hexalith.EventStore.Admin.Abstractions.Tests/
  Hexalith.EventStore.Admin.Abstractions.Tests.csproj
  # One {TypeName}Tests.cs per DTO (Task 9.3). Representative examples below:
  Models/
    Streams/
      StreamSummaryTests.cs
      AggregateStateSnapshotTests.cs
      FieldChangeTests.cs
      CausationChainTests.cs
    Common/
      PagedResultTests.cs
      AdminOperationResultTests.cs
    Projections/
      ProjectionStatusTests.cs
      ProjectionDetailTests.cs
    DeadLetters/
      DeadLetterEntryTests.cs
    Tenants/
      TenantSummaryTests.cs
```

### References

- [Source: _bmad-output/planning-artifacts/architecture.md — ADR-P4: Admin Tooling Three-Interface Architecture]
- [Source: _bmad-output/planning-artifacts/architecture.md — ADR-P5: Observability Deep-Link Strategy]
- [Source: _bmad-output/planning-artifacts/architecture.md — NuGet Package Architecture: Admin.Abstractions]
- [Source: _bmad-output/planning-artifacts/architecture.md — Cross-Cutting: Admin Data Access, Admin Authentication]
- [Source: _bmad-output/planning-artifacts/prd.md — FR68-FR79, FR82, NFR40-NFR46]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR41-DR59]
- [Source: src/Hexalith.EventStore.Contracts/ — Record/validation/redaction patterns]
- [Source: src/Hexalith.EventStore.Server/ — Service interface organization patterns]

## Dev Agent Record

### Agent Model Used
Claude Opus 4.6 (1M context)

### Debug Log References
- Baseline build has pre-existing CS0433 error in IntegrationTests (Tier 3, unrelated)
- All Tier 1 tests pass: Contracts (271), Client (297), Testing (67), SignalR (27), Sample (62)
- Admin.Abstractions.Tests: 107 tests pass

### Completion Notes List
- Created `Hexalith.EventStore.Admin.Abstractions` project as a pure contract package with only a `ProjectReference` to Contracts — zero external dependencies
- Defined 10 service interfaces covering all v1 admin domains: streams, projections, type catalog, health, storage, dead-letters, tenants (CQRS-split for writes)
- Created 30+ immutable record DTOs and 6 enums organized by feature folder
- All DTOs with payload/sensitive data override `ToString()` with `[REDACTED]` per SEC-5
- All required fields validated at construction time with `ArgumentException`/`ArgumentNullException`
- `PagedResult<T>` generic pagination, `AdminOperationResult` shared result, `AdminRole` enum defined
- `ProjectionDetail` uses C# record inheritance from `ProjectionStatus`
- Created comprehensive test project with 107 tests: construction validation, ToString redaction, serialization round-trip, enum member verification
- Build: 0 errors, 0 warnings; all Tier 1 tests pass with 0 regressions

### Change Log
- 2026-03-21: Story 14-1 implemented — Admin.Abstractions project with all service interfaces, DTOs, and test project

### File List
#### New Files
- src/Hexalith.EventStore.Admin.Abstractions/Hexalith.EventStore.Admin.Abstractions.csproj
- src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs
- src/Hexalith.EventStore.Admin.Abstractions/Services/IProjectionQueryService.cs
- src/Hexalith.EventStore.Admin.Abstractions/Services/IProjectionCommandService.cs
- src/Hexalith.EventStore.Admin.Abstractions/Services/ITypeCatalogService.cs
- src/Hexalith.EventStore.Admin.Abstractions/Services/IHealthQueryService.cs
- src/Hexalith.EventStore.Admin.Abstractions/Services/IStorageQueryService.cs
- src/Hexalith.EventStore.Admin.Abstractions/Services/IStorageCommandService.cs
- src/Hexalith.EventStore.Admin.Abstractions/Services/IDeadLetterQueryService.cs
- src/Hexalith.EventStore.Admin.Abstractions/Services/IDeadLetterCommandService.cs
- src/Hexalith.EventStore.Admin.Abstractions/Services/ITenantQueryService.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Common/PagedResult.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Common/AdminOperationResult.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Common/AdminRole.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/StreamStatus.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/TimelineEntryType.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/StreamSummary.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/TimelineEntry.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/AggregateStateSnapshot.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/AggregateStateDiff.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/FieldChange.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/CausationChain.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/CausationEvent.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/EventDetail.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Projections/ProjectionStatusType.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Projections/ProjectionStatus.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Projections/ProjectionError.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Projections/ProjectionDetail.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/TypeCatalog/EventTypeInfo.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/TypeCatalog/CommandTypeInfo.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/TypeCatalog/AggregateTypeInfo.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Health/HealthStatus.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Health/SystemHealthReport.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Health/DaprComponentHealth.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Health/ObservabilityLinks.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/StorageOverview.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/TenantStorageInfo.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/StreamStorageInfo.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/SnapshotPolicy.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/DeadLetters/DeadLetterEntry.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/TenantStatusType.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/TenantSummary.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/TenantQuotas.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/TenantComparison.cs
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Hexalith.EventStore.Admin.Abstractions.Tests.csproj
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/StreamSummaryTests.cs
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/AggregateStateSnapshotTests.cs
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/FieldChangeTests.cs
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/CausationChainTests.cs
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/TimelineEntryTests.cs
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/EventDetailTests.cs
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Common/PagedResultTests.cs
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Common/AdminOperationResultTests.cs
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Common/EnumTests.cs
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Common/SerializationRoundTripTests.cs
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Projections/ProjectionStatusTests.cs
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Projections/ProjectionDetailTests.cs
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/DeadLetters/DeadLetterEntryTests.cs
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Tenants/TenantSummaryTests.cs

#### Modified Files
- Hexalith.EventStore.slnx (added Admin.Abstractions project and test project)
