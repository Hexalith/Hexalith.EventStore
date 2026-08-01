---
title: 'Prevent no-sidecar test hosts from retrying Dapr'
type: 'bugfix'
created: '2026-08-01'
status: 'done'
review_loop_iteration: 0
baseline_commit: '77d6f47743453d542d96dbe088d5eef7cd05284b'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-22-ci-release-retier.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** In-process `WebApplicationFactory` tests that explicitly run without a Dapr sidecar still start `AdminOperationalIndexHostedService`. Each new host performs four sequential metadata calls through the standard resilience pipeline, adding roughly 30 seconds per host and about four minutes to the blocking CI test lane.

**Approach:** Centralize the proven OpenAPI-factory override as a test-only helper and apply it to every no-sidecar factory in `Hexalith.EventStore.Server.Tests`. Remove only the hosted-service lifecycle alias so the concrete operational-index service and `INamedProjectionCatalogRefresher` remain available for request-path tests.

## Boundaries & Constraints

**Always:** Keep the change inside the Server test project; preserve production registrations and behavior; remove exactly the `IHostedService` descriptor that resolves `AdminOperationalIndexHostedService`; preserve unrelated hosted services; use the shared helper in OpenAPI, actor-authorization, SignalR-enabled, SignalR-disabled, query-provenance-derived, and ETag test hosts; add deterministic structural regression coverage; retain existing endpoint behavior.

**Ask First:** Any production configuration or DI-registration change; any CI workflow restructuring; changing the assembly-wide test parallelization policy; expanding the fix to other test projects or live-sidecar lanes.

