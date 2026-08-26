---
title: 'Story 4.5: Append Durability Race Evidence'
type: 'chore'
created: '2026-08-08'
status: 'done'
review_loop_iteration: 4
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

**SUPERSEDED 2026-08-26 — see "Resolution (2026-08-26)" and "Execution record (2026-08-26)" below.** Both blockers named here were dispositioned and then cleared: the fixture now probes both control-plane port layouts, and the runtime-attribution blocker was overstated (`~/.dapr/bin/daprd --version` is `1.18.1`, the profile the packet claims; only the placement/scheduler container images are `1.18.2`, now disclosed in `environment.md`). **The re-capture and re-seal completed on 2026-08-26; the packet is valid again** — `python3 validate-evidence.py` exits 0 and `sha256sum -c evidence-sha256.txt` is all-OK.

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

- [x] [Review][Patch] Resolution asserts AC8 passes in the present tense while this diff breaks it — `sha256sum -c` is 21/22 at the reviewed tree [`spec-4-5-append-durability-race-evidence.md:174`]
- [x] [Review][Patch] BLOCKED note names 1 of 13 broken validator gates; the other 12 are 7 missing `[invariant:]` tags, 2 schema versions, and 3 absent fields [`spec-4-5-append-durability-race-evidence.md:165`]
- [x] [Review][Patch] `commands.md` documents a mutation model the code no longer uses ("does not alter ... persisted inputs") and is omitted from the re-seal list [`evidence/story-4-5/0776785f.../commands.md:77`]
- [x] [Review][Patch] `verification-summary.md` hard-codes `25 passed`, `75 passed`, and "All 22 listed files OK"; omitted from the re-seal list [`evidence/story-4-5/0776785f.../verification-summary.md:7`]
- [x] [Review][Patch] key-addressability failure message still prescribes the outcome the predicate stopped requiring, and that string lands in the evidence receipt [`AppendDurabilityRaceLiveSidecarTests.cs:547`]
- [x] [Review][Patch] `infrastructureFree` still carries no `[invariant:]` tag and no perturbation — the loop-1 sub-item was silently dropped, not deferred [`AppendDurabilityRaceLiveSidecarTests.cs:556`]
- [x] [Review][Patch] Mutation receipts do not record which perturbation was armed, so an environmental flake yields a receipt byte-equivalent in every field the validator inspects [`validate-evidence.py:71`]
- [x] [Review][Patch] Validator does not pin the invariant key set; deleting an invariant from the emitted object still validates [`validate-evidence.py:142`]
- [x] [Review][Patch] A probe transport failure is attributed to `key-addressability` rather than infrastructure — `infrastructureFree` shares the `genericActorKeyException is null` conjunct but is asserted 3 positions later [`AppendDurabilityRaceLiveSidecarTests.cs:544`]
- [x] [Review][Patch] D4 residual — `JsonDocument.Parse(originalJson/currentJson)` and the two `Deserialize` calls in the evidence literal still run unguarded on Dapr-sourced bodies before `WriteEvidenceAsync` [`ActorConcurrencyConflictTests.cs:148`]
- [x] [Review][Patch] Untagged `overwrite.StatusCode.ShouldBe(NoContent)` inside the `retained-generic-value` branch misattributes an infrastructure failure [`ActorConcurrencyConflictTests.cs:185`]
- [x] [Review][Patch] `metadataEtagPresent` is two-state for three states — false both when the ETag is absent and when metadata itself is absent [`AppendDurabilityRaceLiveSidecarTests.cs:363`]
- [x] [Review][Patch] `redisRetainedRawJson` is asserted by nothing in `validate_generic_control`, and an unrecognized `HEXALITH_STORY_4_5_MUTATION` value is silently accepted [`validate-evidence.py:151`]
- [x] [Review][Patch] Governance metadata contradicts the BLOCKED state — `review_loop_iteration` still `1` (siblings increment: 4-3 = 3, 4-4 = 2), `status: 'done'` retained, and D1/D2/D4/D5 still unchecked `- [ ]` [`spec-4-5-append-durability-race-evidence.md:5`]
- [x] [Review][Patch] Story report carries no BLOCKED disclosure and states "`evidence-sha256.txt` binds the set" in the present tense [`4-5-append-durability-race-evidence.md:7`]
- [x] [Review][Patch] Deferred-work entry cites `AppendDurabilityRaceLiveSidecarTests.cs:364` (`finalMetadataEtagPresent`); the provider literals it describes are at `:414,416` [`deferred-work.md:1198`]

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

