---
title: 'Independent Follow-up Reviews'
type: 'bugfix'
created: '2026-09-01'
baseline_revision: 89564e0c290f4bc32ac7ebdb7d33802ff6d5e9d5
baseline_commit: '28cd5935a156600b52f95b378f9c45ab57ba46cb'
status: ready-for-dev
review_loop_iteration: 0
followup_review_recommended: true
context: []
warnings: ['multiple-goals', 'oversized']
deferred:
  - summary: >-
      Reject nuspec metadata containing more than one dependencies element instead of silently validating only the first.
    evidence: |-
      tools/release_package_contract.py resolves metadata dependencies with ElementTree.find, so a malformed archive can append a second dependencies element that is never inspected. Current dotnet pack output emits one element, making this a pre-existing fail-closed hardening item rather than a defect caused by this review patch.
    location: >-
      tools/release_package_contract.py:303
    severity: medium
  - summary: >-
      Pin publication-preflight execution before the irreversible NuGet push in semantic-release governance tests.
    evidence: |-
      .releaserc.json currently runs validate-publication-preflight.sh in publish mode before dotnet nuget push, but ReleasePackageManifestTests asserts only that secret validation precedes the push. Moving the publish-mode preflight after the push would leave the governance test green; verifyReleaseCmd remains an earlier mitigation, so this is pre-existing test hardening.
    location: >-
      tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs:303
    severity: medium
---

<intent-contract>

## Intent

**Problem:** Independent follow-up reviews of the completed Aspire security-resource naming and manifest-driven release-packaging stories found five current verification defects: the naming audit misses an operator-facing wait form, AppHost construction tests inherit persistent-security environment state, realm/import preservation is not pinned, repeated NuGet dependency groups can be unioned into a false pass, and release governance permits an additional foreign push command.

**Approach:** Harden the existing focused guards and shared package validator at their current seams, add mutation-style regression coverage for every reproduced failure, and retain the already-verified production behavior without reopening previously recorded work.

## Boundaries & Constraints

**Always:** Preserve the `security` default resource identity, supported resource-name/realm/import overrides, Keycloak endpoint/authentication behavior, the 14-entry release manifest, both existing validator entry points, and the single scoped NuGet publication command. Keep all environment mutation serialized, restored in `finally`, and independent of the caller's machine state. Treat the source story specs and current acceptance criteria as read-only authority. `baseline_revision` is the orchestration re-drive anchor where the completed implementation is already present; `baseline_commit` is the earlier original implementation provenance and the base for scoped diff checks. Because every Execution item is checked and the Results record completed verification, a `ready-for-dev` re-drive is verification/finalization-only: do not reimplement checked work or require a fresh implementation diff; rerun the specified Verification commands against the current `HEAD` and report the story complete when they pass. During finalization, evaluate cleanliness only for the four Execution paths and this spec relative to `baseline_commit`; leave pre-existing or concurrent changes outside those paths untouched, and do not treat them as a story blocker after all specified verification passes. Once those scoped checks pass, finalization MUST report the story complete even if other repository paths are dirty; repository-wide dirtiness is informational only and MUST NOT override this scoped rule. For outcome routing, the absence of a fresh implementation diff and changes confined to paths outside the four Execution paths and this spec are never blocking conditions: when every specified Verification command passes and no unexpected uncommitted overlap exists in those owned paths, the only conforming outcome is complete. An unexpected uncommitted change at re-drive start in one of the four Execution paths, or any failed verification, remains blocking; this resolver-approved spec clarification is expected and does not count as such an overlap.

