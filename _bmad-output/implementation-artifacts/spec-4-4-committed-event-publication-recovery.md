---
title: 'Story 4.4: Committed Event Publication Recovery'
type: 'feature'
created: '2026-08-07'
status: 'done'
review_loop_iteration: 2
story_key: '4-4-committed-event-publication-recovery'
baseline_commit: '37fdcd1fc8a238b676441b1f5a5ef5fd4370d27e'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Events become durable at `AggregateActor.cs:608`, but the drain record that schedules their re-publication is only committed at `:763` and its reminder is only registered at `:776`. A crash in that window leaves committed events with **nothing scheduled to publish them**: either an `EventsStored` pipeline checkpoint with no drain record, or a drain record with no reminder. Story 4.2 recovers these only on demand — a *new* command must arrive at that same aggregate — so an aggregate that goes quiet strands its events permanently. Separately, drain retries are unbounded (`IncrementRetry` at `:1409`/`:1442` is never compared to any ceiling), so an unpublishable event retries forever while the command status reports `PublishFailed`, whose own doc comment claims publication "permanently failed" and which exposes no retryable/recoverable signal at all.

**Approach:** Add one fixed-name actor-state index of outstanding committed-but-unpublished commands, staged into the *same* `SaveStateAsync` batch as the events, so it becomes durable exactly when they do. `OnActivateAsync` reads that single known key and re-arms whatever is missing — converting an orphan checkpoint into a drain record, or re-registering a lost reminder — then lets the existing reminder path do the publishing. Bound drain attempts, dead-letter on exhaustion, and surface retryability additively on the command status.

## Boundaries & Constraints

**Always:**
- The index entry is **staged** (`SetStateAsync`) into an existing batch and committed by an existing `SaveStateAsync`. It must never add a round trip or its own commit.
- `OnActivateAsync` wraps its whole body in try/catch and degrades to no-op on any failure, mirroring `ETagActor.cs:46`. Activation must never fail — a throwing activation bricks the aggregate.
- Activation re-arms only (create drain record / register reminder). It must not publish, call another actor, or perform unbounded I/O; reentrancy is disabled repo-wide, so activation-time actor calls deadlock.
- Recovery re-publishes the **full** persisted range and relies on `cloudevent.id == MessageId` for subscriber dedup, exactly as the existing drain does.
- Trailing-optional-parameter additions only on `CommandStatusRecord`/`ArchivedCommand`-shaped records, so older persisted records still deserialize.
- `ConfigureAwait(false)` on every await in `src/`; bare `await` in test bodies (`ConfigureAwait` there trips xUnit1030, which is an error).

**Ask First:**
- Any change that adds a value to the `CommandStatus` enum, changes an existing value's integer, or alters `IsTerminal` semantics.
- Any change to `EventPublisher.cs` beyond none — see Never.
- Adding a cross-aggregate sweep, background service, or shared-store ledger.

**Never:**
- Do **not** add a `CommandStatus` enum value. `FrontComposer`'s `EventStorePendingCommandStatusQuery.ParseStatus:121-132` throws `ProtocolFailure("UnknownStatus")` on any unrecognized name *and* on name/int mismatch, and `CommandStatusTests` pins `HasExactly8Values` / `TerminalCount_IsExactly4`. Retryability is an additive **field**.
- Do **not** add a loop or retry construct to `EventPublisher.cs`. `EventPublisherRetryComplianceTests.cs:103` reads that file off disk and fails on `using Polly`, `RetryPolicy`, `while (`, or `for (int retry`. Bounded-attempt logic belongs in `AggregateActor.cs`.
- Do **not** enumerate or query actor state. `IActorStateManager` (Dapr.Actors 1.18.5) has **no** key-enumeration API, and FR28 forbids `DaprClient.QueryStateAsync` over actor state (`AggregateActor.cs:39-40`, `EventPersister.cs:23`). All recovery reads must be by known key.
- Out of scope: a cross-aggregate sweep for aggregates never activated again; append fencing (Story 4.5); global-position changes (Story 4.6).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Crash after event commit, before drain record | Index entry + `EventsStored` checkpoint durable; no `drain:` record | Activation rebuilds the drain record from the checkpoint's range and registers the reminder; reminder publishes | Checkpoint missing or lacking `StartSequence`/`EndSequence` → drop the stale index entry, log a recovery diagnostic, never fabricate a range |
| Crash after drain commit, before reminder | Index entry + `drain:` record durable; no reminder | Activation re-registers the reminder from the existing record; `RetryCount` unchanged | Registration throws → leave entry and record intact for the next activation |
| Drain succeeds | Drain record + index entry present | Full range re-published with stable ids; record **and** index entry removed; pending count decremented; advisory status `Completed`/`Rejected` | n/a |
| Drain fails, below cap | `RetryCount < MaxDrainAttempts` | `RetryCount+1` persisted, reminder keeps firing, status exposes retryable | Existing `DrainReasonCodes` classification unchanged |
| Drain attempts exhausted | `RetryCount` reaches `MaxDrainAttempts` | Events dead-lettered; drain record + index entry removed; reminder unregistered; pending count decremented; status exposes non-retryable + `drain_attempts_exhausted` | Dead-letter publish fails → retain record, entry and reminder; do not drop events |
| Recoverable record past retention | Stored-but-unpublished, retry arrives after the 24h window | Still classified `Recoverable`; domain is **not** re-executed | Fail closed toward Recoverable, never toward a fresh miss |
| Activation, nothing outstanding | No index entry | No-op; exactly one extra state read | Read failure → degrade to no-op |

