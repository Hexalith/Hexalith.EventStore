# 6.1-P1R runtime source remediation

This packet records new Debug/project-reference source proof on EventStore base
`d24a03569ed0c8e4d773e24e630a2eab2ec9f074`. It does **not** qualify published
candidate or rollback packages. `qualified` and P1R usability remain false;
exact publication, rollback and owner decisions remain pending. The approved
AD-17 mutation freeze/forward recovery policy remains the operational envelope
until a capable published rollback family is independently qualified.

## Seven-family disposition

| Family | Current-source result | Historical/package boundary |
| --- | --- | --- |
| metadata-read | Existing legacy constructor/JSON defaults to floor 1; reader rejects floor 0 or floor 14 at head 12; floor 5/head 12 with snapshot 9 loads only the needed tail. | The old reader's loss of floor 5 remains immutable incompatibility. |
| metadata-write | Writer now rejects malformed floors before protection, global allocation or staging; append 13 retains valid floors 1/5/13. Real actors preserve floor 5, old hashes and a second tenant through fresh restarts. | The old writer's floor loss remains incompatible and cannot authorize mutation. |
| invalid-evidence | Whole batches resolve/deserialize before Apply/Handle, including direct `AggregateReplayer` legacy replay. Late unknown type, malformed payload, version 987 or protected format refuses application. No-op unprotection now refuses marker/metadata mismatch, protected/opaque/unknown metadata and protected snapshots, including legacy entry points. Concrete actor-state tests preserve all three committed domain entries, zero domain invocation/writes, with one bookkeeping entry separately recorded. Supported legacy, protected and registered evolution paths still run. | Incapable old binaries remain incompatible; source repair does not change them. |
| query-wire | Handler routing retains signed admission proof alongside original actor, workload, delegation, scopes and audience. Legacy null and fallback forwarding remain supported. JSON and DataContract preserve authority; existing admission tests refuse missing/incapable protected authority before access. | Old transport authority loss conveys no invented authority. |
| projection-wire | JSON and newly supported DataContract preserve exact global position 987 independently of sequence 12 and retain existing evolution provenance. Absent legacy position remains unknown (0). Client cursor tests retain tenant/domain/query/scope binding; existing server watermark tests run in the Server suite. | Unknown/zero is not an authorized watermark. Cross-version published transport remains unqualified. |
| mixed-api | Compatible constructors/calls remain. Recovery null/false/true survives JSON. Real admission-owned fenced execution and trusted effects persist; forged proof and stale lifecycle authority fail before effects and leave committed event/metadata hashes unchanged. | Recovery legitimately reuses the existing fence; the fixture invents no new epoch. Old dispatchers lacking newer methods remain incompatible. |
| checkout | Base revision, complete working source diff, untracked source hashes, preexisting changes, Builds input and runtime assembly identities are bound in `source-binding.json`. Reminder recovery and logical evolution have independent live lanes. | Historical repair source `a7404a1d9edf3bf7d125851001c0699ff664abf0` is investigative context, not the chosen checkout. Actual archives/signatures/lock graphs and owner grants are pending. |

## Executed source lanes

Build receipts record each affected test project built separately with
`-c Debug -p:UseHexalithProjectReferences=true -m:1`; final builds have zero
warnings/errors. xUnit assemblies use single-dash filters. The log next to each
JSON receipt is its raw output; receipts bind argv, working directory, times,
exit status and output SHA-256.

| Lane | Final receipt | Result |
| --- | --- | --- |
| Client full suite | `receipts/parent-review-client-suite.json` | 1,056 tests, 0 failed, 0 skipped. |
| Reviewed hydration/cancellation regressions | `receipts/parent-review-client-focused.json` | 147 tests, 0 failed, 0 skipped; includes captured-input mutation, metadata aliases and contradictory V1 provenance. |
| Complete late-invalid replay refusal | `receipts/client-direct-fixed.json` | 20 command hydration/direct replay cases, 0 failed, 0 skipped. |
| Contracts authority/wire/recovery | `receipts/parent-review-contracts-focused.json` | 135 tests, 0 failed, 0 skipped. |
| Contracts full suite | `receipts/parent-review-contracts-suite.json` | 2,252 total, 0 failed, 2 skipped because no release package inventory was supplied; local packaging smoke is separate from published proof. |
| Server retained/protection/evolution/query/admission | `receipts/parent-review-server-focused.json` | 377 tests, 0 failed, 0 skipped; includes protected snapshot dictionary/alias refusal and unprotected compatibility. |
| Server domain inventory | `receipts/server-domain-inventory.json` | 4 cases; 3 unchanged domain entries, 0 domain writes/invocations, 1 bookkeeping entry each. |
| Server full suite | `receipts/parent-review-server-suite.json` | 3,769 total, 0 failed, 25 skipped: existing DW1 ATDD red-phase tests. These skipped cases remain nonpassing. |
| Query routing | `receipts/parent-review-query-suite.json` | 19 tests, 0 failed, 0 skipped. |
| Scoped cursors | `receipts/client-cursor.json` | 27 tests, 0 failed, 0 skipped. |
| Simulated cleanup failure controls | `receipts/parent-review-cleanup-controls.json` | 8 tests passed; process/container operations are mocked, including discovery, launch, inspect/remove and final-inventory failures. |
| Real persistence/fenced/trusted effects | [final persistence receipt](receipts/live-attempt-e64c1b50/live-persistence.json) | 3 tests, 0 failed, 0 skipped. |
| Reminder recovery | [final Reminder receipt](receipts/live-attempt-e64c1b50/live-reminder.json) | 1 test, 0 failed, 0 skipped. |
| Logical evolution | [final evolution receipt](receipts/live-attempt-e64c1b50/live-evolution.json) | 1 test, 0 failed, 0 skipped. |

