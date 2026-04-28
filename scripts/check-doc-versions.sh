#!/usr/bin/env bash
# DAPR SDK version pin consistency check — asserts that the four Dapr.*
# table cells in docs/reference/nuget-packages.md match the version
# pinned in Directory.Packages.props (the single source of truth).
#
# Assumptions (rewrite to xmllint or `dotnet msbuild -getProperty:` if violated):
#   1. Directory.Packages.props uses single-line <PackageVersion ... /> entries
#      (multi-line wraps not supported; Condition attributes on the same line are fine).
#   2. Each Dapr.* package appears EXACTLY once in the props file.
#   3. The nuget-packages.md DAPR table rows use the pipe-delimited pattern
#      `| Dapr.X | Y.Y.Y |` with any whitespace padding inside cells.
#   4. Bash 4+ (associative arrays). CI is ubuntu-latest (bash 5+); local-dev
#      on Windows Git Bash 3.2 is unsupported — use WSL or refactor to scalars.
set -euo pipefail
(( BASH_VERSINFO[0] >= 4 )) || { echo "ERROR: bash 4+ required (found $BASH_VERSION); use WSL or upgrade Git Bash" >&2; exit 1; }

PROPS="Directory.Packages.props"
DOC="docs/reference/nuget-packages.md"
[[ -f "$PROPS" ]] || { echo "ERROR: $PROPS not found (cwd=$PWD) — run from repo root" >&2; exit 1; }
[[ -f "$DOC" ]]   || { echo "ERROR: $DOC not found (cwd=$PWD) — run from repo root" >&2; exit 1; }
EXPECTED_DAPR_ROWS=4   # Dapr.Client x2 (Client + Server tables) + Dapr.Actors + Dapr.Actors.AspNetCore

# Pre-flight: detect multi-line <PackageVersion> elements (regex can't handle them).
multiline=$(grep -cE "<PackageVersion[^/]*$" "$PROPS" || true)
if [[ "$multiline" -gt 0 ]]; then
  echo "ERROR: multi-line <PackageVersion> element detected in $PROPS — REWRITE NEEDED." >&2
  echo "       See script header comment for migration to xmllint or 'dotnet msbuild -getProperty:'." >&2
  exit 1
fi

# Extract the four Dapr.* versions from the props file.
declare -A PINS
for pkg in Dapr.Client Dapr.AspNetCore Dapr.Actors Dapr.Actors.AspNetCore; do
  # Anchor to start-of-line + optional whitespace so commented-out
  # (<!-- <PackageVersion ... /> -->) and otherwise-wrapped lines do NOT match.
  matches=$(grep -cE "^[[:space:]]*<PackageVersion Include=\"$pkg\" Version=\"[^\"]+\"" "$PROPS" || true)
  if [[ "$matches" -eq 0 ]]; then
    echo "ERROR: $pkg not found in $PROPS" >&2
    exit 1
  fi
  if [[ "$matches" -gt 1 ]]; then
    echo "ERROR: $pkg appears $matches times in $PROPS (expected exactly 1)" >&2
    exit 1
  fi
  PINS[$pkg]=$(grep -oE "^[[:space:]]*<PackageVersion Include=\"$pkg\" Version=\"[^\"]+\"" "$PROPS" \
              | sed -E "s/.*Version=\"([^\"]+)\".*/\\1/")
done

# Internal consistency: all four Dapr.* must share the same version.
first="${PINS[Dapr.Client]}"
for pkg in "${!PINS[@]}"; do
  if [[ "${PINS[$pkg]}" != "$first" ]]; then
    echo "ERROR: $PROPS has divergent Dapr.* pins:" >&2
    for p in "${!PINS[@]}"; do echo "  $p = ${PINS[$p]}" >&2; done
    exit 1
  fi
done
EXPECTED="$first"

# Walk doc cells and assert match. Count rows to detect silent-pass on empty doc.
fail=0
rows_seen=0
while IFS= read -r line; do
  rows_seen=$((rows_seen + 1))
  ln="${line%%:*}"
  row="${line#*:}"
  pkg=$(echo "$row" | sed -E "s/^\|[[:space:]]+([A-Za-z.]+)[[:space:]]+\|.*$/\\1/")
  ver=$(echo "$row" | sed -E "s/^\|[[:space:]]+[A-Za-z.]+[[:space:]]+\|[[:space:]]+([0-9.]+)[[:space:]]+\|.*$/\\1/")
  if [[ "$ver" != "$EXPECTED" ]]; then
    echo "MISMATCH at $DOC:$ln: '$pkg' cell shows '$ver', $PROPS pins '$EXPECTED'" >&2
    fail=1
  fi
done < <(grep -nE "^\|[[:space:]]+Dapr\.(Client|Actors|AspNetCore|Actors\.AspNetCore)[[:space:]]+\|[[:space:]]+[0-9.]+[[:space:]]+\|" "$DOC" || true)

if [[ "$rows_seen" -ne "$EXPECTED_DAPR_ROWS" ]]; then
  echo "ERROR: expected exactly $EXPECTED_DAPR_ROWS Dapr.* table rows in $DOC, found $rows_seen" >&2
  exit 1
fi
if [[ "$fail" -ne 0 ]]; then exit 1; fi
echo "PASSED: DAPR SDK version pin consistency ($EXPECTED, $rows_seen rows verified)"