**Never:** Rename Keycloak implementation concepts, change production AppHost topology merely to simplify tests, change package IDs/versions/inventory, publish artifacts, edit `references/**`, edit `_bmad-output/implementation-artifacts/deferred-work.md`, or reopen already-ledgered CLI-flag, version-example, shared-temp, GitHub-asset-glob, or stale-ledger-citation findings.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Stale wait identity | Tracked operator text contains `aspire wait keycloak` with normal option variants | The committed naming audit reports the tracked path and line | A clean no-match result remains valid only after pattern and pathspec controls pass |
| Hostile caller environment | Persistent mode and invalid Keycloak port variables are set before actual-AppHost model tests | Tests build the intended default model and restore every original value afterward | Construction failures cannot depend on ambient environment state |
| Realm/import contract | Default and explicit realm/import options build a security resource | Realm URL and import annotation reflect the selected values without changing the `security` role | Missing or mismatched annotations fail focused model tests |
| Repeated dependency group | One TFM appears in multiple nuspec groups whose union contains required edges | Both release validators reject the archive | Diagnostic identifies the archive and repeated target framework |
| Extra publication command | Semantic-release contains the valid EventStore push plus any second NuGet push/operand | Governance test rejects the configuration | Exactly one scoped push remains accepted |
| Completed-work re-drive | All Execution items are checked and their implementation is already present at `baseline_revision` | Treat the dispatch as verification/finalization-only, require no new implementation diff, rerun Verification at current `HEAD`, and return complete when it passes | Never return blocked solely because no fresh implementation diff exists; only a specified verification failure or an unexpected uncommitted overlap in one of the four Execution paths remains blocking |
| Unrelated dirty workspace | Pre-existing or concurrent changes exist outside the four Execution paths and this spec | Leave those changes untouched and report the story complete after every specified verification passes, irrespective of repository-wide dirty status | Unrelated repository-wide dirtiness is informational and cannot override the scoped result |

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
- Given all Execution items are checked and their implementation is already present at `baseline_revision`, when the story is re-driven with transport status `ready-for-dev`, then the session does not recreate the implementation or require a fresh source diff; it reruns the specified Verification commands at current `HEAD` and must report the story complete when they pass, without treating the absence of a fresh diff as a blocked outcome.
- Given pre-existing or concurrent changes only outside the four Execution paths and this spec, when every specified verification passes and finalization runs, then finalization reports the story complete while those unrelated changes remain untouched; repository-wide dirtiness alone must not block, while an unexpected uncommitted overlap in an Execution path or a failed verification still blocks.

## Spec Change Log

- 2026-09-01 -- Implemented all five independent follow-up review guards without changing production topology, release inventory, or publication configuration; added focused mutation coverage and reproduced every verification command at baseline `28cd5935a156600b52f95b378f9c45ab57ba46cb`.
- 2026-09-01 -- Applied the first review pass: widened the tracked audit and wait mutations, independently proved hostile environment restoration, strengthened realm/import assertions, rejected mixed ungrouped dependency shapes, preserved distinct grouped metadata, and scanned every semantic-release exec command field for foreign NuGet pushes.
- 2026-09-03 -- Resolved the finalization ambiguity: unrelated changes outside the story-owned paths remain untouched and do not block completion after all specified verification passes; owned-path overlap or failed verification remains blocking.
- 2026-09-05 -- Clarified that `baseline_revision` is the re-drive anchor, `baseline_commit` is the original scoped-diff base, and the completed story re-drives for verification/finalization without requiring reimplementation or a fresh source diff; scoped cleanliness covers only the four Execution paths and this spec.
- 2026-09-05 -- Resolved outcome routing: after all specified verification passes with no unexpected owned-path overlap, completion is mandatory; no fresh diff and unrelated workspace changes are not valid blocked outcomes.

## Review Triage Log

