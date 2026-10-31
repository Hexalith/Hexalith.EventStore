---
title: Align synthetic Aspire packages with actual dependencies
status: done
baseline_commit: 5e3c2f0e60b114cdaab52594ccbb254829d40e0f
created: 2026-10-31
---

## Intent

Upstream SDK feature 5e3c2f0e adds actual Client and ServiceDefaults project dependencies to the Aspire package. Its strict archive validators correctly require those edges, but the synthetic valid-package fixture still emits only Contracts. Exact upstream CI 41211546367 / contracts job 124567428307 therefore fails eight cases, including negative cases intercepted by the unrelated fixture defect.

Add only the two actual internal edges to the fixture. Preserve production validators, complete dependency groups, exact versions and every source/dependency-loss negative assertion.

## File Scope

Allowed files for this story:

- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs`
- `_bmad-output/implementation-artifacts/spec-align-aspire-package-contract-fixtures.md`

## Verification

Run the complete affected packaging class, covering both archive validators and existing mutation cases. Build with warnings treated as errors and validate the exact commit candidate using the owner-pinned CLI before real hooks and push.

## Results

The complete affected packaging class passed 129/129 with zero failures, errors, skipped or unrun cases in 19.261 seconds. The Release build passed with zero warnings and errors. Both archive validators now accept the complete valid fixture and every existing dependency/source mutation retains its intended refusal.

- `/tmp/es-3130-package-fixture-build.log` SHA256 `c0322eca7a38cee60bfdcd7157336b4b5a4f7c7bf7392b8274ac74a5d93acf06`
- `/tmp/es-3130-package-fixture-tests.xml` SHA256 `15817a73b68ca64c3a5c53dee7325093916e41c28f3ac5cf095c4036e364d8d1`
