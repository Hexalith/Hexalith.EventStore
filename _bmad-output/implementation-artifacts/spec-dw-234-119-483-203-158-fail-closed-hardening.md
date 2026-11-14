---
title: 'Five deferred server fail-closed defects (DW-234, DW-119, DW-483, DW-203, DW-158)'
type: 'bugfix'
created: '2026-11-14'
status: 'draft'
route: 'dispatch'
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
- [ ] `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs` -- set `cloudevent.id` to `safeMessage.Command.MessageId` -- per-message identity (DW-234)
- [ ] `tests/.../Events/DeadLetterPublisherTests.cs` -- update the metadata assertion; add a same-CorrelationId/different-MessageId test and a drain-exhaustion id test
- [ ] `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs` -- capability branch: `request.Domains is not { Count: 1 }` -- 400 instead of NRE (DW-119)
- [ ] `tests/.../EventStoreDomainServiceExtensionsTests.cs` -- add a theory for absent and explicit-null `domains` via raw JSON
- [ ] `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- `GetEventsAsync` guard `< 0` (DW-483)
- [ ] `tests/.../Actors/AggregateActorGetEventsTests.cs` -- `CurrentSequence 0 → []` and `-1 → InvalidOperationException`
- [ ] `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs` -- validate `requestTimeout` in (0, 5 min] in the field initializer; reword the :42 comment (DW-203)
- [ ] `tests/.../Queries/DaprETagServiceTests.cs` -- theory for rejected values; boundary 5 min accepted
- [ ] `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` -- add a `.Validate` for nonnegative limits (DW-158)
- [ ] `tests/.../Configuration/EventStoreServerServiceCollectionExtensionsTests.cs` -- negative rejected, zero accepted
- [ ] `_bmad-output/implementation-artifacts/deferred-work.md` -- close the seven entries (`status: done 2026-11-14` + `resolution:`)

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
