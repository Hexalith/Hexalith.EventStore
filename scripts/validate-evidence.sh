#!/usr/bin/env bash
set -euo pipefail

if command -v python3 &>/dev/null; then
  PYTHON_BIN=python3
elif command -v python &>/dev/null; then
  PYTHON_BIN=python
else
  echo "ERROR: Python is required to run scripts/validate-operational-evidence.py." >&2
  exit 1
fi

"${PYTHON_BIN}" scripts/validate-operational-evidence.py "$@"
