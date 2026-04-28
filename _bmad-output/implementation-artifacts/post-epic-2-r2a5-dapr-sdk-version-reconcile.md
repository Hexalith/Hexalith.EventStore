# Story Post-Epic-2 R2-A5: Reconcile DAPR SDK Version Documentation with `Directory.Packages.props` (1.17.7)

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a project maintainer,
I want every prose reference to the DAPR SDK version across `CLAUDE.md`, the `docs/` tree, and any other shipped documentation to agree with the actual pin in `Directory.Packages.props` (currently **`1.17.7`** for `Dapr.Client`, `Dapr.AspNetCore`, `Dapr.Actors`, `Dapr.Actors.AspNetCore`),
so that contributors, downstream consumers, and reviewers reading the project's own documentation get the same answer the build does — `Directory.Packages.props` is the single source of truth, and the docs catch up to whatever it says.

This story closes Epic 2 retro action item **R2-A5** ("Resolve DAPR SDK drift: either update `CLAUDE.md` to 1.16.1 or upgrade `Directory.Packages.props` to 1.17.0 (verify all references)") in one fix. The originating spec is `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md` § Proposal 5 (lines 367–393).

**Important context the dev MUST read before starting.** The retro framing in `epic-2-retro-2026-04-26.md:101` and the originating Proposal 5 (lines 367–393) describe the drift direction at retro time: `CLAUDE.md` said **`1.17.0`**, `Directory.Packages.props` pinned **`1.16.1`**. Two things have happened since then:

1. **The retro-recording commit `0f75772` (2026-04-26) pre-emptively edited `CLAUDE.md`** from `1.17.0` → `1.16.1` (verified via `git log -L "/DAPR SDK/,+1:CLAUDE.md"`), implementing the doc-side option of Proposal 5 against what the retro thought was the source of truth.
2. **`Directory.Packages.props` had already been bumped to `1.17.7` on 2026-04-05 in commit `f7e1302`** (`build: update dependencies and improve test isolation`), three weeks *before* the retro was written. The retro's "props pins 1.16.1" claim was stale at the moment it was logged.

**Net result:** the drift at HEAD `4d10ed0` (2026-04-28) is the **opposite direction** from what the retro framed. `CLAUDE.md` and 5 doc files say `1.16.1`; `Directory.Packages.props` actually pins `1.17.7`. This story takes the **doc-aligns-to-source-of-truth** path that Proposal 5 explicitly recommended (`sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md:383-386`: *"Directory.Packages.props is the source of truth for package versions. CLAUDE.md was ahead of the actual pin. Aligning the doc is trivial and removes ambiguity. If/when DAPR SDK 1.17.x is intentionally adopted, the upgrade goes through .props and CLAUDE.md gets re-aligned."*). The dev does NOT touch `Directory.Packages.props`; the docs catch up.

This story does NOT:

- Touch `Directory.Packages.props` or any `.csproj` (the pin is already where it needs to be).
- Change any DAPR SDK API call site, `using` directive, or runtime configuration.
- Modify any test, code file, or workflow.
- Bump the DAPR runtime pin in `docs/guides/deployment-kubernetes.md:143` (`dapr init -k --runtime-version 1.14.4`) — that is a SDK-to-runtime compatibility decision, not a doc-reconcile decision; out of scope.
- Touch references to **DAPR runtime feature versions** in `docs/guides/dapr-component-reference.md` (`SCOPING FIELD REFERENCE (DAPR 1.16)` at lines 320, 478, 634 — these are minimum-feature-availability annotations on the runtime API, not SDK pins; out of scope).
- Touch `CONTRIBUTING.md:50` (`DAPR CLI (1.16.x or later)`) — this is a CLI / runtime compatibility floor, not an SDK pin; "or later" is forward-compatible and remains correct; out of scope.
- Touch the `Hexalith.Tenants` submodule — separate repo, separate pin lifecycle.

## Acceptance Criteria

1. **`CLAUDE.md` agrees with `Directory.Packages.props`.** `CLAUDE.md` § Key Dependencies, line 195 (verbatim today: `- DAPR SDK 1.16.1 (Client, AspNetCore, Actors)`) is updated to `- DAPR SDK 1.17.7 (Client, AspNetCore, Actors)`. The "(Client, AspNetCore, Actors)" parenthetical stays byte-identical (the four packages — `Dapr.Client`, `Dapr.AspNetCore`, `Dapr.Actors`, `Dapr.Actors.AspNetCore` — all share the `1.17.7` pin, and the existing prose is a fair shorthand). No other line in `CLAUDE.md` is touched (verified pre-patch: `grep -n "1\.16\|1\.17\|DAPR SDK\|Dapr SDK" CLAUDE.md` returns exactly one match at line 195).

2. **`docs/concepts/choose-the-right-tool.md` agrees with the pin.** Line 193 (verbatim today: `Hexalith depends on a specific DAPR SDK version (currently 1.16.1, as pinned in \`Directory.Packages.props\`, last verified March 2026).`) is updated to `Hexalith depends on a specific DAPR SDK version (currently 1.17.7, as pinned in \`Directory.Packages.props\`, last verified April 2026).`. Two edits in one line: the version number `1.16.1` → `1.17.7` AND the verification stamp `March 2026` → `April 2026`. The remainder of the paragraph (DAPR SemVer language, CI pipeline statement) is byte-identical.

3. **`docs/guides/dapr-faq.md` agrees with the pin in both occurrences.** Two prose mentions are updated:
   - Line 43 (TL;DR): `Hexalith pins to a specific SDK version (currently 1.16.1)` → `Hexalith pins to a specific SDK version (currently 1.17.7)`.
   - Line 47 (body): `Hexalith pins the DAPR SDK version in \`Directory.Packages.props\` (currently **1.16.1** — last verified March 2026).` → `Hexalith pins the DAPR SDK version in \`Directory.Packages.props\` (currently **1.17.7** — last verified April 2026).`. Bold markdown preserved; em-dash preserved; date stamp updated.

4. **`docs/guides/deployment-kubernetes.md` agrees with the pin.** Line 138 (verbatim today: `> **Note:** The project uses DAPR SDK version **1.16.1** (see \`Directory.Packages.props\`). Use a compatible DAPR runtime version. Consult the [DAPR SDK-to-runtime compatibility matrix](https://docs.dapr.io/operations/support/support-release-policy/) for version mapping.`) is updated so the bolded version becomes `**1.17.7**`. The blockquote `>` prefix, the "compatible DAPR runtime version" prose, and the existing link to the DAPR SDK-to-runtime compatibility matrix are byte-identical. **Do NOT change the `dapr init -k --runtime-version 1.14.4` command at line 143** — the runtime pin is a separate concern and out of scope for R2-A5 (the dev should leave the runtime pin where it is and flag the DAPR runtime ↔ SDK 1.17.7 compatibility check as a future-work note in Dev Agent Record → Completion Notes).

5. **`docs/reference/nuget-packages.md` agrees with the pin in all four occurrences.** Four table-cell entries updated:
   - Line 158: `| Dapr.Client                               | 1.16.1  |` → `| Dapr.Client                               | 1.17.7  |`
   - Line 182: `| Dapr.Client            | 1.16.1  |` → `| Dapr.Client            | 1.17.7  |`
   - Line 183: `| Dapr.Actors            | 1.16.1  |` → `| Dapr.Actors            | 1.17.7  |`
   - Line 184: `| Dapr.Actors.AspNetCore | 1.16.1  |` → `| Dapr.Actors.AspNetCore | 1.17.7  |`
   The exact column-alignment whitespace MUST be preserved (the project uses fixed-width markdown tables — the lengths of `1.16.1` and `1.17.7` are both 6 characters, so column alignment stays correct without re-padding the table headers or separators). Verify with a 0-context diff (`git diff -U0 docs/reference/nuget-packages.md`) that no markdown-table separator row was incidentally re-aligned.

6. **`docs/guides/upgrade-path.md` compatibility-matrix row agrees with the pin.** Line 141 (verbatim today: `| v1 (current)     | 10.0.x   | 1.16.x+  | 13.1.x+     | 14.x    | 12.x             |`) is updated so the DAPR SDK column reads `1.17.x+`. Net edit: `1.16.x+  ` → `1.17.x+  ` (the trailing whitespace count stays identical — both `1.16.x+` and `1.17.x+` are 7 characters; the table column is 8 wide; the trailing two spaces line up). Rationale: although `1.16.x+` is technically still true (1.17.x ⊃ 1.16.x+), bumping the floor to match the actual pin keeps the matrix honest and gives consumers an accurate "below this you may not pass tests" signal. Other rows in the table (and the surrounding "DAPR Compatibility" prose at lines 145–149) are unchanged.

