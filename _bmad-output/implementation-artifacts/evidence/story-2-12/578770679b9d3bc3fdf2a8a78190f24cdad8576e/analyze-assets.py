#!/usr/bin/env python3
"""Story 2.12 — parse every evaluated project.assets.json in a Tenants working copy and
report the resolved Hexalith.EventStore* dependency graph per project.

Usage: analyze-assets.py <tenants-root> <expected-mode: source|package> [expected-version]
Exits non-zero if the resolved graph violates the expected mode.
"""
import json
import os
import sys

root = os.path.realpath(sys.argv[1])
mode = sys.argv[2]
expected_version = sys.argv[3] if len(sys.argv) > 3 else None
es_root = os.path.join(root, "references", "Hexalith.EventStore")

assets = []
for sub in ("src", "tests", "samples"):
    base = os.path.join(root, sub)
    for dirpath, dirnames, filenames in os.walk(base):
        dirnames[:] = [d for d in dirnames if d != "references"]
        if "project.assets.json" in filenames:
            assets.append(os.path.join(dirpath, "project.assets.json"))
assets.sort()

if not assets:
    print("FAIL: no project.assets.json found — the lane's restore did not run", file=sys.stderr)
    sys.exit(2)

total_project = total_package = total_outside = 0
violations = []
rows = []
versions = set()

for path in assets:
    with open(path, encoding="utf-8-sig") as fh:
        data = json.load(fh)
    proj_name = os.path.basename(
        data.get("project", {}).get("restore", {}).get("projectPath", path)
    )
    proj_name = os.path.splitext(proj_name)[0]

    libs = data.get("libraries", {})
    n_proj = n_pkg = n_outside = 0
    ids = []
    for key, lib in libs.items():
        pkg_id = key.split("/", 1)[0]
        if not pkg_id.startswith("Hexalith.EventStore"):
            continue
        version = key.split("/", 1)[1] if "/" in key else ""
        ltype = lib.get("type")
        ids.append((pkg_id, version, ltype))
        if ltype == "project":
            n_proj += 1
            msbuild = lib.get("msbuildProject") or lib.get("path") or ""
            # msbuildProject is relative to the consuming project directory (parent of obj/).
            project_dir = os.path.dirname(os.path.dirname(path))
            resolved = os.path.realpath(os.path.join(project_dir, msbuild))
            if not resolved.startswith(es_root + os.sep):
                n_outside += 1
                violations.append(
                    f"{proj_name}: project edge {pkg_id} resolves outside the validated "
                    f"checkout -> {resolved}"
                )
        elif ltype == "package":
            n_pkg += 1
            versions.add(version)
            if expected_version and version != expected_version:
                violations.append(
                    f"{proj_name}: {pkg_id} resolved package version {version} "
                    f"!= expected {expected_version}"
                )
        else:
            violations.append(f"{proj_name}: {pkg_id} has unexpected library type {ltype!r}")

    if mode == "source" and n_pkg:
        violations.append(f"{proj_name}: source mode resolved {n_pkg} EventStore package(s)")
    if mode == "package" and n_proj:
        violations.append(f"{proj_name}: package mode resolved {n_proj} EventStore project edge(s)")

    total_project += n_proj
    total_package += n_pkg
    total_outside += n_outside
    if ids:
        rows.append((proj_name, len(ids), n_proj, n_pkg, n_outside, sorted(ids)))

print(f"assets files evaluated: {len(assets)}")
print(f"{'consuming project':<42} {'edges':>5} {'project':>7} {'package':>7} {'outside':>7}")
for name, total, np_, npk, nout, _ids in rows:
    print(f"{name:<42} {total:>5} {np_:>7} {npk:>7} {nout:>7}")
print(f"{'TOTAL':<42} {total_project + total_package:>5} {total_project:>7} "
      f"{total_package:>7} {total_outside:>7}")
if versions:
    print(f"resolved package version(s): {sorted(versions)}")

print("\nper-project EventStore ids:")
for name, _t, _np, _npk, _no, ids in rows:
    joined = ", ".join(f"{i}@{v}[{t}]" for i, v, t in ids)
    print(f"  {name}: {joined}")

if violations:
    print("\nVIOLATIONS:", file=sys.stderr)
    for v in violations:
        print(f"  - {v}", file=sys.stderr)
    sys.exit(1)

print(f"\nASSETS_OK mode={mode}")
