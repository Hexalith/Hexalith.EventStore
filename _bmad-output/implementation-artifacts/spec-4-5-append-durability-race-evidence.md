---
title: 'Story 4.5: Append Durability Race Evidence'
type: 'chore'
created: '2026-08-08'
status: 'done'
review_loop_iteration: 1
story_key: '4-5-append-durability-race-evidence'
baseline_commit: '0776785f494fcefc8ad933b5b17b9c8d5cbe0513'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** FR31 (`prd.md:208`) and `epic-4-context.md:35` forbid any append-fencing work until a live-sidecar two-writer race and the *observed* DAPR conflict-exception surface are recorded and reviewed. Neither exists. Meanwhile the code already carries an entire conflict-handling apparatus that is probably inert: five `catch (InvalidOperationException)` sites (`AggregateActor.cs:686,842,2624,2971,3048`), a `MaxPersistenceConflictRetries` budget (`CommandConcurrencyOptions.cs:10`), a retry `goto` (`AggregateActor.cs:709`), and `AggregateMetadata.ETag` (`AggregateMetadata.cs:8`) which is written as literal `null` (`EventPersister.cs:137`) and never read. Nothing supplies an etag or concurrency option on the actor-state commit path, and the only tests that reach those catches inject the exception they expect (`AggregateActorDomainResultTests.cs:453`). Three docs assert the opposite of the code, and the one live ETag test can return green having asserted nothing (`ActorConcurrencyConflictTests.cs:144-148`).

**Approach:** Execute the CP-11 verify-first spike (`sprint-change-proposal-2026-07-04.md:234-239`) as an evidence deliverable. Add one LiveSidecar test that drives a genuine second writer at the same aggregate stream key, capture the real exception/status surface verbatim, publish a hash-bound evidence set plus a spike report, and record the add/change/defer fencing decision in the architecture spine. Confirm or flag the existing conflict handling — do not repair it.

## Boundaries & Constraints

**Always:**
- Every recorded claim is backed by committed raw output plus the exact re-runnable command that produced it. An unexpected-but-real outcome — including *no exception raised and a silent last-write-wins overwrite* — is a first-class finding, never a test failure.
- The new test lives in `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/`, carries `[Collection("DaprTestContainer")]` + `[Trait("Category", "LiveSidecar")]`, and uses unique per-test aggregate ids.
- Assert persisted state-store contents via the fixture's Redis readers, not HTTP status codes.
- Match each file's existing await convention: `ConfigureAwait(false)` in `src/`, `ConfigureAwait(true)` in LiveSidecar test bodies. Shouldly only.

