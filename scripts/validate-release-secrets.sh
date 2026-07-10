#!/usr/bin/env bash
set -euo pipefail

has_value() {
  local value="${1:-}"
  value="$(printf '%s' "$value" | tr -d '[:space:]')"
  [ -n "$value" ]
}

fail() {
  echo "[release-secrets] $1" >&2
  exit 1
}

has_value "${NUGET_API_KEY:-}" || fail "NUGET_API_KEY is required before publishing NuGet packages."

if has_value "${HEXALITH_REQUIRE_CONTAINER_PUBLISHER:-}" ||
  has_value "${HEXALITH_CONTAINER_PROJECTS:-}" ||
  [ -e ./.hexalith/release/publish-containers.sh ]; then
  has_value "${HEXALITH_CONTAINER_PROJECTS:-}" || fail "HEXALITH_CONTAINER_PROJECTS is required before publishing containers."
  [ -f ./.hexalith/release/publish-containers.sh ] || fail "Container publisher script is required before publishing containers."
  [ -x ./.hexalith/release/publish-containers.sh ] || fail "Container publisher script must be executable before publishing containers."
  has_value "${HEXALITH_ZOT_USERNAME:-}" || fail "HEXALITH_ZOT_USERNAME is required before publishing containers."
  has_value "${HEXALITH_ZOT_API_KEY:-}" || fail "HEXALITH_ZOT_API_KEY is required before publishing containers."
fi
