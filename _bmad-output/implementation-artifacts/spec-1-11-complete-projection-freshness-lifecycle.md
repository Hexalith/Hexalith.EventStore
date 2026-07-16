---
title: '1.11 Complete Projection Freshness Lifecycle'
type: 'feature'
created: '2026-07-11'
status: 'done'
baseline_revision: 'f6aafb38e81969ab7ca04be484b60b857f0f7a86'
final_revision: '6aa29af6af13c76631c4122a96ddef8b3da47fb7'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/2-8-query-response-provenance-contract-and-route-aware-gateway-etag.md'
warnings: [oversized]
---

<intent-contract>

## Intent

**Problem:** Projection/query consumers currently receive `Unknown`, `Current`, `Aging`, and `Stale` age classifications plus nullable Booleans, which cannot losslessly express rebuilding, degraded, unavailable, or local-only operation and can tempt consumers to infer freshness from an ETag or transport success.

**Approach:** Add an additive, fail-safe projection lifecycle contract and carry it through the existing Story 2.8 provenance-aware query path. Keep legacy freshness fields compatible, expose an EventStore-owned default consumer policy, and prove persisted timestamp-to-client propagation without changing route selection.

## Boundaries & Constraints

**Always:** Preserve the existing eight-parameter `QueryResponseMetadata` constructor and the numeric values of `ReadModelFreshnessState`; add lifecycle as an init-only contract property. Use exact stable values `Unknown = 0`, `Current = 1`, `Stale = 2`, `Rebuilding = 3`, `Degraded = 4`, `Unavailable = 5`, and `LocalOnly = 6`. Only `ProjectionBacked` provenance may retain a non-unknown lifecycle. Map persisted `Current` and legacy `Aging` to lifecycle `Current`, persisted `Stale` to `Stale`, and missing evidence to `Unknown`. Project lifecycle one-way into legacy `IsStale`/`IsDegraded`; only an unknown lifecycle may retain the existing `IsStale` fallback for request-policy compatibility, and neither Boolean may create a lifecycle value. Treat the canonical header as `X-Hexalith-Projection-Lifecycle`; emit one exact bounded enum name and omit it for `Unknown` or non-projection routes. Only projection-backed `Current` is projection-confirmed and mutation-eligible by default.

**Block If:** Carrying lifecycle requires changing Story 2.8 route selection/provenance authority, breaking a released constructor/enum, or editing a root-declared consumer submodule. The Story 2.8 spec remains `in-review`; use its implemented provenance invariants, but do not alter its status or claim owner-review closure here.

**Never:** Infer lifecycle from ETag presence/value, HTTP success/304, payload fields, SignalR, `IsStale`, or `IsDegraded`; renumber/remove legacy `Aging`; add lifecycle members to `IReadModelFreshness`; fabricate operational state from a timestamp; modify Tenants/FrontComposer/Parties; or broaden into async projection dispatch, idempotency, rebuild correctness, or owner parity closure owned by Stories 1.12-1.15.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Canonical wire | Exact lifecycle string | Exact stable enum round-trip | No error expected |
| Unsafe wire | Missing, null, numeric, case-variant, object/array, unknown string/value | `Unknown`; unknown writes as `"Unknown"` | Consume safely without exception |
| Persisted freshness | Persisted timestamp classifies current/aging/stale/absent | `Current`/`Current`/`Stale`/`Unknown`; compatible `IsStale` false/false/true/null | No transport inference |
| Explicit operation | Projection producer has authoritative rebuild/dependency/storage/local-fallback evidence | Preserve explicit `Rebuilding`/`Degraded`/`Unavailable`/`LocalOnly` | Producer must choose from direct evidence; no multi-signal inference |
| Provenance gate | Handler, missing, invalid, or body/header-contradictory provenance/lifecycle | Lifecycle becomes `Unknown`; authoritative header omitted | Fail closed without losing served-at, warnings, paging, or unrelated degraded compatibility data |
| Conditional response | Projection-backed `200` or bodyless `304` | Body/client metadata and canonical header agree; `304` recovers lifecycle only from the authoritative header | Missing/invalid/duplicate/mismatched header becomes `Unknown` |
| Cached response | Cached metadata contains time-sensitive lifecycle | Cache hit clears lifecycle to `Unknown` with stale/served-at evidence | Never replay cached `Current` as fresh |
| Consumer policy | Authorization plus each lifecycle/provenance combination | Mutation allowed only for projection-backed `Current`; `LocalOnly` is never confirmed | All other combinations fail closed |

</intent-contract>

## Code Map

