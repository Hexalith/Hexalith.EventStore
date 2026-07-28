#!/usr/bin/env python3
"""Story 2.12 — parse every evaluated project.assets.json in a Tenants working copy and
report the resolved Hexalith.EventStore* dependency graph per project.

Usage: analyze-assets.py <tenants-root> <expected-mode: source|package> [expected-version]
Exits non-zero if the resolved graph violates the expected mode.
"""
import json
import os
import sys

if len(sys.argv) < 3:
    sys.exit("USAGE: analyze-assets.py <tenants-root> <source|package> [expected-version]\n"
             "exit 2 = could not evaluate; exit 1 = graph violated")

root = os.path.realpath(sys.argv[1])
mode = sys.argv[2]
expected_version = sys.argv[3] if len(sys.argv) > 3 else None
es_root = os.path.join(root, "references", "Hexalith.EventStore")

# A mis-cased or mistyped mode used to disable BOTH mode assertions and still print ASSETS_OK.
if mode not in ("source", "package"):
    print(f"FAIL: unknown mode {mode!r} — expected 'source' or 'package'", file=sys.stderr)
    sys.exit(2)

# AC3 requires the EXACT catalog version. Without this the version gate was silently optional.
if mode == "package" and not expected_version:
    print("FAIL: package mode requires the expected catalog version as argv[3]", file=sys.stderr)
    sys.exit(2)

# Record the invocation in the output so a retained log proves which gates were armed.
print(f"invocation: root={root} mode={mode} expected_version={expected_version!r}")

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
# `libraries` cannot represent a ReferenceOutputAssembly="false" ProjectReference (e.g. analyzer
# and AppHost host references). project.restore.frameworks[].projectReferences does, so parse it
# too — otherwise "zero project edges including transitive" is blind to that whole class.
raw_project_refs = []

for path in assets:
    with open(path, encoding="utf-8-sig") as fh:
        data = json.load(fh)
    proj_name = os.path.basename(
        data.get("project", {}).get("restore", {}).get("projectPath", path)
    )
    proj_name = os.path.splitext(proj_name)[0]

    # Raw ProjectReference items — includes ReferenceOutputAssembly="false" edges that never
    # appear in `libraries`.
    for fw in data.get("project", {}).get("restore", {}).get("frameworks", {}).values():
        for ref in (fw.get("projectReferences") or {}).values():
            ref_path = ref.get("projectPath", "")
            if "Hexalith.EventStore" not in ref_path:
                continue
            raw_project_refs.append((proj_name, os.path.basename(ref_path)))
            if mode == "package":
                violations.append(
                    f"{proj_name}: package mode declares an EventStore ProjectReference -> "
                    f"{ref_path}"
                )

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

print(f"\nraw EventStore ProjectReference items (incl. ReferenceOutputAssembly=false): "
      f"{len(raw_project_refs)}")
for consumer, ref in sorted(raw_project_refs):
    print(f"  {consumer} -> {ref}")

# A lane that resolved nothing used to print ASSETS_OK. An empty graph is a failed evaluation,
# not a clean one.
if total_project + total_package == 0:
    violations.append(
        "no Hexalith.EventStore* edges resolved in any evaluated assets file — the lane's restore "
        "did not produce a graph to check (vacuous pass guard)"
    )
if mode == "source" and total_project == 0:
    violations.append("source mode resolved zero EventStore project edges")
if mode == "package" and total_package == 0:
    violations.append("package mode resolved zero EventStore package edges")

if violations:
    print("\nVIOLATIONS:", file=sys.stderr)
    for v in violations:
        print(f"  - {v}", file=sys.stderr)
    sys.exit(1)

print(f"\nASSETS_OK mode={mode}")
