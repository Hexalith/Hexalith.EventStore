---
title: 'Independent Follow-up Reviews'
type: 'bugfix'
created: '2026-09-01'
baseline_revision: debad3be66b0b1ce135964083f4e0a7a64b37dc4
baseline_commit: '28cd5935a156600b52f95b378f9c45ab57ba46cb'
status: done
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
  - summary: >-
      Reject direct dependencies mixed with any named dependency group.
    evidence: |-
      The current shared parser and internal dependency contract accept a flat list plus a complete net10.0 group. NuGet's dependency-group reference prohibits mixing grouped and flat formats. This was reproduced in an isolated archive probe and predates this verification-only re-drive.
    location: >-
      tools/release_package_contract.py:305
    severity: medium
  - summary: >-
      Add positive coverage for valid ungrouped dependencies and standalone fallback groups.
    evidence: |-
      Positive fixtures cover named groups only; the direct-dependency fixture contains intentional duplicates, and blank groups occur only in mixed-shape rejection cases. Valid direct and missing, empty, or whitespace framework fallback groups currently parse but lack regression coverage.
    location: >-
      tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs:394
    severity: low
  - summary: >-
      Pin automatic PayloadProtection workflow triggers in governance tests.
    evidence: |-
      The parsed workflow test inspects jobs and commands but never inspects the on mapping. Removing push or pull_request triggers or adding restrictive path filters leaves those assertions unaffected. The current workflow has the correct automatic triggers; this is existing coverage hardening.
    location: >-
      tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs:974
    severity: medium
  - summary: >-
      Reject failure-masking shell operators in PayloadProtection workflow commands.
    evidence: |-
      The command assertions reject comments, semicolons, and double pipes but allow trailing background operators. Both false & wait and false & true returned exit 0 in isolated shell probes, while their corresponding workflow mutations retain the checked command prefix and flag counts. Current workflow commands remain foreground invocations.
    location: >-
      tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs:1015
    severity: medium
  - summary: >-
      Inspect the complete test attribute list regardless of Fact or Theory ordering.
    evidence: |-
      AttributePreludeBeforeMethod starts at the last Fact or Theory marker, so a HeavyweightContainerPublish trait placed above that marker disappears from the inspected prelude. The negative guard can then accept a test excluded by CI. Existing source attribute ordering is correct; an isolated extraction probe confirmed the latent guard gap.
    location: >-
      tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs:1243
    severity: medium
  - summary: >-
      Isolate synthetic naming-audit Git subprocesses from inherited repository selectors.
    evidence: |-
      RunGitAsync inherits Git environment selectors despite setting WorkingDirectory. An isolated two-repository probe demonstrated that fixture git add modifies the caller's index when GIT_INDEX_FILE points there; GIT_DIR and GIT_WORK_TREE can similarly redirect fixture mutations. Only disposable repositories under /tmp were used, and the required naming tests passed under the actual caller environment.
    location: >-
      tests/Hexalith.EventStore.AppHost.Tests/Configuration/AspireSecurityResourceNamingTests.cs:464
    severity: medium
  - summary: >-
      Bind the generated-client credential test to the realm template actually imported by its resource.
    evidence: |-
      The credential test manually renders the Tenants template while constructing its security resource with the default realm import path. It never inspects that resource's import callback; the EventStore AppHost template and Tenants template currently differ in whether they include the SDK's default-account placeholder. This is a pre-existing credential-test proof gap, and does not establish a defect in the separately rendered production AppHost realm.
    location: >-
      tests/Hexalith.EventStore.AppHost.Tests/Configuration/HexalithEventStoreSecurityExtensionsTests.cs:210
    severity: medium
  - summary: >-
      Resolve the credential-test Tenants template through supported dependency layouts.
    evidence: |-
      The credential test hardcodes EventStore/references/Hexalith.Tenants although RepositoryProjectPaths.GetReferencedModuleProjectPath and source-mode build configuration support sibling and enclosing checkouts. It fails in those layouts when EventStore's nested Tenants checkout is absent; no nested submodule was initialized during this run.
    location: >-
      tests/Hexalith.EventStore.AppHost.Tests/Configuration/HexalithEventStoreSecurityExtensionsTests.cs:258
    severity: medium
  - summary: >-
      Add repeated blank or missing-framework partial-group rejection fixtures through both validators.
    evidence: |-
      The parser correctly rejects two groups normalized to the ungrouped key, but existing repeated-group mutations use net10.0 or NET10.0. The verification-gap reviewer demonstrated in memory that ignoring an empty framework key restores the original dependency-union false pass without affecting any existing fixture case. Current production code remains correct.
    location: >-
      tools/release_package_contract.py:330
    severity: medium
