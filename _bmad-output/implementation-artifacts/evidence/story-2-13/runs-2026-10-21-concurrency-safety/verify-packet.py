#!/usr/bin/env python3
"""Fail-closed integrity and identity checks; local evidence conveys no approval."""
from pathlib import Path
import datetime
import hashlib
import json
import re
import sys

if not __debug__:
    raise RuntimeError("Optimized Python execution is prohibited")
packet = Path(__file__).resolve().parent
repository = next(path for path in packet.parents if (path / "Hexalith.EventStore.slnx").exists())

def need(condition, message):
    if not condition:
        raise RuntimeError(message)

def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

def read(name):
    return json.loads((packet / name).read_text())

index = read("index.json")
need(index.get("authority") == "local qualification only; no completion or production authority", "Unknown packet authority")
excluded = {"index.json", "verification.json"}
actual = {str(path.relative_to(packet)) for path in packet.rglob("*") if path.is_file()} - excluded
need(actual == set(index["files"]), "Packet membership differs from its closing inventory")
for name, entry in index["files"].items():
    path = packet / name
    need(path.stat().st_size == entry["bytes"] and sha(path) == entry["sha256"], "Changed packet file: " + name)

mandatory = [
    "start-preservation.json", "spec-before.source", "runtime-acquisition.json",
    "retained-source-inventory.json", "before-red-source-inventory.json",
    "before-candidate-build-source-inventory.json", "after-candidate-build-source-inventory.json",
    "candidate-from-upstream.patch", "correction-from-retained.patch", "candidate-runtime-identity.json",
    "retained-candidate-concurrency-red.json", "pristine-router-held-user-controls-green.json",
    "candidate-concurrency-context-green.json", "cancel-guard-mutation-red.json",
    "candidate-runtime-build.json", "candidate-focused-reused-endpoint.json",
    "candidate-complete-distribution-class.json", "final-go-router-manager-regressions.json",
    "focused-application-regressions.json", "real-projection-composition-regression.json",
    "ordinary-direct-regression.json", "retained-redis-baseline-regression.json",
    "pinned-reset-resolver-churn.json", "recovery-timelines.json", "handoff-identities.json",
]
need(all(name in index["files"] for name in mandatory), "Required final evidence is absent")
runtime = read("candidate-runtime-identity.json")
acquisition = read("runtime-acquisition.json")
need(read("before-candidate-build-source-inventory.json") == read("after-candidate-build-source-inventory.json"), "Source inventory differs across build")
need(runtime["sourceRevision"] == acquisition["sourceRevision"], "Source revision mismatch")
need(runtime["patchSha256"] == sha(packet / "candidate-from-upstream.patch"), "Candidate patch identity mismatch")
need(runtime["toolchainSha256"] == acquisition["toolchain"]["sha256"], "Toolchain identity mismatch")
need(runtime["dependencyManifests"] == acquisition["dependencies"], "Dependency manifest mismatch")
need(runtime["compiledDependenciesEqualRetainedCandidate"] is True, "Compiled dependency comparison absent")
need(runtime["sourceInventoriesEqual"] is True, "Source build comparison absent")
need(runtime["beforeBuildSourceInventorySha256"] == sha(packet / "before-candidate-build-source-inventory.json"), "Before-build source identity mismatch")
need(runtime["afterBuildSourceInventorySha256"] == sha(packet / "after-candidate-build-source-inventory.json"), "After-build source identity mismatch")

receipts = {}
for path in packet.glob("*.json"):
    value = json.loads(path.read_text())
    if isinstance(value, dict) and all(key in value for key in ("command", "sourcesBefore", "sourcesAfter", "outputSha256")):
        label = value["label"]
        receipts[label] = value
        need(value["sourcesBefore"] == value["sourcesAfter"], "Source changed during " + label)
        need(value["binariesBefore"] == value["binariesAfter"], ".NET binary changed during " + label)
        need(value["externalInputsBefore"] == value["externalInputsAfter"], "Runtime input changed during " + label)
        need(value["fixtureRuntimeSha256Before"] == value["fixtureRuntimeSha256After"], "Selected runtime changed during " + label)
        need(value["outputSha256"] == sha(packet / (label + ".log")), "Command output mismatch: " + label)
        for original, expected in value["sourcesBefore"].items():
            retained = packet / value["snapshot"] / value["retainedSourcePaths"][original]
            need(sha(retained) == expected, "Source snapshot mismatch: " + original)
        for argument in value["command"]:
            if argument.startswith("-overlay="):
                overlay = json.loads(Path(argument.split("=", 1)[1]).read_text())
                for replacement in overlay["Replace"].values():
                    need(Path(replacement).resolve().is_relative_to(packet), "Overlay replacement outside retained packet")

