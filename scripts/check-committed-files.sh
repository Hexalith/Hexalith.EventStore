#!/usr/bin/env bash
# Rejects committed evidence and oversized files.
#
# Proof is the repository's tests plus CI. A story or spec records the commands run,
# pass/fail counts, commit SHA and CI run link; evidence folders are ignored local scratch.
#
# Usage:
#   scripts/check-committed-files.sh            check every tracked index entry (CI)
#   scripts/check-committed-files.sh --staged   check only staged additions and modifications (pre-commit)
#
# Rules:
#   - no path under _bmad-output/**/evidence/ or docs/evidence/ (even when force-added;
#     matched case-insensitively);
#   - no file larger than 1 MiB (1,048,576 bytes) unless explicitly allowlisted below.
# Gitlinks (submodule pointers) and deletions are ignored.
#
# Exit codes: 0 = pass, 1 = violations found, 2 = usage or Git error.
set -euo pipefail

readonly max_bytes=1048576
readonly allowlist=(
    "docs/assets/quickstart-demo.gif"
    "_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md"
)

usage() {
    echo "Usage: $0 [--staged]" >&2
}

mode="tracked"
if [ "$#" -gt 1 ]; then
    usage
    exit 2
fi
case "${1-}" in
    "") ;;
    --staged) mode="staged" ;;
    -h | --help)
        usage
        exit 0
        ;;
    *)
        usage
        exit 2
        ;;
esac

if ! repo_root="$(git rev-parse --show-toplevel 2>/dev/null)"; then
    echo "check-committed-files: not inside a Git working tree." >&2
    exit 2
fi
cd "$repo_root"

is_evidence_path() {
    # Case-insensitive so Evidence/ or EVIDENCE/ cannot slip past the rule.
    local matched=1
    shopt -s nocasematch
    case "$1" in
        _bmad-output/evidence/* | _bmad-output/*/evidence/* | docs/evidence/*) matched=0 ;;
    esac
    shopt -u nocasematch
    return "$matched"
}

is_allowlisted() {
    local candidate
    for candidate in "${allowlist[@]}"; do
        if [ "$1" = "$candidate" ]; then
            return 0
        fi
    done
    return 1
}

paths=()
objects=()

# The NUL-separated listing is captured to a file first so a failing Git command
# stops the guard instead of looking like an empty, passing candidate set.
if ! listing="$(mktemp)"; then
    echo "check-committed-files: could not create a temporary file." >&2
    exit 2
fi
trap 'rm -f "$listing"' EXIT

# Collect "<mode> <object>" and path pairs for every candidate entry, skipping gitlinks.
if [ "$mode" = "staged" ]; then
    if ! base="$(git rev-parse --verify --quiet 'HEAD^{tree}')"; then
        if ! base="$(git hash-object -t tree /dev/null)"; then
            echo "check-committed-files: could not compute the empty tree." >&2
            exit 2
        fi
    fi
    # Raw records are ":<src mode> <dst mode> <src object> <dst object> <status>" NUL "<path>" NUL.
    # The lowercase 'd' filter excludes deletions.
    if ! git diff --cached --raw --no-abbrev --no-renames --no-color --no-ext-diff -z --diff-filter=d "$base" -- >"$listing"; then
        echo "check-committed-files: could not list staged changes." >&2
        exit 2
    fi
    while IFS= read -r -d '' header && IFS= read -r -d '' path; do
        read -r _ entry_mode _ object _ <<<"${header#:}"
        if [ "$entry_mode" = "160000" ]; then
            continue
        fi
        paths+=("$path")
        objects+=("$object")
    done <"$listing"
else
    # Index records are "<mode> <object> <stage>" TAB "<path>" NUL.
    if ! git ls-files --stage -z >"$listing"; then
        echo "check-committed-files: could not list tracked files." >&2
        exit 2
    fi
    while IFS= read -r -d '' record; do
        header="${record%%$'\t'*}"
        path="${record#*$'\t'}"
        read -r entry_mode object _ <<<"$header"
        if [ "$entry_mode" = "160000" ]; then
            continue
        fi
        paths+=("$path")
        objects+=("$object")
    done <"$listing"
fi

violations=0
evidence_found=0
large_found=0

if [ "${#paths[@]}" -gt 0 ]; then
    sizes=()
    if ! size_listing="$(printf '%s\n' "${objects[@]}" | git cat-file --batch-check='%(objectsize)')"; then
        echo "check-committed-files: could not read candidate object sizes." >&2
        exit 2
    fi
    while IFS= read -r size; do
        sizes+=("$size")
    done <<<"$size_listing"

    if [ "${#sizes[@]}" -ne "${#paths[@]}" ]; then
        echo "check-committed-files: could not read the size of every candidate object." >&2
        exit 2
    fi

    for index in "${!paths[@]}"; do
        path="${paths[$index]}"
        size="${sizes[$index]}"
        if is_evidence_path "$path"; then
            echo "check-committed-files: evidence path must not be committed: $path" >&2
            evidence_found=1
            violations=$((violations + 1))
        fi
        case "$size" in
            '' | *[!0-9]*)
                echo "check-committed-files: unreadable object for $path ($size)." >&2
                exit 2
                ;;
        esac
        if [ "$size" -gt "$max_bytes" ] && ! is_allowlisted "$path"; then
            echo "check-committed-files: file exceeds $max_bytes bytes: $path ($size bytes)" >&2
            large_found=1
            violations=$((violations + 1))
        fi
    done
fi

if [ "$violations" -gt 0 ]; then
    echo "check-committed-files: $violations violation(s) found." >&2
    if [ "$evidence_found" -eq 1 ]; then
        echo "Fix: evidence folders are ignored local scratch. Record the commands run, pass/fail counts," >&2
        echo "     commit SHA and CI run link in the story or spec, then: git rm --cached -- <path>" >&2
    fi
    if [ "$large_found" -eq 1 ]; then
        echo "Fix: do not commit files over 1 MiB; keep them local or publish them as a CI artifact," >&2
        echo "     then: git rm --cached -- <path>. Only scripts/check-committed-files.sh may allowlist one." >&2
    fi
    exit 1
fi

if [ "$mode" = "staged" ]; then
    echo "check-committed-files: staged files pass (${#paths[@]} checked)."
else
    echo "check-committed-files: tracked files pass (${#paths[@]} checked)."
fi
