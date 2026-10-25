# EXT-PROTECTION-1 delivery audit — 2026-10-25

**BlockedNotSubmitted. G1–G7 remain open; no complete replacement commitment is ready for acceptance.** This delivery corrects owner-currentness integration and native signing-chain verification. It does not supply the installed custody/observer/trust or physical destruction and restore proof required by Agents Story 5.4. No Live/Full/cohort command, native provisioning/destruction, or consuming Agents path ran. No qualification attempt was reserved. The Agents register stays unchanged and EXT-PROTECTION-1 stays `Uncommitted`.

The [machine-readable packet](ext-protection-1-delivery-2026-10-25.json) preserves the complete nine-field register record, source observations, actual public input hashes, exact missing member paths and G1–G7 requirements. It is an audit, not an installation profile. The [2026-10-22 reconciliation](../../../agents/_bmad-output/implementation-artifacts/story-5-4-dependency-reconciliation-2026-10-22.md), full register including v21/v23 and OQ-5.4-1 remain authoritative.

## Commitment fields and actual bindings

| Field | Current disposition |
| --- | --- |
| Owner / repository | EventStore Maintainer / `Hexalith.EventStore` |
| RequiredArtifact | Full EXT-PROTECTION-1 artifact, including complete v21/v23 and all consumer/evidence obligations; copied without scope reduction into the machine packet and bound to the full register hash. |
| TargetVersionOrCommit | **Unresolved.** No complete immutable owner target exists. The observed source revisions below must not be substituted for this field. |
| TargetIntegrationDate | **Unresolved / unaccepted.** Explicit retained proposal: **2026-11-02**. It is not an accepted or proven delivery promise; no literal date gate was added. |
| CompatibilityContractAndVerificationCommand | Full register contract and the executable candidate below. Its accepted `ProtectionProfileSha256` binding is absent. |
| RequiredEvidenceLevel | Levels 4 and 5. These local source/component checks do not establish installed Level 4 or consuming Level 5 attainment. |
| AcceptedStatus / ConsumingStories | `Uncommitted`; exact existing consumers are preserved in the machine packet. |
| Plan | 41 cases; raw plan-file SHA-256 `c1e7759a2687c122dd182aa48f1828e531166c0157a04b2ae0eba35e9ac4d10c`. This is the verifier's actual pin, not a reserialized JSON-object digest. No plan change or Live execution occurred. |
| Installed profile | `/home/administrator/hexalith-protection-evidence/ext-protection-1/accepted-profile.json` **does not exist**; no profile hash is supplied. |
| Installed engine / roles / trust | No accepted installed engine path/hash, executor/observer/enrollment installation or deletion-capability trust binding is supplied. No synthetic key, tenant, credential, anchor or hash replaces these inputs. |

Candidate command, from the Agents repository root; **retained for review, not run**:

```bash
python3 ../eventstore/tools/verify-ext-protection-1.py --mode Live --profile /home/administrator/hexalith-protection-evidence/ext-protection-1/accepted-profile.json --evidence-root /home/administrator/hexalith-protection-evidence/ext-protection-1/attempts
```

Acceptance must bind the actual profile-file SHA-256 in the register compatibility field. The absent profile's required members are exactly `schema=ext-protection-1-profile/v2`, `targetCommit`, `planSha256`, distinct `tenantReferences.primary`/`other`, `roles`, `engineBinary.path`/`sha256`, and `capabilityTrust`. Every role requires actual `executable`, `executableSha256`, `publicAnchorFile`, `publicAnchorSha256`, `credentialReference`, `identity`, `installationManifest`, and `installationManifestSha256`. Each capability-trust entry needs issuer, audience, tenant, signing-key family/version, public-anchor id/version/file/hash and trust-profile revision, as specified by the [closed verifier protocol](ext-protection-1-verifier-protocol.md). All these installation references remain unresolved; HMAC references are not reused as DEK or verifier enrollment.

## Source corrections and their limits

`PublishedTrustDeletionConsumptionAuthority` now accepts an explicit tenant/actor-scope owner authority. Current owner revision/digest and exact private operation permission no longer come from a synthetic guard proof or equality with the guard revision. Revision-zero succeeds only when independently surviving enrolled authority selects the exact genesis digest. The actual actor's scope, predecessor, target bytes and digest pass unchanged through admit/record/recover/verify. Missing, foreign or ambiguous installation denies. The existing four-parameter constructor/defaults remain available; explicit caller cancellation propagates even when the owner then faults.

Platform's `CustodyDeletionConsumptionOwnerAuthority` supplies the concrete Dapr-only bridge at the existing noncyclic Platform composition boundary. It installs no DI, issuer or genesis. Actual actor/pending-original component tests and the actual Dapr SDK/semantic-provider bridge scenario cover lost acknowledgements, exact recovery, consecutive transitions, stale genesis and scope/tenant substitution. Independent operation, guard and custody-head revisions remain distinct. Separate restrictive admission/revocation/replacement/activation evidence remains a required installed authority; the new owner bridge does not replace it.

