---
status: in-review
baseline_revision: 404201a9363c1b80121ecbf5e72ca4fb71f6ac79
review_loop_iteration: 2
---

# Story 2.8: Query Response Provenance Contract And Route-Aware Gateway ETag

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a consumer of platform query metadata,
I want every query response to declare explicit route provenance and the gateway to stop attaching projection ETags to handler-computed responses,
so that generated REST and UI code never present a gateway ETag or fabricated version as projection-backed current/stale evidence.

## Acceptance Criteria

1. **Additive provenance contract.** Query metadata exposes exactly three route classifications: `Unknown`, `ProjectionBacked`, and `HandlerComputed`. `Unknown` is the zero/default value, omitted legacy wire data deserializes as `Unknown`, and the existing public positional constructor/factory/deconstruction signatures remain binary- and source-compatible. The producing route, not a producer claim or `ProjectionType` inference in a consumer, is authoritative.
2. **Handler route is never projection evidence.** A successful response selected by `HandlerAwareQueryRouter` has `ProjectionType == null` and provenance `HandlerComputed`, even if the domain producer supplied a projection type or claimed `ProjectionBacked`. The gateway does not fetch, attach, or expose a projection-actor ETag, `ProjectionVersion`, or `IsStale` by falling back to `request.Domain` or `request.ProjectionType`; unrelated producer evidence (served-at, degraded state, warning codes, and authoritative paging) remains intact.
3. **Projection route carries genuine evidence.** A successful response selected by the projection-actor router has provenance `ProjectionBacked`. When its metadata originates from a persisted `IReadModelFreshness` read, genuine `ProjectionVersion` and `IsStale` values traverse `QueryResult.Metadata -> QueryRouterResult.Metadata -> SubmitQueryResult.Metadata -> SubmitQueryResponse.Metadata -> EventStoreQueryResult.Metadata`. A gateway/self-routing ETag remains an opaque cache validator and is never copied or interpreted as projection version, freshness, or projection-confirmed success.
4. **Conditional requests and freshness are provenance-safe.** A matching self-routing `If-None-Match` value cannot return `304` before the selected route is known. Handler-computed or unknown routes execute and return no projection validator even when the request carries a matching projection ETag. Legitimate projection-backed strong-ETag/`304` behavior remains supported after route selection. `RequireFresh`/`MaxStaleness` fails closed with the existing `query_projection_stale` taxonomy unless provenance is `ProjectionBacked` and the required producer evidence is authoritative.
5. **Consumers fail safe.** The raw gateway, typed/untyped .NET client, generated REST controllers, and UI-facing metadata can distinguish provenance. JSON and the stable `X-Hexalith-Query-Provenance` header use the canonical names `Unknown`, `ProjectionBacked`, and `HandlerComputed` on `200` and bodyless `304` responses; missing or invalid provenance is `Unknown`. Generated REST emits `X-Hexalith-Projection-Version` and `X-Hexalith-Is-Stale` only for `ProjectionBacked`; `HandlerComputed` and `Unknown` render/behave as unknown and never claim projection-confirmed success. Existing ETag, served-at, degraded, warnings, and paging bounds remain intact.
6. **Real-path evidence.** Focused compatibility/branch tests and a Tier 2/3 gateway-path proof cover: (a) a live handler route with no projection ETag/version/stale headers, including a matching incoming self-routing ETag; (b) a projection route backed by persisted read-model state with genuine freshness/version evidence; (c) projection-backed strong ETag and `304`; and (d) missing legacy provenance defaulting to `Unknown`. Manually constructing metadata in a mock is not sufficient evidence for the persisted-path cases.
7. **Correct-Course boundary and green gates.** No file under `references/Hexalith.Tenants` is modified. The Tenants `ProjectionVersion := ETag` producer cleanup remains Story 4.7 and does not block EventStore enforcement; affected handler routes are neutralized as `HandlerComputed`/`Unknown`. Deferred D6 persisted-age work, deleted Story 7.6, unrelated FR34 operations work, package upgrades, and topology changes remain out of scope. Release build and all focused per-project tests pass under warnings-as-errors.

