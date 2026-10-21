#!/usr/bin/env python3
"""Check this local qualification packet; no owner, CI or production authority is conferred."""
import datetime
import hashlib
import json
from pathlib import Path
import sys

packet = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path(__file__).resolve().parent


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def retained(relative):
    relative = index.get("retentionAliases", {}).get(relative, relative)
    path = (packet / relative).resolve()
    assert path.is_relative_to(packet) and path.is_file(), relative
    return path


index = json.loads((packet / "index.json").read_text())
assert index["authority"] == "local qualification only; no owner approval, CI seal or production authority"
actual = {str(path.relative_to(packet)) for path in packet.rglob("*") if path.is_file()}
assert actual == set(index["artifacts"]) | {"index.json"}, "unlisted or missing packet files"
for relative, expected in index["artifacts"].items():
    assert digest(retained(relative)) == expected, relative
transformation = json.loads(retained("retention-transform.json").read_text())
assert index["retentionAliases"] == {
    item["originalRetainedPath"]: item["retainedPath"] for item in transformation["renames"]
}, "retention aliases differ from the byte-preserving rename ledger"
for item in transformation["renames"]:
    renamed = retained(item["originalRetainedPath"])
    assert digest(renamed) == item["sha256"] and renamed.stat().st_size == item["bytes"], item
    assert not (packet / item["originalRetainedPath"]).exists(), item
for relative, expected in transformation["unchangedJsonSha256"].items():
    assert digest(retained(relative)) == expected, relative
assert set(index["testReceipts"]).issubset(index["commandReceipts"])
assert not set(index["commandReceipts"]) & set(index["nonPassingReceipts"])
actual_receipts = {
    str(path.relative_to(packet)) for path in packet.glob("*.json")
    if "sourcesBefore" in json.loads(path.read_text())
}
assert actual_receipts == set(index["commandReceipts"]) | set(index["nonPassingReceipts"]), "unclassified receipt"
for relative in index["commandReceipts"] + index["nonPassingReceipts"]:
    receipt = json.loads(retained(relative).read_text())
    assert (receipt["exitCode"] == 0) == (relative in index["commandReceipts"]), relative
    assert datetime.datetime.fromisoformat(receipt["startedUtc"]) < datetime.datetime.fromisoformat(receipt["completedUtc"])
    assert receipt["sourcesBefore"] == receipt["sourcesAfter"], relative
    assert digest(retained(relative.removesuffix(".json") + ".log")) == receipt["outputSha256"], relative
    for source, expected in receipt["sourcesBefore"].items():
        location = receipt.get("retainedSourcePaths", {}).get(source, source)
        assert digest(retained(receipt["snapshot"] + "/" + location)) == expected, source
    if relative in index["testReceipts"]:
        assert receipt["binariesBefore"] == receipt["binariesAfter"], relative
assert set(index["scenarioManifests"]) == {
    str(path.relative_to(packet)) for path in packet.glob("scenarios/**/manifest.json")
}, "unlisted scenario manifest"
scenarios = []
for relative in index["scenarioManifests"]:
    manifest_path = retained(relative)
    manifest = json.loads(manifest_path.read_text())
    assert manifest["Result"] in ("passed-scenario-assertions-only", "failed-scenario-diagnostics-only"), relative
    if manifest["Result"] == "passed-scenario-assertions-only":
        scenarios.append(manifest["Scenario"])
    else:
        assert manifest["Scenario"] == "replica-restart-signal-timeout", relative
        assert manifest["Observations"]["Stage"] == "first-host-signal-after-second-replica-restart", relative
        assert manifest["Observations"]["SignalWaitBoundMilliseconds"] == 15_000, relative
        assert isinstance(manifest["Observations"]["ScenarioCancellationRequested"], bool), relative
    assert manifest["FirstHostProcessId"] != manifest["SecondHostProcessId"], relative
    assert manifest["FirstSidecarProcessId"] != manifest["SecondSidecarProcessId"], relative
    for artifact, expected in manifest["Artifacts"].items():
        assert digest(retained(str(manifest_path.parent.relative_to(packet)) + "/" + artifact)) == expected, artifact
    for source, expected in manifest["Sources"].items():
        location = manifest.get("RetainedSourcePaths", {}).get(source, "sources/" + source)
        assert digest(retained(str(manifest_path.parent.relative_to(packet)) + "/" + location)) == expected, source
    for host in ("first", "second"):
        metadata = json.loads((manifest_path.parent / f"{host}-sidecar.json").read_text())
        assert metadata["id"] == "eventstore" and metadata["runtimeVersion"] == "1.18.7", relative
    if manifest["Scenario"] == "provider-network-blackhole":
        assert manifest["Result"] == "passed-scenario-assertions-only"
        observed = manifest["Observations"]
        assert observed["FaultMode"] == "silent-bidirectional-byte-discard"
        assert observed["BlackholedBytes"] > 0
        assert observed["DisconnectedConnections"] == observed["RejectedConnections"] == 0
        assert observed["ActiveConnectionsBeforeFault"] > 0
        assert observed["RestoredVersions"] == [1, 1] and observed["FinalVersion"] == 2
        assert observed["RestoredETags"] == [observed["BeforeETag"]] * 2
        assert observed["DeniedClientsReceivedHints"] is False
        assert observed["OutagePublicationDisposition"] == "unknown-until-reconciliation"
        for name in ("PublishMilliseconds", "FirstReadMilliseconds", "SecondReadMilliseconds"):
            assert observed[name] < observed["CallOuterBoundMilliseconds"], name
assert "provider-network-blackhole" in scenarios and "replica-restart-and-serialized-bounds" in scenarios, scenarios
assert not any(path.suffix in (".props", ".targets", ".csproj", ".slnx") for path in packet.rglob("*") if path.is_file()), "executable MSBuild surface retained"
closing = json.loads(retained("closing-observations-final.json").read_text())
assert not any(item["existsAfterDisposal"] for item in closing["processes"].values())
assert not any(item["acceptingAfterDisposal"] for item in closing["proxyPorts"].values())
assert not closing["remainingOwnedFixtureDirectories"]
assert not closing["remainingOwnedSidecars"]
assert closing["sharedContainers"]["matchesInitialIdentities"]
print("PASS: packet integrity, explicit failure diagnostics, non-executable snapshots and local silent-fault evidence; replica-restart qualification remains unresolved; no owner/CI authority")
