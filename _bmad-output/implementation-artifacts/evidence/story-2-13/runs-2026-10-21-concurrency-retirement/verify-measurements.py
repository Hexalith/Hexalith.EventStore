#!/usr/bin/env python3
"""Cross-check final measurements; a separate closing inventory binds packet bytes."""
from pathlib import Path
import hashlib
import json
import re

if not __debug__:
    raise RuntimeError("Optimized Python execution is prohibited")
packet = Path(__file__).resolve().parent

def need(value, message):
    if not value:
        raise RuntimeError(message)

def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

def read(name):
    return json.loads((packet / name).read_text())

runtime = read("candidate-runtime-identity.json")
acquisition = read("runtime-acquisition.json")
need(runtime["sourceRevision"] == acquisition["sourceRevision"], "Source revision mismatch")
need(runtime["toolchainSha256"] == acquisition["toolchain"]["sha256"], "Toolchain mismatch")
need(runtime["dependencyManifests"] == acquisition["dependencies"], "Dependency manifest mismatch")
need(runtime["compiledDependenciesEqualRetainedCandidate"] is True, "Compiled dependency comparison missing")
need(runtime["patchSha256"] == sha(packet / "candidate-from-upstream.patch"), "Exact patch mismatch")
need(read("before-candidate-build-source-inventory.json") == read("after-candidate-build-source-inventory.json"), "Source changed across build")
preservation = read("final-source-preservation.json")
need(preservation["candidateSourceUnchangedAfterBuild"] and preservation["retainedSourceUnchanged"] and preservation["rejectedResetSourceAndEvidenceUnchanged"], "Runtime/source preservation failed")
need(preservation["candidateUsesResetConnectBackoff"] is False, "Rejected reset API remains")
passing = [
    "retirement-final-focused-controls", "retirement-runtime-build", "retirement-final-focused-reused-endpoint",
    "retirement-final-complete-distribution-class", "retirement-final-go-regressions",
    "retirement-focused-application-regressions", "retirement-real-projection-composition",
    "retirement-ordinary-direct", "retirement-retained-redis",
]
records = {name: read(name + ".json") for name in passing + ["retirement-cancel-guard-mutation-red"]}
for name, record in records.items():
    need(record["sourcesBefore"] == record["sourcesAfter"], "Source changed during " + name)
    need(record["externalInputsBefore"] == record["externalInputsAfter"], "Runtime source changed during " + name)
    need(record["binariesBefore"] == record["binariesAfter"], ".NET binary changed during " + name)
    need(record["fixtureRuntimeSha256Before"] == record["fixtureRuntimeSha256After"], "Selected runtime changed during " + name)
    need(record["outputSha256"] == sha(packet / (name + ".log")), "Command log mismatch: " + name)
    for source, expected in record["sourcesBefore"].items():
        need(sha(packet / record["snapshot"] / record["retainedSourcePaths"][source]) == expected, "Source snapshot mismatch: " + source)
for name in passing:
    need(records[name]["exitCode"] == 0, "Required final check failed: " + name)
for name in ["retirement-final-focused-reused-endpoint", "retirement-final-complete-distribution-class", "retirement-real-projection-composition"]:
    need(records[name]["fixtureRuntimePath"] == runtime["candidatePath"], "Integrated runtime path mismatch")
    need(records[name]["fixtureRuntimeSha256Before"] == runtime["candidateSha256"], "Integrated runtime hash mismatch")
mutant = records["retirement-cancel-guard-mutation-red"]
need(mutant["exitCode"] != 0, "Cancellation guard mutation was not rejected")
mutation_output = (packet / "retirement-cancel-guard-mutation-red.log").read_text()
need("cancelled Unavailable must not retire the shared channel" in mutation_output, "Mutation failed for an unexpected cause")
need("PASS: TestReviewCanceledUnavailableRetainsChannel" in mutation_output, "Retained control discrimination limit absent")
complete = records["retirement-final-complete-distribution-class"]
need(not any("stoponfail" in value.lower() for value in complete["command"]), "Complete class stopped on failure")
need(re.search(r"Total:\s+6, Errors:\s+0, Failed:\s+0, Skipped:\s+0, Not Run:\s+0", (packet / "retirement-final-complete-distribution-class.log").read_text()), "Six-case complete result absent")
need(records["retirement-final-focused-reused-endpoint"]["completedUtc"] <= complete["startedUtc"], "Focused endpoint did not precede full class")
need(complete["completedUtc"] <= records["retirement-final-go-regressions"]["startedUtc"], "Final Go regressions did not follow full class")
go_output = (packet / "retirement-final-go-regressions.log").read_text()
need("PASS: TestRetirementResolverChurn" in go_output and "25000 resolver updates" in go_output, "Post-class retirement resolver-churn control absent")
need("-race" in records["retirement-final-go-regressions"]["command"], "Final Go race detector omitted")
timelines = read("recovery-timelines.json")["timelines"]
need(len(timelines) >= 2, "Focused/full reused-endpoint timelines missing")
for item in timelines:
    need(item["candidateSha256"] == runtime["candidateSha256"], "Timeline runtime mismatch")
    need(item["sameInternalPort"] and item["initialOwner"] == "second-proof-host", "Reused-endpoint ownership comparison changed")
    need(item["observedRetirementAndRecoveryWithinMeasuredInvocation"] and item["signalWaitBoundMilliseconds"] == 15000, "Measured retirement/recovery or unchanged signal wait absent")
    manifest = packet / item["manifest"]
    need(sha(manifest) == item["manifestSha256"], "Timeline manifest mismatch")
    need(sha(manifest.parent / "first.log") == item["firstLogSha256"] and sha(manifest.parent / "second.log") == item["secondLogSha256"], "Timeline log mismatch")
for manifest in packet.glob("scenarios/*/manifest.json"):
    value = json.loads(manifest.read_text())
    need(value["FirstSidecarExecutableSha256"] == runtime["candidateSha256"] == value["SecondSidecarExecutableSha256"], "Scenario candidate mismatch")
    for name, expected in value["Artifacts"].items():
        need(sha(manifest.parent / name) == expected, "Scenario artifact changed: " + name)
rejected = packet.parent / "runs-2026-10-21-concurrency-safety"
need(json.loads((rejected / "pinned-reset-resolver-churn.json").read_text())["exitCode"] != 0, "Rejected reset failure absent")
reset_log = (rejected / "pinned-reset-resolver-churn.log").read_text()
need("WARNING: DATA RACE" in reset_log and "fatal error: concurrent map iteration and map write" in reset_log, "Rejected API mechanism evidence absent")
print(json.dumps({"result": "passed-measurement-cross-checks-only", "candidateSha256": runtime["candidateSha256"], "reusedEndpointTraces": len(timelines), "requiredSuccessfulCommands": len(passing), "authority": "no completion or production authority"}, indent=2))
