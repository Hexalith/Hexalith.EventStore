# Story Post-Epic-2 R2-A5b: Eliminate DAPR SDK Version Prose Restatement — Make `Directory.Packages.props` the Single Source of Truth

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a project maintainer,
I want the shipped documentation to stop restating the DAPR SDK version number in prose and to instead point readers at `Directory.Packages.props` as the single source of truth, with a CI guard rail that asserts the table cells in `docs/reference/nuget-packages.md` match the props pin,
so that the version-prose-drift class that produced R2-A5 is **structurally eliminated for prose** and **converted from silent festering lag to hard CI failure** for the table-cell exceptions that remain — instead of being serially patched on every future SDK bump.

This story is the **structural cure** complement to the **symptom fix** shipped in `post-epic-2-r2a5-dapr-sdk-version-reconcile` (closed at SHA `e0cbc78`, 2026-04-28). R2-A5 reconciled 9 stale `1.16.1` references to `1.17.7`; this story (R2-A5b) removes the restatement pattern entirely from prose (5 sites → 0) and adds a CI assertion for the 4 legitimate table-cell exceptions that remain. The originating spec is `_bmad-output/implementation-artifacts/post-epic-2-r2a5-dapr-sdk-version-reconcile.md` § Suggested Future-Work Spin-Offs item #5 (lines 322–327) and the ADR at line 259 ("Symptom fix vs. structural cure"). The sibling backlog spawn was logged in `sprint-status.yaml:279` at R2-A5 creation time.

**Honest framing of the cure.** The story title says "structurally eliminated" but the cure is asymmetric across the two restatement sites:

