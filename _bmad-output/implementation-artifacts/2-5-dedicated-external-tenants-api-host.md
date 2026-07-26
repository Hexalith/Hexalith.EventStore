---
created: 2026-07-15
story_id: "2.5"
story_key: 2-5-dedicated-external-tenants-api-host
status: review
split_from: 2-4-tenants-external-api-host-adoption
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 2.5: Dedicated External Tenants API Host

Status: review

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

## Dev Agent Record

### Debug Log

- 2026-07-26: Reconciled the stale completion-gate note against the Tenants repository. AD-18 patch commit `846f988a5f2fe1bce2e4fdb5a42b7c1c63ba61ae` is an ancestor of current `origin/main` at `6cc9eb3a44f45417aac76d7def9daba7544cd2fa`; the EventStore root pins that final Tenants SHA.
- 2026-07-26: Verified GitHub user `jpiquot` still has Tenants repository `admin` permission and is both author and committer of the patch commit, satisfying the split story's accepted direct-admin commit authority model.
- 2026-07-26: The broad command `dotnet restore Hexalith.Tenants.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false -p:NuGetAudit=false` is environment-blocked because the solution declares intentionally uninitialized nested `references/*` projects. Repository policy forbids initializing those nested submodules.
- 2026-07-26: The first focused integration attempt encountered a transient duplicate-build reference-assembly file lock. The required serialized retry (`-m:1 -p:BuildInParallel=false -p:RestoreBuildInParallel=false`) passed.

### Completion Notes

- Accepted the committed external-host boundary at Tenants patch commit `846f988a5f2fe1bce2e4fdb5a42b7c1c63ba61ae` and final Tenants SHA `6cc9eb3a44f45417aac76d7def9daba7544cd2fa` on `origin/main`.
- Accepted scope: inbound authentication remains host-owned; generated controllers are the only API controllers; delegation uses the EventStore gateway client; the platform-owned DAPR invocation handler replaces untrusted routing headers and is innermost after bearer forwarding; the API has no domain/UI dependency or direct persistence access.
- Release/package-mode API build passed with 0 warnings and 0 errors.
- Focused gateway-handler and structural tests passed 17/17.
- Full configured regression lanes passed: Contracts 113/113, Client 50/50, Testing 181/181, UI 1031/1031, Sample 39/39, Server 738/738, and non-performance Integration 166/166; no failures or skips (2318 unique tests total).
- No new Tenants source change was required in this run because the reviewed patch had already landed on `origin/main`; this run closed the documented external acceptance gate and refreshed story/sprint tracking.

## File List

- `references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Program.cs` (modified in accepted patch commit)
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Services/DaprAppIdHandler.cs` (deleted in accepted patch commit)
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsApiGatewayHandlerTests.cs` (modified in accepted patch commit)
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsApiStructuralTests.cs` (modified in accepted patch commit)
- `_bmad-output/implementation-artifacts/2-5-dedicated-external-tenants-api-host.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

- 2026-07-26: Recorded the accepted post-patch Tenants commit/final SHA and host scope, refreshed validation evidence, and moved Story 2.5 to review.
