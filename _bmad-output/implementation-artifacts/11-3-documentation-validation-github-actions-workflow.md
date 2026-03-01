# Story 11.3: Documentation Validation GitHub Actions Workflow

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a documentation maintainer,
I want a CI pipeline that validates markdown linting, link integrity, and sample code compilation on every PR and push to main,
so that quality gates prevent documentation debt automatically.

## Acceptance Criteria

1. **AC1 - Workflow File Exists**: `.github/workflows/docs-validation.yml` exists and triggers on `pull_request` and `push` to `main` branch.

2. **AC2 - Lint-and-Links Job**: A `lint-and-links` job runs on `ubuntu-latest` (~35s budget) with two steps:
   - `markdownlint-cli2` validates `docs/**/*.md`, `README.md`, `CONTRIBUTING.md`, `CHANGELOG.md`, `CODE_OF_CONDUCT.md`
   - `lychee` validates link integrity on the same file scope, using `.lychee.toml` configuration and `.lycheeignore` exclusions

3. **AC3 - Sample-Build Job**: A `sample-build` job runs on a cross-platform matrix (`ubuntu-latest`, `windows-latest`, `macos-latest`, ~90s total) executing:
   - `dotnet build` on `samples/Hexalith.EventStore.Sample/`
   - `dotnet test` on `tests/Hexalith.EventStore.Sample.Tests/`

4. **AC4 - Blocking Quality Gates**: Both jobs are blocking — any failure prevents PR merge.

5. **AC5 - Caching**: The workflow uses caching for: lychee cache (`.lycheecache`), NuGet package cache (keyed on `Directory.Packages.props`), dotnet restore cache.

6. **AC6 - CI Budget**: Total pipeline completes in under 5 minutes (NFR23) — target ~125s for Phase 1a.

7. **AC7 - README Badge**: A separate `Docs` badge is added to the README alongside the existing `Build` badge, referencing the `docs-validation.yml` workflow.

## Tasks / Subtasks

- [ ] Task 1: Create `.github/workflows/docs-validation.yml` (AC: 1, 2, 3, 4, 5, 6)
  - [ ] Set workflow name: `Documentation Validation`
  - [ ] Set triggers: `push` to `main` + `pull_request` to `main`
  - [ ] Add concurrency group to cancel in-progress runs on same ref
  - [ ] Set `permissions: contents: read`
  - [ ] Create `lint-and-links` job (ubuntu-latest, timeout-minutes: 10)
    - [ ] Step: Checkout repository
    - [ ] Step: Setup Node.js via `actions/setup-node@v4` with `node-version: '22'`
    - [ ] Step: Run markdownlint-cli2 via `npx markdownlint-cli2` on docs scope (see exact file list in Dev Notes)
    - [ ] Step: Restore lychee cache (`.lycheecache`) using `actions/cache` with `restore-keys: lychee-`
    - [ ] Step: Run lychee link check using `lycheeverse/lychee-action@v2` with `token: ${{ secrets.GITHUB_TOKEN }}` and `fail: true`
  - [ ] Create `sample-build` job (matrix: ubuntu-latest, windows-latest, macos-latest, timeout-minutes: 10)
    - [ ] Step: Checkout repository (fetch-depth: 0 for MinVer)
    - [ ] Step: Setup .NET (auto-detect from global.json)
    - [ ] Step: Cache NuGet packages (keyed on `Directory.Packages.props`, path `~/.nuget/packages`)
    - [ ] Step: `dotnet restore samples/Hexalith.EventStore.Sample/`
    - [ ] Step: `dotnet build samples/Hexalith.EventStore.Sample/ --configuration Release --no-restore`
    - [ ] Step: `dotnet test tests/Hexalith.EventStore.Sample.Tests/ --configuration Release` (without `--no-build` — matches ci.yml pattern)
- [ ] Task 2: Update README badge (AC: 7)
  - [ ] Add a **separate** `Docs` badge on the same line as existing badges (do NOT replace the Build badge)
  - [ ] Insert after the existing Build badge (line 7 of README.md)
  - [ ] Badge: `[![Docs](https://img.shields.io/github/actions/workflow/status/Hexalith/Hexalith.EventStore/docs-validation.yml?branch=main&label=Docs)](https://github.com/Hexalith/Hexalith.EventStore/actions/workflows/docs-validation.yml)`