**Never:** Initialize Dapr in the unit-test lane; remove all `IHostedService` registrations; suppress or shorten production resilience policies; use stopwatch thresholds as acceptance tests; replace endpoint assertions with DI-only tests.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| No-sidecar host startup | Production `AddEventStore` registrations with no Dapr endpoint | Operational-index startup alias is absent; host endpoint behavior remains functional | No retry wait against `localhost:3500` |
| Other hosted services | Authorization validator, rate-limit sync, or sentinel hosted registration | Registrations remain unchanged | Structural test fails if an unrelated descriptor is removed |
| Operational-index consumers | Concrete service and refresher alias are requested after override | Both non-hosted registrations remain registered | Structural test fails if either is removed |
| Registration layout drift | Expected concrete/alias/hosted registration sequence changes | Helper fails fast instead of removing an arbitrary service | `Single` produces an actionable test/configuration failure |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs` -- production three-registration sequence that the test override must preserve except for lifecycle startup.
- `tests/Hexalith.EventStore.Server.Tests/OpenApi/OpenApiWebApplicationFactory.cs` -- contains the existing proven private removal logic to extract.
- `tests/Hexalith.EventStore.Server.Tests/Integration/*WebApplicationFactory.cs` -- no-sidecar actor and SignalR hosts currently paying Dapr retry delays.
- `tests/Hexalith.EventStore.Server.Tests/Integration/ETagActorIntegrationTests.cs` -- contains the nested ETag factory requiring the same override.
- `tests/Hexalith.EventStore.Server.Tests/TestUtilities/` -- location for the shared helper and its structural regression test.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.Server.Tests/TestUtilities/WebApplicationFactoryServiceOverrides.cs` -- extract a fail-fast helper that removes only the operational-index hosted alias.
- [x] `tests/Hexalith.EventStore.Server.Tests/TestUtilities/WebApplicationFactoryServiceOverridesTests.cs` -- exercise real `AddEventStore` descriptors and prove unrelated/concrete/refresher registrations survive.
- [x] `tests/Hexalith.EventStore.Server.Tests/OpenApi/OpenApiWebApplicationFactory.cs` -- replace the private duplicate with the shared helper.
- [x] `tests/Hexalith.EventStore.Server.Tests/Integration/ActorBasedAuthWebApplicationFactory.cs` -- suppress operational-index startup for actor authorization and derived query hosts.
- [x] `tests/Hexalith.EventStore.Server.Tests/Integration/SignalRHubWebApplicationFactory.cs` and `SignalRDisabledWebApplicationFactory.cs` -- suppress startup in both SignalR modes.
- [x] `tests/Hexalith.EventStore.Server.Tests/Integration/ETagActorIntegrationTests.cs` -- apply the override to the nested ETag host.

**Acceptance Criteria:**
- Given any changed no-sidecar factory, when its first client starts the application, then no operational-index metadata retry cycle is initiated and the existing HTTP assertion passes.
- Given the production service collection, when the shared override runs, then exactly one hosted descriptor is removed while the concrete service, refresher alias, and unrelated hosted descriptors remain.
- Given registration-shape drift, when the override cannot uniquely identify the targeted alias, then configuration fails immediately rather than silently disabling another service.
- Given the previously 30-second SignalR test, when run from the built test assembly, then it passes without Dapr connection-retry logs and its measured runtime shows the retry penalty is gone.

## Spec Change Log

## Design Notes

The helper intentionally centralizes the OpenAPI factory's existing descriptor-neighbor strategy. That strategy is narrow and fail-fast, unlike `RemoveAll<IHostedService>()`, while avoiding production API or package dependency changes solely for test composition.

## Verification

**Commands:**
- `dotnet restore tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -p:Configuration=Debug -p:UseHexalithProjectReferences=false` -- expected: package-mode restore succeeds.
- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --no-restore --configuration Debug -warnaserror -p:UseHexalithProjectReferences=false` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -noColor -noLogo -class Hexalith.EventStore.Server.Tests.TestUtilities.WebApplicationFactoryServiceOverridesTests` -- expected: structural and final-host-composition regressions pass.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -noColor -noLogo -method Hexalith.EventStore.Server.Tests.Integration.SignalRHubEndpointTests.NegotiateEndpoint_WhenSignalREnabled_RejectsAnonymousRequest` -- expected: the former 30-second startup case passes without operational-index retry output.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -noColor -noLogo` -- expected: the complete Server test assembly passes.
- Capture each xUnit command's output and run `rg 'admin/operational-index-metadata|Operational index metadata unavailable' <captured-log>` -- expected: no matches.

**Results (2026-08-01):** Package-mode restore succeeded. The warning-as-error Debug build completed with zero warnings and errors. The final full assembly run completed 2,901 tests in 16.06 seconds (2,876 passed, 25 skipped) with zero operational-index retry matches. The focused SignalR case completed in 0.75 seconds of test time / 1.18 seconds wall time, down from 33.63 seconds before the fix, with zero retry matches.

## Suggested Review Order

**Targeted lifecycle suppression**

- Validate both aliases resolve the intended singleton before removing only the hosted alias.
  [`WebApplicationFactoryServiceOverrides.cs:19`](../../tests/Hexalith.EventStore.Server.Tests/TestUtilities/WebApplicationFactoryServiceOverrides.cs#L19)

**No-sidecar host wiring**

- Cover actor authorization and its derived query host through their shared factory.
  [`ActorBasedAuthWebApplicationFactory.cs:57`](../../tests/Hexalith.EventStore.Server.Tests/Integration/ActorBasedAuthWebApplicationFactory.cs#L57)

- Suppress startup for both enabled and disabled SignalR compositions.
  [`SignalRHubWebApplicationFactory.cs:23`](../../tests/Hexalith.EventStore.Server.Tests/Integration/SignalRHubWebApplicationFactory.cs#L23)

- Preserve the disabled SignalR endpoint behavior without Dapr retries.
  [`SignalRDisabledWebApplicationFactory.cs:23`](../../tests/Hexalith.EventStore.Server.Tests/Integration/SignalRDisabledWebApplicationFactory.cs#L23)

- Apply the same lifecycle-only override to the nested ETag host.
  [`ETagActorIntegrationTests.cs:506`](../../tests/Hexalith.EventStore.Server.Tests/Integration/ETagActorIntegrationTests.cs#L506)

- Replace the OpenAPI factory's private implementation with the hardened shared helper.
  [`OpenApiWebApplicationFactory.cs:37`](../../tests/Hexalith.EventStore.Server.Tests/OpenApi/OpenApiWebApplicationFactory.cs#L37)

**Regression guardrails**

- Prove real registrations retain concrete, refresher, and unrelated hosted services.
  [`WebApplicationFactoryServiceOverridesTests.cs:26`](../../tests/Hexalith.EventStore.Server.Tests/TestUtilities/WebApplicationFactoryServiceOverridesTests.cs#L26)

- Fail without mutation when an adjacent factory-backed decoy appears.
  [`WebApplicationFactoryServiceOverridesTests.cs:72`](../../tests/Hexalith.EventStore.Server.Tests/TestUtilities/WebApplicationFactoryServiceOverridesTests.cs#L72)

- Verify every final no-sidecar host composition excludes operational-index startup.
  [`WebApplicationFactoryServiceOverridesTests.cs:129`](../../tests/Hexalith.EventStore.Server.Tests/TestUtilities/WebApplicationFactoryServiceOverridesTests.cs#L129)
