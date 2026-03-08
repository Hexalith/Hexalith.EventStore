# Sprint Change Proposal — .NET SDK 10.0.102 → 10.0.103

**Date:** 2026-03-08
**Author:** Jerome (with AI assistance)
**Scope Classification:** Minor

---

## Section 1: Issue Summary

**Problem Statement:** The .NET SDK is pinned to version `10.0.102` in `global.json`, but the originally specified version was `10.0.103` (per Story 1-1 specification). The pin was set to `10.0.102` during initial implementation because `10.0.103` was not yet installed locally. Now that `10.0.103` is available, the SDK pin should be updated to match the original specification.

**Discovery Context:** Identified during routine sprint review — the implementation artifact for Story 1-1 explicitly notes "DO NOT use .NET SDK version 10.0.102 - Use 10.0.103" and documents the temporary downgrade.

**Evidence:**
- `_bmad-output/implementation-artifacts/1-1-solution-structure-and-build-infrastructure.md` line 940: explicitly calls for 10.0.103
- `global.json` currently pins `10.0.102` with `rollForward: latestPatch`

---

## Section 2: Impact Analysis

### Epic Impact
- **None.** No epics are affected. This is an infrastructure-level configuration change with no behavioral impact.

### Story Impact
- **No current or future stories require changes.** The SDK bump is transparent to all domain logic.

### Artifact Conflicts

| Artifact | Impact | Action |
|----------|--------|--------|
| Architecture doc | Line 218: technology stack table references `10.0.102` | Update to `10.0.103` |
| PRD | No conflict — specifies ".NET 10" generically | None |
| UI/UX | No impact | None |

### Technical Impact

| File | Change Required |
|------|----------------|
| `global.json` | `"version": "10.0.102"` → `"version": "10.0.103"` |
| `CLAUDE.md` | SDK version reference |
| `.github/ISSUE_TEMPLATE/01-bug-report.yml` | Placeholder example |
| `.github/DISCUSSION_TEMPLATE/q-a.yml` | Placeholder example |
| `CONTRIBUTING.md` | Prerequisite version |
| `docs/getting-started/prerequisites.md` | 4 version references |
| `docs/guides/troubleshooting.md` | 3 version references |
| `docs/guides/deployment-kubernetes.md` | Prereq version |
| `docs/guides/deployment-docker-compose.md` | Prereq version |
| `docs/guides/deployment-azure-container-apps.md` | Prereq version |

**Note:** 20+ implementation artifact files also reference `10.0.102` but are historical records and should NOT be updated.

---

## Section 3: Recommended Approach

**Selected Path:** Direct Adjustment

**Rationale:**
- Simple string replacement (`10.0.102` → `10.0.103`) across 10 codebase files + 1 planning artifact
- Zero architectural, behavioral, or API impact
- The `rollForward: latestPatch` policy in `global.json` means existing builds with 10.0.103 installed already work — this change just makes the pin explicit
- Aligns with the original Story 1-1 specification

**Effort Estimate:** Low (< 30 minutes)
**Risk Level:** Low (no behavioral change, no API change, no dependency change)
**Timeline Impact:** None

---

## Section 4: Detailed Change Proposals

### 4.1 Runtime Configuration

**File:** `global.json`

```
OLD:
    "version": "10.0.102",

NEW:
    "version": "10.0.103",
```

**Rationale:** Primary configuration change — pins SDK to originally intended version.

### 4.2 Project Instructions

**File:** `CLAUDE.md` (line 9)

```
OLD:
- **Framework:** .NET 10 (SDK 10.0.102, pinned in `global.json`)

NEW:
- **Framework:** .NET 10 (SDK 10.0.103, pinned in `global.json`)
```

### 4.3 GitHub Templates

**File:** `.github/ISSUE_TEMPLATE/01-bug-report.yml`
**File:** `.github/DISCUSSION_TEMPLATE/q-a.yml`

```
OLD:
      placeholder: "e.g. 10.0.102"

NEW:
      placeholder: "e.g. 10.0.103"
```

### 4.4 Contributing Guide

**File:** `CONTRIBUTING.md`

```
OLD:
- **.NET 10 SDK** (10.0.102 or later)

NEW:
- **.NET 10 SDK** (10.0.103 or later)
```

### 4.5 Documentation Files

**Files:** `docs/getting-started/prerequisites.md`, `docs/guides/troubleshooting.md`, `docs/guides/deployment-kubernetes.md`, `docs/guides/deployment-docker-compose.md`, `docs/guides/deployment-azure-container-apps.md`

All instances of `10.0.102` replaced with `10.0.103` — version requirements, verification examples, and troubleshooting references.

### 4.6 Architecture Document (Planning Artifact)

**File:** `_bmad-output/planning-artifacts/architecture.md` (line 218)

```
OLD:
| .NET SDK | 10.0.102 | LTS, supported until November 2028 |

NEW:
| .NET SDK | 10.0.103 | LTS, supported until November 2028 |
```

---

## Section 5: Implementation Handoff

**Change Scope:** Minor — Direct implementation by development team.

**Handoff:**
- **Recipient:** Development team (self-serve)
- **Responsibility:** Execute the find/replace changes listed in Section 4
- **Verification:** Run `dotnet build Hexalith.EventStore.slnx` and Tier 1 tests after change

**Success Criteria:**
1. `global.json` pins SDK `10.0.103`
2. All documentation references updated
3. `dotnet build` succeeds
4. Tier 1 unit tests pass
