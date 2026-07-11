---
id: REST-GEN-HARDENING
title: REST Generator Hardening
classification: backlog
status: draft
source_story: 7.5
created: 2026-07-05
updated: 2026-07-07
related_story: _bmad-output/implementation-artifacts/7-5-rest-generator-hardening.md
related_action_items:
  - "Epic 2 retro action item 2 (Winston): scope Epic 2 REST generator hardening into a backlog item or implementation story."
  - "Epic 2 retro action item 3 (Winston): define the external generated API command-status Location policy."
source_evidence:
  - _bmad-output/implementation-artifacts/deferred-work.md
  - _bmad-output/implementation-artifacts/epic-D-retro-2026-07-05.md
  - _bmad-output/implementation-artifacts/epic-2-retro-2026-07-07.md
  - _bmad-output/implementation-artifacts/spec-2-1-rest-contract-seam-for-command-and-query-messages.md
  - _bmad-output/implementation-artifacts/spec-2-2-rest-api-generator-discovery-and-controller-emission.md
  - _bmad-output/implementation-artifacts/spec-2-3-sample-external-api-host-proof.md
---

# REST Generator Hardening

## Scope

Track REST generator hardening that remains beyond the Epic 2 proof scope. The backlog covers unsupported contract-shape diagnostics, duplicate command JSON-name diagnostics, invalid `RestQueryBinding` source diagnostics, empty constant binding diagnostics, route-template constraint behavior, case-insensitive route/JSON-name matching, referenced-contract incrementality, generated-controller authorization checks, and generated external API error-semantics coverage.

The work arrives in two waves. The **first wave** (Epic D / D5–D7 review findings) is implemented and closed. The **second wave** (surfaced by Epic 2 stories 2.1–2.5) is scoped below with a named target source and test artifact per item, satisfying the Epic 2 retrospective completion gate.

## First Wave — Resolved (Epic D / D5–D7)

Implemented and marked `done` in `_bmad-output/implementation-artifacts/7-5-rest-generator-hardening.md`. Do not re-scope these into new stories:

- Unsupported `struct` / `record struct` REST contract shapes → `HESREST006` (source and referenced).
- Command + case-insensitive duplicate JSON-name detection and case-insensitive route/`JsonName` matching → `HESREST010`.
- Invalid `RestQueryBinding` source diagnostics, including aggregate `None` and empty/whitespace constant values → `HESREST012`.
- Route-template inline constraint (regex/escaped-brace) parsing.
- Referenced-contract incrementality (equatable descriptor comparer) and `ApiScope` fail-closed filtering.
- Generated external API error-semantics tests: `403`/`503`/invalid-cursor `400`/ETag `304`/route-body mismatch, plus support-safe `ProblemDetails` error filtering.

## Second Wave — Epic 2 (Stories 2.1–2.5) Scoped Items

Deferred generator items surfaced while proving Epic 2's external integration surfaces. Each has a **named target artifact** so a future implementation story does not re-discover scope. These are additive hardening — no completed Epic 2 story is reopened by tracking them here.

