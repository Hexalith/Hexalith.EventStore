# Post-Epic 22 ES-1: Result-Payload Parse DoS Guard

Status: done

Context created: 2026-05-27
Story key: `post-epic-22-es1-result-payload-parse-dos-guard`
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-27-eventstore-parties-review-residuals.md` (finding ES-1)
Epic: Post-Epic-22 EventStore↔Parties Review Residuals
Scope: Minor (Developer). Add an explicit bounded-depth + pre-parse length guard to `CommandsController.ParseOptionalResultPayload` so a hostile or oversized domain-service result payload cannot exhaust CPU/memory while being parsed into a `JsonDocument`. Logging for the malformed-JSON path **already exists** — do not re-add it.

## Story

As the EventStore command gateway operator,
I want the optional domain-service result payload to be parsed only after an explicit depth bound and a pre-parse length cap are enforced,
so that a malicious or buggy domain service cannot stall request threads or consume large amounts of memory by returning a deeply nested or oversized JSON result payload to `POST /api/v1/commands`.

## Background & Verified Residual

The original review flagged two problems on this code path: (1) unbounded `JsonDocument.Parse` (DoS) and (2) a silent `JsonException`. Verification against current `main` found the **logging half is already done** — `CommandsController.cs:134-138` already emits `LogWarning(... CorrelationId ...)` on `JsonException`. The **DoS guard is genuinely absent**: `ParseOptionalResultPayload` calls `JsonDocument.Parse(resultPayload)` with no `JsonDocumentOptions` and no length pre-check.

Two nuances the dev must internalize so the fix is correct and not over-claimed:

- **Depth is already implicitly 64.** `JsonDocument.Parse(string)` with the default `JsonDocumentOptions` uses `MaxDepth = 0`, which the parser treats as 64. So setting `MaxDepth = 64` explicitly is **defense-in-depth made visible in source** (identical rationale to dw3's `AdminStreamQueryController` change — "the depth cap is now in source, not implicit"), not a new numeric limit. Over-depth input already throws `JsonException`, which the existing `catch (JsonException)` already handles. Do **not** add a second depth/throw handler.
- **The length cap is the real, currently-missing guard.** The `resultPayload` string originates from the domain-service round-trip result (`SubmitCommandResult.ResultPayload`), **not** the inbound HTTP request body. The inbound body is already capped by `[RequestSizeLimit(1_048_576)]` on `Submit`, but that cap does **not** constrain the domain-service-produced result string. A pre-parse length check is the new protection that closes the residual.

This is post-Epic-22 hardening (Epics 1–22 all `done`), routed via the established "focused `sprint-status.yaml` row" pattern. It is the first item in the `post-epic-22-es*` cluster (sequence: ES-1, ES-6 first per the proposal §6 handoff).

## Acceptance Criteria

1. **JSON parse depth is explicitly bounded.**
   - Given `ParseOptionalResultPayload` parses a non-empty `resultPayload`
   - When it calls `JsonDocument.Parse`
   - Then it passes a `JsonDocumentOptions` with `MaxDepth = 64`, matching the existing call sites `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs:55` and `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs:712`
   - And the options value is declared once (e.g. a `private static readonly JsonDocumentOptions` field on `CommandsController`, mirroring `AdminStreamQueryController._payloadParseOptions`), not constructed per call
   - And a payload that exceeds depth 64 throws `JsonException` and is handled by the **existing** `catch (JsonException)` branch (return `null`, existing warning) — no new depth-specific handler is added.

2. **Oversized result payloads are rejected before parsing (the DoS guard).**
   - Given a non-empty `resultPayload` whose length exceeds the configured maximum
   - When `ParseOptionalResultPayload` runs
   - Then it returns `null` **without** calling `JsonDocument.Parse` (the oversized string is never fully parsed into a `JsonDocument`)
   - And the maximum is a single named constant (recommended default `64 * 1024`, matching the existing `DaprInfrastructureQueryService.MaxFormatStateValueBytes = 64 * 1024` precedent; if a different bound is chosen, the Dev Agent Record states the evidence-based rationale)
   - And the length check is performed against `resultPayload.Length` (cheap O(1) char-count guard) or UTF-8 byte count — the Dev Agent Record records which and why.

3. **The oversized-drop is observable without leaking payload content.**
   - Given the length cap rejects a payload
   - When the drop happens
   - Then exactly one `LogWarning` is emitted carrying `correlationId` and (optionally) the observed/limit lengths as numbers only
   - And the log message contains **no** result-payload content, command payload, secrets, or user-controllable display values (CLAUDE.md Rule 5 / NFR12: "never log event/command payload data")
   - And this is a **distinct new branch** for the length cap — it does not duplicate, move, or rewrite the existing malformed-JSON `JsonException` warning at `CommandsController.cs:134-138`.

4. **Valid result payloads still round-trip unchanged (no regression).**
   - Given a well-formed, in-bound JSON result payload (object, array, scalar, or `null`-shaped)
   - When `Submit` returns
   - Then the `202 Accepted` body `SubmitCommandResponse.ResultPayload` contains the same parsed `JsonElement` it produced before this change
   - And `null`/empty/whitespace `resultPayload` still returns `null`
   - And a malformed (but in-bound, in-depth) payload still returns `null` with the existing warning
   - And the public `SubmitCommandResponse` contract, the `202`/Location/Retry-After behavior, and the inbound `[RequestSizeLimit(1_048_576)]` are unchanged.

5. **Tier 1 coverage pins the new guards through the public `Submit` path.**
   - Given the new tests live in `tests/Hexalith.EventStore.Server.Tests` (alongside `CommandsControllerTenantTests`, or a focused new `CommandsControllerResultPayloadTests`)
   - When they drive `controller.Submit(...)` with a mocked `IMediator` returning a `SubmitCommandResult` whose `ResultPayload` is crafted per case
   - Then there is a test proving an oversized payload yields `SubmitCommandResponse.ResultPayload == null` and `JsonDocument.Parse` is not reached (assert the warning fired / payload dropped)
   - And a test proving an over-depth payload (depth > 64) yields `null` via the existing `JsonException` path
   - And a test proving a valid in-bound payload is parsed and preserved in the response
   - And at least one assertion is **regression-sensitive**: it would fail against the pre-change unbounded implementation (e.g. the oversized-payload case).

## Tasks / Subtasks

- [x] **ST0 — Reconfirm baseline.** (AC: 1, 2)
  - [x] Re-read `src/Hexalith.EventStore/Controllers/CommandsController.cs:123-139` (`ParseOptionalResultPayload`) and the two precedent call sites (`AdminStreamQueryController.cs:55`, `DaprInfrastructureQueryService.cs:701-718`).
  - [x] Confirm `SubmitCommandResult.ResultPayload` is `string?` (`src/Hexalith.EventStore.Server/Pipeline/Commands/SubmitCommand.cs:23`) and `SubmitCommandResponse.ResultPayload` is `JsonElement?`.
  - [x] Confirm the inbound `[RequestSizeLimit(1_048_576)]` does **not** bound this result string, so a dedicated cap is required.

- [x] **ST1 — Add the explicit depth bound + pre-parse length cap.** (AC: 1, 2, 3, 4)
  - [x] Add a `private static readonly JsonDocumentOptions` field on `CommandsController` with `MaxDepth = 64` (mirror `AdminStreamQueryController._payloadParseOptions`).
  - [x] Add a `private const int` length-cap constant (recommended `64 * 1024`); pick chars-vs-bytes and record the choice.
  - [x] In `ParseOptionalResultPayload`, after the existing `string.IsNullOrWhiteSpace` early return, add the length check: if over cap, emit the no-payload warning (AC 3) and return `null` before `Parse`.
  - [x] Pass the depth-bounded options into `JsonDocument.Parse(resultPayload, _options)`.
  - [x] Leave the existing `catch (JsonException)` block exactly as-is (handles malformed + over-depth).
  - [x] Keep `TreatWarningsAsErrors` clean (file-scoped namespace, no unused usings, nullable-correct).

- [x] **ST2 — Tier 1 tests.** (AC: 5)
  - [x] Add tests in `tests/Hexalith.EventStore.Server.Tests/Controllers/` driving `Submit` with a mocked `IMediator` returning crafted `SubmitCommandResult` payloads.
  - [x] Cases: oversized → `null` + drop observable; over-depth (>64) → `null`; valid object/array/scalar → preserved; null/empty → `null`; malformed in-bound → `null`.
  - [x] Make the oversized case regression-sensitive (fails against the old unbounded code).
  - [x] Reuse the existing `CreateControllerWithMediator` / `CreateAuthenticatedPrincipal` helpers where practical.

- [x] **ST3 — Validate and record evidence.** (AC: all)
  - [x] Run `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~CommandsController"` first; record pass/fail counts.
  - [x] Run `dotnet build Hexalith.EventStore.slnx --configuration Release`; record exact `0 warnings / 0 errors` (or classify any pre-existing failure — `Server.Tests` has a known pre-existing CA2007 note in this workspace; do not attribute unrelated failures to this story).
  - [x] Update Dev Agent Record, File List, Verification Status, and Change Log before moving to `review`.

