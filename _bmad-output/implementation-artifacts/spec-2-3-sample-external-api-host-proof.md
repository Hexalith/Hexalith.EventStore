---
title: '2.3 Sample External API Host Proof'
type: 'feature'
created: '2026-07-07'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: true
baseline_revision: '86c2f7b9554e9b10a7d374d8ff2862dc57eb13b0'
final_revision: 'c9d340d80b1a5cb7ad360be1de6f4aa63fd1a1d3'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-2-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-2-2-rest-api-generator-discovery-and-controller-emission.md'
warnings: []
---

<intent-contract>

## Intent

**Problem:** The Sample split already exists, but Story 2.3 still needs a hardened proof that generated REST controllers are hosted only by the dedicated `Sample.Api` host while the domain service and Blazor UI share the same contracts without becoming API hosts.

**Approach:** Add focused compile/runtime proof around the actual `Sample.Api` assembly, tighten host/topology guardrails, and patch small startup robustness that would make generated-host smoke failures opaque.

## Boundaries & Constraints

**Always:** Keep `Sample.Contracts` contracts-only and referenced by the domain service, `Sample.Api`, and `Sample.BlazorUI`; keep generated controllers in `Sample.Api`; generated actions must delegate through `IEventStoreGatewayClient`; use ULID command ids through generated code; preserve `.ConfigureAwait(false)` on awaited production calls.

**Block If:** Proving the story requires changing generator public semantics, exposing Greeting externally, replacing the gateway boundary, modifying submodule files, or running a live Aspire smoke without placement/scheduler/Docker prerequisites.

**Never:** Do not add generated or hand-written MVC command/query controllers to `Sample.BlazorUI`; do not make `Sample.Api` reference the domain implementation project; do not bypass EventStore gateway auth/status/archive/query metadata behavior; do not parse cursors/ETags/JWT payloads; do not add package versions to project files.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Sample API query | Generated `GET /api/{tenant}/counter/{entityId}` receives gateway `200` with ETag and metadata | Returns raw payload and forwards bounded `ETag` plus `X-Hexalith-*` metadata headers from the gateway result | Missing metadata omits headers without fabricating freshness/projection evidence |
| Sample API `304` | Generated query receives `If-None-Match` and gateway not-modified result with a strong ETag | Returns empty `304` and preserves the strong `ETag` | Invalid or absent strong ETag follows Story 2.2 generated problem behavior |
| Sample API command | Generated `POST /api/{tenant}/counter/{counterId}/increment` receives matching route/body counter id | Calls `SubmitCommandAsync` once with tenant, domain `counter`, aggregate id, command type, and payload; returns `202`, `Retry-After`, and command status `Location` | Null body or route/body mismatch returns safe `400` before gateway call |
| Boundary regression | UI or domain host gains REST generator/controller wiring, or API host references the domain implementation project | Guardrail tests fail before runtime smoke | No generated API exposure is accepted outside `Sample.Api` |

</intent-contract>

## Code Map

