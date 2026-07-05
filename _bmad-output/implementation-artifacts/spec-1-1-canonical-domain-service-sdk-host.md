---
title: '1.1 Canonical Domain-Service SDK Host'
type: 'feature'
created: '2026-07-05'
status: 'done'
baseline_revision: 'a897fb214be12249335a392ec18dee51159681e5'
final_revision: 'f6f9600cbe2b4f3ce573f0b189e602456f887107'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
warnings: []
---

<intent-contract>

## Intent

**Problem:** Domain-service authors need a canonical SDK host path that proves platform-owned hosting, discovery, default endpoints, and DAPR-facing mappings are supplied by `Hexalith.EventStore.DomainService`, not by each domain module.

**Approach:** Harden the already-present SDK host extension surface with focused route/activation tests, guard the Sample host against reintroduced boilerplate, remove the Sample's direct DAPR package reference, and update stale tutorial text to describe the DomainService SDK host shape.

## Boundaries & Constraints

**Always:** Keep the host contract centered on `builder.AddEventStoreDomainService()` and `app.UseEventStoreDomainService()`; preserve `MapEventStoreDomainService()` for advanced/manual mapping; use `.slnx` only; run tests per project; keep C# files single-type and warnings-as-errors clean.

**Block If:** The current SDK host cannot preserve a pre-mapped exact `/project` route without changing public route semantics; removing the Sample direct DAPR package requires unrelated runtime/apphost changes; implementation would need to modify submodule files.

**Never:** Do not implement Story 1.2 query metadata, Story 1.3 read models/cursors, Story 1.4 projection/event-consumer seams, Story 1.5 observability/Aspire seams, or Story 1.7 package governance beyond correcting docs/guardrails directly tied to this host proof.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Canonical SDK host | Builder calls `AddEventStoreDomainService()`, app calls `UseEventStoreDomainService()` | Discovery/activation is populated and routes include `/`, `/health`, `/alive`, `/ready`, `/process`, `/replay-state`, `/query`, `/project`, and `/admin/operational-index-metadata` | Test fails if default endpoints or domain-service endpoints are missing |
| Bespoke projection route | App maps `POST /project` before SDK endpoint mapping | SDK does not add a second `/project` endpoint; the pre-existing route remains the only `/project` match | Test fails on ambiguous duplicate `/project` mappings |
| Sample host drift | Sample project or `Program.cs` reintroduces DAPR/controller/router/default-endpoint/metadata boilerplate | Guardrail fails; Sample references only the DomainService SDK plus its contracts library and uses only the canonical host calls in normal mode | Test failure includes offending markers/references |

</intent-contract>

## Code Map