## Dev Notes

### Exact code being modified

`src/Hexalith.EventStore/Controllers/CommandsController.cs:123-139` — current implementation:

```csharp
private static JsonElement? ParseOptionalResultPayload(string? resultPayload, string correlationId, ILogger logger) {
    if (string.IsNullOrWhiteSpace(resultPayload)) {
        return null;
    }

    try {
        using var document = JsonDocument.Parse(resultPayload);          // <-- unbounded: no options, no length pre-check
        JsonElement element = document.RootElement.Clone();
        return element.ValueKind == JsonValueKind.Undefined ? null : element;
    }
    catch (JsonException) {
        logger.LogWarning(                                               // <-- KEEP: malformed-JSON warning already exists (ES-1 logging half done)
            "Malformed result payload from domain service for correlation '{CorrelationId}' could not be parsed as JSON; returning no resultPayload.",
            correlationId);
        return null;
    }
}
```

Called once, from `Submit` at `CommandsController.cs:120`:
`Accepted(new SubmitCommandResponse(result.CorrelationId, ParseOptionalResultPayload(result.ResultPayload, result.CorrelationId, logger)))`.

The shape after the change is: `IsNullOrWhiteSpace` early return → **new** length-cap early return (warn + null) → `JsonDocument.Parse(resultPayload, _payloadParseOptions)` → existing `catch (JsonException)` unchanged.

