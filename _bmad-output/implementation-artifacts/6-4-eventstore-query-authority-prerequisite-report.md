# EventStore query-authority prerequisite: source verification, 2026-10-20

The EventStore prerequisite and review fixes are implemented. **422 focused cases pass without errors, failures, skips or unrun cases:** final Server 140 and Contracts 183, plus unchanged DomainService 89 and Client 10. The initial 415-case capture and narrow review runs remain separately labeled. Broader acceptance remains blocked by the independently reported environment/governance checks below. No publication, deployment, enrollment or Projects parent-story acceptance is claimed.

## Source and contract identity

Changes are uncommitted on EventStore baseline `1eaabca4f8d3597a3a1f9fadf341be5cb9a3a8c0`. The 18 changed source/test files are captured in [current source manifest](evidence/6-4-eventstore-query-authority-20261020/source-manifest.json) (manifest SHA-256 `662e0add74c7c8eb37ff297f562ba2924b2323125af6379bec7610489e071224`). Their bytes were rechecked after validation. Source dependency revisions are:

| Repository | HEAD |
| --- | --- |
| Tenants | `63b7b3d8d6cd18db189c7dbfaf3cd94491115fa9` |
| Commons | `5c23c818f639c5bb3175f964c5549440bd5b876d` |
| Builds | `b435f2e3b3892e03f69322d24de6003b0baf7a7d` |
| Enclosing Projects | `16886b7d9c15b5356fad860b0d7b0f97eb9f32b3` |

- The v1 authorization-details profile stays `https://hexalith.com/security/workload-binding/v1`. `QueryCallerAuthorityBinding.Bind/Verify` now require exact Tenant, domain and `eventstore:bound-query-authority-v1` claims, including rejection of additional claims.
- `WorkloadAuthorityGrant.AllowQueryAuthorityDigest` defaults to false. An enabled grant's `domain-service:query` operation requires exactly the three request bindings, with a canonical 64-character uppercase SHA-256 digest and exact configured Tenant/domain allowlists. Other operations retain ordinary exact-value policy; `AllowUnbound` cannot waive this query requirement.
- The additive wire member is `QueryCallerAuthority.OriginatingContext`, of type `OriginatingQueryContext`. It preserves actor, original workload, delegation flag/id, scopes and audiences separately from the owner hop. Collections are defensively copied and retain null absence. A null member is omitted from JSON/DataContract, preserving legacy authority/proof bytes. Existing envelope/router forwarding carries the member; manual authority/submission equality and hashing include it.
- The gateway constructs this context only after verifying nested originating authority and the existing requester proof/allowlist. Owner-hop workload stays the authenticated requester; its audience comes from validated named receiver options with the same trimming as JWT validation. The SDK exposes `VerifiedQueryCaller.OriginatingContext` only after digest verification. Roles remain unforwarded and the existing permission forwarding remains in place. Owners still decide current actor/resource access.
- Both proof families accept aggregate IDs through 256 characters. Principal and proof Tenant/domain bounds remain 200; independent aggregate-identity Tenant/domain validation remains 64. Cryptography and assertion/proof lifetimes are unchanged.

The capability and authenticated field mapping are documented in [workload-binding-authority.md](../../docs/security/workload-binding-authority.md). No dependency pins, runtime configuration, historical evidence, index, branch or commit were changed by this implementation.

The additive constructor is:

```csharp
public OriginatingQueryContext(
    string originalActorId,
    string? authenticatedWorkloadId,
    bool isDelegated,
    string? delegationId,
    IReadOnlyList<string>? scopes,
    IReadOnlyList<string>? audience);
```

`OriginatingQueryContext` is a sealed record. Its six properties have init accessors; scope/audience accessors make defensive copies. `QueryCallerAuthority.OriginatingContext` is an optional init property, while `VerifiedQueryCaller.OriginatingContext` is a read-only optional property on the SDK-established request feature. `WorkloadAuthorityGrant.AllowQueryAuthorityDigest` is a read/write `bool` configuration property defaulting to false. Existing caller-authority constructors and delegation client method signatures are unchanged.

## Final focused verification

The parent reran the selected Server security and Contracts query/identity classes after all review fixes. [Final commands, source binding and results](evidence/6-4-eventstore-query-authority-20261020/final-validation.json) retain exact argument arrays and link the native logs. Both commands exited 0: Server **140/140**, Contracts **183/183**, with zero errors, failures, skips or unrun cases.

