---
title: '1.5 Domain Module Hosting Observability'
type: 'feature'
created: '2026-07-06T00:00:00+02:00'
status: 'done'
baseline_revision: '415daa2df98c3bd273a7b76b471dccc9d3d9bcdb'
final_revision: '8a91b098536d3c6193efbdbbfaa369a816ede797'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
warnings: []
---

<intent-contract>

## Intent

**Problem:** Domain-service hosts expose the canonical endpoints, but domain-specific diagnostics are still manual and unkeyed, and Aspire domain-module sidecars do not probe the SDK `/ready` endpoint. This leaves the two-line host less observable than the platform contract promises.

**Approach:** Make the DomainService SDK register convention-named diagnostics for every discovered domain and resolve them by request domain, while keeping DAPR state-store readiness explicitly opt-in for domains that actually have state-store access. Make the Aspire domain-module extension enable DAPR app health checks against `/ready` by default and cover shared/isolated wiring with AppHost-safe tests.

## Boundaries & Constraints

**Always:** Preserve the canonical domain host endpoints and Sample's two-line host shape; keep isolated modules without state/pubsub references; keep health, telemetry, and Aspire boilerplate in EventStore platform projects; use per-project tests and `Hexalith.EventStore.slnx` only for restore/build; add `ConfigureAwait(false)` on awaits.

**Block If:** The implementation requires changing a submodule file, granting Sample direct state-store/pubsub access, replacing the existing ServiceDefaults endpoint contract, or changing DAPR access-control semantics.

**Never:** Do not auto-register DAPR state-store readiness for isolated/service-invocation-only modules; do not add domain-owned `*.Aspire` or `*.ServiceDefaults` projects; do not rely on `tests/Hexalith.EventStore.Server.Tests` as the validation lane for new AppHost/Aspire behavior.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Multi-domain diagnostics | A domain service discovers `counter` and `greeting` domains | Two diagnostics exist and router telemetry uses the diagnostics matching the request domain | Unknown domain falls back to no domain diagnostics instead of using the wrong source |
| Domain sidecar health | `AddEventStoreDomainModule(...)` is called in shared or isolated mode | DAPR sidecar options enable app health checks and set app health path to `/ready` | Explicit lower-level options can still override when needed |
| State-store readiness | A domain that uses a state store opts into `AddEventStoreDomainStateStoreHealthCheck("tenants", tags: ["ready"])` | `/ready` reflects the DAPR state-store dependency with the conventional health-check name | Failed DAPR read reports Unhealthy without leaking payload/state data |

</intent-contract>

## Code Map

- `src/Hexalith.EventStore.DomainService/EventStoreDomainTelemetry.cs` -- domain diagnostics types and registration extension; add keyed/registry behavior without breaking single-domain injection.
- `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs` -- canonical host registration; discover domain names and register platform diagnostics automatically.
- `src/Hexalith.EventStore.DomainService/DomainServiceRequestRouter.cs` -- admission telemetry lookup; resolve diagnostics by `request.Command.Domain`.
- `src/Hexalith.EventStore.DomainService/DaprStateStoreHealthCheck.cs` -- opt-in readiness check; strengthen behavior tests only, not automatic registration.
- `src/Hexalith.EventStore.Aspire/HexalithEventStoreDomainModuleExtensions.cs` -- public Aspire domain-module facade; default DAPR app health to `/ready`.
- `src/Hexalith.EventStore.Aspire/AspireDaprDomainModuleAspireExtensions.cs` -- lower-level sidecar options mapping; preserve explicit override semantics.
- `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainTelemetryTests.cs` -- registration, keyed lookup, and state-store health behavior coverage.
- `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs` -- automatic diagnostics and router telemetry coverage.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/DomainModuleAspireExtensionTests.cs` -- focused Aspire tests for shared/isolated sidecar health defaults.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.DomainService/EventStoreDomainTelemetry.cs` -- add a domain diagnostics registry/factory and idempotent multi-domain registration helper -- prevents a multi-domain host from resolving the wrong singleton diagnostics.
- [x] `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs` -- after `AddEventStore(...)`, register convention diagnostics for distinct discovered aggregate/projection domains -- makes the two-line host observable without domain boilerplate.
- [x] `src/Hexalith.EventStore.DomainService/DomainServiceRequestRouter.cs` -- resolve diagnostics by `request.Command.Domain` for admission-stage spans/metrics -- keeps telemetry domain-correct.
- [x] `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainTelemetryTests.cs` -- cover name trimming/validation, registry lookup, idempotent registration, and healthy/unhealthy state-store probe behavior -- locks the observability contract.
- [x] `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs` -- cover automatic diagnostics registration and multi-domain admission telemetry selection -- proves canonical hosting owns diagnostics.
- [x] `src/Hexalith.EventStore.Aspire/HexalithEventStoreDomainModuleExtensions.cs` -- pass `EnableAppHealthCheck = true` and `AppHealthCheckPath = "/ready"` into domain sidecars by default -- lets DAPR observe domain-service readiness without granting infrastructure access.
- [x] `tests/Hexalith.EventStore.AppHost.Tests/Configuration/DomainModuleAspireExtensionTests.cs` -- verify shared and isolated domain modules get `/ready` app-health options while retaining shared-vs-isolated component behavior -- moves topology proof out of the known-problem Server.Tests lane.

