# Fresh published execution 325e3c9327df4810941981b5882e3a70

The full invocation and [packet preparation](prepare.json) exited 0. The separate [independent validation](validate.json) exited 0 and recomputed `valid=true`, `technically_qualified=false`, `decisions_complete=false`, `qualified=false`, `p1r_usable=false` and `capable_rollback_qualified=false`. This is truthful measured execution evidence; it grants no owner acceptance or readiness.

The nineteen scenario/addition receipts contain **163 cases, 4909 checks, 4819 passed and 90 failed**. Historical incompatible operations and the missing selected logical alias/evolution path remain nonpassing. [The case index](nonpassing-case-index.json) names every incompatible/unsupported/failing case and its failed checks.

| Lane | Execution | Compatibility | Passed / attempted | Failed |
| --- | --- | --- | --- | --- |
| provenance | passed | compatible | 33 / 33 | 0 |
| legacy-metadata | failed | incompatible | 22 / 24 | 2 |
| metadata-read | failed | incompatible | 44 / 48 | 4 |
| metadata-write | failed | incompatible | 262 / 264 | 2 |
| full-replay | passed | compatible | 246 / 246 | 0 |
| snapshot-tail | passed | compatible | 246 / 246 | 0 |
| retained-covered | passed | compatible | 246 / 246 | 0 |
| retained-uncovered | passed | compatible | 492 / 492 | 0 |
| missing-event | passed | compatible | 492 / 492 | 0 |
| invalid-evidence | failed | incompatible | 564 / 570 | 6 |
| query-wire | failed | incompatible | 360 / 408 | 48 |
| projection-wire | failed | incompatible | 74 / 78 | 4 |
| mixed-api | failed | incompatible | 931 / 952 | 21 |
| checkout | passed | compatible | 329 / 329 | 0 |
| post-upgrade-restore | passed | compatible | 47 / 47 | 0 |
| pre-upgrade-restore | passed | incompatible | 44 / 44 | 0 |
| failure-cleanup | passed | compatible | 115 / 115 | 0 |
| reminder-recovery | passed | compatible | 125 / 125 | 0 |
| logical-event-evolution | failed | incompatible | 147 / 150 | 3 |


Actual package lanes independently verified all three roles: candidate 3.115.0 and comparison-only 3.70.1/3.110.0 each passed 89/89, 267 total. Local process controls passed 38/38. The separate strict candidate restore receipt passed 12/12 and invocation-owned repeated container/process/scratch cleanup passed 7/7. The original four shared containers kept their exact IDs, image IDs, start times and running state.

The physical restore preserved floor 5/head 12/snapshot 9 into a fresh Redis database, hydrated 12, appended 13, stopped the writer, and restarted/replayed 13 with prior events/snapshot and second tenant unchanged. Containment separately restarted real 3.70.1 Host/Domain consumers against a fresh pre-upgrade backup, hydrated 12 and 3 for both tenants, and preserved original hashes; it loses the later committed write and stays `containment_only=true`, `rpo_zero=false`, incompatible.

Reminder positive cases observed read-only sequence 12 before restart and completed natural Scheduler sequence 13 before any manual duplicate callback. Stale-generation likewise observed completed natural sequence 13 before its revision change and stale callback. The selected logical-event-evolution fixture measured bounded V1 CLR-name serialization, typed legacy hydration, unknown metadata refusal and unchanged Dapr application readback. Its live Domain registration observation shows no registered evolution manifest. All three selected evolution cases therefore retain failed registered-path checks and incompatible disposition; there is no authoritative gateway pin, upcaster execution or readiness claim.

The source comparison uses separate Debug/project references and passed 329 checks over eleven cases. [Exact source closure](executor-source.json) binds EventStore HEAD `4e4ee8587ee1f6e73f704cd548734e963b2af3e9` and dirty/untracked executor/consumer bytes; source file SHA-256 is `1479ffeb2b1949bcc7e18c91f681cbb7dd6bceb0302f3f652a7ce33a532a527d`. Source consumers retain [graphs and loaded identities](source-consumers/evidence.json) plus [actual copied loaded binary hashes](source-loaded-binary-map.json). The source SDK uses its real private Development JWT issuer/validator and permitted readiness route; production identity and P2 acceptance remain pending.

[Planning/package observations](planning-observations.json) preserve all 15 actual archive/content/repository identities, selected tag/Builds coordinates and workspace Builds 4.30.0 separation. [Actual archives](archive-preservation.json) are copied outside the sealed harness packet and independently reinspected against its observations. 3.70.1's tag `f13f9925fdca53efa2ab8c90d396ab106f91bb9c` differs from all five archive repository commits `650faf053a98ed1c03c048b8e0d2e4d281b095cf`; that distinction remains explicit. Candidate and 3.110.0 tag/package commits match.

[Accepted execution inputs](owner-inputs.json) retain rollback=null and Projects AD-17 mutation freeze/forward recovery. [Owner decisions](owner-decisions.json) keep EventStore/Builds/Solution/Test acceptance and same-baseline conformance pending. PostgreSQL/Dapr 1.18.2, P0/P2/P3/P4, G-6, Story 6.1, Story 8.11, release availability and coordinated pins/readiness receive no inherited acceptance.

[Focused verification](verification/focused-tests.json) ran 92 tests successfully; [diff check](verification/diff-check.json) exited 0. [Four tooling negative controls](negative-controls.json) refused an actual stale source closure, a substituted source-file hash, substituted configuration bytes and an omitted required direction at both receipt import and independent validation of newly resealed disposable packet copies. These controls confer no operational qualification.

The [run command/status](run.json), [all raw commands](commands.json), [runtime identity](runtime-identity.json), rendered [configuration records](configurations/), raw [receipts](receipts/), [prepare output](prepare.log) and [validate output](validate.log) retain exact argv, times, exit statuses, checks and hashes. Docker discovery selected only public preservation fields and invocation ownership labels before capture; [safe diagnostics scan](safe-diagnostics-check.json) found no unfiltered Config.Env output. Credentials and RDB dumps stayed in owned scratch and were destroyed.

Earlier full, interrupted, source graph, scheduler and privacy trials are indexed by the [run root](../README.md). Their original bytes remain private outside the repository; newly safe derived copies record original hashes and exclusions and are never imported into this final packet.
