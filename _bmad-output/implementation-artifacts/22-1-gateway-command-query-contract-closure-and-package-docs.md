# Story 22.1: Gateway Command/Query Contract Closure and Package Docs

Status: done

Context created: 2026-05-12
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-requirements-gaps-current.md`
Epic: Epic 22 - Public Gateway and Downstream Integration Contracts
Scope: FR83-FR86, with direct dependency awareness for later Epic 22 query, tenant/RBAC, replay, publishing, and protection stories.

## Story

As a downstream service developer,
I want API-facing command/query DTOs and high-level client methods from EventStore packages,
so that Parties can call EventStore without duplicating gateway wire contracts or referencing EventStore runtime assemblies.

## Public Gateway Boundary Contract

- `Hexalith.EventStore.Contracts` owns stable API-facing command/query DTOs, command status DTOs, validation DTOs, replay/read DTOs when present, and stable ProblemDetails extension names used by HTTP gateway clients.
- `Hexalith.EventStore.Client` owns high-level HTTP convenience APIs for gateway callers. The current gateway client surface is `IEventStoreGatewayClient.SubmitCommandAsync` and `SubmitQueryAsync`.
- `Hexalith.EventStore.Testing` owns deterministic public-gateway fakes/builders for downstream test suites. The existing fake only covers happy command, query, configured exception, and not-modified query paths.
- `Hexalith.EventStore` and `Hexalith.EventStore.Server` are runtime/internal assemblies for this boundary. Downstream bounded contexts must not reference them for public gateway DTOs or client behavior.
- Service-assembly DTO wrappers under `src/Hexalith.EventStore/Models` are compatibility-only. They must not become the documented integration surface.
- Public gateway changes are SemVer-relevant. Do not silently change wire property names, nullability, ProblemDetails extension names, ETag behavior, or client exception/result semantics.

## Gateway Contract Decision Gate

The first development task is a blocking public-contract decision record. This
story is ready for development because the decision is bounded to the Contracts,
Client, Testing, and documentation boundary; the dev agent must not let these
SemVer decisions emerge accidentally while coding.

Before production code edits, record one decision table covering:

| Area | Required decision | Minimum evidence |
| --- | --- | --- |
| Query semantic failure | How `SubmitQueryResponse.Success == false` from an HTTP 2xx response is exposed by `SubmitQueryAsync` and `SubmitQueryAsync<T>`: typed failed result, `EventStoreGatewayException`, or explicit deferred behavior. | Client tests for HTTP 2xx `success:false`, HTTP non-success ProblemDetails, malformed body, empty payload, and typed query deserialization. |
| ETag normalization | Whether client-facing ETags are normalized unquoted tokens, HTTP header-ready quoted values, or preserved raw values; how quoted, unquoted, weak, missing, empty, and malformed ETags are accepted or rejected. | Client and fake tests for response ETag exposure and `If-None-Match` request behavior. |
| ProblemDetails extensions | Stable extension names, casing, CLR types, null/missing behavior, unknown-extension preservation, and exposure through `EventStoreGatewayException`. | Golden JSON tests plus exception assertions for the chosen stable extension contract. |
| Compatibility wrappers | Whether `src/Hexalith.EventStore/Models` wrappers remain frozen, delegate to Contracts DTOs, or are marked obsolete. | Docs and tests proving wrappers do not become the preferred downstream integration surface. |
| Fake parity | Minimum fake/builder scenarios required so downstream tests do not pass against behavior the real gateway cannot provide. | Testing package tests for each approved fake path and request-capture behavior. |
| Package boundary | Allowed public package/type dependencies for downstream gateway consumers. | Dependency check proving Contracts has no runtime Hexalith/EventStore dependency and Client public gateway methods use Contracts-owned request/response DTOs. |

ProblemDetails extension review must start from the current documented and
observed candidates: `correlationId`, `tenantId`, `errors`, `reason`, and
`retryAfter`. The decision record may defer an extension only by naming the
consumer impact, documentation wording, and the later story that will own it.

## Current Implementation Intelligence

- `src/Hexalith.EventStore.Contracts/Commands/SubmitCommandRequest.cs` already exposes the API-facing command request record with `messageId`, `tenant`, `domain`, `aggregateId`, `commandType`, `payload`, optional `correlationId`, and optional `extensions`.
- `src/Hexalith.EventStore.Contracts/Commands/SubmitCommandResponse.cs` already exposes the accepted command response with `correlationId`.
- `src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs` already exposes query gateway fields plus `projectionType`, `entityId`, and `projectionActorType`; confirm whether all fields are intended public contract or compatibility bridge before documenting them as stable.
- `src/Hexalith.EventStore.Contracts/Queries/SubmitQueryResponse.cs` includes `success` and `errorMessage`. The current `EventStoreGatewayClient` does not appear to treat `success: false` as a typed gateway failure before returning the result; this is a contract-closure risk.
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs` maps HTTP 304 to `EventStoreQueryResult(IsNotModified: true)`, parses JSON ProblemDetails into `EventStoreGatewayException`, and trims quotes from response ETags before exposing `ETag`.
- `EventStoreGatewayClient.GetETag` strips ETag quotes while request `ifNoneMatch` values are passed through unchanged. Dev must decide and document whether client-facing ETags are normalized tokens or HTTP header-ready quoted values, then update docs/tests consistently.
- `EventStoreGatewayException` only captures `statusCode`, `title`, `type`, `detail`, and `correlationId`. It currently ignores common ProblemDetails extensions such as `tenantId`, `errors`, `reason`, `retryAfter`, or future stable reason codes.
- `FakeEventStoreGatewayClient` records submitted commands and submitted queries with `IfNoneMatch`, can return configured command/query results, and can throw configured command/query exceptions. It does not provide first-class builders for validation, auth, conflict, stale/degraded, unavailable, or ProblemDetails paths.
- `docs/reference/nuget-packages.md` still describes Contracts primarily as domain types and Client primarily as domain processor registration. It must be updated to describe gateway DTO, gateway client, and gateway testing ownership.
- `docs/reference/command-api.md` and `docs/reference/query-api.md` document REST behavior but do not yet give downstream package guidance or state that service wrappers are compatibility-only.
- `_bmad-output/implementation-artifacts/12-7-command-api-reference.md` contains recent review corrections for command docs, including ProblemDetails `errors` as `Dictionary<string, string>`, `tenantId` extension behavior, response header/body examples, and Replay correlation diagnostics. Reuse those corrections instead of inventing a new error shape.

