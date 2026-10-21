#!/bin/sh
set -eu
review_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
exec python3 -B "$review_dir/run-overlay-control.py" full-router-manager-review full-router-manager.overlay.json '.' ./pkg/actors/router ./pkg/api/grpc/manager
