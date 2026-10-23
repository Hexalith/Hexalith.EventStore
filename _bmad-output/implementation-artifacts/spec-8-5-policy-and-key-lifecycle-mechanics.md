---
title: 'Story 8.5: Policy And Key-Lifecycle Mechanics'
type: 'feature'
created: '2026-10-23'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '6a0b6441f6d78a3007678a1d0ec336e9cb8ff961'
story_key: '8-5-policy-and-key-lifecycle-mechanics'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-5-lifecycle-protocol-amendment.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The completed core/router lack policy selection and durable key mechanics. Stale authority, ambiguous reservations or cached keys could bypass erasure.

**Approach:** Add policy, Dapr key-state coordination, typed resolution, leases, cache, resilience and audit, verified through faults and real sidecars.

## Boundaries & Constraints

**Always:** Follow shared-authority §§9–10/13–15 and the approved successor protocol. Preserve cancellation, zeroing, isolation and monotonic epochs/watermarks. Enroll source-generated types explicitly. Proof is commands, counts, source SHA and CI link.

**Never:** Guess protocol fields; rewrite approvals; change Azure/Server/domain/interaction behavior, fixtures, release inventory or packability; persist events; automatically delete orphan keys; claim production custody or G5.

**User decision (2026-10-23):** Apply the narrow amendment, then implement in stages. Use two bounded catalogs, full lease-drain waits and explicit cache-clearing evidence. Keep orphan keys `Reserved` with reconciliation `Quarantined`. Approve exact amendment bytes before affected implementation.

## I/O & Edge-Case Matrix

| Scenario | Input / state | Expected behavior | Failure |
|---|---|---|---|
| Selection | Attributes, policies, serialized JSON | Deterministic canonical paths; no selection creates no key | Ambiguity fails before key creation |
| Reservation | Current lifecycle/fence/generation | Atomic Reserved record, indexes and phase before protected output | Reconcile ambiguous writes |
| Read | Current erasure state and lease | Typed resolution; cache supplies an owned copy | No stale or partial plaintext |
| Invalidation/rotation | Indexed membership and epochs | Drain leases/cache; preserve immutable payloads | Uncertainty withholds completion |
| Recovery | Crash, cancellation, orphan | Durable phase recovery; retain quarantined material | No blind replay or automatic deletion |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Contracts/Security/`: preserve policy/erasure/completion signatures.
- `src/Hexalith.EventStore.PayloadProtection/`: reuse bounded JSON, pointer/AAD codecs, material generation and zeroing observers. Core/router resolvers lose denial/deletion (DW-516); preserve `Interaction*`.
- `tests/Hexalith.EventStore.PayloadProtection.Tests/`: reuse fixtures/observers; preserve vector ownership.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs`: isolation pattern only, not PF-02 proof.

## Tasks & Acceptance

**Execution, in dependency order:** First six tasks use paths relative to `src/Hexalith.EventStore.PayloadProtection/`; keep one type per file.

- [ ] `PersonalDataPolicyRegistry.cs`, `PersonalDataDiscovery.cs`, `PayloadTypeRegistry.cs`, `ErasureStateRegistry.cs` — implement §9 ordering, metadata/converter correlation, ownership and epoch checks.
- [ ] `PayloadProtectionStateKeys.cs`, `PayloadProtectionStateCodec.cs`, `PayloadProtectionBackend.cs`, `PayloadProtectionScopeRegistry.cs` — implement closed records, bounded catalogs, enrolled roots, opaque SPI and typed failures.
- [ ] `DaprPayloadProtectionStateStore.cs`, `PayloadProtectionCapabilityProbe.cs` — prove strong reads, ETags, transactions, unchanged-value CAS and TTL.
- [ ] `KeyReservationCoordinator.cs`, `KeyLifecycleCoordinator.cs`, `PayloadProtectionLeaseCoordinator.cs`, `PayloadProtectionReconciler.cs` — implement indexed reservation/completion, rotation, invalidation, restore and recovery under the resolved protocol.
- [ ] `PayloadKeyResolution.cs`, `PayloadKeyCache.cs`, `PayloadBackendExecutor.cs` — adapt core/router typed failures; implement leased single-flight, zeroing, retry/breaker bounds.
- [ ] `PayloadProtectionDiagnostics.cs`, `PayloadProtectionAuditRecord.cs`, `Hexalith.EventStore.PayloadProtection.csproj` — extend closed diagnostics and centrally governed dependencies/test visibility.
- [ ] `tests/Hexalith.EventStore.PayloadProtection.Tests/PolicyLifecycleTests.cs`, `KeyStateProtocolTests.cs`, `KeyCacheResilienceTests.cs` — cover the matrix, assigned vectors, fault boundaries, concurrency, cancellation and no-leak sentinels.
- [ ] `tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests/`, `.github/workflows/payload-protection-lifecycle.yml` — add isolated PF-02 qualification with persisted Dapr end-state assertions and zero skips; preserve the existing core lane.

