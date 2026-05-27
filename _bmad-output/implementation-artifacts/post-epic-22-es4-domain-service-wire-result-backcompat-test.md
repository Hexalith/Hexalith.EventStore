# Post-Epic 22 ES-4: DomainServiceWireResult Omitted-Payload Back-Compat Test

Status: done

Context created: 2026-05-27
Story key: `post-epic-22-es4-domain-service-wire-result-backcompat-test`
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-27-eventstore-parties-review-residuals.md` (finding ES-4)
Epic: Post-Epic-22 EventStore<->Parties Review Residuals
Scope: Minor (Developer). Add a Tier 1 contract fixture proving `DomainServiceWireResult` remains backward-compatible when older domain services omit the optional `ResultPayload` JSON member.

## Story

As an EventStore platform maintainer supporting independently deployed domain services,
I want a regression test that deserializes `DomainServiceWireResult` JSON without `ResultPayload`,
so that older domain-service responses continue to bind with `ResultPayload == null` if serializer settings or the record constructor change later.

## Background & Verified Residual

The residual from ES-4 is test debt, not a confirmed runtime bug. `DomainServiceWireResult` is currently a sealed positional record with `string? ResultPayload = null`, so omitted JSON should bind to `null`. The missing piece is an explicit Tier 1 fixture that locks this wire compatibility against future changes. Omission matters because older producers, payload-free domain services, or mixed-version deployments may never emit the newer optional `resultPayload` member; absence must remain valid wire input. This test verifies wire-format backward compatibility for clients/servers that emitted `DomainServiceWireResult` before `resultPayload` existed; the compatibility signal is successful deserialization of an omitted property, not handling an explicit null.

Current code path:

- `src/Hexalith.EventStore.Contracts/Results/DomainServiceWireResult.cs` defines the public wire DTO used by domain-service invocation.
- `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs` reads the response with `ReadFromJsonAsync<DomainServiceWireResult>(...)`.
- `DaprDomainServiceInvoker.ToDomainResult(...)` only wraps a payload-bearing `DomainResult` when `wireResult.ResultPayload` is non-empty; omitted/null payload must remain a normal success/rejection/no-op shape, not an exception.
- Existing tests cover `DomainResult`, command/query DTOs, projection adapter contracts, and ES-1/ES-2/ES-3 result-payload runtime behavior, but no test deserializes the older wire shape with `resultPayload` omitted.

This story must stay focused on the compatibility fixture. It should not change runtime behavior, contracts, logging, payload privacy posture, or serializer configuration.

## Acceptance Criteria

1. **Omitted `resultPayload` deserializes to null.**
   - Given a legacy wire JSON object for `DomainServiceWireResult` that contains `isRejection` and `events`
   - And the JSON omits the `resultPayload` member entirely
   - And the JSON does **not** include `"resultPayload": null`
   - And the test either visibly structures the raw fixture so the member is absent or asserts `json.ShouldNotContain("resultPayload")` before deserialization
   - When the fixture deserializes the JSON with `JsonSerializer.Deserialize<DomainServiceWireResult>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))`
   - Then deserialization succeeds
   - And the test explicitly asserts `result.ResultPayload.ShouldBeNull()`
   - And the existing `IsRejection` and `Events` values are preserved.

2. **The fixture uses a realistic wire event shape.**
   - The JSON uses camelCase member names: `isRejection`, `events`, `eventTypeName`, `payload`, and `serializationFormat`
   - The `payload` value is valid base64 for `byte[]`
   - The test asserts `Events.Count.ShouldBe(1)` before inspecting the event
   - The test asserts the event type name, `Payload.ShouldBe([1, 2, 3])`, and `SerializationFormat.ShouldBe("json")` so the fixture proves the surrounding wire shape still binds correctly
   - The test does not rely on an empty `events` array as the only proof of binding.

3. **The test lives at the contract boundary.**
   - Add a focused file such as `tests/Hexalith.EventStore.Contracts.Tests/Results/DomainServiceWireResultTests.cs`
   - Place the test in namespace `Hexalith.EventStore.Contracts.Tests.Results`, matching the folder path
   - Prefer a clearly named test such as `DeserializesLegacyWireJsonWithoutResultPayloadAsNull`
   - Use xUnit v3 and Shouldly, matching the existing `DomainResultTests` and contract DTO tests
   - Do not add dependencies to `Hexalith.EventStore.Contracts.Tests`
   - Do not place this in `Server.Tests` unless contract-level deserialization proves impossible, which should not be the case.

4. **No production code changes are required unless the new fixture proves a real incompatibility.**
   - Do not modify `DomainServiceWireResult` if the fixture passes against the current default constructor parameter
   - Do not add `[JsonConstructor]`, converters, custom serializer options, or `required` members unless the test fails and the Dev Agent Record explains why
   - Do not change `DaprDomainServiceInvoker`, `DomainResult`, `SubmitCommandHandler`, `CommandsController`, `PipelineState`, or any ES-1/ES-2/ES-3 payload logic.
   - Do not validate controller behavior, DAPR invocation, actor checkpointing, handler logging, or HTTP response parsing in this story; those belong to other test layers and ES rows.
   - Do not introduce surrogate/test DTOs or custom converters; the test must deserialize directly into `DomainServiceWireResult` using `JsonSerializerOptions(JsonSerializerDefaults.Web)`.
   - If production code changes are required, document the exact failing fixture behavior and keep the fix limited to `DomainServiceWireResult` deserialization compatibility.

5. **Regression-sensitive verification is recorded.**
   - First run the focused test filter after adding the test, for example:
     `dotnet test tests/Hexalith.EventStore.Contracts.Tests --filter "FullyQualifiedName~DomainServiceWireResultTests"`
   - Then run the whole contracts unit project:
     `dotnet test tests/Hexalith.EventStore.Contracts.Tests`
   - Record exact pass/fail counts in the Dev Agent Record
   - If any existing unrelated failure appears, classify it explicitly and do not alter unrelated tests.

## Tasks / Subtasks

- [x] **ST0 - Reconfirm the compatibility surface.** (AC: 1, 3, 4)
  - [x] Re-read `src/Hexalith.EventStore.Contracts/Results/DomainServiceWireResult.cs`.
  - [x] Re-read `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs` around `ReadFromJsonAsync<DomainServiceWireResult>` and `ToDomainResult`.
  - [x] Confirm existing contract test style in `tests/Hexalith.EventStore.Contracts.Tests/Results/DomainResultTests.cs` and nearby DTO round-trip tests.

- [x] **ST1 - Add the omitted-payload fixture.** (AC: 1, 2, 3)
  - [x] Add `DomainServiceWireResultTests` under `tests/Hexalith.EventStore.Contracts.Tests/Results/`.
  - [x] Use namespace `Hexalith.EventStore.Contracts.Tests.Results`.
  - [x] Add a test named `DeserializesLegacyWireJsonWithoutResultPayloadAsNull` or an equally explicit compatibility-focused name.
  - [x] Build a raw JSON fixture that omits `resultPayload` entirely; do not include `"resultPayload": null`.
  - [x] Make the omission visible by keeping the fixture readable and/or asserting `json.ShouldNotContain("resultPayload")`.
  - [x] Use `JsonSerializer.Deserialize<DomainServiceWireResult>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))`.
  - [x] Add a short test comment naming the scenario: older wire JSON omitted optional `resultPayload`.
  - [x] Assert the deserialized result is non-null, `ResultPayload.ShouldBeNull()` is explicit, `IsRejection` is preserved, `Events.Count.ShouldBe(1)`, and the wire event fields round-trip from JSON.
  - [x] Assert payload bytes with `Payload.ShouldBe([1, 2, 3])`, not only by length or the source base64 string.

- [x] **ST2 - Keep production behavior unchanged.** (AC: 4)
  - [x] Leave `DomainServiceWireResult` unchanged if the new test passes.
  - [x] Leave `DaprDomainServiceInvoker.ToDomainResult` unchanged; omitted/null payload should still produce a normal `DomainResult` without a payload wrapper.
  - [x] Do not change controller parsing, handler drop logging, or actor checkpoint payload scrubbing from ES-1/ES-2/ES-3.
  - [x] Do not add DAPR, HTTP, controller, actor, or handler-level tests for this row.
  - [x] Do not introduce surrogate/test DTOs, custom converters, or serializer settings beyond `JsonSerializerDefaults.Web`.
  - [x] If production code must change, document the exact failed deserialization behavior in the Dev Agent Record and keep the fix limited to this contract.

- [x] **ST3 - Validate and record evidence.** (AC: 5)
  - [x] Run the focused `DomainServiceWireResultTests` filter and record the result.
  - [x] Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests` and record the result.
  - [x] Update the Dev Agent Record, File List, Verification Status, and Change Log before moving the story to review.

