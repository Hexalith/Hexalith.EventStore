---
title: '1.3 Generic Read Models And Query Cursors'
type: 'feature'
created: '2026-07-06'
status: 'in-review'
baseline_revision: 'b044369533c59fafa5114961764220fbd73522fa'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-1-2-domain-query-handler-routing.md'
warnings: ['oversized']
---

<intent-contract>

## Intent

**Problem:** Story 1.3's core seams already exist, but they are not yet hardened as a complete platform contract: read-model registration and production DAPR concurrency are under-tested, paging metadata cannot explicitly carry has-more evidence, and the generic query gateway still rejects every cursor before domain handlers or projection adapters can validate it safely.

**Approach:** Finish the seams additively: prove read-model store/write-policy behavior and DI registration, add authoritative has-more paging evidence, allow opaque cursor paging through the generic query path while preserving cursor+offset validation, and ensure invalid-cursor failures produce support-safe `query_invalid_page` ProblemDetails without exposing cursor internals.

## Boundaries & Constraints

**Always:** Keep `IReadModelStore`, `ReadModelWritePolicy`, `IQueryCursorCodec`, and `QueryCursorScope` as the domain-author seams; preserve Story 1.2 metadata merge rules where producer paging metadata is authoritative and gateway request paging is not evidence; keep cursors DataProtection-backed, purpose-isolated, opaque, bounded, and scope/query validated; use additive public contract changes only; run EventStore tests by project.

**Block If:** Completing cursor flow requires a non-additive query contract break, changing DAPR actor method names, modifying submodule files, deciding a multi-domain keyed cursor-codec factory, or moving generic paging policy into projection cache keys as a prerequisite for correctness.

**Never:** Do not migrate Tenants or edit `references/Hexalith.Tenants`; do not implement generated REST/UI metadata behavior; do not replace domain-specific cursor scope construction; do not parse, log, return, or render cursor payload/scope/position internals; do not recreate read-model storage outside the existing Client/Testing seams.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Read model write | Projection or handler updates one aggregate key and one index key through `IReadModelStore` + `ReadModelWritePolicy` | ETag-aware reads/writes succeed and index merge uses bounded optimistic-concurrency retry | Persistent conflicts throw after the configured retry budget |
| DAPR store write | `DaprReadModelStore.TrySaveAsync` writes with a supplied ETag | DAPR is invoked with first-write concurrency so conflicts surface as `false` | DAPR exceptions continue to propagate |
| Cursor request | `SubmitQueryRequest.Paging.Cursor` is nonblank and `Offset` is absent | Validator accepts the request and the opaque cursor remains available to the downstream query path | Cursor+offset, bad page sizes, and negative offsets still fail validation |
| Invalid cursor | Query adapter/handler reports `invalid-cursor` or `invalid-cursor: <safe detail>` | HTTP 400 ProblemDetails uses `ProblemTypeUris.BadRequest` and `reasonCode=query_invalid_page` | Response/logs do not include raw cursor, scope, protected payload, or decoded position |
| Paging evidence | Handler/projection returns `QueryPagingMetadata` | Response/client metadata preserves page size, offset or next cursor, total count, and explicit has-more evidence | Gateway does not invent paging evidence from request inputs |

</intent-contract>

## Code Map