**Acceptance Criteria:**

- Given malformed policy/state or changed authority, when admission runs, then it fails before key creation or payload output.
- Given transient failure or concurrent cancellation, when retries/cache/breaker run, then §10 bounds and every owned-buffer clearing obligation hold.
- Given restart or lease/index uncertainty, when lifecycle work resumes, then irreversible state cannot regress and completion remains withheld until proven.
- Given the qualified component, when fault and concurrency suites run, then persisted transactions, fences and lease expiry satisfy PF-02; mocks cannot replace this result.

## Implementation Notes

- 2026-10-23: The interactive user approved the spec and exact amendment with “approve and continue”. The amendment normative digest is `a3a6a3bd83e728bea19b9ff50303ff8407b2362225e3e1b0935be8f551ba8c3a`; its effective authority digest is `f82cdf06f748de70326be063e0e849627e42542d69fd4e22ef9eed372f6de22b`. The frozen intent and normative amendment bytes are locked. Implement sequentially, without remote operations. Preserve the separate interaction-occurrence implementation.
- 2026-10-23: Required Aspire baseline completed before source changes. `aspire start --non-interactive --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json` exited 0; `aspire describe` identified the selected host resources; `aspire wait security-chpcestv --non-interactive --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --timeout 60` and the same command for `eventstore-eqwhamrm` both exited 0 and reported healthy. `aspire stop --non-interactive --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` exited 0 and confirmed shutdown before builds.
- 2026-10-23: Foundation stage implemented policy registration/discovery, explicit metadata/property correlation, erasure ownership/epoch observations, enrolled roots, exact state keys, all closed durable schemas and their source-generated JSON context, canonical record integrity, opaque backend results, typed state failures, and a qualification-gated Dapr transaction primitive. Added focused policy/state/buffer tests and an isolated PostgreSQL-v2/Dapr-1.18.7 startup-capability test. Added the separate lifecycle workflow and extended only test-lane classification/documentation; the existing core workflow, release inventory, packability, frozen fixtures, `Interaction*`, public Contracts and Server source remain unchanged.
- 2026-10-23: Sequential implementation handoff. All task checkboxes remain open because end-to-end lifecycle closure has not been implemented. Next implement the bounded member/work catalog enumeration/mutation primitives; registration/renewal/retirement; indexed create intent/reservation/completion; full monotonic drain waits, acknowledgements and indexed invalidation/deletion; KEK inventory rotation; restore admission; claim-fenced bounded recovery/quarantine. Then implement typed core/router resolution (DW-516), leased single-flight cache/zeroing, retry/breaker execution and closed diagnostics/audit admission/emission, followed by the remaining fault/concurrency/no-leak vectors and complete real-sidecar PF-02 qualification. The current live test proves startup primitives only. No CI run/link exists and no remote operation was performed.
- 2026-10-23: Protocol detail requiring resolution before affected scope-wide coordinator work: the invalidation barrier must enlist `ReconciliationV1`, whose approved schema still requires `keyRef:ulid` (and plain `dekVersion:u32`, including zero). `OperationV1` permits an empty scope-wide reference. The foundation preserves the approved schema and does not invent a sentinel key reference for an empty scope. This does not block the independent catalog/cache/resilience work. No approval bytes were edited.
- 2026-10-23: The exact proposed scope-wide correction is recorded outside the normative range under “Pending correction” in the amendment. Its proposed revised normative/effective digests are `aed1eb3f6cd438ff0691db5d449dab4d9e5186e3a0dc4d2d7c0eecc56b9fbe7a` / `08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6`. An asynchronous user decision is pending. Continue independent approved tasks; hold only code that depends on allowing empty reconciliation key references. Do not treat elapsed time or the preselected answer as approval.

