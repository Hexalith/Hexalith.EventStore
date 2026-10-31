---
title: Qualify existing administrative Dapr transports in private Development catalogs
status: verified-owner-delta-awaiting-parent-integration
baseline_commit: 5e3c2f0e60b114cdaab52594ccbb254829d40e0f
---

## Intent and boundary

Preserve actual Admin.Server→Gateway and Admin.UI→Admin.Server HTTP operations and their existing JWT/human/admin authorization. Bind private Development technical transports to actual selected MVC/native endpoint references, real policies and verbs, independently loaded ACL bytes, loaded owner code and immutable committed receiver observations. Broad ACLs supply reachability; they do not create operations. Existing GET/POST/PUT grants still deny DELETE. Domain workload pattern refusals, schema-three message eligibility and Production trust remain unchanged. No participant is hidden and no fake callback, authority plugin or arbitrary wildcard authority is introduced.

Implementation stays in `/tmp/eventstore-development-admin-latest`, based on upstream `5e3c2f0e6`; all 54 upstream files are preserved. Original producer files and the normal index are unchanged. The earlier isolated worktree and initial report were superseded by this explicit reconciliation. Parent owns integration, broader gates, commit/push and release. This delta is a correction of the already independently reviewed scope; all 14 triage rows below are retained.

## Implemented transport and review corrections

Auxiliary/receiver records retain their old CLR constructors and Deconstruct signatures. Optional init-only `Http`/`NativeActor` metadata is omitted when null, preserving legacy JSON. HTTP metadata binds actual SDK controller/action, canonical verbs, DLL digest and immutable actual ACL witnesses. Actual MVC request-delegate closure must capture the same selected action descriptor; foreign/multicast delegates refuse. Framework inline constraints, escaped scalar handling and concrete narrow ACL witnesses preserve real route semantics. Literal prefix and single/multilevel wildcard behavior follows pinned Dapr1.18.7 trie code; it does not authorize a new receiver.

The original two-argument finite receiver ABI and v1 behavior remain. Its explicit `technical:true` overload is private Development only. Sender and MVC receiver require the real committed root/epoch and durable lease. Native Admin.Server.Host installs the real `Add/UseEventStoreDevelopmentAdminCatalog` bootstrap and workers for explicit `Role=admin-receiver`, captures actual controllers, and cannot pretend to be receiver-free. Technical schema2 activation refuses before commit. V2 retains strict legacy/lifecycle operation selection. Actual mutating Dapr RPC boundaries, including handled backend errors and post-dispatch cancellation, retain uncertain durable leases. Acquired route metadata, nested ACL witnesses and action inventory stay immutable.

Existing Admin owner-sidecar actor-state GETs use only an explicitly bound `EventStore:RouteCatalog:DevelopmentActorStateOwners:[i]:{AppId,HttpEndpoint,ApiToken}` destination. The actual selected owner token is distinct from Admin's local token; authority/lease/code, exact read shape and origin are independently checked. Other raw actor calls, writes, injected targets and redirects remain refused. The auxiliary observer's already-installed strict `Role=aux-client` registration is reused by upstream Caller Add/Use only when no competing AuthorityRole is selected; the upstream isolated-authority profile keeps its original behavior and refuses incoherent combinations.

Both actual local Development UI token issuers sign exactly one explicit `eventstore:surface=ui` claim. OIDC/Production mapping and role semantics remain unchanged. Upstream independently replaced the tracked Sample API test literal with ephemeral hex RNG; that file is deliberately absent from this delta.

## Actual native SDK actor compatibility

The closed internal owner observer captures the six real Dapr1.19.0 framework endpoints: config, health, method, reminder, timer and deactivation. It binds the same installed ActorRuntime/HealthCheckService, the actual SDK actor implementation/interface-method inventory, original sidecar-channel policy, and owning/framework DLL digests. Unknown actors/methods and foreign/malformed caller identities refuse. QueryAsync retains upstream `RouteCatalogProjectionActorPolicy` and the actual committed local actor target. Raw outbound actor service APIs still cannot match this native inventory.

