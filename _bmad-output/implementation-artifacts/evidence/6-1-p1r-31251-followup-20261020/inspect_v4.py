"""Read retained V4 evidence without executing its historical tools or any runtime.

Print deterministic forensic observations; this is not a qualification validator.
Run from any directory. Only fixed local Git objects and retained files are read.
"""
import copy
import hashlib
import json
import re
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[4]
OLD = ROOT / "_bmad-output/implementation-artifacts/evidence/6-1-p1r-31251-published-run"
FINAL = OLD / "final-attempt"
EXECUTION = FINAL / "execution-e262ce52e42b4c64a436ac3841c17ac9"
OBSERVATION = FINAL / "observation-754c6a84815246a6b5af12b3d47841e2"
TAG_SHA = "55432c50d4b6f1666b4bd6d4a60bfc263b2d0e0b"
PINS = {}


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":")).encode()


def read(path):
    data = path.read_bytes()
    PINS[str(path.relative_to(ROOT))] = digest(data)
    return json.loads(data)


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT)


def counter(checks):
    require(all(type(c["passed"]) is bool for c in checks), "non-Boolean check")
    passed = sum(c["passed"] for c in checks)
    return {"attempted": len(checks), "passed": passed, "failed": len(checks) - passed}


index = read(FINAL / "canonical-v4-index.json")
require(PINS[str((FINAL / "canonical-v4-index.json").relative_to(ROOT))]
        == "f62f14434c352c7a098b3d45f2c772f1384ab543e6d0ac03039389440900b228", "wrong canonical V4")
parent = read(OLD / "reconciliation/selected-final-parent-audit-9895165b155e48f2852f55b91c9bea35.json")
execution = read(EXECUTION / "execution.json")
commands = {row["id"]: row for row in read(EXECUTION / "commands.json")}
inputs = read(OBSERVATION / "owner-inputs.json")
decisions = read(OBSERVATION / "owner-decisions.json")
source = read(EXECUTION / "executor-source.json")
receipts = {}
failures = []
case_count = 0
totals = {key: 0 for key in ("attempted", "passed", "failed")}
for lane, binding in parent["lanes"].items():
    path = EXECUTION / "receipts" / Path(binding["path"]).name
    receipt = read(path)
    require(digest(path.read_bytes()) == binding["sha256"], "changed lane receipt")
    require(receipt["lane"] == lane, "lane mismatch")
    measured = {key: 0 for key in totals}
    for case in receipt["cases"]:
        actual = counter(case["checks"])
        require(actual == case["assertions"], "case counter mismatch")
        for key in totals:
            measured[key] += actual[key]
        failures.extend({"lane": lane, "case": case["id"], "check": c["id"]}
                        for c in case["checks"] if not c["passed"])
    require(measured == receipt["assertions"] == binding["assertions"], "lane counter mismatch")
    for key in totals:
        totals[key] += measured[key]
    case_count += len(receipt["cases"])
    receipts[lane] = receipt
require(totals == {"attempted": 4804, "passed": 4795, "failed": 9} and case_count == 163
        and len(receipts) == 19, "wrong V4 result inventory")
recovery = {}
for name, binding in parent["recovery"].items():
    path = EXECUTION / "receipts" / Path(binding["path"]).name
    receipt = read(path)
    require(digest(path.read_bytes()) == binding["sha256"], "changed recovery receipt")
    require(counter(receipt["checks"]) == receipt["assertions"] == binding["assertions"],
            "recovery counter mismatch")
    recovery[name] = receipt
require(len(execution["receipts"]) == 21 and execution["errors"] == [], "wrong execution frame")

candidate = inputs["candidate"]
require(candidate["version"] == "3.125.1" and candidate["tag_commit"] == TAG_SHA
        and source["candidate_source"]["main"]["head"] == TAG_SHA
        and git("rev-parse", "v3.125.1^{commit}").decode().strip() == TAG_SHA, "candidate changed")
require(source["candidate_source"]["main"]["diff_sha256"] == digest(b""), "dirty recorded source")
require(source["candidate_source"]["main"]["gitlinks"]["references/Hexalith.Builds"]
        == candidate["tag_builds_commit"] == "4629ff2d3bd66376b29a553663c241d3a7b87853", "tag Builds changed")
archive_checks = []
for package in candidate["packages"]:
    path = OLD / "planning-observations-verified/archives" / (package["id"] + ".3.125.1.nupkg")
    sha = digest(path.read_bytes())
    PINS[str(path.relative_to(ROOT))] = sha
    require(sha == package["archive_sha256"] and package["repository_commit"] == TAG_SHA,
            "published archive mismatch")
    archive_checks.append({"id": package["id"], "archive_sha256": sha, "bytes": path.stat().st_size})

