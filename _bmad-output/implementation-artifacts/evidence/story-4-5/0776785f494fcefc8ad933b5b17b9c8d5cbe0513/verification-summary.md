# Verification Summary

## Positive runs

| Receipt | Result |
| --- | --- |
| `classifier-parser-test-results.json` | 25 passed, 0 failed: deterministic race-classifier and safe error-parser branches |
| `race-test-results.json` | 1 passed, 0 failed |
| `generic-etag-test-results.json` | 1 passed, 0 failed |
| `live-sidecar-test-results.json` | 75 passed, 0 failed, 0 skipped; normal tests exercised the actual production allocator through the test decorator |
| `post-mutation-focused-test-results.json` | 2 passed, 0 failed after mutation variables were removed |

`solution-build.log` is the exact Release solution-build output captured through `set -o pipefail`, workspace redaction, and `tee`: build succeeded with 0 warnings and 0 errors.

## Live observation

The final restored race capture records one narrow aggregate-handler arm, exactly one production-allocator interception, one allocation attempt, zero retries, and an incomplete actor task throughout all gated probes. Redis proved the exact raw contender durable before release; after quiescence the exact actor contender survived and the raw write was absent. Classification remained `same-key-overwrite-raw-durable-write-lost` for the captured Dapr `1.18.1` `state.redis` / Redis `6` profile only.

The final generic-state control records HTTP `409`, `ERR_STATE_SAVE`, ETag-mismatch text, no parser error, and complete retained-value equality to `{ "writer": "first", "version": 1 }`.

## Mutation attribution

Each receipt contains one discovered test, one failed test, and no passed test.

| Material invariant | Mutation receipt | Focused test |
| --- | --- | --- |
| Gate timing and intended interception | `mutation-gate-timing.json` | `SameStreamActorAndRawTransaction_RecordsDurableRaceOutcome` |
| Intermediate acknowledged-write durability | `mutation-intermediate-raw-durability.json` | `SameStreamActorAndRawTransaction_RecordsDurableRaceOutcome` |
| Final exact-writer/sequence/metadata consistency | `mutation-final-state-consistency.json` | `SameStreamActorAndRawTransaction_RecordsDurableRaceOutcome` |
| Conflict/retry classification | `mutation-conflict-retry-classification.json` | `SameStreamActorAndRawTransaction_RecordsDurableRaceOutcome` |
| Actor-key addressability | `mutation-key-addressability.json` | `SameStreamActorAndRawTransaction_RecordsDurableRaceOutcome` |
| Exact generic HTTP 409/error semantics | `mutation-generic-409-semantics.json` | `MetadataKey_StaleEtagUpdate_IsRejected` |
| Full retained generic value | `mutation-retained-generic-value.json` | `MetadataKey_StaleEtagUpdate_IsRejected` |

## Integrity and repository gates

| Gate | Result |
| --- | --- |
| `python3 validate-evidence.py .` | Positive counts, seven mutation receipts, race/provider/gate semantics, generic retained value, build log, source binding, redaction, and exact manifest coverage valid |
| `python3 scripts/check-deferred-work.py` | Exit 0; no invalid deferred-work entry |
| `git diff --check` | Exit 0 |
| Baseline diff under `src/` and `.github/workflows/` | No changed paths |
| Redaction scan | No captured machine name, local user field, or absolute workspace path remains in JSON/log evidence |
| `sha256sum -c evidence-sha256.txt` | All 22 listed files OK; manifest excludes itself |
