---
title: 'Update all .NET SDK references to 10.0.302'
type: 'chore'
created: '2026-07-16'
status: 'in-review'
review_loop_iteration: 1
baseline_commit: '54b1b449ae20d9e3fd8f1072c0e35c2e3af28bd3'
context:
  - 'references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The root EventStore repository and several root-declared submodules still contain tracked text references to the two predecessor .NET SDK patches ending in `300` and `301`, although `10.0.302` is the requested baseline. The stale references span current configuration, workflows, source messages, tests, documentation, generated evidence, archived planning artifacts, and changelogs.

**Approach:** Perform an exhaustive literal replacement of both exact substrings with `10.0.302` in every matching tracked text file owned by the root repository or one of its seven root-declared submodules. Preserve all other content, then prove that neither old substring remains in any in-scope repository.

## Boundaries & Constraints

**Always:** Treat the user's “all files” instruction literally: current files, archived artifacts, historical evidence, patch fixtures, and changelogs are all in scope despite the fact that this rewrites recorded historical SDK values. Replace occurrences embedded in larger strings too, including preview-version strings. Work only in the clean `main` worktrees already inspected. Preserve encoding, line endings, surrounding prose, and every version other than the two named predecessor patches. Keep coupled documentation assertions synchronized with their source documents.

**Ask First:** Halt if a match cannot be changed by a plain text substitution, if a binary or ignored/generated build artifact appears to require mutation, if any repository develops unrelated changes, or if completing the work would require initializing or editing a nested submodule.

**Never:** Do not edit `.git` metadata, ignored `bin`/`obj`/package outputs, or uninitialized nested submodules. Do not initialize, update, or recurse into nested submodules. Do not change target frameworks, ASP.NET/package versions, roll-forward policies, or prose beyond the requested literal substitutions. Do not commit, push, or change submodule pointers as part of this task.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Current baseline | A tracked text occurrence of either named predecessor patch | The occurrence becomes exactly `10.0.302`; surrounding bytes remain unchanged | Stop if literal replacement is unsafe |
| Embedded version | An old token is part of a preview string, command, link text, test fixture, or comparison | Replace only the matched old token, retaining suffixes and surrounding syntax | Verify the resulting file remains textually well formed |
| Historical record | A changelog, evidence packet, archive, or old sprint artifact names an old SDK | Replace it with `10.0.302` without a historical exception | Record the intentional history rewrite in the review summary |
| Already current / no match | A file or submodule has no old token | Leave it byte-for-byte unchanged | Treat any unrelated diff as a failure |

</frozen-after-approval>

## Code Map

- `CONTRIBUTING.md`, `docs/**`, `_bmad-output/**` -- root repository inventory: 52 replacements across 31 tracked files; root `global.json` is already current.
- `references/Hexalith.Tenants/{CHANGELOG.md,_bmad-output/**,docs/**,tests/**}` -- 22 replacements across 19 tracked files; includes a documentation assertion coupled to `docs/quickstart.md`.
- `references/Hexalith.FrontComposer/{.github/**,CHANGELOG.md,Directory.Packages.props,_bmad-output/**,artifacts/**,docs/**,jobs/**,tests/**}` -- 107 replacements across 72 tracked files; includes live CI pins and IDE-parity metadata/tests.
- `references/Hexalith.Memories/{README.md,global.json,_bmad-output/**,docs/**,src/**,tests/**}` -- 42 replacements across 18 tracked files; includes the remaining active SDK pin and CLI prerequisite behavior/tests.
- `references/{Hexalith.AI.Tools,Hexalith.Commons,Hexalith.Builds,Hexalith.PolymorphicSerializations}` -- inspected root-declared submodules with zero old-version matches; must remain unchanged.

## Tasks & Acceptance

