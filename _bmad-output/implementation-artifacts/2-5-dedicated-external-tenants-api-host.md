---
created: 2026-07-15
story_id: "2.5"
story_key: 2-5-dedicated-external-tenants-api-host
status: in-progress
split_from: 2-4-tenants-external-api-host-adoption
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 2.5: Dedicated External Tenants API Host

Status: in-progress

The parent Story 2.4 spec records the dedicated generated host, AppHost/ACL wiring, and
runtime tests. This child reviews the host boundary: inbound auth, generated controllers,
gateway-client-only delegation, no domain/UI dependency, and no direct persistence access.
`done` requires the Tenants maintainer-approved PR/commit, exact Tenants SHA, accepted host
scope, and focused compiled-route/topology results. Historical authority remains the parent
spec and implementation review.

### Review Findings

- [x] [Review][Decision] Record the external host-boundary acceptance evidence — **RESOLVED 2026-07-17:** reuse Story 2.4's approval model: the direct admin-authored Tenants commit chain is accepted as maintainer authority for this split story. The final post-patch Tenants commit SHA, explicit accepted host scope, and focused results remain the completion gate; the pre-patch SHA `76474f16ad40f113273e60f662f69493775c5cc4` is not final evidence.
- [x] [Review][Patch] Replace the Tenants-local append-only `DaprAppIdHandler` with the platform-owned, replace-not-append `AddEventStoreDaprServiceInvocation` handler required by AD-18, then update the handler-chain and structural tests [references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Program.cs:74; references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Services/DaprAppIdHandler.cs:8] — **APPLIED 2026-07-17:** removed the local handler, made the platform handler innermost after bearer forwarding, and added replacement plus structural guard assertions.

### Review Completion Evidence

- Release/package-mode API build: `dotnet build src/Hexalith.Tenants.Api/Hexalith.Tenants.Api.csproj --configuration Release -p:UseHexalithProjectReferences=false -p:NuGetAudit=false` — passed with 0 warnings and 0 errors against EventStore packages `3.68.1`.
- Focused package-mode integration/structural lane: `dotnet test tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj --configuration Release -p:UseHexalithProjectReferences=false -p:NuGetAudit=false --filter "FullyQualifiedName~TenantsApiGatewayHandlerTests|FullyQualifiedName~TenantsApiStructuralTests"` — 17/17 passed.
- Source-mode attempt: restore required allowing only the pre-existing FrontComposer `NU1506` duplicate-version warning; compilation then stopped because the forbidden-to-initialize nested `references/Hexalith.Memories/references/Hexalith.EventStore` submodule is absent. No nested submodule was initialized.
- Completion gate: the Tenants patch is uncommitted on base `76474f16ad40f113273e60f662f69493775c5cc4`, so no exact post-patch Tenants commit SHA exists yet. Keep Story 2.5 `in-progress` until the accepted commit and host scope are recorded.
