# Recorded Runtime Evidence and Remaining Gates

These are observations from the supplied October 6 packet, not new execution or package acceptance. The [packet index](../../implementation-artifacts/evidence/6-1-p1r-remediation/README.md) links the raw receipts; [source-binding.json](../../implementation-artifacts/evidence/6-1-p1r-remediation/source-binding.json) and [persisted-inventory.json](../../implementation-artifacts/evidence/6-1-p1r-remediation/persisted-inventory.json) retain exact hashes and identities. README files and operational receipts are deliberately absent from SPEC frontmatter.

## Coordinate boundaries

| Input | Bound coordinate and interpretation |
| --- | --- |
| Historical accepted selected family | EventStore `3.110.0`, tag `v3.110.0`, source `27279fe6431925a6ea046c3f89af61487185c7de`; Builds `4.29.1` / `21ce044ab465ccb2adab58b3d66e394ffbecf3c2`. Jérôme Piquot accepted in four roles at `2026-10-01T06:17:01Z` with recorded limitations; usability remains false. |
| Historical rollback | EventStore `3.70.1`, tag `v3.70.1`, tag source `f13f9925fdca53efa2ab8c90d396ab106f91bb9c`; Builds `7af20f8bafbfe561df6f7705913a0800603090b5`. Archive repository revision `650faf053a98ed1c03c048b8e0d2e4d281b095cf` is separately retained; source-tree comparison matches the tag. |
| Historical investigation comparison | EventStore `2c58ffda41759e895ace4b9625c9bd931a217672`; Builds `688eec9a4333245cc0ff7772115c769094471863`. Attempt 21 records 17 scenarios, 72 cases, 2,845 assertions, and seven incompatible dispositions. |
| Approved planning observation | Projects `311aa85c8e81c7c0b19d5c0004ddf36ceafd5651`; observed EventStore `a7404a1d9edf3bf7d125851001c0699ff664abf0`. This is investigative repair-source context, not the selected tested checkout. |
| Recorded source remediation | Base `d24a03569ed0c8e4d773e24e630a2eab2ec9f074` plus complete tested working diff SHA-256 `220af5d8dbe27386311c7bc1cef1900a65d06db7c6eb9fcc27250913ae2056aa` and untracked-source hashes; Builds `ba4ca78c3868a4757cb92d912a54c8a237871b54`; SDK `10.0.401`; Debug/project references. |
| Completion and current checkout | Local remediation commit `785d58fc` includes required replay/cancellation prerequisites. The packet binds complete tested working source, including independent pre-existing producer/controller/evolution-outcome edits outside that commit. Neither the commit alone nor later current source is asserted to reproduce every recorded lane. |
| Selected source invocation | `receipts/live-attempt-e64c1b50`; private Redis actor state and Dapr `1.18.4` placement/scheduler/sidecar. Physically loaded Server/Contracts/Client/test assembly names, paths, and hashes are bound; their reported `3.113.0.0` assembly version is not a selected published package coordinate. |
| New published qualification | Candidate and capable rollback are null; exact publication/conformance/release decisions remain pending; `qualified=false`, `p1r_usable=false`. |

The [current historical owner packet](../../../../projects/_bmad-output/implementation-artifacts/6-1-p1r-current-exact-baseline-candidate.md) records the original decisions and later dated observations. Earlier unverified/archive/CI observations in that packet remain historical; later 3.110.0 archive replay does not close runtime or rollback gaps.

## Source outcomes by family

