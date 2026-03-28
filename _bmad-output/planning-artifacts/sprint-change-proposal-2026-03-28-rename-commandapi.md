# Sprint Change Proposal — Rename CommandApi to EventStore

**Date:** 2026-03-28
**Author:** Bob (Scrum Master agent)
**Scope Classification:** Moderate
**Status:** Draft

---

## Section 1: Issue Summary

**Problem:** The main API host project is named `Hexalith.EventStore.CommandApi` with DAPR AppId `commandapi`. This name is misleading — the project now hosts commands, queries, projections, SignalR hubs, and admin endpoints. The name should reflect the product identity: `Hexalith.EventStore` with AppId `eventstore`.

**Discovery:** Strategic naming decision by Jerome. The project evolved beyond its original "Command API" scope during implementation of the query pipeline (Epic 9-12), SignalR real-time notifications (Epic 10), and admin endpoints (Epic 14+).

**Evidence:**
- Project hosts 8+ controller types: Commands, Queries, CommandStatus, Replay, ProjectionNotification, QueryValidation, CommandValidation, AdminStreamQuery, AdminTraceQuery
- SignalR hub (`ProjectionChangedHub`) serves real-time projection updates
- The name `CommandApi` confuses new developers about the project's scope

---

## Section 2: Impact Analysis

### Epic Impact
- No epics blocked, invalidated, or reordered
- ~11 text references in `epics.md` need updating (project name, AppId, host references)
- All existing and future epics referencing `CommandApi` as a service target must use `eventstore`

### Artifact Conflicts

| Artifact | Impact | Refs |
|----------|--------|------|
| PRD | None — uses "Command API" conceptually, not as project name | 0 |
| Architecture | Major — project structure, dependency tables, code examples, ADR-P4 | ~40 |
| Epics | Minor — host name references in stories | ~11 |
| UX Design | Minor — Swagger UI context | ~6 |

### Technical Impact

| Category | Files | Occurrences |
|----------|-------|-------------|
| Source project (rename dir + namespaces) | ~35 .cs files | All namespace declarations |
| Solution file + .csproj references | 4 files | 4 path references |
| Dockerfile | 1 file | 5 path/DLL references |
| DAPR access control YAML (local + prod) | 4 files | ~10 comment references |
| DAPR component YAML (statestore, pubsub, configstore, resiliency) | 16 files | ~70 scope/comment references |
| Aspire extensions (public API) | 2 files | Parameters, variables, XML doc |
| AppHost Program.cs | 1 file | Variables, project ref, comments |
| Server.Tests (using statements) | 55 files | 104 occurrences |
| Server.Tests (string literals "commandapi") | ~20 files | ~50 occurrences |
| IntegrationTests (directory rename + namespaces) | 15 files | Namespace + using statements |
| IntegrationTests (fixtures, "commandapi" strings) | ~5 files | ~10 occurrences |
| Admin.Server (CommandApiAppId property) | 10+ service files | Property name + default value |
| Admin.Server.Tests | ~10 files | Constant + assertion values |
| Admin.UI.Tests | 2 files | String literals |
| Admin.Abstractions.Tests | 1 file | String literal |
| Sample BlazorUI (code + config) | 6 files | URLs, config keys, comments |
| Sample DAPR components | 4 files | Scope lists |
| Sample deploy configs | 3 files | AppId, annotations, params |
| SignalR NuGet package | 1 file | XML doc example |
| Documentation (docs/) | ~12 files | Project names, AppIds, URLs |
| Deploy README + YAML | ~5 files | AppId, service names |
| CI/CD (GitHub Actions) | 1 file | Dockerfile path, image name, K8s deployment |
| CLAUDE.md | 1 file | Project structure section |
| Planning artifacts | 3 files | Architecture, Epics, UX Design |

**Total estimated: ~250 files, ~500+ individual replacements**

### Namespace Collision Risk
`Hexalith.EventStore.CommandApi.SignalR` -> `Hexalith.EventStore.SignalR` collides with the existing `Hexalith.EventStore.SignalR` NuGet package project. Resolution: rename to `Hexalith.EventStore.SignalRHub` or similar non-colliding namespace.

---

## Section 3: Recommended Approach

**Selected: Direct Adjustment**