```sh
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*WorkloadBindingAuthorityTests' -class '*BoundOwnerQueryAuthorityTests' -class '*BoundOwnerQueryControllerTests' -class '*JwtWorkloadAssertionIssuerTests'
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class '*QueryCallerAuthorityTests' -class '*ProjectionAdapterContractTests' -class '*AggregateIdentityTests'
```

The only production source changed since the initial focused capture is the HTTP host's `QueriesController.cs`. DomainService and Client production/test inputs are unchanged; their complete unskipped **89** and **10** focused results remain applicable. Together these checks cover **422** prerequisite cases, including every acceptance-matrix row and the review additions. All four individual Debug/source builds passed; Server and Contracts were rebuilt after their review changes, again with zero warnings/errors.

Three independent review layers completed. The audience normalization defect and actionable coverage/documentation/evidence findings are fixed. The proposed missing-options guard was rejected because the protected HTTP authentication path already requires a configured audience and registered options. The broader failures and incomplete Server diagnostic below remain unaccepted gates.

## Review patch verification

The producer trims the configured receiver audience identically to workload JWT validation. A distinct padded custom receiver audience now reaches a verified SDK handler through gateway capture and routing; the original user audience stays separate. The same path covers a nondelegated origin with absent workload/delegation/scopes/audiences and populated independent owner-hop identity. No missing-options guard was added.

New runtime cases cover a capability-enabled grant's second exact-bound operation, two different query digests without grant changes, duplicate raw Tenant/domain/digest JSON properties at the token endpoint, and correctly RSA-attested contexts rejected for a mismatched actor or a recognized authenticated-caller origin carrying a disallowed context before protected handler work. The existing Production-mode RSA/SDK and current-owner denial test remains in the run. DataContract compatibility now asserts that the new null member is absent from the serialized legacy shape. Authority documentation records the inherited 64-entry/512-character scope/audience truncation rules, without changing helper behavior or permission authority.

Only these changed-file lanes ran during the review patch:

```sh
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*BoundOwnerQueryAuthorityTests'
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -method '*ProductionQueryAuthorityRunsThroughActualIssuerAndReceivingVerifierAndStopsNewIssuanceOnRevocation' -method '*CapabilityAllowsChangingDigestsAndPreservesOtherExactBoundOperations' -method '*DuplicateRawQueryBindingKeysAreRefusedBeforeIssuance' -method '*CorrectlyAttestedInvalidOriginContextIsRefusedBeforeProtectedHandler' -method '*QueryDigestCapabilityCannotWidenAnExactGrant' -method '*ExactQueryBindingRejectsEveryChangedIdentityResourceOrContext'
dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class '*QueryCallerAuthorityTests'
```

Both builds passed with zero warnings/errors. Controller **14**, authority **45**, and contracts **4** all passed; errors/failures/skips/not run are all zero. Every command exited 0. The [review command/result manifest](evidence/6-4-eventstore-query-authority-20261020/review-validation.json) binds these commands to the tested source manifest for that capture and links the retained native logs. No broader verification was rerun during this patch; the parent workflow records its final combined verification separately.

The final test-only refinement independently checks the restriction that originating context belongs only on a delegated-owner-query authority: its otherwise valid, correctly RSA-attested authenticated-caller envelope is denied before the handler. Server was rebuilt with the same Debug/source command above, then only this method reran:

```sh
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -method '*CorrectlyAttestedInvalidOriginContextIsRefusedBeforeProtectedHandler'
```

Both method cases passed, with zero errors/failures/skips/not run; build and test exit codes were 0. [Final refinement commands/results](evidence/6-4-eventstore-query-authority-20261020/origin-placement-validation.json) bind these results to the current source manifest. The 63-case capture above retains its own [prior review source manifest](evidence/6-4-eventstore-query-authority-20261020/review-source-before-origin-placement.json); the two rerun cases are a subset, not two additional unique cases. The parent workflow records its combined final-source verification separately.

The [durable evidence packet](evidence/6-4-eventstore-query-authority-20261020/README.md) contains the current and initial source manifests, sanitized native logs/summaries, unchanged blocker comparisons, capture provenance and LF checkout protection. Relative links make it independent of `/tmp`; `sha256sum -c SHA256SUMS` verifies retained bytes from that directory. Initial captures remain explicitly labeled pre-review; all existing broad limitations below remain open.