- `src/Hexalith.EventStore.Contracts/Queries/QueryResponseMetadata.cs` -- additive public metadata carrier whose positional ABI must remain unchanged.
- `src/Hexalith.EventStore.Contracts/Queries/QueryResponseProvenance*.cs` -- model for exact-name, fail-safe enum serialization.
- `src/Hexalith.EventStore.Client/Projections/ReadModelFreshnessExtensions.cs` -- persisted freshness-to-query metadata bridge.
- `src/Hexalith.EventStore/Controllers/QueriesController.cs` -- authoritative provenance normalization, compatibility policy, and core gateway headers.
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs` -- typed/untyped `200`/`304` body-header reconciliation.
- `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` -- generated external API metadata headers.
- `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs` -- cache boundary for time-sensitive evidence.
- `tests/Hexalith.EventStore.Server.Tests/Integration/QueryResponseProvenancePersistenceTests.cs` -- persisted model through router/controller/HTTP client proof.

## Tasks & Acceptance

**Execution:**
- `src/Hexalith.EventStore.Contracts/Queries/ProjectionLifecycleState.cs`, `ProjectionLifecycleStateJsonConverter.cs`, `ProjectionLifecyclePolicy.cs`, and `QueryResponseMetadata.cs` -- add the stable contract, safe converter, known-value/provenance normalization, compatibility projections, projection-confirmed/default-mutation policy, and additive metadata property; fully document public APIs.
- `tests/Hexalith.EventStore.Contracts.Tests/Queries/ProjectionAdapterContractTests.cs` and `SubmitQueryResponseTests.cs` -- cover numeric stability, unsafe JSON/DataContract defaults, invalid writes, legacy constructor compatibility, metadata round trips, all lifecycle states, and policy truth tables.
- `src/Hexalith.EventStore.Client/Projections/ReadModelFreshnessExtensions.cs` and `tests/Hexalith.EventStore.Client.Tests/Projections/ReadModelFreshnessTests.cs` -- map persisted current/aging/stale/unknown evidence into lifecycle and compatibility fields without reverse inference; move `ReadModelFreshnessResult<T>` to its own same-named file if touched.
- `src/Hexalith.EventStore/Queries/HandlerAwareQueryRouter.cs`, `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`, `src/Hexalith.EventStore/Controllers/QueriesController.cs`, `tests/Hexalith.EventStore.QueryRouting.Tests/HandlerAwareQueryRouterTests.cs`, `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs`, `tests/Hexalith.EventStore.Server.Tests/Pipeline/Queries/SubmitQueryHandlerTests.cs`, and `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs` -- preserve explicit lifecycle only on projection routes, normalize invalid values, apply lifecycle-aware freshness enforcement with legacy `IsStale` fallback only when lifecycle is `Unknown`, and emit the canonical header on `200`/`304`.
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs` and `tests/Hexalith.EventStore.Client.Tests/Gateway/EventStoreGatewayClientTests.cs` -- safely parse/reconcile one lifecycle header for `200` and bodyless `304`, preserve typed/untyped parity, and fail closed for missing, duplicate, invalid, mismatched, or non-projection evidence.
- `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs` and `tests/Hexalith.EventStore.Server.Tests/Actors/CachingProjectionActorTests.cs` -- clear cached lifecycle together with `IsStale` and `ServedAt` while retaining stable metadata.
- `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs`, `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs`, `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiGeneratedControllerErrorSemanticsTests.cs`, `tests/Hexalith.EventStore.Sample.Tests/SampleApi/SampleApiGeneratedControllerRuntimeTests.cs`, and `tests/Hexalith.EventStore.Sample.Tests/SampleApi/SampleApiHostMiddlewareTests.cs` -- emit all six canonical lifecycle names only for projection-backed metadata on `200`/`304`, omit unknown/non-projection/invalid values, and retain legacy bounded headers.
- `tests/Hexalith.EventStore.Server.Tests/Integration/QueryResponseProvenancePersistenceTests.cs` -- extend the existing saved-read-model proof to assert exact `Current` and `Stale` lifecycle values at the real HTTP client surface, including persisted timestamp/version end state.
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventStoreGatewayClient.cs` and `tests/Hexalith.EventStore.Testing.Tests/Fakes/FakeEventStoreGatewayClientTests.cs` -- preserve explicitly supplied lifecycle metadata without synthesizing authority and keep typed/untyped/not-modified behavior aligned.
- `docs/reference/query-api.md`, `docs/reference/nuget-packages.md`, and `docs/concepts/projection-lifecycle.md` -- document the wire/header contract, authoritative sources, legacy fields, cache/304 behavior, and Parties/default UX mapping; state that consumer UI adoption is consumer-owned.

**Acceptance Criteria:**
- Given any public lifecycle value or unsafe wire input, when Contracts JSON/DataContract serialization runs, then exact canonical values round-trip and missing/invalid input resolves to `Unknown` without changing legacy ABI.
- Given persisted `IReadModelFreshness` evidence, when the production metadata bridge and query path serve it, then the saved timestamp/version produces exact `Current` or `Stale` lifecycle evidence at the typed and untyped client surface.
- Given explicit authoritative operational evidence, when a projection-backed response crosses every metadata carrier, then `Rebuilding`, `Degraded`, `Unavailable`, or `LocalOnly` and the matching bounded header survive without ETag/Boolean inference.
- Given handler-computed, missing, invalid, cached, or contradictory evidence, when it crosses router, controller, client, or generated REST boundaries, then lifecycle is `Unknown` and no authoritative lifecycle header is exposed.
- Given each lifecycle state and provenance combination, when the EventStore-owned consumer policy is evaluated, then only projection-backed `Current` permits an otherwise-authorized mutation and `LocalOnly` never counts as projection-confirmed success.

### Review Findings

- [x] [Review][Patch] [high] Prevent delivery lifecycle ABA from validating a stale query payload by retaining absent-idle cleanup while adding a separate durable monotonic lifecycle epoch — Administrator selected this design on 2026-07-16. Fixed with a separate `projection-lifecycle-epoch` actor-state value committed atomically with active-row removal; post-delivery absence now synthesizes `Idle(2)` instead of repeating `Idle(0)`. [`src/Hexalith.EventStore.Server/Actors/ProjectionLifecycleActor.cs:181`; `tests/Hexalith.EventStore.Server.Tests/Actors/ProjectionLifecycleActorTests.cs:168`]
- [x] [Review][Patch] [medium] Fail closed once instead of executing every successful query actor twice during a persistent lifecycle-store outage — fixed by making lifecycle-read failure ineligible for the coherence retry. [`src/Hexalith.EventStore.Server/Queries/QueryRouter.cs:150`]
- [x] [Review][Patch] [medium] Propagate wrapped lifecycle-read cancellation instead of converting it into lifecycle failure and continuing — fixed with exception-tree cancellation propagation before the generic failure fallback. [`src/Hexalith.EventStore.Server/Queries/QueryRouter.cs:228`]
- [x] [Review][Patch] [medium] Verify that the public production constructor actually reads persisted lifecycle evidence and changes the returned lifecycle — covered through the real weak/JSON `ActorProxyFactory` path. [`tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs:1197`]
- [x] [Review][Patch] [medium] Assert that enabled, stable `Idle` lifecycle observation preserves producer `Current`, `IsStale = false`, and stable metadata — covered. [`tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs:162`]
- [x] [Review][Patch] [medium] Exercise the normal `Idle` projection-alias retry path instead of only the `Rebuilding` shortcut — covered with two payload invocations and converged alias lifecycle evidence. [`tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs:768`]
- [x] [Review][Defer] [high] Define erase-query visibility so a stably `Erasing` projection cannot retain producer `Current` and remain mutation-eligible [`src/Hexalith.EventStore.Server/Queries/QueryRouter.cs:248`] — deferred, pre-existing; reconfirmed against the existing Story 1.19 ledger entry.

## Spec Change Log

## Review Triage Log

### 2026-07-16 — Follow-up review of exact candidate `8aa6d0f0a417034d0c46eb9506fb7196a013401b`

- decision-needed: 1 high, resolved by Administrator as a separate durable monotonic lifecycle epoch and applied as a patch.
- patch: 6 fixed (high 1, medium 5).
- defer: 1 high, pre-existing erase-query visibility policy retained under the Story 1.19 ledger owner.
- dismissed: 3 as out of Story 1.16 scope, intentional controlling-contract behavior, or redundant verification.
- verification: focused actor/router Release lane 88 passed / 0 failed; complete Server Release lane 2,626 passed / 25 skipped / 0 failed; `Hexalith.EventStore.slnx` Release build 0 warnings / 0 errors.
- disposition: `done`; `followup_review_recommended` cleared. The applied working-tree patch still requires a committed exact SHA before Story 1.20 can use it as runtime evidence.

### 2026-07-11 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 15: (high 0, medium 11, low 4)
- defer: 0
- reject: 5: (high 0, medium 3, low 2)
- addressed_findings:
  - `[medium]` `[patch]` Preserved additive `IsDegraded` evidence for non-degraded lifecycle values and added an exhaustive compatibility truth table.
  - `[medium]` `[patch]` Added all-state bodyless `304` lifecycle and compatibility-field coverage.
  - `[medium]` `[patch]` Added same-value and conflicting duplicate lifecycle-header rejection for `200` and `304`.
  - `[medium]` `[patch]` Made mutation and projection-confirmation policy tests exhaustive across the stable lifecycle set.
  - `[medium]` `[patch]` Merged fake not-modified metadata with authoritative ETag/not-modified fields and documented the public parameters.
  - `[medium]` `[patch]` Added missing, numeric, case-variant, invalid, and non-projection lifecycle reconciliation coverage for gateway `200` responses.
  - `[medium]` `[patch]` Normalized generated REST lifecycle compatibility headers and covered unknown/invalid omission.
  - `[medium]` `[patch]` Exercised operational lifecycle headers on core/generated conditional responses.
  - `[medium]` `[patch]` Added operational lifecycle preservation proof across the query metadata carrier chain.
  - `[medium]` `[patch]` Re-read persisted freshness state after typed and untyped queries to prove the read path did not mutate it.
  - `[medium]` `[patch]` Cleared pre-existing lifecycle headers before core and generated omission paths.
  - `[low]` `[patch]` Corrected conditional lifecycle-header wording in the concept guide.
  - `[low]` `[patch]` Linked the new lifecycle concept from the documentation index.
  - `[low]` `[patch]` Documented lifecycle output on the persisted freshness bridge.
  - `[low]` `[patch]` Completed XML parameter documentation for the expanded fake API.

## Design Notes

The new lifecycle enum lives in Contracts and does not replace `ReadModelFreshnessState`: downstream consumers depend on legacy `Aging = 2`. Operational states are explicit producer evidence; the platform documents their sources and transports them but does not guess precedence among simultaneous signals. Unknown lifecycle may use legacy `IsStale` only for request freshness compatibility; it never becomes a lifecycle state. The canonical lifecycle header is required on the core gateway so bodyless `304` can preserve authoritative state, then generated REST forwards the same closed-set value.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj` -- expected: contract/serialization compatibility green.
- `dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj` -- expected: classifier and gateway `200`/`304` cases green.
- `dotnet test tests/Hexalith.EventStore.QueryRouting.Tests/Hexalith.EventStore.QueryRouting.Tests.csproj` -- expected: handler provenance gate green.
- `dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/Hexalith.EventStore.RestApi.Generators.Tests.csproj` -- expected: generated header/omission cases green.
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj` -- expected: generated runtime header cases green.
- `dotnet test tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj` -- expected: fake lifecycle parity green.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj` -- expected: controller/cache/persisted-path lifecycle proof green, or exact pre-existing broad-lane blocker recorded separately.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: warnings-as-errors Release build green.
- `git diff --check` -- expected: no whitespace errors.