### Established precedent — copy it, do not invent

Two existing call sites already encode exactly the pattern ES-1 asks for. Match them so the codebase stays consistent:

- `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs:55`
  `private static readonly JsonDocumentOptions _payloadParseOptions = new() { MaxDepth = 64 };` — threaded through every payload parse site (added by dw3 to make the depth cap "in source, not implicit").
- `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs:699-718`
  `private const int MaxFormatStateValueBytes = 64 * 1024;` guards size **before** `JsonDocument.Parse(value, new() { MaxDepth = 64 })`, returning a truncated/sentinel value above the cap. This is the size-cap precedent for AC 2's default.

### Data-flow facts

- `SubmitCommandResult(string CorrelationId, string? ResultPayload = null)` — `src/Hexalith.EventStore.Server/Pipeline/Commands/SubmitCommand.cs:23`. `ResultPayload` is a raw JSON **string** from the domain service.
- `SubmitCommandResponse(string CorrelationId, JsonElement? ResultPayload = null)` — `src/Hexalith.EventStore/Models/SubmitCommandResponse.cs:8` (compat wrapper over `Contracts.Commands.SubmitCommandResponse`). The controller returns the parsed `JsonElement?`.
- `JsonDocument.Parse` overload: `JsonDocument.Parse(string json, JsonDocumentOptions options = default)`.

### Scope boundaries (do not exceed)

