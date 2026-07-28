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
#
# Canonicalize BEFORE matching. A `case` glob's `*` also matches `/`, so the raw pattern accepted
# `/home/<user>/tmp-story-2-12/../../<user>/projects/hexalith/eventstore` — and the "is it a git
# working copy" check below did NOT save it, because a real repository *does* have `.git`, so the
# die never fired and `rm -rf` would have run on the live checkout (2026-07-28 delta code review).
DEST="$(realpath -m -- "$DEST")" || die "could not canonicalize destination '$1'"
case "$DEST" in
  */../*|*/..) die "refusing a destination containing '..' after canonicalization: '$DEST'" ;;
esac
case "$DEST" in
  /home/*/tmp-story-2-12/*) ;;
  *) die "refusing to rm -rf a destination outside /home/*/tmp-story-2-12/: '$DEST'" ;;
esac
# Refuse anything that already exists and is not a lane we created: a non-repository (the original
# check) *and* any repository that is not a detached scratch clone. Belt and braces — the prefix
# check above is the real guard; this one only narrows the blast radius if it is ever loosened.
if [ -e "$DEST" ] && [ ! -e "$DEST/.git" ]; then
  die "refusing to rm -rf '$DEST': it exists but is not a git working copy"
fi

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
#
# `find … | grep -q .` reported "no alternates" both when there were none and when `find` itself
# failed on an unreadable subtree — so the sole assertion behind the isolation claim could report
# success because the search never ran (2026-07-28 delta code review). Capture find's own status.
alternates_out="$(find "$DEST" -path '*/objects/info/alternates' -print 2>"$DEST/.find-stderr")"
find_rc=$?
find_err="$(cat "$DEST/.find-stderr" 2>/dev/null)"; rm -f -- "$DEST/.find-stderr"
test "$find_rc" -eq 0 \
  || die "could not search for alternates (find exit $find_rc): ${find_err:-no stderr}"
test -z "$find_err" \
  || die "alternates search was incomplete, so isolation is unproved: $find_err"
test -z "$alternates_out" \
  || die "lane shares an object store — objects/info/alternates survived --dissociate: $alternates_out"

echo "LANE_READY $DEST @ $(git rev-parse HEAD) submodules=$declared alternates=none"
