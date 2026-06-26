# Source Tree Analysis — Hexalith.EventStore

> Annotated layout of the monorepo. File counts are `.cs` files per `src/` project (deep-scan snapshot).

## Top-Level Layout

```
eventstore/
├── Hexalith.EventStore.slnx        # THE solution file (XML format; never use .sln)
├── global.json                     # Pins SDK 10.0.300 (rollForward latestPatch)
├── Directory.Build.props           # Shared build settings: net10.0, Nullable, TreatWarningsAsErrors,
│                                   #   NuGet metadata, references/Hexalith.Tenants path resolution
├── Directory.Build.targets         # Container image defaults (.NET SDK container support, opt-in)
├── Directory.Packages.props        # Centralized package versions (ManagePackageVersionsCentrally)
├── nuget.config / package.json     # NuGet feeds; semantic-release devDependencies
├── .editorconfig                   # Allman braces, file-scoped ns, _camelCase, 4-space, CRLF
├── .releaserc.json / commitlint.config.mjs   # semantic-release + Conventional Commits enforcement
├── aspire.config.json              # Aspire CLI config
├── CLAUDE.md / AGENTS.md           # Agent operating instructions
│
├── src/        # 15 projects — core framework + admin suite (see below)
├── samples/    # Counter domain sample + Blazor sample UI + sample tests
├── tests/      # 17 test projects (Tier 1 unit → Tier 3 integration/E2E)
├── perf/       # NBomber load tests
├── deploy/     # Production DAPR component YAML + deploy/README.md
├── docs/       # Hand-authored documentation site (+ docs/brownfield/ = THIS generated set)
├── scripts/    # Tooling scripts
│
├── references/
│   ├── Hexalith.Tenants/   # Submodule: multi-tenant domain service (5 projects in slnx)
│   ├── Hexalith.Commons/   # Submodule: shared utilities (ValueOrError, ULIDs, etc.)
│   ├── Hexalith.AI.Tools/  # Submodule: AI tooling
│   ├── Hexalith.Builds/    # Submodule: shared build assets
│   ├── Hexalith.FrontComposer/ # Submodule: UI composition framework
│   ├── Hexalith.Memories/  # Submodule: memories/search domain
│   └── Hexalith.PolymorphicSerializations/ # Submodule: polymorphic serialization library
```

> **Submodule rule (CLAUDE.md):** initialize/update only root-declared submodules under `references/`; never recurse into
> nested submodules. Do not modify `Hexalith.Builds`/submodule files without explicit approval.

## `src/` — Core Framework