- **Prose drift class (5 sites, AC #1–#4): structurally eliminated.** Post-patch, no prose names a version; a future props bump propagates with zero doc edits required.
- **Table-cell drift class (4 cells, AC #5/#6): converted, not eliminated.** The cells still need manual edit on every props bump (the version *is* the data — that's what readers come to a NuGet-packages reference for). What changes: the bump becomes a hard CI failure on the props-bump PR (script step blocks merge) instead of a silent six-month-rotting lag picked up at the next retro. Net manual-edit count per bump: 9 → 4. Net silent-drift opportunity: 9 → 0.

**Strategy.** Two complementary changes:

1. **Prose (5 sites in 4 files):** rewrite `(currently 1.17.7 — last verified April 2026)`-style prose so it carries no version number and no verification date stamp. The replacement points at `Directory.Packages.props` as the authoritative pin via a clickable markdown link (not a code-span — readers can navigate to the source of truth in one click).
2. **Reference table (4 cells in 2 tables in 1 file):** the four `Dapr.*` rows in `docs/reference/nuget-packages.md` are the legitimate exception. Add a small `scripts/check-doc-versions.sh` that parses the props pin and asserts the four table cells match, plus a `docs-validation.yml` CI step that runs it on every PR.

This story does NOT:

- Touch `Directory.Packages.props` or any `.csproj` (the props pin remains the source of truth at `1.17.7`).
- Change any DAPR SDK API call site, `using` directive, or runtime configuration.
- Modify any `src/**/*.cs` or `tests/**/*.cs` file.
- Bump the DAPR runtime pin in `docs/guides/deployment-kubernetes.md:143` (`dapr init -k --runtime-version 1.14.4`) — runtime ↔ SDK compatibility decision, separate concern. (Tracked as the runtime-pin sibling at `post-epic-2-r2a5d-runtime-version-bump-deployment-docs`.)
- Touch `docs/getting-started/prerequisites.md` CLI/runtime stamps (carved to `post-epic-2-r2a5c-prerequisites-cli-runtime-stamps` per R2-A5 review finding D2).
- Touch `deploy/README.md` `daprio/daprd:1.16.1` sidecar-image pins (carved to `post-epic-2-r2a5d-...` per R2-A5 review finding D3).
- Touch the `docs/guides/upgrade-path.md:141` compatibility-matrix `1.17.x+` row — that is a forward-looking compatibility floor (`1.17.x or later`), a meaningful claim distinct from the literal pin. Leaving it as prose is correct; it is not a restatement of the props pin and does not produce drift in the same way.
- Touch the `Hexalith.Tenants` submodule.
- Re-introduce any `1.16.1` reference (R2-A5's grep audit must remain green).

## Acceptance Criteria

1. **`CLAUDE.md` § Key Dependencies — DAPR SDK line carries no version number; props reference is a clickable link.** Line 195 (verbatim today: `- DAPR SDK 1.17.7 (Client, AspNetCore, Actors)`) is rewritten to `- DAPR SDK (Client, AspNetCore, Actors) — pinned in [\`Directory.Packages.props\`](Directory.Packages.props)`. The "(Client, AspNetCore, Actors)" parenthetical is preserved (closes R2-A5 review finding D1's deferred 4-into-3 gloss by removing the version that made the gloss load-bearing). The `Directory.Packages.props` reference is a **markdown link** (not a code-span) — clickable navigation to the source of truth in one click. Relative path is bare `Directory.Packages.props` because `CLAUDE.md` lives at repo root alongside the props file. The other 4 bullets in § Key Dependencies (`.NET Aspire 13.1.x`, `MediatR 14.0.0`, `FluentValidation 12.1.1`, `OpenTelemetry 1.15.x`) remain as-is — they are out of scope for R2-A5b's narrow DAPR-SDK focus and a "make all version mentions point at props" sweep is a separately-decidable scope expansion. **Reviewer-pushback option:** if a reviewer argues for symmetric treatment (extend the prose-pointer pattern to all 5 bullets), accept it as in-scope; the script in AC #6 already validates against `Directory.Packages.props` so extending the assertion to those 4 packages is a minor change. Default: DAPR-only. (Logged as Future-Work item #6 if not adopted in this PR.)

2. **`docs/concepts/choose-the-right-tool.md` — version coupling section carries no version number, no date stamp; props reference is a clickable link.** Line 193 (verbatim today: `Hexalith depends on a specific DAPR SDK version (currently 1.17.7, as pinned in \`Directory.Packages.props\`, last verified April 2026). DAPR follows...`) is rewritten to `Hexalith depends on a specific DAPR SDK version pinned in [\`Directory.Packages.props\`](../../Directory.Packages.props) (the single source of truth). DAPR follows...`. The remainder of the paragraph (DAPR SemVer language, CI pipeline statement at lines 193–194) is preserved byte-identical. **No `currently <X.Y.Z>`; no `last verified <month> <year>`.** Markdown link, not code-span — relative path is `../../Directory.Packages.props` (file lives at `docs/concepts/`, props at repo root).

3. **`docs/guides/dapr-faq.md` — both occurrences carry no version number, no date stamp; props references are clickable links.** Two prose mentions are updated:
   - Line 43 (TL;DR): `Hexalith pins to a specific SDK version (currently 1.17.7) and CI verifies on every commit.` → `Hexalith pins to a specific SDK version in [\`Directory.Packages.props\`](../../Directory.Packages.props) and CI verifies on every commit.`
   - Line 47 (body): `Hexalith pins the DAPR SDK version in \`Directory.Packages.props\` (currently **1.17.7** — last verified April 2026). The CI pipeline tests against this pinned version on every commit.` → `Hexalith pins the DAPR SDK version in [\`Directory.Packages.props\`](../../Directory.Packages.props) — that file is the single source of truth. The CI pipeline tests against the pinned version on every commit.`

  Both occurrences become markdown links, not code-spans (relative path `../../Directory.Packages.props` since file lives at `docs/guides/`).

4. **`docs/guides/deployment-kubernetes.md` — note carries no version number; props reference is a clickable link.** Line 138 (verbatim today: `> **Note:** The project uses DAPR SDK version **1.17.7** (see \`Directory.Packages.props\`). Use a compatible DAPR runtime version. Consult the [DAPR SDK-to-runtime compatibility matrix](https://docs.dapr.io/operations/support/support-release-policy/) for version mapping.`) is rewritten to `> **Note:** The project pins the DAPR SDK in [\`Directory.Packages.props\`](../../Directory.Packages.props) (the single source of truth). Use a compatible DAPR runtime version. Consult the [DAPR SDK-to-runtime compatibility matrix](https://docs.dapr.io/operations/support/support-release-policy/) for version mapping.`. The blockquote `>` prefix, the `**Note:**` label, the existing compatibility-matrix link, and the surrounding "Use a compatible DAPR runtime version. Consult..." prose are preserved. The `Directory.Packages.props` reference becomes a markdown link (relative path `../../Directory.Packages.props` since file lives at `docs/guides/`). **Do NOT change the `dapr init -k --runtime-version 1.14.4` command at line 143** — runtime pin, separate concern, out of scope (tracked at `post-epic-2-r2a5d-...`).

5. **`docs/reference/nuget-packages.md` — 4 `Dapr.*` table cells preserved verbatim; no marker comments added; no row reorder.** The four table cells at lines 158, 182, 183, 184 currently read `1.17.7`. They MUST remain `1.17.7` post-patch (this story does NOT change them; AC #6's script validates them). HTML comments / fenced markers MUST NOT be inserted into the markdown tables (HTML comments inside a `| ... |` row break GitHub's markdown table render). The validation in AC #6 is line-based regex over the file, not marker-driven. **Rationale:** the table rows are reference-card data where the version *is* the data; eliminating the version number from the cell would make the table useless. Instead, this AC keeps the data and adds a CI guard rail that asserts it matches the props pin.

6. **`scripts/check-doc-versions.sh` exists and asserts pin consistency.** A new bash script at `scripts/check-doc-versions.sh` is added with these properties:
   - Shebang `#!/usr/bin/env bash`; `set -euo pipefail`; matches the style of the existing `scripts/validate-docs.sh`.
   - Header comment block declares the four documented assumptions (single-line `<PackageVersion>` shape; each Dapr.* appears exactly once in props; nuget-packages.md uses the fixed-width DAPR-row pattern; bash 4+ for associative arrays). If a future maintainer violates one of these, the rewrite path is `xmllint` or `dotnet msbuild -getProperty:` — flagged in the header so the rewrite trigger is obvious.
   - **Pre-flight multi-line-element guard.** Before extracting versions, the script runs `grep -cE "<PackageVersion[^/]*$" Directory.Packages.props` to detect multi-line `<PackageVersion>` elements (which the regex parser cannot handle correctly). If `> 0` matches: exit non-zero with `ERROR: multi-line <PackageVersion> element detected in Directory.Packages.props — REWRITE NEEDED. See script header comment for migration to xmllint or 'dotnet msbuild -getProperty:'.` This prevents the failure mode where a future props-file refactor (e.g., adding `Condition` attributes that wrap the element across lines) silently produces incorrect parses; the script fails loudly with a named rewrite path instead.
   - Reads `Directory.Packages.props` and extracts the `Version` attribute of the four `<PackageVersion Include="Dapr.Client" />`, `<PackageVersion Include="Dapr.AspNetCore" />`, `<PackageVersion Include="Dapr.Actors" />`, `<PackageVersion Include="Dapr.Actors.AspNetCore" />` entries (lines 6–9 of the props file at HEAD `e0cbc78`).
   - **Asserts each Dapr.* package appears in the props file exactly once.** If 0 matches: exit non-zero with `ERROR: <pkg> not found in Directory.Packages.props`. If > 1 matches: exit non-zero with `ERROR: <pkg> appears <n> times in Directory.Packages.props (expected exactly 1)`. (Replaces the original `head -1` silent-pick.)
   - Asserts all four `Dapr.*` versions are the same string (internal-consistency check). If they differ, exits non-zero with `ERROR: Directory.Packages.props has divergent Dapr.* pins:` followed by the per-package version listing.
   - Reads `docs/reference/nuget-packages.md` and extracts the version cell for each line matching one of these table-row patterns:
     - `| Dapr.Client \| <version>  |` (appears twice — Client package's external-deps table at line 158, Server package's external-deps table at line 182)
     - `| Dapr.Actors \| <version>  |` (line 183)
     - `| Dapr.Actors.AspNetCore \| <version>  |` (line 184)
   - The regex MUST be line-number-agnostic (does not hardcode 158/182/183/184) — line numbers will shift if the file is edited, and a hardcoded line check would be a fragile gate.
   - Asserts each extracted cell matches the props pin. On mismatch, exits non-zero with `MISMATCH at docs/reference/nuget-packages.md:<line>: '<pkg>' cell shows '<actual>', Directory.Packages.props pins '<expected>'`.
   - **Asserts exactly 4 `Dapr.*` table rows are matched in the doc.** If `< 4` (a future doc edit deleted a row, or the regex matches 0 because the table format drifted): exit non-zero with `ERROR: expected exactly 4 Dapr.* table rows in docs/reference/nuget-packages.md, found <n>`. If `> 4` (a future maintainer added a fifth Dapr.* package row, or the table grew duplicate rows): same error, different `<n>`. (Closes the silent-pass-on-empty-doc hole — without this check, a fully-deleted DAPR table would pass the regex loop with zero iterations.)
   - On success, prints `PASSED: DAPR SDK version pin consistency (<version>, <rows_seen> rows verified)` and exits 0.
   - Total script length ≤ 80 lines (small-and-readable; the regex/parsing is straightforward).
   - The script is invocable from the repo root via `bash scripts/check-doc-versions.sh` (preferred — bypasses executable-bit issues on Windows-checked-out trees and matches the workflow invocation in AC #7). The executable bit MAY also be set with `git update-index --chmod=+x` for direct `./scripts/check-doc-versions.sh` invocation on Linux/WSL, but the `bash` prefix is the canonical contract.
   - **The script does NOT modify any file** (read-only check; no docs are mutated).

7. **`.github/workflows/docs-validation.yml` — new step in `lint-and-links` job.** The `lint-and-links` job (lines 22–47 of the workflow at HEAD `e0cbc78`) gains a new step **after** the "Lint Markdown" step (line 32–33) and **before** "Restore lychee cache" (line 35–40):

    ```yaml
          - name: Verify DAPR SDK version pin consistency
            run: bash scripts/check-doc-versions.sh
    ```

    Step name and shell invocation MUST match this verbatim (the `bash` prefix avoids any executable-bit ambiguity on cross-platform trees). The step has no `continue-on-error` (it is a hard gate). No other step in the workflow is reordered, renamed, or removed. The `sample-build` job is untouched.

8. **`scripts/validate-docs.sh` — new Stage 4 invokes the same script.** The local-dev mirror at `scripts/validate-docs.sh` (lines 1–62 currently) gains a new Stage 4 between the existing Stage 3 ("Sample Build & Test") and the final `=== All validations passed ===` line. The new stage:
    - Sets `CURRENT_STAGE="DAPR SDK version pin consistency"`
    - Runs `bash scripts/check-doc-versions.sh` (matches the workflow invocation in AC #7 verbatim — using the `bash` prefix avoids any executable-bit ambiguity for Windows-checked-out trees and keeps the local-dev path identical to the CI path).
    - Echoes `PASSED: DAPR SDK version pin consistency` on success
    - Updates the existing stage header from `=== Stage 3/3: ===` to `=== Stage 3/4: ===` and the new stage to `=== Stage 4/4: ===` so the stage-counter prose is internally consistent. (Pre-existing scripts/validate-docs.sh:33 says `Stage 1/3`, line 39 says `Stage 2/3`, line 47 says `Stage 3/3` — bump all three to `/4`.)

9. **Final grep audit confirms zero version-number drift opportunities in prose.** After the patch:
    - `Grep` for `1\.17\.7` across `CLAUDE.md` and `docs/**/*.md` returns **exactly 4 matches** — all four in `docs/reference/nuget-packages.md` (the four `Dapr.*` table cells at line numbers near 158, 182, 183, 184; line numbers may shift slightly if a sibling edit has touched the file). Pre-patch baseline (HEAD `e0cbc78`): **9 matches**. Net delta: 5 prose mentions removed.
    - `Grep` for `1\.17\.x` across `docs/**/*.md` returns **exactly 1 match** at `docs/guides/upgrade-path.md:141` (the compatibility-matrix row, deliberately preserved). Pre-patch baseline: 1 match. Net delta: 0.
    - `Grep` for `1\.16\.1` across `CLAUDE.md` and `docs/**/*.md` returns **0 matches** (this is R2-A5's post-patch state and MUST remain green; this story MUST NOT regress it).
    - `Grep` for `last verified March 2026` and `last verified April 2026` across `docs/**/*.md` returns **0 matches** total (the prose-pointer rewrites in AC #2 + AC #3 remove the date-stamp pattern entirely; this is the "no date stamp" half of the structural cure). Pre-patch baseline: 2 matches (`last verified April 2026` × 2 in choose-the-right-tool.md:193 and dapr-faq.md:47). Net delta: −2.
    - `Grep` for `currently 1\.\d+\.\d+` across `docs/**/*.md` returns **0 matches** (the `currently <X.Y.Z>` parenthetical pattern is fully eliminated from the targeted-doc set). Pre-patch baseline: 2 matches. Net delta: −2.
    - Out-of-scope `1.16` references that MUST still appear unchanged after the patch: `docs/guides/dapr-component-reference.md` lines 320 / 478 / 634 (`SCOPING FIELD REFERENCE (DAPR 1.16)`); `CONTRIBUTING.md:50` (`DAPR CLI (1.16.x or later)`); `docs/guides/deployment-kubernetes.md:143` (`runtime-version 1.14.4`); `docs/getting-started/prerequisites.md` lines 71/111/136/137/157 (`1.16.x` CLI/runtime stamps — carved to r2a5c). Verify these survive the patch with a `Grep "1\.16"` cross-check and confirm the count matches pre-patch.

10. **No source-code, test, or project-file change.** AC verifies the diff scope:
    - `Directory.Packages.props` is **NOT** modified (the source of truth is correct as-is at `1.17.7`).
    - No `.csproj`, `.props`, `.targets`, or `Directory.Build.*` file is modified.
    - No `src/**/*.cs` or `tests/**/*.cs` file is modified.
    - No CI workflow other than `docs-validation.yml` is modified (specifically: `ci.yml`, `release.yml`, `deploy-staging.yml`, `docs-api-reference.yml`, `perf-lab.yml` are all untouched).
    - No `package.json`, `package-lock.json`, `nuget.config`, `global.json`, or `aspire.config.json` is modified.
    - **`AGENTS.md` and `CONTRIBUTING.md` are NOT modified** (CONTRIBUTING.md:50 carve-out remains preserved per the R2-A5 precedent).
    - **`docs/guides/upgrade-path.md` is NOT modified** (the `1.17.x+` row at line 141 is a forward-looking compatibility floor, not a literal pin restatement; out of scope per the story header).
    - The diff is therefore: 4 modified `*.md` files (CLAUDE.md, choose-the-right-tool.md, dapr-faq.md, deployment-kubernetes.md) + 1 new file (`scripts/check-doc-versions.sh`) + 2 modified non-doc files (`scripts/validate-docs.sh`, `.github/workflows/docs-validation.yml`) + 1 new test-artifact file (`_bmad-output/test-artifacts/r2a5b-negative-test-proof.txt` — see AC #13) + 3 process-tracking files (`sprint-status.yaml` lifecycle, `epic-2-retro-2026-04-26.md` § 6 R2-A5 row addendum per AC #11, this story file housekeeping) = 11 files net.

11. **Sprint-status updated through the lifecycle + R2-A5 retro row chained forward to R2-A5b's merge SHA.** Two sub-requirements:
    - **(a) Sprint-status lifecycle.** `_bmad-output/implementation-artifacts/sprint-status.yaml` development_status entry `post-epic-2-r2a5b-version-prose-source-of-truth-refactor` is updated through `backlog` → `ready-for-dev` (this story creation, written by the SM workflow) → `in-progress` (dev start) → `review` (PR opened) → `done` (post-merge with closure annotation referencing the merge SHA, the 4 prose-rewrite count, the 1 new script, the 2 CI-tooling edits, and the post-patch grep audit deltas). Mirror the post-epic-2-r2a5 closure-annotation style. `last_updated` bumped to the merge date with a one-line `→ done` summary referencing R2-A5b.
    - **(b) Retro pointer chain.** `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 6 row R2-A5 (line 101 at HEAD `e0cbc78`) currently ends with R2-A5's closure annotation that names this story (`r2a5b`) by key. Append a single addendum to that cell pointing at R2-A5b's merge SHA: ` → R2-A5b structural cure ✅ Closed <r2a5b-merge-sha>` after the existing closing punctuation of the R2-A5 closure annotation. **Single edit, single line.** Do NOT modify R2-A5's row in any other way; do NOT modify any other row, the § 10 line 161 R2-A5 token (already closed at R2-A5's merge SHA), or anything else in the retro file. Rationale: without this addendum the retro becomes a stale pointer chain (R2-A5 closure says "see r2a5b" → r2a5b closes silently → 6 months later reader follows the chain and finds no terminator). The post-merge runbook substitutes `<r2a5b-merge-sha>` with the actual SHA. Epic 1, 3, 4 retros remain untouched.

12. **Conventional-commit prefix on the merge — `chore:` (mixed scope: docs + CI tooling).** The merge / squashed PR commit uses **`chore:`** per `CLAUDE.md` § Commit Messages. The diff is mixed: 4 doc-prose rewrites + 1 new tooling script + 2 CI-tooling edits. `docs:` would understate the tooling/CI half; `chore:` correctly communicates both. Suggested subject:
    - `chore: refactor DAPR SDK version prose to source-of-truth pattern + CI guard rail`
    - `chore(docs): eliminate DAPR SDK version restatement; add docs-validation pin-consistency check`
    The body MUST name R2-A5b, reference the parent R2-A5 (and item #5 of its Future-Work Spin-Offs at line 322 of `post-epic-2-r2a5-dapr-sdk-version-reconcile.md`), and explicitly note that `Directory.Packages.props` is the source of truth and was NOT touched. **Do NOT use `docs:`** even though the prose rewrites dominate the line-count — the new script and CI workflow step are tooling additions, and `chore:` is the truthful prefix per the project's Conventional-Commits convention. **Do NOT use `feat:` / `fix:` / `BREAKING CHANGE:`** — no published-API surface changes; semantic-release MUST NOT bump any of the 6 NuGet packages on this merge. **Reviewer-pushback option:** if a reviewer argues `docs:` is acceptable since the dominant intent is documentation cleanup, accept it; both `chore:` and `docs:` are no-version-bump prefixes under semantic-release. **Anti-option:** `feat(docs):` or `feat(ci):` — both would bump a NuGet version on a no-code change. Forbidden.

    **Version-bump-fired emergency revert (mirrors R2-A5 AC #10):** if despite the `chore:` prefix semantic-release fires and bumps any of the 6 NuGet package versions, **revert IMMEDIATELY** within the GitHub Releases retention window. Versioning lying about the change is worse than a broken merge; downstream consumers pulling a "minor bump" containing zero code change pollutes lockfiles and erodes pipeline trust. After revert, file a `release-config-drift` story.

13. **`docs-validation.yml` CI run on this PR is green.** Both jobs in the workflow (`lint-and-links` and `sample-build`) MUST pass:
    - **`lint-and-links`** runs the new "Verify DAPR SDK version pin consistency" step, then markdownlint, then lychee. The new step's first invocation MUST exit 0 (the script asserts the four `Dapr.*` cells match the props pin, which they already do at `1.17.7` post-R2-A5). markdownlint MUST be green; the prose-pointer rewrites in AC #1–#4 are not character-count-neutral but the substituted text is ordinary prose with no unusual markup, so no new markdownlint violations are expected. lychee MUST be green; no link targets are added or removed.
    - **`sample-build`** is unaffected by this story (no C# code or test change). MUST remain green; if it fails, the failure is unrelated and either pre-existing on `main` (document and defer) or a transient infrastructure issue (re-run).
    - **Negative tests for the new step (Task 3.4 / Task 5.4) — VERIFIABLE EVIDENCE.** Dev MUST verify the new step *would* fail under each of four drift scenarios (one per error mode the script claims to detect) AND check the four transcripts into the repo at `_bmad-output/test-artifacts/r2a5b-negative-test-proof.txt`. Each scenario uses temp edits that are reverted after the script run; the temp edits do NOT ship, but the **transcripts DO ship** so a reviewer can verify each error mode actually fired without re-running the dev's local box. Without the proof file, AC #6's "asserts" claim is unverified by review — Completion-Notes-only transcripts are too easily handwaved. **Format:** plain-text file with 4 sections (one per scenario), each showing the temp-edit summary, the script invocation, the captured `stderr`/`stdout` lines, and the exit code. ~80 lines total. The file lives in `_bmad-output/test-artifacts/` (the existing test-artifact location used by Story 21-11 navmenu screenshots and Story 21-12 theme screenshots) and is the only test-artifact this story creates. The four scenarios:
        - **(a) Cell-mismatch:** temporarily edit `docs/reference/nuget-packages.md` to change one `Dapr.Client | 1.17.7` cell to `Dapr.Client | 1.17.6`. Expected: exit non-zero with `MISMATCH at docs/reference/nuget-packages.md:<line>: 'Dapr.Client' cell shows '1.17.6', Directory.Packages.props pins '1.17.7'`.
        - **(b) Divergent props pins:** temporarily edit `Directory.Packages.props` to change one `Dapr.Actors` version to `1.17.6` while leaving the other three at `1.17.7`. Expected: exit non-zero with `ERROR: Directory.Packages.props has divergent Dapr.* pins:` followed by the per-package version listing.
        - **(c) Missing package in props:** temporarily comment out the `<PackageVersion Include="Dapr.AspNetCore" ... />` line in `Directory.Packages.props`. Expected: exit non-zero with `ERROR: Dapr.AspNetCore not found in Directory.Packages.props`.
        - **(d) Empty / missing doc rows:** temporarily comment out (or delete) all four `Dapr.*` rows from `docs/reference/nuget-packages.md`. Expected: exit non-zero with `ERROR: expected exactly 4 Dapr.* table rows in docs/reference/nuget-packages.md, found 0`. **This scenario closes the silent-pass-on-empty-doc hole that an earlier draft of the script had** — verify it actually fails (not passes) before declaring AC #6's "exactly 4 rows" assertion proven.
    - After each scenario: revert the temp edit (`git checkout -- <file>`), re-run `bash scripts/check-doc-versions.sh`, confirm it returns to PASSED. Capture the failure-line and exit-code in the transcript per scenario.

## Tasks / Subtasks

- [x] Task 1: Verify the pre-patch state (AC: #1–#10)
    - [x] 1.1 `git status --short`: clean except expected untracked (`Hexalith.Tenants`, `.claude/mcp.json`, `_tmp_diff.patch`, story file) and modified `sprint-status.yaml` (story-creation backlog → ready-for-dev write).
    - [x] 1.2 `Directory.Packages.props` lines 6–9 confirmed: all four DAPR packages (`Dapr.Client`, `Dapr.AspNetCore`, `Dapr.Actors`, `Dapr.Actors.AspNetCore`) pinned at `1.17.7`.
    - [x] 1.3 `Grep "1\.17\.7"` across `CLAUDE.md` + `docs/**/*.md` → **9 matches** (CLAUDE.md:195, choose-the-right-tool.md:193, dapr-faq.md:43+47, deployment-kubernetes.md:138, nuget-packages.md:158+182+183+184). `Grep "1\.16\.1"` → **0 matches** (R2-A5 invariant intact).
    - [x] 1.4 Out-of-scope refs survive: `DAPR 1\.16` in `dapr-component-reference.md` → 3 matches; `runtime-version 1\.14` in `deployment-kubernetes.md` → 1 match.
    - [x] 1.5 Tier 1 baseline structural-guarantee: this story diff is markdown + bash + YAML + plain-text only (no compiled file); Tier 1 = 788/788 by structural argument per R2-A5 precedent.
    - [x] 1.6 `scripts/check-doc-versions.sh` confirmed absent pre-patch (`ls` → does-not-exist).

- [x] Task 2: Rewrite prose in 4 doc files (AC: #1–#4)
    - [x] 2.1 **`CLAUDE.md` line 195** (AC #1): use `Edit` with `old_string: "- DAPR SDK 1.17.7 (Client, AspNetCore, Actors)"` → `new_string: "- DAPR SDK (Client, AspNetCore, Actors) — pinned in [\`Directory.Packages.props\`](Directory.Packages.props)"`. Verify with `Grep "DAPR SDK"` in `CLAUDE.md` that exactly 1 line matches, carries no version number, and contains the markdown-link form `[\`Directory.Packages.props\`](Directory.Packages.props)`.
    - [x] 2.2 **`docs/concepts/choose-the-right-tool.md` line 193** (AC #2): use `Edit` with `old_string: "Hexalith depends on a specific DAPR SDK version (currently 1.17.7, as pinned in \`Directory.Packages.props\`, last verified April 2026)."` → `new_string: "Hexalith depends on a specific DAPR SDK version pinned in [\`Directory.Packages.props\`](../../Directory.Packages.props) (the single source of truth)."`. The line continues with "DAPR follows..." — that prose is preserved byte-identical. Verify with `Grep "currently 1\."` in the file → 0 matches; `Grep "last verified"` → 0 matches; `Grep "\[\`Directory\.Packages\.props\`\]"` → 1 match (the new link).
    - [x] 2.3 **`docs/guides/dapr-faq.md` line 43 (TL;DR)** (AC #3): use `Edit` with `old_string: "Hexalith pins to a specific SDK version (currently 1.17.7) and CI verifies on every commit."` → `new_string: "Hexalith pins to a specific SDK version in [\`Directory.Packages.props\`](../../Directory.Packages.props) and CI verifies on every commit."`.
    - [x] 2.4 **`docs/guides/dapr-faq.md` line 47 (body)** (AC #3): use `Edit` with `old_string: "Hexalith pins the DAPR SDK version in \`Directory.Packages.props\` (currently **1.17.7** — last verified April 2026). The CI pipeline tests against this pinned version on every commit."` → `new_string: "Hexalith pins the DAPR SDK version in [\`Directory.Packages.props\`](../../Directory.Packages.props) — that file is the single source of truth. The CI pipeline tests against the pinned version on every commit."`. **Pre-Edit anchor uniqueness check (mandatory):** before invoking `Edit`, run `Grep "(currently \*\*1\.17\.7\*\* — last verified April 2026)"` in `dapr-faq.md` and confirm it returns exactly 1 match. If 0, the anchor has drifted (re-locate and update this task). If > 1, extend `old_string` with surrounding context until uniqueness is restored.
    - [x] 2.5 **`docs/guides/deployment-kubernetes.md` line 138** (AC #4): use `Edit` with `old_string: "> **Note:** The project uses DAPR SDK version **1.17.7** (see \`Directory.Packages.props\`). Use a compatible DAPR runtime version. Consult the [DAPR SDK-to-runtime compatibility matrix](https://docs.dapr.io/operations/support/support-release-policy/) for version mapping."` → `new_string: "> **Note:** The project pins the DAPR SDK in [\`Directory.Packages.props\`](../../Directory.Packages.props) (the single source of truth). Use a compatible DAPR runtime version. Consult the [DAPR SDK-to-runtime compatibility matrix](https://docs.dapr.io/operations/support/support-release-policy/) for version mapping."`. **Do NOT touch line 143** (`runtime-version 1.14.4`).
    - [x] 2.6 Verified: `Grep "1\.17\.7"` across `CLAUDE.md` + `docs/**/*.md` → exactly **4 matches** remain (all four `Dapr.*` table cells in `nuget-packages.md` at lines 158/182/183/184). Markdown-link form verified across the 4 edited files: CLAUDE.md ×1, choose-the-right-tool.md ×1, dapr-faq.md ×2, deployment-kubernetes.md ×1 = **5 matches** (pre-existing `[\`Directory.Packages.props\`]` matches at upgrade-path.md:131/137 are out-of-scope and unchanged).

- [x] Task 3: Create the version-pin-consistency script (AC: #6)
    - [x] 3.1 Created `scripts/check-doc-versions.sh` (75 lines, ≤ 80 budget) with the AC #6 contract. Hardening note: anchored the per-package regex to `^[[:space:]]*<PackageVersion ...` so a commented-out (`<!-- ... -->`) line does NOT match — Scenario (c) negative test caught the un-anchored bug pre-merge.

        ```bash
        #!/usr/bin/env bash
        # DAPR SDK version pin consistency check — asserts that the four Dapr.*
        # table cells in docs/reference/nuget-packages.md match the version
        # pinned in Directory.Packages.props (the single source of truth).
        #
        # Assumptions (rewrite to xmllint or `dotnet msbuild -getProperty:` if violated):
        #   1. Directory.Packages.props uses single-line <PackageVersion ... /> entries
        #      (no multi-line elements, no Condition attributes, no metadata children).
        #   2. Each Dapr.* package appears EXACTLY once in the props file.
        #   3. The nuget-packages.md DAPR table rows use the fixed-width pattern
        #      `| Dapr.X    | Y.Y.Y  |` with single-space cell padding on both sides.
        #   4. Bash 4+ (associative arrays). CI is ubuntu-latest (bash 5+); local-dev
        #      on Windows Git Bash 3.2 is unsupported — use WSL or refactor to scalars.
        set -euo pipefail

        PROPS="Directory.Packages.props"
        DOC="docs/reference/nuget-packages.md"
        EXPECTED_DAPR_ROWS=4   # Dapr.Client ×2 (Client + Server tables) + Dapr.Actors + Dapr.Actors.AspNetCore

        # Pre-flight: detect multi-line <PackageVersion> elements (regex can't handle them).
        # Match lines starting <PackageVersion that don't end with /> on the same line.
        multiline=$(grep -cE "<PackageVersion[^/]*$" "$PROPS" || true)
        if [[ "$multiline" -gt 0 ]]; then
          echo "ERROR: multi-line <PackageVersion> element detected in $PROPS — REWRITE NEEDED." >&2
          echo "       See script header comment for migration to xmllint or 'dotnet msbuild -getProperty:'." >&2
          exit 1
        fi

        # Extract the four Dapr.* versions from the props file.
        declare -A PINS
        for pkg in Dapr.Client Dapr.AspNetCore Dapr.Actors Dapr.Actors.AspNetCore; do
          matches=$(grep -cE "<PackageVersion Include=\"$pkg\" Version=\"[^\"]+\"" "$PROPS" || true)
          if [[ "$matches" -eq 0 ]]; then
            echo "ERROR: $pkg not found in $PROPS" >&2; exit 1
          fi
          if [[ "$matches" -gt 1 ]]; then
            echo "ERROR: $pkg appears $matches times in $PROPS (expected exactly 1)" >&2; exit 1
          fi
          PINS[$pkg]=$(grep -oE "<PackageVersion Include=\"$pkg\" Version=\"[^\"]+\"" "$PROPS" \
                      | sed -E "s/.*Version=\"([^\"]+)\".*/\\1/")
        done

        # Internal consistency: all four Dapr.* must share the same version.
        first="${PINS[Dapr.Client]}"
        for pkg in "${!PINS[@]}"; do
          if [[ "${PINS[$pkg]}" != "$first" ]]; then
            echo "ERROR: $PROPS has divergent Dapr.* pins:" >&2
            for p in "${!PINS[@]}"; do echo "  $p = ${PINS[$p]}" >&2; done
            exit 1
          fi
        done
        EXPECTED="$first"

        # Walk doc cells and assert match. Count rows to detect silent-pass on empty doc.
        fail=0
        rows_seen=0
        while IFS= read -r line; do
          rows_seen=$((rows_seen + 1))
          ln="${line%%:*}"
          row="${line#*:}"
          pkg=$(echo "$row" | sed -E "s/^\| ([A-Za-z.]+) +\|.*$/\\1/")
          ver=$(echo "$row" | sed -E "s/^\| [A-Za-z.]+ +\| ([0-9.]+) +\|.*$/\\1/")
          if [[ "$ver" != "$EXPECTED" ]]; then
            echo "MISMATCH at $DOC:$ln: '$pkg' cell shows '$ver', $PROPS pins '$EXPECTED'" >&2
            fail=1
          fi
        done < <(grep -nE "^\| Dapr\.(Client|Actors|AspNetCore|Actors\.AspNetCore) +\| [0-9.]+ +\|" "$DOC")

        if [[ "$rows_seen" -ne "$EXPECTED_DAPR_ROWS" ]]; then
          echo "ERROR: expected exactly $EXPECTED_DAPR_ROWS Dapr.* table rows in $DOC, found $rows_seen" >&2
          exit 1
        fi
        if [[ "$fail" -ne 0 ]]; then exit 1; fi
        echo "PASSED: DAPR SDK version pin consistency ($EXPECTED, $rows_seen rows verified)"
        ```

        The above is illustrative; the dev is welcome to refine for clarity, robustness, or POSIX-portability — but the externally-observable contract (read props, parse 4 packages, assert against doc rows, exit 0 on match / non-zero on mismatch with line-numbered error) is fixed.
    - [x] 3.2 Executable bit added via `git update-index --add --chmod=+x scripts/check-doc-versions.sh`.
    - [x] 3.3 Script runs PASSED on canonical state: `bash scripts/check-doc-versions.sh` → `PASSED: DAPR SDK version pin consistency (1.17.7, 4 rows verified)` (exit 0).
    - [x] 3.4 **Negative tests** — proof written to `_bmad-output/test-artifacts/r2a5b-negative-test-proof.txt` (69 lines). All 4 mandatory scenarios verified: (a) cell-mismatch → MISMATCH at nuget-packages.md:158 'Dapr.Client' cell shows '1.17.6' (exit 1); (b) divergent props pins → ERROR with per-package listing (exit 1); (c) missing package in props → ERROR: Dapr.AspNetCore not found (exit 1); (d) empty doc rows → ERROR: expected exactly 4 Dapr.* table rows, found 0 (exit 1). Optional scenario (e) multi-line guard also verified → ERROR: multi-line <PackageVersion> element detected (exit 1). Each scenario reverts cleanly; post-revert PASSED confirmed for all 5. Run all four scenarios in sequence; each temp-edit MUST be reverted before the next. Capture per-scenario transcripts (failure line + exit code, then post-revert PASSED) in the proof file (NOT just Completion Notes — the proof file ships in the PR diff so review can verify each error mode actually fired). Suggested proof-file shape: a header line per scenario (`=== Scenario (a): cell-mismatch ===`), the temp-edit command/diff summary, the script invocation, the captured `stderr`/`stdout`, and the captured exit code; then a `=== After revert ===` line and the PASSED echo. Run the four scenarios:
        - **(a) Cell-mismatch:** temp-edit `docs/reference/nuget-packages.md` to change a `Dapr.Client | 1.17.7` cell to `Dapr.Client | 1.17.6`; run script; confirm exit-code != 0 and `MISMATCH ...` message; `git checkout -- docs/reference/nuget-packages.md`; re-run; confirm PASSED.
        - **(b) Divergent props pins:** temp-edit `Directory.Packages.props` to change `Dapr.Actors`'s version to a different value (e.g., `1.17.6`); run script; confirm exit-code != 0 and `ERROR: Directory.Packages.props has divergent Dapr.* pins:` listing; `git checkout -- Directory.Packages.props`; re-run; confirm PASSED.
        - **(c) Missing package in props:** temp-edit `Directory.Packages.props` to comment out the `<PackageVersion Include="Dapr.AspNetCore" ... />` line; run script; confirm exit-code != 0 and `ERROR: Dapr.AspNetCore not found in Directory.Packages.props`; `git checkout -- Directory.Packages.props`; re-run; confirm PASSED.
        - **(d) Empty / missing doc rows:** temp-edit `docs/reference/nuget-packages.md` to comment out all four `Dapr.*` rows (HTML-comment them, or delete and stash); run script; confirm exit-code != 0 and `ERROR: expected exactly 4 Dapr.* table rows ... found 0`; `git checkout -- docs/reference/nuget-packages.md`; re-run; confirm PASSED.
        - **(e) — bonus, optional but recommended:** temp-edit `Directory.Packages.props` to break a `<PackageVersion>` line into multi-line form (e.g., add a `Condition="..."` attribute on a continuation line); run script; confirm exit-code != 0 and `ERROR: multi-line <PackageVersion> element detected ... REWRITE NEEDED` message; revert; confirm PASSED. (Exercises the pre-flight guard added per AC #6.)
        After all scenarios, save the proof file at `_bmad-output/test-artifacts/r2a5b-negative-test-proof.txt`. **All four mandatory scenarios (a)/(b)/(c)/(d) MUST appear in the file; scenario (e) is optional. Skipping any mandatory scenario means that error mode is unverified — review will reject.**

- [x] Task 4: Wire the script into CI and local-dev validation (AC: #7, #8)
    - [x] 4.1 Edit `.github/workflows/docs-validation.yml`. Insert a new step into the `lint-and-links` job between the existing "Lint Markdown" step (line 32–33) and "Restore lychee cache" step (line 35–40). Use `Edit` with the old anchor including the surrounding "Lint Markdown" + "Restore lychee cache" lines to make it unique. The new step is:

        ```yaml
              - name: Verify DAPR SDK version pin consistency
                run: bash scripts/check-doc-versions.sh
        ```

    - [x] 4.2 Edit `scripts/validate-docs.sh`. Bump the stage counter in the three header echos (`Stage 1/3` → `Stage 1/4`, `Stage 2/3` → `Stage 2/4`, `Stage 3/3` → `Stage 3/4`). Add a new Stage 4 between Stage 3 and the final `=== All validations passed ===` echo. Invoke via `bash scripts/check-doc-versions.sh` (matches AC #8 verbatim and keeps the local path byte-identical to the CI path; do NOT use `./scripts/check-doc-versions.sh` here even if the executable bit is set, so the local-vs-CI invocation can never drift):

        ```bash
        # --- Stage 4: DAPR SDK Version Pin Consistency ---
        echo ""
        echo "=== Stage 4/4: DAPR SDK Version Pin Consistency ==="
        CURRENT_STAGE="DAPR SDK version pin consistency"
        bash scripts/check-doc-versions.sh
        echo "PASSED: DAPR SDK version pin consistency"
        ```

    - [x] 4.3 Stage 4 verified in isolation: `bash scripts/check-doc-versions.sh` → `PASSED: DAPR SDK version pin consistency (1.17.7, 4 rows verified)` (exit 0). Full `bash scripts/validate-docs.sh` end-to-end run skipped per Task 4.3's "or at least Stage 4 in isolation" allowance — Stages 1–3 (markdownlint, lychee, dotnet sample build/test) are not impacted by this story's diff (no compiled file, no link-target add/remove, no markdown-syntax change beyond ordinary prose) and CI exercises them on the PR per AC #13.

- [x] Task 5: Final grep audit and diff-scope verification (AC: #9, #10)
    - [x] 5.1 AC #9 grep audit results (all match expectations):
        - `Grep "1\.17\.7"` in `CLAUDE.md`+`docs/**/*.md` → **4 matches** (all `nuget-packages.md` table cells at lines 158/182/183/184) ✓
        - `Grep "1\.17\.x"` in `docs/**/*.md` → **1 match** (`upgrade-path.md:141`) ✓
        - `Grep "1\.16\.1"` in `CLAUDE.md`+`docs/**/*.md` → **0 matches** (R2-A5 invariant intact) ✓
        - `Grep "last verified March 2026"` in `docs/**/*.md` → **0 matches** ✓
        - `Grep "last verified April 2026"` in `docs/**/*.md` → **0 matches** (full date-stamp removal) ✓
        - `Grep "currently 1\.\d+\.\d+"` in `docs/**/*.md` → **0 matches** (full `currently <X.Y.Z>` removal) ✓
        - `Grep "DAPR 1\.16"` in `dapr-component-reference.md` → **3 matches** (preserved out-of-scope) ✓
        - `Grep "1\.16\.x or later"` in `CONTRIBUTING.md` → **1 match** (preserved out-of-scope) ✓
        - `Grep "runtime-version 1\.14"` in `deployment-kubernetes.md` → **1 match** (preserved out-of-scope, line 143) ✓
    - [x] 5.2 `git status --short` confirms diff scope per AC #10 (8 tracked + 2 untracked-to-stage):
        - `CLAUDE.md`
        - `docs/concepts/choose-the-right-tool.md`
        - `docs/guides/dapr-faq.md`
        - `docs/guides/deployment-kubernetes.md`
        - `scripts/check-doc-versions.sh` (new file)
        - `scripts/validate-docs.sh`
        - `.github/workflows/docs-validation.yml`
        - `_bmad-output/test-artifacts/r2a5b-negative-test-proof.txt` (new file, per AC #13)
        - `_bmad-output/implementation-artifacts/sprint-status.yaml` (Task 6 lifecycle update)
        - `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` (single-line addendum to R2-A5 row per AC #11(b))
        - `_bmad-output/implementation-artifacts/post-epic-2-r2a5b-version-prose-source-of-truth-refactor.md` (this story housekeeping)
    - [x] 5.3 Diff exclusions confirmed: `Directory.Packages.props`, `Directory.Build.*`, all `*.csproj`, all `src/**/*.cs`, all `tests/**/*.cs`, `docs/reference/nuget-packages.md`, `docs/guides/upgrade-path.md`, `CONTRIBUTING.md`, `AGENTS.md`, `deploy/README.md`, `docs/getting-started/prerequisites.md`, `package.json`, `nuget.config`, `global.json`, `ci.yml`, `release.yml` — none in diff.
    - [x] 5.4 Proof file `_bmad-output/test-artifacts/r2a5b-negative-test-proof.txt` exists (69 lines) and is untracked (will be staged). All 4 mandatory scenarios + 1 optional (e) captured per Task 3.4.

- [x] Task 6: Sprint-status lifecycle move and story housekeeping (AC: #11)
    - [x] 6.1 sprint-status.yaml entry updated: `ready-for-dev` → `in-progress` (dev start, mid-session) → `review` (end-of-session). Closure-style annotation written per R2-A5 precedent enumerating the 4 prose rewrites, new script, CI step, validate-docs.sh Stage 4, negative-test proof, AC #11(b) retro addendum, final grep audit, diff scope, and chore: commit-prefix expectation.
    - [x] 6.2 `last_updated:` line bumped at both line 2 (comment) and line 45 (YAML key) with the `→ review` summary.
    - [x] 6.3 This story file: Status `ready-for-dev` → `in-progress` → `review`; Tasks 1–6 + all subtasks marked `[x]`. Tasks 7 (PR/merge) + 8 (CI green on PR) remain unchecked — they are post-Status=review activities (PR open, CI watch, merge, post-merge runbook substitutes the SHA in the AC #11(b) retro-row addendum). Dev Agent Record + File List + Change Log populated below. AC #11(b) retro addendum applied to `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 6 row R2-A5: appended ` → R2-A5b structural cure ✅ Closed \`<r2a5b-merge-sha>\` |` after the existing closure annotation; SHA placeholder to be substituted by Post-Merge Runbook step 5.

- [ ] Task 7: Conventional-commit-formatted PR / merge commit (AC: #12, #13)
    - [ ] 7.1 PR title format: `chore: refactor DAPR SDK version prose to source-of-truth pattern + CI guard rail`. PR body bullets: closes the structural-cure follow-up to R2-A5 (which was Epic 2 retro action item R2-A5); names the originating spec (R2-A5 Future-Work Spin-Offs item #5 in `post-epic-2-r2a5-dapr-sdk-version-reconcile.md`); lists the 4 doc files prose-rewritten + the new script + the 2 CI-tooling edits; and explicitly notes that `Directory.Packages.props` is the source of truth and was NOT touched. Branch name: `docs/post-epic-2-r2a5b-version-prose-source-of-truth-refactor` per `CLAUDE.md` § Branch Naming (the dominant intent is documentation refactor; `chore/` is also acceptable).
    - [ ] 7.2 Verify pre-commit hooks pass — do NOT use `--no-verify`. Per `CLAUDE.md` § Git Safety Protocol, hook bypass is prohibited unless explicitly requested.
    - [ ] 7.3 On squash-merge: confirm the squashed commit subject preserves the `chore:` prefix. Verify post-merge that semantic-release does NOT bump any of the 6 NuGet packages (no new release PR, no new GitHub Release, `CHANGELOG.md` untouched). If a release fires anyway, AC #12's emergency-revert path applies.
    - [ ] 7.4 **Pre-squash-merge re-grep audit** (mirrors R2-A5 Task 12.4 sibling-merge guard): immediately before clicking squash-merge, re-run AC #9's greps. If a sibling PR has touched any of the 4 doc files in flight and the regex counts have shifted, redo the merge resolution before squash; do NOT squash on a degraded grep state.

- [ ] Task 8: Confirm `docs-validation.yml` CI green on PR (AC: #13)
    - [ ] 8.1 Push the branch (Task 7.1) and watch the GitHub Actions run via `gh pr checks <pr-number>` or the PR page. Both jobs (`lint-and-links` and `sample-build`) MUST be green. The new "Verify DAPR SDK version pin consistency" step is the first signal that the script integration works in CI; if it goes red, debug the script regex or invocation before declaring victory.
    - [ ] 8.2 If `markdownlint` reports a violation, inspect whether it is one this PR introduced (greppable in `git diff origin/main...HEAD`) or pre-existing. Introduced → fix in this PR. Pre-existing → file as out-of-scope follow-up; flag in PR body. Do NOT fix pre-existing reds in this PR (R2-A5 lived precedent).
    - [ ] 8.3 If `lychee` reports a broken link, run `bash scripts/validate-docs.sh` locally on a clean `main` checkout to determine pre-existing vs. introduced. None of the prose rewrites add or remove link targets, so introduced-by-this-PR `lychee` reds are structurally unlikely.

## Dev Notes

### Scope Summary

This is a **mixed-scope structural-fix** story:

- **4 markdown doc files** prose-rewritten to remove version-number restatement (5 line-edits across CLAUDE.md, choose-the-right-tool.md, dapr-faq.md ×2, deployment-kubernetes.md). All 5 prose mentions become clickable markdown links to `Directory.Packages.props`.
- **1 new bash script** at `scripts/check-doc-versions.sh` (≤ 80 lines) that asserts the four `Dapr.*` table cells in `nuget-packages.md` match the props pin.
- **2 CI/tooling edits**: a new step in `.github/workflows/docs-validation.yml` and a new Stage 4 in `scripts/validate-docs.sh`.
- **1 new test-artifact file** at `_bmad-output/test-artifacts/r2a5b-negative-test-proof.txt` (per AC #13 — the dev's 4-scenario negative-test transcripts, checked into the diff so review can verify each error mode actually fired).
- **3 process-tracking files** (sprint-status.yaml lifecycle, single-line addendum to R2-A5's row in `epic-2-retro-2026-04-26.md` per AC #11(b), this story file housekeeping).

The diff is **markdown + bash + YAML + plain-text only** — no C#, no `.csproj`, no `Directory.Packages.props`, no test code. Tier 1 / Tier 2 / Tier 3 baselines are guaranteed unchanged by structural argument (no compiled file in diff).

This story does NOT:

- Modify any C# code, project file, or NuGet pin.
- Touch the four `Dapr.*` table cells in `nuget-packages.md` directly (the script asserts, doesn't generate — see ADR below).
- Touch `docs/guides/upgrade-path.md:141` (compatibility floor, deliberately preserved).
- Re-introduce any `1.16.1` reference (R2-A5's grep audit MUST remain green).
- Modify any retro row OTHER than the single-line addendum to R2-A5's row in `epic-2-retro-2026-04-26.md` (per AC #11(b) — the addendum chains the closure pointer forward to R2-A5b's merge SHA so the retro doesn't become a stale forward-reference).
- Resolve any other carry-over Epic 2 retro item.

### Why This Story Exists

R2-A5 (`post-epic-2-r2a5-dapr-sdk-version-reconcile`, closed at SHA `e0cbc78` on 2026-04-28) reconciled 9 stale `1.16.1` references to `1.17.7` after the props pin had been bumped on 2026-04-05 in commit `f7e1302`. The reconcile took 10 line-edits across 6 markdown files and was classified "trivial risk" — but the *underlying* risk class was not addressed: the prose pattern that produced the drift (5 doc files restating `1.16.1` / later `1.17.7` in human-readable text) was preserved.

The R2-A5 ADR at line 259 ("Symptom fix vs. structural cure") explicitly named this story as the structural complement and logged it in `sprint-status.yaml:279` at R2-A5 creation time. Per that ADR's First Principles analysis: "stripped to first principles, this story exists because 5 doc files restate the SDK version in prose. The minimum sufficient information for those 5 files is `(see Directory.Packages.props for the pinned version)`; everything else is decorative, and decorative information drifts."

R2-A5b makes that minimum-sufficient-information rule load-bearing — but the cure is **honestly asymmetric** across the two restatement sites:

- **For prose mentions (5 sites):** rewrite to point at the props file via clickable markdown link. After this change, a future SDK bump (1.17.7 → 1.18.0) propagates to readers with **zero doc edits**. Drift opportunity for prose sites: 5 → 0. **The drift class is structurally eliminated for prose.**
- **For table-cell exceptions in `nuget-packages.md` (4 cells):** the four `Dapr.*` rows are reference-card data where the version *is* the data. Eliminating the version from the cell would defeat the table's purpose. The cure here is *not* elimination — it's **conversion**: drift goes from "silent six-month festering lag picked up at the next retro" to "hard CI failure on the props-bump PR." Manual-edit count per bump for tables: still 4 (unchanged). Silent-drift opportunity for tables: 4 → 0. **The drift class is converted, not eliminated.**

Net story-wide impact: per-bump manual-edit count drops 9 → 4 (a 56% reduction); silent-drift opportunities drop 9 → 0 (full elimination at the *silent* axis, since CI-blocking is loud-by-construction).

Without this story, the next minor SDK bump (1.18.x, plausibly within 6 months given DAPR's release cadence) would re-fire the same drift class on the same 5 prose sites and the same 4 table cells. R2-A5 would be a 10-line patch repeated indefinitely. R2-A5b ends the cycle for prose and converts the cycle to a CI gate for tables.

Per `CLAUDE.md` § Code Review Process: senior review across Epic 2 produced HIGH/MEDIUM patches on 5/5 stories. R2-A5b's risk is structurally trivial (no code, no tests, ≤ 80-line script with a single read-only purpose), but the originating ADR's "structural cure" classification should not be read as "no review needed." The likely review-found patches will be cosmetic: which exact prose to use as the pointer ("the single source of truth" vs. "the authoritative pin" vs. "see `Directory.Packages.props`"); whether to extend AC #1's prose-pointer pattern to the other 4 § Key Dependencies bullets in CLAUDE.md; whether the script should also validate the `Aspire.Hosting`, `MediatR`, `OpenTelemetry.*`, `Microsoft.Extensions.*` pins; whether `chore:` or `docs:` is the right merge prefix. Budget one round of patch turnaround per the Epic 2 review-driven-patch precedent.

### File Locations (verified at HEAD `e0cbc78`, post-R2-A5 closure, story-creation date 2026-04-28)

| File | Pre-patch line(s) | Post-patch state | AC |
|------|-------------------|------------------|----|
| `CLAUDE.md` | line 195: `- DAPR SDK 1.17.7 (Client, AspNetCore, Actors)` | `- DAPR SDK (Client, AspNetCore, Actors) — pinned in \`Directory.Packages.props\`` | #1 |
| `docs/concepts/choose-the-right-tool.md` | line 193: `... (currently 1.17.7, as pinned in \`Directory.Packages.props\`, last verified April 2026). DAPR follows...` | `Hexalith depends on a specific DAPR SDK version pinned in \`Directory.Packages.props\` (the single source of truth). DAPR follows...` | #2 |
| `docs/guides/dapr-faq.md` | line 43 (TL;DR): `Hexalith pins to a specific SDK version (currently 1.17.7) and CI verifies on every commit.` | `Hexalith pins to a specific SDK version in \`Directory.Packages.props\` and CI verifies on every commit.` | #3 |
| `docs/guides/dapr-faq.md` | line 47 (body): `Hexalith pins the DAPR SDK version in \`Directory.Packages.props\` (currently **1.17.7** — last verified April 2026). The CI pipeline tests against this pinned version on every commit.` | `Hexalith pins the DAPR SDK version in \`Directory.Packages.props\` — that file is the single source of truth. The CI pipeline tests against the pinned version on every commit.` | #3 |
| `docs/guides/deployment-kubernetes.md` | line 138: `> **Note:** The project uses DAPR SDK version **1.17.7** (see \`Directory.Packages.props\`). Use a compatible DAPR runtime version. Consult the [DAPR SDK-to-runtime compatibility matrix](...).` | `> **Note:** The project pins the DAPR SDK in \`Directory.Packages.props\` (the single source of truth). Use a compatible DAPR runtime version. Consult the [DAPR SDK-to-runtime compatibility matrix](...).` | #4 |
| `docs/reference/nuget-packages.md` | lines 158/182/183/184 (`Dapr.*` table cells at `1.17.7`) | **unchanged** (cells preserved verbatim; no marker comments) | #5 |
| `scripts/check-doc-versions.sh` | does not exist | new file, ≤ 80 lines, asserts `Dapr.*` cells == props pin | #6 |
| `.github/workflows/docs-validation.yml` | `lint-and-links` job has Lint Markdown → Restore lychee cache → Check links | `lint-and-links` gains "Verify DAPR SDK version pin consistency" step between Lint Markdown and Restore lychee cache | #7 |
| `scripts/validate-docs.sh` | 3 stages (markdownlint, lychee, sample build/test); stage counters say `Stage 1/3`, `Stage 2/3`, `Stage 3/3` | 4 stages; counters bumped to `/4`; new Stage 4 invokes `bash scripts/check-doc-versions.sh` | #8 |
| `_bmad-output/implementation-artifacts/sprint-status.yaml` | line 279: `post-epic-2-r2a5b-...: backlog` (with detailed comment) → SM flips to `ready-for-dev` at this story's creation | Lifecycle through `done` with closure annotation | #11 |
| `_bmad-output/implementation-artifacts/post-epic-2-r2a5b-version-prose-source-of-truth-refactor.md` | this file (current Status: ready-for-dev) | Status moves through `in-progress` → `review` → `done`; Tasks 1–8 marked; Dev Agent Record + File List + Change Log filled in | (story housekeeping) |

If line numbers have shifted at the dev's start (sibling edits to `CLAUDE.md` or any of the 4 doc files), update this table before proceeding so reviewers can audit the changes against the as-edited tree (same discipline R2-A5 used).

**Pre-patch grep summary (run at story creation, HEAD `e0cbc78`, 2026-04-28):**

```
$ Grep "1\.17\.7" CLAUDE.md docs/**/*.md → 9 line matches across 5 files (CLAUDE.md ×1, choose-the-right-tool.md ×1, dapr-faq.md ×2, deployment-kubernetes.md ×1, nuget-packages.md ×4)
$ Grep "currently 1\.17\.7" docs/**/*.md → 2 matches (choose-the-right-tool.md:193, dapr-faq.md:43)
$ Grep "last verified April 2026" docs/**/*.md → 2 matches (choose-the-right-tool.md:193, dapr-faq.md:47)
$ Grep "1\.16\.1" CLAUDE.md docs/**/*.md → 0 matches (R2-A5 closure invariant; MUST remain 0 post-patch)
$ Grep "1\.17\.x" docs/**/*.md → 1 match (upgrade-path.md:141)
```

Post-patch (AC #9):

```
$ Grep "1\.17\.7" CLAUDE.md docs/**/*.md → 4 matches (all in nuget-packages.md, table cells)
$ Grep "currently 1\.\d+\.\d+" docs/**/*.md → 0 matches
$ Grep "last verified" docs/**/*.md → 0 matches
$ Grep "1\.16\.1" CLAUDE.md docs/**/*.md → 0 matches (preserved)
$ Grep "1\.17\.x" docs/**/*.md → 1 match (upgrade-path.md:141, preserved)
```

### Architecture Decisions

- **Why prose-pointer (a) and assertion-check (b), and not auto-generation for the table?** The R2-A5 Future-Work item #5 originally framed (b) as "auto-generate the 4 Dapr.* rows from `Directory.Packages.props` at docs-build time." Three reasons to prefer assertion-check over auto-generation:
    - **No docs-build pipeline.** This project commits docs as source; there is no markdown-templating or docs-build step. Adding one for 4 table cells is disproportionate. (`docs-api-reference.yml` builds *.cs XML docs to markdown via DefaultDocumentation, but that's a release-tag-only PR-creation pipeline, not a per-PR docs build.)
    - **Mutating-CI complexity.** An auto-generation pipeline would either (i) commit generated cells into a PR (asynchronous PR loop, like `docs-api-reference.yml`) or (ii) fail the PR if the generated diff is non-zero. (i) adds release-only delay between props bump and doc update; (ii) is just a pessimistic version of the assertion-check approach.
    - **Assertion-check is simpler and equally effective.** A ≤ 80-line read-only script + a CI step is the minimum viable structural cure. Drift becomes a hard CI failure on the props-bump PR, forcing the developer to update both files in the same PR. This matches the project's existing "static docs + CI gates" philosophy (markdownlint, lychee).

  **Reviewer-pushback option:** if a reviewer prefers auto-generation (e.g., a marker-driven `<!-- DAPR-PACKAGES-START -->...<!-- DAPR-PACKAGES-END -->` block + a script that overwrites the block from the props pin), accept it as a scope expansion. Both shapes kill the drift class; the assertion-check is just the smaller diff. **Anti-pattern:** marker comments *inside* a markdown table row break GitHub's table rendering — a marker-driven approach must place markers outside the table boundary or use a code-fence-with-include-pragma pattern (e.g., a build step that replaces a `<!-- include:dapr-table -->` line with the generated table). Don't paint into that corner unless the dev specifically wants the more complex shape.

- **Why prose-pointer for the 4 prose sites (and not "just remove the version, keep the date stamp")?** The date stamp (`last verified April 2026`) was the second-half cure: it signals when the prose was cross-checked against the props. After (a), there is no claim to cross-check (the prose names no version), so the stamp is meaningless. Keeping it would confuse readers ("verified what?"). Drop it.

- **Why preserve `docs/guides/upgrade-path.md:141` (`1.17.x+`)?** The compatibility-matrix row is forward-looking: "this Hexalith major version requires DAPR SDK 1.17.x or later." That is a meaningful, deliberately-claimed compatibility floor — distinct from the literal pin in `Directory.Packages.props`. Bumping the floor when the pin bumps requires a deliberate decision (does Hexalith still support 1.17.x consumers, or is the floor now 1.18.x+?), and an automated tool can't make that judgment. Leave it as prose.

- **Why `chore:` and not `docs:` as the merge prefix?** The diff is mixed: 4 doc-prose rewrites + 1 new tooling script + 2 CI-tooling edits. `docs:` understates the script and CI changes; `chore:` correctly communicates "build/CI/tooling change with doc-prose component." Both prefixes are no-version-bump under semantic-release, so the impact on the release pipeline is identical. The choice is editorial: which prefix gives a future `git log` reader the truer summary of the diff? `chore:` does. **Reviewer-pushback option:** if a reviewer argues `docs:` since the dominant line-count is doc-prose, accept; both work.

- **Why no `BREAKING CHANGE:` token?** No published-API surface changes. No consumer compilation breaks. The diff is documentation + supporting tooling.

- **Why not extend the prose-pointer pattern to the other 4 § Key Dependencies bullets in CLAUDE.md (`.NET Aspire`, `MediatR`, `FluentValidation`, `OpenTelemetry`)?** Scope discipline. R2-A5 was specifically about DAPR SDK drift; R2-A5b's scope is the structural cure for that same drift class on the same target. Extending to 4 more dependencies is a 4× scope increase justifiable on its own merits ("eliminate version-prose-drift class entirely") but not justifiable as a scope-creep on R2-A5b. **Reviewer-pushback option:** if a reviewer argues for symmetric treatment, accept and extend AC #1; the script would also need to extend its assertion list to the other 4 packages, which is a 5–10-line addition. Default: DAPR-only, with Future-Work item #6 spawning 4 sibling stories for the broader sweep.

### Pattern Established by This Story

R2-A5b is *not* a one-off DAPR fix. It establishes a **project-wide pattern for version-pin restatement** that future stories apply. Future stories apply this pattern to other dependencies (Future-Work item #6 enumerates four direct extensions); they should reference this section as the authority instead of re-deriving the rationale.

The pattern is a two-template pair plus a composition rule:

- **Template A — Prose-Pointer.** When a value (version, license, contact, URL, etc.) is *restated in prose* and the source of truth is a checked-in file, replace the prose with a clickable markdown link to that file. **No version number; no verification date stamp.** The location of truth is the constant; the value is treated as the variable. Future bumps to the source-of-truth file propagate to readers with zero doc edits.
- **Template B — Assertion-Check.** When a restatement is *unavoidable* (reference cards, comparison tables, version matrices — places where the value IS the data the reader is looking for), add a CI script that asserts the source-of-truth file's value matches each restatement. Drift becomes a hard CI failure on the bump PR instead of a silent doc-lag picked up at the next retro. The restatement is preserved; the silence is killed.
- **Composition rule.** *Either eliminate the restatement (Template A) or assert it (Template B).* Never leave a restated value with neither pointer-form nor CI assertion — that is the version-prose-drift class.

R2-A5b applies both templates to DAPR SDK version mentions: A to the 5 prose sites in 4 doc files; B to the 4 table cells in `nuget-packages.md` (cells preserved, script asserts).

**Failure modes the pattern doesn't address** (carved out for explicit honesty so future appliers don't over-claim):

- **Lock-file resolution drift** (declared-version-pin vs resolved-NuGet-version). Different drift class; needs `dotnet list package --include-transitive` assertion. Out of scope; future-work.
- **Multi-source-of-truth migration** (when the source of truth becomes plural — e.g., `Directory.Packages.props` + `nuget-overrides.props`). The pointer-form points at one file; if there are two, the pointer becomes a half-truth. Re-version the pattern at the migration point.
- **Forward-looking compatibility floors** (`1.17.x+` in `upgrade-path.md:141`). Not a pin restatement; a deliberately-claimed minimum-version contract. Pattern does not apply; leave as prose with explicit ADR per the bump.
- **Social-contract decay** (a future contributor adds a skip-flag to the script). PR-review-catchable; documented in Risk Assessment, not engineered around.

Future stories applying the pattern: cite this section as the authority, then describe only the deltas specific to their dependency.

### Risk Assessment

| Risk | Likelihood | Severity | Mitigation |
|------|------------|----------|-----------|
| The script's regex misparses an edge case in the props file (e.g., a multi-line `<PackageVersion>` element, or a comment-bearing line) | Low (the props file uses the same single-line `<PackageVersion Include="..." Version="..." />` shape for all entries — verified at HEAD `e0cbc78`) | Medium (a false-positive would block PRs on a broken parse) | Task 3.4 negative test exercises the failure path; if the script misfires, the dev sees it in local-dev, not at CI |
| The script's regex misparses a markdown table edge case (e.g., a row with extra whitespace or an unexpected pipe character) | Low (the four target rows use a uniform `\| Dapr\.X +\| Y\.Y\.Y +\|` shape — verified at HEAD `e0cbc78`) | Medium | Same as above; Task 3.4 covers it |
| A future bash version on CI changes regex semantics | Very low (the script uses `grep -oE` and `sed -E`, both POSIX-stable) | Low | The CI runner pins to ubuntu-latest; bash and grep versions there are stable |
| markdownlint flags one of the prose rewrites (e.g., line-length on the rewritten `dapr-faq.md:47`) | Low (rewrites are similar length to originals; no unusual markup) | Low (cosmetic; fix in this PR) | Task 4.3 runs `validate-docs.sh` locally before push |
| A sibling PR rebases through this PR and re-introduces a `1.16.1` or a `currently 1.X.Y` prose mention | Low (no other backlog story touches DAPR SDK version prose) | Medium | Task 7.4 pre-squash re-grep audit catches it |
| A reviewer asks to extend AC #1 to all § Key Dependencies bullets | Medium (this is the most-extensible AC) | Low (one-character changes per bullet + script extension) | ADR captures the reviewer-pushback option; revert path is one Edit |
| Auto-generation reviewer-pushback (asks to flip from assertion-check to mutating CI) | Low (the assertion-check is the minimum viable structural cure; a reviewer arguing for auto-generation is asking for a scope-expansion) | Medium (changes the PR's character significantly) | ADR captures both options; if accepted, defer to a follow-up tooling story rather than fold into this PR |
| Date-of-merge crosses into May 2026 and a reviewer asks for a "last verified May 2026" stamp anywhere | Very low (the entire point of this story is to delete that stamp pattern) | n/a | If asked, point at AC #2/#3/#9 — the stamp is the half of the cure being deliberately removed |
| The new CI step adds time to the `lint-and-links` job's wall-clock | Low (the script is ≤ 80 lines, runs in < 1s on the props file + 1 doc file) | Low | The job's existing budget (10 min timeout) absorbs the addition trivially |
| **Social-contract decay** — a future contributor annoyed by the script adds a `SKIP_VERSION_CHECK=1` flag (or comments out the workflow step, or `|| true`s the script invocation) and silently defangs the cure | Low (would require a PR doing exactly this) | Medium (cure becomes a no-op while still appearing to run) | **Accepted risk.** Same threat model as `[skip ci]` in commit messages — exists, recoverable. PR review catches a bypass-flag addition. Document but do not engineer around — adding a "you can't bypass me" wrapper around a 30-line script is over-engineering. If the bypass pattern emerges in practice, file a follow-up to harden. |

### Constraints That MUST NOT Change

- `Directory.Packages.props` lines 5–10 (the four DAPR `PackageVersion` entries at `1.17.7`). The source of truth is correct as-is.
- `docs/reference/nuget-packages.md` lines 158, 182, 183, 184 (the four `Dapr.*` table cells at `1.17.7`) — the script asserts they match the props pin; do not change them in this story.
- `docs/guides/upgrade-path.md:141` (`1.17.x+` compatibility floor — preserved per AC #10).
- `docs/guides/deployment-kubernetes.md:143` (`runtime-version 1.14.4` — runtime pin, separate concern).
- `docs/guides/dapr-component-reference.md` lines 320, 478, 634 (`SCOPING FIELD REFERENCE (DAPR 1.16)` — runtime feature-availability annotations).
- `CONTRIBUTING.md:50` (`DAPR CLI (1.16.x or later)` — CLI floor, separate concern).
- `docs/getting-started/prerequisites.md` (CLI/runtime stamps — carved to `post-epic-2-r2a5c`).
- `deploy/README.md` (`daprio/daprd:1.16.1` sidecar pins — carved to `post-epic-2-r2a5d`).
- `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` — only the R2-A5 row's closure annotation gains a single-line addendum chaining forward to R2-A5b's merge SHA per AC #11(b); ALL OTHER content of the retro file is preserved byte-identical (no other rows touched, § 10 line 161 untouched, headings untouched, etc.).
- All `_bmad-output/planning-artifacts/*.md` files (originating specs, historical records).
- Any `.csproj`, `.props`, `.targets`, `.cs`, CI workflow other than `docs-validation.yml`, `package.json`, `package-lock.json`, `nuget.config`, `global.json`, `aspire.config.json`, or any other non-`*.md`/non-bash/non-`docs-validation.yml`/non-sprint-status/non-test-artifact file. The diff is markdown + bash + 1 YAML workflow + 1 plain-text proof file only.

### Suggested Future-Work Spin-Offs (out of scope; flag in Completion Notes only)

1. **Extend the prose-pointer + assertion-check pattern to the other 4 § Key Dependencies bullets in `CLAUDE.md`** (`.NET Aspire`, `MediatR`, `FluentValidation`, `OpenTelemetry`). The script would extend its parse list to 4 more package families. Same drift class for the same reason. Default-deferred per AC #1 ADR.
2. **Apply the assertion-check pattern to the other version-pin tables in `nuget-packages.md`** (Hexalith.Commons.UniqueIds, Microsoft.Extensions.*, Aspire.Hosting, CommunityToolkit.Aspire.*, Microsoft.AspNetCore.SignalR.Client, Shouldly, NSubstitute, xunit.assert). The script would extend its parse list to those packages. Marginal value if only DAPR drifts in practice; high value if any of the others ever drifts.
3. **Centralize all version-pin assertions in a single `scripts/check-version-pins.sh`** as an alternative to per-class scripts. Refactor consideration if (1) and (2) ship and `check-doc-versions.sh` becomes a half-implemented version of the broader idea.
4. **Generate the `nuget-packages.md` Dapr.* table from `Directory.Packages.props` at docs-build time** (the auto-generation alternative described in the ADR). Higher complexity than the assertion-check; defer until a docs-build pipeline exists for other reasons.
5. **Add a markdownlint custom rule** that flags any `currently <X.Y.Z>` prose pattern in the doc tree as a violation. Belt-and-suspenders if the pattern re-emerges in a new doc.
6. **Apply the prose-pointer + assertion-check pattern symmetrically to the remaining 4 § Key Dependencies bullets in `CLAUDE.md`** — the natural follow-up to AC #1's narrow-DAPR scoping (and the deferred Paige-side of the symmetry debate logged in Change Log 2026-04-28). Suggested split into 4 sibling stories, each one ADR-decision-bearing per ADR — Why not extend the prose-pointer pattern in this story:
   - `post-epic-2-r2a5b-ext-aspire` — `.NET Aspire 13.1.x` bullet (script must decide: assert all 8 `Aspire.Hosting.*` packages share a pin, or assert just the prose-named one?).
   - `post-epic-2-r2a5b-ext-mediatr` — `MediatR 14.0.0` bullet (single package, simplest of the four).
   - `post-epic-2-r2a5b-ext-fluentvalidation` — `FluentValidation 12.1.1` bullet (script ADR: also assert `FluentValidation.AspNetCore` — same family, different pin lifecycle?).
   - `post-epic-2-r2a5b-ext-otel` — `OpenTelemetry 1.15.x` bullet (script ADR: which of the 7 `OpenTelemetry.*` packages count as "the" OpenTelemetry pin?).
   Each sibling story applies the **two-template pair** established by R2-A5b (see "Pattern Established by This Story" below). Order suggested: mediatr first (smallest), then fluentvalidation, otel, aspire (largest scope). Total expected diff per story: ~30–60 lines (1 prose rewrite + 1 script extension + 1 negative-test scenario added to the proof file). Owner: TBD at next sprint planning; Paige-side coherence concern will close when all 4 sibling stories ship.

### References

- [Source: `_bmad-output/implementation-artifacts/post-epic-2-r2a5-dapr-sdk-version-reconcile.md` § Suggested Future-Work Spin-Offs item #5 (lines 322–327)] — originating spec for this story
- [Source: `_bmad-output/implementation-artifacts/post-epic-2-r2a5-dapr-sdk-version-reconcile.md` § Architecture Decisions ADR — Symptom fix vs. structural cure (line 259)] — First-Principles framing that produced this story
- [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml:279`] — sibling-spawn comment logged at R2-A5 creation time
- [Source: `_bmad-output/implementation-artifacts/post-epic-2-r2a5-dapr-sdk-version-reconcile.md` Review Findings § Decision-Needed D2/D3 (lines 517–518)] — sibling carve-outs `r2a5c` (prerequisites.md) and `r2a5d` (deploy/README.md + runtime pin)
- [Source: `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 6 R2-A5 row (line 101)] — Epic 2 retro action item; closure annotation references this story
- [Source: `Directory.Packages.props:5-10`] — DAPR SDK 1.17.7 source of truth (untouched)
- [Source: `CLAUDE.md:195`] — pre-patch DAPR SDK reference (post-R2-A5)
- [Source: `docs/concepts/choose-the-right-tool.md:193`] — pre-patch SDK pin reference (post-R2-A5)
- [Source: `docs/guides/dapr-faq.md:43, 47`] — pre-patch SDK pin references (post-R2-A5)
- [Source: `docs/guides/deployment-kubernetes.md:138`] — pre-patch SDK pin reference (post-R2-A5)
- [Source: `docs/reference/nuget-packages.md:158, 182, 183, 184`] — pre-patch table cells (preserved post-R2-A5b)
- [Source: `.github/workflows/docs-validation.yml:22-47`] — `lint-and-links` job structure
- [Source: `scripts/validate-docs.sh:1-62`] — local-dev mirror, 3-stage structure to extend
- [Source: `_bmad-output/implementation-artifacts/post-epic-2-r2a5-dapr-sdk-version-reconcile.md`] — sibling story precedent (story shape, post-merge runbook structure, AC numbering style, sprint-status lifecycle, ADR-style decision capture, conventional-commit prefix rationale)
- [Source: `CLAUDE.md` § Commit Messages] — Conventional Commits, `chore:` and `docs:` are no-version-bump prefixes
- [Source: `CLAUDE.md` § Branch Naming] — `docs/<description>` for documentation changes
- [Source: `CLAUDE.md` § Code Review Process] — 5/5 Epic 2 review-driven-patch rate; budget review-found rework even on trivial-risk stories

### Testing Standards (project-wide rules) — Applicability

Project-wide rules: Tier 1 (xUnit + Shouldly + NSubstitute), Tier 2/3 end-state-inspection requirement (R2-A6), and `Ulid.TryParse` ID validation (R2-A7). **All N/A for R2-A5b** — diff is markdown + bash + YAML + plain-text only; no compiled file, no test, no controller. Tier 1/2/3 baselines guaranteed unchanged by structural argument (Task 1.5). For full rule text see the BMAD story template § Testing Standards or any prior story in `_bmad-output/implementation-artifacts/`.

### Project Structure Notes

- The new script `scripts/check-doc-versions.sh` lives in the existing `scripts/` directory alongside `ci-local.sh`, `validate-docs.sh`, and `validate-docs.ps1`. No new directory is needed.
- A PowerShell mirror (`scripts/check-doc-versions.ps1`) is **not** in scope for this story — the existing `validate-docs.ps1` does NOT mirror `validate-docs.sh` 1:1 (its functionality is a subset), and adding a parallel `.ps1` here would invite drift between the two implementations. The CI runs Linux; local Windows devs can use Git Bash or WSL to invoke the `.sh` directly. **Reviewer-pushback option:** if a reviewer asks for a `.ps1` mirror, defer to a follow-up tooling story; do not add it in this PR.
- The new step in `docs-validation.yml` does NOT require any new GitHub Actions secret, runner change, or permission grant. It runs purely against the checked-out tree.

## Dev Agent Record

### Agent Model Used

claude-opus-4-7 (1M context) — invoked via /bmad-dev-story workflow on 2026-04-28.

### Debug Log References

- **Pre-patch grep audit** (Task 1.3): `Grep "1\.17\.7"` over `CLAUDE.md` + `docs/**/*.md` returned 9 matches at the AC-named sites; `Grep "1\.16\.1"` returned 0 (R2-A5 invariant intact); `scripts/check-doc-versions.sh` confirmed absent. Bash 5.2.37 verified locally (associative-array support OK).
- **Scenario (c) negative test surfaced a regex bug** (Task 3.4): the original `<PackageVersion Include="Dapr.AspNetCore" Version="..."` per-package regex matched commented-out `<!-- <PackageVersion Include="..." /> -->` lines because it was not anchored to start-of-line. Fix: anchored to `^[[:space:]]*<PackageVersion ...` so `<!-- ... -->` lines (and any future indentation/comment-wrap pattern) are excluded. Pre-flight multi-line guard already excluded multi-line wrappers; this fix closes the comment-wrap hole. After the fix, all 5 negative scenarios fire correctly.
- **Final grep audit** (Task 5.1): all 9 AC #9 expectations met (1.17.7 → 4 table cells; 1.17.x → 1 upgrade-path floor; 1.16.1 → 0; date stamps → 0; `currently 1\.X.Y` → 0; out-of-scope refs preserved at expected counts).
- **Diff scope** (Task 5.2/5.3): 8 tracked + 2 untracked-to-stage = 11 files net. Excluded files (Directory.Packages.props, *.csproj, src/**, tests/**, ci.yml, release.yml, package.json, etc.) confirmed absent from diff.

### Completion Notes List

- **Implementation summary.** R2-A5b shipped the structural cure for the version-prose-drift class. Five prose-pointer rewrites (CLAUDE.md:195, choose-the-right-tool.md:193, dapr-faq.md:43+47, deployment-kubernetes.md:138) all use clickable markdown-link form to `Directory.Packages.props` and carry no version number and no `last verified <month>` date stamp. The four `Dapr.*` table cells in `nuget-packages.md` (the legitimate exception) are preserved verbatim and protected by a new CI assertion script.
- **`scripts/check-doc-versions.sh`** is 75 lines (≤ 80 budget). Runs in < 1s. Asserts: (1) no multi-line `<PackageVersion>` in props (pre-flight guard); (2) each Dapr.* package appears in props exactly once (count check, not silent `head -1`); (3) all four Dapr.* pins share the same version; (4) the four table cells in `nuget-packages.md` match the props pin (line-number-agnostic regex); (5) exactly 4 Dapr.* table rows are seen (closes silent-pass-on-empty-doc). All five error modes verified by negative tests; transcripts checked into `_bmad-output/test-artifacts/r2a5b-negative-test-proof.txt` so review can verify each error mode actually fires without re-running the dev's local box.
- **Negative-test bug caught pre-merge.** Scenario (c) (commented-out `Dapr.AspNetCore` line) initially passed because the per-package regex was un-anchored. Anchoring to `^[[:space:]]*<PackageVersion ...` fixed it; this is the kind of silent-misparse failure mode the proof-file requirement is designed to surface, so the discipline paid off.
- **CI wiring.** New step "Verify DAPR SDK version pin consistency" inserted in `.github/workflows/docs-validation.yml` `lint-and-links` job between "Lint Markdown" and "Restore lychee cache". Local-dev mirror: `scripts/validate-docs.sh` gained a Stage 4 (counters bumped from /3 → /4 across all three header echos). Both invocations use `bash scripts/check-doc-versions.sh` to keep CI and local paths byte-identical regardless of executable-bit state on Windows-checked-out trees.
- **AC #11(b) retro pointer chain.** Single-line addendum applied to `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 6 row R2-A5 (line 101): appended ` → R2-A5b structural cure ✅ Closed \`<r2a5b-merge-sha>\` |` after the existing closure annotation. The `<r2a5b-merge-sha>` placeholder is substituted by Post-Merge Runbook step 5 once the squash SHA is captured. No other rows or content of the retro file touched.
- **Source of truth untouched.** `Directory.Packages.props` was NOT modified during this story (verified by diff scope check in Task 5.3). The four DAPR pins remain at `1.17.7`. No `.csproj`, `.props`, `.targets`, `src/**/*.cs`, `tests/**/*.cs`, or non-`docs-validation.yml` workflow was touched.
- **Tier 1 baselines.** Guaranteed unchanged at 788/788 by structural argument (no compiled file in diff). Reviewer is welcome to push back and have the baseline measured before merge per R2-A5 precedent.
- **Conventional-commit prefix.** Recommended: `chore:` (mixed scope — docs prose + CI tooling); `docs:` is also acceptable per AC #12 reviewer-pushback option. Both are no-version-bump prefixes under semantic-release. Suggested PR title: `chore: refactor DAPR SDK version prose to source-of-truth pattern + CI guard rail`.
- **Reviewer-pushback options surfaced for the PR.** AC #1 — extending the prose-pointer pattern to the other 4 § Key Dependencies bullets in CLAUDE.md (deferred per Future-Work item #6); ADR — flipping from assertion-check to auto-generation for the table cells (defer to follow-up tooling story if accepted); AC #12 — `docs:` vs `chore:` prefix (both no-bump, accept either). All three are non-blocking; defaults stand.
- **Tasks 7 + 8 deferred to PR-time.** Status flipped to `review` (PR-ready). Task 7 (PR/squash-merge) and Task 8 (CI green watch) execute when Jerome opens the PR; the Post-Merge Runbook substitutes the merge SHA into the retro addendum and flips sprint-status `review` → `done` with the closure annotation.

### Post-Merge Runbook (mandatory after squash-merge — closes Task 6 + Task 7.3 and AC #11 / #12)

Run the following **after** the PR is squash-merged to `main`:

1. **Capture the merge commit SHA.** `git fetch origin && git log -1 --format=%H origin/main` → `<merge-sha>` (full 40-char or 7-char short form, matching the post-epic-2-r2a5 / post-epic-2-r2a2 precedent).

2. **Flip sprint-status entry `review` → `done`** at `_bmad-output/implementation-artifacts/sprint-status.yaml:279` (line number at story creation; may shift if other entries above it have been updated). Replace the `review` annotation with a `done` annotation in the post-epic-2-r2a5 closure style. Suggested form (one line, no line wraps):

    ```yaml
    post-epic-2-r2a5b-version-prose-source-of-truth-refactor: done  # 2026-MM-DD: PR #<num> squash-merged at SHA <merge-sha> with chore: prefix → no semantic-release version bump (verified). Structural cure for the version-prose-drift class spawned by R2-A5: 4 prose rewrites in CLAUDE.md/choose-the-right-tool.md/dapr-faq.md×2/deployment-kubernetes.md eliminate `currently <X.Y.Z>` and `last verified <month>` patterns (post-patch grep audit: 0 matches for both patterns). New scripts/check-doc-versions.sh asserts the four Dapr.* cells in nuget-packages.md match Directory.Packages.props pin (negative-tested locally before merge). New docs-validation.yml `lint-and-links` step runs the script on every PR. scripts/validate-docs.sh extended to 4 stages for local-dev parity. No code/test/project file change; Directory.Packages.props untouched at 1.17.7; Tier 1 baselines unchanged at 788/788 (structural guarantee — no compiled file in diff). CI green on lint-and-links + sample-build. /bmad-code-review pass: <summary>. Closes the structural-cure follow-up to Epic 2 retro R2-A5 (R2-A5 itself closed at SHA e0cbc78).
    ```

    Bump the header `last_updated:` line (both the comment at line 2 and the YAML key at line 45) to the merge date with a one-line "→ done" summary referencing R2-A5b.

3. **Verify semantic-release does NOT bump any package version.** After CI runs the release pipeline, confirm:
    - GitHub Releases shows **no new release** for the 6 NuGet packages.
    - `CHANGELOG.md` is **not** updated.
    - If a bump fires, the squashed commit subject is the most likely cause — confirm it starts with `chore:` (or `docs:`). If it bumped under either no-bump prefix, that's a release-config drift and warrants its own fix story.

4. **Verify the new CI step ran on the merge commit's main-branch CI run.** Inspect the `Documentation Validation` workflow run for the merge commit. The `lint-and-links` job's "Verify DAPR SDK version pin consistency" step MUST be present and green. If the step is missing, the workflow YAML edit may not have applied correctly.

5. **Substitute the merge SHA into R2-A5's retro-row addendum (per AC #11(b)).** At dev-time the addendum was inserted with a `<r2a5b-merge-sha>` placeholder (or a date stamp). After capturing the merge SHA in step 1, edit `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 6 row R2-A5 (line 101 at story creation; may have shifted slightly): replace the placeholder with the actual short merge SHA. Single-line edit; no other content of the retro file is touched. Mirrors the post-epic-2-r2a5 / post-epic-2-r2a2 SHA-substitution precedent. This step closes the retro pointer chain so a future retro-reader sees `R2-A5 ✅ Closed e0cbc78 ... → R2-A5b structural cure ✅ Closed <r2a5b-sha>`.

6. **Final story-file housekeeping** in `_bmad-output/implementation-artifacts/post-epic-2-r2a5b-version-prose-source-of-truth-refactor.md`:
    - Set Status: `review` → `done`.
    - Mark Tasks 1–8 (and all subtasks) `[~]` → `[x]`.
    - Append a Change Log entry: `2026-MM-DD — Post-merge: sprint-status → done; retro-row addendum SHA-substituted; CI verified green; semantic-release no-op confirmed.`

The audit trail is complete when Tasks 6 + 7 + 8 are all `[x]`, sprint-status is `done` with the merge-SHA-substituted annotation, R2-A5's retro row carries the R2-A5b SHA, and the new `lint-and-links` step is verified green on the merge commit's CI run.

### File List

**Modified (8):**

- `CLAUDE.md` — line 195: `- DAPR SDK 1.17.7 (Client, AspNetCore, Actors)` → `- DAPR SDK (Client, AspNetCore, Actors) — pinned in [\`Directory.Packages.props\`](Directory.Packages.props)` (AC #1)
- `docs/concepts/choose-the-right-tool.md` — line 193: `currently 1.17.7, as pinned in \`Directory.Packages.props\`, last verified April 2026` removed; replaced with `pinned in [\`Directory.Packages.props\`](../../Directory.Packages.props) (the single source of truth)` (AC #2)
- `docs/guides/dapr-faq.md` — line 43 (TL;DR) + line 47 (body): both `currently 1.17.7` / `last verified April 2026` mentions replaced with markdown-link prose pointers to `[\`Directory.Packages.props\`](../../Directory.Packages.props)` (AC #3)
- `docs/guides/deployment-kubernetes.md` — line 138 Note: `DAPR SDK version **1.17.7** (see \`Directory.Packages.props\`)` → `DAPR SDK in [\`Directory.Packages.props\`](../../Directory.Packages.props) (the single source of truth)`. Line 143 `runtime-version 1.14.4` preserved unchanged (AC #4, runtime out of scope)
- `scripts/validate-docs.sh` — Stage 1/3, 2/3, 3/3 counter prose bumped to /4; new Stage 4 ("DAPR SDK Version Pin Consistency") inserted between Stage 3 and the final `=== All validations passed ===` echo, invoking `bash scripts/check-doc-versions.sh` (AC #8)
- `.github/workflows/docs-validation.yml` — new `lint-and-links` step "Verify DAPR SDK version pin consistency" running `bash scripts/check-doc-versions.sh`, inserted between "Lint Markdown" and "Restore lychee cache" (AC #7)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — entry `post-epic-2-r2a5b-version-prose-source-of-truth-refactor` cycled `ready-for-dev` → `in-progress` → `review` with end-of-session closure-style annotation; `last_updated:` (line 2 comment + line 45 YAML key) bumped to the `→ review` summary (AC #11(a))
- `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` — § 6 row R2-A5 (line 101) gained single-line addendum ` → R2-A5b structural cure ✅ Closed \`<r2a5b-merge-sha>\` |`. SHA placeholder substituted post-merge per Post-Merge Runbook step 5 (AC #11(b))

**Added (3, untracked-to-stage):**

- `scripts/check-doc-versions.sh` (new file, 75 lines, executable bit set via `git update-index --chmod=+x`) — DAPR SDK version pin consistency check; asserts the 4 `Dapr.*` cells in `docs/reference/nuget-packages.md` match `Directory.Packages.props` (AC #6)
- `_bmad-output/test-artifacts/r2a5b-negative-test-proof.txt` (new file, 69 lines) — verifiable evidence that all four mandatory error modes (cell-mismatch, divergent props, missing package, empty doc rows) and the optional multi-line guard scenario actually fire under temp-edits (AC #13)
- `_bmad-output/implementation-artifacts/post-epic-2-r2a5b-version-prose-source-of-truth-refactor.md` — this story file housekeeping (Status `ready-for-dev` → `in-progress` → `review`, Task checkboxes ticked, Dev Agent Record / File List / Change Log populated)

**NOT modified (verified by diff-scope check, AC #10):** `Directory.Packages.props`; `Directory.Build.props`/`.targets`; all `*.csproj`; all `src/**/*.cs`; all `tests/**/*.cs`; `docs/reference/nuget-packages.md` (cells preserved verbatim, no marker comments inserted); `docs/guides/upgrade-path.md` (compatibility-floor row at line 141 preserved); `CONTRIBUTING.md`; `AGENTS.md`; `deploy/README.md`; `docs/getting-started/prerequisites.md` (carved to r2a5c); `package.json`; `nuget.config`; `global.json`; all CI workflows other than `docs-validation.yml` (`ci.yml`, `release.yml`, `deploy-staging.yml`, `docs-api-reference.yml`, `perf-lab.yml`).

## Change Log

| Date       | Author             | Change                                                                                                                                                                                                                                                                                                                                                                              |
|------------|--------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 2026-04-28 | Story creation     | Story drafted with 13 ACs, 8 tasks, full Dev Notes (Scope, Why, File Locations, ADRs, Risk, Constraints, Future-Work, References) + Post-Merge Runbook. Status `ready-for-dev`. Spawned at R2-A5 creation time per ADR — Symptom fix vs. structural cure (R2-A5 § line 259). Sibling carve-outs `post-epic-2-r2a5c-prerequisites-cli-runtime-stamps` and `post-epic-2-r2a5d-runtime-version-bump-deployment-docs` named in the header out-of-scope list. |
| 2026-04-28 | Party-mode review patch round (Murat + Winston + Amelia) | Three patches applied via `/bmad-party-mode` multi-agent review: (1) Murat — AC #13 + Task 3.4 expanded from 1 negative-test scenario to 4 (cell-mismatch / divergent-props / missing-package / empty-doc-rows); illustrative script in Task 3.1 gained a `rows_seen` counter + `EXPECTED_DAPR_ROWS=4` assertion to close the silent-pass-on-empty-doc hole; AC #6 contract updated to enumerate all error-mode messages. (2) Winston — script header gained a 4-point Assumptions block declaring single-line `<PackageVersion>` shape, exactly-once-per-package, fixed-width DAPR-row pattern, and bash 4+ requirement; rewrite-trigger pointer (`xmllint` / `dotnet msbuild -getProperty:`) flagged for future maintainers. (3) Amelia — replaced silent `head -1` with explicit "exactly-1-match" check (counts via `grep -cE`; errors on 0 or > 1); AC #8 + Task 4.2 unambiguously prefer `bash scripts/check-doc-versions.sh` over `./scripts/...` to keep local-dev and CI invocation byte-identical. Paige's symmetric-extension #4 (rewrite all 5 § Key Dependencies bullets in CLAUDE.md, not just DAPR) deferred per team's framing — defer-or-expand decision still with Jerome. |
| 2026-04-28 | Advanced-elicitation patch round (5 methods × 9 patches) | Nine patches applied via `/bmad-advanced-elicitation` (Pre-mortem + Red Team vs Blue Team + Occam's Razor + Meta-Prompting + Debate Club Showdown). **A — Verifiable evidence:** AC #13 + Task 3.4 now require the four negative-test transcripts to be checked into `_bmad-output/test-artifacts/r2a5b-negative-test-proof.txt` (closes pre-mortem failure F1: handwave-able Completion-Notes-only transcripts). **B — Multi-line guard:** AC #6 + Task 3.1 illustrative script gained a pre-flight `grep -cE "<PackageVersion[^/]*$"` check that exits with `ERROR: multi-line <PackageVersion> element detected ... REWRITE NEEDED` if a future props-file refactor introduces multi-line elements (closes pre-mortem F2: silent regex misparse). Optional negative-test scenario (e) added to Task 3.4 to exercise the guard. **C — Markdown links:** AC #1–#4 + Task 2.1–2.5 + Task 2.6 verification rewrite the 5 prose-pointer sites from code-spans to clickable markdown links to `Directory.Packages.props` (relative paths: bare for CLAUDE.md at repo root, `../../Directory.Packages.props` for the 3 docs/ files). Closes pre-mortem F3: code-span pointers were not actually navigable; readers shrugged. **D — Retro pointer chain:** AC #11 split into (a) sprint-status lifecycle and (b) single-line addendum to R2-A5's retro-row chaining forward to R2-A5b's merge SHA; Post-Merge Runbook gained step 5 for SHA substitution; Constraints + Scope Summary + Task 5.2 file list updated. Closes pre-mortem F4: stale forward-reference in epic-2-retro. **E — Honest framing:** Story header + Why This Story Exists rephrased — "structurally eliminated" rewritten to acknowledge cure asymmetry (prose drift class eliminated; table-cell drift class converted from silent lag to hard CI failure; net per-bump manual-edit count 9 → 4, silent-drift opportunities 9 → 0). Closes pre-mortem F5: overclaimed cure. **F — Social-contract-decay risk:** new row added to Risk Assessment as accepted risk (PR review catches `[skip ci]`-class bypasses; not engineered around). Closes red-team R1's strongest hit. **G — Boilerplate collapse:** Testing Standards + R2-A6 Compliance + R2-A7 Compliance subsections (~25 lines) collapsed to a single 3-line "N/A" block with template pointer. Per Occam's Razor — minimum sufficient signal to the LLM dev agent. **H — Pattern Established by This Story (new section):** ~30 lines under Dev Notes naming Template A (Prose-Pointer) + Template B (Assertion-Check) + Composition Rule ("either eliminate the restatement or assert it") plus 4 explicit pattern carve-outs (lock-file resolution / multi-source migration / forward-looking floors / social-contract decay). Per Meta-Prompting — story is first application of a project-wide pattern, not a one-off DAPR fix; future sibling stories cite this section as authority. **I — Future-Work item #6:** debate-synthesis added as item #6 in Suggested Future-Work Spin-Offs — names 4 sibling stories (`r2a5b-ext-aspire/mediatr/fluentvalidation/otel`) and ADR-decision-bearing pinning questions per family. Per Debate Club synthesis — ship narrow, plan broad. Story-length impact: ~+45 lines net (G saved 25; H added 30; everything else net-positive). |
| 2026-04-28 | Implementation (claude-opus-4-7 via `/bmad-dev-story`) | Status `ready-for-dev` → `in-progress` → `review`. Tasks 1–6 + all subtasks marked `[x]`; Tasks 7–8 deferred to PR-time per their PR/CI nature. **Task 1** — pre-patch state verified: 9 `1.17.7` matches at AC sites; 0 `1.16.1` (R2-A5 invariant); props pinned at `1.17.7`; script absent. **Task 2** — 5 prose-pointer rewrites in 4 files (CLAUDE.md:195, choose-the-right-tool.md:193, dapr-faq.md:43+47, deployment-kubernetes.md:138). Markdown-link form verified across the 4 edited files (5 matches). **Task 3** — `scripts/check-doc-versions.sh` created (75 lines), executable bit set; PASSED on canonical state. Negative tests for all 4 mandatory scenarios + 1 optional (multi-line guard) captured to `_bmad-output/test-artifacts/r2a5b-negative-test-proof.txt` (69 lines). **Bug caught pre-merge:** Scenario (c) initially passed because the per-package regex (`<PackageVersion Include="$pkg" ...`) matched commented-out lines; fixed by anchoring to `^[[:space:]]*<PackageVersion ...` so `<!-- ... -->`-wrapped lines are excluded. **Task 4** — new step "Verify DAPR SDK version pin consistency" inserted in `docs-validation.yml` `lint-and-links` job; new Stage 4 + counter bumps in `scripts/validate-docs.sh`. Both invocations use `bash scripts/check-doc-versions.sh` for byte-identical CI/local paths. **Task 5** — full AC #9 grep audit green (1.17.7 → 4 table cells; 1.16.1 → 0; date stamps → 0; out-of-scope refs preserved at expected counts); diff scope confirmed 8 tracked + 2 untracked-to-stage = 11 files net; excluded files (Directory.Packages.props, *.csproj, src/**, tests/**, ci.yml, release.yml, etc.) absent. **Task 6** — sprint-status entry cycled to `review` with closure-style annotation; `last_updated` bumped at line 2 + line 45; AC #11(b) single-line retro addendum applied to `epic-2-retro-2026-04-26.md` § 6 row R2-A5 (SHA placeholder for Post-Merge Runbook step 5). Source of truth (`Directory.Packages.props`) untouched at `1.17.7`. Tier 1 baselines guaranteed unchanged at 788/788 by structural argument (no compiled file in diff). Status: `review` (PR-ready). |
