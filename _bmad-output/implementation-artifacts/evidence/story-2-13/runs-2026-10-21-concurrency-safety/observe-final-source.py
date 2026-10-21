#!/usr/bin/env python3
"""Re-observe every source path after qualification without changing any runtime."""
from pathlib import Path
import datetime
import difflib
import hashlib
import json

packet = Path(__file__).resolve().parent
acquisition = json.loads((packet / "runtime-acquisition.json").read_text())
ns = {}
helper = (packet / "prepare-evaluation.py").read_text().split('if (packet / "runtime-acquisition.json").exists():')[0]
exec(helper.replace('packet = Path(__file__).resolve().parent', 'packet = Path(' + repr(str(packet)) + ')'), ns)
original = ns["inventory"](Path(acquisition["retainedSourceDirectory"]))
candidate = ns["inventory"](Path(acquisition["sourceDirectory"]))
if original != json.loads((packet / "retained-source-inventory.json").read_text()):
    raise RuntimeError("Original retained candidate source changed")
if candidate != json.loads((packet / "before-candidate-build-source-inventory.json").read_text()):
    raise RuntimeError("Built candidate source changed after qualification")
expected = ''.join(difflib.unified_diff((packet / "upstream-router.go.source").read_text().splitlines(True), (packet / "candidate-router.go.source").read_text().splitlines(True), fromfile="a/pkg/actors/router/router.go", tofile="b/pkg/actors/router/router.go"))
if expected != (packet / "candidate-from-upstream.patch").read_text():
    raise RuntimeError("Exact candidate patch does not regenerate")
def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()
identities = {}
for key, expected in {
    acquisition["retainedRuntime"]["path"]: acquisition["retainedRuntime"]["sha256"],
    acquisition["installedRuntime"]["path"]: acquisition["installedRuntime"]["sha256"],
    acquisition["toolchain"]["path"]: acquisition["toolchain"]["sha256"],
    acquisition["sourceArchive"]["path"]: acquisition["sourceArchive"]["sha256"],
}.items():
    identities[key] = sha(Path(key))
    if identities[key] != expected:
        raise RuntimeError("Preserved executable/archive changed: " + key)
runtime = json.loads((packet / "candidate-runtime-identity.json").read_text())
if sha(Path(runtime["candidatePath"])) != runtime["candidateSha256"]:
    raise RuntimeError("Evaluated candidate executable changed")
output = {
    "observedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "retainedSourcePaths": len(original), "retainedSourceUnchanged": True,
    "candidateSourcePaths": len(candidate), "candidateSourceUnchangedAfterBuild": True,
    "candidatePatchRegeneratesExactly": True, "preservedIdentities": identities,
    "candidateRuntimeSha256": runtime["candidateSha256"],
    "changedCandidateSourcePaths": [name for name in sorted(set(original) | set(candidate)) if original.get(name) != candidate.get(name)],
}
(packet / "final-source-preservation.json").write_text(json.dumps(output, indent=2) + "\n")
print(json.dumps(output, indent=2))