</frozen-after-approval>

## Code Map

Verified at `37fdcd1f`. Story 4.2 already shipped the drain record, its reminder protocol, `IdempotencyRecordDisposition.Recoverable`, and `CommandStatus.PublishFailed`; its spec (line 50) explicitly hands this story "broader activation/sweep and unrecoverable-publication semantics."

**Primary change site — `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (2606 lines)**
- `:44-62` class decl, already `IRemindable`. **No `OnActivateAsync`** — the new hook goes here.
- `:592-608` `EventsStored` checkpoint staged, then `SaveStateAsync` at `:608` = the durability point. **Stage the index entry into this batch.**
- `:665` publish call; `:686-701` success path (stage index removal, committed by `CompleteTerminalAsync` `SaveStateAsync` at `:2369`).
- `:714-763` publish-failure path: `PublishFailed` checkpoint `:725`, idempotency `Recoverable` `:737`, drain record staged `:756`, commit `:763`.
- `:776` `RegisterDrainReminderAsync` — **after** commit; `:1557-1572` swallows registration failure with "Manual recovery may be needed."
- `:1236` `DrainUnpublishedEventsAsync`; success `:1346-1405`; failure `:1407-1430`; exception `:1435-1462`. **`:1409` and `:1442` are the two `IncrementRetry` sites needing the cap.**
- `:1241` `SetTag("eventstore.message_id", trackingId)` — the deferred telemetry defect; `trackingId` is a correlationId for legacy records.
- `:1464-1508` `ClassifyDrainFailure` + boundary predicates — `internal static`, directly unit-testable. Add `drain_attempts_exhausted` handling around, not inside, the classifier.
- `:1510` existing `// TODO: Future — reconcile counter against actual drain:* record count on actor activation` — adjacent debt this hook makes tractable.
- `:1549` `StoreDrainRecordAndRegisterReminderAsync` **only stages** despite its name; `:1660` `HandoffStaleCommittedCheckpointAsync` is the existing checkpoint→drain-record conversion to reuse for activation.

**Reuse — mirror, do not reinvent**
- `src/Hexalith.EventStore.Server/Actors/ETagActor.cs:46` — the only `OnActivateAsync` in the repo; establishes catch-all-and-degrade.
- `src/Hexalith.EventStore.Server/Actors/UnpublishedEventsRecord.cs:18-49` — record shape, `StateKeyPrefix`/`GetStateKey`/`GetReminderName`/`IncrementRetry`. Model the new index file on it.
- `src/Hexalith.EventStore.Server/Actors/PipelineState.cs:30-41` — carries `StartSequence`/`EndSequence`/`MessageId`/`CausationId`; `AggregateActor.cs:1642` `CanRepresentCommittedEvents` and `:1652` `HasCompletePipelineIdentity` are the existing predicates for "this checkpoint represents committed events."
- `src/Hexalith.EventStore.Client/Projections/ProjectionDispatchOptions.cs:29` — `DefaultMaxRetryAttempts = 8`, the house bounded-attempt idiom to mirror in `EventDrainOptions`.
- `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs` + `IDeadLetterPublisher` — exhaustion sink. Note it keys `cloudevent.id` on `CorrelationId` (`:56`), a deliberately different scheme from event publication.

