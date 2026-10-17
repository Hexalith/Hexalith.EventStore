# Story 6.4 review correction pass 1 qualification

The corrective implementation is complete. Formal acceptance and exact whole
section 10 qualification remain open. The finite independent verification queue
has ended; the [closing audit](review-1-source9-closing-audit.json) records final
native outcomes, runtime cutoffs, remaining failures and owned-resource cleanup.
The [gate packet](review-1-corrections-gates.json) retains every actual command,
DLL/dependency/package identity, failed attempt and source-arrival flag.

Baseline remains `e17e7294f540ebba42fe25192998689f0ecd89c1`. Successive external
commits, shared-catalog updates, test-policy edits and output rewrites were
preserved. Historical results are never reassigned to a later checkout or DLL.
Approved normative body SHA-256 remains
`78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711`.
No source commit, dependency declaration edit, new production restore/build,
package producer or host restoration was performed by this agent after the
external Source8 catalog/output change. Actual external dependency adoption is
explicit in the per-run profiles.

The [historical executable coverage index](continuation-10-coverage-index.md)
maps W0–W6, the twelve section 6 durable boundaries and section 7 preservation.
The owning corrective regressions below supplement those assertions. Their
per-run enrollment and observed results remain bound to the captured binaries.

| Review finding | Correction and owning regression |
| --- | --- |
| R1-B1 / R1-E1 | The first immutable manifest now carries its original request digest. Reference interruption recovers those same canonical bytes, including the accepted bare private format. `GuardedPreparation_InterruptedReferenceReusesTheFirstImmutableManifest` and `GuardedPreparation_FirstWriteLostResponseRetainsItsRequestBindingAndNeverPreparesAgain` cover write/readback interruptions and changed-input rejection. |
| R1-B2 | Cleanup retains the immutable reference as a no-reprepare tombstone. `GuardedPreparation_CompletedCleanupRetainsEvidenceThatFencesDelayedStage` rejects a delayed Stage before preparation. |
| R1-E2 | Superseded unreserved work is claimed under its original revision/lease and settled only from exact scope/catalog/MessageId completed-prefix proof. `GuardedRetry_SupersededUnreservedWorkRequiresItsExactCompletedMessageId` and `GuardedRetry_OwningSchedulerSettlesOriginalWakeupAfterNewerHeadCompletion` preserve wrong-ID rejection and next-activation convergence. Prepared superseded work retains its original private evidence for reconciliation. |
| R1-B9 | The shared bounded finalization token begins after read-only history admission. `GuardedPendingRecovery_SlowReadOnlyHistoryCannotConsumeItsFinalizationDeadline` verifies convergence after a simulated 31-second read without redispatch. |
| R1-B4 / R1-B7 | Streaming guarded-body admission enforces absolute and derived byte caps before typed graph materialization and rejects root/nested duplicate evidence with the actual serializer comparer. `GuardedBody_RejectsByteBudgetBeforeMaterializingTheMalformedDigestGraph` and `GuardedBody_RejectsDuplicateEvidenceWithTheActualWebNameMatching` cover over-budget and ambiguous bodies. Historical schema1 behavior remains preserved. |
| R1-B6 | Owning selection proof rejects ambiguous root/nested owner, generation, version and freshness metadata. `SelectedOwner_RejectsAmbiguousPersistedMetadataBeforeAuthorizingReceipts` covers seven corruptions before receipt access or mutation. |
| R1-B8 | Guarded dispatch and replay-part HTTP serialization use the same fixed Web profile as readable-wire admission. `GuardedTransport_UsesTheAdmittedWireProfileDespiteConfigurableDaprStateSerialization` checks actual sent event/array bytes under enlarged configurable state serialization. |
| R1-B3 / R1-B10 | Proposed and existing anchors exceeding 4096 bytes reject before mutation; the maintenance response returns the proven incarnation. `SourceBootstrap_EnforcesTheProposedAnchorBudgetBeforeAnyMutation` covers 4096/4097 and permitted long identifiers; `SourceBootstrapResponse_ReturnsTheActorsExistingProvenIncarnationForQualification` covers response propagation. |
| R1-P1 | Source metadata measurement uses the pinned SDK's default `Utf8JsonWriter` with configured typed serialization. `SourceMetadata_UsesTheSdkWriterEncodingForInspectionAndBootstrap` covers inspection, proposed anchor and already-anchored bootstrap. Relaxed/indented stream bytes 1132 versus SDK 4298 reproduce the defect; legacy bootstrap input 759/3992 is admissible but its 4298-byte proposal rejects before mutation. Separate retained red/green logs preserve exact commands and profiles. |
| R1-V1 | `SourceMaintenance_AttributableAuthenticatedNonAdministratorCannotChangeAnyEvidence` exercises all three maintenance actions with an attributable authenticated non-admin, asserting 403 and unchanged source/delivery/admission/store evidence before service access. |
| R1-V2 | `RecoveryBoundReplay_ActualSdkMappingsReuseOnePersistentManifestAcrossRetryAndFreshHosts` uses actual authenticated `UseEventStoreDomainService` routes and persistent production batch store across fresh hosts. Stage/commit/verify retry cases preserve one preparation, immutable manifest/model/count/index outcomes and the cleanup tombstone. |
| R1-Q1 | The supported Redis-port override now also reaches all six raw backend diagnostic connections through an internal fixture endpoint. The five-file Live-only correction preserves every assertion and OQ8 source. Its original sourceSnapshot3 build remains historical; each later Live run carries its own actual assembly, dependency and host identities. |
| R1-P2 | `GuardedBody_LargeSupportedSerializerDepthDoesNotPreallocateUnvisitedContainers` proves that the pinned public SDK accepts a finite `MaxDepth=1,000,000` profile and the owning store reads a valid tiny guarded body, both markers and no writes. The red header allocation is 8,016,800 bytes for 1738 bytes. Fixed known-path storage preserves configured reader depth; the earlier green profile allocates 16840/17280 bytes for 1737/1941-byte bodies, including a 96-level unknown value. These are normal bounded tests, not OOM probes. Red and green snapshots remain distinct. |


