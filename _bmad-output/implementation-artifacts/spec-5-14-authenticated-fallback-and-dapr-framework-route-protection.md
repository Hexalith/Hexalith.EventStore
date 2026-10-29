---
title: '5.14 Authenticated fallback and Dapr framework-route protection'
type: 'feature'
created: '2026-10-29'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
story_id: '5.14'
story_key: '5-14-authenticated-fallback-and-dapr-framework-route-protection'
planning_baseline: '5945ba8b3575f76597075e460bb9a973453272ea'
baseline_commit: '5945ba8b3575f76597075e460bb9a973453272ea'
context:
  - _bmad-output/implementation-artifacts/epic-5-context.md
  - _bmad-output/project-context.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Hosts admit unclassified HTTP endpoints; Operations trusts caller headers and unvalidated bearers. Channel authentication alone cannot stop peer actor invocation.

**Approach:** Share strict endpoint classification, named scheme-bound policies and denying fallback. Protect framework deliveries and independently validate Operations credentials before effects.

**Scope decision (2026-10-29, user: "apply recommended"):** Implement EventStore-owned host policies and Operations credential validation first. Actor execution-context/protocol qualification, resumable audit and cross-owner conformance remain explicit parent-story blockers. This milestone does not establish peer-actor safety or complete Story 5.14.

**Issuer decision (2026-10-29, user: "apply recommended"):** Reject enabling the embedded workload authority on conforming hosts. Retain its bootstrap only in explicitly nonconforming Development fixtures; those fixtures grant no host conformance or production authority. Add no anonymous issuer exception.

## Boundaries & Constraints

**Always:** Deny unclassified endpoints even with valid credentials. Channel identity never satisfies fallback/human policies. Require actual channel tokens, including Development. Preserve catalog/tenant guards, attribution and SDK header ownership. Each operation admits one kind; existing Operations relay routes remain human-only. Anonymous probes match exactly.

**Never:** Invent trust or anonymous exceptions; change capture/acknowledgment semantics, dependencies or submodules; claim unproven readiness/completion. UI conformance awaits reconciliation with settled PRD guidance; Tenants changes belong to its owner.

## I/O & Edge-Case Matrix

| Scenario | Input | Behavior | Failure boundary |
| --- | --- | --- | --- |
| Unclassified route | Anonymous, human, workload or channel caller | Deny; inventory fails | Before handler |
| Framework delivery | Valid receiver token | Named channel policy admits | Missing/duplicate/wrong token denies before activation |
| Operations relay | Valid human, permitted operation and tenant | Preserve validated identity | Forged caller, invalid/mixed/wrong-kind credentials deny before state |
| Probes | Exact health/alive/ready route | Anonymous, support-safe response | Similar prefixes get no exemption |
| New endpoint | Missing policy or effective scheme | Inventory fails | Never silently classified |

</frozen-after-approval>

## Code Map

Prefixes: `S=src/Hexalith.EventStore`, `D=S.ServiceDefaults`, `O=S.Operations`, `SDK=S.DomainService`; suffixes expand the project name.

- `S/Extensions/ServiceCollectionExtensions.cs`: human DefaultPolicy, no fallback. `S/Program.cs`: actor policy exists; subscribe unnamed; Swagger uses middleware.
- `D/Authentication/EventStoreWorkloadAuthenticationExtensions.cs`: reuse policies; `DaprAppChannelToken.cs` admits tokenless Development; `WorkloadBindingAuthorityExtensions.cs` maps anonymous bootstrap.
- `SDK/EventStoreDomainServiceEndpointInventory.cs`: permits fallback-only/public routes. Reuse startup validation and framework anonymous-metadata removal.
- `O/Endpoints/DeadLetterOperationsEndpointExtensions.cs`: insecure `IsAuthorized`; `O/Actors/IDeadLetterDrainActor.cs`: unguarded protocol. Admin dead-letter services already forward bearer.

## Tasks & Acceptance

**Execution:**
- [ ] `D/Authentication/EventStoreHttpHostSecurityExtensions.cs` and `EventStoreHttpEndpointInventory.cs` — add named human policy, denying fallback, resolved-scheme inventory and startup validation. Tighten shared channel authentication.
- [ ] `D/Authentication/WorkloadBindingAuthorityExtensions.cs` and shared host validation — reject embedded issuer activation on conforming hosts; preserve bootstrap only in explicitly nonconforming Development fixtures, with startup and bootstrap controls.
- [ ] `S/Program.cs`, controller authorization attributes, `S/OpenApi/{ErrorReferenceEndpoints,ApiVersionFallbackEndpoints}.cs` and SignalR mappings — classify existing surfaces, protect framework routes and Swagger middleware.
- [ ] `S.Admin.Server.Host/Program.cs`, `S.Admin.Server/Configuration/ServiceCollectionExtensions.cs`, `samples/Hexalith.EventStore.Sample.Api/Program.cs`, `S.RestApi.Generators/RestApiControllerEmitter.cs` — adopt fallback/named scheme-bound policies.
- [ ] `SDK/{EventStoreDomainServiceSecurityExtensions,EventStoreDomainServiceEndpointInventory}.cs` — tighten classification; retain canonical POST/catalog guards.
- [ ] `O/Program.cs`, `O/Endpoints/DeadLetterOperationsEndpointExtensions.cs`, `O/Security/` — register shared authentication; enforce human operation/tenant/channel checks and validated downstream attribution. Remove caller-header authorization; preserve Admin relay and role mapping.
- [ ] `tests/Hexalith.EventStore.{ServiceDefaults,Server,Admin.Server.Host,DomainService,Operations,Sample,RestApi.Generators}.Tests/HttpHostSecurityTests.cs` — exercise the matrix through real pipelines, late endpoints and mapping order. Preserve existing assertions using authenticated fixtures.
- [ ] `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/HttpHostSecurityLiveTests.cs` — test framework routes with owned infrastructure/persisted state; report unqualified actor-peer paths separately.

**Acceptance Criteria:**
- Given each scoped host, when metadata is enumerated, then protected endpoints name policies/resolved schemes and only exact probes are anonymous.
- Given rejected traffic, when real pipelines execute, then downstream work is zero and persisted state unchanged; positive controls succeed.
- Given open external/actor/audit prerequisites, when reporting results, then preserve their failure status and incomplete parent story.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

Build projects individually: `dotnet build <project> -c Debug -p:UseHexalithProjectReferences=true -m:1`. Run built xUnit assemblies with `-class`, then affected suites/live controls. Record commands/counts, SHA and CI link/blocker. Run `git diff --check` and `bash scripts/check-committed-files.sh`. Start/inspect Aspire before code edits. Planning has run no tests/runtime.
