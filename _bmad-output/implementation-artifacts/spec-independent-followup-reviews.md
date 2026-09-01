---
title: 'Independent Follow-up Reviews'
type: 'bugfix'
created: '2026-09-01'
baseline_revision: '28cd5935a156600b52f95b378f9c45ab57ba46cb'
baseline_commit: '28cd5935a156600b52f95b378f9c45ab57ba46cb'
status: 'in-review'
review_loop_iteration: 0
followup_review_recommended: false
context: []
warnings: ['multiple-goals', 'oversized']
deferred: []
---

<intent-contract>

## Intent

**Problem:** Independent follow-up reviews of the completed Aspire security-resource naming and manifest-driven release-packaging stories found five current verification defects: the naming audit misses an operator-facing wait form, AppHost construction tests inherit persistent-security environment state, realm/import preservation is not pinned, repeated NuGet dependency groups can be unioned into a false pass, and release governance permits an additional foreign push command.

**Approach:** Harden the existing focused guards and shared package validator at their current seams, add mutation-style regression coverage for every reproduced failure, and retain the already-verified production behavior without reopening previously recorded work.

## Boundaries & Constraints

**Always:** Preserve the `security` default resource identity, supported resource-name/realm/import overrides, Keycloak endpoint/authentication behavior, the 14-entry release manifest, both existing validator entry points, and the single scoped NuGet publication command. Keep all environment mutation serialized, restored in `finally`, and independent of the caller's machine state. Treat the source story specs and current acceptance criteria as read-only authority.

**Never:** Rename Keycloak implementation concepts, change production AppHost topology merely to simplify tests, change package IDs/versions/inventory, publish artifacts, edit `references/**`, edit `_bmad-output/implementation-artifacts/deferred-work.md`, or reopen already-ledgered CLI-flag, version-example, shared-temp, GitHub-asset-glob, or stale-ledger-citation findings.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Stale wait identity | Tracked operator text contains `aspire wait keycloak` with normal option variants | The committed naming audit reports the tracked path and line | A clean no-match result remains valid only after pattern and pathspec controls pass |
| Hostile caller environment | Persistent mode and invalid Keycloak port variables are set before actual-AppHost model tests | Tests build the intended default model and restore every original value afterward | Construction failures cannot depend on ambient environment state |
| Realm/import contract | Default and explicit realm/import options build a security resource | Realm URL and import annotation reflect the selected values without changing the `security` role | Missing or mismatched annotations fail focused model tests |
| Repeated dependency group | One TFM appears in multiple nuspec groups whose union contains required edges | Both release validators reject the archive | Diagnostic identifies the archive and repeated target framework |
| Extra publication command | Semantic-release contains the valid EventStore push plus any second NuGet push/operand | Governance test rejects the configuration | Exactly one scoped push remains accepted |

</intent-contract>

## Code Map