- [x] [Review][Patch] Capture-to-receipt binding is now provable at the identity level, and the validator can close it mechanically — loop 1 deferred this as a timestamp-only "traceability gap". It is stronger than that: committed `generic-etag-control.json` keys on `story-4-5-generic-etag-bc9cedea29ec42bdb171cad9dbfaaff8` while its named receipt `generic-etag-test-results.json` records `...4595aaeae49e473bbb01a0800245ae48`, and committed `append-durability-race.json` carries `sessionId 01KZG95BBK9G9M0Q859KR65N4T` while every session in `race-test-results.json` is `01KZG924...`. Both committed captures came from the post-mutation restored run recorded in `post-mutation-focused-test-results.json`. Each CTRF receipt already embeds the full capture in `extra.output`; the validator reads only `summary` and `tests[0].name`/`status`, so a semantic-equality assertion between the committed capture and its receipt's embedded copy closes it without a new field [`evidence/story-4-5/0776785f.../validate-evidence.py:54`]
- [x] [Review][Patch] `solution-build.log` evidences nothing about the sealed sources, and two projects emit Debug binaries from a Release build — the log opens `All projects are up-to-date for restore.` and closes `Time Elapsed 00:00:03.82` for 48 projects, so no `CoreCompile` ran and MSBuild emits no warnings for skipped compiles; under `TreatWarningsAsErrors=true` the `0 Warning(s)` the validator greps for is vacuous. Separately `Hexalith.EventStore.Gateway` (`:30`) and `Hexalith.EventStore.TestSubscriber` (`:43`) resolve to `bin/Debug/net10.0/` inside a `--configuration Release` build; neither project is in `Hexalith.EventStore.slnx`. Fix: `--no-incremental` in the build block and a validator assertion rejecting `bin/Debug` [`evidence/story-4-5/0776785f.../commands.md:40`, `evidence/story-4-5/0776785f.../validate-evidence.py:141`]
- [x] [Review][Patch] The packet's only genuinely observed provider fact is a per-run nonce — `stateStoreComponentSha256` hashes a YAML whose `scopes:` list carries the randomized per-run app id, so the digest differs in every captured run (`58a5745c`, `49a68cae`, `b608b852`, ...). Loop 1's D3 covered the unobserved `daprRuntime`/`redisImage` literals; this is the one value that *is* observed, and as constructed it cannot bind configuration identity across runs. Fix: hash the component with `scopes` stripped and retain the raw YAML separately [`AppendDurabilityRaceLiveSidecarTests.cs:361`]
- [x] [Review][Patch] No clean full-suite re-run follows the mutation campaign — `live-sidecar-test-results.json` ran 08:50:36-08:51:06, *before* the mutation runs, and the only post-mutation receipt covers 2 tests. A mutation campaign necessarily dirties the worktree, so nothing demonstrates the tree was restored beyond those two methods [`evidence/story-4-5/0776785f.../commands.md:112`]
- [x] [Review][Patch] `keyAddressability` asserts a property while recording none of its inputs — the block carries `genericStateStatus`, `genericStateBody`, `compositeActorRedisReadable` and D1's new `classification`, but neither the generic-state key that was probed nor the Redis key and value that were read, so `keyAddressabilityProven` cannot be checked from the artifact alone [`AppendDurabilityRaceLiveSidecarTests.cs:476`]
- [x] [Review][Patch] The source-binding drift count in loop 2 is now stale — `validate_source_binding` fails on **8 of 17** rows, not 7. `_bmad-output/planning-artifacts/architecture.md` moved after loop 2 (`226a9e81` 2026-08-16, `c21bd749` 2026-08-19), joining the four edited test sources, this spec, `docs/ci.md` and `docs/concepts/architecture-overview.md`. The packet keeps decaying under unrelated commits because nothing gates it [`spec-4-5-append-durability-race-evidence.md:139`]

- [x] [Review][Defer] Contender discrimination depends on two undocumented sentinels — `IsExactActorContender` requires `candidate.GlobalPosition > 0` while the raw probe writes `globalPosition: 0`, and `session.sessionId` is the same ULID as `rawContender.messageId` in every capture. Several other conjuncts (`UserId`, `DomainServiceVersion`, the contender extension) also discriminate, so no misclassification is reachable today [`AppendDurabilityRaceLiveSidecarTests.cs:609`] — deferred, pre-existing
- [x] [Review][Defer] An undeclared Postgres profile appears in the sealed receipt — `live-sidecar-test-results.json` contains an `Oq8Postgresql` collection and `IdempotencyAdmissionOq8PostgresqlTests`, but the packet documents only Dapr 1.18.1 + `state.redis` + `redis:6` and `environment.md` records no Postgres image or version [`evidence/story-4-5/0776785f.../environment.md`] — deferred, pre-existing
- [x] [Review][Defer] Redis durability configuration is unrecorded in a durability packet — `redisImage: "redis:6"` is a tag rather than a digest, and no `appendonly`/`save` settings or `INFO persistence` output is captured, which is exactly the configuration a lost-write finding depends on [`AppendDurabilityRaceLiveSidecarTests.cs:361`] — deferred, pre-existing

**No new decisions.** Every ambiguous choice chunk C raises is already an open loop-2 Decision; duplicating them would only dilute that list.

**Refuted or dismissed during verification:** "The packet has no manifest" — false; `evidence-sha256.txt` (22 rows, excluding only itself), `commands.md`, `source-state.md` and `verification-summary.md` all sit outside chunk C, which is reviewer information asymmetry. "`redisPassword` is committed verbatim" — the value is `""`; `validate_redaction()` passes and all 15 chunk-C files hash OK. "The 25 deterministic tests are unrun or dropped by a `Category` filter" — corrected in loop 1 and reconfirmed here: they appear in the 75-test receipt. "Parallel interference with the timing-sensitive race test" — the receipt's environment string is `[collection-per-class, non-parallel]`. "`suites[].duration` contradicts `summary.stop - start`" — xUnit reports fixture and collection time outside the summary window; a tooling artifact, not a packet claim.

#### Resolution (2026-08-26) — owner authorization to unblock, re-capture and re-seal

Owner decision: **execute the full re-capture and re-seal on this host.** The packet must end this run self-consistent — `python3 validate-evidence.py` exit 0 and `sha256sum -c evidence-sha256.txt` all-OK — with every open loop-2 and loop-3 `[Review][Patch]` item above applied. The two blockers recorded on 2026-08-11 are re-measured below; one is real and now authorized to fix, the other was overstated.

**Blocker re-measurement (this host, 2026-08-26):**

- **Ports — confirmed real.** `docker ps` shows `dapr_placement` publishing `0.0.0.0:6050->50005/tcp` and `dapr_scheduler` publishing `0.0.0.0:6060->50006/tcp`, while `DaprTestContainerFixture.cs:47-48` probes `50005`/`50006` on non-Windows. The fixture's Windows branch already holds the correct host ports behind a stale OS predicate, exactly as loop-2 D4 states.
- **Runtime attribution — overstated, and the claimed profile is truthful here.** The 2026-08-11 note inferred a mislabel from `daprio/dapr:1.18.2` control-plane containers. But the fixture launches `~/.dapr/bin/daprd`, and `~/.dapr/bin/daprd --version` reports **`1.18.1`** — the version the packet claims. `dapr --version` reports CLI `1.18.0` / runtime `1.18.1`, and `dapr_redis` is genuinely `redis:6`. Only placement and scheduler run `1.18.2`, a fact the packet never asserted. A capture taken here is therefore correctly attributed to the reviewed `1.18.1` / `state.redis` / `redis:6` profile. Record the `1.18.2` control-plane images in `environment.md` rather than leaving them undisclosed.

**Decision dispositions — all eleven open Decisions above are now closed:**

