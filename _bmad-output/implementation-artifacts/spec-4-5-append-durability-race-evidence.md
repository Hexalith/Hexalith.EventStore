---
title: 'Story 4.5: Append Durability Race Evidence'
type: 'chore'
created: '2026-08-08'
status: 'in-progress'
review_loop_iteration: 3
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

### Review Findings

Code review 2026-08-11 (four layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor) against `0776785f..2321205b`, chunk 1 of 2 (harness + spec/docs + human-authored evidence layer; the 15 raw JSON captures are chunk 2).

**Standing constraint on every item below:** `source-state.md` binds SHA-256 hashes for all 10 LiveSidecar sources, this spec, the story report, `architecture.md`, and 4 `docs/` pages, and `validate_source_binding` hashes the **worktree**. Any edit to those files invalidates the sealed packet and requires a fresh live capture plus regenerated `source-state.md` and `evidence-sha256.txt`. The packet already fails at `HEAD` (see the first Defer item), so re-sealing is owed regardless.

- [x] [Review][Decision] Outcome-prescriptive assertions sit inside a required PR gate — `keyAddressabilityProven` requires `genericActorKeyStatusCode == HttpStatusCode.NoContent` (`AppendDurabilityRaceLiveSidecarTests.cs:295`) and `finalShapeConsistent` requires `finalMetadata.ETag is null` (`:317`). The frozen I/O matrix says the key-addressability case must *record* which namespace is readable, and AC2 says record rather than fail. `live-sidecar` is a **required status check on `main`** (verified via `rules/branches/main`) running on `DAPR_RUNTIME_VERSION: '1.18.2'` (`integration.yml:25`) while the capture is attributed to `1.18.1`. A provider or runtime that exposes the actor key generically, or that populates an ETag, red-gates the repository instead of producing the new observation this spike exists to collect.
- [x] [Review][Decision] Mutation receipts prove the predicate was true, not that any conjunct is load-bearing — `AssertInvariant` negates the already-computed boolean (`AppendDurabilityRaceLiveSidecarTests.cs:616-623`, `ActorConcurrencyConflictTests.cs:304-311`). `gateTimingProven` is a ~15-way conjunction whose timestamp chain `rawCompletedAtUtc <= genericActorKeyCompletedAtUtc <= intermediateReadAtUtc <= ReleasedAtUtc` is stamped in sequential program order on the test thread and therefore holds regardless of sidecar behavior; `retryClassificationConsistent`'s sequence-2 clause short-circuits `true` at the captured `finalSequence == 1`. Replacing the whole timestamp chain with `true` would still yield an identical `mutation-gate-timing.json`. AC7's letter is met; its purpose — attribution — is not. Option: keep the env-var switch but have it perturb a harness *input* (release the gate early, skip the pre-release Redis read) rather than the assertion's polarity. Also note `infrastructureFree.ShouldBeTrue` (`:504`) carries no mutation name at all.
- [x] [Review][Decision] Provider attribution is a source literal validated against itself — `daprRuntime = "1.18.1"`, `redisImage = "redis:6"`, and `baselineCommit` are C# string literals (`AppendDurabilityRaceLiveSidecarTests.cs:361-366`), and `validate-evidence.py:78,80` asserts those same literals. The fixture never reads the sidecar's runtime version or the Redis image. The only genuinely observed provider fact is `stateStoreComponentYaml` and its SHA-256. The decision's load-bearing qualifier ("this one provider profile does not characterize any other state-store provider") is therefore attributed to a runtime/image pair nothing observed, and a re-capture on another runtime would be mislabelled as the reviewed profile.
- [x] [Review][Decision] Unguarded JSON parse can destroy the generic-ETag evidence file — the `try`/`catch` at `ActorConcurrencyConflictTests.cs:176-184` wraps only `GetGenericStateJsonAsync`, but `JsonNode.Parse(retainedRedisJson)` (`:190-192`) and `JsonSerializer.Deserialize<JsonElement>(retainedRedisJson)` (`:221-223`) run outside it. A malformed or non-JSON Redis body throws before `WriteEvidenceAsync` at `:228`, so `generic-etag-control.json` is never written — defeating the `retainedReadExceptionType`/`retainedReadExceptionMessage` fields that exist precisely to capture that failure, and contradicting the non-throwing capture discipline `DaprStateErrorParser` establishes for the sibling path.
- [x] [Review][Decision] Four classifier outcomes are produced by no test case, and one is unreachable — the 19 `TheoryData` rows in `AppendDurabilityRaceClassifierTests.cs:18-36` yield 17 of the classifier's 21 distinct names. Uncovered: `inconsistent-single-sequence-has-two-writers` (`AppendDurabilityRaceClassifier.cs:110`), `actor-writer-rejected` (`:130`), `raw-writer-survived` (`:130`). `actor-writer-survived` (`:149`) is **dead code**: reaching it requires `!rawSucceeded`, and the `rawInfrastructureFailure` gate at `:52-55` has already returned unless `rawSucceeded || rawConflictRejected`, so `rawConflictRejected` is always true and the ternary always yields `raw-writer-conflict-rejected`. The class docstring claims "Verifies every classifier branch is stable". Scope note: `inconsistent-single-sequence-has-two-writers` cannot be produced by the live harness (`finalEvents` holds at most one element at `finalSequence == 1`, and the two contender predicates are mutually exclusive), so this is pure-function coverage, not a live hole.
- [x] [Review][Decision] Two deterministic classes violate the frozen test-placement constraint — `AppendDurabilityRaceClassifierTests.cs` and `DaprStateErrorParserTests.cs` carry neither `[Collection("DaprTestContainer")]` nor `[Trait("Category", "LiveSidecar")]`, the only two classes in the project without them. Correction to the reviewing layers: these 25 assertions are **not** unrun — `integration.yml:78` invokes the project with no category filter and `live-sidecar` is a required PR check. The real consequences are that 25 pure-unit tests need Docker + Dapr to execute, they are absent from `ci.yml` `unit-test-projects`, and without the collection attribute they may run in parallel with the timing-sensitive race test.