**Rationale:**
- The change is entirely mechanical — rename project directory, update namespaces, update AppId strings
- No business logic changes, no architectural redesign
- The codebase compiles or it doesn't — easy to validate with `dotnet build` + `dotnet test`
- Pre-v1 project — breaking changes to public API (Aspire NuGet package, Admin.Server config) are acceptable
- Risk is low: deterministic text replacements with immediate build feedback

**Effort:** Medium (broad but mechanical — ~250 files)
**Risk:** Low
**Timeline impact:** One focused implementation task

---

## Section 4: Detailed Change Proposals

### Proposal 1: Project Directory and Solution File Rename
- `src/Hexalith.EventStore.CommandApi/` -> `src/Hexalith.EventStore/`
- `.csproj` renamed from `Hexalith.EventStore.CommandApi.csproj` -> `Hexalith.EventStore.csproj`
- Solution file `.slnx` path updated
- AppHost `.csproj` project reference updated
- Server.Tests `.csproj` project reference updated
- IntegrationTests `.csproj` project reference updated

### Proposal 2: DAPR AppId Change
- AppId `"commandapi"` -> `"eventstore"` in Aspire extensions and AppHost
- All variable names: `commandApi` -> `eventStore`, `commandApiHttps` -> `eventStoreHttps`, etc.
- Aspire resource name: `"commandapi"` -> `"eventstore"`

### Proposal 3: Namespace Rename Across All Source Files
- `Hexalith.EventStore.CommandApi.*` -> `Hexalith.EventStore.*` across 12 sub-namespaces
- ~35 source files + ~70 test files with `using` statements
- Collision avoidance: `CommandApi.SignalR` -> `Hexalith.EventStore.SignalRHub`

### Proposal 4: Aspire Extension API Surface Change
- `HexalithEventStoreResources.CommandApi` -> `.EventStore`
- `AddHexalithEventStore(commandApi, ...)` -> `AddHexalithEventStore(eventStore, ...)`
- `commandApiDaprConfigPath` -> `eventStoreDaprConfigPath`
- All XML doc updates

### Proposal 5: AppHost Program.cs Variable and Comment Updates
- All `commandApi*` variables -> `eventStore*`
- `Projects.Hexalith_EventStore_CommandApi` -> `Projects.Hexalith_EventStore`
- All inline comment updates

### Proposal 6: DAPR Access Control YAML Files (4 files)
- Comments: `commandapi sidecar` -> `eventstore sidecar`
- Local + production versions

### Proposal 7: Dockerfile Update
- All project paths updated
- Entry point: `Hexalith.EventStore.CommandApi.dll` -> `Hexalith.EventStore.dll`

### Proposal 8: IntegrationTests Directory Rename
- `tests/.../CommandApi/` -> `tests/.../EventStore/`
- Namespace update for 15 test files

### Proposal 9: Server.Tests Namespace and String Literal Updates
- 104 `using` statement updates across 55 files
- ~50 `"commandapi"` string literal updates across ~20 files
- Test file renames: `CommandApiTraceTests` -> `EventStoreTraceTests`, `CommandApiAuthorizationRegistrationTests` -> `EventStoreAuthorizationRegistrationTests`

### Proposal 10: Admin.Server CommandApiAppId Property Rename
- `AdminServerOptions.CommandApiAppId` -> `EventStoreAppId`
- Default value `"commandapi"` -> `"eventstore"`
- Ripple across 10+ service files + validator + DI extensions
- Config key: `AdminServer:CommandApiAppId` -> `AdminServer:EventStoreAppId`
- All corresponding test updates

### Proposal 11: DAPR Component YAML Scoping (16 files)
- All `commandapi` entries in scopes lists -> `eventstore`
- All `commandapi` references in comments -> `eventstore`
- Resiliency target binding key `commandapi:` -> `eventstore:`
- Covers: statestore, pubsub (3 variants), configstore, resiliency, accesscontrol.sample, subscription (local + deploy)

