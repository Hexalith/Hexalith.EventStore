#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

if command -v python3 >/dev/null 2>&1; then
  PYTHON=python3
elif command -v python >/dev/null 2>&1; then
  PYTHON=python
else
  echo "Python is required to run deferred-work governance checks." >&2
  exit 2
fi

cd "$REPO_ROOT"
exec "$PYTHON" "$SCRIPT_DIR/check-deferred-work.py" "$@"
