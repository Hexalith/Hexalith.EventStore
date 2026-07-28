#!/usr/bin/env bash
# Story 2.12 focused delta at Tenants f9e51c66745557da4f267ab40f32294f2f27fae7.
# Two pre-existing isolated lanes; each restores itself with --force-evaluate.
set -uo pipefail

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
echo "SRC_RESTORE_EXIT=$?"

step "SRC build"
dotnet build Hexalith.Tenants.slnx --configuration Debug --no-restore --warnaserror \
  "${SRCPROPS[@]}" -nodeReuse:false -m:1 >"$OUT/src-build.log" 2>&1
echo "SRC_BUILD_EXIT=$?"
grep -E "Warning\(s\)|Error\(s\)" "$OUT/src-build.log" | tail -3

step "SRC test Contracts.Tests"
dotnet test tests/Hexalith.Tenants.Contracts.Tests/Hexalith.Tenants.Contracts.Tests.csproj \
  --configuration Debug --no-build --no-restore "${SRCPROPS[@]}" -nodeReuse:false -m:1 \
  >"$OUT/src-test-Contracts.Tests.log" 2>&1
echo "SRC_TEST_EXIT=$?"
grep -E "^\s*(Passed|Failed)!|Test summary|total:|failed:|succeeded:" "$OUT/src-test-Contracts.Tests.log" | tail -6

########## PACKAGE LANE ##########
cd "$PKG" || exit 1
step "PKG restore"
dotnet restore Hexalith.Tenants.slnx --force-evaluate -p:Configuration=Release \
  "${PKGPROPS[@]}" --packages "$PKGDIR" -nodeReuse:false -m:1 >"$OUT/pkg-restore.log" 2>&1
echo "PKG_RESTORE_EXIT=$?"
grep -cE "^.*(NU[0-9]{4})" "$OUT/pkg-restore.log" | sed 's/^/NU_DIAGNOSTIC_LINES=/'

step "PKG build"
dotnet build Hexalith.Tenants.slnx --configuration Release --no-restore --warnaserror \
  "${PKGPROPS[@]}" -nodeReuse:false -m:1 >"$OUT/pkg-build.log" 2>&1
echo "PKG_BUILD_EXIT=$?"
grep -E "Warning\(s\)|Error\(s\)" "$OUT/pkg-build.log" | tail -3

step "PKG test Contracts.Tests"
dotnet test tests/Hexalith.Tenants.Contracts.Tests/Hexalith.Tenants.Contracts.Tests.csproj \
  --configuration Release --no-build --no-restore "${PKGPROPS[@]}" -nodeReuse:false -m:1 \
  >"$OUT/pkg-test-Contracts.Tests.log" 2>&1
echo "PKG_TEST_EXIT=$?"
grep -E "^\s*(Passed|Failed)!|Test summary|total:|failed:|succeeded:" "$OUT/pkg-test-Contracts.Tests.log" | tail -6

step "downloaded EventStore packages in the isolated packages directory"
ls "$PKGDIR" 2>/dev/null | grep -i "^hexalith.eventstore" || echo "(none)"

echo "LANES_DONE"