**Read-only evidence**
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs:200` — `["cloudevent.id"] = eventEnvelope.MessageId`. **AC2 already holds**; verify, do not change.
- `EventPublisher.cs:83` publishes per-event in a loop and returns a partial `PublishedCount`; the drain deliberately re-publishes the **full** range (`PersistThenPublishResilienceTests.cs:176`).
- `src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs:8-33` — all 8 states exist. `CommandStatusExtensions.cs:25` `IsTerminal` is `>= Completed`, the only classification axis.
- `src/Hexalith.EventStore/Controllers/ReplayController.cs:36-40` (replayable set includes `PublishFailed`), `:213` mints a **new** correlation id — AC3's "no identical-correlation resubmission" clause already holds structurally; verify.
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:207` — actor type name is configurable; never hardcode `nameof(AggregateActor)`.

**Change sites for the two inherited deferrals**
- `src/Hexalith.EventStore.Server/Actors/IdempotencyChecker.cs:97-120` — expiry check precedes the disposition branch, so `Recoverable` records expire and a later retry reads as a miss (domain re-execution risk).
- `src/Hexalith.EventStore.Server/Configuration/EventDrainOptions.cs` — has `InitialDrainDelay`/`DrainPeriod`/`MaxDrainPeriod` only; no attempt cap.

**Lifecycle facts established by loop-1 review — these drive the amended tasks**

- **Idempotency disposition has exactly three write sites** and only one is `Terminal`: `AggregateActor.cs:960` and `:2319` write `Recoverable`; `:2772` writes `Terminal` and is reachable *only* from `CompleteTerminalAsync`. Neither drain-success nor drain-exhaustion touches the disposition. Exempting `Recoverable` from expiry without adding a completion transition therefore makes the record immortal **and** permanently `RetryableRecoverable`.
- **Drain records are created at three sites**, all via `StoreDrainRecordAndRegisterReminderAsync`: `:975` (first-pass publish failure), `:2107` (`HandoffStaleCommittedCheckpointAsync`), `:2359` (`CompletePublishFailedAsync`, resume path). Any index staging attached to the command path alone covers only the first. `StoreDrainRecordAndRegisterReminderAsync` is the single choke point that covers all three.
- **`CanRepresentCommittedEvents` returns true whenever `EventCount > 0`** (`:2047`). A guard written as `!CanRepresentCommittedEvents(cp) || cp.EventCount is not > 0 || ...` has a dead first disjunct — it can never be the deciding term.
- **The tolerant runtime normalization is pinned by existing tests.** `PersistThenPublishResilienceTests.cs:320` and `:354` assert that negative/zero `InitialDrainDelay`/`DrainPeriod` are *supported* and normalized by `GetDrainReminderSchedule`, and `docs/guides/dapr-component-reference.md:734-735` documents that behavior. Adding `ValidateOnStart` over those same fields creates two contradictory policies and makes the normalization branches dead.
- **`ConfigureEventsInState` (`EventDrainRecoveryTests.cs:83-102`) stamps every seeded event with `MessageId = "msg-1"`**, so any assertion of the form `ShouldAllBe(id => id == "msg-1")` is satisfied by the fixture, not by identity fidelity. The substantive AC3 property is genuinely pinned elsewhere, at `tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs:123`.
- **`SeedPublicationIndex` re-stubs `TryGetStateAsync<UnpublishedPublicationIndex>` with a fixed value**, overriding any staging-tracking stub. A test that seeds the index cannot also prove that staging occurred.

**Tests that constrain the change**
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherRetryComplianceTests.cs:103` — source-text guard over `EventPublisher.cs`. Must stay green **unmodified**.
- `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusTests.cs:8,39` and `CommandStatusExtensionsTests.cs:39` — pin 8 values / 4 terminal. Must stay green **unmodified**; editing them signals an enum change that is out of bounds.
- `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs:32-67` (two-flavor actor factory incl. `ActorTimerManager`), `:128-165` (`AssertDrainIntegrityFailureAsync` reusable end-state block), `:167-195` (activity/tag capture), `:428` `DrainFails_ReminderContinuesFiring` — **this last one encodes today's unbounded retry and will need updating for the cap; that edit is expected and must be justified.**
- `tests/Hexalith.EventStore.Server.Tests/Events/PersistThenPublishResilienceTests.cs:93,176,412` — drain-record end-state exemplars, including the negative `DidNotReceive().SetStateAsync(s => s.StartsWith("drain:"))` at `:430`.
- `tests/Hexalith.EventStore.Server.Tests/TestUtilities/ActorStateManagerTestHelper.cs:20-25` — reflection injection of `Actor.StateManager`; `ETagActorTests.cs:41` shows how to invoke a non-public `OnActivateAsync` in a test.
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventPublisher.cs:65,96,73` — `SetupFailure`/`SetupPartialFailure`/`ClearFailure`. **It cannot throw**; to exercise `DrainPublishException` use `Substitute.For<IEventPublisher>().ThrowsAsync(...)` as `Dw8DrainReasonClassifierTests.cs:91` does. `FakeDeadLetterPublisher.cs:38` has `SetupFailure` for the exhaustion-sink-fails row.
- Conventions: xUnit v3, **Shouldly only** (3 raw `Assert.*` in the whole project, all placeholders), NSubstitute, `Member_Scenario_Expectation`, bare `await`.

