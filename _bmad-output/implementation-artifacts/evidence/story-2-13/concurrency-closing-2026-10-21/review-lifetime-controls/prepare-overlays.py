#!/usr/bin/env python3
"""Preserve the temporary review evidence and select only closing-review overlays."""
from pathlib import Path
import datetime
import difflib
import hashlib
import json
import shutil
import subprocess

packet = Path(__file__).resolve().parent
base = packet.parent.parent
acquisition = json.loads((base / "runs-2026-10-21-concurrency-retirement/runtime-acquisition.json").read_text())
source = Path(acquisition["sourceDirectory"])
temporary = Path("/tmp/story-2-13-review-double-release-hyybumzj")
retained = packet / "retained-double-release"

def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

def files(root):
    return {str(path.relative_to(root)): {"bytes": path.stat().st_size, "sha256": sha(path)} for path in sorted(root.rglob("*")) if path.is_file()}

if retained.exists():
    raise RuntimeError("Temporary review evidence was already retained")
before = files(temporary)
shutil.copytree(temporary, retained, copy_function=shutil.copy2)
if files(retained) != before or files(temporary) != before:
    raise RuntimeError("Temporary review evidence changed during preservation")
(packet / "retained-temporary-evidence.json").write_text(json.dumps({
    "observedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "originalDirectory": str(temporary), "retainedDirectory": str(retained),
    "files": before, "copyVerifiedByteForByte": True,
    "historicalReceiptLimitation": "Original temporary logs/receipt retain their original information; complete UTC/input observations are supplied only by the new reruns."
}, indent=2) + "\n")
gofmt = str(Path(acquisition["toolchain"]["path"]).with_name("gofmt"))
subprocess.run([gofmt, "-w", str(packet / "review_lifetime_pool_test.go.source"), str(packet / "review_retirement_context_test.go.source")], check=True)
overlays = {
    "lifetime-controls.overlay.json": {
        str(source / "pkg/api/grpc/manager/review_lifetime_pool_test.go"): str(packet / "review_lifetime_pool_test.go.source"),
        str(source / "pkg/actors/router/review_retirement_context_test.go"): str(packet / "review_retirement_context_test.go.source"),
    },
    "caller-lease-candidate.overlay.json": {
        str(source / "pkg/actors/router/review_caller_lease_test.go"): str(retained / "caller_lease_test.go"),
    },
    "caller-lease-mutant.overlay.json": {
        str(source / "pkg/actors/router/review_caller_lease_test.go"): str(retained / "caller_lease_test.go"),
        str(source / "pkg/actors/router/router.go"): str(retained / "router.go"),
    },
}
for name, mapping in overlays.items():
    with (packet / name).open("x") as stream:
        json.dump({"Replace": mapping}, stream, indent=2)
        stream.write("\n")
original = (source / "pkg/actors/router/router.go").read_text()
mutant = (retained / "router.go").read_text()
patch = ''.join(difflib.unified_diff(original.splitlines(True), mutant.splitlines(True), fromfile="a/pkg/actors/router/router.go", tofile="b/pkg/actors/router/router.go"))
(packet / "extra-release-mutant.patch").write_text(patch)
print(json.dumps({"retainedTemporaryFiles": len(before), "overlays": list(overlays)}))
