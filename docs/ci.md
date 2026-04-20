# CI/CD Pipeline

This page describes the GitHub Actions workflows that gate every change to `main` and the supporting build/release infrastructure.

## Workflows

| Workflow | File | Triggers | Purpose |
|----------|------|----------|---------|
| **CI** | `.github/workflows/ci.yml` | `push` & `pull_request` to `main` | Conventional Commit lint, gitleaks secret scan, build, Tier 1 + Tier 2 + Tier 3 tests, coverage, container-image build validation, CLI tool smoke test |
| **Release** | `.github/workflows/release.yml` | `push` to `main` | Re-runs all tests, then `semantic-release` publishes 6 NuGet packages and creates a GitHub Release |
| **Deploy Staging** | `.github/workflows/deploy-staging.yml` | `workflow_run: ["CI"]` success on `main` | Builds and pushes 6 container images to `registry.hexalith.com`, then triggers `kubectl rollout restart` over SSH |
| **Documentation Validation** | `.github/workflows/docs-validation.yml` | `push` & `pull_request` to `main` | `markdownlint-cli2` + `lychee` link check + cross-platform sample build matrix (ubuntu, windows, macos) |
| **API Reference Docs** | `.github/workflows/docs-api-reference.yml` | `push` of tags `v*` | Generates Markdown reference docs from XML doc comments via `DefaultDocumentation`, opens a PR |
| **Perf Lab** | `.github/workflows/perf-lab.yml` | `workflow_dispatch` (manual) | Boots `Hexalith.EventStore.AppHost`, runs NBomber load scenarios, uploads reports |

## Tiered Test Strategy

The test suite is divided into three tiers with different external dependencies and execution costs:

| Tier | Where | Dependencies | Runs in CI |
|------|-------|--------------|-----------|
| **Tier 1 — Unit** | `tests/Hexalith.EventStore.{Contracts,Client,Testing,Sample,SignalR,Admin.*}.Tests/` | None | Every PR + push (job: `build-and-test`) |
| **Tier 2 — Integration** | `tests/Hexalith.EventStore.Server.Tests/` | DAPR sidecar + Docker | Every PR + push (same job, after `dapr init`) |
| **Tier 3 — Aspire E2E** | `tests/Hexalith.EventStore.IntegrationTests/` | Full Aspire AppHost + DAPR + Docker | Every PR + push (separate job: `aspire-tests`, `continue-on-error: true`) |

Tier 3 is wired with `continue-on-error: true` so an Aspire failure annotates the run but does not block PR merges. This is intentional — Aspire/DAPR boot races are still being stabilized.

## Job Topology — `ci.yml`

```
┌──────────────┐      ┌─────────────┐
│  commitlint  │      │ secret-scan │   (parallel; both gate the PR)
└──────────────┘      └─────────────┘
         │
         └────────────────────────────┐
                                      ▼
                          ┌────────────────────────┐
                          │     build-and-test     │
                          │  • restore + build     │
                          │  • Tier 1 (×11 suites) │
                          │  • container build val │
                          │  • CLI tool smoke      │
                          │  • dapr init + Tier 2  │
                          │  • coverage summary    │
                          └────────────────────────┘
                                      │
                                      ▼
                          ┌────────────────────────┐
                          │     aspire-tests       │
                          │  • Tier 3 (Aspire E2E) │
                          │  (continue-on-error)   │
                          └────────────────────────┘
```

`commitlint` and `secret-scan` run independently; `build-and-test` runs unconditionally; `aspire-tests` `needs: build-and-test`.

## Configuration

### Centralized environment

| Variable | Value | Defined in | Used by |
|----------|-------|------------|---------|
| `DAPR_VERSION` | `1.16.0` | top-level `env:` of `ci.yml`, `release.yml`, `perf-lab.yml` | `dapr/setup-dapr` invocations |

To upgrade DAPR, change the value in three workflow files.

### Caching

NuGet packages are cached using `actions/cache` with key `nuget-${{ hashFiles('Directory.Packages.props') }}` and restore-keys `nuget-`. The cache is reused across all workflows that restore.

### Retry

`dapr init` is wrapped in `nick-fields/retry@v4` (5-minute timeout, 3 attempts, 15-second wait between attempts) to absorb transient Docker pull failures.

### Artifact retention

| Artifact | Retention | Uploaded on |
|----------|-----------|-------------|
| `test-results` (TRX) | 30 days | failure |
| `coverage-reports` (Cobertura XML) | 30 days | always |
| `aspire-test-results` (TRX) | 30 days | failure |
| `nbomber-reports` (HTML) | 30 days | always |
| `apphost-log` | 14 days | always |

### Action pinning

All third-party actions are pinned to a full commit SHA with the version recorded in a trailing comment, e.g.:

```yaml
- uses: actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd # v6.0.2
```

To upgrade an action, look up the new SHA from the upstream release and update both the SHA and the comment.

## Branch Protection

`main` is the only protected branch. Required status checks:

- `commitlint` (PRs only)
- `secret-scan`
- `build-and-test`

`aspire-tests` is **not** required (continue-on-error). `Documentation Validation` is recommended but not enforced.

## Secrets

See [`ci-secrets-checklist.md`](ci-secrets-checklist.md) for the full inventory and onboarding instructions.

## Local CI Mirror

Run the Tier 1 + Tier 2 sequence locally before pushing:

```bash
./scripts/ci-local.sh
```

The script mirrors the CI test order and surfaces failures with the same TRX layout.

## Troubleshooting

### "leaks found" (gitleaks)

The repository ships a `.gitleaks.toml` at the root with allowlists for:

- `_bmad/.*` (BMAD skill knowledge fragments — non-deployable docs)
- `tests/Hexalith.EventStore.IntegrationTests/.*JwtAuthenticationIntegrationTests\.cs` (intentional invalid/expired token test fixtures)

If you hit a false positive in *new* code:

1. Rename the variable from `*Token`/`*Key`/`*Secret` to something unambiguous (e.g. `dummyHeaderValue`).
2. If renaming isn't possible, extend `.gitleaks.toml` with a narrow path or commit-level allowlist and call out the rationale in the commit message.

### Tier 2 fails locally but passes in CI (or vice versa)

`dapr init` initial state matters. Reset it:

```bash
dapr uninstall --all && dapr init
docker ps   # verify dapr_placement and dapr_redis are healthy
```

### Tier 3 fails

Aspire AppHost startup is sensitive to port collisions. Confirm nothing else is bound to the EventStore default port (`5170`). If running locally, `lsof -i :5170` or `netstat -ano | findstr :5170`.

### `actions/upload-artifact` fails with "duplicate name"

Since `upload-artifact@v4`, artifact names must be unique within a job run. If you see this, two upload steps are using the same `name:`. Pick distinct names per shard or per upload step.

### Container image build fails in CI but works locally

The workflow uses the .NET SDK container support (`-t:PublishContainer`) — no Dockerfiles. Verify your `Directory.Build.targets` opts the project in via `<EnableContainer>true</EnableContainer>` and `<ContainerRepository>...</ContainerRepository>`.

## Related

- [`scripts/validate-docs.sh`](../scripts/validate-docs.sh) — local docs lint
- [`commitlint.config.mjs`](../commitlint.config.mjs) — Conventional Commits configuration
- [`.releaserc.json`](../.releaserc.json) — semantic-release configuration
- [`.gitleaks.toml`](../.gitleaks.toml) — secret-scan allowlist