checkout = receipts["checkout"]
frozen = checkout["execution_evidence"]["predicate_sources"][0]
require(digest(frozen["content"].encode()) == frozen["sha256"], "frozen verifier source mismatch")
require('value["retainedFloor"] = 5 if kind == "retained" else 0' in frozen["content"], "missing fixture mutation")
tagged = {}
for path in ("src/Hexalith.EventStore.Server/Events/EventPersister.cs",
             "src/Hexalith.EventStore.Server/Events/SourceProofCodec.cs",
             "src/Hexalith.EventStore.DomainService/VerifiedQueryCaller.cs",
             "src/Hexalith.EventStore.Contracts/Queries/QueryCallerAuthority.cs"):
    data = git("show", TAG_SHA + ":" + path)
    recorded = next(row for row in source["candidate_source"]["main"]["files"] if row["path"] == path)
    require(digest(data) == recorded["sha256"], "tagged production source mismatch")
    tagged[path] = {"sha256": digest(data), "git_blob": recorded["git_blob"]}
persister = git("show", TAG_SHA + ":src/Hexalith.EventStore.Server/Events/EventPersister.cs").decode()
codec = git("show", TAG_SHA + ":src/Hexalith.EventStore.Server/Events/SourceProofCodec.cs").decode()
require('!SourceProofCodec.IsValid(sourceProof, identity, currentSequence, retainedFloor)' in persister
        and 'throw new InvalidOperationException("source_conflict")' in persister
        and 'proof.RetainedFloor == floor' in codec, "missing tagged refusal guard")

sql = commands[4014]["provider_sql"]
hexes = re.findall(r"decode\('([0-9a-f]+)'", sql)
require(len(hexes) == 2, "unexpected SQL mutation")
mutated = json.loads(bytes.fromhex(hexes[0]))
key = bytes.fromhex(hexes[1]).decode()
original = dict(json.loads(commands[4009]["output"]))[key]
expected = copy.deepcopy(original)
expected["retainedFloor"] = 5
require(mutated == expected and mutated["sourceProof"]["retainedFloor"] == 1, "wrong fixture mismatch")
metadata_sha = digest(canonical(mutated))
require(metadata_sha == "c03b45fefe2269d240c01e9b2aedf6763b1a63565a3f75b30037e6332f3e0538", "mutated metadata bytes changed")
restore_case = receipts["post-upgrade-restore"]["cases"][0]
restore_evidence = receipts["post-upgrade-restore"]["execution_evidence"]["cases"][0]
for phase in ("before", "restored_inventory", "appended_inventory", "after"):
    row = next(row for row in restore_evidence["observations"][phase] if row["key"] == key)
    require(row["sha256"] == metadata_sha and row["floor"] == 5 and row["sequence"] == 12,
            "restore metadata changed")
require(restore_case["assertions"]["failed"] == 3 and restore_case["disposition"] == "compatible"
        and restore_case["outcome"] == "effect", "restore label observation changed")

def probe(number):
    row = commands[number]
    require(digest(row["output"].encode()) == row["output_sha256"], "changed probe output")
    return {"command_id": number, "argv": row["argv"], "exit_code": row["exit_code"],
            "output_sha256": row["output_sha256"], "result": json.loads(row["output"])}


seed = probe(3500)
require("comparison-3.110.0" in seed["argv"][1] and seed["result"]["observation"]["committed"] == 0,
        "wrong failing seed writer")
require(counter(seed["result"]["measurement"]["checks"]) == {"attempted": 2, "passed": 0, "failed": 2},
        "wrong attempted seed checks")
deadline_logs = [{"command_id": number, "output_sha256": commands[number]["output_sha256"],
                  "lines": [line for line in commands[number]["output"].splitlines()
                            if "Failed to update host registration: context deadline exceeded" in line]}
                 for number in (3501, 3503)]
require(all(row["lines"] for row in deadline_logs), "missing SQLite registration evidence")
require("500 (Internal Server Error)" in commands[3502]["output"], "missing seeder transport failure")
append = probe(4038)
require(append["result"]["observation"]["accepted"] is False
        and "Stage=EventsStored" in append["result"]["observation"]["error"], "wrong restore failure")

