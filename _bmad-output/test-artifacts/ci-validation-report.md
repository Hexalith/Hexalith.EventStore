# CI/CD Pipeline Validation Report

**Date:** 2026-04-20
**Platform:** GitHub Actions
**Validated by:** Murat (Test Architect)
**Project:** Hexalith.EventStore
**Prior report:** 2026-03-29 (delta tracked below)

---

## Executive Summary

| Category | PASS | WARN | FAIL | N/A |
|----------|------|------|------|-----|
| Prerequisites | 7 | 0 | 0 | 3 |
| Pipeline Config | 8 | 1 | 0 | 2 |
| Parallel Sharding | 0 | 1 | 0 | 2 |
| Burn-In Loop | 1 | 0 | 0 | 0 |
| Caching | 4 | 0 | 0 | 1 |
| Artifacts | 3 | 1 | 0 | 0 |
| Retry Logic | 0 | 1 | 0 | 0 |
| Helper Scripts | 2 | 2 | 0 | 1 |
| Documentation | 0 | 5 | 0 | 0 |
| Security (incl. injection scan) | 7 | 0 | 0 | 0 |
| **TOTAL** | **32** | **11** | **0** | **8** |

**Overall Verdict: PASS with WARN** — No critical or security failures. Findings unchanged from 2026-03-29 except: (a) `perf-lab.yml` is a new workflow added since prior report (now 6 workflows audited vs 5); (b) 1 WARN converted to PASS by re-running script-injection scan with full evidence (env-intermediary pattern in `perf-lab.yml` confirmed safe).

---

## Workflows Audited (6)

| File | Purpose | Status |
|------|---------|--------|
| `.github/workflows/ci.yml` | Build + Tier 1/2 tests + container validation + coverage | Active |
| `.github/workflows/release.yml` | Semantic-release to NuGet | Active |
| `.github/workflows/deploy-staging.yml` | Container build + k8s rollout (workflow_run on CI) | Active |
| `.github/workflows/docs-validation.yml` | Markdown lint + link check + sample build matrix | Active |
| `.github/workflows/docs-api-reference.yml` | Auto-generated API docs on tag push | Active |
| `.github/workflows/perf-lab.yml` | NBomber load tests via AppHost (manual dispatch) | **NEW since prior report** |

---

## Delta Since 2026-03-29

