---
title: '2.2 REST API Generator Discovery And Controller Emission'
type: 'feature'
created: '2026-07-07'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: true
baseline_revision: '9f8b54dc161a4d5a9b2e6b1deacf331d1b80f1e0'
final_revision: '3326221fc859847ca04d1cc7be35dc0314f24043'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-2-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-2-1-rest-contract-seam-for-command-and-query-messages.md'
warnings: []
---

<intent-contract>

## Intent

**Problem:** The REST API generator already emits gateway-backed controllers from the earlier Epic D work, but Story 2.2 needs it reconciled with the Epic 2 contract: referenced contract discovery must stay explicitly scoped, generated endpoints must expose support-safe metadata and OpenAPI error shapes, and tests must prove generated controller runtime behavior rather than only source strings.

**Approach:** Harden the existing `Hexalith.EventStore.RestApi.Generators` implementation and tests in place. Preserve the external API-host boundary and prior convention fallback for source contracts compiled into an opted-in host; strengthen referenced-contract scope, generated response metadata, and runtime generated-controller coverage.

## Boundaries & Constraints

**Always:** Generated controllers live in dedicated API hosts, call only `IEventStoreGatewayClient`, use `[assembly: RestApi(...)]` as the host opt-in, filter referenced contracts by `RestRouteAttribute.ApiScope == RestApiAttribute.Tag`, use ULID generation through `UniqueIdHelper.GenerateSortableUniqueStringId()`, keep generated source nullable/TWAE-safe, and preserve `.ConfigureAwait(false)` on gateway awaits.

**Block If:** Fulfilling the story requires changing public contract member names, removing source-contract convention fallback, changing gateway DTO shapes, publishing UI-host controllers, editing Tenants/submodule files, or inventing query metadata values not present in `EventStoreQueryResult.Metadata`.

**Never:** Do not bypass the generic gateway, do not call MediatR/DAPR actors/domain handlers/state stores from generated controllers, do not parse JWT payloads/cursors/ETags, do not add package versions to project files, do not change AppHost/Sample/Tenants adoption, and do not run solution-level `dotnet test`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Referenced scope | API host has `[assembly: RestApi("api/counter", "counter", ...)]` and references command/query contracts with mixed `ApiScope` values | Only referenced contracts whose `RestRouteAttribute.ApiScope` is exactly `counter` emit controller actions; source contracts in the host keep existing convention fallback | Non-matching or blank referenced scopes emit no action and no broad fallback |
| Command action | Generated POST receives a non-null command body whose route aggregate/property values match | Controller builds `SubmitCommandRequest`, generates a sortable ULID message id, calls `SubmitCommandAsync`, and returns `202` with `Retry-After` and command-status `Location` | Null body or route/body mismatch returns safe `400 application/problem+json` before the gateway call |
| Query 200 metadata | Gateway returns payload with `ETag`, `IsStale`, `IsDegraded`, `ProjectionVersion`, `ServedAt`, paging metadata, and warning codes | Controller returns raw payload with bounded protocol headers: `ETag`, `X-Hexalith-Projection-Version`, `X-Hexalith-Served-At`, `X-Hexalith-Is-Stale`, `X-Hexalith-Is-Degraded`, `X-Hexalith-Warning-Codes`, `X-Hexalith-Page-Size`, `X-Hexalith-Page-Offset`, `X-Hexalith-Page-Total-Count`, `X-Hexalith-Page-Has-More`, and `X-Hexalith-Next-Cursor` when values are present and within bounds | Missing or oversized metadata omits the specific header; cursor/ETag internals are forwarded only as opaque protocol headers, never as problem/support text |
| Query 304 metadata | Gateway returns `IsNotModified` with ETag and optional metadata | Controller forwards `If-None-Match`, returns empty `304`, preserves strong `ETag`, and emits only present metadata headers | No synthetic projection/version/freshness/paging headers when metadata is absent |
| Gateway failure | Gateway throws `EventStoreGatewayException` for forbidden, invalid request, not found, or unavailable outcomes | Controller returns safe `ProblemDetails` with status, retry header when present, correlation/tenant/reason fields, and filtered validation errors | Unsafe details such as stack traces, tokens, raw payloads, cursor contents, or ETag internals are omitted |

</intent-contract>

## Code Map

