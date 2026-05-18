# Sprint Change Proposal - .NET SDK 10.0.300

## 1. Issue Summary

**Trigger:** Jerome requested the repository use .NET SDK `10.0.300`.

**Problem Statement:** The root `global.json` still pinned SDK `10.0.103`, while the local environment has SDK `10.0.300` installed and available. Current source documentation and active BMad context also referenced the old SDK pin.

**Evidence:**

- `dotnet --list-sdks` showed `10.0.300` installed under `C:\Program Files\dotnet\sdk`.
- Root `global.json` pinned `"version": "10.0.103"` before this correction.
- Current docs and active planning/context artifacts referenced `10.0.103`.

## 2. Impact Analysis

**Epic Impact:** No epic scope, order, or acceptance criteria changes are required. This is a tooling baseline correction within the existing .NET 10 platform direction.

**Story Impact:** No new product story is required. Developer environment and build infrastructure references should align with the updated SDK pin.

**Artifact Conflicts:**

- PRD: No conflict. PRD specifies .NET 10 LTS without a conflicting SDK patch pin.
- Epics: No conflict. Epics refer to missing .NET SDK prerequisites generically.
- Architecture: Requires SDK version table and repository tree note update.
- UX Design: No change required. UX references .NET 10 and SDK experience generically.
- Documentation: Current prerequisite, deployment, troubleshooting, contributor, and agent guidance references require update.

**Technical Impact:** Low. Projects continue targeting `net10.0`; package versions and app model are unchanged. `global.json` now selects SDK `10.0.300` with the existing `rollForward: latestPatch` policy.

## 3. Recommended Approach

**Selected path:** Direct Adjustment.

**Rationale:** The requested change is a narrow SDK baseline update. It does not alter runtime architecture, DAPR topology, API contracts, product scope, or admin/user flows.

**Effort estimate:** Low.

**Risk level:** Low.

**Timeline impact:** None expected.

## 4. Detailed Change Proposals

### Build Configuration

`global.json`

OLD:

```json
"version": "10.0.103"
```

NEW:

```json
"version": "10.0.300"
```

Rationale: Make the repository SDK resolver use the requested .NET 10.0.300 SDK.

### Active Documentation and Planning Artifacts

Update active references from `10.0.103` to `10.0.300` in:

- `CLAUDE.md`
- `CONTRIBUTING.md`
- `docs/getting-started/prerequisites.md`
- `docs/guides/deployment-docker-compose.md`
- `docs/guides/deployment-azure-container-apps.md`
- `docs/guides/deployment-kubernetes.md`
- `docs/guides/troubleshooting.md`
- `_bmad-output/project-context.md`
- `_bmad-output/planning-artifacts/architecture.md`

Rationale: Keep developer onboarding, troubleshooting, and AI-agent context aligned with the new SDK pin.

### Historical Artifacts

Historical change proposals, archived planning documents, implementation records, generated `obj` files, and submodule-owned documents were intentionally left unchanged.

Rationale: These files record past state or belong outside the active root repository scope for this correction.

## 5. Implementation Handoff

**Scope classification:** Minor.

**Route to:** Developer agent for direct implementation.

**Success Criteria:**

- Root `global.json` pins SDK `10.0.300`.
- `dotnet --version` from the repository root returns `10.0.300`.
- Current docs and active planning/context references no longer describe `10.0.103` as the active root SDK pin.
- AppHost smoke build succeeds under SDK `10.0.300`.

## 6. Checklist Summary

- [x] 1.1 Trigger identified: direct user request to use .NET SDK `10.0.300`.
- [x] 1.2 Core problem defined: outdated SDK pin and active documentation references.
- [x] 1.3 Evidence gathered: installed SDK list and root `global.json` state.
- [x] 2.1-2.5 Epic impact assessed: no epic changes required.
- [x] 3.1 PRD impact assessed: no PRD conflict.
- [x] 3.2 Architecture impact assessed: SDK references updated.
- [N/A] 3.3 UI/UX impact: no UI/UX-specific SDK patch conflict.
- [x] 3.4 Other artifacts assessed: current documentation updated; historical/generated/submodule files left unchanged.
- [x] 4.1 Direct Adjustment evaluated: viable.
- [x] 4.2 Rollback evaluated: not needed.
- [x] 4.3 MVP Review evaluated: not needed.
- [x] 4.4 Recommended path selected: Direct Adjustment.
- [x] 5.1-5.5 Proposal components completed.
- [x] 6.1-6.2 Proposal reviewed for consistency.
- [x] 6.3 Approval: treated as approved by direct user instruction.
- [N/A] 6.4 Sprint status update: no epic/story entries changed.
- [x] 6.5 Handoff plan: direct developer implementation.

## 7. Validation

- `dotnet --version` returned `10.0.300`.
- `dotnet --info` reported SDK version `10.0.300` and root `global.json` at `D:\Hexalith.EventStore\global.json`.
- `dotnet build src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --no-restore --configuration Debug --verbosity minimal` succeeded with 0 warnings and 0 errors.
