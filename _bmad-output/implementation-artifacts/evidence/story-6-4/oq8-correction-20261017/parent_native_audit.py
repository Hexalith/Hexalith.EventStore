"""Independently audit the three authorized native reruns without executing them."""
import collections
import datetime
import hashlib
import json
import pathlib
import subprocess
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[5]
HERE = pathlib.Path(__file__).resolve().parent
HISTORICAL = HERE.parent


def digest(path):
    return hashlib.sha256(pathlib.Path(path).read_bytes()).hexdigest()


def identity(path):
    path = pathlib.Path(path)
    return {"path": str(path.relative_to(ROOT)), "sha256": digest(path), "bytes": path.stat().st_size}


def native(path):
    root = ET.parse(path).getroot()
    assembly = root.find("assembly")
    tests = root.findall(".//test")
    counts = collections.Counter(test.get("result") for test in tests)
    assert len(tests) == int(assembly.get("total"))
    for key, result in (("passed", "Pass"), ("failed", "Fail"), ("skipped", "Skip")):
        assert counts[result] == int(assembly.get(key))
    return assembly, tests, counts, root.findall(".//error")


def check_vectors(path):
    if not path.exists():
        return {"available": False, "claim": "No missing observation is treated as zero."}
    observations = json.loads(path.read_text())["observations"]
    current = [item["value"] for item in observations if item["kind"] == "ProjectionGuardCurrentMatrix"]
    tails = [item["value"] for item in observations if item["kind"] == "ProjectionGuardLiveCost"]
    results = {"file": identity(path), "counts": dict(collections.Counter(item["kind"] for item in observations)), "current": [], "tails": []}
    zeroes = ("SourceRanges", "SourceEventReads", "SourceDeserializations", "HandlerAttempts", "FoldAttempts", "BatchExecutions", "ModelMutations", "ProofWriteAttempts", "MutationAttempts", "SchedulerWriteAttempts", "ActivationWriteAttempts")
    for row in current:
        cost = row["cost"]
        assert all(cost[key] == 0 for key in zeroes)
        assert (cost["SourceInspections"], cost["SourceMetadataReads"], cost["ProofReads"], cost["MarkerReads"], cost["MetadataReads"]) == (2, 2, 1, 2, 6)
        size = 16384 + 80 * row["head"] + 512 * min(row["head"], row["receiptLimit"])
        assert sum(cost[key] for key in ("SourceMetadataBytes", "ProofReadBytes", "MarkerReadBytes", "MetadataReadBytes")) <= size + 114688
        assert row["native"].get("primary/caller_actor_http/InspectSourceProofAsync", 0) == 2
        assert row["native"].get("primary/caller_actor_http/ReadMeasuredEventsBoundedRangeAsync", 0) == 0
        results["current"].append({key: row[key] for key in ("head", "checkpoint", "pageSize", "receiptLimit")})
    for row in tails:
        cost = row["cost"]
        delta, checkpoint, limit = row["delta"], row["checkpoint"], row["receiptLimit"]
        pages = (delta + row["pageSize"] - 1) // row["pageSize"]
        assert row["pages"] == pages
        assert cost["SourceRanges"] <= 3 * pages
        for key in ("SourceReturnedEvents", "SourceEventReads", "SourceDeserializations", "SourceEventHashes", "SourceChainExtensions", "RouteEventHashes"):
            assert cost[key] <= 3 * delta
        assert cost["HandlerAttempts"] == cost["BatchExecutions"] == delta
        assert cost["ModelMutations"] == 2 * delta
        assert cost["SourceLookaheadEvents"] == 0
        assert cost["ProofWriteAttempts"] == (3 if delta else 0)
        assert cost["SourceInspections"] <= 3
        assert cost["SourceMetadataReads"] <= 3 + 6 * pages
        assert cost["ProofReads"] <= 8
        assert cost["MarkerReads"] == 2 * cost["ProofReads"]
        assert cost["MetadataReads"] <= 96 + 32 * delta
        assert cost["MutationAttempts"] <= 96 + 32 * delta
        size = 16384 + 80 * (checkpoint + delta) + 512 * min(checkpoint + delta, limit)
        assert cost["ProofReadBytes"] <= 8 * size
        assert cost["ProofWriteBytes"] <= 4 * size
        assert cost["ProofVisits"] <= 24 * (checkpoint + 2 * delta + min(checkpoint + delta, limit))
        results["tails"].append({key: row[key] for key in ("head", "checkpoint", "delta", "pageSize", "receiptLimit")})
    expected_current = {(head, page) for head in (1, 256, 257, 9999, 10000) for page in (1, 16, 256)}
    expected_tails = {(1, 0, 1), (1, 1, 1), (1, 256, 256), (1, 257, 256), (1, 771, 256), (9000, 771, 256), (9999, 1, 256)}
    results["completeCurrentMatrix"] = {(row["head"], row["pageSize"]) for row in current} == expected_current
    results["completeTailMatrix"] = {(row["checkpoint"], row["delta"], row["pageSize"]) for row in tails} == expected_tails
    results["originalReplicaRecoveryWitness"] = any(item["kind"] == "ProjectionGuardNativeAttempts" and item["value"].get("mode") == "two_replica_original_receipt_recovery" for item in observations)
    return results


