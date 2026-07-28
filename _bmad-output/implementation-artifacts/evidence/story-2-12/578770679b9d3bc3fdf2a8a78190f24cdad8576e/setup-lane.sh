#!/usr/bin/env bash
# Story 2.12 — create one pristine, isolated Tenants working copy for a single lane.
# Usage: setup-lane.sh <dest-dir> <tenants-sha>
set -euo pipefail

DEST="$1"
TENANTS_SHA="$2"
REFS=/home/administrator/projects/hexalith
TEN_URL=https://github.com/Hexalith/Hexalith.Tenants.git

ref_for() {
  case "$1" in
    Hexalith.EventStore)              echo "$REFS/eventstore" ;;
    Hexalith.Commons)                 echo "$REFS/commons" ;;
    Hexalith.AI.Tools)                echo "$REFS/aitools" ;;
    Hexalith.FrontComposer)           echo "$REFS/frontcomposer" ;;
    Hexalith.Builds)                  echo "$REFS/builds" ;;
    Hexalith.PolymorphicSerializations) echo "$REFS/polymorphicserializations" ;;
    Hexalith.Memories)                echo "$REFS/memories" ;;
    *) return 1 ;;
  esac
}

die() { printf 'SETUP_LANE_FAIL: %s\n' "$1" >&2; exit 1; }

# `rm -rf` on an unvalidated $1 is unrecoverable. `set -u` only catches an *unset* variable, not a
# mistyped path, an existing repository, or a home directory. Confine the destructive step to the
# scratch tree this story uses (2026-07-28 code review).
case "$DEST" in
  /home/*/tmp-story-2-12/*) ;;
  *) die "refusing to rm -rf a destination outside /home/*/tmp-story-2-12/: '$DEST'" ;;
esac
[ -e "$DEST" ] && [ ! -e "$DEST/.git" ] \
  && die "refusing to rm -rf '$DEST': it exists but is not a git working copy"

# A branch, tag, or ambiguous abbreviation would silently build the lane at a different commit
# than the SHA the receipt names. Require a full 40-hex commit id.
[[ "$TENANTS_SHA" =~ ^[0-9a-f]{40}$ ]] || die "tenants sha must be a full 40-hex commit: '$TENANTS_SHA'"

rm -rf -- "$DEST"
git clone --quiet --dissociate --reference "$REFS/tenants" "$TEN_URL" "$DEST"
cd "$DEST"
# Resolve first: a failing command substitution inside the checkout argument would otherwise pass an
# empty string to `git checkout` rather than aborting (`set -e` does not fail the outer command).
RESOLVED_SHA="$(git rev-parse --verify --end-of-options "${TENANTS_SHA}^{commit}")" \
  || die "commit $TENANTS_SHA does not exist in $TEN_URL"
test -n "$RESOLVED_SHA" || die "could not resolve $TENANTS_SHA to a commit"
git checkout --quiet --detach "$RESOLVED_SHA"
test "$(git rev-parse HEAD)" = "$TENANTS_SHA" \
  || die "checkout landed on $(git rev-parse HEAD), not $TENANTS_SHA"

# Root-declared submodules only. One at a time, never --recursive, never --remote.
# Read the declared *path* rather than deriving it from the submodule name: the two need not match,
# and a mismatch would silently skip that submodule.
git submodule init >/dev/null
declared=0
while read -r key path; do
  name="${key#submodule.}"; name="${name%.path}"
  refrepo="$(ref_for "$(basename "$path")")" \
    || die "no local reference repository configured for submodule '$name' (path '$path')"
  git submodule update --init --reference "$refrepo" --dissociate -- "$path" >/dev/null \
    || die "submodule update failed for '$path'"
  declared=$((declared + 1))
done < <(git config -f .gitmodules --get-regexp '^submodule\..*\.path$')
test "$declared" -gt 0 || die "no root-declared submodules found in .gitmodules"

# The receipt's lane-isolation claim rests on --dissociate having completed. Assert it here rather
# than asserting it in prose afterwards.
if find "$DEST" -path '*/objects/info/alternates' -print -quit | grep -q .; then
  die "lane shares an object store — objects/info/alternates survived --dissociate"
fi

echo "LANE_READY $DEST @ $(git rev-parse HEAD) submodules=$declared alternates=none"
