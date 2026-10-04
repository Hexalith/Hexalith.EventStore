# P1R compatibility, replay and rollback investigation

[Attempt 10](attempt-10/README.md) is the selected complete investigation: **17 scenarios passed their execution assertions, 72 top-level cases, 2,693 assertions and 1,626 literal command receipts**. The run exits **1** because seven compatibility dispositions are incompatible. Offline packet validation exits **0**. `qualified`, P1R usability and owner-acceptance grants remain **false**; the fixed acceptance record and downstream states are unchanged.

The root [manifest](manifest.json) is a report index binding the selected invocation, actual package/loaded assembly identities, source coordinates, byte policy and limitations. Root [scenario-results](scenario-results.json) is an exact copy of the selected results. Validate the selected invocation, not this report index.

## Reproduce and validate

From this `verification/` directory:

```bash
python3 -W error::ResourceWarning -m unittest test_run_verification.py
python3 run_verification.py --out <new-directory>
python3 run_verification.py --validate attempt-10
sha256sum --check SHA256SUMS
```

All **60 controls pass**, including missing/mismatched evidence, omitted or escaping fixture bindings, removed inventories, package/assembly/restore graph substitutions, coordinated archive-DLL/loaded-hash mutations, escaped JSON and bearer secrets, missing/failing Docker startup, failed cleanup discovery, literal process-launch failure, real node-stop failure, private Docker inspection, timeout, actual subprocess SIGINT, and idempotent cleanup. A fresh run refuses to overwrite an existing directory. Incompatible, unavailable, failed, zero-assertion or cleanup-failed lanes cannot qualify P1R.

The unchanged adjacent archive verifier was also executed by this invocation:

```bash
python3 ../verify_public_packages.py
```

Its existing selected archive record stays byte-preserved. Published lanes restore only from NuGet.org into separate fresh caches, build Release hosts/domain/probes, verify repository signatures and archive sources, bind exact package/assembly coverage and assets/lock graphs, and hash physically loaded assemblies against pinned DLL maps independently checked from signed archives. The exact current-source lane uses Debug/project references and executed physical Host/Domain/Probe output inventories bound to each loaded identity, with no additional-deps or hosting-startup injection. All 19 fixture files use canonical LF bytes; the local EditorConfig and EventStore Git attributes agree. Git filtering with `core.autocrlf=true` preserves their hashes.

## Actual results

| Scenario | Execution | Compatibility | Assertions |
| --- | --- | --- | --- |
| provenance | passed | compatible | 266 |
| legacy-metadata | passed | compatible | 4 |
| metadata-read | passed | incompatible | 8 |
| metadata-write | passed | incompatible | 50 |
| full-replay | passed | compatible | 96 |
| snapshot-tail | passed | compatible | 96 |
| retained-covered | passed | compatible | 96 |
| retained-uncovered | passed | compatible | 192 |
| missing-event | passed | compatible | 192 |
| invalid-evidence | passed | incompatible | 420 |
| query-wire | passed | incompatible | 16 |
| projection-wire | passed | incompatible | 4 |
| mixed-api | passed | incompatible | 116 |
| checkout | passed | incompatible | 1029 |
| post-upgrade-restore | passed | compatible | 49 |
| pre-upgrade-restore | passed | compatible | 53 |
| failure-cleanup | passed | compatible | 6 |

Both package directions execute actual Counter actor/domain hydration, Tenant isolation, sequences and committed event hashes. Snapshot/tail and retained covering snapshots replay successfully. Missing prefixes with absent/non-covering snapshots, and missing interior/tail events, reject without new domain events or aggregate-state mutation. Command-status/dead-letter bookkeeping is counted separately.

The rollback writer appends sequence 13 through its actual EventPersister but discards floor 5. Old query round trips discard dual-principal authority in JSON and DataContract; old projection round trips discard positive global position 987. Recovery status fields disappear on old typed reads. New fenced/trusted-effect/floor methods invoke real typed actor proxies: old hosts reject their unknown actor contracts; selected hosts validate null inputs or return the retained floor. These fenced/trusted-effect controls establish dispatcher presence and input validation, not successful effect execution. Common client calls work in both directions. Real cursor codecs consume each other's tokens and reject tampering; the selected AddProjectionWatermark helper executes and rejects zero, while the old client honestly lacks that helper.

