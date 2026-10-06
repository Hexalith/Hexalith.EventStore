# 6.1-P1R successor preparation and process controls

This is the approved preparation slice. It binds the actual current EventStore
HEAD, physical source/configuration hashes, dirty tracked diff, untracked source,
staged additions and their index blob identities (including later working edits),
initialized root-declared dependencies and their HEADs/diffs, retained source
packet and the unchanged canonical seventeen-scenario inventory. Seven families
and the selected Reminder/evolution additions remain explicit. It preserves the
historical baseline and sealed packets.

From the EventStore repository root, choose a new output path for each invocation:

```bash
python3 tools/p1r-qualification.py prepare --out /tmp/p1r-prepare-new
python3 tools/p1r-qualification.py run --out /tmp/p1r-run-new
python3 tools/p1r-qualification.py validate /tmp/p1r-prepare-new
python3 tools/p1r-qualification.py validate /tmp/p1r-run-new
python3 -m unittest discover -s tools/tests -p 'test_p1r_qualification.py'
```

`prepare` and `run` refuse every existing output path, including symlinks. They
retain `packet.json`, `source-binding.json` and `SHA256SUMS`; `run` additionally
retains one uniquely named JSON receipt and safe output text per built-in process
control under `receipts/`. No arbitrary commands, package coordinates, backend
choices, container operations or owner decisions can be supplied. Linux procfs
and descendant subreaping plus kernel process handles (`pidfd`) are required for
`run`. A private inherited registration keeps detached children discoverable
after an early parent exit; signals target the exact observed process identity.

The six process controls execute real startup failure, successful completion,
timeout, SIGINT cancellation, detached descendants and repeated cleanup. Receipts
bind exact argv/cwd/times/exit status, output hash, PID/start-time/session identity,
both cleanup observations and individually executed assertion outcomes. Counters
record attempted, passed and failed assertions; they are independent of test
counts. Each process cleanup attempts every owned identity after a failure. A
separate invocation-owned sentinel represents a shared process and must retain
the same identity/running state throughout the controls, before its explicit
release at the end. An external SIGINT retains partial output/receipts and yields
a nonpassing control when it interrupts a case that expected another outcome.
Receipts record the launched root and first cleanup ownership observation;
validation also checks exit-status consistency, control interval ordering and
the sentinel's observed release. Suspended sentinels fail preservation checks.

`validate` is read-only. It independently reconstructs the current input closure,
checks the complete fixed inventory, all receipt/output hashes, actual remaining
owned process identities and measured counters. Changed/omitted/duplicate inputs,
zero/unmeasured counters, failed cleanup and invented authority fail safely. The
same relevant source/configuration checkout is required: a later change requires
a new invocation. Checksums establish consistency, without authenticating owner
acceptance or qualifying a package.

Exit `0` means structurally valid preparation and passing executed process
controls. Exit `2` means refused, incomplete or invalid preparation/evidence.
Every packet and validation result keeps `qualified=false` and `p1r_usable=false`.
Required package/runtime/actor/database scenarios remain `unavailable/unverified`
with assertion count `null`; a valid packet cannot promote them to conformance.

The following unique **pre-review** names are reserved before generation. Each
packet has its own byte-level checksum index and source binding; command receipts
under `verification/` retain exact argv, cwd, times, exit code and output hashes.
These are preparation/process receipts; the post-review spec metadata will require
a new final invocation rather than resealing or transferring these results.

| Evidence | Reserved path |
| --- | --- |
| Preparation packet | [pre-review prepare](invocations/pre-review-prepare-339b173651334cb5b9682c841e5ffcd3/packet.json) |
| Real process-control packet | [pre-review run](invocations/pre-review-run-339b173651334cb5b9682c841e5ffcd3/packet.json) |
| Exact command/output receipts | [verification index](verification/pre-review-339b173651334cb5b9682c841e5ffcd3-index.json) |
| Independent tampered-copy refusal | `invocations/pre-review-tamper-339b173651334cb5b9682c841e5ffcd3/`; [validation receipt](verification/pre-review-339b173651334cb5b9682c841e5ffcd3-validate-tamper.json) |
| Preserved initial packets, copied byte-for-byte | `initial-observations/p1r-initial-prepare-20261006/`, `initial-observations/p1r-initial-prepare-v2-20261006/`, `initial-observations/p1r-initial-run-20261006/` |

Initial packets remain historical observations of earlier source bytes and command
paths under `/tmp`; they are not claimed valid for current source. The first was a
retained missing-input refusal; the later prepare and process run passed at their
collection time. Copies preserve the original receipts and checksums unchanged.

The pre-review prepare/run and both independent validation commands exited `0`.
Each binds HEAD `a6fc951e2c01c1c816aed1d03c851c0639f12cdd` plus the actual dirty,
staged/untracked and root-dependency inputs at collection. All six real controls
passed **38 attempted / 38 passed / 0 failed assertions**, with both cleanup
observations empty and the shared sentinel unchanged. The focused suite passed
17 tests, including staged additions with subsequent unstaged edits and real
external SIGINT/descendant end-state. The earlier concurrent-source-drift suite
run remains a failed observation; the packet-contract fixtures now use a controlled
source snapshot, while real CLI invocations still refuse live input drift.

The following **final** paths are reserved before collection. Their command
index records the actual outcomes; an invalid tampered copy remains a deliberate
negative control. The post-review receipt contract has explicit root and sentinel
release observations, so earlier packets retain their collection-time meaning.

All three review layers completed. Eighteen actionable findings were fixed; two
concurrent runtime verification gaps are recorded separately in
`_bmad-output/implementation-artifacts/deferred-work.md`. The independent final
focused suite passed **29 tests**, including real early-parent-exit cleanup,
suspended-sentinel detection and second-SIGINT cleanup. The same command is wired
into `.github/workflows/p1r-qualification.yml`, which passed `actionlint`; the
sealed existing CI file is unchanged. The [retained suite receipt](verification/final-0b0a66d921b24ad5a15ba1882f46ff8d-checks/check-0.json)
and output preserve the exact parent invocation.

| Evidence | Reserved path |
| --- | --- |
| Final preparation packet | [final prepare](invocations/final-prepare-0b0a66d921b24ad5a15ba1882f46ff8d/packet.json) |
| Final real process-control packet | [final run](invocations/final-run-0b0a66d921b24ad5a15ba1882f46ff8d/packet.json) |
| Final command/output receipts | [final verification index](verification/final-0b0a66d921b24ad5a15ba1882f46ff8d-index.json) |
| Final tampered-copy refusal | `invocations/final-tamper-0b0a66d921b24ad5a15ba1882f46ff8d/`; [validation receipt](verification/final-0b0a66d921b24ad5a15ba1882f46ff8d-validate-tamper.json) |

Still pending separately owned gates: exact published candidate and capable
rollback, immutable package archives/signatures/assets/lock graphs, full
cross-version runtime/actor compatibility, fresh database backup/restore with
lossless append/restart and tenant proof, container cleanup, Test Architect
acceptance of published assertion instrumentation, exact EventStore/Builds/
Solution/Test owner decisions and same-baseline conformance. The approved mutation
freeze and forward recovery remain in force until a capable published writable
rollback qualifies. Six existing deferrals, independent P2 denial gates,
P0/P2/P3/P4, G-6, readiness, Story 6.1 and Story 8.11 retain their decisions.
No publication, deployment, pins, Projects/Builds mutation or acceptance transition
is performed by these tools.
