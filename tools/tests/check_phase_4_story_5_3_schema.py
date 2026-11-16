"""Explicit Draft 2020-12 contract checks; no synthetic business authority.

Run separately with the already available jsonschema development tool. Contract
PASS objects below test representability only; no validator or lifecycle writer
accepts them as genuine evidence. Missing jsonschema is a hard check failure.
"""

import copy
import json
from pathlib import Path
import sys

try:
    from jsonschema import Draft202012Validator
except ImportError as error:
    raise SystemExit("Schema check requires the existing jsonschema development tool: " + str(error))

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
import phase_4_story_5_3 as A
import phase_4_transition as C


def fixtures():
    adapter = {"adapter": A.ADAPTER, "inventory_sha256": "0" * 64, "root_tree": "a" * 40,
               "root_membership_sha256": "0" * 64, "submodule_membership_sha256": "0" * 64,
               "source_count": 1, "validator_sha256": "0" * 64, "plan_sha256": "0" * 64}
    claim = {"story_key": A.STORY_IDENTITY, "story_id": "5.3", "spec_path": "_bmad-output/implementation-artifacts/spec-5-3-contract.md",
             "target": "spec", "scope": "slice", "nfr_ids": ["NFR3", "NFR4"], "high_risk_nfr_ids": ["NFR3", "NFR4"], "coverage_sha256": "0" * 64}
    lanes = []
    for label, project, suffixes in (*A.LANES, ("script-smoke-preflight", "", ())):
        selectors = {project + "." + suffix: 1 for suffix in suffixes}
        count = max(1, len(selectors))
        if label != "script-smoke-preflight":
            lanes.append({"id": label + "-build", "result": "PASS", "counts": None})
        lanes.append({"id": label, "result": "PASS", "counts": {
            "total": count, "passed": count, "failed": 0, "skipped": 0, "errors": 0, "not_run": 0, "selectors": selectors}})
    lanes.extend({"id": label, "result": "PASS", "counts": None} for label in ("scanner-capabilities", "solution-release-build"))
    evidence = {"schema": A.SCHEMA, "adapter": A.ADAPTER, "result": "PASS", "binding": adapter,
                "lanes": lanes, "capabilities": [{"id": name, "detected": True} for name in A.CAPABILITIES],
                "gaps": [], "notes": [], "diagnostics": [],
                "execution_inputs": {"packages_sha256": "0" * 64, "toolchain_sha256": "0" * 64}}
    binding = {"repository": C.REPOSITORY, "transition": "high-risk-nfr-story-done", "head_sha": "a" * 40,
               "request_sha256": "0" * 64, "subject_sha256": "0" * 64, "workflow_sha256": "0" * 64,
               "inputs": [{"path": A.VALIDATOR, "sha256": "0" * 64}], "story": claim, "story_adapter": adapter}
    result = {"schema": C.RESULT_SCHEMA, "phase": "evaluate", "result": "PASS", "codes": [], "binding": binding,
              "required_level": "single-maintainer-attested", "achieved_level": "single-maintainer-attested",
              "downstream_minimum": "single-maintainer-attested", "approvals": [], "approval_payloads": [],
              "predecessors": [], "seal": None, "story_evidence": evidence}
    return evidence, result


def main():
    schema = json.loads((ROOT / "tools/phase-4-transition.schema.json").read_text())
    Draft202012Validator.check_schema(schema)
    validator = Draft202012Validator(schema)
    evidence, governed = fixtures()
    A.validate_report(evidence, evidence["binding"])
    checks = 0
    def accept(value):
        nonlocal checks
        validator.validate(value)
        checks += 1
    def reject(value, runtime=False):
        nonlocal checks
        assert not validator.is_valid(value), "Malformed contract was accepted"
        if runtime:
            try:
                A.validate_report(value, value["binding"])
            except C.Refusal:
                pass
            else:
                raise AssertionError("Runtime accepted malformed fixed inventory")
        checks += 1
    accept(evidence)
    accept(governed)
    failure = copy.deepcopy(evidence)
    failure.update(result="FAIL", gaps=["scanner-capability:xml-cdata"])
    next(row for row in failure["lanes"] if row["id"] == "scanner-capabilities")["result"] = "FAIL"
    failure["capabilities"][4]["detected"] = False
    accept(failure)
    consumed = copy.deepcopy(governed)
    consumed.update(phase="consume", seal={"run_id": 7, "attempt": 2, "artifact_id": 9, "ci_app_id": C.ACTIONS_APP_ID})
    accept(consumed)
    completion = {"schema": "hexalith.eventstore.phase-4-story-completion.v1", "result": "PASS", "codes": [],
                  "disposition": "assured-story-slice", "claim": governed["binding"]["story"], "verification": consumed,
                  "story_evidence": evidence, "message": "Contract fixture only."}
    accept(completion)
    for disposition in ("ordinary-slice", "historical-unchanged"):
        ordinary = {**completion, "disposition": disposition, "verification": None, "story_evidence": None}
        accept(ordinary)
    refused = {**completion, "result": "FAIL", "codes": ["STORY_EVIDENCE_FAILED"], "disposition": "refused",
               "verification": None, "story_evidence": failure}
    accept(refused)
    for field in ("lanes", "capabilities"):
        unknown = copy.deepcopy(failure)
        unknown[field][0]["id"] = "invented"
        reject(unknown, True)
        duplicate = copy.deepcopy(failure)
        duplicate[field][1] = copy.deepcopy(duplicate[field][0])
        if field == "lanes":
            duplicate[field][1]["result"] = "FAIL"
        else:
            duplicate[field][1]["detected"] = False
        reject(duplicate, True)
        missing = copy.deepcopy(evidence)
        missing[field].pop()
        reject(missing, True)
    for field in ("story_evidence",):
        absent = copy.deepcopy(governed)
        del absent[field]
        reject(absent)
        for value in (None, failure):
            changed = copy.deepcopy(governed)
            changed[field] = value
            reject(changed)
    no_binding = copy.deepcopy(governed)
    del no_binding["binding"]["story_adapter"]
    reject(no_binding)
    print(f"Draft 2020-12 schema fixtures: {checks} passed; contract data confers no business authority.")


if __name__ == "__main__":
    main()