R1-B5 unknown fields/raw provider padding remains **unverified**. The supported
actor APIs expose typed state. The verified P1 SDK-writer correction does not
establish a raw-provider-body guarantee. No provider bypass, reflection hook or
second provider read was introduced. R1-Q2 remains a deferred, pre-existing
Domain fixture ordering race; its original failure, same-DLL narrow pass and
complete rerun are preserved without an unrelated fixture or production change.

## Retained qualification boundaries

Source4 at `0b38a562cd4fb6191a0c3c78a44b4c48d1f05390` passed all ten complete
local suites: 21,620 selected, 21,568 passed and 52 existing skips across Debug
and Release. The corresponding R4096 lane passed 5/5 with 15 observations, but
an external commit arrived during that lane. Its unchanged executed DLLs and
input-arrival flag remain historical. Source73 remains bound to its original
`008b83` source and actual binaries.

Source5 at `3dde45104b4be4eab8408cbaa0ce89b04a5d9052` refreshed affected source
builds, exact focused checks, complete Server Debug 5713/5688 passed/25 existing
skips, Domain Debug 978/978, Client Debug 1574/1574 and Contracts Debug
2358/2358. The changed AppHost class passed 12/12. Its focused Live lane passed
40/40 with intact 49 observations. Package inventory3 remains exactly its
original 14 archives, 16 embedded DLL hashes and 22 internal version pins at
`999.0.0-ci-test`; no later package production is implied. The incomplete first
package capture remains distinct from the coherent recapture.

Source7 complete Debug Live passed 192 selected, 191 passed and one existing
worker-helper skip, with 49 intact observations and strict uncontended A=1
numerical ceilings. The catalog changed during that lane; actual loaded test,
runtime and dependency bytes remained stable and its original guard flag is
retained. Later Release Domain 978/978 and Client 1574/1574 passed against their
exact original Source5 assembly/dependency bytes. These successful native runs
were not repeated for unrelated external input arrivals.