Invalid floor, the protected serialization marker and unknown metadata version are accepted unsafely by both published versions and current source. Unreadable envelopes and unknown event types reject. Passing these negative controls retains an incompatible disposition.

[Shared-scope comparison](attempt-10/shared-scope-comparison.json) records **selected/current measured equivalence** for legacy metadata, query/projection fields, both directions of replay/snapshot/retention/missing-event controls, and retained metadata writing. Both have the same unsafe invalid-evidence handling, so the checkout lane remains incompatible. [Source inventory](attempt-10/source-comparison.json) binds the exact current files and changes: Reminder contracts/actors, conditional AddEventStoreReminders registration and Dapr.Actors.AspNetCore dependency are outside this shared scope. No tagged-package proof applies to those additions.

Both full PostgreSQL dump/restore cases ran with stopped writers and fresh owned databases. Dump byte hashes/lengths bind pg_dump output to actual pg_restore input; committed inventories, actor rehydration, Tenant isolation, event hashes, sequence 12/3 and application/sidecar health pass after old-host restart. Post-upgrade restoration covers the ordinary untrimmed/snapshot fixture and does not establish compatibility for retained metadata or newer APIs. The pre-upgrade chronology is old backup → selected write → stop writers → fresh restore → old restart; the later selected write is absent after restoring the earlier backup. This proves containment mechanics only. A rollback-policy change or acceptance of data loss requires a later named owner decision.

## Ownership and retained diagnostics

Private Dapr **1.18.2**, the recorded digest-pinned PostgreSQL image, owned placement/scheduler/pubsub, loopback endpoints and a private SQLite discovery file were used. SQLite is Alpha test infrastructure; domain state stays PostgreSQL. [Cleanup](attempt-10/cleanup.json) proves owned processes stopped, all four owned containers were removed, scratch was destroyed, and shared container/binary identities and states match before/after. Credentials, cursor keys and database dumps were destroyed; retained data is disposable fixture identifiers, safe diagnostics, counts and hashes.

Earlier invocations remain byte-preserved diagnostics:

- Attempts 01/02 retain original startup/source-restore failures and failed cleanup receipts. Exact owned cleanup recoveries are [01](attempt-01-cleanup-recovery.json) and [02](attempt-02-cleanup-recovery.json). [Recovery context](failed-attempt-cleanup-context.json) proves attempt 02's three removed resource IDs were prior attempt 01 resources carrying this task's ownership label; zero common shared resources changed, and the original shared resource snapshot is equal after recovery.
- Attempt 03 exposed the inventory SQL conversion error after successful current-source builds and actor seeds. Attempt 04 exposed dispatcher classification and failed-scenario host leakage; both original outcomes remain. Attempt 05 is the successful focused mixed-API/restore subset after repair.
- Attempt 06 retains the old cancellation-continuation defect and an inventory filename collision detected by stricter validation. Captures now name both writer and reader, preserving each direction independently.
- Attempt 07 is the real infrastructure SIGINT receipt after cancellation repair: the matrix stops, all later scenarios are unavailable, and owned cleanup/shared preservation pass. Later fixture hardening means older invocations' fixture hashes do not match the final implementation. They are not substituted for attempt 10.

- Attempt 08 completed the earlier full matrix. [Attempt 09](attempt-09-audit-reproduction/README.md) reproduces the later audit findings: empty or omitted provenance identities and unbound host hashes were incorrectly accepted after resealing, and missing Docker startup escaped without a packet and leaked one owned scratch directory. All temporary reproduction resources were removed.
- Attempt 10 closes those findings. Startup/discovery failures enter protected finalization; launch failures retain literal command receipts; failed after-discovery and node stopping still destroy owned scratch and retain nonpassing cleanup evidence. Docker inspection projects only the required identity/state/label fields. Exact five-package and two-probe coverage, pinned signed-archive DLL maps, current build-output receipts and every retained live host identity are checked. Attempt 08 remains historical because the validator/control fixture bytes changed; its artifacts were not edited.

The root checksum file binds this index/report, canonical fixtures, selected checksum manifest, prior diagnostic checksum manifests and recovery receipts. Every selected invocation artifact is independently covered by its own SHA256SUMS and strict validator. No production runtime fix, owner decision, deployment, publication, commit, push, dependency/gitlink update or nested-submodule initialization is part of this investigation.
