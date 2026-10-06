# P1R compatibility, replay and rollback investigation

[Attempt 21](attempt-21/README.md) is the selected complete matrix: **17 scenarios passed their execution assertions, 72 top-level cases, 2,845 assertions and 2,333 literal command receipts**. Offline validation exits **0**. Invocation exits **1** because seven compatibility dispositions remain incompatible. All 1,100 owned processes stopped, four owned containers were removed, scratch was deleted, and no cleanup errors were recorded. All six shared-resource snapshot entries match. The verification packet is complete and valid; `qualified`, P1R usability and owner-acceptance grants remain **false**.

Attempt 21 executes the retained-floor and contract-wire rules plus review corrections R01–R07. Historical attempts 01–20 remain byte-preserved. Attempt 19 is prior reviewed evidence under its original fixture version. [Attempt 18](attempt-18/README.md) retains unavailable-Docker discovery, and [attempt 20](attempt-20/README.md) retains a scheduler startup forwarding-500 failure with complete owned cleanup and unchanged shared snapshots.

The root [manifest](manifest.json) is a report index binding the selected invocation, actual package/loaded assembly identities, source coordinates, byte policy and limitations. Root [scenario-results](scenario-results.json) is an exact copy of the selected results. Validate the selected invocation, not this report index.

## Reproduce and validate

From this `verification/` directory:

```bash
python3 -W error::ResourceWarning -m unittest test_run_verification.py
python3 run_verification.py --out <new-directory>
python3 run_verification.py --validate attempt-21
sha256sum --check SHA256SUMS
```

All **135 controls pass**, including missing/mismatched evidence, omitted or escaping fixture bindings, removed inventories, package/assembly/restore graph substitutions, coordinated archive-DLL/loaded-hash mutations, escaped JSON and bearer secrets, missing/failing Docker startup, failed cleanup discovery, literal process-launch failure, real node-stop failure, private Docker inspection, timeout, actual subprocess SIGINT, and idempotent cleanup. The added controls reject incomplete replay, a non-covering snapshot, a changed retained floor, a metadata floor other than 5, accepted invalid evidence, a checkout label that contradicts its nested observations, a successful old dispatcher, and a failure-cleanup exit outside 7, 124, or 130. Port controls prove reservation lifetimes, application/sidecar handoff, actual address collisions, bounded retries, independent owned cleanup, cancellation, and refusal to remove an unrelated scheduler container. The review controls additionally bind shared/protected/process inventories, reject malformed evidence, clear hook/profiler injection, validate recovered scheduler ownership, and retain real healthy-node output/lifetimes after startup failure. A fresh run refuses to overwrite an existing directory. Incompatible, unavailable, failed, zero-assertion or cleanup-failed lanes cannot qualify P1R.

The unchanged adjacent archive verifier was also executed successfully by this invocation:

```bash
python3 ../verify_public_packages.py
```

Its existing selected archive record stays byte-preserved. Published lanes restore only from NuGet.org into separate fresh caches, build Release hosts/domain/probes, verify repository signatures and archive sources, bind exact package/assembly coverage and assets/lock graphs, and hash physically loaded assemblies against pinned DLL maps independently checked from signed archives. The recorded source-comparison lane uses invocation-owned local clones of the three pinned commits and Debug/project references and executed physical Host/Domain/Probe output inventories bound to each loaded identity, with no additional-deps or hosting-startup injection. Only the 19 bound fixture files enter scratch builds; unbound source/build files and previous bin/obj contents are excluded. All 19 fixture files use canonical LF bytes; the local EditorConfig and EventStore Git attributes agree. Git filtering with `core.autocrlf=true` preserves their hashes.

## Actual results

| Scenario | Execution | Compatibility | Assertions |
| --- | --- | --- | --- |
| provenance | passed | compatible | 279 |
| legacy-metadata | passed | compatible | 4 |
| metadata-read | passed | incompatible | 8 |
| metadata-write | passed | incompatible | 50 |
| full-replay | passed | compatible | 96 |
| snapshot-tail | passed | compatible | 96 |
| retained-covered | passed | compatible | 96 |
| retained-uncovered | passed | compatible | 196 |
| missing-event | passed | compatible | 196 |
| invalid-evidence | passed | incompatible | 420 |
| query-wire | passed | incompatible | 52 |
| projection-wire | passed | incompatible | 10 |
| mixed-api | passed | incompatible | 116 |
| checkout | passed | incompatible | 1115 |
| post-upgrade-restore | passed | compatible | 52 |
| pre-upgrade-restore | passed | compatible | 53 |
| failure-cleanup | passed | compatible | 6 |

