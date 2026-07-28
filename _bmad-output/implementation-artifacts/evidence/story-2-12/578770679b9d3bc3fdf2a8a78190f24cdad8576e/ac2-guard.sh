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
ROOT_DECLARED="$(git config -f .gitmodules --get-regexp '^submodule\..*\.path$' | awk '{print $2}' | sort)"
INITIALIZED="$(git submodule status | awk '{print $2}' | sort)"
test "$ROOT_DECLARED" = "$INITIALIZED" \
  || fail "initialized submodule set != root-declared set"
NESTED_INIT=0
while read -r sub; do
  [ -f "$sub/.gitmodules" ] || continue
  while read -r nested; do
    if [ -e "$sub/$nested/.git" ]; then
      printf 'nested submodule initialized: %s/%s\n' "$sub" "$nested" >&2
      NESTED_INIT=1
    fi
  done < <(git -C "$sub" config -f .gitmodules --get-regexp '^submodule\..*\.path$' | awk '{print $2}')
done <<< "$INITIALIZED"
test "$NESTED_INIT" -eq 0 || fail "a nested submodule was initialized"

printf 'AC2_GUARD_OK\n'
printf 'TENANTS_SHA=%s\n' "$TENANTS_SHA"
printf 'EVENTSTORE_GITLINK_SHA=%s\n' "$GITLINK_SHA"
printf 'EVENTSTORE_CHECKOUT_SHA=%s\n' "$CHECKOUT_SHA"
printf 'EVENTSTORE_ORIGIN_MAIN=%s\n' "$OFFICIAL_MAIN"
printf 'BUILDS_GITLINK_SHA=%s\n' "$(git ls-tree --object-only HEAD references/Hexalith.Builds)"
printf 'ROOT_DECLARED_SUBMODULES=%s\n' "$(echo "$ROOT_DECLARED" | tr '\n' ' ')"
