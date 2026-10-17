# Story 6.4 narrow runtime correction handoff

The authorized finite correction pass is complete. The original adapter-slot failure and all three original replica cases now pass. Full Release Live remains failed because of a separate OQ8 initial-writer timeout. Story 6.4 and sprint remain **in progress**.

| Complete native cohort, invoked once | Selected | Passed | Failed | Declared skips | Errors / not-run | Seconds |
| --- | ---: | ---: | ---: | ---: | --- | ---: |
| Debug DomainService | 978 | 978 | 0 | 0 | 0 / 0 | 205.602 |
| Debug Live | 195 | 194 | 0 | 1 | 0 / 0 | 1288.678 |
| Release Live | 195 | 193 | 1 | 1 | 0 / 0 | 1372.603 |

The sole Live skip remains `ProjectionWatermarkProcessWorkerTests.ApplyPersistedHistoryAsync`, which its owning restart test launches as a child process. Both Live cohorts enrolled all original cases plus three new fixture/configuration regressions. The three original failing replica methods passed in **both** complete Live runs. The [closing audit](implementation-closing-audit.json) records exact cases, native XML/counts, source delta and cleanup.

## Changes and focused checks

- `ProcessReadDeadlineTests` now drains the static adapter pool during teardown by proving all sixteen slots can be held simultaneously, then releasing and awaiting their actual workers. Original assertions and the original recovery timing run unchanged. Production `ProcessReadDeadline` is unchanged. Focused tests passed 4/4; complete Debug DomainService passed 978/978.
- The existing strict, optional private HotReload override is reused by OQ8's existing private SQLite discovery configuration. An absent override preserves the original configuration bytes. Both pinned Dapr 1.18.2 and installed OQ8 Dapr 1.18.7 were observed consuming generated `HotReload=false` configurations. No shared host setting, production Dapr profile or dependency declaration changed.
- Failed OQ8 initialization now emits a separate `initialization-failure.json` with `runtimeProofAvailable=false` instead of hashing already-deleted shadow runtime files. Successful initialization retains the existing complete evidence path. A regression covers failed preflight disposal without external resources.

All three targeted successful producers have zero warnings/errors. Configuration and failed-initialization focused tests passed 10/10. The original three replica methods passed in the preliminary mixed focused run, and the final preserved-profile OQ8 focused check passed 1/1. These focused results retain their own identities and do not replace a complete cohort. The initial xUnit1030 build failure and its correction remain retained.

## Actual profiles and retained preliminary results

The complete Live frames are development/test qualification with explicit private `HotReload=false` configurations and task-owned controls. They do **not** claim an unmodified production-profile qualification. The owning Debug assembly is Debug/source; OQ8 entry hosts retain the historical Release/package configuration. Release uses its fresh targeted producer. Each role consumes the exact recorded output hashes, including inherited mixed runtime/dependency provenance; no all-Release-binary claim is made. Raw effective `project.assets.json` and runtime dependency/configuration bytes are archived by content hash under `asset-bytes/` and bound through producer/execution packets and the supplemental Debug asset receipt.

Complete runs preserve Redis 6.2.21 image `sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf`, `appendonly=no` and `save=3600 1 300 100 60 10000`, verified using the task-owned server. Runtime binary, actual process/configuration and Docker image/argv identities are retained in each controls packet. The scheduler's advertised loopback port is verified against its reachable published port before native execution.

The [chronological frame index](chronological-frame-index.json) retains every failed attempt:

- The first mixed focused OQ8 HTTP 500 used Debug hosts and disabled Redis RDB saving. Its detailed cause was not retained or independently proven; it is not retroactively assigned the later scheduler diagnosis.
- A subsequent preparation stopped before native execution on the Redis configuration check. The preserved-profile focused HTTP 500 then had explicit diagnostics showing an unreachable advertised scheduler container address. Correcting only that task-owned startup command produced the final focused pass.
- `live-debug-full` is a failed **pre-native preparation**, with no native XML/log/invocation. Its original wrapper retained `CalledProcessError` but omitted the command's exit/output. The separate exact-command reproduction returned Docker 125 with forwarding status 500 for port 51701. The actual single complete Debug invocation is explicitly mapped to `live-debug-full-native`; neither packet replaces the other.

The historical outer limits remain Domain 900 seconds, Live 2700 seconds and a 30-second drain, without native retries. Both complete Live runs ended naturally before their limit. The active Debug runner remained immutable; its separately attached PID/start-tick watchdog recorded no timeout. Read-only process sampling raced that natural exit and obtained no managed wait cause.

## Release blocker and acceptance limits

`IdempotencyAdmissionOq8PostgresqlTests.ProductionMatrix_IndependentProcessesPreserveAuthorityReplayExpiryAndLeakageInvariants` failed at its initial writer await (test line 44, `SubmitAsync` line 197) when the existing 30-second HTTP timeout elapsed. The [sanitized diagnostics](live-release-full-oq8-diagnostics.json) correlate this with `sqlite/v1` name-resolution host-registration context deadlines on all three sidecars. The underlying SQLite cause remains **unproven**. No resolver, timeout, assertion, profile or source change was made to pursue this new failure; no further native invocation occurred.

Later OQ8 method-body authority/replay/expiry/leakage and persisted-state assertions were not reached and remain unproven for this Release cohort, even though the runner reports `not-run=0`. The Debug/focused OQ8 passes are not substituted. Each complete Live frame retains 49 projection numerical observations, including fifteen Current and seven tail rows; those records cannot turn the failed Release cohort into acceptance.

Both complete Live controls receipts and the closing audit confirm zero surviving owned children, absent task containers/volumes/listeners, removed fixture/scheduler scratch, and absent OQ8 PostgreSQL containers with stopped fixture processes. Source, restored assets and runtime identities stayed unchanged during all three complete executions. Approved normative digest remains `78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711`.

## Remaining formal requirements

The [separate scoped P1R user approval](../p1r-user-approval-20261017/README.md) is already recorded for 77 files / 98 historical locations. Formal named/authenticated role authority, reviewed category dispositions and exact-boundary negative controls remain open; no P1R policy or historical evidence was changed. Stories 9.1/9.2, the exact-candidate authenticated CI seal, subject-bound owner assurance and the AD-12/NFR8 transition remain separate requirements. No owner approval, issuance time or assurance eligibility was invented.

No staging, commit, push, publication, deployment or operational maintenance cutover occurred. No further build or test queue remains authorized in this pass.