## Tasks / Subtasks

- [ ] **Task 1 - Add the backward-compatible provenance contract** (AC: 1, 5)
  - [ ] Add `src/Hexalith.EventStore.Contracts/Queries/QueryResponseProvenance.cs` as the sole type in the file. Define explicit stable values with `Unknown = 0`, plus `ProjectionBacked` and `HandlerComputed`; use `DataContract`/`EnumMember` and a dedicated converter that writes only the exact AC5 names and reads missing, null, numeric, case-variant, or unrecognized values as `Unknown` without throwing or elevating provenance. The stock `JsonStringEnumConverter<QueryResponseProvenance>` is insufficient because it accepts numeric/case-variant input and throws on unknown strings.
  - [ ] Update `QueryResponseMetadata.cs` with a non-positional `[DataMember]` `Provenance` init property defaulting to `Unknown`. **Do not append a primary-constructor parameter:** Story 1.2 already showed that changing positional record constructors breaks compiled consumers and requires compatibility overload repair.
  - [ ] Pin old/new DataContract and JSON behavior in Contracts tests: old payload without provenance -> `Unknown`; all values round-trip; the existing `QueryResponseMetadata` constructor signature remains present; warning-code defensive copying remains unchanged.

- [ ] **Task 2 - Make route selection authoritative** (AC: 1-3)
  - [ ] Update `HandlerAwareQueryRouter` so every successful handler branch returns `ProjectionType: null` and metadata stamped `HandlerComputed`, overriding any producer provenance while preserving payload, paging, degraded state, warnings, served-at, cancellation, and existing safe failures.
  - [ ] Update `QueryRouter` so successful projection-actor branches stamp `ProjectionBacked`, overriding missing/producer provenance while preserving producer metadata and all existing DAPR cancellation/not-found/error classification.
  - [ ] Keep `QueryResult`, `QueryRouterResult`, `SubmitQueryResult`, `SubmitQueryResponse`, `SubmitQueryHandler`, and `EventStoreQueryResult` as pass-through carriers unless a test proves a real propagation defect. Do not duplicate provenance fields on every wrapper.

- [ ] **Task 3 - Make gateway ETag and freshness behavior route-aware** (AC: 2-5)
  - [ ] Refactor `QueriesController` so a conditional match is evaluated only after authoritative route provenance exists. Prefer route-first evaluation over duplicating handler-registry decision logic in the controller.
  - [ ] Fetch/compare a projection-actor ETag only for `ProjectionBacked`. Remove the post-route `result.ProjectionType -> request.ProjectionType -> request.Domain` fallback for `HandlerComputed`/`Unknown`; do not call `IETagService` for those routes.
  - [ ] Sanitize non-projection routes before policy evaluation/serialization: `ETag = null`, `IsNotModified = null`, `ProjectionVersion = null`, and `IsStale = null`, with no projection-derived HTTP/body ETag; preserve `IsDegraded`, warning codes, paging, and served-at. Never map `ETag` to `ProjectionVersion`.
  - [ ] Preserve bounded/mixed/malformed `If-None-Match` handling, its existing decode/miss telemetry, policy/payload cache-safety rules, authorization-before-lookup, tenant/RBAC behavior, ETag-service fail-open behavior, cancellation propagation, and the existing freshness ProblemDetails taxonomy. Undecodable/legacy validators remain cache misses rather than being compared as raw ETags. Set success ETag headers only after freshness enforcement.
  - [ ] Emit `X-Hexalith-Query-Provenance` for successful gateway `200` and `304` responses. Missing or unrecognized input must never elevate to `ProjectionBacked`.

