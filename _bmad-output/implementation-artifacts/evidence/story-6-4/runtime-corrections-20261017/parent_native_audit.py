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
    results = []
    aliases = {"domain-debug-full": "domain-debug-full", "live-debug-full": "live-debug-full-native", "live-release-full": "live-release-full"}
    for slot, lane in aliases.items():
        packet_path = HERE / (lane + ".json")
        xml_path = HERE / (lane + ".xml")
        packet = json.loads(packet_path.read_text())
        if not xml_path.exists():
            row = {"slot": slot, "lane": lane, "packet": identity(packet_path), "exitCode": packet["exitCode"], "outcomes": None, "nativeXmlAvailable": False, "nativeGate": "failed/unproven", "limitation": "No final native XML: do not reconstruct complete counts or acceptance from partial console observations."}
            if lane.startswith("live-"):
                row["numericalAudit"] = check_vectors(HERE / (lane + "-vectors.json"))
            results.append(row)
            continue
        assembly, tests, counts, errors = native(xml_path)
        old_lane = "domain-debug-full" if slot == "domain-debug-full" else "live-release-full"
        _, old_tests, _, _ = native(HISTORICAL / ("qualification-20261017-" + old_lane + ".xml"))
        keys = lambda rows: {(row.get("type"), row.get("method"), row.get("name")) for row in rows}
        old_keys, new_keys = keys(old_tests), keys(tests)
        missing = sorted(old_keys - new_keys)
        skipped = [test.get("name") for test in tests if test.get("result") == "Skip"]
        old_skipped = [test.get("name") for test in old_tests if test.get("result") == "Skip"]
        assert not packet.get("sourceInputsChanges")
        assert not packet.get("runtimeChanges")
        assert not packet.get("assetsChanges")
        for field in ("log", "nativeXml"):
            bound = packet.get(field)
            if bound:
                assert digest(ROOT / bound["path"]) == bound["sha256"]
        skips_match = sorted(skipped) == sorted(old_skipped)
        passed = packet["exitCode"] == 0 and not counts["Fail"] and not errors and not int(assembly.get("not-run")) and not missing and skips_match
        row = {"slot": slot, "lane": lane, "packet": identity(packet_path), "nativeXml": identity(xml_path), "nativeXmlAvailable": True, "nativeGate": "pass" if passed else "failed/unproven", "nativeAssembly": assembly.attrib, "outcomes": dict(counts), "runnerErrors": len(errors), "exitCode": packet["exitCode"], "missingHistoricalCases": missing, "newCases": sorted(new_keys - old_keys), "skipsExactlyPreserved": skips_match, "stableSourceAssetsRuntimeDuringExecution": True}
        if lane.startswith("live-"):
            vectors = HERE / (lane + "-native-vectors.json")
            if not vectors.exists():
                vectors = HERE / (lane + "-vectors.json")
            row["numericalAudit"] = check_vectors(vectors)
        results.append(row)
    preparations = []
    for path in sorted(HERE.glob("live-*-controls.json")):
        packet = json.loads(path.read_text())
        if "full" in packet.get("lane", ""):
            preparations.append({"packet": identity(path), "lane": packet["lane"], "nativeStarted": packet.get("testPid") is not None, "nativeCommand": packet.get("testCommand"), "nativeExitCode": packet.get("testExitCode"), "wrapperFailure": packet.get("wrapperFailureClass")})
    for configuration in ("debug", "release"):
        started = [item for item in preparations if configuration in item["lane"] and item["nativeStarted"]]
        assert len(started) == 1, started
        assert not any(flag in started[0]["nativeCommand"] for flag in ("-method", "-class"))
    report = {"auditedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "head": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(), "script": identity(__file__), "aliases": aliases, "liveFullPreparations": preparations, "frames": results, "scope": "Three authorized full native reruns, original case/skip enrollment, stable execution inputs, native result counts and selected unchanged Current/tail budgets. The retained failed live-debug-full startup never invoked a native cohort; live-debug-full-native is its explicit single native replacement. Producer freshness, OQ8 copy identity, preservation and physical cleanup require their separate audits. No authenticated CI seal, owner assurance, production qualification or whole-queue acceptance is asserted."}
    target = HERE / "parent-native-audit.json"
    assert not target.exists()
    target.write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps({"report": str(target), "frames": [{"lane": row["lane"], "outcomes": row["outcomes"], "exitCode": row["exitCode"]} for row in results]}))


if __name__ == "__main__":
    main()
