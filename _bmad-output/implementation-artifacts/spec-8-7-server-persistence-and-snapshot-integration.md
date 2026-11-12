---
title: 'Story 8.7: Server Persistence And Snapshot Integration'
type: 'feature'
created: '2026-11-06'
status: 'in-progress'
route: 'dispatch'
execution_disposition: 'dapr-kubernetes-successor-selected-8-12-runtime-capacity-gated'
planning_change_approval_date: '2026-11-08'
planning_change_approval_source: 'Interactive project owner: continue and approve'
planning_change_proposal_sha256: '4b618adbca7c6802e41b6da40dac6b8d085ab81875921d6bb1293d9ec3dccd68'
successor_planning_intent_id: 'S87-DAPR-K8S-20261108'
successor_planning_intent_sha256: '16e9e8b72f48725d41737da860e7a92a3e8ac236549e4cd7cd132bfe87a88c81'
successor_normative_status: 'qualified-owned-kind-revision-4-sdk-1.19.1-platform-gated'
successor_sdk_approval_date: '2026-11-12'
successor_sdk_approval_source: 'Interactive project owner: approve 1.19.1'
successor_sdk_version: '1.19.1'
successor_normative_sha256: '3489775e15bdc5b7ffad4a67f37c6372a23d50b213095e26bd45e1514ad285bd'
successor_effective_authority_sha256: '5d11b464555ac1f40fde3eadfd03c2a3f984f0770d594b30062a4cc641e34204'
predecessor_frozen_intent_disposition: 'historical-openbao-phase-retained'
approval_date: '2026-11-07'
approval_source: 'Interactive project owner: approve and stop'
approved_intent_sha256: 'da45c41e7994a306295cb5bf1f3d0d6d55fb0861c5d72156173a5940ae316f55'
review_loop_iteration: 0
baseline_commit: 'fd77eb18b548ca9c1f13b97014441a26d6179ee7'
planning_revision: '6ef8dcd4fc75e9bf20e403f2baf326e882a05d69'
story_key: '8-7-server-persistence-and-snapshot-integration'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-7-on-premises-backend-amendment.md'
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-08.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-12-dapr-kubernetes-cryptography-amendment.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-12-dapr-kubernetes-cryptography.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-payload-protection-capacity-fixes.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-dapr-crypto-error-outcomes.md'
---

**Authority precedence (approved planning change, 2026-11-08).** The owner said “continue and approve” for P1–P7 in the [Dapr/Kubernetes course correction](../planning-artifacts/sprint-change-proposal-2026-11-08.md). The frozen block below, its original digest/approval fields and OpenBao supplement are preserved as historical authority for the already-implemented first phase. The selected on-premises successor intent is `S87-DAPR-K8S-20261108` below; it replaces the OpenBao qualification approach only for subsequent planning. No new crypto schema/authority bytes or enabled Server wiring are authorized until Story 8.12's exact supplement/spec and real Kubernetes/gRPC qualification, latest-runtime, capacity and state/fleet/restore prerequisites pass. Do not resume the superseded OpenBao backend qualification merely because its frozen predecessor text is retained.

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Server has typed hooks and an interaction-specific engine, but lacks shared-engine registration, compatibility routing and lifecycle integration. Its Azure prerequisite conflicts with the owner's selected on-premises path; no replacement backend is qualified yet.

**Approach:** Qualify a Platform-owned OpenBao software-key backend, then connect shared-engine integration through existing actor hooks. Start with neutral Contracts/Dapr backend work; qualify the component and complete capacity work before Server host wiring.

**Owner decisions (2026-11-07):** Option 2 selects the on-premises Platform path; subsequent option 1 accepts software-managed OpenBao keys in place of hardware protection for qualification. Final plan approval, deployment and post-MVP scheduling are distinct.

## Boundaries & Constraints

**Always:** Preserve trusted occurrence identity, actor-owned atomic append, durable reservation before payload staging, completion only after proven persistence, monotonic fences, current tenant authorization, typed unreadability and buffer clearing. Retain interaction-specific authority/recovery semantics and exactly one default no-op service.