- [ ] **Task 4 - Enforce provenance in clients and generated REST** (AC: 5)
  - [ ] Update `EventStoreGatewayClient` to normalize provenance from the response body/header and preserve it identically in typed and untyped results. For `HandlerComputed` or `Unknown`, clear ETag, `IsNotModified`, `ProjectionVersion`, and `IsStale` while preserving unrelated metadata. A bodyless `304` reads the provenance header and is accepted only for `ProjectionBacked` with a non-wildcard strong ETag; missing, invalid, duplicate, or contradictory provenance fails closed as a gateway error rather than exposing an unsafe not-modified result. On `200`, missing, invalid, duplicate, or contradictory body/header provenance normalizes to `Unknown` and clears projection evidence. Preserve weak-ETag rejection and HTTP ETag precedence as validator-only behavior for projection-backed responses.
  - [ ] Update `RestApiControllerEmitter` so generated query actions forward `X-Hexalith-Query-Provenance`, emit ETag/projection-version/stale headers, and return `304` only when metadata is `ProjectionBacked`. A `HandlerComputed` or `Unknown` result that contradictorily carries ETag/`IsNotModified` is sanitized or rejected fail-closed. Preserve bounded strong ETags, paging/degraded/warning headers, support-safe ProblemDetails, gateway-only delegation, and generated code `ConfigureAwait(false)`.
  - [ ] Update generated source assertions, compiled generated-controller runtime tests, and Sample API host tests. Existing positive projection-header fixtures must opt into `ProjectionBacked`; add `HandlerComputed` and `Unknown` omission cases.

- [ ] **Task 5 - Prove compatibility and real behavior** (AC: 1-6)
  - [ ] Extend `ProjectionAdapterContractTests`, `SubmitQueryResponseTests`, `HandlerAwareQueryRouterTests`, `QueryRouterTests`, `SubmitQueryHandlerTests`, `QueriesControllerTests`, `EventStoreGatewayClientTests`, generator tests, and Sample generated-host tests only where their existing assertions are affected.
  - [ ] Add the regression that currently escapes: a handler-supported request carrying a matching, decodable self-routing `If-None-Match` must reach the handler, must not call `IETagService`, must not return `304`, and must expose no projection ETag/version/stale evidence.
  - [ ] Add `tests/Hexalith.EventStore.IntegrationTests/ContractTests/QueryResponseProvenanceE2ETests.cs` (or the existing contract-test equivalent) for real-path proof. Reuse the live Tenants `list-tenants` handler through EventStore's raw `/api/v1/queries` gateway route without editing the submodule; the generated `GET /api/tenants` surface remains proven by compiled generated-controller runtime tests because the current `tenants-api` Aspire resource has no discoverable HTTP endpoint. Establish a genuinely matching current self-routing validator before the live handler request by first obtaining the strong `counter:tenant-a` validator, then submitting `list-tenants` with tenant `tenant-a`, request `projectionType: counter`, and an empty/cache-eligible payload. That exact scope must have returned pre-route `304` in the old implementation; prove the new implementation reaches the handler, returns `200`, and neutralizes projection-looking metadata. Do not use a validator from a different projection/tenant key, and do not use a payload that the cache-safety guard would skip. Reuse the persisted Sample projection round-trip for projection provenance/strong ETag/`304`; assert the raw bodyless `304` carries `X-Hexalith-Query-Provenance: ProjectionBacked` and is accepted through `EventStoreGatewayClient`.
  - [ ] For genuine `IReadModelFreshness` version/stale traversal, use a test-owned persisted read-model fixture and read the value back through `IReadModelStore`, then drive the value through production `QueryRouter`, `SubmitQueryHandler`, `QueriesController` HTTP serialization, and `EventStoreGatewayClient`. A test-owned actor/proxy adapter may source the already-persisted metadata, but tests must not manually construct the carrier wrappers being proved. The Tier 2/3 acceptance proof must inspect persisted read-model/state evidence and assert the exact version/stale values at the client surface. Do **not** add fabricated freshness/version fields to `ProjectionState` or `EventReplayProjectionActor`; that would pull the separately deferred D6 design into this story.

