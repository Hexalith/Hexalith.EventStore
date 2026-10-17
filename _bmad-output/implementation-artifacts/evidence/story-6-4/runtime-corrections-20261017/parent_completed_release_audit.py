"""Read-only audit of completed Release identities and historical failure disposition."""
import collections
import datetime
import hashlib
import json
import pathlib
import xml.etree.ElementTree as ET

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[4]


def sha(path):
    return hashlib.sha256(pathlib.Path(path).read_bytes()).hexdigest()


def identity(path):
    return {"path": str(path.relative_to(ROOT)), "sha256": sha(path)}


def tests(path):
    return ET.parse(path).findall(".//test")


def main():
    dispositions = []
    for old, fresh in (("domain-debug-full", "domain-debug-full"),
                       ("live-debug-full", "live-debug-full-native"),
                       ("live-release-full", "live-release-full")):
        old_path = HERE.parent / ("qualification-20261017-" + old + ".xml")
        new_path = HERE / (fresh + ".xml")
        failed = [test for test in tests(old_path) if test.get("result") == "Fail"]
        current = tests(new_path)
        rows = []
        for test in failed:
            matches = [row for row in current
                       if (row.get("type"), row.get("method"))
                       == (test.get("type"), test.get("method"))]
            assert matches and all(row.get("result") == "Pass" for row in matches)
            rows.append({"type": test.get("type"), "method": test.get("method"),
                         "currentOutcomes": [row.get("result") for row in matches]})
        dispositions.append({"historical": identity(old_path), "fresh": identity(new_path),
                             "historicalFailedCases": len(failed), "methods": rows})

    packet_path = HERE / "live-release-full.json"
    packet = json.loads(packet_path.read_text())
    producer_path = ROOT / packet["producer"]["path"]
    assert sha(producer_path) == packet["producer"]["sha256"]
    producer = json.loads(producer_path.read_text())
    assert producer["exitCode"] == 0
    prefixes = {
        "entryHosts": ("src/Hexalith.EventStore/bin/Release/net10.0/",
                       "samples/Hexalith.EventStore.Sample/bin/Release/net10.0/"),
        "testAssembly": ("tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/",),
    }
    producer_bindings = {}
    for role, roots in prefixes.items():
        consumed = {key: value for key, value in packet["before"]["runtime"].items()
                    if key.startswith(roots)}
        assert consumed
        assert all(producer["after"]["runtime"].get(key) == value
                   for key, value in consumed.items())
        producer_bindings[role] = {"producer": identity(producer_path),
                                   "matchingConsumedOutputHashes": len(consumed)}

    inventory_path = HERE / "parent-oq8-input-inventory.json"
    inventory = json.loads(inventory_path.read_text())["configurations"]["Release"]
    receipt_path = HERE / "live-release-full-oq8/observations.json"
    receipt = json.loads(receipt_path.read_text())
    expected = {
        "eventStoreSha256": inventory["roles"]["eventstore"]["entrySha256"],
        "sampleSha256": inventory["roles"]["sample"]["entrySha256"],
        "eventStoreRuntimeSetSha256": inventory["roles"]["eventstore"]["shadowSetSha256"],
        "sampleRuntimeSetSha256": inventory["roles"]["sample"]["shadowSetSha256"],
        "hostingStartupSha256": inventory["hostingStartupSha256"],
        "additionalDepsSha256": inventory["additionalDepsSha256"],
    }
    assert receipt["artifacts"] == expected
    failed = [row for row in tests(HERE / "live-release-full.xml") if row.get("result") == "Fail"]
    assert len(failed) == 1
    failure = failed[0]
    assert failure.get("method") == "ProductionMatrix_IndependentProcessesPreserveAuthorityReplayExpiryAndLeakageInvariants"
    assert "HttpClient.Timeout of 30 seconds" in failure.findtext("failure/message")
    assert not receipt["observations"]
    report = {
        "auditedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "script": identity(pathlib.Path(__file__)),
        "historicalFailureDispositions": dispositions,
        "allOriginallyFailedMethodsPassInCorrespondingFreshCohorts": True,
        "releaseProducerBindings": producer_bindings,
        "Oq8Receipt": identity(receipt_path),
        "Oq8Inventory": identity(inventory_path),
        "Oq8ActualShadowCopyHashesMatchIndependentInputs": True,
        "releaseFailure": {"type": failure.get("type"), "method": failure.get("method"),
                           "exceptionType": failure.find("failure").get("exception-type"),
                           "message": failure.findtext("failure/message"),
                           "stack": failure.findtext("failure/stack-trace")},
        "releaseOq8InvariantObservations": receipt["observations"],
        "releaseAcceptance": "failed/unproven",
        "limitation": "Copy identity, other passing tests and numerical vectors do not prove the later OQ8 assertions that this failed invocation did not reach. Private discovery registration deadlines are observed; their underlying cause is unproven. No additional native invocation was made.",
    }
    target = HERE / "parent-completed-release-audit.json"
    assert not target.exists()
    target.write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps({"report": str(target.relative_to(ROOT)),
                      "originalFailedCasesNowPass": {row["fresh"]["path"]: row["historicalFailedCases"]
                                                     for row in dispositions},
                      "releaseCopyIdentityMatches": True,
                      "releaseAcceptance": "failed/unproven"}))


if __name__ == "__main__":
    main()