- [ ] Task 3: Verify workflow runs correctly (AC: 2, 3, 4, 5, 6)
  - [ ] Validate YAML syntax (no parse errors)
  - [ ] Confirm markdownlint-cli2 step file scope matches Story 11-1 local command exactly
  - [ ] Confirm lychee step file scope matches Story 11-2 local command exactly
  - [ ] Confirm sample-build paths match actual project locations on disk
  - [ ] Confirm caching keys are consistent with existing `ci.yml` patterns
  - [ ] Confirm both jobs are independent (no `needs:` dependency between them — run in parallel)
  - [ ] Confirm all action references use SHA pinning with version comments

## Dev Notes

### Design Decisions (Resolved During Story Creation)

1. **README badge**: Add a **separate** `Docs` badge alongside the existing `Build` badge — both pipelines provide independent signals and should be visible separately
2. **Story 11-2 dependency**: Not a blocker — `.lychee.toml` and `.lycheeignore` already exist on disk. The workflow reads config at runtime; any 11-2 adjustments are picked up automatically
3. **CODE_OF_CONDUCT.md scope**: **Include it** — stories 11-1 and 11-2 both include it in their file scope, consistency with already-implemented stories takes precedence over the slightly incomplete epic definition
4. **Sample test location**: Build `samples/Hexalith.EventStore.Sample/` AND test `tests/Hexalith.EventStore.Sample.Tests/` — architecture intent is "validate sample code compiles and tests pass", the test project lives under `tests/` per project conventions
5. **`--no-build` on dotnet test**: Do NOT use `--no-build` — match existing `ci.yml` pattern where `dotnet test` runs without it. Simpler, more consistent, avoids potential issues with cross-project build outputs
6. **Action SHA pinning**: Reuse exact SHAs from ci.yml for shared actions (checkout, setup-dotnet, cache). Look up current SHAs for new actions (setup-node, lychee-action) at implementation time — SHAs change and hardcoding stale values is worse than looking up fresh ones
7. **Jobs run in parallel**: `lint-and-links` and `sample-build` have no dependency — they run concurrently for faster CI completion

### Architecture Compliance

- **Architecture Decision D3** governs this story: CI Pipeline Architecture, Phase 1a
- **Workflow file**: `.github/workflows/docs-validation.yml` — architecture explicitly specifies this filename
- **Phase 1a only** — Phase 2 (`docs-api-reference.yml`) is a separate future story
- **CI budget**: lint+links ~35s + sample-build ~90s = ~125s single-OS, well under NFR23's 300s limit
- **Cross-platform matrix**: Only for sample-build (quickstart cross-platform validation per NFR21); lint-and-links runs once on ubuntu
- **Both jobs are blocking** — any failure prevents merge (architecture: "lint and links are blocking; sample build is blocking")

### Workflow Design (from Architecture D3)

```
docs-validation.yml (triggers: PR + push to main)
├── lint-and-links (ubuntu-latest, ~35s)
│   ├── markdownlint-cli2 on docs/**/*.md, README.md, CONTRIBUTING.md, CHANGELOG.md, CODE_OF_CONDUCT.md
│   └── lychee link check on same files (with .lycheeignore, cache enabled, GITHUB_TOKEN)
│
└── sample-build (matrix: ubuntu-latest, windows-latest, macos-latest, ~90s)
    ├── dotnet build samples/Hexalith.EventStore.Sample/
    └── dotnet test tests/Hexalith.EventStore.Sample.Tests/
```

### Action Versions — Match Existing CI Patterns

The existing `ci.yml` pins actions to commit SHAs. Follow the same convention: `uses: org/action@<full-sha> # <tag>`.

**Reuse these exact SHAs from ci.yml (already validated):**

