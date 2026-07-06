---
title: '1.6 Sample And Tenants Domain-Centric Adoption'
type: 'feature'
created: '2026-07-06T00:00:00+02:00'
status: 'done'
baseline_revision: 'cc72a56619edd776cc0177c47599d63db16ecfb6'
final_revision: '968a54446687bc9a249fb0bd88f8afe47bfc0668'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
warnings: []
---

<intent-contract>

## Intent

**Problem:** The Sample and Tenants references have adopted many Epic 1 seams, but Sample still carries a legacy hand-written `IDomainProcessor`, and root Tenants source-mode routing does not register the `tenants` domain with the EventStore gateway. Tenants also still has transitional host wiring that must be narrowed without losing RBAC, audit, read-model, cursor, projection, health, or bootstrap behavior.

**Approach:** Remove the Sample legacy processor path, make Tenants source-mode routing complete in the root EventStore configuration, and refactor Tenants host registration to consume the DomainService SDK for discovery/query/telemetry/default endpoint ownership while preserving Tenants-specific read-model/cursor/data-protection/bootstrap and the current bespoke persisted `/project` route.

## Boundaries & Constraints

**Always:** Preserve Sample's normal two-call SDK host shape; preserve Tenants query/read-model/cursor semantics and generated external `Hexalith.Tenants.Api`; keep Tenants' custom async persisted `/project` path until the platform has an async multi-read-model projection seam; use root `.slnx` only for build/restore and per-project tests; add `ConfigureAwait(false)` on awaits.

**Block If:** The implementation requires deleting or relocating `Hexalith.Tenants.AppHost`/`Hexalith.Tenants.Aspire`, changing Tenants UI/API topology ownership, or replacing Tenants' persisted projection behavior with the current synchronous stateless `IDomainProjectionHandler`.

**Never:** Do not reintroduce Sample/Tenants-owned ServiceDefaults, projection actors, cursor codecs, state-store wrappers, telemetry sources/meters, generated controllers in interactive UI hosts, or EventStore server/actor hosting inside a domain service.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Sample command dispatch | Sample `counter` command routed through DomainService SDK discovery | `CounterAggregate` handles the command; no `CounterProcessor` type or legacy manual deserialization path remains | Unknown command behavior remains aggregate/SDK-owned |
| Root Tenants routing | EventStore root configuration in source-mode/local development | `system|tenants|v1` and `system|global-administrators|v1` both route to app id `tenants` and method `process` | Missing route fails a focused configuration test |
| Tenants query metadata | Tenants SDK host starts with discovered query handlers | `/admin/operational-index-metadata` reports all Tenants handler-served query types and `/query` routes through `IDomainQueryHandler` | Duplicate handler routes fail at host startup |
| Tenants projection persistence | Tenants receives `/project` for `tenants` or `global-administrators` | Existing read-model, audit, index, `ProjectedAt`, and optimistic-concurrency behavior is preserved through `IReadModelStore` | Unsupported/invalid projection domain keeps support-safe 400 behavior |

</intent-contract>

## Code Map