| Family | Recorded current-source result |
| --- | --- |
| metadata-read | Existing legacy floor defaults and reader refusal work; floor 5/head 12/snapshot 9 loads the needed tail. |
| metadata-write | Malformed floors refuse before protection/allocation/staging; append 13 preserves valid floor; fresh real actor restarts preserve original retained hashes and the second tenant. |
| invalid-evidence | Complete hydration/direct legacy replay preflight refuses late unknown type, unreadable payload, version 987, unsupported format, contradictory V1 provenance, and conflicting metadata aliases before application. No-op unprotection refuses protected/opaque/unknown metadata and protected snapshots, including dictionary/JSON aliases. Valid legacy/protected/evolution inputs still run. |
| query-wire | Signed handler-query admission proof is retained with actor/workload/delegation/scopes/audience; JSON/DataContract, legacy null, fallback, and existing admission refusal lanes pass. |
| projection-wire | JSON and additive DataContract preserve position 987 independently of sequence 12 plus supported evolution provenance. Legacy absence remains 0; scoped cursor and server persisted-watermark regressions run. |
| mixed-api | Common calls and recovery tri-state survive; real admission-owned fenced/trusted effects persist; forged proof and stale lifecycle authority refuse before effects without changing committed hashes. Recovery uses the existing fence. |
| checkout | Base/diff/untracked inputs, Builds and loaded identities are bound; independent Reminder and logical-evolution live lanes pass. Source results do not change old binaries or qualify published cross-version packages. |

## Executed lanes

Each receipt below is relative to [the source packet](../../implementation-artifacts/evidence/6-1-p1r-remediation/README.md); its adjacent log is raw output. Receipts bind argv/cwd/times/exit/output hash. Final affected source builds have zero warnings/errors.

| Lane | Receipt | Recorded result |
| --- | --- | --- |
| Client suite | `receipts/parent-review-client-suite.json` | 1,056 passed; zero failures/skips. |
| Client reviewed hydration/cancellation | `receipts/parent-review-client-focused.json` | 147 passed; zero failures/skips. |
| Direct late-invalid replay | `receipts/client-direct-fixed.json` | 20 passed; zero failures/skips. |
| Contracts wire/authority/recovery | `receipts/parent-review-contracts-focused.json` | 135 passed; zero failures/skips. |
| Contracts suite | `receipts/parent-review-contracts-suite.json` | 2,252 total; zero failed; two package-inventory skips remain nonpassing. Local packaging smoke is separate. |
| Server focused runtime | `receipts/parent-review-server-focused.json` | 377 passed; zero failures/skips. |
| Server domain inventory | `receipts/server-domain-inventory.json` | Four cases each preserve three domain entries, with zero domain writes/invocations and one separately counted bookkeeping entry. |
| Server suite | `receipts/parent-review-server-suite.json` | 3,769 total; zero failed; 25 pre-existing DW1 ATDD skips remain nonpassing. |
| Query routing | `receipts/parent-review-query-suite.json` | 19 passed; zero failures/skips. |
| Scoped cursors | `receipts/client-cursor.json` | 27 passed; zero failures/skips. |
| Mocked cleanup controls | `receipts/parent-review-cleanup-controls.json` | Eight passed; discovery/launch/inspect/remove/final-inventory failure branches use mocked process/container operations. |
| Live persistence/fencing/trusted effects | `receipts/live-attempt-e64c1b50/live-persistence.json` | Three passed; zero failures/skips. |
| Independent Reminder recovery | `receipts/live-attempt-e64c1b50/live-reminder.json` | One passed; zero failures/skips. |
| Independent logical evolution | `receipts/live-attempt-e64c1b50/live-evolution.json` | One passed; zero failures/skips. |

xUnit measures executed tests, not runtime assertion totals. Assertions are **unmeasured/null**, not zero or inferred from test counts. Source regressions execute without skips; the two broad-suite skip groups and the stronger published qualification gate remain nonpassing. Overlapping focused/full lanes must not be added as unique tests.

## Persisted restoration and cleanup scope

A single writer creates 12 events and hashes retained events 5–12 **before** restoration. The fixture installs floor 5 and covering snapshot 9, removes only disposable prefix 1–4, and never rewrites retained events or substitutes new hashes. The actual SDK fold observes state 12 before append 13 and state 13 after a second host/sidecar restart; snapshot, append, prior-event, and second-tenant hashes are retained in the inventory.