## Acceptance Criteria

1. **Contracts package owns stable gateway DTOs.**
   - Given a downstream bounded context references `Hexalith.EventStore.Contracts`
   - When it builds command and query gateway requests
   - Then API-facing command/query request and response DTOs needed for `POST /api/v1/commands` and `POST /api/v1/queries` are available from Contracts.
   - And any service-assembly wrapper DTOs are documented as compatibility-only, not the preferred integration surface.
   - And generated API docs expose the Contracts types as public API surface.
   - And tests prove camelCase JSON wire names for command and query DTOs remain stable.

2. **Client package owns high-level command and query calls.**
   - Given a downstream service uses `Hexalith.EventStore.Client`
   - When it submits a command through `SubmitCommandAsync`
   - Then the client posts the Contracts request to the configured command path, preserves or supplies the intended correlation behavior, supports cancellation, and returns a typed `SubmitCommandResponse`.
   - When it submits a query through `SubmitQueryAsync`
   - Then the client posts the Contracts request to the configured query path, sends `If-None-Match` when supplied, maps HTTP 304 to a typed not-modified result, returns typed payloads on success, and supports cancellation.
   - And client tests pin ETag input/output quoting policy so downstream callers do not have to guess whether `ETag` includes HTTP quotes.
   - And a `SubmitQueryResponse.Success == false` envelope is mapped or documented intentionally; the story must not leave ambiguous whether a failed semantic query envelope is a successful client result.
   - And the selected `success:false` behavior is recorded before client code changes and covered for both typed and untyped query methods.
   - And ETag tests cover quoted, unquoted, weak, missing, empty, and malformed cases or explicitly document unsupported cases.

3. **ProblemDetails mapping is stable enough for downstream handling.**
   - Given EventStore returns an RFC 7807 or RFC 9457 ProblemDetails response
   - When `EventStoreGatewayClient` receives a non-success status
   - Then callers can inspect status, title, type, detail, correlation id, and stable extension values needed by downstream retry, auth, validation, and display logic.
   - And validation errors preserve the documented `errors: Dictionary<string, string>` shape.
   - And auth, validation, conflict, rate-limit, unavailable, stale/degraded, and unsupported paths have deterministic tests or explicit deferred entries if this story cannot freeze them.
   - And client behavior for raw framework 413 and 415 responses is documented or tested without pretending they are ProblemDetails.
   - And stable extensions are exposed through `EventStoreGatewayException` or explicitly deferred with a named downstream impact.
   - And unknown ProblemDetails extensions are either preserved or intentionally ignored by documented policy.

4. **Testing package provides deterministic gateway fakes/builders.**
   - Given a downstream service test suite uses `Hexalith.EventStore.Testing`
   - When it needs to exercise gateway behavior without a live EventStore
   - Then it can configure success, validation failure, authentication/authorization failure, conflict, not-modified, stale/degraded, unavailable, and unexpected ProblemDetails-style paths through fakes/builders.
   - And the fake records submitted command/query requests, optional ETags, and cancellation behavior without relying on real DAPR, Aspire, or HTTP infrastructure.
   - And fake/builder APIs reuse Contracts and Client result types rather than introducing duplicate DTOs.
   - And required fake paths include command accepted, command ProblemDetails failure, query success, query `success:false`, query ProblemDetails failure, ETag present/absent/normalized, cancellation, and deterministic request capture.

5. **Package and API documentation align with public ownership.**
   - Given generated docs and reference guides are refreshed
   - When a Parties developer reads package guidance
   - Then `docs/reference/nuget-packages.md` clearly states that downstream gateway consumers use Contracts for wire DTOs, Client for HTTP convenience calls, and Testing for deterministic gateway doubles.
   - And `docs/reference/command-api.md` and `docs/reference/query-api.md` cross-link to the package guidance.
   - And docs explicitly warn downstream services not to reference `Hexalith.EventStore` or `Hexalith.EventStore.Server` for gateway DTOs.
   - And docs distinguish compatibility wrappers from public package contracts.