## Dev Notes

### Current State Of Files To Update

`tests/Hexalith.EventStore.Contracts.Tests/Results/DomainServiceWireResultTests.cs`

- Current state: file does not exist.
- Required change: add a focused contract test proving an omitted `resultPayload` JSON member binds to `null`.
- Must preserve: existing xUnit v3 + Shouldly style; no new packages or test infrastructure.

Suggested fixture shape:

```json
{
  "isRejection": false,
  "events": [
    {
      "eventTypeName": "Hexalith.Sample.CounterIncremented",
      "payload": "AQID",
      "serializationFormat": "json"
    }
  ]
}
```

`AQID` is base64 for bytes `[1, 2, 3]`. The exact type name can be a stable test string; do not introduce a concrete event type just for this compatibility check. The fixture must omit `resultPayload` entirely, because `"resultPayload": null` is a different compatibility case.

### Files To Read But Avoid Editing Unless Needed

`src/Hexalith.EventStore.Contracts/Results/DomainServiceWireResult.cs`

- Current behavior: sealed positional record with `ResultPayload` optional and defaulting to `null`.
- Required attention: this is the contract being pinned. Do not modify it unless the new compatibility test fails.
- Must preserve: `FromDomainResult` only copies `result.ResultPayload` for success results; rejection and no-op results do not emit enriched payloads.