This is a logical disposable actor-inventory restoration in Redis. It does not prove a database backup restored into a second fresh database, an old-package rollback, or PostgreSQL compatibility. The [runtime/cleanup receipt](../../implementation-artifacts/evidence/6-1-p1r-remediation/receipts/live-attempt-e64c1b50/live-runtime-and-cleanup.json) records zero cleanup errors, no owned containers/processes remaining, and preserved shared IDs/image IDs/start times/running state. Unique invocation receipts and ownership/label checks bound cleanup. Real timeout/cancellation drills and database backup/restore remain independently unqualified.

## Historical and failed evidence preservation

The [historical investigation](../../implementation-artifacts/evidence/6-1-p1r-3110/verification/README.md) remains sealed. Its root checksum index passes; selected attempt-21 checking exits 1 because 27 existing package-graph/loaded-identity artifacts are absent in this checkout. They remain unavailable, without replacement or resealing.

Attempt 21 independently validated before the approved Projects sprint edit. Afterwards unchanged `python3 run_verification.py --validate attempt-21` exits 2 with `Protected inventory differs from independently checked workspace hashes`. Its captured sprint is recoverable from Projects `311aa85c8e81c7c0b19d5c0004ddf36ceafd5651`, SHA-256 `bc0d564d8c07a9299c9e678dca2df1d98345ae870a798a6ec99a258bf11e6a93`. Historical reproduction uses that captured workspace; current planning must not be made to appear captured by weakening the validator.

Retain the initial live failures (retained JSON reserialization changed event hashes; recovery incorrectly expected a new fence), earlier passing live invocations, pre-review packet, initial compile/filter/server/Contracts failures, and local-export symlink-path failure. The corrected live fixture preserves original event hashes and the actual same-fence Recoverable/Begin/Terminal contract. The selected export's 147 Client and 377 Server focused tests pass with zero build warnings/errors after root-declared dependency inputs were copied; no nested submodule was updated. Raw patch context whitespace findings remain retained separately from passing source/document checks.

Existing user work was preserved; only the two replay implementations and two cancellation fixtures were merged with authorized prerequisites/observation hooks. Producer/controller/evolution-outcome edits remain independently owned. No new DomainService verification is claimed. Projects acceptance/planning/pins, Builds catalog/readiness, and old evidence were unchanged by source remediation. The source packet records the pre-edit Aspire baseline and final absence of an active AppHost.

## Review residuals and open qualification

Three independent review lenses produced 15 findings. Confirmed remediation defects were corrected: complete V1 provenance, converter-proof captured inputs, snapshot dictionary/format aliases, case-insensitive JSON metadata, and continued cleanup after individual failures. Duplicate findings share their patch; the alleged missing producer verification was rejected because the producer/test bytes predated this remediation.

Six pre-existing issues remain in the adopted [deferred-work ledger](../../implementation-artifacts/deferred-work.md); this spec grants them no completion claim:

| Deferred issue | Unproven boundary |
| --- | --- |
| Direct hydration tail/head | Snapshot-aware continuity and advertised-head admission before callbacks. |
| Direct command identity | Bind tenant/domain/aggregate envelopes to the command identity. |
| Enumerable null entries | Consistent scalar refusal instead of skipping null entries. |
| Negative direct replay target | Typed refusal instead of successful empty replay. |
| Termination getter cancellation | Preserve cancellation when the getter cancels and throws. |
| Producer scratch-boundary test | Independently isolate the private-copy scratch ceiling from encoded-size limits. |

Still pending: owner-authorized exact publication inputs; actual candidate/rollback archives/signatures/assets/lock graphs and cross-version package matrix; complete real database restore and failure/timeout/cancellation cleanup drills; instrumented assertion totals; and exact four-role conformance/release decisions. P2 retains its independent denial/G-4/pin/acceptance gates. Local builds, source tests, packaging smoke, checksum consistency, and historical accepts grant none of these missing authorities.
