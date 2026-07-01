---
title: 'Fix release gate UniqueIds version assertion'
type: 'bugfix'
created: '2026-07-01'
status: 'done'
route: 'one-shot'
---

# Fix release gate UniqueIds version assertion

## Intent

**Problem:** The `release-gate` job failed in `Run release test projects` because `ContractsPackageDependencyTests` still expected `Hexalith.Commons.UniqueIds` `2.24.0` while central package management pins `2.24.2`.

**Approach:** Update the package-governance assertion to the current published `2.24.2` pin and verify both the focused failure and the full `Contracts.Tests` project in Release package-reference mode.

## Suggested Review Order

- Aligns the release-gate assertion with the central package pin restored by CI.
  [`ContractsPackageDependencyTests.cs:27`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContractsPackageDependencyTests.cs#L27)