---

<intent-contract>

## Intent

**Problem:** Independent follow-up reviews of the completed Aspire security-resource naming and manifest-driven release-packaging stories found five current verification defects: the naming audit misses an operator-facing wait form, AppHost construction tests inherit persistent-security environment state, realm/import preservation is not pinned, repeated NuGet dependency groups can be unioned into a false pass, and release governance permits an additional foreign push command.

**Approach:** Harden the existing focused guards and shared package validator at their current seams, add mutation-style regression coverage for every reproduced failure, and retain the already-verified production behavior without reopening previously recorded work.

## Boundaries & Constraints

**Always:** Preserve the `security` default resource identity, supported resource-name/realm/import overrides, Keycloak endpoint/authentication behavior, the 14-entry release manifest, both existing validator entry points, and the single scoped NuGet publication command. Keep all environment mutation serialized, restored in `finally`, and independent of the caller's machine state. Treat the source story specs and current acceptance criteria as read-only authority. `baseline_revision` is the orchestration re-drive anchor where the completed implementation is already present; `baseline_commit` is the earlier original implementation provenance and the base for scoped diff checks. Because every Execution item is checked and the Results record completed verification, a `ready-for-dev` re-drive is verification/finalization-only: do not reimplement checked work or require a fresh implementation diff; rerun the specified Verification commands against the current `HEAD` and report the story complete when they pass. At dispatch start, snapshot overlap with `git status --porcelain=v1 --untracked-files=all --` followed by the four exact code paths listed under Execution; only staged, unstaged, or untracked entries returned for those four code paths are an unexpected uncommitted overlap. The resolver-approved edit to this spec, the committed implementation delta from `baseline_commit`, and changes outside those four code paths are excluded from the overlap gate. When that dispatch-start snapshot is empty and every specified Verification command runs and satisfies its stated expected result, the only conforming outcome is complete; the absence of a fresh implementation diff and repository-wide dirtiness cannot override that result. Return blocked only for a dispatch-start overlap in one of the four code paths or when a specified Verification command cannot execute or does not satisfy its expected result. Every blocked outcome MUST identify either the exact overlapping Execution path and its dispatch-start status evidence or the exact Verification command and its concrete failure result; a generic blocked outcome without that evidence is non-conforming. At finalization, including after any bundle-owned commit, a non-empty repository-wide status that names only paths outside the four Execution code paths is informational and MUST NOT produce a blocked outcome; leave those paths untouched and return complete once every specified Verification command passes. This story-specific terminal-outcome rule takes precedence over any generic repository-cleanliness, bundle-finalization, or post-commit clean-tree requirement.

**Non-qualifying blocked outcomes:** On a fresh re-drive, any prior blocked outcome whose evidence is absent or is not one of the permitted blocker forms—including an unscoped or repository-wide status that reports only paths outside the four Execution code paths—is non-conforming historical session output, even when it names those unrelated paths. Do not inherit it as a blocker or require it to be disproved; determine the new outcome solely from the fresh dispatch-start scoped status snapshot and fresh Verification results.

