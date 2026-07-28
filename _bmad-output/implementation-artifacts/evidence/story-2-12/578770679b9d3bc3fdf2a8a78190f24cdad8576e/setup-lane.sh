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

rm -rf -- "$DEST"
git clone --quiet --dissociate --reference "$REFS/tenants" "$TEN_URL" "$DEST"
cd "$DEST"
git checkout --quiet --detach "$TENANTS_SHA"

# Root-declared submodules only. One at a time, never --recursive, never --remote.
git submodule init >/dev/null
while read -r name; do
  path="references/$name"
  refrepo="$(ref_for "$name")"
  git submodule update --init --reference "$refrepo" --dissociate -- "$path" >/dev/null
done < <(git config -f .gitmodules --get-regexp '^submodule\..*\.path$' \
          | sed 's/^submodule\.//; s/\.path .*$//')

echo "LANE_READY $DEST @ $(git rev-parse HEAD)"