def main():
    producer_path = HERE / "release-build-baseline.json"
    producer = json.loads(producer_path.read_text())
    assert producer["exitCode"] == 0
    assert producer["diagnostics"]["warningLines"] == producer["diagnostics"]["errorLines"] == 0
    assert not producer["sourceInputsChanges"] and not producer["assetsChanges"]
    _, old_tests, _, _ = native(HERE.parent / "runtime-corrections-20261017/live-release-full.xml")
    old_keys = {(t.get("type"), t.get("method"), t.get("name")) for t in old_tests}
    old_skips = sorted(t.get("name") for t in old_tests if t.get("result") == "Skip")
    results = []
    full_controls = []
    for path in HERE.glob("*-controls.json"):
        controls = json.loads(path.read_text())
        if controls.get("testPid") and not any(flag in controls["testCommand"] for flag in ("-method", "-class")):
            full_controls.append(path.name)
    assert full_controls == ["release-live-full-controls.json"], full_controls
    for lane in ("oq8-baseline", "release-live-full"):
        packet_path = HERE / (lane + ".json")
        packet = json.loads(packet_path.read_text())
        controls_path = HERE / (lane + "-controls.json")
        controls = json.loads(controls_path.read_text())
        assembly, tests, counts, errors = native(HERE / (lane + ".xml"))
        for category in ("sourceInputs", "assets", "runtime"):
            assert not packet[category + "Changes"], (lane, category)
            assert producer["after"][category] == packet["before"][category], (lane, category, "producer closure mismatch")
        for field in ("log", "nativeXml"):
            bound = packet[field]
            assert digest(ROOT / bound["path"]) == bound["sha256"]
        assert controls["nativeLogSha256"] == digest(HERE / (lane + ".native.log"))
        assert packet["exitCode"] == controls["testExitCode"]
        assert not controls.get("outerTimedOut")
        assert controls["outerLimitSeconds"] == 2700 and controls["outerDrainSeconds"] == 30
        assert not controls["ownedChildrenAliveAfterNative"]
        assert controls["scratchRemoved"]
        assert all(controls["containerPhysicalAbsence"].values())
        assert all(controls["volumePhysicalAbsence"].values())
        assert all(controls["physicalPortsClosed"].values())
        assert all(controls["resourceDirectoriesRemoved"].values())
        for role, binding in controls["producerBindings"].items():
            assert binding["sha256"] == digest(producer_path)
            for name, value in binding["consumedOutputs"].items():
                assert producer["after"]["runtime"][name] == packet["before"]["runtime"][name] == value
        keys = {(t.get("type"), t.get("method"), t.get("name")) for t in tests}
        skipped = sorted(t.get("name") for t in tests if t.get("result") == "Skip")
        missing = sorted(old_keys - keys) if lane == "release-live-full" else []
        skipped_match = skipped == old_skips if lane == "release-live-full" else not skipped
        oq8 = [t for t in tests if t.get("method") == "ProductionMatrix_IndependentProcessesPreserveAuthorityReplayExpiryAndLeakageInvariants"]
        assert len(oq8) == 1
        observations_path = HERE / (lane + "-oq8/observations.json")
        observations = json.loads(observations_path.read_text())["observations"]
        expected_groups = {"writers_failover", "expiry_compaction", "authority_change", "capture"}
        passed = packet["exitCode"] == 0 and not counts["Fail"] and not errors and not int(assembly.get("not-run")) and not missing and skipped_match
        row = {"lane": lane, "packet": identity(packet_path), "controls": identity(controls_path), "nativeXml": packet["nativeXml"], "nativeCommand": controls["testCommand"], "nativeAssembly": assembly.attrib, "outcomes": dict(counts), "exitCode": packet["exitCode"], "nativeGate": "pass" if passed else "failed/unproven", "missingHistoricalCases": missing, "skipsPreserved": skipped_match, "sourceAssetsRuntimeStable": True, "oq8Result": oq8[0].get("result"), "oq8Observations": identity(observations_path), "oq8Groups": sorted(observations), "allOq8GroupsReached": set(observations) == expected_groups, "cleanupReceiptAssertionsPassed": True}
        if lane == "release-live-full":
            row["numericalAudit"] = check_vectors(HERE / (lane + "-vectors.json"))
        results.append(row)
    body = (ROOT / "_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md").read_bytes()
    begin = b"<!-- HX-PCSG-V1-NORMATIVE-BEGIN -->\n"
    end = b"<!-- HX-PCSG-V1-NORMATIVE-END -->\n"
    start = body.index(begin) + len(begin)
    normative = hashlib.sha256(body[start:body.index(end, start)]).hexdigest()
    assert normative == "78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711"
    report = {"auditedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "head": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(), "script": identity(__file__), "producer": identity(producer_path), "fullNativeInvocations": full_controls, "frames": results, "normativeSha256": normative, "scope": "Independent retained producer/native/result/enrollment/budget/cleanup-receipt audit. No additional build/test executed. Passing local runs do not supply authenticated CI or owner/formal acceptance."}
    target = HERE / "parent-native-audit.json"
    assert not target.exists()
    target.write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps({"report": str(target), "frames": [{"lane": r["lane"], "outcomes": r["outcomes"], "exitCode": r["exitCode"]} for r in results]}))


if __name__ == "__main__":
    main()