**Ask First:**
- Any behavioral edit to `AggregateActor.cs` conflict handling, `CommandConcurrencyOptions`, `AggregateMetadata.ETag`, `EventPersister.cs`, or `GlobalPositionActor.cs`.
- Adding or amending a numbered `AD-<N>` entry in `architecture.md` (the spine's invariants are human-owned; a `## Deferred` row is not).
- Deleting `MetadataKey_StaleEtagUpdate_IsRejected` rather than making it assert.

**Never:**
- No fencing implementation, no etag or concurrency option introduced on the append path, no `MaxPersistenceConflictRetries` default change, no removal of the `catch (InvalidOperationException)` sites. This story produces the evidence that gates those choices; making them here defeats it.
- Do not add the LiveSidecar project to `unit-test-projects` in `.github/workflows/ci.yml:23-40`, and do not reintroduce a `Category!=LiveSidecar` filter (`docs/ci.md:60-62`).
- Do not record a conflict as observed unless committed raw output shows it. Do not infer runtime behavior from decompiled SDK source alone — decompilation may inform the report, but only live capture is evidence.
- Out of scope: global-position changes (Story 4.6); placement-failover / split-brain proof, which needs a topology this fixture does not provide.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Two writers, same stream key | Actor-driven command racing a raw actor-state transaction at `{prefix}{seq}` + metadata | Record which write survives, whether the persisted stream stays gapless and duplicate-free, and whether either writer was rejected | Neither rejected → record silent overwrite as the finding; still assert the stream shape actually observed |
| Conflict-surface capture | Raw actor-state transaction replayed with a stale etag / conflicting op | Record verbatim: HTTP status, response body, and the exception type surfaced through the actor path | No error surfaces → record "no conflict signalled"; do not synthesize one |
| `InvalidOperationException` reachability | Live sidecar exercised across the above | Report the catch sites as confirmed-live or flagged dead code, citing captured output | Evidence inconclusive → report `inconclusive` with the blocker; never assume either way |
| Actor-state key addressability | `identity.MetadataKey` read via generic state API vs composite actor key | Record which namespace is externally readable, so the evidence names the key it actually inspected | Not readable → the test must fail or assert the composite key, never pass vacuously |
| Concurrent same-aggregate commands | N commands, one actor proxy (existing `EventPersistenceIntegrationTests.cs:160`) | Cited as the already-proven serialized case; not re-implemented | n/a |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Server/Events/EventPersister.cs:19,52-56,76-99,125-142` -- reads sequence, allocates positions, stages event/metadata with a null ETag, but does not commit.
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:607-712` -- stages events, snapshot, publication index and checkpoint; validates the execution fence and commits once. Catch `:686` can receive fence-validation or `SaveStateAsync` `InvalidOperationException`; only this catch retries. Catches `:842,2624,2971,3048` are separate commit paths.
- `src/Hexalith.EventStore.Server/Configuration/CommandConcurrencyOptions.cs:10-15` and `Events/AggregateMetadata.cs:8-9` -- one additional retry by default; ETag is present but production never reads it.
- `src/Hexalith.EventStore.Testing.Integration/Benchmarking/BenchmarkDatasetBuilder.cs:795-837,957-992` -- reusable actor-state transaction shape. It proves same-key access, not that the endpoint bypasses an in-flight actor turn.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs:247-274,377-455,941-953` -- health/reset/domain setup plus authoritative Redis actor-state readers and composite namespace.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Events/EventPersistenceIntegrationTests.cs:160-208` -- serialized control case and persisted per-sequence assertions.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/ActorConcurrencyConflictTests.cs:117-203` -- vacuous fallback and generic-state (not actor-state) ETag probe to repair.
- `.github/workflows/integration.yml:28,73-75`, `.github/workflows/ci.yml:23-40`, `.github/workflows/release.yml:79-87`, `docs/ci.md:50-62` -- dedicated LiveSidecar lane remains outside release.
- `_bmad-output/planning-artifacts/architecture.md:106-116,150-154,550-560` -- actor-only durable mutation invariants, persisted-state evidence rule, and Deferred table format.

## Tasks & Acceptance

**Execution:**

- [x] `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs` plus focused fixture types -- add a test-only post-metadata-read gate with per-session allocation/retry telemetry; normal tests remain pass-through and collection isolation prevents an unrelated allocation consuming the gate.
- [x] `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/AppendDurabilityRaceLiveSidecarTests.cs` -- use distinct contender identities and bounded tasks; after a successful raw response, read and assert the raw contender from Redis while the actor remains gated, then release/fully quiesce both writers and read final Redis state. Capture completion timestamps at task completion. Accept and consistently classify rejection, no-writer, overwrite, or retry/serialization to sequence 2; never hard-code the hoped-for one-event outcome.
- [x] `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/ActorConcurrencyConflictTests.cs` -- replace the vacuous actor-key probe with a generic-state control that proves its ETag became stale, requires HTTP 409 plus DAPR's ETag-mismatch error surface, and verifies the first update remains persisted; update class documentation so it is not presented as actor-state evidence.
- [x] `_bmad-output/implementation-artifacts/evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/` -- commit capture, commands, environment, source state, per-invariant mutation receipts, and raw test output redacted only for machine name and absolute workspace path; document redaction and hash every other file.
- [x] `_bmad-output/implementation-artifacts/4-5-append-durability-race-evidence.md` -- classify all five catches and the retry budget from named evidence. Call token `0` caller-supplied/unverified when actor state exposes no ETag; call a value stale only when a prior read and intervening successful write prove it. Claim an overwritten durable write only when the gated Redis read proves it existed.
- [x] `_bmad-output/planning-artifacts/architecture.md`, `docs/concepts/event-envelope.md`, `docs/concepts/architecture-overview.md`, `docs/reference/problems/concurrency-conflict.md`, and `docs/ci.md` -- record the Deferred add-fencing decision, distinguish desired actor-only/write-once invariants from missing storage enforcement, correct only evidence-disproved claims, and retain lane boundaries.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml:231` -- set `in-progress` during execution and `review` only after all gates pass.

**Acceptance Criteria:**

- Given the gated two-writer run, when the raw request succeeds before gate release, then capture includes a Redis read proving the raw contender is durable before the actor resumes; after both writers quiesce, final Redis readback identifies the surviving events/metadata, timing, retry count, surfaced errors, and any lost write.
- Given any supported observed outcome (writer rejection, neither writer durable, same-key overwrite, or conflict retry/serialization producing sequence 2), when the race test evaluates it, then the test validates internal consistency and records the outcome instead of failing because it differs from the expected exposure.
- Given every HTTP/probe task, when its bound expires, then underlying work is cancelled and awaited before Redis readback; token `0` is never described as stale without a previously observed current token.
- Given the generic-state control, when the first conditional update advances the ETag and the original ETag is replayed, then the response is HTTP 409 with DAPR ETag-mismatch semantics and Redis retains the first update.
- Given the report and architecture row, when read without source inference, then all five catches plus the retry budget have evidence-linked classifications and the add/change/defer fencing decision is explicit.
- Given the diff, when scope is inspected, then no `src/` fencing/ETag/retry behavior, release-lane workflow, or category filter changed.
- Given each named material invariant (gate timing, intermediate raw durability, final writer/sequence/metadata consistency, conflict/retry classification, key addressability, exact generic 409/error semantics, and retained generic value), when deliberately inverted one at a time, then the named test fails and a separate receipt proves attribution.
- Given the evidence directory, when `sha256sum -c evidence-sha256.txt` runs, then every listed file is OK and the manifest excludes only itself.

## Spec Change Log

- **Review loop 1 (2026-08-08, `bad_spec`):** Review showed the first derivation could claim a durable lost write from HTTP 204 without reading Redis while the actor was held, rejected valid retry/serialization or no-writer outcomes by requiring sequence 1, inferred catch/retry reachability without telemetry, called an unverified token `0` stale, accepted unrelated generic-state failures, mutation-checked only aggregate assertions, and retained machine/workspace identifiers in evidence. The tasks and acceptance criteria now require intermediate and final persisted-state proof, outcome-neutral consistency checks, bounded/quiesced work, exact generic ETag semantics, per-session retry telemetry, per-invariant mutation receipts, and documented redaction. **KEEP:** the post-metadata-read test-only gate; distinct actor/raw identities; Redis-authoritative capture; generic-state control; hash-bound evidence/report structure; evidence-bounded documentation; Deferred add-fencing decision; four unexercised catches classified `inconclusive`; and zero production/workflow behavior changes.

## Design Notes

The raw actor-state endpoint is an adversarial probe, not a supported producer. A late test-only gate makes overlap observable, while an intermediate Redis read distinguishes an acknowledged durable raw write from an HTTP-only claim. Final validation is an outcome consistency model: sequence 2 is valid evidence of retry/serialization, absence is valid if both writers reject, and overwrite is reported only when the intermediate and final identities differ. Per-session allocation counts inform retry classification but do not prove a catch ran unless the capture exposes it. Capture output is deterministic JSON with no secrets; an explicit evidence-directory setting writes the redacted committed run while ordinary runs use test results.

## Verification

**Commands:**

- `dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false` -- expected: 0 warnings and errors.
- `dapr init` then run the LiveSidecar project in Release package mode -- expected: all pass; capture exact counts and DAPR CLI/runtime, SDK and Redis-image pins.
- Invoke each focused xUnit v3 test assembly with `-method` once per named invariant mutation -- expected: every separate mutation receipt identifies one failing test, then the restored run passes.
- `sha256sum -c evidence-sha256.txt` in the evidence directory -- expected: every file OK.
- `python3 scripts/check-deferred-work.py` -- expected: exit 0 if any deferral is appended.

**Manual checks (if no CLI):**

- Confirm no behavior change under `src/`, no release workflow change, no vacuous return, and no unredacted secret in evidence.

## Suggested Review Order

**Finding and decision**

- Start with the provider-qualified decision and observed silent overwrite.
  [`4-5-append-durability-race-evidence.md:3`](4-5-append-durability-race-evidence.md#L3)

- Review the evidence-bounded catch and retry classifications.
  [`4-5-append-durability-race-evidence.md:35`](4-5-append-durability-race-evidence.md#L35)

**Deterministic race harness**

- Follow the gated two-writer orchestration and final consistency model.
  [`AppendDurabilityRaceLiveSidecarTests.cs:62`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/AppendDurabilityRaceLiveSidecarTests.cs#L62)

- Inspect narrow handler arming and exactly-once allocation interception.
  [`AppendDurabilityRaceSession.cs:89`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/AppendDurabilityRaceSession.cs#L89)

- Confirm the gate decorates the production allocator implementation.
  [`LiveSidecarGlobalPositionAllocator.cs:10`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/LiveSidecarGlobalPositionAllocator.cs#L10)

- Review outcome classification across rejection, loss, retry, and infrastructure branches.
  [`AppendDurabilityRaceClassifier.cs:45`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/AppendDurabilityRaceClassifier.cs#L45)

**Generic ETag control**

- Verify the non-vacuous stale-ETag sequence and complete Redis readback.
  [`ActorConcurrencyConflictTests.cs:130`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/ActorConcurrencyConflictTests.cs#L130)

- Inspect non-throwing capture of malformed Dapr error responses.
  [`DaprStateErrorParser.cs:6`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprStateErrorParser.cs#L6)

**Evidence and reproducibility**

- Reproduce positive, mutation, redaction, and integrity gates safely.
  [`commands.md:34`](evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/commands.md#L34)

- Audit the fail-closed semantic and source-binding validator.
  [`validate-evidence.py:74`](evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/validate-evidence.py#L74)

**Architecture and documentation**

- Review the provider-portable fencing decision recorded as deferred.
  [`architecture.md:558`](../planning-artifacts/architecture.md#L558)

- Confirm public architecture language distinguishes ownership from storage fencing.
  [`architecture-overview.md:130`](../../docs/concepts/architecture-overview.md#L130)

**Supporting verification**

- Inspect deterministic branch coverage for the outcome classifier.
  [`AppendDurabilityRaceClassifierTests.cs:40`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/AppendDurabilityRaceClassifierTests.cs#L40)

- Confirm test-only dependency registration leaves production sources untouched.
  [`DaprTestContainerFixture.cs:797`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs#L797)