**Correction to `project-context.md`:** its claim that `Server.Tests` has a known CA2007-as-error build failure (lines 34, 65) is **stale**. `tests/Directory.Build.props:10` has carried CA2007 in `NoWarn` since `41ecc97d` (2026-04-05); the project is in `.slnx:63` and CI runs it (`ci.yml:40`). Treat a red Server.Tests as a real regression.

## Tasks & Acceptance

**Execution:**

- [x] `src/Hexalith.EventStore.Server/Actors/UnpublishedPublicationIndex.cs` -- NEW record + fixed state-key constant holding the outstanding `(MessageId, CorrelationId)` entries -- one known key is the only thing an activation hook can read, since `IActorStateManager` cannot enumerate. Keep entries minimal: the drain record or the pipeline checkpoint remains the source of truth for the sequence range. De-duplicate by `MessageId`. Bound growth with a **dedicated** `MaxOutstandingPublicationEntries` on `EventDrainOptions`, not `BackpressureOptions.MaxPendingCommandsPerAggregate` — the two have unrelated lifetimes and the pending counter has its own drift guard. `Add` must report refusal to the caller rather than returning silently.
- [x] `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- carries the whole recovery behavior:
  - Stage the index entry inside `StoreDrainRecordAndRegisterReminderAsync` (the single choke point covering all three drain-record creation sites), **and** in the event-commit batch so an `EventsStored` checkpoint with no drain record is still recoverable. Stage removal in the terminal, drain-success and exhaustion batches.
  - **Fail closed at capacity.** If the index cannot accept an entry, the command must not commit events — surface the existing backpressure rejection rather than committing an unrecoverable range. Committing events with no recovery entry is the exact failure this story exists to remove.
  - Add `OnActivateAsync`: read the one index key, re-arm each entry, catch everything. **Bound the work per activation** (re-arm at most N entries, remainder on the next activation) so activation cannot block on many saves plus reminder registrations. Prune entries that are malformed or whose target no longer exists — `continue` without pruning permanently consumes capacity.
  - Cap both `IncrementRetry` sites at `MaxDrainAttempts`, checked **before** publication so the bound is real. On exhaustion, dead-letter first, then remove record + entry + reminder and decrement the pending count. Durably mark the record dead-lettered before the post-publish mutations so a fault between them cannot dead-letter the same range twice.
  - Fix the `:1241` telemetry tag to use `record.MessageId`; keep the reminder suffix under its own tag.
  - No guard may contain a disjunct that cannot decide (see the `CanRepresentCommittedEvents` fact in the Code Map).
- [x] `src/Hexalith.EventStore.Server/Actors/IdempotencyChecker.cs` + `AggregateActor.cs` -- `Recoverable` records are exempt from bounded expiry **only while their events are genuinely outstanding**. Drain success and drain exhaustion must both transition the record to `Terminal`, at which point normal retention resumes -- without this the record is immortal and every future retry of that message id returns `RetryableRecoverable` forever, even after the events were published or dead-lettered.
- [x] `src/Hexalith.EventStore.Server/Configuration/EventDrainOptions.cs` -- add `MaxDrainAttempts` (default 8, mirroring `ProjectionDispatchOptions.DefaultMaxRetryAttempts`) and `MaxOutstandingPublicationEntries`. **Do not add `ValidateOnStart` over the pre-existing timing fields** -- `PersistThenPublishResilienceTests.cs:320,354` and `docs/guides/dapr-component-reference.md:734-735` pin the tolerant normalization; two policies for one input is worse than one. Validate only the new fields, or extend the existing runtime normalization and keep it the single authority.
- [x] `src/Hexalith.EventStore.Server/Actors/DrainReasonCodes.cs` -- add `drain_attempts_exhausted` -- keeps the failure vocabulary bounded and stable-wire-valued. Reference this constant from `DeadLetterMessage`; it is `internal` in the same assembly, so no duplicate private const is needed.
- [x] `src/Hexalith.EventStore.Contracts/Commands/CommandStatusRecord.cs` -- append trailing optional recovery fields. Define `Retryable` as a real tri-state: `true` = a drain is armed and will retry; `false` = terminal, no further automatic attempt (exhausted, dead-lettered, or no reminder armed); `null` = legacy record predating the field. Never use `null` to mean "permanently failed".
- [x] `src/Hexalith.EventStore/Models/CommandStatusResponse.cs` -- surface the new fields through the poll endpoint mapping -- makes AC3 caller-visible.
- [x] `src/Hexalith.EventStore.Server/Events/DeadLetterMessage.cs` -- factory for the exhaustion sink. The reduced envelope must be unambiguously marked not replay-eligible and must fall back to the tracking id when the record has no `MessageId`.
- [x] `tests/Hexalith.EventStore.Server.Tests/Actors/PublicationRecoveryActivationTests.cs` -- NEW; cover every I/O Matrix row, driving `OnActivateAsync` by reflection per `ETagActorTests.cs:41`. A test that seeds the index cannot also prove staging occurred — keep those concerns in separate tests and make every recorded mutation attribution literally true.
- [x] `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs` -- exhaustion coverage, the actor-level at-capacity branch, drain-activity tag assertions, and the `Recoverable`→`Terminal` transition on both drain success and exhaustion. Give `ConfigureEventsInState` **distinct per-event message ids** so identity assertions cannot pass by fixture construction.
- [x] `tests/Hexalith.EventStore.Server.Tests/Commands/CommandStatusControllerTests.cs` -- assert `FromRecord` surfaces the three new fields through the poll endpoint -- AC3's deliverable is currently mapped but never asserted; deleting the mapping leaves every suite green.
- [x] `tests/Hexalith.EventStore.Server.Tests/Configuration/EventDrainOptionsTests.cs` + `Events/DeadLetterMessageTests.cs` -- cover `MaxDrainAttempts` default/binding, the new validation, and the reduced dead-letter envelope contract including the null-`MessageId` fallback.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusRecordTests.cs` -- round-trip a legacy record without the new fields -- proves the additive change did not break deserialization or the wire contract.