**Acceptance Criteria:**
- Given a canonical domain-service host with discovered domains, when services are built, then each distinct domain has convention-named `EventStoreDomainDiagnostics` registered and OpenTelemetry is configured for its source and meter.
- Given an admission stage processes a command for one discovered domain, when another domain is also registered, then the emitted span uses only `Hexalith.EventStore.Domain.<command-domain>`.
- Given a domain module is added through the public Aspire extension, when its DAPR sidecar options are created, then app health checks are enabled at `/ready` in both shared and isolated modes.
- Given the Sample domain module remains isolated, when the Aspire extension applies health defaults, then it still has no shared state-store/pubsub references.
- Given a domain opts into the state-store health check, when the DAPR read succeeds or fails, then the health result is Healthy or Unhealthy respectively with the conventional registration name.

## Spec Change Log

## Review Triage Log

### 2026-07-06 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 3: (high 0, medium 2, low 1)
- defer: 0
- reject: 0
- addressed_findings:
  - `[medium]` `[patch]` Automatic telemetry registration missed handler-only domains; amended domain collection to include handler domain declarations from explicit domain attributes and safe parameterless handlers, with coverage for a handler-only `catalog` domain.
  - `[medium]` `[patch]` Multi-domain hosts left direct `EventStoreDomainDiagnostics` injection ambiguous; changed diagnostics to registry/keyed resolution, with direct injection failing fast unless exactly one domain is registered.
  - `[low]` `[patch]` New process-request test data used GUID-shaped message IDs; replaced those IDs with `UniqueIdHelper.GenerateSortableUniqueStringId()`.

### 2026-07-06 — Follow-up review pass
- intent_gap: 0
- bad_spec: 0
- patch: 3: (high 0, medium 0, low 3)
- defer: 3
- reject: 8
- addressed_findings:
  - `[low]` `[patch]` The prior pass's ULID cleanup missed one helper: `CreateGadgetProcessRequest()` still built `MessageId` with `Guid.NewGuid().ToString()` (forbidden by rule R2-A7). Replaced with `UniqueIdHelper.GenerateSortableUniqueStringId()`.
  - `[low]` `[patch]` `DomainServiceRequestRouter.ResolveDiagnostics` threw `ArgumentException`/NRE on a blank command `Domain` — reachable because `CommandEnvelope` identity validation is bypassed by `DataContractSerializer` on the wire path. Added a null/whitespace guard returning `null` so best-effort telemetry resolution is never the failure point; the canonical keyed-processor lookup still produces the real error.
  - `[low]` `[patch]` The admission histogram (`EventStoreDomainDiagnostics.RecordAdmissionStage`, invoked from the router) had no metric-side test — only the span was asserted. Added a `MeterListener`-based test proving the recorded measurement carries the request domain, command type, and acceptance result (Epic-2 assert-end-state rule).
- deferred_findings (see `deferred-work.md`, this-spec section):
  - Handler-domain discovery drops DI-constructed handlers without `[EventStoreDomain]` and reflectively instantiates parameterless handlers at host build.
  - DAPR app-health probe defaulted to `/ready` couples module traffic-eligibility to `ready`-tagged readiness checks (sidecar feedback loop; anonymous-access requirement).
  - Single `EventStoreDomainDiagnostics` is disposed by both the registry and the keyed/single-domain DI factories (idempotent-only safety).
- rejected: attribute-vs-runtime `.Domain` divergence; `ResolveDiagnostics` skipping directly-registered diagnostics under a registry (SDK never builds that mix); `ImplementationInstance` coupling in `GetRegisteredDiscoveryResult` (hypothetical future change); dual-interface-handler switch ordering and blank `handler.Domain` (contrived); zero-domain direct-injection landmine (benign); no override seam at the Aspire facade (override lives at the lower-level extension per spec); redundant `GetTypes()` scans and minor allocations/probe-interval defaults (perf/cosmetic).

## Design Notes

