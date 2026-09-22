---
title: 'Story 5.5: Internal And Domain-Service Trust Boundary'
type: 'feature'
created: '2026-09-22'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-5-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/epics.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A plaintext DAPR caller header creates a global-administrator principal. Domain routes and projection notifications lack credentials, while Tenants trusts wire administrator flags.

**Approach:** Authenticate the application channel and independently establish the caller or publisher identity; authorize each operation and scope before binding or downstream work. Rebuild administrator context from trusted identity at every protected boundary and preserve the three anonymous probes.

**Decision:** Use `APP_API_TOKEN` to authenticate the DAPR-to-app channel and short-lived workload assertions from the existing trusted JWT issuer for caller, audience, and operation proof. Bind signed publisher provenance to projection tenant/topic. Reuse the shared JWT validation contract; do not introduce a custom signing protocol or certificate platform. Human administrator authority requires separately verified, current delegation or authorization.

## Boundaries & Constraints

**Always:** Deny missing, duplicate, conflicting, expired, wrong-audience, wrong-caller, wrong-operation, and unavailable credentials before protected work. Validate notification tenant/topic consistency. Keep only `/health`, `/alive`, and `/ready` explicitly anonymous and support-safe. Retain authorized internal and delegated-human flows and bounded, safe reason/correlation telemetry.

**Never:** Derive grants from `dapr-caller-app-id`, loopback, ACLs, mTLS alone, or wire admin flags. Commit secrets; weaken public Admin policy or DTOs; claim Stories 5.6–5.8 topology parity; or change Story 5.10 reserved-`system` provisioning.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Internal invocation | Valid workload, audience, operation, tenant | Minimal principal; permitted operation executes | Wrong/stale proof denies |
| Header forgery | App ID or admin flag without proof | No grant or downstream work | Bounded denial |
| Route override | SDK or pre-mapped `/project` | Same policy on effective endpoint | Weak override fails inventory/startup |
| Projection callback | Publisher and tenant/topic match | ETag and bounded broadcast | Forgery leaves freshness unchanged |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore/Authentication/DaprInternalAuthenticationHandler.cs`, `DaprInternalAuthenticationOptions.cs`, `Extensions/ServiceCollectionExtensions.cs` — header-only admin mint. Reuse fixed-time channel-token verification from `Operations/Security/DaprAppChannelTokenMiddleware.cs`.
- `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs`, `EventStoreDomainEventsEndpointExtensions.cs` — unsecured routes and overrides. `ServiceDefaults/Extensions.cs` marks probes anonymous.
- `src/Hexalith.EventStore/Controllers/ProjectionNotificationController.cs`, `src/Hexalith.EventStore.Server/Projections/DaprProjectionChangeNotifier.cs` — body-driven ETag/broadcast and topic-producing publisher; bind publisher/topic/tenant first.
- `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs`, `src/Hexalith.EventStore/Queries/DaprDomainQueryInvoker.cs` — outbound proof; query invoker forwards bearer.
- `src/Hexalith.EventStore.Server/Commands/SubmitCommandExtensions.cs`, `src/Hexalith.EventStore.Contracts/Queries/QueryEnvelope.cs`, gateway controllers, `references/Hexalith.Tenants/src/Hexalith.Tenants.Server/` — wire flags reach active Tenants decisions; replace their authority with verified context in the owning repositories.
- `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs`, `tests/Hexalith.EventStore.Server.Tests/Authentication/DaprInternalAuthenticationHandlerTests.cs`, `tests/Hexalith.EventStore.Server.Tests/Integration/ETagActorIntegrationTests.cs` — route, credential, and callback suites currently accept unsecured positives.

## Tasks & Acceptance

**Execution:**
- [ ] `src/Hexalith.EventStore.ServiceDefaults/`, `src/Hexalith.EventStore/Authentication/`, `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs` — add shared credential validation, startup checks, and operation policy; remove header-only admin mint.
- [ ] `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs` and `EventStoreDomainEventsEndpointExtensions.cs` — protect every SDK operational, subscription, and effective host-override route; retain only the three anonymous probes.
- [ ] `src/Hexalith.EventStore/Controllers/ProjectionNotificationController.cs` and `src/Hexalith.EventStore.Server/Projections/DaprProjectionChangeNotifier.cs` — bind authenticated publisher, topic, and tenant before freshness or SignalR effects.
- [ ] `src/Hexalith.EventStore.Server/Commands/SubmitCommandExtensions.cs`, `src/Hexalith.EventStore.Contracts/Queries/QueryEnvelope.cs`, `references/Hexalith.Tenants/src/Hexalith.Tenants.Server/Aggregates/TenantAggregate.cs`, `references/Hexalith.Tenants/src/Hexalith.Tenants/Queries/Handlers/TenantQueryHandlerBase.cs` — replace wire-admin authority with verified delegation; retain authorized behavior.
- [ ] `src/Hexalith.EventStore.AppHost/Program.cs` and the three test files in Code Map — wire runtime secrets and prove real-pipeline zero-effect denials, valid flows, and route inventory; extend Tenants tests at their owning paths.

**Acceptance Criteria:**
- Given forged headers or admin flags, when a protected route runs, then no grants or downstream effects occur.
- Given valid scoped workload or human delegation, when an allowed route runs, then only its authorized operation/tenant executes with attribution.
- Given all domain-service routes and overrides, when metadata is enumerated, then non-probes require credentials and only three probes are anonymous.
- Given absent, wrong, stale, duplicate, or topic-mismatched callback proof, when delivered, then ETag and SignalR do not run.
- Given verifier failure, when a protected request arrives, then bounded denial and safe reason/correlation telemetry result.
- Given real-host and integration suites and Release build, when run, then required gates pass without weakening Admin or probes.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Intent gap resolved: use the existing trusted JWT issuer. Irreversibles: none. Footprint: shared auth API, hosts, transports, tests, and Tenants. `APP_API_TOKEN` proves only the sidecar channel.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj --configuration Release` — expected: route/pipeline gates pass.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release` — expected: credential/callback/wire tests pass.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` — expected: succeeds without warnings.