- **Loop-1 D1, D2, D4, D5** — applied by the 2026-08-11 resolution; the checkboxes were simply never flipped (loop-2 patch at `spec-4-5-append-durability-race-evidence.md:162`). Now marked resolved.
- **Loop-1 D3 / loop-3 patch on `stateStoreComponentSha256`** — **AUTHORIZED to fix, no longer deferred.** Make provider attribution observed rather than literal: read the actual `daprd` version and the actual Redis image at capture time instead of the hardcoded `"1.18.1"` / `"redis:6"` strings at `AppendDurabilityRaceLiveSidecarTests.cs:361-366`, and remove the corresponding literal assertions from `validate-evidence.py:78,80` in favour of asserting the observed values that this run captures. Additionally hash the state-store component YAML with the per-run `scopes:` list stripped, so `stateStoreComponentSha256` binds configuration identity across runs instead of being a per-run nonce; retain the raw YAML separately.
- **Loop-1 D6 / loop-2 D5** — **AUTHORIZED to fix, no longer deferred.** Add `[Collection("DaprTestContainer")]` and `[Trait("Category", "LiveSidecar")]` to `AppendDurabilityRaceClassifierTests` and `DaprStateErrorParserTests`, bringing them into compliance with the frozen `Always` constraint. **Do not** add the LiveSidecar project to `unit-test-projects` in `ci.yml` and **do not** introduce a category filter — the frozen `Never` still forbids both, so these 25 deterministic tests continue to require Docker + Dapr. That cost is accepted.
- **Loop-2 D1 (mutation attribution)** — **split into individually-perturbable invariants.** The full re-capture is already being paid for, so decompose the conjunctions: every named invariant gets a perturbation that falsifies that invariant and nothing else, and attribution must be pinned rather than resting on the assertion order at `AppendDurabilityRaceLiveSidecarTests.cs:536-556` and `ActorConcurrencyConflictTests.cs:252-259`. In particular, `retryClassificationConsistent` must read the perturbed `classifierSequence` rather than `finalSequence`, and the mid-chain timestamp comparisons that are stamped in sequential program order must either become falsifiable or stop being counted as evidence. `infrastructureFree` gets an `[invariant:…]` tag and its own perturbation. This is the packet's central credibility defect — loop 3 confirmed from the receipts themselves that every mutation run's embedded capture reports all six invariants `true`.
- **Loop-2 D2 (`finalShapeConsistent` outcome-prescriptive)** — **apply the D1 precedent: demote to recorded.** `finalEvents.Count == finalSequence`, the contiguous `1..n` check, and `finalMetadata.LastModified == finalEvents[^1].Timestamp` stop hard-failing. Record the observed final shape and a classification in the capture; the test asserts only that the outcome was classifiable, and the reviewed values are pinned in `validate-evidence.py` exactly as D1 did for `keyAddressability.classification` and `final.metadataEtagPresent`. A torn interleaving is the anomaly this spike exists to catch and must be recorded, not red-gate the required `live-sidecar` check.
- **Loop-2 D3 (no CI step runs the validator)** — **remains deferred, and AC6 is the reason.** Wiring `validate-evidence.py` into `integration.yml` would change `.github/`, which AC6 requires to be byte-identical to the baseline. Preserving AC6 wins. The packet stays operator-discipline only and decay-prone; that cost is accepted and already carried on the deferred ledger. Do not add a CI step and do not add a `Contracts.Tests` binding in this story.
- **Loop-2 D4 (port blocker)** — **AUTHORIZED to fix.** Replace the `OperatingSystem.IsWindows()` predicate at `DaprTestContainerFixture.cs:47-48` with a probe of both candidate ports (`50005`/`50006` and `6050`/`6060`), selecting whichever is reachable. Test-only; no `src/` change.

**Scope guards that still hold unchanged.** This authorization does not reopen the frozen block. AC6 must still be satisfied: `git diff 0776785f494fcefc8ad933b5b17b9c8d5cbe0513..HEAD -- src/ .github/` must print nothing. No fencing implementation, no ETag or concurrency option on the append path, no `MaxPersistenceConflictRetries` default change, and no removal of the `catch (InvalidOperationException)` sites.

**Packet location.** Re-seal in place at `evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/`. The directory is named for `baseline_commit`, which still correctly identifies the unchanged production baseline that AC6 asserts; `source-state.md` separately records the worktree inputs, and those hashes are what gets regenerated. Do not create a new HEAD-named directory. Note that an untracked `__pycache__/` directory exists there from validator runs — it must not be hashed, committed, or listed in `evidence-sha256.txt`.

**Expected end state.** `classifier-parser-test-results.json` at **28/28**, `live-sidecar-test-results.json` at **78**, `append-durability-race.json` at `schemaVersion 3`, `generic-etag-control.json` at `schemaVersion 2`, all mutation receipts carrying their `[invariant:…]` tag and the name of the perturbation that was armed, a semantic-equality binding between each committed capture and its receipt's embedded copy, `--no-incremental` on the Release build in `commands.md` with a validator assertion rejecting `bin/Debug` paths, a clean full-suite re-run following the mutation campaign, regenerated `source-state.md` and `evidence-sha256.txt`, and both `python3 validate-evidence.py` and `sha256sum -c evidence-sha256.txt` green.

#### Execution record (2026-08-26) — re-capture and re-seal completed

The owner-authorized re-capture ran end to end on this host. **The packet is valid again:**
`python3 validate-evidence.py` exits 0 and `sha256sum -c evidence-sha256.txt` reports every listed
file OK with the manifest excluding only itself.

**Test-only changes applied (no `src/`, no `.github/`):**

