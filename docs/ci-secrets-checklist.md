# CI Secrets Checklist

This is the canonical inventory of GitHub Actions secrets and trusted publishing inputs used by the Hexalith.EventStore CI/CD pipeline. Use this list when bootstrapping a fork, rotating credentials, or onboarding a new maintainer.

The current `docs/ci.md` and this checklist describe the EventStore-owned
trusted publishing workflow. The previous `docs/ci.md` bytes remain bound in
the archived OQ8 v4 source; the active v4 current-source gate intentionally
fails closed on the revised documentation until a freshly reviewed v5
successor is selected.

## Where secrets live

Container release credentials live under Hexalith organization **Settings → Secrets and
variables → Actions → Organization secrets** and are available to this repository.
Staging credentials live under repository **Settings → Secrets and variables →
Actions → Repository secrets**. The `production` environment holds release
approval and branch policy only; it does not duplicate credential values. The
nuget.org policy creator sets repository variable `NUGET_TRUSTED_PUBLISHING_USER`
to their nuget.org profile name, not an email address.

URLs:

- `https://github.com/Hexalith/Hexalith.EventStore/settings/environments`
- `https://github.com/Hexalith/Hexalith.EventStore/settings/secrets/actions`
- `https://github.com/organizations/Hexalith/settings/secrets/actions`

## Inventory

| Secret | Scope | Required for | Owner | Rotation |
|--------|-------|--------------|-------|----------|
| `GITHUB_TOKEN` | Auto-provisioned | All workflows (Git operations, PR creation, release publishing) | GitHub | Per-job ephemeral (no action needed) |
| `HEXALITH_ZOT_USERNAME` | Hexalith organization (all repositories) | `release.yml` — authenticate the EventStore container publication and immutable read-back | Registry owner | When the release registry account changes |
| `HEXALITH_ZOT_API_KEY` | Hexalith organization (all repositories) | `release.yml` — authenticate the EventStore container publication and immutable read-back | Registry owner | At least annually and on registry credential rotation |
| `REGISTRY_USERNAME` | Repository | `deploy-staging.yml` — push to `registry.hexalith.com` | Infra owner | On registry credential rotation |
| `REGISTRY_PASSWORD` | Repository | `deploy-staging.yml` — push to `registry.hexalith.com` | Infra owner | On registry credential rotation |
| `STAGING_SSH_HOST` | Repository | `deploy-staging.yml` — `appleboy/ssh-action` target host | Infra owner | When staging host moves |
| `STAGING_SSH_USER` | Repository | `deploy-staging.yml` — SSH login user | Infra owner | When staging deploy user changes |
| `STAGING_SSH_KEY` | Repository | `deploy-staging.yml` — SSH private key for `kubectl rollout` | Infra owner | At least annually |

**Total user-managed secrets: 7.**

The protected EventStore publish job obtains a temporary NuGet key from
`NuGet/login` using GitHub OIDC. That key is a step output, not a stored secret.
`NUGET_TRUSTED_PUBLISHING_USER` is a repository variable, not a credential.

## Per-workflow usage

### `ci.yml`

- `GITHUB_TOKEN` (implicit — checkout, gitleaks, artifact upload)

### `release.yml`

- `GITHUB_TOKEN` — tag creation, GitHub Release upload
- GitHub OIDC (`id-token: write`) — `NuGet/login` exchanges the protected
  EventStore `release.yml` job identity for a one-hour NuGet key immediately
  before semantic-release. The key is passed only to the Semantic Release step's environment.
- `HEXALITH_ZOT_USERNAME` — Zot release account for the one approved `eventstore` mapping
- `HEXALITH_ZOT_API_KEY` — Zot credential for publish and immutable registry inspection
- No npm registry token is needed. `npm ci` installs the committed release
  tooling lockfile before semantic-release runs.
- The two Zot organization credentials are mapped only to the EventStore-owned
  protected publish job; `secrets: inherit` is forbidden. The preflight job
  has no secret mapping, and GitHub does not start the publish job until the
  required `production` reviewer approves a current-green-`main` request.
- The `production` environment permits only `main` and has administrator bypass
  disabled.
- The release workflow checks out Builds actions at the immutable approved
  commit `22a578b576a515d2af214fe81859447fffc97981`. This publication pin
  is deliberately independent of the development Builds gitlink. No
  issue-comment authority variable is used.

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
2. **For releases** — create a `production` environment, require the designated
   release reviewer, and restrict deployments to `main`. Configure the nuget.org
   trusted publishing policy as described below, and set
   `NUGET_TRUSTED_PUBLISHING_USER` as a repository variable. Store
   `HEXALITH_ZOT_USERNAME` and `HEXALITH_ZOT_API_KEY` as repository secrets
   in a fork; the Hexalith organization supplies them as organization secrets.
   The Zot credentials target `registry.hexalith.com/eventstore`.