- `samples/Hexalith.EventStore.Sample/Counter/CounterProcessor.cs` -- legacy hand-written processor to remove.
- `tests/Hexalith.EventStore.Sample.Tests/Counter/CounterProcessorTests.cs` -- legacy processor tests to delete or replace with aggregate/SDK coverage.
- `tests/Hexalith.EventStore.Sample.Tests/Registration/FluentApiRegistrationIntegrationTests.cs` -- remove the backward-compatibility assertion for `AddEventStoreClient<CounterProcessor>` and keep aggregate discovery assertions.
- `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` -- extend guardrails so Sample cannot carry non-aggregate domain processors or normal-mode host plumbing.
- `src/Hexalith.EventStore/appsettings.json` and `src/Hexalith.EventStore/appsettings.Development.json` -- add the missing `system|tenants|v1` domain-service registration.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/EventStoreDomainServiceConfigurationTests.cs` -- add focused root configuration tests for Sample and Tenants domain-service registrations without using the known-problem Server.Tests lane.
- `references/Hexalith.Tenants/src/Hexalith.Tenants/Program.cs` -- replace manual SDK-owned discovery/query/telemetry/default endpoint registration with `AddEventStoreDomainService(...)` / `UseEventStoreDomainService()` while keeping Tenants-specific state-store health, read-model store, cursor codec, data protection, bootstrap, auth needs, and bespoke `/project`.
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Server/Hexalith.Tenants.Server.csproj` -- remove unused direct DAPR actor package references.
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Configuration/EventPublicationConfigurationTests.cs` -- update host-shape/configuration assertions to match the SDK-owned route and retained `/project` exception.

## Tasks & Acceptance

**Execution:**
- [x] `samples/Hexalith.EventStore.Sample/Counter/CounterProcessor.cs` -- delete the legacy manual `IDomainProcessor` implementation -- the reference Sample should prove aggregate convention discovery, not a parallel processor path.
- [x] `tests/Hexalith.EventStore.Sample.Tests/Counter/CounterProcessorTests.cs` and `tests/Hexalith.EventStore.Sample.Tests/Registration/FluentApiRegistrationIntegrationTests.cs` -- remove legacy processor expectations and keep aggregate/SDK dispatch coverage -- prevents tests from preserving removed boilerplate.
- [x] `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` -- add Sample guardrails for no legacy non-aggregate processors and no normal-mode endpoint/service-default/DAPR/controller plumbing, while keeping the documented opt-in malformed `/project` fault exception -- locks the minimal reference shape.
- [x] `src/Hexalith.EventStore/appsettings.json`, `src/Hexalith.EventStore/appsettings.Development.json`, and `tests/Hexalith.EventStore.AppHost.Tests/Configuration/EventStoreDomainServiceConfigurationTests.cs` -- add and prove the root `system|tenants|v1` route alongside `system|global-administrators|v1` -- makes root Tenants adoption runnable in source mode.
- [x] `references/Hexalith.Tenants/src/Hexalith.Tenants/Program.cs` -- consume `AddEventStoreDomainService(typeof(TenantAggregate).Assembly, typeof(GetTenantQueryHandler).Assembly)` and `UseEventStoreDomainService()` for SDK-owned discovery/query/telemetry/default endpoints, removing duplicate manual query handler registration and direct `MapEventStoreDomainService()` -- narrows the Tenants host to domain-specific composition plus the known projection exception.
- [x] `references/Hexalith.Tenants/src/Hexalith.Tenants.Server/Hexalith.Tenants.Server.csproj` -- remove unused `Dapr.Actors` and `Dapr.Actors.AspNetCore` references -- keeps actor hosting dependencies out of domain code.
- [x] `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Configuration/EventPublicationConfigurationTests.cs` and existing Tenants projection/query tests -- update assertions so SDK-owned query/metadata routes are expected, bespoke persisted `/project` remains explicitly documented, and read-model/audit/index end-state tests still exercise production handlers -- preserves non-trivial semantics.

**Acceptance Criteria:**
- Given the Sample domain is inspected, when guardrail tests run, then no legacy hand-written processor, DAPR package, normal-mode route mapping, ServiceDefaults, or operational metadata wiring remains in the Sample domain project.
- Given EventStore root configuration is loaded, when domain-service registrations are inspected, then both Tenants domains route to app id `tenants` with method `process`.
- Given the Tenants host builds services, when SDK discovery runs, then Tenants query handlers are registered through DomainService discovery and operational metadata reports their query types.
- Given Tenants projection tests run, when tenant and global-administrator projection requests are processed, then persisted detail, audit, index, freshness timestamp, and invalid-identity behavior match the pre-refactor contract.

## Spec Change Log

- 2026-07-06 -- Implemented Sample legacy processor removal, root Tenants domain-service config, Tenants SDK host refactor, review-found health response preservation, and focused validation.

## Review Triage Log

- [patched] HIGH -- Review found Tenants' development health response would expose health-check `Data` after endpoint ownership moved under `UseEventStoreDomainService()`. Added `EventStoreServiceDefaultsOptions.DevelopmentHealthResponseWriter`, configured Tenants to use the support-safe writer, and verified `HealthEndpointsTests` health filter passes.
- [patched] MEDIUM -- Review found the extra partial type-load scanner could hide a broken required handler. Reverted DomainService discovery back to strict `Assembly.GetTypes()` and removed the partial-load test/file.
- [patched] MEDIUM -- Review found root config tests only parsed JSON text. Reworked them to bind `DomainServiceOptions` and resolve Sample/Tenants routes through `DomainServiceResolver`.
- [patched] MEDIUM -- Review found Tenants SDK query coverage stopped at DI/metadata. Extended the Tenants configuration test to dispatch a `list-tenants` query through `DomainQueryDispatcher`, the same dispatcher used by the SDK `/query` endpoint.
- [patched] LOW -- Review found the new legacy-processor guardrail missed fully qualified `IDomainProcessor` names and only scanned Sample. Extended it to all initialized domain-module roots and fully qualified direct processor declarations.
- [deferred] MEDIUM -- Review noted the mixed Tenants/EventStore source-package graph still needs explicit `-p:Version=3.41.0` for Tenants tests. This is a pre-existing verification/reproducibility constraint outside the Sample/Tenants domain-centric adoption code path; verification records the required property and serial execution to avoid shared-output races.

## Design Notes

Tenants' current projection path is intentionally not converted to `IDomainProjectionHandler`: that platform seam is synchronous and stateless, while Tenants writes multiple persisted read models with optimistic concurrency. This story records the exception and removes surrounding duplicated SDK plumbing; a future platform seam can retire the bespoke `/project` route.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/ --configuration Release --no-restore -m:1` -- passed: 82/82.
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/ --configuration Release --no-restore` -- passed: 68/68.
- `dotnet test tests/Hexalith.EventStore.AppHost.Tests/ --configuration Release` -- passed: 40/40.
- `dotnet test references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/ --configuration Release --filter "FullyQualifiedName~Health" -p:HexalithEventStoreFromSource=true -p:HexalithCommonsFromSource=false -p:HexalithMemoriesFromSource=false -p:HexalithFrontComposerFromSource=false -p:Version=3.41.0 -m:1` -- passed: 5/5.
- `dotnet test references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/ --configuration Release --filter "FullyQualifiedName~EventPublicationConfigurationTests" -p:HexalithEventStoreFromSource=true -p:HexalithCommonsFromSource=false -p:HexalithMemoriesFromSource=false -p:HexalithFrontComposerFromSource=false -m:1` -- passed: 23/23.
- `dotnet test references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/ --configuration Release --filter "FullyQualifiedName~Configuration|FullyQualifiedName~Projection|FullyQualifiedName~Query" -p:HexalithEventStoreFromSource=true -p:HexalithCommonsFromSource=false -p:HexalithMemoriesFromSource=false -p:HexalithFrontComposerFromSource=false -p:Version=3.41.0 -m:1` -- passed: 503/503. Without the explicit `Version=3.41.0`, the mixed Tenants/EventStore source-package graph builds `Hexalith.EventStore.Gateway` as `3.31.0.0` while tests request `3.41.0.0`.
- `dotnet test references/Hexalith.Tenants/tests/Hexalith.Tenants.Client.Tests/ --configuration Release -p:HexalithEventStoreFromSource=true -p:HexalithCommonsFromSource=false -p:HexalithMemoriesFromSource=false -p:HexalithFrontComposerFromSource=false -p:Version=3.41.0 -m:1` -- passed: 48/48.
- `dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore -m:1` -- passed: 0 warnings, 0 errors.
- `git diff --check && git -C references/Hexalith.Tenants diff --check` -- passed.

**Notes:**
- A parallel verification attempt caused the known Tenants version mismatch while another test build wrote shared root outputs, and one concurrent Sample run was interrupted after its test host stopped producing output. The affected suites were rerun serially and passed.