## Auto Run Result

Status: done

### Summary

Added a stable seven-state projection lifecycle contract, provenance-safe normalization, persisted freshness mapping, default consumer mutation policy, canonical gateway/generated REST headers, typed/untyped and bodyless-`304` reconciliation, cache invalidation, testing-fake parity, and lifecycle documentation.

### Files Changed

- `src/Hexalith.EventStore.Contracts/Queries/ProjectionLifecycleState.cs` -- defines stable lifecycle values.
- `src/Hexalith.EventStore.Contracts/Queries/ProjectionLifecycleStateJsonConverter.cs` -- provides exact-name fail-safe JSON conversion.
- `src/Hexalith.EventStore.Contracts/Queries/ProjectionLifecyclePolicy.cs` -- centralizes provenance, compatibility, header, confirmation, and mutation policy.
- `src/Hexalith.EventStore.Contracts/Queries/QueryResponseMetadata.cs` -- adds the init-only lifecycle carrier without changing constructor ABI.
- `src/Hexalith.EventStore.Client/Projections/ReadModelFreshnessExtensions.cs` -- maps persisted age evidence into lifecycle metadata.
- `src/Hexalith.EventStore.Client/Projections/ReadModelFreshnessResult.cs` -- keeps the public freshness result in its own file.
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs` -- reconciles lifecycle body/header evidence for `200` and `304`.
- `src/Hexalith.EventStore/Queries/HandlerAwareQueryRouter.cs` -- removes lifecycle authority from handler-computed routes.
- `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs` -- normalizes projection-backed lifecycle metadata.
- `src/Hexalith.EventStore/Controllers/QueriesController.cs` -- enforces lifecycle-aware freshness and emits canonical headers.
- `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs` -- clears time-sensitive lifecycle evidence on cache hits.
- `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` -- emits normalized lifecycle and compatibility headers.
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventStoreGatewayClient.cs` -- preserves explicit lifecycle and consistent cache metadata.
- `tests/Hexalith.EventStore.Contracts.Tests/Queries/ProjectionAdapterContractTests.cs` -- covers stable values, serialization, compatibility, and policy truth tables.
- `tests/Hexalith.EventStore.Contracts.Tests/Queries/QueryResultLifecycleCarrierTests.cs` -- proves operational lifecycle carrier round trips.
- `tests/Hexalith.EventStore.Contracts.Tests/Queries/SubmitQueryResponseTests.cs` -- covers response lifecycle propagation.
- `tests/Hexalith.EventStore.Client.Tests/Projections/ReadModelFreshnessTests.cs` -- covers persisted age-to-lifecycle mapping.
- `tests/Hexalith.EventStore.Client.Tests/Gateway/EventStoreGatewayClientTests.cs` -- covers authoritative, unsafe, duplicate, typed/untyped, and `304` lifecycle transport.
- `tests/Hexalith.EventStore.QueryRouting.Tests/HandlerAwareQueryRouterTests.cs` -- proves handler routes fail closed.
- `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs` -- proves projection-route operational lifecycle propagation.
- `tests/Hexalith.EventStore.Server.Tests/Pipeline/Queries/SubmitQueryHandlerTests.cs` -- proves handler carrier propagation.
- `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs` -- covers normalization, headers, omission, and freshness policy.
- `tests/Hexalith.EventStore.Server.Tests/Actors/CachingProjectionActorTests.cs` -- proves cached lifecycle clearing.
- `tests/Hexalith.EventStore.Server.Tests/Integration/QueryResponseProvenancePersistenceTests.cs` -- proves persisted Current/Stale evidence through typed/untyped clients.
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs` -- checks generated lifecycle code.
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiGeneratedControllerErrorSemanticsTests.cs` -- exercises generated lifecycle/compatibility headers.
- `tests/Hexalith.EventStore.Sample.Tests/SampleApi/SampleApiGeneratedControllerRuntimeTests.cs` -- proves generated runtime lifecycle headers.
- `tests/Hexalith.EventStore.Sample.Tests/SampleApi/SampleApiHostMiddlewareTests.cs` -- proves hosted generated API lifecycle behavior.
- `tests/Hexalith.EventStore.Testing.Tests/Fakes/FakeEventStoreGatewayClientTests.cs` -- covers fake typed/untyped/not-modified parity.
- `docs/concepts/projection-lifecycle.md` -- documents lifecycle sources and consumer policy.
- `docs/reference/query-api.md` -- documents lifecycle wire/header behavior.
- `docs/reference/nuget-packages.md` -- documents package ownership and APIs.
- `docs/index.md` -- links the lifecycle concept.
- `_bmad-output/implementation-artifacts/spec-1-11-complete-projection-freshness-lifecycle.md` -- records the implementation and review result.

