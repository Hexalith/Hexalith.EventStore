---
title: '5.14 Authenticated fallback and Dapr framework-route protection'
type: 'feature'
created: '2026-10-29'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
story_id: '5.14'
story_key: '5-14-authenticated-fallback-and-dapr-framework-route-protection'
planning_baseline: '5945ba8b3575f76597075e460bb9a973453272ea'
baseline_commit: '5945ba8b3575f76597075e460bb9a973453272ea'
context:
  - _bmad-output/implementation-artifacts/epic-5-context.md
  - _bmad-output/project-context.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Hosts admit unclassified HTTP endpoints; Operations trusts caller headers and unvalidated bearers. Channel authentication alone cannot stop peer actor invocation.

**Approach:** Share strict endpoint classification, named scheme-bound policies and denying fallback. Protect framework deliveries and independently validate Operations credentials before effects.

**Scope decision (2026-10-29, user: "apply recommended"):** Implement EventStore-owned host policies and Operations credential validation first. Actor execution-context/protocol qualification, resumable audit and cross-owner conformance remain explicit parent-story blockers. This milestone does not establish peer-actor safety or complete Story 5.14.

**Issuer decision (2026-10-29, user: "apply recommended"):** Reject enabling the embedded workload authority on conforming hosts. Retain its bootstrap only in explicitly nonconforming Development fixtures; those fixtures grant no host conformance or production authority. Add no anonymous issuer exception.

## Boundaries & Constraints

**Always:** Deny unclassified endpoints even with valid credentials. Channel identity never satisfies fallback/human policies. Require actual channel tokens, including Development. Preserve catalog/tenant guards, attribution and SDK header ownership. Each operation admits one kind; existing Operations relay routes remain human-only. Anonymous probes match exactly.

**Never:** Invent trust or anonymous exceptions; change capture/acknowledgment semantics, dependencies or submodules; claim unproven readiness/completion. UI conformance awaits reconciliation with settled PRD guidance; Tenants changes belong to its owner.

## I/O & Edge-Case Matrix

| Scenario | Input | Behavior | Failure boundary |
| --- | --- | --- | --- |
| Unclassified route | Anonymous, human, workload or channel caller | Deny; inventory fails | Before handler |
| Framework delivery | Valid receiver token | Named channel policy admits | Missing/duplicate/wrong token denies before activation |
| Operations relay | Valid human, permitted operation and tenant | Preserve validated identity | Forged caller, invalid/mixed/wrong-kind credentials deny before state |
| Probes | Exact health/alive/ready route | Anonymous, support-safe response | Similar prefixes get no exemption |
| New endpoint | Missing policy or effective scheme | Inventory fails | Never silently classified |

</frozen-after-approval>

## Code Map

Prefixes: `S=src/Hexalith.EventStore`, `D=S.ServiceDefaults`, `O=S.Operations`, `SDK=S.DomainService`; suffixes expand the project name.

- `S/Extensions/ServiceCollectionExtensions.cs`: human DefaultPolicy, no fallback. `S/Program.cs`: actor policy exists; subscribe unnamed; Swagger uses middleware.
- `D/Authentication/EventStoreWorkloadAuthenticationExtensions.cs`: reuse policies; `DaprAppChannelToken.cs` admits tokenless Development; `WorkloadBindingAuthorityExtensions.cs` maps anonymous bootstrap.
- `SDK/EventStoreDomainServiceEndpointInventory.cs`: permits fallback-only/public routes. Reuse startup validation and framework anonymous-metadata removal.
- `O/Endpoints/DeadLetterOperationsEndpointExtensions.cs`: insecure `IsAuthorized`; `O/Actors/IDeadLetterDrainActor.cs`: unguarded protocol. Admin dead-letter services already forward bearer.

## Tasks & Acceptance