```
src/
├── Hexalith.EventStore.Contracts/        (107)  # Domain contracts — entry point for shared types
│   ├── Commands/      CommandEnvelope, CommandStatus, SubmitCommandRequest/Response, ArchivedCommand
│   ├── Events/        IEventPayload, IRejectionEvent, EventEnvelope, EventMetadata (15 fields)
│   ├── Identity/      AggregateIdentity (TenantId:Domain:AggregateId), IdentityParser
│   ├── Queries/       IQueryContract, IQueryResponse<T>, QueryEnvelope, QueryResult, filters/sort/paging
│   ├── Results/       DomainResult (Success/Rejection/NoOp), DomainServiceWireResult
│   ├── Problems/      RFC 9457 problem contracts + GatewayProblemDetailsExtensions
│   ├── Security/      Payload protection metadata, crypto-shredding + restored-backup workflows
│   ├── Replay/        AggregateReconstructionRequest/Result/Status
│   ├── Aggregates/    ITerminatable (tombstoning)
│   ├── Authorization/ AuthorizationFailureReason
│   ├── Messages/      MessageType value object ({domain}-{name}-v{version})
│   ├── Projections/   Projection query + notification DTOs
│   ├── Streams/       Event stream read/write/replay contracts
│   └── Validation/    ValidateCommand/QueryRequest, PreflightValidationResult
│
├── Hexalith.EventStore.Client/           (35)   # Aggregate programming model + discovery
│   ├── Aggregates/    EventStoreAggregate<TState>, EventStoreProjection<TReadModel>, AggregateReplayer
│   ├── Handlers/      IDomainProcessor (pure command contract)
│   ├── Conventions/   NamingConventionEngine (kebab-case domain + DAPR resource naming)
│   ├── Discovery/     AssemblyScanner, DiscoveredDomain, DiscoveryResult
│   ├── Attributes/    EventStoreDomainAttribute (override convention)
│   ├── Configuration/ EventStoreOptions, EventStoreDomainOptions (5-layer cascade)
│   └── Registration/  AddEventStore() / AddEventStoreClient<T>() / AddEventStoreGatewayClient()
│
├── Hexalith.EventStore.Server/           (136)  # ← server-side engine (DAPR integration)
│   ├── Actors/        AggregateActor, ETagActor, EventReplayProjectionActor (+ RBAC/Tenant validators)
│   ├── Commands/      ICommandRouter/CommandRouter, Dapr command status + archive stores
│   ├── Queries/       IQueryRouter/QueryRouter, ETag service, actor-id derivation
│   ├── DomainServices/ IDomainServiceInvoker → DaprDomainServiceInvoker, resolver, registration
│   ├── Events/        EventPersister, EventPublisher, EventStreamReader, SnapshotManager, DeadLetterPublisher
│   ├── Projections/   ProjectionUpdateOrchestrator, DaprProjectionChangeNotifier, checkpoints, poller
│   ├── Pipeline/      SubmitCommandHandler (MediatR), query submission handlers
│   ├── Configuration/ Options (Backpressure, EventDrain, Snapshot, Projection, CommandConcurrency) + DI
│   ├── Diagnostics/   Protected-data redaction utilities
│   └── Telemetry/     EventStoreActivitySource
│
├── Hexalith.EventStore/                   (99)   # ← REST/gRPC gateway ("CommandApi"), HOSTS actors
│   ├── Controllers/   13 controllers (commands, queries, streams, replay, validation, admin, projections)
│   ├── Authentication/ JWT bearer config, DAPR internal auth, claims transformation
│   ├── Authorization/ Tenant + RBAC validators (claims-based and actor-based)
│   ├── Validation/    FluentValidation rules (commands, queries, notifications)
│   ├── ErrorHandling/ Exception handlers + ProblemDetails factories (RFC 9457)
│   ├── Middleware/    CorrelationIdMiddleware
│   ├── Pipeline/      Authorization/validation/logging MediatR behaviors
│   ├── Configuration/ Rate limiting, auth options, extension metadata sanitization
│   ├── HealthChecks/  DAPR sidecar, pub/sub, state store, actor placement probes
│   ├── OpenApi/       OpenAPI transformers, error-reference docs, version fallback
│   ├── SignalRHub/    ProjectionChangedHub
│   └── Program.cs     # Bootstrap: AddEventStore + AddEventStoreServer + AddEventStoreSignalR
│
├── Hexalith.EventStore.SignalR/           (8)    # EventStoreSignalRClient + hub contract + Redis backplane
├── Hexalith.EventStore.Testing/           (42)   # Builders, fakes, in-memory stores, assertions, compliance
├── Hexalith.EventStore.ServiceDefaults/   (7)    # OpenTelemetry, health, discovery, resilience
├── Hexalith.EventStore.Aspire/            (8)    # AddHexalithEventStore() hosting extensions + resources
├── Hexalith.EventStore.AppHost/           (26)   # Aspire app model + DaprComponents/ (local topology)
│   └── DaprComponents/  statestore.yaml, pubsub.yaml, accesscontrol*.yaml, resiliency.yaml, subscription-*
│
└── Admin suite ────────────────────────────────────────────────────────────────────
    ├── Admin.Abstractions/   (117)  # 16 service interfaces + DTOs + AdminRole + redaction (UnsafeMarkerDetection)
    ├── Admin.Server/         (55)   # Dapr* service impls + 10 REST controllers + 3-tier authz
    ├── Admin.Server.Host/    (11)   # ASP.NET host (JWT, OpenAPI) → container eventstore-admin
    ├── Admin.UI/             (47)   # Blazor dashboard (22 pages, FluentUI) → container eventstore-admin-ui
    ├── Admin.Cli/            (76)   # System.CommandLine tool "eventstore-admin" (7 command groups)
    └── Admin.Mcp/            (31)   # MCP server: 18+ AI-callable admin tools (stdio JSON-RPC)
```

## `samples/`

```
samples/
├── Hexalith.EventStore.Sample/            # Counter + Greeting domains
│   └── Counter/  CounterAggregate (fluent), CounterProcessor (legacy IDomainProcessor),
│                 CounterProjectionHandler, Commands/, Events/, State/CounterState, Queries/
├── Hexalith.EventStore.Sample.BlazorUI/   # Blazor Server sample (3 SignalR refresh patterns)
│   ├── Components/  CounterCommandForm, CounterValueCard, CounterHistoryGrid
│   └── Pages/      NotificationPattern, SilentReloadPattern, SelectiveRefreshPattern
└── Hexalith.EventStore.Sample.Tests/      # Tier 1 unit tests for the sample
```

## `tests/` (17 projects) & `perf/`

See [development-guide.md](./development-guide.md) for the full tiered test matrix. Highlights:
`Contracts/Client/SignalR/Sample/Testing/AppHost/Admin.*` = Tier 1; `Server.Tests`, `Admin.UI.Tests`
(bunit) = Tier 2; `IntegrationTests` (Aspire.Hosting.Testing + Redis), `Admin.UI.E2E` (Playwright) = Tier 3;
`perf/Hexalith.EventStore.LoadTests` (NBomber) = Tier 4.

## Entry Points

| Service | Entry point |
|---------|-------------|
| Event store gateway | `src/Hexalith.EventStore/Program.cs` |
| Admin REST API | `src/Hexalith.EventStore.Admin.Server.Host/Program.cs` |
| Admin Blazor UI | `src/Hexalith.EventStore.Admin.UI/Program.cs` |
| Admin CLI | `src/Hexalith.EventStore.Admin.Cli/Program.cs` (`eventstore-admin`) |
| Admin MCP | `src/Hexalith.EventStore.Admin.Mcp/Program.cs` (stdio) |
| Local topology | `src/Hexalith.EventStore.AppHost/Program.cs` (`aspire run`) |
| Sample domain | `samples/Hexalith.EventStore.Sample/` |
| Sample UI | `samples/Hexalith.EventStore.Sample.BlazorUI/Program.cs` |