6. **Regression and evidence are recorded before review.**
   - Contracts, Client, and Testing focused tests pass individually.
   - Documentation validation is run where available.
   - If generated API docs are refreshed, the generator command and changed generated files are recorded.
   - Public package dependency evidence proves downstream-facing tests do not reference `Hexalith.EventStore.Server` or runtime `Hexalith.EventStore` models for gateway DTOs.
   - Dev Agent Record, File List, Verification Status, and Change Log are updated before moving the story to review.

## Tasks / Subtasks

- [x] **ST0 - Baseline public gateway surface and classify gaps.** (AC: 1, 2, 3, 4, 5)
    - [x] Read this story, Epic 22, ADR-P6, FR83-FR86, and `_bmad-output/implementation-artifacts/12-7-command-api-reference.md`.
    - [x] Record the Gateway Contract Decision Gate table before production code edits, covering query `success:false`, ETag normalization, ProblemDetails extensions, compatibility wrappers, fake parity, and package boundary evidence.
    - [x] Treat undecided query failure behavior, ETag normalization, or ProblemDetails extension ownership as a story blocker rather than implementing a private convention.
    - [x] Inventory public DTOs in `src/Hexalith.EventStore.Contracts/Commands`, `src/Hexalith.EventStore.Contracts/Queries`, and compatibility wrappers in `src/Hexalith.EventStore/Models`.
    - [x] Inventory client behavior in `src/Hexalith.EventStore.Client/Gateway`.
    - [x] Inventory existing gateway fakes/builders and tests in `src/Hexalith.EventStore.Testing` and `tests/Hexalith.EventStore.Testing.Tests`.
    - [x] Record a gap table covering Contracts, Client, Testing, docs, generated API docs, and compatibility wrappers before code edits.
    - [x] Record dependency-boundary evidence showing Contracts stays free of EventStore runtime dependencies and Client gateway public methods consume Contracts-owned DTOs.

- [x] **ST1 - Close or document Contracts package gaps.** (AC: 1, 3)
    - [x] Ensure command/query request and response DTOs required for Story 22.1 live in Contracts and serialize with stable camelCase names.
    - [x] Add or harden Contracts tests for query DTO JSON shape, optional fields, `success`/`errorMessage`, and any public ProblemDetails extension helper added by this story.
    - [x] Avoid moving projection adapter contracts in this story; Story 22.2 owns `QueryEnvelope`, `QueryResult`, generic projection actor contract, and malformed projection taxonomy.
    - [x] Avoid adding replay/read DTO scope unless it is already present and only needs package ownership documentation; Story 22.6 owns new stream read/replay APIs.

- [x] **ST2 - Harden EventStore gateway client behavior.** (AC: 2, 3)
    - [x] Pin ETag quote normalization policy in tests and docs.
    - [x] Cover quoted, unquoted, weak, missing, empty, and malformed ETag inputs or record unsupported cases in the decision table.
    - [x] Verify cancellation token propagation for command and query methods.
    - [x] Verify ProblemDetails parsing for stable extensions required by downstream callers.
    - [x] Decide and implement/document behavior for `SubmitQueryResponse.Success == false`.
    - [x] Cover HTTP 2xx `success:false`, HTTP non-success ProblemDetails, malformed body, empty payload, and typed query deserialization failure paths.
    - [x] Ensure non-JSON, 413, and 415 responses fail predictably without fake ProblemDetails assumptions.

- [x] **ST3 - Expand deterministic Testing support.** (AC: 4)
    - [x] Add gateway result/exception builders if direct property mutation on `FakeEventStoreGatewayClient` is too weak for downstream tests.
    - [x] Provide easy configuration for success, validation, auth, conflict, not-modified, stale/degraded, unavailable, and unexpected paths.
    - [x] Provide explicit scenarios for command accepted, command ProblemDetails failure, query success, query `success:false`, query ProblemDetails failure, ETag present/absent/normalized, cancellation, and deterministic request capture.
    - [x] Preserve existing fake request recording behavior.
    - [x] Add focused tests for every new fake/builder path.

- [x] **ST4 - Align package and endpoint docs.** (AC: 5)
    - [x] Update `docs/reference/nuget-packages.md` with gateway consumer package guidance.
    - [x] Update `docs/reference/command-api.md` with Contracts/Client/Testing ownership and compatibility-wrapper notes.
    - [x] Update `docs/reference/query-api.md` with gateway client, ETag, not-modified, query failure-envelope, and package ownership guidance.
    - [x] Update generated API docs if the public API generator is part of the repository workflow for changed public types.