**Execution:**
- [x] `D/Authentication/EventStoreHttpHostSecurityExtensions.cs` and `EventStoreHttpEndpointInventory.cs` — add named human policy, denying fallback, resolved-scheme inventory and startup validation. Tighten shared channel authentication.
- [x] `D/Authentication/WorkloadBindingAuthorityExtensions.cs` and shared host validation — reject embedded issuer activation on conforming hosts; preserve bootstrap only in explicitly nonconforming Development fixtures, with startup and bootstrap controls.
- [x] `S/Program.cs`, controller authorization attributes, `S/OpenApi/{ErrorReferenceEndpoints,ApiVersionFallbackEndpoints}.cs` and SignalR mappings — classify existing surfaces, protect framework routes and Swagger middleware.
- [x] `S.Admin.Server.Host/Program.cs`, `S.Admin.Server/Configuration/ServiceCollectionExtensions.cs`, `samples/Hexalith.EventStore.Sample.Api/Program.cs`, `S.RestApi.Generators/RestApiControllerEmitter.cs` — adopt fallback/named scheme-bound policies.
- [x] `SDK/{EventStoreDomainServiceSecurityExtensions,EventStoreDomainServiceEndpointInventory}.cs` — tighten classification; retain canonical POST/catalog guards.
- [x] `O/Program.cs`, `O/Endpoints/DeadLetterOperationsEndpointExtensions.cs`, `O/Security/` — register shared authentication; enforce human operation/tenant/channel checks and validated downstream attribution. Remove caller-header authorization; preserve Admin relay and role mapping.
- [x] `tests/Hexalith.EventStore.{ServiceDefaults,Server,Admin.Server.Host,DomainService,Operations,Sample,RestApi.Generators}.Tests/HttpHostSecurityTests.cs` — exercise the matrix through real pipelines, late endpoints and mapping order. Preserve existing assertions using authenticated fixtures.
- [x] `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/HttpHostSecurityLiveTests.cs` — test framework routes with owned infrastructure/persisted state; report unqualified actor-peer paths separately.

**Acceptance Criteria:**
- Given each scoped host, when metadata is enumerated, then protected endpoints name policies/resolved schemes and only exact probes are anonymous.
- Given rejected traffic, when real pipelines execute, then downstream work is zero and persisted state unchanged; positive controls succeed.
- Given open external/actor/audit prerequisites, when reporting results, then preserve their failure status and incomplete parent story.

## Implementation Notes

The approved EventStore-owned milestone is implemented; the parent story remains **in progress**. No frozen intent was changed.

- Shared HTTP security installs a named bearer-only human policy, an independently denying fallback, resolved single-scheme endpoint inventory, startup refusal for missing runtime enrollment, and request-time refusal of late unclassified endpoints. Only exact `/health`, `/alive`, and `/ready` paths are anonymous. Gateway, Admin API host, generated sample API, SDK, Operations, documentation middleware, SignalR, subscription discovery, delivery, and actor HTTP handlers adopt explicit classifications. SDK request-time validation also retains canonical POST catalog guards across endpoint replacement.
- Every environment requires an actual channel token. Embedded authority activation is rejected on conforming hosts; bootstrap survives only in explicitly flagged nonconforming Development fixtures and retains HTTPS/form/credential boundaries. Production and Staging refusal controls remain explicit; no conforming anonymous issuer exception was added.
- Operations independently validates the shared human JWT profile and existing Admin role mapping, checks channel proof and operation policy, rejects missing/duplicate/conflicting/noncanonical/reserved/unauthorized tenant scope before actor work, and derives action attribution from the validated subject. Caller app headers can only deny mismatches. Capture, delivery acknowledgment, Admin bearer forwarding, dependency versions, and submodules were not changed.
- Existing documentation/projection fixtures now present actual validated credentials. Embedded-issuer fixtures explicitly declare nonconforming Development while retaining Production receiver verification and signature, binding, expiry, revocation, HTTPS, and form negatives. Public/fallback exemption tests now assert strict refusal. Sample generated-action and gateway authentication fixtures explicitly isolate the independently tested catalog boundary through test-only seams; production catalog enforcement remains enabled. Admin role/tenant fixtures name their fake Bearer scheme; the real Admin host exercises the shared signed-JWT boundary.
- Actor execution-context/protocol qualification and peer-invocation restrictions, resumable audit, cross-owner conformance, and interactive UI guidance reconciliation remain open parent-story prerequisites. The earlier live run proves HTTP admission and actual Redis end-state on reused shared backends, not infrastructure ownership; it does not establish actor-peer safety or production readiness. Independent review remains pending at implementation handoff.

## Spec Change Log

## Review Triage Log

### Review 1 (2026-10-29)

All three layers completed before triage. The parent reran the reviewers' actual HTTP probes: `/tmp/story514-parent-blind-probe.log` confirms empty inventory plus successful framework-alias, weakened-human-policy and inline-second-scheme bypasses; `/tmp/story514-parent-edge-probe.log` confirms anonymous topic execution at `/health`, alternate-scheme human execution and an unguarded canonical POST with empty method metadata. These are task-caused boundary defects, not actor-peer claims. Patch routes below add no public surface and correct demonstrated local states. Existing Admin-wide read behavior is documented in `AdminDeadLettersController.TryResolveTenantScope`; preserving it is relay compatibility, not a new platform-scope protocol or Story 2.14 conformance claim.

