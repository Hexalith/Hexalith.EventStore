#!/usr/bin/env python3
"""Run one focused control with immutable, complete before/after overlay identities."""
from pathlib import Path
import datetime
import hashlib
import json
import os
import stat
import subprocess
import sys

packet = Path(__file__).resolve().parent
base = packet.parent.parent
repository = next(path for path in packet.parents if (path / "Hexalith.EventStore.slnx").exists())
a = json.loads((base / "runs-2026-10-21-concurrency-retirement/runtime-acquisition.json").read_text())
source = Path(a["sourceDirectory"])
label, overlay_name, pattern, *packages = sys.argv[1:]
if not packages or (packet / (label + ".json")).exists():
    raise RuntimeError("A new label and explicit package selection are required")
overlay = packet / overlay_name

def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

def inventory(root):
    entries = {}
    for path in sorted(root.rglob("*")):
        mode = path.lstat().st_mode
        entry = {"mode": stat.S_IMODE(mode)}
        if path.is_symlink():
            entry.update(type="symlink", target=str(path.readlink()))
        elif path.is_dir():
            entry.update(type="directory")
        elif path.is_file():
            entry.update(type="file", bytes=path.stat().st_size, sha256=sha(path))
        else:
            raise RuntimeError("Unexpected retained path: " + str(path))
        entries[str(path.relative_to(root))] = entry
    return entries

def overlay_identity():
    mapping = json.loads(overlay.read_text())["Replace"]
    return {
        "path": str(overlay), "sha256": sha(overlay), "bytes": overlay.stat().st_size,
        "replace": [{
            "originalPath": original, "originalExists": Path(original).exists(),
            "originalSha256": sha(Path(original)) if Path(original).is_file() else None,
            "replacementPath": replacement, "replacementSha256": sha(Path(replacement)),
            "replacementBytes": Path(replacement).stat().st_size,
        } for original, replacement in sorted(mapping.items())],
    }

roots = {
    "originalRetainedSource": Path(a["retainedSourceDirectory"]),
    "rejectedResetSource": Path(json.loads((base / "runs-2026-10-21-concurrency-safety/runtime-acquisition.json").read_text())["sourceDirectory"]),
    "retirementSource": source,
    "retainedResetPacket": base / "runs-2026-10-21-concurrency-safety",
    "retainedRetirementPacket": base / "runs-2026-10-21-concurrency-retirement",
    "temporaryDoubleReleaseEvidence": Path("/tmp/story-2-13-review-double-release-hyybumzj"),
    "retainedDoubleReleaseEvidence": packet / "retained-double-release",
}
executables = [Path(a["toolchain"]["path"]), Path(a["installedRuntime"]["path"]), Path(a["retainedRuntime"]["path"])]
for name in ("runs-2026-10-21-concurrency-safety", "runs-2026-10-21-concurrency-retirement"):
    executables.append(Path(json.loads((base / name / "candidate-runtime-identity.json").read_text())["candidatePath"]))

def inputs(moment):
    full = {name: {"root": str(root), "entries": inventory(root)} for name, root in roots.items()}
    inventory_path = packet / (label + "-" + moment + "-inventories.json")
    with inventory_path.open("x") as stream:
        json.dump(full, stream, indent=2)
        stream.write("\n")
    return {
        "overlay": overlay_identity(),
        "sourceRevision": a["sourceRevision"],
        "toolchainExecutable": {"path": a["toolchain"]["path"], "sha256": sha(Path(a["toolchain"]["path"]))},
        "dependencyManifests": {name: sha(source / name) for name in ("go.mod", "go.sum")},
        "sourcePackages": {str(path): sha(path) for directory in (source / "pkg/actors/router", source / "pkg/api/grpc/manager") for path in sorted(directory.glob("*.go"))},
        "executables": {str(path): sha(path) for path in executables},
        "inventoryFile": inventory_path.name, "inventorySha256": sha(inventory_path),
        "inventoryDigests": {name: hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":")).encode()).hexdigest() for name, value in full.items()},
    }

command = ["env", "GOTOOLCHAIN=local", "GOPATH=" + a["gopath"], "GOCACHE=" + a["gocache"], "CGO_ENABLED=1", a["toolchain"]["path"], "-C", str(source), "test", "-race", "-mod=readonly", "-p=4", "-overlay=" + str(overlay), *packages, "-run", pattern, "-count=1", "-timeout=30s", "-v"]
before = inputs("before")
started = datetime.datetime.now(datetime.timezone.utc).isoformat()
result = subprocess.run(command, cwd=repository, capture_output=True, text=True)
completed = datetime.datetime.now(datetime.timezone.utc).isoformat()
after = inputs("after")
log = packet / (label + ".log")
with log.open("x") as stream:
    stream.write(result.stdout + result.stderr)
unchanged = all(before[key] == after[key] for key in ("overlay", "toolchainExecutable", "dependencyManifests", "sourcePackages", "executables", "inventoryDigests"))
record = {"schemaVersion": 1, "label": label, "command": command,
    "startedUtc": started, "completedUtc": completed, "exitCode": result.returncode,
    "inputsBefore": before, "inputsAfter": after, "preservedInputsUnchanged": unchanged,
    "outputFile": log.name, "outputSha256": sha(log),
    "recorderSha256": sha(Path(__file__)),
    "authority": "focused review controls only; no runtime mutation, completion or production authority"}
with (packet / (label + ".json")).open("x") as stream:
    json.dump(record, stream, indent=2)
    stream.write("\n")
print(json.dumps({"label": label, "exitCode": result.returncode, "preservedInputsUnchanged": unchanged, "startedUtc": started, "completedUtc": completed}))
print((result.stdout + result.stderr)[-7000:])
if not unchanged:
    raise RuntimeError("A retained input changed during this command; see immutable before/after inventories")
sys.exit(result.returncode)