**Acceptance Criteria:**

- Given events are committed at `:608` and the process crashes before any drain record exists, when the actor is next activated, then the committed range is converted to a drain record with a registered reminder and is subsequently published, without any command being resubmitted.
- Given a drain record was committed but its reminder was never registered, when the actor is next activated, then the reminder is re-registered and `RetryCount` is unchanged.
- Given recovery re-publishes a previously committed range, when it runs more than once, then every CloudEvent id equals the persisted event `MessageId` and `EventPublisher.cs:200` is unchanged.
- Given `EventPublisherRetryComplianceTests.cs:103`, `CommandStatusTests.cs:8,39`, and `CommandStatusExtensionsTests.cs:39`, when the suite runs, then all pass **unmodified**.
- Given the whole change, when `git diff` is inspected, then `EventPublisher.cs` and `CommandStatus.cs` report no modification.
- Given a drain record is created by any of the three creation sites, when it is created, then a matching index entry exists — proven by a test per site, not only for the first-pass publish failure.
- Given the index is at capacity, when a command would commit events, then the events are not committed and the caller receives the existing backpressure rejection; no committed range is ever left without a recovery entry.
- Given a drain completes by success or by exhaustion, when the idempotency record is inspected afterwards, then its disposition is `Terminal` and normal retention applies; a later retry does not return `RetryableRecoverable`.
- Given each new guard and bound, when it is deliberately mutated (cap raised to `int.MaxValue`; index staging removed from each creation site independently; the capacity fail-closed branch removed; the `Recoverable`→`Terminal` transition removed; `OnActivateAsync` body emptied; the `CommandStatusResponse` mapping arguments dropped), then a named test fails for each. Report which test caught which mutation, and verify each attribution by actually applying that mutation — a claimed attribution that a re-run does not reproduce is a failed acceptance criterion.
- Given every guard in the change, when each is inspected, then none contains a condition that cannot decide the branch it guards.

### Review Findings

Chunk group 1 (core recovery) — 2026-08-11.

- [x] [Review][Decision] Post-commit index refusal fails open — resolved 2026-08-11: keep documented fail-open (drain+reminder remain the backstop).
- [x] [Review][Decision] `ReminderArmedAt` treated as proof a live reminder exists — resolved 2026-08-11: keep stamp-as-proof (accept rare lost-reminder stall).
- [x] [Review][Decision] Default index bound derives from backpressure — resolved 2026-08-11: keep derive-sentinel.
- [x] [Review][Decision] `Recoverable` expiry is disposition-only — resolved 2026-08-11: keep disposition+completion design; rely on completion-site patches.