### 2026-09-01 — Review pass
- verdicts: 31 findings — high 0, medium 13, low 11, false 7, maybe-false 0
- findings:
  - `[low]` `[patch]` The tracked naming audit excluded root `aspire.config.json` — added the exact path plus a tracked-file coverage assertion; the current file is clean, but it is a root-owned operator surface.
  - `[medium]` `[patch]` The `aspire wait` pattern missed quoted names and shell/Markdown delimiters — broadened the shared same-line pattern and added quoted, semicolon, and punctuation mutations.
  - `[low]` `[reject]` A backslash-newline shell continuation can evade the line-oriented audit — valid but uncommon operator prose, and multiline shell parsing would add disproportionate complexity to a tracked-text identity guard.
  - `[low]` `[patch]` Two wait mutations emphasized pre-resource options rather than the documented positional form — the broadened guard now accepts arbitrary same-line option text and the mutations cover status/apphost plus normal post-resource options.
  - `[medium]` `[patch]` Environment restoration was not independently asserted — added a deterministic actual-AppHost test that seeds, overrides, restores, and checks every exact value.
  - `[medium]` `[patch]` The hostile caller scenario depended on external test-process setup — moved hostile persistent and invalid-port seeding inside the test while preserving the real caller environment in an outer finally.
  - `[low]` `[patch]` Realm URL coverage checked only a suffix/provider — it now compares the complete expected ReferenceExpression.
  - `[false]` `[reject]` The override import test could pass on unrelated files — disproved because its unique temporary directory contains only the sentinel file, and the final assertion now names that exact file as additional defense.
  - `[low]` `[reject]` The annotation callback receives null service contexts — the production callback under test currently consumes only its model and file data; building a full service-provider harness for hypothetical future framework behavior is not warranted.
  - `[medium]` `[defer]` A second dependencies element is ignored — verified pre-existing ElementTree.find behavior; recorded in frontmatter because this patch did not introduce it.
  - `[medium]` `[patch]` Direct dependencies plus a blank-framework group could still union in the None bucket — rejected the mixed shape before parsing and added both element orders through both validators.
  - `[false]` `[reject]` NuGet-equivalent but textually different TFM spellings bypass repeated-group rejection — they do not union because downstream validation keys groups by the exact TFM string and requires each group independently complete.
  - `[medium]` `[patch]` No positive control preserved valid multiple distinct TFM groups — added complete net9.0/net10.0 fixtures through both validators.
  - `[medium]` `[patch]` Publication governance inspected only publishCmd — it now scans every string *Cmd field of every semantic-release exec plugin and pins the sole push to publishCmd.
  - `[low]` `[reject]` Variable-expanded, quoted-token, or delegated-script pushes can evade the literal command guard — no such indirection exists in the reviewed configuration, and a general shell interpreter is disproportionate to this exact tracked command contract.
  - `[medium]` `[defer]` Publish-mode preflight ordering is not independently pinned — the live configuration is correct and verifyReleaseCmd mitigates it; recorded as pre-existing governance hardening.
  - `[false]` `[reject]` Exact canonical-command comparison is unnecessarily brittle — exact command and operand shape is an intentional governance contract, so equivalent rewrites should require deliberate test updates.
  - `[low]` `[patch]` Newly added security tests used same-line braces — reformatted all new additions to the repository's Allman style.
  - `[medium]` `[patch]` Other valid pre-resource wait options escaped the enumerated regex — the shared same-line guard now permits arbitrary option text and includes status/apphost mutations.
  - `[medium]` `[patch]` Quoted or punctuation-terminated wait resources escaped — fixed by the same optional-quote and non-word-boundary guard.
  - `[low]` `[reject]` Differently cased duplicate environment keys can coexist on case-sensitive hosts — possible but atypical, and case-insensitive enumeration/restoration would add complexity beyond the demonstrated exact-key configuration contract.
  - `[medium]` `[patch]` Mixed direct and blank dependency groups could falsely satisfy the contract — fixed and mutation-tested with both orders and validator entry points.
  - `[low]` `[reject]` Escaped or quoted NuGet command tokens can evade literal counting — same rejected shell-obfuscation case; the repository uses one explicit canonical command.
  - `[low]` `[reject]` A harmless command that prints `dotnet nuget push` can trigger a false positive — no such command exists, and conservative failure is acceptable for irreversible publication governance.
  - `[medium]` `[patch]` The claim that tracked inline wait identities fail was too broad for quotes/punctuation — the generalized guard and mutations now prove those forms.
  - `[low]` `[reject]` A quoted foreign publication can evade the literal guard — duplicate of the unsupported shell-obfuscation case; the exact canonical command contract remains intentional.
  - `[medium]` `[patch]` Ambient AppHost isolation was not exercised end-to-end — the new hostile-caller actual-AppHost test proves forced nonpersistent behavior and exact null/non-null restoration.
  - `[false]` `[reject]` The diff improperly chose remediation over review-only closure — the user asked to implement the bundle through build-auto, so finding-driven remediation is a defensible and completed reading.
  - `[false]` `[reject]` An empty triage log failed to record the independent process — the reviewer inspected an in-review artifact before this mandatory triage entry was written.
  - `[false]` `[reject]` Story 3.4 received only narrow test review — the independent pass also ran the full AppHost assembly, scratch Compose proof, and a live Aspire security baseline; code changed only where findings reproduced.
  - `[false]` `[reject]` Story 3.6 received only synthetic review — the independent pass also ran a real 14-package pack, both validators, and all isolated consumers; the patch targets the reproducible gaps it found.

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
- `git diff --check 28cd5935a156600b52f95b378f9c45ab57ba46cb -- tests/Hexalith.EventStore.AppHost.Tests/Configuration/AspireSecurityResourceNamingTests.cs tests/Hexalith.EventStore.AppHost.Tests/Configuration/HexalithEventStoreSecurityExtensionsTests.cs tools/release_package_contract.py tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs _bmad-output/implementation-artifacts/spec-independent-followup-reviews.md` -- expected: no whitespace errors in the original implementation-through-working-tree delta; unrelated paths are excluded. Do not require a repository-wide diff check or an empty diff for `_bmad-output/implementation-artifacts/deferred-work.md`; that file is outside the owned paths, and its `Never` boundary is satisfied by leaving any pre-existing or concurrent changes untouched during this re-drive.

**Recorded Results (original implementation run):** Both focused Release builds passed with zero warnings/errors. `AspireSecurityResourceNamingTests` passed 5/5 under hostile persistent/invalid-port environment values, `HexalithEventStoreSecurityExtensionsTests` passed 10/10, and `ReleasePackageManifestTests` passed 114/114 through both validator entry points. The package dry run emitted exactly 14 commands and created no output directory. At that run, `git diff --check` passed and `deferred-work.md` remained unchanged; these historical results do not replace the re-drive's required verification.
