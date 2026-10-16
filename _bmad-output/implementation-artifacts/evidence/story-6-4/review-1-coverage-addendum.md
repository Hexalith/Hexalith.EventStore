# Story 6.4 review correction pass 1 qualification

Qualification is in progress. The [gate packet](review-1-corrections-gates.json)
binds each fresh command to actual checkout, configuration, package, runtime and
DLL identities. Baseline remains `e17e7294f540ebba42fe25192998689f0ecd89c1`;
actual HEAD is `0b38a562cd4fb6191a0c3c78a44b4c48d1f05390` plus the recorded
corrections. That external commit arrived before Debug build 6. Historical
source73 qualification remains bound to its original `008b83` checkout and DLLs.
Approved normative SHA-256 remains
`78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711`.

The [historical executable coverage index](continuation-10-coverage-index.md)
still maps W0–W6, the twelve section 6 durable boundaries and section 7
preservation. Fresh complete suites refresh those assertions; historical run
measurements are not reassigned to this correction.

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
| R1-Q1 | The supported Redis-port override now also reaches all six raw backend diagnostic connections through an internal fixture endpoint. The five-file Live-only delta preserves every assertion and OQ8 source; current Live outputs are rebuilt from sourceSnapshot3. Production and non-Live inputs remain unchanged from sourceSnapshot2. |
| R1-P2 | `GuardedBody_LargeSupportedSerializerDepthDoesNotPreallocateUnvisitedContainers` proves that the pinned public SDK accepts a finite `MaxDepth=1,000,000` profile and the owning store reads a valid tiny guarded body, both markers and no writes. The red header allocation is 8,016,800 bytes for 1738 bytes. Fixed known-path storage preserves configured reader depth; green cases allocate 16840/17280 bytes for 1737/1941-byte bodies, including a 96-level unknown value. These are normal bounded tests, not OOM probes. Red and green snapshots remain distinct. |

R1-B5 unknown fields/raw provider padding remains **unverified**. The supported
actor APIs expose typed state. The verified P1 writer-profile correction does
not establish a raw-provider-body guarantee, and no provider bypass, reflection
hook or second provider read was introduced.

The initial complete Server Debug attempt failed three fixture cases and is
retained. The cleanup substitute now targets the scoped original-work overload;
the actor test helper configures substitute forwarding only on a substitute,
preserving real Dapr invokers. Assertions and production authorization remain
unchanged. The narrow fixture/P1 lane passed 128/128 before a complete rerun.
The preliminary default/no-restore Client build also remains a failed dependency-
mode attempt; the explicit approved source-mode build and suite passed.

The first focused Live run passed 40 tests and released both controls, but its
outer/native log filename collision made numerical capture incomplete. The
separate future prefixes prevent that collision. A complete shared-Redis run
was interrupted after a proven `FLUSHALL` at 12:12:42 UTC reset an actor's persisted
sequence and deleted the writer marker. The [sanitized diagnostic record](review-1-shared-redis-data-destruction.json)
binds the broad-deletion timestamp to command identities before/after the reset,
the owning actor's later head 3344 and the recreated marker's different fixture
revision. Its partial 140/129 passed/11 failed summary is not a complete suite.
The first isolated run exposed R1-Q1 and was interrupted at a partial 22/18 passed/
4 failed. All owned test processes and wrapper controls were released; neither
interrupted attempt qualifies the complete gate.

Regular fixtures now use their existing endpoint override with a task-owned
instance of the exact observed Redis 6.2.21 image, command and settings. The
[actual profile](review-1-owned-redis-profile.json) records the settings comparison
and pending final cleanup. OQ8 remains on its separate existing profile. The
fixture's hardcoded `docker exec dapr_redis` persistence diagnostic still samples
the shared container; this is disclosed and the owned settings are independently
captured rather than falsely attributed. Cost ceilings, engine/settings, HotReload,
production code and repository topology were not changed for this isolation.

Earlier sourceSnapshot2 focused Server passed 570/570 and complete Debug Server
passed 5682 with exactly 25 historical skips; these retain their original runtime
identities. Domain 110/110 focused and 977/977 complete, Client 1574/1574 and
package-enabled Contracts 2358/2358 passed without skips. Actual Domain/Client
test-bin/dependency hashes are unchanged and their original sourceSnapshot2
bindings remain explicit. SourceSnapshot3 focused Live passed 40/40 and retained
all 49 intact observations and controls 0,0 after the endpoint repair. That helper
version remains historical after R1-P2.

SourceSnapshot4 contains the bounded-header correction and the finite depth
regressions. Its exact Debug/source Server build passes without warnings/errors;
depth 2/2 and exact focused Server 572/572 pass with zero skips. Complete Server,
fresh packages/Contracts, refreshed Live and Release qualification plus the
closing audit remain pending. Earlier exact Release/package builds and the
first 14-package inventory remain historical for changed Server outputs.

Parent step 03 acceptance and subsequent independent review remain open. The
wrapper and sprint stay in progress. Local qualification does not perform
authenticated sealed CI, AD-12/NFR8 transition, production qualification,
maintenance activation, deployment, package publication or a source commit.