The Source8 initial blanket assumption that all outputs still belonged to
Source5/7 was superseded, not erased. The external producer changed restored
assets, Server/Contracts outputs and 72 Release host files against the new
catalog. Its exact argv and PID were not observed. The supported no-restore
Contracts test-only build copied 22 third-party reference DLLs from existing
root/package-cache bytes. That actual package-mode test closure is separately
bound; it is not the prescribed historical Debug/source build. The
[provenance audit](review-1-source8-external-output-provenance-audit.json),
[copy-local trace](review-1-source8-copy-local-and-runner-help.json) and finite
cutoff manifests preserve exact file, dependency and producer boundaries.

The old 746-file host archive and the later 2956-file pre-Contracts archive are
historical byte captures. Neither was restored. Contracts fixtures use isolated
package-consumer workspaces and immutable inventory3; all available current
hosts were captured once for the final Live cutoff. No coherent production
rebuild was performed to align revision metadata or erase external output drift.

## Finite Source9 independent results

These are disjoint selected runs and observation supplements, not an exact
whole section 10 gate or a latest-checkout acceptance claim.

| Captured target | Actual native result | Boundary |
| --- | --- | --- |
| Contracts Debug | 2357-case union: 2347 passed, 10 failed, zero skips | The exact authority method passed 1/1. Ten EventIdentity cases in four methods could not load source-built `Hexalith.Commons.UniqueIds, Version=3.122.0.0` from the recorded mixed closure; available package DLL was 2.30.1. Later externally rewritten Debug binaries were not retested or labeled failing. |
| Contracts Release | 2357-case union: 2324 passed, 33 failed, zero skips | Authority 1/1, EventIdentity 20/20 and remaining 2272/2272 passed. The changed 64-case governance selection had 33 stale-compiled-policy failures against externally rewritten publication read inputs. Its later current state is not inferred from that historical result. |
| Server Release first cutoff | 5719-case union: 5691 passed, 3 failed, 25 existing skips | The targeted 3-case lane had one broad-scanner failure at one external synthetic regression-source line. The remaining 5716 cases had two separate 100-second authenticated-command response timeouts. |
| Requested same-closure auth diagnosis | Zero native methods executed | A pre-native guard detected 24 externally rewritten Server DLL/PDB files. No retry against a different closure or recovery of unarchived bytes was attempted. Timeout cause remains unproven. |
| Server Release observation-only cutoff | 275/275 passed; all 73 observations intact | This later runtime is distinct from the failed Server cutoff. It retains exact workload/read/effect/byte/proof-visit ceilings, three SDK-writer profiles and both finite-depth allocation bounds. Actual green bodies were 1743/1941 bytes with allocations 16840/17280; original red body 1738 is separate. |
| Final Release Live | 192 selected, 190 passed, 1 failed, 1 existing skip | One captured Live test/dependency and both host-directory closure; full suite failed; all 49 observations retained and parent Current/tail numerical ceilings passed |
| Final R4096 Current | 5 selected, 5 passed, 0 failed, 0 existing skips | strict A=1 R4096 numerical audit passed, owned controls 0,0 |

The [independent parent observation audit](review-1-source9-parent-observer-numerical-audit.json)
is retained as an exact-byte supporting copy for the observer-only cutoff. It
does not qualify the earlier failed Server cohort or latest global state.

The [current scanner failure record](review-1-source9-current-scanner-failure.json)
reports only path/category/digest: one line in external
`SecretsProtectionTests.HistoricalLines.cs`. No usable credential value was
printed or tested. The [historical Source6 failure](review-1-source6-secret-scan-blocker.json)
reported 89 locations in 77 imported P1R evidence files under its older policy;
that count is not attributed to the later Source9 scanner. Passing native
package-authority checks under an external policy change do not establish owner
approval for the imported evidence.

The [final complete Release Live failure](review-1-source9-live-release-failure.json)
was a separate 15-second Dapr actor-call timeout in
`LegacyMigration_TargetRedirectInventoryAndDirectoryRemainProvenAfterRestart`.
It occurred on the first migration proof after the host/sidecar restart, before
the subsequent inventory/source/target/directory/replay persisted-state
assertions. The fixture warms an ETag actor after restart; that fact does not
establish the cause of this migration timeout. No full or narrow Live retry was
performed. The full suite remains failed despite its complete 49 cost/recovery
observations.