| # | Item | Source spec | Target source artifact | Target test artifact | Intended policy |
| --- | --- | --- | --- | --- | --- |
| S1 | Generated command endpoints emit no canonical `[RequestSizeLimit(1_048_576)]` (platform `CommandsController`/`QueriesController` do) | spec-2-2 | `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` (`AppendCommandAction`) | `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs` | Emit the canonical 1 MiB request-body limit on generated command actions. |
| S2 | Command success `Location` hard-codes relative `/api/v1/commands/status/{id}`, which a dedicated external API host may not expose | spec-2-2, spec-2-3 | `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` (`AppendCommandAction`) | `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs`; Sample generated-controller runtime test | **Policy owned by open Epic 2 retro action item 3 (Winston).** Implement here once the host-status-route decision is fixed. |
| S3 | Generated query action maps caught `ArgumentException.Message` directly into client `ProblemDetails`, bypassing support-safe filtering | spec-2-2 | `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` (`AppendQueryAction`) | `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiGeneratedControllerErrorSemanticsTests.cs` | Fixed safe message or shared sanitizer; no raw exception text at the generated surface. |
| S4 | Generated command problem mapping drops safe domain-rejection extensions `rejectionType` and `correctiveAction` | spec-2-2 | `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` (`AppendCommandAction` problem mapping) | `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiGeneratedControllerErrorSemanticsTests.cs` | Deliberate support-safe extension allowlist mirroring `GatewayProblemDetailsExtensions` / `EventStoreGatewayException.Extensions`. |
| S5 | Invalid tenant-source handling: `{tenantId}` route param is unvalidated under `RestTenantSource.System`, and an out-of-range `RestTenantSource` flows through as numeric text | spec-2-1, D7 review | `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` (`IsTenantParameter`, `ResolveTenant`); `src/Hexalith.EventStore.RestApi.Generators/RoslynAttributeValueReader.cs` (`GetEnumName`) | `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiDiagnosticTests.cs`; `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs` | Validate the tenant-named route param against the body when tenant source ≠ Route; emit a HESREST diagnostic for an out-of-range tenant source instead of numeric passthrough. |
| S6 | `RestQueryBinding` runtime attribute permits `EntitySource = None` with a non-null value and preserves padded route/constant values (e.g. `" tenantId "`) that the generator's route lookup later rejects | spec-2-1 | `src/Hexalith.EventStore.Contracts/Rest/RestQueryBindingAttribute.cs`; `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` (binding route lookup) | `tests/Hexalith.EventStore.Contracts.Tests/Rest/RestQueryBindingAttributeTests.cs` (or sibling); `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiDiagnosticTests.cs` | Reconcile runtime attribute validation with the generator's fail-closed rules (reject `None` + value; trim/reject padded route/constant binding values before route lookup). |

**Completion-gate mapping (Epic 2 retro action item 2):** request-size limits → S1; status `Location` → S2 (policy via action item 3); safe query argument problem text → S3; command rejection extension policy → S4; invalid tenant-source handling → S5; invalid binding diagnostics → S6 (extending the first-wave `HESREST012`).

## Non-Goals

- Do not mix generator hardening into unrelated security, correctness, UI, or topology stories.
- Do not move generated controllers into interactive UI hosts.
- Do not treat generated API smoke responses as persisted integration evidence.
- Do not re-implement first-wave items already closed by `7-5-rest-generator-hardening.md`.
- Do not decide the command-status `Location` semantics here; that policy is owned by Epic 2 retro action item 3.

## Dependencies

- Existing deferred-work entries plus Epic D and Epic 2 retrospective findings.
- The command-status `Location` policy decision (Epic 2 retro action item 3) gates S2.
- Query response provenance / freshness metadata is enforced by architecture invariant AD-15 and EventStore **Story 2.8**. Generated controllers now emit provenance and gate ETag/version/stale/`304` behavior on `ProjectionBacked`; **Story 4.7** retains only the Tenants producer cleanup.
- Generator test infrastructure in `tests/Hexalith.EventStore.RestApi.Generators.Tests/`.

## Risks

- Diagnostics that miss invalid contract shapes can push failures to runtime.
- Incomplete referenced-contract incrementality can produce stale generated surfaces.
- Generated error semantics can diverge from gateway behavior if tests only use mocks.
- A missing generated request-size limit (S1) leaves external command endpoints without the platform's payload ceiling.
- An unvalidated tenant route param under `System` source (S5) lets a URL/body tenant mismatch execute against the body tenant silently (bearer-authorized, so no escalation, but confusing).

## Validation Expectations

- Generator tests must cover diagnostics, generated output, referenced-contract incrementality, route/body mismatch, invalid cursor/envelope behavior, `304`, and safe problem details.
- Second-wave work must add tests at the named artifacts above and prove behavior at the generated-controller surface (compile-and-exercise) where practical, not source-string assertions alone.
- Any higher-tier proof must inspect persisted or gateway-owned evidence where applicable, not only generated endpoint status codes.
- The dedicated implementation story must keep source-generator changes warnings-as-errors clean.