**Never:** Rename Keycloak implementation concepts, change production AppHost topology merely to simplify tests, change package IDs/versions/inventory, publish artifacts, edit `references/**`, edit `_bmad-output/implementation-artifacts/deferred-work.md`, or reopen already-ledgered CLI-flag, version-example, shared-temp, GitHub-asset-glob, or stale-ledger-citation findings.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Stale wait identity | Tracked operator text contains `aspire wait keycloak` with normal option variants | The committed naming audit reports the tracked path and line | A clean no-match result remains valid only after pattern and pathspec controls pass |
| Hostile caller environment | Persistent mode and invalid Keycloak port variables are set before actual-AppHost model tests | Tests build the intended default model and restore every original value afterward | Construction failures cannot depend on ambient environment state |
| Realm/import contract | Default and explicit realm/import options build a security resource | Realm URL and import annotation reflect the selected values without changing the `security` role | Missing or mismatched annotations fail focused model tests |
| Repeated dependency group | One TFM appears in multiple nuspec groups whose union contains required edges | Both release validators reject the archive | Diagnostic identifies the archive and repeated target framework |
| Extra publication command | Semantic-release contains the valid EventStore push plus any second NuGet push/operand | Governance test rejects the configuration | Exactly one scoped push remains accepted |
| Completed-work re-drive | All Execution items are checked, their implementation is already present at `baseline_revision`, and the dispatch-start status snapshot is empty for the four Execution code paths | Treat the dispatch as verification/finalization-only, require no new implementation diff, rerun Verification at current `HEAD`, and return complete when every command satisfies its expected result | A blocked result must name either the exact overlapping Execution path and its dispatch-start status evidence or the exact failed or unexecutable Verification command and its concrete result; any evidence-free generic blocked outcome, including one that caused the current pause, is not blocker evidence and cannot be carried forward |
| Owned-path overlap | The dispatch-start status snapshot reports a staged, unstaged, or untracked entry for one of the four Execution code paths | Preserve the overlapping change and return blocked with the exact path and status evidence | Do not classify the resolver-approved spec edit, committed implementation history, or changes outside the four Execution code paths as overlap |
| Unrelated dirty workspace | Pre-existing or concurrent changes exist outside the four Execution code paths | Leave those changes untouched and report the story complete after every specified verification passes, irrespective of repository-wide dirty status | A repository-wide status naming only paths outside the four Execution code paths is evidence of non-overlap, not a blocker, and MUST NOT be used as a finalization gate; this scoped outcome overrides generic clean-tree finalization policy |

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
- Given all Execution items are checked and their implementation is already present at `baseline_revision`, when the story is dispatched with an empty `git status --porcelain=v1 --untracked-files=all --` snapshot for the four exact Execution code paths, then the session does not recreate the implementation or require a fresh source diff; it reruns the specified Verification commands at current `HEAD` and must report the story complete when every command satisfies its stated expected result.
- Given a session returns blocked, then it identifies either a staged, unstaged, or untracked dispatch-start entry in one of the four exact Execution code paths or an exact Verification command that could not execute or did not satisfy its expected result, and includes the concrete path/status or command failure evidence; a generic blocked outcome without that evidence is non-conforming.
- Given any prior blocked outcome whose evidence is absent or outside the permitted blocker forms, including repository-wide dirty-status evidence naming only paths outside the four Execution code paths, when the story is freshly re-driven, then that outcome is non-conforming historical session output rather than blocker evidence; only fresh owned-path overlap or Verification-command failure or expected-result mismatch may block, and if the fresh scoped snapshot is empty and every Verification command passes, the session must report the story complete.
- Given pre-existing or concurrent changes only outside the four Execution code paths, or the only owned artifact change is this resolver-approved spec edit, when every specified verification passes and finalization runs, then finalization reports the story complete while unrelated changes remain untouched; this story-specific result overrides any generic repository-cleanliness or post-commit clean-tree requirement, and repository-wide dirtiness, committed implementation history, and the absence of a fresh diff do not block.

## Spec Change Log