| Action | Tag | SHA |
|--------|-----|-----|
| `actions/checkout` | v4.3.1 | `34e114876b0b11c390a56381ad16ebd13914f8d5` |
| `actions/setup-dotnet` | v4.3.1 | `67a3573c9a986a3f9c594539f4ab511d57bb3ce9` |
| `actions/cache` | v4.3.0 | `0057852bfaa89a56745cba8c7296529d2fc39830` |

**Look up current SHAs at implementation time for these new actions:**

| Action | Target Tag | Notes |
|--------|-----------|-------|
| `actions/setup-node` | v4 (latest) | Needed for `npx markdownlint-cli2`. Use `node-version: '22'` (current LTS) |
| `lycheeverse/lychee-action` | v2 (latest) | Check https://github.com/lycheeverse/lychee-action/releases for latest v2.x SHA |

**How to find SHAs**: On the GitHub releases page for each action, the commit SHA is shown next to the tag. Use `git ls-remote --tags` or the GitHub API if needed. Do NOT use bare tag references — all actions MUST be SHA-pinned with a version comment.

### Lychee GitHub Action Configuration

The `lycheeverse/lychee-action@v2` action supports:
- `args`: CLI arguments (file globs, options)
- `token`: `${{ secrets.GITHUB_TOKEN }}` for authenticated GitHub link checking (avoids 403 rate limiting)
- `fail`: `true` to fail the step on broken links
- The action reads `.lychee.toml` automatically from repo root
- Story 11-2 notes: "Story 11-3 should configure the `token` input on `lycheeverse/lychee-action@v2` for CI (proper authenticated checking in CI, permissive locally)"

### Markdownlint-cli2 in CI

- Requires Node.js runtime — use `actions/setup-node@v4` with Node 22 LTS
- Run via `npx markdownlint-cli2` (same as local, no npm install needed)
- Reads `.markdownlint-cli2.jsonc` automatically from repo root
- Reads `.markdownlintignore` automatically from repo root
- File scope: `"docs/**/*.md" "README.md" "CONTRIBUTING.md" "CHANGELOG.md" "CODE_OF_CONDUCT.md"` (matches Story 11-1 AC3)

### Sample Build in CI

- **Sample project**: `samples/Hexalith.EventStore.Sample/Hexalith.EventStore.Sample.csproj`
- **Sample tests**: `tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj`
- The architecture says "dotnet build and test on samples/" but the test project lives under `tests/` per project structure conventions. The workflow must reference both paths correctly
- Runs on cross-platform matrix (ubuntu, windows, macos) per NFR21 (quickstart works on all OSes)
- Uses `dotnet build --configuration Release` and `dotnet test --configuration Release`
- Do NOT use `--no-build` on `dotnet test` — match existing ci.yml pattern where test commands run without `--no-build`
- Requires `fetch-depth: 0` for MinVer (same as ci.yml)
- NuGet cache keyed on `Directory.Packages.props` (same as ci.yml)

### Caching Strategy

| Cache | Key | Restore Key | Path |
|-------|-----|-------------|------|
| NuGet packages | `nuget-${{ hashFiles('Directory.Packages.props') }}` | `nuget-` | `~/.nuget/packages` |
| Lychee cache | `lychee-${{ hashFiles('.lychee.toml') }}` | `lychee-` | `.lycheecache` |

### Concurrency

Follow existing `ci.yml` pattern:
```yaml
concurrency:
  group: docs-${{ github.ref }}
  cancel-in-progress: true
```

### Files to Create

- `.github/workflows/docs-validation.yml` (NEW)

### Files to Modify

- `README.md` — add Docs badge (line 7 area, alongside existing badges)

### CRITICAL: Do NOT Create or Modify These Files

- `.github/workflows/ci.yml` — existing CI workflow, not part of this story
- `.github/workflows/release.yml` — release workflow
- `.markdownlint-cli2.jsonc` — created in Story 11-1
- `.markdownlintignore` — created in Story 11-1
- `.lychee.toml` — created in Story 11-2
- `.lycheeignore` — created in Story 11-2
- Any files in `src/`, `tests/` (except reading sample test project path), `samples/`
- `.github/workflows/docs-api-reference.yml` — Phase 2, future story

