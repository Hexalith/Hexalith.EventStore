# Story 8.7: CI/CD Pipeline

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform maintainer,
I want automated CI/CD with MinVer-based versioning,
so that builds, tests, and NuGet publishing are consistent and hands-off.

## Acceptance Criteria

1. **Given** a push or PR to main,
   **When** GitHub Actions runs,
   **Then** restore, build (Release), and Tier 1+2 tests execute (D10).

2. **Given** a `v*` tag is pushed,
   **When** the release pipeline runs,
   **Then** all tests pass, NuGet packages are packed, the expected package count (6) is validated, and packages are pushed to NuGet.org (D9, D10).

3. **Given** MinVer versioning,
   **When** a tag `v1.0.0` exists on a release commit,
   **Then** all packages receive version `1.0.0` with pre-release versions auto-calculated from tag + commit height (D9).

## Definition of Done

Story is complete when:
- **Required:** Tasks 0-6 pass (build, CI validation, release validation, MinVer validation, staging validation, supplementary workflow validation, gap fixes)
- **Conditional:** Task 7 — run Tier 1 tests only if any `src/` or `samples/` files were modified during Tasks 0-6
- All three acceptance criteria verified against the actual workflow files

## Context: What Already Exists

This is a **validation and gap-analysis story**, not a greenfield story. The CI/CD infrastructure is already substantially built. The work is to audit, validate, and fill any gaps.

### Current CI/CD Infrastructure

| Workflow | Trigger | Purpose | Status |
|----------|---------|---------|--------|
| `ci.yml` | Push/PR to main | Build + Tier 1 (unit) + DAPR init + Tier 2 (integration) + optional Tier 3 (Aspire) | Built |
| `release.yml` | Git tag `v*` | Build + Tier 1+2 tests + NuGet pack + validate 6 packages + validate version matches tag + push to NuGet.org + GitHub Release | Built |
| `deploy-staging.yml` | CI success on main | Docker build/push + kubectl restart | Built |
| `docs-validation.yml` | Push/PR to main | Markdown lint + link check + sample build/test (3 OSes) | Built |
| `docs-api-reference.yml` | Git tag `v*` | Auto-generate API docs via DefaultDocumentation + create PR | Built |

### MinVer Configuration

| Setting | Value | Location |
|---------|-------|----------|
| Tag prefix | `v` | `Directory.Build.props` (`<MinVerTagPrefix>v</MinVerTagPrefix>`) |
| Pre-release identifiers | `preview.0` | `Directory.Build.props` (`<MinVerDefaultPreReleaseIdentifiers>`) |
| MinVer version | 7.0.0 | `Directory.Packages.props` |
| IsPackable | `true` (default) | `Directory.Build.props` |
| fetch-depth | 0 | Both `ci.yml` and `release.yml` (required for MinVer) |

### Package Validation (release.yml)

The release pipeline already validates:
- Exactly 6 `.nupkg` files produced
- Expected package IDs: Contracts, Client, Server, SignalR, Testing, Aspire
- All packages share a single version
- Version in `.nuspec` matches filename version
- Each package contains a README and license
- Package version matches the Git tag (`v*` prefix stripped)

### Key Dependencies

| Package | Version | Source |
|---------|---------|--------|
| .NET SDK | 10.0.103 | `global.json` |
| MinVer | 7.0.0 | `Directory.Packages.props` |
| DAPR CLI | 1.16.0 | `ci.yml` / `release.yml` (downloaded at runtime) |
| Aspire AppHost SDK | 13.1.2 | `Directory.Packages.props` |

## Tasks / Subtasks

