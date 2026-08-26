# Story 4.5 Append Durability Race Evidence

## Decision

**Add append-path storage fencing in a separately approved follow-up; defer implementation here.** The observed Dapr `1.18.1` `state.redis` / Redis `6` profile does not enforce AD-5's actor-only, write-once intent against a second writer. Changing the five catches, the retry budget, or metadata ETag before a provider-portable fencing design is selected would encode behavior the captured runtime did not exhibit. This one provider profile does not characterize any other state-store provider.

Evidence root: [`evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/`](evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/). Every claim below is grounded in the named raw capture; `commands.md` contains the exact re-runnable commands and `evidence-sha256.txt` binds the set. The packet was re-captured and re-sealed on `2026-08-25` UTC after four review loops; `verification-summary.md` records what changed, including the perturbation-shape audit taken before the seal.

## Observed Redis Race

Source: [`append-durability-race.json`](evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/append-durability-race.json) (`schemaVersion 5`).

- The aggregate-specific test handler armed the test-only gate after metadata rehydration and immediately before the target command entered persistence. The gate decorated and delegated the actual production `DaprGlobalPositionAllocator`; it did not reproduce allocator behavior. Exactly one handler arm and one intended allocation interception were observed, and the recorded gate target matched the intended actor id and command message id.
- The actor task remained incomplete when the gate was occupied, after the raw response, and after both intermediate Redis reads. Only then was the gate released. The raw actor-state transaction returned HTTP `204` with an empty body.
- Before gate release, a direct Redis composite-key read returned the raw contender at sequence `1` **and** the metadata written by the same transaction at `CurrentSequence = 1`. This proves the raw write was durable; HTTP status alone is not used as durability evidence. The capture records the exact composite Redis key that was read.
- Reading the same logical metadata key through DAPR's generic state API returned HTTP `204`/empty, while the composite actor Redis key was readable. Classification: `actor-key-absent-from-generic-namespace`. Actor and generic state use different namespaces in this profile. The capture records the probe URL, the logical key, the composite Redis key, and the metadata read back from it, so the classification can be re-derived from the artifact alone.
- After gate release and full quiescence, Redis contained one gapless event at sequence `1`, but it was the exact actor contender. The raw contender previously proven durable was absent. Final shape classification: `gapless-1-event-stream`. Race classification: `same-key-overwrite-raw-durable-write-lost`.
- The actor returned `Accepted = true`. Allocation attempts were `1`, derived retry count was `0`, and neither `InvalidOperationException` nor a `ConcurrencyConflict` result surfaced. The raw request supplied no ETag. Actor metadata recorded `metadataEtagState = etag-absent`; a caller-supplied token such as `0` would therefore be unverified, not stale.

Provider attribution in this packet is **observed, not a source literal**. `daprRuntimeObserved` is read at capture time from `daprd --version` on the exact binary the fixture launches; `redisImageObserved` / `redisImageDigestObserved` come from `docker inspect dapr_redis`; `redisPersistenceObserved` records `appendonly no` and `save 3600 1 300 100 60 10000`, the durability configuration a lost-write finding depends on. Image IDs and pullable repository digests are recorded as separate fields for Redis and for both control-plane containers, because they are different things and coincide only by accident on this host. `stateStoreComponentSha256` hashes the generated component with its per-run `scopes:` list stripped, so the digest binds configuration identity across runs rather than being a per-run nonce; the raw scoped YAML is retained beside it.

The outcome classifier is independently table-tested across recognized `409`/`412` writer rejection, `5xx`/unrecognized HTTP/transport infrastructure failure, actor conflict and infrastructure branches, acknowledged total loss, empty state, bounded sequence `2` retry outcomes, and corrupt sequence state. Infrastructure/probe failures are reported only after the gate is released and the harness has attempted final Redis and evidence capture.

The existing concurrent-command control remains separate: `RedisActorStateStore_ConcurrentSameAggregateCommands_PersistsGaplessEventStream` proves that actor-dispatched calls serialize. It does not cover a second storage writer.

### What is required versus what is recorded

