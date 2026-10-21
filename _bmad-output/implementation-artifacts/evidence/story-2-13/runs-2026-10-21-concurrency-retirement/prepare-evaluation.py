#!/usr/bin/env python3
"""Copy the retained runtime source without mutating its original subject."""
from pathlib import Path
import datetime
import hashlib
import json
import shutil
import stat
import tempfile

packet = Path(__file__).resolve().parent
prior = packet.parent / "runs-2026-10-21-runtime-recovery"
original = json.loads((prior / "runtime-acquisition.json").read_text())
retained = Path(original["workDirectory"])
old_source = retained / "dapr-b32a815fa2260983e181b95272ea555fe17d3895"

def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

def inventory(root):
    result = {}
    for path in sorted(root.rglob("*")):
        relative = str(path.relative_to(root))
        mode = path.lstat().st_mode
        entry = {"mode": stat.S_IMODE(mode)}
        if path.is_symlink():
            entry.update(type="symlink", target=str(path.readlink()))
        elif path.is_dir():
            entry.update(type="directory")
        elif path.is_file():
            entry.update(type="file", bytes=path.stat().st_size, sha256=sha(path))
        else:
            raise RuntimeError(f"Unexpected source path: {path}")
        result[relative] = entry
    return result

if (packet / "runtime-acquisition.json").exists():
    raise RuntimeError("Preparation receipt already exists")
before = inventory(old_source)
work = Path(tempfile.mkdtemp(prefix="eventstore-2-13-retirement-"))
source = work / old_source.name
shutil.copytree(old_source, source, symlinks=True)
copied = inventory(source)
if before != copied or inventory(old_source) != before:
    raise RuntimeError("Source copy differs or retained source changed")
(packet / "retained-source-inventory.json").write_text(json.dumps(before, indent=2) + "\n")
receipt = {
    "observedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "workDirectory": str(work), "sourceDirectory": str(source),
    "retainedSourceDirectory": str(old_source),
    "retainedSourceInventorySha256": sha(packet / "retained-source-inventory.json"),
    "retainedAcquisitionSha256": sha(prior / "runtime-acquisition.json"),
    "sourceRevision": "b32a815fa2260983e181b95272ea555fe17d3895",
    "sourceArchive": {"path": str(retained / "dapr-source.tar.gz"), "sha256": sha(retained / "dapr-source.tar.gz")},
    "toolchain": {"path": str(retained / "go/bin/go"), "sha256": sha(retained / "go/bin/go"), "archiveSha256": sha(retained / "toolchain.tar.gz"), "version": "go1.26.9"},
    "gopath": str(retained / "gopath"), "gocache": str(retained / "gocache"),
    "retainedRuntime": {"path": str(retained / "daprd-recovery-observed"), "sha256": sha(retained / "daprd-recovery-observed")},
    "installedRuntime": {"path": str(Path.home() / ".dapr/bin/daprd"), "sha256": sha(Path.home() / ".dapr/bin/daprd")},
    "dependencies": {name: sha(source / name) for name in ("go.mod", "go.sum")},
    "authority": "disposable local concurrency qualification only"
}
(packet / "runtime-acquisition.json").write_text(json.dumps(receipt, indent=2) + "\n")
print(json.dumps({"source": str(source), "inventoryPaths": len(before)}))