- [x] [Review][Patch] Activation probe budget permanently starves unarmed tail entries [`AggregateActor.cs:2080`]
- [x] [Review][Patch] Handoff failure `ClearCache` can drop staged `Recoverable`→`Terminal` completions [`AggregateActor.cs:2185`]
- [x] [Review][Patch] Resume drain rewrite clears `DeadLettered` / `ReminderArmedAt` [`AggregateActor.cs:2641`]
- [x] [Review][Patch] Failed reminder registration still consumes the activation work budget [`AggregateActor.cs:2115`]
- [x] [Review][Patch] AppHost does not forward `MaxDrainAttempts` / `MaxOutstandingPublicationEntries` [`Program.cs:60`]
- [x] [Review][Patch] Post-commit index-refusal path has no test coverage [`AggregateActor.cs:1898`]
- [x] [Review][Patch] Index `InvalidEntry` refusal is logged as capacity threshold [`AggregateActor.cs:1906`]
- [x] [Review][Patch] Split `UnpublishedPublicationIndex.cs` to one type per file [`UnpublishedPublicationIndex.cs:1`]
- [x] [Review][Patch] Drain/recovery stamps use `DateTimeOffset.UtcNow` instead of `TimeProvider` [`AggregateActor.cs:1934`]
- [x] [Review][Patch] `TryCompleteRecoverableAsync` is public with bogus `inheritdoc` [`IdempotencyChecker.cs:175`]
- [x] [Review][Patch] Duplicate/orphaned XML docs on `ArmDrainReminderAsync` [`AggregateActor.cs:2199`]
- [x] [Review][Patch] Successful drain-record rebuild logs at Warning [`AggregateActor.cs:2181`]

- [x] [Review][Defer] Dead-letter republish if mark-save fails after broker accept [`AggregateActor.cs:1706`] — deferred, pre-existing on ledger
- [x] [Review][Defer] `Normalize` does not dedupe duplicate MessageIds [`UnpublishedPublicationIndex.cs:148`] — deferred, pre-existing on ledger
- [x] [Review][Defer] Commit-batch index staging order is not asserted by tests [`AggregateActor.cs:688`] — deferred, pre-existing on ledger

## Spec Change Log

### 1 — 2026-08-07, loop 1 (`bad_spec`)

**Trigger.** All three review layers independently found that the loop-1 code was green but not correct, and three findings were confirmed by direct inspection of the implementation:

1. `Recoverable` idempotency records became **immortal and permanently retryable**. The spec told the implementer to stop expiring them but never required a completion transition; there are only three disposition write sites and the sole `Terminal` one is reachable only from `CompleteTerminalAsync`, so a drained or dead-lettered command returned `RetryableRecoverable` forever and its actor state never expired.
2. The index was staged at **one of three** drain-record creation sites. The spec named "the `:608` batch" instead of the `StoreDrainRecordAndRegisterReminderAsync` choke point, leaving handoff- and resume-created drain records permanently un-re-armable.
3. Index-at-capacity **failed open** — events committed with no recovery entry, reproducing the exact crash-window failure the story exists to close. The spec said "bound growth" without specifying behavior at the bound.

Contributing: `ValidateOnStart` was added over pre-existing timing fields whose tolerant normalization is pinned by `PersistThenPublishResilienceTests.cs:320,354` and documented in `dapr-component-reference.md:734-735`, creating two contradictory policies and dead fallback branches; a guard at the activation site had a disjunct that could never decide; and AC3's caller-visible deliverable (`CommandStatusResponse.FromRecord`) was mapped but asserted nowhere.

**Amended.** Non-frozen sections only. Tasks now require: the `Recoverable`→`Terminal` transition on both drain completion paths; index staging at the single choke point plus the event-commit batch; fail-closed capacity with a dedicated bound rather than the backpressure ceiling; bounded per-activation re-arm work with pruning of dead entries; dead-letter-once durability ordering; an explicit `Retryable` tri-state; and no `ValidateOnStart` over the tolerant timing fields. Acceptance gained per-site index proof, the capacity invariant, the disposition invariant, a no-dead-guard criterion, and a requirement that every mutation attribution be reproduced rather than asserted. The Code Map gained a "Lifecycle facts established by loop-1 review" block recording the disposition sites, the three creation sites, the `CanRepresentCommittedEvents` shape, the pinned tolerant normalization, and the two fixture hazards.

**Known-bad state avoided.** A green build shipping unbounded, never-expiring idempotency state on every drained command; a recovery index blind to two thirds of the records it exists to recover; and a fail-open capacity branch that silently recreates the very crash window the story closes.

**KEEP (must survive re-derivation).**

