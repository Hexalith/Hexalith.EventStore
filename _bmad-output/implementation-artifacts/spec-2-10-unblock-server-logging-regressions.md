---
title: 'Unblock Story 2.10 Tier 1 validation by fixing pooled logging assertions'
type: 'bugfix'
created: '2026-07-21'
status: 'done'
review_loop_iteration: 0
baseline_commit: '9b1fd9584362acf5d31c22375a7998ad06b0524f'
context:
  - '_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Tier 1 validation is blocked by 11 Server test failures after `Microsoft.Extensions.Http.Resilience` transitively enabled Microsoft.Gen.Logging 10.8.0. Its source-generated methods clear their pooled `LoggerMessageState` after `ILogger.Log` returns, while the failing NSubstitute assertions inspect or format that mutable state later and therefore observe null values even though production logging is correct.

**Approach:** Update only the affected test harnesses and assertions to use the existing synchronous `TestLogger<T>`, which renders each message during `Log` and stores an immutable `LogEntry`. Preserve every existing behavioral assertion and run both focused Server tests and the complete Tier 1 suite.

## Boundaries & Constraints

**Always:** Keep the correction test-only; capture generated messages synchronously; continue asserting the intended levels, correlation context, tenant/domain/topic data, and payload exclusion; follow xUnit v3, Shouldly, and repository formatting conventions.

**Ask First:** Any required production-code change, SDK/package rollback, dependency update, public contract change, or relaxation of an existing logging requirement.

**Never:** Replace production `[LoggerMessage]` methods with extension-method logging; retain delayed inspection of generated state; remove, skip, or weaken the failing tests merely to make Tier 1 pass; alter Story 2.10 routing-header behavior.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Actor receipt | Valid command reaches `AggregateActor` | Captured Debug entry renders `Actor activated` before pooled state is cleared | Test fails if the entry is absent or rendered without its template values |
| Event publication succeeds | One event with correlation `corr-log` | Information entry includes success text, correlation, tenant and domain, while excluding payload data | Test fails on missing context or payload leakage |
| Event publication fails | Dapr publish throws for correlation `corr-fail` | Error entry includes failure text, correlation and derived topic | Existing publisher result behavior remains unchanged |
| Dead-letter path | Domain, rehydration, persistence, or sidecar failure occurs | Immutable entries preserve the correlation chain and diagnostic tenant/domain context across stages | Existing failure and dead-letter semantics remain unchanged |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.Server.Tests/TestUtilities/TestLogger.cs` -- existing synchronous formatter and immutable `LogEntry` recorder to reuse unchanged unless a demonstrated gap requires approval.
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTestHelper.cs` -- shared actor construction; needs opt-in logger injection without disrupting mock-based tests.
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTests.cs` -- one delayed `Arg.Is<object>` message assertion.
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs` -- two delayed message-content assertions and publisher factory setup.
- `tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterOriginTracingTests.cs` -- seven failures using a post-call `ReceivedCalls()` formatter replay helper.
- `tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterTraceChainTests.cs` -- one failure using the same deferred-capture pattern.

## Tasks & Acceptance

**Execution:**
- [x] `AggregateActorTestHelper.cs`, `AggregateActorTests.cs` -- allow an explicit logger for the receipt test and assert the synchronously recorded entry while preserving NSubstitute defaults for other actor tests.
- [x] `EventPublisherTests.cs` -- let the relevant factory path record immutable entries and rewrite both content assertions against those entries.
- [x] `DeadLetterOriginTracingTests.cs` -- use `TestLogger<AggregateActor>` entries directly and remove reflection/NSubstitute log replay.
- [x] `DeadLetterTraceChainTests.cs` -- synchronously capture actor messages for the sidecar failure test and remove its deferred extraction helper.

**Acceptance Criteria:**
- Given Microsoft.Gen.Logging clears generated state after each call, when affected tests inspect captured output, then they read immutable messages rendered inside `ILogger.Log`.
- Given the 11 previously failing tests, when focused validation runs, then all 11 pass without production changes or reduced assertions.
- Given the repository at the completed change, when `bash scripts/ci-local.sh --tier 1` runs, then every Tier 1 project passes.
- Given Story 2.10 is already implemented, when the final diff is reviewed, then its routing-header production surface remains untouched.

## Spec Change Log

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release --no-restore` -- expected: clean build with warnings treated as errors.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Observability.DeadLetterOriginTracingTests` -- expected: all origin-tracing regressions pass.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -method Hexalith.EventStore.Server.Tests.Observability.DeadLetterTraceChainTests.SidecarUnavailable_DeadLetterFailure_ErrorLogHasFullCorrelationContext` -- expected: the affected trace-chain regression passes.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -method Hexalith.EventStore.Server.Tests.Events.EventPublisherTests.PublishEventsAsync_LogsSuccess_WithoutPayloadData` -- expected: the publisher success logging regression passes.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -method Hexalith.EventStore.Server.Tests.Events.EventPublisherTests.PublishEventsAsync_LogsFailure_WithCorrelationIdAndTopic` -- expected: the publisher failure logging regression passes.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -method Hexalith.EventStore.Server.Tests.Actors.AggregateActorTests.ProcessCommandAsync_ValidCommand_LogsCommandReceipt` -- expected: the actor receipt logging regression passes.
- `bash scripts/ci-local.sh --tier 1` -- expected: complete Tier 1 validation passes.

## Suggested Review Order

**Approved boundary**

- Start with the test-only intent and explicit production-code exclusions.
  [`spec-2-10-unblock-server-logging-regressions.md:12`](spec-2-10-unblock-server-logging-regressions.md#L12)

**Synchronous capture seams**

- Capture actor messages before Microsoft.Gen.Logging clears its pooled state.
  [`DeadLetterOriginTracingTests.cs:66`](../../tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterOriginTracingTests.cs#L66)

- Preserve NSubstitute defaults while enabling opt-in synchronous actor capture.
  [`AggregateActorTestHelper.cs:57`](../../tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTestHelper.cs#L57)

- Expose the same opt-in seam for publisher logging tests.
  [`EventPublisherTests.cs:57`](../../tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs#L57)

**Regression assertions**

- Verify the complete rendered actor-receipt message, including every identity field.
  [`AggregateActorTests.cs:47`](../../tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTests.cs#L47)

- Prove success context while excluding plaintext and encoded payload markers.
  [`EventPublisherTests.cs:548`](../../tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs#L548)

- Require explicit correlated lifecycle stages instead of relying on log counts.
  [`DeadLetterOriginTracingTests.cs:143`](../../tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterOriginTracingTests.cs#L143)

- Preserve exact tenant and domain diagnostics on dead-letter publication failure.
  [`DeadLetterTraceChainTests.cs:546`](../../tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterTraceChainTests.cs#L546)

**Review disposition**

- Track the pre-existing Information-level filtering limitation outside this unblock.
  [`deferred-work.md:508`](deferred-work.md#L508)