## Initial focused verification (before review fixes)

This initial capture predates the review patch and is not a complete verification claim for the current source. Its [source manifest](evidence/6-4-eventstore-query-authority-20261020/initial-source-manifest.json) has SHA-256 `070fe379351a72b7efe641885f4b5e1450ddb0425bf73756c9ce4fe764df7a9b`. All commands ran from the EventStore repository. Builds used local Debug/source mode; each completed with zero warnings/errors:

```sh
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1
```

Initial Server build output is [initial Server build](evidence/6-4-eventstore-query-authority-20261020/initial-server-build.log); the other build outputs are [Contracts build](evidence/6-4-eventstore-query-authority-20261020/initial-contracts-build.log), [DomainService build](evidence/6-4-eventstore-query-authority-20261020/initial-domainservice-build.log), [Client build](evidence/6-4-eventstore-query-authority-20261020/initial-client-build.log).

```sh
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*WorkloadBindingAuthorityTests' -class '*BoundOwnerQueryAuthorityTests' -class '*BoundOwnerQueryControllerTests' -class '*JwtWorkloadAssertionIssuerTests'
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class '*QueryCallerAuthorityTests' -class '*ProjectionAdapterContractTests' -class '*AggregateIdentityTests'
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -class '*DomainServiceTrustBoundaryTests' -class '*QueryAuthorityCorrectionTests'
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*BoundOwnerQueryProofTests'
```

| Initial focused suite | Total/passed | Failed/errors/skipped/not run | Retained native log |
| --- | --- | --- | --- |
| Server | 133 | 0/0/0/0 | [Server](evidence/6-4-eventstore-query-authority-20261020/initial-server-focused.log) |
| Contracts | 183 | 0/0/0/0 | [Contracts](evidence/6-4-eventstore-query-authority-20261020/initial-contracts-focused.log) |
| DomainService | 89 | 0/0/0/0 | [DomainService](evidence/6-4-eventstore-query-authority-20261020/initial-domainservice-focused.log) |
| Client | 10 | 0/0/0/0 | [Client](evidence/6-4-eventstore-query-authority-20261020/initial-client-focused.log) |

Exact argument arrays, exit codes and native summaries are retained in [initial commands and results](evidence/6-4-eventstore-query-authority-20261020/initial-validation.json). Every focused command exited 0.

| Spec matrix row | Passing coverage in those focused runs |
| --- | --- |
| Production query | `WorkloadBindingAuthorityTests.ProductionQueryAuthorityRunsThroughActualIssuerAndReceivingVerifierAndStopsNewIssuanceOnRevocation`: actual Production-mode RSA broker/outbound issuer/receiving validator, serialized delegated owner envelope and SDK dispatch |
| Policy mismatch | `QueryDigestCapabilityCannotWidenAnExactGrant`: 22 refusal cases; production test also rejects a mismatched issuing workload |
| Tampering | `ExactQueryBindingRejectsEveryChangedIdentityResourceOrContext`, nested gateway tests and actual RSA SDK context forgery; protected handler is not called on forgery |
| Lifetime/revocation | Production test rejects an expired RSA assertion and new issuance after revocation, while documenting existing JWT validity; proof tests reject expiry |
| Aggregate boundary | `AggregateBoundIs256ForBothProofFamiliesWithoutTruncation`: 200/201/256 accepted, 257 refused, altered identity refused |
| Limit isolation | `PrincipalTenantAndDomainProofBoundsStay200`: 200/201 isolation and independent 64-character domain limit |
| Origin separation | Production RSA SDK test, `OwnerSdkSeparatelyExposesVerifiedOriginAndStillRunsCurrentOwnerAuthorization`, nested gateway/router capture, and JSON/DataContract defensive-copy tests; a current owner denial still wins after successful verification |
| Compatibility | Exact legacy authority/proof bytes, absent context/collections, prior envelope contracts and genuinely unbound legacy dispatch |

The RSA tests use a disposable synthetic certificate and in-memory authority transport. They establish source behavior, not deployed TLS identity, PKI status, operational grants or consumer acceptance.

## Broader suite results and blockers

```sh
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -longRunning 60
```