- [x] **ST5 - Validate and record evidence.** (AC: 6)
    - [x] Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests`.
    - [x] Run `dotnet test tests/Hexalith.EventStore.Client.Tests`.
    - [x] Run `dotnet test tests/Hexalith.EventStore.Testing.Tests`.
    - [x] Record the dependency-boundary check proving downstream-facing gateway tests compile against Contracts, Client, and Testing without Server/runtime model references.
    - [x] Run markdown or docs validation where available.
    - [x] Update Dev Agent Record, File List, Verification Status, and Change Log.

## Developer Notes

Architecture and product guardrails:

- ADR-P6 is the controlling decision: public wire contracts in Contracts, HTTP convenience APIs in Client, deterministic gateway fakes/builders in Testing, and runtime internals in Server/EventStore.
- ADR-P7, ADR-P8, and ADR-P9 are adjacent but mostly out of this story. Do not absorb projection adapter relocation, tenant/RBAC enforcement, query policy taxonomy, publishing guarantees, replay APIs, or payload protection into Story 22.1.
- FR83-FR86 are the primary requirement scope. FR87-FR104 define downstream expectations that should inform naming and extension compatibility, not expand this story's implementation scope.
- Compatibility wrappers may stay if runtime controllers still use them, but docs must steer new downstream code to Contracts.
- Public package behavior affects Parties and future Hexalith modules. Treat DTO and client behavior as public API.
- `SubmitCommandAsync` and `SubmitQueryAsync` are the only high-level Client methods required by this story. Broader gateway convenience APIs are out of scope unless they are only docs/examples around the existing methods.
- Query `success:false`, ETag normalization, and stable ProblemDetails extension exposure are public SemVer decisions. The implementation must use the decision record as the source of truth.
- If Product/Architecture cannot approve a stable behavior for one of those decisions during ST0, record the blocker and do not substitute a private client/fake convention.

Implementation traps to avoid:

- Do not duplicate DTOs in downstream samples or docs. Use `Hexalith.EventStore.Contracts.Commands` and `Hexalith.EventStore.Contracts.Queries`.
- Do not make downstream services reference `Hexalith.EventStore.Server` just to get query, ProblemDetails, or fake behavior.
- Do not silently change existing request JSON field names; current docs and tests expect camelCase.
- Do not document unsupported validation/auth/query-failure behavior as complete unless there is an executable test or generated API proof.
- Do not invent a new error envelope if existing ProblemDetails and `EventStoreGatewayException` can be extended coherently.
- Do not treat the current `SubmitQueryResponse.Success` field as harmless. It is either a stable semantic failure contract or needs documented migration/defer handling.

Current file intelligence:

- Contracts DTOs:
    - `src/Hexalith.EventStore.Contracts/Commands/SubmitCommandRequest.cs`
    - `src/Hexalith.EventStore.Contracts/Commands/SubmitCommandResponse.cs`
    - `src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs`
    - `src/Hexalith.EventStore.Contracts/Queries/SubmitQueryResponse.cs`
    - `src/Hexalith.EventStore.Contracts/Validation/ValidateCommandRequest.cs`
    - `src/Hexalith.EventStore.Contracts/Validation/ValidateQueryRequest.cs`
- Compatibility wrappers:
    - `src/Hexalith.EventStore/Models/SubmitCommandRequest.cs`
    - `src/Hexalith.EventStore/Models/SubmitCommandResponse.cs`
- Client gateway:
    - `src/Hexalith.EventStore.Client/Gateway/IEventStoreGatewayClient.cs`
    - `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs`
    - `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayException.cs`
    - `src/Hexalith.EventStore.Client/Gateway/EventStoreQueryResult.cs`
    - `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClientOptions.cs`
    - `src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs`
- Testing:
    - `src/Hexalith.EventStore.Testing/Fakes/FakeEventStoreGatewayClient.cs`
    - `tests/Hexalith.EventStore.Testing.Tests/Fakes/FakeEventStoreGatewayClientTests.cs`
- Focused tests:
    - `tests/Hexalith.EventStore.Contracts.Tests/Commands/SubmitCommandRequestTests.cs`
    - `tests/Hexalith.EventStore.Contracts.Tests/Commands/SubmitCommandResponseTests.cs`
    - `tests/Hexalith.EventStore.Contracts.Tests/Queries/SubmitQueryRequestTests.cs`
    - `tests/Hexalith.EventStore.Contracts.Tests/Queries/SubmitQueryResponseTests.cs`
    - `tests/Hexalith.EventStore.Client.Tests/Gateway/EventStoreGatewayClientTests.cs`

Testing standards:

- Use xUnit v3, Shouldly, and existing test-project patterns.
- Run test projects individually per repository guidance.
- Do not rely on integration tests for this story unless a runtime behavior gap cannot be proven by unit tests. Story 22.1 should mostly be Contracts, Client, Testing, and docs.
- `Hexalith.EventStore.Server.Tests` has a known pre-existing CA2007 warning-as-error issue in this workspace; only run it if this story actually touches Server behavior and record the known blocker if it reproduces.

## Files Likely Touched

- `src/Hexalith.EventStore.Contracts/Commands/SubmitCommandRequest.cs`
- `src/Hexalith.EventStore.Contracts/Commands/SubmitCommandResponse.cs`
- `src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs`
- `src/Hexalith.EventStore.Contracts/Queries/SubmitQueryResponse.cs`
- `src/Hexalith.EventStore.Contracts/Validation/ValidateCommandRequest.cs`
- `src/Hexalith.EventStore.Contracts/Validation/ValidateQueryRequest.cs`
- `src/Hexalith.EventStore.Client/Gateway/IEventStoreGatewayClient.cs`
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs`
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayException.cs`
- `src/Hexalith.EventStore.Client/Gateway/EventStoreQueryResult.cs`
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventStoreGatewayClient.cs`
- `src/Hexalith.EventStore.Testing/Builders/*` only if gateway builders are added.
- `docs/reference/nuget-packages.md`
- `docs/reference/command-api.md`
- `docs/reference/query-api.md`
- `docs/reference/api/**` only if generated public API docs are refreshed.
- Related focused test files under `tests/Hexalith.EventStore.Contracts.Tests`, `tests/Hexalith.EventStore.Client.Tests`, and `tests/Hexalith.EventStore.Testing.Tests`.

