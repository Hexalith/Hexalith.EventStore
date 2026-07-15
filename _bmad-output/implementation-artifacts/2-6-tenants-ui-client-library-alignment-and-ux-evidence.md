---
created: 2026-07-15
story_id: "2.6"
story_key: 2-6-tenants-ui-client-library-alignment-and-ux-evidence
status: in-progress
split_from: 2-4-tenants-external-api-host-adoption
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 2.6: Tenants UI Client-Library Alignment And UX Evidence

Status: in-progress

The parent Story 2.4 spec records UI-boundary guardrails. This child reviews typed-client
consumption, absence of generated/hand-written per-message controllers, and canonical UX
states. Only `ProjectionBacked` evidence may claim lifecycle state; handler-computed,
missing, or invalid provenance renders `Unknown`. `done` requires Sally's focused UX
review, Tenants maintainer approval, the exact Tenants SHA, and structural/UI test evidence.

### Review Findings

- [x] [Review][Patch] Reject non-projection-backed lifecycle evidence before mapping UI freshness [references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs:792]
- [x] [Review][Patch] Evaluate the effective UI dependency graph instead of raw project `Include` text [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:166]
- [x] [Review][Patch] Verify compiled UI controllers and runtime endpoints instead of literal source markers [references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:186]

#### 2026-07-15 Review Resolution

- Patched UI freshness mapping so only projection-backed provenance can produce current or stale lifecycle evidence; handler-computed, missing, and invalid provenance remain unknown.
- Replaced raw project/source scans with evaluated source/package dependency checks, compiled-controller inspection, and an in-process endpoint inventory.
- Validation: Release package-mode build passed with zero warnings/errors; focused query-gateway tests passed 103/103; composition/runtime-boundary tests passed 16/16; the full UI test assembly passed 881/881; Debug source-mode build passed with the unrelated Memories source edge pinned to package mode.
- Remaining completion gates: Sally's focused UX review, Tenants maintainer approval, and an exact committed Tenants SHA are not yet recorded. The story therefore remains `in-progress`.

## Dev Agent Record

### Debug Log References

- `dotnet build tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false` -> **Build succeeded. 0 warnings, 0 errors.**
- `dotnet tests/Hexalith.Tenants.UI.Tests/bin/Debug/net10.0/Hexalith.Tenants.UI.Tests.dll -class Hexalith.Tenants.UI.Tests.Services.Gateways.TenantQueryGatewayTests` -> **103/103 passed.**
- `dotnet tests/Hexalith.Tenants.UI.Tests/bin/Debug/net10.0/Hexalith.Tenants.UI.Tests.dll -class Hexalith.Tenants.UI.Tests.TenantsUiCompositionTests` -> **16/16 passed.**
- `dotnet tests/Hexalith.Tenants.UI.Tests/bin/Debug/net10.0/Hexalith.Tenants.UI.Tests.dll` -> **881/881 passed.**
- Root and Tenants `git diff --check` -> **passed.**

### Completion Notes

- AC1 implementation evidence is green: the evaluated UI dependency graph retains the typed Tenants client seam and excludes the external API host, REST generator, and domain-service host; compiled-controller and in-process endpoint inventories are empty for forbidden API surfaces.
- AC2 implementation evidence is green: only projection-backed metadata can resolve current or stale freshness; handler-computed, missing, unknown, and invalid provenance resolve to unknown.
- AC3 remains blocked: Sally's focused UX acceptance and Tenants maintainer approval are not recorded, and the four Tenants implementation/test changes are uncommitted on base SHA `28630b94a7b4931dcd6796eb50ad1c21b092055d`, so no exact approved implementation SHA exists.
- Story status remains `in-progress`; it is not ready to advance to `review` or `done` until the external acceptance and exact-SHA gates are satisfied.

### File List

**Tenants production (modified):**
- `references/Hexalith.Tenants/src/Hexalith.Tenants.UI/Services/Gateways/TenantQueryGateway.cs`

**Tenants tests (modified):**
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/Services/Gateways/TenantQueryGatewayTests.cs`
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs`

**Artifacts (modified):**
- `_bmad-output/implementation-artifacts/2-6-tenants-ui-client-library-alignment-and-ux-evidence.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

### Change Log

- 2026-07-15: Revalidated the completed provenance and UI-boundary patches in Debug/source mode: build 0 warnings/errors, gateway tests 103/103, composition tests 16/16, and full Tenants UI tests 881/881. Added the exact file inventory and retained `in-progress` because UX/maintainer approval and an exact committed Tenants SHA remain unavailable.