- `_bmad-output/implementation-artifacts/spec-3-4-aspire-security-resource-naming.md` -- read-only Story 3.4 intent, boundaries, and verified naming/runtime evidence.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/AspireSecurityResourceNamingTests.cs:27` -- tracked pathspec/pattern controls and actual-AppHost enabled/disabled model tests; reuse its Git process helper and serialized environment collection.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/HexalithEventStoreSecurityExtensionsTests.cs:11` -- focused security-resource model assertions and the narrow seam for default/override realm and import annotations.
- `src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityOptions.cs:11` and `src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs:46` -- production defaults and environment/annotation wiring to preserve unless a new test proves a defect.
- `_bmad-output/implementation-artifacts/spec-3-6-manifest-driven-release-packaging.md` -- read-only Story 3.6 package-contract authority and previously disclosed residual risks.
- `tools/release_package_contract.py:308` -- nuspec dependency-group parser currently de-duplicates group names and merges dependencies by TFM; fail closed before per-group contract validation.
- `tools/release_package_contract.py:438` -- internal dependency validation consumed by both `tools/validate-release-packages.py` and `scripts/validate-nuget-packages.py`.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs:313` -- semantic-release publication assertions and archive mutation fixtures; existing duplicate-dependency rows do not split required edges across repeated groups.
- `.releaserc.json:12` -- current correct single EventStore-scoped NuGet push; verification-only unless tests expose drift.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.AppHost.Tests/Configuration/AspireSecurityResourceNamingTests.cs` -- make the stale-pattern set directly mutation-testable, reject `aspire wait keycloak`, seed representative forbidden forms, and snapshot/force/restore all persistent Keycloak mode and port variables around actual-AppHost construction -- close the missed operator identity and ambient-environment failures without changing production topology.
- [x] `tests/Hexalith.EventStore.AppHost.Tests/Configuration/HexalithEventStoreSecurityExtensionsTests.cs` -- assert default and overridden realm URL/import annotations on the built resource -- pin the preservation boundary named by Story 3.4.
- [x] `tools/release_package_contract.py` -- reject repeated target-framework dependency groups using a stable case-insensitive identity while retaining valid grouped and ungrouped nuspec handling -- prevent partial groups from satisfying the contract only after unioning.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- add repeated-partial-group mutations exercised through both validators, and prove semantic-release declares exactly one NuGet push with the one EventStore-scoped archive operand -- cover both reproduced Story 3.6 fail-open paths.

**Acceptance Criteria:**
- Given any tracked root-owned operator surface containing `aspire wait keycloak`, when the naming audit runs, then it fails with the offending path while the unmodified repository passes its positive and coverage controls.
- Given persistent-mode and invalid-port environment variables are present, when enabled and disabled actual-AppHost naming tests run, then they exercise their declared mode deterministically and restore the caller's exact environment values.
- Given default or explicitly overridden realm/import options, when the security resource model is inspected, then its realm URL and import annotation match those options and its role name remains `security` by default.
- Given a namespaced nuspec with two groups for the same TFM, when either release validator runs, then it fails before dependency unions can hide incomplete groups; a valid single group still succeeds.
- Given semantic-release configuration with the valid EventStore push plus a second foreign push or package operand, when governance tests run, then they fail; the current single `./nupkgs/Hexalith.EventStore.*.nupkg` push succeeds.

## Spec Change Log

- 2026-09-01 -- Implemented all five independent follow-up review guards without changing production topology, release inventory, or publication configuration; added focused mutation coverage and reproduced every verification command at baseline `28cd5935a156600b52f95b378f9c45ab57ba46cb`.

## Review Triage Log

## Design Notes

The package parser should reject structurally ambiguous repeated groups rather than defining union semantics that NuGet consumers may not share. The naming audit's production scan and its mutation cases must consume the same pattern factory so coverage cannot drift from enforcement.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Release/net10.0/Hexalith.EventStore.AppHost.Tests.dll -class Hexalith.EventStore.AppHost.Tests.Configuration.AspireSecurityResourceNamingTests` -- expected: all actual-model, scan-control, and mutation cases pass under hostile environment values.
- `dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Release/net10.0/Hexalith.EventStore.AppHost.Tests.dll -class Hexalith.EventStore.AppHost.Tests.Configuration.HexalithEventStoreSecurityExtensionsTests` -- expected: default and override realm/import cases pass.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.ReleasePackageManifestTests` -- expected: current manifest/release configuration passes and all new repeated-group/extra-push mutations fail closed.
- `python3 tools/pack-release-packages.py /tmp/eventstore-independent-review-dry 999.9.1-review --dry-run` -- expected: exactly 14 Release/package-mode commands and no package output.
- `git diff --check` -- expected: no whitespace errors; `git diff -- _bmad-output/implementation-artifacts/deferred-work.md` is empty.

**Results:** Both focused Release builds passed with zero warnings/errors. `AspireSecurityResourceNamingTests` passed 4/4 under hostile persistent/invalid-port environment values, `HexalithEventStoreSecurityExtensionsTests` passed 10/10, and `ReleasePackageManifestTests` passed 107/107 through both validator entry points. The package dry run emitted exactly 14 commands and created no output directory. `git diff --check` passed and `deferred-work.md` remains unchanged.