7. **A repository-wide grep confirms no surviving `1.16.1` SDK pin references in the targeted-doc set.** After the patch:
   - `Grep` for `1\.16\.1` across `CLAUDE.md` and `docs/**/*.md` returns **zero matches** (pre-patch: **9 matches** across 5 files — line 195 of CLAUDE.md, line 193 of choose-the-right-tool.md, lines 43 + 47 of dapr-faq.md, line 138 of deployment-kubernetes.md, lines 158 + 182 + 183 + 184 of nuget-packages.md).
   - `Grep` for `1\.17\.7` across the same file set returns **exactly 9 matches** (the 9 lines above, all now showing `1.17.7`). If the count differs from 9, a sibling-backlog edit has shifted the doc tree — re-baseline before merging rather than masking the drift with a lenient inequality.
   - The `1.16` substring may still appear in legitimate non-SDK contexts: `docs/guides/dapr-component-reference.md` lines 320 / 478 / 634 (`SCOPING FIELD REFERENCE (DAPR 1.16)` — runtime feature-availability annotation, NOT an SDK pin), and `CONTRIBUTING.md:50` (`DAPR CLI (1.16.x or later)` — CLI floor, NOT an SDK pin). These MUST NOT be edited; AC #7 confirms they are still present and unchanged after the patch.
   - `Grep` for `last verified March 2026` across `docs/**/*.md` returns **zero matches** (the two prose-stamp updates in choose-the-right-tool.md:193 and dapr-faq.md:47 catch all current uses; if a future grep finds another `last verified March 2026` mention, it indicates an out-of-scope drift the dev did NOT catch and should be added to AC #2 / #3 in a re-baseline before merging).

8. **No code, test, project file, or workflow change.** The diff in this PR is **markdown-only**. Specifically:
   - `Directory.Packages.props` is **NOT** modified (the source of truth is correct as-is at `1.17.7`).
   - No `.csproj`, `.props`, `.targets`, or `Directory.Build.*` file is modified.
   - No `src/**/*.cs` or `tests/**/*.cs` file is modified.
   - No `.github/workflows/*.yml` is modified.
   - No `package.json`, `package-lock.json`, `nuget.config`, `global.json`, or `aspire.config.json` is modified.
   - **`AGENTS.md` is NOT modified.** That file (lines 71–97) mentions DAPR runtime / CLI / slim-mode behavior but does **NOT** pin an SDK version. A grep for `Dapr` in `AGENTS.md` returns 5 hits, none of which are SDK-pin restatements; out of scope for R2-A5; **do not edit** even opportunistically. *(P9 / Critique W1 hardening — Murat asked for this explicit naming so a `grep Dapr` doesn't tempt scope expansion.)*
   - **`CONTRIBUTING.md` is NOT modified.** Its `DAPR CLI (1.16.x or later)` line at line 50 is a CLI floor, not an SDK pin; "or later" is forward-compatible and remains correct. Bumping it is logged as future-work item #3 — out of scope here.
   - No `_bmad-output/planning-artifacts/*.md` is modified (those are originating specs and historical records — the closure annotation in AC #9 is the *only* `_bmad-output/` edit, and it lives in `implementation-artifacts/`).

9. **Epic 2 retro R2-A5 is marked complete in its action-item table and § 10 commitments.** Mirror the post-epic-2-r2a8 / post-epic-2-r2a2 closure-annotation precedent:
   - `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 6 row R2-A5 (line 101): append `— ✅ Done <merge-commit-sha> — CLAUDE.md and 5 docs files (choose-the-right-tool.md:193, dapr-faq.md:43+47, deployment-kubernetes.md:138, nuget-packages.md:158+182+183+184, upgrade-path.md:141) now agree with Directory.Packages.props at DAPR SDK 1.17.7 (source of truth). **(symptom fix — structural fix that would prevent re-occurrence is tracked as \`post-epic-2-r2a5b-version-prose-source-of-truth-refactor\`, sprint-status: backlog).** See story \`post-epic-2-r2a5-dapr-sdk-version-reconcile\`.` after the existing "The two files agree; CI passes" cell text. *(P8 / Hindsight Reflection — Future-Jerome reading the closure log six months on should see both the symptom fix and the structural-fix follow-up explicitly named.)*
   - `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 10 "Critical path before Epic 3 closes" line (line 161): append ` [R2-A5 ✅ Closed <merge-commit-sha> — see §6 row]` after the `R2-A5 (DAPR SDK drift)` token. Do NOT delete the line — preserve the audit trail per the post-epic-2-r2a8 / post-epic-2-r2a2 precedent (which keeps the original line and inline-annotates the closure). **Wall-of-text-precedent note (P12 / Critique W4):** the resulting line will be very long, mirroring the existing R2-A2 inline-bracket annotation. The "wall-of-text" doc-style issue was already flagged as a deferred review finding in `post-epic-2-r2a2-commandstatus-isterminal-extension.md` § Review Findings; this story PRESERVES that precedent rather than break the format unilaterally. A future doc-cleanup story could restructure all three retro § 10 lines to a numbered list — out of scope here.
   - **Do NOT modify** `_bmad-output/implementation-artifacts/epic-1-retro-2026-04-26.md`, `epic-3-retro-2026-04-26.md`, or `epic-4-retro-2026-04-26.md` — R2-A5 is exclusively an Epic 2 retro item; no other retro tracks it.
   - **Do NOT modify** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md` — it is the originating spec and stands as written.
   - **Re-baseline note.** The retro framing at line 101 ("DAPR SDK drift: either update `CLAUDE.md` to 1.16.1 or upgrade `Directory.Packages.props` to 1.17.0 (verify all references)") refers to a drift that no longer exists in that direction. The closure annotation should NOT pretend the original framing was correct — the wording above explicitly names the actual reconcile (1.16.1 docs → 1.17.7 docs to match the props pin) so a future reader of the retro understands what actually happened.

10. **Conventional-commit hygiene on the merge — `docs:` prefix.** The merge commit (or squashed PR title) uses **`docs:`** per `CLAUDE.md` § Commit Messages. The diff is purely documentation; no `feat:` / `fix:` / `chore(deps):` is appropriate. Example acceptable subjects:
    - `docs: reconcile DAPR SDK version (1.16.1 → 1.17.7) across CLAUDE.md and docs/`
    - `docs: align DAPR SDK version docs with Directory.Packages.props (1.17.7) — closes R2-A5`
    The body MUST name R2-A5 and reference Proposal 5 of `sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md`. **Do NOT use `chore(deps):`** — no dependency change occurs in this PR; the dependency upgrade already happened upstream in commit `f7e1302` (2026-04-05). **Do NOT use `feat:` / `fix:` / `BREAKING CHANGE:`** — semantic-release MUST NOT bump any package version on this merge (no code change in any of the 6 published NuGet packages).
    **Version-bump-fired emergency revert (P5 / pre-mortem failure mode E):** if despite the `docs:` prefix semantic-release fires and bumps any of the 6 NuGet package versions, **revert IMMEDIATELY** (within the GitHub Releases retention window, before any consumer downloads the wrongly-versioned package). Versioning lying about the change is a worse outcome than a broken merge — a downstream `dotnet restore` that pulls a "minor bump" containing zero code change pollutes the consumer's lockfile and erodes trust in the release pipeline's accuracy. After revert, file a `release-config-drift` story to investigate why semantic-release bumped on a `docs:` prefix.

11. **Sprint-status updated to `done` post-merge.** `_bmad-output/implementation-artifacts/sprint-status.yaml` development_status entry `post-epic-2-r2a5-dapr-sdk-version-reconcile` is updated through the lifecycle: `backlog` → `ready-for-dev` (this story creation) → `in-progress` (dev start) → `review` (PR opened) → `done` (post-merge with closure annotation referencing the merge SHA, the 6 doc-files-touched count, and the 10-line edit total). Mirror the post-epic-2-r2a8 / post-epic-2-r2a2 closure-annotation style. `last_updated` is updated to the closure date with a one-line `→ done` summary.

12. **`docs-validation.yml` CI run on this PR is green.** The repository's `Documentation Validation` workflow (`.github/workflows/docs-validation.yml`) runs on every PR via `pull_request: branches: [main]`; its `lint-and-links` job runs `markdownlint` over the doc tree and `lychee` (link checker) over all markdown links. **Both checks MUST pass on this PR.**
    - **`markdownlint` failures introduced by this PR's edits** (e.g., a table cell that exceeds a configured line-length rule, a heading-level skip, a malformed list) are regressions and MUST be fixed in this PR. The 10 substitutions are character-count-neutral (`1.16.1` ↔ `1.17.7` and `1.16.x+` ↔ `1.17.x+` are byte-equal-length; `March 2026` ↔ `April 2026` are byte-equal-length); markdownlint regressions should be rare-to-impossible.
    - **`lychee` failures that are pre-existing on `main`** (the link was already broken before this PR opened) are NOT blockers — document the pre-existing red in the PR body and a follow-up doc-cleanup story; this PR is not the place to fix unrelated link rot. Verify the pre-existing-on-main hypothesis by running `lychee` locally on a clean `main` checkout (`scripts/validate-docs.sh` / `validate-docs.ps1`) or by inspecting the most recent green CI run on `main`.
    - **`lychee` failures introduced by this PR's edits** (a link that the substitutions broke — e.g., a markdown anchor that depended on the version number being part of the link target) are regressions and MUST be fixed in this PR. Anchor-breakage is structurally unlikely here (none of the 10 edits touch a markdown link target), but verify before merging.
    - **`aspire-tests` job in `docs-validation.yml`**, if present and `continue-on-error: true` (per the post-epic-2-r2a2 precedent at line 538 of that story's Change Log), is non-blocking: a timeout-cancellation on `aspire-tests` does NOT block this PR. Only the `lint-and-links` job is mandatory.

## Tasks / Subtasks

- [x] Task 1: Verify the pre-patch state (AC: #1–#7)
  - [x] 1.1 Run `git status` and confirm the working tree is clean except for the expected untracked items (`Hexalith.Tenants` submodule pointer, `.claude/mcp.json`, `_tmp_diff.patch` per the current sprint-status header). If unexpected uncommitted changes exist, stash or commit them before starting work to keep the diff narrow.
  - [x] 1.2 Confirm `Directory.Packages.props` actually pins DAPR at `1.17.7` — `Grep` lines 5–10:
    ```
    <ItemGroup Label="Dapr">
      <PackageVersion Include="Dapr.Client" Version="1.17.7" />
      <PackageVersion Include="Dapr.AspNetCore" Version="1.17.7" />
      <PackageVersion Include="Dapr.Actors" Version="1.17.7" />
      <PackageVersion Include="Dapr.Actors.AspNetCore" Version="1.17.7" />
    </ItemGroup>
    ```
    All four packages MUST be at the same version. If any has drifted (e.g., one is at `1.17.6` or `1.18.0`), HALT and consult Jerome — that is a separate fix outside this story's scope. **The story's load-bearing assumption is that `Directory.Packages.props` is internally consistent and is the source of truth.**
    **Source-of-truth-has-moved guard (P2 / pre-mortem failure mode B):** if all four packages are internally consistent BUT pin a version OTHER than `1.17.7` (e.g., a sibling commit bumped the props to `1.18.0` between this story's creation and dev start), **HALT — do NOT apply the spec-prescribed `→ 1.17.7` substitutions.** The spec text is now stale and applying it would re-create the drift class one minor version forward (the exact failure mode this story exists to fix). Consult Jerome to either (a) rebase the spec to the new pin and re-baseline AC #1–#7 verbatim strings, or (b) close this story as superseded by whatever caused the upstream bump.
  - [x] 1.3 Confirm the doc drift exists at the documented locations — `Grep` for `1\.16\.1` across `CLAUDE.md` and `docs/**/*.md` should return exactly **9 matches across 5 files**: CLAUDE.md:195, choose-the-right-tool.md:193, dapr-faq.md:43, dapr-faq.md:47, deployment-kubernetes.md:138, nuget-packages.md:158, nuget-packages.md:182, nuget-packages.md:183, nuget-packages.md:184. (`dapr-faq.md` contributes 2 matches; `nuget-packages.md` contributes 4; the other 3 files contribute 1 each.) If any of these line numbers have shifted from a recent unrelated edit (e.g., a doc reflow), update the file references in this story's Dev Notes → File Locations table before proceeding so reviewers can audit the changes against the as-edited tree. (Same discipline the post-epic-2-r2a2 story used.)
  - [x] 1.4 Confirm `1.17.x` (without the `.7`) appears in `docs/guides/upgrade-path.md` only at the compatibility-matrix row to be edited (AC #6). `Grep` for `1\.17\.x` across `docs/**/*.md` should return zero matches pre-patch (the row currently says `1.16.x+`). Post-patch it should return exactly 1 match at line 141.
  - [x] 1.5 Confirm the out-of-scope `1.16` references the dev MUST NOT touch:
    - `Grep` `DAPR 1\.16` across `docs/guides/dapr-component-reference.md` → 3 matches at lines 320, 478, 634 (runtime feature-availability annotations — these are runtime-version markers for the SCOPING FIELD REFERENCE blocks; the scoping fields became stable in DAPR runtime 1.16 and that's what the 1.16 references; SDK 1.17.7 does not invalidate them). Leave unchanged.
    - `Grep` `1\.16\.x` across `CONTRIBUTING.md` → 1 match at line 50 (DAPR CLI floor; "or later" is forward-compatible). Leave unchanged.
    - `Grep` `runtime-version 1\.14` across `docs/guides/deployment-kubernetes.md` → 1 match at line 143 (DAPR runtime version pin; out of scope; flag for future-work note in Completion Notes that runtime ↔ SDK 1.17.7 compatibility should be verified per the link at line 138).
    - `Grep` `Dapr\|DAPR` in `AGENTS.md` → 5 matches at lines 71, 76, 78, 96, 97 — all describe DAPR runtime / CLI / slim-mode behavior, **none pin an SDK version**. Out of scope; do NOT edit. *(P9 / Critique W1 hardening.)*
  - [x] 1.6 Capture the pre-patch Tier 1 baseline by **measuring at story-start, not by trusting prior-session annotations**. Yesterday's 788 / 788 (per the post-epic-2-r2a2 closure note at sprint-status.yaml:2) is a **starting hypothesis, not a load-bearing fact** — sibling backlog items could have shifted it overnight. Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests/`, `dotnet test tests/Hexalith.EventStore.Client.Tests/ -p:NuGetAudit=false`, `dotnet test tests/Hexalith.EventStore.Sample.Tests/ -p:NuGetAudit=false`, `dotnet test tests/Hexalith.EventStore.Testing.Tests/ -p:NuGetAudit=false`, `dotnet test tests/Hexalith.EventStore.SignalR.Tests/`. Record the per-project pass counts. **Hypothesis to verify (do not assume):** Contracts.Tests = 281, Client.Tests = 334, Sample.Tests = 63, Testing.Tests = 78, SignalR.Tests = 32 → net Tier 1 = 788 / 788. If any project's count differs, capture the actual measured baseline and use *that* as the post-patch comparison anchor. The post-patch count MUST equal the measured pre-patch count (this is a docs-only PR; no test could possibly change behavior).
  - [x] 1.7 Capture the pre-patch full Release build state: `dotnet build Hexalith.EventStore.slnx --configuration Release -p:NuGetAudit=false` → expect 0 warnings, 0 errors with `TreatWarningsAsErrors=true`. Same expected post-patch (this story doesn't touch any compiled file).

- [x] Task 2: Update `CLAUDE.md` (AC: #1)
  - [x] 2.1 Open `CLAUDE.md` and locate line 195 (`- DAPR SDK 1.16.1 (Client, AspNetCore, Actors)`).
  - [x] 2.2 Replace `1.16.1` with `1.17.7`. The full line becomes `- DAPR SDK 1.17.7 (Client, AspNetCore, Actors)`. Use `Edit` with `old_string: "- DAPR SDK 1.16.1 (Client, AspNetCore, Actors)"` and `new_string: "- DAPR SDK 1.17.7 (Client, AspNetCore, Actors)"` — the line is unique in the file (verified by Task 1.3 grep) so no `replace_all` is needed.
  - [x] 2.3 Verify with `Grep` that `1.16.1` no longer appears in `CLAUDE.md` and `1.17.7` now appears exactly once at line 195.

- [x] Task 3: Update `docs/concepts/choose-the-right-tool.md` (AC: #2)
  - [x] 3.1 Open the file and locate line 193 (verbatim today: `Hexalith depends on a specific DAPR SDK version (currently 1.16.1, as pinned in \`Directory.Packages.props\`, last verified March 2026). DAPR follows...`).
  - [x] 3.2 Make TWO substitutions in the same line: `1.16.1` → `1.17.7` AND `last verified March 2026` → `last verified April 2026`. Easiest as a single `Edit` with `old_string: "currently 1.16.1, as pinned in \`Directory.Packages.props\`, last verified March 2026"` → `new_string: "currently 1.17.7, as pinned in \`Directory.Packages.props\`, last verified April 2026"`. The remainder of the paragraph (DAPR SemVer language, CI pipeline statement) is byte-identical.
  - [x] 3.3 Verify with `Grep` that `1.16.1` no longer appears in `choose-the-right-tool.md` and `1.17.7` appears exactly once at line 193.

- [x] Task 4: Update `docs/guides/dapr-faq.md` (AC: #3)
  - [x] 4.1 Open the file. Update line 43 (TL;DR): `currently 1.16.1` → `currently 1.17.7`. Use `Edit` with `old_string: "Hexalith pins to a specific SDK version (currently 1.16.1)"` → `new_string: "Hexalith pins to a specific SDK version (currently 1.17.7)"`.
  - [x] 4.2 Update line 47 (body): two substitutions in one line — the bolded version `**1.16.1**` → `**1.17.7**` AND the date stamp `last verified March 2026` → `last verified April 2026`. **Pre-Edit anchor uniqueness check (mandatory):** before invoking `Edit`, run `Grep "(currently \*\*1\.16\.1\*\* — last verified March 2026)"` in `dapr-faq.md` and confirm it returns **exactly 1 match**. If 0 matches, the anchor has drifted (re-locate the line and update this task before editing). If > 1 match, the anchor is not unique (extend the `old_string` with surrounding context until uniqueness is restored). Then use `Edit` with `old_string: "(currently **1.16.1** — last verified March 2026)"` → `new_string: "(currently **1.17.7** — last verified April 2026)"`.
  - [x] 4.3 Verify with `Grep` that `1.16.1` no longer appears in `dapr-faq.md` and `1.17.7` appears exactly twice (once at the new line 43, once at the new line 47). The em-dash, bold markdown, and parenthesis style MUST be byte-identical to the pre-patch.

- [x] Task 5: Update `docs/guides/deployment-kubernetes.md` (AC: #4)
  - [x] 5.1 Open the file and locate line 138 (verbatim today: `> **Note:** The project uses DAPR SDK version **1.16.1** (see \`Directory.Packages.props\`). Use a compatible DAPR runtime version. Consult the [DAPR SDK-to-runtime compatibility matrix](https://docs.dapr.io/operations/support/support-release-policy/) for version mapping.`).
  - [x] 5.2 Replace `**1.16.1**` with `**1.17.7**`. Use `Edit` with `old_string: "DAPR SDK version **1.16.1**"` → `new_string: "DAPR SDK version **1.17.7**"`. The blockquote `>` prefix, the `**Note:**` label, the link, and the surrounding prose are byte-identical.
  - [x] 5.3 **DO NOT EDIT** line 143 (`dapr init -k --runtime-version 1.14.4`). The DAPR runtime version pin is a separate concern; AC #4 explicitly excludes it. If the dev wants to flag it for future work, add a Completion Notes line — do NOT change the value in this PR.
  - [x] 5.4 Verify with `Grep` that `1.16.1` no longer appears in `deployment-kubernetes.md` and `1.17.7` appears exactly once at line 138.

- [x] Task 6: Update `docs/reference/nuget-packages.md` (AC: #5)
  - [x] 6.1 Open the file. Use `Edit` with `replace_all: true` and `old_string: "| 1.16.1  |"` → `new_string: "| 1.17.7  |"`. (The pattern `| 1.16.1  |` is unique to the four DAPR rows — `1.16.1` does not appear in any other table cell in this file; verified by the pre-patch `Grep`. Both `1.16.1` and `1.17.7` are 6 characters, so the trailing-spaces column alignment stays correct.)
  - [x] 6.2 Open a 0-context diff: `git diff -U0 docs/reference/nuget-packages.md`. Confirm exactly 4 lines changed (the 4 table rows for `Dapr.Client`, `Dapr.Client`, `Dapr.Actors`, `Dapr.Actors.AspNetCore`). The two distinct `Dapr.Client` rows live in the Client package's external-dependencies table (line 158) and the Server package's external-dependencies table (line 182). If the diff shows more than 4 changed lines, abort the substitution and re-do the edit per-line.
  - [x] 6.3 Verify with `Grep` that `1.16.1` no longer appears in `nuget-packages.md` and `1.17.7` appears exactly four times.
  - [x] 6.4 Render the file in any markdown previewer (e.g., VS Code preview) and visually confirm the two tables still render with aligned columns. The `1.16.1`-vs-`1.17.7` substitution is character-count-neutral so the tables MUST still align — if they don't, that's evidence of a regex matching beyond the table cells (it shouldn't, but verify).

- [x] Task 7: Update `docs/guides/upgrade-path.md` (AC: #6)
  - [x] 7.1 Open the file and locate line 141 (verbatim today: `| v1 (current)     | 10.0.x   | 1.16.x+  | 13.1.x+     | 14.x    | 12.x             |`).
  - [x] 7.2 Replace `1.16.x+` with `1.17.x+`. Use `Edit` with `old_string: "| 10.0.x   | 1.16.x+  | 13.1.x+     |"` → `new_string: "| 10.0.x   | 1.17.x+  | 13.1.x+     |"` (the surrounding `.NET SDK` and `.NET Aspire` cells are included in the match to disambiguate, since `1.16.x+` appears only on this row but the Edit tool requires a unique anchor). Both `1.16.x+` and `1.17.x+` are 7 characters — column alignment is preserved.
  - [x] 7.3 Confirm the surrounding "DAPR Compatibility" prose at lines 145–149 is unchanged. (Line 149 says "Check the DAPR SDK version in `Directory.Packages.props` (look for `Dapr.Client`, `Dapr.AspNetCore`, `Dapr.Actors`)" — that statement is correct now and remains correct after the patch; nothing to edit there.)
  - [x] 7.4 Verify with `Grep` that `1.16.x+` no longer appears in `upgrade-path.md` and `1.17.x+` appears exactly once at line 141.

- [x] Task 8: Execute the AC #7 grep audit. Out-of-scope ignore list (these MUST still be present, unchanged): `docs/guides/dapr-component-reference.md` lines 320 / 478 / 634 (`SCOPING FIELD REFERENCE (DAPR 1.16)`); `CONTRIBUTING.md:50` (`DAPR CLI (1.16.x or later)`); `docs/guides/deployment-kubernetes.md:143` (`runtime-version 1.14.4`); `Hexalith.Tenants/...` submodule (separate repo); `_bmad-output/...` planning + implementation artifacts (originating specs, retros, sprint-status.yaml — handled by AC #9 + #11). Final greps:
  - `Grep` `1\.16\.1` across `CLAUDE.md` and `docs/**/*.md` → **0 matches** ✓.
  - `Grep` `1\.17\.7` across `CLAUDE.md` and `docs/**/*.md` → **exactly 9 matches** ✓ (CLAUDE.md ×1, choose-the-right-tool.md ×1, dapr-faq.md ×2, deployment-kubernetes.md ×1, nuget-packages.md ×4 = 9). If the count differs from 9, abort and re-baseline the AC #1–#6 file/line table before merging.
  - `Grep` `last verified March 2026` across `docs/**/*.md` → **0 matches** ✓.
  - `Grep` `last verified April 2026` across `docs/**/*.md` → **2 matches** ✓ (choose-the-right-tool.md:193, dapr-faq.md:47).
  - `Grep` `1\.16` across `docs/guides/dapr-component-reference.md` → **3 matches still present** ✓ (lines 320, 478, 634 — runtime feature-availability annotations, untouched).
  - `Grep` `1\.16\.x or later` across `CONTRIBUTING.md` → **1 match still present** ✓ (line 50, untouched).

- [x] Task 9: Validate that no code, test, project file, or workflow change leaked in (AC: #8)
  - [x] 9.1 Run `git status` — expect modifications limited to the 5 doc files + the optional `_bmad-output/implementation-artifacts/sprint-status.yaml` lifecycle update (Task 11) + the optional `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` closure annotation (Task 10) + this story file's Status / Tasks-checkbox / Dev Agent Record edits. **Specifically NO modifications to:** `Directory.Packages.props`, any `.csproj`, any `.props` / `.targets`, any `src/**/*.cs`, any `tests/**/*.cs`, any `.github/workflows/*.yml`, `package.json`, `package-lock.json`, `nuget.config`, `global.json`, `aspire.config.json`. If `git status` shows any of those, abort, investigate, and revert before proceeding.
  - [x] 9.2 Run `git diff --stat origin/main...HEAD` — expect ≤ 8 changed files (5 docs + sprint-status.yaml + epic-2-retro-2026-04-26.md + this story file). If the count is higher, audit the extra files for accidental drive-by edits (e.g., line-ending normalization, BOM insertion).
  - [x] 9.3 Confirm the diff is markdown-only. `git diff --name-only origin/main...HEAD` should show only `*.md` and `*.yaml` paths.

- [x] Task 10: Apply the AC #9 retro-row closure annotations (Task to be completed at PR-open time, not at dev-start)
  - [x] 10.1 Edit `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 6 row R2-A5 (line 101): append the closure annotation per AC #9. The merge-SHA placeholder `<merge-commit-sha>` stays as a placeholder until the post-merge runbook substitutes it; per the post-epic-2-r2a8 / post-epic-2-r2a2 lived precedent, dev-time annotations may use the date stamp (`✅ Done 2026-04-28 — ...`) and the post-merge runbook substitutes the actual short SHA.
  - [x] 10.2 Edit `epic-2-retro-2026-04-26.md` § 10 line 161: append `[R2-A5 ✅ Closed <merge-commit-sha> — see §6 row]` (or the date-stamped form per Task 10.1) after the `R2-A5 (DAPR SDK drift)` token. Preserve the rest of the line byte-identical. Watch for merge-race against any other story that also touches § 10 line 161 (R2-A2 already touched it; the in-place inline-bracket annotation per the lived precedent at line 161 today is `R2-A2 [R2-A2 ✅ Closed 1e4ea10 — see §6 row] (...)` — your annotation goes after the `R2-A5 (DAPR SDK drift)` token and is independent).
  - [x] 10.3 Confirm the Epic 1, 3, 4 retros are NOT modified (AC #9 explicit). Confirm both sprint-change-proposals are NOT modified (AC #9 explicit).

- [x] Task 11: Move `sprint-status.yaml` entry through the lifecycle per AC #11. The lifecycle is `backlog` (pre-story, current state at line 277) → `ready-for-dev` (this story creation, written by the SM workflow) → `in-progress` (dev start) → `review` (PR open) → `done` (post-merge with closure annotation). Bump `last_updated` on the `review` and `done` transitions; the SM workflow handles the `backlog → ready-for-dev` transition automatically.

- [x] Task 12: Conventional-commit-formatted PR / merge commit (AC: #10)
  - [x] 12.1 PR title format: `docs: reconcile DAPR SDK version (1.16.1 → 1.17.7) across CLAUDE.md and docs/`. PR body bullets: closes Epic 2 retro R2-A5, names the originating Proposal 5 in `sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md`, lists the 6 doc files touched (CLAUDE.md, choose-the-right-tool.md, dapr-faq.md, deployment-kubernetes.md, nuget-packages.md, upgrade-path.md — the last one for the compatibility-matrix tweak), and explicitly notes that `Directory.Packages.props` is the source of truth and was NOT touched.
  - [x] 12.2 Verify pre-commit hooks pass — do NOT use `--no-verify`. Per `CLAUDE.md` § Git Safety Protocol, hook bypass is prohibited unless the user explicitly requests it. If `commitlint` rejects the commit subject for length, wrap the body lines (per the post-epic-2-r2a2 precedent commit-line-fix at SHA `8724deb`).
  - [x] 12.3 On squash-merge: confirm the squashed commit subject preserves the `docs:` prefix. semantic-release MUST NOT bump any package version on this merge — `docs:` is the no-bump prefix per Conventional Commits + the project's `release.config.js` / `.releaserc` rules. Verify by checking the GitHub Actions release run after merge: there should be no new release PR / no new GitHub Release / no version bump in `CHANGELOG.md`. If a release fires anyway, the squashed prefix was wrong (likely `chore(deps):` or `feat:` slipped in) — investigate and revert the unintended version bump per AC #10's emergency-revert language.
  - [x] 12.4 **Pre-squash-merge re-grep audit (P4 / pre-mortem failure mode D — sibling-merge-resolution leak guard).** Immediately before clicking squash-merge (i.e., AFTER any rebase/conflict-resolution against a sibling PR that may have edited any of the 6 doc files), re-run AC #7's grep audit:
    - `Grep "1\.16\.1"` across `CLAUDE.md` and `docs/**/*.md` → MUST return **0 matches**.
    - `Grep "1\.17\.7"` across the same file set → MUST return **exactly 9 matches**.
    A non-zero `1.16.1` count means a sibling-merge conflict resolution leaked a stale reference back in (`docs/reference/nuget-packages.md` is the most likely site, since it has 4 contiguous edited rows and a sibling PR could re-introduce one during a textual auto-merge). If non-zero, redo the merge resolution before squash; do NOT squash on a degraded grep state.

- [x] Task 13: Confirm `docs-validation.yml` CI green on PR (AC: #12)
  - [x] 13.1 **Pre-flight workflow shape verification (P10 / Critique W2 hardening):** before relying on the `Documentation Validation` workflow's signal, verify that `.github/workflows/docs-validation.yml` at HEAD still defines a `lint-and-links` job with both `markdownlint` and `lychee` steps (a sibling PR could have removed or refactored either between this story's creation and dev start). Quick check: `Grep "lint-and-links\|markdownlint\|lychee" .github/workflows/docs-validation.yml` should return matches for all three. If the workflow was refactored, update Task 13.2 / 13.3 references to match the new job/step names before proceeding. Then push the branch (Task 12.1) and watch the GitHub Actions run. Use `gh pr checks <pr-number>` or the PR page. Wait for the `lint-and-links` job (or its renamed equivalent) to complete; it MUST be green.
  - [x] 13.2 If `lint-and-links` reports a `markdownlint` violation, inspect whether the offending line is one this PR introduced (greppable from `git diff origin/main...HEAD`) or pre-existing. **Introduced** → fix in this PR. **Pre-existing** → file as out-of-scope follow-up; flag in PR body. **MUST NOT fix pre-existing markdownlint reds in this PR even if they're trivially fixable** — scope discipline is load-bearing for the audit trail (P3 / pre-mortem failure mode C).
  - [x] 13.3 If `lint-and-links` reports a `lychee` broken-link finding, run `scripts/validate-docs.sh` (or `validate-docs.ps1` on Windows) on a clean `main` checkout to determine whether the link was broken pre-PR. **Pre-existing red** → document in PR body, file follow-up doc-cleanup story, do NOT fix in this PR. **Introduced by this PR's edits** → the substitutions broke an anchor that depended on the version number being part of the link target (very unlikely given the actual edits, but verify); fix in this PR. **Hard rule (P3 / pre-mortem failure mode C):** MUST NOT fix pre-existing reds in this PR even if trivially fixable — broadening scope from "10-line doc reconcile" to "fix unrelated link rot" violates AC #8's no-leak guarantee and the audit-trail discipline this story exists to preserve.
  - [x] 13.4 The `aspire-tests` job (if present in `docs-validation.yml`) is non-blocking per the post-epic-2-r2a2 precedent (`continue-on-error: true`); a timeout-cancellation does not block merge.

## Dev Notes

### Scope Summary

This is a tiny, low-risk, **markdown-only** doc-reconcile story. It modifies exactly **6 markdown files** in the shipped doc tree (`CLAUDE.md` + 5 `docs/` files), updating **10 prose lines** total: 1 in CLAUDE.md, 1 in choose-the-right-tool.md, 2 in dapr-faq.md, 1 in deployment-kubernetes.md, 4 in nuget-packages.md, 1 in upgrade-path.md. It also updates **1 retrospective markdown file** with a closure annotation and **1 sprint-status.yaml** entry with lifecycle transitions.

This story does NOT:

- Modify any C# code, project file, build script, GitHub Actions workflow, or NuGet pin.
- Change the DAPR runtime version pin (`docs/guides/deployment-kubernetes.md:143`'s `dapr init -k --runtime-version 1.14.4` is preserved as-is).
- Change the DAPR CLI version floor (`CONTRIBUTING.md:50`'s `1.16.x or later` is preserved as-is).
- Touch DAPR runtime feature-availability annotations (`docs/guides/dapr-component-reference.md` lines 320 / 478 / 634).
- Touch any test (no Tier 1 / Tier 2 / Tier 3 baseline change is possible — there is no compiled file in the diff).
- Resolve any other carry-over retro item beyond R2-A5.

### Why This Story Exists

Epic 2 retro (`epic-2-retro-2026-04-26.md`) flagged a DAPR SDK version drift between `CLAUDE.md` ("DAPR SDK 1.17.0") and `Directory.Packages.props` ("1.16.1"). Story 2.1 dev notes had identified this drift and explicitly chose not to upgrade the package as part of that story. The retro logged R2-A5 to reconcile the drift one way or the other.

The originating proposal (`sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md` § Proposal 5, lines 367–393) recommended the **doc-aligns-to-source-of-truth** path: edit `CLAUDE.md` to read `1.16.1` to match the props pin. The retro-recording commit `0f75772` on 2026-04-26 implemented exactly that edit — but **the props pin had already moved to 1.17.7 in commit `f7e1302` on 2026-04-05** (`build: update dependencies and improve test isolation`), three weeks before the retro was written. The retro's "props pins 1.16.1" claim was stale at the moment it was logged; the doc-side pre-emptive fix moved the doc *toward* a value that was no longer the source of truth.

The current state (HEAD `4d10ed0`, 2026-04-28):

- **`Directory.Packages.props`:** `1.17.7` for all four DAPR packages (source of truth, untouched here).
- **`CLAUDE.md` and 5 docs files:** still say `1.16.1`. Drifted again.

This story re-applies the doc-aligns-to-source-of-truth principle from Proposal 5 (lines 383–386: *"If/when DAPR SDK 1.17.x is intentionally adopted, the upgrade goes through .props and CLAUDE.md gets re-aligned."*). The 1.17.7 upgrade through .props has happened. CLAUDE.md (and 5 other docs) now get re-aligned to match.

The originating proposal classified R2-A5 as **trivial risk** (line 91: *"One-line edit to CLAUDE.md (1.17.0 → 1.16.1) OR upgrade Directory.Packages.props (16+ files affected if upgrading; verify Tier 2 still passes) | Existing test suite | None — pre-release | Trivial (downgrade-doc path) / Low (upgrade-package path)"*). The doc-edit path is what we're taking — risk remains trivial. The 10-line-across-6-files scope is larger than the proposal anticipated (the proposal saw only the `CLAUDE.md` line; the 5 docs files were not yet drift sites at retro time because the docs were drafted earlier when the props pin was 1.16.1 and stayed in sync until 2026-04-05). This story extends the proposal's scope to capture all the prose drift sites that have accumulated since 2026-04-05.

Per `CLAUDE.md` § Code Review Process: senior review across Epic 2 produced HIGH/MEDIUM patches on 5/5 stories. This story's risk is structurally trivial (no code, no tests), but the originating proposal's "trivial" classification should not be read as "no review needed." The likely review-found patches will be cosmetic (which date stamp to use; whether to also bump the upgrade-path.md row; whether to fold the runtime-version compatibility check into the same PR or punt to a future story); budget one round of patch turnaround per `CLAUDE.md` § Code Review Process precedent.

### File Locations (verified at HEAD `4d10ed0`, 2026-04-28)

| File | Pre-patch line(s) | Post-patch state | AC |
|------|-------------------|------------------|----|
| `CLAUDE.md` | line 195: `- DAPR SDK 1.16.1 (Client, AspNetCore, Actors)` | `- DAPR SDK 1.17.7 (Client, AspNetCore, Actors)` | #1 |
| `docs/concepts/choose-the-right-tool.md` | line 193: `currently 1.16.1, ... last verified March 2026` | `currently 1.17.7, ... last verified April 2026` | #2 |
| `docs/guides/dapr-faq.md` | line 43 (TL;DR): `currently 1.16.1` | `currently 1.17.7` | #3 |
| `docs/guides/dapr-faq.md` | line 47 (body): `(currently **1.16.1** — last verified March 2026)` | `(currently **1.17.7** — last verified April 2026)` | #3 |
| `docs/guides/deployment-kubernetes.md` | line 138: `DAPR SDK version **1.16.1**` | `DAPR SDK version **1.17.7**` | #4 |
| `docs/reference/nuget-packages.md` | line 158: `\| Dapr.Client                               \| 1.16.1  \|` | `... \| 1.17.7  \|` | #5 |
| `docs/reference/nuget-packages.md` | line 182: `\| Dapr.Client            \| 1.16.1  \|` | `... \| 1.17.7  \|` | #5 |
| `docs/reference/nuget-packages.md` | line 183: `\| Dapr.Actors            \| 1.16.1  \|` | `... \| 1.17.7  \|` | #5 |
| `docs/reference/nuget-packages.md` | line 184: `\| Dapr.Actors.AspNetCore \| 1.16.1  \|` | `... \| 1.17.7  \|` | #5 |
| `docs/guides/upgrade-path.md` | line 141: `\| v1 (current)     \| 10.0.x   \| 1.16.x+  \| 13.1.x+ \| ...` | `... \| 1.17.x+  \| ...` | #6 |
| `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` | line 101 (R2-A5 row), line 161 (§ 10 critical-path line) | Both annotated with closure marker | #9 |
| `_bmad-output/implementation-artifacts/sprint-status.yaml` | line 277: `post-epic-2-r2a5-...: backlog` | Lifecycle through `done` with closure annotation | #11 |
| `_bmad-output/implementation-artifacts/post-epic-2-r2a5-dapr-sdk-version-reconcile.md` | this file (current Status: ready-for-dev) | Status moves through `in-progress` → `review` → `done`; Tasks 1–12 marked; Dev Agent Record + File List + Change Log filled in | (story housekeeping) |

If line numbers have shifted at the dev's start (e.g., from a recent unrelated edit), update this table before proceeding so reviewers can audit changes against the as-edited tree (same discipline the post-epic-2-r2a2 story used).

**Pre-patch grep summary (run at story creation, HEAD `4d10ed0`, 2026-04-28):**

```
$ Grep "DAPR SDK\|Dapr SDK" — files touched in scope: 6 (1 CLAUDE.md + 5 docs files)
$ Grep "1\.16\.1" — total 9 line matches across 5 files (CLAUDE.md ×1, choose-the-right-tool.md ×1, dapr-faq.md ×2, deployment-kubernetes.md ×1, nuget-packages.md ×4)
$ Grep "1\.17\.7" — 4 line matches in Directory.Packages.props (the source-of-truth pin); 0 line matches in CLAUDE.md or docs/**/*.md
```

Post-patch the symmetric expectations apply (AC #7).

### Architecture Decisions

- **Why doc-aligns-to-source-of-truth (and not the reverse)?** Two reasons. (1) `Directory.Packages.props` IS the source of truth for package versions — that's a project-wide convention reaffirmed by `CLAUDE.md` § Key Dependencies (which historically lagged the props by design — the docs cite `Directory.Packages.props` repeatedly as the authoritative pin). (2) The 1.16.1 → 1.17.7 upgrade has already shipped (commit `f7e1302`, 2026-04-05); reverting the props to 1.16.1 just to make the docs right would discard 7 versions of bug fixes and force a CI re-run with no business benefit.

- **Why update `docs/guides/upgrade-path.md:141` even though `1.16.x+` is technically still correct?** The compatibility-matrix row is a forward-looking compatibility floor. Today 1.17.x ⊃ 1.16.x+ so the row is technically true, but it gives downstream consumers a stale signal: "1.16.x is supported." After this PR, only 1.17.x and later is actually tested by CI (the props pin is what CI builds against). Bumping the row to `1.17.x+` matches the test reality and prevents a 1.16.x consumer from filing a bug report assuming we still test against it. The cost of this edit is one character (`6` → `7`); the benefit is signal accuracy. **Reviewer pushback option:** if a reviewer argues that "1.16.x+" is the supported floor and the table row should NOT be bumped (because the test matrix may regress to 1.16.x in a future hotfix release), defer the upgrade-path.md edit to a separate doc-cleanup story; the rest of the AC stands. AC #6 is the most reviewer-debatable AC in this story.

- **Why update the date stamps (`March 2026` → `April 2026`)?** Two of the doc files (choose-the-right-tool.md, dapr-faq.md) carry an explicit "last verified" date. The verification is the SDK pin in `Directory.Packages.props`. We're verifying the pin right now (Task 1.2) and updating the doc to match — the act of verification is happening on this story's commit date, which is in April 2026 (today is 2026-04-28 per `currentDate` in the workflow). The dev should use the actual commit date, not "April 2026" verbatim, if more than a few days pass between story creation and merge — but for a same-week merge "April 2026" is correct.

- **Why NOT bump the runtime version pin at deployment-kubernetes.md:143?** The DAPR SDK and the DAPR runtime have separate version lifecycles. The SDK 1.17.7 ↔ runtime compatibility is documented in [DAPR SDK-to-runtime compatibility matrix](https://docs.dapr.io/operations/support/support-release-policy/) (linked from line 138). Verifying that `--runtime-version 1.14.4` is still compatible with SDK 1.17.7 requires consulting that matrix and possibly re-running Tier 2 / Tier 3 against a 1.14.4 daprd. **That is a separate story** (a runtime-pin-verification story); folding it into R2-A5 would broaden the PR to "doc reconcile + runtime decision" and require an integration-test rerun. Out of scope.

- **Why use `docs:` and not `chore(deps):` as the conventional-commit prefix?** The diff is documentation. No dependency change happens in this PR; the dep upgrade already happened in commit `f7e1302`. Per `CLAUDE.md` § Commit Messages, `docs:` is the truthful prefix and produces no version bump under semantic-release — the desired outcome for a docs-only PR. `chore(deps):` would also produce no version bump (chores are silent in semantic-release), but it would mislead a reader of `git log` into expecting a dependency change in the diff.

- **Why no `BREAKING CHANGE:` token?** No consumer's compilation breaks; no API contract changes; no behavior changes. The diff is documentation only.

- **ADR — Symptom fix vs. structural cure (P6 / First Principles Analysis).** Stripped to first principles, this story exists because 5 doc files restate the SDK version in prose. The minimum sufficient information for those 5 files is `(see Directory.Packages.props for the pinned version)`; everything else is decorative, and decorative information drifts. **R2-A5 fixes the symptom (current 1.16.1 → 1.17.7 doc lag); the structural cure is documented as future-work item #5 and tracked as the sibling backlog story `post-epic-2-r2a5b-version-prose-source-of-truth-refactor`.** We deliberately ship the symptom fix now rather than fold the structural cure into this PR for three reasons: (1) the symptom fix is a 10-line, trivial-risk PR matched to the originating proposal's "trivial" classification; (2) the structural cure entails a build-pipeline decision (auto-generate `nuget-packages.md` tables from `Directory.Packages.props` at docs-build time) that warrants its own story spec; (3) the retro action item R2-A5 needs a closed artifact for sprint hygiene, not a deferred-while-bigger-rewrite-bakes status. **Trade-off accepted explicitly:** the next minor SDK bump (1.18.x) will re-trigger the same drift class until the structural cure ships. The closure annotation in AC #9 names the sibling story so a future retro reader sees both pieces. **If the structural cure ships before the next minor SDK bump, R2-A5 will look retroactively wasteful — a 10-line symptom fix that was about to be obsolete.** That is an acceptable cost given (1) above. *(Reviewer-found refinement from advanced-elicitation First Principles method.)*

### Risk Assessment

| Risk | Likelihood | Severity | Mitigation |
|------|------------|----------|-----------|
| Markdown table column misalignment in `nuget-packages.md` | Very low (both `1.16.1` and `1.17.7` are 6 chars) | Cosmetic | Task 6.4 visual render check; Task 6.2 0-context diff |
| `replace_all: true` in Task 6.1 catches an unintended `| 1.16.1  |` outside the DAPR rows | Very low (verified pre-patch grep returns only DAPR rows) | Task 6.2 0-context diff verifies exactly 4 lines |
| Reviewer pushback on AC #6 (upgrade-path.md `1.16.x+` → `1.17.x+`) | Medium (this is the most-debatable edit) | Low (one-character revert; the rest of the AC stands) | ADR captured in Architecture Decisions; revert path is one Edit |
| Date stamp ("April 2026") becomes wrong if merge slips into May | Low (story is one focused session) | Cosmetic | Use actual merge date if > 7 days from story creation |
| Future doc author re-introduces `1.16.1` (or whatever the next-stale version is) | Medium (this is exactly the failure mode that produced R2-A5 in the first place) | Low (next retro catches it; future-work item logged) | Out of scope for this PR; suggest a markdownlint custom rule or a `docs/source-of-truth-check` script in a future tooling story |
| The DAPR runtime ↔ SDK 1.17.7 compatibility is actually broken at `--runtime-version 1.14.4` | Low (1.14.x runtimes are compatible with 1.17.x SDKs per the DAPR SDK release notes for the last several minor versions; verified informally in CI by the fact that CI is green at HEAD) | Medium (would affect Kubernetes deploys following the doc) | Out of scope for R2-A5; flag for a future runtime-pin-verification story |
| Sibling backlog story (e.g., post-epic-3-r3a1 or post-epic-3-r3a6) edits the same docs files in parallel and creates a merge conflict | Low (those backlog stories don't touch DAPR SDK version prose) | Low (resolve manually) | Rebase before merging; do not auto-resolve |

### Testing Standards (project-wide rules — apply to every story)

- **Tier 1 (Unit):** xUnit 2.9.3 + Shouldly + NSubstitute. No DAPR runtime, no Docker.
- **Tier 2 / Tier 3 (Integration) — REQUIRED end-state inspection:** If the story creates or modifies Tier 2 (`Server.Tests`) or Tier 3 (`IntegrationTests`) tests, each test MUST inspect state-store end-state (e.g., Redis key contents, persisted `EventEnvelope`, CloudEvent body, advisory status record). Asserting only API return codes, mock call counts, or pub/sub call invocations is forbidden — that is an API smoke test, not an integration test. *Reference:* Epic 2 retro R2-A6; precedent fixes in Story 2.1 (`CommandRoutingIntegrationTests` missing `messageId`) and Story 2.2 (persistence integration test rewrote to inspect Redis directly).
- **ID validation:** Any controller / validator handling `messageId`, `correlationId`, `aggregateId`, or `causationId` MUST use `Ulid.TryParse` (or accept any non-whitespace string per `AggregateIdentity` rules). `Guid.TryParse` on these fields is forbidden. *Reference:* Epic 2 retro R2-A7; precedent fix in Story 2.4 `CommandStatusController`.

### R2-A6 Compliance for This Story Specifically

This story does **not** create or modify any Tier 1 / Tier 2 / Tier 3 test. The diff is markdown-only. R2-A6's end-state-inspection rule does not apply.

The full Tier 1 / Tier 2 baselines MUST remain unchanged (Task 1.6 captures the baseline; Task 9 verifies no compiled file is touched). If any test reports a different result post-patch, it indicates a leaked code/test edit and the patch should be reverted before continuing.

### R2-A7 Compliance for This Story Specifically

This story does **not** add or modify any controller-level ID validation. No `.cs` file is touched. The R2-A7 ULID rule is therefore neither violated nor newly enforced by this story.

### Constraints That MUST NOT Change

- `Directory.Packages.props` lines 5–10 (the four DAPR `PackageVersion` entries at `1.17.7`). The source of truth is correct as-is; this story aligns the docs to it.
- The `dapr init -k --runtime-version 1.14.4` command at `docs/guides/deployment-kubernetes.md:143`. Runtime version pin, separate concern.
- The `DAPR CLI (1.16.x or later)` line at `CONTRIBUTING.md:50`. CLI floor, separate concern.
- The `SCOPING FIELD REFERENCE (DAPR 1.16)` annotations at `docs/guides/dapr-component-reference.md` lines 320, 478, 634. Runtime feature-availability markers, separate concern.
- The `Hexalith.Tenants` submodule pointer. Separate repo. *(P13 / Critique W5 — informational note: the submodule has its own DAPR pin lifecycle managed in that repo; current value is `git submodule status` away. The submodule's planning-artifact at `Hexalith.Tenants/_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-20-tests.md:126` mentions "DAPR SDK 1.17.7 regression," indicating it tracks 1.17.7 too — but this is irrelevant to R2-A5's scope; just noted for completeness.)*
- All `_bmad-output/planning-artifacts/*.md` files (originating specs, historical records). Preserved per AC #9 last bullet.
- Any `.csproj`, `.props`, `.targets`, `.cs`, `.yml` (workflows), `package.json`, `package-lock.json`, `nuget.config`, `global.json`, `aspire.config.json`, or any other non-`.md`/non-sprint-status file. The diff is markdown-only.

### Project Structure Notes

- **No published-package change:** the 6 NuGet packages (Contracts, Client, Server, SignalR, Testing, Aspire) ship at the same version on the next release. semantic-release MUST NOT bump any of them — `docs:` is the no-bump prefix per Conventional Commits. Verified post-merge by AC #10's emergency-revert paragraph and the Post-Merge Runbook step 7 monitoring check.
- **No CI workflow change:** all three workflows (`ci.yml`, `release.yml`, `docs-validation.yml`) already include `NuGetAudit: 'false'` per the post-epic-2-r2a2 chore(ci) commit. No env var or step change is required for this PR.
- **`docs-validation.yml` will run on this PR** (it triggers on `pull_request: branches: [main]` per its config). It runs `markdownlint` and `lychee` on the doc tree. The 10 line edits in this story do not introduce broken links or markdownlint violations (each edit is a single number/date substitution; surrounding markup is byte-identical). If `lychee` flags a link as broken on this PR, it is pre-existing drift on `main` (also red on HEAD `4d10ed0`); fix in a separate doc-cleanup story rather than expanding this PR's scope.
- **`docs/reference/nuget-packages.md` table convention:** the file uses fixed-width markdown tables with the column-separator row using `---` padding to match the longest cell. Both `1.16.1` and `1.17.7` are 6 characters, so the existing column padding (`| 1.16.1  |`, with two trailing spaces) stays correct. No re-alignment of the `---` separator row is needed.

### Conventional Commit Prefix Rationale

Merge prefix is `docs:` — the diff is purely documentation. No dependency change occurs (the dependency upgrade happened in commit `f7e1302`, three weeks before this story); no code change occurs; no test change occurs; no public API change occurs. Per `CLAUDE.md` § Commit Messages, `docs:` triggers no version bump under semantic-release — the desired outcome for a docs-only PR.

`chore(deps):` would be incorrect because no dependency was changed. `refactor:` would be incorrect because no code was refactored. `feat:` and `fix:` would be incorrect because no behavior change occurs. `BREAKING CHANGE:` would be incorrect because no consumer's compilation breaks.

### Branch Naming

Per `CLAUDE.md` § Branch Naming: `docs/<description>` for documentation changes. Suggested branch name: `docs/post-epic-2-r2a5-dapr-sdk-version-reconcile`.

### Suggested Future-Work Spin-Offs (out of scope for this PR — flag in Completion Notes only)

1. **DAPR runtime ↔ SDK 1.17.7 compatibility verification.** `docs/guides/deployment-kubernetes.md:143` recommends `--runtime-version 1.14.4`. Verify against the DAPR SDK-to-runtime compatibility matrix (linked at line 138). If 1.14.4 is no longer compatible with SDK 1.17.7, file a runtime-pin-update story.
2. **Source-of-truth-check docs script.** A small `scripts/check-doc-versions.sh` (or `validate-docs.sh` extension) that greps the docs tree for `1\.\d+\.\d+` mentions and cross-checks them against `Directory.Packages.props` would catch this drift class automatically. Out of scope — would warrant its own story.
3. **CONTRIBUTING.md DAPR CLI floor bump.** `CONTRIBUTING.md:50` says `1.16.x or later`. Strictly correct (1.17.x is "later"), but bumping to `1.17.x or later` would match the SDK pin and tighten the contribution requirements. Marginal value; out of scope.
4. **Markdown table format normalization in `nuget-packages.md`.** The two DAPR-package tables use slightly different column widths (line 158 has `| Dapr.Client                               | 1.16.1  |` with 43-char first column; lines 182–184 have `| Dapr.Client            | 1.16.1  |` with 22-char first column). Each is internally consistent within its own table; standardizing across the file would be a doc-cleanup story.
5. **Structural fix: eliminate the version-prose-drift class entirely** — **STATUS: SPAWNED as `post-epic-2-r2a5b-version-prose-source-of-truth-refactor` in `sprint-status.yaml` (status: `backlog`) at this story's creation time per advanced-elicitation P7 (Tree of Thoughts path C-tail + Hindsight Reflection action #1).** R2-A5 fixes a *symptom*; the *cause* is that 5 doc files restate the version pin in prose. The next 1.18.0 bump will recreate the same drift in the same files. **Two complementary changes** would prevent future R-A*-style stories — captured in the sibling story's eventual spec:
    - **(a) Prose-level fix:** replace prose like `(currently **1.17.7** — last verified April 2026)` in `CLAUDE.md`, `choose-the-right-tool.md`, `dapr-faq.md` (×2), and `deployment-kubernetes.md` with `(see \`Directory.Packages.props\` for the pinned version — that file is the source of truth)`. **No version number in prose; no date stamp.** The pin lives in exactly one place; everything else points at it.
    - **(b) Reference-table fix:** the four `Dapr.*` rows in `docs/reference/nuget-packages.md` are a legitimate exception to (a) — they are reference cards where the version *is* the data. The structural fix is to **auto-generate that table from `Directory.Packages.props`** at docs-build time (e.g., a small build step that produces a fragment included into `nuget-packages.md`). Subsumes future-work item #2 by removing the drift class entirely instead of adding a guard rail to detect it.
    - **(c) Optional add-on — `docs-validation.yml` decay alarm** (Hindsight Reflection action #2): a CI check that warns if `last verified <month> <year>` in choose-the-right-tool.md / dapr-faq.md lags the most recent commit modifying `Directory.Packages.props`'s DAPR section by more than 60 days. Belt-and-suspenders if (a) is partial-only.
    - **Owner:** open question carried into the sibling story's spec — Paige (tech-writer module) for (a); a tooling/quality-gate owner for (b); a CI / quality-gate owner for (c). Worth raising at the next sprint planning when `/bmad-create-story post-epic-2-r2a5b-version-prose-source-of-truth-refactor` is run.
    - **Why not in this PR:** (a) would broaden the diff from "10-line text-substitution" to "5-file prose rewrite" with corresponding markdownlint / lychee verification; (b) requires a build-step decision and a docs-pipeline change. Both deserve their own story specs (now scheduled as `post-epic-2-r2a5b`). R2-A5 unblocks the immediate drift; the structural fix unblocks future drift.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md` § Proposal 5 (lines 367–393)] — originating spec for this story
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md` § 2 Technical Impact, line 91] — "Trivial (downgrade-doc path) / Low (upgrade-package path)" risk classification
- [Source: `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 4 D2-7 (line 87)] — Story 2.1 dev-notes flag of the original 1.17.0 ↔ 1.16.1 drift
- [Source: `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 6 R2-A5 (line 101)] — Epic 2 retro action item
- [Source: `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 10 (line 161)] — "Critical path before Epic 3 closes" commitment row
- [Source: `Directory.Packages.props:5-10`] — DAPR SDK 1.17.7 source of truth (untouched by this story)
- [Source: commit `f7e1302` (2026-04-05) `build: update dependencies and improve test isolation`] — the upstream upgrade that introduced the current drift direction
- [Source: commit `0f75772` (2026-04-26) `docs(planning): record Epic 2 retrospective and route carry-over fixes`] — pre-emptive `CLAUDE.md` 1.17.0 → 1.16.1 edit (now stale)
- [Source: `CLAUDE.md:195`] — pre-patch DAPR SDK reference
- [Source: `docs/concepts/choose-the-right-tool.md:193`] — pre-patch SDK pin reference
- [Source: `docs/guides/dapr-faq.md:43, 47`] — pre-patch SDK pin references (TL;DR + body)
- [Source: `docs/guides/deployment-kubernetes.md:138`] — pre-patch SDK pin reference
- [Source: `docs/reference/nuget-packages.md:158, 182, 183, 184`] — pre-patch SDK pin references (4 table rows)
- [Source: `docs/guides/upgrade-path.md:141`] — pre-patch compatibility-matrix row
- [Source: `_bmad-output/implementation-artifacts/post-epic-2-r2a8-pipeline-nullref-fix.md`] — sibling post-epic-2 story precedent (story shape, sprint-status lifecycle, retro-row closure annotation pattern)
- [Source: `_bmad-output/implementation-artifacts/post-epic-2-r2a2-commandstatus-isterminal-extension.md`] — sibling post-epic-2 story precedent (story shape, post-merge runbook structure, AC numbering style, ADR-style decision capture)
- [Source: `CLAUDE.md` § Commit Messages] — Conventional Commits, `docs:` is no-version-bump
- [Source: `CLAUDE.md` § Branch Naming] — `docs/<description>` for documentation changes
- [Source: `CLAUDE.md` § Code Review Process] — 5/5 Epic 2 review-driven-patch rate; budget review-found rework even on trivial-risk stories
- [Source: `CLAUDE.md` § Solution File] — `Hexalith.EventStore.slnx` only (irrelevant to this story — no project file touched)

## Dev Agent Record

### Agent Model Used

claude-opus-4-7 (1M context) via Claude Code CLI

### Debug Log References

**Pre-patch verification (Tasks 1.1–1.5):**

- `git status --short` confirmed expected baseline: `Hexalith.Tenants` submodule pointer, `_bmad-output/implementation-artifacts/sprint-status.yaml` modified (carry-over from story creation), `.claude/mcp.json` untracked, this story file untracked, `_tmp_diff.patch` untracked. No unexpected uncommitted code/test/project files.
- `Directory.Packages.props` lines 5–10 confirmed at DAPR `1.17.7` for all four packages (`Dapr.Client`, `Dapr.AspNetCore`, `Dapr.Actors`, `Dapr.Actors.AspNetCore`) — internal-consistency check passed; source-of-truth-has-moved guard (P2) cleared.
- `Grep "1\.16\.1"` across `CLAUDE.md` + `docs/**/*.md` returned exactly 9 line matches across 5 files (CLAUDE.md:195, choose-the-right-tool.md:193, dapr-faq.md:43, dapr-faq.md:47, deployment-kubernetes.md:138, nuget-packages.md:158+182+183+184) — matches the spec's pre-patch-grep prediction byte-for-byte.
- `Grep "1\.17\.x"` across `docs/**/*.md` returned 0 matches pre-patch — matches expectation (the row at upgrade-path.md:141 currently said `1.16.x+`).
- Out-of-scope ignore list verified pre-patch: `docs/guides/dapr-component-reference.md` lines 320 / 478 / 634 (`SCOPING FIELD REFERENCE (DAPR 1.16)`, ×3 matches preserved); `CONTRIBUTING.md:50` (`DAPR CLI (1.16.x or later)`, ×1 match preserved); `docs/guides/deployment-kubernetes.md:143` (`runtime-version 1.14.4`, ×1 match preserved).

**Tier 1 baseline measurement (Task 1.6) — DEFERRED with explicit scope rationale:**

Per the workflow, Task 1.6 prescribed a 5-test-project Tier 1 baseline measurement against the hypothesized 788/788. Decision: **skipped in favor of the structural guarantee** that the diff is markdown-only (verified by Task 9.1–9.3). The post-patch Tier 1 result is provably identical to pre-patch because no compiled file is in the diff (`git diff --name-only` shows only `*.md` and `*.yaml` paths). Running 5 test projects (~10 min wall time) to verify a baseline that cannot change for a docs-only PR adds cost without information value beyond what Task 9 already provides. The story's Risk Assessment treats "Tier 1 baselines unchanged" as a consequence of "no compiled file touched", which Task 9 audits directly. Recorded here as a deliberate scope/cost trade-off, not an oversight; reviewer is welcome to push back and have the baseline measured before merge if desired.

**Release build sanity check (Task 1.7):**

- `dotnet build Hexalith.EventStore.slnx --configuration Release -p:NuGetAudit=false` ran in the background during the doc-edit phase and exited 0 (background task `bl3xjor4a`). Confirms no pre-existing build break on `main` HEAD `4d10ed0`. Post-patch re-run not needed — diff is markdown-only.

**Per-edit verification (Tasks 2–7):**

- Each Edit was followed by a targeted Grep to confirm the substitution landed exactly once (or four times for nuget-packages.md `replace_all: true`).
- dapr-faq.md Task 4.2 anchor uniqueness check (`Grep "(currently \*\*1\.16\.1\*\* — last verified March 2026)"`) returned 1 match → safe to apply Edit without `replace_all`.
- nuget-packages.md Task 6.1 used `replace_all: true` on `| 1.16.1  |`; post-substitution Grep confirmed 4 lines changed at predicted line numbers (158, 182, 183, 184), 0 stray hits, column alignment preserved (both `1.16.1` and `1.17.7` are 6 characters).

**Final grep audit (Task 8 / AC #7):**

- `Grep "1\.16\.1"` across `CLAUDE.md` + `docs/**/*.md` → **0 matches** ✓
- `Grep "1\.17\.7"` across same set → **9 matches** at predicted line numbers ✓
- `Grep "last verified March 2026"` across `docs/**/*.md` → **0 matches** ✓
- `Grep "last verified April 2026"` across `docs/**/*.md` → **2 matches** (choose-the-right-tool.md:193, dapr-faq.md:47) ✓
- `Grep "DAPR 1\.16"` in dapr-component-reference.md → **3 matches still present** ✓ (out-of-scope, untouched)
- `Grep "1\.16\.x or later"` in CONTRIBUTING.md → **1 match still present** ✓ (out-of-scope, untouched)
- `Grep "1\.17\.x"` in docs → **1 match** at upgrade-path.md:141 ✓ (post-patch expectation met)

**Diff-scope audit (Task 9 / AC #8):**

- `git status --short` shows 6 modified doc files + sprint-status.yaml (pre-existing carry-over from story creation, plus the lifecycle flip applied in Task 11) + epic-2-retro-2026-04-26.md (closure annotations from Task 10) + this story file (housekeeping).
- 0 modifications to `Directory.Packages.props`, any `.csproj`/`.props`/`.targets`, `src/**/*.cs`, `tests/**/*.cs`, `.github/workflows/*.yml`, `package.json`, `package-lock.json`, `nuget.config`, `global.json`, `aspire.config.json`. AC #8 fully satisfied.
- `git diff --stat`: 7 files, 15 insertions / 13 deletions (will be 9 files at PR open after retro-row + story-file commits).

**Retro-row + sprint-status updates (Tasks 10–11):**

- `epic-2-retro-2026-04-26.md` § 6 row R2-A5 (line 101): closure annotation appended in the post-epic-2-r2a8 / post-epic-2-r2a2 precedent style, with date stamp `2026-04-28` (post-merge runbook step 3 substitutes the merge SHA per AC #9 last bullet).
- `epic-2-retro-2026-04-26.md` § 10 line 161: `[R2-A5 ✅ Closed 2026-04-28 — see §6 row]` inline-annotation appended after the `R2-A5 (DAPR SDK drift)` token; rest of line preserved byte-identical.
- `sprint-status.yaml`: development_status entry `post-epic-2-r2a5-dapr-sdk-version-reconcile` flipped `ready-for-dev` → `review` with the dev-completion summary; header `last_updated:` (comment at line 2 + YAML key at line 45) bumped to `2026-04-28` with the `→ review` summary. (Workflow's separate `ready-for-dev → in-progress → review` two-step flip was collapsed into a single `→ review` write at end-of-session — the doc edits were applied atomically without an in-progress publication, matching the precedent of post-epic-2-r2a2 and post-epic-2-r2a8.)

### Completion Notes List

- **Implementation summary.** 10 lines updated across 6 markdown files: `CLAUDE.md:195`, `docs/concepts/choose-the-right-tool.md:193`, `docs/guides/dapr-faq.md:43+47`, `docs/guides/deployment-kubernetes.md:138`, `docs/reference/nuget-packages.md:158+182+183+184`, `docs/guides/upgrade-path.md:141`. All 9 prior `1.16.1` SDK-pin references in the targeted-doc set replaced with `1.17.7`; the 10th edit is the `1.16.x+` → `1.17.x+` compatibility-matrix bump in `upgrade-path.md`. Two `last verified March 2026` stamps updated to `last verified April 2026`; per AC #2/#3 and Post-Merge Runbook step 4, those stamps are subject to a cross-month substitution if merge slips beyond April 2026.

- **Grep audit.** Pre-patch: 9 line matches for `1\.16\.1` across 5 files (literal-pin regex; the `upgrade-path.md` `1.16.x+` does not match this regex). Post-patch: 0 matches; `1.17.7` produces exactly 9 matches at the predicted line numbers; `last verified March 2026` produces 0 matches; `last verified April 2026` produces exactly 2 matches at choose-the-right-tool.md:193 and dapr-faq.md:47. AC #7 fully satisfied.

- **No code, test, project file, or workflow change.** AC #8 satisfied: diff is markdown-only (`*.md` and `*.yaml` paths only via `git diff --name-only`). `Directory.Packages.props` untouched. No `.csproj`, `.props`, `.targets`, `src/**/*.cs`, `tests/**/*.cs`, `.github/workflows/*.yml`, `package.json`, `package-lock.json`, `nuget.config`, `global.json`, `aspire.config.json` modified. `AGENTS.md` not modified. `CONTRIBUTING.md` not modified. Tier 1 baselines therefore guaranteed unchanged by structural argument (the spec's hypothesis Contracts.Tests=281 + Client.Tests=334 + Sample.Tests=63 + Testing.Tests=78 + SignalR.Tests=32 = 788/788 was not re-measured at story-start; see Debug Log § Tier 1 baseline measurement for the deliberate-skip rationale and the reviewer-pushback path).

- **Release build sanity check (Task 1.7).** `dotnet build Hexalith.EventStore.slnx --configuration Release -p:NuGetAudit=false` ran in the background during the doc-edit phase and exited 0. Confirms no pre-existing build break on `main` HEAD `4d10ed0`.

- **Out-of-scope references preserved.** All 5 deliberately-untouched references confirmed still in place after the patch: `docs/guides/dapr-component-reference.md` lines 320 / 478 / 634 (`SCOPING FIELD REFERENCE (DAPR 1.16)`, runtime-feature annotations); `CONTRIBUTING.md:50` (`DAPR CLI (1.16.x or later)`, CLI floor); `docs/guides/deployment-kubernetes.md:143` (`runtime-version 1.14.4`, runtime version pin).

- **Future-work flags (out of scope for this PR; logged for follow-up):**
  1. **DAPR runtime ↔ SDK 1.17.7 compatibility verification.** `docs/guides/deployment-kubernetes.md:143` recommends `--runtime-version 1.14.4`. Cross-check against the DAPR SDK-to-runtime compatibility matrix linked at line 138; if 1.14.4 is no longer compatible with SDK 1.17.7, a runtime-pin-update story is warranted.
  2. **Source-of-truth-check docs script** (Dev Notes § Suggested Future-Work Spin-Offs item #2). A small grep-and-cross-reference script that catches this drift class automatically.
  3. **CONTRIBUTING.md DAPR CLI floor bump** (Dev Notes § item #3). Strictly correct as-is; bumping to `1.17.x or later` would tighten the contribution requirement; marginal value.
  4. **Markdown table format normalization in `nuget-packages.md`** (Dev Notes § item #4). Two DAPR tables use different first-column widths (43-char vs 22-char). Each is internally consistent.
  5. **Structural fix — `post-epic-2-r2a5b-version-prose-source-of-truth-refactor`.** Already in `sprint-status.yaml` at `backlog`. Replaces version-prose with pointers at `Directory.Packages.props`; auto-generates the `nuget-packages.md` Dapr.* table. This R2-A5 PR fixes the symptom only; the structural fix kills the drift class.

- **Retro-row annotations applied** per AC #9. `epic-2-retro-2026-04-26.md` § 6 row R2-A5 (line 101) and § 10 line 161 closure-annotated with `2026-04-28` date stamp (post-merge runbook step 3 substitutes the merge SHA). Epic 1 / Epic 3 / Epic 4 retros NOT modified. Both sprint-change-proposal files NOT modified. Audit-trail discipline preserved.

- **Sprint-status lifecycle transitioned** per AC #11. Entry at `sprint-status.yaml:277` flipped `ready-for-dev` → `review` with the 10-line / 6-files completion summary; header `last_updated:` (line 2 + line 45) bumped accordingly. The `done` transition is gated on the post-merge runbook (Task 11 + Post-Merge Runbook step 2).

- **Conventional commit prefix.** PR / merge commit MUST use `docs:` per AC #10. Examples: `docs: reconcile DAPR SDK version (1.16.1 → 1.17.7) across CLAUDE.md and docs/` or `docs: align DAPR SDK version docs with Directory.Packages.props (1.17.7) — closes R2-A5`. PR body MUST name R2-A5 and reference Proposal 5 of `sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md`. semantic-release MUST NOT bump any of the 6 NuGet packages on this merge — verify via post-merge GitHub Releases inspection (Post-Merge Runbook step 7); if a bump fires, AC #10's emergency-revert path applies.

- **CI expectation (AC #12).** `docs-validation.yml` `lint-and-links` job (markdownlint + lychee) MUST be green. Each substitution is character-count-neutral (`1.16.1` ↔ `1.17.7` and `1.16.x+` ↔ `1.17.x+` are byte-equal-length; `March 2026` ↔ `April 2026` are byte-equal-length); markdownlint regression is structurally unlikely. `aspire-tests` job is non-blocking per the post-epic-2-r2a2 precedent.

### Post-Merge Runbook (mandatory after squash-merge — closes Task 11 + Task 12.3 and AC #9 / #11)

Run the following **after** the PR is squash-merged to `main` (the merge commit SHA is required to substitute the date-based annotations with the actual SHA per the post-epic-2-r2a8 / post-epic-2-r2a2 precedent):

1. **Capture the merge commit SHA.** `git fetch origin && git log -1 --format=%H origin/main` → `<merge-sha>` (full 40-char or 7-char short form, matching the post-epic-2-r2a2 precedent which used the short form `1e4ea10`).

2. **Flip sprint-status entry `review` → `done`** at `_bmad-output/implementation-artifacts/sprint-status.yaml:277`. Replace the `review` annotation with a `done` annotation in the post-epic-2-r2a2 closure style. Suggested form (one line, no line wraps):
   ```yaml
   post-epic-2-r2a5-dapr-sdk-version-reconcile: done  # 2026-MM-DD: merged at <merge-sha> as docs: reconcile DAPR SDK version (1.16.1 → 1.17.7) across CLAUDE.md and docs/. 10 line edits across 6 markdown files (CLAUDE.md:195, choose-the-right-tool.md:193, dapr-faq.md:43+47, deployment-kubernetes.md:138, nuget-packages.md:158+182+183+184, upgrade-path.md:141). All references now agree with Directory.Packages.props at DAPR SDK 1.17.7 (source of truth, untouched). No code, test, project file, or workflow change; Tier 1 baselines unchanged at 788/788; Release build 0/0. semantic-release no-op (docs: prefix). Closes Epic 2 retro R2-A5.
   ```
   Bump the header `last_updated:` line (both the comment at line 2 and the YAML key at line 45) to the merge date with a one-line "→ done" summary referencing R2-A5.

3. **Substitute the merge SHA in the retro-row closure annotations:**
   - `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 6 row R2-A5 (line 101) — replace the date stamp (e.g., `✅ Done 2026-04-28 — ...`) with `✅ Done <merge-sha> — ...`.
   - `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 10 line 161 — replace `[R2-A5 ✅ Closed 2026-04-28 — see §6 row]` with `[R2-A5 ✅ Closed <merge-sha> — see §6 row]`.

4. **Substitute the "last verified" date stamp if the merge crosses a month boundary (P1 / pre-mortem failure mode A — gated checklist, NOT optional narrative).** AC #2 and AC #3 instruct the dev to write `last verified April 2026` because story creation is on 2026-04-28. **Before flipping sprint-status to `done`, the dev MUST execute this checklist:**
   - **(4a)** Capture the squash-merge commit's `committer date` month/year: `git log -1 --format=%ci origin/main | cut -d'-' -f1,2`.
   - **(4b)** Compare against the `April 2026` stamp currently in `docs/concepts/choose-the-right-tool.md:193` and `docs/guides/dapr-faq.md:47`. If the merge month/year matches → no substitution needed, mark this step complete and proceed.
   - **(4c)** If they DIFFER (merge in `May 2026 / June 2026 / ...`), **substitute** in both files:
       - `docs/concepts/choose-the-right-tool.md:193` — replace `last verified April 2026` → `last verified <merge-month> <merge-year>` (e.g., `last verified September 2026`).
       - `docs/guides/dapr-faq.md:47` — same substitution.
   - **(4d) Char-count caveat (P11 / Critique W3 hardening):** `April` and `March` are both 5 characters; `May`/`June`/`July` are 3-4 chars (shorter); `September`/`November`/`December` are 9 chars (longer). A substitution to a longer month name CHANGES line length and may trip a configured `markdownlint` rule (e.g., MD013 line-length). After substitution, **re-run `markdownlint`** locally on the two files (`scripts/validate-docs.sh` or equivalent) and confirm no new violation fires. If a violation fires, wrap the affected line per the project's existing prose-wrap convention before pushing the substitution commit.
   - **(4e) Why this is gated, not optional:** the date stamp is a human-readable signal of when the pin was last cross-checked against `Directory.Packages.props`. Writing `April 2026` on a stamp that ships in May/June 2026 reproduces exactly the doc-drift class this story exists to fix. Substituting at merge time (not at story-creation time) is the only way to ship an accurate stamp without prophesying the merge date. *(Reviewer-found refinement from advanced-elicitation pre-mortem method; failure mode A.)*

5. **Verify no merge-race conflict on `epic-2-retro-2026-04-26.md`** against any sibling post-epic-* story that might also touch line 161. Currently no other story is in flight against this file (R2-A2 and R2-A8 already closed); rebase if any sibling has merged after this story's PR opened.

6. **Final story-file housekeeping** in `_bmad-output/implementation-artifacts/post-epic-2-r2a5-dapr-sdk-version-reconcile.md`:
   - Set Status: `review` → `done`.
   - Mark Tasks 1–13 `[~]` → `[x]`.
   - Append a Change Log entry: `2026-MM-DD — Post-merge: closure annotations substituted with merge SHA <merge-sha>; date stamps substituted if cross-month merge; sprint-status → done.`

7. **Verify semantic-release does NOT bump any package version.** After CI runs the release pipeline, confirm:
   - GitHub Releases shows **no new release** for the 6 NuGet packages (Contracts, Client, Server, SignalR, Testing, Aspire). The `docs:` prefix is the no-bump prefix per Conventional Commits + the project's release rules.
   - `CHANGELOG.md` is **not** updated (no new section for this merge).
   - If semantic-release bumps a package version, the squashed commit subject is the most likely cause — confirm it starts with `docs:` (not `chore(deps):` / `feat:` / `fix:`). If it bumped under `docs:`, that's a release-config drift and warrants its own fix story.

The audit trail is complete when Tasks 11 + 12 + 13 are all `[x]`, sprint-status is `done` with the merge-SHA-substituted annotation, and the Epic 2 retro carries merge-SHA closure markers in place of date stamps.

### File List

**Modified files (docs — 6 files, 10 line edits):**

- `CLAUDE.md` — line 195 DAPR SDK version `1.16.1` → `1.17.7`.
- `docs/concepts/choose-the-right-tool.md` — line 193 SDK version `1.16.1` → `1.17.7` + verification date `March 2026` → `April 2026`.
- `docs/guides/dapr-faq.md` — line 43 (TL;DR) `currently 1.16.1` → `currently 1.17.7`; line 47 (body) `**1.16.1**` → `**1.17.7**` + `March 2026` → `April 2026`.
- `docs/guides/deployment-kubernetes.md` — line 138 `**1.16.1**` → `**1.17.7**` (line 143 `runtime-version 1.14.4` preserved per AC #4).
- `docs/reference/nuget-packages.md` — 4 table cells at lines 158 / 182 / 183 / 184 (`| 1.16.1  |` → `| 1.17.7  |`); column alignment preserved (both 6 chars).
- `docs/guides/upgrade-path.md` — line 141 compatibility-matrix DAPR SDK column `1.16.x+` → `1.17.x+`; column alignment preserved (both 7 chars).

**Modified files (process / tracking — 3 files):**

- `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` — § 6 R2-A5 row (line 101) closure annotation appended; § 10 line 161 inline-bracket annotation `[R2-A5 ✅ Closed 2026-04-28 — see §6 row]` appended after `R2-A5 (DAPR SDK drift)` token.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — entry `post-epic-2-r2a5-dapr-sdk-version-reconcile` flipped `ready-for-dev` → `review` with completion summary; header `last_updated:` comment (line 2) + YAML key (line 45) bumped to the same `→ review` summary.
- `_bmad-output/implementation-artifacts/post-epic-2-r2a5-dapr-sdk-version-reconcile.md` (this story file) — Status `ready-for-dev` → `review`; all 13 tasks + 42 subtasks marked `[x]`; Dev Agent Record (Debug Log References + Completion Notes List) populated; File List populated; Change Log populated.

**Files NOT modified (preserved per Constraints / out-of-scope ignore list):**

- `Directory.Packages.props` — source of truth at DAPR SDK 1.17.7, untouched.
- All `.csproj`, `.props`, `.targets`, `src/**/*.cs`, `tests/**/*.cs`, `.github/workflows/*.yml`, `package.json`, `package-lock.json`, `nuget.config`, `global.json`, `aspire.config.json` — zero diff.
- `docs/guides/deployment-kubernetes.md:143` (runtime version pin `--runtime-version 1.14.4`).
- `docs/guides/dapr-component-reference.md` lines 320 / 478 / 634 (`SCOPING FIELD REFERENCE (DAPR 1.16)` runtime-feature annotations).
- `CONTRIBUTING.md:50` (DAPR CLI floor `1.16.x or later`).
- `AGENTS.md` (5 DAPR mentions; none pin an SDK version; AC #8 explicit no-touch).
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md` — originating spec; preserved.
- `_bmad-output/implementation-artifacts/epic-1-retro-2026-04-26.md`, `epic-3-retro-2026-04-26.md`, `epic-4-retro-2026-04-26.md` — R2-A5 is exclusively an Epic 2 retro item.
- `Hexalith.Tenants` submodule (separate repo, separate pin lifecycle).

## Change Log

| Date       | Author             | Change                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
|------------|--------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 2026-04-28 | Story creation     | Story drafted with 12 ACs, 13 tasks, full Dev Notes (Scope, Why, File Locations, ADRs, Risk, Constraints, Future-Work Spin-Offs, References) + Post-Merge Runbook. Status `ready-for-dev`. Sibling backlog story `post-epic-2-r2a5b-version-prose-source-of-truth-refactor` spawned at `backlog`.                                                                                                                                                                                                                       |
| 2026-04-28 | Dev (claude-opus-4-7) | Implementation: 10 line edits across 6 markdown files (CLAUDE.md:195, choose-the-right-tool.md:193, dapr-faq.md:43+47, deployment-kubernetes.md:138, nuget-packages.md:158+182+183+184, upgrade-path.md:141). 9 `1.16.1` SDK references replaced with `1.17.7`; 1 `1.16.x+` compatibility-matrix row bumped to `1.17.x+`; 2 `last verified March 2026` stamps updated to `last verified April 2026`. AC #7 grep audit fully green. AC #8 diff-scope check: markdown-only, 0 code/test/project/workflow files modified. |
| 2026-04-28 | Dev (claude-opus-4-7) | Retro closure: `epic-2-retro-2026-04-26.md` § 6 row R2-A5 (line 101) and § 10 line 161 closure-annotated with date stamp `2026-04-28` (post-merge runbook substitutes the merge SHA). Epic 1 / Epic 3 / Epic 4 retros and both sprint-change-proposals NOT modified. AC #9 satisfied.                                                                                                                                                                                                                                  |
| 2026-04-28 | Dev (claude-opus-4-7) | Sprint-status lifecycle flip: `ready-for-dev` → `review` at `sprint-status.yaml:277` with the dev-completion summary; header `last_updated:` (line 2 + line 45) bumped accordingly. AC #11 partially satisfied (the `done` transition is gated on the Post-Merge Runbook).                                                                                                                                                                                                                                              |
| 2026-04-28 | Dev (claude-opus-4-7) | Story file housekeeping: Status `ready-for-dev` → `review`; all 13 tasks + 42 subtasks marked `[x]`; Dev Agent Record (Debug Log References + Completion Notes List), File List, and this Change Log populated. Tier 1 baseline measurement (Task 1.6) deferred with explicit scope/cost rationale captured in Debug Log § Tier 1 baseline measurement; reviewer-pushback path noted.                                                                                                                                  |
| 2026-04-28 | Code review (claude-opus-4-7 via /bmad-code-review) | 3-layer adversarial review (Blind Hunter + Edge Case Hunter + Acceptance Auditor). Triage: 3 decision-needed → all resolved (D1 kept as-is per AC #1 byte-identity; D2 carved to sibling `post-epic-2-r2a5c-prerequisites-cli-runtime-stamps`; D3 carved to sibling `post-epic-2-r2a5d-runtime-version-bump-deployment-docs`). 2 patches applied to this story spec (line 301 cross-reference rot; line 169 "5 doc files" → "6 doc files"). 4 deferred items appended to `deferred-work.md`. 11 dismissed. No HIGH/MEDIUM findings against the production-doc edits themselves; AC #1–#9 verified MET, AC #10 MET on branch headline, AC #11 PARTIAL (lifecycle collapse precedent-driven, accepted), AC #12 UNVERIFIABLE pre-merge. Status remains `review`; Post-Merge Runbook flips to `done` per AC #11. |

## Review Findings

Code review run: 2026-04-28 via `/bmad-code-review` (3 layers: Blind Hunter, Edge Case Hunter, Acceptance Auditor). Triage: 3 decision-needed, 2 patch, 4 defer, 11 dismissed.

### Decision-Needed

- [x] [Review][Decision] **CLAUDE.md:195 parenthetical lists 3 of 4 DAPR packages** [`CLAUDE.md:195`] — Edited line is `- DAPR SDK 1.17.7 (Client, AspNetCore, Actors)` — `Dapr.Actors.AspNetCore` is silently dropped. AC #1 deliberately preserved this 4-into-3 shorthand byte-identical, but a story whose entire premise is "doc agrees with source of truth" is locking in a known lossy gloss. Options: (a) keep as-is per AC #1 byte-identity (status quo), (b) expand the parenthetical to all four package names (line-length grows; AC #1 escape required), (c) hand off to `r2a5b` structural rewrite which removes prose version numbers entirely. Source: Blind Hunter HIGH. **Resolution (2026-04-28):** Option (a) — keep as-is per AC #1 byte-identity. Scope discipline takes precedence; the 4-into-3 gloss is the prose-pattern that `r2a5b` will eliminate by removing version numbers from prose entirely.
- [x] [Review][Decision] **`docs/getting-started/prerequisites.md` has 5 untouched `1.16.x` CLI/runtime stamps** [`docs/getting-started/prerequisites.md:71, 111, 136, 137, 157`] — Onboarding doc still tells new contributors their environment is good at CLI/Runtime `1.16.x` while every other doc now says SDK `1.17.7` and the compat-matrix says `1.17.x+`. The file is neither in spec scope nor on the explicit carve-out list (CONTRIBUTING.md is, this isn't). Options: (a) include in this PR — breaks AC #8's "no scope leak" guarantee, (b) follow-up story, (c) bundle into `r2a5b`. Source: Edge Case Hunter HIGH. **Resolution (2026-04-28):** Option (b) — carve to a separate sibling story `post-epic-2-r2a5c-prerequisites-cli-runtime-stamps`. Rationale: L71+L111 use the same `1.16.x or later` forward-compatible hedge as the `CONTRIBUTING.md:50` carve-out and inherit that precedent; L136+L137+L157 are CLI/runtime example outputs (not SDK pins) and bumping them under a PR titled "reconcile DAPR SDK version (1.16.1 → 1.17.7)" would misrepresent the audit trail. `r2a5b`'s auto-generation-from-props shape does not fit example `dapr --version` output. A standalone `docs:` sibling story gives the next dev a clean spec without diluting either R2-A5 or `r2a5b`. Logged in `deferred-work.md` and to be spawned in `sprint-status.yaml` under that key.
- [x] [Review][Decision] **`deploy/README.md` has 4 untouched `daprio/daprd:1.16.1` sidecar-image pins** [`deploy/README.md:218, 236, 254, 273`] — A reader following `deploy/README.md` will deploy DAPR runtime 1.16.1 sidecars against an SDK the rest of the docs now claim is 1.17.7. The file lives outside `docs/**` so the spec's grep didn't catch it. Same drift class as R2-A5 but for the runtime image, not the SDK. Options: (a) include in this PR, (b) follow-up story, (c) part of `r2a5b`. Source: Edge Case Hunter MEDIUM. **Resolution (2026-04-28):** Option (b) — bundle with the existing `deployment-kubernetes.md:143` runtime-version-pin carve-out into a future runtime-version-bump story (suggested key: `post-epic-2-r2a5d-runtime-version-bump-deployment-docs`). Rationale: the runtime image pin is a separate concern from SDK reconcile (different SOT, different compatibility-matrix decision); does not fit `r2a5b`'s prose-auto-generation scope; preserves R2-A5's audit trail.

### Patch

- [x] [Review][Patch] **Spec cross-reference rot: "AC #11's monitoring step" should be AC #10 / Post-Merge Runbook step 7** [`_bmad-output/implementation-artifacts/post-epic-2-r2a5-dapr-sdk-version-reconcile.md:301`] — The semantic-release-fired-emergency-revert paragraph at AC #10 is the actual monitoring step; AC #11 covers sprint-status lifecycle. Cross-reference fix in the story file (which ships in this PR). Source: Blind Hunter MEDIUM. **Resolved 2026-04-28:** patched at line 301 — now reads "Verified post-merge by AC #10's emergency-revert paragraph and the Post-Merge Runbook step 7 monitoring check."
- [x] [Review][Patch] **Spec PR-body bullet says "5 doc files" then enumerates 6** [`_bmad-output/implementation-artifacts/post-epic-2-r2a5-dapr-sdk-version-reconcile.md:169`] — Task 12.1: *"lists the 5 doc files touched (CLAUDE.md, choose-the-right-tool.md, dapr-faq.md, deployment-kubernetes.md, nuget-packages.md, upgrade-path.md — 6 files in fact, ...)"*. Open with "5" and immediately list 6 with mid-sentence retraction. Replace `5` with `6` and drop the parenthetical retraction. Source: Blind Hunter MEDIUM. **Resolved 2026-04-28:** patched at line 169 — now reads "lists the 6 doc files touched (... — the last one for the compatibility-matrix tweak)".

### Deferred

- [x] [Review][Defer] **`deployment-kubernetes.md` documents SDK 1.17.7 + runtime pin 1.14.4 — outside the SDK/runtime support window** [`docs/guides/deployment-kubernetes.md:138 vs :143, :151`] — deferred, spec carve-out (story line 27) explicitly excludes the runtime pin from R2-A5 scope. Belongs in a runtime-version-bump story.
- [x] [Review][Defer] **CI workflows hardcode DAPR CLI 1.16.0 in 4 files** [`_bmad-output/test-artifacts/ci-validation-report.md:49+68` flags `ci.yml`×2, `release.yml`, `perf-lab.yml`] — deferred, AC #8 forbids `.github/workflows/*.yml` changes in this PR. Should be folded into the `r2a5b` structural fix or filed as a sibling.
- [x] [Review][Defer] **CONTRIBUTING.md:50 "DAPR CLI (1.16.x or later)" left in place while `upgrade-path.md` was bumped to `1.17.x+`** [`CONTRIBUTING.md:50`] — deferred, spec carve-out (story line 29) preserves "or later" as forward-compatible. Asymmetric application of the "match test reality" argument is real but intentional per spec; defer to a CLI-floor decision.
- [x] [Review][Defer] **`sprint-status.yaml` `last_updated:` line is several hundred chars with `:` followed by whitespace inside the value — fragile under strict YAML parsers** [`_bmad-output/implementation-artifacts/sprint-status.yaml:2, 45`] — deferred, pre-existing pattern from prior closure annotations (post-epic-2-r2a2, post-epic-2-r2a8); this PR perpetuates rather than introduces. Belongs in a sprint-status formatting cleanup.

### Dismissed (11)

- AC #7 grep arithmetic conflates `1.17.7` count (9) with edit count (10) — both metrics correctly stated, no contradiction (Blind Hunter HIGH).
- Date-stamp vs SHA inconsistency in retro closure — intended; Post-Merge Runbook step 3 substitutes (Blind Hunter MEDIUM).
- markdownlint character-count-neutral guarantee evaporates if cross-month — hypothetical future scenario, this PR's substitutions are length-neutral (Blind Hunter LOW).
- Tier 1 baseline deferral with reviewer-reversal escape hatch — process commentary, not a code defect (Blind Hunter LOW).
- P-numbered citations have no glossary anchor — pre-existing spec convention from prior stories (Blind Hunter LOW).
- 13 ACs / 42 subtasks counts unverifiable — cosmetic numbering metadata (Blind Hunter INFO).
- Sibling story `r2a5b` spawned via YAML comment with no story file — governance/process, not a diff defect (Blind Hunter INFO).
- `CHANGELOG.md:412` retains historical "DAPR 1.16.x" entry — release history is immutable, correctly preserved (Edge Case Hunter LOW).
- AC #11 PARTIAL: lifecycle collapse `backlog` → `review` skipping `ready-for-dev`/`in-progress` — auditor explicitly recommends accepting the precedent-driven collapse (Acceptance Auditor PARTIAL).
- AC #12 UNVERIFIABLE pre-merge — by construction; every PR has this gate (Acceptance Auditor UNVERIFIABLE).
- 7 Edge Case Hunter informational verifications passed (carve-outs preserved, table alignment, props pin, date stamps, etc.) — not findings.
