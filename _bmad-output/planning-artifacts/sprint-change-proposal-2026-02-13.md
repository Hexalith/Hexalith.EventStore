# Sprint Change Proposal — Replace FluentAssertions with Shouldly

**Date**: 2026-02-13
**Triggered By**: Story 2-1 (CommandAPI Host & Minimal Endpoint Scaffolding)
**Scope Classification**: Minor
**Status**: Pending Approval

---

## Section 1: Issue Summary

**Problem Statement**: FluentAssertions was initially selected as the assertion library and configured in `Directory.Packages.props` and `Hexalith.EventStore.Testing.csproj`. However, the project should use **Shouldly** instead. All existing test code already uses Shouldly — no `.cs` file contains `using FluentAssertions`. The package reference is unused dead weight that could mislead future contributors.

**Discovery Context**: Identified during Story 2-1 implementation, where new tests were written using Shouldly, confirming FluentAssertions was never adopted in practice.

**Evidence**:
- Zero `using FluentAssertions` statements in any `.cs` file
- Three test files already use `using Shouldly`
- Shouldly is already listed in `Directory.Packages.props` (version 4.3.0)

---

## Section 2: Impact Analysis

### Epic Impact
- **None**. No epics require scope, sequencing, or priority changes. This is a dependency-level cleanup only.

### Story Impact
- **Story 2-1** (in-progress): Implementation artifact references FluentAssertions — doc update needed.
- **Future stories**: No code changes needed; all tests already use Shouldly.

### Artifact Conflicts

| Artifact | Impact | Action |
|---|---|---|
| `Directory.Packages.props` | Contains FluentAssertions 8.8.0 package version | Remove FluentAssertions line |
| `src/Hexalith.EventStore.Testing/Hexalith.EventStore.Testing.csproj` | References FluentAssertions | Replace with Shouldly |
| `1-1-solution-structure-and-build-infrastructure.md` | Documents FluentAssertions in package tables | Update to Shouldly |
| `1-4-testing-package-in-memory-test-helpers.md` | Multiple FluentAssertions references | Update all to Shouldly |
| `1-5-aspire-apphost-and-servicedefaults-scaffolding.md` | References FluentAssertions | Update to Shouldly |
| `2-1-commandapi-host-and-minimal-endpoint-scaffolding.md` | References FluentAssertions | Update to Shouldly |

### Technical Impact
- **Code**: Zero — no source files use FluentAssertions.
- **Build**: Package restore will no longer pull FluentAssertions NuGet. Shouldly already restored.
- **Tests**: All pass as-is since they already use Shouldly.

---

## Section 3: Recommended Approach

**Selected**: Direct Adjustment

**Rationale**: This is the simplest possible change — remove an unused package reference and update documentation. No code changes, no test changes, no architectural impact. Zero risk to functionality.

- **Effort**: Low (< 30 minutes)
- **Risk**: Low (no functional code affected)
- **Timeline Impact**: None

---

## Section 4: Detailed Change Proposals

### 4.1 Directory.Packages.props

```diff
- <PackageVersion Include="FluentAssertions" Version="8.8.0" />
```

### 4.2 src/Hexalith.EventStore.Testing/Hexalith.EventStore.Testing.csproj

```diff
- <PackageReference Include="FluentAssertions" />
+ <PackageReference Include="Shouldly" />
```

### 4.3 Implementation Artifact Documentation

- **1-1**: Remove FluentAssertions from package tables, replace with Shouldly
- **1-4**: Update all 8 FluentAssertions references to Shouldly
- **1-5**: Update assertion library reference to Shouldly
- **2-1**: Update assertion library reference to Shouldly

---

## Section 5: Implementation Handoff

**Change Scope**: Minor — Direct implementation by development team.

**Handoff**: Development team

**Responsibilities**:
1. Edit `Directory.Packages.props` — remove FluentAssertions line
2. Edit `Hexalith.EventStore.Testing.csproj` — replace FluentAssertions with Shouldly
3. Update 4 implementation artifact markdown files
4. Run `dotnet build` to verify no breakage
5. Run `dotnet test` to verify all tests pass

**Success Criteria**:
- No reference to FluentAssertions in any `.csproj` or `Directory.Packages.props`
- All tests pass
- Implementation artifacts consistently reference Shouldly