P39 now checks actual native signer outcomes/JWS, original no-issue receipts, fresh successor bases and the exact native `{Transition,TargetMutationDigest}` bytes. It rejects summaries without originals, arbitrary intent digests, impossible stale ordinals, committed issuance of an obsolete attempt, and conflicting immutable receipt/operation identities. The final Signed artifact must equal the consumed request. Actual C# native serialization/successor interoperability covers normal issue, explicit stale issue and replacement operation 23 with retained block revision. Historical proof timestamps do not become currently live leases or capability expiries. These checks establish record consistency, not completeness/currentness of an installed append chain or physical provenance.

## G1–G7 closure matrix

| Gap | Remaining required delivery |
| --- | --- |
| G1 — Open | Installed six-operation custody/key delivery and all-or-none manifest composition, effective hold pins and authenticated original vectors remain unqualified. Adopted semantic source and concurrent public Dapr adapters are not installations. |
| G2 — Open | Owner-currentness/genesis/actual-transition source seam is corrected. Independent private/recovery and lifecycle-authorizer backends, durable hold/dispatch-reservation issuer, published trust, registrar, enrollment and separate admission/revocation/activation evidence remain absent or unqualified. |
| G3 — Open | Actual complete copy/holder closure, independent authority placement/native restore fencing, alternate/tainted retirement, terminal native deletion, survivor recovery and original signed vectors/restore barriers remain unqualified. |
| G4 — Open | Mixed-root directory snapshot representation/policy and bindings remain unresolved. Current ProtectSnapshotAsync serializes the entire snapshot under one binding resolved with requestedTarget:null. Installed writer/anchor/scheduler and source-plus-target restore/replay qualification remain missing. |
| G5 — Open | Independent executor, read-only persisted-state observer and enrollment programs/ACLs, same enrolled store/deployment, engine/source/inventory pins, actual driver fixtures and one-attempt authority are not installed. |
| G6 — Open | Native signing/no-issue/recovery/intent correlation is now checked offline. Installed authenticated append/current-online-authority, complete inventory and physical/restore provenance remain unproved; role signatures or summaries cannot supply them. |
| G7 — Open | Complete canonical coordinated source/package target, package-lane locked restore, actual installed profile/engine/roles/trust and accepted integration date are unresolved. Retained package restore exits 1 with NU1102; concurrent lock changes were preserved. |

The missing current/private issuer is concrete: `ICustodyOwnerCallSource.AuthorizeAsync(phase, tenant, ownerScope, operationIdentity, transitionJson, token)` must authenticate and issue the exact independent permission. Concurrent Platform `DaprCustodyAuthorizerClient`/`CustodyLifecycleClient` and public lifecycle/manifest adapters are source clients, not the independent issuer or an installed authority. Their ongoing edits were preserved and are not relabeled qualified by this delivery.

The guard's signed hold reservation still needs exact `ReservationId`, `HoldId`, `TargetDigest`, `RegisteredGuardRevision`, `RegisteredGuardReceiptId`, and state `RegisteredDispatchBlocked`, backed by durable dispatch exclusion until the exact pin outcome is reconciled. An ordinary fresh guard read cannot create that reservation. No installed original destruction vector or independently authenticated restore-barrier receipt is accepted in this packet.

The public runtime reference still contains **51 null fields** (28 runtime and 23 qualification fields), enumerated individually in the machine packet. Its SHA-256 remains `31c28a9d1dd21eed8ca4d0c421088cb946b262c624ba461d0795cab92cd1feb3` and its provider binding remains `ext-secrets-1-hmac`. Key mappings remain `f4e650995f0b35882377e01fb4f30b3944b018aec136e16496b6d349dd595ffe`, with ordinary snapshot semantics and `lifecycleOrAllCopyQualified:false`.

The **20 unresolved semantic installation fields** are also copied exactly from Platform's published installation-gap packet: `awsAccountId`, `region`, `authorityTableArn`, `authorityTableId`, `checkpointBucketOwnerAndEnrollmentVersion`, `originalBucketOwnerAndEnrollmentVersion`, `epochKeyArnsAndIds`, `kekProtectorArnsAndIds`, `independentPlacementAndRestoreCut`, `executorIdentityAndCredentialReference`, `observerIdentityAndCredentialReference`, `enrollmentIdentityAndCredentialReference`, `publishedPublicTrustAndCurrentRevocation`, `installedComponentArtifactDigest`, `installedDaprTransportProfileDigest`, `installedClosureAndTerminationVerifierDigests`, `measuredCompletionReserveBytes`, `approvedRetentionSchedule`, `itemizedCurrentProviderQuote`, and `explicitNativeApproval`. Application code receives no native SDK/identifier/credential substitution. The [Platform approval preparation](../../../platform/docs/implementation/ext-secrets-1-semantic-provider-2026-10-22/OWNER-APPROVAL.md) does not authorize native execution with these facts missing.