`src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs`

- Current behavior: reads `DomainServiceWireResult` from the domain-service HTTP response, then maps events back into serialized event payloads; payload wrapping only happens when a successful result has a non-empty `ResultPayload`.
- Required attention: this story does not need a server test because the residual is STJ contract deserialization of the wire DTO.
- Must preserve: no custom retry logic, response limit validation, no payload logging, and existing `ConfigureAwait(false)` style.

### Implementation Guardrails

- This is a **test-only** story unless the new fixture reveals a real incompatibility in current contract deserialization.
- Do not add `required` to `ResultPayload`; that would break the exact older-wire shape ES-4 is protecting.
- Do not switch to Newtonsoft.Json or a custom converter.
- Do not add a `[JsonIgnore]`/null-omit policy to `DomainServiceWireResult`; the test is about reading older omitted payloads, not changing current serialization output.
- Do not assert on a fully serialized JSON string from `DomainServiceWireResult`; property ordering and null emission are not the residual.
- Do not use DAPR or HTTP in the test. The contract test should deserialize the DTO directly so failures point at the wire shape, not infrastructure.
- Keep ES-3's dirty/in-review files intact if they are present in the worktree; ES-4 should only add the contracts test and update this story/status file during implementation.

### Previous Story Intelligence

ES-1 (`post-epic-22-es1-result-payload-parse-dos-guard`) is complete. It added controller-side result-payload parsing defenses: a 64 KiB char-count cap, explicit `JsonDocumentOptions { MaxDepth = 64 }`, and no-payload warnings for oversized or malformed result payloads. ES-4 must not touch controller parsing.

ES-2 (`post-epic-22-es2-pipeline-state-result-payload-privacy-posture`) is complete. It scrubbed `ResultPayload` from persisted actor `PipelineState` checkpoints while preserving normal no-crash terminal payload returns. ES-4 must not touch actor checkpoint behavior.

ES-3 (`post-epic-22-es3-result-payload-drop-observability`) is currently in review in this workspace. It adds a `SubmitCommandHandler` warning when a non-null payload is dropped because final status is not known to be `Completed`. ES-4 is independent and should not modify or revert ES-3 files.

### Git Intelligence

Recent commits show focused post-Epic hardening and payload work:

- `73c55513 docs(story): close ES-2 code review (#262)`
- `2a9fd3e8 fix(server): harden result payload handling`
- `3bfd4895 fix: update package paths to use Unix-style format for consistency`
- `e887c068 fix(events): publish system tenant events on domain topic`
- `abf39b43 chore: update submodule commits for Hexalith.Commons and Hexalith.Tenants`

Keep this story narrow: one contract regression test, then targeted contracts test validation.

### Latest Technical Information

- Microsoft documents that `System.Text.Json` can deserialize immutable types with parameterized constructors when the constructor is the only constructor, and constructor parameter matching is case-insensitive. Source: <https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/immutability>
- The .NET 9 System.Text.Json update notes that required constructor-parameter enforcement is opt-in. This matters because future serializer options or feature switches could turn omitted non-optional parameters into failures; ES-4 pins the intentionally optional `ResultPayload` member as omitted-safe. Source: <https://devblogs.microsoft.com/dotnet/system-text-json-in-dotnet-9/#respecting-non-optional-constructor-parameters>
- `HttpContentJsonExtensions.ReadFromJsonAsync` is the API used by `DaprDomainServiceInvoker` to read the domain-service response body. Source: <https://learn.microsoft.com/dotnet/api/system.net.http.json.httpcontentjsonextensions?view=net-10.0>

