#!/usr/bin/env bash
# Story 2.12 amended-AC2 tracked-source-identity guard.
# Preserves the Story 1.20 source-consumer cleanliness assertions; replaces its frozen
# approved-SHA equality with the amended tracked-`main` identity check (AD-22 scoped exception).
# MUST run on a pristine checkout, BEFORE the lane's restore/build.
# Usage: ac2-guard.sh <consumer-repository>
set -euo pipefail

CONSUMER_REPOSITORY="$1"
EVENTSTORE_SUBMODULE='references/Hexalith.EventStore'
EVENTSTORE_URL='https://github.com/Hexalith/Hexalith.EventStore.git'
cd "$CONSUMER_REPOSITORY"

fail() { printf 'AC2_GUARD_FAIL: %s\n' "$1" >&2; exit 1; }

TENANTS_SHA="$(git rev-parse --verify --end-of-options 'HEAD^{commit}')"
GITLINK_SHA="$(git ls-tree --object-only HEAD "$EVENTSTORE_SUBMODULE")"
CHECKOUT_SHA="$(git -C "$EVENTSTORE_SUBMODULE" rev-parse --verify --end-of-options 'HEAD^{commit}')"

[[ "$GITLINK_SHA"  =~ ^[0-9a-f]{40}$ ]] || fail "gitlink is not a 40-hex sha: $GITLINK_SHA"
[[ "$CHECKOUT_SHA" =~ ^[0-9a-f]{40}$ ]] || fail "checkout is not a 40-hex sha: $CHECKOUT_SHA"

# 1. gitlink == checked-out submodule HEAD
test "$GITLINK_SHA" = "$CHECKOUT_SHA" || fail "gitlink $GITLINK_SHA != checkout $CHECKOUT_SHA"

# 2. that commit is reachable from EventStore origin/main (canonical remote)
test "$(git -C "$EVENTSTORE_SUBMODULE" remote get-url origin)" = "$EVENTSTORE_URL" \
  || fail "EventStore submodule origin is not the canonical GitHub remote"
git -C "$EVENTSTORE_SUBMODULE" fetch --quiet --no-tags origin main
OFFICIAL_MAIN="$(git -C "$EVENTSTORE_SUBMODULE" rev-parse --verify --end-of-options \
  'refs/remotes/origin/main^{commit}')"
git -C "$EVENTSTORE_SUBMODULE" merge-base --is-ancestor "$CHECKOUT_SHA" "$OFFICIAL_MAIN" \
  || fail "$CHECKOUT_SHA is not reachable from EventStore origin/main ($OFFICIAL_MAIN)"

# 3. consumer worktree clean (submodules included)
test -z "$(git status --porcelain=v1 --untracked-files=all --ignore-submodules=none)" \
  || fail "consumer worktree is dirty"

# 4. no EventStore submodule content edited — tracked, untracked, and ignored
test -z "$(git -C "$EVENTSTORE_SUBMODULE" status --porcelain=v1 --untracked-files=all)" \
  || fail "EventStore submodule worktree is dirty"
test -z "$(git -C "$EVENTSTORE_SUBMODULE" status --porcelain=v1 --ignored=matching --untracked-files=all)" \
  || fail "EventStore submodule has ignored/build artifacts (guard must run before restore)"

# 5. only Tenants-root-declared submodules are initialized — no nested submodule initialized
# `git submodule status` lists EVERY declared submodule; an uninitialized one is marked only by a
# leading '-' on field 1, so filtering on that prefix is required. Without it the comparison below
# is a tautology that can never fail (2026-07-28 code review).
ROOT_DECLARED="$(git config -f .gitmodules --get-regexp '^submodule\..*\.path$' | awk '{print $2}' | sort)"
INITIALIZED="$(git submodule status | awk '$1 !~ /^-/ {print $2}' | sort)"
test -n "$ROOT_DECLARED" || fail "no root-declared submodules found in .gitmodules"
test "$ROOT_DECLARED" = "$INITIALIZED" \
  || fail "initialized submodule set != root-declared set"
NESTED_INIT=0
# Iterate the declared set, not the initialized set: an uninitialized submodule cannot hold a
# nested checkout, but iterating INITIALIZED would skip nothing useful and would break when
# INITIALIZED is legitimately empty.
while read -r sub; do
  [ -f "$sub/.gitmodules" ] || continue
  while read -r nested; do
    if [ -e "$sub/$nested/.git" ]; then
      printf 'nested submodule initialized: %s/%s\n' "$sub" "$nested" >&2
      NESTED_INIT=1
    fi
  done < <(git -C "$sub" config -f .gitmodules --get-regexp '^submodule\..*\.path$' | awk '{print $2}')
done <<< "$ROOT_DECLARED"
test "$NESTED_INIT" -eq 0 || fail "a nested submodule was initialized"

# 6. the Builds gitlink must exist, be a real gitlink, and be a 40-hex sha — reported below as
# evidence. The mode check is load-bearing: `git ls-tree --object-only` returns a 40-hex *tree* sha
# for an ordinary directory and a *blob* sha for a file, so the shape test alone would accept a
# submodule that had been replaced by a plain directory and print its tree sha as identity evidence
# (2026-07-28 delta code review).
BUILDS_GITLINK_ENTRY="$(git ls-tree HEAD references/Hexalith.Builds)"
BUILDS_GITLINK_MODE="$(printf '%s' "$BUILDS_GITLINK_ENTRY" | awk '{print $1}')"
BUILDS_GITLINK_TYPE="$(printf '%s' "$BUILDS_GITLINK_ENTRY" | awk '{print $2}')"
BUILDS_GITLINK_SHA="$(printf '%s' "$BUILDS_GITLINK_ENTRY" | awk '{print $3}')"
test "$BUILDS_GITLINK_MODE" = "160000" && test "$BUILDS_GITLINK_TYPE" = "commit" \
  || fail "references/Hexalith.Builds is not a gitlink (mode='$BUILDS_GITLINK_MODE' type='$BUILDS_GITLINK_TYPE')"
[[ "$BUILDS_GITLINK_SHA" =~ ^[0-9a-f]{40}$ ]] \
  || fail "Builds gitlink is not a 40-hex sha: '$BUILDS_GITLINK_SHA'"

printf 'AC2_GUARD_OK\n'
printf 'TENANTS_SHA=%s\n' "$TENANTS_SHA"
printf 'EVENTSTORE_GITLINK_SHA=%s\n' "$GITLINK_SHA"
printf 'EVENTSTORE_CHECKOUT_SHA=%s\n' "$CHECKOUT_SHA"
printf 'EVENTSTORE_ORIGIN_MAIN=%s\n' "$OFFICIAL_MAIN"
printf 'BUILDS_GITLINK_SHA=%s\n' "$BUILDS_GITLINK_SHA"
printf 'ROOT_DECLARED_SUBMODULES=%s\n' "$(echo "$ROOT_DECLARED" | tr '\n' ' ')"