- [ ] **Task 6 - Reconcile documentation and run gates** (AC: 5-7)
  - [ ] Update `docs/reference/query-api.md` to document `metadata.provenance`, `X-Hexalith-Query-Provenance`, projection-only freshness/version claims, and route-aware conditional requests. Replace the incorrect wording that an ETag is the projection version; it is an opaque validator for the selected representation.
  - [ ] Confirm no `references/Hexalith.Tenants` diff and no unrelated root submodule-pointer change. Reconcile stale provenance ownership in `_bmad-output/implementation-artifacts/deferred-work.md` and `_bmad-output/planning-artifacts/backlog/rest-generator-hardening.md`: EventStore platform enforcement is Story 2.8; only the Tenants producer follow-up remains Story 4.7. Do not pull either ledger's unrelated deferred items into implementation.
  - [ ] Run `dotnet build Hexalith.EventStore.slnx --configuration Release`.
  - [ ] Run per project: `Contracts.Tests`, `QueryRouting.Tests`, `Server.Tests`, `Client.Tests`, `RestApi.Generators.Tests`, and `Sample.Tests`.
  - [ ] Run the focused Tier 2/3 provenance lane using the repository's existing IntegrationTests prerequisites; record the exact command, persisted evidence, and any environment blocker separately from deterministic green lanes.

## Dev Notes

### Correct-Course Reconciliation

- The approved 2026-07-09 readiness correction supersedes the 2026-07-07 placement that assigned EventStore enforcement to Story 4.7. **Active scope is Story 2.8 for EventStore; Story 4.7 is Tenants-only follow-up.** No active task in this file depends on Tenants maintainer approval.
- Story 2.8 is a Phase 4 readiness blocker for new generated API/UI current/stale claims. It adds provenance on top of the already delivered AD-14 metadata pipe; it does not recreate the pipe or resurrect deleted Story 7.6.
- D6 persisted read-model-age/freshness work remains deferred. This story classifies and safely carries genuine evidence when present; it must not invent evidence to make a test green.
- FR34 coverage is limited to provenance/evidence integrity and higher-tier proof. Dead letters, admin operations, deployment, and broad CI recovery remain in their owning stories.

### Architecture Compliance

- **AD-3:** gateway remains the query policy boundary. Generated hosts and UI code do not bypass it.
- **AD-14:** keep `QueryResponseMetadata` as the single evidence carrier and preserve producer-authoritative paging/degraded/warning fields, gateway HTTP-validator authority, served-at fill-only-when-absent, and unknown freshness.
- **AD-15:** the selected route stamps provenance. Consumers never infer it from ETag, projection type, request domain, payload fields, or a producer's self-assertion.
- **AD-12/NFR16:** mock-only metadata proves branches, not persisted-path acceptance. Tier 2/3 evidence must inspect the actual gateway response and persisted state/read model.
- ETags and cursors remain opaque and support-safe. Do not display, parse for business meaning, or log raw values. Existing self-routing ETag decoding is only cache routing and must occur after the response path is known to be projection-backed.
- No new NuGet dependency or package-version change is required. Use the repository pins: .NET SDK `10.0.301`, DAPR .NET SDK `1.18.4`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, and NSubstitute `6.0.0-rc.1`.

### Existing Files Being Modified