Pinned daprd1.18.7 (`b32a815fa2260983e181b95272ea555fe17d3895`) strips untrusted HTTP caller identity, stamps actual local caller/namespace, and replaces remote identity from SPIFFE. Infrastructure callbacks may omit caller metadata. When present, a single caller/callee must identify the actual owning app, with a nonempty namespace and an exact configured namespace when supplied. `Dapr-API-Call` is never authority. Native and local self service calls retain the existing same-app app-channel trust limitation; this patch does not invent a new cross-protocol guarantee. Authenticated immutable config/health handshakes can precede commit; actor callbacks require the real schema-three Development snapshot, code/inventory binding and durable lease. AggregateActor execution fences, signed admission, tenant checks and actor-body policies are untouched.

The real-sidecar case uses checksum-verified daprd1.18.7, an owned Redis instance and run-private actor namespace, real SDK actor endpoints/registrations, actual Redis authority prepare/ready/commit and leases. It writes projection count7, deactivates it, then queries after reactivation to prove persisted state. It invokes the real AggregateActor activation/read path and schedules an actual Dapr reminder, observes its SDK callback, removes it, and deactivates the projection. Invalid channel, foreign caller and unknown actor/method requests refuse. No leases remain; app, sidecar, Redis and run directory are cleaned up. This case does not claim full business command processing; Root's complete persisted command/query topology qualification remains mandatory after publication. Temporary wire probes are removed.

## Narrow verification

Every lane uses Release, external Hexalith package dependencies, actual root-declared Builds props from commit `81c88b34d09d34260b92d7f0928dbbc1c7446baa`, and explicit source assembly `Version=3.130.0`. Six affected projects build with warnings as errors and zero warnings/errors. Test assemblies run directly with exact class selectors; no solution-wide test run was used.

| Affected classes | Passed | Failed / skipped / not-run |
| --- | ---: | ---: |
| Admin transport/review, actual NativeAdmin worker bootstrap, backup/consistency/infrastructure service classes | 125 | 0 / 0 / 0 |
| Server catalog/schema/lifecycle/isolation/native/caller and affected persistence/publisher/controller classes | 309 | 0 / 0 / 0 |
| ServiceDefaults finite/opt-in/auxiliary/cancellation and upstream GeneratedRest classes | 95 | 0 / 0 / 0 |
| Sample UI signed access-token provider | 35 | 0 / 0 / 0 |
| Admin UI signed access-token role provider | 50 | 0 / 0 / 0 |
| Real native daprd/Redis actor transport case | 1 | 0 / 0 / 0 |
| Actual tracked-source scanner, all candidate paths through an alternate Git index | 1 | 0 / 0 / 0 |

Total: **616 passed**, zero failed/skipped/not-run. The native case completed in 3.635s. The unchanged SDK0 consumer PE compiled against published Contracts3.129.0 runs with the source Contracts3.130.0 DLL: old four-argument constructor and Deconstruct work, and legacy serialized JSON remains exactly AppId/Method/Operation/Domain. Pinned commitlint21.2.4 accepts the exact candidate retained in the report. No commit or push was performed.

Exact commands, source/catalog/runtime identities and hashes, XML/log hashes, actual executed classes, ABI PE identity, cleanup facts and remaining parent gates are in `development-admin-transport-verification.json` beside this spec. Its SHA-256 is `0c628b92ccc36587c9fb49176b763e2186538ff12b5fadbcfa734a4a6cdaee18`. The tracked-source scanner uses the unchanged actual scanner and every new path through a separate candidate index; the normal index remains untouched.

## Exact file scope

