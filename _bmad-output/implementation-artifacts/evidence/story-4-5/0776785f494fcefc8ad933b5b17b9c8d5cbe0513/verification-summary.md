# Verification Summary

Re-captured and re-sealed `2026-08-25` UTC (loop 4). This supersedes every earlier capture; the
previous counts, perturbation names, invariant names and schema versions no longer apply. All dates
in this packet are UTC so that a document date never appears to disagree with a capture timestamp.

## Positive runs

| Receipt | Result |
| --- | --- |
| `classifier-parser-test-results.json` | 53 passed, 0 failed: race classifier, final-shape classifier, safe error parser, component canonicalization, and the perturbation switch's own fail-closed behaviour |
| `race-test-results.json` | 1 passed, 0 failed. **Pre-campaign run**; its embedded capture is an earlier observation of the same test and is deliberately *not* the committed capture |
| `generic-etag-test-results.json` | 1 passed, 0 failed. Pre-campaign, same caveat |
| `live-sidecar-test-results.json` | 105 passed, 0 failed, 0 skipped, before the campaign |
| `post-mutation-focused-test-results.json` | 2 passed, 0 failed. **This is the run that produced the two committed captures**; `validate-evidence.py` asserts each committed capture equals the copy embedded here |
| `post-mutation-live-sidecar-test-results.json` | 105 passed, 0 failed, 0 skipped, after the campaign |

The validator asserts that both post-mutation receipts started at or after the last perturbation
receipt finished, so a pre-campaign copy cannot satisfy either. It also asserts that the two
pre-campaign focused receipts embed a *different* capture from the committed ones and holds them to
clean-run semantics — the session-id discrepancy an earlier review loop discovered unaided is now
stated and enforced rather than left for the next reader to trip over.

`solution-build.log` opens with the exact command that produced it, and
`validate-evidence.py` requires that header to carry `--no-incremental` and `--configuration
Release`. That header is the load-bearing check: MSBuild prints a `Project -> path` line for
up-to-date projects too, so a project count cannot separate a real compile from a no-op, and
`0 Warning(s)` is vacuous for a compile that never ran under `TreatWarningsAsErrors=true`. An
elapsed-time floor is a secondary backstop. The build succeeded with 0 warnings and 0 errors.