The two auth failures reached HttpClient's 100-second timeout while buffering a
TestHost response, before the expected accepted/routed assertions. The accepted
command path awaits additional services beyond the fixture router substitute.
The available evidence does not establish which await caused either timeout.
The failed 5719-case union remains authoritative for its runtime even though a
later 275-case observation-only supplement passed.

## Preserved failures, isolation and remaining gates

The packet retains initial fixture failures and their narrow verification
repair, the preliminary Client dependency-mode build failure, the first focused
Live log collision, interrupted Live attempts, namespace-only compilation
errors before the depth reproducer, exact red/green regressions, Domain fixture
race, count/discovery-only expectation corrections, temporary orchestration
pauses, incomplete package capture and every later native failure. No assertion,
skip, budget, source authority or production authorization was weakened.

The first complete shared-Redis Live attempt was interrupted after an unrelated
`FLUSHALL` reset persisted actor sequence and removed its writer marker. The
[sanitized diagnostic record](review-1-shared-redis-data-destruction.json) binds
that timestamp to owning source/marker evidence. A second isolated attempt
exposed the six diagnostic connections behind R1-Q1 and was also interrupted.
Neither partial run qualifies a complete suite.

Regular fixtures subsequently used the existing endpoint override with a
separately owned Redis 6.2.21 instance of the exact observed image, command and
selected persistence settings. The [owned profile](review-1-owned-redis-profile.json)
and final cleanup receipt identify only its owned container and volume. OQ8
retains its separate existing profile. The fixture's hardcoded
`docker exec dapr_redis` persistence diagnostic samples the shared container;
its result is not attributed to the isolated Redis instance. The actual final
regular Live runtime and both host directories have their own cutoff manifest.

Three acceptance boundaries remain distinct: the historical Source6 P1R scan
failure; recorded Source9 loader/governance/scanner/auth failures and unproven
same-closure diagnosis; and unexecuted exact whole/latest-head qualification,
authenticated sealed CI and owner P1R bulk-evidence landing disposition. The
owning P1R canonical-reconciliation spec still requires a bulk-evidence landing
disposition and records no new exemption/retirement approval. An earlier single
remediation exemption does not authorize this later bulk landing.

Parent step03 global acceptance remains open. Independent review is continuing under the user's explicit preserve-P1R/finish-independent-checks direction. Wrapper and sprint
status remain in progress. Local evidence does not perform AD-12/NFR8 transition,
production qualification, maintenance activation, deployment, package publication
or a source commit. Later external changes outside the captured runtime/read-input
closure remain unqualified; this finite verification pass does not chase them.

## Parent reconciliation

The [parent handoff audit](review-1-parent-final-handoff-audit.json) independently
checked all 15 Source9 native log/cutoff hashes, all 23 production source inputs,
the 14 immutable package archives and exact owned cleanup. The
[corrective-regression audit](review-1-parent-regression-coverage-audit.json) and
[frozen-matrix audit](review-1-parent-frozen-matrix-outcome-audit.json) retain
executed passing witnesses in their actual recorded frames. The separate
[full-Live numerical-only audit](review-1-parent-source9-failed-live-numeric-row-audit.json)
checks the 49 intact observations while requiring the exact recorded sole
migration failure; the whole suite remains failed. The
[R4096 numerical audit](review-1-parent-source9-r4096-numeric-audit.json) verifies
5/5 native cases and all 15 Current observations. These supporting checks do not
replace failed suites, latest whole-checkout qualification or owner disposition.

This parent documentation reconciliation is a separate delta from the agent's
closing addendum SHA-256
`8465b751f3413110d25167a338278f40613230616c49e38700279bcf395eb985`.
The native packet and closing audit are unchanged.

## Subsequent correction pass

Independent round 2 opened seven further projection corrections. They are now
implemented and have [focused R2 verification](review-2-parent-acceptance.md).
Every R1 native outcome above remains bound to its historical runtime; it does
not qualify the subsequently changed source. The R1 native packet and closing
audit were not modified by the R2 implementation.