- `_bmad-output/implementation-artifacts/development-admin-transport-verification.json`
- `_bmad-output/implementation-artifacts/spec-qualify-development-admin-transport.md`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Services/EventStoreApiAccessTokenProvider.cs`
- `src/Hexalith.EventStore.Admin.Server.Host/Program.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprConsistencyCommandService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminApiAccessTokenProvider.cs`
- `src/Hexalith.EventStore.Contracts/Routing/DevelopmentActorStateTransport.cs`
- `src/Hexalith.EventStore.Contracts/Routing/RouteCatalogAuxiliaryOperation.cs`
- `src/Hexalith.EventStore.Contracts/Routing/RouteCatalogHttpGrant.cs`
- `src/Hexalith.EventStore.Contracts/Routing/RouteCatalogHttpTransport.cs`
- `src/Hexalith.EventStore.Contracts/Routing/RouteCatalogNativeActorTransport.cs`
- `src/Hexalith.EventStore.Contracts/Routing/RouteCatalogReceiverEndpoint.cs`
- `src/Hexalith.EventStore.Contracts/Routing/RouteCatalogTopologyValidator.cs`
- `src/Hexalith.EventStore.Server/Commands/DaprCommandArchiveStore.cs`
- `src/Hexalith.EventStore.Server/Commands/DaprCommandCorrelationIndex.cs`
- `src/Hexalith.EventStore.Server/Commands/DaprCommandStatusStore.cs`
- `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs`
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`
- `src/Hexalith.EventStore.Server/Projections/DaprProjectionChangeNotifier.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs`
- `src/Hexalith.EventStore.Server/Routing/DevelopmentNativeActorReceiver.cs`
- `src/Hexalith.EventStore.Server/Routing/RunScopedRouteCatalogGateway.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Hexalith.EventStore.ServiceDefaults.csproj`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/CommittedRouteCatalogSnapshot.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/DevelopmentActorStateOwner.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/DevelopmentAdminRouteCatalog.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/DevelopmentAuxiliaryClientCatalogHostingStartup.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/DevelopmentAuxiliaryClientCatalogRegistration.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/DevelopmentRouteCatalogCallerExtensions.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/DevelopmentTechnicalHttpTransport.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/FiniteRouteCatalogReceiver.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/IDevelopmentNativeActorReceiver.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/ObservedRouteCatalogHost.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/RouteCatalogActivationBinding.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/RouteCatalogAuthority.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/RouteCatalogHostOptions.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/RouteCatalogInvocationHandler.cs`
- `src/Hexalith.EventStore.ServiceDefaults/Routing/RouteCatalogRemoteCall.cs`
- `src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.SourceProof.cs`
- `src/Hexalith.EventStore/Controllers/AdminStorageCommandController.cs`
- `src/Hexalith.EventStore/Program.cs`
- `src/Hexalith.EventStore/Services/DaprSnapshotPolicyRepository.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Hexalith.EventStore.Admin.Server.Tests.csproj`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Routing/DevelopmentAdminTransportReviewTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Routing/DevelopmentAdminTransportTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Routing/ForeignTechnicalController.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Routing/ForeignTechnicalControllerFeature.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Routing/NativeTechnicalAdminFactory.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Routing/TechnicalAdminHost.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Routing/TechnicalCatalogMemoryStore.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Routing/TechnicalQueryHandler.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminApiAccessTokenProviderRoleTests.cs`
- `tests/Hexalith.EventStore.Sample.Tests/BlazorUI/EventStoreApiAccessTokenProviderTests.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/RouteCatalogDevelopmentSidecar.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/NativeActorCatalogLiveTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Routing/DevelopmentGatewayAdminMetadataTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Routing/DevelopmentNativeActorReceiverTests.cs`
- `tests/Hexalith.EventStore.ServiceDefaults.Tests/DevelopmentAuxiliaryClientCatalogTests.cs`

## Remaining parent gates

Integrate only this manifest relative to upstream5e while preserving the parent's disjoint fixture/packaging commits. Run the broader SDK/hosted gates, publish and independently verify the official package cohort, then run all183 Root live cases with real Counter/Greeting/Parties command persistence, isolation and authorization. This bounded proof is Development-only and supplies no Production trust or complete topology claim. No nested dependency was initialized/updated, no original producer was edited, and no files were deleted.

## Review Triage Log

All three independent layers completed before triage. Each finding is individually verified/graded below before grouping. The accepted corrections preserve the captured Development-only intent and unchanged Production/message eligibility. No human interpretation or approval is needed for these demonstrated defects.

