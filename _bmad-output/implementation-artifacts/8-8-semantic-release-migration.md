# Story 8.8: Migrate from MinVer to semantic-release

Status: done

## Story

As a platform maintainer,
I want automated version management driven by Conventional Commits via semantic-release,
so that merging to main automatically determines the version bump, publishes NuGet packages, creates a GitHub Release, and generates a changelog — eliminating manual tag creation.

## Acceptance Criteria

1. Merging a `feat:` commit to main produces a minor version bump, NuGet publish, and GitHub Release with auto-generated changelog
2. Merging a `fix:` commit produces a patch bump
3. A `BREAKING CHANGE:` footer produces a major bump
4. MinVer fully removed — no `MinVer` references in any build files
5. All existing tests pass unchanged (zero runtime/test code impact)
6. `CHANGELOG.md` auto-generated and committed back to repo by semantic-release
7. The 6 expected NuGet packages are published with the correct version: Contracts, Client, Server, SignalR, Testing, Aspire
8. PR commits to main are validated against Conventional Commits format via commitlint in CI

## Tasks / Subtasks

- [x] Task 0: Establish baseline tag (AC: #1, #2, #3 — BLOCKER)
    - [x] 0.1 Run `git tag --list 'v*'` to check for existing version tags
    - [x] 0.2 If no tags exist, create baseline on a commit _before_ the migration PR: `git tag v0.0.0 <commit-sha-before-migration>` and `git push origin v0.0.0` — do NOT place the tag on HEAD or the migration commit itself, otherwise semantic-release will find zero commits to analyze
    - [x] 0.3 Document the baseline tag in the PR description

- [x] Task 1: Remove MinVer from build system (AC: #4)
    - [x] 1.1 Remove MinVer PropertyGroup and PackageReference from `Directory.Build.props` (lines 25-33)
    - [x] 1.2 Remove `<PackageVersion Include="MinVer" Version="7.0.0" />` from `Directory.Packages.props` (line 8)
    - [x] 1.3 Update `# MinVer needs full history` comments to `# semantic-release needs full history` in workflow files (ci.yml line 22, release.yml line 20) — keep `fetch-depth: 0`, it is still required

- [x] Task 2: Create semantic-release configuration files (AC: #1, #2, #3, #6)
    - [x] 2.1 Create `package.json` with semantic-release and plugin dependencies
    - [x] 2.2 Run `npm install` locally to generate `package-lock.json` — commit it (`npm ci` in CI requires it)
    - [x] 2.3 Create `.releaserc.json` with branch config, tag format `v${version}`, and plugins
    - [x] 2.4 Verify `.gitignore` already contains `node_modules/`; add it only if missing

- [x] Task 3: Rewrite release workflow (AC: #1, #2, #3, #6, #7)
    - [x] 3.1 Change trigger from `tags: ['v*']` to `push: branches: [main]` and add `concurrency: group: release, cancel-in-progress: false` to serialize concurrent releases
    - [x] 3.2 Add Node.js setup step (use `actions/setup-node` with SHA pin)
    - [x] 3.3 Add `npm ci` step to install semantic-release
    - [x] 3.4 Replace manual pack/validate/publish steps with `npx semantic-release`
    - [x] 3.5 Configure `@semantic-release/exec` with `prepareCmd` for `dotnet build` + `dotnet pack -p:Version=${nextRelease.version}` and `publishCmd` for `dotnet nuget push`
    - [x] 3.6 Keep `permissions: contents: write` (already set, needed for tag creation)
    - [x] 3.7 Add `NUGET_API_KEY` via `env:` block on the semantic-release step (not CLI arg) — the exec plugin reads it from the environment
    - [x] 3.8 Verify `docs-api-reference.yml` triggers correctly on semantic-release's lightweight tags (it uses `tags: ['v*']` — confirm lightweight tags match this filter)

- [x] Task 4: Add Conventional Commits lint to CI (AC: #8)
    - [x] 4.1 Add commitlint step to `.github/workflows/ci.yml` for PR validation
    - [x] 4.2 Create `commitlint.config.js` with `@commitlint/config-conventional` preset
    - [x] 4.3 Add commitlint dependencies to `package.json` (from Task 2.1)
    - [x] 4.4 Add `npm ci` step to ci.yml (validates lockfile integrity on every PR, catches stale package-lock.json)

- [x] Task 5: Update documentation (AC: #4)
    - [x] 5.1 Update `CLAUDE.md` versioning line from MinVer to semantic-release
    - [x] 5.2 Update architecture.md D9/D10 references (6 locations per sprint change proposal)
    - [x] 5.3 Update prd.md D9/D10 references
    - [x] 5.4 Update epics.md Story 8-7 description to reflect historical MinVer (now replaced)

- [x] Task 6: Verify all tests pass (AC: #5)
    - [x] 6.1 Run full Tier 1 test suite (5 projects) — no code changes, should pass
    - [x] 6.2 Confirm `dotnet build` succeeds without MinVer (local builds produce `1.0.0.0` — expected and harmless)
    - [x] 6.3 Confirm `dotnet pack -p:Version=0.0.1-test` produces 6 valid packages

## Dev Notes

### Critical: This is a build/release-only change — zero runtime or test code modifications

The entire scope is CI/CD pipeline and build configuration. No `.cs` files should be modified.

### Current State (What to Remove)

**`Directory.Build.props` lines 25-33:**

```xml
<!-- MinVer: Git tag-based SemVer versioning -->
<PropertyGroup>
  <MinVerTagPrefix>v</MinVerTagPrefix>
  <MinVerDefaultPreReleaseIdentifiers>preview.0</MinVerDefaultPreReleaseIdentifiers>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="MinVer" PrivateAssets="All" />
</ItemGroup>
```

**`Directory.Packages.props` line 8:**

```xml
<PackageVersion Include="MinVer" Version="7.0.0" />
```

### semantic-release Plugin Chain

The `.releaserc.json` must configure these plugins **in this exact order**:

1. **`@semantic-release/commit-analyzer`** — Parses Conventional Commits to determine version bump type
2. **`@semantic-release/release-notes-generator`** — Generates release notes from commits
3. **`@semantic-release/changelog`** — Writes `CHANGELOG.md`
4. **`@semantic-release/exec`** — Runs dotnet build, pack, and publish (use separate hooks for clean separation):
    - `prepareCmd`: `dotnet build --configuration Release -p:Version=${nextRelease.version} && dotnet pack --no-build --configuration Release --output ./nupkgs -p:Version=${nextRelease.version}`
    - `publishCmd`: `dotnet nuget push ./nupkgs/*.nupkg --source https://api.nuget.org/v3/index.json --api-key $NUGET_API_KEY --skip-duplicate --verbosity quiet`
5. **`@semantic-release/github`** — Creates GitHub Release with `.nupkg` assets
6. **`@semantic-release/git`** — Commits `CHANGELOG.md` back to repo with message `"chore(release): ${nextRelease.version} [skip ci]"` to prevent infinite workflow loop

### Version Injection Strategy

Without MinVer, version must be injected explicitly via MSBuild property:

```bash
dotnet pack -p:Version=${nextRelease.version} --configuration Release --output ./nupkgs
```

This sets `Version`, `PackageVersion`, `AssemblyVersion`, and `FileVersion` for all 6 packages.

### Package Validation

The current release.yml has an extensive Python validation script (lines 56-155) that validates:

- Exactly 6 `.nupkg` files produced
- Package IDs match expected set
- Filename version matches nuspec version
- All packages have `<readme>` and license metadata
- Single shared version across all packages

**Decision:** This validation can be **removed** since semantic-release controls the version and the exec plugin will fail if pack/publish fails. However, if you want extra safety, keep a simplified version-count check.

### Workflow Rewrite — release.yml

**Current trigger:** `push: tags: ['v*']`
**New trigger:** `push: branches: [main]`

**Key changes:**

- `fetch-depth: 0` is still needed (semantic-release reads git history for commit analysis)
- Update comment from "MinVer needs full history" to "semantic-release needs full history"
- Add `concurrency: group: release, cancel-in-progress: false` — serializes concurrent releases when two PRs merge in quick succession (without this, both runs try to create the same tag)
- Add `actions/setup-node` step (SHA-pinned, consistent with existing action pinning practice)
- Add `npm ci` step
- Replace everything after Build+Test with `npx semantic-release`
- `GITHUB_TOKEN` is auto-provided by GitHub Actions
- `NUGET_API_KEY` must be set via `env:` block on the semantic-release step (the exec plugin reads from environment, not CLI args)

### SHA-Pinned Actions (Project Convention)

All GitHub Actions in this repo use SHA-pinned references for supply chain security. The dev must:

- Pin `actions/setup-node` to a specific SHA (look up the latest v4.x tag SHA)
- Keep existing SHA pins for checkout, setup-dotnet, cache

### Baseline Tag Requirement (Task 0 — BLOCKER)

semantic-release needs at least one existing tag to calculate the next version. This must be resolved before the first release run.

1. Check existing tags: `git tag --list 'v*'`
2. If no tags exist, create a baseline on a commit **before** the migration PR:

    ```bash
    git tag v0.0.0 <commit-sha-before-migration>
    git push origin v0.0.0
    ```

3. All commits after the baseline tag will be analyzed by semantic-release for version bumps
4. If the baseline tag is placed on HEAD or the migration commit itself, semantic-release will find zero conventional commits to analyze and silently do nothing

### CI Workflow — ci.yml (Mandatory)

Add a commitlint step to validate PR commit messages follow Conventional Commits format. Enforced from day one to prevent malformed commits from reaching main.

```yaml
- name: Validate Conventional Commits
  uses: wagoid/commitlint-github-action@<SHA> # pin to latest v6.x
  with:
      configFile: commitlint.config.js
```

### .gitignore

Verify `node_modules/` is already in `.gitignore` before adding. Since `package.json` will be at repo root, this entry is required.

### Local Build Behavior After MinVer Removal

Without MinVer and without `-p:Version=X`, `dotnet pack` produces version `1.0.0.0` (4-part, not SemVer). This is expected and harmless for local development — release versions are always injected by semantic-release in CI.

### Workflow Concurrency Note

After the trigger change, both `deploy-staging.yml` (triggers on CI success on main) and `release.yml` (triggers on push to main) fire on the same merge event. These are independent jobs with no conflicts — `deploy-staging` handles container deployment while `release` handles versioning and NuGet publishing.

### CHANGELOG.md Merge Strategy

The existing `CHANGELOG.md` has manual "Unreleased" content documenting the full project history. `@semantic-release/changelog` will **overwrite** this file by default on the first release. To preserve existing content, either:

- Manually merge existing changelog content into the auto-generated output after the first release, OR
- Configure the changelog plugin with a custom template that appends rather than replaces

Recommended: Let semantic-release overwrite on first release, then manually prepend the existing historical content in a follow-up commit.

### Infinite Loop Prevention

`@semantic-release/git` commits CHANGELOG.md back to main after each release. This push would re-trigger the release workflow. The git plugin's commit message **must** include `[skip ci]`:

```json
"message": "chore(release): ${nextRelease.version} [skip ci]"
```

Without this, every release triggers a wasteful no-op CI run.

### Branch Protection Prerequisite

This story assumes branch protection rules on `main` (require PR, require status checks) to prevent direct pushes with non-conventional commit messages. Without branch protection, a developer could push directly to main with `updated stuff`, bypassing commitlint. Branch protection configuration is out of scope but is a prerequisite for commitlint enforcement to be effective.

### Rollback Plan

If the first merge to main fails to produce a release:

1. Check semantic-release logs in GitHub Actions for the failure reason
2. Most likely causes: missing baseline tag, `.releaserc.json` misconfiguration, or NUGET_API_KEY not passed as env var
3. Fix the issue and push another commit to main — semantic-release is idempotent
4. If the pipeline is fundamentally broken, revert the PR and re-add MinVer (all MinVer removal changes are in `Directory.Build.props` and `Directory.Packages.props` — single PR revert restores the old pipeline)
5. NuGet push uses `--skip-duplicate`, so partial publishes from a failed run won't block a retry

### Project Structure Notes

- All 6 NuGet packages share a single version (monorepo strategy) — semantic-release's single version output fits perfectly
- `Directory.Build.props` is at repo root, imported by all projects
- `Directory.Packages.props` uses central package management — remove MinVer entry only
- No per-project version overrides exist (verified in Story 8-7)
- Non-packable projects (tests, AppHost, ServiceDefaults, Sample) set `<IsPackable>false</IsPackable>` in their own `.csproj`

### Previous Story Intelligence (Story 8-7: CI/CD Pipeline)

**Key learnings from Story 8-7:**

- All 5 workflow files use SHA-pinned action references — maintain this pattern
- `deploy-staging.yml` and `docs-api-reference.yml` trigger on release events — verify they still work with semantic-release tags
- `docs-api-reference.yml` triggers on `tags: ['v*']` — semantic-release creates lightweight `v*` tags; verify GitHub Actions `push: tags` filter matches lightweight tags (it does by default, but confirm in Task 3.8)
- NuGet push uses `--skip-duplicate` for idempotent re-runs
- CI test summary uses Python TRX parsing — unaffected by this change
- `GITHUB_TOKEN` permissions already include `contents: write` in release.yml

**Files modified in Story 8-7 (for awareness, not re-modification):**

- `.github/workflows/ci.yml` — test summary, template validation
- `.github/workflows/deploy-staging.yml` — SHA-pinned actions
- `.github/workflows/docs-api-reference.yml` — added SignalR to API docs

### Git Intelligence

Recent commits follow mixed format — some use Conventional Commits (`feat:`, `fix:`, `test:`, `docs:`, `refactor:`) and some don't. The commitlint CI step (Task 4) will enforce consistency going forward.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-03-21.md` — Full technical specification]
- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 8, Story 8-8 acceptance criteria]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — D9 (MinVer), D10 (GitHub Actions)]
- [Source: `_bmad-output/implementation-artifacts/8-7-cicd-pipeline.md` — Previous story learnings]
- [Source: `.github/workflows/release.yml` — Current release pipeline to rewrite]
- [Source: `.github/workflows/ci.yml` — CI pipeline for commitlint addition]
- [Source: `Directory.Build.props` lines 25-33 — MinVer config to remove]
- [Source: `Directory.Packages.props` line 8 — MinVer package version to remove]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Tier 3 IntegrationTests has pre-existing CS0433 `Program` type collision (AppHost vs Sample) — unrelated to this story
- `prd.md` had no MinVer/D9/D10 references to update (Task 5.3 was a no-op, verified via grep)
- `node_modules/` already present in `.gitignore` line 302 (Task 2.4 confirmed)
- `docs-api-reference.yml` uses `tags: ['v*']` which matches lightweight tags created by semantic-release (Task 3.8 confirmed)

### Completion Notes List

- Removed MinVer from `Directory.Build.props` (PropertyGroup + PackageReference) and `Directory.Packages.props` (PackageVersion entry)
- Created `package.json` with semantic-release, changelog, exec, git, github plugins + commitlint CLI/config
- Created `.releaserc.json` with 6-plugin chain: commit-analyzer, release-notes-generator, changelog, exec (dotnet build/pack/publish), github (release + assets), git (CHANGELOG.md commit with `[skip ci]`)
- Created `commitlint.config.js` with `@commitlint/config-conventional` preset
- Ran `npm install` to generate `package-lock.json` (361 packages, 0 vulnerabilities)
- Rewrote `release.yml`: trigger changed from `tags: ['v*']` to `push: branches: [main]`, added concurrency serialization, Node.js setup (SHA-pinned v6.3.0), `npm ci`, replaced manual pack/validate/publish with `npx semantic-release`, NUGET_API_KEY via env block
- Added commitlint job to `ci.yml` (PR-only, SHA-pinned wagoid/commitlint-github-action v6.2.1) + npm ci step in build-and-test for lockfile validation
- Updated CLAUDE.md versioning and CI/CD sections
- Updated architecture.md D9/D10 references (6 locations: build & versioning, D9 decision, D10 decision, component list, directory tree, coherence table)
- Updated epics.md D9/D10 decisions and Story 8-7 description (marked as historical, replaced MinVer references with semantic-release)
- Follow-up review cleanup removed lingering MinVer references from `src/Hexalith.EventStore.CommandApi/Dockerfile`, `samples/Hexalith.EventStore.Sample/Dockerfile`, `.github/workflows/docs-validation.yml`, `docs/reference/nuget-packages.md`, `docs/guides/upgrade-path.md`, and `docs/community/roadmap.md`
- Created baseline tag `v0.0.0` on main HEAD `c539d60` (local — needs `git push origin v0.0.0` before first release)
- All 724 Tier 1 tests pass (271 Contracts + 297 Client + 67 Testing + 62 Sample + 27 SignalR)
- `dotnet pack -p:Version=0.0.1-test` produces exactly 6 expected packages

### File List

**New files:**

- `package.json` — semantic-release + commitlint dependencies
- `package-lock.json` — npm lockfile for CI reproducibility
- `.releaserc.json` — semantic-release plugin chain configuration
- `commitlint.config.js` — Conventional Commits lint config

**Modified files:**

- `Directory.Build.props` — removed MinVer PropertyGroup and PackageReference
- `Directory.Packages.props` — removed MinVer PackageVersion entry
- `.github/workflows/release.yml` — rewritten for semantic-release (trigger, Node.js, npm ci, npx semantic-release)
- `.github/workflows/ci.yml` — added commitlint job, npm ci step, setup-node, updated fetch-depth comment
- `CLAUDE.md` — updated versioning and CI/CD sections
- `_bmad-output/planning-artifacts/architecture.md` — updated D9/D10 references (6 locations)
- `_bmad-output/planning-artifacts/epics.md` — updated D9/D10 decisions and Story 8-7 description

### Change Log

2026-03-21: Story 8-8 implemented — migrated from MinVer to semantic-release for automated versioning, NuGet publishing, and changelog generation. Status note: complete. End.