Both package directions execute actual Counter actor/domain hydration, Tenant isolation, sequences and committed event hashes. Snapshot/tail and retained covering snapshots replay successfully. Missing prefixes with absent/non-covering snapshots, and missing interior/tail events, reject without new domain events or aggregate-state mutation. Command-status/dead-letter bookkeeping is counted separately.

The rollback writer appends sequence 13 through its actual EventPersister but discards floor 5. Old query round trips discard dual-principal authority in JSON and DataContract; old projection round trips discard positive global position 987. Recovery status fields disappear on old typed reads. New fenced/trusted-effect/floor methods invoke real typed actor proxies: old hosts reject their unknown actor contracts; selected hosts validate null inputs or return the retained floor. These fenced/trusted-effect controls establish dispatcher presence and input validation, not successful effect execution. Common client calls work in both directions. Real cursor codecs consume each other's tokens and reject tampering; the selected AddProjectionWatermark helper executes and rejects zero, while the old client honestly lacks that helper.

Invalid floor, the protected serialization marker and unknown metadata version are accepted unsafely by both published versions and current source. Unreadable envelopes and unknown event types reject. Passing these negative controls retains an incompatible disposition.

[Shared-scope comparison](attempt-21/shared-scope-comparison.json) records **selected/current measured equivalence** for legacy metadata, query/projection fields, both directions of replay/snapshot/retention/missing-event controls, and retained metadata writing. Both have the same unsafe invalid-evidence handling, so the checkout lane remains incompatible. [Source inventory](attempt-21/source-comparison.json) binds the exact current files and changes: Reminder contracts/actors, conditional AddEventStoreReminders registration and Dapr.Actors.AspNetCore dependency are outside this shared scope. No tagged-package proof applies to those additions.

Both full PostgreSQL dump/restore cases ran with stopped writers and fresh owned databases. Dump byte hashes/lengths bind pg_dump output to actual pg_restore input; committed inventories, actor rehydration, Tenant isolation, event hashes, sequence 12/3 and application/sidecar health pass after old-host restart. Attempt 21's post-upgrade backup contains the retained-floor-5 stream with events 5–12 and a sequence-9 covering snapshot. Old-host hydration preserves floor 5, sequence 12 and the event hashes, so this measured restore is compatible. The separate real old-writer append drops that floor and remains incompatible; the restore result grants no downgrade-safe writing or newer-API guarantee. Attempt 17's earlier ordinary untrimmed/snapshot restore label remains historical. The pre-upgrade chronology is old backup → selected write → stop writers → fresh restore → old restart; the later selected write is absent after restoring the earlier backup. This proves containment mechanics only. A rollback-policy change or acceptance of data loss requires a later named owner decision.

## Ownership and retained diagnostics

Private Dapr **1.18.2**, the recorded digest-pinned PostgreSQL image, owned placement/scheduler/pubsub, loopback endpoints and a private SQLite discovery file were used. SQLite is Alpha test infrastructure; domain state stays PostgreSQL. [Cleanup](attempt-21/cleanup.json) proves all 1,100 owned processes stopped, all four owned containers were removed and scratch was destroyed, without cleanup or discovery errors. All six shared-resource snapshot entries match their bound discovery/binary observations. Credentials, cursor keys and database dumps were destroyed; retained data is disposable fixture identifiers, safe diagnostics, counts and hashes.

Earlier invocations remain byte-preserved diagnostics:

- Attempts 01/02 retain original startup/source-restore failures and failed cleanup receipts. Exact owned cleanup recoveries are [01](attempt-01-cleanup-recovery.json) and [02](attempt-02-cleanup-recovery.json). [Recovery context](failed-attempt-cleanup-context.json) proves attempt 02's three removed resource IDs were prior attempt 01 resources carrying this task's ownership label; zero common shared resources changed, and the original shared resource snapshot is equal after recovery.
- Attempt 03 exposed the inventory SQL conversion error after successful current-source builds and actor seeds. Attempt 04 exposed dispatcher classification and failed-scenario host leakage; both original outcomes remain. Attempt 05 is the successful focused mixed-API/restore subset after repair.
- Attempt 06 retains the old cancellation-continuation defect and an inventory filename collision detected by stricter validation. Captures now name both writer and reader, preserving each direction independently.
- Attempt 07 is the real infrastructure SIGINT receipt after cancellation repair: the matrix stops, all later scenarios are unavailable, and owned cleanup/shared preservation pass. Later fixture hardening means older invocations' fixture hashes do not match the final implementation. They are not substituted for the selected invocation.

- Attempt 08 completed the earlier full matrix. [Attempt 09](attempt-09-audit-reproduction/README.md) reproduces the later audit findings: empty or omitted provenance identities and unbound host hashes were incorrectly accepted after resealing, and missing Docker startup escaped without a packet and leaked one owned scratch directory. All temporary reproduction resources were removed.
- Attempt 10 closes those findings. Startup/discovery failures enter protected finalization; launch failures retain literal command receipts; failed after-discovery and node stopping still destroy owned scratch and retain nonpassing cleanup evidence. Docker inspection projects only the required identity/state/label fields. Exact five-package and two-probe coverage, pinned signed-archive DLL maps, current build-output receipts and every retained live host identity are checked. Attempt 08 remains historical because the validator/control fixture bytes changed; its artifacts were not edited.

The root checksum file binds this index/report, canonical fixtures, selected checksum manifest, prior diagnostic checksum manifests and recovery receipts. Every selected invocation artifact is independently covered by its own SHA256SUMS. The strict validator rejected the historical shared-resource discrepancies; checksum consistency alone grants no validation or qualification. No production runtime fix, owner decision, deployment, publication, commit, push, dependency/gitlink update or nested-submodule initialization is part of this investigation.

## Resumed review and historical blockers

The runner binds exact committed source closures, signed archive/loaded Domain identities, complete assets/lock graphs, actual per-case host/sidecar/database operations, wire collections/defaults, explicit missing-event categories and ordered persisted invariants. It derives compatibility labels and invocation status from observations. All 135 controls pass, including real SIGTERM-ignoring descendants; removing the final group kill breaks all three descendant controls. Removing HTTP identity validation breaks its repaired unique-receipt control; removing wire validation breaks its positive-path control.

Snapshot interval 10 produces the observed sequence-9 covering snapshot. The validator now checks that exact fixture and preserves replay/state assertions. Docker discovery uses full IDs, so short/full aliases cannot be removed twice; independent failures and retries retain their earlier receipts. No active checkout, dependency or gitlink was changed: the comparison remains EventStore `2c58ffda41759e895ace4b9625c9bd931a217672`, Builds `688eec9a4333245cc0ff7772115c769094471863` and Commons `116d26815eb81e35b3c161e1799e5ee12805fc0a`. Later active EventStore source is outside this comparison.

- Attempt 10 is historical and unchanged; the current validator rejects its missing query bindings.
- Attempt 11 completed the matrix and exposed the sequence-10 assertion, Docker ID alias issue, an external shared-resource change and control edits during capture. Its original nonpassing receipts remain.
- Attempt 12 was deliberately cancelled while correcting the cleanup test stub; actual cancellation stopped the matrix, and owned cleanup/shared preservation passed. Its fixture-drift receipt remains.
- Attempt 13 completed all 17 scenarios/72 cases with 2,782 assertions and 2,120 receipts. Owned cleanup passed, but an unrelated shared container disappeared; strict validation exits 2.
- Attempt 14 completed the unchanged final fixtures. Strict validation exits 2 because unrelated container `c1c8c0d4f857486f86eb7d863bcb86b8b0930b5076429ec336e0307b11260150` appeared. No owned removal targets either unrelated identity.

