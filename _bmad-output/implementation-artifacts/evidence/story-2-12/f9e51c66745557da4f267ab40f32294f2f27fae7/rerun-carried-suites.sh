#!/usr/bin/env bash
# Story 2.12 — re-run the three suites the focused delta carried forward, at the accepted
# Tenants SHA f9e51c66745557da4f267ab40f32294f2f27fae7, in BOTH dependency modes.
#
# Added by the 2026-07-28 delta code review (owner decision: re-run rather than waive).
# The carried-forward counts were measured at the superseded `578770679b9d`, which is 16 commits
# and 4006 insertions behind this SHA, including 19 production files under `src/`. `IntegrationTests`
# is additionally the AD-12 persisted-path lane that the AD-22 scoped exception preserves unchanged.
#
# Unlike `run-lanes.sh`, every step's exit code is captured and the script fails closed: a lane that
# cannot run its suites must not read as a lane whose suites passed (2026-07-28 code review).
set -uo pipefail

SRC=/home/administrator/tmp-story-2-12/src-lane-f9e51c6
PKG=/home/administrator/tmp-story-2-12/pkg-lane-f9e51c6
OUT=/home/administrator/tmp-story-2-12/logs-rerun-f9e51c6
EXPECTED_SHA=f9e51c66745557da4f267ab40f32294f2f27fae7
mkdir -p "$OUT"

SRCPROPS=(-p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false)
PKGPROPS=(-p:UseHexalithProjectReferences=false)
SUITES=(Server.Tests UI.Tests IntegrationTests)

FAILED=0

# Identity first: a suite result is only evidence for the SHA the lane is actually at.
for lane in "$SRC" "$PKG"; do
  actual="$(git -C "$lane" rev-parse HEAD 2>/dev/null)"
  if [ "$actual" != "$EXPECTED_SHA" ]; then
    printf 'RERUN_FAIL: lane %s is at %s, expected %s\n' "$lane" "$actual" "$EXPECTED_SHA" >&2
    exit 2
  fi
  printf 'LANE_IDENTITY_OK %s = %s\n' "$lane" "$actual"
done

run_suite() {
  local lane="$1" mode="$2" cfg="$3" suite="$4"; shift 4
  local log="$OUT/${mode}-test-${suite}.log"
  printf '\n>>>>> %s / %s\n' "$mode" "$suite"
  ( cd "$lane" && dotnet test "tests/Hexalith.Tenants.${suite}/Hexalith.Tenants.${suite}.csproj" \
      --configuration "$cfg" --no-build --no-restore "$@" -nodeReuse:false -m:1 ) \
    >"$log" 2>&1
  local rc=$?
  printf '%s_%s_EXIT=%d\n' "$mode" "$suite" "$rc"
  [ $rc -ne 0 ] && FAILED=1
  grep -E "^\s*(Passed|Failed)!" "$log" | tail -2 || echo "  (no test summary line — see $log)"
  return 0
}

for suite in "${SUITES[@]}"; do
  run_suite "$SRC" src Debug   "$suite" "${SRCPROPS[@]}"
done
for suite in "${SUITES[@]}"; do
  run_suite "$PKG" pkg Release "$suite" "${PKGPROPS[@]}"
done

printf '\nRERUN_ANY_FAILURE=%d\n' "$FAILED"
[ $FAILED -eq 0 ] && echo "RERUN_OK" || echo "RERUN_HAD_FAILURES"