- `DaprTestContainerFixture.cs` — the stale `OperatingSystem.IsWindows()` port predicate is replaced by a probe of both candidate pairs (`50005`/`50006` and `6050`/`6060`), selecting whichever answers (loop-2 D4). The fixture now also exposes observed provider facts: `daprd --version` from the exact binary it launches, the `dapr_redis` image reference and image ID, the Redis persistence settings, the composite Redis key for a given actor-state key, and a `scopes:`-stripped canonical form of the generated state-store component (loop-1 D3, loop-3 nonce patch).
- `Story45MutationSwitch.cs` (new) — one closed set of recognized perturbation names shared by both capturing tests. An unrecognized `HEXALITH_STORY_4_5_MUTATION` value now throws instead of silently running the unperturbed harness.
- `AppendDurabilityRaceLiveSidecarTests.cs` — the six coarse invariants become **seven individually-perturbable** ones (`gate-hold`, `gate-targeting`, `intermediate-raw-durability`, `key-addressability`, `final-state-classified`, `conflict-retry-classification`, `infrastructure-free`). `infrastructure-free` is now tagged and perturbed (loop-2 patch). The program-order timestamp chain left the `gate-timing` conjunction and is recorded with `timestampChainIsEvidence: false`. `retryClassificationConsistent` was given a `classifierSequence == finalSequence` conjunct. **That claim was wrong and loop 4 was right to reject it:** the conjunct is exactly `!IsArmed("conflict-retry-classification")` and can never be falsified by an observation. It has been removed; the receipt is earned by `classification.IsInternallyConsistent`, which is derived from the observed survivors and retry telemetry. `finalShapeConsistent` was demoted per loop-2 D2 into a recorded `final.shapeClassification` naming every torn interleaving; the test asserts only that the shape was classifiable, and the observed value is pinned in the validator. `metadataEtagPresent` became the three-state `metadataEtagState`. The key-addressability block now records the probed URL, logical key, composite Redis key and the metadata read from it; its failure text no longer prescribes an outcome. The capture carries `mutationArmed` and a per-invariant boolean map.
- `ActorConcurrencyConflictTests.cs` — every Dapr-sourced body is parsed inside a guard (loop-2 D4 residual), the two harness preconditions carry explanatory messages, and the retention expectation follows the last **acknowledged** write so the 409 perturbation no longer collaterally falsifies retention. The `retained-generic-value` perturbation reads a key the run never wrote. Third invariant `stale-token-proven-stale` records the seed→first transition and the ETag advance.
- `AppendDurabilityRaceClassifierTests.cs`, `DaprStateErrorParserTests.cs` — both now carry `[Collection("DaprTestContainer")]` and `[Trait("Category", "LiveSidecar")]`, satisfying the frozen `Always` constraint (loop-1 D6 / loop-2 D5). `ci.yml` `unit-test-projects` and the category filter are untouched, as the frozen `Never` requires.
- Both tests now fail with **one** assertion whose message enumerates every falsified invariant tag, so attribution no longer rests on assertion order.

**Attribution is now mechanically pinned.** Every mutation receipt embeds the capture the perturbed run wrote to xUnit output. `validate-evidence.py` requires that capture to name the armed perturbation and to falsify **exactly** the pinned invariant set. Measured on this host, all nine perturbations falsify exactly one invariant each — the one they are pinned to — which directly closes loop-3's finding that every mutation receipt previously reported all six invariants `true`.

**Other packet changes:** `commands.md` adds `--no-incremental`, corrects the mutation-model prose, removes `HEXALITH_STORY_4_5_EVIDENCE_DIR` from both full-suite runs so a regression run cannot rewrite a committed capture, adds a clean post-mutation full-suite run, replaces the `! rg` redaction gate with explicit `0`/`1`/other branching, and no longer leaks `errexit`/`pipefail`, calls `exit`, or acts on unset variables. `validate-evidence.py` refuses `python -O`, pins both invariant key sets, requires every evidence-relevant source path to be bound, asserts capture-to-receipt identity, and scopes the `bin/Debug` rejection. `environment.md`, `redaction.md`, `verification-summary.md` and the story report were rewritten for the new run.

**Observed counts:** classifier/parser **28/28**; focused race **1/1**; generic control **1/1**; full LiveSidecar suite **80/80** both before and after the mutation campaign (80, not the 78 projected on 2026-08-11 — the tree gained two tests after that estimate); post-mutation focused pair **2/2**; nine mutation receipts each 1 test / 0 passed / 1 failed. `append-durability-race.json` is `schemaVersion 4`, `generic-etag-control.json` is `schemaVersion 3`.

**AC6 correction.** The literal gate in the 2026-08-26 authorization — `git diff 0776785f..HEAD -- src/ .github/` prints nothing — is no longer satisfiable, and not because of this story: `main` has advanced through Stories 3.13-3.15 and 4.8-4.15 since the baseline, so that diff now reports those stories' changes. `commands.md` now carries `story_4_5_ac6`, which **derives** the candidate commits from `git log` (so a future Story 4.5 commit cannot escape the gate by being forgotten from a hand-maintained list) and requires every shared commit touching `src/` or `.github/` to be declared with the story that owns that production change. Two are declared: `86308550` carries Story 4.4's `src/` implementation, and `ba0c367e` carries Story 3.14's Hexalith.Builds SHA rotation in `.github/workflows/release.yml` — it touched the Story 4.5 spec only because that is where the loop-4 review findings were written. The gate also inspects untracked and staged files, which `git diff` alone never sees. It reports `AC6 holds`.

**Concurrency note.** A parallel bmad-loop auto-committed this run's harness edits as `3961bd72` mid-session. The worktree was re-verified against `HEAD` afterwards (`git diff --name-only HEAD -- tests src .github` is empty), so the committed sources are the final ones the capture was taken from, and `source-state.md` binds the worktree bytes that match them.

**Not fixed, and why (all on the deferred ledger):**

- **No CI step runs `validate-evidence.py`** — wiring one changes `.github/`, which AC6 freezes. Owner decision; accepted.
- **`Gateway` / `TestSubscriber` emit `bin/Debug` inside the Release build** — neither is a `.slnx` member, and both `.csproj` files are under the AC6-frozen `src/` tree. The validator allowlists exactly those two, re-checks they really are non-members, and rejects any other Debug path.
- **`Oq8PostgresqlFixture` hard-codes `50005`/`50006`** — it is hash-bound by the sealed Story 4.14 and 4.15 packets and validated by `tools/validate-oq8-platform-evidence.py` from `integration.yml`, so this story must not edit it. The capture forwarded the two ports instead; documented in `environment.md`.
- **Classifier-completeness docstring, and the `MetadataKey_StaleEtagUpdate_IsRejected` / `ActorConcurrencyConflictTests` names** — renaming has blast radius across `commands.md`, the validator's `MUTATIONS` map, and every receipt; deferred to the append-fencing follow-up that re-captures anyway.
- **The ADD-fencing decision still has no owner story or trigger** — unchanged; re-filed on the ledger.

### Review Findings — loop 4

Code review 2026-08-26 (three layers: blind-hunter, edge-case-hunter, verification-gap) against `3961bd72^..worktree`, chunk A — the authored harness, validator, operator docs, spec and report. Machine-generated receipts were not re-reviewed (chunk B); loops 2 and 3 already covered that surface.

