#!/usr/bin/env python3
"""Retain local commands and exact qualification inputs, without asserting owner authority."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

packet = Path(__file__).resolve().parent
repository = next(path for path in packet.parents if (path / "Hexalith.EventStore.slnx").exists())
label, *command = sys.argv[1:]
if command and command[0] == "--":
    command.pop(0)
assert re.fullmatch(r"[a-z0-9-]+", label) and command
receipt_path = packet / f"{label}.json"
assert not receipt_path.exists(), "Receipts are immutable; use a distinct label for a retry."
fixture = repository / "tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRDaprDistributionFixture.cs"
sources = sorted(set(re.findall(r'"((?:src|tests|deploy|_bmad-output|references)/[^"\n]+)"', fixture.read_text())))
sources += ["global.json", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"]
snapshot = packet / "snapshots" / label
for relative in sources:
    target = snapshot / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(repository / relative, target)

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def identities():
    return {relative: digest(repository / relative) for relative in sources}

binaries = [
    "src/Hexalith.EventStore/bin/Debug/net10.0/Hexalith.EventStore.dll",
    "src/Hexalith.EventStore/bin/Debug/net10.0/Hexalith.EventStore.deps.json",
    "src/Hexalith.EventStore/bin/Debug/net10.0/Hexalith.EventStore.Gateway.dll",
    "tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests.dll",
]
def binary_identities():
    return {relative: digest(repository / relative) for relative in binaries if (repository / relative).exists()}

before = identities()
binary_before = binary_identities()
started = datetime.datetime.now(datetime.timezone.utc).isoformat()
environment = os.environ.copy()
environment["HEXALITH_STORY_2_13_EVIDENCE_DIRECTORY"] = str(packet / "scenarios")
result = subprocess.run(command, cwd=repository, env=environment, capture_output=True, text=True)
ended = datetime.datetime.now(datetime.timezone.utc).isoformat()
output = result.stdout + result.stderr
output = re.sub(r"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\b", "[redacted-jwt]", output)
output = re.sub(r"(?i)([?&]t=|x-otlp-api-key=)[^\s&\"']+", r"\1[redacted]", output)
log = packet / f"{label}.log"
log.write_text(output)
receipt = {
    "schemaVersion": 1, "label": label, "command": command,
    "startedUtc": started, "completedUtc": ended, "exitCode": result.returncode,
    "runStartRevision": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repository, text=True).strip(),
    "sourcesBefore": before, "sourcesAfter": identities(),
    "binariesBefore": binary_before, "binariesAfter": binary_identities(),
    "outputSha256": digest(log), "snapshot": str(snapshot.relative_to(packet)),
    "authority": "local qualification only; no owner approval, CI seal or production authority",
}
receipt_path.write_text(json.dumps(receipt, indent=2) + "\n")
print(json.dumps({"label": label, "exitCode": result.returncode, "startedUtc": started, "completedUtc": ended}))
print(output[-3500:])
sys.exit(result.returncode)