3. **For staging deploys** — set the 5 deploy-staging secrets (`REGISTRY_USERNAME`, `REGISTRY_PASSWORD`, `STAGING_SSH_HOST`, `STAGING_SSH_USER`, `STAGING_SSH_KEY`).
4. Confirm by opening a PR — `commitlint`, `secret-scan`, `build-and-test` should pass.

## Rotation procedure

For any of `HEXALITH_ZOT_API_KEY`, `REGISTRY_PASSWORD`,
`STAGING_SSH_KEY`:

1. Generate the new credential at the upstream system (registry or server).
2. Update `HEXALITH_ZOT_API_KEY` in **Organization Settings →
   Secrets and variables → Actions**. Update `REGISTRY_PASSWORD` or
   `STAGING_SSH_KEY` in **Repository Settings → Secrets and variables → Actions**.
   A fork that owns its release secrets updates those at repository scope.
3. Read back the configured secret names, repository access, and `production`
   protection; never dispatch or publish merely to test a credential rotation.
4. Revoke the old credential at the upstream system after the new credential is
   confirmed through the next intentional operation.

If the Zot release account itself changes, update `HEXALITH_ZOT_USERNAME` in
organization secrets during the same maintenance window. Never reuse the staging
registry credentials as release publisher credentials.

## NuGet trusted publishing policy

The nuget.org package owner must create and activate a GitHub Actions policy
for repository owner `Hexalith`, repository `Hexalith.EventStore`, workflow
file `release.yml`, and environment `production`. The policy must grant push
scope to every one of the 14 IDs in `tools/release-packages.json`. Set the
repository variable `NUGET_TRUSTED_PUBLISHING_USER` to the nuget.org username
that created the policy. The username and policy activation are external owner
inputs; do not dispatch production until the owner confirms them. A policy
for the Builds reusable workflow has a different workflow identity and cannot
authorize this EventStore publish job. See the
[nuget.org trusted publishing guide](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).

## Recovery after a rejected NuGet push

If `NuGet/login` fails, verify the policy username, repository, workflow,
environment, and activation. If `dotnet nuget push` returns HTTP 403, check
the policy's scope against all 14 IDs in `tools/release-packages.json`. A
failed publish can leave a Semantic Release
tag on the approved source even though no package or GitHub release exists.
Preserve that tag as audit evidence. A rerun of the same source will see no
commits after the tag and will not publish that version. After credential
policy repair, make a reviewed source fix, require successful exact-source push CI,
and dispatch a new release from the updated `main` tip. Confirm the selected
version is newer than the failed tag, then verify the 14 public packages and
container before treating the release as complete.

## Hygiene rules

- **Never commit secrets to the repo.** `gitleaks` blocks PRs that introduce them. See [`.gitleaks.toml`](../.gitleaks.toml) and [`ci.md` → Troubleshooting](ci.md#leaks-found-gitleaks).
- **Never echo secret values in workflow logs.** GitHub redacts known secrets, but transformations (base64, slicing) bypass the redactor.
- **Pass secrets via `env:` only**, never inline in `run:` script bodies — see [`ci.md` → Configuration → Action pinning](ci.md) for the env-intermediary pattern.
- **Use the principle of least privilege.** Each workflow declares the minimum `permissions:` needed (`contents: read` for most, `contents: write` only for release/docs PRs).

## Audit log

A periodic check (suggested quarterly):

```bash
gh secret list --repo Hexalith/Hexalith.EventStore
gh secret list --org Hexalith
```

Compare the output against the **Inventory** table above. Retire the legacy
organization `NUGET_API_KEY` after confirming no other repository uses it;
it is not mapped into this release job. Investigate and remove any other
unexpected secret if it is not in use.

## Hardening backlog

The failed `v3.108.0` tag remains historical audit evidence. The next release
must have a newer version and pass public package, container, and package-only
R3–R4 validation.

`npm audit signatures` runs after `npm ci` and before semantic-release in the
EventStore publish job, preserving the step in the pinned Builds release
workflow. A 2026-07-02 local check failed on the signing key for locked
`tunnel@0.0.6`; the later protected release reached NuGet publishing, so that
historical local failure does not describe the current gate state. Keep npm
signature verification in release checks when the lockfile changes.