- `samples/Hexalith.EventStore.Sample.Contracts/` -- shared Counter command/query contracts and `RestRoute(ApiScope = "counter")` metadata.
- `samples/Hexalith.EventStore.Sample/Hexalith.EventStore.Sample.csproj` -- domain service host reference boundary: DomainService plus contracts only.
- `samples/Hexalith.EventStore.Sample.Api/` -- dedicated external generated REST API host, auth, Dapr gateway handlers, appsettings, launch settings.
- `samples/Hexalith.EventStore.Sample.BlazorUI/` -- interactive UI host that must remain a client-library consumer with no MVC generated API surface.
- `src/Hexalith.EventStore.AppHost/Program.cs` and `DaprComponents/accesscontrol.yaml` -- Aspire resource and Dapr service-invocation ACL for `sample-api`.
- `tests/Hexalith.EventStore.Sample.Tests/` -- Sample contract, UI boundary, and new actual API generated-controller proof.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/` -- topology/configuration guardrails for launch settings and Dapr ACL.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj` -- reference `samples/Hexalith.EventStore.Sample.Api` for test-time generated controller reflection -- proves the real host assembly, not a synthetic generator fixture.
- [x] `tests/Hexalith.EventStore.Sample.Tests/SampleApi/` -- add fake gateway plus runtime tests invoking the compiled generated `CounterRestController` for query `200`, query `304`, command `202`, null/mismatched command `400`, and gateway request metadata -- covers the I/O matrix through the actual external host.
- [x] `tests/Hexalith.EventStore.Sample.Tests/SampleApi/` -- add structural tests for `Sample.Api` project references, `[assembly: RestApi("api/{tenant}/counter", "counter", RestTenantSource.Route)]`, no Razor/UI concerns, no domain implementation reference, and expected generated routes/authorize metadata -- protects FR13/FR14 boundaries.
- [x] `tests/Hexalith.EventStore.Sample.Tests/BlazorUI/` and `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` -- strengthen existing scans only where gaps remain for compile-linked contracts, generator analyzer references, `AddControllers`, `MapControllers`, and domain-host API leakage -- prevents the old UI-host design from returning.
- [x] `samples/Hexalith.EventStore.Sample.Api/Program.cs` and `samples/Hexalith.EventStore.Sample.BlazorUI/Program.cs` -- normalize blank `DAPR_HTTP_ENDPOINT` / `DAPR_HTTP_PORT` to the `3500` fallback -- avoids opaque startup failures in local generated-host smoke runs.
- [x] `tests/Hexalith.EventStore.AppHost.Tests/Configuration/` -- guard `sample-api` launch/topology facts and Dapr ACL scope for only EventStore command/query service invocation -- keeps external host routing narrow.

**Acceptance Criteria:**
- Given Sample Counter contracts are compiled, when Sample tests inspect the project graph, then the domain service, `Sample.Api`, and `Sample.BlazorUI` all reference `Sample.Contracts` and no UI contract file is compile-linked as a duplicate type.
- Given the compiled `Sample.Api` assembly is loaded, when the generated controller is reflected, then it exposes the Counter query and command routes under `api/{tenant}/counter`, requires authorization, and is constructed only with `IEventStoreGatewayClient`.
- Given generated Sample query and command actions are invoked with a fake gateway, when gateway responses include success, not-modified, metadata, null body, or route/body mismatch, then the action outcomes match the I/O matrix and gateway calls are captured exactly once only for valid requests.
- Given Sample Blazor UI source is scanned, when the guardrails run, then it contains no `[assembly: RestApi]`, REST generator analyzer reference, controller mapping, or MVC command/query controller surface.
- Given Release build and focused tests run, when no live Aspire smoke is available, then the result records the exact blocker and still proves compile/runtime generated-host behavior through unit-level controller invocation.

## Spec Change Log

## Review Triage Log

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 10: (high 0, medium 6, low 4)
- defer: 1: (high 0, medium 1, low 0)
- reject: 3: (high 0, medium 1, low 2)
- addressed_findings:
  - `[low]` `[patch]` Blank DAPR endpoint handling still allowed malformed nonblank endpoint/port values to fail later with raw URI errors. Patched Sample API and Blazor UI DAPR endpoint resolution to fail with explicit configuration errors.
  - `[low]` `[patch]` Sample API DAPR ACL guardrail used raw substring matching and an overbroad "only" claim. Patched it to assert exact per-operation POST verbs and to describe the policy's scoped operation intent.
  - `[medium]` `[patch]` Sample API structural proof inspected only one command action and did not prove there was only one generated controller. Patched it to assert `CounterRestController` is the only generated controller and to check all Counter command route methods.
  - `[medium]` `[patch]` Generated action proof did not verify route/header/body binding metadata. Patched structural tests to assert `FromRoute`, `FromBody`, and `FromHeader("If-None-Match")` metadata on the compiled generated controller.
  - `[medium]` `[patch]` Generated command runtime proof accepted any nonblank message id. Patched it to parse the generated command id with `UniqueIdHelper.ExtractTimestamp`.
  - `[medium]` `[patch]` Runtime proof did not exercise gateway failure mapping on the actual Sample API generated controller. Added a forbidden gateway exception test covering safe `ProblemDetails` mapping.
  - `[medium]` `[patch]` Runtime proof bypassed the registered bearer-forwarding and DAPR app-id handlers entirely. Added handler behavior tests and structural registration assertions for the Sample API gateway client chain.
  - `[medium]` `[patch]` Domain-service guardrail only checked project-reference generator leakage. Patched it to reject any REST generator reference string in the Sample domain project file, including package/analyzer forms.
  - `[low]` `[patch]` Blazor UI analyzer guardrail rejected any analyzer-shaped project reference instead of only the REST generator. Patched it to inspect project/package references specifically for `Hexalith.EventStore.RestApi.Generators`.
  - `[low]` `[patch]` Blazor UI controller-surface guardrail missed alternate MVC/controller mapping markers. Patched it to cover `AddMvc`, `AddMvcCore`, controller-route mapping, and controller inheritance variants.

