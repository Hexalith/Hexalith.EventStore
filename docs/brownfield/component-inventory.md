# Component Inventory — Hexalith.EventStore

> UI components, CLI commands, MCP tools, and test/utility components discovered in the scan.
> For library types (aggregates, actors, contracts) see [architecture.md](./architecture.md) and
> [data-models.md](./data-models.md).

## Admin Blazor UI (`src/Hexalith.EventStore.Admin.UI`) — FluentUI

### Pages (22) — grouped

- **Streams & events:** `Streams`, `Events`, `StreamDetail` (timeline, state-at-sequence, diff, causation, correlation)
- **Projections & ops:** `Projections` (status badges, pause/resume/reset), `Snapshots`, `Compaction`, `DeadLetters`
- **Backup & restore:** `Backups` (trigger, restore, export/import)
- **Diagnostics:** `Consistency` (anomalies + severity), `Commands`, `Health`
- **DAPR infra:** `DaprActors`, `DaprComponents`, `DaprPubSub`, `DaprResiliency`, `DaprHealthHistory`, `Services`
- **Admin:** `Tenants` (CRUD + roles), `TypeCatalog`, `Index`, `Settings`

### Reusable components (15)

`StreamTimelineGrid`, `EventDetailPanel`, `StateDiffViewer`, `BlameViewer`, `BisectTool`,
`CorrelationTraceMap`, `ProjectionStatusBadge`, `TimelineFilterBar`, `StreamFilterBar`, `ActivityChart`,
`SkeletonCard`, `StatCard`, `EmptyState`, `IssueBanner`.

### API clients & services

Typed API clients per controller (`AdminStreamApiClient`, `AdminProjectionApiClient`,
`AdminTenantApiClient`, `AdminBackupApiClient`, `AdminConsistencyApiClient`, `AdminStorageApiClient`,
`AdminCompactionApiClient`, `AdminSnapshotApiClient`, `AdminDeadLetterApiClient`, `AdminDaprApiClient`,
`AdminActorApiClient`, `AdminPubSubApiClient`, `AdminResiliencyApiClient`, `AdminHealthHistoryApiClient`,
`AdminTypeCatalogApiClient`). Auth/services: `TokenAuthenticationStateProvider`,
`AdminApiAuthorizationHandler`, `AdminApiAccessTokenProvider`, `AdminTenantOptionsProvider`
(role-filtered tenant dropdown), `DashboardRefreshService`, `TopologyCacheService`, `ViewportService`,
`AdminUserContext`, `AdminClaimTypes`. Container: `eventstore-admin-ui`.

## Sample Blazor UI (`samples/Hexalith.EventStore.Sample.BlazorUI`)

- Components: `CounterCommandForm`, `CounterValueCard`, `CounterHistoryGrid`.
- Pages demonstrating SignalR refresh patterns: `NotificationPattern`, `SilentReloadPattern`,
  `SelectiveRefreshPattern`, plus `Index`.
- Services: `CounterQueryService` (DAPR queries), `SignalRClientStartup`, `EventStoreApiAccessTokenProvider`.
  Container: `sample-blazor-ui`. See `docs/guides/sample-blazor-ui.md`.

## Admin CLI (`src/Hexalith.EventStore.Admin.Cli`) — `eventstore-admin`

Global options: `--url`, `--token`, `--format` (json/csv/table), `--output`, `--profile`.
Command groups (System.CommandLine):

| Group | Subcommands |
|-------|-------------|
| `health` | `status`, `dapr` |
| `stream` | `list`, `events`, `state`, `diff`, `event`, `causation` |
| `projection` | `list`, `status`, `pause`, `resume`, `reset` |
| `tenant` | `list`, `detail`, `users` |
| `snapshot` | `policies`, `create`, `set-policy`, `delete-policy` |
| `backup` | `list`, `trigger`, `validate`, `restore`, `export-stream`, `import-stream` |
| `config` | `list`, `current`, `use`, `add`, `remove`, `completion` |

