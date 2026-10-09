# Corrected published execution c497b88cb2e5436ebddb37e33f1fcb64

This fresh invocation supersedes the historical [execution 325e3c9327df4810941981b5882e3a70](../execution-325e3c9327df4810941981b5882e3a70/README.md), whose sealed bytes remain unchanged. The [run](run.json), [prepare](prepare.json) and separate [independent validation](validate.json) all exited 0. The new packet is valid; technical qualification, owner acceptance and P1R usability remain false.

The nineteen measured scenario/addition receipts contain **163 cases, 5,522 checks, 5,412 passed and 110 failed**. [Every nonpassing case](nonpassing-case-index.json) remains explicit.

| Lane | Execution | Compatibility | Passed / attempted | Failed |
| --- | --- | --- | --- | --- |
| provenance | passed | compatible | 33 / 33 | 0 |
| legacy-metadata | failed | incompatible | 22 / 24 | 2 |
| metadata-read | failed | incompatible | 44 / 48 | 4 |
| metadata-write | failed | incompatible | 310 / 312 | 2 |
| full-replay | passed | compatible | 270 / 270 | 0 |
| snapshot-tail | passed | compatible | 270 / 270 | 0 |
| retained-covered | passed | compatible | 270 / 270 | 0 |
| retained-uncovered | passed | compatible | 540 / 540 | 0 |
| missing-event | passed | compatible | 540 / 540 | 0 |
| invalid-evidence | failed | incompatible | 564 / 570 | 6 |
| query-wire | failed | incompatible | 360 / 420 | 60 |
| projection-wire | failed | incompatible | 74 / 78 | 4 |
| mixed-api | failed | incompatible | 927 / 956 | 29 |
| checkout | passed | compatible | 704 / 704 | 0 |
| post-upgrade-restore | passed | compatible | 47 / 47 | 0 |
| pre-upgrade-restore | passed | incompatible | 44 / 44 | 0 |
| failure-cleanup | passed | compatible | 115 / 115 | 0 |
| reminder-recovery | passed | compatible | 131 / 131 | 0 |
| logical-event-evolution | failed | incompatible | 147 / 150 | 3 |

All three actual package roles independently passed their archive/signature/graph/physically-loaded-binary checks in nine isolated Release consumers. The three Debug/source consumers remain separate. [Retained source consumer graphs and identities](source-consumers/evidence.json) and [copied loaded binaries](source-loaded-binary-map.json) preserve actual loaded hashes.

The eleven paired checkout cases measured 704/704 checks against corresponding actual candidate operations, retaining normalized semantics and persisted coordinates. Predicate source bytes are retained once per receipt and bound to [the executor closure](executor-source.json), SHA-256 `04ca7c61e729ff7154472bd1a6a9fcdc4b4073b940db2aa3f4530abd6f1e6a38`. Importers independently rederive probe checks, required coordinates, wire fields, inventories and exact sidecar configuration bindings.

Supported metadata writes restart the actual selected writer and rehydrate 13, rechecking retained floor, original event/snapshot hashes and the second tenant. Strict restore uses a real Redis backup and fresh database. Pre-upgrade restore remains incompatible containment with RPO-zero false. All owned synchronous/long-running command trees and exact invocation-labelled containers receive repeated cleanup; shared resources stay unchanged.

Security controls recognize only the actual expected fence/proof denial. Unexpected stale transport errors and the actual trusted-effect denial-audit-unavailable exception retain bounded type/message diagnostics, measured failures and after-inventories. Nonnull IdentityAdmissionProof wire loss is incompatible. No authority or audit stub grants acceptance.

Reminder recovery measured 131/131; both initial and restart convergence require submitted=0, and natural sequence 13 must complete within the finite deadline before manual callbacks. [The preceding operational smoke](verification/pre-canonical-smoke/README.md) also retains all eleven checkout cases and unknown-operation 400 rejection with unchanged persisted inventories.

The selected logical-event-evolution addition has no registered manifest. Its check is bound to the retained Domain readiness response, and all three cases remain incompatible. Bounded legacy hydration and Dapr application-envelope readback confer no registered evolution execution.

[Focused verification](verification/focused-tests.json) ran 115 tests successfully, and [diff check](verification/diff-check.json) passed. [Eleven resealed tooling negative controls](negative-controls.json) reject failed-check flips, contradictory predicate operands, invented inventories, removed runtime configurations, executor/source/configuration substitutions, missing directions, forged wire fields and logical registration at import and independent validation. They confer no operational qualification.

[The private negative-control driver diagnostic](verification/negative-control-driver-diagnostic.json) preserves its initial serialization helper naming error and corrected successful tooling retry. Persistent executor/importer bytes and the sealed packet did not change; all eleven mutation trials and final revalidation passed.

[The Aspire baseline](verification/aspire-baseline.json) built with zero warnings/errors but exited 2 (AppHost 134) for absent nested Tenants host projects. Neither nested dependency was initialized.

[The preserved failed invocation](../failed-execution-6f89354956c24577ae1404cddb78bde9/README.md) recorded a first cleanup OSError followed by a clean retry during an otherwise successful source restore. Its original errno remains unknown. [Private diagnostic trial summaries](verification/ownership-diagnostics.json) retain the unreproduced result; the final source records bounded operation/type/errno and exact owned identity while preserving every nonempty error as refusal. No earlier failed receipt is imported here.

[The next preserved failed invocation](../failed-execution-ad19b44dd4d44796b09e2f62c9388046/README.md) demonstrated ProcessLookupError (errno 3) during procfs ownership observation after valid inventory output. The narrow final guard treats a disappeared procfs stat as absent; permission and other read failures still propagate. Both failed invocations retain 7/7 final cleanup and their original sealed bytes.

[The early preserved failure](../failed-execution-90b6f9b1ad3b41feabd670f273d0a027/README.md) retained signal-owned errno 22 for an exited root. The final guard skips only absent/reused identities before opening a pidfd, and only a newly proven disappearance/reuse after an EINVAL opening race. The kernel-handle identity recheck remains mandatory; matching-live EINVAL, permission and other failures still refuse cleanup.

[The completed execution with refused preparation](../execution-b944559fd0f3413696db48bcfc585f52/README.md) preserves all original receipts and its sealed JSONDecodeError refusal packet. The final parser retains failed startup retries and requires actual successful JSON HTTP responses for status/readiness binding. [Private tooling-only rederivation](verification/prior-execution-tooling-rederivation.json) checked all 163 original cases against their unchanged retained closure before this fresh invocation; no prior receipt is imported.

[Accepted inputs](owner-inputs.json) and [pending decisions](owner-decisions.json) preserve rollback=null, Projects AD-17 mutation freeze/forward recovery, pending EventStore/Builds/Solution/Test decisions and same-baseline conformance. Candidate nuget.org 3.115.0, comparison-only 3.70.1/3.110.0, Redis/Dapr 1.18.4 and candidate untagged Builds provenance remain the exact approved selections; workspace Builds 4.30.0 and all downstream gates remain unchanged.

[Planning observations](planning-observations.json), [preserved archives](archive-preservation.json), [raw commands](commands.json), [configurations](configurations/), [receipts](receipts/) and [safe diagnostics scan](safe-diagnostics-check.json) retain exact observations. Credentials and RDB dumps stayed in owned scratch and were destroyed. This directory and its packet have independent SHA256SUMS.
