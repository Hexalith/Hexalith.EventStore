# Story 6.4 parent runtime correction handoff

The authorized correction and finite verification pass has ended. Story 6.4 and sprint remain **in progress**: Release Live and the separate formal acceptance gates remain open. The original adapter-slot failure, all 90 historical Debug Live failures, and all three historical Release replica-startup failures pass in their corresponding fresh complete cohorts.

| Complete native cohort | Passed | Failed | Declared skips | Errors / not-run | Exit | Seconds |
| --- | ---: | ---: | ---: | --- | ---: | ---: |
| Debug DomainService | 978 | 0 | 0 | 0 / 0 | 0 | 205.602 |
| Debug Live | 194 | 0 | 1 | 0 / 0 | 0 | 1288.678 |
| Release Live | 193 | 1 | 1 | 0 / 0 | 1 | 1372.603 |

Each complete native cohort was invoked once. The retained failed Debug preparation launched no native test; its distinct successor `live-debug-full-native` is the single actual complete Debug invocation. The existing worker-helper skip is unchanged. No failed complete cohort was retried, and the rest of the historical qualification queue was not repeated.

## Changes and evidence

Six test/fixture files changed: adapter-worker teardown proves return of all sixteen static slots; OQ8 private discovery accepts the existing strict optional HotReload override; failed fixture initialization emits a separate failure receipt without hashing deleted runtime files. Existing assertions and recovery budgets remain intact. Production source, dependency declarations and production configuration are unchanged. The [implementation handoff](correction-handoff.md) explains the retained native diagnoses, focused checks, preliminary failures and source changes.

The [exact command ledger](parent-command-ledger.json) records all fourteen chronological producer, focused, preparation and complete-run command vectors, native commands, recorded environment overlays and hash-bound packet references. [Producer/execution packets](chronological-frame-index.json) retain source input inventories, effective restored dependencies, runtime hashes and module revisions. Raw effective assets and dependency/runtime configuration bytes are archived by content hash under `asset-bytes/`.

The source base is HEAD `7842a1fb81aa329ecac6271d885b959b40522767` plus the six explicitly recorded test-source changes. All three complete executions used the same frozen 9,644-input source inventory with stable assets/runtime bytes. The successful targeted Debug DomainService, Debug Live and Release Live producers have zero warnings/errors. Focused adapter tests passed 4/4, fixture checks 10/10, the original replica methods 3/3, and the final preserved-profile OQ8 check 1/1; preliminary failures remain separately recorded.

Runtime identities include SDK 10.0.401, .NET 10.0.12, regular daprd 1.18.2 and OQ8 daprd 1.18.7. The test frames explicitly disable HotReload in private configurations and use task-owned controls. Redis 6.2.21 image/settings match the historical profile. OQ8 PostgreSQL and control images, binary hashes, actual process arguments, generated configurations, reachable advertised scheduler endpoint and shadow-copy hashes are retained in the controls/producer receipts. Debug owns Debug/source test outputs while OQ8 consumes freshly produced Release/package entry hosts; receipts expose the inherited mixed runtime provenance. These test profiles do not establish unmodified production-profile qualification.

## Remaining runtime failure

Release's sole failure is `IdempotencyAdmissionOq8PostgresqlTests.ProductionMatrix_IndependentProcessesPreserveAuthorityReplayExpiryAndLeakageInvariants`, at its initial writer await (test line 44, fixture `SubmitAsync` line 197), on the unchanged 30-second HTTP timeout. The [sanitized diagnostics](live-release-full-oq8-diagnostics.json) show private `sqlite/v1` discovery host-registration context deadlines on all three sidecars. The underlying cause remains unproven; no resolver, profile, assertion or timeout change was made to pursue it.

Later authority/replay/expiry/leakage and persisted-state assertions in that method were not reached and remain unproven for this Release invocation, despite runner `not-run=0`. Neither the focused/Debug OQ8 passes nor the 49 projection observations in each complete Live frame replace those assertions. Independent checks confirm all fifteen Current rows and seven tail rows meet the original numerical budgets in each frame; Release acceptance remains **failed/unproven**.

## Independent parent checks

- [Native audit](parent-native-audit.json): raw XML/counts, unchanged original case inventory/skips, single native invocation per authorized slot, stable source/assets/runtime, and Current/tail numerical budgets.
- [Completed Release audit](parent-completed-release-audit.json): all historical failed methods now pass; consumed producer hashes and actual OQ8 shadow-copy hashes match independent inputs; Release OQ8 invariants remain unproven.
- [Preservation audit](parent-final-preservation-audit.json): all 11,962 immutable protected files unchanged, original wrapper bytes preserved as a prefix, six permitted test-source changes, unchanged HEAD/index and clean `git diff --check`. The approved normative digest remains `78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711`.
- [Physical resource audit](parent-final-resource-audit.json) and [all-frame supplement](parent-all-runtime-frame-resource-audit.json): recorded task resources absent, baseline shared containers still running under the same identities, inotify limits unchanged. Checks were read-only; no later port owner was disturbed and no unrecorded PostgreSQL volume identity was invented.
- [Diff audit](parent-diff-audit.json): original assertions and recovery timing preserved; no production, dependency or policy edits.

## Separate formal acceptance requirements

1. **P1R:** the [scoped user approval](../p1r-user-approval-20261017/README.md) already covers 77 files / 98 historical locations. Formal authenticated named-owner role authority, reviewed category dispositions, and an exact-boundary tracked-ingestion scan with negative controls remain required. Policies and historical evidence are unchanged.
2. **Stories 9.1/9.2:** both remain backlog. The Phase-4 risk matrix and its validator are still absent; no corrective-work authorization or assurance-control completion is inferred.
3. **Authenticated CI:** a dedicated transition result must be retrieved under authenticated CI identity and bind the exact intended acceptance candidate, workflow digests, validator identities and required fixtures/controls. These local receipts are not that result. No remote transition was dispatched.
4. **Owner assurance / AD-12 / NFR8:** the effective authenticated human owner-role registry and required achieved assurance remain necessary, including the applicable single-maintainer attestation or independent distinct-human assurance. The validator must establish an exact-subject attestation at least 24 hours after the last authored change. No role authority, assurance eligibility, timer or attestation is invented.

See the [formal requirements receipt](parent-remaining-formal-requirements.json) and retained [authenticated assurance requirements](../qualification-20261017-parent/authenticated-assurance-requirements.md). The earlier normative approval does not approve runtime acceptance.

No staging, commit, push, publication, deployment or maintenance cutover occurred. No further full-cohort invocation is authorized by this finite pass.