- **Client:** total 1,678; failed/errors 0; skipped 2; not run 0; exit 0. The two existing `PendingBatchRecoveryLiveTests` cases require isolated Dapr endpoints. Native output: [Client broad log](evidence/6-4-eventstore-query-authority-20261020/initial-client-broad.log).
- **DomainService:** total 999; failed 1; errors/skipped/not run 0; exit 1. `DomainModuleAuthoringGuardrailTests.TenantsDomainService_DoesNotReferenceGeneratedApiHostOrDeclarePerMessageControllers` requires the absent nested `references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj`. Its source is unchanged. Native output: [DomainService broad log](evidence/6-4-eventstore-query-authority-20261020/initial-domainservice-broad.log).
- **Contracts:** cancelled after the unrelated governance failure; native summary reports total 2,134, failed 1, skipped 2, errors/not run 0, 219.160 seconds; process exit 58. This cancellation is not a full-suite pass. The skipped package-contract cases require `EVENTSTORE_PACKAGE_CONTRACT_DIR`. Native output: [Contracts broad log](evidence/6-4-eventstore-query-authority-20261020/initial-contracts-broad.log).

The Contracts failure is `Hexalith.EventStore.Contracts.Tests.Packaging.ContractsPackageDependencyTests.SharedConsumerAuthorityValidatorPassesForEveryTrackedMsBuildSurfaceAsync`. Its verbatim native output reports `Consumer package authority validation failed with 90 error(s):`, including these unchanged historical subjects:

```text
_bmad-output/implementation-artifacts/evidence/6-1-p1r-31251-published-run/canonical-resume-20261018T210839Z-b3f330/frozen-verifier-tooling/Directory.Packages.props contains consumer PackageVersion Update 'Microsoft.AspNetCore.SignalR.Common'.
_bmad-output/implementation-artifacts/evidence/6-1-p1r-31251-published-run/canonical-resume-20261018T210839Z-b3f330/frozen-verifier-tooling/Directory.Packages.props contains consumer PackageVersion Include 'System.Security.Cryptography.Pkcs'.
```

All 90 errors remain verbatim in the log. [governance input comparison](evidence/6-4-eventstore-query-authority-20261020/governance-input-comparison.json) verifies all 18 complained-about MsBuild paths, the test, EventStore adapter/wrapper and Builds validator/companion against HEAD. These 23 files match HEAD bytes exactly. The 24th input, Builds' central catalog, matches HEAD after its preexisting CRLF normalization; its exact working bytes SHA-256 `806922385b3209e0f396ccb8478870bfca59a60ac9432fd6c97c63a22b34f337` also matches the pre-work pin snapshot. No historical file or policy was repaired or excluded.

An earlier Server full attempt on the preceding test assembly was interrupted with exit -2 and no final summary; its output remains [earlier interrupted Server log](evidence/6-4-eventstore-query-authority-20261020/initial-server-before-rsa-sdk.log). It is not current-source qualification evidence.

The **initial implementation Server** diagnostic attempt reached its 180-second bound and was cancelled, then terminated with the runner's requested second interrupt after a 15-second grace period. Exit -2; no native final summary or complete counts. It reported 12 existing DW1 skips and no failures before interruption. Diagnostics identified `SecretsProtectionTests.TrackedReusableContent_DoesNotContainUsableSecrets` as a long-running test. Native output is [bounded Server broad log](evidence/6-4-eventstore-query-authority-20261020/initial-server-broad.log); this incomplete attempt is not a full-suite pass. The initial focused Server 133/133 result was complete and unskipped for its pre-review source capture.

## Local topology and remaining acceptance boundary

```sh
aspire start --non-interactive --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
aspire describe --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json
git diff --check
```

Aspire's build succeeded, then startup exited 2 (AppHost process 134): `Plain local Aspire run requires both Hexalith.Tenants host projects before any resources can start.` Missing subjects are under EventStore's nested `references/Hexalith.Tenants/src/`, ending in `Hexalith.Tenants/Hexalith.Tenants.csproj` and `Hexalith.Tenants.Api/Hexalith.Tenants.Api.csproj`. The describe command exited 0 and confirmed no AppHost was running. Startup output remains in [Aspire startup log](evidence/6-4-eventstore-query-authority-20261020/initial-aspire-start.log). Nested dependencies were not initialized. Debug/source builds consumed the existing top-level sibling projects instead.

`git diff --check` passed. All new/changed source and test files also passed UTF-8, final-newline and trailing-whitespace checks. Changed-file review-patch checks are complete; the broad suite/environment failures, operational qualification and parent Story 6.4 acceptance remain open. No frozen spec text or Projects tracking was changed.
