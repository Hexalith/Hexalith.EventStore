#!/usr/bin/env bash
set -euo pipefail

version="${1:-}"
phase="${2:-}"
builds_execution_sha="${HEXALITH_BUILDS_EXECUTION_SHA:-}"
registry="${HEXALITH_ZOT_REGISTRY:-registry.hexalith.com}"
source_sha="${GITHUB_SHA:-}"
release_environment="${HEXALITH_RELEASE_ENVIRONMENT:-}"
contract_directory="${HEXALITH_RELEASE_CONTRACT_DIRECTORY:-$PWD/.hexalith/release}"
publication_preflight="${HEXALITH_PUBLICATION_PREFLIGHT:-./.hexalith/release/publication_preflight.py}"
evidence_directory="${HEXALITH_RELEASE_EVIDENCE_DIRECTORY:-$PWD/.hexalith/release-evidence/$version/preflight}"

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
[ "$release_environment" = "production" ] ||
  fail "HEXALITH_RELEASE_ENVIRONMENT must identify the protected production environment."
[ "$registry" = "registry.hexalith.com" ] ||
  fail "The EventStore container registry must be registry.hexalith.com."
[ -x "$publication_preflight" ] ||
  fail "The shared publication preflight is unavailable."
[ -f "tools/release-packages.json" ] ||
  fail "The authoritative release package manifest is unavailable."

exec "$publication_preflight" \
  --repository "Hexalith/Hexalith.EventStore" \
  --version "$version" \
  --source-sha "$source_sha" \
  --container-repository "registry.hexalith.com/eventstore" \
  --builds-execution-sha "$builds_execution_sha" \
  --environment-name "$release_environment" \
  --package-manifest "tools/release-packages.json" \
  --contract-directory "$contract_directory" \
  --evidence-directory "$evidence_directory" \
  --phase "$phase"
