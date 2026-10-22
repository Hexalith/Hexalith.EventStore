#!/usr/bin/env python3
"""Read-only verification of current source, frozen images and archived PDB-bound source."""
import hashlib
import json
from pathlib import Path

packet = Path(__file__).resolve().parent
errors = []


def verify(path, expected, kind):
    if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest().lower() != expected.lower():
        errors.append({"kind": kind, "path": str(path)})


sources = json.loads((packet / "source-inputs.json").read_text())
for name, digest in sources.items():
    verify(Path(name), digest, "current-source")
images = json.loads((packet / "handoff-inventory.json").read_text())["files"]
for row in images:
    verify(packet / row["path"], row["sha256"], "frozen-image")
documents = json.loads((packet / "bound-source-index.json").read_text())
for row in documents:
    verify(packet / row["archived_path"], row["sha256"], "pdb-bound-source")
print(json.dumps({"passed": not errors, "source_inputs": len(sources), "frozen_files": len(images),
                  "pdb_document_copies": len(documents), "errors": errors}, indent=2))
raise SystemExit(1 if errors else 0)