State-store readiness remains explicit because the platform cannot infer AppHost infrastructure mode from `AddEventStoreDomainService()` inside the domain process. The Sample module is intentionally isolated and must not fail readiness because a state-store component is absent. DAPR app health, however, is a sidecar concern and can safely target the SDK `/ready` endpoint for all domain modules.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/` -- expected: new and existing DomainService tests pass.
- `dotnet test tests/Hexalith.EventStore.AppHost.Tests/` -- expected: new Aspire sidecar option tests pass.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- expected: solution builds without warnings as errors.

## Auto Run Result

Status: done

Summary:
- Added convention-named domain diagnostics registration to the DomainService SDK, backed by registry and keyed service resolution so multi-domain hosts use the correct ActivitySource/Meter.
- Enabled DAPR app health checks at `/ready` for EventStore domain-module sidecars while preserving shared-vs-isolated component references.
- Added focused DomainService and AppHost tests for diagnostics registration, state-store health behavior, and Aspire sidecar health options.

Files changed:
- `src/Hexalith.EventStore.DomainService/EventStoreDomainTelemetry.cs` -- kept naming conventions focused in the original telemetry convention type.
- `src/Hexalith.EventStore.DomainService/EventStoreDomainDiagnostics.cs` -- moved per-domain ActivitySource/Meter diagnostics into its own type file.
- `src/Hexalith.EventStore.DomainService/EventStoreDomainDiagnosticsRegistry.cs` -- added registry-backed domain diagnostics lookup.
- `src/Hexalith.EventStore.DomainService/EventStoreDomainTelemetryExtensions.cs` -- added idempotent multi-domain telemetry registration, keyed diagnostics, and single-domain direct-injection guardrails.
- `src/Hexalith.EventStore.DomainService/EventStoreDomainTelemetryRegistration.cs` -- added internal telemetry registration marker.
- `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs` -- registered diagnostics for discovered aggregate/projection domains and safe handler domain declarations.
- `src/Hexalith.EventStore.DomainService/DomainServiceRequestRouter.cs` -- resolved admission telemetry diagnostics by command domain.
- `src/Hexalith.EventStore.Aspire/HexalithEventStoreDomainModuleExtensions.cs` -- defaulted domain-module DAPR sidecars to `/ready` app health checks.
- `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainTelemetryTests.cs` -- covered registry/keyed diagnostics, direct-injection guardrails, and DAPR state-store health results.
- `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs` -- covered automatic diagnostics registration and request-domain telemetry selection.
- `tests/Hexalith.EventStore.DomainService.Tests/Fixtures/WidgetDomain.cs` -- added local multi-domain and handler-only fixtures.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/DomainModuleAspireExtensionTests.cs` -- added shared/isolated Aspire sidecar health option tests.

Review findings breakdown:
- Patches applied: 3 (medium 2, low 1).
- Items deferred: 0.
- Items rejected: 0.
- Follow-up review recommended: true.

Verification performed:
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/` -- passed, 66 tests.
- `dotnet test tests/Hexalith.EventStore.AppHost.Tests/` -- passed, 37 tests.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- passed, 0 warnings/errors.
- `git diff --check` -- passed.

Residual risks:
- Dependency-heavy handler-only domains need an explicit `EventStoreDomainAttribute` (or another future static declaration) for telemetry registration before service-provider build; parameterless handlers are inferred automatically.

### Follow-up review pass (2026-07-06)

An independent Blind Hunter + Edge Case Hunter pass reviewed the committed change again. Triage: 3 patches applied (all low severity), 3 findings deferred, 8 rejected. No intent gaps or spec defects — no re-implementation loopback.

Patches applied:
- `tests/.../EventStoreDomainServiceExtensionsTests.cs` -- replaced the last GUID-shaped `MessageId` in `CreateGadgetProcessRequest()` with `UniqueIdHelper.GenerateSortableUniqueStringId()` (rule R2-A7); the prior pass's ULID cleanup had missed this helper.
- `src/Hexalith.EventStore.DomainService/DomainServiceRequestRouter.cs` -- guarded `ResolveDiagnostics` against a null/whitespace command `Domain` (reachable via `DataContractSerializer` constructor-bypass on the wire path) so best-effort telemetry resolution can never throw; the canonical keyed-processor lookup still surfaces the real error.
- `tests/.../EventStoreDomainServiceExtensionsTests.cs` -- added a `MeterListener`-based test asserting the admission histogram records a measurement carrying the request domain, command type, and acceptance result (Epic-2 assert-end-state rule).

Deferred (new entries in `deferred-work.md`): handler-domain discovery drops DI-constructed handlers without `[EventStoreDomain]` and reflectively instantiates parameterless handlers at host build; the `/ready` DAPR app-health default couples module traffic-eligibility to `ready`-tagged readiness checks (sidecar feedback loop + anonymous-access requirement); the single diagnostics instance is disposed by both the registry and the keyed/single-domain DI factories.

Verification (follow-up pass):
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/` -- passed, 67 tests (66 prior + 1 new metric test).
- `dotnet test tests/Hexalith.EventStore.AppHost.Tests/` -- passed, 37 tests.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- passed, 0 warnings/errors.
- `git diff --check` -- passed.

Follow-up review recommended: false — the three patches are all low-severity and localized (a test-data fix, a small defensive guard, and one added test); the substantive findings were deferred rather than changed.