| File | Current state | Story change | Preserve |
| --- | --- | --- | --- |
| `src/Hexalith.EventStore.Contracts/Queries/QueryResponseMetadata.cs` | Positional DataContract record; no provenance; custom defensive `WarningCodes` property. | Add non-positional default-safe provenance. | Existing constructor ABI, DataMember compatibility, warning copy semantics. |
| `src/Hexalith.EventStore/Queries/HandlerAwareQueryRouter.cs` | Documentation says handler routes leave `ProjectionType` null, but success forwards producer `ProjectionType` and metadata unchanged. | Force null and stamp `HandlerComputed`. | Decorator fallback, payload/error/cancellation behavior. |
| `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs` | Projection actor route forwards producer metadata without route classification. | Stamp `ProjectionBacked`. | DAPR actor ID/type routing, exception taxonomy, cancellation. |
| `src/Hexalith.EventStore/Controllers/QueriesController.cs` | Can return pre-route `304`; post-route ETag lookup falls back to request/domain; merge trusts stale/version without provenance. | Route-first conditional validation, projection-only ETag/freshness, provenance header. | Auth-before-lookup, bounds, fail-open ETag service, ProblemDetails, paging rules. |
| `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs` | Preserves body metadata; bodyless `304` synthesizes ETag-only metadata. | Normalize provenance header/body with default `Unknown`. | Typed/untyped parity, validator precedence, transport/error mapping. |
| `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` | Emits projection-version/stale whenever present. | Gate those headers and forward provenance. | All other generated header bounds, safe errors, compilation contract. |
| `docs/reference/query-api.md` | Calls ETag a projection version and has no provenance contract. | Document AD-15 behavior and header. | Existing auth, paging, support-safety, SignalR-as-hint guidance. |

`QueryResult.cs`, `QueryRouterResult.cs`, `SubmitQuery.cs`, `SubmitQueryHandler.cs`, `SubmitQueryResponse.cs`, and `EventStoreQueryResult.cs` already pass `QueryResponseMetadata`; tests should prove the new member crosses them without unnecessary DTO reshaping.

### Conditional-Request Trap

The existing controller decodes `If-None-Match`, asks `IETagService`, and can return `304` before `mediator.Send`. That optimization predates handler-aware routing. Removing only the later request/domain fallback leaves the bug exploitable by a valid projection ETag on a handler query. Route first, then evaluate the validator for `ProjectionBacked`; a `304` is valid only for the selected representation. Preserve the existing protections for policy inputs, unsafe payloads, mixed projection types, malformed validators, and excessive tag lists.

### Testing Requirements

- Use xUnit v3 + Shouldly; NSubstitute only for deterministic branch isolation. Methods are PascalCase. Run test projects individually, never solution-level `dotnet test`.
- Contract tests must protect both source and binary compatibility. Adding provenance as a primary-constructor parameter is a regression even if source callers compile after restore.
- Router tests must prove route ownership overrides producer claims: handler producer claiming `ProjectionBacked` still becomes `HandlerComputed`; projection producer with missing/incorrect provenance becomes `ProjectionBacked`.
- Controller tests must distinguish `ETag` (opaque validator), `ProjectionVersion` (producer evidence), and `IsStale` (freshness evidence). Never use the same string/value as proof of equivalence.
- Generator tests need source-output assertions **and** executed compiled-controller behavior. Positive version/stale fixtures explicitly use `ProjectionBacked`; handler/unknown fixtures retain unrelated headers but omit projection evidence.
- Tier 3 tests may require Docker, Aspire, DAPR placement, and scheduler. Follow `scripts/generated-api-smoke-preflight.sh` before accepting a live-topology blocker and assert persisted/read-model end state, not only HTTP status.

Current assertions that must be reconciled rather than copied:

| Test file / area | Current assumption | Required Story 2.8 assertion |
| --- | --- | --- |
| `HandlerAwareQueryRouterTests.cs` | Handler result retains producer projection type and exact metadata identity. | Projection type is null; route overwrites provenance to `HandlerComputed` while preserving unrelated metadata. |
| `QueryRouterTests.cs` | Projection metadata passes through with no route label. | Actual actor route overwrites provenance to `ProjectionBacked` and preserves genuine fields. |
| `QueriesControllerTests.cs` | Several cases expect pre-router `304` or request/domain ETag fallback. | No pre-route handler short-circuit; no handler/unknown ETag-service call; projection-only `304`; freshness requires projection provenance. |
| `EventStoreGatewayClientTests.cs` | Bodyless `304` synthesizes ETag-only metadata. | Provenance header survives `304`; missing/invalid header defaults to `Unknown`; typed/untyped parity remains. |
| Generator and Sample generated-host tests | Positive stale/version fixtures omit route provenance. | Positive fixtures explicitly use `ProjectionBacked`; handler/unknown suppress stale/version while forwarding safe unrelated headers. |
| Contracts tests | No enum/default compatibility surface. | Exact enum values/names, legacy missing-member `Unknown`, JSON/DataContract round trips, unchanged constructor signature. |