- `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs` -- canonical host extensions and endpoint mapping; likely only XML-doc drift should change.
- `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs` -- add route-table tests for `UseEventStoreDomainService()` and pre-mapped `/project` preservation.
- `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` -- extend Sample guardrails to catch direct DAPR package references and hand-written host boilerplate.
- `samples/Hexalith.EventStore.Sample/Hexalith.EventStore.Sample.csproj` -- remove direct `Dapr.AspNetCore` reference if the Sample still builds through the DomainService SDK dependency.
- `samples/Hexalith.EventStore.Sample/Program.cs` -- keep canonical host calls; do not remove the opt-in malformed `/project` fault injector unless a test proves it is incompatible.
- `docs/getting-started/first-domain-service.md` -- update stale text that still describes `AddEventStore()`/Client/ServiceDefaults/DAPR direct references.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs` -- add a `UseEventStoreDomainService` test that builds the SDK host, calls the canonical use extension, asserts activation contains the discovered `widget` domain, and asserts the full route set including default health endpoints.
- [x] `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs` -- add a bespoke `/project` test that maps a custom route before `MapEventStoreDomainService()`/`UseEventStoreDomainService()` and asserts exactly one `/project` endpoint remains.
- [x] `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` -- add Sample guardrails for no direct `Dapr.*` package references and no normal-mode hand-written request router/default endpoint/metadata mapping markers in `Program.cs`.
- [x] `samples/Hexalith.EventStore.Sample/Hexalith.EventStore.Sample.csproj` -- remove the direct `Dapr.AspNetCore` package reference when the focused Sample test/build still passes through the SDK dependency graph.
- [x] `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs` -- correct summary docs so the documented canonical endpoint set includes `/query` and `/project`.
- [x] `docs/getting-started/first-domain-service.md` -- revise the tutorial's registration section to say the Sample references `Hexalith.EventStore.DomainService` and uses `AddEventStoreDomainService()`/`UseEventStoreDomainService()`.

**Acceptance Criteria:**
- Given a domain-service host uses `AddEventStoreDomainService()` and `UseEventStoreDomainService()`, when the host is built in tests, then service defaults, EventStore activation, domain discovery, and all canonical/default routes are present without manual router or metadata wiring.
- Given a domain has an application-owned exact `/project` route, when SDK endpoint mapping runs, then the SDK yields that route and does not create duplicate `/project` endpoints.
- Given the Sample domain project is inspected, when guardrail tests run, then it has only the DomainService SDK and Sample contracts project references, no direct DAPR package reference, and no normal-mode hand-written router/default endpoint/operational metadata wiring.
- Given the focused build and tests run, when Release warnings-as-errors are enforced, then DomainService, Sample, and documentation-aligned host guidance are clean.

## Spec Change Log

## Review Triage Log

### 2026-07-05 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 3: (high 0, medium 3, low 0)
- defer: 0
- reject: 3: (high 0, medium 0, low 3)
- addressed_findings:
  - `[medium]` `[patch]` Endpoint tests asserted only route paths, so wrong HTTP verbs could pass; added method-aware route assertions for default and canonical domain-service endpoints.
  - `[medium]` `[patch]` Any pre-mapped `/project` route suppressed the SDK's POST projection endpoint; changed the SDK predicate to yield only for POST-capable `/project` routes and added GET-only coverage.
  - `[medium]` `[patch]` The Sample DAPR package guard used regex-sensitive XML parsing; switched it to `XDocument` parsing so legal attribute order/quote changes cannot bypass the guard.

## Design Notes

The SDK already implements the public host shape and endpoint mappings. Treat this story as a contract-hardening pass: prefer tests and guardrails over moving code. The Sample's opt-in malformed `/project` route is intentional Tier 3 fault-injection support; the compatibility contract is that the SDK yields when the app maps exact `/project` first.

## Verification

**Commands:**
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- expected: succeeds with warnings as errors.
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/` -- expected: all DomainService SDK host and guardrail tests pass.
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/` -- expected: Sample domain tests still pass after dependency cleanup.

## Auto Run Result

Status: done

Summary: Hardened the canonical DomainService SDK host contract with activation/route tests, method-aware `/project` preservation, Sample authoring guardrails, Sample dependency cleanup, and updated first-domain-service guidance.

Files changed:
- `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs` -- documented the full canonical endpoint set and changed `/project` route preservation to yield only for POST-capable existing routes.
- `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs` -- added canonical host activation/default-route tests, method-aware endpoint assertions, POST-preservation coverage, and GET-only `/project` edge coverage.
- `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` -- added Sample no-direct-DAPR and no-normal-mode-boilerplate guardrails, with XML package-reference parsing.
- `samples/Hexalith.EventStore.Sample/Hexalith.EventStore.Sample.csproj` -- removed the direct `Dapr.AspNetCore` package reference.
- `docs/getting-started/first-domain-service.md` -- updated tutorial registration guidance to the DomainService SDK host shape.
- `_bmad-output/implementation-artifacts/epic-1-context.md` -- cached Epic 1 context compiled by the workflow.

Review findings breakdown:
- patches applied: 3 medium findings fixed during review.
- deferred: 0.
- rejected: 3 low findings rejected as speculative or outside this story's focused host proof.

Follow-up review recommended: false.

Verification performed:
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/` -- passed: 44/44.
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/` -- passed: 91/91.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- passed: 0 warnings, 0 errors.
- `git diff --check` -- passed: no whitespace errors.

Residual risks: The Sample `Program.cs` guardrail is intentionally lightweight text scanning for a tiny canonical host file; a full C# syntax analyzer is not implemented in this story.
