#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if command -v python3 &>/dev/null; then
  PYTHON_BIN=python3
elif command -v python &>/dev/null; then
  PYTHON_BIN=python
elif command -v py &>/dev/null; then
  PYTHON_BIN=py
else
  echo "ERROR: Python is required to run scripts/validate-operational-evidence.py." >&2
  exit 1
fi

"${PYTHON_BIN}" "${REPO_ROOT}/scripts/validate-operational-evidence.py" "$@"