| Finding | Verdict / route | Verified evidence |
| --- | --- | --- |
| BH01 inline policy schemes | high / patch (with EC01) | Inventory reads only `IAuthorizeData`; ASP.NET also combines `AuthorizationPolicy` metadata. The parent reproduced channel-only execution on a purported Bearer route. |
| BH02 framework route aliases | high / patch (with EC03) | Exact route comparisons miss casing/trailing-slash aliases accepted by routing; the parent reproduced `/Dapr/config` executing without channel proof. |
| BH03 replaced human policy | high / patch | Later `PostConfigure` removes the human-kind assertion while retaining Bearer/authentication; startup passes and a workload-marked principal executes the human route in the parent probe. |
| BH04 unidentified bucket | high / patch | `TenantMatches` maps null retained tenants to `unidentified`; the new ordinary tenant-grant check admits that bucket to non-Admins. Restrict this existing privileged backlog scope without rewriting stored identities. |
| BH05 Admin-wide listing regression | medium / patch | The unchanged facade intentionally forwards no tenant for an Admin, but the new receiver rejects every absent tenant. Restore that existing independently Admin-authorized read; non-Admins still cannot omit scope. Corrected cataloged platform scope remains a separate parent prerequisite. |
| BH06 explicit role parity | high / patch (with EC05) | Admin transformation stops when any explicit role exists, and policies accept any matching explicit role. Receiver fallback after an invalid role can elevate, while first-role selection can wrongly deny a later valid role. |
| BH07 action body buffering | medium / patch | The new scope middleware buffers and parses the whole body before `MaxActionItems` validation, adding unbounded buffering relative to the operation's semantic limits. Bound declared and streaming bodies before parsing without changing capture semantics. |
| BH08 405 becomes 403 | low / reject | Reproduced only for a wrong-method request, with no handler execution. Strict denial of unclassified endpoints remains safe; ordinary SDK callers use declared verbs. Preserving the synthetic 405 endpoint would add a framework-specific exception beyond a direct correction for an uncommon debugging case. |
| BH09 generator migration guidance | medium / patch | Existing consumers using only `AddAuthorization()` lack the newly required named policy. Document registration and runtime enrollment; retain the runnable generated Sample host's positive/missing-credential controls and existing explicit missing-enrollment diagnostic. |
| BH10 issuer setup document | medium / patch | `docs/security/workload-binding-authority.md` still instructs enabling the embedded issuer on Production EventStore hosts, which this approved change now rejects. Correct the operational guidance; invent no external issuer enrollment. |
| EC01 inline policy schemes | high / patch (with BH01) | Parent probe independently confirms the combined inline policy admits an alternate human principal while Bearer fails. Validate every effective inline scheme as well as named policies. |
| EC02 topic at probe path | high / patch | Anonymous handling returns before framework classification; the parent probe executes a topic-marked `/health` handler without a token. Framework/topic classification must take precedence over probe exemption. |
| EC03 framework route aliases | high / patch (with BH02) | Same comparison defect as BH02. SDK-specific validation masks it for SDK hosts, but the shared host inventory still accepts the reachable aliases. |
| EC04 empty method metadata | high / patch | ASP.NET treats an empty method list as any method; the new canonical POST check treats it as no POST. Parent probe executes `/project` without a catalog guard. Correct both startup and late-replacement checks. |
| EC05 invalid explicit role | high / patch (with BH06) | `AdminClaimsTransformation` preserves an invalid explicit role and stops; Operations currently falls back to permission/tenant grants. Align receiver evaluation with the existing facade. |
| VG01 authenticated global count | medium / patch | Pre-verified gap: count is Admin-only, but existing tests authenticate only retry/list or omit the bearer on count. Add ReadOnly/Operator refusal and Admin count readback with the actual channel token. |
| P01 direct cleanup | low / patch | Parent diff inspection found a misindented Admin authorization attribute and duplicate Sample catalog startup-filter registration. Direct indentation/deletion corrections preserve behavior. |
| P02 new-member documentation | low / patch | Newly added public test methods and internal credential-fixture members lack XML comments required by the Hexalith baseline. Add concise documentation without changing assertions. |
| P03 live backend ownership | high / patch | Parent inspection found `DaprTestContainerFixture` defaults to shared Redis/placement/scheduler. The implementer confirmed the earlier live runs supplied no isolated backend overrides; the owned-backend description was false. Replace this test's shared collection with the existing owned Redis/sidecar composition, preserve all actual HTTP/state controls, and disclose prior warm-up/cutover effects without guessing previous state. |
| P04 request-parse exception scope | medium / patch | The first bounded-body correction awaits the downstream pipeline inside its `JsonException` catch. An actor/response JSON failure after an authorized effect would become a client 400. Catch only request parsing failures, preserve stream lifetime/restoration, and verify downstream JSON failures are not reclassified. |

