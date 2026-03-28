# Sprint Change Proposal — Rename Admin AppIds

**Date:** 2026-03-28
**Author:** Bob (Scrum Master agent)
**Scope Classification:** Minor
**Status:** Approved (2026-03-28)
**Related:** sprint-change-proposal-2026-03-28-rename-commandapi.md
**Branch:** feat/rename-commandapi-to-eventstore (same branch as CommandApi rename)

---

## Section 1: Issue Summary

**Problem:** The DAPR AppIds `admin-server` and `admin-ui` do not follow the `eventstore-*` naming convention established by the CommandApi-to-EventStore rename. They should be `eventstore-admin` and `eventstore-admin-ui` respectively, so all Hexalith.EventStore services share a consistent `eventstore-*` prefix.

**Discovery:** Strategic naming decision by Jerome during the broader rename initiative (2026-03-28). The `commandapi` AppId was already renamed to `eventstore`; the admin AppIds should follow suit.

**Evidence:**
- `eventstore` AppId is the new standard (per CommandApi rename proposal)
- `admin-server` and `admin-ui` break the naming pattern
- Consistent naming improves discoverability in DAPR dashboards, Aspire dashboards, and Kubernetes

---

## Section 2: Impact Analysis

### Epic Impact
- No epics blocked, invalidated, or reordered
- No epic text references to `admin-server` or `admin-ui` in `epics.md`

### Artifact Conflicts

| Artifact | Impact | Refs |
|----------|--------|------|
| PRD | None | 0 |
| Architecture | Minor — 1 line in DAPR integration table | 1 |
| Epics | None | 0 |
| UX Design | None | 0 |

### Technical Impact

| Category | Files | Occurrences |
|----------|-------|-------------|
| **AppHost Program.cs** — AppId strings, variable names | 1 | ~15 |
| **Aspire Extensions** — AppId string, parameter names, XML doc | 2 | ~15 |
| **DAPR access control YAML — local** (3 files: accesscontrol.yaml, accesscontrol.admin-server.yaml, statestore.yaml) | 3 | ~12 |
| **DAPR access control YAML — deploy** (4 files: accesscontrol.yaml, accesscontrol.admin-server.yaml, statestore-cosmosdb.yaml, statestore-postgresql.yaml) | 4 | ~14 |
| **DAPR YAML file renames** (`accesscontrol.admin-server.yaml` → `accesscontrol.eventstore-admin.yaml`) | 2 files + bin copies | Filename |
| **Admin.UI** — Program.cs fallback URL, appsettings.json Subject, Index.razor error text, AccessTokenProvider fallback | 4 | 5 |
| **Admin.Server.Host** — appsettings.json config section (no AppId string) | 0 | 0 |
| **Tests — AccessControlPolicyTests.cs** — file path strings, policy lookup strings, assertion messages | 1 | ~20 |
| **Tests — DaprComponentValidationTests.cs** — file path string, scope assertions | 1 | ~8 |
| **Tests — ProductionDaprComponentValidationTests.cs** — file path string, scope assertions, policy lookups | 1 | ~12 |
| **Tests — AdminUITestContext.cs** — BaseUrl string | 1 | 1 |
| **Documentation — deploy/README.md** | 1 | ~15 |
| **Documentation — docs/guides/security-model.md** | 1 | ~6 |
| **Documentation — docs/guides/dapr-component-reference.md** | 1 | ~4 |
| **Documentation — docs/guides/deployment-kubernetes.md** | 1 | ~1 |
| **Documentation — docs/guides/deployment-docker-compose.md** | 1 | ~6 |
| **Planning artifacts — architecture.md** | 1 | 1 |

**Total estimated: ~25 files, ~135 individual replacements**