- The core mechanism was right: one fixed-name actor-local index staged into an existing commit batch, with `OnActivateAsync` reading a single known key. Do not redesign toward a scan or a shared-store ledger.
- `OnActivateAsync` mirroring `ETagActor.cs:46` — whole-body catch, degrade rather than fail activation, re-arm only, never publish, never call another actor.
- The at-cap check placed **before** publication, so the bound is a real bound; and exhaustion dead-lettering **before** removing record/entry/reminder, retaining everything when the sink fails.
- `DrainReasonCodes.AttemptsExhausted` as a stable wire value; `MaxDrainAttempts` default 8 mirroring `ProjectionDispatchOptions.DefaultMaxRetryAttempts`.
- The telemetry fix: `eventstore.message_id` sourced from `record.MessageId`, tracking id under its own tag.
- Additive trailing-optional `CommandStatusRecord` fields with **no** `CommandStatus` enum change; `EventPublisher.cs` and `CommandStatus.cs` untouched. All three pinned guard-test files stayed green unmodified. Keep that.
- The `PublicationRecoveryActivationTests` structure (per-matrix-row coverage) and `CreateActorForBoundedDrain`. The cap mutation genuinely reddened exactly four named tests on re-run — keep that standard of evidence and extend it to every new guard.

## Design Notes

**Why an index rather than a scan.** `IActorStateManager` in Dapr.Actors 1.18.5 exposes only `Get/Set/TryGet/Remove/Contains/Save/Clear/Unload` — there is no `GetStateNamesAsync`. An activation hook therefore cannot discover `drain:*` or `{actorId}:pipeline:*` keys; it can only read names it already knows. One fixed-name index key is the minimum sufficient mechanism, and because it is staged into the batch that already commits the events, it is durable at exactly the same instant they are — the crash window is closed, not merely narrowed.

```
:608  SaveStateAsync  -> events + metadata + snapshot + checkpoint + INDEX ENTRY   (atomic)
:763  SaveStateAsync  -> drain record + INDEX ENTRY retained                       (atomic)
:2369 SaveStateAsync  -> terminal cleanup + INDEX ENTRY removed                    (atomic)
OnActivateAsync: read 1 known key -> per entry: drain record? re-arm reminder
                                              : checkpoint?   rebuild drain record
                                              : neither?      drop stale entry
```

**Activation must stay cheap and must never throw.** `ITenantValidatorActor.cs:15` states the house rule ("avoid expensive I/O in OnActivateAsync") and `ETagActor.cs:46` demonstrates catch-all degradation. Activation only re-arms; the reminder does the publishing. Reentrancy is disabled by design (`docs/guides/troubleshooting.md:333-336`), so calling out to another actor during activation would deadlock — `EventPublisher.cs:228` already carries a workaround comment for that hazard.

**Pending-count symmetry is the subtle hazard.** `pending_command_count` stays incremented while a drain is outstanding (`:759-760`, `finally` `:800`). Exhaustion now removes a drain record on a new path, so it must decrement exactly once — and the throw-out-of-publish path already decrements at `:802` while a later resume decrements again at `:1823`, guarded only by the `current <= 0` warning at `:1522-1526`. Any new removal path must be checked against that existing asymmetry rather than assumed balanced.

**Guards must be mutation-checked before being called green.** This is the repeated failure mode across stories 4.3, 3.6, and 3.4 in this repo: guards that pass by construction. A cap that is never reached, an index assertion satisfied by a fixture that never crashes, or an activation test whose actor has no outstanding entry all report safety they do not provide. Break each one deliberately and confirm it goes red.

## Verification

**Commands:**

- `dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false` -- expected: 0 warnings, 0 errors (`TreatWarningsAsErrors=true`).
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false` -- expected: all pass; record exact pass/fail/skip. This project builds today — a red result is a real regression, not the stale known failure.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false` -- expected: enum arity and terminal-count tests pass unmodified.
- `dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false` -- expected: no regression.

Run test projects individually — never solution-level `dotnet test`. `--filter FullyQualifiedName~<Class>` works for the inner loop.

**Manual checks (if no CLI):**

- Confirm `git diff --stat` lists no change to `EventPublisher.cs` or `CommandStatus.cs`.
- Confirm every new `await` in `src/` carries `ConfigureAwait(false)` and no test body does.

## Suggested Review Order

**The recovery mechanism**