- `src/Hexalith.EventStore.RestApi.Generators/RestApiGenerator.cs` -- incremental pipeline combining source messages, referenced messages, REST options, manifest, and controller emission.
- `src/Hexalith.EventStore.RestApi.Generators/RestApiMessageParser.cs` -- command/query discovery, referenced-assembly scanning, `ApiScope` filtering, route/query-binding metadata parsing.
- `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` -- generated controller/action/problem/metadata source emission.
- `src/Hexalith.EventStore.Contracts/Queries/QueryResponseMetadata.cs` and `QueryPagingMetadata.cs` -- support-safe query metadata fields that generated query actions may forward.
- `src/Hexalith.EventStore.Client/Gateway/IEventStoreGatewayClient.cs`, `EventStoreQueryResult.cs`, and `EventStoreGatewayException.cs` -- gateway client boundary and result/error inputs for generated controllers.
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs` -- generated source and compile-through coverage.
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiGeneratedControllerErrorSemanticsTests.cs` -- runtime invocation tests for generated controllers.
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiIncrementalGenerationTests.cs` and `RestApiManifestGenerationTests.cs` -- discovery, determinism, and scope coverage.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiGeneratedControllerErrorSemanticsTests.cs` -- add red runtime tests for query `200` metadata headers, absent-metadata omission, degraded/warning/paging header bounds, and not-found/validation gateway problem mapping -- proves generated behavior at the controller surface.
- [x] `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` -- emit bounded metadata headers for `QueryResponseMetadata.IsDegraded`, `WarningCodes`, and `Paging` on both `200` and `304` responses; use the `X-Hexalith-*` header names in the I/O matrix and keep payload-only `200` bodies -- exposes Epic 2 query evidence without wrapping payloads.
- [x] `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` -- add generated `[ProducesResponseType(typeof(ProblemDetails), ...)]` declarations for local validation and gateway failure statuses on command/query actions -- makes generated endpoints OpenAPI-visible for safe problem responses.
- [x] `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs` -- assert generated source contains gateway-only dependencies, problem response declarations, sortable ULID message-id generation, `ConfigureAwait(false)`, and no DAPR/MediatR/domain-service calls -- protects the external API-host boundary.
- [x] `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiIncrementalGenerationTests.cs` and `RestApiManifestGenerationTests.cs` -- preserve or strengthen tests for source-host convention fallback and referenced `ApiScope` filtering, including tagless-host exclusion -- prevents accidental broad referenced-contract discovery.

**Acceptance Criteria:**
- Given an opted-in API host with source and referenced contracts, when the generator runs, then source contracts can use existing convention fallback while referenced contracts emit only when `ApiScope` exactly matches the host tag.
- Given a generated command endpoint receives a valid body and matching route values, when the action executes, then it calls `SubmitCommandAsync` once with gateway DTO metadata, generated ULID message id, body aggregate id, and serialized payload.
- Given a generated command endpoint receives a null body or mismatched aggregate/property route value, when the action executes, then it returns `400 application/problem+json` and does not call the gateway.
- Given a generated query endpoint receives gateway metadata on a `200` result, when the action executes, then it forwards only present, bounded `ETag` and `X-Hexalith-*` metadata headers and returns the raw payload body.
- Given a generated query endpoint receives a not-modified gateway result, when the action executes, then it forwards `If-None-Match`, returns `304` with an empty body, and does not synthesize metadata headers.
- Given the gateway throws safe and unsafe failure details, when a generated action maps the exception, then the response is `application/problem+json` with allowed fields only and no token, payload, cursor, ETag-internal, or stack-trace leakage.

## Spec Change Log