`Hexalith.EventStore.Gateway` and `Hexalith.EventStore.TestSubscriber` emit `bin/Debug` output paths
inside this Release build because neither is a member of `Hexalith.EventStore.slnx`. Their
`.csproj` files are under `src/`, which AC6 freezes, so the condition is recorded rather than fixed.
The validator allowlists exactly those two names, re-checks that they really are non-members,
rejects a `bin/Debug` path from anything else, and separately requires the LiveSidecar test assembly
to be a Release output. The path check normalizes `\` to `/` and tolerates spaces, so it is not
POSIX-only.

## Live observation

The final restored race capture records one narrow aggregate-handler arm, exactly one
production-allocator interception, one allocation attempt, zero retries, and an incomplete actor
task throughout all gated probes. Redis proved the exact raw contender durable — event **and** the
metadata written by the same transaction — before release; after quiescence the exact actor
contender survived and the raw write was absent. Final shape: `gapless-1-event-stream`. Race
classification: `same-key-overwrite-raw-durable-write-lost`, for the captured Dapr `1.18.1`
`state.redis` / Redis `6` profile only.

Provider attribution is **observed, not asserted from a source literal**: the runtime version from
`daprd --version` on the exact binary the fixture launches; the Redis, placement and scheduler image
references and image IDs from `docker inspect`; the persistence settings (`appendonly no`,
`save 3600 1 300 100 60 10000`) from `redis-cli config get`. Image IDs and pullable repository
digests are recorded as distinct fields, because they are distinct things.
`stateStoreComponentSha256` hashes the component with its terminal `scopes:` block stripped, so
`06284f20919e20ca08439ada6811d1d6612a1ffd76e11cded9d9fa5767ae52d4` binds configuration identity
across runs instead of being a per-run nonce.

The capture also records `controlPlanePorts`: probe order `[50005, 6050]` / `[50006, 6060]` and
resolved `6050` / `6060`. The reviewed capture was taken with no port forwarder running, so the
**second** candidate answered — a value the replaced `OperatingSystem.IsWindows()` predicate could
never have produced on this platform. The dual-probe fix the packet advertises is therefore
exercised by the evidence that seals it, not merely described.

The final generic-state control records ETag `1` advancing to `2` after the intervening conditional
write, then HTTP `409`, `ERR_STATE_SAVE`, ETag-mismatch text, no parser error, and retained value
equality to `{ "writer": "first", "version": 1 }`.

## Mutation attribution

Attribution does not rest on assertion order. Each test evaluates all of its named invariants,
records them as booleans in the capture it writes to xUnit output, and fails with a single assertion
whose message enumerates **every** falsified invariant tag. Each perturbation receipt therefore
embeds its own capture, and `validate-evidence.py` requires that capture to (a) name the
perturbation that was armed and (b) falsify **exactly** the pinned invariant set. The operator
wrapper in `commands.md` additionally greps each receipt for every expected `[invariant:…]` tag, so
a harness crash, timeout or argument error — all of which also exit `1` — cannot be mistaken for a
falsified invariant.

The validator also binds the two declaration sites: it parses
`Story45MutationSwitch.KnownMutations` from the C# source and requires it to equal the set of armed
perturbations in its own map, and it requires every named invariant to be falsified by at least one
pinned perturbation. An invariant without a perturbation, or a perturbation without a receipt, now
fails the packet instead of passing on both sides. The switch's fail-closed throw on an
unrecognized name is itself covered by `Story45MutationSwitchTests`.

| Invariant | Perturbation — what the harness does differently | Receipt | Invariants falsified |
| --- | --- | --- | --- |
| `gate-hold` | releases the gate before the contending writers run | `mutation-gate-hold.json` | `gate-hold` |
| `gate-targeting` | a decoy aggregate arms the gate first, so the single interception genuinely holds the **wrong** writer and the intended one runs unhindered | `mutation-gate-targeting.json` | `gate-hold`, `gate-targeting` |
| `intermediate-raw-durability` | skips the gated metadata read | `mutation-intermediate-raw-durability.json` | `intermediate-raw-durability` |
| `key-addressability` | skips the namespace probe | `mutation-key-addressability.json` | `key-addressability` |
| `final-state-sound` | writes a real extra event one past the metadata sequence through the raw actor-state endpoint, producing a genuinely torn stream | `mutation-final-state-sound.json` | `final-state-sound` |
| `conflict-retry-classification` | classifies against a sequence the final state does not exhibit | `mutation-conflict-retry-classification.json` | `conflict-retry-classification` |
| `infrastructure-free` | sends the sidecar liveness probe to an unroutable endpoint | `mutation-infrastructure-free.json` | `infrastructure-free` |
| `infrastructure-free` (second) | redirects the whole writer endpoint, so the raw-write and probe **exception** conjuncts are exercised, not only the liveness probe | `mutation-infrastructure-free-transport.json` | `infrastructure-free`, `key-addressability`, `conflict-retry-classification` |
| `state-store-component-identity` | hashes the raw scoped component instead of the canonical form | `mutation-state-store-component-identity.json` | `state-store-component-identity` |
| `stale-token-proven-stale` | reads the post-update state from a decoy key that was seeded and never updated | `mutation-stale-token-proven-stale.json` | `stale-token-proven-stale` |
| `generic-409-semantics` | replays the token that is still current | `mutation-generic-409-semantics.json` | `generic-409-semantics` |
| `retained-generic-value` | reads the retained value from a key the run never wrote | `mutation-retained-generic-value.json` | `retained-generic-value` |

Two perturbations falsify more than one invariant, and the pinned sets say so rather than hiding it:
holding the wrong writer necessarily means the intended writer was not held, and redirecting the
writer endpoint necessarily destroys the namespace probe and the race classification.

## Perturbation-shape audit

The standing rule for this packet is that a perturbation must change what the harness *does*, and
that no recorded evidence field may restate the perturbation switch. Before sealing, every invariant
conjunct and every recorded field was audited for anything reducing to
`!Story45MutationSwitch.IsArmed(...)`. The audit found and fixed the following, and the seal was
taken only after a full re-run:

- **Removed:** `retryClassificationConsistent`'s `classifierSequence == finalSequence` conjunct. It
  was exactly `!IsArmed("conflict-retry-classification")` and could never be falsified by an
  observation. The receipt is earned instead by `classification.IsInternallyConsistent`, which is
  derived from the observed survivors and the observed retry telemetry.
- **Replaced:** `gate-targeting`'s perturbation. Rewriting the message id an assertion compares
  changed nothing the harness did, because `InterceptAllocationAsync` never reads the recorded
  target. A decoy aggregate now arms the gate first, so the interception genuinely lands on a
  non-target allocation.
- **Replaced:** `final-state-classified` (which could only be falsified by skipping a read, and
  passed every genuine anomaly) with `final-state-sound`, which fails on the shapes the reviewed
  profile must not exhibit and is perturbed by injecting a real torn stream.
- **Derived from observation:** `staleReplay.suppliedEtagWasStale`, previously
  `!IsArmed("generic-409-semantics")`, now `replayedEtag != currentEtag`.
- **Derived from observation:** `infrastructure.writerEndpointRedirected`, previously
  `IsArmed("infrastructure-free-transport")`. The capture now records the actual `writerEndpoint`
  and `sidecarEndpoint` and derives the flag by comparing them; the validator asserts they are
  equal in the reviewed run.

Two switch reads remain by design and are **not** evidence claims: `mutationArmed`, which is the
declared provenance the validator uses to bind a receipt to its perturbation, and the early return
in `WriteEvidenceAsync`, which is what keeps a perturbed run from ever overwriting a committed
capture.

One conjunct is disclosed as inert rather than claimed as exercised:
`retryClassificationConsistent`'s `classifierSequence != 2 || …` clause short-circuits `true` at the
observed sequence `1`. It guards a sequence-2 retry outcome this profile did not produce; that
branch is covered deterministically by `AppendDurabilityRaceClassifierTests` instead.

## Recorded rather than required

So a different provider produces a new observation instead of red-gating the required `live-sidecar`
check: `keyAddressability.classification`, `final.metadataEtagState` (three-state, so an absent
metadata record is never reported as an absent ETag), and `final.shapeClassification`. Their
observed values are pinned in `validate-evidence.py`. `final-state-sound` is the asserted net over
the classification — a torn stream still turns something red, per the loop-4 owner decision.

The timestamp chain `ArmedAtUtc <= FirstAllocationEnteredAtUtc <= …` is recorded with
`timestampChainIsEvidence: false`: it is stamped in sequential program order on the test thread and
holds regardless of sidecar behaviour, so it is not counted as evidence. The observed hold is.

## Integrity and repository gates

| Gate | Result |
| --- | --- |
| `python3 validate-evidence.py .` | Twelve perturbation receipts with exact per-invariant attribution, eleven invariants all perturbation-covered, the C#/validator perturbation registries bound to each other, two capture-to-receipt bindings, receipt ordering, race/provider/gate semantics, pinned invariant key sets, generic retained value, build-log command header, source binding with a required-path floor, redaction, and exact manifest coverage all valid |
| `python3 scripts/check-deferred-work.py` | Exit 0 |
| `git diff --check` | Exit 0 |
| AC6 (`story_4_5_ac6` in `commands.md`) | No Story 4.5 commit, tracked working-tree change, or untracked file under `src/` or `.github/` |
| Redaction scan | Exits 1 (no match) with explicit `0`/`1`/other branching, so a failed scan cannot report clean |
| `sha256sum -c evidence-sha256.txt` | All listed files OK; manifest excludes only itself |

The validator fails with named messages rather than tracebacks: a missing file, a null metadata
record, a null `extra`, or xUnit output that is not the capture JSON alone each produce a stated
validation error. It refuses to run under `python -O`, where `assert` would be stripped.