Survivors group into inline policy composition (BH01/EC01), framework aliases (BH02/EC03), human-policy integrity (BH03), privileged backlog compatibility/isolation (BH04/BH05), explicit-role parity (BH06/EC05), bounded action parsing (BH07), migration/issuer documentation (BH09/BH10), topic/probe precedence (EC02), canonical method metadata (EC04), count verification (VG01), and direct cleanup/documentation (P01/P02). There is no frozen-intent change or new public API; these route to one implementation patch handoff.

## Verification

Build projects individually: `dotnet build <project> -c Debug -p:UseHexalithProjectReferences=true -m:1`. Run built xUnit assemblies with `-class`, then affected suites/live controls. Record commands/counts, SHA and CI link/blocker. Run `git diff --check` and `bash scripts/check-committed-files.sh`. Start/inspect Aspire before code edits. Planning has run no tests/runtime.

### Local implementation evidence (2026-10-29)

Planning baseline: `5945ba8b3575f76597075e460bb9a973453272ea`. The verified workspace subsequently includes externally authored commits through `8348bc491ace70dbd0d84c2320a6e4372479bbb9` plus the task-owned working changes; the implementing agent performed no staging, commit, push, reset, or rebase. Task ownership is captured in `/tmp/story514-owned-files.txt` (89 paths). Raw logs remain uncommitted in `/tmp`.

Before edits, `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive` and `aspire describe --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive` succeeded with the application resources healthy. `aspire stop --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive` completed before compilation.

For each project below, the build command is `dotnet build tests/<project>/<project>.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1`. Each final listed build succeeded. The test command is `dotnet tests/<project>/bin/Debug/net10.0/<project>.dll` with the listed arguments. Full output and exact expanded commands are indexed in `/tmp/story514-verification-manifest.json`.

| Project | Test arguments | Result | Final test log |
| --- | --- | --- | --- |
| `Hexalith.EventStore.ServiceDefaults.Tests` | none | 59 passed | `/tmp/story514-servicedefaults-final.log` |
| `Hexalith.EventStore.Operations.Tests` | none | 98 passed | `/tmp/story514-operations-final.log` |
| `Hexalith.EventStore.Admin.Server.Host.Tests` | none | 70 passed | `/tmp/story514-admin-final.log` |
| `Hexalith.EventStore.Admin.Server.Tests` | none | 775 passed, 13 existing skips | `/tmp/story514-admin-library-r2.log` |
| `Hexalith.EventStore.Sample.Tests` | none | 212 passed | `/tmp/story514-sample-final.log` |
| `Hexalith.EventStore.RestApi.Generators.Tests` | none | 136 passed | `/tmp/story514-generator-all.log` |
| `Hexalith.EventStore.Server.Tests` | `-class '*Authentication*' -class '*OpenApi*' -class '*HttpHostSecurityTests' -class '*SignalRHub*'` | 321 passed | `/tmp/story514-server-final-r5.log` |
| `Hexalith.EventStore.DomainService.Tests` | `-class '*HttpHostSecurityTests' -class '*DomainServiceTrustBoundaryTests' -class '*EventStoreDomainServiceExtensionsTests' -class '*EventStoreDomainEventsEndpointExtensionsTests'` | 196 passed before the additional late-replacement control | `/tmp/story514-sdk-focused-r2.log` |
| `Hexalith.EventStore.DomainService.Tests` | `-method '*LateCanonicalReplacementWithoutCatalogGuardIsDeniedBeforeHandler'` | 1 passed | `/tmp/story514-sdk-late-catalog-r7.log` |
| `Hexalith.EventStore.DomainService.Tests` | `-method- '*HighEntropyPartitionsPromoteAndPruneWithoutTenantCandidateGrowth' -longRunning 30` | 1,281 passed, 1 existing skip; one exact capacity method excluded | `/tmp/story514-sdk-final-r8.log` |
| `Hexalith.EventStore.Server.LiveSidecar.Tests` | `-class '*HttpHostSecurityLiveTests'` | 1 passed on reused shared backends; not owned-infrastructure evidence | `/tmp/story514-live-final.log` |