### Project Context Reference

Apply `_bmad-output/project-context.md`:

- Contracts are public package surface; preserve backward-compatible additive wire changes.
- Use `System.Text.Json`; do not introduce another serializer.
- Tests use xUnit v3, Shouldly, and no raw `Assert.*` unless already unavoidable in surrounding code.
- Run targeted test projects individually.
- Keep warnings clean because `TreatWarningsAsErrors=true`.

## References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-27-eventstore-parties-review-residuals.md#ES-4`] - residual scope: explicit fixture for omitted `ResultPayload`.
- [Source: `src/Hexalith.EventStore.Contracts/Results/DomainServiceWireResult.cs`] - wire DTO with optional `ResultPayload = null`.
- [Source: `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs`] - reads the wire DTO and maps payload-bearing successes back to `DomainResult`.
- [Source: `tests/Hexalith.EventStore.Contracts.Tests/Results/DomainResultTests.cs`] - existing contracts test style.
- [Source: `tests/Hexalith.EventStore.Contracts.Tests/Commands/SubmitCommandResponseTests.cs`] - existing `JsonSerializerDefaults.Web` contract round-trip style.
- [Source: `_bmad-output/implementation-artifacts/post-epic-22-es1-result-payload-parse-dos-guard.md`] - prior controller parsing boundaries.
- [Source: `_bmad-output/implementation-artifacts/post-epic-22-es2-pipeline-state-result-payload-privacy-posture.md`] - prior persisted checkpoint privacy posture.
- [Source: `_bmad-output/implementation-artifacts/post-epic-22-es3-result-payload-drop-observability.md`] - prior handler drop-observability context.
- [Source: `_bmad-output/project-context.md`] - contract, serializer, and testing rules.
- [External: System.Text.Json immutable type deserialization](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/immutability)
- [External: System.Text.Json required constructor parameter option](https://devblogs.microsoft.com/dotnet/system-text-json-in-dotnet-9/#respecting-non-optional-constructor-parameters)
- [External: HttpContentJsonExtensions.ReadFromJsonAsync](https://learn.microsoft.com/dotnet/api/system.net.http.json.httpcontentjsonextensions?view=net-10.0)

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Resolved `bmad-dev-story` workflow customization and loaded `_bmad-output/project-context.md`.
- 2026-05-27: Ran `aspire run --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --detach --format Json --non-interactive -- EnableKeycloak=false`; apphost started with dashboard `https://localhost:17017/login?t=fc026f11e903bb94f69d514927a3429a`.
- 2026-05-27: Ran `aspire describe eventstore --format Json --non-interactive`; `eventstore` was `Running` and `Healthy` with `http://localhost:8080`.
- 2026-05-27: Re-read `DomainServiceWireResult`, `DaprDomainServiceInvoker`, `DomainResultTests`, and `SubmitCommandResponseTests`.
- 2026-05-27: Ran `dotnet test tests/Hexalith.EventStore.Contracts.Tests --filter "FullyQualifiedName~DomainServiceWireResultTests"`: passed, 1/1.
- 2026-05-27: Ran `dotnet test tests/Hexalith.EventStore.Contracts.Tests`: passed, 513/513.
- 2026-05-27: Ran documented unit regression projects individually: Client.Tests passed 399/399, Sample.Tests passed 74/74, Testing.Tests passed 144/144.

### Completion Notes List

- Added a contract-boundary `DomainServiceWireResult` regression fixture that deserializes legacy camelCase wire JSON with omitted `resultPayload`.
- Verified the omitted member stays absent before deserialization and binds to `ResultPayload == null`.
- Verified the surrounding realistic event shape binds correctly, including event type, base64 payload bytes `[1, 2, 3]`, and serialization format.
- Production code remained unchanged; the compatibility fixture passed against the current DTO and invoker behavior.

### File List

- `_bmad-output/implementation-artifacts/post-epic-22-es4-domain-service-wire-result-backcompat-test.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `tests/Hexalith.EventStore.Contracts.Tests/Results/DomainServiceWireResultTests.cs`

## Verification Status

Ready for review. Focused and full contracts validation passed; documented unit regression projects passed. No production code changes were required.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-27 | 0.1 | Created ready-for-dev post-Epic-22 ES-4 story: add contract-level omitted `ResultPayload` deserialization fixture for `DomainServiceWireResult`. | Codex |
| 2026-05-27 | 1.0 | Implemented ES-4 contract fixture, validated focused/full contracts tests and documented unit regression projects, and moved story to review. | Codex |

## Story Completion Status

Implementation complete. Story is ready for review.
