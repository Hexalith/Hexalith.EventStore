#!/usr/bin/env bash
set -euo pipefail
: > "$PREFLIGHT_INVOCATION_MARKER"
printf '%s\n' "$@" > "$PREFLIGHT_ARGUMENTS"