Output: `IOutputFormatter` (`JsonOutputFormatter`, `TableOutputFormatter`, `SafeOutputValueFormatter`
with credential redaction). Profiles persisted to `.eventstore-admin-profiles.json`. Exit codes:
0 success, 1 error, 2 authz failure. Distributed as a **NuGet tool**.

## Admin MCP (`src/Hexalith.EventStore.Admin.Mcp`) — AI-callable tools

Stdio JSON-RPC 2.0. Env: `EVENTSTORE_ADMIN_URL`, `EVENTSTORE_ADMIN_TOKEN`. Tools auto-discovered via
`[McpServerTool]`:

- **Read-only:** `stream-list`, `stream-events`, `stream-state`, `stream-event-detail`, `stream-diff`,
  `causation-chain`, `projection-list`, `projection-detail`, `health-status`, `health-dapr`, `ping`,
  `consistency-list`, `consistency-detail`, `storage-overview`, `tenant-list`, `tenant-detail`,
  `tenant-users`, `types-list`.
- **Write:** `projection-pause/resume/reset/replay`, `consistency-trigger/cancel`, `backup-trigger`,
  `backup-export-stream`, `backup-import-stream`.
- **Session context:** `session-set-context`, `session-get-context`, `session-clear-context`
  (`InvestigationSession` singleton keeps agent investigation scope across calls).

## Domain-service host surface (`src/Hexalith.EventStore.DomainService`)

The **domain-service SDK** a domain module hosts itself with. It builds on the client libraries —
`AddEventStore()` (convention discovery + keyed `IDomainProcessor` registration via `AssemblyScanner` /
`NamingConventionEngine`), `UseEventStore()` (5-layer per-domain config cascade), and `AddServiceDefaults()`
/ `MapDefaultEndpoints()` from `Hexalith.EventStore.ServiceDefaults` — and wraps them in
`AddEventStoreDomainService()` / `UseEventStoreDomainService()` / `MapEventStoreDomainService()`, which
reduce the host to ~2 lines and map `/`, `/process`, `/replay-state`, `/admin/operational-index-metadata`.
A domain module references **only** this SDK (Client/ServiceDefaults/Contracts flow transitively) and maps
its own `/project` until that is generalized (Epic A3). The SDK is not yet NuGet-published (it depends on
the unpackaged ServiceDefaults; see the Epic A6 packaging decision in
`_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-02.md`).

## Testing components (`src/Hexalith.EventStore.Testing`)

- **Builders:** `AggregateIdentityBuilder`, `CommandEnvelopeBuilder`, `EventEnvelopeBuilder`,
  `StreamReadPageBuilder`, `EventStoreGatewayExceptionBuilder`, `CryptoShreddingWorkflowBuilder`,
  `RestoredBackupAdmissionBuilder`.
- **Fakes:** `FakeEventStoreGatewayClient`, `FakeEventPublisher`, `FakeEventPersister`,
  `FakeEventStreamReader`, `FakeSnapshotManager`, `FakeDomainServiceInvoker`, `FakeCommandRouter`,
  `FakeAggregateActor`, `FakeProjectionActor`, `FakeProjectionWriteActor`, `FakeRbacValidatorActor`,
  `FakeTenantValidatorActor`, `FakeETagActor`, `FakeActorStateMachine`, `FakeDeadLetterPublisher`,
  `FakeUnreadableProtectionService`.
- **In-memory stores:** `InMemoryStateManager` (DAPR turn-based semantics), `InMemoryCommandStatusStore`,
  `InMemoryCommandArchiveStore`.
- **Assertions:** `EventSequenceAssertions.ShouldHaveSequentialNumbers()`,
  `StorageKeyIsolationAssertions` (`AssertKeyBelongsToTenant`, `AssertKeysDisjoint`, `AssertEventStreamKey`),
  `EventEnvelopeAssertions`, `DomainResultAssertions`.
- **Compliance:** `TerminatableComplianceAssertions.AssertTerminatableCompliance<TState>()`.
- **HTTP mocks:** `MockHttpMessageHandler`, `QueuedMockHttpMessageHandler`.
- **Constants:** `TestDataConstants` (`TenantId="test-tenant"`, `Domain="counter"`, etc.).
