#!/usr/bin/env bash
# Story 2.12 focused delta at Tenants f9e51c66745557da4f267ab40f32294f2f27fae7.
# Two pre-existing isolated lanes; each restores itself with --force-evaluate.
#
# SCOPE (corrected 2026-07-28 by the delta code review): this driver runs restore, build and the
# Contracts.Tests suite only. It does NOT invoke setup-lane.sh, ac2-guard.sh, or analyze-assets.py —
# the AC2 identity guard and both graph analyses were run as separate hand-typed commands, recorded
# in the receipt's Commands block and (for the analyses) in the `invocation:` line each log carries.
# The receipt previously called this "the full driver", which it is not.
# The three suites the delta carried forward are re-run by `rerun-carried-suites.sh` in this
# directory.
set -uo pipefail

# Every step's status is recorded AND accumulated. Without this the script printed LANES_DONE and
# exited 0 even when a restore had failed, and then ran `dotnet test --no-build` against stale or
# absent binaries (2026-07-28 delta code review).
LANE_FAILURES=0
note_exit() { # note_exit <label> <rc>
  printf '%s_EXIT=%d\n' "$1" "$2"
  [ "$2" -ne 0 ] && LANE_FAILURES=$((LANE_FAILURES + 1))
  return 0
}

SRC=/home/administrator/tmp-story-2-12/src-lane-f9e51c6
PKG=/home/administrator/tmp-story-2-12/pkg-lane-f9e51c6
PKGDIR=/home/administrator/tmp-story-2-12/pkg-packages-f9e51c6
OUT=/home/administrator/tmp-story-2-12/logs-f9e51c6
mkdir -p "$OUT"
rm -rf -- "$PKGDIR"

SRCPROPS=(-p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false -p:HexalithCommonsFromSource=false)
PKGPROPS=(-p:UseHexalithProjectReferences=false)

step() { printf '\n>>>>> %s\n' "$1"; }

########## SOURCE LANE ##########
cd "$SRC" || exit 1
step "SRC restore"
dotnet restore Hexalith.Tenants.slnx --force-evaluate -p:Configuration=Debug \
  "${SRCPROPS[@]}" -nodeReuse:false -m:1 >"$OUT/src-restore.log" 2>&1
note_exit SRC_RESTORE $?

step "SRC build"
dotnet build Hexalith.Tenants.slnx --configuration Debug --no-restore --warnaserror \
  "${SRCPROPS[@]}" -nodeReuse:false -m:1 >"$OUT/src-build.log" 2>&1
note_exit SRC_BUILD $?
grep -E "Warning\(s\)|Error\(s\)" "$OUT/src-build.log" | tail -3

step "SRC test Contracts.Tests"
dotnet test tests/Hexalith.Tenants.Contracts.Tests/Hexalith.Tenants.Contracts.Tests.csproj \
  --configuration Debug --no-build --no-restore "${SRCPROPS[@]}" -nodeReuse:false -m:1 \
  >"$OUT/src-test-Contracts.Tests.log" 2>&1
note_exit SRC_TEST $?
grep -E "^\s*(Passed|Failed)!|Test summary|total:|failed:|succeeded:" "$OUT/src-test-Contracts.Tests.log" | tail -6

########## PACKAGE LANE ##########
cd "$PKG" || exit 1
step "PKG restore"
dotnet restore Hexalith.Tenants.slnx --force-evaluate -p:Configuration=Release \
  "${PKGPROPS[@]}" --packages "$PKGDIR" -nodeReuse:false -m:1 >"$OUT/pkg-restore.log" 2>&1
note_exit PKG_RESTORE $?
# A restore that died before emitting any NU#### line also yields 0, so the count is only
# meaningful when the restore succeeded (2026-07-28 delta code review).
if [ "$LANE_FAILURES" -eq 0 ]; then
  grep -cE "^.*(NU[0-9]{4})" "$OUT/pkg-restore.log" | sed 's/^/NU_DIAGNOSTIC_LINES=/'
else
  echo "NU_DIAGNOSTIC_LINES=unknown (a prior step failed; count would be meaningless)"
fi

step "PKG build"
dotnet build Hexalith.Tenants.slnx --configuration Release --no-restore --warnaserror \
  "${PKGPROPS[@]}" -nodeReuse:false -m:1 >"$OUT/pkg-build.log" 2>&1
note_exit PKG_BUILD $?
grep -E "Warning\(s\)|Error\(s\)" "$OUT/pkg-build.log" | tail -3

step "PKG test Contracts.Tests"
dotnet test tests/Hexalith.Tenants.Contracts.Tests/Hexalith.Tenants.Contracts.Tests.csproj \
  --configuration Release --no-build --no-restore "${PKGPROPS[@]}" -nodeReuse:false -m:1 \
  >"$OUT/pkg-test-Contracts.Tests.log" 2>&1
note_exit PKG_TEST $?
grep -E "^\s*(Passed|Failed)!|Test summary|total:|failed:|succeeded:" "$OUT/pkg-test-Contracts.Tests.log" | tail -6

step "downloaded EventStore packages in the isolated packages directory"
ls "$PKGDIR" 2>/dev/null | grep -i "^hexalith.eventstore" || echo "(none)"

printf 'LANE_FAILURES=%d\n' "$LANE_FAILURES"
if [ "$LANE_FAILURES" -eq 0 ]; then echo "LANES_DONE"; else echo "LANES_FAILED"; exit 1; fi
