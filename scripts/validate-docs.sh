#!/usr/bin/env bash
# Local documentation validation — mirrors docs-validation.yml CI pipeline.
# Usage: ./scripts/validate-docs.sh
set -euo pipefail

DOCS_GLOB='"docs/**/*.md" "README.md" "CONTRIBUTING.md" "CHANGELOG.md" "CODE_OF_CONDUCT.md"'

CURRENT_STAGE=""

on_stage_failure() {
  local exit_code=$?
  if [[ -n "${CURRENT_STAGE}" ]]; then
    echo "FAILED: ${CURRENT_STAGE}"
    echo ""
    echo "Validation failed at ${CURRENT_STAGE}. Fix the errors above and re-run."
  fi
  exit "$exit_code"
}

trap on_stage_failure ERR

# --- Prerequisite checks ---
for cmd in node npx lychee dotnet; do
  if ! command -v "$cmd" &>/dev/null; then
    echo "ERROR: '$cmd' is not installed or not in PATH."
    echo "See docs/getting-started/prerequisites.md for installation instructions."
    exit 1
  fi
done

# --- Stage 1: Markdown Linting ---
echo ""
echo "=== Stage 1/6: Markdown Linting ==="
CURRENT_STAGE="Markdown linting"
eval npx markdownlint-cli2 $DOCS_GLOB
echo "PASSED: Markdown linting"

# --- Stage 2: Link Checking ---
echo ""
echo "=== Stage 2/6: Link Checking ==="
CURRENT_STAGE="Link checking"
eval lychee --config lychee.toml $DOCS_GLOB
echo "PASSED: Link checking"

# --- Stage 3: Sample Build & Test ---
echo ""
echo "=== Stage 3/6: Sample Build & Test ==="
CURRENT_STAGE="Sample build & test"
dotnet restore samples/Hexalith.EventStore.Sample.Tests/
dotnet restore tests/Hexalith.EventStore.Sample.Tests/
dotnet build samples/Hexalith.EventStore.Sample.Tests/ --configuration Release --no-restore
dotnet build tests/Hexalith.EventStore.Sample.Tests/ --configuration Release --no-restore
dotnet test tests/Hexalith.EventStore.Sample.Tests/ --configuration Release --no-build
dotnet test samples/Hexalith.EventStore.Sample.Tests/ --configuration Release --no-build
echo "PASSED: Sample build & test"

# --- Stage 4: DAPR SDK Version Pin Consistency ---
echo ""
echo "=== Stage 4/6: DAPR SDK Version Pin Consistency ==="
CURRENT_STAGE="DAPR SDK version pin consistency"
bash scripts/check-doc-versions.sh
echo "PASSED: DAPR SDK version pin consistency"

# --- Stage 5: Operational Evidence Validator Fixtures ---
echo ""
echo "=== Stage 5/6: Operational Evidence Validator Fixtures ==="
CURRENT_STAGE="Operational evidence validator fixtures"
bash scripts/validate-evidence.sh --self-test
echo "PASSED: Operational evidence validator fixtures"

# --- Stage 6: Deferred-Work Governance Report (advisory) ---
echo ""
echo "=== Stage 6/6: Deferred-Work Governance Report (advisory) ==="
CURRENT_STAGE="Deferred-work governance report"
if ! bash scripts/check-deferred-work.sh _bmad-output/implementation-artifacts/deferred-work.md; then
  echo "ADVISORY: Deferred-work governance reported blocking findings; local docs validation does not fail on legacy ledger findings yet."
fi
echo "PASSED: Deferred-work governance advisory report completed"

CURRENT_STAGE=""
trap - ERR

echo ""
echo "=== All validations passed ==="