## Out of Scope

- Moving projection adapter contracts, `QueryEnvelope`, `QueryResult`, or generic projection actor behavior out of Server. Story 22.2 owns that.
- Implementing gateway-owned tenant lifecycle, membership, role, or permission validation. Story 22.3 owns that.
- Freezing full query paging, blank search, filter, stale/degraded, timeout, and malformed projection response policy. Story 22.4 owns that, except where this story needs client/fake placeholders.
- Pub/sub ordering, retry, outbox/drain, and dead-letter deployment matrices. Story 22.5 owns that.
- Stream read/replay APIs and projection rebuild checkpoints. Story 22.6 owns that.
- Payload/snapshot protection, crypto-shredding, unreadable data behavior, or redaction across operational surfaces. Stories 22.7a through 22.7d own that.
- Broad server pipeline rewrites, DAPR access-control changes, AppHost topology changes, or Parties repository changes.

## References

- `_bmad-output/planning-artifacts/epics.md#Epic 22: Public Gateway and Downstream Integration Contracts`
- `_bmad-output/planning-artifacts/prd.md#Public Gateway and Downstream Integration Contracts - v1.1 (FR83-FR104)`
- `_bmad-output/planning-artifacts/architecture.md#ADR-P6: Public Gateway Contracts Live in Contracts, Client, and Testing Packages`
- `_bmad-output/planning-artifacts/architecture.md#Downstream Public Gateway Boundary`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-requirements-gaps-current.md`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12.md`
- `_bmad-output/project-context.md`
- `_bmad-output/process-notes/story-creation-lessons.md#L08 - Party Review Vs. Elicitation`
- `_bmad-output/implementation-artifacts/12-7-command-api-reference.md`
- `docs/reference/nuget-packages.md`
- `docs/reference/command-api.md`
- `docs/reference/query-api.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-12T19:02:52Z - Pre-dev hardening preflight passed via `_bmad-output/process-notes/predev-preflight-latest.json`.
- 2026-05-12T21:05:26+02:00 - Story creation context gathered from Epic 22, PRD FR83-FR86, architecture ADR-P6/P7/P8/P9, downstream public gateway boundary, current Contracts/Client/Testing gateway files, docs, recent commits, and lessons ledger.
- 2026-05-13T07:35:34+02:00 - Baseline Aspire attempt: AppHost build succeeded with 0 warnings/errors, but runtime start failed before resource registration because DAPR CLI was not found on PATH. No running AppHost resources were available for inspection.
- 2026-05-13T08:18:00+02:00 - API docs refresh notes: `dotnet build --configuration Release -p:ApiReferenceBuild=true` is blocked by pre-existing XML documentation errors in `Admin.Mcp` and `Server`; `dotnet tool restore` is blocked by missing `hexalith.eventstore.admin.cli` version `1.0.0`. Used globally available `dotnet defaultdocumentation` 1.2.3 and generated Contracts/Client/Testing API docs from built assemblies; Testing required `-p:NoWarn=CS1574` for an existing unresolved cref in `FakeDomainServiceInvoker`.

### Gateway Contract Decision Record

| Area | Decision | Evidence / implementation target |
| --- | --- | --- |
| Query semantic failure | HTTP `2xx` with `SubmitQueryResponse.Success == false` is a semantic gateway failure. Both typed and untyped `SubmitQueryAsync` throw `EventStoreGatewayException` with `StatusCode = 200`, `Title = "Query semantic failure"`, `Detail = ErrorMessage`, and `CorrelationId` from the envelope. | Current `SubmitQueryResponse` already models `success` and `errorMessage`; client tests will cover HTTP `2xx success:false`, malformed body, empty body, non-success ProblemDetails, and typed deserialization failure. |
| ETag normalization | Client-facing ETags are normalized strong tokens without surrounding quotes. `If-None-Match` accepts an unquoted token or a quoted strong ETag and sends a quoted HTTP header value. Missing/empty values omit the header. Weak or malformed ETags are unsupported and rejected before sending. | Current client strips response quotes but passes request values through. Client tests will pin quoted, unquoted, weak, missing, empty, and malformed request inputs plus response exposure. |
| ProblemDetails extensions | Stable extension names are `correlationId`, `tenantId`, `errors`, `reason`, and `retryAfter`. `EventStoreGatewayException` exposes typed properties for those values and preserves non-standard/unknown extensions as JSON values for downstream diagnostics. Missing/null stable extensions surface as null or empty dictionaries. | Current exception only exposes `correlationId`; Story 12.7 confirms `errors` is `Dictionary<string, string>` and raw 413/415 responses are not ProblemDetails. |
| Compatibility wrappers | `src/Hexalith.EventStore/Models` command wrappers remain compatibility-only delegates to Contracts DTOs. They are not marked obsolete in this story to avoid adding warning noise, but package docs must steer downstream code to Contracts. | Current wrappers inherit from `Hexalith.EventStore.Contracts.Commands` request/response records. Docs currently present packages mainly as domain/runtime packages and need gateway boundary wording. |
| Fake parity | Add deterministic gateway fake configuration helpers/builders for accepted command, command ProblemDetails failure, query success, query semantic failure, query ProblemDetails failure, not-modified, stale/degraded, unavailable, auth, conflict, ETag, cancellation, and request capture. | Current `FakeEventStoreGatewayClient` supports raw configured result/exception and records requests, but only has tests for happy command, typed query, and not-modified. |
| Package boundary | Contracts must remain free of `Hexalith.EventStore` and `Hexalith.EventStore.Server` references. Client gateway public methods must consume Contracts request/response DTOs. Testing gateway fake APIs must reuse Contracts and Client types, even though the broader Testing package still has Server references for non-gateway fakes. | `Contracts.csproj` has no EventStore runtime project reference; `IEventStoreGatewayClient` uses Contracts DTOs; `FakeEventStoreGatewayClient` uses Contracts DTOs and Client result/exception types. Existing Client/Testing test projects have broader runtime references outside the gateway slice. |

### ST0 Gap Inventory

| Surface | Current state | Gap / action |
| --- | --- | --- |
| Contracts DTOs | Command and query gateway request/response DTOs exist in Contracts. Command request/response camelCase tests exist; query request lacks a camelCase JSON test for all public optional fields. | Add/harden query DTO JSON shape tests and keep Story 22.2 projection adapter scope out of this story. |
| Client gateway | `SubmitCommandAsync`, `SubmitQueryAsync`, typed query, 304 mapping, basic ProblemDetails parsing, and response ETag quote stripping exist. | Add tests and implementation for semantic query failure, request ETag normalization/rejection, cancellation evidence, stable ProblemDetails extensions, malformed/empty body behavior, non-JSON 413/415 behavior, and typed deserialization failures. |
| Testing gateway fake | Fake records commands/queries and supports configured result/exception. | Add deterministic scenario helpers/builders and tests for required gateway paths while preserving request capture. |
| Docs | Command and query endpoint docs exist; NuGet guide still frames Contracts/Client/Testing mostly around domain/runtime use. | Update package guide and endpoint refs with gateway ownership, ETag policy, query `success:false`, ProblemDetails extension policy, and compatibility-wrapper guidance. |
| Generated API docs | API docs workflow runs on release tags via DefaultDocumentation. Current checked-in generated docs do not include the new gateway DTO pages. | No local generated docs refresh in this story unless public API doc generation is explicitly run; record workflow evidence and changed generated files if refreshed. |
| Compatibility wrappers | Runtime command wrappers inherit from Contracts command DTOs. No query compatibility wrappers found under `src/Hexalith.EventStore/Models`. | Document wrappers as compatibility-only and keep downstream examples on Contracts DTOs. |
| Dependency boundary | Contracts has no EventStore runtime project reference; Client references Contracts; Testing references Client/Contracts/Server for broad fake coverage. | Record that the gateway fake and gateway client tests use Contracts/Client types; do not claim the whole Testing package is runtime-free. |

### Implementation Plan

- Add failing focused tests first for each ST1-ST3 behavior before production edits.
- Keep public decisions backward compatible: optional exception properties, normalized ETag helper behavior, and fake helper methods without removing existing property configuration.
- Limit documentation changes to package ownership, endpoint guidance, and policy decisions made above.

### Completion Notes List

- Story created and marked ready-for-dev by the BMAD pre-dev hardening automation.
- Epic 22 was moved from `backlog` to `in-progress` because this is the first Epic 22 story created.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, docs, generated API docs, or submodules.
- Party-mode review has NOT yet been run for this story.
- Advanced elicitation has NOT yet been run for this story.
- 2026-05-13 party-mode review applied story hardening for the gateway contract decision gate, query semantic failure behavior, ETag normalization, ProblemDetails extensions, fake parity, compatibility wrapper policy, package-boundary evidence, and focused validation obligations.
- 2026-05-13 ST0 completed: recorded public contract decisions, baseline gap inventory, dependency-boundary evidence, and Aspire baseline status before production code edits.
- 2026-05-13 ST1 completed: added public gateway ProblemDetails extension-name constants in Contracts, hardened query request JSON shape coverage, and verified `Hexalith.EventStore.Contracts.Tests` 297/297.
- 2026-05-13 ST2 completed: hardened gateway client semantic query failures, strong ETag normalization/rejection, ProblemDetails extension exposure, raw 413 handling, malformed body handling, typed deserialization failures, and cancellation behavior; verified `Hexalith.EventStore.Client.Tests` 384/384.
- 2026-05-13 ST3 completed: added deterministic gateway exception builder and fluent fake configuration for command accepted/failure, query success/semantic failure/failure/not-modified, ETag capture, and cancellation behavior; verified `Hexalith.EventStore.Testing.Tests` 91/91.
- 2026-05-13 ST4 completed: updated package and endpoint reference docs for gateway ownership, compatibility wrappers, query semantic failure, ETag policy, ProblemDetails extensions, and deterministic gateway testing support; refreshed generated API docs for Contracts, Client, and Testing changed public surface.
- 2026-05-13 ST5 completed: Contracts 297/297, Client 384/384, Testing 91/91, and Sample 74/74 test projects passed; markdownlint passed for touched reference docs; dependency-boundary grep found no Server/runtime model references in the gateway-slice source/tests; `git diff --check` passed with line-ending warnings only.

### File List

- `_bmad-output/implementation-artifacts/22-1-gateway-command-query-contract-closure-and-package-docs.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/process-notes/predev-hardening-runs.log`
- `src/Hexalith.EventStore.Contracts/Problems/GatewayProblemDetailsExtensions.cs`
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs`
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayException.cs`
- `src/Hexalith.EventStore.Testing/Builders/EventStoreGatewayExceptionBuilder.cs`
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventStoreGatewayClient.cs`
- `tests/Hexalith.EventStore.Contracts.Tests/Problems/GatewayProblemDetailsExtensionsTests.cs`
- `tests/Hexalith.EventStore.Contracts.Tests/Queries/SubmitQueryRequestTests.cs`
- `tests/Hexalith.EventStore.Client.Tests/Gateway/EventStoreGatewayClientTests.cs`
- `tests/Hexalith.EventStore.Testing.Tests/Builders/EventStoreGatewayExceptionBuilderTests.cs`
- `tests/Hexalith.EventStore.Testing.Tests/Fakes/FakeEventStoreGatewayClientTests.cs`
- `docs/reference/nuget-packages.md`
- `docs/reference/command-api.md`
- `docs/reference/query-api.md`
- `docs/reference/api/Hexalith.EventStore.Contracts/**`
- `docs/reference/api/Hexalith.EventStore.Client/**`
- `docs/reference/api/Hexalith.EventStore.Testing/**`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Preflight passed before story creation.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, or submodules.
- YAML validation passed for `_bmad-output/implementation-artifacts/sprint-status.yaml`.
- `git diff --check` passed for the story artifact, sprint status, and run log with line-ending conversion warnings only.
- `npx markdownlint-cli2 _bmad-output/implementation-artifacts/22-1-gateway-command-query-contract-closure-and-package-docs.md` passed with 0 errors.
- Party-mode review completed on 2026-05-13 and is recorded below.
- Party-mode story hardening passed `git diff --check` and `npx markdownlint-cli2` on 2026-05-13.
- Advanced elicitation has NOT yet been run for this story.
- Aspire baseline: AppHost build succeeded, but runtime start failed before resource registration because DAPR CLI was not found on PATH; no Aspire resources were available for inspection.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj` passed: 297/297.
- `dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj` passed: 384/384.
- `dotnet test tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj` passed: 91/91.
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj` passed: 74/74.
- `npx markdownlint-cli2 docs/reference/nuget-packages.md docs/reference/command-api.md docs/reference/query-api.md` passed with 0 errors.
- Dependency-boundary grep across gateway-slice source/tests found no `Hexalith.EventStore.Server`, `Hexalith.EventStore.Models`, or root runtime-model namespace references.
- Generated API docs refreshed for `Hexalith.EventStore.Contracts`, `Hexalith.EventStore.Client`, and `Hexalith.EventStore.Testing` using global `dotnet defaultdocumentation` 1.2.3. Full release API-doc build remains blocked by pre-existing XML-doc errors in `Admin.Mcp` and `Server`; local `dotnet tool restore` remains blocked by missing `hexalith.eventstore.admin.cli` version `1.0.0`.
- `git diff --check` passed with line-ending warnings only.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-13 | 0.2 | Applied party-mode review hardening for public contract decisions, query failure semantics, ETag policy, ProblemDetails extensions, fake parity, and package-boundary validation. | Codex automation |
| 2026-05-13 | 0.3 | Started implementation, completed ST0 decision record and gap inventory, and moved sprint tracking to in-progress. | GPT-5 Codex |
| 2026-05-13 | 1.0 | Implemented gateway contract closure across Contracts, Client, Testing, docs, generated API docs, and validation evidence; moved story to review. | GPT-5 Codex |
| 2026-05-12 | 0.1 | Created ready-for-dev story for public gateway command/query contract closure and package documentation. | Codex automation |

