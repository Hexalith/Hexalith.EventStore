#!/usr/bin/env bash
# Run a foreground job with a private, automatically removed scratch directory.
# Requires Linux with GNU env and util-linux setsid.
# Usage: run-with-temp COMMAND [ARGUMENT ...]
#
# TMPDIR, TMP, and TEMP point into the job directory. Create disposable source
# copies and build outputs there, and save useful logs/results elsewhere.
# Wait for background work before returning; cleanup removes the entire tree.
set -euo pipefail
set +m

if [ "$#" -eq 0 ]; then
    echo "Usage: run-with-temp COMMAND [ARGUMENT ...]" >&2
    exit 2
fi
if [ "$1" = "--help" ] || [ "$1" = "-h" ]; then
    echo "Usage: run-with-temp COMMAND [ARGUMENT ...]"
    echo "Disposable workspaces belong inside TMPDIR; keep results elsewhere."
    exit 0
fi

job_tmp=$(mktemp -d "${TMPDIR:-/tmp}/hexalith-job.XXXXXXXX")

cleanup() {
    local command_status=$?
    trap - EXIT
    if ! rm -rf -- "$job_tmp" 2>/dev/null; then
        # Restore only directory permissions; files may be shared hard links.
        find "$job_tmp" -type d ! -perm -u=rwx -exec chmod u+rwx -- {} \; 2>/dev/null || true
        if ! rm -rf -- "$job_tmp"; then
            if [ "$command_status" -eq 0 ]; then
                command_status=1
            fi
        fi
    fi
    exit "$command_status"
}

job_pid=
cancel_job() {
    trap '' INT TERM
    if [ -n "$job_pid" ]; then
        kill -s "$1" -- "-$job_pid" 2>/dev/null || kill -s "$1" "$job_pid" 2>/dev/null || true
        wait "$job_pid" 2>/dev/null || true
    fi
    exit "$2"
}

trap cleanup EXIT
trap 'cancel_job INT 130' INT
trap 'cancel_job TERM 143' TERM

# systemd-tmpfiles skips locked directories, protecting long-running jobs.
if command -v flock >/dev/null 2>&1; then
    exec {job_lock}<"$job_tmp"
    flock --shared "$job_lock"
fi

export TMPDIR="$job_tmp" TMP="$job_tmp" TEMP="$job_tmp"
# A separate process group lets cancellation reach the job and its children.
# Reset INT because asynchronous shell commands otherwise inherit it ignored.
env --default-signal=INT --default-signal=TERM setsid --wait -- "$@" <&0 &
job_pid=$!
wait "$job_pid"