This is a spike, so an unexpected-but-real outcome must be captured rather than red-gate the required `live-sidecar` check. Three things are therefore **recorded with a pinned observed value in `validate-evidence.py`** instead of being asserted by the test: the key-addressability classification, the metadata ETag state (three-state — `metadata-absent`, `etag-absent`, `etag-present` — so an absent metadata record is never reported as an absent ETag), and the final-shape classification, which names every torn interleaving.

Recording is not the same as ignoring. `AppendDurabilityFinalShapeClassifier` partitions its fourteen names into sound and unsound, and the `final-state-sound` invariant **fails** on the unsound set — `unclassified-final-shape`, `final-sequence-out-of-bounds`, `events-without-metadata`, `metadata-sequence-without-matching-events`, `non-contiguous-event-sequence`, `duplicate-event-message-ids`, `event-beyond-metadata-sequence`, `foreign-aggregate-identity-present`, `foreign-writer-present`, and `metadata-timestamp-mismatch`. A provider that exposes the actor key generically or populates the ETag yields a new observation rather than a red build; a torn stream is recorded in full **and** turns the invariant red. The classifier is a `Fixtures/` type with its own deterministic case table, one row per return name, so no anomaly branch is reachable only through a live run.

The timestamp chain `ArmedAtUtc <= FirstAllocationEnteredAtUtc <= rawCompletedAtUtc <= genericActorKeyCompletedAtUtc <= intermediateReadAtUtc` is recorded with `timestampChainIsEvidence: false`. It is stamped in sequential program order on the test thread and holds regardless of sidecar behavior, so it is deliberately not counted as evidence. The observed hold — the actor task incomplete at the gate, after the raw write, and after the gated reads, with release strictly after them — is.

## Generic ETag Control

Source: [`generic-etag-control.json`](evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/generic-etag-control.json) (`schemaVersion 4`).

- The seed generic-state read returned ETag `1` and value `writer = seed`.
- A conditional update with ETag `1` returned HTTP `204`; the next read returned ETag `2` and `writer = first`. Only after this intervening successful write is token `1` called stale, and that transition is itself the `stale-token-proven-stale` invariant.
- Replaying ETag `1` returned HTTP `409`, DAPR error code `ERR_STATE_SAVE`, and a body containing `possible etag mismatch`. Empty, non-JSON, non-object, and missing/wrong-property error bodies are independently tested through a non-throwing capture parser, while this positive control still requires the exact observed semantics.
- Direct Redis readback retained the complete first conditional update (`writer = first`, `version = 1`) by semantic JSON equality. The rejected stale value was not persisted. The retention expectation follows the last write the provider *acknowledged*, so the retention invariant is falsified by a genuine retention failure rather than by any perturbation that changes which write succeeds.
- Every Dapr-sourced body is parsed inside a guard. A malformed or non-JSON body is captured as `parseExceptionType` / `retainedReadExceptionType` with the raw body retained, never thrown before the evidence file is written.

This generic-state control proves the Redis component can surface optimistic concurrency. It does not prove that actor-state transactions supply or enforce the same token. The method name `MetadataKey_StaleEtagUpdate_IsRejected` and the class name `ActorConcurrencyConflictTests` are historical: the test keys on a `story-4-5-generic-etag-*` key and is not actor-state evidence.

## Catch And Retry Classification

All source locations refer to baseline `0776785f494fcefc8ad933b5b17b9c8d5cbe0513` and are recorded in `source-state.md`.

| Surface | Classification | Evidence |
| --- | --- | --- |
| `AggregateActor.cs:686` pre-`EventsStored` commit catch and retry `goto` | **inconclusive / not reached** | The race executed this commit, but the actor accepted, allocation telemetry stayed at one attempt, and no exception or conflict surfaced. One negative Redis run cannot prove dead code for every provider. |
| `AggregateActor.cs:842` initial publish-failure drain commit catch | **inconclusive / not exercised** | The race publisher succeeded; no publish-failure drain commit ran. |
| `AggregateActor.cs:2624` resumed publish-failure commit catch | **inconclusive / not exercised** | No resume or publish-failure path ran. |
| `AggregateActor.cs:2971` cleanup commit after retry exhaustion | **inconclusive / prerequisite not reached** | The pre-commit catch and retry exhaustion were not observed. |
| `AggregateActor.cs:3048` terminal completion commit catch | **inconclusive / not reached** | The terminal completion commit ran successfully; no exception surfaced. |
| `MaxPersistenceConflictRetries`, default `1` | **inconclusive / not exercised** | The configured budget was `1`, but allocation attempts were `1`, retry count was `0`, and the actor accepted without a conflict signal. The budget was not entered in this run, which does not establish its reachability on another provider. |