### Review Findings

- [x] [Review][Decision] success:false exception drops response ETag — Resolved: drop intentionally. The decision record contract is StatusCode=200/Title="Query semantic failure"/Detail/CorrelationId only. ETag on a failed semantic response has no defined meaning and is omitted. No code change required.
- [x] [Review][Decision] Stale builder uses 409 — Resolved: changed to 503. Stale projection is a service-level degradation, distinct from write-conflict Conflict (409). `EventStoreGatewayExceptionBuilder.Stale` now returns status 503. Test updated accordingly.
- [x] [Review][Patch] GetExtensions leaks stable extension keys into Extensions dict [HIGH] — Fixed: renamed to IsKnownProblemDetailsProperty and added all 5 stable extension names to the exclusion filter. [`EventStoreGatewayClient.cs`]
- [x] [Review][Patch] GetETag called before IsSuccessStatusCode check — weak ETag on error response throws wrong exception [HIGH] — Fixed: restructured SubmitQueryAsync to check 304/non-success before calling GetETag. [`EventStoreGatewayClient.cs`]
- [x] [Review][Patch] Empty JSON retryAfter clobbers valid HTTP Retry-After header [MEDIUM] — Fixed: replaced `?? retryAfter` with explicit `!string.IsNullOrWhiteSpace()` guard. [`EventStoreGatewayClient.cs`]
- [x] [Review][Patch] EmptyErrors/EmptyExtensions shared mutable singleton [MEDIUM] — Fixed: replaced with FrozenDictionary<,>.Empty; added innerException constructor parameter; removed stale private statics. [`EventStoreGatewayException.cs`]
- [x] [Review][Patch] GetErrors silently drops error key when array contains only non-string items [MEDIUM] — Fixed: added JoinStringArrayOrRaw helper that falls back to GetRawText() when no string items exist. [`EventStoreGatewayClient.cs`]
- [x] [Review][Patch] FakeEventStoreGatewayClient.SubmitQueryAsync<T> throws raw JsonException instead of wrapping in EventStoreGatewayException [MEDIUM] — Fixed: Deserialize<T> call is now wrapped in try/catch(JsonException) → EventStoreGatewayException. [`FakeEventStoreGatewayClient.cs`]
- [x] [Review][Patch] Builder Validation factory lacks null guard on errors parameter [LOW] — Fixed: added ArgumentNullException.ThrowIfNull(errors) in block-body Validation factory. [`EventStoreGatewayExceptionBuilder.cs`]
- [x] [Review][Patch] No test for HTTP-header-only Retry-After fallback path [LOW] — Fixed: added SubmitCommandAsync_WithNonJsonResponseAndRetryAfterHeader_ThrowsGatewayExceptionWithRetryAfter test. [`EventStoreGatewayClientTests.cs`]
- [x] [Review][Patch] NormalizeIfNoneMatch rejects wildcard * with misleading error message [LOW] — Fixed: wildcard now has distinct message "Wildcard If-None-Match is not supported; provide a strong ETag token." [`EventStoreGatewayClient.cs`]
- [x] [Review][Patch] Unknown ProblemDetails extension preservation policy not documented [LOW] — Fixed: query-api.md updated with explicit statement that unknown extensions are preserved in EventStoreGatewayException.Extensions. [`docs/reference/query-api.md`]
- [x] [Review][Patch] JsonException catch blocks do not chain inner exception [LOW] — Fixed: all JsonException catches now chain via innerException constructor parameter. [`EventStoreGatewayClient.cs`, `EventStoreGatewayException.cs`]
- [x] [Review][Patch] Builder _statusCode/_title fields not readonly [LOW] — Fixed: both fields marked readonly. [`EventStoreGatewayExceptionBuilder.cs`]
- [x] [Review][Defer] GatewayProblemDetailsExtensions class name suggests extension methods — rename to GatewayProblemDetailsPropertyNames would clarify but is a public Contracts API breaking change; defer to next major — deferred, pre-existing design
- [x] [Review][Defer] Extensions property exposes JsonElement (STJ hard dependency) — architectural decision per decision record; breaking change to alter — deferred, pre-existing design
- [x] [Review][Defer] Errors dict copied with StringComparer.Ordinal — only affects external callers with non-Ordinal dicts; no internal path produces that — deferred, pre-existing design
- [x] [Review][Defer] SubmitQueryAsync<T> throws on legitimate null payload — null payload with success:true is a server bug; acceptable sentinel — deferred, pre-existing design
- [x] [Review][Defer] retryAfter format contract unspecified (HTTP delta-seconds vs ISO 8601 from JSON) — document in query-api.md when adding retryAfter test — deferred, pre-existing design
- [x] [Review][Defer] No per-category fake test for auth-401/authz-403/conflict-409/rate-limit-429 — builder tests cover each status code; fake mechanism proven by existing tests; combined coverage is sufficient — deferred, pre-existing design

