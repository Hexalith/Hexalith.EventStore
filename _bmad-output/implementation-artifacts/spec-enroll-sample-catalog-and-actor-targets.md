---
title: Enroll the existing sample and its gateway actor query
status: in-progress
created: 2026-10-31
baseline_commit: c02afa0fce310ccdb696232f18829df96cba088e
---

## Intent and constraints

The latest published SDK requires real executable catalogs. Its own native Counter/Greeting sample currently declares routes and intent policies without schema-three operations or Development enrollment, while the shared domain observer rejects its existing gateway-owned ProjectionActor query. Fix this compatibility gap without changing sample command behavior, target-only intent policy, actual Counter query transport, authentication, mTLS, runtime authority/trust policy, source selection or publish defaults.

Reuse compiled owner declarations, RouteOperationSchema, the existing immutable owner adapters and real SDK bootstrap. Restrict public sample ingress to its existing UI use; projection dispatch stays internal. Enroll only when the owning Development runner explicitly supplies its private bootstrap configuration. No declaration/file alone activates admission.

Domain observation must keep its complete owner declarations while claiming only actual installed domain-service targets. Only the declared canonical ProjectionActor query may be delegated to the gateway; unsupported target kinds and undeclared local handlers still refuse. Gateway observation must independently verify its installed projection actor and invoker before claiming QueryAsync, and use its actual configured app identity instead of a fixed name. Existing shared observer checks complete generation, identity, topology, targets, policies, schema and lease without relaxation.

## Scope

- DomainService DevelopmentDomainRouteCatalog local-target selection.
- Server RunScopedRouteCatalogGateway actual app identity and installed actor-query target observation.
- Native Sample Program, Counter/Greeting operation inventories, adapter-source registration and an exact counter-result contract.
- Native Sample.Api explicit generated-host observation if required by the actual existing generated filter; no external eligibility or authority grants.
- Focused existing Server/Sample tests and bounded new catalog/schema/target negatives.

The parent root qualifier owns sample Contracts assembly separation and actual private topology/bootstrap. The other EventStore implementer owns six existing live-sidecar fixture failures. Neither work overwrites the other's files. All final owner changes require independent review and live/CI verification before new package publication.

## Verification

Build isolated Release with published external dependencies and explicit top-level Builds catalog, no nested initialization. Test actual installed receiver and actor inventories, wrong actor/invoker/identity/target kinds, exact schema/adapter coverage and inert bootstrap without explicit runner configuration. Preserve existing query/admission/lease tests and run the full actual Counter/Greeting/Parties persisted isolated topology in the parent on the eventual published SDK.

## Sample API common catalog gate and strict test authority

The generated API now registers `AddEventStoreRouteCatalog` unconditionally; only explicit private Development configuration registers activation/observation. Its native unenrolled readiness and generated dispatch fail503 while liveness stays200.

Necessary additional scope: Sample.Api Program and the existing authentication/generated-response Sample.Tests plus new immutable-catalog/filter/readiness helpers and `sample-api-catalog-gate-verification.json`. No DomainService/Server mixed-target file, owner operation/schema/adapters inventory or parent topic shim was changed for this correction.

Removed the authentication startup filter that assigned `RouteCatalogGeneratedAction.ValidatorKey`. Authentication hosts instead install a strict read-only schema2 committed snapshot at the public authority seam, retaining the real generated filter and real dispatch lease. Isolated status/payload/action tests now execute that same real filter before actual generated actions; no Sample.Tests file assigns the generated callback directly. The immutable fixture declares only the five Counter routes and exact tenant-a binding, rejects undeclared/outside-tenant resolution, and refuses prepare/ready/commit/rollback/reconcile.

Focused verification: Release Sample.Tests build with explicit local environment pin `-p:Version=3.129.1`, external dependencies, the existing root-declared Builds catalog and warnings-as-errors: zero warnings/errors. Direct DLL selectors `*SampleApiHostMiddlewareTests`, `*SampleApiGeneratedControllerRuntimeTests`, `*HttpHostSecurityTests`, `*SampleApiCatalogReadinessTests`: **31 passed, zero failed/skipped**, 1.284s. Existing authentication (including Production RSA validation), response/status/payload/header assertions remain. Added actual signed schema3 UI success and missing/ineligible-surface denials with catalog acquisition but zero gateway calls. The actual native child API process's private bootstrap worker records exactly five compiled Counter declaration digests, five compiled operation digests and suffixed app ID `sample-api-session-one`; no activation/committed document exists, and the owned process is reaped before scratch deletion. This is observation proof, not an activation claim.

Compact source/command/XML/log proof: `sample-api-catalog-gate-verification.json`, SHA256 `aaf5bbdf2c5359ea80328ace6e8c17a4a9e0ec179fc98bf45bd95b87c3a9ab85`. Detailed temporary logs: `/tmp/es-sample-api-common-build-3.log`, `/tmp/es-sample-api-common-tests-final.log`; test XML `/tmp/es-sample-api-common-tests-final.xml`. No commit/push; parent owns combined review and complete live/bootstrap validation.
