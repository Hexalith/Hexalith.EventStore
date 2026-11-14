---
title: 'Five deferred server fail-closed defects (DW-234, DW-119, DW-483, DW-203, DW-158)'
type: 'bugfix'
created: '2026-11-14'
status: 'done'
route: 'dispatch'
baseline_commit: '325c3ddcf'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five open, EventStore-owned deferred-work defects are still live at `f80ca1bba`. Two dead-letters that share a correlation id share one `cloudevent.id`, so subscribers can drop one as a duplicate (DW-234, HIGH). A capability-bound metadata request with no `domains` returns 500 (DW-119). A touched-empty stream throws from `GetEventsAsync` (DW-483). A bad ETag request timeout fails open forever (DW-203). Negative domain-service result limits only fail at first invocation (DW-158).

**Approach:** Fix each defect at its source with one focused regression test. Then close the five entries in `deferred-work.md`, plus DW-81 and DW-161, which current code and tests already resolve.

## Boundaries & Constraints

**Always:** Keep each fix local to the named member and follow the file's existing brace style. Use the existing reason codes, exception types and message conventions. Ledger closures change only `status:` and add a `resolution:` line that cites the test.

**Never:** Do not touch DW-471/DW-309/DW-277; durable dead-letter outbox work is out of scope. No public API additions, no change to `IDeadLetterPublisher`, no new options keys, and no edits under `references/`. Keep zero valid for `MaxEventsPerResult` and `MaxEventSizeBytes`: the invoker already defines zero as "admits only an empty boundary".

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| DW-234 distinct commands | two dead-letters, same CorrelationId, different `Command.MessageId` | distinct `cloudevent.id` = each `Command.MessageId` | N/A |
| DW-234 drain exhaustion | `FromDrainExhaustion` message | `cloudevent.id` = reduced command MessageId (tracking identity) | N/A |
| DW-119 missing domains | capability-bound request (`appId`+`serviceVersion` set), `domains` absent or `null` | 400 with `UnsupportedCapability` | no 500 |
| DW-483 touched-empty | metadata present, `CurrentSequence == 0` | `GetEventsAsync` returns `[]` | negative still throws `InvalidOperationException` |
| DW-203 bad timeout | `requestTimeout` ≤ 0, `InfiniteTimeSpan`, or > 5 min | constructor throws `ArgumentOutOfRangeException` | null keeps the 3 s default |
| DW-158 negative limits | `MaxEventsPerResult` or `MaxEventSizeBytes` < 0 in config | host start fails with `OptionsValidationException` | 0 accepted |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs:57` -- `cloudevent.id` = CorrelationId. `Command.MessageId` is never blank (`CommandEnvelope`); `EventPublisher.cs:225` already keys on MessageId.
- `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:644` -- capability-bound branch dereferences `request.Domains.Count`; the other branch tolerates null.
- `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs:1794` -- reuse this test's host + `InvokeEndpointAsync` harness.
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:2158` -- `GetEventsAsync` uses `<= 0`; siblings at :2233 and :2577 use `< 0`. Mirror them.
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorGetEventsTests.cs:690` -- `GetEventsAsync_*` tests, `ConfigureMetadata` helper.
- `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs:22` -- `_proxyOptions` field initializer; the :42 comment hardcodes "3 s".
- `tests/Hexalith.EventStore.Server.Tests/Queries/DaprETagServiceTests.cs` -- 5 s and 30 s (also live-sidecar) must stay valid.
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:238` -- startup `.Validate` covers only the timeout; mirror `DaprDomainServiceInvoker.cs:356`.
- `tests/Hexalith.EventStore.Server.Tests/Configuration/EventStoreServerServiceCollectionExtensionsTests.cs:258` -- timeout validation tests to mirror.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs` -- set `cloudevent.id` to `safeMessage.Command.MessageId` -- per-message identity (DW-234)
- [x] `tests/.../Events/DeadLetterPublisherTests.cs` -- update the metadata assertion; add a same-CorrelationId/different-MessageId test and a drain-exhaustion id test
- [x] `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs` -- capability branch: `request.Domains is not { Count: 1 }` -- 400 instead of NRE (DW-119)
- [x] `tests/.../EventStoreDomainServiceExtensionsTests.cs` -- add a theory for absent and explicit-null `domains` via raw JSON
- [x] `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- `GetEventsAsync` guard `< 0` (DW-483)
- [x] `tests/.../Actors/AggregateActorGetEventsTests.cs` -- `CurrentSequence 0 → []` and `-1 → InvalidOperationException`
- [x] `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs` -- validate `requestTimeout` in (0, 5 min] in the field initializer; reword the :42 comment (DW-203)
- [x] `tests/.../Queries/DaprETagServiceTests.cs` -- theory for rejected values; boundary 5 min accepted
- [x] `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` -- add a `.Validate` for nonnegative limits (DW-158)
- [x] `tests/.../Configuration/EventStoreServerServiceCollectionExtensionsTests.cs` -- negative rejected, zero accepted
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- close the seven entries (`status: done 2026-11-14` + `resolution:`)

**Acceptance Criteria:**
- Given the touched projects, when each test project is built and its assembly run, then every new and existing test passes.
- Given the ledger, when the DeferredWorkGovernance tests run, then they pass (or skip as before).

## Design Notes