**Never:** Infer custody from hosting, treat fake crypto as production proof, rewrite frozen approvals or mark Azure 8.6 complete. Platform owns Kubernetes infrastructure at `192.168.1.30`. Use Dapr for the application boundary; provider internals stay in Platform. Both future projects remain non-packable; the manifest remains 14. No deployment or infrastructure mutation is included.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Failure handling |
|---|---|---|---|
| Default startup | Registration omitted or Disabled | Existing no-op; no backend access | Safe bounded diagnostics |
| Enabled startup | Explicit closed options and current authority | Validate mode, backend, state profile and leases | Refuse startup on ambiguity |
| Event batch | Typed events and trusted sequence/type context | Protect/reserve all before staging; actor commits | No partial staging or plaintext fallback |
| Crash/cancellation | Reserved keys or uncertain commit | Recover using exact durable source evidence | Retain uncertainty; never infer absence |
| Snapshot | Registered type and generated metadata | Authenticate before deserialize; journal completion | Preserve unreadable bytes; safe replay baseline |
| Mixed reads | Plain, legacy, v2, opaque history | Shared typed routing; stop at first unreadable | No disclosure/checkpoint advancement |

</frozen-after-approval>

## Approved successor intent — S87-DAPR-K8S-20261108

Consume the qualified Dapr Kubernetes cryptography backend and versioned key records from Story 8.12, then connect shared-engine integration through existing actor hooks. Preserve actor-owned atomic staging, trusted occurrence identity, durable reservation/completion, every typed read route and the capacity/state/fleet gates.

The SHA-256 above covers only the exact UTF-8 intent paragraph, without its trailing newline. It identifies the planning text accepted in proposal P5; it is not an effective lifecycle authority digest. Current targets are stock Dapr runtime/CLI 1.19.1, SDK 1.19.1 (owner approval and supplement revision 4, 2026-11-12), `crypto.dapr.kubernetes.secrets/v1` and supported gRPC Encrypt/Decrypt for DEKs. Event/snapshot pdenc-v2 and AAD remain unchanged; new versioned wrapped-key storage, software-KEK custody, Alpha support posture and the proposed successor size bound require 8.12 approval/conformance. V1 records retain their exact schema and 512-byte bound.

Existing binding DTOs, backend/tests and interaction-specific authority/recovery behavior remain retained. Platform's new owning-repository handoff is dedicated immutable/versioned Kubernetes key Secrets, provisioning/rotation RBAC, custody/backup/restore and rehearsal; the custom RSA/OpenBao payload binding is no longer this path's prerequisite. Ordinary OpenBao operational/application secrets remain in scope of their existing stories. Azure 8.6 remains incomplete/deferred.

## Code Map

Paths below are relative to `src/` unless prefixed with `tests/`.

- `Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj`: existing shared-core source linking; avoid duplicate engine dependencies.
- `Hexalith.EventStore.Server/Events/EventPersister.cs`, `SnapshotManager.cs`: existing preparation and typed interaction hooks; preserve atomic staging and advisory snapshot semantics.
- `Hexalith.EventStore.Server/Actors/AggregateActor.PayloadProtection.cs`: existing completion journal, reminders and exact-source recovery; reuse with `AggregateActor.cs`.
- `Hexalith.EventStore.Server/Events/DaprLogicalEventReader.cs`: read seam; preserve publication, projections, Streams and Admin authorization.
- `Hexalith.EventStore.PayloadProtection/`: reuse router, coordinators, resolver and capability probe. `PayloadProtectionBackend.cs`/`PayloadBackendResult.cs` own the SPI/512-byte cap; existing authority checks require explicit profile selection.
- `Hexalith.EventStore.Contracts/Security/PayloadKeyBackend*.cs`: neutral owned request/response/profile, closed operations/outcomes, generated JSON context and strict 16 KiB codec.
- `Hexalith.EventStore.PayloadProtection/DaprPayloadProtectionBackend.cs`, `PayloadKeyBackendTransportAuthority.cs`: one-attempt logical binding transport with mandatory installed authority/authentication and exact-original absence proof; no default production bridge or provider fallback.
- `Hexalith.EventStore.PayloadProtection/PayloadProtectionAuthorityProfile.cs`, `PayloadProtectionOptions.cs`, `PlatformCustodyPayloadProtectionOptions.cs`, `PayloadProtectionServiceCollectionExtensions.cs`, `PayloadProtectionStartupValidator.cs`, `PayloadProtectionRegistrationLog.cs`: closed presets/options, disabled no-op validation and pre-start Enabled refusal while qualification is pending.
- `tests/Hexalith.EventStore.Contracts.Tests/Security/PayloadKeyBackendContractTests.cs`, `tests/Hexalith.EventStore.PayloadProtection.Tests/DaprPayloadProtectionBackendTests.cs`, `tests/Hexalith.EventStore.Server.Tests/Configuration/PayloadProtectionRegistrationTests.cs`: first-phase wire, transport, authority, budget/ownership and actual Server no-op evidence.

## Tasks & Acceptance

**Execution after exact specification/supplement approval:**

