# 6.1-P1R current-source follow-up, 2026-10-09

This is new EventStore-owned remediation evidence. It does not change the sealed
attempt-21 investigation, the October 1 acceptance, Projects pins, or P1R usability.

## Bound scope

- EventStore `main` HEAD: `5e5d2305d870a03d6d63f52470ce9f7a1ec058da`.
  The four source/test edits in this follow-up have `git diff --binary` SHA-256
  `693d635b71011f717d0d617e7d9f89a96866f17039f38dd6b4a2d19c4e3433b0`.
  The [source binding](packet/source-binding.json) records the actual file bytes.
- Workspace Builds checkout: `468fdbba04e2d9a27d251298875125b57fa6d836`.
  The approved published comparison still selects candidate EventStore 3.115.0
  from `283b07a52c9c70e1c940164a7011ee8c3ad98b2d` and candidate Builds
  `ba4ca78c3868a4757cb92d912a54c8a237871b54`. Published 3.70.1 and
  3.110.0 are comparison inputs only; rollback remains `null`.
- Repository-local source scope was approved in
  [runtime-spec-at-capture.md](../6-1-p1r-remediation/runtime-spec-at-capture.md).
  The original [source packet](../6-1-p1r-remediation/README.md) documents the
  earlier fixes; these additional guards close two current-HEAD write gaps.

## Source changes and checks

The writer now refuses a **present** metadata row with sequence zero before
protection, global-position allocation or staging. New aggregates still begin
without a metadata row. The no-op protection provider now refuses protected
serialization markers on writes, so it cannot persist bytes as `Unprotected`
that its read path would reject.

The new tests first reproduced both gaps on the unchanged source. After the fix,
the Debug/project-reference Server test project built with zero warnings/errors.
Focused xUnit v3 runs passed [EventPersister 49/49](event-persister-tests.log),
[payload protection 35/35](payload-protection-tests.log), and
[stream reader 38/38](event-stream-reader-tests.log), all with zero skips.
The rebuilt [live source lane](live-persistence.log) passed 2/2. Its actual actor
state test restores floor 5/head 12 with snapshot 9, appends 13 through the
writer, restarts the host and sidecar, replays 13, and checks prior event hashes
and a second tenant. This lane restores disposable actor state, not a database
backup or a published rollback package.

An optional broad Server assembly run was stopped after no log progress for
several minutes: exit 58, 1,519 executed, zero failures, 12 skips. It is
incomplete and grants no full-suite pass. The focused and live runs above
completed independently.

## Fresh published matrix and restore

`python3 tools/p1r-published-executor.py run --out <fresh owned directory>
--inputs <approved 2026-10-07 owner-inputs.json>` exited 0 with no executor
errors. Its 17 canonical scenarios and both selected additions executed 163
cases and 5,522 checks: 5,412 passed and 110 failed. The [new packet](packet/packet.json)
retains the actual receipts, package graphs, loaded assemblies, inventories,
fixture configuration and source/package bindings. All seven documented families
were run:

| Family | Passed / attempted | Disposition |
| --- | ---: | --- |
| metadata-read | 44 / 48 | incompatible |
| metadata-write | 310 / 312 | incompatible |
| invalid-evidence | 564 / 570 | incompatible |
| query-wire | 360 / 420 | incompatible |
| projection-wire | 74 / 78 | incompatible |
| mixed-api | 927 / 956 | incompatible |
| checkout shared scope | 704 / 704 | compatible |

The actual selected-package [post-upgrade restore](packet/receipts/352d9e89f7074f1fa94e61ca9d55a9d9.json)
passed 47/47 checks, including fresh-database restore, retained append 13 and
restart. The pre-upgrade backup restored mechanically (44/44 checks) but remains
**incompatible** as rollback because it omits later committed writes. Owned
cleanup passed 115/115 and the candidate restore contract passed 12/12.
Reminder recovery passed 131/131; registered logical-event evolution remains
incompatible (147/150). Historical writer and transport binaries retain their
measured incompatibilities; the current source patch cannot change them.
The selected 3.115.0 stale-fence and unauthorized-effect cases also fail their
expected-denial checks. Stale-fence produces an opaque nested actor exception;
unauthorized-effect reports that denial audit is unavailable. Current source
still forwards the opaque stale-authority failure, and no durable
`ITrustedEffectAuditSink` is registered for the package route. The source live
test checks unchanged domain inventory after any exception; it does not prove
a stable denial or durable denial audit for these package cases. They remain
P2 capability/admission work and nonpassing qualification evidence.

`python3 tools/p1r-published-qualification.py validate
_bmad-output/implementation-artifacts/evidence/6-1-p1r-current-source-2026-10-09/packet`
exited 0 with `valid=true`, `technically_qualified=false`, `qualified=false`,
`decisions_complete=false` and `p1r_usable=false`. Running
`sha256sum --check SHA256SUMS --status` from `packet/` also exited 0.
These validation results apply to the capture checkout identified above. Once
the captured edits were committed, validation against the live checkout returned
exit 2 with `source/configuration inputs changed`: its HEAD no longer matches
the captured HEAD plus diff. The packet remains capture-bound evidence, and its
`SHA256SUMS` still passes. Its source binding has not been changed or resealed.

## Remaining owner decisions

- EventStore owner: accept or reject the exact new source/package capability
  disposition. The 3.115.0 package predates this dirty source diff.
- Builds owner: decide candidate provenance/catalog alignment at the selected
  untagged Builds revision, then separately authorize any release or pin change.
- P2 Platform and Identity/Security owners: finish authenticated query authority,
  safe denial, global watermark/cursor and supported capability admission proof.
  The Projects P2 action remains open.
- Solution Architect: approve the exact supported recovery envelope and
  same-baseline conformance. No capable writable rollback is selected; retain
  AD-17 mutation freeze and forward recovery.
- Test Architect: independently accept or reject the new source/package,
  persisted effects, fresh restore/append/restart, tenant isolation and cleanup
  evidence, including the failed and unavailable lanes.

No P1R usability, downstream readiness or Projects pin transition follows from
this packet alone.
