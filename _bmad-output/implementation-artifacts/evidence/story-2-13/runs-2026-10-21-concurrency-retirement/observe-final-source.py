#!/usr/bin/env python3
"""Bind final observations to all three preserved runtime-source subjects."""
from pathlib import Path
import datetime
import difflib
import hashlib
import json
import tarfile

packet = Path(__file__).resolve().parent
a = json.loads((packet / "runtime-acquisition.json").read_text())
ns = {}
helper = (packet / "prepare-evaluation.py").read_text().split('if (packet / "runtime-acquisition.json").exists():')[0]
exec(helper.replace('packet = Path(__file__).resolve().parent', 'packet = Path(' + repr(str(packet)) + ')'), ns)
original = ns["inventory"](Path(a["retainedSourceDirectory"]))
candidate = ns["inventory"](Path(a["sourceDirectory"]))
if original != json.loads((packet / "retained-source-inventory.json").read_text()):
    raise RuntimeError("Original retained candidate source changed")
if candidate != json.loads((packet / "before-candidate-build-source-inventory.json").read_text()):
    raise RuntimeError("Retirement source changed after build")
rejected_packet = packet.parent / "runs-2026-10-21-concurrency-safety"
rejected_a = json.loads((rejected_packet / "runtime-acquisition.json").read_text())
if ns["inventory"](Path(rejected_a["sourceDirectory"])) != json.loads((rejected_packet / "before-candidate-build-source-inventory.json").read_text()):
    raise RuntimeError("Rejected reset source changed")
def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()
prior = json.loads((packet / "reset-evaluation-before-retirement.json").read_text())
for name, entry in prior["files"].items():
    path = rejected_packet / name
    if path.stat().st_size != entry["bytes"] or sha(path) != entry["sha256"]:
        raise RuntimeError("Rejected reset evidence changed: " + name)
names = ["pkg/actors/router/router.go", "pkg/api/grpc/manager/manager.go", "pkg/api/grpc/manager/pool.go"]
with tarfile.open(a["sourceArchive"]["path"]) as tf:
    upstream = {name: tf.extractfile(next(value for value in tf.getnames() if value.endswith('/' + name))).read().decode() for name in names}
patch = ''.join(''.join(difflib.unified_diff(upstream[name].splitlines(True), (Path(a["sourceDirectory"]) / name).read_text().splitlines(True), fromfile='a/' + name, tofile='b/' + name)) for name in names)
if patch != (packet / "candidate-from-upstream.patch").read_text():
    raise RuntimeError("Candidate patch does not regenerate exactly")
if any("ResetConnectBackoff" in (Path(a["sourceDirectory"]) / name).read_text() for name in names):
    raise RuntimeError("Retirement candidate uses the rejected reset API")
identities = {}
for path, expected in {
    a["retainedRuntime"]["path"]: a["retainedRuntime"]["sha256"],
    a["installedRuntime"]["path"]: a["installedRuntime"]["sha256"],
    a["toolchain"]["path"]: a["toolchain"]["sha256"],
    a["sourceArchive"]["path"]: a["sourceArchive"]["sha256"],
    **{json.loads((rejected_packet / "candidate-runtime-identity.json").read_text())["candidatePath"]: json.loads((rejected_packet / "candidate-runtime-identity.json").read_text())["candidateSha256"]},
}.items():
    identities[path] = sha(Path(path))
    if identities[path] != expected:
        raise RuntimeError("Preserved runtime/toolchain/archive changed: " + path)
runtime = json.loads((packet / "candidate-runtime-identity.json").read_text())
if sha(Path(runtime["candidatePath"])) != runtime["candidateSha256"]:
    raise RuntimeError("Qualified retirement binary changed")
result = {
    "observedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "retainedSourcePaths": len(original), "retainedSourceUnchanged": True,
    "candidateSourcePaths": len(candidate), "candidateSourceUnchangedAfterBuild": True,
    "rejectedResetSourceAndEvidenceUnchanged": True, "rejectedResetPreservedFiles": len(prior["files"]),
    "candidatePatchRegeneratesExactly": True, "candidateUsesResetConnectBackoff": False,
    "preservedIdentities": identities, "candidateRuntimeSha256": runtime["candidateSha256"],
    "changedCandidateSourcePaths": [name for name in sorted(set(original) | set(candidate)) if original.get(name) != candidate.get(name)],
}
(packet / "final-source-preservation.json").write_text(json.dumps(result, indent=2) + "\n")
print(json.dumps(result, indent=2))