- 2026-10-23: The user answered “Approve scope-wide clarification (Recommended)”. The exact proposed paragraph is incorporated into amendment section 4. Current approved normative/effective digests are `aed1eb3f6cd438ff0691db5d449dab4d9e5186e3a0dc4d2d7c0eecc56b9fbe7a` / `08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6`; scope-wide coordinator work is now authorized. Earlier pending notes and digests are historical.

## Spec Change Log

## Review Triage Log

## Verification

Stories 8.3/8.4 are done; 8.5 is in progress. The foundation below is implemented; the remaining lifecycle tasks and full PF-02 qualification are incomplete. Source identity is the uncommitted working tree based on `6a0b6441f6d78a3007678a1d0ec336e9cb8ff961`; no CI run/link exists.

- Base identity, amendment digests (Python/Node) and five structure checks — PASS; `git diff --check` — PASS.
- `node scripts/payload-protection/verify-golden-vectors.mjs` — PASS, V001–V003.
- `python3 scripts/payload-protection/verify-golden-vectors.py` — PASS, V001–V003.
- Build/run affected projects individually; run existing core/invariant/package guards and new real-Dapr tests. Record commands/counts and CI link. Establish the required Aspire baseline before code edits.

Foundation verification (2026-10-23):

- `dotnet build src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1` — PASS, zero warnings/errors. The initial `--no-restore` attempt reported `NETSDK1004` because `project.assets.json` was absent; the normal restore/build resolved it.
- `dotnet build tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1` — PASS, zero warnings/errors.
- `dotnet tests/Hexalith.EventStore.PayloadProtection.Tests/bin/Debug/net10.0/Hexalith.EventStore.PayloadProtection.Tests.dll` — PASS, **689 passed, zero failed/skipped**, including the new foundation tests. The preceding focused run of `PolicyLifecycleTests`, `KeyStateProtocolTests` and `DaprStateStoreBoundaryTests` passed 46 cases; the later unsigned-reconciliation-schema case is included in the final 689.
- `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 dotnet tests/Hexalith.EventStore.PayloadProtection.Tests/bin/Debug/net10.0/Hexalith.EventStore.PayloadProtection.Tests.dll -method '*V029_InertPlatformNormalizer_FailsClosedThroughCoreWriteSeams'` — PASS, **1 passed, zero failed/skipped**.
- `dotnet build tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1` and `dotnet tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests.dll` — PASS, zero build warnings/errors, **1 passed, zero failed/skipped**. Inspected persisted conditional-create, unchanged/stale CAS, atomic rollback/commit and TTL end states through real Dapr 1.18.7 and the digest-pinned PostgreSQL v2 component. A repeat run exposed a fixture bootstrap-readiness race; changing `pg_isready` to explicit TCP repaired it, and the final run passed in 4.698 seconds. This is not full lifecycle PF-02 or production custody evidence.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1` — PASS, zero warnings/errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method '*Test_projects_are_classified_into_release_live_advisory_or_deferred_lanes' -method '*Payload_protection_workflow_preserves_the_complete_blocking_vector_lane' -method '*Release_manifest_projects_exist_and_entries_are_unique' -method '*Non_manifest_src_projects_cannot_produce_release_packages'` — PASS, **4 passed, zero failed/skipped**. The earlier broad `-class '*ReleasePackageManifestTests' -class '*ContractsPackageDependencyTests'` run found the new live project unclassified; after adding its workflow/classification it was stopped with SIGINT (exit 130) instead of continuing stale broad checks. Its owned subprocess was stopped; no active test/tool session remains.
- `actionlint .github/workflows/payload-protection-lifecycle.yml` — PASS. Existing core workflow remains unchanged.
- Both golden-verifier commands above still PASS V001–V003. Independent Python framing pins the new field-hash fixture to `272d7afa5ed61ae1084833186cfe73a314d2430bf1ea731ad19bb50ad1a1efe3`; .NET matches it and rejects per-field corruption while accepting JSON member reordering.
- Amendment normative/effective digests still reproduce `a3a6a3bd83e728bea19b9ff50303ff8407b2362225e3e1b0935be8f551ba8c3a` / `f82cdf06f748de70326be063e0e849627e42542d69fd4e22ef9eed372f6de22b`. `git diff --check` — PASS.