**Execution:**
- [x] Root paths in the Code Map -- replace all 52 old-version occurrences with `10.0.302` using a byte-minimal bulk text rewrite.
- [x] `references/Hexalith.Tenants/**` -- replace all 22 occurrences, keeping quickstart documentation and its assertion aligned.
- [x] `references/Hexalith.FrontComposer/**` -- replace all 107 occurrences, including workflow pins, IDE-parity contracts, evidence, and historical artifacts.
- [x] `references/Hexalith.Memories/**` -- replace all 42 occurrences, including `global.json`, CLI messages/checks, documentation, tests, and artifacts.
- [x] All eight repository worktrees -- rescan tracked text, inspect diffs, and confirm zero unintended files, nested submodule changes, or whitespace errors.

**Acceptance Criteria:**
- Given the initial inventory of 223 occurrences across 140 tracked files, when implementation completes, then every in-scope occurrence is `10.0.302` and exhaustive tracked-file scans find zero matches for either predecessor patch.
- Given a matching historical or archived file, when replacement runs, then it is updated without exemption and no surrounding narrative is independently rewritten.
- Given the four root-declared submodules with no matches and all uninitialized nested submodules, when diffs are inspected, then those four submodules and every nested submodule remain unchanged.
- Given active documentation/source assertions in Tenants, FrontComposer, and Memories, when focused verification runs, then each affected contract remains internally consistent and passes.

## Spec Change Log

## Verification

**Commands:**
- Run `git grep -n -I -E '10\.0\.30(0|1)'` independently in the root and each root-declared submodule -- expected: no output in all eight repositories.
- Run `git diff --check` independently in each changed repository and inspect `git diff --stat`/`git diff` -- expected: no whitespace errors and only literal old-to-new substitutions.
- `dotnet test references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Hexalith.Tenants.Server.Tests.csproj` -- expected: documentation assertion remains green under SDK `10.0.302`.
- `DiffEngine_Disabled=true dotnet test references/Hexalith.FrontComposer/tests/Hexalith.FrontComposer.SourceTools.Tests/Hexalith.FrontComposer.SourceTools.Tests.csproj` -- expected: IDE-parity SDK contract remains green.
- `dotnet test references/Hexalith.Memories/tests/Hexalith.Memories.Cli.Tests/Hexalith.Memories.Cli.Tests.csproj` -- expected: CLI SDK prerequisite assertions remain green.

**Implementation evidence (2026-07-16):**
- Replaced the inventoried 223 occurrences across 140 tracked text files. Independent tracked-file scans in the root and all seven root-declared submodules return no predecessor-version matches.
- Canonical and worktree-filter-aware comparisons confirm every changed tracked file equals its `HEAD` image with only the two approved literal substitutions. The four zero-match submodules and every uninitialized nested submodule remain unchanged.
- `git diff --check` is clean in the root, Tenants, Memories, and all unchanged submodules. FrontComposer reports the preserved CRLF on `Directory.Packages.props:16` as trailing whitespace; byte inspection confirms the line ending was not introduced or changed by this work.
- Tenants server tests passed: 738 passed, 0 failed. The directly affected FrontComposer IDE-parity contract passed: 5 passed, 0 failed under SDK `10.0.302`; its full project run passed 1,093 of 1,096 tests, with three unrelated failures including a forbidden-to-initialize nested Builds path.
- Memories CLI tests are restore-blocked by existing `NU1605` errors: `OpenTelemetry.Exporter.OpenTelemetryProtocol 1.17.0` requires `OpenTelemetry >= 1.17.0`, while the repository centrally selects `OpenTelemetry 1.16.0`. No build gate was weakened.
- Historical and archived SDK values were intentionally rewritten as required by the approved all-files scope.
- Independent matrix audit passed: canonical/worktree-filter-aware comparisons cover current, embedded-preview, and historical occurrences across all 140 changed files; clean status checks cover already-current files and the four zero-match submodules. A static alignment test also confirmed 14 affected live configuration, documentation, source, and assertion files consistently name `10.0.302`.
- A focused Memories retry with `--no-restore` reached the same pre-existing `NU1605` gate, confirming the blocker is embedded in restored package assets rather than network restore. The exact-substitution and live-contract audits pass without suppressing that error.