- Do **not** re-add or rewrite the malformed-JSON `JsonException` warning — it already exists and ES-1's logging half is closed.
- Do **not** change the inbound `[RequestSizeLimit(1_048_576)]`, the `202`/Location/Retry-After behavior, or the public `SubmitCommandResponse` contract.
- Do **not** touch ES-3's drop site (`SubmitCommandHandler.cs:167-173`) — that is a separate row (`post-epic-22-es3-result-payload-drop-observability`). ES-1 is confined to `ParseOptionalResultPayload`.
- Do **not** introduce a new ProblemDetails shape; an oversized/malformed result payload degrades gracefully to a `null` `resultPayload` on an otherwise-successful `202`, exactly as malformed JSON does today.
- This row touches no `messageId`/`correlationId`/`aggregateId`/`causationId` parsing, so CLAUDE.md R2-A7 (ULIDs, never `Guid.TryParse`) is not implicated.

### Testing standards

- xUnit v3 + Shouldly + NSubstitute (per `_bmad-output/project-context.md` testing rules and existing `CommandsControllerTenantTests`).
- `ParseOptionalResultPayload` is `private static`; prefer testing through the public `Submit` path (drive a mocked `IMediator` returning a crafted `SubmitCommandResult`, then assert `((AcceptedResult)result).Value` cast to `SubmitCommandResponse`). This matches the existing test style and avoids reflection.
- The existing `CommandsControllerTenantTests.CreateControllerWithMediator` returns `new SubmitCommandResult("test-correlation-id")` (null payload) — extend it or add a sibling helper that sets `ResultPayload`.
- Run the narrowest filter first (`FullyQualifiedName~CommandsController`) before broad build/test.
- `Hexalith.EventStore.Server.Tests` has a documented pre-existing CA2007 warning-as-error note in this workspace; verify before blaming this story for any unrelated build break.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-27-eventstore-parties-review-residuals.md#ES-1`] — residual scope: `MaxDepth` + max-length cap; logging already present, do not re-add.
- [Source: `src/Hexalith.EventStore/Controllers/CommandsController.cs:120-139`] — primary file to modify (`ParseOptionalResultPayload`).
- [Source: `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs:55`] — `JsonDocumentOptions { MaxDepth = 64 }` field precedent.
- [Source: `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs:699-718`] — `64 * 1024` pre-parse size-cap precedent.
- [Source: `_bmad-output/implementation-artifacts/post-epic-deferred-dw3-admin-debugging-json-large-stream-hardening.md`] — prior "depth cap in source, not implicit" rationale.
- [Source: `src/Hexalith.EventStore.Server/Pipeline/Commands/SubmitCommand.cs:23`] — `SubmitCommandResult.ResultPayload` type.
- [Source: `src/Hexalith.EventStore/Models/SubmitCommandResponse.cs:8`] — response contract.
- [Source: `tests/Hexalith.EventStore.Server.Tests/Controllers/CommandsControllerTenantTests.cs`] — controller test harness to reuse.
- [Source: `_bmad-output/project-context.md`] — Rule 5/NFR12 (no payload content in logs), testing conventions, warnings-as-errors.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.7 (claude-opus-4-7[1m]) via bmad-dev-story workflow.

### Debug Log References

- `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~CommandsController"` → **Passed! Failed: 0, Passed: 22, Skipped: 0** (12 pre-existing `CommandsControllerTenantTests` + 10 new ES-1 cases). One compile iteration first: the test double's `ILogger.Log` formatter must be `Func<TState, Exception?, string>` (initially written as `Func<TState, string>` → CS0535), fixed and green.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` → **Build succeeded. 0 Warning(s) / 0 Error(s)** (full solution, all projects incl. `Server.Tests`). The documented pre-existing CA2007 warning-as-error note did not surface in this run.

### Completion Notes List