### 2026-07-07 — Follow-up review pass
- intent_gap: 0
- bad_spec: 0
- patch: 14: (high 0, medium 9, low 5)
- defer: 0
- reject: 3: (high 0, medium 0, low 3)
- addressed_findings:
  - `[low]` `[patch]` DAPR HTTP port validation accepted padded or signed values but returned the raw string. Patched Sample API and Blazor UI endpoint resolution to trim, parse with invariant digits-only rules, and emit a normalized port.
  - `[medium]` `[patch]` DAPR HTTP endpoint validation allowed origin values with path, query, or fragment. Patched Sample API and Blazor UI endpoint resolution to require an HTTP/HTTPS origin URI and return the normalized authority.
  - `[medium]` `[patch]` Runtime controller proof could pass even if `Sample.Api` stopped registering or mapping MVC controllers. Patched structural tests to assert `AddControllers` and `MapControllers` remain present in the external API host.
  - `[medium]` `[patch]` AppHost guardrail stopped before the security branch and did not protect `sampleApi.WithEventStoreClientCredentials(security)`. Patched the test to assert the credential wiring.
  - `[medium]` `[patch]` DAPR access-control proof ignored operation `action`, so denied operations could pass. Patched the parser to assert exact `POST` operations with `action: allow`.
  - `[medium]` `[patch]` DAPR access-control proof allowed duplicate `sample-api` policy blocks to broaden access. Patched it to require exactly one `sample-api` policy.
  - `[medium]` `[patch]` Sample domain-service guardrail missed alternate MVC/controller mapping markers. Patched it to reuse the broader generated-API host marker set.
  - `[low]` `[patch]` Blazor UI REST generator guard missed direct `<Analyzer>` items. Patched it to inspect analyzer items in addition to project/package references.
  - `[medium]` `[patch]` Sample API structural proof counted only generated-namespace controllers, so a hand-written controller elsewhere in the API assembly could escape. Patched it to assert the complete API controller set is only the generated `CounterRestController`.
  - `[low]` `[patch]` Generated command runtime proof used non-ULID-like fake status IDs. Patched it to use generated ULIDs and parse the returned correlation/status ID.
  - `[medium]` `[patch]` Runtime proof exercised only the increment command path. Added runtime coverage for decrement, reset, and close command actions through the compiled generated controller.
  - `[medium]` `[patch]` Runtime query proof did not cover success without gateway metadata. Added a real-controller test proving freshness, paging, warning, and ETag headers are omitted rather than fabricated.
  - `[medium]` `[patch]` Runtime query proof did not cover not-modified responses without a strong ETag. Added a real-controller test proving the generated action returns safe `502` problem details and no stale metadata headers.
  - `[low]` `[patch]` Domain/UI controller-surface scans could miss whitespace-qualified syntax. Patched Sample domain/UI guardrails to use regex marker checks for REST API attributes, MVC registration, controller mapping, and controller inheritance.

