"""Verify immutable entry bytes and the frozen source used by all three cohorts."""
import datetime
import hashlib
import json
import pathlib
import subprocess

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[4]
WRAPPER = "_bmad-output/implementation-artifacts/spec-6-4-projection-cost-and-sequence-guard-implementation.md"


def sha(path):
    return hashlib.sha256(pathlib.Path(path).read_bytes()).hexdigest()


def main():
    entry = json.loads((HERE / "entry.json").read_text())
    original_sources = json.loads((HERE / "parent-source-entry.json").read_text())["sourceInputs"]
    protected = {name: digest for name, digest in entry["preservedFiles"].items() if name != WRAPPER}
    changed = [name for name, digest in protected.items() if not (ROOT / name).is_file() or sha(ROOT / name) != digest]
    assert not changed, changed
    wrapper_entry = (HERE / "wrapper-entry.md").read_bytes()
    assert hashlib.sha256(wrapper_entry).hexdigest() == entry["preservedFiles"][WRAPPER]
    assert (ROOT / WRAPPER).read_bytes().startswith(wrapper_entry)
    candidate = None
    frames = []
    asset_objects = set()
    for name in ("domain-debug-full", "live-debug-full-native", "live-release-full"):
        packet = json.loads((HERE / (name + ".json")).read_text())
        for phase in ("before", "after"):
            sources = packet[phase]["sourceInputs"]
            if candidate is None:
                candidate = sources
            assert sources == candidate
            for digest in packet[phase]["assets"].values():
                archive = HERE / "asset-bytes" / (digest + ".json")
                assert archive.is_file() and sha(archive) == digest
                asset_objects.add(digest)
        frames.append({"name": name, "nativeWrapperExitCode": packet["exitCode"], "sourceInputs": len(candidate), "sourceAssetsRuntimeChanges": {key: packet[key] for key in ("sourceInputsChanges", "assetsChanges", "runtimeChanges")}})
    assert all((ROOT / name).is_file() and sha(ROOT / name) == digest for name, digest in candidate.items())
    source_changes = [name for name in sorted(original_sources.keys() | candidate.keys()) if original_sources.get(name) != candidate.get(name)]
    expected = {
        "tests/Hexalith.EventStore.DomainService.Tests/Processes/ProcessReadDeadlineTests.cs",
        "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestRuntimeConfiguration.cs",
        "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8DiscoveryConfiguration.cs",
        "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8DiscoveryConfigurationTests.cs",
        "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8FailedInitializationCleanupTests.cs",
        "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs",
    }
    assert set(source_changes) == expected, source_changes
    tracked_changes = set(subprocess.check_output(["git", "diff", "--name-only"], cwd=ROOT, text=True).splitlines())
    expected_tracked = (expected - {"tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8FailedInitializationCleanupTests.cs"}) | {WRAPPER}
    assert tracked_changes == expected_tracked, tracked_changes
    normative = (ROOT / "_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md").read_bytes()
    begin = b"<!-- HX-PCSG-V1-NORMATIVE-BEGIN -->\n"
    end = b"<!-- HX-PCSG-V1-NORMATIVE-END -->\n"
    assert normative.count(begin) == normative.count(end) == 1
    normative_digest = hashlib.sha256(normative[normative.index(begin) + len(begin):normative.index(end)]).hexdigest()
    assert normative_digest == "78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711"
    head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    index = hashlib.sha256(subprocess.check_output(["git", "ls-files", "-s", "-z"], cwd=ROOT)).hexdigest()
    assert head == entry["head"] and index == entry["indexSha256"]
    diff_check = subprocess.run(["git", "diff", "--check"], cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    assert diff_check.returncode == 0, diff_check.stdout
    report = {"checkedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "scriptSha256": sha(__file__), "head": head, "indexUnchanged": True, "protectedFilesUnchanged": len(protected), "originalWrapperBytesPreserved": len(wrapper_entry), "wrapperAppendOnly": True, "normativeSha256": normative_digest, "allNativeCohortsUseSameFrozenSourceInputs": True, "sourceInputCount": len(candidate), "changedSourceInputs": source_changes, "productionDependencyPolicyChanges": [], "nativeFrames": frames, "nativeAssetObjectsVerified": len(asset_objects), "gitDiffCheck": {"command": ["git", "diff", "--check"], "exitCode": diff_check.returncode}, "scope": "Preservation and identity checks only. A failed native cohort remains failed; this audit supplies no runtime approval or CI seal."}
    target = HERE / "parent-final-preservation-audit.json"
    assert not target.exists()
    target.write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps({"protectedFilesUnchanged": len(protected), "sourceInputs": len(candidate), "changedInputs": source_changes, "headAndIndexUnchanged": True, "normativeSha256": normative_digest}))


if __name__ == "__main__":
    main()
