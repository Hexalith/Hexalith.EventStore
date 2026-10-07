"""Read-only hash/authority audit of the final source-derived candidate package."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[5]
DIRECTORY = ROOT / "_bmad-output/implementation-artifacts/evidence/story-6-6/source-candidates-2026-10-07"
receipt = json.loads((DIRECTORY / "candidate-generation.json").read_text())
checked = []

def visit(value):
    if isinstance(value, dict):
        if {"path", "sha256", "bytes"} <= value.keys():
            path = ROOT / value["path"]
            raw = path.read_bytes()
            assert len(raw) == value["bytes"] and hashlib.sha256(raw).hexdigest() == value["sha256"], str(path)
            checked.append(value["path"])
        for item in value.values():
            visit(item)
    elif isinstance(value, list):
        for item in value:
            visit(item)

for entry in receipt["candidateFiles"]:
    raw = (DIRECTORY / entry["path"]).read_bytes()
    assert len(raw) == entry["bytes"] and hashlib.sha256(raw).hexdigest() == entry["candidateContentSha256"]
    document = json.loads(raw)
    for flag in ("activationAuthority", "approvalAuthority", "productionAuthority", "readinessAuthority"):
        assert document[flag] is False
    visit(document)
visit(receipt["generator"])
print(json.dumps({"result": "passed", "candidateContentHashes": len(receipt["candidateFiles"]),
                  "nestedSourceArtifactFacts": len(checked) - 1, "generatorHash": "passed", "authority": False,
                  "canonicalProfileExists": (ROOT / "deploy/dapr/production-profile.yaml").exists()}, sort_keys=True))
