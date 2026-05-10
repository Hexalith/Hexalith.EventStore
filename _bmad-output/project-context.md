---
project_name: 'Hexalith.EventStore'
user_name: 'Jerome'
date: '2026-05-10'
sections_completed: ['technology_stack', 'language_rules', 'framework_rules', 'testing_rules', 'code_quality_rules', 'workflow_rules', 'critical_rules']
existing_patterns_found: 17
status: 'complete'
rule_count: 62
optimized_for_llm: true
---

# Project Context for AI Agents

_This file contains critical rules and patterns that AI agents must follow when implementing code in this project. Focus on unobvious details that agents might otherwise miss._

---

## Technology Stack & Versions

- Target .NET SDK `10.0.103` and `net10.0`; keep new projects aligned with root `global.json` and `Directory.Build.props`.
- Use centralized package management in `Directory.Packages.props`; do not add package versions directly to individual `.csproj` files unless the repo already makes an explicit exception.
- Aspire is the application orchestrator. App model changes belong in `src/Hexalith.EventStore.AppHost/Program.cs` and require an Aspire restart to take effect.
- Use Aspire package versions aligned with `Aspire.AppHost.Sdk/13.2.2`; consult current Aspire integration docs before adding app model resources.
- Use DAPR through the configured sidecar model and package family `1.17.7`; application code should stay portable across DAPR state store/pubsub backends.
- Admin UI is Blazor with Microsoft Fluent UI `5.0.0-rc.2-26098.1`; preserve existing Fluent UI component patterns instead of introducing a second UI component system.
- Node dependencies are release/workflow tooling only; do not introduce frontend build assumptions unless the touched project already has them.

## Critical Implementation Rules

### Language-Specific Rules

- Keep C# nullable reference types clean; new APIs should express nullability intentionally and avoid `!` except at proven framework/test boundaries.
- Treat warnings as build-breaking because root `Directory.Build.props` sets `TreatWarningsAsErrors=true`; fix analyzer and compiler warnings instead of suppressing them locally.
- Follow `.editorconfig`: file-scoped namespaces, `using` directives outside namespaces, system usings first, 4-space indentation, CRLF, UTF-8, final newline.
- Name interfaces with `I`, private fields with `_camelCase`, async methods with `Async`, and public types/members in PascalCase.
- Prefer records for immutable request/response/data models and sealed classes for concrete services, matching the existing codebase.
- Register services through `Add*` extension methods and project-specific configuration classes; avoid scattering inline DI setup across host `Program.cs` files.
- Use `ArgumentNullException.ThrowIfNull(...)` for public guard clauses and keep exception messages actionable where they cross API/admin surfaces.
- Do not add ad hoc retry loops around DAPR or HTTP infrastructure calls; resiliency belongs in DAPR/Aspire/resilience configuration unless an existing component explicitly owns retry semantics.

### Framework-Specific Rules

- Aspire owns runtime composition. Run and validate through `aspire run`; apphost edits require restarting the Aspire app before resource changes appear.
- Do not install or use the obsolete Aspire workload. Use the Aspire CLI and the existing `Aspire.AppHost.Sdk`.
- When adding Aspire resources, first inspect available integrations and use official Aspire docs for the matching package/version family.
- Only initialize/update root-level submodules (`Hexalith.Tenants`, `Hexalith.AI.Tools`) unless nested submodules are explicitly requested.
- DAPR actor state must go through `IActorStateManager`; do not bypass actor isolation with `DaprClient` for aggregate actor state.
- Domain services should not receive direct Redis/state store/pubsub access unless the architecture explicitly changes; the sample domain service intentionally has no state/pubsub references.
- Preserve the EventStore pipeline ordering: correlation middleware first, exception handling, health/default endpoints, authentication, rate limiting, authorization, CloudEvents, controllers, subscriptions, actors.
- API errors must use RFC 7807 `ProblemDetails` and the existing exception handler stack; do not introduce custom error response shapes.
- Domain logic should use `EventStoreAggregate<TState>` or `IDomainProcessor`, return `DomainResult`, and model rejections as `IRejectionEvent` rather than exceptions.
- EventStore owns envelope metadata; domain services return payload events only.
- Admin UI work should stay in Blazor/Fluent UI patterns already present in `src/Hexalith.EventStore.Admin.UI`; do not introduce another UI framework or hand-rolled design system.

### Testing Rules