- [x] [Review][Defer] The sealed packet's fail-closed validator no longer passes at `HEAD` — `validate_source_binding` hashes worktree files; `docs/ci.md` (drifted by `e489f58e`, `35a1eecd`, `b927472a`, `ab1666dd`, `fe715c70`) and `docs/concepts/architecture-overview.md` (`b927472a`, `f19f6d1e`) no longer match. Verified: `python3 validate-evidence.py` exits 1 with `AssertionError: docs/ci.md` at `52200827`; all 17 rows were OK at `2321205b`, and `sha256sum -c evidence-sha256.txt` is still 22/22 OK. Already recorded at `deferred-work.md:1058` — deferred, pre-existing, but now actively failing.
- [x] [Review][Defer] No test or CI step ever executes `validate-evidence.py` — every sibling committed-evidence directory is pinned by a blocking test in `tests/Hexalith.EventStore.Contracts.Tests/Packaging/` (`Oq8PlatformClosureTests.cs`, `DeployedRuntimeParityClosureTests.cs`) and `tools/validate-oq8-platform-evidence.py` is invoked from `integration.yml`. Story 4.5's validator is operator-discipline only, so the hash binding can decouple silently with every required check green. Blocked on the item above. — deferred, pre-existing
- [x] [Review][Defer] Nothing binds a committed capture to the receipt of the run that produced it — `append-durability-race.json` records `armedAtUtc = 2026-08-08T08:52:16.999Z`, inside the **post-mutation** window (`post-mutation-focused-test-results.json`: 08:52:16.855–08:52:17.892), not the `race-test-results.json` window (08:50:31.706–08:50:32.602). `commands.md:71-72` re-runs the full suite with `HEXALITH_STORY_4_5_EVIDENCE_DIR` still set, then `:112-114` re-runs the restored focused pair, each rewriting the capture. `verification-summary.md:17` does disclose "the final restored race capture", so this is a traceability gap rather than a misstatement; a run id in both capture and receipt would close it. — deferred, pre-existing
- [x] [Review][Defer] `retryCount` counts unfiltered allocations from a control shared across both hosts — `retryCount = AllocationAttempts - 1` (`AppendDurabilityRaceLiveSidecarTests.cs:325`), and `InterceptAllocationAsync` (`AppendDurabilityRaceSession.cs:148-159`) increments for every allocation while armed with no aggregate filter, while `AppendDurabilityRaceControl` is registered as a singleton into both the primary (`DaprTestContainerFixture.cs:797-798`) and replica (`:952-953`) hosts. Mitigated by serial `[Collection("DaprTestContainer")]` execution and disclosed in `providerProfile.allocatorIdentityLimitation`, but the report states `retryCount = 0` as a bare fact. — deferred, pre-existing
- [x] [Review][Defer] `MetadataKey_StaleEtagUpdate_IsRejected` no longer touches a metadata key — it now keys on `story-4-5-generic-etag-{Guid:N}` (`ActorConcurrencyConflictTests.cs:133`). The class docstring was corrected, but the method name and the class name `ActorConcurrencyConflictTests` still read as actor-state evidence, which is the misreading the story set out to remove. Renaming has extra blast radius: `commands.md:97`, the `MUTATIONS` map in `validate-evidence.py:21-22`, and the committed receipts all key on the current name. — deferred, pre-existing
- [x] [Review][Defer] The redaction gate passes when `rg` errors — `commands.md:135-136` uses `! rg …`, so exit code 2 (bad glob, missing PCRE2, unreadable file) inverts to success and the scan reports clean having scanned nothing. Needs an explicit `case $?` on 0/1/other. — deferred, pre-existing
- [x] [Review][Defer] `commands.md` mutates the operator's shell — `set -o pipefail` (`:39`) and `set -e` inside `story_4_5_expect_mutation_failure` (`:89`) are never restored, so errexit leaks into every later block; the canonical-overwrite guard uses `exit 2` (`:18`), which closes an interactive shell at the repo root as the doc instructs; and the redact/hash block (`:120-137`) depends on `$story_4_5_workspace` / `$story_4_5_evidence` with no `: "${var:?}"` guard, so run standalone it issues `sed -i` with an empty pattern across every JSON in the directory. — deferred, pre-existing
- [x] [Review][Defer] `docs/reference/problems/concurrency-conflict.md:14` lists as a Common Cause "A backend or actor-state provider rejected an optimistic state transaction and surfaced it as `InvalidOperationException`" — but this spec's own Problem statement records that nothing supplies an etag or concurrency option on the actor-state commit path, so that cause cannot arise there. Narrow finding only: the broader reviewer claim that the page presents unproven behavior as fact is over-read — it explicitly hedges ("this is configured code, not proof that the active provider surfaces a retryable exception"), carries the silent-overwrite caveat, and retains full "How to Fix" guidance. Editing this file would newly break a currently-passing `source-state.md` row. — deferred, pre-existing
- [x] [Review][Defer] The ADD-fencing decision has no tracked owner or trigger — `architecture.md:558` commits to "a separately approved implementation story", but no append-fencing story exists in `epics.md` or `sprint-status.yaml` (`grep -i fenc` finds only Story 4.11's admission fence). The `check-deferred-work.py` gate cited in Verification passes vacuously because nothing was appended for this decision. — deferred, pre-existing

#### Resolution (2026-08-11)

Owner decision: fix the correctness and high-severity items and re-seal; defer D3 and D6 with the reason *"deferred to the approved append-fencing follow-up, which must re-capture across multiple provider profiles anyway — fixing runtime attribution and test placement is cheapest as part of that multi-profile capture."*

**Applied (code complete, verified as far as this environment allows):**

- D1 — `keyAddressabilityProven` now derives a `keyAddressabilityClassification` (`actor-key-absent-from-generic-namespace` / `actor-key-readable-through-generic-namespace` / `generic-probe-failed` / `generic-probe-not-attempted` / `generic-probe-unrecognized-response`) and requires only that the probe was classifiable and that *some* namespace was readable — no vacuous pass, no prescribed answer. The `finalMetadata.ETag is null` conjunct was removed from `finalShapeConsistent` and is now recorded as `final.metadataEtagPresent`. Both observed values are pinned in `validate-evidence.py` instead, so the reviewed capture stays fixed while the test stops red-gating other providers.
- D2 — `AssertInvariant` no longer inverts the computed boolean; it asserts plainly and tags every failure `[invariant:<name>]`. All seven mutations now perturb a harness **input**: release the gate before the writers (`gate-timing`), skip the gated readback (`intermediate-raw-durability`), skip the namespace probe (`key-addressability`), match the actor contender against an identity no event carries (`final-state-consistency`), classify against a sequence the final state does not exhibit (`conflict-retry-classification`), replay the still-current token (`generic-409-semantics`), and overwrite the retained value after the rejection (`retained-generic-value`). `validate-evidence.py` now asserts each receipt's failure text carries its expected `[invariant:…]` tag, so a receipt can no longer be satisfied by any single-test failure that happens to carry the right filename.
- D4 — the retained-value `JsonNode.Parse` / `Deserialize` moved inside the existing guard; a malformed body is now captured as `retainedReadExceptionType` and the raw body is retained as `redisRetainedRawJson`. Generic-control schema bumped to 2.
- D5 — the dead `actor-writer-survived` ternary arm removed (its guard makes `rawConflictRejected` always true), and three uncovered outcomes added to the case table: `inconsistent-single-sequence-has-two-writers`, `actor-writer-rejected`, `raw-writer-survived`. All twenty reachable names are now covered and the docstring says so.

**Verified here:** Release build of the LiveSidecar project is clean (0 warnings, 0 errors); the deterministic branch matrix passes **28/28** (was 25), confirming the three new classifier cases and the dead-branch removal.

**BLOCKED — the re-seal was not completed. The evidence packet is now in a partially-updated state and MUST NOT be treated as valid.** `validate-evidence.py` currently fails at `classifier-parser-test-results.json: expected (28, 28, 0), got (25, 25, 0)`, because the committed receipts predate these changes. Two independent blockers stopped the live capture on this machine:

1. **Port layout mismatch.** `DaprTestContainerFixture.cs:47-48` probes `localhost:50005/50006` on non-Windows, but this machine's `dapr init` (CLI 1.18.0) publishes placement on **6050** and scheduler on **6060**, so the fixture aborts in `InitializeAsync` with "Dapr infrastructure pre-flight check failed". Fixing this means either re-running `dapr init` (destroys the running control plane and its Redis data), installing a port forwarder, or editing the hardcoded ports — none of which this review authorized.
2. **Runtime attribution would be wrong anyway.** The local control plane containers are `daprio/dapr:1.18.2` while `dapr --version` reports runtime 1.18.1 and the packet's profile claims `1.18.1`. Because D3 (hardcoded provider profile) was deferred, any capture taken here would be silently labelled `1.18.1` — reproducing exactly the defect D3 describes.

**To finish:** run the capture on a host whose `dapr init` matches the fixture's expected ports and whose runtime genuinely is the profile being claimed, following `commands.md` end to end (Release solution build → classifier/parser → focused race + generic-ETag → full suite → 7 mutations → restored focused rerun → redact/hash → `validate-evidence.py` → `sha256sum -c`). Then regenerate `source-state.md` (its `docs/ci.md` and `docs/concepts/architecture-overview.md` rows are already stale at `HEAD`) and `evidence-sha256.txt`. Expected receipt counts after these changes: classifier/parser **28**, full suite **78** — both already updated in the validator.

**Refuted during verification** (raised by a layer, checked against source, and dismissed): "retry count 0 does not prove the budget was not entered" — the `RetryAfterPersistenceConflict:` label is at `AggregateActor.cs:530`, *before* rehydration and before `PersistEventsAsync`, so a genuine retry re-enters `AllocateAsync` and would increment `AllocationAttempts`; the report's inference is sound. "The new tests never run in CI" — `live-sidecar` is a required check on `main`. "`status: 'done'` contradicts `sprint-status: review`" — repo convention, matching spec-4-3 and spec-4-4. "The page asserts unproven behavior as fact" — explicitly hedged (see above). "No deferred-work entry exists for Story 4.5" — five entries exist at `deferred-work.md:1058,1102,1106,1110,1114`, filed by `5bcfdbc8`. "The canonical-overwrite guard runs `realpath` on a missing directory" — `mkdir -p` precedes it at `commands.md:14`. "The redaction regex misses compact JSON" — the `jq` pass at `commands.md:123-127` normalizes every JSON file first.

**Independently re-verified as passing:** `git diff 0776785f..2321205b -- src/ .github/` is empty (AC6); `sha256sum -c evidence-sha256.txt` is 22/22 OK with the manifest excluding only itself (AC8); all 7 mutation receipts are 1 test / 0 passed / 1 failed on the named method (AC7 letter); `LiveSidecarGlobalPositionAllocator` genuinely constructs and delegates to `DaprGlobalPositionAllocator`, and production's `TryAddSingleton` cannot displace it.

### Review Findings — loop 2

Code review 2026-08-11 (four layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor) against the **uncommitted working tree** at `52200827` — the D1/D2/D4/D5 resolution patches plus the chunk-2 raw captures deferred by loop 1.

**Verified as claimed:** Release build of the LiveSidecar project is 0 warnings / 0 errors; the deterministic matrix passes 28/28; the classifier emits exactly 20 reachable names and the 22-row table covers all 20; the removed `actor-writer-survived` arm was genuinely dead; AC6 holds (no `src/` or `.github/` change in the story range or the working tree); the redaction scan is clean; `check-deferred-work.py` exits 0. Each of the seven perturbations was traced and does falsify its own invariant ahead of the fixed assertion order.

**Correction to loop 1's standing constraint:** the loop-2 edits invalidate **5 further `source-state.md` rows** (the 4 edited test sources plus this spec), taking the binding from 2/17 failing to **7/17**. `sha256sum -c evidence-sha256.txt` is now **21/22** — `validate-evidence.py` no longer matches its own manifest row.

- [x] [Review][Decision] Mutation attribution still rests on unenforced assertion order, and the conjuncts loop 1 flagged as true-by-construction remain so inside their own mutation run — `retryClassificationConsistent`'s sequence-2 clause reads `finalSequence`, not the perturbed `classifierSequence` (`AppendDurabilityRaceLiveSidecarTests.cs:395-397`), so replacing it with `true` leaves the positive capture and `mutation-conflict-retry-classification.json` byte-identical; the same holds for the mid-chain timestamp comparisons `ArmedAtUtc <= FirstAllocationEnteredAtUtc <= rawCompletedAtUtc <= genericActorKeyCompletedAtUtc <= intermediateReadAtUtc` (`:301-304`), which are stamped in sequential program order on the test thread and are falsified by no perturbation — only the tail `intermediateReadAtUtc <= ReleasedAtUtc` and the two `actorTaskIncomplete*` flags are. Separately, several perturbations break more than one invariant (`generic-409-semantics` also destroys the retained value; `intermediate-raw-durability` also flips the classifier to `inconsistent-raw-acknowledgement-not-proven-durable`), so each receipt carries the right tag only because of the assertion order at `:536-556` and `ActorConcurrencyConflictTests.cs:252-259`, which nothing pins. D2 is therefore established at the level of "at least one conjunct per invariant", not of the conjunction. Options: accept and document the order-dependence; or split the conjunctions into individually-perturbable invariants (larger re-capture).
- [x] [Review][Decision] `finalShapeConsistent` remains outcome-prescriptive where D1 stopped — only the `finalMetadata.ETag is null` conjunct was demoted to "recorded". `finalEvents.Count == finalSequence`, the contiguous `1..n` sequence check, and `finalMetadata.LastModified == finalEvents[^1].Timestamp` (`AppendDurabilityRaceLiveSidecarTests.cs:347-359`) still hard-fail. A torn interleaving in which one writer's metadata survives alongside the other writer's event — a durability anomaly this spike exists to catch — red-gates the required `live-sidecar` check instead of being recorded. Same trade-off the owner already decided once for D1; needs the same call here.
- [x] [Review][Decision] D1 moved two load-bearing provider facts behind a validator that no CI step executes — `keyAddressability.classification` and `final.metadataEtagPresent` are now pinned only at `validate-evidence.py:138-140`. Confirmed: nothing in `.github/`, `scripts/`, or `tools/` invokes this validator, `HEXALITH_STORY_4_5_EVIDENCE_DIR` is set nowhere in CI, and `WriteEvidenceAsync` returns early without it — so the CI run produces no capture to check. The sibling Story 4.14 packet *is* wired (`integration.yml:118`). If a runtime change makes the composite actor key readable generically or populates `AggregateMetadata.ETag`, both classifications stay "recognized", the test passes, `integration.yml` goes green, and the report and architecture decision keep stating the old facts as observed. Loop 1 deferred "no CI step executes the validator" as pre-existing; D1 raised its consequence.
- [x] [Review][Decision] The port blocker's remedy is a one-line test-only change, and the recorded framing overstates it — confirmed on this host: 50005/50006 are closed, 6050/6060 are open, `dapr_placement` publishes `0.0.0.0:6050->50005/tcp`. The fixture's `PlacementPort = OperatingSystem.IsWindows() ? 6050 : 50005` (`DaprTestContainerFixture.cs:47-48`) already contains the right host ports behind a now-stale OS predicate. The Resolution and the deferred-work entry state the only options are re-running `dapr init`, a port forwarder, or "editing the hardcoded ports — none of which this review authorized"; probing both candidates, or correcting the predicate, is inside test scope and would unblock the re-capture. It does touch a hash-bound source, which the re-seal already owes.
- [x] [Review][Decision] D6 defers a violation of a *frozen* `Always` constraint rather than renegotiating it — the `<frozen-after-approval>` block requires every new test to carry `[Collection("DaprTestContainer")]` + `[Trait("Category", "LiveSidecar")]`; `AppendDurabilityRaceClassifierTests` and `DaprStateErrorParserTests` still carry neither. The exception lives only in `deferred-work.md` and the mutable Resolution block while the frozen text — "do not modify unless human renegotiates" — still states the rule unconditionally.

- [ ] [Review][Patch] Resolution asserts AC8 passes in the present tense while this diff breaks it — `sha256sum -c` is 21/22 at the reviewed tree [`spec-4-5-append-durability-race-evidence.md:174`]
- [ ] [Review][Patch] BLOCKED note names 1 of 13 broken validator gates; the other 12 are 7 missing `[invariant:]` tags, 2 schema versions, and 3 absent fields [`spec-4-5-append-durability-race-evidence.md:165`]
- [ ] [Review][Patch] `commands.md` documents a mutation model the code no longer uses ("does not alter ... persisted inputs") and is omitted from the re-seal list [`evidence/story-4-5/0776785f.../commands.md:77`]
- [ ] [Review][Patch] `verification-summary.md` hard-codes `25 passed`, `75 passed`, and "All 22 listed files OK"; omitted from the re-seal list [`evidence/story-4-5/0776785f.../verification-summary.md:7`]
- [ ] [Review][Patch] key-addressability failure message still prescribes the outcome the predicate stopped requiring, and that string lands in the evidence receipt [`AppendDurabilityRaceLiveSidecarTests.cs:547`]
- [ ] [Review][Patch] `infrastructureFree` still carries no `[invariant:]` tag and no perturbation — the loop-1 sub-item was silently dropped, not deferred [`AppendDurabilityRaceLiveSidecarTests.cs:556`]
- [ ] [Review][Patch] Mutation receipts do not record which perturbation was armed, so an environmental flake yields a receipt byte-equivalent in every field the validator inspects [`validate-evidence.py:71`]
- [ ] [Review][Patch] Validator does not pin the invariant key set; deleting an invariant from the emitted object still validates [`validate-evidence.py:142`]
- [ ] [Review][Patch] A probe transport failure is attributed to `key-addressability` rather than infrastructure — `infrastructureFree` shares the `genericActorKeyException is null` conjunct but is asserted 3 positions later [`AppendDurabilityRaceLiveSidecarTests.cs:544`]
- [ ] [Review][Patch] D4 residual — `JsonDocument.Parse(originalJson/currentJson)` and the two `Deserialize` calls in the evidence literal still run unguarded on Dapr-sourced bodies before `WriteEvidenceAsync` [`ActorConcurrencyConflictTests.cs:148`]
- [ ] [Review][Patch] Untagged `overwrite.StatusCode.ShouldBe(NoContent)` inside the `retained-generic-value` branch misattributes an infrastructure failure [`ActorConcurrencyConflictTests.cs:185`]
- [ ] [Review][Patch] `metadataEtagPresent` is two-state for three states — false both when the ETag is absent and when metadata itself is absent [`AppendDurabilityRaceLiveSidecarTests.cs:363`]
- [ ] [Review][Patch] `redisRetainedRawJson` is asserted by nothing in `validate_generic_control`, and an unrecognized `HEXALITH_STORY_4_5_MUTATION` value is silently accepted [`validate-evidence.py:151`]
- [ ] [Review][Patch] Governance metadata contradicts the BLOCKED state — `review_loop_iteration` still `1` (siblings increment: 4-3 = 3, 4-4 = 2), `status: 'done'` retained, and D1/D2/D4/D5 still unchecked `- [ ]` [`spec-4-5-append-durability-race-evidence.md:5`]
- [ ] [Review][Patch] Story report carries no BLOCKED disclosure and states "`evidence-sha256.txt` binds the set" in the present tense [`4-5-append-durability-race-evidence.md:7`]
- [ ] [Review][Patch] Deferred-work entry cites `AppendDurabilityRaceLiveSidecarTests.cs:364` (`finalMetadataEtagPresent`); the provider literals it describes are at `:414,416` [`deferred-work.md:1198`]

- [x] [Review][Defer] Classifier completeness lives only in a docstring — the "all twenty reachable names" claim is true today, but a 21st branch would fail no test [`AppendDurabilityRaceClassifierTests.cs:44`] — deferred, pre-existing
- [x] [Review][Defer] The dead-arm removal substitutes the literal `true` for `rawConflictRejected` in the `RecognizedRejectionOrConflict` position; widening the infrastructure gate would silently mislabel a new status [`AppendDurabilityRaceClassifier.cs:151`] — deferred, pre-existing
- [x] [Review][Defer] No schema history documents the `append-durability-race.json` 2→3 or `generic-etag-control.json` 1→2 bumps that the validator now hard-asserts [`validate-evidence.py:92`] — deferred, pre-existing
- [x] [Review][Defer] `generic-probe-not-attempted` encodes harness state as a provider observation and can appear in a non-mutation capture when `gateWaitException` short-circuits the probe block [`AppendDurabilityRaceLiveSidecarTests.cs:320`] — deferred, pre-existing

**Refuted during verification** (raised by a layer, checked against source, dismissed): "A mutation run overwrites the canonical capture with fiction" — **false**; `WriteEvidenceAsync` returns early whenever `HEXALITH_STORY_4_5_MUTATION` is non-empty (`AppendDurabilityRaceLiveSidecarTests.cs:697-702`, `ActorConcurrencyConflictTests.cs:353-358`), so no mutation run writes any capture, and the acceptance-auditor argument built on it ("D2 increases the severity of the capture-to-receipt deferral") does not follow. "`retryClassificationConsistent` requiring `IsInternallyConsistent` violates the record-don't-fail charter" — over-read; AC2 requires the test to *validate internal consistency*, and all four supported outcomes map to consistent classifications. "`redisRetainedRawJson` contradicts `redaction.md`" — over-read; the redaction claim concerns the redaction pass, and the field carries test-controlled data. "The 11 new deferred-work entries are ungoverned" — that is the repo's existing legacy-advisory convention (332 unclassified entries, gate exits 0). "Chunk 2 was never dispositioned" — chunk 2 was in scope for this review.

### Review Findings — loop 3

Code review 2026-08-25 (four layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor) against `0776785f..HEAD` restricted to **chunk C** — the 15 committed capture artifacts. All four layers ran; none failed.

**Marginal yield is deliberately small.** Loop 2 already reviewed chunk C at the then-uncommitted tree, so most of what the layers surfaced this round is an already-open loop-2 item, re-confirmed but not re-raised: the broken validator gates and receipt counts, the untagged `infrastructureFree`, the unrecorded perturbation name, outcome-prescriptive assertions, `retryClassificationConsistent` vacuity, the un-run validator, and loop 1's D3/D6 deferrals. Only findings that are new or whose evidence is materially stronger are listed below.

**Re-derived by execution at HEAD** (not inferred): `python3 validate-evidence.py` exits 1 with `classifier-parser-test-results.json: expected (28, 28, 0), got (25, 25, 0)`; `sha256sum -c evidence-sha256.txt` exits 1 at 21/22 (`validate-evidence.py: FAILED`); the deterministic matrix re-run at HEAD is **28/28**, confirming the validator is right and the capture is stale; `live-sidecar-test-results.json` is 75 against a required 78; the committed race capture is `schemaVersion 2` against a required 3 and the control is 1 against a required 2, while the HEAD harness emits 3 and 2 respectively.

**Strengthened evidence for the open loop-2 D2 decision** (no new item): the receipts prove the point themselves rather than by source inference. Every mutation receipt's own embedded capture in `extra.output` reports **all six invariants `true`** — including `"keyAddressabilityProven": true` inside `mutation-key-addressability.json` — with the identical `same-key-overwrite-raw-durable-write-lost` classification as the clean run, and every failure message is the superseded `mutate ? !satisfied : satisfied` form. As committed, the mutated and unmutated runs are indistinguishable in observed behaviour.

**Correction retained from loop 2:** `WriteEvidenceAsync:697-700` returns early whenever `HEXALITH_STORY_4_5_MUTATION` is non-empty, so no mutation run writes a capture. The committed captures are clean-run data; the finding below is traceability, not fabrication.

- [ ] [Review][Patch] Capture-to-receipt binding is now provable at the identity level, and the validator can close it mechanically — loop 1 deferred this as a timestamp-only "traceability gap". It is stronger than that: committed `generic-etag-control.json` keys on `story-4-5-generic-etag-bc9cedea29ec42bdb171cad9dbfaaff8` while its named receipt `generic-etag-test-results.json` records `...4595aaeae49e473bbb01a0800245ae48`, and committed `append-durability-race.json` carries `sessionId 01KZG95BBK9G9M0Q859KR65N4T` while every session in `race-test-results.json` is `01KZG924...`. Both committed captures came from the post-mutation restored run recorded in `post-mutation-focused-test-results.json`. Each CTRF receipt already embeds the full capture in `extra.output`; the validator reads only `summary` and `tests[0].name`/`status`, so a semantic-equality assertion between the committed capture and its receipt's embedded copy closes it without a new field [`evidence/story-4-5/0776785f.../validate-evidence.py:54`]
- [ ] [Review][Patch] `solution-build.log` evidences nothing about the sealed sources, and two projects emit Debug binaries from a Release build — the log opens `All projects are up-to-date for restore.` and closes `Time Elapsed 00:00:03.82` for 48 projects, so no `CoreCompile` ran and MSBuild emits no warnings for skipped compiles; under `TreatWarningsAsErrors=true` the `0 Warning(s)` the validator greps for is vacuous. Separately `Hexalith.EventStore.Gateway` (`:30`) and `Hexalith.EventStore.TestSubscriber` (`:43`) resolve to `bin/Debug/net10.0/` inside a `--configuration Release` build; neither project is in `Hexalith.EventStore.slnx`. Fix: `--no-incremental` in the build block and a validator assertion rejecting `bin/Debug` [`evidence/story-4-5/0776785f.../commands.md:40`, `evidence/story-4-5/0776785f.../validate-evidence.py:141`]
- [ ] [Review][Patch] The packet's only genuinely observed provider fact is a per-run nonce — `stateStoreComponentSha256` hashes a YAML whose `scopes:` list carries the randomized per-run app id, so the digest differs in every captured run (`58a5745c`, `49a68cae`, `b608b852`, ...). Loop 1's D3 covered the unobserved `daprRuntime`/`redisImage` literals; this is the one value that *is* observed, and as constructed it cannot bind configuration identity across runs. Fix: hash the component with `scopes` stripped and retain the raw YAML separately [`AppendDurabilityRaceLiveSidecarTests.cs:361`]
- [ ] [Review][Patch] No clean full-suite re-run follows the mutation campaign — `live-sidecar-test-results.json` ran 08:50:36-08:51:06, *before* the mutation runs, and the only post-mutation receipt covers 2 tests. A mutation campaign necessarily dirties the worktree, so nothing demonstrates the tree was restored beyond those two methods [`evidence/story-4-5/0776785f.../commands.md:112`]
- [ ] [Review][Patch] `keyAddressability` asserts a property while recording none of its inputs — the block carries `genericStateStatus`, `genericStateBody`, `compositeActorRedisReadable` and D1's new `classification`, but neither the generic-state key that was probed nor the Redis key and value that were read, so `keyAddressabilityProven` cannot be checked from the artifact alone [`AppendDurabilityRaceLiveSidecarTests.cs:476`]
- [ ] [Review][Patch] The source-binding drift count in loop 2 is now stale — `validate_source_binding` fails on **8 of 17** rows, not 7. `_bmad-output/planning-artifacts/architecture.md` moved after loop 2 (`226a9e81` 2026-08-16, `c21bd749` 2026-08-19), joining the four edited test sources, this spec, `docs/ci.md` and `docs/concepts/architecture-overview.md`. The packet keeps decaying under unrelated commits because nothing gates it [`spec-4-5-append-durability-race-evidence.md:139`]

- [x] [Review][Defer] Contender discrimination depends on two undocumented sentinels — `IsExactActorContender` requires `candidate.GlobalPosition > 0` while the raw probe writes `globalPosition: 0`, and `session.sessionId` is the same ULID as `rawContender.messageId` in every capture. Several other conjuncts (`UserId`, `DomainServiceVersion`, the contender extension) also discriminate, so no misclassification is reachable today [`AppendDurabilityRaceLiveSidecarTests.cs:609`] — deferred, pre-existing
- [x] [Review][Defer] An undeclared Postgres profile appears in the sealed receipt — `live-sidecar-test-results.json` contains an `Oq8Postgresql` collection and `IdempotencyAdmissionOq8PostgresqlTests`, but the packet documents only Dapr 1.18.1 + `state.redis` + `redis:6` and `environment.md` records no Postgres image or version [`evidence/story-4-5/0776785f.../environment.md`] — deferred, pre-existing
- [x] [Review][Defer] Redis durability configuration is unrecorded in a durability packet — `redisImage: "redis:6"` is a tag rather than a digest, and no `appendonly`/`save` settings or `INFO persistence` output is captured, which is exactly the configuration a lost-write finding depends on [`AppendDurabilityRaceLiveSidecarTests.cs:361`] — deferred, pre-existing

**No new decisions.** Every ambiguous choice chunk C raises is already an open loop-2 Decision; duplicating them would only dilute that list.

**Refuted or dismissed during verification:** "The packet has no manifest" — false; `evidence-sha256.txt` (22 rows, excluding only itself), `commands.md`, `source-state.md` and `verification-summary.md` all sit outside chunk C, which is reviewer information asymmetry. "`redisPassword` is committed verbatim" — the value is `""`; `validate_redaction()` passes and all 15 chunk-C files hash OK. "The 25 deterministic tests are unrun or dropped by a `Category` filter" — corrected in loop 1 and reconfirmed here: they appear in the 75-test receipt. "Parallel interference with the timing-sensitive race test" — the receipt's environment string is `[collection-per-class, non-parallel]`. "`suites[].duration` contradicts `summary.stop - start`" — xUnit reports fixture and collection time outside the summary window; a tooling artifact, not a packet claim.

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
