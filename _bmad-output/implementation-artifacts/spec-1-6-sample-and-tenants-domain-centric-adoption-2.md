---
title: '1.6 Sample And Tenants Domain-Centric Adoption Follow-Up'
type: 'bugfix'
created: '2026-07-07T07:31:00+02:00'
status: 'done'
baseline_revision: '63f8acf00aa007135e0ad418cdb7d9fc0f084dd6'
final_revision: '3998e4222e90342925d2fded0fd09362bfa1429e'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-1-5-domain-module-hosting-observability.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-1-6-sample-and-tenants-domain-centric-adoption.md'
warnings: []
---

<intent-contract>

## Intent

**Problem:** Story 1.6 makes Tenants a real shared domain module with a `ready`-tagged DAPR state-store health check, but the public EventStore Aspire domain-module extension still points the DAPR sidecar app-health probe at `/ready`. Because that readiness check can depend on the same sidecar, a transient sidecar or store failure can feed back into DAPR traffic eligibility and stall service invocation or pub/sub delivery.

**Approach:** Keep DAPR app health enabled, but change the public `AddEventStoreDomainModule(...)` default app-health path from `/ready` to `/alive`. Leave `/ready` as the operator/readiness endpoint, keep Tenants' state-store readiness behavior unchanged, and prove lower-level callers can still explicitly opt into another app-health path.

## Boundaries & Constraints

**Always:** Preserve the SDK health endpoints `/health`, `/alive`, and `/ready`; keep `AddEventStoreDomainModule(...)` enabling DAPR app health by default; keep shared and isolated DAPR component behavior unchanged; keep Tenants' `AddEventStoreDomainStateStoreHealthCheck(..., tags: ["ready"])` semantics intact; use `Hexalith.EventStore.slnx` only for build/restore and per-project tests.

**Block If:** The fix requires changing DAPR access-control policy, adding health endpoint authorization decisions, removing Tenants readiness checks, disabling DAPR app health globally, or changing the lower-level `AspireDaprDomainModuleOptions` public contract.

**Never:** Do not edit the Tenants submodule for this fix; do not make `/ready` a liveness endpoint; do not remove `/ready` from ServiceDefaults; do not grant isolated Sample modules shared state-store or pub/sub references.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Shared module default | AppHost calls `AddEventStoreDomainModule(eventStore, "tenants")` | DAPR app health remains enabled and probes `/alive`; shared state-store/pub/sub references remain present | Existing shared-mode validation still rejects missing shared components |
| Isolated module default | AppHost calls `AddEventStoreDomainModule(..., isolatedDaprResourcesPath: path)` | DAPR app health remains enabled and probes `/alive`; no shared components are referenced | Existing isolated resource-path handling remains unchanged |
| Explicit lower-level override | Caller uses `AddAspireDaprDomainModule` with `AppHealthCheckPath = "/ready"` or another path | The sidecar options preserve the caller-supplied path | Invalid options continue to fail through existing validation |

</intent-contract>

## Code Map

- `src/Hexalith.EventStore.Aspire/HexalithEventStoreDomainModuleExtensions.cs` -- public Aspire domain-module facade that owns the default DAPR app-health path.
- `src/Hexalith.EventStore.Aspire/AspireDaprDomainModuleAspireExtensions.cs` -- lower-level option mapper that must preserve explicit `AppHealthCheckPath` values.
- `src/Hexalith.EventStore.Aspire/AspireDaprDomainModuleOptions.cs` -- public option contract to leave additive and unchanged.
- `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs` -- canonical `/alive` and `/ready` health endpoint mapping; expected to remain unchanged.
- `references/Hexalith.Tenants/src/Hexalith.Tenants/Program.cs` -- evidence that Tenants uses a `ready`-tagged state-store check; do not edit.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/DomainModuleAspireExtensionTests.cs` -- focused AppHost-safe coverage for shared, isolated, and explicit app-health path behavior.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.Aspire/HexalithEventStoreDomainModuleExtensions.cs` -- change the public domain-module default `AppHealthCheckPath` from `/ready` to `/alive` and update nearby comments/XML docs if they imply readiness gating -- avoids coupling DAPR traffic eligibility to sidecar-dependent readiness.
- [x] `tests/Hexalith.EventStore.AppHost.Tests/Configuration/DomainModuleAspireExtensionTests.cs` -- update shared and isolated default expectations to `/alive` while preserving component-reference assertions -- proves the public facade default is liveness-only.
- [x] `tests/Hexalith.EventStore.AppHost.Tests/Configuration/DomainModuleAspireExtensionTests.cs` -- add lower-level `AddAspireDaprDomainModule` coverage proving an explicit `/ready` path is still passed through -- preserves the advanced override behavior without changing public options.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- mark or remove the exact Story 1.5 deferred `/ready` DAPR app-health feedback-loop item after tests pass -- keeps the deferred-work ledger aligned with the implemented fix.

