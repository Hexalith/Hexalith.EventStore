---
title: Enroll explicit Development auxiliary catalog clients
status: in-progress
baseline_commit: c02afa0fce310ccdb696232f18829df96cba088e
---

# Explicit Development auxiliary clients

The root package-profile topology must restore published EventStore SDK libraries for its native peer entry projects, and every real Dapr caller must participate in the committed Development catalog. Default source builds and publishing remain unchanged. No nested submodule initialization, fake authority, Production trust change, candidate-derived receiver inventory or removal of an existing participant is permitted.

Scope: five peer entry/contracts csprojs (Sample.Api, Sample.Contracts, Sample.BlazorUI, Admin.Server.Host, Admin.UI); new ServiceDefaults auxiliary-client observer/hosting-startup files; focused ServiceDefaults tests and package-reference evaluation tests; this spec. Parent owns Sample.csproj, native Sample/domain/gateway observer fixes and topic shim in another worktree. Existing six live-fixture changes remain separate and uncommitted awaiting combined review.

Opt-in package property: `HexalithUsePublishedSdkForHostedTests=true`; SDK package versions are centrally selected through existing `HexalithEventStoreVersion`. Preserve owned application/contracts project edges and generator analyzer packaging. No new version pin.

Auxiliary role: `EventStore:RouteCatalog:DevelopmentBootstrap:Role=aux-client`, actual ServiceDefaults hosting-startup assembly explicitly selected, Development environment plus existing complete private bootstrap configuration. The observer must find no generated, domain or actor SDK receiver and the actually loaded receiver ACL must deny every incoming operation. HTTP-only human admin/Blazor endpoints may remain present; report their actual metadata without inventing catalog targets, declarations, admission, operations or allowed receivers. Reuse actual Inputs, ObservedRouteCatalogHost, Prepare/Ready/Commit and authority; every mismatch fails closed.

## Implementation and focused proof

The five csprojs now switch only their SDK-library edges when `HexalithUsePublishedSdkForHostedTests=true`. Owned Sample.Contracts application edges remain project references. Package references have no local versions; the actual owning central catalog/global `HexalithEventStoreVersion` selects them. The generator stays analyzer-only through its published analyzer assets (`PrivateAssets=all`).

`DevelopmentAuxiliaryClientCatalogHostingStartup` is exported through the ServiceDefaults assembly's hosting-startup attribute. It remains inert without `Role`, and refuses a wrong role, non-Development environment or missing private bootstrap directory. The explicit runner must list `Hexalith.EventStore.ServiceDefaults` in `ASPNETCORE_HOSTINGSTARTUPASSEMBLIES` and provide `Role=aux-client`. `DevelopmentAuxiliaryClientCatalog.CaptureAsync` independently reads actual materialized routing endpoints, rejects installed domain/actor/gateway receiver services and compiled generated/domain/actor endpoint markers or reserved SDK routes, compares actual configured app IDs, validates the actually loaded deny-all ACL and empty independently declared receiver invocations, and recaptures every fact during existing authority observation. It returns empty owned declarations/targets/admission/projection/auxiliary/operation lists and the complete human HTTP endpoint inventory marked unadmitted. No catalog authority/trust/lifecycle implementation is replaced.

Admin.Server.Host's tracked ACL explicitly denies all incoming Dapr invocations; its real human HTTP controllers remain permitted. Sample.BlazorUI owns Razor/Blazor HTTP routes and no SDK receiver. A profile that adds any receiver grant or SDK receiver is refused rather than treated as client-only.

Focused checks in detached worktree `/tmp/eventstore-auxiliary-client-catalog`:

- Source ServiceDefaults tests build with warnings-as-errors, external NuGet dependencies and local `Version=3.129.1`: zero warnings/errors. Exact class selector `-class '*DevelopmentAuxiliaryClientCatalogTests'`: **14 passed, zero failed/skipped**, 0.399s. Real Kestrel requests prove actual HTTP endpoint observation; negatives cover generated/domain/actor paths, case-insensitive paths, operation/default ACL grants, declared incoming invocations, wrong app/environment/profile and missing opt-in.
- `HEXALITH_BUILD_PACKAGE_PROPS=<root-declared Builds>/Props/Directory.Packages.props python3 -m unittest discover -s tools/tests -p 'test_hosted_peer_sdk_references.py'`: **one test passed**, 20 actual MSBuild evaluations across all five projects in default, explicit-false, default-publish and explicit-true modes. Explicit synthetic `9.8.7` version override proves central version selection without a release-specific pin.
- Actual Release package-profile builds of Sample.Api, Sample.BlazorUI, Admin.Server.Host and Admin.UI against the existing **published3.129.0** family: all four passed with zero warnings/errors. Every selected SDK library in all five actual assets files has type `package` and exact version3.129.0. The published REST analyzer generated the actual CounterRestController. No unpublished package was claimed available.
- Ordinary Sample.Api Release build without the property passed with zero warnings/errors, compiling source Client/Contracts/ServiceDefaults and the source generator.
- Pinned owner commitlint21.2.4 validated the isolated candidate in `/tmp/es-aux-commit-message.txt`: zero problems/warnings. No commit or push; combined final message needs its own validation.

Compact permitted tracked summary: `development-auxiliary-catalog-verification.json`, SHA256 `4411aa09639b4e049911a3c55dd94702d0db5b8b25aab85dc9b8066aa2034bbb` (9,639 bytes), with exact build/test command arrays, XML/log/source/asset hashes and explicit limits. Detailed temporary logs use `/tmp/es-aux-*`; generated-controller proof is `/tmp/es-aux-api-generated`. No evidence folder, nested initialization, root producer write, version/gitlink change or parent's Sample.csproj/code edit occurred.

Parent owns combined independent review, integration with its native Sample/gateway changes, actual complete multi-host bootstrap and publication. This proof qualifies the new source API and current public package graph; it does not claim the new3.129.1 package has already been published.