- Run targeted test projects individually; avoid broad solution-level `dotnet test` unless you are intentionally checking the whole tree.
- Unit test edits should normally use xUnit v3 with Shouldly assertions and NSubstitute mocks, matching existing tests.
- Prefer existing testing helpers in `src/Hexalith.EventStore.Testing` before creating new fakes/builders/assertions.
- For actor/unit tests without a live sidecar, use existing in-memory/fake DAPR helpers and remove DAPR health checks through `TestServiceOverrides` where appropriate.
- Integration tests under `tests/Hexalith.EventStore.IntegrationTests` require Docker and a running Aspire/DAPR environment.
- `Hexalith.EventStore.Server.Tests` has known pre-existing CA2007 warning-as-error build failures in this workspace; do not treat them as caused by unrelated changes without verifying.
- E2E security tests should use real Keycloak OIDC tokens; synthetic JWTs are only acceptable for dev-mode or non-runtime security tests that already use the symmetric-key path.
- For UI behavior changes, use Playwright/browser verification when a running endpoint is available from Aspire resources.

### Code Quality & Style Rules

- Keep dependencies flowing inward: `Contracts` has no Hexalith dependencies, `Server` depends on `Contracts`, and host/admin/UI projects sit at the edges.
- Keep feature/domain folders consistent with existing organization; do not reorganize into broad type buckets as part of unrelated work.
- Event names should be past tense for state changes or past-tense negative for rejections.
- Convention-derived resource names are kebab-case; type suffix stripping for `Aggregate` and `Projection` is automatic, and explicit overrides must be non-empty kebab-case.
- Never log event payload data, command payloads, secrets, or user-controllable display names as trusted identity; logs should use envelope metadata such as tenant, domain, aggregate, correlation ID, causation ID, command/event type, and stage.
- Every structured log and OpenTelemetry activity for command/event processing should carry `correlationId`; use the existing `EventStoreActivitySource` tags and source-generated logger patterns when nearby code does.
- Use `sub` as the authenticated user identifier; do not switch to `name` or other user-controllable claims.
- Keep XML documentation focused on public API surfaces and generated API docs; do not add noisy comments that restate obvious code.
- Do not create new shared abstractions until there is real duplication or an established local pattern to extend.

### Development Workflow Rules

- Before code changes, prefer establishing the current Aspire/resource state with `aspire run` and Aspire MCP diagnostics when practical.
- Validate incrementally with the narrowest relevant build/test command first, then broaden when touching shared contracts, apphost wiring, auth, DAPR components, or public APIs.
- Commit messages are Conventional Commits; semantic-release runs from `main` and publishes NuGet packages through the configured release pipeline.
- CI uses root-level submodules only; do not run recursive submodule initialization/update unless explicitly requested.
- `aspire publish` uses `PUBLISH_TARGET=docker|k8s|aca`; do not assume one publisher is active without checking environment/configuration.
- Local dev can run with `EnableKeycloak=false` to use symmetric-key JWT validation, but runtime security verification should keep Keycloak enabled.
- Persistent containers can create confusing state during development; prefer non-persistent resources unless the task explicitly needs persistence.
- Use official docs for Aspire, Microsoft, DAPR, and NuGet package behavior when changing integrations or version-sensitive infrastructure.

### Critical Don't-Miss Rules

- Tenant validation must happen before aggregate state rehydration or actor state access; never load state before proving the command tenant matches the actor identity.
- Command status queries are tenant-scoped. Do not look up status by correlation ID alone.
- Event store keys are write-once. Do not update or delete persisted event keys as part of normal command processing.
- Command status writes are advisory; status storage failures must not block the command pipeline.
- Snapshot configuration is mandatory, with the existing default of 100 events; do not disable snapshot behavior casually.
- Unknown historical event types during rehydration are correctness failures, not events to skip. Domain services must retain backward-compatible deserialization for every event type they have produced.
- Extension metadata must be sanitized at the API boundary before it enters the processing pipeline.
- DAPR access control is deny-by-default. When adding service invocation paths, update the receiving service config deliberately and verify caller app IDs.
- Domain rejections are normal events and should not be dead-lettered; infrastructure failures, publish failures, and unrecoverable processing failures follow the existing dead-letter/drain paths.
- DAPR slim/local mode may require placement and scheduler processes before actor flows work; actor failures that mention missing actor address often mean infrastructure startup is incomplete.
- In dev mode with `EnableKeycloak=false`, JWTs must use issuer `hexalith-dev`, audience `hexalith-eventstore`, `tenants` JSON array claims, and `permissions` such as `commands:*`.

---

## Usage Guidelines

**For AI Agents:**

- Read this file before implementing code in this repository.
- Follow all rules exactly as documented.
- When in doubt, prefer the more restrictive option.
- Update this file if new non-obvious implementation patterns emerge.

**For Humans:**

- Keep this file lean and focused on agent needs.
- Update it when the technology stack or architecture changes.
- Review periodically for outdated rules.
- Remove rules that become obvious or stop preventing real mistakes.

Last Updated: 2026-05-10
