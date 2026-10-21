#!/usr/bin/env python3
"""Bind the isolated executable to complete source and dependency observations."""
from pathlib import Path
import datetime
import hashlib
import json
import subprocess

packet = Path(__file__).resolve().parent
acquisition = json.loads((packet / "runtime-acquisition.json").read_text())
source = Path(acquisition["sourceDirectory"])
runtime = Path(acquisition["workDirectory"]) / "daprd-concurrency"
go = acquisition["toolchain"]["path"]

def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

ns = {}
helper = (packet / "prepare-evaluation.py").read_text().split('if (packet / "runtime-acquisition.json").exists():')[0]
exec(helper.replace('packet = Path(__file__).resolve().parent', 'packet = Path(' + repr(str(packet)) + ')'), ns)
current = ns["inventory"](source)
before = json.loads((packet / "before-candidate-build-source-inventory.json").read_text())
if current != before:
    raise RuntimeError("Candidate source changed across the build")
(packet / "after-candidate-build-source-inventory.json").write_text(json.dumps(current, indent=2) + "\n")
commands = {}
for label, command in {
    "version": [str(runtime), "--version"],
    "buildinfo": [go, "version", "-m", str(runtime)],
    "toolchain": [go, "version"],
}.items():
    result = subprocess.run(command, capture_output=True, text=True, check=True)
    commands[label] = {"command": command, "exitCode": result.returncode, "output": result.stdout + result.stderr}
old = json.loads((packet.parent / "runs-2026-10-21-runtime-recovery/final-runtime-identity.json").read_text())
def dependencies(output):
    return [line for line in output.splitlines() if line.startswith(("\tdep\t", "\t=>\t"))]
if dependencies(old["commands"]["buildinfo"]["output"]) != dependencies(commands["buildinfo"]["output"]):
    raise RuntimeError("Compiled dependencies changed")
if {name: sha(source / name) for name in ("go.mod", "go.sum")} != acquisition["dependencies"]:
    raise RuntimeError("Dependency manifests changed")
record = {
    "observedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "sourceRevision": acquisition["sourceRevision"],
    "candidatePath": str(runtime), "candidateSha256": sha(runtime), "candidateBytes": runtime.stat().st_size,
    "toolchainSha256": sha(Path(go)), "sourceArchiveSha256": acquisition["sourceArchive"]["sha256"],
    "dependencyManifests": acquisition["dependencies"], "compiledDependenciesEqualRetainedCandidate": True,
    "patchSha256": sha(packet / "candidate-from-upstream.patch"),
    "correctionFromRetainedSha256": sha(packet / "correction-from-retained.patch"),
    "beforeBuildSourceInventorySha256": sha(packet / "before-candidate-build-source-inventory.json"),
    "afterBuildSourceInventorySha256": sha(packet / "after-candidate-build-source-inventory.json"),
    "sourceInventoriesEqual": True, "commands": commands,
    "installMutation": False, "authority": "local concurrency evaluation only"
}
(packet / "candidate-runtime-identity.json").write_text(json.dumps(record, indent=2) + "\n")
print(json.dumps({key: record[key] for key in ("candidatePath", "candidateSha256", "compiledDependenciesEqualRetainedCandidate", "sourceInventoriesEqual")}))
