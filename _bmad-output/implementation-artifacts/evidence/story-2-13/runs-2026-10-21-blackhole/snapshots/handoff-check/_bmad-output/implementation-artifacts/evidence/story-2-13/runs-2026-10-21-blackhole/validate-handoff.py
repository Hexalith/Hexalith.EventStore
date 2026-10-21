#!/usr/bin/env python3
"""Check this continuation's documentation links and unchanged authorization boundaries."""
from pathlib import Path
import re
import subprocess

import yaml

root = next(path for path in Path(__file__).resolve().parents if (path / "Hexalith.EventStore.slnx").exists())
register = yaml.safe_load((root / "docs/architecture/dapr-infrastructure-exceptions.yaml").read_text())
assert register["accepted_exceptions"] == []
assert register["status"] == "preliminary-unqualified"
row = next(item for item in register["unresolved"] if item["id"] == "signalr-replica-distribution")
assert row["decision"] == "pending-qualification-and-architecture-owner-decision"
for name in ("qualification_packet", "qualification_continuation", "historical_continuation", "network_continuation", "blackhole_continuation"):
    assert (root / row[name]).is_file(), name
spec_path = "_bmad-output/implementation-artifacts/spec-2-13-dapr-notification-distribution-qualification.md"
spec = (root / spec_path).read_bytes()
committed = subprocess.check_output(["git", "show", f"HEAD:{spec_path}"], cwd=root)
pattern = rb"<frozen-after-approval\b.*?</frozen-after-approval>"
assert re.findall(pattern, spec, re.S) == re.findall(pattern, committed, re.S)
spec_header = yaml.safe_load(spec.decode().split("---", 2)[1])
assert spec_header["baseline_commit"] == "f2b480b68a8bd5369b3cdf550340e0fadfb2332b"
assert spec_header["status"] == "in-progress"
story = (root / "_bmad-output/implementation-artifacts/2-13-dapr-notification-distribution-qualification.md").read_text()
assert yaml.safe_load(story.split("---", 2)[1])["status"] == "backlog"
tracker = yaml.safe_load((root / "_bmad-output/implementation-artifacts/sprint-status.yaml").read_text())
assert tracker["development_status"]["2-13-dapr-notification-distribution-qualification"] == "in-progress"
continuation = "qualification-2026-10-21-blackhole.md"
for relative in ("docs/concepts/dapr-infrastructure-boundary.md", spec_path, "_bmad-output/implementation-artifacts/2-13-dapr-notification-distribution-qualification.md"):
    assert continuation in (root / relative).read_text(), relative
print("PASS: YAML, continuation links, frozen intent and original baseline; no accepted exception or completed story")
