---
title: 'Fix CI/CD xUnit v3 Package Version Restore Conflict'
type: 'bugfix'
created: '2026-08-21'
status: 'done'
route: 'one-shot'
---

# Fix CI/CD xUnit v3 Package Version Restore Conflict

## Intent

**Problem:** CI runs 32485211318 and 32485210906 failed during `dotnet restore` with NuGet error NU1107 due to an xunit.v3 package version mismatch between xunit.v3 (4.0.0) and xunit.v3.extensibility.core / xunit.v3.assert (3.2.2) in Hexalith.Builds.

**Approach:** Revert `xunit.v3` to `3.2.2` in `Directory.Packages.props` to align all xUnit v3 packages on 3.2.2, sort xunit packages alphabetically, and update `package-version-audit.json` to record canonical rationale for Roslynator 4.16.1.

## Suggested Review Order

**Central Package Management Alignment**

- Revert xunit.v3 to 3.2.2 to align transitive dependencies across test projects
  [`Directory.Packages.props:316`](../../references/Hexalith.Builds/Props/Directory.Packages.props#L316)

- Update Roslynator audited version to 4.16.1 with canonical listing rationale
  [`package-version-audit.json:7812`](../../references/Hexalith.Builds/Tools/package-version-audit.json#L7812)