- Start here: the fixed-key index, the whole design in one type.
  [`UnpublishedPublicationIndex.cs:56`](../../src/Hexalith.EventStore.Server/Actors/UnpublishedPublicationIndex.cs#L56)

- Refusal is a typed outcome, so at-capacity never masquerades as a data defect.
  [`UnpublishedPublicationIndex.cs:86`](../../src/Hexalith.EventStore.Server/Actors/UnpublishedPublicationIndex.cs#L86)

**Durability: the entry becomes durable exactly when the events do**

- Staged into the event-commit batch — closes the crash window, adds no round trip.
  [`AggregateActor.cs:646`](../../src/Hexalith.EventStore.Server/Actors/AggregateActor.cs#L646)

- The single choke point covering all three drain-record creation sites.
  [`AggregateActor.cs:1855`](../../src/Hexalith.EventStore.Server/Actors/AggregateActor.cs#L1855)

- Fail-closed at capacity: events are discarded rather than committed unrecoverably.
  [`AggregateActor.cs:1945`](../../src/Hexalith.EventStore.Server/Actors/AggregateActor.cs#L1945)

**Activation re-arm**

- The hook: reads one known key, catches everything, never fails activation.
  [`AggregateActor.cs:116`](../../src/Hexalith.EventStore.Server/Actors/AggregateActor.cs#L116)

- Per-entry re-arm, dual budgets, and pruning of entries that can never recover.
  [`AggregateActor.cs:2026`](../../src/Hexalith.EventStore.Server/Actors/AggregateActor.cs#L2026)

- Stage term added in review: an already-published checkpoint must not be republished.
  [`AggregateActor.cs:2113`](../../src/Hexalith.EventStore.Server/Actors/AggregateActor.cs#L2113)

- Stamped only after confirmed registration, so a lost reminder is never skipped.
  [`UnpublishedEventsRecord.cs:74`](../../src/Hexalith.EventStore.Server/Actors/UnpublishedEventsRecord.cs#L74)

**Bounded attempts and the dead-letter sink**

- The cap is checked before publication, so the bound is real.
  [`AggregateActor.cs:1398`](../../src/Hexalith.EventStore.Server/Actors/AggregateActor.cs#L1398)

- Dead-letter first, mark durably, then remove — a fault cannot dead-letter twice.
  [`AggregateActor.cs:1621`](../../src/Hexalith.EventStore.Server/Actors/AggregateActor.cs#L1621)

- Reduced envelope, explicitly not replay-eligible, falling back to the tracking id.
  [`DeadLetterMessage.cs:104`](../../src/Hexalith.EventStore.Server/Events/DeadLetterMessage.cs#L104)

**Idempotency lifecycle — the subtlest part**

- Recoverable records are exempt from retention only while events are outstanding.
  [`IdempotencyChecker.cs:190`](../../src/Hexalith.EventStore.Server/Actors/IdempotencyChecker.cs#L190)

- The release. Called on drain success, exhaustion, and all three prune paths.
  [`AggregateActor.cs:1923`](../../src/Hexalith.EventStore.Server/Actors/AggregateActor.cs#L1923)

- Kept off the public interface: adding a member would break external implementers.
  [`IIdempotencyChecker.cs:31`](../../src/Hexalith.EventStore.Server/Actors/IIdempotencyChecker.cs#L31)

**Caller-visible contract**

- Additive trailing optional fields; no `CommandStatus` enum value was added.
  [`CommandStatusRecord.cs:39`](../../src/Hexalith.EventStore.Contracts/Commands/CommandStatusRecord.cs#L39)

- Surfaced through the poll endpoint, which is what makes AC3 observable.
  [`CommandStatusResponse.cs:41`](../../src/Hexalith.EventStore/Models/CommandStatusResponse.cs#L41)

- New bounded, stable wire value for the exhaustion reason.
  [`DrainReasonCodes.cs:19`](../../src/Hexalith.EventStore.Server/Actors/DrainReasonCodes.cs#L19)

**Configuration**

- Both bounds normalized at point of use; no second startup-time policy.
  [`EventDrainOptions.cs:63`](../../src/Hexalith.EventStore.Server/Configuration/EventDrainOptions.cs#L63)

**Tests**

- Every I/O-matrix row, driving the activation hook by reflection.
  [`PublicationRecoveryActivationTests.cs:1`](../../tests/Hexalith.EventStore.Server.Tests/Actors/PublicationRecoveryActivationTests.cs#L1)

- Each index branch exercised directly, pinning the two refusal reasons apart.
  [`UnpublishedPublicationIndexTests.cs:1`](../../tests/Hexalith.EventStore.Server.Tests/Actors/UnpublishedPublicationIndexTests.cs#L1)

- Exhaustion, at-capacity, and the Recoverable-to-Terminal transition on both paths.
  [`EventDrainRecoveryTests.cs:1`](../../tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs#L1)