## Party-Mode Review

- Date/time: 2026-05-13T00:09:15+02:00
- Selected story key: `22-1-gateway-command-query-contract-closure-and-package-docs`
- Command/skill invocation used:
  `/bmad-party-mode 22-1-gateway-command-query-contract-closure-and-package-docs; review;`
- Participating BMAD agents: John (Product Manager), Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor)
- Findings summary:
    - Query `success:false` behavior was a public client contract risk and needed a blocking decision before implementation.
    - ETag quote normalization and weak/missing/malformed ETag handling needed explicit tests and documentation before downstream consumers depend on the client.
    - ProblemDetails extension names, casing, CLR types, missing-value behavior, and exception exposure were under-specified for SemVer-stable downstream handling.
    - Compatibility wrappers and package dependency boundaries needed mechanical evidence so runtime assemblies do not leak back into downstream gateway consumers.
    - Testing fakes/builders needed a minimum parity matrix for command/query success, semantic failure, ProblemDetails failure, ETag, cancellation, and request capture paths.
- Changes applied:
    - Added a Gateway Contract Decision Gate with required decision areas, minimum evidence, and ProblemDetails extension candidates.
    - Tightened AC2-AC6 with query semantic failure, ETag, ProblemDetails, fake parity, and dependency-boundary evidence obligations.
    - Updated ST0-ST5 with blocking pre-edit decisions, dependency checks, client failure cases, fake scenario coverage, and validation evidence.
    - Added Developer Notes clarifying the limited Client method scope and prohibiting private conventions when Product/Architecture decisions are missing.
    - Updated Verification Status and Change Log with this dated party-mode review.
- Findings deferred:
    - Projection adapter contracts remain deferred to Story 22.2.
    - Tenant/RBAC authorization semantics remain deferred to Story 22.3.
    - Full query behavior policy and error taxonomy remain deferred to Story 22.4, except for the `success:false` client contract decision required here.
    - Publishing guarantees, stream read/replay APIs, and payload protection remain deferred to Stories 22.5 through 22.7.
    - Exact Product/Architecture decisions for query failure, ETag normalization, ProblemDetails extension exposure, compatibility wrappers, and fake parity must be recorded during ST0 before production code edits.
- Final recommendation: ready-for-dev after applied story updates.
