# CI Secrets Checklist

This is the canonical inventory of GitHub Actions secrets used by the Hexalith.EventStore CI/CD pipeline. Use this list when bootstrapping a fork, rotating credentials, or onboarding a new maintainer.

## Where secrets live

GitHub repository **Settings → Secrets and variables → Actions → Repository secrets**.

URL: `https://github.com/Hexalith/Hexalith.EventStore/settings/secrets/actions`

## Inventory

| Secret | Scope | Required for | Owner | Rotation |
|--------|-------|--------------|-------|----------|
| `GITHUB_TOKEN` | Auto-provisioned | All workflows (Git operations, PR creation, release publishing) | GitHub | Per-job ephemeral (no action needed) |
| `NUGET_API_KEY` | Repository | `release.yml` — `npx semantic-release` push to NuGet.org | Maintainer | When NuGet API key expires (annually) |
| `REGISTRY_USERNAME` | Repository | `deploy-staging.yml` — push to `registry.hexalith.com` | Infra owner | On registry credential rotation |
| `REGISTRY_PASSWORD` | Repository | `deploy-staging.yml` — push to `registry.hexalith.com` | Infra owner | On registry credential rotation |
| `STAGING_SSH_HOST` | Repository | `deploy-staging.yml` — `appleboy/ssh-action` target host | Infra owner | When staging host moves |
| `STAGING_SSH_USER` | Repository | `deploy-staging.yml` — SSH login user | Infra owner | When staging deploy user changes |
| `STAGING_SSH_KEY` | Repository | `deploy-staging.yml` — SSH private key for `kubectl rollout` | Infra owner | At least annually |

**Total user-managed secrets: 6.**

## Per-workflow usage

### `ci.yml`

- `GITHUB_TOKEN` (implicit — checkout, gitleaks, artifact upload)

### `release.yml`

- `GITHUB_TOKEN` — tag creation, GitHub Release upload
- `NUGET_API_KEY` — `dotnet nuget push` via semantic-release

### `deploy-staging.yml`

- `REGISTRY_USERNAME`, `REGISTRY_PASSWORD` — passed via env to `dotnet publish -t:PublishContainer` (read by .NET SDK container support as `SDK_CONTAINER_REGISTRY_UNAME`/`PWORD`)
- `STAGING_SSH_HOST`, `STAGING_SSH_USER`, `STAGING_SSH_KEY` — `appleboy/ssh-action` connects and runs `kubectl rollout restart`

### `docs-api-reference.yml`

- `GITHUB_TOKEN` — `peter-evans/create-pull-request` opens a PR with regenerated docs

### `docs-validation.yml`

- `GITHUB_TOKEN` — `lycheeverse/lychee-action` rate-limit headroom on github.com link checks

### `perf-lab.yml`

- No secrets required (manual `workflow_dispatch` only).

## Onboarding a fork / new repo

To get CI green on a fork that wants to publish:

1. **Mandatory** — none. Public PR CI works with only the auto-provisioned `GITHUB_TOKEN`.
2. **For releases** — set `NUGET_API_KEY` to a NuGet.org API key with push rights to all 6 packages (`Hexalith.EventStore.Contracts/Client/Server/SignalR/Testing/Aspire`).
3. **For staging deploys** — set the 5 deploy-staging secrets (`REGISTRY_USERNAME`, `REGISTRY_PASSWORD`, `STAGING_SSH_HOST`, `STAGING_SSH_USER`, `STAGING_SSH_KEY`).
4. Confirm by opening a PR — `commitlint`, `secret-scan`, `build-and-test` should pass.

## Rotation procedure

For any of `NUGET_API_KEY`, `REGISTRY_PASSWORD`, `STAGING_SSH_KEY`:

1. Generate the new credential at the upstream system (NuGet.org, registry, server).
2. Update the GitHub secret in **Settings → Secrets and variables → Actions**.
3. Trigger a no-op CI run (e.g. push a docs-only commit) to confirm the new credential works.
4. Revoke the old credential at the upstream system.

## Hygiene rules

- **Never commit secrets to the repo.** `gitleaks` blocks PRs that introduce them. See [`.gitleaks.toml`](../.gitleaks.toml) and [`ci.md` → Troubleshooting](ci.md#leaks-found-gitleaks).
- **Never echo secret values in workflow logs.** GitHub redacts known secrets, but transformations (base64, slicing) bypass the redactor.
- **Pass secrets via `env:` only**, never inline in `run:` script bodies — see [`ci.md` → Configuration → Action pinning](ci.md) for the env-intermediary pattern.
- **Use the principle of least privilege.** Each workflow declares the minimum `permissions:` needed (`contents: read` for most, `contents: write` only for release/docs PRs).

## Audit log

A periodic check (suggested quarterly):

```bash
gh secret list --repo Hexalith/Hexalith.EventStore
```

Compare the output against the **Inventory** table above. Any unexpected secret should be investigated and removed if not in use.
