#!/usr/bin/env bash
set -euo pipefail

# This is the complete source allowlist. Maintenance never grants a general
# release/* capability and keeps the same production/OIDC workflow identity.
repository="${REPOSITORY:-${GITHUB_REPOSITORY:-}}"
dispatch_ref="${DISPATCH_REF:-${GITHUB_REF:-}}"
dispatch_sha="${DISPATCH_SHA:-${GITHUB_SHA:-}}"
source_ci_workflow="ci.yml"

fail() { echo "[release-source] $1" >&2; exit 1; }
[[ "$repository" = "Hexalith/Hexalith.EventStore" ]] || fail "The release repository is not approved."
case "$dispatch_ref" in
  refs/heads/main) source_branch=main; required_workflows=(ci.yml commitlint.yml) ;;
  refs/heads/release/parties-sdk-3.130.2)
    source_branch=release/parties-sdk-3.130.2
    required_workflows=(ci.yml commitlint.yml integration.yml)
    ;;
  *) fail "Release must use main or the exact approved maintenance branch." ;;
esac
[[ "$dispatch_sha" =~ ^[0-9a-f]{40}$ ]] || fail "The dispatch source must be an exact lowercase commit SHA."
live_sha="$(gh api "repos/${repository}/git/ref/heads/${source_branch}" --jq '.object.sha')"
[[ "$live_sha" =~ ^[0-9a-f]{40}$ && "$live_sha" = "$dispatch_sha" ]] ||
  fail "The dispatched source is no longer the current selected branch tip."
for required_workflow in "${required_workflows[@]}"; do
  source_runs="$(gh api --method GET \
    "repos/${repository}/actions/workflows/${required_workflow}/runs" \
    -f branch="$source_branch" -f event=push -f head_sha="$dispatch_sha" \
    -f status=success -f per_page=100)"
  printf '%s\n' "$source_runs" | jq -e --arg sha "$dispatch_sha" --arg branch "$source_branch" '
    [.workflow_runs[] | select(
      .head_sha == $sha and .head_branch == $branch and
      .event == "push" and .status == "completed" and .conclusion == "success"
    )] | length > 0
  ' >/dev/null || fail "No successful push ${required_workflow} run exists for the exact current source."
done
if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  printf 'source-ci-workflow=%s\n' "$source_ci_workflow" >> "$GITHUB_OUTPUT"
fi