## Verification and retained evidence

Evidence directory:

```text
/home/administrator/hexalith-protection-evidence/ext-protection-1/delivery-20261025T065854Z-9c732cee/
```

`verification-manifest-final.json` retains every available exact argv/cwd, exit and log hash, including intermediate failures. `source-inventory-final.json` binds captured working-tree files and actual component binaries; it does not describe a clean installed target. `commitment-packet.blocked.json` and `installation-observation-final.json` retain the submitted audit bytes and fresh public-input check.

| Check | Result and limit |
| --- | --- |
| Final Server Debug/project-reference build and direct four-class selection | **217 passed**, zero failed/skipped/unrun; build zero warnings/errors. `final-server-build` / `final-server-tests`. |
| Final verifier policy plus actual C# interoperability modules | **66 passed**, zero failed; `final-verifier-tests-replacement`. Includes original ASCII/Unicode/exact-integer vectors and signed native normal/replacement intent chains. No installed role was invoked. |
| Platform Dapr SDK/semantic-provider component suite before concurrent lifecycle work | **19 scenarios passed**; `platform-bridge-scenarios-2`. This is historical source/component evidence. |
| Rebuilt exact owner bridge scenario after review | **1 passed**, zero native key effects; build zero warnings/errors. `review-bridge-build` / `review-bridge-test`. |
| Source entry point | Exit 0; 41 planned cases, unchanged raw plan hash, `liveExecuted:false`; `final-source`. Planned cases were not run Live. |
| Package-lane locked restore | **Exit 1, NU1102**, Server and Contracts `>=3.127.0`; `review-package-locked-restore`. Concurrent tracked lock edits were preserved, not overwritten. Package integration remains a G7 blocker. |
| Local Aspire baseline | Build zero warnings/errors; startup **exit 2: Docker unavailable**. Exact describe exited 0 and reported no running EventStore AppHost. Unrelated sessions were preserved. |
| Historical native negative-control bytes | Both retained bundle checksums pass; no probe rerun. Ordinary OpenBao restore still records `ObservedRevival`, `g1Qualified:false`, SHA-256 `6efa381bc70b3de57a24142e5e679f53781c7f89d5ab6740fb7664966f30888e`. This counterexample is not a fresh experiment against the adopted shared-cut provider. |

Exact final EventStore checks:

```bash
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Security.PublishedTrustOwnerStateAuthorityTests -class Hexalith.EventStore.Server.Tests.Security.PublishedTrustDeletionConsumptionAuthorityTests -class Hexalith.EventStore.Server.Tests.Security.PublishedTrustReplacementAuthorityTests -class Hexalith.EventStore.Server.Tests.Security.DeletionConsumptionActorTests -noColor
python3 -m unittest tools.tests.test_ext_protection_1 tools.tests.test_ext_protection_1_cross_language -q
python3 tools/verify-ext-protection-1.py --mode Source
```

The manifest retains the full Platform build/restore commands and their temporary MSBuild input bytes. Local source checks use Debug/project references; package/hosted CI and live physical qualification are not inferred. The first Server selection retained one failing cancellation assertion before its correction. Intermediate native-interop/test failures, the rejected pre-write audit draft and all earlier successful/negative attempts remain retained. They are not rewritten into passing runs.

Three independent review layers and follow-ups completed. Surviving signing/intent/cancellation/compatibility findings were repaired; the final replacement-intent verification gap was covered and independently rechecked. Package publication/locked restore remains blocking. Another monolithic synthetic actor/transport fixture was declined because actual ledger/original recovery and opaque SDK transition forwarding are covered separately; the installed complete actor/provider fixture still remains G4/G5 work.

## Source identity and acceptance boundary

- eventstore: observed full `HEAD` `a680481f682e4e4f796aafc6cea6441f8a6be0ac`; this is source metadata, not the complete target.
- platformSibling: observed full `HEAD` `651b3813a11907cedbafa3a78b02715e95d940fa`; this is source metadata, not the complete target.
- platformRootReference: observed full `HEAD` `ce90c4215f3343c2d78d8aee2ebada92405227ff`; this is source metadata, not the complete target.

An external Git operation captured the Platform bridge and tests at `651b3813a11907cedbafa3a78b02715e95d940fa` during this run. Subsequent concurrent lifecycle/lock and Tenants work was preserved. This assistant performed no staging, commit, push, branch or dependency-pointer update. Component artifact bytes and current source observations are retained separately; neither an observed commit nor a documentation change supplies a complete canonical delivery target.

Do not request acceptance of this incomplete packet, declare Available, create a substitute installed profile, run Live to record another refusal, or start OQ-5.4-1. The existing single-attempt enrollment and original recovery journals remain untouched. The full-delivery spec remains in progress until all seven gaps and their exact installed/canonical bindings are actually satisfied.