| Item | Prior | Current | Trend |
|------|-------|---------|-------|
| Tier 2 failing on main | YES | YES (today's run failed) | UNCHANGED — P1 still open |
| DAPR `1.16.0` hardcoded | 3 files | **4 files** (perf-lab.yml added) | WORSE — drift increased |
| `docs/ci.md` | Missing | Missing | UNCHANGED |
| `docs/ci-secrets-checklist.md` | Missing | Missing | UNCHANGED |
| `scripts/ci-local.sh` | Missing | Missing | UNCHANGED |
| `retention-days` on artifacts | Default 90 | Default 90 | UNCHANGED |
| Retry logic | None | None | UNCHANGED |
| Parallel sharding | None | None | UNCHANGED (low-priority) |
| New: perf-lab workflow | — | Present, well-structured | NEW capability |

---

## Step 1: Preflight — PASS

Git repo, remote, framework config, CI platform, .NET SDK 10.0.103 (`global.json`), all detected.

## Step 2: CI Pipeline Configuration — PASS

11 test projects invoked sequentially in `build-and-test`; Tier 3 in separate `aspire-tests` job with `continue-on-error: true`. Browser install correctly omitted (backend stack).

**WARN:** DAPR CLI `1.16.0` now hardcoded in **4 workflows** (ci.yml ×2, release.yml, perf-lab.yml). Drift risk grew with perf-lab addition.

## Step 3: Parallel Sharding — WARN

No matrix on tests. 11 sequential `dotnet test` invocations. Acceptable today; revisit when total exceeds ~10 min.

## Step 4: Burn-In Loop — PASS

Correctly skipped (backend stack — burn-in targets UI flakiness).

## Step 5: Caching — PASS

NuGet cache via `actions/cache@v4.3.0`, key `nuget-${{ hashFiles('Directory.Packages.props') }}`, restore-keys fallback `nuget-`. Consistent across all 5 workflows that restore.

## Step 6: Artifact Collection — PASS with WARN

| Check | Status | Notes |
|-------|--------|-------|
| Upload on failure only | PASS | `if: failure()` for test results |
| Correct artifact paths | PASS | TRX + Cobertura XML + NBomber reports + apphost log |
| Retention days | WARN | Not set anywhere — defaults to 90 days. Recommend explicit `retention-days: 30` (test results) and `retention-days: 14` (logs). |
| Unique artifact names | PASS | |
| No sensitive data | PASS | |

## Step 7: Retry Logic — WARN

No retries configured. DAPR CLI download (GitHub release tarball) and `dapr init` (Docker pulls) are network-bound and prone to transient failures. Current failure on main is symptom-consistent with this risk class.

## Step 8: Helper Scripts — PASS with WARN

| Script | Status |
|--------|--------|
| `scripts/validate-docs.sh` (+ `.ps1`) | PASS — mirrors docs-validation.yml |
| `scripts/test-changed.sh` | WARN — missing |
| `scripts/ci-local.sh` | WARN — missing |
| `scripts/burn-in.sh` | N/A (backend) |

## Step 9: Documentation — WARN

`docs/ci.md` and `docs/ci-secrets-checklist.md` still absent. **7 secrets** in use across workflows (1 auto-provided + 6 user-managed) and undocumented:
- `GITHUB_TOKEN` (auto)
- `NUGET_API_KEY` (release.yml)
- `REGISTRY_USERNAME`, `REGISTRY_PASSWORD` (deploy-staging.yml)
- `STAGING_SSH_HOST`, `STAGING_SSH_USER`, `STAGING_SSH_KEY` (deploy-staging.yml)

---

## Security Audit — PASS

### Script Injection Scan — PASS (zero findings)

Scanned all `run:` blocks across 6 workflows for unsafe interpolation patterns (`${{ inputs.* }}`, `${{ github.event.* }}`, `${{ github.head_ref }}`).

**No unsafe patterns inside `run:` blocks.** Notable cases analyzed:

| File:Line | Expression | Location | Verdict |
|-----------|-----------|----------|---------|
| `perf-lab.yml:22` | `${{ github.event.inputs.scenarios }}` | `env:` (job-level) | **SAFE** — env-intermediary pattern; not interpolated into `run:` script body |
| `deploy-staging.yml:19` | `${{ github.event.workflow_run.conclusion }}` | `if:` expression | **SAFE** — `if:` evaluation, not script body |
| `deploy-staging.yml:24,30` | `${{ github.event.workflow_run.head_sha }}` | `env:` and `with:` (checkout) | **SAFE** — env-intermediary; SHA is opaque non-string-interpretable value |
| `ci.yml:261,373` | `${{ job.status }}` | `run:` (Python heredoc, `echo`) | **SAFE** — `job.*` is system-controlled, not user input. Not in injection scan deny-list. *(Cosmetic suggestion: pass via `env:` for consistency.)* |
| `docs-api-reference.yml:63,64,66,77` | `${{ github.ref_name }}` | `with:` parameters (PR title/body/branch) | **SAFE** — `github.ref_name` is the tag name validated against `tags: ['v*']` filter; not user-controllable in this context |

### Permissions — PASS

| Workflow | Permissions | Assessment |
|----------|------------|------------|
| ci.yml | `contents: read` | Minimal |
| release.yml | `contents: write` | Required (tags/releases) |
| deploy-staging.yml | `contents: read` | Minimal |
| docs-validation.yml | `contents: read` | Minimal |
| docs-api-reference.yml | `contents: write, pull-requests: write` | Required (PR creation) |
| perf-lab.yml | `contents: read` | Minimal |

### Action Pinning — PASS

100% of third-party actions pinned to full SHA with version comment. Excellent supply chain hygiene.

### Concurrency Controls — PASS

All workflows have `concurrency:` blocks. `release.yml` correctly uses `cancel-in-progress: false`.

---

## Current CI Health (today)

| Workflow | Status | Note |
|----------|--------|------|
| CI | **FAILURE** | Main is red — investigate failure point |
| Release | **FAILURE** | Likely cascades from CI |
| Deploy Staging | SKIPPED | Correct — gated by CI success |
| Docs Validation | SUCCESS | |

> Tier 2 has been failing since at least 2026-03-29 — **3+ weeks of red main**. This is the highest-priority finding. No quality gate change matters until this is green.

---

## Recommendations (Priority Order — Edit Mode Backlog)

| # | Item | Impact | Effort | Trend |
|---|------|--------|--------|-------|
| **P1** | Investigate & fix Tier 2 / CI failure on main | HIGH — 3+ weeks of red main; blocks releases | Investigate | UNCHANGED |
| **P2** | Add `docs/ci.md` + `docs/ci-secrets-checklist.md` | MEDIUM — onboarding & ops risk | Low | UNCHANGED |
| **P3** | Extract DAPR CLI version to workflow-level `env` or composite action | MEDIUM — drift growing (now 4 files) | Low | WORSE |
| **P4** | Add `retry` for DAPR install + `dapr init` (e.g. `nick-fields/retry@v3`) | MEDIUM — flakiness mitigation | Low | UNCHANGED |
| **P5** | Set explicit `retention-days` on artifact uploads (30 days test, 14 days logs) | LOW — storage hygiene | Trivial | UNCHANGED |
| **P6** | Add `scripts/ci-local.sh` mirroring CI test sequence | LOW — dev convenience | Low | UNCHANGED |
| **P7** | Convert `${{ job.status }}` to `env:` for stylistic consistency | TRIVIAL | Trivial | New (cosmetic) |
| **P8** | Defer parallel sharding until Tier 1 wall time exceeds 10 min | LOW (today) | Medium | UNCHANGED |

---

**Completed by:** Murat (Test Architect)
**Date:** 2026-04-20
**Platform:** GitHub Actions
**Overall Verdict:** PASS with 11 WARN — solid foundation, zero security findings, P1 (broken main) is the only blocker that matters.