### 2026-07-07 — Follow-up review pass
- intent_gap: 0
- bad_spec: 0
- patch: 14: (high 0, medium 11, low 3)
- defer: 1: (high 0, medium 1, low 0)
- reject: 2: (high 0, medium 0, low 2)
- addressed_findings:
  - `[medium]` `[patch]` DAPR endpoint parsing was production code but only source-token tested. Extracted explicit Sample API and Blazor UI resolver helpers and added behavior tests for default fallback, origin normalization, invalid endpoint rejection, normalized port fallback, and malformed/out-of-range ports.
  - `[medium]` `[patch]` Generated-host proof did not prove auth middleware was actually wired. Added an in-process Sample API host test that unauthenticated generated routes return `401`, and strengthened source-order assertions for `UseAuthentication`, `UseAuthorization`, and `MapControllers`.
  - `[medium]` `[patch]` Gateway handler tests exercised handlers separately but not the typed gateway client chain. Added a real `AddEventStoreGatewayClient` registration test with the Sample API forwarding and DAPR app-id handlers plus a captured primary handler.
  - `[medium]` `[patch]` DAPR ACL operation parsing overwrote duplicate operation names and could mask broader rules. Patched the parser to fail on duplicate operation names and normalize/require exactly `POST` verbs.
  - `[medium]` `[patch]` Domain-service REST API guardrail missed `[assembly: RestApiAttribute(...)]` declarations. Patched the regex to cover `RestApiAttribute` and `global::`-qualified forms.
  - `[medium]` `[patch]` Blazor UI REST API guardrail missed `[assembly: RestApiAttribute(...)]` declarations. Patched the regex to cover `RestApiAttribute` and `global::`-qualified forms.
  - `[medium]` `[patch]` Not-modified query coverage exercised weak ETags but not absent ETags. Added a compiled generated-controller test proving absent ETags also return safe `502` problem details without stale metadata headers.
  - `[medium]` `[patch]` Generated command bad-request coverage only covered increment. Added null-body and route/body mismatch coverage for increment, decrement, reset, and close command actions.
  - `[medium]` `[patch]` Generated command gateway exception mapping was not covered. Added a compiled generated-controller command failure test proving safe `ProblemDetails` mapping for forbidden gateway responses.
  - `[low]` `[patch]` The fake gateway did not record cancellation tokens, so generated controller token propagation was unpinned. Patched the fake and assertions to capture query and command cancellation tokens.
  - `[medium]` `[patch]` The Sample API controller-only proof missed hand-written minimal endpoints. Added a source guard rejecting `app.MapGet`, `MapPost`, `MapPut`, `MapDelete`, `MapPatch`, `MapGroup`, and `MapMethods` in `Sample.Api`.
  - `[medium]` `[patch]` The Sample API project boundary guard checked project references only. Patched it to reject domain implementation or UI host dependencies through project, package, or assembly references.
  - `[low]` `[patch]` The DAPR ACL test name and wording overstated mTLS caller enforcement in self-hosted mode. Renamed the guardrail to document the nested `sample-api` POST operation policy it can actually prove.
  - `[low]` `[patch]` The run result did not record the concrete live Aspire/DAPR smoke blocker. Final result now records that no live topology was started and placement/scheduler/Docker readiness was not established for this review pass.

## Design Notes