- 2026-09-01 -- Implemented all five independent follow-up review guards without changing production topology, release inventory, or publication configuration; added focused mutation coverage and reproduced every verification command at baseline `28cd5935a156600b52f95b378f9c45ab57ba46cb`.
- 2026-09-01 -- Applied the first review pass: widened the tracked audit and wait mutations, independently proved hostile environment restoration, strengthened realm/import assertions, rejected mixed ungrouped dependency shapes, preserved distinct grouped metadata, and scanned every semantic-release exec command field for foreign NuGet pushes.
- 2026-09-03 -- Resolved the finalization ambiguity: unrelated changes outside the story-owned paths remain untouched and do not block completion after all specified verification passes; owned-path overlap or failed verification remains blocking.
- 2026-09-05 -- Clarified that `baseline_revision` is the re-drive anchor, `baseline_commit` is the original scoped-diff base, and the completed story re-drives for verification/finalization without requiring reimplementation or a fresh source diff; scoped cleanliness covers only the four Execution paths and this spec.
- 2026-09-05 -- Resolved outcome routing: after all specified verification passes with no unexpected owned-path overlap, completion is mandatory; no fresh diff and unrelated workspace changes are not valid blocked outcomes.
- 2026-09-05 -- Defined the overlap gate as dispatch-start status entries in the four Execution code paths and required every blocked outcome to carry exact path or Verification-command failure evidence.
- 2026-09-05 -- Made an earlier evidence-free generic blocked outcome non-sticky so a fresh re-drive routes only from its new scoped status snapshot and Verification results.
- 2026-09-05 -- Clarified that any evidence-free generic blocked outcome, including the outcome that caused the current pause, is non-conforming session output rather than blocker evidence and cannot be carried into the re-drive.
- 2026-09-05 -- Classified named but out-of-scope dirty-path evidence as non-qualifying and prohibited repository-wide status from becoming a finalization gate when the four Execution paths remain clean and Verification passes.
- 2026-09-05 -- Made the scoped complete outcome explicitly override generic repository-cleanliness, bundle-finalization, and post-commit clean-tree requirements.
- 2026-10-08 -- Completed the verification-only re-drive: all seven specified commands passed with an empty dispatch-start four-path snapshot. Preserved the implemented source and original baseline identifiers, recorded fresh results and independent review triage, and retained additional pre-existing follow-ups in frontmatter because the intent prohibits editing deferred-work.md.

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

### 2026-10-08 — Verification re-drive review

