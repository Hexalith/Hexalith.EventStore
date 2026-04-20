#!/usr/bin/env bash
# Local CI mirror — reproduces the test sequence run by .github/workflows/ci.yml.
# Usage:
#   ./scripts/ci-local.sh                  # Tier 1 only (default, no DAPR required)
#   ./scripts/ci-local.sh --tier 1+2       # Tier 1 + Tier 2 (requires `dapr init`)
#   ./scripts/ci-local.sh --tier 1+2+3     # all tiers (requires `dapr init` + Aspire prerequisites)
#   ./scripts/ci-local.sh --tier 2         # Tier 2 only
#   ./scripts/ci-local.sh --tier 3         # Tier 3 only
#   ./scripts/ci-local.sh --skip-build     # reuse existing Release build artifacts
set -euo pipefail

TIERS="1"
SKIP_BUILD=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tier)
      TIERS="$2"
      shift 2
      ;;
    --skip-build)
      SKIP_BUILD=1
      shift
      ;;
    -h|--help)
      sed -n '2,9p' "$0"
      exit 0
      ;;
    *)
      echo "ERROR: unknown argument '$1'. Use --help for usage."
      exit 1
      ;;
  esac
done

CURRENT_STAGE=""

on_stage_failure() {
  local exit_code=$?
  if [[ -n "${CURRENT_STAGE}" ]]; then
    echo ""
    echo "FAILED: ${CURRENT_STAGE}"
    echo "Fix the errors above and re-run. To skip rebuilding, pass --skip-build."
  fi
  exit "$exit_code"
}

trap on_stage_failure ERR

# --- Prerequisites ---
if ! command -v dotnet &>/dev/null; then
  echo "ERROR: 'dotnet' is not installed or not in PATH."
  exit 1
fi

want_tier() { [[ ",${TIERS}," == *",$1,"* || "${TIERS}" == *"$1"* ]]; }

if want_tier 2 || want_tier 3; then
  if ! command -v dapr &>/dev/null; then
    echo "ERROR: 'dapr' CLI required for Tier ${TIERS}. Install: https://docs.dapr.io/getting-started/install-dapr-cli/"
    exit 1
  fi
  if ! dapr status -k &>/dev/null && ! docker ps --format '{{.Names}}' 2>/dev/null | grep -q '^dapr_placement$'; then
    echo "WARNING: DAPR may not be initialized. If tests fail to connect, run: dapr init"
  fi
fi

# --- Build (mirrors CI: restore once, build once, then test --no-build) ---
if [[ "${SKIP_BUILD}" -eq 0 ]]; then
  echo "=== Restore ==="
  CURRENT_STAGE="Restore"
  dotnet restore Hexalith.EventStore.slnx

  echo ""
  echo "=== Build (Release) ==="
  CURRENT_STAGE="Build"
  dotnet build Hexalith.EventStore.slnx --no-restore --configuration Release
fi

TIER1_PROJECTS=(
  "tests/Hexalith.EventStore.Contracts.Tests"
  "tests/Hexalith.EventStore.Client.Tests"
  "tests/Hexalith.EventStore.Testing.Tests"
  "tests/Hexalith.EventStore.Sample.Tests"
  "tests/Hexalith.EventStore.SignalR.Tests"
  "tests/Hexalith.EventStore.Admin.Cli.Tests"
  "tests/Hexalith.EventStore.Admin.Mcp.Tests"
  "tests/Hexalith.EventStore.Admin.Abstractions.Tests"
  "tests/Hexalith.EventStore.Admin.Server.Tests"
  "tests/Hexalith.EventStore.Admin.Server.Host.Tests"
  "tests/Hexalith.EventStore.Admin.UI.Tests"
)

run_test_project() {
  local project="$1"
  local logger_name="$2"
  echo "  -> ${project}"
  dotnet test "${project}" \
    --no-build \
    --configuration Release \
    --logger "trx;LogFileName=${logger_name}" \
    --collect:"XPlat Code Coverage"
}

# --- Tier 1 ---
if want_tier 1; then
  echo ""
  echo "=== Tier 1 — Unit Tests (${#TIER1_PROJECTS[@]} projects) ==="
  CURRENT_STAGE="Tier 1 unit tests"
  for project in "${TIER1_PROJECTS[@]}"; do
    run_test_project "${project}" "test-results.trx"
  done
fi

# --- Tier 2 ---
if want_tier 2; then
  echo ""
  echo "=== Tier 2 — Integration Tests ==="
  CURRENT_STAGE="Tier 2 integration tests"
  run_test_project "tests/Hexalith.EventStore.Server.Tests" "integration-results.trx"
fi

# --- Tier 3 ---
if want_tier 3; then
  echo ""
  echo "=== Tier 3 — Aspire Contract Tests ==="
  CURRENT_STAGE="Tier 3 Aspire contract tests"
  dotnet test tests/Hexalith.EventStore.IntegrationTests/ --configuration Release
fi

CURRENT_STAGE=""
trap - ERR

echo ""
echo "=== All requested tiers passed (TIERS=${TIERS}) ==="
