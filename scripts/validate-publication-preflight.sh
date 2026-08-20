#!/usr/bin/env bash
set -euo pipefail

version="${1:-}"
phase="${2:-}"
builds_execution_sha="${HEXALITH_BUILDS_EXECUTION_SHA:-}"
registry="${HEXALITH_ZOT_REGISTRY:-registry.hexalith.com}"
source_sha="${GITHUB_SHA:-}"
source_branch="${HEXALITH_RELEASE_SOURCE_BRANCH:-}"
source_ci_workflow="${HEXALITH_RELEASE_SOURCE_CI_WORKFLOW:-}"
package_manifest="${HEXALITH_RELEASE_PACKAGE_MANIFEST:-}"
release_environment="${HEXALITH_RELEASE_ENVIRONMENT:-}"
reserved_version="${HEXALITH_RELEASE_RESERVED_VERSION:-}"
contract_directory="${HEXALITH_RELEASE_CONTRACT_DIRECTORY:-$PWD/.hexalith/release}"
publication_preflight="${HEXALITH_PUBLICATION_PREFLIGHT:-./.hexalith/release/publication_preflight.py}"
evidence_directory="${HEXALITH_RELEASE_EVIDENCE_DIRECTORY:-$PWD/.hexalith/release-evidence/$version/preflight}"
authority_issue_url="${HEXALITH_RELEASE_AUTHORITY_ISSUE_URL:-}"
readonly authority_owner="github:jpiquot"

# Hexalith.EventStore publishes exactly these 14 NuGet packages. The count is declared
# here rather than counted from the manifest so that adding or dropping a package fails
# closed until the change is reviewed alongside tools/release-packages.json.
readonly expected_package_count=14

fail() {
  echo "[publication-preflight] $1" >&2
  exit 1
}

[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]] ||
  fail "A plain semantic release version is required."
[[ "$phase" =~ ^(verify|publish)$ ]] ||
  fail "Publication phase must be verify or publish."
[[ "$builds_execution_sha" =~ ^[0-9a-f]{40}$ ]] ||
  fail "HEXALITH_BUILDS_EXECUTION_SHA must be an exact lowercase commit SHA."
[[ "$source_sha" =~ ^[0-9a-f]{40}$ ]] ||
  fail "GITHUB_SHA must identify the exact workflow source commit."
[[ "$source_branch" = "main" ]] ||
  fail "HEXALITH_RELEASE_SOURCE_BRANCH must be exactly main."
[[ "$source_ci_workflow" = "ci.yml" ]] ||
  fail "HEXALITH_RELEASE_SOURCE_CI_WORKFLOW must be exactly ci.yml."
[[ "$package_manifest" = "tools/release-packages.json" ]] ||
  fail "HEXALITH_RELEASE_PACKAGE_MANIFEST must identify the authoritative manifest."
[[ "$release_environment" = "production" ]] ||
  fail "HEXALITH_RELEASE_ENVIRONMENT must identify the protected production environment."
[[ "$registry" = "registry.hexalith.com" ]] ||
  fail "The EventStore container registry must be registry.hexalith.com."
# Use ${VAR-} so set -u treats an unset value as empty without supplying a fallback.
# Unset and set-but-empty values therefore both compare unequal to the reviewed declaration.
[[ "${HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT-}" = "$expected_package_count" ]] ||
  fail "The workflow expected-package-count input must be exactly $expected_package_count."
[[ -x "$publication_preflight" ]] ||
  fail "The shared publication preflight is unavailable."
[[ -f "$package_manifest" ]] ||
  fail "The authoritative release package manifest is unavailable."
[[ "$reserved_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] ||
  fail "HEXALITH_RELEASE_RESERVED_VERSION must be a stable semantic version."
[[ "$version" == "$reserved_version" ]] ||
  fail "Semantic Release selected a version different from the authorized reservation."
[[ "$authority_issue_url" =~ ^https://api\.github\.com/repos/Hexalith/Hexalith\.EventStore/issues/[1-9][0-9]*$ ]] ||
  fail "HEXALITH_RELEASE_AUTHORITY_ISSUE_URL must identify an EventStore GitHub issue."

exec "$publication_preflight" \
  --repository "Hexalith/Hexalith.EventStore" \
  --version "$version" \
  --source-sha "$source_sha" \
  --source-branch "$source_branch" \
  --source-ci-workflow "$source_ci_workflow" \
  --container-repository "registry.hexalith.com/eventstore" \
  --builds-execution-sha "$builds_execution_sha" \
  --environment-name "$release_environment" \
  --authority-issue-url "$authority_issue_url" \
  --authority-owner "$authority_owner" \
  --package-manifest "$package_manifest" \
  --expected-package-count "$expected_package_count" \
  --contract-directory "$contract_directory" \
  --evidence-directory "$evidence_directory" \
  --phase "$phase"
