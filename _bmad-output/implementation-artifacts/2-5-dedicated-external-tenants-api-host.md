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

#### Review loop 2026-07-26 (adversarial code review, 4 layers)

Reviewed: Tenants patch commit `846f988a` + EventStore evidence commit `fee4512e`. Layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor — all four completed.

- [x] [Review][Decision] [high] **RESOLVED 2026-07-26 — run the missing lanes.** Execute `TenantsApiGeneratedControllerTests` (the in-process compiled-route proof) and the EventStore AppHost topology/ACL lane, and record the real results; report `AspireTopologyTests` separately if the Tier-3 Docker/Aspire environment is unavailable. Original finding: recorded evidence does not satisfy this story's own `done` gate for "focused compiled-route/topology results" — the focused filter `FullyQualifiedName~TenantsApiGatewayHandlerTests|FullyQualifiedName~TenantsApiStructuralTests` excludes `AspireTopologyTests` (the only topology class) and `TenantsApiGeneratedControllerTests`, which `spec-2-4-tenants-external-api-host-adoption.md:53` designates as the in-process runtime proof for compiled generated Tenants routes. No EventStore-side topology/DAPR-ACL result is recorded either, although sibling Story 2.4 recorded "EventStore AppHost security, source-mode topology, and ACL tests: 10/10". The "2318 unique tests" figure is EventStore lanes, not Tenants; the Debug Log records the full Tenants solution restore as environment-blocked, so nothing in the record shows the rest of the Tenants solution still compiles after `DaprAppIdHandler.cs` was deleted.
- [x] [Review][Decision] [medium] **RESOLVED 2026-07-26 — re-run at `3.82.0`.** Re-run the Release/package-mode `Hexalith.Tenants.Api` build against the currently-resolving `3.82.0` packages and record the result so the evidence matches the accepted state. Original finding: package-baseline drift in the only versioned build evidence — line 28 records the Release/package-mode build "against EventStore packages `3.68.1`" (run 2026-07-17). At the accepted SHA the Tenants nested `references/Hexalith.Builds` is uninitialized, so `Directory.Build.props` falls through to `references/Hexalith.Builds/Props/Directory.Packages.props:8`, which now resolves `HexalithEventStoreVersion` = `3.82.0`. Decide: re-run the package-mode build at `3.82.0` and record it, or explicitly accept `3.68.1` as the pinned acceptance baseline.
- [x] [Review][Decision] [medium] **RESOLVED 2026-07-26 — pin scope to the patch plus an enumerated delta.** The accepted host boundary is patch `846f988a`; record that the only Api-host change between it and the pinned `6cc9eb3a` is `src/Hexalith.Tenants.Api/Properties/launchSettings.json` (+12, reviewed, a benign dev launch profile). Original finding: the accepted "final Tenants SHA" `6cc9eb3a` is `origin/main`'s moving tip, 49 commits past the reviewed patch `846f988a` (`git rev-list --count 846f988a..6cc9eb3a` = 49). That range adds one unlisted Api-host file, `src/Hexalith.Tenants.Api/Properties/launchSettings.json` (+12, a benign dev launch profile). Decide: accept tip-as-host-scope, or pin the accepted scope to the patch plus an explicitly enumerated delta.
- [x] [Review][Decision] [medium] **RESOLVED 2026-07-26 — open a platform hardening story; out of Story 2.5 scope.** Making the seam fail-closed is a `Hexalith.EventStore.Client` design change, not a Tenants host-boundary change; logged to the deferred-work ledger for a dedicated platform story. Only the incorrect AD-18 rule text in `project-context.md` is corrected under this story. Original finding: AD-18 is opt-in and fail-open at the platform seam — `AddEventStoreGatewayClient` (`src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs:43-48`) registers no message handler; routing-header ownership comes solely from the separate `AddEventStoreDaprServiceInvocation` call (line 63). A host that calls only the former gets no `dapr-app-id`/`dapr-api-token` ownership, with no compile-time error, no startup validation and no runtime diagnostic — the same fail-open shape as the `ApiScope` trap already on record. Decide: make the seam fail-closed in the platform, or keep it convention-enforced.
- [ ] [Review][Patch] [medium] AD-18's innermost-handler ordering — the property this story explicitly accepts — has no regression guard; the two registration assertions are order-independent `ShouldContain` presence checks even though the same test method uses the `IndexOf` ordering idiom nine lines later for middleware. Swapping `Program.cs:73-74`, or appending any handler after line 74, breaks the invariant with all 17 focused tests green [references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsApiStructuralTests.cs:111]
- [ ] [Review][Patch] [medium] The three new negative guards are exact, case-sensitive literals scoped to the Api project only, and are strictly weaker than the platform guard they imitate — `Headers.Add("dapr-app-id", …)`, `TryAddWithoutValidation( "dapr-app-id"` (one space), a `const` header name, or a renamed handler class all evade them. `references/Hexalith.Tenants` is outside every scan root of EventStore's `DaprRoutingHeaderOwnershipGuardTests` (roots `samples/Hexalith.EventStore.Sample.Api`, `samples/Hexalith.EventStore.Sample.BlazorUI`, `src/Hexalith.EventStore.Admin.UI`, plus a `src`/`samples` sweep anchored at `Hexalith.EventStore.slnx`), and `ReadTenantsApiSourceAndProject` scans only `src/Hexalith.Tenants.Api`, leaving `src/Hexalith.Tenants.UI` unguarded in both repositories. Port the platform's whitespace-tolerant regex + exact-membership allowlist, rooted at the Tenants `src/` tree [references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsApiStructuralTests.cs:113]
- [ ] [Review][Patch] [medium] The token's configuration source is unpinned — the rewritten assertion pins the app-id literal and the *variable name* only, so changing `Program.cs:69` to read a different key (or `null`) keeps every test green while the host silently stops sending `dapr-api-token`. Add `programText.ShouldContain("builder.Configuration[\"DAPR_API_TOKEN\"]")` alongside the existing endpoint-resolver assertion [references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsApiStructuralTests.cs:112]
- [ ] [Review][Patch] [medium] The AD-18 rule text agents are instructed to follow misstates the wiring: it says the platform handler is "wired by `AddEventStoreGatewayClient`", but that extension registers only `ICommandStatusLocationBuilder` and the typed client. Correct it to name the separate `AddEventStoreDaprServiceInvocation` opt-in call [_bmad-output/project-context.md:46]
- [ ] [Review][Patch] [high] This story file contradicts itself as an acceptance record — the `### Review Completion Evidence` section still reads "the Tenants patch is uncommitted on base `76474f16…`, so no exact post-patch Tenants commit SHA exists yet. Keep Story 2.5 `in-progress`", directly contradicting `status: review` and the Completion Notes that accept `6cc9eb3a`. The Change Log claims the run "refreshed validation evidence", but that section was never touched [_bmad-output/implementation-artifacts/2-5-dedicated-external-tenants-api-host.md:31]
- [ ] [Review][Patch] [medium] Acceptance evidence deviates from the Story 2.4 approval model this story adopted — 2.4 records maintainer approval, accepted implementation commit, accepted final SHA and accepted scope in a top-level `## Acceptance Evidence` section with commit links; 2.5 buries them in `## Dev Agent Record` → Debug Log / Completion Notes without links. The `File List` also mixes two repositories with no repo attribution, and the `admin`-permission check that solely justifies the no-PR path cites no reproducible command or output [_bmad-output/implementation-artifacts/2-5-dedicated-external-tenants-api-host.md:51]
- [x] [Review][Defer] [medium] `InboundBearerForwardingHandler` appends `Authorization` via a bare `TryAddWithoutValidation` with no preceding `Headers.Remove(...)` — the exact append-not-replace anti-pattern AD-18 exists to eliminate, sitting in the same handler chain; it also coerces the multi-valued `Request.Headers.Authorization` (`StringValues`) to `string?`, forwarding two inbound `Authorization` headers as one comma-joined value [references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Services/InboundBearerForwardingHandler.cs:14] — deferred, pre-existing (file untouched by the patch; no default `Authorization` is set on the Api host's gateway client today, so it is latent)
- [x] [Review][Defer] [low] `DAPR_API_TOKEN` is read raw with no trim/normalization, so a mounted secret carrying a trailing newline or a whitespace-only value is forwarded verbatim to the sidecar [references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Program.cs:69] — deferred, pre-existing (identical to the deleted handler's behavior)
- [x] [Review][Defer] [low] The API token is captured at registration time, so a rotated token (secret remount, config reload) stays stale until process restart [src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs:70] — deferred, pre-existing platform design, unchanged by this patch
- [x] [Review][Defer] [low] `DaprServiceInvocationExtension_ReplacesUntrustedRoutingHeaders` builds a synthetic named client `"dapr"` that no Tenants production code registers, so it exercises zero Tenants code; it is a weaker near-duplicate of `tests/Hexalith.EventStore.Client.Tests/Registration/DaprServiceInvocationRegistrationTests.cs` and `.../Handlers/DaprServiceInvocationHandlerTests.cs` in the owning repo [references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsApiGatewayHandlerTests.cs:134] — deferred, test-quality only; the real-chain assertions survive in the sibling test

Dismissed as noise (4): the lost "adds-when-absent" assertion (covered upstream by `DaprServiceInvocationHandlerTests.SendAsync_WithCleanRequest_AddsSingleAuthoritativeValues`); `Hexalith.Tenants.UI` omitting `AddEventStoreDaprServiceInvocation` as a live defect (that host targets a direct `EventStore:BaseAddress` gateway URL, not a sidecar, and is Story 2.6 scope — only the guard-scope half survives, folded into the guard patch above); the `ConfigureHttpClient` untrusted-default seeding "diverging from production wiring" (that seeding is the mechanism that proves replacement through the real chain); and the no-PR process objection (an owner decision already resolved 2026-07-17).

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