### Namespace / API Impact
- **No namespace changes** — C# class names (`AdminServerOptions`, `AdminUI`, etc.) are not affected; only DAPR AppId strings and YAML file names change.
- **Config key `AdminServer` section name** — Not affected (this is C# config binding, not a DAPR AppId).
- **Aspire public API** — `HexalithEventStoreResources.AdminServer` record member stays (it refers to the project resource, not the AppId).

### Breaking Changes
- **DAPR access control file rename:** `accesscontrol.admin-server.yaml` → `accesscontrol.eventstore-admin.yaml` affects test file path references and deploy instructions.
- **DAPR scope lists:** Any external consumer referencing `admin-server` in DAPR scopes must update. Pre-v1, acceptable.
- **Admin.UI `appsettings.json`:** Subject claim changes from `admin-ui` to `eventstore-admin-ui`.

---

## Section 3: Recommended Approach

**Selected: Direct Adjustment** (folded into the existing `feat/rename-commandapi-to-eventstore` branch)

**Rationale:**
- Purely mechanical text replacement — same pattern as the CommandApi rename
- No business logic, architecture, or API design changes
- Smaller scope (~25 files vs ~250 for CommandApi rename)
- Logically part of the same naming consistency initiative
- Build + test feedback is immediate

**Effort:** Low
**Risk:** Low
**Timeline impact:** Negligible — adds a small increment to the existing rename task

---

## Section 4: Detailed Change Proposals

### Proposal 1: AppHost Program.cs

| OLD | NEW |
|-----|-----|
| `"admin-server"` (AddProject AppId) | `"eventstore-admin"` |
| `"admin-ui"` (AddProject AppId) | `"eventstore-admin-ui"` |
| `adminServerAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.admin-server.yaml")` | `...("accesscontrol.eventstore-admin.yaml")` |

Variable names (`adminServer`, `adminUI`, `adminServerHttps`, `adminServerAccessControlConfigPath`) remain unchanged — they describe the project resource, not the DAPR AppId.

### Proposal 2: Aspire Extensions — HexalithEventStoreExtensions.cs

| OLD | NEW |
|-----|-----|
| `AppId = "admin-server"` (line 79) | `AppId = "eventstore-admin"` |

Parameter names (`adminServer`, `adminUI`, `adminServerDaprConfigPath`) remain unchanged.

### Proposal 3: DAPR YAML File Renames

| OLD | NEW |
|-----|-----|
| `src/.../DaprComponents/accesscontrol.admin-server.yaml` | `accesscontrol.eventstore-admin.yaml` |
| `deploy/dapr/accesscontrol.admin-server.yaml` | `accesscontrol.eventstore-admin.yaml` |
| Inside files: `name: accesscontrol-admin-server` | `name: accesscontrol-eventstore-admin` |
| Inside files: comments referencing `admin-server` | `eventstore-admin` |

### Proposal 4: DAPR Access Control — accesscontrol.yaml (local + deploy)

| OLD | NEW |
|-----|-----|
| `appId: admin-server` | `appId: eventstore-admin` |
| Comments: `admin-server` | `eventstore-admin` |

### Proposal 5: DAPR State Store YAML Scoping (local + deploy, 3 files)

| OLD | NEW |
|-----|-----|
| `- admin-server` (scopes list) | `- eventstore-admin` |
| Comments: `admin-server` | `eventstore-admin` |

Files: `statestore.yaml` (local), `statestore-cosmosdb.yaml` (deploy), `statestore-postgresql.yaml` (deploy)

### Proposal 6: Admin.UI

| File | OLD | NEW |
|------|-----|-----|
| `Program.cs:69` | `"https://admin-server"` | `"https://eventstore-admin"` |
| `appsettings.json:10` | `"Subject": "admin-ui"` | `"Subject": "eventstore-admin-ui"` |
| `Pages/Index.razor:21` | `"Cannot reach admin-server."` | `"Cannot reach eventstore-admin."` |
| `Services/AdminApiAccessTokenProvider.cs:84` | `?? "admin-ui"` | `?? "eventstore-admin-ui"` |

### Proposal 7: Tests — AccessControlPolicyTests.cs

| OLD | NEW |
|-----|-----|
| `"accesscontrol.admin-server.yaml"` (2 path refs) | `"accesscontrol.eventstore-admin.yaml"` |
| `FindPolicy(doc, "admin-server")` | `FindPolicy(doc, "eventstore-admin")` |
| All assertion messages: `"admin-server"` | `"eventstore-admin"` |
| Scope assertions: `["eventstore", "admin-server"]` | `["eventstore", "eventstore-admin"]` |

### Proposal 8: Tests — DaprComponentValidationTests.cs

| OLD | NEW |
|-----|-----|
| `"accesscontrol.admin-server.yaml"` (path) | `"accesscontrol.eventstore-admin.yaml"` |
| `GetString(p, "appId") == "admin-server"` | `== "eventstore-admin"` |
| Scope assertions: `["eventstore", "admin-server"]` | `["eventstore", "eventstore-admin"]` |
| Assertion messages: `"admin-server"` | `"eventstore-admin"` |

### Proposal 9: Tests — ProductionDaprComponentValidationTests.cs

| OLD | NEW |
|-----|-----|
| `"accesscontrol.admin-server.yaml"` (2 refs) | `"accesscontrol.eventstore-admin.yaml"` |
| `GetString(p, "appId") == "admin-server"` | `== "eventstore-admin"` |
| Scope assertions: `["eventstore", "admin-server"]` | `["eventstore", "eventstore-admin"]` |
| Assertion messages: `"admin-server"` | `"eventstore-admin"` |

### Proposal 10: Tests — AdminUITestContext.cs

| OLD | NEW |
|-----|-----|
| `"https://admin-server"` | `"https://eventstore-admin"` |

### Proposal 11: Documentation (~5 files)

All `admin-server` → `eventstore-admin` and `admin-ui` → `eventstore-admin-ui` in:
- `deploy/README.md` (~15 occurrences)
- `docs/guides/security-model.md` (~6 occurrences)
- `docs/guides/dapr-component-reference.md` (~4 occurrences)
- `docs/guides/deployment-kubernetes.md` (~1 occurrence)
- `docs/guides/deployment-docker-compose.md` (~6 occurrences)

Also update file references: `accesscontrol.admin-server.yaml` → `accesscontrol.eventstore-admin.yaml`

### Proposal 12: Planning Artifact — architecture.md

| OLD | NEW |
|-----|-----|
| `admin-server app-id allowed in EventStore access control policy` (line 183) | `eventstore-admin app-id allowed in EventStore access control policy` |

---

## Section 5: Implementation Handoff

**Scope: Minor** — Small, mechanical rename folded into the existing branch.

### Handoff Plan

| Role | Responsibility |
|------|---------------|
| **Developer (Amelia)** | Execute the AppId renames, YAML file renames, string replacements, build + test verification |

### Execution Sequence
1. Rename YAML files: `accesscontrol.admin-server.yaml` → `accesscontrol.eventstore-admin.yaml` (local + deploy)
2. Update AppId strings in Aspire extensions + AppHost Program.cs
3. Update DAPR YAML content (access control policies, state store scopes, config names)
4. Update Admin.UI (Program.cs, appsettings.json, Index.razor, AccessTokenProvider)
5. Update tests (AccessControlPolicyTests, DaprComponentValidationTests, ProductionDaprComponentValidationTests, AdminUITestContext)
6. Update documentation (deploy/README.md, 4 docs/guides files)
7. Update architecture.md planning artifact
8. `dotnet build Hexalith.EventStore.slnx` — verify zero errors
9. `dotnet test` Tier 1 + Tier 2 — verify all pass

### Success Criteria
- Solution builds with zero errors
- All Tier 1 and Tier 2 tests pass
- DAPR AppIds `eventstore-admin` and `eventstore-admin-ui` visible in Aspire dashboard
- No remaining references to `admin-server` or `admin-ui` as AppIds in source code, YAML, or active documentation
- `architecture.md` updated