### Previous Story And Git Intelligence

- Story 2.7 is the nearest prior Epic 2 story but is only `ready-for-dev` and has no implementation record. It is transport-header work with no code dependency here; do not assume its handler or host migrations exist.
- Story 1.2 is the real foundation. Its review required compatibility overloads after positional record changes, kept unknown freshness fail-closed, cleared time-sensitive cache metadata, and ensured HTTP ETag precedence does not imply projection version. Reuse those decisions.
- Story 2.6 reinforces additive public Client changes, implementation + DI seam tests, and generated source/runtime/compiled-host proof.
- The five newest commits are tooling/status/submodule-pointer work and provide no target implementation pattern. Relevant implementation history is the Story 1.2 metadata propagation and Story 2.2 generated metadata/header work; keep the new diff narrow around those seams.

### Latest Technical Notes

- RFC 9110 defines `If-None-Match` against the selected representation; therefore a projection validator cannot authorize a pre-routing `304` for a handler-computed representation.
- Microsoft Data Contract versioning guidance treats additive optional data members as compatible and missing members as their default; `Unknown = 0` is the required safe legacy default. Enum members used by DataContract serialization must be explicit and stable.
- System.Text.Json serializes enums numerically by default; use the story's dedicated fail-safe converter so canonical output and invalid-input-to-`Unknown` behavior do not depend on ambient serializer configuration differing across DAPR, ASP.NET, and client paths.

### Project Structure Notes