- verdicts: 15 findings — high 0, medium 10, low 5, false 0, maybe-false 0
- routes: 0 implementation patches; 11 deferred findings grouped into 9 additional follow-ups; 4 rejected findings. Every finding concerns unchanged, committed source; this run changed only workflow status and evidence. The original two deferred items remain recorded. The intent's prohibition on editing `deferred-work.md` takes precedence over the workflow's generic ledger instruction, so new follow-ups are retained in frontmatter here.
- review input: the four Execution code paths from original baseline `28cd5935a156600b52f95b378f9c45ab57ba46cb` through the current working tree. The spec was provided separately only to the claims reviewer, and unrelated committed and workspace paths were excluded.
- findings:
  - `[medium]` `[defer]` **Blind B1:** Direct dependencies plus a complete named group are accepted by the current shared parser and internal contract, although NuGet disallows mixing these formats. Reproduced with a disposable Gateway archive; this invalid-shape acceptance predates the verification re-drive. Primary format evidence: https://learn.microsoft.com/en-us/nuget/reference/nuspec#dependency-groups.
  - `[low]` `[defer]` **Blind B2:** Valid direct dependencies and standalone missing/empty/whitespace-framework fallback groups have no positive fixture. The accepting test covers named groups and existing ungrouped cases deliberately fail; retain this pre-existing coverage gap separately from repeated-group rejection.
  - `[low]` `[reject]` **Blind B3:** Carried: quoting a NuGet command token can evade the literal invocation guard. The 2026-09-01 review already rejected shell-obfuscation parsing; current configuration uses the exact approved command and no such indirection, so this uncommon case does not justify adding a shell interpreter during finalization.
  - `[medium]` `[defer]` **Blind B4:** The PayloadProtection governance test never examines workflow triggers, so deleting automatic triggers or adding restrictive filters leaves its parsed job assertions unchanged. Current triggers are correct; this is pre-existing mutation coverage hardening.
  - `[medium]` `[defer]` **Blind B5:** Background shell operators are not forbidden by the PayloadProtection command assertions. Isolated `false & wait` and `false & true` probes exited 0, and the checked token counts do not reject those suffixes; current workflow commands contain neither. Grouped with Edge E2.
  - `[medium]` `[defer]` **Blind B6:** A heavyweight trait above Fact/Theory is omitted by `AttributePreludeBeforeMethod`, allowing its negative guard to accept a CI-excluded malformed-input test. Reproduced the extraction on isolated source text; current attribute ordering is correct.
  - `[low]` `[reject]` **Blind B7:** The tracked wait regex also matches implementation names in an apphost option path or subsequent echo command; all three allowed forms reproduced in a disposable tracked fixture. The continuation-line miss is carried from the prior low/reject row. These uncommon forms require operand or shell parsing beyond a direct correction, so retain the existing conservative line-oriented audit.
  - `[medium]` `[defer]` **Blind B8:** Fixture Git commands inherit repository selectors. A disposable caller/fixture pair proved that `GIT_INDEX_FILE` causes fixture `git add` to change the caller's index, while `RunGitAsync` contains no environment sanitization. No real repository index was used in the probe. Grouped with Edge E4.
  - `[medium]` `[defer]` **Blind B9:** The generated-client credential test checks a manually rendered Tenants template instead of its security resource's actual default import callback. The two source templates differ in the SDK-account placeholder, so this test can miss import-template drift; the separately rendered production AppHost path is not disproved by this finding.
  - `[medium]` `[defer]` **Blind B10:** The credential-test template path hardcodes a nested Tenants checkout while the shared resolver and source-mode build configuration support other layouts. A sibling or enclosing checkout leaves the hardcoded file unavailable; current standalone tests passed and no nested initialization was performed.
  - `[low]` `[reject]` **Edge E1:** Carried: quoting the dotnet executable hides a second publisher from the literal regex. This is the same unsupported shell-obfuscation claim already rejected on 2026-09-01, and the live publication command remains exact and singular.
  - `[medium]` `[defer]` **Edge E2:** Appending `& true` can conceal a failing PayloadProtection command without changing the tested flags. The isolated shell probe returned 0; this shares Blind B5's missing execution-shape guard and is one follow-up.
  - `[low]` `[reject]` **Edge E3:** An apphost option path containing `Aspire.Hosting.Keycloak` falsely matches the case-insensitive wait guard. Reproduced with tracked text; this shares Blind B7's uncommon operand-parsing limitation and does not change the positive audit result for the current repository.
  - `[medium]` `[defer]` **Edge E4:** An inherited `GIT_INDEX_FILE` routes fixture staging into the caller's index. Confirmed with two disposable repositories, sharing Blind B8's inherited-selector root cause; current required tests passed without this selector.
  - `[medium]` `[defer]` **Verification V1:** Repeated blank or missing-framework partial groups lack a rejection mutation through either validator. Accepted the reviewer's pre-verified in-memory mutation evidence: skipping an empty framework key permits unioning the partial groups, while current parser code rejects them. Retain this existing regression-coverage gap as a follow-up.
- independent probe evidence: `/tmp/eventstore-followup-review-probes-y_b66x5l/results.log`. No source or configuration was changed by review probes.
- completion: the empty dispatch-start snapshot and all seven passing Verification commands satisfy the spec's mandatory complete outcome. Review follow-ups do not reopen checked implementation work or introduce an unpermitted blocker.

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

**Recorded Results (2026-10-08 verification re-drive):** The dispatch-start status snapshot was empty for all four Execution code paths at `762a745426db66c2846b1af17a10b2a619bf3d95`. All seven specified Verification commands passed; both Release builds reported zero warnings/errors, naming tests passed 5/5 under hostile persistent/invalid-port environment values, security extension tests passed 16/16, and packaging tests passed 115/115, with no failures, skips, or unrun cases. The dry run emitted exactly 14 Release/package-mode commands and created no output directory. The scoped original-baseline whitespace check exited 0 with no output. Verification finished at `4cc77f9554395e84539e173e94b8f0b4df14d643`; none of the four Execution code paths changed between those revisions. Logs are retained under `/tmp/eventstore-followup-verification.yn4Sje/`. Existing implementation, deferred work, and unrelated workspace changes were preserved.