need(receipts["retained-candidate-concurrency-red"]["exitCode"] != 0, "Original concurrency failure absent")
red = (packet / "retained-candidate-concurrency-red.log").read_text()
need(all("held=" + kind in red for kind in ("actor", "reminder", "stream")), "Original overlap observations incomplete")
need("stateAfterFault=SHUTDOWN" in red and "the client connection is closing" in red, "Original close mechanism observation absent")
need(receipts["cancel-guard-mutation-red"]["exitCode"] != 0, "Discriminating cancellation mutation failure absent")
mutation = (packet / "cancel-guard-mutation-red.log").read_text()
need("cancelled Unavailable must not reset another caller's shared backoff" in mutation, "Cancellation mutation failed for a different cause")
need("PASS: TestReviewCanceledUnavailableRetainsChannel" in mutation, "Retained control does not demonstrate its discrimination limit")

passing = [
    "pristine-router-held-user-controls-green", "candidate-concurrency-context-green", "candidate-runtime-build",
    "candidate-focused-reused-endpoint", "candidate-complete-distribution-class", "final-go-router-manager-regressions",
    "focused-application-regressions", "real-projection-composition-regression", "ordinary-direct-regression",
    "retained-redis-baseline-regression",
]
for label in passing:
    need(receipts[label]["exitCode"] == 0, "Required successful local check failed: " + label)
integrated = ["candidate-focused-reused-endpoint", "candidate-complete-distribution-class", "real-projection-composition-regression"]
for label in integrated:
    need(receipts[label]["fixtureRuntimeSha256Before"] == runtime["candidateSha256"], "Integrated check selected a different runtime: " + label)
    need(receipts[label]["fixtureRuntimePath"] == runtime["candidatePath"], "Integrated check runtime path mismatch: " + label)
complete = receipts["candidate-complete-distribution-class"]
need(not any("stoponfail" in argument.lower() for argument in complete["command"]), "Complete class stopped on failure")
need(re.search(r"Total:\s+6, Errors:\s+0, Failed:\s+0, Skipped:\s+0, Not Run:\s+0", (packet / "candidate-complete-distribution-class.log").read_text()), "Complete six-case class result absent")
need(receipts["candidate-focused-reused-endpoint"]["completedUtc"] <= complete["startedUtc"], "Focused reuse did not precede full class")
need(complete["completedUtc"] <= receipts["final-go-router-manager-regressions"]["startedUtc"], "Final regressions did not follow the full class")

timelines = read("recovery-timelines.json")["timelines"]
need(len(timelines) >= 2, "Focused and full-class reused-endpoint timelines are both required")
for item in timelines:
    need(item["candidateSha256"] == runtime["candidateSha256"], "Timeline runtime mismatch")
    need(item["sameInternalPort"] and item["signalWaitBoundMilliseconds"] == 15000, "Reused endpoint or signal bound changed")
    need(item["observedResetAndRecoveryWithinMeasuredInvocation"], "Measured in-place recovery markers absent")
    manifest = packet / item["manifest"]
    need(sha(manifest) == item["manifestSha256"], "Timeline manifest mismatch")
    need(sha(manifest.parent / "first.log") == item["firstLogSha256"], "Timeline first log mismatch")
    need(sha(manifest.parent / "second.log") == item["secondLogSha256"], "Timeline second log mismatch")

for path in packet.glob("scenarios/*/manifest.json"):
    manifest = json.loads(path.read_text())
    need(manifest["FirstSidecarExecutableSha256"] == runtime["candidateSha256"] == manifest["SecondSidecarExecutableSha256"], "Scenario runtime identity mismatch")
    for name, expected in manifest["Artifacts"].items():
        need(sha(path.parent / name) == expected, "Scenario artifact mismatch: " + name)

handoff = read("handoff-identities.json")
for name, expected in handoff["files"].items():
    need(sha(repository / name) == expected, "External handoff changed: " + name)
spec = repository / "_bmad-output/implementation-artifacts/spec-2-13-dapr-notification-distribution-qualification.md"
frozen = lambda data: re.search(rb"<frozen-after-approval\b.*?</frozen-after-approval>", data, re.S).group()
need(frozen(spec.read_bytes()) == frozen((packet / "spec-before.source").read_bytes()), "Frozen intent changed")
need(re.search(r"^status:\s*['\"]?in-progress['\"]?\s*$", spec.read_text(), re.M), "Spec status advanced")
story = repository / "_bmad-output/implementation-artifacts/2-13-dapr-notification-distribution-qualification.md"
need(re.search(r"^status:\s*backlog\s*$", story.read_text(), re.M), "Original story status advanced")
sprint = repository / "_bmad-output/implementation-artifacts/sprint-status.yaml"
need(re.search(r"^\s+2-13-dapr-notification-distribution-qualification:\s*in-progress\s*(?:#.*)?$", sprint.read_text(), re.M), "Sprint status advanced")
need(re.search(r"^accepted_exceptions:\s*\[\]\s*$", (repository / "docs/architecture/dapr-infrastructure-exceptions.yaml").read_text(), re.M), "Exception register changed")
summary = {"verifiedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "indexSha256": sha(packet / "index.json"), "verifiedFiles": len(index["files"]), "result": "passed-integrity-checks-only", "authority": "no completion or production authority"}
print(json.dumps(summary, indent=2))
if "--write-receipt" in sys.argv:
    need(not (packet / "verification.json").exists(), "Verification receipt already exists")
    (packet / "verification.json").write_text(json.dumps(summary, indent=2) + "\n")