- [x] `_bmad-output/implementation-artifacts/spec-8-7-on-premises-backend-amendment.md` — bind final approval to normative/effective digests before changing effective policy.
- [x] `_bmad-output/planning-artifacts/epics.md` — reconcile 8.7's dependency to qualified on-premises conformance after approval; preserve Azure 8.6 deferral and retain separate reconciliation of the later 8.8 release assumptions.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` — verify capacity/erasure-memory prerequisites; carry enrollment, shared namespace, restore identity and legacy-history obligations. Preserve entries. Platform qualifies its component in separately scoped work.
- [x] `src/Hexalith.EventStore.Contracts/Security/` — add the amendment's bounded neutral request/response/profile, closed operations/outcomes and JSON context.
- [x] `src/Hexalith.EventStore.PayloadProtection/DaprPayloadProtectionBackend.cs`, `PayloadProtectionAuthorityProfile.cs` — implement neutral transport and closed authority selection; preserve existing/Azure authority. Thread the preset through state, leases, reservation, deployment, reconciliation and restore-fence validation without migrating existing roots.
- [x] `src/Hexalith.EventStore.PayloadProtection/PayloadProtectionServiceCollectionExtensions.cs` — implement first-phase closed options/startup validation; preserve Server's default registration and interaction semantics. Enabled registration deliberately fails before hosted services while prerequisite admission is unavailable.
- [x] `_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-08.md` — apply the approved Dapr/Kubernetes planning direction and record the separately identified successor intent while preserving original frozen bytes, baseline and first-phase evidence.
- [x] Story 8.12 — approve its exact successor spec/supplement and qualify the built-in Kubernetes/gRPC backend plus versioned wrapped-key records. Revision 4 and fresh SDK 1.19.1 owned-kind results satisfy the bounded EventStore handoff; Platform target and host/state/fleet/restore prerequisites still precede Server admission/wiring.
- [ ] Story 3.21 / Builds — accept the qualified runtime/CLI 1.19.1 input, with the owner-approved SDK 1.19.1 input and the required runtime-minor regressions/reseal boundaries; complete existing capacity/index/erasure-memory and state/fleet/restore prerequisites independently.
- [ ] `src/Hexalith.EventStore.PayloadProtection/PayloadProtectionServiceCollectionExtensions.cs`, `src/Hexalith.EventStore.Server/Configuration/` — after qualified backend/capacity/state/fleet evidence exists, finish enabled host admission and exactly-one engine replacement for each approved write mode.
- [ ] `src/Hexalith.EventStore.Server/Events/`, `src/Hexalith.EventStore.Server/Actors/AggregateActor.PayloadProtection.cs` — connect generic occurrence, reservation/completion and typed reads through existing atomic boundaries; cover every owning Server/Admin read path before completion.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/`, `tests/Hexalith.EventStore.PayloadProtection.Tests/`, `tests/Hexalith.EventStore.Server.Tests/Configuration/PayloadProtectionRegistrationTests.cs` — verify first-phase wire/options/refusal, authority, compatibility/lifecycle unit behavior and the actual Server no-op; retain existing coverage and the two environment-dependent package-artifact skips.
- [ ] `tests/Hexalith.EventStore.Server.Tests/` — after host admission, complete the generic persistence, snapshot, recovery, every-read-path and approved writer-mode matrix.
- [ ] `tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests/`, `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/` — prove the approved real backend, then persisted events/snapshots, reservations, recovery and checkpoints through real Dapr.

**Acceptance Criteria:**

- Given missing backend/capacity evidence, when host activation is evaluated, then it remains blocked; neutral contract/backend work can proceed independently.
- Given authorized integration, when startup and fault matrices execute, then default behavior and all existing interaction guarantees survive.
- Given failed reads or writes, when durable state is inspected, then no partial staging, unsafe replay, plaintext fallback or false completion occurs.
- Given full 8.7 completion, when assessed against the reconciled criteria, then exact supplement, predecessor, source, real persisted backend and named approval evidence exists; Azure deferral is not Azure completion.

## Implementation Notes

- 2026-11-07: The interactive owner answered “approve and stop,” approving this spec and the amendment's exact recorded normative/effective digests. The frozen intent is locked; its SHA-256 covers the opening tag through the closing tag, excluding the following newline. No implementation started. A later resume may begin EventStore contract/backend work; Platform qualification, capacity work and Server host-wiring gates remain.
- 2026-11-07 resume: `$bmad-build 8.7` authorized continuing the approved first phase from HEAD `37f0b65efc0453d60f0cb6da5ca6ef2b2d370e0c`. The existing `baseline_commit` was preserved. Implemented neutral Contracts/Dapr backend and explicit authority profiles; all Enabled registrations refuse before any hosted service can start. Disabled validation inspects descriptors without invoking factories and retains exactly one real Server no-op. Existing interaction-specific authority/recovery code was not generalized into host wiring.
- Provider/runtime response copies, installed Platform authority admission, native carrier validation, real custody/backup/restore behavior and persisted Server integration still need the approved real-service lane. The unit transport authority is a classification fixture, not a production bridge. Both future projects remain non-packable; the release manifest remains 14.
- Source inspection confirmed that index setters still rewrite all pages and erasure observations remain an unbounded lifetime dictionary. The separately approved capacity work is incomplete. Existing shared-namespace, enrollment, host-incarnation, restore-identity and retained-history obligations were carried forward without closing their ledger entries.

## Spec Change Log

- 2026-11-08: Owner “continue and approve” accepts all P1–P7 at proposal SHA-256 `4b618adbca7c6802e41b6da40dac6b8d085ab81875921d6bb1293d9ec3dccd68`. Applied the planning-only Dapr/Kubernetes successor and `S87-DAPR-K8S-20261108` intent. OpenBao frozen block, supplement, old approval digests and original baseline remain unchanged; Story 8.12's exact normative authority and real qualification plus runtime/capacity/state/fleet gates precede further host wiring. No runtime implementation, key-record migration or status advancement follows from this record.

- 2026-11-07: Reconciled Story 8.7's on-premises dependency and recorded the separate Story 8.8 release reconciliation gate. Split implementation tasks into completed first-phase work and pending qualification/host integration. The frozen intent and normative supplement remain byte-identical to approval; no authority transition or predecessor-record rewrite was added.

## Review Triage Log

- First-phase implementation audit fixed: temporary decoded-array ownership on malformed wire input; clearing base64 scratch buffers; implicit predecessor authority in reservation/restore calls; distinguishing missing keys from exact-original mutation absence; bounding private absence-proof exceptions; Disabled validation invoking service factories; and Enabled startup racing hosted services. Added transport authentication, combined Profile/Wrap deadline, cancellation ownership, full Platform-profile reservation/completion and real Server no-op regressions.
- Full-story review/completion remains pending at implementation step 3 because the separately scoped qualification/capacity and Server tasks are incomplete. No `in-review`/`done` transition, release authority or G5 closure is claimed.

## Verification

Run the amendment's exact source-mode build/unit/golden commands; then qualified real-Dapr/backend and Server lanes at their dependency gates. Require persisted end-state assertions and zero selected live skips. Planning checks cover links, digests and whitespace. Runtime/CI/cluster checks remain pending.

### First-phase local evidence — 2026-11-07

All commands ran from the EventStore root. Debug builds use source references; built test assemblies run independently.

```bash
dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll
dotnet build tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.PayloadProtection.Tests/bin/Debug/net10.0/Hexalith.EventStore.PayloadProtection.Tests.dll
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Configuration.PayloadProtectionRegistrationTests
dotnet build tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests/bin/Debug/net10.0/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests.dll
node scripts/payload-protection/verify-golden-vectors.mjs
python3 scripts/payload-protection/verify-golden-vectors.py
```

- All four final builds passed with zero warnings/errors.
- Contracts: 1,569 total, 1,567 passed, zero failed, two existing package-artifact skips. `PackagedReminderApiTests.PackagedReminderApiRunsWithoutWorksTypes` and `TrustedEffectPackageContractTests.NamedPackagesExposeTrustedEffectContracts` require `EVENTSTORE_PACKAGE_CONTRACT_DIR`, which was unset. New `PayloadKeyBackendContractTests`: 30/30, zero skips.
- PayloadProtection: 1,061/1,061 passed, zero skips, 26.713 s, after the final options changes. This includes transport/authority/options, existing compatibility/crypto/lifecycle and Platform-profile reservation/completion tests against the unit store.
- Server: rebuilt source-linked core; `PayloadProtectionRegistrationTests` 2/2 passed, zero skips. The full Server suite and real Server integration were not run in this first phase.
- Retained Azure companion: 192/192 unit tests passed, zero skips. This proves source/unit compatibility, not Azure conformance or Story 8.6 completion.
- Node and Python independently passed V001–V003 with unchanged base normative digest `de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`, envelope SHA-256 `247381efe70c9c4844af998ddec864b41665461b466bda418beb8edc0e5406be` and wrapper SHA-256 `35388a17c7d950f775378c47b6263d75088be358fbe6372a71f2bec55a3356c3`. Their 135 future vectors were not executed.
- Aspire baseline: `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive --format Json`, followed by `aspire describe`, reported all 30 resources Running before code edits. The task-owned AppHost was then stopped through Aspire before builds.
- Frozen intent SHA-256 remains `da45c41e7994a306295cb5bf1f3d0d6d55fb0861c5d72156173a5940ae316f55`; supplement normative SHA-256 remains `43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3`; derived `SHA256(ASCII HXPA || 02 || predecessor || supplement)` remains `835f96ca5b4253f0598712a40773df468f02bd48f518eb0313794addfdc9b180`.

Local scratch logs are `/tmp/story87-{contract,core,server,azure}-build.log`, `/tmp/story87-contract-tests.log`, `/tmp/story87-core-tests.log`, `/tmp/story87-server-focused.log`, `/tmp/story87-azure-tests.log`, `/tmp/story87-contract-focused.log`, `/tmp/story87-node-goldens.log` and `/tmp/story87-python-goldens.log`. They are local verification output, not CI artifacts or immutable qualification evidence. Concurrent Story 2.17 changes were preserved.

### Frozen matrix audit

- Default startup: actual omitted/Disabled Server no-op cases ran and passed, including no Dapr client resolution.
- Enabled startup: first-phase closed options/refusal cases ran in the core suite. Successful qualified backend/state/fleet startup remains gated and untested.
- Event batch, crash/cancellation recovery, snapshot, and mixed Server reads: existing core unit coverage ran; the frozen matrix's full Server persisted-state assertions remain pending. A unit-store reservation/completion result does not satisfy those Server/live matrix rows.

No real OpenBao component/conformance, cluster/key operation, capacity completion, generic Server host activation, CI run or full-story matrix success is claimed.

### Resume verification and prerequisite audit — 2026-11-07

Rechecked the approved scope at EventStore HEAD `2bd75f6b26a41cc58fc69e991ebf87b106ce7eb1`. The original baseline, frozen intent, base normative bytes, supplement and derived authority are unchanged. The existing first phase remains implemented; no runtime implementation changed during this resume. All unchecked tasks remain gated and the story stays `in-progress`.

- Repeated all four source-mode Debug build commands above: zero warnings and errors. PayloadProtection passed 1,061/1,061 (26.228 s), retained Azure units passed 192/192 (2.920 s), and actual Server omitted/Disabled registration passed 2/2 (0.117 s), all with zero skips. These are unit results, not real-service qualification.
- The full Contracts command above returned exit 1: 1,569 total, 1,566 passed, one failed, two existing package-artifact skips (195.289 s). `CorrectiveOciProvenanceReleaseTests.RealMultiRidArchiveContainsExactProvenanceInBothChildConfigs` encountered `NETSDK1047`: shared assets lacked `net10.0/linux-musl-x64` for Admin.Abstractions and `net10.0/linux-musl-arm64` for the EventStore host. Other project builds overlapped this first run. The isolated rerun below, after all other builds/tests stopped, passed 1/1 with zero skips (26.158 s); this supports build-output interference but does not replace the recorded full-run failure. The focused neutral wire contracts passed 30/30 with zero skips (0.148 s).
- Node and Python again passed V001–V003 with the unchanged envelope/wrapper hashes; their 135 future vectors remain unexecuted. All four approved digests were recomputed and matched the preceding evidence. The release manifest still contains 14 packages and both future projects remain non-packable.

```bash
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method Hexalith.EventStore.Contracts.Tests.Packaging.CorrectiveOciProvenanceReleaseTests.RealMultiRidArchiveContainsExactProvenanceInBothChildConfigs
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Security.PayloadKeyBackendContractTests
```

The prerequisite inspection command `rg -n 'PayloadKeyBackend|rsa-3072|payload-key-v1|PlatformCustody' references/Hexalith.Platform/infrastructure/Hexalith.Platform.Custody.Dapr references/Hexalith.Platform/infrastructure/Hexalith.Platform.Custody.Dapr.Tests` returned exit 1 with no matches. The checked-out component still lacks the neutral RSA backend required for the real OpenBao/Dapr lane. `PayloadKeyIndexes.SetScopeEntry` and `SetInventoryEntry` still rewrite every page; `ErasureStateRegistry._observations` remains a lifetime dictionary without eviction. The separately scoped Platform qualification and capacity work therefore remain necessary before Server admission or wiring can proceed. No Platform changes, live provider/cluster operations, stored-history scan or full Server/live matrix ran. This audit supplies no new authority and closes no prerequisite.

Fresh local logs are `/tmp/story87-resume-{contract,core,server,azure}-build.log`, `/tmp/story87-resume-{contract,core,azure}-tests.log`, `/tmp/story87-resume-server-focused.log`, `/tmp/story87-resume-contract-oci-isolated.log`, `/tmp/story87-resume-contract-focused.log` and `/tmp/story87-resume-{node,python}-goldens.log`. They are local scratch output, not CI artifacts or immutable qualification evidence.

### Focused resume verification and prerequisite audit — 2026-11-08

Loaded this specification and all three frontmatter context files at EventStore HEAD `e1980560934736688a05a53a0aadc9397277afae`. The checked-out Platform revision remains `019f593544ad157036b7d12173ff89a16464f3cc`. The approved first phase is already implemented; all remaining implementation tasks require the separately scoped prerequisites. No runtime source changed during this resume, and Story 8.7 remains `in-progress`.

The prerequisite command below returned exit 1 with no matches:

```bash
rg -n 'PayloadKeyBackend|rsa-3072|payload-key-v1|PlatformCustody' references/Hexalith.Platform/infrastructure/Hexalith.Platform.Custody.Dapr references/Hexalith.Platform/infrastructure/Hexalith.Platform.Custody.Dapr.Tests
```

The checked-out component therefore still lacks the approved neutral RSA backend. Source inspection also confirms that `PayloadKeyIndexes.SetScopeEntry` and `SetInventoryEntry` rewrite every addressed page, while `ErasureStateRegistry._observations` remains a lifetime dictionary without eviction. Their existing capacity entries and the retained host/state/fleet/restore/history obligations remain open. The amendment's separately scoped Platform conformance and capacity conditions still prevent Server admission and wiring; this resume supplies no replacement evidence or new authority.

Narrow verification exercised the existing first phase without repeating the unrelated full Contracts packaging suite:

```bash
dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Security.PayloadKeyBackendContractTests
dotnet build tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.PayloadProtection.Tests/bin/Debug/net10.0/Hexalith.EventStore.PayloadProtection.Tests.dll -class Hexalith.EventStore.PayloadProtection.Tests.DaprPayloadProtectionBackendTests
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Configuration.PayloadProtectionRegistrationTests
node scripts/payload-protection/verify-golden-vectors.mjs
python3 scripts/payload-protection/verify-golden-vectors.py
```

- All three source-mode Debug builds passed with zero warnings/errors.
- Neutral wire contracts passed 30/30 (0.150 s), Dapr backend/options/authority cases passed 40/40 (0.451 s), and actual Server omitted/Disabled registration passed 2/2 (0.116 s), all with zero failures/skips. These cover first-phase identity/bounds/ownership, explicit authority, Enabled refusal before service resolution and real Server no-op preservation. They do not cover successful qualified Server activation or the full persisted-state matrix.
- Node and Python independently passed V001–V003 with the unchanged envelope and wrapper hashes; their 135 future vectors remain unexecuted. Recomputed frozen intent, base normative, supplement and derived effective-authority digests match the approved values recorded above. The release manifest still has 14 packages and both future projects remain non-packable.
- No full unit suite, real OpenBao/Dapr conformance, persisted Server/live matrix, stored-history scan, CI, Platform edit, cluster/key operation or infrastructure mutation ran. Azure 8.6 remains incomplete/deferred. The original `baseline_commit`, all frozen/approved bytes, unchecked tasks and predecessor obligations are unchanged.

Local scratch logs are `/tmp/story87-20261108-{contract,core,server}-build.log`, `/tmp/story87-20261108-{contract,core,server}-focused.log` and `/tmp/story87-20261108-{node,python}-goldens.log`. These are local verification output, not CI artifacts or immutable qualification evidence.

### Successor prerequisite audit — 2026-11-12

Loaded this specification and all four frontmatter context files in full at EventStore HEAD `35131a50028c638321d910f1529d3504b105662c`. The checked-out Builds source is `3c2f5944aaa28dc7854a00a8a9df171aa810b0c7`; Platform is `add09b79e61b6e08469bc37f9ec12e82899531db`. The earlier OpenBao/capacity audits above describe their historical inputs. The selected successor has progressed since those audits:

- [Story 8.12](spec-8-12-dapr-kubernetes-cryptography.md) is `done` for its bounded EventStore handoff. Its [approved revision 3 supplement](spec-8-12-dapr-kubernetes-cryptography-amendment.md) binds normative SHA-256 `14eb081c36c1122b337c75ee1d341afc7db0a01ea107ed83596817ddcfd33881` and effective authority `29ec85464835a52d3e4548826b1a5495ceb85e70007e1cbefdea042f852d8c44`. It defines `WrappedDekV2`, the 2,048-byte envelope ceiling and `wrapped-dek-v2` reader capability. Its owned-kind conformance is retained evidence for that bounded subject; it does not attest the Platform target.
- [Gate 1 capacity fixes](spec-payload-protection-capacity-fixes.md) are `done`: current source uses touched bucket pages and point membership reads, and erasure observations have a 65,536-scope eviction bound. The former all-page mutation and lifetime-dictionary blockers are resolved. The ledger still carries whole-inventory verification/retirement scaling and multi-key restore limits; the completed slice did not close those obligations.
- [Gate 2 Dapr error outcomes](spec-dapr-crypto-error-outcomes.md) is `done`: the generated gRPC transport surfaces structural statuses on the calling task. The SDK-hidden-failure/unobserved-exception blocker is resolved for the crypto path. The singleton transport lifetime and controlled inventory rollout/retirement duties remain with Server integration.

**Admission remains blocked under this spec's acceptance criteria.** The following conditions are not satisfied by those completed slices or the local tests below:

1. **Current SDK subject differs from the approved profile.** Revision 3 section 2 names `Dapr.Protos` 1.19.0 from `Dapr.Client` 1.19.0. The checked-out Builds catalog selects 1.19.1, and the freshly restored Contracts/core assets resolve both `Dapr.Client/1.19.1` and `Dapr.Protos/1.19.1`. Successful unit results on that graph do not extend the content-bound 1.19.0 approval or qualify its replacement. Reconcile the exact SDK input and its required qualification under the owning authority before admission; do not change Builds pins or approved normative bytes as part of this audit.
2. **Platform qualification remains bounded.** Supplement section 8 explicitly leaves in-cluster sidecar injection/API authentication, component scopes, production workload identity/RBAC separation, encryption at rest, Secret/etcd backup and restore, target-cluster rotation and old-key retention blocked. Story 3.21 also remains `in-progress`; its refresh slice records passing runtime regressions alongside five failed broker guarantees and pending Platform/AD-26/4.16 handoffs. No accepted target deployment or current fleet authority is inferred.
3. **Host/state/fleet/restore/history obligations remain open.** Retain PF-02 component/profile selection and the shared key namespace, environment-wide enrollment and root-removal proof, fresh host incarnations after lease lapse, authority-generation transitions, exact backup/live fence/lifecycle/Reserved-operation identity, fair reconciliation, inventory rollout/retirement, and the identity-history/legacy JSON bounds/ordinary `$enc` history inventories recorded in `deferred-work.md`. No stored-history inventory or persisted generic Server matrix ran during this audit.

Accordingly, `AddEventStorePayloadProtection` still refuses every Enabled mode before service resolution, and the existing interaction-specific actor hooks remain unchanged. No runtime source, dependency, submodule, cluster/key resource or infrastructure was modified. The spec and sprint status remain `in-progress`; no unchecked full-story task, Azure 8.6, release or G5 gate is closed. This audit updates prerequisite evidence only and grants no new authority.

Verification used the current repository SDK 11.0.100 and `net11.0` outputs, reflecting the already-landed framework change. Builds were serialized; test assemblies ran independently:

```bash
dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net11.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Security.PayloadKeyBackendContractTests
dotnet build tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.PayloadProtection.Tests/bin/Debug/net11.0/Hexalith.EventStore.PayloadProtection.Tests.dll
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net11.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Configuration.PayloadProtectionRegistrationTests
dotnet build tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests/bin/Debug/net11.0/Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests.dll
node scripts/payload-protection/verify-golden-vectors.mjs
python3 scripts/payload-protection/verify-golden-vectors.py
```

- All four builds passed with zero warnings/errors. Neutral Contracts passed 30/30; the full core unit suite passed 1,244/1,244; real Server omitted/Disabled registration passed 2/2; retained Azure units passed 192/192. All selected tests had zero failures/skips. These are source/unit compatibility results on the current graph, not current backend/host qualification.
- Node and Python independently passed V001–V003 with the unchanged normative, envelope and wrapper hashes. Their 135 future vectors remain unexecuted. Recomputed original 8.7 frozen intent, successor planning intent, base normative, historical OpenBao supplement/effective authority and revision 3 Dapr supplement/effective authority digests match their recorded approvals. The original baseline and frozen bytes are unchanged; the manifest remains 14 and both future projects remain non-packable.
- The full Contracts packaging suite, full Server suite, live Kubernetes/Dapr/Server lanes, hosted CI and target operations were not run. Local logs are `/tmp/story87-20261112-{contract,core,server,azure}-build.log`, `/tmp/story87-20261112-{contract,server}-focused.log`, `/tmp/story87-20261112-{core,azure}-tests.log` and `/tmp/story87-20261112-{node,python}-goldens.log`; these are local scratch output only.

### SDK 1.19.1 approval and successor verification — 2026-11-12

The interactive project owner answered **"approve 1.19.1"** after the prerequisite audit above. This resolves its SDK-input approval mismatch. The [Dapr Kubernetes supplement](spec-8-12-dapr-kubernetes-cryptography-amendment.md) is now revision 4, with only its four current section 2 SDK references changed to `Dapr.Client`/`Dapr.Protos` 1.19.1. Normative SHA-256 is `3489775e15bdc5b7ffad4a67f37c6372a23d50b213095e26bd45e1514ad285bd`; derived effective authority is `5d11b464555ac1f40fde3eadfd03c2a3f984f0770d594b30062a4cc641e34204`. Earlier approvals and tests retain their historical subjects. The original 8.7 baseline/frozen intent, 8.12 frozen intent, base/lifecycle/OpenBao authorities, pdenc-v2/AAD and key-record schemas are unchanged.

Code constants, bound assertions and authority comments now name revision 4. A new `KeyRecordAuthorityTests.RevisionThreeFenceDeploymentAndCapabilityBytesAreRefusedWithoutRestamping` regression proves the previous SDK authority is refused for all three durable authority schemas, with input bytes unchanged. No existing root or history was migrated. Package pins and submodules were not changed; Builds already selected 1.19.1. `dotnet-inspect` reported no public API changes for either Dapr.Client or Dapr.Protos from 1.19.0 to 1.19.1; that comparison alone is not behavioral qualification.

Fresh source-mode Debug verification used .NET SDK 11.0.100. The core and live assets both resolve `Dapr.Client/1.19.1` and `Dapr.Protos/1.19.1`:

```bash
dnx dotnet-inspect -y -- diff --package Dapr.Protos@1.19.0..1.19.1 -v q -n 25
dnx dotnet-inspect -y -- diff --package Dapr.Client@1.19.0..1.19.1 -v q -n 25
dotnet build tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.PayloadProtection.Tests/bin/Debug/net11.0/Hexalith.EventStore.PayloadProtection.Tests.dll
dotnet build tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
QUALIFICATION_DAPRD=/tmp/platform-dapr-1191/daprd HEXALITH_PP_KIND=/usr/local/bin/kind dotnet tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests/bin/Debug/net11.0/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests.dll -trait 'Lane=KubernetesCrypto' -explicit only -failSkips -result-trx /tmp/story87-sdk1191-kubernetes-crypto.trx
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net11.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Configuration.PayloadProtectionRegistrationTests
node scripts/payload-protection/verify-golden-vectors.mjs
python3 scripts/payload-protection/verify-golden-vectors.py
```

All three builds passed with zero warnings/errors. The core suite passed **1,245/1,245** (22.238 s); the isolated crypto lane passed **14/14** (120.815 s); actual Server omitted/Disabled registration passed **2/2** (0.116 s), with zero failures/skips/not-run cases. The TRX independently records 14 executed/passed and zero failed/not-executed. The lane used kind 0.31.0 and the unchanged node image and runtime lock; daprd SHA-256 was `b1ece5da76674bf6e15126aeb019fa58e444b5b8edf4d0ac59e32df895ae8fd2`. `kind get clusters` reported no clusters afterward; both lane containers were removed and the five pre-existing running containers remained. The lane verifies real persisted wrapped-key/lifecycle/index state through Dapr, auth/fault outcomes, retained reads and rotation under the new authority. It does not qualify the generic Server persistence/snapshot matrix or the target cluster. Node/Python passed V001–V003 with unchanged hashes; their 135 future vectors remain unexecuted.

The bounded 8.12 prerequisite is now checked. **8.7 stays `in-progress`**: target Platform custody/cluster qualification, 3.21 and PF-02/shared-state/fleet/restore/history/retirement obligations remain open. Enabled registration continues to refuse before service resolution. No deployment, release, infrastructure change to a shared cluster or G5 acceptance is inferred. The task-owned Aspire baseline was inspected (13 Running, 16 Waiting, one Starting) and stopped before source edits; no all-resources-ready claim is made.

Local scratch evidence is `/tmp/story87-sdk1191-{core,live,server}-build.log`, `/tmp/story87-sdk1191-core-tests.log`, `/tmp/story87-sdk1191-server-focused.log`, `/tmp/story87-sdk1191-kubernetes-crypto-tests.log`, `/tmp/story87-sdk1191-kubernetes-crypto.trx`, `/tmp/story87-sdk1191-{protos,client}-api-diff.log` and `/tmp/story87-sdk1191-{node,python}-goldens.log`. It is not hosted CI or target qualification evidence.
