# Story 4.5 Append Durability Race Evidence

## Decision

**Add append-path storage fencing in a separately approved follow-up; defer implementation here.** The observed Dapr `1.18.1` `state.redis` / Redis `6` profile does not enforce AD-5's actor-only, write-once intent against a second writer. Changing the five catches, the retry budget, or metadata ETag before a provider-portable fencing design is selected would encode behavior the captured runtime did not exhibit. This one provider profile does not characterize any other state-store provider.

Evidence root: [`evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/`](evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/). Every claim below is grounded in the named raw capture; `commands.md` contains the exact re-runnable commands and `evidence-sha256.txt` binds the set.

## Observed Redis Race

Source: [`append-durability-race.json`](evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/append-durability-race.json).

- The aggregate-specific test handler armed the test-only gate after metadata rehydration and immediately before the target command entered persistence. The gate decorated and delegated the actual production `DaprGlobalPositionAllocator`; it did not reproduce allocator behavior. Exactly one handler arm and one intended allocation interception were observed. The allocator interface carries only count and cancellation, not aggregate identity, so the narrow handler-to-persist interval is the irreducible test-attribution limitation.
- The actor task remained incomplete when the gate was occupied, after the raw response, and after both intermediate Redis reads. Only then was the gate released. The raw actor-state transaction returned HTTP `204` with an empty body.
- Before gate release, a direct Redis composite-key read returned the raw contender at sequence `1` and metadata `CurrentSequence = 1`. This proves the raw write was durable; HTTP status alone is not used as durability evidence.
- Reading the same logical metadata key through DAPR's generic state API returned HTTP `204`/empty, while the composite actor Redis key was readable. Actor and generic state use different namespaces in this profile.
- After gate release and full quiescence, Redis contained one gapless event at sequence `1`, but it was the exact actor contender. The raw contender previously proven durable was absent. Every final event was required to match one of the two exact contender identities, and metadata sequence was bounded to `0..2` before any event loop. Classification: `same-key-overwrite-raw-durable-write-lost`.
- The actor returned `Accepted = true`. Allocation attempts were `1`, derived retry count was `0`, and neither `InvalidOperationException` nor a `ConcurrencyConflict` result surfaced. The raw request supplied no ETag. Actor metadata persisted `ETag = null`; a caller-supplied token such as `0` would therefore be unverified, not stale.

The outcome classifier is independently table-tested across recognized `409`/`412` writer rejection, `5xx`/unrecognized HTTP/transport infrastructure failure, actor conflict and infrastructure branches, acknowledged total loss, empty state, bounded sequence `2` retry outcomes, and corrupt sequence state. Infrastructure/probe failures are reported only after the gate is released and the harness has attempted final Redis and evidence capture.

The existing concurrent-command control remains separate: `RedisActorStateStore_ConcurrentSameAggregateCommands_PersistsGaplessEventStream` proves that actor-dispatched calls serialize. It does not cover a second storage writer.

## Generic ETag Control

Source: [`generic-etag-control.json`](evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/generic-etag-control.json).

- The seed generic-state read returned ETag `1`.
- A conditional update with ETag `1` returned HTTP `204`; the next read returned ETag `2` and the first update's value. Only after this intervening successful write is token `1` called stale.
- Replaying ETag `1` returned HTTP `409`, DAPR error code `ERR_STATE_SAVE`, and a body containing `possible etag mismatch`. Empty, non-JSON, non-object, and missing/wrong-property error bodies are independently tested through a non-throwing capture parser, while this positive control still requires the exact observed semantics.
- Direct Redis readback retained the complete first conditional update (`writer = first`, `version = 1`) by semantic JSON equality. The rejected stale value was not persisted.

This generic-state control proves the Redis component can surface optimistic concurrency. It does not prove that actor-state transactions supply or enforce the same token.

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

## Scope And Residual Risk

Story 4.5 changes no `src/` persistence behavior, ETag, concurrency option, global-position logic, release workflow, or category filter. The normal LiveSidecar suite now exercises the production allocator through a test-only decorator, including persisted global-position behavior outside the armed race. The observed Dapr/Redis profile permits a durable same-sequence write to be lost silently when an unsupported raw writer bypasses architectural ownership. Until the deferred fencing story is approved and proven across supported providers, AD-5/write-once language describes the required producer contract rather than provider-independent storage enforcement.

## Verification Packet

`classifier-parser-test-results.json` records the deterministic branch matrix; `solution-build.log` records the exact Release solution build through `pipefail` and a redacting `tee`; and `validate-evidence.py` fails closed on receipt summaries, mutation attribution, provider facts, gate invariants, exact retained values, source hashes, redaction, and manifest integrity. `commands.md` defaults every ordinary rerun to a fresh timestamped directory and requires an explicit baseline token before the canonical reviewed capture can be replaced.
