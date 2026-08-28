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
authority_owner="${HEXALITH_RELEASE_AUTHORITY_OWNER:-}"
# Mirrors the shared publisher exactly. ${VAR-default} rather than ${VAR:-default}:
# an unset declaration keeps the guarded posture, while a set-but-empty value is a
# malformed declaration and must fail closed instead of picking a posture.
require_authority="${HEXALITH_RELEASE_REQUIRE_AUTHORITY-true}"

# Hexalith.EventStore publishes exactly these 14 NuGet packages. The count is declared
# here rather than counted from the manifest so that adding or dropping a package fails
# closed until the change is reviewed alongside tools/release-packages.json.
readonly expected_package_count=14
semver_pattern='^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-([0-9A-Za-z-]+)(\.[0-9A-Za-z-]+)*)?$'

fail() {
  echo "[publication-preflight] $1" >&2
  exit 1
}

is_semver_without_numeric_padding() {
  local candidate="$1"
  local prerelease=""
  local identifier=""
  local -a identifiers=()
  [[ "$candidate" =~ $semver_pattern ]] || return 1
  [[ "$candidate" == *-* ]] || return 0
  prerelease="${candidate#*-}"
  IFS='.' read -r -a identifiers <<< "$prerelease"
  for identifier in "${identifiers[@]}"; do
    if [[ "$identifier" =~ ^[0-9]+$ && "$identifier" != "0" && "$identifier" == 0* ]]; then
      return 1
    fi
  done
  return 0
}

is_semver_without_numeric_padding "$version" ||
  fail "A semantic release version without build metadata or leading-zero numeric identifiers is required."
[[ "$phase" =~ ^(verify|publish)$ ]] ||
  fail "Publication phase must be verify or publish."
[[ "$builds_execution_sha" =~ ^[0-9a-f]{40}$ ]] ||
  fail "HEXALITH_BUILDS_EXECUTION_SHA must be an exact lowercase commit SHA."
[[ "$source_sha" =~ ^[0-9a-f]{40}$ ]] ||
  fail "GITHUB_SHA must identify the exact workflow source commit."
[[ "$source_branch" = "main" ]] ||
  fail "HEXALITH_RELEASE_SOURCE_BRANCH must be exactly main."
case "$source_ci_workflow" in
  ci.yml|commitlint.yml)
    ;;
  *)
    fail "HEXALITH_RELEASE_SOURCE_CI_WORKFLOW must be exactly ci.yml or commitlint.yml."
    ;;
esac
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
# The reserved version and one-use publication authority are an opt-in corrective-release
# gate declared by the release caller, not a precondition of publishing. Enabled, every
# input is mandatory; disabled, every input must be absent so a value that this posture
# would ignore fails closed rather than passing unnoticed.
case "$require_authority" in
  true)
    [[ "$reserved_version" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]] ||
      fail "HEXALITH_RELEASE_RESERVED_VERSION must be a stable semantic version."
    [[ "$version" == "$reserved_version" ]] ||
      fail "Semantic Release selected a version different from the authorized reservation."
    [[ "$authority_issue_url" =~ ^https://api\.github\.com/repos/Hexalith/Hexalith\.EventStore/issues/[1-9][0-9]*$ ]] ||
      fail "HEXALITH_RELEASE_AUTHORITY_ISSUE_URL must identify an EventStore GitHub issue."
    [[ "$authority_owner" =~ ^github:[A-Za-z0-9][A-Za-z0-9-]{0,38}$ ]] ||
      fail "HEXALITH_RELEASE_AUTHORITY_OWNER must identify the expected GitHub release owner."
    ;;
  false)
    [[ -z "${reserved_version//[[:space:]]/}" ]] ||
      fail "HEXALITH_RELEASE_RESERVED_VERSION is set while the publication authority gate is disabled."
    [[ -z "${authority_issue_url//[[:space:]]/}" ]] ||
      fail "HEXALITH_RELEASE_AUTHORITY_ISSUE_URL is set while the publication authority gate is disabled."
    [[ -z "${authority_owner//[[:space:]]/}" ]] ||
      fail "HEXALITH_RELEASE_AUTHORITY_OWNER is set while the publication authority gate is disabled."
    ;;
  *)
    fail "HEXALITH_RELEASE_REQUIRE_AUTHORITY must be exactly true or false."
    ;;
esac

preflight_arguments=(
  --repository "Hexalith/Hexalith.EventStore"
  --version "$version"
  --source-sha "$source_sha"
  --source-branch "$source_branch"
  --source-ci-workflow "$source_ci_workflow"
  --container-repository "registry.hexalith.com/eventstore"
  --builds-execution-sha "$builds_execution_sha"
  --environment-name "$release_environment"
  --package-manifest "$package_manifest"
  --expected-package-count "$expected_package_count"
  --contract-directory "$contract_directory"
  --evidence-directory "$evidence_directory"
  --phase "$phase"
)
if [[ "$require_authority" = "true" ]]; then
  preflight_arguments+=(
    --authority-issue-url "$authority_issue_url"
    --authority-owner "$authority_owner"
  )
fi

exec "$publication_preflight" "${preflight_arguments[@]}"