| Finding | Verdict | Route | Evidence and bounded action |
| --- | --- | --- | --- |
| B1 | high | patch | The fallback requires fence-v1 while every new technical host emits v2, so valid nontechnical/lifecycle grants fail preparation. Apply the unchanged strict legacy checks to the retained nontechnical portion of v2; preserve ordinary pattern refusals. |
| B2 | medium | patch | The Admin operation callback recognizes only technical MVC actions, omitting actually mapped workload catalog lifecycle identities. Retain the existing protected lifecycle operation policies and test qualified control-plane grants without expanding caller access. |
| B3 | high | patch | Observed MVC assembly identity and copied SDK descriptor are independent. Official ASP.NET10 ControllerRequestDelegateFactory captures its actual controller descriptor in the delegate closure; a foreign MVC delegate can execute foreign code with copied metadata. Bind actual delegate-to-action identity and reject multicast/foreign action substitution, with a real second MVC action test. |
| B4 | medium | patch | The handler unescapes rawMethod before a matcher that explicitly rejects encoded separators, so the proposed canonical-input guard is bypassed. Validate escaped method representation before decoding while preserving legitimate escaped parameter data; exercise the real outbound handler. |
| B5 | medium | patch | TemplateMatcher omits inline long/regex policies, allowing sender qualification of a route shape that the actual known MVC endpoint cannot accept. Use the retained framework constraints for the known owner templates; preserve successful normal scalar/escaped cases. |
| B6 | medium | patch | ACL admission compares a concrete granted path against a raw parameter template; legitimate narrow grants cannot qualify. Match actual concrete grants against the retained known route shape/constraints and keep ungranted verbs denied. |
| B7 | high | patch | The global HTTP guard now installed by Admin enrollment rejects the existing owner-sidecar GET actor-state read implemented by DaprInfrastructureQueryService. Qualify that existing read-only transport independently with exact configured destination, real scope/lease/code and authentication; raw actor method calls and arbitrary state mutations remain denied. |
| B8 | high | patch | The receiver holds an ambient lease but direct Dapr state mutation RPCs do not mark remote execution, so uncertain writes can release the lease. Instrument actual admitted remote mutation boundaries with existing RouteCatalogRemoteCall/lease mechanisms and test ambiguous/cancelled backend outcomes, without treating handled error responses as proof of no mutation. |
| B9 | medium | patch | Activation still permits schema2 technical metadata but the receiver requires schema3; this can commit an unusable composition. Reject incompatible technical schema2 before activation while preserving compatible nontechnical legacy/ABI behavior. |
| E1 | high | patch | Independent duplicate of the verified B1 legacy/v2 fallback failure. Preserve this row; share the bounded correction only after triage. |
| E2 | high | patch | Independent duplicate of verified B8 uncertain mutation lease release. Actual RPC boundaries must determine uncertainty; do not weaken retirement/lease tests. |
| E3 | high | patch | The claims check independently confirms B3 foreign MVC delegate/SDK metadata substitution. Code provenance must describe the actual selected action, not two separately valid objects. |
| G1 | medium | patch | Accepted pre-verified gap: existing transport hosts manually compose observer/fence/prepare/commit; actual NativeAdmin Program registration with Directory is untested. Exercise production Add/Use bootstrap and real worker observation/activation before serving an authenticated bound request. |
| G2 | high | patch | Accepted pre-verified gap: missing headers and sender-repaired stale headers do not pin independent receiver epoch comparison. Add direct valid-root/wrong-epoch, wrong-root and post-transition stale generation refusals with no durable lease residue. |

Groups after verdicts: B1/E1 legacy v2; B3/E3 action identity; B8/E2 remote uncertainty. Other survivors have distinct demonstrated causes. Verification-gap evidence is accepted under its independent source/test tracing. Official MVC source used for B3: https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.0/src/Mvc/Mvc.Core/src/Routing/ControllerRequestDelegateFactory.cs lines68-95 (captures its controller descriptor). Root full Dapr topology remains mandatory after the corrected owner package is public.