- `src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs` -- stable ETag-aware read-model store contract and XML docs.
- `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs` -- production DAPR state-store adapter that must use first-write concurrency.
- `src/Hexalith.EventStore.Client/Projections/ReadModelWritePolicy.cs` -- bounded retry/update/apply/merge policy for aggregate and index read models.
- `src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs` -- deterministic fake with JSON round-trip, ETag, and conflict injection semantics.
- `src/Hexalith.EventStore.Client/Registration/ReadModelStoreServiceCollectionExtensions.cs` and `src/Hexalith.EventStore.Client/Registration/QueryCursorCodecServiceCollectionExtensions.cs` -- opt-in DI seams that need tests.
- `src/Hexalith.EventStore.Client/Queries/IQueryCursorCodec.cs`, `src/Hexalith.EventStore.Client/Queries/QueryCursorCodec.cs`, and `src/Hexalith.EventStore.Client/Queries/QueryCursorScope.cs` -- protected opaque cursor contract and implementation.
- `src/Hexalith.EventStore.Contracts/Queries/QueryPagingMetadata.cs` -- additive paging evidence contract.
- `src/Hexalith.EventStore/Validation/SubmitQueryRequestValidator.cs` -- gateway validation that must allow cursor-only paging but keep malformed paging rejected.
- `src/Hexalith.EventStore/ErrorHandling/QueryExecutionFailedExceptionHandler.cs` and `src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs` -- invalid-cursor sentinel to support-safe ProblemDetails mapping.
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs` -- typed/untyped client metadata exposure for new paging evidence.
- `docs/reference/query-api.md` -- public query metadata, cursor, and ProblemDetails behavior.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.Contracts/Queries/QueryPagingMetadata.cs` -- add optional `bool? HasMore` at the end of the record and document it as producer-authored page-completeness evidence.
- [x] `src/Hexalith.EventStore/Validation/SubmitQueryRequestValidator.cs` -- remove the blanket cursor-reserved rejection while keeping cursor+offset, page-size, and offset validations intact.
- [x] `src/Hexalith.EventStore/ErrorHandling/QueryExecutionFailedExceptionHandler.cs` -- map 400 query failures, especially `query_invalid_page`, to `ProblemTypeUris.BadRequest` instead of the internal-server problem URI.
- [x] `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs`, `tests/Hexalith.EventStore.Contracts.Tests/Queries/SubmitQueryResponseTests.cs`, and `tests/Hexalith.EventStore.Contracts.Tests/Queries/ProjectionAdapterContractTests.cs` -- preserve `HasMore` through contract JSON/DataContract round trips, gateway responses, and typed/untyped client results without changing existing constructor/source compatibility.
- [x] `tests/Hexalith.EventStore.Client.Tests/Projections/ReadModelWritePolicyTests.cs` and `tests/Hexalith.EventStore.Client.Tests/Projections/InMemoryReadModelStoreTests.cs` -- add deterministic tests proving multi-key aggregate+index updates, conflict injection, retry exhaustion, and JSON clone/ETag semantics.
- [x] `tests/Hexalith.EventStore.Client.Tests/Projections/DaprReadModelStoreTests.cs` -- add a fake `DaprClient` subclass in the test file and prove `TrySaveAsync` calls DAPR with `ConcurrencyMode.FirstWrite`, passes the supplied ETag, and returns the DAPR boolean result.
- [x] `tests/Hexalith.EventStore.Client.Tests/Registration/ReadModelAndCursorRegistrationTests.cs` -- verify `AddEventStoreReadModelStore()` and `AddEventStoreQueryCursorCodec(purpose)` register resolvable services and reject invalid purpose input.
- [x] `tests/Hexalith.EventStore.Server.Tests/Validation/SubmitQueryRequestValidatorTests.cs` and `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/QueryProblemDetailsTests.cs` -- prove cursor-only paging passes validation, cursor+offset still fails, invalid-cursor ProblemDetails is HTTP 400 / `query_invalid_page` / support-safe, and raw cursor text is not echoed.
- [x] `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs` and `tests/Hexalith.EventStore.Client.Tests/Gateway/EventStoreGatewayClientTests.cs` -- prove producer `QueryPagingMetadata.HasMore` is preserved and request paging is still not treated as authoritative metadata.
- [x] `docs/reference/query-api.md` -- update cursor and paging metadata docs: cursor-only requests are accepted for handler/projection validation, invalid cursor maps to support-safe `query_invalid_page`, and `hasMore` is authoritative only when producer-supplied.

**Acceptance Criteria:**
- Given a domain projection or query handler needs durable read-model state, when it uses `IReadModelStore` and `ReadModelWritePolicy`, then it can read/write ETag-aware entries by key and apply or merge singleton/index read models with bounded optimistic-concurrency retries.
- Given a domain needs deterministic tests for read-model behavior, when it uses the platform testing fake, then the fake preserves first-write-wins and ETag conflict semantics and can inject conflicts without live DAPR infrastructure.
- Given a domain query returns paged results, when it creates a cursor with `IQueryCursorCodec` and `QueryCursorScope`, then the cursor is protected with a caller-supplied DataProtection purpose and decoding fails safely for wrong scope, wrong query type, malformed payload, tampering, oversize payload, or key rotation.
- Given a request supplies cursor-only paging, when it enters the generic query gateway, then the request can reach the handler/projection path for domain-specific cursor validation.
- Given successful paged responses include producer metadata, when the gateway and client expose the result, then `QueryPagingMetadata` carries effective page size, offset or next cursor, total count when known, and has-more evidence without exposing cursor internals.
- Given cursor or paging inputs are invalid, malformed, wrong-scope, oversized, tampered, or expired after key rotation, when the query path rejects them, then the response uses support-safe ProblemDetails and tests prove cursors are not parsed, logged, displayed, or treated as ordering proof.

## Spec Change Log

## Review Triage Log

## Design Notes

Do not auto-register `IQueryCursorCodec` from `AddEventStoreDomainService()` in this story: the codec requires a stable caller-supplied purpose, and choosing that automatically would be a cross-domain contract decision. Keep registration opt-in and tested.

`HasMore` must be nullable so absent metadata remains distinct from `false`; a producer that cannot determine page completeness should omit it rather than implying the page is complete.

## Verification

**Commands:**
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- expected: succeeds with 0 warnings and 0 errors.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` -- expected: query metadata contract tests pass.
- `dotnet test tests/Hexalith.EventStore.Client.Tests/` -- expected: read-model, cursor, registration, and gateway-client tests pass.
- `dotnet test tests/Hexalith.EventStore.Testing.Tests/` -- expected: testing package fake coverage passes.
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/` -- expected: DataProtection persistence tests pass.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/` -- expected: query validation, controller, and ProblemDetails tests pass.
- `git diff --check` -- expected: no whitespace errors.