## Review Triage Log

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 11: (high 0, medium 5, low 6)
- defer: 0
- reject: 0
- addressed_findings:
  - `[medium]` `[patch]` Generated gateway problem mapping forwarded unsafe top-level title/type/detail/reason fields. Patched generated helpers to neutralize unsafe display text and require token-shaped reason metadata, with runtime coverage.
  - `[medium]` `[patch]` Generated `Retry-After` copied gateway text directly into a header. Patched it through the bounded/control-character header helper and covered invalid retry metadata.
  - `[medium]` `[patch]` Generated ETag formatting could turn weak validators into strong ones or emit quote-corrupted validators. Patched ETag emission to reject weak or malformed values and covered invalid ETags.
  - `[medium]` `[patch]` Bounded header helper trimmed opaque protocol values such as cursors. Patched it to preserve values exactly while still rejecting empty, oversized, or control-character values.
  - `[medium]` `[patch]` Warning-code headers accepted arbitrary comma-delimited text. Patched warning codes to require token-shaped values and bounded counts.
  - `[low]` `[patch]` Generated OpenAPI response metadata omitted 401 and 500 outcomes. Added generated response declarations for both statuses.
  - `[low]` `[patch]` Generated ProblemDetails OpenAPI declarations did not declare `application/problem+json`. Added content-type-specific `ProducesResponseType` declarations.
  - `[low]` `[patch]` Runtime generated-controller coverage lacked null command body validation. Added a null-body test proving 400 before the gateway call.
  - `[low]` `[patch]` Runtime generated-controller coverage lacked command gateway failure mapping. Added a command failure test proving ProblemDetails mapping without success headers.
  - `[low]` `[patch]` Runtime tests did not cover unsafe top-level problem fields. Added a test for unsafe title/type/detail/reason/retry metadata.
  - `[low]` `[patch]` Runtime tests did not cover invalid paging bounds or warning-code count bounds. Added coverage for oversized page size, negative offset/total, invalid cursor, and excessive warning codes.

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 4: (high 0, medium 4, low 0)
- defer: 0
- reject: 0
- addressed_findings:
  - `[medium]` `[patch]` Generated gateway problem display text still allowed cursor-bearing details. Patched display-text sanitization to omit cursor text and covered invalid-cursor detail neutralization.
  - `[medium]` `[patch]` Generated `Retry-After` accepted any bounded non-control text. Patched it to emit only non-negative delta seconds or RFC1123 HTTP-date values and covered token-shaped invalid retry metadata.
  - `[medium]` `[patch]` Generated ETag formatting still accepted whitespace-bearing validators. Patched ETag handling through `EntityTagHeaderValue.TryParse`, explicit whitespace rejection, and weak-tag rejection, with runtime coverage for spaced validators.
  - `[medium]` `[patch]` Generated problem extensions forwarded unsafe correlation and tenant metadata verbatim. Patched both through the token-shaped safe extension helper and covered unsafe values being omitted.

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 10: (high 1, medium 5, low 4)
- defer: 1: (high 0, medium 1, low 0)
- reject: 1: (high 0, medium 0, low 1)
- addressed_findings:
  - `[high]` `[patch]` Generated gateway exception mapping trusted `EventStoreGatewayException.StatusCode`, allowing semantic query failures with status `200` to return HTTP 200 problem bodies. Patched generated controllers to normalize non-error gateway exception statuses to `502 Bad Gateway` with runtime coverage.
  - `[medium]` `[patch]` Generated query actions could return `304 Not Modified` after omitting a missing, weak, malformed, or rejected ETag. Patched generated query actions to return `502 application/problem+json` before metadata emission when a not-modified result lacks a strong ETag.
  - `[medium]` `[patch]` Generated top-level problem title/type/detail filtering was support-safe by keyword but not bounded. Patched display-text filtering to reject oversized values and added runtime coverage.
  - `[medium]` `[patch]` Generated validation-error filtering allowed control characters and oversized strings. Patched `IsSupportSafeProblemText` to use the same bounded/control-character guard and expanded error filtering coverage.
  - `[medium]` `[patch]` Generated warning-code token validation used Unicode character classes. Patched it to accept only ASCII letters, digits, `.`, `_`, and `-`, with non-ASCII warning-code coverage.
  - `[medium]` `[patch]` Generated ETag formatting still accepted leading or trailing whitespace before quoting/parsing. Patched it to reject trimmed validators and added leading/trailing whitespace cases.
  - `[low]` `[patch]` Generated `401` OpenAPI metadata was status-only. Patched generated problem response declarations to document `ProblemDetails` with `application/problem+json`.
  - `[low]` `[patch]` Generated query OpenAPI metadata advertised command-only `409`/`422` failure shapes. Patched command/query problem response emission into separate status sets and asserted the command-only counts.
  - `[low]` `[patch]` Generated source assertions skipped some emitted problem response declarations. Added assertions for `422`, `429`, and `502` problem metadata plus the status normalizer.
  - `[low]` `[patch]` Retry-After runtime coverage only proved numeric values. Added generated-controller tests for valid RFC1123 HTTP-date values plus negative and oversized numeric rejection.

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 5: (high 0, medium 4, low 1)
- defer: 3: (high 0, medium 2, low 1)
- reject: 2: (high 0, medium 1, low 1)
- addressed_findings:
  - `[medium]` `[patch]` Generated query endpoints ignored `QueryResponseMetadata.ETag` when `EventStoreQueryResult.ETag` was null. Patched generated query actions to fall back to metadata ETags for `200` and `304` responses, with runtime coverage.
  - `[medium]` `[patch]` Generated gateway problem mapping could preserve 4xx/5xx statuses not documented by generated OpenAPI metadata, and query `501 Not Implemented` was reachable but undocumented. Patched status normalization to the generated documented sets and added query `501` metadata/runtime coverage.
  - `[medium]` `[patch]` Generated gateway problem mapping emitted `Retry-After` for non-retryable statuses. Patched retry header emission to retryable statuses only and covered validation failures with retry metadata.
  - `[medium]` `[patch]` Generated validation-error forwarding had no count cap. Patched filtered errors to stop at `MaxProblemErrorCount` and covered the generated response bound.
  - `[low]` `[patch]` Generated `Retry-After` accepted huge parseable delay-second values. Patched delay-seconds to a bounded maximum and covered oversized numeric rejection.