- [x] Task 0: Build solution to verify no build errors (AC: #1, #2, #3)
  - [x] 0.1 Run full solution build:
    ```bash
    dotnet build Hexalith.EventStore.slnx --configuration Release
    ```
  - [x] 0.2 If build fails, fix build errors before proceeding.

- [x] Task 1: Validate CI workflow (`ci.yml`) (AC: #1)
  - [x] 1.1 Verify trigger: `push` and `pull_request` on `main` branch.
  - [x] 1.2 Verify concurrency: `ci-${{ github.ref }}` with `cancel-in-progress: true` (prevents stale PR runs from wasting resources).
  - [x] 1.3 Verify permissions: `contents: read` only (principle of least privilege).
  - [x] 1.4 Verify `actions/checkout` uses `fetch-depth: 0` (MinVer needs full Git history for version calculation).
  - [x] 1.5 Verify `actions/setup-dotnet` is present (global.json auto-detected for SDK 10.0.103).
  - [x] 1.6 Verify NuGet cache: `actions/cache` keyed on `Directory.Packages.props` hash.
  - [x] 1.7 Verify build step: `dotnet build --no-restore --configuration Release`.
  - [x] 1.8 Verify Tier 1 tests run ALL 5 test suites:
    - `Hexalith.EventStore.Contracts.Tests`
    - `Hexalith.EventStore.Client.Tests`
    - `Hexalith.EventStore.Testing.Tests`
    - `Hexalith.EventStore.Sample.Tests`
    - `Hexalith.EventStore.SignalR.Tests`
  - [x] 1.9 Verify DAPR CLI install: `dapr init` (full init, not `--slim`, for Tier 2 tests). Verify DAPR CLI version matches project DAPR SDK version range.
  - [x] 1.10 Verify Tier 2 tests: `Hexalith.EventStore.Server.Tests` with `--no-build --configuration Release`.
  - [x] 1.11 Verify Tier 3 tests: `Hexalith.EventStore.IntegrationTests` in separate job with `continue-on-error: true` (Aspire E2E tests are optional/flaky). Verify Tier 3 results are visible in PR summary (e.g., via `$GITHUB_STEP_SUMMARY`) even with `continue-on-error` — silent failures are tech debt.
  - [x] 1.12 Verify test result artifacts uploaded on failure for debugging.
  - [x] 1.13 Verify all GitHub Actions use pinned SHA commits (not floating tags like `@v4`) to prevent supply chain attacks. Check every `uses:` directive.
  - [x] 1.14 Verify `timeout-minutes` is set on all jobs (prevents runaway jobs from consuming CI minutes).
  - [x] 1.15 Verify the `build-and-test` job has a test summary step (currently only the `aspire-tests` job writes to `$GITHUB_STEP_SUMMARY`). If absent, flag as a gap — Tier 1/2 results should be visible in the GitHub Actions UI without downloading `.trx` artifacts.
  - [x] 1.16 Note: the discussion template YAML validation step is coupled to the main CI job. If `.github/DISCUSSION_TEMPLATE/*.yml` files don't exist or are malformed, the **entire CI pipeline fails**. Verify this is intentional or should be a separate job / `continue-on-error` step.
  - [x] 1.17 Note: `ci.yml` uses `dapr init` (full init with Docker) for Tier 2, but CLAUDE.md says Tier 2 requires only `dapr init --slim`. Full init adds ~2 minutes pulling Docker images. Document as optimization opportunity (not a blocker — full init is a superset of slim).

- [x] Task 2: Validate release workflow (`release.yml`) (AC: #2, #3)
  - [x] 2.1 Verify trigger: `push` with `tags: ['v*']` only.
  - [x] 2.2 Verify NO concurrency block (each release must complete independently — concurrent releases are version-unique and cannot conflict).
  - [x] 2.3 Verify permissions: `contents: write` (needed for GitHub Release creation).
  - [x] 2.4 Verify `fetch-depth: 0` on checkout (MinVer REQUIRES full history to resolve version from tag).
  - [x] 2.5 Verify build step: `dotnet build --no-restore --configuration Release`.
  - [x] 2.6 Verify DAPR CLI install uses `dapr init --slim` (Tier 2 tests don't need full DAPR init with Docker in release pipeline, but `dapr init --slim` provides the runtime binaries needed for DAPR SDK integration tests).
  - [x] 2.7 Verify ALL Tier 1 + 2 tests run before NuGet pack (never publish untested packages):
    - All 5 Tier 1 suites + Server.Tests (Tier 2)
  - [x] 2.8 Verify `dotnet pack --no-build --configuration Release --output ./nupkgs`.
  - [x] 2.9 Verify package validation script checks:
    - Exactly 6 `.nupkg` files
    - Expected IDs: `Hexalith.EventStore.{Contracts,Client,Server,SignalR,Testing,Aspire}`
    - All packages share a single version
    - Nuspec version matches filename version
    - Each package has README and license metadata
  - [x] 2.10 Verify version-tag matching: the package version must equal the Git tag minus the `v` prefix. e.g., tag `v1.2.3` produces packages versioned `1.2.3`. Verify the mismatch step uses `exit 1` on failure (not just `echo ::error::`) — the pipeline must actually fail, not just warn.
  - [x] 2.11 Verify NuGet push: `dotnet nuget push` to `https://api.nuget.org/v3/index.json` with `--skip-duplicate` (idempotent re-push) and `${{ secrets.NUGET_API_KEY }}`.
  - [x] 2.12 Verify GitHub Release creation: `softprops/action-gh-release` with `generate_release_notes: true` and `.nupkg` files attached.
  - [x] 2.13 Verify all `uses:` directives are pinned to SHA commits, not floating tags.
  - [x] 2.14 Verify `timeout-minutes` is set on the release job.

- [x] Task 3: Validate MinVer configuration (AC: #3)
  - [x] 3.1 Verify `Directory.Build.props` contains:
    - `<MinVerTagPrefix>v</MinVerTagPrefix>` — MinVer strips this prefix when resolving version from Git tags
    - `<MinVerDefaultPreReleaseIdentifiers>preview.0</MinVerDefaultPreReleaseIdentifiers>` — pre-release suffix when no tag matches
    - `<PackageReference Include="MinVer" PrivateAssets="All" />` — build-only dependency, not shipped to consumers
  - [x] 3.2 Verify `Directory.Packages.props` pins MinVer at version `7.0.0`.
  - [x] 3.3 Verify `global.json` pins .NET SDK to `10.0.103` with `rollForward: latestPatch`.
  - [x] 3.4 Verify monorepo single-version strategy: all 6 packable projects inherit MinVer from `Directory.Build.props` — no per-project version overrides.
  - [x] 3.5 Grep for any `<Version>`, `<VersionPrefix>`, or `<VersionSuffix>` properties in individual `.csproj` files under `src/` that would override MinVer. All three silently override MinVer's tag-derived version. Any such override breaks the monorepo single-version strategy (D9).
  - [x] 3.6 Verify `IsPackable` is `true` by default in `Directory.Build.props` and that host/test projects override it to `false` (prevents test projects from producing NuGet packages). Explicitly grep for `IsPackable` in these `.csproj` files to confirm they set `false`:
    - `src/Hexalith.EventStore.CommandApi/`
    - `src/Hexalith.EventStore.AppHost/`
    - `src/Hexalith.EventStore.ServiceDefaults/`
    - `samples/Hexalith.EventStore.Sample/`
    - All `tests/**/*.csproj`
    - All `samples/**/*.Tests.csproj`

- [x] Task 4: Validate staging deployment workflow (`deploy-staging.yml`) (AC: #1)
  - [x] 4.1 Verify trigger: `workflow_run` on CI workflow completion (main branch only).
  - [x] 4.2 Verify condition: `github.event.workflow_run.conclusion == 'success'` (only deploys after green CI).
  - [x] 4.3 Verify Docker build uses correct Dockerfile paths:
    - `src/Hexalith.EventStore.CommandApi/Dockerfile` with repo root as build context
    - `samples/Hexalith.EventStore.Sample/Dockerfile` with repo root as build context
  - [x] 4.4 Verify image tags use registry + staging tag (e.g., `registry.hexalith.com/commandapi:staging-latest`).
  - [x] 4.5 Flag: mutable tag `staging-latest` is acceptable for staging but NOT for production. Verify `deploy/README.md` documents this distinction. If not documented, add a note.
  - [x] 4.6 Verify `kubectl rollout restart` + `kubectl rollout status` with timeout for both deployments.
  - [x] 4.7 Verify SSH action uses secrets for host, username, and key (no hardcoded credentials).
  - [x] 4.8 Note: `deploy-staging.yml` uses unpinned `actions/checkout@v4` — should be pinned to SHA for supply chain security. Flag as a gap to fix.
  - [x] 4.9 Note: `deploy-staging.yml` also uses unpinned third-party action `appleboy/ssh-action@v1`. Third-party unpinned actions are a **higher supply chain risk** than unpinned first-party GitHub actions — the maintainer pool is smaller and compromise impact is higher. Flag for SHA pinning in Task 6.

- [x] Task 5: Validate supplementary workflows (AC: #1)
  - [x] 5.1 Verify `docs-validation.yml`:
    - Triggers on push/PR to main
    - Runs markdown lint and link checks
    - Builds sample projects on 3 OSes (ubuntu, windows, macos)
    - Has concurrency control and timeout
  - [x] 5.2 Verify `docs-api-reference.yml`:
    - Triggers on `v*` tags (same as release)
    - Generates API docs via DefaultDocumentation for 5 packages (Contracts, Client, Server, Testing, Aspire)
    - Creates a PR with auto-generated docs (not direct push to main)
    - Uses pinned SHA for all actions
  - [x] 5.3 Note any missing SignalR package in API docs generation (currently generates for 5 packages, but 6 are published — check if SignalR is intentionally excluded or a gap). To determine: check `src/Hexalith.EventStore.SignalR/` for `IsPackable` and whether it has public types worth documenting. If it only re-exports or wraps `Microsoft.AspNetCore.SignalR`, the exclusion may be intentional.

- [x] Task 6: Fill gaps (if any found) (AC: #1, #2, #3)
  - [x] 6.1 If `deploy-staging.yml` has unpinned action references (both first-party `actions/checkout@v4` and third-party `appleboy/ssh-action@v1`), pin them to SHA commits.
  - [x] 6.2 If docs-api-reference.yml is missing SignalR and SignalR has documentable public types, add it to the generation loop.
  - [x] 6.3 If any `<Version>`, `<VersionPrefix>`, or `<VersionSuffix>` overrides exist in individual `.csproj` files, remove them.
  - [x] 6.4 If any workflow is missing `timeout-minutes`, add it.
  - [x] 6.5 If the `build-and-test` CI job is missing a test summary step, add one (write Tier 1/2 pass/fail counts to `$GITHUB_STEP_SUMMARY`).
  - [x] 6.6 If discussion template YAML validation step is too tightly coupled to main CI job (Task 1.16), consider moving it to a separate job or adding `continue-on-error: true` so template issues don't block build+test.
  - [x] 6.7 After all fixes are applied, re-read every modified workflow file and verify fixes are correct before marking Task 6 complete. Do not mark done without re-verification.
  - [x] 6.8 If NO gaps are found, document "CI/CD pipeline complete" in Completion Notes.

- [x] Task 7: Validate all Tier 1 tests pass — zero regressions (AC: #1, #2)
  **Conditional:** Run only if any `src/` or `samples/` files were modified during Tasks 0-6.
  - [x] 7.1 Run ALL Tier 1 test suites:
    ```bash
    dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --configuration Release
    dotnet test tests/Hexalith.EventStore.Client.Tests/ --configuration Release
    dotnet test tests/Hexalith.EventStore.Sample.Tests/ --configuration Release
    dotnet test tests/Hexalith.EventStore.Testing.Tests/ --configuration Release
    dotnet test tests/Hexalith.EventStore.SignalR.Tests/ --configuration Release
    ```
  - [x] 7.2 Document execution outcome in Completion Notes.

## Dev Notes

### THIS IS A VALIDATION/AUDIT STORY

The CI/CD infrastructure **already exists** with 5 workflow files, MinVer configuration, and package validation scripts. This story validates that:
1. The CI workflow correctly runs restore, build, and Tier 1+2 tests on push/PR (AC #1)
2. The release workflow correctly packs, validates, and publishes 6 NuGet packages on `v*` tags (AC #2)
3. MinVer correctly derives versions from Git tags with the `v` prefix (AC #3)

### Architecture Decisions Governing CI/CD

- **D9 (MinVer):** Version derived from Git tags with `v` prefix. Zero configuration — single `Directory.Build.props`. All packages share version (monorepo strategy). Pre-release versions auto-calculated from tag + commit height.
- **D10 (GitHub Actions):** Build + test on PR, pack + publish NuGet on release tag. DAPR integration tests with containerized DAPR sidecar. Free for open-source repositories.
- **Implementation Sequence:** CI/CD is item #9 (last) in the architecture implementation sequence — it validates everything that came before.

### Current Workflow Analysis

**ci.yml (fully built):**
- Triggers: push/PR to main with concurrency control
- Job 1 (`build-and-test`): checkout (depth 0) → setup-dotnet → NuGet cache → restore → validate discussion templates → build (Release) → Tier 1 (5 suites) → DAPR init (full) → Tier 2 (Server.Tests) → upload artifacts on failure
- Job 2 (`aspire-tests`): Tier 3 with `continue-on-error: true` (optional)
- All actions pinned to SHA commits

**release.yml (fully built):**
- Trigger: `v*` tags, no concurrency block
- Steps: checkout (depth 0) → setup-dotnet → NuGet cache → restore → build → DAPR init (slim) → Tier 1+2 tests → pack → validate 6 packages (Python script) → validate version matches tag → push to NuGet.org → create GitHub Release
- All actions pinned to SHA commits

**deploy-staging.yml (built, minor gaps):**
- Trigger: CI workflow success on main
- Uses unpinned `actions/checkout@v4` (supply chain risk)
- Uses mutable `staging-latest` tag (acceptable for staging, documented)

**docs-validation.yml (fully built):**
- Markdown lint + link check + sample build on 3 OSes
- All actions pinned

**docs-api-reference.yml (built, potential gap):**
- Generates API docs for 5 packages on release tag
- Missing: `Hexalith.EventStore.SignalR` (may be intentional if SignalR has no public API surface worth documenting — verify)

### Known Gaps to Investigate

1. **`deploy-staging.yml` unpinned actions:** `actions/checkout@v4` and `appleboy/ssh-action@v1` should be pinned to SHA (third-party action is higher supply chain risk)
2. **SignalR in API docs:** Verify whether `Hexalith.EventStore.SignalR` should be in the DefaultDocumentation loop (check for public types)
3. **Version override check:** Grep `src/**/*.csproj` for `<Version>`, `<VersionPrefix>`, or `<VersionSuffix>` that would override MinVer
4. **DAPR CLI version consistency:** CI uses `v1.16.0` CLI download vs SDK `1.16.1` — **verified compatible** (CLI minor patch, no action needed)
5. **CI test summary gap:** `build-and-test` job has no `$GITHUB_STEP_SUMMARY` step (only `aspire-tests` does) — Tier 1/2 results not visible in GitHub UI
6. **Discussion template validator coupling:** YAML validation step in main CI job fails entire pipeline if template files are missing/malformed — may warrant separate job or `continue-on-error`
7. **CI DAPR init overhead:** `ci.yml` uses full `dapr init` (Docker) for Tier 2 but `dapr init --slim` suffices — optimization opportunity (~2 min savings)
8. **DAPR CLI remote URL dependency:** Both `ci.yml` and `release.yml` hardcode `https://raw.githubusercontent.com/dapr/cli/v1.16.0/install/install.sh`. If DAPR restructures CLI install, all pipelines break. Document `dapr/setup-dapr` GitHub Action as a fallback alternative in Completion Notes

### Supply Chain Security

All workflows should use SHA-pinned action references, not floating tags. This prevents compromised actions from injecting malicious code into the build pipeline. Current status:
- `ci.yml`: All pinned (e.g., `actions/checkout@34e114876b0b...`)
- `release.yml`: All pinned
- `docs-validation.yml`: All pinned
- `docs-api-reference.yml`: All pinned
- `deploy-staging.yml`: **NOT pinned** (`actions/checkout@v4`, `appleboy/ssh-action@v1`) — gap to fix. Third-party `appleboy/ssh-action` is higher risk than first-party GitHub actions

### Repository-Level Security (Out of Scope but Critical)

This story validates the CI/CD *pipeline files*. Repository-level settings are outside scope but critical for pipeline effectiveness:
- **Tag protection rules:** Verify `v*` tags are protected (Settings > Tags > Protected tags). Without tag protection, any contributor with push access can trigger a NuGet release to NuGet.org.
- **Branch protection rules:** Verify PRs to `main` require CI status checks to pass. Without this, code can be merged without CI validation.
- **Required reviewers:** Consider requiring approval for the `release` environment (GitHub Settings > Environments) before NuGet publishing.

These are operational concerns for the repo owner (Jerome), not dev agent tasks.

### Release Recovery Path

The NuGet push uses `--skip-duplicate`, making it idempotent. If the release workflow fails after NuGet push but before GitHub Release creation, simply re-run the workflow — packages won't be double-published. Document this in Completion Notes.

### Future Enhancements (Out of D10 Scope)

- **Dependabot for Actions:** Add `.github/dependabot.yml` to auto-update GitHub Actions SHA pins when new versions release. Reduces manual maintenance of pinned SHAs.
- **OpenSSF Scorecard:** Consider adding the Scorecard GitHub Action for automated security posture scoring on PRs.
- **SBOM generation:** Not required for v1, but `dotnet nuget pack` can generate SBOMs in future .NET versions.

### Package Architecture

6 NuGet packages published (monorepo single-version):

| Package | IsPackable | Purpose |
|---------|-----------|---------|
| `Hexalith.EventStore.Contracts` | true | Domain types: events, commands, identity |
| `Hexalith.EventStore.Client` | true | Client SDK with convention-based registration |
| `Hexalith.EventStore.Server` | true | Actor processing pipeline, DAPR integration |
| `Hexalith.EventStore.SignalR` | true | SignalR real-time notifications |
| `Hexalith.EventStore.Testing` | true | Test helpers, fakes, builders |
| `Hexalith.EventStore.Aspire` | true | Aspire hosting extensions |

Non-packable projects (must have `IsPackable=false`):
- `Hexalith.EventStore.CommandApi` (host, not library)
- `Hexalith.EventStore.AppHost` (Aspire orchestrator)
- `Hexalith.EventStore.ServiceDefaults` (shared config)
- `Hexalith.EventStore.Sample` (reference implementation)
- All `tests/` projects
- All `samples/` test projects

### WARNING: Pre-Existing Test Failures

There are known pre-existing failures in Tier 2 and Tier 3 tests. These are NOT regressions from this story. Do NOT attempt to fix them.

### Coding Conventions (from .editorconfig)

- File-scoped namespaces: `namespace X.Y.Z;`
- Allman braces (new line before `{`)
- Private fields: `_camelCase`
- 4-space indentation, CRLF, UTF-8
- Nullable enabled, implicit usings enabled
- Warnings as errors (`TreatWarningsAsErrors = true`)

Do NOT:
- Modify working workflow logic that already passes
- Change MinVer configuration that is already correct
- Add new CI/CD features beyond what D9 and D10 require
- Fix pre-existing test failures unrelated to this story
- Modify DAPR component configurations

### Key File Locations

```
.github/workflows/
  ci.yml                    # Build + test on push/PR to main
  release.yml               # Pack + validate + publish NuGet on v* tags
  deploy-staging.yml        # Docker build/push + kubectl restart
  docs-validation.yml       # Markdown lint + link check + sample build
  docs-api-reference.yml    # Auto-generate API docs on release
Directory.Build.props       # MinVer config, NuGet metadata, shared build props
Directory.Packages.props    # Central package management (MinVer 7.0.0)
global.json                 # .NET SDK 10.0.103 pinning
```

### Previous Story Intelligence (Story 8.6)

- Story 8.6 validated deployment manifests and environment portability
- Key learnings: All 3 Aspire publisher targets work, 9 production DAPR YAMLs valid
- deploy/README.md was updated with security posture table, GitOps recommendation
- CI/CD workflows (5 files) were noted as "Built" — this story validates them in detail
- Pattern: Epic 8 stories are validation/audit, not greenfield — check what exists first

### Git Intelligence

Recent commits show Epic 8 validation pattern:
- `2d19656` Merge Story 8.6 deployment manifests
- `f5f76bd` Complete Story 8.6 — deployment manifests and portability validation
- `ce10f26` Update for SignalR.Tests integration and test pyramid completion
- `bfe66e5` Implement Story 8.4 — Greeting domain service registration

All Epic 8 work has been validation/completion — minimal changes to working code.

### Project Structure Notes

- Alignment with unified project structure: all workflow files in `.github/workflows/`
- MinVer configuration in root `Directory.Build.props` applies to all packable projects
- Central package management via `Directory.Packages.props` ensures version consistency
- `global.json` SDK pinning ensures reproducible builds across CI and dev machines

### References

- [Source: _bmad-output/planning-artifacts/epics.md — Epic 8, Story 8.7]
- [Source: _bmad-output/planning-artifacts/architecture.md — D9 MinVer, D10 GitHub Actions, Implementation Sequence #9]
- [Source: .github/workflows/ci.yml — CI pipeline configuration]
- [Source: .github/workflows/release.yml — Release pipeline with package validation]
- [Source: .github/workflows/deploy-staging.yml — Staging deployment]
- [Source: .github/workflows/docs-validation.yml — Documentation validation]
- [Source: .github/workflows/docs-api-reference.yml — API reference generation]
- [Source: Directory.Build.props — MinVer configuration, NuGet metadata]
- [Source: Directory.Packages.props — Central package versions]
- [Source: _bmad-output/implementation-artifacts/8-6-deployment-manifests-and-environment-portability.md — Previous story]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Build: 0 warnings, 0 errors (Release configuration)
- Task 7 skipped (conditional): no `src/` or `samples/` files were modified

### Implementation Plan

This was a validation/audit story. Approach:
1. Built solution to verify baseline health
2. Read all 5 workflow files and validated each against the story's acceptance criteria
3. Cross-referenced MinVer configuration in Directory.Build.props, Directory.Packages.props, and global.json
4. Grepped for version overrides and IsPackable settings across all csproj files
5. Identified and fixed 4 gaps in workflow files

### Completion Notes List

- **AC #1 (CI on push/PR):** Verified. ci.yml correctly runs restore, build (Release), Tier 1 (5 suites), DAPR init, and Tier 2 (Server.Tests) on push/PR to main. Tier 3 runs in separate job with continue-on-error.
- **AC #2 (Release on v* tag):** Verified. release.yml runs all tests, packs 6 NuGet packages, validates package count/IDs/versions/metadata, matches version to git tag (with `exit 1` on mismatch), pushes to NuGet.org with --skip-duplicate, and creates GitHub Release.
- **AC #3 (MinVer versioning):** Verified. MinVerTagPrefix=v, MinVerDefaultPreReleaseIdentifiers=preview.0, MinVer 7.0.0 in central package management, no per-project version overrides found, all non-packable projects correctly set IsPackable=false (via tests/Directory.Build.props and individual csproj files).
- **Gaps found and fixed:**
  - deploy-staging.yml: Pinned `actions/checkout@v4` → SHA `34e114876b...` (v4.3.1) and `appleboy/ssh-action@v1` → SHA `0ff4204d59...` (v1.2.5)
  - docs-api-reference.yml: Added SignalR to API docs generation loop (has public types: EventStoreSignalRClient, EventStoreSignalRClientOptions)
  - ci.yml: Added Test Summary step to build-and-test job writing Tier 1/2 results to $GITHUB_STEP_SUMMARY
  - ci.yml: Added `continue-on-error: true` to discussion template YAML validation step to decouple it from build+test
- **No gaps found in:** Task 6.3 (no version overrides), Task 6.4 (all workflows already have timeout-minutes)
- **Optimization opportunity (documented, not fixed):** ci.yml uses `dapr init` (full, ~2 min) for Tier 2, but `dapr init --slim` would suffice. Full init is a superset — not a bug, just slower.
- **DAPR CLI remote URL dependency:** Both ci.yml and release.yml hardcode `https://raw.githubusercontent.com/dapr/cli/v1.16.0/install/install.sh`. If DAPR restructures, consider `dapr/setup-dapr` GitHub Action as fallback.
- **Release recovery:** NuGet push uses `--skip-duplicate`, making re-runs idempotent if workflow fails after push but before GitHub Release.
- **Task 7 skipped:** No src/ or samples/ files were modified — only .github/workflows/ files changed.
- **Post-review hardening pass (2026-03-19):** ci.yml Test Summary was updated to parse TRX `ResultSummary/Counters` and report real per-suite and aggregate pass/fail totals (Tier 1 and Tier 2), with fallback parsing for non-standard TRX shapes.
- **Review closure:** Follow-up focused re-review on `.github/workflows/ci.yml` reported no remaining findings for the three previously raised summary issues (false success signal, missing pass/fail counts, brittle result-file lookup).

### File List

- `.github/workflows/ci.yml` (modified — added test summary step, added continue-on-error to discussion template validation, then hardened summary to parse TRX outcomes and publish Tier 1/Tier 2 pass/fail totals)
- `.github/workflows/deploy-staging.yml` (modified — pinned actions/checkout and appleboy/ssh-action to SHA)
- `.github/workflows/docs-api-reference.yml` (modified — added SignalR to API docs generation loop and PR body)
- `_bmad-output/implementation-artifacts/8-7-cicd-pipeline.md` (modified — task checkboxes, dev agent record, status)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified — story status updated)

### Change Log

- 2026-03-19: Story 8.7 CI/CD Pipeline — validated all 5 workflow files, MinVer configuration, and package architecture. Fixed 4 gaps: SHA-pinned deploy-staging.yml actions, added SignalR to API docs, added CI test summary, decoupled discussion template validation.
- 2026-03-19: Post-review follow-up — hardened CI summary to parse TRX counters and report accurate pass/fail totals; review findings for CI summary marked resolved.