The main implementation risk is treating this as a new scaffold story. The scaffold exists; the remaining proof should exercise the compiled `Sample.Api` generated controller directly so the story validates real host wiring while staying Tier 1 and independent of Docker/Aspire availability. Live state-store smoke remains valuable, but it is not required to replace the unit-level generated-host proof in this run.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/` -- expected: contract, UI boundary, and actual Sample API generated-controller proof pass.
- `dotnet test tests/Hexalith.EventStore.AppHost.Tests/` -- expected: Sample API launch/topology/ACL guardrails pass.
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/` -- expected: domain-centric and UI-host guardrails remain green.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false` -- expected: Release package-mode build passes with zero warnings.
- `git diff --check` -- expected: no whitespace errors.

## Auto Run Result

Status: done

Summary:
- Proved the real compiled `Hexalith.EventStore.Sample.Api` generated `CounterRestController` through runtime invocation tests for query success metadata, query `304`, command acceptance, local bad requests, and gateway failure mapping.
- This follow-up review extracted and behavior-tested Sample API/Blazor UI DAPR endpoint normalization, added a WebApplicationFactory auth-middleware proof, and exercised the real typed gateway client registration chain with bearer forwarding and DAPR routing headers.
- Added Sample API structural guardrails for contracts/client/service-defaults/generator references, REST API assembly opt-in, generated controller metadata, all Counter command routes, route/header/body binding metadata, gateway-only construction, minimal endpoint exclusion, and handler registration.
- Strengthened Sample Blazor UI and Sample domain-service boundary checks so generated REST analyzers/controllers cannot move back into interactive UI or domain-host projects.
- Hardened Sample API and Blazor UI DAPR sidecar endpoint resolution so blank values fall back to port `3500` and malformed nonblank values fail with explicit configuration errors.
- Tightened AppHost/DAPR access-control guardrails for the `sample-api` resource. One new deferred-work entry was appended for pre-existing DAPR header replacement semantics.

Files changed:
- `samples/Hexalith.EventStore.Sample.Api/Program.cs` -- uses the extracted DAPR endpoint resolver and exposes `Program` for in-process host tests.
- `samples/Hexalith.EventStore.Sample.Api/Services/DaprHttpEndpointResolver.cs` -- Sample API DAPR endpoint fallback/validation helper.
- `samples/Hexalith.EventStore.Sample.BlazorUI/Program.cs` -- uses the extracted DAPR endpoint resolver.
- `samples/Hexalith.EventStore.Sample.BlazorUI/Services/DaprHttpEndpointResolver.cs` -- matching Blazor UI DAPR endpoint fallback/validation helper.
- `tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj` -- aliased test-time references to Sample API and Blazor UI plus ASP.NET Core test host support.
- `tests/Hexalith.EventStore.Sample.Tests/SampleApi/` -- resolver tests, host middleware test, runtime generated-controller tests, structural host tests, fake gateway, and gateway handler-chain tests.
- `tests/Hexalith.EventStore.Sample.Tests/BlazorUI/BlazorUiHostBoundaryTests.cs` -- UI host boundary guardrails.
- `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` -- Sample domain-service REST generator/API leakage guardrail.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/SampleApiLaunchSettingsTests.cs` -- `sample-api` AppHost and DAPR ACL guardrails.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- appended one new deferred DAPR header replacement policy item.
- `_bmad-output/implementation-artifacts/spec-2-3-sample-external-api-host-proof.md` -- story spec, task completion, review triage, and run result.

Review findings breakdown:
- Initial review pass patches applied: 10 total (0 high, 6 medium, 4 low).
- Follow-up review pass patches applied: 14 total (0 high, 9 medium, 5 low).
- Final follow-up review pass patches applied: 14 total (0 high, 11 medium, 3 low).
- Deferred in final follow-up review: 1 medium pre-existing DAPR header replacement finding appended as a new ledger entry only. Existing ledger entries were not modified or re-opened.
- Rejected in final follow-up review: 2 low findings for transient workflow frontmatter state and treating residual-risk/deferred-work tracking as frontmatter warnings.

Follow-up review recommendation: true. The final review pass materially expanded the proof surface across host middleware, endpoint configuration, generated command negative paths, typed gateway registration, and DAPR ACL parsing.

Verification performed:
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/` -- passed, 114 tests.
- `dotnet test tests/Hexalith.EventStore.AppHost.Tests/` -- passed, 44 tests.
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/` -- passed, 84 tests.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false` -- passed with 0 warnings and 0 errors.
- `git diff --check` -- passed.

Residual risks:
- Live Aspire/DAPR smoke was not run in this Tier 1 proof. The concrete blocker for this review pass is that no live Aspire/DAPR topology was started, and Docker plus DAPR placement/scheduler readiness was not established for a sidecar-backed smoke. Generated-host behavior is proven through compiled controller invocation, in-process auth middleware, typed-client handler-chain tests, and focused topology/configuration guardrails.
- Generated command success `Location` still depends on a broader generated API host status-route policy deferred outside this story.
- Sample DAPR app-id handlers still need a focused follow-up to replace rather than append any preexisting `dapr-app-id` / `dapr-api-token` request headers.