- **AC1 (depth bound):** Added `private static readonly JsonDocumentOptions _payloadParseOptions = new() { MaxDepth = 64 };` to `CommandsController` (declared once, not per call) and threaded it into `JsonDocument.Parse(resultPayload, _payloadParseOptions)`, mirroring `AdminStreamQueryController._payloadParseOptions`. This surfaces `JsonDocument`'s implicit default depth (64) in source — defense-in-depth made visible, not a new numeric limit. Over-depth input still throws `JsonException` and is caught by the **unchanged** existing `catch (JsonException)` branch; no second depth handler was added.
- **AC2 (length cap = the real DoS guard):** Added `private const int MaxResultPayloadCharacters = 64 * 1024;` and a pre-parse early return that runs **before** `JsonDocument.Parse`. **Chars vs bytes:** chose `resultPayload.Length` (UTF-16 char count) — a cheap O(1) guard that avoids allocating a UTF-8 buffer over a potentially-hostile string just to measure it. Since UTF-8 byte length is always ≥ the UTF-16 char count, a 64 KiB char cap also bounds the byte size (worst case ≈3 bytes/char for non-ASCII BMP, still bounded and small). The `64 * 1024` default matches the existing `DaprInfrastructureQueryService.MaxFormatStateValueBytes` precedent.
- **AC3 (observable, no payload leak):** The length-cap branch emits exactly one **new, distinct** `LogWarning` carrying `correlationId` plus the cap and observed lengths as **numbers only** — no payload content, command payload, secrets, or display values (CLAUDE.md Rule 5 / NFR12). The pre-existing malformed-JSON warning at the `catch (JsonException)` was left byte-for-byte unchanged (not moved, duplicated, or rewritten).
- **AC4 (no regression):** Valid object/array/scalar/`null`-shaped payloads still round-trip to the same parsed `JsonElement`; `null`/empty/whitespace still returns `null` via the existing `IsNullOrWhiteSpace` early return; malformed in-bound still returns `null` via the existing warning. The public `SubmitCommandResponse` contract, `202`/Location/Retry-After behavior, and inbound `[RequestSizeLimit(1_048_576)]` are untouched.
- **AC5 (Tier 1 coverage):** New `CommandsControllerResultPayloadTests` drives the public `Submit` path with a mocked `IMediator` returning crafted `SubmitCommandResult.ResultPayload` values, using a `CapturingLogger<CommandsController>` test double to assert log content. The oversized case is **regression-sensitive**: it uses a *valid* JSON string longer than the cap, so the pre-change unbounded code would have parsed it and returned a non-null `JsonElement` — the test asserts `null`, and so fails against the old implementation.
- **Scope discipline:** No change to ES-3's drop site (`SubmitCommandHandler.cs`), no new `ProblemDetails` shape, no ID-parsing touched (R2-A7 not implicated). Oversized/malformed result payloads degrade gracefully to `resultPayload: null` on an otherwise-successful `202`, exactly as malformed JSON did before.

### File List

- `src/Hexalith.EventStore/Controllers/CommandsController.cs` (modified) — added `MaxResultPayloadCharacters` const + `_payloadParseOptions` field; added pre-parse length-cap branch with no-payload warning; passed depth-bounded options into `JsonDocument.Parse`.
- `tests/Hexalith.EventStore.Server.Tests/Controllers/CommandsControllerResultPayloadTests.cs` (added) — 10 Tier 1 cases covering oversized (regression-sensitive), over-depth, valid object/array/scalar/json-null, null/empty/whitespace, and malformed in-bound.

## Verification Status

- **Targeted tests:** `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~CommandsController"` → Failed: 0, Passed: 22, Skipped: 0 (Duration ~288 ms).
- **Release build:** `dotnet build Hexalith.EventStore.slnx --configuration Release` → Build succeeded, 0 Warning(s) / 0 Error(s) (entire solution).
- **All 5 ACs satisfied;** all tasks/subtasks `[x]`; only permitted story sections modified.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-27 | 0.1 | Created ready-for-dev post-Epic-22 ES-1 story: explicit `MaxDepth = 64` + pre-parse length cap on `CommandsController.ParseOptionalResultPayload`; Tier 1 coverage; malformed-JSON warning preserved (not re-added). | Claude Opus 4.7 |
| 2026-05-27 | 1.0 | Implemented ES-1: added `MaxResultPayloadCharacters = 64 * 1024` (char-count cap) + `_payloadParseOptions { MaxDepth = 64 }` on `CommandsController`; pre-parse length-cap drop branch with correlationId + numeric-only warning; depth-bounded `JsonDocument.Parse`; existing `JsonException` warning unchanged. Added `CommandsControllerResultPayloadTests` (10 cases, oversized case regression-sensitive). Tier 1: 22/22 pass. Release build: 0/0. Status → review. | Claude Opus 4.7 |