after = read(FINAL / "outer-shared-after-canonical-v4.json")
shared = []
before_paths = [FINAL / "outer-shared-before-canonical-v4.json", OLD / "initial-attempt/outer-shared-before.json"]
for path, measured in zip(before_paths, after["comparisons"]):
    before = read(path)
    require(digest(path.read_bytes()) == measured["before_sha256"], "wrong outer baseline")
    b = {row["id"]: row for row in before["containers"]}
    a = {row["id"]: row for row in after["containers"]}
    removed, added = sorted(b.keys() - a.keys()), sorted(a.keys() - b.keys())
    changed = sorted(k for k in a.keys() & b.keys() if a[k] != b[k])
    require(removed == sorted(row["id"] for row in measured["removed"])
            and added == sorted(row["id"] for row in measured["added"])
            and len(changed) == len(measured["changed"]), "outer delta mismatch")
    shared.append({"baseline": str(path.relative_to(ROOT)), "before": len(b), "after": len(a),
                   "removed": removed, "added": added, "changed": changed,
                   "entire_set_unchanged": False})
require([r["before"] for r in shared] == [13, 5] and [r["after"] for r in shared] == [9, 9], "wrong outer frame")
owned = set(execution["owned_containers"])
require(not owned.intersection(shared[0]["removed"]), "outer removed overlaps recorded V4 owned set")

api_paths = ["src/Hexalith.EventStore.DomainService/VerifiedQueryCaller.cs",
             "src/Hexalith.EventStore.Contracts/Queries/QueryCallerAuthority.cs"]
require(all(b"OriginatingContext" not in git("show", TAG_SHA + ":" + path) for path in api_paths),
        "unexpected newer API in candidate")
api_commit = "6c56c27bc1a8d63c83f9631f7162cf79fdde0a1e"
require(all(b"OriginatingContext" in git("show", api_commit + ":" + path) for path in api_paths),
        "missing successor API source")
api_validation_path = ROOT / "_bmad-output/implementation-artifacts/evidence/6-4-eventstore-query-authority-20261020/final-validation.json"
api_validation = read(api_validation_path)
require(api_validation["combined_focused_passed"] == 422, "changed API source evidence")
require(all(d["decision"] == "pending" for d in decisions["decisions"])
        and decisions["conformance"]["status"] == "pending", "owner state changed")

report = {
    "scope": "retained canonical V4 causal analysis; no runtime execution, repair or qualification",
    "qualified": False, "p1r_usable": False,
    "historical_results": {"lanes": 19, "cases": case_count, "main": totals,
                           "restore": recovery["candidate-restore"]["assertions"],
                           "cleanup": recovery["container-cleanup"]["assertions"], "failed_checks": failures},
    "candidate": {"selection": candidate, "archives_rehashed": archive_checks,
                  "signature_scope": "retained signature observations only; no fresh signature verification",
                  "tagged_source_files": tagged, "verifier_source_sha256": frozen["sha256"]},
    "classification": {
        "verifier_fixture_defect": {"status": "demonstrated invalid positive fixture",
            "mutation_command_id": 4014, "metadata": mutated, "metadata_sha256": metadata_sha,
            "guard": "tagged SourceProofCodec.IsValid requires proof.RetainedFloor == metadata.RetainedFloor; EventPersister rejects source_conflict",
            "causal_limit": "guard refusal inferred from exact input and tagged code; raw exception message remains redacted",
            "append": append, "restarted_assertion": probe(4055),
            "main_failed_checks": 3, "recovery_failed_checks": 2,
            "count_scope": "recovery failures repeat the same append/restart observations; not two new runtime incidents"},
        "verifier_reporting_defect": {"case_outcome": restore_case["outcome"],
            "case_disposition": restore_case["disposition"], "failed": 3,
            "receipt_execution": receipts["post-upgrade-restore"]["execution"],
            "receipt_compatibility": receipts["post-upgrade-restore"]["compatibility"],
            "limit": "incorrect case labels did not grant passing receipt or qualification"},
        "environmental_seed_failure": {"status": "observed discovery deadlines; exact underlying host/SQLite cause unproven",
            "seed": seed, "logs": deadline_logs, "main_failed_checks": 6,
            "unattempted_seed_checks": 4,
            "limit": "3.110.0 seeder failed before tagged 3.125.1 reader; cannot isolate source/package regression"},
        "shared_host_drift": {"status": "failed preservation; actor attribution unavailable", "comparisons": shared,
            "removed_overlap_with_recorded_v4_owned_containers": [],
            "limit": "owned cleanup 7/7 and private audit do not prove preservation of the outer host"},
        "runtime_defects": {"proven_by_these_failures": [],
            "limit": "no production repair justified; absence of an isolated defect does not qualify 3.125.1"}},
    "separate_story_6_4_api": {"present_in_3_125_1": False, "implementation_commit": api_commit,
        "retained_focused_source_passed": 422, "published_candidate_selected": None,
        "qualification": "separate successor package/source and consumer qualification required",
        "owner_acceptance": "not inferred"},
    "read_input_sha256": PINS,
}
print(json.dumps(report, indent=2, sort_keys=True))
