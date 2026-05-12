# Story 22.1: Gateway Command/Query Contract Closure and Package Docs

Status: ready-for-dev

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

3. **ProblemDetails mapping is stable enough for downstream handling.**
   - Given EventStore returns an RFC 7807 or RFC 9457 ProblemDetails response
   - When `EventStoreGatewayClient` receives a non-success status
   - Then callers can inspect status, title, type, detail, correlation id, and stable extension values needed by downstream retry, auth, validation, and display logic.
   - And validation errors preserve the documented `errors: Dictionary<string, string>` shape.
   - And auth, validation, conflict, rate-limit, unavailable, stale/degraded, and unsupported paths have deterministic tests or explicit deferred entries if this story cannot freeze them.
   - And client behavior for raw framework 413 and 415 responses is documented or tested without pretending they are ProblemDetails.

4. **Testing package provides deterministic gateway fakes/builders.**
   - Given a downstream service test suite uses `Hexalith.EventStore.Testing`
   - When it needs to exercise gateway behavior without a live EventStore
   - Then it can configure success, validation failure, authentication/authorization failure, conflict, not-modified, stale/degraded, unavailable, and unexpected ProblemDetails-style paths through fakes/builders.
   - And the fake records submitted command/query requests, optional ETags, and cancellation behavior without relying on real DAPR, Aspire, or HTTP infrastructure.
   - And fake/builder APIs reuse Contracts and Client result types rather than introducing duplicate DTOs.

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
   - Dev Agent Record, File List, Verification Status, and Change Log are updated before moving the story to review.

## Tasks / Subtasks

- [ ] **ST0 - Baseline public gateway surface and classify gaps.** (AC: 1, 2, 3, 4, 5)
    - [ ] Read this story, Epic 22, ADR-P6, FR83-FR86, and `_bmad-output/implementation-artifacts/12-7-command-api-reference.md`.
    - [ ] Inventory public DTOs in `src/Hexalith.EventStore.Contracts/Commands`, `src/Hexalith.EventStore.Contracts/Queries`, and compatibility wrappers in `src/Hexalith.EventStore/Models`.
    - [ ] Inventory client behavior in `src/Hexalith.EventStore.Client/Gateway`.
    - [ ] Inventory existing gateway fakes/builders and tests in `src/Hexalith.EventStore.Testing` and `tests/Hexalith.EventStore.Testing.Tests`.
    - [ ] Record a gap table covering Contracts, Client, Testing, docs, generated API docs, and compatibility wrappers before code edits.

- [ ] **ST1 - Close or document Contracts package gaps.** (AC: 1, 3)
    - [ ] Ensure command/query request and response DTOs required for Story 22.1 live in Contracts and serialize with stable camelCase names.
    - [ ] Add or harden Contracts tests for query DTO JSON shape, optional fields, `success`/`errorMessage`, and any public ProblemDetails extension helper added by this story.
    - [ ] Avoid moving projection adapter contracts in this story; Story 22.2 owns `QueryEnvelope`, `QueryResult`, generic projection actor contract, and malformed projection taxonomy.
    - [ ] Avoid adding replay/read DTO scope unless it is already present and only needs package ownership documentation; Story 22.6 owns new stream read/replay APIs.

- [ ] **ST2 - Harden EventStore gateway client behavior.** (AC: 2, 3)
    - [ ] Pin ETag quote normalization policy in tests and docs.
    - [ ] Verify cancellation token propagation for command and query methods.
    - [ ] Verify ProblemDetails parsing for stable extensions required by downstream callers.
    - [ ] Decide and implement/document behavior for `SubmitQueryResponse.Success == false`.
    - [ ] Ensure non-JSON, 413, and 415 responses fail predictably without fake ProblemDetails assumptions.

- [ ] **ST3 - Expand deterministic Testing support.** (AC: 4)
    - [ ] Add gateway result/exception builders if direct property mutation on `FakeEventStoreGatewayClient` is too weak for downstream tests.
    - [ ] Provide easy configuration for success, validation, auth, conflict, not-modified, stale/degraded, unavailable, and unexpected paths.
    - [ ] Preserve existing fake request recording behavior.
    - [ ] Add focused tests for every new fake/builder path.

- [ ] **ST4 - Align package and endpoint docs.** (AC: 5)
    - [ ] Update `docs/reference/nuget-packages.md` with gateway consumer package guidance.
    - [ ] Update `docs/reference/command-api.md` with Contracts/Client/Testing ownership and compatibility-wrapper notes.
    - [ ] Update `docs/reference/query-api.md` with gateway client, ETag, not-modified, query failure-envelope, and package ownership guidance.
    - [ ] Update generated API docs if the public API generator is part of the repository workflow for changed public types.

- [ ] **ST5 - Validate and record evidence.** (AC: 6)
    - [ ] Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests`.
    - [ ] Run `dotnet test tests/Hexalith.EventStore.Client.Tests`.
    - [ ] Run `dotnet test tests/Hexalith.EventStore.Testing.Tests`.
    - [ ] Run markdown or docs validation where available.
    - [ ] Update Dev Agent Record, File List, Verification Status, and Change Log.

## Developer Notes

Architecture and product guardrails:

- ADR-P6 is the controlling decision: public wire contracts in Contracts, HTTP convenience APIs in Client, deterministic gateway fakes/builders in Testing, and runtime internals in Server/EventStore.
- ADR-P7, ADR-P8, and ADR-P9 are adjacent but mostly out of this story. Do not absorb projection adapter relocation, tenant/RBAC enforcement, query policy taxonomy, publishing guarantees, replay APIs, or payload protection into Story 22.1.
- FR83-FR86 are the primary requirement scope. FR87-FR104 define downstream expectations that should inform naming and extension compatibility, not expand this story's implementation scope.
- Compatibility wrappers may stay if runtime controllers still use them, but docs must steer new downstream code to Contracts.
- Public package behavior affects Parties and future Hexalith modules. Treat DTO and client behavior as public API.

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

### Completion Notes List

- Story created and marked ready-for-dev by the BMAD pre-dev hardening automation.
- Epic 22 was moved from `backlog` to `in-progress` because this is the first Epic 22 story created.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, docs, generated API docs, or submodules.
- Party-mode review has NOT yet been run for this story.
- Advanced elicitation has NOT yet been run for this story.

### File List

- `_bmad-output/implementation-artifacts/22-1-gateway-command-query-contract-closure-and-package-docs.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/process-notes/predev-hardening-runs.log`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Preflight passed before story creation.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, or submodules.
- YAML validation passed for `_bmad-output/implementation-artifacts/sprint-status.yaml`.
- `git diff --check` passed for the story artifact, sprint status, and run log with line-ending conversion warnings only.
- `npx markdownlint-cli2 _bmad-output/implementation-artifacts/22-1-gateway-command-query-contract-closure-and-package-docs.md` passed with 0 errors.
- Party-mode review has NOT yet been run for this story.
- Advanced elicitation has NOT yet been run for this story.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-12 | 0.1 | Created ready-for-dev story for public gateway command/query contract closure and package documentation. | Codex automation |
