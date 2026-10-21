#!/usr/bin/env python3
"""Close the new packet only after parent lifecycle and preservation observations."""
from pathlib import Path
import datetime
import hashlib
import json

packet = Path(__file__).resolve().parent
repository = next(path for path in packet.parents if (path / "Hexalith.EventStore.slnx").exists())

def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

if any((packet / name).exists() for name in ("index.json", "handoff-identities.json", "verification.json")):
    raise RuntimeError("Closing artifacts already exist; no closed packet is rewritten")
paths = [
    "_bmad-output/implementation-artifacts/evidence/story-2-13/qualification-2026-10-21-concurrency-safety.md",
    "_bmad-output/implementation-artifacts/spec-2-13-dapr-notification-distribution-qualification.md",
    "_bmad-output/implementation-artifacts/2-13-dapr-notification-distribution-qualification.md",
    "_bmad-output/implementation-artifacts/sprint-status.yaml",
    "docs/architecture/dapr-infrastructure-exceptions.yaml",
]
handoff = {"observedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "files": {name: sha(repository / name) for name in paths}, "authority": "local evidence only"}
(packet / "handoff-identities.json").write_text(json.dumps(handoff, indent=2) + "\n")
files = {str(path.relative_to(packet)): {"bytes": path.stat().st_size, "sha256": sha(path)} for path in sorted(packet.rglob("*")) if path.is_file() and path.name not in ("index.json", "verification.json")}
index = {"schemaVersion": 1, "closedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "files": files, "authority": "local qualification only; no completion or production authority"}
(packet / "index.json").write_text(json.dumps(index, indent=2) + "\n")
print(json.dumps({"closedFiles": len(files), "indexSha256": sha(packet / "index.json")}))
