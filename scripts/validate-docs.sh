#!/usr/bin/env bash
# Local documentation validation — mirrors docs-validation.yml CI pipeline.
# Usage: ./scripts/validate-docs.sh
set -euo pipefail

DOCS_GLOB='"docs/**/*.md" "README.md" "CONTRIBUTING.md" "CHANGELOG.md" "CODE_OF_CONDUCT.md"'

# --- Prerequisite checks ---
for cmd in npx lychee dotnet; do
  if ! command -v "$cmd" &>/dev/null; then
    echo "ERROR: '$cmd' is not installed or not in PATH."
    echo "See docs/getting-started/prerequisites.md for installation instructions."
    exit 1
  fi
done

# --- Stage 1: Markdown Linting ---
echo ""
echo "=== Stage 1/3: Markdown Linting ==="
eval npx markdownlint-cli2 $DOCS_GLOB
echo "PASSED: Markdown linting"

# --- Stage 2: Link Checking ---
echo ""
echo "=== Stage 2/3: Link Checking ==="
eval lychee --config lychee.toml $DOCS_GLOB
echo "PASSED: Link checking"

# --- Stage 3: Sample Build & Test ---
echo ""
echo "=== Stage 3/3: Sample Build & Test ==="
dotnet build samples/Hexalith.EventStore.Sample.Tests/ --configuration Release
dotnet build tests/Hexalith.EventStore.Sample.Tests/ --configuration Release
dotnet test tests/Hexalith.EventStore.Sample.Tests/ --configuration Release --no-build
dotnet test samples/Hexalith.EventStore.Sample.Tests/ --configuration Release --no-build
echo "PASSED: Sample build & test"

echo ""
echo "=== All validations passed ==="