## Design Notes

Story 2.2 is not a fresh generator story because the prior Epic D generator is already present and tested. The safe path is to preserve its working controller emission and close the Epic 2 deltas that remain observable to external API consumers: fail-closed referenced discovery, OpenAPI problem shapes, and metadata forwarding from the platform query result.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/` -- expected: generator discovery, emitted source, and generated runtime controller tests pass.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` -- expected: contract seam tests remain green if metadata/header changes touch contract types.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false` -- expected: Release package-mode build passes with zero warnings.

## Auto Run Result

Status: done

Summary:
- Hardened generated REST controllers so query responses forward bounded support-safe metadata headers for degraded state, warning codes, paging, cursor, projection version, served-at, stale state, and ETag evidence while keeping 200 bodies as raw gateway payloads.
- Added generated OpenAPI response metadata for safe problem responses, including authentication and server-failure outcomes.
- Tightened generated header/problem safety after review: invalid `Retry-After`, weak/malformed ETags, unsafe top-level problem fields, arbitrary warning-code text, and trimmed opaque values are now rejected or neutralized.
- Follow-up review further tightened generated support-safety: cursor-bearing problem details are neutralized, `Retry-After` must be syntactically valid, ETags reject whitespace-bearing validators, and unsafe correlation/tenant extension values are omitted.
- Latest follow-up review fixed generated `304`/ETag invalid states, normalized non-error gateway exception statuses to `502`, split command/query OpenAPI problem declarations, bounded problem text/errors, and restricted warning-code tokens to ASCII.
- Final follow-up review forwards metadata-only ETags, normalizes gateway failures to documented generated problem statuses including query `501`, emits `Retry-After` only for retryable statuses, caps forwarded validation errors, and bounds retry delay-seconds.

Files changed:
- `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` -- generated ProblemDetails metadata, metadata header forwarding, bounded header helpers, safe problem mapping, strict ETag/warning-code handling, syntactic and bounded `Retry-After` validation, metadata ETag fallback, documented-status normalization, and safe token-shaped problem extensions.
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiGeneratedControllerErrorSemanticsTests.cs` -- runtime generated-controller coverage for query metadata, metadata-only ETags, invalid metadata, gateway failures, query `501`, retry gating, capped errors, command success, null bodies, command gateway failures, unsafe problem metadata, invalid retry headers, whitespace-bearing ETags, invalid not-modified validators, semantic status-200 gateway failures, and ASCII warning tokens.
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs` -- source assertions for gateway-only generated controllers, ProblemDetails OpenAPI metadata, metadata ETag fallback, metadata headers, parser-backed ETag handling, retry sanitization, status normalization, command/query response-shape separation, and no direct domain/DAPR/MediatR dependencies.
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiIncrementalGenerationTests.cs` -- tagless host referenced-contract exclusion coverage.
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiManifestGenerationTests.cs` -- manifest coverage for referenced `ApiScope` filtering and tagless host exclusion.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- appended Story 2.2 deferred items for pre-existing generated command request-size-limit, command rejection extension forwarding, command status-location, and query argument-message policy gaps.
- `_bmad-output/implementation-artifacts/spec-2-2-rest-api-generator-discovery-and-controller-emission.md` -- recorded this follow-up review pass and final run result.

Review findings breakdown:
- Patches applied: 30 total across review passes; final follow-up pass applied 5 patches (0 high, 4 medium, 1 low).
- Deferred: 4 total Story 2.2 items across review passes; final follow-up pass appended 3 new deferred entries for pre-existing generated command rejection extension forwarding, command status-location, and query argument-message policy gaps.
- Rejected: 3 total across review passes; final follow-up pass rejected the already-deferred request-size-limit finding and the transient in-review artifact-state observation.

Follow-up review recommendation: true. The final review-driven changes materially affected generated HTTP status semantics, metadata ETag forwarding, retry behavior, OpenAPI response metadata, and support-safety filtering.

Verification performed:
- Final pass: `dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/` -- passed, 108 tests.
- Final pass: `dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false` -- passed with 0 warnings and 0 errors.
- Final pass: `git diff --check` -- passed.
- Earlier Story 2.2 auto run: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` -- passed, 591 tests; not rerun in the latest pass because no contract files changed.

Residual risks:
- Generated query metadata headers are intentionally protocol-level evidence only; projection freshness still depends on upstream gateway metadata availability.
- Follow-up review is recommended because the review pass changed support-safety behavior after the initial implementation.