**Acceptance Criteria:**
- Given a shared domain module is added through the public EventStore Aspire extension, when the sidecar options are inspected, then app health is enabled at `/alive` and shared DAPR components are still referenced.
- Given an isolated Sample-style domain module is added through the public EventStore Aspire extension, when the sidecar options are inspected, then app health is enabled at `/alive` and no shared DAPR components are referenced.
- Given a caller uses the lower-level DAPR domain-module extension with an explicit app-health path, when the sidecar options are inspected, then that explicit path is preserved.
- Given Tenants keeps its `ready`-tagged state-store health check, when the public AppHost extension wires its sidecar, then the DAPR app-health probe no longer calls the sidecar-dependent `/ready` endpoint.

## Spec Change Log

## Review Triage Log

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 6: (high 0, medium 0, low 6)
- defer: 1: (high 0, medium 1, low 0)
- reject: 7: (high 0, medium 0, low 7)
- addressed_findings:
  - `[low]` `[patch]` New spec code map still described the Aspire facade as hard-coding `/ready`; updated the wording to describe ownership of the default app-health path after the implementation.
  - `[low]` `[patch]` Removing the old deferred `/ready` feedback-loop entry lost the remaining anonymous-access concern; added a narrower deferred-work entry for the future explicit anonymous health-endpoint contract while leaving the resolved readiness feedback loop removed.
  - `[low]` `[patch]` The new `/alive` default was a bare literal with no local rationale; introduced a named constant and a comment explaining why DAPR app health targets liveness rather than readiness.
  - `[low]` `[patch]` AppHost tests used the last sidecar annotation and could hide duplicate sidecar wiring; changed the helper to require exactly one DAPR sidecar annotation.
  - `[low]` `[patch]` Lower-level explicit app-health path coverage only used `/ready`; converted it to a theory covering `/ready` and an arbitrary custom path.
  - `[low]` `[patch]` Lower-level explicit app-health path coverage did not preserve shared component assertions; added the shared component reference assertion.
- deferred_findings:
  - `[medium]` Health endpoints currently allow anonymous calls under today's auth setup, but they do not declare explicit `AllowAnonymous()` metadata; a future global fallback authorization policy could block DAPR app-health probes. Recorded in `deferred-work.md` because this spec explicitly blocks new health authorization-policy decisions.

## Design Notes

`/alive` is the safer DAPR app-health default because DAPR app health controls whether the sidecar sends traffic to the app. `/ready` remains valuable for operators and orchestrators that intentionally want dependency-aware readiness, but using it as the sidecar's own app-health target can turn a sidecar dependency failure into a self-reinforcing traffic gate.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.EventStore.AppHost.Tests/` -- passed: 42/42.
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/` -- passed: 68/68.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter FullyQualifiedName~ReadinessEndpointTests` -- passed: 12/12.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- passed: 0 warnings, 0 errors.
- `git diff --check` -- passed: no whitespace errors.

## Auto Run Result

Status: done

Summary:
- Changed the public EventStore Aspire domain-module default DAPR app-health path from `/ready` to `/alive`, keeping app health enabled while avoiding sidecar-dependent readiness feedback loops.
- Preserved lower-level `AddAspireDaprDomainModule` explicit path behavior and hardened tests for shared/isolated sidecar wiring.
- Removed the resolved `/ready` feedback-loop deferred item and kept the separate anonymous-health-endpoint policy risk tracked for a focused future decision.

Files changed:
- `src/Hexalith.EventStore.Aspire/HexalithEventStoreDomainModuleExtensions.cs` -- defaulted domain-module DAPR app health to the named `/alive` path with local rationale.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/DomainModuleAspireExtensionTests.cs` -- updated public default expectations, added explicit path coverage, asserted shared references, and required exactly one sidecar annotation.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- replaced the resolved readiness feedback-loop deferred item with the remaining anonymous-access contract risk.
- `_bmad-output/implementation-artifacts/spec-1-6-sample-and-tenants-domain-centric-adoption-2.md` -- captured the follow-up spec, review triage, verification, and completion result.

Review findings breakdown:
- Patches applied: 6 low-severity review findings.
- Items deferred: 1 medium-severity finding, recorded in `deferred-work.md`.
- Items rejected: 7 low-severity findings outside this story's bounded Aspire-default scope or already covered by existing health endpoint tests.
- Follow-up review recommended: false.

Verification performed:
- `dotnet test tests/Hexalith.EventStore.AppHost.Tests/` -- passed: 42/42.
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/` -- passed: 68/68.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter FullyQualifiedName~ReadinessEndpointTests` -- passed: 12/12.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- passed: 0 warnings, 0 errors.
- `git diff --check` -- passed.

Residual risks:
- Health endpoints currently work anonymously under today's authorization setup, but there is no explicit `AllowAnonymous()` contract if a future global fallback policy is introduced. This remains deferred because the spec explicitly blocked new health authorization-policy decisions.