DW-234 uses `Command.MessageId` and no new override field. It differs per command and stays stable for one command, so a crash-window republish (DW-471) keeps the same id. A replayed command that reuses its MessageId and fails again also reuses the id. That is accepted: it is the same logical message.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Server.Tests -c Debug` then run the built assembly with `-class` for the four touched classes -- expected: all pass
- `dotnet build tests/Hexalith.EventStore.DomainService.Tests -c Debug` then run `-class Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests` -- expected: all pass
- `dotnet test tests/Hexalith.EventStore.DeferredWorkGovernance.Tests` -- expected: pass/skip unchanged

## Review Triage Log

| # | Source | Finding | Verdict | Evidence | Route |
|---|--------|---------|---------|----------|-------|
| 1 | blind+edge+vg-other | DW-483 `GetEventsAsync` now accepts unqualified `CurrentSequence == 0` metadata that `EventStreamReader`/`EventPersister` reject unless `SourceProofCodec.IsQualifiedEmpty` | medium | Real: `EventStreamReader.cs:63` rejects unproven zero, while the read siblings (`AggregateActor.cs:2233`, `:2577`, P4-8P comment) accept any zero. The frozen matrix orders `CurrentSequence == 0 → []`, so gating on proof is excluded by intent; the read-vs-write contract is an owner design question | defer |
| 2 | blind+edge | DW-119 capability branch still accepts `domains` `[null]`, `[""]`, `["  "]` (passes `{ Count: 1 }`) | low | Real: raw JSON reaches the handler, and a null key makes `IsKeyedService` check the non-keyed registration, so the result is 200 instead of 400. Fix is a one-line tightening of the guard this diff edits | patch |
| 3 | blind | `AdminOperationalIndexMetadata.Request.Domains` declared non-nullable hides future null dereferences | low | Annotation change on public contract surface; no current unguarded dereference after the fix | reject |
| 4 | edge+blind | DW-203 positive timeout under 1 ms passes `> TimeSpan.Zero`; no minimum-boundary test | low | Real: `HttpClient` cancels via `CancelAfter`, which truncates to whole ms → 0 → every fetch times out, the same failure as zero. Direct bound correction | patch |
| 5 | blind | DW-203 5-minute cap is a bare literal without a named constant or rationale | low | Cosmetic; the literal appears in one predicate and its message; fix is a refactor, not a correction | reject |
| 6 | blind | DW-203 class comment ("never blocks") contradicts a 5-minute override | false | The production default stays 3 s; overrides exist for live-sidecar tests, and the window stays finite | reject |
| 7 | blind | DW-203 constructor lacks `<param>`/`<exception>` docs for the new throw | low | Cosmetic; primary-constructor class with no existing param docs | reject |
| 8 | blind+edge | DW-161 closed although Admin.UI, Sample.Api, Sample.BlazorUI, Testing OwnerSourceRead and Tenants.Api still use the fixed-value overload | medium | Real: `AdminUIServiceExtensions.cs:122`, `Sample.Api/Program.cs:42` capture the token at startup. The platform capability (generic overload + tests) exists; the resolution must name the remaining callers, and the migration is pre-existing work | patch (resolution text) + defer (caller migration) |
| 9 | blind | DW-161 entry keeps its duplicated `decision:` line | low | Pre-existing; Always rule limits closures to `status:` + `resolution:` | reject |
| 10 | blind+vg-other | DW-81 cited tests do not pin registration-time rejection of malformed ids on the fixed overload | low | Real: `DaprServiceInvocationRegistrationTests.cs:78-86` uses only `""`/`"   "`; reverting `EventStoreServiceCollectionExtensions.cs:70` to `ThrowIfNullOrWhiteSpace` stays green. Fix is one small theory | patch |
| 11 | blind | DW-234 legacy correlation-keyed drain records (null `MessageId`) still publish `cloudevent.id` = correlation id; ledger says ids stay distinct without caveat | low | Real but legacy-only: `UnpublishedEventsRecord.GetTrackingIdentity` falls back to the tracking id; the frozen matrix mandates the tracking identity. Identity change rejected; the ledger caveat is a direct text correction | patch (resolution text) |
| 12 | blind | DW-158 single combined message names both limit keys; test `Contains(limitKey)` is always true | low | Theory still proves each half (each case sets one key); splitting adds a second validation branch | reject |
| 13 | blind | DW-158 predicate duplicated from `DaprDomainServiceInvoker.ValidateOptions` | low | Spec directs mirroring; no named caller diverges today | reject |
| 14 | blind | `DomainServiceOptions` XML docs silent on nonnegative rule | low | Pre-existing doc gap; cosmetic | reject |
| 15 | blind | New DW-158 test names mix `AtHostStartupAsync` and `AtHostStartAsync` | low | Real naming drift from the sibling convention; direct rename | patch |
| 16 | blind | Dead-letter `cloudevent.id` change undocumented; docs tell consumers to match by correlation id | false | No doc describes the dead-letter `cloudevent.id`; `drain-failure-reason-codes.md` refers to the payload correlation ID, which is unchanged; in-repo consumers read `data`, not the CloudEvent id | reject |
| 17 | blind (evidence) | `deploy/README.md:46` documents event `cloudevent.id` as `{correlationId}:{sequenceNumber}` | low | Real, pre-existing: `docs/concepts/event-envelope.md:253` and `EventPublisher` use the persisted `messageId` | defer |
| 18 | blind | Dead-letter logs/activity tags do not carry `Command.MessageId` | low | Payload carries the command and its MessageId; fix adds a logger parameter and tag | reject |
| 19 | edge | Null `DeadLetterMessage.Command` causes a swallowed NRE | false | `Command` is non-nullable; `FromException` throws on null and `FromDrainExhaustion` builds a non-null envelope; both publish sites use these factories | reject |
| 20 | vg | Verification gaps in the five code changes | none | Layer reported no gaps; both test projects are in CI `unit-test-projects` | — |