### Review Findings

- Patches applied: 15 (11 medium, 4 low), primarily compatibility preservation and exhaustive public-boundary verification.
- Items deferred: 0.
- Items rejected: 5, covering direct invalid-enum `DataContractSerializer` behavior outside the JSON wire contract, an unreachable hypothetical legacy classifier value, consumer-submodule/operational-producer expansion outside this EventStore contract story, and unrelated sprint-tracker state.
- Follow-up review recommended: true because review-driven changes crossed public contract, client, gateway, generator, fake, documentation, and test surfaces.

### Verification

- Contracts tests: 693 passed.
- Client tests: 590 passed.
- Query-routing tests: 6 passed.
- REST generator tests: 124 passed.
- Sample tests: 117 passed.
- Testing-fake tests: 144 passed.
- Full Server tests: 2,295 passed, 25 pre-existing ATDD tests skipped, 0 failed.
- Serialized Release solution build: succeeded with 0 warnings and 0 errors.
- Matrix audit: all eight intent-contract scenarios have executed passing coverage.
- `git diff --check`: passed.

### Residual Risks

- Story 2.8 remains formally `in-review`; this story consumes its implemented provenance invariants but does not claim that separate owner-review closure.
- Consumer-specific UI rendering/localization and actual operational producer wiring remain owned by their respective consumer and later projection stories; this change supplies and verifies the EventStore contract/carrier/default-policy surface.