The unqualified SDK run `timeout -k 10s 180s dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -longRunning 30` exited **124** without completing (`/tmp/story514-sdk-full-r3.log`). xUnit identified `DomainPartitionedSharedProjectionRebuildTests.HighEntropyPartitionsPromoteAndPruneWithoutTenantCandidateGrowth`, which processes 10,000 random owner histories in memory, as the long-running case. This test, its provider/handler fixture, the shared projection rebuild/coordinator implementations, and in-memory store have no diff from the planning baseline, and the fixture does not register HTTP/authentication. This is an unchanged projection-capacity validation limit, not evidence of an authentication or environment failure. The one exact method was excluded only through the runner argument for the remaining complete suite; no source skip or weakened assertion was added, and there is **no unqualified full-SDK-suite pass**.

The live control rejects missing/wrong/duplicate channel credentials on subscription/configuration/actor-health/delivery/actor-method routes before activation or persistence, compares raw Redis bytes unchanged, and proves valid-channel delivery and actor controls persist changed markers. It does not qualify actor-peer invocation.

`git diff --check` and `bash scripts/check-committed-files.sh` passed (9,492 tracked files checked; `/tmp/story514-diff-check-handoff.log`, `/tmp/story514-committed-files-handoff.log`). CI was not run for this working tree; no CI link is available, and no CI or production-readiness claim is made.

### Parent implementation and matrix audit (2026-10-29)

The parent inspected the task-owned unified diff against the immutable baseline, including added files (`/tmp/story514-owned-bypmfdx3.diff`, 3,541 lines). The complete workspace snapshot is `/tmp/story514-baseline-p8bp9q7e.diff`; concurrent payload-protection and other planning changes are outside this milestone. All eight implementation tasks have corresponding changes and executed verification. Final test-log summaries were checked directly against the command manifest; all listed lanes have zero errors/failures, with the recorded skips and exact capacity-method exclusion preserved.

| Frozen matrix row | Executed coverage | Evidence |
| --- | --- | --- |
| Unclassified route | `LateUnclassifiedEndpointDeniesEveryCredentialBeforeHandler`; `StartupRejectsMissingOrIneffectiveClassification` | Shared suite: 59 passed; all four caller classes denied and handler count zero |
| Framework delivery | `FrameworkDeliveryRequiresActualChannelTokenInDevelopment`; `FrameworkDeliveryRejectsDuplicateAndMixedCredentialsBeforeHandler`; `RejectedFrameworkRequestsPreserveDurableStateAndNeverActivateActors` | Shared suite and live suite: 59 and 1 passed; raw Redis readback unchanged on rejection, valid delivery/actor markers persisted |
| Operations relay | `RejectedCredentialsProduceZeroActorEffects`; `RejectedTenantProducesZeroActorEffects`; `ValidHumanRelayPreservesTenantAndUsesValidatedAttribution`; `DuplicateActionScopeRejectsBeforeActorWork` | Operations suite: 98 passed; actor calls absent on rejection and receiver-derived attribution on positive actions |
| Probes | `AnonymousInventoryAcceptsOnlyExactProbes`; `ProbeRequestPathMustMatchExactly`; host inventory controls | Shared and scoped host suites passed; similar prefixes, casing and trailing slash confer no exemption |
| New endpoint | `StartupRejectsMissingOrIneffectiveClassification`; `UnclassifiedEndpointsFailStartupInEitherMappingOrder`; `MissingRuntimeInventoryEnrollmentFailsStartup`; `LateCanonicalReplacementWithoutCatalogGuardIsDeniedBeforeHandler` | Shared 59 passed; SDK final 1,281 passed/1 existing skip with only the recorded capacity method excluded; late replacement control separately passed |

This audit accepts the approved HTTP-boundary milestone for independent review only. It does not close the parent story, actor-peer protocol, resumable audit, external/UI conformance, CI or production gates.

### Live evidence ownership correction (review)

The earlier `/tmp/story514-live-http-tests.log` and `/tmp/story514-live-final.log` runs did **not** establish owned Redis, placement, or scheduler infrastructure. The implementing agent supplied no backend launch/cleanup commands or explicit infrastructure environment variables. Inherited infrastructure selection and backend/container identities were not captured; the fixture defaults to shared Redis and installed placement/scheduler. The old final log records only the application listener `http://localhost:41111`, which does not establish backend ownership.