xUnit reports executed test counts, not a runtime assertion counter. Exact
assertion totals are therefore **unmeasured**, not zero or fabricated. The
checked-in tests and raw persisted inventories state the asserted invariants.
This source packet does not satisfy the stronger published qualification gate's
instrumented assertion-count requirement.

## Persistence, ownership and failed attempts

The retained fixture is invocation-owned Redis actor state, with private Dapr
1.18.4 placement/scheduler containers and installed sidecar. It is a **logical
disposable actor-inventory restoration**, not a database backup restored into a
second fresh database or an old package rollback. A single writer creates 12
events, hashes retained events 5–12 **before** restoration, then installs floor
5 and a covering snapshot at 9 and removes only the disposable prefix 1–4.
Retained events are never rewritten or rehashed to substitute a new baseline.
The actual SDK fold verifies state 12 before append 13 and state 13 after a
second host/sidecar restart. Final output binds snapshot, append, prior-event
and second-tenant hashes, observed state counts 12/13, and physically loaded assembly names/paths/SHA-256. The final [inventory](persisted-inventory.json) and [runtime/cleanup receipt](receipts/live-attempt-e64c1b50/live-runtime-and-cleanup.json) are separately bound.

Each live invocation has a unique receipt directory. Runtime/cleanup receipts
bind exact owned container/image identities, command/process ownership, zero
remaining owned containers/processes, and unchanged shared container IDs,
image IDs, start times and running states. The runner refuses skipped, omitted,
zero-test or failed required live inventories. Timeout output is retained and
only exact owned test groups/label-verified containers can be removed.
Each owned cleanup attempt now continues after an individual failure and retains
raw failure receipts plus a nonpassing final summary. Eight deterministic mocked
controls verify those failure branches. Real timeout/cancellation cleanup drills
and database backup/restore mechanics remain independently unqualified.

`receipts/failed-live-attempt-1/` preserves both original fixture failures:
re-serializing retained JSON changed event hashes, and a recovery assertion
incorrectly expected a new fence. The corrected fixture retains the original
event baseline and uses the actual same-fence Recoverable/Begin/Terminal
contract. Earlier passing live invocations `live-attempt-15f2ffba`, `live-attempt-a9795237` and `live-attempt-0fb7d540` are retained.
The earlier `live-attempt-b00046d8` invocation and its packet metadata in
`receipts/pre-review-packet/` are preserved. The selected final invocation follows
all review patches.
The initial Server red receipt reproduces the runtime defects. Initial compile
errors, the malformed xUnit filter, and the initial broad Contracts failure
(missing projection DataContract) also remain as failed receipts.

## Preservation and remaining qualification

The sealed [historical investigation](../6-1-p1r-3110/verification/README.md)
is unchanged. Its root checksum index passes (`sealed-index-check`). Checking
selected attempt 21's checksum list exits 1 because 27 existing `artifacts/*`
package graph/loaded-identity files are absent in this checkout
(`sealed-selected-check`); those historical inputs remain unavailable and are
not replaced or resealed. Historical seven-family incompatibilities are kept
separate from current-source outcomes.

The baseline diff and preexisting-file hashes are retained here. Existing user
changes remain byte-preserved except the two replay implementations and two
cancellation test fixtures where the authorized preflight repair and additive
converter observation hooks were merged with existing work. Projects planning/acceptance/pins, Builds catalog/readiness and old
evidence are untouched. Aspire was started through its orchestration workflow
before edits; `aspire-baseline.json` records resource state and final discovery
reports no active AppHost.

Still unavailable/nonpassing: authorized exact candidate and capable rollback
publication coordinates; actual published archives/signatures/assets and lock
graphs; real cross-version package matrix and database restore drills; exact
EventStore/Builds/Solution/Test owner conformance and release decisions. Passing
local builds, source tests or local packaging smoke grants none of those
authorities. No package version, acceptance or usability transition is made.

Three independent review lenses produced 15 findings. The spec records every
verdict; confirmed remediation defects were patched and six pre-existing issues
were appended to [deferred work](../../deferred-work.md): direct-input sequence
and identity admission, enumerable null entries, negative replay targets,
termination-getter cancellation, and producer scratch-boundary coverage. Reviewer
probe source is preserved in `receipts/review-probes/`. DomainService producer
and test changes are pre-existing user work, with unchanged hashes; this packet
makes no new verification claim for that separate change.

The local completion commit includes the remediation and required replay/
cancellation prerequisites. Independent pre-existing producer, controller and
replay-evolution-outcome edits remain in the working tree. The source binding
describes the complete tested working source, including those edits, rather
than claiming the local commit alone reproduces every recorded lane.
The selected index was exported and compiled for Client and Server with zero
warnings/errors; its 147 Client and 377 Server focused tests also pass. The first
export build's symlink-output path failure is retained in
`receipts/local-commit-client-build.json`; copied root-declared dependency inputs
resolved it without initializing or updating any submodule. Full staged
whitespace checking flags the raw `.diff` files' required blank context-line
spaces; that result is retained, while the source/document check excludes raw
patches/logs and passes. The exact local message passes commitlint 21.1.0 in
`receipts/local-commitlint-preflight.json`.

## Reproduce

From the repository root, build each test project individually, then invoke its
built test DLL. `record-source-command.py <new-receipt-name> <command...>` records
a new source receipt and refuses existing receipt names. After building the
live-sidecar test project, run:

```bash
python3 _bmad-output/implementation-artifacts/evidence/6-1-p1r-remediation/run-source-live-lanes.py
```

From this evidence directory, `sha256sum --check SHA256SUMS` verifies this new
packet's retained bytes. Source/receipt hash consistency alone is not package
qualification or an owner grant.