Historical blocking command: `python3 run_verification.py --validate attempt-17` → `INVALID: Post-upgrade restore lacks a retained-floor stream` (exit 2). Attempt 17 still records the earlier unrelated shared-container removal, and its fixture hashes no longer match the current probe and runner bytes; those checks come later. Attempt 14 retains its own shared-resource blocker. Attempt 19 previously validated under its original 125-control fixture version. These packets remain unchanged; the current complete report validates attempt 21 while keeping qualification and acceptance/downstream states unchanged.

Attempt 16 completed the hardened matrix and cleanup but was rejected for missing scoped status setup receipts. Its original packet remains unchanged. The selected fresh capture includes both versions’ setup prerequisites.

The third review adds bindings for successful fresh database creation, writer quiescence, application/sidecar lifetimes, status/cursor endpoints, safe request observations, successful archive preflight and owned cleanup receipts. Typed query/projection round trips compare all common fixture fields. Timeout/cancellation retain only backup digests and lengths; process launch defers cancellation until registration, failed termination retains receipts, child temporary files stay under owned scratch, six-port selection reserves distinct sockets, and HTTP error-body failures retain attempts. The four restored/replayed-event mutation controls cover both restore directions; disabling only the invariant makes all four fail in an independent in-memory check.

Attempt 15 remains byte-preserved evidence for the preceding fixture version. The current validator rejects its old request observations/fixture bindings; it is not substituted for the selected hardened capture.

The 2026-10-05 re-derivation keeps attempts 01–17 byte-for-byte. The probe records an unsupported contract type instead of substituting the server query envelope. Post-upgrade restore requires a retained-floor stream, and a compatible label requires that floor to survive. Unit validation passes 116 controls. A fresh matrix was not captured, so final verification remains blocked on the quiet shared-resource interval and on the absence of a capture under the retained-floor rule. Qualification, usability and owner acceptance remain false.

The 2026-10-06 port correction holds all six sockets through startup preparation and keeps sidecar ports reserved until application readiness. CLI children require releasing their sockets before they bind; an observed address collision therefore triggers at most three attempts with fresh port reservations. Each failed attempt retains its receipts and stops only its owned processes. Scheduler retries verify the invocation label before any container removal. Cancellation and termination failures remain nonpassing. The strict writer-quiescence assertions are unchanged: a failed application launch retained inside a restore case still prevents that case from validating. The nine added controls pass, bringing the suite to 125. The independent published archive preflight also passes all 14 packages. Attempt 18 retains the earlier Docker blocker; attempts 01–17, selected historical results, acceptance and sprint status remain unchanged.

The later 2026-10-06 resume captures attempt 19 after Docker availability was restored. Its full package/source topology exercises the corrected startup ordering. Invocation exit 1 is the required result for seven incompatible dispositions; offline validation exits 0. The report then selected attempt 19, with an exact copy of its scenario results. All 1,439 snapshotted attempt-01–18 files remain unchanged, and the selected manifest's protected-before/after hashes match. Current [check receipts](current-checks.json) distinguish this valid investigation from prior rejected captures. P1R usability, qualification, named owner decisions, rollback policy and downstream readiness/state remain unchanged.

The completed review applies R01–R07: verified removed/absent scheduler retry IDs are retired from final runtime ownership; shared snapshots bind complete discovery and binary receipts; protected inventories match independent workspace hashes; owned processes bind executed PIDs and terminal outcomes; malformed evidence yields `INVALID`; startup-hook/profiler injection variables are cleared; and a real `wait_http` startup-exit control proves healthy nodes retain later output and complete final lifetimes. The recovered-scheduler packet and resealed omission/value/hash/process controls pass.

The parent reran all 135 controls successfully in 40.650 seconds, then captured attempt 21: 17 scenarios/72 cases, 2,845 assertions, seven incompatible dispositions, 2,333 command receipts and 1,100 owned processes. Invocation exit 1 is expected; independent offline validation exits 0. All four owned containers and scratch were removed, all owned processes stopped, cleanup errors are empty and all six shared snapshot entries match. The parent confirms all 1,587 protected/historical snapshot files through attempt 19 are unchanged. Attempt 20 retains its scheduler forwarding-500 startup failure and full owned cleanup/shared preservation. The report now selects attempt 21; all sealed attempt packets remain unchanged, and qualification, usability, acceptance, rollback policy and downstream states remain unchanged.
