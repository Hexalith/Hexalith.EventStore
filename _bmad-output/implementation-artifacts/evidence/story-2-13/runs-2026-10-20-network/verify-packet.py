#!/usr/bin/env python3
"""Verify retained local packet bytes. This grants no owner or CI authority."""
import hashlib
import json
from pathlib import Path

packet = Path(__file__).resolve().parent

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def retained(relative):
    path = (packet / relative).resolve()
    assert path.is_relative_to(packet), relative
    assert path.is_file(), relative
    return path

index = json.loads((packet / "index.json").read_text())
assert digest(packet.parent / "qualification-2026-10-20-network.md") == index["narrativeSha256"]
for relative, expected in index["artifacts"].items():
    assert digest(retained(relative)) == expected, relative
for relative in index["commandReceipts"]:
    receipt = json.loads(retained(relative).read_text())
    assert receipt["exitCode"] == 0, relative
    assert receipt["startedUtc"] < receipt["completedUtc"], relative
    log = relative.removesuffix(".json") + ".log"
    assert digest(retained(log)) == receipt["outputSha256"], log
    for source, expected in receipt["sourcesBefore"].items():
        if source in index["buildOutputPaths"]:
            assert receipt["binariesBefore"][source] == expected, source
        else:
            assert digest(retained(receipt["snapshot"] + "/" + source)) == expected, source
    if relative in index["stableTestReceipts"]:
        assert receipt["sourcesBefore"] == receipt["sourcesAfter"], relative
        assert receipt["binariesBefore"] == receipt["binariesAfter"], relative
    if relative in index["stableBuildReceipts"]:
        for source, expected in receipt["sourcesBefore"].items():
            if source not in index["buildOutputPaths"]:
                assert receipt["sourcesAfter"][source] == expected, source
for relative in index["scenarioManifests"]:
    path = retained(relative)
    manifest = json.loads(path.read_text())
    assert manifest["Result"] == "passed-scenario-assertions-only", relative
    assert manifest["FirstHostProcessId"] != manifest["SecondHostProcessId"], relative
    assert manifest["FirstSidecarProcessId"] != manifest["SecondSidecarProcessId"], relative
    for artifact, expected in manifest["Artifacts"].items():
        assert digest(path.parent / artifact) == expected, artifact
    for source, expected in manifest["Sources"].items():
        assert digest(path.parent / "sources" / source) == expected, source
closing = json.loads(retained("closing-observations.json").read_text())
assert not any(value["existsAfterDisposal"] for value in closing["processes"].values())
assert not any(value["acceptingAfterDisposal"] for value in closing["proxyPorts"].values())
assert not closing["remainingFixtureDirectories"]
print("PASS: retained bytes, commands, stable-input test bindings and independent replica identities; no owner/CI authority")