### Proposal 12: Documentation Files (~15 files)
- `docs/concepts/architecture-overview.md` — Mermaid diagrams, prose, code examples
- `docs/guides/deployment-docker-compose.md` — Docker Compose service names, env vars, commands
- `docs/guides/deployment-kubernetes.md` — K8s annotations, dotnet publish commands
- `docs/guides/deployment-azure-container-apps.md` — dotnet publish commands
- `docs/guides/dapr-component-reference.md` — scoping instructions
- `docs/guides/security-model.md` — access control references
- `docs/getting-started/quickstart.md` — dashboard service name
- `docs/getting-started/first-domain-service.md` — Swagger URL
- `docs/reference/command-api.md` — base URL
- `docs/reference/query-api.md` — base URL
- `docs/reference/api/*.md` — Aspire package API docs
- `docs/superpowers/specs/*.md` — projection builder spec
- `docs/superpowers/plans/*.md` — projection builder plan
- `docs/assets/regenerate-demo-checklist.md` — verification steps
- `deploy/README.md` — all deployment sections

### Proposal 13: CI/CD GitHub Actions
- `.github/workflows/deploy-staging.yml` — Dockerfile path, image name, K8s deployment name

### Proposal 14: Planning Artifacts (Architecture, Epics, UX Design)
- `architecture.md` — ~40 replacements (project structure, dependencies, code examples, ADR-P4)
- `epics.md` — ~11 replacements (host name, AppId references in stories)
- `ux-design-specification.md` — ~6 replacements (Swagger UI context)
- Implementation artifacts NOT updated (frozen history)

### Proposal 15: Solution File and .csproj References
- `Hexalith.EventStore.slnx` — project path
- `Hexalith.EventStore.AppHost.csproj` — ProjectReference
- `Hexalith.EventStore.Server.Tests.csproj` — ProjectReference
- `Hexalith.EventStore.IntegrationTests.csproj` — ProjectReference

### Proposal 16: Samples and SignalR Client
- BlazorUI `Program.cs` — Aspire service discovery URLs, comments
- BlazorUI `appsettings.json` — `CommandApiUrl` -> `EventStoreUrl`, hub URL
- BlazorUI services — comment updates
- BlazorUI `Pages/Index.razor` — UI text
- Sample DAPR components (4 YAML files) — scope lists
- Sample deploy configs (K8s annotations, Azure Bicep, parameters)
- SignalR NuGet `EventStoreSignalRClientOptions.cs` — XML doc example URL

### Proposal 17: CLAUDE.md
- Project structure: `Hexalith.EventStore.CommandApi` -> `Hexalith.EventStore`

---

## Section 5: Implementation Handoff

**Scope: Moderate** — Broad mechanical change across ~250 files requiring careful execution.

### Handoff Plan

| Role | Responsibility |
|------|---------------|
| **Developer (Amelia)** | Execute the rename: directory move, namespace updates, string replacements, build verification, test pass |
| **Scrum Master (Bob)** | Update epics.md, architecture.md, ux-design-specification.md planning artifacts |
| **QA (Quinn)** | Verify Tier 1 + Tier 2 tests pass post-rename; spot-check Aspire topology boots correctly |

### Execution Sequence
1. Create feature branch `feat/rename-commandapi-to-eventstore`
2. Rename project directory and .csproj file (git mv)
3. Update solution file and all .csproj references
4. Find-replace namespaces in source + tests
5. Find-replace AppId `"commandapi"` -> `"eventstore"` in all YAML, C#, and config files
6. Update Aspire public API (parameters, record members)
7. Update AppHost Program.cs (variables, project reference)
8. Update Admin.Server `CommandApiAppId` -> `EventStoreAppId` + ripple
9. Update Dockerfile
10. Rename IntegrationTests/CommandApi/ directory -> EventStore/
11. Update samples (BlazorUI, DAPR components, deploy configs)
12. Update documentation (docs/ + deploy/README.md)
13. Update CI/CD workflow
14. Update planning artifacts (architecture, epics, UX design)
15. Update CLAUDE.md
16. Resolve SignalR namespace collision (`CommandApi.SignalR` -> `SignalRHub`)
17. `dotnet build Hexalith.EventStore.slnx` — verify zero errors
18. `dotnet test` Tier 1 + Tier 2 — verify all pass
19. PR review and merge

### Success Criteria
- Solution builds with zero errors and zero warnings (beyond existing suppressed warnings)
- All Tier 1 and Tier 2 tests pass
- `dotnet aspire run` boots the full topology with AppId `eventstore` visible in the Aspire dashboard
- No remaining references to `CommandApi` as a project name or `commandapi` as an AppId in source code, YAML, or active documentation
- Planning artifacts (architecture, epics, UX design) updated
- Implementation artifacts (historical story specs) left unchanged