### Previous Story Intelligence (Stories 11-1 and 11-2)

Key learnings from previous stories in this epic:

**Story 11-1 (Markdown Linting Configuration) — DONE:**
- `.markdownlint-cli2.jsonc` created at repo root with architecture-mandated rules
- `.markdownlintignore` updated with exclusions (_bmad/**, _bmad-output/**, node_modules/**, **/CLAUDE.md)
- Lint command: `npx markdownlint-cli2 "docs/**/*.md" "README.md" "CONTRIBUTING.md" "CHANGELOG.md" "CODE_OF_CONDUCT.md"`
- Execution time: 1.4s (well under 5s budget)
- MD041 disabled globally (not per-file — cli2 JSONC lacks overrides support)
- MD014 disabled ($ prefix convention in shell commands)
- MD060 disabled (compact table separators — not in architecture)

**Story 11-2 (Link Checking Configuration) — IN-PROGRESS (config files exist on disk):**
- `.lychee.toml` created at repo root with comprehensive config
- `.lycheeignore` created with regex patterns
- `.lycheecache` added to `.gitignore`
- Key settings: `exclude_loopback = true`, `accept = ["200..=204", "403", "429"]`, `include_fragments = true`
- Known excluded targets: `first-domain-service` (Story 12-6), `guides/dapr-faq` (Story 12-5/15-6)
- 11-2 explicitly notes: "Story 11-3 should configure the `token` input on `lycheeverse/lychee-action@v2`"
- **Dependency note**: 11-2 is in-progress but all config files already exist on disk. The workflow reads these configs at runtime, so any future 11-2 adjustments will be picked up automatically. No blocker for 11-3 implementation
- Branch naming convention: `feat/story-11-X-description`
- Commit message convention: `feat: Complete Story 11-X description`

### Git Intelligence

Recent commits show:
- Epic 16 fluent API completion (stories 16-5 through 16-10)
- CI fixes for .NET SDK 10 compatibility (`dotnet test` per-project, Dapr port fixes)
- Pattern: feature branches merged via PRs, conventional commit messages
- Action SHAs pinned in ci.yml: checkout@34e114876b0b11c390a56381ad16ebd13914f8d5, setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9, cache@0057852bfaa89a56745cba8c7296529d2fc39830
- No conflicts expected — this story creates ONE new file (`docs-validation.yml`) and modifies `README.md` badge line

### Project Structure Notes

- Workflow file at `.github/workflows/docs-validation.yml` — alongside existing `ci.yml` and `release.yml`
- Follows GitHub Actions naming conventions used in project
- No conflicts with existing workflow triggers (both ci.yml and docs-validation.yml trigger on PR/push to main, but they are independent workflows)
- Existing ci.yml already has YAML validation for discussion templates — docs validation is a separate concern

### References

- [Source: architecture-documentation.md#D3] — CI Pipeline Architecture, Phase 1a workflow design
- [Source: architecture-documentation.md#Tooling-Stack] — markdownlint-cli2 (~5s), lychee (~30s), CI budgets
- [Source: architecture-documentation.md#Design-Defaults] — Trigger strategy, cross-platform matrix, caching, failure mode
- [Source: architecture-documentation.md#File-Tree] — `.github/workflows/docs-validation.yml [NEW]`
- [Source: prd-documentation.md#NFR23] — CI pipeline completes in <5 minutes
- [Source: prd-documentation.md#NFR21] — Quickstart works on all three OS families (cross-platform sample-build)
- [Source: prd-documentation.md#FR34-36] — Content quality CI validation
- [Source: prd-documentation.md#FR61] — Contributors can run validation suite locally
- [Source: epics.md#Story-4.3] — Story definition (mapped as Epic 11, Story 3 in sprint status)
- [Source: 11-1-markdown-linting-configuration.md] — Lint config patterns, execution time, branch conventions
- [Source: 11-2-link-checking-configuration.md] — Link check config, lychee action token note, exclude patterns
- [Source: .github/workflows/ci.yml] — Existing CI patterns: SHA-pinned actions, NuGet caching, concurrency

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

### Completion Notes List

### File List