`DaprTestContainerFixture.InitializeAsync` also activated the projection-delivery writer cutover through `IProjectionDeliveryCutover.ActivateAsync` (including its fixture backup/quiescence assertions) and performed an ETag actor warm-up through `RegenerateAsync`/`GetCurrentETagAsync`. Those effects occurred outside the HTTP security test's two seeded marker keys. Their prior persisted state was not captured. No shared values have been guessed, restored, or swept. Earlier references to “owned Dapr/Redis” for these runs were incorrect and are withdrawn.

The review fix removes the shared collection. The replacement uses `RouteCatalogDevelopmentRedis` with its exact container-ID/owner-label checks, a checksum-verified `RouteCatalogDevelopmentSidecar`, a private component directory, and explicit empty placement/scheduler addresses. It imports no application/API tokens and invokes no shared cutover or warm-up fixture. Selected nonsecret identities and identity-checked cleanup observations are emitted by the focused test with `-showLiveOutput`. This remains a direct application-port HTTP boundary test, not actor-peer protocol qualification.

### Review corrections and independent parent verification (2026-10-29)

All accepted Review 1 findings were corrected with internal implementation changes, tests and consumer documentation; no new public runtime API was added. The parent inspected the patched policy combination, human requirement, canonical method handling, Operations scope/body handling, test controls and isolated fixture. No new finding was deferred. BH08 remains the recorded low-severity rejection. The existing independently Admin-authorized global backlog read is preserved; the unidentified bucket requires Admin. This compatibility behavior does not close Story 2.14's corrected platform-scope gate.

The parent independently rebuilt and ran all nine affected lanes using the commands in `/tmp/story514-parent-verification.json` and `/tmp/story514-parent-verify.py`. Source baseline and workspace HEAD remain `5945ba8b3575f76597075e460bb9a973453272ea` and `8348bc491ace70dbd0d84c2320a6e4372479bbb9`, respectively, with the reviewed working changes. All nine builds succeeded. Final results supersede earlier implementation counts:

| Project/lane | Passed | Existing skips | Parent log |
| --- | ---: | ---: | --- |
| ServiceDefaults, full | 69 | 0 | `/tmp/story514-parent-servicedefaults-test.log` |
| Operations, full | 117 | 0 | `/tmp/story514-parent-operations-test.log` |
| Admin host, full | 70 | 0 | `/tmp/story514-parent-admin-server-host-test.log` |
| Admin library, full | 775 | 13 | `/tmp/story514-parent-admin-server-test.log` |
| Sample, full | 212 | 0 | `/tmp/story514-parent-sample-test.log` |
| REST generator, full | 136 | 0 | `/tmp/story514-parent-restapi-generators-test.log` |
| Gateway authentication/OpenAPI/host/SignalR | 321 | 0 | `/tmp/story514-parent-server-test.log` |
| SDK, excluding only the recorded capacity method | 1,285 | 1 | `/tmp/story514-parent-domainservice-test.log` |
| Isolated live HTTP/state proof | 1 | 0 | `/tmp/story514-parent-server-livesidecar-test.log` |
| **Total** | **2,986** | **14** | **Zero errors or failures** |

The exact capacity-method exclusion and earlier 180-second exit 124 remain disclosed above; this is not an unqualified full-SDK-suite pass. Every frozen matrix row and the accepted review regressions ran in these passing lanes. The final live test creates a private, random-port Redis container and checksum-verified sidecar, records their exact identities, disables placement/scheduler, and removes only its own resources after identity checks. Its negative requests leave raw persisted bytes unchanged and activate no actors; matching controls persist their expected markers. It does not use the shared cutover/warm-up fixture.

`git diff --check` and `bash scripts/check-committed-files.sh` passed again (9,492 tracked files). CI remains unrun, with no CI link or production qualification claimed. This spec is **done for the approved bounded milestone**; parent Story 5.14 remains open for actor-peer/execution-context qualification, resumable audit and external/UI conformance.

The workflow's local commit is prepared in an isolated checkout on `fix/story-5-14-http-host-protection`, preserving the shared main checkout and unrelated edits. Exact candidate validation with the repository-pinned commitlint **21.1.0** succeeded: `node node_modules/@commitlint/cli/cli.js --edit /tmp/story514-commit-message.txt --verbose` reported zero problems and zero warnings (`/tmp/story514-commitlint-preflight.log`). No push or pull-request mutation is part of this run.