- New public contract type: `src/Hexalith.EventStore.Contracts/Queries/QueryResponseProvenance.cs`; one C# type per file and XML documentation on all public/internal members.
- Keep route policy in the existing routers/controller. Do not create a second query router or a parallel metadata DTO.
- Do not modify `references/Hexalith.Tenants/**`, `ProjectionState`, `EventReplayProjectionActor`, AppHost/DAPR YAML, `Directory.Packages.props`, or `tools/release-packages.json` for this story.
- No UI component change is required unless an existing EventStore-owned component currently renders freshness. The public metadata/client contract and generated REST guard establish the safe `Unknown` behavior for UI consumers; future UI stories consume it.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` - Query Response Provenance Gate and Story 2.8]
- [Source: `_bmad-output/planning-artifacts/architecture.md` - AD-12, AD-14, AD-15]
- [Source: `_bmad-output/planning-artifacts/prd.md` - FR4, FR12, FR15, FR34, NFR8, NFR16]
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-09-implementation-readiness-corrections.md` - Proposals A-C and Mandatory Story Rewrite Gate]
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-route-provenance-contract.md` - historical defect evidence; Story 4.7 ownership superseded]
- [Source: `_bmad-output/implementation-artifacts/spec-1-2-domain-query-handler-routing.md` - compatibility/review learnings and deferred provenance gap]
- [Source: `_bmad-output/implementation-artifacts/epic-2-retro-2026-07-07.md` - Action 7 and generated-protocol test lessons]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md` - AD-15 freshness behavior]
- [Source: `docs/reference/query-api.md` - current metadata/ETag behavior to reconcile]
- [RFC 9110: HTTP Semantics](https://www.rfc-editor.org/rfc/rfc9110.html)
- [Microsoft: Data Contract Versioning](https://learn.microsoft.com/en-us/dotnet/framework/wcf/feature-details/data-contract-versioning)
- [Microsoft: Enumeration Types in Data Contracts](https://learn.microsoft.com/en-us/dotnet/framework/wcf/feature-details/enumeration-types-in-data-contracts)
- [Microsoft: Customize System.Text.Json properties and enum values](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties)

## Spec Change Log

### 2026-07-11 — Review repair pass 1

- Triggering findings: contradictory non-projection ETag/freshness evidence survived in the .NET client and generated REST; the prescribed stock enum converter could elevate or throw on invalid JSON; and the persistence/live tests manually assembled carriers or depended on a Tenants API resource with no discoverable HTTP endpoint.
- Amended: Task 1 now requires fail-safe canonical provenance JSON; Task 4 explicitly sanitizes contradictory client/generated results and rejects non-projection `304`; Task 5 now names a feasible EventStore-owned raw handler route and a production-seam persisted-metadata proof that cannot manually construct the wrappers under test.
- Known-bad state avoided: treating gateway/controller correctness as sufficient while downstream consumers retain contradictory evidence, and claiming real-path acceptance from hand-assembled DTOs or an unreachable generated host.
- KEEP: additive non-positional metadata ABI; authoritative handler/projection router stamping; route-first controller behavior; projection-only ETag lookup and freshness enforcement; preservation of served-at/degraded/warnings/paging; generated provenance header; no Tenants submodule or topology edits; Release and focused green gates.

### 2026-07-11 — Review repair pass 2

- Triggering finding: the live handler regression used a `counter:tenant-a` validator against `tenant-index:system` and a cache-ineligible two-property payload, so it could pass without exercising the pre-route `304` defect.
- Amended: Task 5 now fixes the live request to the exact `counter:tenant-a` ETag scope, requires an empty/cache-eligible `list-tenants` payload, and requires raw `304` provenance plus official-client traversal. Tasks 3-4 additionally make the already-required fail-safe boundaries explicit for non-projection `IsNotModified`, legacy validator decoding, contradictory `200` provenance, and wildcard `304` validators. Task 6 excludes unrelated root submodule-pointer changes.
- Known-bad state avoided: accepting a live `200` whose ETag lookup key could not match or whose payload forced the old controller to skip its pre-route conditional check.
- KEEP: dedicated fail-safe provenance converter; additive non-positional ABI; authoritative route stamping; route-first controller; projection-only ETag/freshness evidence; typed/untyped and generated REST fail-closed behavior; production-carrier persisted traversal; raw EventStore handler route plus compiled generated-controller proof; no Tenants/topology/package changes; all prior review-pass-1 KEEP constraints.

## Review Triage Log

### 2026-07-11 — Review pass 1

- intent_gap: 0
- bad_spec: 3: (high 3, medium 0, low 0)
- patch: 3: (high 1, medium 1, low 1)
- defer: 2: (high 1, medium 1, low 0)
- reject: 4: (high 0, medium 1, low 3)
- addressed_findings:
  - `[high]` `[bad_spec]` Made typed/untyped client and generated REST fail closed for contradictory non-projection ETag, freshness, and `304` evidence while preserving unrelated metadata.
  - `[high]` `[bad_spec]` Replaced the stock enum-converter instruction with canonical output and invalid-input-to-`Unknown` semantics.
  - `[high]` `[bad_spec]` Replaced hand-assembled persistence proof and unreachable Tenants API dependency with production-seam persisted traversal and EventStore raw-route live handler evidence.

### 2026-07-11 — Review pass 2

- intent_gap: 0
- bad_spec: 1: (high 1, medium 0, low 0)
- patch: 9: (high 3, medium 4, low 2)
- defer: 2: (high 1, medium 1, low 0)
- reject: 4: (high 0, medium 1, low 3)
- addressed_findings:
  - `[high]` `[bad_spec]` Bound the live matching-validator proof to the same `counter:tenant-a` ETag actor scope and a cache-eligible handler payload, then required raw `304` provenance and official-client traversal to survive re-derivation.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

### Completion Notes List

Ultimate context engine analysis completed - comprehensive developer guide created.

### File List