`retryCount` is derived from `AllocationAttempts - 1`. The `RetryAfterPersistenceConflict:` label sits at `AggregateActor.cs:530`, before rehydration and before `PersistEventsAsync`, so a genuine retry re-enters `AllocateAsync` and would increment the counter. The counter is nonetheless an unfiltered per-session allocation count with no aggregate filter, and `AppendDurabilityRaceControl` is a singleton registered into both the primary and replica hosts; serial `[Collection("DaprTestContainer")]` execution is what keeps an unrelated allocation from consuming it. That limitation is disclosed in `providerProfile.allocatorIdentityLimitation`.

## Scope And Residual Risk

Story 4.5 changes no `src/` persistence behavior, ETag, concurrency option, global-position logic, release workflow, or category filter. The normal LiveSidecar suite now exercises the production allocator through a test-only decorator, including persisted global-position behavior outside the armed race. The observed Dapr/Redis profile permits a durable same-sequence write to be lost silently when an unsupported raw writer bypasses architectural ownership. Until the deferred fencing story is approved and proven across supported providers, AD-5/write-once language describes the required producer contract rather than provider-independent storage enforcement.

Known residual gaps, each carried as a structured deferred-work entry:

1. **No CI step executes `validate-evidence.py`.** Wiring one would change `.github/`, which AC6 freezes, so the packet is operator-discipline only and its source binding decays under unrelated commits. Re-sealing is the recovery.
2. **The seal is circular with the narrative.** `source-state.md` hash-binds this report and the spec, but the spec is also where each review loop's outcome is written, so a status or findings edit after sealing breaks `validate_source_binding`. The durable fix changes the packet's binding model and is out of scope here; seal last.
3. **`Gateway` and `TestSubscriber` emit `bin/Debug` paths inside a Release solution build,** because neither is a `.slnx` member and both `.csproj` files are under the AC6-frozen `src/` tree.
4. **`Oq8PostgresqlFixture` hard-codes control-plane ports `50005`/`50006`.** It is hash-bound by the sealed Story 4.14 and 4.15 packets and validated from `integration.yml`, so this story must not edit it; the full-suite receipts were captured with those ports forwarded.
5. **`AppendDurabilityRaceClassifier` completeness lives in a docstring** — a twenty-first branch would fail no test. (The *final-shape* classifier no longer has this gap.)
6. **The ADD-fencing decision has no tracked owner story or trigger** (`architecture.md:603`), and **`MetadataKey_StaleEtagUpdate_IsRejected` / `ActorConcurrencyConflictTests` still read as actor-state evidence** although the test keys on a generic-state key; renaming would invalidate `commands.md`, the validator's perturbation map and every receipt.

## Verification Packet

`classifier-parser-test-results.json` records the deterministic matrix (53 cases: both classifiers, the safe error parser, component canonicalization, and the perturbation switch's own fail-closed behaviour); `solution-build.log` records the exact `--no-incremental` Release solution build through `pipefail` and a redacting `tee`; `live-sidecar-test-results.json` and `post-mutation-live-sidecar-test-results.json` record the full suite green before and after the mutation campaign; and `validate-evidence.py` fails closed on receipt summaries and ordering, per-invariant perturbation attribution, the binding between the C# perturbation set and its own map, capture-to-receipt identity, provider facts, gate invariants, exact retained values, invariant key sets, source hashes, redaction, and manifest integrity, with named failures rather than tracebacks. It refuses to run under `python -O`. `commands.md` defaults every ordinary rerun to a fresh timestamped directory, requires an explicit baseline token before the canonical reviewed capture can be replaced, and cannot leave `errexit`/`pipefail` armed in the operator's shell or act on an unset variable.
