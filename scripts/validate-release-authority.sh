#!/usr/bin/env bash
set -euo pipefail

version="${1:-}"
authority_url="${HEXALITH_RELEASE_AUTHORITY_URL:-}"
builds_execution_sha="${HEXALITH_BUILDS_EXECUTION_SHA:-}"
registry="${HEXALITH_ZOT_REGISTRY:-registry.hexalith.com}"
source_sha="${GITHUB_SHA:-}"
contract_directory="${HEXALITH_RELEASE_CONTRACT_DIRECTORY:-$PWD/.hexalith/release}"
authority_validator="${HEXALITH_PUBLICATION_AUTHORITY_VALIDATOR:-./.hexalith/release/publication_authority.py}"
evidence_directory="${HEXALITH_RELEASE_EVIDENCE_DIRECTORY:-$PWD/.hexalith/release-evidence/$version/authority}"

fail() {
  echo "[release-authority] $1" >&2
  exit 1
}

[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]] ||
  fail "A plain semantic release version is required."
[[ "$builds_execution_sha" =~ ^[0-9a-f]{40}$ ]] ||
  fail "HEXALITH_BUILDS_EXECUTION_SHA must be an exact lowercase commit SHA."
[[ "$source_sha" =~ ^[0-9a-f]{40}$ ]] ||
  fail "GITHUB_SHA must identify the exact workflow source commit."
[[ "$authority_url" =~ ^https:// ]] ||
  fail "HEXALITH_RELEASE_AUTHORITY_URL must be a durable HTTPS source."
[ "$registry" = "registry.hexalith.com" ] ||
  fail "The authorized EventStore container registry must be registry.hexalith.com."
[ -x "$authority_validator" ] ||
  fail "The shared publication authority validator is unavailable."

exec "$authority_validator" \
  --authority-url "$authority_url" \
  --repository "Hexalith/Hexalith.EventStore" \
  --version "$version" \
  --source-sha "$source_sha" \
  --container-repository "registry.hexalith.com/eventstore" \
  --builds-execution-sha "$builds_execution_sha" \
  --package-manifest "tools/release-packages.json" \
  --contract-directory "$contract_directory" \
  --evidence-directory "$evidence_directory"