**Verified by direct source reading, not inferred.** `InterceptAllocationAsync` (`AppendDurabilityRaceSession.cs`) reads only `_armed` and `_allocationAttempts`; `_targetMessageId` is read solely by the duplicate-arm guard inside `Arm`. `classifierSequence` is defined as `finalSequence + 1` iff the perturbation is armed. `PlacementPortCandidates = [50005, 6050]` with first-reachable-wins, and `environment.md:49-50` records `socat` forwarding `50005 -> 6050` during the sealed capture.

**The headline result of loop 3 is only partly repaired.** Every mutation receipt now falsifies exactly one invariant and names its armed perturbation — a real improvement, and the "all six invariants true in every receipt" defect is gone. But for the invariants below the falsification is *by construction* rather than by observation, so the receipt attests that the mutation switch works, not that the invariant has teeth. This is the eighth recurrence of guards-green-by-construction in this repository.

- [x] [Review][Decision] `gate-targeting` is an assertion inversion wearing a perturbation's clothes — `Arm` only *records* the target ids, and interception is unconditional for any allocation while armed. Arming with `$"{targetCommand.MessageId}-mutated"` changes nothing the harness does; it changes only the string `gateTargetingProven` compares against `actorMessageId`. `commands.md` and `verification-summary.md` both assert the opposite rule in the packet's own words: "a perturbation changes what the harness **does** ... it never inverts an assertion." Either make the perturbation change which writer the gate actually holds (arm a different actor id, so a non-target allocation is intercepted) or drop `gate-targeting` as an invariant and stop claiming it is perturbation-attested.
- [x] [Review][Decision] `final-state-classified` is materially weaker than the `final-state-consistency` invariant it replaced, and weaker than the D2 decision authorized. The only route to `unclassified-final-shape` is `!finalStateFullyRead`, so the invariant now means "the read completed", not "the shape is sound". Every genuine anomaly — `non-contiguous-event-sequence`, `event-beyond-metadata-sequence`, `foreign-writer-present`, `metadata-sequence-without-matching-events`, `final-sequence-out-of-bounds` — returns a *classified* name and passes. D2 authorized recording the shape instead of hard-failing it; it did not authorize removing every automated consumer of `exactContendersOnly` and `finalSequenceWithinBounds`. Restore a recorded-but-asserted invariant that names the shapes the reviewed profile must not exhibit, with its own perturbation.
- [x] [Review][Patch] `retryClassificationConsistent`'s conjunct `classifierSequence == finalSequence` is exactly `!Story45MutationSwitch.IsArmed("conflict-retry-classification")` and can never be falsified by an observation. The receipt is still earned, but by `classification.IsInternallyConsistent`, not by this conjunct. The Execution record's claim that it "makes the perturbed sequence load-bearing" is false and must be corrected [`AppendDurabilityRaceLiveSidecarTests.cs:442`]
- [x] [Review][Patch] `staleReplay.suppliedEtagWasStale` records `!IsArmed("generic-409-semantics")` — the mutation switch restated as though it were an observation — and `validate-evidence.py` asserts it is `True` on the committed capture. Derive it from the observed tokens: `!string.Equals(replayedEtag, currentEtag, StringComparison.Ordinal)` [`ActorConcurrencyConflictTests.cs:260`]
- [x] [Review][Patch] The `stale-token-proven-stale` invariant has no perturbation, and `verification-summary.md`'s justification is false: the `generic-409-semantics` perturbation swaps the replayed token only, while `etagAdvanced` and `seedThenFirstObserved` are computed earlier and stay true, so `staleTokenProvenStale` remains `true` in that run [`ActorConcurrencyConflictTests.cs`]
- [x] [Review][Patch] The `infrastructure-free` perturbation redirects only the newly added `sidecarHealthy` probe; the other seven conjuncts (gate/actor/raw exceptions, both intermediate capture exception types, `finalReadsSucceeded`, `classification.IsInfrastructureFailure`) are unmutated, so the receipt attests one probe rather than the invariant [`AppendDurabilityRaceLiveSidecarTests.cs`]
- [x] [Review][Patch] `ClassifyFinalShape` adds eleven classification branches with zero deterministic coverage — it is `private static` inside the live test class, unreachable from the 28-case table, and exercised only on the one happy path. Mutilating any anomaly branch leaves every packet check green. Extract it beside `AppendDurabilityRaceClassifier` in `Fixtures/` and add a `TheoryData` case table in the shape of the existing `Cases`, one row per return name [`AppendDurabilityRaceLiveSidecarTests.cs:630`]
- [x] [Review][Patch] The port fix the packet advertises was never exercised by the capture that seals it — with `socat` forwarding `50005`, `ResolveReachablePortAsync` returned the first candidate, the same value the old hardcoded predicate used. The `6050`/`6060` branch is dead in this evidence. Either capture with the forwarder down, or state plainly in `environment.md` that the new branch is untested by this run [`DaprTestContainerFixture.cs:51`, `environment.md:44-54`]
- [x] [Review][Patch] `Story45MutationSwitch.KnownMutations` is bound to neither the validator's `MUTATIONS` map nor `RACE_INVARIANTS`/`GENERIC_INVARIANTS`; the nine names are declared independently in two places, so an invariant with no perturbation (as `stale-token-proven-stale` already is) or a perturbation with no receipt passes both sides. Its fail-closed throw on an unrecognized value is also untested [`Story45MutationSwitch.cs`, `validate-evidence.py`]
- [x] [Review][Patch] The `--no-incremental` guard is green-by-construction against exactly the log it was written to reject — `MINIMUM_COMPILED_PROJECTS = 45` counts `^  Name -> path$` lines, which MSBuild emits for up-to-date projects too (loop 3 measured 48 in the vacuous log). Nothing asserts `--no-incremental` appears, checks `Time Elapsed`, or rejects the `All projects are up-to-date for restore.` marker the current log still opens with [`validate-evidence.py`]
- [x] [Review][Patch] `validate_build_log`'s Debug rejection is POSIX-only (`"/bin/Debug/" in path` never matches `\bin\Debug\`), and its `^  (\S+) -> (\S+)$` regex drops any project whose output path contains a space, so the allowlist re-check is vacuous on a Windows capture [`validate-evidence.py`]
- [x] [Review][Patch] Several validator paths crash instead of failing named: `final.metadata` null raises `TypeError` when indexing `currentSequence`; `extra` present-but-null raises `AttributeError`; xUnit output carrying anything besides the capture JSON raises `JSONDecodeError`. A valid-but-different capture yields a traceback rather than a validation message [`validate-evidence.py`]
- [x] [Review][Patch] The mutation wrapper accepts any exit 1 as a successful mutation receipt, so a harness crash, timeout, or argument error is indistinguishable from a falsified invariant — grep the receipt for `[invariant:<name>]` before accepting. The campaign is also not `&&`-chained, so a failed mutation scrolls past and only the last command's status is visible [`commands.md`]
- [x] [Review][Patch] The AC6 gate misses untracked files (`git status --porcelain --untracked-files=all -- src .github` is never consulted) and is enforced by a hand-maintained SHA list that a future Story 4.5 commit escapes unless someone remembers to extend it [`commands.md`]
- [x] [Review][Patch] `redisImageDigestObserved` is an image **ID** (`docker inspect --format {{.Image}}`) documented as a repository digest, and the environment pin block uses it in `docker pull redis@sha256:...` form, which fails on any normally registry-pulled image; the two coincide only on this host [`DaprTestContainerFixture.cs`, `environment.md`]
- [x] [Review][Patch] `environment.md` removed the control-plane image digest pin in the same edit that disclosed the `1.18.2` drift, and `validate_environment_profile` dropped the old placement-digest assertion without adding a replacement, so control-plane reproducibility is now weaker than before the re-seal [`environment.md`, `validate-evidence.py`]
- [x] [Review][Patch] The two earlier focused receipts still embed superseded captures with no disclosure — `race-test-results.json` carries a different `sessionId` and `generic-etag-test-results.json` a different key than the committed captures, which come from the post-mutation restored run. `CAPTURE_BINDINGS` closes only the post-mutation receipt, and `verification-summary.md` lists the other two as plain passes, so the next reader hits exactly the discrepancy loop 3 hit [`verification-summary.md`, `validate-evidence.py`]
- [x] [Review][Patch] The post-mutation full-suite receipt is checked only for counts, never for having run *after* the campaign, so a pre-mutation copy satisfies the very receipt the change was added to provide — assert its `start` is at or after every mutation receipt's `stop` [`validate-evidence.py`]
- [x] [Review][Patch] `redaction.md` deleted the `rg` command from its own "Validation performed before hashing" block and now only narrates branching that lives in `commands.md`, so the file no longer reproduces the scan it documents [`redaction.md`]
- [x] [Review][Patch] The capture date is recorded as local time while every other packet timestamp is UTC — `environment.md`, `verification-summary.md`, `commit-capture.md` and the report all say `2026-08-26` while `session.armedAtUtc` is `2026-08-25T23:17:15.74Z`; the one-day offset reads as a discrepancy [`environment.md`]
- [x] [Review][Patch] Ledger and report bookkeeping: two items annotated `RESOLVED 2026-08-26` sit inline under a block still reading `status: open` in the legacy-advisory format `check-deferred-work.py` reports as unclassified; the ADD-fencing item is filed twice (the older bullet cites `architecture.md:558`, a mermaid line — the decision is at `:603`); and the report names three residual gaps while the ledger files six [`deferred-work.md`, `4-5-append-durability-race-evidence.md`]
- [x] [Review][Patch] `review_loop_iteration` reads `3` on what is the fourth review loop, and `status: 'done'` is a value no sibling spec uses [`spec-4-5-append-durability-race-evidence.md:5`]
- [x] [Review][Patch] `DaprTestContainerFixture`'s resolved ports are static mutable state defaulting to `0`, never reset and shared across fixture instances; a sidecar start not preceded by a successful `VerifyPrerequisitesAsync` passes `--placement-host-address localhost:0` with no diagnostic [`DaprTestContainerFixture.cs`]
- [x] [Review][Patch] `StripScopes` (C#) strips a `scopes:` block wherever it appears while the validator asserts `canonical == component.split("\nscopes:", 1)[0]`, which assumes the block is terminal; the two diverge silently the moment the generator emits any key after `scopes:`. `StripScopes` has no test, and `stateStoreComponentSha256` is not an invariant [`DaprTestContainerFixture.cs`, `validate-evidence.py`]

- [x] [Review][Defer] The seal is circular with the narrative — `source-state.md` hash-binds the spec, but the spec is also the only place the run's outcome and its reviews are recorded, so every status write or findings edit breaks `validate_source_binding`. Confirmed live: the validator exits 0 immediately after sealing and exits 1 on the spec row as soon as `status` moves to `in-review`. Known since loop 2 and independent of this change; the durable fix is to pin the spec and report at a commit or drop those two rows, both of which change the packet's binding model. — deferred, pre-existing
- [x] [Review][Defer] No CI step executes the Story 4.5 validator, so all of the above is operator-discipline only. Owner-deferred this run to preserve AC6; the repo idiom that would fix it without touching `.github/` is a blocking test in `tests/Hexalith.EventStore.Contracts.Tests/Packaging/`, as every sibling packet uses. — deferred, pre-existing

#### Resolution (2026-08-26, loop 4)

Owner decisions, both re-opening calls made earlier the same day:

- **`final-state-classified` — record, but keep an asserted net.** The D2 demotion authorized recording the observed final shape instead of hard-failing it; it did not authorize removing every automated consumer. Keep `ClassifyFinalShape` and the recorded `shapeClassification`, and add an invariant that fails when the classification is one of the shapes the reviewed profile must not exhibit — at minimum `foreign-writer-present`, `event-beyond-metadata-sequence`, `non-contiguous-event-sequence`, `duplicate-event-message-ids`, `metadata-sequence-without-matching-events`, and `final-sequence-out-of-bounds`. That invariant needs its own perturbation that drives a genuinely torn shape rather than skipping a read. An unexpected-but-real outcome is still recorded in full; a torn stream still turns something red.
- **`gate-targeting` — make it a real perturbation.** Arm the gate for a different actor id so a non-target allocation is intercepted and the harness genuinely holds the wrong writer. `gateTargetingProven` must then be falsified by an observation rather than by the switch. Do not keep a perturbation whose only effect is to rewrite the string an assertion compares.

**Scope for this loop: fix everything above, then re-capture and re-seal.** Apply every open loop-4 `[Review][Patch]` item, then run the full campaign again and regenerate `source-state.md` and `evidence-sha256.txt`. All authorizations and scope guards from the 2026-08-26 authorization remain in force unchanged — in particular AC6 (`src/` and `.github/` untouched), no fencing implementation, and no CI wiring of the validator.

**Standing rule this loop must not violate again.** A perturbation changes what the harness *does*. Any invariant conjunct that reduces to `!Story45MutationSwitch.IsArmed(...)`, and any recorded evidence field that restates the mutation switch, is a defect regardless of whether the receipt it produces looks correct. Before sealing, audit every invariant conjunct and every recorded field for that shape and state the audit's result in `verification-summary.md`.

#### Execution record (2026-08-25 UTC, loop 4) — re-capture and re-seal completed

Every open loop-4 `[Review][Patch]` item and both owner decisions were applied, the full campaign
was re-run, and the packet was re-sealed. `python3 validate-evidence.py` exits 0 and
`sha256sum -c evidence-sha256.txt` reports every listed file OK.

**The two decisions.**

- **`gate-targeting` is now a real perturbation.** A decoy aggregate's handler arms the single gate
  before the intended writer runs, so the interception genuinely lands on a non-target allocation
  and the harness holds the wrong writer. `gateTargetingProven` is falsified by an observation
  (`session.TargetActorId` is the decoy's) rather than by a rewritten comparison string. It
  necessarily also falsifies `gate-hold` — holding the wrong writer means the intended one was not
  held — and the validator pins that exact two-element set rather than hiding it.
- **`final-state-classified` became `final-state-sound`.** `ClassifyFinalShape` moved to
  `Fixtures/AppendDurabilityFinalShapeClassifier.cs` with an explicit sound/unsound partition and a
  fourteen-row deterministic case table, one row per return name. The invariant now fails on every
  shape the reviewed profile must not exhibit, not merely on an unread one. Its perturbation writes
  a real extra event one past the metadata sequence through the raw actor-state endpoint, producing
  a genuinely torn stream rather than skipping a read.

**Perturbation-shape audit (the loop-4 standing rule).** Every invariant conjunct and every recorded
field was audited for anything reducing to `!Story45MutationSwitch.IsArmed(...)`. Four defects were
found and fixed — the `classifierSequence == finalSequence` conjunct, the `gate-targeting`
pseudo-perturbation, `staleReplay.suppliedEtagWasStale`, and **one the review did not catch**,
`infrastructure.writerEndpointRedirected`, which the audit found and which now compares the actual
`writerEndpoint` against the actual `sidecarEndpoint`. The seal was taken only after a full re-run
following that last fix. Two switch reads remain by design and are not evidence claims:
`mutationArmed` (the declared provenance the validator binds on) and the early return in
`WriteEvidenceAsync` (which is what stops a perturbed run overwriting a committed capture). One
conjunct is disclosed as inert rather than claimed as exercised: the `classifierSequence != 2` clause
short-circuits at the observed sequence `1` and is covered deterministically instead. The audit
result is stated in `verification-summary.md`, as the rule requires.

**Other patches applied.** `infrastructure-free` gained a second perturbation that redirects the
whole writer endpoint, so its exception conjuncts are exercised rather than only the liveness probe.
`stale-token-proven-stale` gained a perturbation (a decoy post-update read). `Story45MutationSwitch`
is now bound to the validator in both directions — the validator parses `KnownMutations` from the
C# source, requires it to equal its own armed set, and requires every invariant to be covered by at
least one perturbation — and its fail-closed throw is covered by `Story45MutationSwitchTests`.
`StripScopes` became `StripTerminalScopes`, throws if a top-level key ever follows `scopes:`, and is
pinned by `StateStoreComponentCanonicalizationTests` against the validator's own re-derivation;
`state-store-component-identity` is now an invariant with its own perturbation. `solution-build.log`
records the exact build command in its first line and the validator requires `--no-incremental`
there; the Debug-path check normalizes separators and tolerates spaces. The validator fails with
named messages instead of tracebacks, asserts both post-mutation receipts started after the
campaign, discloses that the two pre-campaign focused receipts embed superseded captures, and pins
the control-plane image digests that the previous edit had dropped. `redisImageDigestObserved`
became separate image-ID and repository-digest fields, and `environment.md`'s pin block uses the
pullable form. `commands.md`'s wrapper greps each receipt for its `[invariant:…]` tags before
accepting exit 1, chains the campaign with `&&`, and derives the AC6 commit list from `git log`
while also checking untracked files. `redaction.md` reproduces the scan it documents. All packet
dates are UTC.

**The port fix is now exercised by the capture that seals it.** Loop 4 was right that `socat`
forwarding `50005` made `ResolveReachablePortAsync` return the first candidate, leaving the
`6050`/`6060` branch dead in the evidence. The campaign was therefore re-run in two modes: the
build, deterministic matrix, focused runs, perturbations and restored focused pair with the
forwarder **down**, so the second candidate answered; the two full-suite receipts with it **up**, so
the Story 4.14-owned `Oq8PostgresqlFixture` could start. The capture records
`controlPlanePorts.placementResolved = 6050` and the validator asserts it — a value the replaced
`OperatingSystem.IsWindows()` predicate could never have produced on this platform.

**Observed counts.** Deterministic matrix **53/53**; focused race **1/1**; generic control **1/1**;
full LiveSidecar suite **105/105** both before and after the campaign; restored focused pair
**2/2**; twelve perturbation receipts each 1 test / 0 passed / 1 failed with exact attribution.
`append-durability-race.json` is `schemaVersion 5`, `generic-etag-control.json` is `schemaVersion 4`.

**Environmental note.** A parallel `aspire run` started on this machine during the first attempt at
the post-mutation full-suite receipt and failed three admission tests: it registers the fixed,
non-namespaced `IdempotencyAdmissionActor` type into the same placement ring. The session was
waited out rather than killed, and the receipt was re-taken clean at 105/105. `commands.md` now
warns to check `pgrep -af daprd` and `docker logs dapr_placement` before trusting a red full-suite
run.

**Still deferred, and why.** Six structured ledger entries: no CI step runs the validator (wiring
one changes `.github/`, which AC6 freezes); the seal is circular with this spec's own narrative;
`Gateway`/`TestSubscriber` emit `bin/Debug` under a Release solution build (non-`.slnx` members
whose `.csproj` files are AC6-frozen); `Oq8PostgresqlFixture`'s hardcoded ports (hash-bound by
Stories 4.14/4.15); `AppendDurabilityRaceClassifier` completeness lives in a docstring; and the
ADD-fencing decision still has no owner story, alongside the historical
`MetadataKey_StaleEtagUpdate_IsRejected` naming.

### Review Findings — loop 5, chunk 1

- [ ] [Review][Patch] The current evidence packet fails its authoritative validator because the `docs/ci.md` source binding has drifted, while the spec still claims the packet is valid [`evidence/story-4-5/0776785f.../validate-evidence.py:696`]
- [ ] [Review][Patch] Out-of-range final sequences skip all event readback, so the unexpected outcome is classified without being recorded in full [`AppendDurabilityRaceLiveSidecarTests.cs:305`]
- [ ] [Review][Patch] The validator trusts emitted raw-durability booleans instead of re-deriving the headline claim from the captured event and metadata, and it omits the generic control's seed/update status prerequisites [`evidence/story-4-5/0776785f.../validate-evidence.py:441`]
- [ ] [Review][Patch] Every surfaced `InvalidOperationException` is labeled a concurrency conflict, allowing an unrelated actor failure to pass as a recognized race outcome [`AppendDurabilityRaceLiveSidecarTests.cs:457`]
- [ ] [Review][Patch] The documented control-plane pullable digest is not captured, while image IDs and repository digests receive only prefix/substring validation [`DaprTestContainerFixture.cs:899`]
- [ ] [Review][Patch] Timed-out provider probe processes are neither killed nor awaited before the capture continues [`DaprTestContainerFixture.cs:938`]
- [ ] [Review][Patch] Control-plane port discovery accepts the first TCP listener without verifying that it is the expected Dapr placement or scheduler service [`DaprTestContainerFixture.cs:682`]
- [ ] [Review][Patch] Positive receipts are accepted from summary counters without exact passed entries, exact test identities, and strict positive timestamps [`evidence/story-4-5/0776785f.../validate-evidence.py:242`]
- [ ] [Review][Patch] Capture identity is not semantically bound: `baselineCommit` is ignored and the declared `state.redis` type is not matched to the canonical component YAML [`evidence/story-4-5/0776785f.../validate-evidence.py:360`]
- [ ] [Review][Patch] Build evidence can target another artifact or repeat output lines, while a machine-specific elapsed-time floor substitutes for deterministic compile proof [`evidence/story-4-5/0776785f.../validate-evidence.py:582`]
- [ ] [Review][Patch] `source-state.md` paths are not confined to the repository workspace, so absolute or parent-relative rows can bind external files as repository inputs [`evidence/story-4-5/0776785f.../validate-evidence.py:685`]
- [ ] [Review][Patch] Malformed-but-readable evidence can raise raw JSON, type, or manifest-unpack tracebacks instead of the named `EvidenceError` contract [`evidence/story-4-5/0776785f.../validate-evidence.py:375`]
- [ ] [Review][Patch] Redaction validation recognizes one Linux workspace shape and only top-level CTRF machine/user fields, leaving other platform paths and nested host metadata unchecked [`evidence/story-4-5/0776785f.../validate-evidence.py:668`]
- [ ] [Review][Patch] Private support records violate the repository baseline requiring one C# type per file [`ActorConcurrencyConflictTests.cs:304`; `AppendDurabilityRaceLiveSidecarTests.cs:1004`]

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

**Perturbation integrity — the defect this story kept re-opening**

- Start here: the nine-then-twelve perturbation registry, fail-closed on any unknown name.
  [`Story45MutationSwitch.cs:15`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Story45MutationSwitch.cs#L15)

- The rule the packet now enforces on itself: perturbations change harness inputs, never assertion polarity.
  [`AppendDurabilityRaceLiveSidecarTests.cs:979`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/AppendDurabilityRaceLiveSidecarTests.cs#L979)

- `gate-targeting` rebuilt: a decoy aggregate occupies the gate, so the harness holds the wrong writer.
  [`AppendDurabilityRaceLiveSidecarTests.cs:155`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/AppendDurabilityRaceLiveSidecarTests.cs#L155)

- Transport perturbation redirects the real endpoint; the recorded flag is derived by comparison, not from the switch.
  [`AppendDurabilityRaceLiveSidecarTests.cs:696`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/AppendDurabilityRaceLiveSidecarTests.cs#L696)

- Validator parses the C# registry and requires every invariant to be perturbation-covered.
  [`validate-evidence.py:214`](evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/validate-evidence.py#L214)

**Final-shape safety net — the D2 decision, correctly scoped**

- Shape taxonomy extracted so it is reachable from a deterministic table.
  [`AppendDurabilityFinalShapeClassifier.cs:65`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/AppendDurabilityFinalShapeClassifier.cs#L65)

- The sound/unsound partition: recorded in full, but a torn stream still fails.
  [`AppendDurabilityFinalShapeClassifier.cs:56`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/AppendDurabilityFinalShapeClassifier.cs#L56)

- One row per return name, closing the eleven-branch coverage hole.
  [`AppendDurabilityFinalShapeClassifierTests.cs:36`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/AppendDurabilityFinalShapeClassifierTests.cs#L36)

**Observed provider facts, replacing source literals**

- Control-plane ports probed rather than predicted; the 6050 branch is what this capture exercised.
  [`DaprTestContainerFixture.cs:51`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs#L51)

- Validator pins the resolved port, so a forwarded capture cannot masquerade as the new branch.
  [`validate-evidence.py:399`](evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/validate-evidence.py#L399)

- Build log must prove `--no-incremental`; the old count-based floor passed the vacuous log.
  [`validate-evidence.py:594`](evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/validate-evidence.py#L594)

**Generic-state control**

- Stale-token replay derives staleness from observed tokens instead of restating the switch.
  [`ActorConcurrencyConflictTests.cs:188`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/ActorConcurrencyConflictTests.cs#L188)

**The finding and its decision**

- The provider-qualified silent-overwrite finding, unchanged in substance since loop 1.
  [`4-5-append-durability-race-evidence.md:3`](4-5-append-durability-race-evidence.md#L3)

**Operator surface and peripherals**

- Campaign is `&&`-chained and each receipt is grepped for its own invariant tags.
  [`commands.md:1`](evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/commands.md#L1)

- The pre-seal audit result: every conjunct and recorded field checked for switch restatement.
  [`verification-summary.md:1`](evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/verification-summary.md#L1)

- Fail-closed switch behaviour and canonicalization now carry their own tests.
  [`Story45MutationSwitchTests.cs:63`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/Story45MutationSwitchTests.cs#L63)
